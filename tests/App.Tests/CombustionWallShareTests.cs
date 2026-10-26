using App.Core;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// ISSUES.md B34: during combustion each zone loses heat through its volume share of
/// every surface. Two-zone heat-release models neglect heat exchange between the zones and
/// usually weight the burnt and unburnt zones' share of the whole chamber's wall by their
/// volumes (Catania, Misul, Mittica and Spessa, "A Refined Two-Zone Heat Release Model for
/// Combustion Analysis in SI Engines", COMODIA 2001). The original gives the burnt zone
/// piston and head only and the unburnt zone the whole liner.
/// </summary>
public sealed class CombustionWallShareTests
{
    private const double Rpm = 4000;

    private static readonly CylinderGeometry Geometry = new(
        bore: 0.081, stroke: 0.0774, compressionRatio: 9.2, conrodLength: 0.149, cylinderCount: 4);

    private static CylinderModel Model(bool share)
    {
        // A2China.cwt, verbatim.
        var walls = new WallTemperatureTable();
        walls.Rpm.AddRange([1000, 2000, 3000, 4000, 5000, 6000, 7000]);
        walls.HeadTemperature.AddRange([350, 365, 380, 400, 415, 435, 460]);
        walls.PistonTemperature.AddRange([440, 450, 470, 490, 510, 530, 550]);
        walls.UpperLinerTemperature.AddRange([495, 405, 425, 445, 460, 480, 505]);
        walls.LowerLinerTemperature.AddRange([350, 365, 380, 400, 415, 435, 460]);

        return new CylinderModel(Geometry, new TwoZoneGas(), new TwoZoneGas(), walls)
        {
            Rpm = Rpm,
            CrankAngularVelocity = Rpm * Math.PI / 30,
            WoschniCoefficient = 150,
            PressureAtInletValveClosing = 99000,
            TemperatureAtInletValveClosing = 298.15,
            VolumeAtInletValveClosing = Geometry.Volume(-100 * Math.PI / 180),
            CombustionWallShare = share,
        };
    }

    /// <summary>A burning cylinder 10 degrees after top dead centre.</summary>
    private static CylinderModel Burning(bool share, double burntShare, double tb, double tu)
    {
        var model = Model(share);
        var angle = 10 * Math.PI / 180;
        var gas = model.Cylinder.State;

        gas.PGas = 5.0e6;
        gas.VGas = Geometry.Volume(angle);
        gas.Vb = burntShare * gas.VGas;
        gas.Vu = gas.VGas - gas.Vb;
        gas.Tb = tb;
        gas.Tu = tu;

        model.State = EngineState.Combustion;
        model.CrankAngleRadians = angle;

        return model;
    }

    /// <summary>
    /// The oracle: with both zones at one temperature, the two zones together lose exactly
    /// what one gas at that temperature loses through the whole surface, however the volume
    /// divides; the wall is counted once. The original passes this too (see below), so it is
    /// the per-zone shares that tell the two apart.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void AtOneTemperatureTheZonesLoseWhatOneGasLosesThroughTheWholeSurface(double burntShare)
    {
        var model = Burning(share: true, burntShare, tb: 1800, tu: 1800);
        var angle = model.CrankAngleRadians;

        var zones = model.BurntHeatLossRate(angle) + model.UnburntHeatLossRate(angle);

        Assert.Equal(model.SingleZoneHeatLossRate(angle), zones, 1e-9 * Math.Abs(zones));
    }

    /// <summary>Each zone's loss is its volume share of a whole-surface loss at its own temperature.</summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.7)]
    public void EachZoneLosesItsVolumeShareOfTheWholeSurface(double burntShare)
    {
        var model = Burning(share: true, burntShare, tb: 2500, tu: 750);
        var angle = model.CrankAngleRadians;
        var gas = model.Cylinder.State;

        // SingleZoneHeatLossRate is the whole surface at Tb; set Tb to each zone's
        // temperature in turn to get each zone's whole-surface loss.
        var burntWhole = model.SingleZoneHeatLossRate(angle);
        gas.Tb = 750;
        var unburntWhole = model.SingleZoneHeatLossRate(angle);
        gas.Tb = 2500;

        Assert.Equal(burntShare * burntWhole, model.BurntHeatLossRate(angle), 1e-9 * Math.Abs(burntWhole));
        Assert.Equal((1 - burntShare) * unburntWhole, model.UnburntHeatLossRate(angle), 1e-9 * Math.Abs(unburntWhole));
    }

    /// <summary>
    /// Off, the original. At one temperature its zones also make the whole surface, since
    /// the piston and head are shared by volume and the liner goes whole to the unburnt
    /// zone; what differs is that the liner is always charged at the unburnt temperature.
    /// With a hot burnt zone the original loses less, the burnt zone less and the unburnt
    /// zone more than their shares.
    /// </summary>
    [Fact]
    public void WithoutB34TheLinerSeesOnlyTheUnburntTemperature()
    {
        var angle = Burning(share: true, burntShare: 0.6, tb: 1800, tu: 1800).CrankAngleRadians;
        var oneOn = Burning(share: true, burntShare: 0.6, tb: 1800, tu: 1800);
        var oneOff = Burning(share: false, burntShare: 0.6, tb: 1800, tu: 1800);
        var whole = oneOn.SingleZoneHeatLossRate(angle);

        Assert.Equal(whole, oneOff.BurntHeatLossRate(angle) + oneOff.UnburntHeatLossRate(angle), 1e-9 * Math.Abs(whole));

        var on = Burning(share: true, burntShare: 0.6, tb: 2500, tu: 750);
        var off = Burning(share: false, burntShare: 0.6, tb: 2500, tu: 750);

        // Losses are negative: a smaller magnitude is less heat lost.
        Assert.True(Math.Abs(off.BurntHeatLossRate(angle)) < Math.Abs(on.BurntHeatLossRate(angle)));
        Assert.True(Math.Abs(off.UnburntHeatLossRate(angle)) > Math.Abs(on.UnburntHeatLossRate(angle)));
        Assert.True(
            Math.Abs(off.BurntHeatLossRate(angle) + off.UnburntHeatLossRate(angle))
            < Math.Abs(on.BurntHeatLossRate(angle) + on.UnburntHeatLossRate(angle)));
    }

    /// <summary>Outside combustion B34 changes nothing.</summary>
    [Theory]
    [InlineData(EngineState.Compression)]
    [InlineData(EngineState.Expansion)]
    [InlineData(EngineState.Exhaust)]
    public void OutsideCombustionNothingMoves(EngineState state)
    {
        var on = Burning(share: true, burntShare: 0.6, tb: 2200, tu: 800);
        var off = Burning(share: false, burntShare: 0.6, tb: 2200, tu: 800);
        on.State = state;
        off.State = state;
        var angle = on.CrankAngleRadians;

        Assert.Equal(off.BurntHeatLossRate(angle), on.BurntHeatLossRate(angle));
        Assert.Equal(off.UnburntHeatLossRate(angle), on.UnburntHeatLossRate(angle));
    }
}
