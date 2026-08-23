using App.Core.Model;

namespace App.Core;

/// <summary>
/// Reads a <c>.cam</c> valve lift profile. Port of <c>TProfile.LoadText</c>.
/// </summary>
public interface ICamProfileReader
{
    CamProfile Read(string path);
}

/// <summary>
/// Reads a <c>.spk</c> spark map. Port of <c>TVarSpeedList.Load</c>.
/// </summary>
public interface ISpeedKeyedTableReader
{
    SpeedKeyedTable Read(string path);
}

/// <summary>
/// Reads a <c>.cwt</c> wall temperature table. Port of <c>TWallTemps.Load</c>.
/// </summary>
public interface IWallTemperatureTableReader
{
    WallTemperatureTable Read(string path);
}

/// <summary>
/// Reads an <c>.exh</c> exhaust back pressure table. Port of <c>TExhaustPandT.Load</c>.
/// </summary>
public interface IExhaustBackPressureTableReader
{
    ExhaustBackPressureTable Read(string path);
}

/// <summary>
/// Reads and writes a <c>.maf</c> manifold area table.
/// </summary>
/// <remarks>
/// The app edits these through the area table editor, so writing must not reformat
/// the parts of the file the user did not touch. <see cref="Read"/> returns a handle
/// that carries enough of the original to guarantee that.
/// </remarks>
public interface IManifoldAreaTableStore
{
    ManifoldAreaDocument Read(string path);

    void Write(string path, ManifoldAreaDocument document);
}

/// <summary>
/// Reads and writes a <c>.vcd</c> discharge coefficient grid, with the same
/// write-fidelity requirement as <see cref="IManifoldAreaTableStore"/>.
/// </summary>
public interface IDischargeCoefficientTableStore
{
    DischargeCoefficientDocument Read(string path);

    void Write(string path, DischargeCoefficientDocument document);
}

/// <summary>
/// A <c>.maf</c> file: the parsed table plus whatever is needed to write it back
/// unchanged.
/// </summary>
public abstract class ManifoldAreaDocument
{
    /// <summary>The interpolatable table.</summary>
    public abstract ManifoldAreaTable Table { get; }

    /// <summary>Replaces one row's position and area, leaving every other byte alone.</summary>
    public abstract void SetRow(int rowIndex, string position, string area);
}

/// <summary>
/// A <c>.vcd</c> file: the parsed grid plus whatever is needed to write it back
/// unchanged.
/// </summary>
public abstract class DischargeCoefficientDocument
{
    /// <summary>The interpolatable grid.</summary>
    public abstract DischargeCoefficientTable Table { get; }

    /// <summary>Replaces one cell, leaving every other byte alone.</summary>
    public abstract void SetCell(int row, int column, string value);
}
