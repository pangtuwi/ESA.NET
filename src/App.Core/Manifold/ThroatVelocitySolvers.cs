namespace App.Core.Manifold;

/// <summary>
/// The velocity and Mach-number solvers used at an open valve. Port of
/// <c>InlSonicVelSolve</c>, <c>InlSubSonicVelSolve</c>, <c>ExhSonicVelSolve</c>,
/// <c>ExhSubSonicVelSolve</c> and <c>ExhSonicMachSolve</c> (Manifolds.pas:182-343).
/// </summary>
/// <remarks>
/// All five are the same false-position root finder over a different residual, bracketed
/// on a fixed interval, iterated to 1e-7 with a cap of 100,000. Only the residual and the
/// bracket differ, so the search itself lives in one place here.
/// </remarks>
public static class ThroatVelocitySolvers
{
    private const int MaxIterations = 100000;
    private const double Tolerance = 0.0000001;

    /// <summary>
    /// Throat velocity for choked flow reversing through the inlet valve. Port of
    /// <c>InlSonicVelSolve</c>.
    /// </summary>
    public static double InletSonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double cylinderSpeedOfSound) =>
        Solve(
            SonicResidual(gamma, dischargeCoefficient, areaRatio, cylinderSpeedOfSound),
            low: 0.000001 * throatVelocity,
            high: 0.6 * throatVelocity,
            what: "Inlet Sonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// Throat velocity for choked flow out of the exhaust valve. Port of
    /// <c>ExhSonicVelSolve</c>.
    /// </summary>
    /// <remarks>
    /// The residual is identical to <see cref="InletSonic"/>'s. Only the upper bracket
    /// differs: 0.8 of the throat velocity here against 0.6 at the inlet. Since false
    /// position keeps whichever end still brackets the root, the two can converge to the
    /// same answer by different paths - or, if the root lies between 0.6 and 0.8 of the
    /// throat velocity, the inlet version raises where this one succeeds.
    /// </remarks>
    public static double ExhaustSonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double cylinderSpeedOfSound) =>
        Solve(
            SonicResidual(gamma, dischargeCoefficient, areaRatio, cylinderSpeedOfSound),
            low: 0.000001 * throatVelocity,
            high: 0.8 * throatVelocity,
            what: "Exhaust Sonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// Throat velocity for subsonic flow reversing through the inlet valve. Port of
    /// <c>InlSubSonicVelSolve</c>.
    /// </summary>
    public static double InletSubsonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double throatSpeedOfSound, double cylinderSpeedOfSound) =>
        Solve(
            SubsonicResidual(
                gamma, dischargeCoefficient, areaRatio, throatVelocity, throatSpeedOfSound,
                cylinderSpeedOfSound),
            low: 0.000001 * throatVelocity,
            high: 0.99999 * throatVelocity,
            what: "Inlet Subsonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// Throat velocity for subsonic flow out of the exhaust valve. Port of
    /// <c>ExhSubSonicVelSolve</c>.
    /// </summary>
    /// <remarks>
    /// Character for character the same computation as <see cref="InletSubsonic"/> -
    /// same residual, same brackets, same tolerance - differing only in the text of the
    /// two error messages. See ISSUES.md B56.
    /// </remarks>
    public static double ExhaustSubsonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double throatSpeedOfSound, double cylinderSpeedOfSound) =>
        Solve(
            SubsonicResidual(
                gamma, dischargeCoefficient, areaRatio, throatVelocity, throatSpeedOfSound,
                cylinderSpeedOfSound),
            low: 0.000001 * throatVelocity,
            high: 0.99999 * throatVelocity,
            what: "Exhaust Subsonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// Mach number at a choked exhaust valve entrance, from the area-Mach relation. Port
    /// of <c>ExhSonicMachSolve</c>.
    /// </summary>
    public static double ExhaustSonicMach(
        double gamma, double dischargeCoefficient, double areaRatio) =>
        Solve(
            mach => (1 / dischargeCoefficient / areaRatio)
                    - (1 / mach
                       * ManifoldNumerics.Power(
                           2 / (gamma + 1) * (1 + ((gamma - 1) / 2 * mach * mach)),
                           (gamma + 1) / 2 / (gamma - 1))),
            low: 0.45 * areaRatio,
            high: 0.75 * areaRatio,
            what: "Exhaust Sonic Mach Solve(Reverse Flow)");

    private static Func<double, double> SonicResidual(
        double gamma, double dischargeCoefficient, double areaRatio, double cylinderSpeedOfSound) =>
        u => (u * u)
             - (ManifoldNumerics.Power(2 / (gamma + 1), 3.0 / 2)
                * ((1 / dischargeCoefficient / areaRatio) + gamma) * u * cylinderSpeedOfSound)
             + (cylinderSpeedOfSound * cylinderSpeedOfSound * (2 / (gamma + 1)));

    private static Func<double, double> SubsonicResidual(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double throatSpeedOfSound, double cylinderSpeedOfSound) =>
        u => (u * u)
             - (2 / (gamma + 1)
                * ((throatSpeedOfSound * throatSpeedOfSound
                    / throatVelocity / dischargeCoefficient / areaRatio)
                   + (gamma * throatVelocity))
                * u)
             + (cylinderSpeedOfSound * cylinderSpeedOfSound * (2 / (gamma + 1)));

    /// <summary>
    /// False position on a fixed bracket, as all five routines run it.
    /// </summary>
    /// <remarks>
    /// The bracket is re-evaluated from scratch on every pass rather than carried, and the
    /// sign test compares the new residual against the <b>low</b> end's, so the interval
    /// only ever narrows from one side at a time. That is the classical false-position
    /// method, retained rather than replaced by a bisection fallback, because which end
    /// moves changes the answer at the tolerance.
    /// </remarks>
    private static double Solve(Func<double, double> residual, double low, double high, string what)
    {
        var iterations = 0;
        double guess;
        double atGuess;

        do
        {
            var atLow = residual(low);
            var atHigh = residual(high);

            if (atLow * atHigh > 0)
            {
                throw new CfdException($"ERROR : fx1*fx2 > 0 in {what} !!!");
            }

            guess = high - (atHigh * (high - low) / (atHigh - atLow));
            atGuess = residual(guess);

            if ((atGuess > 0 && atLow > 0) || (atGuess < 0 && atLow < 0))
            {
                low = guess;
            }
            else
            {
                high = guess;
            }

            iterations++;
        }
        while (Math.Abs(atGuess) >= Tolerance && iterations <= MaxIterations);

        if (iterations > MaxIterations)
        {
            throw new CfdException($"ERROR : No convergence in {what} !!!");
        }

        return guess;
    }
}
