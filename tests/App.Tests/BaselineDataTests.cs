using System.Globalization;
using App.Core.Expressions;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Guards the phase 4 validation baseline in <c>data/baseline</c>.
/// </summary>
/// <remarks>
/// <para>
/// The baseline is a captured run of the original Delphi application: an engine,
/// every side file it names, and the full-cycle trace it produced. BASELINE.md
/// documents it. Phase 4 is measured against it, so these tests make sure it stays
/// loadable and intact rather than quietly rotting.
/// </para>
/// <para>
/// They also pin the cross-checks that the baseline confirmed about the phase 3
/// port: the C7H17 fuel default, the crossed exhaust discharge tables, and
/// <c>FPlenumP</c> winning over <c>PlenumP</c>.
/// </para>
/// </remarks>
public sealed class BaselineDataTests
{
    private static string? Baseline { get; } = FindBaseline();

    private static void RequireBaseline() =>
        Assert.SkipWhen(Baseline is null, "Not running from a repository checkout.");

    private static string File(string name) => Path.Combine(Baseline!, name);

    private static EngineLoader CreateLoader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static string? FindBaseline()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "baseline");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void EveryFileTheBaselineNeedsIsPresent()
    {
        RequireBaseline();

        string[] required =
        [
            "A2China.eng",
            "A2ChinaInlet_M758.maf",
            "A2ChinaExhaust_M.maf",
            "A2China Inlet Profile.cam",
            "A2China Exhaust Profile.cam",
            "A2China IVIn.vcd",
            "A2China IVOut.vcd",
            "A2ChinaVar.spk",
            "A2China.cwt",
            "A2China.exh",
            "A2China.txt",
        ];

        var missing = required.Where(name => !System.IO.File.Exists(File(name))).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void TheBaselineEngineLoadsWithNothingUnresolved()
    {
        RequireBaseline();

        // Every path in A2China.eng is a dead C:\CAEEng\... absolute path from the
        // machine that produced the run, so this only passes because
        // LegacyPathResolver falls back to the file name beside the .eng.
        var result = CreateLoader().Load(File("A2China.eng"));

        Assert.True(result.IsComplete, string.Join("; ", result.Problems));
        Assert.Equal("A2 China Jetta 1.6L 5V Baseline", result.Engine.Name);
    }

    [Fact]
    public void LoadedValuesMatchTheScreenshottedSettings()
    {
        RequireBaseline();

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;

        // Cylinders tab: the form displays 1595 cc, computed from these.
        Assert.Equal(4, engine.CylinderCount);
        Assert.Equal(81.0, engine.Bore);
        Assert.Equal(77.4, engine.Stroke);
        Assert.Equal(9.2, engine.CompressionRatio);

        var capacity = engine.CylinderCount * Math.PI / 4.0 * engine.Bore * engine.Bore * engine.Stroke / 1000.0;
        Assert.Equal(1595, capacity, 0);

        // Cams tab: durations are Open + 180 + Close, shown as 279 and 281.
        var inlet = engine.Manifold.InletValve;
        var exhaust = engine.Manifold.ExhaustValve;
        Assert.Equal(279, inlet.OpenAngle + 180 + inlet.CloseAngle);
        Assert.Equal(281, exhaust.OpenAngle + 180 + exhaust.CloseAngle);

        // Fuel tab: C 7, H 17, O 0, N 0. No .eng file stores composition, so the
        // screenshot is the only record that the original ran on these.
        Assert.Equal(7, engine.Cylinder.Fuel.C);
        Assert.Equal(17, engine.Cylinder.Fuel.H);
        Assert.Equal(0, engine.Cylinder.Fuel.O);
        Assert.Equal(0, engine.Cylinder.Fuel.N);

        // Inlet tab shows (99000): FPlenumP wins over the older PlenumP=98.0, which
        // this file also carries.
        Assert.Equal("(99000)", engine.Manifold.PlenumPressureFunction.Expression);
    }

    [Fact]
    public void ExhaustDischargeTablesAreCrossedAsTheValvesTabShows()
    {
        RequireBaseline();

        var definition = new EngineDefinitionStore().Read(File("A2China.eng"));

        // The Valves tab shows the exhaust Forward Flow Cd box holding IVOut.vcd and
        // Reverse holding IVIn.vcd, because forward flow through an exhaust valve is
        // outward.
        Assert.Equal("C:\\CAEEng\\A2China IVOut.vcd", definition.ExhaustValveCdOutwardFile);
        Assert.Equal("C:\\CAEEng\\A2China IVIn.vcd", definition.ExhaustValveCdInwardFile);
    }

    [Fact]
    public void TheGridExpressionsStayWithinTheLegacyLimitsAtTheBaselineSpeed()
    {
        RequireBaseline();

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;
        var calculator = new GridSizeCalculator(new CachingExpressionEvaluator());

        var inletAreas = engine.Manifold.InletPipe.AreaVersusLength;
        var inletLength = inletAreas.Position[inletAreas.Count - 1] / 1000.0;

        var exhaustAreas = engine.Manifold.ExhaustPipe.AreaVersusLength;
        var exhaustLength = exhaustAreas.Position[exhaustAreas.Count - 1] / 1000.0;

        // The run was at 4000 rpm. Neither grid may exceed NI or NE or the solver
        // would have raised ECFDError instead of producing the trace.
        var inletPoints = calculator.InletGridSize(engine.Manifold.InletGrid.Expression, inletLength, 4000);
        var exhaustPoints = calculator.ExhaustGridSize(engine.Manifold.ExhaustGrid.Expression, exhaustLength, 4000);

        Assert.InRange(inletPoints, 1, App.Core.EsaLimits.InletGridPoints);
        Assert.InRange(exhaustPoints, 1, App.Core.EsaLimits.ExhaustGridPoints);
    }

    [Fact]
    public void TheReferenceTraceHasTheShapeBaselineMdDescribes()
    {
        RequireBaseline();

        var lines = System.IO.File.ReadAllLines(File("A2China.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(',').Select(h => h.Trim()).ToList();
        var rows = lines.Skip(1).ToList();

        // Crank angle plus the 28 captured values, matching ColName in CAList2z.pas.
        Assert.Equal(29, header.Count);
        Assert.Equal("CA", header[0]);
        Assert.Equal("PCyl", header[2]);
        Assert.Equal("FuelM", header[15]);
        Assert.Equal("htLoss", header[^1]);

        // One row per crank angle over [-359, 360].
        Assert.Equal(720, rows.Count);

        double Field(string row, int index) =>
            double.Parse(row.Split(',')[index], CultureInfo.InvariantCulture);

        Assert.Equal(App.Core.EsaLimits.FirstCrankAngle, Field(rows[0], 0));
        Assert.Equal(App.Core.EsaLimits.LastCrankAngle, Field(rows[^1], 0));

        // The trace validates its own geometry: Vcyl is written in cc, so the swept
        // volume across four cylinders is the 1595 cc the form displays, and the
        // ratio of the extremes is the 9.2 compression ratio in the .eng.
        var volumes = rows.Select(r => Field(r, 1)).ToList();
        var swept = (volumes.Max() - volumes.Min()) * 4;
        var compressionRatio = volumes.Max() / volumes.Min();

        Assert.Equal(1595, swept, 0);
        Assert.Equal(9.2, compressionRatio, 1);
    }
}
