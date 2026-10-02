using App.Core.Model;
using App.Core;
using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace App.Tests;

/// <summary>
/// The seam between the simulation and the window: that pressing Run actually simulates,
/// and that what comes back reaches the charts and the status line.
/// </summary>
public sealed class SimulationWiringTests
{
    private static MainWindowViewModel Loaded() => Loaded(new StubMultiRunEditor());

    private static MainWindowViewModel Loaded(StubMultiRunEditor editor) =>
        Loaded(editor, new StubSimulateOptions());

    private static MainWindowViewModel Loaded(
        StubMultiRunEditor editor, StubSimulateOptions options)
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services =>
            {
                services.AddSingleton<IMultiRunWindowService>(editor);
                services.AddSingleton<ISimulateOptionsWindowService>(options);
            });

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        viewModel.EngineSpeed = 4000;
        viewModel.Settings.CycleCount = 6;
        viewModel.Settings.OneZoneCycleCount = 1;
        viewModel.Settings.MassBalance = 1;

        return viewModel;
    }

    [Fact]
    public void RunningIsUnavailableUntilAnEngineIsOpen()
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        Assert.False(viewModel.SinglePointSimulationCommand.CanExecute(null));
        Assert.False(viewModel.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task RunningPopulatesTheTraceThePerformancePointAndTheStatus()
    {
        BaselinePaths.Require();

        var viewModel = Loaded();

        Assert.True(viewModel.SinglePointSimulationCommand.CanExecute(null));

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        // The captured cycle reaches the view model, so the charts have something to draw.
        Assert.NotNull(viewModel.Trace);
        Assert.True(viewModel.EnergyBalanceCommand.CanExecute(null));
        Assert.True(viewModel.PressureVolumeCommand.CanExecute(null));

        // And a point lands on the torque curve.
        var point = Assert.Single(viewModel.Performance.Points);
        Assert.Equal(4000, point.Speed);
        Assert.InRange(point.Torque, 140, 165);
        Assert.True(viewModel.TorqueCurveCommand.CanExecute(null));

        // The status line reports what happened rather than staying blank.
        Assert.Contains("Converged", viewModel.RunStatus, StringComparison.Ordinal);
        Assert.Contains("Torque", viewModel.RunStatus, StringComparison.Ordinal);

        // And the run is over, so another can start.
        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.SinglePointSimulationCommand.CanExecute(null));
    }

    [Fact]
    public async Task ASecondRunAddsASecondPointToTheCurve()
    {
        BaselinePaths.Require();

        var viewModel = Loaded();

        var options = new StubSimulateOptions();
        viewModel = Loaded(new StubMultiRunEditor(), options);

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        // The speed for the second run is typed into the dialog, not onto the main window.
        options.EngineSpeed = 3000;

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        // Sweeping by hand is how a torque curve gets built without the multi-run grid.
        Assert.Equal(2, viewModel.Performance.Points.Count);
        Assert.Equal([4000, 3000], viewModel.Performance.Points.Select(p => p.Speed));
    }

    [AvaloniaFact]
    public void TheWindowCarriesNoRunControlsOfItsOwn()
    {
        // The original's main form has no speed box and no Run, Stop or Multi-Point
        // buttons: speed, cycles and mass balance are asked for by the Single Speed
        // Simulation dialog, and running is driven from the Run menu.
        var window = new MainWindow { DataContext = TestServices.Resolve<MainWindowViewModel>() };

        Assert.Null(window.FindControl<NumericUpDown>("EngineSpeedBox"));
        Assert.Null(window.FindControl<Button>("RunButton"));
        Assert.Null(window.FindControl<Button>("StopButton"));
        Assert.Null(window.FindControl<Button>("RunMultiPointButton"));

        // The status line stays: Delphi keeps the same two facts on its status bar.
        Assert.NotNull(window.FindControl<TextBlock>("RunStatusText"));
    }

    [Fact]
    public async Task MultiRunNeedsAnEngineAndAGridWithRunsInIt()
    {
        BaselinePaths.Require();

        // Nothing open: the command is unavailable whatever the grid holds, as it is for
        // a single point run.
        Assert.False(
            TestServices.Resolve<MainWindowViewModel>().MultiPointSimulationCommand.CanExecute(null));

        var editor = new StubMultiRunEditor();
        var viewModel = Loaded(editor);

        // With an engine open the command is available even though the grid is empty:
        // the operator fills it in on the window the command itself opens.
        Assert.Equal(0, viewModel.MultiRun.RunCount);
        Assert.True(viewModel.MultiPointSimulationCommand.CanExecute(null));

        // Pressing OK on an empty grid sweeps nothing and says so.
        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(1, editor.Opened);
        Assert.Empty(viewModel.Performance.Points);
        Assert.Contains("holds no runs", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellingTheGridEditorRunsNothing()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Accept = false };
        var viewModel = Loaded(editor);

        editor.Grid = Grid(("3000", "6"));

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        // The edits are kept - the original's single grid window remembers what was typed
        // - but nothing was run.
        Assert.Equal(1, viewModel.MultiRun.RunCount);
        Assert.Empty(viewModel.Performance.Points);
    }

    /// <summary>A grid holding one row per pair of speed and cycle count.</summary>
    private static MultiRunGrid Grid(params (string Speed, string Cycles)[] rows)
    {
        var grid = new MultiRunGrid();

        for (var row = 0; row < rows.Length; row++)
        {
            grid[row, 0] = rows[row].Speed;
            grid[row, 1] = rows[row].Cycles;
        }

        return grid;
    }

    [Fact]
    public async Task AMultiRunBuildsTheTorqueCurveAndLeavesTheLastCycleForTheCharts()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Grid = Grid(("3000", "6"), ("4000", "6")) };
        var viewModel = Loaded(editor);
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        // One point per row, in grid order.
        Assert.Equal(2, viewModel.Performance.Points.Count);
        Assert.Equal([3000, 4000], viewModel.Performance.Points.Select(p => p.Speed));
        Assert.True(viewModel.TorqueCurveCommand.CanExecute(null));

        // The last row's cycle is what the charts show.
        Assert.NotNull(viewModel.Trace);
        Assert.True(viewModel.PressureVolumeCommand.CanExecute(null));

        Assert.Contains("Completed 2 runs", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMultiRunClearsAnyEarlierCurveFirst()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Grid = Grid(("3000", "6")) };
        var viewModel = Loaded(editor);
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        // A single-point run first, then a sweep: the sweep shows itself, not an
        // accumulation, which is what the original does before a multi-run.
        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Performance.Points);

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        var point = Assert.Single(viewModel.Performance.Points);
        Assert.Equal(3000, point.Speed);
    }

    [Fact]
    public async Task ALateProgressReportDoesNotOverwriteTheFinalStatus()
    {
        BaselinePaths.Require();

        var viewModel = Loaded();
        var context = new LateProgressContext();

        // Every progress report is held until the run has finished and written its final
        // status, then released - the ordering that failed once in about a dozen full-suite
        // runs when the thread pool happened to produce it (ISSUES.md A15).
        await context.Start(() => viewModel.SinglePointSimulationCommand.ExecuteAsync(null));

        Assert.True(context.Held > 0, "The run made no progress reports, so this proves nothing.");

        await context.ReleaseAsync();

        Assert.Contains("Converged", viewModel.RunStatus, StringComparison.Ordinal);
        Assert.Contains("Torque", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALateProgressReportDoesNotOverwriteTheSweepsFinalStatus()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Grid = Grid(("4000", "6")) };
        var viewModel = Loaded(editor);
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        var context = new LateProgressContext();

        await context.Start(() => viewModel.MultiPointSimulationCommand.ExecuteAsync(null));

        Assert.True(context.Held > 0, "The sweep made no progress reports, so this proves nothing.");

        await context.ReleaseAsync();

        Assert.Contains("Completed 1 runs", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwitchingToVelocitiesAfterARunRedrawsTheGasFlowChart()
    {
        BaselinePaths.Require();

        // ISSUES.md C7: in the original, switching the gas-flow chart to velocities after
        // a run changed the panel title but not the plot, because the mode was read on a
        // refresh timer that had stopped, and the axis still read Pressure [bar]. The
        // port's Graph Options dialog redraws from the last run's trace instead.
        var options = new StubGraphOptions { Velocities = true };

        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
        {
            services.AddSingleton<IMultiRunWindowService>(new StubMultiRunEditor());
            services.AddSingleton<ISimulateOptionsWindowService>(new StubSimulateOptions());
            services.AddSingleton<IRunTimeGraphOptionsWindowService>(options);
        });

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));
        viewModel.EngineSpeed = 4000;
        viewModel.Settings.CycleCount = 6;
        viewModel.Settings.OneZoneCycleCount = 1;
        viewModel.Settings.MassBalance = 1;

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.Equal("Pressure [bar]", viewModel.GasFlowChart!.YAxisLabel);

        // The run is over, so nothing else will redraw: the switch has to do it.
        await viewModel.RunTimeGraphOptionsCommand.ExecuteAsync(null);

        var chart = viewModel.GasFlowChart!;
        Assert.Equal("Gas Flow : Velocities", chart.Title);
        Assert.Equal("Velocity [m/s]", chart.YAxisLabel);
        Assert.Equal(["Inlet", "Exhaust"], chart.Series.Select(s => s.Name));
        Assert.All(chart.Series, s => Assert.NotEmpty(s.Y));

        // And back again.
        options.Velocities = false;
        await viewModel.RunTimeGraphOptionsCommand.ExecuteAsync(null);

        Assert.Equal("Pressure [bar]", viewModel.GasFlowChart!.YAxisLabel);
    }

    /// <summary>Answers the Graph Options dialog with OK and a chosen mode.</summary>
    private sealed class StubGraphOptions : IRunTimeGraphOptionsWindowService
    {
        public bool Velocities { get; set; }

        public Task<RunTimeGraphOptionsResult> ShowAsync(bool showGasFlowVelocities) =>
            Task.FromResult(new RunTimeGraphOptionsResult(Accepted: true, Velocities));
    }
}
