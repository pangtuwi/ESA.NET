using System.Globalization;
using System.Text;
using App.Core.Manifold;

namespace App.Persistence;

/// <summary>
/// Writes the nine manifold output files the original produces when <c>Save Manifold
/// Data</c> is set. Port of the write blocks in <c>Main_Prog</c>
/// (Manifolds.pas:3022-3118).
/// </summary>
/// <remarks>
/// <para>
/// Rows are collected in memory and written on <see cref="Write"/>, rather than the
/// original's open-append-close of all nine files on every single crank-angle step.
/// </para>
/// <para>
/// The <b>content</b> is reproduced exactly - the same columns, units, field widths and
/// CRLF line endings, so the files can be diffed against the originals. The <b>gate</b> is
/// not: ISSUES.md C1 to C4 describe a write condition that also needs the edit dialog to
/// have been opened and that a converged run never satisfies. Which rows to record is the
/// caller's decision here - <see cref="App.Core.Manifold.ManifoldCaptureWindow"/> is the
/// filter <see cref="App.Core.Simulation.SimulationRunner"/> wraps this in, and
/// <see cref="Reset"/> is how it keeps the last cycle rather than all of them.
/// </para>
/// </remarks>
public sealed class ManifoldTraceWriter : IManifoldRecorder
{
    private readonly List<Row> _rows = [];

    private sealed record Row(
        double CrankAngle,
        double CylinderPressure,
        double CylinderTemperature,
        double CylinderVolume,
        double MassIn,
        double MassOut,
        double[] InletPressure,
        double[] InletVelocity,
        double[] ExhaustPressure,
        double[] ExhaustVelocity);

    /// <summary>How many rows have been recorded.</summary>
    public int RowCount => _rows.Count;

    /// <inheritdoc />
    public void Record(in ManifoldRow row) =>
        _rows.Add(new Row(
            row.CrankAngle,
            row.CylinderPressure,
            row.CylinderTemperature,
            row.CylinderVolume,
            row.MassIn,
            row.MassOut,
            row.InletPressure.ToArray(),
            row.InletVelocity.ToArray(),
            row.ExhaustPressure.ToArray(),
            row.ExhaustVelocity.ToArray()));

    /// <inheritdoc />
    public void Reset() => _rows.Clear();

    /// <summary>Writes all nine files into <paramref name="directory"/>.</summary>
    public void Write(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        Directory.CreateDirectory(directory);

        WriteFile(directory, "Inlet.txt", InletRow);
        WriteFile(directory, "Exhaust.txt", ExhaustRow);
        WriteFile(directory, "Pcyl.txt", r =>
            Field(r.CrankAngle, 5, 0) + "    " + Field(r.CylinderPressure / 1e5, 6, 4));
        WriteFile(directory, "Tcyl.txt", r =>
            Field(r.CrankAngle, 5, 0) + "    " + Field(r.CylinderTemperature, 6, 2)
            + "    " + Field(r.CylinderVolume, 12, 11));
        WriteFile(directory, "MassFlow.txt", r =>
            Field(r.CrankAngle, 5, 0) + "    " + Field(r.MassIn * 1e6, 6, 4)
            + "    " + Field(r.MassOut * 1e6, 6, 4));

        WriteFile(directory, "InlPress.m", r => Field(r.InletPressure, 1e5));
        WriteFile(directory, "InlVel.m", r => Field(r.InletVelocity, 1));
        WriteFile(directory, "ExhPress.m", r => Field(r.ExhaustPressure, 1e5));
        WriteFile(directory, "ExhVel.m", r => Field(r.ExhaustVelocity, 1));
    }

    /// <summary>
    /// Inlet pressure and velocity at the plenum end, the midpoint and the valve.
    /// </summary>
    private static string InletRow(Row row)
    {
        var last = row.InletPressure.Length - 1;

        // Delphi's QI div 2 on a 1-based array, so one less as an index here.
        var middle = ((last + 1) / 2) - 1;

        return Field(row.CrankAngle, 5, 0)
               + "    " + Field(row.InletPressure[0] / 1e5, 6, 4)
               + "   " + Field(row.InletVelocity[0], 6, 2)
               + "    " + Field(row.InletPressure[middle] / 1e5, 6, 4)
               + "   " + Field(row.InletVelocity[middle], 6, 2)
               + "    " + Field(row.InletPressure[last] / 1e5, 6, 4)
               + "   " + Field(row.InletVelocity[last], 6, 2);
    }

    /// <summary>
    /// Exhaust pressure and velocity, reported from the tailpipe end inwards to the valve
    /// - the opposite order to the inlet file, and with one more character of width for
    /// the velocities.
    /// </summary>
    private static string ExhaustRow(Row row)
    {
        var last = row.ExhaustPressure.Length - 1;
        var middle = ((last + 1) / 2) - 1;

        return Field(row.CrankAngle, 5, 0)
               + "    " + Field(row.ExhaustPressure[last] / 1e5, 6, 4)
               + "   " + Field(row.ExhaustVelocity[last], 7, 2)
               + "    " + Field(row.ExhaustPressure[middle] / 1e5, 6, 4)
               + "   " + Field(row.ExhaustVelocity[middle], 7, 2)
               + "    " + Field(row.ExhaustPressure[0] / 1e5, 6, 4)
               + "   " + Field(row.ExhaustVelocity[0], 7, 2);
    }

    /// <summary>
    /// One field per grid point, each followed by a space - including the last, which
    /// leaves a trailing space on every line as the original does.
    /// </summary>
    private static string Field(double[] values, double scale)
    {
        var text = new StringBuilder();

        foreach (var value in values)
        {
            text.Append(Field(value / scale, 6, 4)).Append(' ');
        }

        return text.ToString();
    }

    /// <summary>
    /// Delphi's <c>write(x:width:decimals)</c>: fixed decimals, right-aligned in at least
    /// <paramref name="width"/> characters, and overflowing that width rather than
    /// truncating.
    /// </summary>
    private static string Field(double value, int width, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture)
            .PadLeft(width);

    private void WriteFile(string directory, string name, Func<Row, string> format)
    {
        var text = new StringBuilder();

        foreach (var row in _rows)
        {
            // The original is a Windows program writing text files, so CRLF regardless of
            // the platform this runs on.
            text.Append(format(row)).Append("\r\n");
        }

        File.WriteAllText(Path.Combine(directory, name), text.ToString());
    }
}
