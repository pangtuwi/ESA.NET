using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B35 and B36: the single-zone pressure equation's gamma, and the two unburnt
/// equations' transfer enthalpy.
/// </summary>
public sealed class CylinderEquationTests
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

    private static CycleSolver Solver(Engine engine, bool honourVariableGamma, bool trialState = false)
    {
        var physics = new PhysicsCorrections();
        physics.Overrides[CorrectionCatalogue.SingleZoneGamma.Entry] = honourVariableGamma;
        physics.Overrides[CorrectionCatalogue.SingleZoneTrialState.Entry] = trialState;

        // B18 keeps the unburnt model free of call history, so the test's own property
        // calls below cannot nudge the run.
        physics.Overrides[CorrectionCatalogue.ResidualMolecularWeight.Entry] = true;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        return solver;
    }

    /// <summary>What an adiabatic single-zone compression did.</summary>
    /// <param name="Residual">The first law, <c>m dU - W</c>, as a fraction of the work.</param>
    /// <param name="PolytropicIndex">The <c>n</c> of <c>P V^n</c> across the window.</param>
    /// <param name="MeanGamma">The charge's own gamma, averaged across the window.</param>
    private sealed record Compression(double Residual, double PolytropicIndex, double MeanGamma);

    /// <summary>
    /// Runs the single-zone first cycle with heat transfer switched off and measures its
    /// compression, from 80 to 40 degrees before top dead centre. That is short of the
    /// spark, so nothing has burnt, and past the first few dozen steps from InitVars'
    /// guessed state, whose ideal-gas temperature starts under the 273.15 K clamp.
    /// </summary>
    /// <remarks>
    /// The state is taken as the integrator leaves it, end-of-step pressure with
    /// end-of-step volume. The gas object pairs the end-of-step pressure with the volume
    /// at the start of the step (ISSUES.md B77), which would bias the measurement by about
    /// two per cent of the index.
    /// </remarks>
    private static Compression AdiabaticCompression(bool honourVariableGamma, bool trialState)
    {
        var engine = BaselineEngine();
        Assert.True(engine.VariableGamma, "The baseline engine has Variable Gamma ticked, as every shipped engine does.");

        var solver = Solver(engine, honourVariableGamma, trialState);
        solver.Cylinder.WoschniCoefficient = 0;

        var unburnt = solver.Cylinder.Cylinder.Unburnt;
        double? startEnergy = null;
        double startMass = 0, startPressure = 0, startVolume = 0;
        double endEnergy = 0, work = 0, pressure = 0, volume = 0, gammaSum = 0;
        var steps = 0;

        solver.StepCompleted += s =>
        {
            if (s.Engine.State != EngineState.Compression
                || s.Engine.CrankAngle < -80 || s.Engine.CrankAngle > -40)
            {
                return;
            }

            var gas = s.Engine.Cylinder;
            var endPressure = s.Engine.Integration.Y[1];
            var endVolume = s.Geometry.Volume((s.Engine.CrankAngle + s.Engine.CrankAngleStep) * Math.PI / 180);
            var temperature = endPressure * endVolume / (gas.MGas * unburnt.GasConstant(endPressure, 300));
            var energy = gas.MGas * unburnt.InternalEnergy(endPressure, temperature);

            if (startEnergy is null)
            {
                startEnergy = energy;
                startMass = gas.MGas;
                startPressure = endPressure;
                startVolume = endVolume;
            }
            else
            {
                work -= 0.5 * (endPressure + pressure) * (endVolume - volume);
                Assert.Equal(startMass, gas.MGas);
            }

            Assert.True(temperature > 280, $"{temperature} K at {s.Engine.CrankAngle}");

            endEnergy = energy;
            pressure = endPressure;
            volume = endVolume;
            gammaSum += gas.Gamma;
            steps++;
        };

        // The first cycle is the single-zone one, as the runner runs it.
        engine.ZoneCount = 1;
        solver.RunOneCycle();

        Assert.NotNull(startEnergy);
        Assert.True(steps >= 40, $"Only {steps} compression steps were seen.");

        return new Compression(
            (endEnergy - startEnergy.Value - work) / Math.Abs(work),
            Math.Log(pressure / startPressure) / Math.Log(startVolume / volume),
            gammaSum / steps);
    }

    /// <summary>
    /// The oracle for B35 and B76 together. Compressing a closed, adiabatic charge is
    /// isentropic, and for an ideal gas whose specific heats vary with temperature that is
    /// exactly <c>dP/P = -gamma dV/V</c> with the gas's own gamma at each instant. So the
    /// first law must close on work alone, <c>m dU = -P dV</c> with <c>U</c> from the
    /// property model, and the compression must follow the charge's gamma.
    /// </summary>
    [Fact]
    public void WithBothCorrectionsAnAdiabaticSingleZoneCompressionIsIsentropic()
    {
        BaselinePaths.Require();

        var compression = AdiabaticCompression(honourVariableGamma: true, trialState: true);

        // Measured at -0.14 per cent, and 1.3498 against 1.3502.
        Assert.InRange(Math.Abs(compression.Residual), 0, 0.005);
        Assert.Equal(compression.MeanGamma, compression.PolytropicIndex, 0.002);
    }

    /// <summary>
    /// Why the two go together. B76's oracle on its own: with the trial state, the
    /// equation integrates to exactly the <c>P V^gamma</c> it states, here the original's
    /// 1.4 - a stiffer gas than the charge, so the first law misses the other way. With
    /// the stale state the original reads, every stage of a step sees the step's start and
    /// the integration loses about 0.06 of the index; the 1.4 hid most of that, so B35 on
    /// its own, honest gamma on the stale state, is worse than either.
    /// </summary>
    [Theory]
    [InlineData(false, false, 1.30, 1.36, -0.06, -0.02)] // Legacy: 1.338, -3.5 per cent.
    [InlineData(true, false, 1.27, 1.31, -0.20, -0.14)]  // B35 alone: 1.292, -17 per cent.
    [InlineData(false, true, 1.3995, 1.4005, 0.12, 0.18)] // B76 alone: 1.4000, +15 per cent.
    public void EitherCorrectionAloneMissesTheFirstLaw(
        bool honourVariableGamma, bool trialState,
        double lowestIndex, double highestIndex, double lowestResidual, double highestResidual)
    {
        BaselinePaths.Require();

        var compression = AdiabaticCompression(honourVariableGamma, trialState);

        Assert.InRange(compression.PolytropicIndex, lowestIndex, highestIndex);
        Assert.InRange(compression.Residual, lowestResidual, highestResidual);
    }

    /// <summary>
    /// B35 with <i>Variable Gamma</i> unticked: the constant the original's commented-out
    /// test names, 1.35. Away from the burn and with no heat transfer the equation is
    /// <c>-gamma P/V dV/dtheta</c>, so it scales directly with the gamma it uses.
    /// </summary>
    [Theory]
    [InlineData(false, 1.35)]
    [InlineData(true, double.NaN)]
    public void TheSingleZoneGammaFollowsTheVariableGammaSetting(bool variableGamma, double expected)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(engine, honourVariableGamma: false);
        var model = solver.Cylinder;
        var gas = model.Cylinder.State;

        CylinderModel With(bool honour) => new(model.Geometry, model.Cylinder, model.Plenum, engine.WallTemperature)
        {
            Rpm = model.Rpm,
            CrankAngularVelocity = model.CrankAngularVelocity,
            WoschniCoefficient = 0,
            HonourVariableGamma = honour,
            VariableGamma = variableGamma,
            PressureAtInletValveClosing = model.PressureAtInletValveClosing,
            TemperatureAtInletValveClosing = model.TemperatureAtInletValveClosing,
            VolumeAtInletValveClosing = model.VolumeAtInletValveClosing,
        };

        // A compression state, so the gas carries the charge's own gamma.
        var angle = -90 * Math.PI / 180;
        var volume = model.Geometry.Volume(angle);
        model.Cylinder.UpdateUB(volume, model.Geometry.VolumeRatePerRadian(angle), volume, 1.2e5, 330);

        var legacy = With(honour: false).PressureRateSingleZone(angle, []);
        var corrected = With(honour: true).PressureRateSingleZone(angle, []);

        var gamma = double.IsNaN(expected) ? gas.Gamma : expected;
        Assert.InRange(gas.Gamma, 1.3, 1.4);
        Assert.Equal(gamma / 1.4, corrected / legacy, 12);
    }

    /// <summary>
    /// B36's oracle. <c>dPdThetaUB</c> computes <c>dTu/dtheta</c> on the way to the
    /// pressure rate, and <c>dTudThetaUB</c> computes the same quantity; for one state the
    /// two must agree, whatever was evaluated before. With gas flowing out of the
    /// cylinder the transfer enthalpy is the cylinder's own, and the original's
    /// temperature equation took it from the previous evaluation's state.
    /// </summary>
    [Fact]
    public void BothUnburntEquationsAgreeOnTheTemperatureRateWhateverCameBefore()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(engine, honourVariableGamma: false);
        var model = solver.Cylinder;
        var gas = model.Cylinder.State;

        // Backflow into the inlet: the cylinder branch of the transfer enthalpy.
        model.InletMassFlow = -1e-6;
        gas.DmInDTheta = -2e-4;

        var angle = -150 * Math.PI / 180;
        double[] here = [0, 1.2e5, 0, 360];
        double[] elsewhere = [0, 3.0e5, 0, 700];

        // The temperature rate dPdThetaUB implies, from P = m R T / V.
        double ImpliedByPressureRate(double[] y)
        {
            var dPdTheta = model.PressureRateUnburnt(angle, y);
            return gas.Tu * ((dPdTheta / gas.PGas) + (gas.DmInDTheta / gas.Mu) + (gas.DvDTheta / gas.Vu));
        }

        var implied = ImpliedByPressureRate(here);
        var afterSameState = model.UnburntTemperatureRate(angle, here);

        model.PressureRateUnburnt(angle, elsewhere);
        var afterAnotherState = model.UnburntTemperatureRate(angle, here);

        Assert.Equal(implied, afterSameState, 1e-9 * Math.Abs(implied));
        Assert.Equal(afterSameState, afterAnotherState);
    }
}
