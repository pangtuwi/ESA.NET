using App.Ui.ViewModels;

namespace App.Ui.Dialogs;

/// <summary>What the operator chose in the run-time graph options dialog.</summary>
/// <param name="Accepted">Whether OK was pressed.</param>
/// <param name="Options">The options as the dialog left them; the ones passed in if cancelled.</param>
public sealed record RunTimeGraphOptionsResult(bool Accepted, GraphOptions Options);

/// <summary>Opens the run-time graph options dialog.</summary>
public interface IRunTimeGraphOptionsWindowService
{
    Task<RunTimeGraphOptionsResult> ShowAsync(GraphOptions current);
}
