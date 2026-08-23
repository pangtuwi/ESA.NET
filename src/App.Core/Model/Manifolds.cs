namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TManifolds</c> (Manifolds.pas): the inlet and
/// exhaust valves and pipes, the grid-sizing and valve-flow expressions, and the
/// fixed-capacity characteristic-line arrays for both pipes.
/// </summary>
public sealed class Manifolds
{
    /// <summary>Inlet valve, Delphi <c>IV</c>.</summary>
    public Valve InletValve { get; } = new();

    /// <summary>Exhaust valve, Delphi <c>EV</c>.</summary>
    public Valve ExhaustValve { get; } = new();

    /// <summary>Inlet pipe, Delphi <c>IManf</c>.</summary>
    public Pipe InletPipe { get; } = new();

    /// <summary>Exhaust pipe, Delphi <c>EManf</c>.</summary>
    public Pipe ExhaustPipe { get; } = new();

    /// <summary>Exhaust back pressure and temperature, Delphi <c>ExhBack</c>.</summary>
    public ExhaustBackPressureTable ExhaustBack { get; set; } = new();

    /// <summary>Plenum pressure expression, Delphi <c>CleanAirPresFn</c>.</summary>
    public ExpressionFunction PlenumPressureFunction { get; } = new();

    /// <summary>Inlet grid-size expression, Delphi <c>IGrid</c>.</summary>
    public GridSizeFunction InletGrid { get; } = new();

    /// <summary>Exhaust grid-size expression, Delphi <c>EGrid</c>.</summary>
    public GridSizeFunction ExhaustGrid { get; } = new();

    /// <summary>Inlet valve reverse-flow expression, Delphi <c>IVRFunc</c>.</summary>
    public ExpressionFunction InletValveReverse { get; } = new();

    /// <summary>Inlet valve forward-flow expression, Delphi <c>IVFFunc</c>.</summary>
    public ExpressionFunction InletValveForward { get; } = new();

    /// <summary>Inlet valve forward-reverse expression, Delphi <c>IVFRFunc</c>.</summary>
    public ExpressionFunction InletValveForwardReverse { get; } = new();

    /// <summary>Exhaust valve reverse-flow expression, Delphi <c>EVRFunc</c>.</summary>
    public ExpressionFunction ExhaustValveReverse { get; } = new();

    /// <summary>Exhaust valve forward-flow expression, Delphi <c>EVFFunc</c>.</summary>
    public ExpressionFunction ExhaustValveForward { get; } = new();

    /// <summary>Exhaust valve forward-reverse expression, Delphi <c>EVFRFunc</c>.</summary>
    public ExpressionFunction ExhaustValveForwardReverse { get; } = new();

    public double GammaIn { get; set; }

    public double GammaEx { get; set; }

    public double GammaCyl { get; set; }

    /// <summary>Plenum temperature, Delphi <c>PlenumT</c>.</summary>
    public double PlenumTemperature { get; set; }

    public PipeGrid Inlet { get; } = new(EsaLimits.InletGridPoints);

    public PipeGrid Exhaust { get; } = new(EsaLimits.ExhaustGridPoints);

    /// <summary>Inlet throat velocity, Delphi <c>Iut</c>.</summary>
    public double InletThroatVelocity { get; set; }

    /// <summary>Inlet throat speed of sound, Delphi <c>Ict</c>.</summary>
    public double InletThroatSpeedOfSound { get; set; }

    /// <summary>Inlet throat density, Delphi <c>IRt</c>.</summary>
    public double InletThroatDensity { get; set; }

    /// <summary>Inlet discharge coefficient, Delphi <c>ICd</c>.</summary>
    public double InletDischargeCoefficient { get; set; }

    /// <summary>Exhaust throat velocity, Delphi <c>Eut</c>.</summary>
    public double ExhaustThroatVelocity { get; set; }

    /// <summary>Exhaust throat speed of sound, Delphi <c>Ect</c>.</summary>
    public double ExhaustThroatSpeedOfSound { get; set; }

    /// <summary>Exhaust throat density, Delphi <c>ERt</c>.</summary>
    public double ExhaustThroatDensity { get; set; }

    /// <summary>Exhaust discharge coefficient, Delphi <c>ECd</c>.</summary>
    public double ExhaustDischargeCoefficient { get; set; }

    /// <summary>
    /// Whether the nine manifold output files are written on the final cycle,
    /// Delphi <c>SaveManifoldData</c> (SPEC.md section 3).
    /// </summary>
    public bool SaveManifoldData { get; set; }
}
