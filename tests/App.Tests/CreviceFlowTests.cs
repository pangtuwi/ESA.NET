using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B97: the cylinder has crevices. Gas at cylinder pressure and wall temperature fills
/// them as the pressure rises and comes back as it falls, and about 1 per cent of the charge is
/// lost past the rings (Heywood 1988, section 8.6).
/// </summary>
public sealed class CreviceFlowTests
{
    private const double R = 287;

    private static Engine Baseline(double rpm = 4000)
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
        engine.Rpm = rpm;
        return engine;
    }

    private static PhysicsCorrections Corrected(bool crevices = true, bool combustionEfficiency = true)
    {
        var physics = new PhysicsCorrections();
        physics.Overrides[CorrectionCatalogue.CreviceFlow.Entry] = crevices;
        physics.Overrides[CorrectionCatalogue.CombustionEfficiency.Entry] = combustionEfficiency;
        return physics;
    }

    private static void Run(CycleSolver solver, int cycles, Action<int>? beforeCycle = null)
    {
        var engine = solver.Engine;
        engine.ZoneCount = 1;

        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            beforeCycle?.Invoke(cycle);
            solver.RunOneCycle();
        }
    }

    /// <summary>
    /// The store holds what puts it at the cylinder's pressure, <c>p V / (R T)</c>; gives gas
    /// back in proportion to its unburnt and burnt contents; and loses to blow-by the share of
    /// each release that makes the cycle's blow-by its share of the charge, given its peak.
    /// </summary>
    [Fact]
    public void TheStoreHoldsTheMassThatPutsItAtTheCylindersPressure()
    {
        const double Volume = 1e-6;
        const double Wall = 450;
        var store = new CreviceStore(Volume, Wall, blowByShare: 0.01);
        store.StartCycle();

        var admitted = store.Exchange(40e5, R, R, admitBurnt: false, maximumAdmission: 1, trappedMass: 500e-6, blowBy: false);
        Assert.Equal(40e5 * Volume / (R * Wall), store.Mass, 1e-15);
        Assert.Equal(store.Mass, admitted.AdmittedUnburnt, 1e-15);

        store.Exchange(60e5, R, R, admitBurnt: true, maximumAdmission: 1, trappedMass: 500e-6, blowBy: false);
        var unburntShare = store.UnburntMass / store.Mass;
        var peak = store.PeakMass;
        Assert.Equal(60e5 * Volume / (R * Wall), peak, 1e-15);

        var released = store.Exchange(30e5, R, R, admitBurnt: false, maximumAdmission: 1, trappedMass: 500e-6, blowBy: true);
        Assert.Equal(30e5 * Volume / (R * Wall), store.Mass, 1e-15);
        Assert.Equal(unburntShare, (released.ReleasedUnburnt + released.BlownUnburnt) / (released.Released + released.Blown), 1e-12);
        Assert.Equal(0.01 * 500e-6 / peak, released.Blown / (released.Released + released.Blown), 1e-12);
        Assert.Equal(released.Blown, store.BlowBy, 1e-18);
    }

    /// <summary>
    /// Heywood's oracle (p. 362): 5 to 10 per cent of the charge is in the crevices at peak
    /// pressure, and about 1 per cent goes past the rings. Measured on the baseline engine at
    /// 4000 rpm: 8.8 and 1.0 per cent. Its peak pressure, 70 bar, is above that of Heywood's
    /// engine at 2000 rpm, about 40 atm, so 12 per cent is allowed.
    /// </summary>
    [Fact]
    public void TheCrevicesHoldHeywoodsShareOfTheChargeAndLoseHisBlowBy()
    {
        BaselinePaths.Require();

        var engine = Baseline();
        var physics = Corrected();
        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise());
        Run(solver, 6);

        Assert.InRange(engine.PeakCreviceMass / engine.TrappedMass, 0.05, 0.12);
        Assert.InRange(engine.BlowByMass / engine.TrappedMass, 0.009, 0.011);
    }

    /// <summary>
    /// Mass is conserved through the closed period: what the cylinder holds, what the crevices
    /// hold and what has gone past the rings always make the trapped charge.
    /// </summary>
    [Fact]
    public void TheCylinderTheCrevicesAndTheBlowByMakeTheTrappedCharge()
    {
        BaselinePaths.Require();

        var engine = Baseline();
        var physics = Corrected();
        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise());

        var cycle = 0;
        var worst = 0.0;
        var steps = 0;

        solver.StepCompleted += s =>
        {
            var e = s.Engine;

            if (cycle == 6 && e.State is EngineState.Compression or EngineState.Combustion or EngineState.Expansion)
            {
                worst = Math.Max(worst, Math.Abs(e.Cylinder.MGas + e.CreviceMass + e.BlowByMass - e.TrappedMass) / e.TrappedMass);
                steps++;
            }
        };

        Run(solver, 6, c => cycle = c);

        Assert.True(steps > 200, $"Only {steps} closed steps.");
        Assert.InRange(worst, 0, 1e-12);
    }

    /// <summary>
    /// Unburnt gas the crevices take does not come out of the burnt zone: through combustion the
    /// burnt mass only grows, and the burn finishes on what the cylinder holds, with the unburnt
    /// zone at the original's one per cent floor as it ends.
    /// </summary>
    [Fact]
    public void TheBurntZoneOnlyGrowsAndTheBurnFinishesOnTheCylindersCharge()
    {
        BaselinePaths.Require();

        var engine = Baseline();
        var physics = Corrected();
        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise());

        var cycle = 0;
        var lastBurnt = 0.0;
        var shrank = 0;
        var lastUnburntShare = 1.0;

        solver.StepCompleted += s =>
        {
            var c = s.Engine.Cylinder;

            if (cycle != 6 || s.Engine.State != EngineState.Combustion)
            {
                lastBurnt = 0;
                return;
            }

            if (c.Mb < lastBurnt)
            {
                shrank++;
            }

            lastBurnt = c.Mb;
            lastUnburntShare = c.Mu / c.MGas;
        };

        Run(solver, 6, c => cycle = c);

        Assert.Equal(0, shrank);
        Assert.InRange(lastUnburntShare, 0.009, 0.0101);
    }

    /// <summary>
    /// The oracle for the exchange: energy is conserved. Over compression, and over expansion with
    /// no blow-by and B92 off, the cylinder's and the crevices' internal energy changes by the heat
    /// to the gas less the work it does, the heat including the crevice walls'. The residual is the
    /// integration's own, the same with the crevices as without.
    /// </summary>
    [Theory]
    [InlineData(EngineState.Compression)]
    [InlineData(EngineState.Expansion)]
    public void TheExchangeConservesEnergy(EngineState window)
    {
        BaselinePaths.Require();

        var withCrevices = Residual(window, crevices: true);
        var without = Residual(window, crevices: false);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{window}: residual {withCrevices.Residual:E3} J of {withCrevices.Work:E3} J work with crevices, {without.Residual:E3} of {without.Work:E3} without");

        Assert.InRange(Math.Abs(withCrevices.Residual), 0, Math.Max(2 * Math.Abs(without.Residual), 1e-4 * Math.Abs(withCrevices.Work)));
    }

    private static (double Residual, double Work) Residual(EngineState window, bool crevices)
    {
        var engine = Baseline();
        engine.CreviceBlowBy = 0;
        var physics = Corrected(crevices, combustionEfficiency: false);
        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise());

        var gas = solver.Cylinder.Cylinder;
        var cycle = 0;
        var stepsIn = 0;
        (double Energy, double Work, double Heat)? first = null;
        (double Energy, double Work, double Heat) last = default;

        double Energy(Engine e)
        {
            var c = e.Cylinder;
            var total = (c.Mb > 0 ? c.Mb * gas.Burnt.InternalEnergy(c.PGas, c.Tb) : 0)
                        + (c.Mu > 0 ? c.Mu * gas.Unburnt.InternalEnergy(c.PGas, c.Tu) : 0);

            if (solver.Crevices is { } store)
            {
                total += (store.UnburntMass * gas.Unburnt.InternalEnergy(c.PGas, store.Temperature))
                         + (store.BurntMass * gas.Burnt.InternalEnergy(c.PGas, store.Temperature));
            }

            return total;
        }

        solver.StepCompleted += s =>
        {
            var e = s.Engine;

            if (cycle != 6 || e.State != window)
            {
                stepsIn = 0;
                return;
            }

            // From the end of the window's second step, after the state's entry has settled.
            if (++stepsIn < 2)
            {
                return;
            }

            last = (Energy(e), e.Work, e.HeatLoss);
            first ??= last;
        };

        Run(solver, 6, c => cycle = c);

        var start = first!.Value;
        var work = last.Work - start.Work;
        return (last.Energy - start.Energy - (last.Heat - start.Heat) + work, work);
    }

    /// <summary>
    /// With no crevice volume the exchange moves nothing and the run is the run without B97, to
    /// the settling's own convergence.
    /// </summary>
    [Fact]
    public void WithNoVolumeTheCrevicesChangeNothing()
    {
        BaselinePaths.Require();

        double Torque(bool crevices, double? fraction)
        {
            var engine = Baseline();
            engine.CreviceVolumeFraction = fraction;
            var physics = Corrected(crevices);
            var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
            Assert.True(solver.Initialise());
            Run(solver, 4);
            new PerformanceCalculator().Calculate(engine, physics);
            return engine.Torque;
        }

        var without = Torque(false, null);
        Assert.Equal(1, Torque(true, 0) / without, 1e-8);
        Assert.True(Torque(true, null) < 0.95 * without);
    }

    /// <summary>The optional keys are read where they are given and are never written.</summary>
    [Fact]
    public void TheCreviceKeysAreReadAndNeverWritten()
    {
        var store = new EngineDefinitionStore();
        var definition = store.Read(Path.Combine(TestPaths.Samples, "Default.eng"));
        Assert.Null(definition.CreviceVolumeFraction);
        Assert.Null(definition.CreviceBlowBy);

        var folder = Directory.CreateTempSubdirectory("esa-b97-").FullName;

        try
        {
            var path = Path.Combine(folder, "Crevices.eng");
            File.WriteAllText(path, File.ReadAllText(Path.Combine(TestPaths.Samples, "Default.eng"))
                                    + "\r\n[Crevices]\r\nVolumeFraction=0.03\r\nBlowBy=0.005\r\n");

            var read = store.Read(path);
            Assert.Equal(0.03, read.CreviceVolumeFraction);
            Assert.Equal(0.005, read.CreviceBlowBy);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void CorrectedRunsB97AndLegacyDoesNot()
    {
        Assert.True(new PhysicsCorrections().IsOn(CorrectionCatalogue.CreviceFlow));
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.CreviceFlow));
    }
}
