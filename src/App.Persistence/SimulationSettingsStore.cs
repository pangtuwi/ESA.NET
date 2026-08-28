using System.Globalization;
using App.Core;
using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// Reads and writes <c>ESA.ini</c>, the application defaults. Port of
/// <c>IniValues.LoadIniValues</c> (inivalues.pas).
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="IniDocument"/> rather than a second INI parser, so <c>ESA.ini</c>
/// gets the same guarantee as <c>.eng</c>: writing back an unchanged settings file
/// reproduces it byte for byte, and changing one setting rewrites one line.
/// </para>
/// <para>
/// SPEC.md section 3 quotes an idealised version of this file. The one that actually
/// ships names <c>CAEEng.err</c> rather than <c>ESA2z1z.err</c>, sets
/// <c>MassBalance=0.5</c> rather than <c>1</c>, and ends without a trailing newline.
/// The file wins; the defaults below are the ones in <c>LoadIniValues</c>, which are
/// what applies when the file or a key is missing.
/// </para>
/// <para>
/// <c>IniValues.SaveIniValues</c> is declared but empty in the original (SPEC.md
/// section 6), so the Delphi application never wrote this file back. The port
/// implements it properly.
/// </para>
/// </remarks>
public sealed class SimulationSettingsStore : ISimulationSettingsStore
{
    private const string DefaultFiles = "DefaultFiles";
    private const string Simulation = "Simulation";
    private const string Folders = "Folders";

    /// <summary>The file the original keeps beside its executable.</summary>
    public const string FileName = "ESA.ini";

    public SimulationSettings Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            // A missing ESA.ini is not an error; Delphi's TIniFile returns every
            // default in that case, and so does this.
            return new SimulationSettings();
        }

        var document = IniDocument.Parse(File.ReadAllBytes(path));
        var settings = new SimulationSettings();

        settings.ErrorLogFileName = document.GetValue(DefaultFiles, "ErrorLog") ?? settings.ErrorLogFileName;
        settings.TextSaveFileName = document.GetValue(DefaultFiles, "TextSave") ?? settings.TextSaveFileName;
        settings.EngineFileName = document.GetValue(DefaultFiles, "Engine") ?? settings.EngineFileName;
        settings.DataFolder = document.GetValue(Folders, "Data") ?? settings.DataFolder;

        settings.EngineSpeed = ReadDouble(document, Simulation, "EngineSpeed", settings.EngineSpeed);
        settings.CycleCount = ReadInt32(document, Simulation, "Nocycles", settings.CycleCount);
        settings.OneZoneCycleCount = ReadInt32(document, Simulation, "No1zcycles", settings.OneZoneCycleCount);
        settings.MassBalance = ReadDouble(document, Simulation, "MassBalance", settings.MassBalance);

        return settings;
    }

    public void Write(string path, SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(settings);

        var document = File.Exists(path)
            ? IniDocument.Parse(File.ReadAllBytes(path))
            : IniDocument.Parse(string.Empty);

        document.SetValue(DefaultFiles, "ErrorLog", settings.ErrorLogFileName);
        document.SetValue(DefaultFiles, "TextSave", settings.TextSaveFileName);
        document.SetValue(DefaultFiles, "Engine", settings.EngineFileName);

        // Only written once there is one, so every ESA.ini that predates the data folder
        // - which is all of them - keeps its existing bytes.
        if (!string.IsNullOrWhiteSpace(settings.DataFolder))
        {
            document.SetValue(Folders, "Data", settings.DataFolder);
        }

        document.SetValue(Simulation, "EngineSpeed", Text(settings.EngineSpeed));
        document.SetValue(Simulation, "Nocycles", Text(settings.CycleCount));
        document.SetValue(Simulation, "No1zcycles", Text(settings.OneZoneCycleCount));
        document.SetValue(Simulation, "MassBalance", Text(settings.MassBalance));

        File.WriteAllBytes(path, document.ToBytes());
    }

    private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static double ReadDouble(IniDocument document, string section, string key, double fallback)
    {
        var raw = document.GetValue(section, key);

        return raw is not null
               && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static int ReadInt32(IniDocument document, string section, string key, int fallback)
    {
        var raw = document.GetValue(section, key);

        return raw is not null
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
