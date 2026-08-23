using System.Globalization;
using App.Core;

namespace App.Persistence.Tables;

/// <summary>
/// The shape shared by <c>.spk</c>, <c>.cwt</c> and <c>.exh</c>: a row count on line
/// one, a heading on line two that the Delphi loaders read and discard, then that many
/// rows of whitespace- or tab-separated numbers.
/// </summary>
/// <remarks>
/// The three Delphi loaders (<c>TVarSpeedList.Load</c>, <c>TWallTemps.Load</c>,
/// <c>TExhaustPandT.Load</c>) are near-identical; this is the one place that
/// difference lives, so a fix reaches all three.
/// </remarks>
internal static class DelimitedTable
{
    private static readonly char[] Separators = [' ', '\t'];

    /// <summary>
    /// Parses the file into rows of doubles.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <param name="columns">Number of columns each row must supply.</param>
    /// <param name="maximumRows">
    /// Row limit, or <see langword="null"/> for no limit. The Delphi loaders enforce
    /// this by calling <c>Halt</c>, killing the application mid-run; this throws.
    /// </param>
    public static List<double[]> Read(string path, int columns, int? maximumRows)
    {
        var lines = File.ReadAllLines(path);

        if (lines.Length < 1)
        {
            throw new LegacyDataException($"'{path}' is empty; expected a row count on the first line.");
        }

        if (!int.TryParse(lines[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowCount))
        {
            throw new LegacyDataException(
                $"'{path}' does not start with a row count; found '{lines[0].Trim()}'.");
        }

        if (rowCount < 0)
        {
            throw new LegacyDataException($"'{path}' declares a negative row count of {rowCount}.");
        }

        if (maximumRows is { } limit && rowCount > limit)
        {
            throw new LegacyDataException(
                $"'{path}' declares {rowCount} rows but the maximum is {limit}.");
        }

        // Line 0 is the count, line 1 the heading, data starts at line 2.
        const int FirstDataLine = 2;

        if (lines.Length < FirstDataLine + rowCount)
        {
            throw new LegacyDataException(
                $"'{path}' declares {rowCount} rows but contains only {Math.Max(0, lines.Length - FirstDataLine)}.");
        }

        var rows = new List<double[]>(rowCount);

        for (var i = 0; i < rowCount; i++)
        {
            var lineNumber = FirstDataLine + i;
            var fields = lines[lineNumber].Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < columns)
            {
                throw new LegacyDataException(
                    $"'{path}' line {lineNumber + 1} has {fields.Length} columns; expected {columns}.");
            }

            var values = new double[columns];
            for (var c = 0; c < columns; c++)
            {
                if (!double.TryParse(fields[c], NumberStyles.Float, CultureInfo.InvariantCulture, out values[c]))
                {
                    throw new LegacyDataException(
                        $"'{path}' line {lineNumber + 1} column {c + 1} is not a number: '{fields[c]}'.");
                }
            }

            rows.Add(values);
        }

        return rows;
    }
}
