using App.Core.Expressions;
using App.Core.Interpolation;
using App.Core.Model;
using App.Core.Thermo;

namespace App.Core.Simulation;

/// <summary>
/// Drives one cylinder through the four-stroke cycle. Port of <c>TEngine2z.InitVars</c>
/// and <c>TEngine2z.Run</c> (ICEngine2Z.pas:639-931, 940-1054), with the cycle loop from
/// <c>TFMain.Simulate</c> (Main.pas:281-377).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Step"/> is one call of <c>Run</c>: pick the state, install the equation set
/// if the state changed, integrate, refresh the gas, take the manifolds' answer, move the
/// mass across the valves and accumulate the work and heat. The Delphi global
/// <c>Engine2z</c> becomes <see cref="Engine"/>, held per instance.
/// </para>
/// <para>
/// The manifolds arrive through <see cref="IManifoldSource"/> rather than being called
/// directly, so the in-cylinder model can be exercised against the reference run before
/// the wave solver exists.
/// </para>
/// </remarks>
public sealed class CycleSolver
{
    private readonly Engine _engine;
    private readonly IManifoldSource _manifold;
    private readonly IExpressionEvaluator _evaluator;
    private readonly Rkf5Integrator _integrator = new();
    private readonly DerivativeFunction[] _equations = new DerivativeFunction[EsaLimits.MaxEquations];

    private readonly TwoZoneGas _cylinder;
    private readonly TwoZoneGas _plenum;
    private readonly TwoZoneGas _exhaust;
    private readonly TwoZoneGas _atmosphere;

    /// <summary>B46: zero the mass-flow derivatives on entry to compression.</summary>
    private readonly bool _zeroClosedCylinderMassFlow;

    /// <summary>B38: take Woschni's reference conditions at each inlet valve closing.</summary>
    private readonly bool _updateIvcReference;

    /// <summary>B50: compute the manifold gammas, as <c>InitVars</c> does, for the wave solver.</summary>
    private readonly bool _computeManifoldGammas;

    /// <summary>
    /// B77: update the gas at the angle the step finished at, and integrate work and heat
    /// by the trapezoid of the step's two ends.
    /// </summary>
    private readonly bool _endOfStepState;

    /// <summary>B78: reset the burnt volume at every combustion entry.</summary>
    private readonly bool _resetBurntVolume;

    /// <summary>B37: run valve overlap as two zones, the burnt residual and the fresh charge.</summary>
    private readonly bool _gasExchangeZones;

    /// <summary>B79: count each flow through overlap's valves once in the cycle totals.</summary>
    private readonly bool _countValveFlowsOnce;

    /// <summary>B37: which zones the cylinder holds, as of the current overlap step.</summary>
    private OverlapPhase _overlapPhase;

    /// <summary>
    /// B37: the flow derivatives overlap found, put back as it ends, so that the closed
    /// zones of overlap leave the states either side of it as they were.
    /// </summary>
    private (double In, double Out) _flowRatesBeforeOverlap;

    /// <summary>B37: how many zones overlap is running, which sets its equations.</summary>
    private enum OverlapPhase
    {
        /// <summary>Burnt gas only, until the first fresh charge arrives.</summary>
        BurntOnly,

        /// <summary>The burnt residual and the fresh charge, side by side.</summary>
        TwoZone,

        /// <summary>Fresh charge only, if every bit of burnt gas has been expelled.</summary>
        UnburntOnly,
    }

    /// <summary>B77: the work and heat-loss rates at the start of the current step.</summary>
    private (double Work, double BurntHeat, double UnburntHeat) _startRates;

    /// <param name="engine">The engine to simulate.</param>
    /// <param name="manifold">Where the manifold boundary conditions come from.</param>
    /// <param name="evaluator">Evaluates the <c>.eng</c> file's expressions.</param>
    /// <param name="physics">
    /// Which corrections to apply (<c>CORRECTIONS.md</c>). Null is Legacy: the original's
    /// physics, as <c>data/baseline/</c> was produced by it.
    /// </param>
    public CycleSolver(
        Engine engine,
        IManifoldSource manifold,
        IExpressionEvaluator? evaluator = null,
        PhysicsCorrections? physics = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(manifold);

        _engine = engine;
        _manifold = manifold;
        _evaluator = evaluator ?? new CachingExpressionEvaluator();

        // B14: Fehlberg's coefficient in place of the original's transposed digit.
        _integrator.FehlbergCoefficient =
            physics?.IsOn(CorrectionCatalogue.Rkf5Coefficient) ?? false;

        _zeroClosedCylinderMassFlow =
            physics?.IsOn(CorrectionCatalogue.ClosedCylinderMassFlow) ?? false;

        _updateIvcReference = physics?.IsOn(CorrectionCatalogue.IvcReference) ?? false;
        _computeManifoldGammas = physics?.IsOn(CorrectionCatalogue.ManifoldGammas) ?? false;
        _endOfStepState = physics?.IsOn(CorrectionCatalogue.EndOfStepState) ?? false;
        _resetBurntVolume = physics?.IsOn(CorrectionCatalogue.BurntVolumeReset) ?? false;
        _countValveFlowsOnce = physics?.IsOn(CorrectionCatalogue.OverlapValveTotals) ?? false;
        _gasExchangeZones = physics?.IsOn(CorrectionCatalogue.GasExchangeZones) ?? false;

        _cylinder = new TwoZoneGas(engine.Cylinder);
        _plenum = new TwoZoneGas(engine.Plenum);
        _exhaust = new TwoZoneGas(engine.Exhaust);
        _atmosphere = new TwoZoneGas(engine.Atmosphere);

        // B16 and B18 are properties of the gas models themselves, so every gas gets them.
        var analyticDudp = physics?.IsOn(CorrectionCatalogue.AnalyticPressureDerivative) ?? false;
        var residualWeight = physics?.IsOn(CorrectionCatalogue.ResidualMolecularWeight) ?? false;

        foreach (var gas in new[] { _cylinder, _plenum, _exhaust, _atmosphere })
        {
            foreach (var model in new[] { gas.Burnt, gas.Unburnt })
            {
                model.AnalyticPressureDerivative = analyticDudp;
                model.ResidualMolecularWeight = residualWeight;
            }
        }

        Geometry = CylinderGeometry.FromEngine(engine);
        InletValve = ValveMotion.FromValve(engine.Manifold.InletValve);
        ExhaustValve = ValveMotion.FromValve(engine.Manifold.ExhaustValve);

        SparkAdvance = LegacyInterpolation.AtSpeed(
            engine.SparkAngle.Rpm, engine.SparkAngle.Values, engine.Rpm);

        States = CrankAngleStateMap.FromEngine(engine, SparkAdvance);

        Cylinder = new CylinderModel(Geometry, _cylinder, _plenum, engine.WallTemperature)
        {
            Rpm = engine.Rpm,
            WoschniCoefficient = engine.WoshiniCoefficient,
            MotoredVolumeAtCallAngle = physics?.IsOn(CorrectionCatalogue.WoschniMotoredAngle) ?? false,
            TrueSweptVolume = physics?.IsOn(CorrectionCatalogue.WoschniSweptVolume) ?? false,
            ClampPressureRiseTerm = physics?.IsOn(CorrectionCatalogue.WoschniNegativeVelocity) ?? false,
            PressureRiseTermInCombustionOnly =
                physics?.IsOn(CorrectionCatalogue.WoschniCombustionTerm) ?? false,
            HonourVariableGamma = physics?.IsOn(CorrectionCatalogue.SingleZoneGamma) ?? false,
            VariableGamma = engine.VariableGamma,
            SingleZoneTrialState = physics?.IsOn(CorrectionCatalogue.SingleZoneTrialState) ?? false,
            GasExchangeZones = physics?.IsOn(CorrectionCatalogue.GasExchangeZones) ?? false,
        };
    }

    /// <summary>The engine being simulated. Mutated in place, as the original mutated its global.</summary>
    public Engine Engine => _engine;

    public CylinderGeometry Geometry { get; }

    public CrankAngleStateMap States { get; }

    public ValveMotion InletValve { get; }

    public ValveMotion ExhaustValve { get; }

    public CylinderModel Cylinder { get; }

    /// <summary>Spark advance in degrees before top dead centre at the running speed.</summary>
    public double SparkAdvance { get; }

    /// <summary>Raised once per completed step, for progress reporting and trace capture.</summary>
    public event Action<CycleSolver>? StepCompleted;

    // -----------------------------------------------------------------------
    // Initialisation
    // -----------------------------------------------------------------------

    /// <summary>
    /// The burnt-gas equilibrium solvers' counters, one per gas zone, for
    /// <see cref="RunDiagnostics"/>.
    /// </summary>
    /// <summary>
    /// Gas-property curve-fit evaluations clamped to their range, summed over both zones of
    /// every gas, for <see cref="RunDiagnostics"/> (ISSUES.md B20).
    /// </summary>
    public long GasPropertyTemperatureClamps =>
        new[] { _cylinder, _plenum, _exhaust, _atmosphere }
            .Sum(gas => gas.Burnt.TemperatureClamps + gas.Unburnt.TemperatureClamps);

    public IEnumerable<EquilibriumDiagnostics> EquilibriumDiagnostics =>
        new[] { _cylinder, _plenum, _exhaust, _atmosphere }
            .Select(gas => gas.Burnt.Equilibrium)
            .OfType<EquilibriumSolver>()
            .Select(solver => solver.Diagnostics);

    /// <summary>
    /// Sets up every gas and the integrator. Port of <c>TEngine2z.InitVars</c>.
    /// </summary>
    /// <returns>
    /// Whether both cam profiles loaded, Delphi's <c>AllOK</c>. The original returns this
    /// through a <c>var</c> parameter and the caller refuses to run when it is false.
    /// </returns>
    public bool Initialise()
    {
        var engine = _engine;

        engine.TwoZoneInitialised = false;
        engine.TwoZoneOverlap = false;

        // InitVars hard-codes a one degree step. Main.pas never overrides it, so the
        // reference run is at one degree and so is this.
        engine.CrankAngleStep = 1;
        engine.CrankAngularVelocity = engine.Rpm * Math.PI / 30;
        engine.SweptVolume = Geometry.SweptVolume;
        engine.ForcedEgr = 0;

        engine.Integration.EquationCount = EsaLimits.MaxEquations;
        engine.Integration.Dx = engine.CrankAngleStep * Math.PI / 180;

        Cylinder.CrankAngularVelocity = engine.CrankAngularVelocity;

        InitialiseAtmosphere();
        InitialisePlenum();
        InitialiseCylinder();
        InitialiseExhaust();

        // Not conditions at inlet valve closing despite the names: the plenum's, fixed
        // here for the whole run. See ISSUES.md B38.
        // InitVars: PlenumT := Plenum.Tgas. The manifold solver reads this when it lays
        // out its grids on the first step.
        engine.Manifold.PlenumTemperature = _plenum.GasTemperature();

        // InitVars computes GammaIn and GammaEx here and nothing ever reads them. B50 hands
        // them to the wave solver, and only then are they computed, because the property
        // models' solves would otherwise disturb Legacy's call history. Neither is computed
        // as the original wrote it, because both of those are wrong (ISSUES.md B50):
        if (_computeManifoldGammas)
        {
            // The unburnt model's first evaluation carries B18's transient - it starts the
            // residual iteration from zeros - so the settled second one is taken. The
            // original's would have been 1.3718 on the baseline engine; settled it is 1.3557.
            _cylinder.Unburnt.Gamma(engine.Plenum.PGas, engine.Manifold.PlenumTemperature);
            engine.Manifold.GammaIn = _cylinder.Unburnt.Gamma(
                engine.Plenum.PGas, engine.Manifold.PlenumTemperature);

            // The original asks at Exh.TGas, which is the unburnt 293.15 K because the
            // exhaust holds no burnt mass yet, and so gets cold products. The exhaust
            // temperature is in Tb, raw from the .exh table and therefore Celsius (B66).
            engine.Manifold.GammaEx = _cylinder.Burnt.Gamma(
                engine.Exhaust.PGas, engine.Exhaust.Tb + 273.15);
        }

        engine.PressureAtIvc = engine.Plenum.PGas;
        engine.TemperatureAtIvc = _plenum.GasTemperature();
        engine.VolumeAtIvc = Geometry.Volume(States.InletClose * Math.PI / 180);

        Cylinder.PressureAtInletValveClosing = engine.PressureAtIvc;
        Cylinder.TemperatureAtInletValveClosing = engine.TemperatureAtIvc;
        Cylinder.VolumeAtInletValveClosing = engine.VolumeAtIvc;

        var y = engine.Integration.Y;
        y[0] = 0;
        y[1] = engine.Cylinder.PGas;
        y[2] = 2200;
        y[3] = engine.Cylinder.Tb;
        engine.TimeStep = 0;

        InitialiseMasses();
        InitialiseAccumulators();

        return engine.Manifold.InletValve.Profile.ProfileOk
               && engine.Manifold.ExhaustValve.Profile.ProfileOk;
    }

    private void InitialiseAtmosphere()
    {
        var atm = _engine.Atmosphere;

        atm.Mb = 0;
        atm.Tb = atm.Tu;

        // hu has never been computed at this point - no ReturnProps call is made on the
        // atmosphere - so this copies zero. Only the unreachable gas-exchange equations
        // read hin, so nothing depends on it. See ISSUES.md B44.
        atm.HIn = atm.Hu;
    }

    private void InitialisePlenum()
    {
        var engine = _engine;
        var plenum = engine.Plenum;

        plenum.Mb = 0;
        plenum.PGas = _evaluator.Evaluate(
            engine.Manifold.PlenumPressureFunction.Expression, engine.Rpm);
        plenum.Tb = _atmosphere.GasTemperature();
        plenum.Tu = _atmosphere.GasTemperature();

        Setup(_plenum);

        // The plenum is updated over the cylinder's volume at bottom dead centre, which
        // is not the plenum's volume; it is a stand-in that makes the mass work out.
        var bottomDeadCentre = Geometry.Volume(Math.PI);
        _plenum.UpdateUB(bottomDeadCentre, 0, bottomDeadCentre, plenum.PGas, plenum.Tu);

        plenum.HIn = plenum.Hu;
    }

    private void InitialiseCylinder()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;

        cylinder.ThetaSpark = -SparkAdvance;
        cylinder.PGas = engine.Plenum.PGas;
        cylinder.Tb = _plenum.GasTemperature();
        cylinder.Tu = _plenum.GasTemperature();

        Setup(_cylinder);

        cylinder.HIn = cylinder.Hu;
    }

    private void InitialiseExhaust()
    {
        var engine = _engine;
        var exhaust = engine.Exhaust;
        var table = engine.Manifold.ExhaustBack;

        // The .exh table holds gauge pressure in kPa; TExhaustPandT.Pres adds
        // atmospheric to make it absolute (ExhBackPandT.pas:72).
        exhaust.PGas = (LegacyInterpolation.AtSpeed(table.Rpm, table.Pressure, engine.Rpm) * 1000)
                       + engine.Atmosphere.PGas;

        exhaust.Tu = 293.15;

        // Deliberately not converted. The .exh column is headed TEMP[C] and
        // TExhaustPandT.Temp returns it raw, so the original uses Celsius wherever a
        // temperature in kelvin is wanted. See ISSUES.md B66.
        exhaust.Tb = LegacyInterpolation.AtSpeed(table.Rpm, table.Temperature, engine.Rpm);

        Setup(_exhaust);

        exhaust.HIn = exhaust.Hu;
    }

    private void Setup(TwoZoneGas gas)
    {
        var fuel = gas.State.Fuel;

        gas.Unburnt.Setup(0, fuel.C, fuel.H, fuel.O, fuel.N, 1 / fuel.Lambda, _engine.ForcedEgr);
        gas.Burnt.Setup(0, fuel.C, fuel.H, fuel.O, fuel.N, 1 / fuel.Lambda, _engine.ForcedEgr);
    }

    private void InitialiseMasses()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;

        // A 90 per cent volumetric efficiency guess, through the ideal gas law at the
        // universal constant for air.
        var charge = 0.9 * cylinder.PGas * engine.SweptVolume
                     / EsaLimits.RUniversal / _cylinder.GasTemperature();

        engine.TotalMassInInletValve = charge;
        engine.TotalMass = charge;

        cylinder.Fuel.M = 1 / cylinder.Fuel.Lambda * engine.TotalMassInInletValve
                          / (cylinder.Fuel.AFRatio + 1);

        engine.AtmosphericMass = engine.Plenum.PGas / engine.Plenum.RGas / engine.Plenum.Tu
                                 * Geometry.Volume(Math.PI);

        cylinder.MGas = engine.TotalMass;
        cylinder.Mb = 0;
        cylinder.Mu = engine.TotalMass;
        cylinder.VGas = engine.VolumeAtIvc;
        cylinder.Vu = engine.VolumeAtIvc;

        engine.MassIn = 0;
        engine.MassOut = 0;

        // The original's own comment: "This is just for initialization : set to 0 for
        // Integration". The first state change to Compression overwrites it.
        engine.TotalMassInInletValve = engine.AtmosphericMass;
        engine.TotalMassOutExhaustValve = 0;
    }

    private void InitialiseAccumulators()
    {
        var engine = _engine;

        engine.Work = 0;
        engine.PumpingWork = 0;

        // Not a typo: PMax starts at 1e7 Pa, so the running maximum only ever falls to a
        // real value once a cycle exceeds it. See ISSUES.md B45.
        engine.PeakPressure = 10000000;
        engine.PeakTemperature = 0;
        engine.PeakInletVelocity = 0;
        engine.PeakExhaustVelocity = 0;
    }

    // -----------------------------------------------------------------------
    // One step
    // -----------------------------------------------------------------------

    /// <summary>
    /// Advances the cycle by one crank-angle step. Port of <c>TEngine2z.Run</c>.
    /// </summary>
    public void Step()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;
        var state = engine.Integration;

        state.X = engine.CrankAngle * Math.PI / 180;
        engine.State = States.StateAt(engine.CrankAngle);

        Cylinder.State = engine.State;
        Cylinder.CrankAngleRadians = state.X;

        if (engine.State != engine.OldState)
        {
            engine.OldState = engine.State;

            if (engine.ZoneCount == 1)
            {
                EnterSingleZoneCompression();
            }
            else
            {
                EnterTwoZoneState();
            }
        }

        // Re-tested every step, not only on entry: the burning equations divide by the
        // burnt mass, so until the first step has produced any they cannot be used.
        if (engine.ZoneCount == 2 && engine.State == EngineState.Combustion)
        {
            InstallCombustionEquations(cylinder.Mb == 0);
        }

        if (engine.ZoneCount == 2 && engine.State == EngineState.Overlap && _gasExchangeZones)
        {
            PrepareOverlapStep();
        }

        if (_endOfStepState)
        {
            // Under B77 the gas already holds this step's starting state - the last step's
            // end, with the pressure correction applied - so these are the start rates.
            _startRates = (
                Cylinder.WorkRate(state.X, state.Y),
                Cylinder.BurntHeatLossRate(state.X),
                Cylinder.UnburntHeatLossRate(state.X));
        }

        _integrator.Step(state, _equations);

        RefreshGasFromIntegrator();

        if (engine.ZoneCount == 1)
        {
            CollapseToSingleZone();
        }

        engine.Emissions.CopyFrom(_cylinder.Burnt.Equilibrium!.State.X);

        var manifold = StepManifolds();

        // The plenum is refreshed at zero volume, so only its pressure and temperature
        // carry; the masses and volumes it computes are meaningless and unread.
        _plenum.UpdateUB(
            0,
            Geometry.VolumeRatePerRadian(state.X),
            0,
            manifold.InletPressure,
            manifold.InletTemperature);

        // The mass-transfer pressure correction is applied in the single-zone model
        // throughout, and in the two-zone model only during overlap.
        if (engine.ZoneCount == 1 || (engine.State == EngineState.Overlap && !_gasExchangeZones))
        {
            cylinder.PGas += manifold.PressureCorrection;
            state.Y[1] = cylinder.PGas;
        }

        cylinder.MGas += engine.MassIn - engine.MassOut;

        if (cylinder.MGas < 0)
        {
            throw new EngineException("Negative engine gas mass.");
        }

        if (engine.ZoneCount == 1)
        {
            engine.TotalMassInInletValve += engine.MassIn;
            engine.TotalMassOutExhaustValve += engine.MassOut;
        }
        else
        {
            MoveMassBetweenZones();
        }

        AccumulateWorkAndHeat();

        StepCompleted?.Invoke(this);
    }

    /// <summary>
    /// Single-zone state entry. The original only ever installs equations and resets
    /// accumulators on the transition into compression, so the single-zone model uses one
    /// equation set for the whole cycle.
    /// </summary>
    private void EnterSingleZoneCompression()
    {
        var engine = _engine;

        if (engine.State != EngineState.Compression)
        {
            return;
        }

        ResetCycleAccumulators();
        InstallEquations(CylinderModel.Zero, Cylinder.PressureRateSingleZone, CylinderModel.Zero, CylinderModel.Zero);
        TakeIvcReference();
    }

    /// <summary>
    /// B38: Woschni's reference conditions become the cylinder's state as the inlet valve
    /// closes - the last intake step's - where Legacy keeps the plenum's from
    /// initialisation for the whole run.
    /// </summary>
    private void TakeIvcReference()
    {
        if (!_updateIvcReference)
        {
            return;
        }

        var engine = _engine;

        engine.PressureAtIvc = engine.Cylinder.PGas;
        engine.TemperatureAtIvc = _cylinder.GasTemperature();
        engine.VolumeAtIvc = engine.Cylinder.VGas;

        Cylinder.PressureAtInletValveClosing = engine.PressureAtIvc;
        Cylinder.TemperatureAtInletValveClosing = engine.TemperatureAtIvc;
        Cylinder.VolumeAtInletValveClosing = engine.VolumeAtIvc;
    }

    private void EnterTwoZoneState()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;

        if (!engine.TwoZoneInitialised)
        {
            engine.TwoZoneInitialised = true;
            cylinder.Mu = cylinder.MGas;
            cylinder.Mb = 0;
        }

        switch (engine.State)
        {
            case EngineState.Compression:
                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateUnburnt,
                    CylinderModel.Zero,
                    Cylinder.UnburntTemperatureRate);

                _cylinder.Burnt.Equilibrium!.Frozen = false;
                ResetCycleAccumulators();
                TakeIvcReference();

                // The original's mass block has no case for the closed states, so these
                // keep the last intake step's value and the previous cycle's last exhaust
                // step's, and expansion uses the exhaust one as though gas were still
                // leaving. Legacy reproduces that; B46 closes the cylinder. Exhaust and
                // intake set their own on their first step.
                if (_zeroClosedCylinderMassFlow)
                {
                    cylinder.DmInDTheta = 0;
                    cylinder.DmOutDTheta = 0;
                }

                break;

            case EngineState.Combustion:
                InstallCombustionEquations(useUnburntEquations: false);

                // The burnt zone starts at the adiabatic flame temperature for the
                // unburnt state, found by isenthalpic iteration.
                engine.Integration.Y[2] = InitialBurntTemperature(cylinder.PGas, cylinder.Tu);

                // The original sets only the burnt temperature here. The burnt volume is
                // zeroed once, in InitVars, so the first two-zone burn starts from nothing
                // and every later one from the last burn's end-of-burn volume - larger
                // than the cylinder at the spark - and burns with no unburnt volume at all,
                // creating about 15 per cent of the fuel energy. Legacy reproduces it; B78
                // starts every burn as the first one starts. See ISSUES.md B78.
                if (_resetBurntVolume)
                {
                    engine.Integration.Y[0] = 0;
                }
                break;

            case EngineState.Expansion:
                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateBurnedDown,
                    Cylinder.BurntTemperatureRateBurnedDown,
                    CylinderModel.Zero);

                _cylinder.Burnt.Equilibrium!.Frozen = false;
                cylinder.Mb = cylinder.MGas;
                cylinder.Mu = 0;
                cylinder.Vb = cylinder.VGas;
                cylinder.Vu = 0;
                break;

            case EngineState.Exhaust:
                // No equations installed: exhaust inherits expansion's. See ISSUES.md B40.
                engine.TotalMassOutExhaustValve = 0;
                break;

            case EngineState.Overlap:
                _cylinder.Burnt.Equilibrium!.Frozen = true;

                if (_gasExchangeZones)
                {
                    // B37: overlap opens with the exhaust's burnt gas alone. Its zones are
                    // integrated closed and the valves' flows applied after each step
                    // (ApplyOverlapFlows), so the flow derivatives are zero through it and
                    // put back as intake begins.
                    _overlapPhase = OverlapPhase.BurntOnly;
                    _flowRatesBeforeOverlap = (cylinder.DmInDTheta, cylinder.DmOutDTheta);
                    cylinder.DmInDTheta = 0;
                    cylinder.DmOutDTheta = 0;
                }
                else
                {
                    // The gas-exchange set belongs here and is commented out in the
                    // original, so two-zone overlap runs the single-zone constant-gamma
                    // pressure equation. See ISSUES.md B37.
                    InstallEquations(
                        CylinderModel.Zero,
                        Cylinder.PressureRateSingleZone,
                        CylinderModel.Zero,
                        CylinderModel.Zero);
                }

                engine.BurntMassOutInlet = 0;
                engine.UnburntMassOutExhaust = 0;
                break;

            case EngineState.Intake:
                // The zones are mixed as the exhaust valve closes. The original weights the
                // fresh charge at the plenum's temperature, but the unburnt equations
                // replace that with the integrator's own temperature on their first call,
                // so its mix in effect is the one the overlap tail made: the whole charge on
                // the ideal gas law at the cylinder's pressure (MoveMassDuringOverlap).
                // Under B37 overlap no longer makes it, so it is made here. See ISSUES.md B37.
                if (_gasExchangeZones)
                {
                    (cylinder.DmInDTheta, cylinder.DmOutDTheta) = _flowRatesBeforeOverlap;
                    MixZonesAtExhaustClosing();
                }
                else
                {
                    cylinder.Tu = ((cylinder.Mb * cylinder.Tb) + (cylinder.Mu * engine.Plenum.Tu)) / cylinder.MGas;
                }

                cylinder.Mu = cylinder.MGas;
                cylinder.Vb = 0;
                cylinder.Vu = cylinder.VGas;

                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateUnburnt,
                    CylinderModel.Zero,
                    Cylinder.UnburntTemperatureRate);

                cylinder.Mb = 0;
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// B37: the mix at exhaust valve closing, the one the original makes in effect - the
    /// whole charge at the cylinder's pressure and volume, on the ideal gas law with the
    /// unburnt gas constant, which is what intake and compression take it to be.
    /// </summary>
    private void MixZonesAtExhaustClosing()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;
        var y = engine.Integration.Y;
        var volume = Geometry.Volume(engine.Integration.X);
        var temperature = cylinder.Tu;

        for (var iteration = 0; iteration < 3; iteration++)
        {
            temperature = y[1] * volume / (cylinder.MGas * _cylinder.Unburnt.GasConstant(y[1], temperature));
        }

        cylinder.Tu = temperature;
        y[2] = temperature;
        y[3] = temperature;
    }

    /// <summary>
    /// B37: installs overlap's equations for this step from the zones the cylinder holds.
    /// Re-tested every step, as combustion re-tests its burnt mass, because the fresh zone
    /// appears part way through overlap and either zone can empty.
    /// </summary>
    /// <remarks>
    /// Every set is closed - the flow derivatives are zero through overlap - because the
    /// step's flows are applied after it by <see cref="ApplyOverlapFlows"/>. The two-zone
    /// set divides by both zones' masses and volumes, so a zone on its own runs its own
    /// single-zone set. See ISSUES.md B37.
    /// </remarks>
    private void PrepareOverlapStep()
    {
        switch (_overlapPhase)
        {
            case OverlapPhase.BurntOnly:
                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateBurnedDown,
                    Cylinder.BurntTemperatureRateBurnedDown,
                    CylinderModel.Zero);
                break;

            case OverlapPhase.UnburntOnly:
                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateUnburnt,
                    CylinderModel.Zero,
                    Cylinder.UnburntTemperatureRate);
                break;

            default:
                InstallEquations(
                    Cylinder.BurntVolumeRateGasExchangeZones,
                    Cylinder.PressureRateGasExchangeZones,
                    Cylinder.BurntTemperatureRateGasExchangeZones,
                    Cylinder.UnburntTemperatureRateGasExchangeZones);
                break;
        }
    }

    private void InstallCombustionEquations(bool useUnburntEquations)
    {
        if (useUnburntEquations)
        {
            InstallEquations(
                CylinderModel.Zero,
                Cylinder.PressureRateUnburnt,
                CylinderModel.Zero,
                Cylinder.UnburntTemperatureRate);
        }
        else
        {
            InstallEquations(
                Cylinder.BurntVolumeRateBurning,
                Cylinder.PressureRateBurning,
                Cylinder.BurntTemperatureRateBurning,
                Cylinder.UnburntTemperatureRateBurning);
        }
    }

    private void InstallEquations(
        DerivativeFunction burntVolume,
        DerivativeFunction pressure,
        DerivativeFunction burntTemperature,
        DerivativeFunction unburntTemperature)
    {
        _equations[0] = burntVolume;
        _equations[1] = pressure;
        _equations[2] = burntTemperature;
        _equations[3] = unburntTemperature;
    }

    /// <summary>
    /// Resets the per-cycle accumulators and fixes the fuel mass from the air that came
    /// in on the previous cycle. Both zone models do this on entry to compression.
    /// </summary>
    private void ResetCycleAccumulators()
    {
        var engine = _engine;
        var fuel = engine.Cylinder.Fuel;

        engine.Work = 0;
        engine.PumpingWork = 0;
        engine.WorkDone = 0;
        engine.HeatLoss = 0;
        engine.FuelEnergy = 0;
        engine.PeakPressure = 0;
        engine.PeakTemperature = 0;
        engine.PeakInletVelocity = 0;
        engine.PeakExhaustVelocity = 0;

        engine.NewAirMass = engine.TotalMassInInletValve;
        engine.TotalMassInInletValve = 0;
        engine.TotalMassOutExhaustValve = 0;

        fuel.M = 1 / fuel.Lambda * engine.NewAirMass / (fuel.AFRatio + 1);
    }

    /// <summary>
    /// The angle, in radians, that the integrator's solution belongs to once a step is
    /// done: the step's own start in Legacy, as the original updates there although the
    /// solution has moved on by a step (ISSUES.md B77); the step's end under B77.
    /// </summary>
    private double SolutionAngle => _endOfStepState
        ? (_engine.CrankAngle + _engine.CrankAngleStep) * Math.PI / 180
        : _engine.Integration.X;

    /// <summary>
    /// Writes the integrator's result back into the gas through the update method that
    /// matches the state. Port of the <c>case state of</c> block after <c>Integrate</c>.
    /// </summary>
    /// <remarks>
    /// The original updates at <c>VCyl(x)</c>, the angle the step started from, with the
    /// solution at the angle it finished at: the volume, its rate and the burnt fraction
    /// one step behind the pressure and temperatures. Legacy reproduces it; under B77 the
    /// update is at the solution's own angle. See ISSUES.md B77.
    /// </remarks>
    private void RefreshGasFromIntegrator()
    {
        var engine = _engine;
        var y = engine.Integration.Y;
        var x = SolutionAngle;
        var volume = Geometry.Volume(x);
        var rate = Geometry.VolumeRatePerRadian(x);

        switch (engine.State)
        {
            case EngineState.Combustion:
                _cylinder.UpdateB(x, volume, rate, y[0], y[1], y[2], y[3]);
                break;

            case EngineState.Intake or EngineState.Compression:
                _cylinder.UpdateUB(volume, rate, y[0], y[1], y[3]);
                break;

            case EngineState.Expansion or EngineState.Exhaust:
                _cylinder.UpdateBD(volume, rate, y[0], y[1], y[2]);
                break;

            case EngineState.Overlap when _gasExchangeZones:
                switch (_overlapPhase)
                {
                    case OverlapPhase.BurntOnly:
                        _cylinder.UpdateBD(volume, rate, y[0], y[1], y[2]);
                        break;

                    case OverlapPhase.UnburntOnly:
                        _cylinder.UpdateUB(volume, rate, y[0], y[1], y[3]);
                        break;

                    default:
                        _cylinder.UpdateGasExchangeZones(volume, rate, y[0], y[1], y[2], y[3]);
                        break;
                }

                break;

            case EngineState.Overlap:
                _cylinder.UpdateGE(volume, rate, y[0], y[1], y[2], y[3]);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Forces the two zones to agree in the single-zone model: both masses become the
    /// whole charge and both temperatures come from the ideal gas law.
    /// </summary>
    /// <remarks>
    /// From the gas's volume, which in Legacy belongs to the angle the step started at
    /// rather than to the pressure (ISSUES.md B77).
    /// </remarks>
    private void CollapseToSingleZone()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;

        cylinder.Mb = cylinder.MGas;
        cylinder.Mu = cylinder.MGas;
        cylinder.Tb = cylinder.PGas * cylinder.VGas / cylinder.RGas / cylinder.MGas;

        if (cylinder.Tb < 273.15)
        {
            cylinder.Tb = 273.15;
        }

        cylinder.Tu = cylinder.Tb;
        engine.Integration.Y[2] = cylinder.Tb;
        engine.Integration.Y[3] = cylinder.Tu;
    }

    private ManifoldStep StepManifolds()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;
        var x = engine.Integration.X;

        // Tgas recomputes and stores xb as a side effect, so it is called here in the
        // same position the original calls it, not folded into the request below.
        var gasTemperature = _cylinder.GasTemperature();

        var request = new ManifoldRequest(
            CrankAngle: (x * 180 / Math.PI) + 360,
            CylinderPressure: cylinder.PGas,
            CylinderTemperature: gasTemperature,
            // The volume the pressure belongs to under B77; the step's start in Legacy.
            // The angle and the valve areas stay on the step grid either way.
            CylinderVolume: Geometry.Volume(SolutionAngle),
            CylinderMass: cylinder.MGas,
            AtmosphericPressure: engine.Atmosphere.PGas,
            AtmosphericTemperature: _atmosphere.GasTemperature(),
            InletValveArea: InletValve.FlowArea(engine.CrankAngle),
            ExhaustValveArea: ExhaustValve.FlowArea(engine.CrankAngle));

        var result = _manifold.Step(in request);

        engine.MassIn = result.MassIn;
        engine.MassOut = result.MassOut;
        engine.DPressureFromMass = result.PressureCorrection;
        engine.InletPressure = result.InletPressure;
        engine.ExhaustPressure = result.ExhaustPressure;
        engine.InletVelocity = result.InletVelocity;
        engine.ExhaustVelocity = result.ExhaustVelocity;
        engine.Manifold.PlenumTemperature = result.InletTemperature;

        Cylinder.InletMassFlow = result.MassIn;

        return result;
    }

    private void AccumulateWorkAndHeat()
    {
        var engine = _engine;
        var y = engine.Integration.Y;
        var x = engine.Integration.X;
        var dx = engine.Integration.Dx;

        // Legacy pairs the end-of-step pressure with the start-of-step volume rate. Under
        // B77 each end of the step is evaluated at its own state and angle, and the step
        // takes the trapezoid of the two. See ISSUES.md B77.
        var end = SolutionAngle;
        var work = _endOfStepState
            ? 0.5 * dx * (_startRates.Work + Cylinder.WorkRate(end, y))
            : dx * Cylinder.WorkRate(x, y);

        switch (engine.State)
        {
            case EngineState.Combustion or EngineState.Compression or EngineState.Expansion:
                engine.Work += work;
                break;

            default:
                engine.PumpingWork -= work;
                break;
        }

        // CA is in degrees where dxdTheta expects radians, so this accumulates almost
        // nothing. Reproduced; the field is never read. See ISSUES.md B39.
        engine.FuelEnergy += engine.Cylinder.Fuel.M * engine.Cylinder.Fuel.Q
                             * _cylinder.BurnRate(engine.CrankAngle) * dx;

        if (engine.Cylinder.PGas > engine.PeakPressure)
        {
            engine.PeakPressure = engine.Cylinder.PGas;
        }

        if (_cylinder.GasTemperature() > engine.PeakTemperature)
        {
            engine.PeakTemperature = _cylinder.GasTemperature();
        }

        if (_endOfStepState)
        {
            engine.Qb = 0.5 * dx * (_startRates.BurntHeat + Cylinder.BurntHeatLossRate(end));
            engine.Qu = 0.5 * dx * (_startRates.UnburntHeat + Cylinder.UnburntHeatLossRate(end));
        }
        else
        {
            engine.Qb = Cylinder.BurntHeatLossRate(x) * dx;
            engine.Qu = Cylinder.UnburntHeatLossRate(x) * dx;
        }

        engine.HeatLoss += engine.Qb + engine.Qu;
    }

    /// <summary>
    /// Isenthalpic estimate of the burnt-gas temperature at the start of combustion. Port
    /// of the free function <c>InitialTb</c>.
    /// </summary>
    /// <remarks>
    /// The original calls <c>Halt</c> - terminating the process outright, losing the
    /// user's work - if the iteration has not converged after 1000 passes. The port throws
    /// instead, the same trade the table readers make. See ISSUES.md C12.
    /// </remarks>
    private double InitialBurntTemperature(double pressure, double unburntTemperature)
    {
        var burnt = 2000.0;

        for (var iteration = 0; iteration <= 1000; iteration++)
        {
            var unburntEnthalpy = _cylinder.Unburnt.Enthalpy(pressure, unburntTemperature);
            var burntEnthalpy = _cylinder.Burnt.Enthalpy(pressure, burnt);
            var burntCp = _cylinder.Burnt.SpecificHeatConstantPressure(pressure, burnt);

            var delta = (unburntEnthalpy - burntEnthalpy) / burntCp;
            burnt += delta;

            if (Math.Abs(delta) < 0.1)
            {
                return burnt;
            }
        }

        throw new EngineException(
            "Could not estimate the initial burned gas temperature.");
    }

    /// <summary>
    /// Moves the mass that crossed the valves between the burnt and unburnt zones. Port
    /// of the two-zone half of <c>Run</c>'s mass block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exhaust and intake are one line each. Overlap is eight cases, because either valve
    /// can flow either way and what comes back in depends on what went out: burnt gas
    /// pushed into the inlet is remembered in <c>MbOutInlet</c> so that it is drawn back
    /// into the burnt zone rather than counted as fresh charge, and the same for unburnt
    /// gas pushed into the exhaust.
    /// </para>
    /// <para>
    /// Note the sign convention. <c>dmindtheta</c> is set to <b>minus</b> the inlet mass
    /// over the step while <c>dmoutdtheta</c> takes the exhaust mass unnegated, so the
    /// two derivatives that feed the unburnt and burnt-down equations do not agree about
    /// which direction is positive.
    /// </para>
    /// </remarks>
    private void MoveMassBetweenZones()
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;
        var dx = engine.Integration.Dx;
        var massIn = engine.MassIn;
        var massOut = engine.MassOut;

        switch (engine.State)
        {
            case EngineState.Exhaust:
                cylinder.Mb -= massOut;
                cylinder.DmOutDTheta = massOut / dx;
                engine.TotalMassOutExhaustValve += massOut;
                break;

            case EngineState.Intake:
                cylinder.Mu += massIn;
                cylinder.DmInDTheta = -massIn / dx;
                engine.TotalMassInInletValve += massIn;
                break;

            case EngineState.Overlap:
                MoveMassDuringOverlap(massIn, massOut);
                break;

            default:
                break;
        }
    }

    private void MoveMassDuringOverlap(double massIn, double massOut)
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;

        // B37 reads each zone's change off this bookkeeping as the flows to apply.
        var burntBefore = cylinder.Mb;
        var unburntBefore = cylinder.Mu;
        var freshCharge = 0.0;

        // Exhaust valve. The totals count burnt gas out of the exhaust and fresh charge in
        // through the inlet; the other zone's gas in each port is remembered so that it
        // returns to its own zone. When a step's flow crosses from one kind to the other -
        // more drawn than the zone holds, or more returned than the port holds - the
        // original counts the whole flow in one and the crossing part again in the other.
        // B79 counts each part once. See ISSUES.md B79.
        if (massOut > 0 && cylinder.Mb > 0)
        {
            engine.TotalMassOutExhaustValve += _countValveFlowsOnce ? Math.Min(massOut, cylinder.Mb) : massOut;
            cylinder.Mb -= massOut;
        }

        if (massOut > 0 && cylinder.Mb == 0)
        {
            cylinder.Mu -= massOut;
            engine.UnburntMassOutExhaust += massOut;
        }

        if (massOut < 0 && engine.UnburntMassOutExhaust == 0)
        {
            // massOut is negative here: this adds the reverted mass back.
            cylinder.Mb -= massOut;
            engine.TotalMassOutExhaustValve += massOut;
        }

        if (massOut < 0 && engine.UnburntMassOutExhaust > 0)
        {
            if (_countValveFlowsOnce)
            {
                // Beyond the unburnt gas the exhaust holds, what returns is burnt gas.
                engine.TotalMassOutExhaustValve += Math.Min(0, massOut + engine.UnburntMassOutExhaust);
            }

            cylinder.Mu -= massOut;
            engine.UnburntMassOutExhaust += massOut;
        }

        if (cylinder.Mb < 0)
        {
            cylinder.Mu += cylinder.Mb;
            engine.UnburntMassOutExhaust -= cylinder.Mb;
            cylinder.Mb = 0;
        }

        if (engine.UnburntMassOutExhaust < 0)
        {
            // More came back through the exhaust than unburnt gas went out, and the whole
            // return was credited to the unburnt zone above; the excess is burnt gas. The
            // original credits it to the burnt zone as well, counting it twice. Under B37
            // the zone masses are the zones', so it is moved rather than copied.
            if (_gasExchangeZones)
            {
                cylinder.Mu += engine.UnburntMassOutExhaust;
            }

            cylinder.Mb -= engine.UnburntMassOutExhaust;
            engine.UnburntMassOutExhaust = 0;
        }

        // Inlet valve.
        if (massIn > 0 && engine.BurntMassOutInlet == 0)
        {
            cylinder.Mu += massIn;
            engine.TotalMassInInletValve += massIn;
            freshCharge = massIn;
        }

        if (massIn > 0 && engine.BurntMassOutInlet > 0)
        {
            if (_countValveFlowsOnce)
            {
                // Beyond the burnt gas the port holds, what enters is fresh charge.
                engine.TotalMassInInletValve += Math.Max(0, massIn - engine.BurntMassOutInlet);
            }

            cylinder.Mb += massIn;
            engine.BurntMassOutInlet -= massIn;
        }

        if (massIn < 0 && cylinder.Mu > 0)
        {
            engine.TotalMassInInletValve += _countValveFlowsOnce ? Math.Max(massIn, -cylinder.Mu) : massIn;
            cylinder.Mu += massIn;
        }

        if (massIn < 0 && cylinder.Mu == 0)
        {
            cylinder.Mb += massIn;
            engine.BurntMassOutInlet -= massIn;
        }

        if (cylinder.Mu < 0)
        {
            cylinder.Mb += cylinder.Mu;
            engine.BurntMassOutInlet -= cylinder.Mu;
            cylinder.Mu = 0;
        }

        if (engine.BurntMassOutInlet < 0)
        {
            // The same at the inlet: the excess over the burnt gas pushed into the port is
            // fresh charge, credited to the burnt zone above and then to the unburnt zone as
            // well. Under B37 it is moved, and it carries the plenum's enthalpy.
            if (_gasExchangeZones)
            {
                cylinder.Mb += engine.BurntMassOutInlet;
                freshCharge -= engine.BurntMassOutInlet;
            }

            cylinder.Mu -= engine.BurntMassOutInlet;
            engine.BurntMassOutInlet = 0;
        }

        if (_gasExchangeZones)
        {
            ApplyOverlapFlows(burntBefore, unburntBefore, freshCharge);
            return;
        }

        // Both zones are put back on the ideal gas law at the mixed temperature, and the
        // integrator's two temperature components with them. The volume is the gas's: in
        // Legacy the angle the step started at, a step behind the pressure, so the
        // temperature is off by dV/V, a few per cent near top dead centre; under B77 the
        // angle the pressure belongs to. See ISSUES.md B77.
        cylinder.Tu = cylinder.PGas * cylinder.VGas / cylinder.RGas / cylinder.MGas;
        engine.Integration.Y[2] = cylinder.Tu;
        engine.Integration.Y[3] = cylinder.Tu;
    }

    /// <summary>
    /// B37: puts the step's flows through both valves into the zones they reached, each
    /// carrying its own enthalpy, at the volume the step finished at.
    /// </summary>
    /// <param name="burntBefore">The burnt zone's mass before the bookkeeping moved it.</param>
    /// <param name="unburntBefore">The unburnt zone's mass before the bookkeeping moved it.</param>
    /// <param name="freshCharge">The mass the step drew from the plenum, new charge.</param>
    /// <remarks>
    /// <para>
    /// The cylinder is a fixed volume while the flows are applied - the piston's work was
    /// the closed step's - so this is the original's mass-transfer pressure correction
    /// taken to two zones. Each zone's energy changes by the enthalpy its flows carry:
    /// fresh charge at the plenum's, and everything else - burnt gas leaving or coming back
    /// through either valve, unburnt gas pushed out of either and returning - at the zone's
    /// own, the state it left in. The zones then settle to a common pressure in the
    /// cylinder's volume, the one compressing the other, and that work is counted in both,
    /// so the total energy changes by the enthalpy flows alone. A zone that had no mass is
    /// created this way: the first fresh charge fills a volume of its own against the burnt
    /// gas, at the temperature the first law gives it.
    /// </para>
    /// <para>
    /// The original instead resets both zones to one temperature every step (the
    /// <see cref="MoveMassDuringOverlap"/> tail), which is how overlap keeps one zone in
    /// effect. See ISSUES.md B37.
    /// </para>
    /// </remarks>
    private void ApplyOverlapFlows(double burntBefore, double unburntBefore, double freshCharge)
    {
        var engine = _engine;
        var cylinder = engine.Cylinder;
        var y = engine.Integration.Y;
        var burnt = _cylinder.Burnt;
        var unburnt = _cylinder.Unburnt;

        // The state the integrator finished the step at, whichever angle the gas is paired
        // with (ISSUES.md B77).
        var volume = Geometry.Volume((engine.CrankAngle + engine.CrankAngleStep) * Math.PI / 180);
        var pressure = y[1];
        var burntTemperature = burntBefore > 0 ? y[2] : cylinder.Tb;
        var unburntTemperature = unburntBefore > 0 ? y[3] : engine.Plenum.Tu;
        var burntVolume = burntBefore <= 0 ? 0 : unburntBefore <= 0 ? volume : y[0];

        var burntEnergy = burntBefore > 0 ? burntBefore * burnt.InternalEnergy(pressure, burntTemperature) : 0;
        burntEnergy += (cylinder.Mb - burntBefore) * burnt.Enthalpy(pressure, burntTemperature);

        var unburntEnergy = unburntBefore > 0 ? unburntBefore * unburnt.InternalEnergy(pressure, unburntTemperature) : 0;
        unburntEnergy += freshCharge * engine.Plenum.Hu;
        unburntEnergy += (cylinder.Mu - unburntBefore - freshCharge) * unburnt.Enthalpy(pressure, unburntTemperature);

        // The zones settle at a common pressure P' in the cylinder's volume, each doing
        // work on the other at the mean pressure Pm, so for each zone
        //     m u(T') = E - Pm (V' - V),   V' = m R T' / P'.
        // The two works sum to zero once the volumes fill the cylinder, so the settling
        // moves no energy. With V' put in, each zone is one equation in its own
        // temperature, m (u + (Pm/P') R T') = E + Pm V, rising like the enthalpy - safe for
        // Newton, where iterating the volume separately oscillates and can throw a newly
        // created zone outside its curve fits. Only the pressure is iterated.
        var unburntVolume = volume - burntVolume;
        var newPressure = pressure;
        var newBurntTemperature = burntTemperature;
        var newUnburntTemperature = unburntTemperature;
        var burntGas = 0.0;
        var unburntGas = 0.0;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var meanPressure = 0.5 * (pressure + newPressure);
            var workFactor = meanPressure / newPressure;

            if (cylinder.Mb > 0)
            {
                newBurntTemperature = SettledTemperature(
                    burnt, cylinder.Mb, burntEnergy + (meanPressure * burntVolume), workFactor,
                    newPressure, newBurntTemperature);
                burntGas = cylinder.Mb * burnt.GasConstant(newPressure, newBurntTemperature) * newBurntTemperature;
            }

            if (cylinder.Mu > 0)
            {
                newUnburntTemperature = SettledTemperature(
                    unburnt, cylinder.Mu, unburntEnergy + (meanPressure * unburntVolume), workFactor,
                    newPressure, newUnburntTemperature);
                unburntGas = cylinder.Mu * unburnt.GasConstant(newPressure, newUnburntTemperature) * newUnburntTemperature;
            }

            var lastPressure = newPressure;
            newPressure = (burntGas + unburntGas) / volume;

            if (Math.Abs(newPressure - lastPressure) <= 1e-13 * newPressure)
            {
                break;
            }
        }

        var newBurntVolume = burntGas / newPressure;

        if (!double.IsFinite(newPressure) || newPressure <= 0)
        {
            throw new EngineException("Overlap flows left the cylinder with no pressure.");
        }

        y[0] = newBurntVolume;
        y[1] = newPressure;
        y[2] = newBurntTemperature;
        y[3] = newUnburntTemperature;

        _overlapPhase = cylinder.Mu <= 0 ? OverlapPhase.BurntOnly
            : cylinder.Mb <= 0 ? OverlapPhase.UnburntOnly
            : OverlapPhase.TwoZone;

        RefreshGasFromIntegrator();
    }

    /// <summary>
    /// B37: the temperature at which a zone settles, solving
    /// <c>m (u(T) + f R T) = energy</c> by Newton's method, where <c>f</c> is the ratio of
    /// the mean pressure the zone works at to the pressure it settles to.
    /// </summary>
    private static double SettledTemperature(
        GasPropertyModel model, double mass, double energy, double workFactor, double pressure, double guess)
    {
        var target = energy / mass;
        var temperature = guess;

        for (var iteration = 0; iteration < 50; iteration++)
        {
            var gasConstant = model.GasConstant(pressure, temperature);
            var step = (model.InternalEnergy(pressure, temperature) + (workFactor * gasConstant * temperature) - target)
                       / (model.SpecificHeatConstantVolume(pressure, temperature) + (workFactor * gasConstant));
            temperature -= step;

            if (Math.Abs(step) <= 1e-10 * temperature)
            {
                break;
            }
        }

        return temperature;
    }

    // -----------------------------------------------------------------------
    // The cycle loop
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs whole cycles until the mass balance converges or the requested count is
    /// reached. Port of the loop in <c>TFMain.Simulate</c>, with the UI stripped out.
    /// </summary>
    /// <param name="settings">Cycle count, warm-up cycles and the convergence tolerance.</param>
    /// <param name="twoZone">Whether to switch to the two-zone model after the warm-up.</param>
    /// <returns>How many cycles were actually run.</returns>
    /// <remarks>
    /// <para>
    /// Each cycle starts at inlet valve closing and runs a full 720 degrees back to it.
    /// The crank angle wraps at 360 rather than being tracked cumulatively, which is why
    /// the terminating test is a distance rather than an equality.
    /// </para>
    /// <para>
    /// Convergence is tested at the <b>top</b> of each cycle against the totals the
    /// previous one accumulated, so a converged run stops before doing the work rather
    /// than after. That is what leaves the manifold output files unwritten: their gate
    /// wants the final requested cycle, which a converged run never reaches. See
    /// ISSUES.md C1.
    /// </para>
    /// </remarks>
    public int RunCycles(SimulationSettings settings, bool twoZone = true)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var engine = _engine;
        var requested = Math.Max(settings.CycleCount, EsaLimits.MinimumCycles);

        engine.CycleCount = requested;
        engine.ZoneCount = 1;

        for (var cycle = 1; cycle <= requested; cycle++)
        {
            if (twoZone && cycle >= settings.OneZoneCycleCount + 1)
            {
                engine.ZoneCount = 2;
            }
            else if (!twoZone)
            {
                engine.ZoneCount = 1;
            }

            if (Math.Abs(engine.TotalMassInInletValve - engine.TotalMassOutExhaustValve) * 1E6
                < settings.MassBalance)
            {
                engine.CycleCount = cycle - 1;
                return cycle - 1;
            }

            RunOneCycle();
        }

        return requested;
    }

    /// <summary>Runs one complete 720 degree cycle from inlet valve closing.</summary>
    public void RunOneCycle()
    {
        var engine = _engine;
        var start = States.InletClose;
        var step = engine.CrankAngleStep;

        engine.CrankAngle = start;

        do
        {
            Step();

            engine.CrankAngle += step;

            if (engine.CrankAngle > 360)
            {
                engine.CrankAngle -= 720;
            }
        }
        while (Math.Abs(engine.CrankAngle - start) >= step);
    }
}
