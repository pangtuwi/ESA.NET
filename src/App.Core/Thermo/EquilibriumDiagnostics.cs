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
    }

    public override string ToString() =>
        $"{Solves} solves, mean {MeanEquilibriumIterations:F2} iterations, worst {WorstEquilibriumIterations}, "
        + $"{EquilibriumCapHits} cap hits, {InitialEstimateCapHits} estimate cap hits";
}
