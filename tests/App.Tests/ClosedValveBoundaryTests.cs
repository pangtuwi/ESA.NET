using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The solid-wall condition at a shut valve.
/// </summary>
public sealed class ClosedValveBoundaryTests
{
    private static double TimeStep(double rpm = 4000, double crankAngleStep = 1) =>
        1 / (rpm / 60 * 360) * crankAngleStep;

    private static (PipeGeometry Inlet, PipeGeometry Exhaust) Pipes()
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

        return (new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength),
                new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength));
    }

    private static (PipeGrid Current, PipeGrid Next) Grids(
        int capacity, int points, double length, double pressure, double temperature, double gamma)
    {
        var current = new PipeGrid(capacity);
        var next = new PipeGrid(capacity);

        PipeGridInitialiser.Initialise(current, points, length, pressure, temperature, gamma);
        PipeGridInitialiser.Initialise(next, points, length, pressure, temperature, gamma);

        return (current, next);
    }

    [Fact]
    public void AStagnantInletWallHoldsItsState()
    {
        BaselinePaths.Require();

        var (inlet, _) = Pipes();
        var (current, next) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, 99000, 298.15, CharacteristicSolver.InletGamma);

        ClosedValveBoundary.ApplyInlet(current, next, inlet, TimeStep());

        // Gas at rest against a stationary wall: nothing should change.
        Assert.Equal(0, next.Velocity[38], 12);
        Assert.Equal(99000, next.Pressure[38], 6);
        Assert.Equal(current.Density[38], next.Density[38], 12);
    }

    [Fact]
    public void AStagnantExhaustWallHoldsItsState()
    {
        BaselinePaths.Require();

        var (_, exhaust) = Pipes();
        var (current, next) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, 117800, 973.15,
            CharacteristicSolver.ExhaustGamma);

        ClosedValveBoundary.ApplyExhaust(current, next, exhaust, TimeStep());

        // The exhaust wall is the pipe's first point, not its last.
        Assert.Equal(0, next.Velocity[0], 12);
        Assert.Equal(117800, next.Pressure[0], 6);
        Assert.Equal(current.Density[0], next.Density[0], 12);
    }

    [Fact]
    public void TheWallVelocityIsImposedRatherThanSolved()
    {
        BaselinePaths.Require();

        var (inlet, _) = Pipes();
        var (current, next) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, 99000, 298.15, CharacteristicSolver.InletGamma);

        // Drive gas at the wall and it still comes back at the imposed velocity: only one
        // characteristic reaches a wall, so velocity cannot be solved for there.
        for (var i = 0; i < 39; i++)
        {
            current.Velocity[i] = 25;
        }

        ClosedValveBoundary.ApplyInlet(current, next, inlet, TimeStep());

        Assert.Equal(0, next.Velocity[38], 12);
    }

    [Fact]
    public void GasArrivingAtAShutInletValveRaisesThePressureThere()
    {
        BaselinePaths.Require();

        var (inlet, _) = Pipes();
        var (current, next) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, 99000, 298.15, CharacteristicSolver.InletGamma);

        for (var i = 0; i < 39; i++)
        {
            current.Velocity[i] = 25;
        }

        ClosedValveBoundary.ApplyInlet(current, next, inlet, TimeStep());

        // Flow into a closed end has to stagnate, and the pressure rise is of the order
        // rho*c*u for an acoustic wave: about 1.2 * 346 * 25, a few tens of kPa.
        var rise = next.Pressure[38] - 99000;
        Assert.True(rise > 0, $"Pressure at the shut valve fell by {-rise:F0} Pa.");
        Assert.InRange(rise, 5000, 30000);
    }

    [Fact]
    public void TheTwoRoutinesBuildTheirInterpolantsFromDifferentVelocities()
    {
        BaselinePaths.Require();

        var (inlet, exhaust) = Pipes();

        // Leave a velocity on the wall point itself, as the step after a valve shuts
        // would. The inlet routine reads it; the exhaust routine substitutes the imposed
        // wall velocity instead. That asymmetry is in the original. See ISSUES.md B54.
        var (inletCurrent, inletNext) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, 99000, 298.15, CharacteristicSolver.InletGamma);
        inletCurrent.Velocity[38] = 40;

        var (plainCurrent, plainNext) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, 99000, 298.15, CharacteristicSolver.InletGamma);

        ClosedValveBoundary.ApplyInlet(inletCurrent, inletNext, inlet, TimeStep());
        ClosedValveBoundary.ApplyInlet(plainCurrent, plainNext, inlet, TimeStep());

        // The inlet routine used the stale wall velocity, so the two disagree.
        Assert.NotEqual(plainNext.Pressure[38], inletNext.Pressure[38]);

        // The exhaust routine ignores it, so the same experiment there changes nothing.
        var (a, aNext) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, 117800, 973.15,
            CharacteristicSolver.ExhaustGamma);
        var (b, bNext) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, 117800, 973.15,
            CharacteristicSolver.ExhaustGamma);
        a.Velocity[0] = 40;

        ClosedValveBoundary.ApplyExhaust(a, aNext, exhaust, TimeStep());
        ClosedValveBoundary.ApplyExhaust(b, bNext, exhaust, TimeStep());

        Assert.Equal(bNext.Pressure[0], aNext.Pressure[0], 9);
    }
}
