using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class ApplicationShell : IApplicationShell
{
    /// <inheritdoc />
    /// <remarks>
    /// <c>Shutdown</c> rather than closing the main window, because the chart windows are
    /// top-level windows of their own and would otherwise keep the process alive.
    /// </remarks>
    public void Exit()
    {
        if (Desktop() is { } desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <inheritdoc />
    public async Task<bool> OpenFileAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path) || Desktop()?.MainWindow is not { } window)
        {
            return false;
        }

        return await window.Launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    private static IClassicDesktopStyleApplicationLifetime? Desktop() =>
        Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
}
