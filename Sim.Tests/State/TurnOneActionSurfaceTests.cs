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
/// ADR-033 D1/D2, the TURN-1 ACCEPTANCE on the simulation side: on the canonical founded world the action
/// surface is exactly the civilization's baseline — the five labour sectors under their BASELINE identities,
/// research target selection, construction of the zero-node projects, basic fighting and the simulated
/// standing baselines — and nothing locked. Completing root_crop relabels the Farming sector "Farming"
/// (same sector, same share, same production), and the whole surface survives a snapshot round trip because
/// it is derived from the saved tables alone.
/// </summary>
public class TurnOneActionSurfaceTests
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

    private static ActionDescriptor[] Of(ImmutableArray<ActionDescriptor> actions, ActionDomain domain) =>
        actions.Where(a => a.Domain == domain).ToArray();

    /// <summary>Every prerequisite-ancestor of the nodes, the nodes included.</summary>
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
        return Enumerable.Range(0, seen.Length).Where(i => seen[i]).Select(i => Research.Nodes[i].Id).ToArray();
    }

    private static TurnExecutor Production(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    // ------------------------------------------------------------------ turn 1

    private static readonly Lazy<WorldState> Canonical = new(() => WorldFounding.Found(TestConfigs.Worldgen(), Cfg, 42));

    [Fact]
    public void TurnOne_CanonicalFoundedWorld_OffersExactlyTheBaselineSurface_AndNothingLocked()
    {
        WorldState w = Canonical.Value;
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player);
        Assert.Equal(12, w.Settlements.Count);

        // LABOUR: the five sectors of every settlement, each under its BASELINE identity, as SectorAllocation orders.
        ActionDescriptor[] labour = Of(actions, ActionDomain.Labour);
        Assert.Equal(12 * Sectors.Count, labour.Length);
        string[] baselineLabels = ["Gathering", "Hunting & fishing", "Gathering wood & stone", "Crafts & toolmaking", "Building"];
        foreach (ActionDescriptor a in labour)
        {
            int sector = (int)(a.Id & 7);
            Assert.Equal(baselineLabels[sector], a.Label);
            Assert.Equal(ActionKind.Order, a.Kind);
            Assert.Equal(OrderKind.SectorAllocation, a.Order);
            Assert.False(a.Provenance.Researched);
            Assert.NotEmpty(a.Provenance.Baseline);
            Assert.Null(a.Blocker);
        }

        // RESEARCH: set a target among the available nodes; there is no target to clear.
        ActionDescriptor research = Assert.Single(Of(actions, ActionDomain.Research));
        Assert.Equal("research.set-target", research.Key);
        Assert.Equal(ResearchQuery.AvailableNodes(w, Research, Player).Select(k => (long)k.Value), research.Targets.Select(t => t.Id));
        Assert.All(research.Targets, t => Assert.False(t.Current));

        // CONSTRUCTION: granary and workshop have null knowledge requirements, so both are AVAILABLE in every
        // settlement at zero nodes — legal, and blocked only by materials (founding endows grain alone).
        ActionDescriptor[] construction = Of(actions, ActionDomain.Construction);
        Assert.Equal(12 * 2, construction.Length);
        Assert.Equal(12, construction.Count(a => a.Label == "Build Granary"));
        Assert.Equal(12, construction.Count(a => a.Label == "Build Workshop"));
        Assert.All(construction, a => Assert.StartsWith("needs ", a.Blocker));
        Assert.Contains(construction, a => a.Blocker == "needs 40 timber (has 0), 20 stone (has 0)");

        // MILITARY: basic fighting — the founding warband — a standing capability with no order.
        ActionDescriptor military = Assert.Single(Of(actions, ActionDomain.Military));
        Assert.Equal((ActionKind.Standing, (OrderKind?)null, "Basic fighting"), (military.Kind, military.Order, military.Label));
        ActionTarget warband = Assert.Single(military.Targets);
        Assert.Equal("Warband", warband.Label);

        // STANDING: exactly the research.json baseline capabilities flagged simulated.
        Assert.Equal(
            ["baseline.settlement_founding", "baseline.food_gathering", "baseline.construction", "baseline.migration",
             "baseline.basic_fishing", "baseline.basic_shelter", "baseline.basic_paths", "baseline.hunting"],
            Of(actions, ActionDomain.Standing).Select(a => a.Provenance.Baseline.Single()));
        Assert.All(Of(actions, ActionDomain.Standing), a => Assert.Equal((ActionKind.Standing, (OrderKind?)null), (a.Kind, a.Order)));

        // NOTHING LOCKED: no researched activity, no tax, no road development, no Age advance, no institution.
        Assert.Empty(Of(actions, ActionDomain.Age));
        Assert.Empty(Of(actions, ActionDomain.Roads));
        Assert.Empty(Of(actions, ActionDomain.Governance));
        Assert.Empty(Of(actions, ActionDomain.Institutions));
        foreach (string locked in new[] { "Farming", "Herding & fishing", "Logging", "Mining" })
            Assert.DoesNotContain(actions, a => a.Domain == ActionDomain.Labour && a.Label.Contains(locked, StringComparison.Ordinal));
        Assert.DoesNotContain(actions, a => a.Provenance.Researched);
        // R1: the crafts known at founding are exactly the BASELINE recipes (null requirement in content).
        Assert.Equal(["Weaving", "Toolmaking"], Of(actions, ActionDomain.Production).Select(a => a.Label));
        Assert.Equal(12 * 5 + 1 + 24 + 1 + 8 + 2, actions.Length);
    }

    // ------------------------------------------------------------------ Farming replaces Gathering (step 7)

    [Fact]
    public void RootCropComplete_FarmingReplacesGathering_SameSectorSameShare_ProductionUnchanged()
    {
        WorldState before = Solo();
        WorldState after = Complete(Solo(), Player, "root_crop");
        SettlementId s0 = before.Settlements[0].Id;

        LabourActivity gathering = LabourActivities.Of(before, Cfg, Player, s0, Sectors.Farming)!;
        LabourActivity farming = LabourActivities.Of(after, Cfg, Player, s0, Sectors.Farming)!;
        Assert.Equal("Gathering", gathering.Label);
        Assert.Equal("Farming", farming.Label);
        LabourIdentity id = Assert.Single(farming.Identities);
        Assert.Equal(("activity.farming", true), (id.Id, id.Researched));
        ResearchNode rootCrop = Research.Nodes[Research.IndexOfId("root_crop")];
        Assert.Equal([rootCrop.Name], id.NodeNames.ToArray());
        Assert.Equal(rootCrop.Key, Assert.Single(id.Nodes));
        // Same sector, same share, same goods.
        Assert.Equal((gathering.Sector, gathering.Share), (farming.Sector, farming.Share));
        Assert.Equal(gathering.GoodNames.ToArray(), farming.GoodNames.ToArray());
        // The other sectors are untouched by a farming node.
        for (int sector = 1; sector < Sectors.Count; sector++)
            Assert.Equal(LabourActivities.Of(before, Cfg, Player, s0, sector)!.Label, LabourActivities.Of(after, Cfg, Player, s0, sector)!.Label);

        // Production is unchanged: production reads no research, so stepping both worlds yields the same stocks.
        WorldState b1 = Production().Step(before), a1 = Production().Step(after);
        Assert.True(WorldStates.TableEquals(b1.GoodStocks, a1.GoodStocks));
        Assert.True(WorldStates.TableEquals(b1.Buckets, a1.Buckets));
        Assert.True(WorldStates.TableEquals(b1.SectorAllocations, a1.SectorAllocations));

        // The action surface follows: the Farming sector's descriptor is relabelled and carries the node.
        ActionDescriptor d = AvailableActionsQuery.For(after, Cfg, Player)
            .Single(a => a.Domain == ActionDomain.Labour && a.Id == LabourActivities.PackTarget(s0, Sectors.Farming));
        Assert.Equal("Farming", d.Label);
        Assert.Equal(["activity.farming"], d.Provenance.Entities.ToArray());
        Assert.Equal(["root_crop"], d.Provenance.Nodes.Select(k => Research.Nodes[Research.IndexOf(k)].Id));
    }

    [Fact]
    public void RootCropComplete_TheWholeActionSurface_SurvivesASaveLoadRoundTrip()
    {
        // The identity is DERIVED from the saved ResearchCompleted table — no UI or derived state is stored —
        // so the whole query result, not only the label, is equal before save and after load (same world).
        // The rig also knows the trackway class, so the road action and the known class are in the surface.
        WorldState w = Production().Step(Complete(Solo(), Player, ["root_crop", .. WithAncestors("track_road")]));
        ImmutableArray<ActionDescriptor> beforeSave = AvailableActionsQuery.For(w, Cfg, Player);
        Assert.Contains(beforeSave, a => a.Domain == ActionDomain.Roads);
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, w.Terrain);
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(loaded));
        ImmutableArray<ActionDescriptor> afterLoad = AvailableActionsQuery.For(loaded, Cfg, Player);
        Assert.Equal(beforeSave.Length, afterLoad.Length);
        // Value equality: every field, targets and provenance element-wise (ImmutableArray's own Equals is by
        // reference, so the arrays are compared as sequences of value-equal descriptors).
        Assert.Equal(beforeSave.AsEnumerable(), afterLoad.AsEnumerable());
        Assert.True(AvailableActionsQuery.Same(beforeSave, afterLoad));
        Assert.Equal(LabourActivities.For(w, Cfg, Player).AsEnumerable(), LabourActivities.For(loaded, Cfg, Player).AsEnumerable());
        Assert.Contains(afterLoad, a => a.Domain == ActionDomain.Labour && a.Label == "Farming");
        // The knowledge-derived readings the surface rests on are equal too: unlocked capabilities,
        // knowledge-eligible entities and the best road class the issuer knows.
        Assert.Equal(ResearchQuery.UnlockedCapabilities(w, Research, Player), ResearchQuery.UnlockedCapabilities(loaded, Research, Player));
        Assert.Equal(ResearchQuery.KnowledgeEligibleEntities(w, Research, Player), ResearchQuery.KnowledgeEligibleEntities(loaded, Research, Player));
        Assert.Equal(EdgeTypes.Trackway, RoadDevelopmentQuery.BestKnownClass(loaded, Research, Cfg.Roads!, Player));
        Assert.Equal(RoadDevelopmentQuery.BestKnownClass(w, Research, Cfg.Roads!, Player), RoadDevelopmentQuery.BestKnownClass(loaded, Research, Cfg.Roads!, Player));
        // ...and it is derived from the saved ResearchCompleted table ALONE: take the row away from the loaded
        // world and the sector is "Gathering" again — nothing else carries the identity.
        loaded.ResearchCompleted.Clear();
        Assert.Equal("Gathering", LabourActivities.Of(loaded, Cfg, Player, loaded.Settlements[0].Id, Sectors.Farming)!.Label);
        // ...and the AI rival's surface too (a second polity's knowledge is saved state as well).
        WorldState duo = Production().Step(Complete(Duo(), Rival, "sheep_goat", "dog_domestication"));
        using var buffer2 = new MemoryStream();
        Snapshot.Save(duo, buffer2);
        buffer2.Position = 0;
        WorldState duoLoaded = Snapshot.Load(buffer2, duo.Terrain);
        Assert.True(AvailableActionsQuery.Same(AvailableActionsQuery.For(duo, Cfg, Rival), AvailableActionsQuery.For(duoLoaded, Cfg, Rival)));
        Assert.Contains(AvailableActionsQuery.For(duoLoaded, Cfg, Rival), a => a.Label == "Herding & fishing");
    }
}
