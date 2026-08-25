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
