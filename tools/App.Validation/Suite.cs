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

/// <summary>One thesis figure.</summary>
/// <param name="Data">The thesis's plotted series, relative to the repository root.</param>
/// <param name="CorrelateFrom">The speed below which the thesis discounts its own correlation.</param>
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
    IReadOnlyList<SeriesSpec> Series);

/// <param name="Variable"><c>rpm</c>, <c>spark</c> or <c>crank</c>; x axes only.</param>
internal sealed record AxisSpec(string? Variable, string Label, double Min, double Max);

/// <summary>One simulated series and the two thesis series it is compared with.</summary>
/// <param name="Output"><c>torque</c>, <c>power</c>, <c>torque_delta</c> or <c>inlet_pressure</c>.</param>
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
