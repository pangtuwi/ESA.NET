namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TCdValve</c> (IPolTab.pas): a discharge-coefficient grid of
/// at most 20 by 20, bilinearly interpolated, loaded from a <c>.vcd</c> file.
/// </summary>
public sealed class DischargeCoefficientTable
{
    private const int Size = EsaLimits.MaxDischargeTableSize;

    /// <summary>Table values, <c>Cell</c>. Delphi indices are 1-based; these are 0-based.</summary>
    public double[,] Cell { get; } = new double[Size, Size];

    public double[] XIndex { get; } = new double[Size];

    public double[] YIndex { get; } = new double[Size];

    public int XCount { get; set; }

    public int YCount { get; set; }

    public string FileName { get; set; } = string.Empty;
}
