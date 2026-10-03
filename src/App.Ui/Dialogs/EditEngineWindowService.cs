using App.Core;
using App.Ui.ViewModels;
using App.Ui.Views;

namespace App.Ui.Dialogs;

/// <inheritdoc />
public sealed class EditEngineWindowService : IEditEngineWindowService
{
    private readonly Func<EditEngineViewModel> _viewModels;

    public EditEngineWindowService(Func<EditEngineViewModel> viewModels) => _viewModels = viewModels;

    /// <inheritdoc />
    public void Show(EngineDefinition definition, string path, Action<EngineDefinition, string>? onApplied = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var viewModel = _viewModels();
        viewModel.Load(definition);
        viewModel.FilePath = path;

        // The caption is bound to the view model, so it follows a Load inside the editor.
        var window = new EditEngineWindow { DataContext = viewModel };

        void Applied(object? sender, EventArgs args)
        {
            onApplied?.Invoke(viewModel.Definition!, viewModel.FilePath);
            window.Close();
        }

        void Cancelled(object? sender, EventArgs args) => window.Close();

        void Closed(object? sender, EventArgs args)
        {
            viewModel.Applied -= Applied;
            viewModel.CloseRequested -= Cancelled;
            window.Closed -= Closed;
        }

        viewModel.Applied += Applied;
        viewModel.CloseRequested += Cancelled;
        window.Closed += Closed;

        window.Show();
    }
}
