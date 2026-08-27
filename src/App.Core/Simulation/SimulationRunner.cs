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
public sealed record SimulationResult(
    Engine Engine, CrankAngleTrace Trace, int CyclesRun, bool Converged);

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
    /// <exception cref="EngineException">The engine could not be initialised or ran to an impossible state.</exception>
    public SimulationResult Run(
        Engine engine,
        SimulationSettings settings,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellation = default)
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

        var recorder = new CrankAngleTraceRecorder(solver.InletValve, solver.ExhaustValve);
        var requested = Math.Max(settings.CycleCount, EsaLimits.MinimumCycles);
        var cycle = 0;

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

                return new SimulationResult(engine, recorder.Trace, cycle - 1, Converged: true);
            }

            solver.RunOneCycle();
        }

        engine.CycleCount = requested;
        new PerformanceCalculator().Calculate(engine);

        return new SimulationResult(engine, recorder.Trace, requested, Converged: false);
    }
}
