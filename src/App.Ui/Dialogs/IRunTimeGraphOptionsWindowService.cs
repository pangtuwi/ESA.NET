namespace App.Ui.Dialogs;

/// <summary>What the operator chose in the run-time graph options dialog.</summary>
/// <param name="Accepted">Whether OK was pressed.</param>
/// <param name="ShowGasFlowVelocities">Whether gas-flow velocities should be plotted.</param>
public sealed record RunTimeGraphOptionsResult(
    bool Accepted,
    bool ShowGasFlowVelocities);

/// <summary>Opens the run-time graph options dialog.</summary>
public interface IRunTimeGraphOptionsWindowService
{
    Task<RunTimeGraphOptionsResult> ShowAsync(bool showGasFlowVelocities);
}
