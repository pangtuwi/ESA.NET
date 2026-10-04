using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B64 and B65: the state at the valve end of the pipes - the secant that finds
/// it at the exhaust valve, and the temperature reported from it at the inlet valve.
/// </summary>
public sealed class ValveEndStateTests
{
    private const double BackPressure = 117800;
    private const double BackTemperature = 973.15;
    private const double ExhaustAngle = 200;

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

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

    /// <summary>
    /// B64's oracle. Which way the secant takes its first probe decides the path to the
    /// Mach-matching root, not the root: started upward, as the other three valve routines
    /// start it, the exhaust valve arrives at the same pipe-end state to well inside the
    /// solver's own tolerances. So the correction changes the arithmetic, and nothing a
    /// converged boundary should depend on.
    /// </summary>
    [Theory]
    [InlineData(600000, 1400)] // Choked blowdown.
    [InlineData(160000, 1100)] // Subsonic.
    public void TheExhaustSecantFindsTheSameRootWhicheverWayItProbes(
        double cylinderPressure, double cylinderTemperature)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var pipe = new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength);
        var valve = ValveMotion.FromValve(engine.Manifold.ExhaustValve);

        PipeGrid Solve(bool upward)
        {
            var current = new PipeGrid(EsaLimits.ExhaustGridPoints);
            var next = new PipeGrid(EsaLimits.ExhaustGridPoints);
            PipeGridInitialiser.Initialise(
                current, 16, pipe.Length, BackPressure, BackTemperature, CharacteristicSolver.ExhaustGamma);
            PipeGridInitialiser.Initialise(
                next, 16, pipe.Length, BackPressure, BackTemperature, CharacteristicSolver.ExhaustGamma);

            ExhaustValveOpenBoundary.Apply(
                current, next, pipe, valve, TimeStep(),
                cylinderPressure, cylinderTemperature,
                crankAngle: ExhaustAngle + 360,
                pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
                new InletValveReverseBoundary.ThroatState(
                    0, 0, Math.Sqrt(CharacteristicSolver.ExhaustGamma * 287 * BackTemperature),
                    0, BackPressure, 0.7),
                (0.41, -0.715, -0.6),
                upwardProbe: upward);

            return next;
        }

        var downward = Solve(upward: false);
        var upward = Solve(upward: true);

        // The outer iteration stops once pressure moves less than 1e-3 Pa and velocity
        // less than 1e-4 m/s between passes; the two answers sit well inside that.
        Assert.Equal(downward.Pressure[0], upward.Pressure[0], 0.01);
        Assert.Equal(downward.Velocity[0], upward.Velocity[0], 0.001);
    }

    /// <summary>
    /// B65's oracle. The temperature reported for the gas at the inlet valve - the one the
    /// plenum, and through it the charge entering the cylinder, is refreshed with - has to
    /// be that gas's own temperature, <c>p / (rho R)</c> at the valve end, at every step.
    /// The original reports the starting plenum temperature throughout, while the valve end
    /// sees backflow from the cylinder at several hundred kelvin more.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheInletTemperatureIsTheGasAtTheValveEnd(bool corrected)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var physics = new PhysicsCorrections();
        physics.Overrides[CorrectionCatalogue.LiveInletTemperature.Entry] = corrected;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        const int measuredCycle = 2;
        var cycle = 0;
        double lowest = double.MaxValue, highest = 0, worstMismatch = 0;

        solver.StepCompleted += s =>
        {
            if (cycle != measuredCycle)
            {
                return;
            }

            var reported = s.Engine.Manifold.PlenumTemperature;
            var inlet = s.Engine.Manifold.Inlet;
            var end = inlet.ActiveCount - 1;
            var atTheValve = inlet.Pressure[end] / (inlet.Density[end] * 287);

            lowest = Math.Min(lowest, reported);
            highest = Math.Max(highest, reported);
            worstMismatch = Math.Max(worstMismatch, Math.Abs((reported / atTheValve) - 1));
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
            Assert.InRange(worstMismatch, 0, 1e-9);

            // Backflow puts hot gas at the valve end for part of every cycle.
            Assert.True(highest - lowest > 100, $"The valve-end temperature only spanned {lowest:F0} to {highest:F0} K.");
        }
        else
        {
            Assert.Equal(lowest, highest);
            Assert.True(worstMismatch > 0.1, $"{worstMismatch}");
        }
    }
}
