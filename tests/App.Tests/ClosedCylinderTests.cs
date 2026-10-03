using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B46: with both valves shut, nothing crosses the cylinder boundary, so the
/// equations must carry no flow term and the first law must close on work and heat alone.
/// The original breaks both through expansion; the B46 correction restores them.
/// </summary>
public sealed class ClosedCylinderTests
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

    /// <summary>What the third cycle's closed period looked like.</summary>
    /// <param name="LargestFlowTerm">The largest mass-flow derivative seen, either valve.</param>
    /// <param name="EnergyResidual">
    /// The first law over expansion, <c>Mb dUb - (W + Q)</c>, as a fraction of the work.
    /// </param>
    private sealed record ClosedPeriod(double LargestFlowTerm, double EnergyResidual);

    private static ClosedPeriod Measure(PhysicsCorrections physics)
    {
        var engine = BaselineEngine();
        var solver = new CycleSolver(engine, new ManifoldSolver(engine), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        const int measuredCycle = 3;
        var cycle = 0;
        var largestFlowTerm = 0.0;
        double? startEnergy = null;
        double endEnergy = 0, work = 0, heat = 0, lastPressure = 0, lastVolume = 0;

        solver.StepCompleted += s =>
        {
            var state = s.Engine.State;

            if (cycle != measuredCycle
                || state is not (EngineState.Compression or EngineState.Combustion or EngineState.Expansion))
            {
                return;
            }

            var gas = s.Engine.Cylinder;
            largestFlowTerm = Math.Max(
                largestFlowTerm, Math.Max(Math.Abs(gas.DmInDTheta), Math.Abs(gas.DmOutDTheta)));

            if (state != EngineState.Expansion)
            {
                return;
            }

            // Expansion is one zone of fixed mass, so its internal energy is Mb Ub. Work
            // done on the gas is the trapezoid of -P dV between steps.
            var energy = gas.Mb * gas.Ub;

            if (startEnergy is null)
            {
                startEnergy = energy;
            }
            else
            {
                work -= 0.5 * (gas.PGas + lastPressure) * (gas.VGas - lastVolume);
                heat += s.Engine.Qb;
            }

            endEnergy = energy;
            lastPressure = gas.PGas;
            lastVolume = gas.VGas;
        };

        // One single-zone cycle to start, as the runner does, then two-zone.
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

        var residual = (endEnergy - startEnergy.Value - work - heat) / Math.Abs(work);

        return new ClosedPeriod(largestFlowTerm, residual);
    }

    [Fact]
    public void TheCorrectionLeavesNoFlowTermInAClosedCylinderAndTheFirstLawCloses()
    {
        BaselinePaths.Require();

        var corrected = new PhysicsCorrections();
        corrected.Overrides[CorrectionCatalogue.ClosedCylinderMassFlow.Entry] = true;

        var closed = Measure(corrected);

        Assert.Equal(0, closed.LargestFlowTerm);

        // Measured at 1.1 per cent, the same with B14 on or off, so it is not the
        // integrator. Legacy's is twelve times that.
        Assert.InRange(Math.Abs(closed.EnergyResidual), 0, 0.02);
    }

    /// <summary>
    /// Legacy's half of the oracle, kept so that Legacy stays the original: the last
    /// exhaust step's flow is still on the cylinder through expansion, and the first law
    /// misses by about an eighth of the work.
    /// </summary>
    [Fact]
    public void LegacyCarriesTheLastExhaustFlowThroughExpansion()
    {
        BaselinePaths.Require();

        var legacy = Measure(new PhysicsCorrections());

        // 6.3e-5 kg/rad on the baseline engine.
        Assert.InRange(legacy.LargestFlowTerm, 1e-5, 1e-3);
        Assert.InRange(Math.Abs(legacy.EnergyResidual), 0.08, 0.2);
    }
}
