using App.Persistence;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace App.Tests;

/// <summary>
/// Proves the shell window actually constructs and carries the menu recovered from
/// Main.dfm, without needing a display.
/// </summary>
public sealed class MenuStructureTests
{
    private static Menu Menu()
    {
        var window = new MainWindow { DataContext = TestServices.Resolve<MainWindowViewModel>() };
        return window.FindControl<Menu>("MainMenu")
               ?? throw new InvalidOperationException("The shell window has no menu.");
    }

    [AvaloniaFact]
    public void ShellWindowShowsTheLegacyMenuStructure()
    {
        var topLevel = Menu().Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();

        Assert.Equal(["_File", "_Run", "_Graph", "_Text", "_Help"], topLevel);
    }

    [AvaloniaFact]
    public void EditWindowBuildsWithEveryTab()
    {
        var viewModel = TestServices.Resolve<EditEngineViewModel>();
        viewModel.Load(new EngineDefinitionStore().Read(Path.Combine(TestPaths.Samples, "Default.eng")));

        var window = new EditEngineWindow { DataContext = viewModel };
        var tabs = window.FindControl<TabControl>("EditTabs")
                   ?? throw new InvalidOperationException("The edit window has no tab control.");

        var headers = tabs.Items.OfType<TabItem>().Select(t => t.Header as string).ToList();

        Assert.Equal(
            ["Cylinders", "Heat Trans", "Inlet", "Exhaust", "Cams", "Valves", "Fuel", "Model"],
            headers);
    }

    [AvaloniaFact]
    public void EveryMenuItemIsBoundToACommand()
    {
        var leaves = Menu().Items
            .OfType<MenuItem>()
            .SelectMany(top => top.Items.OfType<MenuItem>())
            .ToList();

        // Five File, five Run, nine Graph, one Text, two Help.
        //
        // The Graph menu has grown past what Main.dfm carried. The original drew five of
        // its charts - the P-V diagram, the in-cylinder trace and the three gas-flow
        // modes - inside its own main window, switched by radio buttons on a separate
        // options dialog rather than by menu items. They are windows here like the other
        // four, so they need somewhere to be opened from.
        Assert.Equal(22, leaves.Count);
        Assert.All(leaves, item => Assert.NotNull(item.Command));
    }
}
