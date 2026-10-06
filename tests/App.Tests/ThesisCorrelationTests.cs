using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using App.Core;
using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The simulation against the engine on the test bed: the baseline engine's measured
/// torque curve from the thesis, Figure 6.12 (<c>data/thesis/README.md</c>). Every other
/// physics test checks the port against the original program or against a physical law;
/// this one checks it against the dynamometer.
/// </summary>
/// <remarks>
/// Legacy is gated: it is the physics the thesis correlated, and must stay as close to the
/// measured curve as it is. Corrected is reported and not gated: it removes defects the
/// original's empirical inputs were calibrated around, so how far it sits from the test
/// data is a finding to weigh, not a regression. Before B78 it was 13.4 per cent rms; with
/// B78 it was 5.2, Legacy's figure, and with B37 it is 5.3 (ISSUES.md F6).
/// </remarks>
public sealed class ThesisCorrelationTests
{
    /// <summary>The thesis discounts its own correlation below this speed (README).</summary>
    private const int CorrelatedFrom = 2500;

    private sealed record Point(int Rpm, double Measured, double? ThesisSimulated);

    private static IReadOnlyList<Point> Figure6_12()
    {
        var path = Path.Combine(BaselinePaths.Directory!, "..", "thesis", "Figure6_12.csv");

        static double? Parse(string text) =>
            text.Length == 0 ? null : double.Parse(text, CultureInfo.InvariantCulture);

        return File.ReadAllLines(path)
            .Skip(1)
            .Select(line => line.Split(','))
            .Where(fields => fields[1].Length > 0)
            .Select(fields => new Point(
                int.Parse(fields[0], CultureInfo.InvariantCulture),
                Parse(fields[1])!.Value,
                Parse(fields[2])))
            .ToList();
    }

    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        return loader.Load(BaselinePaths.File("A2China.eng")).Engine;
    }

    /// <summary>
    /// Torque at each speed, at the reference settings; a run the wave solver stops is
    /// reported as the exception's message rather than failing the whole sweep.
    /// </summary>
    private static IReadOnlyDictionary<int, (double Torque, string? Failure)> Sweep(
        Func<PhysicsCorrections> physics, IEnumerable<int> speeds)
    {
        var results = new ConcurrentDictionary<int, (double, string?)>();

        Parallel.ForEach(speeds, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, rpm =>
        {
            var engine = BaselineEngine();
            engine.Rpm = rpm;

            var settings = new SimulationSettings
            {
                CycleCount = 6,
                OneZoneCycleCount = 1,
                MassBalance = 1,
                Physics = physics(),
            };

            try
            {
                var result = new SimulationRunner(new CachingExpressionEvaluator())
                    .Run(engine, settings, cancellation: TestContext.Current.CancellationToken);
                results[rpm] = (result.Engine.Torque, null);
            }
            catch (CfdException ex)
            {
                results[rpm] = (double.NaN, ex.Message);
            }
        });

        return results;
    }

    /// <summary>The rms of the relative error, in per cent, over the points that ran.</summary>
    private static double RmsError(
        IReadOnlyList<Point> points, IReadOnlyDictionary<int, (double Torque, string? Failure)> sweep) =>
        Math.Sqrt(points
            .Where(p => sweep[p.Rpm].Failure is null)
            .Average(p => Math.Pow((sweep[p.Rpm].Torque / p.Measured) - 1, 2))) * 100;

    /// <summary>The rms of the relative difference from the thesis's own simulation, in per cent.</summary>
    private static double RmsFromThesisSimulation(
        IReadOnlyList<Point> points, IReadOnlyDictionary<int, (double Torque, string? Failure)> sweep) =>
        Math.Sqrt(points
            .Where(p => p.ThesisSimulated is not null && sweep[p.Rpm].Failure is null)
            .Average(p => Math.Pow((sweep[p.Rpm].Torque / p.ThesisSimulated!.Value) - 1, 2))) * 100;

    private static double Bias(
        IReadOnlyList<Point> points, IReadOnlyDictionary<int, (double Torque, string? Failure)> sweep) =>
        points.Where(p => sweep[p.Rpm].Failure is null).Average(p => (sweep[p.Rpm].Torque / p.Measured) - 1) * 100;

    private static string Table(
        string title, IReadOnlyList<Point> points, IReadOnlyDictionary<int, (double Torque, string? Failure)> sweep)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"{title}: rpm, measured, thesis simulated, this run, error");

        foreach (var point in points)
        {
            var (torque, failure) = sweep[point.Rpm];
            var thesis = point.ThesisSimulated?.ToString("F1", CultureInfo.InvariantCulture) ?? "-";
            text.AppendLine(failure is null
                ? string.Create(CultureInfo.InvariantCulture,
                    $"{point.Rpm,5} {point.Measured,6:F1} {thesis,6} {torque,6:F1} {((torque / point.Measured) - 1) * 100,6:+0.0;-0.0}%")
                : string.Create(CultureInfo.InvariantCulture,
                    $"{point.Rpm,5} {point.Measured,6:F1} {thesis,6}  did not run: {failure}"));
        }

        return text.ToString();
    }

    /// <summary>
    /// The gate. Legacy reproduces the thesis's correlation with the dynamometer: the same
    /// shape, the same peak, and the same error against the measured curve, to within
    /// what the port has always measured. A change that moves Legacy away from the engine
    /// fails here even if it keeps every other figure.
    /// </summary>
    /// <remarks>
    /// Run with B4, the area clamp, which is bit-identical wherever Legacy runs (ISSUES.md
    /// B4). Without it six of the twenty speeds stop on a non-finite state: the pipe grid's
    /// last point lands a hair past the end of its area table, where the original's lookup
    /// falls to zero (ISSUES.md B4).
    /// </remarks>
    [Fact]
    public void LegacyStillMatchesTheDynamometer()
    {
        BaselinePaths.Require();

        var points = Figure6_12();
        var sweep = Sweep(
            () =>
            {
                var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
                physics.Overrides[CorrectionCatalogue.AreaClamp.Entry] = true;
                return physics;
            },
            points.Select(p => p.Rpm));

        TestContext.Current.TestOutputHelper?.WriteLine(Table("Legacy (with B4)", points, sweep));

        Assert.All(points, p => Assert.Null(sweep[p.Rpm].Failure));

        var correlated = points.Where(p => p.Rpm >= CorrelatedFrom).ToList();
        var peak = sweep[4000].Torque;

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"rms from {CorrelatedFrom} rpm {RmsError(correlated, sweep):F2}%, bias {Bias(correlated, sweep):+0.00;-0.00}%, rms over all {RmsError(points, sweep):F2}%, from the thesis's simulation {RmsFromThesisSimulation(points, sweep):F2}%, 4000 rpm {peak:F2} Nm"));

        // Measured at 5.29 and 5.45 per cent: the correlation the thesis reported.
        Assert.InRange(RmsError(correlated, sweep), 0, 8.0);
        Assert.InRange(RmsError(points, sweep), 0, 8.2);

        // And the port is still the program the thesis ran: measured at 2.2 per cent from
        // its simulated curve, the gap being the original's revisions after the thesis and
        // this engine file's, which is the reference run's rather than the thesis's.
        Assert.InRange(RmsFromThesisSimulation(points, sweep), 0, 3.3);

        // The measured peak is 151.5 Nm and the thesis's own simulation 151.6.
        Assert.Equal(151.5, peak, 151.5 * 0.01);
    }

    /// <summary>
    /// Corrected against the same curve: reported, not gated. The figures are in the test
    /// output and in ISSUES.md B78.
    /// </summary>
    [Fact]
    public void CorrectedIsReportedAgainstTheDynamometer()
    {
        BaselinePaths.Require();

        var points = Figure6_12();
        var sweep = Sweep(() => new PhysicsCorrections { Mode = PhysicsMode.Corrected }, points.Select(p => p.Rpm));

        TestContext.Current.TestOutputHelper?.WriteLine(Table("Corrected", points, sweep));

        var correlated = points.Where(p => p.Rpm >= CorrelatedFrom).ToList();
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"rms from {CorrelatedFrom} rpm {RmsError(correlated, sweep):F2}%, bias {Bias(correlated, sweep):+0.00;-0.00}%, 4000 rpm {sweep[4000].Torque:F2} Nm"));

        // Only that the comparison was made at the peak.
        Assert.Null(sweep[4000].Failure);
    }

}
