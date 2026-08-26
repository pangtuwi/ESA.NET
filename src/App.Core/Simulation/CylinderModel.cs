using App.Core.Expressions;
using App.Core.Interpolation;
using App.Core.Model;
using App.Core.Thermo;

namespace App.Core.Simulation;

/// <summary>
/// The in-cylinder model: Woschini heat transfer and the ordinary differential
/// equations the integrator advances. Port of the heat-transfer methods and the
/// derivative functions of <c>ICEngine2Z.pas</c> (lines 167-633).
/// </summary>
/// <remarks>
/// <para>
/// In the original the derivative functions are <b>free functions over a global</b>,
/// each opening with <c>with Engine2z do</c>. CLAUDE.md forbids that global, so they are
/// instance methods here and the <c>fn[1..4]</c> array becomes delegates bound to this
/// instance.
/// </para>
/// <para>
/// <b>They are not pure and must not be made pure.</b> Each one begins by calling an
/// <c>Update*</c> method on the cylinder gas, which overwrites its pressure, volumes,
/// masses and thermodynamic partials, and then reads those partials straight back. RKF5
/// evaluates each equation six times per step at six different trial vectors, so the gas
/// is a scratchpad whose contents depend on the order the equations were called in.
/// Reordering the statements inside one of these methods changes the answer.
/// </para>
/// <para>
/// The fields on this class that the solver writes between steps -
/// <see cref="State"/>, <see cref="CrankAngleRadians"/>, <see cref="InletMassFlow"/> and
/// the three conditions at inlet valve closing - are the rest of that shared mutable
/// state. They are set by <c>CycleSolver</c>, exactly as the original set them on the
/// global.
/// </para>
/// </remarks>
public sealed class CylinderModel
{
    private const double WoschniC2 = 3.24E-3;

    private readonly CylinderGeometry _geometry;
    private readonly WallTemperatureTable _wallTemperatures;

    public CylinderModel(
        CylinderGeometry geometry,
        TwoZoneGas cylinder,
        TwoZoneGas plenum,
        WallTemperatureTable wallTemperatures)
    {
        _geometry = geometry;
        Cylinder = cylinder;
        Plenum = plenum;
        _wallTemperatures = wallTemperatures;
    }

    /// <summary>The cylinder gas, Delphi <c>Cyl</c>.</summary>
    public TwoZoneGas Cylinder { get; }

    /// <summary>The inlet plenum gas, Delphi <c>Plenum</c>.</summary>
    public TwoZoneGas Plenum { get; }

    public CylinderGeometry Geometry => _geometry;

    /// <summary>Engine speed in rev/min, Delphi <c>Nrpm</c>.</summary>
    public double Rpm { get; set; }

    /// <summary>Crank angular velocity in rad/s, Delphi <c>wcrank</c>.</summary>
    public double CrankAngularVelocity { get; set; }

    /// <summary>Delphi <c>WoshiniCoeff</c>, read from the <c>.eng</c> file.</summary>
    public double WoschniCoefficient { get; set; }

    /// <summary>The current crank-angle state, Delphi <c>State</c>.</summary>
    public EngineState State { get; set; }

    /// <summary>
    /// The solver's current crank angle in radians, Delphi <c>x</c> (a field of
    /// <c>TRKF</c>). <see cref="HeatTransferCoefficient"/> reads this rather than the
    /// angle it was called with; see the note there.
    /// </summary>
    public double CrankAngleRadians { get; set; }

    /// <summary>Cylinder pressure at inlet valve closing, Delphi <c>PCylIVC</c>.</summary>
    public double PressureAtInletValveClosing { get; set; }

    /// <summary>Cylinder temperature at inlet valve closing, Delphi <c>TCylIVC</c>.</summary>
    public double TemperatureAtInletValveClosing { get; set; }

    /// <summary>Cylinder volume at inlet valve closing, Delphi <c>VCylIVC</c>.</summary>
    public double VolumeAtInletValveClosing { get; set; }

    /// <summary>
    /// Mass through the inlet valve over the last step, Delphi <c>MIn</c>. The unburnt
    /// equations use its sign to decide whether the incoming enthalpy is the plenum's or
    /// the cylinder's own.
    /// </summary>
    public double InletMassFlow { get; set; }

    // ---------------------------------------------------------------------------
    // Heat transfer
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Woschini heat-transfer coefficient. Port of <c>TEngine2z.hWoshini</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things here are worth knowing before touching it.
    /// </para>
    /// <para>
    /// The motored volume comes from <see cref="CrankAngleRadians"/> - the solver's
    /// current crank angle - and not from the angle the calling derivative was handed.
    /// Inside an RKF5 step the two differ at five of the six stages, so the motored
    /// pressure lags the trial state. Reproduced. See ISSUES.md B32.
    /// </para>
    /// <para>
    /// <c>Vswept</c> is computed as <c>VCyl(pi) * CR/(CR+1)</c>. The swept volume of a
    /// cylinder whose volume at bottom dead centre is <c>VCyl(pi)</c> is
    /// <c>VCyl(pi) * (CR-1)/CR</c>; at a compression ratio of 9.2 the two differ by a
    /// factor of 1.13. Reproduced. See ISSUES.md B33.
    /// </para>
    /// </remarks>
    public double HeatTransferCoefficient(double pressure, double temperature)
    {
        var c1 = State switch
        {
            EngineState.Compression or EngineState.Combustion or EngineState.Expansion =>
                EsaLimits.WoshiniC1Closed,
            _ => EsaLimits.WoshiniC1GasExchange,
        };

        var meanPistonSpeed = 2 * _geometry.Stroke * Rpm / 60;

        var motoredVolume = _geometry.Volume(CrankAngleRadians);
        var motoredPressure = PressureAtInletValveClosing
                              * DelphiMath.Pwr(VolumeAtInletValveClosing / motoredVolume, 1.30);

        var sweptVolume = _geometry.Volume(Math.PI)
                          * _geometry.CompressionRatio / (_geometry.CompressionRatio + 1);

        var w = (c1 * meanPistonSpeed)
                + (WoschniC2
                   * (sweptVolume * TemperatureAtInletValveClosing)
                   / (PressureAtInletValveClosing * VolumeAtInletValveClosing)
                   * (pressure - motoredPressure));

        return WoschniCoefficient
               * DelphiMath.Pwr(_geometry.Bore, -0.2)
               * DelphiMath.Pwr(pressure / 101325, 0.8)
               * DelphiMath.Pwr(temperature, -0.53)
               * DelphiMath.Pwr(w, 0.8);
    }

    private double AverageLinerTemperature(double crankAngleRadians)
    {
        var linerRatio = _geometry.Volume(crankAngleRadians) / _geometry.Volume(Math.PI);

        return (linerRatio * LinerUpper) + ((1 - linerRatio) * LinerLower);
    }

    private double Head => LegacyInterpolation.AtSpeed(
        _wallTemperatures.Rpm, _wallTemperatures.HeadTemperature, Rpm);

    private double Piston => LegacyInterpolation.AtSpeed(
        _wallTemperatures.Rpm, _wallTemperatures.PistonTemperature, Rpm);

    private double LinerUpper => LegacyInterpolation.AtSpeed(
        _wallTemperatures.Rpm, _wallTemperatures.UpperLinerTemperature, Rpm);

    private double LinerLower => LegacyInterpolation.AtSpeed(
        _wallTemperatures.Rpm, _wallTemperatures.LowerLinerTemperature, Rpm);

    /// <summary>
    /// Heat loss from the burnt zone per radian, Delphi <c>TEngine2z.dQbdtheta</c>.
    /// Negative, because it leaves the gas.
    /// </summary>
    /// <remarks>
    /// The combustion branch scales the piston and head term by the burnt volume
    /// fraction and then <b>omits the liner term entirely</b>, where
    /// <see cref="UnburntHeatLossRate"/>'s matching branch keeps it. Reproduced; the
    /// asymmetry is defensible for a flame kernel near the head but it is not stated
    /// anywhere. See ISSUES.md B34.
    /// </remarks>
    public double BurntHeatLossRate(double crankAngleRadians)
    {
        var gas = Cylinder.State;
        var wallArea = _geometry.WallArea(crankAngleRadians);
        var pistonArea = _geometry.PistonArea;
        var averageLiner = AverageLinerTemperature(crankAngleRadians);

        var q = State switch
        {
            EngineState.Combustion =>
                HeatTransferCoefficient(gas.PGas, gas.Tb) * gas.Vb / gas.VGas * pistonArea
                * (gas.Tb - Piston + gas.Tb - Head),

            EngineState.Intake or EngineState.Compression => 0,

            _ =>
                HeatTransferCoefficient(gas.PGas, gas.Tb)
                * ((pistonArea * (gas.Tb - Piston + gas.Tb - Head))
                   + (wallArea * (gas.Tb - averageLiner))),
        };

        return -q / CrankAngularVelocity;
    }

    /// <summary>
    /// Heat loss from the unburnt zone per radian, Delphi <c>TEngine2z.dQudtheta</c>.
    /// </summary>
    public double UnburntHeatLossRate(double crankAngleRadians)
    {
        var gas = Cylinder.State;
        var wallArea = _geometry.WallArea(crankAngleRadians);
        var pistonArea = _geometry.PistonArea;
        var averageLiner = AverageLinerTemperature(crankAngleRadians);

        var q = State switch
        {
            EngineState.Combustion =>
                HeatTransferCoefficient(gas.PGas, gas.Tu)
                * ((gas.Vu / gas.VGas * pistonArea * (gas.Tu - Piston + gas.Tu - Head))
                   + (wallArea * (gas.Tu - averageLiner))),

            EngineState.Expansion or EngineState.Exhaust => 0,

            _ =>
                HeatTransferCoefficient(gas.PGas, gas.Tu)
                * ((pistonArea * (gas.Tu - Piston + gas.Tu - Head))
                   + (wallArea * (gas.Tu - averageLiner))),
        };

        return -q / CrankAngularVelocity;
    }

    /// <summary>
    /// Single-zone heat loss per radian, Delphi <c>TEngine2z.dQldtheta1z</c>. Always the
    /// full-surface form, with no state switch, and always off the burnt temperature.
    /// </summary>
    public double SingleZoneHeatLossRate(double crankAngleRadians)
    {
        var gas = Cylinder.State;
        var wallArea = _geometry.WallArea(crankAngleRadians);
        var pistonArea = _geometry.PistonArea;
        var averageLiner = AverageLinerTemperature(crankAngleRadians);

        var q = HeatTransferCoefficient(gas.PGas, gas.Tb)
                * ((pistonArea * (gas.Tb - Piston + gas.Tb - Head))
                   + (wallArea * (gas.Tb - averageLiner)));

        return -q / CrankAngularVelocity;
    }

    /// <summary>
    /// Rate of work done on the piston, Delphi's free function <c>dWdTheta</c>:
    /// pressure times the rate of change of volume.
    /// </summary>
    public double WorkRate(double crankAngleRadians, ReadOnlySpan<double> y) =>
        y[1] * _geometry.VolumeRatePerRadian(crankAngleRadians);

    // ---------------------------------------------------------------------------
    // Equations : single zone
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Delphi <c>Zero</c>: the dummy equation assigned to the components that are not
    /// integrated in a given state.
    /// </summary>
    public static double Zero(double crankAngleRadians, ReadOnlySpan<double> y) => 0;

    /// <summary>
    /// Single-zone pressure equation, Delphi <c>dPdTheta1z</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ratio of specific heats is hard-coded to 1.4 here.</b> The original has
    /// both the <c>VariableGamma</c> test and the <c>Cyl.Gamma</c> assignment present but
    /// commented out immediately above the literal, so the engine's own computed gamma is
    /// never used by this equation and the <b>Variable Gamma checkbox on the Model tab
    /// does nothing at all</b>. See ISSUES.md C11 and B35.
    /// </para>
    /// <para>
    /// This equation also does <b>not</b> call any <c>Update*</c> method, so it reads
    /// whatever pressure and volume the gas was left holding by the previous call rather
    /// than the trial vector it was handed. Only <c>dVCyldTheta</c> and the burn rate see
    /// <paramref name="crankAngleRadians"/>.
    /// </para>
    /// <para>
    /// In two-zone mode this is the equation the <b>overlap</b> state uses, not the gas
    /// exchange set - see <see cref="PressureRateGasExchange"/>.
    /// </para>
    /// </remarks>
    public double PressureRateSingleZone(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        const double localGamma = 1.4;

        var gas = Cylinder.State;

        return (-localGamma * gas.PGas / gas.VGas * _geometry.VolumeRatePerRadian(crankAngleRadians))
               + ((localGamma - 1) / gas.VGas
                  * ((gas.Fuel.M * gas.Fuel.Q * Cylinder.BurnRate(crankAngleRadians))
                     + SingleZoneHeatLossRate(crankAngleRadians)));
    }

    // ---------------------------------------------------------------------------
    // Equations : unburnt
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Unburnt pressure equation, Delphi <c>dPdThetaUB</c>. Used through intake and
    /// compression.
    /// </summary>
    /// <remarks>
    /// The transfer enthalpy is chosen <b>after</b> the update here and <b>before</b> it
    /// in <see cref="UnburntTemperatureRate"/>. The two therefore disagree about which
    /// <c>hu</c> the cylinder branch means whenever <see cref="InletMassFlow"/> is not
    /// positive. Reproduced. See ISSUES.md B36.
    /// </remarks>
    public double PressureRateUnburnt(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var volume = _geometry.Volume(crankAngleRadians);

        Cylinder.UpdateUB(volume, _geometry.VolumeRatePerRadian(crankAngleRadians), volume, y[1], y[3]);

        var gas = Cylinder.State;
        var transferEnthalpy = InletMassFlow > 0 ? Plenum.State.Hu : gas.Hu;

        var dTudTheta = ((-gas.PGas * gas.DvDTheta)
                         + UnburntHeatLossRate(crankAngleRadians)
                         + (gas.DmInDTheta * (gas.Uu - transferEnthalpy))
                         + (gas.DmInDTheta * gas.DuDpu * gas.PGas)
                         + (gas.Mu / gas.Vu * gas.DuDpu * gas.PGas * gas.DvDTheta))
                        / ((gas.Mu * gas.DuDtu) + (gas.Mu * gas.DuDpu * gas.PGas / gas.Tu));

        return gas.PGas * ((-gas.DmInDTheta / gas.Mu) + (dTudTheta / gas.Tu) - (gas.DvDTheta / gas.Vu));
    }

    /// <summary>
    /// Unburnt temperature equation, Delphi <c>dTudThetaUB</c>. The numerator and
    /// denominator are the same as in <see cref="PressureRateUnburnt"/>; only the order
    /// of the enthalpy choice and the update differs.
    /// </summary>
    public double UnburntTemperatureRate(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var gas = Cylinder.State;

        // Before the update, unlike dPdThetaUB. Reproduced; see ISSUES.md B36.
        var transferEnthalpy = InletMassFlow > 0 ? Plenum.State.Hu : gas.Hu;

        var volume = _geometry.Volume(crankAngleRadians);
        Cylinder.UpdateUB(volume, _geometry.VolumeRatePerRadian(crankAngleRadians), volume, y[1], y[3]);

        return ((-gas.PGas * gas.DvDTheta)
                + UnburntHeatLossRate(crankAngleRadians)
                + (gas.DmInDTheta * (gas.Uu - transferEnthalpy))
                + (gas.DmInDTheta * gas.DuDpu * gas.PGas)
                + (gas.Mu / gas.Vu * gas.DuDpu * gas.PGas * gas.DvDTheta))
               / ((gas.Mu * gas.DuDtu) + (gas.Mu * gas.DuDpu * gas.PGas / gas.Tu));
    }

    // ---------------------------------------------------------------------------
    // Equations : burning
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The coefficients the four burning equations share. Delphi recomputes these
    /// locally in each of the four functions, including re-running <c>UpdateB</c> and
    /// both heat-loss integrals; the letters are the original's own variable names.
    /// </summary>
    private readonly record struct BurningCoefficients(
        double Q, double F, double L, double J, double S, double R, double B, double H, double D);

    private BurningCoefficients Burning(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        Cylinder.UpdateB(
            crankAngleRadians,
            _geometry.Volume(crankAngleRadians),
            _geometry.VolumeRatePerRadian(crankAngleRadians),
            y[0],
            y[1],
            y[2],
            y[3]);

        var gas = Cylinder.State;

        var q = UnburntHeatLossRate(crankAngleRadians) / (gas.Mu * gas.Ru);
        var f = (gas.VGas * gas.DuDtb / gas.Rb) + (gas.DuDpb * gas.Mb) + gas.Vu;
        var l = gas.Vu / gas.Tu;
        var j = -gas.Vu / gas.PGas;
        var s = (gas.DmbDTheta * gas.Vu / gas.Mu) + gas.DvDTheta;
        var r = (-gas.PGas * gas.DvDTheta * (1 + (gas.DuDtb / gas.Rb)))
                + BurntHeatLossRate(crankAngleRadians)
                - (gas.DmbDTheta
                   * (gas.Ub - gas.Uu + (((gas.Ru * gas.Tu / gas.Rb) - gas.Tb) * gas.DuDtb)));
        var b = -gas.Tu / gas.PGas;
        var h = -(gas.Mu * gas.Ru * (1 + (gas.DuDtb / gas.Rb)));
        var d = 1 + (gas.DuDtu / gas.Ru);

        return new BurningCoefficients(q, f, l, j, s, r, b, h, d);
    }

    /// <summary>Burnt volume equation, Delphi <c>dVbdThetaB</c>.</summary>
    public double BurntVolumeRateBurning(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var (q, f, l, j, s, r, b, h, d) = Burning(crankAngleRadians, y);

        return ((-q * f * l) + (q * j * h) + (r * b * l) - (r * j * d) - (s * b * h) + (s * f * d))
               / ((-b * h) + (d * f));
    }

    /// <summary>Pressure equation while burning, Delphi <c>dPdThetaB</c>.</summary>
    public double PressureRateBurning(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = Burning(crankAngleRadians, y);

        return ((-c.H * c.Q) + (c.D * c.R)) / ((-c.B * c.H) + (c.D * c.F));
    }

    /// <summary>
    /// Burnt temperature equation while burning, Delphi <c>dTbdThetaB</c>. Carries four
    /// coefficients the other three do not: N, O, M and T.
    /// </summary>
    public double BurntTemperatureRateBurning(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var (q, f, l, j, s, r, b, h, d) = Burning(crankAngleRadians, y);
        var gas = Cylinder.State;

        var n = gas.Mb * gas.DuDpb;
        var o = gas.Mb * gas.DuDtb;
        var m = gas.PGas;

        // The original marks this line "//## Was T", so it was changed at some point and
        // the previous form is gone.
        var t = BurntHeatLossRate(crankAngleRadians) + (gas.DmbDTheta * (gas.Hu - gas.Ub));

        return -((-q * n * h) - (q * m * f * l) + (q * m * h * j) + (r * n * d) + (r * m * b * l)
                 - (r * m * d * j) - (s * m * b * h) + (s * m * d * f) + (t * b * h) - (t * d * f))
               / o / ((-b * h) + (d * f));
    }

    /// <summary>Unburnt temperature equation while burning, Delphi <c>dTudThetaB</c>.</summary>
    public double UnburntTemperatureRateBurning(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = Burning(crankAngleRadians, y);

        return ((c.Q * c.F) - (c.R * c.B)) / ((-c.B * c.H) + (c.D * c.F));
    }

    // ---------------------------------------------------------------------------
    // Equations : burnt, expanding
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Pressure equation for the fully burnt gas, Delphi <c>dPdThetaBD</c>. Used through
    /// expansion and, because the exhaust state never reassigns the equation set, through
    /// exhaust as well.
    /// </summary>
    public double PressureRateBurnedDown(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var volume = _geometry.Volume(crankAngleRadians);

        Cylinder.UpdateBD(volume, _geometry.VolumeRatePerRadian(crankAngleRadians), volume, y[1], y[2]);

        var gas = Cylinder.State;
        var dTbdTheta = BurnedDownTemperatureRate(crankAngleRadians);

        return gas.PGas
               * ((-gas.DmOutDTheta / gas.Mb) + (dTbdTheta / gas.Tb) - (gas.DvDTheta / gas.Vb));
    }

    /// <summary>Burnt temperature equation for the fully burnt gas, Delphi <c>dTbdThetaBD</c>.</summary>
    public double BurntTemperatureRateBurnedDown(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var volume = _geometry.Volume(crankAngleRadians);

        Cylinder.UpdateBD(volume, _geometry.VolumeRatePerRadian(crankAngleRadians), volume, y[1], y[2]);

        return BurnedDownTemperatureRate(crankAngleRadians);
    }

    private double BurnedDownTemperatureRate(double crankAngleRadians)
    {
        var gas = Cylinder.State;

        return ((-gas.PGas * gas.DvDTheta)
                + BurntHeatLossRate(crankAngleRadians)
                + (gas.DmOutDTheta * (gas.Ub - gas.Hb))
                + (gas.DmOutDTheta * gas.DuDpb * gas.PGas)
                + (gas.Mb / gas.Vb * gas.DuDpb * gas.PGas * gas.DvDTheta))
               / ((gas.Mb * gas.DuDtb) + (gas.Mb * gas.DuDpb * gas.PGas / gas.Tb));
    }

    // ---------------------------------------------------------------------------
    // Equations : gas exchange
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The coefficients the four gas-exchange equations share, again using the
    /// original's own single-letter names.
    /// </summary>
    private readonly record struct GasExchangeCoefficients(
        double A, double D, double Q, double E, double G, double R,
        double I, double J, double K, double S, double M, double N, double P, double T);

    private GasExchangeCoefficients GasExchange(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        Cylinder.UpdateGE(
            _geometry.Volume(crankAngleRadians),
            _geometry.VolumeRatePerRadian(crankAngleRadians),
            y[0],
            y[1],
            y[2],
            y[3]);

        var gas = Cylinder.State;

        return new GasExchangeCoefficients(
            A: -gas.PGas,
            D: gas.Mu * gas.DuDtu,
            Q: UnburntHeatLossRate(crankAngleRadians)
               - (gas.PGas * gas.DvDTheta)
               - (gas.DmInDTheta * (gas.Uu - gas.HIn)),
            E: gas.PGas,
            G: gas.Mb * gas.DuDtb,
            R: BurntHeatLossRate(crankAngleRadians) - (gas.DmOutDTheta * (gas.Hb - gas.Ub)),
            I: 1 / gas.Vb,
            J: 1 / gas.PGas,
            K: -1 / gas.Tb,
            S: -gas.DmOutDTheta / gas.Mb,
            M: -1 / gas.Vu,
            N: 1 / gas.PGas,
            P: -1 / gas.Tu,
            T: (gas.DmInDTheta / gas.Mu) - (gas.DvDTheta / gas.Vu));
    }

    private static double GasExchangeDeterminant(GasExchangeCoefficients c) =>
        (c.A * c.J * c.G * c.P) - (c.E * c.N * c.D * c.K)
        + (c.I * c.N * c.D * c.G) - (c.M * c.J * c.D * c.G);

    /// <summary>
    /// Burnt volume equation during gas exchange, Delphi <c>dVbdThetaGE</c>.
    /// </summary>
    /// <remarks>
    /// <b>Unreachable in the original.</b> The overlap state assigns the single-zone
    /// pressure equation and leaves the other three components at
    /// <see cref="Zero"/>; the four lines that would have installed this set are
    /// commented out at <c>ICEngine2Z.pas:725-728</c>, the only place they were ever
    /// referenced. Ported so the behaviour exists if that block is ever restored, but
    /// nothing in the port calls it either and no reference data exercises it. The same
    /// applies to the other three <c>*GasExchange</c> equations. See ISSUES.md B37.
    /// </remarks>
    public double BurntVolumeRateGasExchange(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = GasExchange(crankAngleRadians, y);

        return -((-c.J * c.G * c.P * c.Q) + (c.D * c.R * c.K * c.N)
                 - (c.N * c.D * c.G * c.S) + (c.J * c.D * c.G * c.T))
               / GasExchangeDeterminant(c);
    }

    /// <inheritdoc cref="BurntVolumeRateGasExchange"/>
    public double PressureRateGasExchange(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = GasExchange(crankAngleRadians, y);

        return ((c.P * c.Q * c.E * c.K) - (c.P * c.Q * c.I * c.G) - (c.R * c.A * c.K * c.P)
                + (c.R * c.M * c.D * c.K) + (c.S * c.A * c.G * c.P) - (c.S * c.M * c.D * c.G)
                - (c.D * c.T * c.E * c.K) + (c.D * c.T * c.I * c.G))
               / GasExchangeDeterminant(c);
    }

    /// <inheritdoc cref="BurntVolumeRateGasExchange"/>
    public double BurntTemperatureRateGasExchange(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = GasExchange(crankAngleRadians, y);

        return ((-c.E * c.J * c.P * c.Q) + (c.R * c.A * c.J * c.P) + (c.R * c.I * c.D * c.N)
                - (c.R * c.M * c.D * c.J) - (c.E * c.D * c.N * c.S) + (c.E * c.D * c.J * c.T))
               / GasExchangeDeterminant(c);
    }

    /// <inheritdoc cref="BurntVolumeRateGasExchange"/>
    public double UnburntTemperatureRateGasExchange(double crankAngleRadians, ReadOnlySpan<double> y)
    {
        var c = GasExchange(crankAngleRadians, y);

        return ((-c.Q * c.E * c.K * c.N) + (c.Q * c.I * c.G * c.N) - (c.Q * c.M * c.G * c.J)
                + (c.A * c.K * c.N * c.R) - (c.A * c.G * c.N * c.S) + (c.A * c.G * c.J * c.T))
               / GasExchangeDeterminant(c);
    }
}
