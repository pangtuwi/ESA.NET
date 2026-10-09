using App.Core;
using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B81: the exhaust valve's two direction guards can latch a flow that only their
/// own pins sustain. Each guard pins a pressure a hair either side of the cylinder's to
/// hold the flow's direction while it still has momentum, but the pin alone implies a
/// velocity, and when that velocity is past the guard's own threshold the hold never ends.
/// </summary>
/// <remarks>
/// Found through Corrected at lambda 0.80 on the baseline engine, which stopped in cycle 1
/// with "Density negative in cThermo": the exhaust boundary latched on outflow at 240
/// degrees, the cylinder was compressed sealed to 7.8 bar by top dead centre, and that
/// charge blew back into the inlet pipe when overlap began.
/// </remarks>
public sealed class ExhaustValveLatchTests
{
    private const double Gamma = CharacteristicSolver.ExhaustGamma;

    /// <summary>The baseline engine's EVF, EVFR and EVR.</summary>
    private static readonly (double Forward, double ForwardReverse, double Reverse) Tuning = (0.41, -0.715, -0.6);

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static Engine BaselineEngine() =>
        new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore())
        .Load(BaselinePaths.File("A2China.eng")).Engine;

    private static (PipeGeometry Pipe, ValveMotion Valve) Exhaust()
    {
        var engine = BaselineEngine();

        return (new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength),
                ValveMotion.FromValve(engine.Manifold.ExhaustValve));
    }

    private static PipeGrid ExhaustGrid(PipeGeometry pipe, double pressure, double temperature)
    {
        var grid = new PipeGrid(EsaLimits.ExhaustGridPoints);
        PipeGridInitialiser.Initialise(grid, 16, pipe.Length, pressure, temperature, Gamma);
        return grid;
    }

    /// <summary>
    /// Four steps of the open routine against a pipe held at 1.1 bar and a cylinder at 0.8,
    /// starting from an outflow that has decayed to 0.42 m/s, just over EVF. Returns the
    /// pipe-end velocity after each step.
    /// </summary>
    private static double[] OutflowAgainstAHigherPipe(double cylinderTemperature, bool release, ManifoldDiagnostics diagnostics)
    {
        const double cylinderPressure = 80000;
        var (pipe, valve) = Exhaust();
        var grid = ExhaustGrid(pipe, 110000, 1250);
        var next = ExhaustGrid(pipe, 110000, 1250);
        grid.Velocity[0] = 0.42;

        var throat = new InletValveReverseBoundary.ThroatState(
            0, 0, Math.Sqrt(Gamma * 287 * cylinderTemperature), 0, cylinderPressure, 0.7);

        var velocities = new double[4];

        for (var step = 0; step < velocities.Length; step++)
        {
            // 240 degrees after top dead centre, the exhaust stroke, as found.
            throat = ExhaustValveOpenBoundary.Apply(
                grid, next, pipe, valve, TimeStep(), cylinderPressure, cylinderTemperature, 600,
                pipe.Area(0), valve.FlowArea(240), throat, Tuning, Gamma,
                releaseLatch: release, diagnostics: diagnostics);

            grid.Velocity[0] = next.Velocity[0];
            grid.Pressure[0] = next.Pressure[0];
            grid.Density[0] = next.Density[0];
            grid.SpeedOfSound[0] = next.SpeedOfSound[0];

            velocities[step] = grid.Velocity[0];
        }

        return velocities;
    }

    /// <summary>
    /// The oracle: gas does not flow from a cylinder at 0.8 bar into a pipe at 1.1. The
    /// original holds 0.42 m/s of outflow for as long as it is asked, because the throat pin
    /// at 0.999999 of cylinder pressure implies that much on its own at 1250 K; corrected,
    /// the flow turns inward at once.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHotCylinderDoesNotKeepPushingIntoAHigherPipe(bool release)
    {
        BaselinePaths.Require();

        var diagnostics = new ManifoldDiagnostics();
        var velocities = OutflowAgainstAHigherPipe(1250, release, diagnostics);

        if (release)
        {
            Assert.All(velocities, u => Assert.True(u < -100, $"Pipe-end velocity {u:F3} m/s."));
            Assert.Equal(1, diagnostics.ExhaustValveLatchReleases);
            Assert.Equal(0, diagnostics.ExhaustValveLatches);
        }
        else
        {
            Assert.All(velocities, u => Assert.InRange(u, Tuning.Forward, 0.43));
            Assert.Equal(velocities.Length, diagnostics.ExhaustValveLatches);
        }
    }

    /// <summary>
    /// The threshold is temperature. At 1150 K the pin's floor, which scales with the
    /// cylinder's speed of sound, falls under EVF, so the original's guard lets go and the
    /// flow turns inward as it should.
    /// </summary>
    [Fact]
    public void ACoolerCylinderNeverLatchedInTheFirstPlace()
    {
        BaselinePaths.Require();

        var diagnostics = new ManifoldDiagnostics();
        var velocities = OutflowAgainstAHigherPipe(1150, release: false, diagnostics);

        Assert.All(velocities, u => Assert.True(u < -100, $"Pipe-end velocity {u:F3} m/s."));
        Assert.Equal(0, diagnostics.ExhaustValveLatches);
    }

    /// <summary>
    /// The mirror oracle: gas does not flow from a pipe at 1.1 bar into a cylinder at 3. The
    /// original holds 1.8 m/s of inflow, the stagnation pin at 1.000001 of cylinder pressure
    /// sustaining it past EVR; corrected, the reverse routine substitutes outward flow.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHigherCylinderDoesNotKeepDrawingFromALowerPipe(bool release)
    {
        BaselinePaths.Require();

        const double cylinderPressure = 300000;
        var (pipe, valve) = Exhaust();
        var grid = ExhaustGrid(pipe, 110000, 1100);
        grid.Velocity[0] = -1.0;

        var throat = new InletValveReverseBoundary.ThroatState(
            0, 0, Math.Sqrt(Gamma * 287 * 1100), 0, cylinderPressure, 0.7);
        var diagnostics = new ManifoldDiagnostics();

        for (var step = 0; step < 4; step++)
        {
            throat = ExhaustValveReverseBoundary.Apply(
                grid, pipe, valve, TimeStep(), cylinderPressure, 1500, 660,
                pipe.Area(0), valve.FlowArea(300), throat, (Tuning.Forward, Tuning.Reverse), Gamma,
                singleRelaxation: true, stagnationChoke: true,
                releaseLatch: release, diagnostics: diagnostics);

            if (release)
            {
                Assert.Equal(0, grid.Velocity[0]);
            }
            else
            {
                Assert.InRange(grid.Velocity[0], -1.8, Tuning.Reverse);
            }
        }

        Assert.Equal(release ? 0 : 4, diagnostics.ExhaustValveLatches);
    }

    /// <summary>
    /// The case that was reported: the baseline engine at lambda 0.80 and 4000 rpm under
    /// Corrected. With B81 it runs; without it the exhaust boundary latches through the
    /// exhaust stroke of the first cycle and the run stops in the inlet pipe.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CorrectedRunsAtLambdaPointEight(bool release)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        engine.Rpm = 4000;

        foreach (var gas in (Gas[])[engine.Plenum, engine.Cylinder, engine.Exhaust, engine.Atmosphere])
        {
            gas.Fuel.Lambda = 0.80;
        }

        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.ExhaustValveLatch.Entry] = release;

        var settings = new SimulationSettings
        {
            CycleCount = 8,
            OneZoneCycleCount = 1,
            MassBalance = 1,
            Physics = physics,
        };

        var runner = new SimulationRunner(new CachingExpressionEvaluator());

        if (!release)
        {
            var failure = Assert.Throws<CfdException>(
                () => runner.Run(engine, settings, cancellation: TestContext.Current.CancellationToken));
            Assert.Contains("Density negative", failure.Message);
            return;
        }

        var result = runner.Run(engine, settings, cancellation: TestContext.Current.CancellationToken);

        // Measured at 159.8 Nm, against 163.3 at lambda 1.
        Assert.InRange(result.Engine.Torque, 150, 170);
        Assert.True(result.Diagnostics!.Manifold.ExhaustValveLatchReleases > 0);
    }
}
