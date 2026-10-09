using System.Globalization;
using System.Text.Json;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Keeps the thesis validation suite (<c>validation/README.md</c>) runnable without running
/// it: every case loads with every side file found, every grid reads, and every series
/// <c>suite.json</c> names exists in the thesis data. The suite itself takes minutes and
/// is not part of the test run.
/// </summary>
public sealed class ThesisSuiteTests
{
    private static string? Root =>
        TestPaths.Legacy is null ? null : Path.GetDirectoryName(TestPaths.Legacy);

    private static string Validation => Path.Combine(Root!, "validation");

    private static void Require() =>
        Assert.SkipWhen(Root is null, "Not running from a repository checkout.");

    private static JsonElement Suite() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Validation, "suite.json"))).RootElement;

    private static IEnumerable<JsonElement> AllSeries() =>
        Suite().GetProperty("figures").EnumerateArray()
            .SelectMany(f => f.GetProperty("series").EnumerateArray());

    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    /// <summary>The one <c>.eng</c> a case folder holds.</summary>
    private static string EngineOf(string caseName) =>
        Assert.Single(Directory.GetFiles(Path.Combine(Validation, "cases", caseName), "*.eng"));

    [Fact]
    public void EveryCaseLoadsWithEverySideFileFoundInItsOwnFolder()
    {
        Require();

        var cases = Directory.GetDirectories(Path.Combine(Validation, "cases"));
        Assert.NotEmpty(cases);

        foreach (var folder in cases)
        {
            var path = EngineOf(Path.GetFileName(folder));
            var result = Loader().Load(path);

            Assert.True(result.IsComplete, $"{path}: {string.Join("; ", result.Problems)}");

            // A case is self-contained: nothing resolves outside its folder.
            Assert.All(result.SideFiles, f => Assert.Equal(
                Path.GetFullPath(folder), Path.GetDirectoryName(Path.GetFullPath(f.Path))));
        }
    }

    [Fact]
    public void EverySeriesNamesACaseAndAGridThatReads()
    {
        Require();

        var store = new MultiRunGridStore();

        foreach (var series in AllSeries())
        {
            var caseName = series.GetProperty("case").GetString()!;
            var grids = new[] { series.GetProperty("grid").GetString()! }
                .Concat(series.TryGetProperty("baseline", out var b) ? [b.GetString()!] : []);

            foreach (var grid in grids)
            {
                var path = Path.Combine(Validation, "cases", caseName, grid + ".msr");
                var document = store.Read(path);

                Assert.False(document.ShortFormat, path);
                Assert.InRange(document.Grid.RunCount, 1, 30);
            }
        }
    }

    [Fact]
    public void EverySeriesComparesWithThesisDataThatExists()
    {
        Require();

        foreach (var figure in Suite().GetProperty("figures").EnumerateArray())
        {
            var path = Path.Combine(Root!, figure.GetProperty("data").GetString()!);
            var rows = File.ReadAllLines(path).Skip(1).Select(l => l.Split(',')).ToList();

            Assert.All(rows, r =>
            {
                Assert.Equal(4, r.Length);
                Assert.Contains(r[1], new[] { "measured", "thesis_model" });
                double.Parse(r[2], CultureInfo.InvariantCulture);
                double.Parse(r[3], CultureInfo.InvariantCulture);
            });

            foreach (var series in figure.GetProperty("series").EnumerateArray())
            {
                foreach (var (key, role) in new[] { ("measured", "measured"), ("thesisModel", "thesis_model") })
                {
                    var name = series.GetProperty(key).GetString();
                    Assert.True(
                        rows.Count(r => r[0] == name && r[1] == role) >= 3,
                        $"{figure.GetProperty("id")}: no {role} series '{name}' in {path}");
                }
            }
        }
    }
}
