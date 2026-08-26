using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// A straight line in velocity, pressure and density through two adjacent grid points,
/// used to resample the state wherever a characteristic foot lands between them.
/// </summary>
/// <remarks>
/// Every boundary routine in Manifolds.pas builds one of these from the boundary point and
/// its neighbour. They do not all build it the same way round: the slope is formed
/// <c>from</c> to <c>to</c> and the intercept anchored on <c>to</c>, and which point plays
/// which role differs between the inlet and exhaust versions of the same routine. That is
/// algebraically the same line either way, so it is carried through here only because
/// floating point makes it a different one.
/// </remarks>
internal readonly record struct GridInterpolants(
    double VelocitySlope, double VelocityIntercept,
    double PressureSlope, double PressureIntercept,
    double DensitySlope, double DensityIntercept)
{
    /// <summary>
    /// Builds the line through <paramref name="from"/> and <paramref name="to"/>.
    /// </summary>
    /// <param name="velocityOverrideAt">
    /// A grid index whose stored velocity should be replaced by
    /// <paramref name="velocityOverride"/>, or -1 for none. Only
    /// <c>EXHAUST_VALVE_CLOSED</c> uses this, substituting the imposed wall velocity for
    /// the stored one. See ISSUES.md B54.
    /// </param>
    public static GridInterpolants Through(
        PipeGrid grid, int from, int to, int velocityOverrideAt = -1, double velocityOverride = 0)
    {
        double Velocity(int index) =>
            index == velocityOverrideAt ? velocityOverride : grid.Velocity[index];

        var dx = grid.X[from] - grid.X[to];

        var velocitySlope = (Velocity(from) - Velocity(to)) / dx;
        var pressureSlope = (grid.Pressure[from] - grid.Pressure[to]) / dx;
        var densitySlope = (grid.Density[from] - grid.Density[to]) / dx;

        return new GridInterpolants(
            velocitySlope, Velocity(to) - (velocitySlope * grid.X[to]),
            pressureSlope, grid.Pressure[to] - (pressureSlope * grid.X[to]),
            densitySlope, grid.Density[to] - (densitySlope * grid.X[to]));
    }

    public double VelocityAt(double x) => (VelocitySlope * x) + VelocityIntercept;

    public double PressureAt(double x) => (PressureSlope * x) + PressureIntercept;

    public double DensityAt(double x) => (DensitySlope * x) + DensityIntercept;
}
