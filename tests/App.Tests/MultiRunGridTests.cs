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

    /// <summary>Files a cell short, from before the Burn Angle column. See ISSUES.md C13.</summary>
    private static List<string> ShortFiles() =>
        [.. LegacyFiles().Where(p => File.ReadLines(p).First().Split(',').Length == 14)];

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
        // are a column short and are written back with that column filled in. See the
        // tests below and ISSUES.md C13.
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
    public void CellsAreFilledFromTheLeftAfterTheRowNumber()
    {
        // LoadGrid walks backwards from the end of the line, so a short line fills the
        // right-hand columns and leaves the left ones at their default. This parses
        // forwards instead, past the row number the line starts with. See ISSUES.md B68.
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");

        try
        {
            // Only three fields where fifteen are expected.
            File.WriteAllText(path, "1,4000,6\n");

            var document = new MultiRunGridStore().Read(path);
            var grid = document.Grid;

            // The two values landed in Speed and Iters, and the row number is discarded.
            Assert.Equal("4000", grid[0, 0]);
            Assert.Equal("6", grid[0, 1]);
            Assert.Equal(MultiRunGrid.Unset, grid[0, MultiRunGrid.ColumnCount - 1]);
            Assert.True(document.ShortFormat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("x,4000,6")]                                       // no row number
    [InlineData("1,-,-,-,-,-,-,-,-,-,-,-,-,-,-,-")]                // one cell too many
    public void ALineThatIsNotInTheFormatIsRefusedRatherThanMangled(string line)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");

        try
        {
            File.WriteAllText(path, line + "\n");

            Assert.Throws<FormatException>(() => new MultiRunGridStore().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MostShippedGridsAreAColumnShortAndStillLoadIntoTheRightColumns()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // Forty-three of the forty-nine shipped files carry thirteen cells where the
        // current grid has fourteen - they predate the Burn Angle column being added.
        // Parsing forwards puts each value in the column it was written from and leaves
        // the missing one unset, where LoadGrid filled from the right and shifted every
        // value over, the row number landing in Speed. See ISSUES.md C13.
        var short_ = ShortFiles();

        Assert.True(short_.Count > 40, $"Expected most files to be short, found {short_.Count}.");

        foreach (var path in short_)
        {
            var document = new MultiRunGridStore().Read(path);

            Assert.True(document.ShortFormat, path);

            // A real speed, not the row number, and never a grid counting 1, 2, 3 up.
            Assert.True(document.Grid.Speed(0) >= 100, path);
            Assert.Equal(MultiRunGrid.Unset, document.Grid[0, MultiRunGrid.ColumnCount - 1]);
        }
    }

    [Fact]
    public void AShortGridsValuesLandInTheColumnsTheHeadingsName()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // These sweep inlet manifold length: the manifold file belongs in IManfFile,
        // column two, which is where filling from the right would not have put it.
        var path = ShortFiles().FirstOrDefault(
            p => Path.GetFileName(p).Equals("VarInlet700900.msr", StringComparison.OrdinalIgnoreCase));

        Assert.SkipWhen(path is null, "VarInlet700900.msr is not in this checkout.");

        var grid = new MultiRunGridStore().Read(path!).Grid;

        Assert.Equal("IManfFile", MultiRunGrid.ColumnNames[2]);
        Assert.EndsWith("Inlet.maf", grid.Text(0, 2)!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(grid.Text(0, 3));
    }

    [Fact]
    public void AShortGridIsWrittenBackInTheCurrentFormatWithItsValuesIntact()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // A short file cannot round-trip byte for byte - writing it fills in the column
        // it is missing - but nothing else about it may move.
        var store = new MultiRunGridStore();
        var source = ShortFiles()[0];
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");

        try
        {
            var first = store.Read(source);

            store.Write(target, first);

            var second = store.Read(target);

            Assert.False(second.ShortFormat);
            Assert.Equal(15, File.ReadLines(target).First().Split(',').Length);

            for (var row = 0; row < MultiRunGrid.MaxRuns; row++)
            {
                for (var column = 0; column < MultiRunGrid.ColumnCount; column++)
                {
                    Assert.Equal(first.Grid[row, column], second.Grid[row, column]);
                }
            }
        }
        finally
        {
            File.Delete(target);
        }
    }
}
