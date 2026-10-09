using System.Globalization;
using App.Core;

namespace App.Persistence.Tables;

/// <summary>
/// Reads a predecessor <c>.grd</c> grid size file. Port of <c>TGridSize.Load</c> and
/// <c>TGridSize.GridSize</c> (CAEEng, 1999, <c>GridSizes.pas</c>).
/// </summary>
/// <remarks>
/// <para>
/// The first line is a count <c>n</c>; the predecessor read the next <c>n</c> lines and
/// kept the last one as its function string, ignoring everything after it. With
/// <c>UserDefined</c> zero, <c>GridSize</c> was then <c>StrToInt</c> of that string, so the
/// line must be a whole number of points. <c>Example1/Inlet.grd</c> and <c>Exhaust.grd</c>
/// both begin <c>1</c>, <c>35</c>: the Pascal fragments that follow were never read.
/// </para>
/// <para>
/// ESA 3.0 never read a <c>.grd</c>. The port reads one only for an older-schema engine
/// that gives no grid size of its own (ISSUES.md A35).
/// </para>
/// </remarks>
public static class GridSizeFile
{
    /// <summary>The grid point count the file gives, as an expression string.</summary>
    public static string Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var lines = File.ReadAllLines(path);

        if (lines.Length == 0
            || !int.TryParse(lines[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || count < 1)
        {
            throw new LegacyDataException($"'{path}' does not start with a line count.");
        }

        if (lines.Length <= count)
        {
            throw new LegacyDataException($"'{path}' promises {count} lines and has {lines.Length - 1}.");
        }

        // StrToInt: a whole number, or the predecessor raised EConvertError.
        var text = lines[count].Trim();

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var points) || points < 1)
        {
            throw new LegacyDataException($"'{path}' gives '{text}' where a grid point count belongs.");
        }

        return points.ToString(CultureInfo.InvariantCulture);
    }
}
