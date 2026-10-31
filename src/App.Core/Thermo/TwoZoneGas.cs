using App.Core.Model;

namespace App.Core.Thermo;

/// <summary>
/// One gas volume split into a burnt and an unburnt zone. Port of Delphi
/// <c>TGas2Z</c> (Gasses2Z.pas).
/// </summary>
/// <remarks>
/// <para>
/// The engine holds three of these: the cylinder, the inlet plenum and the
/// atmosphere. Each owns a burnt and an unburnt <see cref="GasPropertyModel"/> and the
/// <see cref="Model.Gas"/> record that carries its state.
/// </para>
/// <para>
/// The four <c>Update*</c> methods are the whole of the class. Each is handed a trial
/// state by the integrator, writes it into <see cref="State"/>, refreshes the
/// thermodynamic properties from the two property models, and leaves the result there
/// for the derivative functions to read back. They are deliberately <b>not</b> pure:
/// RKF5 calls a derivative six times per step with different trial vectors, and the
/// order in which this scratchpad is overwritten is part of the answer. See
/// task-phase4.md, "The ODEs are free functions over a global, and they mutate".
/// </para>
/// </remarks>
public sealed class TwoZoneGas
{
    /// <summary>Creates a gas that owns its own state.</summary>
    public TwoZoneGas()
        : this(new Gas())
    {
    }

    /// <summary>
    /// Wraps an existing state record. <c>TEngine2z</c> holds four of these and the rest
    /// of the engine reads their fields directly, so the solver passes in the
    /// <see cref="Model.Engine"/>'s own <see cref="Model.Gas"/> instances rather than
    /// letting each gas allocate a private one that nothing else can see.
    /// </summary>
    public TwoZoneGas(Gas state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    /// <summary>The gas state, Delphi's fields on <c>TGas2Z</c> itself.</summary>
    public Gas State { get; }

    /// <summary>Properties of the burnt zone, Delphi <c>Burnt : TProp</c>.</summary>
    public GasPropertyModel Burnt { get; } = new(burned: true);

    /// <summary>Properties of the unburnt zone, Delphi <c>Unburnt : TProp</c>.</summary>
    public GasPropertyModel Unburnt { get; } = new(burned: false);

    /// <summary>
    /// Mass-fraction weighted mean of the two zone temperatures, Delphi
    /// <c>TGas2z.Tgas</c>.
    /// </summary>
    /// <remarks>
    /// The original's <c>Tgas</c> is a function that writes: it recomputes <c>xb</c> from
    /// the zone masses and <b>stores</b> it before weighting the temperatures. This one
    /// computes the same fraction from the same masses and stores nothing. The write was
    /// never load-bearing in the port: the only reader of the stored fraction is
    /// <see cref="UpdateB"/>, which assigns it first. See ISSUES.md B27.
    /// </remarks>
    public double GasTemperature()
    {
        var burntFraction = State.Mb == 0 ? 0 : State.Mb / State.MGas;
        return (burntFraction * State.Tb) + ((1 - burntFraction) * State.Tu);
    }

    /// <summary>
    /// Burning: two zones, with the burnt mass fraction driven by the Wiebe-like cosine
    /// profile. Port of <c>UpdateB</c>.
    /// </summary>
    /// <param name="crankAngleRadians">Delphi <c>x1</c>, crank angle in radians.</param>
    /// <param name="volume">Delphi <c>V1</c>, total cylinder volume.</param>
    /// <param name="dVdTheta">Delphi <c>dVdtheta1</c>.</param>
    /// <param name="burntVolume">Delphi <c>Vb1</c>, the integrator's <c>y[1]</c>.</param>
    /// <param name="pressure">Delphi <c>P1</c>, the integrator's <c>y[2]</c>.</param>
    /// <param name="burntTemperature">Delphi <c>Tb1</c>, the integrator's <c>y[3]</c>.</param>
    /// <param name="unburntTemperature">Delphi <c>Tu1</c>, the integrator's <c>y[4]</c>.</param>
    public void UpdateB(
        double crankAngleRadians,
        double volume,
        double dVdTheta,
        double burntVolume,
        double pressure,
        double burntTemperature,
        double unburntTemperature)
    {
        var gas = State;

        gas.PGas = pressure;
        gas.Tb = burntTemperature;
        gas.Tu = unburntTemperature;
        gas.Vb = burntVolume;
        gas.VGas = volume;

        // The original's own comment on this line is "//##?? iffy line???". SPEC.md
        // section 5 records it as an intentional safeguard; it stays.
        if (gas.Vb > gas.VGas)
        {
            gas.Vb = gas.VGas;
        }

        gas.DvDTheta = dVdTheta;

        // Note the ordering: Vu follows the clamped Vb but precedes the xb clamps
        // below, so the zone volumes and the zone masses need not agree.
        gas.Vu = gas.VGas - gas.Vb;

        gas.Xb = BurntFraction(crankAngleRadians);

        if (gas.Xb < 0.01)
        {
            gas.Xb = 0.01;
        }

        if (1 - gas.Xb < 0.01)
        {
            gas.Xb = 0.99;
        }

        if (BurnableMass is { } burnable)
        {
            // B97: the burn law burns the charge the cylinder holds, not what the crevices
            // have taken, and gas they give back joins the burnt zone. The unburnt zone is
            // the unburnt share of that charge and the burnt zone is the rest of the cylinder.
            gas.Mu = (1 - gas.Xb) * burnable;
            gas.Mb = gas.MGas - gas.Mu;
        }
        // Always true after the clamps above, which pin xb into [0.01, 0.99]. Kept as
        // written. See ISSUES.md B28.
        else if (gas.Xb > 0 && gas.Xb < 1)
        {
            gas.Mb = gas.Xb * gas.MGas;
            gas.Mu = (1 - gas.Xb) * gas.MGas;
        }

        RefreshUnburntProperties();
        RefreshBurntProperties();

        gas.UGas = (gas.Xb * gas.Ub) + ((1 - gas.Xb) * gas.Uu);
        gas.RGas = (gas.Xb * gas.Rb) + ((1 - gas.Xb) * gas.Ru);
        gas.HGas = ((1 - gas.Xb) * gas.Hu) + (gas.Xb * gas.Hb);

        // Unburnt first, then burnt: Get_Gamma re-solves the equilibrium, so the order
        // decides what the solver is left holding.
        var unburntGamma = Unburnt.Gamma(gas.PGas, gas.Tu);
        var burntGamma = Burnt.Gamma(gas.PGas, gas.Tb);
        gas.Gamma = ((1 - gas.Xb) * unburntGamma) + (gas.Xb * burntGamma);

        gas.DmbDTheta = BurnRate(crankAngleRadians) * (BurnableMass ?? gas.MGas);
    }

    /// <summary>
    /// ISSUES.md B97: the charge the burn law burns while the cylinder exchanges gas with its
    /// crevices, kg, or null without them, when it is the whole cylinder. It is the unburnt
    /// zone's mass over the unburnt fraction: unburnt gas the crevices take leaves the burnt
    /// zone as it was, and the burn then finishes on what the cylinder holds. Set by the solver
    /// at the start of combustion and as the crevices take unburnt gas, cleared as it ends.
    /// </summary>
    public double? BurnableMass { get; set; }

    /// <summary>
    /// ISSUES.md B97: the cycle's trapped charge, the cylinder's and the crevices', kg. The
    /// fuel it carries burns in proportion to <see cref="BurnableMass"/>.
    /// </summary>
    public double TrappedMass { get; set; }

    /// <summary>
    /// Burnt down: one zone, all products. Port of <c>UpdateBD</c>, used through
    /// expansion and blowdown.
    /// </summary>
    /// <remarks><paramref name="burntVolume"/> is accepted and ignored, as in the original.</remarks>
    public void UpdateBD(
        double volume,
        double dVdTheta,
        double burntVolume,
        double pressure,
        double burntTemperature)
    {
        _ = burntVolume;

        var gas = State;

        gas.PGas = pressure;
        gas.Tb = burntTemperature;
        gas.Tu = burntTemperature;
        gas.Vb = volume;
        gas.VGas = volume;
        gas.DvDTheta = dVdTheta;
        gas.Vu = 0;
        gas.Mu = 0;
        gas.Mb = gas.MGas;

        RefreshBurntProperties();

        gas.UGas = gas.Ub;
        gas.RGas = gas.Rb;
        gas.HGas = gas.Hb;
        gas.Gamma = Burnt.Gamma(gas.PGas, gas.Tb);
        gas.DmbDTheta = 0;
    }

    /// <summary>
    /// Unburnt: one zone, fresh charge. Port of <c>UpdateUB</c>, used through intake
    /// and compression.
    /// </summary>
    /// <remarks><paramref name="burntVolume"/> is accepted and ignored, as in the original.</remarks>
    public void UpdateUB(
        double volume,
        double dVdTheta,
        double burntVolume,
        double pressure,
        double unburntTemperature)
    {
        _ = burntVolume;

        var gas = State;

        gas.PGas = pressure;
        gas.Tb = unburntTemperature;
        gas.Tu = unburntTemperature;
        gas.Vu = volume;
        gas.VGas = volume;
        gas.DvDTheta = dVdTheta;
        gas.Vb = 0;
        gas.Mb = 0;
        gas.Mu = gas.MGas;

        RefreshUnburntProperties();

        gas.UGas = gas.Uu;
        gas.RGas = gas.Ru;
        gas.HGas = gas.Hu;
        gas.Gamma = Unburnt.Gamma(gas.PGas, gas.Tu);
        gas.DmbDTheta = 0;
    }

    /// <summary>
    /// Gas exchange: the unburnt model over the whole volume. Port of <c>UpdateGE</c>,
    /// used through valve overlap.
    /// </summary>
    /// <remarks>
    /// <paramref name="burntVolume"/> and <paramref name="burntTemperature"/> are
    /// accepted and ignored — both zone temperatures come from
    /// <paramref name="unburntTemperature"/>. Both zone volumes are set to zero while
    /// <c>Vgas</c> takes the full volume, so <c>Vu + Vb</c> does not equal <c>Vgas</c>
    /// in this state alone. Nothing downstream reads them here. See ISSUES.md B29.
    /// </remarks>
    public void UpdateGE(
        double volume,
        double dVdTheta,
        double burntVolume,
        double pressure,
        double burntTemperature,
        double unburntTemperature)
    {
        _ = burntVolume;
        _ = burntTemperature;

        var gas = State;

        gas.PGas = pressure;
        gas.Tb = unburntTemperature;
        gas.Tu = unburntTemperature;
        gas.Vb = 0;
        gas.Vu = 0;
        gas.VGas = volume;
        gas.DvDTheta = dVdTheta;

        RefreshUnburntProperties();

        gas.UGas = gas.Uu;
        gas.RGas = gas.Ru;
        gas.HGas = gas.Hu;
        gas.Gamma = Unburnt.Gamma(gas.PGas, gas.Tu);
        gas.DmbDTheta = 0;
    }

    /// <summary>
    /// Valve overlap as two zones, the burnt residual and the fresh charge, each at its own
    /// temperature with its own properties. B37's replacement for <see cref="UpdateGE"/>,
    /// which the gas-exchange equations cannot run on: it leaves both zone volumes at zero
    /// and takes both temperatures from the unburnt one. See ISSUES.md B37 and B29.
    /// </summary>
    /// <remarks>
    /// The zone masses are the overlap bookkeeping's, as they stand; this sets the volumes,
    /// temperatures and properties around them.
    /// </remarks>
    public void UpdateGasExchangeZones(
        double volume,
        double dVdTheta,
        double burntVolume,
        double pressure,
        double burntTemperature,
        double unburntTemperature)
    {
        var gas = State;

        gas.PGas = pressure;
        gas.Tb = burntTemperature;
        gas.Tu = unburntTemperature;
        gas.VGas = volume;
        gas.DvDTheta = dVdTheta;
        gas.Vb = burntVolume;
        gas.Vu = volume - burntVolume;

        RefreshUnburntProperties();
        RefreshBurntProperties();

        var burntFraction = gas.MGas > 0 ? gas.Mb / gas.MGas : 0;
        gas.UGas = (burntFraction * gas.Ub) + ((1 - burntFraction) * gas.Uu);
        gas.RGas = (burntFraction * gas.Rb) + ((1 - burntFraction) * gas.Ru);
        gas.HGas = (burntFraction * gas.Hb) + ((1 - burntFraction) * gas.Hu);

        var unburntGamma = Unburnt.Gamma(gas.PGas, gas.Tu);
        var burntGamma = Burnt.Gamma(gas.PGas, gas.Tb);
        gas.Gamma = ((1 - burntFraction) * unburntGamma) + (burntFraction * burntGamma);
        gas.DmbDTheta = 0;
    }

    /// <summary>The Wiebe efficiency parameter <c>a</c>: 99.3 per cent burnt by the end of the burn angle (Heywood 1988, ch. 9).</summary>
    public const double WiebeEfficiency = 5;

    /// <summary>The Wiebe form factor <c>m</c> (Heywood 1988, ch. 9).</summary>
    public const double WiebeForm = 2;

    /// <summary>
    /// Burn by a Wiebe law from the start of combustion, the spark plus
    /// <see cref="Model.Fuel.CombustionDelay"/>, rather than the original's raised cosine
    /// from the spark itself. Set from <c>CorrectionCatalogue.WiebeBurn</c>; ISSUES.md B89.
    /// </summary>
    public bool WiebeBurn { get; set; }

    /// <summary>
    /// The flame-development angle at the running speed, spark to 10 per cent burnt, in
    /// degrees, when B90 supplies the default delay; null otherwise. Set by the solver
    /// from <see cref="FlameDevelopment.Angle"/>.
    /// </summary>
    public double? FlameDevelopmentAngle { get; set; }

    /// <summary>
    /// Degrees from the spark to the start of combustion: the file's
    /// <see cref="Model.Fuel.CombustionDelay"/> if it gives one; otherwise, under B90, the
    /// delay that puts the Wiebe law's 10 per cent point at the flame-development angle,
    /// for the burn angle the cylinder has now; otherwise none. Only under B89.
    /// </summary>
    public double DelayToStartOfCombustion =>
        !WiebeBurn ? 0
        : State.Fuel.CombustionDelay
          ?? (FlameDevelopmentAngle is { } angle ? FlameDevelopment.DefaultDelay(angle, State.Fuel.BurnAngle) : 0);

    /// <summary>
    /// The crank angle combustion starts at, in degrees: the spark, plus the delay to the
    /// start of combustion under B89.
    /// </summary>
    public double StartOfCombustion => State.ThetaSpark + DelayToStartOfCombustion;

    /// <summary>
    /// Fraction of the charge burnt at this crank angle. Port of the private
    /// <c>xburnt</c>: a raised cosine from the spark to the end of the burn angle.
    /// </summary>
    /// <remarks>
    /// Under B89 it is a Wiebe law, <c>1 - exp(-a u^(m+1))</c> with <c>u</c> the fraction
    /// of the burn angle elapsed since the start of combustion, divided by its value at the
    /// end, <c>1 - exp(-a)</c>, so that the burn is complete where the state map ends
    /// combustion and expansion entry takes no unburnt charge at once. The raised cosine
    /// burns from the spark at a finite rate straight away; the Wiebe law starts slowly, as
    /// a flame kernel does (ISSUES.md B89).
    /// </remarks>
    /// <param name="crankAngleRadians">
    /// Crank angle in <b>radians</b>, converted here because <c>ThetaSpark</c> and
    /// <c>BurnAngle</c> are both in degrees.
    /// </param>
    public double BurntFraction(double crankAngleRadians)
    {
        var theta = crankAngleRadians * 180 / Math.PI;
        var spark = StartOfCombustion;
        var burnAngle = State.Fuel.BurnAngle;

        if (theta <= spark)
        {
            return 0;
        }

        if (theta < spark + burnAngle)
        {
            if (WiebeBurn)
            {
                var u = (theta - spark) / burnAngle;
                return (1 - Math.Exp(-WiebeEfficiency * Math.Pow(u, WiebeForm + 1)))
                       / (1 - Math.Exp(-WiebeEfficiency));
            }

            return 0.5 * (1 - Math.Cos(Math.PI * (theta - spark) / burnAngle));
        }

        return 1;
    }

    /// <summary>
    /// Rate of burn per radian of crank angle, Delphi <c>dxdTheta</c>. The derivative of
    /// <see cref="BurntFraction"/>, with the burn angle converted to radians so the
    /// result is per radian while its argument is compared in degrees.
    /// </summary>
    public double BurnRate(double crankAngleRadians)
    {
        var theta = crankAngleRadians * 180 / Math.PI;
        var spark = StartOfCombustion;
        var burnAngle = State.Fuel.BurnAngle;

        if (theta <= spark)
        {
            return 0;
        }

        if (theta < spark + burnAngle)
        {
            if (WiebeBurn)
            {
                var u = (theta - spark) / burnAngle;
                return WiebeEfficiency * (WiebeForm + 1) * Math.Pow(u, WiebeForm)
                       * Math.Exp(-WiebeEfficiency * Math.Pow(u, WiebeForm + 1))
                       / (1 - Math.Exp(-WiebeEfficiency))
                       / (burnAngle * Math.PI / 180);
            }

            return 0.5 * Math.PI / (burnAngle * Math.PI / 180)
                * Math.Sin(Math.PI * (theta - spark) / burnAngle);
        }

        return 0;
    }

    /// <summary>
    /// Delphi passes <c>Ru, hu, uu, Cpu, dudTu, dudPu, dudFu</c> to <c>ReturnProps</c>
    /// as var parameters, so the flat fields on the gas are the output buffer. Here the
    /// model fills <see cref="Model.Gas.Unburnt"/> and the flat fields mirror it.
    /// </summary>
    private void RefreshUnburntProperties()
    {
        var gas = State;
        var properties = gas.Unburnt;

        Unburnt.ReturnProps(gas.PGas, gas.Tu, properties);

        gas.Ru = properties.R;
        gas.Hu = properties.H;
        gas.Uu = properties.U;
        gas.Cpu = properties.Cp;
        gas.DuDtu = properties.DuDt;
        gas.DuDpu = properties.DuDp;
        gas.DuDfu = properties.DuDf;
        gas.Error = properties.Error;
    }

    /// <inheritdoc cref="RefreshUnburntProperties"/>
    private void RefreshBurntProperties()
    {
        var gas = State;
        var properties = gas.Burnt;

        Burnt.ReturnProps(gas.PGas, gas.Tb, properties);

        gas.Rb = properties.R;
        gas.Hb = properties.H;
        gas.Ub = properties.U;
        gas.Cpb = properties.Cp;
        gas.DuDtb = properties.DuDt;
        gas.DuDpb = properties.DuDp;
        gas.DuDfb = properties.DuDf;
        gas.Error = properties.Error;
    }
}
