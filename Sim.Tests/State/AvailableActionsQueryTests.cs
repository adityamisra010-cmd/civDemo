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
/// ADR-033 D2 — "what can this civilization do now?" answered by ONE read-only aggregation of domain-owned
/// predicates. These tests pin: the ordering key (domain ordinal, stable id) with a tie-dense test; that only
/// AVAILABLE actions are listed (a locked future action never is); the knowledge-derived labour identities
/// (ADR-033 D1) on the same sector with the same share and the same production; and that the whole result
/// survives a snapshot round trip, because every input it reads is authoritative saved state.
/// </summary>
public class AvailableActionsQueryTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Solo() => DevSolo.Value.Clone();
    private static WorldState Duo() => DevDuo.Value.Clone();

    private static WorldState Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
        {
            int index = Research.IndexOfId(id);
            Assert.True(index >= 0, id);
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[index].Key));
        }
        return w;
    }

    /// <summary>Every prerequisite-ancestor of the nodes, the nodes included (to complete a node "as if researched").</summary>
    private static string[] WithAncestors(params string[] nodeIds)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodeIds) stack.Push(Research.IndexOfId(id));
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

    private static ActionDescriptor[] Of(ImmutableArray<ActionDescriptor> actions, ActionDomain domain) =>
        actions.Where(a => a.Domain == domain).ToArray();

    private static TurnExecutor Production(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    // ------------------------------------------------------------------ ordering (tie-dense)

    private static ActionDescriptor D(ActionDomain domain, long id) =>
        new(domain, id, ActionKind.Order, $"{domain}.{id}", "x", null, [], null, ActionProvenance.None);

    [Fact]
    public void Ordering_IsTheCompositeKey_DomainOrdinalThenStableId_TieDense_InputOrderIrrelevant()
    {
        // Tie-dense: EVERY domain carries the SAME dense run of ids (0..15, plus large packed ids), so the
        // domain decides every cross-domain pair and the id every same-domain pair; nothing else may.
        ActionDomain[] domains = Enum.GetValues<ActionDomain>();
        long[] ids = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, (3L << 32) | 1, (3L << 32) | 2, 1L << 40];
        var expected = new List<ActionDescriptor>();
        foreach (ActionDomain d in domains.OrderBy(d => (int)d))
            foreach (long id in ids.Order())
                expected.Add(D(d, id));

        // Several deterministic permutations of the same set: the result must not depend on input order.
        for (int seed = 1; seed <= 5; seed++)
        {
            var shuffled = new List<ActionDescriptor>(expected);
            uint state = (uint)seed * 2654435761u;
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int j = (int)(state % (uint)(i + 1));
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            ImmutableArray<ActionDescriptor> ordered = AvailableActionsQuery.Order(shuffled);
            Assert.Equal(expected.Select(a => (a.Domain, a.Id)), ordered.Select(a => (a.Domain, a.Id)));
        }
        // The comparer itself: domain first, then id; equal keys compare equal.
        Assert.True(AvailableActionsQuery.Compare(D(ActionDomain.Labour, 1L << 40), D(ActionDomain.Research, 0)) < 0);
        Assert.True(AvailableActionsQuery.Compare(D(ActionDomain.Standing, 0), D(ActionDomain.Standing, 1)) < 0);
        Assert.Equal(0, AvailableActionsQuery.Compare(D(ActionDomain.Age, 2), D(ActionDomain.Age, 2)));
    }

    [Fact]
    public void Ids_AreUniqueWithinEveryDomain_OnARealWorld_SoTheKeyIsTotal()
    {
        WorldState w = Complete(Duo(), Player, "root_crop");
        foreach (PolityId p in new[] { Player, Rival })
        {
            ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, p);
            Assert.Equal(actions.Length, actions.Select(a => (a.Domain, a.Id)).Distinct().Count());
            Assert.Equal(actions.Length, actions.Select(a => a.Key).Distinct().Count());
            for (int i = 1; i < actions.Length; i++) Assert.True(AvailableActionsQuery.Compare(actions[i - 1], actions[i]) < 0);
        }
    }

    // ------------------------------------------------------------------ the other researched identities

    [Fact]
    public void HerdingReplacesHunting_LoggingAndMiningJoinGatheringWoodAndStone()
    {
        WorldState herd = Complete(Solo(), Player, "dog_domestication", "sheep_goat");
        SettlementId s0 = herd.Settlements[0].Id;
        Assert.Equal("Herding & fishing", LabourActivities.Of(herd, Cfg, Player, s0, Sectors.Herding)!.Label);
        Assert.Equal("Gathering wood & stone", LabourActivities.Of(herd, Cfg, Player, s0, Sectors.Extraction)!.Label);

        WorldState log = Complete(Solo(), Player, WithAncestors("ground_stone_early"));
        LabourActivity extraction = LabourActivities.Of(log, Cfg, Player, s0, Sectors.Extraction)!;
        Assert.Equal("Gathering wood & stone · Logging", extraction.Label);
        Assert.Equal(["gathering_wood_stone", "activity.logging"], extraction.Identities.Select(i => i.Id));
        Assert.Equal([false, true], extraction.Identities.Select(i => i.Researched));

        WorldState mine = Complete(Solo(), Player, WithAncestors("ground_stone_early", "mining_shaft"));
        Assert.Equal("Gathering wood & stone · Logging · Mining", LabourActivities.Of(mine, Cfg, Player, s0, Sectors.Extraction)!.Label);
        // Crafting and Construction have no researched identity in this pass.
        Assert.Equal("Crafts & toolmaking", LabourActivities.Of(mine, Cfg, Player, s0, Sectors.Crafting)!.Label);
        Assert.Equal("Building", LabourActivities.Of(mine, Cfg, Player, s0, Sectors.Construction)!.Label);
    }

    // ------------------------------------------------------------------ only what exists, only what is yours

    [Fact]
    public void OnlyTheIssuersSettlements_AndNothingForAnUnregisteredPolity()
    {
        WorldState w = Duo();
        foreach (PolityId p in new[] { Player, Rival })
        {
            SettlementId[] mine = LabourActivities.ControlledSettlements(w, p);
            Assert.Equal(2, mine.Length);   // dev world: four settlements, round-robin between two Empires
            ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, p);
            foreach (ActionDescriptor a in actions.Where(a => a.Domain is ActionDomain.Labour or ActionDomain.Construction))
            {
                ActionTarget place = a.Targets.First(t => t.Kind == ActionTargetKind.Settlement);
                Assert.True(EmpireQuery.ControlsSettlement(w, p, new SettlementId((int)place.Id)), a.Key);
            }
            Assert.Equal(2 * Sectors.Count, actions.Count(a => a.Domain == ActionDomain.Labour));
        }
        Assert.Empty(AvailableActionsQuery.For(w, Cfg, new PolityId(99)));
    }

    [Fact]
    public void ResearchTarget_ClearAppearsWhenATargetIsSet_AndTheCurrentTargetIsMarked()
    {
        WorldState w = Solo();
        int grinding = Research.IndexOfId("grinding_stone");
        w.ResearchTargets.Add(new ResearchTargetRow(Player, Research.Nodes[grinding].Key));
        ActionDescriptor[] research = Of(AvailableActionsQuery.For(w, Cfg, Player), ActionDomain.Research);
        Assert.Equal(["research.set-target", "research.clear-target"], research.Select(a => a.Key));
        ActionTarget current = Assert.Single(research[0].Targets, t => t.Current);
        Assert.Equal(Research.Nodes[grinding].Key.Value, current.Id);
        Assert.Equal(Research.Nodes[grinding].BaseCost, current.Cost);
        Assert.Equal(current, Assert.Single(research[1].Targets));
    }

    [Fact]
    public void AdvanceAge_OfferedOnlyWhenEligible_AndNotWhileAnAdvanceIsQueued()
    {
        WorldState w = Solo();
        Assert.Empty(Of(AvailableActionsQuery.For(w, Cfg, Player), ActionDomain.Age));   // not eligible
        Complete(w, Player, "cereal_cultivation", "pottery_open_fired");                 // A2 entry holds
        // M5 R2b: the founding warband is not the Neolithic's military realization; a granary is the second category.
        Assert.True(EmpireQuery.TryGetCapital(w, Player, out SettlementId granarySite));
        w.Structures.Add(new StructureRow(granarySite, 1, 1));
        ActionDescriptor advance = Assert.Single(Of(AvailableActionsQuery.For(w, Cfg, Player), ActionDomain.Age));
        Assert.Equal((2L, OrderKind.AdvanceAge, "age.advance.2"), (advance.Id, advance.Order!.Value, advance.Key));
        Assert.Equal(TestConfigs.Ages().Surges.Select(s => (long)s.Key), advance.Targets.Select(t => t.Id));
        // A decision already queued this turn is not offered twice.
        OrderRecord queued = AgeQuery.AdvanceOrder(w, Player, 2, TestConfigs.Ages().Surges[0].Key);
        Assert.Empty(Of(AvailableActionsQuery.For(w, Cfg, Player, new ActionQueryContext(Queued: [queued])), ActionDomain.Age));
    }

    [Fact]
    public void RoadDevelopment_AppearsOnlyOnceAClassIsKnown_ThroughTheSamePlanTheSystemApplies()
    {
        WorldState w = Solo();
        Assert.Empty(Of(AvailableActionsQuery.For(w, Cfg, Player), ActionDomain.Roads));
        Assert.Empty(RoadDevelopmentQuery.Plan(w, Research, Cfg.Roads!, Cfg.Goods!, Player, 100.0));
        // The founded world has no cached travel costs until catchment has run: step once, then know trackways.
        WorldState stepped = Production().Step(w);
        Complete(stepped, Player, WithAncestors("track_road"));
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(stepped, Research, Cfg.Roads!, Cfg.Goods!, Player, 100.0);
        ActionDescriptor roads = Assert.Single(Of(AvailableActionsQuery.For(stepped, Cfg, Player), ActionDomain.Roads));
        Assert.NotEmpty(plan);
        Assert.Equal((long)EdgeTypes.Trackway, roads.Id);
        Assert.Equal(OrderKind.DevelopRoads, roads.Order);
        Assert.Equal(plan.Length, roads.Targets.Length);
        Assert.Equal(["infra.road_track"], roads.Provenance.Entities.ToArray());
        Assert.Contains("track_road", roads.Provenance.Nodes.Select(k => Research.Nodes[Research.IndexOf(k)].Id));
    }
}
