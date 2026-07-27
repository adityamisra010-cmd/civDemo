using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Price;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.4 acceptance (D-033). The stated criteria, each with teeth:
///   1. prices bounded within the clamps ALWAYS;
///   2. grain pinned at 1;
///   3. a scarcity shock raises the right price and relaxes when it clears;
///   4. EXPLAINABILITY — a price change decomposes into its contributing
///      terms. The sum check is necessary and NOT sufficient (it reconciles by
///      construction); the per-term SENSITIVITY tests are the ones with teeth,
///      and there is one per term (director ruling, docs/t3.4-lens-manifest.md);
///   5. no global solve — structurally, one damped step per good per turn;
///   6. dt-correctness (law 3).
/// The 500-turn soak and its oscillation detector live in PriceSoakTests.
///
/// COVERAGE WARNING (director ruling, 2026-07-26 — until T3.11 lands the driven
/// golden). THE GOLDENS DO NOT COVER PRICE BEHAVIOUR AT ALL. Every pinned world
/// runs the all-farming default, so no good but grain ever flows, every price
/// sits at exactly 1.0, and the step's exponent is exactly 0 — ADR-016 changed
/// the solver's mathematics from Euler to exact integration and left all three
/// golden hashes BYTE-IDENTICAL. Price behaviour is therefore covered by THIS
/// file and the driven soak alone. Do not read a green golden as evidence that
/// the price solver is unchanged.
/// </summary>
public class PriceTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static readonly SettlementId S0 = new(0);

    /// <summary>A hand-built world carrying the four measured quantities the
    /// solver reads, so each can be perturbed INDEPENDENTLY — which is exactly
    /// what the per-term sensitivity tests require.</summary>
    private static WorldState World(
        SimConfig cfg,
        (string Good, long Stock, long Produced, long InputDemand, long ConsumptionDemand)[] goods,
        (string Good, double Price)[]? prices = null)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;
        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            long stock = 0, produced = 0, inputDemand = 0, consumptionDemand = 0;
            foreach ((string name, long st, long pr, long id, long cd) in goods)
            {
                if (name != g.Name) continue;
                stock = st; produced = pr; inputDemand = id; consumptionDemand = cd;
            }
            var row = new GoodStockRow(
                S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0,
                lastProducedUnits: produced,
                lastInputDemandUnits: inputDemand,
                lastConsumptionDemandUnits: consumptionDemand);
            if (stock > 0)
            {
                var ledger = new Ledger(world.LedgerFlows);
                int idx = world.GoodStocks.Add(row);
                ledger.Flow(ref world.GoodStocks.Ref(idx).Amount,
                    ConservedQuantityIds.OfGood(new GoodId(g.Id)), ReasonIds.InitialEndowment,
                    stock, FlowDirection.Source, OverdrawPolicy.Throw);
            }
            else world.GoodStocks.Add(row);

            if (prices is not null)
                foreach ((string name, double p) in prices)
                    if (name == g.Name)
                        world.Prices.Add(new PriceRow(S0, new GoodId(g.Id), p));
        }
        return world;
    }

    private static WorldState Step(SimConfig cfg, WorldState w, double dt = 10.0) =>
        new TurnExecutor(FlatEra(dt), [SystemCatalog.Price(cfg)]).Step(w);

    private static double Price(WorldState w, SimConfig cfg, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.Prices.Count; i++)
            if (w.Prices[i].Good.Value == id) return w.Prices[i].Price;
        return double.NaN;
    }

    private static PriceTermRow Terms(WorldState w, SimConfig cfg, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.PriceTerms.Count; i++)
            if (w.PriceTerms[i].Good.Value == id) return w.PriceTerms[i];
        throw new InvalidOperationException($"no price-term row for {good}");
    }

    /// <summary>
    /// THE BAND-EDGE VACUITY GUARD. Every confirmed T3.4 test-power finding had
    /// one shape: a long horizon runs the price onto a clamp, and the assertion
    /// then compares two clamp CONSTANTS. 0.05 == 0.05 is true no matter what
    /// the solver does, so the test passes while asserting nothing.
    ///
    /// Call this at every assertion point that compares prices. It fails loudly
    /// if the value under comparison is resting on a band edge, so a retune or a
    /// later demand-side packet that moves where prices settle re-surfaces the
    /// problem automatically instead of silently re-draining the assertions.
    /// </summary>
    private static void AssertOffBandEdges(SimConfig cfg, double price, string what)
    {
        Assert.True(price > cfg.Price.BandMin * 1.001 && price < cfg.Price.BandMax * 0.999,
            $"VACUOUS ASSERTION: {what} is resting on a band edge ({price}). "
            + "Comparing clamp constants proves nothing about the solver.");
    }

    // --- 2. grain is the numeraire --------------------------------------------

    [Fact]
    public void Grain_IsPinnedAtExactlyOne_ForeverAndUnderAnyPressure()
    {
        // Not "initialised to" 1.0 — PINNED. Grain carries wild excess demand
        // here; if the solver stepped it at all, 200 turns of compounding would
        // show. Exact equality, no epsilon: the unit of account either is 1 or
        // the whole price system is denominated in something that drifts.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = World(cfg,
            [("grain", 10, 0, 5_000, 50_000), ("pottery", 100, 10, 0, 0)]);

        for (int t = 0; t < 200; t++) w = Step(cfg, w);

        Assert.Equal(1.0, Price(w, cfg, "grain"));
        PriceTermRow g = Terms(w, cfg, "grain");
        Assert.Equal(0.0, g.Delta);
        Assert.Equal(0.0, g.Consumption);
        Assert.Equal(0.0, g.InputDemand);
        // The decomposition must report the pin too, not a stale or invented
        // previous price: PrevPrice + Delta == the written price, on grain as
        // on everything else.
        Assert.Equal(1.0, g.PrevPrice);
        Assert.Equal(Price(w, cfg, "grain"), g.PrevPrice + g.Delta);
    }

    [Fact]
    public void Grain_IsPinned_NotMerelyInitialised_ACorruptedPriceIsCorrected()
    {
        // PINNED means WRITTEN EVERY TURN. An implementation that only seeds
        // 1.0 when no row exists passes every "grain == 1" test on a clean run
        // and silently accepts a corrupted or loaded price forever. Seed a bad
        // grain price and require the solver to overwrite it on the FIRST step.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = World(cfg,
            [("grain", 10, 0, 5_000, 50_000)],
            prices: [("grain", 17.5)]);
        Assert.Equal(17.5, Price(w, cfg, "grain")); // the corruption is really there

        WorldState next = Step(cfg, w);
        Assert.Equal(1.0, Price(next, cfg, "grain"));
        for (int t = 0; t < 50; t++) next = Step(cfg, next);
        Assert.Equal(1.0, Price(next, cfg, "grain"));
    }

    // --- 3. the price responds in the right direction -------------------------

    [Fact]
    public void ExcessDemand_RaisesPrice_AndSurplus_LowersIt()
    {
        SimConfig cfg = TestConfigs.Sim();

        // Scarce: demand far above production, nothing in store.
        WorldState scarce = Step(cfg, World(cfg,
            [("pottery", 0, 1, 0, 500)]));
        Assert.True(Price(scarce, cfg, "pottery") > 1.0,
            $"scarcity did not raise the price (got {Price(scarce, cfg, "pottery")})");

        // Glut: heavy production and a full warehouse, no demand at all.
        WorldState glut = Step(cfg, World(cfg,
            [("pottery", 10_000, 5_000, 0, 0)]));
        Assert.True(Price(glut, cfg, "pottery") < 1.0,
            $"a glut did not lower the price (got {Price(glut, cfg, "pottery")})");
    }

    [Fact]
    public void ScarcityShock_RaisesTheRightPrice_ThenRelaxesWhenItClears()
    {
        // The stated acceptance criterion, both halves. Only pottery is shocked;
        // cloth is the control and must not move, which is what makes this a
        // test of the RIGHT price rather than of all prices.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = World(cfg,
            // Cloth is the CONTROL and its fixture is chosen so it settles in
            // the band INTERIOR: supply and demand balanced, and no stock to
            // release (a stock of 100 releases 500/turn at dt=10, swamping both
            // flows and pinning cloth to the floor — which is what made the
            // original version of this assertion compare 0.05 to 0.05).
            [("pottery", 0, 1, 0, 500), ("cloth", 0, 100, 0, 100)]);

        for (int t = 0; t < 20; t++) w = Step(cfg, w);
        double shocked = Price(w, cfg, "pottery");
        double control = Price(w, cfg, "cloth");
        Assert.True(shocked > 1.5, $"20 turns of severe scarcity only reached {shocked}");
        // The control must be a live price, not a clamp constant: comparing
        // BandMin to BandMin later would assert nothing about "the RIGHT price".
        AssertOffBandEdges(cfg, control, "the control good (cloth)");

        // The shock clears: production catches up and a stock accumulates.
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Good.Value != cfg.Goods!.IdOf("pottery")) continue;
            ref GoodStockRow row = ref w.GoodStocks.Ref(i);
            row.LastConsumptionDemandUnits = 100;
            row.LastProducedUnits = 400;
        }
        WorldState relaxed = w;
        for (int t = 0; t < 40; t++) relaxed = Step(cfg, relaxed);

        Assert.True(Price(relaxed, cfg, "pottery") < shocked,
            $"price did not relax when the shortage cleared: {shocked} → {Price(relaxed, cfg, "pottery")}");
        // The control good never moved with it.
        Assert.Equal(control, Price(relaxed, cfg, "cloth"), 9);
    }

    // --- 1. the clamps hold ---------------------------------------------------

    [Fact]
    public void Price_NeverLeavesTheBand_UnderExtremePressureInEitherDirection()
    {
        SimConfig cfg = TestConfigs.Sim();

        WorldState up = World(cfg, [("pottery", 0, 0, 1_000_000_000, 1_000_000_000)]);
        for (int t = 0; t < 500; t++) up = Step(cfg, up);
        Assert.InRange(Price(up, cfg, "pottery"), cfg.Price.BandMin, cfg.Price.BandMax);

        WorldState down = World(cfg, [("pottery", 1_000_000_000, 1_000_000_000, 0, 0)]);
        for (int t = 0; t < 500; t++) down = Step(cfg, down);
        Assert.InRange(Price(down, cfg, "pottery"), cfg.Price.BandMin, cfg.Price.BandMax);
    }

    [Fact]
    public void PriceStep_NeverExceedsTheMaxRelativeChange_ScaledByDt()
    {
        // The per-step rail, and the reason it is a per-YEAR rate: the same
        // sim-year horizon must respect the same bound at every dt, which a
        // literal per-turn constant cannot do (law 3).
        SimConfig cfg = TestConfigs.Sim();
        foreach (double dt in new[] { 10.0, 5.0, 1.0 })
        {
            WorldState w = World(cfg, [("pottery", 0, 0, 1_000_000, 1_000_000)]);
            w = Step(cfg, w, dt); // establish the price rows before measuring steps
            for (int t = 0; t < 30; t++)
            {
                double before = Price(w, cfg, "pottery");
                w = Step(cfg, w, dt);
                double after = Price(w, cfg, "pottery");
                double bound = cfg.Price.MaxRelativeChangePerYear * before * dt;
                Assert.True(Math.Abs(after - before) <= bound + 1e-9,
                    $"dt={dt}: step {before}→{after} exceeded the rail {bound}");
            }
        }
    }

    [Fact]
    public void Rail_BindsOnAModestExcess_WhereTheBandCannotSaturateIt()
    {
        // The rail could be DELETED from the shipped source with all 333 tests
        // green: the existing rail test drives such an enormous excess that the
        // BAND clamps the price anyway, so removing the rail changes nothing it
        // measures. This fixture is deliberately modest — the price never
        // approaches an edge, so the rail is the only thing bounding the step.
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        WorldState w = World(cfg, [("pottery", 0, 1, 0, 500)]);

        bool railEverBound = false;
        for (int t = 0; t < 6; t++)
        {
            double before = t == 0 ? 1.0 : Price(w, cfg, "pottery");
            w = Step(cfg, w, dt);
            double after = Price(w, cfg, "pottery");
            AssertOffBandEdges(cfg, after, $"pottery at turn {t}");

            double bound = cfg.Price.MaxRelativeChangePerYear * before * dt;
            Assert.True(Math.Abs(after - before) <= bound + 1e-12,
                $"turn {t}: step {before} → {after} exceeded the rail {bound}");
            if (Math.Abs(Math.Abs(after - before) - bound) < 1e-9) railEverBound = true;
        }

        // NON-VACUITY: the rail must actually BIND somewhere in this run, or
        // the bound above is satisfied trivially and deleting the rail again
        // goes unnoticed.
        Assert.True(railEverBound,
            "the rail never bound — this fixture cannot detect a deleted rail");
    }

    [Fact]
    public void Solver_ReadsPrevQuantities_NotThisTurns_OneTurnLagHolds()
    {
        // §3.2. If the solver read the CURRENT turn's stocks it would be
        // partially solving within the turn. Under the full pipeline Production
        // and Consumption rewrite the demand signals in Next BEFORE Price runs,
        // so a seeded PREV signal that moves turn-1's price can only have come
        // from Prev.
        SimConfig cfg = TestConfigs.Sim();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(FlatEra(10.0),
            PipelineLoader.Load(pipe, SystemCatalog.All(cfg)));

        WorldState seeded = World(cfg, [("pottery", 0, 1, 0, 5_000)]);
        seeded.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        WorldState control = World(cfg, [("pottery", 0, 1, 0, 0)]);
        control.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));

        WorldState a = exec.Step(seeded), b = exec.Step(control);
        Assert.True(Price(a, cfg, "pottery") > Price(b, cfg, "pottery"),
            "the PREV-seeded demand signal did not reach turn 1 — the solver is reading "
            + "this turn's rewritten stocks instead of Prev");
    }

    // --- 4. EXPLAINABILITY ----------------------------------------------------

    [Fact]
    public void Decomposition_TermsSumToTheAppliedDelta_AndDeltaMovesThePrice()
    {
        // NECESSARY, NOT SUFFICIENT. This reconciles by construction — the
        // clamp term is defined as (delta − raw), so the sum closes whatever
        // the other terms say. It is here to catch a term dropped from the row
        // or a price written that the decomposition never saw; it is NOT the
        // evidence that the attribution is right. That is the sensitivity
        // tests below (director ruling: "a decomposition that recomputes the
        // price change from the same expression proves nothing").
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = World(cfg, [("pottery", 400, 120, 90, 260), ("cloth", 50, 5, 0, 400)]);
        WorldState next = Step(cfg, w);

        foreach (string good in new[] { "pottery", "cloth" })
        {
            PriceTermRow t = Terms(next, cfg, good);
            double sum = t.Consumption + t.InputDemand + t.Production + t.StockRelease + t.Clamp;
            Assert.Equal(t.Delta, sum, 12);
            Assert.Equal(Price(next, cfg, good), t.PrevPrice + t.Delta, 12);
        }
    }

    /// <summary>Steps one world twice: baseline, and with ONE measured input
    /// perturbed. Returns both decompositions.</summary>
    private static (PriceTermRow Base, PriceTermRow Perturbed) Perturb(
        SimConfig cfg, string good,
        (string, long, long, long, long)[] baseline,
        (string, long, long, long, long)[] perturbed)
        => (Terms(Step(cfg, World(cfg, baseline)), cfg, good),
            Terms(Step(cfg, World(cfg, perturbed)), cfg, good));

    [Fact]
    public void Sensitivity_ConsumptionDemand_MovesOnlyTheConsumptionTerm()
    {
        // THE TEST THAT BITES. Perturb consumption demand alone; the
        // consumption term must move and the other three must hold EXACTLY.
        // Attribute this input to the wrong term and this fails.
        SimConfig cfg = TestConfigs.Sim();
        var (b, p) = Perturb(cfg, "pottery",
            [("pottery", 200, 100, 50, 100)],
            [("pottery", 200, 100, 50, 900)]);

        Assert.True(p.Consumption > b.Consumption,
            $"consumption term did not respond to consumption demand ({b.Consumption} → {p.Consumption})");

        // WHY NOT "the others hold EXACTLY". Under exact integration (ADR-016)
        // the change is MULTIPLICATIVE, and apportioning it among additive
        // causes uses each term's share of the exponent. Moving one input moves
        // the exponent, so every term rescales by the same common factor — the
        // others do not hold constant, and asserting they do would be asserting
        // something the mathematics forbids.
        //
        // What IS invariant, and is just as discriminating: the untouched terms
        // rescale TOGETHER, so their RATIOS to one another are unchanged. A
        // mislabelled attribution breaks that immediately, because the
        // perturbed input would then be feeding one of these terms.
        Assert.Equal(b.Production / b.StockRelease, p.Production / p.StockRelease, 12);
        Assert.Equal(b.Production / b.InputDemand, p.Production / p.InputDemand, 12);
    }

    [Fact]
    public void Sensitivity_InputDemand_MovesOnlyTheInputDemandTerm()
    {
        SimConfig cfg = TestConfigs.Sim();
        var (b, p) = Perturb(cfg, "pottery",
            [("pottery", 200, 100, 50, 100)],
            [("pottery", 200, 100, 900, 100)]);

        Assert.True(p.InputDemand > b.InputDemand,
            $"input-demand term did not respond to input demand ({b.InputDemand} → {p.InputDemand})");
        // Same reasoning as the consumption case: the untouched terms rescale
        // together under the shared exponent, so their ratios are the invariant
        // with teeth (see that test for the full argument).
        Assert.Equal(b.Production / b.StockRelease, p.Production / p.StockRelease, 12);
        Assert.Equal(b.Production / b.Consumption, p.Production / p.Consumption, 12);
    }

    [Fact]
    public void Sensitivity_Production_MovesTheProductionTerm_AndPushesPriceDown()
    {
        // Production enters with a NEGATIVE sign and also enlarges the market
        // scale, so the other terms legitimately rescale with it. Asserting
        // they hold constant would be wrong; what must be true is that the
        // production term moves DOWN and the price follows.
        SimConfig cfg = TestConfigs.Sim();
        var (b, p) = Perturb(cfg, "pottery",
            [("pottery", 20, 100, 50, 100)],
            [("pottery", 20, 400, 50, 100)]);

        Assert.True(p.Production < b.Production,
            $"production term did not respond to production ({b.Production} → {p.Production})");
        Assert.True(p.Production < 0.0, "production must push the price DOWN");
        Assert.True(p.Delta < b.Delta, "more production did not lower the price change");
    }

    [Fact]
    public void Sensitivity_StockRelease_MovesTheStockReleaseTerm_AndDampsASpike()
    {
        // The warehouse term: a full store damps a shortage. Perturb the STOCK
        // alone, with demand held high, and the stock-release term must move
        // down and the price rise less.
        SimConfig cfg = TestConfigs.Sim();
        var (b, p) = Perturb(cfg, "pottery",
            [("pottery", 0, 10, 0, 900)],
            [("pottery", 5_000, 10, 0, 900)]);

        Assert.True(p.StockRelease < b.StockRelease,
            $"stock-release term did not respond to stock ({b.StockRelease} → {p.StockRelease})");
        Assert.True(p.Delta < b.Delta,
            "a full warehouse did not damp the price spike — the stock term is not doing its job");
    }

    [Fact]
    public void Decomposition_IsWrittenFresh_EveryTurn_NeverStale()
    {
        // T3.3 precedent: an observational row that is not rewritten every turn
        // reads as a live measurement of a turn that never happened.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Step(cfg, World(cfg, [("pottery", 0, 0, 0, 900)]));
        int firstCount = w.PriceTerms.Count;
        Assert.True(firstCount > 0);

        WorldState next = Step(cfg, w);
        Assert.Equal(firstCount, next.PriceTerms.Count); // cleared and rewritten, never appended
    }

    // --- 6. dt-correctness ----------------------------------------------------

    [Fact]
    public void Price_DtInvariant_EqualHorizonAgreesAcrossDt()
    {
        // ADR-016 (directed amendment to D-033): the step is the CLOSED FORM
        // p *= exp(lambda * (excess/scale) * dt), not Euler. Price compounds —
        // the integrated quantity is on the right-hand side — so Euler
        // under-integrated with a residue that grew with dt. Measured on the
        // Euler implementation, same fixture, 100 sim-years:
        //
        //   dt      10       5      2.5      1
        //   Euler  7.439   8.225   8.694   9.006     (21% spread)
        //
        // Exact integration collapses that to floating-point noise. Measured
        // on this implementation, same fixture and horizon:
        //
        //   dt 10   9.227814352139522
        //   dt 5    9.227814352139527
        //   dt 2.5  9.227814352139545
        //   dt 1    9.227814352139534     relative spread 5.8e-16
        //
        // Fifteen significant figures, and 9.2278 is exactly the limit the
        // Euler sequence was climbing toward from below. This test replaces the
        // monotone-convergence pin it used to carry: the residue is not bounded
        // any more, it is GONE, and the tolerance says so — 1e-12 is IEEE
        // summation noise across different step counts, not a modelling slack.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 100;
        double[] dts = [10.0, 5.0, 2.5, 1.0];
        var landed = new double[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            // Production and demand are PER-TURN INTEGRALS in the real sim and
            // scale with dt — that is why excess/scale is dimensionless.
            double f = dts[d] / 10.0;
            WorldState w = World(cfg,
                [("pottery", 10, (long)(40 * f), (long)(20 * f), (long)(120 * f))]);
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = Step(cfg, w, dts[d]);
            landed[d] = Price(w, cfg, "pottery");
        }

        Assert.True(landed[0] > 2.0,
            $"the dt test is vacuous — price barely moved (landed {landed[0]})");
        AssertOffBandEdges(cfg, landed[0], "the dt-invariance landing price");

        // TIGHT: relative spread across the whole shipped dt range. The rail is
        // linear in dt and would reintroduce a spread if it bound, so this
        // fixture is chosen to stay under it; the rail's own behaviour is
        // pinned by Rail_BindsOnAModestExcess.
        for (int d = 1; d < dts.Length; d++)
        {
            double rel = Math.Abs(landed[d] - landed[0]) / landed[0];
            Assert.True(rel < 1e-12,
                $"price not dt-invariant under exact integration: dt={dts[0]} -> {landed[0]}, "
                + $"dt={dts[d]} -> {landed[d]} (relative {rel})");
        }
    }

    [Fact]
    public void Price_TrajectoryDoesNotChangeSpeed_AcrossAnEraDtFlip()
    {
        // ERA-BOUNDARY CONTINUITY, in the family of ADR-011's continuity test.
        // The era table shrinks dt across the campaign (Neolithic 10 -> Bronze
        // 5 -> later 2.5). If the integration were dt-sensitive, a price would
        // visibly change SPEED at the boundary for a reason having nothing to
        // do with the simulated world — the campaign-scale form of the same
        // defect the dt test catches at a point.
        //
        // Compare a run that crosses a 10 -> 5 flip mid-horizon against runs
        // held at each dt throughout. Under exact integration all three reach
        // the same place over the same sim-years.
        SimConfig cfg = TestConfigs.Sim();
        var flip = EraTableLoader.Load(
            "{ \"bands\": [ { \"name\": \"neolithic\", \"startYear\": 0, \"endYear\": 50, \"dtYears\": 10 }, "
            + "{ \"name\": \"bronze\", \"startYear\": 50, \"endYear\": 100000, \"dtYears\": 5 } ] }");

        static WorldState Fixture(SimConfig c, double f) => World(c,
            [("pottery", 10, (long)(40 * f), (long)(20 * f), (long)(120 * f))]);

        // Crossing run: 5 turns at dt=10 (to year 50), then 10 turns at dt=5.
        // The fixture's flows must track the band's dt, exactly as real
        // production would.
        var crossing = new TurnExecutor(flip, [SystemCatalog.Price(cfg)]);
        WorldState w = Fixture(cfg, 1.0);
        for (int t = 0; t < 5; t++) w = crossing.Step(w);
        double atFlip = Price(w, cfg, "pottery");
        // Re-fixture the flows for the new band, then continue to year 100.
        WorldState after = Fixture(cfg, 0.5);
        after.Prices.Add(new PriceRow(S0, new GoodId(cfg.Goods!.IdOf("pottery")), atFlip));
        var bronze = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Price(cfg)]);
        for (int t = 0; t < 10; t++) after = bronze.Step(after);
        double crossed = Price(after, cfg, "pottery");

        // Held-at-one-dt controls over the same 100 sim-years.
        WorldState held10 = Fixture(cfg, 1.0);
        for (int t = 0; t < 10; t++) held10 = Step(cfg, held10, 10.0);

        AssertOffBandEdges(cfg, crossed, "the era-crossing price");
        double heldPrice = Price(held10, cfg, "pottery");
        double rel = Math.Abs(crossed - heldPrice) / crossed;
        Assert.True(rel < 0.001,
            $"trajectory changed speed across the era dt flip: crossing run {crossed}, "
            + $"held-at-10 run {heldPrice} (relative {rel})");
    }

    // --- 5. no global solve ---------------------------------------------------

    [Fact]
    public void Settlements_ArePricedIndependently_NoCrossSettlementCoupling()
    {
        // The D-033 mandate, tested behaviourally rather than by inspection: a
        // violent shortage in settlement 1 must not move settlement 0's price
        // by even one ulp. Any global solve — any coupling at all — fails this.
        SimConfig cfg = TestConfigs.Sim();

        WorldState alone = World(cfg, [("pottery", 100, 10, 0, 200)]);
        WorldState paired = World(cfg, [("pottery", 100, 10, 0, 200)]);
        var s1 = new SettlementId(1);
        paired.Settlements.Add(new SettlementRow(s1, SiteCell: 1, FoundedTurn: 0));
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            paired.GoodStocks.Add(new GoodStockRow(
                s1, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0,
                lastProducedUnits: 0, lastInputDemandUnits: 1_000_000,
                lastConsumptionDemandUnits: 1_000_000));
        }

        // SHORT horizon, deliberately: at 50 turns both prices sit on the band
        // ceiling and the assertion below degenerates to 20.0 == 20.0. The
        // guard makes that impossible to reintroduce silently.
        for (int t = 0; t < 8; t++) { alone = Step(cfg, alone); paired = Step(cfg, paired); }

        double isolated = Price(alone, cfg, "pottery");
        double withNeighbour = double.NaN;
        for (int i = 0; i < paired.Prices.Count; i++)
            if (paired.Prices[i].Settlement == S0 && paired.Prices[i].Good.Value == cfg.Goods!.IdOf("pottery"))
                withNeighbour = paired.Prices[i].Price;

        AssertOffBandEdges(cfg, isolated, "the isolated settlement's pottery price");
        // EXACT equality — not "close". Coupling of any strength fails here.
        Assert.Equal(isolated, withNeighbour);

        // COVERAGE: settlement 1 must actually have been priced. Without this a
        // solver that prices only settlement 0 passes the equality above
        // trivially — 75% of the price table can vanish and nothing notices.
        double neighbourOwn = double.NaN;
        for (int i = 0; i < paired.Prices.Count; i++)
            if (paired.Prices[i].Settlement == s1 && paired.Prices[i].Good.Value == cfg.Goods!.IdOf("pottery"))
                neighbourOwn = paired.Prices[i].Price;
        Assert.False(double.IsNaN(neighbourOwn), "settlement 1 was never priced at all");
        Assert.True(neighbourOwn > 1.0,
            $"settlement 1 carries a 1,000,000-unit shortage and its price is {neighbourOwn}");
    }

    [Fact]
    public void Goods_ArePricedIndependently_NoSimultaneousSystemOverGoods()
    {
        // D-033's "no simultaneous system over goods", which until now was
        // covered by nothing but a golden hash — and CLAUDE.md disqualifies a
        // golden-only kill. Two worlds differing ONLY in OTHER goods'
        // quantities must price the good under test bit-identically.
        SimConfig cfg = TestConfigs.Sim();
        WorldState a = World(cfg, [("pottery", 200, 100, 50, 300)]);
        WorldState b = World(cfg,
            [("pottery", 200, 100, 50, 300), ("cloth", 0, 0, 0, 1_000_000),
             ("stone", 10_000_000, 10_000_000, 0, 0)]);

        for (int t = 0; t < 8; t++) { a = Step(cfg, a); b = Step(cfg, b); }

        double pa = Price(a, cfg, "pottery"), pb = Price(b, cfg, "pottery");
        AssertOffBandEdges(cfg, pa, "pottery in the control world");
        Assert.Equal(pa, pb);
        // Non-vacuity: the other goods really did move, so the invariance above
        // is a statement about independence and not about a dead fixture.
        Assert.True(Price(b, cfg, "cloth") > 1.0, "the cloth shock never happened");
    }
}
