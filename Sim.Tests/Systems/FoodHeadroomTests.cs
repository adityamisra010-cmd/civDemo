using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-1 (CR-015 §3.6a, test plan §6.4 H-*) — the ONE definition of the
/// food-influx limit. It counts FEEDABLE food — the exact mirror of
/// ConsumptionSystem's one-directional substitution — not the sum of food
/// goods. The Default-mix rig carries the spec's own Libur t110 numbers.
/// </summary>
public class FoodHeadroomTests
{
    private static readonly SettlementId S0 = new(0);

    private static BasketBook Book(SimConfig cfg) => new(cfg.Needs!, cfg.Goods!);

    private sealed record Food(string Good, long Produced, long Demanded);

    /// <summary>A hand world holding exactly the rows FoodHeadroom reads: the
    /// catchment summary row (T4.21-4 RULE 2 — its ABSENCE is the "influx never
    /// measured" null arm, so the finite arm requires it; arable is irrelevant to
    /// FoodHeadroom, which reads produced units, so any positive value serves),
    /// the deficit row (DemandUnits), the vitals row (DtYears) and one grain stock
    /// row per food good with its LastProduced / LastConsumptionDemand units.</summary>
    private static WorldState Rig(
        SimConfig cfg, long demand, double dt, Food[] foods,
        bool deficitRow = true, bool vitalsRow = true, long[]? adults = null,
        bool catchmentRow = true)
    {
        var w = new WorldState(7);
        w.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        if (catchmentRow)
        {
            w.CatchmentSummaries.Add(new CatchmentSummaryRow(
                S0, NodeCount: 1, EffectiveArableKm2: 5000.0, NetworkRevision: 0, LastRecomputeTurn: 0));
        }
        if (deficitRow) w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, 0.0, demand));
        if (vitalsRow) w.SettlementVitals.Add(new SettlementVitalsRow(S0, 0, 0, dt));
        foreach (Food f in foods)
        {
            w.GoodStocks.Add(new GoodStockRow(
                S0, new GoodId(cfg.Goods!.IdOf(f.Good)), Conserved.Zero, 0.0, 0.0,
                lastProducedUnits: f.Produced, lastConsumptionDemandUnits: f.Demanded));
        }
        if (adults is not null)
        {
            var ledger = new Ledger(w.LedgerFlows);
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = w.Buckets.Add(new BucketRow(
                    S0, new CultureId(1), new ReligionId(1), new ClassId(1), c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (adults[c] > 0)
                    ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, adults[c], FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        return w;
    }

    /// <summary>The spec's Default-mix rig (Libur t110): grain 7458, livestock +
    /// fish 5275, D = 6291, non-staple demand 629 (livestock 315, fish 314).</summary>
    private static Food[] LiburT110 =>
    [
        new("grain", 7458, 5662),
        new("livestock", 2637, 315),
        new("fish", 2638, 314),
    ];

    [Fact]
    public void H_Limit_ExactMirrorOfSubstitution()
    {
        // KILLS M-DEM-SUMFOOD (X := Σ LastProduced over food goods → 12733, a
        // 2.02× limit instead of 1.32×).
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        const long D = 6291;
        const double dt = 10.0;

        // (a) Every non-staple in surplus: the closed form X = S / (1 − Σ b_g)
        //     = S / b_staple — Libur's 7458/0.9 ≈ 8287, ρ_eff = 1.32.
        WorldState surplus = Rig(cfg, D, dt, LiburT110);
        double x = FoodHeadroom.FeedableAtLimit(surplus, S0, book, D);
        double bLivestock = 315 / (double)D, bFish = 314 / (double)D;
        double closedForm = 7458.0 / (1.0 - (bLivestock + bFish));
        Assert.Equal(closedForm, x);                                   // to the ulp of the closed form
        Assert.InRange(x, 7458.0 / 0.9 - 1.0, 7458.0 / 0.9 + 1.0);     // ≈ 8287
        Assert.InRange(x / D, 1.31, 1.33);                             // ρ_eff = 1.32
        Assert.Equal(closedForm / dt, FoodHeadroom.Limit(surplus, S0, cfg.Consumption.CohortWeights, book));
        // The sum of food goods is NOT the limit: a livestock surplus is not bread.
        Assert.NotEqual(7458.0 + 5275.0, x);
        Assert.True(x < 7458.0 + 5275.0);

        // (b) Every non-staple SHORT (NS_g < b_g·X): X = S + Σ NS_g — the
        //     non-staple can cover only what it produced, the staple the rest.
        WorldState @short = Rig(cfg, D, dt,
            [new("grain", 7458, 5662), new("livestock", 10, 315), new("fish", 20, 314)]);
        Assert.Equal(7458.0 + 10.0 + 20.0, FoodHeadroom.FeedableAtLimit(@short, S0, book, D));

        // (c) Mixed: livestock in surplus, fish short — the fixed point
        //     X = (S + NS_fish) / (1 − b_livestock).
        WorldState mixed = Rig(cfg, D, dt,
            [new("grain", 7458, 5662), new("livestock", 2637, 315), new("fish", 20, 314)]);
        double xMixed = FoodHeadroom.FeedableAtLimit(mixed, S0, book, D);
        Assert.Equal((7458.0 + 20.0) / (1.0 - bLivestock), xMixed);
        // and it IS a fixed point of X = S + Σ min(NS_g, b_g X):
        Assert.Equal(xMixed, 7458.0 + Math.Min(2637.0, bLivestock * xMixed) + Math.Min(20.0, bFish * xMixed), 9);

        // (d) The partition is MONOTONE: X only falls when a good moves short,
        //     so a good in surplus at the higher X stays in surplus at the lower
        //     one and the loop terminates in ≤ |FoodGoods| moves. Livestock sits
        //     just above its share at the all-surplus X; fish is short; the
        //     fixed point still holds exactly.
        WorldState edge = Rig(cfg, D, dt,
            [new("grain", 7458, 5662), new("livestock", 415, 315), new("fish", 1, 314)]);
        double xEdge = FoodHeadroom.FeedableAtLimit(edge, S0, book, D);
        Assert.Equal(xEdge, 7458.0 + Math.Min(415.0, bLivestock * xEdge) + Math.Min(1.0, bFish * xEdge), 9);
    }

    [Fact]
    public void H_Limit_NullArms_ArePositiveInfinity()
    {
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        double[] cw = cfg.Consumption.CohortWeights;

        // No deficit row (turn 1 of a founded world; a colony's first turn).
        Assert.Equal(double.PositiveInfinity,
            FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, LiburT110, deficitRow: false), S0, cw, book));
        // DemandUnits == 0 (every `new ConsumptionDeficitRow(s, d)` hand rig).
        Assert.Equal(double.PositiveInfinity,
            FoodHeadroom.Limit(Rig(cfg, 0, 10.0, LiburT110), S0, cw, book));
        // No vitals row.
        Assert.Equal(double.PositiveInfinity,
            FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, LiburT110, vitalsRow: false), S0, cw, book));
        // A settlement that is not the one asked about is invisible.
        Assert.Equal(double.PositiveInfinity,
            FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, LiburT110), new SettlementId(9), cw, book));
        // Vacancy on the null arm is +∞ too (no bound).
        Assert.Equal(double.PositiveInfinity,
            FoodHeadroom.Vacancy(Rig(cfg, 0, 10.0, LiburT110), S0, cw, book));
    }

    [Fact]
    public void H_NullArm_NoCatchmentRow_IsPositiveInfinity_ButAbandonedWithARowIsZero()
    {
        // T4.21-4 RULE 2 — the null arm's key is ROW ABSENCE, never "production
        // == 0". The two arms of one decision, asserted together so neither can
        // be satisfied by weakening the other:
        //
        //  (i) NO catchment summary row ⇒ the food influx was never MEASURED
        //      (ProductionSystem.Farm reads prev.CatchmentSummaries for arable, so
        //      with no row the land side is 0 and the staple harvest is 0 — a
        //      structural unavailability, not a measured zero capacity) ⇒ +∞: no
        //      growth cap and no vacancy bound for that settlement.
        // (ii) A catchment row PRESENT with arable > 0 and zero food production is
        //      the ABANDONED settlement: its zero influx is genuine ⇒ N_lim = 0 and
        //      the growth cap still binds (the D_Cap_NoGrowthOnAGranary semantics).
        //
        // KILLS the mutation "key the arm on EffectiveArableKm2 == 0 / on S == 0"
        // — either one collapses (ii) into (i) and this test's second half fails.
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        double[] cw = cfg.Consumption.CohortWeights;

        // (i) Every other input present and finite — only the catchment row is gone.
        WorldState noRow = Rig(cfg, 6291, 10.0, LiburT110, catchmentRow: false);
        Assert.Equal(double.PositiveInfinity, FoodHeadroom.Limit(noRow, S0, cw, book));
        Assert.Equal(double.PositiveInfinity, FoodHeadroom.Vacancy(noRow, S0, cw, book));
        // The SAME rig with the row present is finite — the row is the only cause.
        WorldState withRow = Rig(cfg, 6291, 10.0, LiburT110);
        Assert.Equal(7458.0 / (1.0 - ((315 / 6291.0) + (314 / 6291.0))) / 10.0,
            FoodHeadroom.Limit(withRow, S0, cw, book));

        // (ii) Row present, arable > 0, nothing produced: the abandoned settlement.
        WorldState abandoned = Rig(cfg, 6291, 10.0, []);
        bool arableRowPresent = false;
        for (int i = 0; i < abandoned.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow r = abandoned.CatchmentSummaries[i];
            if (r.Settlement == S0 && r.EffectiveArableKm2 > 0.0) { arableRowPresent = true; break; }
        }
        Assert.True(arableRowPresent, "the abandoned arm needs a catchment row with arable > 0");
        Assert.Equal(0.0, FoodHeadroom.Limit(abandoned, S0, cw, book));
        Assert.Equal(0.0, FoodHeadroom.Vacancy(abandoned, S0, cw, book));
    }

    [Fact]
    public void H_Limit_NoFoodRowsAtAll_IsZero()
    {
        // A settlement with a demand row but no food produced (an abandoned
        // rig): S = 0 and every non-staple is short at 0 ⇒ X = 0 ⇒ N_lim = 0.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, 6291, 10.0, []);
        Assert.Equal(0.0, FoodHeadroom.Limit(w, S0, cfg.Consumption.CohortWeights, Book(cfg)));
    }

    [Fact]
    public void H_Limit_ReadsPrevDt_NotTheCurrentOne()
    {
        // N_lim = X / dt_prev: an era-boundary turn reads the dt that PRODUCED X.
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        double at10 = FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, LiburT110), S0, cfg.Consumption.CohortWeights, book);
        double at5 = FoodHeadroom.Limit(Rig(cfg, 6291, 5.0, LiburT110), S0, cfg.Consumption.CohortWeights, book);
        Assert.Equal(2.0, at5 / at10);
    }

    [Fact]
    public void H_Limit_TableOrderIndependent()
    {
        // The scan runs in REGISTRY order over the basket's food goods, so the
        // GoodStocks row order cannot change a bit of the result.
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        Food[] a = LiburT110;
        Food[] b = [a[2], a[0], a[1]];
        Food[] c = [a[1], a[2], a[0]];
        long bitsA = BitConverter.DoubleToInt64Bits(FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, a), S0, cfg.Consumption.CohortWeights, book));
        long bitsB = BitConverter.DoubleToInt64Bits(FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, b), S0, cfg.Consumption.CohortWeights, book));
        long bitsC = BitConverter.DoubleToInt64Bits(FoodHeadroom.Limit(Rig(cfg, 6291, 10.0, c), S0, cfg.Consumption.CohortWeights, book));
        Assert.Equal(bitsA, bitsB);
        Assert.Equal(bitsA, bitsC);

        // Rows of ANOTHER settlement interleaved change nothing either.
        WorldState noisy = Rig(cfg, 6291, 10.0, a);
        noisy.GoodStocks.Add(new GoodStockRow(new SettlementId(3), new GoodId(cfg.Goods!.IdOf("grain")),
            Conserved.Zero, 0.0, 0.0, lastProducedUnits: 999_999, lastConsumptionDemandUnits: 1));
        Assert.Equal(bitsA, BitConverter.DoubleToInt64Bits(
            FoodHeadroom.Limit(noisy, S0, cfg.Consumption.CohortWeights, book)));
    }

    [Fact]
    public void H_Vacancy_IsLimitMinusNutritionalPersons_ClampedAtZero()
    {
        SimConfig cfg = TestConfigs.Sim();
        BasketBook book = Book(cfg);
        double[] cw = cfg.Consumption.CohortWeights;

        // 600 adults (weight 1.0) + 100 children in cohort 0 (weight 0.6) = 660 ae.
        var counts = new long[Cohorts.Count];
        counts[5] = 600;
        counts[0] = 100;
        WorldState w = Rig(cfg, 6291, 10.0, LiburT110, adults: counts);
        double limit = FoodHeadroom.Limit(w, S0, cw, book);
        double nutritional = 600 * cw[5] + 100 * cw[0];
        Assert.Equal(limit - nutritional, FoodHeadroom.Vacancy(w, S0, cw, book));
        Assert.True(FoodHeadroom.Vacancy(w, S0, cw, book) > 0.0);

        // A population above the limit has NO vacancy — never a negative one
        // (the cap is a growth limiter; decline is the deficit channel's job).
        var crowd = new long[Cohorts.Count];
        crowd[5] = 100_000;
        WorldState full = Rig(cfg, 6291, 10.0, LiburT110, adults: crowd);
        Assert.Equal(0.0, FoodHeadroom.Vacancy(full, S0, cw, book));
    }
}
