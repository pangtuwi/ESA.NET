using App.Core;
using App.Core.Model;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Choosing Legacy or Corrected physics for a multi-point sweep, and ESA.ini's
/// <c>[Physics]</c> reaching the application at all.
/// </summary>
public sealed class SweepPhysicsTests
{
    /// <summary>Answers every read of ESA.ini with the settings a test sets.</summary>
    private sealed class StubSettingsStore(SimulationSettings settings) : ISimulationSettingsStore
    {
        public SimulationSettings Read(string path) => settings;

        public void Write(string path, SimulationSettings value)
        {
        }
    }

    private static SimulationSettings CorrectedWithB14Off()
    {
        var settings = new SimulationSettings();
        settings.Physics.Mode = PhysicsMode.Corrected;
        settings.Physics.Overrides["B14"] = false;
        return settings;
    }

    /// <summary>
    /// ESA.ini's <c>[Physics]</c> used to be read for nothing: every session started on
    /// Legacy whatever the file said, and its per-correction switches never applied.
    /// </summary>
    [Fact]
    public void EsaIniPhysicsIsWhatASessionStartsOn()
    {
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<ISimulationSettingsStore>(new StubSettingsStore(CorrectedWithB14Off())));

        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);
        Assert.False(viewModel.Settings.Physics.Overrides["B14"]);
        Assert.False(viewModel.Settings.Physics.IsOn(CorrectionCatalogue.Rkf5Coefficient));
    }

    [Fact]
    public void WithNoEsaIniASessionStartsOnCorrected()
    {
        // No ESA.ini sits beside the test assembly.
        var viewModel = TestServices.Resolve<MainWindowViewModel>();

        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);
        Assert.Empty(viewModel.Settings.Physics.Overrides);
    }

    [Fact]
    public void TheGridWindowStartsFromTheSessionsPhysicsAndNamesEsaIniSwitches()
    {
        var viewModel = TestServices.Resolve<MultiRunViewModel>();

        viewModel.SetPhysics(CorrectedWithB14Off().Physics);

        Assert.True(viewModel.CorrectedPhysics);
        Assert.False(viewModel.LegacyPhysics);
        Assert.Contains("B14=0", viewModel.PhysicsNote, StringComparison.Ordinal);

        viewModel.LegacyPhysics = true;

        Assert.Equal(PhysicsMode.Legacy, viewModel.Physics);
        Assert.False(viewModel.CorrectedPhysics);

        viewModel.SetPhysics(new PhysicsCorrections());
        Assert.Equal(string.Empty, viewModel.PhysicsNote);
    }

    [AvaloniaFact]
    public void TheGridWindowsPhysicsButtonsAreBound()
    {
        var viewModel = TestServices.Resolve<MultiRunViewModel>();
        viewModel.SetPhysics(new PhysicsCorrections());

        var window = new MultiRunWindow { DataContext = viewModel };
        window.Show();

        try
        {
            var legacy = window.FindControl<RadioButton>("LegacyPhysicsButton")!;
            var corrected = window.FindControl<RadioButton>("CorrectedPhysicsButton")!;

            Assert.True(corrected.IsChecked);
            Assert.False(legacy.IsChecked);

            legacy.IsChecked = true;

            Assert.Equal(PhysicsMode.Legacy, viewModel.Physics);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The sweep runs every row on the physics chosen in the grid window, and the choice
    /// is kept for the session as the Single Speed dialog's is. Before, a sweep borrowed
    /// whatever the last single-point run had used.
    /// </summary>
    [Fact]
    public async Task TheSweepRunsOnThePhysicsChosenInTheGridWindow()
    {
        BaselinePaths.Require();

        var grid = new MultiRunGrid();
        grid[0, 0] = "4000";
        grid[0, 1] = "3";

        var editor = new StubMultiRunEditor { Grid = grid, Physics = PhysicsMode.Corrected };
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<IMultiRunWindowService>(editor));

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>().Load(BaselinePaths.File("A2China.eng"));
        viewModel.CurrentEngineFile = BaselinePaths.File("A2China.eng");
        viewModel.Settings.MassBalance = 1;

        Assert.Equal(PhysicsMode.Legacy, editor.ShownPhysics);

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        var row = Assert.Single(viewModel.LastSweep!);

        Assert.NotNull(row.Result);
        Assert.Equal(PhysicsMode.Corrected, row.Result.Physics!.Mode);
        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);

        // And the next time the window opens it starts from that choice.
        editor.Physics = null;
        editor.Accept = false;
        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(PhysicsMode.Corrected, editor.ShownPhysics);
    }

    [Fact]
    public async Task CancellingTheGridWindowLeavesThePhysicsAlone()
    {
        BaselinePaths.Require();

        var editor = new StubMultiRunEditor { Accept = false, Physics = PhysicsMode.Legacy };
        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<IMultiRunWindowService>(editor));

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>().Load(BaselinePaths.File("A2China.eng"));

        await viewModel.MultiPointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(PhysicsMode.Corrected, viewModel.Settings.Physics.Mode);
    }
}
