using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B79: the cycle's valve totals, which set the fuel mass, the volumetric
/// efficiency and the mass-balance convergence test, counting each flow once.
/// </summary>
public sealed class ValveTotalsTests
{
    private static Engine BaselineEngine(int rpm)
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
        engine.Rpm = rpm;

        return engine;
    }

    /// <summary>
    /// The largest amount by which one step's flow through either valve, in kilograms,
    /// fails to be accounted for by the change in that valve's total and in the other
    /// kind of gas its port holds, over the given cycles.
    /// </summary>
    private static (double Worst, int Steps) WorstUnaccountedFlow(int rpm, bool corrected, int cycles)
    {
        var engine = BaselineEngine(rpm);
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.OverlapValveTotals.Entry] = corrected;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var cycle = 0;
        var worst = 0.0;
        var steps = 0;
        double lastIn = 0, lastOut = 0, lastBurntInPort = 0, lastUnburntInExhaust = 0;
        var lastState = engine.State;

        solver.StepCompleted += s =>
        {
            var e = s.Engine;

            // State entries reset the totals and the ports, so the step that enters a state
            // has no comparable "before".
            if (cycle > 1 && e.State == lastState)
            {
                // Each valve's flow is its total's change, less what went into, or came
                // back out of, the other kind of gas held in its port.
                var inlet = e.MassIn - ((e.TotalMassInInletValve - lastIn) - (e.BurntMassOutInlet - lastBurntInPort));
                var exhaust = e.MassOut - ((e.TotalMassOutExhaustValve - lastOut) + (e.UnburntMassOutExhaust - lastUnburntInExhaust));

                worst = Math.Max(worst, Math.Max(Math.Abs(inlet), Math.Abs(exhaust)));
                steps++;
            }

            lastIn = e.TotalMassInInletValve;
            lastOut = e.TotalMassOutExhaustValve;
            lastBurntInPort = e.BurntMassOutInlet;
            lastUnburntInExhaust = e.UnburntMassOutExhaust;
            lastState = e.State;
        };

        engine.ZoneCount = 1;

        for (cycle = 1; cycle <= cycles; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            solver.RunOneCycle();
        }

        return (worst, steps);
    }

    /// <summary>
    /// B79's oracle. Every kilogram through a valve is counted once: in the valve's total,
    /// or - for the other kind of gas, burnt gas pushed into the inlet port or unburnt gas
    /// into the exhaust - in what that port is remembered to hold. So each step's flow is
    /// exactly the change in the two. The original breaks this on the steps where a flow
    /// crosses from one kind of gas to the other, once an overlap at 3000, 4000 and 5000
    /// rpm on the baseline engine.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryFlowThroughAValveIsCountedOnce(bool corrected)
    {
        BaselinePaths.Require();

        var worst = 0.0;

        foreach (var rpm in new[] { 3000, 4000, 5000 })
        {
            var (speedWorst, steps) = WorstUnaccountedFlow(rpm, corrected, cycles: 4);
            Assert.True(steps > 2000, $"{steps}");
            worst = Math.Max(worst, speedWorst);
        }

        if (corrected)
        {
            // Rounding in sums of a few hundred milligrams.
            Assert.InRange(worst, 0, 1e-15);
        }
        else
        {
            Assert.True(worst > 1e-10, $"{worst}");
        }
    }
}
