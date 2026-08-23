namespace App.Core.Expressions;

/// <summary>The variables ESA registers with the evaluator.</summary>
/// <remarks>
/// <c>GridSizes.pas</c> registers <c>L</c> as "Length" and <c>N</c> as
/// "EngineSpeed"; <c>DoubleFunc.pas</c> registers <c>N</c> alone.
/// </remarks>
public enum ExpressionVariable
{
    /// <summary>Engine speed in rev/min.</summary>
    EngineSpeed,

    /// <summary>Pipe length in metres.</summary>
    Length,
}

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
}

/// <summary>
/// A parsed expression. Immutable, so a parsed tree is safe to cache and share.
/// </summary>
public abstract record ExpressionNode
{
    public abstract double Evaluate(double engineSpeed, double length);
}

internal sealed record ConstantNode(double Value) : ExpressionNode
{
    public override double Evaluate(double engineSpeed, double length) => Value;
}

internal sealed record VariableNode(ExpressionVariable Variable) : ExpressionNode
{
    public override double Evaluate(double engineSpeed, double length) =>
        Variable == ExpressionVariable.EngineSpeed ? engineSpeed : length;
}

internal sealed record NegateNode(ExpressionNode Operand) : ExpressionNode
{
    public override double Evaluate(double engineSpeed, double length) =>
        -Operand.Evaluate(engineSpeed, length);
}

internal sealed record BinaryNode(BinaryOperator Operator, ExpressionNode Left, ExpressionNode Right) : ExpressionNode
{
    public override double Evaluate(double engineSpeed, double length)
    {
        var left = Left.Evaluate(engineSpeed, length);
        var right = Right.Evaluate(engineSpeed, length);

        return Operator switch
        {
            BinaryOperator.Add => left + right,
            BinaryOperator.Subtract => left - right,
            BinaryOperator.Multiply => left * right,

            // AdCalc raises ExecError(13) rather than yielding an infinity
            // (ADCALC.PAS lines 2607-2613), so division by zero is an error here too.
            BinaryOperator.Divide => right == 0.0
                ? throw new ExpressionException("Division by zero.")
                : left / right,

            BinaryOperator.Power => DelphiMath.Power(left, right),
            _ => throw new ExpressionException($"Unsupported operator {Operator}."),
        };
    }
}
