using System.Globalization;

namespace App.Core;

/// <summary>
/// A loaded <c>.eng</c> engine definition: the primary ESA data format, a text INI
/// file (SPEC.md section 3).
/// </summary>
/// <remarks>
/// <para>
/// Only the raw accessors are abstract. A concrete implementation supplies
/// name/value access over whatever it loaded; everything typed is derived here so
/// the conversion rules live in one place.
/// </para>
/// <para>
/// Implementations are required to preserve the underlying file byte for byte
/// across a load/save cycle that changes nothing, and to leave every untouched
/// line alone when a single value is written. That matters because the shipped
/// data includes files from an older schema: five <c>Example1</c> engines carry
/// <c>[InManifold]</c> and <c>[ExManifold]</c> sections that SPEC.md does not
/// document. Round-tripping must not silently drop them.
/// </para>
/// </remarks>
public abstract class EngineDefinition
{
    /// <summary>Section names, in file order.</summary>
    public abstract IReadOnlyList<string> Sections { get; }

    /// <summary>Key names within a section, in file order. Empty if the section is absent.</summary>
    public abstract IReadOnlyList<string> KeysIn(string section);

    /// <summary>
    /// The raw text of a value, or <see langword="null"/> when the section or key is
    /// absent. Lookup is case-insensitive: Edit.pas reads <c>CdIvIn</c> while every
    /// shipped file writes <c>CdIVIn</c>, and Delphi's <c>TIniFile</c> does not care.
    /// </summary>
    public abstract string? GetValue(string section, string key);

    /// <summary>
    /// Writes the raw text of a value, replacing an existing entry in place or
    /// appending a new one.
    /// </summary>
    public abstract void SetValue(string section, string key, string value);

    // Defaults below match the Delphi fallbacks in Edit.pas LoadTextFile.

    public string Name
    {
        get => GetString("Cylinders", "Name", "No Name Found");
        set => SetValue("Cylinders", "Name", value);
    }

    public int CylinderCount
    {
        get => GetInt32("Cylinders", "NoCyls", 0);
        set => SetInt32("Cylinders", "NoCyls", value);
    }

    public double Bore
    {
        get => GetDouble("Cylinders", "Bore", 0);
        set => SetDouble("Cylinders", "Bore", value);
    }

    public double Stroke
    {
        get => GetDouble("Cylinders", "Stroke", 0);
        set => SetDouble("Cylinders", "Stroke", value);
    }

    public double CompressionRatio
    {
        get => GetDouble("Cylinders", "CR", 0);
        set => SetDouble("Cylinders", "CR", value);
    }

    public double ConrodLength
    {
        get => GetDouble("Cylinders", "ConrodLength", 0);
        set => SetDouble("Cylinders", "ConrodLength", value);
    }

    public string WallTemperatureFile
    {
        get => GetString("HeatTransfer", "TempFile", "Default.cwt");
        set => SetValue("HeatTransfer", "TempFile", value);
    }

    public double WoshiniCoefficient
    {
        get => GetDouble("HeatTransfer", "CWoshini", 131);
        set => SetDouble("HeatTransfer", "CWoshini", value);
    }

    public string InletAreaFile
    {
        get => GetString("Inlet", "AreaFile", "Default.maf");
        set => SetValue("Inlet", "AreaFile", value);
    }

    public string PlenumPressureFunction
    {
        get => GetString("Inlet", "FPlenumP", "99.0");
        set => SetValue("Inlet", "FPlenumP", value);
    }

    public string InletGridFunction
    {
        get => GetString("Inlet", "InletGrid", "50");
        set => SetValue("Inlet", "InletGrid", value);
    }

    public string InletValveReverseFunction
    {
        get => GetString("Inlet", "IVRFn", "0");
        set => SetValue("Inlet", "IVRFn", value);
    }

    public string InletValveForwardFunction
    {
        get => GetString("Inlet", "IVFFn", "0");
        set => SetValue("Inlet", "IVFFn", value);
    }

    public string InletValveForwardReverseFunction
    {
        get => GetString("Inlet", "IVFRFn", "0");
        set => SetValue("Inlet", "IVFRFn", value);
    }

    public string ExhaustAreaFile
    {
        get => GetString("Exhaust", "AreaFile", "Default.maf");
        set => SetValue("Exhaust", "AreaFile", value);
    }

    public string ExhaustBackPressureFile
    {
        get => GetString("Exhaust", "ExhBackFile", "Default.exh");
        set => SetValue("Exhaust", "ExhBackFile", value);
    }

    public string ExhaustGridFunction
    {
        get => GetString("Exhaust", "ExhaustGrid", "50");
        set => SetValue("Exhaust", "ExhaustGrid", value);
    }

    public string ExhaustValveReverseFunction
    {
        get => GetString("Exhaust", "EVRFn", "0");
        set => SetValue("Exhaust", "EVRFn", value);
    }

    public string ExhaustValveForwardFunction
    {
        get => GetString("Exhaust", "EVFFn", "0");
        set => SetValue("Exhaust", "EVFFn", value);
    }

    public string ExhaustValveForwardReverseFunction
    {
        get => GetString("Exhaust", "EVFRFn", "0");
        set => SetValue("Exhaust", "EVFRFn", value);
    }

    /// <summary>Inlet valve opening angle, <c>IVO</c>.</summary>
    public double InletValveOpen
    {
        get => GetDouble("Cams", "IVO", 0);
        set => SetDouble("Cams", "IVO", value);
    }

    public double InletValveClose
    {
        get => GetDouble("Cams", "IVC", 0);
        set => SetDouble("Cams", "IVC", value);
    }

    public double ExhaustValveOpen
    {
        get => GetDouble("Cams", "EVO", 0);
        set => SetDouble("Cams", "EVO", value);
    }

    public double ExhaustValveClose
    {
        get => GetDouble("Cams", "EVC", 0);
        set => SetDouble("Cams", "EVC", value);
    }

    public double InletValveLift
    {
        get => GetDouble("Cams", "IVLift", 0);
        set => SetDouble("Cams", "IVLift", value);
    }

    public double ExhaustValveLift
    {
        get => GetDouble("Cams", "EVLift", 0);
        set => SetDouble("Cams", "EVLift", value);
    }

    public string InletValveProfileFile
    {
        get => GetString("Cams", "IVProfile", string.Empty);
        set => SetValue("Cams", "IVProfile", value);
    }

    public string ExhaustValveProfileFile
    {
        get => GetString("Cams", "EVProfile", string.Empty);
        set => SetValue("Cams", "EVProfile", value);
    }

    public int InletValveCount
    {
        get => GetInt32("Valves", "IVNo", 0);
        set => SetInt32("Valves", "IVNo", value);
    }

    public int ExhaustValveCount
    {
        get => GetInt32("Valves", "EVNo", 0);
        set => SetInt32("Valves", "EVNo", value);
    }

    public double InletValveDiameter
    {
        get => GetDouble("Valves", "IVDiam", 0);
        set => SetDouble("Valves", "IVDiam", value);
    }

    public double ExhaustValveDiameter
    {
        get => GetDouble("Valves", "EVDiam", 0);
        set => SetDouble("Valves", "EVDiam", value);
    }

    public string InletValveCdInwardFile
    {
        get => GetString("Valves", "CdIvIn", "Default.vcd");
        set => SetValue("Valves", "CdIvIn", value);
    }

    public string InletValveCdOutwardFile
    {
        get => GetString("Valves", "CdIvOut", "Default.vcd");
        set => SetValue("Valves", "CdIvOut", value);
    }

    public string ExhaustValveCdInwardFile
    {
        get => GetString("Valves", "CdEvIn", "Default.vcd");
        set => SetValue("Valves", "CdEvIn", value);
    }

    public string ExhaustValveCdOutwardFile
    {
        get => GetString("Valves", "CdEvOut", "Default.vcd");
        set => SetValue("Valves", "CdEvOut", value);
    }

    public double BurnAngle
    {
        get => GetDouble("Fuel", "BurnAngle", 55);
        set => SetDouble("Fuel", "BurnAngle", value);
    }

    public string SparkAngleFile
    {
        get => GetString("Fuel", "SparkAngle", "Default.spk");
        set => SetValue("Fuel", "SparkAngle", value);
    }

    public double AirFuelRatio
    {
        get => GetDouble("Fuel", "AFRatio", 0);
        set => SetDouble("Fuel", "AFRatio", value);
    }

    public double FuelTemperature
    {
        get => GetDouble("Fuel", "TFuel", 0);
        set => SetDouble("Fuel", "TFuel", value);
    }

    public double FuelCalorificValue
    {
        get => GetDouble("Fuel", "QFuel", 0);
        set => SetDouble("Fuel", "QFuel", value);
    }

    public double Lambda
    {
        get => GetDouble("Fuel", "Lambda", 0.96);
        set => SetDouble("Fuel", "Lambda", value);
    }

    public double AtmosphericTemperature
    {
        get => GetDouble("Conditions", "TAtm", 0);
        set => SetDouble("Conditions", "TAtm", value);
    }

    public double AtmosphericPressure
    {
        get => GetDouble("Conditions", "PAtm", 0);
        set => SetDouble("Conditions", "PAtm", value);
    }

    public double OilViscosity
    {
        get => GetDouble("Conditions", "vOil", 0);
        set => SetDouble("Conditions", "vOil", value);
    }

    public bool VariableGamma
    {
        get => GetString("Calculation", "VariableGamma", "0") == "1";
        set => SetValue("Calculation", "VariableGamma", value ? "1" : "0");
    }

    public bool SaveManifoldData
    {
        get => GetString("Calculation", "SaveManfData", "0") == "1";
        set => SetValue("Calculation", "SaveManfData", value ? "1" : "0");
    }

    public Integrator Integrator
    {
        get => (Integrator)GetInt32("Calculation", "Integrator", 0);
        set => SetInt32("Calculation", "Integrator", (int)value);
    }

    public string PerformanceDataFile
    {
        get => GetString("Calculation", "PerfDataSave", "SimulDat.txt");
        set => SetValue("Calculation", "PerfDataSave", value);
    }

    /// <summary>Reads a value, falling back to the Delphi default when it is absent.</summary>
    protected string GetString(string section, string key, string fallback) =>
        GetValue(section, key) ?? fallback;

    /// <summary>
    /// Reads a numeric value with invariant parsing. The legacy files always use a
    /// dot decimal separator, so parsing must never follow the current culture.
    /// </summary>
    protected double GetDouble(string section, string key, double fallback)
    {
        var raw = GetValue(section, key);
        return raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    protected int GetInt32(string section, string key, int fallback)
    {
        var raw = GetValue(section, key);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    protected void SetDouble(string section, string key, double value) =>
        SetValue(section, key, value.ToString(CultureInfo.InvariantCulture));

    protected void SetInt32(string section, string key, int value) =>
        SetValue(section, key, value.ToString(CultureInfo.InvariantCulture));
}
