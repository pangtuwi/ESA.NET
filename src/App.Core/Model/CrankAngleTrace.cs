namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TCAList</c> (CAList2z.pas): captured data for one cycle,
/// indexed by crank angle over <c>[-359, 360]</c>, plus the column metadata used
/// by the PVT grid and its text export.
/// </summary>
public sealed class CrankAngleTrace
{
    private const int Span = EsaLimits.LastCrankAngle - EsaLimits.FirstCrankAngle + 1;

    private readonly CrankAnglePoint[] _points = CreatePoints();

    /// <summary>Column headings, Delphi <c>ColName[1..28]</c>.</summary>
    public string[] ColumnNames { get; } = CreateNames();

    /// <summary>Displayed decimal places per column, Delphi <c>Decimals[1..28]</c>.</summary>
    public int[] Decimals { get; } = new int[EsaLimits.CapturedValueCount];

    /// <summary>Display scale factors, Delphi <c>k[1..28]</c>.</summary>
    public double[] ScaleFactors { get; } = new double[EsaLimits.CapturedValueCount];

    /// <summary>Indexed by crank angle in <c>[-359, 360]</c>, as in Delphi.</summary>
    public CrankAnglePoint this[int crankAngle] => _points[crankAngle - EsaLimits.FirstCrankAngle];

    private static CrankAnglePoint[] CreatePoints()
    {
        var points = new CrankAnglePoint[Span];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new CrankAnglePoint();
        }

        return points;
    }

    private static string[] CreateNames()
    {
        var names = new string[EsaLimits.CapturedValueCount];
        Array.Fill(names, string.Empty);
        return names;
    }
}
