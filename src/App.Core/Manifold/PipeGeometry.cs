using App.Core.Interpolation;
using App.Core.Model;

namespace App.Core.Manifold;

/// <summary>
/// Cross-sectional area of one manifold pipe against distance along it. Port of Delphi
/// <c>TPipe</c> (Pipes.pas).
/// </summary>
/// <remarks>
/// The <c>.maf</c> table holds millimetres and square millimetres; everything here is
/// metres and square metres, converted on each lookup exactly as the original does.
/// </remarks>
public sealed class PipeGeometry
{
    private readonly ManifoldAreaTable _table;

    public PipeGeometry(ManifoldAreaTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    /// <summary>
    /// Pipe length in metres, Delphi <c>TPipe.Length</c>: the last position in the table.
    /// </summary>
    public double Length => _table.Position[_table.Count - 1] / 1000;

    /// <summary>Area in square metres at <paramref name="length"/> metres along the pipe.</summary>
    public double Area(double length) => LegacyInterpolation.AreaAt(_table, length * 1000) / 1e6;

    /// <summary>
    /// Rate of change of area with distance, in metres. Port of <c>TPipe.dAdL</c>: a
    /// central difference over plus and minus two millimetres.
    /// </summary>
    /// <remarks>
    /// The middle branch is a deliberate guard against ISSUES.md B4, the lookup that
    /// falls to zero past the end of the table rather than clamping. When the forward
    /// sample lands past the last entry and comes back zero, the original switches to a
    /// backward difference instead of differencing against that zero. So the author knew
    /// about the cliff here, and worked around it, while leaving it in place.
    /// </remarks>
    public double AreaGradient(double length)
    {
        var millimetres = length * 1000;

        double At(double position) => LegacyInterpolation.AreaAt(_table, position);

        if (millimetres - 2 < 0)
        {
            return (At(millimetres + 2) - At(millimetres)) / 1e6 / 0.002;
        }

        if (At(millimetres + 2) == 0)
        {
            return (At(millimetres) - At(millimetres - 2)) / 1e6 / 0.002;
        }

        return (At(millimetres + 2) - At(millimetres - 2)) / 1e6 / 0.004;
    }
}
