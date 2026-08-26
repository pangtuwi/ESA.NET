using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Reverse flow out of the cylinder through the inlet valve.
/// </summary>
public sealed class InletValveReverseBoundaryTests
{
    private const double PlenumPressure = 99000;
    private const double PlenumTemperature = 298.15;

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    /// <summary>
    /// Main_Prog passes crank angle as x*180/pi + 360, so the trace's -359..360 arrives
    /// here as 1..720. Getting this wrong reads the valve lift 360 degrees away.
    /// </summary>
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

    private static PipeGrid Grid(PipeGeometry pipe)
    {
        var grid = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(
            grid, 39, pipe.Length, PlenumPressure, PlenumTemperature, CharacteristicSolver.InletGamma);
        return grid;
    }

    [Fact]
    public void ACylinderAtOrBelowTheThroatPressureCannotReachTheStalledBranchOnTheFirstPass()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var grid = Grid(pipe);

        // The no-flow branch tests Pcyl <= Pt, and the line immediately above it assigns
        // Pt := 0.999999*Pcyl whenever that same condition holds. The assignment therefore
        // guarantees the test fails, and the routine drops into the subsonic branch with a
        // pressure ratio pinned just above 1. The throat velocity that follows is
        // vanishingly small, which puts the root outside the velocity solver's fixed
        // bracket, and the run stops. See ISSUES.md B59.
        var throat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7);

        var error = Assert.Throws<CfdException>(() => InletValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 50000, cylinderTemperature: 800, crankAngle: MainProgAngle(-10),
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(-10),
            throat, reverseTuning: -0.24));

        // Recorded rather than tolerated: this is what the original does with the same
        // inputs, and Main.pas turns it into "Terminated on CFD Error".
        Assert.Contains("Subsonic Velocity Solve", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStrongCylinderPushesGasBackUpThePipe()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var grid = Grid(pipe);

        // Cylinder far above the pipe, valve well open: the choked branch.
        // Well into the inlet valve's opening ramp, in trace convention.
        const double CrankAngle = 355;
        var throat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7);

        var result = InletValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 400000, cylinderTemperature: 1100, crankAngle: MainProgAngle(CrankAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(CrankAngle),
            throat, reverseTuning: -0.24);

        // Reverse flow is negative by convention at both the throat and the pipe end.
        Assert.True(result.Velocity < 0, $"Throat velocity came out {result.Velocity:F1} m/s.");
        Assert.True(grid.Velocity[38] < 0, $"Pipe-end velocity came out {grid.Velocity[38]:F1} m/s.");

        // Choked, so the throat sits at Mach 1 and the pipe end is pushed above plenum
        // pressure by the gas arriving.
        Assert.Equal(1, result.MachNumber);
        Assert.True(grid.Pressure[38] > PlenumPressure);
    }

    [Fact]
    public void TheThroatStateIsSelfConsistent()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var grid = Grid(pipe);

        // Well into the inlet valve's opening ramp, in trace convention.
        const double CrankAngle = 355;
        var throat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, PlenumPressure, 0.7);

        var result = InletValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 400000, cylinderTemperature: 1100, crankAngle: MainProgAngle(CrankAngle),
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(CrankAngle),
            throat, reverseTuning: -0.24);

        // MassFlow consumes these four together, so they have to agree with each other:
        // density, pressure and speed of sound are related by c^2 = gamma*P/rho.
        var implied = CharacteristicSolver.InletGamma * result.Pressure
                      / (result.SpeedOfSound * result.SpeedOfSound);

        Assert.Equal(implied, result.Density, Math.Abs(implied) * 1e-6);
        Assert.True(result.DischargeCoefficient is > 0 and <= 1);
    }

    [Fact]
    public void ASeatedValveHasNoDischargeCoefficient()
    {
        BaselinePaths.Require();

        var (_, valve) = Inlet();

        // At 341 degrees the inlet valve is at its timing point but the cam profile still
        // holds it seated, so the lift ratio is zero. The original assigns zero and then
        // overwrites it with an unassigned local; the port returns the zero. See B57.
        Assert.Equal(0, valve.Lift(341));
        Assert.Equal(0, valve.FlowCoefficient(341, 1.5, reverse: true));

        // Off the end of every shipped table the original answers with a flat 0.7.
        Assert.Equal(0.7, valve.FlowCoefficient(0, 6.0, reverse: true));
    }
}
