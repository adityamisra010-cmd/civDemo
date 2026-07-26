using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.2 — GOODS &amp; RECIPES (m3-spec §4): the D-031 registry, the
/// per-(settlement, good) stocks table, conservation registration for all 14
/// goods BY NAME, the FoodStore→grain migration, and the founding deposit
/// endowment. Loader-rejection tests pin the "recipe validation errors
/// actionable" criterion.
/// </summary>
public class GoodsTests
{
    private static GoodsConfig Goods()
    {
        using var g = Sim.Data.DataFiles.OpenGoods();
        return GoodsConfigLoader.Load(g);
    }

    [Fact]
    public void Roster_IsTheFourteen_OfD031_GrainTheNumeraire()
    {
        GoodsConfig cfg = Goods();
        string[] expected =
        [
            "grain", "livestock", "fish",
            "timber", "stone", "clay", "copper-ore", "tin-ore", "fiber", "hides",
            "bronze", "tools", "pottery", "cloth",
        ];
        Assert.Equal(expected, cfg.Goods.Select(g => g.Name).ToArray());
        Assert.Equal(14, cfg.Goods.Length);
        Assert.Equal(cfg.IdOf("grain"), cfg.GrainId);
    }

    [Fact]
    public void Auditor_CoversAllFourteenGoods_ByName()
    {
        // The T3.2 accept criterion verbatim: the auditor covers all 14 by
        // name. A hand-planted violation in EACH good's stock must surface in
        // the report under the good's NAME.
        GoodsConfig goods = Goods();
        foreach (GoodEntry g in goods.Goods)
        {
            var world = new WorldState();
            world.Settlements.Add(new SettlementRow(new SettlementId(0), 0, 0));
            // A phantom flow with no matching stock = 7 conjured units — the
            // Conserved type itself cannot be forged, so the imbalance is
            // planted on the flow side.
            world.LedgerFlows.Add(new LedgerFlowRow(
                ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                ReasonIds.InitialEndowment, TotalSourced: 7, TotalSunk: 0));
            Assert.False(ConservationAuditor.IsConserved(world, goods, out string report));
            Assert.Contains(g.Name, report);
        }
    }

    [Fact]
    public void Founding_LaysOutStocks_SettlementMajor_GoodsAscending_DepositsRolled()
    {
        SimConfig cfg = TestUtil.TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestUtil.TestConfigs.DevWorldgen(), cfg, 42);
        int n = world.Settlements.Count, k = cfg.Goods!.Goods.Length;
        Assert.Equal(n * k, world.GoodStocks.Count);
        for (int i = 0; i < world.GoodStocks.Count; i++)
        {
            Assert.Equal(i / k, world.GoodStocks[i].Settlement.Value);
            Assert.Equal(cfg.Goods.Goods[i % k].Id, world.GoodStocks[i].Good.Value);
        }

        // Deposits: one row per deposit-bearing good per settlement, same
        // order; abundances non-negative and NOT all equal across settlements
        // (the comparative-advantage precondition).
        int bearing = cfg.Goods.Goods.Count(g => g.DepositChannel is not null);
        Assert.Equal(n * bearing, world.Deposits.Count);
        foreach (GoodEntry g in cfg.Goods.Goods.Where(g => g.DepositChannel is not null))
        {
            var values = new List<double>();
            for (int i = 0; i < world.Deposits.Count; i++)
                if (world.Deposits[i].Good.Value == g.Id)
                {
                    Assert.True(world.Deposits[i].Abundance >= 0.0);
                    values.Add(world.Deposits[i].Abundance);
                }
            Assert.Equal(n, values.Count);
            Assert.True(values.Distinct().Count() > 1,
                $"{g.Name}: every settlement rolled the identical deposit {values[0]} — endowments do not differ");
        }
    }

    [Fact]
    public void FoodStoreMigration_GrainCarriesTheRole_ExactlyOnceThroughTheLedger()
    {
        // The migrated FoodStore semantics, end to end: found → run turns →
        // grain harvested and eaten via the SAME reasons as M2, every other
        // good untouched, conservation exact across ALL goods by the auditor.
        SimConfig cfg = TestUtil.TestConfigs.Sim();
        EraTable era;
        using (var s = Sim.Data.DataFiles.OpenEraPacing()) era = EraTableLoader.Load(s);
        TurnExecutor exec;
        using (var s = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(era, PipelineLoader.Load(s, Sim.Core.SystemCatalog.All(cfg)));
        WorldState world = WorldFounding.Found(TestUtil.TestConfigs.DevWorldgen(), cfg, 42);
        for (int t = 1; t <= 10; t++)
        {
            world = exec.Step(world);
            Assert.True(ConservationAuditor.IsConserved(world, cfg.Goods, out string report), report);
        }
        var grainQ = ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.GrainId));
        long harvested = 0, eaten = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow f = world.LedgerFlows[i];
            if (f.Quantity == grainQ && f.Reason == ReasonIds.Harvest) harvested = f.TotalSourced;
            if (f.Quantity == grainQ && f.Reason == ReasonIds.Eaten) eaten = f.TotalSunk;
            // No other good moved: production is T3.3; at T3.2 only grain flows.
            if (ConservedQuantityIds.IsGood(f.Quantity) && f.Quantity != grainQ)
                Assert.Fail($"good quantity {f.Quantity.Value} has flows before T3.3 exists");
        }
        Assert.True(harvested > 0 && eaten > 0, "grain never harvested/eaten — the migration lost the M2 loop");
        for (int i = 0; i < world.GoodStocks.Count; i++)
            if (world.GoodStocks[i].Good.Value != cfg.Goods.GrainId)
                Assert.Equal(0, world.GoodStocks[i].Amount.Value);
    }

    // --- loader rejection: actionable errors (the T3.2 accept criterion) ----

    [Theory]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true}],"recipes":[{"name":"x","inputs":[{"good":"iron","perOutput":1.0}],"laborPerOutput":0.1,"output":{"good":"grain","qty":1}}]}""",
        "input good 'iron' is not in the roster")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true}],"recipes":[{"name":"x","inputs":[],"laborPerOutput":0.1,"output":{"good":"grain","qty":1}}]}""",
        "no inputs")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true},{"id":2,"name":"clay","category":"raw","bulkPerUnit":1.0}],"recipes":[{"name":"x","inputs":[{"good":"clay","perOutput":1.0}],"laborPerOutput":0.1,"output":{"good":"clay","qty":1}}]}""",
        "consumes its own output")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true},{"id":2,"name":"clay","category":"raw","bulkPerUnit":1.0}],"recipes":[{"name":"x","inputs":[{"good":"clay","perOutput":1.0}],"laborPerOutput":0.1,"output":{"good":"grain","qty":1},"requires":"unknown_var > 1"}]}""",
        "requires")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0}],"recipes":[]}""",
        "numeraire")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true},{"id":1,"name":"clay","category":"raw","bulkPerUnit":1.0}],"recipes":[]}""",
        "strictly ascending")]
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"cheese","bulkPerUnit":1.0,"numeraire":true}],"recipes":[]}""",
        "category")]
    // T3.3 adversarial finding: two entries for one good made the Leontief cap
    // and the sink asymmetric (cap counted the coefficient once, the sink loop
    // charged it per ENTRY), so a purely data-level edit crashed the turn with a
    // Ledger overdraw. CLAUDE.md says tuning data is always allowed, so the
    // loader owes an actionable rejection instead of a mid-turn exception.
    [InlineData("""{"goods":[{"id":1,"name":"grain","category":"food","bulkPerUnit":1.0,"numeraire":true},{"id":2,"name":"clay","category":"raw","bulkPerUnit":1.0}],"recipes":[{"name":"x","inputs":[{"good":"clay","perOutput":2.0},{"good":"clay","perOutput":2.0}],"laborPerOutput":0.1,"output":{"good":"grain","qty":1}}]}""",
        "more than once")]
    public void Loader_RejectsBadConfigs_WithActionableMessages(string json, string fragment)
    {
        var ex = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(json));
        Assert.Contains(fragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- FsCheck: conservation exact across all goods ------------------------

    [Property(MaxTest = 40)]
    public Property RandomStockTransfers_ConserveEveryGood_Exactly()
    {
        // T3.2 accept criterion: "conservation exact across all goods under
        // the FsCheck suite." Random sequences of Ledger transfers and
        // clamped sinks across random (settlement, good) stocks — the audit
        // (all goods, by name) must hold EXACTLY after every sequence.
        GoodsConfig goods = Goods();
        Gen<(int G, int S1, int S2, int Amt, bool Sink)> opGen =
            Gen.Choose(0, goods.Goods.Length - 1).SelectMany(g =>
            Gen.Choose(0, 2).SelectMany(s1 =>
            Gen.Choose(0, 2).SelectMany(s2 =>
            Gen.Choose(0, 500).SelectMany(amt =>
            Gen.Choose(0, 1).Select(sink => (g, s1, s2, amt, sink == 1))))));
        var ops = opGen.ArrayOf(30);

        return Prop.ForAll(ops.ToArbitrary(), sequence =>
        {
            var world = new WorldState();
            var ledger = new Ledger(world.LedgerFlows);
            for (int s = 0; s < 3; s++)
            {
                world.Settlements.Add(new SettlementRow(new SettlementId(s), s, 0));
                foreach (GoodEntry g in goods.Goods)
                {
                    int row = world.GoodStocks.Add(new GoodStockRow(
                        new SettlementId(s), new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
                    ledger.Flow(ref world.GoodStocks.Ref(row).Amount,
                        ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                        ReasonIds.InitialEndowment, 1000, FlowDirection.Source,
                        OverdrawPolicy.Throw);
                }
            }
            foreach ((int g, int s1, int s2, int amt, bool sink) in sequence)
            {
                var good = new GoodId(goods.Goods[g].Id);
                int a = GoodStockIndex.IndexOf(world.GoodStocks, new SettlementId(s1), good);
                int b = GoodStockIndex.IndexOf(world.GoodStocks, new SettlementId(s2), good);
                if (sink)
                {
                    ledger.Flow(ref world.GoodStocks.Ref(a).Amount,
                        ConservedQuantityIds.OfGood(good), ReasonIds.Eaten,
                        amt, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
                }
                else if (a != b)
                {
                    ledger.Transfer(ref world.GoodStocks.Ref(a).Amount,
                        ref world.GoodStocks.Ref(b).Amount, amt,
                        OverdrawPolicy.ClampToAvailable);
                }
            }
            bool ok = ConservationAuditor.IsConserved(world, goods, out string report);
            return ok.Label(report);
        });
    }
}
