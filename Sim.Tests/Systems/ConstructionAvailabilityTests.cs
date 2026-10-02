using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D3 — EnqueueConstruction gains its availability predicate: a project is AVAILABLE to an issuer in a
/// settlement iff the issuer controls it, goods.json defines the project, and the project's research entity
/// (goods.json projects[].entity — data, loader-validated) is knowledge-eligible for the issuer. ONE
/// predicate (ConstructionQuery.IsProjectAvailable), TWO callers: ConstructionSystem accepts the order on it,
/// and the action surface lists the project by it (ADR-033 D2) — making ADR-028's LOCKED / AVAILABLE
/// expressible. Affordability (materials, capacity) is a blocker on a listed project, never a reason to hide it.
/// </summary>
public class ConstructionAvailabilityTests
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

    private static void Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[Research.IndexOfId(id)].Key));
    }

    private static void TopUp(WorldState w, SettlementId s, string good, long target)
    {
        var id = new GoodId(Cfg.Goods!.IdOf(good));
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static TurnExecutor Only(OrderLog orders, params SystemRegistration[] systems) =>
        new(ResearchRigs.FlatEra(10.0), systems, orders);

    // ------------------------------------------------------------------ kind 4 — construction (ADR-033 D3)

    [Fact]
    public void GoodsJson_LinksEachProjectToItsResearchEntity_AndTheLoaderValidatesTheLink()
    {
        Assert.Equal("building.granary", Cfg.Goods!.ProjectById(1)!.Entity);
        Assert.Equal("building.workshop", Cfg.Goods!.ProjectById(2)!.Entity);
        SimConfigLoader.ValidateProjectsAgainstContent(Cfg.Goods, Research);   // the canonical link holds

        string goods;
        using (var stream = Sim.Data.DataFiles.OpenGoods()) goods = new StreamReader(stream).ReadToEnd();
        foreach ((string replacement, string fragment) in new[]
                 {
                     ("\"entity\": \"building.ghost\"", "names entity 'building.ghost', which research.json does not define"),
                     ("\"entity\": \"unit.spearmen\"", "of kind Unit"),
                     ("\"entity\": \"activity.farming\"", "of kind Activity"),
                 })
        {
            string bad = goods.Replace("\"entity\": \"building.granary\"", replacement, StringComparison.Ordinal);
            Assert.NotEqual(goods, bad);
            using var sim = Sim.Data.DataFiles.OpenSim();
            using var needs = Sim.Data.DataFiles.OpenNeeds();
            using var research = Sim.Data.DataFiles.OpenResearch();
            using var badGoods = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(bad));
            var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(sim, needs, badGoods, research));
            Assert.Contains(fragment, e.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EnqueueConstruction_AKnowledgeLockedProject_IsNeitherListedNorAccepted_UntilItsEntityIsEligible()
    {
        // A kiln project linked to building.kiln (requires kiln_updraft): LOCKED at founding, AVAILABLE after.
        GoodsConfig goods = Cfg.Goods!;
        SimConfig cfg = Cfg with
        {
            Goods = goods with
            {
                Projects = [.. goods.Projects!, new ConstructionProjectEntry(3, "kiln", [new ProjectInput("timber", 5)], 0.5, "building.kiln")],
            },
        };
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        var orders = new OrderLog();
        orders.Append(ConstructionQuery.EnqueueOrder(w, Player, s0, 3));

        Assert.Equal(ProjectAvailability.NotKnowledgeEligible, ConstructionQuery.Availability(w, cfg, Player, s0, 3));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, cfg, Player), a => a.Label == "Build Kiln");
        WorldState refused = Only(orders, SystemCatalog.Construction(cfg)).Step(w);
        Assert.Equal(0, refused.ConstructionQueue.Count + refused.Structures.Count);

        Complete(w, Player, "kiln_updraft");
        Assert.Equal(ProjectAvailability.Available, ConstructionQuery.Availability(w, cfg, Player, s0, 3));
        ActionDescriptor kiln = Assert.Single(AvailableActionsQuery.For(w, cfg, Player), a => a.Key == $"construction.{s0.Value}.kiln");
        Assert.Equal("Build Kiln", kiln.Label);
        Assert.Equal(["building.kiln"], kiln.Provenance.Entities.ToArray());
        Assert.Equal(["kiln_updraft"], kiln.Provenance.Nodes.Select(k => Research.Nodes[Research.IndexOf(k)].Id));
        Assert.True(kiln.Provenance.Researched);
        WorldState accepted = Only(orders, SystemCatalog.Construction(cfg)).Step(w);
        Assert.Equal(1, accepted.ConstructionQueue.Count + accepted.Structures.Count);   // queued (or built at once)
    }

    [Fact]
    public void EnqueueConstruction_InASettlementTheIssuerDoesNotRule_IsNeitherListedNorAccepted()
    {
        WorldState w = Duo();
        SettlementId rivals = w.Settlements[1].Id;
        Assert.True(EmpireQuery.ControlsSettlement(w, Rival, rivals));
        Assert.Equal(ProjectAvailability.NotControlled, ConstructionQuery.Availability(w, Cfg, Player, rivals, 1));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, Cfg, Player), a => a.Key == $"construction.{rivals.Value}.granary");
        var orders = new OrderLog();
        orders.Append(ConstructionQuery.EnqueueOrder(w, Player, rivals, 1));
        Assert.Equal(0, Only(orders, SystemCatalog.Construction(Cfg)).Step(w).ConstructionQueue.Count);
    }
}
