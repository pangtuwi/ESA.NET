using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B88: friction from the engine's design, Sandoval and Heywood's revision of the
/// Patton-Nitschke-Heywood model, with pumping from the simulated cycle only, in place of
/// the Barnes-Moss curve in speed, which as a motored total carries pumping of its own.
/// </summary>
public sealed class MechanicalFrictionTests
{
    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static string CaseFile(string relative) =>
        Path.Combine(BaselinePaths.Directory!, "..", "..", "validation", "cases", relative);

    /// <summary>
    /// The oracle: each component is the thesis's equation, evaluated by hand for the
    /// eight-valve engine at 4000 rpm and the plenum at atmospheric. Bore 81 mm, stroke 77.4,
    /// four cylinders, compression ratio 10.2, eight valves of 10.6 mm lift, and the stated
    /// defaults: main bearings 54.27 by 20.25 mm, five of them, rod bearings 47.79 by 17.82,
    /// five camshaft bearings, single overhead cam, direct acting, flat followers. In
    /// kilopascals: crankshaft 3.26 + 9.66 + 7.95 (eq. 7.b), reciprocating 37.46 + 6.96 +
    /// 4.64 (8.b), gas loading 14.98 (9.b, K doubled), valvetrain 2.40 + 4.12 + 5.81 + 0.35
    /// + 3.30 (10.b, Table 4.2), auxiliary 24.25 (11).
    /// </summary>
    [Fact]
    public void EachComponentIsTheThesisEquation()
    {
        BaselinePaths.Require();

        var geometry = FrictionGeometry.FromEngine(Loader().Load(CaseFile("VW8V_A4LowCost/A4LowCost.eng")).Engine);

        Assert.Equal(ValvetrainMechanism.SohcDirectActing, geometry.Valvetrain);
        Assert.Equal(CamFollower.Flat, geometry.Follower);
        Assert.Equal(8, geometry.ValveCount);
        Assert.Equal(10.6, geometry.ValveLift, 1e-9);
        Assert.Equal(5, geometry.MainBearingCount);
        Assert.Equal(5, geometry.CamshaftBearingCount);

        var friction = FrictionModel.Calculate(geometry, 4000, 1);

        Assert.Equal(20.868, friction.Crankshaft / 1000, 0.005);
        Assert.Equal(49.061, friction.Reciprocating / 1000, 0.005);
        Assert.Equal(14.980, friction.ReciprocatingGasLoading / 1000, 0.005);
        Assert.Equal(15.982, friction.Valvetrain / 1000, 0.005);
        Assert.Equal(24.246, friction.Auxiliary / 1000, 0.005);
        Assert.Equal(125.137, friction.Total / 1000, 0.02);
    }

    /// <summary>
    /// Barnes-Moss is a motored total, pumping included, so a model of mechanical and
    /// auxiliary friction alone must sit below it at every speed, on every engine, and rise
    /// with speed as rubbing friction does.
    /// </summary>
    [Theory]
    [InlineData("A2China_Baseline/A2China.eng")]
    [InlineData("Nissan_290/Nissan6.eng")]
    [InlineData("VW8V_A4LowCost/A4LowCost.eng")]
    public void MechanicalFrictionIsBelowTheTotalCurveAndRisesWithSpeed(string file)
    {
        BaselinePaths.Require();

        var geometry = FrictionGeometry.FromEngine(Loader().Load(CaseFile(file)).Engine);
        var previous = 0.0;

        for (var rpm = 1000; rpm <= 7000; rpm += 500)
        {
            var total = FrictionModel.Calculate(geometry, rpm, 1).Total;

            Assert.InRange(total, previous, PerformanceCalculator.TotalFmep(rpm));
            previous = total;
        }
    }

    /// <summary>
    /// Under B88 brake work is the cycle's p dV, which carries its own pumping, less the
    /// mechanical friction alone: pumping is counted once.
    /// </summary>
    [Fact]
    public void BrakeWorkIsTheCyclesPdVLessMechanicalFriction()
    {
        BaselinePaths.Require();

        var engine = Loader().Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;

        var result = new SimulationRunner(new CachingExpressionEvaluator()).Run(
            engine,
            new SimulationSettings
            {
                CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1,
                Physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected },
            },
            cancellation: TestContext.Current.CancellationToken);

        var e = result.Engine;
        var friction = FrictionModel.Calculate(
            FrictionGeometry.FromEngine(e), e.Rpm, e.Plenum.PGas / e.Atmosphere.PGas).Total;

        Assert.Equal(friction, e.Fmep);
        Assert.Equal(e.Imep - e.Pmep - friction, e.Bmep, 1e-6);
        Assert.True(e.Fmep < PerformanceCalculator.TotalFmep(e.Rpm));
    }

    [Fact]
    public void WithoutB88TheFrictionIsTheCurve()
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.MechanicalFriction.Entry] = false;

        var engine = new Engine { Rpm = 4000, SweptVolume = 4e-4, Work = 600, PumpingWork = -20 };
        new PerformanceCalculator().Calculate(engine, physics);

        Assert.Equal(PerformanceCalculator.TotalFmep(4000), engine.Fmep);
    }

    /// <summary>No shipped file has <c>[Friction]</c> keys, so every input is left to its default.</summary>
    [Fact]
    public void AFileWithoutFrictionKeysLeavesEveryInputToItsDefault()
    {
        BaselinePaths.Require();

        var inputs = Loader().Load(BaselinePaths.File("A2China.eng")).Engine.Friction;

        Assert.Null(inputs.MainBearingDiameter);
        Assert.Null(inputs.MainBearingCount);
        Assert.Null(inputs.CamshaftBearingCount);
        Assert.Null(inputs.Valvetrain);
        Assert.Null(inputs.Follower);
        Assert.Null(inputs.OilViscosityRatio);
        Assert.Null(inputs.RingTensionRatio);
    }

    /// <summary>
    /// The optional keys are read, override the defaults, and survive a read and write of
    /// the file byte for byte.
    /// </summary>
    [Fact]
    public void FrictionKeysAreReadAndRoundTrip()
    {
        BaselinePaths.Require();

        var source = Path.GetDirectoryName(CaseFile("VW8V_A4LowCost/A4LowCost.eng"))!;
        var folder = Directory.CreateTempSubdirectory("esa-friction-").FullName;

        try
        {
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            var path = Path.Combine(folder, "A4LowCost.eng");
            File.AppendAllText(
                path,
                "\r\n[Friction]\r\nMainBearingD=54\r\nMainBearingL=19.5\r\nMainBearings=5\r\nRodBearingD=47.8\r\n"
                + "RodBearingL=17\r\nCamBearings=5\r\nValvetrain=SohcRockerArm\r\nFollower=Roller\r\n"
                + "OilViscosityRatio=1.2\r\nRingTensionRatio=0.8\r\nRingRoughness=0.9\r\n");

            var inputs = Loader().Load(path).Engine.Friction;

            Assert.Equal(54, inputs.MainBearingDiameter);
            Assert.Equal(19.5, inputs.MainBearingLength);
            Assert.Equal(5, inputs.MainBearingCount);
            Assert.Equal(47.8, inputs.RodBearingDiameter);
            Assert.Equal(17, inputs.RodBearingLength);
            Assert.Equal(5, inputs.CamshaftBearingCount);
            Assert.Equal(ValvetrainMechanism.SohcRockerArm, inputs.Valvetrain);
            Assert.Equal(CamFollower.Roller, inputs.Follower);
            Assert.Equal(1.2, inputs.OilViscosityRatio);
            Assert.Equal(0.8, inputs.RingTensionRatio);
            Assert.Equal(0.9, inputs.RingRoughnessFactor);

            var store = new EngineDefinitionStore();
            var copy = Path.Combine(folder, "Copy.eng");
            store.Write(copy, store.Read(path));

            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(copy));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
