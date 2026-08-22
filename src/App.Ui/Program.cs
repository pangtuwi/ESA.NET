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

        return BuildAvaloniaApp(host.Services)
            .StartWithClassicDesktopLifetime(args);
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
