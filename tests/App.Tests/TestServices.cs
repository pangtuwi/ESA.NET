using App.Core;
using App.Persistence;
using App.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Resolves view models from the real composition root, so the tests exercise the same
/// service graph the application does rather than a hand-assembled stand-in.
/// </summary>
internal static class TestServices
{
    /// <summary>
    /// A data folder under the temp directory, one per test process.
    /// </summary>
    /// <remarks>
    /// Load-bearing. Several tests drive a real simulation through the view model, and
    /// every run now writes a folder of its own; without this they would fill the
    /// operator's Documents with run folders from the test suite.
    /// </remarks>
    public static string DataRoot { get; } = Path.Combine(
        Path.GetTempPath(), "esa-tests", Guid.NewGuid().ToString("N"));

    private static readonly IServiceProvider Provider =
        Configured(ServiceRegistration.CreateServices()).BuildServiceProvider();

    public static T Resolve<T>() where T : notnull => Provider.GetRequiredService<T>();

    /// <summary>
    /// Resolves from a fresh provider with <paramref name="configure"/> applied, for tests
    /// that need a stand-in for something that would open a window.
    /// </summary>
    public static T Resolve<T>(Action<IServiceCollection> configure)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(configure);

        var services = Configured(ServiceRegistration.CreateServices());

        configure(services);

        return services.BuildServiceProvider().GetRequiredService<T>();
    }

    /// <summary>A workspace rooted in its own temp folder, for a test that wants one.</summary>
    public static Workspace TemporaryWorkspace([System.Runtime.CompilerServices.CallerMemberName] string name = "") =>
        new(Path.Combine(DataRoot, RunFolderName.Sanitise(name), Guid.NewGuid().ToString("N")[..8]));

    private static IServiceCollection Configured(IServiceCollection services)
    {
        services.AddSingleton<IWorkspace>(new Workspace(Path.Combine(DataRoot, "shared")));

        return services;
    }
}
