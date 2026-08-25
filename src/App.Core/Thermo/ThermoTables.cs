namespace App.Core.Thermo;

/// <summary>
/// The curve-fit coefficient tables from Delphi <c>GASPROPS.PAS</c> and
/// <c>Eqbm.pas</c>, reproduced digit for digit.
/// </summary>
/// <remarks>
/// Rows are indexed by the Olikara and Borman species numbering, which the
/// <see cref="Species"/> enum mirrors: 1 H, 2 O, 3 N, 4 H2, 5 OH, 6 CO, 7 NO,
/// 8 O2, 9 H2O, 10 CO2, 11 N2, 12 Ar. Index 0 of each array is unused so that the
/// Delphi subscripts can be read straight across.
/// </remarks>
internal static class ThermoTables
{
    /// <summary>Least-squares thermodynamic fit, 300 to 1000 K. Delphi <c>ThermoLT</c>.</summary>
    public static readonly double[,] LowTemperature = Pad(
    [
        [0.25000000e+1, 0, 0, 0, 0, 0.25471627e+5, -0.46011762],
        [0.29464287e+1, -0.16381665e-2, 0.24210316e-5, -0.16028432e-8, 0.38906964e-12, 0.29147644e+5, 0.29639949e+1],
        [0.25030714e+1, -0.21800181e-4, 0.54205287e-7, -0.56475602e-10, 0.20999044e-13, 0.56098904e+5, 0.41675764e+1],
        [0.30574451e+1, 0.26765200e-2, -0.58099162e-5, 0.55210391e-8, -0.18122739e-11, -0.98890474e+3, -0.22997056e+1],
        [0.38375943e+1, -0.10778858e-2, 0.96830378e-6, 0.18713972e-9, -0.22571094e-12, 0.36412823e+4, 0.49370009e+0],
        [0.37100928e+1, -0.16190964e-2, 0.36923594e-5, -0.20319674e-8, 0.23953344e-12, -0.14356310e+5, 0.29555351e+1],
        [0.40459521e+1, -0.34181783e-2, 0.79819190e-5, -0.61139316e-8, 0.15919076e-12, 0.97453934e+4, 0.29974988e+1],
        [0.36255985e+1, -0.18782184e-2, 0.70554544e-5, -0.67635137e-8, 0.21555993e-11, -0.10475226e+4, 0.43052778e+1],
        [0.40701275e+1, -0.11084499e-2, 0.41521180e-5, -0.29637404e-8, 0.80702103e-12, -0.30279722e+5, -0.32270046e+0],
        [0.24007797e+1, 0.87350957e-2, -0.66070878e-5, 0.20021861e-8, 0.63274039e-15, -0.48377527e+5, 0.96951457e+1],
        [0.36748261e+1, -0.12081500e-2, 0.23240102e-5, -0.63217559e-9, -0.22577253e-12, -0.10611588e+4, 0.23580424e+1],
        [0.25000000e+1, 0, 0, 0, 0, -0.74537498e+3, 0.43660006e+1],
    ]);

    /// <summary>Least-squares thermodynamic fit, 1000 to 5000 K. Delphi <c>ThermoHT</c>.</summary>
    public static readonly double[,] HighTemperature = Pad(
    [
        [0.25000000e+1, 0, 0, 0, 0, 0.25471627e+5, -0.46011763],
        [0.25420596e+1, -0.27550619e-4, -0.31028033e-8, 0.45510674e-11, -0.43680515e-15, 0.29230803e+5, 0.49203080e+1],
        [0.24502682e+1, 0.10661458e-3, -0.74653373e-7, 0.18796524e-10, -0.10259839e-14, 0.56116040e+5, 0.44487581e+1],
        [0.31001901e+1, 0.51119464e-3, 0.52644210e-7, -0.34909973e-10, 0.36945345e-14, -0.87738042e+3, -0.19629421e+1],
        [0.29106427e+1, 0.95931650e-3, -0.19441702e-6, 0.13756646e-10, 0.14224542e-15, 0.39353815e+4, 0.54423445e+1],
        [0.29840696e+1, 0.14891390e-2, -0.57899684e-6, 0.10364577e-09, -0.69353550e-14, -0.14245228e+5, 0.63479156e+1],
        [0.31890000e+1, 0.13382281e-2, -0.52899318e-6, 0.95919332e-10, -0.64847932e-14, 0.98283290e+4, 0.67458126e+1],
        [0.36219535e+1, 0.73618264e-3, -0.19652228e-6, 0.36201558e-10, -0.28945627e-14, -0.12019825e+4, 0.36150960e+1],
        [0.27167633e+1, 0.29451374e-2, -0.80224374e-6, 0.10226682e-09, -0.48472145e-14, -0.29905826e+5, 0.66305671e+1],
        [0.44608041e+1, 0.30981719e-2, -0.12392571e-5, 0.22741325e-09, -0.15525954e-13, -0.48961442e+5, -0.98635982e+0],
        [0.28963194e+1, 0.15154866e-2, -0.57235277e-6, 0.99807393e-10, -0.65223555e-14, -0.90586184e+3, 0.61615148e+1],
        [0.25000000e+1, 0, 0, 0, 0, -0.74537502e+3, 0.43660006e+1],
    ]);

    /// <summary>
    /// Fuel thermodynamic fits, indexed 0 to 6 by fuel type. Delphi <c>FuelThermo</c>.
    /// Row 0 is the user-specified fuel and is filled in at set-up time.
    /// </summary>
    public static readonly double[,] Fuel =
    {
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 1.412633, 2.087101e-2, -8.142134e-6, 0, 0, -1.026351e+4, 1.917126e+1 },
        { 0, 0.15027072e1, 0.10416798e-1, -0.39181522e-5, 0.67777899e-9, -0.44283706e-13, -0.99787078e4, 0.10707143e2 },
        { 0, 1.779819, 1.262503e-2, -3.624890e-6, 0, 0, -2.525420e+4, 1.50884e+1 },
        { 0, -2.545087, 4.79554e-2, -2.030765e-5, 0, 0, 8.782234e+3, 3.348825e+1 },
        { 0, 4.0652, 6.0977e-2, -1.8801e-5, 0, 0, -3.5880e+4, 1.545e+1 },
        { 0, 7.9710, 1.1954e-1, -3.6858e-5, 0, 0, -1.9385e+4, -1.7879 },
    };

    /// <summary>Species molecular weights. Delphi <c>MolWt</c>.</summary>
    public static readonly double[] MolecularWeight =
        [0, 1.0080, 15.9994, 14.0067, 2.0160, 17.0074, 28.0106, 30.0061, 31.9988, 18.0154, 44.0100, 28.0134, 39.948];

    /// <summary>Universal gas constant in J/(kmol.K). Delphi <c>Runiversal</c> in GASPROPS.PAS.</summary>
    public const double UniversalGasConstant = 8314.41;

    /// <summary>Dry air composition as mole fractions. Delphi <c>AirComposition</c>.</summary>
    public static readonly double[] AirComposition =
        [0, 0, 0, 0, 0, 0, 0, 0, 0.2096, 0, 0, 0.7811, 0.0093];

    /// <summary>
    /// Equilibrium constant coefficients, indexed by reaction number in the Olikara
    /// and Borman scheme. Delphi <c>KCoef</c>. Rows 4, 6 and 8 are dummies that keep
    /// the numbering aligned with the species.
    /// </summary>
    public static readonly double[,] EquilibriumCoefficients = Pad(
    [
        [0.432168, -0.112464e2, 0.267269e1, -0.745744e-1, 0.242484e-2],
        [0.310805, -0.129540e2, 0.321779e1, -0.738336e-1, 0.344645e-2],
        [0.389716, -0.245828e2, 0.314505e1, -0.963730e-1, 0.585643e-2],
        [0, 0, 0, 0, 0],
        [-0.141784, -0.213308e1, 0.853461, 0.355015e-1, -0.310227e-2],
        [0, 0, 0, 0, 0],
        [0.150879e-1, -0.470959e1, 0.646096, 0.272805e-2, -0.154444e-2],
        [0, 0, 0, 0, 0],
        [-0.752364, 0.124210e2, -0.260286e1, 0.259556, -0.162687e-1],
        [-0.415302e-2, 0.148627e2, -0.475746e1, 0.124699, -0.900227e-2],
    ]);

    /// <summary>
    /// Copies a set of Delphi 1-based rows into an array whose row 0 and column 0 are
    /// unused, so the Delphi subscripts carry over unchanged.
    /// </summary>
    private static double[,] Pad(double[][] rows)
    {
        var columns = rows[0].Length;
        var padded = new double[rows.Length + 1, columns + 1];

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                padded[row + 1, column + 1] = rows[row][column];
            }
        }

        return padded;
    }
}
