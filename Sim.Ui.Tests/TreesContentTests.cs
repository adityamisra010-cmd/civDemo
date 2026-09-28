using System.Text.Json.Nodes;
using Sim.Ui.Trees;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// THE TREES — CONTENT → GRAPH → LAYOUT (docs/architecture/the-trees-ui.md §3–§5).
/// Loaded from the SAME ui-content/trees files the game ships (copied to the test output),
/// and from edited copies of them, so "the graph is data" is proven by editing data.
/// </summary>
public class TreesContentTests
{
    internal static string Dir => TreesContentLoader.DefaultDirectory;
    internal static string ReadShipped(string file) => File.ReadAllText(Path.Combine(Dir, file));

    internal static TreesContentSet Shipped()
    {
        ContentLoadResult r = TreesContentLoader.LoadDirectory(Dir);
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        return r.Content!;
    }

    internal static ContentLoadResult LoadEdited(Action<JsonNode> editTrees, Action<JsonNode>? editAges = null)
    {
        JsonNode trees = JsonNode.Parse(ReadShipped(TreesContentLoader.TreesFile))!;
        JsonNode ages = JsonNode.Parse(ReadShipped(TreesContentLoader.AgesFile))!;
        editTrees(trees);
        editAges?.Invoke(ages);
        return TreesContentLoader.LoadFromJson(trees.ToJsonString(), ages.ToJsonString(),
            ReadShipped(TreesContentLoader.GalleryFile), ReadShipped(TreesContentLoader.AnimationsFile));
    }

    internal static DemoStateSource ShippedState(TreesContentSet content)
    {
        (DemoStateSource? s, IReadOnlyList<ContentDiagnostic> d) = DemoStateSource.LoadFile(Path.Combine(Dir, TreesContentLoader.DemoStateFile), content);
        Assert.True(s is not null, string.Join("\n", d));
        return s!;
    }

    // --- loading --------------------------------------------------------------------------

    [Fact]
    public void ShippedContent_Loads_WithTheSevenLensesTheTwelveNodeTypesAndTheRelationTaxonomy()
    {
        TreesContentSet c = Shipped();
        Assert.Equal(["KNOWLEDGE", "TECHNIQUES", "INSTITUTIONS", "INFRASTRUCTURE", "INDUSTRY", "MILITARY", "APPLICATIONS"],
            c.Trees.LensesInOrder().Select(l => l.Id).ToArray());
        Assert.Equal(["knowledge", "technique", "institution", "infrastructure", "industry", "military-capability",
                      "unit", "doctrine", "application", "milestone", "policy", "capability"],
            c.Trees.NodeTypes.Select(t => t.Id).ToArray());
        string[] required = ["prerequisite", "enables", "feeds", "requires", "produces", "institutionalizes",
                             "diffusesTo", "derivedFrom", "improves", "supports", "unlocks", "dependsOn"];
        foreach (string k in required) Assert.Contains(c.Trees.RelationKinds, x => x.Id == k);
        Assert.True(c.Trees.Placeholder, "the shipped graph must be flagged placeholder");
        Assert.All(c.Trees.Nodes, n => Assert.True(n.Placeholder, $"{n.Id} is demo content and must say so"));
        Assert.All(c.Ages.Ages, a => Assert.Matches("^AGE_[0-9]{2}$", a.Id));
    }

    [Fact]
    public void Nodes_AreData_AddingAndRenamingANodeInJson_ChangesTheGraph()
    {
        ContentLoadResult r = LoadEdited(t =>
        {
            JsonArray nodes = t["nodes"]!.AsArray();
            nodes.Add(new JsonObject
            {
                ["id"] = "knowledge.test-node", ["domain"] = "KNOWLEDGE", ["type"] = "knowledge", ["name"] = "Test Node",
                ["prerequisites"] = new JsonArray("knowledge.engineering"),
            });
            nodes.First(n => (string?)n!["id"] == "knowledge.mathematics")!["name"] = "Renamed";
        });
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var g = new TreeGraph(r.Content!.Trees);
        Assert.Equal("Renamed", g.Node(g.IndexOf("knowledge.mathematics")).Name);
        int test = g.IndexOf("knowledge.test-node");
        Assert.Contains(g.Upstream(test), i => g.Node(i).Id == "knowledge.mathematics");
    }

    [Fact]
    public void TypedRelationships_InlineFieldsAndTheEdgeList_ResolveIntoOneTypedEdgeList()
    {
        var g = new TreeGraph(Shipped().Trees);
        GraphEdge Find(string from, string to, string kind) =>
            g.Edges.Single(e => g.Node(e.From).Id == from && g.Node(e.To).Id == to && e.Kind.Id == kind);

        // Inline "prerequisites" on Engineering → "Mathematics is a prerequisite of Engineering".
        GraphEdge pre = Find("knowledge.mathematics", "knowledge.engineering", "prerequisite");
        Assert.Equal("prerequisites", pre.Origin);
        // Inline "dependsOn" on Factory is a REVERSE kind: authored Factory→Machine Tools, flows Machine Tools→Factory.
        GraphEdge dep = Find("industry.factory", "industry.machine-tools", "dependsOn");
        Assert.Equal(g.IndexOf("industry.machine-tools"), dep.FlowFrom);
        Assert.Equal(g.IndexOf("industry.factory"), dep.FlowTo);
        // The edge list carries the rest, with its note.
        GraphEdge inst = Find("knowledge.engineering", "institution.engineering-university", "institutionalizes");
        Assert.Equal("edges", inst.Origin);
        Assert.Contains("DEMO", inst.Note);
        // related* lists become undirected relatedTo edges.
        Assert.False(Find("institution.engineering-university", "industry.machine-tools", "relatedTo").Directed);
        // At least eight distinct kinds are exercised by the demo graph.
        Assert.True(g.Edges.Select(e => e.Kind.Id).Distinct().Count() >= 8);
    }

    [Fact]
    public void TheDemoChain_IsOneDirectedPath_ThatCrossesTheLenses()
    {
        // Task §22: Mathematics → Engineering → Engineering University → Engineering
        // personnel → Engineering knowledge → Machine Tools → Factory → Industrial
        // production → Automobile. Proven as the graph's own shortest directed path.
        var g = new TreeGraph(Shipped().Trees);
        int[] path = g.Path(g.IndexOf("knowledge.mathematics"), g.IndexOf("application.automobile"));
        Assert.Equal(
            ["knowledge.mathematics", "knowledge.engineering", "institution.engineering-university",
             "capability.engineering-personnel", "knowledge.engineering-knowledge", "industry.machine-tools",
             "industry.factory", "capability.industrial-production", "application.automobile"],
            path.Select(i => g.Node(i).Id).ToArray());
        Assert.True(path.Select(i => g.Node(i).Domain).Distinct().Count() >= 4, "the chain must cross at least four lenses");
        // Not seven isolated trees: every lens has an edge to another lens.
        foreach (LensDef l in g.Content.Lenses)
            Assert.Contains(g.Edges, e => g.IsCrossLens(e) && (g.Node(e.From).Domain == l.Id || g.Node(e.To).Domain == l.Id));
    }

    [Fact]
    public void Queries_UpstreamDownstreamAndFeedback_AreDeterministicAndLayeringAware()
    {
        var g = new TreeGraph(Shipped().Trees);
        int eu = g.IndexOf("institution.engineering-university");
        string[] up = g.Upstream(eu).Select(i => g.Node(i).Id).ToArray();
        Assert.Contains("knowledge.mathematics", up);
        Assert.Contains("institution.university", up);
        // The feedback edge (Engineering knowledge IMPROVES the university) is not layering,
        // so it never makes the knowledge its own ancestor.
        Assert.DoesNotContain("knowledge.engineering-knowledge", up);
        Assert.Contains("knowledge.engineering-knowledge", g.Upstream(eu, layeringOnly: false).Select(i => g.Node(i).Id));
        Assert.Equal(g.Upstream(eu), g.Upstream(eu));
        Assert.True(g.Upstream(eu).SequenceEqual(g.Upstream(eu).OrderBy(x => x)), "closures are sorted");
        Assert.Contains("application.automobile", g.Downstream(eu).Select(i => g.Node(i).Id));
    }

    // --- validation ------------------------------------------------------------------------

    [Fact]
    public void Validation_UnknownReferencesTyposAndLayeringCycles_AreReportedWithAPath()
    {
        ContentLoadResult badRef = LoadEdited(t => t["edges"]!.AsArray().Add(new JsonObject { ["from"] = "knowledge.nope", ["to"] = "knowledge.mathematics", ["kind"] = "enables" }));
        Assert.False(badRef.Ok);
        Assert.Contains(badRef.Errors, e => e.Path.StartsWith("edges[") && e.Message.Contains("knowledge.nope"));

        ContentLoadResult typo = LoadEdited(t => t["nodes"]![0]!["nmae"] = "x");
        Assert.False(typo.Ok);
        Assert.Contains(typo.Errors, e => e.Message.Contains("nmae"));

        ContentLoadResult cycle = LoadEdited(t => t["edges"]!.AsArray().Add(new JsonObject
            { ["from"] = "application.automobile", ["to"] = "knowledge.mathematics", ["kind"] = "enables" }));
        Assert.False(cycle.Ok);
        Assert.Contains(cycle.Errors, e => e.Message.Contains("cycle") && e.Message.Contains("knowledge.mathematics"));

        // The SAME back-link as a non-layering kind is legal feedback.
        ContentLoadResult feedback = LoadEdited(t => t["edges"]!.AsArray().Add(new JsonObject
            { ["from"] = "application.automobile", ["to"] = "knowledge.mathematics", ["kind"] = "improves" }));
        Assert.True(feedback.Ok, string.Join("\n", feedback.Errors));

        ContentLoadResult glyph = LoadEdited(t => t["nodeTypes"]![0]!["base"] = "Pentagon");
        Assert.False(glyph.Ok);
        Assert.Contains(glyph.Errors, e => e.Path == "nodeTypes[0].base" && e.Message.Contains("Hexagon"));

        ContentLoadResult state = LoadEdited(t => t["stateSets"]![0]!["states"]!.AsArray().Add("no-such-state"));
        Assert.False(state.Ok);

        ContentLoadResult age = LoadEdited(_ => { }, a => a["ages"]![1]!["order"] = 0);
        Assert.False(age.Ok);
        Assert.Contains(age.Errors, e => e.Message.Contains("strictly ordered"));
    }

    [Fact]
    public void DemoState_StatesOutsideTheTypesSet_AndAgeRegression_AreRejected()
    {
        TreesContentSet c = Shipped();
        JsonNode state = JsonNode.Parse(ReadShipped(TreesContentLoader.DemoStateFile))!;
        // Knowledge uses the research-only set: "mature" is not allowed.
        state["steps"]![0]!["nodes"]![0]!["state"] = "mature";
        (DemoStateSource? s1, IReadOnlyList<ContentDiagnostic> d1) = DemoStateSource.Load(state.ToJsonString(), c);
        Assert.Null(s1);
        Assert.Contains(d1, x => x.Severity == DiagnosticSeverity.Error && x.Message.Contains("research"));

        JsonNode back = JsonNode.Parse(ReadShipped(TreesContentLoader.DemoStateFile))!;
        back["steps"]![3]!["ages"]!["current"] = "AGE_02";
        (DemoStateSource? s2, IReadOnlyList<ContentDiagnostic> d2) = DemoStateSource.Load(back.ToJsonString(), c);
        Assert.Null(s2);
        Assert.Contains(d2, x => x.Message.Contains("forward only"));
    }

    [Fact]
    public void DemoState_StepsMergeOverThePreviousStep_AndSequencesAdvance()
    {
        TreesContentSet c = Shipped();
        DemoStateSource s = ShippedState(c);
        Assert.Equal(4, s.StepCount);
        Assert.Equal("researching", s.Trees.Status("institution.engineering-university")!.StateId);
        Assert.Equal("mature", s.Trees.Status("technique.masonry")!.StateId);
        long seq0 = s.Trees.Sequence;
        s.Next();
        Assert.Equal("researched", s.Trees.Status("institution.engineering-university")!.StateId);
        Assert.Equal("mature", s.Trees.Status("technique.masonry")!.StateId);   // carried over
        Assert.True(s.Trees.Sequence > seq0);
        s.SetStep(3);
        Assert.Equal("AGE_04", s.Ages.CurrentAgeId);
        s.SetStep(99);
        Assert.Equal(3, s.Step);
        // Every node in the shipped script is in a state its type allows.
        foreach (NodeStatus n in s.Trees.Nodes)
            Assert.Contains(n.StateId, c.Trees.StatesFor(c.Trees.Nodes.First(x => x.Id == n.NodeId).Type));
    }

    // --- layout -----------------------------------------------------------------------------

    [Fact]
    public void Layout_IsDeterministic_LayeringFlowsLeftToRight_AndEveryNodeSitsInItsLensBand()
    {
        var g = new TreeGraph(Shipped().Trees);
        TreeLayoutResult a = TreeLayout.Compute(g), b = TreeLayout.Compute(g);
        Assert.Equal(a.Boxes, b.Boxes);
        Assert.Equal(7, a.Bands.Count);
        foreach (GraphEdge e in g.Edges.Where(e => e.Directed && e.Kind.Layering))
            Assert.True(a.Box(e.FlowTo).Column > a.Box(e.FlowFrom).Column,
                $"{g.Node(e.FlowFrom).Id} → {g.Node(e.FlowTo).Id} ({e.Kind.Id}) does not flow left to right");
        for (int i = 0; i < g.Count; i++)
        {
            BandBox band = a.Bands[a.Box(i).Band];
            Assert.Equal(g.Node(i).Domain, band.LensId);
            Assert.InRange(a.Box(i).Y, band.Y, band.Y + band.Height - a.Box(i).H);
        }
        for (int i = 0; i < g.Count; i++)
            for (int j = i + 1; j < g.Count; j++)
            {
                NodeBox p = a.Box(i), q = a.Box(j);
                bool overlap = p.X < q.Right && q.X < p.Right && p.Y < q.Bottom && q.Y < p.Bottom;
                Assert.False(overlap, $"{g.Node(i).Id} overlaps {g.Node(j).Id}");
            }
    }

    [Fact]
    public void Layout_TieDense_EqualBarycentresBreakOnTheNodeIndex()
    {
        // Twelve siblings under one parent, all with the identical barycentre: the composite
        // key (score, slot, index) must leave them in content order, every run.
        JsonNode t = JsonNode.Parse(ReadShipped(TreesContentLoader.TreesFile))!;
        var nodes = new JsonArray { new JsonObject { ["id"] = "k.root", ["domain"] = "KNOWLEDGE", ["type"] = "knowledge", ["name"] = "Root" } };
        for (int i = 0; i < 12; i++)
            nodes.Add(new JsonObject
            {
                ["id"] = $"k.leaf{i:00}", ["domain"] = "KNOWLEDGE", ["type"] = "knowledge", ["name"] = $"Leaf {i}",
                ["prerequisites"] = new JsonArray("k.root"),
            });
        t["nodes"] = nodes;
        t["edges"] = new JsonArray();
        JsonNode ages = JsonNode.Parse(ReadShipped(TreesContentLoader.AgesFile))!;
        foreach (JsonNode? m in ages["milestones"]!.AsArray()) m!["nodeRefs"] = new JsonArray();
        ContentLoadResult r = TreesContentLoader.LoadFromJson(t.ToJsonString(), ages.ToJsonString(),
            ReadShipped(TreesContentLoader.GalleryFile), ReadShipped(TreesContentLoader.AnimationsFile));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var g = new TreeGraph(r.Content!.Trees);
        TreeLayoutResult layout = TreeLayout.Compute(g, new LayoutOptions(Sweeps: 8));
        int[] order = Enumerable.Range(1, 12).OrderBy(i => layout.Box(i).Slot).ToArray();
        Assert.Equal(Enumerable.Range(1, 12).ToArray(), order);
        Assert.Equal(layout.Boxes, TreeLayout.Compute(g, new LayoutOptions(Sweeps: 8)).Boxes);
        Assert.Equal(7, layout.Bands.Count);   // empty lenses still get a band
    }
}
