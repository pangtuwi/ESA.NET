using App.Core;
using App.Core.Interpolation;
using App.Core.Manifold;
using App.Core.Model;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B4 and B5: the two legacy lookups the register suspected.
/// </summary>
public sealed class LookupTests
{
    /// <summary>A pipe tapering from 1200 mm² to 800 mm² over 400 mm.</summary>
    private static ManifoldAreaTable TaperedTable()
    {
        var table = new ManifoldAreaTable { Count = 3 };
        table.Position[0] = 0;
        table.Position[1] = 200;
        table.Position[2] = 400;
        table.Area[0] = 1200;
        table.Area[1] = 1000;
        table.Area[2] = 800;

        return table;
    }

    /// <summary>
    /// B4's oracle, part one. A pipe does not stop having an area a hair past the last
    /// entry of its table; with the clamp it keeps the last one, where the original falls
    /// to zero.
    /// </summary>
    [Fact]
    public void PastTheEndOfTheTableTheAreaHoldsInsteadOfFallingToZero()
    {
        var table = TaperedTable();

        Assert.Equal(800, LegacyInterpolation.AreaAt(table, 400.001, clampPastEnd: true));
        Assert.Equal(0, LegacyInterpolation.AreaAt(table, 400.001));

        // Inside the table the two agree exactly.
        foreach (var position in new[] { -5.0, 0, 50, 200, 333.3, 400 })
        {
            Assert.Equal(
                LegacyInterpolation.AreaAt(table, position),
                LegacyInterpolation.AreaAt(table, position, clampPastEnd: true));
        }
    }

    /// <summary>
    /// B4's oracle, part two. The original's end-of-pipe gradient leans on the cliff - a
    /// zero past the end switches it to a backward difference - so the clamp finds the end
    /// by position instead, and the gradient at every point along the pipe is unchanged.
    /// </summary>
    [Fact]
    public void TheEndOfPipeGradientIsUnchangedByTheClamp()
    {
        var legacy = new PipeGeometry(TaperedTable());
        var clamped = new PipeGeometry(TaperedTable(), clampPastEnd: true);

        for (var millimetres = 0.0; millimetres <= 400; millimetres += 0.5)
        {
            var x = millimetres / 1000;
            Assert.Equal(legacy.AreaGradient(x), clamped.AreaGradient(x));
        }

        // At the very end it is the backward difference over the last two millimetres: the
        // taper's -1 mm²/mm, which is -0.001 m²/m.
        Assert.Equal(-0.001, clamped.AreaGradient(0.4), 12);
    }

    /// <summary>
    /// B4's oracle, part three. A grid whose last point lands a hair past the end of its
    /// table - which floating point can do to any grid laid to the table's length - gives
    /// that point zero area and the friction term a zero diameter. The original stops on a
    /// non-finite state; with the clamp the wall point is solved like any other.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AGridPointJustPastTheTableIsStillInsideThePipe(bool clamped)
    {
        var table = TaperedTable();
        var pipe = new PipeGeometry(table, clamped);

        var current = new PipeGrid(EsaLimits.InletGridPoints);
        var next = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(current, 21, 0.4000001, 100000, 300, 1.4);
        PipeGridInitialiser.Initialise(next, 21, 0.4000001, 100000, 300, 1.4);

        // The wall point still carries the velocity it had while the valve was open, so
        // the friction term at the wall is live.
        current.Velocity[20] = 30;
        var dt = 0.5 * (current.X[1] - current.X[0]) / Math.Sqrt(1.4 * 287 * 300);

        void Shut() => ClosedValveBoundary.ApplyInlet(current, next, pipe, dt, gamma: 1.4);

        if (clamped)
        {
            Shut();
            Assert.True(double.IsFinite(next.Pressure[20]), $"{next.Pressure[20]}");
            Assert.True(double.IsFinite(next.Density[20]), $"{next.Density[20]}");
        }
        else
        {
            Assert.Throws<CfdException>(Shut);
        }
    }

    /// <summary>
    /// B5, closed as not a defect. The last step of <c>TCdValve.GetValue</c> lists its two
    /// points in the opposite order to the x interpolations, but each y is paired with its
    /// own row's value, and a straight line through two points is the same line whichever
    /// is named first. Over every shipped <c>.vcd</c>, inside the grid and beyond it, the
    /// lookup agrees with a conventionally written bilinear interpolation to rounding.
    /// </summary>
    [Fact]
    public void TheCdLookupIsAnOrdinaryBilinearInterpolation()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var store = new DischargeCoefficientTableStore();
        var tables = 0;
        var worst = 0.0;

        foreach (var path in Directory.EnumerateFiles(TestPaths.Legacy!, "*.vcd", SearchOption.AllDirectories))
        {
            var table = store.Read(path).Table;
            if (table.XCount < 2 || table.YCount < 2)
            {
                continue;
            }

            tables++;

            var xLow = table.XIndex[0];
            var xHigh = table.XIndex[table.XCount - 1];
            var yLow = table.YIndex[0];
            var yHigh = table.YIndex[table.YCount - 1];

            for (var i = -2; i <= 42; i++)
            {
                for (var j = -2; j <= 42; j++)
                {
                    var x = xLow + ((xHigh - xLow) * i / 40);
                    var y = yLow + ((yHigh - yLow) * j / 40);

                    var lookup = LegacyInterpolation.CoefficientAt(table, x, y);
                    var conventional = Bilinear(table, x, y);

                    worst = Math.Max(worst, Math.Abs(lookup - conventional) / Math.Max(1, Math.Abs(conventional)));
                }
            }
        }

        Assert.True(tables > 0, "No .vcd tables were found.");
        Assert.InRange(worst, 0, 1e-12);
    }

    /// <summary>
    /// The same cell selection as the original, with the y step written low point first.
    /// </summary>
    private static double Bilinear(DischargeCoefficientTable table, double x, double y)
    {
        var xi = 0;
        while (xi < table.XCount - 1 && table.XIndex[xi] < x)
        {
            xi++;
        }

        var yi = 0;
        while (yi < table.YCount - 1 && table.YIndex[yi] < y)
        {
            yi++;
        }

        xi = Math.Max(xi, 1);
        yi = Math.Max(yi, 1);

        double Line(double at, double a, double fa, double b, double fb) => fa + ((fb - fa) * (at - a) / (b - a));

        var lower = Line(x, table.XIndex[xi - 1], table.Cell[xi - 1, yi - 1], table.XIndex[xi], table.Cell[xi, yi - 1]);
        var upper = Line(x, table.XIndex[xi - 1], table.Cell[xi - 1, yi], table.XIndex[xi], table.Cell[xi, yi]);

        return Line(y, table.YIndex[yi - 1], lower, table.YIndex[yi], upper);
    }
}
