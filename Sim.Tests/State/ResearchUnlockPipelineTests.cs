using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// R1 — THE RESEARCH TREES DRIVE GAMEPLAY (the Director's unlock-pipeline directive, tests 1–18 and the corpus
/// tests; 19–20 and the synthetic-node acceptance test are in Sim.Ui.Tests ResearchUnlockPipelineUiTests).
/// Every gate here is DECLARED IN CONTENT and evaluated by the one knowledge evaluator
/// (ResearchQuery.IsKnowledgeEligible / RequirementMet); no test names a capability that the content does not.
/// "Unavailable" is checked at three places: the read-only query, the domain predicate, and AUTHORITATIVE
/// acceptance (an order injected straight into a step is refused by the system).
/// </summary>
public class ResearchUnlockPipelineTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));
    private static WorldState Solo() => DevSolo.Value.Clone();

    private static WorldState Know(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in WithAncestors(nodeIds))
        {
            ResearchNodeId key = Research.Nodes[Research.IndexOfId(id)].Key;
            if (!ResearchQuery.IsCompleted(w, polity, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(polity, key));
        }
        return w;
    }

    private static string[] WithAncestors(params string[] nodeIds)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodeIds) { int i = Research.IndexOfId(id); Assert.True(i >= 0, id); stack.Push(i); }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        var result = new List<string>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) result.Add(Research.Nodes[i].Id);
        return [.. result];
    }

    private static WorldState Step(WorldState w, params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), log).Step(w);
    }

    private static T[] Rows<T>(Table<T> t) where T : unmanaged { var r = new T[t.Count]; for (int i = 0; i < r.Length; i++) r[i] = t[i]; return r; }

    private static ImmutableArray<ActionDescriptor> Actions(WorldState w) => AvailableActionsQuery.For(w, Cfg, Player);
    private static bool Lists(WorldState w, ActionDomain d, string? label = null) =>
        Actions(w).Any(a => a.Domain == d && (label is null || a.Label == label));
    private static SettlementId Capital(WorldState w) { Assert.True(EmpireQuery.TryGetCapital(w, Player, out SettlementId c)); return c; }
    private static RecipeEntry Recipe(string name) => Cfg.Goods!.Recipes.Single(r => r.Name == name);
    private static int Entity(string id) { int e = Research.EntityIndexOf(id); Assert.True(e >= 0, id); return e; }
    private static bool[] Mask(params string[] nodes)
    {
        var m = new bool[Research.Nodes.Count];
        foreach (string id in nodes) m[Research.IndexOfId(id)] = true;
        return m;
    }

    private static long Produced(WorldState w, string good)
    {
        int id = Cfg.Goods!.IdOf(good);
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good.Value == id) sum += w.GoodStocks[i].LastProducedUnits;
        return sum;
    }

    // ------------------------------------------------------------------ 1–2 farming

    [Fact]
    public void T01_Farming_UnavailableBeforeItsResearch()
    {
        WorldState w = Solo();
        LabourActivity farm = LabourActivities.Of(w, Cfg, Player, Capital(w), Sectors.Farming)!;
        Assert.Equal("Gathering", farm.Label);
        Assert.DoesNotContain(Actions(w), a => a.Domain == ActionDomain.Labour && a.Label.Contains("Farming", StringComparison.Ordinal));
    }

    [Fact]
    public void T02_Farming_AvailableAfterItsResearch()
    {
        WorldState w = Know(Solo(), Player, "cereal_cultivation");
        Assert.Equal("Farming", LabourActivities.Of(w, Cfg, Player, Capital(w), Sectors.Farming)!.Label);
        ActionDescriptor a = Actions(w).First(x => x.Domain == ActionDomain.Labour && x.Label == "Farming");
        Assert.Contains("activity.farming", a.Provenance.Entities);
    }

    // ------------------------------------------------------------------ 3–6 pottery and bronze casting (recipes)

    [Fact]
    public void T03_Pottery_UnavailableBeforeResearch_QueryAndProduction()
    {
        WorldState w = Solo();
        Assert.False(Lists(w, ActionDomain.Production, "Pottery firing"));
        Assert.False(CraftingQuery.IsRecipeAvailable(w, Cfg, Capital(w), Recipe("pottery-firing")));
        Assert.Equal(0, Produced(Step(w), "pottery"));   // ProductionSystem fires nothing
    }

    [Fact]
    public void T04_Pottery_AvailableAfterResearch_QueryAndProduction()
    {
        WorldState w = Know(Solo(), Player, "pottery_open_fired");
        ActionDescriptor a = Actions(w).Single(x => x.Domain == ActionDomain.Production && x.Label == "Pottery firing");
        Assert.Equal(["recipe.pottery_firing"], a.Provenance.Entities.ToArray());
        Assert.Contains("pottery_open_fired", a.Provenance.Nodes.Select(k => Research.Nodes[Research.IndexOf(k)].Id));
        Assert.True(CraftingQuery.IsRecipeAvailable(w, Cfg, Capital(w), Recipe("pottery-firing")));
        Assert.True(Produced(Step(w), "pottery") > 0);   // knowledge acquired, not inventory granted: made by labour from clay
        Assert.Equal(0, Produced(w, "pottery"));
    }

    [Fact]
    public void T05_BronzeCasting_UnavailableBeforeResearch_EvenWithTheArtisanLatchOpen()
    {
        WorldState w = Solo();
        SettlementId cap = Capital(w);
        w.Variables.Add(new VariableRow(cap, Variables.IdOf("artisan_share"), 0.5));   // the class-domain latch holds
        Assert.False(Lists(w, ActionDomain.Production, "Bronze casting"));
        Assert.False(CraftingQuery.IsRecipeAvailable(w, Cfg, cap, Recipe("bronze-casting")));
    }

    [Fact]
    public void T06_BronzeCasting_AvailableAfterResearch_SubjectToTheArtisanLatch()
    {
        WorldState w = Know(Solo(), Player, "tin_bronze");
        SettlementId cap = Capital(w);
        Assert.True(Lists(w, ActionDomain.Production, "Bronze casting"));                         // known
        Assert.False(CraftingQuery.IsRecipeAvailable(w, Cfg, cap, Recipe("bronze-casting")));    // latch closed at founding
        w.Variables.Add(new VariableRow(cap, Variables.IdOf("artisan_share"), 0.5));
        Assert.True(CraftingQuery.IsRecipeAvailable(w, Cfg, cap, Recipe("bronze-casting")));
        Assert.Contains(Actions(w).Single(x => x.Label == "Bronze casting").Targets, t => t.Id == cap.Value);
    }

    // ------------------------------------------------------------------ 7–8 tax

    [Fact]
    public void T07_Tax_UnavailableBeforePrerequisiteResearch()
    {
        WorldState w = Solo();
        Assert.False(Governance.CanLevyTax(w, Cfg, Player));
        Assert.False(Lists(w, ActionDomain.Governance));
    }

    [Fact]
    public void T08_Tax_AvailableAfterPrerequisiteResearch()
    {
        WorldState w = Know(Solo(), Player, "taxation");
        Assert.True(Governance.CanLevyTax(w, Cfg, Player));
        ActionDescriptor tax = Actions(w).Single(a => a.Domain == ActionDomain.Governance);
        Assert.Equal(OrderKind.SetTaxRate, tax.Order);
        WorldState next = Step(w, Governance.TaxOrder(w.Clock.Turn, Player, 20));
        Assert.Equal(1, next.TaxPolicies.Count);
    }

    private static bool Avail(WorldState w, int node) =>
        ResearchQuery.AvailableMask(Research, ResearchQuery.CompletedMask(w, Research, Player))[node];

    /// <summary>R5 (Director 2026-10-04: "taxation is supposed to be a researchable node"): the Taxation civic is
    /// not researchable at turn 1, becomes researchable exactly when its prerequisites are known, and the four
    /// refinement technologies (tax assessment / by area / by weight / in coin) — with their whole ancestry —
    /// neither list the edict nor let a hand-built SetTaxRate through. Completing Taxation does both.</summary>
    [Fact]
    public void T08b_Tax_IsTheTaxationCivic_NotItsRefinements_AndAHandBuiltEdictIsRefusedBeforeIt()
    {
        int taxation = Research.IndexOfId("taxation");
        ResearchNode node = Research.Nodes[taxation];
        Assert.Equal(ResearchTree.Civics, node.Tree);
        Assert.Equal(1007, node.Key.Value);

        WorldState w = Solo();
        Assert.False(Avail(w, taxation));   // turn 1: Stone Age, no tax

        WorldState refined = Know(Solo(), Player, "arithmetic_babylonian", "surveying", "standard_weights", "coinage_electrum");
        Assert.False(ResearchQuery.IsCompleted(refined, Player, node.Key));
        Assert.False(Governance.CanLevyTax(refined, Cfg, Player));
        Assert.False(Lists(refined, ActionDomain.Governance));
        WorldState refused = Step(refined, Governance.TaxOrder(refined.Clock.Turn, Player, 25));
        Assert.Equal(0, refused.TaxPolicies.Count);

        // Its prerequisites alone make it researchable, not completed.
        WorldState ready = Know(Solo(), Player, "token_counting", "stamp_seal", "proto_writing");
        Assert.True(Avail(ready, taxation));
        Assert.False(Governance.CanLevyTax(ready, Cfg, Player));

        WorldState taxed = Know(refined, Player, "taxation");
        Assert.True(Governance.CanLevyTax(taxed, Cfg, Player));
        Assert.True(Lists(taxed, ActionDomain.Governance));
        Assert.Equal(1, Step(taxed, Governance.TaxOrder(taxed.Clock.Turn, Player, 25)).TaxPolicies.Count);
    }

    // ------------------------------------------------------------------ 9–10 roads

    [Fact]
    public void T09_RoadDevelopment_UnavailableBeforeRoadTechnology()
    {
        WorldState w = Step(Solo());   // travel costs cached
        Assert.False(Lists(w, ActionDomain.Roads));
        Assert.Empty(RoadDevelopmentQuery.Plan(w, Research, Cfg.Roads!, Cfg.Goods!, Player, 100.0));
    }

    [Fact]
    public void T10_RoadDevelopment_AvailableAfterRoadTechnology()
    {
        WorldState w = Know(Step(Solo()), Player, "track_road");
        ActionDescriptor roads = Actions(w).Single(a => a.Domain == ActionDomain.Roads);
        Assert.Equal(OrderKind.DevelopRoads, roads.Order);
        Assert.Contains("infra.road_track", roads.Provenance.Entities);
    }

    // ------------------------------------------------------------------ 11–12 university

    private static readonly string[] UniversityKnowledge =
        ["medicine_hippocratic", "geometry_axiomatic", "cuneiform", "stamp_seal", "legal_code_roman"];

    [Fact]
    public void T11_University_UnavailableBeforeItsPrerequisites()
    {
        WorldState w = Know(Solo(), Player, "medicine_hippocratic", "geometry_axiomatic");   // the building's knowledge only
        Assert.False(Lists(w, ActionDomain.Institutions));
        int project = Cfg.Goods!.Projects!.First(p => p.Founds is not null).Id;
        Assert.False(ConstructionQuery.IsProjectAvailable(w, Cfg, Player, Capital(w), project));
    }

    [Fact]
    public void T12_University_AvailableAfterItsPrerequisites_KnowledgeNotABuilding()
    {
        WorldState w = Know(Solo(), Player, UniversityKnowledge);
        ActionDescriptor[] found = Actions(w).Where(a => a.Domain == ActionDomain.Institutions).ToArray();
        Assert.Equal(5 * w.Settlements.Count, found.Length);   // five university types × controlled settlements
        Assert.All(found, a => Assert.Equal(OrderKind.EnqueueConstruction, a.Order));
        Assert.Equal(0, w.Institutions.Count);                          // research completion founds nothing
    }

    // ------------------------------------------------------------------ 13–14 cross-branch, civic + technology

    [Fact]
    public void T13_CrossBranchPrerequisites_EvaluatedOnTheRealGraph()
    {
        int university = Entity("building.university");   // medicine_hippocratic AND (geometry_axiomatic OR algebra)
        string medicine = Research.Nodes[Research.IndexOfId("medicine_hippocratic")].Domain;
        string geometry = Research.Nodes[Research.IndexOfId("geometry_axiomatic")].Domain;
        Assert.NotEqual(medicine, geometry);
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, university, Mask(WithAncestors("medicine_hippocratic"))));
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, university, Mask(WithAncestors("geometry_axiomatic"))));
        Assert.True(ResearchQuery.IsKnowledgeEligible(Research, university, Mask(WithAncestors("medicine_hippocratic", "geometry_axiomatic"))));
    }

    [Fact]
    public void T14_CivicPlusTechnologyPrerequisites_BothMustHold()
    {
        int inst = Entity("inst.university");   // library (institution: technology knowledge) AND legal_code_roman (a CIVIC)
        Assert.Equal(ResearchTree.Civics, Research.Nodes[Research.IndexOfId("legal_code_roman")].Tree);
        string[] tech = WithAncestors("cuneiform", "stamp_seal");
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, inst, Mask(tech)));
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, inst, Mask(WithAncestors("legal_code_roman"))));
        Assert.True(ResearchQuery.IsKnowledgeEligible(Research, inst, Mask([.. tech, .. WithAncestors("legal_code_roman")])));
    }

    // ------------------------------------------------------------------ 15 direct order injection

    [Fact]
    public void T15_DirectInjection_OfEveryResearchGatedOrderKind_IsRejectedByAuthoritativeAcceptance()
    {
        WorldState w = Step(Solo());
        SettlementId cap = Capital(w);
        int university = Cfg.Goods!.Projects!.First(p => p.Founds is not null).Id;
        ResearchNodeId locked = Research.Nodes[Research.IndexOfId("tin_bronze")].Key;   // not available at founding
        OrderRecord[] injected =
        [
            Governance.TaxOrder(w.Clock.Turn, Player, 30),                                 // kind 5
            ConstructionQuery.EnqueueOrder(w, Player, cap, university),                    // kind 4
            RoadDevelopmentQuery.DevelopOrder(w, Player, 100),                             // kind 8
            OrderRecord.From(w.Clock.Turn, Player, OrderKind.SetResearchTarget, (int)locked.Value, 0.0),   // kind 6
            AgeQuery.AdvanceOrder(w, Player, 2, 0),                                        // kind 7
        ];
        WorldState next = Step(w, injected);
        Assert.Equal(0, next.TaxPolicies.Count);
        Assert.DoesNotContain(Rows(next.ConstructionQueue), r => r.ProjectId == university);
        Assert.Empty(Rows(next.RoadDevelopments));
        Assert.DoesNotContain(Rows(next.ResearchTargets), r => r.Node.Value == locked.Value);
        Assert.Equal(AgeQuery.CurrentAge(w, Cfg.Ages!, Player), AgeQuery.CurrentAge(next, Cfg.Ages!, Player));
    }

    // ------------------------------------------------------------------ 16 AI

    [Fact]
    public void T16_AiCannotBypassCapabilityRequirements()
    {
        WorldState w = Step(DevDuo.Value.Clone());
        OrderRecord[] ai = AiOrders.For(w, Cfg);
        Assert.DoesNotContain(ai, o => o.Kind is OrderKind.SetTaxRate or OrderKind.DevelopRoads);
        Assert.DoesNotContain(ai, o => o.Kind == OrderKind.EnqueueConstruction && Cfg.Goods!.ProjectById((int)o.Amount)?.Founds is not null);
        // Its orders take the same authoritative path: whatever it asked for, no gated effect appears.
        WorldState next = Step(w, ai);
        Assert.Equal(0, next.TaxPolicies.Count);
        Assert.Empty(Rows(next.RoadDevelopments));
    }

    // ------------------------------------------------------------------ 17–18 save/load, replay

    [Fact]
    public void T17_SaveLoad_PreservesCapabilities()
    {
        WorldState w = Know(Step(Solo()), Player, "pottery_open_fired", "tin_bronze", "taxation", "track_road");
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, w.Terrain);
        Assert.True(AvailableActionsQuery.Same(Actions(w), Actions(loaded)));
        Assert.True(Lists(loaded, ActionDomain.Production, "Pottery firing"));
        Assert.True(Lists(loaded, ActionDomain.Governance));
    }

    [Fact]
    public void T18_Replay_PreservesCapabilityAvailability_TurnByTurn()
    {
        WorldState start = Know(Solo(), Player, "taxation");
        WorldState a = start.Clone(), b = start.Clone();
        for (int t = 0; t < 4; t++)
        {
            OrderRecord tax = Governance.TaxOrder(a.Clock.Turn, Player, 10 + t);
            a = Step(a, tax);
            b = Step(b, tax);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            Assert.True(AvailableActionsQuery.Same(Actions(a), Actions(b)));
        }
    }

    // ------------------------------------------------------------------ corpus-level

    [Fact]
    public void Corpus_EveryGatedEntity_IsUnavailableWithZeroKnowledge_AndBaselineEntitiesAreAvailable()
    {
        var none = new bool[Research.Nodes.Count];
        for (int e = 0; e < Research.Entities.Count; e++)
        {
            bool gated = Research.Entities[e].Requirement is not null;
            Assert.True(gated != ResearchQuery.IsKnowledgeEligible(Research, e, none), Research.Entities[e].Id);
        }
    }

    [Fact]
    public void Corpus_EveryEntity_IsAvailableWhenItsRequirementClosureIsCompleted()
    {
        for (int e = 0; e < Research.Entities.Count; e++)
        {
            ResearchEntity entity = Research.Entities[e];
            if (entity.Requirement is null || entity.Unresolved.Count > 0) continue;
            // The closure: every node the requirement or its institution atoms name, with all their ancestors.
            var ids = new List<string>();
            var pending = new Stack<int>();
            var seen = new bool[Research.Entities.Count];
            pending.Push(e);
            while (pending.Count > 0)
            {
                int x = pending.Pop();
                if (seen[x]) continue;
                seen[x] = true;
                foreach (int n in Research.Entities[x].NodeAtoms) ids.Add(Research.Nodes[n].Id);
                foreach (int i in Research.Entities[x].InstitutionAtoms) pending.Push(i);
            }
            Assert.True(ResearchQuery.IsKnowledgeEligible(Research, e, Mask(WithAncestors([.. ids]))), entity.Id);
        }
    }

    [Fact]
    public void Corpus_DownstreamChain_IsNotUnlockedByTheUpstreamNodeAlone()
    {
        // Every entity that requires a node with prerequisites: completing ONLY that node's ancestors (not the node)
        // never makes the entity eligible unless another alternative of its OR holds there.
        int checkedCount = 0;
        for (int e = 0; e < Research.Entities.Count; e++)
        {
            ResearchEntity entity = Research.Entities[e];
            if (entity.Requirement is null || entity.NodeAtoms.Count != 1 || entity.InstitutionAtoms.Count != 0) continue;
            int node = entity.NodeAtoms[0];
            if (Research.Nodes[node].PrerequisiteNodes.Count == 0) continue;
            var ancestors = WithAncestors(Research.Nodes[node].Id).Where(id => id != Research.Nodes[node].Id).ToArray();
            Assert.False(ResearchQuery.IsKnowledgeEligible(Research, e, Mask(ancestors)), entity.Id);
            Assert.True(ResearchQuery.IsKnowledgeEligible(Research, e, Mask([.. ancestors, Research.Nodes[node].Id])), entity.Id);
            checkedCount++;
        }
        Assert.True(checkedCount > 50, "vacuous: " + checkedCount);
    }

    [Fact]
    public void Corpus_EveryRecipeIsLinked_AndBaselineRecipesAreExactlyTheNullRequirementOnes()
    {
        var none = new bool[Research.Nodes.Count];
        foreach (RecipeEntry r in Cfg.Goods!.Recipes)
        {
            Assert.NotNull(r.Entity);
            ResearchEntity entity = Research.Entities[Entity(r.Entity!)];
            Assert.Equal(ResearchEntityKind.Recipe, entity.Kind);
            Assert.Equal(entity.Requirement is null, CraftingQuery.IsKnown(Research, r, none));
        }
        Assert.Equal(["pottery-firing", "bronze-casting"],
            Cfg.Goods.Recipes.Where(r => Research.Entities[Entity(r.Entity!)].Requirement is not null).Select(r => r.Name).ToArray());
    }

    [Fact]
    public void UncontrolledSettlement_HasNoResearchedKnowledge_ButRunsBaselineRecipes()
    {
        WorldState w = Know(Solo(), Player, "pottery_open_fired");
        SettlementId cap = Capital(w);
        var kept = Rows(w.Controls).Where(c => c.Place != cap).ToArray();
        w.Controls.Clear();
        foreach (ControlRow c in kept) w.Controls.Add(c);
        Assert.False(CraftingQuery.IsRecipeAvailable(w, Cfg, cap, Recipe("pottery-firing")));
        Assert.True(CraftingQuery.IsRecipeAvailable(w, Cfg, cap, Recipe("weaving")));
    }
}
