using App.Core;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// That an edit reaches the engine the simulation reads.
/// </summary>
/// <remarks>
/// <para>
/// The original reads <c>Save Manifold Data</c> off the edit form rather than the engine,
/// so the form had to have been opened once for it to mean anything (ISSUES.md C2), and
/// the line it is read by only ever assigns <c>TRUE</c>, so unticking never turned output
/// off again (C3).
/// </para>
/// <para>
/// The port reads the engine instead, which removes the latch - but
/// <c>EngineLoadResult</c> carries an engine and a definition that are separate snapshots,
/// and the editor writes only to the definition. Until the shell re-derived the engine on
/// OK, ticking the box changed nothing at all. These pin both halves.
/// </para>
/// </remarks>
public sealed class EngineEditRefreshTests
{
    /// <summary>Stands in for the editor window, exposing the OK callback to the test.</summary>
    private sealed class StubEditor : IEditEngineWindowService
    {
        public EngineDefinition? Definition { get; private set; }

        public Action? OnApplied { get; private set; }

        public void Show(EngineDefinition definition, string path, Action? onApplied = null)
        {
            Definition = definition;
            OnApplied = onApplied;
        }
    }

    private static (MainWindowViewModel ViewModel, StubEditor Editor) Loaded()
    {
        var editor = new StubEditor();

        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<IEditEngineWindowService>(editor));

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");

        return (viewModel, editor);
    }

    /// <summary>Opens the editor, changes something and presses OK.</summary>
    private static void Edit(
        MainWindowViewModel viewModel, StubEditor editor, Action<EditEngineViewModel> change)
    {
        viewModel.EditEngineCommand.Execute(null);

        var form = TestServices.Resolve<EditEngineViewModel>();

        form.Load(editor.Definition!);
        change(form);
        form.OkCommand.Execute(null);

        editor.OnApplied!();
    }

    [Fact]
    public void TickingSaveManifoldDataReachesTheEngineTheRunReads()
    {
        BaselinePaths.Require();

        var (viewModel, editor) = Loaded();

        // A2China.eng has SaveManfData=0.
        Assert.False(viewModel.CurrentEngine!.Engine.Manifold.SaveManifoldData);

        Edit(viewModel, editor, form => form.SaveManifoldData = true);

        Assert.True(viewModel.CurrentEngine!.Definition.SaveManifoldData);
        Assert.True(viewModel.CurrentEngine.Engine.Manifold.SaveManifoldData);
        Assert.True(viewModel.CurrentEngine.Engine.SaveManifoldData);
    }

    [Fact]
    public void UntickingItTurnsOutputOffAgain()
    {
        BaselinePaths.Require();

        // C3: in the original the flag only ever latched on. Here it goes both ways.
        var (viewModel, editor) = Loaded();

        Edit(viewModel, editor, form => form.SaveManifoldData = true);
        Assert.True(viewModel.CurrentEngine!.Engine.Manifold.SaveManifoldData);

        Edit(viewModel, editor, form => form.SaveManifoldData = false);
        Assert.False(viewModel.CurrentEngine!.Engine.Manifold.SaveManifoldData);
    }

    [Fact]
    public void TheEngineNeedsNoTripThroughTheEditorToHonourTheFile()
    {
        BaselinePaths.Require();

        // C2: the original reads the form, so the engine's own setting means nothing until
        // the window has been opened. Here the loader applies it and the editor is optional.
        var loaded = TestServices.Resolve<IEngineLoader>().Load(BaselinePaths.File("A2China.eng"));

        Assert.Equal(loaded.Definition.SaveManifoldData, loaded.Engine.Manifold.SaveManifoldData);
        Assert.Equal(loaded.Definition.SaveManifoldData, loaded.Engine.SaveManifoldData);
    }

    [Fact]
    public void OtherEditedValuesReachTheEngineToo()
    {
        BaselinePaths.Require();

        // The staleness was never confined to the checkbox: every field the editor writes
        // went to the definition and stopped there.
        var (viewModel, editor) = Loaded();
        var before = viewModel.CurrentEngine!.Engine.Bore;

        Edit(viewModel, editor, form => form.Bore = before + 1);

        Assert.Equal(before + 1, viewModel.CurrentEngine!.Engine.Bore, 6);
    }

    [Fact]
    public void TheDefinitionSurvivesTheRebuildSoTheOpenEditorStaysLive()
    {
        BaselinePaths.Require();

        // OK does not close the window, so the editor keeps its reference and can be used
        // again. A rebuild that swapped the definition out would strand it.
        var (viewModel, editor) = Loaded();
        var definition = viewModel.CurrentEngine!.Definition;

        Edit(viewModel, editor, form => form.SaveManifoldData = true);

        Assert.Same(definition, viewModel.CurrentEngine!.Definition);
        Assert.Same(definition, editor.Definition);
    }
}
