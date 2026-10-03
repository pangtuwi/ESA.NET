using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// Commands that were reported doing nothing at all: File, Exit (GitHub issue 144), Help,
/// About (145), Help, User Manual (146) and the editor's Cancel (147).
/// </summary>
public sealed class DeadButtonTests
{
    private sealed class StubShell : IApplicationShell
    {
        public int Exits { get; private set; }

        public string? Opened { get; private set; }

        public bool OpenSucceeds { get; set; } = true;

        public void Exit() => Exits++;

        public Task<bool> OpenFileAsync(string path)
        {
            Opened = path;
            return Task.FromResult(OpenSucceeds);
        }
    }

    private sealed class StubAbout : IAboutWindowService
    {
        public int Shown { get; private set; }

        public Task ShowAsync()
        {
            Shown++;
            return Task.CompletedTask;
        }
    }

    private static (MainWindowViewModel ViewModel, StubShell Shell, StubAbout About) Build()
    {
        var shell = new StubShell();
        var about = new StubAbout();

        var viewModel = TestServices.Resolve<MainWindowViewModel>(services =>
        {
            services.AddSingleton<IApplicationShell>(shell);
            services.AddSingleton<IAboutWindowService>(about);
        });

        return (viewModel, shell, about);
    }

    [Fact]
    public void ExitEndsTheApplication()
    {
        var (viewModel, shell, _) = Build();

        viewModel.ExitCommand.Execute(null);

        Assert.Equal(1, shell.Exits);
    }

    [Fact]
    public async Task UserManualOpensTheShippedPdf()
    {
        var (viewModel, shell, _) = Build();

        await viewModel.UserManualCommand.ExecuteAsync(null);

        Assert.Equal(MainWindowViewModel.UserManualPath, shell.Opened);
        Assert.Equal(string.Empty, viewModel.RunStatus);
    }

    /// <summary>The build copies the manual beside the executable, where the command looks.</summary>
    [Fact]
    public void TheUserManualIsInstalledBesideTheExecutable() =>
        Assert.True(File.Exists(MainWindowViewModel.UserManualPath), MainWindowViewModel.UserManualPath);

    /// <summary>
    /// The legacy PDF was made by running <c>txt2pdf</c> over the binary Word file, so it
    /// opens as pages of noise. What ships must be a real conversion of the manual.
    /// </summary>
    [Fact]
    public void TheShippedManualIsNotTheGarbledLegacyPdf()
    {
        var bytes = File.ReadAllBytes(MainWindowViewModel.UserManualPath);
        var text = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
        Assert.DoesNotContain("txt2pdf", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AManualThatWillNotOpenIsReportedWithItsPath()
    {
        var (viewModel, shell, _) = Build();
        shell.OpenSucceeds = false;

        await viewModel.UserManualCommand.ExecuteAsync(null);

        Assert.Contains(MainWindowViewModel.UserManualPath, viewModel.RunStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AboutShowsTheAboutBox()
    {
        var (viewModel, _, about) = Build();

        await viewModel.AboutCommand.ExecuteAsync(null);

        Assert.Equal(1, about.Shown);
    }

    [AvaloniaFact]
    public void TheAboutBoxCarriesTheOriginalsCreditsAndCloses()
    {
        var viewModel = new AboutViewModel();
        var window = new AboutWindow { DataContext = viewModel };
        var closes = 0;

        viewModel.CloseRequested += (_, _) => closes++;
        window.Show();

        Assert.Contains(AboutViewModel.Credits, line => line.Contains("van Vuuren", StringComparison.Ordinal));
        Assert.Contains(AboutViewModel.Credits, line => line.Contains("Williams", StringComparison.Ordinal));
        Assert.StartsWith(".NET port, version ", AboutViewModel.PortVersion, StringComparison.Ordinal);

        var ok = window.FindControl<Button>("OkButton");

        Assert.NotNull(ok?.Command);
        ok.Command.Execute(null);

        Assert.Equal(1, closes);

        window.Close();
    }

    [Fact]
    public void CancelClosesTheEditorWithoutTouchingTheDefinition()
    {
        var path = Path.Combine(TestPaths.Samples, "Default.eng");
        var definition = new EngineDefinitionStore().Read(path);
        var viewModel = new EditEngineViewModel();
        var closes = 0;
        var applied = 0;

        viewModel.Load(definition);
        viewModel.CloseRequested += (_, _) => closes++;
        viewModel.Applied += (_, _) => applied++;

        viewModel.Bore = 99;
        viewModel.CancelCommand.Execute(null);

        Assert.Equal(1, closes);
        Assert.Equal(0, applied);
        Assert.Equal(81.0, definition.Bore);
    }

    /// <summary>
    /// Every button on the editor's bottom row either does something or is visibly
    /// unavailable; none may sit there enabled and inert, as Cancel, Load and Save did.
    /// </summary>
    [AvaloniaFact]
    public void NoEditorButtonIsEnabledWithNothingBehindIt()
    {
        var viewModel = new EditEngineViewModel();
        viewModel.Load(new EngineDefinitionStore().Read(Path.Combine(TestPaths.Samples, "Default.eng")));

        var window = new EditEngineWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            foreach (var name in new[] { "LoadButton", "SaveButton", "OkButton", "CancelButton" })
            {
                var button = window.FindControl<Button>(name);

                Assert.NotNull(button);
                Assert.True(button.Command is not null || !button.IsEnabled, $"{name} is enabled with no command");
            }

            Assert.NotNull(window.FindControl<Button>("CancelButton")!.Command);

            // ISSUES.md A5: No Cylinders stays editable, deliberately unlike the original.
            var cylinders = window.FindControl<TextBox>("CylinderCountBox");

            Assert.NotNull(cylinders);
            Assert.True(cylinders.IsEnabled);
            Assert.False(cylinders.IsReadOnly);
        }
        finally
        {
            window.Close();
        }
    }
}
