namespace App.Ui.Dialogs;

/// <summary>Opens the About box. Delphi <c>AboutBox.Show</c>.</summary>
public interface IAboutWindowService
{
    Task ShowAsync();
}
