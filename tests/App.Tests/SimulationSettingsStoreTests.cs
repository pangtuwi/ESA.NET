using App.Core.Model;
using App.Persistence;

namespace App.Tests;

public sealed class SimulationSettingsStoreTests
{
    private static readonly SimulationSettingsStore Store = new();

    [Fact]
    public void ReadsTheShippedEsaIni()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var settings = Store.Read(Path.Combine(TestPaths.Legacy!, "ESA", "ESA.ini"));

        // The shipped file, not SPEC.md section 3's idealised example: the error log is
        // CAEEng.err rather than ESA2z1z.err, and MassBalance is 0.5 rather than 1.
        Assert.Equal("CAEEng.err", settings.ErrorLogFileName);
        Assert.Equal("Lastcyc.txt", settings.TextSaveFileName);
        Assert.Equal("Default.eng", settings.EngineFileName);
        Assert.Equal(4000, settings.EngineSpeed);
        Assert.Equal(6, settings.CycleCount);
        Assert.Equal(1, settings.OneZoneCycleCount);
        Assert.Equal(0.5, settings.MassBalance);
    }

    [Fact]
    public void AMissingFileYieldsTheDelphiDefaults()
    {
        // TIniFile returns defaults for a file that is not there, and so does this.
        var settings = Store.Read(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini"));
        var expected = new SimulationSettings();

        Assert.Equal(expected.ErrorLogFileName, settings.ErrorLogFileName);
        Assert.Equal(expected.EngineFileName, settings.EngineFileName);
        Assert.Equal(expected.EngineSpeed, settings.EngineSpeed);
        Assert.Equal(expected.MassBalance, settings.MassBalance);
    }

    [Fact]
    public void WritingUnchangedSettingsLeavesTheFileByteIdentical()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // The shipped ESA.ini ends without a trailing newline; that must survive too.
        var source = Path.Combine(TestPaths.Legacy!, "ESA", "ESA.ini");
        var original = File.ReadAllBytes(source);

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
        try
        {
            File.WriteAllBytes(target, original);
            Store.Write(target, Store.Read(target));

            Assert.Equal(original, File.ReadAllBytes(target));
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void ChangingOneSettingRewritesOnlyThatLine()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var source = Path.Combine(TestPaths.Legacy!, "ESA", "ESA.ini");
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");

        try
        {
            File.Copy(source, target);
            var originalLines = File.ReadAllLines(target);

            var settings = Store.Read(target);
            settings.CycleCount = 12;
            Store.Write(target, settings);

            var written = File.ReadAllLines(target);
            var differences = originalLines
                .Zip(written, (before, after) => (before, after))
                .Where(pair => pair.before != pair.after)
                .ToList();

            Assert.Single(differences);
            Assert.Equal("Nocycles=12", differences[0].after);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void WritesASettingsFileThatDidNotExist()
    {
        // IniValues.SaveIniValues is declared but empty in the original (SPEC.md
        // section 6), so Delphi never wrote this file back at all.
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");

        try
        {
            Store.Write(target, new SimulationSettings { EngineSpeed = 5500, CycleCount = 8 });

            var reloaded = Store.Read(target);
            Assert.Equal(5500, reloaded.EngineSpeed);
            Assert.Equal(8, reloaded.CycleCount);
        }
        finally
        {
            File.Delete(target);
        }
    }
}
