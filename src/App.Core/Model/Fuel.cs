namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TFuel</c> (Fuel.pas). Elemental composition is integer, all
/// other quantities are double.
/// </summary>
public sealed class Fuel
{
    /// <summary>Calorific value, <c>Q</c>.</summary>
    public double Q { get; set; }

    /// <summary>Fuel temperature, <c>T</c>.</summary>
    public double T { get; set; }

    /// <summary>
    /// Air-fuel ratio expressed as X:1. SPEC.md section 5 records that fuel mass
    /// divides by <c>AFRatio + 1</c> because total mixture mass is X+1 parts.
    /// </summary>
    public double AFRatio { get; set; }

    public double Lambda { get; set; }

    public double BurnAngle { get; set; }

    /// <summary>
    /// Crank angle from the spark to the start of combustion, in degrees: the flame
    /// development the original's burn law has no place for. No Delphi counterpart; read
    /// from an optional <c>[Fuel] CombustionDelay</c> key, null when absent, and used only
    /// under B89. Absent, B90 supplies a default from the flame-development angle, and
    /// without B90 the delay is 0 (ISSUES.md B89, B90).
    /// </summary>
    public double? CombustionDelay { get; set; }

    /// <summary>
    /// The fraction of the fuel's energy the burn releases, or null for
    /// <see cref="App.Core.Simulation.CombustionLoss.DefaultEfficiency"/>. No Delphi counterpart; read from
    /// an optional <c>[Fuel] CombustionEfficiency</c> key, never written, and used only under
    /// B92 (ISSUES.md B92).
    /// </summary>
    public double? CombustionEfficiency { get; set; }

    /// <summary>Fuel mass, <c>m</c>.</summary>
    public double M { get; set; }

    public int C { get; set; }

    public int H { get; set; }

    public int O { get; set; }

    public int N { get; set; }
}
