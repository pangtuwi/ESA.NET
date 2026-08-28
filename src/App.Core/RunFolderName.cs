using System.Globalization;
using System.Text;

namespace App.Core;

/// <summary>
/// Names run folders and the row folders inside a sweep. Pure string work, kept apart
/// from <see cref="IWorkspace"/> so the rules can be checked without a file system.
/// </summary>
public static class RunFolderName
{
    /// <summary>
    /// Sortable, and legal on Windows - which rules out the colons of a round-trip
    /// timestamp.
    /// </summary>
    public const string TimestampFormat = "yyyy-MM-dd_HHmmss";

    /// <summary>
    /// The folder one run goes in: the time it started, then the engine it ran.
    /// </summary>
    /// <remarks>
    /// Time first so a directory listing is chronological across every engine, which is
    /// how a run is usually looked for - the one just finished, or the one from Tuesday.
    /// </remarks>
    public static string ForRun(string engineFilePath, DateTimeOffset startedAt)
    {
        var stamp = startedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var engine = Sanitise(Path.GetFileNameWithoutExtension(engineFilePath ?? string.Empty));

        return engine.Length == 0 ? stamp : $"{stamp}_{engine}";
    }

    /// <summary>
    /// The folder one row of a sweep goes in, below the sweep's own folder:
    /// <c>Row01_4000rpm</c>.
    /// </summary>
    /// <param name="row">Zero-based grid row; the name numbers from one.</param>
    public static string ForRow(int row, double speed) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Row{row + 1:D2}_{Math.Round(speed):F0}rpm");

    /// <summary>
    /// Replaces anything outside <c>A-Z a-z 0-9 . _ -</c> with an underscore, so a folder
    /// named after an engine file is legal on every platform whatever the file was called.
    /// Runs of replaced characters collapse into one.
    /// </summary>
    public static string Sanitise(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var text = new StringBuilder(name.Length);

        foreach (var character in name.Trim())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                text.Append(character);
            }
            else if (text.Length > 0 && text[^1] != '_')
            {
                text.Append('_');
            }
        }

        // A name of nothing but separators would leave a bare underscore behind.
        return text.ToString().Trim('_');
    }
}
