namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>EqSpecArray</c> (<c>array[1..12] of Extended</c>). Delphi's
/// 80-bit <c>Extended</c> has no .NET equivalent, so values are <see cref="double"/>;
/// the precision difference is a known port caveat recorded in CLAUDE.md.
/// </summary>
public sealed class SpeciesValues
{
    private readonly double[] _values = new double[EsaLimits.SpeciesCount];

    /// <summary>Indexed by <see cref="Species"/>, preserving the Delphi 1-based order.</summary>
    public double this[Species species]
    {
        get => _values[(int)species - 1];
        set => _values[(int)species - 1] = value;
    }

    /// <summary>Indexed by the Delphi 1-based ordinal.</summary>
    public double this[int oneBasedIndex]
    {
        get => _values[oneBasedIndex - 1];
        set => _values[oneBasedIndex - 1] = value;
    }

    public ReadOnlySpan<double> AsSpan() => _values;
}
