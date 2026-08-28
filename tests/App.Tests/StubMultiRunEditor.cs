using App.Core.Model;
using App.Ui.Dialogs;

namespace App.Tests;

/// <summary>
/// Stands in for the multi-run grid editor, which the Multi Point Simulation command
/// opens before it sweeps. Tests set the grid the operator would have typed.
/// </summary>
internal sealed class StubMultiRunEditor : IMultiRunWindowService
{
    /// <summary>What the editor hands back. Defaults to whatever it was given.</summary>
    public MultiRunGrid? Grid { get; set; }

    /// <summary>Whether OK was pressed.</summary>
    public bool Accept { get; set; } = true;

    /// <summary>The Show Graphs check box.</summary>
    public bool ShowGraphs { get; set; }

    /// <summary>How many times the editor was opened.</summary>
    public int Opened { get; private set; }

    /// <summary>The base file caption it was passed.</summary>
    public string? BaseFile { get; private set; }

    public Task<MultiRunEditResult> ShowAsync(MultiRunGrid grid, string? baseFile)
    {
        Opened++;
        BaseFile = baseFile;

        return Task.FromResult(new MultiRunEditResult(Accept, Grid ?? grid, ShowGraphs));
    }
}
