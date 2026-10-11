using App.Core;
using App.Core.Expressions;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md A36 and B83: an abrupt area step in a pipe solved as a junction between two
/// segments, conserving mass and energy, instead of as the original's area-gradient source.
/// </summary>
public sealed class AreaJunctionTests
{
    private const double Gamma = 1.4;
    private const double Pressure = 1e5;
    private const double Temperature = 300;

    private static readonly double Density = Pressure / 287 / Temperature;
    private static readonly double SoundSpeed = Math.Sqrt(Gamma * Pressure / Density);

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(BaselinePaths.Directory!, "..", ".."));

    /// <summary>A pipe of one area up to a step a millimetre wide, and another after it.</summary>
    private static ManifoldAreaTable SteppedTable(double length, double stepAt, double before, double after)
    {
        var table = new ManifoldAreaTable { Count = 4 };
        (table.Position[0], table.Area[0]) = (0, before);
        (table.Position[1], table.Area[1]) = (stepAt - 0.5, before);
        (table.Position[2], table.Area[2]) = (stepAt + 0.5, after);
        (table.Position[3], table.Area[3]) = (length, after);
        return table;
    }

    private static (PipeLayout Layout, PipeGrid Current, PipeGrid Next) SteppedPipe(
        double length, double stepAt, double before, double after, int points)
    {
        var table = SteppedTable(length, stepAt, before, after);
        var layout = PipeLayout.Build(new PipeGeometry(table), table, points);
        var current = new PipeGrid(layout.PointCount);
        var next = new PipeGrid(layout.PointCount);
        PipeGridInitialiser.Initialise(current, layout.Positions, Pressure, Temperature, Gamma);
        PipeGridInitialiser.Initialise(next, layout.Positions, Pressure, Temperature, Gamma);
        return (layout, current, next);
    }

    /// <summary>
    /// One time step of every point between the two ends, as <c>ManifoldSolver</c> does it;
    /// the two ends are held where they are.
    /// </summary>
    private static void Advance(PipeLayout layout, PipeGrid current, PipeGrid next, double dt)
    {
        var junction = 0;

        for (var i = 1; i <= current.ActiveCount - 2; i++)
        {
            if (junction < layout.Junctions.Count && layout.Junctions[junction] == i)
            {
                JunctionBoundary.Apply(current, next, layout.Geometry[i], layout.Geometry[i + 1], Gamma, dt, i);
                junction++;
                i++;
                continue;
            }

            CharacteristicSolver.UpdateInteriorPoint(current, next, layout.Geometry[i], Gamma, dt, i);
        }

        foreach (var end in new[] { 0, current.ActiveCount - 1 })
        {
            next.Velocity[end] = current.Velocity[end];
            next.Pressure[end] = current.Pressure[end];
            next.Density[end] = current.Density[end];
            next.SpeedOfSound[end] = current.SpeedOfSound[end];
        }

        for (var i = 0; i < current.ActiveCount; i++)
        {
            current.Velocity[i] = next.Velocity[i];
            current.Pressure[i] = next.Pressure[i];
            current.Density[i] = next.Density[i];
            current.SpeedOfSound[i] = next.SpeedOfSound[i];
        }
    }

    /// <summary>
    /// The steps are the eight-valve engine's and nothing else's. Every gradual change in
    /// every other shipped table - the trumpets, the tapers, the tumble inlets - is sampled
    /// at five millimetres or more, or changes by a fraction of a per cent, so B83 leaves
    /// every other engine's grid exactly as the original lays it.
    /// </summary>
    [Fact]
    public void OnlyTheEightValveEnginesTablesHaveSteps()
    {
        BaselinePaths.Require();

        var store = new ManifoldAreaTableStore();
        var stepped = new List<string>();

        foreach (var path in Directory.EnumerateFiles(RepositoryRoot, "*.maf", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

            if (relative.StartsWith("validation-reports/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            if (PipeLayout.FindSteps(store.Read(path).Table).Count > 0)
            {
                stepped.Add(relative);
            }
        }

        Assert.All(stepped, path => Assert.True(
            path.StartsWith("legacy/CAEEng/", StringComparison.Ordinal)
            || path.StartsWith("validation/cases/VW8V_A4LowCost/", StringComparison.Ordinal),
            path));
        Assert.Contains("legacy/CAEEng/A4LowCostEx.maf", stepped);

        var exhaust = store.Read(Path.Combine(RepositoryRoot, "legacy", "CAEEng", "A4LowCostEx.maf")).Table;
        Assert.Equal([(83.0, 84.0), (243.0, 244.0), (413.0, 414.0)], PipeLayout.FindSteps(exhaust));
    }

    /// <summary>
    /// A junction gets two faces at its midpoint, and each segment is gridded no finer than
    /// the operator's grid.
    /// </summary>
    [Fact]
    public void EachJunctionHasTwoFacesAndNoSegmentIsFinerThanTheOperatorsGrid()
    {
        var (layout, _, _) = SteppedPipe(958, 243.5, 707, 1164, points: 14);
        var spacing = 0.958 / 13;

        var junction = Assert.Single(layout.Junctions);
        Assert.Equal(0.2435, layout.Positions[junction], 12);
        Assert.Equal(layout.Positions[junction], layout.Positions[junction + 1]);
        Assert.Equal(0.958, layout.Positions[^1]);

        for (var i = 1; i < layout.PointCount; i++)
        {
            if (i != junction + 1)
            {
                Assert.True(layout.Positions[i] - layout.Positions[i - 1] >= spacing - 1e-12);
            }
        }

        // Each side sees its own area right up to the junction, and no gradient from the step.
        Assert.Equal(707e-6, layout.Geometry[junction].Area(0.2435), 15);
        Assert.Equal(1164e-6, layout.Geometry[junction + 1].Area(0.2435), 15);
        Assert.Equal(0, layout.Geometry[junction].AreaGradient(0.2425));
        Assert.Equal(0, layout.Geometry[junction + 1].AreaGradient(0.2445));
    }

    /// <summary>Gas at rest either side of a step stays at rest, to the last bit.</summary>
    [Fact]
    public void GasAtRestStaysAtRest()
    {
        var (layout, current, next) = SteppedPipe(1000, 500, 1000, 2000, points: 51);
        var dt = 0.5 * 0.02 / SoundSpeed;

        for (var step = 0; step < 200; step++)
        {
            Advance(layout, current, next, dt);
        }

        for (var i = 0; i < current.ActiveCount; i++)
        {
            Assert.Equal(0, current.Velocity[i]);
            Assert.Equal(Pressure, current.Pressure[i]);
            Assert.Equal(Density, current.Density[i]);
        }
    }

    /// <summary>
    /// The acoustic oracle. A weak pressure pulse meeting an area change from A1 to A2 is
    /// reflected with (A1 - A2)/(A1 + A2) of its amplitude and transmitted with
    /// 2 A1/(A1 + A2), both ways round: halved into a larger pipe and reflected inverted,
    /// or amplified into a smaller one and reflected upright. The pulses are compared by
    /// their integrals, which the scheme's numerical diffusion spreads but does not change.
    /// </summary>
    [Theory]
    [InlineData(1000, 2000)]
    [InlineData(2000, 1000)]
    public void AWeakPulseIsReflectedAndTransmittedAsAcousticsSays(double before, double after)
    {
        var (layout, current, next) = SteppedPipe(4000, 2000, before, after, points: 401);
        var junction = layout.Junctions[0];

        // A rightward simple wave, a thousandth of the pressure at its peak.
        for (var i = 0; i < current.ActiveCount; i++)
        {
            var excess = 1e-3 * Pressure * Math.Exp(-Math.Pow((current.X[i] - 1.0) / 0.1, 2));

            // Cut off the tail: a velocity of 1e-313 overflows the laminar friction factor.
            excess = excess < 1e-9 ? 0 : excess;
            current.Pressure[i] = Pressure + excess;
            current.Density[i] = Density + (excess / (SoundSpeed * SoundSpeed));
            current.Velocity[i] = excess / (Density * SoundSpeed);
            current.SpeedOfSound[i] = Math.Sqrt(Gamma * current.Pressure[i] / current.Density[i]);
        }

        double Integral(int from, int to)
        {
            var sum = 0.0;

            for (var i = from; i < to; i++)
            {
                sum += ((current.Pressure[i] - Pressure) + (current.Pressure[i + 1] - Pressure)) / 2
                       * (current.X[i + 1] - current.X[i]);
            }

            return sum;
        }

        var incident = Integral(0, junction);
        var dt = 0.5 * 0.01 / SoundSpeed;

        // Until the pulse has crossed and both halves are clear of the junction.
        for (var step = 0; step < (int)(1.6 / SoundSpeed / dt); step++)
        {
            Advance(layout, current, next, dt);
        }

        var reflected = Integral(0, junction) / incident;
        var transmitted = Integral(junction + 1, current.ActiveCount - 1) / incident;

        Assert.Equal((before - after) / (before + after), reflected, 0.01);
        Assert.Equal(2 * before / (before + after), transmitted, 0.01);
    }

    /// <summary>
    /// The steady state downstream of a step, solved independently of the junction: an
    /// isentropic contraction by the area-Mach relation, or a Borda-Carnot expansion by mass,
    /// energy and the momentum of a control volume whose upstream face carries the upstream
    /// pressure across the whole downstream area.
    /// </summary>
    private static (double U, double P, double R) Downstream(
        double u1, double p1, double r1, double areaUp, double areaDown)
    {
        var c1 = Math.Sqrt(Gamma * p1 / r1);
        var k = (Gamma - 1) / 2;

        double Bisect(Func<double, double> f, double low, double high)
        {
            for (var i = 0; i < 200; i++)
            {
                var mid = (low + high) / 2;

                if (Math.Sign(f(mid)) == Math.Sign(f(low)))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return (low + high) / 2;
        }

        if (areaDown < areaUp)
        {
            double AreaRatio(double m) =>
                1 / m * Math.Pow(2 / (Gamma + 1) * (1 + (k * m * m)), (Gamma + 1) / (2 * (Gamma - 1)));

            var m1 = u1 / c1;
            var target = AreaRatio(m1) * areaDown / areaUp;
            var m2 = Bisect(m => AreaRatio(m) - target, 1e-9, 1);
            var temperatureRatio = (1 + (k * m1 * m1)) / (1 + (k * m2 * m2));
            var p2 = p1 * Math.Pow(temperatureRatio, Gamma / (Gamma - 1));
            var r2 = r1 * Math.Pow(p2 / p1, 1 / Gamma);
            return (m2 * Math.Sqrt(Gamma * p2 / r2), p2, r2);
        }

        (double R, double P) State(double u2)
        {
            var r2 = r1 * u1 * areaUp / (u2 * areaDown);
            var c2Squared = (c1 * c1) + (k * ((u1 * u1) - (u2 * u2)));
            return (r2, r2 * c2Squared / Gamma);
        }

        var u2 = Bisect(
            u =>
            {
                var (r2, p2) = State(u);
                return p1 + (r1 * u1 * u1 * areaUp / areaDown) - p2 - (r2 * u * u);
            },
            u1 * 1e-3,
            u1);

        var (rDown, pDown) = State(u2);
        return (u2, pDown, rDown);
    }

    /// <summary>
    /// Steady flow through a step is a fixed point of the junction: mass flux and
    /// stagnation enthalpy are the same on both faces, a contraction keeps the stagnation
    /// pressure and an expansion loses Borda-Carnot's share of it, whichever way the pipe
    /// is flowing. A contraction to half the area chokes from a Mach number of 0.31, so it
    /// is fed more slowly than an expansion.
    /// </summary>
    [Theory]
    [InlineData(1000, 2000, 120)]
    [InlineData(2000, 1000, 60)]
    [InlineData(1000, 2000, -60)]
    [InlineData(2000, 1000, -120)]
    public void SteadyFlowThroughAStepIsAFixedPoint(double leftArea, double rightArea, double upstreamVelocity)
    {
        var (layout, current, next) = SteppedPipe(400, 200, leftArea, rightArea, points: 41);
        var junction = layout.Junctions[0];
        var fromLeft = upstreamVelocity > 0;
        var (areaUp, areaDown) = fromLeft ? (leftArea, rightArea) : (rightArea, leftArea);

        var (u2, p2, r2) = Downstream(Math.Abs(upstreamVelocity), Pressure, Density, areaUp, areaDown);
        var upstream = (U: upstreamVelocity, P: Pressure, R: Density);
        var downstream = (U: Math.Sign(upstreamVelocity) * u2, P: p2, R: r2);

        for (var i = 0; i < current.ActiveCount; i++)
        {
            var onLeft = i <= junction;
            var (u, p, r) = onLeft == fromLeft ? upstream : downstream;
            (current.Velocity[i], current.Pressure[i], current.Density[i]) = (u, p, r);
            current.SpeedOfSound[i] = Math.Sqrt(Gamma * p / r);
        }

        // A step short enough that wall friction moves nothing.
        JunctionBoundary.Apply(current, next, layout.Geometry[junction], layout.Geometry[junction + 1], Gamma, 1e-9, junction);

        for (var face = junction; face <= junction + 1; face++)
        {
            Assert.Equal(current.Velocity[face], next.Velocity[face], 1e-4);
            Assert.Equal(current.Pressure[face], next.Pressure[face], 1e-2);
            Assert.Equal(current.Density[face], next.Density[face], 1e-7);
        }

        double Stagnation(double u, double p, double r) =>
            p * Math.Pow(1 + ((Gamma - 1) / 2 * u * u * r / (Gamma * p)), Gamma / (Gamma - 1));

        var lossUp = Stagnation(upstream.U, upstream.P, upstream.R);
        var lossDown = Stagnation(downstream.U, downstream.P, downstream.R);

        if (areaDown < areaUp)
        {
            Assert.Equal(lossUp, lossDown, lossUp * 1e-12);
        }
        else
        {
            Assert.True(lossDown < lossUp - 100, $"{lossUp - lossDown}");
        }
    }

    /// <summary>
    /// At low speed the expansion's pressure recovery is the textbook Borda-Carnot result,
    /// <c>ρ u1² (A1/A2)(1 - A1/A2)</c>, to within the compressibility of a Mach number of
    /// 0.03.
    /// </summary>
    [Fact]
    public void ASlowExpansionRecoversBordaCarnotsPressure()
    {
        var (u2, p2, _) = Downstream(10, Pressure, Density, 1000, 2000);

        Assert.Equal(Density * 100 * 0.5 * 0.5, p2 - Pressure, Density * 100 * 0.25 * 0.01);
        Assert.Equal(5, u2, 0.01);
    }

    private static Engine EightValveEngine(int rpm)
    {
        var loader = new EngineLoader(
            new EngineDefinitionStore(),
            new CamProfileReader(),
            new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(),
            new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(),
            new DischargeCoefficientTableStore());

        var engine = loader.Load(Path.Combine(RepositoryRoot, "validation", "cases", "VW8V_A4LowCost", "A4LowCost.eng")).Engine;
        engine.Rpm = rpm;
        return engine;
    }

    private static string? Failure(int rpm, bool junctions)
    {
        var engine = EightValveEngine(rpm);
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides[CorrectionCatalogue.AreaJunctions.Entry] = junctions;

        try
        {
            new SimulationRunner(new CachingExpressionEvaluator()).Run(
                engine,
                new SimulationSettings { CycleCount = 8, OneZoneCycleCount = 1, MassBalance = 0, Physics = physics },
                cancellation: TestContext.Current.CancellationToken);
            return null;
        }
        catch (CfdException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// A36: the eight-valve engine of the thesis's section 6.5 stops in the first cycle on
    /// its exhaust's area steps without the junctions, and runs with them.
    /// </summary>
    [Theory]
    [InlineData(2500)]
    [InlineData(4000)]
    [InlineData(6000)]
    public void TheEightValveEngineRunsWithJunctions(int rpm)
    {
        BaselinePaths.Require();

        Assert.NotNull(Failure(rpm, junctions: false));
        Assert.Null(Failure(rpm, junctions: true));
    }
}
