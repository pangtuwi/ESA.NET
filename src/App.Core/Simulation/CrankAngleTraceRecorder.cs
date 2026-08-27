using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// Captures the twenty-eight recorded quantities at each crank angle as a run proceeds.
/// Port of <c>TCAList.UpdateCApoint</c> (CAList2z.pas:94-136).
/// </summary>
/// <remarks>
/// The trace is overwritten every cycle, so what it holds at the end of a run is the last
/// cycle - which is the one the charts and the PVT export show.
/// </remarks>
public sealed class CrankAngleTraceRecorder
{
    private readonly ValveMotion _inletValve;
    private readonly ValveMotion _exhaustValve;

    public CrankAngleTraceRecorder(ValveMotion inletValve, ValveMotion exhaustValve)
    {
        _inletValve = inletValve;
        _exhaustValve = exhaustValve;
    }

    /// <summary>The captured cycle.</summary>
    public CrankAngleTrace Trace { get; } = new();

    /// <summary>Records the state at the end of one step.</summary>
    public void Record(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var crankAngle = (int)Math.Round(engine.CrankAngle);

        // The original shows a message and gives up rather than writing out of range.
        if (crankAngle < EsaLimits.FirstCrankAngle || crankAngle > EsaLimits.LastCrankAngle)
        {
            return;
        }

        var point = Trace[crankAngle];
        var cylinder = engine.Cylinder;

        point[1] = cylinder.VGas;
        point[2] = cylinder.PGas;
        point[3] = cylinder.MGas;
        point[4] = cylinder.Mb;
        point[5] = cylinder.Mu;
        point[6] = engine.MassIn;
        point[7] = engine.MassOut;
        point[8] = cylinder.Vb;
        point[9] = cylinder.Vu;
        point[10] = cylinder.Tb;
        point[11] = cylinder.Tu;
        point[12] = engine.Qb;
        point[13] = engine.Qu;
        point[14] = cylinder.Gamma;
        point[15] = cylinder.Fuel.M;
        point[16] = _inletValve.FlowArea(engine.CrankAngle);
        point[17] = _exhaustValve.FlowArea(engine.CrankAngle);
        point[18] = engine.InletVelocity;
        point[19] = engine.ExhaustVelocity;
        point[20] = engine.InletPressure;
        point[21] = engine.ExhaustPressure;
        point[22] = engine.Work;
        point[23] = engine.PumpingWork;
        point[24] = engine.Emissions[Species.CO];
        point[25] = engine.Emissions[Species.NO];
        point[26] = engine.Emissions[Species.CO2];

        // Unburnt hydrocarbons are recorded as a literal zero: the column exists in the
        // grid and the export, and nothing ever computes a value for it. See ISSUES.md B71.
        point[27] = 0;

        point[28] = engine.HeatLoss;
    }
}
