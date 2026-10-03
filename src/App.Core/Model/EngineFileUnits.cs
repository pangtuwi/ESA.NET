namespace App.Core.Model;

/// <summary>
/// The conversions from the units an operator types and a <c>.eng</c> file stores into
/// the SI units <see cref="Engine"/> holds. Port of the arithmetic in Delphi
/// <c>TFEdit.ReadfromEdits</c> (<c>Edit.pas:417-419, 448-454, 465-466</c>).
/// </summary>
/// <remarks>
/// <para>
/// Delphi converted on the way out of the edit form, so its engine object was SI
/// throughout and the simulation could assume it. The port first stored what the file
/// said and converted at each point of use instead - four separate places for the valve
/// timings alone - which is how a multi-run override came to be divided by a thousand
/// twice (ISSUES.md A6, A17). Everything that puts a typed or stored value onto an
/// <see cref="Engine"/> goes through here: <c>EngineLoader</c> for the <c>.eng</c> and
/// <c>MultiRunner</c> for the grid's override columns.
/// </para>
/// <para>
/// <b>Valve timings</b> are typed as degrees before or after a dead centre, the way a cam
/// card reads, and become crank angles in degrees on the solver's scale, where firing top
/// dead centre is 0 and a cycle runs from -360 to 360. On the baseline engine IVO 19,
/// IVC 80, EVO 64 and EVC 37 become 341, -100, 116 and -323.
/// </para>
/// </remarks>
public static class EngineFileUnits
{
    /// <summary>
    /// Millimetres to metres: bore, stroke, conrod length, valve lift and valve diameter
    /// (<c>Edit.pas:417-419, 453-454, 465-466</c>).
    /// </summary>
    public static double Length(double millimetres) => millimetres / 1000;

    /// <summary>
    /// Inlet valve opening, degrees before top dead centre, to Delphi <c>IV.O</c>:
    /// <c>360 - IVO</c> (<c>Edit.pas:448</c>).
    /// </summary>
    public static double InletOpen(double degreesBeforeTopDeadCentre) =>
        360 - degreesBeforeTopDeadCentre;

    /// <summary>
    /// Inlet valve closing, degrees after bottom dead centre, to Delphi <c>IV.C</c>:
    /// <c>-180 + IVC</c> (<c>Edit.pas:449</c>).
    /// </summary>
    public static double InletClose(double degreesAfterBottomDeadCentre) =>
        -180 + degreesAfterBottomDeadCentre;

    /// <summary>
    /// Exhaust valve opening, degrees before bottom dead centre, to Delphi <c>EV.O</c>:
    /// <c>180 - EVO</c> (<c>Edit.pas:450</c>).
    /// </summary>
    public static double ExhaustOpen(double degreesBeforeBottomDeadCentre) =>
        180 - degreesBeforeBottomDeadCentre;

    /// <summary>
    /// Exhaust valve closing, degrees after top dead centre, to Delphi <c>EV.C</c>:
    /// <c>-360 + EVC</c> (<c>Edit.pas:451</c>).
    /// </summary>
    public static double ExhaustClose(double degreesAfterTopDeadCentre) =>
        -360 + degreesAfterTopDeadCentre;

    // The way back, for showing an engine's values in the units the editor and the
    // multi-run grid use. Each undoes the conversion above it exactly.

    /// <summary>Metres back to millimetres.</summary>
    public static double ToMillimetres(double metres) => metres * 1000;

    /// <summary>Delphi <c>IV.O</c> back to degrees before top dead centre.</summary>
    public static double ToInletOpen(double openAngle) => 360 - openAngle;

    /// <summary>Delphi <c>IV.C</c> back to degrees after bottom dead centre.</summary>
    public static double ToInletClose(double closeAngle) => closeAngle + 180;

    /// <summary>Delphi <c>EV.O</c> back to degrees before bottom dead centre.</summary>
    public static double ToExhaustOpen(double openAngle) => 180 - openAngle;

    /// <summary>Delphi <c>EV.C</c> back to degrees after top dead centre.</summary>
    public static double ToExhaustClose(double closeAngle) => closeAngle + 360;
}
