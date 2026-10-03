using App.Core;
using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Run ▸ Pause and Run ▸ QuickRun, both stubs until now.
/// </summary>
public sealed class PauseAndQuickRunTests
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
        new() { CycleCount = 3, OneZoneCycleCount = 1, MassBalance = 0 };

    /// <summary>
    /// Counts steps as they happen, on the simulation's own thread, and presses Pause at
    /// a given step.
    /// </summary>
    /// <remarks>
    /// Pressing it from here rather than from the test's thread is what makes the test
    /// deterministic. Polled from outside, a busy machine can leave the poll unscheduled
    /// until the run has finished, and the pause then catches nothing.
    /// </remarks>
    private sealed class StepCounter(RunPause pause, int pauseAt) : IProgress<SimulationProgress>
    {
        private int _steps;

        public int Steps => Volatile.Read(ref _steps);

        public void Report(SimulationProgress value)
        {
            if (Interlocked.Increment(ref _steps) == pauseAt)
            {
                pause.Pause();
            }
        }
    }

    private const int PauseAt = 50;

    /// <summary>Waits until the run has been paused, with a limit so a fault cannot hang the suite.</summary>
    private static async Task WaitForPauseAsync(StepCounter steps, CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);

        while (steps.Steps < PauseAt)
        {
            Assert.True(DateTime.UtcNow < deadline, $"The run made only {steps.Steps} step(s).");
            await Task.Delay(10, token);
        }
    }

    /// <summary>
    /// A paused run makes no progress, and once resumed it finishes exactly as an
    /// uninterrupted one does - the gate holds the run without touching it.
    /// </summary>
    [Fact]
    public async Task APausedRunHoldsAndThenFinishesBitForBitTheSame()
    {
        BaselinePaths.Require();

        var token = TestContext.Current.CancellationToken;
        var runner = new SimulationRunner(new CachingExpressionEvaluator());

        var uninterrupted = runner.Run(BaselineEngine(), Settings(), cancellation: token);

        using var pause = new RunPause();
        var steps = new StepCounter(pause, PauseAt);

        var paused = Task.Run(
            () => runner.Run(BaselineEngine(), Settings(), steps, token, pause: pause), token);

        // Paused part-way through, at the end of step 50. Nothing more happens however long
        // it is left.
        await WaitForPauseAsync(steps, token);
        await Task.Delay(500, token);

        Assert.Equal(PauseAt, steps.Steps);
        Assert.False(paused.IsCompleted);

        pause.Resume();

        var resumed = await paused;

        Assert.Equal(uninterrupted.CyclesRun, resumed.CyclesRun);
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(uninterrupted.Engine.Torque),
            BitConverter.DoubleToInt64Bits(resumed.Engine.Torque));
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(uninterrupted.Engine.Imep),
            BitConverter.DoubleToInt64Bits(resumed.Engine.Imep));
    }

    [Fact]
    public async Task StopEndsAPausedRun()
    {
        BaselinePaths.Require();

        var token = TestContext.Current.CancellationToken;
        var runner = new SimulationRunner(new CachingExpressionEvaluator());

        using var pause = new RunPause();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var steps = new StepCounter(pause, PauseAt);

        var paused = Task.Run(
            () => runner.Run(BaselineEngine(), Settings(), steps, stop.Token, pause: pause), token);

        await WaitForPauseAsync(steps, token);
        await Task.Delay(200, token);

        Assert.False(paused.IsCompleted);

        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => paused);
        Assert.True(pause.IsPaused);
    }

    [Fact]
    public void PauseIsOnlyAvailableWhileRunning()
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        Assert.False(viewModel.PauseCommand.CanExecute(null));
    }

    [Fact]
    public void QuickRunIsAvailableWithNoEngineOpen()
    {
        // It opens the default engine itself, so it needs none to start with.
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        Assert.True(viewModel.QuickRunCommand.CanExecute(null));
    }

    /// <summary>
    /// No ESA.ini sits beside the test assembly, so the default engine cannot be found.
    /// QuickRun must stop there and not fall through to running the engine already open,
    /// which is what the original did.
    /// </summary>
    [Fact]
    public async Task QuickRunStopsWhenTheDefaultEngineWillNotOpen()
    {
        BaselinePaths.Require();

        var options = new StubSimulateOptions();
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<App.Ui.Dialogs.ISimulateOptionsWindowService>(options));

        var open = TestServices.Resolve<IEngineLoader>().Load(BaselinePaths.File("A2China.eng"));
        viewModel.CurrentEngine = open;

        await viewModel.QuickRunCommand.ExecuteAsync(null);

        Assert.Equal(0, options.Opened);
        Assert.Same(open, viewModel.CurrentEngine);
        Assert.Contains("not found", viewModel.RunStatus, StringComparison.Ordinal);
        Assert.Null(viewModel.Trace);
    }
}
