using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B78: the burn's burnt volume, and the energy the burn conserves or creates.
/// </summary>
public sealed class CombustionEnergyTests
{
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
        engine.Rpm = 4000;

        return engine;
    }

    /// <summary>What one burn did, measured from the gas between steps.</summary>
    /// <param name="Residual">
    /// The first law over the burn, <c>dE + W - Q</c>, as a fraction of the fuel energy.
    /// </param>
    /// <param name="BurntVolumeAtSpark">The integrator's burnt volume on the first burn step.</param>
    /// <param name="CylinderVolumeAtSpark">The cylinder volume on the same step.</param>
    /// <param name="LeastUnburntVolume">The smallest unburnt volume while xb is under 0.9.</param>
    private sealed record Burn(
        double Residual, double BurntVolumeAtSpark, double CylinderVolumeAtSpark, double LeastUnburntVolume);

    /// <summary>
    /// Runs the given number of cycles and measures the burn of the last one.
    /// </summary>
    /// <remarks>
    /// B46 closes the cylinder, so the burn's mass is fixed, and B77 pairs the gas's
    /// volume with its pressure, so the energy and work measured from the gas between steps
    /// belong to the same instant. The total internal energy is the two zones' absolute
    /// energies, chemical energy included, so combustion moves energy between the zones and
    /// creates none.
    /// </remarks>
    private static Burn MeasureBurn(bool corrected, int measuredCycle)
    {
        var engine = BaselineEngine();
        var physics = new PhysicsCorrections();
        physics.Overrides[CorrectionCatalogue.BurntVolumeReset.Entry] = corrected;
        physics.Overrides[CorrectionCatalogue.ClosedCylinderMassFlow.Entry] = true;
        physics.Overrides[CorrectionCatalogue.EndOfStepState.Entry] = true;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var cycle = 0;
        double? startEnergy = null;
        double endEnergy = 0, work = 0, heat = 0, lastPressure = 0, lastVolume = 0;
        double burntVolumeAtSpark = double.NaN, cylinderVolumeAtSpark = double.NaN;
        var leastUnburnt = double.MaxValue;

        solver.StepCompleted += s =>
        {
            if (cycle != measuredCycle || s.Engine.State != EngineState.Combustion)
            {
                return;
            }

            var gas = s.Engine.Cylinder;
            var energy = (gas.Mb * gas.Ub) + (gas.Mu * gas.Uu);

            if (startEnergy is null)
            {
                startEnergy = energy;
                burntVolumeAtSpark = s.Engine.Integration.Y[0];
                cylinderVolumeAtSpark = gas.VGas;
            }
            else
            {
                work += 0.5 * (gas.PGas + lastPressure) * (gas.VGas - lastVolume);
                heat += s.Engine.Qb + s.Engine.Qu;
            }

            if (gas.Xb < 0.9)
            {
                leastUnburnt = Math.Min(leastUnburnt, gas.Vu);
            }

            endEnergy = energy;
            lastPressure = gas.PGas;
            lastVolume = gas.VGas;
        };

        engine.ZoneCount = 1;

        for (cycle = 1; cycle <= measuredCycle; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            solver.RunOneCycle();
        }

        Assert.NotNull(startEnergy);

        var fuelEnergy = engine.Cylinder.Fuel.M * engine.Cylinder.Fuel.Q;

        // Heat is accumulated as a loss, negative; work is done by the gas.
        var residual = (endEnergy - startEnergy.Value + work - heat) / fuelEnergy;

        return new Burn(residual, burntVolumeAtSpark, cylinderVolumeAtSpark, leastUnburnt);
    }

    /// <summary>
    /// B78's oracle. With both valves shut the cylinder is a closed system, so through the
    /// burn its total internal energy - chemical included - can change only by the work
    /// it does and the heat it loses. The original starts every burn after the first with
    /// the last burn's end-of-burn volume, larger than the cylinder, so the unburnt zone
    /// burns in no volume and the burn creates energy.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AConvergedBurnConservesEnergy(bool corrected)
    {
        BaselinePaths.Require();

        // The third cycle is the second two-zone one: the first burn the defect reaches.
        var burn = MeasureBurn(corrected, measuredCycle: 3);

        if (corrected)
        {
            // Measured at 0.93 per cent of the fuel energy: the integrator's own error over a
            // burn of fifty-odd one-degree steps.
            Assert.InRange(Math.Abs(burn.Residual), 0, 0.015);
        }
        else
        {
            // Measured at +16.2 per cent: energy created, not lost.
            Assert.True(burn.Residual > 0.10, $"{burn.Residual}");
        }
    }

    /// <summary>
    /// The mechanism. The first two-zone burn starts from no burnt volume, because
    /// InitVars zeroed it; under B78 every burn does. In the original every later burn
    /// starts larger than the cylinder, the burnt-volume clamp (ISSUES.md B10) holds the
    /// burnt zone to the whole cylinder, and the unburnt zone has no volume for the whole
    /// burn.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryBurnStartsLikeTheFirst(bool corrected)
    {
        BaselinePaths.Require();

        var first = MeasureBurn(corrected, measuredCycle: 2);
        var later = MeasureBurn(corrected, measuredCycle: 3);

        // The first two-zone burn is the same either way, and splits the volume properly.
        Assert.True(first.BurntVolumeAtSpark < 0.05 * first.CylinderVolumeAtSpark, $"{first.BurntVolumeAtSpark}");
        Assert.True(first.LeastUnburntVolume > 0, $"{first.LeastUnburntVolume}");

        if (corrected)
        {
            Assert.True(later.BurntVolumeAtSpark < 0.05 * later.CylinderVolumeAtSpark, $"{later.BurntVolumeAtSpark}");
            Assert.True(later.LeastUnburntVolume > 0, $"{later.LeastUnburntVolume}");
        }
        else
        {
            // 91 cm3 against a 64 cm3 cylinder on the first burn step.
            Assert.True(later.BurntVolumeAtSpark > later.CylinderVolumeAtSpark, $"{later.BurntVolumeAtSpark}");
            Assert.Equal(0, later.LeastUnburntVolume);
        }
    }

}
