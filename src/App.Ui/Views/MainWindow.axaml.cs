using Avalonia.Controls;

namespace App.Ui.Views;

/// <summary>
/// The shell window. Views hold no logic beyond binding; the code-behind exists only
/// to load the XAML.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
