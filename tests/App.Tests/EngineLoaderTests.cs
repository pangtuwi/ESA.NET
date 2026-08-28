using App.Core;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

public sealed class EngineLoaderTests
{
    private static EngineLoader CreateLoader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static string Data(params string[] parts) =>
        Path.Combine([TestPaths.Legacy!, "ESA", "Data", .. parts]);

    private static void RequireLegacy() =>
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

    [Fact]
    public void LoadsAnEngineAndItsSideFilesEndToEnd()
    {
        RequireLegacy();

        var result = CreateLoader().Load(Data("Example2", "ChinaBora98.eng"));
        var engine = result.Engine;

        Assert.Equal(4, engine.CylinderCount);
        Assert.Equal(81.0, engine.Bore);

        // Every side file this engine names sits beside it or below it.
        Assert.True(result.IsComplete, string.Join("; ", result.Problems));

        Assert.NotEmpty(engine.Manifold.InletValve.Profile.Points);
        Assert.NotEmpty(engine.Manifold.ExhaustValve.Profile.Points);
        Assert.True(engine.Manifold.InletPipe.AreaVersusLength.Count > 0);
        Assert.True(engine.Manifold.ExhaustPipe.AreaVersusLength.Count > 0);
        Assert.True(engine.Manifold.InletValve.CdForward.XCount > 0);
        Assert.NotEmpty(engine.WallTemperature.Rpm);
        Assert.NotEmpty(engine.Manifold.ExhaustBack.Rpm);
        Assert.NotEmpty(engine.SparkAngle.Rpm);
    }

    [Fact]
    public void EverySideFileItReadIsReportedWithWhereItWasFound()
    {
        RequireLegacy();

        // What a run folder copies into its inputs: without this list there is no way to
        // say afterwards which files a result was computed from, since the .eng entries
        // are bare names, backslash-relative paths and absolute paths to dead drives.
        var result = CreateLoader().Load(Data("Example2", "ChinaBora98.eng"));

        Assert.True(result.IsComplete, string.Join("; ", result.Problems));
        Assert.NotEmpty(result.SideFiles);
        Assert.All(result.SideFiles, side => Assert.True(File.Exists(side.Path), side.Path));
        Assert.All(result.SideFiles, side => Assert.True(Path.IsPathRooted(side.Path), side.Path));

        // Named the way the load problems name them, so a manifest reads the same either
        // way.
        Assert.Contains(result.SideFiles, side => side.Kind == "inlet cam profile");
        Assert.Contains(result.SideFiles, side => side.Kind == "exhaust manifold area");
    }

    [Fact]
    public void AMissingSideFileIsAProblemRatherThanAnEntry()
    {
        RequireLegacy();

        var loader = CreateLoader();
        var definition = new EngineDefinitionStore().Read(Data("Example2", "ChinaBora98.eng"));

        definition.InletValveProfileFile = "nowhere-at-all.cam";

        var result = loader.Rebuild(definition, Data("Example2", "ChinaBora98.eng"));

        Assert.Contains(result.Problems, p => p.Contains("nowhere-at-all.cam", StringComparison.Ordinal));
        Assert.DoesNotContain(result.SideFiles, side => side.Kind == "inlet cam profile");
    }

    [Fact]
    public void ExhaustDischargeTablesAreWiredCrossed()
    {
        RequireLegacy();

        // ICEngine2Z.pas lines 998-1005: EV.CdForward comes from CdEvOut and
        // EV.CdReverse from CdEvIn, because forward flow through an exhaust valve is
        // outward. Getting this backwards would change the physics silently.
        var path = Data("Example1", "Nissan", "Nissan7.eng");
        var result = CreateLoader().Load(path);
        var definition = result.Definition;

        Assert.NotEqual(definition.ExhaustValveCdInwardFile, definition.ExhaustValveCdOutwardFile);

        var exhaust = result.Engine.Manifold.ExhaustValve;
        var store = new DischargeCoefficientTableStore();
        var resolver = new LegacyPathResolver(path);

        var outward = store.Read(resolver.Resolve(definition.ExhaustValveCdOutwardFile)!).Table;
        var inward = store.Read(resolver.Resolve(definition.ExhaustValveCdInwardFile)!).Table;

        Assert.Equal(outward.Cell[0, 0], exhaust.CdForward.Cell[0, 0]);
        Assert.Equal(inward.Cell[0, 0], exhaust.CdReverse.Cell[0, 0]);

        // The inlet valve is wired the straightforward way round.
        var intake = result.Engine.Manifold.InletValve;
        var inletIn = store.Read(resolver.Resolve(definition.InletValveCdInwardFile)!).Table;
        Assert.Equal(inletIn.Cell[0, 0], intake.CdForward.Cell[0, 0]);
    }

    [Fact]
    public void ReadsTheOlderSchemaEnginesIncludingInlineValues()
    {
        RequireLegacy();

        var result = CreateLoader().Load(Data("Example1", "Nissan5.eng"));
        var engine = result.Engine;

        Assert.True(result.Definition.UsesOlderManifoldSchema);
        Assert.Equal("Nissan2.4L Engine # Z24 Block,NA20 Head # Custom Inlet # NA20 Exh.", engine.Name);
        Assert.Equal(4, engine.CylinderCount);

        // Wall temperatures are inline rather than in a .cwt file.
        Assert.True(result.Definition.HasInlineWallTemperatures);
        Assert.Equal(180, engine.WallTemperature.HeadTemperature[0]);
        Assert.Equal(260, engine.WallTemperature.PistonTemperature[0]);

        // As is the exhaust back pressure.
        Assert.True(result.Definition.HasInlineExhaustBackPressure);
        Assert.Equal(35.0, engine.Manifold.ExhaustBack.Pressure[0]);
        Assert.Equal(400, engine.Manifold.ExhaustBack.Temperature[0]);
    }

    [Fact]
    public void AFixedSparkAngleIsAcceptedInPlaceOfASparkMap()
    {
        RequireLegacy();

        // Nissan5.eng writes SparkAngle=10.0 rather than naming a .spk file.
        var engine = CreateLoader().Load(Data("Example1", "Nissan5.eng")).Engine;

        Assert.Single(engine.SparkAngle.Rpm);
        Assert.Equal(10.0, engine.SparkAngle.Values[0]);
    }

    [Fact]
    public void FuelCompositionDefaultsToTheDelphiFormValues()
    {
        RequireLegacy();

        // No .eng file carries C/H/O/N, so every engine gets the C7H17 petrol surrogate
        // the Delphi form started with.
        var engine = CreateLoader().Load(Data("Example2", "ChinaBora98.eng")).Engine;

        Assert.Equal(7, engine.Cylinder.Fuel.C);
        Assert.Equal(17, engine.Cylinder.Fuel.H);
        Assert.Equal(0, engine.Cylinder.Fuel.O);
        Assert.Equal(0, engine.Cylinder.Fuel.N);

        // The composition is shared by every gas, as Edit.pas copies it.
        Assert.Equal(engine.Cylinder.Fuel.C, engine.Plenum.Fuel.C);
        Assert.Equal(engine.Cylinder.Fuel.H, engine.Exhaust.Fuel.H);
    }

    [Fact]
    public void MissingSideFilesAreCollectedRatherThanThrown()
    {
        var directory = Directory.CreateTempSubdirectory("esa-loader");

        try
        {
            var path = Path.Combine(directory.FullName, "Broken.eng");
            File.WriteAllText(
                path,
                """
                [Cylinders]
                Name=Broken
                NoCyls=4
                [Cams]
                IVProfile=NoSuchProfile.cam
                [Valves]
                CdIVIn=NoSuchTable.vcd

                """);

            var result = CreateLoader().Load(path);

            Assert.False(result.IsComplete);
            Assert.Contains(result.Problems, p => p.Contains("NoSuchProfile.cam", StringComparison.Ordinal));

            // The engine still comes back, carrying whatever did load.
            Assert.Equal("Broken", result.Engine.Name);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void EveryShippedEngineLoadsWithoutThrowing()
    {
        RequireLegacy();

        var loader = CreateLoader();
        var files = TestPaths.AllLegacyEngineFiles().ToList();
        var failures = new List<string>();
        var loaded = 0;

        foreach (var path in files)
        {
            try
            {
                loader.Load(path);
                loaded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetRelativePath(TestPaths.Legacy!, path)}: {ex.Message}");
            }
        }

        // 65 original engines under legacy/ESA plus the fixture copies in
        // legacy/samples. Counted rather than hard-coded so adding a fixture does not
        // fail the sweep.
        Assert.True(files.Count >= 65, $"Expected at least the 65 shipped engines, found {files.Count}.");
        Assert.Equal(files.Count, loaded);
        Assert.Empty(failures);
    }
}
