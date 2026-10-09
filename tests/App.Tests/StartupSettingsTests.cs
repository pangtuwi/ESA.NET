using App.Core;
using App.Core.Model;
using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// <c>ESA.ini</c>'s run options reach the main window at startup, as Delphi's
/// <c>LoadIniValues</c> did from <c>FormCreate</c> (Main.pas:772).
/// </summary>
/// <remarks>
/// For a while the store parsed <c>[Simulation]</c> and <c>[Physics]</c> correctly and the
/// main window ignored both, starting every session from the class defaults. That made the
/// <c>[Physics]</c> overrides, which can be set nowhere else, do nothing at all.
/// </remarks>
public sealed class StartupSettingsTests
{
    [Fact]
    public void TheMainWindowStartsFromEsaIni()
    {
        var path = WriteIni("""
            [Simulation]
            EngineSpeed=5500
            Nocycles=9
            No1zcycles=2
            MassBalance=0.5

            [Physics]
            Mode=Corrected
            B14=0
            """);

        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
            services.AddTransient(_ => new SimulationSettingsStore().Read(path)));

        Assert.Equal(5500, viewModel.EngineSpeed);
        Assert.Equal(9, viewModel.Settings.CycleCount);
        Assert.Equal(2, viewModel.Settings.OneZoneCycleCount);
        Assert.Equal(0.5, viewModel.Settings.MassBalance);
        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);
        Assert.False(viewModel.Settings.Physics.Overrides["B14"]);
    }

    [Fact]
    public void WithoutEsaIniTheMainWindowStartsFromTheDelphiDefaults()
    {
        // No ESA.ini sits beside the test assembly, so this is the real registration
        // reading a missing file.
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        Assert.Equal(4000, viewModel.EngineSpeed);
        Assert.Equal(6, viewModel.Settings.CycleCount);
        Assert.Equal(1, viewModel.Settings.MassBalance);

        // The port's own default, not the original's: it had no corrections to make.
        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);
        Assert.Empty(viewModel.Settings.Physics.Overrides);
    }

    [Fact]
    public async Task ThePhysicsOverridesReachTheRun()
    {
        BaselinePaths.Require();

        // The dialog chooses only the mode; an override from ESA.ini must survive its OK
        // and reach the solver, which the run's manifest records.
        var path = WriteIni("""
            [Physics]
            B14=1
            """);
        var dialog = new StubSimulateOptions { Physics = PhysicsMode.Legacy };
        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
        {
            services.AddTransient(_ => new SimulationSettingsStore().Read(path));
            services.AddSingleton<ISimulateOptionsWindowService>(dialog);
        });

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.Opened);
        Assert.True(viewModel.Settings.Physics.Overrides["B14"]);

        var manifest = File.ReadAllText(Path.Combine(viewModel.LastRunDirectory!, "run.txt"));
        Assert.Contains("corrections on: B14", manifest, StringComparison.Ordinal);
    }

    private static string WriteIni(string text)
    {
        var directory = Path.Combine(TestServices.DataRoot, "ini", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, SimulationSettingsStore.FileName);
        File.WriteAllText(path, text);

        return path;
    }
}
