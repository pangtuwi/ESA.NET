using System.Globalization;
using System.Text;
using System.Text.Json;

namespace App.Validation;

/// <summary>What a report was run on: written beside it and read back by <c>--previous</c>.</summary>
internal sealed record RunInfo(
    string Commit, bool Dirty, string Physics, string Started, double Minutes, IReadOnlyList<string> Figures);

/// <summary>
/// <c>results.csv</c> (every simulated point), <c>failures.csv</c> and <c>run.json</c>:
/// the machine-readable half of a report, so two runs can be diffed and a later report
/// can draw an earlier one as its previous run.
/// </summary>
internal static class ResultsFile
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void Write(string directory, RunInfo info, IReadOnlyList<FigureResult> figures)
    {
        var results = new StringBuilder("figure,series,x,y\n");
        var failures = new StringBuilder("figure,series,x,failure\n");

        foreach (var figure in figures)
        {
            foreach (var s in figure.Series)
            {
                foreach (var p in s.Esa)
                {
                    results.Append(CultureInfo.InvariantCulture,
                        $"{figure.Spec.Id},{Csv(s.Spec.Name)},{p.X:R},{p.Y:R}\n");
                }

                foreach (var f in s.Failures)
                {
                    failures.Append(CultureInfo.InvariantCulture,
                        $"{figure.Spec.Id},{Csv(s.Spec.Name)},{f.X:R},{Csv(f.Message)}\n");
                }
            }
        }

        File.WriteAllText(Path.Combine(directory, "results.csv"), results.ToString());
        File.WriteAllText(Path.Combine(directory, "failures.csv"), failures.ToString());
        File.WriteAllText(Path.Combine(directory, "run.json"), JsonSerializer.Serialize(info, Json));
    }

    /// <summary>An earlier report's points, by figure and series name.</summary>
    public static (RunInfo? Info, Dictionary<(string, string), List<Point>> Points) Read(string directory)
    {
        var points = new Dictionary<(string, string), List<Point>>();

        foreach (var line in File.ReadLines(Path.Combine(directory, "results.csv")).Skip(1))
        {
            var f = Split(line);
            var key = (f[0], f[1]);

            if (!points.TryGetValue(key, out var list))
            {
                points[key] = list = [];
            }

            list.Add(new Point(
                double.Parse(f[2], CultureInfo.InvariantCulture), double.Parse(f[3], CultureInfo.InvariantCulture)));
        }

        var infoPath = Path.Combine(directory, "run.json");
        var info = File.Exists(infoPath) ? JsonSerializer.Deserialize<RunInfo>(File.ReadAllText(infoPath)) : null;
        return (info, points);
    }

    private static string Csv(string s) =>
        s.Contains(',', StringComparison.Ordinal) || s.Contains('"', StringComparison.Ordinal)
            ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : s;

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
