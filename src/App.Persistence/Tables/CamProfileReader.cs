using System.Globalization;
using App.Core;
using App.Core.Model;

namespace App.Persistence.Tables;

/// <summary>
/// Reads <c>.cam</c> valve lift profiles. Port of <c>TProfile.LoadText</c>
/// (Profiles.pas).
/// </summary>
/// <remarks>
/// The Delphi loader is a bare <c>Readln(TF, inx, iny)</c> loop to end of file: two
/// whitespace-separated doubles per line, no row count and no heading. The shipped
/// files happen to be fixed-width, but the reader never depended on that and neither
/// does this one.
/// </remarks>
public sealed class CamProfileReader : ICamProfileReader
{
    private static readonly char[] Separators = [' ', '\t'];

    public CamProfile Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var profile = new CamProfile { FileName = path };
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;

            // Delphi's Readln skips blank lines rather than failing on them.
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 2)
            {
                throw new LegacyDataException(
                    $"'{path}' line {lineNumber} has {fields.Length} value(s); a cam profile needs two.");
            }

            if (!double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                throw new LegacyDataException($"'{path}' line {lineNumber} is not a pair of numbers: '{line}'.");
            }

            profile.Points.Add(new ProfilePoint(x, y));
        }

        if (profile.Points.Count == 0)
        {
            throw new LegacyDataException($"'{path}' contains no profile points.");
        }

        SetLimits(profile);
        profile.ProfileOk = true;

        return profile;
    }

    /// <summary>
    /// Port of <c>TProfile.GetLimits</c>: the extents, the peak lift and the duration
    /// implied by the points, plus the mean point spacing.
    /// </summary>
    private static void SetLimits(CamProfile profile)
    {
        var points = profile.Points;

        profile.XMin = points.Min(p => p.X);
        profile.XMax = points.Max(p => p.X);
        profile.YMin = points.Min(p => p.Y);
        profile.YMax = points.Max(p => p.Y);
        profile.Lift = profile.YMax;
        profile.Duration = profile.XMax - profile.XMin;
        profile.Spacing = points.Count > 1 ? profile.Duration / (points.Count - 1) : 0;
    }
}
