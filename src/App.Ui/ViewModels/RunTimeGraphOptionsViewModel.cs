using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// The Run-Time Graph Options dialog. Port of <c>TFGraphOptions</c> (FflowGraphOptions.dfm):
/// a radio group and a pair of Y-axis boxes for each of the three embedded charts.
/// </summary>
/// <remarks>
/// <para>
/// The original honours a Y-axis box only when it holds a number <b>greater than zero</b>
/// (<c>Main.pas:412-456, 507-534</c>), using zero to mean "automatic". That makes a
/// negative minimum impossible to set, though the velocity chart's own default is -150.
/// Here a blank box means automatic and any number is honoured.
/// </para>
/// <para>
/// The port shipped this dialog with only the gas-flow group, and with Nothing and Mass
/// Transfer greyed out (ISSUES.md A27).
/// </para>
/// </remarks>
public sealed partial class RunTimeGraphOptionsViewModel : ObservableObject
{
    /// <summary>Whether the dialog was accepted.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Raised when the dialog should close.</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GasFlowNothing), nameof(GasFlowPressure),
        nameof(GasFlowVelocity), nameof(GasFlowMassTransfer))]
    private GasFlowGraph _gasFlow = GasFlowGraph.Pressure;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PressureVolumeNothing))]
    private bool _pressureVolume = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InCylinderNothing))]
    private bool _inCylinder = true;

    // The six Y-axis boxes, held as typed so a half-entered value is not thrown away.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _gasFlowMaximum = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _gasFlowMinimum = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _pressureVolumeMaximum = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _pressureVolumeMinimum = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _inCylinderMaximum = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _inCylinderMinimum = string.Empty;

    // One property per radio button, so each binds two-way on its own.

    public bool GasFlowNothing
    {
        get => GasFlow == GasFlowGraph.Nothing;
        set => Select(value, GasFlowGraph.Nothing);
    }

    public bool GasFlowPressure
    {
        get => GasFlow == GasFlowGraph.Pressure;
        set => Select(value, GasFlowGraph.Pressure);
    }

    public bool GasFlowVelocity
    {
        get => GasFlow == GasFlowGraph.Velocity;
        set => Select(value, GasFlowGraph.Velocity);
    }

    public bool GasFlowMassTransfer
    {
        get => GasFlow == GasFlowGraph.MassTransfer;
        set => Select(value, GasFlowGraph.MassTransfer);
    }

    public bool PressureVolumeNothing
    {
        get => !PressureVolume;
        set => PressureVolume = !value;
    }

    public bool InCylinderNothing
    {
        get => !InCylinder;
        set => InCylinder = !value;
    }

    private void Select(bool value, GasFlowGraph graph)
    {
        if (value)
        {
            GasFlow = graph;
        }
    }

    /// <summary>What is wrong with the Y-axis boxes, or null when OK can be pressed.</summary>
    public string? Error =>
        Check("Gas Flow", GasFlowMinimum, GasFlowMaximum)
        ?? Check("P-V Diagram", PressureVolumeMinimum, PressureVolumeMaximum)
        ?? Check("In Cylinder", InCylinderMinimum, InCylinderMaximum);

    /// <summary>Fills the dialog from the options in force.</summary>
    public void Load(GraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        GasFlow = options.GasFlow;
        PressureVolume = options.PressureVolume;
        InCylinder = options.InCylinder;

        (GasFlowMinimum, GasFlowMaximum) = Texts(options.GasFlowAxis);
        (PressureVolumeMinimum, PressureVolumeMaximum) = Texts(options.PressureVolumeAxis);
        (InCylinderMinimum, InCylinderMaximum) = Texts(options.InCylinderAxis);

        Accepted = false;
    }

    /// <summary>The options as the dialog now has them. Valid only while <see cref="Error"/> is null.</summary>
    public GraphOptions Options => new(
        GasFlow,
        PressureVolume,
        InCylinder,
        new AxisLimits(Parse(GasFlowMinimum), Parse(GasFlowMaximum)),
        new AxisLimits(Parse(PressureVolumeMinimum), Parse(PressureVolumeMaximum)),
        new AxisLimits(Parse(InCylinderMinimum), Parse(InCylinderMaximum)));

    /// <summary>Delphi <c>BOK</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanAccept => Error is null;

    /// <summary>Delphi <c>BitBtn1</c>, Cancel.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private static string? Check(string chart, string minimum, string maximum)
    {
        if (!IsBlankOrNumber(minimum) || !IsBlankOrNumber(maximum))
        {
            return $"{chart}: the Y axis limits must be numbers, or blank for automatic.";
        }

        return Parse(minimum) is { } low && Parse(maximum) is { } high && low >= high
            ? $"{chart}: the Y axis minimum must be below the maximum."
            : null;
    }

    private static bool IsBlankOrNumber(string text) =>
        string.IsNullOrWhiteSpace(text) || Parse(text) is not null;

    private static double? Parse(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value)
            ? value
            : null;

    private static (string Minimum, string Maximum) Texts(AxisLimits limits) =>
        (Text(limits.Minimum), Text(limits.Maximum));

    private static string Text(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
