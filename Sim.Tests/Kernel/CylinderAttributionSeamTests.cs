using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 "Seam exposure", review F7-WRAP-NEVER-EXERCISED) — THE SEAM HALVES of the
/// distance-reading features. Every seed-42 and dev rig has 0 seam-crossing pairs (MEASURED, recorded per rig), so a
/// TOPOLOGY-BLIND implementation (a feature reading raw dx where the cylinder's MapTopology reads the short way round) would
/// pass all of them. Each rig here puts the feature's own distance reading ACROSS the seam and pins what the cylinder
/// decides, so the topology-blind mutant of that feature fails it:
/// <list type="bullet">
/// <item><b>Relief's donor order</b> — SeamWorld (the land strip across the seam), a seam-spanning donor set: the starving
///   settlement's nearest sibling the short way round lies ACROSS the seam and has the HIGHEST id, so the cylinder's
///   nearest-first order differs from both the raw-dx order and ascending id (T2 fix round, finding T2-V1). Turn-exact
///   against the relief-off twin; read off the grain books, the across-seam sibling gives first (all it holds) and the
///   interior one the remainder, so a topology-blind (raw dx) and a distance-blind (id order) donor order both fail.</item>
/// <item><b>The construction purse's donor order</b> — seed 16 (the canonical worldgen, land across the seam: 5 seam-crossing
///   pairs, 4 wrapped donor orders): a granary at the settlement whose donor order the seam changes most, against the purse-off
///   twin; the stone donors are the cylinder order's.</item>
/// <item><b>Weather geography</b> — seed 16: the weather a settlement draws depends on its TRUE separation from the others,
///   so it is invariant under any rotation of the settlements around the cylinder (which moves the seam through them), and
///   it is the weather the shipped pipeline produces. Since the T2 fix round (finding T2-V2) the invariance is not vacuous:
///   moving one settlement (which changes separations, unlike a rotation) changes the weather, and the step is the documented
///   kernel's, recomputed by <see cref="HarvestWeatherReference"/>.</item>
/// <item><b>AI founding</b> — SeamWorld with an AI Empire on the east edge: its first colony is founded across the seam, on
///   the west piece reachable only the short way round. Turn-exact against the founding-off twin.</item>
/// <item><b>Trade</b> — SeamWorld, the two settlements either side of the seam (crafts known, trade not): the ungated
///   arm's first trade flows across the seam. Turn-exact against the gated (shipped) arm.</item>
/// </list>
/// The AI-founding and trade halves read distance only through the SHARED traversal lattice and its Pathfinder (the
/// founding flood, the pair travel costs); neither feature has a dx reading of its own. So they prove that the shared
/// lattice wraps for these features — a topology property — and cannot tell a topology-blind feature from topology-blind
/// pathfinding: a non-wrapping lattice fails them with the same messages as their topology-blind mutants (MEASURED, T2 fix
/// round: "the AI's colony (x 255) is not on the west piece"; trade "Expected: 34, Actual: −1").
/// Every rig is the shipped cylinder (WrapX asserted), both arms on the same terrain and orders, world 0 identical, the first
/// action read independently of the hash, the write set measured and topology-free, and (T2 fix round) the ON arm
/// reproduced by a second, independent ON run.
/// </summary>
public class CylinderAttributionSeamTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe logs t2-out/s2/probe-a.txt: A1, A4, D4–D6,
    // E1, E2):
    //   relief (SeamWorld R (20,128), A (236,128), B (20,178); ZeroFoodOrders(0)): the cylinder donor order of R is [R, A, B]
    //     (A 40 px the short way, B 50 px; flat [R, B, A]); 2 seam-crossing pairs (A–R, A–B), 1 wrapped donor order (R's);
    //     first relief world 3 = first different; at world 3 A gave 313 (all it held: 0 left) and B the remaining 90.
    //   purse (seed 16, a granary at s6 ordered at turn 0): s6's cylinder order [6,1,4,10,7,3,5,2,9,8,0,11], flat
    //     [6,1,4,10,5,8,0,9,11,3,2,7]; first different world 1; at world 1 the stone came from s10 (4), s5 (9), s2 (2) and
    //     s8 (5) — the flat order would have taken s8's 7 and none of s2's; s6 used its own 40 timber.
    //   weather (seed 16): 5 seam-crossing pairs of 66; the single-system weather step on world 0 equals the shipped
    //     pipeline's world 1 weather and is unchanged under every rotation tried (0 differing rows), while the rotations
    //     move the seam through the settlements (crossing pairs 5 … 31).
    //   AI founding (SeamWorld AI (215,180) player, (236,128) AI; west 0.9, east 0.3): the AI's first FoundSettlement is
    //     stamped 35, source s1 (x 236) to the site (44, 94) — raw dx −192, the short way +64; first different world 36.
    //   trade (SeamWorld.TwoSides, west 0.9, east 0.3, recipes known): the ungated arm's first TradeFlows row is in world 34
    //     (s1 → s0, across the seam); the gated arm none in 36; first different world 34.
    // ---------------------------------------------------------------------------------------------------------------
    // RE-PIN (D-055, 2026-10-16; batch 4 track T2 FIX ROUND, run 2026-10-17) — the relief rig, CAUSE: finding T2-V1
    // (adjudicated partial): on the sites above the cylinder's donor order of R, [R, A, B] = [0, 1, 2], EQUALS ascending
    // id, so the rig could not tell nearest-first from id order (mutant V1, PayingSettlements(prev, polity) — id order —
    // survived it, MEASURED on this clone), and its amounts were ON − OFF stock differences, which also carry each donor's
    // spoilage and granary bound. The old values reproduced on this clone before the change (probe t2-out/fix/probes/
    // relief-probe-run1.txt, the old numbering): first relief world 3 = first different; A's stock ON − OFF −313 (ON 0, OFF
    // the bound-capped 313) and B's −90 — while the grain books say the grant was 3,160 and A gave 2,062 (all it held after
    // eating) and B 1,098. New rig: the sites renumbered R (20,128) = s0, the INTERIOR sibling (20,178) = s1, the ACROSS-SEAM
    // sibling (236,128) = s2. MEASURED (same probe, the new numbering), base-b3 + T1a + T2, Release, by the agent writing
    // this block: the cylinder order [0, 2, 1]; raw dx and ascending id both [0, 1, 2]; 2 seam-crossing pairs, 1 wrapped
    // donor order; first relief world 3 = first different; at world 3 R ate 3,170 beyond its own store (its grain residual
    // −3,170; the Ledger's cumulative Eaten sink ON − OFF 26,471 − 23,301 = 3,170); the across-seam sibling gave 1,983, all it
    // held after eating (its residual, closing 0); the interior one the remainder 1,187 (its own closing is the granary
    // bound, 313, in both arms, so no stock difference shows it).
    // ---------------------------------------------------------------------------------------------------------------
    private const int ReliefFirst = 3;
    private const long ReliefGrant = 3_170, ReliefAcrossTheSeam = 1_983, ReliefRemainder = 1_187;
    private const int PurseFirst = 1;
    private const long AiFoundingStamp = 35;
    private const int AiFoundingDiverges = (int)AiFoundingStamp + 1;
    private const int TradeFirst = 34;
    private const int Seed16CrossingPairs = 5, Seed16WrappedDonorOrders = 4;
    // MEASURED (D-055, 2026-10-16; batch 4 track T2 FIX ROUND, run 2026-10-17), base-b3 + T1a + T2, Release, by the agent
    // writing this block — the weather rig's non-vacuity guard (finding T2-V2: a kernel weighting every pair 1, which is
    // rotation-invariant too, passed the rig): on seed 16, moving settlement 0 by 160 px east along its row (separations
    // change; a rotation would not change them) changes all 12 of the weather step's rows; the step equals
    // HarvestWeatherReference's recomputation bit for bit. (The verdict's probe measured 12 of 12 for 40, 160 and 300 px.)
    private const int MovePx = 160, WeatherRowsMovedByOneSite = 12;
    private static readonly string[] ReliefWrites = ["ConsumptionDeficits", "GoodStocks", "LedgerFlows"];
    private static readonly string[] PurseWrites =
        ["ConstructionHolds", "ConstructionLabor", "ConstructionProgress", "ConstructionQueue", "GoodStocks", "LedgerFlows", "Structures"];
    private static readonly string[] FoundingWrites =
    [
        "Buckets", "ClassStates", "Controls", "Deposits", "GoodStocks", "Grievance", "LedgerFlows", "SettlementVitals",
        "Settlements", "SmoothedAttractiveness",
    ];
    private static readonly string[] TradeWrites = ["GoodStocks", "TradeFlows"];

    // ------------------------------------------------------------------ a lockstep for hand-built (SeamWorld) rigs

    private sealed record Pair(WorldState[] A, WorldState[] B, OrderLog LogA, OrderLog LogB, int FirstDifferent);

    /// <summary>Steps two arms of one hand-founded world in lockstep through the production pipeline (built with
    /// <paramref name="pipeline"/>), delivering <paramref name="player"/>'s batches and, for an AI rig, the AI producer's
    /// orders after them; keeps every world.</summary>
    private static Pair Lockstep(WorldState a, WorldState b, SimConfig ca, SimConfig cb, WorldgenConfig pipeline, int horizon,
        OrderLog? player = null, bool ai = false)
    {
        Assert.True(a.Terrain!.WrapX, "the seam rig is not the cylinder");
        player ??= new OrderLog();
        OrderLog la = new(), lb = new();
        TurnExecutor ea = R.Pipeline(ca, pipeline, la), eb = R.Pipeline(cb, pipeline, lb);
        var wa = new WorldState[horizon + 1];
        var wb = new WorldState[horizon + 1];
        wa[0] = a;
        wb[0] = b;
        int first = WorldHash.ComputeHex(a) == WorldHash.ComputeHex(b) ? -1 : 0;
        for (int t = 1; t <= horizon; t++)
        {
            OrderBatch batch = player.BatchFor(wa[t - 1].Clock.Turn);
            for (int k = 0; k < batch.Count; k++) { la.Append(batch[k]); lb.Append(batch[k]); }
            if (ai)
            {
                AiOrders.Append(la, wa[t - 1], ca, pipeline);
                AiOrders.Append(lb, wb[t - 1], cb, pipeline);
            }
            wa[t] = ea.Step(wa[t - 1]);
            wb[t] = eb.Step(wb[t - 1]);
            if (first < 0 && WorldHash.ComputeHex(wa[t]) != WorldHash.ComputeHex(wb[t])) first = t;
        }
        return new Pair(wa, wb, la, lb, first);
    }

    /// <summary>DETERMINISM (design §2 template; T2 fix round, finding SEAM-RIGS-NO-DETERMINISM-LEG): <paramref name="again"/>,
    /// a freshly founded copy of the ON arm's world 0, stepped through a fresh pipeline with the same order source, has the
    /// ON arm's hash at every turn of <paramref name="series"/>.</summary>
    private static void AssertOnArmRepeats(WorldState[] series, WorldState again, SimConfig cfg, WorldgenConfig pipeline,
        OrderLog? player = null, bool ai = false)
    {
        player ??= new OrderLog();
        var log = new OrderLog();
        TurnExecutor exec = R.Pipeline(cfg, pipeline, log);
        WorldState w = again;
        Assert.True(WorldHash.ComputeHex(series[0]) == WorldHash.ComputeHex(w), "a second founding of the ON arm differs at world 0");
        for (int t = 1; t < series.Length; t++)
        {
            OrderBatch batch = player.BatchFor(w.Clock.Turn);
            for (int k = 0; k < batch.Count; k++) log.Append(batch[k]);
            if (ai) AiOrders.Append(log, w, cfg, pipeline);
            w = exec.Step(w);
            Assert.True(WorldHash.ComputeHex(series[t]) == WorldHash.ComputeHex(w), $"a second ON run differs at turn {t}");
        }
    }

    private static long Stock(IReadOnlyWorldState w, SettlementId s, int good)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(good));
        return i < 0 ? 0 : w.GoodStocks[i].Amount.Value;
    }

    private static int[] Ids(SettlementId[] ids)
    {
        var r = new int[ids.Length];
        for (int i = 0; i < r.Length; i++) r[i] = ids[i].Value;
        return r;
    }

    /// <summary>The settlements of <paramref name="polity"/> ordered nearest-first to <paramref name="user"/> by RAW dx (a
    /// flat reading of the same sites; ties by id) — what a topology-blind donor order would be.</summary>
    private static int[] RawDxOrder(IReadOnlyWorldState w, PolityId polity, SettlementId user)
    {
        SettlementId[] ids = RoadDevelopmentQuery.PayingSettlements(w, polity);
        int size = w.Terrain!.Size, at = SiteOf(w, user);
        var keys = new (long D, int Id)[ids.Length];
        for (int i = 0; i < ids.Length; i++) keys[i] = (MapTopology.SquaredPx(SiteOf(w, ids[i]), at, size, wrap: false), ids[i].Value);
        Array.Sort(keys, static (x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.Id.CompareTo(y.Id));
        var r = new int[keys.Length];
        for (int i = 0; i < r.Length; i++) r[i] = keys[i].Id;
        return r;
    }

    private static int SiteOf(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return w.Settlements[i].SiteCell;
        throw new InvalidOperationException($"no settlement {s.Value}");
    }

    // ------------------------------------------------------------------ relief

    /// <summary>RELIEF across the seam: the starving settlement's grant is drawn NEAREST-FIRST TO IT, THE SHORT WAY ROUND —
    /// the sibling across the seam (the nearest on the cylinder, but the farthest by raw dx and the highest id) gives first,
    /// all it holds, and the interior one only the remainder. The sites are numbered so the cylinder's donor order differs
    /// from both the raw-dx order and ascending id: a topology-blind (raw dx) donor order and a distance-blind (id order)
    /// one each draw from the interior sibling first and leave grain across the seam. The amounts are read off the grain
    /// BOOKS (relief's Ledger.Transfer publishes no row; a donor's ON − OFF stock difference also carries its spoilage and
    /// granary bound), cross-checked against the Ledger's Eaten row.</summary>
    [Fact]
    public void Relief_DrawsFromTheSiblingAcrossTheSeamFirst_TurnExactAgainstTheReliefOffTwin()
    {
        SimConfig on = TestConfigs.PreM6(TestConfigs.Sim()), off = TestConfigs.PreEmpireFoodAllocation(on);
        int y = SeamWorld.DefaultSize / 2;
        int[] sites = [SeamWorld.Cell(20, y), SeamWorld.Cell(20, y + 50), SeamWorld.Cell(236, y)];
        var r = new SettlementId(0);
        var interior = new SettlementId(1);
        var across = new SettlementId(2);
        WorldState w0 = SeamWorld.Found(true, sites, sim: on);
        EmpireQuery.TryGetController(w0, r, out PolityId polity);
        Assert.Equal(new[] { 0, 2, 1 }, Ids(RoadDevelopmentQuery.PayingSettlements(w0, polity, r, r)));   // the cylinder's
        Assert.Equal(new[] { 0, 1, 2 }, RawDxOrder(w0, polity, r));                                       // raw dx's
        Assert.Equal(new[] { 0, 1, 2 }, Ids(RoadDevelopmentQuery.PayingSettlements(w0, polity)));         // ascending id
        Assert.Equal(2, R.SeamCrossingPairs(w0));                                                         // across–R, across–interior
        Assert.Equal(1, R.WrappedDonorOrders(w0));                                                        // R's order

        OrderLog starve = Sim.Tests.Observability.ExplainRigs.ZeroFoodOrders(0);
        Pair p = Lockstep(w0, SeamWorld.Found(true, sites, sim: off), on, off, SeamWorld.Worldgen(), ReliefFirst + 2, starve);
        int relief = -1;
        for (int t = 1; t <= ReliefFirst + 2 && relief < 0; t++) if (R.ReliefMovedGrain.Holds(p.A[t - 1], p.A[t], on)) relief = t;
        Assert.Equal(ReliefFirst, relief);                                       // the independent first action (the books)

        // What relief did in the step to world 3, read off the ON arm's grain books (prev → a).
        int grain = on.Goods!.GrainId;
        WorldState prev = p.A[ReliefFirst - 1], a = p.A[ReliefFirst], b = p.B[ReliefFirst];
        long grant = -R.GrainResidual(prev, a, r, on);                           // R ate this beyond its own store
        Assert.Equal(ReliefGrant, grant);
        Assert.Equal(grant, EatenSunk(a, grain) - EatenSunk(b, grain));          // the Ledger's Eaten row says the same
        Assert.Equal(0L, Stock(a, across, grain));                               // across the seam: drawn FIRST, emptied —
        Assert.Equal(ReliefAcrossTheSeam, R.GrainResidual(prev, a, across, on)); //   all it held after eating
        Assert.Equal(ReliefRemainder, grant - ReliefAcrossTheSeam);              // the interior one: only the remainder,
        Assert.True(Stock(a, interior, grain) > 0, "the interior sibling gave the remainder and keeps grain");

        Assert.Equal(ReliefFirst, p.FirstDifferent);
        string[] written = R.WriteSet(p.A[ReliefFirst], p.B[ReliefFirst]);
        Assert.Equal(ReliefWrites, written);
        R.AssertNoTopologyTable(written);
        AssertOnArmRepeats(p.A, SeamWorld.Found(true, sites, sim: TestConfigs.PreM6(TestConfigs.Sim())), TestConfigs.PreM6(TestConfigs.Sim()), SeamWorld.Worldgen(), starve);
    }

    /// <summary>The grain the Ledger has sunk as Eaten on <paramref name="w"/> (the cumulative LedgerFlows row).</summary>
    private static long EatenSunk(IReadOnlyWorldState w, int grain)
    {
        ConservedQuantityId q = ConservedQuantityIds.OfGood(new GoodId(grain));
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == q && w.LedgerFlows[i].Reason == ReasonIds.Eaten) return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    // ------------------------------------------------------------------ the construction purse

    /// <summary>THE PURSE across the seam (seed 16): a granary ordered at s6, which holds no stone, is paid from its
    /// Empire's purse nearest-first the short way round — the stone donors are a prefix of the cylinder order, not of the raw
    /// order.</summary>
    [Fact]
    public void ConstructionPurse_DrawsStoneInTheCylindersDonorOrder_Seed16()
    {
        SimConfig on = TestConfigs.PreM6(TestConfigs.Sim()), off = TestConfigs.PreEmpireConstructionStockpile(on);
        var rig = new R.RigWorld("seed16-purse", TestConfigs.Worldgen(), TestConfigs.Worldgen(), 16, Ai: false,
            Script: _ =>
            {
                var q = new OrderLog();
                q.Append(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 6, 1.0));
                return q;
            });
        R.Arm live = R.Run(rig, "on", on, PurseFirst + 2, keep: [0, PurseFirst]);
        R.Arm twin = R.Run(rig, "purse-off", off, PurseFirst + 2, keep: [PurseFirst]);
        WorldState w0 = live.At(0);
        var s6 = new SettlementId(6);
        Assert.Equal((Seed16CrossingPairs, Seed16WrappedDonorOrders), (R.SeamCrossingPairs(w0), R.WrappedDonorOrders(w0)));
        Assert.Equal(new[] { 6, 1, 4, 10, 7, 3, 5, 2, 9, 8, 0, 11 }, Ids(RoadDevelopmentQuery.PayingSettlements(w0, new PolityId(1), s6, s6)));
        Assert.Equal(new[] { 6, 1, 4, 10, 5, 8, 0, 9, 11, 3, 2, 7 }, RawDxOrder(w0, new PolityId(1), s6));

        R.AssertWorld0Identical(live, twin);
        Assert.Equal(PurseFirst, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(live.At(PurseFirst), twin.At(PurseFirst));
        Assert.Equal(PurseWrites, written);
        R.AssertNoTopologyTable(written);

        // The draws (ON − OFF; purse off, s6 cannot build at all): its own 40 timber, and 20 stone from s10, s5, s2, s8.
        int stone = on.Goods!.IdOf("stone"), timber = on.Goods.IdOf("timber");
        WorldState a = live.At(PurseFirst), b = twin.At(PurseFirst);
        var drawn = new List<(int Id, long Stone)>();
        for (int i = 0; i < a.Settlements.Count; i++)
        {
            SettlementId s = a.Settlements[i].Id;
            long d = Stock(b, s, stone) - Stock(a, s, stone);
            if (d != 0) drawn.Add((s.Value, d));
        }
        Assert.Equal(new List<(int, long)> { (2, 2), (5, 9), (8, 5), (10, 4) }, drawn);
        Assert.Equal(40L, Stock(b, s6, timber) - Stock(a, s6, timber));
        Assert.Equal(1L, BuiltGranaries(a, s6));
        R.AssertSameSeries(live, R.Run(rig, "on-again", TestConfigs.PreM6(TestConfigs.Sim()), PurseFirst + 2));   // determinism: a second ON run
    }

    private static long BuiltGranaries(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Structures.Count; i++)
            if (w.Structures[i].Settlement == s && w.Structures[i].ProjectId == 1) return w.Structures[i].Count;
        return 0;
    }

    // ------------------------------------------------------------------ weather geography

    /// <summary>WEATHER GEOGRAPHY across the seam (seed 16): the step's weather is the shipped pipeline's and the documented
    /// kernel's (recomputed), it moves when one settlement's separations change, and it is the same under every rotation of
    /// the settlements around the cylinder — the seam passes through different pairs, the true separations do not change,
    /// so neither does any settlement's weather.</summary>
    [Fact]
    public void WeatherGeography_TheWeatherIsInvariantUnderRotationsThatMoveTheSeam_Seed16()
    {
        SimConfig cfg = TestConfigs.PreM6(TestConfigs.Sim());
        Assert.False(cfg.HarvestVariance.LegacyTravelCostKernel);
        WorldState w0 = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 16);
        Assert.True(w0.Terrain!.WrapX);
        Assert.Equal(Seed16CrossingPairs, R.SeamCrossingPairsAll(w0));
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        var weather = new TurnExecutor(EraTableLoader.Load(era), [SystemCatalog.HarvestWeather(cfg)]);
        WorldState reference = weather.Step(w0.Clone());
        WorldState shipped = R.Pipeline(cfg, TestConfigs.Worldgen(), null).Step(w0.Clone());
        Assert.True(WorldStates.TableEquals(reference.HarvestWeather, shipped.HarvestWeather),
            "the single-system weather step is not the shipped pipeline's");

        // What the kernel reads (T2 fix round, finding T2-V2), before the invariance: moving ONE settlement (which changes
        // separations, unlike a rotation) moves the weather, so the invariance below is not a constant kernel's ...
        int size = w0.Terrain.Size;
        WorldState moved = w0.Clone();
        int c0 = moved.Settlements[0].SiteCell;
        moved.Settlements[0] = moved.Settlements[0] with { SiteCell = c0 / size * size + (c0 % size + MovePx) % size };
        WorldState movedStep = weather.Step(moved);
        Assert.Equal(reference.HarvestWeather.Count, movedStep.HarvestWeather.Count);
        int changed = 0;
        for (int i = 0; i < reference.HarvestWeather.Count; i++) if (reference.HarvestWeather[i] != movedStep.HarvestWeather[i]) changed++;
        Assert.Equal(WeatherRowsMovedByOneSite, changed);
        // ... and the step is the documented model's, with the kernel exp(−d / range) over the straight-line distance the
        // short way round, recomputed test-side (which also pins the kernel's distance SCALE, which the count above cannot).
        double dtYears = R.Eras().DtDaysAt(w0.Clock.SimDays) / (double)SimClock.YearDays;
        HarvestWeatherReference.AssertStepIs(w0, reference, cfg, dtYears, "seed 16");

        var crossings = new SortedSet<int>();
        foreach (int k in new[] { 64, 192, 320, 448, 576, 704, 832, 960 })
        {
            WorldState rotated = w0.Clone();
            for (int i = 0; i < rotated.Settlements.Count; i++)
            {
                int c = rotated.Settlements[i].SiteCell;
                rotated.Settlements[i] = rotated.Settlements[i] with { SiteCell = c / size * size + (c % size + k) % size };
            }
            crossings.Add(R.SeamCrossingPairsAll(rotated));
            WorldState stepped = weather.Step(rotated);
            Assert.True(WorldStates.TableEquals(reference.HarvestWeather, stepped.HarvestWeather),
                $"rotating the settlements {k} px round the cylinder changed the weather");
        }
        Assert.True(crossings.Count > 1, "the rotations never moved the seam through a different set of pairs (vacuous)");

        // Determinism: a second founding, stepped by a fresh weather executor, is the same world and the same weather.
        WorldState again = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.PreM6(TestConfigs.Sim()), 16);
        Assert.Equal(WorldHash.ComputeHex(w0), WorldHash.ComputeHex(again));
        WorldState againStep = new TurnExecutor(R.Eras(), [SystemCatalog.HarvestWeather(TestConfigs.PreM6(TestConfigs.Sim()))]).Step(again);
        Assert.Equal(WorldHash.ComputeHex(reference), WorldHash.ComputeHex(againStep));
    }

    // ------------------------------------------------------------------ AI founding

    /// <summary>AI FOUNDING across the seam (SeamWorld): the AI Empire on the east edge founds its first colony on the west
    /// piece, reachable only the short way round; the worlds are identical until that order's delivery.</summary>
    [Fact]
    public void AiFounding_FoundsAcrossTheSeam_TurnExactAgainstTheFoundingOffTwin()
    {
        SimConfig on = TestConfigs.PreM6(TestConfigs.Sim()), off = SingleSwitchTwins.PreAiFoundingOnly(on);
        int size = SeamWorld.DefaultSize;
        int[] sites = [SeamWorld.Cell(215, 180), SeamWorld.Cell(236, 128)];
        WorldState a = SeamWorld.Found(true, sites, aiEmpires: 1, sim: on, westFertility: 0.9, eastFertility: 0.3);
        WorldState b = SeamWorld.Found(true, sites, aiEmpires: 1, sim: off, westFertility: 0.9, eastFertility: 0.3);
        Pair p = Lockstep(a, b, on, off, SeamWorld.Worldgen(aiEmpires: 1), AiFoundingDiverges + 2, ai: true);

        Assert.Equal(AiFoundingStamp, R.FirstStamp(p.LogA, OrderKind.FoundSettlement));
        Assert.Equal(-1L, R.FirstStamp(p.LogB, OrderKind.FoundSettlement));
        Assert.Equal(AiFoundingDiverges, p.FirstDifferent);
        R.AssertLogsEqualBefore(p.LogA, p.LogB, AiFoundingStamp);
        R.AssertLogsEqualAtExcept(p.LogA, p.LogB, AiFoundingStamp, OrderKind.FoundSettlement);
        string[] written = R.WriteSet(p.A[AiFoundingDiverges], p.B[AiFoundingDiverges]);
        Assert.Equal(FoundingWrites, written);
        R.AssertNoTopologyTable(written);

        OrderRecord found = default;
        for (int i = 0; i < p.LogA.Count; i++) if (p.LogA[i].Kind == OrderKind.FoundSettlement) { found = p.LogA[i]; break; }
        Assert.Equal(2, found.ActorId);
        int source = SiteOf(a, new SettlementId(found.TargetId)) % size, site = FoundingQuery.SiteCellOf(found) % size;
        Assert.True(site < size / 2, $"the AI's colony (x {site}) is not on the west piece");
        Assert.NotEqual(site - source, MapTopology.Dx(source, site, size, wrap: true));   // its reach crosses the seam
        Assert.Equal(p.A[(int)AiFoundingStamp].Settlements.Count + 1, p.A[AiFoundingDiverges].Settlements.Count);
        AssertOnArmRepeats(p.A, SeamWorld.Found(true, sites, aiEmpires: 1, sim: TestConfigs.PreM6(TestConfigs.Sim()), westFertility: 0.9, eastFertility: 0.3),
            TestConfigs.PreM6(TestConfigs.Sim()), SeamWorld.Worldgen(aiEmpires: 1), ai: true);
    }

    // ------------------------------------------------------------------ trade

    /// <summary>TRADE across the seam (SeamWorld): with the crafts known and trade not, the gated (shipped) arm never trades
    /// and the ungated arm's first trade flows between the two settlements either side of the seam.</summary>
    [Fact]
    public void TradeKnowledge_TheUngatedArmTradesAcrossTheSeam_TurnExactAgainstTheGatedArm()
    {
        // ADR-045 (D-062): the R2a content twin is derived from the PRE-tranche file (the R4 rule), and the ON arm is the
        // pre-M6 world (TestConfigs.PreM6: its research content is that file) — the two arms differ by the R2a layer alone.
        SimConfig on = TestConfigs.PreM6(TestConfigs.Sim()), off = SingleSwitchTwins.PreTradeKnowledgeOnly(on);
        WorldState a = TestConfigs.KnowRecipes(SeamWorld.Found(true, SeamWorld.TwoSides(), sim: on, westFertility: 0.9, eastFertility: 0.3), on);
        WorldState b = TestConfigs.KnowRecipes(SeamWorld.Found(true, SeamWorld.TwoSides(), sim: off, westFertility: 0.9, eastFertility: 0.3), off);
        Assert.Equal(1, R.SeamCrossingPairs(a));
        Pair p = Lockstep(a, b, on, off, SeamWorld.Worldgen(), TradeFirst + 2);

        int firstFlow = -1, gatedFlow = -1;
        for (int t = 1; t <= TradeFirst + 2; t++)
        {
            if (firstFlow < 0 && p.B[t].TradeFlows.Count > 0) firstFlow = t;
            if (gatedFlow < 0 && p.A[t].TradeFlows.Count > 0) gatedFlow = t;
        }
        Assert.Equal(TradeFirst, firstFlow);                                      // the independent first action
        Assert.Equal(-1, gatedFlow);
        Assert.Equal(TradeFirst, p.FirstDifferent);
        string[] written = R.WriteSet(p.A[TradeFirst], p.B[TradeFirst]);
        Assert.Equal(TradeWrites, written);
        R.AssertNoTopologyTable(written);
        int size = SeamWorld.DefaultSize;
        for (int i = 0; i < p.B[TradeFirst].TradeFlows.Count; i++)
        {
            TradeFlowRow f = p.B[TradeFirst].TradeFlows[i];
            int xa = SiteOf(a, f.From) % size, xb = SiteOf(a, f.To) % size;
            Assert.NotEqual(xb - xa, MapTopology.Dx(xa, xb, size, wrap: true));     // every flow crosses the seam
        }
        SimConfig again = TestConfigs.PreM6(TestConfigs.Sim());
        AssertOnArmRepeats(p.A, TestConfigs.KnowRecipes(SeamWorld.Found(true, SeamWorld.TwoSides(), sim: again, westFertility: 0.9, eastFertility: 0.3), again),
            again, SeamWorld.Worldgen());
    }
}
