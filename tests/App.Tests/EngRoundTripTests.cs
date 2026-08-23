using App.Persistence;

namespace App.Tests;

/// <summary>
/// The phase 2 acceptance gate: a legacy <c>.eng</c> file read and written back must
/// be byte for byte identical.
/// </summary>
public sealed class EngRoundTripTests
{
    public static TheoryData<string> Samples()
    {
        var data = new TheoryData<string>();
        foreach (var path in TestPaths.SampleEngineFiles())
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void SampleFileRoundTripsByteForByte(string fileName)
    {
        var path = Path.Combine(TestPaths.Samples, fileName);
        var original = File.ReadAllBytes(path);

        var written = IniDocument.Parse(original).ToBytes();

        Assert.Equal(original, written);
    }

    [Fact]
    public void SampleFixturesArePresent()
    {
        // Guards against a silently empty theory if the fixture copy ever breaks.
        Assert.NotEmpty(TestPaths.SampleEngineFiles());
    }

    /// <summary>
    /// The wider net: every <c>.eng</c> file in the untouched Delphi tree, including
    /// the five Example1 engines that use the older <c>[InManifold]</c> schema.
    /// </summary>
    [Fact]
    public void EveryLegacyEngineFileRoundTripsByteForByte()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var files = TestPaths.AllLegacyEngineFiles().ToList();
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var path in files)
        {
            var original = File.ReadAllBytes(path);
            if (!IniDocument.Parse(original).ToBytes().SequenceEqual(original))
            {
                failures.Add(Path.GetRelativePath(TestPaths.Legacy!, path));
            }
        }

        Assert.Empty(failures);
    }
}
