using System.Globalization;
using App.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace App.Ui.ViewModels;

/// <summary>
/// The headline figures shown in the results panel, formatted as the original's
/// Single Simulation Results box shows them.
/// </summary>
/// <remarks>
/// Presentation only: every value here is a rendering of something already on
/// <see cref="Engine"/>. The decimal places are the original's, which differ per field -
/// mean effective pressures to three places, most other things to one.
/// </remarks>
public sealed partial class SimulationResultsViewModel : ObservableObject
{
    private static string Fixed(double value, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string _engineName = string.Empty;

    [ObservableProperty]
    private string _runTime = "00:00:00";

    [ObservableProperty]
    private string _cycles = "-";

    [ObservableProperty]
    private string _massBalance = "-";

    [ObservableProperty]
    private string _speed = "-";

    [ObservableProperty]
    private string _torque = "-";

    [ObservableProperty]
    private string _power = "-";

    [ObservableProperty]
    private string _volumetricEfficiency = "-";

    [ObservableProperty]
    private string _fuelConsumption = "-";

    [ObservableProperty]
    private string _specificFuelConsumption = "-";

    [ObservableProperty]
    private string _cylinderMass = "-";

    [ObservableProperty]
    private string _imep = "-";

    [ObservableProperty]
    private string _bmep = "-";

    [ObservableProperty]
    private string _fmep = "-";

    [ObservableProperty]
    private string _pmep = "-";

    [ObservableProperty]
    private string _work = "-";

    [ObservableProperty]
    private string _heatLoss = "-";

    [ObservableProperty]
    private string _pumping = "-";

    [ObservableProperty]
    private string _friction = "-";

    [ObservableProperty]
    private string _exhaust = "-";

    /// <summary>
    /// Always 100: the fuel energy is the denominator every other share is taken against,
    /// so the original prints it as a constant rather than computing it.
    /// </summary>
    public string Fuel => "100";

    /// <summary>Fills the panel from a completed run.</summary>
    /// <param name="requestedCycles">
    /// What was asked for, so the panel can show "4 / 4" as the original does - cycles
    /// completed over cycles requested.
    /// </param>
    public void Update(Engine engine, int cyclesRun, int requestedCycles, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(engine);

        EngineName = engine.Name;
        RunTime = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        Cycles = $"{cyclesRun} / {requestedCycles}";

        MassBalance = Fixed(
            Math.Abs(engine.TotalMassInInletValve - engine.TotalMassOutExhaustValve) * 1e6, 1);

        Speed = Fixed(engine.Rpm, 0);
        Torque = Fixed(engine.Torque, 1);
        Power = Fixed(engine.BrakePower / 1e3, 1);
        VolumetricEfficiency = Fixed(engine.VolumetricEfficiency, 1);

        FuelConsumption = Fixed(engine.FuelMassFlow, 1);
        SpecificFuelConsumption = Fixed(engine.Sfc, 1);
        CylinderMass = Fixed(engine.TotalMass * 1e6, 1);

        Imep = Fixed(engine.Imep / 1e5, 3);
        Bmep = Fixed(engine.Bmep / 1e5, 3);
        Fmep = Fixed(engine.Fmep / 1e5, 3);
        Pmep = Fixed(engine.Pmep / 1e5, 3);

        Work = Fixed(engine.QWork, 1);
        HeatLoss = Fixed(engine.QHeat, 1);
        Pumping = Fixed(engine.QPump, 1);
        Friction = Fixed(engine.QFriction, 1);
        Exhaust = Fixed(engine.QExhaust, 1);
    }
}
