using System.Globalization;
using App.Core.Interpolation;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B66: the <c>.exh</c> temperature column, headed <c>TEMP[C]</c>, is Celsius. The
/// original used it raw wherever it wanted kelvin; under B66 it is converted on reading.
/// </summary>
public sealed class ExhaustTemperatureTests
{
    private static string CaseFile(string folder, string file) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../validation/cases", folder, file));

    private static string RepoFile(string relative) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", relative));

    /// <summary>
    /// The provenance oracle: the five-valve case's table is the dynamometer's measured exhaust
    /// temperature, which the 35.5 mm test sheet records in Celsius (<c>TExh</c>). From 1750 rpm
    /// up every point is within 10 degrees of the sheet's, another run of the same test; read as
    /// kelvin it would put full-load exhaust at 218 C at 1500 rpm.
    /// </summary>
    [Fact]
    public void TheTableIsTheMeasuredExhaustTemperatureInCelsius()
    {
        var table = new Dictionary<double, double>();

        foreach (var line in File.ReadAllLines(CaseFile("ChinaBora98_Dia355", "ChinaBora98.exh")).Skip(2))
        {
            var parts = line.Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3)
            {
                table[double.Parse(parts[0], CultureInfo.InvariantCulture)] = double.Parse(parts[1], CultureInfo.InvariantCulture);
            }
        }

        var lines = File.ReadAllLines(RepoFile("data/thesis/tests/FiveValve_355.csv"));
        var header = lines[0].Split(',');
        var rpm = Array.IndexOf(header, "rpm");
        var exhaust = Array.IndexOf(header, "exhaust_t_c");
        var compared = 0;

        foreach (var row in lines.Skip(1).Select(l => l.Split(',')))
        {
            var speed = double.Parse(row[rpm], CultureInfo.InvariantCulture);
            var nearest = table.Keys.MinBy(k => Math.Abs(k - speed));

            if (speed < 1700 || Math.Abs(nearest - speed) > 30)
            {
                continue;
            }

            Assert.InRange(table[nearest] - double.Parse(row[exhaust], CultureInfo.InvariantCulture), -10, 10);
            compared++;
        }

        Assert.Equal(20, compared);
    }

    private static Engine Load()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(CaseFile("ChinaBora98_Dia355", "ChinaBora98_Dia355.eng")).Engine;
        engine.Rpm = 4000;
        return engine;
    }

    /// <summary>
    /// Under B66 the exhaust gas and the exhaust pipe start at the table's value plus 273.15, at
    /// 4000 rpm 776 C, 1049.15 K; in Legacy both take 776 raw, as the original did.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheExhaustStartsAtTheTableTemperature(bool b66)
    {
        var engine = Load();
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.ExhaustTemperatureCelsius.Entry] = b66;

        var table = engine.Manifold.ExhaustBack;
        var raw = LegacyInterpolation.AtSpeed(table.Rpm, table.Temperature, engine.Rpm);
        Assert.Equal(776, raw, 1e-9);

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var expected = raw + (b66 ? 273.15 : 0);
        Assert.Equal(expected, engine.Exhaust.Tb, 1e-9);

        // The pipe is laid out on the first manifold step, from the back temperature, and its
        // temperature array is never written again (ISSUES.md B65).
        engine.ZoneCount = 1;
        solver.RunOneCycle();
        var exhaust = engine.Manifold.Exhaust;
        Assert.All(Enumerable.Range(0, exhaust.ActiveCount), i => Assert.Equal(expected, exhaust.Temperature[i], 1e-9));
    }

    [Fact]
    public void CorrectedRunsB66AndLegacyDoesNot()
    {
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.ExhaustTemperatureCelsius));
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.ExhaustTemperatureCelsius));
    }
}
