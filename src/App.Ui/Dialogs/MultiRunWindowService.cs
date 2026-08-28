using App.Core.Model;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class MultiRunWindowService : IMultiRunWindowService
{
    private readonly Func<MultiRunViewModel> _viewModels;

    public MultiRunWindowService(Func<MultiRunViewModel> viewModels) => _viewModels = viewModels;

    /// <inheritdoc />
    public async Task<MultiRunEditResult> ShowAsync(MultiRunGrid grid, string? baseFile)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var viewModel = _viewModels();

        viewModel.Load(grid);
        viewModel.SetBaseFile(baseFile);

        var window = new MultiRunWindow { DataContext = viewModel };

        // The view model asks to close; doing it here rather than in the window's
        // code-behind keeps the view free of logic.
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
                // No parent window, which is the headless case. Nothing to wait on.
                window.Show();
            }
        }
        finally
        {
            viewModel.CloseRequested -= Close;
        }

        return new MultiRunEditResult(viewModel.Accepted, viewModel.Grid, viewModel.ShowGraphs);
    }

    private static Window? Owner() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
