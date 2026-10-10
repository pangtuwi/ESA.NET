using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// Where a pipe's grid points go, and which stretch of pipe each belongs to, once its
/// abrupt area steps are junctions (ISSUES.md B83). No Delphi counterpart: the original lays
/// one evenly spaced grid over the whole pipe (<c>CalcX</c>) and meets every step through
/// its area-gradient source term.
/// </summary>
/// <remarks>
/// <para>
/// A step is a table interval shorter than <see cref="StepLength"/> across which the area
/// changes by at least <see cref="StepRatio"/>. The original's <c>dAdL</c> differences the
/// area over plus and minus two millimetres, so a step narrower than that stencil is seen
/// only as a gradient the size of the jump divided by the stencil, which a characteristic
/// foot landing near it turns into a pressure source several times the pressure itself
/// (ISSUES.md A36). Every gradual change in the shipped tables, the trumpets included, is
/// sampled at five millimetres or more, or changes by a fraction of a per cent.
/// </para>
/// <para>
/// Each step becomes a junction at its midpoint, with two grid points there, one face for
/// each side. The pipe between junctions is a segment, gridded evenly at no finer a
/// spacing than the operator's grid would have, so no foot travels further in a step than
/// it did on the original's grid.
/// </para>
/// </remarks>
public sealed class PipeLayout
{
    /// <summary>A table interval shorter than this, in millimetres, can be a step: the <c>dAdL</c> stencil's width.</summary>
    public const double StepLength = 4;

    /// <summary>The smallest ratio of the larger to the smaller area that makes a short interval a step.</summary>
    public const double StepRatio = 1.05;

    private PipeLayout(double[] positions, PipeGeometry[] geometry, int[] junctions)
    {
        Positions = positions;
        Geometry = geometry;
        Junctions = junctions;
    }

    /// <summary>Every grid point's position along the pipe, in metres. A junction's two faces share one.</summary>
    public IReadOnlyList<double> Positions { get; }

    /// <summary>The stretch of pipe each grid point belongs to, for its area and gradient.</summary>
    public IReadOnlyList<PipeGeometry> Geometry { get; }

    /// <summary>
    /// The index of each junction's left face. Its right face is the next point.
    /// </summary>
    public IReadOnlyList<int> Junctions { get; }

    /// <summary>The number of grid points, both faces of every junction included.</summary>
    public int PointCount => Positions.Count;

    /// <summary>
    /// The table intervals that are steps, as their two positions in millimetres. A step
    /// at either end of the pipe is left to the boundary there.
    /// </summary>
    public static IReadOnlyList<(double From, double To)> FindSteps(ManifoldAreaTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var steps = new List<(double, double)>();

        for (var i = 1; i < table.Count - 2; i++)
        {
            var from = table.Position[i];
            var to = table.Position[i + 1];
            var smaller = Math.Min(table.Area[i], table.Area[i + 1]);
            var larger = Math.Max(table.Area[i], table.Area[i + 1]);

            if (to - from < StepLength && smaller > 0 && larger / smaller >= StepRatio)
            {
                steps.Add((from, to));
            }
        }

        return steps;
    }

    /// <summary>
    /// Lays out the grid of a pipe of <paramref name="pointCount"/> points as the operator's
    /// grid-size expression gives them, with a junction at every step.
    /// </summary>
    /// <param name="pipe">The whole pipe.</param>
    /// <param name="table">Its area table, for finding the steps.</param>
    /// <param name="pointCount">Delphi <c>QI</c> or <c>QE</c>.</param>
    public static PipeLayout Build(PipeGeometry pipe, ManifoldAreaTable table, int pointCount)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(table);

        var length = pipe.Length;
        var spacing = length / (pointCount - 1);
        var steps = FindSteps(table);

        var positions = new List<double>();
        var geometry = new List<PipeGeometry>();
        var junctions = new List<int>();

        var start = 0.0;
        var lookupFrom = double.NegativeInfinity;

        for (var s = 0; s <= steps.Count; s++)
        {
            var last = s == steps.Count;
            var end = last ? length : (steps[s].From + steps[s].To) / 2 / 1000;
            var lookupTo = last ? double.PositiveInfinity : steps[s].From;
            var segment = steps.Count == 0 ? pipe : pipe.Segment(start, end, lookupFrom, lookupTo);

            // No finer than the operator's spacing, so no characteristic foot travels
            // further in a step than it would have on the original's grid.
            var intervals = Math.Max(1, (int)Math.Floor(((end - start) / spacing) + 1e-9));

            if (s > 0)
            {
                junctions.Add(positions.Count - 1);
            }

            for (var k = 0; k <= intervals; k++)
            {
                positions.Add(k == intervals ? end : start + ((end - start) * k / intervals));
                geometry.Add(segment);
            }

            if (!last)
            {
                start = end;
                lookupFrom = steps[s].To;
            }
        }

        return new PipeLayout([.. positions], [.. geometry], [.. junctions]);
    }
}
