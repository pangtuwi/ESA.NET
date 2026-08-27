namespace App.Core.Charts;

/// <summary>One plotted line: a name for the legend and the points to draw.</summary>
public sealed record ChartSeries(string Name, double[] X, double[] Y)
{
    /// <summary>How many points the series holds.</summary>
    public int Count => X.Length;
}

/// <summary>
/// Everything needed to draw one of the original's charts, with no drawing library in
/// sight.
/// </summary>
/// <remarks>
/// The Delphi forms build their TeeChart series inline, mixing the data transform, the
/// axis setup and the widget together. Splitting the first two out means the numbers can
/// be tested without a display, and the view is left with nothing to do but render.
/// </remarks>
/// <param name="Title">Chart heading.</param>
/// <param name="XAxisLabel">Horizontal axis caption, units included.</param>
/// <param name="YAxisLabel">Vertical axis caption.</param>
/// <param name="Series">The lines to draw, in the original's order.</param>
/// <param name="YMinimum">Fixed lower limit, or <see langword="null"/> to fit the data.</param>
/// <param name="YMaximum">Fixed upper limit, or <see langword="null"/> to fit the data.</param>
public sealed record ChartDefinition(
    string Title,
    string XAxisLabel,
    string YAxisLabel,
    IReadOnlyList<ChartSeries> Series,
    double? YMinimum = null,
    double? YMaximum = null);
