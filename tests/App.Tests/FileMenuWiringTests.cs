using App.Core;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// The File menu, and the path from opening an engine to being able to run it.
/// </summary>
/// <remarks>
/// This covers the gap that made the shipped application unusable: every File command was
/// a stub, so nothing could set <c>CurrentEngine</c>, and Run stayed disabled with no way
/// for the operator to change that.
/// </remarks>
public sealed class FileMenuWiringTests
{
    /// <summary>Stands in for the file picker, so the dialog never opens in a test.</summary>
    private sealed class StubFileDialog : IFileDialogService
    {
        public string? OpenResult { get; set; }

        public string? SaveResult { get; set; }

        public string? SuggestedName { get; private set; }

        public Task<string?> OpenEngineAsync() => Task.FromResult(OpenResult);

        public Task<string?> SaveEngineAsync(string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(SaveResult);
        }
    }

    private sealed class StubEditor : IEditEngineWindowService
    {
        public int Opened { get; private set; }

        public void Show(EngineDefinition definition, string path) => Opened++;
    }

    private static (MainWindowViewModel ViewModel, StubFileDialog Files, StubEditor Editor) Build()
    {
        var files = new StubFileDialog();
        var editor = new StubEditor();

        var services = App.Ui.ServiceRegistration.CreateServices();
        services.AddSingleton<IFileDialogService>(files);
        services.AddSingleton<IEditEngineWindowService>(editor);

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<MainWindowViewModel>(), files, editor);
    }

    [Fact]
    public async Task OpeningAnEngineEnablesRunning()
    {
        BaselinePaths.Require();

        var (viewModel, files, _) = Build();

        // This is the state a freshly started application is in.
        Assert.Null(viewModel.CurrentEngine);
        Assert.False(viewModel.SinglePointSimulationCommand.CanExecute(null));

        files.OpenResult = BaselinePaths.File("A2China.eng");
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Opening a file is the only route an operator has to enabling Run.
        Assert.NotNull(viewModel.CurrentEngine);
        Assert.True(viewModel.SinglePointSimulationCommand.CanExecute(null));
        Assert.Contains("Opened", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellingThePickerLeavesEverythingAlone()
    {
        var (viewModel, files, _) = Build();

        files.OpenResult = null;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.CurrentEngine);
        Assert.Empty(viewModel.RunStatus);
    }

    [Fact]
    public async Task AFileThatWillNotOpenIsReportedRatherThanThrown()
    {
        var (viewModel, files, _) = Build();

        files.OpenResult = Path.Combine(Path.GetTempPath(), "no-such-engine.eng");

        // A missing or malformed file must not reach the dispatcher and close the
        // application, which is what an unhandled exception in a command would do.
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.CurrentEngine);
        Assert.Contains("Could not open", viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsAndEditNeedAnEngineFirst()
    {
        BaselinePaths.Require();

        var (viewModel, files, editor) = Build();

        Assert.False(viewModel.SaveAsCommand.CanExecute(null));
        Assert.False(viewModel.EditEngineCommand.CanExecute(null));

        files.OpenResult = BaselinePaths.File("A2China.eng");
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.SaveAsCommand.CanExecute(null));
        Assert.True(viewModel.EditEngineCommand.CanExecute(null));

        viewModel.EditEngineCommand.Execute(null);
        Assert.Equal(1, editor.Opened);

        // The save dialog is offered the current file's name rather than a blank box.
        files.SaveResult = null;
        await viewModel.SaveAsCommand.ExecuteAsync(null);
        Assert.Equal("A2China.eng", files.SuggestedName);
    }

    [Fact]
    public void LoadDefaultReportsWhenTheConfiguredEngineIsMissing()
    {
        var (viewModel, _, _) = Build();

        // There is no ESA.ini beside the test assembly, so the store returns its
        // defaults, which name Default.eng - and that is not there either.
        viewModel.LoadDefaultCommand.Execute(null);

        Assert.Null(viewModel.CurrentEngine);
        Assert.Contains("not found", viewModel.RunStatus, StringComparison.Ordinal);
    }
}
