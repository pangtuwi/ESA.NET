using App.Core.Charts;
using App.Core.Model;
using App.Ui.Charts;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ScottPlot.Avalonia;

namespace App.Tests;

/// <summary>
/// The rendering side of the charts: that a definition reaches a plot and produces the
/// series it describes.
/// </summary>
public sealed class ChartRenderingTests
{
    private static ChartDefinition Sample() => new(
        "Test Chart",
        "X [unit]",
        "Y [unit]",
        [
            new ChartSeries("First", [1, 2, 3], [10, 20, 30]),
            new ChartSeries("Second", [1, 2, 3], [5, 15, 25]),
        ],
        YMinimum: -150,
        YMaximum: 450);

    [Fact]
    public void TheRendererDrawsOneLinePerSeries()
    {
        var plot = new ScottPlot.Plot();

        ChartRenderer.Apply(plot, Sample());

        var lines = plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal("First", lines[0].LegendText);
        Assert.Equal("Second", lines[1].LegendText);
    }

    [Fact]
    public void FixedAxisLimitsAreApplied()
    {
        var plot = new ScottPlot.Plot();

        ChartRenderer.Apply(plot, Sample());

        // The gas-flow velocity chart pins its axis rather than fitting the data, so a
        // definition that carries limits has to override autoscaling.
        var limits = plot.Axes.GetLimits();

        Assert.Equal(-150, limits.Bottom, 6);
        Assert.Equal(450, limits.Top, 6);
    }

    [Fact]
    public void ADefinitionWithoutLimitsFitsItsData()
    {
        var plot = new ScottPlot.Plot();

        ChartRenderer.Apply(plot, Sample() with { YMinimum = null, YMaximum = null });

        var limits = plot.Axes.GetLimits();

        // Autoscaled around the data's own 5 to 30, not the fixed -150 to 450.
        Assert.InRange(limits.Bottom, 0, 5);
        Assert.InRange(limits.Top, 30, 40);
    }

    [Fact]
    public void RenderingTwiceReplacesRatherThanAccumulates()
    {
        var plot = new ScottPlot.Plot();

        ChartRenderer.Apply(plot, Sample());
        ChartRenderer.Apply(plot, Sample());

        // Redrawing is how the original refreshes a chart after a run, so it must clear
        // first or every run would leave its predecessor behind.
        Assert.Equal(2, plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>().Count());
    }

    [AvaloniaFact]
    public void TheChartWindowBindsItsDefinitionThroughToThePlot()
    {
        var window = new ChartWindow
        {
            DataContext = new ChartWindowViewModel { Definition = Sample() },
        };

        window.Show();

        var plot = window.FindControl<AvaPlot>("Plot")
                   ?? throw new InvalidOperationException("The chart window has no plot.");

        // The attached property does the drawing, so the view's code-behind stays empty.
        Assert.Equal(2, plot.Plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>().Count());
        Assert.Equal("Test Chart", window.Title);
    }

    [AvaloniaFact]
    public void EveryGraphMenuItemIsBoundToACommand()
    {
        var window = new MainWindow { DataContext = TestServices.Resolve<MainWindowViewModel>() };
        var menu = window.FindControl<Menu>("MainMenu")
                   ?? throw new InvalidOperationException("The shell window has no menu.");

        var graph = menu.Items.OfType<MenuItem>().Single(i => (i.Header as string) == "_Graph");
        var leaves = graph.Items.OfType<MenuItem>().ToList();

        // Four from the original plus the five run-time charts it drew in its main window.
        Assert.Equal(9, leaves.Count);
        Assert.All(leaves, item => Assert.NotNull(item.Command));
    }

    [Fact]
    public void ChartCommandsStayDisabledUntilThereIsSomethingToPlot()
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        // Nothing has been run, so the charts that need a captured cycle are unavailable
        // rather than throwing when invoked.
        Assert.False(viewModel.EnergyBalanceCommand.CanExecute(null));
        Assert.False(viewModel.PressureVolumeCommand.CanExecute(null));
        Assert.False(viewModel.InCylinderCommand.CanExecute(null));

        // The torque curve needs points from a multi-run, and the cam profiles need only
        // an engine - neither of which is loaded either.
        Assert.False(viewModel.TorqueCurveCommand.CanExecute(null));
        Assert.False(viewModel.ValveOpeningCommand.CanExecute(null));

        // Setting the trace must not only make CanExecute true but say so: without a
        // change notification the menu items would stay greyed out until something else
        // forced a re-query, which CanExecute alone does not catch.
        var notified = false;
        viewModel.EnergyBalanceCommand.CanExecuteChanged += (_, _) => notified = true;

        viewModel.Trace = new CrankAngleTrace();

        Assert.True(viewModel.EnergyBalanceCommand.CanExecute(null));
        Assert.True(viewModel.PressureVolumeCommand.CanExecute(null));
        Assert.True(notified, "Setting the trace did not raise CanExecuteChanged.");
    }
}
