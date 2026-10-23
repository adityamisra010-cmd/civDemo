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

    [Fact]
    public void Fraction_IsTheSeedOffsetPlusTheGoldenStepPerSettlementId_ModuloOne()
    {
        foreach (ulong seed in new ulong[] { 1, 2, 7, 42 })
        {
            double u0 = DepositConcealment.Fraction(seed, 0);
            for (int s = 0; s < 64; s++)
            {
                double u = DepositConcealment.Fraction(seed, s);
                Assert.InRange(u, 0.0, 0.9999999999999999);
                double x = u0 + DepositConcealment.GoldenStep * s;
                Assert.Equal(x - Math.Floor(x) >= 1.0 ? 0.0 : x - Math.Floor(x), u);
            }
        }
    }

    /// <summary>ADR-044 §3 (the coordinator's ruling under D-062): the eligible rows are the ORE deposits (copper-ore,
    /// tin-ore) with abundance &gt; 0 at every settlement, and the realized concealed share over ALL of them — colonies
    /// included — is pinned exactly at turn 0 and at turn 300 of the AI world (aiEmpires 1, the AI's own orders), each in
    /// [20 %, 30 %]. MEASURED 2026-10-23.</summary>
    [Theory]
    [InlineData(1UL, 6, 24, 9, 38)]
    [InlineData(2UL, 7, 24, 10, 38)]
    [InlineData(42UL, 7, 24, 10, 38)]
    [InlineData(7UL, 6, 24, 10, 38)]
    public void RealizedConcealedShare_AtTurnZeroAndTurn300_OfTheAiWorld_IsPinnedWithin20And30Percent(
        ulong seed, int concealed0, int eligible0, int concealed300, int eligible300)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = WorldFounding.Found(wg with { AiEmpires = 1 }, cfg, seed);
        Assert.Equal((concealed0, eligible0), ConcealedShare(w, cfg));
        Assert.InRange(concealed0 / (double)eligible0, 0.20, 0.30);
        var log = new OrderLog();
        TurnExecutor exec = Exec(cfg, log, wg);
        for (int t = 0; t < 300; t++)
        {
            AiOrders.Append(log, w, cfg, wg);
            w = exec.Step(w);
        }
        Assert.Equal(300L, w.Clock.Turn);
        Assert.Equal((concealed300, eligible300), ConcealedShare(w, cfg));
        Assert.InRange(concealed300 / (double)eligible300, 0.20, 0.30);
    }

    private static (int Concealed, int Eligible) ConcealedShare(IReadOnlyWorldState w, SimConfig cfg)
    {
        int eligible = 0, concealed = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            eligible += DepositConcealment.EligibleCount(w, cfg, w.Settlements[i].Id);
            concealed += DepositConcealment.ConcealedGoods(w.Deposits, w.Seed, cfg, w.Settlements[i].Id).Length;
        }
        return (concealed, eligible);
    }

    // ================================================================== Prospect (D4)

    // ADR-046 (M6 review, 2026-10-23): a Prospect sinks a BILL — the survey's grain, timber and stone; the assay's less of
    // those plus bronze, behind ore_prospecting. Every test below reads the bill from sim.json, never a literal.
    private static (GoodId Good, long Qty)[] Survey(SimConfig cfg) => DiscoveryQuery.ProspectBill(cfg, ProspectMethod.Survey);
    private static (GoodId Good, long Qty)[] Assay(SimConfig cfg) => DiscoveryQuery.ProspectBill(cfg, ProspectMethod.Assay);

    private static OrderRecord AssayOrder(long turn, SettlementId s) =>
        OrderRecord.From(turn, Player, OrderKind.Prospect, s.Value, 1.0);

    /// <summary>Empties every bill good out of the whole empire's purse, then puts exactly <paramref name="bill"/> at
    /// <paramref name="s"/> (each quantity + <paramref name="extra"/>, which may be −1 for "one short" of one good).</summary>
    private static WorldState Purse(WorldState w0, SimConfig cfg, SettlementId s, (GoodId Good, long Qty)[] bill,
        GoodId? shortGood = null)
    {
        WorldState w = w0.Clone();
        var goods = new List<GoodId>();
        foreach ((GoodId g, _) in Survey(cfg)) goods.Add(g);
        foreach ((GoodId g, _) in Assay(cfg)) if (!goods.Contains(g)) goods.Add(g);
        for (int i = 0; i < w.Settlements.Count; i++)
            foreach (GoodId g in goods)
            {
                long have = Stock(w, w.Settlements[i].Id, g);
                if (have > 0) FoundingRigs.SinkGood(w, w.Settlements[i].Id, g, have);
            }
        foreach ((GoodId g, long q) in bill)
        {
            long amount = q - (shortGood is { } sg && sg == g ? 1 : 0);
            if (amount > 0) Endow(w, s, g, amount);
        }
        return w;
    }

    [Fact]
    public void Prospect_TheBills_AreGrainTimberStone_AndTheAssayAddsBronze_FromSimJson()
    {
        SimConfig cfg = TestConfigs.Sim();
        Assert.Equal(new[] { (Good(cfg, "grain"), 40L), (Good(cfg, "timber"), 20L), (Good(cfg, "stone"), 10L) }, Survey(cfg));
        Assert.Equal(new[] { (Good(cfg, "grain"), 20L), (Good(cfg, "timber"), 10L), (Good(cfg, "stone"), 5L), (Good(cfg, "bronze"), 2L) },
            Assay(cfg));
        Assert.Equal("ore_prospecting", cfg.Discovery!.AssayRequires);
        Assert.Equal("40 grain, 20 timber, 10 stone", DiscoveryQuery.BillText(cfg, Survey(cfg)));
    }

    [Fact]
    public void Prospect_TurnExact_EveryGoodSunkExactly_AndTheConcealedDepositProducesOnlyAfterTheSurvey()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, GoodId hidden) = ConcealedAtPlayer(w0, cfg);
        foreach ((GoodId g, long q) in Survey(cfg)) if (g.Value != cfg.Goods!.GrainId) Endow(w0, s, g, q);   // timber, stone at hand
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
        Assert.Equal(1, w1.SettlementSurveys.Count);
        Assert.Equal(new SettlementSurveyRow(Player, s, 0), w1.SettlementSurveys[0]);
        Assert.Equal(0, l1.SettlementSurveys.Count);
        foreach ((GoodId g, long q) in Survey(cfg))
        {
            Assert.Equal(q, Sunk(w1, g, ReasonIds.Prospecting));   // each good of the bill, sunk exactly once
            Assert.Equal(0, Sunk(l1, g, ReasonIds.Prospecting));
        }
        // CONSERVATION: every account of the step reconciles, the discovery sinks named per good.
        TurnRecord rec = Observer.Observe(w0, w1, cfg, []).Turn;
        Assert.True(rec.Grain.Reconciles);
        Assert.Equal(40, rec.Grain.Expedition);
        foreach (GoodAccount a in rec.Goods)
        {
            Assert.True(a.Reconciles, a.Name + " does not reconcile");
            long expected = 0;
            foreach ((GoodId g, long q) in Survey(cfg)) if (g.Value == a.Good) expected = q;
            Assert.Equal(expected, a.Discovery);
        }
        Assert.Equal(0L, w1.GoodStocks[GoodStockIndex.IndexOf(w1.GoodStocks, s, hidden)].LastProducedUnits);
        Assert.False(DepositConcealment.IsHiddenFrom(w1, cfg, Player, s, hidden));

        WorldState w2 = exec.Step(w1), l2 = late.Step(l1);
        Assert.True(w2.GoodStocks[GoodStockIndex.IndexOf(w2.GoodStocks, s, hidden)].LastProducedUnits > 0);
        Assert.Equal(0L, l2.GoodStocks[GoodStockIndex.IndexOf(l2.GoodStocks, s, hidden)].LastProducedUnits);
        Assert.Equal(new SettlementSurveyRow(Player, s, 1), l2.SettlementSurveys[0]);
    }

    [Fact]
    public void Prospect_OneGoodShort_IsRefusedAndChargesNothing_ForEachGoodOfTheBill()
    {
        // DiscoverySystem alone: no harvest or meal moves a good, so the purse the step draws from is the purse the
        // preview read. For EACH good of the bill, one unit short (the others in full) — refused, nothing of ANY good paid,
        // the state the orderless step's; exactly the bill — paid, the purse emptied of every good to the unit.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, _) = ConcealedAtPlayer(w0, cfg);
        var log = new OrderLog();
        log.Append(Order(0, OrderKind.Prospect, s));
        foreach ((GoodId shortGood, _) in Survey(cfg))
        {
            WorldState w = Purse(w0, cfg, s, Survey(cfg), shortGood);
            Assert.Equal(ProspectVerdict.Unaffordable, DiscoveryQuery.CheckProspect(w, cfg, Player, s));
            Assert.Equal("1 " + cfg.Goods!.ById(shortGood.Value).Name + " short",
                DiscoveryQuery.Shortfall(w, cfg, s, Survey(cfg), w.GoodStocks, w.ConstructionHolds));
            WorldState with = DiscoveryOnly(cfg, log).Step(w), without = DiscoveryOnly(cfg, new OrderLog()).Step(w);
            Assert.False(DepositConcealment.HasSurveyed(with, Player, s));
            foreach ((GoodId g, _) in Survey(cfg)) Assert.Equal(0, Sunk(with, g, ReasonIds.Prospecting));
            Assert.Equal(WorldHash.ComputeHex(without), WorldHash.ComputeHex(with));
            Assert.Equal(new[] { (OrderKind.Prospect, s, DiscoveryQuery.BillText(cfg, Survey(cfg))) },
                DiscoveryQuery.RefusedAtDelivery(w, with, cfg, new List<OrderRecord> { Order(0, OrderKind.Prospect, s) }, Player));
        }
        WorldState exact = Purse(w0, cfg, s, Survey(cfg));
        Assert.Equal(ProspectVerdict.Ok, DiscoveryQuery.CheckProspect(exact, cfg, Player, s));
        WorldState paid = DiscoveryOnly(cfg, log).Step(exact);
        Assert.True(DepositConcealment.HasSurveyed(paid, Player, s));
        foreach ((GoodId g, long q) in Survey(cfg))
        {
            Assert.Equal(0, Stock(paid, s, g));
            Assert.Equal(q, Sunk(paid, g, ReasonIds.Prospecting));
        }
        Assert.Empty(DiscoveryQuery.RefusedAtDelivery(exact, paid, cfg, new List<OrderRecord> { Order(0, OrderKind.Prospect, s) }, Player));
    }

    private static TurnExecutor DiscoveryOnly(SimConfig cfg, OrderLog log) => new(GovernanceRigs.FlatEra(), [SystemCatalog.Discovery(cfg)], log);

    private static void Endow(WorldState w, SettlementId s, GoodId g, long amount)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Settlement != s || w.GoodStocks[i].Good != g) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(g),
                ReasonIds.InitialEndowment, amount, FlowDirection.Source, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException("no stock row");
    }

    [Fact]
    public void Prospect_ASecondProspect_SameTurnAnyMethodOrLater_IsRefused_NoRepeatCharge()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, _) = ConcealedAtPlayer(w0, cfg);
        Sim.Core.Systems.Research.ResearchContent research = cfg.Research!;
        w0.ResearchCompleted.Add(new ResearchCompletedRow(Player, research.Nodes[research.IndexOfId("ore_prospecting")].Key));
        WorldState w = Purse(w0, cfg, s, Survey(cfg));
        foreach ((GoodId g, long q) in Assay(cfg)) Endow(w, s, g, q);   // both bills affordable
        var log = new OrderLog();
        log.Append(Order(0, OrderKind.Prospect, s));
        log.Append(Order(0, OrderKind.Prospect, s));   // a duplicate survey in the same batch
        log.Append(AssayOrder(0, s));                  // and an assay of the same settlement
        TurnExecutor exec = DiscoveryOnly(cfg, log);
        WorldState w1 = exec.Step(w);
        foreach ((GoodId g, long q) in Survey(cfg)) Assert.Equal(q, Sunk(w1, g, ReasonIds.Prospecting));   // the first order only
        Assert.Equal(0, Sunk(w1, Good(cfg, "bronze"), ReasonIds.Prospecting));
        Assert.Equal(ProspectVerdict.AlreadySurveyed, DiscoveryQuery.CheckProspect(w1, cfg, Player, s));
        log.Append(Order(1, OrderKind.Prospect, s));   // a later turn
        WorldState w2 = exec.Step(w1);
        WorldState control = DiscoveryOnly(cfg, new OrderLog()).Step(w1);
        Assert.Equal(WorldHash.ComputeHex(control), WorldHash.ComputeHex(w2));
    }

    [Fact]
    public void Assay_IsLockedUntilOreProspecting_ThenSpendsBronze_AndTheSurveyNeverWaitsOnIt()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w0 = Dev(cfg);
        (SettlementId s, _) = ConcealedAtPlayer(w0, cfg);
        GoodId bronze = Good(cfg, "bronze");
        var assayLog = new OrderLog();
        assayLog.Append(AssayOrder(0, s));

        // 1. Locked: the assay is refused (nothing paid) even with its whole bill in store.
        WorldState locked = Purse(w0, cfg, s, Assay(cfg));
        Assert.False(DiscoveryQuery.AssayUnlocked(locked, cfg, Player));
        Assert.Equal(ProspectVerdict.NotUnlocked, DiscoveryQuery.CheckProspect(locked, cfg, Player, s, ProspectMethod.Assay));
        Assert.Equal(WorldHash.ComputeHex(DiscoveryOnly(cfg, new OrderLog()).Step(locked)),
            WorldHash.ComputeHex(DiscoveryOnly(cfg, assayLog).Step(locked)));

        // 2. Unlocked, no bronze: the assay is unaffordable, the SURVEY stays open — no deadlock on the metal.
        Sim.Core.Systems.Research.ResearchContent research = cfg.Research!;
        WorldState known = Purse(w0, cfg, s, Survey(cfg));
        known.ResearchCompleted.Add(new ResearchCompletedRow(Player, research.Nodes[research.IndexOfId("ore_prospecting")].Key));
        Assert.True(DiscoveryQuery.AssayUnlocked(known, cfg, Player));
        Assert.Equal(ProspectVerdict.Unaffordable, DiscoveryQuery.CheckProspect(known, cfg, Player, s, ProspectMethod.Assay));
        Assert.Equal(ProspectVerdict.Ok, DiscoveryQuery.CheckProspect(known, cfg, Player, s, ProspectMethod.Survey));

        // 3. Unlocked with the assay bill: paid good by good — bronze is a real sink.
        WorldState ready = Purse(w0, cfg, s, Assay(cfg));
        ready.ResearchCompleted.Add(new ResearchCompletedRow(Player, research.Nodes[research.IndexOfId("ore_prospecting")].Key));
        Assert.Equal(ProspectVerdict.Ok, DiscoveryQuery.CheckProspect(ready, cfg, Player, s, ProspectMethod.Assay));
        WorldState paid = DiscoveryOnly(cfg, assayLog).Step(ready);
        Assert.True(DepositConcealment.HasSurveyed(paid, Player, s));
        foreach ((GoodId g, long q) in Assay(cfg)) Assert.Equal(q, Sunk(paid, g, ReasonIds.Prospecting));
        Assert.Equal(2, Sunk(paid, bronze, ReasonIds.Prospecting));
        TurnRecord rec = Observer.Observe(ready, paid, cfg, []).Turn;
        foreach (GoodAccount a in rec.Goods) Assert.True(a.Reconciles, a.Name + " does not reconcile");
    }

    [Fact]
    public void TheLoader_RefusesABasicSurveyThatCostsWhatOnlyASurveyCanFind()
    {
        SimConfig cfg = TestConfigs.Sim();
        foreach (string good in new[] { "copper-ore", "tin-ore", "bronze", "tools" })   // concealable, or made from one
        {
            DiscoveryConfig d = cfg.Discovery! with { ProspectCost = [.. cfg.Discovery!.ProspectCost, new DiscoveryCostItem(good, 1)] };
            var ex = Assert.Throws<SimConfigException>(() => SimConfigLoader.ValidateDiscoveryAgainstContent(cfg with { Discovery = d }));
            Assert.Contains(good, ex.Message);
        }
        // the same metal in the ASSAY bill is the intended design, and stone/timber/grain in the survey are fine
        SimConfigLoader.ValidateDiscoveryAgainstContent(cfg);
        Assert.True(SimConfigLoader.ConcealedLine(cfg.Goods!, cfg.Discovery!.ConcealableGoods)[Good(cfg, "tools").Value]);
        Assert.False(SimConfigLoader.ConcealedLine(cfg.Goods!, cfg.Discovery!.ConcealableGoods)[Good(cfg, "timber").Value]);
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
        var own = new List<SettlementId>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, ai, w.Settlements[i].Id)) own.Add(w.Settlements[i].Id);
        Assert.NotEmpty(own);
        // An empty purse: the predicate refuses, the AI does not pay for a refusal.
        WorldState poor = w.Clone();
        GoodId grain = new(cfg.Goods!.GrainId);
        foreach (SettlementId s in own) { long have = Stock(poor, s, grain); if (have > 0) FoundingRigs.SinkGood(poor, s, grain, have); }
        Assert.Null(AiDiscoveryPolicy.Prospect(poor, cfg, ai));
        Assert.Null(AiDiscoveryPolicy.Prospect(w, cfg, ai));   // ADR-046: the founding store holds grain but no stone yet
        foreach (SettlementId s in own) foreach ((GoodId g, long q) in Survey(cfg)) if (g != grain) Endow(w, s, g, q);
        OrderRecord? order = AiDiscoveryPolicy.Prospect(w, cfg, ai);   // the survey bill covered
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
        own.Sort();
        foreach (int s in own)   // the survey bill in every AI settlement (ADR-046), so every key ties
            foreach ((GoodId g, long q) in Survey(cfg)) if (g.Value != cfg.Goods!.GrainId) Endow(flat, new SettlementId(s), g, q);
        Assert.Equal(own[0], AiDiscoveryPolicy.Prospect(flat, cfg, ai)!.Value.TargetId);
        Assert.Equal(own[0], AiDiscoveryPolicy.Explore(flat, cfg, ai)!.Value.TargetId);
    }

    [Fact]
    public void Ai_Prospect_TakesTheAssayWhenOpenAndAffordable_ElseTheSurvey()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Dev(cfg, aiEmpires: 1);
        var ai = new PolityId(2);
        var own = new List<SettlementId>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, ai, w.Settlements[i].Id)) own.Add(w.Settlements[i].Id);
        GoodId grain = new(cfg.Goods!.GrainId);
        foreach (SettlementId s in own) foreach ((GoodId g, long q) in Survey(cfg)) if (g != grain) Endow(w, s, g, q);
        Assert.Equal(0.0, AiDiscoveryPolicy.Prospect(w, cfg, ai)!.Value.Amount);            // locked: the survey
        Sim.Core.Systems.Research.ResearchContent research = cfg.Research!;
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, research.Nodes[research.IndexOfId("ore_prospecting")].Key));
        Assert.Equal(0.0, AiDiscoveryPolicy.Prospect(w, cfg, ai)!.Value.Amount);            // open, no bronze: still the survey
        foreach (SettlementId s in own) Endow(w, s, Good(cfg, "bronze"), 2);
        OrderRecord assay = AiDiscoveryPolicy.Prospect(w, cfg, ai)!.Value;
        Assert.Equal(1.0, assay.Amount);                                                      // open and affordable: the assay
        Assert.Equal(ProspectVerdict.Ok, DiscoveryQuery.CheckProspect(w, cfg, ai, new SettlementId(assay.TargetId), ProspectMethod.Assay));
    }
}
