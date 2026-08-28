using App.Core;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Ui;

internal static class Program
{
    // Avalonia configuration; must not touch any Avalonia type before AppMain is called.
    [STAThread]
    public static int Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddEsa();

        using var host = builder.Build();

        PrepareWorkspace(host.Services);

        return BuildAvaloniaApp(host.Services)
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Creates the data folder, so Engines is there to be filled before the first run.
    /// A folder that cannot be created is not a reason to refuse to start: the run that
    /// needs it will say so.
    /// </summary>
    private static void PrepareWorkspace(IServiceProvider services)
    {
        try
        {
            services.GetRequiredService<IWorkspace>().Prepare();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Nothing to show it on yet - the window is not up.
        }
    }

    /// <summary>Used by the XAML previewer, which requires this exact signature.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
