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
///
/// ADR-029 §11 / D-044 R8, R10 — THE RESEARCH DIALECT. It is the same parser, the
/// same tokenizer and the same evaluator; it is not a second language. It is
/// entered ONLY through <see cref="Parse(string, PredicateSymbols)"/>, and it
/// adds exactly three things:
///   (a) keyword aliases: the upper-case words AND, OR and NOT are the operators
///       &amp;&amp;, || and ! (the corpus prerequisite spelling);
///   (b) boolean ATOMS: a bare name that is NOT followed by a comparison operator
///       is an atom — a yes/no fact the caller resolves, e.g. "this node is
///       complete";
///   (c) caller-bound QUANTITIES: a name that is not a registered variable may be
///       bound by the caller to a number it reads, e.g. a good's stock.
///
///   unary    := NOT unary | '(' orExpr ')' | atom | comparison
///   operand  := variableName | quantityName | numberLiteral
///
/// There are still no functions and no arithmetic. <see cref="Parse(string)"/>
/// behaves as before: without a symbol table there are no aliases, atoms or
/// quantities, so every shipped predicate parses, evaluates and fails with the
/// same results and messages as before (the code path gained the introspection
/// bookkeeping, not new behaviour).
/// </summary>
public sealed class Predicate
{
    /// <summary>The source text, kept for error reporting and round-trip tests.</summary>
    public string Source { get; }

    private readonly Node _root;

    /// <summary>Research dialect: the distinct atom ids, in first-appearance order
    /// (a fixed walk of the source, never a hash order). Empty for a legacy predicate.</summary>
    public IReadOnlyList<int> AtomIds { get; }

    /// <summary>Research dialect: the distinct caller-bound quantity ids, in first-appearance order.</summary>
    public IReadOnlyList<int> QuantityIds { get; }

    /// <summary>True when any comparison reads a registered (settlement-scoped) variable.</summary>
    public bool ReadsVariables { get; }

    /// <summary>True when the expression contains a negation (! / NOT) anywhere.
    /// A predicate without one is MONOTONE in its atoms: making an atom true can
    /// never make it false. Research prerequisites rely on that (ADR-029 §2.2).</summary>
    public bool UsesNot { get; }

    private Predicate(string source, Node root, int[] atoms, int[] quantities, bool readsVariables, bool usesNot)
    {
        Source = source;
        _root = root;
        AtomIds = atoms;
        QuantityIds = quantities;
        ReadsVariables = readsVariables;
        UsesNot = usesNot;
    }

    /// <summary>Reads a registered variable's value for the settlement under evaluation.</summary>
    public delegate double VariableReader(int varId);

    /// <summary>Research dialect: resolves an atom id to its yes/no fact.</summary>
    public delegate bool AtomReader(int atomId);

    /// <summary>Research dialect: resolves a caller-bound quantity id to its value.</summary>
    public delegate double QuantityReader(int quantityId);

    public bool Evaluate(VariableReader read) => Eval(_root, read, null, null);

    /// <summary>Research dialect evaluation. Any reader the predicate does not
    /// need may be null; a predicate that needs a missing reader throws.</summary>
    public bool Evaluate(VariableReader? variables, AtomReader? atoms, QuantityReader? quantities) =>
        Eval(_root, variables, atoms, quantities);

    /// <summary>
    /// Research dialect: can the expression be TRUE for SOME truth assignment of its
    /// comparisons, given the atoms? For a NOT-free predicate this is exact, because
    /// the expression is monotone: take every comparison as true. For a predicate
    /// with a NOT it answers true, meaning "undecided" — a caller must never reject
    /// on it. The loader uses this to find Eureka conditions that can never hold
    /// (ADR-029 §2.5).
    /// </summary>
    public bool CanHold(AtomReader atoms) => UsesNot || Optimistic(_root, atoms);

    private static bool Optimistic(Node n, AtomReader atoms) => n switch
    {
        OrNode o => Optimistic(o.L, atoms) || Optimistic(o.R, atoms),
        AndNode a => Optimistic(a.L, atoms) && Optimistic(a.R, atoms),
        AtomNode at => atoms(at.AtomId),
        CompareNode => true,
        _ => true, // NotNode: unreachable, UsesNot short-circuits above
    };

    public static Predicate Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new PredicateFormatException(
                $"predicate is empty; expected comparisons over registered variables ({Variables.KnownList()}).");
        var tokens = Tokenize(source);
        int pos = 0;
        var walk = new Walk(null);
        Node root = ParseOr(source, tokens, ref pos, walk);
        if (pos != tokens.Count)
            throw new PredicateFormatException(
                $"predicate '{source}': unexpected token '{tokens[pos].Text}' at offset {tokens[pos].Offset} after a complete expression.");
        return new Predicate(source, root, [.. walk.Atoms], [.. walk.Quantities], walk.ReadsVariables, walk.UsesNot);
    }

    /// <summary>
    /// Research dialect parse (ADR-029 §11). Names resolve in a fixed order: a
    /// name followed by a comparison operator is an operand — a registered
    /// variable first, then a caller-bound quantity. Any other name is an atom.
    /// An unknown name fails loudly and names the token and the symbol table.
    /// </summary>
    public static Predicate Parse(string source, PredicateSymbols symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        if (string.IsNullOrWhiteSpace(source))
            throw new PredicateFormatException(
                $"predicate is empty; expected an expression over {symbols.Describe}.");
        var tokens = Tokenize(source);
        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.Kind != TokenKind.Name) continue;
            if (string.Equals(t.Text, "AND", StringComparison.Ordinal)) tokens[i] = new Token("&&", t.Offset, TokenKind.Op);
            else if (string.Equals(t.Text, "OR", StringComparison.Ordinal)) tokens[i] = new Token("||", t.Offset, TokenKind.Op);
            else if (string.Equals(t.Text, "NOT", StringComparison.Ordinal)) tokens[i] = new Token("!", t.Offset, TokenKind.Not);
        }
        int pos = 0;
        var walk = new Walk(symbols);
        Node root = ParseOr(source, tokens, ref pos, walk);
        if (pos != tokens.Count)
            throw new PredicateFormatException(
                $"predicate '{source}': unexpected token '{tokens[pos].Text}' at offset {tokens[pos].Offset} after a complete expression.");
        return new Predicate(source, root, [.. walk.Atoms], [.. walk.Quantities], walk.ReadsVariables, walk.UsesNot);
    }

    /// <summary>Parse-time bookkeeping: the symbol table (null = legacy dialect)
    /// and the introspection lists, filled in source order.</summary>
    private sealed class Walk(PredicateSymbols? symbols)
    {
        public readonly PredicateSymbols? Symbols = symbols;
        public readonly List<int> Atoms = [];
        public readonly List<int> Quantities = [];
        public bool ReadsVariables;
        public bool UsesNot;

        public void NoteAtom(int id) { if (!Atoms.Contains(id)) Atoms.Add(id); }
        public void NoteQuantity(int id) { if (!Quantities.Contains(id)) Quantities.Add(id); }
    }

    // --- AST ----------------------------------------------------------------

    private abstract class Node;
    private sealed class OrNode(Node l, Node r) : Node { public readonly Node L = l, R = r; }
    private sealed class AndNode(Node l, Node r) : Node { public readonly Node L = l, R = r; }
    private sealed class NotNode(Node inner) : Node { public readonly Node Inner = inner; }
    private sealed class AtomNode(int atomId) : Node { public readonly int AtomId = atomId; }
    private sealed class CompareNode(Operand l, string op, Operand r) : Node
    {
        public readonly Operand L = l, R = r;
        public readonly string Op = op;
    }
    private readonly struct Operand(int varId, double literal, int quantityId = -1)
    {
        public readonly int VarId = varId;      // -1 → literal (or quantity)
        public readonly double Literal = literal;
        public readonly int QuantityId = quantityId;   // research dialect; -1 → not a quantity
        public double Value(VariableReader? read, QuantityReader? quantities) =>
            VarId >= 0 ? (read ?? throw new InvalidOperationException("predicate reads a variable but no variable reader was supplied"))(VarId)
            : QuantityId >= 0 ? (quantities ?? throw new InvalidOperationException("predicate reads a quantity but no quantity reader was supplied"))(QuantityId)
            : Literal;
    }

    private static bool Eval(Node n, VariableReader? read, AtomReader? atoms, QuantityReader? quantities) => n switch
    {
        OrNode o => Eval(o.L, read, atoms, quantities) || Eval(o.R, read, atoms, quantities),
        AndNode a => Eval(a.L, read, atoms, quantities) && Eval(a.R, read, atoms, quantities),
        NotNode not => !Eval(not.Inner, read, atoms, quantities),
        AtomNode at => (atoms ?? throw new InvalidOperationException("predicate reads an atom but no atom reader was supplied"))(at.AtomId),
        CompareNode c => c.Op switch
        {
            ">" => c.L.Value(read, quantities) > c.R.Value(read, quantities),
            "<" => c.L.Value(read, quantities) < c.R.Value(read, quantities),
            ">=" => c.L.Value(read, quantities) >= c.R.Value(read, quantities),
            "<=" => c.L.Value(read, quantities) <= c.R.Value(read, quantities),
            _ => c.L.Value(read, quantities) == c.R.Value(read, quantities), // "==" (exact — documented: use bands, not equality, for real gates)
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

    private static Node ParseOr(string src, List<Token> t, ref int pos, Walk walk)
    {
        Node left = ParseAnd(src, t, ref pos, walk);
        while (pos < t.Count && t[pos].Text == "||")
        {
            pos++;
            left = new OrNode(left, ParseAnd(src, t, ref pos, walk));
        }
        return left;
    }

    private static Node ParseAnd(string src, List<Token> t, ref int pos, Walk walk)
    {
        Node left = ParseUnary(src, t, ref pos, walk);
        while (pos < t.Count && t[pos].Text == "&&")
        {
            pos++;
            left = new AndNode(left, ParseUnary(src, t, ref pos, walk));
        }
        return left;
    }

    private static Node ParseUnary(string src, List<Token> t, ref int pos, Walk walk)
    {
        if (pos >= t.Count)
            throw new PredicateFormatException(
                $"predicate '{src}': expression ends where a comparison was expected.");
        if (t[pos].Kind == TokenKind.Not)
        {
            pos++;
            walk.UsesNot = true;
            return new NotNode(ParseUnary(src, t, ref pos, walk));
        }
        if (t[pos].Text == "(")
        {
            int open = t[pos].Offset;
            pos++;
            Node inner = ParseOr(src, t, ref pos, walk);
            if (pos >= t.Count || t[pos].Text != ")")
                throw new PredicateFormatException(
                    $"predicate '{src}': '(' at offset {open} is never closed.");
            pos++;
            return inner;
        }
        // Research dialect only: a bare name not followed by a comparison operator is an atom.
        if (walk.Symbols is { } symbols && t[pos].Kind == TokenKind.Name && !IsComparisonAt(t, pos + 1))
        {
            Token tok = t[pos];
            int atom = symbols.ResolveAtom(tok.Text);
            if (atom < 0)
                throw new PredicateFormatException(
                    $"predicate '{src}': unknown name '{tok.Text}' at offset {tok.Offset}; " +
                    $"expected {symbols.Describe}.");
            pos++;
            walk.NoteAtom(atom);
            return new AtomNode(atom);
        }
        return ParseComparison(src, t, ref pos, walk);
    }

    private static bool IsComparisonAt(List<Token> t, int i) =>
        i < t.Count && t[i].Kind == TokenKind.Op && t[i].Text is not ("&&" or "||");

    private static Node ParseComparison(string src, List<Token> t, ref int pos, Walk walk)
    {
        Operand left = ParseOperand(src, t, ref pos, walk);
        if (pos >= t.Count || t[pos].Kind != TokenKind.Op || t[pos].Text is "&&" or "||")
            throw new PredicateFormatException(
                $"predicate '{src}': expected a comparison operator (> < >= <= ==) after " +
                $"'{t[pos - 1].Text}' at offset {t[pos - 1].Offset}.");
        string op = t[pos].Text;
        pos++;
        Operand right = ParseOperand(src, t, ref pos, walk);
        return new CompareNode(left, op, right);
    }

    private static Operand ParseOperand(string src, List<Token> t, ref int pos, Walk walk)
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
            if (id < 0 && walk.Symbols is { } symbols)
            {
                int q = symbols.ResolveQuantity(tok.Text);
                if (q < 0)
                    throw new PredicateFormatException(
                        $"predicate '{src}': unknown operand '{tok.Text}' at offset {tok.Offset}; " +
                        $"expected a registered variable ({Variables.KnownList()}) or {symbols.Describe}.");
                pos++;
                walk.NoteQuantity(q);
                return new Operand(-1, 0.0, q);
            }
            if (id < 0)
                throw new PredicateFormatException(
                    $"predicate '{src}': unknown variable '{tok.Text}' at offset {tok.Offset}; " +
                    $"known variables: {Variables.KnownList()}.");
            pos++;
            walk.ReadsVariables = true;
            return new Operand(id, 0.0);
        }
        throw new PredicateFormatException(
            $"predicate '{src}': unexpected token '{tok.Text}' at offset {tok.Offset}; " +
            "expected a variable or number.");
    }
}

/// <summary>
/// The caller's symbol table for the research dialect (ADR-029 §11). It is two
/// resolvers and a description for error messages. A resolver returns a
/// non-negative id or -1 for "unknown". Resolution is pure: the parse depends
/// only on the source text and the table, never on iteration order.
/// </summary>
public sealed class PredicateSymbols(
    Func<string, int> resolveAtom, Func<string, int> resolveQuantity, string describe)
{
    public int ResolveAtom(string name) => resolveAtom(name);
    public int ResolveQuantity(string name) => resolveQuantity(name);

    /// <summary>What the table accepts, for actionable error messages.</summary>
    public string Describe { get; } = describe;
}
