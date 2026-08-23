using System.Globalization;

namespace App.Core.Expressions;

/// <summary>
/// Parses the expression dialect that ESA stores in <c>.eng</c> files, replacing the
/// proprietary <c>TAdCalc</c> evaluator.
/// </summary>
/// <remarks>
/// <para>
/// The grammar is deliberately only as large as the data requires. Every expression
/// across all 65 shipped <c>.eng</c> files uses nothing but numeric literals
/// (including scientific notation such as <c>1.0293E-19</c>), the variables
/// <c>N</c> and <c>L</c>, the operators <c>+ - * / ^</c>, parentheses and spaces.
/// AdCalc itself also offers thirty-odd functions, comparisons, logical operators
/// and string handling; none of that is reproduced. Anything outside the grammar is
/// rejected with <see cref="ExpressionException"/> rather than guessed at.
/// </para>
/// <para>
/// Two semantics are taken from ADCALC.PAS rather than from convention, because both
/// are silent if wrong:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>^</c> is <b>left</b>-associative. <c>GetLevel</c> (ADCALC.PAS line 2555) scores
/// <c>+ -</c> as 0, <c>* /</c> as 1 and <c>^</c> as 2, and the evaluator recurses only
/// while the next operator scores <i>strictly</i> higher than the current one. For
/// <c>^</c> following <c>^</c> that test is false, so the fold runs left to right and
/// <c>2^3^2</c> is 64, not 512. Most languages disagree.
/// </item>
/// <item>
/// A leading <c>-</c> is applied against a zero accumulator <i>before</i> the power
/// loop runs (ADCALC.PAS lines 2579-2592), so unary minus binds looser than <c>^</c>
/// and <c>-2^2</c> is -4.
/// </item>
/// </list>
/// <para>
/// A sign is only accepted where AdCalc accepts one: at the start of an expression or
/// immediately inside a bracket. <c>3*-2</c> is a parse error in the original and stays
/// one here; the original spelling is <c>3*(-2)</c>, which is what the shipped files use.
/// </para>
/// </remarks>
public static class ExpressionParser
{
    public static ExpressionNode Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var tokens = Tokenise(expression);
        var position = 0;
        var node = ParseSigned(expression, tokens, ref position);

        if (position != tokens.Count)
        {
            throw new ExpressionException(
                $"Unexpected '{tokens[position].Text}' at position {tokens[position].Start} in '{expression}'.");
        }

        return node;
    }

    /// <summary>
    /// An expression, optionally preceded by a sign. The sign is applied to the whole
    /// first term, which is what makes it bind looser than <c>^</c>.
    /// </summary>
    private static ExpressionNode ParseSigned(string source, List<Token> tokens, ref int position)
    {
        var negate = false;

        if (Peek(tokens, position) is { Kind: TokenKind.Operator } sign && sign.Text is "+" or "-")
        {
            negate = sign.Text == "-";
            position++;
        }

        // Parse everything that binds tighter than +/- before applying the sign.
        var node = ParseBinary(source, tokens, ref position, MultiplicativeLevel);

        if (negate)
        {
            node = node is ConstantNode constant
                ? new ConstantNode(-constant.Value)
                : new NegateNode(node);
        }

        while (Peek(tokens, position) is { Kind: TokenKind.Operator } token && token.Text is "+" or "-")
        {
            position++;
            var right = ParseBinary(source, tokens, ref position, MultiplicativeLevel);
            node = new BinaryNode(token.Text == "+" ? BinaryOperator.Add : BinaryOperator.Subtract, node, right);
        }

        return node;
    }

    private const int MultiplicativeLevel = 1;

    /// <summary>
    /// Precedence climbing. Recursing at <c>level + 1</c> makes every operator
    /// left-associative, matching AdCalc's strictly-greater-than test.
    /// </summary>
    private static ExpressionNode ParseBinary(string source, List<Token> tokens, ref int position, int minimumLevel)
    {
        var left = ParsePrimary(source, tokens, ref position);

        while (Peek(tokens, position) is { Kind: TokenKind.Operator } token
               && LevelOf(token.Text) is var level
               && level >= minimumLevel)
        {
            position++;
            var right = ParseBinary(source, tokens, ref position, level + 1);
            left = new BinaryNode(OperatorFor(token.Text), left, right);
        }

        return left;
    }

    private static ExpressionNode ParsePrimary(string source, List<Token> tokens, ref int position)
    {
        var token = Peek(tokens, position)
                    ?? throw new ExpressionException($"Unexpected end of expression in '{source}'.");

        switch (token.Kind)
        {
            case TokenKind.Number:
                position++;
                return new ConstantNode(token.Value);

            case TokenKind.Identifier:
                position++;
                return new VariableNode(VariableFor(token.Text, source));

            case TokenKind.OpenBracket:
                position++;
                var inner = ParseSigned(source, tokens, ref position);
                if (Peek(tokens, position) is not { Kind: TokenKind.CloseBracket })
                {
                    throw new ExpressionException($"Unclosed bracket in '{source}'.");
                }

                position++;
                return inner;

            default:
                throw new ExpressionException(
                    $"Expected a value but found '{token.Text}' at position {token.Start} in '{source}'.");
        }
    }

    private static ExpressionVariable VariableFor(string name, string source) => name.ToUpperInvariant() switch
    {
        "N" => ExpressionVariable.EngineSpeed,
        "L" => ExpressionVariable.Length,
        _ => throw new ExpressionException(
            $"Unknown identifier '{name}' in '{source}'. Only N (engine speed) and L (length) are defined."),
    };

    /// <summary>Precedence, matching <c>TParser.GetLevel</c> in ADCALC.PAS.</summary>
    private static int LevelOf(string op) => op switch
    {
        "+" or "-" => 0,
        "*" or "/" => 1,
        "^" => 2,
        _ => -1,
    };

    private static BinaryOperator OperatorFor(string op) => op switch
    {
        "+" => BinaryOperator.Add,
        "-" => BinaryOperator.Subtract,
        "*" => BinaryOperator.Multiply,
        "/" => BinaryOperator.Divide,
        "^" => BinaryOperator.Power,
        _ => throw new ExpressionException($"Unsupported operator '{op}'."),
    };

    private static Token? Peek(List<Token> tokens, int position) =>
        position < tokens.Count ? tokens[position] : null;

    private static List<Token> Tokenise(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < expression.Length)
        {
            var c = expression[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsAsciiDigit(expression[i + 1])))
            {
                var start = i;
                while (i < expression.Length && (char.IsAsciiDigit(expression[i]) || expression[i] == '.'))
                {
                    i++;
                }

                // Scientific notation: the exponent marker only counts as part of the
                // number when digits actually follow it, so a stray E stays an identifier.
                if (i < expression.Length && (expression[i] is 'E' or 'e'))
                {
                    var afterExponent = i + 1;
                    if (afterExponent < expression.Length && expression[afterExponent] is '+' or '-')
                    {
                        afterExponent++;
                    }

                    if (afterExponent < expression.Length && char.IsAsciiDigit(expression[afterExponent]))
                    {
                        i = afterExponent;
                        while (i < expression.Length && char.IsAsciiDigit(expression[i]))
                        {
                            i++;
                        }
                    }
                }

                var text = expression[start..i];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new ExpressionException($"'{text}' is not a valid number in '{expression}'.");
                }

                tokens.Add(new Token(TokenKind.Number, text, start, value));
                continue;
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = i;
                while (i < expression.Length && (char.IsAsciiLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, expression[start..i], start, 0));
                continue;
            }

            switch (c)
            {
                case '+' or '-' or '*' or '/' or '^':
                    tokens.Add(new Token(TokenKind.Operator, c.ToString(), i, 0));
                    break;
                case '(':
                    tokens.Add(new Token(TokenKind.OpenBracket, "(", i, 0));
                    break;
                case ')':
                    tokens.Add(new Token(TokenKind.CloseBracket, ")", i, 0));
                    break;
                default:
                    throw new ExpressionException(
                        $"Unexpected character '{c}' at position {i} in '{expression}'.");
            }

            i++;
        }

        return tokens;
    }

    private enum TokenKind
    {
        Number,
        Identifier,
        Operator,
        OpenBracket,
        CloseBracket,
    }

    private sealed record Token(TokenKind Kind, string Text, int Start, double Value);
}
