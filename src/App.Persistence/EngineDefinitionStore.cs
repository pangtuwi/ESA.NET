using App.Core;

namespace App.Persistence;

/// <summary>
/// Reads and writes <c>.eng</c> engine definitions.
/// </summary>
/// <remarks>
/// SPEC.md section 3 permits the .NET port to standardise on UTF-8 rather than
/// preserving Delphi's ANSI encoding. Every shipped legacy file is plain ASCII, so
/// decoding as UTF-8 and writing back without a BOM is lossless for them, and a
/// BOM that is present on input is preserved on output.
/// </remarks>
public sealed class EngineDefinitionStore : IEngineDefinitionStore
{
    public EngineDefinition Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new IniEngineDefinition(IniDocument.Parse(File.ReadAllBytes(path)));
    }

    public void Write(string path, EngineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition is not IniEngineDefinition ini)
        {
            throw new ArgumentException(
                $"Definition must have been produced by {nameof(EngineDefinitionStore)}.",
                nameof(definition));
        }

        File.WriteAllBytes(path, ini.Document.ToBytes());
    }
}
