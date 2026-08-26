using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Valve lift and flow area, checked against the baseline trace's <c>IV A</c> and
/// <c>EV A</c> columns. Like the geometry, this depends on nothing that has to be
/// integrated: the cam profile, the converted timings, the maximum lift and the valve
/// count and diameter fix the answer at every crank angle.
/// </summary>
public sealed class ValveMotionTests
{
    private static EngineLoader Loader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static (ValveMotion Inlet, ValveMotion Exhaust) Baseline()
    {
        var engine = Loader().Load(BaselinePaths.File("A2China.eng")).Engine;

        return (ValveMotion.Inlet(engine.Manifold.InletValve),
                ValveMotion.Exhaust(engine.Manifold.ExhaustValve));
    }

    private static void AssertMatchesTrace(ValveMotion valve, string column)
    {
        BaselinePaths.Require();

        var worst = 0.0;
        var worstAngle = 0.0;

        foreach (var (crankAngle, expected) in BaselinePaths.TraceColumn(column))
        {
            // The trace writes square millimetres to one decimal place.
            var actual = valve.FlowArea(crankAngle) * 1E6;
            var error = Math.Abs(actual - expected);

            if (error > worst)
            {
                worst = error;
                worstAngle = crankAngle;
            }
        }

        Assert.True(
            worst <= 0.05,
            $"Worst {column} error {worst:G4} mm2 at {worstAngle} degrees exceeds the printed precision.");
    }

    [Fact]
    public void InletFlowAreaMatchesTheBaselineTraceAtEveryCrankAngle() =>
        AssertMatchesTrace(Baseline().Inlet, "IV A");

    [Fact]
    public void ExhaustFlowAreaMatchesTheBaselineTraceAtEveryCrankAngle() =>
        AssertMatchesTrace(Baseline().Exhaust, "EV A");

    [Fact]
    public void TheOpenWindowWrapsPastTopDeadCentreAndMatchesTheDisplayedDuration()
    {
        BaselinePaths.Require();

        var (inlet, exhaust) = Baseline();

        // Open + 180 + Close, the read-only field the original shows on the Cams tab.
        Assert.Equal(279, inlet.Duration, 9);
        Assert.Equal(281, exhaust.Duration, 9);

        // The window straddles the end of the trace's crank-angle range and reappears
        // at its start: the inlet is off its seat from 344 degrees round through 360 and
        // -359, and closes again at -106.
        Assert.True(inlet.IsOpen(344));
        Assert.True(inlet.IsOpen(360));
        Assert.True(inlet.IsOpen(-359));
        Assert.True(inlet.IsOpen(-103));

        // Outside the timing window there is no lift at all, and the profile holds the
        // valve seated for the first one per cent and last two per cent of the window,
        // so the lift stays at zero for a few degrees inside each end of it too.
        Assert.Equal(0, inlet.Lift(340));
        Assert.Equal(0, inlet.Lift(342));
        Assert.Equal(0, inlet.Lift(-99));
        Assert.Equal(0, inlet.Lift(-101));
    }

    [Fact]
    public void PeakLiftIsTheMaximumLiftFromTheEngineFile()
    {
        BaselinePaths.Require();

        var (inlet, exhaust) = Baseline();

        // The .cam profile is normalised to a peak of 1, so the peak lift over the cycle
        // has to come back as the file's IVLift and EVLift, in metres.
        var inletPeak = Enumerable.Range(-359, 720).Max(ca => inlet.Lift(ca));
        var exhaustPeak = Enumerable.Range(-359, 720).Max(ca => exhaust.Lift(ca));

        Assert.Equal(0.00862, inletPeak, 5);
        Assert.Equal(0.0104, exhaustPeak, 5);
    }
}
