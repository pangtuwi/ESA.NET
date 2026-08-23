namespace App.Persistence;

/// <summary>
/// Turns the side-file paths stored in a <c>.eng</c> file into paths that exist on
/// this machine.
/// </summary>
/// <remarks>
/// <para>
/// The shipped data is a museum of 2001 Windows habits. Some entries are bare file
/// names, some are relative with backslash separators
/// (<c>Variable_Inlet_Diameter\A3TumbleInlet_M770_36.maf</c>), and some are absolute
/// paths to a drive that has not existed for twenty years
/// (<c>c:\CAEEng\NissanTesis\490Inlet.maf</c>).
/// </para>
/// <para>
/// Backslashes are not separators on Linux or macOS, so a naive
/// <see cref="File.Exists(string)"/> fails on every relative path there too. Resolution
/// therefore tries, in order: the path as written, the path with separators
/// normalised, that path relative to the engine file's own directory, the bare file
/// name in that directory, and finally a search of the directory tree beneath it.
/// </para>
/// </remarks>
public sealed class LegacyPathResolver
{
    private readonly string _baseDirectory;

    /// <param name="engineFilePath">The <c>.eng</c> file whose entries are being resolved.</param>
    public LegacyPathResolver(string engineFilePath)
    {
        ArgumentNullException.ThrowIfNull(engineFilePath);

        _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(engineFilePath)) ?? ".";
    }

    /// <summary>The directory the engine file lives in; all relative entries hang off it.</summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>
    /// Resolves a stored entry to an existing file, or <see langword="null"/> when
    /// nothing matches.
    /// </summary>
    public string? Resolve(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var trimmed = storedPath.Trim();

        if (File.Exists(trimmed))
        {
            return trimmed;
        }

        var normalised = Normalise(trimmed);

        if (File.Exists(normalised))
        {
            return normalised;
        }

        var relative = Path.Combine(_baseDirectory, normalised);
        if (File.Exists(relative))
        {
            return relative;
        }

        // Absolute legacy paths are worth mining for their file name: the data has
        // usually been copied next to the engine file at some point since.
        var fileName = Path.GetFileName(normalised);
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var beside = Path.Combine(_baseDirectory, fileName);
        if (File.Exists(beside))
        {
            return beside;
        }

        // Last resort: anywhere below the engine file. Ordered so the result does not
        // depend on the file system's enumeration order.
        return Directory
            .EnumerateFiles(_baseDirectory, fileName, SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Rewrites Windows separators for the current platform. On Windows this is a
    /// no-op; elsewhere it is what makes a relative legacy path usable at all.
    /// </summary>
    private static string Normalise(string path) =>
        Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', Path.DirectorySeparatorChar);
}
