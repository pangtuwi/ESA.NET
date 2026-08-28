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

    /// <inheritdoc />
    public async Task<string?> OpenEngineAsync()
    {
        if (MainWindow() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Engine",
            AllowMultiple = false,
            FileTypeFilter = [EngineFiles],
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> OpenMultiRunAsync()
    {
        if (MainWindow() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Multi-Run Grid",
            AllowMultiple = false,
            FileTypeFilter = [MultiRunFiles],
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveMultiRunAsync(string suggestedName)
    {
        if (MainWindow() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Multi Simulation Run File",
            SuggestedFileName = suggestedName,
            DefaultExtension = "msr",
            FileTypeChoices = [MultiRunFiles],
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveEngineAsync(string suggestedName)
    {
        if (MainWindow() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Engine As",
            SuggestedFileName = suggestedName,
            DefaultExtension = "eng",
            FileTypeChoices = [EngineFiles],
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// The picker needs a window to parent itself to. Reaching for it through the
    /// lifetime rather than injecting it avoids a cycle: the window's own view model is
    /// what asks for the dialog.
    /// </summary>
    private static Window? MainWindow() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
