using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B87: torque from the cycle's net indicated work less the friction correlation,
/// where the original takes the work from inlet closing to exhaust opening less it.
/// </summary>
public sealed class NetWorkTorqueTests
{
    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static PhysicsCorrections Physics(bool netWork)
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.NetWorkTorque.Entry] = netWork;
        return physics;
    }

    /// <summary>The trace's p dV over the whole cycle, by the trapezoid.</summary>
    private static double CycleWork(CrankAngleTrace trace)
    {
        var work = 0.0;

        for (var angle = -359; angle < 360; angle++)
        {
            var (a, b) = (trace[angle], trace[angle + 1]);
            work += 0.5 * (a[2] + b[2]) * (b[1] - a[1]);
        }

        // And round from 360 back to -359.
        var (last, first) = (trace[360], trace[-359]);
        return work + (0.5 * (last[2] + first[2]) * (first[1] - last[1]));
    }

    /// <summary>
    /// B87's oracle. A cycle's brake work is the net work its gas does on the piston, the p dV
    /// of the whole cycle, less friction. Under B87 the reported BMEP is exactly that, to
    /// the trace's resolution. The original's is not: it counts the work from inlet closing
    /// to exhaust opening only, and is out by the gas exchange's work.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrakeWorkIsTheCyclesPdVLessFriction(bool netWork)
    {
        BaselinePaths.Require();

        var engine = Loader().Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;

        var result = new SimulationRunner(new CachingExpressionEvaluator()).Run(
            engine,
            new SimulationSettings { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1, Physics = Physics(netWork) },
            cancellation: TestContext.Current.CancellationToken);

        var e = result.Engine;
        var expected = CycleWork(result.Trace) - (PerformanceCalculator.TotalFmep(e.Rpm) * e.SweptVolume);
        var brake = e.Bmep * e.SweptVolume;

        if (netWork)
        {
            Assert.Equal(expected, brake, Math.Abs(expected) * 0.005);
        }
        else
        {
            Assert.True(Math.Abs(brake - expected) > Math.Abs(expected) * 0.02, $"{brake} against {expected}");
        }
    }

    /// <summary>
    /// The same cycle's accumulators give one BMEP under B87 however the original divides the
    /// work between IMEP and PMEP: moving work from one to the other moves nothing.
    /// </summary>
    [Fact]
    public void MovingWorkBetweenImepAndPmepMovesNothing()
    {
        var engine = new Engine { Rpm = 4000, SweptVolume = 4e-4 };
        engine.Work = 600;
        engine.PumpingWork = 20;

        var calculator = new PerformanceCalculator();
        calculator.Calculate(engine, Physics(true));
        var bmep = engine.Bmep;

        engine.Work = 570;
        engine.PumpingWork = -10;
        calculator.Calculate(engine, Physics(true));

        Assert.Equal(bmep, engine.Bmep, 1e-6);
    }

    /// <summary>
    /// What it changes on the engine it was found on: the eight-valve engine's cam B, whose
    /// exhaust opens 20 degrees earlier than cam A's, gains torque on cam A at 5000 rpm under
    /// B87, as on the dynamometer, and loses it under the original (ISSUES.md F7).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CamBGainsOnCamAAtSpeedOnlyFromTheNetWork(bool netWork)
    {
        BaselinePaths.Require();

        var grid = new MultiRunGrid();
        string[][] cams = [["23", "66", "51", "38", "8.6", "8.6"], ["29", "75", "71", "33", "10", "10"]];

        for (var row = 0; row < 2; row++)
        {
            grid[row, 0] = "5000";
            grid[row, 1] = "8";

            for (var column = 0; column < 6; column++)
            {
                grid[row, 6 + column] = cams[row][column];
            }
        }

        var rows = new MultiRunner(Loader(), new SimulationRunner(new CachingExpressionEvaluator())).Run(
            Path.Combine(BaselinePaths.Directory!, "..", "..", "validation", "cases", "VW8V_A4LowCost", "A4LowCost.eng"),
            grid,
            new SimulationSettings { CycleCount = 8, OneZoneCycleCount = 1, MassBalance = 1, Physics = Physics(netWork) },
            cancellation: TestContext.Current.CancellationToken);

        var gain = rows[1].Result!.Engine.Torque - rows[0].Result!.Engine.Torque;

        Assert.True(netWork ? gain > 0 : gain < 0, $"{gain}");
    }
}
