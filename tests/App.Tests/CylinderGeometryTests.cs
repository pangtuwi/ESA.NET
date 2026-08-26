using System.Globalization;
using App.Core.Simulation;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// Geometry is the one layer of the simulation that can be checked outright: the
/// baseline trace prints <c>Vcyl</c> at every one of 720 crank angles, and it depends on
/// nothing but bore, stroke, compression ratio and conrod length.
/// </summary>
public sealed class CylinderGeometryTests
{
    private static CylinderGeometry Baseline()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new App.Persistence.Tables.CamProfileReader(),
            new App.Persistence.Tables.SpeedKeyedTableReader(),
            new App.Persistence.Tables.WallTemperatureTableReader(),
            new App.Persistence.Tables.ExhaustBackPressureTableReader(),
            new App.Persistence.Tables.ManifoldAreaTableStore(),
            new App.Persistence.Tables.DischargeCoefficientTableStore());

        return CylinderGeometry.FromEngine(loader.Load(BaselinePaths.File("A2China.eng")).Engine);
    }

    [Fact]
    public void CylinderVolumeMatchesTheBaselineTraceAtEveryCrankAngle()
    {
        BaselinePaths.Require();

        var geometry = Baseline();
        var worst = 0.0;
        var worstAngle = 0.0;

        foreach (var (crankAngle, expected) in BaselinePaths.TraceColumn("Vcyl"))
        {
            // The trace writes cubic centimetres to two decimal places.
            var actual = geometry.Volume(crankAngle * Math.PI / 180) * 1E6;
            var error = Math.Abs(actual - expected);

            if (error > worst)
            {
                worst = error;
                worstAngle = crankAngle;
            }
        }

        Assert.True(
            worst <= 0.005,
            $"Worst volume error {worst:G6} cc at {worstAngle} degrees exceeds the printed precision.");
    }

    [Fact]
    public void CapacityAndSweptVolumeAgreeWithTheEditFormAndTheTrace()
    {
        BaselinePaths.Require();

        var geometry = Baseline();

        // The edit form displays whole cc; 81 mm x 77.4 mm x 4 is 1595 cc.
        Assert.Equal(1595, geometry.CapacityCc(), 0);

        // Vd is one cylinder's swept volume, and the trace's extremes span exactly that.
        var volumes = BaselinePaths.TraceColumn("Vcyl").Select(p => p.Value).ToList();
        Assert.Equal(volumes.Max() - volumes.Min(), geometry.SweptVolume * 1E6, 2);
    }

    [Fact]
    public void TheVolumeDerivativeAgreesWithADifferenceOfTheVolume()
    {
        BaselinePaths.Require();

        var geometry = Baseline();

        // Not a legacy comparison: the trace does not print dV/dtheta. This pins the
        // analytic derivative against a central difference of Volume, which is the only
        // independent check available, and would catch a transposed term.
        for (var degrees = -350; degrees <= 350; degrees += 10)
        {
            var theta = degrees * Math.PI / 180;
            const double h = 1e-6;

            var difference = (geometry.Volume(theta + h) - geometry.Volume(theta - h)) / (2 * h);
            var analytic = geometry.VolumeRatePerRadian(theta);

            Assert.Equal(difference, analytic, Math.Abs(difference) * 1e-6 + 1e-12);
        }
    }

    [Fact]
    public void WallAreaIsTheLateralAreaOfTheInstantaneousVolume()
    {
        BaselinePaths.Require();

        var geometry = Baseline();
        var theta = Math.PI / 2;

        Assert.Equal(geometry.Volume(theta) * 4 / geometry.Bore, geometry.WallArea(theta), 1e-12);
    }

    [Fact]
    public void GeometryConvertsTheMillimetresTheEngFileHolds()
    {
        BaselinePaths.Require();

        // Engine carries the file's own units; the physics needs metres. If this ever
        // flips, every volume in the simulation moves by 1e9.
        Assert.Equal(0.081, Baseline().Bore, 1e-12);
        Assert.Equal(0.0774, Baseline().Stroke, 1e-12);
    }
}
