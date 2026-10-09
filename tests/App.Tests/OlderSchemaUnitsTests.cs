using App.Core;
using App.Core.Expressions;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// The older <c>[InManifold]</c> / <c>[ExManifold]</c> schema writes plenum pressure in
/// kilopascals, wall temperatures in Celsius and grid sizes as fixed counts under
/// <c>[Calculation]</c>, as the predecessor's <c>Edit.pas</c> read them. ISSUES.md A31-A33.
/// </summary>
public sealed class OlderSchemaUnitsTests
{
    private static EngineLoader CreateLoader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static string A4LowCost => Path.Combine(TestPaths.Legacy!, "CAEEng", "A4LowCost.eng");

    private static string Nissan(int number) =>
        Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Example1", $"Nissan{number}.eng");

    private static void RequireLegacy() =>
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

    private static IniEngineDefinition Definition(string text) => new(IniDocument.Parse(text));

    [Fact]
    public void TheOlderPlenumPressureReachesTheSolverInPascals()
    {
        RequireLegacy();

        // PlenumP=99 is kPa; Edit.pas did PlenumP6000 := StrToFloatF(EPPlenum.Text)*1e3.
        var engine = CreateLoader().Load(A4LowCost).Engine;

        Assert.Equal("99000", engine.Manifold.PlenumPressureFunction.Expression);
    }

    [Fact]
    public void TheOlderWallTemperaturesAreConvertedFromCelsiusToKelvin()
    {
        RequireLegacy();

        // THead=130, TPiston=200, TULiner=160, TLLiner=130, all Celsius; Edit.pas added
        // 273.15 to each, and a .cwt, which the solver reads alike, holds kelvin.
        var walls = CreateLoader().Load(A4LowCost).Engine.WallTemperature;

        Assert.Equal("(inline)", walls.FileName);
        Assert.Equal(403.15, walls.HeadTemperature[0], 10);
        Assert.Equal(473.15, walls.PistonTemperature[0], 10);
        Assert.Equal(433.15, walls.UpperLinerTemperature[0], 10);
        Assert.Equal(403.15, walls.LowerLinerTemperature[0], 10);
    }

    [Fact]
    public void TheOlderFixedGridSizesAreUsedAndFitTheLimits()
    {
        RequireLegacy();

        // [Calculation] InletGrid=13, ExhaustGrid=14 with VarInletGrid=0, VarExhGrid=0.
        // The default of 50 the port fell back to is over the 38-point exhaust limit.
        var manifold = CreateLoader().Load(A4LowCost).Engine.Manifold;

        Assert.Equal("13", manifold.InletGrid.Expression);
        Assert.Equal("14", manifold.ExhaustGrid.Expression);

        var grids = new GridSizeCalculator(new CachingExpressionEvaluator());
        Assert.Equal(13, grids.InletGridSize(manifold.InletGrid.Expression, 0.658, 6000));
        Assert.Equal(14, grids.ExhaustGridSize(manifold.ExhaustGrid.Expression, 0.658, 6000));
    }

    [Fact]
    public void TheInlineExhaustBackPressureIsKeptInTheExhFilesOwnUnits()
    {
        RequireLegacy();

        // ExhBackP=20 (kPa gauge) and ExhT=400 (Celsius), the units of an .exh row.
        var back = CreateLoader().Load(A4LowCost).Engine.Manifold.ExhaustBack;

        Assert.Equal("(inline)", back.FileName);
        Assert.Equal(20, back.Pressure[0]);
        Assert.Equal(400, back.Temperature[0]);
    }

    [Fact]
    public void InAFileCarryingBothSchemasTheCurrentKeysWin()
    {
        RequireLegacy();

        // Nissan1.eng has PlenumP=94.0 and FPlenumP=99000, THead=180 and a TempFile,
        // ExhBackP=40.0 and an [Exhaust] ExhBackFile, and grid expressions under [Inlet]
        // and [Exhaust]. ESA 3.0 read only the current keys (Edit.pas:258-270).
        var result = CreateLoader().Load(Nissan(1));
        var engine = result.Engine;

        Assert.True(result.Definition.UsesOlderManifoldSchema);
        Assert.Equal("99000", engine.Manifold.PlenumPressureFunction.Expression);
        Assert.Equal("(0.0096*N+4.703)*L/0.8", engine.Manifold.InletGrid.Expression);
        Assert.Equal("(0.0048*N+1.5)*L/1", engine.Manifold.ExhaustGrid.Expression);

        Assert.True(result.Definition.HasInlineWallTemperatures);
        Assert.False(result.Definition.UsesInlineWallTemperatures);
        Assert.NotEqual("(inline)", engine.WallTemperature.FileName);

        Assert.True(result.Definition.HasInlineExhaustBackPressure);
        Assert.False(result.Definition.UsesInlineExhaustBackPressure);
        Assert.NotEqual("(inline)", engine.Manifold.ExhaustBack.FileName);
    }

    [Fact]
    public void AnOlderFileWithNoGridSizesKeepsTheDefault()
    {
        RequireLegacy();

        // Nissan5.eng has neither [Inlet]/[Exhaust] nor [Calculation]; Edit.pas's own
        // default is 50 for both, which is what it gets.
        var result = CreateLoader().Load(Nissan(5));

        Assert.Equal("97000", result.Engine.Manifold.PlenumPressureFunction.Expression);
        Assert.Equal("50", result.Engine.Manifold.InletGrid.Expression);
        Assert.Equal("50", result.Engine.Manifold.ExhaustGrid.Expression);
    }

    [Fact]
    public void AVariableGridInTheOlderSchemaGivesNoFixedCount()
    {
        // VarInletGrid=1 made the predecessor read Inlet.grd instead of the count.
        var definition = Definition(
            "[InManifold]\nPlenumP=99\n[Calculation]\nInletGrid=13\nExhaustGrid=14\nVarInletGrid=1\nVarExhGrid=0\n");

        Assert.Equal("50", definition.EffectiveInletGridFunction);
        Assert.Equal("14", definition.EffectiveExhaustGridFunction);
    }

    [Fact]
    public void ACalculationGridOutsideTheOlderSchemaIsNotRead()
    {
        // Only the older schema kept grid sizes under [Calculation].
        var definition = Definition("[Calculation]\nInletGrid=13\nExhaustGrid=14\n");

        Assert.Equal("50", definition.EffectiveInletGridFunction);
        Assert.Equal("50", definition.EffectiveExhaustGridFunction);
    }

    [Fact]
    public void AnUnparseableOlderPlenumPressureIsScaledAsAnExpression()
    {
        var definition = Definition("[InManifold]\nPlenumP=97+2\n");

        Assert.Equal("(97+2)*1000", definition.EffectivePlenumPressure);
    }

    [Fact]
    public void TheAccessorsWriteNothing()
    {
        RequireLegacy();

        // The translations are read-side only: the file is exactly as it was.
        var bytes = File.ReadAllBytes(A4LowCost);
        var definition = new IniEngineDefinition(IniDocument.Parse(bytes));

        _ = definition.EffectivePlenumPressure;
        _ = definition.EffectiveInletGridFunction;
        _ = definition.EffectiveExhaustGridFunction;
        _ = definition.UsesInlineWallTemperatures;
        _ = definition.UsesInlineExhaustBackPressure;

        Assert.Equal(bytes, definition.Document.ToBytes());
    }
}
