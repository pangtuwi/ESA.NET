using Avalonia;
using Avalonia.Headless;
using EsaApp = App.Ui.App;

[assembly: AvaloniaTestApplication(typeof(App.Tests.AvaloniaTestApp))]

namespace App.Tests;

/// <summary>Builds the real application in headless mode for the UI tests.</summary>
internal static class AvaloniaTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EsaApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
