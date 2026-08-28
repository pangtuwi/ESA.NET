using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>Progress from a running simulation, for a status line or a progress bar.</summary>
/// <param name="Cycle">Which cycle is running, from one.</param>
/// <param name="RequestedCycles">How many were asked for.</param>
/// <param name="CrankAngle">The crank angle just completed.</param>
/// <param name="MassBalance">
/// The convergence measure in milligrams: how far apart the totals through the two valves
/// are. The run stops when this falls below the tolerance.
/// </param>
public readonly record struct SimulationProgress(
    int Cycle, int RequestedCycles, double CrankAngle, double MassBalance);

/// <summary>What a completed run produced.</summary>
/// <param name="Engine">The engine, carrying its performance figures.</param>
/// <param name="Trace">The last cycle, captured crank angle by crank angle.</param>
/// <param name="CyclesRun">How many cycles were actually simulated.</param>
/// <param name="Converged">Whether the run stopped on its mass balance rather than the cycle limit.</param>
/// <param name="ManifoldDataCaptured">
/// Whether the manifold recorder was handed the last cycle's rows, so the caller knows
/// there is something to write. False when no recorder was supplied or the engine does not
/// ask for manifold data.
/// </param>
public sealed record SimulationResult(
    Engine Engine,
    CrankAngleTrace Trace,
    int CyclesRun,
    bool Converged,
    bool ManifoldDataCaptured = false);

/// <summary>
/// Runs a complete simulation: initialise, simulate to convergence, capture the last
/// cycle and compute the performance figures. Corresponds to what
/// <c>TFMain.Simulate</c> drives, minus the window.
/// </summary>
public sealed class SimulationRunner
{
    private readonly IExpressionEvaluator _evaluator;

    public SimulationRunner(IExpressionEvaluator evaluator) => _evaluator = evaluator;

    /// <summary>
    /// Simulates <paramref name="engine"/> at its current speed.
    /// </summary>
    /// <param name="progress">Called after each step, on the calling thread.</param>
    /// <param name="cancellation">Checked between steps, so a long run can be stopped.</param>
    /// <param name="afterInitialise">
    /// Applied once the engine is initialised and before the first cycle. The original
    /// has this seam because <c>InitVars</c> derives some values that a multi-run row is
    /// then allowed to override - the spark angle above all, which <c>InitVars</c> looks
    /// up from the <c>.spk</c> map and the grid may replace.
    /// </param>
    /// <param name="manifoldRecorder">
    /// Where the nine manifold output files' rows go, when the engine asks for them.
    /// <see cref="App.Core.Model.Manifolds.SaveManifoldData"/> is the only condition: the
    /// original also required the run to reach the last <b>requested</b> cycle, which a
    /// converged run never does, so ticking the box could produce nothing at all
    /// (ISSUES.md C1). Here the recorder is reset at each cycle boundary and so ends up
    /// holding the last cycle that actually ran.
    /// </param>
    /// <param name="recordManifoldData">
    /// Overrides that gate. <see langword="null"/> leaves it to the engine, which is the
    /// original's behaviour and what <c>ManifoldOutputGateTests</c> pins;
    /// <see langword="true"/> or <see langword="false"/> decides regardless of the flag.
    /// The application passes <see langword="true"/>, because every run archives its
    /// manifold files into its own run folder and there is no longer anything for the
    /// checkbox to save.
    /// </param>
    /// <exception cref="EngineException">The engine could not be initialised or ran to an impossible state.</exception>
    public SimulationResult Run(
        Engine engine,
        SimulationSettings settings,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellation = default,
        Action<Engine>? afterInitialise = null,
        IManifoldRecorder? manifoldRecorder = null,
        bool? recordManifoldData = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settings);

        var manifold = new ManifoldSolver(engine, _evaluator);
        var solver = new CycleSolver(engine, manifold, _evaluator);

        if (!solver.Initialise())
        {
            throw new EngineException(
                "The engine could not be initialised: one or both cam profiles failed to load.");
        }

        afterInitialise?.Invoke(engine);

        var recorder = new CrankAngleTraceRecorder(solver.InletValve, solver.ExhaustValve);
        var requested = Math.Max(settings.CycleCount, EsaLimits.MinimumCycles);
        var cycle = 0;

        // Ticking Save Manifold Data is the whole gate, where the original wanted that and
        // two more conditions besides. See ISSUES.md C1. The caller may override it.
        var capturing = manifoldRecorder is not null
                        && (recordManifoldData ?? engine.Manifold.SaveManifoldData);

        if (capturing)
        {
            manifold.Recorder = new ManifoldCaptureWindow(
                manifoldRecorder!, -180 + engine.Manifold.InletValve.CloseAngle);
        }

        solver.StepCompleted += s =>
        {
            cancellation.ThrowIfCancellationRequested();
            recorder.Record(s.Engine);

            progress?.Report(new SimulationProgress(
                cycle,
                requested,
                s.Engine.CrankAngle,
                Math.Abs(s.Engine.TotalMassInInletValve - s.Engine.TotalMassOutExhaustValve) * 1e6));
        };

        engine.ZoneCount = 1;

        for (cycle = 1; cycle <= requested; cycle++)
        {
            if (cycle >= settings.OneZoneCycleCount + 1)
            {
                engine.ZoneCount = 2;
            }

            var balance =
                Math.Abs(engine.TotalMassInInletValve - engine.TotalMassOutExhaustValve) * 1e6;

            // Convergence is tested at the top of each cycle against the totals the
            // previous one left, so a converged run stops before doing the work.
            if (balance < settings.MassBalance)
            {
                engine.CycleCount = cycle - 1;
                new PerformanceCalculator().Calculate(engine);

                // The cycle that just converged never ran, so the recorder still holds the
                // one before it - which is the last cycle there was.
                return new SimulationResult(
                    engine, recorder.Trace, cycle - 1, Converged: true,
                    ManifoldDataCaptured: capturing && cycle > 1);
            }

            // Keep only the cycle in hand, so whichever turns out to be the last one run
            // is the one written.
            manifold.Recorder?.Reset();

            solver.RunOneCycle();
        }

        engine.CycleCount = requested;
        new PerformanceCalculator().Calculate(engine);

        return new SimulationResult(
            engine, recorder.Trace, requested, Converged: false, ManifoldDataCaptured: capturing);
    }
}
