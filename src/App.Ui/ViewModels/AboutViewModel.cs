using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// The About box. Port of <c>TAboutBox</c> (AboutBoxUnit.pas / .dfm), whose captions are
/// carried over as they were, less its "Version 2.5" label, which had not kept up with
/// the 3.0 release.
/// </summary>
public sealed partial class AboutViewModel
{
    /// <summary>Delphi <c>AboutBox.Caption</c>.</summary>
    public static string Title => "About \"Engine Simulation and Analysis\"";

    /// <summary>Delphi <c>ProductName</c>.</summary>
    public static string ProductName => "Engine Simulation and Analysis (ESA)";

    /// <summary>Delphi <c>Label1</c>.</summary>
    public static string Institution => "University of Stellenbosch";

    /// <summary>Delphi <c>Copyright</c>.</summary>
    public static string Copyright => "Copyright : Centre for Automotive Engineering";

    /// <summary>Delphi <c>Comments</c> and <c>Label2</c> to <c>Label4</c>: who wrote what.</summary>
    public static IReadOnlyList<string> Credits { get; } =
    [
        "C. M. van Vuuren : CFD",
        "P. N. T. Williams : Engine Model, Windows Interface",
    ];

    /// <summary>Which build of the port this is; the original showed its own version here.</summary>
    public static string PortVersion
    {
        get
        {
            var assembly = typeof(AboutViewModel).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? assembly.GetName().Version?.ToString()
                          ?? "unknown";

            // The SDK appends "+<commit>"; the version is the part worth reading.
            var plus = version.IndexOf('+', StringComparison.Ordinal);

            return $".NET port, version {(plus < 0 ? version : version[..plus])}";
        }
    }

    /// <summary>Raised when OK is pressed.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Delphi <c>OKButtonClick</c>.</summary>
    [RelayCommand]
    private void Ok() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
