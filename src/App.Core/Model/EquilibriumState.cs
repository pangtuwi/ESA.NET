namespace App.Core.Model;

/// <summary>
/// Port of the public data of Delphi <c>TEqbm</c> (Eqbm.pas): species mole
/// fractions and their derivatives with respect to temperature, pressure and
/// equivalence ratio. The solver itself is phase 4 work.
/// </summary>
public sealed class EquilibriumState
{
    /// <summary>Species mole fractions, <c>x</c>.</summary>
    public SpeciesValues X { get; } = new();

    /// <summary>Derivative with respect to temperature, <c>dxdT</c>.</summary>
    public SpeciesValues DxDt { get; } = new();

    /// <summary>Derivative with respect to pressure, <c>dxdp</c>.</summary>
    public SpeciesValues DxDp { get; } = new();

    /// <summary>Derivative with respect to equivalence ratio, <c>dxdF</c>.</summary>
    public SpeciesValues DxDf { get; } = new();

    public int ErrorCode { get; set; }

    public bool Frozen { get; set; }
}
