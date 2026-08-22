using App.Core;

namespace App.Persistence;

/// <summary>
/// An <see cref="EngineDefinition"/> backed by the <see cref="IniDocument"/> it was
/// read from, so that saving an unmodified definition reproduces the source file
/// byte for byte.
/// </summary>
public sealed class IniEngineDefinition : EngineDefinition
{
    public IniEngineDefinition(IniDocument document) => Document = document;

    /// <summary>
    /// The underlying document. Exposed because the older <c>[InManifold]</c> and
    /// <c>[ExManifold]</c> schema found in five Example1 engines has no typed surface
    /// yet, and phase 3 will need to read it.
    /// </summary>
    public IniDocument Document { get; }

    public override IReadOnlyList<string> Sections => Document.Sections;

    public override IReadOnlyList<string> KeysIn(string section) => Document.KeysIn(section);

    public override string? GetValue(string section, string key) => Document.GetValue(section, key);

    public override void SetValue(string section, string key, string value) =>
        Document.SetValue(section, key, value);
}
