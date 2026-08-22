namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TGridSize</c> (GridSizes.pas): an AdCalc
/// expression in engine speed <c>N</c> and pipe length <c>L</c> that yields the
/// active manifold grid-point count. SPEC.md section 2 records that counts above
/// <see cref="EsaLimits.InletGridPoints"/> or
/// <see cref="EsaLimits.ExhaustGridPoints"/> raise <see cref="CfdException"/>.
/// </summary>
public sealed class GridSizeFunction
{
    public string Expression { get; set; } = string.Empty;

    /// <summary>The most recently computed grid-point count.</summary>
    public int GridSize { get; set; }
}
