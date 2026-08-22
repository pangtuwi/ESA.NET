namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TWallTemps</c> (WallTemps.pas): RPM-keyed head, piston, upper
/// liner and lower liner temperatures, loaded from a <c>.cwt</c> file.
/// </summary>
public sealed class WallTemperatureTable
{
    public List<double> Rpm { get; } = [];

    public List<double> HeadTemperature { get; } = [];

    public List<double> PistonTemperature { get; } = [];

    public List<double> UpperLinerTemperature { get; } = [];

    public List<double> LowerLinerTemperature { get; } = [];

    public string FileName { get; set; } = string.Empty;
}
