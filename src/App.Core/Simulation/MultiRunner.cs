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
    public IReadOnlyList<MultiRunRowResult> Run(
        string enginePath,
        MultiRunGrid grid,
        SimulationSettings settings,
        IProgress<MultiRunProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(enginePath);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);

        var results = new List<MultiRunRowResult>();
        var rows = grid.RunCount;

        for (var row = 0; row < rows; row++)
        {
            cancellation.ThrowIfCancellationRequested();

            var speed = grid.Speed(row) ?? 0;

            try
            {
                var engine = _loader.Load(enginePath).Engine;
                var (rowSettings, afterInitialise) = ApplyRow(engine, grid, row, settings);
                var index = row;

                var inner = progress is null
                    ? null
                    : new RelayProgress<SimulationProgress>(
                        p => progress.Report(new MultiRunProgress(index, rows, speed, p)));

                results.Add(new MultiRunRowResult(
                    row,
                    engine.Rpm,
                    _runner.Run(engine, rowSettings, inner, cancellation, afterInitialise),
                    null));
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
                results.Add(new MultiRunRowResult(row, speed, null, error.Message));
            }
        }

        return results;
    }

    /// <summary>
    /// Applies one row's overrides to a freshly loaded engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things here are wrong in the original and are reproduced. The valve timings
    /// in columns 7 to 10 are assigned <b>raw</b>, bypassing the conversion the edit form
    /// applies, so a row saying IVO 19 sets the opening angle to 19 rather than 341
    /// (ISSUES.md B72). Setting the inlet lift in column 11 also overwrites the
    /// <b>exhaust</b> lift with the same value (B73). And column 12's exhaust lift is not
    /// divided by a thousand as column 11's is, so it stays in millimetres where
    /// everything else is metres (B74).
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

        if (grid.Text(row, 2) is { } inletArea)
        {
            manifold.InletPipe.AreaVersusLength.FileName = inletArea;
        }

        if (grid.Text(row, 3) is { } exhaustArea)
        {
            manifold.ExhaustPipe.AreaVersusLength.FileName = exhaustArea;
        }

        if (grid.Text(row, 4) is { } inletCam)
        {
            manifold.InletValve.ProfileFile = inletCam;
        }

        if (grid.Text(row, 5) is { } exhaustCam)
        {
            manifold.ExhaustValve.ProfileFile = exhaustCam;
        }

        // Raw, as the original assigns them. See B72.
        if (grid.Number(row, 6) is { } inletOpen)
        {
            manifold.InletValve.OpenAngle = inletOpen;
        }

        if (grid.Number(row, 7) is { } inletClose)
        {
            manifold.InletValve.CloseAngle = inletClose;
        }

        if (grid.Number(row, 8) is { } exhaustOpen)
        {
            manifold.ExhaustValve.OpenAngle = exhaustOpen;
        }

        if (grid.Number(row, 9) is { } exhaustClose)
        {
            manifold.ExhaustValve.CloseAngle = exhaustClose;
        }

        if (grid.Number(row, 10) is { } inletLift)
        {
            // Both of these, in this order. See B73.
            manifold.InletValve.MaxLift = inletLift / 1000;
            manifold.ExhaustValve.MaxLift = manifold.InletValve.MaxLift;
        }

        if (grid.Number(row, 11) is { } exhaustLift)
        {
            // Not divided. See B74.
            manifold.ExhaustValve.MaxLift = exhaustLift;
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
        }, afterInitialise);
    }

    private sealed class RelayProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
