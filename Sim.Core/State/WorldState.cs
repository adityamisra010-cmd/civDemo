namespace Sim.Core.State;

/// <summary>
/// Row of the region table. Regions are the spatial anchor every M0 system keys on
/// (weather/growth in T0.5, trade in T0.6). Real world-substrate state arrives with
/// M1 worldgen; at M0 a region is just its identity.
/// </summary>
public readonly record struct RegionRow(RegionId Id);

/// <summary>
/// Row of the rainfall table — owned by WeatherSystem (M0 toy). Rainfall is an
/// environmental state in mm/year, redrawn each turn from the system's RNG stream;
/// a rate-like double, not a conserved stock (law 7).
/// </summary>
public record struct RainfallRow(RegionId Region, double RainfallMmPerYear);

/// <summary>
/// Row of the biomass table — owned by GrowthSystem (M0 toy). Biomass is a
/// conserved stock (law 1, mutated only via Ledger); GrowthRemainder is the D-004
/// per-entity remainder accumulator (the fractional part of integration carried
/// between turns, deterministically — no stochastic rounding). It lives in the row
/// so it travels with Clone and snapshots (systems are stateless).
/// </summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct BiomassRow(RegionId region, Conserved biomass, double growthRemainder) : IEquatable<BiomassRow>
{
    public RegionId Region = region;
    public Conserved Biomass = biomass;
    public double GrowthRemainder = growthRemainder;

    public readonly bool Equals(BiomassRow other) =>
        Region == other.Region && Biomass == other.Biomass && GrowthRemainder.Equals(other.GrowthRemainder);
    public override readonly bool Equals(object? obj) => obj is BiomassRow other && Equals(other);
    public override readonly int GetHashCode() => Region.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>Row of the toy-goods table — owned by TradeSystem (M0 toy). A conserved stock per region.</summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct GoodsRow(RegionId region, Conserved amount) : IEquatable<GoodsRow>
{
    public RegionId Region = region;
    public Conserved Amount = amount;

    public readonly bool Equals(GoodsRow other) => Region == other.Region && Amount == other.Amount;
    public override readonly bool Equals(object? obj) => obj is GoodsRow other && Equals(other);
    public override readonly int GetHashCode() => Region.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// A node of the built transport network (T1.3), anchored at a traversal-lattice
/// node. Created empty by worldgen; WRITE ownership is assigned at T1.6
/// (PathBuildSystem) — until then no system owns these tables.
/// </summary>
public record struct NetworkNodeRow(NetworkNodeId Id, int LatticeNode);

/// <summary>
/// An edge of the built transport network (T1.3): a fast lane between two network
/// nodes' lattice anchors. Cost is the TOTAL traversal cost of the edge (in the
/// lattice's cost·px units), fixed when the edge is built.
/// </summary>
public record struct NetworkEdgeRow(NetworkEdgeId Id, NetworkNodeId A, NetworkNodeId B, int EdgeType, double Cost);

/// <summary>A settlement (T1.4): site is a TERRAIN cell index. The table never assumes one row.</summary>
public record struct SettlementRow(SettlementId Id, int SiteCell, long FoundedTurn);

/// <summary>
/// Network bookkeeping (T1.4): a single row holding the network revision counter.
/// Worldgen initializes it to 0; PathBuildSystem owns incrementing it from T1.6.
/// CatchmentSystem recomputes only when this revision moves (D-016).
/// </summary>
public record struct NetworkMetaRow(int Revision);

/// <summary>One lattice node inside a settlement's catchment (owned by CatchmentSystem).</summary>
public record struct CatchmentNodeRow(SettlementId Settlement, int LatticeNode, double TravelCost);

/// <summary>
/// Per-settlement catchment summary (owned by CatchmentSystem): node count,
/// arable land, the network revision it was computed against, and the turn
/// of the last recompute (the D-016 skip-proof observable).
///
/// EffectiveArableKm2 is in FERTILITY-WEIGHTED km² — the land content of a
/// hypothetical ideal-suitability (f = 1.0) km², summed over the catchment's
/// lattice blocks. It was renamed from EffectiveFarmland at T3.2b, when the
/// field turned out to be holding fertility-weighted NODE COUNTS that one
/// consumer converted to km² and another did not (CR-002). The unit is in the
/// name so that a reader of any consumer knows what they are holding.
/// </summary>
public record struct CatchmentSummaryRow(
    SettlementId Settlement, int NodeCount, double EffectiveArableKm2,
    int NetworkRevision, long LastRecomputeTurn, int SizeTier = 0);
// T3.8 SizeTier: the QUANTIZED settlement-size step (0..4) this summary was
// computed at — dwellings/sizeDwellingsRef bucketed into fifths. Stored so the
// D-016 recompute gate can see a tier change without recomputing every turn
// (a continuous dwellings-driven budget would re-run the partition Dijkstra
// each turn; the tier changes a handful of times per campaign).

/// <summary>
/// One population bucket (T2.1, D-026): the key is (Settlement, Culture,
/// Religion, Class, CohortIdx) — the full M2-final shape, even though M2
/// instantiates one culture and one religion (the key carries the dimensions
/// now so the schema never churns). Count is a CONSERVED long stock (law 1 —
/// every person moves via Ledger only) plus the D-004 remainder accumulators
/// for each fractional flow that touches this bucket. Remainders live in the
/// row so they clone and snapshot (systems are stateless): BirthRemainder
/// accumulates on the CREDITED newborn cohorts (0..k for a dt spanning k+1
/// cohorts), DeathRemainder/StarvationRemainder on every bucket,
/// AgingRemainder on the bucket aging OUT (unused on the absorbing 75+).
/// </summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct BucketRow(
    SettlementId settlement, CultureId culture, ReligionId religion, ClassId cls,
    int cohortIdx, Conserved count,
    double birthRemainder, double deathRemainder, double starvationRemainder,
    double agingRemainder, double mobilityRemainder = 0.0, double migrationRemainder = 0.0,
    double reboundReservoir = 0.0, double unplacedDeparture = 0.0,
    double unplacedRemainder = 0.0) : IEquatable<BucketRow>
{
    public SettlementId Settlement = settlement;
    public CultureId Culture = culture;
    public ReligionId Religion = religion;
    public ClassId Class = cls;
    public int CohortIdx = cohortIdx;
    public Conserved Count = count;
    public double BirthRemainder = birthRemainder;
    public double DeathRemainder = deathRemainder;
    public double StarvationRemainder = starvationRemainder;
    public double AgingRemainder = agingRemainder;

    /// <summary>T2.2: D-004 remainder for class-mobility transfers, carried on
    /// the SOURCE row of the flow (peasant row for promotion, artisan row for
    /// demotion).</summary>
    public double MobilityRemainder = mobilityRemainder;

    /// <summary>T2.5: D-004 remainder for migration outflows, carried on the
    /// SOURCE row — one accumulator per bucket, updated at each (dest, bucket)
    /// visit in the pinned order, so fractional outflow is conserved in total
    /// while sub-person destination attribution rides the carry (documented).</summary>
    public double MigrationRemainder = migrationRemainder;

    /// <summary>T2.7: deferred-conception reservoir, carried on the group's
    /// cohort-0 (newborn anchor) row only. Famine suppresses conceptions; a
    /// TUNE-recoverable fraction of the suppressed EXACT births banks here and
    /// is released back into the births flow once the settlement is fed again
    /// (post-famine rebound). NOT a conserved stock — it counts conceptions
    /// that never became people; people still enter ONLY via the Births
    /// Ledger flow at release time ("deferred, not invented").</summary>
    public double ReboundReservoir = reboundReservoir;

    /// <summary>
    /// T4.4 — THE D-037 B1 CARRIER. This turn's UNPLACED DEPARTURE DEMAND for this
    /// bucket: the people who generated migration desire and found NO viable existing
    /// destination at all, and whom ADR-012 therefore leaves at home.
    ///
    /// WHY IT HAS TO EXIST AT ALL. `MigrationSystem` forms the flight term ONLY inside
    /// its destination loop, always as `damping(i→j) × viability(j) × FamineFlight ×
    /// deficit_i`. When no destination is viable EVERY product is zero, so the desire
    /// is never expressed as a quantity — it is structurally ABSENT, not discarded.
    /// D-037 B1 needs exactly that absent quantity: *"ADR-012 rules that with no viable
    /// destination people die at home. Extend it: groups may depart into UNCLAIMED land
    /// and found new settlements."*
    ///
    /// NOT A NEW FORMULA. ADR-012 names this desire verbatim — *"flight desire remains
    /// source-driven (FamineFlightFactor × deficit_source), uncapped by the gap
    /// mechanism, exactly as D-021 ratified"* — and says viability *"only redistributes
    /// WHERE the fleeing go"*. This field is that same ratified term with the
    /// destination redistribution removed BECAUSE THERE IS NO DESTINATION. No migration
    /// flow, cap, gate, EMA or attractiveness term is changed by writing it down.
    ///
    /// PER BUCKET, not per settlement, so a founding party keeps its FULL bucket key
    /// (D-026) — migration requires key-for-key arrival, and a party assembled from a
    /// settlement-level scalar would have to INVENT an allocation across buckets.
    ///
    /// REWRITTEN EVERY TURN by MigrationSystem (including to zero). It is a DESIRE, not
    /// a stock: no person is held here and none is conserved here.
    /// </summary>
    public double UnplacedDeparture = unplacedDeparture;

    /// <summary>T4.4: the D-004 remainder for the unplaced-departure draw, carried on
    /// the source bucket exactly as <see cref="MigrationRemainder"/> is. Without it a
    /// bucket whose per-turn demand is under one person floors to zero EVERY turn and
    /// can never colonise however long the condition holds — the permanent sub-unit
    /// stall T4.13 F3 measured. This is the ratified D-004 accumulator pattern, NOT a
    /// timer: what accumulates is the demand itself, so it fills at a rate the
    /// mechanism sets and stops the moment a viable destination appears.</summary>
    public double UnplacedRemainder = unplacedRemainder;

    public readonly bool Equals(BucketRow other) =>
        Settlement == other.Settlement && Culture == other.Culture
        && Religion == other.Religion && Class == other.Class
        && CohortIdx == other.CohortIdx && Count == other.Count
        && BirthRemainder.Equals(other.BirthRemainder)
        && DeathRemainder.Equals(other.DeathRemainder)
        && StarvationRemainder.Equals(other.StarvationRemainder)
        && AgingRemainder.Equals(other.AgingRemainder)
        && MobilityRemainder.Equals(other.MobilityRemainder)
        && MigrationRemainder.Equals(other.MigrationRemainder)
        && ReboundReservoir.Equals(other.ReboundReservoir)
        && UnplacedDeparture.Equals(other.UnplacedDeparture)
        && UnplacedRemainder.Equals(other.UnplacedRemainder);
    public override readonly bool Equals(object? obj) => obj is BucketRow other && Equals(other);
    public override readonly int GetHashCode() => Settlement.Value * Cohorts.Count + CohortIdx; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// One (settlement, good) stock (T3.2, D-031): a CONSERVED long quantity —
/// the m3-spec §3 goods table. THE M2 FoodStoreRow MIGRATED HERE: grain's row
/// plays its exact role (1 grain unit = 1 person-year, the D-015 constant) —
/// Farming credits it (Harvest) and owns ProduceRemainder; Consumption debits
/// it (Eaten, ClampToAvailable) and owns ConsumeRemainder; the shared handle
/// is issued ONLY by SystemCatalog. Other goods' remainders serve their
/// production/consumption flows from T3.3 on. LastProducedUnits is the units
/// credited THIS turn (observational — grain's is the numerator of the
/// published food_surplus_ratio). Founding lays rows out settlement-major,
/// goods ascending in registry order — the deterministic layout consumers may
/// rely on (hand-built test worlds may carry sparse rows; systems locate by
/// (settlement, good), never by index arithmetic).
/// </summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct GoodStockRow(
    SettlementId settlement, GoodId good, Conserved amount,
    double produceRemainder, double consumeRemainder,
    long lastProducedUnits = 0,
    long lastInputDemandUnits = 0,
    long lastConsumptionDemandUnits = 0,
    long lastConsumptionEatenUnits = 0) : IEquatable<GoodStockRow>
{
    public SettlementId Settlement = settlement;
    public GoodId Good = good;
    public Conserved Amount = amount;
    public double ProduceRemainder = produceRemainder;
    public double ConsumeRemainder = consumeRemainder;
    public long LastProducedUnits = lastProducedUnits;

    /// <summary>T3.4 (D-033): units of this good that RECIPES WANTED this turn,
    /// before any input cap bound — the input-demand term of excess demand.
    /// Observational, never a stock. WANTED, not consumed: a good that is
    /// scarce must still register the demand that went unmet, or its price can
    /// never rise to reflect the scarcity. Zeroed every turn like
    /// LastProducedUnits (T3.3 precedent: a stale observational field reads as
    /// a live one).</summary>
    public long LastInputDemandUnits = lastInputDemandUnits;

    /// <summary>T3.4 (D-033): PRE-CLAMP consumption demand for this good this
    /// turn — the consumption term of excess demand. Pre-clamp for the same
    /// reason as above: what a starving settlement could not buy is exactly the
    /// signal that should move the price.</summary>
    public long LastConsumptionDemandUnits = lastConsumptionDemandUnits;

    /// <summary>T3.5 (D-035): units of this good this settlement ACTUALLY
    /// obtained this turn — post-clamp, the companion to the pre-clamp demand
    /// above. The pair is the settlement-wide FILL RATIO, and the fill ratio is
    /// what turns a basket into a satisfaction: NeedsGrievanceSystem reads both
    /// from Prev and needs them together, because demand alone cannot tell a
    /// settlement that ate everything it wanted from one that ate nothing.
    /// Observational, never a stock; zeroed every turn with the rest.</summary>
    public long LastConsumptionEatenUnits = lastConsumptionEatenUnits;

    public readonly bool Equals(GoodStockRow other) =>
        Settlement == other.Settlement && Good == other.Good && Amount == other.Amount
        && ProduceRemainder.Equals(other.ProduceRemainder)
        && ConsumeRemainder.Equals(other.ConsumeRemainder)
        && LastProducedUnits == other.LastProducedUnits
        && LastInputDemandUnits == other.LastInputDemandUnits
        && LastConsumptionDemandUnits == other.LastConsumptionDemandUnits
        && LastConsumptionEatenUnits == other.LastConsumptionEatenUnits;
    public override readonly bool Equals(object? obj) => obj is GoodStockRow other && Equals(other);
    public override readonly int GetHashCode() => Settlement.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// One (settlement, good) DEPOSIT (T3.2): the founding-rolled extraction
/// abundance multiplier — a double, NOT a conserved stock (it scales rates;
/// units never enter the world except through production Ledger flows,
/// T3.3). Derived from the good's terrain channel at the site plus the
/// seeded founding roll; immutable after founding (depletion is a later
/// milestone if the director wants it). This is what makes settlements
/// DIFFERENT — the precondition for comparative advantage (m3-spec §3).
/// </summary>
public record struct DepositRow(SettlementId Settlement, GoodId Good, double Abundance);

/// <summary>
/// One settlement's consumption deficit for the turn just computed (T1.5, owned
/// by ConsumptionSystem): DeficitRatio = unmet demand / integer demand, in [0,1];
/// 0 when demand was fully met (or there was no demand). DemographicsSystem reads
/// the PREVIOUS turn's row for its starvation multiplier — the kernel's standard
/// one-turn-lag coupling (§3.2), documented and accepted.
/// </summary>
/// <summary>DemandUnits (T2.2; RE-STATED at T3.5): the settlement's whole
/// NUTRITIONAL REQUIREMENT for the turn in person-year-equivalents, pre-clamp —
/// the denominator of the published food_surplus_ratio. Its MAGNITUDE is
/// unchanged by T3.5: before the D-035 baskets, grain was the only food, so
/// grain demand WAS the whole requirement. What changed is that it is no longer
/// any single good's demand — no good's LastConsumptionDemandUnits equals it,
/// because the requirement is spread across the food basket and unmet
/// non-staple demand substitutes into the staple. Anything dividing by it must
/// therefore supply a numerator denominated the same way: FOOD, not grain.</summary>
public record struct ConsumptionDeficitRow(SettlementId Settlement, double DeficitRatio, long DemandUnits = 0);

/// <summary>
/// T3.3 (D-032): a settlement's labor allocation across the five sectors,
/// replacing the M2 farm/path pair. Values are RAW weights in [0,1] as set by
/// orders; consumers read NORMALIZED shares via <see cref="Sectors.Share"/>
/// (divide by the row sum), so partial sector orders stay well-defined. The
/// never-ordered default is all-farming (the M2 default, unchanged). Farming /
/// Herding / Extraction / Crafting feed ProductionSystem; Construction feeds
/// PathBuild (the M2 "path share").
/// </summary>
public record struct SectorAllocationRow(
    SettlementId Settlement, double Farming, double Herding, double Extraction,
    double Crafting, double Construction);

/// <summary>
/// The D-032 sector vocabulary (T3.3): fixed order, array-friendly ids. The
/// registry is deliberately code, not data — sectors are kernel-contract
/// surface (order payloads and the allocation row shape key on them), unlike
/// goods, which are data.
/// </summary>
public static class Sectors
{
    public const int Farming = 0;
    public const int Herding = 1;      // herding/fishing — the food-from-deposits sector
    public const int Extraction = 2;
    public const int Crafting = 3;
    public const int Construction = 4; // PathBuild's pool (housing joins at T3.8)
    public const int Count = 5;

    /// <summary>Raw weight of one sector in a row (by sector id).</summary>
    public static double Raw(in SectorAllocationRow row, int sector) => sector switch
    {
        Farming => row.Farming, Herding => row.Herding, Extraction => row.Extraction,
        Crafting => row.Crafting, Construction => row.Construction,
        _ => throw new ArgumentOutOfRangeException(nameof(sector), sector, "unknown sector"),
    };

    /// <summary>Row with one sector's raw weight replaced (by sector id).</summary>
    public static SectorAllocationRow With(in SectorAllocationRow row, int sector, double value) => sector switch
    {
        Farming => row with { Farming = value }, Herding => row with { Herding = value },
        Extraction => row with { Extraction = value }, Crafting => row with { Crafting = value },
        Construction => row with { Construction = value },
        _ => throw new ArgumentOutOfRangeException(nameof(sector), sector, "unknown sector"),
    };

    /// <summary>
    /// NORMALIZED share: raw ÷ row sum. An all-zero row (never legal via
    /// orders, which validate at load; defensively possible in hand-built
    /// state) yields 0 for every sector — no labor anywhere, nothing produced,
    /// nothing banked, which is the conservative reading.
    /// </summary>
    public static double Share(in SectorAllocationRow row, int sector)
    {
        double sum = row.Farming + row.Herding + row.Extraction + row.Crafting + row.Construction;
        return sum > 0.0 ? Raw(row, sector) / sum : 0.0;
    }

    /// <summary>
    /// The never-ordered default: the subsistence mix of a mixed-farming
    /// riverine village (T3.5b item 1, DERIVED not fitted — reference class,
    /// activity reasoning and anti-fitting statement in
    /// docs/t3.5b-derivations.md §1, committed before any measurement).
    /// Majority cultivation; herding/fishing for the traction-and-manure herd
    /// ADR-013's land budget already reserves; fuel-wood and building-material
    /// extraction; craft (tools are REAL goods since T3.3 and need this labour
    /// to exist); construction for dwellings and paths.
    ///
    /// Replaces the M2 all-farming placeholder, whose consequence was a world
    /// containing grain and nothing else — no trade to test at T3.6, no
    /// merchant emergence at T3.7, no comparative advantage at T3.10.
    ///
    /// Regime check (ADR-013 §4, recomputed at T3.5b item 1a): land binds
    /// while food-labour capacity a·(s_f·m·5.0 + s_h·ā·3.0) exceeds c ≈ 0.84;
    /// worst-case flip at s_f* = 0.295. This mix sits at 0.55 — land-bound
    /// with ≥1.87× headroom (see t3.5b spec item 1a for all four cases).
    /// </summary>
    public static SectorAllocationRow Default(SettlementId settlement) =>
        new(settlement, Farming: 0.55, Herding: 0.15, Extraction: 0.10, Crafting: 0.12, Construction: 0.08);
}

/// <summary>
/// One settlement's path-building progress (T1.6, owned by PathBuildSystem):
/// Banked is accrued build-labor (NOT a conserved stock — a rate accumulator
/// like the D-004 remainders); FrontierNode is the lattice node the path chain
/// currently ends at (−1 until first resolved to the settlement's origin).
/// </summary>
public record struct PathProgressRow(SettlementId Settlement, double Banked, int FrontierNode);

/// <summary>
/// One published simulation variable (T2.2, D-020): a named per-settlement
/// double, keyed by the CODE registry id (Sim.Core.State.Variables — names
/// never live in sim rows, ADR-001). Written each turn by ClassMobilitySystem;
/// the predicate DSL evaluates against the PREV turn's rows (one-turn lag,
/// §3.2). Observational state, not a stock.
/// </summary>
public record struct VariableRow(SettlementId Settlement, int VarId, double Value);

/// <summary>
/// Per-(settlement, class) emergence state (T2.2, D-020): Active is the
/// HYSTERESIS latch — 1 once the class's emergence predicate has fired, back
/// to 0 only via its recession predicate (absent recede = never). Persistent
/// sim state (serialized): the latch is what makes predicate flips hysteretic
/// instead of oscillating.
/// </summary>
public record struct ClassStateRow(SettlementId Settlement, ClassId Class, int Active);

/// <summary>
/// Pairwise settlement travel cost (T2.5): the shortest lattice+network cost
/// From → To — DERIVED state owned by CatchmentSystem, recomputed on exactly
/// the D-016 catchment triggers (revision / settlement-set change; the
/// piggyback the spec mandates). PositiveInfinity marks an unreachable pair —
/// serialized as its IEEE bits, and exp(−∞) = 0 makes the damping (and
/// therefore every flow across it) EXACTLY zero by construction.
/// </summary>
public record struct SettlementDistanceRow(SettlementId From, SettlementId To, double TravelCost);

/// <summary>
/// Per-settlement migration totals for the turn just computed (T2.5, owned by
/// MigrationSystem; rebuilt each turn): the chronicle observables T2.9 reads
/// for surge events. People counts, but NOT conserved stocks — bookkeeping of
/// transfers that already conserve by construction.
/// </summary>
public record struct MigrationFlowRow(SettlementId Settlement, long Inflow, long Outflow);

/// <summary>
/// Per-settlement SMOOTHED attractiveness (T2.8 migration stabilization,
/// owned by MigrationSystem): an EMA (first-order low-pass, TUNE window) over
/// the instantaneous per-capita attractiveness that DRIVES migration desire —
/// a one-turn emptying can no longer mint a one-turn magnet. Persistent sim
/// state (a filter carries memory); rows appear lazily the first turn a
/// settlement is seen (initialized AT the instantaneous value — the filter
/// starts converged, not at zero).
/// </summary>
public record struct SmoothedAttractivenessRow(SettlementId Settlement, double Value);

/// <summary>
/// T3.4b (CR-003 ruling §3): per-settlement harvest weather. LogDeviation is the
/// AR(1) state in LOG space — it lives in the row so it clones and snapshots
/// (systems are stateless). Multiplier is exp(LogDeviation − σ²/2), the
/// mean-one factor applied to realised farm output this turn.
///
/// MEAN ONE IS THE POINT. E[exp(x)] = exp(σ²/2) for x ~ N(0, σ²), so the −σ²/2
/// correction makes E[Multiplier] exactly 1. Weather changes the VARIANCE of
/// yield and never its expectation, which is what keeps the derived 26.0 a
/// statement about land quality under normal conditions (CR-003: the multiplier
/// "multiplies OUTPUT and never alters the derived 26.0"). Without the
/// correction, adding weather would silently raise mean yield by exp(σ²/2) and
/// re-open a constant the director derived.
/// </summary>
public record struct HarvestWeatherRow(SettlementId Settlement, double LogDeviation, double Multiplier);

/// <summary>
/// T3.6 (D-034): one realised trade flow for the turn just computed — Quantity
/// units of Good moved From → To (always the low-price → high-price direction
/// at COMPUTE time). Owned by the trade system, REBUILT each turn (cleared
/// first — the T3.3 stale-row precedent); rows only for flows that actually
/// moved (Quantity > 0). The R1/R2 observables and the T3.7 merchant-emergence
/// signal read this table. Bookkeeping of transfers that already conserve by
/// construction (Ledger.Transfer), NOT itself a conserved stock.
/// </summary>
public record struct TradeFlowRow(SettlementId From, SettlementId To, GoodId Good, long Quantity);

/// <summary>
/// T3.8 — the HOUSING stock (director ruling: maintenance, not abstract
/// decay). Dwellings is a CONSERVED capital stock (quantity
/// ConservedQuantityIds.Dwellings): built from real materials via Ledger
/// (source HousingBuilt; timber+clay sunk via HousingMaterials), degraded by
/// UNMET MAINTENANCE via Ledger sink HousingDecayed — fully maintained
/// housing persists indefinitely. BuildRemainder/DecayRemainder are the
/// standard sub-unit banks (§3.3). LastMaintenanceFraction ∈ [0,1] is the
/// share of upkeep materials actually available last step (observational —
/// UI and the H1 curve read it). LastLaborUsed is the construction labor
/// (adult-years) housing consumed this turn — the PUBLISHED ROW PathBuild
/// subtracts from its banked labor at the standard §3.2 one-turn lag, which
/// is how the construction pool is split without a system-to-system
/// reference (law 6).
/// </summary>
/// <remarks>Field-based (not a record struct) so Ledger can take `ref` to the stock.</remarks>
public struct HousingRow(
    SettlementId settlement, Conserved dwellings,
    double buildRemainder, double decayRemainder,
    double lastMaintenanceFraction, double lastLaborUsed) : IEquatable<HousingRow>
{
    public SettlementId Settlement = settlement;
    public Conserved Dwellings = dwellings;
    public double BuildRemainder = buildRemainder;
    public double DecayRemainder = decayRemainder;
    public double LastMaintenanceFraction = lastMaintenanceFraction;
    public double LastLaborUsed = lastLaborUsed;

    public readonly bool Equals(HousingRow other) =>
        Settlement == other.Settlement && Dwellings == other.Dwellings
        && BuildRemainder.Equals(other.BuildRemainder) && DecayRemainder.Equals(other.DecayRemainder)
        && LastMaintenanceFraction.Equals(other.LastMaintenanceFraction)
        && LastLaborUsed.Equals(other.LastLaborUsed);
    public override readonly bool Equals(object? obj) => obj is HousingRow other && Equals(other);
    public override readonly int GetHashCode() => Settlement.Value; // gate:allow-gethashcode — equality plumbing, never logic input
}

/// <summary>
/// T3.4 (D-033): the price of one good in one settlement, in GRAIN units —
/// grain is the numeraire and its own price is pinned at exactly 1.0. Prices
/// are ratios, so `double` (law 7); they are NOT conserved and never move
/// through the Ledger.
/// </summary>
public record struct PriceRow(SettlementId Settlement, GoodId Good, double Price);

/// <summary>
/// T3.4 (D-033) EXPLAINABILITY — the glass-box law made real. One row per
/// (settlement, good) per turn recording WHY the price moved, decomposed into
/// the four excess-demand terms plus the clamp.
///
/// The four economic terms are each computed from a DIFFERENT measured input
/// (consumption demand, recipe input demand, production, stock release) sharing
/// only the common factor k = lambda * price * dtYears / marketScale. That is
/// what makes the decomposition falsifiable rather than decorative: attributing
/// a term to the wrong input changes how it RESPONDS to a perturbation of that
/// input, which is what the per-term sensitivity tests measure. A sum check
/// alone cannot see a permuted label, so the sum check is necessary and is
/// never the only check (director ruling, docs/t3.4-lens-manifest.md).
///
/// INVARIANT: Consumption + InputDemand + Production + StockRelease + Clamp
/// == Delta, and PrevPrice + Delta == the new PriceRow.Price.
/// </summary>
public record struct PriceTermRow(
    SettlementId Settlement, GoodId Good,
    double PrevPrice,
    double Consumption,
    double InputDemand,
    double Production,
    double StockRelease,
    double Clamp,
    double Delta);

/// <summary>
/// Per-settlement vital counts for the turn just computed (T2.6, owned by
/// DemographicsSystem; rebuilt each turn): Births = the Births flow credited,
/// Deaths = base deaths PLUS starvation sunk, DtYears = the dt they occurred
/// over (so a Prev-reader can form per-year rates dt-correctly even across an
/// era-pacing transition). Chronicle observables — the D-021 generational
/// turnover input and a T2.9 chronicle surface. People counts, NOT conserved
/// stocks: bookkeeping of flows the Ledger already audits.
/// </summary>
public record struct SettlementVitalsRow(SettlementId Settlement, long Births, long Deaths, double DtYears);

/// <summary>
/// One (settlement, class, need) satisfaction value in [0,1] (T2.6, D-018;
/// owned by NeedsGrievanceSystem, rebuilt each turn from PREV supply signals).
/// Rows exist only for BOUND needs — M2 binds Sustenance alone, from the Prev
/// consumption deficit. Classes carry EQUAL values at M2 (consumption
/// clamping is settlement-wide); the rows are per-class because M3's class
/// consumption baskets differentiate them — honesty note, not a mechanism.
/// </summary>
public record struct NeedSatisfactionRow(SettlementId Settlement, ClassId Class, int NeedId, double Value);

/// <summary>
/// One (settlement, class) grievance stock (T2.6, D-018/D-021; owned by
/// NeedsGrievanceSystem). A DOUBLE stock and deliberately NOT conserved —
/// grievance is manufactured by deprivation and destroyed by decay, so it
/// never touches the Ledger (law 1 governs people/money/goods; this is none
/// of them). Integration is explicit and dt-correct (see the system header).
/// Persistent sim state (serialized); seeded at founding at 0.
/// </summary>
public record struct GrievanceRow(SettlementId Settlement, ClassId Class, double Value);

/// <summary>
/// Cumulative source/sink counterweights per (quantity, reason) — written only by
/// Ledger.Flow (§3.6). These rows make conservation exactly auditable:
/// Σ stocks + Σ TotalSunk − Σ TotalSourced = 0 per quantity, at every turn.
/// </summary>
public record struct LedgerFlowRow(ConservedQuantityId Quantity, ReasonId Reason, long TotalSourced, long TotalSunk);

/// <summary>
/// State of one PCG32 stream (kernel contract §3.5: stream states live inside
/// WorldState so save/load/replay preserve randomness exactly). Keyed by
/// (System, Region); State/Inc are the canonical PCG32 pair (Inc always odd).
/// Unmanaged row per ADR-001 — travels with the double-buffer clone for free.
/// </summary>
public record struct RngStreamRow(SystemId System, RegionId Region, ulong State, ulong Inc);

/// <summary>
/// T4.3 (D-037 A3): a polity's claim to a settlement — WHICH polities assert a
/// right to it. Multiple polities may claim the SAME settlement simultaneously
/// (D-037 A3); this is why claim is a RELATION keyed by (Polity, Place), never
/// an owner-id field on the settlement row — an owner-id cannot express
/// overlap. Strength is the claim's own magnitude (D-037 C1: strengthens with
/// recognition, population of the claiming culture, continuous assertion;
/// decays with time and disuse) — SCHEMA ONLY at T4.3: no system computes,
/// writes, or decays this field yet; that is a later packet's mechanism.
/// </summary>
public record struct ClaimRow(PolityId Polity, SettlementId Place, double Strength);

/// <summary>
/// T4.3 (D-037 A3 / D-040 C7): which polity's orders a settlement actually
/// obeys — distinct from claim (who asserts a right) and recognition (who
/// acknowledges another's claim). A RELATION keyed by (Polity, Place), NEVER
/// an owner-id field on the settlement row (T4.3 PROHIBITED 1) — but D-037
/// A3 is explicit that control's CARDINALITY is "exactly one, or none
/// (stateless)": at most one ControlRow should exist per Place. D-040 C7
/// does NOT amend that cardinality; its "contested where claims overlap"
/// requirement is about CLAIM's multiplicity and about a later RESOLUTION
/// mechanism (out of scope here) choosing which polity's control row, if
/// any, exists — not about CONTROL itself holding multiple simultaneous
/// rows for one place. Strength is that value's SLOT, not a stored decay
/// term (T4.3 PROHIBITED 3): no distance-decay, communication-latency, or
/// loyalty computation exists yet — this field is written by nothing in
/// this packet, and nothing in this packet enforces the one-or-none
/// cardinality either (no system exists yet to violate it).
/// </summary>
public record struct ControlRow(PolityId Polity, SettlementId Place, double Strength);

/// <summary>
/// T4.8 — ONE NOTABLE, AND THE PERSON THEY ARE (R-1 RULED, m4-spec §7).
///
/// R-1 chose Option B over Option A deliberately: "A NOTABLE IS A PERSON.
/// Extracted from the bucket via `Ledger.Transfer`; a conserved population stock
/// with births, deaths and a law-1 audit." So <see cref="Count"/> is a CONSERVED
/// stock of <see cref="ConservedQuantityIds.Population"/> — the same quantity a
/// bucket holds — and it is 1 while this notable lives, 0 once the slot is
/// vacated. It is NOT a flag, and that is the whole point: a general who dies or
/// defects moves a real person, so lifecycle and defection are conservation-exact
/// instead of each inventing its own bookkeeping.
///
/// <see cref="CohortIdx"/> is the age band the person was extracted FROM, kept so
/// that a notable remains a person in the demographic sense rather than an
/// age-less token, and so the extraction can be reasoned about against the pyramid.
///
/// <see cref="Allegiance"/> is which polity they serve — the field a DEFECTION
/// changes. The person themselves moves by `Ledger.Transfer` between rows, so the
/// defection is auditable rather than a silent field flip.
///
/// DELIBERATELY ABSENT: competence, traits, experience, and every battle field.
/// Those parameterize the AutoResolver, which is M6 (m4-spec §1.6, "Strategic war
/// is AutoResolver ONLY at M4" — and the resolver itself is deferred). This row
/// carries identity, place, allegiance and the person. Nothing else.
/// </summary>
public struct NotableRow(
    NotableId id, SettlementId settlement, PolityId allegiance, int cohortIdx, Conserved count)
    : IEquatable<NotableRow>
{
    public NotableId Id = id;
    public SettlementId Settlement = settlement;
    public PolityId Allegiance = allegiance;
    public int CohortIdx = cohortIdx;

    /// <summary>The PERSON. A conserved Population stock — 1 while this notable
    /// lives here, 0 once the slot is vacated by death or defection. A FIELD, not
    /// a property, because the Ledger mutates it by `ref` like every other
    /// conserved stock in the world.</summary>
    public Conserved Count = count;

    public readonly bool Equals(NotableRow other) =>
        Id == other.Id && Settlement == other.Settlement && Allegiance == other.Allegiance
        && CohortIdx == other.CohortIdx && Count == other.Count;
    public override readonly bool Equals(object? obj) => obj is NotableRow other && Equals(other);
    public override readonly int GetHashCode() => Id.Value; // gate:allow-gethashcode — equality plumbing, never logic input
    public static bool operator ==(NotableRow a, NotableRow b) => a.Equals(b);
    public static bool operator !=(NotableRow a, NotableRow b) => !a.Equals(b);
}

/// <summary>
/// T4.3 (D-037 A3): which OTHER polity's claim a polity acknowledges —
/// bilateral and ASYMMETRIC by construction: a row (Recogniser, Recognised)
/// never implies its reverse. NEVER a boolean/flag field on a polity row
/// (T4.3 PROHIBITED 2), which could only express "recognised by someone" or
/// a single counterparty, not the many-to-many asymmetric relation D-037
/// requires. Row PRESENCE is the fact: no payload beyond the two keys is
/// needed at T4.3 (D-037 C7's diplomatic consequences are a later, M7
/// packet).
/// </summary>
public record struct RecognitionRow(PolityId Recogniser, PolityId Recognised);

/// <summary>
/// D-042 §6 — WHO ISSUES A POLITY'S ORDERS. Player and AI are different COMMAND
/// SOURCES acting on the same state model, never different simulation entities
/// (D-042 §1.4, §6.6: "the simulation must not contain separate gameplay physics
/// for human- and AI-controlled Empires"). Serialized as its underlying int so
/// the canonical stream stays platform-independent.
/// </summary>
public enum CommandSource
{
    /// <summary>Default. Every polity is AI-commanded unless designated otherwise.</summary>
    Ai = 0,
    /// <summary>The single human-controlled Empire (D-042 §1.1).</summary>
    Player = 1,
}

/// <summary>
/// D-042 — THE EMPIRE ROSTER. One row per strategic actor.
///
/// THE IDENTITY IS <see cref="PolityId"/>, REUSED, NOT REPLACED. D-042 §13
/// reconciles "Empire" with D-037's polity: the Empire is the strategic-actor
/// ROLE of the existing polity, not a third abstraction, so no `EmpireId` is
/// introduced and no rename is implied.
///
/// THE ROW IS DELIBERATELY LIGHTWEIGHT (D-042 §3.2, §12): identity plus command
/// source and nothing else. Government, economy, knowledge, military and
/// institution state are SEPARATE domains that key themselves to this id — the
/// Empire must not become a God object holding every domain's state.
///
/// SETTLEMENT MEMBERSHIP IS NOT HERE, and that is load-bearing. Controlled
/// settlements are DERIVED from <see cref="ControlRow"/>, which stays the single
/// source of truth (D-042 §5). A `SettlementIds` field here would be a second
/// source of truth and would re-introduce the container D-037 A2 rejects.
/// </summary>
public record struct PolityRow(PolityId Id, CommandSource Source);

/// <summary>
/// D-042 §6 — THE CAPITAL DESIGNATION, AS A RELATION.
///
/// A RELATION, NOT A FIELD, for two reasons. (1) Absence of a row IS "no
/// capital" — no sentinel `SettlementId` is invented, and an Empire between
/// capitals is representable without a magic value. (2) It mirrors the shipped
/// claim/control/recognition idiom rather than bolting an id onto another row
/// (T4.3 PROHIBITED 1 bans exactly that shape for control).
///
/// THE CAPITAL IS NOT THE EMPIRE'S IDENTITY (D-042 §6, and the packet states the
/// error case explicitly: `EmpireId = CapitalSettlementId` is WRONG). The
/// identity is <see cref="PolityRow.Id"/> and lives in a different table, so
/// losing or moving a capital cannot change who the Empire is.
///
/// CARDINALITY: zero-or-one per polity. Nothing in this packet enforces it —
/// no system writes this table yet, exactly as Claims/Controls/Recognitions
/// shipped at T4.3.
/// </summary>
public record struct CapitalRow(PolityId Polity, SettlementId Place);

/// <summary>
/// Read-only view of the world (kernel contract §3.1). Systems read the previous
/// turn's state exclusively through this interface; it exposes only
/// <see cref="IReadOnlyTable{T}"/> views, so no mutation compiles. Writable access
/// exists solely via typed handles to a system's own tables, constructed by the
/// kernel (SimContext, lands in T0.5).
/// </summary>
public interface IReadOnlyWorldState
{
    ulong Seed { get; }
    Kernel.SimClock Clock { get; }
    Worldgen.TerrainSet? Terrain { get; }
    IReadOnlyTable<RegionRow> Regions { get; }
    IReadOnlyTable<RngStreamRow> RngStreams { get; }
    IReadOnlyTable<RainfallRow> Rainfall { get; }
    IReadOnlyTable<BiomassRow> Biomass { get; }
    IReadOnlyTable<GoodsRow> Goods { get; }
    IReadOnlyTable<LedgerFlowRow> LedgerFlows { get; }
    IReadOnlyTable<NetworkNodeRow> NetworkNodes { get; }
    IReadOnlyTable<NetworkEdgeRow> NetworkEdges { get; }
    IReadOnlyTable<SettlementRow> Settlements { get; }
    IReadOnlyTable<NetworkMetaRow> NetworkMeta { get; }
    IReadOnlyTable<CatchmentNodeRow> CatchmentNodes { get; }
    IReadOnlyTable<CatchmentSummaryRow> CatchmentSummaries { get; }
    IReadOnlyTable<BucketRow> Buckets { get; }
    IReadOnlyTable<GoodStockRow> GoodStocks { get; }
    IReadOnlyTable<DepositRow> Deposits { get; }
    IReadOnlyTable<ConsumptionDeficitRow> ConsumptionDeficits { get; }
    IReadOnlyTable<SectorAllocationRow> SectorAllocations { get; }
    IReadOnlyTable<PathProgressRow> PathProgress { get; }
    IReadOnlyTable<VariableRow> Variables { get; }
    IReadOnlyTable<ClassStateRow> ClassStates { get; }
    IReadOnlyTable<SettlementDistanceRow> SettlementDistances { get; }
    IReadOnlyTable<MigrationFlowRow> MigrationFlows { get; }
    IReadOnlyTable<SettlementVitalsRow> SettlementVitals { get; }
    IReadOnlyTable<NeedSatisfactionRow> NeedSatisfactions { get; }
    IReadOnlyTable<GrievanceRow> Grievances { get; }
    IReadOnlyTable<SmoothedAttractivenessRow> SmoothedAttractiveness { get; }
    IReadOnlyTable<PriceRow> Prices { get; }
    IReadOnlyTable<PriceTermRow> PriceTerms { get; }
    IReadOnlyTable<HarvestWeatherRow> HarvestWeather { get; }
    IReadOnlyTable<TradeFlowRow> TradeFlows { get; }
    IReadOnlyTable<HousingRow> Housing { get; }
    IReadOnlyTable<ClaimRow> Claims { get; }
    IReadOnlyTable<ControlRow> Controls { get; }

    /// <summary>Notables as PEOPLE (T4.8, R-1) — owned by no system yet; M4 ships the
    /// conserved carrier and its lifecycle operations, not a spawner.</summary>
    IReadOnlyTable<NotableRow> Notables { get; }
    IReadOnlyTable<RecognitionRow> Recognitions { get; }

    /// <summary>The Empire roster — identity + command source (D-042). Schema
    /// only; no system writes this table yet.</summary>
    IReadOnlyTable<PolityRow> Polities { get; }

    /// <summary>Capital designations, zero-or-one per polity (D-042 §6). Schema
    /// only; no system writes this table yet.</summary>
    IReadOnlyTable<CapitalRow> Capitals { get; }
}

/// <summary>
/// The single source of truth (kernel contract §3.1): plain data — a set of tables,
/// each owned by exactly one system. RNG stream states join in T0.3 and the clock in
/// T0.4; every added field MUST be included in <see cref="Clone"/> (the double-buffer
/// copy, §3.2) — the clone round-trip tests guard this.
/// </summary>
public sealed class WorldState : IReadOnlyWorldState
{
    /// <summary>World seed (D-007/D-008: root of all stream derivation and replay).</summary>
    public ulong Seed { get; }

    /// <summary>The clock (§3.4, ADR-002) — advanced by the kernel, once per turn.</summary>
    public Kernel.SimClock Clock { get; set; }

    /// <summary>
    /// Immutable terrain rasters (ADR-008): set once after worldgen, shared by
    /// REFERENCE across Clone (immutable data needs no double buffer). The
    /// terrain content hash is folded into WorldHash by the canonical schema.
    /// </summary>
    public Worldgen.TerrainSet? Terrain { get; set; }

    public Table<RegionRow> Regions { get; }

    /// <summary>PCG32 stream states (§3.5) — owned by the kernel's RngRegistry.</summary>
    public Table<RngStreamRow> RngStreams { get; }

    /// <summary>Per-region rainfall — owned by WeatherSystem (M0 toy).</summary>
    public Table<RainfallRow> Rainfall { get; }

    /// <summary>Per-region biomass stock — owned by GrowthSystem (M0 toy).</summary>
    public Table<BiomassRow> Biomass { get; }

    /// <summary>Per-region toy-good stock — owned by TradeSystem (M0 toy).</summary>
    public Table<GoodsRow> Goods { get; }

    /// <summary>Source/sink counterweights — written only by Ledger (§3.6).</summary>
    public Table<LedgerFlowRow> LedgerFlows { get; }

    /// <summary>Transport-network nodes (T1.3) — write ownership assigned at T1.6.</summary>
    public Table<NetworkNodeRow> NetworkNodes { get; }

    /// <summary>Transport-network edges (T1.3) — write ownership assigned at T1.6.</summary>
    public Table<NetworkEdgeRow> NetworkEdges { get; }

    /// <summary>Settlements (T1.4) — created by worldgen founding; never assumes one row.</summary>
    public Table<SettlementRow> Settlements { get; }

    /// <summary>Network revision counter (T1.4/D-016) — initialized by worldgen, incremented by PathBuild (T1.6).</summary>
    public Table<NetworkMetaRow> NetworkMeta { get; }

    /// <summary>Catchment membership (T1.4) — owned by CatchmentSystem (derived state, D-016).</summary>
    public Table<CatchmentNodeRow> CatchmentNodes { get; }

    /// <summary>Catchment summaries (T1.4) — owned by CatchmentSystem (derived state, D-016).</summary>
    public Table<CatchmentSummaryRow> CatchmentSummaries { get; }

    /// <summary>Population buckets (T2.1, D-026) — conserved stocks, owned by DemographicsSystem.</summary>
    public Table<BucketRow> Buckets { get; }

    /// <summary>Per-(settlement, good) stocks (T3.2, D-031) — conserved; grain carries the M2 FoodStore role (see SystemCatalog).</summary>
    public Table<GoodStockRow> GoodStocks { get; }

    /// <summary>Per-(settlement, good) deposit abundances (T3.2) — founding-rolled, immutable observational state.</summary>
    public Table<DepositRow> Deposits { get; }

    /// <summary>Per-settlement consumption deficits (T1.5) — owned by ConsumptionSystem.</summary>
    public Table<ConsumptionDeficitRow> ConsumptionDeficits { get; }

    /// <summary>Labor splits (T1.6) — owned by PathBuildSystem (order consumer).</summary>
    public Table<SectorAllocationRow> SectorAllocations { get; }

    /// <summary>Path-build banks + frontiers (T1.6) — owned by PathBuildSystem.</summary>
    public Table<PathProgressRow> PathProgress { get; }

    /// <summary>Published variables (T2.2, D-020) — owned by ClassMobilitySystem.</summary>
    public Table<VariableRow> Variables { get; }

    /// <summary>Class emergence latches (T2.2, D-020) — owned by ClassMobilitySystem.</summary>
    public Table<ClassStateRow> ClassStates { get; }

    /// <summary>Pairwise travel costs (T2.5) — owned by CatchmentSystem (D-016 piggyback).</summary>
    public Table<SettlementDistanceRow> SettlementDistances { get; }

    /// <summary>Per-turn migration totals (T2.5) — owned by MigrationSystem (chronicle hooks).</summary>
    public Table<MigrationFlowRow> MigrationFlows { get; }

    /// <summary>Per-turn vital counts (T2.6) — owned by DemographicsSystem (chronicle + D-021 turnover input).</summary>
    public Table<SettlementVitalsRow> SettlementVitals { get; }

    /// <summary>Per-(settlement, class, bound-need) satisfaction (T2.6) — owned by NeedsGrievanceSystem.</summary>
    public Table<NeedSatisfactionRow> NeedSatisfactions { get; }

    /// <summary>Per-(settlement, class) grievance stocks (T2.6) — owned by NeedsGrievanceSystem.</summary>
    public Table<GrievanceRow> Grievances { get; }

    /// <summary>Per-settlement EMA-smoothed attractiveness (T2.8) — owned by MigrationSystem.</summary>
    public Table<SmoothedAttractivenessRow> SmoothedAttractiveness { get; }

    /// <summary>Per-(settlement, good) price in grain units (T3.4) — owned by PriceSystem.</summary>
    public Table<PriceRow> Prices { get; }

    /// <summary>Per-(settlement, good) price-change decomposition (T3.4) — owned by PriceSystem.</summary>
    public Table<PriceTermRow> PriceTerms { get; }

    /// <summary>Per-settlement harvest weather (T3.4b) — owned by HarvestWeatherSystem.</summary>
    public Table<HarvestWeatherRow> HarvestWeather { get; }

    /// <summary>Per-turn realised trade flows (T3.6, D-034) — owned by TradeArbitrageSystem.</summary>
    public Table<TradeFlowRow> TradeFlows { get; }

    /// <summary>Per-settlement housing stock (T3.8) — owned by HousingSystem.</summary>
    public Table<HousingRow> Housing { get; }

    /// <summary>Polity claims to a settlement (T4.3, D-037 A3) — schema only; no
    /// system writes this table yet.</summary>
    public Table<ClaimRow> Claims { get; }

    /// <summary>Which polity's orders a settlement obeys (T4.3, D-037 A3) —
    /// schema only; no system writes this table yet.</summary>
    public Table<ControlRow> Controls { get; }
    public Table<NotableRow> Notables { get; }

    /// <summary>Bilateral, asymmetric claim recognition between polities (T4.3,
    /// D-037 A3) — schema only; no system writes this table yet.</summary>
    public Table<RecognitionRow> Recognitions { get; }

    /// <summary>The Empire roster (D-042) — schema only; no system writes it yet.</summary>
    public Table<PolityRow> Polities { get; }

    /// <summary>Capital designations (D-042 §6) — schema only; no system writes it yet.</summary>
    public Table<CapitalRow> Capitals { get; }

    IReadOnlyTable<RegionRow> IReadOnlyWorldState.Regions => Regions;
    IReadOnlyTable<RngStreamRow> IReadOnlyWorldState.RngStreams => RngStreams;
    IReadOnlyTable<RainfallRow> IReadOnlyWorldState.Rainfall => Rainfall;
    IReadOnlyTable<BiomassRow> IReadOnlyWorldState.Biomass => Biomass;
    IReadOnlyTable<GoodsRow> IReadOnlyWorldState.Goods => Goods;
    IReadOnlyTable<LedgerFlowRow> IReadOnlyWorldState.LedgerFlows => LedgerFlows;
    IReadOnlyTable<NetworkNodeRow> IReadOnlyWorldState.NetworkNodes => NetworkNodes;
    IReadOnlyTable<NetworkEdgeRow> IReadOnlyWorldState.NetworkEdges => NetworkEdges;
    IReadOnlyTable<SettlementRow> IReadOnlyWorldState.Settlements => Settlements;
    IReadOnlyTable<NetworkMetaRow> IReadOnlyWorldState.NetworkMeta => NetworkMeta;
    IReadOnlyTable<CatchmentNodeRow> IReadOnlyWorldState.CatchmentNodes => CatchmentNodes;
    IReadOnlyTable<CatchmentSummaryRow> IReadOnlyWorldState.CatchmentSummaries => CatchmentSummaries;
    IReadOnlyTable<BucketRow> IReadOnlyWorldState.Buckets => Buckets;
    IReadOnlyTable<GoodStockRow> IReadOnlyWorldState.GoodStocks => GoodStocks;
    IReadOnlyTable<DepositRow> IReadOnlyWorldState.Deposits => Deposits;
    IReadOnlyTable<ConsumptionDeficitRow> IReadOnlyWorldState.ConsumptionDeficits => ConsumptionDeficits;
    IReadOnlyTable<SectorAllocationRow> IReadOnlyWorldState.SectorAllocations => SectorAllocations;
    IReadOnlyTable<PathProgressRow> IReadOnlyWorldState.PathProgress => PathProgress;
    IReadOnlyTable<VariableRow> IReadOnlyWorldState.Variables => Variables;
    IReadOnlyTable<ClassStateRow> IReadOnlyWorldState.ClassStates => ClassStates;
    IReadOnlyTable<SettlementDistanceRow> IReadOnlyWorldState.SettlementDistances => SettlementDistances;
    IReadOnlyTable<MigrationFlowRow> IReadOnlyWorldState.MigrationFlows => MigrationFlows;
    IReadOnlyTable<SettlementVitalsRow> IReadOnlyWorldState.SettlementVitals => SettlementVitals;
    IReadOnlyTable<NeedSatisfactionRow> IReadOnlyWorldState.NeedSatisfactions => NeedSatisfactions;
    IReadOnlyTable<GrievanceRow> IReadOnlyWorldState.Grievances => Grievances;
    IReadOnlyTable<SmoothedAttractivenessRow> IReadOnlyWorldState.SmoothedAttractiveness => SmoothedAttractiveness;
    IReadOnlyTable<PriceRow> IReadOnlyWorldState.Prices => Prices;
    IReadOnlyTable<PriceTermRow> IReadOnlyWorldState.PriceTerms => PriceTerms;
    IReadOnlyTable<HarvestWeatherRow> IReadOnlyWorldState.HarvestWeather => HarvestWeather;
    IReadOnlyTable<TradeFlowRow> IReadOnlyWorldState.TradeFlows => TradeFlows;
    IReadOnlyTable<HousingRow> IReadOnlyWorldState.Housing => Housing;
    IReadOnlyTable<ClaimRow> IReadOnlyWorldState.Claims => Claims;
    IReadOnlyTable<ControlRow> IReadOnlyWorldState.Controls => Controls;
    IReadOnlyTable<NotableRow> IReadOnlyWorldState.Notables => Notables;
    IReadOnlyTable<RecognitionRow> IReadOnlyWorldState.Recognitions => Recognitions;
    IReadOnlyTable<PolityRow> IReadOnlyWorldState.Polities => Polities;
    IReadOnlyTable<CapitalRow> IReadOnlyWorldState.Capitals => Capitals;

    public WorldState(ulong seed = 0UL)
    {
        Seed = seed;
        Regions = new Table<RegionRow>();
        RngStreams = new Table<RngStreamRow>();
        Rainfall = new Table<RainfallRow>();
        Biomass = new Table<BiomassRow>();
        Goods = new Table<GoodsRow>();
        LedgerFlows = new Table<LedgerFlowRow>();
        NetworkNodes = new Table<NetworkNodeRow>();
        NetworkEdges = new Table<NetworkEdgeRow>();
        Settlements = new Table<SettlementRow>();
        NetworkMeta = new Table<NetworkMetaRow>();
        CatchmentNodes = new Table<CatchmentNodeRow>();
        CatchmentSummaries = new Table<CatchmentSummaryRow>();
        Buckets = new Table<BucketRow>();
        GoodStocks = new Table<GoodStockRow>();
        Deposits = new Table<DepositRow>();
        ConsumptionDeficits = new Table<ConsumptionDeficitRow>();
        SectorAllocations = new Table<SectorAllocationRow>();
        PathProgress = new Table<PathProgressRow>();
        Variables = new Table<VariableRow>();
        ClassStates = new Table<ClassStateRow>();
        SettlementDistances = new Table<SettlementDistanceRow>();
        MigrationFlows = new Table<MigrationFlowRow>();
        SettlementVitals = new Table<SettlementVitalsRow>();
        NeedSatisfactions = new Table<NeedSatisfactionRow>();
        Grievances = new Table<GrievanceRow>();
        SmoothedAttractiveness = new Table<SmoothedAttractivenessRow>();
        Prices = new Table<PriceRow>();
        PriceTerms = new Table<PriceTermRow>();
        HarvestWeather = new Table<HarvestWeatherRow>();
        TradeFlows = new Table<TradeFlowRow>();
        Housing = new Table<HousingRow>();
        Claims = new Table<ClaimRow>();
        Controls = new Table<ControlRow>();
        Notables = new Table<NotableRow>();
        Recognitions = new Table<RecognitionRow>();
        Polities = new Table<PolityRow>();
        Capitals = new Table<CapitalRow>();
    }

    private WorldState(
        ulong seed, Kernel.SimClock clock, Table<RegionRow> regions, Table<RngStreamRow> rngStreams,
        Table<RainfallRow> rainfall, Table<BiomassRow> biomass, Table<GoodsRow> goods,
        Table<LedgerFlowRow> ledgerFlows, Table<NetworkNodeRow> networkNodes,
        Table<NetworkEdgeRow> networkEdges, Table<SettlementRow> settlements,
        Table<NetworkMetaRow> networkMeta, Table<CatchmentNodeRow> catchmentNodes,
        Table<CatchmentSummaryRow> catchmentSummaries, Table<BucketRow> buckets,
        Table<GoodStockRow> goodStocks, Table<DepositRow> deposits,
        Table<ConsumptionDeficitRow> consumptionDeficits,
        Table<SectorAllocationRow> sectorAllocations, Table<PathProgressRow> pathProgress,
        Table<VariableRow> variables, Table<ClassStateRow> classStates,
        Table<SettlementDistanceRow> settlementDistances, Table<MigrationFlowRow> migrationFlows,
        Table<SettlementVitalsRow> settlementVitals, Table<NeedSatisfactionRow> needSatisfactions,
        Table<GrievanceRow> grievances, Table<SmoothedAttractivenessRow> smoothedAttractiveness,
        Table<PriceRow> prices, Table<PriceTermRow> priceTerms,
        Table<HarvestWeatherRow> harvestWeather, Table<TradeFlowRow> tradeFlows,
        Table<HousingRow> housing, Table<ClaimRow> claims, Table<ControlRow> controls,
        Table<RecognitionRow> recognitions, Table<NotableRow> notables,
        Table<PolityRow> polities, Table<CapitalRow> capitals)
    {
        Seed = seed;
        Clock = clock;
        Regions = regions;
        RngStreams = rngStreams;
        Rainfall = rainfall;
        Biomass = biomass;
        Goods = goods;
        LedgerFlows = ledgerFlows;
        NetworkNodes = networkNodes;
        NetworkEdges = networkEdges;
        Settlements = settlements;
        NetworkMeta = networkMeta;
        CatchmentNodes = catchmentNodes;
        CatchmentSummaries = catchmentSummaries;
        Buckets = buckets;
        GoodStocks = goodStocks;
        Deposits = deposits;
        ConsumptionDeficits = consumptionDeficits;
        SectorAllocations = sectorAllocations;
        PathProgress = pathProgress;
        Variables = variables;
        ClassStates = classStates;
        SettlementDistances = settlementDistances;
        MigrationFlows = migrationFlows;
        SettlementVitals = settlementVitals;
        NeedSatisfactions = needSatisfactions;
        Grievances = grievances;
        SmoothedAttractiveness = smoothedAttractiveness;
        Prices = prices;
        PriceTerms = priceTerms;
        HarvestWeather = harvestWeather;
        TradeFlows = tradeFlows;
        Housing = housing;
        Claims = claims;
        Controls = controls;
        Notables = notables;
        Recognitions = recognitions;
        Polities = polities;
        Capitals = capitals;
    }

    /// <summary>
    /// Full deep copy — the §3.2 double-buffer step (Prev → Next). The copy shares no
    /// mutable state with the source: mutating one never affects the other. Every
    /// field of WorldState MUST be carried here; the clone round-trip tests guard this.
    /// </summary>
    public WorldState Clone() =>
        new(Seed, Clock, Regions.Clone(), RngStreams.Clone(), Rainfall.Clone(), Biomass.Clone(),
            Goods.Clone(), LedgerFlows.Clone(), NetworkNodes.Clone(), NetworkEdges.Clone(),
            Settlements.Clone(), NetworkMeta.Clone(), CatchmentNodes.Clone(),
            CatchmentSummaries.Clone(), Buckets.Clone(), GoodStocks.Clone(), Deposits.Clone(),
            ConsumptionDeficits.Clone(), SectorAllocations.Clone(), PathProgress.Clone(),
            Variables.Clone(), ClassStates.Clone(), SettlementDistances.Clone(), MigrationFlows.Clone(),
            SettlementVitals.Clone(), NeedSatisfactions.Clone(), Grievances.Clone(),
            SmoothedAttractiveness.Clone(), Prices.Clone(), PriceTerms.Clone(),
            HarvestWeather.Clone(), TradeFlows.Clone(), Housing.Clone(),
            Claims.Clone(), Controls.Clone(), Recognitions.Clone(), Notables.Clone(),
            Polities.Clone(), Capitals.Clone())
        {
            Terrain = Terrain, // ADR-008: immutable — reference shared, never copied
        };
}
