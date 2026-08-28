using App.Core;

namespace App.Ui.Dialogs;

/// <summary>Opens the engine editor. Injected for the same reason the chart windows are.</summary>
public interface IEditEngineWindowService
{
    /// <summary>Shows the eight-tab editor on <paramref name="definition"/>.</summary>
    /// <param name="onApplied">
    /// Called each time the operator presses OK, after the form has been written back to
    /// the definition. The window is not modal and OK does not close it, so this can fire
    /// more than once.
    /// </param>
    void Show(EngineDefinition definition, string path, Action? onApplied = null);
}
