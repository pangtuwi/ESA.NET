using App.Core;
using App.Core.Model;
using App.Core.Simulation;

namespace App.Tests;

/// <summary>
/// Pins the ODE stepper against the Delphi original, including the defect it carries.
/// </summary>
public sealed class Rkf5IntegratorTests
{
    private static IntegratorState State(int equations, double x, double dx, params double[] y)
    {
        var state = new IntegratorState { EquationCount = equations, X = x, Dx = dx };

        for (var i = 0; i < y.Length; i++)
        {
            state.Y[i] = y[i];
        }

        return state;
    }

    /// <summary>Integrates from <paramref name="x0"/> to 1 in <paramref name="steps"/> steps.</summary>
    private static double Integrate(DerivativeFunction f, double y0, double x0, int steps, Integrator method)
    {
        var integrator = new Rkf5Integrator();
        var h = (1.0 - x0) / steps;
        var state = State(1, x0, h, y0);
        state.Integrator = method;

        for (var i = 0; i < steps; i++)
        {
            integrator.Step(state, [f]);
            state.X += h;
        }

        return state.Y[0];
    }

    // ---------------------------------------------------------------------------
    // The transposed coefficient
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A consistent Runge-Kutta stage evaluates at a state whose coefficients sum to
    /// the stage's node. Four of the five rows do. The fifth does not, because
    /// RKf5.pas line 76 reads <c>854/4104</c> where Fehlberg published
    /// <c>845/4104</c> — a transposed digit.
    /// </summary>
    /// <remarks>
    /// Reproduced deliberately: the reference run in <c>data/baseline/</c> was produced
    /// by it. This test exists so nobody corrects it by accident. See ISSUES.md B14.
    /// </remarks>
    [Fact]
    public void TheFifthStageRowDoesNotSumToItsNode()
    {
        // Recovered by feeding the integrator a derivative that reports the state it
        // was handed, so the trial vectors it builds can be read back out.
        var seen = new List<double>();
        var integrator = new Rkf5Integrator();

        // f(x,y) = 1 makes every k equal to dx, so each trial value is dx times that
        // row's coefficient sum, plus y.
        var state = State(1, 0, 1, 0);
        integrator.Step(state, [(_, y) => { seen.Add(y[0]); return 1; }]);

        // Six stages were evaluated, at y = 0 then the five row sums.
        Assert.Equal(6, seen.Count);
        Assert.Equal(0, seen[0]);
        Assert.Equal(1.0 / 4.0, seen[1], 12);
        Assert.Equal(3.0 / 8.0, seen[2], 12);
        Assert.Equal(12.0 / 13.0, seen[3], 12);

        // The fifth row: 455/456, not the 1 that consistency requires.
        Assert.Equal(455.0 / 456.0, seen[4], 12);
        Assert.NotEqual(1.0, seen[4], 6);

        Assert.Equal(1.0 / 2.0, seen[5], 12);
    }

    /// <summary>
    /// The consequence: the method converges at first order, not fifth. ESA offers this
    /// to the user as "Runga Kutte Felberg (accurate)" against "Euler (fast)", and in
    /// convergence terms it is no better than Euler.
    /// </summary>
    [Fact]
    public void TheTransposedCoefficientCollapsesTheMethodToFirstOrder()
    {
        // dy/dx = y from 0 to 1, y(0) = 1, so the exact answer is e.
        static double Exponential(double x, ReadOnlySpan<double> y) => y[0];

        var coarse = Math.Abs(Integrate(Exponential, 1, 0, 20, Integrator.Rkf5) - Math.E);
        var fine = Math.Abs(Integrate(Exponential, 1, 0, 40, Integrator.Rkf5) - Math.E);

        var observedOrder = Math.Log2(coarse / fine);

        // Halving the step halves the error: first order. A true RKF5 would divide it
        // by about 32. If this test starts reporting ~5, someone has "fixed" the
        // coefficient and the port no longer matches its own reference data.
        Assert.InRange(observedOrder, 0.9, 1.2);
    }

    // ---------------------------------------------------------------------------
    // Guards against the C# integer-division trap
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Delphi's <c>/</c> is always real division, so <c>1932/2197</c> is a fraction.
    /// In C# the same literals would be integer division and evaluate to zero. The
    /// weights summing to one is the cheapest way to catch that having happened.
    /// </summary>
    [Fact]
    public void TheFifthOrderWeightsSumToOne()
    {
        // With f(x,y) = 1 every k equals dx, so a unit step advances y by the sum of
        // the weights.
        var state = State(1, 0, 1, 0);
        new Rkf5Integrator().Step(state, [(_, _) => 1]);

        Assert.Equal(1.0, state.Y[0], 12);
    }

    [Fact]
    public void ConstantDerivativesAdvanceTheStateExactly()
    {
        var state = State(2, 0, 0.5, 3, 7);
        new Rkf5Integrator().Step(state, [(_, _) => 2, (_, _) => -4]);

        Assert.Equal(3 + (2 * 0.5), state.Y[0], 12);
        Assert.Equal(7 - (4 * 0.5), state.Y[1], 12);
    }

    // ---------------------------------------------------------------------------
    // Behaviour shared with the original
    // ---------------------------------------------------------------------------

    [Fact]
    public void SteppingDoesNotAdvanceTheIndependentVariable()
    {
        // TRKF.IntegrateRKF never touches x; TEngine2z.Run sets it from the crank angle.
        var state = State(1, 2.5, 0.1, 1);
        new Rkf5Integrator().Step(state, [(_, y) => y[0]]);

        Assert.Equal(2.5, state.X);
    }

    [Fact]
    public void EquationsBeyondTheActiveCountAreUntouched()
    {
        var state = State(2, 0, 1, 1, 1, 99, 99);
        new Rkf5Integrator().Step(state, [(_, _) => 1, (_, _) => 1, (_, _) => 1, (_, _) => 1]);

        Assert.Equal(99, state.Y[2]);
        Assert.Equal(99, state.Y[3]);
    }

    [Fact]
    public void EulerUsesTheUnchangedStateForEveryDerivative()
    {
        // y1' = y2, y2' = y1. A sequential update would feed the new y1 into y2's
        // derivative; the original fills a temporary and assigns it wholesale.
        var state = State(2, 0, 1, 1, 2);
        state.Integrator = Integrator.Euler;

        new Rkf5Integrator().Step(state, [(_, y) => y[1], (_, y) => y[0]]);

        Assert.Equal(1 + 2, state.Y[0], 12);
        Assert.Equal(2 + 1, state.Y[1], 12);
    }

    [Fact]
    public void EulerConvergesAtFirstOrder()
    {
        static double Exponential(double x, ReadOnlySpan<double> y) => y[0];

        var coarse = Math.Abs(Integrate(Exponential, 1, 0, 20, Integrator.Euler) - Math.E);
        var fine = Math.Abs(Integrate(Exponential, 1, 0, 40, Integrator.Euler) - Math.E);

        Assert.InRange(Math.Log2(coarse / fine), 0.9, 1.2);
    }

    [Fact]
    public void AnythingThatIsNotRkf5FallsThroughToEuler()
    {
        // The original tests "if Integrator = 0 ... else Euler", so an out-of-range
        // value is Euler rather than an error.
        var rkf5 = State(1, 0, 1, 1);
        var strange = State(1, 0, 1, 1);
        strange.Integrator = (Integrator)7;

        var euler = State(1, 0, 1, 1);
        euler.Integrator = Integrator.Euler;

        var integrator = new Rkf5Integrator();
        integrator.Step(rkf5, [(_, y) => y[0]]);
        integrator.Step(strange, [(_, y) => y[0]]);
        integrator.Step(euler, [(_, y) => y[0]]);

        Assert.Equal(euler.Y[0], strange.Y[0]);
        Assert.NotEqual(rkf5.Y[0], strange.Y[0]);
    }

    // ---------------------------------------------------------------------------
    // Argument checking
    // ---------------------------------------------------------------------------

    [Fact]
    public void TooManyEquationsIsRejected()
    {
        var state = new IntegratorState { EquationCount = EsaLimits.MaxEquations + 1, Dx = 1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Rkf5Integrator().Step(state, [(_, _) => 1]));
    }

    [Fact]
    public void TooFewDerivativesIsRejected()
    {
        var state = State(3, 0, 1, 1, 1, 1);

        var error = Assert.Throws<ArgumentException>(
            () => new Rkf5Integrator().Step(state, [(_, _) => 1]));

        Assert.Contains("derivative functions", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIntegratorCanBeReusedAcrossSteps()
    {
        // The stage buffers are shared between calls, so a stale k must not leak.
        var integrator = new Rkf5Integrator();
        var repeated = State(1, 0, 0.25, 1);
        var fresh = State(1, 0, 0.25, 1);

        integrator.Step(repeated, [(_, y) => y[0]]);
        integrator.Step(repeated, [(_, y) => y[0]]);

        new Rkf5Integrator().Step(fresh, [(_, y) => y[0]]);
        new Rkf5Integrator().Step(fresh, [(_, y) => y[0]]);

        Assert.Equal(fresh.Y[0], repeated.Y[0]);
    }
}
