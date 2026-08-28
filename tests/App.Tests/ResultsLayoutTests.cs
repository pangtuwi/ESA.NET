using App.Core.Charts;
using App.Ui.Charts;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using ScottPlot.Avalonia;

namespace App.Tests;

/// <summary>
/// Pins the results screen to the layout the Delphi original showed: the run's figures in
/// the top left quadrant and a chart in each of the other three.
/// </summary>
public sealed class ResultsLayoutTests
{
    private static MainWindow Window() =>
        new() { DataContext = TestServices.Resolve<MainWindowViewModel>() };

    private static T Control<T>(MainWindow window, string name)
        where T : Control =>
        window.FindControl<T>(name)
        ?? throw new InvalidOperationException($"The shell window has no {name}.");

    [AvaloniaFact]
    public void ResultsScreenIsFourQuadrants()
    {
        var grid = Control<Grid>(Window(), "ResultsGrid");

        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
    }

    [AvaloniaFact]
    public void FiguresOccupyTheTopLeftQuadrant()
    {
        var window = Window();
        var quadrant = Control<Border>(window, "FiguresQuadrant");

        Assert.Equal(0, Grid.GetRow(quadrant));
        Assert.Equal(0, Grid.GetColumn(quadrant));
        Assert.Contains(
            Control<Grid>(window, "ResultsPanel"),
            quadrant.GetLogicalDescendants().OfType<Grid>());
    }

    [AvaloniaFact]
    public void TheOtherThreeQuadrantsHoldCharts()
    {
        var window = Window();

        var placements = new[]
        {
            ("PressureVolumeQuadrant", "PressureVolumePlot"),
            ("GasFlowQuadrant", "GasFlowPlot"),
            ("InCylinderQuadrant", "InCylinderPlot"),
        }
        .Select(pair =>
        {
            var quadrant = Control<Border>(window, pair.Item1);
            Assert.Same(Control<AvaPlot>(window, pair.Item2), quadrant.Child);
            return (Grid.GetRow(quadrant), Grid.GetColumn(quadrant));
        })
        .ToList();

        Assert.Equal([(0, 1), (1, 0), (1, 1)], placements);
    }

    [AvaloniaFact]
    public void ChartsDrawWhenTheViewModelSuppliesThem()
    {
        var window = Window();
        var plot = Control<AvaPlot>(window, "PressureVolumePlot");

        ChartHost.SetDefinition(
            plot,
            new ChartDefinition(
                "P-V", "Volume", "Pressure",
                [new ChartSeries("Cycle", [1, 2, 3], [3, 2, 1])]));

        Assert.Equal("P-V", plot.Plot.Axes.Title.Label.Text);
        Assert.Single(plot.Plot.GetPlottables<ScottPlot.Plottables.Scatter>());
    }

    [AvaloniaFact]
    public void AFixedVerticalRangeStillFitsTheHorizontalAxisToTheData()
    {
        var window = Window();
        var plot = Control<AvaPlot>(window, "GasFlowPlot");

        // A chart with fixed vertical limits, as the gas-flow charts have. Setting only
        // those left ScottPlot's default -10 to 10 on the horizontal axis, which showed a
        // five degree slice of the cycle.
        ChartHost.SetDefinition(
            plot,
            new ChartDefinition(
                "Gas Flow : Pressures", "Crank Angle°", "Pressure [bar]",
                [new ChartSeries("Cylinder", [0, 360, 720], [1, 2, 1])],
                YMinimum: 0.5,
                YMaximum: 2.5));

        var limits = plot.Plot.Axes.GetLimits();

        Assert.InRange(limits.Left, -100, 0);
        Assert.InRange(limits.Right, 720, 820);
        Assert.Equal(0.5, limits.Bottom, 6);
        Assert.Equal(2.5, limits.Top, 6);
    }

    [AvaloniaFact]
    public void ValveEventsAreRuledAcrossTheGasFlowChart()
    {
        var window = Window();
        var plot = Control<AvaPlot>(window, "GasFlowPlot");

        ChartHost.SetDefinition(
            plot,
            new ChartDefinition(
                "Gas Flow : Pressures", "Crank Angle°", "Pressure [bar]",
                [new ChartSeries("Cylinder", [0, 360, 720], [1, 2, 1])],
                Markers: [new ChartMarker("TDC", 360, AtBottom: true), new ChartMarker("IVC", 600)]));

        Assert.Equal(2, plot.Plot.GetPlottables<ScottPlot.Plottables.VerticalLine>().Count());
        Assert.Equal(
            ["TDC", "IVC"],
            plot.Plot.GetPlottables<ScottPlot.Plottables.Text>().Select(t => t.LabelText));
    }
}
