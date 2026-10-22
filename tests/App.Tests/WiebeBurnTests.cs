using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B89: the burn as a Wiebe law (a = 5, m = 2) from the start of combustion, the
/// spark plus an optional delay, in place of the original's raised cosine from the spark.
/// </summary>
public sealed class WiebeBurnTests
{
    private const double Spark = -20;
    private const double BurnAngle = 55;

    private static TwoZoneGas Gas(bool wiebe, double delay = 0)
    {
        var gas = new TwoZoneGas { WiebeBurn = wiebe };
        gas.State.ThetaSpark = Spark;
        gas.State.Fuel.BurnAngle = BurnAngle;
        gas.State.Fuel.CombustionDelay = delay;
        return gas;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    /// <summary>
    /// The oracle: a normalised Wiebe law with a = 5 and m = 2 reaches the burnt fraction
    /// x where u = (-ln(1 - x (1 - e^-5)) / 5)^(1/3) of the burn angle has elapsed, and is
    /// nothing at the start of combustion and everything at its end.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void TheBurntFractionIsTheWiebeLaw(double x)
    {
        var u = Math.Pow(-Math.Log(1 - (x * (1 - Math.Exp(-5)))) / 5, 1.0 / 3);

        Assert.Equal(x, Gas(wiebe: true).BurntFraction(Radians(Spark + (u * BurnAngle))), 1e-12);
    }

    [Fact]
    public void TheBurnIsNothingAtTheStartAndAllAtTheEnd()
    {
        var gas = Gas(wiebe: true);

        Assert.Equal(0, gas.BurntFraction(Radians(Spark)));
        Assert.Equal(1, gas.BurntFraction(Radians(Spark + BurnAngle - 1e-9)), 1e-9);
        Assert.Equal(1, gas.BurntFraction(Radians(Spark + BurnAngle)));
    }

    /// <summary>The burn rate per radian is the burnt fraction's derivative.</summary>
    [Theory]
    [InlineData(-15.0)]
    [InlineData(0.0)]
    [InlineData(10.0)]
    [InlineData(30.0)]
    public void TheBurnRateIsTheDerivative(double degrees)
    {
        var gas = Gas(wiebe: true);
        const double h = 1e-6;
        var theta = Radians(degrees);
        var difference = (gas.BurntFraction(theta + h) - gas.BurntFraction(theta - h)) / (2 * h);

        Assert.Equal(difference, gas.BurnRate(theta), 1e-6);
    }

    /// <summary>Off, the burn is the original's raised cosine from the spark, delay or none.</summary>
    [Theory]
    [InlineData(-10.0)]
    [InlineData(5.0)]
    [InlineData(25.0)]
    public void WithoutB89TheBurnIsTheRaisedCosineFromTheSpark(double degrees)
    {
        var gas = Gas(wiebe: false, delay: 7);

        Assert.Equal(0.5 * (1 - Math.Cos(Math.PI * (degrees - Spark) / BurnAngle)), gas.BurntFraction(Radians(degrees)));
        Assert.Equal(Spark, gas.StartOfCombustion);
    }

    [Theory]
    [InlineData(-10.0)]
    [InlineData(5.0)]
    [InlineData(25.0)]
    public void ADelayShiftsTheBurnByExactlyTheDelay(double degrees)
    {
        var delayed = Gas(wiebe: true, delay: 7);

        Assert.Equal(Spark + 7, delayed.StartOfCombustion);
        Assert.Equal(Gas(wiebe: true).BurntFraction(Radians(degrees)), delayed.BurntFraction(Radians(degrees + 7)), 1e-12);
        Assert.Equal(Gas(wiebe: true).BurnRate(Radians(degrees)), delayed.BurnRate(Radians(degrees + 7)), 1e-9);
    }

    [Fact]
    public void NoShippedFileHasADelay()
    {
        BaselinePaths.Require();

        Assert.Equal(0, Loader().Load(BaselinePaths.File("A2China.eng")).Engine.Cylinder.Fuel.CombustionDelay);
    }

    /// <summary>
    /// A file that gives the delay has it read, and survives a read and write byte for
    /// byte; and in a whole run under B89 combustion starts at the spark plus the delay.
    /// </summary>
    [Fact]
    public void ADelayInTheFileIsReadRoundTripsAndStartsCombustionLater()
    {
        BaselinePaths.Require();

        var folder = Directory.CreateTempSubdirectory("esa-wiebe-").FullName;

        try
        {
            foreach (var file in Directory.GetFiles(BaselinePaths.Directory!))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            var path = Path.Combine(folder, "A2China.eng");
            var text = File.ReadAllText(path);
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            File.WriteAllText(path, text.Replace("[Fuel]" + newline, "[Fuel]" + newline + "CombustionDelay=10" + newline, StringComparison.Ordinal));

            var store = new EngineDefinitionStore();
            var copy = Path.Combine(folder, "Copy.eng");
            store.Write(copy, store.Read(path));
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(copy));

            var engine = Loader().Load(path).Engine;
            Assert.Equal(10, engine.Cylinder.Fuel.CombustionDelay);

            engine.Rpm = 4000;
            var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
            var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
            Assert.True(solver.Initialise());

            var firstCombustion = double.NaN;
            var firstExpansion = double.NaN;
            var cycle = 0;

            solver.StepCompleted += s =>
            {
                if (cycle == 2 && double.IsNaN(firstCombustion) && s.Engine.State == EngineState.Combustion)
                {
                    firstCombustion = s.Engine.CrankAngle;
                }

                if (cycle == 2 && double.IsNaN(firstExpansion) && s.Engine.State == EngineState.Expansion)
                {
                    firstExpansion = s.Engine.CrankAngle;
                }
            };

            engine.ZoneCount = 1;

            for (cycle = 1; cycle <= 2; cycle++)
            {
                if (cycle > 1)
                {
                    engine.ZoneCount = 2;
                }

                solver.RunOneCycle();
            }

            Assert.Equal(-solver.SparkAdvance + 10, firstCombustion, 1.0);
            Assert.Equal(-solver.SparkAdvance + 10 + 55, firstExpansion, 1.0);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
