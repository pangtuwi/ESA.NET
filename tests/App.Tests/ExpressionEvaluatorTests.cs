using App.Core;
using App.Core.Expressions;

namespace App.Tests;

/// <summary>
/// Pins the semantics recovered from ADCALC.PAS. These are the tests to change first
/// if the port ever disagrees with the original's numbers.
/// </summary>
public sealed class ExpressionEvaluatorTests
{
    private static readonly CachingExpressionEvaluator Evaluator = new();

    private static double Eval(string expression, double n = 0, double l = 0) =>
        Evaluator.Evaluate(expression, n, l);

    [Fact]
    public void PowerIsLeftAssociative()
    {
        // ADCALC.PAS recurses only while the next operator scores strictly higher
        // than the current one, so ^ following ^ folds left: (2^3)^2, not 2^(3^2).
        // Almost every other language would give 512 here.
        Assert.Equal(64.0, Eval("2^3^2"));
    }

    [Fact]
    public void UnaryMinusBindsLooserThanPower()
    {
        // A leading '-' is applied against a zero accumulator before the power loop
        // runs, so this is -(2^2), not (-2)^2.
        Assert.Equal(-4.0, Eval("-2^2"));
        Assert.Equal(4.0, Eval("(-2)^2"));
    }

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("2*3^2", 18)]
    [InlineData("10-2-3", 5)]
    [InlineData("100/10/2", 5)]
    [InlineData("-2*3", -6)]
    [InlineData("+5", 5)]
    [InlineData("(0-2)*(0-3)", 6)]
    public void PrecedenceAndAssociativityFollowTheOriginal(string expression, double expected)
    {
        Assert.Equal(expected, Eval(expression), 12);
    }

    [Theory]
    [InlineData("1.0293E-19", 1.0293E-19)]
    [InlineData("1.0293e-19", 1.0293E-19)]
    [InlineData("2.4888E5", 248880)]
    [InlineData("0.758", 0.758)]
    [InlineData(".5", 0.5)]
    public void ScientificNotationParses(string expression, double expected)
    {
        // Exact: the same literal text must parse to the same double.
        Assert.Equal(expected, Eval(expression));
    }

    [Fact]
    public void VariablesResolve()
    {
        Assert.Equal(6000.0, Eval("N", n: 6000));
        Assert.Equal(0.758, Eval("L", l: 0.758));
        Assert.Equal(6000.758, Eval("N+L", n: 6000, l: 0.758), 9);
    }

    [Fact]
    public void VariableNamesAreCaseInsensitive()
    {
        Assert.Equal(4000.0, Eval("n", n: 4000));
        Assert.Equal(0.5, Eval("l", l: 0.5));
    }

    [Fact]
    public void DivisionByZeroIsAnError()
    {
        // AdCalc raises ExecError(13) rather than returning an infinity.
        Assert.Throws<ExpressionException>(() => Eval("1/0"));
    }

    [Theory]
    [InlineData("Sin(N)")]          // AdCalc has 30-plus functions; this port has none.
    [InlineData("N > 1000")]
    [InlineData("N and L")]
    [InlineData("2 +")]
    [InlineData("(2")]
    [InlineData("2)")]
    [InlineData("")]
    [InlineData("N $ 2")]
    [InlineData("X")]
    // A sign is only legal at the start of an expression or a bracket, matching
    // AdCalc, which handles signs only in its initial state. Confirmed against the
    // data: no expression in any of the 65 .eng files puts a sign after an operator.
    [InlineData("3*-2")]
    [InlineData("2^-2")]
    public void UnsupportedSyntaxIsRejected(string expression)
    {
        Assert.Throws<ExpressionException>(() => Eval(expression));
    }

    [Fact]
    public void NegativeBaseWithIntegerExponentIsAllowed()
    {
        // Delphi routes integer exponents through IntPower, which handles a negative
        // base; only a fractional exponent would reach Ln and fail.
        Assert.Equal(-8.0, Eval("(0-2)^3"));
        Assert.Throws<ExpressionException>(() => Eval("(0-2)^0.5"));
    }

    [Fact]
    public void PowerMatchesDelphiBranching()
    {
        Assert.Equal(1.0, DelphiMath.Power(0, 0));
        Assert.Equal(0.0, DelphiMath.Power(0, 3));
        Assert.Equal(0.25, DelphiMath.Power(2, -2));
        Assert.Equal(1024.0, DelphiMath.Power(2, 10));
        Assert.Throws<ExpressionException>(() => DelphiMath.Power(0, -1));
    }

    [Fact]
    public void RealInletGridExpressionEvaluates()
    {
        // Verbatim from Default.eng.
        const string Expression =
            "((1.0293E-19*N^6 - 0.0000000000000024888*N^5 + 0.000000000024186*N^4 - 0.00000012043*N^3 " +
            "+ 0.00032229*N^2 - 0.42783*N + 236.51)*L/0.758)";

        var value = Evaluator.Evaluate(Expression, engineSpeed: 4000, length: 0.758);

        Assert.True(double.IsFinite(value));
        Assert.True(value > 0, $"Expected a positive grid length, got {value}.");
    }

    [Fact]
    public void GridSizeRoundsHalfToEvenAndEnforcesTheLimit()
    {
        var calculator = new GridSizeCalculator(Evaluator);

        // Delphi Round is round-half-to-even: 2.5 goes to 2, 3.5 goes to 4.
        Assert.Equal(2, calculator.InletGridSize("2.5", 0, 0));
        Assert.Equal(4, calculator.InletGridSize("3.5", 0, 0));

        Assert.Equal(EsaLimits.InletGridPoints, calculator.InletGridSize("68", 0, 0));
        Assert.Throws<CfdException>(() => calculator.InletGridSize("69", 0, 0));
        Assert.Throws<CfdException>(() => calculator.ExhaustGridSize("39", 0, 0));
    }
}
