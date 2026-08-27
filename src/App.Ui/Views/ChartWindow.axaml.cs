using Avalonia.Controls;

namespace App.Ui.Views;

/// <summary>
/// Hosts one chart. The drawing is done by <see cref="Charts.ChartHost"/> through an
/// attached property, so there is nothing for this to do.
/// </summary>
public sealed partial class ChartWindow : Window
{
    public ChartWindow() => InitializeComponent();
}
