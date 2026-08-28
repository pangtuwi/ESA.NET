using App.Core.Model;
using App.Ui.Dialogs;
using App.Ui.ViewModels;

namespace App.Tests;

/// <summary>
/// Stands in for the Single Speed Simulation dialog, which the Single Point Simulation
/// command opens before every run. Tests set what the operator would have typed.
/// </summary>
internal sealed class StubSimulateOptions : ISimulateOptionsWindowService
{
    /// <summary>Whether Run was pressed rather than Cancel.</summary>
    public bool Accept { get; set; } = true;

    /// <summary>The speed to answer with. Defaults to whatever it was shown.</summary>
    public double? EngineSpeed { get; set; }

    /// <summary>The cycle count to answer with. Defaults to the settings it was shown.</summary>
    public int? TotalCycles { get; set; }

    /// <summary>The mass balance to answer with. Defaults to the settings it was shown.</summary>
    public double? MassBalance { get; set; }

    /// <summary>Which charts to ask for. Defaults to all three, as Graphs On does.</summary>
    public GraphSelection Graphs { get; set; } = new(true, true, true);

    /// <summary>How many times the dialog was opened.</summary>
    public int Opened { get; private set; }

    /// <summary>The speed it was opened on.</summary>
    public double ShownSpeed { get; private set; }

    public Task<SimulateOptionsResult> ShowAsync(SimulationSettings settings, double engineSpeed)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Opened++;
        ShownSpeed = engineSpeed;

        return Task.FromResult(new SimulateOptionsResult(
            Accept,
            EngineSpeed ?? engineSpeed,
            TotalCycles ?? settings.CycleCount,
            MassBalance ?? settings.MassBalance,
            Graphs));
    }
}
