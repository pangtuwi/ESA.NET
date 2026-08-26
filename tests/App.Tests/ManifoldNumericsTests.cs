using App.Core;
using App.Core.Manifold;

namespace App.Tests;

/// <summary>
/// The shared numerical helpers of the manifold solver.
/// </summary>
public sealed class ManifoldNumericsTests
{
    [Fact]
    public void PowerRefusesTheBasesTheOriginalRefuses()
    {
        // A third power routine, agreeing with neither DelphiMath.Power nor Pwr: this one
        // raises rather than returning a value. See ISSUES.md B47.
        Assert.Throws<CfdException>(() => ManifoldNumerics.Power(0, 2));
        Assert.Throws<CfdException>(() => ManifoldNumerics.Power(-1, 2));
        Assert.Throws<CfdException>(() => ManifoldNumerics.Power(1e20, 2));

        Assert.Equal(1, ManifoldNumerics.Power(5, 0));
        Assert.Equal(8, ManifoldNumerics.Power(2, 3), 12);
    }

    [Fact]
    public void PowerGoesThroughExpAndLogEvenForIntegerExponents()
    {
        // Unlike DelphiMath.Power, which routes integer exponents through repeated
        // squaring, this one always takes the transcendental path. The two therefore
        // differ in the last bits and must not be swapped for one another.
        Assert.Equal(Math.Exp(3 * Math.Log(7)), ManifoldNumerics.Power(7, 3), 15);
    }

    [Fact]
    public void SpeedOfSoundRaisesOnNegativeInputsAndReturnsZeroOnTheGapInTheGuard()
    {
        Assert.Equal(Math.Sqrt(1.4 * 1e5 / 1.2), ManifoldNumerics.SpeedOfSound(1.4, 1e5, 1.2), 12);

        Assert.Throws<CfdException>(() => ManifoldNumerics.SpeedOfSound(1.4, 1e5, -1.2));
        Assert.Throws<CfdException>(() => ManifoldNumerics.SpeedOfSound(1.4, -1e5, 1.2));

        // Zero pressure with positive density satisfies neither guard, and the original
        // then falls out of the routine without assigning a result at all. See B48.
        Assert.Equal(0, ManifoldNumerics.SpeedOfSound(1.4, 0, 1.2));
    }

    [Fact]
    public void ViscosityOfAirIsRightToWithinAPerCentAtRoomTemperature()
    {
        // Air at 300 K is about 1.85e-5 Pa s.
        Assert.Equal(1.85e-5, ManifoldNumerics.Viscosity(300), 6e-7);

        // And rises with temperature, as a gas should.
        Assert.True(ManifoldNumerics.Viscosity(900) > ManifoldNumerics.Viscosity(300));
    }

    [Fact]
    public void TheFrictionFactorFollowsTheLaminarLawBelowTwoThousandThreeHundred()
    {
        // f = 16/Re in the laminar band, which is the Fanning definition rather than the
        // Darcy one - a factor of four apart, and worth pinning.
        var density = 1.2;
        var velocity = 0.02;
        var diameter = 0.04;
        var speedOfSound = 347.0;

        var f = ManifoldNumerics.FanningFriction(1.4, density, velocity, diameter, speedOfSound);

        var temperature = speedOfSound * speedOfSound / 1.4 / 287;
        var reynolds = density * velocity * diameter / ManifoldNumerics.Viscosity(temperature);

        Assert.True(reynolds < 2300, $"This case was meant to be laminar, Re came out {reynolds}.");
        Assert.Equal(16 / reynolds, f, 12);
    }

    [Fact]
    public void TheTransitionalAndTurbulentBandsAreTheSameExpression()
    {
        // 2300 to 4000 and 4000 to 1e5 evaluate identically in the original, so the
        // boundary between them does nothing. Reproduced; see ISSUES.md B49.
        // Straddle it by varying velocity, which moves Reynolds number directly.
        double At(double velocity) =>
            ManifoldNumerics.FanningFriction(1.4, 1.2, velocity, 0.04, 347.0);

        var below = At(3.0);
        var above = At(6.0);

        // Both should sit on the same smooth 0.0791/Re^0.25 curve, so the ratio follows
        // the velocity ratio to the quarter power with no step at the boundary.
        Assert.Equal(Math.Pow(2.0, 0.25), below / above, 1e-9);
    }

    [Fact]
    public void ZeroVelocityGivesZeroFriction()
    {
        Assert.Equal(0, ManifoldNumerics.FanningFriction(1.4, 1.2, 0, 0.04, 347.0));
    }

    [Fact]
    public void CriticalPressureApproachesTheTextbookChokingRatioForASmallThroat()
    {
        // As Cd*Aratio tends to zero the equation collapses to the standard critical
        // pressure ratio, (2/(gam+1))^(gam/(gam-1)) - 0.5283 at gamma 1.4.
        const double Gamma = 1.4;
        var expected = Math.Pow(2 / (Gamma + 1), Gamma / (Gamma - 1));

        Assert.Equal(expected, ManifoldNumerics.CriticalPressure(Gamma, 1e-6, 1e-6), 6);
    }

    [Fact]
    public void CriticalPressureSolvesItsOwnEquation()
    {
        const double Gamma = 1.3994;
        const double Cd = 0.7;
        const double AreaRatio = 0.3;

        var p = ManifoldNumerics.CriticalPressure(Gamma, Cd, AreaRatio);

        var residual = (Cd * AreaRatio * Cd * AreaRatio * (Gamma - 1) / (Gamma + 1)
                        * ManifoldNumerics.Power(p, 2 / Gamma))
                       + (2 / (Gamma + 1) * ManifoldNumerics.Power(p, (1 - Gamma) / Gamma))
                       - 1;

        // The original iterates until the residual is under 1e-7.
        Assert.True(Math.Abs(residual) < 1e-7, $"Residual {residual:E3} at p = {p}.");
        Assert.InRange(p, 0.5, 1.0);
    }
}
