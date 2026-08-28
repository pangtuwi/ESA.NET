using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// Reads and writes the saved multi-run grid, the <c>.msr</c> format. Port of
/// <c>TFMultiRun.SaveGrid</c> and <c>LoadGrid</c> (MultiRun.pas:136-200).
/// </summary>
/// <remarks>
/// One line per run, comma separated: the row number, then the fourteen cells. All
/// hundred rows are written whether or not they hold anything, which is why every shipped
/// <c>.msr</c> file is exactly a hundred lines long.
/// </remarks>
public sealed class MultiRunGridStore
{
    /// <summary>A grid together with the line terminator its file used.</summary>
    /// <param name="Grid">The grid the file describes.</param>
    /// <param name="LineTerminator">The terminator the file used, so saving keeps it.</param>
    /// <param name="ShortFormat">
    /// Set when any line was a cell short of the current format - the forty-three shipped
    /// files that predate the Burn Angle column. They read correctly, but
    /// <see cref="Write"/> writes the current format, so saving one rewrites it.
    /// </param>
    public sealed record Document(
        MultiRunGrid Grid, string LineTerminator, bool ShortFormat = false);

    /// <summary>Reads a <c>.msr</c> file.</summary>
    /// <remarks>
    /// <para>
    /// The original parses each line <b>backwards</b> - from the end of the string,
    /// splitting on commas and filling columns from the rightmost inwards until it runs
    /// out of either. For a line carrying all fifteen fields that is the same answer this
    /// gives; for a short line it is not. Forty-three of the forty-nine shipped files
    /// carry fourteen fields, from before the Burn Angle column was added, and filling
    /// those from the right puts every value one column over and the row number itself
    /// into Speed - a grid that sweeps the engine from 1 rev/min. See ISSUES.md B68
    /// and C13.
    /// </para>
    /// <para>
    /// So this parses forwards instead: the leading field is the row number
    /// <c>SaveGrid</c> writes and is discarded, the rest fill the columns from the left,
    /// and a line that runs out early leaves the trailing columns unset. A line with more
    /// fields than the grid has columns, or one that does not start with a row number, is
    /// not a <c>.msr</c> line and raises <see cref="FormatException"/> rather than being
    /// quietly mangled.
    /// </para>
    /// </remarks>
    /// <exception cref="FormatException">The file is not in the <c>.msr</c> format.</exception>
    public Document Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var text = File.ReadAllText(path);
        var terminator = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var grid = new MultiRunGrid();
        var lines = text.Split(terminator);
        var short_ = false;

        for (var row = 0; row < MultiRunGrid.MaxRuns && row < lines.Length; row++)
        {
            var line = lines[row];

            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(',');

            // Field zero is the row number SaveGrid writes; the cells follow it.
            if (!int.TryParse(
                    fields[0].Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                throw new FormatException(
                    $"{Path.GetFileName(path)} line {row + 1} does not start with a row number: "
                    + $"\"{fields[0]}\".");
            }

            if (fields.Length - 1 > MultiRunGrid.ColumnCount)
            {
                throw new FormatException(
                    $"{Path.GetFileName(path)} line {row + 1} has {fields.Length - 1} cells where "
                    + $"the grid has {MultiRunGrid.ColumnCount}.");
            }

            short_ |= fields.Length - 1 < MultiRunGrid.ColumnCount;

            // Filled from the left, so a short line leaves the trailing columns unset.
            for (var column = 0; column < fields.Length - 1; column++)
            {
                grid[row, column] = fields[column + 1];
            }
        }

        return new Document(grid, terminator, short_);
    }

    /// <summary>Writes a <c>.msr</c> file, all hundred rows.</summary>
    /// <remarks>
    /// Always the current fifteen-field format, so a short-format file read and saved
    /// again is written back with its missing column filled in as unset. That is the one
    /// case where a <c>.msr</c> does not round-trip byte for byte, and writing it short
    /// instead would mean silently dropping any Burn Angle the operator had typed.
    /// </remarks>
    public void Write(string path, Document document)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(document);

        var text = new System.Text.StringBuilder();

        for (var row = 0; row < MultiRunGrid.MaxRuns; row++)
        {
            text.Append(row + 1);

            for (var column = 0; column < MultiRunGrid.ColumnCount; column++)
            {
                text.Append(',').Append(document.Grid[row, column]);
            }

            text.Append(document.LineTerminator);
        }

        File.WriteAllText(path, text.ToString());
    }
}
