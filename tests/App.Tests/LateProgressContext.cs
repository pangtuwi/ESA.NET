using System.Collections.Concurrent;

namespace App.Tests;

/// <summary>
/// A synchronization context that holds back every <see cref="Progress{T}"/> report until
/// <see cref="ReleaseAsync"/>, and runs everything else on the thread pool straight away.
/// </summary>
/// <remarks>
/// Makes deterministic the race behind ISSUES.md A15. <see cref="Progress{T}"/> posts each
/// report to the context it was created on, and only a single-threaded, first-in-first-out
/// context - Avalonia's dispatcher - guarantees those reports run before whatever the view
/// model does after the run's <c>await</c>. Under xUnit they went to the thread pool, and
/// once in about a dozen full-suite runs a late one landed after the final status line.
/// Holding them all until the command has finished makes every report late.
/// </remarks>
internal sealed class LateProgressContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _held = new();

    /// <summary>How many reports have been held back so far.</summary>
    public int Held => _held.Count;

    /// <summary>
    /// Starts <paramref name="action"/> with this as the current context, so anything it
    /// creates before its first real <c>await</c> - a <see cref="Progress{T}"/> included -
    /// captures it, and restores the caller's context before returning.
    /// </summary>
    public Task Start(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = Current;
        SetSynchronizationContext(this);

        try
        {
            return action();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>Runs every held report, in the order they were made, and waits for them.</summary>
    public Task ReleaseAsync() =>
        Task.Run(() =>
        {
            while (_held.TryDequeue(out var report))
            {
                Run(report.Callback, report.State);
            }
        });

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (IsProgressReport(d))
        {
            _held.Enqueue((d, state));
            return;
        }

        ThreadPool.QueueUserWorkItem(_ => Run(d, state));
    }

    public override SynchronizationContext CreateCopy() => this;

    // Progress<T> posts a callback bound to itself, so its target is the Progress<T>.
    private static bool IsProgressReport(SendOrPostCallback d) =>
        d.Target?.GetType() is { IsGenericType: true } type
        && type.GetGenericTypeDefinition() == typeof(Progress<>);

    private void Run(SendOrPostCallback callback, object? state)
    {
        var previous = Current;
        SetSynchronizationContext(this);

        try
        {
            callback(state);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
