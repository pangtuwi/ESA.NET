using System.Globalization;

namespace App.Tests;

/// <summary>
/// Locates <c>data/baseline</c> and reads the reference PVT trace. Phase 4 checks its
/// work against that trace from several test classes, so the parsing lives here rather
/// than being repeated.
/// </summary>
/// <remarks>
/// See BASELINE.md. The trace is <c>A2China.txt</c>: a comma-separated header naming 29
/// columns, then one row per crank angle from -359 to 360.
/// </remarks>
internal static class BaselinePaths
{
    private static readonly Lazy<List<string>> TraceLines = new(ReadTraceLines);

    private static readonly Lazy<List<string>> TraceHeader = new(() =>
        TraceLines.Value[0].Split(',').Select(h => h.Trim()).ToList());

    /// <summary>The baseline directory, or <see langword="null"/> outside a checkout.</summary>
    public static string? Directory { get; } = Find();

    public static void Require() =>
        Assert.SkipWhen(Directory is null, "Not running from a repository checkout.");

    public static string File(string name) => Path.Combine(Directory!, name);

    /// <summary>
    /// One column of the trace, paired with the crank angle it was recorded at.
    /// </summary>
    /// <param name="column">A name from the trace header, for example <c>PCyl</c>.</param>
    public static IReadOnlyList<(double CrankAngle, double Value)> TraceColumn(string column)
    {
        var index = TraceHeader.Value.IndexOf(column);

        if (index < 0)
        {
            throw new ArgumentException(
                $"The trace has no column '{column}'. It has: {string.Join(", ", TraceHeader.Value)}.",
                nameof(column));
        }

        return TraceLines.Value
            .Skip(1)
            .Select(line => line.Split(','))
            .Select(fields => (
                double.Parse(fields[0], CultureInfo.InvariantCulture),
                double.Parse(fields[index], CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static List<string> ReadTraceLines() =>
        System.IO.File.ReadAllLines(File("A2China.txt"))
            .Where(line => line.Trim().Length > 0)
            .ToList();

    private static string? Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "baseline");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
