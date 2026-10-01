using App.Core.Model;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The conversions from the units an operator types into the SI an <see cref="Engine"/>
/// holds, which the loader and the multi-run grid both go through. See ISSUES.md A6.
/// </summary>
public sealed class EngineFileUnitsTests
{
    [Fact]
    public void ValveTimingsBecomeSolverCrankAngles()
    {
        // The baseline engine's cam card: IVO 19 BTDC, IVC 80 ABDC, EVO 64 BBDC, EVC 37
        // ATDC (Edit.pas:448-451).
        Assert.Equal(341, EngineFileUnits.InletOpen(19));
        Assert.Equal(-100, EngineFileUnits.InletClose(80));
        Assert.Equal(116, EngineFileUnits.ExhaustOpen(64));
        Assert.Equal(-323, EngineFileUnits.ExhaustClose(37));
    }

    [Fact]
    public void LengthsBecomeMetres()
    {
        Assert.Equal(0.081, EngineFileUnits.Length(81), 15);
        Assert.Equal(0.00862, EngineFileUnits.Length(8.62), 15);
    }

    [Fact]
    public void ALoadedEngineHoldsTheConvertedValues()
    {
        BaselinePaths.Require();

        var loader = new EngineLoader(
            new EngineDefinitionStore(), new CamProfileReader(), new SpeedKeyedTableReader(),
            new WallTemperatureTableReader(), new ExhaustBackPressureTableReader(),
            new ManifoldAreaTableStore(), new DischargeCoefficientTableStore());

        var engine = loader.Load(BaselinePaths.File("A2China.eng")).Engine;
        var inlet = engine.Manifold.InletValve;
        var exhaust = engine.Manifold.ExhaustValve;

        // SI, as Delphi's Engine2z was once the edit form had converted it. Before A6 the
        // engine held the file's own 19, 80, 64, 37 and millimetres, and four different
        // places converted the timings at the point of use.
        Assert.Equal((341, -100), (inlet.OpenAngle, inlet.CloseAngle));
        Assert.Equal((116, -323), (exhaust.OpenAngle, exhaust.CloseAngle));
        Assert.Equal(0.00862, inlet.MaxLift, 15);
        Assert.Equal(0.0104, exhaust.MaxLift, 15);
        Assert.Equal(0.081, engine.Bore, 15);
        Assert.Equal(0.0774, engine.Stroke, 15);
    }
}
