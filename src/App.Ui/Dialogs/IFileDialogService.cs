namespace App.Ui.Dialogs;

/// <summary>
/// Picks files for the File menu. Injected so view models can ask for a path without
/// holding a window, which also keeps them constructible in tests with no display.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Asks for an engine file to open, or null if the user cancelled.</summary>
    Task<string?> OpenEngineAsync();

    /// <summary>Asks where to save an engine file, or null if the user cancelled.</summary>
    Task<string?> SaveEngineAsync(string suggestedName);
}
