namespace App.Validation;

/// <summary>A grid row that did not run, at the x it would have been plotted at.</summary>
internal sealed record Failure(double X, string Message);

/// <summary>One simulated series against the thesis's two.</summary>
/// <param name="Slot">The series' colour slot within its figure, 1-based.</param>
/// <param name="Previous">The same series from an earlier report, when one was given.</param>
/// <param name="VersusMeasured">ESA.NET now against the dynamometer.</param>
/// <param name="ThesisVersusMeasured">The thesis's own 2002 model against the dynamometer: the bar.</param>
/// <param name="VersusThesis">ESA.NET now against the 2002 model.</param>
/// <param name="PreviousVersusMeasured">The earlier report against the dynamometer.</param>
internal sealed record SeriesResult(
    SeriesSpec Spec,
    int Slot,
    IReadOnlyList<Point> Measured,
    IReadOnlyList<Point> Thesis,
    IReadOnlyList<Point> Esa,
    IReadOnlyList<Point>? Previous,
    IReadOnlyList<Failure> Failures,
    int Rows,
    Comparison? VersusMeasured,
    Comparison? ThesisVersusMeasured,
    Comparison? VersusThesis,
    Comparison? PreviousVersusMeasured)
{
    public bool Relative => Spec.Output is "torque" or "power";

    public string Unit => Spec.Output switch
    {
        "power" => "kW",
        "inlet_pressure" => "bar",
        _ => "Nm",
    };

    /// <summary>Ran, ran with gaps, or did not run.</summary>
    public string Status => Failures.Count == 0 ? "ran" : Esa.Count == 0 ? "did not run" : "partly ran";
}

internal sealed record FigureResult(FigureSpec Spec, IReadOnlyList<SeriesResult> Series)
{
    public bool IsTimingLoop => Spec.X.Variable == "spark";
}

/// <summary>Turns grid rows into each figure's series and compares them.</summary>
internal static class Evaluation
{
    public static FigureResult Evaluate(
        FigureSpec figure,
        ThesisData thesis,
        IReadOnlyDictionary<(string Case, string Grid), IReadOnlyList<RowResult>> runs,
        Func<string, string, IReadOnlyList<Point>?> previous)
    {
        var slots = new Dictionary<string, int>();
        var series = new List<SeriesResult>();

        foreach (var spec in figure.Series)
        {
            // Torque and power of one engine are one entity in two charts: one colour.
            var entity = figure.Series.Any(s => s.Output == "power") ? spec.Case + "/" + spec.Grid : spec.Name;
            if (!slots.TryGetValue(entity, out var slot))
            {
                slots[entity] = slot = slots.Count + 1;
            }

            var rows = runs[(spec.Case, spec.Grid)];
            var (esa, failures) = Simulated(figure, spec, rows, runs);
            var measured = thesis.Measured(spec.Measured);
            var model = thesis.ThesisModel(spec.ThesisModel);
            var before = previous(figure.Id, spec.Name);
            var relative = spec.Output is "torque" or "power";

            series.Add(new SeriesResult(
                spec, slot, measured, model, esa, before, failures, rows.Count,
                Comparison.Of(esa, measured, relative, figure.CorrelateFrom),
                Comparison.Of(model, measured, relative, figure.CorrelateFrom),
                Comparison.Of(esa, model, relative, figure.CorrelateFrom),
                before is null ? null : Comparison.Of(before, measured, relative, figure.CorrelateFrom)));
        }

        return new FigureResult(figure, series);
    }

    private static (IReadOnlyList<Point>, IReadOnlyList<Failure>) Simulated(
        FigureSpec figure,
        SeriesSpec spec,
        IReadOnlyList<RowResult> rows,
        IReadOnlyDictionary<(string Case, string Grid), IReadOnlyList<RowResult>> runs)
    {
        double X(RowResult r) => figure.X.Variable == "spark" ? r.Spark ?? double.NaN : r.Speed;

        var failures = rows.Where(r => r.Failure is not null).Select(r => new Failure(X(r), r.Failure!)).ToList();

        switch (spec.Output)
        {
            case "inlet_pressure":
                // The recorder counts from intake top dead centre (Main_Prog's 1 to 720, so
                // inlet valve closing is at IVC + 360); the thesis's axis counts from firing
                // top dead centre, its ram peak sitting at inlet valve closing, 580°. Shift by
                // 360° and put in angle order (validation/README.md, N5).
                return ([.. (rows.FirstOrDefault(r => r.Trace is not null)?.Trace ?? [])
                    .Select(p => p with { X = (p.X + 360) % 720 })
                    .OrderBy(p => p.X)], failures);

            case "torque_delta":
                var baseline = runs[(spec.Case, spec.Baseline!)];
                failures.AddRange(baseline
                    .Where(b => b.Failure is not null && rows.All(r => r.Row != b.Row || r.Failure is null))
                    .Select(b => new Failure(X(b), "baseline: " + b.Failure)));
                return ([.. rows.Join(baseline, r => r.Row, b => b.Row, (r, b) => (r, b))
                    .Where(p => p.r.Failure is null && p.b.Failure is null)
                    .Select(p => new Point(X(p.r), p.r.Torque - p.b.Torque))
                    .OrderBy(p => p.X)], failures);

            default:
                return ([.. rows.Where(r => r.Failure is null)
                    .Select(r => new Point(X(r), spec.Output == "power" ? r.PowerKw : r.Torque))
                    .OrderBy(p => p.X)], failures);
        }
    }
}
