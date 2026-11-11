using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Xunit;
using Xunit.Abstractions;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// D-084 (lane RT) — the research-tree audit (docs/m6-plan/research-tree-audit-2026-11-10.md) REPRODUCED on the shipped
/// content and the stepped real world, then held as regressions. Each fact here was first run on the unchanged tree
/// (base 51ab032) and failed there for the reason the audit gives; the record of those failures is in
/// docs/m6-plan/research-tree-fixes-2026-11.md. The facts read only what the player is shown — the graph's edge kinds,
/// the detail pane's prerequisite labels, the painted edge inks and the three "~N turns" estimates — and compare it with the
/// canonical expression (Predicate) and with what the engine does when it is stepped.
/// </summary>
public class ResearchTreeAuditTests(SteppedWorldFixture fx, ITestOutputHelper output) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly EraTable Era = UiSession.ProductionEra();
    private const double W = 1600, H = 1000;

    // ------------------------------------------------------------------ §4: a drawn kind must be true of the expression

    /// <summary>Whether the expression holds when exactly <paramref name="trueAtoms"/> are complete.</summary>
    private static bool Holds(Predicate p, SortedSet<int> trueAtoms) => p.Evaluate(null, a => trueAtoms.Contains(a), null);

    /// <summary>"" when the per-atom kind the player is shown is true of the expression, else why not. A "requires" (And)
    /// atom holds on EVERY satisfying route (MustHoldAtoms); a "one of" (Or) atom, with the required atoms, completes the
    /// expression on its own — and the required atoms alone do not. Any other kind makes no per-atom claim.</summary>
    private static string KindError(ResearchNode node, int atom, EdgeKind kind)
    {
        Predicate p = node.Prerequisite!;
        var must = new SortedSet<int>(p.MustHoldAtoms());
        if (kind == EdgeKind.And)
            return must.Contains(atom) ? "" : "shown 'requires' but not required on every route";
        if (kind == EdgeKind.Or)
        {
            if (must.Contains(atom)) return "shown 'one of' but required on every route";
            var with = new SortedSet<int>(must) { atom };
            if (!Holds(p, with)) return "shown 'one of' but it does not complete the choice on its own";
            if (Holds(p, must)) return "shown 'one of' but the expression holds without it";
        }
        return "";
    }

    [Fact]
    public void EveryDrawnEdgeKind_IsTrueOfItsExpression_InBothTrees()
    {
        // Audit §4 / §8: the graph classified each edge from MustHoldAtoms alone — "one of" for every atom outside the must
        // set — so an atom that is one part of a grouped alternative ("plough_ard" in seed_drill OR (plough_ard AND
        // horse_collar)) was drawn as an alternative it is not.
        ResearchContent c = fx.Content;
        var wrong = new List<string>();
        int edges = 0;
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchGraph g = ResearchGraph.Build(c, tree);
            foreach (GraphEdge e in g.Edges)
            {
                edges++;
                ResearchNode node = g.Node(e.To);
                int atom = g.Vertices[e.From].ContentIndex;
                string why = KindError(node, atom, e.Kind);
                if (why.Length > 0) wrong.Add(node.Id + " <- " + c.Nodes[atom].Id + ": " + why);
            }
        }
        foreach (string line in wrong) output.WriteLine(line);
        Assert.Equal(1973, edges);   // audit §2: every direct prerequisite edge, both trees
        Assert.Empty(wrong);
    }

    [Fact]
    public void EveryPrerequisiteLabel_OfTheDetailPane_IsTrueOfItsExpression()
    {
        // The pane's per-row "required" / "one of" label is ResearchNodeView.Prerequisites[k].Kind.
        ResearchContent c = fx.Content;
        ResearchSnapshot s = ResearchSnapshot.Build(fx.Session.World, c, Me);
        var wrong = new List<string>();
        for (int i = 0; i < c.Nodes.Count; i++)
            foreach (PrereqView p in s.Nodes[i].Prerequisites)
            {
                string why = KindError(c.Nodes[i], p.ContentIndex, p.Kind);
                if (why.Length > 0) wrong.Add(c.Nodes[i].Id + " <- " + c.Nodes[p.ContentIndex].Id + ": " + why);
            }
        foreach (string line in wrong) output.WriteLine(line);
        Assert.Empty(wrong);
    }

    // ------------------------------------------------------------------ §4: the UI model keeps every truth table

    /// <summary>Every assignment of <paramref name="atoms"/>: bit k of the mask says whether atom k is complete.</summary>
    private static IEnumerable<SortedSet<int>> Assignments(IReadOnlyList<int> atoms)
    {
        for (int mask = 0; mask < 1 << atoms.Count; mask++)
        {
            var set = new SortedSet<int>();
            for (int k = 0; k < atoms.Count; k++) if (((mask >> k) & 1) != 0) set.Add(atoms[k]);
            yield return set;
        }
    }

    /// <summary>The Boolean function a REQUIRES list states, read as the player reads it. A flat list (no head rows): every
    /// "required" row AND, when there is any, at least one "one of" row. An outline: each head row's operator ("all of:" /
    /// "one of:") over the rows one level in; the top level is ONE OF when a top-level prerequisite says "one of" or a top-level
    /// head begins "or " or "either ", else ALL OF.</summary>
    private static Func<SortedSet<int>, bool> Reading(IReadOnlyList<RequiresRow> rows)
    {
        if (!rows.Any(r => r.Atom < 0))
        {
            Assert.All(rows, r => Assert.True(r.Label is RequiresRows.Required or RequiresRows.OneOf, "a flat row without a kind: " + r.Text));
            int[] req = [.. rows.Where(r => r.Label == RequiresRows.Required).Select(r => r.Atom)];
            int[] alt = [.. rows.Where(r => r.Label == RequiresRows.OneOf).Select(r => r.Atom)];
            return done => req.All(done.Contains) && (alt.Length == 0 || alt.Any(done.Contains));
        }
        bool rootAny = rows.Any(r => r.Depth == 0 && (r.Atom >= 0 ? r.Label == RequiresRows.OneOf
            : r.Text.StartsWith("or ", StringComparison.Ordinal) || r.Text.StartsWith("either ", StringComparison.Ordinal)));
        int at = 0;
        Func<SortedSet<int>, bool> root = ReadGroup(rows, ref at, 0, rootAny);
        Assert.Equal(rows.Count, at);
        return root;
    }

    private static Func<SortedSet<int>, bool> ReadGroup(IReadOnlyList<RequiresRow> rows, ref int at, int depth, bool any)
    {
        var parts = new List<Func<SortedSet<int>, bool>>();
        while (at < rows.Count && rows[at].Depth == depth)
        {
            RequiresRow r = rows[at++];
            if (r.Atom >= 0) { int a = r.Atom; parts.Add(done => done.Contains(a)); continue; }
            bool sub = r.Text.EndsWith("one of:", StringComparison.Ordinal);
            Assert.True(sub || r.Text.EndsWith("all of:", StringComparison.Ordinal), "a head row that names no operator: " + r.Text);
            parts.Add(ReadGroup(rows, ref at, depth + 1, sub));
        }
        Assert.True(at >= rows.Count || rows[at].Depth < depth, "a row deeper than its group's head");
        Func<SortedSet<int>, bool>[] ps = [.. parts];
        return any ? done => ps.Any(p => p(done)) : done => ps.All(p => p(done));
    }

    [Fact]
    public void EveryExpression_ItsUiModel_ItsPaneRows_AndItsGraph_KeepItsTruthTable()
    {
        // The brief's corpus-wide check (D-084 §4): for every prerequisite expression of the shipped content and every
        // completion of its atoms, the canonical Predicate, the UI model (PrereqExpr, on the view and in the graph) and the
        // REQUIRES rows the pane paints agree.
        ResearchContent c = fx.Content;
        ResearchSnapshot s = ResearchSnapshot.Build(fx.Session.World, c, Me);
        ResearchGraph[] graphs = [ResearchGraph.Build(c, ResearchTree.Technology), ResearchGraph.Build(c, ResearchTree.Civics)];
        int expressions = 0, assignments = 0, outlined = 0;
        var outlinedIds = new List<string>();
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchNode node = c.Nodes[i];
            ResearchGraph g = graphs[node.Tree == ResearchTree.Civics ? 1 : 0];
            if (node.Prerequisite is not { } p)
            {
                Assert.Null(s.Nodes[i].Requirement);
                Assert.Null(g.Requirement(g.VertexOf(i)));
                continue;
            }
            expressions++;
            PrereqExpr view = Assert.IsType<PrereqExpr>(s.Nodes[i].Requirement), graph = Assert.IsType<PrereqExpr>(g.Requirement(g.VertexOf(i)));
            List<RequiresRow> rows = RequiresRows.Of(s.Nodes[i]);
            Assert.Equal(node.PrerequisiteNodes.OrderBy(x => x), rows.Where(r => r.Atom >= 0).Select(r => r.Atom).OrderBy(x => x));
            if (rows.Any(r => r.Atom < 0)) { outlined++; outlinedIds.Add(node.Id); }
            Func<SortedSet<int>, bool> read = Reading(rows);
            foreach (SortedSet<int> done in Assignments(node.PrerequisiteNodes))
            {
                assignments++;
                bool want = Holds(p, done);
                Assert.True(want == view.Holds(done.Contains), node.Id + ": the view's expression");
                Assert.True(want == graph.Holds(done.Contains), node.Id + ": the graph's expression");
                Assert.True(want == read(done), node.Id + ": the pane's rows");
            }
        }
        output.WriteLine($"{expressions} expressions, {assignments} assignments; outlined: {string.Join(", ", outlinedIds)}");
        Assert.Equal(995, expressions);   // audit §2: 1 005 nodes, 10 roots
        Assert.Equal(["astronomy_records", "seed_drill_tull", "nuclear_fusion_research", "constitutional_monarchy"], outlinedIds);
    }

    [Theory]
    [InlineData("seed_drill_tull", "seed_drill OR (plough_ard AND horse_collar)", "Or Grouped Grouped")]
    [InlineData("astronomy_records", "arithmetic_babylonian OR ((hieroglyphic OR chinese_script OR mesoamerican_script) AND calendar_lunar)",
        "Or Grouped Grouped Grouped Grouped")]
    [InlineData("nuclear_fusion_research", "nuclear_physics AND (superconductivity_applied OR (thermonuclear AND laser))", "And Or Grouped Grouped")]
    [InlineData("constitutional_monarchy", "succession_law AND (written_constitution OR (bill_of_rights AND representative_assembly))",
        "And Or Grouped Grouped")]
    public void TheFourNestedExpressions_EveryAtomCombination_CanonicalAgainstTheUiModel(string id, string source, string kinds)
    {
        // Audit §4.1–§4.4: the canonical expression, the UI model, the pane's rows and the graph's kinds, over every combination
        // of the node's atoms; and the old flat reading (every MustHoldAtoms atom AND any other) shown to differ.
        ResearchContent c = fx.Content;
        int i = c.IndexOfId(id);
        ResearchNode node = c.Nodes[i];
        Predicate p = node.Prerequisite!;
        Assert.Equal(source, p.Source);
        PrereqExpr e = PrereqExpr.Of(node)!;
        Assert.Equal(source, e.Text(k => c.Nodes[k].Id));   // the grouping kept, written as the content writes it
        Assert.Equal(kinds, string.Join(" ", PrereqExpr.Kinds(node)));
        ResearchGraph g = ResearchGraph.Build(c, node.Tree);
        int v = g.VertexOf(i);
        Assert.Equal(kinds, string.Join(" ", g.Edges.Where(x => x.To == v).Select(x => x.Kind)));
        List<RequiresRow> rows = RequiresRows.Of(ResearchSnapshot.Build(fx.Session.World, c, Me).Nodes[i]);
        Func<SortedSet<int>, bool> read = Reading(rows);
        var must = new SortedSet<int>(p.MustHoldAtoms());
        int[] others = [.. node.PrerequisiteNodes.Where(a => !must.Contains(a))];
        int flatWrong = 0, combos = 0;
        foreach (SortedSet<int> done in Assignments(node.PrerequisiteNodes))
        {
            combos++;
            bool want = Holds(p, done);
            Assert.Equal(want, e.Holds(done.Contains));
            Assert.Equal(want, read(done));
            bool flat = must.All(done.Contains) && (others.Length == 0 || others.Any(done.Contains));
            if (flat != want) flatWrong++;
        }
        output.WriteLine($"{id}: {combos} combinations, the old flat reading wrong on {flatWrong}; rows: " +
            string.Join(" | ", rows.Select(r => new string(' ', 2 * r.Depth) + (r.Atom >= 0 ? (r.Met ? "[x] " : "[ ] ") : "") + r.Text + (r.Label.Length > 0 ? " (" + r.Label + ")" : ""))));
        Assert.Equal(1 << node.PrerequisiteNodes.Count, combos);
        Assert.True(flatWrong > 0, "the flat reading this fixes was wrong on some combination");
    }

    [Theory]
    [InlineData("seed_drill_tull")]
    [InlineData("astronomy_records")]
    [InlineData("nuclear_fusion_research")]
    [InlineData("constitutional_monarchy")]
    public void TheFourNestedExpressions_ThePaintedPane_ShowsTheGroups_AndNoKindLabelOnAGroupedPart(string id)
    {
        ResearchContent c = fx.Content;
        int node = c.IndexOfId(id);
        // One atom complete (the last), so the ticks are mixed.
        WorldState w = WithDone(c.Nodes[node].PrerequisiteNodes[^1]);
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        s.Tab = c.Nodes[node].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = node;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD pane = s.DetailRect;   // the pane scrolls: its lines below the fold are painted (clipped), so select by column
        TextCmd[] texts = [.. d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right)];
        string all = string.Join(" ", texts.Select(t => t.Text));
        // The expression in words with its groups: the first alternative or requirement, then the parenthesised group.
        PrereqExpr e = PrereqExpr.Of(c.Nodes[node])!;
        string words = e.Text(k => c.Nodes[k].Name);
        Assert.Contains(words[..Math.Min(18, words.Length)], all, StringComparison.Ordinal);
        Assert.Contains("all of:", all, StringComparison.Ordinal);
        TextCmd[] labels = [.. texts.Where(t => t.Text is RequiresRows.OneOf or RequiresRows.Required)];
        double rowX = RowX(texts);
        EdgeKind[] kinds = PrereqExpr.Kinds(c.Nodes[node]);
        for (int k = 0; k < kinds.Length; k++)
        {
            string name = c.Nodes[c.Nodes[node].PrerequisiteNodes[k]].Name;
            TextCmd row = Assert.Single(texts, t => IsRequiresRow(t, rowX) && t.Text.StartsWith(name, StringComparison.Ordinal));
            TextCmd[] onRow = [.. labels.Where(l => Math.Abs(l.Y - row.Y) < 6.0)];
            if (kinds[k] == EdgeKind.Grouped) Assert.Empty(onRow);
            else Assert.Equal(RequiresRows.LabelOf(kinds[k]), Assert.Single(onRow).Text);
        }
    }

    /// <summary>The pane's content edge: where its section headings ("REQUIRES") are written.</summary>
    private static double RowX(TextCmd[] paneTexts) => Assert.Single(paneTexts, t => t.Text == "REQUIRES").X;

    /// <summary>A REQUIRES row's name: body text set in from the content edge past its tick (the expression above the rows is
    /// in the numeric face, and other sections' wrapped lines — the Age sweep's list of names — start at the content edge).</summary>
    private static bool IsRequiresRow(TextCmd t, double contentX) => t.Role != FontRole.Numeric && t.X > contentX + 4.0;

    [Fact]
    public void SeedDrillTull_ThePaintedPane_PutsNoOneOfLabelOnAPartOfTheGroupedAlternative()
    {
        // Seed drill (Tull) requires seed_drill OR (plough_ard AND horse_collar): neither the ard plough nor the horse collar
        // is an alternative on its own, so neither row may carry the "one of" label.
        ResearchContent c = fx.Content;
        int tull = c.IndexOfId("seed_drill_tull");
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(fx.Session.World);
        s.Tab = TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = tull;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD pane = s.DetailRect;   // the pane scrolls: its lines below the fold are painted (clipped), so select by column
        TextCmd[] texts = [.. d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right)];
        TextCmd[] oneOf = [.. texts.Where(t => t.Text == "one of")];
        foreach (string part in new[] { "plough_ard", "horse_collar" })
        {
            string name = c.Nodes[c.IndexOfId(part)].Name;
            TextCmd row = Assert.Single(texts, t => IsRequiresRow(t, RowX(texts)) && t.Text.StartsWith(name, StringComparison.Ordinal));
            Assert.DoesNotContain(oneOf, l => Math.Abs(l.Y - row.Y) < 6.0);
        }
    }

    [Fact]
    public void ATwoOfThreeRequirement_NoPrerequisiteAloneOrRequired_IsShownAsItsGroups_AndItsLockLineNamesAGroup()
    {
        // Beyond the shipped corpus (whose four grouped expressions each keep one "one of" or "requires" atom): seed_drill_tull's
        // three prerequisites re-written as "any two of the three". No atom is required and none completes the choice on its
        // own, so every edge is Grouped, the pane is the expression's outline with no kind label, the outline keeps the truth
        // table, and the card's lock line takes the branch no shipped node reaches ("Needs a group: ... with others").
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string shipped = "\"prereq\": \"seed_drill OR (plough_ard AND horse_collar)\"";
        Assert.Contains(shipped, text);
        ResearchContent c = ResearchContentLoader.Load(text.Replace(shipped,
            "\"prereq\": \"(seed_drill AND plough_ard) OR (seed_drill AND horse_collar) OR (plough_ard AND horse_collar)\""), fx.Session.Config.Goods);
        int node = c.IndexOfId("seed_drill_tull"), sd = c.IndexOfId("seed_drill"), pa = c.IndexOfId("plough_ard"), hc = c.IndexOfId("horse_collar");
        Assert.Equal([EdgeKind.Grouped, EdgeKind.Grouped, EdgeKind.Grouped], PrereqExpr.Kinds(c.Nodes[node]));
        ResearchGraph g = ResearchGraph.Build(c, ResearchTree.Technology);
        int v = g.VertexOf(node);
        Assert.All(g.Edges.Where(e => e.To == v), e => Assert.Equal(EdgeKind.Grouped, e.Kind));
        foreach (int[] done in new int[][] { [], [sd], [pa], [sd, hc], [pa, hc], [sd, pa, hc] })
        {
            WorldState w = ((WorldState)fx.Session.World).Clone();
            Keep(w.ResearchCompleted, r => !(r.Polity.Value == Me.Value && (r.Node.Value == c.Nodes[sd].Key.Value
                || r.Node.Value == c.Nodes[pa].Key.Value || r.Node.Value == c.Nodes[hc].Key.Value)));
            foreach (int i in done) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[i].Key));
            ResearchNodeView view = ResearchSnapshot.Build(w, c, Me).Nodes[node];
            List<RequiresRow> rows = RequiresRows.Of(view);
            // One head per pair: "either all of:", then "or all of:" twice (D-084 verification round 1, RT-R1-06 c).
            Assert.Equal(["either all of:", "or all of:", "or all of:"], rows.Where(r => r.Atom < 0).Select(r => r.Text));
            Assert.All(rows, r => Assert.Equal("", r.Label));   // no prerequisite claims a kind
            Func<SortedSet<int>, bool> read = Reading(rows);
            foreach (SortedSet<int> set in Assignments(c.Nodes[node].PrerequisiteNodes))
            {
                bool want = set.Count >= 2;   // any two of the three
                Assert.Equal(want, Holds(c.Nodes[node].Prerequisite!, set));
                Assert.Equal(want, view.Requirement!.Holds(set.Contains));
                Assert.Equal(want, read(set));
            }
            var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
            s.Refresh(w);
            s.Tab = TreeTab.Technology;
            string line = s.LockLine(node);
            output.WriteLine($"{string.Join("+", done.Select(i => c.Nodes[i].Id))}: {line} | " +
                string.Join(" | ", rows.Select(r => new string(' ', 2 * r.Depth) + (r.Atom >= 0 ? (r.Met ? "[x] " : "[ ] ") : "") + r.Text)));
            if (done.Length < 2)
            {
                Assert.True(view.Lock.HasFlag(LockReason.MissingPrerequisites));
                int firstMissing = c.Nodes[node].PrerequisiteNodes.First(i => !done.Contains(i));
                Assert.Equal("Needs a group: " + c.Nodes[firstMissing].Name + " with others", line);
            }
            else Assert.False(view.Lock.HasFlag(LockReason.MissingPrerequisites));
        }
    }

    [Fact]
    public void ARedundantPartOfAGroup_IsNeverLabelledOneOf_BecauseTheRequiredAtomsAloneAlreadyHold()
    {
        // "seed_drill OR (seed_drill AND plough_ard AND horse_collar)" is seed_drill alone: plough_ard and horse_collar change
        // nothing. With the required atom (seed_drill) each of them still "completes" the expression — but only because
        // seed_drill alone already does, so neither may be labelled "one of" (PrereqExpr.Kinds' second condition, which no
        // shipped expression exercises): they are Grouped, the pane shows no kind on them and the outline keeps the truth table.
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string shipped = "\"prereq\": \"seed_drill OR (plough_ard AND horse_collar)\"";
        Assert.Contains(shipped, text);
        ResearchContent c = ResearchContentLoader.Load(text.Replace(shipped,
            "\"prereq\": \"seed_drill OR (seed_drill AND plough_ard AND horse_collar)\""), fx.Session.Config.Goods);
        int node = c.IndexOfId("seed_drill_tull");
        Assert.Equal([c.IndexOfId("seed_drill"), c.IndexOfId("plough_ard"), c.IndexOfId("horse_collar")], c.Nodes[node].PrerequisiteNodes);
        Assert.Equal([EdgeKind.And, EdgeKind.Grouped, EdgeKind.Grouped], PrereqExpr.Kinds(c.Nodes[node]));
        ResearchNodeView view = ResearchSnapshot.Build(fx.Session.World, c, Me).Nodes[node];
        List<RequiresRow> rows = RequiresRows.Of(view);
        Assert.DoesNotContain(rows, r => r.Label == RequiresRows.OneOf);
        Func<SortedSet<int>, bool> read = Reading(rows);
        foreach (SortedSet<int> set in Assignments(c.Nodes[node].PrerequisiteNodes))
        {
            bool want = set.Contains(c.IndexOfId("seed_drill"));
            Assert.Equal(want, Holds(c.Nodes[node].Prerequisite!, set));
            Assert.Equal(want, read(set));
        }
        Assert.Equal("", KindError(c.Nodes[node], c.IndexOfId("plough_ard"), PrereqExpr.Kinds(c.Nodes[node])[1]));
    }

    [Theory]
    [InlineData("(seed_drill AND plough_ard) OR (seed_drill AND horse_collar) OR (plough_ard AND horse_collar)", "either all of:|or all of:|or all of:")]
    [InlineData("(seed_drill AND plough_ard AND horse_collar) OR seed_drill", "either all of:")]
    [InlineData("seed_drill AND ((plough_ard AND horse_collar) OR plough_ard)", "one of:|all of:")]
    public void AnOutline_NeverBeginsWithOr_ItsFirstAlternativeGroupSaysEither_OrNothingUnderAOneOfHead(string prereq, string heads)
    {
        // D-084 verification round 1 (RT-R1-06 c): every group that was an alternative was headed "or ...", the first one too, so
        // an outline whose root is ONE OF and whose first part is a group began "or all of:". Now "or " heads the alternatives
        // after the first; the first says "either " at the root, whose operator no head of its own states (without it, the
        // second case would read as "all of"), and nothing under a "one of:" head, which states it. Beyond the shipped corpus,
        // whose four outlines never put a group first among alternatives; each outline keeps the expression's truth table.
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string shipped = "\"prereq\": \"seed_drill OR (plough_ard AND horse_collar)\"";
        Assert.Contains(shipped, text);
        ResearchContent c = ResearchContentLoader.Load(text.Replace(shipped, "\"prereq\": \"" + prereq + "\""), fx.Session.Config.Goods);
        int node = c.IndexOfId("seed_drill_tull");
        List<RequiresRow> rows = RequiresRows.Of(ResearchSnapshot.Build(fx.Session.World, c, Me).Nodes[node]);
        output.WriteLine(prereq + ": " + string.Join(" | ", rows.Select(r => new string(' ', 2 * r.Depth) + r.Text + (r.Label.Length > 0 ? " (" + r.Label + ")" : ""))));
        Assert.Equal(heads, string.Join("|", rows.Where(r => r.Atom < 0).Select(r => r.Text)));
        Assert.False(rows[0].Text.StartsWith("or ", StringComparison.Ordinal), "the outline begins with 'or'");
        Func<SortedSet<int>, bool> read = Reading(rows);
        foreach (SortedSet<int> set in Assignments(c.Nodes[node].PrerequisiteNodes))
            Assert.Equal(Holds(c.Nodes[node].Prerequisite!, set), read(set));
    }

    [Fact]
    public void TheExpressionLine_QuotesANameWithParenthesesOfItsOwn_SoEveryUnquotedParenthesisIsAGroup()
    {
        // D-084 verification round 1 (RT-R1-06 a): 11 node names hold parentheses ("Cuneiform (full writing)") and 13 expressions
        // with an OR name one, so their pane line read "Lunar calendar AND (Cuneiform (full writing) OR ...)". Such a name is now
        // quoted, and outside the quotes the line has exactly the parentheses of the expression's own groups.
        ResearchContent c = fx.Content;
        ResearchSnapshot s0 = ResearchSnapshot.Build(fx.Session.World, c, Me);
        for (int i = 0; i < c.Nodes.Count; i++) Assert.DoesNotContain('(', c.Nodes[i].Id);
        int lines = 0, quoting = 0;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            if (s0.Nodes[i].Requirement is not { HasAlternatives: true } e) continue;
            lines++;
            string shown = e.Text(k => PrereqExpr.NameInExpression(c.Nodes[k].Name)), ids = e.Text(k => c.Nodes[k].Id);
            string outside = Regex.Replace(shown, "\"[^\"]*\"", "\"\"");
            Assert.Equal(ids.Count(ch => ch == '('), outside.Count(ch => ch == '('));
            Assert.Equal(ids.Count(ch => ch == ')'), outside.Count(ch => ch == ')'));
            bool any = false;
            foreach (int a in c.Nodes[i].PrerequisiteNodes)
                if (c.Nodes[a].Name.Contains('(')) { any = true; Assert.Contains("\"" + c.Nodes[a].Name + "\"", shown, StringComparison.Ordinal); }
            if (any) quoting++;
        }
        output.WriteLine($"{lines} expression lines, {quoting} of them quote a name");
        Assert.Equal(96, lines);   // audit §2: 96 nodes have an OR
        Assert.Equal(13, quoting);
        // As painted: Civil solar calendar.
        int civil = c.IndexOfId("calendar_civil");
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(fx.Session.World);
        s.Tab = TreeTab.Civics;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = civil;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD pane = s.DetailRect;
        string all = string.Join(" ", d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right).Select(t => t.Text));
        Assert.Contains("Lunar calendar AND (\"Cuneiform (full writing)\" OR Egyptian hieroglyphic OR Oracle bone script OR Mesoamerican writing)",
            all, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("(seed_drill AND 1 > 0) OR (plough_ard AND horse_collar)", "Or Grouped Grouped")]
    [InlineData("(seed_drill AND 1 > 2) OR (plough_ard AND horse_collar)", "Grouped Grouped Grouped")]
    public void AComparisonOfLiterals_WhichTheLoaderAdmits_HasNoTree_AndTheScreensWriteItAsTheContentDoes(string prereq, string kinds)
    {
        // D-084 verification round 1 (RT-R1-02): the research loader refuses a NOT and a comparison that reads a variable or a
        // quantity in a prerequisite, but it admits a comparison of literals ("1 > 0" is always true, "1 > 2" always false).
        // PrereqExpr.Of threw on one, so the graph, the snapshot and the screen threw on content that loads. Now such an
        // expression has no tree (Of is null), every kind shown is still true of the Predicate itself, the screen builds and
        // paints the node's pane, and the pane writes the expression as the content writes it.
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string shipped = "\"prereq\": \"seed_drill OR (plough_ard AND horse_collar)\"";
        Assert.Contains(shipped, text);
        ResearchContent c = ResearchContentLoader.Load(text.Replace(shipped, "\"prereq\": \"" + prereq + "\""), fx.Session.Config.Goods);
        int node = c.IndexOfId("seed_drill_tull");
        ResearchNode n = c.Nodes[node];
        Assert.Equal(prereq, n.Prerequisite!.Source);
        Assert.Null(PrereqExpr.Of(n));
        EdgeKind[] k = PrereqExpr.Kinds(n);
        Assert.Equal(kinds, string.Join(" ", k));
        for (int a = 0; a < k.Length; a++) Assert.Equal("", KindError(n, n.PrerequisiteNodes[a], k[a]));
        ResearchGraph g = ResearchGraph.Build(c, ResearchTree.Technology);
        Assert.Null(g.Requirement(g.VertexOf(node)));
        Assert.Equal(kinds, string.Join(" ", g.Edges.Where(e => e.To == g.VertexOf(node)).Select(e => e.Kind)));
        ResearchNodeView view = ResearchSnapshot.Build(fx.Session.World, c, Me).Nodes[node];
        Assert.Null(view.Requirement);
        List<RequiresRow> rows = RequiresRows.Of(view);   // the flat list, each row with the label its own kind has
        Assert.Equal(n.PrerequisiteNodes, rows.Select(r => r.Atom));
        Assert.Equal(k.Select(RequiresRows.LabelOf), rows.Select(r => r.Label));
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(fx.Session.World);
        s.Tab = TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = node;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD pane = s.DetailRect;
        string all = string.Join(" ", d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right).Select(t => t.Text));
        Assert.Contains(prereq.Replace("_", " "), all, StringComparison.Ordinal);
        output.WriteLine($"{prereq}: no tree; kinds {kinds}; the pane writes '{prereq.Replace("_", " ")}'");
    }

    // ------------------------------------------------------------------ §3: an edge's ink follows its prerequisite's completion

    /// <summary>The stepped world with <paramref name="done"/> also complete.</summary>
    private WorldState WithDone(params int[] done)
    {
        WorldState w = ((WorldState)fx.Session.World).Clone();
        foreach (int i in done)
            if (!ResearchQuery.IsCompleted(w, Me, fx.Content.Nodes[i].Key))
                w.ResearchCompleted.Add(new ResearchCompletedRow(Me, fx.Content.Nodes[i].Key));
        return w;
    }

    /// <summary>The first node of <paramref name="tree"/> (content order), not complete, with at least two prerequisites none of
    /// which is complete on the stepped world, and — when <paramref name="withOneOf"/> — at least one "one of" edge.</summary>
    private int MixedCandidate(ResearchTree tree, bool withOneOf)
    {
        ResearchContent c = fx.Content;
        bool[] done = ResearchQuery.CompletedMask(fx.Session.World, c, Me);
        ResearchGraph g = ResearchGraph.Build(c, tree);
        for (int v = 0; v < g.OwnCount; v++)
        {
            ResearchNode n = g.Node(v);
            int ci = g.Vertices[v].ContentIndex;
            if (done[ci] || n.PrerequisiteNodes.Count < 2) continue;
            bool anyDone = false, anyOr = false;
            foreach (int p in n.PrerequisiteNodes) anyDone |= done[p];
            foreach (GraphEdge e in g.Edges) if (e.To == v && e.Kind == EdgeKind.Or) anyOr = true;
            if (!anyDone && (!withOneOf || anyOr)) return ci;
        }
        throw new InvalidOperationException("no candidate in " + tree);
    }

    /// <summary>The painted line segments of edge <paramref name="e"/> (the <paramref name="lane"/>-th edge the screen routed
    /// for its focus), in screen space, excluding the dark casing. The final stub into the card's top is left out: every
    /// incoming edge of a card ends on that same segment, so it belongs to no one edge.</summary>
    private static List<LineCmd> EdgeLines(ProgressionScreen s, DrawList d, GraphEdge e, int lane)
    {
        (double X, double Y)[] pts = ResearchTreeLayout.Route(s.Layout, e, lane);
        RectD c = s.Canvas;
        var found = new List<LineCmd>();
        for (int k = 0; k + 2 < pts.Length; k++)
        {
            double x0 = s.Camera.ToScreenX(pts[k].X, c.X), y0 = s.Camera.ToScreenY(pts[k].Y, c.Y);
            double x1 = s.Camera.ToScreenX(pts[k + 1].X, c.X), y1 = s.Camera.ToScreenY(pts[k + 1].Y, c.Y);
            foreach (LineCmd l in d.Commands.OfType<LineCmd>())
                if (Math.Abs(l.X0 - x0) < 1e-6 && Math.Abs(l.Y0 - y0) < 1e-6 && Math.Abs(l.X1 - x1) < 1e-6 && Math.Abs(l.Y1 - y1) < 1e-6
                    && l.Color != s.Theme.Ink.Text)
                    found.Add(l);
        }
        return found;
    }

    [Theory]
    [InlineData(ResearchTree.Technology, false)]
    [InlineData(ResearchTree.Technology, true)]
    [InlineData(ResearchTree.Civics, false)]
    [InlineData(ResearchTree.Civics, true)]
    public void IncomingEdges_TakeTheirPrerequisitesCompletion_PinnedAndHovered(ResearchTree tree, bool withOneOf)
    {
        // Audit §3: the pinned path drew every incoming edge in the pinned orange and the unpinned (hover) path every one in
        // the prerequisite ink, whether or not the prerequisite was complete. A completed prerequisite's edge must take the
        // completed ink; an outstanding one keeps the prerequisite ink (orange, D-070); "one of" edges keep their dash.
        ResearchContent c = fx.Content;
        int node = MixedCandidate(tree, withOneOf);
        int first = c.Nodes[node].PrerequisiteNodes[0];
        WorldState w = WithDone(first);
        foreach (bool pinned in new[] { true, false })
        {
            var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
            s.Refresh(w);
            s.Tab = tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
            s.Paint(W, H, ApproxTextMeasure.Instance);
            s.FitAll();
            s.Paint(W, H, ApproxTextMeasure.Instance);
            if (pinned) s.Selected = node;
            else
            {
                s.Selected = -1;
                PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(node)];
                s.PointerMove(s.Camera.ToScreenX(p.CenterX, s.Canvas.X), s.Camera.ToScreenY(p.CenterY, s.Canvas.Y));
                Assert.Equal(node, s.Hovered);
            }
            DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
            int fv = s.Graph.VertexOf(node), lane = 0, completedSeen = 0, outstandingSeen = 0;
            foreach (GraphEdge e in s.Graph.Edges)
            {
                if (e.From != fv && e.To != fv) continue;
                int at = lane++;
                if (e.To != fv) continue;
                bool done = s.Graph.Vertices[e.From].ContentIndex == first;
                List<LineCmd> lines = EdgeLines(s, d, e, at);
                Assert.NotEmpty(lines);
                Rgba want = done ? s.Theme.Semantic.Completed : pinned ? s.Theme.Semantic.Pinned : s.Theme.Semantic.Prerequisite;
                Assert.All(lines, l => Assert.True(l.Color == want,
                    $"{c.Nodes[node].Id} <- {c.Nodes[s.Graph.Vertices[e.From].ContentIndex].Id} ({(pinned ? "pinned" : "hovered")}, " +
                    $"{(done ? "complete" : "outstanding")}): ink {l.Color} expected {want}"));
                Assert.All(lines, l => Assert.Equal(e.Kind == EdgeKind.And, l.Dash is null));
                if (done) completedSeen++; else outstandingSeen++;
            }
            Assert.Equal(1, completedSeen);
            Assert.True(outstandingSeen >= 1);
        }
    }

    [Theory]
    [InlineData(ResearchTree.Technology)]
    [InlineData(ResearchTree.Civics)]
    public void AHoveredCardsIncomingEdges_AreCasedAsThePinnedOnes_SoTheCompletedInkReadsOnEveryAgesField(ResearchTree tree)
    {
        // D-084 verification round 1 (RT-R1-04): on the hover path (no pin) the completed ink was drawn uncased, under 3:1 on the
        // tree's field in every Age (1.33 to 3.15:1) and fainter than the prerequisite ink it replaced. Every incoming edge of
        // a hovered card, completed and outstanding alike, is now drawn over the dark casing the pinned path uses: each ink
        // segment lies on a wider casing segment painted before it. The casing reads on every Age's field and the completed
        // ink on the casing, both at 3:1 or more (WCAG 2.1 SC 1.4.11); dependents keep their plain stroke.
        foreach (Sim.Ui.Theme.EraTheme t in Sim.Ui.Theme.EraThemes.All)
        {
            double casingOnField = Sim.Ui.Theme.ThemeColor.Contrast(t.Ink.Text, t.Material.Field);
            double completedOnCasing = Sim.Ui.Theme.ThemeColor.Contrast(t.Semantic.Completed, t.Ink.Text);
            output.WriteLine($"{t.Era}: casing on the field {casingOnField:0.00}:1, completed ink on the casing {completedOnCasing:0.00}:1");
            Assert.True(casingOnField >= 3.0, $"{t.Era}: casing on the field {casingOnField:0.00}:1");
            Assert.True(completedOnCasing >= 3.0, $"{t.Era}: completed ink on the casing {completedOnCasing:0.00}:1");
        }
        ResearchContent c = fx.Content;
        int node = MixedCandidate(tree, withOneOf: false);
        int first = c.Nodes[node].PrerequisiteNodes[0];
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(WithDone(first));
        s.Tab = tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.FitAll();
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = -1;
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(node)];
        s.PointerMove(s.Camera.ToScreenX(p.CenterX, s.Canvas.X), s.Camera.ToScreenY(p.CenterY, s.Canvas.Y));
        Assert.Equal(node, s.Hovered);
        Assert.Equal(-1, s.Pin);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        var at = new Dictionary<LineCmd, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < d.Commands.Count; i++) if (d.Commands[i] is LineCmd l) at[l] = i;
        bool Same(LineCmd a, LineCmd b) => Math.Abs(a.X0 - b.X0) < 1e-6 && Math.Abs(a.Y0 - b.Y0) < 1e-6 && Math.Abs(a.X1 - b.X1) < 1e-6 && Math.Abs(a.Y1 - b.Y1) < 1e-6;
        LineCmd[] casings = [.. d.Commands.OfType<LineCmd>().Where(l => l.Color == s.Theme.Ink.Text)];
        int fv = s.Graph.VertexOf(node), lane = 0, completed = 0, outstanding = 0, dependents = 0;
        foreach (GraphEdge e in s.Graph.Edges)
        {
            if (e.From != fv && e.To != fv) continue;
            int laneOf = lane++;
            List<LineCmd> inks = EdgeLines(s, d, e, laneOf);
            Assert.NotEmpty(inks);
            foreach (LineCmd ink in inks)
            {
                bool cased = casings.Any(k => Same(k, ink) && k.Width > ink.Width && at[k] < at[ink]);
                if (e.To == fv) Assert.True(cased, $"{c.Nodes[node].Id} <- {c.Nodes[s.Graph.Vertices[e.From].ContentIndex].Id}: an uncased incoming stroke");
                else Assert.DoesNotContain(casings, k => Same(k, ink));
            }
            if (e.To != fv) dependents++;
            else if (s.Graph.Vertices[e.From].ContentIndex == first) completed++;
            else outstanding++;
        }
        output.WriteLine($"{tree}: {c.Nodes[node].Id} hovered; {completed} completed and {outstanding} outstanding incoming edges cased, {dependents} dependents plain");
        Assert.Equal(1, completed);
        Assert.True(outstanding >= 1);
    }

    [Theory]
    [InlineData(ResearchTree.Technology)]
    [InlineData(ResearchTree.Civics)]
    public void TheSharedStubIntoACard_ShowsAnOutstandingPrerequisitesInk_EvenWhenTheCompletedEdgeComesLast(ResearchTree tree)
    {
        // Every incoming edge of a card ends on the same stub into its top, so the stroke drawn last there is the one the player
        // sees. With one prerequisite complete and others outstanding, that stub must show the outstanding ink (orange when
        // pinned, the prerequisite ink when hovered): the card still waits on research. The completed prerequisite chosen is
        // the one whose edge comes LAST in Graph.Edges, so drawing in Graph.Edges order (the order before 59bac37) would put
        // the completed ink on top.
        ResearchContent c = fx.Content;
        int node = MixedCandidate(tree, withOneOf: false);
        ResearchGraph g0 = ResearchGraph.Build(c, tree);
        int v0 = g0.VertexOf(node);
        GraphEdge lastIn = g0.Edges.Last(e => e.To == v0);
        int completed = g0.Vertices[lastIn.From].ContentIndex;
        WorldState w = WithDone(completed);
        foreach (bool pinned in new[] { true, false })
        {
            var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
            s.Refresh(w);
            s.Tab = tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
            s.Paint(W, H, ApproxTextMeasure.Instance);
            s.FitAll();
            s.Paint(W, H, ApproxTextMeasure.Instance);
            if (pinned) s.Selected = node;
            else
            {
                s.Selected = -1;
                PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(node)];
                s.PointerMove(s.Camera.ToScreenX(p.CenterX, s.Canvas.X), s.Camera.ToScreenY(p.CenterY, s.Canvas.Y));
                Assert.Equal(node, s.Hovered);
            }
            DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
            int fv = s.Graph.VertexOf(node), lane = 0;
            RectD cv = s.Canvas;
            var stubs = new List<(double X0, double Y0, double X1, double Y1)>();
            foreach (GraphEdge e in s.Graph.Edges)
            {
                if (e.From != fv && e.To != fv) continue;
                int at = lane++;
                if (e.To != fv) continue;
                (double X, double Y)[] pts = ResearchTreeLayout.Route(s.Layout, e, at);
                stubs.Add((s.Camera.ToScreenX(pts[^2].X, cv.X), s.Camera.ToScreenY(pts[^2].Y, cv.Y),
                    s.Camera.ToScreenX(pts[^1].X, cv.X), s.Camera.ToScreenY(pts[^1].Y, cv.Y)));
            }
            Assert.True(stubs.Count >= 2);
            Assert.All(stubs, st => Assert.Equal(stubs[0], st));   // one stub, shared by every incoming edge
            (double X0, double Y0, double X1, double Y1) stub = stubs[0];
            List<LineCmd> onStub = [.. d.Commands.OfType<LineCmd>().Where(l => l.Color != s.Theme.Ink.Text
                && Math.Abs(l.X0 - stub.X0) < 1e-6 && Math.Abs(l.Y0 - stub.Y0) < 1e-6
                && Math.Abs(l.X1 - stub.X1) < 1e-6 && Math.Abs(l.Y1 - stub.Y1) < 1e-6)];
            Rgba outstanding = pinned ? s.Theme.Semantic.Pinned : s.Theme.Semantic.Prerequisite;
            Assert.Contains(onStub, l => l.Color == s.Theme.Semantic.Completed);   // the completed edge is drawn there too
            Assert.True(onStub[^1].Color == outstanding,
                $"{c.Nodes[node].Id} ({(pinned ? "pinned" : "hovered")}, {c.Nodes[completed].Id} complete): the stub shows " +
                $"{onStub[^1].Color}, expected the outstanding ink {outstanding}");
            output.WriteLine($"{tree} {(pinned ? "pinned" : "hovered")}: {c.Nodes[node].Id}, {c.Nodes[completed].Id} complete, " +
                $"{stubs.Count} edges on the stub, {onStub.Count} strokes, the last {onStub[^1].Color}");
        }
    }

    [Theory]
    [InlineData("seed_drill_tull")]
    [InlineData("astronomy_records")]
    [InlineData("nuclear_fusion_research")]
    [InlineData("constitutional_monarchy")]
    public void TheFourNestedExpressions_PinnedEdges_RequiresSolid_OneOfDashed_GroupedPartsDotted(string id)
    {
        // The stroke of each incoming edge is its kind's (ProgressionScreen.EdgeDash): a part of a grouped alternative is never
        // drawn with the "one of" dash. D-084 verification round 1 (RT-R1-03, the verifier's V13): the strokes themselves are
        // asserted too, not only their agreement with EdgeDash, so a grouped part drawn solid (as "requires") fails here: a
        // "requires" edge is solid, a "one of" edge dashed and a grouped part dotted, a shorter dash than "one of".
        ResearchContent c = fx.Content;
        int node = c.IndexOfId(id);
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(fx.Session.World);
        s.Tab = c.Nodes[node].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.FitAll();
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = node;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        double zs = Math.Max(0.6, s.Camera.Zoom);
        (double On, double Off)? solid = ProgressionScreen.EdgeDash(EdgeKind.And, zs), dashed = ProgressionScreen.EdgeDash(EdgeKind.Or, zs),
            dotted = ProgressionScreen.EdgeDash(EdgeKind.Grouped, zs);
        Assert.Null(solid);
        Assert.NotNull(dashed);
        Assert.NotNull(dotted);
        Assert.True(dotted!.Value.On < dashed!.Value.On, $"a grouped part's dash ({dotted.Value.On:0.##} on) is not shorter than 'one of''s ({dashed.Value.On:0.##} on)");
        int fv = s.Graph.VertexOf(node), lane = 0, grouped = 0;
        foreach (GraphEdge e in s.Graph.Edges)
        {
            if (e.From != fv && e.To != fv) continue;
            int at = lane++;
            if (e.To != fv) continue;
            List<LineCmd> lines = EdgeLines(s, d, e, at);
            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.Equal(ProgressionScreen.EdgeDash(e.Kind, zs), l.Dash));
            Assert.All(lines, l => Assert.Equal(e.Kind switch { EdgeKind.And => solid, EdgeKind.Or => dashed, _ => dotted }, l.Dash));
            if (e.Kind == EdgeKind.Grouped) grouped++;
        }
        Assert.True(grouped >= 2);
        Assert.NotEqual(dashed, dotted);
    }

    [Fact]
    public void TheCompletedInk_ReadsOnItsCasing_AndStandsApartFromThePinnedOrange_InAllNineEras()
    {
        // A pinned card's completed prerequisite is now drawn in the completed ink over the same dark casing as the orange
        // (ProgressionScreen.PinnedEdge): it must read against that casing (WCAG 2.1 SC 1.4.11, 3:1 for a graphical object)
        // and stay clearly apart from the pinned orange (the separation D-070 measured for the orange, ΔE*ab ≥ 25).
        Assert.Equal(9, Sim.Ui.Theme.EraThemes.All.Count);
        foreach (Sim.Ui.Theme.EraTheme t in Sim.Ui.Theme.EraThemes.All)
        {
            double onCasing = Sim.Ui.Theme.ThemeColor.Contrast(t.Semantic.Completed, t.Ink.Text);
            double apart = Sim.Ui.Theme.ThemeColor.DeltaE(t.Semantic.Completed, t.Semantic.Pinned);
            output.WriteLine($"{t.Era}: completed on casing {onCasing:0.00}:1, ΔE to the pinned orange {apart:0.0}");
            Assert.True(onCasing >= 3.0, $"{t.Era}: completed ink on its casing {onCasing:0.00}:1");
            Assert.True(apart >= ResearchPinTests.PinnedSeparation, $"{t.Era}: completed vs pinned ΔE {apart:0.0}");
        }
    }

    // ------------------------------------------------------------------ §2.2: the long tier gaps

    [Fact]
    public void TheLongestGaps_EveryInTreeEdgeOverTwelveTiers_IsMeasuredAndRouted_AsTheAuditCounts()
    {
        // Audit §2.2: 18 in-tree edges span more than 12 stored-depth tiers; the layout keeps each such gap (every tier between is
        // used), so each runs 13 to 20 drawn tiers through a spine, its prerequisite never on the screen with the dependent.
        ResearchContent c = fx.Content;
        var gaps = new List<(int Gap, string Edge)>();
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchGraph g = ResearchGraph.Build(c, tree);
            TreeLayout L = ResearchTreeLayout.Compute(g);
            foreach (GraphEdge e in g.Edges)
            {
                if (g.Vertices[e.From].External) continue;
                int stored = g.Node(e.To).Depth - g.Node(e.From).Depth;
                if (stored <= 12) continue;
                int drawn = L.Placed[e.To].Column - L.Placed[e.From].Column;
                Assert.Equal(stored, drawn);
                Assert.Equal(6, ResearchTreeLayout.Route(L, e).Length);   // down a spine, never a short elbow
                gaps.Add((drawn, g.Node(e.From).Id + " -> " + g.Node(e.To).Id));
            }
        }
        foreach ((int gap, string edge) in gaps.OrderByDescending(x => x.Gap).ThenBy(x => x.Edge, StringComparer.Ordinal)) output.WriteLine(gap + "  " + edge);
        Assert.Equal(18, gaps.Count);
        Assert.Equal(20, gaps.Max(x => x.Gap));
    }

    [Fact]
    public void ThePinsOffscreenMarker_NamesEachPrerequisiteOutOfView_NearestFirst_WithItsTierDistance()
    {
        // D-084 readability of the long gaps: High-yield dwarf cereals pinned with the camera on it has its four prerequisites
        // above; the marker's first line is unchanged ("4 prerequisites above"), its second names them nearest first with the
        // tiers to each — Canal irrigation 20 tiers up.
        ResearchContent c = fx.Content;
        int node = c.IndexOfId("green_revolution");
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(fx.Session.World);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = node;
        s.Focus(node, jump: true);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        List<int> above = s.PinnedOffscreen(below: false);
        Assert.Equal(c.Nodes[node].PrerequisiteNodes.Count, above.Count);
        Assert.Contains(d.Commands.OfType<TextCmd>(), t => t.Text == ProgressionScreen.OffscreenLabel(above.Count, below: false));
        int pv = s.Graph.VertexOf(node);
        string[] items = [.. above.Select(s.OffscreenItem)];
        for (int k = 0; k < above.Count; k++)
        {
            int tiers = s.Layout.Placed[pv].Column - s.Layout.Placed[s.Graph.VertexOf(above[k])].Column;
            Assert.Equal(c.Nodes[above[k]].Name + " (" + tiers.ToString(CultureInfo.InvariantCulture) + (tiers == 1 ? " tier)" : " tiers)"), items[k]);
        }
        Assert.Equal("Canal irrigation (20 tiers)", s.OffscreenItem(c.IndexOfId("irrigation_canal")));
        string detail = s.OffscreenDetail(ApproxTextMeasure.Instance, below: false, 10_000);
        Assert.Equal(string.Join(" · ", items), detail);
        TextCmd painted = Assert.Single(d.Commands.OfType<TextCmd>(), t => t.Text.StartsWith(items[0], StringComparison.Ordinal));
        Assert.Contains(items[^1], painted.Text, StringComparison.Ordinal);
        // Too little room: as many as fit, then how many more.
        Assert.Equal(items[0] + " · +" + (items.Length - 1).ToString(CultureInfo.InvariantCulture) + " more",
            s.OffscreenDetail(ApproxTextMeasure.Instance, below: false, 1.0));
        output.WriteLine(detail);
    }

    // ------------------------------------------------------------------ §6: Longbow, diagnosed (no content change)

    [Fact]
    public void Longbow_HasOneDirectPrerequisite_ButItsRequiresSectionAlsoPrintsTheStageAndTheAgeSweep()
    {
        // Audit §6, diagnosis only (D-084 DC1-R1 (a)): the graph draws one edge into Longbow (Simple bow) and the REQUIRES rows
        // list that one prerequisite; under them, in the same section and with no heading of their own, the pane prints the
        // research-stage gate (four other node ids) and the Age sweep (every outstanding node of Age IV, by name). Measured on
        // the stepped world with Simple bow complete, where the card itself says "Needs university".
        ResearchContent c = fx.Content;
        int longbow = c.IndexOfId("longbow"), bow = c.IndexOfId("bow_simple");
        Assert.Equal("bow_simple", c.Nodes[longbow].Prerequisite!.Source);
        WorldState w = WithDone(c.IndexOfId("cordage"), bow);
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = longbow;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        int v = s.Graph.VertexOf(longbow);
        Assert.Equal([s.Graph.VertexOf(bow)], s.Graph.Edges.Where(e => e.To == v).Select(e => e.From));
        Assert.Equal([bow], RequiresRows.Of(s.Snapshot!.Nodes[longbow]).Select(r => r.Atom));
        Assert.Equal("Needs university", s.StateLine(longbow));
        RectD pane = s.DetailRect;
        string[] lines = [.. d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right).Select(t => t.Text)];
        int requires = Array.IndexOf(lines, "REQUIRES"), idea = Array.IndexOf(lines, "IDEA");
        Assert.True(requires >= 0 && idea > requires);
        string section = string.Join(" ", lines[(requires + 1)..idea]);
        Assert.Contains("Simple bow", section, StringComparison.Ordinal);
        Assert.Contains("Subtree closed: the research stage is not reached (" + c.Stage.Source.Replace("_", " "), section, StringComparison.Ordinal);
        ResearchQuery.SweepState sweep = s.Snapshot.Sweeps[longbow];
        Assert.True(sweep.Blocked);
        Assert.Equal(4, sweep.CohortAge);   // a node of Age VI waits for Age IV complete (ADR-048)
        Assert.Contains("Age 4 sweep: " + sweep.Outstanding.Length.ToString(CultureInfo.InvariantCulture) + " nodes outstanding:", section, StringComparison.Ordinal);
        output.WriteLine($"Longbow: 1 edge, 1 REQUIRES row; the section also names the stage's atoms ({c.Stage.Source}) and {sweep.Outstanding.Length} Age IV nodes");
    }

    // ------------------------------------------------------------------ §5: the "~N turns" of a civic counts the bank

    private static readonly Regex TurnsOnCard = new(@"~(\d+) turns?\b", RegexOptions.CultureInvariant);
    private static readonly Regex TurnsOnInspector = new(@"about (\d+) turns? at the current rate", RegexOptions.CultureInvariant);

    private static int Parse(Regex rx, string text)
    {
        Match m = rx.Match(text);
        Assert.True(m.Success, "no turns estimate in: " + text);
        return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>The stepped world with the player's Civics Points bank set to <paramref name="bankMilli"/> (the audit identity
    /// Balance = Generated − Spent kept) and, when given, <paramref name="progress"/> on <paramref name="node"/>.</summary>
    private WorldState WithBank(long bankMilli, int node = -1, double progress = 0.0)
    {
        WorldState w = ((WorldState)fx.Session.World).Clone();
        CivicsPointsRow old = ResearchQuery.CivicsPointsAccount(w, Me);
        var row = new CivicsPointsRow(Me, bankMilli, old.SpentMilli + bankMilli, old.SpentMilli);
        int at = -1;
        for (int r = 0; r < w.CivicsPoints.Count; r++) if (w.CivicsPoints[r].Polity.Value == Me.Value) at = r;
        if (at >= 0) w.CivicsPoints[at] = row;
        else w.CivicsPoints.Add(row);
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
        if (node >= 0 && progress > 0)
        {
            ResearchNodeId key = fx.Content.Nodes[node].Key;
            Keep(w.ResearchProgress, r => !(r.Polity.Value == Me.Value && r.Node.Value == key.Value));
            w.ResearchProgress.Add(new ResearchProgressRow(Me, key, progress));
        }
        return w;
    }

    /// <summary>An available civic on the stepped world with no Idea and no foreign exposure (nothing but CP can pay it), not
    /// the target, and at least <paramref name="minTurns"/> turns of CP from completion at the current rate.</summary>
    private int AvailableCivic(double minTurns)
    {
        ResearchSnapshot s = ResearchSnapshot.Build(fx.Session.World, fx.Content, Me, NextDt(fx.Session.World));
        for (int i = 0; i < fx.Content.Nodes.Count; i++)
        {
            ResearchNodeView v = s.Nodes[i];
            if (v.Tree != ResearchTree.Civics || v.State != NodeState.Available || v.Eurekas.Count > 0 || v.ExposureOffered > 0) continue;
            if (s.TechnologyRoute.Current == v.Key || s.CivicsRoute.Current == v.Key) continue;   // a funded route node shows no "about N turns"
            if (v.EffectiveCost - v.Progress >= minTurns * s.CivicsPerTurn) return i;
        }
        throw new InvalidOperationException("no available civic");
    }

    /// <summary>Keeps only the rows of <paramref name="table"/> that satisfy <paramref name="keep"/>, in their order.</summary>
    private static void Keep<T>(Table<T> table, Func<T, bool> keep) where T : unmanaged
    {
        var rows = new List<T>();
        for (int r = 0; r < table.Count; r++) if (keep(table[r])) rows.Add(table[r]);
        table.Clear();
        foreach (T row in rows) table.Add(row);
    }

    private static double NextDt(IReadOnlyWorldState w) => ActionQueryContext.ForNextStep(w, Era).NextDtYears!.Value;

    /// <summary>The three estimates the player is shown for <paramref name="node"/> on <paramref name="w"/>: the tree card's
    /// state line, the selected node's pane and the inspector card (InfoQuery).</summary>
    private (int Card, int Pane, int Inspector) Estimates(WorldState w, int node)
    {
        ResearchContent c = fx.Content;
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        s.Tab = c.Nodes[node].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        int card = Parse(TurnsOnCard, s.StateLine(node));
        s.Selected = node;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        // The pane's COST block (its wrapped lines in paint order): "progress / cost unit  ·  ~N turns at the current rate".
        RectD pane0 = s.DetailRect;
        string panel = string.Join(" ", d.Commands.OfType<TextCmd>().Where(t => t.X >= pane0.X && t.X <= pane0.Right).Select(t => t.Text));
        int cost = panel.IndexOf("Effective cost", StringComparison.Ordinal);
        Assert.True(cost >= 0, "no COST block in the pane: " + panel);
        int pane = Parse(TurnsOnCard, panel[cost..]);
        string status = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Node(c.Nodes[node].Key), ActionQueryContext.ForNextStep(w, Era),
            actions: ImmutableArray<ActionDescriptor>.Empty).StatusLine;
        int inspector = Parse(TurnsOnInspector, status);
        return (card, pane, inspector);
    }

    /// <summary>The End Turns the engine takes to complete <paramref name="node"/> when it is ordered on <paramref name="w"/>:
    /// the player's target on its lane is replaced by the node, then research-only steps until it is complete.</summary>
    private int EngineTurns(WorldState w, int node, int limit = 60)
    {
        ResearchContent c = fx.Content;
        WorldState x = w.Clone();
        ResearchTree lane = c.Nodes[node].Tree;
        Keep(x.ResearchTargets, r => !(r.Polity.Value == Me.Value && c.IndexOf(r.Node) is int i and >= 0 && c.Nodes[i].Tree == lane));
        x.ResearchTargets.Add(new ResearchTargetRow(Me, c.Nodes[node].Key));
        var research = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]);
        for (int turn = 1; turn <= limit; turn++)
        {
            x = research.Step(x);
            if (ResearchQuery.IsCompleted(x, Me, c.Nodes[node].Key)) return turn;
        }
        throw new InvalidOperationException("not complete after " + limit + " turns");
    }

    [Fact]
    public void ACivicsEstimate_CountsTheBank_OnTheCardThePaneAndTheInspector_AsTheEngineSpendsIt()
    {
        // Audit §5: the three surfaces divided EffectiveCost − Progress by the CP a turn and ignored the banked CP, which the
        // engine spends on the civic it funds (ResearchSystem 3b: accrue, then draw min(balance, remaining)). Engine rule, so
        // the estimate is the End Turns until completion: max(1, ceil((remaining − bank) / CP a turn)) — the bank is spent
        // inside the End Turn step, so a covering bank completes the civic at the FIRST End Turn (1), never in 0.
        ResearchContent c = fx.Content;
        double g = ResearchSnapshot.Build(fx.Session.World, c, Me, NextDt(fx.Session.World)).CivicsPerTurn;
        Assert.True(g > 0);
        int civic = AvailableCivic(4.0);
        ResearchNodeView v = ResearchSnapshot.Build(fx.Session.World, c, Me, NextDt(fx.Session.World)).Nodes[civic];
        double remaining = v.EffectiveCost - v.Progress;
        output.WriteLine($"stepped world: bank {ResearchQuery.CivicsPointsBalance(fx.Session.World, Me):0.###} CP, {g:0.###} CP a turn; " +
            $"{c.Nodes[civic].Id}: remaining {remaining:0.###} CP");
        foreach ((double bank, int turns) in new[] { (remaining + 0.25 * g, 1), (remaining - 0.5 * g, 1), (remaining - 1.5 * g, 2), (remaining - 2.5 * g, 3) })
        {
            WorldState w = WithBank((long)Math.Round(bank * 1000.0), civic);
            (int card, int pane, int inspector) = Estimates(w, civic);
            int engine = EngineTurns(w, civic);
            output.WriteLine($"bank {bank:0.###}: card {card}, pane {pane}, inspector {inspector}, engine {engine}");
            Assert.Equal(turns, engine);
            Assert.Equal((engine, engine, engine), (card, pane, inspector));
        }
        // Progress plus bank above the cost: one End Turn.
        WorldState part = WithBank((long)Math.Round(0.6 * remaining * 1000.0), civic, v.Progress + 0.5 * remaining);
        Assert.Equal((1, 1, 1), Estimates(part, civic));
        Assert.Equal(1, EngineTurns(part, civic));
    }

    [Fact]
    public void ATechnologyEstimate_IsNotReducedByTheCivicsBank()
    {
        // Technology has no RP bank (ADR-051's reserve is absent from the shipped tuning): the CP bank never shortens it.
        ResearchContent c = fx.Content;
        Assert.Null(c.Tuning.Reserve);
        ResearchSnapshot s0 = ResearchSnapshot.Build(fx.Session.World, c, Me, NextDt(fx.Session.World));
        int tech = -1;
        for (int i = 0; i < c.Nodes.Count && tech < 0; i++)
            if (s0.Nodes[i] is { Tree: ResearchTree.Technology, State: NodeState.Available } tv && tv.Eurekas.Count == 0 && tv.ExposureOffered == 0
                && tv.EffectiveCost - tv.Progress > 2 * s0.PointsPerTurn && s0.TechnologyRoute.Current != tv.Key && s0.CivicsRoute.Current != tv.Key)
                tech = i;
        Assert.True(tech >= 0);
        ResearchNodeView v = s0.Nodes[tech];
        int expected = (int)Math.Ceiling((v.EffectiveCost - v.Progress) / s0.PointsPerTurn);
        WorldState rich = WithBank(1_000_000_000L);   // a million CP banked
        Assert.Equal((expected, expected, expected), Estimates(rich, tech));
    }

    /// <summary>The selected node's pane on <paramref name="w"/> from its COST block ("Effective cost") to the ENABLES heading: the
    /// progress line with its estimate, the action and the route.</summary>
    private string PaneCostBlock(WorldState w, int node)
    {
        ResearchContent c = fx.Content;
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        s.Tab = c.Nodes[node].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        s.Selected = node;
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD pane = s.DetailRect;
        string panel = string.Join(" ", d.Commands.OfType<TextCmd>().Where(t => t.X >= pane.X && t.X <= pane.Right).Select(t => t.Text));
        int cost = panel.IndexOf("Effective cost", StringComparison.Ordinal), enables = panel.IndexOf("ENABLES", StringComparison.Ordinal);
        Assert.True(cost >= 0 && enables > cost, "no COST block in the pane: " + panel);
        return panel[cost..enables];
    }

    [Fact]
    public void ACivicNotYetOpen_LockedPartialOrAWaitingDestination_ShowsNoEstimate_ForItsLaneFundsItsRouteFirst()
    {
        // D-084 verification round 1 (RT-R1-01): the pane gave every incomplete civic the bank-aware estimate. A civic that is
        // not open cannot be funded at the next End Turn: ordered, it becomes the lane's destination and the lane funds its
        // route first, one civic per End Turn with overflow off, spending the bank on those (ResearchSystem 1b, 3b). The
        // stepped world banks 715.213 CP, which covered the own cost of 15 locked civics, so their panes read "~1 turns"
        // though no locked civic completes at the first End Turn — Gift exchange takes 2. Now no civic that is not open gets a
        // figure (its pane says so), whether it is locked, partial or the destination waiting on its route; a technology that
        // is not open keeps the figure it always had, its own turns at the RP rate (no bank).
        ResearchContent c = fx.Content;
        ResearchSnapshot s0 = ResearchSnapshot.Build(fx.Session.World, c, Me, NextDt(fx.Session.World));
        Assert.True(s0.TwoLanes && s0.Routing && !c.Tuning.ResearchOverflow);
        Assert.True(s0.CivicsBalance > 0);
        int civicsNotOpen = 0, coveredByTheBank = 0, techNotOpen = 0;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchNodeView v = s0.Nodes[i];
            if (v.State == NodeState.Completed || v.Available) { Assert.False(s0.NoEstimateUntilOpen(v)); continue; }
            if (v.Tree == ResearchTree.Civics)
            {
                civicsNotOpen++;
                if (v.EffectiveCost - v.Progress <= s0.CivicsBalance) coveredByTheBank++;
                Assert.True(s0.NoEstimateUntilOpen(v));
                Assert.Null(s0.TurnsOf(v));
            }
            else
            {
                techNotOpen++;
                Assert.False(s0.NoEstimateUntilOpen(v));
                Assert.Equal((long)Math.Ceiling(Math.Max(0.0, v.EffectiveCost - v.Progress) / s0.PointsPerTurn), s0.TurnsOf(v));
            }
        }
        output.WriteLine($"stepped world: {civicsNotOpen} civics not open ({coveredByTheBank} whose own cost the bank covers), {techNotOpen} technologies not open");
        Assert.True(coveredByTheBank > 0);

        // Gift exchange, locked (it needs Council of elders): the engine takes 2 End Turns, so the old "~1" was unreachable.
        int gift = c.IndexOfId("gift_exchange"), council = c.IndexOfId("kin_council");
        WorldState locked = ((WorldState)fx.Session.World).Clone();
        Assert.Equal(NodeState.Locked, s0.Nodes[gift].State);
        Assert.Equal([council], c.Nodes[gift].PrerequisiteNodes);
        Assert.True(s0.Nodes[gift].EffectiveCost - s0.Nodes[gift].Progress < s0.CivicsBalance);
        Assert.Equal(2, EngineTurns(locked, gift));
        // Partial: progress kept but not open. The waiting destination: ordered, its route not yet done.
        WorldState partial = WithBank(ResearchQuery.CivicsPointsAccount(fx.Session.World, Me).BalanceMilli, gift, 25.0);
        WorldState destination = ((WorldState)fx.Session.World).Clone();
        Keep(destination.ResearchTargets, r => !(r.Polity.Value == Me.Value && c.IndexOf(r.Node) is int i and >= 0 && c.Nodes[i].Tree == ResearchTree.Civics));
        destination.ResearchTargets.Add(new ResearchTargetRow(Me, c.Nodes[gift].Key));
        foreach ((string label, WorldState w, NodeState state) in new[] { ("locked", locked, NodeState.Locked), ("partial", partial, NodeState.Partial),
                     ("waiting destination", destination, NodeState.CurrentTarget) })
        {
            ResearchSnapshot s = ResearchSnapshot.Build(w, c, Me, NextDt(w));
            Assert.Equal(state, s.Nodes[gift].State);
            Assert.False(s.Nodes[gift].Available);
            Assert.Null(s.TurnsOf(s.Nodes[gift]));
            string block = PaneCostBlock(w, gift);
            output.WriteLine($"{label}: {block}");
            Assert.DoesNotMatch(TurnsOnCard, block);
            Assert.Contains(ProgressionScreen.NoEstimateYet, block, StringComparison.Ordinal);
            Assert.Contains("Civics Points banked", block, StringComparison.Ordinal);   // D-070 R4: the bank is still shown
        }
        // An open civic keeps its figure (the engine's End Turns; ACivicsEstimate_… pins the value): Council of elders itself.
        Assert.True(s0.Nodes[council].Available);
        Assert.Matches(TurnsOnCard, PaneCostBlock(locked, council));
        Assert.DoesNotContain(ProgressionScreen.NoEstimateYet, PaneCostBlock(locked, council), StringComparison.Ordinal);
    }

    [Fact]
    public void OneTurn_IsWrittenSingular_OnTheCardThePaneAndTheInspector_AndMoreArePlural()
    {
        // D-084 verification round 1 (RT-R1-06 b): with the bank counted, an estimate of one End Turn is common, and the three
        // surfaces wrote "~1 turns" and "about 1 turns". One turn is now singular on all three (InfoQuery.TurnsPhrase); more
        // stay plural. Council of elders on the stepped world: the 715.213 CP bank covers it, so the engine completes it at the
        // first End Turn; with the bank emptied it takes several.
        ResearchContent c = fx.Content;
        int kin = c.IndexOfId("kin_council");
        foreach ((WorldState w, bool one) in new[] { ((WorldState)fx.Session.World, true), (WithBank(0L), false) })
        {
            var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
            s.Refresh(w);
            s.Tab = TreeTab.Civics;
            s.Paint(W, H, ApproxTextMeasure.Instance);
            long n = s.Snapshot!.TurnsOf(s.Snapshot.Nodes[kin])!.Value;
            Assert.Equal(one, n == 1);
            string words = one ? "1 turn" : n.ToString(CultureInfo.InvariantCulture) + " turns";
            string card = s.StateLine(kin), block = PaneCostBlock(w, kin);
            string status = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Node(c.Nodes[kin].Key), ActionQueryContext.ForNextStep(w, Era),
                actions: ImmutableArray<ActionDescriptor>.Empty).StatusLine;
            output.WriteLine($"{(one ? "banked" : "bank emptied")}: card '{card}' | inspector '{status}'");
            Assert.Equal("Available · ~" + words, card);
            Assert.Contains("~" + words + " at the current rate", block, StringComparison.Ordinal);
            Assert.EndsWith("(about " + words + " at the current rate).", status, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"(?<!\d)1 turns", card + " " + block + " " + status);
        }
        Assert.Equal(1, EngineTurns((WorldState)fx.Session.World, kin));
    }

    // ------------------------------------------------------------------ verification round 1 (RT-R1-03): claims the mutants showed unpinned

    [Fact]
    public void ADependentsRoute_NeverSharesASegmentWithAnIncomingRoute_SoWhereDependentsFallInTheDrawOrderShowsNothing()
    {
        // The verifier's V06 (dependents drawn last instead of first) changes no pixel: a focus's dependent routes leave from
        // under its card (ResearchTreeLayout.Route: down to the row gap below it, then on), its incoming routes arrive from
        // above it, so no dependent segment lies on an incoming one. This pins that geometry for every card of both trees,
        // with the lanes the screen gives a focus's edges; it is why the draw order is only claimed among incoming edges.
        ResearchContent c = fx.Content;
        static bool Shared(((double X, double Y) A, (double X, double Y) B) s, ((double X, double Y) A, (double X, double Y) B) t)
        {
            static double Run(double a0, double a1, double b0, double b1) => Math.Min(Math.Max(a0, a1), Math.Max(b0, b1)) - Math.Max(Math.Min(a0, a1), Math.Min(b0, b1));
            if (s.A.Y == s.B.Y && t.A.Y == t.B.Y && s.A.Y == t.A.Y) return Run(s.A.X, s.B.X, t.A.X, t.B.X) > 1e-9;
            if (s.A.X == s.B.X && t.A.X == t.B.X && s.A.X == t.A.X) return Run(s.A.Y, s.B.Y, t.A.Y, t.B.Y) > 1e-9;
            return false;
        }
        int foci = 0, sharedAmongIncoming = 0;
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchGraph g = ResearchGraph.Build(c, tree);
            TreeLayout L = ResearchTreeLayout.Compute(g);
            for (int f = 0; f < g.OwnCount; f++)
            {
                foci++;
                var ins = new List<(double X, double Y)[]>();
                var outs = new List<(double X, double Y)[]>();
                int lane = 0;
                foreach (GraphEdge e in g.Edges)
                {
                    if (e.From != f && e.To != f) continue;
                    (double X, double Y)[] pts = ResearchTreeLayout.Route(L, e, lane++);
                    if (pts.Length > 0) (e.To == f ? ins : outs).Add(pts);
                }
                foreach ((double X, double Y)[] a in ins)
                    foreach ((double X, double Y)[] b in outs)
                        for (int i = 0; i + 1 < a.Length; i++)
                            for (int j = 0; j + 1 < b.Length; j++)
                                Assert.False(Shared((a[i], a[i + 1]), (b[j], b[j + 1])), $"{g.Node(f).Id}: a dependent route shares a segment with an incoming one");
                bool any = false;
                for (int x = 0; x < ins.Count && !any; x++)
                    for (int y = x + 1; y < ins.Count && !any; y++)
                        for (int i = 0; i + 1 < ins[x].Length && !any; i++)
                            for (int j = 0; j + 1 < ins[y].Length && !any; j++)
                                any = Shared((ins[x][i], ins[x][i + 1]), (ins[y][j], ins[y][j + 1]));
                if (any) sharedAmongIncoming++;
            }
        }
        output.WriteLine($"{foci} cards; incoming routes share a segment with each other at {sharedAmongIncoming} (there the completed-then-outstanding order matters)");
        Assert.Equal(1005, foci);
        Assert.True(sharedAmongIncoming > 0);
    }

    [Fact]
    public void SeedDrillTull_Locked_ItsLockLineNamesTheAlternative_AndSaysAnotherOneExists()
    {
        // The verifier's V07: Seed drill (Tull) needs Seed drill OR (Ard plough AND Padded horse collar). Locked, its card names
        // the alternative that completes the choice on its own and, with " / ...", that another one exists — the grouped one.
        // Without it the line reads "Needs one of: Seed drill", as if that were the only way.
        ResearchContent c = fx.Content;
        int node = c.IndexOfId("seed_drill_tull"), sd = c.IndexOfId("seed_drill"), pa = c.IndexOfId("plough_ard"), hc = c.IndexOfId("horse_collar");
        foreach (int[] done in new int[][] { [], [pa], [hc] })
        {
            WorldState w = ((WorldState)fx.Session.World).Clone();
            Keep(w.ResearchCompleted, r => !(r.Polity.Value == Me.Value && (r.Node.Value == c.Nodes[sd].Key.Value
                || r.Node.Value == c.Nodes[pa].Key.Value || r.Node.Value == c.Nodes[hc].Key.Value)));
            foreach (int i in done) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[i].Key));
            var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
            s.Refresh(w);
            s.Tab = TreeTab.Technology;
            Assert.True(s.Snapshot!.Nodes[node].Lock.HasFlag(LockReason.MissingPrerequisites));
            string line = s.LockLine(node);
            output.WriteLine($"{(done.Length == 0 ? "nothing" : c.Nodes[done[0]].Id)} complete: {line}");
            Assert.StartsWith("Needs one of: " + c.Nodes[sd].Name, line, StringComparison.Ordinal);
            Assert.EndsWith(" / ...", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePane_ShowsTheBank_BesideACivicsEstimate_AndNotForATechnology()
    {
        // The verifier's V08: D-070 R4 shows the bank wherever Civics is shown, and the pane shows it beside the estimate that
        // counts it — "Civics Points banked 715.2 CP" in an open civic's COST block. A technology's has no such row (no bank).
        ResearchContent c = fx.Content;
        WorldState w = (WorldState)fx.Session.World;
        string bank = "Civics Points banked " + ProgressionScreen.CivicsBankFigure(ResearchQuery.CivicsPointsBalance(w, Me));
        string civic = PaneCostBlock(w, c.IndexOfId("kin_council"));
        output.WriteLine(civic);
        Assert.Contains(bank, civic, StringComparison.Ordinal);
        Assert.True(civic.IndexOf(bank, StringComparison.Ordinal) < civic.IndexOf("turn at the current rate", StringComparison.Ordinal));
        ResearchSnapshot s0 = ResearchSnapshot.Build(w, c, Me, NextDt(w));
        int tech = -1;
        for (int i = 0; i < c.Nodes.Count && tech < 0; i++)
            if (s0.Nodes[i] is { Tree: ResearchTree.Technology, State: NodeState.Available }) tech = i;
        Assert.True(tech >= 0);
        Assert.DoesNotContain("Civics Points banked", PaneCostBlock(w, tech), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSharedEstimate_PaysACivicAtItsLanesCivicsRate_AndATechnologyAtTheResearchRate()
    {
        // The verifier's V24: ResearchSnapshot.TurnsOf, behind the card's and the pane's "~N turns", divides by the lane's own
        // rate — Civics Points for a civic in the two-lane model, Research Points for a technology (ADR-043). On the stepped world
        // the two rates nearly coincide (CP is the RP base function), so the snapshot is rebuilt with the RP rate three times
        // the CP rate and no bank, everything else as built.
        ResearchSnapshot s = ResearchSnapshot.Build(fx.Session.World, fx.Content, Me, NextDt(fx.Session.World));
        Assert.True(s.TwoLanes && s.CivicsPerTurn > 0);
        var fast = new ResearchSnapshot
        {
            Turn = s.Turn, Nodes = s.Nodes, StageReached = s.StageReached, StageExpression = s.StageExpression,
            PointsPerTurn = 3.0 * s.CivicsPerTurn, TargetIndex = s.TargetIndex, CompletedTechnology = s.CompletedTechnology,
            CompletedCivics = s.CompletedCivics, TwoLanes = true, CivicsTargetIndex = s.CivicsTargetIndex, CivicsBalance = 0.0,
            CivicsPerTurn = s.CivicsPerTurn, Reserve = s.Reserve, Routing = s.Routing, TechnologyRoute = s.TechnologyRoute,
            CivicsRoute = s.CivicsRoute, Sweeps = s.Sweeps,
        };
        int civic = AvailableCivic(4.0), tech = -1;
        for (int i = 0; i < s.Nodes.Count && tech < 0; i++)
            if (s.Nodes[i] is { Tree: ResearchTree.Technology, State: NodeState.Available } tv && tv.EffectiveCost - tv.Progress > 4.0 * fast.PointsPerTurn)
                tech = i;
        Assert.True(tech >= 0);
        ResearchNodeView cv = fast.Nodes[civic], t = fast.Nodes[tech];
        long civicTurns = (long)Math.Ceiling((cv.EffectiveCost - cv.Progress) / s.CivicsPerTurn);
        output.WriteLine($"{fx.Content.Nodes[civic].Id}: {civicTurns} turns at {s.CivicsPerTurn:0.###} CP; {fx.Content.Nodes[tech].Id} at {fast.PointsPerTurn:0.###} RP");
        Assert.Equal(civicTurns, fast.TurnsOf(cv));
        Assert.NotEqual(civicTurns, (long)Math.Ceiling((cv.EffectiveCost - cv.Progress) / fast.PointsPerTurn));   // the RP rate would say otherwise
        Assert.Equal((long)Math.Ceiling((t.EffectiveCost - t.Progress) / fast.PointsPerTurn), fast.TurnsOf(t));
    }
}
