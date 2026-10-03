using App.Core;
using App.Core.Model;
using App.Core.Simulation;
using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// Checks the Woschni heat-transfer chain against the baseline trace.
/// </summary>
/// <remarks>
/// <para>
/// This is a stronger check than it first looks. The trace records <c>Qb</c> and
/// <c>Qu</c> as the per-step heat loss in joules, computed at the end of
/// <c>TEngine2z.Run</c> from the cylinder state that the same row also records - so
/// pressure, both zone temperatures, both zone volumes and the total volume can all be
/// replayed from the row itself. Nothing has to be integrated to get here, and every
/// piece of the correlation is exercised: the state-dependent C1, the motored pressure,
/// the mis-scaled swept volume, the four wall-temperature lookups, the liner blend and
/// the three state switches in each integral.
/// </para>
/// <para>
/// Baseline engine, 4000 rpm, 1 degree steps. The reference conditions are the ones
/// InitVars fixes once and never revises - see ISSUES.md B38.
/// </para>
/// </remarks>
public sealed class CylinderHeatTransferTests
{
    private const double Rpm = 4000;
    private const double DegreeStep = 1.0;

    // A2China at 4000 rpm. ThetaSpark is -SparkAngle.GetVal(4000) and A2ChinaVar.spk
    // gives 21 at 4000; the valve angles are converted as EngineFileUnits converts them.
    private static readonly CrankAngleStateMap States = new(
        inletOpen: 360 - 19,
        inletClose: -180 + 80,
        exhaustOpen: 180 - 64,
        exhaustClose: -360 + 37,
        sparkAngle: -21,
        burnAngle: 55);

    private static CylinderModel Model(
        bool motoredAtCallAngle = false,
        bool trueSweptVolume = false,
        bool clampPressureRise = false,
        bool pressureRiseInCombustionOnly = false)
    {
        var geometry = new CylinderGeometry(
            bore: 0.081, stroke: 0.0774, compressionRatio: 9.2,
            conrodLength: 0.149, cylinderCount: 4);

        // A2China.cwt, verbatim.
        var walls = new WallTemperatureTable();
        walls.Rpm.AddRange([1000, 2000, 3000, 4000, 5000, 6000, 7000]);
        walls.HeadTemperature.AddRange([350, 365, 380, 400, 415, 435, 460]);
        walls.PistonTemperature.AddRange([440, 450, 470, 490, 510, 530, 550]);
        walls.UpperLinerTemperature.AddRange([495, 405, 425, 445, 460, 480, 505]);
        walls.LowerLinerTemperature.AddRange([350, 365, 380, 400, 415, 435, 460]);

        return new CylinderModel(geometry, new TwoZoneGas(), new TwoZoneGas(), walls)
        {
            Rpm = Rpm,
            CrankAngularVelocity = Rpm * Math.PI / 30,
            WoschniCoefficient = 150,
            MotoredVolumeAtCallAngle = motoredAtCallAngle,
            TrueSweptVolume = trueSweptVolume,
            ClampPressureRiseTerm = clampPressureRise,
            PressureRiseTermInCombustionOnly = pressureRiseInCombustionOnly,

            // InitVars: the plenum pressure expression is (99000), the plenum
            // temperature is ambient, and the volume is taken at inlet valve closing.
            PressureAtInletValveClosing = 99000,
            TemperatureAtInletValveClosing = 298.15,
            VolumeAtInletValveClosing = geometry.Volume(States.InletClose * Math.PI / 180),
        };
    }

    private sealed record Row(
        double CrankAngle, double Volume, double Pressure,
        double BurntVolume, double UnburntVolume,
        double BurntTemperature, double UnburntTemperature,
        double Qb, double Qu);

    private static List<Row> Rows()
    {
        double[] Column(string name) => BaselinePaths.TraceColumn(name).Select(p => p.Value).ToArray();

        var crankAngles = BaselinePaths.TraceColumn("Vcyl").Select(p => p.CrankAngle).ToArray();
        var volume = Column("Vcyl");
        var pressure = Column("PCyl");
        var burntVolume = Column("Vb");
        var unburntVolume = Column("Vu");
        var burntTemperature = Column("Tb");
        var unburntTemperature = Column("Tu");
        var qb = Column("Qb");
        var qu = Column("Qu");

        return Enumerable.Range(0, crankAngles.Length)
            .Select(i => new Row(
                crankAngles[i],
                volume[i] / 1E6,
                pressure[i],
                burntVolume[i] / 1E6,
                unburntVolume[i] / 1E6,
                burntTemperature[i],
                unburntTemperature[i],
                qb[i],
                qu[i]))
            .ToList();
    }

    private static void Load(CylinderModel model, Row row)
    {
        var gas = model.Cylinder.State;

        gas.PGas = row.Pressure;
        gas.VGas = row.Volume;
        gas.Vb = row.BurntVolume;
        gas.Vu = row.UnburntVolume;
        gas.Tb = row.BurntTemperature;
        gas.Tu = row.UnburntTemperature;

        model.State = States.StateAt(row.CrankAngle);
        model.CrankAngleRadians = row.CrankAngle * Math.PI / 180;
    }

    [Fact]
    public void HeatLossMatchesTheBaselineTraceAtEveryCrankAngle()
    {
        BaselinePaths.Require();

        var model = Model();
        var step = DegreeStep * Math.PI / 180;

        var worst = 0.0;
        var worstDetail = string.Empty;
        var beyondHalfAUnit = 0;

        foreach (var row in Rows())
        {
            Load(model, row);

            // Run records Qb := dQbdtheta(x,Y)*dx, so the trace holds joules per step.
            var burnt = model.BurntHeatLossRate(model.CrankAngleRadians) * step;
            var unburnt = model.UnburntHeatLossRate(model.CrankAngleRadians) * step;

            foreach (var (name, actual, expected) in
                     (ReadOnlySpan<(string, double, double)>)[("Qb", burnt, row.Qb), ("Qu", unburnt, row.Qu)])
            {
                var error = Math.Abs(actual - expected);

                if (error > 0.0005)
                {
                    beyondHalfAUnit++;
                }

                if (error > worst)
                {
                    worst = error;
                    worstDetail = $"{name} at {row.CrankAngle} degrees ({model.State}): "
                                  + $"expected {expected:F3}, got {actual:F6}";
                }
            }
        }

        // The trace prints three decimals and this test feeds the model those same
        // rounded values back, so the output carries the inputs' rounding as well as its
        // own. One unit in the last place is therefore the floor: agreement closer than
        // that cannot be demonstrated from this data, and disagreement wider than it
        // cannot be blamed on rounding.
        Assert.True(worst <= 0.0011, $"Worst heat-loss error {worst:G4} J. {worstDetail}");

        // Of the 1440 values, only a handful should even reach that floor. If this
        // climbs, something in the correlation has drifted while staying inside the
        // printed precision.
        Assert.True(
            beyondHalfAUnit <= 10,
            $"{beyondHalfAUnit} of 1440 heat-loss values differ by more than half a printed unit.");
    }

    /// <summary>Woschni's published constant for the pressure-rise term, m/(s K).</summary>
    private const double WoschniC2 = 3.24E-3;

    /// <summary>
    /// B32's oracle. A cylinder at exactly the motored pressure has no combustion term:
    /// Woschni's velocity is the mean piston speed term alone. That has to hold at the
    /// crank angle of the state being evaluated, which inside an RKF5 step is not the step's
    /// start.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AtTheMotoredPressureOnlyThePistonSpeedTermRemains(bool corrected)
    {
        var model = Model(motoredAtCallAngle: corrected);
        model.State = EngineState.Compression;

        // The step starts 60 degrees before top dead centre; RKF5's fourth stage evaluates
        // the state twelve thirteenths of a degree on.
        var stepStart = -60 * Math.PI / 180;
        var trialAngle = stepStart + (12.0 / 13 * Math.PI / 180);
        model.CrankAngleRadians = stepStart;

        var motoredPressure = model.PressureAtInletValveClosing
                              * Math.Pow(model.VolumeAtInletValveClosing / model.Geometry.Volume(trialAngle), 1.30);
        var pistonSpeedTerm = EsaLimits.WoshiniC1Closed * 2 * model.Geometry.Stroke * Rpm / 60;

        var combustionTerm = model.CharacteristicVelocity(motoredPressure, trialAngle) - pistonSpeedTerm;

        if (corrected)
        {
            Assert.Equal(0, combustionTerm / pistonSpeedTerm, 9);
        }
        else
        {
            // The original compares the trial pressure with the motored pressure a step
            // behind it, so a motored cylinder appears to be burning.
            Assert.True(Math.Abs(combustionTerm / pistonSpeedTerm) > 1e-4, $"{combustionTerm}");
        }
    }

    /// <summary>
    /// B33's oracle. The pressure-rise term of Woschni's velocity is
    /// <c>C2 Vd Tr / (pr Vr) (p - pmot)</c>, so its slope in pressure gives back the swept
    /// volume it was computed with. That should be the displacement, the volume at bottom
    /// dead centre less the volume at top.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePressureRiseTermUsesTheDisplacement(bool corrected)
    {
        var model = Model(trueSweptVolume: corrected);
        model.State = EngineState.Combustion;
        model.CrankAngleRadians = 10 * Math.PI / 180;

        var slope = (model.CharacteristicVelocity(4e6, model.CrankAngleRadians)
                     - model.CharacteristicVelocity(3e6, model.CrankAngleRadians)) / 1e6;
        var sweptVolume = slope * model.PressureAtInletValveClosing * model.VolumeAtInletValveClosing
                          / (WoschniC2 * model.TemperatureAtInletValveClosing);

        var displacement = model.Geometry.Volume(Math.PI) - model.Geometry.Volume(0);
        var ratio = sweptVolume / displacement;

        if (corrected)
        {
            Assert.Equal(1, ratio, 9);
        }
        else
        {
            // CR/(CR+1) where (CR-1)/CR belongs: CR^2/(CR^2-1), 1.012 at 9.2.
            var cr = model.Geometry.CompressionRatio;
            Assert.Equal(cr * cr / ((cr * cr) - 1), ratio, 9);
        }
    }

    private static double PistonSpeedTerm(EngineState state, double rpm = Rpm) =>
        (state is EngineState.Compression or EngineState.Combustion or EngineState.Expansion
            ? EsaLimits.WoshiniC1Closed
            : EsaLimits.WoshiniC1GasExchange)
        * 2 * 0.0774 * rpm / 60;

    /// <summary>
    /// B31's oracle. Woschni's pressure-rise term stands for combustion-driven turbulence,
    /// which cannot be negative, so below the motored pressure the velocity is the piston
    /// speed term and the coefficient follows from it. The original lets the term go
    /// negative, and once it outweighs the piston speed term <c>Pwr</c> answers a
    /// coefficient of exactly zero.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BelowTheMotoredPressureTheCoefficientFollowsThePistonSpeedTerm(bool corrected)
    {
        // A misfire: 1 bar at top dead centre against 11 bar motored. At 1500 rpm the
        // piston speed term is small enough for the negative term to outweigh it; at the
        // baseline's 4000 rpm it cannot, and the baseline never gets here (ISSUES.md B31).
        const double rpm = 1500;
        var model = Model(clampPressureRise: corrected);
        model.Rpm = rpm;
        model.State = EngineState.Expansion;
        model.CrankAngleRadians = 0;

        const double pressure = 1e5;
        var velocity = model.CharacteristicVelocity(pressure, model.CrankAngleRadians);
        var coefficient = model.HeatTransferCoefficient(pressure, 1500, model.CrankAngleRadians);

        if (corrected)
        {
            Assert.Equal(PistonSpeedTerm(EngineState.Expansion, rpm), velocity, 9);
            Assert.True(coefficient > 0);
        }
        else
        {
            Assert.True(velocity < 0, $"{velocity}");
            Assert.Equal(0, coefficient);
        }
    }

    /// <summary>
    /// B75's oracle. Woschni published <c>C2 = 3.24e-3</c> for combustion and expansion and
    /// zero for gas exchange and compression, so outside those two states the velocity is
    /// the piston speed term whatever the pressure. The original applies the term in every
    /// state.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePressureRiseTermBelongsToCombustionAndExpansion(bool corrected)
    {
        var model = Model(pressureRiseInCombustionOnly: corrected);
        model.CrankAngleRadians = 20 * Math.PI / 180;

        // Well above the motored pressure, so the term is positive wherever it applies.
        const double pressure = 4e6;

        foreach (var state in Enum.GetValues<EngineState>())
        {
            model.State = state;
            var term = model.CharacteristicVelocity(pressure, model.CrankAngleRadians) - PistonSpeedTerm(state);
            var applies = !corrected || state is EngineState.Combustion or EngineState.Expansion;

            if (applies)
            {
                Assert.True(term > 1, $"{state}: {term}");
            }
            else
            {
                Assert.Equal(0, term, 9);
            }
        }
    }
}
