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

        var corrected = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
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

        var legacy = Measure(new PhysicsCorrections { Mode = PhysicsMode.Legacy });

        // 6.3e-5 kg/rad on the baseline engine.
        Assert.InRange(legacy.LargestFlowTerm, 1e-5, 1e-3);
        Assert.InRange(Math.Abs(legacy.EnergyResidual), 0.08, 0.2);
    }

    /// <summary>
    /// B38's oracle. Woschni's motored pressure is what the cylinder would reach without
    /// combustion, compressed from its state at inlet valve closing, so through compression
    /// it has to track the actual pressure. That needs the reference to be the cylinder's
    /// own state at closing, taken each cycle.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheMotoredPressureTracksCompressionFromTheCylindersStateAtClosing(bool corrected)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.IvcReference.Entry] = corrected;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        const int measuredCycle = 3;
        var cycle = 0;
        double closingPressure = 0, closingTemperature = 0, closingVolume = 0;
        var worstTracking = 0.0;
        var referenceChecked = false;

        solver.StepCompleted += s =>
        {
            var gas = s.Engine.Cylinder;

            if (s.Engine.State == EngineState.Intake)
            {
                // The state the last intake step leaves is the state at closing.
                var burntFraction = gas.Mb == 0 ? 0 : gas.Mb / gas.MGas;
                closingPressure = gas.PGas;
                closingTemperature = (burntFraction * gas.Tb) + ((1 - burntFraction) * gas.Tu);
                closingVolume = gas.VGas;
                return;
            }

            if (cycle != measuredCycle || s.Engine.State != EngineState.Compression)
            {
                return;
            }

            var model = s.Cylinder;

            if (corrected && !referenceChecked)
            {
                Assert.Equal(closingPressure, model.PressureAtInletValveClosing);
                Assert.Equal(closingTemperature, model.TemperatureAtInletValveClosing);
                Assert.Equal(closingVolume, model.VolumeAtInletValveClosing);
                referenceChecked = true;
            }

            var x = s.Engine.Integration.X;
            var motored = model.PressureAtInletValveClosing
                          * Math.Pow(model.VolumeAtInletValveClosing / s.Geometry.Volume(x), 1.30);
            worstTracking = Math.Max(worstTracking, Math.Abs((gas.PGas / motored) - 1));
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

        if (corrected)
        {
            Assert.True(referenceChecked);

            // Measured at 4.5 per cent: the fixed polytropic index of 1.3 and the heat lost
            // through compression, not the reference.
            Assert.InRange(worstTracking, 0, 0.08);
        }
        else
        {
            // The plenum's 0.99 bar against about 2.1 bar in the cylinder at closing: the
            // motored pressure is less than half the real one. Measured at 123 per cent.
            Assert.True(worstTracking > 0.5, $"{worstTracking}");
        }
    }
}
