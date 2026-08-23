namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TProfile</c> (Profiles.pas): a cam or valve lift profile
/// loaded from a <c>.cam</c> file.
/// </summary>
public sealed class CamProfile
{
    /// <summary>Ordered profile points, replacing the Delphi <c>First</c>/<c>Current</c> linked list.</summary>
    public List<ProfilePoint> Points { get; } = [];

    /// <summary>Point spacing, <c>Spacing</c>.</summary>
    public double Spacing { get; set; }

    public bool ProfileOk { get; set; }

    public bool Modifying { get; set; }

    public double XMin { get; set; }

    public double XMax { get; set; }

    public double YMin { get; set; }

    public double YMax { get; set; }

    public double Lift { get; set; }

    public double Duration { get; set; }

    public string FileName { get; set; } = string.Empty;
}
