namespace App.Core.Manifold;

/// <summary>
/// Passes on only the rows inside the crank-angle window the original's manifold output
/// covers, and forwards <see cref="Reset"/> so the sink keeps one cycle at a time.
/// </summary>
/// <remarks>
/// <para>
/// The original decides what to write with a <c>tStep</c> test inside <c>Main_Prog</c>
/// (Manifolds.pas:3022, 3051-3053), which is what makes ISSUES.md C1 bite: the test names
/// the last <b>requested</b> cycle, and a run that converges early exits before reaching
/// it. Filtering from outside the solver keeps the same rows without the same gate.
/// </para>
/// </remarks>
/// <param name="inner">The sink that receives the rows inside the window.</param>
/// <param name="inletCloseAngle">
/// The inlet valve's closing angle as a signed crank angle, i.e.
/// <c>-180 + Manifold.InletValve.CloseAngle</c>.
/// </param>
public sealed class ManifoldCaptureWindow(IManifoldRecorder inner, double inletCloseAngle)
    : IManifoldRecorder
{
    private readonly IManifoldRecorder _inner =
        inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// Whether a crank angle falls in the window the original captures, in
    /// <c>Main_Prog</c>'s 1 to 720 convention: one full cycle starting at firing top dead
    /// centre, which is 620 steps rather than 720 because the hundred before it belong to
    /// the previous cycle.
    /// </summary>
    public static bool Contains(double crankAngle, double inletCloseAngle) =>
        (crankAngle > 359 && crankAngle <= 720)
        || (crankAngle > 0 && crankAngle < inletCloseAngle + 360);

    /// <inheritdoc />
    public void Record(in ManifoldRow row)
    {
        if (Contains(row.CrankAngle, inletCloseAngle))
        {
            _inner.Record(in row);
        }
    }

    /// <inheritdoc />
    public void Reset() => _inner.Reset();
}
