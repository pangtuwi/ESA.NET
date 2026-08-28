using App.Core;
using App.Core.Charts;
using App.Core.Model;
using App.Core.Interpolation;
using App.Core.Simulation;
using App.Persistence;
using App.Ui.Charts;
using App.Ui.Dialogs;
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
    private readonly IFileDialogService _files;
    private readonly IEditEngineWindowService _editor;
    private readonly IMultiRunWindowService _multiRunEditor;
    private readonly MultiRunner _multiRunner;

    private CancellationTokenSource? _running;

    public MainWindowViewModel(
        IEngineLoader engineLoader,
        IEngineDefinitionStore definitions,
        IChartWindowService charts,
        SimulationRunner runner,
        ISimulationSettingsStore settingsStore,
        IFileDialogService files,
        IEditEngineWindowService editor,
        IMultiRunWindowService multiRunEditor,
        MultiRunner multiRunner)
    {
        _engineLoader = engineLoader;
        _definitions = definitions;
        _charts = charts;
        _runner = runner;
        _settingsStore = settingsStore;
        _files = files;
        _editor = editor;
        _multiRunEditor = multiRunEditor;
        _multiRunner = multiRunner;
    }

    /// <summary>
    /// The multi-run table. Carried between openings of the editor, as the original's
    /// single <c>TFMultiRun</c> instance keeps whatever was last typed into it.
    /// </summary>
    [ObservableProperty]
    private MultiRunGrid _multiRun = new();

    /// <summary>
    /// Delphi <c>ShowGraphs</c>, set from the grid editor's check box: whether the charts
    /// follow the sweep row by row or are drawn once at the end.
    /// </summary>
    [ObservableProperty]
    private bool _showGraphsDuringSweep = true;

    /// <summary>Run options, as ESA.ini carries them.</summary>
    public SimulationSettings Settings { get; } = new();

    /// <summary>Engine speed for a single-point run, in rev/min.</summary>
    [ObservableProperty]
    private double _engineSpeed = 4000;

    /// <summary>The headline figures, shown in the top-left panel.</summary>
    public SimulationResultsViewModel Results { get; } = new();

    /// <summary>The P-V diagram, shown in the top-right quadrant.</summary>
    [ObservableProperty]
    private ChartDefinition? _pressureVolumeChart;

    /// <summary>The gas-flow chart, shown in the bottom-left quadrant.</summary>
    [ObservableProperty]
    private ChartDefinition? _gasFlowChart;

    /// <summary>The in-cylinder chart, shown in the bottom-right quadrant.</summary>
    [ObservableProperty]
    private ChartDefinition? _inCylinderChart;

    /// <summary>
    /// Whether the gas-flow quadrant shows velocities rather than pressures. The original
    /// offers the same choice on its run-time graph options dialog.
    /// </summary>
    [ObservableProperty]
    private bool _showGasFlowVelocities;

    partial void OnShowGasFlowVelocitiesChanged(bool value) => RefreshEmbeddedCharts();

    /// <summary>What the simulation is doing, for the status bar.</summary>
    [ObservableProperty]
    private string _runStatus = string.Empty;

    /// <summary>Whether a simulation is in progress, which disables starting another.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SinglePointSimulationCommand))]
    [NotifyCanExecuteChangedFor(nameof(MultiPointSimulationCommand))]
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
    [NotifyCanExecuteChangedFor(nameof(MultiPointSimulationCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditEngineCommand))]
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

    /// <summary>Opens an engine file. Port of <c>Load1Click</c>.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (await _files.OpenEngineAsync() is not { } path)
        {
            return;
        }

        LoadEngineReportingFailure(path);
    }

    /// <summary>Saves the current definition under a new name. Port of <c>SaveAs1Click</c>.</summary>
    [RelayCommand(CanExecute = nameof(HasEngine))]
    private async Task SaveAsAsync()
    {
        var suggested = Path.GetFileName(CurrentEngineFile) is { Length: > 0 } name
            ? name
            : "Engine.eng";

        if (await _files.SaveEngineAsync(suggested) is { } path)
        {
            SaveEngineAs(path);
            RunStatus = $"Saved {Path.GetFileName(path)}.";
        }
    }

    /// <summary>Opens the eight-tab editor on the current engine. Port of <c>Edit1Click</c>.</summary>
    [RelayCommand(CanExecute = nameof(HasEngine))]
    private void EditEngine() => _editor.Show(CurrentEngine!.Definition, CurrentEngineFile);

    /// <summary>
    /// Opens whatever <c>ESA.ini</c> names as the default engine. Port of
    /// <c>LoadDefault1Click</c>.
    /// </summary>
    [RelayCommand]
    private void LoadDefault()
    {
        // ESA.ini sits beside the executable, as it did beside ESA.EXE. A missing file is
        // not an error - the store returns the same defaults Delphi's TIniFile would.
        var settings = _settingsStore.Read(
            Path.Combine(AppContext.BaseDirectory, SimulationSettingsStore.FileName));

        var name = settings.EngineFileName;

        if (string.IsNullOrWhiteSpace(name))
        {
            RunStatus = "No default engine is named in ESA.ini.";
            return;
        }

        // The entry may be a bare name, so look beside the executable before giving up.
        var path = File.Exists(name)
            ? name
            : Path.Combine(AppContext.BaseDirectory, name);

        if (!File.Exists(path))
        {
            RunStatus = $"The default engine named in ESA.ini was not found: {name}";
            return;
        }

        LoadEngineReportingFailure(path);
    }

    /// <summary>
    /// Loads a file and reports why if it will not open, rather than letting the
    /// exception reach the dispatcher and close the application.
    /// </summary>
    private void LoadEngineReportingFailure(string path)
    {
        try
        {
            LoadEngine(path);

            RunStatus = CurrentEngine!.Problems.Count == 0
                ? $"Opened {Path.GetFileName(path)}."
                : $"Opened {Path.GetFileName(path)} with {CurrentEngine.Problems.Count} problem(s): "
                  + string.Join("; ", CurrentEngine.Problems);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                          or LegacyDataException or FormatException)
        {
            RunStatus = $"Could not open {Path.GetFileName(path)}: {error.Message}";
        }
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
            Results.Update(engine, result.CyclesRun, Settings.CycleCount, stopwatch.Elapsed);
            RefreshEmbeddedCharts();

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

    /// <summary>
    /// Opens the multi-run grid, then runs every row of it building a torque curve. Port
    /// of <c>MultiPointSimulation1Click</c>, which shows <c>FMultiRun</c> modally and
    /// exits if the operator does not press OK - there is no separate menu item for the
    /// grid in the original either.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartMultiRun))]
    private async Task MultiPointSimulationAsync()
    {
        var edit = await _multiRunEditor.ShowAsync(MultiRun, CurrentEngineFile);

        MultiRun = edit.Grid;
        ShowGraphsDuringSweep = edit.ShowGraphs;

        if (!edit.Accepted)
        {
            return;
        }

        if (MultiRun.RunCount == 0)
        {
            RunStatus = "The grid holds no runs. Fill in the Speed column from the first row down.";
            return;
        }

        _running = new CancellationTokenSource();
        IsRunning = true;

        // The original clears the curve before a sweep, so it shows this run and not an
        // accumulation of every run before it.
        Performance.Points.Clear();
        TorqueCurveCommand.NotifyCanExecuteChanged();

        var multiStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var token = _running.Token;
            var path = CurrentEngineFile;

            var progress = new Progress<MultiRunProgress>(
                p => RunStatus =
                    $"Run {p.Row + 1} of {p.TotalRows} at {p.Speed:F0} rev/min   "
                    + $"cycle {p.Inner.Cycle}   {p.Inner.CrankAngle,4:F0}°");

            // A row at a time, awaited individually, so the curve builds as the sweep
            // proceeds - the original adds its performance point and redraws inside the
            // loop. Driving the loop from here rather than passing a callback into the
            // runner keeps every collection touch on this thread.
            var results = new List<MultiRunRowResult>();
            var rows = MultiRun.RunCount;

            for (var row = 0; row < rows; row++)
            {
                var index = row;
                var completed = await Task.Run(
                    () => _multiRunner.RunRow(path, MultiRun, index, Settings, progress, token),
                    token);

                results.Add(completed);
                RowFinished(completed, multiStopwatch);
            }

            // Delphi redraws once more with the graphs forced back on at the end of the
            // sweep, so the charts show the last row whatever Show Graphs was set to.
            var last = results.LastOrDefault(r => r.Result is not null)?.Result;

            if (last is not null && !ShowGraphsDuringSweep)
            {
                Trace = last.Trace;
                Results.Update(last.Engine, last.CyclesRun, Settings.CycleCount, multiStopwatch.Elapsed);
                RefreshEmbeddedCharts();
            }

            TorqueCurveCommand.NotifyCanExecuteChanged();

            var failed = results.Count(r => r.Failure is not null);

            RunStatus = failed == 0
                ? $"Completed {results.Count} runs."
                : $"Completed {results.Count - failed} of {results.Count} runs; "
                  + $"{failed} failed. First failure: "
                  + results.First(r => r.Failure is not null).Failure;
        }
        catch (OperationCanceledException)
        {
            RunStatus = $"Stopped after {Performance.Points.Count} runs.";
        }
        finally
        {
            IsRunning = false;
            _running.Dispose();
            _running = null;
        }
    }

    /// <summary>
    /// One row of the sweep has finished. Adds its point to the curve and, when Show
    /// Graphs is on, brings the charts up to date with it.
    /// </summary>
    private void RowFinished(MultiRunRowResult row, System.Diagnostics.Stopwatch elapsed)
    {
        if (row.Result is not { } result)
        {
            return;
        }

        var engine = result.Engine;

        Performance.Points.Add(new PerformancePoint
        {
            Speed = row.Speed,
            Torque = engine.Torque,
            Power = engine.BrakePower / 1e3,
            VolumetricEfficiency = engine.VolumetricEfficiency,
        });

        TorqueCurveCommand.NotifyCanExecuteChanged();

        if (!ShowGraphsDuringSweep)
        {
            return;
        }

        Trace = result.Trace;
        Results.Update(engine, result.CyclesRun, Settings.CycleCount, elapsed.Elapsed);
        RefreshEmbeddedCharts();
    }

    // The grid is filled in by the editor the command itself opens, so an empty one is no
    // reason to disable it.
    private bool CanStartMultiRun => CurrentEngine is not null && !IsRunning;

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

    /// <summary>
    /// Rebuilds the three charts embedded in the main window. The original redraws them
    /// the same way at the end of a run, walking every even crank angle.
    /// </summary>
    private void RefreshEmbeddedCharts()
    {
        if (Trace is not { } trace)
        {
            return;
        }

        var engine = CurrentEngine?.Engine;
        var events = engine is null
            ? null
            : CrankAngleStateMap.FromEngine(
                engine,
                LegacyInterpolation.AtSpeed(
                    engine.SparkAngle.Rpm, engine.SparkAngle.Values, engine.Rpm));

        PressureVolumeChart = EngineCharts.PressureVolume(trace);

        GasFlowChart = ShowGasFlowVelocities
            ? EngineCharts.GasFlowVelocity(trace, events)
            : EngineCharts.GasFlowPressure(trace, engine?.Rpm ?? 0, events);

        InCylinderChart = EngineCharts.InCylinder(trace);
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
