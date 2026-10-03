using App.Core;
using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// The engine editor's Load, Save and OK (ISSUES.md A23). Load fills the form only; OK
/// makes the form the current engine; Save writes a copy and changes nothing else.
/// </summary>
public sealed class EditorLoadSaveTests
{
    private sealed class StubFiles : IFileDialogService
    {
        public string? OpenResult { get; set; }

        public string? SaveResult { get; set; }

        public Task<string?> OpenEngineAsync() => Task.FromResult(OpenResult);

        public Task<string?> SaveEngineAsync(string suggestedName) => Task.FromResult(SaveResult);

        public Task<string?> OpenMultiRunAsync() => Task.FromResult<string?>(null);

        public Task<string?> SaveMultiRunAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> SaveTextAsync(string title, string suggestedName, string startIn) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubEditor : IEditEngineWindowService
    {
        public EngineDefinition? Definition { get; private set; }

        public string? Path { get; private set; }

        public Action<EngineDefinition, string>? OnApplied { get; private set; }

        public void Show(EngineDefinition definition, string path, Action<EngineDefinition, string>? onApplied = null)
        {
            Definition = definition;
            Path = path;
            OnApplied = onApplied;
        }
    }

    private static readonly EngineDefinitionStore Store = new();

    private static string Sample(string name) => Path.Combine(TestPaths.Samples, name);

    private static string Scratch(string name)
    {
        var folder = Path.Combine(TestServices.DataRoot, "editor", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name);
    }

    /// <summary>A form opened on <paramref name="path"/>, as the editor window opens it.</summary>
    private static (EditEngineViewModel Form, StubFiles Files, EngineDefinition Original) Opened(string path)
    {
        var files = new StubFiles();
        var form = TestServices.Resolve<EditEngineViewModel>(
            services => services.AddSingleton<IFileDialogService>(files));

        var original = Store.Read(path);

        form.Load(original);
        form.FilePath = path;

        return (form, files, original);
    }

    [Fact]
    public async Task LoadReplacesTheFormAndNothingElse()
    {
        var (form, files, original) = Opened(Sample("Default.eng"));
        var loaded = Store.Read(Sample("Nissan5.eng"));

        Assert.NotEqual(loaded.Name, original.Name);

        files.OpenResult = Sample("Nissan5.eng");
        await form.LoadFileCommand.ExecuteAsync(null);

        Assert.Equal(loaded.Name, form.EngineName);
        Assert.Equal(loaded.Bore, form.Bore);
        Assert.Equal(Sample("Nissan5.eng"), form.FilePath);
        Assert.Contains("Nissan5.eng", form.Title, StringComparison.Ordinal);
        Assert.NotSame(original, form.Definition);

        // The definition the main window holds is untouched.
        Assert.Equal(Store.Read(Sample("Default.eng")).Name, original.Name);
    }

    [Fact]
    public async Task CancelAfterLoadAppliesNothing()
    {
        var (form, files, original) = Opened(Sample("Default.eng"));
        var applied = 0;
        var closed = 0;

        form.Applied += (_, _) => applied++;
        form.CloseRequested += (_, _) => closed++;

        files.OpenResult = Sample("Nissan5.eng");
        await form.LoadFileCommand.ExecuteAsync(null);
        form.CancelCommand.Execute(null);

        Assert.Equal(1, closed);
        Assert.Equal(0, applied);
        Assert.Equal(Store.Read(Sample("Default.eng")).Name, original.Name);
    }

    /// <summary>OK after Load makes the loaded file the main window's engine.</summary>
    [Fact]
    public async Task OkAfterLoadMakesTheLoadedFileTheCurrentEngine()
    {
        BaselinePaths.Require();

        var editor = new StubEditor();
        var main = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<IEditEngineWindowService>(editor));

        main.CurrentEngine = TestServices.Resolve<IEngineLoader>().Load(BaselinePaths.File("A2China.eng"));
        main.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        main.EditEngineCommand.Execute(null);

        var (form, files, _) = Opened(editor.Path!);
        files.OpenResult = Sample("Nissan5.eng");

        await form.LoadFileCommand.ExecuteAsync(null);
        form.OkCommand.Execute(null);
        editor.OnApplied!(form.Definition!, form.FilePath);

        Assert.Equal(Sample("Nissan5.eng"), main.CurrentEngineFile);
        Assert.Equal(Store.Read(Sample("Nissan5.eng")).Name, main.CurrentEngine!.Engine.Name);
        Assert.Same(form.Definition, main.CurrentEngine.Definition);
    }

    [Fact]
    public async Task SaveWritesACopyAndChangesNothingElse()
    {
        var (form, files, original) = Opened(Sample("Default.eng"));
        var applied = 0;

        form.Applied += (_, _) => applied++;

        form.Bore = 99;
        files.SaveResult = Scratch("Copy.eng");
        await form.SaveFileCommand.ExecuteAsync(null);

        Assert.Equal(99, Store.Read(files.SaveResult).Bore);
        Assert.Contains("Saved a copy", form.FileStatus, StringComparison.Ordinal);

        // Neither the definition the form holds nor the file it came from moved, and the
        // main window was not told: Save is not OK.
        Assert.Same(original, form.Definition);
        Assert.Equal(81.0, original.Bore);
        Assert.Equal(Sample("Default.eng"), form.FilePath);
        Assert.Equal(0, applied);
    }

    /// <summary>
    /// Saving untouched values writes the source file's bytes exactly, the same line
    /// <c>EditEngineViewModelTests</c> holds for OK.
    /// </summary>
    [Theory]
    [InlineData("Default.eng")]
    [InlineData("Nissan5.eng")]
    [InlineData("ChinaBora92.eng")]
    public async Task SavingAnUntouchedFormCopiesTheFileByteForByte(string name)
    {
        var (form, files, _) = Opened(Sample(name));

        files.SaveResult = Scratch(name);
        await form.SaveFileCommand.ExecuteAsync(null);

        Assert.Equal(File.ReadAllBytes(Sample(name)), File.ReadAllBytes(files.SaveResult));
    }

    [Fact]
    public void SaveIsUnavailableWhileAFieldIsInvalid()
    {
        var (form, _, _) = Opened(Sample("Default.eng"));

        Assert.True(form.SaveFileCommand.CanExecute(null));

        form.CylinderCount = 0;

        Assert.False(form.SaveFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFileThatWillNotLoadIsReportedAndTheFormKept()
    {
        var (form, files, original) = Opened(Sample("Default.eng"));

        files.OpenResult = Path.Combine(TestServices.DataRoot, "no-such-engine.eng");
        await form.LoadFileCommand.ExecuteAsync(null);

        Assert.Contains("Could not load", form.FileStatus, StringComparison.Ordinal);
        Assert.Same(original, form.Definition);
        Assert.Equal(original.Name, form.EngineName);
    }
}
