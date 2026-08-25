using App.Core;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// Guards the curve-fit coefficient tables and the numerical helpers the
/// thermodynamic model is built on.
/// </summary>
/// <remarks>
/// <para>
/// Every table was compared digit for digit against its Delphi declaration —
/// <c>ThermoLT</c>, <c>ThermoHT</c>, <c>FuelThermo</c>, <c>MolWt</c> and
/// <c>AirComposition</c> in GASPROPS.PAS, <c>KCoef</c> in Eqbm.pas. These tests pin
/// the shape, the padding convention and enough values at the edges and in the
/// middle that a transcription slip cannot pass unnoticed.
/// </para>
/// <para>
/// The tables are 1-based to match the Delphi subscripts, so row 0 and column 0 are
/// deliberately zero and are asserted as such.
/// </para>
/// </remarks>
public sealed class ThermoTablesTests
{
    [Fact]
    public void ThermodynamicFitsAreTwelveSpeciesBySevenCoefficients()
    {
        foreach (var table in (double[][,])[ThermoTables.LowTemperature, ThermoTables.HighTemperature])
        {
            // Twelve species and seven coefficients, plus the unused zero row and column.
            Assert.Equal(13, table.GetLength(0));
            Assert.Equal(8, table.GetLength(1));

            for (var i = 0; i < table.GetLength(0); i++)
            {
                Assert.Equal(0, table[i, 0]);
            }

            for (var j = 0; j < table.GetLength(1); j++)
            {
                Assert.Equal(0, table[0, j]);
            }
        }
    }

    [Fact]
    public void LowTemperatureFitMatchesTheDelphiDeclaration()
    {
        var t = ThermoTables.LowTemperature;

        // Species 1, H: a constant 2.5 fit with only the last two terms non-zero.
        Assert.Equal(0.25000000e+1, t[1, 1]);
        Assert.Equal(0.25471627e+5, t[1, 6]);
        Assert.Equal(-0.46011762, t[1, 7]);

        // Species 9, H2O, the middle of the table.
        Assert.Equal(0.40701275e+1, t[9, 1]);
        Assert.Equal(-0.30279722e+5, t[9, 6]);

        // Species 12, Ar, the last row.
        Assert.Equal(0.25000000e+1, t[12, 1]);
        Assert.Equal(-0.74537498e+3, t[12, 6]);
        Assert.Equal(0.43660006e+1, t[12, 7]);
    }

    [Fact]
    public void HighTemperatureFitDiffersFromTheLowTemperatureFit()
    {
        var low = ThermoTables.LowTemperature;
        var high = ThermoTables.HighTemperature;

        // The two fits agree on H's leading coefficient but not on its last, which is
        // where a copy-paste of one table over the other would show up.
        Assert.Equal(low[1, 1], high[1, 1]);
        Assert.Equal(-0.46011763, high[1, 7]);
        Assert.NotEqual(low[1, 7], high[1, 7]);

        Assert.Equal(0.44608041e+1, high[10, 1]);
        Assert.Equal(-0.74537502e+3, high[12, 6]);
    }

    [Fact]
    public void FuelFitsCoverSevenFuelTypesWithTheUserFuelAtZero()
    {
        var fuel = ThermoTables.Fuel;

        Assert.Equal(7, fuel.GetLength(0));
        Assert.Equal(8, fuel.GetLength(1));

        // Row 0 is the user-specified fuel, filled in at set-up time.
        for (var j = 0; j < fuel.GetLength(1); j++)
        {
            Assert.Equal(0, fuel[0, j]);
        }

        // Row 5 is the C7H17 gasoline the baseline engine runs on.
        Assert.Equal(4.0652, fuel[5, 1]);
        Assert.Equal(-3.5880e+4, fuel[5, 6]);

        // Row 6, diesel, is the last.
        Assert.Equal(7.9710, fuel[6, 1]);
        Assert.Equal(-1.7879, fuel[6, 7]);
    }

    [Fact]
    public void MolecularWeightsAreChemicallySane()
    {
        var w = ThermoTables.MolecularWeight;

        Assert.Equal(13, w.Length);
        Assert.Equal(0, w[0]);

        Assert.Equal(1.0080, w[(int)Species.H]);
        Assert.Equal(15.9994, w[(int)Species.O]);
        Assert.Equal(31.9988, w[(int)Species.O2]);
        Assert.Equal(18.0154, w[(int)Species.H2O]);
        Assert.Equal(44.0100, w[(int)Species.CO2]);
        Assert.Equal(28.0134, w[(int)Species.N2]);
        Assert.Equal(39.948, w[(int)Species.Ar]);

        // Diatomic species weigh twice their atom, give or take the fit's rounding.
        Assert.Equal(2 * w[(int)Species.O], w[(int)Species.O2], 3);
        Assert.Equal(2 * w[(int)Species.N], w[(int)Species.N2], 3);
    }

    [Fact]
    public void AirCompositionIsDryAirAndSumsToOne()
    {
        var air = ThermoTables.AirComposition;

        Assert.Equal(0.2096, air[(int)Species.O2]);
        Assert.Equal(0.7811, air[(int)Species.N2]);
        Assert.Equal(0.0093, air[(int)Species.Ar]);

        Assert.Equal(1.0, air.Sum(), 10);

        // Nothing else is present in dry air.
        foreach (var species in (Species[])[Species.H, Species.O, Species.N, Species.H2,
                                            Species.OH, Species.CO, Species.NO, Species.H2O, Species.CO2])
        {
            Assert.Equal(0, air[(int)species]);
        }
    }

    [Fact]
    public void EquilibriumCoefficientsKeepTheDummyRowsThatAlignTheNumbering()
    {
        var k = ThermoTables.EquilibriumCoefficients;

        // Ten reactions by five coefficients, 1-based.
        Assert.Equal(11, k.GetLength(0));
        Assert.Equal(6, k.GetLength(1));

        // Reactions 4, 6 and 8 are dummies that keep the row numbers lined up with
        // Olikara and Borman's scheme.
        foreach (var dummy in (int[])[4, 6, 8])
        {
            for (var j = 1; j <= 5; j++)
            {
                Assert.Equal(0, k[dummy, j]);
            }
        }

        // Reaction 1, half H2 to H.
        Assert.Equal(0.432168, k[1, 1]);
        Assert.Equal(0.242484e-2, k[1, 5]);

        // Reaction 10, CO plus half O2 to CO2, the last real row.
        Assert.Equal(-0.415302e-2, k[10, 1]);
        Assert.Equal(-0.900227e-2, k[10, 5]);
    }

    [Fact]
    public void UniversalGasConstantIsInJoulesPerKilomoleKelvin()
    {
        Assert.Equal(8314.41, ThermoTables.UniversalGasConstant);
    }
}
