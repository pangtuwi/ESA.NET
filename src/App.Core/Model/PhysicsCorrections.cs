namespace App.Core.Model;

/// <summary>Which physics a run uses. See <c>CORRECTIONS.md</c>.</summary>
public enum PhysicsMode
{
    /// <summary>
    /// The original's behaviour, defects in section B of <c>ISSUES.md</c> included. What
    /// <c>data/baseline/</c> was produced by and what the port is validated against.
    /// </summary>
    Legacy,

    /// <summary>Every correction in <see cref="CorrectionCatalogue"/> applied.</summary>
    Corrected,
}

/// <summary>
/// One correction to a legacy defect that moves results, named for its <c>ISSUES.md</c>
/// entry. These are tier 3 of <c>CORRECTIONS.md</c>; tiers 1 and 2 are fixed outright and
/// have no entry here.
/// </summary>
/// <param name="Entry">The <c>ISSUES.md</c> number, such as <c>B14</c>, and its <c>ESA.ini</c> key.</param>
/// <param name="Summary">What the correction changes, in a line.</param>
public sealed record Correction(string Entry, string Summary);

/// <summary>
/// The corrections that exist. Each tier 3 correction adds its entry here when it lands,
/// together with the code that reads it; until then a flag would switch nothing.
/// </summary>
public static class CorrectionCatalogue
{
    /// <summary>
    /// ISSUES.md B14: the RKF5 tableau's fifth stage uses Fehlberg's published
    /// <c>845/4104</c> instead of the original's transposed <c>854/4104</c>, restoring
    /// fifth-order convergence where the original's method is first order.
    /// </summary>
    public static Correction Rkf5Coefficient { get; } =
        new("B14", "RKF5 fifth stage uses Fehlberg's 845/4104, not the transposed 854/4104");

    /// <summary>Every correction, in the order they landed.</summary>
    public static IReadOnlyList<Correction> All { get; } = [Rkf5Coefficient];
}

/// <summary>
/// The Legacy/Corrected switch: a mode, and any per-correction overrides of it.
/// </summary>
/// <remarks>
/// <para>
/// Agreed in <c>CORRECTIONS.md</c> section 5. The operator chooses Legacy or Corrected in
/// the Single Speed Simulation dialog; individual corrections can be overridden only in
/// <c>ESA.ini</c>, under <c>[Physics]</c>, for whoever is working on the physics. Legacy
/// is the default while tier 3 is in progress, and becomes Corrected in one step once the
/// last correction has landed.
/// </para>
/// <para>
/// Never stored in the <c>.eng</c>: corrections describe the simulator, not the engine.
/// </para>
/// </remarks>
public sealed class PhysicsCorrections
{
    /// <summary>The mode every correction follows unless overridden.</summary>
    public PhysicsMode Mode { get; set; } = PhysicsMode.Legacy;

    /// <summary>
    /// Per-correction overrides of <see cref="Mode"/>, keyed by <c>ISSUES.md</c> entry and
    /// matched case-insensitively. An entry not in the catalogue is kept, so an
    /// <c>ESA.ini</c> written for a later version survives being read by this one.
    /// </summary>
    public IDictionary<string, bool> Overrides { get; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="correction"/> applies: its override if there is one, otherwise the mode.</summary>
    public bool IsOn(Correction correction)
    {
        ArgumentNullException.ThrowIfNull(correction);

        return Overrides.TryGetValue(correction.Entry, out var on) ? on : Mode == PhysicsMode.Corrected;
    }

    /// <summary>The corrections that apply, in catalogue order.</summary>
    public IReadOnlyList<Correction> Active => [.. CorrectionCatalogue.All.Where(IsOn)];

    /// <summary>
    /// The physics in a line, for <c>run.txt</c>: the mode, and which corrections were on
    /// when that is not simply all or none of them.
    /// </summary>
    public string Describe()
    {
        var active = Active;
        var all = CorrectionCatalogue.All;

        var mode = Mode == PhysicsMode.Legacy ? "Legacy" : "Corrected";

        if (all.Count == 0)
        {
            return Mode == PhysicsMode.Legacy
                ? "Legacy"
                : "Corrected (no corrections implemented yet, so the same as Legacy)";
        }

        if (active.Count == 0)
        {
            return $"{mode}, no corrections on";
        }

        return active.Count == all.Count
            ? $"{mode}, all {all.Count} corrections on"
            : $"{mode}, corrections on: {string.Join(", ", active.Select(c => c.Entry))}";
    }

    /// <summary>An independent copy, for a run to keep what it was asked to do.</summary>
    public PhysicsCorrections Clone()
    {
        var copy = new PhysicsCorrections { Mode = Mode };

        foreach (var (entry, on) in Overrides)
        {
            copy.Overrides[entry] = on;
        }

        return copy;
    }
}
