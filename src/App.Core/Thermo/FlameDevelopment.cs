namespace App.Core.Thermo;

/// <summary>
/// The flame-development angle of a spark-ignition engine and the delay to the start of
/// combustion it implies for the Wiebe burn. No Delphi counterpart (ISSUES.md B90).
/// </summary>
/// <remarks>
/// <para>
/// J. B. Heywood, <i>Internal Combustion Engine Fundamentals</i>, McGraw-Hill, 1988,
/// section 9.2.3. The flame-development angle is the crank angle from the spark to 10 per
/// cent of the charge burnt (p. 389); there is no physical delay, the flame propagating
/// from the spark at once, so the Wiebe law's start of combustion (eq. 9.28, p. 390) is a
/// fitted point, placed here so that the law's 10 per cent point falls at the
/// flame-development angle.
/// </para>
/// <para>
/// Figure 9-17 (p. 394) measures the angle at about 18, 23, 27 and 29 degrees at 750,
/// 1500, 2250 and 3000 rev/min, and notes that it rises by a factor of about 1.6 for a
/// fourfold rise in speed: <c>23 (N / 1500)^k</c> with <c>k = ln 1.6 / ln 4</c> passes
/// within a degree of all four. Those engines ran at 0.54 atm inlet pressure, and a
/// lower inlet pressure lengthens the angle (p. 395), so at full throttle the angle, and
/// the delay, are upper bounds.
/// </para>
/// </remarks>
public static class FlameDevelopment
{
    /// <summary>The flame-development angle at 1500 rev/min, degrees (Heywood, Fig. 9-17).</summary>
    public const double AngleAt1500 = 23;

    /// <summary>The speed exponent: a factor of 1.6 for a fourfold rise in speed (Heywood, p. 394).</summary>
    public static readonly double SpeedExponent = Math.Log(1.6) / Math.Log(4);

    /// <summary>
    /// The fraction of the burn angle after the start of combustion at which the normalised
    /// Wiebe law of <see cref="TwoZoneGas"/> reaches 10 per cent burnt.
    /// </summary>
    public static readonly double WiebeTenPercent = Math.Pow(
        -Math.Log(1 - (0.1 * (1 - Math.Exp(-TwoZoneGas.WiebeEfficiency)))) / TwoZoneGas.WiebeEfficiency,
        1 / (TwoZoneGas.WiebeForm + 1));

    /// <summary>The flame-development angle, spark to 10 per cent burnt, in degrees.</summary>
    /// <param name="rpm">Engine speed, rev/min.</param>
    public static double Angle(double rpm) => AngleAt1500 * Math.Pow(rpm / 1500, SpeedExponent);

    /// <summary>
    /// Degrees from the spark to the start of combustion that put the Wiebe law's 10 per
    /// cent point at <paramref name="flameDevelopmentAngle"/>; never negative.
    /// </summary>
    public static double DefaultDelay(double flameDevelopmentAngle, double burnAngle) =>
        Math.Max(0, flameDevelopmentAngle - (WiebeTenPercent * burnAngle));
}
