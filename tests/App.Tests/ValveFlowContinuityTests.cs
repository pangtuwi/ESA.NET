using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B93: the mass the cylinder is credited with through a valve is the mass the
/// pipe passes through its valve end. The boundaries cap the valve-to-port area ratio at one;
/// the original then multiplied the throat flow by the whole valve area, so wherever the
/// valve's area exceeded the port's the cylinder received mass the pipe never delivered.
/// </summary>
public sealed class ValveFlowContinuityTests
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
        return engine;
    }

    private sealed record Balance(double CylinderIn, double PipeIn, double WorstExhaustMismatch, int ExhaustStepsChecked);

    /// <summary>
    /// Runs six cycles in Corrected, B93 as given, and over the sixth sums the mass the
    /// cylinder took through the inlet valve and the mass the inlet pipe passed through its
    /// valve end. On the exhaust side it compares the two step by step wherever the valve's
    /// area exceeds the port's by a tenth and gas is leaving. The choked blowdown before that
    /// is a separate matter (ISSUES.md B94), and its tail reaches the first three degrees past
    /// the crossing: 4.1, 2.2 and 0.25 per cent at 175 to 177 degrees.
    /// </summary>
    private static Balance Run(bool b93)
    {
        var engine = BaselineEngine();
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.ValveFlowContinuity.Entry] = b93;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var inletPipe = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength, clampPastEnd: true);
        var exhaustPipe = new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength, clampPastEnd: true);
        const int measuredCycle = 6;
        var cycle = 0;
        double cylinderIn = 0, pipeIn = 0, worst = 0;
        var checkedSteps = 0;

        solver.StepCompleted += s =>
        {
            if (cycle != measuredCycle)
            {
                return;
            }

            var dt = 1 / (s.Engine.Rpm / 60 * 360) * s.Engine.CrankAngleStep;
            var inlet = s.Engine.Manifold.Inlet;
            var end = inlet.ActiveCount - 1;

            cylinderIn += s.Engine.MassIn;
            pipeIn += inlet.Density[end] * inlet.Velocity[end] * inletPipe.Area(inlet.X[end]) * dt;

            var exhaust = s.Engine.Manifold.Exhaust;
            var port = exhaustPipe.Area(exhaust.X[0]);
            var pipeOut = exhaust.Density[0] * exhaust.Velocity[0] * port * dt;

            if (solver.ExhaustValve.FlowArea(s.Engine.CrankAngle) > 1.1 * port && s.Engine.MassOut > 1e-7)
            {
                worst = Math.Max(worst, Math.Abs((s.Engine.MassOut / pipeOut) - 1));
                checkedSteps++;
            }
        };

        engine.ZoneCount = 1;

        for (cycle = 1; cycle <= measuredCycle; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            solver.RunOneCycle();
        }

        return new Balance(cylinderIn, pipeIn, worst, checkedSteps);
    }

    /// <summary>
    /// The oracle: what the cylinder takes in over a cycle is what the inlet pipe delivered,
    /// and while the exhaust valve is clearly wider than its port every step's outflow is what the
    /// exhaust pipe received. Measured: 599.7 mg against 599.7, and the exhaust steps agree
    /// to the boundaries' iteration tolerance.
    /// </summary>
    [Fact]
    public void TheCylinderTakesWhatThePipeDelivers()
    {
        BaselinePaths.Require();

        var balance = Run(b93: true);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"inlet: cylinder {balance.CylinderIn * 1e6:F2} mg, pipe {balance.PipeIn * 1e6:F2} mg; " +
            $"exhaust worst {balance.WorstExhaustMismatch:E2} over {balance.ExhaustStepsChecked} steps");

        Assert.InRange(balance.CylinderIn / balance.PipeIn, 0.995, 1.005);
        Assert.True(balance.ExhaustStepsChecked > 20, $"Only {balance.ExhaustStepsChecked} exhaust steps were wider than the port.");
        Assert.InRange(balance.WorstExhaustMismatch, 0, 1e-3);
    }

    /// <summary>
    /// Without B93 the baseline engine's three inlet valves, 2193 mm² at full lift against a
    /// 1200 mm² port, credit the cylinder with half as much again as the pipe delivers
    /// (577 mg against 374 at 4000 rpm), and the exhaust's 1960 mm² against 970 does the same.
    /// </summary>
    [Fact]
    public void WithoutB93TheCylinderReceivesMassThePipeNeverDelivered()
    {
        BaselinePaths.Require();

        var balance = Run(b93: false);

        Assert.InRange(balance.CylinderIn / balance.PipeIn, 1.3, 1.8);
        Assert.True(balance.WorstExhaustMismatch > 0.3, $"The exhaust mismatch was only {balance.WorstExhaustMismatch:P1}.");
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.ValveFlowContinuity));
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.ValveFlowContinuity));
    }
}
