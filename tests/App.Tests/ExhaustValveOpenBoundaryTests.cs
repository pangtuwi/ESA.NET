using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Blowdown and outward flow through an open exhaust valve.
/// </summary>
public sealed class ExhaustValveOpenBoundaryTests
{
    private const double BackPressure = 117800;
    private const double BackTemperature = 973.15;

    // EVFFn, EVFRFn and EVRFn from A2China.eng, all constants.
    private static readonly (double Forward, double ForwardReverse, double Reverse) Tuning =
        (0.41, -0.715, -0.6);

    private const double ExhaustAngle = 200;

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static double MainProgAngle(double traceAngle) => traceAngle + 360;

    private static (PipeGeometry Pipe, ValveMotion Valve) Exhaust()
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

        return (new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength),
                ValveMotion.Exhaust(engine.Manifold.ExhaustValve));
    }

    private static (PipeGrid Current, PipeGrid Next) Grids(PipeGeometry pipe)
    {
        var current = new PipeGrid(EsaLimits.ExhaustGridPoints);
        var next = new PipeGrid(EsaLimits.ExhaustGridPoints);

        PipeGridInitialiser.Initialise(
            current, 16, pipe.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);
        PipeGridInitialiser.Initialise(
            next, 16, pipe.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);

        return (current, next);
    }

    private static InletValveReverseBoundary.ThroatState StartingThroat(double pressure) =>
        new(0, 0, Math.Sqrt(CharacteristicSolver.ExhaustGamma * 287 * BackTemperature),
            0, pressure, 0.7);

    [Fact]
    public void BlowdownDrivesGasOutIntoTheExhaustPipe()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var (current, next) = Grids(pipe);

        // Cylinder far above the pipe at exhaust valve opening: choked blowdown.
        var result = ExhaustValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 600000, cylinderTemperature: 1400,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(BackPressure), Tuning);

        // Outward flow is positive at both throat and pipe end - no negation here, unlike
        // the two reverse routines.
        Assert.True(result.Velocity > 0, $"Throat velocity came out {result.Velocity:F1} m/s.");
        Assert.True(next.Velocity[0] > 0, $"Pipe-end velocity came out {next.Velocity[0]:F1} m/s.");

        // Choked at a pressure ratio of 600/117.8, well above the critical 1.83.
        Assert.Equal(1, result.MachNumber);
        Assert.True(next.Pressure[0] > BackPressure);
    }

    [Fact]
    public void TheThroatStateStaysSelfConsistentOnBlowdown()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var (current, next) = Grids(pipe);

        var result = ExhaustValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 600000, cylinderTemperature: 1400,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(BackPressure), Tuning);

        // The choked branch sets density from P/(R*T) rather than gamma*P/c^2; the two
        // agree, which is the point.
        var implied = CharacteristicSolver.ExhaustGamma * result.Pressure
                      / (result.SpeedOfSound * result.SpeedOfSound);

        Assert.Equal(implied, result.Density, Math.Abs(implied) * 1e-6);
        Assert.InRange(result.DischargeCoefficient, 0, 1);
    }

    [Fact]
    public void ASubsonicPressureRatioStaysBelowMachOneAtTheThroat()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var (current, next) = Grids(pipe);

        // Late in the exhaust stroke: the cylinder is only a little above the pipe, so
        // the ratio sits under the critical 1.83 and the flow is subsonic.
        var result = ExhaustValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 150000, cylinderTemperature: 1000,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(BackPressure), Tuning);

        Assert.InRange(result.MachNumber, 0, 1);
        Assert.True(result.Velocity > 0);
    }

    [Fact]
    public void AThroatAtOrAboveCylinderPressureHandsOverToTheReverseRoutine()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var (current, next) = Grids(pipe);

        var before = current.Pressure[0];

        // Throat already above the cylinder and the pipe end at rest, so the guard that
        // would push Pt below Pcyl does not fire (it needs u > EVF) and the reverse
        // hand-over goes ahead.
        var result = ExhaustValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 90000, cylinderTemperature: 900,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(BackPressure), Tuning);

        // Reverse convention at the throat, and the current arrays are left mutated by
        // the delegate, as at the inlet. See ISSUES.md B58.
        Assert.True(result.Velocity <= 0, $"Throat velocity came out {result.Velocity:F2} m/s.");
        Assert.NotEqual(before, current.Pressure[0]);
        Assert.Equal(BackPressure, current.Pressure[1]);
    }
}
