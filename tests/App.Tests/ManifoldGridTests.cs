using App.Core;
using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The manifold grid: how many points each pipe gets, where they sit, and what state they
/// start in.
/// </summary>
/// <remarks>
/// This is the first thing phase 4b has to get right, because a wrong point count makes
/// every later field comparison meaningless. The baseline gives it away without being
/// asked: the <c>.m</c> field files carry one column per grid point, 39 for the inlet and
/// 16 for the exhaust.
/// </remarks>
public sealed class ManifoldGridTests
{
    private const double Rpm = 4000;
    private const double Gamma = 1.3994;

    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = Rpm;
        return engine;
    }

    private static int ColumnCount(string fieldFile) =>
        System.IO.File.ReadLines(BaselinePaths.File(fieldFile)).First()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    [Fact]
    public void GridSizesMatchTheColumnCountsOfTheOriginalsOwnFieldFiles()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var calculator = new GridSizeCalculator(new CachingExpressionEvaluator());

        var inletPipe = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);
        var exhaustPipe = new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength);

        var inlet = calculator.InletGridSize(
            engine.Manifold.InletGrid.Expression, inletPipe.Length, Rpm);
        var exhaust = calculator.ExhaustGridSize(
            engine.Manifold.ExhaustGrid.Expression, exhaustPipe.Length, Rpm);

        // The expressions are sixth and fifth order polynomials in engine speed, scaled
        // by pipe length and rounded. Landing on exactly the right integer validates the
        // whole phase 3 expression chain, DelphiMath.Power's integer branch included.
        Assert.Equal(ColumnCount("InlPress.m"), inlet);
        Assert.Equal(ColumnCount("ExhPress.m"), exhaust);
        Assert.Equal(39, inlet);
        Assert.Equal(16, exhaust);

        // And both sit inside the fixed legacy maxima.
        Assert.True(inlet <= EsaLimits.InletGridPoints);
        Assert.True(exhaust <= EsaLimits.ExhaustGridPoints);
    }

    [Fact]
    public void EveryFieldFileAgreesWithItsPipesGridSize()
    {
        BaselinePaths.Require();

        // Pressure and velocity are written for the same grid, so all four files have to
        // agree in pairs. If they ever disagree the fixture has been regenerated wrongly.
        Assert.Equal(ColumnCount("InlPress.m"), ColumnCount("InlVel.m"));
        Assert.Equal(ColumnCount("ExhPress.m"), ColumnCount("ExhVel.m"));
    }

    [Fact]
    public void PipeLengthIsTheLastPositionInTheAreaTable()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var inlet = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);

        var table = engine.Manifold.InletPipe.AreaVersusLength;
        Assert.Equal(table.Position[table.Count - 1] / 1000, inlet.Length, 12);

        // Metres, not the millimetres the .maf file holds.
        Assert.True(inlet.Length is > 0.05 and < 2, $"Inlet pipe length came out as {inlet.Length} m.");
    }

    [Fact]
    public void TheGridSpansThePipeWithEvenSpacing()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var pipe = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);
        var grid = new PipeGrid(EsaLimits.InletGridPoints);

        PipeGridInitialiser.Initialise(grid, 39, pipe.Length, 99000, 298.15, Gamma);

        Assert.Equal(39, grid.ActiveCount);
        Assert.Equal(0, grid.X[0]);

        // Q-1 intervals, so the last point lands exactly on the far end of the pipe.
        Assert.Equal(pipe.Length, grid.X[38], 12);

        var spacing = pipe.Length / 38;
        for (var i = 1; i < 39; i++)
        {
            Assert.Equal(spacing, grid.X[i] - grid.X[i - 1], 12);
        }
    }

    [Fact]
    public void TheGridStartsStagnantAndUniform()
    {
        BaselinePaths.Require();

        var grid = new PipeGrid(EsaLimits.ExhaustGridPoints);
        PipeGridInitialiser.Initialise(grid, 16, 0.5, 117800, 973.15, Gamma);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(0, grid.Velocity[i]);
            Assert.Equal(117800, grid.Pressure[i]);
            Assert.Equal(973.15, grid.Temperature[i]);

            // The manifold solver uses the universal 287 throughout, never the burnt
            // mixture's own gas constant.
            Assert.Equal(117800.0 / 287 / 973.15, grid.Density[i], 12);
            Assert.Equal(Math.Sqrt(Gamma * 287 * 973.15), grid.SpeedOfSound[i], 12);
        }
    }

    [Fact]
    public void AGridLargerThanTheLegacyMaximumIsRefused()
    {
        var grid = new PipeGrid(EsaLimits.ExhaustGridPoints);

        var error = Assert.Throws<CfdException>(
            () => PipeGridInitialiser.Initialise(grid, EsaLimits.ExhaustGridPoints + 1, 0.5, 1e5, 300, Gamma));

        Assert.Contains("greater than maximum", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAreaGradientSidestepsTheLookupCliffAtTheEndOfThePipe()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var pipe = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);

        // Past the last table entry the lookup falls to zero rather than clamping
        // (ISSUES.md B4), so a central difference at the very end would read as a huge
        // negative gradient. TPipe.dAdL detects the zero and switches to a backward
        // difference instead.
        var atEnd = pipe.AreaGradient(pipe.Length);
        var justInside = pipe.AreaGradient(pipe.Length - 0.004);

        Assert.True(
            Math.Abs(atEnd) < Math.Abs(justInside) * 100 + 1e-6,
            $"Gradient at the pipe end is {atEnd}, against {justInside} just inside it.");

        // The cliff itself is still there; only dAdL works around it.
        Assert.Equal(0, pipe.Area(pipe.Length + 0.01));
    }
}
