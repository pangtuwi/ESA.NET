namespace App.Core.Simulation;

/// <summary>
/// The combustion efficiency of a spark-ignition engine: the share of the fuel's energy the
/// burn releases. No Delphi counterpart (ISSUES.md B92).
/// </summary>
/// <remarks>
/// <para>
/// J. B. Heywood, <i>Internal Combustion Engine Fundamentals</i>, McGraw-Hill, 1988,
/// section 3.5.5 and Figure 3-9 (p. 82): "for spark-ignition engines, for lean equivalence
/// ratios, the combustion efficiency is usually in the range 95 to 98 percent. For mixtures
/// richer than stoichiometric, lack of oxygen prevents complete combustion of the fuel carbon
/// and hydrogen, and the combustion efficiency steadily decreases as the mixture becomes
/// richer. Combustion efficiency is little affected by other engine operating and design
/// variables". Section 4.9.4 (p. 154, eq. 4.69) counts the shortfall as the heating value of
/// the CO, H2, unburnt hydrocarbons and particulates in the exhaust.
/// </para>
/// <para>
/// The rich-side fall is already in the model, whose burnt gas is in equilibrium and carries
/// the CO and H2 that lack of oxygen leaves. What it does not have is the few per cent that
/// escape even with oxygen to spare: unburnt hydrocarbons from crevices, quench layers and
/// absorption, and CO and H2 out of equilibrium. That shortfall is the 2 to 5 per cent of the
/// lean range, taken at its midpoint and at every equivalence ratio.
/// </para>
/// </remarks>
public static class CombustionLoss
{
    /// <summary>
    /// The midpoint of Heywood's 95 to 98 per cent, the default where the <c>.eng</c> gives no
    /// <c>[Fuel] CombustionEfficiency</c>.
    /// </summary>
    public const double DefaultEfficiency = 0.965;
}
