using App.Core;
using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B92: the burn releases a combustion efficiency's share of the fuel's energy,
/// Heywood's 95 to 98 per cent for a spark-ignition engine (Fig. 3-9, p. 82), and the rest
/// leaves with the exhaust.
/// </summary>
public sealed class CombustionEfficiencyTests
{
    private const double Spark = -20;
    private const double BurnAngle = 55;
    private const double FuelMass = 3.5e-5;
    private const double Heating = 43e6;

    private static CylinderModel Model(double efficiency)
    {
        var gas = new TwoZoneGas { WiebeBurn = true };
        gas.State.ThetaSpark = Spark;
        gas.State.Fuel.BurnAngle = BurnAngle;
        gas.State.Fuel.M = FuelMass;
        gas.State.Fuel.Q = Heating;

        var geometry = new CylinderGeometry(
            bore: 0.081, stroke: 0.0774, compressionRatio: 9.2, conrodLength: 0.149, cylinderCount: 4);

        return new CylinderModel(geometry, gas, new TwoZoneGas(), new WallTemperatureTable())
        {
            Rpm = 4000,
            CrankAngularVelocity = 4000 * Math.PI / 30,
            CombustionEfficiency = efficiency,
        };
    }

    /// <summary>
    /// The oracle: over the whole burn the loss adds up to exactly the unreleased share of the
    /// fuel's energy, (1 - efficiency) times the fuel mass times its heating value, since the
    /// burn rate integrates to one.
    /// </summary>
    [Theory]
    [InlineData(0.965)]
    [InlineData(0.95)]
    [InlineData(0.98)]
    public void OverTheBurnTheLossIsTheUnreleasedShareOfTheFuelEnergy(double efficiency)
    {
        var model = Model(efficiency);
        const int Steps = 20000;
        var from = Spark * Math.PI / 180;
        var to = (Spark + BurnAngle) * Math.PI / 180;
        var h = (to - from) / Steps;

        var sum = 0.0;
        for (var i = 0; i < Steps; i++)
        {
            sum += model.CombustionLossRate(from + ((i + 0.5) * h)) * h;
        }

        Assert.Equal(-(1 - efficiency) * FuelMass * Heating, sum, 1e-6 * FuelMass * Heating);
    }

    [Fact]
    public void WithoutB92NothingIsLost()
    {
        var model = Model(1);

        Assert.Equal(0, model.CombustionLossRate(10 * Math.PI / 180));
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.CombustionEfficiency));
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.CombustionEfficiency));
    }

    [Fact]
    public void TheDefaultIsTheMidpointOfHeywoodsRange()
    {
        Assert.Equal((0.95 + 0.98) / 2, CombustionLoss.DefaultEfficiency, 1e-12);
    }

    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static Engine RunAt4000(Engine engine, bool b92)
    {
        engine.Rpm = 4000;
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.CombustionEfficiency.Entry] = b92;

        return new SimulationRunner(new CachingExpressionEvaluator())
            .Run(engine, new SimulationSettings { CycleCount = 8, OneZoneCycleCount = 1, MassBalance = 1, Physics = physics },
                cancellation: TestContext.Current.CancellationToken)
            .Engine;
    }

    /// <summary>
    /// In a whole run the unreleased energy reaches the exhaust, not the walls. The exhaust share
    /// is the energy balance's remainder, so it gains the unreleased 3.5 per cent of the fuel's
    /// energy and loses what the cooler gas no longer carries as sensible heat: it rises, by less
    /// than 3.5 points. Brake work falls a few per cent, and so does the heat to the walls, which
    /// the cooler gas heats less, but by under a point.
    /// Measured on the baseline engine at 4000 rpm: torque 183.3 to 177.5 Nm, heat 17.7 to
    /// 17.0 per cent, exhaust 44.3 to 46.0.
    /// </summary>
    [Fact]
    public void TheUnreleasedEnergyReachesTheExhaustNotTheWalls()
    {
        BaselinePaths.Require();

        var off = RunAt4000(Loader().Load(BaselinePaths.File("A2China.eng")).Engine, b92: false);
        var on = RunAt4000(Loader().Load(BaselinePaths.File("A2China.eng")).Engine, b92: true);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"torque {off.Torque:F2} to {on.Torque:F2} Nm, heat {off.QHeat:F2} to {on.QHeat:F2} %, exhaust {off.QExhaust:F2} to {on.QExhaust:F2} %");

        Assert.InRange(on.Torque / off.Torque, 0.95, 0.99);
        Assert.InRange(on.QHeat - off.QHeat, -1.0, 0);
        Assert.InRange(on.QExhaust - off.QExhaust, 0.5, (1 - CombustionLoss.DefaultEfficiency) * 100);
    }

    /// <summary>A file that gives the efficiency has it read, and survives a read and write byte for byte.</summary>
    [Fact]
    public void AnEfficiencyInTheFileIsReadAndRoundTrips()
    {
        BaselinePaths.Require();

        var folder = Directory.CreateTempSubdirectory("esa-b92-").FullName;

        try
        {
            foreach (var file in Directory.GetFiles(BaselinePaths.Directory!))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            var path = Path.Combine(folder, "A2China.eng");
            var text = File.ReadAllText(path);
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            File.WriteAllText(path, text.Replace("[Fuel]" + newline, "[Fuel]" + newline + "CombustionEfficiency=0.95" + newline, StringComparison.Ordinal));

            var store = new EngineDefinitionStore();
            var copy = Path.Combine(folder, "Copy.eng");
            store.Write(copy, store.Read(path));
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(copy));

            Assert.Equal(0.95, Loader().Load(path).Engine.Cylinder.Fuel.CombustionEfficiency);
            Assert.Null(Loader().Load(BaselinePaths.File("A2China.eng")).Engine.Cylinder.Fuel.CombustionEfficiency);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
