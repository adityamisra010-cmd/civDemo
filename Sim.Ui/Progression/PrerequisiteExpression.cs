using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>The operator of a <see cref="PrereqExpr"/> node: a single prerequisite (an atom), ALL of its parts, or ONE OF them.</summary>
public enum PrereqOp { Atom = 0, All = 1, Any = 2 }

/// <summary>
/// D-084 (research-tree audit §4) — A NODE'S PREREQUISITE EXPRESSION AS THE RESEARCH SCREENS KEEP IT: the content's parsed
/// <see cref="Predicate"/> folded (<see cref="Predicate.Fold{T}"/>) into an n-ary tree whose atoms are content indices. A chain
/// of one operator becomes one group ("a AND b AND c" is all-of[a, b, c]); a change of operator is a nested group, so
/// "seed_drill OR (plough_ard AND horse_collar)" stays one-of[seed_drill, all-of[plough_ard, horse_collar]] and is never read as
/// three alternatives. The tree models atoms, AND, OR and parentheses — every shipped prerequisite is made of those alone — and
/// then holds exactly the expression's truth table (pinned for every node by Sim.Ui.Tests). An expression with anything else has
/// no tree here (<see cref="Of"/> returns null). Pure; no hashing, no dictionaries.
/// </summary>
public sealed class PrereqExpr
{
    public PrereqOp Op { get; }
    /// <summary>The prerequisite's content index (<see cref="PrereqOp.Atom"/>), else -1.</summary>
    public int Atom { get; }
    /// <summary>The parts of a group, in source order (at least two); empty for an atom.</summary>
    public IReadOnlyList<PrereqExpr> Parts { get; }

    private PrereqExpr(PrereqOp op, int atom, PrereqExpr[] parts) { Op = op; Atom = atom; Parts = parts; }

    public static PrereqExpr Leaf(int contentIndex) => new(PrereqOp.Atom, contentIndex, []);

    /// <summary>A group of <paramref name="op"/> over <paramref name="parts"/>: a part of the same operator is spliced in (the
    /// operator is associative), a repeated atom is kept once (it is idempotent) and a group of one part is that part.</summary>
    public static PrereqExpr Group(PrereqOp op, IReadOnlyList<PrereqExpr> parts)
    {
        if (op == PrereqOp.Atom) throw new ArgumentException("a group is ALL or ANY", nameof(op));
        var flat = new List<PrereqExpr>();
        foreach (PrereqExpr p in parts)
        {
            if (p.Op == op) { foreach (PrereqExpr q in p.Parts) AddPart(flat, q); }
            else AddPart(flat, p);
        }
        if (flat.Count == 0) throw new ArgumentException("a group needs a part", nameof(parts));
        return flat.Count == 1 ? flat[0] : new PrereqExpr(op, -1, [.. flat]);
    }

    private static void AddPart(List<PrereqExpr> parts, PrereqExpr p)
    {
        if (p.Op == PrereqOp.Atom)
            foreach (PrereqExpr q in parts) if (q.Op == PrereqOp.Atom && q.Atom == p.Atom) return;
        parts.Add(p);
    }

    /// <summary>The expression of <paramref name="node"/>; null for a root, and null for an expression this tree does not model —
    /// one holding a comparison or a NOT, which <see cref="Predicate.Fold{T}"/> folds to one placeholder whose truth is not known
    /// here. The research loader refuses a NOT and a comparison that reads a variable or a quantity in a prerequisite, but it
    /// admits a comparison of literals such as "1 &gt; 0" (always true or always false), so such content can load (D-084
    /// verification round 1, RT-R1-02; no shipped prerequisite has one). The screens then show what makes no false claim: each
    /// prerequisite's kind (<see cref="Kinds"/>, decided from the Predicate itself), the flat REQUIRES list and the content's own
    /// expression text — never a guessed tree, and never an exception.</summary>
    public static PrereqExpr? Of(ResearchNode node)
    {
        if (node.Prerequisite is not { } p) return null;
        PrereqExpr e = p.Fold<PrereqExpr>(Leaf, (l, r) => Group(PrereqOp.All, [l, r]), (l, r) => Group(PrereqOp.Any, [l, r]), Unrepresentable);
        return e.HasUnrepresentable() ? null : e;
    }

    /// <summary>The placeholder Fold returns for a comparison or a negation.</summary>
    private static readonly PrereqExpr Unrepresentable = new(PrereqOp.Atom, -1, []);

    private bool HasUnrepresentable()
    {
        if (ReferenceEquals(this, Unrepresentable)) return true;
        foreach (PrereqExpr p in Parts) if (p.HasUnrepresentable()) return true;
        return false;
    }

    /// <summary>Whether the expression holds when <paramref name="done"/> says which prerequisites are complete.</summary>
    public bool Holds(Func<int, bool> done)
    {
        switch (Op)
        {
            case PrereqOp.Atom: return done(Atom);
            case PrereqOp.All:
                foreach (PrereqExpr p in Parts) if (!p.Holds(done)) return false;
                return true;
            default:
                foreach (PrereqExpr p in Parts) if (p.Holds(done)) return true;
                return false;
        }
    }

    /// <summary>Whether the expression has an alternative anywhere (an ANY group).</summary>
    public bool HasAlternatives
    {
        get
        {
            if (Op == PrereqOp.Any) return true;
            foreach (PrereqExpr p in Parts) if (p.HasAlternatives) return true;
            return false;
        }
    }

    /// <summary>The expression in words with every nested group in parentheses: "Seed drill OR (Ard plough AND Padded horse
    /// collar)". The detail pane passes <see cref="NameInExpression"/>, so a name with parentheses of its own is quoted.</summary>
    public string Text(Func<int, string> name)
    {
        if (Op == PrereqOp.Atom) return name(Atom);
        var parts = new List<string>(Parts.Count);
        foreach (PrereqExpr p in Parts) parts.Add(p.Op == PrereqOp.Atom ? p.Text(name) : "(" + p.Text(name) + ")");
        return string.Join(Op == PrereqOp.All ? " AND " : " OR ", parts);
    }

    /// <summary>D-084 verification round 1 (RT-R1-06 a): a node's name as the pane's expression line writes it — in double quotes
    /// when it holds a parenthesis of its own (11 shipped names do, such as "Cuneiform (full writing)"), so that every unquoted
    /// parenthesis of the line is the expression's grouping: Lunar calendar AND ("Cuneiform (full writing)" OR Egyptian
    /// hieroglyphic OR ...).</summary>
    public static string NameInExpression(string name) => name.IndexOf('(') >= 0 || name.IndexOf(')') >= 0 ? "\"" + name + "\"" : name;

    /// <summary>
    /// The per-prerequisite kind the screens show — the edge's stroke and the pane's label — aligned with
    /// <see cref="ResearchNode.PrerequisiteNodes"/>, decided from the canonical expression itself so that no kind is ever false:
    /// <see cref="EdgeKind.And"/> ("requires") when the prerequisite holds on every satisfying route
    /// (<see cref="Predicate.MustHoldAtoms"/>); <see cref="EdgeKind.Or"/> ("one of") when it is not required, the required ones
    /// alone do not satisfy the expression, and the required ones with it do — it completes the choice on its own;
    /// <see cref="EdgeKind.Grouped"/> otherwise — it counts only together with others (a part of a grouped alternative), as
    /// plough_ard and horse_collar do in seed_drill OR (plough_ard AND horse_collar). Without a Grouped prerequisite the plain
    /// reading — every "requires" AND at least one "one of" (if any) — is exactly the expression.
    /// </summary>
    public static EdgeKind[] Kinds(ResearchNode node)
    {
        IReadOnlyList<int> atoms = node.PrerequisiteNodes;
        var kinds = new EdgeKind[atoms.Count];
        if (node.Prerequisite is not { } p) return kinds;
        IReadOnlyList<int> must = p.MustHoldAtoms();
        bool mustAlone = p.Evaluate(null, a => Contains(must, a), null);
        for (int k = 0; k < atoms.Count; k++)
        {
            int a = atoms[k];
            if (Contains(must, a)) kinds[k] = EdgeKind.And;
            else if (!mustAlone && p.Evaluate(null, x => x == a || Contains(must, x), null)) kinds[k] = EdgeKind.Or;
            else kinds[k] = EdgeKind.Grouped;
        }
        return kinds;
    }

    private static bool Contains(IReadOnlyList<int> list, int v)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == v) return true;
        return false;
    }
}

/// <summary>One row of the detail pane's REQUIRES list (D-084): a prerequisite (<see cref="Atom"/> ≥ 0) with its
/// <see cref="Kind"/> label and completion, or the head of a nested group ("one of:", "all of:") the rows under it belong to.
/// <see cref="Depth"/> is the indentation.</summary>
public sealed record RequiresRow(int Depth, int Atom, string Text, string Label, bool Met);

/// <summary>D-084 — the detail pane's REQUIRES rows. A node whose every prerequisite is "requires" or "one of" (no
/// <see cref="EdgeKind.Grouped"/> one) keeps the flat list it always had: one row per prerequisite, labelled. A node with a
/// grouped alternative gets the expression's own outline: its top-level prerequisites labelled, every nested group under a
/// head row naming its operator and its prerequisites indented under it — so the grouping is never lost and no row claims a
/// kind it does not have.</summary>
public static class RequiresRows
{
    public const string Required = "required", OneOf = "one of";

    public static string LabelOf(EdgeKind kind) => kind switch { EdgeKind.And => Required, EdgeKind.Or => OneOf, _ => "" };

    public static List<RequiresRow> Of(ResearchNodeView n)
    {
        var rows = new List<RequiresRow>();
        if (n.Prerequisites.Count == 0) return rows;
        bool grouped = false;
        foreach (PrereqView p in n.Prerequisites) grouped |= p.Kind == EdgeKind.Grouped;
        if (!grouped || n.Requirement is not { } expr)
        {
            foreach (PrereqView p in n.Prerequisites) rows.Add(new RequiresRow(0, p.ContentIndex, p.Name, LabelOf(p.Kind), p.Completed));
            return rows;
        }
        if (expr.Op == PrereqOp.Atom) Part(n, expr, 0, "", rows);
        else   // the REQUIRES heading heads the root
            for (int k = 0; k < expr.Parts.Count; k++) Part(n, expr.Parts[k], 0, HeadPrefix(expr.Op == PrereqOp.Any, k, root: true), rows);
        return rows;
    }

    /// <summary>One part at <paramref name="depth"/>: a prerequisite's row, or a nested group's head (after
    /// <paramref name="prefix"/>) and its parts one level in.</summary>
    private static void Part(ResearchNodeView n, PrereqExpr part, int depth, string prefix, List<RequiresRow> rows)
    {
        if (part.Op == PrereqOp.Atom)
        {
            foreach (PrereqView p in n.Prerequisites)
                if (p.ContentIndex == part.Atom) { rows.Add(new RequiresRow(depth, p.ContentIndex, p.Name, LabelOf(p.Kind), p.Completed)); return; }
            return;
        }
        string head = prefix + (part.Op == PrereqOp.All ? "all of:" : "one of:");
        rows.Add(new RequiresRow(depth, -1, head, "", part.Holds(i => Done(n, i))));
        for (int k = 0; k < part.Parts.Count; k++) Part(n, part.Parts[k], depth + 1, HeadPrefix(part.Op == PrereqOp.Any, k, root: false), rows);
    }

    /// <summary>The word before the head of a nested group that is part <paramref name="index"/> of its parent: in an
    /// alternative (ONE OF) parent, "or " before every alternative after the first; the first takes "either " at the root,
    /// whose operator no head of its own states, and nothing under a "one of:" head, which states it (D-084 verification
    /// round 1, RT-R1-06 c: an outline no longer begins "or all of:"). Under an ALL parent, nothing.</summary>
    private static string HeadPrefix(bool parentAny, int index, bool root) => !parentAny ? "" : index > 0 ? "or " : root ? "either " : "";

    private static bool Done(ResearchNodeView n, int contentIndex)
    {
        foreach (PrereqView p in n.Prerequisites) if (p.ContentIndex == contentIndex) return p.Completed;
        return false;
    }
}
