using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// The fuel in a charge, and the equivalence ratio the chemistry burns it at. No Delphi
/// counterpart: ISSUES.md B95.
/// </summary>
/// <remarks>
/// The original counts the fuel as <c>(1/lambda) m / (AFRatio + 1)</c> on the declared air-fuel
/// ratio, while its chemistry burns the fuel's own formula at an equivalence ratio of
/// <c>1/lambda</c>, against the formula's stoichiometric ratio. For the C7H17 surrogate that
/// ratio is 15.4 where every engine declares 14.5, so the chemistry burnt 5.6 per cent less fuel
/// per kilogram of air than the fuel energy, efficiency and specific consumption counted. Under
/// B95 both take the declared ratio: the fuel is the exact share of a mixture at
/// <c>lambda AFRatio</c> of air to fuel, and the chemistry burns its formula at the equivalence
/// ratio that gives that share.
/// </remarks>
public static class FuelMixture
{
    /// <summary>
    /// The fuel in <paramref name="mixtureMass"/> of fresh charge: the original's
    /// <c>(1/lambda) m / (AFRatio + 1)</c>, or under B95 the exact <c>m / (1 + lambda AFRatio)</c>.
    /// The two agree at lambda 1.
    /// </summary>
    public static double FuelMass(Fuel fuel, double mixtureMass, bool exact)
    {
        ArgumentNullException.ThrowIfNull(fuel);

        return exact
            ? mixtureMass / (1 + (fuel.Lambda * fuel.AFRatio))
            : 1 / fuel.Lambda * mixtureMass / (fuel.AFRatio + 1);
    }

    /// <summary>
    /// The equivalence ratio at which the chemistry burns a formula whose stoichiometric
    /// air-fuel ratio is <paramref name="formulaRatio"/>, so that its fuel per kilogram of air is
    /// the declared mixture's, <c>1 / (lambda AFRatio)</c>. The original's <c>1/lambda</c> when
    /// the engine declares no ratio.
    /// </summary>
    public static double EquivalenceRatio(Fuel fuel, double formulaRatio)
    {
        ArgumentNullException.ThrowIfNull(fuel);

        return fuel.AFRatio > 0
            ? formulaRatio / (fuel.Lambda * fuel.AFRatio)
            : 1 / fuel.Lambda;
    }
}
