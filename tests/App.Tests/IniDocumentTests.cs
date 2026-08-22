using App.Persistence;

namespace App.Tests;

public sealed class IniDocumentTests
{
    [Fact]
    public void SettingAValueChangesOnlyThatLine()
    {
        var source = Path.Combine(TestPaths.Samples, "Default.eng");
        var originalLines = File.ReadAllLines(source);

        var document = IniDocument.Parse(File.ReadAllBytes(source));
        document.SetValue("Cylinders", "Bore", "82.0");
        var writtenLines = document.ToText().Split('\n');

        Assert.Equal("Bore=82.0", writtenLines[3]);

        var differences = originalLines
            .Zip(writtenLines, (before, after) => (before, after))
            .Where(pair => pair.before != pair.after)
            .ToList();

        Assert.Single(differences);
    }

    [Theory]
    [InlineData("[A]\r\nx=1\r\n")]
    [InlineData("[A]\nx=1\n")]
    [InlineData("[A]\nx=1")]
    [InlineData("[A]\r\nx=1\ny=2\r\n")]
    [InlineData("\n\n[A]\n\n; comment\nx = 1 \n\n")]
    [InlineData("")]
    public void ArbitraryTerminatorsRoundTrip(string text)
    {
        Assert.Equal(text, IniDocument.Parse(text).ToText());
    }

    [Fact]
    public void KeysAreReportedInFileOrder()
    {
        var document = IniDocument.Parse("[A]\nb=2\na=1\nc=3\n");

        Assert.Equal(["b", "a", "c"], document.KeysIn("A"));
    }

    [Fact]
    public void ValuesKeepTheirExactText()
    {
        // Leading and trailing spaces inside a value are part of the legacy data.
        var document = IniDocument.Parse("[A]\nx= 1.50 \n");

        Assert.Equal(" 1.50 ", document.GetValue("A", "x"));
    }

    [Fact]
    public void AddingAKeyAppendsToItsSection()
    {
        var document = IniDocument.Parse("[A]\nx=1\n[B]\ny=2\n");

        document.SetValue("A", "z", "3");

        Assert.Equal("[A]\nx=1\nz=3\n[B]\ny=2\n", document.ToText());
    }

    [Fact]
    public void AddingAKeyToAnUnknownSectionAppendsBoth()
    {
        var document = IniDocument.Parse("[A]\nx=1\n");

        document.SetValue("C", "z", "3");

        Assert.Equal("[A]\nx=1\n[C]\nz=3\n", document.ToText());
    }

    [Fact]
    public void AppendingAfterAnUnterminatedFinalLineInsertsATerminator()
    {
        // The old final line must gain a terminator, or the appended key would run
        // into it. The appended line itself ends the file normally.
        var document = IniDocument.Parse("[A]\nx=1");

        document.SetValue("A", "z", "3");

        Assert.Equal("[A]\nx=1\nz=3\n", document.ToText());
    }

    [Fact]
    public void ByteOrderMarkIsPreserved()
    {
        byte[] source = [0xEF, 0xBB, 0xBF, .. "[A]\nx=1\n"u8];

        Assert.Equal(source, IniDocument.Parse(source).ToBytes());
    }
}
