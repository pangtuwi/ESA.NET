using App.Core.Charts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace App.Ui.ViewModels;

/// <summary>
/// One chart window. The Delphi original has a separate form per graph - the torque
/// curve, the valve lift, the energy balance and the rest - each with its own hard-wired
/// series; here they are all the same window showing a different
/// <see cref="ChartDefinition"/>.
/// </summary>
public sealed partial class ChartWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ChartDefinition? _definition;

    /// <summary>Window caption, taken from the chart itself.</summary>
    public string Title => Definition?.Title ?? "Chart";

    partial void OnDefinitionChanged(ChartDefinition? value) => OnPropertyChanged(nameof(Title));
}
