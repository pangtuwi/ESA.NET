namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TDoubFunc</c> (DoubleFunc.pas): an AdCalc
/// expression evaluated against engine speed. Only the expression text lives in
/// Core; the evaluator that replaces the proprietary <c>TAdCalc</c> is phase 3
/// work and will sit behind an interface then.
/// </summary>
public sealed class ExpressionFunction
{
    /// <summary>The expression source, as stored in the <c>.eng</c> file.</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>The most recently evaluated result.</summary>
    public double Value { get; set; }
}
