namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TCAPoint</c> (CAList2z.pas): the 28 quantities captured at one
/// crank angle. The Delphi class exposes named read-only properties over
/// <c>Value[1..28]</c>; those names are reproduced by <see cref="CapturedQuantity"/>.
/// </summary>
public sealed class CrankAnglePoint
{
    private readonly double[] _values = new double[EsaLimits.CapturedValueCount];

    /// <summary>Indexed by the Delphi 1-based ordinal.</summary>
    public double this[int oneBasedIndex]
    {
        get => _values[oneBasedIndex - 1];
        set => _values[oneBasedIndex - 1] = value;
    }

    public double this[CapturedQuantity quantity]
    {
        get => _values[(int)quantity - 1];
        set => _values[(int)quantity - 1] = value;
    }

    public ReadOnlySpan<double> AsSpan() => _values;
}

/// <summary>
/// The named subset of the 28 captured quantities, taken from the properties
/// declared on Delphi <c>TCAPoint</c>. Ordinals not named here are captured but
/// unnamed in the original.
/// </summary>
public enum CapturedQuantity
{
    Volume = 1,
    Pressure = 2,
    CylinderMass = 3,
    BurntMass = 4,
    UnburntMass = 5,
    MassIn = 6,
    MassOut = 7,
    BurntTemperature = 10,
    UnburntTemperature = 11,
    InletValveArea = 16,
    ExhaustValveArea = 17,
    InletVelocity = 18,
    ExhaustVelocity = 19,
    InletPressure = 20,
    ExhaustPressure = 21,
}
