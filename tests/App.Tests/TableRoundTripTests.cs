using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The write-fidelity gate for the two formats the app saves. Same bargain as
/// <see cref="EngRoundTripTests"/>: reading a file and writing it back unchanged must
/// reproduce it byte for byte, so the table editors never restyle a user's data.
/// </summary>
public sealed class TableRoundTripTests
{
    private static void RequireLegacy() =>
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

    private static IEnumerable<string> LegacyFiles(string extension) =>
        Directory.EnumerateFiles(TestPaths.Legacy!, extension, SearchOption.AllDirectories).Order();

    [Fact]
    public void EveryManifoldAreaFileRoundTripsByteForByte()
    {
        RequireLegacy();

        var store = new ManifoldAreaTableStore();
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".maf");
        var failures = new List<string>();
        var checkedFiles = 0;

        try
        {
            foreach (var path in LegacyFiles("*.maf"))
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

        Assert.True(checkedFiles > 0, "No .maf files were found to test.");
        Assert.Empty(failures);
    }

    [Fact]
    public void EveryDischargeCoefficientFileRoundTripsByteForByte()
    {
        RequireLegacy();

        var store = new DischargeCoefficientTableStore();
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".vcd");
        var failures = new List<string>();
        var checkedFiles = 0;

        try
        {
            foreach (var path in LegacyFiles("*.vcd"))
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

        Assert.True(checkedFiles > 0, "No .vcd files were found to test.");
        Assert.Empty(failures);
    }

    [Fact]
    public void EditingOneAreaCellLeavesEveryOtherByteAlone()
    {
        RequireLegacy();

        var source = Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Example1", "290Inlet.maf");
        var originalLines = File.ReadAllLines(source);

        var store = new ManifoldAreaTableStore();
        var document = store.Read(source);
        document.SetRow(2, "290", "1100");

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".maf");
        try
        {
            store.Write(target, document);
            var written = File.ReadAllLines(target);

            Assert.Equal("3,290,1100", written[2]);

            var differences = originalLines
                .Zip(written, (before, after) => (before, after))
                .Where(pair => pair.before != pair.after)
                .ToList();

            Assert.Single(differences);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void EditingOneCoefficientLeavesEveryOtherByteAlone()
    {
        RequireLegacy();

        var source = Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Example1", "Nissan IVIn.vcd");
        var originalLines = File.ReadAllLines(source);

        var store = new DischargeCoefficientTableStore();
        var document = store.Read(source);
        document.SetCell(row: 1, column: 0, value: "0.99");

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".vcd");
        try
        {
            store.Write(target, document);
            var written = File.ReadAllLines(target);

            var differences = originalLines
                .Zip(written, (before, after) => (before, after))
                .Where(pair => pair.before != pair.after)
                .ToList();

            Assert.Single(differences);
            Assert.Contains("0.99", differences[0].after, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(target);
        }
    }
}
