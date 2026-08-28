using App.Core;
using App.Core.Interpolation;
using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// One run's folder: copies of what it read, and everywhere its output goes.
/// </summary>
/// <remarks>
/// <para>
/// The original wrote its nine manifold files and <c>SimulDat.txt</c> under bare relative
/// names, so they landed in the working directory and the next run overwrote them
/// (ISSUES.md C4, C6). Nothing recorded which engine, which speed, or which cam profile
/// had produced them. This puts each run's output in a folder of its own next to copies of
/// its inputs - the arrangement <c>data/baseline/</c> was assembled by hand to get, and
/// which <c>BASELINE.md</c> gives as the reason that set can be trusted where the output
/// scattered through <c>legacy/ESA/Data</c> cannot.
/// </para>
/// <para>
/// The file <b>formats</b> are untouched: this routes the existing writers at a
/// destination and adds none of its own except <c>run.txt</c>.
/// </para>
/// </remarks>
public sealed class RunArchive
{
    /// <summary>Where the copies of the run's input files go.</summary>
    public const string InputsFolderName = "inputs";

    /// <summary>The performance file's legacy name, from <c>TFMain.WriteRunFile</c>.</summary>
    public const string PerformanceFileName = "SimulDat.txt";

    /// <summary>The PVT trace's legacy name, ESA.ini's <c>TextSave</c> default.</summary>
    public const string TraceFileName = "Lastcyc.txt";

    /// <summary>The manifest, which is the port's own and has no legacy counterpart.</summary>
    public const string ManifestFileName = "run.txt";

    private readonly PerformanceResultWriter _performance = new();
    private readonly CrankAngleTraceWriter _trace = new();

    /// <param name="directory">The run folder, which must already exist.</param>
    public RunArchive(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        Directory = directory;
    }

    /// <summary>The run folder itself.</summary>
    public string Directory { get; }

    /// <summary>Where <c>run.txt</c> goes.</summary>
    public string ManifestFile => Path.Combine(Directory, ManifestFileName);

    /// <summary>
    /// Where the performance rows go. A single-point run leaves one row here; a sweep
    /// leaves one per grid row, which is the one place the original's appending
    /// (ISSUES.md C6) is worth having.
    /// </summary>
    public string PerformanceFile => Path.Combine(Directory, PerformanceFileName);

    /// <summary>A folder for one row of a sweep, below this one.</summary>
    public RunArchive Row(int row, double speed)
    {
        var path = Path.Combine(Directory, RunFolderName.ForRow(row, speed));

        System.IO.Directory.CreateDirectory(path);

        return new RunArchive(path);
    }

    /// <summary>
    /// Copies the engine file and every side file it read into <c>inputs</c>, and returns
    /// what was copied, described for the manifest.
    /// </summary>
    /// <remarks>
    /// Copied verbatim, so an archived <c>.eng</c> is byte for byte the file that was run -
    /// the same guarantee <c>EngRoundTripTests</c> holds the writer to. One file read twice
    /// is copied once and listed under both its uses, which is the ordinary case: the four
    /// discharge coefficient entries of a shipped engine usually name two files between
    /// them. Two different files sharing a name - which <c>LegacyPathResolver</c> can turn
    /// up, since it searches the whole tree below the engine - both get copied, the second
    /// under a numbered name rather than over the first.
    /// </remarks>
    public IReadOnlyList<string> CopyInputs(string engineFilePath, EngineLoadResult? engine)
    {
        ArgumentNullException.ThrowIfNull(engineFilePath);

        var inputs = Path.Combine(Directory, InputsFolderName);
        System.IO.Directory.CreateDirectory(inputs);

        var copied = new List<string>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        Copy(engineFilePath, "engine");

        foreach (var side in engine?.SideFiles ?? [])
        {
            Copy(side.Path, side.Kind);
        }

        return copied;

        void Copy(string path, string what)
        {
            if (path.Length == 0 || !File.Exists(path))
            {
                return;
            }

            var full = Path.GetFullPath(path);

            if (sources.TryGetValue(full, out var already))
            {
                // The same file under another name: one copy, both uses recorded.
                copied[already] = copied[already][..^1] + $", {what})";
                return;
            }

            var name = Unique(Path.GetFileName(full));

            try
            {
                File.Copy(full, Path.Combine(inputs, name), overwrite: true);
                sources[full] = copied.Count;
                copied.Add($"{name}  ({what})");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                copied.Add($"{Path.GetFileName(full)}  ({what}) - not copied: {error.Message}");
            }
        }

        string Unique(string name)
        {
            if (taken.Add(name))
            {
                return name;
            }

            var stem = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);

            for (var attempt = 2; ; attempt++)
            {
                var candidate = $"{stem}_{attempt}{extension}";

                if (taken.Add(candidate))
                {
                    return candidate;
                }
            }
        }
    }

    /// <summary>Appends this run's performance row, heading it if the file is new.</summary>
    public void AppendPerformance(Engine engine) =>
        _performance.Append(PerformanceFile, engine, ExhaustBackPressure(engine));

    /// <summary>
    /// The absolute exhaust back pressure the results row reports, looked up afresh from
    /// the <c>.exh</c> table as <c>TFMain.WriteRunFile</c> does rather than read off the
    /// exhaust gas, which the run has since moved.
    /// </summary>
    public static double ExhaustBackPressure(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var table = engine.Manifold.ExhaustBack;

        // Gauge kPa in the file; TExhaustPandT.Pres makes it absolute pascals
        // (ExhBackPandT.pas:72). The writer then subtracts a hard-coded atmosphere to get
        // back to gauge, which is ISSUES.md B69 and reproduced there.
        return (LegacyInterpolation.AtSpeed(table.Rpm, table.Pressure, engine.Rpm) * 1000)
               + engine.Atmosphere.PGas;
    }

    /// <summary>Writes the full-cycle PVT trace.</summary>
    public void WriteTrace(CrankAngleTrace trace) =>
        _trace.Write(Path.Combine(Directory, TraceFileName), trace);

    /// <summary>Writes the nine manifold output files.</summary>
    public void WriteManifoldData(ManifoldTraceWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Directory);
    }
}
