namespace App.Core.Simulation;

/// <summary>
/// Holds a running simulation between steps. Port of Delphi's <c>Paused</c> flag, which
/// <c>Pause1Click</c> toggled and the run loop polled, skipping steps while message
/// processing carried on.
/// </summary>
/// <remarks>
/// It only waits. Nothing about the step that follows changes, so a paused and resumed
/// run produces the same result to the last bit as one that never paused. The wait takes
/// the run's cancellation token, so Stop still ends a paused run.
/// </remarks>
public sealed class RunPause : IDisposable
{
    private readonly ManualResetEventSlim _running = new(initialState: true);

    /// <summary>Whether the run is being held.</summary>
    public bool IsPaused => !_running.IsSet;

    /// <summary>Holds the run at the end of the step in progress.</summary>
    public void Pause() => _running.Reset();

    /// <summary>Lets the run carry on.</summary>
    public void Resume() => _running.Set();

    /// <summary>
    /// Returns at once unless paused; otherwise blocks until resumed or cancelled.
    /// </summary>
    /// <exception cref="OperationCanceledException">The run was stopped while paused.</exception>
    public void WaitWhilePaused(CancellationToken cancellation) => _running.Wait(cancellation);

    public void Dispose() => _running.Dispose();
}
