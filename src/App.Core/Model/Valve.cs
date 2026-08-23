namespace App.Core.Model;

/// <summary>Port of Delphi <c>TValve</c> (Valves.pas).</summary>
public sealed class Valve
{
    /// <summary>Number of valves of this kind per cylinder, <c>No</c>.</summary>
    public int Count { get; set; }

    /// <summary>Opening crank angle, <c>O</c>.</summary>
    public double OpenAngle { get; set; }

    /// <summary>Closing crank angle, <c>C</c>.</summary>
    public double CloseAngle { get; set; }

    /// <summary>Valve diameter, <c>D</c>.</summary>
    public double Diameter { get; set; }

    public double MaxLift { get; set; }

    public string ProfileFile { get; set; } = string.Empty;

    public CamProfile Profile { get; set; } = new();

    public DischargeCoefficientTable CdForward { get; set; } = new();

    public DischargeCoefficientTable CdReverse { get; set; } = new();
}
