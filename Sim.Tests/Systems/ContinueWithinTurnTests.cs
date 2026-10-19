using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// D-058 — THE DIRECTOR'S RULING OF 2026-10-19, verbatim: "Build one at a time, so that if 6 can be built and an order of 9
/// is give, and in that turn, resources are available for 1 more and then some, it should show in the next turn as 7
/// build, 1 under construction, 1 not started". Construction stays SEQUENTIAL (one project of a settlement's queue at a
/// time, in queue order), but one step's building capacity is not capped at one project: when the head completes, what is
/// left of the settlement's capacity continues to its next entry IN THE SAME STEP, which is resolved at its own order's
/// place in the building order under the set-aside rules of any head (ConstructionQuery.HoldsWalk; sim.json
/// construction.continueWithinTurn, shipped on; control TestConfigs.PreContinueWithinTurn).
///
/// THE RIG is IncrementalConstructionTests' (the founded dev world, seed 42, four settlements, all the player's; timber and
/// stone at exactly 0 unless set; housing's draw 0; turn length 10 years; ConstructionSystem alone unless a test names road
/// development). The projects are test-only "works" (timber 40, stone 20) whose labour L is a stated multiple of the
/// building settlement's capacity C, so how many a step can build is set by the test: C / L = 64 / 9 = 7.11… (seven and a
/// ninth), 16 (labour never binds), 4 (exactly four), 1 (exactly one). Every figure is computed by hand beside it.
/// Each test carries a PREMISE: the same step under PreContinueWithinTurn, the batch-4 rule (one head a settlement a step).
/// </summary>
public class ContinueWithinTurnTests
{
    private static readonly SimConfig Off = TestConfigs.PreContinueWithinTurn(Cfg);
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly int PlayerPurse = Reservations.PolityPurse(Player);
    private const int University = 11;        // goods.json: founds an institution

    // ================================================================== helpers

    /// <summary>Appends an entry at <paramref name="s"/>'s next slot with its progress row (EnqueuedTurn, p).</summary>
    private static void Entry(WorldState w, SettlementId s, long enqueued, int project = Works, long progress = 0)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, enqueued, progress));
    }

    private static OrderRecord[] Nine(SettlementId s)
    {
        var orders = new OrderRecord[9];
        for (int i = 0; i < orders.Length; i++) orders[i] = Enqueue(0, Player, s, Works);
        return orders;
    }

    /// <summary>(slot, progress) of every entry of <paramref name="s"/>'s queue, ascending slot.</summary>
    private static (int Slot, long P)[] Queue(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = ConstructionQuery.Queue(w, s);
        var result = new (int, long)[q.Length];
        for (int i = 0; i < q.Length; i++) result[i] = (q[i].Slot, ConstructionQuery.ProgressOf(w, q[i]));
        return result;
    }

    private static ConstructionHoldRow[] Holds(IReadOnlyWorldState w) => w.ConstructionHolds.Rows().ToArray();

    private static int LabourRows(IReadOnlyWorldState w, SettlementId s)
    {
        int n = 0;
        for (int i = 0; i < w.ConstructionLabor.Count; i++) if (w.ConstructionLabor[i].Settlement == s) n++;
        return n;
    }

    /// <summary>A world with S0 on all builders, <paramref name="timber"/> and <paramref name="stone"/> in the purse (at S2),
    /// and the works costing labour C × <paramref name="lOverC"/>.</summary>
    private static (WorldState W, SimConfig Cfg, double C, double L) Rig(double lOverC, long timber, long stone, SimConfig? baseCfg = null)
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        double c = C(w, S0, 10.0);
        Assert.True(c > 0.0, "anti-vacuity: the rig has builders");
        double l = c * lOverC;
        Set(w, S2, Timber, timber);
        Set(w, S2, Stone, stone);
        return (w, WithWorks(baseCfg ?? Cfg, l), c, l);
    }

    // ================================================================== the Director's example

    /// <summary>
    /// THE DIRECTOR'S EXAMPLE, turn-exact. Nine works ordered at S0 in one batch stamped turn 0 (delivered in step 0 → 1,
    /// resolved in that same step), capacity for 7 and a ninth of an 8th (C / L = 64 / 9), materials for all nine (400 timber,
    /// 200 stone). At turn 1: 7 built; slot 7 under construction at exactly floor((64/9 − 7) × N) = floor(111,111.1) = 111,111
    /// (ceil(40 × 0.111111) = 5 timber and ceil(2.22) = 3 stone in); slot 8 not started (p = 0). Drawn 7 × 40 + 5 = 285 timber
    /// and 7 × 20 + 3 = 143 stone, every unit through the Ledger (Sunk); the part-built head then sets aside its remaining
    /// 35 timber and 17 stone. At turn 2 the rest (slot 7's remaining 888,889 units, then slot 8 whole: 0.89 + 1 &lt; 7.11 of
    /// capacity) completes: 9 built, the queue empty, exactly 9 × 40 and 9 × 20 drawn in all.
    /// PREMISE (PreContinueWithinTurn, batch 4): one a step — turn 1 shows 1 built, 8 not started, 40 / 20 drawn.
    /// </summary>
    [Fact]
    public void TheDirectorsExample_NineOrdered_CapacityForSevenAndSome_SevenBuilt_OneUnderConstruction_OneNotStarted()
    {
        (WorldState w, SimConfig cfg, double c, double l) = Rig(9.0 / 64.0, 400, 200);
        long sunkT = Sunk(w, Timber), sunkS = Sunk(w, Stone);

        WorldState pre = ConstructionOnly(TestConfigs.PreContinueWithinTurn(cfg), Log(Nine(S0))).Step(w);
        Assert.Equal(1L, Built(pre, S0, Works));
        Assert.Equal([(1, 0L), (2, 0L), (3, 0L), (4, 0L), (5, 0L), (6, 0L), (7, 0L), (8, 0L)], Queue(pre, S0));
        Assert.Equal((360L, 180L), (TotalOf(pre, Timber), TotalOf(pre, Stone)));

        TurnExecutor exec = ConstructionOnly(cfg, Log(Nine(S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, t1.Clock.Turn);
        Assert.Equal(7L, Built(t1, S0, Works));
        Assert.Equal([(7, 111_111L), (8, 0L)], Queue(t1, S0));
        Assert.Equal((285L, 143L), (Sunk(t1, Timber) - sunkT, Sunk(t1, Stone) - sunkS));
        Assert.Equal((115L, 57L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Equal([new ConstructionHoldRow(PlayerPurse, Timber, 35), new ConstructionHoldRow(PlayerPurse, Stone, 17)], Holds(t1));
        // ONE published labour row: the seven whole projects and the ninth of the eighth, summed in the walk's order.
        double used = 0.0;
        for (int k = 0; k < 7; k++) used += ConstructionQuery.LabourBetween(l, 0, N);
        used += ConstructionQuery.LabourBetween(l, 0, 111_111);
        Assert.Equal(1, LabourRows(t1, S0));
        Assert.Equal(used, Labour(t1, S0));
        Assert.True(used <= c, "the published labour never exceeds the settlement's capacity");
        AssertConserved(t1);

        WorldState t2 = exec.Step(t1);
        Assert.Equal(9L, Built(t2, S0, Works));
        Assert.Empty(Queue(t2, S0));
        Assert.Empty(Holds(t2));
        Assert.Equal((360L, 180L), (Sunk(t2, Timber) - sunkT, Sunk(t2, Stone) - sunkS));
        Assert.Equal((40L, 20L), (TotalOf(t2, Timber), TotalOf(t2, Stone)));
        AssertConserved(t2);
    }

    /// <summary>
    /// CAPACITY EXACTLY FINISHING k. C / L = 4 (a dyadic L, so 4 × L = C exactly): four complete and nothing is left to move
    /// the fifth by one unit, so it is NOT started and holds nothing (it is the head from the next step, as before). And
    /// C / L = 1: the step is byte-identical to the batch-4 rule — continuation changes nothing when capacity finishes
    /// exactly one.
    /// </summary>
    [Theory]
    [InlineData(0.25, 4)]
    [InlineData(1.0, 1)]
    public void CapacityExactlyFinishingK_BuildsK_TheNextIsNotStarted_AndHoldsNothing(double lOverC, int k)
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(lOverC, 400, 200);
        WorldState t1 = ConstructionOnly(cfg, Log(Nine(S0))).Step(w);
        Assert.Equal((long)k, Built(t1, S0, Works));
        (int Slot, long P)[] q = Queue(t1, S0);
        Assert.Equal(9 - k, q.Length);
        Assert.Equal((k, 0L), q[0]);
        for (int i = 0; i < q.Length; i++) Assert.Equal(0L, q[i].P);
        Assert.Empty(Holds(t1));
        Assert.Equal((400L - 40 * k, 200L - 20 * k), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        AssertConserved(t1);
        WorldState pre = ConstructionOnly(TestConfigs.PreContinueWithinTurn(cfg), Log(Nine(S0))).Step(w);
        if (k == 1) Assert.Equal(WorldHash.ComputeHex(pre), WorldHash.ComputeHex(t1));
        else Assert.Equal(1L, Built(pre, S0, Works));
    }

    /// <summary>
    /// MATERIALS, NOT LABOUR, BIND MID-QUEUE. C / L = 16 (labour for sixteen); 300 timber and 200 stone: seven complete
    /// (280 / 140), the eighth gets the last 20 timber — p = floor(20N/40) = 500,000, drawing 20 timber and 10 stone — and
    /// stops short; the ninth is not started (sequential: a head that does not complete passes nothing on), so it neither
    /// draws nor holds. The eighth, a head now, sets aside its remaining need within what is left: timber min(20, 0) = 0,
    /// stone min(10, 200 − 150) = 10.
    /// </summary>
    [Fact]
    public void MaterialsBindMidQueue_SevenBuilt_TheEighthStopsAtTheMaterialEdge_TheNinthNotStarted()
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(1.0 / 16.0, 300, 200);
        long sunkT = Sunk(w, Timber), sunkS = Sunk(w, Stone);
        WorldState t1 = ConstructionOnly(cfg, Log(Nine(S0))).Step(w);
        Assert.Equal(7L, Built(t1, S0, Works));
        Assert.Equal([(7, 500_000L), (8, 0L)], Queue(t1, S0));
        Assert.Equal((300L, 150L), (Sunk(t1, Timber) - sunkT, Sunk(t1, Stone) - sunkS));
        Assert.Equal([new ConstructionHoldRow(PlayerPurse, Stone, 10)], Holds(t1));
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(t1, cfg, t1.ConstructionQueue, t1.ConstructionProgress, t1.GoodStocks, 10.0, []);
        Assert.Equal(new ConstructionQueueRow(S0, 7, Works), Assert.Single(walk.Heads).Entry);   // next step: the eighth is the head
        AssertConserved(t1);

        WorldState pre2 = ConstructionOnly(TestConfigs.PreContinueWithinTurn(cfg), Log(Nine(S0))).Step(w);
        Assert.Equal((1L, 260L), (Built(pre2, S0, Works), TotalOf(pre2, Timber)));
    }

    /// <summary>
    /// A SET-ASIDE HELD BY AN OLDER PROJECT ELSEWHERE BINDS THE CONTINUATION. S1's head (ordered turn 0, p = 437,500, no
    /// builders) sets aside its remaining 22 timber and 11 stone first. S0's three works (ordered turn 2, labour for
    /// sixteen) share the rest of the purse, 82 − 22 = 60 timber and 41 − 11 = 30 stone: slot 0 completes (40 / 20), slot 1
    /// continues on the last 20 / 10 to p = 500,000 and slot 2 is not started. PREMISE: with the hold rule off and the
    /// continuation on, the same continuation takes the 22 / 11 too — slot 1 completes and slot 2 reaches
    /// min(floor(2N/40), floor(1N/20)) = 50,000 — so the hold is what stopped it: the continuing entry spends only what a
    /// head in its place may spend.
    /// </summary>
    [Fact]
    public void ASetAsideHeldByAnOlderProjectElsewhere_BindsTheContinuation()
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(1.0 / 16.0, 82, 41);
        Entry(w, S1, 0, Granary, 437_500);
        NoBuilders(w, S1);
        for (int i = 0; i < 3; i++) Entry(w, S0, 2);

        WorldState t1 = ConstructionOnly(cfg, null).Step(w);
        Assert.Equal((1L, 437_500L), (Built(t1, S0, Works), P(t1, S1)));
        Assert.Equal([(1, 500_000L), (2, 0L)], Queue(t1, S0));
        Assert.Equal([new ConstructionHoldRow(PlayerPurse, Timber, 22), new ConstructionHoldRow(PlayerPurse, Stone, 11)], Holds(t1));
        Assert.Equal((22L, 11L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        AssertConserved(t1);

        SimConfig noHolds = cfg with { Construction = cfg.Construction! with { Reservations = false } };
        WorldState premise = ConstructionOnly(noHolds, null).Step(w);
        Assert.Equal(2L, Built(premise, S0, Works));
        Assert.Equal([(2, 50_000L)], Queue(premise, S0));
    }

    /// <summary>
    /// THE CONTINUING ENTRY TAKES ITS OWN ORDER'S PLACE (the D-055 priority contract, applied within the step). A part-built
    /// head A at S1 (ordered turn 3, p = 437,500, no builders) needs 22 timber and 11 stone. S0's slot 0 (ordered turn 0, p =
    /// 750,000) completes on its last 10 / 5 with capacity to spare, and its slot 1 continues in the same step:
    /// <list type="bullet">
    /// <item>slot 1 ordered at turn 1, EARLIER than A: it is walked before A and takes the 22 / 11 — p = floor(22N/40) =
    ///   550,000 — and A finds nothing to set aside;</item>
    /// <item>slot 1 ordered at turn 5, LATER than A: A sets aside its 22 / 11 first, and slot 1 — a head now, with builders —
    ///   waits at p = 0 and holds nothing (nothing is left).</item>
    /// </list>
    /// PREMISE (PreContinueWithinTurn): slot 1 is not walked in that step either way, and A holds the 22 / 11.
    /// </summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(5L)]
    public void TheContinuingEntry_TakesItsOwnOrdersPlace_InTheSameStep(long slot1Turn)
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(1.0 / 16.0, 32, 16);
        Entry(w, S1, 3, Granary, 437_500);
        NoBuilders(w, S1);
        Entry(w, S0, 0, Granary, 750_000);
        Entry(w, S0, slot1Turn, Granary);

        WorldState pre = ConstructionOnly(TestConfigs.PreContinueWithinTurn(cfg), null).Step(w);
        Assert.Equal([(1, 0L)], Queue(pre, S0));
        Assert.Equal([new ConstructionHoldRow(PlayerPurse, Timber, 22), new ConstructionHoldRow(PlayerPurse, Stone, 11)], Holds(pre));

        WorldState t1 = ConstructionOnly(cfg, null).Step(w);
        Assert.Equal(1L, Built(t1, S0));
        if (slot1Turn < 3)
        {
            Assert.Equal([(1, 550_000L)], Queue(t1, S0));
            Assert.Empty(Holds(t1));
            Assert.Equal((0L, 0L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        }
        else
        {
            Assert.Equal([(1, 0L)], Queue(t1, S0));
            Assert.Equal([new ConstructionHoldRow(PlayerPurse, Timber, 22), new ConstructionHoldRow(PlayerPurse, Stone, 11)], Holds(t1));
            Assert.Equal((22L, 11L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        }
        Assert.Equal(437_500L, P(t1, S1));
        AssertConserved(t1);
    }

    /// <summary>
    /// TIE-DENSE: the continuing entries are placed by the building order's composite INTEGER key (EnqueuedTurn,
    /// SettlementId, Slot) — no double is ordered. Four settlements, three works each, every order stamped turn 0 (twelve
    /// entries tied on the turn), labour for sixteen everywhere, and 200 timber and 100 stone: materials for exactly five.
    /// By the key, S0's continuing slots 1 and 2 outrank S1's head (0, S0, 1) &lt; (0, S1, 0): S0 builds three, S1 two,
    /// S1's slot 2 continues and finds nothing, and S2 and S3 wait at p = 0. The oracle below sorts the twelve keys
    /// independently and serves them in turn. PREMISE (PreContinueWithinTurn, one a step): the four heads build one each
    /// and the fifth project's materials stay in the purse.
    /// </summary>
    [Fact]
    public void TieDense_TwelveEntriesTiedOnTheTurn_ServedInSettlementThenSlotOrder()
    {
        WorldState w = Solo();
        for (int s = 0; s < 4; s++) AllBuilders(w, new SettlementId(s));
        double minC = double.MaxValue;
        for (int s = 0; s < 4; s++) minC = Math.Min(minC, C(w, new SettlementId(s), 10.0));
        SimConfig cfg = WithWorks(Cfg, minC / 16.0);
        Set(w, S2, Timber, 200);
        Set(w, S2, Stone, 100);
        // Interleaved insertion order (settlement 3 first), so table order is not the key order.
        for (int round = 0; round < 3; round++)
            for (int s = 3; s >= 0; s--) Entry(w, new SettlementId(s), 0);

        // The oracle: the keys sorted, five served whole, a settlement's entry reached only if its earlier ones completed.
        var keys = new List<(long Turn, int Settlement, int Slot)>();
        for (int s = 0; s < 4; s++) for (int slot = 0; slot < 3; slot++) keys.Add((0, s, slot));
        keys.Sort();
        var built = new int[4];
        long timber = 200;
        foreach ((long _, int s, int slot) in keys)
        {
            if (built[s] != slot || timber < 40) continue;
            built[s]++;
            timber -= 40;
        }
        Assert.Equal([3, 2, 0, 0], built);

        WorldState t1 = ConstructionOnly(cfg, null).Step(w);
        for (int s = 0; s < 4; s++) Assert.Equal((long)built[s], Built(t1, new SettlementId(s), Works));
        Assert.Equal((0L, 0L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        AssertConserved(t1);

        WorldState pre = ConstructionOnly(TestConfigs.PreContinueWithinTurn(cfg), null).Step(w);
        for (int s = 0; s < 4; s++) Assert.Equal(1L, Built(pre, new SettlementId(s), Works));
    }

    /// <summary>
    /// A PROJECT THAT FOUNDS AN INSTITUTION IS NEVER STARTED AS A CONTINUATION: its host's viability is read on PREV, which
    /// cannot count a founding completed in the same step. S0: a works (completes, capacity to spare), then a university,
    /// then a works. The university is not walked this step (p = 0, nothing set aside for it) and the works behind it waits
    /// its turn — sequential. It is the head from the next step, exactly as before.
    /// </summary>
    [Fact]
    public void AFoundingProject_IsNotStartedAsAContinuation_AndNothingBehindItIs()
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(1.0 / 16.0, 5000, 5000);
        Entry(w, S0, 0);
        Entry(w, S0, 0, University);
        Entry(w, S0, 0);
        WorldState t1 = ConstructionOnly(cfg, null).Step(w);
        Assert.Equal(1L, Built(t1, S0, Works));
        Assert.Equal([(1, 0L), (2, 0L)], Queue(t1, S0));
        Assert.Empty(Holds(t1));
        Assert.Equal((4960L, 4980L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
    }

    /// <summary>
    /// REPLAY AND RESUME. The Director's example over three steps: the uninterrupted run, the same orders replayed, and a run
    /// saved at turn 1 — with slot 7 part-built and its set-aside published — loaded through Snapshot and continued, give the
    /// same world hash at every turn.
    /// </summary>
    [Fact]
    public void ReplayAndResumeThroughASave_MidChain_EqualTheUninterruptedRun()
    {
        (WorldState w, SimConfig cfg, _, _) = Rig(9.0 / 64.0, 400, 200);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Nine(S0)));
        var a = new List<string>();
        WorldState t = w;
        for (int k = 1; k <= 3; k++) { t = exec.Step(t); a.Add(WorldHash.ComputeHex(t)); }

        WorldState r = w;
        TurnExecutor again = ConstructionOnly(cfg, Log(Nine(S0)));
        for (int k = 1; k <= 3; k++) { r = again.Step(r); Assert.Equal(a[k - 1], WorldHash.ComputeHex(r)); }

        WorldState t1 = exec.Step(w);
        Assert.Equal([(7, 111_111L), (8, 0L)], Queue(t1, S0));
        Assert.NotEmpty(Holds(t1));
        WorldState loaded = ConstructionLifecyclePersistenceTests.ViaSnapshot(ConstructionLifecyclePersistenceTests.SaveBytes(t1), t1.Terrain);
        Assert.Equal(a[0], WorldHash.ComputeHex(loaded));
        WorldState u = loaded;
        for (int k = 2; k <= 3; k++) { u = exec.Step(u); Assert.Equal(a[k - 1], WorldHash.ComputeHex(u)); }
    }

    /// <summary>
    /// ROAD WORKS UNDER A SPLIT BELOW 100, IN THE SAME STEP (ResourceAllocationTests' rule; pipeline: construction before
    /// road development). Route S0–S1 whose dirt-path row is 60 km (the trackway known: the road order costs 60 timber),
    /// 100 timber at S2, 1,000 stone at S3, three works at S0 (labour for sixteen), b = 50 and a road order stamped turn 0.
    /// TIMBER: S = 100, E_b = 50, E_r = 50; D_b = the claim's walk on S = 40 + 40 + 20 (slot 0 and slot 1 complete, slot 2
    /// continues on 20) = 100; D_r = 60; G_b = min(100, 50 + max(0, 50 − 60)) = 50. The step's walk on 50: slot 0 completes
    /// (40), slot 1 continues on 10 — p = 250,000 — and stops (slot 2 is not reached). Road works find 100 − 50 = 50 and
    /// none of it set aside: charged floor(60 × 50 / 60) = 50 ≥ min(D_r, E_r) = 50 — the guarantee holds.
    /// STONE: S = 1,000, D_b = 20 + 20 + 10 drawn + 10 set aside = 60, G_b = min(60, 1000) = 60; the walk spends 20 + 5
    /// drawn + 15 set aside = 40 = min(G_b, its heads' need 20 + 20) — below the grant because the timber stopped slot 1
    /// short, the case SplitReportCheck's P2 now states as min(G_b, R).
    /// PREMISE (PreContinueWithinTurn): D_b timber = 40, so G_b = 40: slot 0 completes and the road takes its whole 60.
    /// </summary>
    [Fact]
    public void RoadWorksUnderASplitBelow100_InTheSameStep_KeepTheirGuarantee()
    {
        (WorldState w, SimConfig cfg) = RoadRig();
        OrderRecord[] orders =
        [
            new(0, Player.Value, OrderKind.SetResourceAllocation, Player.Value, 50),
            OrderRecord.From(0, Player, OrderKind.DevelopRoads, 0, 100.0),
        ];
        long sunk0 = Sunk(w, Timber), stone0 = TotalOf(w, Stone);
        WorldState next = BuildAndRoads(cfg, Log(orders)).Step(w);
        Assert.Equal(1L, Built(next, S0, Works));
        Assert.Equal([(1, 250_000L), (2, 0L)], Queue(next, S0));
        Assert.Equal(50L, RoadUnits(next));
        Assert.Equal(0L, TotalOf(next, Timber));
        Assert.Equal(stone0 - 25, TotalOf(next, Stone));
        Assert.Equal([new ConstructionHoldRow(PlayerPurse, Stone, 15)], Holds(next));
        Assert.Equal(100L, Sunk(next, Timber) - sunk0);
        AssertConserved(next);

        WorldState pre = BuildAndRoads(TestConfigs.PreContinueWithinTurn(cfg), Log(orders)).Step(w);
        Assert.Equal((1L, 60L), (Built(pre, S0, Works), RoadUnits(pre)));
        Assert.Equal([(1, 0L), (2, 0L)], Queue(pre, S0));
    }

    private static (WorldState W, SimConfig Cfg) RoadRig()
    {
        WorldState w = Solo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, 60, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Set(w, S2, Timber, 100);
        Set(w, S3, Stone, 1000);
        SimConfig cfg = WithWorks(Cfg, C(w, S0, 10.0) / 16.0);
        for (int i = 0; i < 3; i++) Entry(w, S0, 0);
        return (w, cfg);
    }

    private static TurnExecutor BuildAndRoads(SimConfig cfg, OrderLog? log) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], log);

    private static long RoadUnits(IReadOnlyWorldState w)
    {
        long u = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) u += w.RoadDevelopments[i].MaterialUnits;
        return u;
    }

    // ================================================================== the switch

    [Fact]
    public void TheShippedSimJson_SwitchesTheContinuationOn_AndTheLoaderRefusesItWithoutIncremental()
    {
        Assert.True(Cfg.Construction!.ContinueWithinTurn);
        Assert.True(ConstructionQuery.ContinuesWithinTurn(Cfg));
        Assert.False(ConstructionQuery.ContinuesWithinTurn(Off));
        Assert.False(ConstructionQuery.ContinuesWithinTurn(TestConfigs.PreReservations(Cfg)));          // R4: the older twin strips it
        Assert.False(ConstructionQuery.ContinuesWithinTurn(TestConfigs.PreIncrementalConstruction(Cfg)));
        Assert.True(ConstructionQuery.ContinuesWithinTurn(TestConfigs.PreAllocationRatios(Cfg)));        // single-layer helpers keep it

        string json;
        using (var reader = new StreamReader(Sim.Data.DataFiles.OpenSim())) json = reader.ReadToEnd();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        Assert.True((bool)doc["construction"]!["continueWithinTurn"]!);
        doc["construction"]!["incremental"] = false;
        doc["construction"]!["allocationRatios"] = false;
        doc["construction"]!["reservations"] = false;
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("construction.continueWithinTurn requires construction.incremental to be true (D-058)", e.Message);
        doc["construction"]!["continueWithinTurn"] = false;
        Assert.False(SimConfigLoader.Load(doc.ToJsonString()).Construction!.ContinueWithinTurn);
        doc["construction"]!.AsObject().Remove("continueWithinTurn");
        Assert.False(SimConfigLoader.Load(doc.ToJsonString()).Construction!.ContinueWithinTurn);
    }
}
