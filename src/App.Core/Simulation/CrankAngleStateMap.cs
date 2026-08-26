using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// Decides which of the six crank-angle states the engine is in. Port of
/// <c>TEngine2z.GetState</c> (ICEngine2Z.pas:118-134).
/// </summary>
/// <remarks>
/// <para>
/// The boundaries are the two valve events, the spark and the end of the burn, tested in
/// a fixed order so that the cycle reads Overlap, Intake, Compression, Combustion,
/// Expansion, Exhaust and back to Overlap across a crank angle running from -359 to 360.
/// Overlap appears at both ends because it straddles top dead centre on the exhaust
/// stroke.
/// </para>
/// <para>
/// <b>Valve angles are the converted ones.</b> The <c>.eng</c> file stores the timings as
/// the operator enters them, in degrees before or after a dead centre; Delphi converts
/// on the way out of the edit form (<c>Edit.pas:448-451</c>) to the signed crank angles
/// used here. <see cref="FromEngine"/> applies that conversion, so callers pass a loaded
/// engine rather than doing the arithmetic themselves.
/// </para>
/// <para>
/// Nothing here bounds the answer to one cycle: a crank angle past
/// <see cref="InletOpen"/> is Overlap however far past it goes.
/// </para>
/// </remarks>
public sealed class CrankAngleStateMap
{
    /// <param name="inletOpen">Delphi <c>IV.O</c>, already converted: <c>360 - IVO</c>.</param>
    /// <param name="inletClose">Delphi <c>IV.C</c>, already converted: <c>-180 + IVC</c>.</param>
    /// <param name="exhaustOpen">Delphi <c>EV.O</c>, already converted: <c>180 - EVO</c>.</param>
    /// <param name="exhaustClose">Delphi <c>EV.C</c>, already converted: <c>-360 + EVC</c>.</param>
    /// <param name="sparkAngle">Delphi <c>Cyl.ThetaSpark</c>, negative before top dead centre.</param>
    /// <param name="burnAngle">Delphi <c>Cyl.Fuel.BurnAngle</c>, in degrees.</param>
    public CrankAngleStateMap(
        double inletOpen,
        double inletClose,
        double exhaustOpen,
        double exhaustClose,
        double sparkAngle,
        double burnAngle)
    {
        InletOpen = inletOpen;
        InletClose = inletClose;
        ExhaustOpen = exhaustOpen;
        ExhaustClose = exhaustClose;
        SparkAngle = sparkAngle;
        BurnAngle = burnAngle;
    }

    public double InletOpen { get; }

    public double InletClose { get; }

    public double ExhaustOpen { get; }

    public double ExhaustClose { get; }

    /// <summary>
    /// Delphi <c>Cyl.ThetaSpark</c>. <c>InitVars</c> sets it to the <b>negated</b> spark
    /// advance from the <c>.spk</c> map, so an advance of 21 degrees is -21 here.
    /// </summary>
    public double SparkAngle { get; }

    public double BurnAngle { get; }

    /// <summary>
    /// Builds the map from a loaded engine, converting the valve timings the way the edit
    /// form does and negating the spark advance the way <c>InitVars</c> does.
    /// </summary>
    /// <param name="engine">The loaded engine.</param>
    /// <param name="sparkAdvance">
    /// Spark advance in degrees before top dead centre, looked up from the <c>.spk</c> map
    /// at the running speed. Passed in rather than read here because the lookup belongs to
    /// the solver, which knows the speed.
    /// </param>
    public static CrankAngleStateMap FromEngine(Engine engine, double sparkAdvance)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var inlet = engine.Manifold.InletValve;
        var exhaust = engine.Manifold.ExhaustValve;

        return new CrankAngleStateMap(
            inletOpen: 360 - inlet.OpenAngle,
            inletClose: -180 + inlet.CloseAngle,
            exhaustOpen: 180 - exhaust.OpenAngle,
            exhaustClose: -360 + exhaust.CloseAngle,
            sparkAngle: -sparkAdvance,
            burnAngle: engine.Cylinder.Fuel.BurnAngle);
    }

    /// <summary>The state at a crank angle in <b>degrees</b>.</summary>
    public EngineState StateAt(double crankAngleDegrees)
    {
        if (crankAngleDegrees < ExhaustClose)
        {
            return EngineState.Overlap;
        }

        if (crankAngleDegrees < InletClose)
        {
            return EngineState.Intake;
        }

        if (crankAngleDegrees < SparkAngle)
        {
            return EngineState.Compression;
        }

        if (crankAngleDegrees < SparkAngle + BurnAngle)
        {
            return EngineState.Combustion;
        }

        if (crankAngleDegrees < ExhaustOpen)
        {
            return EngineState.Expansion;
        }

        if (crankAngleDegrees < InletOpen)
        {
            return EngineState.Exhaust;
        }

        return EngineState.Overlap;
    }
}
