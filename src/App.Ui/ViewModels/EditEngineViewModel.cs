using System.ComponentModel.DataAnnotations;
using System.Globalization;
using App.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>
/// The engine editor. Port of <c>TFEdit</c> (Edit.pas / Edit.dfm).
/// </summary>
/// <remarks>
/// <para>
/// Field for field this matches the Delphi form, including its unit captions and its
/// live capacity calculation. The layout is native Avalonia rather than the original's
/// absolute pixel positions.
/// </para>
/// <para>
/// Two departures from the original are deliberate. <c>BOKClick</c> caught
/// <c>EConvertError</c> and showed the user nothing (SPEC.md section 6), so a typo in
/// any numeric field silently discarded the whole edit; here every field validates and
/// Save stays disabled while anything is invalid. And values are written back only when
/// they actually change, so a file the user opened and closed keeps its exact bytes —
/// including the way its numbers were originally spelled.
/// </para>
/// </remarks>
public sealed partial class EditEngineViewModel : ObservableValidator
{
    private EngineDefinition? _definition;

    public string Title => "Edit Engine Data";

    // --- Cylinders -----------------------------------------------------------

    [ObservableProperty]
    private string _engineName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Capacity))]
    [NotifyDataErrorInfo]
    [Range(1, 16, ErrorMessage = "Between 1 and 16 cylinders.")]
    private int _cylinderCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Capacity))]
    [NotifyDataErrorInfo]
    [Range(1.0, 500.0, ErrorMessage = "Bore in mm.")]
    private double _bore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Capacity))]
    [NotifyDataErrorInfo]
    [Range(1.0, 500.0, ErrorMessage = "Stroke in mm.")]
    private double _stroke;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 30.0, ErrorMessage = "Compression ratio, X:1.")]
    private double _compressionRatio;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 1000.0, ErrorMessage = "Conrod length in mm.")]
    private double _conrodLength;

    /// <summary>
    /// Swept volume in cc, recalculated as the user types. Port of <c>ECCChanged</c>:
    /// <c>Cyl * Pi/4 * Bore^2 * Stroke / 1000</c>.
    /// </summary>
    public double Capacity => CylinderCount * Math.PI / 4.0 * Bore * Bore * Stroke / 1000.0;

    // --- Heat transfer and spark ---------------------------------------------

    [ObservableProperty]
    private string _wallTemperatureFile = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 1000.0, ErrorMessage = "Woshini coefficient, usually 131.")]
    private double _woshiniCoefficient;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 180.0, ErrorMessage = "Burn angle in degrees crank.")]
    private double _burnAngle;

    /// <summary>
    /// A <c>.spk</c> file name, or a bare number for a fixed angle at every speed.
    /// Nissan5.eng writes <c>SparkAngle=10.0</c>.
    /// </summary>
    [ObservableProperty]
    private string _sparkAngleFile = string.Empty;

    // --- Inlet ---------------------------------------------------------------

    [ObservableProperty]
    private string _inletAreaFile = string.Empty;

    [ObservableProperty]
    private string _plenumPressureFunction = string.Empty;

    [ObservableProperty]
    private string _inletGridFunction = string.Empty;

    [ObservableProperty]
    private string _inletValveReverseFunction = string.Empty;

    [ObservableProperty]
    private string _inletValveForwardFunction = string.Empty;

    [ObservableProperty]
    private string _inletValveForwardReverseFunction = string.Empty;

    // --- Exhaust -------------------------------------------------------------

    [ObservableProperty]
    private string _exhaustAreaFile = string.Empty;

    [ObservableProperty]
    private string _exhaustBackPressureFile = string.Empty;

    [ObservableProperty]
    private string _exhaustGridFunction = string.Empty;

    [ObservableProperty]
    private string _exhaustValveReverseFunction = string.Empty;

    [ObservableProperty]
    private string _exhaustValveForwardFunction = string.Empty;

    [ObservableProperty]
    private string _exhaustValveForwardReverseFunction = string.Empty;

    // --- Cams ----------------------------------------------------------------

    [ObservableProperty]
    private string _inletValveProfileFile = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-180.0, 180.0, ErrorMessage = "Degrees before top dead centre.")]
    private double _inletValveOpen;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-180.0, 180.0, ErrorMessage = "Degrees after bottom dead centre.")]
    private double _inletValveClose;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 50.0, ErrorMessage = "Total valve lift in mm.")]
    private double _inletValveLift;

    [ObservableProperty]
    private string _exhaustValveProfileFile = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-180.0, 180.0, ErrorMessage = "Degrees before bottom dead centre.")]
    private double _exhaustValveOpen;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-180.0, 180.0, ErrorMessage = "Degrees after top dead centre.")]
    private double _exhaustValveClose;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 50.0, ErrorMessage = "Total valve lift in mm.")]
    private double _exhaustValveLift;

    // --- Valves --------------------------------------------------------------

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 8, ErrorMessage = "Valves per cylinder.")]
    private int _inletValveCount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 200.0, ErrorMessage = "Valve diameter in mm.")]
    private double _inletValveDiameter;

    [ObservableProperty]
    private string _inletValveCdInwardFile = string.Empty;

    [ObservableProperty]
    private string _inletValveCdOutwardFile = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 8, ErrorMessage = "Valves per cylinder.")]
    private int _exhaustValveCount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 200.0, ErrorMessage = "Valve diameter in mm.")]
    private double _exhaustValveDiameter;

    [ObservableProperty]
    private string _exhaustValveCdInwardFile = string.Empty;

    [ObservableProperty]
    private string _exhaustValveCdOutwardFile = string.Empty;

    // --- Fuel and conditions -------------------------------------------------

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 100.0, ErrorMessage = "Stoichiometric air-fuel ratio, X:1.")]
    private double _airFuelRatio;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 200.0, ErrorMessage = "Fuel energy in MJ/kg.")]
    private double _fuelCalorificValue;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-50.0, 200.0, ErrorMessage = "Fuel temperature in °C.")]
    private double _fuelTemperature;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.1, 3.0, ErrorMessage = "Lambda, 1.0 is stoichiometric.")]
    private double _lambda;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Carbon atoms per molecule.")]
    private int _fuelCarbon;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Hydrogen atoms per molecule.")]
    private int _fuelHydrogen;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Nitrogen atoms per molecule.")]
    private int _fuelNitrogen;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Oxygen atoms per molecule.")]
    private int _fuelOxygen;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(-100.0, 100.0, ErrorMessage = "Atmospheric temperature in °C.")]
    private double _atmosphericTemperature;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 1000.0, ErrorMessage = "Atmospheric pressure in kPa.")]
    private double _atmosphericPressure;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 10000.0, ErrorMessage = "Oil viscosity.")]
    private double _oilViscosity;

    // --- Model ---------------------------------------------------------------

    [ObservableProperty]
    private string _performanceDataFile = string.Empty;

    [ObservableProperty]
    private bool _variableGamma;

    [ObservableProperty]
    private bool _saveManifoldData;

    /// <summary>Integrator 0 is RKF5, 1 is Euler.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRkf5))]
    [NotifyPropertyChangedFor(nameof(IsEuler))]
    private int _integratorIndex;

    /// <summary>Radio-button view of <see cref="IntegratorIndex"/>.</summary>
    public bool IsRkf5
    {
        get => IntegratorIndex == (int)Integrator.Rkf5;
        set
        {
            if (value)
            {
                IntegratorIndex = (int)Integrator.Rkf5;
            }
        }
    }

    public bool IsEuler
    {
        get => IntegratorIndex == (int)Integrator.Euler;
        set
        {
            if (value)
            {
                IntegratorIndex = (int)Integrator.Euler;
            }
        }
    }

    /// <summary>
    /// Values that only the older <c>[InManifold]</c> schema carries, shown read-only so
    /// the five Nissan engines stop being invisible without inviting edits to a schema
    /// the port does not write.
    /// </summary>
    [ObservableProperty]
    private string _olderSchemaNotes = string.Empty;

    public bool UsesOlderSchema => OlderSchemaNotes.Length > 0;

    /// <summary>Save is blocked while any field is invalid, unlike the original.</summary>
    public bool CanSave => !HasErrors && _definition is not null;

    /// <summary>Fills the form from a definition and remembers it for <see cref="Apply"/>.</summary>
    public void Load(EngineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _definition = definition;

        EngineName = definition.Name;
        CylinderCount = definition.CylinderCount;
        Bore = definition.Bore;
        Stroke = definition.Stroke;
        CompressionRatio = definition.CompressionRatio;
        ConrodLength = definition.ConrodLength;

        WallTemperatureFile = definition.WallTemperatureFile;
        WoshiniCoefficient = definition.WoshiniCoefficient;
        BurnAngle = definition.BurnAngle;
        SparkAngleFile = definition.SparkAngleFile;

        InletAreaFile = definition.EffectiveInletAreaFile;
        PlenumPressureFunction = definition.EffectivePlenumPressure;
        InletGridFunction = definition.InletGridFunction;
        InletValveReverseFunction = definition.InletValveReverseFunction;
        InletValveForwardFunction = definition.InletValveForwardFunction;
        InletValveForwardReverseFunction = definition.InletValveForwardReverseFunction;

        ExhaustAreaFile = definition.EffectiveExhaustAreaFile;
        ExhaustBackPressureFile = definition.ExhaustBackPressureFile;
        ExhaustGridFunction = definition.ExhaustGridFunction;
        ExhaustValveReverseFunction = definition.ExhaustValveReverseFunction;
        ExhaustValveForwardFunction = definition.ExhaustValveForwardFunction;
        ExhaustValveForwardReverseFunction = definition.ExhaustValveForwardReverseFunction;

        InletValveProfileFile = definition.InletValveProfileFile;
        InletValveOpen = definition.InletValveOpen;
        InletValveClose = definition.InletValveClose;
        InletValveLift = definition.InletValveLift;
        ExhaustValveProfileFile = definition.ExhaustValveProfileFile;
        ExhaustValveOpen = definition.ExhaustValveOpen;
        ExhaustValveClose = definition.ExhaustValveClose;
        ExhaustValveLift = definition.ExhaustValveLift;

        InletValveCount = definition.InletValveCount;
        InletValveDiameter = definition.InletValveDiameter;
        InletValveCdInwardFile = definition.InletValveCdInwardFile;
        InletValveCdOutwardFile = definition.InletValveCdOutwardFile;
        ExhaustValveCount = definition.ExhaustValveCount;
        ExhaustValveDiameter = definition.ExhaustValveDiameter;
        ExhaustValveCdInwardFile = definition.ExhaustValveCdInwardFile;
        ExhaustValveCdOutwardFile = definition.ExhaustValveCdOutwardFile;

        AirFuelRatio = definition.AirFuelRatio;
        FuelCalorificValue = definition.FuelCalorificValue;
        FuelTemperature = definition.FuelTemperature;
        Lambda = definition.Lambda;
        FuelCarbon = definition.FuelCarbon;
        FuelHydrogen = definition.FuelHydrogen;
        FuelNitrogen = definition.FuelNitrogen;
        FuelOxygen = definition.FuelOxygen;

        AtmosphericTemperature = definition.AtmosphericTemperature;
        AtmosphericPressure = definition.AtmosphericPressure;
        OilViscosity = definition.OilViscosity;

        PerformanceDataFile = definition.PerformanceDataFile;
        VariableGamma = definition.VariableGamma;
        SaveManifoldData = definition.SaveManifoldData;
        IntegratorIndex = (int)definition.Integrator;

        OlderSchemaNotes = DescribeOlderSchema(definition);

        ValidateAllProperties();
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(UsesOlderSchema));
    }

    /// <summary>
    /// Writes the edited values back into the definition.
    /// </summary>
    /// <remarks>
    /// Every write goes through a changed-check, so a field the user did not touch is
    /// never rewritten. That is what keeps a load-and-save cycle byte-identical: the
    /// file keeps <c>Bore=81.0</c> rather than having it reformatted to <c>81</c>.
    /// </remarks>
    public void Apply()
    {
        if (_definition is null)
        {
            throw new InvalidOperationException("Load must be called before Apply.");
        }

        var d = _definition;

        SetIfChanged(d.Name, EngineName, v => d.Name = v);
        SetIfChanged(d.CylinderCount, CylinderCount, v => d.CylinderCount = v);
        SetIfChanged(d.Bore, Bore, v => d.Bore = v);
        SetIfChanged(d.Stroke, Stroke, v => d.Stroke = v);
        SetIfChanged(d.CompressionRatio, CompressionRatio, v => d.CompressionRatio = v);
        SetIfChanged(d.ConrodLength, ConrodLength, v => d.ConrodLength = v);

        SetIfChanged(d.WallTemperatureFile, WallTemperatureFile, v => d.WallTemperatureFile = v);
        SetIfChanged(d.WoshiniCoefficient, WoshiniCoefficient, v => d.WoshiniCoefficient = v);
        SetIfChanged(d.BurnAngle, BurnAngle, v => d.BurnAngle = v);
        SetIfChanged(d.SparkAngleFile, SparkAngleFile, v => d.SparkAngleFile = v);

        SetIfChanged(d.InletGridFunction, InletGridFunction, v => d.InletGridFunction = v);
        SetIfChanged(d.InletValveReverseFunction, InletValveReverseFunction,
            v => d.InletValveReverseFunction = v);
        SetIfChanged(d.InletValveForwardFunction, InletValveForwardFunction,
            v => d.InletValveForwardFunction = v);
        SetIfChanged(d.InletValveForwardReverseFunction, InletValveForwardReverseFunction,
            v => d.InletValveForwardReverseFunction = v);

        SetIfChanged(d.ExhaustBackPressureFile, ExhaustBackPressureFile, v => d.ExhaustBackPressureFile = v);
        SetIfChanged(d.ExhaustGridFunction, ExhaustGridFunction, v => d.ExhaustGridFunction = v);
        SetIfChanged(d.ExhaustValveReverseFunction, ExhaustValveReverseFunction,
            v => d.ExhaustValveReverseFunction = v);
        SetIfChanged(d.ExhaustValveForwardFunction, ExhaustValveForwardFunction,
            v => d.ExhaustValveForwardFunction = v);
        SetIfChanged(d.ExhaustValveForwardReverseFunction, ExhaustValveForwardReverseFunction,
            v => d.ExhaustValveForwardReverseFunction = v);

        // The area and plenum entries are read through the older-schema fallbacks, so
        // they are only written back when the file uses the documented sections. A file
        // on the older schema is left exactly as found.
        if (!d.UsesOlderManifoldSchema)
        {
            SetIfChanged(d.InletAreaFile, InletAreaFile, v => d.InletAreaFile = v);
            SetIfChanged(d.ExhaustAreaFile, ExhaustAreaFile, v => d.ExhaustAreaFile = v);
            SetIfChanged(d.PlenumPressureFunction, PlenumPressureFunction, v => d.PlenumPressureFunction = v);
        }

        SetIfChanged(d.InletValveProfileFile, InletValveProfileFile, v => d.InletValveProfileFile = v);
        SetIfChanged(d.InletValveOpen, InletValveOpen, v => d.InletValveOpen = v);
        SetIfChanged(d.InletValveClose, InletValveClose, v => d.InletValveClose = v);
        SetIfChanged(d.InletValveLift, InletValveLift, v => d.InletValveLift = v);
        SetIfChanged(d.ExhaustValveProfileFile, ExhaustValveProfileFile, v => d.ExhaustValveProfileFile = v);
        SetIfChanged(d.ExhaustValveOpen, ExhaustValveOpen, v => d.ExhaustValveOpen = v);
        SetIfChanged(d.ExhaustValveClose, ExhaustValveClose, v => d.ExhaustValveClose = v);
        SetIfChanged(d.ExhaustValveLift, ExhaustValveLift, v => d.ExhaustValveLift = v);

        SetIfChanged(d.InletValveCount, InletValveCount, v => d.InletValveCount = v);
        SetIfChanged(d.InletValveDiameter, InletValveDiameter, v => d.InletValveDiameter = v);
        SetIfChanged(d.InletValveCdInwardFile, InletValveCdInwardFile, v => d.InletValveCdInwardFile = v);
        SetIfChanged(d.InletValveCdOutwardFile, InletValveCdOutwardFile, v => d.InletValveCdOutwardFile = v);
        SetIfChanged(d.ExhaustValveCount, ExhaustValveCount, v => d.ExhaustValveCount = v);
        SetIfChanged(d.ExhaustValveDiameter, ExhaustValveDiameter, v => d.ExhaustValveDiameter = v);
        SetIfChanged(d.ExhaustValveCdInwardFile, ExhaustValveCdInwardFile, v => d.ExhaustValveCdInwardFile = v);
        SetIfChanged(d.ExhaustValveCdOutwardFile, ExhaustValveCdOutwardFile, v => d.ExhaustValveCdOutwardFile = v);

        SetIfChanged(d.AirFuelRatio, AirFuelRatio, v => d.AirFuelRatio = v);
        SetIfChanged(d.FuelCalorificValue, FuelCalorificValue, v => d.FuelCalorificValue = v);
        SetIfChanged(d.FuelTemperature, FuelTemperature, v => d.FuelTemperature = v);
        SetIfChanged(d.Lambda, Lambda, v => d.Lambda = v);

        // Composition is written only when it differs from what the file says, which for
        // a file with no composition keys means only when it differs from the Delphi
        // form defaults. Opening and saving an untouched engine adds nothing.
        SetIfChanged(d.FuelCarbon, FuelCarbon, v => d.FuelCarbon = v);
        SetIfChanged(d.FuelHydrogen, FuelHydrogen, v => d.FuelHydrogen = v);
        SetIfChanged(d.FuelNitrogen, FuelNitrogen, v => d.FuelNitrogen = v);
        SetIfChanged(d.FuelOxygen, FuelOxygen, v => d.FuelOxygen = v);

        SetIfChanged(d.AtmosphericTemperature, AtmosphericTemperature, v => d.AtmosphericTemperature = v);
        SetIfChanged(d.AtmosphericPressure, AtmosphericPressure, v => d.AtmosphericPressure = v);
        SetIfChanged(d.OilViscosity, OilViscosity, v => d.OilViscosity = v);

        SetIfChanged(d.PerformanceDataFile, PerformanceDataFile, v => d.PerformanceDataFile = v);
        SetIfChanged(d.VariableGamma, VariableGamma, v => d.VariableGamma = v);
        SetIfChanged(d.SaveManifoldData, SaveManifoldData, v => d.SaveManifoldData = v);
        SetIfChanged((int)d.Integrator, IntegratorIndex, v => d.Integrator = (Integrator)v);
    }

    private static void SetIfChanged<T>(T current, T updated, Action<T> write)
    {
        if (!EqualityComparer<T>.Default.Equals(current, updated))
        {
            write(updated);
        }
    }

    private static string DescribeOlderSchema(EngineDefinition definition)
    {
        if (!definition.UsesOlderManifoldSchema)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        if (definition.HasInlineWallTemperatures)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"wall temperatures head {definition.InlineHeadTemperature}, piston {definition.InlinePistonTemperature}, "
                + $"upper liner {definition.InlineUpperLinerTemperature}, lower liner {definition.InlineLowerLinerTemperature}"));
        }

        if (definition.HasInlineExhaustBackPressure)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"exhaust back pressure {definition.InlineExhaustBackPressure}, temperature {definition.InlineExhaustTemperature}"));
        }

        return "This engine uses the older [InManifold] / [ExManifold] schema: "
               + string.Join("; ", parts)
               + ". These values are shown read-only and are written back unchanged.";
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is not (nameof(CanSave) or nameof(UsesOlderSchema)))
        {
            OnPropertyChanged(nameof(CanSave));
        }
    }

    [RelayCommand]
    private void Ok()
    {
        Apply();
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised after OK has written the form back to the definition, so the shell can
    /// re-derive the engine the simulation reads. Without it the operator's edits reach
    /// the definition and stop there - ISSUES.md C2.
    /// </summary>
    public event EventHandler? Applied;
}
