using App.Core;
using App.Core.Manifold;

namespace App.Tests;

/// <summary>
/// The five root finders used at an open valve.
/// </summary>
/// <remarks>
/// Each solves its own residual, so the sharpest available check is that the returned
/// root actually zeroes it. The equations themselves come from compressible flow through
/// a restriction and have known limiting behaviour worth pinning as well.
/// </remarks>
public sealed class ThroatVelocitySolversTests
{
    private const double Gamma = 1.3994;
    private const double ExhaustGamma = 1.3;

    [Fact]
    public void TheSonicSolverReturnsARootOfItsOwnEquation()
    {
        const double Cd = 0.7;
        const double AreaRatio = 0.25;
        const double ThroatVelocity = 300;
        const double CylinderSpeedOfSound = 500;

        var u = ThroatVelocitySolvers.InletSonic(Gamma, Cd, AreaRatio, ThroatVelocity, CylinderSpeedOfSound);

        var residual = (u * u)
                       - (Math.Pow(2 / (Gamma + 1), 1.5) * ((1 / Cd / AreaRatio) + Gamma)
                          * u * CylinderSpeedOfSound)
                       + (CylinderSpeedOfSound * CylinderSpeedOfSound * (2 / (Gamma + 1)));

        Assert.True(Math.Abs(residual) < 1e-6, $"Residual {residual:E3} at u = {u}.");
        Assert.True(u > 0, $"Velocity came back as {u}.");
    }

    [Fact]
    public void TheSubsonicSolverReturnsARootOfItsOwnEquation()
    {
        const double Cd = 0.7;
        const double AreaRatio = 0.25;
        const double ThroatVelocity = 300;
        const double ThroatSpeedOfSound = 420;
        const double CylinderSpeedOfSound = 500;

        var u = ThroatVelocitySolvers.InletSubsonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, ThroatSpeedOfSound, CylinderSpeedOfSound);

        var residual = (u * u)
                       - (2 / (Gamma + 1)
                          * ((ThroatSpeedOfSound * ThroatSpeedOfSound / ThroatVelocity / Cd / AreaRatio)
                             + (Gamma * ThroatVelocity))
                          * u)
                       + (CylinderSpeedOfSound * CylinderSpeedOfSound * (2 / (Gamma + 1)));

        Assert.True(Math.Abs(residual) < 1e-6, $"Residual {residual:E3} at u = {u}.");
    }

    [Fact]
    public void TheTwoSubsonicSolversAreTheSameComputation()
    {
        const double Cd = 0.65;
        const double AreaRatio = 0.3;
        const double ThroatVelocity = 250;
        const double ThroatSpeedOfSound = 400;
        const double CylinderSpeedOfSound = 480;

        // Same residual, same brackets, same tolerance: the original duplicates the whole
        // routine and changes only the two error messages. See ISSUES.md B56.
        var inlet = ThroatVelocitySolvers.InletSubsonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, ThroatSpeedOfSound, CylinderSpeedOfSound);
        var exhaust = ThroatVelocitySolvers.ExhaustSubsonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, ThroatSpeedOfSound, CylinderSpeedOfSound);

        Assert.Equal(inlet, exhaust);
    }

    [Fact]
    public void TheTwoSonicSolversDifferOnlyInWhereTheyStartLooking()
    {
        const double Cd = 0.7;
        const double AreaRatio = 0.25;
        const double ThroatVelocity = 300;
        const double CylinderSpeedOfSound = 500;

        var inlet = ThroatVelocitySolvers.InletSonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, CylinderSpeedOfSound);
        var exhaust = ThroatVelocitySolvers.ExhaustSonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, CylinderSpeedOfSound);

        // The residual is identical and only the upper bracket differs, 0.6 against 0.8 of
        // the throat velocity, so both find the same root when it lies below both.
        Assert.Equal(inlet, exhaust, 6);
    }

    [Fact]
    public void ExhaustSonicMachSolvesTheAreaMachRelation()
    {
        const double Cd = 0.8;
        const double AreaRatio = 0.4;

        var mach = ThroatVelocitySolvers.ExhaustSonicMach(ExhaustGamma, Cd, AreaRatio);

        var residual = (1 / Cd / AreaRatio)
                       - (1 / mach
                          * Math.Pow(
                              2 / (ExhaustGamma + 1) * (1 + ((ExhaustGamma - 1) / 2 * mach * mach)),
                              (ExhaustGamma + 1) / 2 / (ExhaustGamma - 1)));

        Assert.True(Math.Abs(residual) < 1e-6, $"Residual {residual:E3} at M = {mach}.");

        // Subsonic branch of the area-Mach relation: a converging passage that chokes at
        // its throat runs below Mach 1 upstream of it.
        Assert.InRange(mach, 0, 1);
    }

    [Fact]
    public void ABracketThatDoesNotStraddleTheRootIsRefused()
    {
        // The original raises rather than widening the bracket or falling back, so a
        // case it cannot bracket stops the run. Cd and area ratio at their extremes push
        // the root outside the fixed interval.
        Assert.Throws<CfdException>(
            () => ThroatVelocitySolvers.InletSonic(Gamma, 1.0, 100.0, 300, 500));
    }

    /// <summary>The smaller root of <c>u^2 - b u + c</c>, written so that it does not cancel.</summary>
    private static double SmallRoot(double b, double c) => 2 * c / (b + Math.Sqrt((b * b) - (4 * c)));

    // ISSUES.md B85: the inlet valve's last open step before closing on the eight-valve
    // engine with cam B at 2000 rpm, under Corrected - a lift of half a micrometre, gas
    // flowing back out of the cylinder.
    private const double ShutGamma = 1.3523466604344605;
    private const double ShutCd = 0.010602190923317711;
    private const double ShutAreaRatio = 7.267307535169792E-05;

    /// <summary>
    /// B85's oracle for the subsonic solve. As the valve shuts the physical root, the
    /// smaller one, falls towards zero; here it lies below the original's lower bracket of
    /// a millionth of the throat velocity, and the original stops. From zero the solve
    /// returns the root, the closed form's to nine figures.
    /// </summary>
    [Fact]
    public void ANearlyShutValvesSubsonicRootIsFoundFromZero()
    {
        const double throatVelocity = 283.1040961574345;
        const double throatSpeedOfSound = 401.2444210511341;
        const double cylinderSpeedOfSound = 418.4698472203295;

        var b = 2 / (ShutGamma + 1)
                * ((throatSpeedOfSound * throatSpeedOfSound / throatVelocity / ShutCd / ShutAreaRatio)
                   + (ShutGamma * throatVelocity));
        var c = cylinderSpeedOfSound * cylinderSpeedOfSound * (2 / (ShutGamma + 1));
        var root = SmallRoot(b, c);

        Assert.True(root < 0.000001 * throatVelocity, $"{root}");
        Assert.Throws<CfdException>(() => ThroatVelocitySolvers.InletSubsonic(
            ShutGamma, ShutCd, ShutAreaRatio, throatVelocity, throatSpeedOfSound, cylinderSpeedOfSound));

        var u = ThroatVelocitySolvers.InletSubsonic(
            ShutGamma, ShutCd, ShutAreaRatio, throatVelocity, throatSpeedOfSound, cylinderSpeedOfSound,
            fromZero: true);

        Assert.Equal(root, u, root * 1e-9);
        Assert.Equal(u, ThroatVelocitySolvers.ExhaustSubsonic(
            ShutGamma, ShutCd, ShutAreaRatio, throatVelocity, throatSpeedOfSound, cylinderSpeedOfSound,
            fromZero: true));
    }

    /// <summary>B85's oracle for the sonic solve, from the same step one pass later, choked.</summary>
    [Fact]
    public void ANearlyShutValvesSonicRootIsFoundFromZero()
    {
        const double throatVelocity = 374.0975300802144;
        const double cylinderSpeedOfSound = 405.7144768309805;

        var b = Math.Pow(2 / (ShutGamma + 1), 1.5) * ((1 / ShutCd / ShutAreaRatio) + ShutGamma)
                * cylinderSpeedOfSound;
        var c = cylinderSpeedOfSound * cylinderSpeedOfSound * (2 / (ShutGamma + 1));
        var root = SmallRoot(b, c);

        Assert.True(root < 0.000001 * throatVelocity, $"{root}");
        Assert.Throws<CfdException>(() => ThroatVelocitySolvers.InletSonic(
            ShutGamma, ShutCd, ShutAreaRatio, throatVelocity, cylinderSpeedOfSound, wholeSubsonicRange: true));

        var u = ThroatVelocitySolvers.InletSonic(
            ShutGamma, ShutCd, ShutAreaRatio, throatVelocity, cylinderSpeedOfSound,
            wholeSubsonicRange: true, fromZero: true);

        Assert.Equal(root, u, root * 1e-9);
        Assert.InRange(u, 0, throatVelocity);
    }

    /// <summary>
    /// Where the original's bracket does hold the root, starting from zero finds the same
    /// one: both are the closed form's to nine figures.
    /// </summary>
    [Fact]
    public void AnOpenValvesRootIsTheSameFromZero()
    {
        const double Cd = 0.7;
        const double AreaRatio = 0.25;
        const double ThroatVelocity = 300;
        const double ThroatSpeedOfSound = 450;
        const double CylinderSpeedOfSound = 500;

        var original = ThroatVelocitySolvers.InletSubsonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, ThroatSpeedOfSound, CylinderSpeedOfSound);
        var fromZero = ThroatVelocitySolvers.InletSubsonic(
            Gamma, Cd, AreaRatio, ThroatVelocity, ThroatSpeedOfSound, CylinderSpeedOfSound, fromZero: true);

        Assert.Equal(original, fromZero, original * 1e-9);
    }

    private static double AreaMachResidual(double gamma, double cd, double areaRatio, double mach) =>
        (1 / cd / areaRatio)
        - (1 / mach * Math.Pow(2 / (gamma + 1) * (1 + ((gamma - 1) / 2 * mach * mach)), (gamma + 1) / 2 / (gamma - 1)));

    /// <summary>
    /// B91's oracle: the subsonic root of the area-Mach relation lies between
    /// <c>g(0) Cd AR</c> and 1, and the solve finds it across six decades of
    /// <c>Cd AR</c>, including the nearly shut valve it was found on: the eight-valve engine
    /// at 6000 rpm under B90, overlap at crank -329, Cd 0.0104 and area ratio 1.36e-4.
    /// </summary>
    [Theory]
    [InlineData(1.3302313921665512, 0.010416786570743464, 0.00013629486544891977)]
    [InlineData(ExhaustGamma, 0.5, 0.4)]
    [InlineData(ExhaustGamma, 0.8, 0.4)]
    [InlineData(ExhaustGamma, 0.3, 1e-3)]
    [InlineData(ExhaustGamma, 0.9, 0.99)]
    [InlineData(1.4, 1e-3, 1e-3)]
    public void UnderB91TheSonicMachSolveFindsTheSubsonicRoot(double gamma, double cd, double areaRatio)
    {
        var mach = ThroatVelocitySolvers.ExhaustSonicMach(gamma, cd, areaRatio, analyticBracket: true);
        var lowest = Math.Pow(2 / (gamma + 1), (gamma + 1) / 2 / (gamma - 1)) * cd * areaRatio;

        Assert.InRange(mach, lowest, 1);
        Assert.True(
            Math.Abs(AreaMachResidual(gamma, cd, areaRatio, mach)) * cd * areaRatio < 1e-6,
            $"{AreaMachResidual(gamma, cd, areaRatio, mach)} at {mach}");
    }

    /// <summary>
    /// The original's bracket, 0.45 to 0.75 of the area ratio, holds the root only for a
    /// discharge coefficient near 1, and stops the run on the nearly shut valve and on an
    /// ordinary one of 0.5.
    /// </summary>
    [Theory]
    [InlineData(1.3302313921665512, 0.010416786570743464, 0.00013629486544891977)]
    [InlineData(ExhaustGamma, 0.5, 0.4)]
    public void TheOriginalsSonicMachBracketMissesALowDischargeCoefficient(double gamma, double cd, double areaRatio)
    {
        Assert.Throws<CfdException>(() => ThroatVelocitySolvers.ExhaustSonicMach(gamma, cd, areaRatio));
    }
}
