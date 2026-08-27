using System.Globalization;
using App.Core;
using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// The PVT text export, checked by round-tripping the original's own file through it.
/// </summary>
/// <remarks>
/// Reading <c>A2China.txt</c> back into a trace and writing it out again has to reproduce
/// it line for line. That tests the formatter against real output at full width without
/// depending on how accurately the simulation reproduces the values - the same bargain
/// <see cref="EngRoundTripTests"/> strikes for engine files.
/// </remarks>
public sealed class CrankAngleTraceWriterTests
{
    /// <summary>Parses the reference file back into a trace, undoing the display scaling.</summary>
    private static (CrankAngleTrace Trace, List<string> Lines) ReadReference()
    {
        var lines = File.ReadAllLines(BaselinePaths.File("A2China.txt"))
            .Where(line => line.Trim().Length > 0)
            .ToList();

        var trace = new CrankAngleTrace();

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(',');
            var crankAngle = int.Parse(fields[0], CultureInfo.InvariantCulture);
            var point = trace[crankAngle];

            for (var column = 1; column <= EsaLimits.CapturedValueCount; column++)
            {
                var displayed = double.Parse(fields[column], CultureInfo.InvariantCulture);
                point[column] = displayed / trace.ScaleFactors[column - 1];
            }
        }

        return (trace, lines);
    }

    [Fact]
    public void TheOriginalsOwnFileRoundTripsLineForLine()
    {
        BaselinePaths.Require();

        var (trace, original) = ReadReference();

        var produced = new CrankAngleTraceWriter().Format(trace)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(original.Count, produced.Length);

        var differences = original
            .Zip(produced, (before, after) => (before, after))
            .Select((pair, index) => (pair.before, pair.after, index))
            .Where(row => row.before != row.after)
            .ToList();

        Assert.True(
            differences.Count == 0,
            $"{differences.Count} of {original.Count} lines differ. First:\n"
            + string.Join("\n", differences.Take(3).Select(
                d => $"  line {d.index}\n  want |{d.before}|\n  got  |{d.after}|")));
    }

    [Fact]
    public void TheHeadingNamesAndOrderMatchTheOriginal()
    {
        BaselinePaths.Require();

        var (trace, original) = ReadReference();
        var produced = new CrankAngleTraceWriter().Format(trace).Split("\r\n")[0];

        Assert.Equal(original[0], produced);

        // The 28 columns in Delphi's own order, which the emissions and work columns at
        // the end make easy to get wrong.
        Assert.Equal("Vcyl", trace.ColumnNames[0]);
        Assert.Equal("htLoss", trace.ColumnNames[27]);
        Assert.Equal("WWork", trace.ColumnNames[21]);
    }

    [Fact]
    public void TheLastColumnIsScaledByItsOwnFactorNotTheSecondToLast()
    {
        // The original writes value[NoVals]*k[i], reusing a loop counter Pascal leaves
        // undefined. k[27] is 1000 and k[28] is 1, so getting this wrong would report
        // heat loss a thousand times too large. See ISSUES.md B67.
        var trace = new CrankAngleTrace();

        Assert.Equal(1e3, trace.ScaleFactors[26]);
        Assert.Equal(1, trace.ScaleFactors[27]);

        trace[0][28] = -381.67;

        var row = new CrankAngleTraceWriter().Format(trace)
            .Split("\r\n")
            .First(l => l.StartsWith("0,", StringComparison.Ordinal));

        Assert.EndsWith("-381.670", row, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingProducesTheSameTextAsFormatting()
    {
        BaselinePaths.Require();

        var (trace, _) = ReadReference();
        var writer = new CrankAngleTraceWriter();
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");

        try
        {
            writer.Write(path, trace);
            Assert.Equal(writer.Format(trace), File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
