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

        // A second vertical axis, added only when a series asks for one, so charts that
        // do not need it keep a plain single-axis frame.
        var right = definition.Series.Any(s => s.UseRightAxis)
            ? plot.Axes.AddRightAxis()
            : null;

        foreach (var series in definition.Series)
        {
            var line = plot.Add.ScatterLine(series.X, series.Y);
            line.LegendText = series.Name;
            line.MarkerStyle = MarkerStyle.None;

            if (series.UseRightAxis && right is not null)
            {
                line.Axes.YAxis = right;
            }
        }

        plot.Title(definition.Title);
        plot.XLabel(definition.XAxisLabel);
        plot.YLabel(definition.YAxisLabel);

        if (right is not null && definition.RightYAxisLabel is { } rightLabel)
        {
            right.LabelText = rightLabel;
        }

        // Fit the data first, then override the vertical range where the original fixes
        // it. Setting only the vertical limits would leave the horizontal axis at
        // ScottPlot's default -10 to 10, which shows a five degree slice of the cycle.
        plot.Axes.AutoScale();

        if (definition.YMinimum is { } minimum && definition.YMaximum is { } maximum)
        {
            plot.Axes.SetLimitsY(minimum, maximum);
        }

        DrawMarkers(plot, definition);

        // A legend naming the single line a one-series chart already names in its title
        // is noise; the original does not draw one either.
        if (definition.Series.Count > 1)
        {
            plot.ShowLegend();
        }
        else
        {
            plot.HideLegend();
        }
    }

    /// <summary>
    /// Rules the valve events and dead centres across the plot, captioned inside the data
    /// area as the original does rather than out on the axis, where the labels would
    /// collide with the title.
    /// </summary>
    private static void DrawMarkers(Plot plot, ChartDefinition definition)
    {
        if (definition.Markers is not { Count: > 0 } markers)
        {
            return;
        }

        // Read the limits back after scaling so the captions can be placed a fixed
        // fraction inside the frame rather than at an absolute value.
        var limits = plot.Axes.GetLimits();
        var span = limits.Top - limits.Bottom;

        foreach (var marker in markers)
        {
            var line = plot.Add.VerticalLine(marker.X);
            line.LineWidth = 1;
            line.LinePattern = LinePattern.Dashed;
            line.LineColor = MarkerColour(marker);

            var text = plot.Add.Text(
                marker.Label,
                marker.X,
                marker.AtBottom ? limits.Bottom + (span * 0.04) : limits.Top - (span * 0.04));

            text.LabelFontSize = 9;
            text.LabelBold = true;
            text.LabelAlignment = Alignment.MiddleCenter;
            text.LabelFontColor = Colors.White;
            text.LabelBackgroundColor = MarkerColour(marker);
        }
    }

    /// <summary>
    /// The original's colouring: dead centres in black, the exhaust events green and the
    /// inlet events cyan.
    /// </summary>
    private static Color MarkerColour(ChartMarker marker) => marker.Label switch
    {
        "EVO" or "EVC" => Colors.Green,
        "IVO" or "IVC" => Colors.Teal,
        _ => Colors.Black,
    };
}
