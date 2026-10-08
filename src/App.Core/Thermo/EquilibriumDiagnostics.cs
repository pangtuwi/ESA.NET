namespace App.Core.Thermo;

/// <summary>
/// Counters recording how hard the equilibrium solver had to work.
/// </summary>
/// <remarks>
/// Kept because the solver's convergence tests are <b>absolute</b> — <c>tol &lt; 0.0004</c>
/// on a variable that ranges over thirty orders of magnitude — and Delphi ran them in
/// 80-bit <c>Extended</c> where this port has 53-bit <c>double</c>. The failure mode
/// that narrowing produces is not a drifting last digit: it is a different iteration
/// count, with the answer being whatever the loop happened to stop at. A shifting
/// distribution here is the early warning that precision is starting to bite.
/// </remarks>
public sealed class EquilibriumDiagnostics
{
    /// <summary>Calls to <see cref="EquilibriumSolver.Solve"/> that ran the solver.</summary>
    public long Solves { get; internal set; }

    /// <summary>Calls that returned immediately because the model was frozen.</summary>
    public long FrozenSkips { get; internal set; }

    /// <summary>Total decade steps taken hunting a bracket for the oxygen estimate.</summary>
    public long BracketSteps { get; internal set; }

    /// <summary>Total Newton iterations inside the initial estimate.</summary>
    public long InitialEstimateIterations { get; internal set; }

    /// <summary>Times the initial estimate hit its 20-iteration cap.</summary>
    public long InitialEstimateCapHits { get; internal set; }

    /// <summary>Total Newton iterations inside the main equilibrium loop.</summary>
    public long EquilibriumIterations { get; internal set; }

    /// <summary>Times the main loop hit its 25-iteration cap.</summary>
    public long EquilibriumCapHits { get; internal set; }

    /// <summary>The largest iteration count any single main-loop solve needed.</summary>
    public int WorstEquilibriumIterations { get; internal set; }

    /// <summary>
    /// Solves whose temperature lay above the 4000 K top of the equilibrium-constant curve
    /// fits, and were evaluated at 4000 K. The original's <c>KEquilib</c> was written to
    /// clamp, but raised first, so this was fatal (ISSUES.md B21). Below the fits' 600 K
    /// floor cannot arise: <see cref="EquilibriumSolver.Solve"/> pins anything under
    /// 1000 K first.
    /// </summary>
    public long TemperatureClamps { get; internal set; }

    /// <summary>
    /// The original's error 2, "Initial Estimate Predicts Extremely Low O2". Still fatal;
    /// counted because the original's counters for it were unreachable (ISSUES.md B22).
    /// </summary>
    public long LowOxygenErrors { get; internal set; }

    /// <summary>The original's error 3, insufficient resolution from the matrix solver. Still fatal (B22).</summary>
    public long ResolutionErrors { get; internal set; }

    /// <summary>
    /// Matrix solves that double precision could not resolve and that were redone in
    /// double-double, standing in for the original's 80-bit <c>Extended</c>. Each one
    /// would otherwise have been a resolution error (ISSUES.md A30).
    /// </summary>
    public long ExtendedPrecisionReductions { get; internal set; }

    /// <summary>The original's error 5, negative mole fractions after iterating. Still fatal (B22).</summary>
    public long NegativeFractionErrors { get; internal set; }

    /// <summary>Mean main-loop iterations per solve, or zero if nothing has been solved.</summary>
    public double MeanEquilibriumIterations => Solves == 0 ? 0 : (double)EquilibriumIterations / Solves;

    public void Reset()
    {
        Solves = 0;
        FrozenSkips = 0;
        BracketSteps = 0;
        InitialEstimateIterations = 0;
        InitialEstimateCapHits = 0;
        EquilibriumIterations = 0;
        EquilibriumCapHits = 0;
        WorstEquilibriumIterations = 0;
        TemperatureClamps = 0;
        LowOxygenErrors = 0;
        ResolutionErrors = 0;
        ExtendedPrecisionReductions = 0;
        NegativeFractionErrors = 0;
    }

    public override string ToString() =>
        $"{Solves} solves, mean {MeanEquilibriumIterations:F2} iterations, worst {WorstEquilibriumIterations}, "
        + $"{EquilibriumCapHits} cap hits, {InitialEstimateCapHits} estimate cap hits";
}
