using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Sweeping a grid of operating points.
/// </summary>
public sealed class MultiRunnerTests
{
    private static MultiRunner Runner()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

        return new MultiRunner(loader, new SimulationRunner(new CachingExpressionEvaluator()));
    }

    private static SimulationSettings Settings() =>
        new() { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1 };

    private static MultiRunGrid SpeedSweep(params double[] speeds)
    {
        var grid = new MultiRunGrid();

        for (var row = 0; row < speeds.Length; row++)
        {
            grid[row, 0] = speeds[row].ToString(System.Globalization.CultureInfo.InvariantCulture);
            grid[row, 1] = "6";
        }

        return grid;
    }

    [Fact]
    public void ASpeedSweepProducesOnePointPerRow()
    {
        BaselinePaths.Require();

        var results = Runner().Run(
            BaselinePaths.File("A2China.eng"),
            SpeedSweep(3000, 4000),
            Settings(),
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Null(r.Failure));

        Assert.Equal(3000, results[0].Speed);
        Assert.Equal(4000, results[1].Speed);

        // Torque rises with speed on this engine between these two points, and the 4000
        // rev/min row is the one the reference run captured.
        Assert.InRange(results[1].Result!.Engine.Torque, 140, 165);
    }

    [Fact]
    public void EachRowStartsFromAFreshEngineSoOverridesDoNotLeak()
    {
        BaselinePaths.Require();

        var grid = SpeedSweep(4000, 4000);

        // Override the burn angle on the first row only.
        grid[0, 13] = "80";

        var results = Runner().Run(
            BaselinePaths.File("A2China.eng"), grid, Settings(),
            cancellation: TestContext.Current.CancellationToken);

        // Same speed, different burn angle, so the two rows must differ - and the second
        // must be back at the engine file's own 55 degrees rather than carrying 80 over.
        Assert.Equal(80, results[0].Result!.Engine.Cylinder.Fuel.BurnAngle);
        Assert.Equal(55, results[1].Result!.Engine.Cylinder.Fuel.BurnAngle);
        Assert.NotEqual(results[0].Result!.Engine.Torque, results[1].Result!.Engine.Torque);
    }

    [Fact]
    public void TheSparkOverrideSurvivesInitialisation()
    {
        BaselinePaths.Require();

        var grid = SpeedSweep(4000);
        grid[0, 12] = "30";

        var results = Runner().Run(
            BaselinePaths.File("A2China.eng"), grid, Settings(),
            cancellation: TestContext.Current.CancellationToken);

        // Initialise derives the spark angle from the .spk map, which gives 21 at 4000
        // rev/min. The grid's override is applied afterwards, exactly as the original
        // applies it after InitVars - set any earlier and it would be overwritten.
        Assert.Equal(-30, results[0].Result!.Engine.Cylinder.ThetaSpark);
    }

    [Fact]
    public void AFailingRowIsRecordedRatherThanAbandoningTheSweep()
    {
        BaselinePaths.Require();

        // An engine file that is not there fails every row, which is enough to show that
        // failures are caught per row and recorded rather than thrown out of the sweep.
        var results = Runner().Run(
            Path.Combine(Path.GetTempPath(), "no-such-engine.eng"),
            SpeedSweep(3000, 4000, 5000),
            Settings(),
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotNull(r.Failure));
        Assert.All(results, r => Assert.Null(r.Result));
    }

    [Fact]
    public void AZeroBurnAngleRunsAndReportsNegativeWork()
    {
        BaselinePaths.Require();

        var grid = SpeedSweep(4000);
        grid[0, 13] = "0";

        var results = Runner().Run(
            BaselinePaths.File("A2China.eng"), grid, Settings(),
            cancellation: TestContext.Current.CancellationToken);

        // Not a defect, and worth pinning so nobody "fixes" it: a burn angle of zero
        // means no combustion, so the cycle is pure pumping loss and the reported torque
        // is negative. Nothing validates the value, and nothing needs to - the answer is
        // the right one for the question asked.
        Assert.Null(results[0].Failure);
        Assert.True(
            results[0].Result!.Engine.Torque < 0,
            $"Expected negative torque with no combustion, got {results[0].Result!.Engine.Torque:F1}.");
    }

    [Fact]
    public void OnlyPopulatedRowsAreRun()
    {
        BaselinePaths.Require();

        var grid = SpeedSweep(4000);

        // The grid is always a hundred rows; RunCount stops at the first unset speed.
        Assert.Equal(1, grid.RunCount);

        var results = Runner().Run(
            BaselinePaths.File("A2China.eng"), grid, Settings(),
            cancellation: TestContext.Current.CancellationToken);

        Assert.Single(results);
    }

    [Fact]
    public void AShortFormatGridSweepsTheSpeedsItHolds()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // 43 of the 49 shipped .msr files are a column short, from before the Burn Angle
        // column (ISSUES.md C13). The original filled from the right, so the row number
        // landed in the speed column and a sweep started at 1 rev/min; parsing forwards
        // gives the speeds the file was written with.
        var path = Directory
            .EnumerateFiles(TestPaths.Legacy!, "Default.msr", SearchOption.AllDirectories)
            .First();

        var document = new MultiRunGridStore().Read(path);

        Assert.True(document.ShortFormat);
        Assert.Equal(2000, document.Grid.Speed(0));
        Assert.Equal(3000, document.Grid.Speed(1));

        // The trailing column the file predates is left unset, i.e. no override.
        Assert.Null(document.Grid.Number(0, MultiRunGrid.ColumnCount - 1));
        Assert.True(document.Grid.RunCount > 1);
    }
}
