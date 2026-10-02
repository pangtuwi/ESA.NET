namespace App.Core.Model;

/// <summary>Port of Delphi <c>TValve</c> (Valves.pas).</summary>
/// <remarks>
/// SI, as the Delphi object was: the <c>.eng</c> file's degrees before or after a dead
/// centre and millimetres are converted on the way in, through
/// <see cref="EngineFileUnits"/>. See ISSUES.md A6.
/// </remarks>
public sealed class Valve
{
    /// <summary>Number of valves of this kind per cylinder, <c>No</c>.</summary>
    public int Count { get; set; }

    /// <summary>
    /// Opening crank angle in degrees on the solver's scale, Delphi <c>O</c>: 341 for an
    /// inlet valve opening 19 degrees before top dead centre.
    /// </summary>
    public double OpenAngle { get; set; }

    /// <summary>
    /// Closing crank angle in degrees on the solver's scale, Delphi <c>C</c>: -100 for an
    /// inlet valve closing 80 degrees after bottom dead centre.
    /// </summary>
    public double CloseAngle { get; set; }

    /// <summary>Valve diameter in metres, Delphi <c>D</c>.</summary>
    public double Diameter { get; set; }

    /// <summary>Maximum lift in metres, Delphi <c>MaxLift</c>.</summary>
    public double MaxLift { get; set; }

    public string ProfileFile { get; set; } = string.Empty;

    public CamProfile Profile { get; set; } = new();

    public DischargeCoefficientTable CdForward { get; set; } = new();

    public DischargeCoefficientTable CdReverse { get; set; } = new();
}
