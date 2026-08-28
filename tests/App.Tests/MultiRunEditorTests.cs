using App.Core.Model;
using App.Persistence;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace App.Tests;

/// <summary>
/// The multi-run grid editor: that it edits the grid the sweep will use, that Load and
/// Save go through the <c>.msr</c> store unchanged, and that it says what the original
/// left the operator to work out.
/// </summary>
public sealed class MultiRunEditorTests
{
    /// <summary>Stands in for the file picker, so no dialog opens in a test.</summary>
    private sealed class StubFiles : IFileDialogService
    {
        public string? OpenGridResult { get; set; }

        public string? SaveGridResult { get; set; }

        public string? SuggestedName { get; private set; }

        public Task<string?> OpenEngineAsync() => Task.FromResult<string?>(null);

        public Task<string?> SaveEngineAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> OpenMultiRunAsync() => Task.FromResult(OpenGridResult);

        public Task<string?> SaveMultiRunAsync(string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(SaveGridResult);
        }

        public Task<string?> SaveTextAsync(string title, string suggestedName, string startIn) =>
            Task.FromResult<string?>(null);
    }

    private static (MultiRunViewModel ViewModel, StubFiles Files) Build()
    {
        var files = new StubFiles();

        return (new MultiRunViewModel(files), files);
    }

    [Fact]
    public void TheGridIsAHundredRowsOfFourteenDashes()
    {
        var (viewModel, _) = Build();

        Assert.Equal(MultiRunGrid.MaxRuns, viewModel.Rows.Count);
        Assert.All(viewModel.Rows, row => Assert.Equal(MultiRunGrid.ColumnCount, row.Cells.Count));
        Assert.All(
            viewModel.Rows.SelectMany(row => row.Cells),
            cell => Assert.Equal(MultiRunGrid.Unset, cell.Value));

        // The fixed left-hand column the original prints, 1-based.
        Assert.Equal([1, 2, 3], viewModel.Rows.Take(3).Select(row => row.Number));
        Assert.Equal(MultiRunGrid.ColumnNames, MultiRunViewModel.ColumnNames);
    }

    [Fact]
    public void EditingACellWritesThroughToTheGrid()
    {
        var (viewModel, _) = Build();

        viewModel.Rows[0].Cells[0].Value = "4000";
        viewModel.Rows[0].Cells[1].Value = "6";

        Assert.Equal("4000", viewModel.Grid[0, 0]);
        Assert.Equal(1, viewModel.RunCount);
        Assert.Equal(4000, viewModel.Grid.Speed(0));

        // Clearing a cell puts the dash back, which is the original's "leave this alone".
        viewModel.Rows[0].Cells[0].Value = "   ";

        Assert.Equal(MultiRunGrid.Unset, viewModel.Rows[0].Cells[0].Value);
        Assert.Equal(0, viewModel.RunCount);
    }

    [Fact]
    public void LoadingAGridRefreshesEveryCell()
    {
        var (viewModel, _) = Build();

        var grid = new MultiRunGrid();
        grid[0, 0] = "3000";
        grid[1, 0] = "4000";
        grid[1, 12] = "25";

        viewModel.Load(grid);

        Assert.Equal("3000", viewModel.Rows[0].Cells[0].Value);
        Assert.Equal("25", viewModel.Rows[1].Cells[12].Value);
        Assert.Equal(2, viewModel.RunCount);

        // The cells now write through to the grid that was handed in.
        viewModel.Rows[2].Cells[0].Value = "5000";

        Assert.Equal("5000", grid[2, 0]);
    }

    [Fact]
    public void ARunStoppingAtAGapIsReported()
    {
        var (viewModel, _) = Build();

        viewModel.Rows[0].Cells[0].Value = "3000";
        viewModel.Rows[1].Cells[0].Value = "4000";

        Assert.Equal(2, viewModel.RunCount);
        Assert.Equal(0, viewModel.IgnoredRows);
        Assert.Equal("2 runs.", viewModel.Summary);

        // A gap: the original silently drops everything below it.
        viewModel.Rows[3].Cells[0].Value = "5000";
        viewModel.Rows[4].Cells[0].Value = "6000";

        Assert.Equal(2, viewModel.RunCount);
        Assert.Equal(2, viewModel.IgnoredRows);
        Assert.Contains("2 further row(s)", viewModel.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShortFormatFileLoadsIntoTheRightColumnsAndSaysSo()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // One of the forty-three files of ISSUES.md C13, a cell short of the current
        // format. The original filled from the right and swept the engine from 1 rev/min;
        // this loads the speeds it actually holds and warns that saving rewrites the file.
        var source = Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Default", "Default.msr");

        Assert.SkipUnless(File.Exists(source), "Default.msr is not in this checkout.");

        var (viewModel, files) = Build();

        files.OpenGridResult = source;

        await viewModel.LoadGridCommand.ExecuteAsync(null);

        Assert.True(viewModel.Grid.Speed(0) >= 100);
        Assert.NotNull(viewModel.Warning);
        Assert.Contains("C13", viewModel.Warning, StringComparison.Ordinal);

        // The notice is about the file, not about anything the operator can retype away.
        viewModel.Rows[0].Cells[0].Value = "3000";

        Assert.NotNull(viewModel.Warning);
    }

    [Fact]
    public void AGridInTheCurrentFormatCarriesNoNotice()
    {
        var (viewModel, _) = Build();
        var grid = new MultiRunGrid();

        grid[0, 0] = "3000";
        viewModel.Load(grid);

        Assert.Null(viewModel.Warning);
    }

    [Fact]
    public async Task LoadAndSaveGoThroughTheMsrStoreUnchanged()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // A file in the current fifteen-field format. Default.msr next to it is one of
        // the forty-three short-format files of ISSUES.md C13, which cannot round-trip
        // because writing it back fills in the column it is missing.
        var source = Path.Combine(TestPaths.Legacy!, "ESA", "Data", "Default", "Default1000.msr");

        Assert.SkipUnless(File.Exists(source), "Default1000.msr is not in this checkout.");

        var (viewModel, files) = Build();
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msr");

        try
        {
            files.OpenGridResult = source;
            files.SaveGridResult = target;

            await viewModel.LoadGridCommand.ExecuteAsync(null);

            Assert.Equal(source, viewModel.GridFile);
            Assert.True(viewModel.RunCount > 0);

            await viewModel.SaveGridCommand.ExecuteAsync(null);

            // The editor must not restyle a file it merely opened and saved.
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public async Task SavingSuggestsTheOriginalsDefaultName()
    {
        var (viewModel, files) = Build();

        await viewModel.SaveGridCommand.ExecuteAsync(null);

        Assert.Equal("Default.msr", files.SuggestedName);
    }

    [Fact]
    public void OkAndCancelBothCloseAndSayWhichWasPressed()
    {
        var (ok, _) = Build();
        var (cancel, _) = Build();
        var closes = 0;

        ok.CloseRequested += (_, _) => closes++;
        cancel.CloseRequested += (_, _) => closes++;

        Assert.False(ok.Accepted);

        ok.AcceptCommand.Execute(null);
        cancel.CancelCommand.Execute(null);

        Assert.True(ok.Accepted);
        Assert.False(cancel.Accepted);
        Assert.Equal(2, closes);
    }

    [AvaloniaFact]
    public void TheWindowShowsTheFifteenColumnsTheOriginalDoes()
    {
        var (viewModel, _) = Build();
        var window = new MultiRunWindow { DataContext = viewModel };

        var grid = window.FindControl<DataGrid>("RunGrid")
                   ?? throw new InvalidOperationException("The multi-run window has no grid.");

        // The fixed row-number column, then the fourteen editable ones in order.
        Assert.Equal(
            ["No", .. MultiRunGrid.ColumnNames],
            grid.Columns.Select(column => column.Header as string));

        Assert.True(grid.Columns[0].IsReadOnly);
        Assert.False(grid.IsReadOnly);
        Assert.Same(viewModel.Rows, grid.ItemsSource);
    }

    [AvaloniaFact]
    public void TheWindowCarriesTheOriginalsButtonsAndCheckBox()
    {
        var (viewModel, _) = Build();
        var window = new MultiRunWindow { DataContext = viewModel };

        foreach (var name in new[] { "LoadButton", "SaveButton", "OkButton", "CancelButton" })
        {
            var button = window.FindControl<Button>(name);

            Assert.NotNull(button);
            Assert.NotNull(button.Command);
        }

        var showGraphs = window.FindControl<CheckBox>("ShowGraphsBox");

        // Delphi has it checked in the form definition.
        Assert.NotNull(showGraphs);
        Assert.True(viewModel.ShowGraphs);

        Assert.NotNull(window.FindControl<TextBlock>("BaseFileText"));
        Assert.NotNull(window.FindControl<TextBlock>("SummaryText"));
    }

    [Fact]
    public void TheBaseFileCaptionNamesTheEngineTheSweepStartsFrom()
    {
        var (viewModel, _) = Build();

        Assert.Contains("none loaded", viewModel.BaseFile, StringComparison.Ordinal);

        viewModel.SetBaseFile(@"C:\CAEEng\A2China.eng");

        Assert.Equal(@"Base File : C:\CAEEng\A2China.eng", viewModel.BaseFile);
    }
}
