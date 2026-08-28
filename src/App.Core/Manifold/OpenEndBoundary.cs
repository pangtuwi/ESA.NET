using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// The open end of a pipe, where it meets the inlet plenum or the exhaust back pressure.
/// Port of <c>INFLOW_INLET_PIPE</c> and <c>OUTFLOW_EXHAUST_PIPE</c>
/// (Manifolds.pas:1535-1884).
/// </summary>
/// <remarks>
/// <para>
/// One characteristic reaches the boundary from inside the pipe, and the path line
/// carries entropy from the boundary point itself. That leaves one relation short, and
/// which condition supplies it depends on which way the gas is going:
/// </para>
/// <list type="bullet">
/// <item>
/// Gas <b>entering</b> the pipe is expanded isentropically from the reservoir's
/// stagnation state, so the boundary pressure and density follow from the velocity.
/// </item>
/// <item>
/// Gas <b>leaving</b> takes the reservoir's static pressure directly, and the velocity
/// follows from the characteristic.
/// </item>
/// </list>
/// <para>
/// The inlet and exhaust versions are mirror images of that, with the sense of "entering"
/// reversed because one boundary is the pipe's first point and the other its last.
/// </para>
/// </remarks>
public static class OpenEndBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;
    private const int MaxIterations = 1000;

    /// <summary>
    /// The inlet pipe's plenum end, its first point. Port of <c>INFLOW_INLET_PIPE</c>.
    /// </summary>
    public static void ApplyInlet(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry pipe,
        double dt,
        double plenumPressure,
        double plenumTemperature) =>
        Apply(
            current, target, pipe, CharacteristicSolver.InletGamma, dt,
            plenumPressure, plenumTemperature,
            boundary: 0,
            interior: 1,
            sign: -1,
            checksDensityForConvergence: false);

    /// <summary>
    /// The exhaust pipe's tailpipe end, its last point. Port of
    /// <c>OUTFLOW_EXHAUST_PIPE</c>.
    /// </summary>
    public static void ApplyExhaust(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry pipe,
        double dt,
        double backPressure,
        double backTemperature) =>
        Apply(
            current, target, pipe, CharacteristicSolver.ExhaustGamma, dt,
            backPressure, backTemperature,
            boundary: current.ActiveCount - 1,
            interior: current.ActiveCount - 2,
            sign: 1,
            checksDensityForConvergence: true);

    /// <param name="sign">+1 for the <c>C+</c> characteristic, -1 for <c>C-</c>.</param>
    /// <param name="checksDensityForConvergence">
    /// Whether density joins velocity and pressure in the convergence test. <b>The two
    /// routines disagree.</b> The outflow one tests all three; the inflow one tests only
    /// velocity and pressure, so it can stop while density is still moving. See
    /// ISSUES.md B55.
    /// </param>
    private static void Apply(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry pipe,
        double gamma,
        double dt,
        double reservoirPressure,
        double reservoirTemperature,
        int boundary,
        int interior,
        double sign,
        bool checksDensityForConvergence)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pipe);

        var x4 = current.X[boundary];
        var startingVelocity = current.Velocity[boundary];

        // As with the closed-valve pair, the two routines form the line in opposite
        // directions and anchor it on opposite points.
        var line = sign > 0
            ? GridInterpolants.Through(current, from: interior, to: boundary)
            : GridInterpolants.Through(current, from: boundary, to: interior);

        // The characteristic foot, starting at the interior neighbour.
        var waveX = current.X[interior];
        var waveU = current.Velocity[interior];
        var waveP = current.Pressure[interior];
        var waveR = current.Density[interior];

        // The path-line foot, starting at the boundary point.
        var pathX = current.X[boundary];
        var pathU = current.Velocity[boundary];
        var pathP = current.Pressure[boundary];
        var pathR = current.Density[boundary];

        double u4 = 0, p4 = 0, r4 = 0;
        double previousU = 0, previousP = 0, previousR = 0;
        double q = 0, t = 0, a0 = 0, t0 = 0;

        var iteration = 0;
        bool converged;

        do
        {
            // ---- The characteristic arriving from inside the pipe ----
            var waveFootIterations = 0;
            while (waveFootIterations++ <= MaxIterations)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (waveU, waveP, waveR);
                }

                var meanVelocity = (waveU + u4) / 2;
                var meanPressure = (waveP + p4) / 2;
                var meanDensity = (waveR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var lambda = 1 / (meanVelocity + (sign * c));
                var x = x4 - (dt / lambda);

                x = sign > 0 ? Math.Min(x, pipe.Length) : Math.Max(x, 0);

                if (Math.Abs(x - waveX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, waveR, waveU, diameter, c);

                    q = meanDensity * c;

                    var source = (-waveR * waveU * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * waveU - (sign * c))
                                    * (waveR * waveU * Math.Abs(waveU) * 2 * friction / diameter));

                    t = waveP + (sign * q * waveU) + (source * dt);
                    break;
                }

                waveX = x;
                waveU = line.VelocityAt(x);
                waveP = line.PressureAt(x);
                waveR = line.DensityAt(x);
            }

            if (waveFootIterations > MaxIterations + 1)
            {
                throw new CfdException("ERROR : No convergence in open-end characteristic foot !!!");
            }

            // ---- The path line ----
            var pathFootIterations = 0;
            while (pathFootIterations++ <= MaxIterations)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (pathU, pathP, pathR);
                }

                var meanVelocity = (pathU + u4) / 2;
                var meanPressure = (pathP + p4) / 2;
                var meanDensity = (pathR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var x = Math.Abs(meanVelocity) < 1E-8 ? x4 : x4 - (dt / (1 / meanVelocity));

                x = sign > 0 ? Math.Min(x, pipe.Length) : Math.Max(x, 0);

                if (Math.Abs(x - pathX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, pathR, pathU, diameter, c);

                    a0 = c * c;

                    var b0 = (gamma - 1)
                             * (pathR * pathU * Math.Abs(pathU) * 2 * friction / diameter);

                    t0 = (b0 * (x4 - x)) + pathP - (a0 * pathR);
                    break;
                }

                pathX = x;
                pathU = line.VelocityAt(x);
                pathP = line.PressureAt(x);
                pathR = line.DensityAt(x);
            }

            if (pathFootIterations > MaxIterations + 1)
            {
                throw new CfdException("ERROR : No convergence in open-end path foot !!!");
            }

            // ---- Close the system, according to which way the gas is going ----
            var velocityToTest = iteration == 0 ? startingVelocity : u4;
            var leavingThePipe = sign < 0 ? velocityToTest < 0 : velocityToTest >= 0;

            if (leavingThePipe)
            {
                // The reservoir's static pressure is imposed and the characteristic gives
                // the velocity.
                p4 = reservoirPressure;
                u4 = sign * (t - p4) / q;
                r4 = (p4 - t0) / a0;
            }
            else
            {
                // Expanded isentropically out of the reservoir's stagnation state. The
                // velocity from the previous pass sets the static temperature, which sets
                // pressure and density, and the characteristic then revises the velocity.
                if (iteration == 0)
                {
                    u4 = startingVelocity;
                }

                var t4 = reservoirTemperature - ((gamma - 1) / 2 / gamma * u4 * u4 / 287);
                p4 = reservoirPressure
                     * ManifoldNumerics.Power(t4 / reservoirTemperature, gamma / (gamma - 1));
                r4 = p4 / (287 * t4);
                u4 = sign * (t - p4) / q;
            }

            converged = iteration != 0
                        && Math.Abs(u4 - previousU) < VelocityTolerance
                        && Math.Abs(p4 - previousP) < PressureTolerance
                        && (!checksDensityForConvergence
                            || Math.Abs(r4 - previousR) < DensityTolerance);

            previousU = u4;
            previousP = p4;
            previousR = r4;
            iteration++;

            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        target.Velocity[boundary] = u4;
        target.Pressure[boundary] = p4;
        target.Density[boundary] = r4;
        target.SpeedOfSound[boundary] = Math.Sqrt(gamma * p4 / r4);
    }
}
