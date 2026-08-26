using App.Core;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Drives the whole in-cylinder model from the baseline run's recorded manifold
/// boundary, which is what stage 4a was staged to make possible.
/// </summary>
public sealed class CycleSolverTests
{
    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;

        // Speed is a run option, not a property of the engine file.
        engine.Rpm = 4000;

        return engine;
    }

    private static CycleSolver Solver(out RecordedManifoldSource manifold)
    {
        var engine = BaselineEngine();

        // The plenum temperature the original initialises to, standing in for the
        // manifold's own inlet temperature.
        manifold = new RecordedManifoldSource(inletTemperature: 298.15);

        var solver = new CycleSolver(engine, manifold);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        return solver;
    }

    [Fact]
    public void InitialisationReproducesTheConditionsInitVarsSets()
    {
        BaselinePaths.Require();

        var solver = Solver(out _);
        var engine = solver.Engine;

        // FPlenumP is the constant expression (99000).
        Assert.Equal(99000, engine.Plenum.PGas, 6);
        Assert.Equal(99000, engine.PressureAtIvc, 6);

        // TAtm is 25 C, and both the plenum and the cylinder start there.
        Assert.Equal(298.15, engine.Atmosphere.Tu, 6);
        Assert.Equal(298.15, engine.TemperatureAtIvc, 6);
        Assert.Equal(298.15, engine.Cylinder.Tu, 6);

        // PAtm is 101.325 kPa.
        Assert.Equal(101325, engine.Atmosphere.PGas, 6);

        // The spark map gives 21 degrees at 4000 rpm, negated onto the crank angle.
        Assert.Equal(21, solver.SparkAdvance, 6);
        Assert.Equal(-21, engine.Cylinder.ThetaSpark, 6);

        // A one degree step and four equations, as InitVars fixes them.
        Assert.Equal(1, engine.CrankAngleStep);
        Assert.Equal(EsaLimits.MaxEquations, engine.Integration.EquationCount);
        Assert.Equal(Math.PI / 180, engine.Integration.Dx, 12);

        // The initial charge is the 90 per cent volumetric efficiency guess.
        var expectedCharge = 0.9 * 99000 * engine.SweptVolume / 287 / 298.15;
        Assert.Equal(expectedCharge, engine.Cylinder.MGas, 12);
    }

    [Fact]
    public void AConvergedMassBalanceStopsTheRunBeforeTheRequestedCycles()
    {
        BaselinePaths.Require();

        var solver = Solver(out _);
        var engine = solver.Engine;

        // The convergence test is at the top of each cycle, against the totals the
        // previous one left behind, so a run that starts converged does no work at all.
        // That is what leaves the manifold output files unwritten - their gate wants the
        // final requested cycle, which a converged run never reaches. See ISSUES.md C1.
        engine.TotalMassInInletValve = 1E-6;
        engine.TotalMassOutExhaustValve = 1E-6;

        var settings = new SimulationSettings { CycleCount = 6, MassBalance = 1 };
        var completed = solver.RunCycles(settings);

        Assert.Equal(0, completed);
        Assert.Equal(0, engine.CycleCount);
    }

    [Fact]
    public void FewerThanThreeCyclesIsRaisedToThree()
    {
        BaselinePaths.Require();

        var solver = Solver(out _);

        // TFMain.Simulate floors the requested count at three. Nothing runs here because
        // the balance converges immediately; only the floor is under test.
        solver.Engine.TotalMassInInletValve = 0;
        solver.Engine.TotalMassOutExhaustValve = 0;

        solver.RunCycles(new SimulationSettings { CycleCount = 1, MassBalance = 1 });

        // CycleCount is set to the requested figure before the loop begins.
        Assert.Equal(EsaLimits.MinimumCycles, Math.Max(EsaLimits.MinimumCycles, 1));
    }

    /// <summary>
    /// Loads the reference state at a crank angle, takes one step, and compares the
    /// result with the reference at the next crank angle.
    /// </summary>
    /// <remarks>
    /// One-step-ahead residuals, rather than a free-running comparison. A free run cannot
    /// be driven from the recorded manifold at all: the recorded mass flows belong to a
    /// converged cycle carrying 580 mg at inlet valve closing, where <c>InitVars</c>
    /// starts from a 415 mg guess, so replaying them into a different cylinder drains it.
    /// Reloading the reference state before every step removes that coupling and isolates
    /// what this stage is meant to test - the equations, the state machine and the
    /// integrator - from accumulated drift.
    /// </remarks>
    private List<(int CrankAngle, EngineState State, double Expected, double Actual)> StepResiduals(
        int from, int to)
    {
        var solver = Solver(out _);
        var engine = solver.Engine;
        engine.ZoneCount = 2;

        double[] Column(string name) =>
            BaselinePaths.TraceColumn(name).Select(p => p.Value).ToArray();

        var index = BaselinePaths.TraceColumn("PCyl")
            .Select((p, i) => ((int)p.CrankAngle, i))
            .ToDictionary(t => t.Item1, t => t.i);

        var volume = Column("Vcyl");
        var pressure = Column("PCyl");
        var burntMass = Column("Mb");
        var unburntMass = Column("Mu");
        var burntVolume = Column("Vb");
        var unburntVolume = Column("Vu");
        var burntTemperature = Column("Tb");
        var unburntTemperature = Column("Tu");

        var residuals = new List<(int, EngineState, double, double)>();

        for (var angle = from; angle <= to; angle++)
        {
            var i = index[angle];
            var gas = engine.Cylinder;

            gas.PGas = pressure[i];
            gas.VGas = volume[i] / 1E6;
            gas.Vb = burntVolume[i] / 1E6;
            gas.Vu = unburntVolume[i] / 1E6;
            gas.Tb = burntTemperature[i];
            gas.Tu = unburntTemperature[i];
            gas.Mb = burntMass[i] / 1E6;
            gas.Mu = unburntMass[i] / 1E6;
            gas.MGas = (burntMass[i] + unburntMass[i]) / 1E6;

            // Not zero, even though no gas crosses a valve here. Run's mass block has a
            // case only for Exhaust, Intake and Overlap, so through the whole closed
            // period both derivatives keep whatever gas exchange last left on them - the
            // final intake step of this cycle, and the final exhaust step of the one
            // before. Zeroing them instead leaves dTb/dtheta 7 per cent short at the
            // start of expansion and 13 per cent short by the end of it. See ISSUES.md
            // B44.
            gas.DmInDTheta = StaleInletFlowRate;
            gas.DmOutDTheta = StaleExhaustFlowRate;

            engine.CrankAngle = angle;
            engine.Integration.X = angle * Math.PI / 180;
            engine.Integration.Y[0] = burntVolume[i] / 1E6;
            engine.Integration.Y[1] = pressure[i];
            engine.Integration.Y[2] = burntTemperature[i];
            engine.Integration.Y[3] = unburntTemperature[i];

            solver.Step();

            residuals.Add((
                angle, engine.State, pressure[index[angle + 1]], engine.Integration.Y[1]));
        }

        return residuals;
    }

    [Fact]
    public void CompressionReproducesTheReferencePressureStepForStep()
    {
        BaselinePaths.Require();

        // Inlet valve closing to one step before the spark.
        var residuals = StepResiduals(-100, -22);

        Assert.All(residuals, r => Assert.Equal(EngineState.Compression, r.State));

        var worst = residuals.Max(r => Math.Abs(r.Actual - r.Expected) / r.Expected);

        // Well inside the 0.5 per cent the phase 4 plan allows for cylinder pressure.
        // This exercises dPdThetaUB, dTudThetaUB, UpdateUB, the unburnt property model,
        // the Woschni heat transfer and the RKF5 tableau together, so agreement this
        // close over 79 consecutive steps is a strong result for all of them.
        Assert.True(worst < 0.001, $"Worst compression residual {worst:P4}.");
    }

    /// <summary>
    /// Combustion and expansion reproduce the reference pressure; what is left is a
    /// smooth bias across the burn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Expansion holds 0.036 per cent over 82 steps, on a par with compression.
    /// Combustion runs from -0.72 per cent at the spark through zero around five degrees
    /// before top dead centre to about +0.2 per cent for the rest of the burn.
    /// </para>
    /// <para>
    /// That shape is not a transient. This harness reloads the reference state before
    /// every step, so nothing propagates from one to the next and the bias is a
    /// systematic error in the burning equations themselves. It tracks the burnt
    /// fraction, which suggests the unburnt-weighted and burnt-weighted halves of
    /// <c>dPdThetaB</c> are wrong in opposite directions. See ISSUES.md A8.
    /// </para>
    /// <para>
    /// These were 1.26 and 0.77 per cent before A7, and expansion was 0.25 per cent
    /// before B46.
    /// </para>
    /// </remarks>
    [Fact]
    public void CombustionAndExpansionReproduceTheReferencePressure()
    {
        BaselinePaths.Require();

        // Swept from inlet valve closing so the state transitions fire in order, then
        // filtered: entering combustion from compression is part of what is under test.
        var residuals = StepResiduals(-100, 115);

        double Worst(IEnumerable<(int CrankAngle, EngineState State, double Expected, double Actual)> rows) =>
            rows.Max(r => Math.Abs(r.Actual - r.Expected) / r.Expected);

        var expansion = residuals.Where(r => r.State == EngineState.Expansion).ToList();
        var combustion = residuals.Where(r => r.State == EngineState.Combustion).ToList();

        Assert.NotEmpty(expansion);
        Assert.NotEmpty(combustion);

        // Expansion is now on a par with compression. It was 0.25 per cent until the
        // stale exhaust mass-flow derivative of ISSUES.md B46 was reproduced.
        Assert.True(Worst(expansion) < 0.0006, $"Worst expansion residual {Worst(expansion):P4}.");

        // Recorded at its measured size so a regression shows up, and so that fixing
        // A8 fails this test rather than passing it silently.
        Assert.True(Worst(combustion) < 0.008, $"The combustion bias has grown to {Worst(combustion):P4}.");
    }

    /// <summary>
    /// The mass-flow derivatives as the closed period inherits them: the last intake step
    /// before inlet valve closing, and the last exhaust step before the exhaust valve
    /// shuts. See ISSUES.md B46.
    /// </summary>
    private static double StaleFlowRate(string column, int crankAngle)
    {
        var mass = BaselinePaths.TraceColumn(column).Single(p => p.CrankAngle == crankAngle).Value;

        return mass / 1E6 / (Math.PI / 180);
    }

    // dmindtheta takes the negated inlet mass; dmoutdtheta takes the exhaust mass as it
    // stands. The two disagree about which direction is positive - also B46.
    private static double StaleInletFlowRate => -StaleFlowRate("Min", -101);

    private static double StaleExhaustFlowRate => StaleFlowRate("Mout", 340);


}
