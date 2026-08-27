using App.Core.Charts;
using App.Ui.ViewModels;
using App.Ui.Views;

namespace App.Ui.Charts;

/// <inheritdoc />
public sealed class ChartWindowService : IChartWindowService
{
    /// <inheritdoc />
    public void Show(ChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        new ChartWindow { DataContext = new ChartWindowViewModel { Definition = definition } }.Show();
    }
}
