using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class RunTimeGraphOptionsWindowService : IRunTimeGraphOptionsWindowService
{
    private readonly Func<RunTimeGraphOptionsViewModel> _viewModels;

    public RunTimeGraphOptionsWindowService(Func<RunTimeGraphOptionsViewModel> viewModels) =>
        _viewModels = viewModels;

    public async Task<RunTimeGraphOptionsResult> ShowAsync(bool showGasFlowVelocities)
    {
        var viewModel = _viewModels();
        viewModel.Load(showGasFlowVelocities);

        var window = new RunTimeGraphOptionsWindow { DataContext = viewModel };

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

        return new RunTimeGraphOptionsResult(
            viewModel.Accepted,
            viewModel.ShowGasFlowVelocities);
    }

    private static Window? Owner() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
