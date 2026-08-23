namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TExhaustPandT</c> (ExhBackPandT.pas): RPM-keyed exhaust back
/// pressure and temperature, loaded from an <c>.exh</c> file.
/// </summary>
public sealed class ExhaustBackPressureTable
{
    public List<double> Rpm { get; } = [];

    public List<double> Pressure { get; } = [];

    public List<double> Temperature { get; } = [];

    /// <summary>Atmospheric pressure used as the table's reference, <c>PAtm</c>.</summary>
    public double AtmosphericPressure { get; set; }

    public string FileName { get; set; } = string.Empty;
}
