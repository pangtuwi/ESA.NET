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

    /// <summary>
    /// The column metadata Delphi sets up in <c>TCAList.Create</c>
    /// (CAList2z.pas:62-89): heading, displayed decimal places and the factor the stored
    /// SI value is multiplied by for display.
    /// </summary>
    /// <remarks>
    /// The scale factors are what turn cubic metres into cc, kilograms into milligrams
    /// and mole fractions into parts per thousand. They belong to the presentation, not
    /// the simulation, which is why the stored values stay in SI.
    /// </remarks>
    private static readonly (string Name, int Decimals, double Scale)[] Columns =
    [
        ("Vcyl", 2, 1e6), ("PCyl", 0, 1), ("Mcyl", 2, 1e6), ("Mb", 2, 1e6),
        ("Mu", 2, 1e6), ("Min", 2, 1e6), ("Mout", 2, 1e6), ("Vb", 2, 1e6),
        ("Vu", 2, 1e6), ("Tb", 1, 1), ("Tu", 1, 1), ("Qb", 3, 1),
        ("Qu", 3, 1), ("Gamma", 3, 1), ("FuelM", 1, 1), ("IV A", 1, 1e6),
        ("EV A", 1, 1e6), ("IV V", 3, 1), ("EV V", 3, 1), ("IV P", 0, 1),
        ("EV P", 0, 1), ("WWork", 3, 1), ("PWork", 3, 1), ("CO", 2, 1e3),
        ("NO", 2, 1e3), ("CO2", 2, 1e3), ("HC", 2, 1e3), ("htLoss", 3, 1),
    ];

    /// <summary>Column headings, Delphi <c>ColName[1..28]</c>.</summary>
    public string[] ColumnNames { get; } = [.. Columns.Select(c => c.Name)];

    /// <summary>Displayed decimal places per column, Delphi <c>Decimals[1..28]</c>.</summary>
    public int[] Decimals { get; } = [.. Columns.Select(c => c.Decimals)];

    /// <summary>Display scale factors, Delphi <c>k[1..28]</c>.</summary>
    public double[] ScaleFactors { get; } = [.. Columns.Select(c => c.Scale)];

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
}
