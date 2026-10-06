using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B77: what the gas holds between steps. The integrator's solution belongs to
/// the angle a step finishes at; the original updates the gas at the angle it started at.
/// </summary>
public sealed class StepStateTests
{
    private static Engine BaselineEngine()
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;

        return engine;
    }

    private static CycleSolver Solver(Engine engine, bool endOfStep, params Correction[] alsoOn)
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        physics.Overrides[CorrectionCatalogue.EndOfStepState.Entry] = endOfStep;

        // B18 keeps the unburnt model free of call history, so the test's own property
        // calls cannot nudge the run.
        physics.Overrides[CorrectionCatalogue.ResidualMolecularWeight.Entry] = true;

        foreach (var correction in alsoOn)
        {
            physics.Overrides[correction.Entry] = true;
        }

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        return solver;
    }

    private static void RunCycles(CycleSolver solver, int cycles, Action<int>? onCycle = null)
    {
        solver.Engine.ZoneCount = 1;

        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            if (cycle > 1)
            {
                solver.Engine.ZoneCount = 2;
            }

            onCycle?.Invoke(cycle);
            solver.RunOneCycle();
        }
    }

    private static double EndAngle(Engine engine) =>
        (engine.CrankAngle + engine.CrankAngleStep) * Math.PI / 180;

    /// <summary>
    /// B77's oracle. Between steps the gas has to be one state: its volume, and everything
    /// derived from the angle, belonging to the same instant as its pressure and
    /// temperatures. Three places show whether it is:
    /// <list type="bullet">
    /// <item>Through the closed compression the charge is fixed, so <c>P V / (m R T)</c> is a
    /// constant of the motion: whatever value it starts at, it must not move. The original
    /// pairs each step's pressure with the previous angle's volume, so it drifts with
    /// <c>dV/V</c>.</item>
    /// <item>Through overlap the zone temperatures are put back on the ideal gas law every
    /// step, from the gas's own volume, so they must equal <c>P V / (m R)</c> at the angle
    /// the pressure belongs to.</item>
    /// <item>Through combustion the burnt fraction must be the burn law's at that angle.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BetweenStepsTheGasIsOneState(bool corrected)
    {
        BaselinePaths.Require();

        // B46 zeroes the stale mass-flow derivatives that would otherwise move the charge
        // through compression and break the invariant on their own.
        var engine = BaselineEngine();
        var solver = Solver(engine, corrected, CorrectionCatalogue.ClosedCylinderMassFlow);

        const int measuredCycle = 3;
        var cycle = 0;
        double lowest = double.MaxValue, highest = double.MinValue, overlapWorst = 0, burnWorst = 0;
        int compressionSteps = 0, overlapSteps = 0, burnSteps = 0;

        solver.StepCompleted += s =>
        {
            if (cycle != measuredCycle)
            {
                return;
            }

            var gas = s.Engine.Cylinder;
            var end = EndAngle(s.Engine);

            switch (s.Engine.State)
            {
                case EngineState.Compression:
                    var invariant = gas.PGas * gas.VGas / (gas.MGas * gas.Ru * gas.Tu);
                    lowest = Math.Min(lowest, invariant);
                    highest = Math.Max(highest, invariant);
                    compressionSteps++;
                    break;

                case EngineState.Overlap:
                    var idealGas = gas.PGas * s.Geometry.Volume(end) / (gas.MGas * gas.RGas);
                    overlapWorst = Math.Max(overlapWorst, Math.Abs((gas.Tu / idealGas) - 1));
                    overlapSteps++;
                    break;

                // Away from the 0.01 clamps at either end of the burn.
                case EngineState.Combustion when gas.Xb is > 0.02 and < 0.98:
                    burnWorst = Math.Max(burnWorst, Math.Abs(gas.Xb - s.Cylinder.Cylinder.BurntFraction(end)));
                    burnSteps++;
                    break;
            }
        };

        RunCycles(solver, measuredCycle, c => cycle = c);

        Assert.True(compressionSteps > 50 && overlapSteps > 20 && burnSteps > 10, $"{compressionSteps}, {overlapSteps}, {burnSteps}");

        var drift = (highest / lowest) - 1;

        if (corrected)
        {
            // Measured at 2.1e-5 across the whole compression: the integrator's own error,
            // the original's first-order RKF5 (B14) being the one in use here.
            Assert.InRange(drift, 0, 1e-4);
            Assert.InRange(overlapWorst, 0, 1e-12);
            Assert.InRange(burnWorst, 0, 1e-15);
        }
        else
        {
            // Measured at 1.6, 2.7 and 2.9 per cent.
            Assert.True(drift > 0.01, $"{drift}");
            Assert.True(overlapWorst > 0.01, $"{overlapWorst}");
            Assert.True(burnWorst > 0.01, $"{burnWorst}");
        }
    }

    /// <summary>
    /// B77's oracle for the work accumulator. Through an adiabatic single-zone compression
    /// the first law closes on work alone, and with B35 and B76 the integrator's own state
    /// does close it (<c>CylinderEquationTests</c>). The accumulated work has to close it
    /// too. The original accumulates the step's closing pressure times the volume rate at
    /// its opening angle, a phase error of a whole step in the integrand; under B77 each
    /// end is evaluated at its own state and the step takes the trapezoid.
    /// </summary>
    [Theory]
    [InlineData(false, -0.03, -0.01)] // Measured at -2.0 per cent.
    [InlineData(true, -0.004, 0.004)] // Measured at +0.16 per cent.
    public void TheAccumulatedWorkClosesTheFirstLaw(bool corrected, double lowest, double highest)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(
            engine, corrected, CorrectionCatalogue.SingleZoneGamma, CorrectionCatalogue.SingleZoneTrialState);
        solver.Cylinder.WoschniCoefficient = 0;

        var unburnt = solver.Cylinder.Cylinder.Unburnt;
        double? startEnergy = null, startWork = null;
        double endEnergy = 0, endWork = 0;

        solver.StepCompleted += s =>
        {
            if (s.Engine.State != EngineState.Compression
                || s.Engine.CrankAngle < -80 || s.Engine.CrankAngle > -40)
            {
                return;
            }

            // The integrator's state, so the energy does not depend on B77 itself.
            var gas = s.Engine.Cylinder;
            var pressure = s.Engine.Integration.Y[1];
            var volume = s.Geometry.Volume(EndAngle(s.Engine));
            var temperature = pressure * volume / (gas.MGas * unburnt.GasConstant(pressure, 300));
            var energy = gas.MGas * unburnt.InternalEnergy(pressure, temperature);

            startEnergy ??= energy;
            startWork ??= s.Engine.Work;
            endEnergy = energy;
            endWork = s.Engine.Work;
        };

        RunCycles(solver, 1);

        Assert.NotNull(startEnergy);

        // The accumulator counts work done by the gas; compression does work on it.
        var workOnGas = -(endWork - startWork!.Value);
        var residual = (endEnergy - startEnergy.Value - workOnGas) / Math.Abs(workOnGas);

        Assert.InRange(residual, lowest, highest);
    }

    /// <summary>
    /// B77's oracle for the PVT trace: a row holds the state of the angle it is filed
    /// under. Each step's closing state belongs to the angle it finished at, so under B77
    /// it is filed there - the step that starts at 360 filling row -359 - where the
    /// original files it under the angle the step started at.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachTraceRowHoldsTheStateOfItsOwnAngle(bool corrected)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(engine, corrected);
        var recorder = new CrankAngleTraceRecorder(solver.InletValve, solver.ExhaustValve, endOfStepAngle: corrected);
        var closingPressure = new Dictionary<int, double>();
        var cycle = 0;

        solver.StepCompleted += s =>
        {
            recorder.Record(s.Engine);

            if (cycle == 2)
            {
                closingPressure[(int)Math.Round(s.Engine.CrankAngle)] = s.Engine.Cylinder.PGas;
            }
        };

        RunCycles(solver, 2, c => cycle = c);

        foreach (var (startAngle, pressure) in closingPressure)
        {
            var row = startAngle;

            if (corrected)
            {
                row = startAngle == EsaLimits.LastCrankAngle ? EsaLimits.FirstCrankAngle : startAngle + 1;
            }

            Assert.Equal(pressure, recorder.Trace[row][2]);
            Assert.Equal(1, recorder.Trace[row][1] / solver.Geometry.Volume(row * Math.PI / 180), 12);
        }
    }
}
