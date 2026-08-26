using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// Turns a completed cycle's accumulators into the reported performance figures. Port of
/// <c>TEngine2z.Performance</c> and <c>TEngine2z.TFMEP</c> (ICEngine2Z.pas:933-936,
/// 1087-1116).
/// </summary>
/// <remarks>
/// Everything here reads the accumulators <c>CycleSolver</c> leaves on the engine at the
/// end of a cycle - indicated and pumping work, heat loss, and the mass totals through
/// each valve - and writes the results back onto the same engine, as the original did.
/// </remarks>
public sealed class PerformanceCalculator
{
    /// <summary>
    /// Total friction mean effective pressure in pascals. Port of
    /// <c>TEngine2z.TFMEP</c>: an empirical quadratic in engine speed, and the only
    /// friction model in the program.
    /// </summary>
    public static double TotalFmep(double rpm) =>
        1.0e5 * (0.97 + (0.15 * rpm / 1000) + (0.05 * (rpm / 1000) * (rpm / 1000)));

    /// <summary>Computes every reported figure and stores it on <paramref name="engine"/>.</summary>
    public void Calculate(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var fuel = engine.Cylinder.Fuel;
        var sweptVolume = engine.SweptVolume;

        engine.TotalMass = engine.Cylinder.MGas;

        // Recomputed here from the final total, not carried over from the cycle.
        fuel.M = 1 / fuel.Lambda * engine.TotalMassInInletValve / (fuel.AFRatio + 1);

        engine.ResidualFraction = (1 - (engine.TotalMassInInletValve / engine.TotalMass)) * 100;

        engine.Imep = engine.Work / sweptVolume;
        engine.Pmep = engine.PumpingWork / sweptVolume;

        // FMEP is the friction correlation minus PMEP, and BMEP then subtracts PMEP
        // again, so the two cancel and BMEP is really IMEP - TFMEP. The reported FMEP is
        // therefore the intermediate rather than the correlation. See ISSUES.md B2.
        engine.Fmep = TotalFmep(engine.Rpm) - engine.Pmep;
        engine.Bmep = engine.Imep - engine.Pmep - engine.Fmep;

        engine.Torque = engine.Bmep * sweptVolume * engine.CylinderCount / (2 * 2 * Math.PI);
        engine.BrakePower = engine.Torque * engine.Rpm * 2 * Math.PI / 60;

        engine.IndicatedPower = engine.Imep * sweptVolume * engine.CylinderCount / (4 * Math.PI)
                                * engine.Rpm * 2 * Math.PI / 60;

        engine.HeatPower = engine.HeatLoss * engine.CylinderCount / (4 * Math.PI)
                           * engine.Rpm * 2 * Math.PI / 60;

        engine.VolumetricEfficiency = engine.TotalMassInInletValve / engine.AtmosphericMass * 100;
        engine.MechanicalEfficiency = engine.Bmep / engine.Imep * 100;

        // Both of these hard-code four cylinders: the factor is 2 * Nrpm where the
        // physics wants NCyl * Nrpm / 2, and the two agree only at NCyl = 4. Every
        // shipped engine is a four-cylinder, so the original never exercised it.
        // Ported verbatim to stay in agreement with data/baseline/. See ISSUES.md B1.
        engine.FuelMassFlow = fuel.M * 2 * engine.Rpm * 60;
        engine.Sfc = engine.FuelMassFlow * 1000 / engine.BrakePower * 1000;
        engine.ThermalEfficiency =
            engine.BrakePower / (fuel.Q * fuel.M * 2 * engine.Rpm / 60) * 100;

        CalculateEnergyBalance(engine);
    }

    /// <summary>
    /// The five energy-balance percentages. Each is a share of the fuel energy admitted
    /// in one cycle, and the exhaust term is whatever the other four leave over.
    /// </summary>
    private static void CalculateEnergyBalance(Engine engine)
    {
        var fuel = engine.Cylinder.Fuel;
        var fuelEnergy = fuel.Q * fuel.M;

        var heat = -engine.HeatLoss;
        var work = engine.Bmep * engine.SweptVolume;
        var pumping = engine.PumpingWork;
        var friction = engine.Fmep * engine.SweptVolume;

        engine.QFuel = fuelEnergy;
        engine.QExhaust = (fuelEnergy - heat - work - pumping - friction) / fuelEnergy * 100;
        engine.QWork = work / fuelEnergy * 100;
        engine.QHeat = heat / fuelEnergy * 100;
        engine.QPump = pumping / fuelEnergy * 100;
        engine.QFriction = friction / fuelEnergy * 100;
    }
}
