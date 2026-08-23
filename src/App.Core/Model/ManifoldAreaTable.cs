namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TAManf</c> (FManfA.pas): a one-dimensional area-versus-length
/// table of at most 50 points, linearly interpolated, loaded from a <c>.maf</c> file.
/// </summary>
public sealed class ManifoldAreaTable
{
    /// <summary>Areas, <c>Cell</c>.</summary>
    public double[] Area { get; } = new double[EsaLimits.MaxManifoldAreaPoints];

    /// <summary>Positions along the pipe, <c>Index</c>.</summary>
    public double[] Position { get; } = new double[EsaLimits.MaxManifoldAreaPoints];

    public int Count { get; set; }

    public string FileName { get; set; } = string.Empty;
}
