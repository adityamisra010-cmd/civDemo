using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Consumption;

namespace Sim.Core.Systems.Demographics;

/// <summary>Writable handles to DemographicsSystem's own tables (built by
/// SystemCatalog only). SettlementVitals (T2.6) is this system's per-turn
/// chronicle of birth/death counts — the D-021 generational-turnover input
/// NeedsGrievance reads from Prev.</summary>
public readonly record struct DemographicsTables(
    Table<BucketRow> Buckets, Table<SettlementVitalsRow> Vitals);

/// <summary>
/// Demographics (T1.5; cohortized T2.1 per D-026; EXPONENTIAL-SURVIVAL
/// MICRO-STEP integration at T2.7b per ADR-011 / CR-001 ruling): births,
/// mortality, starvation and cohort aging over the Buckets table — every
/// person moving exclusively through the Ledger (law 1). Rates are
/// per-sim-year (law 3); their INTEGRATION is the closed form of the
/// constant-rate ODE composed over FIXED half-year micro-steps, not explicit
/// Euler at turn scale (ADR-011: Euler was implementation, not law — ADR-010
/// precedent).
///
/// THE MICRO-STEP KERNEL (dt-invariance BY CONSTRUCTION): a turn of dt years
/// integrates n = dt / 0.5 identical half-year micro-steps in EXACT double
/// arithmetic over the settlement's cohort vector; every era dt (10, 5, 3,
/// 2, 1, 0.5) is an exact multiple of the micro-step, so every dt executes
/// the SAME kernel the same number of times per sim-year — the growth rate
/// cannot depend on dt except through turn-boundary integer flooring (a dt
/// that is not a multiple gets one shorter final micro-step; deterministic).
/// Without this, the aging scheme's two regimes (deterministic k-slot
/// transit at dt ≥ width, diffusive fractional advance below) meet mid-arc
/// and disagree by ~1–2.5/1000·yr of growth (measured) — dwarfing the
/// ratified +0.7/1000 signal.
///
/// PER MICRO-STEP h, per settlement, the PINNED composition order:
///   0. Births — per group: B = Σ_c fertility_c × pop_c × w(λ_c·h) × h ×
///      famine factor (w(x) = (1−e^(−x))/x, the person-years kernel — people
///      dying mid-step bear children for the fraction they lived; λ = base +
///      starvation rate). The suppression factor max(0, 1 − slope × PREV
///      EFFECTIVE deficit — T4.21-3, see below) multiplies fertility;
///      suppressed exact births bank into the ReboundReservoir and release on
///      fed turns (PREV NOMINAL deficit exactly zero) at the TUNE rate — all
///      at micro-scale, dt-correct by construction.
///      NEWBORN CREDIT happens HERE, inside the births step: survivors =
///      B × w(λ_0·h) join cohort 0 immediately (the shortfall is an infant
///      Death — Births counts live births, Deaths includes in-step infant
///      deaths), and the credited newborns then face THIS micro-step's
///      sinks and aging below. That double exposure (w(λ_0·h) × e^(−λ_0·h)
///      instead of the pure mid-step w factor) is a deliberate PINNED
///      choice: it is identical at every dt (per-micro-step, so
///      dt-invariance is untouched), it is part of the honest compositional
///      residue the retune absorbed, and moving the credit after the sinks
///      would be a behavior change re-anchoring every tuned rate for no
///      structural gain.
///   1. Base deaths — pop_c × (1 − e^(−m_c·h)), then
///   2. Starvation — remaining × (1 − e^(−s_c·h)), s_c = max rate × PREV
///      EFFECTIVE deficit × age multiplier (sequential exponential sinks
///      compose to e^(−(m+s)h) regardless of order; the order is pinned).
///      THE EFFECTIVE DEFICIT (T4.21-3, CR-015 / ADR-026, "famine is
///      exceptional"): dEff = FoodState.EffectiveDeficit(d, FoodState.Of(prev))
///      — in FAMINE (a famine-class disaster applied to the harvest, or food
///      labour abandoned, with d > 0) it IS the nominal deficit; otherwise the
///      dead-zone form max(0, d − a)/(1 − a) with a the absorbable shortfall:
///      STRESS (d ≤ a) starves nobody and suppresses no birth, SEVERE starves
///      on the unabsorbed remainder. One per-turn scalar feeds BOTH
///      exceptional channels (mortality and fertility suppression, G3(b));
///      the rebound release gate reads the NOMINAL d == 0.0.
///      Mortality acts on PRESENT counts (ADR-011 §1): per-capita and
///      position-independent — people moved by an earlier system this turn
///      die where they stand; the Prev-sized dodge class is structurally
///      dead. The deficit is LAST turn's Consumption output (§3.2 — signals
///      are Prev-read; only the integration acts on present stocks).
///   3. Aging — ADR-010 slot-advance at micro-scale: h/width of each cohort
///      advances one slot (descending, cascade-free); 75+ absorbs. At
///      h < width this is the fractional-advance kernel ADR-010 defines.
///      Newborns age upward through the same micro-kernel — the coarse-dt
///      "newborn cohort spread" of the T2.1 kernel is SUPERSEDED: the spread
///      now emerges from integration instead of being imposed.
///
/// THE HEADROOM GROWTH CAP (T4.21-3, CR-015 §28 / ADR-026 §2.2(ii)): under
/// constant conditions a population rises asymptotically toward N_lim — the
/// adult-equivalents its last-turn FEEDABLE food influx sustains
/// (FoodHeadroom.Limit, ONE definition shared with Migration's vacancy bound
/// and the observer) — and never overshoots into deficit. Once per
/// settlement per turn: N_lim from PREV; N_now = Σ cohortWeights[c] × pop over
/// the OWNED buckets at step start (post-migration, post-colonization,
/// post-revolt: refugees who arrived this turn have used headroom);
/// H_0 = max(0, N_lim − N_now) — clamped, because the cap is a GROWTH
/// limiter: decline is the deficit channel's job (unclamped, one ordinary bad
/// draw with a full granary would cut births 73 %). A stockpile is not an
/// influx: an abandoned settlement living on its granary (S = 0, d = 0) has
/// N_lim = 0 ⇒ births ≤ deaths. N_lim = +∞ (no deficit row, DemandUnits 0, no
/// vitals row — every founding turn and every hand rig) skips the cap ENTIRELY.
/// Per micro-step the births step is split so the cap is order-consistent
/// with births-before-sinks: PASS A per group computes the uncapped
/// candidate (unsuppressed, base = unsuppressed × suppression, the bank from
/// the UNCAPPED pair, the release from the nominal-d gate, cand = base +
/// release) with no state mutation; CAP: the standing population's deaths
/// this step D_pre = Σ pop (1 − e^(−(m+s)h)) w_c and its aging drift
/// A_step = Σ pop e^(−(m+s)h) (h/width)(w_{c+1} − w_c) are closed forms of the
/// pre-birth state, allowed = (1 − e^(−k h)) × H_rem, bornMax =
/// (allowed + D_pre − A_step) / (W(λ_0 h) e^(−λ_0 h) w_0) — newborn survivors
/// after this step's cohort-0 sinks, heads (weight-neutral aging into cohort
/// 1 ONLY because cohortWeights[0] == cohortWeights[1], asserted by
/// D_CohortWeights_NewbornAgingWeightNeutral) — and m = min(1, max(0,
/// bornMax)/Σ cand); PASS B commits: reservoir += bank, then IF m &lt; 1 the
/// candidate and the release are scaled (strict, deferred-not-invented: the
/// unreleased part STAYS banked; headroom-withheld births are never banked —
/// there is no physiological debt to refund), reservoir −= release, born =
/// cand, then the newborn credit exactly as before. After the sinks and aging
/// H_rem −= realised nutritional growth (aging drift included), so when
/// binding H_rem(end) = H_0 e^(−k dt) whatever the step cut — exact
/// composition; k = HeadroomRelaxationPerYear. min(1.0, big) returns the
/// literal 1.0 and the guard keeps the fed path instruction-identical to the
/// pre-T4.21 kernel. When bornMax ≤ 0 births are 0 but N_nutr may still rise
/// by A_step − D_pre (children maturing 0.6 → 1.0 outpacing deaths): the exact
/// bound is N_nutr(end) ≤ N_lim + Σ_steps max(0, A_step − D_pre). H_rem is NOT
/// clamped (§3.6b as written): after such a step it goes negative and the
/// later steps of the same turn hold births below replacement at rate k
/// until the drift excess is paid back — the ONLY decline the cap can
/// cause, bounded by that same Σ max(0, A_step − D_pre) within the turn
/// (D_Asymptote_* pins it against an uncapped replica turn). The cap is a
/// local of the turn, never state; it scales a birth CANDIDATE before it is
/// realised and never removes a person.
///
/// INTEGER RECONCILIATION, once per turn: the micro-integrated exact flow
/// totals (births, base deaths, starvation, per-cohort aging) floor through
/// the row's existing D-004 remainder accumulators into Ledger flows, in the
/// pinned order births → aging ASCENDING → deaths → starvation (ascending
/// transfers are the availability chain for multi-slot pass-through people;
/// each cohort's sole aging destination is the next slot). ClampToAvailable
/// backstops (a bucket bottoms at zero, never negative); clamp shortfalls
/// are NOT banked; remainders carry only sub-person fractions.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class DemographicsSystem : ISimSystem<DemographicsTables>
{
    public static readonly SystemId WellKnownId = new(7);
    public const string Name = "demographics";

    /// <summary>The fixed demographic micro-step (sim-years): the finest dt
    /// in the canonical era table, dividing every band's dt exactly. This is
    /// the kernel's integration scale, not TUNE — changing it redefines the
    /// integrator (ADR-011).</summary>
    public const double MicroStepYears = 0.5;

    private readonly SimConfig _cfg;

    /// <summary>T4.21-3: the shared basket book FoodHeadroom.Limit reads (which
    /// food goods exist and which is the staple) and the per-cohort nutritional
    /// weights (adult-equivalents per head). Both are config, read identically
    /// by every caller of the one headroom definition — not a channel between
    /// systems (the ClassMobilitySystem precedent).</summary>
    private readonly BasketBook _baskets;
    private readonly double[] _cohortWeights;

    public DemographicsSystem(SimConfig cfg)
    {
        _cfg = cfg;
        GoodsConfig goods = cfg.Goods
            ?? throw new ArgumentException(
                "DemographicsSystem requires SimConfig.Goods (goods.json) from T4.21-3.");
        NeedsConfig needs = cfg.Needs
            ?? throw new ArgumentException(
                "DemographicsSystem requires SimConfig.Needs (needs.json) from T4.21-3.");
        _baskets = new BasketBook(needs, goods);
        _cohortWeights = cfg.Consumption.CohortWeights;
    }

    public SystemId Id => WellKnownId;

    /// <summary>w(x) = (1 − e^(−x))/x, the uniform-exposure survival kernel;
    /// w(0) = 1 (guarded below the double-precision floor, deterministic).</summary>
    internal static double W(double x) => x < 1e-12 ? 1.0 : (1.0 - Math.Exp(-x)) / x;

    /// <summary>T4.21-3 — the settlement's food headroom as READ FROM PREV:
    /// FoodHeadroom.Vacancy(prev) = max(0, N_lim − N_nutr(prev)), adult-equivalents,
    /// +∞ on the null arm. For the observer and the chronicle (one definition,
    /// recomputed, never stored). NOTE it is NOT the H_0 the kernel integrates
    /// from: H_0 subtracts N_now over the OWNED buckets at step start (after
    /// this turn's migration, colonization and revolt moved people), so a
    /// destination that received refugees this turn has H_0 &lt; Headroom(prev)
    /// by their adult-equivalents (D_Headroom_CountsArrivals).</summary>
    public static double Headroom(IReadOnlyWorldState prev, SettlementId settlement, SimConfig cfg) =>
        FoodHeadroom.Vacancy(prev, settlement, cfg.Consumption.CohortWeights, new BasketBook(cfg.Needs!, cfg.Goods!));

    public void Step(SimContext<DemographicsTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        DemographicsConfig d = _cfg.Demographics;
        double dt = ctx.DtYears;

        Table<SettlementVitalsRow> vitals = ctx.Owned.Vitals;
        vitals.Clear();

        // Whole-step exact-flow accumulators, indexed by bucket row (one
        // allocation per step; rows belong to exactly one settlement).
        int rowCount = buckets.Count;
        var pop = new double[rowCount];          // micro-integrated cohort vector
        var birthsExact = new double[rowCount];  // live births credited (cohort-0 rows)
        var deathsExact = new double[rowCount];  // base + in-step infant deaths
        var starveExact = new double[rowCount];
        var agingExact = new double[rowCount];   // outflow to the NEXT slot
        var starveRate = new double[Cohorts.Count];
        var totalRate = new double[Cohorts.Count];
        // T4.21-3 PASS A scratch, indexed by the group's anchor (cohort-0) row:
        // the uncapped candidate and its parts, committed in PASS B.
        var unsuppressedA = new double[rowCount];
        var bankA = new double[rowCount];
        var releaseA = new double[rowCount];
        var candA = new double[rowCount];
        double k = d.HeadroomRelaxationPerYear;

        // Group anchors: cohort-0 rows; group member rows located once.
        // (Linear scans, law 5 — same access pattern the T2.1 kernel used.)
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // PREV turn's deficit ratio (absent before the first consumption turn → 0).
            double deficit = 0.0;
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            {
                if (prev.ConsumptionDeficits[i].Settlement == settlement)
                {
                    deficit = prev.ConsumptionDeficits[i].DeficitRatio;
                    break;
                }
            }
            // T4.21-3 (CR-015, ADR-026 §2.2(i)): the EFFECTIVE deficit — ONE
            // per-turn scalar, computed once from PREV beside the nominal
            // deficit and held constant across the micro-loop (so e^(−s·h)
            // still composes exactly: dt-invariance by construction). Outside
            // FAMINE adaptation absorbs a shortfall up to `a` and the
            // exceptional channels read the unabsorbed remainder
            // (d − a)/(1 − a); in FAMINE (disaster applied or food labour
            // abandoned, d > 0) dEff == deficit, so ONE argument serves both
            // regimes — the null arm a = 0 reproduces today's linear response
            // bit for bit. Both exceptional demographic channels read it
            // (G3(b)): starvation mortality AND fertility suppression. Every
            // other reader keeps the NOMINAL deficit — the rebound RELEASE
            // gate below (`deficit == 0.0`) is byte-identical: release is a
            // recovery signal (is there any shortfall at all?), not a
            // response magnitude.
            FoodStateKind foodState = FoodState.Of(prev, settlement, _cfg, out _);
            double dEff = FoodState.EffectiveDeficit(deficit, foodState, _cfg);
            double suppression = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * dEff);
            for (int c = 0; c < Cohorts.Count; c++)
            {
                starveRate[c] = StarvationRate(d, c, dEff);
                totalRate[c] = d.MortalityPerYear[c] + starveRate[c];
            }

            // Seed the micro-state from PRESENT integer counts; zero the flow
            // accumulators for this settlement's rows.
            for (int i = 0; i < rowCount; i++)
            {
                if (buckets[i].Settlement != settlement) continue;
                pop[i] = buckets[i].Count.Value;
                birthsExact[i] = deathsExact[i] = starveExact[i] = agingExact[i] = 0.0;
            }

            // T4.21-3 headroom growth cap, once per settlement per turn (header):
            // N_lim from PREV (one definition, FoodHeadroom.Limit); N_now over
            // the OWNED buckets at step start; H_0 = max(0, N_lim − N_now).
            // +∞ ⇒ the cap is skipped ENTIRELY (the fed path below executes the
            // pre-T4.21 instruction sequence).
            double nLim = FoodHeadroom.Limit(prev, settlement, _cohortWeights, _baskets);
            bool capped = !double.IsPositiveInfinity(nLim);
            double hRem = 0.0;
            if (capped)
            {
                double nNow = 0.0;
                for (int i = 0; i < rowCount; i++)
                {
                    if (buckets[i].Settlement != settlement) continue;
                    nNow += _cohortWeights[buckets[i].CohortIdx] * pop[i];
                }
                hRem = Math.Max(0.0, nLim - nNow);
            }

            // --- the micro-loop -------------------------------------------
            double remaining = dt;
            while (remaining > 1e-9)
            {
                double h = Math.Min(MicroStepYears, remaining);
                remaining -= h;
                double advance = h / Cohorts.WidthYears; // ADR-010 fractional slot

                // Per GROUP (anchored at cohort-0 rows): births first, from
                // pre-sink populations (order pinned; see header).
                // PASS A (T4.21-3): the uncapped candidate per group — no
                // state mutation. The same add and the same multiply as the
                // pre-T4.21 bank/release lines, so the committed arithmetic in
                // PASS B is bit-identical when the cap does not bind.
                double sumCand = 0.0;
                for (int i = 0; i < rowCount; i++)
                {
                    if (buckets[i].Settlement != settlement || buckets[i].CohortIdx != 0) continue;
                    BucketRow anchor = buckets[i];

                    double unsuppressed = 0.0;
                    for (int j = 0; j < rowCount; j++)
                    {
                        BucketRow p = buckets[j];
                        if (!SameGroup(p, anchor)) continue;
                        double f = d.FertilityPerPersonPerYear[p.CohortIdx];
                        if (f <= 0.0) continue;
                        unsuppressed += f * pop[j] * W(totalRate[p.CohortIdx] * h) * h;
                    }

                    double born = unsuppressed * suppression;
                    // The bank is taken from the UNCAPPED pair: headroom-
                    // withheld births are not deferred conceptions.
                    double bank = d.ReboundRecoverableFraction * (unsuppressed - born);
                    double release = 0.0;
                    if (deficit == 0.0 && unsuppressed > 0.0)
                    {
                        release = (anchor.ReboundReservoir + bank)
                                  * Math.Min(1.0, d.ReboundReleaseRatePerYear * h);
                    }
                    unsuppressedA[i] = unsuppressed;
                    bankA[i] = bank;
                    releaseA[i] = release;
                    candA[i] = born + release;
                    sumCand += candA[i];
                }

                // CAP (T4.21-3, header): skipped entirely at N_lim = +∞.
                double m = 1.0;
                double nutritionBefore = 0.0;
                if (capped)
                {
                    double deathsPre = 0.0, agingDrift = 0.0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (buckets[i].Settlement != settlement) continue;
                        int c = buckets[i].CohortIdx;
                        double w = _cohortWeights[c];
                        double survive = Math.Exp(-totalRate[c] * h);
                        nutritionBefore += w * pop[i];
                        deathsPre += pop[i] * (1.0 - survive) * w;
                        if (c >= Cohorts.Count - 1) continue;                       // 75+ absorbs
                        if (FindInGroup(buckets, buckets[i], c + 1) < 0) continue; // mirrors the aging guard
                        agingDrift += pop[i] * survive * advance * (_cohortWeights[c + 1] - w);
                    }
                    double allowed = (1.0 - Math.Exp(-k * h)) * hRem;
                    double newbornNet = W(totalRate[0] * h) * Math.Exp(-totalRate[0] * h) * _cohortWeights[0];
                    double bornMax = (allowed + deathsPre - agingDrift) / newbornNet;
                    m = sumCand > 0.0 ? Math.Min(1.0, Math.Max(0.0, bornMax) / sumCand) : 1.0;
                }

                // PASS B: commit per group — bank, (scaled) release, births,
                // the newborn credit. Guarded scaling: the fed path executes
                // the pre-T4.21 instruction sequence.
                for (int i = 0; i < rowCount; i++)
                {
                    if (buckets[i].Settlement != settlement || buckets[i].CohortIdx != 0) continue;

                    double born = candA[i];
                    ref BucketRow reservoirRow = ref buckets.Ref(i);
                    reservoirRow.ReboundReservoir += bankA[i];
                    if (deficit == 0.0 && unsuppressedA[i] > 0.0)
                    {
                        double release = releaseA[i];
                        if (m < 1.0) { born *= m; release *= m; }
                        reservoirRow.ReboundReservoir -= release;
                    }
                    else if (m < 1.0)
                    {
                        born *= m;
                    }

                    // Live births recorded; in-step infant deaths at the
                    // newborn cohort's rate; survivors join cohort 0.
                    birthsExact[i] += born;
                    double survivors = born * W(totalRate[0] * h);
                    deathsExact[i] += born - survivors;
                    pop[i] += survivors;
                }

                // Sinks then aging, per row. Aging descending is cascade-free
                // (arrivals land on already-processed higher cohorts).
                for (int i = rowCount - 1; i >= 0; i--)
                {
                    if (buckets[i].Settlement != settlement) continue;
                    int c = buckets[i].CohortIdx;

                    double dead = pop[i] * (1.0 - Math.Exp(-d.MortalityPerYear[c] * h));
                    pop[i] -= dead;
                    deathsExact[i] += dead;
                    double starved = pop[i] * (1.0 - Math.Exp(-starveRate[c] * h));
                    pop[i] -= starved;
                    starveExact[i] += starved;

                    if (c >= Cohorts.Count - 1) continue; // 75+ absorbs
                    double moving = pop[i] * advance;
                    int destRow = FindInGroup(buckets, buckets[i], c + 1);
                    if (destRow < 0) continue; // row never founded — nobody to receive
                    pop[i] -= moving;
                    pop[destRow] += moving;
                    agingExact[i] += moving;
                }

                // T4.21-3: the remaining headroom absorbs the REALISED
                // nutritional growth of the step (aging drift included), so
                // that when binding H_rem(end) = H_0 e^(−k dt) exactly,
                // however dt is cut into steps.
                if (capped)
                {
                    double nutritionAfter = 0.0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (buckets[i].Settlement != settlement) continue;
                        nutritionAfter += _cohortWeights[buckets[i].CohortIdx] * pop[i];
                    }
                    hRem -= nutritionAfter - nutritionBefore;
                }
            }

            // --- integer reconciliation, once per turn --------------------
            // Pinned order: births → aging ASCENDING → deaths → starvation.
            // Ascending transfers are the availability chain: a person who
            // crossed two slots in the micro-state contributed to BOTH rows'
            // outflows, so each row must receive its arrivals before passing
            // its own through-flow on (descending clamps pass-through rows to
            // zero and squashes the age distribution — the bug the dt-10 vs
            // 2×dt-5 bisection caught). Sinks run last: after the transfers,
            // everyone's integer body rests in the row their micro-deaths
            // were attributed to. Flows floor through the rows' existing
            // D-004 remainder accumulators; ClampToAvailable backstops.
            long vitalBirths = 0, vitalDeaths = 0;
            for (int i = 0; i < rowCount; i++)
            {
                if (buckets[i].Settlement != settlement || birthsExact[i] <= 0.0) continue;
                ref BucketRow row = ref buckets.Ref(i);
                double exact = birthsExact[i] + row.BirthRemainder;
                long born = ConservedMath.WholeUnits(exact, $"births (settlement {settlement.Value}, bucket {i})");
                ctx.Ledger.Flow(
                    ref row.Count, ConservedQuantityIds.Population, ReasonIds.Births,
                    born, FlowDirection.Source, OverdrawPolicy.Throw);
                row.BirthRemainder = exact - born;
                vitalBirths += born;
            }
            for (int i = 0; i < rowCount; i++)
            {
                if (buckets[i].Settlement != settlement || agingExact[i] <= 0.0) continue;
                BucketRow key = buckets[i];
                int destRow = FindInGroup(buckets, key, key.CohortIdx + 1);
                if (destRow < 0) continue;
                ref BucketRow row = ref buckets.Ref(i);
                double exact = agingExact[i] + row.AgingRemainder;
                long moving = ConservedMath.WholeUnits(exact, $"cohort aging (settlement {settlement.Value}, bucket {i})");
                row.AgingRemainder = exact - moving;
                if (moving > 0)
                {
                    ctx.Ledger.Transfer(
                        ref buckets.Ref(i).Count, ref buckets.Ref(destRow).Count,
                        moving, OverdrawPolicy.ClampToAvailable);
                }
            }
            for (int i = 0; i < rowCount; i++)
            {
                if (buckets[i].Settlement != settlement) continue;
                vitalDeaths += SinkExact(ctx, buckets, i, deathsExact[i], ReasonIds.Deaths);
                vitalDeaths += SinkExact(ctx, buckets, i, starveExact[i], ReasonIds.Starvation);
            }

            vitals.Add(new SettlementVitalsRow(settlement, vitalBirths, vitalDeaths, dt));
        }
    }

    private static double StarvationRate(DemographicsConfig d, int cohortIdx, double deficit)
    {
        double multiplier = BandViews.IsChild(cohortIdx) ? d.StarvationChildMultiplier
            : BandViews.IsElder(cohortIdx) ? d.StarvationElderMultiplier : 1.0;
        return d.StarvationMortalityMaxPerYear * deficit * multiplier;
    }

    private static bool SameGroup(in BucketRow a, in BucketRow b) =>
        a.Settlement == b.Settlement && a.Culture == b.Culture
        && a.Religion == b.Religion && a.Class == b.Class;

    private static int FindInGroup(Table<BucketRow> buckets, in BucketRow key, int cohortIdx)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].CohortIdx == cohortIdx && SameGroup(buckets[i], key)) return i;
        }
        return -1;
    }

    /// <summary>Sinks the micro-integrated exact amount from the row via its
    /// remainder accumulator; returns the ACTUAL count sunk (after any clamp)
    /// for the vitals chronicle.</summary>
    private static long SinkExact(
        SimContext<DemographicsTables> ctx, Table<BucketRow> buckets,
        int index, double exactAmount, ReasonId reason)
    {
        if (exactAmount <= 0.0
            && (reason == ReasonIds.Deaths ? buckets[index].DeathRemainder : buckets[index].StarvationRemainder) <= 0.0)
            return 0;
        ref BucketRow row = ref buckets.Ref(index);
        double exact = exactAmount
                       + (reason == ReasonIds.Deaths ? row.DeathRemainder : row.StarvationRemainder);
        long sunk = ConservedMath.WholeUnits(exact, $"deaths/starvation (reason {reason.Value}, bucket {index})");
        long before = row.Count.Value;
        ctx.Ledger.Flow(
            ref row.Count, ConservedQuantityIds.Population, reason,
            sunk, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        double remainder = exact - sunk; // sub-person fraction only (see header)
        if (reason == ReasonIds.Deaths) row.DeathRemainder = remainder;
        else row.StarvationRemainder = remainder;
        return before - row.Count.Value;
    }
}
