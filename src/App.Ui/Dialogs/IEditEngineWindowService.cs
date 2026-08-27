using App.Core;

namespace App.Ui.Dialogs;

/// <summary>Opens the engine editor. Injected for the same reason the chart windows are.</summary>
public interface IEditEngineWindowService
{
    /// <summary>Shows the eight-tab editor on <paramref name="definition"/>.</summary>
    void Show(EngineDefinition definition, string path);
}
