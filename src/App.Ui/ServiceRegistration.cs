using App.Core;
using App.Persistence;
using App.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.Ui;

/// <summary>
/// The composition root. Kept separate from <see cref="Program"/> so that tests and
/// the XAML previewer can build the same service graph.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection CreateServices() => new ServiceCollection().AddEsa();

    public static IServiceCollection AddEsa(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEngineDefinitionStore, EngineDefinitionStore>();
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
