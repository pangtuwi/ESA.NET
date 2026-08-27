using App.Core.Charts;
using ScottPlot;

namespace App.Ui.Charts;

/// <summary>
/// Draws a <see cref="ChartDefinition"/> onto a ScottPlot plot.
/// </summary>
/// <remarks>
/// The only place in the application that knows both the chart data and the drawing
/// library. Everything upstream works in <see cref="ChartDefinition"/>, which carries no
/// dependency on ScottPlot, so swapping the charting library would mean rewriting this
/// file and nothing else.
/// </remarks>
public static class ChartRenderer
{
    /// <summary>Clears <paramref name="plot"/> and draws <paramref name="definition"/>.</summary>
    public static void Apply(Plot plot, ChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ArgumentNullException.ThrowIfNull(definition);

        plot.Clear();

        foreach (var series in definition.Series)
        {
            var line = plot.Add.ScatterLine(series.X, series.Y);
            line.LegendText = series.Name;
            line.MarkerStyle = MarkerStyle.None;
        }

        plot.Title(definition.Title);
        plot.XLabel(definition.XAxisLabel);
        plot.YLabel(definition.YAxisLabel);

        // The original fixes the vertical range on some charts and lets others fit the
        // data; a definition says which by leaving the limits null.
        if (definition.YMinimum is { } minimum && definition.YMaximum is { } maximum)
        {
            plot.Axes.SetLimitsY(minimum, maximum);
        }
        else
        {
            plot.Axes.AutoScale();
        }

        plot.ShowLegend();
    }
}
