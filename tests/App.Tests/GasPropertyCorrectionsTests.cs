using App.Core.Model;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// ISSUES.md B16-B18: the burnt model's pressure derivative and the unburnt model's
/// residual fraction, each checked against what the quantity has to be rather than
/// against the original.
/// </summary>
public sealed class GasPropertyCorrectionsTests
{
    private static GasPropertyModel Model(bool burned, double equivalenceRatio, double residualFraction)
    {
        var model = new GasPropertyModel(burned);
        model.Setup(0, 7, 17, 0, 0, equivalenceRatio, residualFraction);

        return model;
    }

    /// <summary>
    /// B16's oracle. <c>dudp</c> is the derivative of the equilibrium internal energy with
    /// respect to pressure, so the analytic value from the solver's own <c>dxdp</c> has to
    /// agree with a converged numerical derivative of <c>u</c>: a central difference
    /// Richardson-extrapolated over two bands. The solver's <c>dxdp</c> is per atmosphere;
    /// before it is divided by 101325 the two are five orders of magnitude apart, which is
    /// why the original abandoned the analytic form for a difference (with B17 on top).
    /// </summary>
    /// <remarks>
    /// The two are compared as the cylinder equations use them. Where dissociation is
    /// slight, <c>dudp</c> is tiny and the solver's convergence tolerance, about 1e-9
    /// J/(kg.Pa), is a large share of it - 1e-4 relative at 1500 K and 60 bar on the rich
    /// side - so the error is carried across the whole pressure, <c>p dudp</c>, and
    /// expressed as the temperature change in <c>u</c> it would stand for.
    /// </remarks>
    [Theory]
    [InlineData(0.9)]
    [InlineData(1.0)]
    [InlineData(1.15)]
    public void TheAnalyticDudpIsTheDerivativeOfTheEquilibriumInternalEnergy(double equivalenceRatio)
    {
        var model = Model(burned: true, equivalenceRatio, residualFraction: 0);
        model.AnalyticPressureDerivative = true;
        var worst = 0.0;

        foreach (var temperature in new[] { 1500.0, 2000.0, 2500.0, 2800.0 })
        {
            foreach (var pressure in new[] { 5e5, 2e6, 6e6 })
            {
                var properties = new GasProperties();
                model.ReturnProps(pressure, temperature, properties);

                double Difference(double band) =>
                    (model.InternalEnergy(pressure + band, temperature)
                     - model.InternalEnergy(pressure - band, temperature)) / (2 * band);

                var converged = ((4 * Difference(1e-3 * pressure)) - Difference(2e-3 * pressure)) / 3;

                Assert.True(properties.DuDp < 0, "Dissociation rises as pressure falls, so u does too.");
                worst = Math.Max(worst, Math.Abs(properties.DuDp - converged) * pressure / properties.DuDt);
            }
        }

        // Kelvin. Measured at 4.3e-6, stoichiometric at 1500 K and 5 bar.
        Assert.InRange(worst, 0, 1e-4);
    }

    /// <summary>
    /// The correction computes from the solve <c>ReturnProps</c> already made, so the rest
    /// of what it returns is untouched: only <c>dudp</c> can move.
    /// </summary>
    [Fact]
    public void TheAnalyticDudpLeavesEveryOtherPropertyAlone()
    {
        var legacy = new GasProperties();
        var corrected = new GasProperties();

        Model(burned: true, 1.0, 0).ReturnProps(4e6, 2600, legacy);

        var model = Model(burned: true, 1.0, 0);
        model.AnalyticPressureDerivative = true;
        model.ReturnProps(4e6, 2600, corrected);

        Assert.Equal(legacy.R, corrected.R);
        Assert.Equal(legacy.U, corrected.U);
        Assert.Equal(legacy.Cp, corrected.Cp);
        Assert.Equal(legacy.DuDt, corrected.DuDt);
        Assert.Equal(legacy.DuDf, corrected.DuDf);
        Assert.Equal(legacy.DuDp, corrected.DuDp, 1e-6 * Math.Abs(legacy.DuDp));
    }

    /// <summary>
    /// The residual mass fraction of the charge the unburnt model produced: the residual's
    /// moles times its molecular weight, over the molecular weight of the mixture as its
    /// returned gas constant gives it.
    /// </summary>
    private static double ResidualMassFraction(GasPropertyModel model, GasProperties properties) =>
        model.ResidualMoleFraction * model.ResidualOnlyMolecularWeight
        / (ThermoTables.UniversalGasConstant / properties.R);

    /// <summary>
    /// B18's oracle. The unburnt model is asked for a residual mass fraction <c>f</c>, and
    /// the charge it builds has to carry exactly that, on the first call as on every other.
    /// Ferguson's closed form needs the residual's molecular weight to convert <c>f</c> to
    /// a mole fraction.
    /// </summary>
    [Theory]
    [InlineData(0.9, 0.02)]
    [InlineData(1.0, 0.08)]
    [InlineData(1.2, 0.2)]
    public void TheChargeCarriesTheResidualMassFractionItWasAskedFor(double equivalenceRatio, double f)
    {
        var model = Model(burned: false, equivalenceRatio, f);
        model.ResidualMolecularWeight = true;

        var properties = new GasProperties();
        model.ReturnProps(1e5, 320, properties);

        Assert.Equal(f, ResidualMassFraction(model, properties), 1e-12);
    }

    /// <summary>
    /// B18, the call history. Corrected, the first evaluation of a state is the same as
    /// every later one, to the last bit. The original's first call takes the products'
    /// molecular weight from a mixture array still at zero, which makes the charge pure
    /// residual; from then on it reuses its own last answer, and settles on a value that
    /// misses <c>f</c>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheFirstEvaluationOfTheChargeIsTheSameAsEveryOther(bool corrected)
    {
        const double f = 0.08;
        var model = Model(burned: false, 1.0, f);
        model.ResidualMolecularWeight = corrected;

        var history = new List<(double R, double U, double MoleFraction)>();

        for (var call = 1; call <= 10; call++)
        {
            var properties = new GasProperties();
            model.ReturnProps(1e5, 320, properties);
            history.Add((properties.R, properties.U, model.ResidualMoleFraction));

            if (call == 10)
            {
                var settled = ResidualMassFraction(model, properties);

                if (corrected)
                {
                    Assert.Equal(f, settled, 1e-12);
                }
                else
                {
                    // Measured at 0.080360 against 0.08 asked for.
                    Assert.True(Math.Abs(settled - f) > 1e-4, $"{settled}");
                }
            }
        }

        if (corrected)
        {
            Assert.Equal(history[0], history[1]);
            Assert.Equal(history[0], history[9]);
        }
        else
        {
            Assert.Equal(1, history[0].MoleFraction);
            Assert.NotEqual(history[0].R, history[9].R);
        }
    }
}
