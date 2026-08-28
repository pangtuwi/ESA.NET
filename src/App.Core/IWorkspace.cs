namespace App.Core;

/// <summary>
/// The application's data folder: where engines are kept and where the results of a run
/// are put.
/// </summary>
/// <remarks>
/// <para>
/// The Delphi original had no such notion. It opened its nine manifold output files with
/// bare relative names (ISSUES.md C4), so they landed in whatever the working directory
/// happened to be, and <c>SimulDat.txt</c> beside them; the next run overwrote both, and
/// nothing recorded which engine or which speed had produced them.
/// </para>
/// <para>
/// Everything a run writes now goes into a folder of its own, alongside copies of the
/// files it read. <c>data/baseline/</c> is that arrangement made by hand, and
/// <c>BASELINE.md</c> gives the reason it is worth the disk: a result whose inputs travel
/// with it can be trusted afterwards, and one whose inputs have since been edited cannot.
/// </para>
/// </remarks>
public interface IWorkspace
{
    /// <summary>The data folder itself.</summary>
    string RootDirectory { get; }

    /// <summary>Where engine files and their side files are kept.</summary>
    string EnginesDirectory { get; }

    /// <summary>The parent of every run folder.</summary>
    string RunsDirectory { get; }

    /// <summary>
    /// Creates the folder structure, so the operator has somewhere to put engine files
    /// before anything has been run. Doing it twice is harmless.
    /// </summary>
    void Prepare();

    /// <summary>
    /// Creates and returns a folder for one run, named for the time and the engine.
    /// </summary>
    /// <param name="engineFilePath">
    /// The <c>.eng</c> the run started from; only its file name is used. May be empty,
    /// which names the folder for the time alone.
    /// </param>
    /// <param name="startedAt">When the run started, which dates the folder.</param>
    string CreateRunDirectory(string engineFilePath, DateTimeOffset startedAt);
}
