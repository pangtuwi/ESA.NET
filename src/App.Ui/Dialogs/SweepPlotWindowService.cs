using App.Core.Simulation;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class SweepPlotWindowService : ISweepPlotWindowService
{
    private readonly Func<SweepPlotViewModel> _viewModels;

    public SweepPlotWindowService(Func<SweepPlotViewModel> viewModels) => _viewModels = viewModels;

    /// <inheritdoc />
    public async Task ShowAsync(IReadOnlyList<MultiRunRowResult> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var viewModel = _viewModels();
        viewModel.Load(rows);

        var window = new SweepPlotWindow { DataContext = viewModel };

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
