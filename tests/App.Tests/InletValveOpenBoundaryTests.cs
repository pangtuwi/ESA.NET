using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Flow through an open inlet valve, in both directions.
/// </summary>
public sealed class InletValveOpenBoundaryTests
{
    private const double PlenumPressure = 99000;
    private const double PlenumTemperature = 298.15;

    // IVRFn, IVFFn and IVFRFn from A2China.eng at 4000 rpm: all three are constants.
    private static readonly (double Forward, double ForwardReverse, double Reverse) Tuning =
        (0.645, -0.044, -0.24);

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static double MainProgAngle(double traceAngle) => traceAngle + 360;

    private static (PipeGeometry Pipe, ValveMotion Valve) Inlet()
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
                ValveMotion.Inlet(engine.Manifold.InletValve));
    }

    private static (PipeGrid Current, PipeGrid Next) Grids(PipeGeometry pipe)
    {
        var current = new PipeGrid(EsaLimits.InletGridPoints);
        var next = new PipeGrid(EsaLimits.InletGridPoints);

        PipeGridInitialiser.Initialise(
            current, 39, pipe.Length, PlenumPressure, PlenumTemperature,
            CharacteristicSolver.InletGamma);
        PipeGridInitialiser.Initialise(
            next, 39, pipe.Length, PlenumPressure, PlenumTemperature,
            CharacteristicSolver.InletGamma);

        return (current, next);
    }

    /// <summary>Mid-intake, where the valve is well off its seat.</summary>
    private const double IntakeAngle = -250;

    [Fact]
    public void ALowCylinderPressureDrawsAirInThroughTheValve()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var (current, next) = Grids(pipe);

        var throat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7);

        var result = InletValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 60000, cylinderTemperature: 350,
            crankAngle: MainProgAngle(IntakeAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length),
            valveFlowArea: valve.FlowArea(IntakeAngle),
            throat, Tuning);

        // Forward flow: positive velocity at the pipe end and at the throat, and the pipe
        // end drops below plenum pressure as the gas accelerates.
        Assert.True(next.Velocity[38] > 0, $"Pipe-end velocity came out {next.Velocity[38]:F2} m/s.");
        Assert.True(result.Velocity > 0, $"Throat velocity came out {result.Velocity:F2} m/s.");
        Assert.True(next.Pressure[38] < PlenumPressure);
    }

    [Fact]
    public void TheThroatStateStaysSelfConsistentOnTheForwardBranch()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var (current, next) = Grids(pipe);

        var result = InletValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 60000, cylinderTemperature: 350,
            crankAngle: MainProgAngle(IntakeAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length),
            valveFlowArea: valve.FlowArea(IntakeAngle),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7), Tuning);

        // MassFlow multiplies these together, so they have to agree: rho = P/(R*T) and
        // c^2 = gamma*R*T give rho = gamma*P/c^2.
        var implied = CharacteristicSolver.InletGamma * result.Pressure
                      / (result.SpeedOfSound * result.SpeedOfSound);

        Assert.Equal(implied, result.Density, Math.Abs(implied) * 1e-6);

        // Subsonic through the valve, so the throat Mach number is below one.
        Assert.InRange(result.MachNumber, 0, 1);
        Assert.InRange(result.DischargeCoefficient, 0, 1);
    }

    [Fact]
    public void AHighCylinderPressureHandsOverToTheReverseRoutine()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var (current, next) = Grids(pipe);

        // Cylinder above the pipe: flow reverses and the open routine delegates.
        var result = InletValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 150000, cylinderTemperature: 700,
            crankAngle: MainProgAngle(IntakeAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length),
            valveFlowArea: valve.FlowArea(IntakeAngle),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7), Tuning);

        // Reverse flow is negative at both the throat and the pipe end.
        Assert.True(result.Velocity <= 0, $"Throat velocity came out {result.Velocity:F2} m/s.");
        Assert.True(next.Velocity[38] < 0, $"Pipe-end velocity came out {next.Velocity[38]:F2} m/s.");
    }

    [Fact]
    public void TheReverseHandOverLeavesTheCurrentArraysMutated()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var (current, next) = Grids(pipe);

        var before = current.Pressure[38];

        InletValveOpenBoundary.Apply(
            current, next, pipe, valve, TimeStep(),
            cylinderPressure: 150000, cylinderTemperature: 700,
            crankAngle: MainProgAngle(IntakeAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length),
            valveFlowArea: valve.FlowArea(IntakeAngle),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7), Tuning);

        // The reverse routine works on the current-time-level arrays, so a step that
        // reverses leaves them half-updated - the valve point moved, everything else
        // untouched. That is the original's staging, not an accident of this port.
        // See ISSUES.md B58.
        Assert.NotEqual(before, current.Pressure[38]);
        Assert.Equal(PlenumPressure, current.Pressure[37]);
    }
}
