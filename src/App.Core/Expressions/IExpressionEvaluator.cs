namespace App.Core.Expressions;

/// <summary>
/// Evaluates the expressions stored in <c>.eng</c> files. Replaces the proprietary
/// <c>TAdCalc</c> component the Delphi original used through <c>TDoubFunc</c> and
/// <c>TGridSize</c>.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> for the given engine speed and pipe
    /// length. Expressions that use only <c>N</c> may pass any value for
    /// <paramref name="length"/>.
    /// </summary>
    /// <exception cref="ExpressionException">
    /// The expression could not be parsed, uses an unsupported construct, or failed
    /// during evaluation.
    /// </exception>
    double Evaluate(string expression, double engineSpeed, double length = 0);
}
