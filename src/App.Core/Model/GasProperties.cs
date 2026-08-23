namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TProp</c> (GASPROPS.PAS): the equilibrium
/// state plus the residual-gas composition and the last computed properties.
/// </summary>
public sealed class GasProperties
{
    public EquilibriumState Equilibrium { get; } = new();

    /// <summary>Initial fuel/air/residual concentrations, <c>InitFAirResConcs</c>.</summary>
    public SpeciesValues InitialFuelAirResidualConcentrations { get; } = new();

    /// <summary>Working fuel/air/residual concentrations, <c>FAirResConcs</c>.</summary>
    public SpeciesValues FuelAirResidualConcentrations { get; } = new();

    public double FuelMoleFraction { get; set; }

    /// <summary>Gas constant, <c>R</c>.</summary>
    public double R { get; set; }

    /// <summary>Specific enthalpy, <c>h</c>.</summary>
    public double H { get; set; }

    /// <summary>Specific internal energy, <c>u</c>.</summary>
    public double U { get; set; }

    /// <summary>Specific heat at constant pressure, <c>Cp</c>.</summary>
    public double Cp { get; set; }

    public double DuDt { get; set; }

    public double DuDp { get; set; }

    public double DuDf { get; set; }

    public int Error { get; set; }
}
