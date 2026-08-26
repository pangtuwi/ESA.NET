using App.Core.Simulation;

namespace App.Tests;

/// <summary>
/// Replays the manifold boundary conditions the baseline run recorded, so the
/// in-cylinder model can be driven end to end before the wave solver exists.
/// </summary>
/// <remarks>
/// <para>
/// The trace records six of the seven values <c>Main_Prog</c> returns: <c>Min</c>,
/// <c>Mout</c>, <c>IV P</c>, <c>EV P</c>, <c>IV V</c> and <c>EV V</c>. It does not record
/// the seventh.
/// </para>
/// <para>
/// <b>The pressure correction cannot be recovered.</b> <c>dPMass</c> is
/// <c>(cStag^2*MassIn - cCyl^2*MassOut)/CylVol</c>, built from the local speeds of sound
/// and densities at the two valve throats, and none of those appear in any output the
/// original writes. This fixture therefore reports zero for it. The consequence is
/// specific and bounded: the correction is applied only during overlap and throughout
/// the single-zone model, so anything driven by this source is trustworthy across the
/// closed period - compression, combustion and expansion - and is not a like-for-like
/// comparison during gas exchange. Phase 4b removes the limitation by computing it.
/// </para>
/// <para>
/// Inlet temperature is likewise unrecorded, so the initialised plenum temperature
/// stands in for it.
/// </para>
/// </remarks>
internal sealed class RecordedManifoldSource : IManifoldSource
{
    private readonly Dictionary<int, ManifoldStep> _byCrankAngle = [];
    private readonly double _inletTemperature;

    public RecordedManifoldSource(double inletTemperature)
    {
        _inletTemperature = inletTemperature;

        double[] Column(string name) => BaselinePaths.TraceColumn(name).Select(p => p.Value).ToArray();

        var crankAngles = BaselinePaths.TraceColumn("Min").Select(p => p.CrankAngle).ToArray();
        var massIn = Column("Min");
        var massOut = Column("Mout");
        var inletPressure = Column("IV P");
        var exhaustPressure = Column("EV P");
        var inletVelocity = Column("IV V");
        var exhaustVelocity = Column("EV V");

        for (var i = 0; i < crankAngles.Length; i++)
        {
            // Masses are written in milligrams, k = 1e6 in CAList2z.pas.
            _byCrankAngle[(int)Math.Round(crankAngles[i])] = new ManifoldStep(
                MassIn: massIn[i] / 1E6,
                MassOut: massOut[i] / 1E6,
                PressureCorrection: 0,
                InletPressure: inletPressure[i],
                ExhaustPressure: exhaustPressure[i],
                InletVelocity: inletVelocity[i],
                ExhaustVelocity: exhaustVelocity[i],
                InletTemperature: inletTemperature);
        }
    }

    /// <summary>Crank angles the fixture was asked for that the trace does not cover.</summary>
    public List<double> Misses { get; } = [];

    public ManifoldStep Step(in ManifoldRequest request)
    {
        // Run passes x*180/pi + 360, so the trace's -359..360 arrives as 1..720.
        var degrees = request.CrankAngle - 360;

        if (degrees > 360)
        {
            degrees -= 720;
        }

        var key = (int)Math.Round(degrees);

        if (_byCrankAngle.TryGetValue(key, out var recorded))
        {
            return recorded;
        }

        Misses.Add(degrees);

        return new ManifoldStep(0, 0, 0, 0, 0, 0, 0, _inletTemperature);
    }
}
