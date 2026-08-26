using System.Globalization;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Checks the reported performance against <c>SimulDat.txt</c>, the original's own
/// results file for the baseline run.
/// </summary>
/// <remarks>
/// The inputs come from the reference trace rather than from a simulated cycle: the
/// accumulators as they stood at the end of the cycle, plus the mass totals the results
/// file itself reports. That makes this a test of the performance arithmetic alone -
/// twenty-odd formulas, several of them surprising - independently of how well the cycle
/// that fed them is reproduced.
/// </remarks>
public sealed class PerformanceCalculatorTests
{
    private const double Rpm = 4000;

    private static Dictionary<string, double> Reported()
    {
        var lines = System.IO.File.ReadAllLines(BaselinePaths.File("SimulDat.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var values = lines[1].TrimEnd('.').Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return header
            .Zip(values, (name, value) => (name, value))
            .ToDictionary(p => p.name, p => double.Parse(p.value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The trace's accumulators one step before they reset at inlet valve closing, which
    /// is where a completed cycle's totals stand.
    /// </summary>
    private static (double Work, double PumpWork, double HeatLoss) CycleTotals()
    {
        double At(string column) =>
            BaselinePaths.TraceColumn(column).Single(p => p.CrankAngle == -101).Value;

        return (At("WWork"), At("PWork"), At("htLoss"));
    }

    private static App.Core.Model.Engine Prepared()
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
        engine.Rpm = Rpm;

        // Initialise through the solver so the swept volume, the atmospheric reference
        // mass and the plenum gas constant are the ones the simulation would compute -
        // volumetric efficiency depends on all three.
        var solver = new CycleSolver(engine, new RecordedManifoldSource(298.15));
        solver.Initialise();

        var reported = Reported();
        var (work, pumpWork, heatLoss) = CycleTotals();

        engine.Work = work;
        engine.PumpingWork = pumpWork;
        engine.HeatLoss = heatLoss;

        // Masses in milligrams in the results file.
        engine.Cylinder.MGas = reported["TMass"] / 1E6;
        engine.TotalMassInInletValve = reported["MassIn"] / 1E6;
        engine.TotalMassOutExhaustValve = reported["MassOut"] / 1E6;

        return engine;
    }

    [Fact]
    public void EveryReportedFigureMatchesTheOriginalsResultsFile()
    {
        BaselinePaths.Require();

        var engine = Prepared();
        var reported = Reported();

        new PerformanceCalculator().Calculate(engine);

        // Mean effective pressures, written in bar to three decimals.
        Assert.Equal(reported["IMEP"], engine.Imep / 1e5, 3);
        Assert.Equal(reported["PMEP"], engine.Pmep / 1e5, 3);
        Assert.Equal(reported["FMEP"], engine.Fmep / 1e5, 3);
        Assert.Equal(reported["BMEP"], engine.Bmep / 1e5, 3);

        Assert.Equal(reported["Torque"], engine.Torque, 2);
        Assert.Equal(reported["Power"], engine.BrakePower / 1000, 3);

        // Efficiencies, in per cent to one decimal.
        Assert.Equal(reported["MEff"], engine.MechanicalEfficiency, 1);
        Assert.Equal(reported["ThEff"], engine.ThermalEfficiency, 1);

        // Fuel flow in kg/h and specific consumption in g/kWh.
        Assert.Equal(reported["mf"], engine.FuelMassFlow, 2);
        Assert.Equal(reported["SFC"], engine.Sfc, 1);
    }

    [Fact]
    public void TheEnergyBalanceMatchesAndAccountsForAllOfTheFuel()
    {
        BaselinePaths.Require();

        var engine = Prepared();
        var reported = Reported();

        new PerformanceCalculator().Calculate(engine);

        Assert.Equal(reported["QHeat"], engine.QHeat, 1);
        Assert.Equal(reported["QWork"], engine.QWork, 1);
        Assert.Equal(reported["QExht"], engine.QExhaust, 1);
        Assert.Equal(reported["QPump"], engine.QPump, 1);
        Assert.Equal(reported["QFric"], engine.QFriction, 1);

        // The exhaust term is the remainder, so the five shares close on 100 per cent by
        // construction. Worth asserting anyway: it catches a term dropped or double
        // counted.
        var total = engine.QHeat + engine.QWork + engine.QExhaust + engine.QPump + engine.QFriction;
        Assert.Equal(100, total, 6);

        // Brake work as a share of fuel energy is thermal efficiency by another route,
        // and the original computes the two completely differently - one from BMEP and
        // swept volume, the other from brake power and a fuel flow rate.
        Assert.Equal(engine.ThermalEfficiency, engine.QWork, 1);
    }

    [Fact]
    public void VolumetricEfficiencyExceedsOneHundredPerCentOnTheReferenceEngine()
    {
        BaselinePaths.Require();

        var engine = Prepared();

        new PerformanceCalculator().Calculate(engine);

        // 109.7 per cent: the inlet tuning genuinely delivers more than the reference
        // mass. That reference is the cylinder volume at bottom dead centre filled at
        // plenum conditions, computed once during initialisation, so this also checks
        // that the plenum's gas constant came out right - 287 exactly would give 108.2.
        Assert.Equal(Reported()["VEff"], engine.VolumetricEfficiency, 1);
    }

    [Fact]
    public void FuelFlowAndThermalEfficiencyAreWrongForAnyEngineThatIsNotAFourCylinder()
    {
        BaselinePaths.Require();

        var engine = Prepared();
        var calculator = new PerformanceCalculator();

        calculator.Calculate(engine);
        var fourCylinderFlow = engine.FuelMassFlow;

        // The factor is 2 * Nrpm where the physics wants NCyl * Nrpm / 2, so the two
        // agree only at four cylinders. A six-cylinder gets the same fuel flow as a
        // four, and its SFC and thermal efficiency are wrong by 4/NCyl with it.
        // Reproduced deliberately: see ISSUES.md B1.
        engine.CylinderCount = 6;
        calculator.Calculate(engine);

        Assert.Equal(fourCylinderFlow, engine.FuelMassFlow, 12);
    }
}
