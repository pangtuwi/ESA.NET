using System.Globalization;
using System.Text;
using App.Core;
using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// Writes a cycle's captured data as the comma-separated text file the original produces
/// from the PVT window's Save As. Port of <c>TCAList.SendToFile</c>
/// (CAList2z.pas:139-159).
/// </summary>
/// <remarks>
/// A heading row naming the crank angle and the 28 captured quantities, then one row per
/// crank angle from -359 to 360, each value scaled for display and written to its own
/// number of decimal places in a field ten characters wide.
/// </remarks>
public sealed class CrankAngleTraceWriter
{
    /// <summary>Writes <paramref name="trace"/> to <paramref name="path"/>.</summary>
    public void Write(string path, CrankAngleTrace trace)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(trace);

        File.WriteAllText(path, Format(trace));
    }

    /// <summary>The file's contents, exposed separately so it can be compared in tests.</summary>
    public string Format(CrankAngleTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var text = new StringBuilder();
        var columns = EsaLimits.CapturedValueCount;

        text.Append("CA, ");

        for (var column = 1; column < columns; column++)
        {
            text.Append(trace.ColumnNames[column - 1]).Append(", ");
        }

        // Delphi's Writeln is CRLF on Windows, which is what the program produces. Note
        // that data/baseline/A2China.txt arrived with LF endings while every other file
        // from the same run kept CRLF, so that copy was normalised in transit; see
        // BASELINE.md.
        text.Append(trace.ColumnNames[columns - 1]).Append("\r\n");

        for (var crankAngle = EsaLimits.FirstCrankAngle;
             crankAngle <= EsaLimits.LastCrankAngle;
             crankAngle++)
        {
            var point = trace[crankAngle];

            text.Append(crankAngle.ToString(CultureInfo.InvariantCulture)).Append(", ");

            for (var column = 1; column <= columns; column++)
            {
                text.Append(Field(
                    point[column] * trace.ScaleFactors[column - 1],
                    trace.Decimals[column - 1]));

                text.Append(column < columns ? ", " : "\r\n");
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Delphi's <c>write(x:10:d)</c>: fixed decimals, right-aligned in ten characters.
    /// </summary>
    /// <remarks>
    /// The original writes the last column as <c>value[NoVals]*k[i]</c>, reusing the
    /// loop counter after the loop that filled the other 27 columns. Pascal leaves that
    /// counter undefined on exit, so the scale applied to the final column is whatever
    /// the compiler happened to leave behind. It evidently left 28, because the recorded
    /// heat loss is in joules and <c>k[28]</c> is 1 where <c>k[27]</c> is 1000 - had it
    /// left 27 the column would read a thousand times larger. The port uses
    /// <c>k[NoVals]</c>, which is both the evident intent and what the reference file
    /// shows. See ISSUES.md B67.
    /// </remarks>
    private static string Field(double value, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture)
            .PadLeft(10);
}
