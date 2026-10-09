using System.Globalization;
using App.Core;
using App.Core.Model;
using App.Persistence.Tables;

namespace App.Persistence;

/// <summary>
/// Builds an <see cref="Engine"/> from a <c>.eng</c> file and the side files it names.
/// </summary>
public sealed class EngineLoader : IEngineLoader
{
    private readonly IEngineDefinitionStore _definitions;
    private readonly ICamProfileReader _camProfiles;
    private readonly ISpeedKeyedTableReader _sparkMaps;
    private readonly IWallTemperatureTableReader _wallTemperatures;
    private readonly IExhaustBackPressureTableReader _exhaustBackPressures;
    private readonly IManifoldAreaTableStore _manifoldAreas;
    private readonly IDischargeCoefficientTableStore _dischargeCoefficients;

    public EngineLoader(
        IEngineDefinitionStore definitions,
        ICamProfileReader camProfiles,
        ISpeedKeyedTableReader sparkMaps,
        IWallTemperatureTableReader wallTemperatures,
        IExhaustBackPressureTableReader exhaustBackPressures,
        IManifoldAreaTableStore manifoldAreas,
        IDischargeCoefficientTableStore dischargeCoefficients)
    {
        _definitions = definitions;
        _camProfiles = camProfiles;
        _sparkMaps = sparkMaps;
        _wallTemperatures = wallTemperatures;
        _exhaustBackPressures = exhaustBackPressures;
        _manifoldAreas = manifoldAreas;
        _dischargeCoefficients = dischargeCoefficients;
    }

    public EngineLoadResult Load(string engineFilePath)
    {
        ArgumentNullException.ThrowIfNull(engineFilePath);

        return Rebuild(_definitions.Read(engineFilePath), engineFilePath);
    }

    /// <inheritdoc />
    public EngineLoadResult Rebuild(EngineDefinition definition, string engineFilePath)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(engineFilePath);

        var context = new LoadContext(new LegacyPathResolver(engineFilePath), [], []);
        var engine = new Engine();

        ApplyGeometry(engine, definition);
        ApplyFuelAndConditions(engine, definition);
        ApplyCalculationSettings(engine, definition);
        ApplyValveGeometry(engine, definition);
        ApplyExpressions(engine, definition);
        ApplyOlderSchemaValues(engine, definition);
        LoadSideFiles(engine, definition, context);

        // The same definition instance goes back out, so whoever is editing it - the edit
        // window holds a reference for as long as it is open - keeps a live one.
        return new EngineLoadResult(engine, definition, context.Problems, context.SideFiles);
    }

    private sealed record LoadContext(
        LegacyPathResolver Resolver, List<string> Problems, List<ResolvedSideFile> SideFiles);

    private static void ApplyGeometry(Engine engine, EngineDefinition definition)
    {
        engine.Name = definition.Name;
        engine.CylinderCount = definition.CylinderCount;
        // Millimetres in the file, metres on the engine, as the edit form converted them
        // (Edit.pas:417-419). See EngineFileUnits and ISSUES.md A6.
        engine.Bore = EngineFileUnits.Length(definition.Bore);
        engine.Stroke = EngineFileUnits.Length(definition.Stroke);
        engine.CompressionRatio = definition.CompressionRatio;
        engine.ConrodLength = EngineFileUnits.Length(definition.ConrodLength);
        engine.WoshiniCoefficient = definition.WoshiniCoefficient;

        // Only the older schema carries a firing order.
        engine.FireOrder = definition.GetValue("Cylinders", "FireOrder") ?? string.Empty;
    }

    private static void ApplyFuelAndConditions(Engine engine, EngineDefinition definition)
    {
        // Delphi copies one fuel definition into the plenum, cylinder and exhaust gases
        // (Edit.pas lines 475-495), so all of them share these values here too.
        foreach (var gas in (Gas[])[engine.Plenum, engine.Cylinder, engine.Exhaust, engine.Atmosphere])
        {
            gas.Fuel.AFRatio = definition.AirFuelRatio;
            // Same reasoning as the atmosphere above: these are physical quantities the
            // simulation reads directly, so they are converted here rather than at the
            // simulation boundary. Edit.pas:472-473 does TFuel + 273.15 and QFuel * 1E6.
            gas.Fuel.T = definition.FuelTemperature + 273.15;
            gas.Fuel.Q = definition.FuelCalorificValue * 1E6;
            gas.Fuel.Lambda = definition.Lambda;
            gas.Fuel.BurnAngle = definition.BurnAngle;
            gas.Fuel.C = definition.FuelCarbon;
            gas.Fuel.H = definition.FuelHydrogen;
            gas.Fuel.O = definition.FuelOxygen;
            gas.Fuel.N = definition.FuelNitrogen;
        }

        // SI on the engine, like everything else loaded here (ISSUES.md A6). PGas and Tu
        // on a Gas are also the fields the solver writes pascals and kelvin into every
        // step, so the file's kilopascals and Celsius could never have stayed in them.
        engine.Atmosphere.PGas = definition.AtmosphericPressure * 1000;
        engine.Atmosphere.Tu = definition.AtmosphericTemperature + 273.15;
        engine.OilViscosity = definition.OilViscosity;
    }

    private static void ApplyCalculationSettings(Engine engine, EngineDefinition definition)
    {
        engine.VariableGamma = definition.VariableGamma;
        engine.SaveManifoldData = definition.SaveManifoldData;
        engine.Manifold.SaveManifoldData = definition.SaveManifoldData;
        engine.Integration.Integrator = definition.Integrator;
    }

    private static void ApplyValveGeometry(Engine engine, EngineDefinition definition)
    {
        // Degrees either side of a dead centre become solver crank angles, millimetres
        // become metres, as the edit form converted them (Edit.pas:448-454, 465-466).
        var inlet = engine.Manifold.InletValve;
        inlet.OpenAngle = EngineFileUnits.InletOpen(definition.InletValveOpen);
        inlet.CloseAngle = EngineFileUnits.InletClose(definition.InletValveClose);
        inlet.MaxLift = EngineFileUnits.Length(definition.InletValveLift);
        inlet.Count = definition.InletValveCount;
        inlet.Diameter = EngineFileUnits.Length(definition.InletValveDiameter);
        inlet.ProfileFile = definition.InletValveProfileFile;

        var exhaust = engine.Manifold.ExhaustValve;
        exhaust.OpenAngle = EngineFileUnits.ExhaustOpen(definition.ExhaustValveOpen);
        exhaust.CloseAngle = EngineFileUnits.ExhaustClose(definition.ExhaustValveClose);
        exhaust.MaxLift = EngineFileUnits.Length(definition.ExhaustValveLift);
        exhaust.Count = definition.ExhaustValveCount;
        exhaust.Diameter = EngineFileUnits.Length(definition.ExhaustValveDiameter);
        exhaust.ProfileFile = definition.ExhaustValveProfileFile;
    }

    private static void ApplyExpressions(Engine engine, EngineDefinition definition)
    {
        var manifold = engine.Manifold;

        // Pascals and expressions under either schema: the older one's kilopascals and
        // fixed grid counts are translated by the accessors (ISSUES.md A31, A33).
        manifold.PlenumPressureFunction.Expression = definition.EffectivePlenumPressure;
        manifold.InletGrid.Expression = definition.EffectiveInletGridFunction;
        manifold.ExhaustGrid.Expression = definition.EffectiveExhaustGridFunction;

        manifold.InletValveReverse.Expression = definition.InletValveReverseFunction;
        manifold.InletValveForward.Expression = definition.InletValveForwardFunction;
        manifold.InletValveForwardReverse.Expression = definition.InletValveForwardReverseFunction;

        manifold.ExhaustValveReverse.Expression = definition.ExhaustValveReverseFunction;
        manifold.ExhaustValveForward.Expression = definition.ExhaustValveForwardFunction;
        manifold.ExhaustValveForwardReverse.Expression = definition.ExhaustValveForwardReverseFunction;
    }

    /// <summary>
    /// Values that only the older <c>[InManifold]</c> / <c>[ExManifold]</c> schema
    /// carries, used by the five Example1 Nissan engines and <c>A4LowCost.eng</c>.
    /// </summary>
    private static void ApplyOlderSchemaValues(Engine engine, EngineDefinition definition)
    {
        var manifold = engine.Manifold;

        manifold.InletPipe.InsertLength = definition.InletInsertLength;
        manifold.InletPipe.InsertAt = definition.InletInsertAt;
        manifold.ExhaustPipe.InsertLength = definition.ExhaustInsertLength;
        manifold.ExhaustPipe.InsertAt = definition.ExhaustInsertAt;

        // The older files list four wall temperatures directly instead of naming a .cwt.
        // They are Celsius and a .cwt is kelvin; the predecessor's Edit.pas added 273.15
        // to each, as this does (ISSUES.md A32).
        if (definition.UsesInlineWallTemperatures)
        {
            var table = new WallTemperatureTable { FileName = "(inline)" };
            table.Rpm.Add(0);
            table.HeadTemperature.Add(definition.InlineHeadTemperature + 273.15);
            table.PistonTemperature.Add(definition.InlinePistonTemperature + 273.15);
            table.UpperLinerTemperature.Add(definition.InlineUpperLinerTemperature + 273.15);
            table.LowerLinerTemperature.Add(definition.InlineLowerLinerTemperature + 273.15);
            engine.WallTemperature = table;
        }

        // Likewise a single exhaust back pressure and temperature instead of an .exh, in
        // the .exh's own units, kPa gauge and Celsius.
        if (definition.UsesInlineExhaustBackPressure)
        {
            var table = new ExhaustBackPressureTable { FileName = "(inline)" };
            table.Rpm.Add(0);
            table.Temperature.Add(definition.InlineExhaustTemperature);
            table.Pressure.Add(definition.InlineExhaustBackPressure);
            engine.Manifold.ExhaustBack = table;
        }
    }

    private void LoadSideFiles(Engine engine, EngineDefinition definition, LoadContext context)
    {
        var manifold = engine.Manifold;

        manifold.InletValve.Profile = Read(
            definition.InletValveProfileFile, "inlet cam profile", _camProfiles.Read, manifold.InletValve.Profile);
        manifold.ExhaustValve.Profile = Read(
            definition.ExhaustValveProfileFile, "exhaust cam profile", _camProfiles.Read,
            manifold.ExhaustValve.Profile);

        // The exhaust valve's forward and reverse tables are deliberately crossed:
        // ICEngine2Z.pas lines 998-1005 assign EV.CdForward from CdEvOut and
        // EV.CdReverse from CdEvIn, because forward flow through an exhaust valve is
        // outward. Straightening this out would silently change the physics.
        manifold.InletValve.CdForward = Read(
            definition.InletValveCdInwardFile, "inlet valve inward Cd", ReadGrid, manifold.InletValve.CdForward);
        manifold.InletValve.CdReverse = Read(
            definition.InletValveCdOutwardFile, "inlet valve outward Cd", ReadGrid, manifold.InletValve.CdReverse);
        manifold.ExhaustValve.CdForward = Read(
            definition.ExhaustValveCdOutwardFile, "exhaust valve outward Cd", ReadGrid,
            manifold.ExhaustValve.CdForward);
        manifold.ExhaustValve.CdReverse = Read(
            definition.ExhaustValveCdInwardFile, "exhaust valve inward Cd", ReadGrid,
            manifold.ExhaustValve.CdReverse);

        // EffectiveInletAreaFile falls back to the older [InManifold] section, which is
        // where the five Nissan engines keep theirs.
        manifold.InletPipe.AreaVersusLength = Read(
            definition.EffectiveInletAreaFile, "inlet manifold area", ReadArea,
            manifold.InletPipe.AreaVersusLength);
        manifold.ExhaustPipe.AreaVersusLength = Read(
            definition.EffectiveExhaustAreaFile, "exhaust manifold area", ReadArea,
            manifold.ExhaustPipe.AreaVersusLength);

        if (!definition.UsesInlineWallTemperatures)
        {
            engine.WallTemperature = Read(
                definition.WallTemperatureFile, "wall temperatures", _wallTemperatures.Read, engine.WallTemperature);
        }

        if (!definition.UsesInlineExhaustBackPressure)
        {
            manifold.ExhaustBack = Read(
                definition.ExhaustBackPressureFile, "exhaust back pressure", _exhaustBackPressures.Read,
                manifold.ExhaustBack);
        }

        // An older-schema file with no grid size of its own takes it from the predecessor's
        // Inlet.grd and Exhaust.grd beside it (ISSUES.md A35).
        if (definition.OlderInletGridFile is { } inletGrid)
        {
            manifold.InletGrid.Expression = Read(
                inletGrid, "inlet grid size", GridSizeFile.Read, manifold.InletGrid.Expression);
        }

        if (definition.OlderExhaustGridFile is { } exhaustGrid)
        {
            manifold.ExhaustGrid.Expression = Read(
                exhaustGrid, "exhaust grid size", GridSizeFile.Read, manifold.ExhaustGrid.Expression);
        }

        LoadSparkAngle(engine, definition, context);

        return;

        DischargeCoefficientTable ReadGrid(string path) => _dischargeCoefficients.Read(path).Table;

        ManifoldAreaTable ReadArea(string path) => _manifoldAreas.Read(path).Table;

        T Read<T>(string stored, string what, Func<string, T> read, T fallback) =>
            ReadSideFile(stored, what, read, fallback, context);
    }

    /// <summary>
    /// The <c>SparkAngle</c> key holds either a <c>.spk</c> file name or a bare number.
    /// Nissan5.eng writes <c>SparkAngle=10.0</c>, meaning one fixed angle at every speed;
    /// the other files name a spark map.
    /// </summary>
    private void LoadSparkAngle(Engine engine, EngineDefinition definition, LoadContext context)
    {
        var stored = definition.SparkAngleFile;

        if (string.IsNullOrWhiteSpace(stored))
        {
            return;
        }

        if (double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedAngle))
        {
            var table = new SpeedKeyedTable { FileName = "(fixed)" };
            table.Rpm.Add(0);
            table.Values.Add(fixedAngle);
            engine.SparkAngle = table;
            return;
        }

        engine.SparkAngle = ReadSideFile(stored, "spark map", _sparkMaps.Read, engine.SparkAngle, context);
    }

    private static T ReadSideFile<T>(string stored, string what, Func<string, T> read, T fallback, LoadContext context)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return fallback;
        }

        var path = context.Resolver.Resolve(stored);

        if (path is null)
        {
            context.Problems.Add($"Could not find the {what} file '{stored}'.");
            return fallback;
        }

        try
        {
            var table = read(path);

            // Recorded only once it has read, so the list holds the files a run actually
            // used rather than every path that happened to resolve.
            context.SideFiles.Add(new ResolvedSideFile(what, stored.Trim(), Path.GetFullPath(path)));

            return table;
        }
        catch (Exception ex) when (ex is LegacyDataException or IOException)
        {
            context.Problems.Add($"Could not read the {what} file '{path}': {ex.Message}");
            return fallback;
        }
    }
}
