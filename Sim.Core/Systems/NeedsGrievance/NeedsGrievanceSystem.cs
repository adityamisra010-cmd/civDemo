using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Consumption;

namespace Sim.Core.Systems.NeedsGrievance;

/// <summary>Writable handles to NeedsGrievanceSystem's own tables (built by
/// SystemCatalog only).</summary>
public readonly record struct NeedsGrievanceTables(
    Table<NeedSatisfactionRow> Satisfactions, Table<GrievanceRow> Grievances);

/// <summary>
/// Needs + grievance (T2.6; T3.5 — D-035). Everything reads Prev (§3.2). Slots
/// after Demographics, before PathBuild.
///
/// SATISFACTION (rebuilt each turn, per settlement × class × BOUND need). M3
/// binds THREE needs — Sustenance, Shelter, Comfort — against the D-035 class
/// baskets, and every one of them is computed from what the settlement actually
/// obtained last turn:
///
///   s = (obtained / wanted) × varietyFactor(obtained across the basket)
///
/// The first factor is quantity, the second is D-035-A. The variety term is a
/// FACTOR INSIDE the satisfaction, never a bonus added to it (law 2): the same
/// calories from grain alone satisfy Sustenance less than the same calories
/// spread over grain, livestock and fish — and its Herfindahl runs over the
/// basket's DECLARED goods, so a grain-only diet takes the full monotony
/// penalty rather than scoring as an unimprovable one-good basket. Shelter
/// declares varietyWeight 0: two dwellings' worth of timber is not a more
/// varied roof.
///
/// CLASSES NOW GENUINELY DIFFER (the M2 header's honesty note, discharged).
/// Peasant and artisan satisfactions diverge because their basket LINES differ
/// — artisans eat more livestock and fish and hold more pottery and cloth — so
/// a failed fishery hurts artisans more than peasants without a single
/// class-indexed coefficient anywhere. UNBOUND needs get no rows and contribute
/// EXACTLY nothing: they are skipped before any weight is read, so their
/// weights remain dormant data whatever value they hold (the T2.6 zero-effect
/// gate, which the CES aggregation must not quietly break).
///
/// AGGREGATION (D-035-B, amending d018:52's weighted sum): the bound needs
/// combine by CES with σ &lt; 1, after d018:46's Tier A gate override has
/// reweighted them. Abundant comfort cannot buy off absent shelter — see
/// <see cref="NeedsAggregation"/> for the mathematics and for why σ &lt; 1 is
/// the load-bearing constraint rather than a tuning preference.
///
/// GRIEVANCE (persistent per-(settlement, class) DOUBLE stock): deliberately
/// NOT conserved and NEVER Ledger — law 1 governs people/money/goods, and
/// grievance is manufactured by deprivation and destroyed by decay. Explicit
/// dt-correct integration, per year, integrated with dtYears:
///
///   G_next = G_prev + W × (expectation − S)⁺ × dt − decayRate(t) × G_prev × dt
///
/// where S is the CES aggregate and W the sum of the bound needs' RAW weights.
/// W is the accrual's scale and the weights' RELATIVE sizes act inside the
/// aggregation, which is where D-035-B put them; in the one-bound-need limit
/// this reduces to M2's w × (expectation − s) bit-exactly FOR s ABOVE THE
/// SATISFACTION FLOOR. Below the floor the two diverge, because the floor
/// clamps unconditionally — at s = 0 with one bound need the accrual is 0.95·w,
/// not w. Stated rather than rounded off: that config is unreachable in shipped
/// data, which is exactly why the qualification would otherwise go unnoticed.
///
/// expectation is FIXED at 1.0 — the D-018 §4 habituation ratchet is deferred
/// to the milestone that gives needs supply curves to habituate to.
///
/// GENERATIONAL DECAY (D-021 §8 — "children inherit a fraction of their
/// parents' grudges"): decayRate = BaseDecayPerYear +
/// (1 − InheritFraction) × turnoverRate, where turnoverRate = (PREV births +
/// deaths) / PREV population per settlement per year — read from the PREV
/// SettlementVitals chronicle row (Demographics writes it; communication is
/// through tables, law 6), whose own DtYears field keeps the rate per-year
/// across era-pacing transitions. All three knobs are TUNE in needs.json.
///
/// READ ISOLATION (the T2.6 packet's teeth, unchanged): NeedSatisfactions and
/// Grievances are referenced ONLY by this system, serialization, StateEquals,
/// tests, and Sim.Ui — enforced by the CI read-isolation grep; grievance drives
/// NO behavior until M5 ships the unrest valves.
/// STATELESS: config is immutable tuning, not state. No RNG.
/// </summary>
public sealed class NeedsGrievanceSystem : ISimSystem<NeedsGrievanceTables>
{
    public static readonly SystemId WellKnownId = new(11);
    public const string Name = "needsgrievance";

    /// <summary>Expectation baseline (see header: habituation deferred, D-018 §4).</summary>
    private const double Expectation = 1.0;

    /// <summary>d018:46's Tier A gate needs — Sustenance, Shelter, Safety. Below
    /// the floor these scale superlinearly and collapse the upper needs' weights;
    /// see <see cref="NeedsAggregation.ApplyTierAGate"/>. Data-checked against
    /// the registry at construction, not assumed.</summary>
    private static readonly int[] TierAGateNeedIds = [1, 2, 3];

    private readonly NeedsConfig _needs;
    private readonly BasketBook _baskets;
    private readonly GoodId _grain;

    public NeedsGrievanceSystem(SimConfig cfg)
    {
        _needs = cfg.Needs ?? throw new NeedsConfigException(
            "SimConfig.Needs is not loaded — construct configs via " +
            "SimConfigLoader.Load(simStream, needsStream) so needs.json travels with sim.json.");
        GoodsConfig goods = cfg.Goods ?? throw new NeedsConfigException(
            "SimConfig.Goods is not loaded — T3.5 satisfaction is computed from goods baskets.");
        _baskets = new BasketBook(_needs, goods);
        _grain = new GoodId(goods.GrainId);

        for (int i = 0; i < TierAGateNeedIds.Length; i++)
        {
            bool known = false;
            for (int n = 0; n < _needs.Needs.Length; n++)
                if (_needs.Needs[n].Id == TierAGateNeedIds[i]) { known = true; break; }
            if (!known)
                throw new NeedsConfigException(
                    $"d018:46 Tier A gate need id {TierAGateNeedIds[i]} is absent from the needs registry.");
        }
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<NeedsGrievanceTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        double dt = ctx.DtYears;
        GrievanceTuning tuning = _needs.Grievance;
        AggregationTuning agg = _needs.Aggregation;

        Table<NeedSatisfactionRow> satisfactions = ctx.Owned.Satisfactions;
        satisfactions.Clear();

        // Grievance rows persist (cloned): founded settlement-major in class
        // order; iterate rows and derive everything per row's settlement.
        Table<GrievanceRow> grievances = ctx.Owned.Grievances;

        int needCount = _needs.Needs.Length;
        Span<double> sat = stackalloc double[needCount];
        Span<double> weight = stackalloc double[needCount];
        Span<double> adjusted = stackalloc double[needCount];
        Span<bool> isGate = stackalloc bool[needCount];

        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            long settlementPop = 0;
            for (int b = 0; b < prev.Buckets.Count; b++)
                if (prev.Buckets[b].Settlement == settlement) settlementPop += prev.Buckets[b].Count.Value;

            // T2.13 director finding (ghost grievance): grievance is HELD BY
            // PEOPLE — an extinct settlement's stock is zeroed, not left to
            // decay for centuries in an empty ruin, and no satisfaction rows
            // publish (nobody to be satisfied).
            if (settlementPop == 0)
            {
                for (int g = 0; g < grievances.Count; g++)
                    if (grievances[g].Settlement == settlement)
                        grievances[g] = grievances[g] with { Value = 0.0 };
                continue;
            }

            // D-021 generational turnover from the PREV vitals chronicle row
            // (absent on the first turn → 0): per-year via the ROW's dt.
            double turnoverPerYear = 0.0;
            for (int i = 0; i < prev.SettlementVitals.Count; i++)
            {
                SettlementVitalsRow v = prev.SettlementVitals[i];
                if (v.Settlement != settlement) continue;
                if (v.DtYears > 0.0)
                    turnoverPerYear = (v.Births + v.Deaths) / (double)settlementPop / v.DtYears;
                break;
            }
            double decayRate = tuning.BaseDecayPerYear
                               + (1.0 - tuning.InheritFraction) * turnoverPerYear;

            for (int g = 0; g < grievances.Count; g++)
            {
                if (grievances[g].Settlement != settlement) continue;
                ClassId cls = grievances[g].Class;

                // --- per-need satisfaction from PREV basket fills -------------
                int bound = 0;
                double rawWeightSum = 0.0;
                for (int n = 0; n < needCount; n++)
                {
                    NeedEntry need = _needs.Needs[n];
                    if (!need.Bound) continue;                       // unbound: skipped before any weight is read
                    ReadOnlySpan<BasketLine> basket = _baskets.Basket(cls, need.Id);
                    if (basket.Length == 0) continue;                // this class demands nothing here

                    double value = Satisfaction(prev, settlement, cls, need, basket);
                    satisfactions.Add(new NeedSatisfactionRow(settlement, cls, need.Id, value));

                    sat[bound] = value;
                    weight[bound] = need.Weight;
                    isGate[bound] = IsTierAGate(need.Id);
                    rawWeightSum += need.Weight;
                    bound++;
                }

                double aggregate = Expectation;
                if (bound > 0)
                {
                    NeedsAggregation.ApplyTierAGate(
                        sat[..bound], isGate[..bound], weight[..bound],
                        agg.TierAFloor, agg.TierAGain, agg.TierACollapse, adjusted[..bound]);
                    aggregate = NeedsAggregation.Aggregate(
                        sat[..bound], adjusted[..bound], agg.Sigma, agg.SatisfactionFloor);
                }
                double accrualPerYear = rawWeightSum * Math.Max(0.0, Expectation - aggregate);

                double gPrev = 0.0;
                for (int i = 0; i < prev.Grievances.Count; i++)
                {
                    if (prev.Grievances[i].Settlement == settlement
                        && prev.Grievances[i].Class == cls)
                    { gPrev = prev.Grievances[i].Value; break; }
                }
                // Explicit Euler, floored at 0: a decayRate × dt > 1 step (huge
                // turnover at Neolithic dt) must bottom out, not oscillate
                // negative. The floor is the stock's own domain bound, not a
                // conservation clamp.
                double gNext = gPrev + accrualPerYear * dt - decayRate * gPrev * dt;
                grievances[g] = grievances[g] with { Value = Math.Max(0.0, gNext) };
            }
        }
    }

    /// <summary>
    /// One (settlement, class, need) satisfaction: quantity obtained over
    /// quantity wanted, times the D-035-A variety factor over what was obtained.
    ///
    /// Everything is expressed in PER-PERSON-YEAR RATES: the population and
    /// dtYears factors are identical in numerator and denominator and cancel, so
    /// a hamlet and a city facing the same fill ratios have the same
    /// satisfaction — which is what a per-capita satisfaction must mean.
    ///
    /// SUBSTITUTION (Sustenance only) mirrors ConsumptionSystem's: unmet
    /// non-staple food demand falls back on the staple, so a fishery failure
    /// costs nutrition only if the grain store cannot cover it — and costs
    /// variety either way. That is D-035-A's whole purpose, and it is why the
    /// two systems read the same <see cref="BasketBook"/> rather than each
    /// keeping a copy of the basket that could drift from the other.
    /// </summary>
    private double Satisfaction(
        IReadOnlyWorldState prev, SettlementId settlement, ClassId cls,
        NeedEntry need, ReadOnlySpan<BasketLine> basket)
    {
        Span<double> obtained = stackalloc double[basket.Length];
        double wanted = 0.0, got = 0.0;

        if (need.Id == BasketBook.SustenanceNeedId)
        {
            double shortfall = 0.0, stapleRate = 0.0;
            int stapleIndex = -1;
            for (int i = 0; i < basket.Length; i++)
            {
                wanted += basket[i].PerPersonYear;
                if (basket[i].Good == _grain)
                {
                    stapleIndex = i;
                    stapleRate = basket[i].PerPersonYear;
                    continue;
                }
                double o = basket[i].PerPersonYear * Fill(prev, settlement, basket[i].Good);
                obtained[i] = o;
                got += o;
                shortfall += basket[i].PerPersonYear - o;
            }
            if (stapleIndex >= 0)
            {
                double o = (stapleRate + shortfall) * Fill(prev, settlement, _grain);
                obtained[stapleIndex] = o;
                got += o;
            }
        }
        else
        {
            for (int i = 0; i < basket.Length; i++)
            {
                wanted += basket[i].PerPersonYear;
                double o = basket[i].PerPersonYear * Fill(prev, settlement, basket[i].Good);
                obtained[i] = o;
                got += o;
            }
        }

        double quantity = wanted > 0.0 ? Math.Clamp(got / wanted, 0.0, 1.0) : 1.0;
        return Math.Clamp(quantity * NeedsAggregation.VarietyFactor(obtained, need.VarietyWeight), 0.0, 1.0);
    }

    /// <summary>
    /// The settlement-wide fill ratio for one good last turn: obtained over
    /// pre-clamp demanded.
    ///
    /// THREE CASES, and conflating them was a real defect. A published demand of
    /// zero can mean either of two opposite things:
    ///
    ///  1. NO STOCK ROW AT ALL — the good does not exist in this settlement's
    ///     world. Nothing wanted from a market that is not there: 1.0.
    ///  2. A ROW EXISTS AND DEMAND IS ZERO — the basket wanted a positive RATE
    ///     but the turn's integer demand quantised away (WholeUnits floors, and
    ///     the smallest basket line is 0.005/person-year). Whether the household
    ///     got its sub-unit share depends on whether the stock existed, so the
    ///     STOCK is the discriminator: an empty store means they got nothing.
    ///  3. DEMAND IS POSITIVE — the ordinary case, the honest ratio.
    ///
    /// Case 2 previously returned 1.0 unconditionally, reporting a settlement
    /// with EMPTY timber, stone, pottery and cloth stores as fully sheltered and
    /// comforted. Measured reachability: zero occurrences across 400 turns of
    /// the founded world at shipped pacing AND at dt = 1 — it needs fewer than
    /// ~200-400 people, against live settlements of 2290-6279. But the size of
    /// that gap is a T3.5 artefact: splitting one flow at rate ~1.0 into seven
    /// at 0.005-0.06 moved the quantisation threshold from about two people to
    /// two hundred. Inherited shape, T3.5 reachability, T3.5 fix.
    /// </summary>
    private static double Fill(IReadOnlyWorldState prev, SettlementId settlement, GoodId good)
    {
        int i = GoodStockIndex.IndexOf(prev.GoodStocks, settlement, good);
        if (i < 0) return 1.0;                                   // case 1
        long demanded = prev.GoodStocks[i].LastConsumptionDemandUnits;
        if (demanded <= 0)                                       // case 2
            return prev.GoodStocks[i].Amount.Value > 0 ? 1.0 : 0.0;
        return Math.Clamp(                                       // case 3
            prev.GoodStocks[i].LastConsumptionEatenUnits / (double)demanded, 0.0, 1.0);
    }

    private static bool IsTierAGate(int needId)
    {
        for (int i = 0; i < TierAGateNeedIds.Length; i++) if (TierAGateNeedIds[i] == needId) return true;
        return false;
    }
}
