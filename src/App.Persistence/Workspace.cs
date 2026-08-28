using App.Core;
using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// The data folder on disk. See <see cref="IWorkspace"/> for why the port has one where
/// the original did not.
/// </summary>
/// <remarks>
/// The root is resolved once, first hit winning: the <c>ESA_DATA_ROOT</c> environment
/// variable, then <c>ESA.ini</c>'s <c>[Folders] Data</c>, then <c>Documents/ESA</c>. The
/// environment variable exists for the test suite above all - the tests drive real runs
/// through the view model, and without it they would write into the operator's Documents.
/// </remarks>
public sealed class Workspace : IWorkspace
{
    /// <summary>The environment variable that overrides everything else.</summary>
    public const string RootVariable = "ESA_DATA_ROOT";

    /// <summary>The folder name used under Documents when nothing else is configured.</summary>
    public const string DefaultFolderName = "ESA";

    private readonly Lock _gate = new();

    private bool _prepared;

    /// <param name="rootDirectory">
    /// The data folder, used as given. <see cref="From"/> is what applies the environment
    /// variable and <c>ESA.ini</c>; taking a root here rather than resolving one keeps a
    /// test's workspace a test's workspace whatever the machine running it has set.
    /// </param>
    public Workspace(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);

        RootDirectory = Path.GetFullPath(Expand(rootDirectory.Trim()));
        EnginesDirectory = Path.Combine(RootDirectory, "Engines");
        RunsDirectory = Path.Combine(RootDirectory, "Runs");
    }

    /// <summary>
    /// The workspace the application runs on: <c>ESA_DATA_ROOT</c> if it is set, then
    /// <c>ESA.ini</c>'s <c>[Folders] Data</c>, then <c>Documents/ESA</c>.
    /// </summary>
    public static Workspace From(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            return new Workspace(ResolveRoot(settings.DataFolder));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException
                                          or PathTooLongException)
        {
            // A hand-edited ESA.ini - or an environment variable - naming an impossible
            // folder is not a reason to refuse to start; Documents is somewhere the
            // operator can always reach.
            return new Workspace(DefaultRoot());
        }
    }

    /// <inheritdoc />
    public string RootDirectory { get; }

    /// <inheritdoc />
    public string EnginesDirectory { get; }

    /// <inheritdoc />
    public string RunsDirectory { get; }

    /// <inheritdoc />
    public string CreateRunDirectory(string engineFilePath, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(engineFilePath);

        Prepare();

        var wanted = Path.Combine(RunsDirectory, RunFolderName.ForRun(engineFilePath, startedAt));

        lock (_gate)
        {
            // Two runs of the same engine inside one second is unlikely but not
            // impossible, and silently writing a second run's files over the first would
            // be exactly the loss this folder exists to prevent.
            var path = wanted;

            for (var attempt = 2; Directory.Exists(path); attempt++)
            {
                path = $"{wanted}_{attempt}";
            }

            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Also called before each run folder is created, so a workspace whose folders were
    /// deleted mid-session still works. The note is written only if it is not already
    /// there, so an operator who edits it keeps their edit.
    /// </remarks>
    public void Prepare()
    {
        lock (_gate)
        {
            if (_prepared)
            {
                return;
            }

            Directory.CreateDirectory(EnginesDirectory);
            Directory.CreateDirectory(RunsDirectory);

            var readme = Path.Combine(RootDirectory, "README.txt");

            if (!File.Exists(readme))
            {
                File.WriteAllText(readme, Readme());
            }

            _prepared = true;
        }
    }

    /// <summary>
    /// Where the data folder would be for a given <c>[Folders] Data</c> entry, first hit
    /// winning. Exposed so the resolution order can be checked without a file system.
    /// </summary>
    public static string ResolveRoot(string? configuredRoot)
    {
        if (Environment.GetEnvironmentVariable(RootVariable) is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return configuredRoot;
        }

        return DefaultRoot();
    }

    /// <summary>
    /// <c>Documents/ESA</c>. Documents is empty on a headless Linux box with no XDG
    /// configuration, in which case the home directory is the next best thing and the
    /// current directory the one after that.
    /// </summary>
    public static string DefaultRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (documents.Length == 0)
        {
            documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(
            documents.Length == 0 ? Directory.GetCurrentDirectory() : documents,
            DefaultFolderName);
    }

    /// <summary>
    /// Expands a leading <c>~</c>, which an operator editing <c>ESA.ini</c> by hand on
    /// Linux or macOS will reasonably expect to work.
    /// </summary>
    private static string Expand(string path)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Environment.ExpandEnvironmentVariables(path);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return path.Length == 1 ? home : Path.Combine(home, path[2..]);
    }

    private string Readme() =>
        $"""
         ESA data folder
         ===============

         Engines   the engine files and the side files they name - cam profiles (.cam),
                   manifold areas (.maf), discharge coefficients (.vcd), spark maps
                   (.spk), wall temperatures (.cwt) and exhaust back pressure (.exh).
                   Subfolders are fine; an engine file's side files are looked for
                   beside it and below it.

         Runs      one folder per simulation, named for the time it started and the
                   engine it ran. Each holds:

                     run.txt       what was run, and what came out
                     inputs        copies of the engine file and every side file it
                                   read, so the result can still be accounted for
                                   after the engine has been edited
                     SimulDat.txt  the performance row, as the original writes it
                     Lastcyc.txt   the full-cycle PVT trace, 720 rows
                     Inlet.txt Exhaust.txt Pcyl.txt Tcyl.txt MassFlow.txt
                     InlPress.m InlVel.m ExhPress.m ExhVel.m
                                   the nine manifold output files

                   A multi-point sweep puts all of its rows in one folder, each row in
                   a Row01_4000rpm subfolder of its own, with one SimulDat.txt at the
                   top carrying every row.

         Nothing here is read back by the application except the engines, so old run
         folders can be deleted or moved whenever they stop being interesting.

         This folder is set by the {RootVariable} environment variable, or by Data=
         under [Folders] in ESA.ini, and defaults to Documents/{DefaultFolderName}.

         """.ReplaceLineEndings("\r\n");
}
