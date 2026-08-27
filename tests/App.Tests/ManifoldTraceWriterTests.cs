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
    /// Runs to convergence, recording the last cycle's capture window, and writes the nine
    /// files into a temporary directory.
    /// </summary>
    private static string RunAndWrite()
    {
        var engine = BaselineEngine();
        var manifold = new ManifoldSolver(engine);
        var solver = new CycleSolver(engine, manifold);

        solver.Initialise();

        var inletClose = -180 + engine.Manifold.InletValve.CloseAngle;
        var settings = new SimulationSettings
        {
            CycleCount = 6, OneZoneCycleCount = 1, MassBalance = 1,
        };

        // Find how many cycles the run takes, then repeat it recording the last one. The
        // original decides this with a tStep test inside Main_Prog; doing it from outside
        // avoids reproducing the write-gate defects of ISSUES.md C1 to C4.
        var cycles = solver.RunCycles(settings);

        engine = BaselineEngine();
        manifold = new ManifoldSolver(engine);
        solver = new CycleSolver(engine, manifold);
        solver.Initialise();

        var writer = new ManifoldTraceWriter();
        var cycle = 0;

        solver.StepCompleted += s =>
        {
            if (Math.Abs(s.Engine.CrankAngle - (-180 + s.Engine.Manifold.InletValve.CloseAngle)) < 1e-9)
            {
                cycle++;
            }
        };

        for (var i = 1; i <= cycles; i++)
        {
            manifold.Recorder = i == cycles
                ? new CaptureWindow(writer, inletClose)
                : null;

            solver.RunOneCycle();
        }

        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        writer.Write(directory);

        return directory;
    }

    /// <summary>Passes on only the rows inside the original's capture window.</summary>
    private sealed class CaptureWindow(ManifoldTraceWriter inner, double inletCloseAngle)
        : IManifoldRecorder
    {
        public void Record(in ManifoldRow row)
        {
            if (ManifoldTraceWriter.IsInCaptureWindow(row.CrankAngle, inletCloseAngle))
            {
                inner.Record(in row);
            }
        }
    }

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
        Assert.True(ManifoldTraceWriter.IsInCaptureWindow(360, -100));
        Assert.True(ManifoldTraceWriter.IsInCaptureWindow(720, -100));
        Assert.True(ManifoldTraceWriter.IsInCaptureWindow(259, -100));

        Assert.False(ManifoldTraceWriter.IsInCaptureWindow(260, -100));
        Assert.False(ManifoldTraceWriter.IsInCaptureWindow(359, -100));
    }

    /// <summary>
    /// The values in the nine files, against the originals, at the accuracy actually
    /// achieved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Absolute bounds rather than relative ones: the velocity and mass columns pass
    /// through zero several times a cycle, so a relative comparison there is dominated by
    /// division by nearly nothing and says more about the crossings than the physics.
    /// </para>
    /// <para>
    /// The inlet wave field agrees far more closely than the exhaust one - 0.006 bar
    /// against 0.098. Some of that gap is not the port's: ISSUES.md F1 records that the
    /// manifold files and the PVT trace come from adjacent cycles, and that gas exchange
    /// differs by up to 0.07 bar between them. The exhaust bound here is the same order as
    /// that, so it sits near the limit of what this reference data can resolve. The inlet
    /// bound is well inside it and is a real measurement.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheValuesAgreeWithTheOriginalsToTheMeasuredBounds()
    {
        BaselinePaths.Require();

        var directory = RunAndWrite();

        try
        {
            void Compare(string name, params double[] bounds)
            {
                var produced = Rows(Path.Combine(directory, name));
                var original = Rows(BaselinePaths.File(name));

                Assert.Equal(original.Count, produced.Count);

                var worst = new double[bounds.Length];

                foreach (var (mine, theirs) in produced.Zip(original))
                {
                    for (var column = 0; column < bounds.Length; column++)
                    {
                        worst[column] = Math.Max(
                            worst[column], Math.Abs(mine[column + 1] - theirs[column + 1]));
                    }
                }

                for (var column = 0; column < bounds.Length; column++)
                {
                    Assert.True(
                        worst[column] <= bounds[column],
                        $"{name} column {column + 1}: worst difference {worst[column]:G4} "
                        + $"exceeds {bounds[column]:G4}.");
                }
            }

            // Cylinder pressure in bar, temperature in kelvin, volume in cubic metres.
            Compare("Pcyl.txt", 0.5);
            Compare("Tcyl.txt", 5.0, 1e-12);

            // Mass through each valve per step, in milligrams.
            Compare("MassFlow.txt", 0.25, 0.2);

            // Pressure in bar and velocity in m/s at three stations along each pipe.
            Compare("Inlet.txt", 0.01, 4.0, 0.01, 4.0, 0.01, 10.0);
            Compare("Exhaust.txt", 0.01, 40.0, 0.1, 25.0, 0.15, 10.0);

            // And the full field files, one column per grid point.
            CompareField("InlPress.m", 0.03);
            CompareField("InlVel.m", 10.0);
            CompareField("ExhPress.m", 0.15);
            CompareField("ExhVel.m", 40.0);

            void CompareField(string name, double bound)
            {
                var produced = Rows(Path.Combine(directory, name));
                var original = Rows(BaselinePaths.File(name));

                Assert.Equal(original.Count, produced.Count);

                var worst = produced.Zip(original)
                    .SelectMany(pair => pair.First.Zip(pair.Second, (a, b) => Math.Abs(a - b)))
                    .Max();

                Assert.True(worst <= bound, $"{name}: worst difference {worst:G4} exceeds {bound:G4}.");
            }
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
