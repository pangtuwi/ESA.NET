using App.Core;
using App.Core.Manifold;
using App.Core.Model;
using App.Core.Simulation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// ISSUES.md B84: the inlet valve's first step after opening. The throat is left where the
/// previous valve event put it while the valve is shut, and the open routine's first pass
/// resets it to the cylinder's pressure only when the old throat velocity is a subsonic
/// inflow or the pipe end flows back slower than IVFR.
/// </summary>
public sealed class InletValveOpeningTests
{
    private const double Gamma = CharacteristicSolver.InletGamma;

    private static Engine EightValveEngine() =>
        new EngineLoader(
                new EngineDefinitionStore(),
                new CamProfileReader(),
                new SpeedKeyedTableReader(),
                new WallTemperatureTableReader(),
                new ExhaustBackPressureTableReader(),
                new ManifoldAreaTableStore(),
                new DischargeCoefficientTableStore())
            .Load(Path.Combine(BaselinePaths.Directory!, "..", "..", "validation", "cases", "VW8V_A4LowCost", "A4LowCost.eng"))
            .Engine;

    /// <summary>
    /// The inlet valve opening on the eight-valve engine at 2000 rpm under Corrected with
    /// B83, in its second cycle, exactly as captured: the inlet pipe behind the closed valve,
    /// its end at rest at 99.98 kPa, the cylinder at 134.4 kPa and 966 K, and the throat as
    /// the last valve event's choked reverse flow left it, 372.7 m/s out of the cylinder at
    /// 90.84 kPa.
    /// </summary>
    private static (Exception? Failure, double PipeEndVelocity, double ThroatVelocity) Open(
        bool fresh, (double Forward, double ForwardReverse, double Reverse) tuning)
    {
        const double gamma = 1.3523466604344605;
        const double cylinderPressure = 134447.00628724656;

        double[] x = [0, 0.05758333333333333, 0.11516666666666665, 0.17275, 0.2303333333333333, 0.28791666666666665, 0.3455, 0.3455, 0.41816666666666663, 0.49083333333333334, 0.5635, 0.5635, 0.658];
        double[] u = [-11.308765234864715, -13.863334056929942, -13.234761868929455, -12.208849631070736, -10.844614917843874, -9.20551175613661, -7.363627957282465, -4.927826252735447, -9.581302898962104, -6.525010467732548, -3.607312334702026, -3.404805376839312, 0];
        double[] p = [99000, 98971.8146245519, 99016.54838612444, 99079.74478245544, 99155.86264149357, 99240.36449052891, 99326.1905076067, 99343.2076111427, 99738.86278517234, 99870.84395307791, 99947.5005494671, 99948.19494954446, 99980.11271407397];
        double[] r = [1.1568272530151218, 1.155813678521176, 1.1550639182404743, 1.1537605364255448, 1.1511589396914892, 1.146090442420148, 1.1366923344885242, 1.136836336005783, 1.1067851691011843, 1.057878452415234, 0.9780239570965814, 0.9780289816664521, 0.7930082269453095];

        var engine = EightValveEngine();
        var table = engine.Manifold.InletPipe.AreaVersusLength;
        var whole = new PipeGeometry(table, clampPastEnd: true);
        var layout = PipeLayout.Build(whole, table, 11);
        var valve = ValveMotion.FromValve(engine.Manifold.InletValve);

        var current = new PipeGrid(EsaLimits.InletGridPoints) { ActiveCount = x.Length };
        var target = new PipeGrid(EsaLimits.InletGridPoints) { ActiveCount = x.Length };

        for (var i = 0; i < x.Length; i++)
        {
            (current.X[i], current.Velocity[i], current.Pressure[i], current.Density[i]) = (x[i], u[i], p[i], r[i]);
            current.SpeedOfSound[i] = Math.Sqrt(gamma * p[i] / r[i]);
            target.X[i] = x[i];
        }

        var stale = new InletValveReverseBoundary.ThroatState(
            MachNumber: 1, Velocity: -372.6553226360287, SpeedOfSound: 372.6553226360287,
            Density: 0.8846326975878107, Pressure: 90842.61179122489, DischargeCoefficient: 0.010845883293365444);

        var throat = fresh ? ManifoldSolver.FreshThroat(stale, cylinderPressure) : stale;

        try
        {
            var result = InletValveOpenBoundary.Apply(
                current, target, layout.Geometry[^1], valve, 8.333333333333333E-05,
                cylinderPressure, cylinderTemperature: 965.5983541935658, crankAngle: 699,
                pipeAreaAtValve: whole.Area(whole.Length), valveFlowArea: 4.7316001584821814E-07,
                throat, tuning, gamma, wholeSubsonicBracket: true, stallBelowThroat: true, releaseLatch: true);

            return (null, target.Velocity[x.Length - 1], result.Velocity);
        }
        catch (CfdException ex)
        {
            return (ex, double.NaN, double.NaN);
        }
    }

    /// <summary>
    /// B84's oracle: gas does not flow from the lower pressure to the higher. With the
    /// eight-valve engine's IVF and IVFR of zero and a pipe end at rest, neither of the
    /// first pass's tests resets the stale throat. The original measures the pipe end
    /// against it, finds it higher, and drives inflow into a cylinder 34 kPa above the pipe
    /// until its secant goes non-finite. A throat that starts at rest at the cylinder's
    /// pressure is reset by the first test, and the cylinder blows back into the pipe.
    /// </summary>
    [Fact]
    public void AnOpeningValveBlowsBackIntoTheLowerPipe()
    {
        BaselinePaths.Require();

        var original = Open(fresh: false, (0, 0, 0));
        Assert.True(
            original.Failure is not null || original.PipeEndVelocity > 0,
            $"{original.Failure?.Message} {original.PipeEndVelocity}");

        var corrected = Open(fresh: true, (0, 0, 0));
        Assert.Null(corrected.Failure);
        Assert.True(corrected.PipeEndVelocity < 0, $"{corrected.PipeEndVelocity}");
        Assert.True(corrected.ThroatVelocity < 0, $"{corrected.ThroatVelocity}");
    }

    /// <summary>
    /// With a negative IVFR, as the baseline engine has, the pipe end at rest passes the first
    /// pass's second test and the original resets the throat itself: the fresh throat
    /// changes which way nothing flows.
    /// </summary>
    [Fact]
    public void WithANegativeIvfrTheOriginalAlreadyBlowsBack()
    {
        BaselinePaths.Require();

        var tuning = (0.645, -0.044, -0.24);
        var original = Open(fresh: false, tuning);
        var corrected = Open(fresh: true, tuning);

        Assert.Null(original.Failure);
        Assert.Null(corrected.Failure);
        Assert.True(original.PipeEndVelocity < 0, $"{original.PipeEndVelocity}");
        Assert.True(corrected.PipeEndVelocity < 0, $"{corrected.PipeEndVelocity}");
    }
}
