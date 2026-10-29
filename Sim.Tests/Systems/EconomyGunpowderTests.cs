using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.EconomyRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-056 §9 — EC-3 GUNPOWDER (D-065 R2: a separately reviewed M7 packet): saltpetre and sulphur deposits, black powder
/// milling and high explosives, gated on existing nodes (saltpetre_refining, dynamite — TC-1). The same acceptance
/// shape as EC-1: exact roster, exact switch, knowledge-only gates, per-execution coefficients, untouched concealment
/// pools of the packets before it, exact conservation over a run that exercises every good.
/// </summary>
public class EconomyGunpowderTests
{
    private static readonly string[] Ec3Goods = ["saltpetre", "sulphur", "gunpowder", "explosives"];

    [Fact]
    public void Ec3_IsShippedOn_WithTheExactRosterAndRecipeBook()
    {
        GoodsConfig goods = TestConfigs.Sim().Goods!;
        Assert.True(goods.IsPacketEnabled("ec3"));
        (int Id, string Name, string Category, double Bulk, string? Channel, double Spread, string? Gate)[] expected =
        [
            (20, "saltpetre", "raw", 3.0, "elevation", 0.9, "saltpetre_refining"),
            (21, "sulphur", "raw", 4.0, "elevation", 0.95, "saltpetre_refining"),
            (22, "gunpowder", "processed", 1.5, null, 0.0, null),
            (23, "explosives", "processed", 1.5, null, 0.0, null),
        ];
        foreach (var e in expected)
        {
            GoodEntry g = goods.ById(e.Id);
            Assert.Equal((e.Name, e.Category, e.Bulk, e.Channel, e.Spread, e.Gate, "ec3"),
                (g.Name, g.Category, g.BulkPerUnit, g.DepositChannel, g.DepositSpread, g.Knowledge, g.Packet));
        }
        RecipeEntry powder = Array.Find(goods.Recipes, r => r.Name == "gunpowder-milling")!;
        Assert.Equal(("saltpetre_refining", "gunpowder", 4, 0.3), (powder.Knowledge, powder.Output.Good, powder.Output.Qty, powder.LaborPerOutput));
        Assert.Equal([("saltpetre", 3.0), ("sulphur", 0.5), ("timber", 2.0)], Array.ConvertAll(powder.Inputs, i => (i.Good, i.PerOutput)));
        RecipeEntry blast = Array.Find(goods.Recipes, r => r.Name == "explosives-making")!;
        Assert.Equal(("dynamite", "explosives", 1, 0.5), (blast.Knowledge, blast.Output.Good, blast.Output.Qty, blast.LaborPerOutput));
        Assert.Equal([("saltpetre", 2.0), ("sulphur", 2.0)], Array.ConvertAll(blast.Inputs, i => (i.Good, i.PerOutput)));
        Assert.Null(powder.Entity);
        Assert.Null(blast.Entity);
    }

    [Fact]
    public void SwitchOff_RemovesExactlyThePacket_AndEachPacketSwitchesAlone()
    {
        SimConfig on = TestConfigs.Sim();
        GoodsConfig off = TestConfigs.PreEc3(on).Goods!;
        Assert.Equal(Ec3Goods, off.DisabledGoodNames);
        foreach (string g in Ec3Goods) Assert.Equal(-1, off.IdOf(g));
        Assert.DoesNotContain(off.Recipes, r => r.Packet == "ec3");
        // D-065 R2: the three packets are independent — each one off alone validates, and all three off is the base.
        Assert.Equal(18 + 1, TestConfigs.PreEc3(on).Goods!.Goods.Length);
        Assert.Equal(23 - 4, TestConfigs.PreEc1(on).Goods!.Goods.Length);
        Assert.Equal(23 - 1, TestConfigs.PreEc2(on).Goods!.Goods.Length);
        Assert.Equal(14, TestConfigs.PreEconomyTrack(on).Goods!.Goods.Length);
        Assert.Equal(4, TestConfigs.PreEconomyTrack(on).Goods!.Recipes.Length);
    }

    [Fact]
    public void Milling_RunsOnlyWithItsKnowledge_AndConsumesPerExecutionExactly()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        var craft = new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0);
        (string, long)[] stocks = [("saltpetre", 1_000_000), ("sulphur", 1_000_000), ("timber", 1_000_000)];

        WorldState before = exec.Step(World(cfg, 100, craft, stocks: stocks));
        Assert.Equal(0, Stock(before, cfg, "gunpowder"));
        Assert.Equal(1_000_000, Stock(before, cfg, "saltpetre"));

        WorldState after = exec.Step(Know(World(cfg, 100, craft, stocks: stocks), cfg, Ruler, "saltpetre_refining"));
        long powder = Stock(after, cfg, "gunpowder");
        Assert.True(powder > 0);
        Assert.Equal(0, Stock(after, cfg, "explosives"));   // dynamite unknown
        double x = powder + Row(after, cfg, "gunpowder", S0).ProduceRemainder;
        Assert.Equal((long)Math.Floor(x * (3.0 / 4)), Flow(after, cfg, "saltpetre", ReasonIds.InputsConsumed, sunk: true));
        Assert.Equal((long)Math.Floor(x * (0.5 / 4)), Flow(after, cfg, "sulphur", ReasonIds.InputsConsumed, sunk: true));
        Assert.Equal((long)Math.Floor(x * (2.0 / 4)), Flow(after, cfg, "timber", ReasonIds.InputsConsumed, sunk: true));
    }

    [Fact]
    public void Explosives_NeedDynamite_AndAreNotMadeFromGunpowder()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState next = exec.Step(Know(World(cfg, 100, new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("saltpetre", 1_000_000), ("sulphur", 1_000_000), ("gunpowder", 1_000)]), cfg, Ruler, "dynamite"));
        long blast = Stock(next, cfg, "explosives");
        Assert.True(blast > 0);
        double x = blast + Row(next, cfg, "explosives", S0).ProduceRemainder;
        Assert.True(Flow(next, cfg, "saltpetre", ReasonIds.InputsConsumed, sunk: true) >= (long)Math.Floor(x * 2.0));
        Assert.Equal(0, Flow(next, cfg, "gunpowder", ReasonIds.InputsConsumed, sunk: true));
    }

    [Fact]
    public void Saltpetre_IsNotWorkedWithoutItsKnowledge()
    {
        SimConfig on = DiscoveryQuery.WithoutDiscovery(TestConfigs.Sim());   // the subject is the knowledge gate, not concealment
        SimConfig off = TestConfigs.PreEc3(on);
        var dig = new SectorAllocationRow(S0, 0.0, 0.0, 1.0, 0.0, 0.0);
        (string, double)[] deposits = [("stone", 1.0), ("saltpetre", 1.0), ("sulphur", 1.0)];
        WorldState unknown = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)]).Step(World(on, 100, dig, deposits: deposits));
        WorldState twin = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(off)]).Step(World(off, 100, dig, deposits: deposits));
        Assert.Equal(0, Stock(unknown, on, "saltpetre") + Stock(unknown, on, "sulphur"));
        Assert.Equal(Stock(twin, off, "stone"), Stock(unknown, on, "stone"));
        WorldState known = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)])
            .Step(Know(World(on, 100, dig, deposits: deposits), on, Ruler, "saltpetre_refining"));
        Assert.True(Stock(known, on, "saltpetre") > 0);
        Assert.Equal(Stock(known, on, "saltpetre"), Stock(known, on, "sulphur"));   // equal abundance, equal split
    }

    [Theory]
    [InlineData(1ul)]
    [InlineData(2ul)]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void Founding_TheEarlierPoolsAreUntouchedByThePacket(ulong seed)
    {
        SimConfig on = TestConfigs.Sim();
        SimConfig off = TestConfigs.PreEc3(on);
        WorldState a = WorldFounding.Found(TestConfigs.Worldgen(), on, seed);
        WorldState b = WorldFounding.Found(TestConfigs.Worldgen(), off, seed);
        for (int s = 0; s < a.Settlements.Count; s++)
        {
            SettlementId id = a.Settlements[s].Id;
            int[] ca = DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, id);
            int[] cb = DepositConcealment.ConcealedGoods(b.Deposits, b.Seed, off, id);
            Assert.Equal(cb, Array.FindAll(ca, g => g < 20));
        }
    }

    [Fact]
    public void Ec3Goods_AreConservedExactly_EveryTurn_OverARunThatMakesAllFour()
    {
        SimConfig cfg = DiscoveryQuery.WithoutDiscovery(TestConfigs.Sim());
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        Know(w, cfg, null, "saltpetre_refining", "dynamite");
        Allocate(w, 40.0, 10.0, 25.0, 15.0, 10.0);
        // Saltpetre and sulphur seldom lie at one settlement (spreads 0.9 and 0.95), and 30 turns are too few for the
        // explosives recipe to meet both unless they are held — and the milling recipe, earlier in the book, draws the
        // shared saltpetre first (recipe order is input priority, T3.3): every settlement is endowed with 5,000 of each,
        // through the Ledger (InitialEndowment), so the run exercises every recipe of the packet and the books still close.
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            int g = w.GoodStocks[i].Good.Value;
            if (g != cfg.Goods!.IdOf("saltpetre") && g != cfg.Goods!.IdOf("sulphur")) continue;
            ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(new GoodId(g)), ReasonIds.InitialEndowment,
                5_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        TurnExecutor exec = Canonical(cfg);
        var produced = new long[24];
        for (int t = 0; t < 30; t++)
        {
            WorldState next = exec.Step(w);
            TurnRecord rec = Observer.Observe(w, next, cfg, []).Turn;
            foreach (StockRecord st in rec.Stocks) Assert.True(st.Reconciles, $"turn {rec.Turn}: {st.Name} discrepancy {st.Discrepancy}");
            foreach (GoodAccount g in rec.Goods)
            {
                Assert.True(g.Reconciles, $"turn {rec.Turn}: {g.Name} discrepancy {g.Discrepancy}");
                if (g.Good is >= 20 and <= 23) produced[g.Good] += g.Produced;
            }
            w = next;
        }
        for (int g = 20; g <= 23; g++) Assert.True(produced[g] > 0, $"good {g} never produced in 30 turns");
    }
}
