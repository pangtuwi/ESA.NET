using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The whole manifold solver, driven a step at a time through the seam
/// <see cref="IManifoldSource"/> defines.
/// </summary>
public sealed class ManifoldSolverTests
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
        engine.Rpm = 4000;
        engine.CrankAngleStep = 1;
        engine.Manifold.PlenumTemperature = 298.15;

        return engine;
    }

    private static ManifoldRequest Request(
        double traceAngle, double cylinderPressure, double cylinderTemperature,
        double inletArea, double exhaustArea) =>
        new(
            CrankAngle: traceAngle + 360,
            CylinderPressure: cylinderPressure,
            CylinderTemperature: cylinderTemperature,
            CylinderVolume: 300e-6,
            CylinderMass: 5.8e-4,
            AtmosphericPressure: 101325,
            AtmosphericTemperature: 298.15,
            InletValveArea: inletArea,
            ExhaustValveArea: exhaustArea);

    [Fact]
    public void TheGridsAreSizedAndSeededFromTheEngineFile()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = new ManifoldSolver(engine);

        // The grid is laid out on the first step, not at construction, because the
        // original does it inside Main_Prog at tStep = 0 - by which point InitVars has
        // established the plenum temperature it seeds from.
        solver.Step(Request(-100, 200000, 400, 0, 0));

        // The same 39 and 16 the .m field files carry.
        Assert.Equal(39, solver.InletGrid.ActiveCount);
        Assert.Equal(16, solver.ExhaustGrid.ActiveCount);

        // Seeded at plenum and back-pressure conditions respectively.
        Assert.Equal(99000, solver.InletGrid.Pressure[0], 6);
        Assert.All(
            Enumerable.Range(0, 16),
            i => Assert.Equal(solver.ExhaustGrid.Pressure[0], solver.ExhaustGrid.Pressure[i], 6));
    }

    [Fact]
    public void TheFirstCallOnlyLaysOutTheGrid()
    {
        BaselinePaths.Require();

        var solver = new ManifoldSolver(BaselineEngine());

        var first = solver.Step(Request(-100, 200000, 400, 0, 0));

        // tStep = 0 in the original advances no characteristics and passes no mass.
        Assert.Equal(0, first.MassIn);
        Assert.Equal(0, first.MassOut);
        Assert.Equal(0, first.PressureCorrection);
        Assert.Equal(0, solver.TimeStep);
    }

    [Fact]
    public void TheClosedPeriodPassesNoMassAndNoPressureCorrection()
    {
        BaselinePaths.Require();

        var solver = new ManifoldSolver(BaselineEngine());
        solver.Step(Request(-100, 200000, 400, 0, 0));

        // Compression through expansion: both valves shut, and the tail of Main_Prog
        // forces the three flow outputs to zero across the whole closed period.
        foreach (var angle in new[] { -99, -50, 0, 50, 100, 115 })
        {
            var step = solver.Step(Request(angle, 2_000_000, 1500, 0, 0));

            Assert.Equal(0, step.MassIn);
            Assert.Equal(0, step.MassOut);
            Assert.Equal(0, step.PressureCorrection);
        }
    }

    [Fact]
    public void AClosedManifoldStaysAtItsSeededPressures()
    {
        BaselinePaths.Require();

        var solver = new ManifoldSolver(BaselineEngine());
        solver.Step(Request(-100, 200000, 400, 0, 0));

        for (var angle = -99; angle <= 115; angle++)
        {
            solver.Step(Request(angle, 2_000_000, 1500, 0, 0));
        }

        // Nothing has driven either pipe over 215 steps with both valves shut, so both
        // should still be sitting at their reservoir pressures. This exercises the
        // interior scheme, both walls and both open ends together, and would catch drift
        // that a single step cannot.
        Assert.Equal(99000, solver.InletGrid.Pressure[38], 0);
        Assert.Equal(99000, solver.InletGrid.Pressure[0], 0);
        Assert.All(
            Enumerable.Range(0, 39),
            i => Assert.True(
                Math.Abs(solver.InletGrid.Velocity[i]) < 1e-6,
                $"Inlet velocity at point {i} drifted to {solver.InletGrid.Velocity[i]:E3} m/s."));
    }

    [Fact]
    public void BlowdownThroughTheExhaustValveMovesMassOutAndCorrectsCylinderPressure()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = new ManifoldSolver(engine);
        var exhaustValve = ValveMotion.Exhaust(engine.Manifold.ExhaustValve);

        solver.Step(Request(-100, 200000, 400, 0, 0));

        // Step up to exhaust valve opening with the valves shut, then open it.
        for (var angle = -99; angle <= 115; angle++)
        {
            solver.Step(Request(angle, 2_000_000, 1500, 0, 0));
        }

        var step = solver.Step(
            Request(120, 600000, 1400, 0, exhaustValve.FlowArea(120)));

        // Gas leaves the cylinder, so the mass out is positive and the correction it
        // implies is negative: the cylinder loses pressure with the mass.
        Assert.True(step.MassOut > 0, $"Mass out came back as {step.MassOut:E3} kg.");
        Assert.True(
            step.PressureCorrection < 0,
            $"Pressure correction came back as {step.PressureCorrection:E3} Pa.");
    }

    [Fact]
    public void InletValveClosingAdvancesTheCycleCounter()
    {
        BaselinePaths.Require();

        var solver = new ManifoldSolver(BaselineEngine());
        solver.Step(Request(-100, 200000, 400, 0, 0));

        Assert.Equal(0, solver.TimeStep);

        // Inlet valve closing is at -100 in trace terms, so the counter comes round when
        // the crank angle passes it again on the next cycle.
        for (var angle = -99; angle <= 360; angle++)
        {
            solver.Step(Request(angle, 150000, 800, 0, 0));
        }

        for (var angle = -359; angle <= -100; angle++)
        {
            solver.Step(Request(angle, 150000, 800, 0, 0));
        }

        Assert.Equal(1, solver.TimeStep);
    }

    /// <summary>
    /// The acceptance run for phase 4: load the baseline engine, simulate to convergence
    /// with both the cylinder model and the real manifold solver live, and compare the
    /// final cycle against the original's own trace at every crank angle.
    /// </summary>
    /// <remarks>
    /// Everything before this validated one layer at a time, mostly through one-step-ahead
    /// residuals from the reference state. This is the first test where nothing is fed
    /// from the trace: the port runs open-loop from InitVars' initial guess, converges its
    /// own mass balance, and is then asked whether it arrived at the same place.
    /// </remarks>
    [Fact]
    public void AConvergedRunMatchesTheBaselineTraceAtEveryCrankAngle()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = new App.Core.Simulation.CycleSolver(engine, new ManifoldSolver(engine));

        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var captured = new Dictionary<int, (double Pressure, double Mass)>();

        solver.StepCompleted += s =>
        {
            var crankAngle = (int)Math.Round(s.Engine.CrankAngle);

            // Each cycle starts at inlet valve closing; keep only the last one.
            if (crankAngle == -100)
            {
                captured.Clear();
            }

            captured[crankAngle] = (s.Engine.Cylinder.PGas, s.Engine.Cylinder.MGas);
        };

        var cycles = solver.RunCycles(
            new SimulationSettings { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1 });

        // The original converged in four requested cycles; this reaches its mass balance
        // in three, which is within the same ballpark and not itself a fidelity claim.
        Assert.InRange(cycles, 2, 6);
        Assert.Equal(720, captured.Count);

        var referencePressure = BaselinePaths.TraceColumn("PCyl");
        var referenceMass = BaselinePaths.TraceColumn("Mcyl");

        var worstPressure = 0.0;
        var worstPressureAngle = 0;
        var worstMass = 0.0;

        for (var i = 0; i < referencePressure.Count; i++)
        {
            var angle = (int)referencePressure[i].CrankAngle;
            var got = captured[angle];

            var pressureError = Math.Abs(got.Pressure - referencePressure[i].Value)
                                / referencePressure[i].Value;

            if (pressureError > worstPressure)
            {
                worstPressure = pressureError;
                worstPressureAngle = angle;
            }

            // Mass is printed in milligrams to two decimals, so tiny values carry little
            // precision; compare against the trapped charge rather than each reading.
            worstMass = Math.Max(
                worstMass,
                Math.Abs((got.Mass * 1e6) - referenceMass[i].Value) / 580.32);
        }

        Assert.True(
            worstPressure < 0.005,
            $"Worst cylinder pressure error {worstPressure:P3} at {worstPressureAngle} degrees.");

        Assert.True(worstMass < 0.01, $"Worst cylinder mass error {worstMass:P3} of the charge.");
    }

    [Fact]
    public void AConvergedRunReproducesTheReportedMassFlows()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = new App.Core.Simulation.CycleSolver(engine, new ManifoldSolver(engine));

        solver.Initialise();
        solver.RunCycles(
            new SimulationSettings { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1 });

        // SimulDat.txt reports MassIn 560.11 mg, MassOut 560.38 mg and a trapped charge
        // of 580.11 mg. The port reaches its own converged balance rather than being fed
        // these, so a relative bound is the honest comparison; it currently lands about
        // 0.3 per cent high on all three.
        void Within(double expected, double actual, string what) =>
            Assert.True(
                Math.Abs(actual - expected) / expected < 0.01,
                $"{what}: expected {expected:F2} mg, got {actual:F2} mg "
                + $"({(actual - expected) / expected:P2}).");

        Within(560.11, engine.TotalMassInInletValve * 1e6, "Mass in");
        Within(560.38, engine.TotalMassOutExhaustValve * 1e6, "Mass out");
        Within(580.11, engine.Cylinder.MGas * 1e6, "Trapped charge");
    }
}
