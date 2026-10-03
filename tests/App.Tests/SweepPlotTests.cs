using App.Core.Charts;
using App.Core.Model;
using App.Core.Simulation;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Graph ▸ Multi-Run Results: any variable of a sweep plotted against any other.
/// </summary>
public sealed class SweepPlotTests
{
    /// <summary>An engine as a sweep row leaves it, with the figures a test needs.</summary>
    private static Engine Ran(double rpm, double torque, double spark = 30, double burnAngle = 55)
    {
        var engine = new Engine { Rpm = rpm, Torque = torque, Imep = 10.5e5, PeakPressure = 62e5, TotalMass = 412e-6 };

        engine.Cylinder.ThetaSpark = -spark;
        engine.Cylinder.Fuel.BurnAngle = burnAngle;
        engine.Manifold.InletValve.OpenAngle = EngineFileUnits.InletOpen(19);
        engine.Manifold.InletValve.CloseAngle = EngineFileUnits.InletClose(80);
        engine.Manifold.ExhaustValve.OpenAngle = EngineFileUnits.ExhaustOpen(60);
        engine.Manifold.ExhaustValve.CloseAngle = EngineFileUnits.ExhaustClose(41);
        engine.Manifold.InletValve.MaxLift = EngineFileUnits.Length(8.62);

        return engine;
    }

    private static MultiRunRowResult Row(int row, Engine engine) =>
        new(row, engine.Rpm, new SimulationResult(engine, new CrankAngleTrace(), 3, Converged: true), null);

    private static MultiRunRowResult Failed(int row, double rpm) =>
        new(row, rpm, null, "fx1*fx2 > 0 in Inlet Subsonic Velocity Solve");

    private static SweepVariable Named(string name) =>
        SweepVariables.All.Single(v => v.Name == name);

    [Theory]
    [InlineData(19.0)]
    [InlineData(-5.0)]
    [InlineData(0.0)]
    public void TheTimingInversesUndoTheEditorsConversions(double degrees)
    {
        Assert.Equal(degrees, EngineFileUnits.ToInletOpen(EngineFileUnits.InletOpen(degrees)));
        Assert.Equal(degrees, EngineFileUnits.ToInletClose(EngineFileUnits.InletClose(degrees)));
        Assert.Equal(degrees, EngineFileUnits.ToExhaustOpen(EngineFileUnits.ExhaustOpen(degrees)));
        Assert.Equal(degrees, EngineFileUnits.ToExhaustClose(EngineFileUnits.ExhaustClose(degrees)));
        Assert.Equal(degrees, EngineFileUnits.ToMillimetres(EngineFileUnits.Length(degrees)), 12);
    }

    [Fact]
    public void EachVariableReadsTheEngineInTheUnitsTheOperatorUses()
    {
        var engine = Ran(4000, 152.4, spark: 32, burnAngle: 50);

        Assert.Equal(4000, Named("Engine speed").Value(engine));
        Assert.Equal(152.4, Named("Torque").Value(engine));
        Assert.Equal(32, Named("Spark advance").Value(engine));
        Assert.Equal(50, Named("Burn angle").Value(engine));
        Assert.Equal(19, Named("Inlet valve opens").Value(engine));
        Assert.Equal(80, Named("Inlet valve closes").Value(engine));
        Assert.Equal(60, Named("Exhaust valve opens").Value(engine));
        Assert.Equal(41, Named("Exhaust valve closes").Value(engine));
        Assert.Equal(8.62, Named("Inlet valve lift").Value(engine), 12);
        Assert.Equal(10.5, Named("IMEP").Value(engine), 12);
        Assert.Equal(62, Named("Peak cylinder pressure").Value(engine), 12);
        Assert.Equal(412, Named("Trapped mass").Value(engine), 9);

        Assert.Equal(SweepVariables.All.Count, SweepVariables.All.Select(v => v.Name).Distinct().Count());
        Assert.All(SweepVariables.All, v => Assert.False(string.IsNullOrWhiteSpace(v.Unit)));
    }

    [Fact]
    public void ThePlotLeavesOutFailedRowsAndRunsAlongTheHorizontalAxis()
    {
        IReadOnlyList<MultiRunRowResult> rows =
        [
            Row(0, Ran(5000, 160, spark: 34)),
            Failed(1, 6000),
            Row(2, Ran(3000, 140, spark: 28)),
            Row(3, Ran(4000, 152, spark: 31)),
        ];

        var chart = EngineCharts.SweepPlot(rows, Named("Spark advance"), Named("Torque"));
        var series = Assert.Single(chart.Series);

        Assert.Equal([28, 31, 34], series.X);
        Assert.Equal([140, 152, 160], series.Y);
        Assert.Equal("Torque vs Spark advance", chart.Title);
        Assert.Equal("Spark advance [°BTDC]", chart.XAxisLabel);
        Assert.Equal("Torque [N·m]", chart.YAxisLabel);
        Assert.True(series.ShowMarkers);
        Assert.True(series.ShowLine);

        Assert.False(EngineCharts.SweepPlot(rows, Named("Spark advance"), Named("Torque"), joinPoints: false)
            .Series[0].ShowLine);
    }

    [Fact]
    public void TheDialogStartsOnTorqueAgainstSpeedAndRedrawsOnEveryChoice()
    {
        var viewModel = new SweepPlotViewModel();

        viewModel.Load([Row(0, Ran(3000, 140)), Failed(1, 6000), Row(2, Ran(4000, 152))]);

        Assert.Equal("Engine speed", viewModel.SelectedX.Name);
        Assert.Equal("Torque", viewModel.SelectedY.Name);
        Assert.Equal([3000, 4000], viewModel.Chart!.Series[0].X);
        Assert.StartsWith("2 of 3 row(s) plotted.", viewModel.Summary, StringComparison.Ordinal);
        Assert.Contains("Row 2 (6000 rev/min) failed", viewModel.Summary, StringComparison.Ordinal);

        viewModel.SelectedY = Named("IMEP");
        Assert.Equal("IMEP [bar]", viewModel.Chart!.YAxisLabel);

        viewModel.SelectedX = Named("Burn angle");
        Assert.Equal("Burn angle [°CA]", viewModel.Chart!.XAxisLabel);

        viewModel.JoinPoints = false;
        Assert.False(viewModel.Chart!.Series[0].ShowLine);

        var closed = 0;
        viewModel.CloseRequested += (_, _) => closed++;
        viewModel.CloseCommand.Execute(null);
        Assert.Equal(1, closed);
    }

    [AvaloniaFact]
    public void EveryControlInTheWindowIsBound()
    {
        var viewModel = new SweepPlotViewModel();
        viewModel.Load([Row(0, Ran(3000, 140)), Row(1, Ran(4000, 152))]);

        var window = new SweepPlotWindow { DataContext = viewModel };
        window.Show();

        try
        {
            var x = window.FindControl<ComboBox>("XAxisBox")!;
            var y = window.FindControl<ComboBox>("YAxisBox")!;

            Assert.Same(viewModel.SelectedX, x.SelectedItem);
            Assert.Same(viewModel.SelectedY, y.SelectedItem);
            Assert.Equal(SweepVariables.All.Count, x.ItemCount);

            // Choosing in the window reaches the view model and redraws.
            y.SelectedItem = Named("Brake power");
            Assert.Equal("Brake power [kW]", viewModel.Chart!.YAxisLabel);

            window.FindControl<CheckBox>("JoinPointsBox")!.IsChecked = false;
            Assert.False(viewModel.JoinPoints);

            Assert.NotNull(window.FindControl<Button>("CloseButton")?.Command);
            Assert.NotNull(window.FindControl<ScottPlot.Avalonia.AvaPlot>("SweepPlot"));
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class StubSweepPlot : ISweepPlotWindowService
    {
        public IReadOnlyList<MultiRunRowResult>? Shown { get; private set; }

        public Task ShowAsync(IReadOnlyList<MultiRunRowResult> rows)
        {
            Shown = rows;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task TheMenuCommandNeedsASweepWithARowThatRan()
    {
        var plot = new StubSweepPlot();
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<ISweepPlotWindowService>(plot));

        Assert.False(viewModel.SweepResultsCommand.CanExecute(null));

        viewModel.LastSweep = [Failed(0, 6000)];
        Assert.False(viewModel.SweepResultsCommand.CanExecute(null));

        IReadOnlyList<MultiRunRowResult> rows = [Row(0, Ran(3000, 140)), Failed(1, 6000)];
        viewModel.LastSweep = rows;
        Assert.True(viewModel.SweepResultsCommand.CanExecute(null));

        await viewModel.SweepResultsCommand.ExecuteAsync(null);

        Assert.Same(rows, plot.Shown);
    }
}
