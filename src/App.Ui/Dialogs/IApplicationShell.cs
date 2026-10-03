namespace App.Ui.Dialogs;

/// <summary>
/// What the shell asks of the application around it: to end, and to hand a file to the
/// operating system. Injected so that a test can press Exit without ending the test host.
/// </summary>
public interface IApplicationShell
{
    /// <summary>Ends the application. Delphi <c>Exit1Click</c>, which closed the main form.</summary>
    void Exit();

    /// <summary>
    /// Opens <paramref name="path"/> in whatever the operating system uses for it.
    /// </summary>
    /// <returns>Whether the file was handed over; false if it is missing or nothing opened it.</returns>
    Task<bool> OpenFileAsync(string path);
}
