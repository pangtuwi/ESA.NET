using App.Core.Model;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class SimulateOptionsWindowService : ISimulateOptionsWindowService
{
    private readonly Func<SimulateOptionsViewModel> _viewModels;

    public SimulateOptionsWindowService(Func<SimulateOptionsViewModel> viewModels) =>
        _viewModels = viewModels;

    /// <inheritdoc />
    public async Task<SimulateOptionsResult> ShowAsync(
        SimulationSettings settings, double engineSpeed)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var viewModel = _viewModels();

        viewModel.Load(settings, engineSpeed);

        var window = new SimulateOptionsWindow { DataContext = viewModel };

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

        return new SimulateOptionsResult(
            viewModel.Accepted,
            viewModel.EngineSpeed,
            viewModel.TotalCycles,
            viewModel.MassBalance,
            viewModel.Graphs);
    }

    private static Window? Owner() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
