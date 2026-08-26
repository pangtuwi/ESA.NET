using App.Core;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The state map decides which equations run at each crank angle, so its boundaries have
/// to land in exactly the right place. The baseline trace shows where they are without
/// being told: two of the heat-loss integrals are switched off by state, so the runs of
/// exact zeroes in the <c>Qb</c> and <c>Qu</c> columns mark four of the six boundaries
/// directly.
/// </summary>
public sealed class CrankAngleStateMapTests
{
    // A2China at 4000 rpm: IVO 19, IVC 80, EVO 64, EVC 37, spark 21 BTDC, burn 55.
    private static CrankAngleStateMap Baseline() => new(
        inletOpen: 360 - 19,
        inletClose: -180 + 80,
        exhaustOpen: 180 - 64,
        exhaustClose: -360 + 37,
        sparkAngle: -21,
        burnAngle: 55);

    [Fact]
    public void TheCycleVisitsTheSixStatesInOrder()
    {
        var map = Baseline();

        Assert.Equal(EngineState.Overlap, map.StateAt(-359));
        Assert.Equal(EngineState.Overlap, map.StateAt(-324));
        Assert.Equal(EngineState.Intake, map.StateAt(-323));
        Assert.Equal(EngineState.Intake, map.StateAt(-101));
        Assert.Equal(EngineState.Compression, map.StateAt(-100));
        Assert.Equal(EngineState.Compression, map.StateAt(-22));
        Assert.Equal(EngineState.Combustion, map.StateAt(-21));
        Assert.Equal(EngineState.Combustion, map.StateAt(33));
        Assert.Equal(EngineState.Expansion, map.StateAt(34));
        Assert.Equal(EngineState.Expansion, map.StateAt(115));
        Assert.Equal(EngineState.Exhaust, map.StateAt(116));
        Assert.Equal(EngineState.Exhaust, map.StateAt(340));
        Assert.Equal(EngineState.Overlap, map.StateAt(341));
        Assert.Equal(EngineState.Overlap, map.StateAt(360));
    }

    [Fact]
    public void BurntHeatLossIsSwitchedOffAcrossExactlyIntakeAndCompression()
    {
        // dQbdtheta returns zero for Intake and Compression and nothing else.
        AssertSwitchedOff("Qb", state => state is EngineState.Intake or EngineState.Compression);
    }

    [Fact]
    public void UnburntHeatLossIsSwitchedOffAcrossExactlyExpansionAndExhaust()
    {
        // dQudtheta returns zero for Expansion and Exhaust and nothing else.
        AssertSwitchedOff("Qu", state => state is EngineState.Expansion or EngineState.Exhaust);
    }

    /// <summary>
    /// Checks a heat-loss column against the states that switch it off.
    /// </summary>
    /// <remarks>
    /// The implication only runs one way. Inside a switched-off state the integral
    /// returns a literal zero, so every such row must be zero. Outside it the value is
    /// live but can still round to zero at three decimal places, which happens at three
    /// crank angles in this trace - so a zero outside is evidence, not proof. Bounding
    /// how many there may be still pins the boundaries: shifting one by a single degree
    /// would either put a live value inside a switched-off run or add a fourth zero
    /// outside it.
    /// </remarks>
    private static void AssertSwitchedOff(string column, Func<EngineState, bool> switchedOff)
    {
        BaselinePaths.Require();

        var map = Baseline();
        var zerosOutside = 0;

        foreach (var (crankAngle, value) in BaselinePaths.TraceColumn(column))
        {
            var state = map.StateAt(crankAngle);

            if (switchedOff(state))
            {
                Assert.True(
                    value == 0,
                    $"At {crankAngle} degrees the state is {state}, which switches {column} off, "
                    + $"but the trace has {value}.");
            }
            else if (value == 0)
            {
                zerosOutside++;
            }
        }

        Assert.True(
            zerosOutside <= 3,
            $"{zerosOutside} rows outside the switched-off states have {column} = 0, "
            + "which suggests a state boundary has moved.");
    }

    [Fact]
    public void ValveTimingsAreConvertedOnTheWayOutOfTheEngineFile()
    {
        BaselinePaths.Require();

        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        var map = CrankAngleStateMap.FromEngine(engine, sparkAdvance: 21);

        // The file holds 19, 80, 64, 37 as entered; the simulation needs signed crank
        // angles. Getting either sense backwards would move every state boundary.
        Assert.Equal(341, map.InletOpen);
        Assert.Equal(-100, map.InletClose);
        Assert.Equal(116, map.ExhaustOpen);
        Assert.Equal(-323, map.ExhaustClose);

        // InitVars negates the spark advance the .spk map returns.
        Assert.Equal(-21, map.SparkAngle);
        Assert.Equal(55, map.BurnAngle);
    }
}
