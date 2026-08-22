namespace App.Core.Model;

/// <summary>Port of Delphi <c>TPerfPoint</c> (PerfData.pas).</summary>
public sealed class PerformancePoint
{
    public double Speed { get; set; }

    public double Torque { get; set; }

    public double Power { get; set; }

    /// <summary>Volumetric efficiency, <c>VolEff</c>.</summary>
    public double VolumetricEfficiency { get; set; }
}
