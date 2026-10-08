namespace App.Core.Thermo;

/// <summary>
/// The numerical helpers from Delphi <c>MATHDPM.PAS</c> that the thermodynamic model
/// depends on. Only the routines the physics actually calls are ported.
/// </summary>
/// <remarks>
/// These are reproduced rather than replaced with .NET equivalents because the
/// equilibrium solver's convergence test reads <see cref="GaussReduce"/>'s reported
/// accuracy, and that number falls out of the exact elimination order below.
/// </remarks>
public static class DelphiNumerics
{
    /// <summary>Delphi <c>ZERO_UNDERFLOW</c>.</summary>
    public const double ZeroUnderflow = 1e-35;

    /// <summary>Delphi <c>ARRSIZE</c>: the equilibrium system is always four by four.</summary>
    public const int ArraySize = 4;

    /// <summary>
    /// Solves <c>A y = b</c> in place, returning the guaranteed decimal-place accuracy
    /// of the solution. Port of <c>GaussReduce</c>.
    /// </summary>
    /// <remarks>
    /// Delphi passes the matrix by value, so the caller's copy is untouched and the
    /// original matrix is still available for the residual check at the end. The clone
    /// here does the same. There is no pivoting: the elimination walks
    /// <c>row := (rowinc + pivot) mod 4</c> downwards, which leaves the pivot row until
    /// last so that <c>Valpp</c>, read before the loop, is still valid.
    /// </remarks>
    /// <param name="matrix">The system matrix. Not modified.</param>
    /// <param name="rhs">The right-hand side on entry, the solution on exit.</param>
    /// <returns>
    /// <c>-SciExp</c> of the largest residual: 5 means every element of the solution
    /// satisfies the original system to at least five decimal places. The equilibrium
    /// solver treats anything under 5 as a failure.
    /// </returns>
    public static int GaussReduce(double[,] matrix, double[] rhs)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);

        const int N = ArraySize;

        var a = (double[,])matrix.Clone();
        var originalRhs = (double[])rhs.Clone();

        for (var pivot = 0; pivot < N; pivot++)
        {
            var valpp = a[pivot, pivot];

            for (var rowinc = N - 1; rowinc >= 0; rowinc--)
            {
                var row = (rowinc + pivot) % N;
                var multiplier = a[row, pivot] / valpp;

                for (var colinc = N - 1; colinc >= 0; colinc--)
                {
                    var col = (pivot + colinc) % N;

                    if (rowinc != 0)
                    {
                        a[row, col] -= a[pivot, col] * multiplier;
                    }
                    else
                    {
                        a[row, col] /= valpp;
                    }
                }

                if (rowinc != 0)
                {
                    rhs[row] -= rhs[pivot] * multiplier;
                }
                else
                {
                    rhs[row] /= valpp;
                }
            }
        }

        var accuracy = 0.0;

        for (var row = 0; row < N; row++)
        {
            var residual = 0.0;

            for (var col = 0; col < N; col++)
            {
                residual += matrix[row, col] * rhs[col];
            }

            residual = Math.Abs(residual - originalRhs[row]);

            if (residual > accuracy)
            {
                accuracy = residual;
            }
        }

        return -SciExp(accuracy);
    }

    /// <summary>
    /// <see cref="GaussReduce"/> carried out in double-double arithmetic, about 106 bits
    /// of significand, for the systems 53 bits cannot resolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original ran <c>GaussReduce</c> in 80-bit <c>Extended</c>, a 64-bit
    /// significand. That margin matters once a species fraction is tiny: the equilibrium
    /// Jacobian's oxygen column scales as <c>1/sqrt(x[8])</c>, so with rich burnt gas
    /// at 1000 K, where <c>x[8]</c> is near 4e-20, it reaches 1e19 against entries of
    /// order ten elsewhere. The unpivoted elimination then loses everything 53 bits
    /// carry; the solution is wrong by orders of magnitude and its residual fails the
    /// solver's resolution test, which 64 bits pass. See ISSUES.md A30.
    /// </para>
    /// <para>
    /// The elimination order is <see cref="GaussReduce"/>'s exactly. Only the arithmetic
    /// is wider, and wider than <c>Extended</c> on purpose: on that system 64 bits clear
    /// the resolution test but still answer the nitrogen pressure derivative as -5.3e-9
    /// where it is +1.7e-12, because the test is an absolute residual and the right-hand
    /// side is small. 106 bits agree with an exact solve to nine digits. So this is the
    /// accurate result rather than a bit-for-bit emulation of the original's. The solution
    /// is rounded back to double, and the reported accuracy is the residual of that
    /// rounded solution, which is what the caller is given.
    /// </para>
    /// </remarks>
    /// <param name="matrix">The system matrix. Not modified.</param>
    /// <param name="rhs">The right-hand side on entry, the solution on exit.</param>
    /// <returns>The decimal-place accuracy, as <see cref="GaussReduce"/> reports it.</returns>
    public static int GaussReduceExtended(double[,] matrix, double[] rhs)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);

        const int N = ArraySize;

        var a = new DoubleDouble[N, N];
        var b = new DoubleDouble[N];

        for (var row = 0; row < N; row++)
        {
            b[row] = rhs[row];

            for (var col = 0; col < N; col++)
            {
                a[row, col] = matrix[row, col];
            }
        }

        for (var pivot = 0; pivot < N; pivot++)
        {
            var valpp = a[pivot, pivot];

            for (var rowinc = N - 1; rowinc >= 0; rowinc--)
            {
                var row = (rowinc + pivot) % N;
                var multiplier = a[row, pivot] / valpp;

                for (var colinc = N - 1; colinc >= 0; colinc--)
                {
                    var col = (pivot + colinc) % N;

                    a[row, col] = rowinc != 0
                        ? a[row, col] - (a[pivot, col] * multiplier)
                        : a[row, col] / valpp;
                }

                b[row] = rowinc != 0
                    ? b[row] - (b[pivot] * multiplier)
                    : b[row] / valpp;
            }
        }

        var originalRhs = (double[])rhs.Clone();

        for (var row = 0; row < N; row++)
        {
            rhs[row] = b[row].Hi;
        }

        var accuracy = 0.0;

        for (var row = 0; row < N; row++)
        {
            DoubleDouble residual = 0;

            for (var col = 0; col < N; col++)
            {
                residual += (DoubleDouble)matrix[row, col] * rhs[col];
            }

            accuracy = Math.Max(accuracy, Math.Abs((residual - originalRhs[row]).Hi));
        }

        return -SciExp(accuracy);
    }

    /// <summary>
    /// An unevaluated sum of two doubles, <c>Hi + Lo</c> with <c>|Lo|</c> at most half an
    /// ulp of <c>Hi</c>: Dekker's and Knuth's error-free transformations, after Hida, Li
    /// and Bailey's QD library.
    /// </summary>
    private readonly record struct DoubleDouble(double Hi, double Lo)
    {
        public static implicit operator DoubleDouble(double value) => new(value, 0);

        public static DoubleDouble operator +(DoubleDouble x, DoubleDouble y)
        {
            var (s, e) = TwoSum(x.Hi, y.Hi);
            var (t, f) = TwoSum(x.Lo, y.Lo);
            (s, e) = QuickTwoSum(s, e + t);
            return Normalise(s, e + f);
        }

        public static DoubleDouble operator -(DoubleDouble x, DoubleDouble y) => x + new DoubleDouble(-y.Hi, -y.Lo);

        public static DoubleDouble operator *(DoubleDouble x, DoubleDouble y)
        {
            var p = x.Hi * y.Hi;
            var e = Math.FusedMultiplyAdd(x.Hi, y.Hi, -p);
            return Normalise(p, e + ((x.Hi * y.Lo) + (x.Lo * y.Hi)));
        }

        public static DoubleDouble operator /(DoubleDouble x, DoubleDouble y)
        {
            var q1 = x.Hi / y.Hi;
            var r = x - (y * q1);
            var q2 = r.Hi / y.Hi;
            r -= y * q2;
            var q3 = r.Hi / y.Hi;
            return Normalise(q1, q2) + q3;
        }

        private static DoubleDouble Normalise(double hi, double lo)
        {
            var (s, e) = QuickTwoSum(hi, lo);
            return new DoubleDouble(s, e);
        }

        private static (double Sum, double Error) TwoSum(double a, double b)
        {
            var s = a + b;
            var bb = s - a;
            return (s, (a - (s - bb)) + (b - bb));
        }

        private static (double Sum, double Error) QuickTwoSum(double a, double b)
        {
            var s = a + b;
            return (s, b - (s - a));
        }
    }

    /// <summary>Exponent of <paramref name="x"/> in scientific notation. Port of <c>SciExp</c>.</summary>
    public static int SciExp(double x)
    {
        x = Math.Abs(x);

        if (x < ZeroUnderflow)
        {
            x = ZeroUnderflow;
        }

        return (int)Math.Truncate(Log10(x));
    }

    /// <summary>
    /// Base-10 logarithm. Port of <c>log10</c>, which divides by a literal 2.302585093
    /// rather than by <c>Ln(10)</c> and returns zero for non-positive input.
    /// </summary>
    public static double Log10(double x) => x > 0 ? Math.Log(x) / 2.302585093 : 0;

    /// <summary>Port of <c>ZeroFilter</c>: values under 1e-8 in magnitude become zero.</summary>
    public static double ZeroFilter(double x) => Math.Abs(x) < 1e-8 ? 0 : x;

    /// <summary>
    /// Port of <c>doublePower</c>, the routine the equilibrium constants are raised
    /// through. It goes the long way round via polar form and then filters the result,
    /// so small values collapse to zero rather than underflowing gradually.
    /// </summary>
    public static double DoublePower(double a, double b)
    {
        if (a == 0)
        {
            return 0;
        }

        if (b == 0)
        {
            return 1;
        }

        var angle = a > 0 ? 0.0 : -Math.PI;
        var r = Math.Exp(b * Math.Log(Math.Abs(a)));

        angle *= b;

        var real = ZeroFilter(r * Math.Cos(angle));
        var imaginary = ZeroFilter(r * Math.Sin(angle));

        // The original returns zero unless the result is wholly real.
        return imaginary == 0 ? real : 0;
    }
}
