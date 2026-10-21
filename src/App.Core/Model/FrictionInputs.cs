namespace App.Core.Model;

/// <summary>
/// The valvetrain mechanisms of Sandoval's Table 4.2, each with its own constants.
/// No Delphi counterpart: the original's friction is a curve in speed alone
/// (ISSUES.md B88).
/// </summary>
public enum ValvetrainMechanism
{
    SohcFingerFollower,
    SohcRockerArm,
    SohcDirectActing,
    DohcFingerFollower,
    DohcDirectActing,
    Ohv,
}

/// <summary>Whether the cam lobe slides on its follower or rolls on it.</summary>
public enum CamFollower
{
    Flat,
    Roller,
}

/// <summary>
/// The inputs of the mechanical friction model that the <c>.eng</c> schema has no other
/// place for, read from optional <c>[Friction]</c> keys. Each is null when the file does
/// not give it, and <c>FrictionGeometry.FromEngine</c> then supplies the stated default.
/// No Delphi counterpart (ISSUES.md B88).
/// </summary>
/// <remarks>Lengths are millimetres, as the model's equations take them.</remarks>
public sealed class FrictionInputs
{
    /// <summary>Main bearing diameter, mm. Sandoval <c>D_b</c>.</summary>
    public double? MainBearingDiameter { get; set; }

    /// <summary>Main bearing length, mm. Sandoval <c>L_b</c>.</summary>
    public double? MainBearingLength { get; set; }

    /// <summary>Number of main bearings in the engine. Sandoval <c>n_b</c>.</summary>
    public int? MainBearingCount { get; set; }

    /// <summary>Connecting rod bearing diameter, mm.</summary>
    public double? RodBearingDiameter { get; set; }

    /// <summary>Connecting rod bearing length, mm.</summary>
    public double? RodBearingLength { get; set; }

    /// <summary>Number of camshaft bearings in the engine, every camshaft together.</summary>
    public int? CamshaftBearingCount { get; set; }

    public ValvetrainMechanism? Valvetrain { get; set; }

    public CamFollower? Follower { get; set; }

    /// <summary>
    /// The oil's viscosity over the 10W-30 at 90 °C the model was calibrated with,
    /// Sandoval's <c>mu/mu0</c>. Default 1: a warm engine on the reference oil.
    /// </summary>
    public double? OilViscosityRatio { get; set; }

    /// <summary>Piston ring tension over the reference, Sandoval's <c>F_t/F_t0</c>.</summary>
    public double? RingTensionRatio { get; set; }

    /// <summary>Liner roughness factor, Sandoval's <c>C_r</c>.</summary>
    public double? RingRoughnessFactor { get; set; }
}
