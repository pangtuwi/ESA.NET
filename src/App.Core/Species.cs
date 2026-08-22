namespace App.Core;

/// <summary>
/// The twelve equilibrium species in Olikara and Borman order, matching the
/// 1-based <c>EqSpecArray</c> indices used throughout Eqbm.pas.
/// </summary>
public enum Species
{
    H = 1,
    O = 2,
    N = 3,
    H2 = 4,
    OH = 5,
    CO = 6,
    NO = 7,
    O2 = 8,
    H2O = 9,
    CO2 = 10,
    N2 = 11,
    Ar = 12,
}
