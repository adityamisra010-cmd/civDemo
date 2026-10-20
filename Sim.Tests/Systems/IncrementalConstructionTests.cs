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
/// WS1 (D-054 — the Director's 2026-10-14 instruction, workstream 1; supersedes M4-D's "built whole or waits") —
/// INCREMENTAL CONSTRUCTION. A queue head's progress p (of N = 1,000,000) advances each step by its builders' pace,
/// floor(C / L × N), as far as the NEXT units of every material allow (materials first: after p, exactly
/// ceil(cost × p / N) of each input has gone in); heads are served OLDEST ORDER FIRST against what earlier heads left;
/// what has gone in is never lost while a head waits, only when it is cancelled (kind 11, no refund).
///
/// The rigs run ONLY ConstructionSystem (and the systems a test names) on the founded dev world (seed 42, four
/// settlements, all the player's unless stated), every timber and stone row at exactly 0 unless set, housing's published
/// draw at 0 and the turn length fixed (ResearchRigs.FlatEra), so every progress unit and every draw is computable by
/// hand. Where a test needs a labour PACE it uses a test-only project whose laborRequired is a chosen multiple of the
/// settlement's capacity C, so C / L is an exact dyadic or a stated fraction.
/// </summary>
public class IncrementalConstructionTests
{
    internal static readonly SimConfig Cfg = TestConfigs.Sim();                       // shipped: incremental ON
    private static readonly ResearchContent Research = TestConfigs.Research();
    internal const long N = ConstructionQuery.ProgressUnits;
    internal const int Granary = 1;
    internal const int Works = 90;            // test-only project: timber 40, stone 20, L chosen per test
    internal static readonly int Stone = Cfg.Goods!.IdOf("stone");
    internal static readonly int Timber = Cfg.Goods!.IdOf("timber");
    internal static readonly PolityId Player = new(1);
    internal static readonly PolityId Rival = new(2);
    internal static readonly SettlementId S0 = new(0), S1 = new(1), S2 = new(2), S3 = new(3);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    // ================================================================== rig

    /// <summary>The founded dev world (four settlements, all the player's), stone and timber at exactly 0, housing's
    /// published draw 0 (so C is share × labour adults × dt exactly).</summary>
    internal static WorldState Solo() => Bare(DevSolo.Value.Clone());

    /// <summary>The founded dev world with one AI Empire: S0 and S2 the player's, S1 and S3 the AI's.</summary>
    internal static WorldState Duo() => Bare(DevDuo.Value.Clone());

    private static WorldState Bare(WorldState w)
    {
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            Set(w, w.Settlements[s].Id, Stone, 0);
            Set(w, w.Settlements[s].Id, Timber, 0);
        }
        for (int i = 0; i < w.Housing.Count; i++) w.Housing.Ref(i).LastLaborUsed = 0.0;
        return w;
    }

    internal static void Set(WorldState w, SettlementId s, int good, long qty)
    {
        var id = new GoodId(good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have == qty) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.InitialEndowment,
            Math.Abs(qty - have), qty > have ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    internal static long Of(IReadOnlyWorldState w, SettlementId s, int good)
    {
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(good));
        return idx < 0 ? 0 : w.GoodStocks[idx].Amount.Value;
    }

    internal static long TotalOf(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good.Value == good) sum += w.GoodStocks[i].Amount.Value;
        return sum;
    }

    /// <summary>Total sunk under ConstructionMaterials for one good (the Ledger's own counterweight).</summary>
    internal static long Sunk(IReadOnlyWorldState w, int good)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.OfGood(new GoodId(good))
                && w.LedgerFlows[i].Reason == ReasonIds.ConstructionMaterials) return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    internal static void Share(WorldState w, SettlementId s, double construction)
    {
        var row = new SectorAllocationRow(s, Farming: 1.0 - construction, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: construction);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    internal static void AllBuilders(WorldState w, SettlementId s) => Share(w, s, 1.0);
    internal static void NoBuilders(WorldState w, SettlementId s) => Share(w, s, 0.0);

    /// <summary><paramref name="cfg"/> with the test-only project <paramref name="id"/> (timber 40, stone 20, labour
    /// <paramref name="labor"/>, no research link) added.</summary>
    internal static SimConfig WithWorks(SimConfig cfg, double labor, int id = Works, long timber = 40, long stone = 20)
    {
        GoodsConfig goods = cfg.Goods!;
        var extra = new ConstructionProjectEntry(id, "works" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [new ProjectInput("timber", timber), new ProjectInput("stone", stone)], labor);
        return cfg with { Goods = goods with { Projects = [.. goods.Projects!, extra] } };
    }

    internal static OrderRecord Enqueue(long turn, PolityId actor, SettlementId s, int project = Granary) =>
        OrderRecord.From(turn, actor, OrderKind.EnqueueConstruction, s.Value, project);

    internal static OrderRecord Cancel(long turn, PolityId actor, SettlementId s, double slot) =>
        new(turn, actor.Value, OrderKind.CancelConstruction, s.Value, slot);

    internal static OrderLog Log(params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        return log;
    }

    internal static TurnExecutor ConstructionOnly(SimConfig cfg, OrderLog? orders, double dtYears = 10.0) =>
        new(ResearchRigs.FlatEra(dtYears), [SystemCatalog.Construction(cfg)], orders);

    internal static ConstructionProgressRow? ProgressRow(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = ConstructionQuery.Queue(w, s);
        if (q.Length == 0) return null;
        int i = ConstructionQuery.ProgressIndex(w.ConstructionProgress, q[0]);
        return i < 0 ? null : w.ConstructionProgress[i];
    }

    internal static long P(IReadOnlyWorldState w, SettlementId s) => ProgressRow(w, s)?.Progress ?? 0;

    internal static long Built(IReadOnlyWorldState w, SettlementId s, int project = Granary) => ConstructionQuery.Built(w, s, project);

    internal static double Labour(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConstructionLabor.Count; i++) if (w.ConstructionLabor[i].Settlement == s) return w.ConstructionLabor[i].LastLaborUsed;
        return 0.0;
    }

    internal static void AssertConserved(IReadOnlyWorldState w)
    {
        Assert.True(ConservationAuditor.IsConserved(w, Cfg.Goods, out string report), report);
        for (int i = 0; i < w.GoodStocks.Count; i++) Assert.True(w.GoodStocks[i].Amount.Value >= 0, "a negative stock");
    }

    /// <summary>The capacity C (adult-years) of <paramref name="s"/> this rig gives at <paramref name="dt"/>.</summary>
    internal static double C(IReadOnlyWorldState w, SettlementId s, double dt) => ConstructionQuery.CapacityAdultYears(w, Cfg, s, dt);

    // ================================================================== the lockstep arithmetic

    [Fact]
    public void Delivered_IsCeilOfCostTimesProgress_MaterialsFirst_AndExactlyTheCostAtCompletion()
    {
        Assert.Equal(0, ConstructionQuery.Delivered(40, 0));
        Assert.Equal(1, ConstructionQuery.Delivered(40, 1));                  // the FIRST unit goes in before any work
        Assert.Equal(1, ConstructionQuery.Delivered(40, N / 40));             // still one unit up to its bucket edge
        Assert.Equal(2, ConstructionQuery.Delivered(40, N / 40 + 1));
        Assert.Equal(40, ConstructionQuery.Delivered(40, N));
        Assert.Equal(20, ConstructionQuery.Delivered(20, N));
        Assert.Equal(1800, ConstructionQuery.Delivered(1800, N));
        Assert.Equal(ConstructionQuery.MaxInputQty, ConstructionQuery.Delivered(ConstructionQuery.MaxInputQty, N));   // no overflow at the bound
        long prev = 0;
        for (long p = 0; p <= N; p += 997)
        {
            long d = ConstructionQuery.Delivered(7, p);
            Assert.True(d >= prev && d <= 7, "monotone and bounded");
            prev = d;
        }
        // The labour bound: floor, clamped before the cast; NaN and non-positive capacity give 0.
        Assert.Equal(250_000, ConstructionQuery.LabourBound(1.0, 4.0, 0));
        Assert.Equal(333_333, ConstructionQuery.LabourBound(1.0, 3.0, 0));
        Assert.Equal(N - 999_999, ConstructionQuery.LabourBound(1e9, 1.0, 999_999));
        Assert.Equal(0, ConstructionQuery.LabourBound(double.NaN, 2.0, 0));
        Assert.Equal(0, ConstructionQuery.LabourBound(0.0, 2.0, 0));
        Assert.Equal(0, ConstructionQuery.LabourBound(-1.0, 2.0, 0));
    }

    /// <summary>The lockstep delivery is exact long arithmetic (qty × p), so goods.json may not name an input past
    /// ConstructionQuery.MaxInputQty = (long.MaxValue − N) / N: the loader refuses it with the bound, and accepts the bound.</summary>
    [Fact]
    public void TheGoodsLoader_RefusesAProjectInputWhoseDeliveryWouldOverflow_AndAcceptsTheBound()
    {
        static string Json(long qty) =>
            "{\"goods\":[{\"id\":1,\"name\":\"grain\",\"category\":\"food\",\"bulkPerUnit\":1.0,\"numeraire\":true},"
            + "{\"id\":2,\"name\":\"stone\",\"category\":\"raw\",\"bulkPerUnit\":1.0}],\"recipes\":[],"
            + "\"projects\":[{\"id\":1,\"name\":\"cairn\",\"inputs\":[{\"good\":\"stone\",\"qty\":"
            + qty.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}],\"laborRequired\":1.0}]}";
        Assert.Equal((long.MaxValue - N) / N, ConstructionQuery.MaxInputQty);
        GoodsConfig ok = GoodsConfigLoader.Load(Json(ConstructionQuery.MaxInputQty));
        Assert.Equal(ConstructionQuery.MaxInputQty, ok.ProjectById(1)!.Inputs[0].Qty);
        var e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(Json(ConstructionQuery.MaxInputQty + 1)));
        Assert.Contains("qty must be at most " + ConstructionQuery.MaxInputQty.ToString(System.Globalization.CultureInfo.InvariantCulture), e.Message, StringComparison.Ordinal);
    }

    // ================================================================== progress over turns

    [Fact]
    public void PartialConstruction_ProgressesOverFourTurns_ExactProgressAndDrawsEachStep_CompletionAtTheStatedTurn()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        double c = C(w, S0, 10.0);
        Assert.True(c > 0.0, "anti-vacuity: the rig has builders");
        SimConfig cfg = WithWorks(Cfg, 4.0 * c);                              // C / L = 1/4 exactly: 250,000 units a step
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 100);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0, Works)));

        WorldState t = w;
        long[] timber = [90, 80, 70, 60], stone = [95, 90, 85, 80];
        for (int k = 1; k <= 3; k++)
        {
            t = exec.Step(t);
            Assert.Equal(new ConstructionProgressRow(S0, 0, Works, 0, 250_000L * k), ProgressRow(t, S0));
            Assert.Equal((timber[k - 1], stone[k - 1]), (Of(t, S0, Timber), Of(t, S0, Stone)));   // 10 timber, 5 stone a step
            Assert.Equal(0, Built(t, S0, Works));
            Assert.Equal(4.0 * c * 250_000.0 / N, Labour(t, S0));                // L × (p' − p) / N
        }
        // The completion turn by the stated rule: the first k with k × 250,000 ≥ N — turn 4.
        t = exec.Step(t);
        Assert.Equal(4, t.Clock.Turn);
        Assert.Equal(1, Built(t, S0, Works));
        Assert.Equal((0, 0), (t.ConstructionQueue.Count, t.ConstructionProgress.Count));
        Assert.Equal((60L, 80L), (Of(t, S0, Timber), Of(t, S0, Stone)));     // exactly the cost in total: 40 and 20
        Assert.Equal((40L, 20L), (Sunk(t, Timber) - Sunk(w, Timber), Sunk(t, Stone) - Sunk(w, Stone)));
        AssertConserved(t);
    }

    [Fact]
    public void TheDtRule_TwoTurnLengths_CompleteAtTheSameElapsedYears_WhenCOverLDividesN()
    {
        // Frozen population (construction only), a dyadic share (1.0) and housing's draw 0: C(dt=5) = C(dt=10) / 2
        // exactly, so with L = 4 × C(10) a step adds 250,000 at dt 10 and 125,000 at dt 5.
        WorldState w = Solo();
        AllBuilders(w, S0);
        double c10 = C(w, S0, 10.0), c5 = C(w, S0, 5.0);
        Assert.Equal(c10, 2.0 * c5);
        SimConfig cfg = WithWorks(Cfg, 4.0 * c10);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 100);

        static long CompletionTurn(WorldState w0, SimConfig cfg, double dt)
        {
            TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0, Works)), dt);
            WorldState t = w0;
            for (int k = 1; k <= 20; k++)
            {
                t = exec.Step(t);
                if (Built(t, S0, Works) == 1) return k;
            }
            return -1;
        }
        Assert.Equal(4, CompletionTurn(w, cfg, 10.0));
        Assert.Equal(8, CompletionTurn(w, cfg, 5.0));
        Assert.Equal(4 * 10.0, 8 * 5.0);                                      // the same elapsed years
    }

    [Fact]
    public void TheLabourBound_LosesAtMostOneUnitAStep_SoAOneThirdPaceCompletesOnTheFourthTurn()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        double c = C(w, S0, 10.0);
        SimConfig cfg = WithWorks(Cfg, 3.0 * c);                              // C / L = 1/3: 333,333.33… a step
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 100);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0, Works)));
        WorldState t = w;
        long p = 0;
        for (int k = 1; k <= 3; k++)
        {
            t = exec.Step(t);
            long dp = P(t, S0) - p;
            Assert.Equal(333_333, dp);
            double ideal = c / (3.0 * c) * N;
            Assert.True(ideal - dp >= 0.0 && ideal - dp < 1.0, $"per-step loss {ideal - dp} must be under one unit (1 ppm)");
            p = P(t, S0);
        }
        Assert.Equal(999_999, p);                                             // three steps fall one unit short
        t = exec.Step(t);
        Assert.Equal(1, Built(t, S0, Works));                                 // the fourth completes
    }

    // ================================================================== shortage, lockstep, resumption

    [Fact]
    public void AShortage_StopsProgress_FromABucketEdgeNothingIsDrawn_AndNothingAlreadyInIsLost()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);                                              // timber plenty, stone none
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, 0, 0), ProgressRow(t1, S0));
        Assert.Equal(100, Of(t1, S0, Timber));                                // not one unit of timber without stone
        Assert.Equal(0, t1.ConstructionLabor.Count);                          // no labour used: nothing advanced
        WorldState t2 = exec.Step(t1);
        Assert.Equal(0, P(t2, S0));
        Assert.Equal(100, Of(t2, S0, Timber));
    }

    /// <summary>The material bound FLOORS: with a cost that does not divide N (stone 3), one stone held moves the head to
    /// floor(1 × N / 3) = 333,333 — whose lockstep delivery is exactly that one stone — never to 333,334, whose delivery
    /// ceil(3 × 333,334 / N) = 2 would take a unit the purse does not hold.</summary>
    [Fact]
    public void TheMaterialBound_Floors_WhenTheCostDoesNotDivideN_NoUnitBeyondWhatIsHeld()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 1);
        SimConfig cfg = WithWorks(Cfg, 1.0, Works, timber: 7, stone: 3);
        WorldState t1 = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0, Works))).Step(w);
        Assert.Equal(333_333, P(t1, S0));
        Assert.Equal((97L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));    // ceil(7 × 0.333333) = 3 timber, the 1 stone
        AssertConserved(t1);
    }

    [Fact]
    public void Lockstep_DrawsInTheStepThatReachesTheEdge_ThenWaits_ThenResumesWhenTheMaterialReturns_TotalExactlyTheCost()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 3);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0)));

        // 3 stone of 20: p' = floor(3 × N / 20) = 150,000; timber ceil(40 × 0.15) = 6 goes in with it.
        WorldState t1 = exec.Step(w);
        Assert.Equal(150_000, P(t1, S0));
        Assert.Equal((94L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));
        Assert.Equal(2.0 * 150_000.0 / N, Labour(t1, S0));

        // The shortage: at the bucket edge, nothing moves and nothing already in is lost.
        WorldState t2 = exec.Step(t1);
        Assert.Equal(150_000, P(t2, S0));
        Assert.Equal((94L, 0L), (Of(t2, S0, Timber), Of(t2, S0, Stone)));

        // The stone returns: the rest goes in and the granary completes — 40 timber and 20 stone in all.
        Set(t2, S0, Stone, 17);
        WorldState t3 = exec.Step(t2);
        Assert.Equal(1, Built(t3, S0));
        Assert.Equal((60L, 0L), (Of(t3, S0, Timber), Of(t3, S0, Stone)));
        Assert.Equal((40L, 20L), (Sunk(t3, Timber) - Sunk(w, Timber), Sunk(t3, Stone) - Sunk(w, Stone) - 0));
        Assert.Equal(0, t3.ConstructionProgress.Count);
    }

    // ================================================================== competition, reservation, no double spend

    [Fact]
    public void CompetingHeads_AreServedOldestOrderFirst_TheExactSplit_NotSettlementIdOrder()
    {
        // S1 ordered at turn 0, S0 at turn 1; both wait for stone until turn 2, when the purse holds 25: S1 (older, the
        // HIGHER id) takes its whole 20 and completes; S0 gets the 5 left: p = floor(5 × N / 20) = 250,000.
        WorldState w = Solo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        Set(w, S2, Timber, 200);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S1), Enqueue(1, Player, S0)));
        WorldState t2 = exec.Run(w, 2);
        Assert.Equal((0L, 0L), (P(t2, S0), P(t2, S1)));
        Assert.Equal(new ConstructionProgressRow(S1, 0, Granary, 0, 0), ProgressRow(t2, S1));
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, 1, 0), ProgressRow(t2, S0));
        Set(t2, S3, Stone, 25);
        WorldState t3 = exec.Step(t2);
        Assert.Equal(1, Built(t3, S1));
        Assert.Equal(250_000, P(t3, S0));
        Assert.Equal(0, Of(t3, S3, Stone));
        Assert.Equal(200 - 40 - 10, Of(t3, S2, Timber));                       // S1's 40, S0's ceil(40 × 0.25) = 10
        AssertConserved(t3);
    }

    /// <summary>The gated-head rig: S1, the OLDER order (turn 0), has no builders; S0 (turn 1) has every builder; the
    /// player's purse holds 40 timber at S2, and 20 stone arrive there before step 2 → 3.</summary>
    private static (WorldState Before, WorldState After) GatedEarlierHead(SimConfig cfg)
    {
        WorldState w = Solo();
        NoBuilders(w, S1);                                                    // S1, the older order, has no builders
        AllBuilders(w, S0);
        Set(w, S2, Timber, 40);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S1), Enqueue(1, Player, S0)));
        WorldState t2 = exec.Run(w, 2);
        Set(t2, S2, Stone, 20);
        return (t2, exec.Step(t2));
    }

    /// <summary>
    /// D-055 RE-PIN (2026-10-16; the Director's batch-4 instruction, item 1 — "projects must not consume resources reserved
    /// by other projects"; batch-4 design §1.7, a DEFECT pin; MEASURED on this tree by the agent writing this line). This
    /// test was <c>AGatedEarlierHead_TakesNothing_AndTheLaterHeadUsesThePurse</c>: the older head S1 had no builders, drew
    /// nothing, and the younger S0 took the purse's 40 timber and 20 stone and completed — built (S1, S0) = (0, 1), S2 left
    /// (0, 0): exactly the batch-3 defect the instruction names (D-054 Q17). CAUSE: the hold rule (sim.json
    /// construction.reservations; ConstructionQuery.HoldsWalk). S1's head is a VALID wait — no builders, adults present, the
    /// player's labour allocation — so it draws nothing and SETS ASIDE its remaining need of the player's purse, 40 timber and
    /// 20 stone; S0 waits, HeldForEarlier naming S1. NEW: built (0, 0), both heads at p = 0, S2 keeps (40, 20), the published
    /// holds (purse 1) timber 40 and stone 20. ATTRIBUTION: the old values reproduce under TestConfigs.PreReservations, the
    /// batch-3 rule (<see cref="AGatedEarlierHead_UnderPreReservations_TakesNothing_AndTheLaterHeadUsesThePurse"/>).
    /// </summary>
    [Fact]
    public void AGatedEarlierHead_TakesNothing_ButSetsAsideItsNeed_AndTheLaterHeadWaits()
    {
        (WorldState t2, WorldState t3) = GatedEarlierHead(Cfg);
        Assert.Equal((0L, 0L), (Built(t3, S1), Built(t3, S0)));
        Assert.Equal((0L, 0L), (P(t3, S1), P(t3, S0)));
        Assert.Equal((40L, 20L), (Of(t3, S2, Timber), Of(t3, S2, Stone)));
        int purse = Reservations.PolityPurse(Player);
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 40), new ConstructionHoldRow(purse, Stone, 20)], t3.ConstructionHolds.Rows());
        HeadStatusReport younger = ConstructionQuery.HeadStatus(t2, Cfg, S0, null, 10.0)!;
        Assert.Equal((HeadLimitKind.Materials, MaterialCause.HeldForEarlier), (younger.Resolution.Limit, younger.Cause));
        Assert.Equal(new QueueEntryRef(S1, 0, Granary), younger.Holder);
        HeadStatusReport older = ConstructionQuery.HeadStatus(t2, Cfg, S1, null, 10.0)!;
        Assert.Equal((HeadLimitKind.Labour, false, HoldRelease.None), (older.Resolution.Limit, older.Resolution.NoLabourAdults, older.Release));
        AssertConserved(t3);
    }

    /// <summary>D-055 (2026-10-16) — the PreReservations twin of
    /// <see cref="AGatedEarlierHead_TakesNothing_ButSetsAsideItsNeed_AndTheLaterHeadWaits"/>: the batch-3 pin, unchanged.</summary>
    [Fact]
    public void AGatedEarlierHead_UnderPreReservations_TakesNothing_AndTheLaterHeadUsesThePurse()
    {
        (_, WorldState t3) = GatedEarlierHead(TestConfigs.PreReservations(Cfg));
        Assert.Equal((0L, 1L), (Built(t3, S1), Built(t3, S0)));
        Assert.Equal(0, P(t3, S1));
        Assert.Equal((0L, 0L), (Of(t3, S2, Timber), Of(t3, S2, Stone)));
        Assert.Equal(0, t3.ConstructionHolds.Count);
    }

    /// <summary>The reservation rig of <see cref="ProjectsDoNotConsumeResourcesReservedByOtherProjects"/>: S3 (the HIGH id)
    /// ordered at turn 0 at a pace of 250,000 a step, S0 (the LOW id) at turn 1 with builders to spare, 200 timber at S1,
    /// one scarce purse of 12 stone set before step 2 → 3, and S3 cancelled by an order stamped turn 3.</summary>
    private static (WorldState T2, WorldState T3, TurnExecutor Exec, SimConfig Cfg) Reserved(SimConfig cfg0)
    {
        WorldState w = Solo();
        Share(w, S3, 1.0);
        AllBuilders(w, S0);
        double c3 = C(w, S3, 10.0);
        SimConfig cfg = WithWorks(cfg0, 4.0 * c3);
        Set(w, S1, Timber, 200);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S3, Works), Enqueue(1, Player, S0), Cancel(3, Player, S3, 0)));
        WorldState t2 = exec.Run(w, 2);                                       // both queued, no stone yet
        Assert.Equal((0L, 0L), (P(t2, S3), P(t2, S0)));
        Set(t2, S1, Stone, 12);                                               // ONE scarce purse of 12 stone
        return (t2, exec.Step(t2), exec, cfg);
    }

    /// <summary>
    /// D-054's definition of "reserved" (Director WS1 item 5): a project never consumes (a) what another project has already
    /// had delivered — sunk — nor (b) what an EARLIER project in the building order takes this step.
    ///
    /// D-055 RE-PIN of step (b), step (a) RE-ASSERTED (2026-10-16; the Director's batch-4 instruction, item 1 — the
    /// requirement this test is named for, now including what an earlier project still NEEDS; batch-4 design §1.7, a DEFECT
    /// pin; MEASURED on this tree by the agent writing this line). OLD (b): S3 took its 5 stone and S0 the 7 left — P(S0) =
    /// 350,000, stone 0, timber 176, 12 stone sunk. That left S3, which advances at a labour pace and still needs 15 stone,
    /// with nothing for its next step: the defect. CAUSE: the hold rule (sim.json construction.reservations;
    /// ConstructionQuery.HoldsWalk) — S3, earlier in the building order, draws its 5 and then SETS ASIDE min(its remaining
    /// 15, the 7 left) = 7 stone and its remaining 30 timber; S0 can draw no stone, so it does not move and sets aside its
    /// own 40 timber of the 160 left. NEW (b): P(S3) = 250,000, P(S0) = 0, the 7 stone stay (set aside), timber 200 − 10 =
    /// 190, 5 stone sunk; published holds (purse 1) timber 30 + 40 = 70, stone 7. Step (a) is unchanged in meaning: the
    /// cancel (stamped turn 3) forfeits S3's 5 sunk stone — with the purse emptied, nothing returns and S0 still cannot
    /// move (P(S0) 0 → 0; 5 stone sunk in all, 12 before). ATTRIBUTION: the old values of both steps reproduce under
    /// TestConfigs.PreReservations, the batch-3 rule
    /// (<see cref="ProjectsDoNotConsumeResourcesReservedByOtherProjects_UnderPreReservations_IsBatch3"/>).
    /// </summary>
    [Fact]
    public void ProjectsDoNotConsumeResourcesReservedByOtherProjects()
    {
        (WorldState t2, WorldState t3, TurnExecutor exec, SimConfig cfg) = Reserved(Cfg);
        // (b): S3, earlier in the building order, takes its 5 first (delivered(20, 250,000) = 5) and sets aside the 7 left.
        Assert.Equal(250_000, P(t3, S3));
        Assert.Equal(0, P(t3, S0));
        Assert.Equal(7, Of(t3, S1, Stone));
        Assert.Equal(200 - 10, Of(t3, S1, Timber));                           // S3 ceil(40 × .25) = 10; S0 draws nothing
        Assert.Equal(5, Sunk(t3, Stone) - Sunk(t2, Stone));
        int purse = Reservations.PolityPurse(Player);
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 30 + 40), new ConstructionHoldRow(purse, Stone, 7)], t3.ConstructionHolds.Rows());
        HeadStatusReport younger = ConstructionQuery.HeadStatus(t2, cfg, S0, null, 10.0)!;
        Assert.Equal((MaterialCause.HeldForEarlier, Stone, 7L), (younger.Cause, younger.Resolution.LimitGood, younger.SetAsideAhead));
        Assert.Equal(new QueueEntryRef(S3, 0, Works), younger.Holder);

        // (a): cancelling S3 (stamped turn 3) forfeits its 5 stone: they never return to the purse and S0 cannot use them.
        Set(t3, S1, Stone, 0);
        WorldState t4 = exec.Step(t3);
        Assert.Empty(ConstructionQuery.Queue(t4, S3));
        Assert.Equal(0, TotalOf(t4, Stone));
        Assert.Equal(0, P(t4, S0));                                           // S0 did not advance on S3's sunk stone
        Assert.Equal(5, Sunk(t4, Stone) - Sunk(t2, Stone));
        Assert.Equal([new ConstructionHoldRow(purse, Timber, 40)], t4.ConstructionHolds.Rows());   // S0 alone now holds
        AssertConserved(t4);
    }

    /// <summary>D-055 (2026-10-16) — the PreReservations twin of <see cref="ProjectsDoNotConsumeResourcesReservedByOtherProjects"/>:
    /// both steps' batch-3 pins, unchanged.</summary>
    [Fact]
    public void ProjectsDoNotConsumeResourcesReservedByOtherProjects_UnderPreReservations_IsBatch3()
    {
        (WorldState t2, WorldState t3, TurnExecutor exec, _) = Reserved(TestConfigs.PreReservations(Cfg));
        Assert.Equal(250_000, P(t3, S3));
        Assert.Equal(350_000, P(t3, S0));                                     // floor(7 × N / 20)
        Assert.Equal(0, Of(t3, S1, Stone));
        Assert.Equal(200 - 10 - 14, Of(t3, S1, Timber));                      // S3 ceil(40 × .25) = 10, S0 ceil(40 × .35) = 14
        Assert.Equal(12, Sunk(t3, Stone) - Sunk(t2, Stone));
        Set(t3, S1, Stone, 0);
        WorldState t4 = exec.Step(t3);
        Assert.Empty(ConstructionQuery.Queue(t4, S3));
        Assert.Equal(0, TotalOf(t4, Stone));
        Assert.Equal(350_000, P(t4, S0));
        Assert.Equal(12, Sunk(t4, Stone) - Sunk(t2, Stone));
        Assert.Equal(0, t4.ConstructionHolds.Count);
        AssertConserved(t4);
    }

    /// <summary>
    /// D-055 (2026-10-16; batch-4 design §1.7, a behaviour candidate: "exact road units"; MEASURED on this tree by the agent
    /// writing this line). Batch 3 bounded the road's spend (0 &lt; units ≤ half) and never pinned it. Now pinned in both
    /// arms: with the hold rule (shipped) S3's works, a head that advanced 250,000 and still needs 30 timber and 15 stone,
    /// SET ASIDE that need after their draw, so road works — later in the same step — get half − 30 = 30 timber (0 →
    /// 0.25 of the route) and the published holds are (purse 1) timber 30, stone 15; under TestConfigs.PreReservations (the
    /// batch-3 rule) the road takes the whole leftover, half = 60 timber (0 → 0.5), and nothing is held.
    /// </summary>
    [Theory]
    [InlineData(true, 30L, 0.25)]
    [InlineData(false, 60L, 0.5)]
    public void TwoHeadsAndARoad_InOneStep_NoUnitSpentTwice_NoNegativeStock_TheLedgerBalances(bool reservations, long roadUnits, double after)
    {
        // pipeline.json: construction BEFORE roaddevelopment, both on next.GoodStocks. Route S0–S1, the trackway known.
        SimConfig shipped = reservations ? Cfg : TestConfigs.PreReservations(Cfg);
        WorldState w = Solo();
        for (int s = 0; s < 4; s++)
            w.Settlements[s] = w.Settlements[s] with { SiteCell = w.Terrain!.Index(new[] { 100, 130, 100, 150 }[s], new[] { 100, 100, 105, 150 }[s]) };
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        AllBuilders(w, S1);
        AllBuilders(w, S3);
        SimConfig cfg = WithWorks(shipped, 4.0 * C(w, S3, 10.0));            // S3's works: 250,000 units a step
        RoadDevelopmentStep step = Assert.Single(RoadDevelopmentQuery.Plan(w, Research, cfg.Roads!, cfg.Goods!, Player, 100.0));
        long road = Assert.Single(step.Cost).Units;                           // timber per km × length
        long half = road / 2;
        Assert.True(half > 0, "anti-vacuity: the road costs timber");
        Assert.Equal(120, road);                                              // MEASURED: 60 is half; the works still need 30
        Set(w, S2, Timber, 40 + 10 + half);                                   // the granary's 40, the works' first 10, half the road
        Set(w, S2, Stone, 40);
        var both = new SystemRegistration[] { SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg) };
        long timber0 = TotalOf(w, Timber), sunk0 = Sunk(w, Timber), stone0 = TotalOf(w, Stone);
        WorldState next = new TurnExecutor(ResearchRigs.FlatEra(10.0), both,
            Log(Enqueue(0, Player, S1), Enqueue(0, Player, S3, Works), RoadDevelopmentQuery.DevelopOrder(w, Player, 100.0))).Step(w);
        // One EnqueuedTurn: S1 then S3 (settlement id). S1's granary takes its whole 40 timber and 20 stone and completes;
        // S3's works advance 250,000 — 10 timber and 5 stone; the road, later in the pipeline, is paid only from the rest.
        Assert.Equal(1, Built(next, S1));
        Assert.Equal(250_000, P(next, S3));
        Assert.Equal(stone0 - 25, TotalOf(next, Stone));
        RoadDevelopmentRow dev = Assert.Single(next.RoadDevelopments.Rows());
        Assert.True(dev.MaterialUnits > 0 && dev.MaterialUnits <= half, $"the road spent {dev.MaterialUnits} of the {half} building work left");
        Assert.Equal((roadUnits, after), (dev.MaterialUnits, dev.ProgressAfter));
        Assert.Equal(timber0 - 50 - dev.MaterialUnits, TotalOf(next, Timber));
        Assert.Equal(50 + dev.MaterialUnits, Sunk(next, Timber) - sunk0);     // every unit spent once, through the Ledger
        int purse = Reservations.PolityPurse(Player);
        Assert.Equal(reservations ? new[] { new ConstructionHoldRow(purse, Timber, 30), new ConstructionHoldRow(purse, Stone, 15) }
            : Array.Empty<ConstructionHoldRow>(), next.ConstructionHolds.Rows());
        if (reservations) Assert.Equal(30, TotalOf(next, Timber));            // exactly the works' set-aside is left
        AssertConserved(next);
    }
}
