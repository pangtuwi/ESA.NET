using App.Core.Interpolation;
using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// Valve lift and flow area against crank angle. Port of <c>TValve.Lift</c>,
/// <c>TValve.Open</c> and <c>TValve.FlowArea</c> (Valves.pas:53-84), with
/// <c>TProfile.Gety</c> (Profiles.pas:105-142) for the profile lookup.
/// </summary>
/// <remarks>
/// <b>Units.</b> As with <see cref="CylinderGeometry"/>, the angles and lengths are the
/// converted, SI ones: the <c>.eng</c> file holds valve timings as degrees before or
/// after a dead centre and lift and diameter in millimetres, and Delphi converts on the
/// way out of the edit form. <see cref="FromValve"/> is that boundary.
/// </remarks>
public sealed class ValveMotion
{
    private readonly CamProfile _profile;
    private readonly DischargeCoefficientTable _forward;
    private readonly DischargeCoefficientTable _reverse;

    /// <param name="openAngle">Delphi <c>O</c>, converted: <c>360 - IVO</c> or <c>180 - EVO</c>.</param>
    /// <param name="closeAngle">Delphi <c>C</c>, converted: <c>-180 + IVC</c> or <c>-360 + EVC</c>.</param>
    /// <param name="maxLift">Delphi <c>MaxLift</c>, in metres.</param>
    /// <param name="diameter">Delphi <c>D</c>, in metres.</param>
    /// <param name="count">Delphi <c>No</c>, valves of this kind per cylinder.</param>
    /// <param name="profile">The normalised cam profile from the <c>.cam</c> file.</param>
    public ValveMotion(
        double openAngle,
        double closeAngle,
        double maxLift,
        double diameter,
        int count,
        CamProfile profile,
        DischargeCoefficientTable? forward = null,
        DischargeCoefficientTable? reverse = null)
    {
        OpenAngle = openAngle;
        CloseAngle = closeAngle;
        MaxLift = maxLift;
        Diameter = diameter;
        Count = count;
        _profile = profile;
        _forward = forward ?? new DischargeCoefficientTable();
        _reverse = reverse ?? new DischargeCoefficientTable();
    }

    public double OpenAngle { get; }

    public double CloseAngle { get; }

    public double MaxLift { get; }

    public double Diameter { get; }

    public int Count { get; }

    /// <summary>
    /// Builds the motion for the inlet valve, converting from the file's units and
    /// timing convention.
    /// </summary>
    public static ValveMotion Inlet(Valve valve) =>
        Convert(valve, openAngle: 360 - valve.OpenAngle, closeAngle: -180 + valve.CloseAngle);

    /// <summary>Builds the motion for the exhaust valve.</summary>
    public static ValveMotion Exhaust(Valve valve) =>
        Convert(valve, openAngle: 180 - valve.OpenAngle, closeAngle: -360 + valve.CloseAngle);

    private static ValveMotion Convert(Valve valve, double openAngle, double closeAngle)
    {
        ArgumentNullException.ThrowIfNull(valve);

        return new ValveMotion(
            openAngle,
            closeAngle,
            valve.MaxLift / 1000,
            valve.Diameter / 1000,
            valve.Count,
            valve.Profile,
            valve.CdForward,
            valve.CdReverse);
    }

    /// <summary>
    /// Open duration in degrees of crank angle: <c>Open + 180 + Close</c> as the original
    /// displays it, which is the width of the window <see cref="Lift"/> uses.
    /// </summary>
    public double Duration => CloseAngle + 720 - OpenAngle;

    /// <summary>
    /// Valve lift in metres at a crank angle in degrees. Port of <c>TValve.Lift</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The window wraps: for any real cam the converted opening angle is greater than the
    /// converted closing angle, so the close is shifted forward a full cycle and a
    /// negative crank angle with it. The profile is then sampled at the fraction of the
    /// way through that window, and scaled by the maximum lift.
    /// </para>
    /// <para>
    /// The original reads <c>CNew</c> unconditionally but assigns it only inside
    /// <c>if O &gt; C</c>. When that test fails it compares against, and divides by, an
    /// uninitialised local. Here the else branch takes the closing angle as it stands,
    /// which is what the code was evidently meant to do; it is not reachable with any
    /// cam the original could load. See ISSUES.md B26.
    /// </para>
    /// </remarks>
    public double Lift(double crankAngleDegrees)
    {
        var theta = crankAngleDegrees;
        var close = CloseAngle;

        if (OpenAngle > CloseAngle)
        {
            close = CloseAngle + 720;

            if (theta < 0)
            {
                theta += 720;
            }
        }

        if (theta < OpenAngle || theta > close)
        {
            return 0;
        }

        return ProfileAt((theta - OpenAngle) / (close - OpenAngle)) * MaxLift;
    }

    /// <summary>Whether the valve is off its seat. Port of <c>TValve.Open</c>.</summary>
    public bool IsOpen(double crankAngleDegrees) => Lift(crankAngleDegrees) > 0;

    /// <summary>
    /// Curtain area in square metres. Port of <c>TValve.FlowArea</c>: the cylindrical
    /// area under the valve head, times the number of valves.
    /// </summary>
    public double FlowArea(double crankAngleDegrees) =>
        Lift(crankAngleDegrees) * Math.PI * Diameter * Count;


    /// <summary>
    /// Discharge coefficient at a crank angle and pressure ratio. Port of
    /// <c>TValve.FlowCoeff</c> (Valves.pas:31-47).
    /// </summary>
    /// <param name="crankAngleDegrees">Crank angle.</param>
    /// <param name="pressureRatio">Pressure ratio across the valve.</param>
    /// <param name="reverse">
    /// Whether flow is in the reverse direction, selecting <see cref="Valve.CdReverse"/>
    /// over <see cref="Valve.CdForward"/>. Note that for the exhaust valve those two are
    /// wired to the outward and inward tables respectively, because forward flow through
    /// an exhaust valve is outward: ISSUES.md B3.
    /// </param>
    /// <remarks>
    /// <b>A seated valve returns an undefined coefficient.</b> The original assigns the
    /// result 0 when the lift ratio is zero, and then unconditionally overwrites it with a
    /// local that only the other branch ever assigns, so a shut valve yields whatever was
    /// in that variable. The port returns 0, which is what the live branch was plainly
    /// meant to produce. See ISSUES.md B57.
    /// </remarks>
    public double FlowCoefficient(double crankAngleDegrees, double pressureRatio, bool reverse)
    {
        // A pressure ratio beyond 5 is off the end of every shipped table, and the
        // original answers with a flat 0.7 rather than extrapolating.
        if (pressureRatio > 5)
        {
            return 0.7;
        }

        var liftRatio = Lift(crankAngleDegrees) / MaxLift;

        if (liftRatio == 0)
        {
            return 0;
        }

        var table = reverse ? _reverse : _forward;

        return LegacyInterpolation.CoefficientAt(table, pressureRatio, liftRatio);
    }

    /// <summary>
    /// Normalised lift at a normalised position through the open period. Port of
    /// <c>TProfile.Gety</c>: clamp to the first point below the table, clamp to the last
    /// point above it, and interpolate linearly between.
    /// </summary>
    /// <remarks>
    /// The original returns <c>-1</c> for a profile that failed to load or holds fewer
    /// than two points, which would then be scaled by the maximum lift into a negative
    /// area. Reproduced, because a caller that has ignored <c>ProfileOk</c> is the case
    /// this is signalling. See ISSUES.md B41.
    /// </remarks>
    private double ProfileAt(double position)
    {
        var points = _profile.Points;

        if (!_profile.Modifying && (!_profile.ProfileOk || points.Count < 2))
        {
            return -1;
        }

        if (position < points[0].X)
        {
            return points[0].Y;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            if (position <= points[i + 1].X)
            {
                return LegacyInterpolation.Between(
                    position, points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
            }
        }

        return points[^1].Y;
    }
}
