using System.Globalization;

namespace App.Validation;

/// <summary>A point of any series, in the figure's own units.</summary>
internal readonly record struct Point(double X, double Y);

/// <summary>
/// One figure's thesis data, <c>data/thesis/figures/Figure6_NN.csv</c>: series, role
/// (<c>measured</c> or <c>thesis_model</c>), x, y.
/// </summary>
internal sealed class ThesisData
{
    private readonly Dictionary<(string Series, string Role), List<Point>> _series = [];

    public static ThesisData Load(string path)
    {
        var data = new ThesisData();

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var f = line.Split(',');
            var key = (f[0], f[1]);

            if (!data._series.TryGetValue(key, out var points))
            {
                data._series[key] = points = [];
            }

            points.Add(new Point(
                double.Parse(f[2], CultureInfo.InvariantCulture),
                double.Parse(f[3], CultureInfo.InvariantCulture)));
        }

        return data;
    }

    public IReadOnlyList<Point> Measured(string series) => Get(series, "measured");

    public IReadOnlyList<Point> ThesisModel(string series) => Get(series, "thesis_model");

    private List<Point> Get(string series, string role) =>
        _series.TryGetValue((series, role), out var points)
            ? [.. points.OrderBy(p => p.X)]
            : throw new InvalidDataException($"No {role} series '{series}'.");
}
