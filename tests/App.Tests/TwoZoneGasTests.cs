using System.Globalization;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// The two-zone gas model, checked against the zone columns of the baseline trace —
/// <c>Mb</c>, <c>Mu</c>, <c>Vb</c>, <c>Vu</c> and <c>Gamma</c> — and against the burn
/// profile that produces them.
/// </summary>
/// <remarks>
/// The baseline engine sparks at 21° BTDC and burns over 55°, so combustion runs from
/// crank angle −21 to +34. The trace records volumes in cm³ and masses in mg; the model
/// works in m³ and kg.
/// </remarks>
public sealed class TwoZoneGasTests
{
    private const double Carbon = 7;
    private const double Hydrogen = 17;

    /// <summary>Spark advance is 21° BTDC at 4000 rpm, and <c>ThetaSpark</c> negates it.</summary>
    private const double ThetaSpark = -21;

    private const double BurnAngle = 55;

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

    private static void RequireBaseline() =>
        Assert.SkipWhen(Baseline is null, "Not running from a repository checkout.");

    private sealed record TraceRow(
        double VCyl,
        double Pressure,
        double MCyl,
        double Mb,
        double Mu,
        double Vb,
        double Vu,
        double Tb,
        double Tu,
        double Gamma);

    private static TraceRow Row(int crankAngle)
    {
        var lines = File.ReadAllLines(Path.Combine(Baseline!, "A2China.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(',').Select(h => h.Trim()).ToList();
        var row = lines.Skip(1)
            .First(l => (int)double.Parse(l.Split(',')[0], CultureInfo.InvariantCulture) == crankAngle)
            .Split(',');

        double Field(string name) => double.Parse(row[header.IndexOf(name)], CultureInfo.InvariantCulture);

        return new TraceRow(
            Field("Vcyl"),
            Field("PCyl"),
            Field("Mcyl"),
            Field("Mb"),
            Field("Mu"),
            Field("Vb"),
            Field("Vu"),
            Field("Tb"),
            Field("Tu"),
            Field("Gamma"));
    }

    private static double Radians(double crankAngle) => crankAngle * Math.PI / 180;

    private static TwoZoneGas Cylinder(double massInMilligrams = 580.32)
    {
        var gas = new TwoZoneGas();

        gas.Burnt.Setup(0, Carbon, Hydrogen, 0, 0, 1.0, 1e-5);
        gas.Unburnt.Setup(0, Carbon, Hydrogen, 0, 0, 1.0, 1e-5);

        gas.State.ThetaSpark = ThetaSpark;
        gas.State.Fuel.BurnAngle = BurnAngle;
        gas.State.MGas = massInMilligrams / 1e6;

        return gas;
    }

    // ---------------------------------------------------------------------------
    // The burn profile
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Before the spark nothing has burnt, after the burn angle everything has, and in
    /// between the raised cosine is symmetric about its midpoint.
    /// </summary>
    [Fact]
    public void BurntFractionFollowsTheRaisedCosine()
    {
        var gas = Cylinder();

        Assert.Equal(0, gas.BurntFraction(Radians(-30)));
        Assert.Equal(0, gas.BurntFraction(Radians(ThetaSpark)));
        Assert.Equal(1, gas.BurntFraction(Radians(ThetaSpark + BurnAngle)));
        Assert.Equal(1, gas.BurntFraction(Radians(90)));

        // Half burnt at the midpoint, and symmetric either side of it.
        Assert.Equal(0.5, gas.BurntFraction(Radians(ThetaSpark + (BurnAngle / 2))), 12);
        Assert.Equal(
            1.0,
            gas.BurntFraction(Radians(ThetaSpark + 10)) + gas.BurntFraction(Radians(ThetaSpark + BurnAngle - 10)),
            12);
    }

    /// <summary>
    /// <c>dxdTheta</c> is the derivative of <c>xburnt</c> per <b>radian</b>, even though
    /// both compare their argument in degrees.
    /// </summary>
    [Fact]
    public void BurnRateIsTheDerivativeOfBurntFractionPerRadian()
    {
        var gas = Cylinder();
        const double step = 1e-6;

        foreach (var crankAngle in new double[] { -15, 0, 15, 30 })
        {
            var difference =
                (gas.BurntFraction(Radians(crankAngle) + step) - gas.BurntFraction(Radians(crankAngle) - step))
                / (2 * step);

            Assert.Equal(difference, gas.BurnRate(Radians(crankAngle)), 6);
        }

        Assert.Equal(0, gas.BurnRate(Radians(-30)));
        Assert.Equal(0, gas.BurnRate(Radians(90)));
    }

    // ---------------------------------------------------------------------------
    // Against the reference run
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The trace's <c>Mb</c> column is the burnt fraction times the charge mass, so it
    /// pins the burn profile and the 1 per cent clamp at once. Crank angles −21 to −18
    /// sit below the clamp and all report the same 5.80 mg — one per cent of the charge —
    /// which is the clamp, not the cosine.
    /// </summary>
    [Theory]
    [InlineData(-21)]
    [InlineData(-19)]
    [InlineData(-18)]
    [InlineData(-17)]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public void ClampedBurntFractionMatchesTheTraceBurntMass(int crankAngle)
    {
        RequireBaseline();

        var row = Row(crankAngle);
        var gas = Cylinder(row.MCyl);

        var fraction = gas.BurntFraction(Radians(crankAngle));
        fraction = Math.Max(fraction, 0.01);
        fraction = 1 - fraction < 0.01 ? 0.99 : fraction;

        // Both Mb and Mcyl are printed to two decimal places, so 0.01 mg covers the
        // rounding in the pair.
        Assert.Equal(row.Mb / 1e6, fraction * row.MCyl / 1e6, 1e-8);
    }

    /// <summary>
    /// Feeding a combustion row of the trace back into <c>UpdateB</c> reproduces the
    /// zone masses and the mixture gamma the original recorded.
    /// </summary>
    [Theory]
    [InlineData(-17)]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    public void UpdateBReproducesTheTraceZoneSplit(int crankAngle)
    {
        RequireBaseline();

        var row = Row(crankAngle);
        var gas = Cylinder(row.MCyl);

        // Prime the unburnt composition: FuelAirResConcs is a successive substitution
        // whose first call sees a zero mixture, so the real solver has always called it
        // several times before the values settle. See ISSUES.md B18.
        for (var i = 0; i < 4; i++)
        {
            gas.UpdateB(
                Radians(crankAngle),
                row.VCyl / 1e6,
                0,
                row.Vb / 1e6,
                row.Pressure,
                row.Tb,
                row.Tu);
        }

        var state = gas.State;

        // 1 per cent on masses, per the phase 4 tolerance policy.
        Assert.Equal(row.Mb / 1e6, state.Mb, Math.Abs(row.Mb / 1e6 * 0.01) + 5e-12);
        Assert.Equal(row.Mu / 1e6, state.Mu, Math.Abs(row.Mu / 1e6 * 0.01) + 5e-12);

        // The trace prints gamma to three decimal places.
        Assert.Equal(row.Gamma, state.Gamma, 0.0005 + (row.Gamma * 0.001));
    }

    /// <summary>
    /// Through the whole burn the trace shows <c>Vb</c> equal to <c>Vcyl</c> and
    /// <c>Vu</c> at zero, which is the "iffy line" clamp firing on every step rather
    /// than an artefact of one crank angle. The clamp is load-bearing, not decorative.
    /// </summary>
    [Fact]
    public void TheTraceShowsTheBurntVolumeClampFiringThroughoutCombustion()
    {
        RequireBaseline();

        for (var crankAngle = -20; crankAngle <= 30; crankAngle++)
        {
            var row = Row(crankAngle);

            Assert.Equal(row.VCyl, row.Vb, 0.005);
            Assert.Equal(0, row.Vu, 0.005);
        }
    }

    // ---------------------------------------------------------------------------
    // The bookkeeping each state performs
    // ---------------------------------------------------------------------------

    /// <summary>
    /// <c>if Vb &gt; Vgas then Vb := Vgas</c>, and <c>Vu</c> is what is left over.
    /// </summary>
    [Fact]
    public void UpdateBClampsTheBurntVolumeToTheCylinderVolume()
    {
        var gas = Cylinder();

        gas.UpdateB(Radians(0), 60e-6, 0, 90e-6, 2e6, 2800, 800);

        Assert.Equal(60e-6, gas.State.Vb);
        Assert.Equal(0, gas.State.Vu);

        gas.UpdateB(Radians(0), 60e-6, 0, 20e-6, 2e6, 2800, 800);

        Assert.Equal(20e-6, gas.State.Vb);
        Assert.Equal(40e-6, gas.State.Vu, 1e-15);
    }

    /// <summary>
    /// Compression and intake: one unburnt zone holding the whole charge, with the
    /// burnt-zone temperature shadowing the unburnt one.
    /// </summary>
    [Fact]
    public void UpdateUBPutsEverythingInTheUnburntZone()
    {
        var gas = Cylinder();

        gas.UpdateUB(60e-6, 1e-6, 999, 2e5, 700);

        Assert.Equal(0, gas.State.Vb);
        Assert.Equal(60e-6, gas.State.Vu);
        Assert.Equal(0, gas.State.Mb);
        Assert.Equal(gas.State.MGas, gas.State.Mu);
        Assert.Equal(700, gas.State.Tb);
        Assert.Equal(700, gas.State.Tu);
        Assert.Equal(0, gas.State.DmbDTheta);
        Assert.Equal(gas.State.Uu, gas.State.UGas);
        Assert.Equal(gas.State.Ru, gas.State.RGas);
        Assert.Equal(gas.State.Hu, gas.State.HGas);
    }

    /// <summary>
    /// Expansion and blowdown: one burnt zone holding the whole charge.
    /// </summary>
    [Fact]
    public void UpdateBDPutsEverythingInTheBurntZone()
    {
        var gas = Cylinder();

        gas.UpdateBD(60e-6, 1e-6, 999, 2e6, 2400);

        Assert.Equal(60e-6, gas.State.Vb);
        Assert.Equal(0, gas.State.Vu);
        Assert.Equal(gas.State.MGas, gas.State.Mb);
        Assert.Equal(0, gas.State.Mu);
        Assert.Equal(2400, gas.State.Tb);
        Assert.Equal(2400, gas.State.Tu);
        Assert.Equal(gas.State.Ub, gas.State.UGas);
        Assert.Equal(gas.State.Rb, gas.State.RGas);
        Assert.Equal(gas.State.Hb, gas.State.HGas);
    }

    /// <summary>
    /// Overlap: the unburnt model over the whole volume, with both zone volumes left at
    /// zero while <c>Vgas</c> carries the total. Reproduced as found — see ISSUES.md B29.
    /// </summary>
    [Fact]
    public void UpdateGEZeroesBothZoneVolumesAndIgnoresTheBurntTemperature()
    {
        var gas = Cylinder();

        gas.UpdateGE(400e-6, 1e-6, 999, 1.0e5, 2400, 900);

        Assert.Equal(400e-6, gas.State.VGas);
        Assert.Equal(0, gas.State.Vb);
        Assert.Equal(0, gas.State.Vu);
        Assert.Equal(900, gas.State.Tb);
        Assert.Equal(900, gas.State.Tu);
        Assert.Equal(gas.State.Uu, gas.State.UGas);
    }

    /// <summary>
    /// <c>Tgas</c> is a function that writes: it recomputes <c>xb</c> from the zone
    /// masses before weighting the temperatures. See ISSUES.md B27.
    /// </summary>
    [Fact]
    public void GasTemperatureRecomputesTheBurntFractionAsASideEffect()
    {
        var gas = Cylinder();

        gas.State.Mb = gas.State.MGas / 4;
        gas.State.Mu = gas.State.MGas * 3 / 4;
        gas.State.Tb = 2400;
        gas.State.Tu = 800;
        gas.State.Xb = 0.99;

        Assert.Equal(1200, gas.GasTemperature(), 1e-9);
        Assert.Equal(0.25, gas.State.Xb, 1e-12);

        gas.State.Mb = 0;
        gas.State.Xb = 0.5;

        Assert.Equal(800, gas.GasTemperature(), 1e-9);
        Assert.Equal(0, gas.State.Xb);
    }
}
