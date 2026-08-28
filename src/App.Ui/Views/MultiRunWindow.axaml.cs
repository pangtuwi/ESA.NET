using Avalonia.Controls;

namespace App.Ui.Views;

/// <summary>
/// The multi-run grid editor window. Views hold no logic beyond binding; the code-behind
/// exists only to load the XAML.
/// </summary>
public sealed partial class MultiRunWindow : Window
{
    public MultiRunWindow() => InitializeComponent();
}
