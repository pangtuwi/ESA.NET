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

    /// <summary>Asks for a saved multi-run grid to open, or null if the user cancelled.</summary>
    Task<string?> OpenMultiRunAsync();

    /// <summary>Asks where to save a multi-run grid, or null if the user cancelled.</summary>
    Task<string?> SaveMultiRunAsync(string suggestedName);

    /// <summary>
    /// Asks where to save a text export, or null if the user cancelled.
    /// </summary>
    /// <param name="startIn">
    /// Where the picker opens - the run folder the file came from, when there is one.
    /// </param>
    Task<string?> SaveTextAsync(string title, string suggestedName, string startIn);
}
