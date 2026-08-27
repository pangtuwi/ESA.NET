namespace App.Core.Model;

/// <summary>
/// The multi-run table: a list of simulation points, each a speed and a cycle count plus
/// optional overrides for the manifold and cam files and the valve timing. Port of the
/// grid behind Delphi <c>TFMultiRun</c> (MultiRun.pas).
/// </summary>
/// <remarks>
/// Cells are held as text because the original does, and because a dash rather than a
/// value is how it says "leave this as the engine file has it". The typed accessors below
/// apply that convention.
/// </remarks>
public sealed class MultiRunGrid
{
    /// <summary>Delphi <c>MaxNoRuns</c>. The grid is always this tall, whatever is filled in.</summary>
    public const int MaxRuns = 100;

    /// <summary>The fourteen editable columns, in the order the grid shows them.</summary>
    public const int ColumnCount = 14;

    /// <summary>What the original writes, and expects, for a cell with no override.</summary>
    public const string Unset = "-";

    private readonly string[,] _cells = new string[MaxRuns, ColumnCount];

    public MultiRunGrid()
    {
        for (var row = 0; row < MaxRuns; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                _cells[row, column] = Unset;
            }
        }
    }

    /// <summary>Column headings, Delphi <c>SG1.Cells[1..14, 0]</c>.</summary>
    public static IReadOnlyList<string> ColumnNames { get; } =
    [
        "Speed", "Iters", "IManfFile", "EManfFile", "ICamFile", "ECamFile",
        "IVO", "IVC", "EVO", "EVC", "IValveLift", "EValveLift",
        "Spark °BTDC", "Burn Angle°",
    ];

    /// <summary>One cell, by zero-based row and column.</summary>
    public string this[int row, int column]
    {
        get => _cells[row, column];
        set => _cells[row, column] = string.IsNullOrWhiteSpace(value) ? Unset : value.Trim();
    }

    /// <summary>
    /// How many runs the grid describes. Port of <c>BOkClick</c>: the count is where the
    /// speed column first reads as unset, so runs have to start at the first row and be
    /// contiguous - a gap silently truncates the list.
    /// </summary>
    public int RunCount
    {
        get
        {
            var count = 0;

            while (count < MaxRuns && _cells[count, 0] != Unset)
            {
                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// A numeric override, or <see langword="null"/> where the cell is unset. Port of
    /// <c>GetMultiRunVar</c>.
    /// </summary>
    public double? Number(int row, int column) =>
        double.TryParse(
            _cells[row, column], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>
    /// A text override, or <see langword="null"/> where the cell is unset. Port of
    /// <c>GetMultiRunStr</c>.
    /// </summary>
    public string? Text(int row, int column) =>
        _cells[row, column] == Unset ? null : _cells[row, column];

    /// <summary>Engine speed for a run, in rev/min.</summary>
    public double? Speed(int row) => Number(row, 0);

    /// <summary>Requested cycle count for a run.</summary>
    public double? Cycles(int row) => Number(row, 1);
}
