using System.Globalization;
using App.Core.Model;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// The property model, checked against the <c>Gamma</c> column of the baseline trace —
/// the first quantity in phase 4 that the reference run exposes directly.
/// </summary>
public sealed class GasPropertyModelTests
{
    private const double Carbon = 7;
    private const double Hydrogen = 17;

    private static string? Baseline { get; } = FindBaseline();

    private static string? FindBaseline()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "baseline");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static (double Pressure, double Tu, double Tb, double Gamma) TraceRow(int crankAngle)
    {
        var lines = File.ReadAllLines(Path.Combine(Baseline!, "A2China.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(',').Select(h => h.Trim()).ToList();
        var row = lines.Skip(1)
            .First(l => (int)double.Parse(l.Split(',')[0], CultureInfo.InvariantCulture) == crankAngle)
            .Split(',');

        double Field(string name) => double.Parse(row[header.IndexOf(name)], CultureInfo.InvariantCulture);

        return (Field("PCyl"), Field("Tu"), Field("Tb"), Field("Gamma"));
    }

    private static GasPropertyModel Model(bool burned, double residualFraction = 1e-5)
    {
        var model = new GasPropertyModel(burned);
        model.Setup(0, Carbon, Hydrogen, 0, 0, 1.0, residualFraction);
        return model;
    }

    private static void RequireBaseline() =>
        Assert.SkipWhen(Baseline is null, "Not running from a repository checkout.");

    // ---------------------------------------------------------------------------
    // Against the reference run
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Combustion, expansion and exhaust, where the cylinder holds burnt products and
    /// the equilibrium branch applies.
    /// </summary>
    [Theory]
    [InlineData(60)]
    [InlineData(200)]
    [InlineData(300)]
    public void BurntGammaMatchesTheBaselineTrace(int crankAngle)
    {
        RequireBaseline();

        var (pressure, _, tb, expected) = TraceRow(crankAngle);
        var gamma = Model(burned: true).Gamma(pressure, tb);

        // The trace prints gamma to three decimals, so that is the resolution available.
        Assert.Equal(expected, gamma, 3);
    }

    /// <summary>
    /// Compression, where the charge is the unburnt fuel, air and residual mixture.
    /// </summary>
    /// <remarks>
    /// The baseline engine runs without forced recirculation, so the residual fraction
    /// sits at the 1e-5 floor the original clamps to. The model is called twice for the
    /// reason given in <see cref="TheFirstUnburntCallIsATransient"/>.
    /// </remarks>
    [Theory]
    [InlineData(-200)]
    [InlineData(-120)]
    [InlineData(-101)]
    public void UnburntGammaMatchesTheBaselineTrace(int crankAngle)
    {
        RequireBaseline();

        var (pressure, tu, _, expected) = TraceRow(crankAngle);

        var model = Model(burned: false);
        model.Gamma(pressure, tu);
        var gamma = model.Gamma(pressure, tu);

        Assert.Equal(expected, gamma, 3);
    }

    /// <summary>
    /// The first unburnt call gives a different answer from every call after it.
    /// </summary>
    /// <remarks>
    /// <c>FuelAirResConcs</c> takes the products' molecular weight from the mixture
    /// array it is about to overwrite. On the first call that array is still zero, which
    /// drives the residual mass fraction to one and yields the composition of pure
    /// residual. Reproduced as found; this test exists so the behaviour is not mistaken
    /// for a defect in the port.
    /// </remarks>
    [Fact]
    public void TheFirstUnburntCallIsATransient()
    {
        RequireBaseline();

        var (pressure, tu, _, _) = TraceRow(-200);
        var model = Model(burned: false);

        var first = model.Gamma(pressure, tu);
        var second = model.Gamma(pressure, tu);
        var third = model.Gamma(pressure, tu);

        // The first call is visibly different; after that the mixture converges by
        // successive substitution rather than settling exactly, so later calls agree
        // to about nine significant figures rather than to the last bit.
        Assert.NotEqual(first, second, 3);
        Assert.Equal(second, third, 7);
        Assert.True(
            Math.Abs(third - second) < Math.Abs(second - first),
            "Each call should move the mixture less than the one before it.");
    }

    // ---------------------------------------------------------------------------
    // Thermodynamic consistency
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InternalEnergyIsEnthalpyLessRT(bool burned)
    {
        const double P = 1_000_000;
        const double T = 1800;

        var model = Model(burned);

        // Each unburnt call advances the mixture by one substitution, so h, u and R
        // taken from three separate calls would otherwise come from three slightly
        // different compositions. Run it to convergence first.
        for (var i = 0; i < 20; i++)
        {
            model.Enthalpy(P, T);
        }

        var h = model.Enthalpy(P, T);
        var u = model.InternalEnergy(P, T);
        var r = model.GasConstant(P, T);

        // Relative, because these are of order 1e6 J/kg.
        Assert.Equal(1.0, u / (h - (r * T)), 9);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GammaIsCpOverCv(bool burned)
    {
        const double P = 1_000_000;
        const double T = 1800;

        var model = Model(burned);
        model.Gamma(P, T);

        var cp = model.SpecificHeatConstantPressure(P, T);
        var cv = model.SpecificHeatConstantVolume(P, T);

        Assert.Equal(1.0, model.Gamma(P, T) / (cp / cv), 9);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheGasConstantIsPhysicallySane(bool burned)
    {
        var model = Model(burned);
        model.GasConstant(1_000_000, 1500);

        // Combustion products and a fuel-air charge both sit near air's 287 J/(kg.K),
        // which is the universal constant over a molecular weight around 29.
        Assert.InRange(model.GasConstant(1_000_000, 1500), 240, 300);
    }

    /// <summary>
    /// <c>ReturnProps</c> reports an equilibrium <c>Cp</c> modestly above the frozen one,
    /// which is what dissociating combustion products should give.
    /// </summary>
    /// <remarks>
    /// This test used to assert the opposite. It pinned <c>Cp</c> at fifty to two hundred
    /// times the frozen value and called that faithful reproduction of a legacy defect
    /// (the retracted ISSUES.md B15). The inflation was the port's own - pascals passed
    /// where <c>go2</c> passes atmospheres - and it reached <c>Cp</c> and <c>DuDt</c>
    /// through <c>MixdhdT</c> and <c>MixdRdT</c>. <c>Gamma</c> was unaffected either way,
    /// because <c>Get_gamma</c> passes a zero derivative array, which is why it matched
    /// the baseline trace throughout and gave no warning. See ISSUES.md A7.
    /// </remarks>
    [Fact]
    public void ReturnPropsReportsAnEquilibriumSpecificHeatAboveTheFrozenOne()
    {
        var model = Model(burned: true);
        var properties = new GasProperties();

        model.ReturnProps(2_000_000, 2400, properties);

        Assert.InRange(properties.R, 240, 320);
        Assert.Equal(1.0, properties.U / (properties.H - (properties.R * 2400)), 9);

        // The frozen value is what physics expects of the mixture as composed.
        var frozen = model.SpecificHeatConstantPressure(2_000_000, 2400);
        Assert.InRange(frozen, 1300, 1600);

        // Allowing the composition to shift with temperature adds to it, because energy
        // goes into dissociation as well as into raising the temperature. A modest
        // multiple is right; the fifty to two hundred this once asserted was not.
        Assert.InRange(properties.Cp / frozen, 1.0, 3.0);
        Assert.True(properties.DuDt > 0, $"dudT came back as {properties.DuDt}.");

        // dudT is now the same order as the frozen specific heat, which is what the
        // burnt temperature equation needs: it divides by this.
        Assert.InRange(properties.DuDt, 500, 5000);

        // The pressure derivative comes from a finite difference of u, not the analytic
        // path, so it was never affected. See ISSUES.md B16.
        Assert.NotEqual(0, properties.DuDp);
    }

    [Fact]
    public void UnburntGasHasNoPressureOrCompositionDerivatives()
    {
        // Frozen reactions: the original sets both to zero outright.
        var model = Model(burned: false);
        var properties = new GasProperties();

        model.ReturnProps(1_000_000, 600, properties);

        Assert.Equal(0, properties.DuDp);
        Assert.Equal(0, properties.DuDf);
        Assert.True(properties.DuDt > 0);
    }

    [Fact]
    public void OnlyABurntModelOwnsAnEquilibriumSolver()
    {
        Assert.NotNull(Model(burned: true).Equilibrium);
        Assert.Null(Model(burned: false).Equilibrium);
    }

    // ---------------------------------------------------------------------------
    // Fuel selection
    // ---------------------------------------------------------------------------

    [Fact]
    public void AUserFuelBorrowsTheCurveFitOfWhicheverLibraryFuelItResembles()
    {
        // C7H17 has a hydrogen to carbon ratio of 2.43, above the 2.1 threshold, so it
        // takes the petrol fit rather than the diesel one. Library fuel 5 is that same
        // C7H17 gasoline, so the two should agree.
        var user = new GasPropertyModel(burned: false);
        user.Setup(0, Carbon, Hydrogen, 0, 0, 1.0, 1e-5);

        var library = new GasPropertyModel(burned: false);
        library.Setup(5, 0, 0, 0, 0, 1.0, 1e-5);

        user.Gamma(1_000_000, 600);
        library.Gamma(1_000_000, 600);

        Assert.Equal(library.Gamma(1_000_000, 600), user.Gamma(1_000_000, 600), 6);
    }

    [Fact]
    public void ChangingParametersLeavesNinetyNineAlone()
    {
        var model = Model(burned: true);
        model.Gamma(1_000_000, 2000);
        var before = model.Gamma(1_000_000, 2000);

        // Everything held; only the equivalence ratio moves.
        model.ChangeParameters(99, 99, 99, 99, 1.15, 99);

        Assert.NotEqual(before, model.Gamma(1_000_000, 2000), 4);
    }
}
