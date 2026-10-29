using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Production;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.EconomyRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-056 §8 — EC-2 MOUNTS (D-065 R2: a separately reviewed M7 packet). Horses are a HERD: a conserved stock distinct
/// from livestock, not food, that breeds from itself on the settlement's pasture under its herding workers once the
/// settlement knows horse_domestication, and draws grain fodder. Each test pins one property with exact integers where
/// the quantity is conserved.
/// </summary>
public class EconomyMountsTests
{
    private static readonly SectorAllocationRow Herd = new(S0, 0.0, 1.0, 0.0, 0.0, 0.0);

    [Fact]
    public void Ec2_IsShippedOn_HorsesAreAHerdNotFoodNotADeposit()
    {
        GoodsConfig goods = TestConfigs.Sim().Goods!;
        Assert.True(goods.IsPacketEnabled("ec2"));
        GoodEntry h = goods.ById(19);
        Assert.Equal("horses", h.Name);
        Assert.Equal("mount", h.Category);
        Assert.Equal(4.0, h.BulkPerUnit);
        Assert.Null(h.DepositChannel);
        Assert.Equal("ec2", h.Packet);
        Assert.Equal("horse_domestication", h.Knowledge);
        Assert.Equal(new HerdEntry("livestock", 2.0, 0.2, 0.12, "grain", 0.2), h.Herd);
        foreach (RecipeEntry r in goods.Recipes)
        {
            Assert.NotEqual("horses", r.Output.Good);
            foreach (RecipeInput i in r.Inputs) Assert.NotEqual("horses", i.Good);
        }
        // Not food: no basket line names it (the Sustenance set is the basket book's).
        Assert.DoesNotContain(TestConfigs.Sim().Needs!.Baskets.Entries, l => l.Good == "horses");
    }

    [Fact]
    public void SwitchOff_RemovesExactlyTheHerd()
    {
        GoodsConfig on = TestConfigs.Sim().Goods!;
        GoodsConfig off = TestConfigs.PreEc2(TestConfigs.Sim()).Goods!;
        Assert.Equal(["horses"], off.DisabledGoodNames);
        Assert.Equal(on.Goods.Length - 1, off.Goods.Length);
        Assert.Equal(-1, off.IdOf("horses"));
        Assert.Equal(on.Recipes.Length, off.Recipes.Length);
    }

    [Fact]
    public void Loader_RefusesAHerdAsARecipeInput_AndAHerdOnADeposit()
    {
        JsonObject doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        foreach (JsonNode? r in doc["recipes"]!.AsArray())
            if ((string?)r!["name"] == "weaving") r["inputs"]!.AsArray().Add(new JsonObject { ["good"] = "horses", ["perOutput"] = 1.0 });
        var e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("herd good 'horses'", e.Message);

        doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        foreach (JsonNode? g in doc["goods"]!.AsArray())
            if ((string?)g!["name"] == "horses") g["depositChannel"] = "moisture";
        e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("both a herd and a depositChannel", e.Message);

        doc = JsonNode.Parse(TestConfigs.GoodsJson())!.AsObject();
        foreach (JsonNode? g in doc["goods"]!.AsArray())
            if ((string?)g!["name"] == "horses") g["herd"]!["pastureGood"] = "bronze";
        e = Assert.Throws<GoodsConfigException>(() => GoodsConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("pastureGood 'bronze'", e.Message);
    }

    // --- the closed form ----------------------------------------------------------------------------------------------

    [Fact]
    public void HerdAfter_IsTheExactFlow_ComposingTwoHalfTurnsGivesTheWholeTurn()
    {
        // An exact solution of an autonomous ODE composes: x(t1 + t2) = x(t2)(x(t1)). The Euler step this replaces does
        // not — that is the dt-correctness the closed form buys (law 3). Below K, at K, above K, and with no capacity.
        (double H, double K, double C)[] cases = [(0, 100, 10), (5, 100, 10), (60, 100, 0), (100, 100, 10), (180, 100, 10), (40, 0, 0)];
        foreach ((double h, double k, double c) in cases)
            foreach (double dt in new[] { 0.5, 1.0, 2.0, 10.0 })
            {
                double whole = ProductionSystem.HerdAfter(h, k, c, 0.12, dt);
                double halves = ProductionSystem.HerdAfter(ProductionSystem.HerdAfter(h, k, c, 0.12, dt / 2), k, c, 0.12, dt / 2);
                Assert.True(Math.Abs(whole - halves) <= 1e-9 * Math.Max(1.0, whole), $"H {h} K {k} c {c} dt {dt}: {whole} vs {halves}");
                if (h <= k) Assert.True(whole >= h - 1e-12 && whole <= Math.Max(k, h) + 1e-12, "below K the herd rises toward K and never crosses it");
                else Assert.True(whole < h && whole >= k, "above K the excess is lost toward K");
            }
        Assert.Equal(40 * Math.Exp(-1.2), ProductionSystem.HerdAfter(40, 0, 0, 0.12, 10));
        // The head-years are additive over the flow: ∫0..dt = ∫0..dt/2 + ∫ from the half-turn herd over dt/2.
        foreach ((double h, double k, double c) in cases)
        {
            double whole = ProductionSystem.HeadYears(h, k, c, 0.12, 10.0);
            double half = ProductionSystem.HeadYears(h, k, c, 0.12, 5.0)
                + ProductionSystem.HeadYears(ProductionSystem.HerdAfter(h, k, c, 0.12, 5.0), k, c, 0.12, 5.0);
            Assert.True(Math.Abs(whole - half) <= 1e-9 * Math.Max(1.0, whole), $"H {h} K {k} c {c}: {whole} vs {half}");
        }
        Assert.Equal(1000.0, ProductionSystem.HeadYears(100, 100, 10, 0.12, 10), 9);
        Assert.Equal(100.0, ProductionSystem.HerdAfter(100, 100, 10, 0.12, 10));
    }

    // --- breeding in the step -----------------------------------------------------------------------------------------

    [Fact]
    public void Breeding_NeedsItsKnowledge_AndConservesHerdAndFodderExactly()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        (string, double)[] pasture = [("livestock", 0.5)];
        (string, long)[] grain = [("grain", 1_000_000)];

        WorldState unknown = exec.Step(World(cfg, 100, Herd, stocks: grain, deposits: pasture));
        Assert.Equal(0, Stock(unknown, cfg, "horses"));
        Assert.Equal(0, Flow(unknown, cfg, "grain", ReasonIds.Fodder, sunk: true));

        WorldState known = exec.Step(Know(World(cfg, 100, Herd, stocks: grain, deposits: pasture), cfg, Ruler, "horse_domestication"));
        // K = 100 herders × 2 head × 0.5 pasture = 100; capture c = 100 × 0.2 × 0.5 = 10 head/yr; from an empty herd.
        double h1 = ProductionSystem.HerdAfter(0, 100.0 * 2.0 * 0.5, 100.0 * 0.2 * 0.5, 0.12, 10.0);
        long horses = (long)Math.Floor(h1);
        Assert.True(horses is > 0 and <= 100);
        Assert.Equal(horses, Stock(known, cfg, "horses"));
        Assert.Equal(horses, Flow(known, cfg, "horses", ReasonIds.Produced, sunk: false));
        long fodder = (long)Math.Floor(ProductionSystem.HeadYears(0, 100.0 * 2.0 * 0.5, 100.0 * 0.2 * 0.5, 0.12, 10.0) * 0.2);
        Assert.True(fodder > 0);
        Assert.Equal(fodder, Flow(known, cfg, "grain", ReasonIds.Fodder, sunk: true));
        Assert.Equal(1_000_000 - fodder, Stock(known, cfg, "grain"));
        // Livestock untouched by the herd: the same knowledge without the packet (its realized herding effects included).
        SimConfig twin = TestConfigs.PreEc2(cfg);
        WorldState twinKnown = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(twin)])
            .Step(Know(World(twin, 100, Herd, stocks: grain, deposits: pasture), twin, Ruler, "horse_domestication"));
        Assert.Equal(Stock(twinKnown, twin, "livestock"), Stock(known, cfg, "livestock"));
    }

    [Fact]
    public void NoFodder_NoBreeding_AndAnUnfedHerdIsLostAtTheBreedingRate()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState w = Know(World(cfg, 100, Herd, stocks: [("horses", 50)], deposits: [("livestock", 0.5)]), cfg, Ruler, "horse_domestication");
        WorldState next = exec.Step(w);
        long expected = (long)Math.Floor(50 * Math.Exp(-0.12 * 10.0));
        Assert.Equal(expected, Stock(next, cfg, "horses"));
        Assert.Equal(50 - expected, Flow(next, cfg, "horses", ReasonIds.HerdLosses, sunk: true));
        Assert.Equal(0, Flow(next, cfg, "grain", ReasonIds.Fodder, sunk: true));
    }

    [Fact]
    public void Breeding_IsDtCorrect_TenOneYearTurnsMatchOneTenYearTurn()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState Start() => Know(World(cfg, 100, Herd, stocks: [("grain", 1_000_000), ("horses", 10)],
            deposits: [("livestock", 0.5)]), cfg, Ruler, "horse_domestication");
        WorldState one = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]).Step(Start());
        WorldState ten = new TurnExecutor(FlatEra(1.0), [SystemCatalog.Production(cfg)]).Run(Start(), 10);
        // The herd is the exact flow, sliced into whole units with a banked remainder: within one unit.
        Assert.InRange(Stock(ten, cfg, "horses") - Stock(one, cfg, "horses"), -1, 1);
        Assert.True(Stock(one, cfg, "horses") > 10);
        // Fodder is the exact head-years of the trajectory: the two slicings draw the same grain to within the whole-unit
        // slicing of the horses (each of the ten short turns integrates from a herd floored to whole head).
        long f1 = Flow(one, cfg, "grain", ReasonIds.Fodder, true), f10 = Flow(ten, cfg, "grain", ReasonIds.Fodder, true);
        Assert.True(Math.Abs(f1 - f10) <= 2, $"fodder {f1} vs {f10}");
    }

    [Fact]
    public void LabourSurface_ListsTheHerd_ExactlyWhereProductionBreedsIt()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = World(cfg, 100, Herd, deposits: [("livestock", 0.5)]);
        Assert.DoesNotContain("horses", LabourActivities.GoodsOf(w, cfg.Goods, S0, Sectors.Herding, cfg.Research, cfg).Names);
        Know(w, cfg, Ruler, "horse_domestication");
        Assert.Contains("horses", LabourActivities.GoodsOf(w, cfg.Goods, S0, Sectors.Herding, cfg.Research, cfg).Names);
        Assert.DoesNotContain("horses", LabourActivities.GoodsOf(w, cfg.Goods, S0, Sectors.Extraction, cfg.Research, cfg).Names);
    }

    [Fact]
    public void Horses_AreConservedExactly_EveryTurn_AndTheGrainAccountClosesThroughFodder()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        Know(w, cfg, null, "horse_domestication");
        TurnExecutor exec = Canonical(cfg);
        long bred = 0, fodder = 0;
        for (int t = 0; t < 30; t++)
        {
            WorldState next = exec.Step(w);
            TurnRecord rec = Observer.Observe(w, next, cfg, []).Turn;
            foreach (StockRecord st in rec.Stocks) Assert.True(st.Reconciles, $"turn {rec.Turn}: {st.Name} discrepancy {st.Discrepancy}");
            Assert.True(rec.Grain.Reconciles, $"turn {rec.Turn}: grain discrepancy {rec.Grain.Discrepancy}");
            Assert.Equal(rec.Grain.Closing, rec.Grain.Opening + rec.Grain.Endowment + rec.Grain.Harvest - rec.Grain.Eaten
                - rec.Grain.Spoilage - rec.Grain.Overflow - rec.Grain.Expedition - rec.Grain.Military - rec.Grain.Fodder);   // ADR-055: the warband's upkeep too
            foreach (GoodAccount g in rec.Goods)
            {
                Assert.True(g.Reconciles, $"turn {rec.Turn}: {g.Name} discrepancy {g.Discrepancy}");
                if (g.Good == 19) bred += g.Produced;
            }
            fodder += rec.Grain.Fodder;
            w = next;
        }
        Assert.True(bred > 0, "no horse bred in 30 turns with horse_domestication known — vacuous");
        Assert.True(fodder > 0, "no fodder drawn — vacuous");
    }
}
