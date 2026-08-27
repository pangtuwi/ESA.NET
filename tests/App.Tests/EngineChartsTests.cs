using System.Globalization;
using App.Core;
using App.Core.Charts;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The chart data builders, driven from the baseline trace.
/// </summary>
/// <remarks>
/// These are the first pieces of the port with no machine-comparable reference: the
/// original's charts survive only as screenshots. So the tests check what can be checked -
/// that the right quantities are plotted, in the right units, over the right crank angles,
/// with the axis limits the source computes - rather than comparing pixels.
/// </remarks>
public sealed class EngineChartsTests
{
    /// <summary>Loads the reference trace into a <see cref="CrankAngleTrace"/>.</summary>
    private static CrankAngleTrace ReferenceTrace()
    {
        var trace = new CrankAngleTrace();

        foreach (var line in File.ReadAllLines(BaselinePaths.File("A2China.txt")).Skip(1))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var fields = line.Split(',');
            var point = trace[int.Parse(fields[0], CultureInfo.InvariantCulture)];

            for (var column = 1; column <= EsaLimits.CapturedValueCount; column++)
            {
                point[column] = double.Parse(fields[column], CultureInfo.InvariantCulture)
                                / trace.ScaleFactors[column - 1];
            }
        }

        return trace;
    }

    [Fact]
    public void ThePressureVolumeDiagramPlotsTheRealCycle()
    {
        BaselinePaths.Require();

        var chart = EngineCharts.PressureVolume(ReferenceTrace());
        var series = Assert.Single(chart.Series);

        // Every even crank angle: 360 of the 720.
        Assert.Equal(360, series.Count);

        // Volume in cc spans the swept volume plus clearance, and pressure in bar reaches
        // the recorded peak of about 70.
        Assert.InRange(series.X.Min(), 40, 50);
        Assert.InRange(series.X.Max(), 440, 450);
        Assert.InRange(series.Y.Max(), 68, 72);

        Assert.Equal("Gas Volume [cc]", chart.XAxisLabel);
        Assert.Equal("Pressure [bar]", chart.YAxisLabel);
    }

    [Fact]
    public void GasExchangeChartsUseTheShiftedCrankAngle()
    {
        BaselinePaths.Require();

        var chart = EngineCharts.GasFlowVelocity(ReferenceTrace());

        // The axis runs 0 to 720 rather than -359 to 360, so the intake and exhaust
        // events sit together instead of being split across the ends.
        foreach (var series in chart.Series)
        {
            Assert.InRange(series.X.Min(), 0, 2);
            Assert.InRange(series.X.Max(), 718, 720);
        }

        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(-150, chart.YMinimum);
        Assert.Equal(450, chart.YMaximum);
    }

    [Fact]
    public void ThePressureAxisScalesWithSpeedAndIsThenClamped()
    {
        BaselinePaths.Require();

        var trace = ReferenceTrace();

        // 0.00025*rpm + 1.55, floored and capped by the original at 0 and 5 bar.
        Assert.Equal(2.55, EngineCharts.GasFlowPressure(trace, 4000).YMaximum!.Value, 6);
        Assert.Equal(0.2, EngineCharts.GasFlowPressure(trace, 4000).YMinimum!.Value, 6);

        // At high speed the computed maximum exceeds 5 and is clamped; the minimum goes
        // negative and is floored at zero.
        Assert.Equal(5, EngineCharts.GasFlowPressure(trace, 20000).YMaximum!.Value);
        Assert.Equal(0, EngineCharts.GasFlowPressure(trace, 20000).YMinimum!.Value);
    }

    [Fact]
    public void TheInCylinderChartCoversOnlyTheClosedPeriod()
    {
        BaselinePaths.Require();

        var chart = EngineCharts.InCylinder(ReferenceTrace());

        Assert.Equal(3, chart.Series.Count);

        // Points appear only where both valves are shut, so the curve stops well short of
        // the full 360 that an every-other-degree sweep would give.
        var pressure = chart.Series[0];
        Assert.True(pressure.Count < 200, $"Expected the closed period only, got {pressure.Count} points.");

        // Every plotted angle really is inside the closed period - but the chart's idea
        // of closed is where the flow areas reach zero, which is not the nominal valve
        // timing. Each cam profile holds its valve seated across the first and last few
        // per cent of its window, so the closed period runs a couple of degrees wider at
        // both ends than the -100 and 116 the timing gives.
        Assert.InRange(pressure.X.Min(), -106, -100);
        Assert.InRange(pressure.X.Max(), 116, 122);
    }

    [Fact]
    public void TheMassBalanceCarriesItsThreeDifferentScaleFactors()
    {
        BaselinePaths.Require();

        var chart = EngineCharts.GasFlowMass(ReferenceTrace());

        Assert.Equal(5, chart.Series.Count);

        // The cylinder charge is about 580 mg, which at 1e5 plots near 58.
        var cylinder = chart.Series.Single(s => s.Name == "Cylinder");
        Assert.InRange(cylinder.Y.Max(), 55, 65);

        // The per-step flows are three orders smaller and use 1e7 to share the axis.
        var massOut = chart.Series.Single(s => s.Name == "Mass Out");
        Assert.InRange(massOut.Y.Max(), 50, 200);
    }

    [Fact]
    public void TheEnergyBalanceTakesItsLimitsFromABeforeTopDeadCentre()
    {
        BaselinePaths.Require();

        var trace = ReferenceTrace();
        var chart = EngineCharts.EnergyBalance(trace);

        // Every crank angle, not every other: this chart is drawn from the form, which
        // has its own loop.
        Assert.All(chart.Series, s => Assert.Equal(720, s.Count));

        // Limits are pinned to the values at -180 rather than the data's own range.
        Assert.Equal(trace[-180][28] - 100, chart.YMinimum!.Value, 6);
        Assert.Equal(trace[-180][22] + 50, chart.YMaximum!.Value, 6);
    }

    [Fact]
    public void TheValveLiftChartShowsBothCamsInMillimetres()
    {
        BaselinePaths.Require();

        var loader = new EngineLoader(
            new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;

        var chart = EngineCharts.ValveLift(
            ValveMotion.Inlet(engine.Manifold.InletValve),
            ValveMotion.Exhaust(engine.Manifold.ExhaustValve));

        Assert.Equal(2, chart.Series.Count);

        // The .eng file gives 8.62 mm inlet and 10.4 mm exhaust; the axis clears the
        // taller of the two by half a millimetre.
        Assert.Equal(8.62, chart.Series[0].Y.Max(), 2);
        Assert.Equal(10.4, chart.Series[1].Y.Max(), 2);
        Assert.Equal(10.9, chart.YMaximum!.Value, 2);

        // The shifted angle puts the overlap around top dead centre in the middle of the
        // plot rather than split across its ends.
        var peakAt = chart.Series[0].X[Array.IndexOf(chart.Series[0].Y, chart.Series[0].Y.Max())];
        Assert.InRange(peakAt, -200, 200);
    }

    [Fact]
    public void TheTorqueCurvePlotsThreeSeriesAgainstSpeed()
    {
        var data = new PerformanceData();

        data.Points.AddRange(
        [
            new PerformancePoint { Speed = 2000, Torque = 140.2, Power = 29.4, VolumetricEfficiency = 95.1 },
            new PerformancePoint { Speed = 4000, Torque = 151.3, Power = 63.4, VolumetricEfficiency = 109.7 },
            new PerformancePoint { Speed = 6000, Torque = 138.8, Power = 87.2, VolumetricEfficiency = 98.4 },
        ]);

        var chart = EngineCharts.TorqueCurve(data);

        Assert.Equal(3, chart.Series.Count);
        Assert.All(chart.Series, s => Assert.Equal(3, s.Count));
        Assert.Equal([2000, 4000, 6000], chart.Series[0].X);
        Assert.Equal(151.3, chart.Series[0].Y[1]);
        Assert.Equal(63.4, chart.Series[1].Y[1]);
    }
}
