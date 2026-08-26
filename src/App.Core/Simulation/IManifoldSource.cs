namespace App.Core.Simulation;

/// <summary>
/// Supplies the cylinder's boundary conditions at each crank angle: what crossed the
/// valves and what the gas was doing on the far side of them.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam between the in-cylinder model and the manifold gas dynamics. In the
/// original there is no seam - <c>TEngine2z.Run</c> calls
/// <c>Manifold.Main_Prog(...)</c> directly with eighteen arguments, seven of them
/// <c>var</c> parameters carrying results back out. Splitting it here lets the cylinder
/// model be built and checked against the reference run before a line of the
/// one-dimensional wave solver exists, which is the whole point of staging phase 4.
/// </para>
/// <para>
/// Phase 4b replaces the recorded implementation with the real solver. Nothing else has
/// to change: <c>CycleSolver</c> only ever sees this interface.
/// </para>
/// </remarks>
public interface IManifoldSource
{
    /// <summary>
    /// Advances the manifolds by one crank-angle step and reports what crossed the
    /// valves. Corresponds to one call of <c>TManifolds.Main_Prog</c>.
    /// </summary>
    /// <param name="request">The cylinder-side conditions the manifolds need.</param>
    ManifoldStep Step(in ManifoldRequest request);
}

/// <summary>
/// What the manifolds are told about the cylinder. The subset of
/// <c>Main_Prog</c>'s input parameters that vary from step to step.
/// </summary>
/// <param name="CrankAngle">Crank angle in degrees, as <c>Run</c> passes it: <c>x*180/pi + 360</c>.</param>
/// <param name="CylinderPressure">Delphi <c>Pcyl</c>.</param>
/// <param name="CylinderTemperature">Delphi <c>Tcyl</c>, the mass-weighted mean of the two zones.</param>
/// <param name="CylinderVolume">Delphi <c>CylVol</c>.</param>
/// <param name="CylinderMass">Delphi <c>MassCyl</c>.</param>
/// <param name="AtmosphericPressure">Delphi <c>Patm</c>.</param>
/// <param name="AtmosphericTemperature">Delphi <c>Tatm</c>.</param>
/// <param name="InletValveArea">Delphi <c>IValveArea</c>.</param>
/// <param name="ExhaustValveArea">Delphi <c>EValveArea</c>.</param>
public readonly record struct ManifoldRequest(
    double CrankAngle,
    double CylinderPressure,
    double CylinderTemperature,
    double CylinderVolume,
    double CylinderMass,
    double AtmosphericPressure,
    double AtmosphericTemperature,
    double InletValveArea,
    double ExhaustValveArea);

/// <summary>
/// What the manifolds report back. The seven <c>var</c> parameters of
/// <c>Main_Prog</c> that carry results out.
/// </summary>
/// <param name="MassIn">
/// Mass through the inlet valve over the step, Delphi <c>MassIn</c>. Positive into the
/// cylinder.
/// </param>
/// <param name="MassOut">
/// Mass through the exhaust valve over the step, Delphi <c>MassOut</c>. Positive out of
/// the cylinder; negative is reversion.
/// </param>
/// <param name="PressureCorrection">
/// Delphi <c>dPMass</c>: the cylinder pressure correction for the mass that crossed the
/// valves, <c>(cStag^2*MassIn - cCyl^2*MassOut)/CylVol</c>. Applied during overlap and
/// throughout the single-zone model.
/// </param>
/// <param name="InletPressure">Pressure at the inlet valve, Delphi <c>InletP</c>.</param>
/// <param name="ExhaustPressure">Pressure at the exhaust valve, Delphi <c>ExhaustP</c>.</param>
/// <param name="InletVelocity">Gas velocity at the inlet valve, Delphi <c>InletU</c>.</param>
/// <param name="ExhaustVelocity">Gas velocity at the exhaust valve, Delphi <c>ExhaustU</c>.</param>
/// <param name="InletTemperature">
/// Gas temperature at the inlet plenum, Delphi <c>Manifold.InletT</c>. Not one of
/// <c>Main_Prog</c>'s <c>var</c> parameters - the original leaves it on the manifold
/// object and <c>Run</c> reads it straight back off - but it is an output of the same
/// call, so it travels with the rest.
/// </param>
public readonly record struct ManifoldStep(
    double MassIn,
    double MassOut,
    double PressureCorrection,
    double InletPressure,
    double ExhaustPressure,
    double InletVelocity,
    double ExhaustVelocity,
    double InletTemperature);
