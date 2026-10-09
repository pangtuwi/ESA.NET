namespace App.Core.Manifold;

/// <summary>
/// Counters recording where the wave solver's interior-point iteration did not do what it
/// was asked. Each one replaces something the original did instead of telling anyone.
/// </summary>
/// <remarks>
/// <para>
/// <c>INTERNAL_PIPE</c> hangs when a characteristic foot never settles (ISSUES.md B51),
/// silently takes the last pass as the answer when the outer iteration gives up (B52), and
/// raises a modal dialog per grid point per time step when a foot's mean pressure or
/// density goes negative, then carries on regardless (B53). The port caps the foot loops,
/// takes the same last pass, and drops the dialog. These counters are how anyone finds out
/// that any of it happened.
/// </para>
/// <para>
/// One instance per <see cref="ManifoldSolver"/>, so one per run, with no shared state.
/// </para>
/// </remarks>
public sealed class ManifoldDiagnostics
{
    /// <summary>Interior-point updates computed, one per grid point per time step.</summary>
    public long InteriorPoints { get; internal set; }

    /// <summary>
    /// Characteristic foot searches that reached their cap without settling within
    /// 0.1 mm. The original's loops had no cap and would have hung here (ISSUES.md B51).
    /// </summary>
    public long FootLoopCapHits { get; internal set; }

    /// <summary>
    /// Interior points whose outer iteration reached its cap without converging and took
    /// the last pass as the answer (ISSUES.md B52).
    /// </summary>
    public long OuterIterationCapHits { get; internal set; }

    /// <summary>The most outer iterations any single interior point needed.</summary>
    public int WorstOuterIterations { get; internal set; }

    /// <summary>
    /// Characteristic foot evaluations whose mean pressure or density was negative, where
    /// the original raised a dialog and carried on (ISSUES.md B53).
    /// </summary>
    public long NegativeFootStates { get; internal set; }

    /// <summary>
    /// Exhaust-valve solves that settled on a flow only a guard's pin sustained: outflow held
    /// by the throat pinned just under the cylinder, or inflow held by the stagnation pinned
    /// just over it. Either keeps the flow from ever reversing (ISSUES.md B81). Counted when
    /// the latch is kept.
    /// </summary>
    public long ExhaustValveLatches { get; internal set; }

    /// <summary>The same latches, released by B81. Not a warning.</summary>
    public long ExhaustValveLatchReleases { get; internal set; }

    /// <summary>
    /// Inlet-valve forward solves that settled on an inflow only the forward guard's pin
    /// sustained: the pipe end pinned just above a cylinder that has risen above the pipe
    /// (ISSUES.md B82). Counted when the latch is kept.
    /// </summary>
    public long InletValveLatches { get; internal set; }

    /// <summary>The same latches, released by B82. Not a warning.</summary>
    public long InletValveLatchReleases { get; internal set; }

    /// <summary>Whether anything was counted that an operator should hear about.</summary>
    public bool HasWarnings =>
        FootLoopCapHits > 0 || OuterIterationCapHits > 0 || NegativeFootStates > 0;

    public override string ToString() =>
        $"{InteriorPoints} interior points, worst {WorstOuterIterations} outer iterations, "
        + $"{OuterIterationCapHits} outer cap hits, {FootLoopCapHits} foot cap hits, "
        + $"{NegativeFootStates} negative foot states";
}
