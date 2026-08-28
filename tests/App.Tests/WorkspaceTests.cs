using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// The data folder: where it is, and that a run gets a folder of its own inside it.
/// </summary>
public sealed class WorkspaceTests
{
    private static readonly DateTimeOffset When =
        new(2026, 8, 28, 14, 15, 30, TimeSpan.Zero);

    /// <summary>
    /// Runs <paramref name="check"/> with <c>ESA_DATA_ROOT</c> set to
    /// <paramref name="value"/>, restoring whatever it held. The tests that turn on the
    /// resolution order have to control it rather than inherit whatever the machine
    /// running them has set.
    /// </summary>
    private static void WithRootVariable(string? value, Action check)
    {
        var previous = Environment.GetEnvironmentVariable(Workspace.RootVariable);

        try
        {
            Environment.SetEnvironmentVariable(Workspace.RootVariable, value);
            check();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Workspace.RootVariable, previous);
        }
    }

    [Fact]
    public void TheDefaultIsEsaUnderDocuments()
    {
        // Nothing configured and nothing in the environment: Documents, which is where an
        // engineer will look for it and what a backup will pick up. A headless Linux box
        // with no XDG configuration reports no Documents folder, and the home directory is
        // the next best thing.
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (documents.Length == 0)
        {
            documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        WithRootVariable(null, () =>
            Assert.Equal(Path.Combine(documents, "ESA"), Workspace.ResolveRoot(null)));
    }

    [Fact]
    public void EsaIniOverridesTheDefault()
    {
        WithRootVariable(null, () => Assert.Equal("/data/esa", Workspace.ResolveRoot("/data/esa")));
    }

    [Fact]
    public void TheEnvironmentVariableOverridesEsaIni()
    {
        WithRootVariable(
            "/tmp/from-environment",
            () => Assert.Equal("/tmp/from-environment", Workspace.ResolveRoot("/data/esa")));
    }

    [Fact]
    public void ASettingsFileWithNoFoldersSectionLeavesTheDefaultAlone()
    {
        // Every ESA.ini that predates the data folder - which is all of them - has no
        // [Folders] section at all.
        Assert.Equal(string.Empty, new SimulationSettings().DataFolder);
    }

    [Fact]
    public void AnImpossibleConfiguredFolderFallsBackRatherThanRefusingToStart()
    {
        // ESA.ini is hand-edited, and the workspace is built during startup: a typo there
        // must not stop the application opening at all.
        var settings = new SimulationSettings { DataFolder = "\0not-a-path" };

        WithRootVariable(
            null, () => Assert.Equal(Workspace.DefaultRoot(), Workspace.From(settings).RootDirectory));
    }

    [Fact]
    public void TheFirstRunCreatesTheFolderStructureAndTheNoteExplainingIt()
    {
        var workspace = TestServices.TemporaryWorkspace();

        // Nothing yet: merely constructing a workspace must not litter the disk.
        Assert.False(Directory.Exists(workspace.RootDirectory));

        var run = workspace.CreateRunDirectory("/engines/A2China.eng", When);

        Assert.True(Directory.Exists(workspace.EnginesDirectory));
        Assert.True(Directory.Exists(workspace.RunsDirectory));
        Assert.True(File.Exists(Path.Combine(workspace.RootDirectory, "README.txt")));

        Assert.Equal(
            Path.Combine(workspace.RunsDirectory, "2026-08-28_141530_A2China"), run);

        Assert.True(Directory.Exists(run));
    }

    [Fact]
    public void TwoRunsInTheSameSecondDoNotShareAFolder()
    {
        // The whole point of the folder is that a run's output survives the next run.
        var workspace = TestServices.TemporaryWorkspace();

        var first = workspace.CreateRunDirectory("A2China.eng", When);
        var second = workspace.CreateRunDirectory("A2China.eng", When);

        Assert.NotEqual(first, second);
        Assert.EndsWith("_2", second, StringComparison.Ordinal);
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }
}
