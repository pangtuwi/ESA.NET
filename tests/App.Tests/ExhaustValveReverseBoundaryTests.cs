using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Reverse flow at the exhaust valve: gas driven back into the cylinder from the pipe.
/// </summary>
public sealed class ExhaustValveReverseBoundaryTests
{
    private const double BackPressure = 117800;
    private const double BackTemperature = 973.15;

    // EVFFn and EVRFn from A2China.eng, both constants.
    private static readonly (double Forward, double Reverse) Tuning = (0.41, -0.6);

    private static double TimeStep() => 1 / (4000.0 / 60 * 360);

    private static double MainProgAngle(double traceAngle) => traceAngle + 360;

    private static (PipeGeometry Pipe, ValveMotion Valve) Exhaust()
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

        return (new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength),
                ValveMotion.Exhaust(engine.Manifold.ExhaustValve));
    }

    private static PipeGrid Grid(PipeGeometry pipe)
    {
        var grid = new PipeGrid(EsaLimits.ExhaustGridPoints);
        PipeGridInitialiser.Initialise(
            grid, 16, pipe.Length, BackPressure, BackTemperature,
            CharacteristicSolver.ExhaustGamma);
        return grid;
    }

    /// <summary>Mid-exhaust, where the valve is well off its seat.</summary>
    private const double ExhaustAngle = 200;

    private static InletValveReverseBoundary.ThroatState StartingThroat() =>
        new(0, 0, Math.Sqrt(CharacteristicSolver.ExhaustGamma * 287 * BackTemperature),
            0, BackPressure, 0.7);

    [Fact]
    public void AHigherCylinderSubstitutesBackToNormalOutwardFlow()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var grid = Grid(pipe);

        // Cylinder well above the pipe: nothing can come back in, so the substitution
        // branch takes over, holds the pipe end still and relaxes the throat halfway
        // towards cylinder conditions.
        var result = ExhaustValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 400000, cylinderTemperature: 1100,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(), Tuning);

        Assert.Equal(0, result.MachNumber);
        Assert.Equal(0, result.Velocity);
        Assert.Equal(0, grid.Velocity[0]);

        // The relaxation Pt := 0.5*Pcyl + 0.5*Pt is applied on every outer pass, and this
        // branch does not stop the loop the way the inlet's equivalent does (ISSUES.md
        // B61). It runs twice - once on iteration 0, which has no convergence test, and
        // again on iteration 1, which converges because nothing moved - so the throat
        // lands three quarters of the way to cylinder pressure, not half.
        Assert.Equal((0.75 * 400000) + (0.25 * BackPressure), result.Pressure, 6);
    }

    [Fact]
    public void AHigherPipePushesGasBackIntoTheCylinder()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var grid = Grid(pipe);

        // Pipe above the cylinder, so flow genuinely reverses through the valve.
        var result = ExhaustValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 90000, cylinderTemperature: 900,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(), Tuning);

        // Reverse convention: negative at the throat, as at the inlet.
        Assert.True(result.Velocity <= 0, $"Throat velocity came out {result.Velocity:F2} m/s.");
        Assert.True(result.MachNumber > 0, $"Throat Mach came out {result.MachNumber:F4}.");

        // The throat has been pulled down to cylinder pressure on the subsonic branch.
        Assert.Equal(90000, result.Pressure, 6);
    }

    [Fact]
    public void TheThroatStateStaysSelfConsistent()
    {
        BaselinePaths.Require();

        var (pipe, valve) = Exhaust();
        var grid = Grid(pipe);

        var result = ExhaustValveReverseBoundary.Apply(
            grid, pipe, valve, TimeStep(),
            cylinderPressure: 90000, cylinderTemperature: 900,
            crankAngle: MainProgAngle(ExhaustAngle),
            pipeAreaAtValve: pipe.Area(0), valveFlowArea: valve.FlowArea(ExhaustAngle),
            StartingThroat(), Tuning);

        // rho = gamma*P/c^2, the relation MassFlow depends on.
        var implied = CharacteristicSolver.ExhaustGamma * result.Pressure
                      / (result.SpeedOfSound * result.SpeedOfSound);

        Assert.Equal(implied, result.Density, Math.Abs(implied) * 1e-6);
        Assert.InRange(result.DischargeCoefficient, 0, 1);
    }

    [Fact]
    public void TheExhaustCriticalRatioIsThePlainIsentropicOne()
    {
        // The inlet's forward routine derives its critical ratio from CritPress, which
        // accounts for discharge coefficient and area ratio; the exhaust reverse routine
        // uses the plain isentropic value. Same valve, different switching criteria.
        // See ISSUES.md B60.
        const double Gamma = CharacteristicSolver.ExhaustGamma;
        var plain = Math.Pow((Gamma + 1) / 2, Gamma / (Gamma - 1));

        Assert.Equal(1.8324, plain, 3);
        Assert.NotEqual(plain, 1 / ManifoldNumerics.CriticalPressure(Gamma, 0.7, 0.3), 3);
    }
}
