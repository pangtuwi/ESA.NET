using App.Core.Model;

namespace App.Core.Charts;

/// <summary>
/// One quantity a multi-run sweep can be plotted by: an input the grid can vary, or a
/// figure the run produced.
/// </summary>
/// <param name="Name">What the quantity is called on screen.</param>
/// <param name="Unit">Its unit, as the axis caption shows it.</param>
/// <param name="Value">Reads it from the engine a row ran on, in that unit.</param>
public sealed record SweepVariable(string Name, string Unit, Func<Engine, double> Value)
{
    /// <summary>Name and unit together, for a picker and an axis caption.</summary>
    public string DisplayName => $"{Name} [{Unit}]";

    public override string ToString() => DisplayName;
}

/// <summary>
/// Everything a sweep can be plotted by. Not in the original, which showed a sweep only
/// as its torque, power and volumetric efficiency curves against speed.
/// </summary>
/// <remarks>
/// Every value is read from the engine as the row actually ran - after the grid's
/// overrides and after <c>InitVars</c> - so a column the grid left at "-" still reports
/// the engine's own value rather than nothing. Inputs are given in the units the editor
/// and the grid use, outputs in the units the results panel and <c>run.txt</c> use.
/// </remarks>
public static class SweepVariables
{
    /// <summary>Engine speed, the usual horizontal axis.</summary>
    public static SweepVariable Speed { get; } = new("Engine speed", "rev/min", e => e.Rpm);

    /// <summary>Torque, the usual vertical axis.</summary>
    public static SweepVariable Torque { get; } = new("Torque", "N·m", e => e.Torque);

    /// <summary>The variables, inputs first, in the order the picker lists them.</summary>
    public static IReadOnlyList<SweepVariable> All { get; } =
    [
        // Inputs: what a grid row can set.
        Speed,
        new("Spark advance", "°BTDC", e => -e.Cylinder.ThetaSpark),
        new("Burn angle", "°CA", e => e.Cylinder.Fuel.BurnAngle),
        new("Inlet valve opens", "°BTDC", e => EngineFileUnits.ToInletOpen(e.Manifold.InletValve.OpenAngle)),
        new("Inlet valve closes", "°ABDC", e => EngineFileUnits.ToInletClose(e.Manifold.InletValve.CloseAngle)),
        new("Exhaust valve opens", "°BBDC", e => EngineFileUnits.ToExhaustOpen(e.Manifold.ExhaustValve.OpenAngle)),
        new("Exhaust valve closes", "°ATDC", e => EngineFileUnits.ToExhaustClose(e.Manifold.ExhaustValve.CloseAngle)),
        new("Inlet valve lift", "mm", e => EngineFileUnits.ToMillimetres(e.Manifold.InletValve.MaxLift)),
        new("Exhaust valve lift", "mm", e => EngineFileUnits.ToMillimetres(e.Manifold.ExhaustValve.MaxLift)),

        // Outputs: what the run produced.
        Torque,
        new("Brake power", "kW", e => e.BrakePower / 1e3),
        new("IMEP", "bar", e => e.Imep / 1e5),
        new("BMEP", "bar", e => e.Bmep / 1e5),
        new("FMEP", "bar", e => e.Fmep / 1e5),
        new("PMEP", "bar", e => e.Pmep / 1e5),
        new("Volumetric efficiency", "%", e => e.VolumetricEfficiency),
        new("Thermal efficiency", "%", e => e.ThermalEfficiency),
        new("Mechanical efficiency", "%", e => e.MechanicalEfficiency),
        new("SFC", "g/kWh", e => e.Sfc),
        new("Fuel flow", "kg/h", e => e.FuelMassFlow),
        new("Trapped mass", "mg", e => e.TotalMass * 1e6),
        new("Peak cylinder pressure", "bar", e => e.PeakPressure / 1e5),
        new("Peak temperature", "K", e => e.PeakTemperature),
    ];
}
