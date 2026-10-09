using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The nine manifold output files, written from a converged run and compared with the
/// originals in <c>data/baseline/</c>.
/// </summary>
public sealed class ManifoldTraceWriterTests
{
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
        engine.CrankAngleStep = 1;

        return engine;
    }

    /// <summary>
    /// Runs the reference case the way the application does, and writes the nine files
    /// into a temporary directory.
    /// </summary>
    /// <remarks>
    /// Through <see cref="SimulationRunner"/>, not a hand-driven cycle loop. The first
    /// version found the cycle count with <c>RunCycles</c> and then recorded from a second
    /// pass of bare <c>RunOneCycle</c> calls that never set <c>ZoneCount</c>, leaving it at
    /// 0 - neither the single-zone model nor the two-zone one, but a hybrid the application
    /// never runs - and its bounds described that (ISSUES.md A11). At the reference's own
    /// settings, 6 cycles requested at 1 mg, the run converges after three cycles, which
    /// is the cycle the original's files hold: see ISSUES.md F1.
    /// </remarks>
    private static string RunAndWrite()
    {
        var writer = new ManifoldTraceWriter();

        var result = new SimulationRunner(new CachingExpressionEvaluator()).Run(
            BaselineEngine(),
            new SimulationSettings
            {
                CycleCount = 6,
                OneZoneCycleCount = 1,
                MassBalance = 1,
                Physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy },
            },
            cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: writer,
            recordManifoldData: true);

        // The comparison is only meaningful against the cycle the reference holds. Fail
        // loudly if the run stops anywhere else rather than quietly comparing the wrong one.
        Assert.True(result.Converged);
        Assert.Equal(ReferenceCycle, result.CyclesRun);
        Assert.True(result.ManifoldDataCaptured);

        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        writer.Write(directory);

        return directory;
    }

    /// <summary>
    /// The cycle the original's manifold files and PVT trace both hold: the third, which is
    /// the last the reference run simulated before converging (BASELINE.md, ISSUES.md F1).
    /// </summary>
    private const int ReferenceCycle = 3;

    [Fact]
    public void AllNineFilesAreWrittenWithTheOriginalsRowAndColumnCounts()
    {
        BaselinePaths.Require();

        var directory = RunAndWrite();

        try
        {
            foreach (var name in new[]
                     {
                         "Inlet.txt", "Exhaust.txt", "Pcyl.txt", "Tcyl.txt", "MassFlow.txt",
                         "InlPress.m", "InlVel.m", "ExhPress.m", "ExhVel.m",
                     })
            {
                var produced = File.ReadAllLines(Path.Combine(directory, name));
                var original = File.ReadAllLines(BaselinePaths.File(name));

                Assert.Equal(original.Length, produced.Length);

                // Same number of fields on every line, which for the .m files is one per
                // grid point and for the rest is the fixed column set.
                Assert.Equal(
                    original[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    produced[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheFieldLayoutMatchesTheOriginalCharacterForCharacter()
    {
        BaselinePaths.Require();

        var directory = RunAndWrite();

        try
        {
            // Column positions, not values: the numbers differ in the last places because
            // the run is the port's own, but every field must occupy the same width and
            // sit at the same offset. Blanking the digits leaves the skeleton to compare.
            foreach (var name in new[] { "Inlet.txt", "Exhaust.txt", "Pcyl.txt", "MassFlow.txt" })
            {
                var produced = File.ReadAllLines(Path.Combine(directory, name))[0];
                var original = File.ReadAllLines(BaselinePaths.File(name))[0];

                static string Skeleton(string line) =>
                    new([.. line.Select(c => char.IsDigit(c) ? '#' : c)]);

                Assert.Equal(Skeleton(original), Skeleton(produced));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheCapturedWindowStartsAtFiringTopDeadCentre()
    {
        // 620 rows, not 720: the window runs from firing TDC round to inlet valve
        // closing, so the hundred steps before TDC belong to the previous cycle.
        Assert.True(ManifoldCaptureWindow.Contains(360, -100));
        Assert.True(ManifoldCaptureWindow.Contains(720, -100));
        Assert.True(ManifoldCaptureWindow.Contains(259, -100));

        Assert.False(ManifoldCaptureWindow.Contains(260, -100));
        Assert.False(ManifoldCaptureWindow.Contains(359, -100));
    }

    /// <summary>
    /// The values in the nine files, against the originals, at the accuracy actually
    /// achieved on the reference cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Absolute bounds rather than relative ones: the velocity and mass columns pass
    /// through zero several times a cycle, so a relative comparison there is dominated by
    /// division by nearly nothing and says more about the crossings than the physics.
    /// </para>
    /// <para>
    /// Two bounds per column. The <b>rms</b> bound is the accuracy check: it is what the
    /// port's agreement actually is, and it does not move between platforms. The
    /// <b>worst</b> bound only catches something gross. The worst differences are all at
    /// discrete switches - the inlet valve's flow reversing, a regime change in a valve
    /// routine - that land one crank degree apart in the port and the original, and a
    /// one-step shift in a switch is worth the whole jump across it. MassFlow.txt column 1
    /// shows it: the reference reverses at 186 and the port at 187, and the two agree to
    /// 0.001 mg either side, so the worst difference is 0.47 mg against an rms of 0.026.
    /// Last-bit differences between runtimes can move a switch by a step, which is what
    /// made the old single-bound test fail on macOS (ISSUES.md A11), so the worst bounds
    /// leave room for the whole jump.
    /// </para>
    /// <para>
    /// Measured on Linux x64, rms then worst: cylinder pressure 0.047 and 0.195 bar, the
    /// worst in the combustion bias of ISSUES.md A8; temperature 0.48 and 1.9 K; mass flow
    /// 0.026 and 0.47 mg in, 0.014 and 0.21 mg out; inlet field 0.0018 and 0.027 bar,
    /// 0.54 and 11.5 m/s; exhaust field 0.0008 and 0.006 bar, 0.34 and 6.9 m/s. The rms
    /// bounds are about twice those. The exhaust agrees better than the inlet on this
    /// cycle; the order-of-magnitude gap A10 recorded the other way round came from
    /// comparing a later cycle.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheValuesAgreeWithTheOriginalsToTheMeasuredBounds()
    {
        BaselinePaths.Require();

        var directory = RunAndWrite();

        try
        {
            // One (rms, worst) pair per value column, in file order.
            void Compare(string name, params (double Rms, double Worst)[] bounds)
            {
                var produced = Rows(Path.Combine(directory, name));
                var original = Rows(BaselinePaths.File(name));

                Assert.Equal(original.Count, produced.Count);

                for (var column = 0; column < bounds.Length; column++)
                {
                    var differences = produced.Zip(original, (mine, theirs) =>
                        mine[column + 1] - theirs[column + 1]).ToList();

                    Check($"{name} column {column + 1}", differences, bounds[column]);
                }
            }

            // Cylinder pressure in bar, temperature in kelvin, volume in cubic metres.
            Compare("Pcyl.txt", (0.1, 0.4));
            Compare("Tcyl.txt", (1.0, 4.0), (1e-12, 1e-12));

            // Mass through each valve per step, in milligrams.
            Compare("MassFlow.txt", (0.05, 1.0), (0.03, 0.5));

            // Pressure in bar and velocity in m/s at three stations along each pipe.
            Compare(
                "Inlet.txt",
                (0.002, 0.01), (1.5, 8.0), (0.004, 0.02), (1.0, 6.0), (0.004, 0.05), (1.5, 20.0));
            Compare(
                "Exhaust.txt",
                (0.0005, 0.002), (1.0, 4.0), (0.002, 0.01), (0.6, 2.0), (0.002, 0.012), (1.0, 12.0));

            // And the full field files, one column per grid point.
            CompareField("InlPress.m", (0.004, 0.05));
            CompareField("InlVel.m", (1.0, 20.0));
            CompareField("ExhPress.m", (0.0015, 0.012));
            CompareField("ExhVel.m", (0.7, 12.0));

            void CompareField(string name, (double Rms, double Worst) bound)
            {
                var produced = Rows(Path.Combine(directory, name));
                var original = Rows(BaselinePaths.File(name));

                Assert.Equal(original.Count, produced.Count);

                Check(
                    name,
                    [.. produced.Zip(original).SelectMany(pair => pair.First.Zip(pair.Second, (a, b) => a - b))],
                    bound);
            }

            static void Check(string what, List<double> differences, (double Rms, double Worst) bound)
            {
                var rms = Math.Sqrt(differences.Average(d => d * d));
                var worst = differences.Max(Math.Abs);

                Assert.True(rms <= bound.Rms, $"{what}: rms difference {rms:G4} exceeds {bound.Rms:G4}.");
                Assert.True(
                    worst <= bound.Worst, $"{what}: worst difference {worst:G4} exceeds {bound.Worst:G4}.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The manifold files and the PVT trace are the same cycle, sampled at different points
    /// in the step - not adjacent cycles, as BASELINE.md first had it (ISSUES.md F1).
    /// </summary>
    /// <remarks>
    /// The manifold row takes the cylinder pressure going into the manifold step; the
    /// trace records it after the mass-transfer pressure correction, which applies during
    /// valve overlap. So the two files differ only on the overlap angles either side of
    /// top dead centre - 55 of them, by up to 0.071 bar - and the port's own pair differs
    /// from each other in the same places by the same amounts.
    /// </remarks>
    [Fact]
    public void TheManifoldPressureDiffersFromTheTraceOnlyWhereTheOriginalsDo()
    {
        BaselinePaths.Require();

        var writer = new ManifoldTraceWriter();

        var result = new SimulationRunner(new CachingExpressionEvaluator()).Run(
            BaselineEngine(),
            new SimulationSettings
            {
                CycleCount = 6,
                OneZoneCycleCount = 1,
                MassBalance = 1,
                Physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy },
            },
            cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: writer,
            recordManifoldData: true);

        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        writer.Write(directory);

        try
        {
            // Pcyl.txt's crank angle is offset by 360 from the trace's.
            var trace = BaselinePaths.TraceColumn("PCyl")
                .ToDictionary(r => (int)r.CrankAngle, r => r.Value / 1e5);

            var theirs = Rows(BaselinePaths.File("Pcyl.txt"))
                .Select(r => r[1] - trace[(int)r[0] - 360])
                .ToList();

            var mine = Rows(Path.Combine(directory, "Pcyl.txt"))
                .Select(r => r[1] - (result.Trace[(int)r[0] - 360][2] / 1e5))
                .ToList();

            Assert.Equal(55, theirs.Count(d => Math.Abs(d) > 0.001));
            Assert.Equal(55, mine.Count(d => Math.Abs(d) > 0.001));

            // The same gap at every angle, to within a hundredth of the gap itself.
            Assert.All(mine.Zip(theirs), pair => Assert.True(
                Math.Abs(pair.First - pair.Second) <= 0.001,
                $"The port's gap {pair.First:F4} bar differs from the original's {pair.Second:F4}."));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static List<double[]> Rows(string path) =>
        [.. File.ReadAllLines(path)
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => double.Parse(f, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray())];
}
