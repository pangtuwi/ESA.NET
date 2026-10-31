using App.Core;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// ISSUES.md B95: the fuel is Heywood's gasoline, C8.26H15.5 with its own vapour fit (Heywood
/// 1988, Table 4.11), burnt at the declared air-fuel ratio and heating value. The original's
/// C7H17 surrogate has too much hydrogen for a gasoline, so its chemistry burnt a stoichiometric
/// ratio of 15.4 and a heating value of 44.9 MJ/kg while the performance figures counted the
/// declared 14.5 and 43.
/// </summary>
public sealed class GasolineFuelTests
{
    private const double Declared = 14.5;

    private static GasPropertyModel Unburnt(int fuelType, double equivalenceRatio = 1, double residual = 0.05)
    {
        var gas = new GasPropertyModel(burned: false);
        gas.Setup(fuelType, 7, 17, 0, 0, equivalenceRatio, residual);
        return gas;
    }

    private static double ReferenceEnthalpy(Species species)
    {
        const double T = GasPropertyModel.HeatingValueTemperature;
        var a = ThermoTables.LowTemperature;
        var i = (int)species;

        return (a[i, 1] + (a[i, 2] * T / 2) + (a[i, 3] * T * T / 3) + (a[i, 4] * T * T * T / 4)
                + (a[i, 5] * T * T * T * T / 5) + (a[i, 6] / T)) * ThermoTables.UniversalGasConstant * T / 1e6;
    }

    /// <summary>
    /// The heating values below are built on the species fits, which at 298.15 K give carbon
    /// dioxide and water vapour Heywood's enthalpies of formation (Tables 3.2 and 4.8: -393.5 and
    /// -241.8 MJ/kmol) and oxygen zero, its reference state.
    /// </summary>
    [Fact]
    public void TheSpeciesFitsGiveHeywoodsEnthalpiesOfFormation()
    {
        Assert.Equal(-393.52, ReferenceEnthalpy(Species.CO2), 0.05);
        Assert.Equal(-241.83, ReferenceEnthalpy(Species.H2O), 0.05);
        Assert.Equal(0, ReferenceEnthalpy(Species.O2), 0.05);
    }

    /// <summary>
    /// The oracle for the fuels: each one's stoichiometric ratio is Heywood's eq. 3.6,
    /// <c>34.56 (4 + y) / (12.011 + 1.008 y)</c> with <c>y</c> its H/C ratio, to the 0.1 per cent
    /// by which the model's air (with its argon) differs from Heywood's; Heywood's gasoline's is
    /// his Table 4.11's 14.64 to 0.3 per cent. Its heating value, 43.66 MJ/kg, is its Table 4.11
    /// formation enthalpy, -112.7 MJ/kmol, burnt to carbon dioxide and water vapour; the
    /// surrogate's is 44.88, a paraffin's.
    /// </summary>
    [Theory]
    [InlineData(0, 7, 17, 15.37, 44.884)]
    [InlineData(GasPropertyModel.HeywoodGasoline, 8.26, 15.5, 14.61, 43.656)]
    public void EachFuelHasItsStoichiometryAndHeatingValue(int fuelType, double carbon, double hydrogen, double ratio, double megajoules)
    {
        var gas = Unburnt(fuelType);
        var y = hydrogen / carbon;

        Assert.Equal(34.56 * (4 + y) / (12.011 + (1.008 * y)), gas.StoichiometricAirFuelRatio(), 0.001 * ratio);
        Assert.Equal(ratio, gas.StoichiometricAirFuelRatio(), 0.002 * ratio);
        Assert.Equal(megajoules * 1e6, gas.TableHeatingValue(), 0.005e6);

        if (fuelType == GasPropertyModel.HeywoodGasoline)
        {
            Assert.Equal(14.64, gas.StoichiometricAirFuelRatio(), 0.003 * 14.64);
        }
    }

    /// <summary>
    /// Heywood's gasoline's vapour specific heat at 300 K is his eq. 4.42 from Table 4.11,
    /// 42.96 cal/(gmol K), 1.566 kJ/(kg K), against the surrogate's 1.70.
    /// </summary>
    [Fact]
    public void HeywoodsGasolineHasItsOwnSpecificHeat()
    {
        var a = ThermoTables.HeywoodGasoline;
        const double K = 0.3;
        var calories = a[0] + (a[1] * K) + (a[2] * K * K) + (a[3] * K * K * K) + (a[4] / (K * K));

        Assert.Equal(42.96, calories, 0.01);
        Assert.Equal(1.566, calories * 4.184 / ThermoTables.HeywoodGasolineMolecularWeight, 0.001);
    }

    /// <summary>
    /// The oracle for the mixture: burnt at <see cref="FuelMixture.EquivalenceRatio"/>, the
    /// chemistry's fuel is the declared mixture's, <c>1 / (1 + lambda AFRatio)</c> of the fresh
    /// charge by mass, which is also what <see cref="FuelMixture.FuelMass"/> counts under B95.
    /// At the original's <c>1/lambda</c> the surrogate's chemistry burnt 5.6 per cent less.
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.91)]
    [InlineData(1.1)]
    public void TheChemistryBurnsTheFuelTheBookkeepingCounts(double lambda)
    {
        var fuel = new Fuel { Lambda = lambda, AFRatio = Declared };
        var probe = Unburnt(GasPropertyModel.HeywoodGasoline);
        var equivalenceRatio = FuelMixture.EquivalenceRatio(fuel, probe.StoichiometricAirFuelRatio());

        // The model floors the residual at 1e-5, small enough not to show.
        var gas = Unburnt(GasPropertyModel.HeywoodGasoline, equivalenceRatio, residual: 0);

        // The fuel's mole fraction is set when the mixture is first evaluated.
        gas.Enthalpy(101325, 400);
        var mixtureMolecularWeight = ThermoTables.UniversalGasConstant / gas.GasConstant(101325, 400);
        var fuelMassFraction = gas.FuelMoleFraction * ThermoTables.HeywoodGasolineMolecularWeight / mixtureMolecularWeight;

        var counted = FuelMixture.FuelMass(fuel, 1, exact: true);
        Assert.Equal(1 / (1 + (lambda * Declared)), counted, 1e-12);
        Assert.Equal(counted, fuelMassFraction, 2e-4 * counted);
    }

    [Fact]
    public void TheOriginalsFuelMassIsTheExactOneAtLambdaOne()
    {
        var stoichiometric = new Fuel { Lambda = 1, AFRatio = Declared };
        Assert.Equal(FuelMixture.FuelMass(stoichiometric, 1, exact: false), FuelMixture.FuelMass(stoichiometric, 1, exact: true), 1e-15);

        var rich = new Fuel { Lambda = 0.91, AFRatio = Declared };
        Assert.InRange(FuelMixture.FuelMass(rich, 1, exact: false) / FuelMixture.FuelMass(rich, 1, exact: true), 1.005, 1.008);
    }

    /// <summary>
    /// Matched to the declared heating value, the fit releases exactly that, and only the fuel's
    /// chemical energy has moved: the specific heat is unchanged.
    /// </summary>
    [Theory]
    [InlineData(43e6)]
    [InlineData(44e6)]
    public void MatchedTheFuelReleasesTheDeclaredHeatingValue(double declared)
    {
        var gas = Unburnt(GasPropertyModel.HeywoodGasoline);

        // The unburnt model's residual iteration settles on its third evaluation.
        gas.Enthalpy(101325, 500);
        gas.Enthalpy(101325, 500);
        var specificHeat = gas.SpecificHeatConstantPressure(101325, 500);

        gas.MatchHeatingValue(declared);

        Assert.Equal(declared, gas.TableHeatingValue(), 1e-6 * declared);
        // To the last digits the residual iteration is still settling in, 2e-9.
        Assert.Equal(specificHeat, gas.SpecificHeatConstantPressure(101325, 500), 1e-8 * specificHeat);
    }

    [Fact]
    public void CorrectedRunsB95AndLegacyDoesNot()
    {
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.GasolineFuel));
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.GasolineFuel));
    }
}
