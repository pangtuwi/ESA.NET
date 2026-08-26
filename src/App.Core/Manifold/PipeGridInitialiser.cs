using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// Lays out a pipe's grid and fills it with uniform stagnant gas. Port of the
/// <c>CalcX</c>, <c>CalcVel</c>, <c>CalcPress</c>, <c>CalcDens</c> and <c>CalcSOS</c>
/// procedures (Manifolds.pas:347-410), which the original calls once at <c>tStep = 0</c>.
/// </summary>
/// <remarks>
/// The five are separate procedures in the original, each looping over both pipes. They
/// are one call per pipe here because nothing else calls them and the split served no
/// purpose beyond grouping by quantity.
/// </remarks>
public static class PipeGridInitialiser
{
    /// <summary>
    /// Sets up <paramref name="grid"/> with <paramref name="pointCount"/> evenly spaced
    /// points over the pipe, all at rest at the given pressure and temperature.
    /// </summary>
    /// <param name="grid">The grid to fill. Its active count is set here.</param>
    /// <param name="pointCount">Delphi <c>QI</c> or <c>QE</c>, from the grid-size expression.</param>
    /// <param name="pipeLength">Pipe length in metres.</param>
    /// <param name="pressure">Uniform initial pressure in pascals.</param>
    /// <param name="temperature">Uniform initial temperature in kelvin.</param>
    /// <param name="gamma">Ratio of specific heats used for the speed of sound.</param>
    public static void Initialise(
        PipeGrid grid,
        int pointCount,
        double pipeLength,
        double pressure,
        double temperature,
        double gamma)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (pointCount > grid.Capacity)
        {
            throw new CfdException(
                $"Calculated grid length of {pointCount} but was greater than maximum of {grid.Capacity}");
        }

        grid.ActiveCount = pointCount;

        // The spacing divides the pipe into QI-1 intervals, so the last point sits
        // exactly on the far end.
        var spacing = pipeLength / (pointCount - 1);

        for (var i = 0; i < pointCount; i++)
        {
            grid.X[i] = i == 0 ? 0 : grid.X[i - 1] + spacing;
            grid.Velocity[i] = 0;
            grid.Pressure[i] = pressure;
            grid.Temperature[i] = temperature;

            // Both use the universal 287 rather than the mixture's own gas constant,
            // throughout the manifold solver.
            grid.Density[i] = grid.Pressure[i] / 287 / grid.Temperature[i];
            grid.SpeedOfSound[i] = Math.Sqrt(gamma * 287 * grid.Temperature[i]);
        }
    }
}
