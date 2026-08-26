using App.Core.Model;

namespace App.Core.Simulation;

/// <summary>
/// Slider-crank geometry for one cylinder. Port of the private geometry methods on
/// Delphi <c>TEngine2z</c> (ICEngine2Z.pas lines 96-165): <c>VCyl</c>,
/// <c>dVCyldTheta</c>, <c>dVCyldt</c>, <c>WallArea</c> and <c>CapacityCC</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Units.</b> Everything here is SI: metres in, cubic metres out, exactly as the
/// Delphi engine object works. <see cref="Model.Engine"/> is not: it carries the values
/// as the <c>.eng</c> file writes them, which for bore, stroke and conrod length is
/// millimetres. Delphi converts on the way out of the edit form
/// (<c>Edit.pas:417-419</c>, <c>Bore := StrToFloatf(EBore.Text)/1000</c>), and the port
/// has no edit form in the loop, so <see cref="FromEngine"/> is where that conversion
/// happens. See ISSUES.md A6.
/// </para>
/// <para>
/// The original's <c>VCyl</c> opens with a guard that pops a dialog when handed a crank
/// angle above 4*pi, on the theory that the caller passed degrees. Core cannot show a
/// dialog and the original carries on regardless of the answer, so the guard is dropped
/// rather than reproduced.
/// </para>
/// </remarks>
public sealed class CylinderGeometry
{
    private readonly double _bore;
    private readonly double _stroke;
    private readonly double _compressionRatio;
    private readonly double _conrodLength;
    private readonly double _cylinderCount;

    /// <param name="bore">Bore in metres, Delphi <c>Bore</c>.</param>
    /// <param name="stroke">Stroke in metres, Delphi <c>Stroke</c>.</param>
    /// <param name="compressionRatio">Delphi <c>CR</c>.</param>
    /// <param name="conrodLength">Connecting rod length in metres, Delphi <c>ConrodLength</c>.</param>
    /// <param name="cylinderCount">Delphi <c>NCyl</c>, a Double in the original.</param>
    public CylinderGeometry(
        double bore,
        double stroke,
        double compressionRatio,
        double conrodLength,
        double cylinderCount)
    {
        _bore = bore;
        _stroke = stroke;
        _compressionRatio = compressionRatio;
        _conrodLength = conrodLength;
        _cylinderCount = cylinderCount;
    }

    /// <summary>
    /// Builds the geometry from a loaded engine, converting the three length fields from
    /// the millimetres the <c>.eng</c> file holds into the metres the physics needs.
    /// </summary>
    public static CylinderGeometry FromEngine(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return new CylinderGeometry(
            engine.Bore / 1000,
            engine.Stroke / 1000,
            engine.CompressionRatio,
            engine.ConrodLength / 1000,
            engine.CylinderCount);
    }

    /// <summary>Bore in metres.</summary>
    public double Bore => _bore;

    /// <summary>Stroke in metres.</summary>
    public double Stroke => _stroke;

    /// <summary>Compression ratio, Delphi <c>CR</c>.</summary>
    public double CompressionRatio => _compressionRatio;

    /// <summary>
    /// Swept volume of one cylinder in cubic metres, Delphi <c>Vd</c> as
    /// <c>InitVars</c> computes it.
    /// </summary>
    public double SweptVolume => Math.PI * _bore * _bore * _stroke / 4;

    /// <summary>
    /// Piston area in square metres. Not a Delphi member: the three heat-transfer
    /// routines each recompute <c>pi*sqr(Bore)/4</c> locally.
    /// </summary>
    public double PistonArea => Math.PI * _bore * _bore / 4;

    /// <summary>
    /// Instantaneous cylinder volume in cubic metres. Port of <c>TEngine2z.VCyl</c>.
    /// </summary>
    /// <param name="crankAngleRadians">Crank angle in radians, zero at firing TDC.</param>
    public double Volume(double crankAngleRadians)
    {
        var rodRatio = 2 * _conrodLength / _stroke;

        return ((Math.PI / 4 * _bore * _bore * _stroke) / (_compressionRatio - 1))
               * (1 + ((_compressionRatio - 1) / 2
                   * (1 + rodRatio
                        - Math.Cos(crankAngleRadians)
                        - Math.Sqrt((rodRatio * rodRatio)
                                    - (Math.Sin(crankAngleRadians) * Math.Sin(crankAngleRadians))))));
    }

    /// <summary>
    /// Rate of change of cylinder volume per radian of crank angle. Port of
    /// <c>TEngine2z.dVCyldTheta</c>.
    /// </summary>
    public double VolumeRatePerRadian(double crankAngleRadians)
    {
        var halfRodRatio = _stroke / 2 / _conrodLength;
        var sin = Math.Sin(crankAngleRadians);

        return Math.PI / 8 * _bore * _bore * _stroke * sin
               * (1 + ((halfRodRatio * Math.Cos(crankAngleRadians))
                       / Math.Sqrt(1 - (halfRodRatio * sin * halfRodRatio * sin))));
    }

    /// <summary>
    /// Rate of change of cylinder volume per second. Port of <c>TEngine2z.dVCyldt</c>.
    /// </summary>
    public double VolumeRatePerSecond(double crankAngleRadians, double rpm) =>
        VolumeRatePerRadian(crankAngleRadians) * 2 * Math.PI * (rpm / 60);

    /// <summary>
    /// Side wall area exposed to the gas, in square metres. Port of
    /// <c>TEngine2z.WallArea</c>: the cylinder volume over a quarter of the bore, which
    /// is the lateral area of a cylinder of that volume.
    /// </summary>
    public double WallArea(double crankAngleRadians) => Volume(crankAngleRadians) * 4 / _bore;

    /// <summary>
    /// Total engine capacity in cubic centimetres. Port of
    /// <c>TEngine2z.CapacityCC</c>.
    /// </summary>
    public double CapacityCc() => _cylinderCount * (Math.PI * _bore * _bore / 4) * _stroke * 1E6;
}
