using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// D-051 §6 applied to construction (authority: D-051 §6 and D-053 §1 — the Director's 2026-10-13 instruction, item 1;
/// the M5 playtest: Bigen waited 62 turns and Shuze 180 for 20 stone while the empire held 128-384) — THE EMPIRE PURSE.
/// A settlement an Empire controls is checked against, and pays from, the
/// goods of every settlement that Empire controls (Sim.Core/State/EmpireStockpile.cs), drawn instantly and losslessly in
/// the stated donor order: the builder first, then nearest-first by exact integer squared site-cell distance, ties to the
/// lower settlement id. A stateless settlement, and every settlement of a Controls-less toy, is its own stockpile. The
/// switch is sim.json construction.empireStockpile (shipped ON); TestConfigs.PreEmpireConstructionStockpile switches it
/// off, and with the whole D-053 batch off (TestConfigs.PreD053) the world is de86ace byte for byte. One purse serves many
/// queues, so (switch on) the obtainability guard reads a CRAFTED input UNCLAIMED — less every queued project's inputs, every
/// order accepted earlier in the batch, and (on the action surface) the turn's pending orders — while a raw input of the
/// recipe chain keeps the pooled reading; the AI RESERVES the purse within one decision; every display of a settlement the
/// viewer does not rule reads that settlement's own stock (P7).
///
/// Most tests run ONLY ConstructionSystem (and RoadDevelopmentSystem where roads are the subject) on the founded dev world
/// (seed 42, four settlements, all the player's unless stated), with every stock that matters set exactly and the sites
/// placed on chosen pixels of the founded terrain, so each draw is computable by hand. The attribution controls run the
/// production pipeline.
/// </summary>
public class EmpireConstructionStockpileTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();                                       // shipped: ON
    private static readonly SimConfig Off = TestConfigs.PreEmpireConstructionStockpile(TestConfigs.Sim());
    private static readonly ResearchContent Research = TestConfigs.Research();
    private const int Granary = 1;
    private static readonly int Stone = Cfg.Goods!.IdOf("stone");
    private static readonly int Timber = Cfg.Goods!.IdOf("timber");
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private static readonly SettlementId S0 = new(0), S1 = new(1), S2 = new(2), S3 = new(3);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    /// <summary>The founded dev world (four settlements, all the player's), every stone and timber row at exactly 0.</summary>
    private static WorldState Solo() => Bare(DevSolo.Value.Clone());

    /// <summary>The founded dev world with one AI Empire: round-robin control, S0 and S2 the player's, S1 and S3 the AI's.</summary>
    private static WorldState Duo() => Bare(DevDuo.Value.Clone());

    private static WorldState Bare(WorldState w)
    {
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            Set(w, w.Settlements[s].Id, Stone, 0);
            Set(w, w.Settlements[s].Id, Timber, 0);
        }
        return w;
    }

    /// <summary>Sets one (settlement, good) stock to exactly <paramref name="qty"/> through the Ledger (find-or-add: every
    /// reader takes the FIRST row, so a duplicate row would be invisible).</summary>
    private static void Set(WorldState w, SettlementId s, int good, long qty)
    {
        var id = new GoodId(good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have == qty) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.InitialEndowment,
            Math.Abs(qty - have), qty > have ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    private static long Of(IReadOnlyWorldState w, SettlementId s, int good)
    {
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(good));
        return idx < 0 ? 0 : w.GoodStocks[idx].Amount.Value;
    }

    private static long TotalOf(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good.Value == good) sum += w.GoodStocks[i].Amount.Value;
        return sum;
    }

    /// <summary>Total sunk under ConstructionMaterials for one good (the Ledger's own counterweight).</summary>
    private static long Sunk(IReadOnlyWorldState w, int good)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.OfGood(new GoodId(good))
                && w.LedgerFlows[i].Reason == ReasonIds.ConstructionMaterials) return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    private static long Built(IReadOnlyWorldState w, SettlementId s) => ConstructionQuery.Built(w, s, Granary);

    /// <summary>Gives the four settlements the stated pixels of the founded terrain (the construction-only executor
    /// reads positions for the draw order and nothing else).</summary>
    private static WorldState Place(WorldState w, params (int X, int Y)[] at)
    {
        for (int s = 0; s < at.Length; s++)
            w.Settlements[s] = w.Settlements[s] with { SiteCell = w.Terrain!.Index(at[s].X, at[s].Y) };
        return w;
    }

    private static void AllBuilders(WorldState w, SettlementId s)
    {
        var row = new SectorAllocationRow(s, Farming: 0.0, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: 1.0);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    private static void NoBuilders(WorldState w, SettlementId s)
    {
        var row = new SectorAllocationRow(s, Farming: 1.0, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: 0.0);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    private static void Uncontrol(WorldState w, SettlementId s)
    {
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != s) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow r in kept) w.Controls.Add(r);
    }

    private static OrderRecord Enqueue(long turn, PolityId actor, SettlementId s) =>
        OrderRecord.From(turn, actor, OrderKind.EnqueueConstruction, s.Value, Granary);

    private static OrderLog Log(params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        return log;
    }

    private static TurnExecutor ConstructionOnly(SimConfig cfg, OrderLog? orders) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg)], orders);

    private static int[] Ids(SettlementId[] ids)
    {
        var r = new int[ids.Length];
        for (int i = 0; i < r.Length; i++) r[i] = ids[i].Value;
        return r;
    }

    private static void AssertConserved(IReadOnlyWorldState w)
    {
        Assert.True(ConservationAuditor.IsConserved(w, Cfg.Goods, out string report), report);
        for (int i = 0; i < w.GoodStocks.Count; i++) Assert.True(w.GoodStocks[i].Amount.Value >= 0, "a negative stock");
    }

    /// <summary>The builder S2 at (100,100); S0 and S3 one pixel away (a TIE — S0, the lower id, first); S1 two pixels
    /// away. Draw order [2, 0, 3, 1].</summary>
    private static WorldState TieWorld(WorldState w) => Place(w, (101, 100), (102, 100), (100, 100), (100, 101));

    // ------------------------------------------------------------------ the donor order

    [Fact]
    public void Holders_AreTheBuilderFirst_ThenNearestFirst_TiesToTheLowerId_TieDense_RowOrderFree()
    {
        WorldState w = TieWorld(Solo());
        Assert.Equal([2, 0, 3, 1], Ids(EmpireStockpile.Holders(w, S2)));
        Assert.Equal([2, 0, 3, 1], Ids(ConstructionQuery.Holders(w, Cfg, S2)));
        // With distinct site cells this IS ADR-032's road payment order with both route ends at the builder.
        Assert.Equal([2, 0, 3, 1], Ids(RoadDevelopmentQuery.PayingSettlements(w, Player, S2, S2)));

        // Tie-dense: every other holder one pixel from the builder → pure ascending id after the builder, and the
        // answer is a function of the row SET (the settlement rows permuted give the same order).
        WorldState ring = Place(Solo(), (101, 100), (100, 100), (100, 101), (99, 100));
        Assert.Equal([1, 0, 2, 3], Ids(EmpireStockpile.Holders(ring, S1)));
        (ring.Settlements[0], ring.Settlements[3]) = (ring.Settlements[3], ring.Settlements[0]);
        Assert.Equal([1, 0, 2, 3], Ids(EmpireStockpile.Holders(ring, S1)));

        // A terrain-less world has no positions: the builder, then ascending id. (ADR-032's road order would put S0
        // first here — every distance 0, ties by id; the builder-first pin is the one deliberate difference.)
        WorldState flat = Solo();
        flat.Terrain = null;
        Assert.Equal([2, 0, 1, 3], Ids(EmpireStockpile.Holders(flat, S2)));
        Assert.Equal([0, 1, 2, 3], Ids(RoadDevelopmentQuery.PayingSettlements(flat, Player, S2, S2)));

        // The switch off: the builder alone.
        Assert.Equal([2], Ids(ConstructionQuery.Holders(w, Off, S2)));
    }

    [Fact]
    public void ASettlementWithNoStone_BuildsAGranaryFromItsEmpire_ExactPerRowDraws_BuilderFirst_ThenNearest_TieToLowerId()
    {
        WorldState w = TieWorld(Solo());
        AllBuilders(w, S2);
        Set(w, S2, Timber, 40);       // the builder's own timber covers the timber: S0's is never touched
        Set(w, S0, Timber, 1_000);
        Set(w, S0, Stone, 5);         // tie at 1 px, lower id: drawn first
        Set(w, S3, Stone, 10);        // tie at 1 px, higher id: drawn second
        Set(w, S1, Stone, 100);       // 2 px: drawn last, for the remainder
        long stone0 = TotalOf(w, Stone), timber0 = TotalOf(w, Timber);

        WorldState next = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S2))).Step(w);

        Assert.Equal(1, Built(next, S2));
        Assert.Equal(0, next.ConstructionQueue.Count);
        Assert.Equal((0L, 1_000L), (Of(next, S2, Timber), Of(next, S0, Timber)));
        Assert.Equal((0L, 0L, 0L, 95L), (Of(next, S2, Stone), Of(next, S0, Stone), Of(next, S3, Stone), Of(next, S1, Stone)));
        // Lossless: exactly the project's cost left the empire, all of it through the Ledger under its reason.
        Assert.Equal((stone0 - 20, timber0 - 40), (TotalOf(next, Stone), TotalOf(next, Timber)));
        Assert.Equal((20L, 40L), (Sunk(next, Stone) - Sunk(w, Stone), Sunk(next, Timber) - Sunk(w, Timber)));
        AssertConserved(next);

        // THE CONTROL: the same world and order with the switch off — the builder's own stock alone, no stone, so the
        // head waits and nothing moves.
        WorldState off = ConstructionOnly(Off, Log(Enqueue(0, Player, S2))).Step(w);
        Assert.Equal(0, Built(off, S2));
        Assert.Equal(1, off.ConstructionQueue.Count);
        Assert.Equal((5L, 10L, 100L, 40L), (Of(off, S0, Stone), Of(off, S3, Stone), Of(off, S1, Stone), Of(off, S2, Timber)));
    }

    // ------------------------------------------------------------------ who is NOT an empire

    [Fact]
    public void AStatelessSettlement_IsItsOwnStockpile_ItNeverDrawsOnAnEmpire()
    {
        WorldState w = TieWorld(Solo());
        Uncontrol(w, S2);                                      // nobody rules S2 now
        AllBuilders(w, S2);
        Set(w, S2, Timber, 40);
        Set(w, S0, Stone, 1_000);                              // the empire next door has plenty
        w.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Granary));   // no Empire can order it: the queue row as saved
        Assert.Equal([2], Ids(ConstructionQuery.Holders(w, Cfg, S2)));
        Assert.Equal("needs 20 stone (has 0)", ConstructionQuery.Blocker(w, Cfg, S2, Cfg.Goods!.ProjectById(Granary)!, null));

        WorldState waits = ConstructionOnly(Cfg, null).Step(w);
        Assert.Equal(0, Built(waits, S2));
        Assert.Equal(1_000, Of(waits, S0, Stone));

        Set(w, S2, Stone, 20);                                 // its OWN stone: it builds, and only from itself
        WorldState builds = ConstructionOnly(Cfg, null).Step(w);
        Assert.Equal(1, Built(builds, S2));
        Assert.Equal((0L, 0L, 1_000L), (Of(builds, S2, Stone), Of(builds, S2, Timber), Of(builds, S0, Stone)));
        AssertConserved(builds);
    }

    [Fact]
    public void AWorldWithNoControlsAtAll_KeepsEachSettlementToItsOwnStock_ExactlyAsTheSwitchOffWorld()
    {
        WorldState w = TieWorld(Solo());
        w.Controls.Clear();                                    // a hand-built toy: no empire to pool over
        AllBuilders(w, S2);
        Set(w, S2, Timber, 40);
        Set(w, S0, Stone, 1_000);
        Assert.Equal([2], Ids(EmpireStockpile.Holders(w, S2)));
        // ...where road development treats every settlement as the issuer's (ADR-032's toy rule) — the documented difference.
        Assert.Equal([0, 1, 2, 3], Ids(RoadDevelopmentQuery.PayingSettlements(w, Player)));

        WorldState on = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S2))).Run(w, 2);
        WorldState off = ConstructionOnly(Off, Log(Enqueue(0, Player, S2))).Run(w, 2);
        Assert.Equal(0, Built(on, S2));
        Assert.Equal(1, on.ConstructionQueue.Count);           // accepted (the guard: no Controls to check) and waiting
        Assert.Equal(1_000, Of(on, S0, Stone));
        Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(on));
    }

    [Fact]
    public void AnotherEmpiresStone_IsNeverUsed()
    {
        // Round-robin control: S0, S2 the player's; S1, S3 the AI's — the AI's settlements are the NEAREST to S0.
        WorldState w = Place(Duo(), (100, 100), (101, 100), (130, 100), (100, 101));
        Assert.True(EmpireQuery.ControlsSettlement(w, Rival, S1) && EmpireQuery.ControlsSettlement(w, Rival, S3), "fixture: S1, S3 the AI's");
        AllBuilders(w, S0);
        Set(w, S0, Timber, 40);
        Set(w, S1, Stone, 1_000);
        Set(w, S3, Stone, 1_000);
        Assert.Equal([0, 2], Ids(ConstructionQuery.Holders(w, Cfg, S0)));
        Assert.Equal("needs 20 stone (empire has 0)", ConstructionQuery.Blocker(w, Cfg, S0, Cfg.Goods!.ProjectById(Granary)!, null));

        WorldState waits = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0))).Step(w);
        Assert.Equal(0, Built(waits, S0));
        Assert.Equal((1_000L, 1_000L), (Of(waits, S1, Stone), Of(waits, S3, Stone)));

        Set(w, S2, Stone, 20);                                 // the player's far settlement: its own empire's stone
        WorldState builds = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0))).Step(w);
        Assert.Equal(1, Built(builds, S0));
        Assert.Equal((0L, 1_000L, 1_000L), (Of(builds, S2, Stone), Of(builds, S1, Stone), Of(builds, S3, Stone)));
        AssertConserved(builds);
    }

    // ------------------------------------------------------------------ competing heads

    [Fact]
    public void TwoCompetingHeads_OverOnePurse_TheLowerIdBuilds_TheOtherWaits_NothingIsAllocatedTwice()
    {
        // Heads at S1 and S2 (granaries: 40 timber + 20 stone each); the purse holds timber for both, stone for ONE.
        // D-054 WS1: this pins the M4-D WHOLE-OR-NOTHING rule, which the switch keeps (TestConfigs.PreIncrementalConstruction).
        // Under incremental construction S2 would take the 10 left and progress half-way — the FIFO split is pinned in
        // IncrementalConstructionTests (competing heads, oldest order first).
        SimConfig whole = TestConfigs.PreIncrementalConstruction(Cfg);
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S1, S2 }) { AllBuilders(w, s); Set(w, s, Timber, 40); }
        Set(w, S3, Stone, 30);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Granary));   // S2's row FIRST in the queue table:
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));   // resolution follows the settlement table

        WorldState next = ConstructionOnly(whole, null).Step(w);
        Assert.Equal((1L, 0L), (Built(next, S1), Built(next, S2)));          // ascending id: S1 first
        Assert.Equal(10, Of(next, S3, Stone));                                // S1 drew 20 of the 30
        Assert.Equal((0L, 40L), (Of(next, S1, Timber), Of(next, S2, Timber))); // S2 drew nothing: whole or not at all
        ConstructionQueueRow waiting = Assert.Single(next.ConstructionQueue.Rows());
        Assert.Equal(S2, waiting.Settlement);
        Assert.Equal(20, Sunk(next, Stone) - Sunk(w, Stone));
        AssertConserved(next);

        // It keeps waiting on the 10 left, and builds the step the purse covers it.
        WorldState still = ConstructionOnly(whole, null).Step(next);
        Assert.Equal(0, Built(still, S2));
        Assert.Equal(10, Of(still, S3, Stone));
        Set(still, S0, Stone, 10);
        WorldState done = ConstructionOnly(whole, null).Step(still);
        Assert.Equal(1, Built(done, S2));
        Assert.Equal((0L, 0L), (Of(done, S0, Stone), Of(done, S3, Stone)));
        AssertConserved(done);
    }

    [Fact]
    public void TheCentralPurse_ReservesNothing_ALowerIdHeadMayBuildFromAHigherIdSettlementsOwnStone()
    {
        // S2 holds exactly one granary's stone ITSELF; S1 (lower id) holds none. One purse (D-051 §6: no per-city
        // stockpiles), resolved in ascending id: S1 builds from S2's stone and S2's own head waits.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S1, S2 }) { AllBuilders(w, s); Set(w, s, Timber, 40); }
        Set(w, S2, Stone, 20);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Granary));

        WorldState next = ConstructionOnly(Cfg, null).Step(w);
        Assert.Equal((1L, 0L), (Built(next, S1), Built(next, S2)));
        Assert.Equal((0L, 40L), (Of(next, S2, Stone) , Of(next, S2, Timber)));
        // Switch off: each its own — S2 builds, S1 waits.
        WorldState off = ConstructionOnly(Off, null).Step(w);
        Assert.Equal((0L, 1L), (Built(off, S1), Built(off, S2)));
    }

    // ------------------------------------------------------------------ construction before roads, one table

    [Fact]
    public void AProjectAndARoadInOneStep_TheProjectDrawsFirst_TheRoadSeesOnlyTheRemainder_NoUnitSpentTwice()
    {
        // pipeline.json: construction runs BEFORE roaddevelopment, and both draw on next.GoodStocks.
        string[] order;
        using (var stream = Sim.Data.DataFiles.OpenPipeline())
        {
            var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!["pipeline"]!.AsArray();
            order = new string[doc.Count];
            for (int i = 0; i < order.Length; i++) order[i] = (string)doc[i]!;
        }
        Assert.True(Array.IndexOf(order, ConstructionSystem.Name) >= 0
            && Array.IndexOf(order, ConstructionSystem.Name) < Array.IndexOf(order, Sim.Core.Systems.Roads.RoadDevelopmentSystem.Name));

        // Route S0–S1 (30 px), the player knows the trackway (timber per km). ALL the empire's timber sits at S2.
        WorldState Road()
        {
            WorldState w = Place(Solo(), (100, 100), (130, 100), (100, 105), (150, 150));
            w.SettlementDistances.Clear();                     // one candidate route, stated
            w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
            w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
            w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
            AllBuilders(w, S1);
            Set(w, S1, Stone, 20);
            return w;
        }
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(Road(), Research, Cfg.Roads!, Cfg.Goods!, Player, 100.0);
        RoadDevelopmentStep step = Assert.Single(plan);
        RoadMaterialCost cost = Assert.Single(step.Cost);
        Assert.Equal(Timber, cost.Good.Value);
        long c = cost.Units;
        Assert.True(c > 40, $"anti-vacuity: the road ({c} timber) must cost more than the granary's 40");

        // The empire holds EXACTLY the road's cost: alone, the road completes and spends it all.
        var both = new SystemRegistration[] { SystemCatalog.Construction(Cfg), SystemCatalog.RoadDevelopment(Cfg) };
        WorldState roadAlone = Road();
        Set(roadAlone, S2, Timber, c);
        WorldState r = new TurnExecutor(ResearchRigs.FlatEra(10.0), both, Log(RoadDevelopmentQuery.DevelopOrder(roadAlone, Player, 100.0))).Step(roadAlone);
        Assert.Equal(1.0, r.RoadDevelopments[0].ProgressAfter);
        Assert.Equal(c, r.RoadDevelopments[0].MaterialUnits);
        Assert.Equal(0, Of(r, S2, Timber));

        // With a granary ordered in the same step, the project draws its 40 FIRST; the road is paid only from what is left.
        WorldState w0 = Road();
        Set(w0, S2, Timber, c);
        long sunk0 = Sunk(w0, Timber);
        WorldState next = new TurnExecutor(ResearchRigs.FlatEra(10.0), both,
            Log(Enqueue(0, Player, S1), RoadDevelopmentQuery.DevelopOrder(w0, Player, 100.0))).Step(w0);
        Assert.Equal(1, Built(next, S1));
        RoadDevelopmentRow dev = Assert.Single(next.RoadDevelopments.Rows());
        Assert.True(dev.MaterialUnits <= c - 40, $"the road spent {dev.MaterialUnits} of the {c - 40} construction left");
        Assert.True(dev.MaterialUnits > 0 && dev.ProgressAfter < 1.0, "the road got the affordable proportion of the remainder");
        Assert.Equal(c - 40 - dev.MaterialUnits, Of(next, S2, Timber));
        Assert.Equal(40 + dev.MaterialUnits, Sunk(next, Timber) - sunk0);   // every unit spent once, through the Ledger
        AssertConserved(next);
    }

    // ------------------------------------------------------------------ delivery, replay, the AI

    [Fact]
    public void AnOrderStampedT_IsBuiltInTheStepFromT_FromASiblingsStone_TurnExact()
    {
        WorldState w = TieWorld(Solo());
        AllBuilders(w, S2);
        Set(w, S2, Timber, 40);
        Set(w, S0, Stone, 20);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(1, Player, S2)));   // stamped turn 1

        WorldState t1 = exec.Step(w);                          // step 0 → 1: the order is not yet delivered
        Assert.Equal(1, t1.Clock.Turn);
        Assert.Equal((0, 0L, 20L), (t1.ConstructionQueue.Count, Built(t1, S2), Of(t1, S0, Stone)));
        WorldState t2 = exec.Step(t1);                         // step 1 → 2: enqueued AND resolved, from S0's stone
        Assert.Equal((0, 1L, 0L, 0L), (t2.ConstructionQueue.Count, Built(t2, S2), Of(t2, S0, Stone), Of(t2, S2, Timber)));
    }

    [Fact]
    public void TheSameWorldAndOrders_ReplayToTheSameHash_OnTheProductionPipeline()
    {
        static (string Hash, long Built) RunOnce()
        {
            WorldState w = Solo();                             // the founded sites: the full pipeline reads them
            AllBuilders(w, S1);
            Set(w, S1, Timber, 400);
            Set(w, S3, Stone, 200);
            using var era = Sim.Data.DataFiles.OpenEraPacing();
            using var pipe = Sim.Data.DataFiles.OpenPipeline();
            var exec = new TurnExecutor(EraTableLoader.Load(era),
                PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.DevWorldgen())), Log(Enqueue(0, Player, S1)));
            WorldState final = exec.Run(w, 4);
            return (WorldHash.ComputeHex(final), Built(final, S1));
        }

        (string a, long builtA) = RunOnce();
        (string b, long builtB) = RunOnce();
        Assert.Equal(1, builtA);                               // anti-vacuity: the sibling-funded build happened
        Assert.Equal(builtA, builtB);
        Assert.Equal(a, b);
    }

    [Fact]
    public void TheAiPolicy_SeesThePurse_ItOrdersWhereTheEmpireCanPay_AndTheSystemBuildsIt()
    {
        // The AI holds S1 and S3. S1 has the timber, S3 the stone: neither could build alone.
        WorldState w = Place(Duo(), (100, 100), (110, 100), (130, 100), (112, 100));
        AllBuilders(w, S1);
        Set(w, S1, Timber, 40);
        Set(w, S3, Stone, 20);

        OrderRecord[] on = AiConstructionPolicy.Decide(w, Cfg, Rival);
        OrderRecord[] off = AiConstructionPolicy.Decide(w, Off, Rival);
        Assert.Empty(off);                                     // own stock only: nobody can afford a granary
        // ON: the purse covers ONE granary, so the AI orders exactly one — at S1, first in settlement-table order — and
        // reserves its cost: S3 is given nothing to wait on (D-053 §1; the reserved purse, AiConstructionPolicy item 3).
        Assert.Equal([ConstructionQuery.EnqueueOrder(w, Rival, S1, Granary)], on);

        WorldState next = ConstructionOnly(Cfg, Log(on)).Step(w);
        Assert.Equal((1L, 0L), (Built(next, S1), Built(next, S3)));     // S1 builds from S3's stone
        Assert.Equal((0L, 0L), (Of(next, S3, Stone), Of(next, S1, Timber)));
        Assert.Equal(0, next.ConstructionQueue.Count);                  // and no queue is left waiting
        // The player's order for the player's own settlement goes through the very same purse rule.
        Assert.Equal([0, 2], Ids(ConstructionQuery.Holders(w, Cfg, S0)));
        AssertConserved(next);
    }

    // ------------------------------------------------------------------ what the player is told

    [Fact]
    public void TheBlocker_SaysWhatThePurseHolds_EmpireForAControlledSettlement_HasForItsOwnStock()
    {
        WorldState w = TieWorld(Solo());
        Set(w, S0, Timber, 4);
        Set(w, S3, Timber, 6);
        Set(w, S1, Stone, 6);
        ConstructionProjectEntry granary = Cfg.Goods!.ProjectById(Granary)!;

        Assert.Equal("needs 40 timber (empire has 10), 20 stone (empire has 6)", ConstructionQuery.Blocker(w, Cfg, S2, granary, null));
        Assert.Equal("needs 40 timber (has 0), 20 stone (has 0)", ConstructionQuery.Blocker(w, Off, S2, granary, null));
        MaterialShortfall[] s = ConstructionQuery.Shortfalls(w, Cfg, S2, granary);
        Assert.Equal([(Timber, 40L, 10L), (Stone, 20L, 6L)], s.Select(x => (x.Good.Value, x.Required, x.Held)).ToArray());

        // The action surface and the project card read the same sentence.
        ActionDescriptor listed = AvailableActionsQuery.For(w, Cfg, Player, new ActionQueryContext(null, null))
            .Single(a => a.Domain == ActionDomain.Construction && a.Id == (((long)S2.Value << 32) | (uint)Granary));
        Assert.Equal("needs 40 timber (empire has 10), 20 stone (empire has 6)", listed.Blocker);
        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S2.Value), new ActionQueryContext(null, null));
        // D-054 WS1 (shipped): the card states the incremental rule and what the head will wait for — the same purse figures.
        Assert.Contains("It will wait for 40 timber (empire has 10), 20 stone (empire has 6).", card.StatusLine);
        InfoCard whole = InfoQuery.Card(w, TestConfigs.PreIncrementalConstruction(Cfg), Player, InfoSubject.OfProject(Granary, S2.Value), new ActionQueryContext(null, null));
        Assert.Contains("needs 40 timber (empire has 10), 20 stone (empire has 6)", whole.StatusLine);   // the switch-off sentence

        // Enough in the purse (and no turn length to judge labour by): nothing blocks.
        Set(w, S1, Timber, 30);
        Set(w, S1, Stone, 20);
        Assert.Null(ConstructionQuery.Blocker(w, Cfg, S2, granary, null));
        Assert.True(ConstructionQuery.MaterialsAvailable(w.GoodStocks, Cfg.Goods!, ConstructionQuery.Holders(w, Cfg, S2), granary));
        Assert.False(ConstructionQuery.MaterialsAvailable(w.GoodStocks, Cfg.Goods!, ConstructionQuery.Holders(w, Off, S2), granary));
    }

    // ------------------------------------------------------------------ v2: the card, the claims, the reserved purse

    private const int Workshop = 2;
    private static readonly int Tools = Cfg.Goods!.IdOf("tools");
    private static readonly int Bronze = Cfg.Goods!.IdOf("bronze");

    private static void Artisans(WorldState w, SettlementId s, double share) =>
        w.Variables.Add(new VariableRow(s, Variables.ArtisanShare, share));

    private static OrderRecord EnqueueWorkshop(long turn, PolityId actor, SettlementId s) =>
        OrderRecord.From(turn, actor, OrderKind.EnqueueConstruction, s.Value, Workshop);

    private static ProjectAvailability WorkshopAt(IReadOnlyWorldState w, SimConfig cfg, SettlementId s) =>
        ConstructionQuery.Availability(w, cfg, Player, s, Workshop);

    /// <summary>The project card's material lines: (label, Met, note), input order.</summary>
    /// <summary>D-054 WS1: the project card's status for an orderable project (InfoQuery.ProjectCard).</summary>
    private const string CanBeOrderedNow = "Can be ordered now. Work goes on each turn as far as materials and builders allow; "
        + "what has gone in is never lost while it waits, only if the project is cancelled.";

    /// <summary>D-054 WS1: the card's claims note — counts, never a set-aside. D-055 (2026-10-16, track T1b; the Director's
    /// batch-4 instruction, item 1 — batch-4 design §1.6): RE-PINNED TEXT. With the hold rule on a queue HEAD's claim IS set
    /// aside, so the parenthetical "(this does not set it aside: …)" became false for it: the count is stated bare and the
    /// set-asides are named after it (<see cref="SetAsideFor"/>). Batch 3's note is <see cref="CountedBatch3"/>, reproduced
    /// beside each re-pin under TestConfigs.PreReservations.</summary>
    private static string Counted(long n, string at) => "; " + n.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " counted toward projects already queued at " + at;

    /// <summary>Batch 3's claims note (the hold rule off: TestConfigs.PreReservations).</summary>
    private static string CountedBatch3(long n, string at) => Counted(n, at)
        + " (this does not set it aside: earlier projects in the building order, and road works under the split, can use it first)";

    /// <summary>D-055 (2026-10-16, T1b): the card's set-aside note — what the hold rule sets aside ahead of the entry.</summary>
    private static string SetAsideFor(long n, string heads) => "; " + n.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " set aside for " + heads + ", ahead in the building order";

    private static (string, bool?, string?)[] Materials(InfoCard card)
    {
        var lines = new List<(string, bool?, string?)>();
        foreach (InfoLink l in card.Requirements)
            if (l.Subject is { Kind: InfoKind.Good }) lines.Add((l.Label, l.Met, l.Note));
        return [.. lines];
    }

    [Fact]
    public void TheProjectCard_ReadsThePurse_ItsMaterialLinesAgreeWithTheBlocker_SwitchOnAndOff()
    {
        // Finding A1 (both verifiers): the card said "Can be built now." beside "20 stone | Met=False | has 0 at settlement 2".
        // The builder S2 holds nothing; its sibling S0 holds the whole granary.
        WorldState w = TieWorld(Solo());
        Set(w, S0, Timber, 400);
        Set(w, S0, Stone, 384);
        ConstructionProjectEntry granary = Cfg.Goods!.ProjectById(Granary)!;
        var ctx = new ActionQueryContext(null, null);
        Func<int, string> name = id => "S" + id;

        // ON: no blocker; the card agrees line by line — every material Met, the note the purse's holding.
        Assert.Null(ConstructionQuery.Blocker(w, Cfg, S2, granary, null));
        InfoCard on = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S2.Value), ctx, name);
        // D-054 WS1 (shipped): the incremental rule; with incremental construction off, M4-D's "Can be built now."
        Assert.Equal((InfoStatus.Available, CanBeOrderedNow), (on.Status, on.StatusLine));
        InfoCard onWhole = InfoQuery.Card(w, TestConfigs.PreIncrementalConstruction(Cfg), Player, InfoSubject.OfProject(Granary, S2.Value), ctx, name);
        Assert.Equal((InfoStatus.Available, "Can be built now."), (onWhole.Status, onWhole.StatusLine));
        Assert.Equal([("40 timber", (bool?)true, (string?)"empire has 400"), ("20 stone", true, "empire has 384")], Materials(on));
        Assert.Empty(on.WhyLocked);

        // OFF: the builder's own stock — the blocker and the card agree on that too.
        Assert.Equal("needs 40 timber (has 0), 20 stone (has 0)", ConstructionQuery.Blocker(w, Off, S2, granary, null));
        InfoCard off = InfoQuery.Card(w, Off, Player, InfoSubject.OfProject(Granary, S2.Value), ctx, name);
        Assert.Equal(InfoStatus.Blocked, off.Status);
        Assert.Equal(CanBeOrderedNow + " It will wait for 40 timber (has 0), 20 stone (has 0).", off.StatusLine);
        Assert.Equal([("40 timber", (bool?)false, (string?)"has 0 at S2"), ("20 stone", false, "has 0 at S2")], Materials(off));

        // A purse short of stone: the card names the shortfall exactly as the blocker does.
        Set(w, S0, Stone, 6);
        Assert.Equal("needs 20 stone (empire has 6)", ConstructionQuery.Blocker(w, Cfg, S2, granary, null));
        InfoCard short6 = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S2.Value), ctx, name);
        Assert.Equal(InfoStatus.Blocked, short6.Status);
        Assert.Equal([("40 timber", (bool?)true, (string?)"empire has 400"), ("20 stone", false, "empire has 6")], Materials(short6));
    }

    [Fact]
    public void TheProjectCard_OfAForeignSettlement_ReadsItsOwnStockOnly_ItsEmpiresOtherSettlementsStayUnread()
    {
        // P7: the AI's S1, seen by the player. Its pooled purse would sum S3 too (the AI's other settlement), which the
        // player's card must not read; S1's own stock is what the card shows of it.
        WorldState w = Place(Duo(), (100, 100), (110, 100), (130, 100), (112, 100));
        Set(w, S1, Stone, 5);
        Set(w, S3, Stone, 300);
        Set(w, S3, Timber, 300);
        Func<int, string> name = id => "S" + id;
        InfoCard Card(IReadOnlyWorldState world) =>
            InfoQuery.Card(world, Cfg, Player, InfoSubject.OfProject(Granary, S1.Value), new ActionQueryContext(null, null), name);
        Assert.Equal([0, 2], Ids(ConstructionQuery.HoldersSeenBy(w, Cfg, Player, S0)));   // the viewer's own: pooled
        Assert.Equal([1], Ids(ConstructionQuery.HoldersSeenBy(w, Cfg, Player, S1)));      // a foreign one: its own stock
        Assert.Equal([1, 3], Ids(ConstructionQuery.HoldersSeenBy(w, Cfg, Rival, S1)));    // ...which its ruler reads pooled

        InfoCard before = Card(w);
        Assert.Equal([("40 timber", (bool?)false, (string?)"has 0 at S1"), ("20 stone", false, "has 5 at S1")], Materials(before));
        WorldState moved = w.Clone();
        Set(moved, S3, Stone, 17);                                       // only the foreign sibling moves
        Set(moved, S3, Timber, 1);
        Assert.Equal(before, Card(moved));
        // Positive control: the foreign settlement's OWN stock is read.
        Set(moved, S1, Stone, 7);
        Assert.Equal([("40 timber", (bool?)false, (string?)"has 0 at S1"), ("20 stone", false, "has 7 at S1")], Materials(Card(moved)));
    }

    /// <summary>
    /// C1 review F6 (D-054 WS1: a queued entry's requirement is Met when the purse covers what IT still needs): the card of an
    /// ALREADY-QUEUED workshop does not count that entry's own claim against itself. S0's workshop is queued (nothing in yet);
    /// the empire's only ten tools are at S3: its tools line is Met (ten held, ten to go, no OTHER claim). A second workshop
    /// queued at S2 claims the same ten: now the line is not Met. Before the fix the first reading was Met = false (the entry's
    /// own ten were subtracted from the ten held).
    /// </summary>
    [Fact]
    public void TheProjectCard_OfAQueuedEntry_DoesNotCountItsOwnClaimAgainstItself()
    {
        WorldState w = TieWorld(Solo());
        Set(w, S3, Tools, 10);
        Set(w, S3, Timber, 200);
        Set(w, S3, Stone, 200);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Workshop));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Workshop, 0, 0));
        Func<int, string> name = id => "S" + id;
        (string, bool?, string?) ToolsLine(IReadOnlyWorldState world) =>
            Materials(InfoQuery.Card(world, Cfg, Player, InfoSubject.OfProject(Workshop, S0.Value), new ActionQueryContext(null, null), name))
                .Single(l => l.Item1.EndsWith("tools", StringComparison.Ordinal));
        Assert.Equal((bool?)true, ToolsLine(w).Item2);

        WorldState contested = w.Clone();
        contested.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Workshop));
        contested.ConstructionProgress.Add(new ConstructionProgressRow(S2, 0, Workshop, 0, 0));
        Assert.Equal((bool?)false, ToolsLine(contested).Item2);

        // A project not yet queued here is judged as the obtainability guard judges a NEW order: every queued claim counts.
        Assert.Equal((bool?)false, Materials(InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Workshop, S2.Value),
            new ActionQueryContext(null, null), name)).Single(l => l.Item1.EndsWith("tools", StringComparison.Ordinal)).Item2);
    }

    [Fact]
    public void TheGuard_ReadsThePurse_ASiblingsToolsMakeTheWorkshopLegal_AndASiblingsBronzeMakesTheChainProducible_OnNotOff()
    {
        // Mutant M5 (the guard reads the builder's own stock) survived v1: no test held the guard to the purse.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { Set(w, s, Tools, 0); Set(w, s, Bronze, 0); }

        // Direct: the only ten tools are at S3.
        WorldState tools = w.Clone();
        Set(tools, S3, Tools, 10);
        Assert.Equal((ProjectAvailability.Available, ProjectAvailability.InputUnobtainable), (WorkshopAt(tools, Cfg, S0), WorkshopAt(tools, Off, S0)));
        Set(tools, S3, Tools, 9);                                        // nine and no recipe: the permanent block, either way
        Assert.Equal((ProjectAvailability.InputUnobtainable, ProjectAvailability.InputUnobtainable), (WorkshopAt(tools, Cfg, S0), WorkshopAt(tools, Off, S0)));

        // One recipe deep: toolmaking runs at S0 (its artisans, its own timber), its bronze is held only at S3.
        WorldState chain = w.Clone();
        Artisans(chain, S0, 0.10);
        Set(chain, S0, Timber, 1);
        Assert.Equal(ProjectAvailability.InputUnobtainable, WorkshopAt(chain, Cfg, S0));   // anti-vacuity: no bronze anywhere
        Set(chain, S3, Bronze, 1);
        Assert.Equal((ProjectAvailability.Available, ProjectAvailability.InputUnobtainable), (WorkshopAt(chain, Cfg, S0), WorkshopAt(chain, Off, S0)));
        Assert.Null(ConstructionQuery.UnobtainableInput(chain, Cfg, S0, Cfg.Goods!.ProjectById(Workshop)!));
        Assert.Equal("tools", ConstructionQuery.UnobtainableInput(chain, Off, S0, Cfg.Goods!.ProjectById(Workshop)!)!.Name);
    }

    [Fact]
    public void TenToolsHeldOnce_AdmitOneWorkshop_TheNextOrderIsRefused_InTheSameBatchAndOnALaterTurn()
    {
        // Finding A2 (measured): S3 holds the only ten tools; S0 and S2 both ordered a workshop; S0 built and S2's head froze
        // for 51+ steps ("needs 10 tools (empire has 0)"), with no cancel order. The guard now reads the purse UNCLAIMED.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { AllBuilders(w, s); Set(w, s, Tools, 0); }
        Set(w, S3, Tools, 10);
        Set(w, S3, Timber, 1_000);
        Set(w, S3, Stone, 1_000);
        Assert.Equal((ProjectAvailability.Available, ProjectAvailability.Available), (WorkshopAt(w, Cfg, S0), WorkshopAt(w, Cfg, S2)));

        // ONE BATCH: the first order claims the ten tools at intake; the second is refused — S2's queue never holds it.
        WorldState batch = ConstructionOnly(Cfg, Log(EnqueueWorkshop(0, Player, S0), EnqueueWorkshop(0, Player, S2))).Step(w);
        Assert.Equal((1L, 0L), (ConstructionQuery.Built(batch, S0, Workshop), ConstructionQuery.Built(batch, S2, Workshop)));
        Assert.Equal(0, batch.ConstructionQueue.Count);                    // nothing frozen
        Assert.Equal(0, TotalOf(batch, Tools));
        AssertConserved(batch);
        // ...and with twenty tools, two are admitted and the third refused: the claims add up.
        WorldState twenty = w.Clone();
        Set(twenty, S3, Tools, 20);
        WorldState three = ConstructionOnly(Cfg, Log(EnqueueWorkshop(0, Player, S0), EnqueueWorkshop(0, Player, S1), EnqueueWorkshop(0, Player, S2))).Step(twenty);
        Assert.Equal((1L, 1L, 0L), (ConstructionQuery.Built(three, S0, Workshop), ConstructionQuery.Built(three, S1, Workshop), ConstructionQuery.Built(three, S2, Workshop)));
        Assert.Equal(0, three.ConstructionQueue.Count);

        // A LATER TURN: S0's workshop is queued and waiting (no builders); the ten tools are claimed — a second workshop
        // anywhere in the Empire, S0 included, is now InputUnobtainable, neither listed nor accepted.
        WorldState later = w.Clone();
        NoBuilders(later, S0);
        later.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Workshop));
        Assert.Equal((ProjectAvailability.InputUnobtainable, ProjectAvailability.InputUnobtainable), (WorkshopAt(later, Cfg, S2), WorkshopAt(later, Cfg, S0)));
        Assert.Equal("tools", ConstructionQuery.UnobtainableInput(later, Cfg, S2, Cfg.Goods!.ProjectById(Workshop)!)!.Name);
        Assert.Equal(10, ConstructionQuery.Claimed(later.ConstructionQueue, Cfg.Goods!, ConstructionQuery.Holders(later, Cfg, S2), new GoodId(Tools)));
        Assert.Equal(0, ConstructionQuery.Unclaimed(later.GoodStocks, later.ConstructionQueue, Cfg.Goods!, ConstructionQuery.Holders(later, Cfg, S2), new GoodId(Tools)));
        Assert.DoesNotContain(AvailableActionsQuery.For(later, Cfg, Player), a => a.Key == $"construction.{S2.Value}.workshop");
        WorldState refused = ConstructionOnly(Cfg, Log(EnqueueWorkshop(0, Player, S2))).Step(later);
        ConstructionQueueRow waiting = Assert.Single(refused.ConstructionQueue.Rows());
        Assert.Equal((S0, Workshop), (waiting.Settlement, waiting.ProjectId));   // S0's own, still waiting for builders
        Assert.Equal(10, TotalOf(refused, Tools));
        // ...the switch off reads no claims (de86ace): S0's own ten tools admit a second workshop at S0 itself.
        WorldState own = w.Clone();
        Set(own, S3, Tools, 0);
        Set(own, S0, Tools, 10);
        own.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Workshop));
        Assert.Equal((ProjectAvailability.Available, ProjectAvailability.InputUnobtainable), (WorkshopAt(own, Off, S0), WorkshopAt(own, Cfg, S0)));

        // A STATELESS settlement is its own purse: its own stock, its own queue — no Empire's claim touches it.
        WorldState free = w.Clone();
        Uncontrol(free, S2);
        Set(free, S2, Tools, 10);
        ConstructionProjectEntry workshop = Cfg.Goods!.ProjectById(Workshop)!;
        free.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Workshop));   // the Empire's claim on S3's ten
        Assert.Null(ConstructionQuery.UnobtainableInput(free, Cfg, S2, workshop));
        free.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Workshop));   // its own claim on its own ten
        Assert.Equal("tools", ConstructionQuery.UnobtainableInput(free, Cfg, S2, workshop)!.Name);
        Assert.Equal("tools", ConstructionQuery.UnobtainableInput(free, Cfg, S1, workshop)!.Name);   // the Empire's ten are claimed
    }

    [Fact]
    public void TheAi_TenToolsHeldOnce_OrdersOneWorkshop_AndNoQueueFreezes()
    {
        // Finding A2 (measured): the AI ordered 'S1:p2; S3:p2' against ten tools in one decision and S3 froze for 30+ steps.
        WorldState w = Place(Duo(), (100, 100), (110, 100), (130, 100), (112, 100));
        foreach (SettlementId s in new[] { S1, S3 })
        {
            AllBuilders(w, s);
            Set(w, s, Tools, 0);
            w.Structures.Add(new StructureRow(s, Granary, 1));   // the granary stands: the workshop is next
        }
        Set(w, S3, Tools, 10);
        Set(w, S3, Timber, 1_000);
        Set(w, S3, Stone, 1_000);

        OrderRecord[] on = AiConstructionPolicy.Decide(w, Cfg, Rival);
        Assert.Equal([OrderRecord.From(w.Clock.Turn, Rival, OrderKind.EnqueueConstruction, S1.Value, Workshop)], on);
        Assert.Equal([OrderRecord.From(w.Clock.Turn, Rival, OrderKind.EnqueueConstruction, S3.Value, Workshop)], AiConstructionPolicy.Decide(w, Off, Rival));

        // The AI loop over 30 steps: S1 builds from S3's tools, and nothing is ever left queued.
        var log = new OrderLog();
        TurnExecutor exec = ConstructionOnly(Cfg, log);
        WorldState cur = w;
        for (int t = 0; t < 30; t++)
        {
            foreach (OrderRecord o in AiConstructionPolicy.Decide(cur, Cfg, Rival)) log.Append(o);
            cur = exec.Step(cur);
            Assert.Equal(0, cur.ConstructionQueue.Count);
        }
        Assert.Equal((1L, 0L), (ConstructionQuery.Built(cur, S1, Workshop), ConstructionQuery.Built(cur, S3, Workshop)));
        Assert.Equal(1, log.Count);
        AssertConserved(cur);
    }

    [Fact]
    public void TheAi_ReservesThePurse_WithinOneDecision_AndAgainstEveryQueuedProject()
    {
        // The AI holds S1 and S3; the purse (all at S3) covers ONE granary.
        WorldState w = Place(Duo(), (100, 100), (110, 100), (130, 100), (112, 100));
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        OrderRecord Granary1 = ConstructionQuery.EnqueueOrder(w, Rival, S1, Granary), Granary3 = ConstructionQuery.EnqueueOrder(w, Rival, S3, Granary);
        Assert.Equal([Granary1], AiConstructionPolicy.Decide(w, Cfg, Rival));   // settlement-table order: S1 first
        Assert.Equal([Granary3], AiConstructionPolicy.Decide(w, Off, Rival));   // own stock: only S3 can pay
        // One unit short of two granaries: still one. Exactly two: both.
        Set(w, S3, Timber, 79);
        Set(w, S3, Stone, 40);
        Assert.Equal([Granary1], AiConstructionPolicy.Decide(w, Cfg, Rival));
        Set(w, S3, Timber, 80);
        Assert.Equal([Granary1, Granary3], AiConstructionPolicy.Decide(w, Cfg, Rival));

        // A project already QUEUED in the Empire (S1's granary, waiting) claims its cost: with a purse for one more, S3 is
        // ordered; with a purse for only the queued one, nothing is.
        WorldState queued = w.Clone();
        queued.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        Assert.Equal([Granary3], AiConstructionPolicy.Decide(queued, Cfg, Rival));
        Set(queued, S3, Timber, 40);
        Assert.Empty(AiConstructionPolicy.Decide(queued, Cfg, Rival));
        Assert.True(ConstructionQuery.MaterialsAvailable(queued.GoodStocks, Cfg.Goods!, ConstructionQuery.Holders(queued, Cfg, S3), Cfg.Goods!.ProjectById(Granary)!),
            "anti-vacuity: the unreserved purse would cover S3's granary");
    }

    // ------------------------------------------------------------------ v3: what the claims refuse, and say; any slot; raw inputs; pending orders

    private static readonly Func<int, string> SName = id => "S" + id;

    private static InfoCard WorkshopCard(IReadOnlyWorldState w, SimConfig cfg, SettlementId s, ActionQueryContext? ctx = null) =>
        InfoQuery.Card(w, cfg, Player, InfoSubject.OfProject(Workshop, s.Value), ctx ?? new ActionQueryContext(null, null), SName);

    [Fact]
    public void AClaimedCraftedInput_IsNamedAsClaimed_OnTheCardAndItsRequirementsLine_AGenuineShortageKeepsTheOldReason()
    {
        // A-1: the purse HOLDS the ten tools (at S3) but S0's queued workshop already claims them — the card must say so,
        // not "S2 cannot make yet" (S2 can make nothing, but the empire is not short: its tools are spoken for).
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { Set(w, s, Tools, 0); Set(w, s, Bronze, 0); }
        Set(w, S3, Tools, 10);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Workshop));
        Assert.Equal(ProjectAvailability.InputUnobtainable, WorkshopAt(w, Cfg, S2));

        InfoCard card = WorkshopCard(w, Cfg, S2);
        Assert.Equal(InfoStatus.Locked, card.Status);
        Assert.Equal("Cannot be ordered yet: it needs 10 tools; the empire's 10 are already claimed by projects queued at S0.", card.StatusLine);
        Assert.Equal("needs 10 tools; the empire's 10 are already claimed by projects queued at S0", card.Summary);
        Assert.Equal("needs 10 tools; the empire's 10 are already claimed by projects queued at S0",
            ConstructionQuery.ClaimedReason(w, Cfg, S2, Cfg.Goods!.ProjectById(Workshop)!, w.ConstructionQueue, SName));
        // The crafted input's line is Met against the UNCLAIMED holding, and its note says so; the raw lines keep the pool.
        // D-054 WS1 (shipped): the note says the claim is a count, not a set-aside; the switch-off note keeps "unclaimed".
        SimConfig whole = TestConfigs.PreIncrementalConstruction(Cfg);
        // D-055 (2026-10-16, T1b): re-pinned text — S0's workshop is its queue's head, so (the hold rule on) it SETS ASIDE the
        // ten tools: the note names that set-aside after the count; batch 3's note reproduces under PreReservations (`pre`).
        SimConfig pre = TestConfigs.PreReservations(Cfg);
        (string, bool?, string?) tools = Materials(card).Single(l => l.Item1 == "10 tools");
        Assert.Equal(("10 tools", (bool?)false, (string?)("empire has 10" + Counted(10, "S0") + SetAsideFor(10, "S0's Workshop"))), tools);
        Assert.Equal(("10 tools", (bool?)false, (string?)("empire has 10" + CountedBatch3(10, "S0"))),
            Materials(WorkshopCard(w, pre, S2)).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("10 tools", (bool?)false, (string?)"empire has 10; 10 claimed by projects queued at S0, 0 unclaimed"),
            Materials(WorkshopCard(w, whole, S2)).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("60 timber", (bool?)false, (string?)"empire has 0"), Materials(card).Single(l => l.Item1 == "60 timber"));

        // Partly claimed: fifteen held, ten claimed — five unclaimed is still short of ten.
        Set(w, S3, Tools, 15);
        InfoCard partly = WorkshopCard(w, Cfg, S2);
        Assert.Equal("Cannot be ordered yet: it needs 10 tools; 10 of the empire's 15 are already claimed by projects queued at S0.", partly.StatusLine);
        Assert.Equal(("10 tools", (bool?)false, (string?)("empire has 15" + Counted(10, "S0") + SetAsideFor(10, "S0's Workshop"))),
            Materials(partly).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("10 tools", (bool?)false, (string?)("empire has 15" + CountedBatch3(10, "S0"))),
            Materials(WorkshopCard(w, pre, S2)).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("10 tools", (bool?)false, (string?)"empire has 15; 10 claimed by projects queued at S0, 5 unclaimed"),
            Materials(WorkshopCard(w, whole, S2)).Single(l => l.Item1 == "10 tools"));
        // Two queues claim it: both are named, ascending id.
        WorldState two = w.Clone();
        Set(two, S3, Tools, 25);
        two.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Workshop));
        Assert.Equal("Cannot be ordered yet: it needs 10 tools; 20 of the empire's 25 are already claimed by projects queued at S0, S1.",
            WorkshopCard(two, Cfg, S2).StatusLine);

        // Twenty held, ten claimed: the ten unclaimed admit it — the line is Met, and still names the claim.
        Set(w, S3, Tools, 20);
        Assert.Equal(ProjectAvailability.Available, WorkshopAt(w, Cfg, S2));
        InfoCard enough = WorkshopCard(w, Cfg, S2);
        Assert.Equal(("10 tools", (bool?)true, (string?)("empire has 20" + Counted(10, "S0") + SetAsideFor(10, "S0's Workshop"))),
            Materials(enough).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("10 tools", (bool?)true, (string?)("empire has 20" + CountedBatch3(10, "S0"))),
            Materials(WorkshopCard(w, pre, S2)).Single(l => l.Item1 == "10 tools"));
        Assert.Equal(("10 tools", (bool?)true, (string?)"empire has 20; 10 claimed by projects queued at S0, 10 unclaimed"),
            Materials(WorkshopCard(w, whole, S2)).Single(l => l.Item1 == "10 tools"));
        Assert.Null(ConstructionQuery.ClaimedReason(w, Cfg, S2, Cfg.Goods!.ProjectById(Workshop)!, w.ConstructionQueue, SName));

        // A GENUINE shortage (nine tools, no claim, no recipe): the claims are not the reason — the older sentence stands.
        WorldState nine = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { Set(nine, s, Tools, 0); Set(nine, s, Bronze, 0); }
        Set(nine, S3, Tools, 9);
        Assert.Null(ConstructionQuery.ClaimedReason(nine, Cfg, S2, Cfg.Goods!.ProjectById(Workshop)!, nine.ConstructionQueue, SName));
        Assert.Equal("Cannot be ordered yet: it needs tools, which S2 cannot make yet.", WorkshopCard(nine, Cfg, S2).StatusLine);
        // Switch off: no claims at all — S2's own stock (none), the older sentence.
        InfoCard off = WorkshopCard(w, Off, S2);
        Assert.Equal("Cannot be ordered yet: it needs tools, which S2 cannot make yet.", off.StatusLine);
        Assert.Equal(("10 tools", (bool?)false, (string?)"has 0 at S2"), Materials(off).Single(l => l.Item1 == "10 tools"));
    }

    [Fact]
    public void TheClaims_CountAProjectQueuedAtANonHeadSlot_AndTheAiHonoursThem()
    {
        // A-2 (mutant MA2k, "Claimed skips Slot > 0", survived v2): S0's queue is [granary (head), workshop (slot 1)]; the
        // only ten tools are at S3. The waiting workshop is unbuilt, so its ten tools are claimed although it is not the head.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) Set(w, s, Tools, 0);
        Set(w, S3, Tools, 10);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 1, Workshop));
        Assert.Equal(10, ConstructionQuery.Claimed(w.ConstructionQueue, Cfg.Goods!, ConstructionQuery.Holders(w, Cfg, S2), new GoodId(Tools)));
        Assert.Equal(ProjectAvailability.InputUnobtainable, WorkshopAt(w, Cfg, S2));
        // Anti-vacuity: with the workshop not queued, S2's workshop is legal.
        WorldState headOnly = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) Set(headOnly, s, Tools, 0);
        Set(headOnly, S3, Tools, 10);
        headOnly.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        Assert.Equal(ProjectAvailability.Available, WorkshopAt(headOnly, Cfg, S2));

        // The AI counterpart: the rival's S1 queue is [granary (head), workshop (slot 1)], S3 holds the ten tools and has its
        // granary — S3's own workshop would be the next project, but the waiting workshop at S1 claims the tools: no order.
        WorldState ai = Place(Duo(), (100, 100), (110, 100), (130, 100), (112, 100));
        foreach (SettlementId s in new[] { S1, S3 }) { AllBuilders(ai, s); Set(ai, s, Tools, 0); }
        ai.Structures.Add(new StructureRow(S3, Granary, 1));
        Set(ai, S3, Tools, 10);
        Set(ai, S3, Timber, 1_000);
        Set(ai, S3, Stone, 1_000);
        WorldState aiHeadOnly = ai.Clone();
        ai.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        ai.ConstructionQueue.Add(new ConstructionQueueRow(S1, 1, Workshop));
        Assert.Empty(AiConstructionPolicy.Decide(ai, Cfg, Rival));
        // Anti-vacuity: with only the granary queued at S1, the AI orders S3's workshop from S3's tools.
        aiHeadOnly.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        Assert.Equal([ConstructionQuery.EnqueueOrder(aiHeadOnly, Rival, S3, Workshop)], AiConstructionPolicy.Decide(aiHeadOnly, Cfg, Rival));
    }

    [Fact]
    public void TheCard_NamesThePurseInItsWhyLocked_AndItsTreeNodeIsNotTheToolsChainWhenASiblingHoldsTheTools()
    {
        // A-3 (MA1c): no tools anywhere in the Empire — the why-locked line reads the EMPIRE's holding ("(empire has 0)").
        WorldState none = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { Set(none, s, Tools, 0); Set(none, s, Bronze, 0); }
        InfoCard locked = WorkshopCard(none, Cfg, S2);
        InfoLink why = Assert.Single(locked.WhyLocked, l => l.Subject is { Kind: InfoKind.Good } g && g.Id == Tools);
        Assert.StartsWith("10 tools (empire has 0): tools <- ", why.Label);
        int toolsNode = InfoQuery.SourceChainNode(none, Cfg, Player, "tools");
        Assert.True(toolsNode >= 0, "anti-vacuity: the tools chain ends at a research node");
        ResearchNodeId toolsKey = Research.Nodes[toolsNode].Key;
        Assert.Equal(toolsKey, locked.ResearchNode);                        // short of tools: the card points at their chain

        // A-3 (MA1d): only a SIBLING (S3) holds the ten tools — the purse is not short of them, so the card's tree node is
        // not the tools chain's research node, and no why-locked line names tools.
        WorldState sibling = none.Clone();
        Set(sibling, S3, Tools, 10);
        InfoCard held = WorkshopCard(sibling, Cfg, S2);
        Assert.NotEqual((ResearchNodeId?)toolsKey, held.ResearchNode);
        Assert.DoesNotContain(held.WhyLocked, l => l.Subject is { Kind: InfoKind.Good } g && g.Id == Tools);
        Assert.Equal(("10 tools", (bool?)true, (string?)"empire has 10"), Materials(held).Single(l => l.Item1 == "10 tools"));
    }

    [Fact]
    public void ARawInputOfTheChain_ReadsThePooledPurse_AGranaryClaimingTheTimberDoesNotMakeTheWorkshopUnobtainable()
    {
        // A-4 (the orchestrator's rule, MA2g): claims apply only to CRAFTED goods. S0 runs toolmaking (artisans; bronze held
        // at S3, one timber of its own, NO timber deposit), and a granary queued at S1 claims forty timber — far more than the
        // purse's one. Timber is raw: it is felled again, so the shortage is transient and the workshop is not refused.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { Set(w, s, Tools, 0); Set(w, s, Bronze, 0); Set(w, s, Timber, 0); }
        Artisans(w, S0, 0.10);
        Set(w, S0, Timber, 1);
        Set(w, S3, Bronze, 1);
        var kept = new List<DepositRow>();
        for (int i = 0; i < w.Deposits.Count; i++)
            if (!(w.Deposits[i].Settlement == S0 && w.Deposits[i].Good.Value == Timber)) kept.Add(w.Deposits[i]);
        w.Deposits.Clear();
        foreach (DepositRow r in kept) w.Deposits.Add(r);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        Assert.Equal(40, ConstructionQuery.Claimed(w.ConstructionQueue, Cfg.Goods!, ConstructionQuery.Holders(w, Cfg, S0), new GoodId(Timber)));
        Assert.Equal(-39, ConstructionQuery.Unclaimed(w.GoodStocks, w.ConstructionQueue, Cfg.Goods!, ConstructionQuery.Holders(w, Cfg, S0), new GoodId(Timber)));

        Assert.NotEqual(ProjectAvailability.InputUnobtainable, WorkshopAt(w, Cfg, S0));
        Assert.Equal(ProjectAvailability.Available, WorkshopAt(w, Cfg, S0));
        Assert.Null(ConstructionQuery.UnobtainableInput(w, Cfg, S0, Cfg.Goods!.ProjectById(Workshop)!));
        InfoCard card = WorkshopCard(w, Cfg, S0);
        Assert.DoesNotContain("cannot make yet", card.StatusLine);
        Assert.Equal(InfoStatus.Blocked, card.Status);                      // a materials shortfall, the transient blocker
        // Anti-vacuity: without the one timber the chain has no timber source at all (no stock, no deposit) — refused.
        Set(w, S0, Timber, 0);
        Assert.Equal(ProjectAvailability.InputUnobtainable, WorkshopAt(w, Cfg, S0));
    }

    [Fact]
    public void ThePendingBatch_ClaimsAsTheIntakeWould_TheListingOffersExactlyWhatTheIntakeWouldAccept()
    {
        // A-5: the action surface passes the turn's pending orders (ActionQueryContext.Queued). A workshop ordered this turn
        // claims its ten tools at once, so a second same-turn workshop is not offered — and the listing agrees with the
        // intake order by order.
        WorldState w = TieWorld(Solo());
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) { AllBuilders(w, s); Set(w, s, Tools, 0); }
        Set(w, S3, Tools, 10);
        Set(w, S3, Timber, 1_000);
        Set(w, S3, Stone, 1_000);
        static bool Listed(IReadOnlyWorldState world, SettlementId s, IReadOnlyList<OrderRecord> pending) =>
            AvailableActionsQuery.For(world, Cfg, Player, new ActionQueryContext(pending, null)).Any(a => a.Key == $"construction.{s.Value}.workshop");
        bool Accepted(IReadOnlyList<OrderRecord> pending, SettlementId s)
        {
            var all = new List<OrderRecord>(pending) { EnqueueWorkshop(0, Player, s) };
            WorldState next = ConstructionOnly(Cfg, Log([.. all])).Step(w);
            long built = ConstructionQuery.Built(next, s, Workshop);
            int queued = 0;
            for (int i = 0; i < next.ConstructionQueue.Count; i++)
                if (next.ConstructionQueue[i].Settlement == s && next.ConstructionQueue[i].ProjectId == Workshop) queued++;
            long before = 0;
            foreach (OrderRecord o in pending) if (o.TargetId == s.Value) before++;
            return built + queued > before;
        }

        OrderRecord[] none = [];
        OrderRecord[] one = [EnqueueWorkshop(0, Player, S0)];
        foreach (SettlementId s in new[] { S0, S1, S2, S3 })
        {
            Assert.True(Listed(w, s, none), $"S{s.Value}: offered with nothing pending");
            Assert.Equal(Accepted(none, s), Listed(w, s, none));
            Assert.False(Listed(w, s, one), $"S{s.Value}: the pending workshop claims the ten tools");
            Assert.Equal(Accepted(one, s), Listed(w, s, one));
        }
        // The card reads the same pending claims and names where they are.
        InfoCard card = WorkshopCard(w, Cfg, S2, new ActionQueryContext(one, null));
        Assert.Equal("Cannot be ordered yet: it needs 10 tools; the empire's 10 are already claimed by projects queued at S0.", card.StatusLine);

        // A pending order the intake would REFUSE claims nothing: with ten tools, of [S0, S1] only S0's is accepted, so a
        // third settlement's workshop is still refused (S0's ten) — and with twenty, [S0, S1] are both accepted and claim all.
        OrderRecord[] two = [EnqueueWorkshop(0, Player, S0), EnqueueWorkshop(0, Player, S1)];
        Assert.Equal(10, ConstructionQuery.Claimed(ConstructionQuery.QueueWithPending(w, Cfg, two), Cfg.Goods!, ConstructionQuery.Holders(w, Cfg, S2), new GoodId(Tools)));
        WorldState twenty = w.Clone();
        Set(twenty, S3, Tools, 20);
        Assert.Equal(20, ConstructionQuery.Claimed(ConstructionQuery.QueueWithPending(twenty, Cfg, two), Cfg.Goods!, ConstructionQuery.Holders(twenty, Cfg, S2), new GoodId(Tools)));
        Assert.True(Listed(twenty, S2, one));
        Assert.False(Listed(twenty, S2, two));
        // Switch off: no claims, the pending batch changes nothing (S3's own ten tools admit S3's workshop either way).
        Assert.Same(w.ConstructionQueue, ConstructionQuery.QueueWithPending(w, Off, one));
        Assert.Contains(AvailableActionsQuery.For(w, Off, Player, new ActionQueryContext(one, null)), a => a.Key == "construction.3.workshop");
    }

    private static readonly Lazy<WorldState> UniversityDuoAt3 = new(() =>
        UniversityRigs.Production(UniversityRigs.Cfg()).Run(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, UniversityRigs.Cfg(), 42), 3));

    [Fact]
    public void TheAiUniversitySite_IsJudgedAgainstTheReservedPurse_NotTheHostsOwnStock()
    {
        // Mutant M8 (the site check reads the host's own stock) survived v1. The rival's two settlements can each host a
        // university (knowledge, market, food, labour); the 1800 timber and 1200 stone lie only at the site the AI would
        // NOT pick (the smaller spare market).
        SimConfig cfg = UniversityRigs.Cfg(adultsPerUniversity: 100), off = TestConfigs.PreEmpireConstructionStockpile(cfg);
        WorldState w = UniversityDuoAt3.Value.Clone();
        UniversityRigs.Grant(w, Research, Rival);
        SettlementId[] sites = LabourActivities.ControlledSettlements(w, Rival);
        Assert.Equal(2, sites.Length);
        foreach (SettlementId s in sites)
        {
            UniversityRigs.Construction(w, s, 0.5);
            UniversityRigs.Surplus(w, s, 2.0);
            w.Structures.Add(new StructureRow(s, Granary, 1));
            Set(w, s, Timber, 0);
            Set(w, s, Stone, 0);
            Set(w, s, Tools, 0);
        }
        long a0 = BandViews.Adults(w.Buckets, sites[0]), a1 = BandViews.Adults(w.Buckets, sites[1]);
        SettlementId best = a0 > a1 ? sites[0] : a1 > a0 ? sites[1] : (sites[0].Value < sites[1].Value ? sites[0] : sites[1]);
        SettlementId other = best == sites[0] ? sites[1] : sites[0];
        Set(w, other, Timber, 1_800);
        Set(w, other, Stone, 1_200);
        double dt = w.Clock.DtYears;
        int military = InstitutionContent.ProjectOfType(cfg, 1)!.Id;

        // ON: the purse covers it wherever it is built — the best site, which holds nothing itself.
        Assert.Equal([ConstructionQuery.EnqueueOrder(w, Rival, best, military)], AiConstructionPolicy.Decide(w, cfg, Rival, dt));
        // OFF: only the host's own stock — the other site.
        Assert.Equal([ConstructionQuery.EnqueueOrder(w, Rival, other, military)], AiConstructionPolicy.Decide(w, off, Rival, dt));

        // RESERVED within the decision: a granary ordered first at the other site (its granary removed) takes 40 timber and
        // 20 stone of the purse, and the university no longer fits — until the purse holds both.
        WorldState both = w.Clone();
        var kept = new List<StructureRow>();
        for (int i = 0; i < both.Structures.Count; i++)
            if (!(both.Structures[i].Settlement == other && both.Structures[i].ProjectId == Granary)) kept.Add(both.Structures[i]);
        both.Structures.Clear();
        foreach (StructureRow r in kept) both.Structures.Add(r);
        OrderRecord granary = ConstructionQuery.EnqueueOrder(both, Rival, other, Granary);
        Assert.Equal([granary], AiConstructionPolicy.Decide(both, cfg, Rival, dt));
        Set(both, other, Timber, 1_840);
        Set(both, other, Stone, 1_220);
        Assert.Equal([granary, ConstructionQuery.EnqueueOrder(both, Rival, best, military)], AiConstructionPolicy.Decide(both, cfg, Rival, dt));
    }

    [Fact]
    public void Draw_IsTheWholeQuantityOrAThrow_AHolderListedTwiceCannotShortTheCharge()
    {
        // Finding A5: the roads' post-condition ("left != 0" after the loop) was lost in the move. Measured on v1: holders
        // [S0, S0] with S0 holding 10 and a charge of 15 drew 10 and returned.
        WorldState w = Solo();
        Set(w, S0, Stone, 10);
        var stone = new GoodId(Stone);
        Assert.Throws<InvalidOperationException>(() =>
            EmpireStockpile.Draw(new Ledger(w.LedgerFlows), w.GoodStocks, [S0, S0], stone, 15, ReasonIds.ConstructionMaterials));
        // A purse short even by its own (over-counted) sum is refused before any flow: nothing moves.
        WorldState refused = Solo();
        Set(refused, S0, Stone, 10);
        Assert.Throws<LedgerOverdrawException>(() =>
            EmpireStockpile.Draw(new Ledger(refused.LedgerFlows), refused.GoodStocks, [S0, S0], stone, 25, ReasonIds.ConstructionMaterials));
        Assert.Equal(10, Of(refused, S0, Stone));
        // A proper purse draws exactly the charge, donor by donor.
        WorldState ok = Solo();
        Set(ok, S0, Stone, 10);
        Set(ok, S1, Stone, 10);
        EmpireStockpile.Draw(new Ledger(ok.LedgerFlows), ok.GoodStocks, [S0, S1], stone, 15, ReasonIds.ConstructionMaterials);
        Assert.Equal((0L, 5L), (Of(ok, S0, Stone), Of(ok, S1, Stone)));
    }

    /// <summary>The gate rig: two granaries queued with no progress rows (EnqueuedTurn −1 for both, so the tie goes to the
    /// lower settlement id: S1 first); S1 has no builders, S2 every builder; the purse holds exactly one granary's 40 timber
    /// and 20 stone, at S3.</summary>
    private static WorldState GateWorld()
    {
        WorldState w = TieWorld(Solo());
        NoBuilders(w, S1);
        AllBuilders(w, S2);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S2, 0, Granary));
        return w;
    }

    /// <summary>
    /// Finding A6 (the wording): heads are tried in ascending order, each against what earlier heads left; a head that
    /// fails a gate (here: no builders at S1) draws nothing.
    ///
    /// D-055 RE-PIN (2026-10-16; the Director's batch-4 instruction, item 1 — "projects must not consume resources reserved
    /// by other projects"; batch-4 design §1.7, a DEFECT pin; MEASURED on this tree by the agent writing this line). This
    /// test was <c>ALowerIdHeadThatFailsAGate_DrawsNothing_AndBlocksNoHigherIdHead</c>: S2 took the purse's 40 timber and 20
    /// stone and completed while S1, first in the building order, waited for builders — built (S1, S2) = (0, 1), S3 left
    /// (0, 0), one queue row. That is the batch-3 defect (D-054 Q17). CAUSE: the hold rule (sim.json
    /// construction.reservations; ConstructionQuery.HoldsWalk) — "no builders" with adults present is a VALID wait (the
    /// player's labour allocation, not a released reservation), so S1 draws nothing and SETS ASIDE its whole need of the
    /// Empire purse; S2 waits, HeldForEarlier naming S1. NEW: built (0, 0), S3 keeps (40, 20), two queue rows, published holds
    /// (purse 1) timber 40 and stone 20. ATTRIBUTION: the old values reproduce under TestConfigs.PreReservations, the batch-3
    /// rule (<see cref="ALowerIdHeadThatFailsAGate_UnderPreReservations_DrawsNothing_AndBlocksNoHigherIdHead"/>).
    /// </summary>
    [Fact]
    public void ALowerIdHeadThatFailsAGate_DrawsNothing_ButSetsAsideItsNeed_AndTheHigherIdHeadWaits()
    {
        WorldState w = GateWorld();
        WorldState next = ConstructionOnly(Cfg, null).Step(w);
        Assert.Equal((0L, 0L), (Built(next, S1), Built(next, S2)));
        Assert.Equal((40L, 20L), (Of(next, S3, Timber), Of(next, S3, Stone)));
        Assert.Equal(2, next.ConstructionQueue.Count);
        int purse = Reservations.PolityPurse(Player);
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 40), new ConstructionHoldRow(purse, Stone, 20)], next.ConstructionHolds.Rows());
        HeadStatusReport waiting = ConstructionQuery.HeadStatus(w, Cfg, S2, null, 10.0)!;
        Assert.Equal((HeadLimitKind.Materials, MaterialCause.HeldForEarlier), (waiting.Resolution.Limit, waiting.Cause));
        Assert.Equal(new QueueEntryRef(S1, 0, Granary), waiting.Holder);
        AssertConserved(next);
    }

    /// <summary>D-055 (2026-10-16) — the PreReservations twin of
    /// <see cref="ALowerIdHeadThatFailsAGate_DrawsNothing_ButSetsAsideItsNeed_AndTheHigherIdHeadWaits"/>: the batch-3 pin,
    /// unchanged.</summary>
    [Fact]
    public void ALowerIdHeadThatFailsAGate_UnderPreReservations_DrawsNothing_AndBlocksNoHigherIdHead()
    {
        WorldState next = ConstructionOnly(TestConfigs.PreReservations(Cfg), null).Step(GateWorld());
        Assert.Equal((0L, 1L), (Built(next, S1), Built(next, S2)));
        Assert.Equal((0L, 0L), (Of(next, S3, Timber), Of(next, S3, Stone)));
        ConstructionQueueRow waiting = Assert.Single(next.ConstructionQueue.Rows());
        Assert.Equal(S1, waiting.Settlement);
        Assert.Equal(0, next.ConstructionHolds.Count);
        AssertConserved(next);
    }

    // ------------------------------------------------------------------ the attribution controls (de86ace byte for byte)
    // THE BATCH OFF REPRODUCES de86ace; THE LAYER IS PROVED TURN-EXACT. The D-053 batch has three layers (the purse §1, relief
    // §2, the granary effects §3), all ON in the shipped content. Switching THIS layer off alone no longer returns de86ace on
    // the combined tree — the other two layers stay on — so each control below runs in two arms: the switch-off arm is the
    // BATCH off (TestConfigs.PreD053) and keeps its de86ace LITERAL; the PER-LAYER proof is the next arm — the combined world
    // against the combined world with ONLY the purse off (Off), first differing exactly at the delivery of the first
    // construction order the purse changes.

    /// <summary>de86ace, MEASURED by `sim run --founded --seed 42 --turns 300 --ai-empires 1 --hash-log` built from that
    /// tree (the value AiFoundingAttributionTests pinned as S1cTurn300 there).</summary>
    private const string De86aceAiTurn300 = "aa7a87a98370c24815d478877a3f4f862838fde51a33fd5d0841157ca73066a7";

    /// <summary>The founded seed-42 300-turn golden at de86ace (SnapshotTests.PreD054FoundedGoldenHash, unmoved by the batch).</summary>
    private const string De86aceFounded = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.PreD054Worldgen())), orders);
    }

    [Fact]
    public void WithTheBatchOff_TheFoundedGoldenIsDe86aceByteForByte_AndThePurseMovesItNot()
    {
        // The order-less founded world builds nothing either way (no order, no AI): the batch-off run must be de86ace's
        // golden exactly, and so must the shipped (ON) run SnapshotTests pins and the purse-off one.
        // D-054 (batch 3) — the PreD054 PAIR: founded flat (TestConfigs.PreD054Worldgen) with the sim half off (PreD053
        // strips PreD054Sim; the purse-off leg strips it explicitly), and the de86ace literals — v32 measurements — compared
        // through SchemaProjection.V32.
        Assert.Equal(De86aceFounded, SchemaProjection.V32(Sim.Tests.Kernel.SnapshotTests.RunFoundedGolden(TestConfigs.PreD053(Cfg), TestConfigs.PreD054Worldgen())));
        Assert.Equal(De86aceFounded, SchemaProjection.V32(Sim.Tests.Kernel.SnapshotTests.RunFoundedGolden(TestConfigs.PreD054Sim(Off), TestConfigs.PreD054Worldgen())));
        Assert.Equal(De86aceFounded, Sim.Tests.Kernel.SnapshotTests.PreD054FoundedGoldenHash);
    }

    private static (WorldState World, OrderLog Log, List<string> Hashes) RunAi(SimConfig cfg)
    {
        WorldgenConfig wg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor exec = Production(cfg, log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        var hashes = new List<string>();
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.PreD054Worldgen());
            w = exec.Step(w);
            hashes.Add(WorldHash.ComputeHex(w));
        }
        return (w, log, hashes);
    }

    [Fact]
    public void WithTheBatchOff_TheAiWorldIsDe86aceByteForByte_AndThePurseAloneDivergesExactlyAtTheFirstDifferentConstructionOrdersDelivery()
    {
        // THE BATCH OFF: de86ace, byte for byte (the roads refactor rides in this run too — the AI develops roads in it).
        (WorldState batchOff, _, _) = RunAi(TestConfigs.PreD053(Cfg));
        Assert.Equal(De86aceAiTurn300, SchemaProjection.V32(batchOff));
        long roadUnits = 0;
        for (int i = 0; i < batchOff.RoadDevelopments.Count; i++) roadUnits += batchOff.RoadDevelopments[i].MaterialUnits;
        Assert.True(roadUnits > 0, "anti-vacuity: the AI paid for a road (through EmpireStockpile.Draw) in the batch-off run");

        // THE PER-LAYER PROOF on the combined tree: ON (the shipped batch) against ON with only the purse off. The logs
        // agree up to the first construction order the purse changes, and the worlds agree exactly until that order's
        // delivery (an order stamped T applies in the step T → T+1). MEASURED on this tree: the AI's first granary is
        // ordered at turn 4 from its Empire's purse (turn 9 from its own stock with the purse off), worlds first differ at 5.
        // ADR-055/056 (the R4 rule): the purse twin is an older layer's twin — it strips the newer military layer and switches the
        // economy packets off — so the ON arm is the military-off, pre-packet world too; the per-layer proof compares like with like.
        (_, OrderLog onLog, List<string> onHashes) = RunAi(TestConfigs.PreEconomyTrack(TestConfigs.PreMilitary(Cfg)));
        (_, OrderLog offLog, List<string> offHashes) = RunAi(Off);
        long firstDifferent = -1;
        for (int t = 0; t < 300 && firstDifferent < 0; t++) if (onHashes[t] != offHashes[t]) firstDifferent = t + 1;
        int k = 0;
        while (k < offLog.Count && k < onLog.Count && offLog[k] == onLog[k]) k++;
        Assert.True(k < onLog.Count, "the purse changed no AI order");
        OrderRecord first = onLog[k];
        Assert.Equal(OrderKind.EnqueueConstruction, first.Kind);
        Assert.Equal(first.Turn + 1, firstDifferent);
    }
}
