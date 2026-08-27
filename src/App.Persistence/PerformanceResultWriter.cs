using System.Globalization;
using System.Text;
using App.Core.Model;

namespace App.Persistence;

/// <summary>
/// Appends one line of headline results per simulated point to the performance file,
/// <c>SimulDat.txt</c> by default. Port of <c>TFMain.WriteRunFile</c>
/// (Main.pas:1211-1238).
/// </summary>
/// <remarks>
/// <para>
/// <b>The file accumulates.</b> The original writes a heading only when the file does not
/// yet exist and appends otherwise, so results build up across runs and across sessions
/// until somebody deletes it. That is why the captured baseline holds two identical rows:
/// the same point was simulated twice. Reproduced, because a results log that silently
/// discarded earlier runs would be worse.
/// </para>
/// <para>
/// The newline is written <b>before</b> each row rather than after, so the file ends
/// without a terminator.
/// </para>
/// </remarks>
public sealed class PerformanceResultWriter
{
    private const string Separator = "  ";

    /// <summary>
    /// Appends <paramref name="engine"/>'s current results, creating the file with its
    /// heading if it is not already there.
    /// </summary>
    /// <param name="atmosphericPressure">
    /// Only used to note that the original does not use it: the reported back pressure
    /// subtracts a hard-coded 101.325 kPa to undo the conversion in
    /// <c>TExhaustPandT.Pres</c>, so an engine run at a different ambient pressure
    /// reports the wrong figure here. See ISSUES.md B69.
    /// </param>
    public void Append(string path, Engine engine, double exhaustBackPressure)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(engine);

        var text = new StringBuilder();

        if (!File.Exists(path))
        {
            text.Append(Heading());
        }

        // Before the row, not after it.
        text.Append("\r\n").Append(Row(engine, exhaustBackPressure));

        File.AppendAllText(path, text.ToString());
    }

    /// <summary>The heading line, with the original's irregular spacing preserved.</summary>
    public static string Heading() =>
        "Speed" + "  " + "IMEP" + "     " + "PMEP" + "    " + "FMEP" + "   " + "BMEP" + "   "
        + "MEff" + "  " + "VEff" + "   " + "ThEff" + "  " + "Torque" + "  " + "Power"
        + "    " + "mf" + "    " + "SFC" + "  " + "TMass" + "  " + "MassIn" + "  "
        + "MassOut" + "  " + "Lambda" + "  " + "Spark" + "  " + "BackP" + "  "
        + "QHeat" + "  " + "QWork" + "  " + "QExht" + "  " + "QPump" + "  " + "QFric";

    /// <summary>One results row.</summary>
    public static string Row(Engine engine, double exhaustBackPressure)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var fuel = engine.Cylinder.Fuel;

        return string.Join(Separator,
            Field(engine.Rpm, 4, 0),
            Field(engine.Imep / 1e5, 6, 3),
            Field(engine.Pmep / 1e5, 6, 3),
            Field(engine.Fmep / 1e5, 6, 3),
            Field(engine.Bmep / 1e5, 6, 3),
            Field(engine.MechanicalEfficiency, 4, 1),
            Field(engine.VolumetricEfficiency, 4, 1),
            Field(engine.ThermalEfficiency, 4, 1),
            Field(engine.Torque, 6, 2),
            Field(engine.BrakePower / 1e3, 6, 3),
            Field(engine.FuelMassFlow, 5, 2),
            Field(engine.Sfc, 5, 1),
            Field(engine.TotalMass * 1e6, 6, 2),
            Field(engine.TotalMassInInletValve * 1e6, 6, 2),
            Field(engine.TotalMassOutExhaustValve * 1e6, 6, 2),
            Field(fuel.Lambda, 5, 2),
            Field(engine.Cylinder.ThetaSpark * -1, 5, 1),

            // Back to gauge kilopascals, by subtracting the same hard-coded atmospheric
            // pressure the original does.
            Field((exhaustBackPressure / 1e3) - 101.325, 5, 1),

            Field(engine.QHeat, 5, 1),
            Field(engine.QWork, 5, 1),
            Field(engine.QExhaust, 5, 1),
            Field(engine.QPump, 5, 1),
            Field(engine.QFriction, 5, 1));
    }

    private static string Field(double value, int width, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture)
            .PadLeft(width);
}
