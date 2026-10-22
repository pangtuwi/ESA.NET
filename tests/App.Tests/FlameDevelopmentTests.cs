using App.Core.Thermo;

namespace App.Tests;

/// <summary>
/// ISSUES.md B90: with no delay in the file, the Wiebe burn of B89 starts so that 10 per
/// cent of the charge is burnt at Heywood's flame-development angle for the speed.
/// </summary>
public sealed class FlameDevelopmentTests
{
    private const double Spark = -25;

    private static TwoZoneGas Gas(double rpm, double burnAngle, double? fileDelay = null, bool defaultDelay = true)
    {
        var gas = new TwoZoneGas
        {
            WiebeBurn = true,
            FlameDevelopmentAngle = defaultDelay ? FlameDevelopment.Angle(rpm) : null,
        };
        gas.State.ThetaSpark = Spark;
        gas.State.Fuel.BurnAngle = burnAngle;
        gas.State.Fuel.CombustionDelay = fileDelay;
        return gas;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    /// <summary>
    /// Oracle: the correlation reproduces Heywood's Figure 9-17 (p. 394), the
    /// flame-development angle read as about 18, 23, 27 and 29 degrees at 750, 1500, 2250
    /// and 3000 rev/min, to within the reading.
    /// </summary>
    [Theory]
    [InlineData(750, 18)]
    [InlineData(1500, 23)]
    [InlineData(2250, 27)]
    [InlineData(3000, 29)]
    public void TheAngleIsHeywoodsFigure917(double rpm, double measured)
    {
        Assert.Equal(measured, FlameDevelopment.Angle(rpm), 1.5);
    }

    /// <summary>
    /// Oracle: with no delay in the file, 10 per cent of the charge is burnt exactly the
    /// flame-development angle after the spark, whatever the burn angle.
    /// </summary>
    [Theory]
    [InlineData(2000, 45)]
    [InlineData(2000, 55)]
    [InlineData(2000, 65)]
    [InlineData(5000, 55)]
    public void TenPercentIsBurntAtTheFlameDevelopmentAngle(double rpm, double burnAngle)
    {
        var gas = Gas(rpm, burnAngle);

        Assert.Equal(0.1, gas.BurntFraction(Radians(Spark + FlameDevelopment.Angle(rpm))), 1e-9);
    }

    [Fact]
    public void ADelayInTheFileWins()
    {
        Assert.Equal(Spark + 4, Gas(4000, 55, fileDelay: 4).StartOfCombustion);
        Assert.Equal(Spark, Gas(4000, 55, fileDelay: 0).StartOfCombustion);
    }

    /// <summary>A burn angle long enough to reach 10 per cent before the angle starts at the spark, not before it.</summary>
    [Fact]
    public void TheDefaultIsNeverNegative()
    {
        Assert.Equal(0, FlameDevelopment.DefaultDelay(10, 55));
        Assert.Equal(Spark, Gas(500, 90).StartOfCombustion);
    }

    [Fact]
    public void WithoutB90ThereIsNoDefaultDelay()
    {
        Assert.Equal(Spark, Gas(4000, 55, defaultDelay: false).StartOfCombustion);
    }

    [Fact]
    public void WithoutB89ThereIsNoDelayAtAll()
    {
        var gas = Gas(4000, 55, fileDelay: 4);
        gas.WiebeBurn = false;

        Assert.Equal(Spark, gas.StartOfCombustion);
    }

    /// <summary>At 55 degrees the default delay is about 8, 14 and 17 degrees at 1500, 3000 and 4000 rev/min.</summary>
    [Theory]
    [InlineData(1500, 7.8)]
    [InlineData(3000, 13.9)]
    [InlineData(4000, 16.9)]
    public void TheDefaultDelayAt55Degrees(double rpm, double delay)
    {
        Assert.Equal(delay, Gas(rpm, 55).StartOfCombustion - Spark, 0.1);
    }
}
