using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Migration;

/// <summary>Writable handles to MigrationSystem's tables (built by
/// SystemCatalog only). Buckets is SHARED with Demographics and ClassMobility
/// (see SystemCatalog); MigrationFlows is this system's chronicle table.</summary>
public readonly record struct MigrationTables(
    Table<BucketRow> Buckets, Table<MigrationFlowRow> Flows);

/// <summary>
/// Migration (T2.5, m2 spec §3 / D-021 Exit valve): people are Ledger.Transfers
/// of buckets between settlements — migrants keep their FULL bucket key
/// (culture, religion, class, cohort travel together; the destination bucket is
/// the same key at the destination settlement). Everything reads Prev (§3.2).
///
/// DRIVER, per source bucket and destination:
///   desired/yr = BaseRatePerYear × CohortProfile[cohort] × PREV count
///                × damping(i→j) × (gap(i→j) + FamineFlightFactor × deficit_i)
///   gap      = max(0, A_j − A_i), A = FoodWeight × food/capita + LandWeight ×
///              farmland/capita (Prev stores + Prev catchment summaries; capita
///              floored at 1 so an empty settlement never divides by zero).
///   damping  = exp(−travelCost / DampingDecayCost) from Prev
///              SettlementDistances; an UNREACHABLE pair stores +∞ and
///              exp(−∞) = 0 — zero flow BY CONSTRUCTION, not by branch.
///   deficit  = the source's PREV consumption-deficit ratio — the famine
///              flight term is gap-INDEPENDENT (D-021: starving people leave
///              for anywhere reachable, weighted by damping alone when no gap
///              is positive).
///
/// OVERDRAW DISCIPLINE: desired outflows to ALL destinations are computed from
/// Prev first; if their sum exceeds the bucket's PREV count they are scaled
/// proportionally (scale = count / Σdesired). Transfers then execute in the
/// PINNED ascending (source, dest, bucket-key) order — source settlements
/// ascending, destinations ascending inside, buckets in table order (the
/// founding bucket-key order) innermost — through the per-source-row
/// MigrationRemainder (fractional outflow is conserved in TOTAL; sub-person
/// destination attribution rides the carry in visit order — documented).
/// ClampToAvailable backstops the floors: a bucket can hit exactly zero,
/// never negative, and nobody moves who does not exist.
///
/// CHRONICLE HOOKS: per-settlement Inflow/Outflow totals for THIS turn are
/// rebuilt into MigrationFlows every step (T2.9 reads them for surge events).
/// Slots after ClassMobility, before Demographics (m2 spec §3 pipeline).
/// STATELESS: config is immutable tuning, not state. No RNG.
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
        if (n < 2) return;

        // --- Prev-derived per-settlement signals -----------------------------
        var attractiveness = new double[n];
        var deficit = new double[n];
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
            long capita = Math.Max(pop, 1);

            long food = 0;
            for (int i = 0; i < prev.FoodStores.Count; i++)
                if (prev.FoodStores[i].Settlement == id) { food = prev.FoodStores[i].Store.Value; break; }
            double farmland = 0.0;
            for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
                if (prev.CatchmentSummaries[i].Settlement == id)
                { farmland = prev.CatchmentSummaries[i].EffectiveFarmland; break; }
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
                if (prev.ConsumptionDeficits[i].Settlement == id)
                { deficit[s] = prev.ConsumptionDeficits[i].DeficitRatio; break; }

            attractiveness[s] = m.AttractivenessFoodWeight * (food / (double)capita)
                                + m.AttractivenessLandWeight * (farmland / (double)capita);
        }

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

        // --- desired outflows (all from Prev), then proportional scaling -----
        // desiredTotal[bucketRow] = Σ_j desired; perDest factor recomputed in
        // the transfer loop from the same Prev inputs (bit-identical products).
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
                    total += perCount * damping[src, dst]
                             * (Math.Max(0.0, attractiveness[dst] - attractiveness[src])
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
                double push = damping[src, dst]
                              * (Math.Max(0.0, attractiveness[dst] - attractiveness[src])
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
