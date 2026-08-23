using App.Core;
using App.Core.Interpolation;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Reads the real side files shipped under legacy/ESA/Data.
/// </summary>
public sealed class TableReaderTests
{
    private static string Example1(string name) =>
        Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Example1", name);

    private static void RequireLegacy() =>
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

    [Fact]
    public void ReadsASparkMap()
    {
        RequireLegacy();

        var table = new SpeedKeyedTableReader().Read(Example1("Nissan.spk"));

        Assert.Equal(6, table.Rpm.Count);
        Assert.Equal(1000, table.Rpm[0]);
        Assert.Equal(12, table.Values[0]);
        Assert.Equal(6000, table.Rpm[^1]);
        Assert.Equal(30, table.Values[^1]);
    }

    [Fact]
    public void ReadsWallTemperatures()
    {
        RequireLegacy();

        var table = new WallTemperatureTableReader().Read(Example1("Nissan.cwt"));

        Assert.Equal(6, table.Rpm.Count);

        // Row one: 1000, head 350, piston 440, upper liner 495, lower liner 350.
        Assert.Equal(350, table.HeadTemperature[0]);
        Assert.Equal(440, table.PistonTemperature[0]);
        Assert.Equal(495, table.UpperLinerTemperature[0]);
        Assert.Equal(350, table.LowerLinerTemperature[0]);
    }

    [Fact]
    public void ReadsExhaustBackPressureWithTemperatureBeforePressure()
    {
        RequireLegacy();

        var table = new ExhaustBackPressureTableReader().Read(Example1("Nissan.exh"));

        // The heading row reads SPEED / TEMP[C] / P[kPa]: row one is 1000, 450, 2.0.
        // SPEC.md section 3 lists pressure before temperature and is wrong.
        Assert.Equal(1000, table.Rpm[0]);
        Assert.Equal(450, table.Temperature[0]);
        Assert.Equal(2.0, table.Pressure[0]);

        Assert.Equal(700, table.Temperature[^1]);
        Assert.Equal(30.0, table.Pressure[^1]);
    }

    [Fact]
    public void ReadsACamProfile()
    {
        RequireLegacy();

        var profile = new CamProfileReader().Read(Example1("Nissan Inlet Profile.cam"));

        Assert.True(profile.ProfileOk);
        Assert.Equal(100, profile.Points.Count);

        // The file holds a normalised shape, not absolute lift: both axes run 0 to 1,
        // and the engine scales by IVLift and the cam duration on use. It opens and
        // closes at zero lift.
        Assert.Equal(0.0, profile.Points[0].X);
        Assert.Equal(0.0, profile.Points[0].Y);
        Assert.Equal(1.0, profile.Points[^1].X);
        Assert.Equal(0.0, profile.Points[^1].Y);
        Assert.Equal(1.0, profile.Lift);
        Assert.Equal(1.0, profile.Duration);
    }

    [Fact]
    public void ReadsAManifoldAreaTable()
    {
        RequireLegacy();

        var table = new ManifoldAreaTableStore().Read(Example1("290Inlet.maf")).Table;

        // 1,0,745 / 2,210,745 / 3,290,1026 then a row of dashes ends it.
        Assert.Equal(3, table.Count);
        Assert.Equal(0, table.Position[0]);
        Assert.Equal(745, table.Area[0]);
        Assert.Equal(290, table.Position[2]);
        Assert.Equal(1026, table.Area[2]);
    }

    [Fact]
    public void ReadsADischargeCoefficientGrid()
    {
        RequireLegacy();

        var table = new DischargeCoefficientTableStore().Read(Example1("Nissan IVIn.vcd")).Table;

        Assert.Equal(6, table.XCount);
        Assert.Equal(1.0, table.XIndex[0]);
        Assert.Equal(2.0, table.XIndex[5]);

        Assert.True(table.YCount > 1);
        Assert.Equal(0, table.YIndex[0]);
        Assert.Equal(0.95, table.Cell[0, 0]);
    }

    [Fact]
    public void EveryShippedSideFileParses()
    {
        RequireLegacy();

        var cam = new CamProfileReader();
        var spk = new SpeedKeyedTableReader();
        var cwt = new WallTemperatureTableReader();
        var exh = new ExhaustBackPressureTableReader();
        var maf = new ManifoldAreaTableStore();
        var vcd = new DischargeCoefficientTableStore();

        var failures = new List<string>();
        var read = 0;

        foreach (var path in Directory.EnumerateFiles(TestPaths.Legacy!, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".cam": cam.Read(path); break;
                    case ".spk": spk.Read(path); break;
                    case ".cwt": cwt.Read(path); break;
                    case ".exh": exh.Read(path); break;
                    case ".maf": maf.Read(path); break;
                    case ".vcd": vcd.Read(path); break;
                    default: continue;
                }

                read++;
            }
            catch (Exception ex) when (ex is LegacyDataException or FormatException)
            {
                failures.Add($"{Path.GetRelativePath(TestPaths.Legacy!, path)}: {ex.Message}");
            }
        }

        Assert.True(read > 20, $"Expected to read a meaningful number of side files, read {read}.");
        Assert.Empty(failures);
    }

    [Fact]
    public void ManifoldAreaLookupFallsToZeroPastTheEndOfTheTable()
    {
        RequireLegacy();

        var table = new ManifoldAreaTableStore().Read(Example1("290Inlet.maf")).Table;

        Assert.Equal(745, LegacyInterpolation.AreaAt(table, 0));
        Assert.Equal(745, LegacyInterpolation.AreaAt(table, 210));
        Assert.Equal(1026, LegacyInterpolation.AreaAt(table, 290));

        // Past the last position the original returns zero rather than clamping.
        Assert.Equal(0, LegacyInterpolation.AreaAt(table, 400));
    }

    [Fact]
    public void SpeedLookupClampsAtBothEnds()
    {
        RequireLegacy();

        var table = new SpeedKeyedTableReader().Read(Example1("Nissan.spk"));

        Assert.Equal(12, LegacyInterpolation.AtSpeed(table.Rpm, table.Values, 500));
        Assert.Equal(30, LegacyInterpolation.AtSpeed(table.Rpm, table.Values, 9000));
        Assert.Equal(16, LegacyInterpolation.AtSpeed(table.Rpm, table.Values, 1500));
    }

    [Fact]
    public void RowLimitIsReportedRatherThanHaltingTheApplication()
    {
        // TWallTemps.Load calls Halt when the count exceeds 40, terminating the
        // application and losing the user's work. The port throws instead.
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cwt");
        var lines = new List<string> { "41", "SPEED\tTHEAD\tTPISTON\tTULINER\tTLLINER" };
        for (var i = 0; i < 41; i++)
        {
            lines.Add($"{1000 + i}\t350\t440\t495\t350");
        }

        try
        {
            File.WriteAllLines(path, lines);
            var error = Assert.Throws<LegacyDataException>(() => new WallTemperatureTableReader().Read(path));
            Assert.Contains("maximum is 40", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
