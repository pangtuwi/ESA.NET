using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using App.Core.Model;
using App.Validation;

// The thesis validation suite: re-runs every dynamometer correlation of thesis sections
// 6.4-6.6 and writes a report. See validation/README.md.
//
//   dotnet run --project tools/App.Validation -c Release -- [options]
//
//   --figure 6.12        only this figure; repeat or comma-separate for more
//   --mode corrected     physics: corrected (the default) or legacy
//   --set B4=1           override one correction, as in ESA.ini; repeatable
//   --previous <dir>     an earlier report folder, drawn and compared as the previous run
//   --out <dir>          where to write; default validation-reports/<date>_<time>_<mode>

var options = Options.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(Options.Usage);
    return 2;
}

var root = FindRoot(Directory.GetCurrentDirectory()) ?? FindRoot(AppContext.BaseDirectory);
if (root is null)
{
    Console.Error.WriteLine("Run from inside the repository: validation/suite.json was not found above here.");
    return 2;
}

var validation = Path.Combine(root, "validation");
var suite = Suite.Load(Path.Combine(validation, "suite.json"));
var figures = suite.Figures.Where(f => options.Figures.Count == 0 || options.Figures.Contains(f.Id)).ToList();
if (figures.Count == 0)
{
    Console.Error.WriteLine($"No figure {string.Join(", ", options.Figures)} in suite.json.");
    return 2;
}

var physics = new PhysicsCorrections { Mode = options.Legacy ? PhysicsMode.Legacy : PhysicsMode.Corrected };
foreach (var (entry, on) in options.Overrides)
{
    physics.Overrides[entry] = on;
}

var started = DateTime.Now;
var output = options.Output ?? Path.Combine(
    root, "validation-reports",
    started.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + (options.Legacy ? "_legacy" : "_corrected"));
Directory.CreateDirectory(output);

Console.WriteLine($"Physics: {physics.Describe()}");
Console.WriteLine($"Figures: {string.Join(", ", figures.Select(f => f.Id))}");

var grids = figures.SelectMany(f => f.Series)
    .SelectMany(s => s.Baseline is null ? new[] { (s.Case, s.Grid) } : [(s.Case, s.Grid), (s.Case, s.Baseline)])
    .Distinct().ToList();
var traced = figures.SelectMany(f => f.Series).Where(s => s.Output == "inlet_pressure").Select(s => (s.Case, s.Grid)).ToHashSet();

var clock = Stopwatch.StartNew();
var runs = new CaseRunner(Path.Combine(validation, "cases"), physics, new SyncLog()).Run(grids, traced);

var (previousInfo, previousPoints) = options.Previous is null ? (null, new()) : ResultsFile.Read(options.Previous);
var results = figures.Select(f => Evaluation.Evaluate(
    f,
    ThesisData.Load(Path.Combine(root, f.Data)),
    runs,
    (id, series) => options.Previous is null ? null
        : previousPoints.TryGetValue((id, series), out var p) ? p.OrderBy(q => q.X).ToList() : null)).ToList();

var info = new RunInfo(
    Git(root, "rev-parse --short HEAD") ?? "unknown",
    !string.IsNullOrWhiteSpace(Git(root, "status --porcelain --untracked-files=no")),
    physics.Describe(),
    started.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
    clock.Elapsed.TotalMinutes,
    [.. figures.Select(f => f.Id)]);

ResultsFile.Write(output, info, results);
var readme = Path.Combine(validation, "README.md");
File.WriteAllText(
    Path.Combine(output, "report.html"),
    HtmlReport.Build(info, previousInfo, results, HtmlReport.Assumptions(readme), Cases(readme)));

Console.WriteLine();
Console.WriteLine("Figure  Series                    Status        vs measured      2002 model vs measured");
foreach (var r in results)
{
    foreach (var s in r.Series)
    {
        string C(Comparison? c)
        {
            if (c is null) return "–";
            var digits = s.Unit == "bar" && !c.Relative ? 3 : 1;
            var rms = c.Rms.ToString("0." + new string('0', digits), System.Globalization.CultureInfo.InvariantCulture);
            return $"{rms}{(c.Relative ? "%" : " " + s.Unit)} ({HtmlReport.Signed(c.Bias, digits)})";
        }
        Console.WriteLine(FormattableString.Invariant(
            $"{r.Spec.Id,-7} {s.Spec.Name,-25} {s.Status,-13} {C(s.VersusMeasured),-16} {C(s.ThesisVersusMeasured)}"));
    }
}

Console.WriteLine();
Console.WriteLine($"Report: {Path.Combine(output, "report.html")} ({clock.Elapsed.TotalMinutes:0.0} min)");
return 0;

static string? FindRoot(string start)
{
    for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
    {
        if (File.Exists(Path.Combine(d.FullName, "validation", "suite.json")))
        {
            return d.FullName;
        }
    }

    return null;
}

static string? Git(string root, string arguments)
{
    try
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var git = Process.Start(start)!;
        var text = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? text.Trim() : null;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return null;
    }
}

// The case table of validation/README.md: case name to its engine description.
static Dictionary<string, string> Cases(string readme) =>
    Regex.Matches(File.ReadAllText(readme), @"^\| `(\w+)` \| ([^|]+) \|", RegexOptions.Multiline)
        .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());

/// <summary>Writes progress lines as they come, from whichever thread.</summary>
internal sealed class SyncLog : IProgress<string>
{
    private readonly Lock _lock = new();

    public void Report(string value)
    {
        lock (_lock)
        {
            Console.WriteLine(value);
        }
    }
}

/// <summary>The command line.</summary>
internal sealed record Options(
    HashSet<string> Figures, bool Legacy, List<(string, bool)> Overrides, string? Previous, string? Output)
{
    public const string Usage =
        "usage: App.Validation [--figure 6.12[,6.14]] [--mode corrected|legacy] [--set B4=1] [--previous <report dir>] [--out <dir>]";

    public static Options? Parse(string[] args)
    {
        var o = new Options([], false, [], null, null);

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");

            try
            {
                switch (args[i])
                {
                    case "--figure":
                        foreach (var f in Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            o.Figures.Add(f);
                        }

                        break;
                    case "--mode":
                        var mode = Next().ToUpperInvariant();
                        if (mode is not ("LEGACY" or "CORRECTED"))
                        {
                            return null;
                        }

                        o = o with { Legacy = mode == "LEGACY" };
                        break;
                    case "--set":
                        var parts = Next().Split('=');
                        o.Overrides.Add((parts[0], parts.Length < 2 || parts[1] is "1" or "true" or "on"));
                        break;
                    case "--previous":
                        o = o with { Previous = Path.GetFullPath(Next()) };
                        break;
                    case "--out":
                        o = o with { Output = Path.GetFullPath(Next()) };
                        break;
                    default:
                        return null;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return o;
    }
}
