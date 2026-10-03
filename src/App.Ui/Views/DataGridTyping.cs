using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace App.Ui.Views;

/// <summary>
/// Lets typing into a selected <see cref="DataGrid"/> cell start editing it, as typing
/// into Delphi's <c>TStringGrid</c> with <c>goEditing</c> did.
/// </summary>
/// <remarks>
/// Avalonia's grid only enters edit mode on F2 or a click on the current cell. F2 is
/// Fn+F2 on most Mac keyboards, so an operator who selects a cell and types sees nothing
/// happen. An attached property rather than code-behind, for the reason
/// <see cref="Charts.ChartHost"/> is one: the views hold no logic.
/// </remarks>
public static class DataGridTyping
{
    /// <summary>Whether a keystroke on a selected cell starts editing it.</summary>
    public static readonly AttachedProperty<bool> BeginEditOnTypingProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, bool>("BeginEditOnTyping", typeof(DataGridTyping));

    static DataGridTyping() =>
        BeginEditOnTypingProperty.Changed.AddClassHandler<DataGrid>(OnChanged);

    public static bool GetBeginEditOnTyping(DataGrid grid) =>
        grid is null ? throw new ArgumentNullException(nameof(grid)) : grid.GetValue(BeginEditOnTypingProperty);

    public static void SetBeginEditOnTyping(DataGrid grid, bool value)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.SetValue(BeginEditOnTypingProperty, value);
    }

    private static void OnChanged(DataGrid grid, AvaloniaPropertyChangedEventArgs args)
    {
        grid.RemoveHandler(InputElement.TextInputEvent, OnTextInput);

        if (args.NewValue is true)
        {
            grid.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Bubble);
        }
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        // Once a cell is editing, its own TextBox takes the keystrokes and marks them handled.
        if (sender is not DataGrid grid || e.Handled || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
        {
            return;
        }

        var typed = e.Text;

        // Like the original, the first keystroke replaces what the cell held.
        void Preparing(object? s, DataGridPreparingCellForEditEventArgs args)
        {
            if (args.EditingElement is TextBox box)
            {
                box.Text = typed;
                box.CaretIndex = typed.Length;
            }
        }

        grid.PreparingCellForEdit += Preparing;

        try
        {
            if (grid.BeginEdit())
            {
                e.Handled = true;
            }
        }
        finally
        {
            grid.PreparingCellForEdit -= Preparing;
        }
    }
}
