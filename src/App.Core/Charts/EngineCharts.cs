using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Charts;

/// <summary>
/// Builds the charts the original draws, from a captured cycle and a set of performance
/// points. Ports the series construction in <c>TFMain.UpdateGraphs</c> (Main.pas:590-636),
/// <c>TFEnergyBalance.FormShow</c> (GHeatLoss.pas) and
/// <c>TFValveLift.FormShow</c> (GValveLift.pas).
/// </summary>
/// <remarks>
/// <b>Point density.</b> The original plots at two different resolutions depending on how
/// the chart came to be drawn: a static redraw walks every <b>even</b> crank angle
/// (<c>RedrawGraphs</c>, 360 points), while a live run updates every <b>five</b> degrees
/// (<c>Simulate</c>, 144 points). Neither plots all 720. These builders take the stride as
/// a parameter and default to the static redraw's two, which is what a finished run shows.
/// See ISSUES.md B70.
/// </remarks>
public static class EngineCharts
{
    /// <summary>The stride <c>RedrawGraphs</c> uses: every even crank angle.</summary>
    public const int RedrawStride = 2;

    /// <summary>The stride a live run uses.</summary>
    public const int LiveStride = 5;

    private static IEnumerable<int> CrankAngles(int stride)
    {
        for (var angle = EsaLimits.FirstCrankAngle; angle <= EsaLimits.LastCrankAngle; angle++)
        {
            if (angle % stride == 0)
            {
                yield return angle;
            }
        }
    }

    /// <summary>
    /// Gas-exchange charts run on a crank angle shifted into 0 to 720 rather than -359 to
    /// 360, so the intake and exhaust events sit together instead of being split across
    /// the ends. Delphi: <c>if CA &lt; 0 then FlowAngle := CA+720</c>.
    /// </summary>
    private static double FlowAngle(int crankAngle) => crankAngle < 0 ? crankAngle + 720 : crankAngle;

    private static ChartSeries Build(
        CrankAngleTrace trace, int stride, string name, Func<CrankAnglePoint, double> value,
        Func<int, double>? x = null)
    {
        var angles = CrankAngles(stride).ToArray();

        return new ChartSeries(
            name,
            [.. angles.Select(a => x?.Invoke(a) ?? a)],
            [.. angles.Select(a => value(trace[a]))]);
    }

    /// <summary>
    /// The pressure-volume diagram, cylinder pressure against cylinder volume. Delphi
    /// <c>Series2</c>.
    /// </summary>
    public static ChartDefinition PressureVolume(CrankAngleTrace trace, int stride = RedrawStride)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var angles = CrankAngles(stride).ToArray();

        return new ChartDefinition(
            "P-V Diagram",
            "Volume [cc]",
            "Pressure [bar]",
            [new ChartSeries(
                "Cylinder",
                [.. angles.Select(a => trace[a][CapturedQuantity.Volume] * 1e6)],
                [.. angles.Select(a => trace[a][CapturedQuantity.Pressure] / 1e5)])]);
    }

    /// <summary>
    /// Pressure at the cylinder and either side of it, through the gas exchange. Delphi
    /// <c>Series3</c>, <c>Series1</c> and <c>Series6</c> in pressure mode.
    /// </summary>
    /// <param name="rpm">
    /// Engine speed, which sets the axis limits: the original scales them with speed and
    /// then clamps the result to [0, 5] bar.
    /// </param>
    public static ChartDefinition GasFlowPressure(
        CrankAngleTrace trace, double rpm, int stride = RedrawStride)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var maximum = Math.Min((0.00025 * rpm) + 1.55, 5);
        var minimum = Math.Max((-0.0001 * rpm) + 0.6, 0);

        return new ChartDefinition(
            "Gas Flow: Pressure",
            "Crank Angle [deg]",
            "Pressure [bar]",
            [
                Build(trace, stride, "Cylinder", p => p[CapturedQuantity.Pressure] / 1e5, FlowAngle),
                Build(trace, stride, "Inlet Valve", p => p[CapturedQuantity.InletPressure] / 1e5, FlowAngle),
                Build(trace, stride, "Exhaust Valve", p => p[CapturedQuantity.ExhaustPressure] / 1e5, FlowAngle),
            ],
            minimum,
            maximum);
    }

    /// <summary>Gas velocity at each valve. Delphi <c>Series1</c> and <c>Series6</c> in velocity mode.</summary>
    public static ChartDefinition GasFlowVelocity(CrankAngleTrace trace, int stride = RedrawStride)
    {
        ArgumentNullException.ThrowIfNull(trace);

        return new ChartDefinition(
            "Gas Flow: Velocity",
            "Crank Angle [deg]",
            "Velocity [m/s]",
            [
                Build(trace, stride, "Inlet Valve", p => p[CapturedQuantity.InletVelocity], FlowAngle),
                Build(trace, stride, "Exhaust Valve", p => p[CapturedQuantity.ExhaustVelocity], FlowAngle),
            ],
            -150,
            450);
    }

    /// <summary>
    /// The mass balance: what is in the cylinder and what is crossing the valves.
    /// </summary>
    /// <remarks>
    /// The five series carry three different scale factors - 1e5 for the masses in the
    /// cylinder and 1e7 for the per-step flows - chosen so they share one axis. That
    /// makes the axis unitless, which is why it has no caption in the original either.
    /// </remarks>
    public static ChartDefinition GasFlowMass(CrankAngleTrace trace, int stride = RedrawStride)
    {
        ArgumentNullException.ThrowIfNull(trace);

        return new ChartDefinition(
            "Gas Flow: Mass Balance",
            "Crank Angle [deg]",
            "Mass [scaled]",
            [
                Build(trace, stride, "Burnt", p => p[CapturedQuantity.BurntMass] * 1e5, FlowAngle),
                Build(trace, stride, "Unburnt", p => p[CapturedQuantity.UnburntMass] * 1e5, FlowAngle),
                Build(trace, stride, "Mass In", p => p[CapturedQuantity.MassIn] * 1e7, FlowAngle),
                Build(trace, stride, "Mass Out", p => p[CapturedQuantity.MassOut] * 1e7, FlowAngle),
                Build(trace, stride, "Cylinder", p => p[CapturedQuantity.CylinderMass] * 1e5, FlowAngle),
            ]);
    }

    /// <summary>
    /// Cylinder pressure and the two zone temperatures, <b>over the closed period only</b>.
    /// </summary>
    /// <remarks>
    /// The original plots a point only where both valve flow areas are zero, so the curve
    /// simply stops at exhaust valve opening and resumes at inlet valve closing rather
    /// than running right round the cycle.
    /// </remarks>
    public static ChartDefinition InCylinder(CrankAngleTrace trace, int stride = RedrawStride)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var closed = CrankAngles(stride)
            .Where(a => trace[a][CapturedQuantity.InletValveArea] == 0
                        && trace[a][CapturedQuantity.ExhaustValveArea] == 0)
            .ToArray();

        ChartSeries Series(string name, Func<CrankAnglePoint, double> value) =>
            new(name, [.. closed.Select(a => (double)a)], [.. closed.Select(a => value(trace[a]))]);

        return new ChartDefinition(
            "In-Cylinder Conditions",
            "Crank Angle [deg]",
            "Pressure [bar] / Temperature [K]",
            [
                Series("Pressure", p => p[CapturedQuantity.Pressure] / 1e5),
                Series("Burnt Temperature", p => p[CapturedQuantity.BurntTemperature]),
                Series("Unburnt Temperature", p => p[CapturedQuantity.UnburntTemperature]),
            ]);
    }

    /// <summary>
    /// The energy balance: accumulated heat loss against indicated and pumping work. Port
    /// of <c>TFEnergyBalance.FormShow</c>.
    /// </summary>
    /// <remarks>
    /// The axis limits are taken from the values at 180 degrees before top dead centre,
    /// padded by 100 below and 50 above - a fixed reference point rather than the data's
    /// own range.
    /// </remarks>
    public static ChartDefinition EnergyBalance(CrankAngleTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        const int HeatLoss = 28;
        const int Work = 22;
        const int PumpingWork = 23;

        var angles = Enumerable
            .Range(EsaLimits.FirstCrankAngle, EsaLimits.LastCrankAngle - EsaLimits.FirstCrankAngle + 1)
            .ToArray();

        ChartSeries Series(string name, int ordinal) =>
            new(name, [.. angles.Select(a => (double)a)], [.. angles.Select(a => trace[a][ordinal])]);

        return new ChartDefinition(
            "Energy Balance",
            "Crank Angle [deg]",
            "Energy [J]",
            [
                Series("Heat Loss", HeatLoss),
                Series("Indicated Work", Work),
                Series("Pumping Work", PumpingWork),
            ],
            trace[-180][HeatLoss] - 100,
            trace[-180][Work] + 50);
    }

    /// <summary>
    /// Valve lift against crank angle. Port of <c>TFValveLift.FormShow</c>, which walks a
    /// shifted angle so the overlap around top dead centre sits in the middle of the plot.
    /// </summary>
    public static ChartDefinition ValveLift(ValveMotion inlet, ValveMotion exhaust)
    {
        ArgumentNullException.ThrowIfNull(inlet);
        ArgumentNullException.ThrowIfNull(exhaust);

        var plotted = Enumerable.Range(-359, 720).ToArray();

        double Lift(ValveMotion valve, int i)
        {
            var crankAngle = i + 360;

            if (crankAngle > 360)
            {
                crankAngle -= 720;
            }

            return valve.Lift(crankAngle) * 1e3;
        }

        return new ChartDefinition(
            "Camshaft Profiles",
            "Crank Angle [deg]",
            "Lift [mm]",
            [
                new ChartSeries(
                    "Inlet",
                    [.. plotted.Select(i => (double)i)],
                    [.. plotted.Select(i => Lift(inlet, i))]),
                new ChartSeries(
                    "Exhaust",
                    [.. plotted.Select(i => (double)i)],
                    [.. plotted.Select(i => Lift(exhaust, i))]),
            ],
            YMaximum: (Math.Max(inlet.MaxLift, exhaust.MaxLift) * 1e3) + 0.5);
    }

    /// <summary>
    /// Torque, power and volumetric efficiency against engine speed, across the points a
    /// multi-run has produced. Port of <c>TPerfData</c> and its curve.
    /// </summary>
    public static ChartDefinition TorqueCurve(PerformanceData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var points = data.Points;
        var speeds = points.Select(p => p.Speed).ToArray();

        return new ChartDefinition(
            "Performance",
            "Engine Speed [rev/min]",
            "Torque [Nm] / Power [kW] / Volumetric Efficiency [%]",
            [
                new ChartSeries("Torque", speeds, [.. points.Select(p => p.Torque)]),
                new ChartSeries("Power", speeds, [.. points.Select(p => p.Power)]),
                new ChartSeries("Volumetric Efficiency", speeds, [.. points.Select(p => p.VolumetricEfficiency)]),
            ]);
    }
}
