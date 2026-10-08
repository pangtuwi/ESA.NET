using App.Core.Manifold;
using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>One row's outcome.</summary>
/// <param name="Row">Zero-based grid row.</param>
/// <param name="Speed">Engine speed simulated.</param>
/// <param name="Result">What the run produced, or null if the row failed.</param>
/// <param name="Failure">Why the row failed, or null if it succeeded.</param>
public sealed record MultiRunRowResult(
    int Row, double Speed, SimulationResult? Result, string? Failure);

/// <summary>Progress across a multi-run.</summary>
public readonly record struct MultiRunProgress(
    int Row, int TotalRows, double Speed, SimulationProgress Inner);

/// <summary>
/// Runs every row of a multi-run grid. Port of the loop in <c>TFMain.MultiRunSimulate</c>
/// (Main.pas:1314-1392).
/// </summary>
/// <remarks>
/// Each row starts from a freshly loaded engine, as the original creates a new
/// <c>TEngine2z</c> and re-reads the edit form every iteration, so overrides from one row
/// never leak into the next. A row that fails is recorded and the sweep continues, which
/// is what the original's try/except around each iteration does.
/// </remarks>
public sealed class MultiRunner
{
    private readonly IEngineLoader _loader;
    private readonly SimulationRunner _runner;

    public MultiRunner(IEngineLoader loader, SimulationRunner runner)
    {
        _loader = loader;
        _runner = runner;
    }

    /// <summary>Runs every populated row of <paramref name="grid"/>.</summary>
    /// <param name="enginePath">The engine each row starts from.</param>
    /// <param name="manifoldRecorderFor">
    /// Where each row's manifold rows go, asked for by row index so a sweep can give every
    /// row a recorder of its own. Null records nothing, which is what the sweep did for as
    /// long as it had nowhere to put the files (ISSUES.md A12).
    /// </param>
    public IReadOnlyList<MultiRunRowResult> Run(
        string enginePath,
        MultiRunGrid grid,
        SimulationSettings settings,
        IProgress<MultiRunProgress>? progress = null,
        CancellationToken cancellation = default,
        Func<int, IManifoldRecorder?>? manifoldRecorderFor = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var results = new List<MultiRunRowResult>();

        for (var row = 0; row < grid.RunCount; row++)
        {
            results.Add(RunRow(
                enginePath, grid, row, settings, progress, cancellation,
                manifoldRecorderFor?.Invoke(row)));
        }

        return results;
    }

    /// <summary>
    /// Runs one row of <paramref name="grid"/>, from a freshly loaded engine.
    /// </summary>
    /// <remarks>
    /// Split out from <see cref="Run"/> so a caller can drive the sweep a row at a time
    /// and act on each result as it arrives - the original adds its performance point and
    /// redraws inside the loop (<c>Main.pas:1355-1380</c>) rather than at the end.
    /// </remarks>
    /// <param name="enginePath">The engine the row starts from.</param>
    /// <param name="row">Zero-based grid row, which must be below <see cref="MultiRunGrid.RunCount"/>.</param>
    /// <param name="manifoldRecorder">
    /// Where this row's manifold rows go. The sweep wrote none at all until it had a
    /// folder per row to put them in - see ISSUES.md A12, which was left open for want of
    /// exactly that destination.
    /// </param>
    /// <param name="pause">Run ▸ Pause, passed through to the row's run.</param>
    public MultiRunRowResult RunRow(
        string enginePath,
        MultiRunGrid grid,
        int row,
        SimulationSettings settings,
        IProgress<MultiRunProgress>? progress = null,
        CancellationToken cancellation = default,
        IManifoldRecorder? manifoldRecorder = null,
        RunPause? pause = null)
    {
        ArgumentNullException.ThrowIfNull(enginePath);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);

        cancellation.ThrowIfCancellationRequested();

        var rows = grid.RunCount;
        var speed = grid.Speed(row) ?? 0;

        try
        {
            var loaded = LoadRowEngine(enginePath, grid, row, out var sideFileFailure);

            if (sideFileFailure is not null)
            {
                return new MultiRunRowResult(row, speed, null, sideFileFailure);
            }

            var engine = loaded.Engine;
            var (rowSettings, afterInitialise) = ApplyRow(engine, grid, row, settings);

            var inner = progress is null
                ? null
                : new RelayProgress<SimulationProgress>(
                    p => progress.Report(new MultiRunProgress(row, rows, speed, p)));

            return new MultiRunRowResult(
                row,
                engine.Rpm,
                _runner.Run(
                    engine, rowSettings, inner, cancellation, afterInitialise, manifoldRecorder,
                    // Every row archives its manifold files, as every single-point run
                    // does; the engine's own flag no longer gates them.
                    recordManifoldData: manifoldRecorder is not null,
                    pause: pause),
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is EngineException or CfdException
                                          or EquilibriumException or GasPropertiesException
                                          or FormatException or IOException)
        {
            // The original writes "Error in Multirun Command Line n" and carries on to
            // the next row rather than abandoning the sweep.
            return new MultiRunRowResult(row, speed, null, error.Message);
        }
    }

    /// <summary>
    /// Loads the engine for one row, reading the side files that grid columns 2 to 5 name
    /// in place of the engine's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original set <c>AInManf.AFileName</c> and the other three from the grid
    /// (<c>Main.pas:1329-1332</c>) and it was <c>InitVars</c> that read them
    /// (<c>ICEngine2Z.pas:994-1009</c>). Here the tables are read when the engine is loaded,
    /// and Core reads no files, so the row's names go onto the definition and the engine is
    /// rebuilt from it through the loader, which resolves them against the engine file
    /// exactly as it resolves the <c>.eng</c>'s own. Assigning the names onto the loaded
    /// engine, which is what the port first did, ran every row on the base engine's
    /// tables (ISSUES.md A30).
    /// </para>
    /// <para>
    /// The definition is the one this row's load just read and is never written back, so
    /// nothing reaches the <c>.eng</c> file or the next row.
    /// </para>
    /// <para>
    /// A side file the engine itself names but cannot find stays the warning it is for a
    /// single-point run. One the grid names is the row's whole point, so a row whose file
    /// cannot be found or read fails, rather than run silently on whatever the engine had.
    /// </para>
    /// </remarks>
    private EngineLoadResult LoadRowEngine(
        string enginePath, MultiRunGrid grid, int row, out string? failure)
    {
        failure = null;

        var loaded = _loader.Load(enginePath);
        var definition = loaded.Definition;

        // The kinds are the loader's own words for each table, as ResolvedSideFile carries
        // them. Writing [Inlet] and [Exhaust] serves the older schema too: the effective
        // area file looks there before [InManifold] and [ExManifold].
        var overrides = new List<(string Kind, string Stored)>();

        if (grid.Text(row, 2) is { } inletArea)
        {
            definition.InletAreaFile = inletArea;
            overrides.Add(("inlet manifold area", inletArea));
        }

        if (grid.Text(row, 3) is { } exhaustArea)
        {
            definition.ExhaustAreaFile = exhaustArea;
            overrides.Add(("exhaust manifold area", exhaustArea));
        }

        if (grid.Text(row, 4) is { } inletCam)
        {
            definition.InletValveProfileFile = inletCam;
            overrides.Add(("inlet cam profile", inletCam));
        }

        if (grid.Text(row, 5) is { } exhaustCam)
        {
            definition.ExhaustValveProfileFile = exhaustCam;
            overrides.Add(("exhaust cam profile", exhaustCam));
        }

        if (overrides.Count == 0)
        {
            return loaded;
        }

        loaded = _loader.Rebuild(definition, enginePath);

        foreach (var (kind, stored) in overrides)
        {
            var read = loaded.SideFiles.Any(file =>
                file.Kind == kind && string.Equals(file.Stored, stored.Trim(), StringComparison.Ordinal));

            if (!read)
            {
                var reason = loaded.Problems.FirstOrDefault(
                    problem => problem.Contains($"the {kind} file", StringComparison.Ordinal));

                failure = $"Row {row + 1} names the {kind} file '{stored}', which could not be loaded."
                          + (reason is null ? string.Empty : $" {reason}");

                return loaded;
            }
        }

        return loaded;
    }

    /// <summary>
    /// Applies one row's overrides to a freshly loaded engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grid is typed in the units of the Cams tab and the <c>.eng</c> file - degrees
    /// before or after a dead centre, millimetres of lift - and <see cref="Engine"/> holds
    /// SI, so every override is converted through <see cref="EngineFileUnits"/>, the same
    /// conversions the loader applies (ISSUES.md A6). A row that restates the engine's own
    /// values therefore runs the engine unchanged.
    /// </para>
    /// <para>
    /// The original got three of these wrong, and none is reproduced. It assigned the
    /// timings in columns 7 to 10 raw onto its already-converted angles, so IVO 19 opened
    /// the valve at 19 rather than 341 (B72); copied column 11's inlet lift onto the
    /// exhaust valve as well (B73); and left column 12's exhaust lift in millimetres where
    /// everything else was metres (B74). The port's first version divided column 11 by a
    /// thousand on top of the downstream conversion, leaving the inlet lift a thousand
    /// times too small (A17).
    /// </para>
    /// <para>
    /// The cycle counts follow the original's arithmetic: <c>No2z := NoCycles-1</c> then
    /// <c>No1zCycles := NoCycles - No2z</c>, which is always one.
    /// </para>
    /// </remarks>
    private static (SimulationSettings Settings, Action<Engine>? AfterInitialise) ApplyRow(
        Engine engine, MultiRunGrid grid, int row, SimulationSettings settings)
    {
        var manifold = engine.Manifold;

        engine.Rpm = grid.Speed(row) ?? engine.Rpm;

        var cycles = (int)(grid.Cycles(row) ?? settings.CycleCount);

        // Converted exactly as the loader converts the .eng's own values. See B72.
        if (grid.Number(row, 6) is { } inletOpen)
        {
            manifold.InletValve.OpenAngle = EngineFileUnits.InletOpen(inletOpen);
        }

        if (grid.Number(row, 7) is { } inletClose)
        {
            manifold.InletValve.CloseAngle = EngineFileUnits.InletClose(inletClose);
        }

        if (grid.Number(row, 8) is { } exhaustOpen)
        {
            manifold.ExhaustValve.OpenAngle = EngineFileUnits.ExhaustOpen(exhaustOpen);
        }

        if (grid.Number(row, 9) is { } exhaustClose)
        {
            manifold.ExhaustValve.CloseAngle = EngineFileUnits.ExhaustClose(exhaustClose);
        }

        // Typed in millimetres; each lift touches its own valve only. See B73, B74, A17.
        if (grid.Number(row, 10) is { } inletLift)
        {
            manifold.InletValve.MaxLift = EngineFileUnits.Length(inletLift);
        }

        if (grid.Number(row, 11) is { } exhaustLift)
        {
            manifold.ExhaustValve.MaxLift = EngineFileUnits.Length(exhaustLift);
        }

        // Spark and burn angle are applied after initialisation, because that is where
        // the original applies them - InitVars sits between column 12 and column 13, and
        // it is InitVars that derives the spark angle from the .spk map. Setting them any
        // earlier would simply be overwritten.
        var spark = grid.Number(row, 12);
        var burnAngle = grid.Number(row, 13);

        Action<Engine>? afterInitialise = spark is null && burnAngle is null
            ? null
            : initialised =>
            {
                if (spark is { } advance)
                {
                    initialised.Cylinder.ThetaSpark = -advance;
                }

                if (burnAngle is { } angle)
                {
                    initialised.Cylinder.Fuel.BurnAngle = angle;
                }
            };

        return (new SimulationSettings
        {
            CycleCount = cycles,
            OneZoneCycleCount = 1,
            MassBalance = settings.MassBalance,
            EngineSpeed = engine.Rpm,

            // Every row runs on the physics the sweep was started with.
            Physics = settings.Physics.Clone(),
        }, afterInitialise);
    }

    private sealed class RelayProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
