using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// The multi-run grid and its <c>.msr</c> file format.
/// </summary>
public sealed class MultiRunGridTests
{
    private static IEnumerable<string> LegacyFiles() =>
        TestPaths.Legacy is null
            ? []
            : Directory.EnumerateFiles(TestPaths.Legacy, "*.msr", SearchOption.AllDirectories).Order();

    /// <summary>Files written by the current fifteen-field format.</summary>
    private static IEnumerable<string> WellFormedFiles() =>
        LegacyFiles().Where(p => File.ReadLines(p).First().Split(',').Length == 15);

    [Fact]
    public void EveryWellFormedGridRoundTripsByteForByte()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var store = new MultiRunGridStore();
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");
        var failures = new List<string>();
        var checkedFiles = 0;

        try
        {
            foreach (var path in WellFormedFiles())
            {
                var original = File.ReadAllBytes(path);
                store.Write(target, store.Read(path));

                if (!File.ReadAllBytes(target).SequenceEqual(original))
                {
                    failures.Add(Path.GetRelativePath(TestPaths.Legacy!, path));
                }

                checkedFiles++;
            }
        }
        finally
        {
            File.Delete(target);
        }

        // Only six of the forty-nine shipped files are in the current format; the rest
        // are a column short. See the test below and ISSUES.md C13.
        Assert.True(checkedFiles >= 6, $"Expected the well-formed .msr files, found {checkedFiles}.");
        Assert.Empty(failures);
    }

    [Fact]
    public void ASavedGridIsAlwaysAHundredRowsLong()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // SaveGrid writes every row whether or not it holds anything, so the shipped
        // files are all exactly MaxNoRuns lines.
        foreach (var path in LegacyFiles().Take(5))
        {
            var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
            Assert.Equal(MultiRunGrid.MaxRuns, lines.Count);
        }
    }

    [Fact]
    public void TheRunCountStopsAtTheFirstUnsetSpeed()
    {
        var grid = new MultiRunGrid();

        Assert.Equal(0, grid.RunCount);

        grid[0, 0] = "2000";
        grid[1, 0] = "3000";
        grid[2, 0] = "4000";

        Assert.Equal(3, grid.RunCount);

        // A gap truncates the list rather than being skipped: the original scans for the
        // first dash and stops there, so anything below a blank row is silently ignored.
        grid[4, 0] = "6000";
        Assert.Equal(3, grid.RunCount);
    }

    [Fact]
    public void ADashMeansNoOverrideRatherThanAValue()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var path = WellFormedFiles().First();
        var grid = new MultiRunGridStore().Read(path).Grid;

        // These sweep speed with everything else left to the engine file.
        Assert.NotNull(grid.Speed(0));
        Assert.Equal(5, grid.Cycles(0));

        Assert.Null(grid.Text(0, 2));
        Assert.Null(grid.Number(0, 6));

        // And the unfilled rows carry no speed at all.
        Assert.Null(grid.Speed(MultiRunGrid.MaxRuns - 1));
    }

    [Fact]
    public void CellsAreFilledFromTheRightAsTheOriginalParses()
    {
        // LoadGrid walks backwards from the end of the line, so a short line fills the
        // right-hand columns and leaves the left ones at their default. See ISSUES.md B68.
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");

        try
        {
            // Only three fields where fifteen are expected.
            File.WriteAllText(path, "1,4000,6\n");

            var grid = new MultiRunGridStore().Read(path).Grid;

            // The two values landed in the last two columns, not the first two.
            Assert.Equal("4000", grid[0, MultiRunGrid.ColumnCount - 2]);
            Assert.Equal("6", grid[0, MultiRunGrid.ColumnCount - 1]);
            Assert.Equal(MultiRunGrid.Unset, grid[0, 0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MostShippedGridsAreAColumnShortAndLoadShifted()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // Forty-three of the forty-nine shipped files carry thirteen cells where the
        // current grid has fourteen - they predate a column being added. LoadGrid fills
        // from the right and does not notice, so every value lands one column over and
        // the row number itself ends up in the speed column. See ISSUES.md C13.
        var short_ = LegacyFiles()
            .Where(p => File.ReadLines(p).First().Split(',').Length == 14)
            .ToList();

        Assert.True(short_.Count > 40, $"Expected most files to be short, found {short_.Count}.");

        var grid = new MultiRunGridStore().Read(short_[0]).Grid;

        // Row one's speed reads as "1" - its own row number - rather than a real speed.
        Assert.Equal("1", grid[0, 0]);
        Assert.Equal(1, grid.Speed(0));

        // And row two reads as 2, which is the giveaway: a speed column counting up in
        // ones is the signature of this shift.
        Assert.Equal(2, grid.Speed(1));
    }
}
