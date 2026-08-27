using App.Core.Charts;
using Avalonia;
using ScottPlot.Avalonia;

namespace App.Ui.Charts;

/// <summary>
/// Attaches a <see cref="ChartDefinition"/> to an <see cref="AvaPlot"/> from XAML, so the
/// plot can be bound to a view model like any other control.
/// </summary>
/// <remarks>
/// ScottPlot's control is driven imperatively, which would normally mean code in a view's
/// code-behind. An attached property keeps that out: the view binds
/// <c>charts:ChartHost.Definition</c> and this does the drawing when the value arrives or
/// changes.
/// </remarks>
public static class ChartHost
{
    /// <summary>The chart to draw.</summary>
    public static readonly AttachedProperty<ChartDefinition?> DefinitionProperty =
        AvaloniaProperty.RegisterAttached<AvaPlot, ChartDefinition?>(
            "Definition", typeof(ChartHost));

    static ChartHost() => DefinitionProperty.Changed.AddClassHandler<AvaPlot>(OnDefinitionChanged);

    public static ChartDefinition? GetDefinition(AvaPlot plot) =>
        plot is null ? throw new ArgumentNullException(nameof(plot)) : plot.GetValue(DefinitionProperty);

    public static void SetDefinition(AvaPlot plot, ChartDefinition? value)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.SetValue(DefinitionProperty, value);
    }

    private static void OnDefinitionChanged(AvaPlot plot, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not ChartDefinition definition)
        {
            return;
        }

        ChartRenderer.Apply(plot.Plot, definition);
        plot.Refresh();
    }
}
