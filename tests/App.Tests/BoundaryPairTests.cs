using App.Core;
using App.Core.Manifold;
using App.Core.Model;

namespace App.Tests;

/// <summary>
/// ISSUES.md B54-B56: three pairs of wave-solver routines that should be one routine but
/// disagree. Each correction makes the inlet side agree with the exhaust side, and each is
/// checked against what the boundary physically has to do rather than against the
/// original.
/// </summary>
public sealed class BoundaryPairTests
{
    private const double Gamma = 1.4;
    private const double Pressure = 100000;
    private const double Temperature = 300;

    /// <summary>The length of the grid laid along the pipe, in metres.</summary>
    private const double GridLength = 0.4;

    /// <summary>
    /// A straight pipe of 1000 mm², so neither end sees an area change. The table runs on
    /// past the grid, because the area lookup falls to zero a hair beyond its last entry
    /// (ISSUES.md B4) and the last grid point can land there in floating point.
    /// </summary>
    private static PipeGeometry UniformPipe()
    {
        var table = new ManifoldAreaTable { Count = 2 };
        table.Position[0] = 0;
        table.Position[1] = 410;
        table.Area[0] = 1000;
        table.Area[1] = 1000;

        return new PipeGeometry(table);
    }

    private static (PipeGrid Current, PipeGrid Next) Grids(int points)
    {
        var current = new PipeGrid(EsaLimits.InletGridPoints);
        var next = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(current, points, GridLength, Pressure, Temperature, Gamma);
        PipeGridInitialiser.Initialise(next, points, GridLength, Pressure, Temperature, Gamma);

        return (current, next);
    }

    /// <summary>
    /// A time step that carries a sound wave half a grid spacing, so the characteristic
    /// foot lands between grid points and the interpolant is actually used.
    /// </summary>
    private static double HalfCellStep(PipeGrid grid) =>
        0.5 * (grid.X[1] - grid.X[0]) / Math.Sqrt(Gamma * 287 * Temperature);

    /// <summary>
    /// B54's oracle. A shut valve imposes zero velocity at the wall, so the new wall state
    /// cannot depend on whatever velocity the wall point still holds from when the valve
    /// was open. The original's inlet routine builds its interpolant from that stale value.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AShutInletValveForgetsTheVelocityItHadWhileOpen(bool corrected)
    {
        var pipe = UniformPipe();

        PipeGrid Shut(double staleWallVelocity)
        {
            var (current, next) = Grids(21);
            current.Velocity[20] = staleWallVelocity;

            ClosedValveBoundary.ApplyInlet(
                current, next, pipe, HalfCellStep(current), gamma: Gamma,
                imposedWallVelocityInInterpolant: corrected);

            return next;
        }

        var stale = Shut(30);
        var clean = Shut(0);

        if (corrected)
        {
            Assert.Equal(clean.Pressure[20], stale.Pressure[20]);
            Assert.Equal(clean.Density[20], stale.Density[20]);
        }
        else
        {
            Assert.NotEqual(clean.Pressure[20], stale.Pressure[20]);
        }

        // The wall velocity itself is imposed either way.
        Assert.Equal(0, stale.Velocity[20]);
    }

    /// <summary>
    /// B55's oracle: symmetric boundaries stay symmetric. Gas leaving a straight pipe into
    /// the same reservoir through either end, from mirrored states, has to arrive at the
    /// mirrored boundary state. With the correction both ends apply the same convergence
    /// test, so only floating-point construction separates them.
    /// </summary>
    /// <remarks>
    /// The original agrees here too, and on the baseline engine B55 changes nothing to the
    /// last bit: converging pressure to 1e-3 Pa already pins density to about 1e-8 kg/m³,
    /// far inside its own 1e-4 tolerance. The two ends now say so, rather than relying on
    /// it (ISSUES.md B55).
    /// </remarks>
    [Fact]
    public void GasLeavingEitherOpenEndArrivesAtTheMirroredState()
    {
        var pipe = UniformPipe();
        const int points = 21;

        var (inletCurrent, inletNext) = Grids(points);
        var (exhaustCurrent, exhaustNext) = Grids(points);

        // A gentle gradient pushing gas out of the end, the inlet's at x = 0 and the
        // exhaust's at x = L, so the reservoir-pressure (outflow) branch runs at both.
        for (var i = 0; i < points; i++)
        {
            var fromEnd = (double)i / (points - 1);
            var velocity = 20 * (1 - fromEnd);
            var pressure = Pressure + (2000 * (1 - fromEnd));
            var density = pressure / 287 / Temperature;

            var mirrored = points - 1 - i;

            inletCurrent.Velocity[i] = -velocity;
            inletCurrent.Pressure[i] = pressure;
            inletCurrent.Density[i] = density;

            exhaustCurrent.Velocity[mirrored] = velocity;
            exhaustCurrent.Pressure[mirrored] = pressure;
            exhaustCurrent.Density[mirrored] = density;
        }

        var dt = HalfCellStep(inletCurrent);

        OpenEndBoundary.ApplyInlet(
            inletCurrent, inletNext, pipe, dt, Pressure, Temperature, Gamma, checksDensity: true);
        OpenEndBoundary.ApplyExhaust(
            exhaustCurrent, exhaustNext, pipe, dt, Pressure, Temperature, Gamma);

        Assert.True(inletNext.Velocity[0] < 0, "Gas should be leaving through the inlet end.");
        Assert.Equal(-exhaustNext.Velocity[points - 1], inletNext.Velocity[0], 9);
        Assert.Equal(exhaustNext.Pressure[points - 1], inletNext.Pressure[0], 6);
        Assert.Equal(exhaustNext.Density[points - 1], inletNext.Density[0], 9);
    }

    /// <summary>
    /// B56's oracle. The sonic entrance velocity solves <c>u^2 - b u + a*^2 = 0</c>, whose
    /// smaller root is the physical one. Chosen here so that root sits at about 0.7 of the
    /// choked throat velocity <c>a*</c>: between the original's two brackets.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothValvesFindTheSubsonicRootWhereverItLiesBelowTheThroat(bool corrected)
    {
        const double cylinderSpeedOfSound = 500;
        const double dischargeCoefficient = 0.8666;
        const double areaRatio = 1.0;

        var criticalSpeed = cylinderSpeedOfSound * Math.Sqrt(2 / (Gamma + 1));

        // The residual's coefficients, and its smaller root in the cancellation-free form.
        var b = Math.Pow(2 / (Gamma + 1), 1.5)
                * ((1 / dischargeCoefficient / areaRatio) + Gamma) * cylinderSpeedOfSound;
        var c = criticalSpeed * criticalSpeed;
        var root = 2 * c / (b + Math.Sqrt((b * b) - (4 * c)));

        Assert.InRange(root / criticalSpeed, 0.65, 0.75);

        var exhaust = ThroatVelocitySolvers.ExhaustSonic(
            Gamma, dischargeCoefficient, areaRatio, criticalSpeed, cylinderSpeedOfSound, corrected);
        Assert.Equal(root, exhaust, 6);

        if (corrected)
        {
            var inlet = ThroatVelocitySolvers.InletSonic(
                Gamma, dischargeCoefficient, areaRatio, criticalSpeed, cylinderSpeedOfSound,
                wholeSubsonicRange: true);
            Assert.Equal(root, inlet, 6);
        }
        else
        {
            // The original's inlet bracket stops at 0.6 of the throat velocity, so the
            // root is outside it and the run would stop here.
            Assert.Throws<CfdException>(() => ThroatVelocitySolvers.InletSonic(
                Gamma, dischargeCoefficient, areaRatio, criticalSpeed, cylinderSpeedOfSound));
        }
    }
}
