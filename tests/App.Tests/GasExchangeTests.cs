using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B37: valve overlap as two zones, the burnt residual and the fresh charge.
/// </summary>
public sealed class GasExchangeTests
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

    /// <summary>
    /// B37 on or off, with B77 so that the gas between steps is one state, and B18 so that
    /// the test's own property calls cannot nudge the run.
    /// </summary>
    private static CycleSolver Solver(Engine engine, bool twoZones)
    {
        var physics = new PhysicsCorrections();
        physics.Overrides[CorrectionCatalogue.GasExchangeZones.Entry] = twoZones;
        physics.Overrides[CorrectionCatalogue.EndOfStepState.Entry] = true;
        physics.Overrides[CorrectionCatalogue.ResidualMolecularWeight.Entry] = true;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        return solver;
    }

    private static void RunCycles(CycleSolver solver, int cycles, Action<int> onCycle)
    {
        solver.Engine.ZoneCount = 1;

        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            if (cycle > 1)
            {
                solver.Engine.ZoneCount = 2;
            }

            onCycle(cycle);
            solver.RunOneCycle();
        }
    }

    private static double EndAngle(Engine engine) =>
        (engine.CrankAngle + engine.CrankAngleStep) * Math.PI / 180;

    /// <summary>
    /// The four gas-exchange equations' solutions, the original's Cramer's-rule
    /// expressions, solve the system they come from: two ideal-gas zones at one pressure
    /// sharing the cylinder, and each zone's energy balance.
    /// </summary>
    [Fact]
    public void TheGasExchangeSolutionsSolveTheirEquations()
    {
        var random = new Random(37);

        for (var trial = 0; trial < 1000; trial++)
        {
            double Next() => (random.NextDouble() * 2) - 1;

            var c = new CylinderModel.GasExchangeCoefficients(
                A: Next(), D: Next(), Q: Next(), E: Next(), G: Next(), R: Next(),
                I: Next(), J: Next(), K: Next(), S: Next(), M: Next(), N: Next(), P: Next(), T: Next());

            if (Math.Abs(CylinderModel.GasExchangeDeterminant(c)) < 1e-3)
            {
                continue;
            }

            var dVb = CylinderModel.BurntVolumeRate(c);
            var dP = CylinderModel.PressureRate(c);
            var dTb = CylinderModel.BurntTemperatureRate(c);
            var dTu = CylinderModel.UnburntTemperatureRate(c);

            var scale = 1 + Math.Abs(dVb) + Math.Abs(dP) + Math.Abs(dTb) + Math.Abs(dTu);

            Assert.Equal(c.S, (c.I * dVb) + (c.J * dP) + (c.K * dTb), 1e-9 * scale);
            Assert.Equal(c.T, (c.M * dVb) + (c.N * dP) + (c.P * dTu), 1e-9 * scale);
            Assert.Equal(c.R, (c.E * dVb) + (c.G * dTb), 1e-9 * scale);
            Assert.Equal(c.Q, (c.A * dVb) + (c.D * dTu), 1e-9 * scale);
        }
    }

    /// <summary>
    /// B37's oracle for the zones. Through overlap the cylinder holds two gases side by
    /// side: their volumes fill it, their masses are the charge, each is on the ideal gas
    /// law at the one pressure, and the fresh charge is the colder. The fresh zone comes
    /// into being on the first step that draws fresh charge. The original holds one gas in
    /// effect: zone volumes of zero (ISSUES.md B29) and one temperature for both.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlapRunsAsTwoZones(bool twoZones)
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(engine, twoZones);
        var cycle = 0;
        int overlapSteps = 0, twoZoneSteps = 0;
        double worstVolume = 0, worstBurnt = 0, worstUnburnt = 0, worstMass = 0;
        bool sawFreshCharge = false, zoneBeforeFreshCharge = false;

        solver.StepCompleted += s =>
        {
            if (cycle != 3 || s.Engine.State != EngineState.Overlap)
            {
                return;
            }

            var gas = s.Engine.Cylinder;
            overlapSteps++;

            if (!twoZones)
            {
                // The overlap tail puts both of the integrator's temperatures back on the one
                // ideal-gas temperature every step.
                Assert.Equal(0, gas.Vb);
                Assert.Equal(0, gas.Vu);
                Assert.Equal(s.Engine.Integration.Y[2], s.Engine.Integration.Y[3]);
                return;
            }

            sawFreshCharge |= s.Engine.MassIn > 0;
            zoneBeforeFreshCharge |= gas.Mu > 0 && !sawFreshCharge;

            worstMass = Math.Max(worstMass, Math.Abs(((gas.Mb + gas.Mu) / gas.MGas) - 1));

            if (gas.Mb > 0 && gas.Mu > 0)
            {
                twoZoneSteps++;

                var volume = s.Geometry.Volume(EndAngle(s.Engine));
                worstVolume = Math.Max(worstVolume, Math.Abs(((gas.Vb + gas.Vu) / volume) - 1));
                worstBurnt = Math.Max(worstBurnt, Math.Abs((gas.PGas * gas.Vb / (gas.Mb * gas.Rb * gas.Tb)) - 1));
                worstUnburnt = Math.Max(worstUnburnt, Math.Abs((gas.PGas * gas.Vu / (gas.Mu * gas.Ru * gas.Tu)) - 1));
                Assert.True(gas.Tu < gas.Tb, $"{gas.Tu} against {gas.Tb}");
            }
        };

        RunCycles(solver, 3, c => cycle = c);

        Assert.True(overlapSteps > 40, $"{overlapSteps}");

        if (twoZones)
        {
            // From the first fresh charge to exhaust valve closing: measured at 28 of 56.
            Assert.True(twoZoneSteps > 20, $"{twoZoneSteps} of {overlapSteps}");
            Assert.False(zoneBeforeFreshCharge);
            Assert.InRange(worstMass, 0, 1e-12);
            Assert.InRange(worstVolume, 0, 1e-12);
            Assert.InRange(worstBurnt, 0, 1e-9);
            Assert.InRange(worstUnburnt, 0, 1e-9);
        }
    }

    /// <summary>
    /// B37's oracle for the energy. Overlap is an open system: the cylinder's energy -
    /// both zones', chemical included - changes by the heat it loses, the work it does,
    /// and the enthalpy every flow carries across the valves. Fresh charge brings the
    /// plenum's; everything else, burnt or unburnt, leaving or coming back through either
    /// valve, is taken at its zone's own, which over a step is the mean of the two ends.
    /// </summary>
    [Fact]
    public void OverlapObeysTheOpenSystemFirstLaw()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var solver = Solver(engine, twoZones: true);
        var cycle = 0;

        double? lastEnergy = null;
        double lastBurntMass = 0, lastUnburntMass = 0, lastBurntEnthalpy = 0, lastUnburntEnthalpy = 0;
        double lastPumpingWork = 0, lastBurntInPort = 0;
        double residual = 0, exchanged = 0;
        var steps = 0;

        solver.StepCompleted += s =>
        {
            if (cycle != 3)
            {
                return;
            }

            var e = s.Engine;
            var gas = e.Cylinder;
            var energy = (gas.Mb * gas.Ub) + (gas.Mu * gas.Uu);

            if (e.State == EngineState.Overlap && lastEnergy is not null)
            {
                var work = -(e.PumpingWork - lastPumpingWork);
                var heat = e.Qb + e.Qu;
                // Inflow first draws back the burnt gas overlap pushed into the port; only
                // what comes after it is fresh charge.
                var fresh = e.MassIn > 0 ? e.MassIn - Math.Min(e.MassIn, lastBurntInPort) : 0;
                var unburntEnthalpy = lastUnburntMass > 0 ? 0.5 * (lastUnburntEnthalpy + gas.Hu) : gas.Hu;

                var enthalpy = ((gas.Mb - lastBurntMass) * 0.5 * (lastBurntEnthalpy + gas.Hb))
                               + (fresh * e.Plenum.Hu)
                               + ((gas.Mu - lastUnburntMass - fresh) * unburntEnthalpy);

                residual += energy - lastEnergy.Value + work - heat - enthalpy;
                exchanged += Math.Abs(work) + Math.Abs(heat) + Math.Abs(enthalpy);
                steps++;
            }

            lastEnergy = e.State == EngineState.Overlap || e.State == EngineState.Exhaust ? energy : null;
            lastBurntMass = gas.Mb;
            lastUnburntMass = gas.Mu;
            lastBurntEnthalpy = gas.Hb;
            lastUnburntEnthalpy = gas.Hu;
            lastPumpingWork = e.PumpingWork;
            lastBurntInPort = e.BurntMassOutInlet;
        };

        RunCycles(solver, 3, c => cycle = c);

        Assert.True(steps > 40, $"{steps}");

        TestContext.Current.TestOutputHelper?.WriteLine($"residual {residual} J of {exchanged} J exchanged: {residual / exchanged:E2}");

        // Measured at 0.22 per cent: the integrator's error, and the enthalpies' within
        // each step.
        Assert.InRange(Math.Abs(residual / exchanged), 0, 0.01);
    }
}
