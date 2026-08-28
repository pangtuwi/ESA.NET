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

        if (onApplied is not null)
        {
            viewModel.Applied += (_, _) => onApplied();
        }

        new EditEngineWindow { DataContext = viewModel, Title = $"Edit Engine - {path}" }.Show();
    }
}
