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
    /// The upper bracket, as a fraction of the throat velocity, that covers every entrance
    /// velocity below it. The subsonic solvers always used it; under B56 the sonic ones do.
    /// </summary>
    private const double WholeSubsonicRange = 0.99999;

    /// <summary>
    /// Throat velocity for choked flow reversing through the inlet valve. Port of
    /// <c>InlSonicVelSolve</c>.
    /// </summary>
    /// <param name="wholeSubsonicRange">
    /// ISSUES.md B56: bracket the root over the whole range below the throat velocity, as
    /// <see cref="Sonic"/> explains, rather than the original's 0.6 of it.
    /// </param>
    public static double InletSonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double cylinderSpeedOfSound, bool wholeSubsonicRange = false, bool fromZero = false) =>
        Sonic(
            gamma, dischargeCoefficient, areaRatio, throatVelocity, cylinderSpeedOfSound,
            upperFraction: wholeSubsonicRange ? WholeSubsonicRange : 0.6,
            fromZero,
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
    /// <param name="wholeSubsonicRange">
    /// ISSUES.md B56: bracket the root over the whole range below the throat velocity
    /// rather than the original's 0.8 of it.
    /// </param>
    public static double ExhaustSonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double cylinderSpeedOfSound, bool wholeSubsonicRange = false, bool fromZero = false) =>
        Sonic(
            gamma, dischargeCoefficient, areaRatio, throatVelocity, cylinderSpeedOfSound,
            upperFraction: wholeSubsonicRange ? WholeSubsonicRange : 0.8,
            fromZero,
            what: "Exhaust Sonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// The one sonic entrance-velocity solve both valves share.
    /// </summary>
    /// <remarks>
    /// The residual is <c>u^2 - b u + c</c> with <c>c = a0^2 2/(gamma+1)</c>, the critical
    /// speed of sound squared, so its two roots multiply to <c>a*^2</c>. When the throat is
    /// choked its velocity is <c>a*</c> itself, so exactly one root lies below the throat
    /// velocity - the subsonic entrance velocity the valve passes into the pipe - and the
    /// other above it. The bracket that finds the physical root, and only it, is therefore
    /// the whole range below the throat velocity, which is what B56 uses for both valves.
    /// The original's 0.6 and 0.8 are narrower than that by different amounts: a root
    /// between 0.6 and 0.8 of the throat velocity raises at the inlet and is found at the
    /// exhaust, and one above 0.8 raises at both.
    /// </remarks>
    private static double Sonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double cylinderSpeedOfSound, double upperFraction, bool fromZero, string what) =>
        Solve(
            SonicResidual(gamma, dischargeCoefficient, areaRatio, cylinderSpeedOfSound),
            low: LowerBracket(throatVelocity, fromZero),
            high: upperFraction * throatVelocity,
            what: what);

    /// <summary>
    /// Throat velocity for subsonic flow reversing through the inlet valve. Port of
    /// <c>InlSubSonicVelSolve</c>.
    /// </summary>
    public static double InletSubsonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double throatSpeedOfSound, double cylinderSpeedOfSound, bool fromZero = false) =>
        Subsonic(
            gamma, dischargeCoefficient, areaRatio, throatVelocity, throatSpeedOfSound,
            cylinderSpeedOfSound, fromZero, what: "Inlet Subsonic Velocity Solve(Reverse Flow)");

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
        double throatSpeedOfSound, double cylinderSpeedOfSound, bool fromZero = false) =>
        Subsonic(
            gamma, dischargeCoefficient, areaRatio, throatVelocity, throatSpeedOfSound,
            cylinderSpeedOfSound, fromZero, what: "Exhaust Subsonic Velocity Solve(Reverse Flow)");

    /// <summary>
    /// The one subsonic entrance-velocity solve both valves share. The original wrote it
    /// twice, differing only in the error message (ISSUES.md B56).
    /// </summary>
    private static double Subsonic(
        double gamma, double dischargeCoefficient, double areaRatio, double throatVelocity,
        double throatSpeedOfSound, double cylinderSpeedOfSound, bool fromZero, string what) =>
        Solve(
            SubsonicResidual(
                gamma, dischargeCoefficient, areaRatio, throatVelocity, throatSpeedOfSound,
                cylinderSpeedOfSound),
            low: LowerBracket(throatVelocity, fromZero),
            high: WholeSubsonicRange * throatVelocity,
            what: what);

    /// <summary>
    /// Mach number at a choked exhaust valve entrance, from the area-Mach relation. Port
    /// of <c>ExhSonicMachSolve</c>.
    /// </summary>
    /// <remarks>
    /// The subsonic root of <c>1/(Cd AR) = A/A*(M)</c> satisfies <c>M = g(M) Cd AR</c>
    /// with <c>g(M) = [2/(gamma+1) (1 + (gamma-1)/2 M^2)]^((gamma+1)/(2(gamma-1)))</c>,
    /// which rises from <c>g(0)</c> to 1 at Mach 1, so the root lies between
    /// <c>m0 = g(0) Cd AR</c> and 1 whenever <c>Cd AR</c> is at most 1. The original
    /// brackets it on 0.45 to 0.75 of the area ratio, which holds the root only for a
    /// discharge coefficient between about 0.78 and 1.29; below that the solve stops.
    /// Under B91 the bracket is <c>m0</c> to 1 (ISSUES.md B91).
    /// </remarks>
    /// <param name="analyticBracket">ISSUES.md B91: bracket the root on <c>[m0, 1]</c>.</param>
    public static double ExhaustSonicMach(
        double gamma, double dischargeCoefficient, double areaRatio, bool analyticBracket = false)
    {
        var exponent = (gamma + 1) / 2 / (gamma - 1);

        double Residual(double mach) =>
            (1 / dischargeCoefficient / areaRatio)
            - (1 / mach
               * ManifoldNumerics.Power(2 / (gamma + 1) * (1 + ((gamma - 1) / 2 * mach * mach)), exponent));

        var (low, high) = analyticBracket
            ? (ManifoldNumerics.Power(2 / (gamma + 1), exponent) * dischargeCoefficient * areaRatio, 1.0)
            : (0.45 * areaRatio, 0.75 * areaRatio);

        return Solve(Residual, low, high, what: "Exhaust Sonic Mach Solve(Reverse Flow)");
    }

    /// <summary>
    /// Entrance velocity of choked flow from the cylinder into a pipe that satisfies
    /// continuity with the sonic throat, energy, and the characteristic arriving from the pipe.
    /// No Delphi counterpart: ISSUES.md B94.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original takes the entrance velocity from <see cref="ExhaustSonic"/> or
    /// <see cref="InletSonic"/>, whose residual is the sudden expansion's momentum balance
    /// with the throat pressure on the base. Continuity, energy and that momentum balance fix
    /// the entrance state from the cylinder alone; the boundary then also puts the pressure
    /// on the pipe's characteristic, a fourth relation for three unknowns, and continuity is
    /// the one that gives. On the baseline engine's blowdown the pipe received a third to a
    /// half of the cylinder's outflow.
    /// </para>
    /// <para>
    /// With the throat choked the cylinder cannot feel the pipe, so the closure is continuity
    /// and energy against the sonic throat plus the characteristic: the entrance pressure
    /// <c>t + q u</c> on the characteristic equals the pressure continuity needs,
    /// <c>Cd Ar p0 (2/(gamma+1))^((gamma+1)/(2(gamma-1))) (1 - (gamma-1)/2 (u/a0)^2) / (u/a0)</c>,
    /// with <c>a0</c> the cylinder's speed of sound. The first rises with <c>u</c> and the
    /// second falls, from infinity at zero to <c>Cd Ar p*</c> at the critical speed
    /// <c>a* = a0 sqrt(2/(gamma+1))</c>, where the entrance itself would be sonic; so there is
    /// one root below <c>a*</c>, found by bisection, or none, when the pipe's pressure is too low
    /// for a subsonic entrance, and then the entrance is sonic. Momentum across the expansion
    /// is then met by the jet's mixing loss, which the entrance entropy carries.
    /// </para>
    /// </remarks>
    /// <param name="characteristicIntercept">
    /// <c>t</c>: the entrance pressure on the arriving characteristic at zero velocity.
    /// </param>
    /// <param name="characteristicSlope">
    /// <c>q</c>: the characteristic's rise in pressure per unit of entrance velocity.
    /// </param>
    public static double ChokedEntranceOnCharacteristic(
        double gamma, double dischargeCoefficient, double areaRatio, double cylinderPressure,
        double cylinderSpeedOfSound, double characteristicIntercept, double characteristicSlope)
    {
        var flowFactor = dischargeCoefficient * areaRatio * cylinderPressure
                         * ManifoldNumerics.Power(2 / (gamma + 1), (gamma + 1) / 2 / (gamma - 1));

        double Residual(double u)
        {
            var ratio = u / cylinderSpeedOfSound;
            var continuity = flowFactor * (1 - ((gamma - 1) / 2 * ratio * ratio)) / ratio;
            return characteristicIntercept + (characteristicSlope * u) - continuity;
        }

        var high = cylinderSpeedOfSound * Math.Sqrt(2 / (gamma + 1));

        if (Residual(high) <= 0)
        {
            return high;
        }

        var low = 0.0;

        for (var i = 0; i < 200 && high - low > Tolerance * high; i++)
        {
            var middle = (low + high) / 2;

            if (middle == 0 || Residual(middle) < 0)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// The lower end of the sonic and subsonic brackets: the original's millionth of the
    /// throat velocity, or under ISSUES.md B85 zero.
    /// </summary>
    /// <remarks>
    /// Both residuals are <c>u^2 - b u + c</c> with <c>c = 2 a^2/(gamma+1)</c>, positive, so
    /// the residual is positive at zero and the physical root is the smaller one, about
    /// <c>c/b</c>. And <c>b</c> grows as the inverse of the discharge coefficient times the
    /// area ratio, so as a valve shuts the root falls towards zero: once their product is
    /// below about a millionth it falls below the original's lower end, both ends have the
    /// same sign, and the solve stops. From zero the bracket holds the root however nearly
    /// shut the valve is.
    /// </remarks>
    private static double LowerBracket(double throatVelocity, bool fromZero) =>
        fromZero ? 0 : 0.000001 * throatVelocity;

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
