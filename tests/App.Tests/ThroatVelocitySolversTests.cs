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
}
