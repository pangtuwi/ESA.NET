namespace App.Core.Model;

/// <summary>
/// Application defaults held in <c>ESA.ini</c> and read by Delphi
/// <c>IniValues.LoadIniValues</c> (SPEC.md section 3). Defaults match the Delphi
/// fallbacks. The reader for this file is phase 3 work.
/// </summary>
public sealed class SimulationSettings
{
    public string ErrorLogFileName { get; set; } = "ESA2z1z.err";

    public string TextSaveFileName { get; set; } = "Lastcyc.txt";

    public string EngineFileName { get; set; } = "Default.eng";

    public double EngineSpeed { get; set; } = 4000;

    public int CycleCount { get; set; } = 6;

    /// <summary>Cycles run one-zone before switching to two zones, Delphi <c>No1zcycles</c>.</summary>
    public int OneZoneCycleCount { get; set; } = 1;

    /// <summary>
    /// Convergence tolerance in micrograms. SPEC.md section 5: the run stops when
    /// <c>abs(TotalMInIV - TotalMOutEV) * 1e6</c> falls below this value.
    /// </summary>
    public double MassBalance { get; set; } = 1;
}
