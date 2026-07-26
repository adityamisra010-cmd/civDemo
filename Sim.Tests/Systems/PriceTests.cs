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
            [("pottery", 0, 1, 0, 500), ("cloth", 100, 100, 0, 100)]);

        for (int t = 0; t < 20; t++) w = Step(cfg, w);
        double shocked = Price(w, cfg, "pottery");
        double control = Price(w, cfg, "cloth");
        Assert.True(shocked > 1.5, $"20 turns of severe scarcity only reached {shocked}");

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
        Assert.Equal(b.InputDemand, p.InputDemand);
        Assert.Equal(b.Production, p.Production);
        Assert.Equal(b.StockRelease, p.StockRelease);
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
        Assert.Equal(b.Consumption, p.Consumption);
        Assert.Equal(b.Production, p.Production);
        Assert.Equal(b.StockRelease, p.StockRelease);
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
    public void Price_DtSensitivity_IsEulerDiscretization_BoundedAndConverging()
    {
        // Law 3 asks that every RATE be per-sim-year and integrated with
        // dtYears, which the step does: excess/scale is a ratio of per-turn
        // quantities and so is dimensionless, and the single explicit × dtYears
        // carries the whole dt dependence. Law 3 does NOT demand that
        // trajectories be dt-invariant, and this one is not, for a reason no
        // correct implementation of the mandated formula can avoid.
        //
        // D-033 fixes the step as p += lambda * p * (excess/scale) * dt. That is
        // Euler on dp/dt = lambda * p * (excess/scale) — a COMPOUNDING process,
        // where Euler systematically under-integrates and converges from below
        // as dt shrinks. Measured over 100 sim-years on a sustained excess:
        //
        //   dt      10       5      2.5      1
        //   price  7.439   8.225   8.694   9.006
        //
        // Monotone, converging toward the exponential limit (~9.1), a 21% spread
        // across the shipped range. The exact integration p *= exp(...) would
        // remove it, and the T2.7b/ADR-011 exponential-survival precedent shows
        // the project will make that change deliberately — but the price step is
        // fixed by mandate and this is discretization, not a law violation, so
        // the residue is RECORDED and BOUNDED rather than silently re-formed.
        // Flagged for director ruling; queue.md carries the item.
        //
        // What this test pins: the sensitivity is monotone in dt, bounded, and
        // STILL PRESENT — so it cannot quietly go vacuous, and it cannot quietly
        // grow either (T3.3 ToolWear pattern).
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 100;
        double[] dts = [10.0, 5.0, 2.5, 1.0];
        var landed = new double[dts.Length];

        for (int d = 0; d < dts.Length; d++)
        {
            // Production and demand are PER-TURN INTEGRALS in the real sim —
            // they scale with dt, which is precisely why excess/scale is
            // dimensionless. A fixture that held them fixed while stock release
            // scaled would be testing an economy that cannot exist.
            double f = dts[d] / 10.0;
            WorldState w = World(cfg,
                [("pottery", 10, (long)(40 * f), (long)(20 * f), (long)(120 * f))]);
            for (int t = 0; t < (int)(horizonYears / dts[d]); t++) w = Step(cfg, w, dts[d]);
            landed[d] = Price(w, cfg, "pottery");
        }

        // NON-VACUITY: the solver actually moved the price over the horizon.
        Assert.True(landed[0] > 2.0,
            $"the dt test is vacuous — price barely moved (landed {landed[0]})");

        // MONOTONE CONVERGENCE from below, the signature of Euler on a
        // compounding process. A step that had a dt BUG rather than dt
        // discretization would not be monotone.
        for (int d = 1; d < dts.Length; d++)
        {
            Assert.True(landed[d] > landed[d - 1],
                $"not monotone in dt: dt={dts[d - 1]} → {landed[d - 1]}, dt={dts[d]} → {landed[d]}");
        }

        // BOUNDED: the spread across the shipped dt range stays where it was
        // measured. If a retune widens it, this fails and the residue gets
        // re-examined rather than drifting.
        double spread = (landed[^1] - landed[0]) / landed[0];
        Assert.InRange(spread, 0.10, 0.35);

        // The sensitivity STILL EXISTS, so the test cannot pass by the
        // mechanism quietly disappearing.
        Assert.True(spread > 0.0, "dt sensitivity vanished — this test is now vacuous");
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

        for (int t = 0; t < 50; t++) { alone = Step(cfg, alone); paired = Step(cfg, paired); }

        double isolated = Price(alone, cfg, "pottery");
        double withNeighbour = double.NaN;
        for (int i = 0; i < paired.Prices.Count; i++)
            if (paired.Prices[i].Settlement == S0 && paired.Prices[i].Good.Value == cfg.Goods!.IdOf("pottery"))
                withNeighbour = paired.Prices[i].Price;

        // EXACT equality — not "close". Coupling of any strength fails here.
        Assert.Equal(isolated, withNeighbour);
    }
}
