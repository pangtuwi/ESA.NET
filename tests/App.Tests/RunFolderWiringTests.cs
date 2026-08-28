using App.Core;
using App.Core.Model;
using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Pressing Run leaves a folder behind: the whole of it, not just the parts the operator
/// remembered to ask for.
/// </summary>
/// <remarks>
/// Before this the port wrote the nine manifold files beside the <c>.eng</c> and nothing
/// else at all - <c>PerformanceResultWriter</c> was never called from the application, and
/// the PVT trace export was a stub. See ISSUES.md C4, and A12 for the sweep.
/// </remarks>
public sealed class RunFolderWiringTests
{
    private static (MainWindowViewModel ViewModel, Workspace Workspace) Loaded(
        IMultiRunWindowService? editor = null)
    {
        var workspace = TestServices.TemporaryWorkspace();

        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
        {
            services.AddSingleton<IWorkspace>(workspace);
            services.AddSingleton(editor ?? new StubMultiRunEditor());
            services.AddSingleton<ISimulateOptionsWindowService>(new StubSimulateOptions());
        });

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");
        viewModel.EngineSpeed = 4000;
        viewModel.Settings.CycleCount = 6;
        viewModel.Settings.OneZoneCycleCount = 1;
        viewModel.Settings.MassBalance = 1;

        return (viewModel, workspace);
    }

    private static string OnlyRunFolder(Workspace workspace) =>
        Assert.Single(Directory.GetDirectories(workspace.RunsDirectory));

    private static MultiRunGrid Grid(params double[] speeds)
    {
        var grid = new MultiRunGrid();

        for (var row = 0; row < speeds.Length; row++)
        {
            grid[row, 0] = speeds[row].ToString(System.Globalization.CultureInfo.InvariantCulture);
            grid[row, 1] = "6";
        }

        return grid;
    }

    [Fact]
    public async Task ASinglePointRunWritesItsWholeFolder()
    {
        BaselinePaths.Require();

        var (viewModel, workspace) = Loaded();

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        var folder = OnlyRunFolder(workspace);

        // Named for when it ran and what it ran.
        Assert.EndsWith("_A2China", folder, StringComparison.Ordinal);

        // The nine manifold files, the performance row, the PVT trace and the manifest.
        // A2China.eng has SaveManfData=0, so under the original's gate none of these
        // would exist at all - every run archives its own now.
        Assert.False(viewModel.CurrentEngine!.Engine.Manifold.SaveManifoldData);

        foreach (var name in (string[])
                 [
                     "Inlet.txt", "Exhaust.txt", "Pcyl.txt", "Tcyl.txt", "MassFlow.txt",
                     "InlPress.m", "InlVel.m", "ExhPress.m", "ExhVel.m",
                     RunArchive.PerformanceFileName, RunArchive.TraceFileName,
                     RunArchive.ManifestFileName,
                 ])
        {
            Assert.True(
                File.Exists(Path.Combine(folder, name)), $"{name} is missing from the run folder.");
        }

        // And copies of everything it read, so the numbers can still be accounted for
        // after the engine is edited.
        var inputs = Directory.GetFiles(Path.Combine(folder, RunArchive.InputsFolderName));

        Assert.Equal(10, inputs.Length);
        Assert.Contains(inputs, f => Path.GetFileName(f) == "A2China.eng");

        // The status line says where it all went.
        Assert.Contains(folder, viewModel.RunStatus, StringComparison.Ordinal);

        // Nothing was written beside the engine file, which is where the port used to put
        // the manifold files and where the original put them if you were lucky.
        Assert.False(File.Exists(Path.Combine(BaselinePaths.Directory!, RunArchive.ManifestFileName)));
    }

    [Fact]
    public async Task TwoRunsGetTwoFolders()
    {
        BaselinePaths.Require();

        var (viewModel, workspace) = Loaded();

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);
        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(2, Directory.GetDirectories(workspace.RunsDirectory).Length);

        // Each holds its own results rather than the second landing on the first, which is
        // what bare relative names cost the original (ISSUES.md C4, C6).
        foreach (var folder in Directory.GetDirectories(workspace.RunsDirectory))
        {
            Assert.Equal(
                2, File.ReadAllLines(Path.Combine(folder, RunArchive.PerformanceFileName)).Length);
        }
    }

    [Fact]
    public async Task ASweepPutsEveryRowInOneFolder()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Grid = Grid(3000, 4000) };
        var (viewModel, workspace) = Loaded(editor);

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        var sweep = OnlyRunFolder(workspace);
        var rows = Directory.GetDirectories(sweep)
            .Select(Path.GetFileName)
            .Where(name => name!.StartsWith("Row", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["Row01_3000rpm", "Row02_4000rpm"], rows);

        // Each row's own manifold files and trace. The sweep wrote none of this before -
        // ISSUES.md A12, which was open for want of somewhere to put it.
        foreach (var row in rows)
        {
            var folder = Path.Combine(sweep, row!);

            Assert.True(File.Exists(Path.Combine(folder, "Pcyl.txt")), $"{row} has no Pcyl.txt.");
            Assert.True(File.Exists(Path.Combine(folder, "InlPress.m")), $"{row} has no InlPress.m.");
            Assert.True(File.Exists(Path.Combine(folder, RunArchive.TraceFileName)));
        }

        // One performance file at the top with a heading and both rows, which is the one
        // place the original's appending (ISSUES.md C6) is worth having.
        var performance = File.ReadAllLines(Path.Combine(sweep, RunArchive.PerformanceFileName));

        Assert.Equal(3, performance.Length);
        Assert.Equal(PerformanceResultWriter.Heading(), performance[0]);
        Assert.StartsWith("3000", performance[1].TrimStart(), StringComparison.Ordinal);
        Assert.StartsWith("4000", performance[2].TrimStart(), StringComparison.Ordinal);

        // And a manifest naming every row and the inputs the sweep started from.
        var manifest = File.ReadAllText(Path.Combine(sweep, RunArchive.ManifestFileName));

        Assert.Contains("Row01_3000rpm", manifest, StringComparison.Ordinal);
        Assert.Contains("Row02_4000rpm", manifest, StringComparison.Ordinal);
        Assert.Contains("A2China.eng", manifest, StringComparison.Ordinal);
        Assert.Contains("2 row(s)", manifest, StringComparison.Ordinal);
    }

    /// <summary>Answers the Save As with a path a test chose, so no dialog opens.</summary>
    private sealed class StubFiles : IFileDialogService
    {
        public string? SaveTextResult { get; set; }

        public string? SuggestedName { get; private set; }

        public string? StartIn { get; private set; }

        public Task<string?> OpenEngineAsync() => Task.FromResult<string?>(null);

        public Task<string?> SaveEngineAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> OpenMultiRunAsync() => Task.FromResult<string?>(null);

        public Task<string?> SaveMultiRunAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> SaveTextAsync(string title, string suggestedName, string startIn)
        {
            SuggestedName = suggestedName;
            StartIn = startIn;

            return Task.FromResult(SaveTextResult);
        }
    }

    [Fact]
    public async Task ThePvtTraceExportSavesTheLastRunsTraceAndOffersItsFolder()
    {
        BaselinePaths.Require();

        var files = new StubFiles();
        var workspace = TestServices.TemporaryWorkspace();

        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
        {
            services.AddSingleton<IWorkspace>(workspace);
            services.AddSingleton<IFileDialogService>(files);
            services.AddSingleton<IMultiRunWindowService>(new StubMultiRunEditor());
            services.AddSingleton<ISimulateOptionsWindowService>(new StubSimulateOptions());
        });

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");
        viewModel.EngineSpeed = 4000;
        viewModel.Settings.CycleCount = 6;
        viewModel.Settings.MassBalance = 1;

        // Nothing run yet, so there is nothing to export - the menu item was a stub
        // before this and offered itself whatever the state.
        Assert.False(viewModel.PvtTraceCommand.CanExecute(null));

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.True(viewModel.PvtTraceCommand.CanExecute(null));

        var target = Path.Combine(workspace.RootDirectory, "exported.txt");
        files.SaveTextResult = target;

        await viewModel.PvtTraceCommand.ExecuteAsync(null);

        // Saved where the operator asked, and identical to the copy the run folder already
        // holds - this is a copy out of the archive, not the only chance to keep it.
        Assert.Equal(RunArchive.TraceFileName, files.SuggestedName);
        Assert.Equal(OnlyRunFolder(workspace), files.StartIn);

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(OnlyRunFolder(workspace), RunArchive.TraceFileName)),
            File.ReadAllBytes(target));
    }

    [Fact]
    public async Task AStoppedRunStillLeavesAManifestSayingSo()
    {
        BaselinePaths.Require();

        var (viewModel, workspace) = Loaded();

        // Stop before the first step: the run produces nothing, but what was asked for and
        // what became of it is still worth recording.
        var run = viewModel.SinglePointSimulationCommand.ExecuteAsync(null);
        viewModel.StopCommand.Execute(null);
        await run;

        var folder = OnlyRunFolder(workspace);
        var manifest = File.ReadAllText(Path.Combine(folder, RunArchive.ManifestFileName));

        Assert.Contains("A2China.eng", manifest, StringComparison.Ordinal);
        Assert.Contains("4000 rev/min", manifest, StringComparison.Ordinal);
    }
}
