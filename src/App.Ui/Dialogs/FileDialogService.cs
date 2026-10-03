using App.Core;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType EngineFiles = new("Engine files")
    {
        Patterns = ["*.eng"],
    };

    private static readonly FilePickerFileType MultiRunFiles = new("Multi-run grids")
    {
        Patterns = ["*.msr"],
    };

    private static readonly FilePickerFileType TextFiles = new("Text files")
    {
        Patterns = ["*.txt"],
    };

    private readonly IWorkspace _workspace;

    public FileDialogService(IWorkspace workspace) => _workspace = workspace;

    /// <inheritdoc />
    public async Task<string?> OpenEngineAsync()
    {
        if (Owner() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Engine",
            AllowMultiple = false,
            FileTypeFilter = [EngineFiles],
            SuggestedStartLocation = await FolderAsync(window, _workspace.EnginesDirectory),
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> OpenMultiRunAsync()
    {
        if (Owner() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Multi-Run Grid",
            AllowMultiple = false,
            FileTypeFilter = [MultiRunFiles],
            SuggestedStartLocation = await FolderAsync(window, _workspace.EnginesDirectory),
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveMultiRunAsync(string suggestedName)
    {
        if (Owner() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Multi Simulation Run File",
            SuggestedFileName = suggestedName,
            DefaultExtension = "msr",
            FileTypeChoices = [MultiRunFiles],
            SuggestedStartLocation = await FolderAsync(window, _workspace.EnginesDirectory),
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveEngineAsync(string suggestedName)
    {
        if (Owner() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Engine As",
            SuggestedFileName = suggestedName,
            DefaultExtension = "eng",
            FileTypeChoices = [EngineFiles],
            SuggestedStartLocation = await FolderAsync(window, _workspace.EnginesDirectory),
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveTextAsync(string title, string suggestedName, string startIn)
    {
        if (Owner() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = [TextFiles],
            SuggestedStartLocation = await FolderAsync(
                window, startIn.Length == 0 ? _workspace.RunsDirectory : startIn),
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// The folder a picker should open in, or null to leave it to the platform - which is
    /// what happens when the folder is not there yet.
    /// </summary>
    private static async Task<IStorageFolder?> FolderAsync(Window window, string path)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        try
        {
            return await window.StorageProvider.TryGetFolderFromPathAsync(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The picker needs a window to parent itself to: the one the operator is working in.
    /// Reaching for it through the lifetime rather than injecting it avoids a cycle: the
    /// window's own view model is what asks for the dialog.
    /// </summary>
    /// <remarks>
    /// This used to be the main window every time. Behind a modal dialog - the multi-run
    /// grid - the main window is disabled, and on macOS a picker parented to it opens as a
    /// sheet on a window that cannot take input, so Load and Save did nothing at all
    /// (GitHub issue 148).
    /// </remarks>
    private static Window? Owner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.Windows.LastOrDefault(window => window.IsActive)
               ?? desktop.Windows.LastOrDefault(window => window.IsVisible)
               ?? desktop.MainWindow;
    }
}
