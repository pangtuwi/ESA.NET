using System.Text;

namespace App.Persistence;

/// <summary>
/// A format-preserving INI document.
/// </summary>
/// <remarks>
/// <para>
/// The point of this type is that <c>Parse</c> followed by <c>ToBytes</c> returns
/// the original bytes exactly. A conventional parse-to-dictionary-then-reserialize
/// model cannot do that: it loses key order, section order, blank lines, comments,
/// unknown keys, and the way numbers were originally written (<c>81.0</c> would come
/// back as <c>81</c>). ESA's <c>.eng</c> files are user-authored and hand-edited, so
/// all of that has to survive a load/save cycle.
/// </para>
/// <para>
/// Values are therefore held as text and every line keeps its own terminator, which
/// also means a file with mixed or missing terminators round-trips unchanged.
/// </para>
/// </remarks>
public sealed class IniDocument
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly List<IniLine> _lines;

    private IniDocument(List<IniLine> lines, string byteOrderMark, string defaultTerminator)
    {
        _lines = lines;
        ByteOrderMark = byteOrderMark;
        DefaultTerminator = defaultTerminator;
    }

    /// <summary>The BOM found at the start of the source, or empty. Re-emitted as it was found.</summary>
    public string ByteOrderMark { get; }

    /// <summary>
    /// The terminator used when appending new lines: whatever the file used first,
    /// defaulting to <c>"\n"</c> as every shipped legacy file is LF-terminated.
    /// </summary>
    public string DefaultTerminator { get; }

    public IReadOnlyList<IniLine> Lines => _lines;

    /// <summary>Section names in file order, without duplicates.</summary>
    public IReadOnlyList<string> Sections =>
        _lines.Where(l => l.Kind == IniLineKind.Section)
              .Select(l => l.Section!)
              .Distinct(StringComparer.OrdinalIgnoreCase)
              .ToList();

    public static IniDocument Parse(ReadOnlySpan<byte> bytes)
    {
        var bom = string.Empty;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bom = "﻿";
            bytes = bytes[3..];
        }

        return Parse(Utf8NoBom.GetString(bytes), bom);
    }

    public static IniDocument Parse(string text) => Parse(text, string.Empty);

    private static IniDocument Parse(string text, string byteOrderMark)
    {
        var lines = new List<IniLine>();
        var defaultTerminator = string.Empty;
        var position = 0;
        string? currentSection = null;

        while (position < text.Length)
        {
            var lineEnd = text.IndexOf('\n', position);
            string content;
            string terminator;

            if (lineEnd < 0)
            {
                // Final line with no terminator; keep it that way.
                content = text[position..];
                terminator = string.Empty;
                position = text.Length;
            }
            else
            {
                var hasCarriageReturn = lineEnd > position && text[lineEnd - 1] == '\r';
                content = text[position..(hasCarriageReturn ? lineEnd - 1 : lineEnd)];
                terminator = hasCarriageReturn ? "\r\n" : "\n";
                position = lineEnd + 1;
            }

            if (defaultTerminator.Length == 0 && terminator.Length > 0)
            {
                defaultTerminator = terminator;
            }

            lines.Add(Classify(content, terminator, ref currentSection));
        }

        return new IniDocument(lines, byteOrderMark, defaultTerminator.Length > 0 ? defaultTerminator : "\n");
    }

    private static IniLine Classify(string content, string terminator, ref string? currentSection)
    {
        var trimmed = content.Trim();

        if (trimmed.Length == 0)
        {
            return new IniLine(IniLineKind.Blank, content, terminator, null, null, null);
        }

        // Delphi's TIniFile treats both ';' and '#' prefixed lines as comments.
        if (trimmed[0] is ';' or '#')
        {
            return new IniLine(IniLineKind.Comment, content, terminator, null, null, null);
        }

        if (trimmed[0] == '[' && trimmed[^1] == ']')
        {
            currentSection = trimmed[1..^1].Trim();
            return new IniLine(IniLineKind.Section, content, terminator, currentSection, null, null);
        }

        var separator = content.IndexOf('=', StringComparison.Ordinal);
        if (separator < 0)
        {
            return new IniLine(IniLineKind.Unrecognised, content, terminator, null, null, null);
        }

        // Keys are trimmed for lookup, but the raw text keeps whatever spacing was
        // there. Values are taken verbatim: an .eng expression such as
        // "((1.05E-16*N^5 - ...)*L/1)" must survive untouched.
        var key = content[..separator].Trim();
        var value = content[(separator + 1)..];
        return new IniLine(IniLineKind.KeyValue, content, terminator, currentSection, key, value);
    }

    /// <summary>
    /// Reads a value. Section and key matching are case-insensitive, matching Delphi
    /// <c>TIniFile</c>: Edit.pas asks for <c>CdIvIn</c> while the shipped files all
    /// write <c>CdIVIn</c>.
    /// </summary>
    public string? GetValue(string section, string key)
    {
        var line = FindKeyLine(section, key);
        return line?.Value;
    }

    /// <summary>Key names within a section, in file order.</summary>
    public IReadOnlyList<string> KeysIn(string section) =>
        _lines.Where(l => l.Kind == IniLineKind.KeyValue
                          && string.Equals(l.Section, section, StringComparison.OrdinalIgnoreCase))
              .Select(l => l.Key!)
              .ToList();

    /// <summary>
    /// Writes a value, replacing an existing entry in place. Only the value span of
    /// that one line changes; every other byte in the document is left alone. When the
    /// key is absent it is appended to the end of its section, and when the section is
    /// absent both are appended to the end of the document.
    /// </summary>
    public void SetValue(string section, string key, string value)
    {
        var existing = FindKeyLine(section, key);
        if (existing is not null)
        {
            existing.ReplaceValue(value);
            return;
        }

        var newLine = new IniLine(IniLineKind.KeyValue, $"{key}={value}", DefaultTerminator, section, key, value);
        var insertAt = FindSectionEnd(section);

        if (insertAt < 0)
        {
            EnsureLastLineTerminated();
            _lines.Add(new IniLine(IniLineKind.Section, $"[{section}]", DefaultTerminator, section, null, null));
            _lines.Add(newLine);
            return;
        }

        if (insertAt == _lines.Count)
        {
            EnsureLastLineTerminated();
            _lines.Add(newLine);
            return;
        }

        _lines.Insert(insertAt, newLine);
    }

    public byte[] ToBytes() => Utf8NoBom.GetBytes(ToText());

    public string ToText()
    {
        var builder = new StringBuilder(ByteOrderMark);
        foreach (var line in _lines)
        {
            builder.Append(line.Text).Append(line.Terminator);
        }

        return builder.ToString();
    }

    private IniLine? FindKeyLine(string section, string key) =>
        _lines.FirstOrDefault(l => l.Kind == IniLineKind.KeyValue
                                   && string.Equals(l.Section, section, StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The index just past the last key line of a section, or -1 when the section is
    /// absent. Trailing blank and comment lines stay below the inserted key.
    /// </summary>
    private int FindSectionEnd(string section)
    {
        var lastKey = -1;
        var sectionSeen = false;

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (line.Kind == IniLineKind.Section)
            {
                sectionSeen |= string.Equals(line.Section, section, StringComparison.OrdinalIgnoreCase);
                if (sectionSeen && lastKey < 0 && string.Equals(line.Section, section, StringComparison.OrdinalIgnoreCase))
                {
                    lastKey = i;
                }
            }
            else if (line.Kind == IniLineKind.KeyValue
                     && string.Equals(line.Section, section, StringComparison.OrdinalIgnoreCase))
            {
                lastKey = i;
            }
        }

        return sectionSeen ? lastKey + 1 : -1;
    }

    /// <summary>
    /// Gives the final line a terminator before anything is appended after it, so a
    /// file that ended without a newline does not run its last value into a new key.
    /// </summary>
    private void EnsureLastLineTerminated()
    {
        if (_lines.Count > 0)
        {
            _lines[^1].EnsureTerminator(DefaultTerminator);
        }
    }
}
