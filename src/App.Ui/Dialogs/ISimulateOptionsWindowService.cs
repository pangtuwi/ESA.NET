using App.Core.Model;
using App.Ui.ViewModels;

namespace App.Ui.Dialogs;

/// <summary>What the operator chose in the Single Speed Simulation dialog.</summary>
/// <param name="Accepted">Delphi <c>mrOK</c>: true when Run was pressed.</param>
/// <param name="EngineSpeed">Delphi <c>Engine2z.Nrpm</c>, in rev/min and already clamped.</param>
/// <param name="TotalCycles">Delphi <c>NoCycles</c>.</param>
/// <param name="MassBalance">Delphi <c>MassBalance</c>, in milligrams.</param>
/// <param name="Graphs">Which run-time charts to draw.</param>
public sealed record SimulateOptionsResult(
    bool Accepted,
    double EngineSpeed,
    int TotalCycles,
    double MassBalance,
    GraphSelection Graphs);

/// <summary>
/// Opens the Single Speed Simulation dialog. Injected for the same reason the other
/// windows are: the view models stay testable without a display.
/// </summary>
public interface ISimulateOptionsWindowService
{
    /// <summary>
    /// Shows the dialog and waits for it, as <c>FSimulateOptions.ShowModal</c> does.
    /// </summary>
    /// <param name="settings">The values from <c>ESA.ini</c> the form opens on.</param>
    /// <param name="engineSpeed">The speed to open on, in rev/min.</param>
    Task<SimulateOptionsResult> ShowAsync(SimulationSettings settings, double engineSpeed);
}
