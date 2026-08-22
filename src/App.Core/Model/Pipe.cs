namespace App.Core.Model;

/// <summary>Port of Delphi <c>TPipe</c> (Pipes.pas).</summary>
public sealed class Pipe
{
    /// <summary>Area versus length, <c>AvsL</c>.</summary>
    public ManifoldAreaTable AreaVersusLength { get; } = new();

    /// <summary>Length of the inserted section, <c>InsertL</c>.</summary>
    public double InsertLength { get; set; }

    /// <summary>Position of the inserted section, <c>InsertAt</c>.</summary>
    public double InsertAt { get; set; }
}
