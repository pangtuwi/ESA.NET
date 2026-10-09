using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace App.Validation;

/// <summary>The self-contained HTML report: one file, no external resources.</summary>
internal static partial class HtmlReport
{
    private static string H(string s) => WebUtility.HtmlEncode(s);

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    public static string Build(
        RunInfo info,
        RunInfo? previous,
        IReadOnlyList<FigureResult> figures,
        IReadOnlyDictionary<string, string> assumptions,
        IReadOnlyDictionary<string, string> cases)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>Thesis Validation Report</title><style>").Append(Css).Append("</style></head><body><main>");

        html.Append("<h1>ESA.NET against the dynamometer</h1>");
        html.Append(Inv($"<p class=\"meta\">Commit <code>{H(info.Commit)}</code>{(info.Dirty ? " <span class=\"warn\">with uncommitted changes</span>" : "")} · {H(info.Physics)} · run {H(info.Started)} in {info.Minutes:0.0} min"));
        if (previous is not null)
        {
            html.Append(Inv($" · compared with the run of {H(previous.Started)} on <code>{H(previous.Commit)}</code>, {H(previous.Physics)}"));
        }

        html.Append("</p>");
        html.Append("<p class=\"lede\">Every correlation with the test bed in the thesis, sections 6.4 to 6.6, re-run on the current physics. ");
        html.Append("Each chart is drawn after the thesis figure of the same number. Colour marks the series; the mark shows where a curve came from.</p>");
        html.Append(Key());

        html.Append("<h2 id=\"summary\">Summary</h2>");
        html.Append(Summary(figures, previous is not null));
        html.Append("<p class=\"note\">Differences are per cent of the reference for torque and power, and in the figure's own units for the torque change of Figure 6.11 and the pressure trace of Figure 6.8. ");
        html.Append("rms is the root mean square of the differences at the reference's own points, the simulated curve interpolated between its runs; bias is their mean. ");
        html.Append("“2002 model” is the thesis's own simulation, the bar the port is measured against.</p>");

        foreach (var figure in figures)
        {
            html.Append(Figure(figure, assumptions, cases, previous is not null));
        }

        html.Append("<footer><p>Written by <code>dotnet run --project tools/App.Validation</code>. Inputs, cases and assumptions: <code>validation/README.md</code>. ");
        html.Append("Points: <code>results.csv</code> and <code>failures.csv</code> beside this file.</p></footer>");
        html.Append("</main><div id=\"tip\" role=\"tooltip\"></div><script>").Append(Script).Append("</script></body></html>");
        return html.ToString();
    }

    private static string Key() =>
        "<p class=\"key\" aria-label=\"How to read the charts\">" +
        "<span><svg width=\"22\" height=\"12\"><circle cx=\"11\" cy=\"6\" r=\"4.5\" fill=\"var(--text-secondary)\"/></svg>measured on the dynamometer</span>" +
        "<span><svg width=\"30\" height=\"12\"><line x1=\"1\" x2=\"29\" y1=\"6\" y2=\"6\" stroke=\"var(--text-secondary)\" stroke-width=\"2\" stroke-dasharray=\"7 4\"/><rect x=\"11.5\" y=\"2.5\" width=\"7\" height=\"7\" fill=\"var(--surface-1)\" stroke=\"var(--text-secondary)\" stroke-width=\"1.6\"/></svg>the thesis's 2002 model</span>" +
        "<span><svg width=\"30\" height=\"12\"><line x1=\"1\" x2=\"29\" y1=\"6\" y2=\"6\" stroke=\"var(--text-secondary)\" stroke-width=\"2.5\"/><circle cx=\"15\" cy=\"6\" r=\"3.5\" fill=\"var(--text-secondary)\"/></svg>ESA.NET now</span>" +
        "<span><svg width=\"30\" height=\"12\"><line x1=\"1\" x2=\"29\" y1=\"6\" y2=\"6\" stroke=\"var(--text-secondary)\" stroke-width=\"1.6\" stroke-dasharray=\"1.5 3.5\"/></svg>previous run</span>" +
        "<span><svg width=\"14\" height=\"12\"><path d=\"M3,2 L11,10 M3,10 L11,2\" stroke=\"var(--critical)\" stroke-width=\"2\"/></svg>a run that did not finish</span></p>";

    private static string Number(Comparison? c, string unit)
    {
        if (c is null)
        {
            return "<td class=\"num muted\">–</td>";
        }

        var u = c.Relative ? "%" : " " + unit;
        var digits = unit == "bar" && !c.Relative ? 3 : 1;
        return Inv($"<td class=\"num\">{Math.Round(c.Rms, digits).ToString(digits == 3 ? "0.000" : "0.0", CultureInfo.InvariantCulture)}{u} <span class=\"muted\">({Signed(c.Bias, digits)})</span></td>");
    }

    /// <summary>A bias with its sign, and no sign on a value that rounds to zero.</summary>
    public static string Signed(double v, int digits = 1)
    {
        var zeros = new string('0', digits);
        var r = Math.Round(v, digits);
        return r == 0 ? "0." + zeros : r.ToString($"+0.{zeros};-0.{zeros}", CultureInfo.InvariantCulture);
    }

    private static string Status(SeriesResult s) => s.Status switch
    {
        "ran" => "<span class=\"status good\">✓ ran</span>",
        "partly ran" => Inv($"<span class=\"status serious\">◐ {s.Rows - s.Failures.Count} of {s.Rows} ran</span>"),
        _ => "<span class=\"status critical\">✕ did not run</span>",
    };

    private static string Summary(IReadOnlyList<FigureResult> figures, bool withPrevious)
    {
        var t = new StringBuilder("<div class=\"scroll\"><table class=\"summary\"><thead><tr><th>Series</th><th>Status</th>");
        t.Append("<th class=\"num\">ESA.NET vs measured<br><span class=\"muted\">rms (bias)</span></th>");
        t.Append("<th class=\"num\">2002 model vs measured</th><th class=\"num\">ESA.NET vs 2002 model</th>");
        if (withPrevious)
        {
            t.Append("<th class=\"num\">Previous vs measured</th><th class=\"num\">Change in rms</th>");
        }

        t.Append("</tr></thead><tbody>");

        var columns = withPrevious ? 7 : 5;
        foreach (var f in figures)
        {
            t.Append(Inv($"<tr class=\"group\"><th colspan=\"{columns}\"><a href=\"#f{f.Spec.Id.Replace('.', '-')}\">Figure {H(f.Spec.Id)}</a> <span class=\"muted\">{H(f.Spec.Title)}</span></th></tr>"));
            foreach (var s in f.Series)
            {
                t.Append(Inv($"<tr><td><span class=\"swatch\" style=\"background:var(--series-{s.Slot})\"></span>{H(s.Spec.Name)}</td><td>{Status(s)}</td>"));
                t.Append(Number(s.VersusMeasured, s.Unit)).Append(Number(s.ThesisVersusMeasured, s.Unit)).Append(Number(s.VersusThesis, s.Unit));

                if (withPrevious)
                {
                    t.Append(Number(s.PreviousVersusMeasured, s.Unit));
                    if (s.VersusMeasured is { } now && s.PreviousVersusMeasured is { } before)
                    {
                        var d = now.Rms - before.Rms;
                        var digits = s.Unit == "bar" && !now.Relative ? 3 : 1;
                        var cls = Math.Abs(d) < 0.5 * Math.Pow(10, -digits) ? "muted" : d < 0 ? "better" : "worse";
                        t.Append(Inv($"<td class=\"num {cls}\">{Signed(d, digits)}{(now.Relative ? " pt" : " " + s.Unit)}</td>"));
                    }
                    else
                    {
                        t.Append("<td class=\"num muted\">–</td>");
                    }
                }

                t.Append("</tr>");
            }
        }

        return t.Append("</tbody></table></div>").ToString();
    }

    private static string Figure(
        FigureResult f,
        IReadOnlyDictionary<string, string> assumptions,
        IReadOnlyDictionary<string, string> cases,
        bool withPrevious)
    {
        var spec = f.Spec;
        var html = new StringBuilder();
        html.Append(Inv($"<section id=\"f{spec.Id.Replace('.', '-')}\"><h2>Figure {H(spec.Id)} · {H(spec.Title)}</h2>"));

        var caseNames = f.Series.Select(s => s.Spec.Case).Distinct().Select(c => cases.TryGetValue(c, out var d) ? $"<code>{H(c)}</code> ({H(d)})" : $"<code>{H(c)}</code>");
        html.Append(Inv($"<p class=\"meta\">Thesis section {H(spec.Section)}, page {spec.Page} · {string.Join(", ", caseNames)}</p>"));

        if (spec.CorrelateFrom is { } from)
        {
            html.Append(Inv($"<p class=\"note\">Compared from {from:0} rpm upward, where the thesis says its correlation holds.</p>"));
        }

        // The series legend, so identity is never colour alone.
        html.Append("<p class=\"legend\">");
        foreach (var s in f.Series.GroupBy(s => s.Slot).Select(g => g.First()))
        {
            var name = f.Series.Any(x => x.Spec.Output == "power") ? "Torque and power" : s.Spec.Name;
            html.Append(Inv($"<span><span class=\"swatch\" style=\"background:var(--series-{s.Slot})\"></span>{H(name)}</span>"));
        }

        html.Append("</p><div class=\"charts\">");

        var axes = spec.Y2 is null ? new[] { ("y", spec.Y) } : new[] { ("y", spec.Y), ("y2", spec.Y2) };
        foreach (var (axis, ySpec) in axes)
        {
            var series = f.Series.Where(s => s.Spec.Axis == axis).ToList();
            if (series.Count == 0)
            {
                continue;
            }

            var xUnit = spec.X.Variable switch { "spark" => "°BTDC", "crank" => "°", _ => "rpm" };
            var chart = new SvgChart(spec.X, Fit(ySpec, series), xUnit, series[0].Unit);

            foreach (var s in series)
            {
                chart.Add(s.Slot, s.Spec.Name, Source.Measured, s.Measured);
            }

            foreach (var s in series)
            {
                chart.Add(s.Slot, s.Spec.Name, Source.Thesis, s.Thesis);
                if (s.Previous is not null)
                {
                    chart.Add(s.Slot, s.Spec.Name, Source.Previous, s.Previous);
                }

                chart.Add(s.Slot, s.Spec.Name, Source.Esa, s.Esa);
                chart.AddFailures(s.Spec.Name, s.Failures);
            }

            html.Append("<figure>").Append(chart.Render($"Figure {spec.Id}: {ySpec.Label} against {spec.X.Label}")).Append("</figure>");
        }

        html.Append("</div>");
        html.Append(Comparisons(f, withPrevious));
        html.Append(Failures(f));
        html.Append(DataTable(f));

        if (spec.Assumptions.Count > 0)
        {
            html.Append("<details><summary>Assumptions behind this figure</summary><dl class=\"assumptions\">");
            foreach (var id in spec.Assumptions)
            {
                html.Append(Inv($"<dt>{H(id)}</dt><dd>{H(assumptions.TryGetValue(id, out var text) ? text : "(not in validation/README.md)")}</dd>"));
            }

            html.Append("</dl></details>");
        }

        return html.Append("</section>").ToString();
    }

    /// <summary>
    /// The thesis figure's own y range, widened when a curve leaves it, so nothing the
    /// port computes is clipped off the chart.
    /// </summary>
    private static AxisSpec Fit(AxisSpec y, IEnumerable<SeriesResult> series)
    {
        var values = series.SelectMany(s => s.Esa.Concat(s.Measured).Concat(s.Thesis).Concat(s.Previous ?? []))
            .Select(p => p.Y).ToList();
        if (values.Count == 0)
        {
            return y;
        }

        var step = (y.Max - y.Min) / 8;
        var min = Math.Min(y.Min, Math.Floor(values.Min() / step) * step);
        var max = Math.Max(y.Max, Math.Ceiling(values.Max() / step) * step);
        return y with { Min = min, Max = max };
    }

    private static string Comparisons(FigureResult f, bool withPrevious)
    {
        var t = new StringBuilder("<div class=\"scroll\"><table><thead><tr><th>Series</th><th>Status</th><th class=\"num\">ESA.NET vs measured</th><th class=\"num\">2002 model vs measured</th><th class=\"num\">ESA.NET vs 2002 model</th>");
        if (withPrevious)
        {
            t.Append("<th class=\"num\">Previous vs measured</th>");
        }

        if (f.IsTimingLoop)
        {
            t.Append("<th class=\"num\">Peak: ESA.NET</th><th class=\"num\">Peak: 2002 model</th><th class=\"num\">Peak: measured</th>");
        }

        t.Append("</tr></thead><tbody>");
        foreach (var s in f.Series)
        {
            t.Append(Inv($"<tr><td><span class=\"swatch\" style=\"background:var(--series-{s.Slot})\"></span>{H(s.Spec.Name)}</td><td>{Status(s)}</td>"));
            t.Append(Number(s.VersusMeasured, s.Unit)).Append(Number(s.ThesisVersusMeasured, s.Unit)).Append(Number(s.VersusThesis, s.Unit));
            if (withPrevious)
            {
                t.Append(Number(s.PreviousVersusMeasured, s.Unit));
            }

            if (f.IsTimingLoop)
            {
                foreach (var points in new[] { s.Esa, s.Thesis, s.Measured })
                {
                    t.Append(Comparison.Peak(points) is { } p
                        ? Inv($"<td class=\"num\">{p.Y:0.0} Nm at {p.X:0.#}°</td>")
                        : "<td class=\"num muted\">–</td>");
                }
            }

            t.Append("</tr>");
        }

        return t.Append("</tbody></table></div>").ToString();
    }

    private static string Failures(FigureResult f)
    {
        var failed = f.Series.SelectMany(s => s.Failures.Select(x => (s.Spec.Name, x))).ToList();
        if (failed.Count == 0)
        {
            return "";
        }

        var html = new StringBuilder("<div class=\"failures\"><p><span class=\"status critical\">✕</span> Runs that did not finish:</p><ul>");
        foreach (var g in failed.GroupBy(x => (x.Name, x.x.Message)))
        {
            var where = string.Join(", ", g.Select(x => x.x.X.ToString("0.#", CultureInfo.InvariantCulture)));
            html.Append(Inv($"<li><b>{H(g.Key.Name)}</b> at {H(where)}: <code>{H(g.Key.Message)}</code></li>"));
        }

        return html.Append("</ul></div>").ToString();
    }

    private static string DataTable(FigureResult f)
    {
        var total = f.Series.Sum(s => s.Measured.Count + s.Thesis.Count + s.Esa.Count);
        if (total > 400)
        {
            return Inv($"<p class=\"note\">{total} points: too many to tabulate here. They are in <code>results.csv</code> and <code>data/thesis/figures/</code>.</p>");
        }

        var t = new StringBuilder("<details><summary>Data table</summary><div class=\"scroll\"><table class=\"data\"><thead><tr><th>Series</th><th>Source</th><th class=\"num\">x</th><th class=\"num\">y</th></tr></thead><tbody>");
        foreach (var s in f.Series)
        {
            foreach (var (source, points) in new[] { (Source.Measured, s.Measured), (Source.Thesis, s.Thesis), (Source.Esa, s.Esa) })
            {
                foreach (var p in points)
                {
                    t.Append(Inv($"<tr><td>{H(s.Spec.Name)}</td><td>{SvgChart.Name(source)}</td><td class=\"num\">{p.X:0.#}</td><td class=\"num\">{p.Y:0.00}</td></tr>"));
                }
            }
        }

        return t.Append("</tbody></table></div></details>").ToString();
    }

    /// <summary>The assumption register of <c>validation/README.md</c>: id to its text, markdown stripped.</summary>
    public static Dictionary<string, string> Assumptions(string readme)
    {
        var result = new Dictionary<string, string>();
        foreach (Match m in AssumptionRow().Matches(File.ReadAllText(readme)))
        {
            result[m.Groups[1].Value] = Markdown().Replace(m.Groups[2].Value, "").Trim();
        }

        return result;
    }

    [GeneratedRegex(@"^\| ([NVC]\d+) \| (.+?) \|", RegexOptions.Multiline)]
    private static partial Regex AssumptionRow();

    [GeneratedRegex(@"[`*]")]
    private static partial Regex Markdown();

    private const string Css = """
        :root{color-scheme:light;--surface-0:#f4f3ef;--surface-1:#fcfcfb;--text-primary:#0b0b0b;--text-secondary:#52514e;--text-muted:#77766f;--grid:#e3e2dc;--axis:#a3a29b;
          --series-1:#2a78d6;--series-2:#eb6834;--series-3:#1baf7a;--good:#0ca30c;--serious:#ec835a;--critical:#d03b3b;--better:#006300;--worse:#b42318;}
        @media (prefers-color-scheme:dark){:root:where(:not([data-theme="light"])){color-scheme:dark;--surface-0:#121211;--surface-1:#1a1a19;--text-primary:#ffffff;--text-secondary:#c3c2b7;--text-muted:#94938a;--grid:#2c2c2a;--axis:#5c5b55;
          --series-1:#3987e5;--series-2:#d95926;--series-3:#199e70;--better:#4cc24c;--worse:#f07a6a;}}
        :root[data-theme="dark"]{color-scheme:dark;--surface-0:#121211;--surface-1:#1a1a19;--text-primary:#ffffff;--text-secondary:#c3c2b7;--text-muted:#94938a;--grid:#2c2c2a;--axis:#5c5b55;
          --series-1:#3987e5;--series-2:#d95926;--series-3:#199e70;--better:#4cc24c;--worse:#f07a6a;}
        *{box-sizing:border-box}
        body{margin:0;background:var(--surface-0);color:var(--text-primary);font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif}
        main{max-width:1180px;margin:0 auto;padding:24px 16px 48px}
        h1{font-size:26px;margin:0 0 6px}
        h2{font-size:19px;margin:0 0 4px}
        section{background:var(--surface-1);border-radius:10px;padding:20px;margin:22px 0}
        .meta{color:var(--text-secondary);margin:0 0 10px;font-size:13.5px}
        .lede{max-width:820px}
        .note{color:var(--text-secondary);font-size:13.5px;max-width:900px}
        .muted{color:var(--text-muted)} .small{font-size:12.5px}
        .warn{color:var(--critical)}
        code{font:12.5px ui-monospace,SFMono-Regular,Menlo,monospace;overflow-wrap:anywhere}
        .key,.legend{display:flex;flex-wrap:wrap;gap:6px 20px;color:var(--text-secondary);font-size:13.5px;margin:8px 0}
        .key span,.legend span{display:inline-flex;align-items:center;gap:7px}
        .swatch{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:6px;vertical-align:baseline}
        .charts{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,440px),720px));gap:16px}
        figure{margin:0}
        svg.chart{width:100%;height:auto;display:block}
        svg .grid{stroke:var(--grid);stroke-width:1}
        svg .axis{stroke:var(--axis);stroke-width:1}
        svg .tick{fill:var(--text-muted);font-size:11.5px}
        svg .label{fill:var(--text-secondary);font-size:12.5px}
        svg .hit{fill:transparent;cursor:crosshair}
        .scroll{overflow-x:auto;margin:10px 0}
        table{border-collapse:collapse;font-size:13.5px;width:100%}
        th,td{text-align:left;padding:6px 10px;border-bottom:1px solid var(--grid);vertical-align:top}
        th{color:var(--text-secondary);font-weight:600}
        .num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
        table.summary{background:var(--surface-1);border-radius:10px}
        tr.group th{padding-top:14px;color:var(--text-primary);font-weight:600;background:var(--surface-2,transparent)} tr.group .muted{font-weight:400} table.summary td:first-child{white-space:nowrap}
        .status{white-space:nowrap;font-weight:600}
        .status.good{color:var(--good)} .status.serious{color:var(--serious)} .status.critical{color:var(--critical)}
        .better{color:var(--better)} .worse{color:var(--worse)}
        .failures ul{margin:4px 0 8px;padding-left:22px;font-size:13.5px}
        details{margin:8px 0} summary{cursor:pointer;color:var(--text-secondary)}
        dl.assumptions{display:grid;grid-template-columns:3em 1fr;gap:6px 12px;font-size:13.5px}
        dl.assumptions dt{font-weight:600} dl.assumptions dd{margin:0}
        a{color:var(--series-1)}
        footer{color:var(--text-muted);font-size:13px}
        #tip{position:fixed;pointer-events:none;background:var(--surface-1);color:var(--text-primary);border:1px solid var(--grid);border-radius:6px;padding:6px 9px;font-size:12.5px;white-space:pre-line;box-shadow:0 4px 14px rgba(0,0,0,.18);display:none;max-width:420px}
        """;

    private const string Script = """
        const tip=document.getElementById('tip');
        function show(e){const t=e.target.closest('[data-tip]');if(!t){tip.style.display='none';return;}
          tip.textContent=t.dataset.tip;tip.style.display='block';
          const x=Math.min(e.clientX+14,window.innerWidth-tip.offsetWidth-8),y=Math.max(e.clientY-tip.offsetHeight-12,8);
          tip.style.left=x+'px';tip.style.top=y+'px';}
        document.addEventListener('pointermove',show);
        document.addEventListener('pointerdown',show);
        """;
}
