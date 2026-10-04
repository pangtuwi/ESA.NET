using App.Core;
using App.Core.Expressions;
using App.Core.Model;
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
    /// <summary><c>ESA.ini</c>, which sits beside the executable as it did beside ESA.EXE.</summary>
    public static string SettingsPath { get; } =
        Path.Combine(AppContext.BaseDirectory, SimulationSettingsStore.FileName);

    public static IServiceCollection CreateServices() => new ServiceCollection().AddEsa();

    public static IServiceCollection AddEsa(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEngineDefinitionStore, EngineDefinitionStore>();
        services.AddSingleton<ISimulationSettingsStore, SimulationSettingsStore>();

        // The application defaults, read from ESA.ini beside the executable at startup as
        // Delphi's FormCreate does with LoadIniValues (Main.pas:772). Transient because the
        // main window changes its copy through the Simulate dialog; a test registers its
        // own over the top.
        services.AddTransient(provider =>
            provider.GetRequiredService<ISimulationSettingsStore>().Read(SettingsPath));

        // The data folder, resolved once from the same file. A test registers its own over
        // the top, which is what keeps a test run out of the operator's Documents.
        services.AddSingleton<IWorkspace>(provider => Workspace.From(
            provider.GetRequiredService<ISimulationSettingsStore>().Read(SettingsPath)));

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
        services.AddSingleton<ISimulateOptionsWindowService, SimulateOptionsWindowService>();
        services.AddSingleton<IRunTimeGraphOptionsWindowService, RunTimeGraphOptionsWindowService>();
        services.AddSingleton<IAboutWindowService, AboutWindowService>();
        services.AddSingleton<ISweepPlotWindowService, SweepPlotWindowService>();
        services.AddSingleton<IApplicationShell, ApplicationShell>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<EditEngineViewModel>();
        services.AddTransient<MultiRunViewModel>();
        services.AddTransient<SimulateOptionsViewModel>();
        services.AddTransient<RunTimeGraphOptionsViewModel>();
        services.AddTransient<SweepPlotViewModel>();
        services.AddSingleton<Func<SweepPlotViewModel>>(
            provider => provider.GetRequiredService<SweepPlotViewModel>);
        services.AddSingleton<Func<EditEngineViewModel>>(
            provider => provider.GetRequiredService<EditEngineViewModel>);
        services.AddSingleton<Func<MultiRunViewModel>>(
            provider => provider.GetRequiredService<MultiRunViewModel>);

        services.AddSingleton<Func<SimulateOptionsViewModel>>(
            provider => provider.GetRequiredService<SimulateOptionsViewModel>);
        services.AddSingleton<Func<RunTimeGraphOptionsViewModel>>(
            provider => provider.GetRequiredService<RunTimeGraphOptionsViewModel>);

        return services;
    }
}
