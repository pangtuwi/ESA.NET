using App.Core;

namespace App.Ui.Dialogs;

/// <summary>Opens the engine editor. Injected for the same reason the chart windows are.</summary>
public interface IEditEngineWindowService
{
    /// <summary>Shows the eight-tab editor on <paramref name="definition"/>, read from <paramref name="path"/>.</summary>
    /// <param name="onApplied">
    /// Called when the operator presses OK, after the form has been written back, with the
    /// definition the form holds and the file it came from. Those are the ones passed in
    /// unless the editor's Load read another file, in which case they are that file's.
    /// </param>
    void Show(EngineDefinition definition, string path, Action<EngineDefinition, string>? onApplied = null);
}
