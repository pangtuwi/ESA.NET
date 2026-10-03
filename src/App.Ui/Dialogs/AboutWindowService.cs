using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class AboutWindowService : IAboutWindowService
{
    /// <inheritdoc />
    public async Task ShowAsync()
    {
        var viewModel = new AboutViewModel();
        var window = new AboutWindow { DataContext = viewModel };

        void Close(object? sender, EventArgs args) => window.Close();

        viewModel.CloseRequested += Close;

        try
        {
            if (Owner() is { } owner)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        }
        finally
        {
            viewModel.CloseRequested -= Close;
        }
    }

    private static Window? Owner() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
