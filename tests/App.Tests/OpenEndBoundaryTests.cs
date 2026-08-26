using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The open ends of the two pipes: the inlet plenum and the exhaust back pressure.
/// </summary>
public sealed class OpenEndBoundaryTests
{
    private const double PlenumPressure = 99000;
    private const double PlenumTemperature = 298.15;
    private const double BackPressure = 117800;
    private const double BackTemperature = 973.15;

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
    public void APipeAtRestAtPlenumConditionsIsHeldThere()
    {
        BaselinePaths.Require();

        var (inlet, _) = Pipes();
        var (current, next) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, PlenumPressure, PlenumTemperature,
            CharacteristicSolver.InletGamma);

        OpenEndBoundary.ApplyInlet(current, next, inlet, TimeStep(), PlenumPressure, PlenumTemperature);

        // Nothing is driving the gas and the pipe already sits at plenum conditions, so
        // the boundary has nothing to do. This exercises the isentropic branch: at zero
        // velocity the stagnation state and the static state coincide.
        Assert.Equal(0, next.Velocity[0], 9);
        Assert.Equal(PlenumPressure, next.Pressure[0], 6);
        Assert.Equal(current.Density[0], next.Density[0], 9);
    }

    [Fact]
    public void APipeAtRestAtBackPressureIsHeldThere()
    {
        BaselinePaths.Require();

        var (_, exhaust) = Pipes();
        var (current, next) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);

        OpenEndBoundary.ApplyExhaust(current, next, exhaust, TimeStep(), BackPressure, BackTemperature);

        // The exhaust boundary is the pipe's last point, and at rest it takes the
        // imposed-pressure branch rather than the isentropic one.
        Assert.Equal(0, next.Velocity[15], 9);
        Assert.Equal(BackPressure, next.Pressure[15], 6);
        Assert.Equal(current.Density[15], next.Density[15], 9);
    }

    [Fact]
    public void LowPressureInThePipeDrawsGasInFromThePlenum()
    {
        BaselinePaths.Require();

        var (inlet, _) = Pipes();
        var (current, next) = Grids(
            EsaLimits.InletGridPoints, 39, inlet.Length, PlenumPressure, PlenumTemperature,
            CharacteristicSolver.InletGamma);

        // Drop the pressure inside the pipe, as the intake stroke does.
        for (var i = 0; i < 39; i++)
        {
            current.Pressure[i] = 90000;
            current.Density[i] = 90000.0 / 287 / PlenumTemperature;
        }

        OpenEndBoundary.ApplyInlet(current, next, inlet, TimeStep(), PlenumPressure, PlenumTemperature);

        // Gas accelerates into the pipe, and the isentropic branch means the boundary
        // pressure sits below the plenum's stagnation pressure by the dynamic head.
        Assert.True(next.Velocity[0] > 0, $"Velocity came out {next.Velocity[0]:F2} m/s.");
        Assert.True(
            next.Pressure[0] < PlenumPressure,
            $"Boundary pressure {next.Pressure[0]:F0} Pa should be below the plenum's {PlenumPressure}.");
    }

    [Fact]
    public void GasLeavingTheTailpipeTakesTheBackPressureExactly()
    {
        BaselinePaths.Require();

        var (_, exhaust) = Pipes();
        var (current, next) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);

        for (var i = 0; i < 16; i++)
        {
            current.Velocity[i] = 60;
        }

        OpenEndBoundary.ApplyExhaust(current, next, exhaust, TimeStep(), BackPressure, BackTemperature);

        // Outflow is the imposed-pressure branch: the reservoir's static pressure is taken
        // directly, with no stagnation correction at all.
        Assert.Equal(BackPressure, next.Pressure[15], 6);
        Assert.True(next.Velocity[15] > 0, $"Velocity came out {next.Velocity[15]:F2} m/s.");
    }

    [Fact]
    public void ReverseFlowAtTheTailpipeSwitchesToTheIsentropicBranch()
    {
        BaselinePaths.Require();

        var (_, exhaust) = Pipes();
        var (current, next) = Grids(
            EsaLimits.ExhaustGridPoints, 16, exhaust.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);

        // Gas being drawn back in through the tailpipe.
        for (var i = 0; i < 16; i++)
        {
            current.Velocity[i] = -60;
        }

        OpenEndBoundary.ApplyExhaust(current, next, exhaust, TimeStep(), BackPressure, BackTemperature);

        // Now the reservoir is a stagnation state, so the boundary pressure falls below it
        // rather than sitting on it. That is the whole difference between the branches.
        Assert.True(
            next.Pressure[15] < BackPressure,
            $"Boundary pressure {next.Pressure[15]:F0} Pa should be below the back pressure "
            + $"of {BackPressure} on the isentropic branch.");
    }
}
