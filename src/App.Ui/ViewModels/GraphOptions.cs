using App.Core.Charts;

namespace App.Ui.ViewModels;

/// <summary>What the gas-flow quadrant plots. Delphi <c>RGFlowGraph.ItemIndex</c>, 0 to 3.</summary>
public enum GasFlowGraph
{
    /// <summary>Nothing: the quadrant is left empty.</summary>
    Nothing,

    /// <summary>Cylinder, inlet and exhaust pressure.</summary>
    Pressure,

    /// <summary>Inlet and exhaust gas velocity.</summary>
    Velocity,

    /// <summary>Burnt, unburnt and cylinder mass, and the flows through the valves.</summary>
    MassTransfer,
}

/// <summary>
/// One quadrant's Y-axis scaling, Delphi's paired <c>...YMax</c> and <c>...YMin</c> boxes.
/// A null limit leaves that end of the axis as the chart chooses it.
/// </summary>
public sealed record AxisLimits(double? Minimum, double? Maximum)
{
    /// <summary>Both ends left to the chart.</summary>
    public static AxisLimits Automatic { get; } = new(null, null);

    /// <summary>The chart with whichever limits are set laid over its own.</summary>
    public ChartDefinition ApplyTo(ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        return chart with
        {
            YMinimum = Minimum ?? chart.YMinimum,
            YMaximum = Maximum ?? chart.YMaximum,
        };
    }
}

/// <summary>
/// Everything the Run-Time Graph Options dialog sets: what each of the three embedded
/// charts plots, and its Y-axis scaling. Port of <c>TFGraphOptions</c>
/// (FflowGraphOptions.dfm), whose radio groups and edit boxes <c>Main.pas</c> reads
/// directly when it draws.
/// </summary>
/// <param name="GasFlow">Delphi <c>RGFlowGraph</c>.</param>
/// <param name="PressureVolume">Delphi <c>RGPVGraph</c>: Nothing, or Cylinder Pressure.</param>
/// <param name="InCylinder">Delphi <c>RGInCylGraph</c>: Nothing, or Cyl. Press., Tb and Tu.</param>
/// <param name="GasFlowAxis">Delphi <c>EGFYMax</c> and <c>EGFYMin</c>.</param>
/// <param name="PressureVolumeAxis">Delphi <c>EPVYMax</c> and <c>EPVYMin</c>.</param>
/// <param name="InCylinderAxis">Delphi <c>EICYMax</c> and <c>EICYMin</c>.</param>
public sealed record GraphOptions(
    GasFlowGraph GasFlow,
    bool PressureVolume,
    bool InCylinder,
    AxisLimits GasFlowAxis,
    AxisLimits PressureVolumeAxis,
    AxisLimits InCylinderAxis)
{
    /// <summary>All three charts, pressures in the gas-flow quadrant, axes automatic.</summary>
    public static GraphOptions Default { get; } = new(
        GasFlowGraph.Pressure, true, true,
        AxisLimits.Automatic, AxisLimits.Automatic, AxisLimits.Automatic);

    /// <summary>
    /// Applies the Single Speed dialog's chart choice, as <c>Main.pas:861-882</c> writes
    /// its On, Off or Selection into the three radio groups before a run.
    /// </summary>
    /// <remarks>
    /// The original sets the gas-flow group to Pressure whenever the chart is wanted,
    /// throwing away a choice of Velocity or Mass Transfer made in this dialog. Here a
    /// chosen mode survives the run; only Nothing becomes Pressure, since the operator has
    /// just asked for the chart.
    /// </remarks>
    public GraphOptions WithSelection(GraphSelection selection) => this with
    {
        GasFlow = !selection.GasFlow
            ? GasFlowGraph.Nothing
            : GasFlow == GasFlowGraph.Nothing ? GasFlowGraph.Pressure : GasFlow,
        PressureVolume = selection.PressureVolume,
        InCylinder = selection.InCylinder,
    };
}
