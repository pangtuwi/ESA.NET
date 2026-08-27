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

    public CycleSolver(Engine engine, IManifoldSource manifold, IExpressionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(manifold);

        _engine = engine;
        _manifold = manifold;
        _evaluator = evaluator ?? new CachingExpressionEvaluator();

        _cylinder = new TwoZoneGas(engine.Cylinder);
        _plenum = new TwoZoneGas(engine.Plenum);
        _exhaust = new TwoZoneGas(engine.Exhaust);
        _atmosphere = new TwoZoneGas(engine.Atmosphere);

        Geometry = CylinderGeometry.FromEngine(engine);
        InletValve = ValveMotion.Inlet(engine.Manifold.InletValve);
        ExhaustValve = ValveMotion.Exhaust(engine.Manifold.ExhaustValve);

        SparkAdvance = LegacyInterpolation.AtSpeed(
            engine.SparkAngle.Rpm, engine.SparkAngle.Values, engine.Rpm);

        States = CrankAngleStateMap.FromEngine(engine, SparkAdvance);

        Cylinder = new CylinderModel(Geometry, _cylinder, _plenum, engine.WallTemperature)
        {
            Rpm = engine.Rpm,
            WoschniCoefficient = engine.WoshiniCoefficient,
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
    /// Sets up every gas and the integrator. Port of <c>TEngine2z.InitVars</c>.
    /// </summary>
    /// <returns>
    /// Whether both cam profiles loaded, Delphi's <c>AllOK</c>. The original returns this
    /// through a <c>var</c> parameter and the caller refuses to run when it is false.
    /// </returns>
    /// <remarks>
    /// This is also where the <c>.eng</c> file's units become SI, because the port has no
    /// edit form in the simulation path to do it. See ISSUES.md A6.
    /// </remarks>
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
        if (engine.ZoneCount == 1 || engine.State == EngineState.Overlap)
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
                break;

            case EngineState.Combustion:
                InstallCombustionEquations(useUnburntEquations: false);

                // The burnt zone starts at the adiabatic flame temperature for the
                // unburnt state, found by isenthalpic iteration.
                engine.Integration.Y[2] = InitialBurntTemperature(cylinder.PGas, cylinder.Tu);
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

                // The gas-exchange set belongs here and is commented out in the original,
                // so two-zone overlap runs the single-zone constant-gamma pressure
                // equation. See ISSUES.md B37.
                InstallEquations(
                    CylinderModel.Zero,
                    Cylinder.PressureRateSingleZone,
                    CylinderModel.Zero,
                    CylinderModel.Zero);

                engine.BurntMassOutInlet = 0;
                engine.UnburntMassOutExhaust = 0;
                break;

            case EngineState.Intake:
                cylinder.Tu = ((cylinder.Mb * cylinder.Tb) + (cylinder.Mu * engine.Plenum.Tu))
                              / cylinder.MGas;
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
    /// Writes the integrator's result back into the gas through the update method that
    /// matches the state. Port of the <c>case state of</c> block after <c>Integrate</c>.
    /// </summary>
    private void RefreshGasFromIntegrator()
    {
        var engine = _engine;
        var y = engine.Integration.Y;
        var x = engine.Integration.X;
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
            CylinderVolume: Geometry.Volume(x),
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

        var work = dx * Cylinder.WorkRate(x, y);

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

        engine.Qb = Cylinder.BurntHeatLossRate(x) * dx;
        engine.Qu = Cylinder.UnburntHeatLossRate(x) * dx;
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

        // Exhaust valve.
        if (massOut > 0 && cylinder.Mb > 0)
        {
            cylinder.Mb -= massOut;
            engine.TotalMassOutExhaustValve += massOut;
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
            cylinder.Mb -= engine.UnburntMassOutExhaust;
            engine.UnburntMassOutExhaust = 0;
        }

        // Inlet valve.
        if (massIn > 0 && engine.BurntMassOutInlet == 0)
        {
            cylinder.Mu += massIn;
            engine.TotalMassInInletValve += massIn;
        }

        if (massIn > 0 && engine.BurntMassOutInlet > 0)
        {
            cylinder.Mb += massIn;
            engine.BurntMassOutInlet -= massIn;
        }

        if (massIn < 0 && cylinder.Mu > 0)
        {
            cylinder.Mu += massIn;
            engine.TotalMassInInletValve += massIn;
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
            cylinder.Mu -= engine.BurntMassOutInlet;
            engine.BurntMassOutInlet = 0;
        }

        // Both zones are put back on the ideal gas law at the mixed temperature, and the
        // integrator's two temperature components with them.
        cylinder.Tu = cylinder.PGas * cylinder.VGas / cylinder.RGas / cylinder.MGas;
        engine.Integration.Y[2] = cylinder.Tu;
        engine.Integration.Y[3] = cylinder.Tu;
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
