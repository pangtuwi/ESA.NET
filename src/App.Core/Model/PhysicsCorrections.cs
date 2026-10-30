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
/// <param name="InCorrected">
/// Whether Corrected runs it. False for a correction that is sound physics somewhere but fails a
/// measured oracle where the suite runs (B90, ISSUES.md A40), or that replaces what turned out to
/// be the thesis's own design (B34); <c>ESA.ini</c> can still turn either on.
/// </param>
public sealed record Correction(string Entry, string Summary, bool InCorrected = true);

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

    /// <summary>
    /// ISSUES.md B64: the exhaust open routine's Mach-matching secant takes its first probe
    /// upward by 1.001, as the other three valve routines do, not downward by 0.99999.
    /// </summary>
    public static Correction SecantProbe { get; } =
        new("B64", "Exhaust open secant probes upward by 1.001, as the other three valve routines do");

    /// <summary>
    /// ISSUES.md B65: the inlet temperature reported to the plenum is the one the wave solver
    /// holds at the valve end, <c>c^2 / (gamma R)</c>, not the starting plenum temperature.
    /// </summary>
    public static Correction LiveInletTemperature { get; } =
        new("B65", "Inlet temperature reported from the wave solver's state, not frozen at the start");

    /// <summary>
    /// ISSUES.md B4: a pipe's area holds its last value past the end of the <c>.maf</c>
    /// table, instead of falling to zero, and the end-of-pipe gradient finds the end by
    /// position.
    /// </summary>
    public static Correction AreaClamp { get; } =
        new("B4", "Pipe area holds its last value past the end of the table, not zero");

    /// <summary>
    /// ISSUES.md B16 (with B17): the burnt <c>dudp</c> from the equilibrium solver's
    /// pressure derivatives, in per-pascal units and with the right molecular weight, in
    /// place of a central difference costing two more solves.
    /// </summary>
    public static Correction AnalyticPressureDerivative { get; } =
        new("B16", "Burnt dudp from the solver's pressure derivatives, not a central difference");

    /// <summary>
    /// ISSUES.md B18: the unburnt charge's residual mole fraction from the residual's own
    /// molecular weight, so it is right on the first call and every call after.
    /// </summary>
    public static Correction ResidualMolecularWeight { get; } =
        new("B18", "Unburnt residual fraction from the residual's molecular weight, in closed form");

    /// <summary>
    /// ISSUES.md B35: the single-zone pressure equation - cycle one, and two-zone overlap
    /// through B37 - honours <c>VariableGamma</c>: the cylinder's own gamma, or 1.35 with
    /// it off, in place of a hard-coded 1.4.
    /// </summary>
    public static Correction SingleZoneGamma { get; } =
        new("B35", "Single-zone pressure equation on the cylinder's own gamma, honouring Variable Gamma");

    /// <summary>
    /// ISSUES.md B76: the single-zone pressure equation reads the integrator's trial
    /// pressure and the volume at its own angle, not the state the last step left behind.
    /// </summary>
    public static Correction SingleZoneTrialState { get; } =
        new("B76", "Single-zone pressure equation on the trial state, not the last step's");

    /// <summary>
    /// ISSUES.md B77: after each step the gas is updated at the angle the solution belongs
    /// to, the step's end, not the angle the step started at; work and heat loss are the
    /// trapezoid of the step's two ends; and the PVT trace files each row at that angle.
    /// </summary>
    public static Correction EndOfStepState { get; } =
        new("B77", "Gas updated at the angle the step finished at; work and heat by the trapezoid; trace rows at that angle");

    /// <summary>
    /// ISSUES.md B78: the burnt volume starts every burn from zero, as it does in the first
    /// two-zone cycle, instead of from the previous burn's end-of-burn volume.
    /// </summary>
    public static Correction BurntVolumeReset { get; } =
        new("B78", "Burnt volume reset to zero at every combustion entry, not carried over from the last burn");

    /// <summary>
    /// ISSUES.md B37: valve overlap runs two zones, the burnt residual and the fresh charge,
    /// through the original's gas-exchange equations, with each step's flows through both
    /// valves put into the zones at their own enthalpies, instead of one gas on the
    /// single-zone pressure equation.
    /// </summary>
    public static Correction GasExchangeZones { get; } =
        new("B37", "Overlap as two zones, burnt residual and fresh charge, each flow at its own enthalpy");

    /// <summary>
    /// ISSUES.md B79: overlap's valve totals count each flow once. A return larger than
    /// what the valve's port holds, or an outflow larger than the zone it is drawn from,
    /// is split between the two kinds of gas instead of being counted whole in one total
    /// and again in the other's port.
    /// </summary>
    public static Correction OverlapValveTotals { get; } =
        new("B79", "Overlap's valve totals count each flow once, splitting returns and outflows that cross zones");

    /// <summary>
    /// ISSUES.md B81: an exhaust-valve flow that settles only because a guard pinned the
    /// throat or stagnation pressure a hair either side of the cylinder's is released to the
    /// other direction, instead of latching there.
    /// </summary>
    public static Correction ExhaustValveLatch { get; } =
        new("B81", "Exhaust-valve flow held only by a guard's pin is released, not latched");

    /// <summary>
    /// ISSUES.md B82: an inlet-valve inflow that settles only because the forward guard
    /// pinned the pipe end a hair above the cylinder is released to the reverse routine,
    /// instead of latching there - the inlet's counterpart of B81.
    /// </summary>
    public static Correction InletValveLatch { get; } =
        new("B82", "Inlet-valve inflow held only by the forward guard's pin is released, not latched");

    /// <summary>
    /// ISSUES.md B83: an area step narrower than the original's plus-and-minus-two-millimetre
    /// area gradient is a junction between two segments, conserving mass and energy, instead
    /// of a gradient source a characteristic foot can land on.
    /// </summary>
    public static Correction AreaJunctions { get; } =
        new("B83", "Abrupt area steps are junctions conserving mass and energy, not a gradient source");

    /// <summary>
    /// ISSUES.md B84: a valve that has just opened starts from a throat at rest at the
    /// cylinder's pressure, instead of the state the previous valve event left behind.
    /// </summary>
    public static Correction FreshValveThroat { get; } =
        new("B84", "A valve that has just opened starts from a throat at rest at cylinder pressure, not the last event's throat");

    /// <summary>
    /// ISSUES.md B85: the throat velocity solves bracket the entrance velocity from zero
    /// rather than a millionth of the throat velocity, which a nearly shut valve's root
    /// falls below.
    /// </summary>
    public static Correction ThroatSolveFromZero { get; } =
        new("B85", "The throat velocity solves bracket from zero, so a nearly shut valve's root is inside");

    /// <summary>
    /// ISSUES.md B86: convergence is not tested until a two-zone cycle has run, because the
    /// warm-up cycle's own inlet and exhaust totals agreeing is a coincidence, not
    /// convergence.
    /// </summary>
    public static Correction WarmUpConvergence { get; } =
        new("B86", "Convergence is not tested until a two-zone cycle has run: the warm-up cycle's balance is a coincidence");

    /// <summary>
    /// ISSUES.md B87: BMEP, and torque with it, is the cycle's net indicated work less the
    /// friction correlation, not the work from inlet closing to exhaust opening less it,
    /// which made torque depend on the valve timings through the window.
    /// </summary>
    public static Correction NetWorkTorque { get; } =
        new("B87", "BMEP is the cycle's net indicated work less the friction correlation, not the work from inlet closing to exhaust opening");

    /// <summary>
    /// ISSUES.md B88: friction is the mechanical and auxiliary friction of Sandoval and
    /// Heywood's model, from the engine's design, with pumping from the simulated cycle only,
    /// not the Barnes-Moss curve in speed, which carries pumping of its own.
    /// </summary>
    public static Correction MechanicalFriction { get; } =
        new("B88", "Friction is a mechanical model from the engine's design, with pumping from the simulated cycle, not a total-friction curve in speed");

    /// <summary>
    /// ISSUES.md B89: the burn is a Wiebe law from the start of combustion, the spark plus an
    /// optional delay, not the original's raised cosine from the spark itself.
    /// </summary>
    public static Correction WiebeBurn { get; } =
        new("B89", "The burn is a Wiebe law from the spark plus an optional delay, not a raised cosine from the spark");

    /// <summary>
    /// ISSUES.md B90: with no delay in the file, the Wiebe burn starts where its 10 per cent
    /// point falls at Heywood's flame-development angle for the running speed, not at the
    /// spark. Not run by Corrected: the angle was measured at part throttle, and at full
    /// throttle it puts the best-torque spark 10 to 26 degrees beyond the engines' measured
    /// one (ISSUES.md A40). <c>B90=1</c> in <c>ESA.ini</c> turns it on.
    /// </summary>
    public static Correction FlameDevelopmentDelay { get; } =
        new("B90", "With no delay given, the burn starts so that 10 per cent is burnt at the flame-development angle for the speed", InCorrected: false);

    /// <summary>
    /// ISSUES.md B91: the exhaust valve's choked reverse-flow Mach solve brackets the
    /// area-Mach relation's subsonic root on its analytic bounds, not 0.45 to 0.75 of the
    /// area ratio.
    /// </summary>
    public static Correction SonicMachBracket { get; } =
        new("B91", "The exhaust sonic Mach solve brackets the area-Mach root on its analytic bounds");

    /// <summary>
    /// ISSUES.md B34: during combustion each zone loses heat through its volume share of
    /// piston, head and liner, not the burnt zone through piston and head alone and the
    /// unburnt zone through the whole liner. Not run by Corrected: the original's split
    /// approximates the thesis's own flame-geometry model (section 2.7.4.3, Table 2.1), which
    /// gives the burnt zone no liner until its radius reaches the bore, so it was a design,
    /// not a defect. <c>B34=1</c> in <c>ESA.ini</c> turns the volume split on.
    /// </summary>
    public static Correction CombustionWallShare { get; } =
        new("B34", "During the burn each zone loses heat through its volume share of every surface, the liner included", InCorrected: false);

    /// <summary>
    /// ISSUES.md B92: the burn releases a combustion efficiency's share of the fuel's energy,
    /// Heywood's 95 to 98 per cent for a spark-ignition engine, not all of it; the rest leaves
    /// with the exhaust as unburnt fuel.
    /// </summary>
    public static Correction CombustionEfficiency { get; } =
        new("B92", "The burn releases a combustion efficiency's share of the fuel energy, not all of it");

    /// <summary>
    /// ISSUES.md B93: the mass through a valve is the throat flow over the area the valve
    /// boundary solved for, the valve's flow area capped at the port's, not over the whole
    /// valve area. The boundaries cap the area ratio at one, so the original credited the
    /// cylinder with mass the pipe never delivered whenever the valve's area exceeded the port's.
    /// </summary>
    public static Correction ValveFlowContinuity { get; } =
        new("B93", "Mass through a valve is the throat flow over the area its boundary solved, capped at the port's");

    /// <summary>
    /// ISSUES.md B94: choked flow from the cylinder into a pipe enters it at the velocity that
    /// satisfies continuity with the sonic throat on the pipe's characteristic, not at the
    /// sudden expansion's momentum-balance velocity, which left the pipe a third to a half of
    /// the exhaust blowdown.
    /// </summary>
    public static Correction ChokedDischargeContinuity { get; } =
        new("B94", "Choked flow from the cylinder enters the pipe on its characteristic with continuity, not at the momentum-balance velocity");

    /// <summary>
    /// ISSUES.md B86: whether the mass-balance test may stop a run that has completed
    /// <paramref name="completedCycles"/> cycles. The original tests from the first; under
    /// B86 not until the first cycle after the single-zone warm-up has run, and never on a
    /// run's first cycle, which starts from <c>InitVars</c>' guesses.
    /// </summary>
    public static bool MayConverge(PhysicsCorrections physics, int completedCycles, int oneZoneCycles) =>
        !physics.IsOn(WarmUpConvergence) || completedCycles >= Math.Max(2, oneZoneCycles + 1);

    /// <summary>Every correction, in the order they landed.</summary>
    public static IReadOnlyList<Correction> All { get; } =
    [
        Rkf5Coefficient, ClosedCylinderMassFlow, WoschniMotoredAngle, WoschniSweptVolume,
        WoschniNegativeVelocity, IvcReference, WoschniCombustionTerm, ManifoldGammas,
        ClosedValveWallVelocity, OpenEndDensityConvergence, SonicEntranceBracket,
        InletReverseStall, ExhaustReverseSingleRelaxation, ExhaustReverseStagnationChoke,
        SecantProbe, LiveInletTemperature, AreaClamp, AnalyticPressureDerivative,
        ResidualMolecularWeight, SingleZoneGamma, SingleZoneTrialState, EndOfStepState,
        BurntVolumeReset, GasExchangeZones, OverlapValveTotals, ExhaustValveLatch, InletValveLatch,
        AreaJunctions, FreshValveThroat, ThroatSolveFromZero, WarmUpConvergence, NetWorkTorque,
        MechanicalFriction, WiebeBurn, SonicMachBracket, FlameDevelopmentDelay, CombustionWallShare,
        CombustionEfficiency, ValveFlowContinuity, ChokedDischargeContinuity,
    ];
}

/// <summary>
/// The Legacy/Corrected switch: a mode, and any per-correction overrides of it.
/// </summary>
/// <remarks>
/// <para>
/// Agreed in <c>CORRECTIONS.md</c> section 5. The operator chooses Legacy or Corrected in
/// the Single Speed Simulation dialog; individual corrections can be overridden only in
/// <c>ESA.ini</c>, under <c>[Physics]</c>, for whoever is working on the physics.
/// Corrected is the default, since the last tier 3 correction landed; Legacy, the
/// original's physics as <c>data/baseline/</c> validates it, has to be asked for.
/// </para>
/// <para>
/// Never stored in the <c>.eng</c>: corrections describe the simulator, not the engine.
/// </para>
/// </remarks>
public sealed class PhysicsCorrections
{
    /// <summary>
    /// The mode a run uses unless something says otherwise: Corrected, with Legacy opt-in
    /// (<c>CORRECTIONS.md</c> section 5).
    /// </summary>
    public const PhysicsMode DefaultMode = PhysicsMode.Corrected;

    /// <summary>The mode every correction follows unless overridden.</summary>
    public PhysicsMode Mode { get; set; } = DefaultMode;

    /// <summary>
    /// Per-correction overrides of <see cref="Mode"/>, keyed by <c>ISSUES.md</c> entry and
    /// matched case-insensitively. An entry not in the catalogue is kept, so an
    /// <c>ESA.ini</c> written for a later version survives being read by this one.
    /// </summary>
    public IDictionary<string, bool> Overrides { get; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="correction"/> applies: its override if there is one, otherwise
    /// the mode, Corrected running every correction it is meant to.
    /// </summary>
    public bool IsOn(Correction correction)
    {
        ArgumentNullException.ThrowIfNull(correction);

        return Overrides.TryGetValue(correction.Entry, out var on)
            ? on
            : Mode == PhysicsMode.Corrected && correction.InCorrected;
    }

    /// <summary>The corrections that apply, in catalogue order.</summary>
    public IReadOnlyList<Correction> Active => [.. CorrectionCatalogue.All.Where(IsOn)];

    /// <summary>
    /// The physics in a line, for <c>run.txt</c>: the mode, and which corrections were on
    /// when that is not simply the mode's own set.
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

        if (active.Count == all.Count)
        {
            return $"{mode}, all {all.Count} corrections on";
        }

        var corrected = all.Where(c => c.InCorrected).ToList();

        return active.SequenceEqual(corrected)
            ? $"{mode}, all {corrected.Count} of its corrections on; off: {string.Join(", ", all.Except(corrected).Select(c => c.Entry))}"
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
