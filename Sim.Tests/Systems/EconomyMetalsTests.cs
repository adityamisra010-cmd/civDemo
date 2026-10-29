using System.Text.Json.Nodes;
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
/// ADR-056 §4 — EC-1 METALS (D-065 R2: a separately reviewed M7 packet). The packet's acceptance, each with teeth:
/// the roster and recipe book exactly as shipped; the switch (goods.json packets[ec1].enabled) removes exactly the
/// packet; the knowledge gates (existing node ids, TC-1) gate WORKING the ores and RUNNING the recipes, and are met only
/// by knowledge; the per-execution coefficients hold exactly; the base concealment pool is untouched by the packet's
/// ores; and every unit of the four goods is conserved exactly through the Ledger over a run that exercises them.
/// </summary>
public class EconomyMetalsTests
{
    private static readonly string[] Ec1Goods = ["iron-ore", "coal", "iron", "steel"];

    [Fact]
    public void Ec1_IsShippedOn_WithTheExactRosterAndRecipeBook()
    {
        GoodsConfig goods = TestConfigs.Sim().Goods!;
        Assert.True(goods.IsPacketEnabled("ec1"));
        (int Id, string Name, string Category, double Bulk, string? Channel, double Spread, string? Gate)[] expected =
        [
            (15, "iron-ore", "raw", 8.0, "elevation", 0.7, "iron_bloomery"),
            (16, "coal", "raw", 7.0, "elevation", 0.9, "coke"),
            (17, "iron", "processed", 2.0, null, 0.0, null),
            (18, "steel", "processed", 2.0, null, 0.0, null),
        ];
        foreach (var e in expected)
        {
            GoodEntry g = goods.ById(e.Id);
            Assert.Equal(e.Name, g.Name);
            Assert.Equal(e.Category, g.Category);
            Assert.Equal(e.Bulk, g.BulkPerUnit);
            Assert.Equal(e.Channel, g.DepositChannel);
            Assert.Equal(e.Spread, g.DepositSpread);
            Assert.Equal(e.Gate, g.Knowledge);
            Assert.Equal("ec1", g.Packet);
        }
        (string Name, string Gate, (string, double)[] Inputs, string Out, int Qty, double Labour)[] recipes =
        [
            ("iron-smelting", "iron_bloomery", [("iron-ore", 3.0), ("timber", 4.0)], "iron", 1, 0.4),
            ("coke-smelting", "coke", [("iron-ore", 3.0), ("coal", 2.0)], "iron", 2, 0.3),
            ("steel-cementation", "steel_carburized", [("iron", 2.0), ("timber", 2.0)], "steel", 1, 0.5),
            ("steel-converting", "bessemer", [("iron", 5.0), ("coal", 2.0)], "steel", 4, 0.2),
            ("iron-toolmaking", "iron_farm_tools", [("iron", 1.0), ("timber", 1.0)], "tools", 2, 0.2),
        ];
        foreach (var r in recipes)
        {
            RecipeEntry entry = Array.Find(goods.Recipes, x => x.Name == r.Name)!;
            Assert.NotNull(entry);
            Assert.Equal("ec1", entry.Packet);
            Assert.Equal(r.Gate, entry.Knowledge);
            Assert.Null(entry.Entity);   // TC-1: no research.json entity is added
            Assert.Equal(r.Inputs.Length, entry.Inputs.Length);
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                Assert.Equal(r.Inputs[i].Item1, entry.Inputs[i].Good);
                Assert.Equal(r.Inputs[i].Item2, entry.Inputs[i].PerOutput);
            }
            Assert.Equal(r.Out, entry.Output.Good);
            Assert.Equal(r.Qty, entry.Output.Qty);
            Assert.Equal(r.Labour, entry.LaborPerOutput);
            Assert.Equal("artisan_share > 0.05", entry.Requires);
        }
    }

    [Fact]
    public void SwitchOff_RemovesExactlyThePacket_AndRestoresTheBaseRoster()
    {
        GoodsConfig on = TestConfigs.Sim().Goods!;
        GoodsConfig off = TestConfigs.PreEc1(TestConfigs.Sim()).Goods!;
        Assert.False(off.IsPacketEnabled("ec1"));
        Assert.Equal(Ec1Goods, off.DisabledGoodNames);
        Assert.Equal(on.Goods.Length - Ec1Goods.Length, off.Goods.Length);
        for (int i = 0; i < 14; i++)
        {
            Assert.Null(off.Goods[i].Packet);
            Assert.Equal(on.Goods[i], off.Goods[i]);   // the base roster, unchanged and in order
        }
        Assert.DoesNotContain(off.Goods, g => g.Packet == "ec1");
        Assert.DoesNotContain(off.Recipes, r => r.Packet == "ec1");
        Assert.Equal(["pottery-firing", "weaving", "bronze-casting", "toolmaking"],
            Array.ConvertAll(Array.FindAll(off.Recipes, r => r.Packet is null), r => r.Name));
        foreach (string g in Ec1Goods) Assert.Equal(-1, off.IdOf(g));
    }

    [Fact]
    public void Loader_RefusesAnUndeclaredPacket_AndAnEnabledRecipeNeedingADisabledGood()
    {
        JsonObject doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        doc["goods"]!.AsArray()[14]!["packet"] = "ec9";
        var e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("'ec9' is not declared", e.Message);

        doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        doc["packets"]!.AsArray()[0]!["enabled"] = false;
        foreach (JsonNode? r in doc["recipes"]!.AsArray())
            if ((string?)r!["name"] == "iron-toolmaking") r.AsObject().Remove("packet");   // stranded in the base book
        e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("iron-toolmaking", e.Message);
        Assert.Contains("'iron' is not in the roster", e.Message);
    }

    [Fact]
    public void FourFileLoad_RefusesAKnowledgeGateNamingNoNode()
    {
        JsonObject doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        foreach (JsonNode? r in doc["recipes"]!.AsArray())
            if ((string?)r!["name"] == "iron-smelting") r["knowledge"] = "iron_bloomery AND no_such_node";
        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(doc.ToJsonString()));
        using var research = Sim.Data.DataFiles.OpenResearch();
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(sim, needs, goods, research));
        Assert.Contains("iron-smelting", e.Message);
        Assert.Contains("no_such_node", e.Message);
    }

    // --- the knowledge gates -------------------------------------------------------------------------------------

    [Fact]
    public void Smelting_RunsOnlyWithItsKnowledge_AndConsumesPerExecutionExactly()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        var craft = new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0);
        (string, long)[] stocks = [("iron-ore", 1_000_000), ("timber", 1_000_000)];

        // Without iron_bloomery: no iron, and not one unit of ore moves.
        WorldState before = exec.Step(World(cfg, 100, craft, stocks: stocks));
        Assert.Equal(0, Stock(before, cfg, "iron"));
        Assert.Equal(1_000_000, Stock(before, cfg, "iron-ore"));

        // With it: iron is made, and the ore and timber sinks are the PER-EXECUTION coefficients (3 ore, 4 timber per
        // unit) applied to the exact output x = produced + banked remainder (first step: no earlier bank).
        WorldState after = exec.Step(Know(World(cfg, 100, craft, stocks: stocks), cfg, Ruler, "iron_bloomery"));
        long iron = Stock(after, cfg, "iron");
        Assert.True(iron > 0, "iron_bloomery known, ore and timber held — no iron is vacuous");
        double x = iron + Row(after, cfg, "iron", S0).ProduceRemainder;
        Assert.Equal((long)Math.Floor(x * 3.0), Flow(after, cfg, "iron-ore", ReasonIds.InputsConsumed, sunk: true));
        Assert.Equal((long)Math.Floor(x * 4.0), Flow(after, cfg, "timber", ReasonIds.InputsConsumed, sunk: true));
        Assert.Equal(iron, Flow(after, cfg, "iron", ReasonIds.Produced, sunk: false));
        Assert.Equal(1_000_000 - (long)Math.Floor(x * 3.0), Stock(after, cfg, "iron-ore"));
    }

    [Fact]
    public void IronToolmaking_IsASubstitution_TwoToolsPerIron_LikeBronze()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState next = exec.Step(Know(World(cfg, 100, new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("iron", 1_000_000), ("timber", 1_000_000)]), cfg, Ruler, "iron_farm_tools"));
        long tools = Stock(next, cfg, "tools");
        Assert.True(tools > 0);
        double x = tools + Row(next, cfg, "tools", S0).ProduceRemainder;
        Assert.Equal((long)Math.Floor(x * 0.5), Flow(next, cfg, "iron", ReasonIds.InputsConsumed, sunk: true));
        Assert.Equal(2.0, Array.Find(cfg.Goods!.Recipes, r => r.Name == "iron-toolmaking")!.Output.Qty
            / Array.Find(cfg.Goods!.Recipes, r => r.Name == "iron-toolmaking")!.Inputs[0].PerOutput);
        Assert.Equal(2.0, Array.Find(cfg.Goods!.Recipes, r => r.Name == "toolmaking")!.Output.Qty
            / Array.Find(cfg.Goods!.Recipes, r => r.Name == "toolmaking")!.Inputs[0].PerOutput);
    }

    [Fact]
    public void PacketGates_AreMetOnlyByKnowledge_AWorldWithNoControllerRunsTheBaseEconomyExactly()
    {
        // A hand-built world with no control relation: base recipes read "everything known" (the R1 precedent), but no
        // packet recipe runs — so the crafting split, and every base output, is the pre-packet world's exactly.
        SimConfig on = TestConfigs.Sim();
        SimConfig off = TestConfigs.PreEc1(on);
        var craft = new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0);
        (string, long)[] plenty =
        [
            ("clay", 1_000_000), ("timber", 1_000_000), ("fiber", 1_000_000), ("copper-ore", 1_000_000),
            ("tin-ore", 1_000_000), ("bronze", 1_000_000), ("iron-ore", 1_000_000), ("coal", 1_000_000), ("iron", 1_000_000),
        ];
        WorldState a = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)]).Step(World(on, 100, craft, controlled: false, stocks: plenty));
        WorldState b = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(off)]).Step(World(off, 100, craft, controlled: false, stocks: plenty));
        Assert.Equal(0, Stock(a, on, "steel"));
        Assert.Equal(1_000_000, Stock(a, on, "iron"));
        foreach (string g in new[] { "pottery", "cloth", "bronze", "tools", "clay", "timber" })
            Assert.Equal(Stock(b, off, g), Stock(a, on, g));
        Assert.True(Stock(a, on, "pottery") > 0);
    }

    [Fact]
    public void IronOre_IsNotWorkedWithoutItsKnowledge_AndTakesItsShareOfExtractionWithIt()
    {
        SimConfig on = DiscoveryQuery.WithoutDiscovery(TestConfigs.Sim());   // the subject is the knowledge gate, not concealment
        SimConfig off = TestConfigs.PreEc1(on);
        var dig = new SectorAllocationRow(S0, 0.0, 0.0, 1.0, 0.0, 0.0);
        (string, double)[] deposits = [("stone", 1.0), ("iron-ore", 1.0)];

        // Unknown: the ore takes no labour, so stone is the pre-packet world's stone exactly (100 × 4.0 × 1.0 × 10).
        WorldState unknown = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)]).Step(World(on, 100, dig, deposits: deposits));
        WorldState twin = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(off)]).Step(World(off, 100, dig, deposits: deposits));
        Assert.Equal(0, Stock(unknown, on, "iron-ore"));
        Assert.Equal(4_000, Stock(twin, off, "stone"));
        Assert.Equal(Stock(twin, off, "stone"), Stock(unknown, on, "stone"));

        // Known: equal abundance, so the pool splits exactly in half (law 1: labour moves, it is not created). The
        // knowledge closure of iron_bloomery carries a realized extraction effect (ADR-047), so the per-worker rate is the
        // holder's, not the base 4.0 — the SPLIT is what is pinned: ore and stone equal, each half of the whole.
        WorldState known = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)])
            .Step(Know(World(on, 100, dig, deposits: deposits), on, Ruler, "iron_bloomery"));
        WorldState knownStoneOnly = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(on)])
            .Step(Know(World(on, 100, dig, deposits: [("stone", 1.0)]), on, Ruler, "iron_bloomery"));
        long ore = Stock(known, on, "iron-ore");
        Assert.True(ore > 0);
        Assert.Equal(ore, Stock(known, on, "stone"));
        Assert.Equal(Stock(knownStoneOnly, on, "stone"), 2 * ore);
        Assert.Equal(ore, Flow(known, on, "iron-ore", ReasonIds.Produced, sunk: false));

        // The labour surface lists what production works — the same predicate.
        Assert.DoesNotContain("iron-ore", LabourActivities.GoodsOf(World(on, 100, dig, deposits: deposits), on.Goods, S0, Sectors.Extraction, on.Research, on).Names);
        Assert.Contains("iron-ore", LabourActivities.GoodsOf(Know(World(on, 100, dig, deposits: deposits), on, Ruler, "iron_bloomery"), on.Goods, S0, Sectors.Extraction, on.Research, on).Names);
    }

    // --- worldgen: deposits and concealment ------------------------------------------------------------------------

    [Theory]
    [InlineData(1ul)]
    [InlineData(2ul)]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void Founding_BaseDepositsAndTheBaseConcealmentPool_AreUntouchedByThePacket(ulong seed)
    {
        SimConfig on = TestConfigs.Sim();
        SimConfig off = TestConfigs.PreEc1(on);
        WorldState a = WorldFounding.Found(TestConfigs.Worldgen(), on, seed);
        WorldState b = WorldFounding.Found(TestConfigs.Worldgen(), off, seed);
        Assert.Equal(b.Settlements.Count, a.Settlements.Count);

        // The base rows, in order, value for value; the packet's rows are the only additions.
        var baseRows = new List<DepositRow>();
        int ore = 0, coal = 0;
        for (int i = 0; i < a.Deposits.Count; i++)
        {
            DepositRow d = a.Deposits[i];
            if (d.Good.Value == 15) { ore++; continue; }
            if (d.Good.Value == 16) { coal++; continue; }
            baseRows.Add(d);
        }
        Assert.Equal(b.Deposits.Count, baseRows.Count);
        for (int i = 0; i < baseRows.Count; i++) Assert.Equal(b.Deposits[i], baseRows[i]);
        Assert.Equal(a.Settlements.Count, ore);
        Assert.Equal(a.Settlements.Count, coal);

        // Concealment: the base pool (copper, tin) is classified identically; the packet pool conceals near 0.25.
        int eligible = 0, concealed = 0;
        for (int s = 0; s < a.Settlements.Count; s++)
        {
            SettlementId id = a.Settlements[s].Id;
            int[] ca = DepositConcealment.ConcealedGoods(a.Deposits, a.Seed, on, id);
            int[] cb = DepositConcealment.ConcealedGoods(b.Deposits, b.Seed, off, id);
            Assert.Equal(Array.FindAll(cb, g => g < 15), Array.FindAll(ca, g => g < 15));
            concealed += Array.FindAll(ca, g => g >= 15).Length;
            for (int i = 0; i < a.Deposits.Count; i++)
                if (a.Deposits[i].Settlement == id && a.Deposits[i].Good.Value >= 15 && a.Deposits[i].Abundance > 0.0) eligible++;
        }
        Assert.True(eligible > 0);
        double share = concealed / (double)eligible;
        Assert.InRange(share, 0.1, 0.4);
    }

    // --- conservation over a run that exercises every good of the packet -------------------------------------------

    [Fact]
    public void Ec1Goods_AreConservedExactly_EveryTurn_OverARunThatMakesAllFour()
    {
        SimConfig cfg = DiscoveryQuery.WithoutDiscovery(TestConfigs.Sim());   // every ore visible: exercise, not survey
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        Know(w, cfg, null, "iron_bloomery", "coke", "steel_carburized", "bessemer", "iron_farm_tools");
        Allocate(w, 40.0, 10.0, 25.0, 15.0, 10.0);
        TurnExecutor exec = Canonical(cfg);
        var produced = new long[19];
        long ironOreIn = 0, coalIn = 0, ironIn = 0;
        for (int t = 0; t < 30; t++)
        {
            WorldState next = exec.Step(w);
            TurnRecord rec = Observer.Observe(w, next, cfg, []).Turn;
            foreach (StockRecord st in rec.Stocks)
            {
                Assert.True(st.Reconciles, $"turn {rec.Turn}: {st.Name} discrepancy {st.Discrepancy}");
                long sourced = 0, sunk = 0;
                foreach (var leg in st.Sources) sourced += leg.Units;
                foreach (var leg in st.Sinks) sunk += leg.Units;
                Assert.Equal(st.Closing, st.Opening + sourced - sunk);
            }
            foreach (GoodAccount g in rec.Goods)
            {
                Assert.True(g.Reconciles, $"turn {rec.Turn}: {g.Name} discrepancy {g.Discrepancy}");
                if (g.Good is >= 15 and <= 18) produced[g.Good] += g.Produced;
                if (g.Good == 15) ironOreIn += g.InputsConsumed;
                if (g.Good == 16) coalIn += g.InputsConsumed;
                if (g.Good == 17) ironIn += g.InputsConsumed;
            }
            w = next;
        }
        // Non-vacuity: every good of the packet was made and every raw one was used.
        for (int g = 15; g <= 18; g++) Assert.True(produced[g] > 0, $"good {g} never produced in 30 turns");
        Assert.True(ironOreIn > 0 && coalIn > 0 && ironIn > 0);
    }
}
