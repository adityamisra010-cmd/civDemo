using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Systems.ClassMobility;

/// <summary>Raised on any predicate-DSL violation, with an actionable message
/// naming the offending token and listing the known variables (T0.4 template).</summary>
public sealed class PredicateFormatException(string message) : Exception(message);

/// <summary>
/// The D-020 emergence-predicate DSL (T2.2), CLOSED grammar — comparisons and
/// boolean operators over REGISTERED variables, nothing else:
///
///   orExpr   := andExpr ( '||' andExpr )*
///   andExpr  := unary   ( '&amp;&amp;' unary   )*
///   unary    := '!' unary | '(' orExpr ')' | comparison
///   compare  := operand ('&gt;' | '&lt;' | '&gt;=' | '&lt;=' | '==') operand
///   operand  := variableName | numberLiteral (invariant culture)
///
/// No functions, no arithmetic (v1 — queue if needed). Precedence: ! over
/// &amp;&amp; over ||. Parsed ONCE at config load with loud rejection: an unknown
/// variable names the token AND lists the registry (Variables.KnownList); a
/// malformed expression names the offending token and its position. The parsed
/// tree is immutable and evaluates against a per-settlement variable reader
/// (PREV turn's rows — one-turn lag, §3.2). Deterministic: pure tree walk,
/// ordinal string handling, invariant number parsing.
/// </summary>
public sealed class Predicate
{
    /// <summary>The source text, kept for error reporting and round-trip tests.</summary>
    public string Source { get; }

    private readonly Node _root;

    private Predicate(string source, Node root)
    {
        Source = source;
        _root = root;
    }

    /// <summary>Reads a registered variable's value for the settlement under evaluation.</summary>
    public delegate double VariableReader(int varId);

    public bool Evaluate(VariableReader read) => Eval(_root, read);

    public static Predicate Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new PredicateFormatException(
                $"predicate is empty; expected comparisons over registered variables ({Variables.KnownList()}).");
        var tokens = Tokenize(source);
        int pos = 0;
        Node root = ParseOr(source, tokens, ref pos);
        if (pos != tokens.Count)
            throw new PredicateFormatException(
                $"predicate '{source}': unexpected token '{tokens[pos].Text}' at offset {tokens[pos].Offset} after a complete expression.");
        return new Predicate(source, root);
    }

    // --- AST ----------------------------------------------------------------

    private abstract class Node;
    private sealed class OrNode(Node l, Node r) : Node { public readonly Node L = l, R = r; }
    private sealed class AndNode(Node l, Node r) : Node { public readonly Node L = l, R = r; }
    private sealed class NotNode(Node inner) : Node { public readonly Node Inner = inner; }
    private sealed class CompareNode(Operand l, string op, Operand r) : Node
    {
        public readonly Operand L = l, R = r;
        public readonly string Op = op;
    }
    private readonly struct Operand(int varId, double literal)
    {
        public readonly int VarId = varId;      // -1 → literal
        public readonly double Literal = literal;
        public double Value(VariableReader read) => VarId >= 0 ? read(VarId) : Literal;
    }

    private static bool Eval(Node n, VariableReader read) => n switch
    {
        OrNode o => Eval(o.L, read) || Eval(o.R, read),
        AndNode a => Eval(a.L, read) && Eval(a.R, read),
        NotNode not => !Eval(not.Inner, read),
        CompareNode c => c.Op switch
        {
            ">" => c.L.Value(read) > c.R.Value(read),
            "<" => c.L.Value(read) < c.R.Value(read),
            ">=" => c.L.Value(read) >= c.R.Value(read),
            "<=" => c.L.Value(read) <= c.R.Value(read),
            _ => c.L.Value(read) == c.R.Value(read), // "==" (exact — documented: use bands, not equality, for real gates)
        },
        _ => throw new InvalidOperationException("unreachable"),
    };

    // --- tokenizer ----------------------------------------------------------

    private readonly record struct Token(string Text, int Offset, TokenKind Kind);
    private enum TokenKind { Name, Number, Op, Paren, Not }

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '(' || c == ')')
            {
                tokens.Add(new Token(c.ToString(), i, TokenKind.Paren));
                i++;
            }
            else if (c == '!' && (i + 1 >= s.Length || s[i + 1] != '='))
            {
                tokens.Add(new Token("!", i, TokenKind.Not));
                i++;
            }
            else if (c == '&' || c == '|')
            {
                if (i + 1 >= s.Length || s[i + 1] != c)
                    throw new PredicateFormatException(
                        $"predicate '{s}': single '{c}' at offset {i}; boolean operators are '&&' and '||'.");
                tokens.Add(new Token(new string(c, 2), i, TokenKind.Op));
                i += 2;
            }
            else if (c == '>' || c == '<' || c == '=')
            {
                bool eq = i + 1 < s.Length && s[i + 1] == '=';
                string op = eq ? $"{c}=" : c.ToString();
                if (op == "=")
                    throw new PredicateFormatException(
                        $"predicate '{s}': single '=' at offset {i}; equality is '=='.");
                tokens.Add(new Token(op, i, TokenKind.Op));
                i += eq ? 2 : 1;
            }
            else if (char.IsAsciiDigit(c) || c == '.' || c == '-')
            {
                int start = i;
                i++;
                while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == '.')) i++;
                string text = s[start..i];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    throw new PredicateFormatException(
                        $"predicate '{s}': '{text}' at offset {start} is not a valid number.");
                tokens.Add(new Token(text, start, TokenKind.Number));
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_')) i++;
                tokens.Add(new Token(s[start..i], start, TokenKind.Name));
            }
            else
            {
                throw new PredicateFormatException(
                    $"predicate '{s}': unexpected character '{c}' at offset {i}.");
            }
        }
        return tokens;
    }

    // --- recursive-descent parser -------------------------------------------

    private static Node ParseOr(string src, List<Token> t, ref int pos)
    {
        Node left = ParseAnd(src, t, ref pos);
        while (pos < t.Count && t[pos].Text == "||")
        {
            pos++;
            left = new OrNode(left, ParseAnd(src, t, ref pos));
        }
        return left;
    }

    private static Node ParseAnd(string src, List<Token> t, ref int pos)
    {
        Node left = ParseUnary(src, t, ref pos);
        while (pos < t.Count && t[pos].Text == "&&")
        {
            pos++;
            left = new AndNode(left, ParseUnary(src, t, ref pos));
        }
        return left;
    }

    private static Node ParseUnary(string src, List<Token> t, ref int pos)
    {
        if (pos >= t.Count)
            throw new PredicateFormatException(
                $"predicate '{src}': expression ends where a comparison was expected.");
        if (t[pos].Kind == TokenKind.Not)
        {
            pos++;
            return new NotNode(ParseUnary(src, t, ref pos));
        }
        if (t[pos].Text == "(")
        {
            int open = t[pos].Offset;
            pos++;
            Node inner = ParseOr(src, t, ref pos);
            if (pos >= t.Count || t[pos].Text != ")")
                throw new PredicateFormatException(
                    $"predicate '{src}': '(' at offset {open} is never closed.");
            pos++;
            return inner;
        }
        return ParseComparison(src, t, ref pos);
    }

    private static Node ParseComparison(string src, List<Token> t, ref int pos)
    {
        Operand left = ParseOperand(src, t, ref pos);
        if (pos >= t.Count || t[pos].Kind != TokenKind.Op || t[pos].Text is "&&" or "||")
            throw new PredicateFormatException(
                $"predicate '{src}': expected a comparison operator (> < >= <= ==) after " +
                $"'{t[pos - 1].Text}' at offset {t[pos - 1].Offset}.");
        string op = t[pos].Text;
        pos++;
        Operand right = ParseOperand(src, t, ref pos);
        return new CompareNode(left, op, right);
    }

    private static Operand ParseOperand(string src, List<Token> t, ref int pos)
    {
        if (pos >= t.Count)
            throw new PredicateFormatException(
                $"predicate '{src}': expression ends where a variable or number was expected.");
        Token tok = t[pos];
        if (tok.Kind == TokenKind.Number)
        {
            pos++;
            return new Operand(-1, double.Parse(tok.Text, CultureInfo.InvariantCulture));
        }
        if (tok.Kind == TokenKind.Name)
        {
            int id = Variables.IdOf(tok.Text);
            if (id < 0)
                throw new PredicateFormatException(
                    $"predicate '{src}': unknown variable '{tok.Text}' at offset {tok.Offset}; " +
                    $"known variables: {Variables.KnownList()}.");
            pos++;
            return new Operand(id, 0.0);
        }
        throw new PredicateFormatException(
            $"predicate '{src}': unexpected token '{tok.Text}' at offset {tok.Offset}; " +
            "expected a variable or number.");
    }
}
