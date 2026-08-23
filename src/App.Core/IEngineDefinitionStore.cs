namespace App.Core;

/// <summary>
/// Loads and saves <c>.eng</c> engine definitions. Implemented in App.Persistence.
/// </summary>
public interface IEngineDefinitionStore
{
    /// <summary>Loads a definition, preserving enough of the file to save it back unchanged.</summary>
    EngineDefinition Read(string path);

    /// <summary>
    /// Saves a definition. Writing a definition that was read and not modified must
    /// reproduce the source file byte for byte.
    /// </summary>
    void Write(string path, EngineDefinition definition);
}
