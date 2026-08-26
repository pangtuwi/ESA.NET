using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The interior-point update, checked against invariants the method must satisfy.
/// </summary>
/// <remarks>
/// The <c>.m</c> field files will check this against the original once <c>Main_Prog</c>
/// drives it. Until then the useful tests are the ones the physics fixes on its own: a
/// uniform stagnant pipe has to come back unchanged, and a pressure gradient has to push
/// gas the right way.
/// </remarks>
public sealed class CharacteristicSolverTests
{
    private const double Gamma = CharacteristicSolver.InletGamma;

    /// <summary>Time step for one crank degree at 4000 rpm, as Main_Prog computes it.</summary>
    private static double TimeStep(double rpm = 4000, double crankAngleStep = 1) =>
        1 / (rpm / 60 * 360) * crankAngleStep;

    private static PipeGeometry BaselineInletPipe()
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
        return new PipeGeometry(engine.Manifold.InletPipe.AreaVersusLength);
    }

    private static (PipeGrid Current, PipeGrid Next) Grids(PipeGeometry pipe, int points,
        double pressure, double temperature)
    {
        var current = new PipeGrid(EsaLimits.InletGridPoints);
        var next = new PipeGrid(EsaLimits.InletGridPoints);

        PipeGridInitialiser.Initialise(current, points, pipe.Length, pressure, temperature, Gamma);
        PipeGridInitialiser.Initialise(next, points, pipe.Length, pressure, temperature, Gamma);

        return (current, next);
    }

    [Fact]
    public void AUniformStagnantPipeIsPreservedExactly()
    {
        BaselinePaths.Require();

        var pipe = BaselineInletPipe();
        var (current, next) = Grids(pipe, 39, 99000, 298.15);

        // Deliberately not a constant-area pipe: the baseline inlet tapers. With the gas
        // at rest the area terms drop out of both source terms, so a correct scheme
        // returns the state untouched however the pipe is shaped. Any slip in the
        // characteristic bookkeeping shows up here as drift.
        for (var i = 1; i <= 37; i++)
        {
            CharacteristicSolver.UpdateInteriorPoint(current, next, pipe, Gamma, TimeStep(), i);
        }

        for (var i = 1; i <= 37; i++)
        {
            Assert.Equal(0, next.Velocity[i], 9);
            Assert.Equal(99000, next.Pressure[i], 6);
            Assert.Equal(current.Density[i], next.Density[i], 12);
            Assert.Equal(current.SpeedOfSound[i], next.SpeedOfSound[i], 9);
        }
    }

    [Fact]
    public void GasAcceleratesDownAPressureGradient()
    {
        BaselinePaths.Require();

        var pipe = BaselineInletPipe();
        var (current, next) = Grids(pipe, 39, 99000, 298.15);

        // Tilt the pressure down along the pipe, keeping the temperature uniform so the
        // density follows the pressure.
        for (var i = 0; i < 39; i++)
        {
            current.Pressure[i] = 101000 - (i * 100.0);
            current.Density[i] = current.Pressure[i] / 287 / 298.15;
        }

        CharacteristicSolver.UpdateInteriorPoint(current, next, pipe, Gamma, TimeStep(), 19);

        // High pressure behind, low pressure ahead: the gas must start moving forwards.
        Assert.True(next.Velocity[19] > 0, $"Velocity came out {next.Velocity[19]} m/s.");

        // And the pressure at the point stays between its two neighbours' starting values
        // rather than running off.
        Assert.InRange(next.Pressure[19], current.Pressure[20], current.Pressure[18]);
    }

    [Fact]
    public void TheStepRunsCloseToACourantNumberOfOne()
    {
        BaselinePaths.Require();

        var pipe = BaselineInletPipe();
        var spacing = pipe.Length / 38;
        var speedOfSound = Math.Sqrt(Gamma * 287 * 298.15);
        var courant = speedOfSound * TimeStep() / spacing;

        // Not an assertion about the original so much as a record of how the scheme is
        // being run. The grid-size expression is evidently tuned to put the characteristic
        // foot roughly one cell away at one crank degree: much below one and the scheme
        // wastes points, much above one and the linear interpolant is extrapolating.
        // Phase 4b should expect accuracy to depend on the crank-angle step through this.
        Assert.InRange(courant, 0.5, 1.5);
    }

    [Fact]
    public void AStagnantPipeStaysStagnantOverManySteps()
    {
        BaselinePaths.Require();

        var pipe = BaselineInletPipe();
        var (current, next) = Grids(pipe, 39, 99000, 298.15);

        // Repeated application must not accumulate drift, which a one-step test cannot
        // show. The boundaries are held fixed here because they belong to routines this
        // one knows nothing about.
        for (var step = 0; step < 50; step++)
        {
            for (var i = 1; i <= 37; i++)
            {
                CharacteristicSolver.UpdateInteriorPoint(current, next, pipe, Gamma, TimeStep(), i);
            }

            for (var i = 1; i <= 37; i++)
            {
                current.Velocity[i] = next.Velocity[i];
                current.Pressure[i] = next.Pressure[i];
                current.Density[i] = next.Density[i];
                current.SpeedOfSound[i] = next.SpeedOfSound[i];
            }
        }

        for (var i = 1; i <= 37; i++)
        {
            Assert.Equal(0, current.Velocity[i], 9);
            Assert.Equal(99000, current.Pressure[i], 5);
        }
    }

    [Fact]
    public void TheTwoPipesRunAtDifferentHardCodedGammas()
    {
        // INTERNAL_PIPE overwrites the gamma it is handed: 1.3994 for the inlet branch,
        // 1.3 for the exhaust one. The equilibrium-derived GammaIn and GammaEx that
        // InitVars computes are never read by anything. See ISSUES.md B50.
        Assert.Equal(1.3994, CharacteristicSolver.InletGamma);
        Assert.Equal(1.3, CharacteristicSolver.ExhaustGamma);
        Assert.NotEqual(CharacteristicSolver.InletGamma, CharacteristicSolver.ExhaustGamma);
    }
}
