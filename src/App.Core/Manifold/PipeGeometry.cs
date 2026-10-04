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
    private readonly bool _clampPastEnd;

    /// <param name="table">The pipe's <c>.maf</c> area table.</param>
    /// <param name="clampPastEnd">
    /// ISSUES.md B4: hold the last area past the end of the table rather than falling to
    /// zero. False is the original's lookup.
    /// </param>
    public PipeGeometry(ManifoldAreaTable table, bool clampPastEnd = false)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
        _clampPastEnd = clampPastEnd;
    }

    /// <summary>
    /// Pipe length in metres, Delphi <c>TPipe.Length</c>: the last position in the table.
    /// </summary>
    public double Length => _table.Position[_table.Count - 1] / 1000;

    /// <summary>Area in square metres at <paramref name="length"/> metres along the pipe.</summary>
    public double Area(double length) =>
        LegacyInterpolation.AreaAt(_table, length * 1000, _clampPastEnd) / 1e6;

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

        double At(double position) => LegacyInterpolation.AreaAt(_table, position, _clampPastEnd);

        if (millimetres - 2 < 0)
        {
            return (At(millimetres + 2) - At(millimetres)) / 1e6 / 0.002;
        }

        // The original finds the end of the pipe by the lookup falling to zero. With the
        // clamp there is no zero to find, so the same end is found by position, which
        // gives the same backward difference wherever the zero was the cliff's.
        var pastTheEnd = _clampPastEnd
            ? millimetres + 2 > _table.Position[_table.Count - 1]
            : At(millimetres + 2) == 0;

        if (pastTheEnd)
        {
            return (At(millimetres) - At(millimetres - 2)) / 1e6 / 0.002;
        }

        return (At(millimetres + 2) - At(millimetres - 2)) / 1e6 / 0.004;
    }
}
