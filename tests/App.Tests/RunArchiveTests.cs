using App.Core;
using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// A run folder: copies of what the run read, everything it produced, and a manifest
/// tying the two together.
/// </summary>
/// <remarks>
/// This is what ISSUES.md C4 was really about. Moving the nine manifold files out of the
/// working directory settled where they land; it did nothing about the fact that nothing
/// recorded which engine, which speed or which cam profile had produced them, and that
/// the next run overwrote them anyway. <c>data/baseline/</c> is this arrangement made by
/// hand, and BASELINE.md gives the reason it can be trusted where the output scattered
/// through <c>legacy/ESA/Data</c> cannot.
/// </remarks>
public sealed class RunArchiveTests
{
    private static readonly DateTimeOffset When =
        new(2026, 8, 28, 14, 15, 30, TimeSpan.Zero);

    private static IEngineLoader Loader() => new EngineLoader(
        new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

    private static (RunArchive Archive, EngineLoadResult Engine, string Path) Baseline()
    {
        var path = BaselinePaths.File("A2China.eng");
        var engine = Loader().Load(path);
        var archive = new RunArchive(
            TestServices.TemporaryWorkspace().CreateRunDirectory(path, When));

        return (archive, engine, path);
    }

    [Fact]
    public void TheInputsAreCopiedInVerbatim()
    {
        BaselinePaths.Require();

        var (archive, engine, path) = Baseline();

        var copied = archive.CopyInputs(path, engine);
        var inputs = Path.Combine(archive.Directory, RunArchive.InputsFolderName);

        // The engine and the nine distinct side files it read: two cam profiles, two
        // discharge coefficient grids named by four entries between them, two manifold
        // area tables, the wall temperatures, the exhaust back pressure and the spark map.
        Assert.Equal(10, copied.Count);
        Assert.Equal(10, Directory.GetFiles(inputs).Length);

        // The four Cd entries name two files, so one of them is listed under both its uses
        // rather than copied twice.
        Assert.Contains(copied, entry => entry.Contains("Cd, ", StringComparison.Ordinal));

        // Byte for byte, so an archived .eng is the file that was run - the same
        // guarantee EngRoundTripTests holds the writer to.
        Assert.Equal(
            File.ReadAllBytes(path),
            File.ReadAllBytes(Path.Combine(inputs, "A2China.eng")));

        foreach (var side in engine.SideFiles)
        {
            var archived = Path.Combine(inputs, Path.GetFileName(side.Path));

            Assert.True(File.Exists(archived), $"{side.Kind} was not archived.");
            Assert.Equal(File.ReadAllBytes(side.Path), File.ReadAllBytes(archived));
        }

        // And the originals are left alone.
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void TheManifestSaysWhatWasRunAndWhatCameOut()
    {
        BaselinePaths.Require();

        var (archive, engine, path) = Baseline();

        new RunManifest(When)
            .Engine(path, engine.Engine.Name, engine.Problems)
            .Requested(4000, new SimulationSettings { CycleCount = 6, MassBalance = 1 })
            .Outcome("Converged after 3 cycles.", TimeSpan.FromSeconds(12.4))
            .Inputs(archive.CopyInputs(path, engine))
            .Write(archive.ManifestFile);

        var manifest = File.ReadAllText(archive.ManifestFile);

        Assert.Contains("2026-08-28 14:15:30", manifest, StringComparison.Ordinal);
        Assert.Contains(engine.Engine.Name, manifest, StringComparison.Ordinal);
        Assert.Contains("4000 rev/min", manifest, StringComparison.Ordinal);
        Assert.Contains(
            File.ReadAllLines(archive.ManifestFile),
            line => line.StartsWith("Cycles requested", StringComparison.Ordinal)
                    && line.EndsWith(" 6", StringComparison.Ordinal));
        Assert.Contains("Converged after 3 cycles.", manifest, StringComparison.Ordinal);

        // The inputs are listed by name, so the folder can be read without opening it.
        Assert.Contains("A2China.eng", manifest, StringComparison.Ordinal);
        Assert.Contains("inlet cam profile", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void ACompletedRunFillsItsFolder()
    {
        BaselinePaths.Require();

        var (archive, load, path) = Baseline();
        var engine = load.Engine;

        engine.Rpm = 4000;
        engine.CrankAngleStep = 1;

        // Deliberately unticked: every run archives its manifold data now, so the
        // engine's own Save Manifold Data flag no longer decides. See ISSUES.md C1-C4.
        engine.Manifold.SaveManifoldData = false;

        var manifold = new ManifoldTraceWriter();

        var result = new SimulationRunner(new CachingExpressionEvaluator()).Run(
            engine,
            new SimulationSettings { CycleCount = 20, OneZoneCycleCount = 1, MassBalance = 1 },
            cancellation: TestContext.Current.CancellationToken,
            manifoldRecorder: manifold,
            recordManifoldData: true);

        Assert.True(result.ManifoldDataCaptured);

        archive.WriteTrace(result.Trace);
        archive.AppendPerformance(result.Engine);
        archive.WriteManifoldData(manifold);
        archive.CopyInputs(path, load);

        string[] expected =
        [
            "Inlet.txt", "Exhaust.txt", "Pcyl.txt", "Tcyl.txt", "MassFlow.txt",
            "InlPress.m", "InlVel.m", "ExhPress.m", "ExhVel.m",
            RunArchive.TraceFileName, RunArchive.PerformanceFileName,
        ];

        foreach (var name in expected)
        {
            var file = Path.Combine(archive.Directory, name);

            Assert.True(File.Exists(file), $"{name} is missing from the run folder.");
            Assert.True(new FileInfo(file).Length > 0, $"{name} is empty.");
        }

        // The performance file carries the original's heading and one row for this run.
        var rows = File.ReadAllLines(archive.PerformanceFile);

        Assert.Equal(PerformanceResultWriter.Heading(), rows[0]);
        Assert.Equal(2, rows.Length);
        Assert.StartsWith("4000", rows[1].TrimStart(), StringComparison.Ordinal);

        // And the trace is the full cycle, headed, 720 rows from -359 to 360.
        Assert.Equal(721, File.ReadAllLines(Path.Combine(
            archive.Directory, RunArchive.TraceFileName)).Length);
    }

    [Fact]
    public void ASecondRunOfTheSameEngineDoesNotOverwriteTheFirst()
    {
        BaselinePaths.Require();

        // The trap C4 leaves the operator in: bare relative names, so every run lands on
        // top of the last one.
        var workspace = TestServices.TemporaryWorkspace();
        var path = BaselinePaths.File("A2China.eng");
        var load = Loader().Load(path);

        var first = new RunArchive(workspace.CreateRunDirectory(path, When));
        var second = new RunArchive(workspace.CreateRunDirectory(path, When.AddMinutes(1)));

        first.CopyInputs(path, load);
        second.CopyInputs(path, load);

        Assert.NotEqual(first.Directory, second.Directory);
        Assert.Equal(2, Directory.GetDirectories(workspace.RunsDirectory).Length);
    }

    [Fact]
    public void TwoDifferentFilesSharingANameDoNotCollide()
    {
        BaselinePaths.Require();

        var (archive, load, path) = Baseline();

        // LegacyPathResolver searches the whole tree below the engine file, so two side
        // files with the same name in different folders is possible. Copying the second
        // over the first would archive an input the run never read.
        var elsewhere = Path.Combine(archive.Directory, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        var first = load.SideFiles[0];
        var twin = Path.Combine(elsewhere, Path.GetFileName(first.Path));
        File.Copy(first.Path, twin);

        var withTwin = new EngineLoadResult(
            load.Engine,
            load.Definition,
            load.Problems,
            [.. load.SideFiles, new ResolvedSideFile(first.Kind, first.Stored, twin)]);

        var copied = archive.CopyInputs(path, withTwin);
        var inputs = Directory.GetFiles(Path.Combine(archive.Directory, RunArchive.InputsFolderName));

        Assert.Equal(copied.Count, inputs.Length);

        Assert.Contains(
            inputs,
            f => Path.GetFileNameWithoutExtension(f).EndsWith("_2", StringComparison.Ordinal));
    }
}
