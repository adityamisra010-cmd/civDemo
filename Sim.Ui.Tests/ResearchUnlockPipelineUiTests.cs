using Xunit;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Actions;
using Sim.Ui.Theme;

namespace Sim.Ui.Tests;

/// <summary>
/// R1 — THE RESEARCH TREES DRIVE GAMEPLAY, seen from the UI. The acceptance criterion of the Director's unlock
/// directive: adding a research node and DECLARING its gameplay consequence in content (a research.json node,
/// a building entity requiring it, a goods.json project linked to that entity) flows UNRESEARCHED → NOT
/// AVAILABLE and RESEARCHED → AVAILABLE through the query, authoritative order acceptance and the UI's action
/// surface — with no UI code naming the node, the entity or the project.
/// </summary>
public class ResearchUnlockPipelineUiTests
{
    private static readonly PolityId Me = new(1);
    private const string SyntheticNode = "synthetic_r1_test_node";
    private const string SyntheticEntity = "building.synthetic_r1_test_hall";
    private const int SyntheticProject = 9901;

    /// <summary>The canonical six files with ONE synthetic node + consequence added (content only).</summary>
    private static SimConfig SyntheticConfig()
    {
        JsonNode research = JsonNode.Parse(ReadAll(Sim.Data.DataFiles.OpenResearch()))!;
        JsonArray techs = research["technologies"]!.AsArray();
        long maxKey = 0;
        foreach (JsonNode? n in techs) maxKey = Math.Max(maxKey, (long)n!["key"]!);
        JsonNode node = techs[0]!.DeepClone();   // a root node's shape; every field it needs, valid by construction
        node["key"] = maxKey + 1;
        node["id"] = SyntheticNode;
        node["name"] = "Synthetic test knowledge";
        node["prereq"] = null;
        node["eurekas"] = new JsonArray();
        JsonObject unlocks = node["unlocks"]!.AsObject();
        foreach (string list in new[] { "units", "buildings", "institutions", "infrastructure", "activities", "projects", "recipes" })
            unlocks[list] = new JsonArray();
        unlocks["buildings"] = new JsonArray(SyntheticEntity);
        techs.Add(node);
        research["entities"]!.AsArray().Add(new JsonObject
        {
            ["id"] = SyntheticEntity, ["kind"] = "building", ["name"] = "Synthetic test hall",
            ["requires"] = SyntheticNode, ["unresolved"] = new JsonArray(),
        });

        JsonNode goods = JsonNode.Parse(ReadAll(Sim.Data.DataFiles.OpenGoods()))!;
        goods["projects"]!.AsArray().Add(new JsonObject
        {
            ["id"] = SyntheticProject, ["name"] = "synthetic test hall", ["entity"] = SyntheticEntity,
            ["inputs"] = new JsonArray(new JsonObject { ["good"] = "timber", ["qty"] = 1 }), ["laborRequired"] = 0.5,
        });

        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var g = new MemoryStream(Encoding.UTF8.GetBytes(goods.ToJsonString()));
        using var r = new MemoryStream(Encoding.UTF8.GetBytes(research.ToJsonString()));
        using var ages = Sim.Data.DataFiles.OpenAges();
        using var families = Sim.Data.DataFiles.OpenUnitFamilies();
        return SimConfigLoader.Load(sim, needs, g, r, ages, families);
    }

    private static string ReadAll(Stream s) { using (s) using (var reader = new StreamReader(s)) return reader.ReadToEnd(); }

    private static WorldgenConfig DevWorldgen()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream);
        return cfg with { SizePx = 256, Siting = cfg.Siting with { SettlementCount = 4 } };
    }

    private static WorldState Step(WorldState w, SimConfig cfg, params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, DevWorldgen())), log);
        return exec.Step(w);
    }

    private static ActionSurfaceModel Surface(WorldState w, SimConfig cfg, int selected) =>
        ActionSurface.Build(new ActionSurfaceInput(w, null, cfg, UiSession.ProductionEra(), Me, selected, [],
            id => "settlement " + id, EraThemes.For(UiEras.Of(w, cfg.Ages, Me))));

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static bool Queued(IReadOnlyWorldState w, int project)
    {
        for (int i = 0; i < w.ConstructionQueue.Count; i++) if (w.ConstructionQueue[i].ProjectId == project) return true;
        for (int i = 0; i < w.Structures.Count; i++) if (w.Structures[i].ProjectId == project) return true;
        return false;
    }

    [Fact]
    public void AddingANodeWithADeclaredConsequence_FlowsUnavailableToAvailable_QueryValidationAndUi_WithNoUiCode()
    {
        SimConfig cfg = SyntheticConfig();
        WorldState w = WorldFounding.Found(DevWorldgen(), cfg, 42);
        int capital = Capital(w);
        var settlement = new SettlementId(capital);

        // UNRESEARCHED → NOT AVAILABLE: not in the query, not in the UI, and an injected order is refused.
        Assert.False(ConstructionQuery.IsProjectAvailable(w, cfg, Me, settlement, SyntheticProject));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, cfg, Me), a => a.Domain == ActionDomain.Construction && a.Targets[1].Id == SyntheticProject);
        Assert.DoesNotContain(Surface(w, cfg, capital).Construction!.Projects, p => p.ProjectId == SyntheticProject);
        WorldState refused = Step(w, cfg, ConstructionQuery.EnqueueOrder(w, Me, settlement, SyntheticProject));
        Assert.False(Queued(refused, SyntheticProject));

        // RESEARCHED → AVAILABLE (the node's completion row is the whole change).
        int index = cfg.Research!.IndexOfId(SyntheticNode);
        w.ResearchCompleted.Add(new ResearchCompletedRow(Me, cfg.Research.Nodes[index].Key));
        Assert.True(ConstructionQuery.IsProjectAvailable(w, cfg, Me, settlement, SyntheticProject));
        ActionDescriptor offered = Assert.Single(AvailableActionsQuery.For(w, cfg, Me),
            a => a.Domain == ActionDomain.Construction && a.Targets[0].Id == capital && a.Targets[1].Id == SyntheticProject);
        Assert.Equal([SyntheticEntity], offered.Provenance.Entities.ToArray());
        ProjectEntry shown = Assert.Single(Surface(w, cfg, capital).Construction!.Projects, p => p.ProjectId == SyntheticProject);
        Assert.Equal("Synthetic test hall", shown.Name);
        Assert.Equal("Synthetic test knowledge", shown.LearnedFrom);
        WorldState accepted = Step(w, cfg, ConstructionQuery.EnqueueOrder(w, Me, settlement, SyntheticProject));
        Assert.True(Queued(accepted, SyntheticProject));
    }

    [Fact]
    public void UiShowsOnlyLegalActions_EveryProductionEntryAndBuildButtonIsAQueryDescriptor()   // directive test 19
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldState w = WorldFounding.Found(DevWorldgen(), cfg, 42);
        int capital = Capital(w);
        foreach (bool researched in new[] { false, true })
        {
            if (researched)
                foreach (string id in ActionSurfacePreview.WithAncestors(cfg.Research!, "pottery_open_fired", "tin_bronze", "medicine_hippocratic",
                             "geometry_axiomatic", "cuneiform", "stamp_seal", "legal_code_roman"))
                    w.ResearchCompleted.Add(new ResearchCompletedRow(Me, cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Key));
            ImmutableArray<ActionDescriptor> query = AvailableActionsQuery.For(w, cfg, Me);
            ActionSurfaceModel m = Surface(w, cfg, capital);
            Assert.Equal(query.Where(a => a.Domain == ActionDomain.Production).Select(a => a.Label), m.Production!.Entries.Select(e => e.Name));
            foreach (ProjectEntry p in m.Construction!.Projects)
                Assert.Contains(query, a => a.Domain is ActionDomain.Construction or ActionDomain.Institutions
                    && a.Targets[0].Id == capital && a.Targets[1].Id == p.ProjectId);
            Assert.Equal(researched, m.Production.Entries.Any(e => e.Name == "Pottery firing"));
            Assert.Equal(researched, m.Production.Entries.Any(e => e.Name == "Bronze casting"));
            Assert.Equal(researched, m.Construction.Projects.Any(p => p.Name.Contains("University", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void CompletingResearch_MakesTheActionAppear_WithNoSeparateUiToggle()   // directive test 20
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldState w = WorldFounding.Found(DevWorldgen(), cfg, 42);
        var research = cfg.Research!;
        int pottery = research.IndexOfId("pottery_open_fired");
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "pottery_open_fired"))
            if (id != "pottery_open_fired") w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        Assert.DoesNotContain(Surface(w, cfg, Capital(w)).Production!.Entries, e => e.Name == "Pottery firing");

        // Research it through the order pathway only: set the target and step until ResearchSystem completes it.
        OrderRecord target = ResearchQuery.TargetOrder(w, research, Me, research.Nodes[pottery].Key)!.Value;
        WorldState prev = w;
        w = Step(w, cfg, target);
        for (int t = 0; t < 400 && !ResearchQuery.IsCompleted(w, Me, research.Nodes[pottery].Key); t++) { prev = w; w = Step(w, cfg); }
        Assert.True(ResearchQuery.IsCompleted(w, Me, research.Nodes[pottery].Key));

        ActionSurfaceModel m = ActionSurface.Build(new ActionSurfaceInput(w, prev, cfg, UiSession.ProductionEra(), Me, Capital(w), [],
            id => "settlement " + id, EraThemes.For(UiEras.Of(w, cfg.Ages, Me))));
        ProductionEntry entry = Assert.Single(m.Production!.Entries, e => e.Name == "Pottery firing");
        Assert.Equal(research.Nodes[pottery].Name, entry.LearnedFrom);
        Assert.Contains("new: Pottery firing", m.Notices);
    }
}
