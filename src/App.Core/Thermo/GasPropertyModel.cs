using App.Core.Model;

namespace App.Core.Thermo;

/// <summary>
/// Thermodynamic properties of the working gas. Port of Delphi <c>TProp</c>
/// (GASPROPS.PAS).
/// </summary>
/// <remarks>
/// <para>
/// Two modes, fixed at construction by <c>BurnedGasCntrl</c>. An <b>unburnt</b> model
/// treats a frozen fuel, air and residual mixture; a <b>burnt</b> model solves the
/// twelve-species equilibrium at every call. Only the burnt model owns an
/// <see cref="EquilibriumSolver"/>.
/// </para>
/// <para>
/// Every gas in the engine holds one of each: <c>Cyl.Unburnt</c> and <c>Cyl.Burnt</c>.
/// </para>
/// </remarks>
public sealed class GasPropertyModel
{
    private readonly bool _burned;

    /// <summary>Composition of the residual gas, Delphi <c>InitFAirResConcs</c>.</summary>
    private readonly double[] _residual = new double[EsaLimits.SpeciesCount + 1];

    /// <summary>Composition of the fuel, air and residual mixture, Delphi <c>FAirResConcs</c>.</summary>
    private readonly double[] _mixture = new double[EsaLimits.SpeciesCount + 1];

    private readonly double[] _zero = new double[EsaLimits.SpeciesCount + 1];

    /// <summary>
    /// Curve fit for the user-specified fuel. Delphi copies a library row into
    /// <c>FuelThermo[0]</c> at set-up; that table is shared here, so the row lives on
    /// the instance instead.
    /// </summary>
    private readonly double[] _userFuelThermo = new double[8];

    private double _userFuelMolecularWeight;

    private int _fuelType;
    private double _n;
    private double _m;
    private double _l;
    private double _k;
    private double _equivalenceRatio;
    private double _residualFraction;

    public GasPropertyModel(bool burned)
    {
        _burned = burned;

        if (burned)
        {
            Equilibrium = new EquilibriumSolver();
        }
    }

    /// <summary>The equilibrium solver, or <see langword="null"/> for an unburnt model.</summary>
    public EquilibriumSolver? Equilibrium { get; }

    /// <summary>Fuel mole fraction of the mixture, Delphi <c>FuelMolFrac</c>.</summary>
    public double FuelMoleFraction { get; private set; }

    /// <summary>
    /// Establishes the fuel and the operating point. Port of <c>SetUp</c>.
    /// </summary>
    /// <param name="fuelType">
    /// 0 for a fuel described by <paramref name="n"/> to <paramref name="k"/>, or 1 to 6
    /// to select one from <see cref="ThermoTables.FuelComposition"/>.
    /// </param>
    /// <param name="equivalenceRatio">Fuel-air equivalence ratio; below 1 is lean.</param>
    /// <param name="residualFraction">Exhaust gas recirculation, Delphi <c>f</c>.</param>
    public void Setup(
        int fuelType,
        double n,
        double m,
        double l,
        double k,
        double equivalenceRatio,
        double residualFraction)
    {
        _fuelType = fuelType;
        _equivalenceRatio = equivalenceRatio;
        _residualFraction = residualFraction;

        if (fuelType == 0)
        {
            _n = n;
            _m = m;
            _l = l;
            _k = k;

            // A hydrogen to carbon ratio above 2.1 is treated as petrol, otherwise
            // diesel, and that library row becomes the user fuel's curve fit.
            var template = _m / _n > 2.1 ? 5 : 6;

            for (var i = 1; i <= 7; i++)
            {
                _userFuelThermo[i] = ThermoTables.Fuel[template, i];
            }

            _userFuelMolecularWeight = (_n * 12.0112) + (_m * 1.008) + (_l * 15.9994) + (_k * 14.0067);
        }
        else
        {
            _n = ThermoTables.FuelComposition[fuelType, 1];
            _m = ThermoTables.FuelComposition[fuelType, 2];
            _l = ThermoTables.FuelComposition[fuelType, 3];
            _k = ThermoTables.FuelComposition[fuelType, 4];
        }

        if (!_burned)
        {
            // The residual is frozen at an assumed exhaust temperature of 1000 K.
            ResidualConcentrations(_equivalenceRatio, _n, _m, _l, _k, 1000);
        }
    }

    /// <summary>
    /// Changes the operating point. Port of <c>ChangeParam</c>, where 99 means "leave
    /// this one alone".
    /// </summary>
    public void ChangeParameters(double n, double m, double l, double k, double equivalenceRatio, double residualFraction)
    {
        if (n != 99)
        {
            _n = n;
        }

        if (m != 99)
        {
            _m = m;
        }

        if (l != 99)
        {
            _l = l;
        }

        if (k != 99)
        {
            _k = k;
        }

        if (equivalenceRatio != 99)
        {
            _equivalenceRatio = equivalenceRatio;
        }

        if (residualFraction != 99)
        {
            _residualFraction = residualFraction;
        }
    }

    /// <summary>
    /// Fills <paramref name="properties"/> with everything the ODEs need at this state.
    /// Port of <c>ReturnProps</c>.
    /// </summary>
    public void ReturnProps(double pressure, double gasTemperature, GasProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (!_burned)
        {
            FuelAirResidualConcentrations();

            var molecularWeight = MixtureMolecularWeight(_mixture, FuelMoleFraction);
            var r = ThermoTables.UniversalGasConstant / molecularWeight;
            var enthalpy = MixtureEnthalpy(_mixture, FuelMoleFraction, gasTemperature, molecularWeight);
            var specificHeat = MixtureSpecificHeat(_mixture, FuelMoleFraction, gasTemperature, molecularWeight);

            // Frozen reactions, so dM/dT and dR/dT are both zero.
            var dhdT = MixtureDhDt(_zero, specificHeat, molecularWeight, gasTemperature, 0, enthalpy);

            properties.R = r;
            properties.H = enthalpy;
            properties.U = enthalpy - (r * gasTemperature);
            properties.Cp = dhdT;
            properties.DuDt = dhdT - r - (0 * gasTemperature);
            properties.DuDp = 0;
            properties.DuDf = 0;
            return;
        }

        var solver = Equilibrium!;
        solver.Solve(_equivalenceRatio, _n, _m, _l, _k, pressure, gasTemperature);

        var mw = MixtureMolecularWeight(SpeciesOf(solver), 0);
        var gasConstant = ThermoTables.UniversalGasConstant / mw;
        var h = MixtureEnthalpy(SpeciesOf(solver), 0, gasTemperature, mw);
        var cp1 = MixtureSpecificHeat(SpeciesOf(solver), 0, gasTemperature, mw);

        var dMdT = WeightedByMolecularWeight(DerivativesOf(solver, Derivative.Temperature));
        var dhdTb = MixtureDhDt(DerivativesOf(solver, Derivative.Temperature), cp1, mw, gasTemperature, dMdT, h);
        var dRdT = -gasConstant * dMdT / mw;

        properties.R = gasConstant;
        properties.H = h;
        properties.U = h - (gasConstant * gasTemperature);
        properties.Cp = dhdTb;
        properties.DuDt = dhdTb - gasConstant - (dRdT * gasTemperature);

        // The analytic dudp is computed and then discarded: the original replaces it
        // with a central difference of u over a 0.05 per cent pressure band. Marked
        // with a bare "//#" in the source, so evidently a deliberate patch. It costs
        // two further equilibrium solves per call. See ISSUES.md B16.
        var uLow = InternalEnergy(pressure - (0.00025 * pressure), gasTemperature);
        var uHigh = InternalEnergy(pressure + (0.00025 * pressure), gasTemperature);
        properties.DuDp = (uHigh - uLow) / (0.0005 * pressure);

        // Re-solve at the requested state: the two difference calls above left the
        // solver holding a neighbouring pressure.
        solver.Solve(_equivalenceRatio, _n, _m, _l, _k, pressure, gasTemperature);

        var dMdF = WeightedByMolecularWeight(DerivativesOf(solver, Derivative.EquivalenceRatio));
        var dhdF = MixtureDhDx(DerivativesOf(solver, Derivative.EquivalenceRatio), mw, gasTemperature, dMdF, h);
        var dRdF = -gasConstant * dMdF / mw;
        properties.DuDf = dhdF - (dRdF * gasTemperature);
    }

    /// <summary>Gas constant of the mixture, J/(kg.K). Port of <c>Get_R</c>.</summary>
    public double GasConstant(double pressure, double gasTemperature) =>
        ThermoTables.UniversalGasConstant / MolecularWeightAt(pressure, gasTemperature);

    /// <summary>Specific enthalpy, J/kg. Port of <c>Get_h</c>.</summary>
    public double Enthalpy(double pressure, double gasTemperature)
    {
        var (species, fuelFraction) = Composition(pressure, gasTemperature);
        var mw = MixtureMolecularWeight(species, fuelFraction);
        return MixtureEnthalpy(species, fuelFraction, gasTemperature, mw);
    }

    /// <summary>Specific internal energy, J/kg. Port of <c>Get_u</c>.</summary>
    public double InternalEnergy(double pressure, double gasTemperature)
    {
        var (species, fuelFraction) = Composition(pressure, gasTemperature);
        var mw = MixtureMolecularWeight(species, fuelFraction);
        var r = ThermoTables.UniversalGasConstant / mw;
        return MixtureEnthalpy(species, fuelFraction, gasTemperature, mw) - (r * gasTemperature);
    }

    /// <summary>
    /// Ratio of specific heats. Port of <c>Get_gamma</c>.
    /// </summary>
    /// <remarks>
    /// Even in the burnt branch this uses the <b>frozen</b> specific heat: the original
    /// computes <c>dMdT</c> and then passes a zero array and a zero <c>dMdT</c> to
    /// <c>MixdhdT</c> anyway, with the real arguments left commented out beside them.
    /// Gamma therefore does not depend on the equilibrium temperature derivatives, and
    /// so it never sees the equilibrium temperature derivatives at all. That is why it
/// matched the baseline trace even while ISSUES.md A7 was inflating them. See B19.
    /// </remarks>
    public double Gamma(double pressure, double gasTemperature)
    {
        var (species, fuelFraction) = Composition(pressure, gasTemperature);
        var mw = MixtureMolecularWeight(species, fuelFraction);
        var r = ThermoTables.UniversalGasConstant / mw;
        var h = MixtureEnthalpy(species, fuelFraction, gasTemperature, mw);
        var cp1 = MixtureSpecificHeat(species, fuelFraction, gasTemperature, mw);
        var cp = MixtureDhDt(_zero, cp1, mw, gasTemperature, 0, h);

        return cp / (cp - r);
    }

    /// <summary>Specific heat at constant pressure, J/(kg.K). Port of <c>Get_cp</c>.</summary>
    public double SpecificHeatConstantPressure(double pressure, double gasTemperature)
    {
        var (species, fuelFraction) = Composition(pressure, gasTemperature);
        var mw = MixtureMolecularWeight(species, fuelFraction);
        var h = MixtureEnthalpy(species, fuelFraction, gasTemperature, mw);
        var cp1 = MixtureSpecificHeat(species, fuelFraction, gasTemperature, mw);

        return MixtureDhDt(_zero, cp1, mw, gasTemperature, 0, h);
    }

    /// <summary>Specific heat at constant volume, J/(kg.K). Port of <c>Get_cv</c>.</summary>
    public double SpecificHeatConstantVolume(double pressure, double gasTemperature) =>
        SpecificHeatConstantPressure(pressure, gasTemperature) - GasConstant(pressure, gasTemperature);

    private enum Derivative
    {
        Temperature,
        Pressure,
        EquivalenceRatio,
    }

    private double[] SpeciesOf(EquilibriumSolver solver)
    {
        var values = new double[EsaLimits.SpeciesCount + 1];

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            values[i] = solver.State.X[i];
        }

        return values;
    }

    private double[] DerivativesOf(EquilibriumSolver solver, Derivative which)
    {
        var values = new double[EsaLimits.SpeciesCount + 1];

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            values[i] = which switch
            {
                Derivative.Temperature => solver.State.DxDt[i],
                Derivative.Pressure => solver.State.DxDp[i],
                _ => solver.State.DxDf[i],
            };
        }

        return values;
    }

    /// <summary>The composition to evaluate properties from, in whichever mode applies.</summary>
    private (double[] Species, double FuelFraction) Composition(double pressure, double gasTemperature)
    {
        if (!_burned)
        {
            FuelAirResidualConcentrations();
            return (_mixture, FuelMoleFraction);
        }

        Equilibrium!.Solve(_equivalenceRatio, _n, _m, _l, _k, pressure, gasTemperature);
        return (SpeciesOf(Equilibrium), 0);
    }

    private double MolecularWeightAt(double pressure, double gasTemperature)
    {
        var (species, fuelFraction) = Composition(pressure, gasTemperature);
        return MixtureMolecularWeight(species, fuelFraction);
    }

    // ---------------------------------------------------------------------------
    // Mixture rules
    // ---------------------------------------------------------------------------

    private double MixtureMolecularWeight(double[] species, double fuelMoleFraction)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += species[i] * ThermoTables.MolecularWeight[i];
        }

        if (!_burned)
        {
            total += fuelMoleFraction * FuelMolecularWeight();
        }

        return total;
    }

    private double MixtureSpecificHeat(double[] species, double fuelMoleFraction, double gasTemperature, double molecularWeight)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += species[i] * SpecieSpecificHeat(i, gasTemperature);
        }

        if (!_burned)
        {
            total += fuelMoleFraction * FuelSpecificHeat(gasTemperature);
        }

        return total / molecularWeight;
    }

    private double MixtureEnthalpy(double[] species, double fuelMoleFraction, double gasTemperature, double molecularWeight)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += species[i] * SpecieEnthalpy(i, gasTemperature);
        }

        if (!_burned)
        {
            total += fuelMoleFraction * FuelEnthalpy(gasTemperature);
        }

        return total / molecularWeight;
    }

    private static double WeightedByMolecularWeight(double[] derivatives)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += derivatives[i] * ThermoTables.MolecularWeight[i];
        }

        return total;
    }

    private double MixtureDhDt(double[] dxdT, double cp, double molecularWeight, double gasTemperature, double dMdT, double h)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += dxdT[i] * SpecieEnthalpy(i, gasTemperature);
        }

        return cp + ((total - (dMdT * h)) / molecularWeight);
    }

    /// <summary>Shared body of <c>Mixdhdp</c> and <c>MixdhdF</c>, which differ only in argument.</summary>
    private double MixtureDhDx(double[] dxdx, double molecularWeight, double gasTemperature, double dMdx, double h)
    {
        var total = 0.0;

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            total += dxdx[i] * SpecieEnthalpy(i, gasTemperature);
        }

        return (total - (dMdx * h)) / molecularWeight;
    }

    // ---------------------------------------------------------------------------
    // Curve fits
    // ---------------------------------------------------------------------------

    /// <summary>Molar specific heat of one species, J/(kmol.K). Port of <c>SpecHeat</c>.</summary>
    private static double SpecieSpecificHeat(int species, double gasTemperature)
    {
        gasTemperature = ClampToFitRange(gasTemperature);

        var t = gasTemperature;
        var table = gasTemperature > 1000 ? ThermoTables.HighTemperature : ThermoTables.LowTemperature;

        var value = table[species, 1]
                    + (table[species, 2] * t)
                    + (table[species, 3] * t * t)
                    + (table[species, 4] * t * t * t)
                    + (table[species, 5] * t * t * t * t);

        return value * ThermoTables.UniversalGasConstant;
    }

    /// <summary>Molar enthalpy of one species, J/kmol. Port of <c>Enthalpy</c>.</summary>
    private static double SpecieEnthalpy(int species, double gasTemperature)
    {
        gasTemperature = ClampToFitRange(gasTemperature);

        var t = gasTemperature;
        var table = gasTemperature > 1000 ? ThermoTables.HighTemperature : ThermoTables.LowTemperature;

        var value = table[species, 1]
                    + (table[species, 2] * t / 2)
                    + (table[species, 3] * t * t / 3)
                    + (table[species, 4] * t * t * t / 4)
                    + (table[species, 5] * t * t * t * t / 5)
                    + (table[species, 6] / t);

        return value * ThermoTables.UniversalGasConstant * t;
    }

    /// <summary>
    /// The curve fits cover 300 K to 5000 K. The original widened the lower guard to
    /// 260 K "to avoid error messages for now" and clamps rather than extrapolating.
    /// See ISSUES.md B20.
    /// </summary>
    private static double ClampToFitRange(double gasTemperature) =>
        gasTemperature is < 260 or > 5000
            ? gasTemperature > 5000 ? 5000 : 300
            : gasTemperature;

    private double FuelSpecificHeat(double gasTemperature)
    {
        var row = FuelThermoRow();
        var t = gasTemperature;

        return (row[1] + (row[2] * t) + (row[3] * t * t)) * ThermoTables.UniversalGasConstant;
    }

    private double FuelEnthalpy(double gasTemperature)
    {
        var row = FuelThermoRow();
        var t = gasTemperature;

        var value = row[1] + (row[2] * t / 2) + (row[3] * t * t / 3) + (row[6] / t);
        value += (row[4] * t * t * t / 4) + (row[5] * t * t * t * t / 5);

        return value * ThermoTables.UniversalGasConstant * t;
    }

    private double[] FuelThermoRow()
    {
        if (_fuelType == 0)
        {
            return _userFuelThermo;
        }

        var row = new double[8];

        for (var i = 1; i <= 7; i++)
        {
            row[i] = ThermoTables.Fuel[_fuelType, i];
        }

        return row;
    }

    private double FuelMolecularWeight() =>
        _fuelType == 0 ? _userFuelMolecularWeight : ThermoTables.FuelMolecularWeight[_fuelType];

    // ---------------------------------------------------------------------------
    // Residual and mixture composition
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Water-gas equilibrium constant at the exhaust temperature. Port of <c>CalcK</c>,
    /// after Ferguson's worked example 3.2.
    /// </summary>
    private static double WaterGasConstant(double gasTemperature)
    {
        var t = gasTemperature / 1000;
        var lnK = 2.743 - (1.761 / t) - (1.611 / (t * t)) + (0.2803 / (t * t * t));

        return Math.Exp(lnK);
    }

    /// <summary>
    /// Composition of the residual gas. Port of <c>ResidualConcs</c>, after Ferguson
    /// page 108 in the Olikara and Borman numbering.
    /// </summary>
    private void ResidualConcentrations(double equivalenceRatio, double n, double m, double l, double k, double exhaustTemperature)
    {
        var keq = WaterGasConstant(exhaustTemperature);
        var eps = 0.2096 / (n + (0.25 * m) - (0.5 * l));

        Array.Clear(_residual);
        double totalMoles;

        if (equivalenceRatio > 1)
        {
            // Rich: some carbon leaves as CO, set by the water-gas shift.
            var a = 1 - keq;
            var b = 0.4192 - (equivalenceRatio * eps * ((2 * n) - l))
                    + (keq * ((0.4192 * (equivalenceRatio - 1)) + (n * equivalenceRatio * eps)));
            var c = -0.4192 * n * equivalenceRatio * eps * (equivalenceRatio - 1) * keq;

            _residual[6] = (-b + Math.Sqrt((b * b) - (4 * a * c))) / (2 * a);
            _residual[4] = (0.4192 * (equivalenceRatio - 1)) - _residual[6];
            _residual[9] = 0.4192 - (equivalenceRatio * eps * ((2 * n) - l)) + _residual[6];
            _residual[10] = (n * equivalenceRatio * eps) - _residual[6];
            _residual[11] = 0.7811 + (k * equivalenceRatio * eps / 2);
            _residual[12] = 0.0093;

            totalMoles = _residual[4] + _residual[6] + _residual[9] + _residual[10] + _residual[11] + _residual[12];
        }
        else
        {
            // Lean: complete combustion with oxygen left over.
            _residual[8] = 0.2096 * (1 - equivalenceRatio);
            _residual[9] = m * equivalenceRatio * eps / 2;
            _residual[10] = n * equivalenceRatio * eps;
            _residual[11] = 0.7811 + (k * equivalenceRatio * eps / 2);
            _residual[12] = 0.0093;

            totalMoles = _residual[8] + _residual[9] + _residual[10] + _residual[11] + _residual[12];
        }

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            _residual[i] /= totalMoles;
        }
    }

    /// <summary>
    /// Composition of the fuel, air and residual charge. Port of <c>FuelAirResConcs</c>,
    /// after Ferguson page 111.
    /// </summary>
    /// <remarks>
    /// The products' molecular weight is taken from <c>FAirResConcs</c> — the mixture
    /// this procedure is about to overwrite — rather than from the residual. On the
    /// first call that array is still zero, which makes the residual mass fraction
    /// exactly one and the charge pure residual; from the second call onward it carries
    /// the previous result. Reproduced as found. See ISSUES.md B18.
    /// </remarks>
    private void FuelAirResidualConcentrations()
    {
        var f = _residualFraction < 1e-5 ? 1e-5 : _residualFraction;
        var eps = 0.2096 / (_n + (0.25 * _m) - (0.5 * _l));

        var fuelAir = new double[EsaLimits.SpeciesCount + 1];

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            fuelAir[i] = ThermoTables.AirComposition[i] / (1 + (eps * _equivalenceRatio));
        }

        FuelMoleFraction = eps * _equivalenceRatio / (1 + (eps * _equivalenceRatio));

        var reactantWeight = MixtureMolecularWeight(fuelAir, FuelMoleFraction);
        var productWeight = MixtureMolecularWeight(_mixture, 0);

        var residualFraction = 1 / (1 + (productWeight / reactantWeight * ((1 / f) - 1)));

        for (var i = 1; i <= EsaLimits.SpeciesCount; i++)
        {
            _mixture[i] = ((1 - residualFraction) * fuelAir[i]) + (residualFraction * _residual[i]);
        }

        FuelMoleFraction = (1 - residualFraction) * FuelMoleFraction;
    }
}
