using App.Core.Model;
using App.Core;
using App.Persistence;
using App.Ui.ViewModels;
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
    private static MainWindowViewModel Loaded()
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

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

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);
        viewModel.EngineSpeed = 3000;
        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        // Sweeping by hand is how a torque curve gets built without the multi-run grid.
        Assert.Equal(2, viewModel.Performance.Points.Count);
        Assert.Equal([4000, 3000], viewModel.Performance.Points.Select(p => p.Speed));
    }

    [AvaloniaFact]
    public void TheWindowShowsTheSpeedBoxTheRunButtonAndTheStatusLine()
    {
        var window = new MainWindow { DataContext = TestServices.Resolve<MainWindowViewModel>() };

        Assert.NotNull(window.FindControl<NumericUpDown>("EngineSpeedBox"));
        Assert.NotNull(window.FindControl<Button>("RunButton"));
        Assert.NotNull(window.FindControl<Button>("StopButton"));
        Assert.NotNull(window.FindControl<TextBlock>("RunStatusText"));
    }

    [Fact]
    public void MultiRunNeedsBothAnEngineAndAPopulatedGrid()
    {
        BaselinePaths.Require();

        var viewModel = Loaded();

        // An engine is open but the grid is empty, so there is nothing to sweep.
        Assert.Equal(0, viewModel.MultiRun.RunCount);
        Assert.False(viewModel.MultiPointSimulationCommand.CanExecute(null));

        viewModel.MultiRun[0, 0] = "4000";
        viewModel.MultiRun[0, 1] = "6";

        // RunCount is computed rather than observable, so the command is re-queried when
        // the engine or the running flag changes; a grid edit alone will not refresh it.
        Assert.True(viewModel.MultiPointSimulationCommand.CanExecute(null));
    }

    [Fact]
    public async Task AMultiRunBuildsTheTorqueCurveAndLeavesTheLastCycleForTheCharts()
    {
        BaselinePaths.Require();

        var viewModel = Loaded();
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        viewModel.MultiRun[0, 0] = "3000";
        viewModel.MultiRun[0, 1] = "6";
        viewModel.MultiRun[1, 0] = "4000";
        viewModel.MultiRun[1, 1] = "6";

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

        var viewModel = Loaded();
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        // A single-point run first, then a sweep: the sweep shows itself, not an
        // accumulation, which is what the original does before a multi-run.
        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Performance.Points);

        viewModel.MultiRun[0, 0] = "3000";
        viewModel.MultiRun[0, 1] = "6";

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        var point = Assert.Single(viewModel.Performance.Points);
        Assert.Equal(3000, point.Speed);
    }
}
