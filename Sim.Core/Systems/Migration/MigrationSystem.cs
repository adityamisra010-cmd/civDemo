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
///   damping   = exp(−travelCost / DampingDecayCostUnits) from Prev
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
/// T4.10 — THE FOOD TERM IS GONE FROM ATTRACTIVENESS (director ruling,
/// Option A). R was `FoodWeight × food + LandWeight × farmland`, where `food`
/// was the raw grain STOCK. T4.2 bounded that stock (spoilage + granary
/// capacity), which destroyed its meaning as an attractiveness signal: an
/// adequately-fed settlement and a moderately food-SHORT one both converge
/// toward near-zero post-consumption stock, so stock magnitude stopped
/// distinguishing the two cases migration most needs to tell apart. The
/// replacement signal considered (1 − DeficitRatio) was measured IDENTICALLY
/// 1.0 across the canonical world — 4 seeds × 3 checkpoints × 12 settlements,
/// DeficitRatio exactly 0 everywhere — so it contributes zero differential and
/// no coefficient for it can be derived, validated or refuted (cr-003's
/// unfalsifiable-constant bar). Rather than ship an underivable weight, the
/// term is REMOVED:
///
///     R = LandWeight × farmland
///
/// LAND OPPORTUNITY sets baseline destination attractiveness. FOOD acts on
/// migration through the mechanisms that already exist and are already
/// ratified — famine flight (source push), destination-deficit repulsion and
/// the absolute food gate (both in `viability`) — NOT through a fourth
/// channel. This is the same reasoning as (c) below, applied to hunger: a
/// graded food term inside the gap would be a stacked modifier on a mechanism
/// that already handles hunger twice. The measured cost of the removal is
/// small BY CONSTRUCTION and was quantified before the change: at canonical
/// seed 1 turn 100 the food term supplied ~0.7 % of the attractiveness
/// differential (0.000286 against land's 0.041063) — post-T4.2 the world was
/// already ~97 % land-driven. Full derivation and measurements:
/// docs/t4.10-review-record.md.
///
/// T2.8 STABILIZATION — the market-mandate pattern applied to people, BOTH:
/// (a) DAMPED FLOWS (gap-closing cap): with A = R/P (R = LandWeight ×
///     farmland, P = population), the pairwise flow that would
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

    /// <summary>The grain stock, read ONLY for the T2.13 ABSOLUTE FOOD GATE
    /// (`anyFood`) that zeroes a destination's viability when it has neither a
    /// store nor a harvest. T4.10 removed the attractiveness food term, so the
    /// stock no longer feeds R — but the gate still needs to know whether there
    /// is any food at all, which is a presence test, not a magnitude one.</summary>
    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentException("MigrationSystem requires SimConfig.Goods (goods.json) at M3."));

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
        var resources = new double[n];    // R = lw × farmland (T4.10: food term removed)
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
            for (int i = 0; i < prev.GoodStocks.Count; i++)
                if (prev.GoodStocks[i].Settlement == id && prev.GoodStocks[i].Good == _grain)
                { food = prev.GoodStocks[i].Amount.Value; lastHarvest = prev.GoodStocks[i].LastProducedUnits; break; }
            anyFood[s] = food > 0 || lastHarvest > 0;
            // T3.2b: fertility-weighted km² (was fertility-weighted nodes; the
            // paired AttractivenessLandWeight was re-denominated by the same
            // 1/256 in sim.json, so the product is unchanged).
            double arableKm2 = 0.0;
            for (int i = 0; i < prev.CatchmentSummaries.Count; i++)
                if (prev.CatchmentSummaries[i].Settlement == id)
                { arableKm2 = prev.CatchmentSummaries[i].EffectiveArableKm2; break; }
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
                if (prev.ConsumptionDeficits[i].Settlement == id)
                { deficit[s] = prev.ConsumptionDeficits[i].DeficitRatio; break; }

            // T4.10: LAND ONLY. `food` above is still read, but only for the
            // absolute food gate below — never for attractiveness magnitude.
            resources[s] = m.AttractivenessLandWeight * arableKm2;
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
        //   3. T4.13 — HAPPINESS, and it is deliberately the WEAKEST of the
        //      three. People prefer to move somewhere that is bearable, but
        //      material survival decides first: this term MULTIPLIES the two
        //      gates above rather than joining them, so it can shade a choice
        //      between comparable destinations and can never rescue one the
        //      food gates have already closed. A destination in famine is
        //      viability 0 however content it looks, which is what keeps
        //      severe famine from being cured by moving people into it.
        //
        //      The factor is (1 − w) + w·happiness with happiness in [0,1]:
        //      exactly 1.0 for a fully-provided destination, (1 − w) for a
        //      destitute one. w = 0 recovers the pre-T4.13 behaviour exactly.
        //
        //      THE FEEDBACK IS EMERGENT, NOT GRANTED. Nothing here adds
        //      happiness to anyone. Happiness is derived from conditions; this
        //      term reads it; migration then moves people; the movement changes
        //      population and therefore food-per-head and housing-per-head; and
        //      the NEXT turn's happiness is recomputed from those changed
        //      conditions. If the move does not actually improve conditions,
        //      happiness does not improve — there is no path by which the act
        //      of migrating pays a happiness bonus.
        var viability = new double[n];
        for (int s = 0; s < n; s++)
        {
            if (!anyFood[s]) { viability[s] = 0.0; continue; }

            double material = Math.Max(0.0, 1.0 - m.DestinationDeficitRepulsion * deficit[s]);
            double happiness01 =
                SettlementHappiness.Of(prev, prev.Settlements[s].Id, _cfg) / SettlementHappiness.Max;
            double w = m.AttractivenessHappinessWeight;
            viability[s] = material * (1.0 - w + w * happiness01);
        }

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
        // Damping matrix from Prev distances (missing row — e.g. before the
        // first catchment recompute — is unreachable: damping 0, no flow).
        var damping = new double[n, n];
        // T4.4: whether this source has ANY distance row at all. A genuinely
        // unreachable pair STILL HAS A ROW (it stores +inf, and exp(-inf) = 0), so
        // "no row" means the network has not been computed yet — not "nowhere to
        // go". Migration cannot tell the two apart and does not need to (both give
        // zero flow), but colonization must: only the second is D-037 B1's
        // condition, and treating missing data as isolation would authorise a
        // founding out of an empty table.
        var hasDistances = new bool[n];
        for (int i = 0; i < prev.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = prev.SettlementDistances[i];
            int fi = row.From.Value <= maxId ? settlementIndex[row.From.Value] : -1;
            int ti = row.To.Value <= maxId ? settlementIndex[row.To.Value] : -1;
            if (fi >= 0 && ti >= 0)
            {
                damping[fi, ti] = Math.Exp(-row.TravelCost / m.DampingDecayCostUnits);
                hasDistances[fi] = true;
            }
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

        // === T4.4 (D-037 B1) — THE UNPLACED-DEPARTURE READOUT ==================
        // A PURE WRITE. It reads what this system has already computed and writes
        // BucketRow.UnplacedDeparture. It moves no person, touches no flow, and is
        // placed AFTER every input it reads is final and BEFORE any transfer, so
        // no ordering between it and the transfer loop can exist.
        //
        // WHAT IT WRITES, and why this is not a second migration model: ADR-012
        // states the Exit valve's desire is source-driven — "flight desire remains
        // source-driven (FamineFlightFactor × deficit_source), uncapped by the gap
        // mechanism, exactly as D-021 ratified" — and that viability "only
        // redistributes WHERE the fleeing go". This system only ever forms that
        // desire multiplied by damping(i→j) × viability(j), so when NO destination
        // is both reachable and viable every product is zero and the desire is
        // never expressed at all. That is precisely ADR-012's ruled outcome
        // ("people die at home") and precisely what D-037 B1 extends: "with no
        // viable destination people die at home. Extend it".
        //
        // The condition is BINARY and it is B1's own condition — NO viable
        // reachable destination — not "demand that happened to go unmet". A
        // settlement with even one viable neighbour writes ZERO here and colonises
        // nothing, however hungry it is. That is the property the deficit-ratio
        // trigger lacked, and it is why founding cannot cascade: a settlement
        // founded with provisions has store > 0, so ADR-012's own gate makes it a
        // VIABLE DESTINATION, which zeroes its founder's demand the next turn.
        //
        // The gap channel contributes nothing here BY CONSTRUCTION: a gap is
        // max(0, S_dst − S_src) and needs a destination to exist. There is no
        // destination-free gap desire to leave unplaced.
        Table<BucketRow> bucketsOut = ctx.Owned.Buckets;
        for (int i = 0; i < bucketsOut.Count; i++)
            bucketsOut.Ref(i).UnplacedDeparture = 0.0;   // rewritten every turn, never stale
        for (int src = 0; src < n; src++)
        {
            bool anyViableDestination = false;
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                if (damping[src, dst] > 0.0 && viability[dst] > 0.0) { anyViableDestination = true; break; }
            }
            if (anyViableDestination) continue;          // migration owns these people
            // Missing network data is NOT isolation (see hasDistances above). With a
            // single settlement there is no network to miss, and being alone in the
            // world IS the condition.
            if (n > 1 && !hasDistances[src]) continue;
            if (deficit[src] <= 0.0) continue;           // no flight desire to strand

            foreach (int row in bucketRows[src])
            {
                BucketRow b = prev.Buckets[row];
                double perCount = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                  * b.Count.Value * ctx.DtYears;
                if (perCount <= 0.0) continue;
                bucketsOut.Ref(row).UnplacedDeparture = perCount * m.FamineFlightFactor * deficit[src];
            }
        }
        // === end T4.4 readout ==================================================

        if (n < 2) return;

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
                    long moved = ConservedMath.WholeUnits(exact, $"migration outflow (bucket {srcRow})");
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
