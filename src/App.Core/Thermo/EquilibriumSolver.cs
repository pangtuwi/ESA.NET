using App.Core.Model;

namespace App.Core.Thermo;

/// <summary>
/// The twelve-species equilibrium combustion model. Port of Delphi <c>TEqbm</c>
/// (Eqbm.pas), credited in the original to "Arthur" and valid for 600 K to 4000 K.
/// </summary>
/// <remarks>
/// <para>
/// Species are numbered in the Olikara and Borman scheme that <see cref="Species"/>
/// mirrors. The solver works on 1-based arrays so the ported formulas read the same
/// as the source; results are copied into <see cref="State"/> at the end of a solve.
/// </para>
/// <para>
/// Four unknowns are solved simultaneously — H2, CO, O2 and N2, that is
/// <c>x[4]</c>, <c>x[6]</c>, <c>x[8]</c>, <c>x[11]</c> — by Newton iteration on a
/// four by four system. The other eight species follow algebraically from the
/// equilibrium constants.
/// </para>
/// <para>
/// SPEC.md section 5 records that the Delphi equilibrium behaviour is authoritative
/// and that no new freeze thresholds should be introduced. The only freeze is
/// <see cref="Frozen"/>, which the engine sets during overlap.
/// </para>
/// </remarks>
public sealed class EquilibriumSolver
{
    // 1-based: index 0 is unused so the Delphi subscripts carry over unchanged.
    private readonly double[] _x = new double[EsaLimits.SpeciesCount + 1];
    private readonly double[] _dxdT = new double[EsaLimits.SpeciesCount + 1];
    private readonly double[] _dxdp = new double[EsaLimits.SpeciesCount + 1];
    private readonly double[] _dxdF = new double[EsaLimits.SpeciesCount + 1];

    private readonly double[,] _matrix = new double[DelphiNumerics.ArraySize, DelphiNumerics.ArraySize];

    /// <summary>Moles of fuel per mole of products, <c>x13</c> in the original.</summary>
    private double _x13;

    private double _c1;
    private double _c2;
    private double _c3;
    private double _c5;
    private double _c7;
    private double _c9;
    private double _c10;

    /// <summary>The species fractions and their derivatives.</summary>
    public EquilibriumState State { get; } = new();

    /// <summary>
    /// When set, <see cref="Solve"/> returns immediately and leaves the previous
    /// composition in place. The engine freezes the burnt zone during overlap.
    /// </summary>
    public bool Frozen { get; set; }

    public EquilibriumDiagnostics Diagnostics { get; } = new();

    /// <summary>
    /// Solves for the equilibrium composition and its derivatives. Port of <c>go2</c>.
    /// </summary>
    /// <param name="equivalenceRatio">Fuel-air equivalence ratio, <c>ER</c>.</param>
    /// <param name="n">Carbon atoms per fuel molecule.</param>
    /// <param name="m">Hydrogen atoms.</param>
    /// <param name="l">Oxygen atoms.</param>
    /// <param name="k">Nitrogen atoms.</param>
    /// <param name="pressure">Pressure in pascals.</param>
    /// <param name="gasTemperature">Temperature in kelvin.</param>
    /// <exception cref="EquilibriumException">
    /// The composition could not be solved. The original raises <c>EEqbmError</c> here
    /// and nothing in the application catches it, so an equilibrium failure aborts the
    /// run.
    /// </exception>
    public void Solve(
        double equivalenceRatio,
        double n,
        double m,
        double l,
        double k,
        double pressure,
        double gasTemperature)
    {
        if (Frozen)
        {
            Diagnostics.FrozenSkips++;
            return;
        }

        // Below 1000 K the curve fits are pinned rather than extrapolated.
        if (gasTemperature < 1000)
        {
            gasTemperature = 1000;
        }

        // A zero equivalence ratio divides by zero further down.
        if (equivalenceRatio < 1e-10)
        {
            equivalenceRatio = 1e-10;
        }

        var p = pressure / 101325;
        var ro = (n + (m / 4) - (l / 2)) / equivalenceRatio;
        var rdd = 0.0444 * ro;
        var rd = (k / 2) + (3.7274 * ro);
        var r = (l / 2) + ro;

        var d1 = m / n;
        var d2 = 2 * r / n;
        var d3 = 2 * rd / n;
        var d4 = rdd / n;

        var rootP = Math.Sqrt(p);
        _c1 = EquilibriumConstant(1, gasTemperature) / rootP;
        _c2 = EquilibriumConstant(2, gasTemperature) / rootP;
        _c3 = EquilibriumConstant(3, gasTemperature) / rootP;
        _c5 = EquilibriumConstant(5, gasTemperature);
        _c7 = EquilibriumConstant(7, gasTemperature);
        _c9 = EquilibriumConstant(9, gasTemperature) * rootP;
        _c10 = EquilibriumConstant(10, gasTemperature) * rootP;

        InitialEstimate(equivalenceRatio, n, m, r, rd, rdd);

        // 99 is the original's sentinel for "no workable oxygen estimate". It is in fact
        // unreachable, because the only path that sets it raises first: ISSUES.md B23.
        if (_x[8] == 99)
        {
            return;
        }

        Diagnostics.Solves++;

        EquilibriumCalc(d1, d2, d3, d4, n, rdd);
        // p, not pressure: go2 passes its atmospheres value here, even though the
        // parameter on the other side is named Pres.
        PartialDerivatives(p, gasTemperature, d1, d2, d3, d4, ro, equivalenceRatio, n);

        PublishState();
    }

    /// <summary>
    /// First guess at the oxygen fraction, then the three species that follow from it.
    /// Port of <c>InitialEstimate</c>.
    /// </summary>
    /// <remarks>
    /// The bracket hunt divides <c>x[8]</c> by ten until the residual turns negative,
    /// as far down as 1e-33. The Newton loop that follows tests an <b>absolute</b>
    /// tolerance against a variable that may be anywhere in that range, which is where
    /// the 80-bit to 64-bit narrowing is most likely to change an iteration count.
    /// </remarks>
    private void InitialEstimate(double equivalenceRatio, double n, double m, double r, double rd, double rdd)
    {
        if (r < 0.5 * n)
        {
            throw new EquilibriumException(
                "Error in Chemical Equilibrium: Solid Carbon Will Form: 0.5n/r="
                + (0.5 * n / r).ToString("F5", System.Globalization.CultureInfo.InvariantCulture));
        }

        _x13 = equivalenceRatio > 1
            ? 1 / (n + (m / 2) + rd + rdd)
            : 1 / ((m / 4) + r + rd + rdd);

        _x[8] = 1;
        var rootX8 = Math.Sqrt(_x[8]);
        var fn = Residual(n, m, r, rootX8);

        if (fn > 0)
        {
            do
            {
                _x[8] = 0.1 * _x[8];
                Diagnostics.BracketSteps++;

                if (_x[8] < 1e-33)
                {
                    throw new EquilibriumException(
                        "Error in Chemical Equilibrium: Initial Estimate Predicts Extremely Low "
                        + "O2 Concentrations of "
                        + _x[8].ToString("F5", System.Globalization.CultureInfo.InvariantCulture));
                }

                rootX8 = Math.Sqrt(_x[8]);
                fn = Residual(n, m, r, rootX8);
            }
            while (fn > 0);
        }

        var iterations = 0;
        double tolerance;

        do
        {
            iterations++;
            rootX8 = Math.Sqrt(_x[8]);
            fn = Residual(n, m, r, rootX8);

            var dfn = (_c10 * n / (2 * Sqr(1 + (_c10 * rootX8)) * rootX8))
                      + (_c9 * m / (4 * Sqr(1 + (_c9 * rootX8)) * rootX8))
                      + (2 / _x13);

            var next = _x[8] - (fn / dfn);
            tolerance = Math.Abs(next - _x[8]);
            _x[8] = next;
        }
        while (tolerance >= 0.0004 && Math.Abs(fn) >= 0.0005 && iterations < 20);

        Diagnostics.InitialEstimateIterations += iterations;

        if (iterations >= 20)
        {
            Diagnostics.InitialEstimateCapHits++;
        }

        _x[6] = n * _x13 / (1 + (_c10 * Math.Sqrt(_x[8])));
        _x[4] = 0.5 * m * _x13 / (1 + (_c9 * Math.Sqrt(_x[8])));
        _x[11] = rd * _x13;
    }

    /// <summary>The oxygen residual the initial estimate drives to zero.</summary>
    private double Residual(double n, double m, double r, double rootX8) =>
        (((2 * _c10 * n * rootX8) + n) / (1 + (_c10 * rootX8)))
        + (0.5 * _c9 * m * rootX8 / (1 + (_c9 * rootX8)))
        + (2 * _x[8] / _x13)
        - (2 * r);

    /// <summary>
    /// Newton iteration on the four principal species. Port of <c>EquilibriumCalc</c>.
    /// </summary>
    private void EquilibriumCalc(double d1, double d2, double d3, double d4, double n, double rdd)
    {
        var b = new double[DelphiNumerics.ArraySize];
        var iterations = 0;
        var resolution = 0;
        double tolerance;

        do
        {
            iterations++;
            SetupMatrix(d1, d2, d3, d4);
            UpdateDependentSpecies();

            b[0] = -(_x[1] + (2 * _x[4]) + _x[5] + (2 * _x[9])) + (d1 * (_x[6] + _x[10]));
            b[1] = -(_x[2] + _x[5] + _x[6] + _x[7] + (2 * _x[8]) + _x[9] + (2 * _x[10]))
                   + (d2 * (_x[6] + _x[10]));
            b[2] = -(_x[3] + _x[7] + (2 * _x[11])) + (d3 * (_x[6] + _x[10]));
            b[3] = 1 - (d4 * (_x[6] + _x[10]))
                     - (_x[1] + _x[2] + _x[3] + _x[4] + _x[5] + _x[6] + _x[7] + _x[8] + _x[9] + _x[10] + _x[11]);

            resolution = DelphiNumerics.GaussReduce(_matrix, b);

            _x[4] += b[0];
            _x[6] += b[1];
            _x[8] += b[2];
            _x[11] += b[3];

            // Crash avoidance, as the original labels it. Note the guard tests for
            // negative but the clamps test for non-positive: ISSUES.md B24.
            if (_x[4] < 0 || _x[6] < 0 || _x[8] < 0 || _x[11] < 0)
            {
                if (_x[4] <= 0)
                {
                    _x[4] = 1e-25;
                }

                if (_x[6] <= 0)
                {
                    _x[6] = 1e-25;
                }

                if (_x[8] <= 0)
                {
                    _x[8] = 1e-25;
                }

                if (_x[11] <= 0)
                {
                    _x[11] = 1e-25;
                }
            }

            tolerance = Math.Abs(b[0] / _x[4]);
            tolerance = Math.Max(tolerance, Math.Abs(b[1] / _x[6]));
            tolerance = Math.Max(tolerance, Math.Abs(b[2] / _x[8]));
            tolerance = Math.Max(tolerance, Math.Abs(b[3] / _x[11]));
        }
        while (!(tolerance < 0.0001 && resolution > 5) && iterations <= 25);

        Diagnostics.EquilibriumIterations += iterations;
        Diagnostics.WorstEquilibriumIterations = Math.Max(Diagnostics.WorstEquilibriumIterations, iterations);

        if (iterations > 25)
        {
            Diagnostics.EquilibriumCapHits++;
        }

        if (resolution < 5)
        {
            throw new EquilibriumException(
                "Error in Chemical Equilibrium: Matrixsolver Returned Insufficient Resolution ");
        }

        if (iterations > 24)
        {
            throw new EquilibriumException(
                "Error in Chemical Equilibrium: Newton Raphson Iteration did not Converge.");
        }

        if (_x[4] < 0 || _x[6] < 0 || _x[8] < 0 || _x[11] < 0)
        {
            throw new EquilibriumException("Negative Mole Fractions Found During Iteration.");
        }

        UpdateDependentSpecies();
        _x13 = (_x[6] + _x[10]) / n;
        _x[12] = rdd * _x13;
    }

    /// <summary>The eight species that follow algebraically from the four solved for.</summary>
    private void UpdateDependentSpecies()
    {
        _x[1] = _c1 * Math.Sqrt(_x[4]);
        _x[2] = _c2 * Math.Sqrt(_x[8]);
        _x[3] = _c3 * Math.Sqrt(_x[11]);
        _x[5] = _c5 * Math.Sqrt(_x[4]) * Math.Sqrt(_x[8]);
        _x[7] = _c7 * Math.Sqrt(_x[8]) * Math.Sqrt(_x[11]);
        _x[9] = _c9 * _x[4] * Math.Sqrt(_x[8]);
        _x[10] = _c10 * _x[6] * Math.Sqrt(_x[8]);
    }

    /// <summary>The Jacobian of the four-species system. Port of <c>SetupMatrix</c>.</summary>
    private void SetupMatrix(double d1, double d2, double d3, double d4)
    {
        var t14 = 0.5 * _c1 / Math.Sqrt(_x[4]);
        var t28 = 0.5 * _c2 / Math.Sqrt(_x[8]);
        var t311 = 0.5 * _c3 / Math.Sqrt(_x[11]);
        var t54 = 0.5 * _c5 * Math.Sqrt(_x[8]) / Math.Sqrt(_x[4]);
        var t58 = 0.5 * _c5 * Math.Sqrt(_x[4]) / Math.Sqrt(_x[8]);
        var t78 = 0.5 * _c7 * Math.Sqrt(_x[11]) / Math.Sqrt(_x[8]);
        var t711 = 0.5 * _c7 * Math.Sqrt(_x[8]) / Math.Sqrt(_x[11]);
        var t94 = _c9 * Math.Sqrt(_x[8]);
        var t98 = 0.5 * _c9 * _x[4] / Math.Sqrt(_x[8]);
        var t106 = _c10 * Math.Sqrt(_x[8]);
        var t108 = 0.5 * _c10 * _x[6] / Math.Sqrt(_x[8]);

        _matrix[0, 0] = t14 + 2 + t54 + (2 * t94);
        _matrix[0, 1] = -d1 * (1 + t106);
        _matrix[0, 2] = t58 + (2 * t98) - (d1 * t108);
        _matrix[0, 3] = 0;

        _matrix[1, 0] = t54 + t94;
        _matrix[1, 1] = 1 + (2 * t106) - (d2 * (1 + t106));
        _matrix[1, 2] = t28 + t58 + t78 + 2 + t98 + (2 * t108) - (d2 * t108);
        _matrix[1, 3] = t711;

        _matrix[2, 0] = 0;
        _matrix[2, 1] = -d3 * (1 + t106);
        _matrix[2, 2] = t78 - (d3 * t108);
        _matrix[2, 3] = t311 + t711 + 2;

        _matrix[3, 0] = t14 + 1 + t54 + t94;
        _matrix[3, 1] = 1 + t106 + (d4 * (1 + t106));
        _matrix[3, 2] = t28 + t58 + t78 + 1 + t98 + t108 + (d4 * t108);
        _matrix[3, 3] = t311 + t711 + 1;
    }

    /// <summary>
    /// Derivatives of every species with respect to temperature, pressure and
    /// equivalence ratio. Port of <c>Partial_dxd</c>.
    /// </summary>
    /// <remarks>
    /// Three more solves against the same Jacobian, one per independent variable. Note
    /// that <c>C5</c> and <c>C7</c> carry no pressure dependence, so their terms are
    /// absent from the pressure derivatives.
    /// </remarks>
    private void PartialDerivatives(
        double pressure,
        double gasTemperature,
        double d1,
        double d2,
        double d3,
        double d4,
        double ro,
        double equivalenceRatio,
        double n)
    {
        // Atmospheres, the same units go2 built C1 to C10 from. The Delphi parameter
        // this arrives in is named Pres, which reads like pascals and is not: go2 passes
        // its local p, already divided by 101325. Passing pascals here instead inflates
        // every dC/dT by sqrt(101325) and, through dhdT and dRdT, dudT with them. See
        // ISSUES.md A7.
        var p = pressure;
        SetupMatrix(d1, d2, d3, d4);

        var rootP = Math.Sqrt(p);
        var dc1dT = ConstantDerivative(1, gasTemperature) / rootP;
        var dc2dT = ConstantDerivative(2, gasTemperature) / rootP;
        var dc3dT = ConstantDerivative(3, gasTemperature) / rootP;
        var dc5dT = ConstantDerivative(5, gasTemperature);
        var dc7dT = ConstantDerivative(7, gasTemperature);
        var dc9dT = ConstantDerivative(9, gasTemperature) * rootP;
        var dc10dT = ConstantDerivative(10, gasTemperature) * rootP;

        var bT = new double[DelphiNumerics.ArraySize];
        bT[0] = -((dc1dT * _x[1] / _c1) + (dc5dT * _x[5] / _c5) + (2 * dc9dT * _x[9] / _c9)
                  - (d1 * dc10dT * _x[10] / _c10));
        bT[1] = -((dc2dT * _x[2] / _c2) + (dc5dT * _x[5] / _c5) + (dc7dT * _x[7] / _c7)
                  + (dc9dT * _x[9] / _c9) + ((2 - d2) * dc10dT * _x[10] / _c10));
        bT[2] = -((dc3dT * _x[3] / _c3) + (dc7dT * _x[7] / _c7) - (d3 * dc10dT * _x[10] / _c10));
        bT[3] = -((dc1dT * _x[1] / _c1) + (dc2dT * _x[2] / _c2) + (dc3dT * _x[3] / _c3)
                  + (dc5dT * _x[5] / _c5) + (dc7dT * _x[7] / _c7) + (dc9dT * _x[9] / _c9)
                  + ((1 + d4) * dc10dT * _x[10] / _c10));

        RequireResolution(DelphiNumerics.GaussReduce(_matrix, bT));

        var dc1dp = -0.5 * _c1 / p;
        var dc2dp = -0.5 * _c2 / p;
        var dc3dp = -0.5 * _c3 / p;
        var dc9dp = 0.5 * _c9 / p;
        var dc10dp = 0.5 * _c10 / p;

        var bP = new double[DelphiNumerics.ArraySize];
        bP[0] = -((dc1dp * _x[1] / _c1) + (2 * dc9dp * _x[9] / _c9) - (d1 * dc10dp * _x[10] / _c10));
        bP[1] = -((dc2dp * _x[2] / _c2) + (dc9dp * _x[9] / _c9) + ((2 - d2) * dc10dp * _x[10] / _c10));
        bP[2] = -((dc3dp * _x[3] / _c3) - (d3 * dc10dp * _x[10] / _c10));
        bP[3] = -((dc1dp * _x[1] / _c1) + (dc2dp * _x[2] / _c2) + (dc3dp * _x[3] / _c3)
                  + (dc9dp * _x[9] / _c9) + ((1 + d4) * dc10dp * _x[10] / _c10));

        RequireResolution(DelphiNumerics.GaussReduce(_matrix, bP));

        var d5 = -ro * _x13 / equivalenceRatio;
        var bF = new double[DelphiNumerics.ArraySize];
        bF[0] = 0;
        bF[1] = (_x[6] + _x[10]) * 2 * d5 / n;
        bF[2] = (_x[6] + _x[10]) * 7.4548 * d5 / n;
        bF[3] = -(_x[6] + _x[10]) * 0.0444 * d5 / n;

        RequireResolution(DelphiNumerics.GaussReduce(_matrix, bF));

        var rootX4 = Math.Sqrt(_x[4]);
        var rootX8 = Math.Sqrt(_x[8]);
        var rootX11 = Math.Sqrt(_x[11]);

        _dxdT[1] = (_c1 * 0.5 * bT[0] / rootX4) + (dc1dT * rootX4);
        _dxdT[2] = (_c2 * 0.5 * bT[2] / rootX8) + (dc2dT * rootX8);
        _dxdT[3] = (_c3 * 0.5 * bT[3] / rootX11) + (dc3dT * rootX11);
        _dxdT[4] = bT[0];
        _dxdT[5] = (_c5 * 0.5 * ((rootX8 * bT[0] / rootX4) + (rootX4 * bT[2] / rootX8)))
                   + (dc5dT * rootX4 * rootX8);
        _dxdT[6] = bT[1];
        _dxdT[7] = (_c7 * 0.5 * ((rootX11 * bT[2] / rootX8) + (rootX8 * bT[3] / rootX11)))
                   + (dc7dT * rootX8 * rootX11);
        _dxdT[8] = bT[2];
        _dxdT[9] = (_c9 * ((rootX8 * bT[0]) + (0.5 * _x[4] * bT[2] / rootX8))) + (dc9dT * _x[4] * rootX8);
        _dxdT[10] = (_c10 * ((rootX8 * bT[1]) + (0.5 * _x[6] * bT[2] / rootX8))) + (dc10dT * _x[6] * rootX8);
        _dxdT[11] = bT[3];
        _dxdT[12] = d4 * (bT[1] + _dxdT[10]);

        _dxdp[1] = (_c1 * 0.5 * bP[0] / rootX4) + (dc1dp * rootX4);
        _dxdp[2] = (_c2 * 0.5 * bP[2] / rootX8) + (dc2dp * rootX8);
        _dxdp[3] = (_c3 * 0.5 * bP[3] / rootX11) + (dc3dp * rootX11);
        _dxdp[4] = bP[0];
        _dxdp[5] = _c5 * 0.5 * ((rootX8 * bP[0] / rootX4) + (rootX4 * bP[2] / rootX8));
        _dxdp[6] = bP[1];
        _dxdp[7] = _c7 * 0.5 * ((rootX11 * bP[2] / rootX8) + (rootX8 * bP[3] / rootX11));
        _dxdp[8] = bP[2];
        _dxdp[9] = (_c9 * ((rootX8 * bP[0]) + (0.5 * _x[4] * bP[2] / rootX8))) + (dc9dp * _x[4] * rootX8);
        _dxdp[10] = (_c10 * ((rootX8 * bP[1]) + (0.5 * _x[6] * bP[2] / rootX8))) + (dc10dp * _x[6] * rootX8);
        _dxdp[11] = bP[3];
        _dxdp[12] = d4 * (bP[1] + _dxdp[10]);

        _dxdF[1] = _c1 * 0.5 * bF[0] / rootX4;
        _dxdF[2] = _c2 * 0.5 * bF[2] / rootX8;
        _dxdF[3] = _c3 * 0.5 * bF[3] / rootX11;
        _dxdF[4] = bF[0];
        _dxdF[5] = _c5 * 0.5 * ((rootX8 * bF[0] / rootX4) + (rootX4 * bF[2] / rootX8));
        _dxdF[6] = bF[1];
        _dxdF[7] = _c7 * 0.5 * ((rootX11 * bF[2] / rootX8) + (rootX8 * bF[3] / rootX11));
        _dxdF[8] = bF[2];
        _dxdF[9] = _c9 * ((rootX8 * bF[0]) + (0.5 * _x[4] * bF[2] / rootX8));
        _dxdF[10] = _c10 * ((rootX8 * bF[1]) + (0.5 * _x[6] * bF[2] / rootX8));
        _dxdF[11] = bF[3];
        _dxdF[12] = (d4 * (_dxdF[6] + _dxdF[10])) + (0.0444 * d5);
    }

    private static void RequireResolution(int resolution)
    {
        if (resolution < 5)
        {
            throw new EquilibriumException(
                "Error in Chemical Equilibrium: Matrixsolver Returned Insufficient Resolution ");
        }
    }

    /// <summary>Equilibrium constant for one reaction. Port of <c>KEquilib</c>.</summary>
    /// <remarks>
    /// The curve fit is only valid over 600 K to 4000 K. Outside that the original
    /// raises before it reaches the clamp on the following line, so the clamp is dead
    /// code and an out-of-range temperature is fatal. See ISSUES.md B21, and B22 for
    /// why every error path in the original throws rather than being suppressed.
    /// </remarks>
    private static double EquilibriumConstant(int reaction, double gasTemperature)
    {
        if (gasTemperature is < 600 or > 4000)
        {
            throw new EquilibriumException(
                "Error in Chemical Equilibrium: Temperature Out of Range for Equilibrium "
                + "Constant Curve Fit. Requested Temp: "
                + gasTemperature.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "K");
        }

        var t = gasTemperature / 1000;

        var logK = (ThermoTables.EquilibriumCoefficients[reaction, 1] * Math.Log(t))
                   + (ThermoTables.EquilibriumCoefficients[reaction, 2] / t)
                   + ThermoTables.EquilibriumCoefficients[reaction, 3]
                   + (ThermoTables.EquilibriumCoefficients[reaction, 4] * t)
                   + (ThermoTables.EquilibriumCoefficients[reaction, 5] * Sqr(t));

        var kValue = DelphiNumerics.DoublePower(10, logK);

        // Small numbers come back as zero from DoublePower's filter, which would divide
        // by zero downstream.
        return kValue < 1e-10 ? 1e-10 : kValue;
    }

    /// <summary>Temperature derivative of an equilibrium constant. Port of <c>dKEq_dT</c>.</summary>
    private static double ConstantDerivative(int reaction, double gasTemperature)
    {
        var t = gasTemperature / 1000;
        var ki = EquilibriumConstant(reaction, gasTemperature);

        return 0.001 * Math.Log(10) * ki
               * ((ThermoTables.EquilibriumCoefficients[reaction, 1] / t)
                  - (ThermoTables.EquilibriumCoefficients[reaction, 2] / Sqr(t))
                  + ThermoTables.EquilibriumCoefficients[reaction, 4]
                  + (2 * ThermoTables.EquilibriumCoefficients[reaction, 5] * t));
    }

    private void PublishState()
    {
        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            State.X[i] = _x[i];
            State.DxDt[i] = _dxdT[i];
            State.DxDp[i] = _dxdp[i];
            State.DxDf[i] = _dxdF[i];
        }

        State.Frozen = Frozen;
    }

    private static double Sqr(double value) => value * value;
}
