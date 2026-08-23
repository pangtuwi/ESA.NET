namespace App.Core.Expressions;

/// <summary>
/// Turns a manifold grid-size expression into an active grid-point count.
/// Port of <c>TGridSize.GridSize</c> (GridSizes.pas) plus the limit checks that
/// <c>TManifolds.Main_Prog</c> applies to the result (Manifolds.pas lines 2729-2737).
/// </summary>
public sealed class GridSizeCalculator
{
    private readonly IExpressionEvaluator _evaluator;

    public GridSizeCalculator(IExpressionEvaluator evaluator) => _evaluator = evaluator;

    /// <summary>Inlet grid points, capped at <see cref="EsaLimits.InletGridPoints"/>.</summary>
    public int InletGridSize(string expression, double pipeLength, double engineSpeed) =>
        GridSize(expression, pipeLength, engineSpeed, EsaLimits.InletGridPoints, "Inlet");

    /// <summary>Exhaust grid points, capped at <see cref="EsaLimits.ExhaustGridPoints"/>.</summary>
    public int ExhaustGridSize(string expression, double pipeLength, double engineSpeed) =>
        GridSize(expression, pipeLength, engineSpeed, EsaLimits.ExhaustGridPoints, "Exhaust");

    private int GridSize(string expression, double pipeLength, double engineSpeed, int limit, string which)
    {
        var result = _evaluator.Evaluate(expression, engineSpeed, pipeLength);

        // Delphi's Round is round-half-to-even, which is also .NET's default. Neither
        // a cast nor Floor(x + 0.5) would match.
        var points = (int)Math.Round(result, MidpointRounding.ToEven);

        if (points > limit)
        {
            throw new CfdException(
                $"Calculated {which} Grid Length of {points} but was greater than Maximum of {limit}");
        }

        return points;
    }
}
