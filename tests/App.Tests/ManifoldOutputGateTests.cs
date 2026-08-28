using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// When the nine manifold output files get written. The original wanted three conditions
/// and a converged run satisfied only two of them, so ticking <c>Save Manifold Data</c>
/// could produce nothing at all - ISSUES.md C1.
/// </summary>
public sealed class ManifoldOutputGateTests
{
    /// <summary>Counts rows and resets, standing in for the file writer.</summary>
    private sealed class CountingRecorder : IManifoldRecorder
    {
        public int Rows { get; private set; }

        public int Resets { get; private set; }

        public double FirstCrankAngle { get; private set; } = double.NaN;

        public void Record(in ManifoldRow row)
        {
            if (Rows == 0)
            {
                FirstCrankAngle = row.CrankAngle;
            }

            Rows++;
        }

        public void Reset()
        {
            Rows = 0;
            Resets++;
            FirstCrankAngle = double.NaN;
        }
    }

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

    private static SimulationRunner Runner() => new(new CachingExpressionEvaluator());

    /// <summary>Converges well before the requested cycle count on the baseline engine.</summary>
    private static SimulationSettings Converging() =>
        new() { CycleCount = 20, OneZoneCycleCount = 1, MassBalance = 1 };

    [Fact]
    public void TickingSaveManifoldDataIsTheWholeGate()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        engine.Manifold.SaveManifoldData = true;

        var recorder = new CountingRecorder();
        var result = Runner().Run(
            engine, Converging(), cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: recorder);

        Assert.True(result.ManifoldDataCaptured);
        Assert.True(recorder.Rows > 0, "Ticking the box recorded nothing.");
    }

    [Fact]
    public void LeavingItUntickedRecordsNothing()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        engine.Manifold.SaveManifoldData = false;

        var recorder = new CountingRecorder();
        var result = Runner().Run(
            engine, Converging(), cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: recorder);

        Assert.False(result.ManifoldDataCaptured);
        Assert.Equal(0, recorder.Rows);
        Assert.Equal(0, recorder.Resets);
    }

    [Fact]
    public void AConvergedRunStillWritesItsManifoldData()
    {
        BaselinePaths.Require();

        // The C1 trap: the original gates on reaching the last *requested* cycle, and a
        // run that converges early exits before it - 20 requested here against a baseline
        // engine that settles in a handful, so the original would have produced nothing.
        var engine = BaselineEngine();
        engine.Manifold.SaveManifoldData = true;

        var recorder = new CountingRecorder();
        var result = Runner().Run(
            engine, Converging(), cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: recorder);

        Assert.True(result.Converged, "Expected this run to converge early.");
        Assert.True(result.CyclesRun < 20, "Expected fewer cycles than were requested.");
        Assert.True(recorder.Rows > 0, "A converged run wrote no manifold data.");
    }

    [Fact]
    public void TheRowsAreTheLastCycleRunRatherThanEveryCycle()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        engine.Manifold.SaveManifoldData = true;

        var recorder = new CountingRecorder();
        var result = Runner().Run(
            engine, Converging(), cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: recorder);

        // One reset per cycle actually run, and what survives is one cycle's window: the
        // 620 steps from firing top dead centre round to inlet valve closing.
        Assert.Equal(result.CyclesRun, recorder.Resets);
        Assert.InRange(recorder.Rows, 600, 640);

        // And that window starts at firing top dead centre, not at the cycle's own start.
        Assert.Equal(360, recorder.FirstCrankAngle);
    }

    [Fact]
    public void TheWindowIsTheOriginalsAndStartsAtFiringTopDeadCentre()
    {
        // 620 rows, not 720: the hundred steps before top dead centre belong to the
        // previous cycle. Pinned here as well as in ManifoldTraceWriterTests because the
        // runner now depends on it.
        Assert.True(ManifoldCaptureWindow.Contains(360, -100));
        Assert.True(ManifoldCaptureWindow.Contains(720, -100));
        Assert.True(ManifoldCaptureWindow.Contains(259, -100));

        Assert.False(ManifoldCaptureWindow.Contains(260, -100));
        Assert.False(ManifoldCaptureWindow.Contains(359, -100));
    }

    [Fact]
    public void TheCaptureWindowForwardsResetToItsSink()
    {
        var inner = new CountingRecorder();
        var window = new ManifoldCaptureWindow(inner, -100);

        window.Reset();

        Assert.Equal(1, inner.Resets);
    }
}
