using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B86: the mass-balance convergence test, which compares one cycle's own inlet and
/// exhaust totals, applied to the single-zone warm-up cycle that starts from
/// <c>InitVars</c>' guesses.
/// </summary>
public sealed class WarmUpConvergenceTests
{
    private static Engine BaselineEngine(int rpm)
    {
        var engine = new EngineLoader(
                new EngineDefinitionStore(),
                new CamProfileReader(),
                new SpeedKeyedTableReader(),
                new WallTemperatureTableReader(),
                new ExhaustBackPressureTableReader(),
                new ManifoldAreaTableStore(),
                new DischargeCoefficientTableStore())
            .Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = rpm;
        return engine;
    }

    private static SimulationResult Run(int rpm, double massBalance, PhysicsCorrections physics) =>
        new SimulationRunner(new CachingExpressionEvaluator()).Run(
            BaselineEngine(rpm),
            new SimulationSettings { CycleCount = 8, OneZoneCycleCount = 1, MassBalance = massBalance, Physics = physics },
            cancellation: TestContext.Current.CancellationToken);

    /// <summary>
    /// B86's oracle. With a tolerance every cycle passes, the run stops at the first test it
    /// is allowed. The original's is at the top of the first cycle, where both totals are
    /// still zero, so it reports a run of no cycles at all; B86's is after the first
    /// two-zone cycle, so that what is reported is never a warm-up cycle.
    /// </summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public void TheWarmUpCycleIsNeverTheConvergedOne(bool warmUpConvergence, int cycles)
    {
        BaselinePaths.Require();

        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.WarmUpConvergence.Entry] = warmUpConvergence;

        var result = Run(4000, 1e9, physics);

        Assert.True(result.Converged);
        Assert.Equal(cycles, result.CyclesRun);
    }

    /// <summary>
    /// The case it was found on: the baseline engine at 1500 rpm, Corrected as it stood
    /// before B85. The warm-up cycle's totals agree to within the 1 mg tolerance and the
    /// original reports it, at 135 Nm against 106 measured; with B86 the run goes on, and
    /// lands within a few per cent of the dynamometer.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBaselineEngineAt1500RpmIsNotReportedFromItsWarmUpCycle(bool warmUpConvergence)
    {
        BaselinePaths.Require();

        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.ThroatSolveFromZero.Entry] = false;

        // B89 changes the warm-up cycle's burn, and with it the coincidence this rebuilds.
        physics.Overrides[CorrectionCatalogue.WiebeBurn.Entry] = false;
        physics.Overrides[CorrectionCatalogue.WarmUpConvergence.Entry] = warmUpConvergence;

        var result = Run(1500, 1, physics);

        if (warmUpConvergence)
        {
            Assert.True(result.CyclesRun >= 2, $"{result.CyclesRun}");
            Assert.Equal(106.0, result.Engine.Torque, 106.0 * 0.1);
        }
        else
        {
            Assert.Equal(1, result.CyclesRun);
            Assert.True(result.Engine.Torque > 125, $"{result.Engine.Torque}");
        }
    }
}
