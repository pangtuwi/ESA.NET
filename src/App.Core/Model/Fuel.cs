namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TFuel</c> (Fuel.pas). The Delphi composition is integer; here it is
/// double, so that a fuel such as Heywood's gasoline, C8.26H15.5, can be given (ISSUES.md B96).
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

    public double C { get; set; }

    public double H { get; set; }

    public double O { get; set; }

    public double N { get; set; }

    /// <summary>
    /// Whether the engine file gave the composition. Without it the fuel is the default, the
    /// C7H17 surrogate in Legacy and Heywood's gasoline in Corrected (ISSUES.md B95). No Delphi
    /// counterpart.
    /// </summary>
    public bool CompositionGiven { get; set; }
}
