using System.Collections.Concurrent;
using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Validation;

/// <summary>One grid row's outcome.</summary>
/// <param name="Spark">The grid's spark advance, °BTDC, where it sets one.</param>
/// <param name="Trace">Valve-end inlet pressure through the last cycle, in bar, where asked for.</param>
internal sealed record RowResult(
    int Row,
    double Speed,
    double? Spark,
    double Torque,
    double PowerKw,
    string? Failure,
    IReadOnlyList<Point>? Trace,
    TimeSpan Elapsed);

/// <summary>
/// Runs the grids the suite names, every row from a freshly loaded engine through
/// <see cref="MultiRunner.RunRow"/>, which is what the application's multi-run does.
/// Rows are independent, so they run in parallel.
/// </summary>
internal sealed class CaseRunner(string casesDirectory, PhysicsCorrections physics, IProgress<string> log)
{
    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    /// <summary>Runs each (case, grid) once, whatever number of series share it.</summary>
    /// <param name="traced">The (case, grid) pairs whose rows record the inlet pressure trace.</param>
    public IReadOnlyDictionary<(string Case, string Grid), IReadOnlyList<RowResult>> Run(
        IEnumerable<(string Case, string Grid)> grids, ISet<(string Case, string Grid)> traced)
    {
        var jobs = new List<(string Case, string Grid, string Engine, MultiRunGrid Table, int Row)>();

        foreach (var (caseName, grid) in grids.Distinct())
        {
            var folder = Path.Combine(casesDirectory, caseName);
            var engine = Directory.GetFiles(folder, "*.eng").Single();
            var table = new MultiRunGridStore().Read(Path.Combine(folder, grid + ".msr")).Grid;

            for (var row = 0; row < table.RunCount; row++)
            {
                jobs.Add((caseName, grid, engine, table, row));
            }
        }

        var results = new ConcurrentDictionary<(string, string), ConcurrentBag<RowResult>>();
        var done = 0;

        Parallel.ForEach(
            jobs,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            job =>
            {
                var recorder = traced.Contains((job.Case, job.Grid)) ? new ValveEndRecorder() : null;
                var result = RunRow(job.Engine, job.Table, job.Row, recorder);

                results.GetOrAdd((job.Case, job.Grid), _ => []).Add(result);

                var n = Interlocked.Increment(ref done);
                log.Report(FormattableString.Invariant(
                    $"[{n,3}/{jobs.Count}] {job.Case}/{job.Grid} row {job.Row + 1}: {result.Speed:F0} rpm, ")
                    + (result.Failure is null
                        ? FormattableString.Invariant($"{result.Torque:F1} Nm, {result.Elapsed.TotalSeconds:F1} s")
                        : "FAILED: " + result.Failure));
            });

        return results.ToDictionary(
            r => r.Key,
            r => (IReadOnlyList<RowResult>)[.. r.Value.OrderBy(x => x.Row)]);
    }

    private RowResult RunRow(string engine, MultiRunGrid table, int row, ValveEndRecorder? recorder)
    {
        var settings = new SimulationSettings
        {
            CycleCount = (int)(table.Cycles(row) ?? 8),
            OneZoneCycleCount = 1,
            MassBalance = 1,
            Physics = physics.Clone(),
        };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var runner = new MultiRunner(Loader(), new SimulationRunner(new CachingExpressionEvaluator()));
        var result = runner.RunRow(engine, table, row, settings, manifoldRecorder: recorder);
        var spark = table.Number(row, 12);

        return result.Result is { } run
            ? new RowResult(row, result.Speed, spark, run.Engine.Torque, run.Engine.BrakePower / 1000,
                null, recorder?.Trace, clock.Elapsed)
            : new RowResult(row, result.Speed, spark, double.NaN, double.NaN,
                result.Failure ?? "failed", null, clock.Elapsed);
    }

    /// <summary>
    /// Keeps the pressure at the inlet pipe's valve end, the last grid point (the same
    /// point <c>ManifoldSolver.Report</c> hands the cylinder), through the last cycle run.
    /// </summary>
    private sealed class ValveEndRecorder : IManifoldRecorder
    {
        private readonly List<Point> _rows = [];

        public IReadOnlyList<Point> Trace => [.. _rows];

        public void Record(in ManifoldRow row) =>
            _rows.Add(new Point(row.CrankAngle, row.InletPressure.Span[^1] / 1e5));

        public void Reset() => _rows.Clear();
    }
}
