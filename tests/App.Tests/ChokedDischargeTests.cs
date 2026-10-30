using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B94: choked flow from the cylinder into a pipe enters it on the pipe's
/// characteristic with continuity against the sonic throat. The original took the entrance
/// velocity from the sudden expansion's momentum balance and the pressure from the
/// characteristic, a fourth relation for three unknowns, and continuity gave: the exhaust pipe
/// received a third to a half of the blowdown.
/// </summary>
public sealed class ChokedDischargeTests
{
    private const double Gamma = 1.3;
    private const double CylinderPressure = 4.0e5;
    private const double CylinderSpeedOfSound = 800;
    private const double DischargeCoefficient = 0.6;
    private const double AreaRatio = 0.5;

    private static double CriticalSpeed => CylinderSpeedOfSound * Math.Sqrt(2 / (Gamma + 1));

    private static double CriticalPressure =>
        CylinderPressure * ManifoldNumerics.Power(2 / (Gamma + 1), Gamma / (Gamma - 1));

    /// <summary>The entrance pressure continuity and energy ask for at entrance velocity <paramref name="u"/>.</summary>
    private static double ContinuityPressure(double u)
    {
        var ratio = u / CylinderSpeedOfSound;
        return DischargeCoefficient * AreaRatio * CylinderPressure
               * ManifoldNumerics.Power(2 / (Gamma + 1), (Gamma + 1) / 2 / (Gamma - 1))
               * (1 - ((Gamma - 1) / 2 * ratio * ratio)) / ratio;
    }

    /// <summary>
    /// The oracle for the solve: the entrance velocity it returns puts the pressure on the
    /// characteristic and at what continuity with the sonic throat needs, both at once, and
    /// the mass the entrance passes is the throat's.
    /// </summary>
    [Theory]
    [InlineData(1.0e5, 400)]
    [InlineData(1.5e5, 250)]
    [InlineData(0.8e5, 900)]
    public void TheEntranceIsOnTheCharacteristicWithContinuity(double intercept, double slope)
    {
        var u = ThroatVelocitySolvers.ChokedEntranceOnCharacteristic(
            Gamma, DischargeCoefficient, AreaRatio, CylinderPressure, CylinderSpeedOfSound, intercept, slope);

        Assert.InRange(u, 0, CriticalSpeed);

        var p = intercept + (slope * u);
        Assert.Equal(1, p / ContinuityPressure(u), 1e-6);

        // Mass: the entrance's rho u against the sonic throat's rho* a* Cd Ar.
        var entranceSpeedOfSound2 = (CylinderSpeedOfSound * CylinderSpeedOfSound) - ((Gamma - 1) / 2 * u * u);
        var entranceFlux = Gamma * p / entranceSpeedOfSound2 * u;
        var throatFlux = Gamma * CriticalPressure / (CriticalSpeed * CriticalSpeed) * CriticalSpeed
                         * DischargeCoefficient * AreaRatio;
        Assert.Equal(1, entranceFlux / throatFlux, 1e-6);
    }

    /// <summary>
    /// When the pipe's pressure is too low for any subsonic entrance, the entrance is sonic,
    /// at the critical speed, where continuity's pressure is Cd Ar p*.
    /// </summary>
    [Fact]
    public void BelowEverySubsonicEntranceTheEntranceIsSonic()
    {
        var u = ThroatVelocitySolvers.ChokedEntranceOnCharacteristic(
            Gamma, DischargeCoefficient, AreaRatio, CylinderPressure, CylinderSpeedOfSound,
            characteristicIntercept: 1000, characteristicSlope: 1);

        Assert.Equal(CriticalSpeed, u, 1e-9);
        Assert.Equal(DischargeCoefficient * AreaRatio * CriticalPressure, ContinuityPressure(u), 1e-6 * CriticalPressure);
    }

    /// <summary>
    /// The transition is continuous. Where the characteristic passes through the original's
    /// momentum-balance velocity, the two closures give the same entrance, and the throat
    /// pressure the momentum balance then asks for is p* exactly: on that boundary the choked
    /// and subsonic solutions meet, so the switch between them has nothing to jump over.
    /// </summary>
    [Fact]
    public void AtTheChokingLimitTheMomentumBalanceAsksForPStar()
    {
        var momentum = ThroatVelocitySolvers.ExhaustSonic(
            Gamma, DischargeCoefficient, AreaRatio, CriticalSpeed, CylinderSpeedOfSound,
            wholeSubsonicRange: true, fromZero: true);

        const double Slope = 300;
        var intercept = ContinuityPressure(momentum) - (Slope * momentum);

        var u = ThroatVelocitySolvers.ChokedEntranceOnCharacteristic(
            Gamma, DischargeCoefficient, AreaRatio, CylinderPressure, CylinderSpeedOfSound, intercept, Slope);

        Assert.Equal(momentum, u, 1e-4);

        var p = intercept + (Slope * u);
        var entranceSpeedOfSound2 = (CylinderSpeedOfSound * CylinderSpeedOfSound) - ((Gamma - 1) / 2 * u * u);
        var density = Gamma * p / entranceSpeedOfSound2;

        var asked = ManifoldNumerics.MomentumThroatPressure(
            Gamma, p, density, u, CriticalPressure, DischargeCoefficient, AreaRatio);

        Assert.Equal(1, asked / CriticalPressure, 1e-6);
    }

    /// <summary>The choke test accepts p* itself under B94, and is the original's otherwise.</summary>
    [Fact]
    public void ThePStarThroatCountsAsChoked()
    {
        var critical = ManifoldNumerics.Power((Gamma + 1) / 2, Gamma / (Gamma - 1));
        var justAbove = CylinderPressure / critical * (1 + 1e-12);

        Assert.True(ManifoldNumerics.IsChoked(CylinderPressure, justAbove, critical, tolerant: true));
        Assert.False(ManifoldNumerics.IsChoked(CylinderPressure, justAbove, critical, tolerant: false));
        Assert.False(ManifoldNumerics.IsChoked(CylinderPressure, 1.01 * CylinderPressure / critical, critical, tolerant: true));
    }

    private static (double Worst, int Steps) Blowdown(bool b94)
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        engine.Rpm = 4000;

        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.ChokedDischargeContinuity.Entry] = b94;

        var solver = new CycleSolver(engine, new ManifoldSolver(engine, physics: physics), physics: physics);
        Assert.True(solver.Initialise(), "Both cam profiles should have loaded.");

        var exhaustPipe = new PipeGeometry(engine.Manifold.ExhaustPipe.AreaVersusLength, clampPastEnd: true);
        const int measuredCycle = 6;
        var cycle = 0;
        var worst = 0.0;
        var steps = 0;

        solver.StepCompleted += s =>
        {
            if (cycle != measuredCycle)
            {
                return;
            }

            var exhaust = s.Engine.Manifold.Exhaust;
            var port = exhaustPipe.Area(exhaust.X[0]);
            var dt = 1 / (s.Engine.Rpm / 60 * 360) * s.Engine.CrankAngleStep;
            var pipeOut = exhaust.Density[0] * exhaust.Velocity[0] * port * dt;

            // The blowdown: the valve still narrower than its port, and a real outflow.
            if (solver.ExhaustValve.FlowArea(s.Engine.CrankAngle) < port && s.Engine.MassOut > 1e-6 && s.Engine.CrankAngle < 270)
            {
                worst = Math.Max(worst, Math.Abs((s.Engine.MassOut / pipeOut) - 1));
                steps++;
            }
        };

        engine.ZoneCount = 1;

        for (cycle = 1; cycle <= measuredCycle; cycle++)
        {
            if (cycle > 1)
            {
                engine.ZoneCount = 2;
            }

            solver.RunOneCycle();
        }

        return (worst, steps);
    }

    /// <summary>
    /// In a whole run every step of the blowdown puts into the exhaust pipe what leaves the
    /// cylinder. Measured on the baseline engine at 4000 rpm: equal at every step, where without
    /// B94 the pipe took 7.6 of 24.7 g/s at 130 degrees and 74 of 184 at 150.
    /// </summary>
    [Fact]
    public void TheExhaustPipeReceivesTheBlowdown()
    {
        BaselinePaths.Require();

        var (worst, steps) = Blowdown(b94: true);
        var (without, _) = Blowdown(b94: false);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"worst step {worst:E2} over {steps} blowdown steps; without B94 {without:P1}");

        Assert.True(steps > 30, $"Only {steps} blowdown steps were seen.");
        Assert.InRange(worst, 0, 1e-3);
        Assert.True(without > 0.3, $"Without B94 the worst step was only {without:P1} out.");
        Assert.False(new PhysicsCorrections { Mode = PhysicsMode.Legacy }.IsOn(CorrectionCatalogue.ChokedDischargeContinuity));
    }
}
