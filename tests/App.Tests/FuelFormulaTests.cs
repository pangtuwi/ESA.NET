using App.Core;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;
using App.Persistence;
using App.Persistence.Tables;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace App.Tests;

/// <summary>
/// ISSUES.md B96: the fuel is defined by its composition. Its stoichiometric air-fuel ratio is the
/// formula's, not the <c>AFRatio</c> input; its heating value is the fuel's own unless the file
/// gives <c>QFuel</c>; a composition that borrows the petrol or diesel fit takes that fit's heat
/// capacity per kilogram; and the composition may be fractional.
/// </summary>
public sealed class FuelFormulaTests
{
    private static Engine Baseline()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;
        return engine;
    }

    private static IEnumerable<Fuel> Fuels(Engine engine) =>
        ((Gas[])[engine.Plenum, engine.Cylinder, engine.Exhaust, engine.Atmosphere]).Select(g => g.Fuel);

    private static void Initialise(Engine engine, PhysicsCorrections physics)
    {
        var solver = new CycleSolver(engine, new App.Core.Manifold.ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");
    }

    /// <summary>
    /// The oracle for the run: under Corrected the cylinder's fuel carries its formula's
    /// stoichiometric ratio, Heywood's gasoline's 14.605 for a file that gives no composition,
    /// and the file's 43 MJ/kg, which it gives; Legacy keeps the file's 14.5.
    /// </summary>
    [Fact]
    public void CorrectedBurnsTheFormulasRatioAndLegacyTheInput()
    {
        BaselinePaths.Require();

        var corrected = Baseline();
        Initialise(corrected, new PhysicsCorrections());
        Assert.Equal(14.605, corrected.Cylinder.Fuel.AFRatio, 0.001);
        Assert.Equal(43e6, corrected.Cylinder.Fuel.Q, 1);

        var legacy = Baseline();
        Initialise(legacy, new PhysicsCorrections { Mode = PhysicsMode.Legacy });
        Assert.Equal(14.5, legacy.Cylinder.Fuel.AFRatio, 1e-12);
    }

    /// <summary>
    /// Without <c>QFuel</c> the heating value is the fuel's own: Heywood's gasoline's, 43.66
    /// MJ/kg, which the performance figures then take.
    /// </summary>
    [Fact]
    public void WithoutQFuelTheHeatingValueIsTheFuelsOwn()
    {
        BaselinePaths.Require();

        var engine = Baseline();

        foreach (var fuel in Fuels(engine))
        {
            fuel.Q = 0;
        }

        Initialise(engine, new PhysicsCorrections());
        Assert.Equal(43.656e6, engine.Cylinder.Fuel.Q, 0.005e6);
    }

    /// <summary>A composition the file gives is burnt as given, at its own ratio.</summary>
    [Fact]
    public void AGivenCompositionIsBurntAtItsOwnRatio()
    {
        BaselinePaths.Require();

        var engine = Baseline();

        foreach (var fuel in Fuels(engine))
        {
            (fuel.C, fuel.H, fuel.O, fuel.N, fuel.CompositionGiven) = (2, 6, 1, 0, true);
        }

        Initialise(engine, new PhysicsCorrections());

        // Ethanol: Heywood's eq. 3.6 with its oxygen, 9.0.
        Assert.Equal(GasPropertyModel.StoichiometricAirFuelRatio(2, 6, 1, 0), engine.Cylinder.Fuel.AFRatio, 1e-12);
        Assert.Equal(9.0, engine.Cylinder.Fuel.AFRatio, 0.02);
    }

    /// <summary>
    /// The oracle for the borrowed fit: a hydrocarbon's vapour heat capacity per kilogram varies
    /// little with molecule size (Heywood Table 4.11 at 600 K: propane 2.86, isooctane 2.78,
    /// gasoline 2.66, diesel 2.58 kJ/(kg K)), so a C8H15, which borrows the diesel fit, should
    /// have about the gasoline's. Scaled per kilogram it is 5.0 to 6.9 per cent above Heywood's
    /// gasoline from 400 to 800 K; per mole, as the original borrowed it, 87 to 90 per cent above.
    /// Its formation enthalpy per mole is the fit's either way.
    /// </summary>
    [Fact]
    public void ABorrowedFitIsScaledPerKilogram()
    {
        var heywood = new GasPropertyModel(burned: false);
        heywood.Setup(GasPropertyModel.HeywoodGasoline, 0, 0, 0, 0, 1, 0.05);

        GasPropertyModel UserFuel(bool scale)
        {
            var gas = new GasPropertyModel(burned: false) { ScaleBorrowedFit = scale };
            gas.Setup(0, 8, 15, 0, 0, 1, 0.05);
            return gas;
        }

        var scaled = UserFuel(scale: true);
        var original = UserFuel(scale: false);

        foreach (var t in new[] { 400.0, 600, 800 })
        {
            var reference = heywood.FuelSpecificHeat(t) / heywood.FuelMolecularWeight();

            Assert.InRange(scaled.FuelSpecificHeat(t) / scaled.FuelMolecularWeight() / reference, 0.95, 1.08);
            Assert.InRange(original.FuelSpecificHeat(t) / original.FuelMolecularWeight() / reference, 1.7, 1.95);
        }

        Assert.Equal(original.FuelEnthalpy(GasPropertyModel.HeatingValueTemperature), scaled.FuelEnthalpy(GasPropertyModel.HeatingValueTemperature), 1e-6);
    }

    /// <summary>
    /// Through the editor's real window: the composition takes fractions, and the formula's ratio
    /// beside it is Heywood's gasoline's while the composition is left at the default, then the
    /// typed formula's.
    /// </summary>
    [AvaloniaFact]
    public void TheEditorTakesAFractionalCompositionAndShowsItsRatio()
    {
        var viewModel = TestServices.Resolve<EditEngineViewModel>();
        viewModel.Load(new EngineDefinitionStore().Read(Path.Combine(TestPaths.Samples, "Default.eng")));

        var window = new EditEngineWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var ratio = window.FindControl<TextBlock>("FormulaAirFuelRatioText");
            Assert.NotNull(ratio);
            Assert.Equal("14.6", ratio.Text);

            window.FindControl<TextBox>("FuelCarbonBox")!.Text = "8.26";
            window.FindControl<TextBox>("FuelHydrogenBox")!.Text = "15.5";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(8.26, viewModel.FuelCarbon, 1e-12);
            Assert.Equal(15.5, viewModel.FuelHydrogen, 1e-12);
            Assert.Equal(
                Math.Round(GasPropertyModel.StoichiometricAirFuelRatio(8.26, 15.5, 0, 0), 2).ToString(System.Globalization.CultureInfo.CurrentCulture),
                ratio.Text);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A fractional composition is written and read back as given.</summary>
    [Fact]
    public void AFractionalCompositionRoundTrips()
    {
        var store = new EngineDefinitionStore();
        var definition = store.Read(Path.Combine(TestPaths.Samples, "Default.eng"));
        definition.FuelCarbon = 8.26;
        definition.FuelHydrogen = 15.5;

        var folder = Directory.CreateTempSubdirectory("esa-b96-").FullName;

        try
        {
            var path = Path.Combine(folder, "Fractional.eng");
            store.Write(path, definition);
            var back = store.Read(path);

            Assert.True(back.HasFuelComposition);
            Assert.Equal(8.26, back.FuelCarbon, 1e-12);
            Assert.Equal(15.5, back.FuelHydrogen, 1e-12);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void CorrectedRunsB96AndLegacyDoesNot()
    {
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.FuelFromFormula));
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.FuelFromFormula));
    }
}
