using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md A39: B59 stalls the inlet valve when the cylinder cannot push gas into the
/// pipe. Judged against the pipe end's pressure from the previous step, that flipped every
/// step near equality, stalled and flowing by turns, and fed an odd-even oscillation into
/// the pipe. Judged against the pressure the end would have this step with nothing
/// flowing - the arriving characteristic's - it does not.
/// </summary>
public sealed class InletValveStallTests
{
    private const double PipePressure = 99000;
    private const double Temperature = 298.15;
    private const double CrankAngle = 355;

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    /// <summary>
    /// The inlet pipe at rest at <paramref name="pipe"/>, except its end, left by the
    /// previous step at <paramref name="end"/>; the valve opening, the cylinder at
    /// <paramref name="cylinder"/>, the throat carried in at <paramref name="throat"/>.
    /// Returns the pipe end's velocity after the step.
    /// </summary>
    private static double PipeEndVelocity(double pipe, double end, double cylinder, double throat)
    {
        var engine = Loader().Load(BaselinePaths.File("A2China.eng")).Engine;
        var geometry = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);
        var valve = ValveMotion.FromValve(engine.Manifold.InletValve);
        var grid = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(grid, 39, geometry.Length, pipe, Temperature, CharacteristicSolver.InletGamma);

        grid.Pressure[38] = end;
        grid.Density[38] = grid.Density[37] * Math.Pow(end / pipe, 1 / CharacteristicSolver.InletGamma);
        grid.SpeedOfSound[38] = Math.Sqrt(CharacteristicSolver.InletGamma * end / grid.Density[38]);

        InletValveReverseBoundary.Apply(
            grid, geometry, valve, TimeStep(),
            cylinderPressure: cylinder, cylinderTemperature: 800, crankAngle: CrankAngle + 360,
            pipeAreaAtValve: geometry.Area(geometry.Length), valveFlowArea: valve.FlowArea(CrankAngle),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, throat, 0.7), reverseTuning: -0.24,
            wholeSubsonicBracket: true, stallBelowThroat: true, bracketFromZero: true);

        return grid.Velocity[38];
    }

    /// <summary>
    /// Oracle: the pipe behind the end is below the cylinder, so nothing stops the cylinder
    /// pushing gas into it, though the end itself was left above the cylinder last step.
    /// The gas flows out; judged on the stale end it stalled.
    /// </summary>
    [Fact]
    public void ACylinderAboveThePipeBehindAStaleEndFlowsOut()
    {
        BaselinePaths.Require();

        Assert.True(PipeEndVelocity(pipe: 99000, end: 115000, cylinder: 105000, throat: 105000) < 0);
    }

    /// <summary>
    /// Oracle: the pipe behind the end stands above the cylinder, so the cylinder cannot
    /// push into it, though the end was left below the cylinder last step and the throat at
    /// the end's pressure. It stalls. The converse of the case above; the original's own
    /// no-flow branch reaches the same answer here a few passes later.
    /// </summary>
    [Fact]
    public void ACylinderBelowThePipeBehindAStaleEndStalls()
    {
        BaselinePaths.Require();

        Assert.Equal(0, PipeEndVelocity(pipe: 115000, end: 99000, cylinder: 105000, throat: 99000));
    }

    private sealed class MassFlowRecorder : IManifoldRecorder
    {
        public List<double> MassIn { get; } = [];

        public void Record(in ManifoldRow row) => MassIn.Add(row.MassIn);

        public void Reset() => MassIn.Clear();
    }

    /// <summary>
    /// Oracle: across a whole converged cycle the inlet valve never stalls for a step
    /// between two steps of reverse flow. Before A39 the Nissan engines did so 8 to 87 times
    /// a cycle in Corrected, stalled and flowing by turns, the oscillation that grew until it
    /// stopped the 12 degree cam's run under B90.
    /// </summary>
    [Theory]
    [InlineData("Nissan_290_Adv12", 3000)]
    [InlineData("Nissan_290_Adv12", 4000)]
    [InlineData("Nissan_290", 4000)]
    [InlineData("Nissan_390", 4000)]
    public void TheValveNeverStallsBetweenTwoStepsOfReverseFlow(string folder, int rpm)
    {
        BaselinePaths.Require();

        var path = Directory.GetFiles(
            Path.Combine(BaselinePaths.Directory!, "..", "..", "validation", "cases", folder), "*.eng")[0];
        var engine = Loader().Load(path).Engine;
        engine.Rpm = rpm;

        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        var manifold = new ManifoldSolver(engine, physics: physics);
        var recorder = new MassFlowRecorder();
        manifold.Recorder = recorder;
        var solver = new CycleSolver(engine, manifold, physics: physics);
        Assert.True(solver.Initialise());

        engine.ZoneCount = 1;

        for (var cycle = 1; cycle <= 8; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            solver.RunOneCycle();
        }

        var flips = 0;

        for (var i = 1; i + 1 < recorder.MassIn.Count; i++)
        {
            if (recorder.MassIn[i] == 0 && recorder.MassIn[i - 1] < 0 && recorder.MassIn[i + 1] < 0)
            {
                flips++;
            }
        }

        Assert.Equal(0, flips);
    }
}
