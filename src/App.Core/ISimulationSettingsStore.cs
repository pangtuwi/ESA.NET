using App.Core.Model;

namespace App.Core;

/// <summary>
/// Reads and writes the application defaults held in <c>ESA.ini</c>.
/// </summary>
public interface ISimulationSettingsStore
{
    /// <summary>
    /// Reads the settings file. A missing file yields the Delphi defaults rather than an
    /// error, matching <c>TIniFile</c>'s behaviour in the original.
    /// </summary>
    SimulationSettings Read(string path);

    /// <summary>
    /// Writes the settings file, preserving anything already in it that these settings
    /// do not cover.
    /// </summary>
    void Write(string path, SimulationSettings settings);
}
