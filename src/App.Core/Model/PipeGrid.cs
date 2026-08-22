namespace App.Core.Model;

/// <summary>
/// One pipe's characteristic-line state arrays. Ports the parallel
/// <c>TInletCalcArray</c> / <c>TExhaustCalcArray</c> fields of Delphi
/// <c>TManifolds</c> (Manifolds.pas).
/// </summary>
/// <remarks>
/// The capacity is fixed at construction: 68 points for the inlet and 38 for the
/// exhaust. SPEC.md section 6 records these as intentional legacy design limits
/// retained for the new software, so the type keeps them rather than growing.
/// </remarks>
public sealed class PipeGrid
{
    public PipeGrid(int capacity)
    {
        Capacity = capacity;
        X = new double[capacity];
        Velocity = new double[capacity];
        Pressure = new double[capacity];
        Density = new double[capacity];
        SpeedOfSound = new double[capacity];
        Temperature = new double[capacity];
    }

    public int Capacity { get; }

    /// <summary>Active point count, Delphi <c>QI</c> or <c>QE</c>.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Position along the pipe, Delphi <c>XInlet</c> / <c>XExhaust</c>.</summary>
    public double[] X { get; }

    /// <summary>Gas velocity, Delphi <c>uInlet</c> / <c>uExhaust</c>.</summary>
    public double[] Velocity { get; }

    /// <summary>Pressure, Delphi <c>PInlet</c> / <c>PExhaust</c>.</summary>
    public double[] Pressure { get; }

    /// <summary>Density, Delphi <c>RInlet</c> / <c>RExhaust</c>.</summary>
    public double[] Density { get; }

    /// <summary>Speed of sound, Delphi <c>cInlet</c> / <c>cExhaust</c>.</summary>
    public double[] SpeedOfSound { get; }

    /// <summary>Temperature, Delphi <c>TempInlet</c> / <c>TempExhaust</c>.</summary>
    public double[] Temperature { get; }
}
