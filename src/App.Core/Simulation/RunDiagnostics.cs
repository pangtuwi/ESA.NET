using App.Core.Manifold;
using App.Core.Thermo;

namespace App.Core.Simulation;

/// <summary>
/// What the solvers had to do, or failed to do, over one run, gathered for the operator.
/// </summary>
/// <remarks>
/// The original either stopped, hung, popped a dialog or said nothing in each of these
/// cases (ISSUES.md B20, B21, B22, B51, B52, B53). The port carries on where the original was
/// evidently meant to, and counts every occasion here, so that a result produced through
/// any of them says so.
/// </remarks>
/// <param name="EquilibriumSolves">Burnt-gas equilibrium solves across every gas zone.</param>
/// <param name="EquilibriumTemperatureClamps">Solves above the 4000 K top of the curve fits (B21).</param>
/// <param name="EquilibriumCapHits">Solves whose Newton iteration reached its cap.</param>
/// <param name="EquilibriumEstimateCapHits">Initial estimates that reached their cap.</param>
/// <param name="GasPropertyTemperatureClamps">
/// Species curve-fit evaluations outside 260-5000 K, answered at the nearer end of the fit,
/// where the original terminated the run (B20, A19).
/// </param>
/// <param name="Manifold">The wave solver's counters (B51-B53).</param>
public sealed record RunDiagnostics(
    long EquilibriumSolves,
    long EquilibriumTemperatureClamps,
    long EquilibriumCapHits,
    long EquilibriumEstimateCapHits,
    long GasPropertyTemperatureClamps,
    ManifoldDiagnostics Manifold)
{
    /// <summary>Gathers the counters from a run's solvers.</summary>
    public static RunDiagnostics From(
        IEnumerable<EquilibriumDiagnostics> equilibrium,
        long gasPropertyTemperatureClamps,
        ManifoldDiagnostics manifold)
    {
        ArgumentNullException.ThrowIfNull(equilibrium);
        ArgumentNullException.ThrowIfNull(manifold);

        var solvers = equilibrium.ToList();

        return new RunDiagnostics(
            solvers.Sum(d => d.Solves),
            solvers.Sum(d => d.TemperatureClamps),
            solvers.Sum(d => d.EquilibriumCapHits),
            solvers.Sum(d => d.InitialEstimateCapHits),
            gasPropertyTemperatureClamps,
            manifold);
    }

    /// <summary>
    /// Whether anything was counted that should qualify the result: a clamp, a cap reached
    /// or a negative state carried through.
    /// </summary>
    public bool HasWarnings =>
        EquilibriumTemperatureClamps > 0
        || EquilibriumCapHits > 0
        || EquilibriumEstimateCapHits > 0
        || GasPropertyTemperatureClamps > 0
        || Manifold.HasWarnings;

    /// <summary>The warnings in a sentence, for a status line, or empty if there are none.</summary>
    public string Summary()
    {
        var parts = new List<string>();

        void Add(long count, string what)
        {
            if (count > 0)
            {
                parts.Add($"{count} {what}");
            }
        }

        Add(EquilibriumTemperatureClamps, "equilibrium solve(s) above 4000 K");
        Add(EquilibriumCapHits, "equilibrium iteration(s) at their cap");
        Add(EquilibriumEstimateCapHits, "equilibrium estimate(s) at their cap");
        Add(GasPropertyTemperatureClamps, "gas-property evaluation(s) outside 260-5000 K");
        Add(Manifold.OuterIterationCapHits, "wave-solver point(s) not converged");
        Add(Manifold.FootLoopCapHits, "characteristic foot search(es) at their cap");
        Add(Manifold.NegativeFootStates, "negative pressure or density state(s) in the pipes");

        return string.Join(", ", parts);
    }
}
