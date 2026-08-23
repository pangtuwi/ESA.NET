using System.Text;

namespace App.Persistence.Tables;

/// <summary>
/// A comma-delimited grid file held as the exact text that was read, so that writing
/// it back unchanged reproduces the original bytes and editing one cell touches only
/// that cell.
/// </summary>
/// <remarks>
/// <para>
/// This is the same bargain <see cref="IniDocument"/> makes for <c>.eng</c> files, and
/// for the same reason: <c>.maf</c> and <c>.vcd</c> files are user data that the app
/// writes back, so a parse-and-reserialize round trip would quietly restyle numbers
/// the user typed.
/// </para>
/// <para>
/// The Delphi parsers (<c>TFManfArea.LoadGrid</c>, <c>TFIpol.LoadGrid</c>) walk each
/// line right to left accumulating characters until a comma. That technique is not
/// reproduced — only its observable result, which is a plain split on commas with
/// spaces stripped and empty cells normalised to <c>-</c>.
/// </para>
/// </remarks>
internal sealed class DelimitedGridDocument
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly List<GridLine> _lines;

    private DelimitedGridDocument(List<GridLine> lines) => _lines = lines;

    /// <summary>Marks an unused cell in both formats.</summary>
    public const string Unused = "-";

    public int LineCount => _lines.Count;

    public static DelimitedGridDocument Parse(ReadOnlySpan<byte> bytes)
    {
        var text = Utf8NoBom.GetString(bytes);
        var lines = new List<GridLine>();
        var position = 0;

        while (position < text.Length)
        {
            var newline = text.IndexOf('\n', position);
            string content;
            string terminator;

            if (newline < 0)
            {
                content = text[position..];
                terminator = string.Empty;
                position = text.Length;
            }
            else
            {
                var hasCarriageReturn = newline > position && text[newline - 1] == '\r';
                content = text[position..(hasCarriageReturn ? newline - 1 : newline)];
                terminator = hasCarriageReturn ? "\r\n" : "\n";
                position = newline + 1;
            }

            lines.Add(new GridLine(content, terminator));
        }

        return new DelimitedGridDocument(lines);
    }

    /// <summary>The comma-separated fields of a line, trimmed, with empties as <c>-</c>.</summary>
    public IReadOnlyList<string> Fields(int lineIndex) => _lines[lineIndex].Fields;

    /// <summary>
    /// Replaces one field, rewriting only that field's span so the rest of the line —
    /// spacing, other cells, the terminator — survives byte for byte.
    /// </summary>
    public void SetField(int lineIndex, int fieldIndex, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (lineIndex < 0 || lineIndex >= _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        _lines[lineIndex].SetField(fieldIndex, value);
    }

    public byte[] ToBytes() => Utf8NoBom.GetBytes(ToText());

    public string ToText()
    {
        var builder = new StringBuilder();
        foreach (var line in _lines)
        {
            builder.Append(line.Text).Append(line.Terminator);
        }

        return builder.ToString();
    }

    private sealed class GridLine
    {
        private string[]? _fields;

        public GridLine(string text, string terminator)
        {
            Text = text;
            Terminator = terminator;
        }

        public string Text { get; private set; }

        public string Terminator { get; }

        public IReadOnlyList<string> Fields => _fields ??= Split(Text);

        public void SetField(int index, string value)
        {
            var raw = Text.Split(',');

            if (index < 0 || index >= raw.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            raw[index] = value;
            Text = string.Join(',', raw);
            _fields = null;
        }

        private static string[] Split(string text)
        {
            var raw = text.Split(',');
            var fields = new string[raw.Length];

            for (var i = 0; i < raw.Length; i++)
            {
                var trimmed = raw[i].Replace(" ", string.Empty, StringComparison.Ordinal);
                fields[i] = trimmed.Length == 0 ? Unused : trimmed;
            }

            return fields;
        }
    }
}
