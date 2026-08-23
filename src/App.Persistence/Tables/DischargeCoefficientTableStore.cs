using System.Globalization;
using App.Core;
using App.Core.Model;

namespace App.Persistence.Tables;

/// <summary>
/// Reads and writes <c>.vcd</c> discharge coefficient grids. Ports the parsing in
/// <c>TFIpol.LoadGrid</c> and the table build in <c>TCdValve.UpdateTable</c>
/// (IPolTab.pas).
/// </summary>
/// <remarks>
/// Every line starts with an empty field, because <c>SaveGrid</c> writes a comma before
/// each cell. Line one then carries the x-axis header, and the first cell of each
/// following line carries that row's y index. A <c>-</c> ends the axis in either
/// direction, giving a table of at most 20 by 20.
/// </remarks>
public sealed class DischargeCoefficientTableStore : IDischargeCoefficientTableStore
{
    /// <summary>Field 0 is the empty leading field; the axis value sits at field 1.</summary>
    private const int AxisField = 1;

    /// <summary>Grid cells start one field after the y index.</summary>
    private const int FirstValueField = 2;

    public DischargeCoefficientDocument Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var document = DelimitedGridDocument.Parse(File.ReadAllBytes(path));

        if (document.LineCount == 0)
        {
            throw new LegacyDataException($"'{path}' is empty.");
        }

        var table = new DischargeCoefficientTable { FileName = path };

        // Line 0 holds the x axis, from field 2 onward.
        var header = document.Fields(0);
        var xCount = 0;
        for (var f = FirstValueField; f < header.Count && xCount < EsaLimits.MaxDischargeTableSize; f++)
        {
            if (header[f] == DelimitedGridDocument.Unused)
            {
                break;
            }

            table.XIndex[xCount] = ParseCell(header[f], path, 0, f);
            xCount++;
        }

        var yCount = 0;
        for (var line = 1; line < document.LineCount && yCount < EsaLimits.MaxDischargeTableSize; line++)
        {
            var fields = document.Fields(line);

            if (fields.Count <= AxisField || fields[AxisField] == DelimitedGridDocument.Unused)
            {
                break;
            }

            table.YIndex[yCount] = ParseCell(fields[AxisField], path, line, AxisField);

            for (var x = 0; x < xCount; x++)
            {
                var field = FirstValueField + x;

                if (field >= fields.Count || fields[field] == DelimitedGridDocument.Unused)
                {
                    throw new LegacyDataException(
                        $"'{path}' line {line + 1} is missing a coefficient for x index {table.XIndex[x]}.");
                }

                table.Cell[x, yCount] = ParseCell(fields[field], path, line, field);
            }

            yCount++;
        }

        table.XCount = xCount;
        table.YCount = yCount;

        if (xCount == 0 || yCount == 0)
        {
            throw new LegacyDataException($"'{path}' contains no discharge coefficient data.");
        }

        return new VcdDocument(document, table);
    }

    public void Write(string path, DischargeCoefficientDocument document)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(document);

        if (document is not VcdDocument vcd)
        {
            throw new ArgumentException(
                $"Document must have been produced by {nameof(DischargeCoefficientTableStore)}.", nameof(document));
        }

        File.WriteAllBytes(path, vcd.Grid.ToBytes());
    }

    private static double ParseCell(string text, string path, int line, int field)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new LegacyDataException(
                $"'{path}' line {line + 1} field {field + 1} is not a number: '{text}'.");
        }

        return value;
    }

    private sealed class VcdDocument : DischargeCoefficientDocument
    {
        private readonly DischargeCoefficientTable _table;

        public VcdDocument(DelimitedGridDocument grid, DischargeCoefficientTable table)
        {
            Grid = grid;
            _table = table;
        }

        internal DelimitedGridDocument Grid { get; }

        public override DischargeCoefficientTable Table => _table;

        public override void SetCell(int row, int column, string value) =>
            Grid.SetField(row, FirstValueField + column, value);
    }
}
