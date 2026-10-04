using App.Core.Model;

namespace App.Ui.Dialogs;

/// <summary>What the operator did with the multi-run grid editor.</summary>
/// <param name="Accepted">Delphi <c>OkToMultiRun</c>: true when OK was pressed.</param>
/// <param name="Grid">The edited grid, whether or not it was accepted.</param>
/// <param name="ShowGraphs">Delphi <c>CBShowGraphs</c>.</param>
/// <param name="Physics">
/// The physics the sweep should run on. Not in the original; the same choice the Single
/// Speed dialog offers, so a sweep need not borrow it from the last single-point run.
/// </param>
public sealed record MultiRunEditResult(bool Accepted, MultiRunGrid Grid, bool ShowGraphs, PhysicsMode Physics);

/// <summary>Opens the multi-run grid editor. Injected for the same reason the editor is.</summary>
public interface IMultiRunWindowService
{
    /// <summary>
    /// Shows the editor on <paramref name="grid"/> and waits for it, as
    /// <c>FMultiRun.ShowModal</c> does.
    /// </summary>
    /// <param name="grid">The grid to edit, carried over from the last time it was opened.</param>
    /// <param name="baseFile">The engine every row starts from, for the window's caption.</param>
    /// <param name="physics">The session's physics, which the window starts from.</param>
    Task<MultiRunEditResult> ShowAsync(MultiRunGrid grid, string? baseFile, PhysicsCorrections physics);
}
