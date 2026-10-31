namespace App.Core.Simulation;

/// <summary>
/// ISSUES.md B97: the cylinder's crevices, the narrow volumes around the piston rings, the
/// spark plug and the head gasket, as one store at cylinder pressure and wall temperature.
/// </summary>
/// <remarks>
/// <para>
/// Heywood (1988, section 8.6, pp. 360-365): as the cylinder pressure rises, unburnt mixture
/// is forced into each crevice and cools there to close to the wall temperature, the
/// volumes being thin; after the flame arrives burnt gas follows; once the pressure falls
/// the gas flows back. Some 5 to 10 per cent of the charge is in the crevices at peak
/// pressure, most of it returns during expansion, and about 1 per cent of the charge goes
/// past the rings to the crankcase as blow-by.
/// </para>
/// <para>
/// The store holds whatever mass puts it at the cylinder's pressure, <c>p V / (R T)</c>, with
/// <c>T</c> the wall temperature. It keeps the unburnt and burnt gas it holds apart, and gives
/// gas back in proportion. Blow-by is a share of each release, so that over a cycle it comes to
/// about <see cref="BlowByShare"/> of the trapped charge, as Heywood's figure for an engine at
/// wide-open throttle and 2000 rpm. The volume between the rings, where Heywood's measurements
/// put the pressure several times below the cylinder's, is left out: its gas is mostly what
/// becomes the blow-by.
/// </para>
/// <para>
/// This class only keeps the books. What a release or admission does to the cylinder's
/// zones, and the heat the gas exchanges with the walls, is the solver's
/// (<c>CycleSolver.ExchangeWithCrevices</c>).
/// </para>
/// </remarks>
public sealed class CreviceStore
{
    /// <summary>
    /// The crevice volume at cylinder pressure as a fraction of the clearance volume:
    /// Heywood's Table 8.1 (p. 361), a production V-6 measured cold, volume above the first
    /// ring 1.05, behind it 0.52, spark plug thread 0.28 and head gasket 0.34 per cent. Its
    /// volumes between and behind the lower rings, another 1.29 per cent, are not included;
    /// see the remarks.
    /// </summary>
    public const double DefaultVolumeFraction = 0.0219;

    /// <summary>
    /// Blow-by per cycle as a fraction of the trapped charge: "about 1 percent goes to the
    /// crankcase as blowby" (Heywood p. 362).
    /// </summary>
    public const double DefaultBlowByShare = 0.01;

    /// <param name="volume">The crevice volume, m^3.</param>
    /// <param name="temperature">The wall temperature the crevice gas is held at, K.</param>
    /// <param name="blowByShare">Blow-by per cycle as a fraction of the trapped charge.</param>
    public CreviceStore(double volume, double temperature, double blowByShare)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(temperature);
        ArgumentOutOfRangeException.ThrowIfNegative(blowByShare);

        Volume = volume;
        Temperature = temperature;
        BlowByShare = blowByShare;
    }

    /// <summary>The crevice volume, m^3.</summary>
    public double Volume { get; }

    /// <summary>The wall temperature the crevice gas is held at, K.</summary>
    public double Temperature { get; }

    /// <summary>Blow-by per cycle as a fraction of the trapped charge.</summary>
    public double BlowByShare { get; }

    /// <summary>Unburnt mixture in the crevices, kg.</summary>
    public double UnburntMass { get; private set; }

    /// <summary>Burnt gas in the crevices, kg.</summary>
    public double BurntMass { get; private set; }

    /// <summary>Everything in the crevices, kg.</summary>
    public double Mass => UnburntMass + BurntMass;

    /// <summary>The most the crevices have held this cycle, kg.</summary>
    public double PeakMass { get; private set; }

    /// <summary>The mass lost past the rings this cycle, kg.</summary>
    public double BlowBy { get; private set; }

    /// <summary>Starts a cycle's peak and blow-by from what the crevices hold now.</summary>
    public void StartCycle()
    {
        PeakMass = Mass;
        BlowBy = 0;
    }

    /// <summary>
    /// Brings the crevices to the cylinder's pressure: admits gas if they hold less than
    /// <c>p V / (R T)</c>, releases it in proportion to what they hold if more.
    /// </summary>
    /// <param name="pressure">The cylinder pressure, Pa.</param>
    /// <param name="unburntGasConstant">The unburnt mixture's gas constant at the wall temperature, J/(kg K).</param>
    /// <param name="burntGasConstant">The burnt gas's gas constant at the wall temperature, J/(kg K).</param>
    /// <param name="admitBurnt">Whether gas admitted now is burnt; otherwise unburnt.</param>
    /// <param name="maximumAdmission">The most that may be admitted, kg: what the source zone can spare.</param>
    /// <param name="trappedMass">The cycle's trapped charge, kg, which blow-by is a share of.</param>
    /// <param name="blowBy">Whether a release loses its share to blow-by.</param>
    public CreviceExchange Exchange(
        double pressure,
        double unburntGasConstant,
        double burntGasConstant,
        bool admitBurnt,
        double maximumAdmission,
        double trappedMass,
        bool blowBy)
    {
        var held = (UnburntMass * unburntGasConstant) + (BurntMass * burntGasConstant);
        var excess = (pressure * Volume / Temperature) - held;

        if (excess >= 0)
        {
            var admitted = Math.Min(excess / (admitBurnt ? burntGasConstant : unburntGasConstant), maximumAdmission);

            if (admitBurnt)
            {
                BurntMass += admitted;
            }
            else
            {
                UnburntMass += admitted;
            }

            PeakMass = Math.Max(PeakMass, Mass);

            return admitBurnt
                ? new CreviceExchange(AdmittedBurnt: admitted)
                : new CreviceExchange(AdmittedUnburnt: admitted);
        }

        var share = Math.Min(1, -excess / held);
        var unburnt = share * UnburntMass;
        var burnt = share * BurntMass;

        UnburntMass -= unburnt;
        BurntMass -= burnt;

        var lost = blowBy && PeakMass > 0 ? Math.Min(1, BlowByShare * trappedMass / PeakMass) : 0;
        BlowBy += lost * (unburnt + burnt);

        return new CreviceExchange(
            ReleasedUnburnt: (1 - lost) * unburnt,
            ReleasedBurnt: (1 - lost) * burnt,
            BlownUnburnt: lost * unburnt,
            BlownBurnt: lost * burnt);
    }
}

/// <summary>One step's exchange between the cylinder and its crevices, kg each.</summary>
/// <param name="AdmittedUnburnt">Unburnt mixture that entered the crevices.</param>
/// <param name="AdmittedBurnt">Burnt gas that entered the crevices.</param>
/// <param name="ReleasedUnburnt">Unburnt mixture that came back to the cylinder.</param>
/// <param name="ReleasedBurnt">Burnt gas that came back to the cylinder.</param>
/// <param name="BlownUnburnt">Unburnt mixture lost past the rings.</param>
/// <param name="BlownBurnt">Burnt gas lost past the rings.</param>
public readonly record struct CreviceExchange(
    double AdmittedUnburnt = 0,
    double AdmittedBurnt = 0,
    double ReleasedUnburnt = 0,
    double ReleasedBurnt = 0,
    double BlownUnburnt = 0,
    double BlownBurnt = 0)
{
    /// <summary>Everything that entered the crevices.</summary>
    public double Admitted => AdmittedUnburnt + AdmittedBurnt;

    /// <summary>Everything that came back to the cylinder.</summary>
    public double Released => ReleasedUnburnt + ReleasedBurnt;

    /// <summary>Everything lost past the rings.</summary>
    public double Blown => BlownUnburnt + BlownBurnt;
}
