using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md A37: a multi-run row sets the spark and burn angle after initialisation, and
/// the original's <c>GetState</c> reads both from the cylinder on every step, so the
/// combustion window moves with them. The port's state map once kept the copies it was
/// built with.
/// </summary>
public sealed class SparkOverrideStateTests
{
    private static Engine BaselineEngine() =>
        new EngineLoader(
                new EngineDefinitionStore(),
                new CamProfileReader(),
                new SpeedKeyedTableReader(),
                new WallTemperatureTableReader(),
                new ExhaustBackPressureTableReader(),
                new ManifoldAreaTableStore(),
                new DischargeCoefficientTableStore())
            .Load(BaselinePaths.File("A2China.eng"))
            .Engine;

    [Fact]
    public void TheMapDecidesOnTheSparkAndBurnAngleItIsGiven()
    {
        var map = new CrankAngleStateMap(
            inletOpen: 341, inletClose: -100, exhaustOpen: 116, exhaustClose: -323, sparkAngle: -21, burnAngle: 55);

        Assert.Equal(EngineState.Compression, map.StateAt(-21, sparkAngle: -8, burnAngle: 45));
        Assert.Equal(EngineState.Combustion, map.StateAt(-8, sparkAngle: -8, burnAngle: 45));
        Assert.Equal(EngineState.Combustion, map.StateAt(36, sparkAngle: -8, burnAngle: 45));
        Assert.Equal(EngineState.Expansion, map.StateAt(37, sparkAngle: -8, burnAngle: 45));

        // The one-argument form is the map's own spark and burn angle, as before.
        Assert.Equal(EngineState.Combustion, map.StateAt(-21));
        Assert.Equal(EngineState.Expansion, map.StateAt(34));
    }

    /// <summary>
    /// The oracle: with the spark moved to 8 degrees before top dead centre and the burn to
    /// 45 after the solver is built, as <c>MultiRunner</c> moves them, combustion runs from
    /// -8 to 37, and the charge is all but burnt when expansion takes the rest.
    /// </summary>
    [Theory]
    [InlineData(PhysicsMode.Legacy)]
    [InlineData(PhysicsMode.Corrected)]
    public void ACombustionWindowFollowsASparkSetAfterInitialisation(PhysicsMode mode)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        engine.Rpm = 4000;
        var physics = new PhysicsCorrections { Mode = mode };

        // The window follows the spark itself here; B90 would start it later by the
        // default delay to the start of combustion.
        physics.Overrides[CorrectionCatalogue.FlameDevelopmentDelay.Entry] = false;
        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise());
        Assert.Equal(21, solver.SparkAdvance);

        engine.Cylinder.ThetaSpark = -8;
        engine.Cylinder.Fuel.BurnAngle = 45;

        var firstCombustion = double.NaN;
        var firstExpansion = double.NaN;
        var burntBeforeExpansion = double.NaN;
        var lastBurnt = 0.0;
        var previous = EngineState.Overlap;
        var cycle = 0;

        solver.StepCompleted += s =>
        {
            var e = s.Engine;

            if (cycle == 2 && e.State != previous)
            {
                if (e.State == EngineState.Combustion && double.IsNaN(firstCombustion))
                {
                    firstCombustion = e.CrankAngle;
                }

                if (e.State == EngineState.Expansion && double.IsNaN(firstExpansion))
                {
                    firstExpansion = e.CrankAngle;
                    burntBeforeExpansion = lastBurnt;
                }
            }

            previous = e.State;
            lastBurnt = e.Cylinder.Xb;
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

        Assert.Equal(-8, firstCombustion, 1.0);
        Assert.Equal(37, firstExpansion, 1.0);
        // The gas clamps the burnt fraction at 0.99 (TwoZoneGas); the old window ended at
        // 33 degrees, where the burn law stands at 0.98.
        Assert.InRange(burntBeforeExpansion, 0.99, 1);
    }
}
