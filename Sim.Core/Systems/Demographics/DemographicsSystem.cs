using Sim.Core.Kernel;
using Sim.Core.State;

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
///      deficit) multiplies fertility; suppressed exact births bank into the
///      ReboundReservoir and release on fed turns at the TUNE rate — all at
///      micro-scale, dt-correct by construction.
///   1. Base deaths — pop_c × (1 − e^(−m_c·h)), then
///   2. Starvation — remaining × (1 − e^(−s_c·h)), s_c = max rate × PREV
///      deficit × age multiplier (sequential exponential sinks compose to
///      e^(−(m+s)h) regardless of order; the order is pinned).
///      Mortality acts on PRESENT counts (ADR-011 §1): per-capita and
///      position-independent — people moved by an earlier system this turn
///      die where they stand; the Prev-sized dodge class is structurally
///      dead. The deficit is LAST turn's Consumption output (§3.2 — signals
///      are Prev-read; only the integration acts on present stocks).
///   3. Aging — ADR-010 slot-advance at micro-scale: h/width of each cohort
///      advances one slot (descending, cascade-free); 75+ absorbs. At
///      h < width this is the fractional-advance kernel ADR-010 defines.
///   4. Newborn credit — into cohort 0 (h < width always), surviving the
///      in-step w(λ_0·h) factor; the shortfall is an infant Death (Births
///      counts live births, Deaths includes in-step infant deaths). Newborns
///      then age upward through the same micro-kernel — the coarse-dt
///      "newborn cohort spread" of the T2.1 kernel is SUPERSEDED: the spread
///      now emerges from integration instead of being imposed.
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
public sealed class DemographicsSystem(SimConfig cfg) : ISimSystem<DemographicsTables>
{
    public static readonly SystemId WellKnownId = new(7);
    public const string Name = "demographics";

    /// <summary>The fixed demographic micro-step (sim-years): the finest dt
    /// in the canonical era table, dividing every band's dt exactly. This is
    /// the kernel's integration scale, not TUNE — changing it redefines the
    /// integrator (ADR-011).</summary>
    public const double MicroStepYears = 0.5;

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    /// <summary>w(x) = (1 − e^(−x))/x, the uniform-exposure survival kernel;
    /// w(0) = 1 (guarded below the double-precision floor, deterministic).</summary>
    internal static double W(double x) => x < 1e-12 ? 1.0 : (1.0 - Math.Exp(-x)) / x;

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
            double suppression = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * deficit);
            for (int c = 0; c < Cohorts.Count; c++)
            {
                starveRate[c] = StarvationRate(d, c, deficit);
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

            // --- the micro-loop -------------------------------------------
            double remaining = dt;
            while (remaining > 1e-9)
            {
                double h = Math.Min(MicroStepYears, remaining);
                remaining -= h;
                double advance = h / Cohorts.WidthYears; // ADR-010 fractional slot

                // Per GROUP (anchored at cohort-0 rows): births first, from
                // pre-sink populations (order pinned; see header).
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
                    ref BucketRow reservoirRow = ref buckets.Ref(i);
                    reservoirRow.ReboundReservoir +=
                        d.ReboundRecoverableFraction * (unsuppressed - born);
                    if (deficit == 0.0 && unsuppressed > 0.0)
                    {
                        double release = reservoirRow.ReboundReservoir
                                         * Math.Min(1.0, d.ReboundReleaseRatePerYear * h);
                        reservoirRow.ReboundReservoir -= release;
                        born += release;
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
                long born = (long)Math.Floor(exact);
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
                long moving = (long)Math.Floor(exact);
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
        long sunk = (long)Math.Floor(exact);
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
