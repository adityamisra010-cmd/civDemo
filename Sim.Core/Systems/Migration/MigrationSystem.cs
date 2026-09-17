using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Consumption;

namespace Sim.Core.Systems.Migration;

/// <summary>Writable handles to MigrationSystem's tables (built by
/// SystemCatalog only). Buckets is SHARED with Demographics/ClassMobility
/// (see SystemCatalog); MigrationFlows is this system's chronicle table;
/// SmoothedAttractiveness is its persistent EMA filter state (T2.8).</summary>
public readonly record struct MigrationTables(
    Table<BucketRow> Buckets, Table<MigrationFlowRow> Flows,
    Table<SmoothedAttractivenessRow> Smoothed);

/// <summary>
/// Migration (T2.5, m2 spec §3 / D-021 Exit valve; STABILIZED at T2.8 by
/// director ruling — the ping-pong attractor was a paired-feedback violation;
/// BOUNDED at T4.21-2 under CR-015 / ADR-025 — the linear flight surge and
/// the unbounded basin refill were the two migration causes of the Libur
/// cascade): people are Ledger.Transfers of buckets between settlements —
/// migrants keep their FULL bucket key. Everything reads Prev (§3.2).
/// Step = <see cref="Plan"/> (a pure function of Prev) + a transfer loop that
/// consumes the plan; the observer calls the same static (spec §3.11).
///
/// TWO CHANNELS, per source bucket b of settlement i and destination j
/// (ADR-025 §2; docs/t4.21-architecture.md §3.4–§3.5):
///
///   GAP (T2.8, unchanged in form):
///     desired = BaseRatePerYear × CohortProfile[cohort] × PREV count × dt
///               × damping(i→j) × viability(j) × gapScaleExecuted(i→j) × gap(i→j)
///     gap     = max(0, S_j − S_i) over the SMOOTHED attractiveness S (below).
///     gapScaleExecuted = gapScale(pair cap, T2.8 a) × destScale_j × srcScale_i
///               × vacScale_j — each basin/vacancy factor multiplied only when
///               it is &lt; 1, so the fed world executes T2.8's instruction
///               sequence bit for bit.
///
///   FLIGHT (D-021 Exit valve; the bounded hazard form, ADR-025 §2.1):
///     ω_i     = max_{j≠i} damping(i→j) × viability(j)      EXIT OPENNESS ∈ [0,1]
///     Z_i     = Σ_{j≠i} damping(i→j) × viability(j);  w_ij = damping × viability / Z_i
///     K       = BaseRatePerYear × FamineFlightFactor        (0.24/yr; no constant moved)
///     φ_b     = 1 − exp(−CohortProfile[cohort] × K × ω_i × d_i × dt)   HOW MANY ∈ [0,1)
///     flight_b→j = φ_b × count_b × (w_ij × vacScale_j)                  WHERE
///     d_i     = the source's PREV consumption-deficit ratio, NOMINAL (not the
///               adapted d_eff of ADR-026 — flight is ordinary pressure at small
///               d and crisis flight at large d, one continuum; famine's
///               exceptional response is the mortality channel, not a separate
///               flight regime).
///
///   HOW MANY is separated from WHERE. φ_b depends on the source's deficit and
///   on the BEST exit only — never on the number of exits: adding a second
///   identical destination leaves φ_b unchanged and halves each share. The
///   exact integral 1 − exp(−rate·dt) is the survival-kernel form the
///   demographics and decay systems use (ADR-011/016 family): two dt = 5 steps
///   at held (ω, d) compose to one dt = 10 step, and Σ_j flight_b→j ≤ φ_b ×
///   count_b &lt; count_b — the Exit valve is BOUNDED BY EXACT INTEGRATION; the
///   overdraw scaler below is the backstop, no longer the only bound (this
///   replaces ADR-012's "surge by design, bounded by the overdraw scaler
///   alone", amended by ADR-025 §1). For small exponent φ ≈ profile·K·ω·d·dt
///   — the pre-amendment gauge with Σ_j replaced by max_j.
///
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
///               Viability reads the NOMINAL destination deficit (ADR-025 §2.2).
///   die at home: ω = max rather than a Σ-normalisation because it keeps
///               ADR-012's "when every reachable destination is itself
///               starving, flight goes to zero" BIT-EXACT — φ = 0 iff every
///               damping × viability = 0, with no 0/0 rule. There is no exodus
///               without a destination — people die at home instead of
///               circulating between ruins (the exit-session pathology).
///               Flight stays gap-INDEPENDENT (D-021: starving people leave
///               for anywhere reachable AND VIABLE) and is not gap-capped.
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
///     GapClosingFraction × m* — at f &lt; 1 the post-move gap keeps its sign,
///     so overshoot is STRUCTURALLY impossible at the pair level. The cap
///     reads INSTANTANEOUS physics while desire reads the SMOOTHED signal:
///     right after a large move the instantaneous m* says "equalized" and
///     the cap zeroes further flow even while the EMA still remembers a gap.
///     T4.21-2 (ADR-025 §2.3) — BASIN CAPS AT BOTH ENDS, the same derivation
///     for a basin: multiple sources sharing one destination (fan-in) or one
///     source feeding many (fan-out) used to sum their pair caps — up to
///     (k+1)/2 × f × m* with k destinations, measured 6.1–6.4× at Libur t119
///     — and the collective inflow was only "pinned empirically". Now, for a
///     destination j with basin B_j = {i : pair-capped gap desire i→j &gt; 0}
///     of two or more sources, j is pooled with its sources
///       P_pool = P_j + Σ P_i,  R_pool = R_j + Σ R_i,
///       M*_j^in = max(0, R_j × P_pool / R_pool − P_j)
///     and the pair-capped inflow is scaled so it never exceeds f × M*_j^in
///     (destScale_j); a source i with basin C_i of two or more destinations
///     is bounded by the mirror M*_i^out = max(0, P_i − R_i × P_pool / R_pool)
///     on its dest-scaled outflow (srcScale_i). Single-pair basins SKIP (the
///     pooled formula equals m* algebraically, not in bits), so every
///     single-source-single-destination rig is bit-identical. Bounded BY
///     CONSTRUCTION at both ends; no new constant (f, R, P are T2.8's).
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
/// T4.21-2 — THE VACANCY BOUND ON TOTAL INFLOW (ADR-025 §2.4; spec §3.5c):
/// HOW MANY a destination can absorb per turn, both channels, one factor:
///     V_j   = FoodHeadroom.Vacancy(prev, j)      max(0, N_lim,j − N_j), adult-equivalents;
///                                               +∞ when j carries no demand row
///     cap_j = (1 − exp(−k × dt)) × V_j          k = demographics.headroomRelaxationPerYear —
///                                               the SAME relaxation law that bounds births
///     in_j  = Σ_i Σ_b (gapExecuted_ib→j + φ_b × count_b × w_ij) × cohortWeight[c_b]
///     vacScale_j = in_j &gt; cap_j ? cap_j / in_j : 1.0
/// VACANCY ≠ ATTRACTIVENESS made literal: a settlement's population approaches
/// its food-influx limit at rate k whether the arrivals are born or walk in.
/// Refused people STAY at their source (no redistribution within the turn;
/// they do not enter the D-037 readout — CR-015 G7(a)). Absent demand row ⇒
/// V = +∞ ⇒ vacScale = 1.0 literal, so every hand rig and every settlement's
/// first turn is untouched by construction.
///
/// OVERDRAW DISCIPLINE: desired outflows to ALL destinations are computed from
/// Prev first (gap components pre-scaled by their caps, flight by φ and the
/// shares); if their sum exceeds the bucket's PREV count they are scaled
/// proportionally — a backstop now, since flight is bounded below count by
/// construction. Transfers then execute in the PINNED ascending (source, dest,
/// bucket-key) order through the per-source-row MigrationRemainder.
/// ClampToAvailable backstops the floors: a bucket can hit exactly zero, never
/// negative.
///
/// CHRONICLE HOOKS: per-settlement Inflow/Outflow totals rebuilt into
/// MigrationFlows every step. Slots after ClassMobility, before Demographics.
/// STATELESS except the EMA filter rows (world state, not system state).
/// No RNG.
/// </summary>
public sealed class MigrationSystem : ISimSystem<MigrationTables>
{
    public static readonly SystemId WellKnownId = new(10);
    public const string Name = "migration";

    private readonly SimConfig _cfg;

    /// <summary>T4.21-2: the shared basket book, for <see cref="FoodHeadroom"/>'s
    /// vacancy (the feedable-food fixed point needs the Sustenance goods and the
    /// staple). Config, read identically by every system that needs it — not a
    /// channel between systems (precedent: ClassMobilitySystem).</summary>
    private readonly BasketBook _baskets;

    public MigrationSystem(SimConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        _cfg = cfg;
        GoodsConfig goods = cfg.Goods
            ?? throw new ArgumentException("MigrationSystem requires SimConfig.Goods (goods.json) at M3.");
        NeedsConfig needs = cfg.Needs
            ?? throw new ArgumentException(
                "MigrationSystem requires SimConfig.Needs (needs.json) — the T4.21-2 vacancy bound "
                + "reads FoodHeadroom over the D-035 Sustenance basket.");
        _baskets = new BasketBook(needs, goods);
    }

    public SystemId Id => WellKnownId;

    /// <summary>
    /// φ = 1 − exp(−profile × K × ω × d × dt): the per-turn flight fraction of a
    /// bucket (ADR-025 §2.1), EXACTLY 0 when there is no deficit or no open exit
    /// (die at home, by branch AND by value). The D-037 B1 readout calls it with
    /// ω := 1 (§3.4.4). ONE expression, so the tests that pin turn-exact values
    /// and the observer multiply the same bits.
    /// </summary>
    public static double FlightFractionOf(
        double cohortProfile, double k, double exitOpenness, double deficit, double dtYears)
    {
        if (!(deficit > 0.0) || !(exitOpenness > 0.0)) return 0.0;
        return 1.0 - Math.Exp(-(cohortProfile * k * exitOpenness * deficit * dtYears));
    }

    /// <summary>The planner with a basket book built from <paramref name="cfg"/>
    /// (the observer's entry point; Step passes its own book).</summary>
    public static MigrationPlan Plan(IReadOnlyWorldState prev, SimConfig cfg, double dtYears)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        GoodsConfig goods = cfg.Goods
            ?? throw new ArgumentException("MigrationSystem.Plan requires SimConfig.Goods (goods.json).");
        NeedsConfig needs = cfg.Needs
            ?? throw new ArgumentException("MigrationSystem.Plan requires SimConfig.Needs (needs.json).");
        return Plan(prev, cfg, dtYears, new BasketBook(needs, goods));
    }

    /// <summary>
    /// T4.21-2 (spec §3.11, ADR-025 §2.6) — THE PLANNER. Everything Step decides
    /// before it moves a person, as a pure function of Prev: the per-settlement
    /// signals, viability, the EMA's NEW smoothed values (the table write stays
    /// in Step), damping, the D-037 B1 readout values (the table write stays in
    /// Step), the T2.8 pair caps, exit openness and shares, the flight fraction
    /// per bucket, the basin caps, the vacancy bound, the per-pair push and the
    /// per-bucket desired totals with their overdraw scales. Step is `Plan +
    /// transfer loop`; the observer calls this same static, so the arithmetic
    /// exists once. On the fed path (d = 0 everywhere, single-pair basins, no
    /// vacancy bite) the ORDER of every product is the order T2.8's inline code
    /// multiplied in — the goldens pin that.
    /// </summary>
    public static MigrationPlan Plan(
        IReadOnlyWorldState prev, SimConfig cfg, double dtYears, BasketBook baskets)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(baskets);
        MigrationConfig m = cfg.Migration;
        var grain = new GoodId(cfg.Goods?.GrainId
            ?? throw new ArgumentException("MigrationSystem requires SimConfig.Goods (goods.json) at M3."));
        double[] cohortWeights = cfg.Consumption.CohortWeights;
        int n = prev.Settlements.Count;
        var plan = new MigrationPlan(n, prev.Buckets.Count, dtYears);

        // --- Prev-derived per-settlement signals -----------------------------
        double[] resources = plan.Resources;    // R = lw × farmland (T4.10: food term removed)
        long[] population = plan.Population;    // P (raw, no floor — m* uses physics)
        double[] instant = plan.Instant;        // A = R / max(P, 1)
        double[] deficit = plan.Deficit;
        bool[] anyFood = plan.AnyFood;          // T2.13: store > 0 OR last harvest > 0
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
                if (prev.GoodStocks[i].Settlement == id && prev.GoodStocks[i].Good == grain)
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
        //   T4.21-2: viability reads the NOMINAL destination deficit (ADR-025
        //   §2.2) — DestinationDeficit_StillRepels and HappinessMigrationTests
        //   pin it.
        double[] viability = plan.Viability;
        for (int s = 0; s < n; s++)
        {
            if (!anyFood[s]) { viability[s] = 0.0; continue; }

            double material = Math.Max(0.0, 1.0 - m.DestinationDeficitRepulsion * deficit[s]);
            double happiness01 =
                SettlementHappiness.Of(prev, prev.Settlements[s].Id, cfg) / SettlementHappiness.Max;
            double w = m.AttractivenessHappinessWeight;
            viability[s] = material * (1.0 - w + w * happiness01);
        }

        // --- EMA filter update (T2.8 b): PREV smoothed → the NEW value -------
        // Read from PREV (the owned table is the cloned prev table and nothing
        // earlier in the pipeline writes it); Step writes the value back in
        // place, or appends a row for a first sighting initialised AT the
        // instantaneous value, in ascending settlement-row order.
        double[] smoothed = plan.Smoothed;
        double alpha = Math.Min(1.0, dtYears / m.AttractivenessSmoothingWindowYears);
        for (int s = 0; s < n; s++)
        {
            SettlementId id = prev.Settlements[s].Id;
            int rowIdx = -1;
            for (int i = 0; i < prev.SmoothedAttractiveness.Count; i++)
                if (prev.SmoothedAttractiveness[i].Settlement == id) { rowIdx = i; break; }
            double prevSmoothed = rowIdx >= 0 ? prev.SmoothedAttractiveness[rowIdx].Value : instant[s];
            double value = prevSmoothed + (instant[s] - prevSmoothed) * alpha;
            plan.SmoothedRowIndex[s] = rowIdx;
            smoothed[s] = value;
        }
        // Damping matrix from Prev distances (missing row — e.g. before the
        // first catchment recompute — is unreachable: damping 0, no flow).
        double[,] damping = plan.Damping;
        // T4.4: whether this source has ANY distance row at all. A genuinely
        // unreachable pair STILL HAS A ROW (it stores +inf, and exp(-inf) = 0), so
        // "no row" means the network has not been computed yet — not "nowhere to
        // go". Migration cannot tell the two apart and does not need to (both give
        // zero flow), but colonization must: only the second is D-037 B1's
        // condition, and treating missing data as isolation would authorise a
        // founding out of an empty table.
        bool[] hasDistances = plan.HasDistances;
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
        int[][] bucketRows = plan.BucketRows;
        var bucketCounts = new int[n];
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            int sid = prev.Buckets[i].Settlement.Value;
            if (sid <= maxId && settlementIndex[sid] >= 0) bucketCounts[settlementIndex[sid]]++;
        }
        for (int s = 0; s < n; s++) { bucketRows[s] = new int[bucketCounts[s]]; bucketCounts[s] = 0; }
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            int sid = prev.Buckets[i].Settlement.Value;
            if (sid <= maxId && settlementIndex[sid] >= 0)
            {
                int s = settlementIndex[sid];
                bucketRows[s][bucketCounts[s]++] = i;
            }
        }

        // K = BaseRatePerYear × FamineFlightFactor — the flight hazard scale
        // (0.24/yr at the shipped constants; T4.12's ruling untouched).
        double kFlight = m.BaseRatePerYear * m.FamineFlightFactor;

        // === T4.4 (D-037 B1) — THE UNPLACED-DEPARTURE READOUT ==================
        // A PURE VALUE here; Step writes it to BucketRow.UnplacedDeparture. It
        // reads what this planner has already computed, moves no person and
        // touches no flow; Step's write is placed AFTER every input it reads is
        // final and BEFORE any transfer, so no ordering between it and the
        // transfer loop can exist.
        //
        // WHAT IT WRITES, and why this is not a second migration model: ADR-012
        // states the Exit valve's desire is source-driven and that viability
        // "only redistributes WHERE the fleeing go". This system only ever forms
        // that desire multiplied by an exit's damping(i→j) × viability(j), so
        // when NO destination is both reachable and viable (ω_i = 0) the desire
        // is never expressed at all. That is precisely ADR-012's ruled outcome
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
        // Refugees a destination REFUSES under the vacancy bound do not enter
        // here — B1's condition is about viability, not capacity (CR-015 G7(a)).
        //
        // THE VALUE (T4.21-2, ADR-025 §2.5 / spec §3.4.4) is the DESTINATION-FREE
        // hazard: (1 − exp(−CohortProfile × K × d × dt)) × count, i.e. φ_b with
        // ω := 1 on the nominal d. Under B1's own condition ω_i = 0, so reusing
        // φ_b verbatim would write 0 and kill colonization; ω := 1 is exactly
        // ADR-012's "flight desire remains source-driven" quantity and ADR-021's
        // "structurally absent, not discarded". Bounded below count: DrawParty
        // floors it, so a source is never emptied in one founding (supersedes
        // t4.4-review-record §D2 "it can be emptied").
        //
        // The gap channel contributes nothing here BY CONSTRUCTION: a gap is
        // max(0, S_dst − S_src) and needs a destination to exist. There is no
        // destination-free gap desire to leave unplaced.
        double[] unplaced = plan.Unplaced;              // zero unless written below
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
                if (b.Count.Value <= 0) continue;
                unplaced[row] = FlightFractionOf(m.CohortProfile[b.CohortIdx], kFlight, 1.0, deficit[src], dtYears)
                                * b.Count.Value;
            }
        }
        // === end T4.4 readout ==================================================

        if (n < 2) return plan;

        // --- T2.8 (a): per-pair gap-closing caps -----------------------------
        // gapScale[src,dst] scales the pair's ENTIRE gap-driven desire so it
        // never exceeds f × m*. Computed once from Prev; the transfer loop
        // recomputes the same product terms from the same inputs (association
        // differs at ULP level between the desire and transfer sites — a
        // pre-T2.13 pattern; ClampToAvailable backstops any ULP overdraw).
        double[,] gapScale = plan.GapScale;
        double[,] gapPairDesire = plan.GapPairDesire;
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
                                 * b.Count.Value * dtYears * damping[src, dst]
                                 * viability[dst] * gap;
                }
                if (gapDesire <= 0.0) continue;
                gapPairDesire[src, dst] = gapDesire;

                double denom = resources[src] + resources[dst];
                double equalizing = denom > 0.0
                    ? Math.Max(0.0, (resources[dst] * population[src] - resources[src] * population[dst]) / denom)
                    : 0.0;
                double cap = m.GapClosingFraction * equalizing;
                gapScale[src, dst] = gapDesire > cap ? cap / gapDesire : 1.0;
            }
        }

        // --- §3.4: exit openness ω, share normaliser Z, shares w ----------------
        // ω is a max over a table-ordered scan (order-independent); Z a sum in
        // ascending destination order (pinned). Both read the same products the
        // gap channel multiplies. No sort over doubles anywhere in this system.
        double[] omega = plan.ExitOpenness;
        double[] zNorm = plan.ShareNormaliser;
        double[,] share = plan.Share;
        for (int src = 0; src < n; src++)
        {
            double best = 0.0, z = 0.0;
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double open = damping[src, dst] * viability[dst];
                if (open > best) best = open;
                z += open;
            }
            omega[src] = best;
            zNorm[src] = z;
            if (z > 0.0)
            {
                for (int dst = 0; dst < n; dst++)
                {
                    if (dst == src) continue;
                    share[src, dst] = damping[src, dst] * viability[dst] / z;
                }
            }
        }

        // --- §3.4: φ per bucket — HOW MANY, on the best exit and the nominal d --
        double[] phi = plan.FlightFraction;
        double[] flightBound = plan.FlightBound;
        for (int src = 0; src < n; src++)
        {
            double bound = 0.0;
            foreach (int row in bucketRows[src])
            {
                BucketRow b = prev.Buckets[row];
                phi[row] = FlightFractionOf(m.CohortProfile[b.CohortIdx], kFlight, omega[src], deficit[src], dtYears);
                bound += phi[row] * b.Count.Value;
            }
            flightBound[src] = bound;
        }

        // --- §3.5b: basin caps on the GAP channel, destination end then source end
        // Basin membership is "pair-capped gap desire > 0". |basin| < 2 ⇒ SKIP —
        // the pooled M* equals m* algebraically at one member, not in bits, and
        // the skip is what keeps every single-pair rig bit-identical.
        double[] destScale = plan.DestScale;
        double[] gapInflowCap = plan.GapInflowCap;
        double[] gapInflowPairCapped = plan.GapInflowPairCapped;
        for (int dst = 0; dst < n; dst++)
        {
            int members = 0;
            double inflow = 0.0;
            long pPool = population[dst];
            double rPool = resources[dst];
            for (int src = 0; src < n; src++)
            {
                if (src == dst) continue;
                double capped = gapScale[src, dst] * gapPairDesire[src, dst];
                if (capped <= 0.0) continue;
                members++;
                inflow += capped;
                pPool += population[src];
                rPool += resources[src];
            }
            gapInflowPairCapped[dst] = inflow;
            if (members < 2) continue;
            double equalizing = rPool > 0.0
                ? Math.Max(0.0, resources[dst] * pPool / rPool - population[dst])
                : 0.0;
            double cap = m.GapClosingFraction * equalizing;
            gapInflowCap[dst] = cap;
            destScale[dst] = inflow > cap ? cap / inflow : 1.0;
        }
        // Source end: the mirror, on the DEST-SCALED outflow (the chain is
        // sequential — each stage bounds what the stage before it let through,
        // so a source already held back by its destinations' basins is not
        // limited twice for the same people).
        double[] srcScale = plan.SrcScale;
        double[] gapOutflowCap = plan.GapOutflowCap;
        double[] gapOutflowDestScaled = plan.GapOutflowDestScaled;
        for (int src = 0; src < n; src++)
        {
            int members = 0;
            double outflow = 0.0;
            long pPool = population[src];
            double rPool = resources[src];
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double capped = gapScale[src, dst] * gapPairDesire[src, dst];
                if (capped <= 0.0) continue;
                members++;
                if (destScale[dst] < 1.0) capped *= destScale[dst];
                outflow += capped;
                pPool += population[dst];
                rPool += resources[dst];
            }
            gapOutflowDestScaled[src] = outflow;
            if (members < 2) continue;
            double equalizing = rPool > 0.0
                ? Math.Max(0.0, population[src] - resources[src] * pPool / rPool)
                : 0.0;
            double cap = m.GapClosingFraction * equalizing;
            gapOutflowCap[src] = cap;
            srcScale[src] = outflow > cap ? cap / outflow : 1.0;
        }
        // The executed gap scale before vacancy: pair × destination basin ×
        // source basin, each basin factor multiplied ONLY when it bites.
        double[,] gapScaleExecuted = plan.GapScaleExecuted;
        for (int src = 0; src < n; src++)
        {
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double gs = gapScale[src, dst];
                if (destScale[dst] < 1.0) gs *= destScale[dst];
                if (srcScale[src] < 1.0) gs *= srcScale[src];
                gapScaleExecuted[src, dst] = gs;
            }
        }

        // --- §3.5c: the VACANCY bound on TOTAL inflow ----------------------------
        // in_j in adult-equivalents over both channels (gap executed through the
        // basin scales; flight through the shares), against cap_j = (1 − e^{−k·dt})
        // × V_j. V = +∞ (no demand row) ⇒ cap = +∞ ⇒ vacScale = 1.0 literal.
        double[] vacancy = plan.Vacancy;
        double[] vacancyCap = plan.VacancyCap;
        double[] desiredInflowAe = plan.DesiredInflowAe;
        double[] vacScale = plan.VacancyScale;
        double relax = 1.0 - Math.Exp(-(cfg.Demographics.HeadroomRelaxationPerYear * dtYears));
        for (int dst = 0; dst < n; dst++)
        {
            double inflowAe = 0.0;
            for (int src = 0; src < n; src++)
            {
                if (src == dst) continue;
                double gsBasin = gapScaleExecuted[src, dst];
                double gap = Math.Max(0.0, smoothed[dst] - smoothed[src]);
                bool gapOpen = gsBasin > 0.0 && gap > 0.0;
                bool flightOpen = share[src, dst] > 0.0 && deficit[src] > 0.0 && omega[src] > 0.0;
                if (!gapOpen && !flightOpen) continue;
                foreach (int row in bucketRows[src])
                {
                    BucketRow b = prev.Buckets[row];
                    long count = b.Count.Value;
                    if (count <= 0) continue;
                    double heads = 0.0;
                    if (gapOpen)
                    {
                        double perCount = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx] * count * dtYears;
                        heads += perCount * damping[src, dst] * viability[dst] * (gsBasin * gap);
                    }
                    if (flightOpen) heads += phi[row] * count * share[src, dst];
                    inflowAe += heads * cohortWeights[b.CohortIdx];
                }
            }
            desiredInflowAe[dst] = inflowAe;
            double v = FoodHeadroom.Vacancy(prev, prev.Settlements[dst].Id, cohortWeights, baskets);
            vacancy[dst] = v;
            if (double.IsPositiveInfinity(v)) continue;     // cap +∞, scale 1.0 (ctor defaults)
            double cap = relax * v;
            vacancyCap[dst] = cap;
            vacScale[dst] = inflowAe > cap ? cap / inflowAe : 1.0;
        }
        // Fold vacancy into the executed gap scale and the flight weight.
        double[,] flightWeight = plan.FlightWeight;
        for (int src = 0; src < n; src++)
        {
            bool sourceHasFlight = deficit[src] > 0.0 && omega[src] > 0.0;
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                if (vacScale[dst] < 1.0) gapScaleExecuted[src, dst] *= vacScale[dst];
                if (sourceHasFlight && share[src, dst] > 0.0)
                {
                    double fw = share[src, dst];
                    if (vacScale[dst] < 1.0) fw *= vacScale[dst];
                    flightWeight[src, dst] = fw;
                }
            }
        }

        // --- the per-pair GAP push the transfer loop multiplies -----------------
        // On the fed path gapScaleExecuted == gapScale and the expression is
        // T2.8's damping × viability × (gapScale × gap + FamineFlightFactor × 0)
        // bit for bit (x + 0.0 == x).
        double[,] push = plan.Push;
        for (int src = 0; src < n; src++)
        {
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                push[src, dst] = damping[src, dst] * viability[dst]
                                 * (gapScaleExecuted[src, dst] * Math.Max(0.0, smoothed[dst] - smoothed[src]));
            }
        }

        // --- desired outflows (all from Prev), then proportional scaling -----
        // desiredTotal[bucketRow] = Σ_j (gap executed + flight) desire; the
        // transfer loop multiplies the same factors (association differs at ULP
        // level between the two sites — the pre-T2.13 pattern). The channel
        // totals record what the loop will ask for, after the overdraw scale.
        double[] desiredTotal = plan.DesiredTotal;
        double[] overdrawScale = plan.OverdrawScale;
        for (int src = 0; src < n; src++)
        {
            foreach (int row in bucketRows[src])
            {
                BucketRow b = prev.Buckets[row];
                long count = b.Count.Value;
                double perCount = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                  * count * dtYears;
                if (perCount <= 0.0) continue;
                double total = 0.0;
                for (int dst = 0; dst < n; dst++)
                {
                    if (dst == src) continue;
                    double term = perCount * damping[src, dst] * viability[dst]
                                  * (gapScaleExecuted[src, dst] * Math.Max(0.0, smoothed[dst] - smoothed[src]));
                    if (flightWeight[src, dst] > 0.0) term += phi[row] * count * flightWeight[src, dst];
                    total += term;
                }
                desiredTotal[row] = total;
                double scale = total > count ? count / total : 1.0;
                overdrawScale[row] = scale;
                for (int dst = 0; dst < n; dst++)
                {
                    if (dst == src) continue;
                    double gapHeads = perCount * push[src, dst];
                    if (gapHeads > 0.0)
                    {
                        gapHeads *= scale;
                        plan.GapOut[src] += gapHeads;
                        plan.GapIn[dst] += gapHeads;
                    }
                    if (flightWeight[src, dst] > 0.0)
                    {
                        double flightHeads = phi[row] * count * flightWeight[src, dst] * scale;
                        plan.FlightOut[src] += flightHeads;
                        plan.FlightIn[dst] += flightHeads;
                    }
                }
            }
        }
        return plan;
    }

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

        MigrationPlan plan = Plan(prev, _cfg, ctx.DtYears, _baskets);

        // --- EMA filter write (T2.8 b): the owned table is the cloned prev ---
        // table; rows update in place, and a settlement without a row (first
        // sighting) appends one, in ascending settlement-row order.
        Table<SmoothedAttractivenessRow> smoothedTable = ctx.Owned.Smoothed;
        for (int s = 0; s < n; s++)
        {
            int rowIdx = plan.SmoothedRowIndex[s];
            if (rowIdx >= 0) smoothedTable[rowIdx] = smoothedTable[rowIdx] with { Value = plan.Smoothed[s] };
            else smoothedTable.Add(new SmoothedAttractivenessRow(prev.Settlements[s].Id, plan.Smoothed[s]));
        }

        // --- D-037 B1 readout write: rewritten every turn, never stale --------
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        for (int i = 0; i < buckets.Count; i++)
            buckets.Ref(i).UnplacedDeparture = i < plan.Unplaced.Length ? plan.Unplaced[i] : 0.0;

        if (n < 2) return;

        int[][] bucketRows = plan.BucketRows;
        double[] desiredTotal = plan.DesiredTotal;
        double[] phi = plan.FlightFraction;

        // --- transfers, pinned ascending (source, dest, bucket-key) ----------
        for (int src = 0; src < n; src++)
        {
            for (int dst = 0; dst < n; dst++)
            {
                if (dst == src) continue;
                double push = plan.Push[src, dst];
                double flightWeight = plan.FlightWeight[src, dst];
                if (push <= 0.0 && flightWeight <= 0.0) continue;

                for (int k = 0; k < bucketRows[src].Length; k++)
                {
                    int srcRow = bucketRows[src][k];
                    BucketRow b = prev.Buckets[srcRow];
                    long prevCount = b.Count.Value;
                    if (prevCount <= 0 && buckets.Ref(srcRow).MigrationRemainder == 0.0) continue;

                    double desired = m.BaseRatePerYear * m.CohortProfile[b.CohortIdx]
                                     * prevCount * ctx.DtYears * push;
                    if (flightWeight > 0.0) desired += phi[srcRow] * prevCount * flightWeight;
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
                    int dstRow = k < bucketRows[dst].Length ? bucketRows[dst][k] : -1;
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
