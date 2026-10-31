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
    /// <param name="engine">The engine whose completed cycle to report.</param>
    /// <param name="physics">Which corrections apply (<c>CORRECTIONS.md</c>). Null is Legacy.</param>
    public void Calculate(Engine engine, PhysicsCorrections? physics = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var fuel = engine.Cylinder.Fuel;
        var sweptVolume = engine.SweptVolume;

        engine.TotalMass = engine.Cylinder.MGas;

        // Recomputed here from the final total, not carried over from the cycle.
        fuel.M = FuelMixture.FuelMass(
            fuel, engine.TotalMassInInletValve, physics?.IsOn(CorrectionCatalogue.GasolineFuel) ?? false);

        engine.ResidualFraction = (1 - (engine.TotalMassInInletValve / engine.TotalMass)) * 100;

        engine.Imep = engine.Work / sweptVolume;
        engine.Pmep = engine.PumpingWork / sweptVolume;

        // FMEP is the friction correlation minus PMEP, and BMEP then subtracts PMEP
        // again, so the two cancel and BMEP is really IMEP - TFMEP. The reported FMEP is
        // therefore the intermediate rather than the correlation. See ISSUES.md B2.
        //
        // IMEP is the work from inlet closing to exhaust opening and PMEP the rest
        // (CycleSolver.AccumulateWorkAndHeat), so the original's torque counts the work in
        // that window only, and moving a valve event moves work in or out of torque though
        // the gas does the same work. Under B87 BMEP is the whole cycle's net work, IMEP
        // less PMEP, less the friction correlation, which no window divides; IMEP and PMEP
        // are still reported as the original splits them. See ISSUES.md B87.
        //
        // Under B88 friction is not that curve, which as a motored total carries pumping of
        // its own, but the engine's mechanical and auxiliary friction from its design, set
        // against the same net work, so pumping is the simulated cycle's alone. See
        // ISSUES.md B88.
        if (physics?.IsOn(CorrectionCatalogue.MechanicalFriction) ?? false)
        {
            engine.Fmep = FrictionModel.Calculate(
                    FrictionGeometry.FromEngine(engine), engine.Rpm, engine.Plenum.PGas / engine.Atmosphere.PGas)
                .Total;
            engine.Bmep = engine.Imep - engine.Pmep - engine.Fmep;
        }
        else if (physics?.IsOn(CorrectionCatalogue.NetWorkTorque) ?? false)
        {
            engine.Fmep = TotalFmep(engine.Rpm);
            engine.Bmep = engine.Imep - engine.Pmep - engine.Fmep;
        }
        else
        {
            engine.Fmep = TotalFmep(engine.Rpm) - engine.Pmep;
            engine.Bmep = engine.Imep - engine.Pmep - engine.Fmep;
        }

        engine.Torque = engine.Bmep * sweptVolume * engine.CylinderCount / (2 * 2 * Math.PI);
        engine.BrakePower = engine.Torque * engine.Rpm * 2 * Math.PI / 60;

        engine.IndicatedPower = engine.Imep * sweptVolume * engine.CylinderCount / (4 * Math.PI)
                                * engine.Rpm * 2 * Math.PI / 60;

        engine.HeatPower = engine.HeatLoss * engine.CylinderCount / (4 * Math.PI)
                           * engine.Rpm * 2 * Math.PI / 60;

        engine.VolumetricEfficiency = engine.TotalMassInInletValve / engine.AtmosphericMass * 100;
        engine.MechanicalEfficiency = engine.Bmep / engine.Imep * 100;

        // A four-stroke cylinder fires every other revolution, so the engine admits fuel
        // NCyl * Nrpm / 2 times a minute. The original wrote 2 * Nrpm, which is the same
        // only at four cylinders - every shipped engine, so it was never exercised - and
        // left fuel flow, SFC and thermal efficiency wrong by 4 / NCyl for any other
        // engine (ISSUES.md B1). The factor is kept as one term so that at four cylinders
        // it is exactly 2.0 and the arithmetic is the original's to the last bit.
        var firingsPerRevolution = engine.CylinderCount / 2;

        engine.FuelMassFlow = fuel.M * firingsPerRevolution * engine.Rpm * 60;
        engine.Sfc = engine.FuelMassFlow * 1000 / engine.BrakePower * 1000;
        engine.ThermalEfficiency =
            engine.BrakePower / (fuel.Q * fuel.M * firingsPerRevolution * engine.Rpm / 60) * 100;

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
