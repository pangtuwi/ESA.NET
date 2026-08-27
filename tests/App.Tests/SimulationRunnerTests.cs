using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The runner that drives a whole simulation, as the application does.
/// </summary>
public sealed class SimulationRunnerTests
{
    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;
        engine.CrankAngleStep = 1;

        return engine;
    }

    private static SimulationSettings Settings() =>
        new() { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1 };

    [Fact]
    public void OneCallReproducesTheAcceptanceResult()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        Assert.True(result.Converged);
        Assert.InRange(result.CyclesRun, 2, 6);

        // The same whole-cycle agreement the acceptance test measures, reached through
        // the interface the application actually calls.
        var reference = BaselinePaths.TraceColumn("PCyl");
        var worst = reference.Max(
            r => Math.Abs(result.Trace[(int)r.CrankAngle][2] - r.Value) / r.Value);

        Assert.True(worst < 0.005, $"Worst cylinder pressure error {worst:P3}.");
    }

    [Fact]
    public void TheCapturedTraceCarriesEveryRecordedQuantity()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        var point = result.Trace[0];

        // A spread across the twenty-eight, in SI as stored: volume in cubic metres,
        // pressure in pascals, mass in kilograms.
        Assert.InRange(point[1], 4e-5, 6e-5);
        Assert.InRange(point[2], 4e6, 7e6);
        Assert.InRange(point[3], 5e-4, 6.5e-4);

        // Valve areas come from the cam profiles, and both valves are shut at firing top
        // dead centre.
        Assert.Equal(0, point[16]);
        Assert.Equal(0, point[17]);

        // Gamma is a real ratio of specific heats, not a leftover zero.
        Assert.InRange(point[14], 1.1, 1.4);

        // Unburnt hydrocarbons are always zero: nothing computes them. See ISSUES.md B71.
        Assert.Equal(0, point[27]);
    }

    [Fact]
    public void ThePerformanceFiguresComeBackOnTheEngine()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        // SimulDat.txt reports 14.291 bar IMEP, 151.34 Nm and 63.395 kW. This is a
        // simulated run rather than the reference accumulators fed in, so it carries the
        // same ~0.3 per cent the whole-cycle comparison shows; a relative bound is the
        // honest comparison.
        void Within(double expected, double actual, string what) =>
            Assert.True(
                Math.Abs(actual - expected) / expected < 0.01,
                $"{what}: expected {expected}, got {actual:F3} "
                + $"({(actual - expected) / expected:P2}).");

        Within(14.291, result.Engine.Imep / 1e5, "IMEP");
        Within(151.34, result.Engine.Torque, "Torque");
        Within(63.395, result.Engine.BrakePower / 1e3, "Power");
    }

    [Fact]
    public void ProgressIsReportedAndTheRunCanBeCancelled()
    {
        BaselinePaths.Require();

        using var cancellation = new CancellationTokenSource();

        // Progress<T> posts to the captured synchronisation context, which in a test is
        // the thread pool, so the reports would arrive out of order and after the run had
        // finished. Reporting synchronously keeps the assertions meaningful.
        var collected = new List<SimulationProgress>();
        var runner = new SimulationRunner(new CachingExpressionEvaluator());

        Assert.Throws<OperationCanceledException>(() => runner.Run(
            BaselineEngine(),
            Settings(),
            new SynchronousProgress<SimulationProgress>(p =>
            {
                collected.Add(p);

                if (collected.Count == 200)
                {
                    cancellation.Cancel();
                }
            }),
            cancellation.Token));

        Assert.Equal(200, collected.Count);
        Assert.All(collected, p => Assert.InRange(p.CrankAngle, -359, 360));
        Assert.Equal(6, collected[0].RequestedCycles);
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
