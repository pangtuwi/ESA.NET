namespace App.Persistence;

/// <summary>
/// One physical line of an INI file, kept as the exact text that was read plus the
/// exact terminator that followed it. Re-emitting <see cref="Text"/> and
/// <see cref="Terminator"/> in order reproduces the source byte for byte.
/// </summary>
public sealed class IniLine
{
    internal IniLine(IniLineKind kind, string text, string terminator, string? section, string? key, string? value)
    {
        Kind = kind;
        Text = text;
        Terminator = terminator;
        Section = section;
        Key = key;
        Value = value;
    }

    public IniLineKind Kind { get; }

    /// <summary>The line's text, excluding its terminator.</summary>
    public string Text { get; private set; }

    /// <summary>The terminator that followed the line: <c>"\r\n"</c>, <c>"\n"</c>, or empty at end of file.</summary>
    public string Terminator { get; private set; }

    /// <summary>Section name for a <see cref="IniLineKind.Section"/> line, otherwise <see langword="null"/>.</summary>
    public string? Section { get; }

    /// <summary>Key name for a <see cref="IniLineKind.KeyValue"/> line, otherwise <see langword="null"/>.</summary>
    public string? Key { get; }

    /// <summary>Value text for a <see cref="IniLineKind.KeyValue"/> line, otherwise <see langword="null"/>.</summary>
    public string? Value { get; private set; }

    /// <summary>
    /// Replaces the value of a key line, rewriting only the span after the first
    /// <c>=</c> so that any leading whitespace or unusual spelling of the key is left
    /// exactly as it was found.
    /// </summary>
    internal void ReplaceValue(string value)
    {
        var separator = Text.IndexOf('=', StringComparison.Ordinal);
        Text = string.Concat(Text.AsSpan(0, separator + 1), value);
        Value = value;
    }

    /// <summary>Gives a terminator to a line that ended the file without one.</summary>
    internal void EnsureTerminator(string terminator)
    {
        if (Terminator.Length == 0)
        {
            Terminator = terminator;
        }
    }
}

public enum IniLineKind
{
    Blank,
    Comment,
    Section,
    KeyValue,
    /// <summary>A line that is none of the above; kept verbatim and otherwise ignored.</summary>
    Unrecognised,
}
