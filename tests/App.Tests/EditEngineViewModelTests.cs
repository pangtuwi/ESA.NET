using App.Core;
using App.Persistence;
using App.Ui.ViewModels;

namespace App.Tests;

/// <summary>
/// The editor's contract with user data: it must not rewrite what the user did not
/// touch, and it must not silently swallow an invalid entry the way the original did.
/// </summary>
public sealed class EditEngineViewModelTests
{
    private static readonly EngineDefinitionStore Store = new();

    private static string SamplePath(string fileName) => Path.Combine(TestPaths.Samples, fileName);

    private static EditEngineViewModel LoadedFrom(EngineDefinition definition)
    {
        var viewModel = new EditEngineViewModel();
        viewModel.Load(definition);
        return viewModel;
    }

    [Fact]
    public void PopulatesEveryTabFromARealEngine()
    {
        var viewModel = LoadedFrom(Store.Read(SamplePath("Default.eng")));

        Assert.Equal("China Bora 1.6L 5V # 9.72:1", viewModel.EngineName);
        Assert.Equal(4, viewModel.CylinderCount);
        Assert.Equal(81.0, viewModel.Bore);
        Assert.Equal(9.72, viewModel.CompressionRatio);
        Assert.Equal(150, viewModel.WoshiniCoefficient);
        Assert.Equal(55.0, viewModel.BurnAngle);
        Assert.Equal(3, viewModel.InletValveCount);
        Assert.Equal(14.5, viewModel.AirFuelRatio);
        Assert.True(viewModel.VariableGamma);
        Assert.True(viewModel.IsRkf5);
        Assert.False(viewModel.IsEuler);
        Assert.Equal("DefaultDat.txt", viewModel.PerformanceDataFile);
    }

    [Fact]
    public void CapacityFollowsTheDelphiFormula()
    {
        var viewModel = LoadedFrom(Store.Read(SamplePath("Default.eng")));

        // Cyl * Pi/4 * Bore^2 * Stroke / 1000, as ECCChanged computes it.
        var expected = 4 * Math.PI / 4.0 * 81.0 * 81.0 * 77.4 / 1000.0;

        Assert.Equal(expected, viewModel.Capacity, 6);
        Assert.Equal(1595.0, viewModel.Capacity, 0);
    }

    [Fact]
    public void CapacityRecalculatesAsFieldsChange()
    {
        var viewModel = LoadedFrom(Store.Read(SamplePath("Default.eng")));
        var before = viewModel.Capacity;

        viewModel.Bore = 86.0;

        Assert.True(viewModel.Capacity > before);
    }

    [Fact]
    public void LoadingAndSavingWithNoEditsLeavesTheFileByteIdentical()
    {
        // The whole point of the changed-check in Apply: opening an engine and pressing
        // OK must not restyle numbers, so Bore=81.0 does not become Bore=81.
        foreach (var name in (string[])["Default.eng", "ChinaBora98.eng", "Nissan5.eng"])
        {
            var source = SamplePath(name);
            var original = File.ReadAllBytes(source);

            var definition = Store.Read(source);
            LoadedFrom(definition).Apply();

            var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".eng");
            try
            {
                Store.Write(target, definition);
                Assert.Equal(original, File.ReadAllBytes(target));
            }
            finally
            {
                File.Delete(target);
            }
        }
    }

    [Fact]
    public void EditingOneFieldRewritesOnlyThatLine()
    {
        var source = SamplePath("Default.eng");
        var originalLines = File.ReadAllLines(source);

        var definition = Store.Read(source);
        var viewModel = LoadedFrom(definition);
        viewModel.Bore = 82.5;
        viewModel.Apply();

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".eng");
        try
        {
            Store.Write(target, definition);
            var written = File.ReadAllLines(target);

            var differences = originalLines
                .Zip(written, (before, after) => (before, after))
                .Where(pair => pair.before != pair.after)
                .ToList();

            Assert.Single(differences);
            Assert.Equal("Bore=82.5", differences[0].after);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void FuelCompositionIsWrittenOnlyWhenItChanges()
    {
        var source = SamplePath("Default.eng");
        var definition = Store.Read(source);
        var viewModel = LoadedFrom(definition);

        // No .eng file carries composition, so the form shows the Delphi defaults.
        Assert.Equal(EngineDefinition.DefaultFuelCarbon, viewModel.FuelCarbon);
        Assert.Equal(EngineDefinition.DefaultFuelHydrogen, viewModel.FuelHydrogen);
        Assert.False(definition.HasFuelComposition);

        // Applying unchanged defaults must not add keys.
        viewModel.Apply();
        Assert.False(definition.HasFuelComposition);
        Assert.Equal(File.ReadAllBytes(source), ((IniEngineDefinition)definition).Document.ToBytes());

        // Changing one does add it, fixing the original's silent data loss.
        viewModel.FuelCarbon = 8;
        viewModel.Apply();

        Assert.True(definition.HasFuelComposition);
        Assert.Equal("8", definition.GetValue("Fuel", "C"));
        Assert.Equal(8, Store.Read(WriteToTemp(definition)).FuelCarbon);
    }

    [Fact]
    public void SaveIsBlockedWhileAFieldIsInvalid()
    {
        var viewModel = LoadedFrom(Store.Read(SamplePath("Default.eng")));

        Assert.True(viewModel.CanSave);

        // Edit.pas caught EConvertError and showed nothing, discarding the whole edit.
        viewModel.CylinderCount = 0;

        Assert.True(viewModel.HasErrors);
        Assert.False(viewModel.CanSave);

        viewModel.CylinderCount = 4;

        Assert.False(viewModel.HasErrors);
        Assert.True(viewModel.CanSave);
    }

    [Fact]
    public void OlderSchemaEnginesAreDescribedRatherThanHidden()
    {
        var viewModel = LoadedFrom(Store.Read(SamplePath("Nissan5.eng")));

        Assert.True(viewModel.UsesOlderSchema);
        Assert.Contains("InManifold", viewModel.OlderSchemaNotes, StringComparison.Ordinal);
        Assert.Contains("wall temperatures", viewModel.OlderSchemaNotes, StringComparison.Ordinal);

        // The area file is found under the older section rather than [Inlet].
        Assert.Contains("490Inlet.maf", viewModel.InletAreaFile, StringComparison.Ordinal);
    }

    private static string WriteToTemp(EngineDefinition definition)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".eng");
        Store.Write(path, definition);
        return path;
    }
}
