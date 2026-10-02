using App.Persistence;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace App.Tests;

/// <summary>
/// The original ESA icon reaches the windows through the style in <c>App.axaml</c>, so
/// a missing asset or a broken resource path shows up as a window with no icon rather
/// than as a build error.
/// </summary>
public sealed class WindowIconTests
{
    [AvaloniaFact]
    public void TheShellWindowCarriesTheEsaIcon()
    {
        var window = new MainWindow { DataContext = TestServices.Resolve<MainWindowViewModel>() };
        window.Show();

        try
        {
            Assert.NotNull(window.Icon);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADialogCarriesTheEsaIconToo()
    {
        var viewModel = TestServices.Resolve<EditEngineViewModel>();
        viewModel.Load(new EngineDefinitionStore().Read(Path.Combine(TestPaths.Samples, "Default.eng")));

        var window = new EditEngineWindow { DataContext = viewModel };
        window.Show();

        try
        {
            Assert.NotNull(window.Icon);
        }
        finally
        {
            window.Close();
        }
    }
}
