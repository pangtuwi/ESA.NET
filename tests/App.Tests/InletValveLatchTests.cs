using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md A29, B59 and B82: the inlet valve's boundary between the stalled, forward and
/// reverse cases. Under B59 as first written, a stall set the pipe end to rest without the
/// pipe behind it, and the forward guard's pin then held an inflow at IVF that nothing but
/// the pin sustained; the pipe end's state ran away and Corrected stopped at 2500 and 2750
/// rpm on the baseline engine.
/// </summary>
public sealed class InletValveLatchTests
{
    private const double Gamma = CharacteristicSolver.InletGamma;

    /// <summary>The baseline engine's IVF, IVFR and IVR.</summary>
    private static readonly (double Forward, double ForwardReverse, double Reverse) Tuning = (0.645, -0.044, -0.24);

    private static (PipeGeometry Pipe, ValveMotion Valve) Inlet()
    {
        var engine = new EngineLoader(
                new EngineDefinitionStore(),
                new CamProfileReader(),
                new SpeedKeyedTableReader(),
                new WallTemperatureTableReader(),
                new ExhaustBackPressureTableReader(),
                new ManifoldAreaTableStore(),
                new DischargeCoefficientTableStore())
            .Load(BaselinePaths.File("A2China.eng")).Engine;

        return (new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength),
                ValveMotion.FromValve(engine.Manifold.InletValve));
    }

    private static PipeGrid InletGrid(PipeGeometry pipe, int points, double pressure, double temperature)
    {
        var grid = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(grid, points, pipe.Length, pressure, temperature, Gamma);
        return grid;
    }

    /// <summary>
    /// B59's oracle, repaired. A stalled valve is a closed end: the pipe end comes to rest,
    /// and a pipe already at rest keeps its pressure and density. The stall as first written
    /// averaged the pipe end towards the cylinder instead, a quarter of the way to 50 kPa
    /// here, which no closed end does.
    /// </summary>
    [Fact]
    public void AStalledValveIsAClosedEndToTheQuiescentPipeBehindIt()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var grid = InletGrid(pipe, 39, 99000, 298.15);
        var density = grid.Density[grid.ActiveCount - 1];

        var result = InletValveReverseBoundary.Apply(
            grid, pipe, valve, 1 / (4000.0 / 60 * 360),
            cylinderPressure: 50000, cylinderTemperature: 800, crankAngle: 350,
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(-10),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, 99000, 0.7),
            reverseTuning: Tuning.Reverse,
            stallBelowThroat: true);

        var end = grid.ActiveCount - 1;
        Assert.Equal(0, result.Velocity);
        Assert.Equal(0, grid.Velocity[end]);
        Assert.Equal(99000, grid.Pressure[end], 1e-6);
        Assert.Equal(density, grid.Density[end], 1e-12);
    }

    /// <summary>
    /// B59's trigger, repaired. The forward routine pins the throat to the cylinder before
    /// every hand-over to the reverse routine, so judged against the throat a cylinder that
    /// has risen above the pipe still stalls. Judged against the pipe end it flows out, into
    /// the pipe, as it must.
    /// </summary>
    [Fact]
    public void ACylinderAboveThePipePushesGasOutEvenWithTheThroatPinnedToIt()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Inlet();
        var grid = InletGrid(pipe, 26, 105000, 298.15);

        InletValveReverseBoundary.Apply(
            grid, pipe, valve, 1 / (2500.0 / 60 * 360),
            cylinderPressure: 112000, cylinderTemperature: 380, crankAngle: 177,
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(-183),
            new InletValveReverseBoundary.ThroatState(0, 0, Math.Sqrt(Gamma * 287 * 380), 0, 112000, 0.7),
            reverseTuning: Tuning.Reverse,
            stallBelowThroat: true);

        // Reverse flow runs towards the plenum, against the pipe's positive direction.
        Assert.True(grid.Velocity[grid.ActiveCount - 1] < -10, $"{grid.Velocity[grid.ActiveCount - 1]}");
    }

    /// <summary>
    /// B82's oracle: gas does not flow from a pipe at 104.4 kPa into a cylinder at 105.0. The
    /// inputs are a latch as it occurred, at 1750 rpm under Corrected: a pipe end held at
    /// 0.65 m/s of inflow, just over IVF, by the forward guard's pin a hair above the throat,
    /// while its density sinks step by step. Released, the inflow ends and the pipe end
    /// stops drifting.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APinnedInflowAgainstAHigherCylinderIsReleased(bool release)
    {
        BaselinePaths.Require();

        const double dt = 9.523809523809524E-05;
        const double cylinderPressure = 105022.23091455201;
        const double cylinderTemperature = 406.0333795673067;
        const double interiorPressure = 104406.35880912792;
        const double interiorDensity = 1.1753295847922296;

        var (pipe, valve) = Inlet();
        var interiorTemperature = interiorPressure / interiorDensity / 287;
        var grid = InletGrid(pipe, 18, interiorPressure, interiorTemperature);
        var next = InletGrid(pipe, 18, interiorPressure, interiorTemperature);
        var q = grid.ActiveCount - 1;

        for (var i = 0; i <= q; i++)
        {
            grid.Velocity[i] = 1.183973727220296;
        }

        grid.Velocity[q] = 0.6514927869645714;
        grid.Pressure[q] = 105770.13238945848;
        grid.Density[q] = 0.8867529391403741;
        grid.SpeedOfSound[q] = Math.Sqrt(Gamma * grid.Pressure[q] / grid.Density[q]);

        var throat = new InletValveReverseBoundary.ThroatState(
            0.005053637190020367, 2.0314952099915105, 401.9867540161353, 0.8873163717702711,
            105765.49208268501, 0.8);
        var diagnostics = new ManifoldDiagnostics();
        var velocities = new double[6];
        var densities = new double[6];

        for (var step = 0; step < velocities.Length; step++)
        {
            throat = InletValveOpenBoundary.Apply(
                grid, next, pipe, valve, dt, cylinderPressure, cylinderTemperature, 126,
                pipe.Area(pipe.Length), 0.0021523490626543357, throat, Tuning, Gamma,
                stallBelowThroat: true, releaseLatch: release, diagnostics: diagnostics);

            grid.Velocity[q] = next.Velocity[q];
            grid.Pressure[q] = next.Pressure[q];
            grid.Density[q] = next.Density[q];
            grid.SpeedOfSound[q] = next.SpeedOfSound[q];

            velocities[step] = grid.Velocity[q];
            densities[step] = grid.Density[q];
        }

        if (release)
        {
            // Out of the cylinder or at rest, as the two pressures, within 0.3 per cent of
            // each other, settle: never the pin's inflow, and the pipe end holds steady.
            Assert.All(velocities, u => Assert.True(u <= 0, $"Pipe-end velocity {u:F3} m/s."));
            Assert.InRange(densities.Max() - densities.Min(), 0, 0.005);
            Assert.Equal(1, diagnostics.InletValveLatchReleases);
            Assert.Equal(0, diagnostics.InletValveLatches);
        }
        else
        {
            // Measured at 0.650 to 0.653 m/s, the density falling 0.2 per cent a step.
            Assert.All(velocities, u => Assert.InRange(u, Tuning.Forward, 0.66));
            Assert.True(densities.Zip(densities.Skip(1)).All(d => d.Second < d.First));
            Assert.Equal(velocities.Length, diagnostics.InletValveLatches);
        }
    }
}
