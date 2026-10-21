using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// An engine's friction model inputs with every default resolved, in the units the
/// equations take: millimetres, and counts for the whole engine.
/// </summary>
/// <param name="Bore">Sandoval <c>B</c>, mm.</param>
/// <param name="Stroke">Sandoval <c>S</c>, mm.</param>
/// <param name="CylinderCount">Sandoval <c>n_c</c>.</param>
/// <param name="CompressionRatio">Sandoval <c>r_c</c>.</param>
/// <param name="ValveCount">Every valve in the engine, Sandoval <c>n_v</c>.</param>
/// <param name="ValveLift">The valves' mean maximum lift, Sandoval <c>L_v</c>, mm.</param>
public sealed record FrictionGeometry(
    double Bore,
    double Stroke,
    int CylinderCount,
    double CompressionRatio,
    int ValveCount,
    double ValveLift,
    double MainBearingDiameter,
    double MainBearingLength,
    int MainBearingCount,
    double RodBearingDiameter,
    double RodBearingLength,
    int CamshaftBearingCount,
    ValvetrainMechanism Valvetrain,
    CamFollower Follower,
    double OilViscosityRatio,
    double RingTensionRatio,
    double RingRoughnessFactor)
{
    /// <summary>Main bearing diameter over bore when the file gives none. An assumption, not a published value.</summary>
    public const double DefaultMainBearingDiameterRatio = 0.67;

    /// <summary>Main bearing length over bore when the file gives none. An assumption.</summary>
    public const double DefaultMainBearingLengthRatio = 0.25;

    /// <summary>Rod bearing diameter over bore when the file gives none. An assumption.</summary>
    public const double DefaultRodBearingDiameterRatio = 0.59;

    /// <summary>Rod bearing length over bore when the file gives none. An assumption.</summary>
    public const double DefaultRodBearingLengthRatio = 0.22;

    /// <summary>
    /// Resolves an engine's friction inputs. What the file does not give is defaulted:
    /// bearing sizes as fixed fractions of the bore, typical of the 1.6 L engines the
    /// validation suite runs but taken from no published source; an in-line block's
    /// cylinders plus one main bearings, one rod bearing a cylinder, and cylinders plus
    /// one bearings for each camshaft; direct-acting flat followers, single overhead cam
    /// at two valves a cylinder and double above; and the reference oil and rings, so
    /// that every factor the thesis fitted to its own engines is 1 (ISSUES.md B88).
    /// </summary>
    public static FrictionGeometry FromEngine(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var inputs = engine.Friction;
        var bore = engine.Bore * 1000;
        var cylinders = (int)engine.CylinderCount;
        var inlet = engine.Manifold.InletValve;
        var exhaust = engine.Manifold.ExhaustValve;
        var valvesPerCylinder = inlet.Count + exhaust.Count;

        var valvetrain = inputs.Valvetrain
                         ?? (valvesPerCylinder > 2 ? ValvetrainMechanism.DohcDirectActing : ValvetrainMechanism.SohcDirectActing);
        var camshafts = valvetrain is ValvetrainMechanism.DohcFingerFollower or ValvetrainMechanism.DohcDirectActing ? 2 : 1;

        var lift = valvesPerCylinder == 0
            ? 0
            : ((inlet.Count * inlet.MaxLift) + (exhaust.Count * exhaust.MaxLift)) / valvesPerCylinder * 1000;

        return new FrictionGeometry(
            Bore: bore,
            Stroke: engine.Stroke * 1000,
            CylinderCount: cylinders,
            CompressionRatio: engine.CompressionRatio,
            ValveCount: valvesPerCylinder * cylinders,
            ValveLift: lift,
            MainBearingDiameter: inputs.MainBearingDiameter ?? DefaultMainBearingDiameterRatio * bore,
            MainBearingLength: inputs.MainBearingLength ?? DefaultMainBearingLengthRatio * bore,
            MainBearingCount: inputs.MainBearingCount ?? cylinders + 1,
            RodBearingDiameter: inputs.RodBearingDiameter ?? DefaultRodBearingDiameterRatio * bore,
            RodBearingLength: inputs.RodBearingLength ?? DefaultRodBearingLengthRatio * bore,
            CamshaftBearingCount: inputs.CamshaftBearingCount ?? camshafts * (cylinders + 1),
            Valvetrain: valvetrain,
            Follower: inputs.Follower ?? CamFollower.Flat,
            OilViscosityRatio: inputs.OilViscosityRatio ?? 1,
            RingTensionRatio: inputs.RingTensionRatio ?? 1,
            RingRoughnessFactor: inputs.RingRoughnessFactor ?? 1);
    }
}

/// <summary>Each component's friction mean effective pressure, Pa.</summary>
public sealed record FrictionBreakdown(
    double Crankshaft,
    double Reciprocating,
    double ReciprocatingGasLoading,
    double Valvetrain,
    double Auxiliary)
{
    /// <summary>The mechanical and auxiliary friction together, Pa. No pumping.</summary>
    public double Total => Crankshaft + Reciprocating + ReciprocatingGasLoading + Valvetrain + Auxiliary;
}

/// <summary>
/// The rubbing and auxiliary friction of a spark-ignition engine from its design: Patton,
/// Nitschke and Heywood's model (SAE 890836) as Sandoval and Heywood revised it (D.
/// Sandoval, <i>An Improved Friction Model for Spark Ignition Engines</i>, B.S. thesis,
/// MIT, 2002; SAE 2003-01-0725). No Delphi counterpart: the original's friction is
/// <c>TEngine2z.TFMEP</c>, a curve in speed alone (ISSUES.md B88).
/// </summary>
/// <remarks>
/// <para>
/// The thesis's equations take lengths in millimetres, speed in rev/min, mean piston speed
/// in m/s, and give kilopascals; this converts the result to pascals. Equation and page
/// numbers below are the thesis's.
/// </para>
/// <para>
/// The model's pumping terms (12, 13) are left out. The cycle simulation computes the
/// pumping work from the cylinder pressure itself, and counting both would count it twice.
/// </para>
/// </remarks>
public static class FrictionModel
{
    /// <summary>The ring gas-loading exponent's constant, <c>K</c>, p. 36. Equation 9.b doubles it.</summary>
    public const double RingGasLoadingK = 2.38e-2;

    /// <summary>Valvetrain constants <c>C_ff</c>, <c>C_rf</c>, <c>C_oh</c>, <c>C_om</c>: Table 4.2, p. 22.</summary>
    /// <remarks>The OHV roller constant is printed as 0.5, out of line with the others; kept as printed.</remarks>
    public static (double FlatFollower, double RollerFollower, double OscillatingHydrodynamic, double OscillatingMixed)
        ValvetrainConstants(ValvetrainMechanism mechanism) => mechanism switch
        {
            ValvetrainMechanism.SohcFingerFollower => (600, 0.0227, 0.2, 42.8),
            ValvetrainMechanism.SohcRockerArm => (400, 0.0151, 0.5, 21.4),
            ValvetrainMechanism.SohcDirectActing => (200, 0.0076, 0.5, 10.7),
            ValvetrainMechanism.DohcFingerFollower => (600, 0.0227, 0.2, 25.8),
            ValvetrainMechanism.DohcDirectActing => (133, 0.0050, 0.5, 10.7),
            ValvetrainMechanism.Ohv => (400, 0.5, 0.5, 32.1),
            _ => throw new ArgumentOutOfRangeException(nameof(mechanism)),
        };

    /// <param name="geometry">The engine.</param>
    /// <param name="rpm">Engine speed, Sandoval <c>N</c>.</param>
    /// <param name="intakeOverAmbient">Intake manifold over atmospheric pressure, <c>p_i/p_a</c>.</param>
    public static FrictionBreakdown Calculate(FrictionGeometry geometry, double rpm, double intakeOverAmbient)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var b = geometry.Bore;
        var s = geometry.Stroke;
        var nc = (double)geometry.CylinderCount;
        var n = rpm;
        var viscosity = Math.Sqrt(geometry.OilViscosityRatio);
        var pistonSpeed = 2 * s / 1000 * n / 60;
        var ring = geometry.RingTensionRatio;

        // Equation 7.b, p. 13: main bearing seals, main bearing hydrodynamic friction and
        // turbulent dissipation.
        var mainD = geometry.MainBearingDiameter;
        var mainL = geometry.MainBearingLength;
        var mains = geometry.MainBearingCount;
        var crankshaft = (1.22e5 * mainD / (b * b * s * nc))
                         + (3.03e-4 * viscosity * n * mainD * mainD * mainD * mainL * mains / (b * b * s * nc))
                         + (1.35e-10 * mainD * mainD * n * n * mains / nc);

        // Equation 8.b, p. 14: piston skirt, rings without gas loading, rod bearings.
        var rodD = geometry.RodBearingDiameter;
        var rodL = geometry.RodBearingLength;
        var rods = geometry.CylinderCount;
        var reciprocating = (2.94e2 * viscosity * pistonSpeed / b)
                            + (4.06e4 * ring * geometry.RingRoughnessFactor * (1 + (500 / n)) / (b * b))
                            + (3.03e-4 * viscosity * n * rodD * rodD * rodD * rodL * rods / (b * b * s * nc));

        // Equation 9.b, p. 15: the rings' extra friction under gas pressure.
        var rc = geometry.CompressionRatio;
        var gasLoading = 6.89 * intakeOverAmbient
                         * ((0.088 * viscosity * rc)
                            + (0.182 * ring * Math.Pow(rc, 1.33 - (2 * RingGasLoadingK * pistonSpeed))));

        // Equation 10.b, p. 16, with the 4.12 kPa the camshaft bearing seals add (p. 15).
        var (cff, crf, coh, com) = ValvetrainConstants(geometry.Valvetrain);
        var nv = (double)geometry.ValveCount;
        var lift = geometry.ValveLift;
        var follower = geometry.Follower == CamFollower.Flat
            ? cff * (1 + (500 / n)) * nv / (s * nc)
            : crf * n * nv / (s * nc);
        var valvetrain = (244 * viscosity * n * geometry.CamshaftBearingCount / (b * b * s * nc))
                         + 4.12
                         + follower
                         + (coh * viscosity * Math.Pow(lift, 1.5) * Math.Sqrt(n) * nv / (b * s * nc))
                         + (com * (1 + (500 / n)) * lift * nv / (s * nc));

        // Equation 11, p. 16: oil pump, water pump and non-charging alternator.
        var auxiliary = 6.23 + (5.22e-3 * n) - (1.79e-7 * n * n);

        const double kPa = 1000;
        return new FrictionBreakdown(
            crankshaft * kPa, reciprocating * kPa, gasLoading * kPa, valvetrain * kPa, auxiliary * kPa);
    }
}
