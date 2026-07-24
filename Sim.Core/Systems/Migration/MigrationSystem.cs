using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Migration;

/// <summary>Writable handles to MigrationSystem's tables (built by
/// SystemCatalog only). Buckets is SHARED with Demographics and ClassMobility
/// (see SystemCatalog); MigrationFlows is this system's chronicle table;
/// SmoothedAttractiveness is its persistent EMA filter state (T2.8).</summary>
public readonly record struct MigrationTables(
    Table<BucketRow> Buckets, Table<MigrationFlowRow> Flows,
    Table<SmoothedAttractivenessRow> Smoothed);

/// <summary>
/// Migration (T2.5, m2 spec §3 / D-021 Exit valve; STABILIZED at T2.8 by
/// director ruling — the ping-pong attractor was a paired-feedback violation):
/// people are Ledger.Transfers of buckets between settlements — migrants keep
/// their FULL bucket key. Everything reads Prev (§3.2).
///
/// DRIVER, per source bucket and destination:
///   desired/yr = BaseRatePerYear × CohortProfile[cohort] × PREV count
///                × damping(i→j) × viability(j)
///                × (gapScale(i→j) × gap(i→j) + FamineFlightFactor × deficit_i)
///   gap       = max(0, S_j − S_i) over the SMOOTHED attractiveness S (below).
///   damping   = exp(−travelCost / DampingDecayCost) from Prev
///               SettlementDistances; an UNREACHABLE pair stores +∞ and
///               exp(−∞) = 0 — zero flow BY CONSTRUCTION, not by branch.
///   viability = max(0, 1 − DestinationDeficitRepulsion × deficit_j)
///               (T2.13, director packet — the STARVATION-MAGNETISM fix):
///               migrants know whether the destination can feed them, so the
///               DESTINATION's PREV deficit gates every arriving flow. At
///               deficit 1.0 a settlement receives EXACTLY ZERO migrants —
///               attractiveness may still read high (land per capita), but an
///               empty granary repels regardless of how empty the land is.
///               The M2 exit session exposed the inversion this kills: an
///               emptied, food-less settlement's per-capita land made it the
///               world's strongest magnet, and famine flight (destination-
///               blind, damping-only) funneled refugees INTO the famine —
///               1,520 arrivals / 884 same-turn deaths in one turn at the
///               director's settlement 3, circulating among the starving
///               cluster indefinitely. Viability multiplies BOTH channels:
///               "flee a starving settlement" survives intact (see below);
///               "walk into a starving settlement" is dead by construction.
///   deficit_i = the source's PREV consumption-deficit ratio — famine flight
///               stays gap-INDEPENDENT (D-021: starving people leave for
///               anywhere reachable AND VIABLE) and is deliberately NOT
///               gap-capped: the Exit valve is a surge by design, bounded by
///               the overdraw scaler alone. When every reachable destination
///               is itself starving, flight goes to zero: there is no exodus
///               without a destination — people die at home instead of
///               circulating between ruins (the exit-session pathology).
///
/// T2.8 STABILIZATION — the market-mandate pattern applied to people, BOTH:
/// (a) DAMPED FLOWS (gap-closing cap): with A = R/P (R = FoodWeight × food +
///     LandWeight × farmland, P = population), the pairwise flow that would
///     EQUALIZE instantaneous per-capita attractiveness has the closed form
///       m* = (R_j × P_i − R_i × P_j) / (R_i + R_j),  taken at max(0, ·).
///     The pair's total gap-driven desire is scaled so it never exceeds
///     GapClosingFraction × m* — at f < 1 the post-move gap keeps its sign,
///     so overshoot is STRUCTURALLY impossible at the pair level. The cap
///     reads INSTANTANEOUS physics while desire reads the SMOOTHED signal:
///     right after a large move the instantaneous m* says "equalized" and
///     the cap zeroes further flow even while the EMA still remembers a gap.
///     (Multiple sources can share one destination; with f well below 1 and
///     the ascending-pair execution order the collective inflow stays inside
///     the basin — pinned empirically by the oscillation regression tests.)
/// (b) ATTRACTIVENESS SMOOTHING: S is a first-order low-pass over A —
///       S += (A − S) × min(1, dt / WindowYears)
///     (per-year time constant, integrated with dtYears, factor clamped at 1
///     for dt ≥ τ). Persistent filter state in the SmoothedAttractiveness
///     table; a settlement's first sighting initializes S = A (the filter
///     starts converged). A one-turn emptying can no longer mint a one-turn
///     magnet.
/// (c) A separate crowding-saturation term was CONSIDERED AND DECLINED: the
///     gap-closing cap already encodes diminishing pull — every arrival
///     lowers the destination's per-capita draw and shrinks m* — so a third
///     term would be a free-floating modifier stacked on a mechanism that
///     already saturates (law 2).
///
/// OVERDRAW DISCIPLINE: desired outflows to ALL destinations are computed from
/// Prev first (gap components pre-scaled by their pair caps); if their sum
/// exceeds the bucket's PREV count they are scaled proportionally. Transfers
/// then execute in the PINNED ascending (source, dest, bucket-key) order
/// through the per-source-row MigrationRemainder. ClampToAvailable backstops
/// the floors: a bucket can hit exactly zero, never negative.
///
/// CHRONICLE HOOKS: per-settlement Inflow/Outflow totals rebuilt into
/// MigrationFlows every step. Slots after ClassMobility, before Demographics.
/// STATELESS except the EMA filter rows (world state, not system state).
/// No RNG.
/// </summary>
public sealed class MigrationSystem(SimConfig cfg) : ISimSystem<MigrationTables>
{
    public static readonly SystemId WellKnownId = new(10);
    public const string Name = "migration";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<MigrationTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        MigrationConfig m = _cfg.Migration;
        int n = prev.Settlements.Count;

        // Chronicle rows exist (zeroed) every turn, even a no-flow one.
        Table<MigrationFlowRow> flows = ctx.Owned.Flows;
        flows.Clear();
        for (int s = 0; s < n; s++)
            flows.Add(new MigrationFlowRow(prev.Settlements[s].Id, 0, 0));

        // --- Prev-derived per-settlement signals -----------------------------
        var resources = new double[n];    // R = fw × food + lw × farmland
        var population = new long[n];     // P (raw, no floor — m* uses physics)
        var instant = new double[n];      // A = R / max(P, 1)
        var deficit = new double[n];
        var anyFood = new bool[n];        // T2.13: store > 0 OR last harvest > 0
        int maxId = 0;
        for (int s = 0; s < n; s++) maxId = Math.Max(maxId, prev.Settlements[s].Id.Value);
        var settlementIndex = new int[maxId + 1]; // id → row index (array, law 5: no dictionaries in sim logic)
        Array.Fill(settlementIndex, -1);
        for (int s = 0; s < n; s++)
        {
            SettlementId id = prev.Settlements[s].Id;
            settlementIndex[id.Value] = s;

            long pop = 0;
            for (int i = 0; i < prev.Buckets.Count; i++)
                if (prev.Buckets[i].Settlement == id) pop += prev.Buckets[i].Count.Value;
            population[s] = pop;

            long food = 0, lastHarvest = 0;
            for (int i = 0; i < prev.FoodStores.Count; i++)
                if (prev.FoodStores[i].Settlement == id)
                { food = prev.FoodStores[i].Store.Value; lastHarvest = prev.FoodStores[i].LastHarvestUnits; break; }
            anyFood[s] = food > 0 || lastHarvest > 0;
            double farmland = 0.0;
            for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
                if (prev.CatchmentSummaries[i].Settlement == id)
                { farmland = prev.CatchmentSummaries[i].EffectiveFarmland; break; }
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
                if (prev.ConsumptionDeficits[i].Settlement == id)
                { deficit[s] = prev.ConsumptionDeficits[i].DeficitRatio; break; }

            resources[s] = m.AttractivenessFoodWeight * food + m.AttractivenessLandWeight * farmland;
            instant[s] = resources[s] / Math.Max(pop, 1);
        }

        // T2.13: destination viability — TWO gates, both from Prev, both
        // multiplying every pairwise flow below (both channels):
        //   1. The deficit gate: max(0, 1 − Repulsion × deficit_dst) — a
        //      settlement in famine repels in proportion to its hunger.
        //   2. The ABSOLUTE food gate: no store AND no harvest ⇒ viability 0
        //      regardless of the deficit signal. Without it, an EMPTY ruin is
        //      a trap: zero population means zero demand means the deficit
        //      READS 0.00, while land-per-capita (floor 1) reads astronomical
        //      — the exit session's resurrection cycle (die → deficit resets
        //      → 144 colonists in one turn → starve on the stale signal →
        //      die → repeat every ~9 turns). An empty granary on unfarmed
        //      land repels no matter how empty the land is.
        var viability = new double[n];
        for (int s = 0; s < n; s++)
            viability[s] = anyFood[s]
                ? Math.Max(0.0, 1.0 - m.DestinationDeficitRepulsion * deficit[s])
                : 0.0;

        // --- EMA filter update (T2.8 b): PREV smoothed → owned smoothed ------
        // The owned table is the cloned prev table; rows update in place, and
        // a settlement without a row (first sighting) appends one initialized
        // AT the instantaneous value, in ascending settlement-row order.
        Table<SmoothedAttractivenessRow> smoothedTable = ctx.Owned.Smoothed;
        var smoothed = new double[n];
        double alpha = Math.Min(1.0, ctx.DtYears / m.AttractivenessSmoothingWindowYears);
        for (int s = 0; s < n; s++)
        {
            SettlementId id = prev.Settlements[s].Id;
            int rowIdx = -1;
            for (int i = 0; i < smoothedTable.Count; i++)
                if (smoothedTable[i].Settlement == id) { rowIdx = i; break; }
            double prevSmoothed = rowIdx >= 0 ? smoothedTable[rowIdx].Value : instant[s];
            double value = prevSmoothed + (instant[s] - prevSmoothed) * alpha;
            if (rowIdx >= 0) smoothedTable[rowIdx] = smoothedTable[rowIdx] with { Value = value };
            else smoothedTable.Add(new SmoothedAttractivenessRow(id, value));
            smoothed[s] = value;
        }
        if (n < 2) return;

        // Damping matrix from Prev distances (missing row — e.g. before the
        // first catchment recompute — is unreachable: damping 0, no flow).
        var damping = new double[n, n];
        for (int i = 0; i < prev.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = prev.SettlementDistances[i];
            int fi = row.From.Value <= maxId ? settlementIndex[row.From.Value] : -1;
            int ti = row.To.Value <= maxId ? settlementIndex[row.To.Value] : -1;
            if (fi >= 0 && ti >= 0)
                damping[fi, ti] = Math.Exp(-row.TravelCost / m.DampingDecayCost);
        }

        // Per-settlement bucket row indices, in table order (the bucket-key order).
        var bucketRows = new List<int>[n];
        for (int s = 0; s < n; s++) bucketRows[s] = [];
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            int sid = prev.Buckets[i].Settlement.Value;
            if (sid <= maxId && settlementIndex[sid] >= 0)
                bucketRows[settlementIndex[sid]].Add(i);
        }

        // --- T2.8 (a): per-pair gap-closing caps -----------------------------
        // gapScale[src,dst] scales the pair's ENTIRE gap-driven desire so it
        // never exceeds f × m*. Computed once from Prev; the transfer loop
        // recomputes the same product terms from the same inputs (association
        // differs at ULP level between the desire and transfer sites — a
        // pre-T2.13 pattern; ClampToAvailable backstops any ULP overdraw).
        var gapScale = new double[n, n];
        for (int src = 0; src < n; src++)
        {
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double gap = Math.Max(0.0, smoothed[dst] - smoothed[src]);
                if (gap <= 0.0 || damping[src, dst] <= 0.0 || viability[dst] <= 0.0)
                    continue; // no (viable) gap desire — scale moot

                // The pair's total gap-driven desire across every bucket.
                double gapDesire = 0.0;
                foreach (int row in bucketRows[src])
                {
                    BucketRow b = prev.Buckets[row];
                    gapDesire += m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                 * b.Count.Value * ctx.DtYears * damping[src, dst]
                                 * viability[dst] * gap;
                }
                if (gapDesire <= 0.0) continue;

                double denom = resources[src] + resources[dst];
                double equalizing = denom > 0.0
                    ? Math.Max(0.0, (resources[dst] * population[src] - resources[src] * population[dst]) / denom)
                    : 0.0;
                double cap = m.GapClosingFraction * equalizing;
                gapScale[src, dst] = gapDesire > cap ? cap / gapDesire : 1.0;
            }
        }

        // --- desired outflows (all from Prev), then proportional scaling -----
        // desiredTotal[bucketRow] = Σ_j (gap-capped + flight) desire; perDest
        // factors recomputed in the transfer loop (bit-identical products).
        var desiredTotal = new double[prev.Buckets.Count];
        for (int src = 0; src < n; src++)
        {
            foreach (int row in bucketRows[src])
            {
                BucketRow b = prev.Buckets[row];
                double perCount = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                  * b.Count.Value * ctx.DtYears;
                if (perCount <= 0.0) continue;
                double total = 0.0;
                for (int dst = 0; dst < n; dst++)
                {
                    if (dst == src) continue;
                    total += perCount * damping[src, dst] * viability[dst]
                             * (gapScale[src, dst] * Math.Max(0.0, smoothed[dst] - smoothed[src])
                                + m.FamineFlightFactor * deficit[src]);
                }
                desiredTotal[row] = total;
            }
        }

        // --- transfers, pinned ascending (source, dest, bucket-key) ----------
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        for (int src = 0; src < n; src++)
        {
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double push = damping[src, dst] * viability[dst]
                              * (gapScale[src, dst] * Math.Max(0.0, smoothed[dst] - smoothed[src])
                                 + m.FamineFlightFactor * deficit[src]);
                if (push <= 0.0) continue;

                for (int k = 0; k < bucketRows[src].Count; k++)
                {
                    int srcRow = bucketRows[src][k];
                    BucketRow b = prev.Buckets[srcRow];
                    long prevCount = b.Count.Value;
                    if (prevCount <= 0 && buckets.Ref(srcRow).MigrationRemainder == 0.0) continue;

                    double desired = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                     * prevCount * ctx.DtYears * push;
                    // Overdraw scaling: never ask for more than the bucket held.
                    double scale = desiredTotal[srcRow] > prevCount
                        ? prevCount / desiredTotal[srcRow] : 1.0;

                    ref BucketRow srcRef = ref buckets.Ref(srcRow);
                    double exact = desired * scale + srcRef.MigrationRemainder;
                    long moved = (long)Math.Floor(exact);
                    srcRef.MigrationRemainder = exact - moved; // sub-person fraction only
                    if (moved <= 0) continue;

                    // Same key at dest: founding lays out every settlement's
                    // buckets identically; both the k-index shortcut and the
                    // key check are GUARDED for hand-built worlds (review
                    // finding: an unguarded index crashed when a destination
                    // had fewer buckets than the source).
                    int dstRow = k < bucketRows[dst].Count ? bucketRows[dst][k] : -1;
                    if (dstRow >= 0)
                    {
                        BucketRow d = prev.Buckets[dstRow];
                        if (d.Culture != b.Culture || d.Religion != b.Religion
                            || d.Class != b.Class || d.CohortIdx != b.CohortIdx) dstRow = -1;
                    }
                    if (dstRow < 0) dstRow = FindBucket(buckets, prev.Settlements[dst].Id, b);
                    if (dstRow < 0)
                    {
                        // No matching bucket — nobody can arrive. Restore the
                        // floored amount to the remainder (review finding: the
                        // intent was silently discarded, biasing outflow low).
                        srcRef.MigrationRemainder += moved;
                        continue;
                    }

                    long before = buckets.Ref(srcRow).Count.Value;
                    ctx.Ledger.Transfer(
                        ref buckets.Ref(srcRow).Count, ref buckets.Ref(dstRow).Count,
                        moved, OverdrawPolicy.ClampToAvailable);
                    long actuallyMoved = before - buckets.Ref(srcRow).Count.Value;

                    if (actuallyMoved > 0)
                    {
                        flows[src] = flows[src] with { Outflow = flows[src].Outflow + actuallyMoved };
                        flows[dst] = flows[dst] with { Inflow = flows[dst].Inflow + actuallyMoved };
                    }
                }
            }
        }
    }

    private static int FindBucket(Table<BucketRow> buckets, SettlementId settlement, in BucketRow key)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            BucketRow b = buckets[i];
            if (b.Settlement == settlement && b.Culture == key.Culture
                && b.Religion == key.Religion && b.Class == key.Class
                && b.CohortIdx == key.CohortIdx) return i;
        }
        return -1;
    }
}
