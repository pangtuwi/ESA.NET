using System.Reflection;
using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// Enforces the layering rule from CLAUDE.md: App.Core must never depend on a UI
/// framework, and App.Persistence must not either. A project reference added by
/// accident fails here rather than being noticed months later.
/// </summary>
public sealed class LayeringTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Avalonia",
        "ScottPlot",
        "System.Windows",
        "PresentationFramework",
        "WindowsBase",
        "App.Ui",
    ];

    [Fact]
    public void CoreReferencesNoUiAssembly() => AssertNoUiReferences(typeof(Engine).Assembly);

    [Fact]
    public void PersistenceReferencesNoUiAssembly() => AssertNoUiReferences(typeof(IniDocument).Assembly);

    private static void AssertNoUiReferences(Assembly assembly)
    {
        var offenders = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offenders);
    }
}
