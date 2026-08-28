namespace App.Core.Manifold;

/// <summary>
/// Receives one row of manifold state per crank-angle step, for the nine output files the
/// original writes when <c>Save Manifold Data</c> is set.
/// </summary>
/// <remarks>
/// The writing itself is file IO and so belongs in the persistence layer; this is the seam
/// the solver calls through. In the original the writes are inline in <c>Main_Prog</c>.
/// </remarks>
public interface IManifoldRecorder
{
    /// <summary>Records the state at the end of one step.</summary>
    void Record(in ManifoldRow row);

    /// <summary>
    /// Discards everything recorded so far, because a new cycle is starting.
    /// </summary>
    /// <remarks>
    /// The original writes the files from the last <b>requested</b> cycle, which a run
    /// that converges early never reaches - ISSUES.md C1. Resetting at each cycle
    /// boundary instead leaves the recorder holding the last cycle actually run,
    /// whichever that turns out to be, in one pass.
    /// </remarks>
    void Reset();
}

/// <summary>
/// One crank angle's worth of everything the nine files report.
/// </summary>
/// <param name="CrankAngle">Crank angle in <c>Main_Prog</c>'s 1 to 720 convention.</param>
/// <param name="CylinderPressure">Pascals.</param>
/// <param name="CylinderTemperature">Kelvin.</param>
/// <param name="CylinderVolume">Cubic metres.</param>
/// <param name="MassIn">Kilograms through the inlet valve this step.</param>
/// <param name="MassOut">Kilograms through the exhaust valve this step.</param>
/// <param name="InletPressure">Pressure at every inlet grid point, pascals.</param>
/// <param name="InletVelocity">Velocity at every inlet grid point, metres per second.</param>
/// <param name="ExhaustPressure">Pressure at every exhaust grid point, pascals.</param>
/// <param name="ExhaustVelocity">Velocity at every exhaust grid point.</param>
public readonly record struct ManifoldRow(
    double CrankAngle,
    double CylinderPressure,
    double CylinderTemperature,
    double CylinderVolume,
    double MassIn,
    double MassOut,
    ReadOnlyMemory<double> InletPressure,
    ReadOnlyMemory<double> InletVelocity,
    ReadOnlyMemory<double> ExhaustPressure,
    ReadOnlyMemory<double> ExhaustVelocity);
