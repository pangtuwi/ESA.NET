using App.Persistence;

namespace App.Tests;

/// <summary>
/// The shipped <c>.eng</c> files store paths written on Windows in 2001: bare names,
/// relative paths with backslashes, and absolute paths to drives that no longer exist.
/// </summary>
public sealed class LegacyPathResolverTests
{
    private static (DirectoryInfo Root, string EngineFile) CreateEngine()
    {
        var root = Directory.CreateTempSubdirectory("esa-paths");
        var engineFile = Path.Combine(root.FullName, "Engine.eng");
        File.WriteAllText(engineFile, "[Cylinders]\n");
        return (root, engineFile);
    }

    [Fact]
    public void FindsAFileBesideTheEngine()
    {
        var (root, engineFile) = CreateEngine();

        try
        {
            var target = Path.Combine(root.FullName, "Profile.cam");
            File.WriteAllText(target, "0 0\n");

            var resolved = new LegacyPathResolver(engineFile).Resolve("Profile.cam");

            Assert.NotNull(resolved);
            Assert.Equal(new FileInfo(target).FullName, new FileInfo(resolved).FullName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolvesARelativePathWrittenWithBackslashes()
    {
        var (root, engineFile) = CreateEngine();

        try
        {
            var nested = root.CreateSubdirectory("Variable_Inlet_Diameter");
            var target = Path.Combine(nested.FullName, "Inlet.maf");
            File.WriteAllText(target, "1,0,745\n");

            // Backslashes are not separators outside Windows, so this only resolves
            // because the resolver normalises them.
            var resolved = new LegacyPathResolver(engineFile).Resolve(@"Variable_Inlet_Diameter\Inlet.maf");

            Assert.NotNull(resolved);
            Assert.Equal(new FileInfo(target).FullName, new FileInfo(resolved).FullName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void FallsBackToTheFileNameFromADeadAbsolutePath()
    {
        var (root, engineFile) = CreateEngine();

        try
        {
            var target = Path.Combine(root.FullName, "490Inlet.maf");
            File.WriteAllText(target, "1,0,745\n");

            // Verbatim from Nissan5.eng. The drive has not existed for twenty years,
            // but the file has since been copied next to the engine.
            var resolved = new LegacyPathResolver(engineFile).Resolve(@"c:\CAEEng\NissanTesis\490Inlet.maf");

            Assert.NotNull(resolved);
            Assert.Equal(new FileInfo(target).FullName, new FileInfo(resolved).FullName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void SearchesBelowTheEngineDirectoryAsALastResort()
    {
        var (root, engineFile) = CreateEngine();

        try
        {
            var nested = root.CreateSubdirectory("Camshafts");
            var target = Path.Combine(nested.FullName, "Deep.cam");
            File.WriteAllText(target, "0 0\n");

            var resolved = new LegacyPathResolver(engineFile).Resolve("Deep.cam");

            Assert.NotNull(resolved);
            Assert.Equal(new FileInfo(target).FullName, new FileInfo(resolved).FullName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReturnsNullWhenNothingMatches()
    {
        var (root, engineFile) = CreateEngine();

        try
        {
            var resolver = new LegacyPathResolver(engineFile);

            Assert.Null(resolver.Resolve("Missing.cam"));
            Assert.Null(resolver.Resolve(string.Empty));
            Assert.Null(resolver.Resolve(null));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
