using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// The pipe end at a shut valve: a solid wall. Port of <c>INLET_VALVE_CLOSED</c> and
/// <c>EXHAUST_VALVE_CLOSED</c> (Manifolds.pas:2357-2648).
/// </summary>
/// <remarks>
/// <para>
/// Only one characteristic reaches a wall from inside the pipe - <c>C+</c> at the inlet's
/// far end, <c>C-</c> at the exhaust's near end - so velocity cannot be solved for and is
/// imposed instead. Pressure follows from the single compatibility relation and density
/// from the path line, and the iteration converges on those two alone.
/// </para>
/// <para>
/// The two routines are mirror images with one real difference, described on
/// <see cref="Apply"/>.
/// </para>
/// </remarks>
public static class ClosedValveBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;
    private const int MaxIterations = 1000;

    /// <summary>Applies the wall condition at the inlet pipe's valve end, its last point.</summary>
    public static void ApplyInlet(
        PipeGrid current, PipeGrid target, PipeGeometry pipe, double dt, double wallVelocity = 0) =>
        Apply(
            current, target, pipe, CharacteristicSolver.InletGamma, dt, wallVelocity,
            wall: current.ActiveCount - 1,
            interior: current.ActiveCount - 2,
            sign: 1,
            interpolantUsesWallVelocity: false);

    /// <summary>Applies the wall condition at the exhaust pipe's valve end, its first point.</summary>
    public static void ApplyExhaust(
        PipeGrid current, PipeGrid target, PipeGeometry pipe, double dt, double wallVelocity = 0) =>
        Apply(
            current, target, pipe, CharacteristicSolver.ExhaustGamma, dt, wallVelocity,
            wall: 0,
            interior: 1,
            sign: -1,
            interpolantUsesWallVelocity: true);

    /// <summary>
    /// Solves the wall boundary at <paramref name="wall"/> using the characteristic
    /// arriving from <paramref name="interior"/>.
    /// </summary>
    /// <param name="sign">
    /// +1 for the <c>C+</c> characteristic, -1 for <c>C-</c>. It flips the wave speed, the
    /// sign of the friction term's velocity-and-sound factor, and how the compatibility
    /// relation is rearranged for pressure.
    /// </param>
    /// <param name="interpolantUsesWallVelocity">
    /// Whether the linear interpolant is built from the imposed wall velocity or from the
    /// velocity stored at the wall point. <b>The two routines disagree here.</b> The inlet
    /// version builds its interpolant from the two stored grid velocities; the exhaust
    /// version substitutes the imposed wall velocity for the stored one. They differ
    /// whenever the wall point still carries a velocity from when the valve was open,
    /// which is exactly the step after it shuts. See ISSUES.md B54.
    /// </param>
    private static void Apply(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry pipe,
        double gamma,
        double dt,
        double wallVelocity,
        int wall,
        int interior,
        double sign,
        bool interpolantUsesWallVelocity)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pipe);

        var wallX = current.X[wall];
        var wallPressure = current.Pressure[wall];
        var wallDensity = current.Density[wall];

        var interpolantWallVelocity =
            interpolantUsesWallVelocity ? wallVelocity : current.Velocity[wall];

        // The two routines form the interpolant in opposite directions and anchor the
        // intercept on opposite points. Algebraically it is the same straight line either
        // way; in floating point it is not, so each is built the way its own routine
        // builds it.
        var line = sign > 0
            ? GridInterpolants.Through(current, from: interior, to: wall)
            : GridInterpolants.Through(
                current, from: wall, to: interior, velocityOverrideAt: wall,
                velocityOverride: interpolantWallVelocity);

        // Point 4, the new state at the wall. Its position drifts with the imposed wall
        // velocity, which Main_Prog always passes as zero, so in practice it sits on the
        // wall itself.
        var pathVelocity = (wallVelocity + wallVelocity) / 2;
        var x4 = Math.Abs(pathVelocity) > 1E-8 ? wallX + (dt / (1 / pathVelocity)) : wallX;

        var footX = current.X[interior];
        var footU = current.Velocity[interior];
        var footP = current.Pressure[interior];
        var footR = current.Density[interior];

        double u4 = 0, p4 = 0, r4 = 0;
        double previousP = 0, previousR = 0;
        double q = 0, t = 0;

        var iteration = 0;
        bool converged;

        do
        {
            // ---- The one characteristic that reaches the wall ----
            while (true)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (footU, footP, footR);
                }

                var meanVelocity = (footU + u4) / 2;
                var meanPressure = (footP + p4) / 2;
                var meanDensity = (footR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var lambda = 1 / (meanVelocity + (sign * c));
                var x = x4 - (dt / lambda);

                // Defensive in the original and inert in practice: the foot always lands
                // inside the pipe, because it is travelling away from the wall.
                x = sign > 0 ? Math.Min(x, pipe.Length) : Math.Max(x, 0);

                if (Math.Abs(x - footX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, footR, footU, diameter, c);

                    q = meanDensity * c;

                    var source = (-footR * footU * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * footU - (sign * c))
                                    * (footR * footU * Math.Abs(footU) * 2 * friction / diameter));

                    t = footP + (sign * q * footU) + (source * dt);
                    break;
                }

                footX = x;
                footU = line.VelocityAt(x);
                footP = line.PressureAt(x);
                footR = line.DensityAt(x);
            }

            // ---- The path line, carrying entropy off the wall itself ----
            if (iteration == 0)
            {
                (u4, p4, r4) = (wallVelocity, wallPressure, wallDensity);
            }

            var pathPressure = (wallPressure + p4) / 2;
            var pathDensity = (wallDensity + r4) / 2;
            var pathC = ManifoldNumerics.SpeedOfSound(gamma, pathPressure, pathDensity);

            var pathDiameter = Math.Sqrt(4 * pipe.Area(wallX) / Math.PI);
            var pathFriction =
                ManifoldNumerics.FanningFriction(gamma, wallDensity, wallVelocity, pathDiameter, pathC);

            var a0 = pathC * pathC;
            var b0 = (gamma - 1)
                     * (wallDensity * wallVelocity * Math.Abs(wallVelocity) * 2 * pathFriction / pathDiameter);
            var t0 = (b0 * (x4 - wallX)) + wallPressure - (a0 * wallDensity);

            // ---- Velocity is imposed, so only pressure and density are solved ----
            u4 = wallVelocity;
            p4 = t - (sign * q * u4);
            r4 = (p4 - t0) / a0;

            converged = iteration != 0
                        && Math.Abs(p4 - previousP) < PressureTolerance
                        && Math.Abs(r4 - previousR) < DensityTolerance;

            previousP = p4;
            previousR = r4;
            iteration++;

            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        target.Velocity[wall] = u4;
        target.Pressure[wall] = p4;
        target.Density[wall] = r4;
        target.SpeedOfSound[wall] = Math.Sqrt(gamma * p4 / r4);
    }
}
