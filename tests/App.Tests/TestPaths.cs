namespace App.Tests;

/// <summary>
/// Locates test data. Fixtures under <c>legacy/samples</c> are copied verbatim into
/// the test output by the project file; the full legacy tree is found by walking up
/// to the repository root.
/// </summary>
internal static class TestPaths
{
    /// <summary>The frozen <c>.eng</c> fixtures, copied next to the test assembly.</summary>
    public static string Samples { get; } = Path.Combine(AppContext.BaseDirectory, "samples");

    /// <summary>The untouched Delphi tree, or <see langword="null"/> when running outside a checkout.</summary>
    public static string? Legacy { get; } = FindLegacy();

    public static IEnumerable<string> SampleEngineFiles() =>
        Directory.EnumerateFiles(Samples, "*.eng", SearchOption.AllDirectories).Order();

    public static IEnumerable<string> AllLegacyEngineFiles() =>
        Legacy is null
            ? []
            : Directory.EnumerateFiles(Legacy, "*.eng", SearchOption.AllDirectories).Order();

    private static string? FindLegacy()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "legacy");
            if (Directory.Exists(Path.Combine(candidate, "ESA", "Data")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
