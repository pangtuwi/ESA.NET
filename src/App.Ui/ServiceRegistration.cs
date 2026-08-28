using App.Core;
using App.Core.Expressions;
using App.Persistence;
using App.Persistence.Tables;
using App.Core.Simulation;
using App.Ui.Charts;
using App.Ui.Dialogs;
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
        services.AddSingleton<ISimulationSettingsStore, SimulationSettingsStore>();

        services.AddSingleton<ICamProfileReader, CamProfileReader>();
        services.AddSingleton<ISpeedKeyedTableReader, SpeedKeyedTableReader>();
        services.AddSingleton<IWallTemperatureTableReader, WallTemperatureTableReader>();
        services.AddSingleton<IExhaustBackPressureTableReader, ExhaustBackPressureTableReader>();
        services.AddSingleton<IManifoldAreaTableStore, ManifoldAreaTableStore>();
        services.AddSingleton<IDischargeCoefficientTableStore, DischargeCoefficientTableStore>();

        services.AddSingleton<IEngineLoader, EngineLoader>();

        // Shared so that a parsed expression is reused across the whole session.
        services.AddSingleton<IExpressionEvaluator, CachingExpressionEvaluator>();
        services.AddSingleton<GridSizeCalculator>();
        services.AddSingleton<IChartWindowService, ChartWindowService>();
        services.AddSingleton<SimulationRunner>();
        services.AddSingleton<MultiRunner>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IEditEngineWindowService, EditEngineWindowService>();
        services.AddSingleton<IMultiRunWindowService, MultiRunWindowService>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<EditEngineViewModel>();
        services.AddTransient<MultiRunViewModel>();
        services.AddSingleton<Func<EditEngineViewModel>>(
            provider => provider.GetRequiredService<EditEngineViewModel>);
        services.AddSingleton<Func<MultiRunViewModel>>(
            provider => provider.GetRequiredService<MultiRunViewModel>);

        return services;
    }
}
