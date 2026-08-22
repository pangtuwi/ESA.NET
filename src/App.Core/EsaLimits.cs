namespace App.Core;

/// <summary>
/// Fixed capacities and physical constants carried over verbatim from the Delphi
/// original. SPEC.md section 6 records that the grid capacities are intentional
/// legacy design limits retained for the new software.
/// </summary>
public static class EsaLimits
{
    /// <summary>Inlet manifold grid points. <c>NI</c> in Manifolds.pas.</summary>
    public const int InletGridPoints = 68;

    /// <summary>Exhaust manifold grid points. <c>NE</c> in Manifolds.pas.</summary>
    public const int ExhaustGridPoints = 38;

    /// <summary>Calculated points per pipe step. <c>H</c> in Manifolds.pas.</summary>
    public const int CalculatedPipePoints = 4;

    /// <summary>Discharge-coefficient table extent. <c>maxxy</c> in IPolTab.pas.</summary>
    public const int MaxDischargeTableSize = 20;

    /// <summary>Manifold area/length table extent. <c>maxx</c> in FManfA.pas.</summary>
    public const int MaxManifoldAreaPoints = 50;

    /// <summary>Speed-keyed table extent. <c>maxinarray</c> in WallTemps.pas and ExhBackPandT.pas.</summary>
    public const int MaxSpeedTableRows = 40;

    /// <summary>Captured quantities per crank angle. <c>NoVals</c> in CAList2z.pas.</summary>
    public const int CapturedValueCount = 28;

    /// <summary>First crank angle held by a <see cref="Model.CrankAngleTrace"/>.</summary>
    public const int FirstCrankAngle = -359;

    /// <summary>Last crank angle held by a <see cref="Model.CrankAngleTrace"/>.</summary>
    public const int LastCrankAngle = 360;

    /// <summary>Stored performance points. <c>MaxNoPoints</c> in PerfData.pas.</summary>
    public const int MaxPerformancePoints = 100;

    /// <summary>ODE system extent. <c>MaxN</c> in RKf5.pas.</summary>
    public const int MaxEquations = 10;

    /// <summary>Species tracked by the equilibrium model. See <see cref="Species"/>.</summary>
    public const int SpeciesCount = 12;

    /// <summary>Universal gas constant as used by Gasses2Z.pas (<c>Runiversal</c>).</summary>
    public const double RUniversal = 287.0;

    /// <summary>
    /// Woshini coefficient for compression, combustion and expansion. SPEC.md
    /// section 5 records this as an empirically validated calibration value.
    /// </summary>
    public const double WoshiniC1Closed = 2.28;

    /// <summary>Woshini coefficient for exhaust, overlap and intake.</summary>
    public const double WoshiniC1GasExchange = 6.18;

    /// <summary>Minimum number of simulated cycles accepted by the run options (SPEC.md section 1).</summary>
    public const int MinimumCycles = 3;

    /// <summary>Rows accepted by the multi-run grid (SPEC.md section 1).</summary>
    public const int MaxMultiRunRows = 100;
}
