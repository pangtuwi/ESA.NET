using System.Globalization;
using App.Core.Model;
using App.Persistence;
using App.Ui.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// One editable cell, writing straight through to the grid behind it so that the run
/// count and the warnings stay live as the operator types.
/// </summary>
public sealed partial class MultiRunCell : ObservableObject
{
    private readonly MultiRunViewModel _owner;

    internal MultiRunCell(MultiRunViewModel owner, int row, int column)
    {
        _owner = owner;
        Row = row;
        Column = column;
        _value = owner.Grid[row, column];
    }

    /// <summary>Zero-based grid row.</summary>
    public int Row { get; }

    /// <summary>Zero-based grid column, indexing <see cref="MultiRunGrid.ColumnNames"/>.</summary>
    public int Column { get; }

    private bool _refreshing;

    [ObservableProperty]
    private string _value;

    partial void OnValueChanged(string value)
    {
        if (!_refreshing)
        {
            _owner.CellEdited(this, value);
        }
    }

    /// <summary>Pushes a value in from the grid without writing back out again.</summary>
    internal void Refresh(string value)
    {
        _refreshing = true;

        try
        {
            Value = value;
        }
        finally
        {
            _refreshing = false;
        }
    }
}

/// <summary>One grid row: its number, as the original's fixed column shows it, and its cells.</summary>
public sealed class MultiRunRow
{
    internal MultiRunRow(MultiRunViewModel owner, int row)
    {
        Number = row + 1;
        Cells = [.. Enumerable.Range(0, MultiRunGrid.ColumnCount).Select(c => new MultiRunCell(owner, row, c))];
    }

    /// <summary>The row number the original prints in its fixed left-hand column.</summary>
    public int Number { get; }

    /// <summary>The fourteen editable cells, in column order.</summary>
    public IReadOnlyList<MultiRunCell> Cells { get; }
}

/// <summary>
/// The multi-run grid editor. Port of <c>TFMultiRun</c> (MultiRun.pas / MultiRun.dfm).
/// </summary>
/// <remarks>
/// <para>
/// A hundred rows of fourteen columns, always all present, with a dash meaning "leave
/// this as the engine file has it" - the original's convention, which
/// <see cref="MultiRunGrid"/> already carries. Load and Save go through
/// <see cref="MultiRunGridStore"/>, so a file opened and saved again keeps the format the
/// original wrote.
/// </para>
/// <para>
/// Two departures from the original, both showing the operator something it hid rather
/// than changing what runs. The run count stops at the first row with no speed, so a
/// blank row silently drops everything below it (ISSUES.md C14); <see cref="Summary"/>
/// says so when it happens. And a file whose speed column counts 1, 2, 3 upwards is a short-format
/// <c>.msr</c> loaded a column over (ISSUES.md C13), which the original swept from 1
/// rev/min without comment; <see cref="Warning"/> names it. Neither refuses the run.
/// </para>
/// </remarks>
public sealed partial class MultiRunViewModel : ObservableObject
{
    private readonly IFileDialogService _files;
    private readonly MultiRunGridStore _store = new();

    public MultiRunViewModel(IFileDialogService files)
    {
        _files = files;
        Rows = [.. Enumerable.Range(0, MultiRunGrid.MaxRuns).Select(row => new MultiRunRow(this, row))];
    }

    /// <summary>Raised when the operator has finished with the window, either way.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The window caption, Delphi <c>FMultiRun.Caption</c>.</summary>
    public static string Title => "Multiple Variable Simulation Run";

    /// <summary>Column headings for the fourteen editable columns.</summary>
    public static IReadOnlyList<string> ColumnNames => MultiRunGrid.ColumnNames;

    /// <summary>The grid being edited. Cells write through to it as they are typed.</summary>
    public MultiRunGrid Grid { get; private set; } = new();

    /// <summary>The hundred rows the window shows.</summary>
    public IReadOnlyList<MultiRunRow> Rows { get; }

    /// <summary>
    /// Delphi <c>OkToMultiRun</c>: true when the operator pressed OK, false when they
    /// cancelled or closed the window.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>The line terminator a loaded file used, so saving it back preserves it.</summary>
    private string _lineTerminator = "\r\n";

    /// <summary>Delphi <c>LFilename</c>: the engine every row starts from.</summary>
    [ObservableProperty]
    private string _baseFile = "Base File : none loaded";

    /// <summary>Delphi <c>CBShowGraphs</c>, checked by default as the form has it.</summary>
    [ObservableProperty]
    private bool _showGraphs = true;

    /// <summary>Delphi <c>StatusBar1.Panels[1]</c>: the grid file last loaded or saved.</summary>
    [ObservableProperty]
    private string _gridFile = string.Empty;

    /// <summary>
    /// Delphi <c>StatusBar1.Panels[0]</c>, which showed the current cell's text. A
    /// <c>DataGrid</c> selects rows rather than cells, so this describes the selected row.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionText))]
    private MultiRunRow? _selectedRow;

    /// <inheritdoc cref="SelectedRow" />
    public string SelectionText
    {
        get
        {
            if (SelectedRow is not { } row)
            {
                return string.Empty;
            }

            var overrides = row.Cells.Count(cell => cell.Value != MultiRunGrid.Unset);

            return $"Row {row.Number}   {overrides} of {MultiRunGrid.ColumnCount} set";
        }
    }

    /// <summary>How many runs the grid describes, Delphi <c>NoRuns</c>.</summary>
    public int RunCount => Grid.RunCount;

    /// <summary>What OK will do, and what it will leave out.</summary>
    public string Summary
    {
        get
        {
            var count = RunCount;
            var ignored = IgnoredRows;

            var runs = count switch
            {
                0 => "No runs. Fill in the Speed column from the first row down.",
                1 => "1 run.",
                _ => $"{count} runs.",
            };

            return ignored == 0
                ? runs
                : $"{runs} {ignored} further row(s) are filled in but will not run: the list "
                  + "stops at the first blank Speed.";
        }
    }

    /// <summary>
    /// Rows with a speed that sit past <see cref="RunCount"/>, i.e. below a gap. The
    /// original drops these without saying anything.
    /// </summary>
    public int IgnoredRows
    {
        get
        {
            var ignored = 0;

            for (var row = RunCount + 1; row < MultiRunGrid.MaxRuns; row++)
            {
                if (Grid.Speed(row) is not null)
                {
                    ignored++;
                }
            }

            return ignored;
        }
    }

    /// <summary>
    /// Set when the loaded file looks like the short format described in ISSUES.md C13,
    /// whose values all land one column over.
    /// </summary>
    [ObservableProperty]
    private string? _warning;

    /// <summary>Points every row at the engine the sweep will start from.</summary>
    public void SetBaseFile(string? path) =>
        BaseFile = string.IsNullOrEmpty(path) ? "Base File : none loaded" : $"Base File : {path}";

    /// <summary>Replaces the grid being edited, refreshing every cell.</summary>
    public void Load(MultiRunGrid grid, string lineTerminator = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(grid);

        Grid = grid;
        _lineTerminator = lineTerminator;

        foreach (var cell in Rows.SelectMany(row => row.Cells))
        {
            cell.Refresh(grid[cell.Row, cell.Column]);
        }

        Rescan();
    }

    /// <summary>Delphi <c>BLoadClick</c>.</summary>
    [RelayCommand]
    private async Task LoadGridAsync()
    {
        if (await _files.OpenMultiRunAsync() is not { } path)
        {
            return;
        }

        try
        {
            var document = _store.Read(path);

            Load(document.Grid, document.LineTerminator);
            GridFile = path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                          or FormatException)
        {
            Warning = $"Could not open {Path.GetFileName(path)}: {error.Message}";
        }
    }

    /// <summary>Delphi <c>BSaveClick</c>.</summary>
    [RelayCommand]
    private async Task SaveGridAsync()
    {
        var suggested = string.IsNullOrEmpty(GridFile) ? "Default.msr" : Path.GetFileName(GridFile);

        if (await _files.SaveMultiRunAsync(suggested) is not { } path)
        {
            return;
        }

        try
        {
            _store.Write(path, new MultiRunGridStore.Document(Grid, _lineTerminator));
            GridFile = path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Warning = $"Could not save {Path.GetFileName(path)}: {error.Message}";
        }
    }

    /// <summary>Delphi <c>BOkClick</c>.</summary>
    [RelayCommand]
    private void Accept()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Delphi <c>BCancelClick</c>.</summary>
    [RelayCommand]
    private void Cancel()
    {
        Accepted = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes an edited cell through to the grid and re-reads what changed.</summary>
    internal void CellEdited(MultiRunCell cell, string value)
    {
        Grid[cell.Row, cell.Column] = value;

        // The grid normalises blank to a dash, so show what it actually stored.
        var stored = Grid[cell.Row, cell.Column];

        if (stored != value)
        {
            cell.Refresh(stored);
        }

        Rescan();
        OnPropertyChanged(nameof(SelectionText));
    }

    private void Rescan()
    {
        Warning = ShortFormatWarning();

        OnPropertyChanged(nameof(RunCount));
        OnPropertyChanged(nameof(IgnoredRows));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// The signature of a short-format file (ISSUES.md C13): a speed column counting 1,
    /// 2, 3 up the grid, because the row number each line starts with has landed there.
    /// </summary>
    private string? ShortFormatWarning()
    {
        var count = RunCount;

        if (count < 3)
        {
            return null;
        }

        for (var row = 0; row < count; row++)
        {
            if (Grid.Speed(row) != row + 1)
            {
                return null;
            }
        }

        return $"The Speed column counts 1 to {count.ToString(CultureInfo.InvariantCulture)}, which "
               + "is what a short-format .msr file looks like when loaded: every value has landed "
               + "one column to the right and the row number has become the speed. Running this "
               + "would sweep the engine from 1 rev/min. See ISSUES.md C13.";
    }
}
