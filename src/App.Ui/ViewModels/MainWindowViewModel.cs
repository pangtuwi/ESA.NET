using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// View model for the shell window.
/// </summary>
/// <remarks>
/// Phase 2 is a skeleton: every command is a deliberate no-op so that the menu
/// structure can be reviewed against the original before any behaviour is ported.
/// The commands correspond one-to-one with the <c>OnClick</c> handlers on
/// <c>TFMain</c> in Main.pas.
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Window caption. The Delphi original appended a version and build date.</summary>
    public string Title => "Engine Simulation and Analysis (ESA)";

    // File. Delphi: Load1Click, SaveAs1Click, Edit1Click, LoadDefault1Click, Exit1Click.

    [RelayCommand]
    private static void Load()
    {
        // Phase 3.
    }

    [RelayCommand]
    private static void SaveAs()
    {
        // Phase 3.
    }

    [RelayCommand]
    private static void EditEngine()
    {
        // Phase 3.
    }

    [RelayCommand]
    private static void LoadDefault()
    {
        // Phase 3.
    }

    [RelayCommand]
    private static void Exit()
    {
        // Phase 3.
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
