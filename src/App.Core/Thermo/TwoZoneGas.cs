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
    /// This is a function in the original and it has a side effect: it recomputes and
    /// stores <c>xb</c> before using it. Reproduced, because the callers in
    /// <c>ICEngine2Z.pas</c> read <c>Tgas</c> at points where nothing else has
    /// refreshed <c>xb</c>. See ISSUES.md B27.
    /// </remarks>
    public double GasTemperature()
    {
        State.Xb = State.Mb == 0 ? 0 : State.Mb / State.MGas;
        return (State.Xb * State.Tb) + ((1 - State.Xb) * State.Tu);
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

        // Always true after the clamps above, which pin xb into [0.01, 0.99]. Kept as
        // written. See ISSUES.md B28.
        if (gas.Xb > 0 && gas.Xb < 1)
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

        gas.DmbDTheta = BurnRate(crankAngleRadians) * gas.MGas;
    }

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
    /// Fraction of the charge burnt at this crank angle. Port of the private
    /// <c>xburnt</c>: a raised cosine from the spark to the end of the burn angle.
    /// </summary>
    /// <param name="crankAngleRadians">
    /// Crank angle in <b>radians</b>, converted here because <c>ThetaSpark</c> and
    /// <c>BurnAngle</c> are both in degrees.
    /// </param>
    public double BurntFraction(double crankAngleRadians)
    {
        var theta = crankAngleRadians * 180 / Math.PI;
        var spark = State.ThetaSpark;
        var burnAngle = State.Fuel.BurnAngle;

        if (theta <= spark)
        {
            return 0;
        }

        if (theta < spark + burnAngle)
        {
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
        var spark = State.ThetaSpark;
        var burnAngle = State.Fuel.BurnAngle;

        if (theta <= spark)
        {
            return 0;
        }

        if (theta < spark + burnAngle)
        {
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
