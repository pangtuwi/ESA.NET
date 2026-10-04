using App.Core.Charts;
using App.Core.Simulation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// Plots one variable of the last multi-run sweep against another: speed against torque,
/// spark advance against torque, IMEP against burn angle, and so on. Not in the original,
/// which showed a sweep only as its torque curve.
/// </summary>
public sealed partial class SweepPlotViewModel : ObservableObject
{
    private IReadOnlyList<MultiRunRowResult> _rows = [];

    /// <summary>Raised when Close is pressed.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Everything either axis can show.</summary>
    public IReadOnlyList<SweepVariable> Variables => SweepVariables.All;

    /// <summary>The horizontal axis. Engine speed to start with.</summary>
    [ObservableProperty]
    private SweepVariable _selectedX = SweepVariables.Speed;

    /// <summary>The vertical axis. Torque to start with.</summary>
    [ObservableProperty]
    private SweepVariable _selectedY = SweepVariables.Torque;

    /// <summary>Whether the points are joined by a line, or left as a scatter.</summary>
    [ObservableProperty]
    private bool _joinPoints = true;

    /// <summary>The plot, redrawn whenever an axis or the line choice changes.</summary>
    [ObservableProperty]
    private ChartDefinition? _chart;

    /// <summary>How many rows are plotted, and why any are not.</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>Takes the sweep's rows and draws the default plot.</summary>
    public void Load(IReadOnlyList<MultiRunRowResult> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _rows = rows;
        Summary = Describe(rows);
        Redraw();
    }

    partial void OnSelectedXChanged(SweepVariable value) => Redraw();

    partial void OnSelectedYChanged(SweepVariable value) => Redraw();

    partial void OnJoinPointsChanged(bool value) => Redraw();

    private void Redraw()
    {
        // A ComboBox can momentarily clear its selection while its items are replaced.
        if (SelectedX is null || SelectedY is null)
        {
            return;
        }

        Chart = EngineCharts.SweepPlot(_rows, SelectedX, SelectedY, JoinPoints);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private static string Describe(IReadOnlyList<MultiRunRowResult> rows)
    {
        var plotted = rows.Count(r => r.Result is not null);
        var summary = $"{plotted} of {rows.Count} row(s) plotted.";

        var failed = rows.Where(r => r.Result is null).ToList();

        return failed.Count == 0
            ? summary
            : summary + " " + string.Join(" ", failed.Select(
                r => $"Row {r.Row + 1} ({r.Speed:F0} rev/min) failed: {r.Failure}"));
    }
}
