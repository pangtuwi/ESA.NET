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

    // ---------------------------------------------------------------------------
    // Fuel composition.
    //
    // The Delphi Edit form has C, H, N and O fields and copies them into the plenum,
    // cylinder and exhaust gases (Edit.pas lines 475-495), from where they feed the
    // equilibrium model through TProp.Setup. But no .eng key ever held them, so the
    // original silently reset the composition to its form defaults on every load —
    // a real defect, and one that matters because phase 4's chemistry depends on
    // these numbers.
    //
    // The port reads optional [Fuel] keys and falls back to those same defaults. The
    // keys are written only when a value actually changes, so every existing file
    // stays byte-identical until someone edits it.
    // ---------------------------------------------------------------------------

    /// <summary>Carbon atoms per fuel molecule. Delphi form default 7.</summary>
    public int FuelCarbon
    {
        get => GetInt32("Fuel", "C", DefaultFuelCarbon);
        set => SetInt32("Fuel", "C", value);
    }

    /// <summary>Hydrogen atoms per fuel molecule. Delphi form default 17.</summary>
    public int FuelHydrogen
    {
        get => GetInt32("Fuel", "H", DefaultFuelHydrogen);
        set => SetInt32("Fuel", "H", value);
    }

    /// <summary>Oxygen atoms per fuel molecule. Delphi form default 0.</summary>
    public int FuelOxygen
    {
        get => GetInt32("Fuel", "O", DefaultFuelOxygen);
        set => SetInt32("Fuel", "O", value);
    }

    /// <summary>Nitrogen atoms per fuel molecule. Delphi form default 0.</summary>
    public int FuelNitrogen
    {
        get => GetInt32("Fuel", "N", DefaultFuelNitrogen);
        set => SetInt32("Fuel", "N", value);
    }

    /// <summary>The composition the Delphi Edit form starts with: a C7H17 petrol surrogate.</summary>
    public const int DefaultFuelCarbon = 7;

    public const int DefaultFuelHydrogen = 17;

    public const int DefaultFuelOxygen = 0;

    public const int DefaultFuelNitrogen = 0;

    /// <summary>True when the file carries any explicit fuel composition.</summary>
    public bool HasFuelComposition =>
        GetValue("Fuel", "C") is not null || GetValue("Fuel", "H") is not null
        || GetValue("Fuel", "O") is not null || GetValue("Fuel", "N") is not null;

    // ---------------------------------------------------------------------------
    // The older, undocumented schema.
    //
    // Five Example1 engines (Nissan1-5.eng) and legacy/CAEEng/A4LowCost.eng predate
    // the [Inlet]/[Exhaust] sections and instead use [InManifold] and [ExManifold],
    // with wall temperatures and exhaust back pressure written inline rather than in
    // .cwt and .exh files. SPEC.md section 3 does not mention any of it. These
    // accessors give the loader and the Edit form a way to see those values; nothing
    // writes them, so the files keep round-tripping unchanged.
    //
    // The schema is the predecessor's, CAEEng (1999), whose Edit.pas read it. Its
    // units are not the current schema's: walls in Celsius, plenum pressure in kPa,
    // grid sizes as fixed point counts under [Calculation] (ISSUES.md A31-A33).
    // Nissan1-3 carry both schemas; there the current keys win, as they did for
    // ESA 3.0, which never read the older ones.
    // ---------------------------------------------------------------------------

    public double InletInsertLength => GetDouble("InManifold", "InsertL", 0);

    public double InletInsertAt => GetDouble("InManifold", "InsertAt", 0);

    public double ExhaustInsertLength => GetDouble("ExManifold", "InsertL", 0);

    public double ExhaustInsertAt => GetDouble("ExManifold", "InsertAt", 0);

    /// <summary>True when the file lists wall temperatures inline.</summary>
    public bool HasInlineWallTemperatures => GetValue("Cylinders", "THead") is not null;

    /// <summary>
    /// True when the inline wall temperatures are the ones to use: the file lists them and
    /// names no <c>[HeatTransfer] TempFile</c>. Nissan1-3 do both, and ESA 3.0 read only
    /// the <c>.cwt</c> (<c>Edit.pas:258</c>), so the <c>.cwt</c> wins (ISSUES.md A32).
    /// </summary>
    public bool UsesInlineWallTemperatures =>
        HasInlineWallTemperatures && GetValue("HeatTransfer", "TempFile") is null;

    // The four inline wall temperatures are as the file writes them, in degrees Celsius.
    // The predecessor's Edit.pas added 273.15 to each (THead := StrToFloatf(ETHead.Text)
    // + 273.15); a .cwt holds kelvin, so the loader converts (ISSUES.md A32).

    public double InlineHeadTemperature => GetDouble("Cylinders", "THead", 0);

    public double InlinePistonTemperature => GetDouble("Cylinders", "TPiston", 0);

    public double InlineUpperLinerTemperature => GetDouble("Cylinders", "TULiner", 0);

    public double InlineLowerLinerTemperature => GetDouble("Cylinders", "TLLiner", 0);

    /// <summary>True when the file gives one exhaust back pressure inline.</summary>
    public bool HasInlineExhaustBackPressure => GetValue("ExManifold", "ExhBackP") is not null;

    /// <summary>
    /// True when the inline back pressure is the one to use: the file gives it and names
    /// no <c>[Exhaust] ExhBackFile</c>. Nissan1-3 do both; ESA 3.0 read only the
    /// <c>.exh</c> (<c>Edit.pas:269</c>), so the <c>.exh</c> wins (ISSUES.md A32).
    /// </summary>
    public bool UsesInlineExhaustBackPressure =>
        HasInlineExhaustBackPressure && GetValue("Exhaust", "ExhBackFile") is null;

    public double InlineExhaustBackPressure => GetDouble("ExManifold", "ExhBackP", 0);

    public double InlineExhaustTemperature => GetDouble("ExManifold", "ExhT", 0);

    /// <summary>
    /// True when the file uses the older schema. Such files have no <c>[Calculation]</c>
    /// section either, so the calculation settings fall back to their Delphi defaults.
    /// </summary>
    public bool UsesOlderManifoldSchema =>
        Sections.Any(s => string.Equals(s, "InManifold", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The inlet area file under whichever schema the file uses. The older engines put
    /// <c>AreaFile</c> under <c>[InManifold]</c> rather than <c>[Inlet]</c>.
    /// </summary>
    public string EffectiveInletAreaFile =>
        GetValue("Inlet", "AreaFile") ?? GetValue("InManifold", "AreaFile") ?? "Default.maf";

    /// <summary>The exhaust area file under whichever schema the file uses.</summary>
    public string EffectiveExhaustAreaFile =>
        GetValue("Exhaust", "AreaFile") ?? GetValue("ExManifold", "AreaFile") ?? "Default.maf";

    /// <summary>
    /// The plenum pressure expression in pascals, under whichever schema the file uses.
    /// </summary>
    /// <remarks>
    /// The two schemas disagree about units as well as location: the current one writes
    /// <c>FPlenumP=(99000)</c> in pascals, an expression in <c>N</c>; the older one
    /// <c>PlenumP=99</c>, a plain number in kilopascals, which the predecessor's
    /// <c>Edit.pas</c> scaled with <c>PlenumP6000 := StrToFloatF(EPPlenum.Text)*1e3</c>.
    /// The older value is scaled the same way here, so <c>ManifoldSolver</c> always gets
    /// pascals (ISSUES.md A31). When a file carries both, <c>FPlenumP</c> wins.
    /// </remarks>
    public string EffectivePlenumPressure
    {
        get
        {
            if (GetValue("Inlet", "FPlenumP") is { } current)
            {
                return current;
            }

            if (GetValue("InManifold", "PlenumP") is not { } older)
            {
                return "99.0";
            }

            return double.TryParse(older, NumberStyles.Float, CultureInfo.InvariantCulture, out var kilopascals)
                ? (kilopascals * 1e3).ToString("R", CultureInfo.InvariantCulture)
                : $"({older.Trim()})*1000";
        }
    }

    /// <summary>The inlet grid size expression under whichever schema the file uses.</summary>
    /// <remarks>
    /// The current schema writes <c>[Inlet] InletGrid</c>, an expression in <c>N</c> and
    /// <c>L</c>. The older one writes <c>[Calculation] InletGrid=13</c>, a fixed point
    /// count, used as it stands unless <c>VarInletGrid=1</c> (the predecessor's
    /// <c>IGrid.UserDefined := StrToInt(EInletGrid.Text)</c>). Without the fallback such
    /// a file ran on the default of 50 (ISSUES.md A33).
    /// </remarks>
    public string EffectiveInletGridFunction =>
        GetValue("Inlet", "InletGrid") ?? OlderFixedGridSize("InletGrid", "VarInletGrid") ?? "50";

    /// <summary>The exhaust grid size expression under whichever schema the file uses.</summary>
    /// <remarks>
    /// As <see cref="EffectiveInletGridFunction"/>, from <c>[Calculation] ExhaustGrid</c>
    /// and <c>VarExhGrid</c>. The default of 50 is over the 38-point exhaust limit, so
    /// without the fallback every such file failed its run (ISSUES.md A33).
    /// </remarks>
    public string EffectiveExhaustGridFunction =>
        GetValue("Exhaust", "ExhaustGrid") ?? OlderFixedGridSize("ExhaustGrid", "VarExhGrid") ?? "50";

    // VarInletGrid=1 made the predecessor load its function from Inlet.grd instead,
    // a file this port does not read (ISSUES.md section E), so it gives no fixed count.
    private string? OlderFixedGridSize(string key, string variableKey) =>
        UsesOlderManifoldSchema && GetValue("Calculation", variableKey)?.Trim() != "1"
            ? GetValue("Calculation", key)
            : null;
}
