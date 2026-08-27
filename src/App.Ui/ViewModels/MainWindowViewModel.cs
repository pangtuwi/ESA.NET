using App.Core;
using App.Core.Charts;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Ui.Charts;
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
    private readonly IChartWindowService _charts;
    private readonly SimulationRunner _runner;
    private readonly ISimulationSettingsStore _settingsStore;

    private CancellationTokenSource? _running;

    public MainWindowViewModel(
        IEngineLoader engineLoader,
        IEngineDefinitionStore definitions,
        IChartWindowService charts,
        SimulationRunner runner,
        ISimulationSettingsStore settingsStore)
    {
        _engineLoader = engineLoader;
        _definitions = definitions;
        _charts = charts;
        _runner = runner;
        _settingsStore = settingsStore;
    }

    /// <summary>Run options, as ESA.ini carries them.</summary>
    public SimulationSettings Settings { get; } = new();

    /// <summary>Engine speed for a single-point run, in rev/min.</summary>
    [ObservableProperty]
    private double _engineSpeed = 4000;

    /// <summary>What the simulation is doing, for the status bar.</summary>
    [ObservableProperty]
    private string _runStatus = string.Empty;

    /// <summary>Whether a simulation is in progress, which disables starting another.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SinglePointSimulationCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    /// <summary>
    /// The last completed run's captured cycle, which the charts draw from. Null until a
    /// simulation has been run, which is what disables the chart commands.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnergyBalanceCommand))]
    [NotifyCanExecuteChangedFor(nameof(PressureVolumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(GasFlowPressureCommand))]
    [NotifyCanExecuteChangedFor(nameof(GasFlowVelocityCommand))]
    [NotifyCanExecuteChangedFor(nameof(GasFlowMassCommand))]
    [NotifyCanExecuteChangedFor(nameof(InCylinderCommand))]
    private CrankAngleTrace? _trace;

    /// <summary>Points accumulated across a multi-run, for the torque curve.</summary>
    public PerformanceData Performance { get; } = new();

    /// <summary>Window caption. The Delphi original appended a version and build date.</summary>
    public string Title => "Engine Simulation and Analysis (ESA)";

    /// <summary>The engine currently open, or <see langword="null"/> before anything is loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(ValveOpeningCommand))]
    [NotifyCanExecuteChangedFor(nameof(SinglePointSimulationCommand))]
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

    /// <summary>
    /// Simulates the open engine at the chosen speed, then makes the charts and exports
    /// available. Runs off the UI thread so the window stays responsive and Stop works.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartRun))]
    private async Task SinglePointSimulationAsync()
    {
        var engine = CurrentEngine!.Engine;
        engine.Rpm = EngineSpeed;

        _running = new CancellationTokenSource();
        IsRunning = true;
        RunStatus = "Simulating...";

        try
        {
            var token = _running.Token;

            // Progress<T> marshals back to the UI thread for us.
            var progress = new Progress<SimulationProgress>(
                p => RunStatus =
                    $"Cycle {p.Cycle} of {p.RequestedCycles}   "
                    + $"{p.CrankAngle,4:F0}°   mass balance {p.MassBalance:F2} mg");

            var result = await Task.Run(
                () => _runner.Run(engine, Settings, progress, token), token);

            Trace = result.Trace;

            Performance.Points.Add(new PerformancePoint
            {
                Speed = engine.Rpm,
                Torque = engine.Torque,
                Power = engine.BrakePower / 1e3,
                VolumetricEfficiency = engine.VolumetricEfficiency,
            });

            TorqueCurveCommand.NotifyCanExecuteChanged();

            RunStatus = result.Converged
                ? $"Converged after {result.CyclesRun} cycles.   "
                  + $"Torque {engine.Torque:F1} Nm   Power {engine.BrakePower / 1e3:F1} kW"
                : $"Stopped at the requested {result.CyclesRun} cycles without converging.   "
                  + $"Torque {engine.Torque:F1} Nm   Power {engine.BrakePower / 1e3:F1} kW";
        }
        catch (OperationCanceledException)
        {
            RunStatus = "Stopped.";
        }
        catch (EngineException error)
        {
            RunStatus = $"Terminated on engine error: {error.Message}";
        }
        catch (CfdException error)
        {
            RunStatus = $"Terminated on CFD error: {error.Message}";
        }
        catch (EquilibriumException error)
        {
            RunStatus = $"Terminated on equilibrium error: {error.Message}";
        }
        catch (GasPropertiesException error)
        {
            RunStatus = $"Terminated on gas properties error: {error.Message}";
        }
        finally
        {
            IsRunning = false;
            _running.Dispose();
            _running = null;
        }
    }

    private bool CanStartRun => CurrentEngine is not null && !IsRunning;

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

    /// <summary>Asks a running simulation to stop at the next step.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _running?.Cancel();

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

    /// <summary>Torque, power and volumetric efficiency against speed.</summary>
    [RelayCommand(CanExecute = nameof(HasPerformancePoints))]
    private void TorqueCurve() => _charts.Show(EngineCharts.TorqueCurve(Performance));

    private bool HasPerformancePoints => Performance.Points.Count > 0;

    /// <summary>
    /// The camshaft profiles. Unlike the others this needs only the engine, not a
    /// completed run, so it is available as soon as a file is open.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasEngine))]
    private void ValveOpening()
    {
        var manifold = CurrentEngine!.Engine.Manifold;

        _charts.Show(EngineCharts.ValveLift(
            ValveMotion.Inlet(manifold.InletValve),
            ValveMotion.Exhaust(manifold.ExhaustValve)));
    }

    private bool HasEngine => CurrentEngine is not null;

    /// <summary>Accumulated heat loss against indicated and pumping work.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void EnergyBalance() => _charts.Show(EngineCharts.EnergyBalance(Trace!));

    /// <summary>The pressure-volume diagram.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void PressureVolume() => _charts.Show(EngineCharts.PressureVolume(Trace!));

    /// <summary>Pressure either side of the cylinder through the gas exchange.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void GasFlowPressure() =>
        _charts.Show(EngineCharts.GasFlowPressure(Trace!, CurrentEngine?.Engine.Rpm ?? 0));

    /// <summary>Gas velocity at each valve.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void GasFlowVelocity() => _charts.Show(EngineCharts.GasFlowVelocity(Trace!));

    /// <summary>The mass balance across the cycle.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void GasFlowMass() => _charts.Show(EngineCharts.GasFlowMass(Trace!));

    /// <summary>Cylinder pressure and zone temperatures over the closed period.</summary>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void InCylinder() => _charts.Show(EngineCharts.InCylinder(Trace!));

    private bool HasTrace => Trace is not null;

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
