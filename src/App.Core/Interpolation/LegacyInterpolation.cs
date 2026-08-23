using App.Core.Model;

namespace App.Core.Interpolation;

/// <summary>
/// The lookup behaviour of the legacy tables, kept out of the data-only models so the
/// quirks are visible and testable in one place.
/// </summary>
/// <remarks>
/// Some of what follows looks wrong. It is reproduced deliberately: the phase 4
/// reference runs were produced by this behaviour, so "fixing" it here would make the
/// ported physics disagree with the original for reasons that had nothing to do with
/// the physics.
/// </remarks>
public static class LegacyInterpolation
{
    /// <summary>Straight line between two points. Port of <c>InterpFc</c> (PNTWMath.pas).</summary>
    public static double Between(double x, double x1, double y1, double x2, double y2) =>
        x2 == x1 ? y1 : y1 + ((y2 - y1) * ((x - x1) / (x2 - x1)));

    /// <summary>
    /// Area at a position along the manifold. Port of <c>TAManf.GetValue</c>
    /// (FManfA.pas).
    /// </summary>
    /// <remarks>
    /// Note the last two lines of the original: past the end of the table the result is
    /// <b>zero</b>, not the final area. That is a cliff rather than a clamp, and the
    /// manifold solver depends on it.
    /// </remarks>
    public static double AreaAt(ManifoldAreaTable table, double position)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Count == 0)
        {
            return 0;
        }

        var i = 0;
        while (i < table.Count - 1 && table.Position[i] < position)
        {
            i++;
        }

        double result;
        if (i == 0)
        {
            result = table.Area[0];
        }
        else if (i == table.Count - 1 && table.Position[i] < position)
        {
            result = table.Area[i];
        }
        else
        {
            result = Between(position, table.Position[i - 1], table.Area[i - 1], table.Position[i], table.Area[i]);
        }

        return position > table.Position[i] ? 0 : result;
    }

    /// <summary>
    /// Discharge coefficient at a lift and pressure ratio. Port of
    /// <c>TCdValve.GetValue</c> (IPolTab.pas).
    /// </summary>
    /// <remarks>
    /// The final interpolation passes its y arguments in the opposite order to the two
    /// x interpolations above it — <c>(iny, yIndex[yi], Value1, yIndex[yi-1], Value2)</c>
    /// in the original. Reproduced verbatim.
    /// </remarks>
    public static double CoefficientAt(DischargeCoefficientTable table, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.XCount == 0 || table.YCount == 0)
        {
            return 0;
        }

        var xi = 0;
        while (xi < table.XCount - 1 && table.XIndex[xi] < x)
        {
            xi++;
        }

        var yi = 0;
        while (yi < table.YCount - 1 && table.YIndex[yi] < y)
        {
            yi++;
        }

        // The original forces both indices to at least the second entry so that
        // index-1 is always valid.
        xi = Math.Max(xi, 1);
        yi = Math.Max(yi, 1);

        var value1 = Between(x, table.XIndex[xi - 1], table.Cell[xi - 1, yi], table.XIndex[xi], table.Cell[xi, yi]);
        var value2 = Between(
            x, table.XIndex[xi - 1], table.Cell[xi - 1, yi - 1], table.XIndex[xi], table.Cell[xi, yi - 1]);

        return Between(y, table.YIndex[yi], value1, table.YIndex[yi - 1], value2);
    }

    /// <summary>
    /// Value at an engine speed from an RPM-keyed table, clamped at both ends. Port of
    /// the shared body of <c>TVarSpeedList.GetVal</c>, <c>TWallTemps.THead</c> and
    /// friends.
    /// </summary>
    public static double AtSpeed(IReadOnlyList<double> rpm, IReadOnlyList<double> values, double speed)
    {
        ArgumentNullException.ThrowIfNull(rpm);
        ArgumentNullException.ThrowIfNull(values);

        if (rpm.Count == 0)
        {
            return 0;
        }

        if (speed <= rpm[0])
        {
            return values[0];
        }

        for (var i = 1; i < rpm.Count; i++)
        {
            if (speed <= rpm[i])
            {
                return Between(speed, rpm[i - 1], values[i - 1], rpm[i], values[i]);
            }
        }

        return values[^1];
    }
}
