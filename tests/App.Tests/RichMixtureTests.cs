using App.Core;
using App.Core.Expressions;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Rich mixtures, which every full-load spark-ignition engine runs: the thesis's
/// dynamometer logs show lambda 0.88 to 0.93. Below lambda 0.98 every run on the
/// baseline engine stopped at its first step with "Matrixsolver Returned Insufficient
/// Resolution", in Legacy and Corrected alike. ISSUES.md A30.
/// </summary>
/// <remarks>
/// The failing solve was the burnt zone's equilibrium at the 1000 K floor, phi 1/0.92,
/// 8.3 bar, where oxygen is 4e-20 of the gas. The Jacobian's oxygen column scales as
/// <c>1/sqrt(x[8])</c> and reaches 1e19, and the unpivoted elimination needs more than
/// double's 53 bits to resolve the pressure and equivalence-ratio derivatives against
/// it. The original ran it in 80-bit <c>Extended</c>, which clears the test.
/// </remarks>
public sealed class RichMixtureTests
{
    private const double FailingPressure = 834541.7838658673;

    /// <summary>
    /// The Jacobian and pressure right-hand side of the solve that stopped the run, as the
    /// solver built them at phi 1/0.92, 1000 K and <see cref="FailingPressure"/>.
    /// </summary>
    private static readonly double[] CapturedMatrix =
    [
        15.060687497933444, -25.326508312508274, -1.4740918873136026E+17, 0,
        6.530343748925597, -10.981623864341504, 3.0865966407925146E+17, 1.0173714973403882E-14,
        0, -114.94835062586456, -1.6390082174218099E+19, 2.000000000020725,
        7.5303437490467, 11.113182459852235, 3.3164941151197225E+18, 1.000000000020725,
    ];

    private static readonly double[] CapturedPressureRhs =
        [0.0006983595792178082, -0.001462293055435904, 0.07764896464538724, -0.015712083167892146];

    private static double[,] Matrix()
    {
        var matrix = new double[4, 4];

        for (var i = 0; i < 16; i++)
        {
            matrix[i / 4, i % 4] = CapturedMatrix[i];
        }

        return matrix;
    }

    /// <summary>
    /// The arithmetic itself. Double resolves the captured system to three places, which
    /// the solver treats as fatal; the wider reduction resolves it and gets the answer an
    /// exact rational solve gives.
    /// </summary>
    /// <remarks>
    /// The expected solution is from replaying <c>GaussReduce</c>'s elimination in exact
    /// rationals rounded to a 300-bit significand after every operation. The same replay at
    /// 53 bits reproduces the port's double result digit for digit, residual 1.1e-4 and
    /// resolution 3, and at 64 bits, the original's <c>Extended</c>, gives resolution 7.
    /// </remarks>
    [Fact]
    public void DoubleCannotResolveTheRichJacobianAndTheWiderReductionCan()
    {
        var inDouble = (double[])CapturedPressureRhs.Clone();
        Assert.Equal(3, DelphiNumerics.GaussReduce(Matrix(), inDouble));

        var wider = (double[])CapturedPressureRhs.Clone();
        Assert.True(DelphiNumerics.GaussReduceExtended(Matrix(), wider) >= 5);

        double[] exact = [9.604570501e-14, 4.242763917e-14, -4.737557983e-21, 1.661686095e-12];

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(1.0, wider[i] / exact[i], 1e-8);
        }

        // And the double answer was not merely short of the test: its hydrogen derivative
        // has the wrong sign and is three thousand times too large.
        Assert.True(inDouble[0] < 0 && Math.Abs(inDouble[0] / exact[0]) > 1000);
    }

    /// <summary>
    /// A system double does resolve is answered exactly as before: the wider reduction is
    /// only ever a fallback, which is why the baseline is bit-identical.
    /// </summary>
    [Fact]
    public void TheSolverOnlyWidensWhatDoubleCannotResolve()
    {
        var stoichiometric = new EquilibriumSolver();
        stoichiometric.Solve(1.0, 7, 17, 0, 0, FailingPressure, 1000);
        Assert.Equal(0, stoichiometric.Diagnostics.ExtendedPrecisionReductions);

        var rich = new EquilibriumSolver();
        rich.Solve(1 / 0.92, 7, 17, 0, 0, FailingPressure, 1000);
        Assert.True(rich.Diagnostics.ExtendedPrecisionReductions > 0);
        Assert.Equal(0, rich.Diagnostics.ResolutionErrors);
    }

    /// <summary>
    /// The pressure derivatives the wider reduction returns at the failing state agree with
    /// a finite difference of the solver. Hydrogen, carbon monoxide and the other majors
    /// move by less than the Newton loop's own tolerance there, so only the species whose
    /// derivative stands clear of that noise are compared.
    /// </summary>
    [Theory]
    [InlineData(Species.O2)]
    [InlineData(Species.N2)]
    [InlineData(Species.OH)]
    [InlineData(Species.H)]
    public void TheRichPressureDerivativesMatchAFiniteDifference(Species species)
    {
        const double Atmosphere = 101325.0;
        const double Step = 0.01 * FailingPressure;

        static EquilibriumSolver Solved(double pressure)
        {
            var solver = new EquilibriumSolver();
            solver.Solve(1 / 0.92, 7, 17, 0, 0, pressure, 1000);
            return solver;
        }

        var analytic = Solved(FailingPressure).State.DxDp[species];
        var numeric = (Solved(FailingPressure + Step).State.X[species] - Solved(FailingPressure - Step).State.X[species])
                      / (2 * Step) * Atmosphere;

        Assert.Equal(1.0, analytic / numeric, 0.01);
    }

    /// <summary>
    /// The case that was reported: the baseline engine at lambda 0.92 and 4000 rpm, in
    /// both modes. It used to stop at the first step of the first cycle.
    /// </summary>
    [Theory]
    [InlineData(PhysicsMode.Legacy)]
    [InlineData(PhysicsMode.Corrected)]
    public void TheBaselineEngineRunsRich(PhysicsMode mode)
    {
        BaselinePaths.Require();

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

        // The loader copies the fuel into every gas, as Edit.pas does, so all four change.
        foreach (var gas in (Gas[])[engine.Plenum, engine.Cylinder, engine.Exhaust, engine.Atmosphere])
        {
            gas.Fuel.Lambda = 0.92;
        }

        var settings = new SimulationSettings
        {
            CycleCount = 8,
            OneZoneCycleCount = 1,
            MassBalance = 1,
            Physics = new PhysicsCorrections { Mode = mode },
        };

        var result = new SimulationRunner(new CachingExpressionEvaluator())
            .Run(engine, settings, cancellation: TestContext.Current.CancellationToken);

        var diagnostics = result.Diagnostics!;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{mode}: {result.Engine.Torque:F2} Nm, {diagnostics.EquilibriumExtendedPrecisionReductions} widened reductions of {diagnostics.EquilibriumSolves} solves");

        // Measured at 152.8 Nm in Legacy and 163.9 in Corrected, against 151.8 and 163.3 at
        // lambda 1: a little more torque for a little more fuel, as an engine gives.
        Assert.InRange(result.Engine.Torque, 145, 175);
        Assert.True(diagnostics.EquilibriumExtendedPrecisionReductions > 0);
    }
}
