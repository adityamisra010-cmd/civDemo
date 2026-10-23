using System.Collections.Immutable;
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
    // M6 Track EX (2026-10-22): the discovery layer off (R4) — the rigs add copper/tin deposit rows, which the shipped
    // concealment may classify as hidden (MEASURED: "Expected: Available, Actual: InputUnobtainable").
    private static readonly SimConfig Cfg = TestConfigs.PreDiscovery(TestConfigs.Sim());
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

    // ------------------------------------------------------------------ D-051 §4 / Director 2026-10-10 — unobtainable crafted inputs

    private const int Granary = 1, Workshop = 2;

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

    private static void Artisans(WorldState w, SettlementId s, double share) =>
        w.Variables.Add(new VariableRow(s, Variables.ArtisanShare, share));

    [Fact]
    public void Workshop_AtFounding_IsNotALegalOrder_BecauseItsToolsHaveNoProductionPath_WhileTheGranaryIs()
    {
        // The Age I trap D-051 §4 names: the workshop needs 10 tools, tools come only from bronze toolmaking
        // (artisan-gated, bronze from tin_bronze, Age III), and no cancel order exists — so before this guard a turn-1
        // workshop order sat at the queue head and froze the settlement's construction for hundreds of turns (measured
        // 2026-10-10: 0 of 48 workshops in 150 turns on seeds 42/1/2/7, while granaries built in 46 of 48). The workshop
        // is now neither listed nor accepted; the granary (raw inputs only) is unaffected.
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        Assert.Equal(ProjectAvailability.InputUnobtainable, ConstructionQuery.Availability(w, Cfg, Player, s0, Workshop));
        Assert.Equal("tools", ConstructionQuery.UnobtainableInput(w, Cfg, s0, Cfg.Goods!.ProjectById(Workshop)!)!.Name);
        Assert.Equal(ProjectAvailability.Available, ConstructionQuery.Availability(w, Cfg, Player, s0, Granary));
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player);
        Assert.DoesNotContain(actions, a => a.Key == $"construction.{s0.Value}.workshop");
        Assert.Contains(actions, a => a.Key == $"construction.{s0.Value}.granary");

        var orders = new OrderLog();
        orders.Append(ConstructionQuery.EnqueueOrder(w, Player, s0, Workshop));
        WorldState after = Only(orders, SystemCatalog.Construction(Cfg)).Step(w);
        for (int i = 0; i < after.ConstructionQueue.Count; i++) Assert.NotEqual(Workshop, after.ConstructionQueue[i].ProjectId);
        for (int i = 0; i < after.Structures.Count; i++) Assert.NotEqual(Workshop, after.Structures[i].ProjectId);
    }

    [Fact]
    public void ARefusedWorkshopOrder_ChangesNothing_TheWorldIsByteIdenticalToNoOrder()
    {
        // The guard is a read-only predicate at order intake: a refused order moves no good, writes no row and touches
        // no ledger total, so the stepped world hashes exactly as the same step with no order at all.
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        var orders = new OrderLog();
        orders.Append(ConstructionQuery.EnqueueOrder(w, Player, s0, Workshop));
        WorldState refused = Only(orders, SystemCatalog.Construction(Cfg)).Step(w.Clone());
        WorldState none = Only(new OrderLog(), SystemCatalog.Construction(Cfg)).Step(w.Clone());
        Assert.Equal(WorldHash.ComputeHex(none), WorldHash.ComputeHex(refused));
    }

    [Fact]
    public void Workshop_BecomesLegal_ExactlyWhenToolsAreObtainable_HeldOrMadeThroughAnAvailableRecipeChain()
    {
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;

        // (1) Held: ten tools in store make the input obtainable whatever the recipes say; nine do not.
        WorldState held = w.Clone();
        TopUp(held, s0, "tools", 9);
        Assert.Equal(ProjectAvailability.InputUnobtainable, ConstructionQuery.Availability(held, Cfg, Player, s0, Workshop));
        TopUp(held, s0, "tools", 10);
        Assert.Equal(ProjectAvailability.Available, ConstructionQuery.Availability(held, Cfg, Player, s0, Workshop));

        // (2) One recipe deep: bronze in store, but toolmaking's D-020 artisan gate unmet — still unobtainable.
        WorldState bronze = w.Clone();
        TopUp(bronze, s0, "bronze", 1);
        Assert.Equal(ProjectAvailability.InputUnobtainable, ConstructionQuery.Availability(bronze, Cfg, Player, s0, Workshop));
        // With artisans present, toolmaking is available and its inputs (bronze held, timber held or deposited) are
        // obtainable: the workshop is legal, though not yet affordable — that stays a blocker, not a reason to hide it.
        Artisans(bronze, s0, 0.10);
        TopUp(bronze, s0, "timber", 1);
        Assert.Equal(ProjectAvailability.Available, ConstructionQuery.Availability(bronze, Cfg, Player, s0, Workshop));

        // (3) Two recipes deep: no bronze held — bronze-casting must be known (tin_bronze, with its ancestors) and its
        // ores present as deposits. Knowledge alone is not enough without the ores.
        WorldState chain = w.Clone();
        Artisans(chain, s0, 0.10);
        TopUp(chain, s0, "timber", 1);
        Complete(chain, Player, WithAncestors("tin_bronze"));
        int copper = Cfg.Goods!.IdOf("copper-ore"), tin = Cfg.Goods!.IdOf("tin-ore");
        var kept = new List<DepositRow>();
        for (int i = 0; i < chain.Deposits.Count; i++)
        {
            DepositRow d = chain.Deposits[i];
            if (d.Settlement == s0 && (d.Good.Value == copper || d.Good.Value == tin)) continue;
            kept.Add(d);
        }
        chain.Deposits.Clear();
        foreach (DepositRow d in kept) chain.Deposits.Add(d);
        Assert.Equal(ProjectAvailability.InputUnobtainable, ConstructionQuery.Availability(chain, Cfg, Player, s0, Workshop));
        chain.Deposits.Add(new DepositRow(s0, new GoodId(Cfg.Goods!.IdOf("copper-ore")), 0.5));
        chain.Deposits.Add(new DepositRow(s0, new GoodId(Cfg.Goods!.IdOf("tin-ore")), 0.5));
        Assert.Equal(ProjectAvailability.Available, ConstructionQuery.Availability(chain, Cfg, Player, s0, Workshop));
    }

    [Fact]
    public void TheGuard_IsAtEnqueueOnly_AWorkshopAlreadyAtTheQueueHead_StillBlocksIt_QueueSemanticsUnchanged()
    {
        // Director 2026-10-10: no cancellation mechanic, no change to queue-head semantics. A workshop already queued
        // (a save from before the guard) is resolved exactly as before: it stays at the head, unbuilt, and the granary
        // behind it waits — the guard never reaches into the queue.
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        TopUp(w, s0, "timber", 1000);
        TopUp(w, s0, "stone", 1000);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s0, 0, Workshop));
        w.ConstructionQueue.Add(new ConstructionQueueRow(s0, 1, Granary));
        WorldState next = Only(new OrderLog(), SystemCatalog.Construction(Cfg)).Step(w);
        Assert.Equal(2, next.ConstructionQueue.Count);
        Assert.Equal(Workshop, next.ConstructionQueue[0].ProjectId);
        Assert.Equal(0, next.Structures.Count);
    }
}
