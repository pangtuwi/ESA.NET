using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The performance results file, <c>SimulDat.txt</c>.
/// </summary>
public sealed class PerformanceResultWriterTests
{
    private const double BackPressureAbsolute = 119125;

    private static Engine PreparedEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;

        var solver = new CycleSolver(engine, new RecordedManifoldSource(298.15));
        solver.Initialise();

        // The accumulators as the reference run left them, so the row is comparable with
        // the recorded one without depending on simulation accuracy.
        double At(string column) =>
            BaselinePaths.TraceColumn(column).Single(p => p.CrankAngle == -101).Value;

        engine.Work = At("WWork");
        engine.PumpingWork = At("PWork");
        engine.HeatLoss = At("htLoss");
        engine.Cylinder.MGas = 580.11 / 1e6;
        engine.TotalMassInInletValve = 560.11 / 1e6;
        engine.TotalMassOutExhaustValve = 560.38 / 1e6;
        engine.Cylinder.ThetaSpark = -21;

        new PerformanceCalculator().Calculate(engine);

        return engine;
    }

    [Fact]
    public void TheHeadingMatchesTheOriginalsExactly()
    {
        BaselinePaths.Require();

        var original = File.ReadAllLines(BaselinePaths.File("SimulDat.txt"))[0];

        // Irregular spacing and all: the original builds it from string literals with
        // hand-chosen gaps rather than fixed-width fields.
        Assert.Equal(original, PerformanceResultWriter.Heading());
    }

    [Fact]
    public void ARowReproducesTheRecordedResultsRow()
    {
        BaselinePaths.Require();

        var original = File.ReadAllLines(BaselinePaths.File("SimulDat.txt"))[1];
        var produced = PerformanceResultWriter.Row(PreparedEngine(), BackPressureAbsolute);

        Assert.Equal(original.TrimEnd('.'), produced);
    }

    [Fact]
    public void BackPressureIsReportedAsGaugeKilopascals()
    {
        BaselinePaths.Require();

        var produced = PerformanceResultWriter.Row(PreparedEngine(), BackPressureAbsolute);
        var fields = produced.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // 119.125 kPa absolute reported as 17.8 gauge, by subtracting a hard-coded
        // 101.325 rather than the engine's own ambient pressure. See ISSUES.md B69.
        Assert.Equal("17.8", fields[17]);
    }

    [Fact]
    public void ResultsAccumulateAcrossRunsWithOneHeading()
    {
        BaselinePaths.Require();

        var engine = PreparedEngine();
        var writer = new PerformanceResultWriter();
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");

        try
        {
            writer.Append(path, engine, BackPressureAbsolute);
            writer.Append(path, engine, BackPressureAbsolute);

            var lines = File.ReadAllLines(path);

            // Heading once, then a row per run - which is why the captured baseline holds
            // two identical rows: the point was simulated twice.
            Assert.Equal(3, lines.Length);
            Assert.Equal(PerformanceResultWriter.Heading(), lines[0]);
            Assert.Equal(lines[1], lines[2]);

            // The newline goes before each row, so the file ends without one.
            Assert.False(File.ReadAllText(path).EndsWith('\n'));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
