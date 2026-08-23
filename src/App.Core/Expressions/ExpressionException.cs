namespace App.Core.Expressions;

/// <summary>
/// Thrown when an expression cannot be parsed or evaluated.
/// </summary>
/// <remarks>
/// The Delphi original reported these through <c>TAdCalc</c>'s error codes; the
/// port raises instead. Failing loudly matters here: an unsupported construct that
/// quietly evaluated to something plausible would surface much later as a wrong
/// manifold grid size, with nothing to point at.
/// </remarks>
public class ExpressionException : Exception
{
    public ExpressionException()
    {
    }

    public ExpressionException(string message) : base(message)
    {
    }

    public ExpressionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
