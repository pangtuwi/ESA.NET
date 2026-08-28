using App.Core;

namespace App.Tests;

/// <summary>
/// How run folders are named. The original had no such folders: it opened its output
/// files under bare relative names and dropped them in the working directory, where the
/// next run overwrote them (ISSUES.md C4).
/// </summary>
public sealed class RunFolderNameTests
{
    private static readonly DateTimeOffset When =
        new(2026, 8, 28, 14, 15, 30, TimeSpan.Zero);

    [Fact]
    public void ARunIsNamedForTheTimeThenTheEngine()
    {
        Assert.Equal(
            "2026-08-28_141530_A2China",
            RunFolderName.ForRun("/data/Engines/A2China.eng", When));
    }

    [Fact]
    public void TheTimeComesFirstSoAListingIsChronological()
    {
        var earlier = RunFolderName.ForRun("Zebra.eng", When);
        var later = RunFolderName.ForRun("Aardvark.eng", When.AddMinutes(1));

        Assert.True(
            string.CompareOrdinal(earlier, later) < 0,
            "A folder listing should be in the order the runs happened.");
    }

    [Fact]
    public void WithNoEngineOpenTheTimeIsTheWholeName()
    {
        Assert.Equal("2026-08-28_141530", RunFolderName.ForRun(string.Empty, When));
    }

    [Fact]
    public void AnythingUnsafeInTheEngineNameBecomesAnUnderscore()
    {
        // The shipped data is full of names like "A2China Exhaust Profile" and
        // "Variable_Inlet_Diameter/...", so a folder named after one has to be sanitised.
        Assert.Equal("A2_China_v2.1", RunFolderName.Sanitise("A2 China: v2.1"));
        Assert.Equal("Nissan_5", RunFolderName.Sanitise("Nissan  5"));
        Assert.Equal(string.Empty, RunFolderName.Sanitise("   "));
        Assert.Equal("x", RunFolderName.Sanitise("*x*"));
    }

    [Fact]
    public void ColonsNeverReachAFolderName()
    {
        // A round-trip timestamp would carry them, and they are illegal on Windows.
        Assert.DoesNotContain(':', RunFolderName.ForRun("A2China.eng", When));
    }

    [Fact]
    public void ASweepRowIsNumberedFromOneAndCarriesItsSpeed()
    {
        Assert.Equal("Row01_4000rpm", RunFolderName.ForRow(0, 4000));
        Assert.Equal("Row10_6500rpm", RunFolderName.ForRow(9, 6500.4));

        // Two-digit padding so a listing of a ten-row sweep stays in order.
        Assert.True(
            string.CompareOrdinal(RunFolderName.ForRow(1, 1000), RunFolderName.ForRow(9, 1000)) < 0);
    }
}
