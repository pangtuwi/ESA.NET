using App.Core.Simulation;

namespace App.Ui.Dialogs;

/// <summary>Opens the multi-run results plot on a sweep's rows.</summary>
public interface ISweepPlotWindowService
{
    Task ShowAsync(IReadOnlyList<MultiRunRowResult> rows);
}
