using System.Text.Json;

namespace App.Validation;

/// <summary><c>validation/suite.json</c>: the figures, and the case behind each series.</summary>
internal sealed record Suite(string Description, int Cycles, IReadOnlyList<FigureSpec> Figures)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Suite Load(string path) =>
        JsonSerializer.Deserialize<Suite>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"{path} is empty.");
}

/// <summary>
/// One thesis figure, or one test figure: a channel beyond torque from the test sheets in the
/// thesis's source workbooks (<c>CORRELATION.md</c>, step 1), whose id starts with <c>T</c>.
/// </summary>
/// <param name="Data">The thesis's plotted series, relative to the repository root.</param>
/// <param name="CorrelateFrom">The speed below which the thesis discounts its own correlation.</param>
/// <param name="Source">For a test figure, the workbook and sheets its data comes from.</param>
internal sealed record FigureSpec(
    string Id,
    string Section,
    int Page,
    string Title,
    string Data,
    AxisSpec X,
    AxisSpec Y,
    AxisSpec? Y2,
    double? CorrelateFrom,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<SeriesSpec> Series,
    string? Source = null)
{
    /// <summary>"Figure 6.12" for a thesis figure, "Test 1" for test figure T1.</summary>
    public string Label => Id.StartsWith('T') ? "Test " + Id[1..] : "Figure " + Id;
}

/// <param name="Variable"><c>rpm</c>, <c>spark</c> or <c>crank</c>; x axes only.</param>
internal sealed record AxisSpec(string? Variable, string Label, double Min, double Max);

/// <summary>One simulated series and the two thesis series it is compared with.</summary>
/// <param name="Output">
/// <c>torque</c>, <c>power</c>, <c>torque_delta</c>, <c>inlet_pressure</c>, <c>air_flow</c>
/// (kg/h: fuel flow x lambda x AFRatio) or <c>work_per_air</c> (brake power over air flow, kJ/kg).
/// </param>
/// <param name="Baseline">For <c>torque_delta</c>, the grid subtracted.</param>
/// <param name="Station">For <c>inlet_pressure</c>, where along the pipe; only <c>valve</c>.</param>
internal sealed record SeriesSpec(
    string Name,
    string Case,
    string Grid,
    string Output,
    string Axis,
    string Measured,
    string ThesisModel,
    string? Baseline,
    string? Station);
