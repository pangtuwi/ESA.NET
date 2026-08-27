using App.Core.Expressions;
using App.Core.Interpolation;
using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Manifold;

/// <summary>
/// The one-dimensional manifold gas dynamics, driving both pipes forward one crank-angle
/// step at a time. Port of <c>TManifolds.Main_Prog</c> (Manifolds.pas:2654-3170) and
/// <c>MassFlow</c> (lines 414-440).
/// </summary>
/// <remarks>
/// <para>
/// Each step solves both pipes: an open end, the interior points by the method of
/// characteristics, and a valve end that is either a solid wall or one of the four flow
/// routines. Which pair runs depends on the two valve areas, giving the four combinations
/// the original spells out longhand.
/// </para>
/// <para>
/// This is the real implementation of <see cref="IManifoldSource"/>, replacing the
/// recorded fixture stage 4a was validated against.
/// </para>
/// </remarks>
public sealed class ManifoldSolver : IManifoldSource
{
    private const double MassFlowGamma = 1.3994;

    private readonly Engine _engine;
    private readonly PipeGeometry _inletPipe;
    private readonly PipeGeometry _exhaustPipe;
    private readonly ValveMotion _inletValve;
    private readonly ValveMotion _exhaustValve;

    private readonly PipeGrid _inlet;
    private readonly PipeGrid _inletNext;
    private readonly PipeGrid _exhaust;
    private readonly PipeGrid _exhaustNext;

    private readonly double _plenumPressure;
    private double _plenumTemperature;
    private readonly double _backPressure;
    private readonly double _backTemperature;

    private readonly (double Forward, double ForwardReverse, double Reverse) _inletTuning;
    private readonly (double Forward, double ForwardReverse, double Reverse) _exhaustTuning;

    private InletValveReverseBoundary.ThroatState _inletThroat;
    private InletValveReverseBoundary.ThroatState _exhaustThroat;

    private bool _started;

    public ManifoldSolver(Engine engine, IExpressionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        var expressions = evaluator ?? new CachingExpressionEvaluator();
        var manifold = engine.Manifold;
        var rpm = engine.Rpm;

        _inletPipe = new PipeGeometry(manifold.InletPipe.AreaVersusLength);
        _exhaustPipe = new PipeGeometry(manifold.ExhaustPipe.AreaVersusLength);
        _inletValve = ValveMotion.Inlet(manifold.InletValve);
        _exhaustValve = ValveMotion.Exhaust(manifold.ExhaustValve);

        var grids = new GridSizeCalculator(expressions);
        var inletPoints = grids.InletGridSize(manifold.InletGrid.Expression, _inletPipe.Length, rpm);
        var exhaustPoints = grids.ExhaustGridSize(
            manifold.ExhaustGrid.Expression, _exhaustPipe.Length, rpm);

        _plenumPressure = expressions.Evaluate(manifold.PlenumPressureFunction.Expression, rpm);

        // Gauge kPa plus atmospheric, and the temperature raw: see the notes in
        // CycleSolver.InitialiseExhaust and ISSUES.md B66.
        var back = manifold.ExhaustBack;
        _backPressure = (LegacyInterpolation.AtSpeed(back.Rpm, back.Pressure, rpm) * 1000)
                        + engine.Atmosphere.PGas;
        _backTemperature = LegacyInterpolation.AtSpeed(back.Rpm, back.Temperature, rpm);

        _inletTuning = (
            Forward: InletForwardTuning(manifold, expressions, rpm),
            ForwardReverse: expressions.Evaluate(manifold.InletValveForwardReverse.Expression, rpm),
            Reverse: expressions.Evaluate(manifold.InletValveReverse.Expression, rpm));

        _exhaustTuning = (
            Forward: expressions.Evaluate(manifold.ExhaustValveForward.Expression, rpm),
            ForwardReverse: expressions.Evaluate(manifold.ExhaustValveForwardReverse.Expression, rpm),
            Reverse: expressions.Evaluate(manifold.ExhaustValveReverse.Expression, rpm));

        _inlet = manifold.Inlet;
        _exhaust = manifold.Exhaust;
        _inletNext = new PipeGrid(EsaLimits.InletGridPoints);
        _exhaustNext = new PipeGrid(EsaLimits.ExhaustGridPoints);

        _inletPoints = inletPoints;
        _exhaustPoints = exhaustPoints;
    }

    private readonly int _inletPoints;
    private readonly int _exhaustPoints;

    /// <summary>
    /// Delphi <c>tStep</c>: how many times inlet valve closing has come round. The
    /// manifold output files are gated on it.
    /// </summary>
    public int TimeStep { get; private set; }

    /// <summary>Gas velocity along the inlet pipe, for the field files and charts.</summary>
    public PipeGrid InletGrid => _inlet;

    /// <summary>Gas velocity along the exhaust pipe.</summary>
    public PipeGrid ExhaustGrid => _exhaust;

    /// <summary>
    /// The <c>IVF</c> constant, which the original overrides with a hard-coded straight
    /// line at or below 1000 rpm rather than evaluating the user's expression. See
    /// ISSUES.md B6.
    /// </summary>
    private static double InletForwardTuning(
        Model.Manifolds manifold, IExpressionEvaluator expressions, double rpm) =>
        rpm <= 1000
            ? (-3.66666E-05 * rpm) + 7.250E-01
            : expressions.Evaluate(manifold.InletValveForward.Expression, rpm);

    private void Initialise(int inletPoints, int exhaustPoints)
    {
        _plenumTemperature = _engine.Manifold.PlenumTemperature;

        foreach (var (grid, points, length, pressure, temperature, gamma) in
                 new[]
                 {
                     (_inlet, inletPoints, _inletPipe.Length, _plenumPressure, _plenumTemperature,
                         CharacteristicSolver.InletGamma),
                     (_inletNext, inletPoints, _inletPipe.Length, _plenumPressure, _plenumTemperature,
                         CharacteristicSolver.InletGamma),
                     (_exhaust, exhaustPoints, _exhaustPipe.Length, _backPressure, _backTemperature,
                         CharacteristicSolver.ExhaustGamma),
                     (_exhaustNext, exhaustPoints, _exhaustPipe.Length, _backPressure, _backTemperature,
                         CharacteristicSolver.ExhaustGamma),
                 })
        {
            PipeGridInitialiser.Initialise(grid, points, length, pressure, temperature, gamma);
        }

        // The throat quantities start at zero so that the first cylinder-pressure
        // correction is zero, with only the throat pressures seeded from the reservoirs.
        _inletThroat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, _plenumPressure, 0);
        _exhaustThroat = new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, _backPressure, 0);

        TimeStep = 0;
    }

    /// <inheritdoc />
    public ManifoldStep Step(in ManifoldRequest request)
    {
        var dt = 1 / (_engine.Rpm / 60 * 360) * _engine.CrankAngleStep;
        var crankAngle = request.CrankAngle;

        var inletOpen = request.InletValveArea > 0;
        var exhaustOpen = request.ExhaustValveArea > 0;

        // The first call only lays out the grid, exactly as tStep = 0 does in the
        // original: no characteristics are advanced and nothing crosses a valve.
        if (!_started)
        {
            // Everything the original does at tStep = 0, deferred to here rather than to
            // construction because the plenum temperature is not known until InitVars has
            // run - and InitVars is what sets it.
            _started = true;
            Initialise(_inletPoints, _exhaustPoints);
            return Report(0, 0, 0);
        }

        SolveInletPipe(request, dt, crankAngle, inletOpen);
        SolveExhaustPipe(request, dt, crankAngle, exhaustOpen);

        var (massIn, massOut, pressureCorrection) = inletOpen || exhaustOpen
            ? MassFlow(request, dt)
            : (0, 0, 0);

        // Inlet valve closing brings the cycle counter round.
        var inletClose = -180 + _engine.Manifold.InletValve.CloseAngle + 360;
        var exhaustOpenAngle = 180 - _engine.Manifold.ExhaustValve.OpenAngle + 360;

        if (crankAngle == inletClose)
        {
            TimeStep++;
        }

        // Through the closed period nothing crosses either valve, whatever the routines
        // above may have computed.
        if (crankAngle >= inletClose && crankAngle <= exhaustOpenAngle)
        {
            massIn = 0;
            massOut = 0;
            pressureCorrection = 0;
        }

        return Report(massIn, massOut, pressureCorrection);
    }

    private ManifoldStep Report(double massIn, double massOut, double pressureCorrection)
    {
        var inletEnd = _inlet.ActiveCount - 1;

        return new ManifoldStep(
            MassIn: massIn,
            MassOut: massOut,
            PressureCorrection: pressureCorrection,
            InletPressure: _inlet.Pressure[inletEnd],
            ExhaustPressure: _exhaust.Pressure[0],
            InletVelocity: _inlet.Velocity[inletEnd],
            ExhaustVelocity: _exhaust.Velocity[0],

            // The pipe temperature arrays are written once at initialisation and never
            // again, so this is permanently the starting plenum temperature. See
            // ISSUES.md B65.
            InletTemperature: _inlet.Temperature[inletEnd]);
    }

    private void SolveInletPipe(in ManifoldRequest request, double dt, double crankAngle, bool open)
    {
        OpenEndBoundary.ApplyInlet(
            _inlet, _inletNext, _inletPipe, dt, _plenumPressure, _plenumTemperature);

        for (var i = 1; i <= _inlet.ActiveCount - 2; i++)
        {
            CharacteristicSolver.UpdateInteriorPoint(
                _inlet, _inletNext, _inletPipe, CharacteristicSolver.InletGamma, dt, i);
        }

        if (open)
        {
            _inletThroat = InletValveOpenBoundary.Apply(
                _inlet, _inletNext, _inletPipe, _inletValve, dt,
                request.CylinderPressure, request.CylinderTemperature, crankAngle,
                _inletPipe.Area(_inletPipe.Length), request.InletValveArea,
                _inletThroat, _inletTuning);
        }
        else
        {
            ClosedValveBoundary.ApplyInlet(_inlet, _inletNext, _inletPipe, dt);

            // A shut valve passes nothing, so the throat quantities MassFlow multiplies
            // are left at whatever the last open step produced; the areas below are zero,
            // which is what actually stops the flow.
        }

        Advance(_inlet, _inletNext);
    }

    private void SolveExhaustPipe(in ManifoldRequest request, double dt, double crankAngle, bool open)
    {
        if (open)
        {
            _exhaustThroat = ExhaustValveOpenBoundary.Apply(
                _exhaust, _exhaustNext, _exhaustPipe, _exhaustValve, dt,
                request.CylinderPressure, request.CylinderTemperature, crankAngle,
                _exhaustPipe.Area(0), request.ExhaustValveArea,
                _exhaustThroat, _exhaustTuning);
        }
        else
        {
            ClosedValveBoundary.ApplyExhaust(_exhaust, _exhaustNext, _exhaustPipe, dt);
        }

        for (var i = 1; i <= _exhaust.ActiveCount - 2; i++)
        {
            CharacteristicSolver.UpdateInteriorPoint(
                _exhaust, _exhaustNext, _exhaustPipe, CharacteristicSolver.ExhaustGamma, dt, i);
        }

        OpenEndBoundary.ApplyExhaust(
            _exhaust, _exhaustNext, _exhaustPipe, dt, _backPressure, _backTemperature);

        Advance(_exhaust, _exhaustNext);
    }

    private static void Advance(PipeGrid current, PipeGrid next)
    {
        for (var i = 0; i < current.ActiveCount; i++)
        {
            current.Velocity[i] = next.Velocity[i];
            current.Pressure[i] = next.Pressure[i];
            current.Density[i] = next.Density[i];
            current.SpeedOfSound[i] = next.SpeedOfSound[i];
        }
    }

    /// <summary>
    /// Mass across each valve over the step, and the cylinder pressure correction that
    /// goes with it. Port of <c>MassFlow</c>.
    /// </summary>
    /// <remarks>
    /// Both stagnation branches in the original evaluate the same expression, because the
    /// alternatives the tests were meant to select are commented out between them
    /// (ISSUES.md B43), so only one form survives here. Note also that this runs at the
    /// inlet gamma even for the exhaust side, because <c>Main_Prog</c> passes its own
    /// 1.3994 rather than either pipe's value.
    /// </remarks>
    private (double MassIn, double MassOut, double PressureCorrection) MassFlow(
        in ManifoldRequest request, double dt)
    {
        const double gamma = MassFlowGamma;

        var massIn = _inletThroat.Velocity * _inletThroat.Density
                     * _inletThroat.DischargeCoefficient * request.InletValveArea * dt;

        var massOut = _exhaustThroat.Velocity * _exhaustThroat.Density
                      * _exhaustThroat.DischargeCoefficient * request.ExhaustValveArea * dt;

        var inletStagnation = Math.Sqrt(
            (_inletThroat.SpeedOfSound * _inletThroat.SpeedOfSound)
            + ((gamma - 1) / 2 * _inletThroat.Velocity * _inletThroat.Velocity));

        var exhaustStagnation = Math.Sqrt(
            (_exhaustThroat.SpeedOfSound * _exhaustThroat.SpeedOfSound)
            + ((gamma - 1) / 2 * _exhaustThroat.Velocity * _exhaustThroat.Velocity));

        var correction =
            ((inletStagnation * inletStagnation * massIn)
             - (exhaustStagnation * exhaustStagnation * massOut))
            / request.CylinderVolume;

        return (massIn, massOut, correction);
    }
}
