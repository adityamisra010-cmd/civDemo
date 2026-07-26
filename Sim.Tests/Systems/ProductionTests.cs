using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.3 acceptance (D-032). The four stated criteria, each with teeth:
///   1. output scales with LABOR and INPUTS, never from nothing;
///   2. tool stocks measurably raise yield AND deplete (ClassSystemTests owns
///      the monotone/saturation/depletion pin; here we pin the conservation
///      side — every unit produced traces to inputs consumed);
///   3. zero references remain to the deleted M2 scaffolding (grep-level,
///      asserted as a test so it cannot rot);
///   4. dt-halving holds — the whole sector loop is dt-correct (law 3).
/// </summary>
public class ProductionTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static readonly SettlementId S0 = new(0);

    /// <summary>A hand-built world: one settlement, `adults` adults in cohort 5,
    /// a stock row for every good, and the given sector allocation.</summary>
    private static WorldState World(
        SimConfig cfg, long adults, SectorAllocationRow allocation,
        double arableKm2 = 0.0, (string Good, long Amount)[]? stocks = null,
        (string Good, double Abundance)[]? deposits = null)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = adults;
        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            S0, NodeCount: 1, EffectiveArableKm2: arableKm2,
            NetworkRevision: 0, LastRecomputeTurn: 0));
        world.SectorAllocations.Add(allocation);

        var ledger = new Ledger(world.LedgerFlows);
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = world.GoodStocks.Add(new GoodStockRow(
                S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            long amount = 0;
            if (stocks is not null)
                foreach ((string good, long qty) in stocks)
                    if (good == g.Name) amount = qty;
            if (amount > 0)
            {
                ledger.Flow(ref world.GoodStocks.Ref(row).Amount,
                    ConservedQuantityIds.OfGood(new GoodId(g.Id)), ReasonIds.InitialEndowment,
                    amount, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        if (deposits is not null)
            foreach ((string good, double abundance) in deposits)
                world.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf(good)), abundance));
        return world;
    }

    private static long Stock(WorldState w, SimConfig cfg, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == id) return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static long FlowOf(WorldState w, SimConfig cfg, string good, ReasonId reason, bool sunk)
    {
        var q = ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.IdOf(good)));
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == q && w.LedgerFlows[i].Reason == reason)
                return sunk ? w.LedgerFlows[i].TotalSunk : w.LedgerFlows[i].TotalSourced;
        return 0;
    }

    // --- 1. never from nothing ------------------------------------------------

    [Fact]
    public void Crafting_WithoutInputs_ProducesExactlyNothing()
    {
        // Full crafting allocation, plenty of labor, ZERO input stocks. Pottery
        // needs clay + timber; with neither, output must be EXACTLY zero — not
        // small, not rounded down from something. This is law 1 at the level
        // that matters: the recipe cannot mint pots from labor alone.
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState next = exec.Step(World(cfg, adults: 5000,
            allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0)));

        Assert.Equal(0, Stock(next, cfg, "pottery"));
        Assert.Equal(0, Stock(next, cfg, "cloth"));
        Assert.Equal(0, FlowOf(next, cfg, "pottery", ReasonIds.Produced, sunk: false));
    }

    [Fact]
    public void Crafting_OutputIsCappedByTheScarcestInput_AndConsumesIt()
    {
        // Pottery = 2 clay + 0.5 timber per pot. Give 100 clay and 1000 timber:
        // clay binds at 50 pots. Labor is abundant, so the INPUT cap is what
        // decides — and the clay actually leaves the stock.
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState next = exec.Step(World(cfg, adults: 100_000,
            allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("clay", 100), ("timber", 1000)]));

        long pots = Stock(next, cfg, "pottery");
        Assert.InRange(pots, 1, 50);                        // capped by clay, never above
        Assert.Equal(pots, FlowOf(next, cfg, "pottery", ReasonIds.Produced, sunk: false));

        // Conservation of the INPUT side: clay consumed ≈ 2 per pot, and the
        // stock never goes negative (Throw would have fired if it tried).
        long clayUsed = FlowOf(next, cfg, "clay", ReasonIds.InputsConsumed, sunk: true);
        Assert.Equal(100 - Stock(next, cfg, "clay"), clayUsed);
        Assert.True(clayUsed >= 2 * pots - 1 && clayUsed <= 2 * pots + 1,
            $"{clayUsed} clay for {pots} pots — recipe ratio violated (expected ≈2 per pot)");
        Assert.True(Stock(next, cfg, "clay") >= 0);
    }

    [Fact]
    public void Crafting_ScalesWithLabor_UntilInputsBind()
    {
        // Same generous inputs, growing crafting labor: output rises, then
        // stops at the input ceiling. Both halves of Leontief, in one series.
        SimConfig cfg = TestConfigs.Sim();
        // Sizing (stated, because it is easy to get wrong): pottery is 0.05
        // labor-years per pot, so at dt = 10 ONE crafter makes ~200 pots — and
        // crafting labor splits across the two ungated recipes (pottery and
        // weaving; bronze/tools are artisan_share-gated and unavailable at
        // turn 1), so ~100 pots per crafter. Inputs must therefore be large
        // enough that the labor side is what binds at the low end.
        const long clay = 100_000;                          // ceiling: 50,000 pots
        long[] adultCounts = [10, 50, 500, 100_000];  // 500 puts 250 on pottery ⇒ exactly the 50,000 ceiling
        var pots = new long[adultCounts.Length];
        for (int i = 0; i < adultCounts.Length; i++)
        {
            var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
            WorldState next = exec.Step(World(cfg, adultCounts[i],
                allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
                stocks: [("clay", clay), ("timber", clay)]));
            pots[i] = Stock(next, cfg, "pottery");
        }
        for (int i = 1; i < pots.Length; i++)
            Assert.True(pots[i] >= pots[i - 1], $"output not monotone in labor: {pots[i - 1]} → {pots[i]}");
        Assert.True(pots[1] > pots[0], "labor had no effect — the labor side is inert");
        Assert.Equal(pots[^1], pots[^2]);                   // saturated: inputs bind, not labor
        Assert.True(pots[^1] <= clay / 2,
            $"{pots[^1]} pots from {clay} clay — exceeded the 2-clay-per-pot input ceiling");
    }

    [Fact]
    public void Extraction_WithoutDeposits_ProducesNothing_AndWithThemFollowsAbundance()
    {
        SimConfig cfg = TestConfigs.Sim();
        var alloc = new SectorAllocationRow(S0, 0.0, 0.0, 1.0, 0.0, 0.0);

        var bare = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)])
            .Step(World(cfg, adults: 5000, allocation: alloc));
        Assert.Equal(0, Stock(bare, cfg, "stone"));
        Assert.Equal(0, Stock(bare, cfg, "clay"));

        // Endowed: richer deposit yields strictly more (labor follows the seam
        // AND the seam gives more per digger — the concentration is deliberate).
        var endowed = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)])
            .Step(World(cfg, adults: 5000, allocation: alloc,
                deposits: [("stone", 0.8), ("clay", 0.2)]));
        Assert.True(Stock(endowed, cfg, "stone") > 0, "rich deposit produced nothing");
        Assert.True(Stock(endowed, cfg, "stone") > Stock(endowed, cfg, "clay"),
            "extraction did not follow abundance — comparative advantage cannot emerge");
    }

    [Fact]
    public void SectorShares_AreNormalized_SoAPartialAllocationIsWellDefined()
    {
        // Raw weights need not sum to 1: consumers normalize. An all-farming
        // row and a 2.0-farming row must produce the SAME harvest.
        SimConfig cfg = TestConfigs.Sim();
        WorldState a = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)])
            .Step(World(cfg, 1000, new SectorAllocationRow(S0, 1.0, 0, 0, 0, 0), arableKm2: 1e9));
        WorldState b = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)])
            .Step(World(cfg, 1000, new SectorAllocationRow(S0, 2.0, 0, 0, 0, 0), arableKm2: 1e9));
        Assert.Equal(Stock(a, cfg, "grain"), Stock(b, cfg, "grain"));

        // And a half-and-half row gives exactly half the farm labor.
        WorldState half = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)])
            .Step(World(cfg, 1000, new SectorAllocationRow(S0, 1.0, 0, 0, 0, 1.0), arableKm2: 1e9));
        Assert.Equal(Stock(a, cfg, "grain") / 2, Stock(half, cfg, "grain"));
    }

    [Fact]
    public void Crafting_ThreeRecipesShareOneScarceInput_NeverOverdraws()
    {
        // THE SHARPEST CONSERVATION RISK IN THIS PACKET, pinned. Timber is an
        // input to pottery-firing (0.5), bronze-casting (2.0) AND toolmaking
        // (1.0). The per-recipe output cap is computed from the CURRENT stock and
        // the sinks execute sequentially, so recipe 2 sees a stock recipe 1
        // already drained. The claim in ProductionSystem's header is that
        // OverdrawPolicy.Throw is safe BY CONSTRUCTION because each recipe
        // re-reads the drained stock. If that reasoning is wrong, this test
        // throws rather than quietly going negative.
        //
        // Scarce timber, abundant everything else, artisan gate open so all four
        // recipes compete for the same timber in one turn.
        SimConfig cfg = TestConfigs.Sim();
        const long timber = 37;                             // deliberately awkward, not a round number
        WorldState world = World(cfg, adults: 100_000,
            allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("timber", timber), ("clay", 1_000_000), ("fiber", 1_000_000),
                     ("copper-ore", 1_000_000), ("tin-ore", 1_000_000), ("bronze", 1_000_000)]);
        world.Variables.Add(new VariableRow(S0, Variables.ArtisanShare, 0.5));

        WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]).Step(world);

        long left = Stock(next, cfg, "timber");
        long used = FlowOf(next, cfg, "timber", ReasonIds.InputsConsumed, sunk: true);
        Assert.True(left >= 0, $"timber went negative: {left}");
        Assert.Equal(timber, left + used);                   // EXACT, no epsilon (law 1)
        Assert.True(used > 0, "no timber consumed — the shared-input contention is not exercised");
    }

    [Fact]
    public void Recipe_InputsAndLabor_ArePerEXECUTION_NotPerOutputUnit()
    {
        // DIMENSIONAL DECLARATION, pinned (S8 §4.1 requirement 2 applied to this
        // packet's own contract). T3.2's goods.json _doc said "inputs per output
        // unit", but `toolmaking` declares bronze 1.0 + timber 1.0 with
        // output.qty 2 — under a per-UNIT reading `qty` would be decorative and
        // 2 tools would cost 2 bronze; under a per-EXECUTION reading one batch
        // costs 1 bronze and yields 2 tools, which is what makes toolmaking a
        // value-add step. T3.3 executes the PER-EXECUTION reading, the doc was
        // corrected to say so, and this test is what stops the reading drifting
        // again: it fixes the ratio at 2 tools per bronze.
        //
        // toolmaking is artisan-gated (`artisan_share > 0.05`), so the world
        // publishes that variable directly — the D-020 gate is exercised here too.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = World(cfg, adults: 200,
            allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("bronze", 100), ("timber", 100_000)]);
        world.Variables.Add(new VariableRow(S0, Variables.ArtisanShare, 0.5));

        WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]).Step(world);

        long tools = Stock(next, cfg, "tools");
        long bronzeUsed = FlowOf(next, cfg, "bronze", ReasonIds.InputsConsumed, sunk: true);
        Assert.True(tools > 0, "toolmaking produced nothing — the artisan gate or the recipe is broken");
        Assert.True(bronzeUsed > 0, "tools appeared without consuming bronze — production from nothing");
        // EXACT, no epsilon: the whole-unit accumulators make this a clean 2:1.
        Assert.Equal(2 * bronzeUsed, tools);
    }

    // --- 4. dt-correctness ----------------------------------------------------

    [Fact]
    public void Production_DtCorrect_EqualHorizonTotalsAgreeAcrossDt()
    {
        // Law 3 across the WHOLE sector loop (farming + extraction + crafting
        // together): the same sim-year horizon must produce the same totals at
        // dt 10, 5 and 2.5, within the remainder's 1-unit telescoping. A
        // hardcoded per-turn amount anywhere in the loop shifts these ~2×.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 200;
        double[] dts = [10.0, 5.0, 2.5];
        var grain = new long[dts.Length];
        var stone = new long[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            var exec = new TurnExecutor(FlatEra(dts[d]), [SystemCatalog.Production(cfg)]);
            WorldState w = World(cfg, adults: 2000,
                allocation: new SectorAllocationRow(S0, 1.0, 0.0, 1.0, 0.0, 0.0),
                arableKm2: 1e9, deposits: [("stone", 0.6)]);
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = exec.Step(w);
            grain[d] = Stock(w, cfg, "grain");
            stone[d] = Stock(w, cfg, "stone");
        }

        for (int d = 1; d < dts.Length; d++)
        {
            Assert.True(Math.Abs(grain[d] - grain[0]) <= 1,
                $"grain not dt-invariant: dt {dts[0]} → {grain[0]}, dt {dts[d]} → {grain[d]}");
            Assert.True(Math.Abs(stone[d] - stone[0]) <= 1,
                $"stone not dt-invariant: dt {dts[0]} → {stone[0]}, dt {dts[d]} → {stone[d]}");
        }
        Assert.True(grain[0] > 0 && stone[0] > 0, "nothing produced — dt test vacuous");
    }

    // --- 3. the demolition, asserted so it cannot rot -------------------------

    [Fact]
    public void ScaffoldingDemolition_NoReferencesRemain()
    {
        // m3 spec §1 acceptance: "zero references remain to the deleted
        // multiplier (grep-level)". Asserted here rather than left to a
        // one-time grep, so a revert or a bad merge fails the build.
        string root = FindRepoRoot();
        string[] retired =
        [
            "ToolYieldBonusCap", "ToolYieldBonusSlope", "toolYieldBonusCap",
            "toolYieldBonusSlope", "ConstructionLaborWeight", "constructionLaborWeight",
            "FarmingSystem", "FarmingTables",
        ];
        var offenders = new List<string>();
        foreach (string dir in new[] { "Sim.Core", "Sim.Data", "Sim.Cli", "Sim.Tests", "Sim.Ui", "Sim.Ui.Tests" })
        {
            string path = Path.Combine(root, dir);
            if (!Directory.Exists(path)) continue;
            foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (Path.GetExtension(file) is not (".cs" or ".json")) continue;
                if (file.EndsWith("sim.json", StringComparison.Ordinal)) continue;
                if (file.EndsWith("ProductionTests.cs", StringComparison.Ordinal)) continue;
                // COMMENT LINES ARE EXEMPT — the same call made for the CR-002
                // denomination gate: the whole point of a demolition is that
                // the history stays legible, and a dead name inside a /// block
                // cannot be read by the compiler. LIVE CODE is not exempt.
                foreach (string line in File.ReadAllLines(file))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || trimmed.StartsWith("*", StringComparison.Ordinal)
                        || trimmed.StartsWith("\"_doc", StringComparison.Ordinal)) continue;
                    foreach (string token in retired)
                        if (line.Contains(token, StringComparison.Ordinal))
                            offenders.Add($"{Path.GetRelativePath(root, file)}: {token}");
                }
            }
        }
        Assert.True(offenders.Count == 0,
            "M2 scaffolding survives the T3.3 demolition:\n  " + string.Join("\n  ", offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
