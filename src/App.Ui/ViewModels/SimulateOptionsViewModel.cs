using System.Globalization;
using App.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>Which of the three run-time charts a run should draw.</summary>
/// <param name="GasFlow">Delphi <c>CBGasFlow</c>.</param>
/// <param name="PressureVolume">Delphi <c>CBPV</c>.</param>
/// <param name="InCylinder">Delphi <c>CBInCyl</c>.</param>
public readonly record struct GraphSelection(
    bool GasFlow, bool PressureVolume, bool InCylinder)
{
    /// <summary>Whether any chart is drawn at all, Delphi <c>ShowGraphs</c>.</summary>
    public bool Any => GasFlow || PressureVolume || InCylinder;
}

/// <summary>Delphi's three <c>Graphic Display Options</c> radio buttons.</summary>
public enum GraphDisplayMode
{
    /// <summary>Delphi <c>RBGraphsOn</c>: all three charts, and the boxes greyed out.</summary>
    On,

    /// <summary>Delphi <c>RBGraphsOff</c>: no charts, and the boxes greyed out.</summary>
    Off,

    /// <summary>Delphi <c>RBSelection</c>: whichever boxes the operator ticks.</summary>
    Selection,
}

/// <summary>
/// The <b>Single Speed Simulation</b> dialog. Port of <c>TFSimulateOptions</c>
/// (FormSimul.pas / FormSimul.dfm), which the original shows modally from
/// <c>Run &gt; Single Point Simulation</c> before every single-speed run.
/// </summary>
/// <remarks>
/// <para>
/// Engine speed, cycle count and mass balance are asked for here rather than kept on the
/// main window - the original's main form has no run controls at all, only the menu. The
/// three fields open on the values <c>ESA.ini</c> carries, as <c>FormCreate</c> does.
/// </para>
/// <para>
/// The graph radio buttons drive the three checkboxes exactly as the original's click
/// handlers do: <i>Graphs On</i> ticks and disables all three, <i>Graphs Off</i> clears and
/// disables them, and <i>Selection</i> clears them and hands them to the operator.
/// </para>
/// </remarks>
public sealed partial class SimulateOptionsViewModel : ObservableObject
{
    /// <summary>Delphi's clamp in <c>FormClose</c>, which will not let the form close outside it.</summary>
    public const double MinimumRpm = 1250;

    /// <inheritdoc cref="MinimumRpm" />
    public const double MaximumRpm = 7000;

    /// <summary>Raised when the operator has finished with the window, either way.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The window caption, Delphi <c>FSimulateOptions.Caption</c>.</summary>
    public static string Title => "Single Speed Simulation";

    /// <summary>
    /// Delphi <c>mrOK</c>: true when Run was pressed, false when Cancel was, or the window
    /// was closed.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>Delphi <c>ERPM</c>, in rev/min.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedWarning))]
    private double _engineSpeed = 4000;

    /// <summary>Delphi <c>ENoCycles</c>.</summary>
    [ObservableProperty]
    private int _totalCycles = 6;

    /// <summary>Delphi <c>EMassBalance</c>, in milligrams.</summary>
    [ObservableProperty]
    private double _massBalance = 1;

    /// <summary>
    /// Which of the three radio buttons is chosen. Held as one value rather than three
    /// bools so the options stay mutually exclusive without relying on the view's radio
    /// group to enforce it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GraphsOn), nameof(GraphsOff), nameof(Selection),
        nameof(CanChooseGraphs))]
    private GraphDisplayMode _graphMode = GraphDisplayMode.On;

    /// <summary>Delphi <c>RBGraphsOn</c>, the option the form opens on.</summary>
    public bool GraphsOn
    {
        get => GraphMode == GraphDisplayMode.On;
        set => Choose(value, GraphDisplayMode.On);
    }

    /// <summary>Delphi <c>RBGraphsOff</c>.</summary>
    public bool GraphsOff
    {
        get => GraphMode == GraphDisplayMode.Off;
        set => Choose(value, GraphDisplayMode.Off);
    }

    /// <summary>Delphi <c>RBSelection</c>.</summary>
    public bool Selection
    {
        get => GraphMode == GraphDisplayMode.Selection;
        set => Choose(value, GraphDisplayMode.Selection);
    }

    /// <summary>
    /// A radio button only ever reports itself as chosen; unchecking happens by another
    /// being checked, so a false is nothing to act on.
    /// </summary>
    private void Choose(bool chosen, GraphDisplayMode mode)
    {
        if (chosen)
        {
            GraphMode = mode;
        }
    }

    /// <summary>Delphi <c>CBGasFlow</c>.</summary>
    [ObservableProperty]
    private bool _showGasFlow = true;

    /// <summary>Delphi <c>CBPV</c>.</summary>
    [ObservableProperty]
    private bool _showPressureVolume = true;

    /// <summary>Delphi <c>CBInCyl</c>.</summary>
    [ObservableProperty]
    private bool _showInCylinder = true;

    /// <summary>
    /// Whether the three checkboxes are live. Only <i>Selection</i> enables them; the other
    /// two set them and grey them out.
    /// </summary>
    public bool CanChooseGraphs => GraphMode == GraphDisplayMode.Selection;

    /// <summary>What the three checkboxes come to, for the caller.</summary>
    public GraphSelection Graphs =>
        new(ShowGasFlow, ShowPressureVolume, ShowInCylinder);

    /// <summary>
    /// Names the clamp rather than springing it on the operator at the point they press
    /// Run, which is what the original does.
    /// </summary>
    public string? SpeedWarning =>
        EngineSpeed < MinimumRpm || EngineSpeed > MaximumRpm
            ? $"The original clamps engine speed to {MinimumRpm.ToString("F0", CultureInfo.InvariantCulture)}"
              + $" - {MaximumRpm.ToString("F0", CultureInfo.InvariantCulture)} rev/min, and Run will"
              + " use the nearer limit."
            : null;

    // The original's three click handlers: Graphs On ticks all three, Graphs Off and
    // Selection clear all three, and only Selection leaves them enabled.
    partial void OnGraphModeChanged(GraphDisplayMode value) =>
        SetGraphs(value == GraphDisplayMode.On);

    private void SetGraphs(bool shown)
    {
        ShowGasFlow = shown;
        ShowPressureVolume = shown;
        ShowInCylinder = shown;
    }

    /// <summary>Fills the form from the values <c>ESA.ini</c> carries, Delphi <c>FormCreate</c>.</summary>
    public void Load(SimulationSettings settings, double engineSpeed)
    {
        ArgumentNullException.ThrowIfNull(settings);

        EngineSpeed = engineSpeed;
        TotalCycles = settings.CycleCount;
        MassBalance = settings.MassBalance;
    }

    /// <summary>
    /// Delphi <c>BitBtn2</c>, the Run button, and the clamp <c>FormClose</c> applies.
    /// </summary>
    /// <remarks>
    /// The original refuses to close while the speed is outside its range - and because
    /// <c>FormClose</c> runs whichever button was pressed, that traps Cancel too. Clamping
    /// and closing gets the operator the same run without the dead end.
    /// </remarks>
    [RelayCommand]
    private void Run()
    {
        EngineSpeed = Math.Clamp(EngineSpeed, MinimumRpm, MaximumRpm);
        Accepted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Delphi <c>BitBtn1</c>, the Cancel button.</summary>
    [RelayCommand]
    private void Cancel()
    {
        Accepted = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
