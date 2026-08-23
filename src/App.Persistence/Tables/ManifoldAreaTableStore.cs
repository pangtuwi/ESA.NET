using System.Globalization;
using App.Core;
using App.Core.Model;

namespace App.Persistence.Tables;

/// <summary>
/// Reads and writes <c>.maf</c> manifold area tables. Ports the parsing in
/// <c>TFManfArea.LoadGrid</c> and the validation in <c>TAManf.UpdateTable</c>
/// (FManfA.pas), both of which live in form code-behind in the original.
/// </summary>
/// <remarks>
/// Each line is <c>row,position,area</c> — the leading field is a one-based row number
/// written by <c>SaveGrid</c>, not data. Positions are in millimetres and areas in
/// square millimetres; <c>TPipe</c> converts to metres and square metres on use.
/// A <c>-</c> in the position column ends the table.
/// </remarks>
public sealed class ManifoldAreaTableStore : IManifoldAreaTableStore
{
    /// <summary>Column indices within a line, after the leading row number.</summary>
    private const int PositionField = 1;

    private const int AreaField = 2;

    public ManifoldAreaDocument Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var document = DelimitedGridDocument.Parse(File.ReadAllBytes(path));
        var table = new ManifoldAreaTable { FileName = path };
        var count = 0;

        for (var line = 0; line < document.LineCount && count < EsaLimits.MaxManifoldAreaPoints; line++)
        {
            var fields = document.Fields(line);

            if (fields.Count <= AreaField)
            {
                // A short or blank line ends the table, as end-of-file does in the original.
                break;
            }

            var position = fields[PositionField];
            if (position == DelimitedGridDocument.Unused)
            {
                break;
            }

            table.Position[count] = ParseCell(position, path, line, "position");
            table.Area[count] = ParseCell(fields[AreaField], path, line, "area");
            count++;
        }

        table.Count = count;
        Validate(table, path);

        return new MafDocument(document, table);
    }

    public void Write(string path, ManifoldAreaDocument document)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(document);

        if (document is not MafDocument maf)
        {
            throw new ArgumentException(
                $"Document must have been produced by {nameof(ManifoldAreaTableStore)}.", nameof(document));
        }

        File.WriteAllBytes(path, maf.Grid.ToBytes());
    }

    private static double ParseCell(string text, string path, int line, string what)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new LegacyDataException($"'{path}' line {line + 1} has a non-numeric {what}: '{text}'.");
        }

        return value;
    }

    /// <summary>
    /// The checks <c>TAManf.UpdateTable</c> applies before accepting a table. The
    /// original reported each through a dialog and abandoned the load; this throws.
    /// </summary>
    private static void Validate(ManifoldAreaTable table, string path)
    {
        if (table.Count == 0)
        {
            throw new LegacyDataException($"'{path}' contains no area points.");
        }

        if (table.Position[0] != 0)
        {
            throw new LegacyDataException(
                $"'{path}' does not begin at zero: the first position is {table.Position[0]}.");
        }

        for (var i = 1; i < table.Count; i++)
        {
            if (table.Position[i] <= table.Position[i - 1])
            {
                throw new LegacyDataException(
                    $"'{path}' positions are not sequential ascending at row {i + 1}: "
                    + $"{table.Position[i]} follows {table.Position[i - 1]}.");
            }
        }
    }

    private sealed class MafDocument : ManifoldAreaDocument
    {
        private readonly ManifoldAreaTable _table;

        public MafDocument(DelimitedGridDocument grid, ManifoldAreaTable table)
        {
            Grid = grid;
            _table = table;
        }

        internal DelimitedGridDocument Grid { get; }

        public override ManifoldAreaTable Table => _table;

        public override void SetRow(int rowIndex, string position, string area)
        {
            Grid.SetField(rowIndex, PositionField, position);
            Grid.SetField(rowIndex, AreaField, area);
        }
    }
}
