using Avalonia.Controls;

namespace App.Ui.Views;

/// <summary>
/// The Single Speed Simulation dialog. Views hold no logic beyond binding; the code-behind
/// exists only to load the XAML.
/// </summary>
public sealed partial class SimulateOptionsWindow : Window
{
    public SimulateOptionsWindow() => InitializeComponent();
}
