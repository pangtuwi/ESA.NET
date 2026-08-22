namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TGas2Z</c> (Gasses2Z.pas): the state of one gas volume, split
/// into burnt and unburnt zones.
/// </summary>
public sealed class Gas
{
    public double PGas { get; set; }

    public double MGas { get; set; }

    /// <summary>Burnt mass, <c>mb</c>.</summary>
    public double Mb { get; set; }

    /// <summary>Unburnt mass, <c>mu</c>.</summary>
    public double Mu { get; set; }

    public double VGas { get; set; }

    /// <summary>Unburnt volume, <c>Vu</c>.</summary>
    public double Vu { get; set; }

    /// <summary>
    /// Burnt volume, <c>Vb</c>. SPEC.md section 5 records the clamp
    /// <c>if Vb &gt; Vgas then Vb := Vgas</c> as an intentional safeguard that must
    /// remain when the behaviour is ported.
    /// </summary>
    public double Vb { get; set; }

    public double DvDTheta { get; set; }

    /// <summary>Burnt mass fraction, <c>xb</c>.</summary>
    public double Xb { get; set; }

    /// <summary>Burnt-zone temperature.</summary>
    public double Tb { get; set; }

    /// <summary>Unburnt-zone temperature.</summary>
    public double Tu { get; set; }

    public double UGas { get; set; }

    public double HGas { get; set; }

    public double RGas { get; set; }

    public double DmbDTheta { get; set; }

    public double DmInDTheta { get; set; }

    public double DmOutDTheta { get; set; }

    public double HIn { get; set; }

    public double Gamma { get; set; }

    public Fuel Fuel { get; } = new();

    public double Rb { get; set; }

    public double Hb { get; set; }

    public double Ub { get; set; }

    public double Cpb { get; set; }

    public double DuDtb { get; set; }

    public double DuDpb { get; set; }

    public double DuDfb { get; set; }

    public double Ru { get; set; }

    public double Hu { get; set; }

    public double Uu { get; set; }

    public double Cpu { get; set; }

    public double DuDtu { get; set; }

    public double DuDpu { get; set; }

    public double DuDfu { get; set; }

    public int Error { get; set; }

    public double ThetaSpark { get; set; }

    public GasProperties Burnt { get; } = new();

    public GasProperties Unburnt { get; } = new();
}
