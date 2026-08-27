namespace App.Core.Charts;

/// <summary>One plotted line: a name for the legend and the points to draw.</summary>
/// <param name="Name">Legend text.</param>
/// <param name="X">Horizontal values.</param>
/// <param name="Y">Vertical values.</param>
/// <param name="UseRightAxis">
/// Whether the series is measured against a second vertical axis on the right. The
/// in-cylinder chart needs it: pressure runs to about 70 bar while temperature runs to
/// 4200 K, and on one axis the pressure trace would be flat against the bottom.
/// </param>
public sealed record ChartSeries(string Name, double[] X, double[] Y, bool UseRightAxis = false)
{
    /// <summary>How many points the series holds.</summary>
    public int Count => X.Length;
}

/// <summary>
/// A labelled vertical line, for the valve events and dead centres the original draws
/// across its gas-flow chart.
/// </summary>
/// <param name="Label">Short caption, such as <c>IVO</c> or <c>TDC</c>.</param>
/// <param name="X">Where the line sits on the horizontal axis.</param>
/// <param name="AtBottom">Whether the caption sits at the foot of the plot rather than the top.</param>
public sealed record ChartMarker(string Label, double X, bool AtBottom = false);

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
/// <param name="RightYAxisLabel">
/// Caption for the second vertical axis, where any series has
/// <see cref="ChartSeries.UseRightAxis"/> set.
/// </param>
/// <param name="Markers">Labelled vertical lines drawn across the plot.</param>
public sealed record ChartDefinition(
    string Title,
    string XAxisLabel,
    string YAxisLabel,
    IReadOnlyList<ChartSeries> Series,
    double? YMinimum = null,
    double? YMaximum = null,
    string? RightYAxisLabel = null,
    IReadOnlyList<ChartMarker>? Markers = null);
