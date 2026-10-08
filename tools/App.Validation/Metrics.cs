namespace App.Validation;

/// <summary>How one series compares with another: rms and mean of the difference.</summary>
/// <param name="Relative">Per cent of the reference when true; the figure's own units when false.</param>
internal sealed record Comparison(double Rms, double Bias, int Points, bool Relative)
{
    /// <summary>
    /// Compares <paramref name="series"/> with <paramref name="reference"/> at the
    /// reference's own x values, interpolating the series linearly. Reference points
    /// outside the series' x range, or below <paramref name="from"/>, are left out:
    /// a comparison is only made where both curves exist.
    /// </summary>
    public static Comparison? Of(
        IReadOnlyList<Point> series, IReadOnlyList<Point> reference, bool relative, double? from = null)
    {
        if (series.Count < 2)
        {
            return null;
        }

        var differences = new List<double>();

        foreach (var r in reference)
        {
            if (from is { } f && r.X < f - 1)
            {
                continue;
            }

            if (Interpolate(series, r.X) is { } y)
            {
                differences.Add(relative ? ((y / r.Y) - 1) * 100 : y - r.Y);
            }
        }

        return differences.Count == 0
            ? null
            : new Comparison(
                Math.Sqrt(differences.Average(d => d * d)), differences.Average(), differences.Count, relative);
    }

    /// <summary>
    /// Linear interpolation inside the series' x range, within half a percent of either
    /// end. Not across a gap: where two neighbours are further apart than
    /// <see cref="GapFactor"/> times the series' median spacing - a run that failed, or
    /// the stretch of cycle the manifold capture window leaves out - there is no value.
    /// </summary>
    public static double? Interpolate(IReadOnlyList<Point> s, double x)
    {
        var gap = GapLimit(s);
        var span = s[^1].X - s[0].X;
        var tolerance = Math.Max(span * 0.005, 1e-9);

        if (x < s[0].X - tolerance || x > s[^1].X + tolerance)
        {
            return null;
        }

        for (var i = 1; i < s.Count; i++)
        {
            if (x <= s[i].X + tolerance || i == s.Count - 1)
            {
                var a = s[i - 1];
                var b = s[i];
                if (b.X - a.X > gap)
                {
                    return null;
                }

                var t = b.X == a.X ? 0 : Math.Clamp((x - a.X) / (b.X - a.X), 0, 1);
                return a.Y + (t * (b.Y - a.Y));
            }
        }

        return null;
    }

    /// <summary>How much wider than usual a step must be to count as a gap.</summary>
    public const double GapFactor = 1.6;

    /// <summary>The widest step that is not a gap: <see cref="GapFactor"/> times the median spacing.</summary>
    public static double GapLimit(IReadOnlyList<Point> s)
    {
        if (s.Count < 3)
        {
            return double.PositiveInfinity;
        }

        var steps = s.Zip(s.Skip(1), (a, b) => b.X - a.X).Order().ToList();
        return steps[steps.Count / 2] * GapFactor;
    }

    /// <summary>The highest point: for a timing loop, MBT and the torque there.</summary>
    public static Point? Peak(IReadOnlyList<Point> s) => s.Count == 0 ? null : s.MaxBy(p => p.Y);
}
