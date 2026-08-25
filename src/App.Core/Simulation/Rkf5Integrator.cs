using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// The ODE stepper. Port of Delphi <c>TRKF</c> (RKf5.pas).
/// </summary>
/// <remarks>
/// <para>
/// Despite the name this is a <b>fixed-step</b> method. It evaluates the six Fehlberg
/// stages and takes the fifth-order step; there is no error estimate, no comparison
/// against the embedded fourth-order solution and no adaptive step control. The step
/// is whatever <see cref="IntegratorState.Dx"/> says. Do not "improve" it into an
/// adaptive solver — every number in <c>data/baseline/</c> was produced this way.
/// </para>
/// <para>
/// <see cref="Step"/> advances <see cref="IntegratorState.Y"/> only. It never touches
/// <see cref="IntegratorState.X"/>: the caller owns the independent variable, as
/// <c>TEngine2z.Run</c> does when it sets <c>x := CA * Pi / 180</c> each step.
/// </para>
/// </remarks>
public sealed class Rkf5Integrator
{
    /// <summary>The six stage vectors, <c>k</c> in the original. Reused across steps.</summary>
    private readonly double[,] _k = new double[6, EsaLimits.MaxEquations];

    /// <summary>The trial state each stage is evaluated at, <c>ytemp</c> in the original.</summary>
    private readonly double[] _trial = new double[EsaLimits.MaxEquations];

    /// <summary>Holds the Euler update until every derivative has been evaluated.</summary>
    private readonly double[] _next = new double[EsaLimits.MaxEquations];

    /// <summary>
    /// Advances the state by one step using whichever method
    /// <see cref="IntegratorState.Integrator"/> selects.
    /// </summary>
    /// <remarks>
    /// The original tests <c>if Integrator = 0 then IntegrateRKF else IntegrateEuler</c>,
    /// so anything that is not RKF5 falls through to Euler.
    /// </remarks>
    public void Step(IntegratorState state, IReadOnlyList<DerivativeFunction> derivatives)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(derivatives);

        if (state.EquationCount < 0 || state.EquationCount > EsaLimits.MaxEquations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.EquationCount,
                $"The system must have between 0 and {EsaLimits.MaxEquations} equations.");
        }

        if (derivatives.Count < state.EquationCount)
        {
            throw new ArgumentException(
                $"{state.EquationCount} equations need {state.EquationCount} derivative functions, "
                + $"but {derivatives.Count} were supplied.",
                nameof(derivatives));
        }

        if (state.Integrator == Integrator.Rkf5)
        {
            StepRkf5(state, derivatives);
        }
        else
        {
            StepEuler(state, derivatives);
        }
    }

    /// <summary>Port of <c>TRKF.IntegrateRKF</c>.</summary>
    /// <remarks>
    /// The trial vector is rebuilt inside each stage's equation loop, exactly as the
    /// original does. That is redundant — it depends only on completed stages, so every
    /// pass produces identical values — but it is kept so the structure matches the
    /// source it was read from. With four equations the cost is negligible.
    /// </remarks>
    private void StepRkf5(IntegratorState state, IReadOnlyList<DerivativeFunction> derivatives)
    {
        var n = state.EquationCount;
        var y = state.Y;
        var x = state.X;
        var dx = state.Dx;

        // Stage 1 is evaluated at the current state, so it needs no trial vector.
        for (var i = 0; i < n; i++)
        {
            _k[0, i] = dx * derivatives[i](x, y.AsSpan(0, n));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                _trial[j] = y[j] + _k[0, j] / 4;
            }

            _k[1, i] = dx * derivatives[i](x + dx / 4, _trial.AsSpan(0, n));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                _trial[j] = y[j] + (3.0 / 32.0 * _k[0, j]) + (9.0 / 32.0 * _k[1, j]);
            }

            _k[2, i] = dx * derivatives[i](x + (3.0 / 8.0 * dx), _trial.AsSpan(0, n));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                _trial[j] = y[j]
                            + (1932.0 / 2197.0 * _k[0, j])
                            - (7200.0 / 2197.0 * _k[1, j])
                            + (7296.0 / 2197.0 * _k[2, j]);
            }

            _k[3, i] = dx * derivatives[i](x + (12.0 / 13.0 * dx), _trial.AsSpan(0, n));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                // 854/4104 is not a typo here: it is the typo the original carries.
                // Fehlberg's published coefficient is 845/4104, which makes this row
                // sum to its node of 1. As written the row sums to 455/456, so the
                // stage is evaluated at a state inconsistent with the point x + dx.
                // Reproduced deliberately — the baseline was produced by it.
                // See ISSUES.md B14.
                _trial[j] = y[j]
                            + (439.0 / 216.0 * _k[0, j])
                            - (8.0 * _k[1, j])
                            + (3680.0 / 513.0 * _k[2, j])
                            - (854.0 / 4104.0 * _k[3, j]);
            }

            _k[4, i] = dx * derivatives[i](x + dx, _trial.AsSpan(0, n));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                _trial[j] = y[j]
                            - (8.0 / 27.0 * _k[0, j])
                            + (2.0 * _k[1, j])
                            - (3544.0 / 2565.0 * _k[2, j])
                            + (1859.0 / 4104.0 * _k[3, j])
                            - (11.0 / 40.0 * _k[4, j]);
            }

            _k[5, i] = dx * derivatives[i](x + (dx / 2), _trial.AsSpan(0, n));
        }

        // The fifth-order weights. Stage 2 carries a weight of zero, so it is computed
        // and then never used — as in the original. The bracket matches the source, so
        // the five weighted terms are summed before being added to y.
        for (var i = 0; i < n; i++)
        {
            y[i] += (16.0 / 135.0 * _k[0, i])
                    + (6656.0 / 12825.0 * _k[2, i])
                    + (28561.0 / 56430.0 * _k[3, i])
                    - (9.0 / 50.0 * _k[4, i])
                    + (2.0 / 55.0 * _k[5, i]);
        }
    }

    /// <summary>Port of <c>TRKF.IntegrateEuler</c>, the quick alternative.</summary>
    /// <remarks>
    /// Every derivative is evaluated against the unchanged state before any of the
    /// state is replaced, which the original achieves by filling <c>ytemp</c> and then
    /// assigning it wholesale.
    /// </remarks>
    private void StepEuler(IntegratorState state, IReadOnlyList<DerivativeFunction> derivatives)
    {
        var n = state.EquationCount;
        var y = state.Y;

        for (var i = 0; i < n; i++)
        {
            _next[i] = y[i] + (state.Dx * derivatives[i](state.X, y.AsSpan(0, n)));
        }

        for (var i = 0; i < n; i++)
        {
            y[i] = _next[i];
        }
    }
}
