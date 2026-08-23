using App.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Resolves view models from the real composition root, so the tests exercise the same
/// service graph the application does rather than a hand-assembled stand-in.
/// </summary>
internal static class TestServices
{
    private static readonly IServiceProvider Provider =
        ServiceRegistration.CreateServices().BuildServiceProvider();

    public static T Resolve<T>() where T : notnull => Provider.GetRequiredService<T>();
}
