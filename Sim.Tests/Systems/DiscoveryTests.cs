using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Discovery;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 Track EX (D-062; ADR-044) — CONCEALED DEPOSITS, PROSPECT (OrderKind 15), EXPLORE (OrderKind 14) AND THE MINIMAL FOG,
/// end to end through the production pipeline. Worlds: the dev preset (256 px cylinder, 4 settlements, seed 42) for the
/// orders and the fog; the canonical 1024 px worlds of seeds 1, 2, 42 and 7 for the realized concealed share.
/// Every order-delivery semantic carries its own turn-exact pin (stamped t → first visible in t+1, never in t).
/// </summary>
public class DiscoveryTests
{
    private static readonly PolityId Player = new(1);

    private static TurnExecutor Exec(SimConfig cfg, OrderLog log, WorldgenConfig wg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
    }

    private static WorldState Dev(SimConfig cfg, int aiEmpires = 0) =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = aiEmpires }, cfg, 42);

    private static GoodId Good(SimConfig cfg, string name) => new(cfg.Goods!.IdOf(name));

    private static long Stock(IReadOnlyWorldState w, SettlementId s, GoodId g)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, g);
        return i >= 0 ? w.GoodStocks[i].Amount.Value : 0;
    }

    private static long Sunk(IReadOnlyWorldState w, GoodId g, ReasonId reason)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
        {
            LedgerFlowRow r = w.LedgerFlows[i];
            if (r.Quantity == ConservedQuantityIds.OfGood(g) && r.Reason == reason) return r.TotalSunk;
        }
        return 0;
    }

    /// <summary>The first player settlement with a concealed deposit, and that deposit's good.</summary>
    private static (SettlementId Settlement, GoodId Good) ConcealedAtPlayer(WorldState w, SimConfig cfg)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (!EmpireQuery.ControlsSettlement(w, Player, s)) continue;
            int[] concealed = DepositConcealment.ConcealedGoods(w.Deposits, w.Seed, cfg, s);
            if (concealed.Length > 0) return (s, new GoodId(concealed[0]));
        }
        throw new InvalidOperationException("no player settlement carries a concealed deposit");
    }

    private static OrderRecord Order(long turn, OrderKind kind, SettlementId s, PolityId? actor = null) =>
        OrderRecord.From(turn, actor ?? Player, kind, s.Value, 0.0);

    // ================================================================== classification (D1, D2)

    [Fact]
    public void Classification_IsReproducible_Stateless_AndLeavesTheDepositTableAndTheFoundedWorldByteIdentical()
    {
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreDiscovery(on);
        WorldState a = Dev(on), b = Dev(on), c = Dev(off);
        Assert.True(WorldStates.TableEquals(a.Deposits, c.Deposits));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(c));   // concealment draws no stream and writes nothing at founding
        for (int i = 0; i < a.Settlements.Count; i++)
        {
            SettlementId s = a.Settlements[i].Id;
            Assert.Equal(DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, s), DepositConcealment.ConcealedGoods(b.Deposits, b.Seed, on, s));
            Assert.Equal(DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, s), DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, s));
            Assert.Empty(DepositConcealment.ConcealedGoods(c.Deposits, c.Seed, off, s));   // the switch off: nothing concealed
            foreach (int g in DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, s))
                Assert.True(DepositConcealment.IsConcealable(on, g));
        }
    }

    [Fact]
    public void Classification_TieDense_EqualKeysOrderByGoodId_IndependentOfInputOrder()
    {
        // Every key equal: the k lowest are the k lowest good ids, whatever order the entries come in.
        (ulong, int)[] forward = [(5UL, 3), (5UL, 4), (5UL, 6), (5UL, 7)];
        (ulong, int)[] backward = [(5UL, 7), (5UL, 6), (5UL, 4), (5UL, 3)];
        Assert.Equal([3, 4], DepositConcealment.LowestByKey(forward, 2));
        Assert.Equal([3, 4], DepositConcealment.LowestByKey(backward, 2));
        // Mixed: the key decides first, the good id breaks ties.
        (ulong, int)[] mixed = [(9UL, 1), (2UL, 8), (2UL, 5), (9UL, 0)];
        Assert.Equal([5, 8, 0], DepositConcealment.LowestByKey(mixed, 3));
        Assert.Equal(0, DepositConcealment.ConcealedCount(0, 0.25, 0.99));
        Assert.Equal(1, DepositConcealment.ConcealedCount(4, 0.25, 0.0));
        Assert.Equal(1, DepositConcealment.ConcealedCount(4, 0.25, 0.999));
        Assert.Equal(0, DepositConcealment.ConcealedCount(3, 0.25, 0.2));
        Assert.Equal(1, DepositConcealment.ConcealedCount(3, 0.25, 0.25));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    [InlineData(7UL)]
    public void RealizedConcealedShare_AtTurnZero_IsWithin20And30Percent(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
        int eligible = 0, concealed = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            eligible += DepositConcealment.EligibleCount(w, cfg, w.Settlements[i].Id);
            concealed += DepositConcealment.ConcealedGoods(w.Deposits, w.Seed, cfg, w.Settlements[i].Id).Length;
        }
        Assert.True(eligible > 0);
        double share = concealed / (double)eligible;
        Assert.InRange(share, 0.20, 0.30);
    }

    // ================================================================== Prospect (D4)

    [Fact]
    public void Prospect_TurnExact_CostSunkExactly_AndTheConcealedDepositProducesOnlyAfterTheSurvey()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, GoodId hidden) = ConcealedAtPlayer(w0, cfg);
        GoodId tools = Good(cfg, "tools");
        GovernanceRigs.AddStock(w0, s, tools, 1_000);   // above what the step's farm-tool wear consumes before DiscoverySystem draws (ADR-044 §5)
        long cost = DiscoveryQuery.ProspectCost(w0, cfg, Player);
        Assert.Equal(cfg.Discovery!.ProspectTools, cost);   // no ore recognition at the founding
        Assert.True(DepositConcealment.IsHiddenFrom(w0, cfg, Player, s, hidden));
        Assert.Equal(0.0, DepositConcealment.KnownAbundance(w0, cfg, s, hidden.Value));

        // Extraction labour at the settlement (so the hidden deposit WOULD be worked if known), stamped turn 0.
        var log = new OrderLog();
        for (int sector = 0; sector < Sectors.Count; sector++)
            log.Append(OrderRecord.From(0, Player, OrderKind.SectorAllocation, s.Value * 8 + sector, sector == Sectors.Extraction ? 60 : 10));
        log.Append(Order(0, OrderKind.Prospect, s));
        var lateLog = new OrderLog();   // the turn-exact twin: the same order stamped turn 1
        for (int i = 0; i < log.Count - 1; i++) lateLog.Append(log[i]);
        lateLog.Append(Order(1, OrderKind.Prospect, s));

        TurnExecutor exec = Exec(cfg, log, TestConfigs.DevWorldgen()), late = Exec(cfg, lateLog, TestConfigs.DevWorldgen());
        WorldState w1 = exec.Step(w0), l1 = late.Step(w0);
        // Stamped 0 → the survey row is in the state of turn 1, stamped with the order's turn; the late twin has none yet.
        Assert.Equal(1, w1.SettlementSurveys.Count);
        Assert.Equal(new SettlementSurveyRow(Player, s, 0), w1.SettlementSurveys[0]);
        Assert.Equal(0, l1.SettlementSurveys.Count);
        Assert.Equal(cost, Sunk(w1, tools, ReasonIds.Prospecting));   // the cost, sunk exactly once
        Assert.Equal(0, Sunk(l1, tools, ReasonIds.Prospecting));
        // The goods account of the step reconciles with the sink named (Observer's identity).
        TurnRecord rec = Observer.Observe(w0, w1, cfg, []).Turn;
        foreach (GoodAccount g in rec.Goods)
            if (g.Good == tools.Value) { Assert.True(g.Reconciles); Assert.Equal(cost, g.Discovery); }
        // The step 0 → 1 read PREV (no survey): the hidden deposit produced nothing.
        Assert.Equal(0L, w1.GoodStocks[GoodStockIndex.IndexOf(w1.GoodStocks, s, hidden)].LastProducedUnits);
        Assert.False(DepositConcealment.IsHiddenFrom(w1, cfg, Player, s, hidden));

        // Step 1 → 2 reads the survey: the discovered deposit now produces. The late twin's survey lands in state 2.
        WorldState w2 = exec.Step(w1), l2 = late.Step(l1);
        Assert.True(w2.GoodStocks[GoodStockIndex.IndexOf(w2.GoodStocks, s, hidden)].LastProducedUnits > 0);
        Assert.Equal(0L, l2.GoodStocks[GoodStockIndex.IndexOf(l2.GoodStocks, s, hidden)].LastProducedUnits);
        Assert.Equal(new SettlementSurveyRow(Player, s, 1), l2.SettlementSurveys[0]);
    }

    [Fact]
    public void Prospect_Unaffordable_IsRefusedAndChargesNothing_TheStateIsTheOrderlessStep()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, _) = ConcealedAtPlayer(w0, cfg);
        Assert.Equal(0, Stock(w0, s, Good(cfg, "tools")));   // the founding holds no tools anywhere (ADR-044 §5)
        Assert.Equal(ProspectVerdict.Unaffordable, DiscoveryQuery.CheckProspect(w0, cfg, Player, s));
        var log = new OrderLog();
        log.Append(Order(0, OrderKind.Prospect, s));
        WorldState with = Exec(cfg, log, TestConfigs.DevWorldgen()).Step(w0);
        WorldState without = Exec(cfg, new OrderLog(), TestConfigs.DevWorldgen()).Step(w0);
        Assert.Equal(WorldHash.ComputeHex(without), WorldHash.ComputeHex(with));
    }

    [Fact]
    public void Prospect_ASecondProspect_SameTurnOrLater_IsRefused_NoRepeatCharge()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, _) = ConcealedAtPlayer(w0, cfg);
        GoodId tools = Good(cfg, "tools");
        GovernanceRigs.AddStock(w0, s, tools, 1_000);
        var log = new OrderLog();
        log.Append(Order(0, OrderKind.Prospect, s));
        log.Append(Order(0, OrderKind.Prospect, s));   // a duplicate in the same batch
        TurnExecutor exec = Exec(cfg, log, TestConfigs.DevWorldgen());
        WorldState w1 = exec.Step(w0);
        long cost = DiscoveryQuery.ProspectCost(w0, cfg, Player);
        Assert.Equal(cost, Sunk(w1, tools, ReasonIds.Prospecting));
        Assert.Equal(ProspectVerdict.AlreadySurveyed, DiscoveryQuery.CheckProspect(w1, cfg, Player, s));
        log.Append(Order(1, OrderKind.Prospect, s));   // a later turn
        WorldState w2 = exec.Step(w1);
        WorldState control = Exec(cfg, new OrderLog(), TestConfigs.DevWorldgen()).Step(w1);
        Assert.Equal(cost, Sunk(w2, tools, ReasonIds.Prospecting));
        Assert.Equal(WorldHash.ComputeHex(control), WorldHash.ComputeHex(w2));
    }

    [Fact]
    public void Prospect_TheOreRecognitionTechnology_HalvesTheCost()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg);
        Assert.Equal(4, DiscoveryQuery.ProspectCost(w, cfg, Player));
        Sim.Core.Systems.Research.ResearchContent research = cfg.Research!;
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, research.Nodes[research.IndexOfId("copper_smelting")].Key));
        Assert.True(DiscoveryQuery.HasProspectDiscount(w, cfg, Player));
        Assert.Equal(2, DiscoveryQuery.ProspectCost(w, cfg, Player));
    }

    // ================================================================== Explore (D6)

    [Fact]
    public void Explore_TurnExact_RadiusGrowsByTheStepToTheCap_GrainSunkPerExpedition_ExtentMonotone()
    {
        SimConfig cfg = TestConfigs.Sim();
        FogConfig fog = cfg.Fog!;
        WorldState w = Dev(cfg);
        SettlementId s = w.Settlements[0].Id;
        Assert.True(EmpireQuery.ControlsSettlement(w, Player, s));
        GoodId grain = new(cfg.Goods!.GrainId);
        var log = new OrderLog();
        TurnExecutor exec = Exec(cfg, log, TestConfigs.DevWorldgen());
        int expected = fog.SeedRadiusNodes;
        int paid = 0;
        WorldState prev = w;
        for (int t = 0; t < 6; t++)
        {
            ExploreVerdict v = DiscoveryQuery.CheckExplore(prev, cfg, Player, s);
            log.Append(Order(t, OrderKind.Explore, s));
            WorldState next = exec.Step(prev);
            if (expected < fog.ExploreMaxRadiusNodes)
            {
                Assert.Equal(ExploreVerdict.Ok, v);
                expected = Math.Min(fog.ExploreMaxRadiusNodes, expected + fog.ExploreStepNodes);
                paid++;
            }
            else Assert.Equal(ExploreVerdict.AtCap, v);
            // Stamped t → the radius is in the state of t + 1 (and was not in the state of t).
            Assert.Equal(expected, DiscoveryQuery.ExploredRadius(next, fog, Player, s));
            Assert.Equal(paid * fog.ExploreGrain, Sunk(next, grain, ReasonIds.Expedition));
            AssertMonotone(prev, next);
            // The whole disc of the radius is known.
            (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(next);
            var disc = new ulong[DiscoveryQuery.WordCount(l)];
            DiscoveryQuery.OrDisc(disc, l, wrap, DiscoveryQuery.NodeOfSettlement(next, s), expected);
            ulong[] stored = DiscoveryQuery.StoredExtent(next, Player, disc.Length);
            for (int i = 0; i < disc.Length; i++) Assert.Equal(disc[i], disc[i] & stored[i]);
            prev = next;
        }
        Assert.Equal(4, paid);   // 3 → 6 → 9 → 12 → 15, then the cap refuses
    }

    [Fact]
    public void Explore_StampedLater_AppearsLater()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg);
        SettlementId s = w.Settlements[0].Id;
        var log = new OrderLog();
        log.Append(Order(1, OrderKind.Explore, s));
        TurnExecutor exec = Exec(cfg, log, TestConfigs.DevWorldgen());
        WorldState w1 = exec.Step(w), w2 = exec.Step(w1);
        Assert.Equal(0, w1.Explorations.Count);
        Assert.Equal(new ExplorationRow(Player, s, cfg.Fog!.SeedRadiusNodes + cfg.Fog.ExploreStepNodes), w2.Explorations[0]);
    }

    [Fact]
    public void Explore_TheDiscWrapsAtTheCylinderSeam_AndIsClippedOnAFlatMap()
    {
        const int l = 64;
        var wrapped = new ulong[DiscoveryQuery.WordCount(l)];
        var flat = new ulong[wrapped.Length];
        int center = 10 * l + 0;   // x = 0, y = 10
        DiscoveryQuery.OrDisc(wrapped, l, true, center, 3);
        DiscoveryQuery.OrDisc(flat, l, false, center, 3);
        Assert.True(DiscoveryQuery.Has(wrapped, 10 * l + (l - 3)));   // three columns west, across the seam
        Assert.True(DiscoveryQuery.Has(wrapped, 10 * l + 3));
        Assert.False(DiscoveryQuery.Has(wrapped, 10 * l + (l - 4)));
        Assert.False(DiscoveryQuery.Has(flat, 10 * l + (l - 3)));
        Assert.True(DiscoveryQuery.Has(flat, 10 * l + 3));
        Assert.True(DiscoveryQuery.InDisc(l, true, center, 3, 10 * l + (l - 3)));
        Assert.False(DiscoveryQuery.InDisc(l, false, center, 3, 10 * l + (l - 3)));
        // Every node the disc sets is one InDisc admits, and vice versa (one geometry).
        for (int n = 0; n < l * l; n++) Assert.Equal(DiscoveryQuery.InDisc(l, true, center, 3, n), DiscoveryQuery.Has(wrapped, n));
        // North and south are hard edges on both.
        var top = new ulong[wrapped.Length];
        DiscoveryQuery.OrDisc(top, l, true, 0 * l + 5, 2);
        for (int n = 0; n < l * l; n++) if (DiscoveryQuery.Has(top, n)) Assert.True(n / l <= 2);
    }

    private static void AssertMonotone(IReadOnlyWorldState before, IReadOnlyWorldState after)
    {
        for (int i = 0; i < before.KnownExtents.Count; i++)
        {
            KnownExtentRow b = before.KnownExtents[i];
            ulong now = 0;
            for (int j = 0; j < after.KnownExtents.Count; j++)
                if (after.KnownExtents[j].Polity == b.Polity && after.KnownExtents[j].Word == b.Word) now = after.KnownExtents[j].Bits;
            Assert.Equal(b.Bits, b.Bits & now);
        }
    }

    // ================================================================== the fog (D5)

    [Fact]
    public void Fog_TheSeedIsKnown_OwnSettlementNodesAreVisible_TheRestOfTheSeedIsRemembered_FarLandIsUnknown()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg);
        PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
        Assert.False(k.AllKnown);
        SettlementId s = w.Settlements[0].Id;
        int node = DiscoveryQuery.NodeOfSettlement(w, s);
        int l = k.LatticeSize;
        Assert.True(k.Known(node) && k.Visible(node) && !k.Remembered(node));
        int ring = (node / l) * l + MapTopologyWrap(node % l + 3, l);   // seed radius 3: known; no formation 3 nodes east
        Assert.True(k.Known(ring));
        int far = ((node / l + l / 2) % l) * l + node % l;
        Assert.False(k.Known(far));
        Assert.False(k.Visible(far));
        // Every visible node is known.
        for (int n = 0; n < l * l; n++) if (k.Visible(n)) Assert.True(k.Known(n));
        // The switch off: all-known, exactly as before.
        PolityKnowledge off = PolityKnowledge.Of(w, Player, TestConfigs.PreDiscovery(cfg));
        Assert.True(off.AllKnown);
        Assert.True(off.Known(far) && off.Visible(far));
    }

    private static int MapTopologyWrap(int x, int l) => ((x % l) + l) % l;

    [Fact]
    public void Fog_ASettlementInRememberedLandIsUnknown_UntilItIsSighted_AndAnExpeditionSightsIt()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg, aiEmpires: 1);
        SettlementId mine = default, rival = default;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId id = w.Settlements[i].Id;
            if (EmpireQuery.ControlsSettlement(w, Player, id)) mine = id; else rival = id;
        }
        PolityKnowledge k0 = PolityKnowledge.Of(w, Player, cfg);
        Assert.False(k0.KnowsSettlement(rival));
        // Its land remembered (in the stored extent, out of sight) — the settlement is still unknown: presence needs a sighting.
        (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(w);
        int rivalNode = DiscoveryQuery.NodeOfSettlement(w, rival);
        WorldState remembered = w.Clone();
        remembered.KnownExtents.Add(new KnownExtentRow(Player, rivalNode >> 6, 1UL << (rivalNode & 63)));
        PolityKnowledge k1 = PolityKnowledge.Of(remembered, Player, cfg);
        Assert.True(k1.Known(rivalNode) && !k1.Visible(rivalNode));
        Assert.False(k1.KnowsSettlement(rival));
        // A sighting makes its presence known (internals stay hidden).
        WorldState sighted = remembered.Clone();
        sighted.SettlementSightings.Add(new SettlementSightingRow(Player, rival, 0));
        PolityKnowledge k2 = PolityKnowledge.Of(sighted, Player, cfg);
        Assert.True(k2.KnowsSettlement(rival));
        Assert.False(k2.ReadsInternals(rival));

        // An Explore whose disc covers the rival's node writes the sighting (stamped t → in t + 1).
        int mineNode = DiscoveryQuery.NodeOfSettlement(w, mine);
        int dx = Math.Abs(rivalNode % l - mineNode % l);
        if (wrap) dx = Math.Min(dx, l - dx);
        int dy = rivalNode / l - mineNode / l;
        int reach = (int)Math.Ceiling(Math.Sqrt((double)dx * dx + (double)dy * dy));
        SimConfig far = cfg with { Fog = cfg.Fog! with { ExploreStepNodes = reach, ExploreMaxRadiusNodes = Math.Max(cfg.Fog.ExploreMaxRadiusNodes, reach + cfg.Fog.SeedRadiusNodes) } };
        var log = new OrderLog();
        log.Append(Order(0, OrderKind.Explore, mine));
        WorldState w1 = Exec(far, log, TestConfigs.DevWorldgen() with { AiEmpires = 1 }).Step(w);
        Assert.True(DiscoveryQuery.HasSighting(w1, Player, rival));
        Assert.True(PolityKnowledge.Of(w1, Player, far).KnowsSettlement(rival));
        Assert.False(DiscoveryQuery.HasSighting(w, Player, rival));
    }

    [Fact]
    public void Founding_OnANodeTheIssuerDoesNotKnow_IsRefused_AndKnowingItRemovesThatRefusal()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg);
        SettlementSiting.FrontierSiting frontier = FoundingQuery.Frontier(w, cfg, TestConfigs.DevWorldgen())!;
        SettlementId source = w.Settlements[0].Id;
        PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
        int size = w.Terrain!.Size;
        int unknownLand = -1;
        for (int cell = 0; cell < size * size && unknownLand < 0; cell += 7)
            if (SettlementSiting.IsFrontierCandidate(frontier, w.Terrain, cell) && !k.KnowsCell(cell)) unknownLand = cell;   // a site the static rules admit
        Assert.True(unknownLand >= 0);
        Assert.Equal(FoundingVerdict.UnknownSite, FoundingQuery.Check(w, cfg, frontier, Player, source, unknownLand));
        // The switch off: the arm never fires.
        Assert.NotEqual(FoundingVerdict.UnknownSite, FoundingQuery.Check(w, TestConfigs.PreDiscovery(cfg), frontier, Player, source, unknownLand));
        // The node known: no longer refused for being unknown.
        WorldState known = w.Clone();
        int node = DiscoveryQuery.NodeOfCell(w, unknownLand);
        known.KnownExtents.Add(new KnownExtentRow(Player, node >> 6, 1UL << (node & 63)));
        Assert.NotEqual(FoundingVerdict.UnknownSite, FoundingQuery.Check(known, cfg, frontier, Player, source, unknownLand));
    }

    // ================================================================== the AI (D8)

    [Fact]
    public void Ai_Prospect_PicksTheMostVisibleEligibleGoods_TiesToTheLowerId_OnlyWhenAffordable()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg, aiEmpires: 1);
        var ai = new PolityId(2);
        Assert.Null(AiDiscoveryPolicy.Prospect(w, cfg, ai));   // no tools: the predicate refuses, the AI does not pay for a refusal
        var own = new List<SettlementId>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, ai, w.Settlements[i].Id)) own.Add(w.Settlements[i].Id);
        Assert.NotEmpty(own);
        GovernanceRigs.AddStock(w, own[0], Good(cfg, "tools"), 100);
        OrderRecord? order = AiDiscoveryPolicy.Prospect(w, cfg, ai);
        Assert.NotNull(order);
        // The argmin of (−knownEligible, id) over the admitted settlements, recomputed here.
        SettlementId best = own[0];
        foreach (SettlementId s in own)
        {
            int a = AiDiscoveryPolicy.KnownEligible(w, cfg, s), b = AiDiscoveryPolicy.KnownEligible(w, cfg, best);
            if (a > b || (a == b && s.Value < best.Value)) best = s;
        }
        Assert.Equal(OrderRecord.From(w.Clock.Turn, ai, OrderKind.Prospect, best.Value, 0.0), order);
    }

    [Fact]
    public void Ai_KeysAreTieDense_EqualCountsAndRadiiGoToTheLowestId()
    {
        // Two AI settlements with identical keys must resolve to the lower id: build a world where every settlement of the
        // AI has the same visible eligible count (all concealable goods removed) and the same explored radius.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg, aiEmpires: 1);
        var ai = new PolityId(2);
        WorldState flat = w.Clone();
        flat.Deposits.Clear();   // every settlement: 0 visible eligible goods
        var own = new List<int>();
        for (int i = 0; i < flat.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(flat, ai, flat.Settlements[i].Id)) own.Add(flat.Settlements[i].Id.Value);
        foreach (int s in own) GovernanceRigs.AddStock(flat, new SettlementId(s), Good(cfg, "tools"), 50);
        own.Sort();
        Assert.Equal(own[0], AiDiscoveryPolicy.Prospect(flat, cfg, ai)!.Value.TargetId);
        Assert.Equal(own[0], AiDiscoveryPolicy.Explore(flat, cfg, ai)!.Value.TargetId);
    }
}
