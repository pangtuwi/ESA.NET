namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TVarSpeedList</c> (VarSpeedList.pas): RPM-keyed values with
/// linear interpolation, loaded from a <c>.spk</c> file for spark angle.
/// </summary>
public sealed class SpeedKeyedTable
{
    public List<double> Rpm { get; } = [];

    public List<double> Values { get; } = [];

    public string FileName { get; set; } = string.Empty;
}
