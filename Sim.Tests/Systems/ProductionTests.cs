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

    [Fact]
    public void LastProducedUnits_ZeroesWhenAGoodProducesNothing_NotStale()
    {
        // T3.3 adversarial finding. LastProducedUnits is documented as "the units
        // credited THIS turn (observational)", but only the success paths wrote
        // it, so a good that stopped producing kept reporting its last good turn
        // forever. Grain was accidentally safe (farming always writes it) and is
        // the only consumer today — food_surplus_ratio and migration's anyFood
        // gate — which is why this had to be fixed BEFORE T3.4/T3.5 begin
        // reading other goods' LastProduced.
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);

        // Exactly enough clay for one turn of pottery, then nothing.
        WorldState w = World(cfg, adults: 100_000,
            allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
            stocks: [("clay", 100), ("timber", 1000)]);

        WorldState w1 = exec.Step(w);
        Assert.True(LastProduced(w1, cfg, "pottery") > 0, "no pottery in turn 1 — the test is vacuous");

        // Run until the clay is genuinely gone. Proportional rationing (the
        // dt-invariance fix) can leave a unit behind, so the number of turns is
        // not something worth pinning — that the field ZEROES once production
        // stops is.
        WorldState cur = w1;
        for (int t = 0; t < 10 && Stock(cur, cfg, "clay") > 0; t++) cur = exec.Step(cur);
        Assert.Equal(0, Stock(cur, cfg, "clay"));
        long potteryAtExhaustion = Stock(cur, cfg, "pottery");

        WorldState after = exec.Step(cur);
        Assert.Equal(0, LastProduced(after, cfg, "pottery"));      // was: last good turn, forever
        Assert.Equal(potteryAtExhaustion, Stock(after, cfg, "pottery")); // truly nothing made

        // Grain's contract is unchanged: it still reports the turn's harvest.
        Assert.Equal(0, LastProduced(after, cfg, "grain"));     // no farm labor allocated here
    }

    private static long LastProduced(WorldState w, SimConfig cfg, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == id) return w.GoodStocks[i].LastProducedUnits;
        return -1;
    }

    [Fact]
    public void Farming_WithNegativeSectorWeight_DoesNotCrashTheKernel()
    {
        // T3.3 adversarial finding (minor, and the verifier was right that it is
        // unreachable through orders — they validate [0,100]). But the SCHEMA
        // reads the five weights as raw doubles, so a hand-edited or corrupted
        // snapshot delivers a state the sim could not step: farming was the only
        // sector without a non-positive-labor guard, and it threw
        // ArgumentOutOfRangeException out of the Ledger. Cheap to make
        // consistent with the other three sectors.
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
        WorldState w = World(cfg, adults: 1000,
            allocation: new SectorAllocationRow(S0, -1.0, 2.0, 0.0, 0.0, 0.0),
            arableKm2: 1e6);

        WorldState next = exec.Step(w);           // must not throw
        Assert.Equal(0, Stock(next, cfg, "grain")); // and produces nothing from negative labor
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
        var pottery = new long[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            var exec = new TurnExecutor(FlatEra(dts[d]), [SystemCatalog.Production(cfg)]);
            WorldState w = World(cfg, adults: 2000,
                allocation: new SectorAllocationRow(S0, 1.0, 0.0, 1.0, 1.0, 0.0),
                arableKm2: 1e9, deposits: [("stone", 0.6)],
                stocks: [("clay", 400_000), ("timber", 400_000), ("fiber", 400_000)]);
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = exec.Step(w);
            grain[d] = Stock(w, cfg, "grain");
            stone[d] = Stock(w, cfg, "stone");
            pottery[d] = Stock(w, cfg, "pottery");
        }

        for (int d = 1; d < dts.Length; d++)
        {
            Assert.True(Math.Abs(grain[d] - grain[0]) <= 1,
                $"grain not dt-invariant: dt {dts[0]} → {grain[0]}, dt {dts[d]} → {grain[d]}");
            Assert.True(Math.Abs(stone[d] - stone[0]) <= 1,
                $"stone not dt-invariant: dt {dts[0]} → {stone[0]}, dt {dts[d]} → {stone[d]}");
            Assert.True(Math.Abs(pottery[d] - pottery[0]) <= 1,
                $"pottery not dt-invariant: dt {dts[0]} → {pottery[0]}, dt {dts[d]} → {pottery[d]}");
        }
        // NON-VACUITY, all three sectors. The first version of this test claimed
        // to exercise "the WHOLE sector loop (farming + extraction + crafting)"
        // but allocated ZERO crafting labor and endowed no recipe inputs, so
        // Craft() returned at its guard and criterion 4 went untested for the
        // one sector that could fail it (T3.3 adversarial finding).
        Assert.True(grain[0] > 0, "no grain — farming half of the dt test is vacuous");
        Assert.True(stone[0] > 0, "no stone — extraction half of the dt test is vacuous");
        Assert.True(pottery[0] > 0, "no pottery — CRAFTING half of the dt test is vacuous");
    }

    [Fact]
    public void Crafting_IntraTurnRecipeChain_IsExactlyDtInvariant()
    {
        // THE REGRESSION GUARD. Sequential execution in registry order is not an
        // accident to be tidied away — it is what makes the recipe GRAPH work
        // inside one turn. bronze-casting precedes toolmaking, so toolmaking
        // re-reads a stock bronze-casting has already credited and the chain
        // ore -> bronze -> tools completes within a single Step. Here bronze
        // starts at ZERO, so every tool produced must have come from bronze cast
        // in the same turn.
        //
        // This is EXACTLY dt-invariant — 50 tools at dt 10, 5, 2.5, 1 and 0.5 —
        // and it is invariant BECAUSE the passes are sequential.
        //
        // A T3.3 review round replaced this loop with two-pass proportional
        // rationing to make contested-input COMPOSITION dt-invariant (see the
        // next test). That construction computes every ration from stocks read
        // BEFORE any recipe commits, so toolmaking could not see bronze cast
        // later in the same turn: bronze's stock was 0 at ration time, the
        // ration went to 0, and toolmaking was scaled out of existence. It
        // measured 0 / 24 / 36 / 44 / 46 tools across those same five dt — a
        // 0-vs-46 spread replacing an exact 50, i.e. strictly worse than the
        // spread it was written to remove, and worst at dt = 10, the shipped
        // Neolithic band. It was reverted. This test exists so that the same
        // fix cannot be re-attempted without failing first.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 10;
        double[] dts = [10.0, 5.0, 2.5, 1.0, 0.5];
        var tools = new long[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            var exec = new TurnExecutor(FlatEra(dts[d]), [SystemCatalog.Production(cfg)]);
            WorldState w = World(cfg, adults: 4,
                allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
                // NO bronze endowment — toolmaking's input must be cast this turn.
                stocks: [("copper-ore", 10_000_000), ("tin-ore", 10_000_000),
                         ("timber", 10_000_000), ("clay", 10_000_000), ("fiber", 10_000_000)]);
            w.Variables.Add(new VariableRow(S0, Variables.ArtisanShare, 0.5)); // all recipes available
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = exec.Step(w);
            tools[d] = Stock(w, cfg, "tools");
        }

        Assert.True(tools[0] > 0,
            "no tools — the chain never fired, so this test proves nothing about ordering");
        for (int d = 1; d < dts.Length; d++)
        {
            // EXACT equality, not a tolerance: the chain either carries within
            // the turn at every step size or it does not.
            Assert.Equal(tools[0], tools[d]);
        }
    }

    [Fact]
    public void Crafting_ContestedScarceInput_ConservesExactlyAtEveryDt()
    {
        // timber is an input to pottery-firing, bronze-casting AND toolmaking.
        // When it is scarce and nothing replenishes it, WHICH recipe gets it
        // depends on dt: each recipe's labor appetite scales with dt while a
        // stock level does not, so at coarse dt the first claimant in registry
        // order takes more of it. Measured, 10 sim-years, 4 adults, timber 60:
        //
        //   dt   pottery  tools
        //   10     120      0
        //    5     100      0
        //    2.5    84     12
        //    1      76     14
        //    0.5    70     16
        //
        // That spread is a COMPOSITION difference, and this test deliberately
        // does NOT assert it away. "Which recipe gets the scarce input" is the
        // allocation question relative prices answer, and prices arrive at T3.4;
        // until then the flat-price special case has to break the tie somehow,
        // and registry order is a deterministic way to do it. Law 3 asks that
        // every RATE be per-sim-year and integrated with dtYears, which Craft
        // does; it does not ask that a Leontief min() of a rate×dt against an
        // inventory LEVEL scale on both sides — scaling the level by dt would
        // authorise consuming ten times the stock that exists at dt = 10, which
        // is a law 1 break dressed as a law 3 fix.
        //
        // What IS invariant, and what this test pins: the QUANTITY conserved.
        // Every unit of the scarce input is accounted for at every dt, exactly.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 10;
        const long timberEndowed = 60;
        double[] dts = [10.0, 5.0, 2.5, 1.0, 0.5];
        var consumed = new long[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            var exec = new TurnExecutor(FlatEra(dts[d]), [SystemCatalog.Production(cfg)]);
            WorldState w = World(cfg, adults: 4,
                allocation: new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 1.0, 0.0),
                stocks: [("timber", timberEndowed), ("clay", 10_000_000), ("fiber", 10_000_000),
                         ("copper-ore", 10_000_000), ("tin-ore", 10_000_000)]);
            w.Variables.Add(new VariableRow(S0, Variables.ArtisanShare, 0.5));
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = exec.Step(w);

            long left = Stock(w, cfg, "timber");
            long used = FlowOf(w, cfg, "timber", ReasonIds.InputsConsumed, sunk: true);
            // Law 1, exact, no epsilon — at EVERY integration step.
            Assert.Equal(timberEndowed, left + used);
            consumed[d] = used;
        }

        // The scarce input is fully drawn down at every dt: the quantity that
        // moves does not depend on the step size, only its composition does.
        for (int d = 0; d < dts.Length; d++)
            Assert.Equal(timberEndowed, consumed[d]);
    }

    [Fact]
    public void ToolWear_DtSensitivity_IsBoundedAndByDesign_NotAccidental()
    {
        // T3.3 adversarial finding: Production_DtCorrect endows NO tools, so
        // equipRatio is 0, the whole wear block is skipped, and the dt test
        // covered neither mechanism this packet added. Crafting is now covered
        // and dt-INVARIANT (see Crafting_WithAContestedScarceInput_IsDtInvariant).
        // TOOL WEAR IS DIFFERENT AND IS NOT WIDENED INTO THE ±1 CLAIM.
        //
        // Wear is rate × equipped-farmers × dtYears, a per-sim-year rate
        // integrated with dtYears exactly as law 3 prescribes. In the
        // stock-limited branch that reduces to a first-order step on a decay
        // whose own state is the integrand, so cumulative wear IS dt-sensitive.
        // A reviewer proposed replacing it with exp(); that was REFUTED and the
        // refutation is the reason this test pins rather than fixes: exp()
        // embeds a MEMORYLESS lifetime, contradicting the TUNE doc's declared
        // finite "a tool set lasts ~10 working years" and leaving an immortal
        // tail. Deciding properly needs tool VINTAGE state, which m3-spec §2
        // puts out of milestone scope ("vintaged capital"). Queued there.
        //
        // What this test DOES guarantee, so the sensitivity can never silently
        // grow: the coefficient rate/toolsPerFarmer × dt is ≤ 1 for EVERY band
        // in era-pacing.json, so the step never overshoots, stock never goes
        // negative, and finer dt only ever leaves MORE stock (monotone).
        SimConfig cfg = TestConfigs.Sim();
        double coefficientAtCoarsestDt =
            cfg.Production.ToolWearPerEquippedFarmerPerYear
            / cfg.Production.ToolsPerFarmerToEquip * 10.0;   // 10 = coarsest band (Neolithic)
        Assert.True(coefficientAtCoarsestDt <= 1.0,
            $"tool-wear decay coefficient {coefficientAtCoarsestDt} exceeds 1 at the coarsest dt — "
            + "the step now overshoots and the clamp is load-bearing rather than belt-and-braces. "
            + "Re-derive the wear rate or move to a vintaged-capital model.");

        const int horizonYears = 20;
        double[] dts = [10.0, 5.0, 2.5];
        var toolsLeft = new long[dts.Length];
        for (int d = 0; d < dts.Length; d++)
        {
            var exec = new TurnExecutor(FlatEra(dts[d]), [SystemCatalog.Production(cfg)]);
            WorldState w = World(cfg, adults: 1000,
                allocation: new SectorAllocationRow(S0, 1.0, 0.0, 0.0, 0.0, 0.0),
                arableKm2: 1e9, stocks: [("tools", 1000)]);
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = exec.Step(w);
            toolsLeft[d] = Stock(w, cfg, "tools");
        }

        // Never negative, never above the endowment, and MONOTONE in dt: finer
        // steps retain at least as much stock. A regression that made wear
        // non-monotone or unbounded fails here loudly.
        for (int d = 0; d < dts.Length; d++)
            Assert.InRange(toolsLeft[d], 0, 1000);
        for (int d = 1; d < dts.Length; d++)
            Assert.True(toolsLeft[d] >= toolsLeft[d - 1],
                $"tool wear is not monotone in dt: dt {dts[d - 1]} left {toolsLeft[d - 1]}, "
                + $"dt {dts[d]} left {toolsLeft[d]}");
        // And the sensitivity is REAL, so this test is not vacuous.
        Assert.True(toolsLeft[^1] > toolsLeft[0],
            "tool wear shows no dt sensitivity at all — either it became invariant (delete this "
            + "test and fold tools into the ±1 dt ladder) or the wear block stopped running");
    }

    // --- 3. the demolition, asserted so it cannot rot -------------------------

    [Fact]
    public void ScaffoldingDemolition_NoReferencesRemain()
    {
        // m3 spec §1 acceptance: "zero references remain to the deleted
        // multiplier (grep-level)". Asserted here rather than left to a
        // one-time grep, so a revert or a bad merge fails the build.
        //
        // TWO HOLES IN THE FIRST VERSION OF THIS TEST, both found by the T3.3
        // adversarial pass by CONSTRUCTING a surviving-scaffold scenario and
        // watching the gate report success. Recorded because they are the
        // difference between a gate and a decoration:
        //   (1) it skipped sim.json ENTIRELY — the one file all three retired
        //       parameters actually lived in. Re-inserting the keys under
        //       "farming" restored the scaffolding on the config side and the
        //       gate still passed. Now sim.json is SCANNED, and only the
        //       documentation VALUE is exempt: a retired name is a violation
        //       when it appears as a live JSON KEY ("token":), which is exactly
        //       how SimConfig binds it, and harmless inside a _doc string.
        //   (2) it exempted any line whose first character is '*', meaning to
        //       skip block-comment continuations. The repo contains NO /* */
        //       blocks, but its house style wraps multiplications with '*' at
        //       the start of the continuation line — so the rule whitelisted
        //       LIVE CODE. A compiling statement carrying a retired token on
        //       such a line passed the gate. Comments are now stripped
        //       properly instead of guessed at by prefix.
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
                string ext = Path.GetExtension(file);
                if (ext is not (".cs" or ".json")) continue;
                // This file names the retired tokens to test for them.
                if (file.EndsWith("ProductionTests.cs", StringComparison.Ordinal)) continue;

                string text = File.ReadAllText(file);
                string live = ext == ".cs" ? StripCsComments(text) : text;
                foreach (string token in retired)
                {
                    bool hit = ext == ".json"
                        // A LIVE config key is `"token":` — that is what binds.
                        // A mention inside a _doc value is the demolition record.
                        ? live.Contains($"\"{token}\"", StringComparison.Ordinal)
                          && System.Text.RegularExpressions.Regex.IsMatch(
                              live, "\"" + System.Text.RegularExpressions.Regex.Escape(token) + "\"\\s*:")
                        : live.Contains(token, StringComparison.Ordinal);
                    if (hit) offenders.Add($"{Path.GetRelativePath(root, file)}: {token}");
                }
            }
        }
        Assert.True(offenders.Count == 0,
            "M2 scaffolding survives the T3.3 demolition:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Blanks C# comments so the demolition scan sees only LIVE code. Handles
    /// // to end-of-line and /* */ spans, and skips string literals so a '//'
    /// inside a string does not blind the rest of the line. Deliberately not a
    /// prefix heuristic: guessing "this line starts with * so it is a comment"
    /// is what let live code through the first time.
    /// </summary>
    private static string StripCsComments(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool inLine = false, inBlock = false, inString = false, inVerbatim = false, inChar = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
            if (inBlock) { if (c == '*' && next == '/') { inBlock = false; i++; } continue; }
            if (inString)
            {
                sb.Append(c);
                if (!inVerbatim && c == '\\') { if (i + 1 < text.Length) { sb.Append(next); i++; } continue; }
                if (c == '"') { inString = false; inVerbatim = false; }
                continue;
            }
            if (inChar)
            {
                sb.Append(c);
                if (c == '\\') { if (i + 1 < text.Length) { sb.Append(next); i++; } continue; }
                if (c == '\'') inChar = false;
                continue;
            }
            if (c == '/' && next == '/') { inLine = true; continue; }
            if (c == '/' && next == '*') { inBlock = true; i++; continue; }
            if (c == '"') { inString = true; inVerbatim = i > 0 && text[i - 1] == '@'; sb.Append(c); continue; }
            if (c == '\'') { inChar = true; sb.Append(c); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
