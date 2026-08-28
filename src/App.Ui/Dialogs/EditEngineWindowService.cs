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
    public void Show(EngineDefinition definition, string path, Action? onApplied = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var viewModel = _viewModels();
        viewModel.Load(definition);

        var window = new EditEngineWindow { DataContext = viewModel, Title = $"Edit Engine - {path}" };

        void Applied(object? sender, EventArgs args)
        {
            onApplied?.Invoke();
            window.Close();
        }

        viewModel.Applied += Applied;

        window.Show();
    }
}
