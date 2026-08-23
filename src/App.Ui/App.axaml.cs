using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace App.Ui;

/// <summary>
/// The Avalonia application. It is handed the service provider built by
/// <see cref="Program"/> rather than reaching for a static one, because the port
/// forbids static mutable state.
/// </summary>
public sealed partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services) => _services = services;

    /// <summary>
    /// Used only by the XAML previewer, which needs a parameterless constructor. It
    /// builds a throwaway provider; the running application always uses the one from
    /// <see cref="Program"/>.
    /// </summary>
    public App() => _services = ServiceRegistration.CreateServices().BuildServiceProvider();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
