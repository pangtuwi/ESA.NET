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
    private readonly ISimulateOptionsWindowService _simulateOptions;
    private readonly MultiRunner _multiRunner;
    private readonly IWorkspace _workspace;

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
        MultiRunner multiRunner,
        ISimulateOptionsWindowService simulateOptions,
        IWorkspace workspace)
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
        _simulateOptions = simulateOptions;
        _workspace = workspace;
    }

    /// <summary>
    /// Everything one finished run leaves behind: a folder of its own, holding copies of
    /// the files it read, its performance row, its PVT trace, the nine manifold files and
    /// a manifest.
    /// </summary>
    /// <remarks>
    /// The original wrote the manifold files and <c>SimulDat.txt</c> under bare relative
    /// names, so they landed in the working directory and the next run overwrote them
    /// (ISSUES.md C4, C6). A run that failed or was stopped still gets a folder, because
    /// the manifest saying why is the part worth keeping.
    /// </remarks>
    private string? ArchiveRun(
        DateTimeOffset startedAt,
        SimulationResult? result,
        ManifoldTraceWriter? manifoldWriter,
        TimeSpan elapsed,
        string outcome)
    {
        try
        {
            var archive = new RunArchive(_workspace.CreateRunDirectory(CurrentEngineFile, startedAt));

            var manifest = new RunManifest(startedAt)
                .Engine(
                    CurrentEngineFile,
                    CurrentEngine?.Engine.Name ?? string.Empty,
                    CurrentEngine?.Problems ?? [])
                .Requested(EngineSpeed, Settings)
                .Outcome(outcome, elapsed);

            if (result is not null)
            {
                manifest.Performance(result.Engine, result.CyclesRun);
                archive.AppendPerformance(result.Engine);
                archive.WriteTrace(result.Trace);
            }

            if (manifoldWriter is { RowCount: > 0 })
            {
                archive.WriteManifoldData(manifoldWriter);
            }

            manifest.Inputs(archive.CopyInputs(CurrentEngineFile, CurrentEngine));
            manifest.Write(archive.ManifestFile);

            LastRunDirectory = archive.Directory;

            return archive.Directory;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            RunStatus = $"The run folder could not be written: {error.Message}";
            return null;
        }
    }

    /// <summary>
    /// The folder the last run wrote to, which is where the PVT trace export offers to
    /// save from. Empty until something has been run.
    /// </summary>
    [ObservableProperty]
    private string _lastRunDirectory = string.Empty;

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

    /// <summary>
    /// Engine speed for a single-point run, in rev/min. Set from the Single Speed
    /// Simulation dialog; the original's main form has no speed control.
    /// </summary>
    [ObservableProperty]
    private double _engineSpeed = 4000;

    /// <summary>
    /// Which run-time charts the last run was asked for, Delphi <c>ShowGraphs</c>,
    /// <c>ShowFlowGraphs</c>, <c>ShowPVGraphs</c> and <c>ShowCylGraphs</c>.
    /// </summary>
    [ObservableProperty]
    private GraphSelection _runGraphs = new(true, true, true);

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
    [NotifyCanExecuteChangedFor(nameof(PvtTraceCommand))]
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
    private void EditEngine() =>
        _editor.Show(CurrentEngine!.Definition, CurrentEngineFile, RebuildEngineAfterEdit);

    /// <summary>
    /// Re-derives the engine from the definition the editor has just written to, so the
    /// next run uses what the operator actually set.
    /// </summary>
    /// <remarks>
    /// <c>EngineLoadResult</c> carries an <c>Engine</c> and an <c>EngineDefinition</c> that
    /// are separate snapshots, and the editor writes only to the definition. Without this
    /// the simulation keeps running on the values the file was opened with, whatever the
    /// operator changes - which is the port's own version of ISSUES.md C2, and worse than
    /// the original's, whose OK handler assigns onto the engine directly. Rebuilding also
    /// picks up any side file the operator renamed, and refreshes the load problems shown
    /// in the status line.
    /// </remarks>
    private void RebuildEngineAfterEdit()
    {
        if (CurrentEngine is null || string.IsNullOrEmpty(CurrentEngineFile))
        {
            return;
        }

        CurrentEngine = _engineLoader.Rebuild(CurrentEngine.Definition, CurrentEngineFile);
    }

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
        // The original asks for speed, cycles, mass balance and the graph options in a
        // modal dialog before doing anything, and abandons the run unless OK was pressed
        // (Main.pas:857). Its main form carries no run controls at all.
        var options = await _simulateOptions.ShowAsync(Settings, EngineSpeed);

        if (!options.Accepted)
        {
            return;
        }

        EngineSpeed = options.EngineSpeed;
        Settings.CycleCount = options.TotalCycles;
        Settings.MassBalance = options.MassBalance;

        // Delphi FormClose hard-codes No1zCycles to 1 whatever ESA.ini said.
        Settings.OneZoneCycleCount = 1;

        RunGraphs = options.Graphs;

        var engine = CurrentEngine!.Engine;
        engine.Rpm = EngineSpeed;

        _running = new CancellationTokenSource();
        IsRunning = true;
        RunStatus = "Simulating...";

        var startedAt = DateTimeOffset.Now;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var manifoldWriter = new ManifoldTraceWriter();

        SimulationResult? result = null;
        string outcome;

        try
        {
            var token = _running.Token;

            // Progress<T> marshals back to the UI thread for us.
            var progress = new Progress<SimulationProgress>(
                p => RunStatus =
                    $"Cycle {p.Cycle} of {p.RequestedCycles}   "
                    + $"{p.CrankAngle,4:F0}°   mass balance {p.MassBalance:F2} mg");

            // Every run archives its manifold files, so the engine's Save Manifold Data
            // flag no longer gates them - see ISSUES.md C1 to C4.
            result = await Task.Run(
                () => _runner.Run(
                    engine, Settings, progress, token,
                    manifoldRecorder: manifoldWriter, recordManifoldData: true),
                token);

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

            outcome = (result.Converged
                          ? $"Converged after {result.CyclesRun} cycles."
                          : $"Stopped at the requested {result.CyclesRun} cycles without converging.")
                      + $"   Torque {engine.Torque:F1} Nm   Power {engine.BrakePower / 1e3:F1} kW";
        }
        catch (OperationCanceledException)
        {
            outcome = "Stopped.";
        }
        catch (EngineException error)
        {
            outcome = $"Terminated on engine error: {error.Message}";
        }
        catch (CfdException error)
        {
            outcome = $"Terminated on CFD error: {error.Message}";
        }
        catch (EquilibriumException error)
        {
            outcome = $"Terminated on equilibrium error: {error.Message}";
        }
        catch (GasPropertiesException error)
        {
            outcome = $"Terminated on gas properties error: {error.Message}";
        }
        finally
        {
            IsRunning = false;
            _running.Dispose();
            _running = null;
        }

        // Outside the try: a stopped or failed run is archived too, because the manifest
        // saying what it was asked to do and how it ended is the part worth keeping.
        var directory = ArchiveRun(startedAt, result, manifoldWriter, stopwatch.Elapsed, outcome);

        RunStatus = directory is null
            ? outcome
            : $"{outcome}   Results in {directory}";
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

        var startedAt = DateTimeOffset.Now;
        var multiStopwatch = System.Diagnostics.Stopwatch.StartNew();

        // One folder for the whole sweep, with a subfolder per row inside it. The sweep
        // wrote no manifold data at all until it had somewhere to put it - ISSUES.md A12,
        // which was left open for want of exactly this destination.
        var sweep = OpenSweepFolder(startedAt);

        var manifest = sweep is null
            ? null
            : new RunManifest(startedAt)
                .Engine(CurrentEngineFile, CurrentEngine!.Engine.Name, CurrentEngine.Problems)
                .RequestedSweep(MultiRun.RunCount, Settings);

        var results = new List<MultiRunRowResult>();
        string outcome;

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
            var rows = MultiRun.RunCount;

            for (var row = 0; row < rows; row++)
            {
                var index = row;
                var manifoldWriter = new ManifoldTraceWriter();

                var completed = await Task.Run(
                    () => _multiRunner.RunRow(
                        path, MultiRun, index, Settings, progress, token, manifoldWriter),
                    token);

                results.Add(completed);
                ArchiveRow(sweep, manifest, completed, manifoldWriter);
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

            outcome = failed == 0
                ? $"Completed {results.Count} runs."
                : $"Completed {results.Count - failed} of {results.Count} runs; "
                  + $"{failed} failed. First failure: "
                  + results.First(r => r.Failure is not null).Failure;
        }
        catch (OperationCanceledException)
        {
            outcome = $"Stopped after {Performance.Points.Count} runs.";
        }
        finally
        {
            IsRunning = false;
            _running.Dispose();
            _running = null;
        }

        CloseSweepFolder(sweep, manifest, outcome, multiStopwatch.Elapsed);

        RunStatus = sweep is null
            ? outcome
            : $"{outcome}   Results in {sweep.Directory}";
    }

    /// <summary>Creates the folder a whole sweep writes into, or null if it cannot be.</summary>
    private RunArchive? OpenSweepFolder(DateTimeOffset startedAt)
    {
        try
        {
            return new RunArchive(_workspace.CreateRunDirectory(CurrentEngineFile, startedAt));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            RunStatus = $"The run folder could not be written: {error.Message}";
            return null;
        }
    }

    /// <summary>
    /// Writes one row's output into a subfolder of the sweep's, and its performance row
    /// into the sweep's own <c>SimulDat.txt</c> - the one place the original's appending
    /// (ISSUES.md C6) earns its keep, since every row there belongs to the same sweep.
    /// </summary>
    private void ArchiveRow(
        RunArchive? sweep, RunManifest? manifest, MultiRunRowResult row, ManifoldTraceWriter manifoldWriter)
    {
        if (sweep is null)
        {
            return;
        }

        try
        {
            var folder = RunFolderName.ForRow(row.Row, row.Speed);
            manifest?.Row(row.Row, row.Speed, folder, row);

            if (row.Result is not { } result)
            {
                return;
            }

            var archive = sweep.Row(row.Row, row.Speed);

            archive.WriteTrace(result.Trace);
            sweep.AppendPerformance(result.Engine);

            if (manifoldWriter.RowCount > 0)
            {
                archive.WriteManifoldData(manifoldWriter);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            RunStatus = $"Row {row.Row + 1} could not be written: {error.Message}";
        }
    }

    /// <summary>Copies the sweep's inputs in and writes its manifest.</summary>
    private void CloseSweepFolder(
        RunArchive? sweep, RunManifest? manifest, string outcome, TimeSpan elapsed)
    {
        if (sweep is null || manifest is null)
        {
            return;
        }

        try
        {
            manifest.Outcome(outcome, elapsed);
            manifest.Inputs(sweep.CopyInputs(CurrentEngineFile, CurrentEngine));
            manifest.Write(sweep.ManifestFile);

            LastRunDirectory = sweep.Directory;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            RunStatus = $"The run folder could not be completed: {error.Message}";
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

        // The Single Speed Simulation dialog decides which of the three the run draws, as
        // ShowFlowGraphs, ShowPVGraphs and ShowCylGraphs do in the original. A quadrant
        // that was not asked for is left empty rather than drawn anyway.
        PressureVolumeChart = RunGraphs.PressureVolume
            ? EngineCharts.PressureVolume(trace)
            : null;

        GasFlowChart = RunGraphs.GasFlow
            ? ShowGasFlowVelocities
                ? EngineCharts.GasFlowVelocity(trace, events)
                : EngineCharts.GasFlowPressure(trace, engine?.Rpm ?? 0, events)
            : null;

        InCylinderChart = RunGraphs.InCylinder
            ? EngineCharts.InCylinder(trace)
            : null;
    }

    // Text. Delphi: PVTTrace1Click.

    /// <summary>
    /// Saves the last run's full-cycle PVT trace where the operator asks. Port of
    /// <c>PVTTrace1Click</c>, which shows the trace in a window with a Save As of its own
    /// (<c>TCAList.SendToFile</c>).
    /// </summary>
    /// <remarks>
    /// Every run already writes this file into its own run folder, so this is a copy out
    /// of the archive rather than the only chance to keep it - which is what the original
    /// offered, and why <c>Lastcyc.txt</c> was so easily lost.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasTrace))]
    private async Task PvtTraceAsync()
    {
        if (await _files.SaveTextAsync("PVT Trace", RunArchive.TraceFileName, LastRunDirectory)
            is not { } path)
        {
            return;
        }

        try
        {
            new CrankAngleTraceWriter().Write(path, Trace!);
            RunStatus = $"PVT trace written to {path}.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            RunStatus = $"The PVT trace could not be written: {error.Message}";
        }
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
