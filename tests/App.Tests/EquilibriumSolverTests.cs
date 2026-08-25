using App.Core;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// The twelve-species equilibrium model. Checked against physics rather than against
/// recorded output, because nothing in <c>data/baseline/</c> exposes species directly —
/// the trace's <c>Gamma</c> column is the first place this becomes observable, and that
/// needs the property model on top.
/// </summary>
public sealed class EquilibriumSolverTests
{
    // C7H17, the gasoline surrogate every shipped engine runs on.
    private const double Carbon = 7;
    private const double Hydrogen = 17;
    private const double Oxygen = 0;
    private const double Nitrogen = 0;

    private static EquilibriumSolver Solved(
        double equivalenceRatio = 1.0,
        double pressure = 1_000_000,
        double temperature = 2000)
    {
        var solver = new EquilibriumSolver();
        solver.Solve(equivalenceRatio, Carbon, Hydrogen, Oxygen, Nitrogen, pressure, temperature);
        return solver;
    }

    private static double Fraction(EquilibriumSolver s, Species species) => s.State.X[species];

    private static double Total(EquilibriumSolver s)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += s.State.X[i];
        }

        return total;
    }

    // ---------------------------------------------------------------------------
    // Conservation. These are what the four solved equations actually enforce.
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0.7, 1500)]
    [InlineData(1.0, 2000)]
    [InlineData(1.0, 2800)]
    [InlineData(1.2, 2400)]
    public void MoleFractionsSumToOne(double equivalenceRatio, double temperature)
    {
        var solver = Solved(equivalenceRatio, temperature: temperature);

        Assert.Equal(1.0, Total(solver), 6);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(1.2)]
    public void EverySpeciesIsNonNegative(double equivalenceRatio)
    {
        var solver = Solved(equivalenceRatio);

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            Assert.True(solver.State.X[i] >= 0, $"Species {i} came back as {solver.State.X[i]}.");
        }
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.0)]
    [InlineData(1.15)]
    public void AtomsAreConserved(double equivalenceRatio)
    {
        var solver = Solved(equivalenceRatio);
        var x = solver.State.X;

        // Carbon appears only as CO and CO2, so every other element is measured
        // relative to that pair, exactly as the solver's right-hand side does.
        var carbonBearing = x[Species.CO] + x[Species.CO2];

        var hydrogen = x[Species.H] + (2 * x[Species.H2]) + x[Species.OH] + (2 * x[Species.H2O]);
        var oxygenAtoms = x[Species.O] + x[Species.OH] + x[Species.CO] + x[Species.NO]
                          + (2 * x[Species.O2]) + x[Species.H2O] + (2 * x[Species.CO2]);
        var nitrogen = x[Species.N] + x[Species.NO] + (2 * x[Species.N2]);

        var stoichiometricOxygen = (Carbon + (Hydrogen / 4) - (Oxygen / 2)) / equivalenceRatio;

        Assert.Equal(Hydrogen / Carbon, hydrogen / carbonBearing, 6);
        Assert.Equal(2 * ((Oxygen / 2) + stoichiometricOxygen) / Carbon, oxygenAtoms / carbonBearing, 6);
        Assert.Equal(
            2 * ((Nitrogen / 2) + (3.7274 * stoichiometricOxygen)) / Carbon,
            nitrogen / carbonBearing,
            6);
    }

    // ---------------------------------------------------------------------------
    // Chemistry that has to come out right
    // ---------------------------------------------------------------------------

    [Fact]
    public void NitrogenDominatesAndTheMajorProductsAppear()
    {
        var solver = Solved();

        // Air is most of the charge, so N2 is most of the products.
        Assert.InRange(Fraction(solver, Species.N2), 0.6, 0.8);

        // Burning a hydrocarbon in air makes water and carbon dioxide.
        Assert.InRange(Fraction(solver, Species.H2O), 0.05, 0.20);
        Assert.InRange(Fraction(solver, Species.CO2), 0.03, 0.15);

        // Argon rides along untouched.
        Assert.True(Fraction(solver, Species.Ar) > 0);
    }

    [Fact]
    public void RichMixturesMakeCarbonMonoxideAndLeanOnesLeaveOxygen()
    {
        var lean = Solved(equivalenceRatio: 0.8);
        var rich = Solved(equivalenceRatio: 1.2);

        Assert.True(
            Fraction(rich, Species.CO) > Fraction(lean, Species.CO),
            "A rich mixture should leave more CO than a lean one.");

        Assert.True(
            Fraction(lean, Species.O2) > Fraction(rich, Species.O2),
            "A lean mixture should leave more O2 than a rich one.");
    }

    [Fact]
    public void DissociationIncreasesWithTemperature()
    {
        var cool = Solved(temperature: 1500);
        var hot = Solved(temperature: 3000);

        // The radicals are the signature of dissociation.
        foreach (var radical in (Species[])[Species.H, Species.O, Species.OH, Species.NO])
        {
            Assert.True(
                Fraction(hot, radical) > Fraction(cool, radical),
                $"{radical} should rise with temperature: {Fraction(cool, radical):E3} to {Fraction(hot, radical):E3}.");
        }
    }

    // ---------------------------------------------------------------------------
    // The derivatives, checked by finite difference
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The temperature derivatives are wrong by roughly 318, and that is the original's
    /// behaviour, not a porting slip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>go2</c> builds the equilibrium constants from pressure in <b>atmospheres</b>
    /// (<c>p := Pres/101325</c>), but <c>Partial_dxd</c> rebuilds their temperature
    /// derivatives from pressure in <b>pascals</b> (<c>p := Pres</c>). Every
    /// <c>dC/dT</c> is therefore off by <c>sqrt(101325)</c>, which is 318.3 — too large
    /// for C9 and C10, too small for C1 to C3 — and the resulting <c>dx/dT</c> lands
    /// between about 260 and 360 times the true derivative depending on which constants
    /// a species leans on.
    /// </para>
    /// <para>
    /// Confirmed by replicating the algorithm independently and reproducing the same
    /// ratios, so this pins a defect in the source rather than in the translation.
    /// Correcting the units would make these ratios collapse to 1 and fail this test,
    /// which is the point: <c>data/baseline/</c> was produced by the defective version.
    /// See ISSUES.md B15.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(Species.H2)]
    [InlineData(Species.CO)]
    [InlineData(Species.O2)]
    [InlineData(Species.N2)]
    public void TemperatureDerivativesCarryThePressureUnitDefect(Species species)
    {
        const double T = 2400;
        const double Step = 2;

        var analytic = Solved(temperature: T).State.DxDt[species];

        var up = Solved(temperature: T + Step).State.X[species];
        var down = Solved(temperature: T - Step).State.X[species];
        var numeric = (up - down) / (2 * Step);

        Assert.True(Math.Abs(numeric) > 1e-9, $"{species}: the finite difference is too small to compare against.");

        var ratio = analytic / numeric;

        // Same sign, but hundreds of times too large.
        Assert.InRange(ratio, 250, 380);
    }

    [Theory]
    [InlineData(Species.CO2)]
    [InlineData(Species.H2O)]
    [InlineData(Species.O2)]
    [InlineData(Species.CO)]
    public void PressureDerivativesMatchAFiniteDifference(Species species)
    {
        const double P = 1_000_000;
        const double Step = 500;

        var analytic = Solved(pressure: P).State.DxDp[species];

        var up = Solved(pressure: P + Step).State.X[species];
        var down = Solved(pressure: P - Step).State.X[species];
        var numeric = (up - down) / (2 * Step);

        var tolerance = Math.Max(Math.Abs(analytic) * 0.02, 1e-13);

        Assert.True(
            Math.Abs(analytic - numeric) <= tolerance,
            $"{species}: analytic {analytic:E6}, finite difference {numeric:E6}.");
    }

    // ---------------------------------------------------------------------------
    // Behaviour carried over from the original
    // ---------------------------------------------------------------------------

    [Fact]
    public void FreezingLeavesThePreviousCompositionAlone()
    {
        // The engine freezes the burnt zone during overlap.
        var solver = Solved();
        var before = Fraction(solver, Species.CO2);

        solver.Frozen = true;
        solver.Solve(1.0, Carbon, Hydrogen, Oxygen, Nitrogen, 500_000, 2500);

        Assert.Equal(before, Fraction(solver, Species.CO2));
        Assert.Equal(1, solver.Diagnostics.FrozenSkips);
    }

    [Fact]
    public void TemperaturesBelowAThousandArePinnedRatherThanExtrapolated()
    {
        var atLimit = Solved(temperature: 1000);
        var below = Solved(temperature: 700);

        Assert.Equal(Fraction(atLimit, Species.CO2), Fraction(below, Species.CO2), 12);
    }

    [Fact]
    public void TemperaturesAboveTheCurveFitAreFatal()
    {
        // KEquilib raises before it reaches its own clamp, so out of range is not
        // survivable. Nothing in the original catches it either.
        var error = Assert.Throws<EquilibriumException>(() => Solved(temperature: 4500));

        Assert.Contains("Out of Range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AZeroEquivalenceRatioIsClampedRatherThanDividingByZero()
    {
        var solver = Solved(equivalenceRatio: 0);

        Assert.Equal(1.0, Total(solver), 6);
        Assert.True(Fraction(solver, Species.O2) > 0);
    }

    [Fact]
    public void EnoughOxygenIsRequiredToAvoidSolidCarbon()
    {
        // r < 0.5n means there is not enough oxygen to take every carbon to CO.
        var error = Assert.Throws<EquilibriumException>(() => Solved(equivalenceRatio: 12));

        Assert.Contains("Solid Carbon", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Precision instrumentation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The convergence tests are absolute against variables spanning many orders of
    /// magnitude, so the iteration count is the quantity most exposed to the 80-bit
    /// Extended to double narrowing. This records where it currently sits.
    /// </summary>
    [Fact]
    public void ConvergenceIsComfortablyInsideTheIterationCaps()
    {
        var solver = new EquilibriumSolver();

        foreach (var er in (double[])[0.7, 0.9, 1.0, 1.1, 1.3])
        {
            foreach (var t in (double[])[1200, 1800, 2400, 3000, 3600])
            {
                solver.Solve(er, Carbon, Hydrogen, Oxygen, Nitrogen, 1_000_000, t);
            }
        }

        Assert.Equal(25, solver.Diagnostics.Solves);
        Assert.Equal(0, solver.Diagnostics.EquilibriumCapHits);
        Assert.Equal(0, solver.Diagnostics.InitialEstimateCapHits);

        // Well clear of the cap of 25. If this creeps up, precision is starting to bite.
        Assert.True(
            solver.Diagnostics.WorstEquilibriumIterations <= 10,
            $"Worst case took {solver.Diagnostics.WorstEquilibriumIterations} iterations.");
    }
}
