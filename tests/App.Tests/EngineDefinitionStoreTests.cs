using App.Core;
using App.Persistence;

namespace App.Tests;

public sealed class EngineDefinitionStoreTests
{
    private static readonly EngineDefinitionStore Store = new();

    private static string SamplePath(string fileName) => Path.Combine(TestPaths.Samples, fileName);

    [Fact]
    public void ReadsTypedValuesFromDefaultEng()
    {
        var definition = Store.Read(SamplePath("Default.eng"));

        Assert.Equal("China Bora 1.6L 5V # 9.72:1", definition.Name);
        Assert.Equal(4, definition.CylinderCount);
        Assert.Equal(81.0, definition.Bore);
        Assert.Equal(77.4, definition.Stroke);
        Assert.Equal(9.72, definition.CompressionRatio);
        Assert.Equal(149, definition.ConrodLength);
        Assert.Equal(3, definition.InletValveCount);
        Assert.Equal(2, definition.ExhaustValveCount);
        Assert.Equal(14.5, definition.AirFuelRatio);
        Assert.True(definition.VariableGamma);
        Assert.False(definition.SaveManifoldData);
        Assert.Equal(Integrator.Rkf5, definition.Integrator);
        Assert.Equal("DefaultDat.txt", definition.PerformanceDataFile);
    }

    [Fact]
    public void ValueLookupIgnoresKeyCase()
    {
        var definition = Store.Read(SamplePath("Default.eng"));

        // Edit.pas asks for "CdIvIn"; the file writes "CdIVIn".
        Assert.Equal("A2China IVIn.vcd", definition.GetValue("Valves", "CdIvIn"));
        Assert.Equal("A2China IVIn.vcd", definition.GetValue("valves", "cdivin"));
        Assert.Equal("A2China IVIn.vcd", definition.InletValveCdInwardFile);
    }

    [Fact]
    public void MissingKeyFallsBackToDelphiDefault()
    {
        // Nissan5.eng predates the [Calculation] section entirely.
        var definition = Store.Read(SamplePath("Nissan5.eng"));

        Assert.Null(definition.GetValue("Calculation", "PerfDataSave"));
        Assert.Equal("SimulDat.txt", definition.PerformanceDataFile);
        Assert.Equal(Integrator.Rkf5, definition.Integrator);
    }

    [Fact]
    public void UndocumentedOlderSectionsSurviveReading()
    {
        var definition = Store.Read(SamplePath("Nissan5.eng"));

        Assert.Contains("InManifold", definition.Sections);
        Assert.Contains("ExManifold", definition.Sections);
        Assert.Equal("0", definition.GetValue("InManifold", "InsertL"));
    }

    [Fact]
    public void ExpressionValuesAreReadVerbatim()
    {
        var definition = Store.Read(SamplePath("Default.eng"));

        Assert.Equal("(99000)", definition.PlenumPressureFunction);
        Assert.StartsWith("((1.0293E-19*N^6", definition.InletGridFunction, StringComparison.Ordinal);
        Assert.EndsWith("*L/0.758)", definition.InletGridFunction, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadThenWriteReproducesTheFileExactly()
    {
        var source = SamplePath("ChinaBora98.eng");
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".eng");

        try
        {
            Store.Write(target, Store.Read(source));
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void WriteRejectsForeignDefinitions()
    {
        Assert.Throws<ArgumentException>(() => Store.Write("ignored.eng", new StubDefinition()));
    }

    private sealed class StubDefinition : EngineDefinition
    {
        public override IReadOnlyList<string> Sections => [];

        public override IReadOnlyList<string> KeysIn(string section) => [];

        public override string? GetValue(string section, string key) => null;

        public override void SetValue(string section, string key, string value)
        {
        }
    }
}
