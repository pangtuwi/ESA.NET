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

    /// <summary>
    /// ISSUES.md B46: the two-zone mass-flow derivatives are zeroed when the inlet valve
    /// closes. The original leaves gas exchange's last values on them, so the closed
    /// cylinder's burnt-down equations behave as though gas were still leaving it.
    /// </summary>
    public static Correction ClosedCylinderMassFlow { get; } =
        new("B46", "Mass-flow derivatives are zeroed through the closed period, not left stale from gas exchange");

    /// <summary>
    /// ISSUES.md B32: the Woschni motored pressure is taken at the angle of the state it
    /// is compared with. The original takes it at the step's start, so at five of RKF5's
    /// six stages it lags the trial state.
    /// </summary>
    public static Correction WoschniMotoredAngle { get; } =
        new("B32", "Woschni motored pressure at the trial state's own crank angle, not the step's start");

    /// <summary>
    /// ISSUES.md B33: the Woschni velocity uses the cylinder's swept volume, not
    /// <c>VCyl(pi) * CR/(CR+1)</c>, which is 1.2 per cent larger at a compression ratio of 9.2.
    /// </summary>
    public static Correction WoschniSweptVolume { get; } =
        new("B33", "Woschni velocity uses the swept volume, not VCyl(pi) * CR/(CR+1)");

    /// <summary>
    /// ISSUES.md B31: Woschni's pressure-rise term is floored at zero, so the heat-transfer
    /// coefficient never collapses to zero when the pressure falls below motored.
    /// </summary>
    public static Correction WoschniNegativeVelocity { get; } =
        new("B31", "Woschni pressure-rise term floored at zero, so h never collapses to zero below motored");

    /// <summary>
    /// ISSUES.md B38: Woschni's reference conditions are the cylinder's state at each
    /// inlet valve closing, not the plenum's at initialisation.
    /// </summary>
    public static Correction IvcReference { get; } =
        new("B38", "Woschni reference conditions taken at each inlet valve closing, not fixed at initialisation");

    /// <summary>
    /// ISSUES.md B75: Woschni's pressure-rise term applies in combustion and expansion
    /// only; the original applies it through compression and gas exchange as well.
    /// </summary>
    public static Correction WoschniCombustionTerm { get; } =
        new("B75", "Woschni pressure-rise term in combustion and expansion only, as published");

    /// <summary>
    /// ISSUES.md B50: the wave solver runs on the gammas <c>InitVars</c> computes from the
    /// equilibrium property model, not on the hard-coded 1.3994 and 1.3, and
    /// <c>MassFlow</c> uses each pipe's own rather than 1.3994 for both.
    /// </summary>
    public static Correction ManifoldGammas { get; } =
        new("B50", "Wave solver runs on the computed inlet and exhaust gammas, not 1.3994 and 1.3");

    /// <summary>
    /// ISSUES.md B54: the inlet's closed-valve interpolant is built from the imposed wall
    /// velocity, as the exhaust's is, not from the velocity left at the wall while it was open.
    /// </summary>
    public static Correction ClosedValveWallVelocity { get; } =
        new("B54", "Inlet closed-valve interpolant uses the imposed wall velocity, as the exhaust's does");

    /// <summary>
    /// ISSUES.md B55: the inlet's open end requires density to converge as well as velocity
    /// and pressure, as the exhaust's does.
    /// </summary>
    public static Correction OpenEndDensityConvergence { get; } =
        new("B55", "Inlet open end converges on density too, as the exhaust's does");

    /// <summary>
    /// ISSUES.md B56: both sonic entrance-velocity solvers bracket the whole range below the
    /// throat velocity, where the original used 0.6 of it at the inlet and 0.8 at the exhaust.
    /// </summary>
    public static Correction SonicEntranceBracket { get; } =
        new("B56", "Sonic entrance velocity bracketed over the whole subsonic range at both valves");

    /// <summary>
    /// ISSUES.md B59: inlet reverse flow stalls when the cylinder is at or below the throat
    /// pressure, instead of being nudged past the no-flow branch into a subsonic solve at a
    /// ratio of 1.000001.
    /// </summary>
    public static Correction InletReverseStall { get; } =
        new("B59", "Inlet reverse flow stalls when the cylinder is at or below the throat");

    /// <summary>
    /// ISSUES.md B61: the exhaust reverse substitution branch stops the loop, as the inlet's
    /// does, so its throat relaxation is applied once and not twice.
    /// </summary>
    public static Correction ExhaustReverseSingleRelaxation { get; } =
        new("B61", "Exhaust reverse substitution relaxes the throat once, as the inlet's does");

    /// <summary>
    /// ISSUES.md B62: the exhaust reverse routine decides choking on the pipe's stagnation
    /// pressure, the one it builds the throat from, not on the static pipe-end pressure.
    /// </summary>
    public static Correction ExhaustReverseStagnationChoke { get; } =
        new("B62", "Exhaust reverse choking decided on the pipe's stagnation pressure");

    /// <summary>Every correction, in the order they landed.</summary>
    public static IReadOnlyList<Correction> All { get; } =
    [
        Rkf5Coefficient, ClosedCylinderMassFlow, WoschniMotoredAngle, WoschniSweptVolume,
        WoschniNegativeVelocity, IvcReference, WoschniCombustionTerm, ManifoldGammas,
        ClosedValveWallVelocity, OpenEndDensityConvergence, SonicEntranceBracket,
        InletReverseStall, ExhaustReverseSingleRelaxation, ExhaustReverseStagnationChoke,
    ];
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
