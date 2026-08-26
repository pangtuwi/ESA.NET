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
    // gives 21 at 4000; the valve angles are converted by CrankAngleStateMap.FromEngine.
    private static readonly CrankAngleStateMap States = new(
        inletOpen: 360 - 19,
        inletClose: -180 + 80,
        exhaustOpen: 180 - 64,
        exhaustClose: -360 + 37,
        sparkAngle: -21,
        burnAngle: 55);

    private static CylinderModel Model()
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
}
