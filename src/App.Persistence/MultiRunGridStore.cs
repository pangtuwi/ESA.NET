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
    public sealed record Document(MultiRunGrid Grid, string LineTerminator);

    /// <summary>Reads a <c>.msr</c> file.</summary>
    /// <remarks>
    /// <para>
    /// The original parses each line <b>backwards</b> - from the end of the string,
    /// splitting on commas and filling columns from the rightmost inwards until it runs
    /// out of either. One consequence is that the row number <c>SaveGrid</c> writes at
    /// the start of every line is never read: the loop stops once the fourteen cells are
    /// filled, and whatever is left at the front is discarded. Another is that a line
    /// with too few commas fills the <b>right-hand</b> columns and leaves the left ones
    /// alone, rather than the other way round.
    /// </para>
    /// <para>
    /// Splitting forwards and taking the last fourteen fields is the same thing for any
    /// well-formed file and is what this does; see ISSUES.md B68 for where the two part
    /// company.
    /// </para>
    /// </remarks>
    public Document Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var text = File.ReadAllText(path);
        var terminator = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var grid = new MultiRunGrid();
        var lines = text.Split(terminator);

        for (var row = 0; row < MultiRunGrid.MaxRuns && row < lines.Length; row++)
        {
            var line = lines[row];

            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(',');

            // Filled from the right, as the original does.
            for (var column = MultiRunGrid.ColumnCount - 1; column >= 0; column--)
            {
                var field = fields.Length - MultiRunGrid.ColumnCount + column;

                if (field < 0)
                {
                    break;
                }

                grid[row, column] = fields[field];
            }
        }

        return new Document(grid, terminator);
    }

    /// <summary>Writes a <c>.msr</c> file, all hundred rows.</summary>
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
