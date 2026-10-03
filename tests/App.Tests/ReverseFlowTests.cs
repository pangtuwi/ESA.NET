using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B59-B62: the two reverse-flow valve routines, checked against what flow
/// through a valve physically has to do.
/// </summary>
public sealed class ReverseFlowTests
{
    private const double Gamma = 1.4;

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static double MainProgAngle(double traceAngle) => traceAngle + 360;

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

        return loader.Load(BaselinePaths.File("A2China.eng")).Engine;
    }

    /// <summary>
    /// B59's oracle. A cylinder at a lower pressure than the throat cannot push gas out
    /// through the inlet valve: nothing flows. The original nudges the throat pressure just
    /// under the cylinder's, which makes its own no-flow branch unreachable, and with these
    /// inputs stops the run with a CFD error instead (pinned in
    /// <c>InletValveReverseBoundaryTests</c>).
    /// </summary>
    [Fact]
    public void ACylinderBelowTheThroatPushesNothingOutThroughTheInletValve()
    {
        BaselinePaths.Require();

        var engine = BaselineEngine();
        var pipe = new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);
        var valve = ValveMotion.FromValve(engine.Manifold.InletValve);

        var grid = new PipeGrid(EsaLimits.InletGridPoints);
        PipeGridInitialiser.Initialise(
            grid, 39, pipe.Length, 99000, 298.15, CharacteristicSolver.InletGamma);

        var result = InletValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 50000, cylinderTemperature: 800, crankAngle: MainProgAngle(-10),
            pipeAreaAtValve: pipe.Area(pipe.Length), valveFlowArea: valve.FlowArea(-10),
            new InletValveReverseBoundary.ThroatState(0, 0, 0, 0, 99000, 0.7),
            reverseTuning: -0.24,
            stallBelowThroat: true);

        Assert.Equal(0, result.Velocity);
        Assert.Equal(0, result.MachNumber);
        Assert.Equal(0, grid.Velocity[grid.ActiveCount - 1]);
    }

    /// <summary>
    /// B60, closed as not a defect. <c>CritPress</c> is the exact critical ratio for a
    /// restriction fed from a <b>moving</b> pipe: at its root the throat is sonic and the
    /// pipe state upstream of it, at the pressure the ratio implies, carries the same mass
    /// and the same stagnation enthalpy. The inlet's forward routine is fed from the pipe,
    /// so it is right to use it; the reverse routine is fed from the cylinder, a stagnant
    /// reservoir, where it reduces to the plain isentropic ratio the reverse routine uses.
    /// </summary>
    [Theory]
    [InlineData(0.7, 0.3)]
    [InlineData(0.6, 0.9)]
    [InlineData(0.85, 1.0)]
    public void CritPressIsThePipeFedCriticalRatio(double dischargeCoefficient, double areaRatio)
    {
        // Throat to pipe-static pressure ratio at choking.
        var r = ManifoldNumerics.CriticalPressure(Gamma, dischargeCoefficient, areaRatio);

        // A pipe state, and the throat it expands to isentropically at that ratio.
        const double pipeTemperature = 300;
        const double pipePressure = 100000;
        const double r287 = 287;
        var throatTemperature = pipeTemperature * Math.Pow(r, (Gamma - 1) / Gamma);
        var throatSpeed = Math.Sqrt(Gamma * r287 * throatTemperature);
        var throatDensity = pipePressure * r / (r287 * throatTemperature);
        var pipeDensity = pipePressure / (r287 * pipeTemperature);

        // Continuity fixes the pipe velocity that feeds a sonic throat...
        var pipeVelocity = throatDensity * throatSpeed * dischargeCoefficient * areaRatio / pipeDensity;

        // ...and energy then has to balance across the restriction.
        var cp = Gamma * r287 / (Gamma - 1);
        var upstream = (cp * pipeTemperature) + (pipeVelocity * pipeVelocity / 2);
        var throat = (cp * throatTemperature) + (throatSpeed * throatSpeed / 2);

        Assert.Equal(1, throat / upstream, 6);
    }

    [Fact]
    public void CritPressReducesToTheIsentropicRatioForAReservoir()
    {
        var isentropic = Math.Pow(2 / (Gamma + 1), Gamma / (Gamma - 1));

        Assert.Equal(isentropic, ManifoldNumerics.CriticalPressure(Gamma, 0.7, 0.001), 6);
    }

    private static (PipeGeometry Pipe, ValveMotion Valve) Exhaust()
    {
        var engine = BaselineEngine();

        return (new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength),
                ValveMotion.FromValve(engine.Manifold.ExhaustValve));
    }

    private static PipeGrid ExhaustGrid(PipeGeometry pipe, double pressure, double temperature)
    {
        var grid = new PipeGrid(EsaLimits.ExhaustGridPoints);
        PipeGridInitialiser.Initialise(
            grid, 16, pipe.Length, pressure, temperature, CharacteristicSolver.ExhaustGamma);
        return grid;
    }

    private static InletValveReverseBoundary.ThroatState Throat(double pressure, double temperature) =>
        new(0, 0, Math.Sqrt(CharacteristicSolver.ExhaustGamma * 287 * temperature), 0, pressure, 0.7);

    /// <summary>
    /// B61's oracle. When the cylinder is the higher of the two, nothing comes back in, and
    /// the throat is relaxed halfway towards cylinder conditions - once, as the inlet's
    /// equivalent branch does. The original relaxes it on every outer pass and runs two,
    /// landing three quarters of the way (pinned in <c>ExhaustValveReverseBoundaryTests</c>).
    /// </summary>
    [Fact]
    public void AHigherCylinderRelaxesTheExhaustThroatOnceHalfway()
    {
        BaselinePaths.Require();

        const double backPressure = 117800;
        const double backTemperature = 973.15;
        var (pipe, valve) = Exhaust();
        var grid = ExhaustGrid(pipe, backPressure, backTemperature);

        var result = ExhaustValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 400000, cylinderTemperature: 1100,
            crankAngle: MainProgAngle(200),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(200),
            Throat(backPressure, backTemperature), (0.41, -0.6),
            singleRelaxation: true);

        Assert.Equal(0, result.Velocity);
        Assert.Equal((0.5 * 400000) + (0.5 * backPressure), result.Pressure, 6);
    }

    /// <summary>
    /// B62's oracle: a throat Mach number cannot exceed 1. Gas running down the pipe towards
    /// the valve carries a stagnation pressure above its static one, and the throat is
    /// built from the stagnation pressure. The original decides between choked and subsonic
    /// on the static pressure, so with these inputs it settles on the subsonic branch and
    /// builds a throat at Mach 1.11; deciding on the stagnation pressure, the throat chokes
    /// at exactly Mach 1.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExhaustThroatFedFromAMovingPipeNeverRunsSupersonic(bool corrected)
    {
        BaselinePaths.Require();

        const double pipeTemperature = 900;
        const double pipePressure = 160000;
        const double gamma = CharacteristicSolver.ExhaustGamma;

        var (pipe, valve) = Exhaust();
        var grid = ExhaustGrid(pipe, pipePressure, pipeTemperature);

        // Gas heading for the valve, which sits at x = 0, at Mach 0.4.
        var speedOfSound = Math.Sqrt(gamma * 287 * pipeTemperature);
        for (var i = 0; i < grid.ActiveCount; i++)
        {
            grid.Velocity[i] = -0.4 * speedOfSound;
        }

        var result = ExhaustValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 80000, cylinderTemperature: 900,
            crankAngle: MainProgAngle(200),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(200),
            Throat(pipePressure, pipeTemperature), (0.41, -0.6),
            stagnationChoke: corrected);

        if (corrected)
        {
            Assert.Equal(1, result.MachNumber);
        }
        else
        {
            Assert.True(result.MachNumber > 1.05, $"Throat Mach came out {result.MachNumber:F4}.");
        }
    }
}
