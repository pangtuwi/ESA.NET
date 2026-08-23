namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TRKF</c> (RKf5.pas). The four
/// <c>dxdyFunction</c> pointers are behaviour and arrive in phase 4; only the
/// integrator state is modelled here.
/// </summary>
public sealed class IntegratorState
{
    /// <summary>Number of active equations, <c>NEqns</c>.</summary>
    public int EquationCount { get; set; }

    public Integrator Integrator { get; set; }

    /// <summary>Independent variable, <c>x</c>.</summary>
    public double X { get; set; }

    /// <summary>Step size, <c>dx</c>.</summary>
    public double Dx { get; set; }

    /// <summary>Dependent variables, Delphi <c>yarray = array[1..MaxN] of Double</c>.</summary>
    public double[] Y { get; } = new double[EsaLimits.MaxEquations];
}
