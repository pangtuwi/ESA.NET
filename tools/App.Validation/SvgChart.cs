using System.Globalization;
using System.Net;
using System.Text;

namespace App.Validation;

/// <summary>Which of the four sources a line is: the colour is the series, this is the mark.</summary>
internal enum Source
{
    Measured,
    Thesis,
    Esa,
    Previous,
}

/// <summary>
/// A line chart as inline SVG. Colours are CSS custom properties defined by the report
/// (<c>--series-1</c> and so on), so the chart follows the page's light or dark theme;
/// every plotted point carries a <c>data-tip</c> the report's script shows on hover.
/// </summary>
/// <remarks>
/// One y axis per chart, always: a figure the thesis drew with torque and power on twin
/// axes is drawn as two charts.
/// </remarks>
internal sealed class SvgChart(AxisSpec x, AxisSpec y, string xUnit, string yUnit)
{
    private const double Width = 680;
    private const double Height = 360;
    private const double Left = 62;
    private const double Right = 18;
    private const double Top = 14;
    private const double Bottom = 46;

    /// <summary>Above this many points a series is drawn as a line alone, without markers or hover targets.</summary>
    private const int Dense = 80;

    private readonly StringBuilder _lines = new();
    private readonly StringBuilder _marks = new();
    private readonly StringBuilder _targets = new();

    private static string N(double v) => (Math.Round(v, 2) + 0.0).ToString("0.##", CultureInfo.InvariantCulture);

    private double Px(double v) => Left + ((v - x.Min) / (x.Max - x.Min) * (Width - Left - Right));

    private double Py(double v) => Height - Bottom - ((v - y.Min) / (y.Max - y.Min) * (Height - Top - Bottom));

    private string Tip(string label, Source source, Point p) =>
        WebUtility.HtmlEncode(FormattableString.Invariant(
            $"{label} · {Name(source)}\n{p.X:0.#} {xUnit}: {p.Y:0.0##} {yUnit}"));

    public static string Name(Source source) => source switch
    {
        Source.Measured => "measured",
        Source.Thesis => "2002 model",
        Source.Esa => "ESA.NET now",
        _ => "previous run",
    };

    public void Add(int slot, string label, Source source, IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        var dense = points.Count > Dense;
        var colour = source == Source.Measured && dense ? "var(--text-secondary)" : $"var(--series-{slot})";
        // A simulated curve breaks at a gap rather than bridging a run that failed.
        var gap = source is Source.Esa or Source.Previous ? Comparison.GapLimit(points) : double.PositiveInfinity;
        var pieces = new List<List<Point>> { new() };
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0 && points[i].X - points[i - 1].X > gap)
            {
                pieces.Add([]);
            }

            pieces[^1].Add(points[i]);
        }

        var (width, dash, opacity) = source switch
        {
            Source.Esa => (2.5, "", 1.0),
            Source.Thesis => (2.0, "7 4", 1.0),
            Source.Previous => (1.6, "1.5 3.5", 0.75),
            _ => (1.5, "", dense ? 0.8 : 0.0),
        };

        foreach (var piece in pieces.Where(p => opacity > 0 && p.Count > 1))
        {
            var path = string.Join(' ', piece.Select(p => N(Px(p.X)) + "," + N(Py(p.Y))));
            _lines.Append(CultureInfo.InvariantCulture,
                $"<polyline points=\"{path}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\" opacity=\"{N(opacity)}\"");
            _lines.Append(dash.Length > 0 ? $" stroke-dasharray=\"{dash}\"/>" : "/>");
        }

        if (dense)
        {
            return;
        }

        foreach (var p in points)
        {
            var (cx, cy) = (N(Px(p.X)), N(Py(p.Y)));

            switch (source)
            {
                case Source.Measured:
                    _marks.Append(CultureInfo.InvariantCulture,
                        $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"4.5\" fill=\"{colour}\" stroke=\"var(--surface-1)\" stroke-width=\"2\"/>");
                    break;
                case Source.Thesis:
                    _marks.Append(CultureInfo.InvariantCulture,
                        $"<rect x=\"{N(Px(p.X) - 3.5)}\" y=\"{N(Py(p.Y) - 3.5)}\" width=\"7\" height=\"7\" fill=\"var(--surface-1)\" stroke=\"{colour}\" stroke-width=\"1.6\"/>");
                    break;
                case Source.Esa:
                    _marks.Append(CultureInfo.InvariantCulture,
                        $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"3.5\" fill=\"{colour}\" stroke=\"var(--surface-1)\" stroke-width=\"1.5\"/>");
                    break;
                default:
                    break;
            }

            _targets.Append(CultureInfo.InvariantCulture,
                $"<circle class=\"hit\" cx=\"{cx}\" cy=\"{cy}\" r=\"9\" data-tip=\"{Tip(label, source, p)}\"/>");
        }
    }

    /// <summary>Marks a run that failed with a cross on the x axis, its message on hover.</summary>
    public void AddFailures(string label, IEnumerable<Failure> failures)
    {
        foreach (var f in failures.Where(f => !double.IsNaN(f.X)))
        {
            var (cx, cy) = (Px(f.X), Height - Bottom - 8);
            _marks.Append(CultureInfo.InvariantCulture,
                $"<path d=\"M{N(cx - 4)},{N(cy - 4)} L{N(cx + 4)},{N(cy + 4)} M{N(cx - 4)},{N(cy + 4)} L{N(cx + 4)},{N(cy - 4)}\" stroke=\"var(--critical)\" stroke-width=\"2\" stroke-linecap=\"round\"/>");
            _targets.Append(CultureInfo.InvariantCulture,
                $"<circle class=\"hit\" cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"9\" data-tip=\"{WebUtility.HtmlEncode(FormattableString.Invariant($"{label} · did not run\n{f.X:0.#} {xUnit}: {f.Message}"))}\"/>");
        }
    }

    public string Render(string title)
    {
        var svg = new StringBuilder();
        var id = "c" + Guid.NewGuid().ToString("N")[..8];

        svg.Append(CultureInfo.InvariantCulture,
            $"<svg class=\"chart\" viewBox=\"0 0 {N(Width)} {N(Height)}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(title)}\">");
        svg.Append(CultureInfo.InvariantCulture,
            $"<defs><clipPath id=\"{id}\"><rect x=\"{N(Left)}\" y=\"{N(Top)}\" width=\"{N(Width - Left - Right)}\" height=\"{N(Height - Top - Bottom)}\"/></clipPath></defs>");

        foreach (var t in Ticks(y.Min, y.Max))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"{N(Left)}\" x2=\"{N(Width - Right)}\" y1=\"{N(Py(t))}\" y2=\"{N(Py(t))}\" class=\"grid\"/>");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{N(Left - 8)}\" y=\"{N(Py(t) + 4)}\" text-anchor=\"end\" class=\"tick\">{N(t)}</text>");
        }

        foreach (var t in Ticks(x.Min, x.Max))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{N(Px(t))}\" y=\"{N(Height - Bottom + 18)}\" text-anchor=\"middle\" class=\"tick\">{N(t)}</text>");
        }

        svg.Append(CultureInfo.InvariantCulture,
            $"<line x1=\"{N(Left)}\" x2=\"{N(Width - Right)}\" y1=\"{N(Height - Bottom)}\" y2=\"{N(Height - Bottom)}\" class=\"axis\"/>");
        svg.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{N((Left + Width - Right) / 2)}\" y=\"{N(Height - 8)}\" text-anchor=\"middle\" class=\"label\">{WebUtility.HtmlEncode(x.Label)}</text>");
        svg.Append(CultureInfo.InvariantCulture,
            $"<text transform=\"translate(14,{N((Top + Height - Bottom) / 2)}) rotate(-90)\" text-anchor=\"middle\" class=\"label\">{WebUtility.HtmlEncode(y.Label)}</text>");
        svg.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#{id})\">").Append(_lines).Append(_marks).Append("</g>");
        svg.Append(_targets).Append("</svg>");
        return svg.ToString();
    }

    /// <summary>Round tick values covering [min, max], about five to ten of them.</summary>
    private static IEnumerable<double> Ticks(double min, double max)
    {
        var raw = (max - min) / 8;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * magnitude).First(s => s >= raw);
        var first = Math.Ceiling((min - 1e-9) / step) * step;

        for (var v = first; v <= max + 1e-9; v += step)
        {
            yield return Math.Round(v, 10);
        }
    }
}
