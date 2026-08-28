using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// Advances one interior grid point by the method of characteristics. Port of
/// <c>INTERNAL_PIPE</c> (Manifolds.pas:1887-2356).
/// </summary>
/// <remarks>
/// <para>
/// Three characteristic lines are traced back from the new point: <c>C+</c> travelling at
/// <c>u + c</c>, <c>C-</c> at <c>u - c</c>, and the path line <c>C0</c> at <c>u</c>. Each
/// foot is located by fixed-point iteration against a piecewise-linear interpolant of the
/// current state, then the compatibility relations along the three lines are solved for
/// velocity, pressure and density at the new point. The whole thing repeats until the
/// three converge.
/// </para>
/// <para>
/// The original writes this out twice, once per pipe, and the two copies are identical
/// apart from the ratio of specific heats. They are one method here, parameterised by
/// gamma - which is the only thing that ever differed.
/// </para>
/// </remarks>
public static class CharacteristicSolver
{
    /// <summary>Ratio of specific heats the inlet pipe is hard-coded to use.</summary>
    public const double InletGamma = 1.3994;

    /// <summary>Ratio of specific heats the exhaust pipe is hard-coded to use.</summary>
    public const double ExhaustGamma = 1.3;

    /// <summary>Delphi <c>0.001*E4</c>: how close a characteristic foot must land, in metres.</summary>
    private const double FootTolerance = 0.001 * 0.1;

    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;

    private const int MaxIterations = 100;

    /// <summary>
    /// Computes the new state at interior point <paramref name="index"/> and writes it
    /// into <paramref name="target"/>.
    /// </summary>
    /// <param name="current">The pipe state at the start of the step, Delphi's un-suffixed arrays.</param>
    /// <param name="target">Where the new state goes, Delphi's <c>...New</c> arrays.</param>
    /// <param name="pipe">The pipe's area profile.</param>
    /// <param name="gamma"><see cref="InletGamma"/> or <see cref="ExhaustGamma"/>.</param>
    /// <param name="dt">Time step in seconds.</param>
    /// <param name="index">
    /// Zero-based index of the point being updated, Delphi's <c>W-1</c>. Its neighbours at
    /// <c>index - 1</c> and <c>index + 1</c> are Delphi's <c>W-2</c> and <c>W</c>.
    /// </param>
    public static void UpdateInteriorPoint(
        PipeGrid current, PipeGrid target, PipeGeometry pipe, double gamma, double dt, int index)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pipe);

        var left = index - 1;
        var right = index + 1;
        var here = current.X[index];

        // Two linear interpolants over the three points, one either side of the point
        // being updated. A characteristic foot landing beyond the point uses the right
        // one, otherwise the left.
        var leftLine = Interpolant(current, left, index);
        var rightLine = Interpolant(current, index, right);

        // Points 1, 2 and 3 are the feet of C+, C- and C0. They start at the neighbours
        // and the point itself, and walk along the interpolants as the iteration refines
        // where each characteristic came from.
        var one = new Foot(current, left);
        var two = new Foot(current, right);
        var three = new Foot(current, index);

        // Point 4 is the answer at the new time level.
        double u4 = 0, p4 = 0, r4 = 0;
        double previousU = 0, previousP = 0, previousR = 0;

        double qPlus = 0, tPlus = 0, qMinus = 0, tMinus = 0, a0 = 0, t0 = 0;

        var iteration = 0;
        bool converged;

        do
        {
            // ---- C+ : the foot of the forward-running characteristic ----
            var plusFootIterations = 0;
            while (plusFootIterations++ <= MaxIterations)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (one.U, one.P, one.R);
                }

                var meanVelocity = (one.U + u4) / 2;
                var meanPressure = (one.P + p4) / 2;
                var meanDensity = (one.R + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var lambdaPlus = 1 / (meanVelocity + c);
                var x = here - (dt / lambdaPlus);

                if (x < 0)
                {
                    x = 0;
                }

                if (Math.Abs(x - one.X) < FootTolerance)
                {
                    // xs[k] is deliberately not updated here. The original only assigns it
                    // in the non-converged branch, so the next outer iteration compares
                    // against the last position that missed, not against this one. The
                    // state at the foot is likewise left where it was: u, P and R come
                    // from the previous position while the area and its gradient come
                    // from this one. Within the 0.1 mm tolerance, but not the same thing.
                    var diameter = HydraulicDiameter(pipe, x);
                    var friction = ManifoldNumerics.FanningFriction(gamma, one.R, one.U, diameter, c);

                    qPlus = meanDensity * c;

                    var source = (-one.R * one.U * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * one.U - c)
                                    * (one.R * one.U * Math.Abs(one.U) * 2 * friction / diameter));

                    tPlus = one.P + (qPlus * one.U) + (source * dt);
                    break;
                }

                one.MoveTo(x, x > here ? rightLine : leftLine);
            }

            // ---- C- : the foot of the backward-running characteristic ----
            var minusFootIterations = 0;
            while (minusFootIterations++ <= MaxIterations)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (two.U, two.P, two.R);
                }

                var meanVelocity = (two.U + u4) / 2;
                var meanPressure = (two.P + p4) / 2;
                var meanDensity = (two.R + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var lambdaMinus = 1 / (meanVelocity - c);
                var x = here - (dt / lambdaMinus);

                if (x > pipe.Length)
                {
                    x = pipe.Length;
                }

                if (Math.Abs(x - two.X) < FootTolerance)
                {
                    // xs[k] is deliberately not updated here. The original only assigns it
                    // in the non-converged branch, so the next outer iteration compares
                    // against the last position that missed, not against this one. The
                    // state at the foot is likewise left where it was: u, P and R come
                    // from the previous position while the area and its gradient come
                    // from this one. Within the 0.1 mm tolerance, but not the same thing.
                    var diameter = HydraulicDiameter(pipe, x);
                    var friction = ManifoldNumerics.FanningFriction(gamma, two.R, two.U, diameter, c);

                    qMinus = meanDensity * c;

                    var source = (-two.R * two.U * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * two.U + c)
                                    * (two.R * two.U * Math.Abs(two.U) * 2 * friction / diameter));

                    tMinus = two.P - (qMinus * two.U) + (source * dt);
                    break;
                }

                two.MoveTo(x, x > here ? rightLine : leftLine);
            }

            // ---- C0 : the path line, carrying entropy ----
            var pathFootIterations = 0;
            while (pathFootIterations++ <= MaxIterations)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (three.U, three.P, three.R);
                }

                var meanVelocity = (three.U + u4) / 2;
                var meanPressure = (three.P + p4) / 2;
                var meanDensity = (three.R + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                // Stagnant gas has no path line to trace: the foot is the point itself.
                var x = Math.Abs(meanVelocity) < 1E-8 ? here : here - (dt / (1 / meanVelocity));

                if (x < 0)
                {
                    x = 0;
                }

                if (x > pipe.Length)
                {
                    x = pipe.Length;
                }

                if (Math.Abs(x - three.X) < FootTolerance)
                {
                    // xs[k] is deliberately not updated here. The original only assigns it
                    // in the non-converged branch, so the next outer iteration compares
                    // against the last position that missed, not against this one. The
                    // state at the foot is likewise left where it was: u, P and R come
                    // from the previous position while the area and its gradient come
                    // from this one. Within the 0.1 mm tolerance, but not the same thing.
                    var diameter = HydraulicDiameter(pipe, x);
                    var friction = ManifoldNumerics.FanningFriction(gamma, three.R, three.U, diameter, c);

                    a0 = c * c;

                    var b0 = (gamma - 1)
                             * (three.R * three.U * Math.Abs(three.U) * 2 * friction / diameter);

                    t0 = (b0 * (here - x)) + three.P - (a0 * three.R);
                    break;
                }

                three.MoveTo(x, x > here ? rightLine : leftLine);
            }

            // ---- Solve the three compatibility relations at the new point ----
            u4 = (tPlus - tMinus) / (qPlus + qMinus);
            p4 = tPlus - (qPlus * u4);
            r4 = (p4 - t0) / a0;

            converged = iteration != 0
                        && Math.Abs(u4 - previousU) < VelocityTolerance
                        && Math.Abs(p4 - previousP) < PressureTolerance
                        && Math.Abs(r4 - previousR) < DensityTolerance;

            previousU = u4;
            previousP = p4;
            previousR = r4;
            iteration++;

            // The cap gives up rather than reporting: whatever the last pass produced is
            // taken as the answer. See ISSUES.md B52.
            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        target.Velocity[index] = u4;
        target.Pressure[index] = p4;
        target.Density[index] = r4;
        target.SpeedOfSound[index] = Math.Sqrt(gamma * p4 / r4);
    }

    /// <summary>
    /// Diameter of a round pipe with the local cross-sectional area. The manifold's area
    /// table does not record a shape, so a circle is assumed throughout.
    /// </summary>
    private static double HydraulicDiameter(PipeGeometry pipe, double position) =>
        Math.Sqrt(4 * pipe.Area(position) / Math.PI);

    /// <summary>A straight line through two adjacent grid points, for each of u, P and R.</summary>
    private readonly record struct Interpolants(
        double VelocitySlope, double VelocityIntercept,
        double PressureSlope, double PressureIntercept,
        double DensitySlope, double DensityIntercept);

    private static Interpolants Interpolant(PipeGrid grid, int from, int to)
    {
        // Note the direction: the original forms dx as x[from] - x[to] and anchors the
        // intercept on the second point, so the slope signs follow from that ordering.
        var dx = grid.X[from] - grid.X[to];

        double Slope(double[] values) => (values[from] - values[to]) / dx;
        double Intercept(double[] values, double slope) => values[to] - (slope * grid.X[to]);

        var velocitySlope = Slope(grid.Velocity);
        var pressureSlope = Slope(grid.Pressure);
        var densitySlope = Slope(grid.Density);

        return new Interpolants(
            velocitySlope, Intercept(grid.Velocity, velocitySlope),
            pressureSlope, Intercept(grid.Pressure, pressureSlope),
            densitySlope, Intercept(grid.Density, densitySlope));
    }

    /// <summary>
    /// The foot of one characteristic: where it came from, and the gas state there.
    /// </summary>
    private sealed class Foot
    {
        public Foot(PipeGrid grid, int index)
        {
            X = grid.X[index];
            U = grid.Velocity[index];
            P = grid.Pressure[index];
            R = grid.Density[index];
        }

        public double X { get; set; }

        public double U { get; private set; }

        public double P { get; private set; }

        public double R { get; private set; }

        /// <summary>Moves the foot and resamples the state from the given interpolant.</summary>
        public void MoveTo(double x, Interpolants line)
        {
            X = x;
            U = (line.VelocitySlope * x) + line.VelocityIntercept;
            P = (line.PressureSlope * x) + line.PressureIntercept;
            R = (line.DensitySlope * x) + line.DensityIntercept;
        }
    }
}
