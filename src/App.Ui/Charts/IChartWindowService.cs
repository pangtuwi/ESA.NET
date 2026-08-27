using App.Core.Charts;

namespace App.Ui.Charts;

/// <summary>
/// Opens chart windows. Injected so that view models can offer a chart without holding a
/// window, which also keeps them constructible in tests with no display.
/// </summary>
public interface IChartWindowService
{
    /// <summary>Shows <paramref name="definition"/> in its own window.</summary>
    void Show(ChartDefinition definition);
}
