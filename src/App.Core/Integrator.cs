namespace App.Core;

/// <summary>
/// ODE integrator selection. Values match the <c>Integrator</c> key written to
/// <c>.eng</c> files: 0 is RKF5, 1 is Euler (SPEC.md section 2).
/// </summary>
public enum Integrator
{
    Rkf5 = 0,
    Euler = 1,
}
