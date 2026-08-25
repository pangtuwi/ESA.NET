namespace App.Core.Simulation;

/// <summary>
/// One right-hand side of the ODE system: <c>dy[i]/dx</c> evaluated at
/// <paramref name="x"/> for the state vector <paramref name="y"/>.
/// </summary>
/// <remarks>
/// <para>
/// Port of Delphi <c>dxdyFunction</c> (RKf5.pas). The Delphi declaration passes
/// <c>y : yarray</c> <b>by value</b>, so a derivative cannot alter the caller's
/// vector. <see cref="ReadOnlySpan{T}"/> carries that guarantee across without
/// copying the array on every call.
/// </para>
/// <para>
/// Evaluating a derivative is <b>not</b> side-effect free. The engine's ODEs begin by
/// calling <c>Cyl.UpdateB</c> or <c>Cyl.UpdateUB</c>, which mutate the gas so that the
/// thermodynamic partials can be read back off it. The integrator calls these in a
/// fixed order and that order is part of the answer.
/// </para>
/// </remarks>
public delegate double DerivativeFunction(double x, ReadOnlySpan<double> y);
