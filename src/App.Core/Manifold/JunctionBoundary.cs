using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// An abrupt change of area inside a pipe, solved as a junction between two segments
/// rather than as an area-gradient source term (ISSUES.md B83). No Delphi counterpart: the
/// original meets a step through <c>INTERNAL_PIPE</c>'s <c>dAdL</c> source, which a step
/// narrower than its plus-and-minus-two-millimetre stencil turns into a pressure source
/// several times the pressure itself (ISSUES.md A36).
/// </summary>
/// <remarks>
/// <para>
/// The junction has two grid points at one position, a face on each side, with the
/// segment's own area at each. Three relations arrive along characteristics: the
/// <c>C+</c> from the left segment at the left face, the <c>C-</c> from the right segment
/// at the right face, and the path line carrying entropy from whichever side the gas comes
/// from. The junction supplies the other three, for a control volume of no length:
/// </para>
/// <list type="bullet">
/// <item>Mass: <c>ρ u A</c> is the same on both faces.</item>
/// <item>Energy: so is the stagnation enthalpy, <c>c²/(γ-1) + u²/2</c>.</item>
/// <item>
/// Momentum, which depends on the direction. Gas entering the larger area separates at the
/// step, and the upstream pressure acts across the whole of the downstream area: the
/// Borda-Carnot sudden expansion, with its loss of stagnation pressure. Gas entering the
/// smaller area accelerates without loss: entropy carries across. At rest both give equal
/// pressures, so the two agree as the flow reverses.
/// </item>
/// </list>
/// <para>
/// The six are solved by Newton's method with the characteristics' coefficients held, and
/// the characteristics are then traced again from the new faces until the faces stop
/// moving, to the tolerances <see cref="CharacteristicSolver"/> uses for an interior point.
/// </para>
/// <para>
/// A step can choke, and the face that chokes is the smaller one. When the subsonic
/// solution puts it at or past the speed of sound it is solved again as choked, its
/// velocity the local speed of sound in the direction of the flow (ISSUES.md A38). Gas
/// leaving the smaller area into the larger then leaves as a sonic jet: the upstream face
/// is sonic and fixed by its characteristic and entropy alone, and the downstream face by
/// its own characteristic, mass and energy, the Borda-Carnot momentum balance giving way,
/// as it must once the jet's exit pressure no longer acts across the whole step. Gas
/// entering the smaller area chokes at its downstream face, where the characteristic from
/// downstream stands still and can carry nothing back, so that relation gives way to the
/// sonic one and the contraction stays isentropic.
/// </para>
/// </remarks>
public static class JunctionBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;
    private const int MaxFootIterations = 100;
    private const int MaxOuterIterations = 1000;
    private const int MaxNewtonIterations = 50;

    /// <summary>
    /// Computes the new state at both faces of the junction whose left face is
    /// <paramref name="left"/>, and writes it into <paramref name="target"/>.
    /// </summary>
    /// <param name="current">The pipe state at the start of the step.</param>
    /// <param name="target">Where the new state goes.</param>
    /// <param name="leftPipe">The segment to the left of the junction.</param>
    /// <param name="rightPipe">The segment to the right.</param>
    /// <param name="gamma">The pipe's ratio of specific heats.</param>
    /// <param name="dt">Time step in seconds.</param>
    /// <param name="left">The index of the left face. The right face is the next point.</param>
    /// <param name="diagnostics">Where to count junction solves, or null.</param>
    public static void Apply(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry leftPipe,
        PipeGeometry rightPipe,
        double gamma,
        double dt,
        int left,
        ManifoldDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(leftPipe);
        ArgumentNullException.ThrowIfNull(rightPipe);

        var right = left + 1;
        var here = current.X[left];
        var leftArea = leftPipe.Area(here);
        var rightArea = rightPipe.Area(here);

        var leftLine = GridInterpolants.Through(current, from: left - 1, to: left);
        var rightLine = GridInterpolants.Through(current, from: right, to: right + 1);

        var plus = new Foot(current, left - 1);
        var minus = new Foot(current, right + 1);
        var leftPath = new Foot(current, left);
        var rightPath = new Foot(current, right);

        var faces = new[]
        {
            current.Velocity[left], current.Pressure[left], current.Density[left],
            current.Velocity[right], current.Pressure[right], current.Density[right],
        };

        var iteration = 0;
        var choked = false;
        var smallerFace = leftArea < rightArea ? 0 : 3;
        bool converged;

        do
        {
            var previous = (double[])faces.Clone();
            var fromLeft = faces[0] >= 0;

            var (qPlus, tPlus) = Trace(
                plus, leftLine, leftPipe, gamma, dt, here, +1, faces[0], faces[1], faces[2], leftPipe.Start, here);
            var (qMinus, tMinus) = Trace(
                minus, rightLine, rightPipe, gamma, dt, here, -1, faces[3], faces[4], faces[5], here, rightPipe.End);
            var (a0, t0) = fromLeft
                ? Trace(leftPath, leftLine, leftPipe, gamma, dt, here, 0, faces[0], faces[1], faces[2], leftPipe.Start, here)
                : Trace(rightPath, rightLine, rightPipe, gamma, dt, here, 0, faces[3], faces[4], faces[5], here, rightPipe.End);

            var relations = new Relations(qPlus, tPlus, qMinus, tMinus, a0, t0, fromLeft, leftArea, rightArea, gamma, choked);
            var guess = (double[])faces.Clone();

            // The smaller face at or past the speed of sound, in the flow's direction, or no
            // subsonic solution at all: the step chokes (ISSUES.md A38). A step that fails
            // the subsonic solve flows from the side whose characteristic carries the
            // higher pressure.
            if (!choked && !TrySolve(faces, relations))
            {
                choked = true;
                Array.Copy(guess, faces, faces.Length);
                SeedSonic(faces, smallerFace, gamma, tPlus >= tMinus, leftArea, rightArea);
                Solve(faces, relations with { Choked = true, FromLeft = tPlus >= tMinus });
            }
            else if (!choked && SonicOrBeyond(faces, smallerFace, gamma, fromLeft))
            {
                choked = true;
                Solve(faces, relations with { Choked = true });
            }
            else if (choked)
            {
                Solve(faces, relations);
            }

            converged = iteration != 0
                        && Math.Abs(faces[0] - previous[0]) < VelocityTolerance
                        && Math.Abs(faces[3] - previous[3]) < VelocityTolerance
                        && Math.Abs(faces[1] - previous[1]) < PressureTolerance
                        && Math.Abs(faces[4] - previous[4]) < PressureTolerance
                        && Math.Abs(faces[2] - previous[2]) < DensityTolerance
                        && Math.Abs(faces[5] - previous[5]) < DensityTolerance;

            iteration++;

            if (iteration > MaxOuterIterations)
            {
                converged = true;

                if (diagnostics is not null)
                {
                    diagnostics.JunctionOuterCapHits++;
                }
            }
        }
        while (!converged);

        if (diagnostics is not null)
        {
            diagnostics.JunctionPoints++;

            if (choked)
            {
                diagnostics.JunctionChokes++;
            }
        }

        for (var face = 0; face < 2; face++)
        {
            var index = face == 0 ? left : right;
            var (u, p, r) = (faces[3 * face], faces[(3 * face) + 1], faces[(3 * face) + 2]);

            if (!(p > 0 && r > 0))
            {
                throw new CfdException("ERROR : Pressure or density negative at an area junction !!!");
            }

            var c = Math.Sqrt(gamma * p / r);

            // A choked face sits at the speed of sound to the Newton tolerance; past it, or
            // either face supersonic otherwise, the step has no solution here.
            if (Math.Abs(u) > c * (1 + 1e-6))
            {
                throw new CfdException("ERROR : Flow chokes at an area junction !!!");
            }

            target.Velocity[index] = u;
            target.Pressure[index] = p;
            target.Density[index] = r;
            target.SpeedOfSound[index] = c;
        }
    }

    /// <summary>
    /// Traces one characteristic back from the junction to its foot and returns its
    /// compatibility coefficients: <c>(q, T)</c> with <c>p ± q u = T</c> for <c>C±</c>, or
    /// <c>(a0, t0)</c> with <c>p - a0 ρ = t0</c> for the path line. The same mean-state
    /// iteration, area source and friction as <see cref="CharacteristicSolver"/>.
    /// </summary>
    /// <param name="sign">+1 for <c>C+</c>, -1 for <c>C-</c>, 0 for the path line.</param>
    private static (double Coefficient, double Constant) Trace(
        Foot foot,
        GridInterpolants line,
        PipeGeometry pipe,
        double gamma,
        double dt,
        double here,
        int sign,
        double u4,
        double p4,
        double r4,
        double lowest,
        double highest)
    {
        for (var footIterations = 0; footIterations <= MaxFootIterations; footIterations++)
        {
            var meanVelocity = (foot.U + u4) / 2;
            var meanPressure = (foot.P + p4) / 2;
            var meanDensity = (foot.R + r4) / 2;
            var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

            var x = Math.Clamp(here - ((meanVelocity + (sign * c)) * dt), lowest, highest);

            if (Math.Abs(x - foot.X) < FootTolerance)
            {
                var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                var friction = ManifoldNumerics.FanningFriction(gamma, foot.R, foot.U, diameter, c);
                var wallShear = foot.R * foot.U * Math.Abs(foot.U) * 2 * friction / diameter;

                if (sign == 0)
                {
                    var a0 = c * c;
                    return (a0, ((gamma - 1) * wallShear * (here - x)) + foot.P - (a0 * foot.R));
                }

                var q = meanDensity * c;
                var source = (-foot.R * foot.U * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                             + ((((gamma - 1) * foot.U) - (sign * c)) * wallShear);

                return (q, foot.P + (sign * q * foot.U) + (source * dt));
            }

            foot.MoveTo(x, line);
        }

        throw new CfdException("ERROR : No convergence in area-junction characteristic foot !!!");
    }

    /// <summary>
    /// A starting point for the choked solve when the subsonic one found none: the smaller
    /// face at its own speed of sound in the flow's direction, the other carrying the same
    /// mass flux.
    /// </summary>
    private static void SeedSonic(double[] faces, int smallerFace, double gamma, bool fromLeft, double leftArea, double rightArea)
    {
        var other = 3 - smallerFace;
        var sign = fromLeft ? 1 : -1;
        var (smallArea, otherArea) = smallerFace == 0 ? (leftArea, rightArea) : (rightArea, leftArea);

        faces[smallerFace] = sign * Math.Sqrt(gamma * faces[smallerFace + 1] / faces[smallerFace + 2]);
        faces[other] = faces[smallerFace] * faces[smallerFace + 2] * smallArea / (faces[other + 2] * otherArea);
    }

    /// <summary>Whether the face at <paramref name="face"/> flows at or past the speed of sound in the flow's direction.</summary>
    private static bool SonicOrBeyond(double[] faces, int face, double gamma, bool fromLeft)
    {
        var (u, p, r) = (faces[face], faces[face + 1], faces[face + 2]);
        var c = Math.Sqrt(gamma * p / r);
        return p > 0 && r > 0 && (fromLeft ? u >= c : -u >= c);
    }

    /// <summary>The six relations at the junction, with the characteristics' coefficients held.</summary>
    /// <param name="Choked">Whether the smaller face is choked (ISSUES.md A38).</param>
    private readonly record struct Relations(
        double QPlus, double TPlus, double QMinus, double TMinus, double A0, double T0,
        bool FromLeft, double LeftArea, double RightArea, double Gamma, bool Choked = false)
    {
        /// <summary>The residuals and their Jacobian at <paramref name="v"/>: uL, pL, rL, uR, pR, rR.</summary>
        public void Evaluate(double[] v, double[] f, double[,] j)
        {
            Array.Clear(j);
            var (uL, pL, rL, uR, pR, rR) = (v[0], v[1], v[2], v[3], v[4], v[5]);
            var g = Gamma / (Gamma - 1);

            // The characteristics.
            f[0] = pL + (QPlus * uL) - TPlus;
            j[0, 0] = QPlus;
            j[0, 1] = 1;

            f[1] = pR - (QMinus * uR) - TMinus;
            j[1, 3] = -QMinus;
            j[1, 4] = 1;

            // The upstream side's entropy, along its path line.
            var (up, down) = FromLeft ? (0, 3) : (3, 0);
            f[2] = v[up + 1] - (A0 * v[up + 2]) - T0;
            j[2, up + 1] = 1;
            j[2, up + 2] = -A0;

            // Mass.
            f[3] = (rL * uL * LeftArea) - (rR * uR * RightArea);
            j[3, 0] = rL * LeftArea;
            j[3, 2] = uL * LeftArea;
            j[3, 3] = -rR * RightArea;
            j[3, 5] = -uR * RightArea;

            // Stagnation enthalpy.
            f[4] = (g * ((pL / rL) - (pR / rR))) + (((uL * uL) - (uR * uR)) / 2);
            j[4, 0] = uL;
            j[4, 1] = g / rL;
            j[4, 2] = -g * pL / (rL * rL);
            j[4, 3] = -uR;
            j[4, 4] = -g / rR;
            j[4, 5] = g * pR / (rR * rR);

            // Momentum, by the direction of the flow.
            var (uU, pU, rU) = (v[up], v[up + 1], v[up + 2]);
            var (ud, pd, rd) = (v[down], v[down + 1], v[down + 2]);
            var (areaUp, areaDown) = FromLeft ? (LeftArea, RightArea) : (RightArea, LeftArea);

            if (areaDown >= areaUp)
            {
                // Borda-Carnot: the upstream pressure acts across the whole downstream area.
                var k = areaUp / areaDown;
                f[5] = pU + (rU * uU * uU * k) - pd - (rd * ud * ud);
                j[5, up] = 2 * rU * uU * k;
                j[5, up + 1] = 1;
                j[5, up + 2] = uU * uU * k;
                j[5, down] = -2 * rd * ud;
                j[5, down + 1] = -1;
                j[5, down + 2] = -ud * ud;
            }
            else
            {
                // Isentropic contraction.
                var ratio = Math.Pow(rd / rU, Gamma);
                f[5] = pd - (pU * ratio);
                j[5, up + 1] = -ratio;
                j[5, up + 2] = pU * Gamma * ratio / rU;
                j[5, down + 1] = 1;
                j[5, down + 2] = -pU * Gamma * ratio / rd;
            }

            if (!Choked)
            {
                return;
            }

            // Choked (ISSUES.md A38): the smaller face is sonic in the flow's direction.
            // Leaving the smaller area, that replaces the momentum balance; entering it, it
            // replaces the characteristic arriving from downstream, which stands still at a
            // sonic face.
            var sign = FromLeft ? 1 : -1;

            if (areaUp < areaDown)
            {
                Sonic(v, f, j, row: 5, face: up, sign);
            }
            else
            {
                Sonic(v, f, j, row: FromLeft ? 1 : 0, face: down, sign);
            }
        }

        /// <summary>Row <paramref name="row"/> becomes <c>u = sign c</c> at <paramref name="face"/>.</summary>
        private void Sonic(double[] v, double[] f, double[,] j, int row, int face, int sign)
        {
            var (u, p, r) = (v[face], v[face + 1], v[face + 2]);
            var c = Math.Sqrt(Gamma * p / r);

            for (var column = 0; column < 6; column++)
            {
                j[row, column] = 0;
            }

            f[row] = u - (sign * c);
            j[row, face] = 1;
            j[row, face + 1] = -sign * Gamma / (2 * c * r);
            j[row, face + 2] = sign * Gamma * p / (2 * c * r * r);
        }
    }

    /// <summary>
    /// <see cref="Solve"/>, reporting a failure to converge, or a non-physical state, as
    /// false rather than throwing.
    /// </summary>
    private static bool TrySolve(double[] v, in Relations relations)
    {
        try
        {
            Solve(v, relations);
            return v[1] > 0 && v[2] > 0 && v[4] > 0 && v[5] > 0;
        }
        catch (CfdException)
        {
            return false;
        }
    }

    /// <summary>
    /// Newton's method on the six relations from the faces' current estimate, scaled to
    /// the faces' own pressure, density and speed of sound so that one tolerance serves
    /// them all.
    /// </summary>
    private static void Solve(double[] v, in Relations relations)
    {
        var pressure = Math.Max(Math.Abs(v[1]), Math.Abs(v[4]));
        var density = Math.Max(Math.Abs(v[2]), Math.Abs(v[5]));
        var sound = Math.Sqrt(relations.Gamma * pressure / density);
        var area = Math.Max(relations.LeftArea, relations.RightArea);

        double[] columns = [sound, pressure, density, sound, pressure, density];
        double[] rows = [1 / pressure, 1 / pressure, 1 / pressure, 1 / (density * sound * area), 1 / (sound * sound), 1 / pressure];

        var f = new double[6];
        var j = new double[6, 6];

        for (var iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            relations.Evaluate(v, f, j);

            for (var r = 0; r < 6; r++)
            {
                f[r] = -f[r] * rows[r];

                for (var c = 0; c < 6; c++)
                {
                    j[r, c] *= rows[r] * columns[c];
                }
            }

            var step = SolveLinear(j, f);
            var largest = 0.0;

            // Damped so that no pressure or density falls below a tenth of its value in one
            // step: a choked solve started far from its answer (ISSUES.md A38) otherwise
            // overshoots to a negative state. A step that keeps them positive is taken whole.
            var damping = 1.0;

            foreach (var c in (int[])[1, 2, 4, 5])
            {
                var change = step[c] * columns[c];

                if (v[c] + change < 0.1 * v[c])
                {
                    damping = Math.Min(damping, 0.9 * v[c] / -change);
                }
            }

            for (var c = 0; c < 6; c++)
            {
                v[c] += damping * step[c] * columns[c];
                largest = Math.Max(largest, Math.Abs(step[c]));
            }

            if (largest < 1e-12)
            {
                return;
            }
        }

        throw new CfdException("ERROR : No convergence in area-junction solve !!!");
    }

    /// <summary>Gaussian elimination with partial pivoting. Overwrites both arguments.</summary>
    private static double[] SolveLinear(double[,] a, double[] b)
    {
        var n = b.Length;

        for (var pivot = 0; pivot < n; pivot++)
        {
            var best = pivot;

            for (var r = pivot + 1; r < n; r++)
            {
                if (Math.Abs(a[r, pivot]) > Math.Abs(a[best, pivot]))
                {
                    best = r;
                }
            }

            if (a[best, pivot] == 0)
            {
                throw new CfdException("ERROR : Singular area-junction system !!!");
            }

            if (best != pivot)
            {
                for (var c = 0; c < n; c++)
                {
                    (a[pivot, c], a[best, c]) = (a[best, c], a[pivot, c]);
                }

                (b[pivot], b[best]) = (b[best], b[pivot]);
            }

            for (var r = pivot + 1; r < n; r++)
            {
                var m = a[r, pivot] / a[pivot, pivot];

                for (var c = pivot; c < n; c++)
                {
                    a[r, c] -= m * a[pivot, c];
                }

                b[r] -= m * b[pivot];
            }
        }

        var x = new double[n];

        for (var r = n - 1; r >= 0; r--)
        {
            var sum = b[r];

            for (var c = r + 1; c < n; c++)
            {
                sum -= a[r, c] * x[c];
            }

            x[r] = sum / a[r, r];
        }

        return x;
    }

    /// <summary>The foot of one characteristic: where it came from, and the gas state there.</summary>
    private sealed class Foot(PipeGrid grid, int index)
    {
        public double X { get; private set; } = grid.X[index];

        public double U { get; private set; } = grid.Velocity[index];

        public double P { get; private set; } = grid.Pressure[index];

        public double R { get; private set; } = grid.Density[index];

        public void MoveTo(double x, GridInterpolants line)
        {
            X = x;
            U = line.VelocityAt(x);
            P = line.PressureAt(x);
            R = line.DensityAt(x);
        }
    }
}
