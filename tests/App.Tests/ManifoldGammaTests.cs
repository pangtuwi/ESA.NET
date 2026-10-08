using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B50: the wave solver should run on the gas that is actually in each pipe -
/// the unburnt mixture in the inlet and hot products in the exhaust - rather than on the
/// original's hard-coded 1.3994 and 1.3.
/// </summary>
public sealed class ManifoldGammaTests
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

    private static (CycleSolver Solver, ManifoldSolver Manifold) OneStep(bool corrected)
    {
        var engine = BaselineEngine();
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.ManifoldGammas.Entry] = corrected;

        var manifold = new ManifoldSolver(engine, physics: physics);
        var solver = new CycleSolver(engine, manifold, physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        // The manifold settles its gammas, and lays out its grids, on the first step. A
        // cycle starts at inlet valve closing, as RunOneCycle starts it.
        engine.ZoneCount = 1;
        engine.CrankAngle = solver.States.InletClose;
        solver.Step();

        return (solver, manifold);
    }

    /// <summary>
    /// The pipes' gammas are the property models' own for the gas in each pipe, and every
    /// point of each pipe carries the speed of sound that gamma gives: <c>c^2 rho / p</c>
    /// comes back as the gamma, at every point the solver has written.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachPipeRunsOnItsOwnGassGamma(bool corrected)
    {
        BaselinePaths.Require();

        var (solver, manifold) = OneStep(corrected);
        var engine = solver.Engine;
        var gas = solver.Cylinder.Cylinder;

        if (corrected)
        {
            // The fuel-air mixture at plenum conditions, and burnt products at the back
            // pressure and the exhaust temperature in kelvin.
            Assert.Equal(
                gas.Unburnt.Gamma(engine.Plenum.PGas, engine.Manifold.PlenumTemperature),
                manifold.InletGamma, 6);
            Assert.Equal(
                gas.Burnt.Gamma(engine.Exhaust.PGas, engine.Exhaust.Tb + 273.15),
                manifold.ExhaustGamma, 6);

            // Physically: a rich-ish fuel-air mixture sits below air's 1.40, and products
            // at about 1100 K near 1.29 - not the 1.37 the original's formula gives them by
            // asking at 293 K.
            Assert.InRange(manifold.InletGamma, 1.33, 1.39);
            Assert.InRange(manifold.ExhaustGamma, 1.27, 1.31);
        }
        else
        {
            Assert.Equal(CharacteristicSolver.InletGamma, manifold.InletGamma);
            Assert.Equal(CharacteristicSolver.ExhaustGamma, manifold.ExhaustGamma);
        }

        foreach (var (grid, gamma) in new[]
                 {
                     (engine.Manifold.Inlet, manifold.InletGamma),
                     (engine.Manifold.Exhaust, manifold.ExhaustGamma),
                 })
        {
            for (var i = 0; i < grid.ActiveCount; i++)
            {
                var implied = grid.SpeedOfSound[i] * grid.SpeedOfSound[i] * grid.Density[i] / grid.Pressure[i];
                Assert.Equal(gamma, implied, 9);
            }
        }
    }

    /// <summary>
    /// The wave-solver invariant CORRECTIONS.md names for B50: a uniform stagnant pipe
    /// stays put whatever its gamma, here the exhaust's computed one over fifty steps.
    /// </summary>
    [Fact]
    public void AStagnantExhaustPipeStaysPutAtTheComputedGamma()
    {
        BaselinePaths.Require();

        var (solver, manifold) = OneStep(corrected: true);
        var engine = solver.Engine;
        var pipe = new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength);
        var gamma = manifold.ExhaustGamma;
        var pressure = engine.Exhaust.PGas;
        var temperature = engine.Exhaust.Tb + 273.15;
        var dt = 1 / (engine.Rpm / 60 * 360);

        var current = new PipeGrid(EsaLimits.ExhaustGridPoints);
        var next = new PipeGrid(EsaLimits.ExhaustGridPoints);
        PipeGridInitialiser.Initialise(current, 30, pipe.Length, pressure, temperature, gamma);
        PipeGridInitialiser.Initialise(next, 30, pipe.Length, pressure, temperature, gamma);

        var density = current.Density[15];

        for (var step = 0; step < 50; step++)
        {
            for (var i = 1; i <= 28; i++)
            {
                CharacteristicSolver.UpdateInteriorPoint(current, next, pipe, gamma, dt, i);
            }

            for (var i = 1; i <= 28; i++)
            {
                current.Velocity[i] = next.Velocity[i];
                current.Pressure[i] = next.Pressure[i];
                current.Density[i] = next.Density[i];
                current.SpeedOfSound[i] = next.SpeedOfSound[i];
            }
        }

        for (var i = 1; i <= 28; i++)
        {
            Assert.Equal(0, current.Velocity[i], 9);
            Assert.Equal(pressure, current.Pressure[i], 5);
            Assert.Equal(density, current.Density[i], 9);
        }
    }
}
