using App.Core;
using App.Core.Model;

namespace App.Persistence.Tables;

/// <summary>
/// Reads <c>.spk</c> spark maps. Port of <c>TVarSpeedList.Load</c> (VarSpeedList.pas).
/// </summary>
/// <remarks>
/// Unlike the wall temperature and exhaust readers, the Delphi original enforces no row
/// limit here, so neither does this.
/// </remarks>
public sealed class SpeedKeyedTableReader : ISpeedKeyedTableReader
{
    public SpeedKeyedTable Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var table = new SpeedKeyedTable { FileName = path };

        foreach (var row in DelimitedTable.Read(path, columns: 2, maximumRows: null))
        {
            table.Rpm.Add(row[0]);
            table.Values.Add(row[1]);
        }

        return table;
    }
}

/// <summary>
/// Reads <c>.cwt</c> wall temperature tables. Port of <c>TWallTemps.Load</c>
/// (WallTemps.pas). Columns are RPM, head, piston, upper liner, lower liner.
/// </summary>
public sealed class WallTemperatureTableReader : IWallTemperatureTableReader
{
    public WallTemperatureTable Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var table = new WallTemperatureTable { FileName = path };

        foreach (var row in DelimitedTable.Read(path, columns: 5, maximumRows: EsaLimits.MaxSpeedTableRows))
        {
            table.Rpm.Add(row[0]);
            table.HeadTemperature.Add(row[1]);
            table.PistonTemperature.Add(row[2]);
            table.UpperLinerTemperature.Add(row[3]);
            table.LowerLinerTemperature.Add(row[4]);
        }

        return table;
    }
}

/// <summary>
/// Reads <c>.exh</c> exhaust back pressure tables. Port of <c>TExhaustPandT.Load</c>
/// (ExhBackPandT.pas).
/// </summary>
/// <remarks>
/// The column order is RPM, <b>temperature</b>, <b>pressure</b> — temperature first.
/// SPEC.md section 3 states "RPM, exhaust pressure, and exhaust-temperature columns"
/// and is wrong; the Delphi loader reads <c>ATExh</c> before <c>APExh</c>, and the
/// heading row of the shipped files reads <c>SPEED / TEMP[C] / P[kPa]</c>.
/// </remarks>
public sealed class ExhaustBackPressureTableReader : IExhaustBackPressureTableReader
{
    public ExhaustBackPressureTable Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var table = new ExhaustBackPressureTable { FileName = path };

        foreach (var row in DelimitedTable.Read(path, columns: 3, maximumRows: EsaLimits.MaxSpeedTableRows))
        {
            table.Rpm.Add(row[0]);
            table.Temperature.Add(row[1]);
            table.Pressure.Add(row[2]);
        }

        return table;
    }
}
