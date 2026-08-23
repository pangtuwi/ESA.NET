namespace App.Core.Expressions;

/// <summary>
/// Arithmetic helpers that reproduce Delphi semantics exactly where .NET's
/// equivalents differ.
/// </summary>
public static class DelphiMath
{
    /// <summary>
    /// Port of Delphi <c>Math.Power</c>, which AdCalc uses for the <c>^</c> operator
    /// (ADCALC.PAS line 2614).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately not <see cref="Math.Pow"/>. Delphi routes integer
    /// exponents through <c>IntPower</c>, which multiplies by repeated squaring,
    /// and only falls back to <c>Exp(y * Ln(x))</c> for fractional exponents.
    /// The two paths differ in the last bits, and a manifold grid size is the
    /// <c>Round</c> of an expression built from <c>N^6</c> terms — close enough to a
    /// boundary that the difference can change an integer grid count.
    /// </para>
    /// <para>
    /// The branching also decides which inputs are legal: a negative base raised to
    /// an integer power is fine, a negative base raised to a fractional power is not,
    /// because Delphi would evaluate <c>Ln</c> of a negative number.
    /// </para>
    /// </remarks>
    public static double Power(double baseValue, double exponent)
    {
        if (exponent == 0.0)
        {
            return 1.0;
        }

        if (baseValue == 0.0)
        {
            if (exponent > 0.0)
            {
                return 0.0;
            }

            throw new ExpressionException("Zero raised to a negative power is undefined.");
        }

        if (Math.Abs(exponent) <= int.MaxValue && exponent == Math.Truncate(exponent))
        {
            return IntPower(baseValue, (int)exponent);
        }

        if (baseValue < 0.0)
        {
            throw new ExpressionException(
                $"Cannot raise the negative value {baseValue} to the fractional power {exponent}.");
        }

        return Math.Exp(exponent * Math.Log(baseValue));
    }

    /// <summary>
    /// Port of Delphi <c>Math.IntPower</c>: exponentiation by repeated squaring, with
    /// a negative exponent handled as the reciprocal of the positive result.
    /// </summary>
    private static double IntPower(double baseValue, int exponent)
    {
        var remaining = Math.Abs(exponent);
        var factor = baseValue;
        var result = 1.0;

        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                result *= factor;
            }

            factor *= factor;
            remaining >>= 1;
        }

        return exponent < 0 ? 1.0 / result : result;
    }
}
