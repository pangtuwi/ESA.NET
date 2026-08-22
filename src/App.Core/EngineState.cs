namespace App.Core;

/// <summary>
/// The six crank-angle states. Values match the Delphi constants in ICEngine2z.pas
/// so that persisted or logged integers keep their meaning.
/// </summary>
public enum EngineState
{
    Compression = 1,
    Combustion = 2,
    Expansion = 3,
    Exhaust = 4,
    Overlap = 5,
    Intake = 6,
}
