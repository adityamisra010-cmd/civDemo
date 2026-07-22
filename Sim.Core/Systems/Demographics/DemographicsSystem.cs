using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Demographics;

/// <summary>Writable handles to DemographicsSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct DemographicsTables(Table<BucketRow> Buckets);

/// <summary>
/// Demographics (T1.5, cohortized at T2.1 per D-026): births, mortality,
/// starvation, and cohort aging over the Buckets table — every person moving
/// exclusively through the Ledger (law 1). All rates are per-sim-year, computed
/// from PREV counts (§3.2), integrated with dtYears (law 3), and converted to
/// whole people through per-row D-004 remainder accumulators.
///
/// OPERATION ORDER IS PINNED (a semantics surface — under famine the
/// ClampToAvailable floors make outcomes depend on it): per settlement, per
/// bucket group (culture, religion, class in table order),
///   1. Births      — Σ_c fertility[c] × PREV count_c, sourced (reason Births)
///      into the cohorts a dt-window of newborns actually spans: children born
///      during a dt-year turn end it aged 0..dt, so cohort j in 0..k receives
///      the fraction of [0, dt) overlapping [j·width, (j+1)·width) (dt = 10,
///      width = 5 → half into cohort 0, half into cohort 1). Each credited
///      cohort carries its own BirthRemainder. Newborns inherit the group key
///      (peasant parents bear peasants).
///      FAMINE FERTILITY RESPONSE (T2.7): conceptions scale by
///      max(0, 1 − SuppressionSlope × PREV deficit) — famine defers births.
///      A TUNE fraction of the suppressed EXACT births banks into the group's
///      ReboundReservoir (cohort-0 row); on a FED turn (PREV deficit exactly
///      zero — the established fed-turn predicate) the reservoir drains at
///      ReboundReleaseRatePerYear back into the same births flow. Released
///      births can never exceed what was banked (deferred, NOT invented), and
///      release requires living fertile adults (birthsPerYear > 0) — a
///      reservoir cannot bear children into an extinct group.
///   2. Base deaths — sink per bucket (reason Deaths), mortality[c] × PREV.
///   3. Starvation  — sink per bucket (reason Starvation), max rate × PREV
///      deficit ratio × age multiplier × PREV count. The multiplier is
///      StarvationChildMultiplier on child cohorts, StarvationElderMultiplier
///      on elder cohorts, 1 on adults — famine age-selectivity, the mechanism
///      (not a modifier: it sits inside the resolution equation). The deficit
///      is LAST turn's Consumption output (one-turn lag, §3.2).
///   4. Aging       — SLOT-ADVANCE integration, processed DESCENDING cohort
///      (15 → 0): dt years of aging is dt/width cohort slots = k whole slots
///      + fraction f. From cohort c, floor(f × PREV + remainder) people move
///      k+1 slots and the REST of the cohort moves k slots (k = 0 → they
///      stay). Both moves are Ledger.Transfers (conserving by construction),
///      destinations clamp to the absorbing 75+ cohort, far move first
///      (pinned). Linear-rate aging is NOT dt-correct here: at Neolithic
///      dt = 10 a 1/width rate gives rate·dt = 2 — outside explicit Euler's
///      stable range, everyone ages at half speed. Slot-advance is exact for
///      dt a multiple of width and reduces to the linear rate for dt < width.
///
/// FLOORS (documented): every sink and transfer uses ClampToAvailable — a
/// starved settlement bottoms out at zero people in a bucket, never negative.
/// A clamp shortfall is NOT banked in the remainder (people who do not exist
/// cannot die later); remainders carry only sub-person fractions.
///
/// EXTINCTION RULING (director, T1.8): terminal extinction is ACCEPTED until
/// migration (T2.5) opens the recovery valves. A dead world must stay dead
/// CLEANLY: zero people → zero births, zero deaths, zero harvest, zero path
/// labor, static food, exact audit, no NaN, forever.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class DemographicsSystem(SimConfig cfg) : ISimSystem<DemographicsTables>
{
    public static readonly SystemId WellKnownId = new(7);
    public const string Name = "demographics";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<DemographicsTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        DemographicsConfig d = _cfg.Demographics;
        double dt = ctx.DtYears;

        // Newborn cohort spread for this dt (see header): shares over cohorts 0..k.
        Span<double> birthShare = stackalloc double[Cohorts.Count];
        int newbornCohorts = NewbornShares(dt, birthShare);

        // Slot-advance decomposition of dt years of aging.
        double slots = dt / Cohorts.WidthYears;
        int wholeSlots = (int)Math.Floor(slots);
        double fracSlot = slots - wholeSlots;

        // Ascending settlement-row order — the fixed iteration order (law 5).
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

            // 1. Births — per bucket group, in table order. Founding lays each
            // group out as a contiguous ascending cohort run; the group loop
            // keys off cohort-0 rows so a reordered table still groups correctly.
            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Settlement != settlement || buckets[i].CohortIdx != 0) continue;
                BucketRow anchor = buckets[i];

                double birthsPerYear = 0.0;
                for (int j = 0; j < prev.Buckets.Count; j++)
                {
                    BucketRow p = prev.Buckets[j];
                    if (SameGroup(p, anchor))
                        birthsPerYear += d.FertilityPerPersonPerYear[p.CohortIdx] * p.Count.Value;
                }

                // Famine fertility response (see header): suppress during a
                // deficit, bank a recoverable fraction, release when fed again.
                double unsuppressed = birthsPerYear * dt;
                double factor = Math.Max(0.0, 1.0 - d.FamineFertilitySuppressionSlope * deficit);
                double totalExact = unsuppressed * factor;
                ref BucketRow reservoirRow = ref buckets.Ref(i); // the cohort-0 anchor carries the bank
                reservoirRow.ReboundReservoir +=
                    d.ReboundRecoverableFraction * (unsuppressed - totalExact);
                if (deficit == 0.0 && birthsPerYear > 0.0)
                {
                    double release = reservoirRow.ReboundReservoir
                                     * Math.Min(1.0, d.ReboundReleaseRatePerYear * dt);
                    reservoirRow.ReboundReservoir -= release;
                    totalExact += release;
                }

                for (int c = 0; c < newbornCohorts; c++)
                {
                    int idx = FindInGroup(buckets, anchor, c);
                    if (idx < 0) continue; // group founded without this cohort row — nothing to credit
                    ref BucketRow row = ref buckets.Ref(idx);
                    double exact = totalExact * birthShare[c] + row.BirthRemainder;
                    long born = (long)Math.Floor(exact);
                    ctx.Ledger.Flow(
                        ref row.Count, ConservedQuantityIds.Population, ReasonIds.Births,
                        born, FlowDirection.Source, OverdrawPolicy.Throw);
                    row.BirthRemainder = exact - born;
                }
            }

            // 2. Base deaths, then 3. starvation — per bucket, table order.
            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Settlement != settlement) continue;
                long prevCount = prev.Buckets[i].Count.Value;
                int c = buckets[i].CohortIdx;

                Sink(ctx, buckets, i, d.MortalityPerYear[c], prevCount, ReasonIds.Deaths);

                double multiplier = BandViews.IsChild(c) ? d.StarvationChildMultiplier
                    : BandViews.IsElder(c) ? d.StarvationElderMultiplier : 1.0;
                Sink(ctx, buckets, i,
                    d.StarvationMortalityMaxPerYear * deficit * multiplier, prevCount,
                    ReasonIds.Starvation);
            }

            // 4. Aging — descending cohort within the table (the pinned order;
            // amounts come from PREV so order affects only clamp availability).
            for (int i = buckets.Count - 1; i >= 0; i--)
            {
                if (buckets[i].Settlement != settlement) continue;
                BucketRow key = buckets[i];
                int c = key.CohortIdx;
                if (c >= Cohorts.Count - 1) continue; // 75+ is absorbing
                long prevCount = prev.Buckets[i].Count.Value;

                // Far move: the fractional slot, through the remainder accumulator.
                double exact = fracSlot * prevCount + buckets.Ref(i).AgingRemainder;
                long far = (long)Math.Floor(exact);
                buckets.Ref(i).AgingRemainder = exact - far;
                int farDest = Math.Min(c + wholeSlots + 1, Cohorts.Count - 1);
                if (farDest != c && far > 0)
                    Transfer(ctx, buckets, i, FindInGroup(buckets, key, farDest), far);

                // Near move: with k >= 1 whole slots, everyone else advances k.
                if (wholeSlots >= 1)
                {
                    int nearDest = Math.Min(c + wholeSlots, Cohorts.Count - 1);
                    long near = prevCount - far;
                    if (nearDest != c && near > 0)
                        Transfer(ctx, buckets, i, FindInGroup(buckets, key, nearDest), near);
                }
            }
        }
    }

    /// <summary>Cohort shares of a dt-window of newborns (ages 0..dt at turn
    /// end, uniform): share_j = |[0,dt) ∩ [j·w,(j+1)·w)| / dt. Returns the
    /// number of cohorts with nonzero share.</summary>
    private static int NewbornShares(double dt, Span<double> shares)
    {
        shares.Clear();
        int n = 0;
        for (int j = 0; j < Cohorts.Count; j++)
        {
            double lo = j * Cohorts.WidthYears;
            if (lo >= dt) break;
            double hi = Math.Min(dt, lo + Cohorts.WidthYears);
            shares[j] = (hi - lo) / dt;
            n = j + 1;
        }
        return n;
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

    private static void Sink(
        SimContext<DemographicsTables> ctx, Table<BucketRow> buckets,
        int index, double ratePerYear, long prevCount, ReasonId reason)
    {
        ref BucketRow row = ref buckets.Ref(index);
        double exact = ratePerYear * prevCount * ctx.DtYears
                       + (reason == ReasonIds.Deaths ? row.DeathRemainder : row.StarvationRemainder);
        long sunk = (long)Math.Floor(exact);
        ctx.Ledger.Flow(
            ref row.Count, ConservedQuantityIds.Population, reason,
            sunk, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        double remainder = exact - sunk; // sub-person fraction only (see header)
        if (reason == ReasonIds.Deaths) row.DeathRemainder = remainder;
        else row.StarvationRemainder = remainder;
    }

    private static void Transfer(
        SimContext<DemographicsTables> ctx, Table<BucketRow> buckets,
        int fromIndex, int toIndex, long amount)
    {
        if (toIndex < 0) return; // destination row never founded — nothing to receive it
        ctx.Ledger.Transfer(
            ref buckets.Ref(fromIndex).Count, ref buckets.Ref(toIndex).Count,
            amount, OverdrawPolicy.ClampToAvailable);
    }
}
