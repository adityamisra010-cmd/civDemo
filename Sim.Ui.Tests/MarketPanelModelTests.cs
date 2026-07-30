using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T3.9a acceptance, view-model level: the market panel (goods/stock/price/
// last-move rows + the PriceTerms decomposition), headless like HudModel —
// no Game, no window; the executor runs exactly as the CLI does. §7.5 note:
// grain's price rests against its own pin (1.0 by definition), so NO test
// here has grain's price value as its subject — grain assertions cover the
// numeraire LABEL only; price/delta behavior is asserted on non-grain goods.
public class MarketPanelModelTests
{
    internal static SimConfig SimCfg()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        SimConfig cfg = SimConfigLoader.Load(stream, needs, goods);
        return cfg with { Founding = cfg.Founding with { EndowmentJitter = 0.0 } };
    }

    internal static WorldgenConfig DevCfg()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) is { } c
            ? c with { SizePx = 256, Siting = c.Siting with { SettlementCount = 4 } }
            : throw new InvalidOperationException();
    }

    internal static TurnExecutor Executor(SimConfig cfg)
    {
        using var eraStream = global::Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = global::Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), null);
    }

    internal static WorldState SteppedWorld(SimConfig cfg, int turns)
    {
        TurnExecutor exec = Executor(cfg);
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        for (int t = 0; t < turns; t++) world = exec.Step(world);
        return world;
    }

    // --- item 1: the goods/price rows ------------------------------------

    [Fact]
    public void Rows_EveryRegistryGood_StockPriceDeltaMatchTheTables()
    {
        SimConfig cfg = SimCfg();
        WorldState world = SteppedWorld(cfg, 3);
        int settlement = world.Settlements[0].Id.Value;

        IReadOnlyList<MarketGoodRow> rows = MarketModel.Rows(world, settlement, cfg.Goods!);
        Assert.Equal(cfg.Goods!.Goods.Length, rows.Count);

        foreach (MarketGoodRow row in rows)
        {
            // Stock: the GoodStocks amount for this (settlement, good), 0 if no row.
            long stock = 0;
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Settlement.Value == settlement
                    && world.GoodStocks[i].Good.Value == row.GoodId)
                { stock = world.GoodStocks[i].Amount.Value; break; }
            Assert.Equal(stock, row.Stock);

            if (row.IsNumeraire) continue; // §7.5: grain's price is pinned — not a test subject
            double? price = null;
            for (int i = 0; i < world.Prices.Count; i++)
                if (world.Prices[i].Settlement.Value == settlement
                    && world.Prices[i].Good.Value == row.GoodId)
                { price = world.Prices[i].Price; break; }
            Assert.Equal(price, row.Price);

            double? delta = null;
            for (int i = 0; i < world.PriceTerms.Count; i++)
                if (world.PriceTerms[i].Settlement.Value == settlement
                    && world.PriceTerms[i].Good.Value == row.GoodId)
                { delta = world.PriceTerms[i].Delta; break; }
            Assert.Equal(delta, row.Delta);
            Assert.NotNull(price); // PriceSystem writes every registry good every turn
            Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"price {price!.Value:F4}"), row.Line);
        }
    }

    [Fact]
    public void GrainRow_IsLabelledNumeraire_NeverAChangeReadout()
    {
        SimConfig cfg = SimCfg();
        WorldState world = SteppedWorld(cfg, 2);
        IReadOnlyList<MarketGoodRow> rows =
            MarketModel.Rows(world, world.Settlements[0].Id.Value, cfg.Goods!);
        MarketGoodRow grain = rows.Single(r => r.IsNumeraire);
        Assert.Equal("grain", grain.Name);
        Assert.Contains("(numeraire, pinned)", grain.Line);
        Assert.DoesNotContain("chg", grain.Line); // a pinned price has no "move" to report
    }

    [Fact]
    public void Rows_BeforeFirstTurn_PriceReadsNotYetMeasured_NeverFabricated()
    {
        // §7.4 guard: prices are written by PriceSystem each turn; a founded,
        // never-stepped world has NO price rows. Proven red by removing the
        // guard (fabricating a price for an absent row) — see commit message.
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        Assert.Equal(0, world.Prices.Count); // the precondition the guard exists for
        IReadOnlyList<MarketGoodRow> rows =
            MarketModel.Rows(world, world.Settlements[0].Id.Value, cfg.Goods!);
        foreach (MarketGoodRow row in rows.Where(r => !r.IsNumeraire))
        {
            Assert.Null(row.Price);
            Assert.Contains("price not yet measured", row.Line);
        }
    }

    [Fact]
    public void Rows_UnknownSettlement_RendersHarmlessly()
    {
        SimConfig cfg = SimCfg();
        WorldState world = SteppedWorld(cfg, 1);
        IReadOnlyList<MarketGoodRow> rows = MarketModel.Rows(world, 9999, cfg.Goods!);
        Assert.Equal(cfg.Goods!.Goods.Length, rows.Count);
        Assert.All(rows, r => Assert.Equal(0, r.Stock));
        Assert.All(rows.Where(r => !r.IsNumeraire), r => Assert.Null(r.Price));
    }

    // --- item 1: the PriceTerms decomposition ----------------------------

    [Fact]
    public void Breakdown_TermsMatchTheRow_AndHeaderReconstructsTheNewPrice()
    {
        SimConfig cfg = SimCfg();
        WorldState world = SteppedWorld(cfg, 3);
        int settlement = world.Settlements[0].Id.Value;
        int timber = cfg.Goods!.IdOf("timber");

        PriceTermRow expected = default;
        bool found = false;
        for (int i = 0; i < world.PriceTerms.Count; i++)
            if (world.PriceTerms[i].Settlement.Value == settlement
                && world.PriceTerms[i].Good.Value == timber)
            { expected = world.PriceTerms[i]; found = true; break; }
        Assert.True(found);

        PriceBreakdown? breakdown = MarketModel.Breakdown(world, settlement, timber, cfg.Goods!);
        Assert.NotNull(breakdown);
        Assert.Equal(expected.PrevPrice, breakdown!.PrevPrice);
        Assert.Equal(expected.Delta, breakdown.Delta);
        Assert.Equal(expected.PrevPrice + expected.Delta, breakdown.NewPrice);
        Assert.Equal(
            [expected.Consumption, expected.InputDemand, expected.Production,
             expected.StockRelease, expected.Clamp],
            breakdown.Terms.Select(t => t.Value).ToArray());
        Assert.Equal(PriceBreakdown.TermNames, breakdown.Terms.Select(t => t.Name).ToArray());
        Assert.Equal(5, breakdown.TermLines.Count);
        // The T3.4 invariant surfaced: the displayed terms sum to the displayed delta.
        Assert.Equal(breakdown.Delta, breakdown.Terms.Sum(t => t.Value), 12);
    }

    private static WorldState TermWorld(PriceTermRow row)
    {
        var world = new WorldState(7) { Clock = new SimClock(1, 3600, 3600) };
        world.Settlements.Add(new SettlementRow(row.Settlement, SiteCell: 0, FoundedTurn: 0));
        world.PriceTerms.Add(row);
        return world;
    }

    [Fact]
    public void Breakdown_Driver_IsLargestMagnitudeTerm()
    {
        SimConfig cfg = SimCfg();
        var world = TermWorld(new PriceTermRow(new SettlementId(0), new GoodId(4),
            PrevPrice: 1.0, Consumption: 0.01, InputDemand: 0.002,
            Production: -0.05, StockRelease: -0.001, Clamp: 0.0, Delta: -0.039));
        PriceBreakdown? b = MarketModel.Breakdown(world, 0, 4, cfg.Goods!);
        Assert.Equal("production", b!.Driver); // magnitude, not sign, decides
        Assert.Equal("driver: production", b.DriverLine);
    }

    [Fact]
    public void Breakdown_Driver_TieDense_StableEarliestTermWins()
    {
        // CLAUDE.md: any argmax over double scores ships a tie-dense test.
        // All five terms at IDENTICAL magnitude (mixed signs): the stable
        // integer tie-break is the fixed term order — consumption wins.
        SimConfig cfg = SimCfg();
        var world = TermWorld(new PriceTermRow(new SettlementId(0), new GoodId(4),
            PrevPrice: 1.0, Consumption: 0.01, InputDemand: -0.01,
            Production: 0.01, StockRelease: -0.01, Clamp: 0.01, Delta: 0.01));
        Assert.Equal("consumption", MarketModel.Breakdown(world, 0, 4, cfg.Goods!)!.Driver);

        // A later-only tie (input demand vs stock release): earliest of the
        // tied pair wins, unaffected by smaller earlier terms.
        var world2 = TermWorld(new PriceTermRow(new SettlementId(0), new GoodId(4),
            PrevPrice: 1.0, Consumption: 0.001, InputDemand: -0.02,
            Production: 0.0, StockRelease: 0.02, Clamp: 0.0, Delta: 0.001));
        Assert.Equal("input demand", MarketModel.Breakdown(world2, 0, 4, cfg.Goods!)!.Driver);
    }

    [Fact]
    public void Breakdown_AllZeroTerms_NamesNoDriver()
    {
        SimConfig cfg = SimCfg();
        var world = TermWorld(new PriceTermRow(new SettlementId(0), new GoodId(4),
            PrevPrice: 1.0, Consumption: 0.0, InputDemand: 0.0,
            Production: 0.0, StockRelease: 0.0, Clamp: 0.0, Delta: 0.0));
        Assert.Equal("none (no movement)", MarketModel.Breakdown(world, 0, 4, cfg.Goods!)!.Driver);
    }

    [Fact]
    public void Breakdown_NoTermsRowYet_IsNull()
    {
        SimConfig cfg = SimCfg();
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42); // never stepped
        Assert.Null(MarketModel.Breakdown(
            world, world.Settlements[0].Id.Value, cfg.Goods!.IdOf("timber"), cfg.Goods!));
    }
}
