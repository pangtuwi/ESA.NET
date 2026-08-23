using App.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// View model for the shell window.
/// </summary>
/// <remarks>
/// The commands correspond one-to-one with the <c>OnClick</c> handlers on
/// <c>TFMain</c> in Main.pas. The File menu is live as of phase 3; Run and Graph stay
/// no-ops until the simulation core and the charts arrive.
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IEngineLoader _engineLoader;
    private readonly IEngineDefinitionStore _definitions;

    public MainWindowViewModel(IEngineLoader engineLoader, IEngineDefinitionStore definitions)
    {
        _engineLoader = engineLoader;
        _definitions = definitions;
    }

    /// <summary>Window caption. The Delphi original appended a version and build date.</summary>
    public string Title => "Engine Simulation and Analysis (ESA)";

    /// <summary>The engine currently open, or <see langword="null"/> before anything is loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private EngineLoadResult? _currentEngine;

    /// <summary>The file the current engine came from, shown in the status line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _currentEngineFile = string.Empty;

    /// <summary>What the Delphi status bar showed: the file and engine name, plus any load problems.</summary>
    public string StatusText
    {
        get
        {
            if (CurrentEngine is null)
            {
                return "No engine loaded.";
            }

            var status = $"{Path.GetFileName(CurrentEngineFile)} — {CurrentEngine.Engine.Name}";

            return CurrentEngine.IsComplete
                ? status
                : $"{status} ({CurrentEngine.Problems.Count} data file(s) could not be loaded)";
        }
    }

    /// <summary>
    /// Opens an engine file and everything it names. Port of <c>Load1Click</c>.
    /// </summary>
    /// <remarks>
    /// Unlike the original, the side files are read here rather than at simulation-init
    /// time, so a missing cam profile is reported when the engine is opened instead of
    /// part-way through a run.
    /// </remarks>
    public void LoadEngine(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        CurrentEngine = _engineLoader.Load(path);
        CurrentEngineFile = path;
    }

    /// <summary>Saves the current definition. Port of <c>SaveAs1Click</c>.</summary>
    public void SaveEngineAs(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (CurrentEngine is null)
        {
            return;
        }

        _definitions.Write(path, CurrentEngine.Definition);
        CurrentEngineFile = path;
    }

    // File. Delphi: Load1Click, SaveAs1Click, Edit1Click, LoadDefault1Click, Exit1Click.

    [RelayCommand]
    private void Load()
    {
        // The file dialog belongs to the view; the shell wires it to LoadEngine.
    }

    [RelayCommand]
    private void SaveAs()
    {
        // The file dialog belongs to the view; the shell wires it to SaveEngineAs.
    }

    [RelayCommand]
    private void EditEngine()
    {
        // The window is opened by the view, against CurrentEngine.Definition.
    }

    [RelayCommand]
    private void LoadDefault()
    {
        // Resolved from ESA.ini's [DefaultFiles] Engine entry by the shell.
    }

    [RelayCommand]
    private static void Exit()
    {
        // The lifetime shutdown belongs to the view.
    }

    // Run. Delphi: SinglePointSimulation1Click, MultiPointSimulation1Click,
    // Pause1Click, STOP1Click, QuickRunClick.

    [RelayCommand]
    private static void SinglePointSimulation()
    {
        // Phase 4.
    }

    [RelayCommand]
    private static void MultiPointSimulation()
    {
        // Phase 4.
    }

    [RelayCommand]
    private static void Pause()
    {
        // Phase 4.
    }

    [RelayCommand]
    private static void Stop()
    {
        // Phase 4.
    }

    [RelayCommand]
    private static void QuickRun()
    {
        // Phase 4.
    }

    // Graph. Delphi: Options1Click, ShowTorqueCurve1Click, ValveOpening1Click, HeatLoss1Click.

    [RelayCommand]
    private static void RunTimeGraphOptions()
    {
        // Phase 5.
    }

    [RelayCommand]
    private static void TorqueCurve()
    {
        // Phase 5.
    }

    [RelayCommand]
    private static void ValveOpening()
    {
        // Phase 5.
    }

    [RelayCommand]
    private static void EnergyBalance()
    {
        // Phase 5.
    }

    // Text. Delphi: PVTTrace1Click.

    [RelayCommand]
    private static void PvtTrace()
    {
        // Phase 5.
    }

    // Help. Delphi: Contents1Click, About1Click.

    [RelayCommand]
    private static void UserManual()
    {
        // Phase 3.
    }

    [RelayCommand]
    private static void About()
    {
        // Phase 3.
    }
}
