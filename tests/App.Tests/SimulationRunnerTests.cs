using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The runner that drives a whole simulation, as the application does.
/// </summary>
public sealed class SimulationRunnerTests
{
    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;
        engine.CrankAngleStep = 1;

        return engine;
    }

    private static SimulationSettings Settings() =>
        new() { CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1 };

    [Fact]
    public void OneCallReproducesTheAcceptanceResult()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        Assert.True(result.Converged);
        Assert.InRange(result.CyclesRun, 2, 6);

        // The same whole-cycle agreement the acceptance test measures, reached through
        // the interface the application actually calls.
        var reference = BaselinePaths.TraceColumn("PCyl");
        var worst = reference.Max(
            r => Math.Abs(result.Trace[(int)r.CrankAngle][2] - r.Value) / r.Value);

        Assert.True(worst < 0.005, $"Worst cylinder pressure error {worst:P3}.");
    }

    [Fact]
    public void TheCapturedTraceCarriesEveryRecordedQuantity()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        var point = result.Trace[0];

        // A spread across the twenty-eight, in SI as stored: volume in cubic metres,
        // pressure in pascals, mass in kilograms.
        Assert.InRange(point[1], 4e-5, 6e-5);
        Assert.InRange(point[2], 4e6, 7e6);
        Assert.InRange(point[3], 5e-4, 6.5e-4);

        // Valve areas come from the cam profiles, and both valves are shut at firing top
        // dead centre.
        Assert.Equal(0, point[16]);
        Assert.Equal(0, point[17]);

        // Gamma is a real ratio of specific heats, not a leftover zero.
        Assert.InRange(point[14], 1.1, 1.4);

        // Unburnt hydrocarbons are always zero: nothing computes them. See ISSUES.md B71.
        Assert.Equal(0, point[27]);
    }

    [Fact]
    public void ThePerformanceFiguresComeBackOnTheEngine()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        // SimulDat.txt reports 14.291 bar IMEP, 151.34 Nm and 63.395 kW. This is a
        // simulated run rather than the reference accumulators fed in, so it carries the
        // same ~0.3 per cent the whole-cycle comparison shows; a relative bound is the
        // honest comparison.
        void Within(double expected, double actual, string what) =>
            Assert.True(
                Math.Abs(actual - expected) / expected < 0.01,
                $"{what}: expected {expected}, got {actual:F3} "
                + $"({(actual - expected) / expected:P2}).");

        Within(14.291, result.Engine.Imep / 1e5, "IMEP");
        Within(151.34, result.Engine.Torque, "Torque");
        Within(63.395, result.Engine.BrakePower / 1e3, "Power");
    }

    [Fact]
    public void ProgressIsReportedAndTheRunCanBeCancelled()
    {
        BaselinePaths.Require();

        using var cancellation = new CancellationTokenSource();

        // Progress<T> posts to the captured synchronisation context, which in a test is
        // the thread pool, so the reports would arrive out of order and after the run had
        // finished. Reporting synchronously keeps the assertions meaningful.
        var collected = new List<SimulationProgress>();
        var runner = new SimulationRunner(new CachingExpressionEvaluator());

        Assert.Throws<OperationCanceledException>(() => runner.Run(
            BaselineEngine(),
            Settings(),
            new SynchronousProgress<SimulationProgress>(p =>
            {
                collected.Add(p);

                if (collected.Count == 200)
                {
                    cancellation.Cancel();
                }
            }),
            cancellation.Token));

        Assert.Equal(200, collected.Count);
        Assert.All(collected, p => Assert.InRange(p.CrankAngle, -359, 360));
        Assert.Equal(6, collected[0].RequestedCycles);
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    [Fact]
    public void TheResultCarriesTheSolversDiagnostics()
    {
        BaselinePaths.Require();

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(BaselineEngine(), Settings(), cancellation: TestContext.Current.CancellationToken);

        var diagnostics = Assert.IsType<RunDiagnostics>(result.Diagnostics);

        // The reference engine at 4000 rpm needs no clamp, no cap and carries no negative
        // state through the pipes - measured, so a change that starts needing one shows up.
        Assert.True(diagnostics.EquilibriumSolves > 0);
        Assert.True(diagnostics.Manifold.InteriorPoints > 0);
        Assert.InRange(diagnostics.Manifold.WorstOuterIterations, 1, 20);
        Assert.False(diagnostics.HasWarnings, diagnostics.Summary());
        Assert.Empty(diagnostics.Summary());
    }

    [Fact]
    public void TheDiagnosticsSummaryNamesOnlyWhatWasCounted()
    {
        var manifold = new App.Core.Manifold.ManifoldDiagnostics { NegativeFootStates = 2 };
        var diagnostics = new RunDiagnostics(
            EquilibriumSolves: 100,
            EquilibriumTemperatureClamps: 3,
            EquilibriumCapHits: 0,
            EquilibriumEstimateCapHits: 0,
            GasPropertyTemperatureClamps: 0,
            Manifold: manifold);

        Assert.True(diagnostics.HasWarnings);
        Assert.Equal(
            "3 equilibrium solve(s) above 4000 K, 2 negative pressure or density state(s) in the pipes",
            diagnostics.Summary());
    }

    [Fact]
    public void EachCorrectionReachesTheRunAndOverridesTakeThemAllBackOut()
    {
        BaselinePaths.Require();

        SimulationResult Run(Action<PhysicsCorrections> choose)
        {
            var settings = Settings();
            choose(settings.Physics);

            return new SimulationRunner(new CachingExpressionEvaluator())
                .Run(BaselineEngine(), settings, cancellation: TestContext.Current.CancellationToken);
        }

        double TorqueShift(Correction alone)
        {
            var legacy = Run(_ => { });
            var corrected = Run(p => p.Overrides[alone.Entry] = true);

            return Math.Abs(corrected.Engine.Torque / legacy.Engine.Torque - 1);
        }

        // Each correction on its own moves the answer by about what its register entry
        // measured at the reference settings: B14, B31, B32, B33 and B75 by a tenth of a
        // per cent or less, B50 by about one, B38 by about four, B46 by about six.
        Assert.InRange(TorqueShift(CorrectionCatalogue.Rkf5Coefficient), 1e-6, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.ClosedCylinderMassFlow), 0.03, 0.10);
        Assert.InRange(TorqueShift(CorrectionCatalogue.WoschniMotoredAngle), 1e-6, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.WoschniSweptVolume), 1e-6, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.WoschniNegativeVelocity), 1e-6, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.IvcReference), 0.02, 0.07);
        Assert.InRange(TorqueShift(CorrectionCatalogue.WoschniCombustionTerm), 1e-6, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.ManifoldGammas), 0.005, 0.03);
        Assert.InRange(TorqueShift(CorrectionCatalogue.ClosedValveWallVelocity), 0, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.SonicEntranceBracket), 0, 0.005);

        // B55 changes nothing on the baseline engine to the last bit: converging pressure to
        // 1e-3 Pa already pins density far inside its own tolerance (ISSUES.md B55).
        Assert.Equal(0, TorqueShift(CorrectionCatalogue.OpenEndDensityConvergence));
        Assert.InRange(TorqueShift(CorrectionCatalogue.InletReverseStall), 1e-6, 0.005);

        // Neither branch they change is reached at 4000 rpm: the exhaust substitution never
        // runs on the baseline engine, and the static and stagnation choke tests agree at
        // this speed (ISSUES.md B61, B62).
        Assert.Equal(0, TorqueShift(CorrectionCatalogue.ExhaustReverseSingleRelaxation));
        Assert.Equal(0, TorqueShift(CorrectionCatalogue.ExhaustReverseStagnationChoke));

        // B64 changes only the secant's path; B65 heats the charge with the gas actually at
        // the inlet valve, by about three per cent of torque at this speed.
        Assert.InRange(TorqueShift(CorrectionCatalogue.SecantProbe), 1e-7, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.LiveInletTemperature), 0.01, 0.06);

        // No area lookup on the baseline engine ever passes the end of its table (B4).
        Assert.Equal(0, TorqueShift(CorrectionCatalogue.AreaClamp));

        // B16 swaps a difference for the derivative it approximates, and B18 gives the
        // charge the residual fraction it was asked for: a thousandth of torque at most.
        Assert.InRange(TorqueShift(CorrectionCatalogue.AnalyticPressureDerivative), 1e-7, 0.005);
        Assert.InRange(TorqueShift(CorrectionCatalogue.ResidualMolecularWeight), 1e-4, 0.005);

        // With every correction overridden off, Corrected is Legacy to the last bit - the
        // switch adds nothing of its own.
        var legacyRun = Run(_ => { });
        var nothingOn = Run(p =>
        {
            p.Mode = PhysicsMode.Corrected;

            foreach (var correction in CorrectionCatalogue.All)
            {
                p.Overrides[correction.Entry] = false;
            }
        });

        Assert.Equal(legacyRun.Engine.Torque, nothingOn.Engine.Torque);
        Assert.Equal(legacyRun.Engine.Imep, nothingOn.Engine.Imep);
    }
}
