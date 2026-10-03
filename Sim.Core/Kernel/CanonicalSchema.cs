using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// THE canonical serialization chokepoint (§3.8, ADR-005). Every table and every
/// field of WorldState is written here, FIELD BY FIELD, in the fixed order listed
/// in this one reviewable file. No reflection (.NET guarantees no member order),
/// no raw struct memory / MemoryMarshal (padding bytes are a determinism hazard;
/// memcpy stays licensed for Clone() only — ADR-001). Doubles serialize as raw
/// IEEE-754 bits with NO normalization of -0.0/NaN: bit-exactness is the point.
/// BinaryWriter is explicitly little-endian for all primitives.
///
/// ADDING STATE? Three edits, same file: (1) write in Write, (2) read in Read,
/// (3) width in ExpectedLength. The anti-padding length test and the golden hash
/// will fail loudly until all three agree — that is their job.
/// </summary>
public static class CanonicalSchema
{
    /// <summary>Bumped on ANY schema change. Saves break between milestones (D-008).
    /// v2 (T1.1, ADR-008): terrain presence flag + terrain content hash after the clock.
    /// v3 (T1.3): NetworkNodes + NetworkEdges tables after LedgerFlows.
    /// v4 (T1.4): Settlements, NetworkMeta, CatchmentNodes, CatchmentSummaries after NetworkEdges.
    /// v5 (T1.5): PopBands, FoodStores, ConsumptionDeficits after CatchmentSummaries.
    /// v6 (T1.6): SectorAllocations, PathProgress after ConsumptionDeficits.
    /// v7 (T2.1, D-026): Buckets (settlement, culture, religion, class, cohort)
    /// replaces PopBands in the same stream position.
    /// v8 (T2.2, D-020): BucketRow gains MobilityRemainder, FoodStoreRow gains
    /// LastHarvestUnits, ConsumptionDeficitRow gains DemandUnits; Variables and
    /// ClassStates tables appended after PathProgress.
    /// v9 (T2.5): BucketRow gains MigrationRemainder; SettlementDistances and
    /// MigrationFlows tables appended after ClassStates.
    /// v10 (T2.7): BucketRow gains ReboundReservoir (deferred-conception bank
    /// for the post-famine fertility rebound, cohort-0 rows only).
    /// v11 (T2.6, D-018/D-021): SettlementVitals, NeedSatisfactions and
    /// Grievances tables appended after MigrationFlows.
    /// v12 (T2.8, migration stabilization): SmoothedAttractiveness table
    /// appended after Grievances (the EMA filter state).
    /// v13 (T3.2, D-031): FoodStores REPLACED by GoodStocks (per settlement ×
    /// good; grain carries the migrated FoodStore) + the Deposits table.
    /// v14 (T3.3, D-032): LaborAllocationRow (Settlement + FarmShare) becomes
    /// SectorAllocationRow (Settlement + five sector weights) — the farm/path
    /// pair generalizes to farming/herding/extraction/crafting/construction.
    /// Row width 12 → 44; the table's position in the stream is unchanged.
    /// v15 (T3.4, D-033): GoodStockRow gains LastInputDemandUnits and
    /// LastConsumptionDemandUnits (the two observational demand signals the
    /// price solver reads); Prices and PriceTerms tables appended after
    /// SmoothedAttractiveness.
    /// v16 (T3.4b, CR-003 §3): HarvestWeather table appended after PriceTerms —
    /// the per-settlement AR(1) weather state and its mean-one multiplier.
    /// v17 (T3.5, D-035): GoodStockRow gains LastConsumptionEatenUnits — the
    /// post-clamp companion to LastConsumptionDemandUnits, without which a
    /// basket's fill ratio is not recoverable from published state.
    /// v18 (T3.6, D-034): TradeFlows table appended after HarvestWeather — the
    /// per-turn realised trade flows (From, To, Good, Quantity).
    /// v19 (T3.8): Housing table appended after TradeFlows (the per-settlement
    /// dwelling stock with its remainders and published labor/maintenance
    /// observables); CatchmentSummaryRow gains SizeTier (the quantized
    /// settlement-size step the summary was computed at — the D-016 gate for
    /// the size catchment bonus).
    /// v22 (T4.4, D-037 B1): BucketRow gains UnplacedDeparture and
    /// UnplacedRemainder — the departure demand that found NO viable existing
    /// destination (the quantity MigrationSystem never formed because its flight
    /// term only exists multiplied by damping x viability), plus its D-004
    /// accumulator. Two bit-exact doubles appended to the bucket row; no table
    /// joined or left the stream.
    /// v21 (T4.8, R-1): Notables table appended after Recognitions. R-1 ruled a
    /// notable is a PERSON, so the row carries a CONSERVED Population count
    /// extracted from a bucket via Ledger.Transfer - not a label. No system
    /// writes it yet: M4 ships the carrier and its lifecycle operations, and the
    /// spawner is later work.
    /// v20 (T4.3, D-037 A3): Claims, Controls and Recognitions tables appended
    /// after Housing — the claim/control/recognition data model, schema only
    /// (no system writes any of the three yet). Three separate RELATION
    /// tables, never an owner-id field or a boolean flag (T4.3's three named
    /// prohibitions).</summary>
    /// v25 (T4.21-1, CR-015 §3.3): Disasters table appended after Structures —
    /// the per-settlement famine-class production shock (Kind, Severity,
    /// RemainingYears, Multiplier, AppliedMultiplier), rebuilt each step; rows
    /// only where a shock is active or was just applied. COLLISION NOTE, per the
    /// director-ruled v22/v23 precedent: the unmerged `m5-full-build` branch also
    /// carries a "v25" (TaxPolicies, SystemId 22 for GovernanceSystem). Whichever
    /// lands on main first keeps v25 and the other becomes v26; PipelineLoader
    /// refuses duplicate ids and names at load, so a merge collision is loud.
    /// There is exactly one meaning of every version number in this file.
    /// v26 (ADR-029, D-044): ResearchTargets, ResearchProgress, ResearchCompleted,
    /// ResearchEurekas and ResearchCostModifiers appended after Disasters — the
    /// minimum authoritative research state (the one active target per polity,
    /// per-node partial progress in RP, the completed-knowledge relation, the
    /// fired-Eureka relation) plus the specialized-university cost-factor input
    /// contract, which no system writes yet. Node ids are the STABLE keys of
    /// research.json. COLLISION NOTE, appended to the v25 note above: v25 is
    /// T4.21-1's Disasters (it landed on main first), so the unmerged
    /// `m5-full-build` "v25" (TaxPolicies) now takes v27 when it rebases, not v26.
    /// It also holds OrderKind 5 and SystemId 22, and this schema's writer took
    /// OrderKind 6 and SystemId 24 so that neither collides. There is exactly
    /// one meaning of every version number in this file. ADR-029 addendum A
    /// (research finalization, before v26 reached main) appended ResearchCredits
    /// (acceleration-credit provenance per source) and ResearchExposures (the
    /// foreign-exposure input seam, no writer) and gave ResearchEurekas back its
    /// three-field layout; v26 has never been on main, so its layout changed in place.
    /// v24 (M4-D): ConstructionQueue and Structures appended after Capitals —
    /// the per-settlement construction queue (ordered by an explicit Slot, never
    /// by row position) and the completed-structure counts. No progress field
    /// exists in either, by design: construction is atomic within a turn, so
    /// there is no fractional state to persist.
    /// v23 (M4, D-042): Polities and Capitals tables appended after Notables.
    /// PolityRow is the Empire roster — the pre-existing D-037 PolityId reused as
    /// the strategic identity, carrying only its command source (AI or player), so
    /// human and AI command are structurally separated from simulation state.
    /// Membership is NOT stored here: it derives from the Controls relation, the
    /// single source of truth. CapitalRow is a RELATION, not a field: absence of a
    /// row means no capital, so capital loss is representable without destroying
    /// the surviving Empire, and the capital is a designation rather than the
    /// Empire identity. Schema only — no system writes either table yet.
    /// DIRECTOR-RULED: this work was authored as a second, independent "v22"
    /// before T4.4 merged. T4.4's v22 is authoritative because it landed on main
    /// first and is certified; these two tables are v23. There is exactly one
    /// meaning of every version number in this file.</summary>
    /// v28 (ADR-031, D-047 rulings 12-14 and 18): AgeStates, AgeEligibility,
    /// AgeTransitions, MilitaryUnits and UnitConversions appended after
    /// ResearchExposures — the polity Age (absence = founding Age), the published
    /// next-Age eligibility, the Age-transition log, the minimal military-formation
    /// state and the modernization log. v27 IS SKIPPED ON PURPOSE: the v26 note above
    /// reserves it for the unmerged `m5-full-build` TaxPolicies rebase, so that there
    /// is exactly one meaning of every version number in this file.
    /// v29 (ADR-032, the transport/inter-city road foundation): TransportEdges and
    /// RoadDevelopments appended after UnitConversions — the built inter-settlement
    /// transport MULTIGRAPH (edge id, endpoints, class, mode, construction state,
    /// capacity, length, a neutral read-only condition placeholder, the in-place
    /// modernization state — target class, fraction, effective cost factor; no owner
    /// field) and the road-development log (with progress before/after). The row widths
    /// were amended on the unmerged branch before any merge (80 / 68 bytes); v29 has
    /// never existed on main with any other layout. No existing row changed width; NetworkEdges (the free
    /// DirtPath baseline) is untouched.
    /// v30 (ADR-033 D4, the M5 governing loop ported from `m5-full-build`): TaxPolicies appended after
    /// RoadDevelopments — one standing (Polity, Rate) policy per Empire, 12 bytes; absence = the
    /// never-legislated zero. No treasury, receipt or stock (CR-008). No existing row changed width;
    /// ControlRow.Strength keeps its v20 slot and now carries the administrative reach GovernanceSystem
    /// computes. v27 STAYS PERMANENTLY UNUSED: the v26/v28 notes above reserved it for the
    /// `m5-full-build` TaxPolicies rebase, but Snapshot requires an exact version match and v28/v29 already
    /// exist, so the reservation can never be what ships — the tax table is v30, and no stream will ever
    /// carry v27. There is exactly one meaning of every version number in this file.
    /// v31 (ADR-033 D6 + D10): Institutions and ConstructionLabor appended after TaxPolicies — one row per
    /// founded institution instance (stable id, owning polity, settlement, university type key, founding
    /// turn and the stored MATURITY, 32 bytes; ADR-028 §3: ACTIVE and MATURE are stored, LOCKED, AVAILABLE
    /// and SATURATED are derived), and the construction labour a settlement's queue consumed this step
    /// (12 bytes; rebuilt every step, rows only where a project was built), which PathBuild subtracts so
    /// construction capacity is spent once. No existing row changed width; a world with no institution
    /// and no completed project carries two empty count prefixes and nothing else.
    public const int Version = 31;

    // Fixed field widths per row, in bytes — the anti-padding proof sums these.
    private const int CountPrefixWidth = 4;              // int row count per table
    private const int RegionRowWidth = 4;                // RegionId
    private const int RngStreamRowWidth = 4 + 4 + 8 + 8; // SystemId, RegionId, State, Inc
    private const int RainfallRowWidth = 4 + 8;          // RegionId, rainfall bits
    private const int BiomassRowWidth = 4 + 8 + 8;       // RegionId, stock, remainder bits
    private const int GoodsRowWidth = 4 + 8;             // RegionId, stock
    private const int LedgerFlowRowWidth = 4 + 4 + 8 + 8; // Quantity, Reason, sourced, sunk
    private const int NetworkNodeRowWidth = 4 + 4;        // Id, LatticeNode
    private const int NetworkEdgeRowWidth = 4 + 4 + 4 + 4 + 8; // Id, A, B, EdgeType, Cost bits
    private const int SettlementRowWidth = 4 + 4 + 8;          // Id, SiteCell, FoundedTurn
    private const int NetworkMetaRowWidth = 4;                 // Revision
    private const int CatchmentNodeRowWidth = 4 + 4 + 8;       // Settlement, LatticeNode, TravelCost bits
    private const int CatchmentSummaryRowWidth = 4 + 4 + 8 + 4 + 8 + 4; // Settlement, NodeCount, EffectiveArableKm2 bits, NetworkRevision, LastRecomputeTurn, SizeTier (v19)
    private const int BucketRowWidth = 4 + 4 + 4 + 4 + 4 + 8 + 8 + 8 + 8 + 8 + 8 + 8 + 8 + 8 + 8; // Settlement, Culture, Religion, Class, CohortIdx, Count, 6 remainder bit-fields (v8 +Mobility, v9 +Migration), ReboundReservoir (v10), UnplacedDeparture + UnplacedRemainder (v22)
    private const int GoodStockRowWidth = 4 + 4 + 8 + 8 + 8 + 8 + 8 + 8 + 8; // + LastInputDemandUnits, LastConsumptionDemandUnits (v15), LastConsumptionEatenUnits (v17)
    private const int DepositRowWidth = 4 + 4 + 8;                  // Settlement, Good, Abundance bits (v13)
    private const int ConsumptionDeficitRowWidth = 4 + 8 + 8;       // Settlement, DeficitRatio bits, DemandUnits (v8)
    // T3.3 (D-032): Settlement + five sector weights (was Settlement + FarmShare).
    private const int SectorAllocationRowWidth = 4 + 8 + 8 + 8 + 8 + 8;
    private const int PathProgressRowWidth = 4 + 8 + 4;             // Settlement, Banked bits, FrontierNode
    private const int VariableRowWidth = 4 + 4 + 8;                 // Settlement, VarId, Value bits (v8)
    private const int ClassStateRowWidth = 4 + 4 + 4;               // Settlement, Class, Active (v8)
    private const int SettlementDistanceRowWidth = 4 + 4 + 8;       // From, To, TravelCost bits (v9)
    private const int MigrationFlowRowWidth = 4 + 8 + 8;            // Settlement, Inflow, Outflow (v9)
    private const int SettlementVitalsRowWidth = 4 + 8 + 8 + 8;     // Settlement, Births, Deaths, DtYears bits (v11)
    private const int NeedSatisfactionRowWidth = 4 + 4 + 4 + 8;     // Settlement, Class, NeedId, Value bits (v11)
    private const int GrievanceRowWidth = 4 + 4 + 8;                // Settlement, Class, Value bits (v11)
    private const int SmoothedAttractivenessRowWidth = 4 + 8;       // Settlement, Value bits (v12)
    private const int PriceRowWidth = 4 + 4 + 8;                    // Settlement, Good, Price bits (v15)
    private const int PriceTermRowWidth = 4 + 4 + 8 * 7;            // Settlement, Good, 7 double bit-fields (v15)
    private const int HarvestWeatherRowWidth = 4 + 8 + 8;           // Settlement, LogDeviation, Multiplier (v16)
    private const int TradeFlowRowWidth = 4 + 4 + 4 + 8;            // From, To, Good, Quantity (v18)
    private const int HousingRowWidth = 4 + 8 + 8 + 8 + 8 + 8;      // Settlement, Dwellings, BuildRem, DecayRem, MaintFraction, LaborUsed (v19)
    private const int ClaimRowWidth = 4 + 4 + 8;                    // Polity, Place, Strength bits (v20)
    private const int ControlRowWidth = 4 + 4 + 8;                  // Polity, Place, Strength bits (v20)
    private const int RecognitionRowWidth = 4 + 4;                  // Recogniser, Recognised (v20)
    private const int NotableRowWidth = 4 + 4 + 4 + 4 + 8;          // Id, Settlement, Allegiance, CohortIdx, Count (v21)
    private const int PolityRowWidth = 4 + 4;                       // Id, Source (v23)
    private const int CapitalRowWidth = 4 + 4;                      // Polity, Place (v23)
    private const int ConstructionQueueRowWidth = 4 + 4 + 4;        // Settlement, Slot, ProjectId (v24)
    private const int StructureRowWidth = 4 + 4 + 8;                // Settlement, ProjectId, Count (v24)
    private const int DisasterRowWidth = 4 + 4 + 8 + 8 + 8 + 8;      // Settlement, Kind, Severity, RemainingYears, Multiplier, AppliedMultiplier (v25)
    private const int ResearchTargetRowWidth = 4 + 4;               // Polity, Node (v26)
    private const int ResearchProgressRowWidth = 4 + 4 + 8;         // Polity, Node, Progress bits (v26)
    private const int ResearchCompletedRowWidth = 4 + 4;            // Polity, Node (v26)
    private const int ResearchEurekaRowWidth = 4 + 4 + 4;           // Polity, Node, Eureka (v26)
    private const int ResearchCreditRowWidth = 4 + 4 + 4 + 8;       // Polity, Node, Source, Amount bits (v26, addendum A)
    private const int ResearchExposureRowWidth = 4 + 4 + 8;         // Polity, Node, Offered bits (v26, addendum A)
    private const int ResearchCostModifierRowWidth = 4 + 4 + 8;     // Polity, UniversityType, Factor bits (v26)
    private const int AgeStateRowWidth = 4 + 4 + 8 + 4 + 8;         // Polity, Age, EnteredTurn, Surge, SurgeStartTurn (v28)
    private const int AgeEligibilityRowWidth = 4 + 4 + 8 + 4 * 6 + 1; // Polity, NextAge, EvaluatedTurn, six ints, Eligible byte (v28)
    private const int AgeTransitionRowWidth = 4 + 4 + 4 + 4 + 8 + 8; // Polity, From, To, Surge, DecisionTurn, EffectiveTurn (v28)
    private const int MilitaryUnitRowWidth = 4 + 4 + 4 + 4 + 4 + 8 + 8 + 8 + 4; // Id, Owner, Family, Identity, Location, X, Y, Experience bits, Army (v28)
    private const int UnitConversionRowWidth = 8 + 4 * 9;           // Turn, Unit, Owner, FromFamily, FromIdentity, ToFamily, ToIdentity, FromAge, ToAge, Outcome (v28)
    private const int TransportEdgeRowWidth = 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 + 4 + 8 + 8 + 4 + 8 + 8; // Id, A, B, EdgeType, Mode, State, Capacity, LengthKm bits, Condition, BuiltTurn, UpgradedTurn, TargetClass, Modernization bits, CostFactor bits (v29)
    private const int RoadDevelopmentRowWidth = 8 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 + 8 + 8;   // Turn, Polity, Edge, A, B, FromClass, ToClass, Kind, Usage, MaterialUnits, ProgressBefore bits, ProgressAfter bits (v29)
    private const int TaxPolicyRowWidth = 4 + 8;                    // Polity, Rate bits (v30)
    private const int InstitutionRowWidth = 4 + 4 + 4 + 4 + 8 + 8;  // Id, Polity, Settlement, Type, FoundedTurn, Maturity bits (v31)
    private const int ConstructionLaborRowWidth = 4 + 8;            // Settlement, LastLaborUsed bits (v31)
    private const int SeedWidth = 8;
    private const int ClockWidth = 8 + 8 + 8;            // Turn, SimDays, DtDays

    /// <summary>Writes the complete canonical state stream (schema order, declaration order).</summary>
    public static void Write(WorldState world, BinaryWriter writer)
    {
        // 1. Seed
        writer.Write(world.Seed);

        // 2. Clock
        writer.Write(world.Clock.Turn);
        writer.Write(world.Clock.SimDays);
        writer.Write(world.Clock.DtDays);

        // 2b. Terrain (ADR-008): the immutable rasters never serialize per-state;
        // their once-computed content hash is folded in, so worlds on different
        // terrain can never hash equal, and a save binds to its terrain.
        writer.Write(world.Terrain is not null);
        if (world.Terrain is not null)
            writer.Write(world.Terrain.ContentHash);

        // 3. Regions
        writer.Write(world.Regions.Count);
        for (int i = 0; i < world.Regions.Count; i++)
            writer.Write(world.Regions[i].Id.Value);

        // 4. RNG streams
        writer.Write(world.RngStreams.Count);
        for (int i = 0; i < world.RngStreams.Count; i++)
        {
            RngStreamRow row = world.RngStreams[i];
            writer.Write(row.System.Value);
            writer.Write(row.Region.Value);
            writer.Write(row.State);
            writer.Write(row.Inc);
        }

        // 5. Rainfall
        writer.Write(world.Rainfall.Count);
        for (int i = 0; i < world.Rainfall.Count; i++)
        {
            RainfallRow row = world.Rainfall[i];
            writer.Write(row.Region.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.RainfallMmPerYear));
        }

        // 6. Biomass
        writer.Write(world.Biomass.Count);
        for (int i = 0; i < world.Biomass.Count; i++)
        {
            BiomassRow row = world.Biomass[i];
            writer.Write(row.Region.Value);
            writer.Write(row.Biomass.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.GrowthRemainder));
        }

        // 7. Goods
        writer.Write(world.Goods.Count);
        for (int i = 0; i < world.Goods.Count; i++)
        {
            GoodsRow row = world.Goods[i];
            writer.Write(row.Region.Value);
            writer.Write(row.Amount.Value);
        }

        // 8. Ledger flows
        writer.Write(world.LedgerFlows.Count);
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            writer.Write(row.Quantity.Value);
            writer.Write(row.Reason.Value);
            writer.Write(row.TotalSourced);
            writer.Write(row.TotalSunk);
        }

        // 9. Network nodes (v3)
        writer.Write(world.NetworkNodes.Count);
        for (int i = 0; i < world.NetworkNodes.Count; i++)
        {
            NetworkNodeRow row = world.NetworkNodes[i];
            writer.Write(row.Id.Value);
            writer.Write(row.LatticeNode);
        }

        // 10. Network edges (v3)
        writer.Write(world.NetworkEdges.Count);
        for (int i = 0; i < world.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow row = world.NetworkEdges[i];
            writer.Write(row.Id.Value);
            writer.Write(row.A.Value);
            writer.Write(row.B.Value);
            writer.Write(row.EdgeType);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Cost));
        }

        // 11. Settlements (v4)
        writer.Write(world.Settlements.Count);
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementRow row = world.Settlements[i];
            writer.Write(row.Id.Value);
            writer.Write(row.SiteCell);
            writer.Write(row.FoundedTurn);
        }

        // 12. Network meta (v4)
        writer.Write(world.NetworkMeta.Count);
        for (int i = 0; i < world.NetworkMeta.Count; i++)
            writer.Write(world.NetworkMeta[i].Revision);

        // 13. Catchment nodes (v4)
        writer.Write(world.CatchmentNodes.Count);
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.LatticeNode);
            writer.Write(BitConverter.DoubleToInt64Bits(row.TravelCost));
        }

        // 14. Catchment summaries (v4)
        writer.Write(world.CatchmentSummaries.Count);
        for (int i = 0; i < world.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow row = world.CatchmentSummaries[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.NodeCount);
            writer.Write(BitConverter.DoubleToInt64Bits(row.EffectiveArableKm2));
            writer.Write(row.NetworkRevision);
            writer.Write(row.LastRecomputeTurn);
            writer.Write(row.SizeTier);
        }

        // 15. Population buckets (v7; v5 shipped the retired PopBands here)
        writer.Write(world.Buckets.Count);
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            BucketRow row = world.Buckets[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Culture.Value);
            writer.Write(row.Religion.Value);
            writer.Write(row.Class.Value);
            writer.Write(row.CohortIdx);
            writer.Write(row.Count.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.BirthRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.DeathRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.StarvationRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.AgingRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.MobilityRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.MigrationRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.ReboundReservoir));
            writer.Write(BitConverter.DoubleToInt64Bits(row.UnplacedDeparture));
            writer.Write(BitConverter.DoubleToInt64Bits(row.UnplacedRemainder));
        }

        // 16. Good stocks + deposits (v13 — the FoodStore migrated into grain).
        writer.Write(world.GoodStocks.Count);
        for (int i = 0; i < world.GoodStocks.Count; i++)
        {
            GoodStockRow row = world.GoodStocks[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Good.Value);
            writer.Write(row.Amount.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.ProduceRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.ConsumeRemainder));
            writer.Write(row.LastProducedUnits);
            writer.Write(row.LastInputDemandUnits);
            writer.Write(row.LastConsumptionDemandUnits);
            writer.Write(row.LastConsumptionEatenUnits);
        }
        writer.Write(world.Deposits.Count);
        for (int i = 0; i < world.Deposits.Count; i++)
        {
            DepositRow row = world.Deposits[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Good.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Abundance));
        }

        // 17. Consumption deficits (v5; +DemandUnits v8)
        writer.Write(world.ConsumptionDeficits.Count);
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
        {
            ConsumptionDeficitRow row = world.ConsumptionDeficits[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.DeficitRatio));
            writer.Write(row.DemandUnits);
        }

        // 18. Sector allocations (v6; widened to five sectors at v14, T3.3/D-032)
        writer.Write(world.SectorAllocations.Count);
        for (int i = 0; i < world.SectorAllocations.Count; i++)
        {
            SectorAllocationRow row = world.SectorAllocations[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Farming));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Herding));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Extraction));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Crafting));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Construction));
        }

        // 19. Path progress (v6)
        writer.Write(world.PathProgress.Count);
        for (int i = 0; i < world.PathProgress.Count; i++)
        {
            PathProgressRow row = world.PathProgress[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Banked));
            writer.Write(row.FrontierNode);
        }

        // 20. Variables (v8)
        writer.Write(world.Variables.Count);
        for (int i = 0; i < world.Variables.Count; i++)
        {
            VariableRow row = world.Variables[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.VarId);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Value));
        }

        // 21. Class states (v8)
        writer.Write(world.ClassStates.Count);
        for (int i = 0; i < world.ClassStates.Count; i++)
        {
            ClassStateRow row = world.ClassStates[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Class.Value);
            writer.Write(row.Active);
        }

        // 22. Settlement distances (v9)
        writer.Write(world.SettlementDistances.Count);
        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = world.SettlementDistances[i];
            writer.Write(row.From.Value);
            writer.Write(row.To.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.TravelCost));
        }

        // 23. Migration flows (v9)
        writer.Write(world.MigrationFlows.Count);
        for (int i = 0; i < world.MigrationFlows.Count; i++)
        {
            MigrationFlowRow row = world.MigrationFlows[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Inflow);
            writer.Write(row.Outflow);
        }

        // 24. Settlement vitals (v11)
        writer.Write(world.SettlementVitals.Count);
        for (int i = 0; i < world.SettlementVitals.Count; i++)
        {
            SettlementVitalsRow row = world.SettlementVitals[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Births);
            writer.Write(row.Deaths);
            writer.Write(BitConverter.DoubleToInt64Bits(row.DtYears));
        }

        // 25. Need satisfactions (v11)
        writer.Write(world.NeedSatisfactions.Count);
        for (int i = 0; i < world.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow row = world.NeedSatisfactions[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Class.Value);
            writer.Write(row.NeedId);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Value));
        }

        // 26. Grievances (v11)
        writer.Write(world.Grievances.Count);
        for (int i = 0; i < world.Grievances.Count; i++)
        {
            GrievanceRow row = world.Grievances[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Class.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Value));
        }

        // 27. Smoothed attractiveness (v12)
        writer.Write(world.SmoothedAttractiveness.Count);
        for (int i = 0; i < world.SmoothedAttractiveness.Count; i++)
        {
            SmoothedAttractivenessRow row = world.SmoothedAttractiveness[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Value));
        }

        // 28. Prices + price-term decomposition (v15, T3.4)
        writer.Write(world.Prices.Count);
        for (int i = 0; i < world.Prices.Count; i++)
        {
            PriceRow row = world.Prices[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Good.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Price));
        }
        writer.Write(world.PriceTerms.Count);
        for (int i = 0; i < world.PriceTerms.Count; i++)
        {
            PriceTermRow row = world.PriceTerms[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Good.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.PrevPrice));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Consumption));
            writer.Write(BitConverter.DoubleToInt64Bits(row.InputDemand));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Production));
            writer.Write(BitConverter.DoubleToInt64Bits(row.StockRelease));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Clamp));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Delta));
        }

        // 29. Harvest weather (v16, T3.4b)
        writer.Write(world.HarvestWeather.Count);
        for (int i = 0; i < world.HarvestWeather.Count; i++)
        {
            HarvestWeatherRow row = world.HarvestWeather[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.LogDeviation));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Multiplier));
        }

        // 30. Trade flows (v18, T3.6)
        writer.Write(world.TradeFlows.Count);
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow row = world.TradeFlows[i];
            writer.Write(row.From.Value);
            writer.Write(row.To.Value);
            writer.Write(row.Good.Value);
            writer.Write(row.Quantity);
        }

        // 31. Housing (v19, T3.8)
        writer.Write(world.Housing.Count);
        for (int i = 0; i < world.Housing.Count; i++)
        {
            HousingRow row = world.Housing[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Dwellings.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.BuildRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.DecayRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.LastMaintenanceFraction));
            writer.Write(BitConverter.DoubleToInt64Bits(row.LastLaborUsed));
        }

        // 32. Claims (v20, T4.3)
        writer.Write(world.Claims.Count);
        for (int i = 0; i < world.Claims.Count; i++)
        {
            ClaimRow row = world.Claims[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Place.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Strength));
        }

        // 33. Controls (v20, T4.3)
        writer.Write(world.Controls.Count);
        for (int i = 0; i < world.Controls.Count; i++)
        {
            ControlRow row = world.Controls[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Place.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Strength));
        }

        // 34. Recognitions (v20, T4.3)
        writer.Write(world.Recognitions.Count);
        for (int i = 0; i < world.Recognitions.Count; i++)
        {
            RecognitionRow row = world.Recognitions[i];
            writer.Write(row.Recogniser.Value);
            writer.Write(row.Recognised.Value);
        }

        // 35. Notables (v21, T4.8, R-1: a notable is a PERSON)
        writer.Write(world.Notables.Count);
        for (int i = 0; i < world.Notables.Count; i++)
        {
            NotableRow row = world.Notables[i];
            writer.Write(row.Id.Value);
            writer.Write(row.Settlement.Value);
            writer.Write(row.Allegiance.Value);
            writer.Write(row.CohortIdx);
            writer.Write(row.Count.Value);
        }

        // 36. Polities (v23, M4, D-042: the Empire roster and its command source)
        writer.Write(world.Polities.Count);
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityRow row = world.Polities[i];
            writer.Write(row.Id.Value);
            writer.Write((int)row.Source);
        }

        // 37. Capitals (v23, M4, D-042: capital as a RELATION, absence = none)
        writer.Write(world.Capitals.Count);
        for (int i = 0; i < world.Capitals.Count; i++)
        {
            CapitalRow row = world.Capitals[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Place.Value);
        }

        // 38. ConstructionQueue (v24, M4-D: settlement-owned, ordered by Slot)
        writer.Write(world.ConstructionQueue.Count);
        for (int i = 0; i < world.ConstructionQueue.Count; i++)
        {
            ConstructionQueueRow row = world.ConstructionQueue[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Slot);
            writer.Write(row.ProjectId);
        }

        // 39. Structures (v24, M4-D: completed structures per settlement)
        writer.Write(world.Structures.Count);
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.ProjectId);
            writer.Write(row.Count);
        }

        // 40. Disasters (v25, T4.21-1: famine-class production shocks)
        writer.Write(world.Disasters.Count);
        for (int i = 0; i < world.Disasters.Count; i++)
        {
            DisasterRow row = world.Disasters[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Kind);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Severity));
            writer.Write(BitConverter.DoubleToInt64Bits(row.RemainingYears));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Multiplier));
            writer.Write(BitConverter.DoubleToInt64Bits(row.AppliedMultiplier));
        }

        // 41. ResearchTargets (v26, ADR-029: one active target per polity)
        writer.Write(world.ResearchTargets.Count);
        for (int i = 0; i < world.ResearchTargets.Count; i++)
        {
            ResearchTargetRow row = world.ResearchTargets[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
        }

        // 42. ResearchProgress (v26: per-node partial progress, RP)
        writer.Write(world.ResearchProgress.Count);
        for (int i = 0; i < world.ResearchProgress.Count; i++)
        {
            ResearchProgressRow row = world.ResearchProgress[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Progress));
        }

        // 43. ResearchCompleted (v26: the completed-knowledge relation)
        writer.Write(world.ResearchCompleted.Count);
        for (int i = 0; i < world.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow row = world.ResearchCompleted[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
        }

        // 44. ResearchEurekas (v26: the fired-Eureka relation)
        writer.Write(world.ResearchEurekas.Count);
        for (int i = 0; i < world.ResearchEurekas.Count; i++)
        {
            ResearchEurekaRow row = world.ResearchEurekas[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
            writer.Write(row.Eureka);
        }

        // 45. ResearchCostModifiers (v26: specialized-university cost factors — input contract)
        writer.Write(world.ResearchCostModifiers.Count);
        for (int i = 0; i < world.ResearchCostModifiers.Count; i++)
        {
            ResearchCostModifierRow row = world.ResearchCostModifiers[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.UniversityType);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Factor));
        }

        // 46. ResearchCredits (v26, ADR-029 addendum A: acceleration-credit provenance per source)
        writer.Write(world.ResearchCredits.Count);
        for (int i = 0; i < world.ResearchCredits.Count; i++)
        {
            ResearchCreditRow row = world.ResearchCredits[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
            writer.Write(row.Source);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Amount));
        }

        // 47. ResearchExposures (v26, ADR-029 addendum A: the foreign-exposure input seam)
        writer.Write(world.ResearchExposures.Count);
        for (int i = 0; i < world.ResearchExposures.Count; i++)
        {
            ResearchExposureRow row = world.ResearchExposures[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Node.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Offered));
        }

        // 48. AgeStates (v28, ADR-031)
        writer.Write(world.AgeStates.Count);
        for (int i = 0; i < world.AgeStates.Count; i++)
        {
            AgeStateRow row = world.AgeStates[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.Age);
            writer.Write(row.EnteredTurn);
            writer.Write(row.Surge);
            writer.Write(row.SurgeStartTurn);
        }

        // 49. AgeEligibility (v28, ADR-031)
        writer.Write(world.AgeEligibility.Count);
        for (int i = 0; i < world.AgeEligibility.Count; i++)
        {
            AgeEligibilityRow row = world.AgeEligibility[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.NextAge);
            writer.Write(row.EvaluatedTurn);
            writer.Write(row.CoreMet);
            writer.Write(row.CoreTotal);
            writer.Write(row.SupportingMet);
            writer.Write(row.SupportingRequired);
            writer.Write(row.CategoryMask);
            writer.Write(row.CategoriesRequired);
            writer.Write(row.Eligible);
        }

        // 50. AgeTransitions (v28, ADR-031)
        writer.Write(world.AgeTransitions.Count);
        for (int i = 0; i < world.AgeTransitions.Count; i++)
        {
            AgeTransitionRow row = world.AgeTransitions[i];
            writer.Write(row.Polity.Value);
            writer.Write(row.FromAge);
            writer.Write(row.ToAge);
            writer.Write(row.Surge);
            writer.Write(row.DecisionTurn);
            writer.Write(row.EffectiveTurn);
        }

        // 51. MilitaryUnits (v28, ADR-031)
        writer.Write(world.MilitaryUnits.Count);
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow row = world.MilitaryUnits[i];
            writer.Write(row.Id);
            writer.Write(row.Owner.Value);
            writer.Write(row.Family);
            writer.Write(row.Identity);
            writer.Write(row.Location.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.X));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Y));
            writer.Write(BitConverter.DoubleToInt64Bits(row.Experience));
            writer.Write(row.Army);
        }

        // 52. UnitConversions (v28, ADR-031)
        writer.Write(world.UnitConversions.Count);
        for (int i = 0; i < world.UnitConversions.Count; i++)
        {
            UnitConversionRow row = world.UnitConversions[i];
            writer.Write(row.Turn);
            writer.Write(row.Unit);
            writer.Write(row.Owner.Value);
            writer.Write(row.FromFamily);
            writer.Write(row.FromIdentity);
            writer.Write(row.ToFamily);
            writer.Write(row.ToIdentity);
            writer.Write(row.FromAge);
            writer.Write(row.ToAge);
            writer.Write(row.Outcome);
        }

        // 53. TransportEdges (v29, ADR-032)
        writer.Write(world.TransportEdges.Count);
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow row = world.TransportEdges[i];
            writer.Write(row.Id);
            writer.Write(row.A.Value);
            writer.Write(row.B.Value);
            writer.Write(row.EdgeType);
            writer.Write(row.Mode);
            writer.Write(row.State);
            writer.Write(row.CapacityTonnesPerYear);
            writer.Write(BitConverter.DoubleToInt64Bits(row.LengthKm));
            writer.Write(row.Condition);
            writer.Write(row.BuiltTurn);
            writer.Write(row.UpgradedTurn);
            writer.Write(row.TargetClass);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Modernization));
            writer.Write(BitConverter.DoubleToInt64Bits(row.CostFactor));
        }

        // 54. RoadDevelopments (v29, ADR-032)
        writer.Write(world.RoadDevelopments.Count);
        for (int i = 0; i < world.RoadDevelopments.Count; i++)
        {
            RoadDevelopmentRow row = world.RoadDevelopments[i];
            writer.Write(row.Turn);
            writer.Write(row.Polity.Value);
            writer.Write(row.Edge);
            writer.Write(row.A.Value);
            writer.Write(row.B.Value);
            writer.Write(row.FromClass);
            writer.Write(row.ToClass);
            writer.Write(row.Kind);
            writer.Write(row.Usage);
            writer.Write(row.MaterialUnits);
            writer.Write(BitConverter.DoubleToInt64Bits(row.ProgressBefore));
            writer.Write(BitConverter.DoubleToInt64Bits(row.ProgressAfter));
        }

        // 55. TaxPolicies (v30, ADR-033 D4: the standing policy per Empire; absence = zero)
        writer.Write(world.TaxPolicies.Count);
        for (int i = 0; i < world.TaxPolicies.Count; i++)
        {
            TaxPolicyRow row = world.TaxPolicies[i];
            writer.Write(row.Polity.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Rate));
        }

        // 56. Institutions (v31, ADR-033 D6: founded institution instances with their stored maturity)
        writer.Write(world.Institutions.Count);
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            writer.Write(row.Id);
            writer.Write(row.Polity.Value);
            writer.Write(row.Settlement.Value);
            writer.Write(row.Type);
            writer.Write(row.FoundedTurn);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Maturity));
        }

        // 57. ConstructionLabor (v31, ADR-033 D10: the labour the queue consumed this step)
        writer.Write(world.ConstructionLabor.Count);
        for (int i = 0; i < world.ConstructionLabor.Count; i++)
        {
            ConstructionLaborRow row = world.ConstructionLabor[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.LastLaborUsed));
        }
    }

    /// <summary>Reads a state stream written by <see cref="Write"/> (same order, field by field).</summary>
    public static WorldState Read(BinaryReader reader) => Read(reader, out _);

    /// <summary>
    /// As <see cref="Read(BinaryReader)"/>; <paramref name="expectedTerrainHash"/>
    /// returns the terrain content hash the state was saved against (null if the
    /// world had no terrain). Terrain itself is not in the stream (ADR-008) — the
    /// caller regenerates it from seed + config and must match this hash
    /// (Snapshot.Load enforces it).
    /// v14 (T3.3, D-032): LaborAllocationRow (Settlement + FarmShare) becomes
    /// SectorAllocationRow (Settlement + five sector weights) — the farm/path
    /// pair generalizes to farming/herding/extraction/crafting/construction.
    /// Row width 12 → 44; the table's position in the stream is unchanged.
    /// </summary>
    public static WorldState Read(BinaryReader reader, out byte[]? expectedTerrainHash)
    {
        ulong seed = reader.ReadUInt64();
        var world = new WorldState(seed)
        {
            Clock = new SimClock(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64()),
        };

        expectedTerrainHash = reader.ReadBoolean() ? reader.ReadBytes(32) : null;

        int regionCount = reader.ReadInt32();
        for (int i = 0; i < regionCount; i++)
            world.Regions.Add(new RegionRow(new RegionId(reader.ReadInt32())));

        int rngCount = reader.ReadInt32();
        for (int i = 0; i < rngCount; i++)
        {
            world.RngStreams.Add(new RngStreamRow(
                new SystemId(reader.ReadInt32()), new RegionId(reader.ReadInt32()),
                reader.ReadUInt64(), reader.ReadUInt64()));
        }

        int rainCount = reader.ReadInt32();
        for (int i = 0; i < rainCount; i++)
        {
            world.Rainfall.Add(new RainfallRow(
                new RegionId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int biomassCount = reader.ReadInt32();
        for (int i = 0; i < biomassCount; i++)
        {
            var region = new RegionId(reader.ReadInt32());
            long stock = reader.ReadInt64();
            double remainder = BitConverter.Int64BitsToDouble(reader.ReadInt64());
            world.Biomass.Add(new BiomassRow(region, Conserved.FromSnapshot(stock), remainder));
        }

        int goodsCount = reader.ReadInt32();
        for (int i = 0; i < goodsCount; i++)
        {
            var region = new RegionId(reader.ReadInt32());
            long stock = reader.ReadInt64();
            world.Goods.Add(new GoodsRow(region, Conserved.FromSnapshot(stock)));
        }

        int flowCount = reader.ReadInt32();
        for (int i = 0; i < flowCount; i++)
        {
            world.LedgerFlows.Add(new LedgerFlowRow(
                new ConservedQuantityId(reader.ReadInt32()), new ReasonId(reader.ReadInt32()),
                reader.ReadInt64(), reader.ReadInt64()));
        }

        int netNodeCount = reader.ReadInt32();
        for (int i = 0; i < netNodeCount; i++)
        {
            world.NetworkNodes.Add(new NetworkNodeRow(
                new NetworkNodeId(reader.ReadInt32()), reader.ReadInt32()));
        }

        int netEdgeCount = reader.ReadInt32();
        for (int i = 0; i < netEdgeCount; i++)
        {
            world.NetworkEdges.Add(new NetworkEdgeRow(
                new NetworkEdgeId(reader.ReadInt32()), new NetworkNodeId(reader.ReadInt32()),
                new NetworkNodeId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int settlementCount = reader.ReadInt32();
        for (int i = 0; i < settlementCount; i++)
        {
            world.Settlements.Add(new SettlementRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt64()));
        }

        int netMetaCount = reader.ReadInt32();
        for (int i = 0; i < netMetaCount; i++)
            world.NetworkMeta.Add(new NetworkMetaRow(reader.ReadInt32()));

        int catchNodeCount = reader.ReadInt32();
        for (int i = 0; i < catchNodeCount; i++)
        {
            world.CatchmentNodes.Add(new CatchmentNodeRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int catchSummaryCount = reader.ReadInt32();
        for (int i = 0; i < catchSummaryCount; i++)
        {
            world.CatchmentSummaries.Add(new CatchmentSummaryRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32()));
        }

        int bucketCount = reader.ReadInt32();
        for (int i = 0; i < bucketCount; i++)
        {
            var settlement = new SettlementId(reader.ReadInt32());
            var culture = new CultureId(reader.ReadInt32());
            var religion = new ReligionId(reader.ReadInt32());
            var cls = new ClassId(reader.ReadInt32());
            int cohort = reader.ReadInt32();
            long count = reader.ReadInt64();
            world.Buckets.Add(new BucketRow(
                settlement, culture, religion, cls, cohort, Conserved.FromSnapshot(count),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int goodStockCount = reader.ReadInt32();
        for (int i = 0; i < goodStockCount; i++)
        {
            var settlement = new SettlementId(reader.ReadInt32());
            var good = new GoodId(reader.ReadInt32());
            long amount = reader.ReadInt64();
            world.GoodStocks.Add(new GoodStockRow(
                settlement, good, Conserved.FromSnapshot(amount),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadInt64()));
        }
        int depositCount = reader.ReadInt32();
        for (int i = 0; i < depositCount; i++)
        {
            world.Deposits.Add(new DepositRow(
                new SettlementId(reader.ReadInt32()),
                new GoodId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int deficitCount = reader.ReadInt32();
        for (int i = 0; i < deficitCount; i++)
        {
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt64()));
        }

        int allocCount = reader.ReadInt32();
        for (int i = 0; i < allocCount; i++)
        {
            world.SectorAllocations.Add(new SectorAllocationRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int progressCount = reader.ReadInt32();
        for (int i = 0; i < progressCount; i++)
        {
            world.PathProgress.Add(new PathProgressRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt32()));
        }

        int variableCount = reader.ReadInt32();
        for (int i = 0; i < variableCount; i++)
        {
            world.Variables.Add(new VariableRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int classStateCount = reader.ReadInt32();
        for (int i = 0; i < classStateCount; i++)
        {
            world.ClassStates.Add(new ClassStateRow(
                new SettlementId(reader.ReadInt32()),
                new ClassId(reader.ReadInt32()), reader.ReadInt32()));
        }

        int distanceCount = reader.ReadInt32();
        for (int i = 0; i < distanceCount; i++)
        {
            world.SettlementDistances.Add(new SettlementDistanceRow(
                new SettlementId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int migFlowCount = reader.ReadInt32();
        for (int i = 0; i < migFlowCount; i++)
        {
            world.MigrationFlows.Add(new MigrationFlowRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt64(), reader.ReadInt64()));
        }

        int vitalsCount = reader.ReadInt32();
        for (int i = 0; i < vitalsCount; i++)
        {
            world.SettlementVitals.Add(new SettlementVitalsRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt64(), reader.ReadInt64(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int satisfactionCount = reader.ReadInt32();
        for (int i = 0; i < satisfactionCount; i++)
        {
            world.NeedSatisfactions.Add(new NeedSatisfactionRow(
                new SettlementId(reader.ReadInt32()), new ClassId(reader.ReadInt32()),
                reader.ReadInt32(), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int grievanceCount = reader.ReadInt32();
        for (int i = 0; i < grievanceCount; i++)
        {
            world.Grievances.Add(new GrievanceRow(
                new SettlementId(reader.ReadInt32()), new ClassId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int smoothedCount = reader.ReadInt32();
        for (int i = 0; i < smoothedCount; i++)
        {
            world.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int priceCount = reader.ReadInt32();
        for (int i = 0; i < priceCount; i++)
        {
            world.Prices.Add(new PriceRow(
                new SettlementId(reader.ReadInt32()), new GoodId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }
        int priceTermCount = reader.ReadInt32();
        for (int i = 0; i < priceTermCount; i++)
        {
            world.PriceTerms.Add(new PriceTermRow(
                new SettlementId(reader.ReadInt32()), new GoodId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int weatherCount = reader.ReadInt32();
        for (int i = 0; i < weatherCount; i++)
        {
            world.HarvestWeather.Add(new HarvestWeatherRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int tradeFlowCount = reader.ReadInt32();
        for (int i = 0; i < tradeFlowCount; i++)
        {
            world.TradeFlows.Add(new TradeFlowRow(
                new SettlementId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                new GoodId(reader.ReadInt32()), reader.ReadInt64()));
        }

        int housingCount = reader.ReadInt32();
        for (int i = 0; i < housingCount; i++)
        {
            var settlement = new SettlementId(reader.ReadInt32());
            long dwellings = reader.ReadInt64();
            world.Housing.Add(new HousingRow(
                settlement, Conserved.FromSnapshot(dwellings),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int claimCount = reader.ReadInt32();
        for (int i = 0; i < claimCount; i++)
        {
            world.Claims.Add(new ClaimRow(
                new PolityId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int controlCount = reader.ReadInt32();
        for (int i = 0; i < controlCount; i++)
        {
            world.Controls.Add(new ControlRow(
                new PolityId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int recognitionCount = reader.ReadInt32();
        for (int i = 0; i < recognitionCount; i++)
        {
            world.Recognitions.Add(new RecognitionRow(
                new PolityId(reader.ReadInt32()), new PolityId(reader.ReadInt32())));
        }

        int notableCount = reader.ReadInt32();
        for (int i = 0; i < notableCount; i++)
        {
            var id = new NotableId(reader.ReadInt32());
            var settlement = new SettlementId(reader.ReadInt32());
            var allegiance = new PolityId(reader.ReadInt32());
            int cohort = reader.ReadInt32();
            world.Notables.Add(new NotableRow(
                id, settlement, allegiance, cohort, Conserved.FromSnapshot(reader.ReadInt64())));
        }

        int polityCount = reader.ReadInt32();
        for (int i = 0; i < polityCount; i++)
        {
            var id = new PolityId(reader.ReadInt32());
            world.Polities.Add(new PolityRow(id, (CommandSource)reader.ReadInt32()));
        }

        int capitalCount = reader.ReadInt32();
        for (int i = 0; i < capitalCount; i++)
        {
            world.Capitals.Add(new CapitalRow(
                new PolityId(reader.ReadInt32()), new SettlementId(reader.ReadInt32())));
        }

        int queueCount = reader.ReadInt32();
        for (int i = 0; i < queueCount; i++)
        {
            world.ConstructionQueue.Add(new ConstructionQueueRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt32()));
        }

        int structureCount = reader.ReadInt32();
        for (int i = 0; i < structureCount; i++)
        {
            world.Structures.Add(new StructureRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt64()));
        }

        int disasterCount = reader.ReadInt32();
        for (int i = 0; i < disasterCount; i++)
        {
            world.Disasters.Add(new DisasterRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int researchTargetCount = reader.ReadInt32();
        for (int i = 0; i < researchTargetCount; i++)
        {
            world.ResearchTargets.Add(new ResearchTargetRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32())));
        }

        int researchProgressCount = reader.ReadInt32();
        for (int i = 0; i < researchProgressCount; i++)
        {
            world.ResearchProgress.Add(new ResearchProgressRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int researchCompletedCount = reader.ReadInt32();
        for (int i = 0; i < researchCompletedCount; i++)
        {
            world.ResearchCompleted.Add(new ResearchCompletedRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32())));
        }

        int researchEurekaCount = reader.ReadInt32();
        for (int i = 0; i < researchEurekaCount; i++)
        {
            world.ResearchEurekas.Add(new ResearchEurekaRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32()), reader.ReadInt32()));
        }

        int researchCostModifierCount = reader.ReadInt32();
        for (int i = 0; i < researchCostModifierCount; i++)
        {
            world.ResearchCostModifiers.Add(new ResearchCostModifierRow(
                new PolityId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int researchCreditCount = reader.ReadInt32();
        for (int i = 0; i < researchCreditCount; i++)
        {
            world.ResearchCredits.Add(new ResearchCreditRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int researchExposureCount = reader.ReadInt32();
        for (int i = 0; i < researchExposureCount; i++)
        {
            world.ResearchExposures.Add(new ResearchExposureRow(
                new PolityId(reader.ReadInt32()), new ResearchNodeId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int ageStateCount = reader.ReadInt32();
        for (int i = 0; i < ageStateCount; i++)
        {
            world.AgeStates.Add(new AgeStateRow(
                new PolityId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt64(),
                reader.ReadInt32(), reader.ReadInt64()));
        }

        int ageEligibilityCount = reader.ReadInt32();
        for (int i = 0; i < ageEligibilityCount; i++)
        {
            world.AgeEligibility.Add(new AgeEligibilityRow(
                new PolityId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt64(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadBoolean()));
        }

        int ageTransitionCount = reader.ReadInt32();
        for (int i = 0; i < ageTransitionCount; i++)
        {
            world.AgeTransitions.Add(new AgeTransitionRow(
                new PolityId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt64(), reader.ReadInt64()));
        }

        int militaryUnitCount = reader.ReadInt32();
        for (int i = 0; i < militaryUnitCount; i++)
        {
            world.MilitaryUnits.Add(new MilitaryUnitRow(
                reader.ReadInt32(), new PolityId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt32(),
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()), BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()), reader.ReadInt32()));
        }

        int unitConversionCount = reader.ReadInt32();
        for (int i = 0; i < unitConversionCount; i++)
        {
            world.UnitConversions.Add(new UnitConversionRow(
                reader.ReadInt64(), reader.ReadInt32(), new PolityId(reader.ReadInt32()),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        }

        int transportEdgeCount = reader.ReadInt32();
        for (int i = 0; i < transportEdgeCount; i++)
        {
            world.TransportEdges.Add(new TransportEdgeRow(
                reader.ReadInt32(), new SettlementId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()), reader.ReadInt32(),
                reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int roadDevelopmentCount = reader.ReadInt32();
        for (int i = 0; i < roadDevelopmentCount; i++)
        {
            world.RoadDevelopments.Add(new RoadDevelopmentRow(
                reader.ReadInt64(), new PolityId(reader.ReadInt32()), reader.ReadInt32(),
                new SettlementId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int taxPolicyCount = reader.ReadInt32();
        for (int i = 0; i < taxPolicyCount; i++)
        {
            world.TaxPolicies.Add(new TaxPolicyRow(
                new PolityId(reader.ReadInt32()), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int institutionCount = reader.ReadInt32();
        for (int i = 0; i < institutionCount; i++)
        {
            world.Institutions.Add(new InstitutionRow(
                reader.ReadInt32(), new PolityId(reader.ReadInt32()), new SettlementId(reader.ReadInt32()),
                reader.ReadInt32(), reader.ReadInt64(), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int constructionLaborCount = reader.ReadInt32();
        for (int i = 0; i < constructionLaborCount; i++)
        {
            world.ConstructionLabor.Add(new ConstructionLaborRow(
                new SettlementId(reader.ReadInt32()), BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        return world;
    }

    /// <summary>
    /// Exact stream length from schema widths × row counts — the structural
    /// anti-padding proof: any raw-memory shortcut in Write breaks equality with
    /// this sum (struct layouts pad; the schema does not).
    /// </summary>
    public static long ExpectedLength(WorldState world) =>
        SeedWidth + ClockWidth
        + 1 + (world.Terrain is not null ? 32 : 0)   // terrain flag + content hash
        + CountPrefixWidth + (long)world.Regions.Count * RegionRowWidth
        + CountPrefixWidth + (long)world.RngStreams.Count * RngStreamRowWidth
        + CountPrefixWidth + (long)world.Rainfall.Count * RainfallRowWidth
        + CountPrefixWidth + (long)world.Biomass.Count * BiomassRowWidth
        + CountPrefixWidth + (long)world.Goods.Count * GoodsRowWidth
        + CountPrefixWidth + (long)world.LedgerFlows.Count * LedgerFlowRowWidth
        + CountPrefixWidth + (long)world.NetworkNodes.Count * NetworkNodeRowWidth
        + CountPrefixWidth + (long)world.NetworkEdges.Count * NetworkEdgeRowWidth
        + CountPrefixWidth + (long)world.Settlements.Count * SettlementRowWidth
        + CountPrefixWidth + (long)world.NetworkMeta.Count * NetworkMetaRowWidth
        + CountPrefixWidth + (long)world.CatchmentNodes.Count * CatchmentNodeRowWidth
        + CountPrefixWidth + (long)world.CatchmentSummaries.Count * CatchmentSummaryRowWidth
        + CountPrefixWidth + (long)world.Buckets.Count * BucketRowWidth
        + CountPrefixWidth + (long)world.GoodStocks.Count * GoodStockRowWidth
        + CountPrefixWidth + (long)world.Deposits.Count * DepositRowWidth
        + CountPrefixWidth + (long)world.ConsumptionDeficits.Count * ConsumptionDeficitRowWidth
        + CountPrefixWidth + (long)world.SectorAllocations.Count * SectorAllocationRowWidth
        + CountPrefixWidth + (long)world.PathProgress.Count * PathProgressRowWidth
        + CountPrefixWidth + (long)world.Variables.Count * VariableRowWidth
        + CountPrefixWidth + (long)world.ClassStates.Count * ClassStateRowWidth
        + CountPrefixWidth + (long)world.SettlementDistances.Count * SettlementDistanceRowWidth
        + CountPrefixWidth + (long)world.MigrationFlows.Count * MigrationFlowRowWidth
        + CountPrefixWidth + (long)world.SettlementVitals.Count * SettlementVitalsRowWidth
        + CountPrefixWidth + (long)world.NeedSatisfactions.Count * NeedSatisfactionRowWidth
        + CountPrefixWidth + (long)world.Grievances.Count * GrievanceRowWidth
        + CountPrefixWidth + (long)world.SmoothedAttractiveness.Count * SmoothedAttractivenessRowWidth
        + CountPrefixWidth + (long)world.Prices.Count * PriceRowWidth
        + CountPrefixWidth + (long)world.PriceTerms.Count * PriceTermRowWidth
        + CountPrefixWidth + (long)world.HarvestWeather.Count * HarvestWeatherRowWidth
        + CountPrefixWidth + (long)world.TradeFlows.Count * TradeFlowRowWidth
        + CountPrefixWidth + (long)world.Housing.Count * HousingRowWidth
        + CountPrefixWidth + (long)world.Claims.Count * ClaimRowWidth
        + CountPrefixWidth + (long)world.Controls.Count * ControlRowWidth
        + CountPrefixWidth + (long)world.Recognitions.Count * RecognitionRowWidth
        + CountPrefixWidth + (long)world.Notables.Count * NotableRowWidth
        + CountPrefixWidth + (long)world.Polities.Count * PolityRowWidth
        + CountPrefixWidth + (long)world.Capitals.Count * CapitalRowWidth
        + CountPrefixWidth + (long)world.ConstructionQueue.Count * ConstructionQueueRowWidth
        + CountPrefixWidth + (long)world.Structures.Count * StructureRowWidth
        + CountPrefixWidth + (long)world.Disasters.Count * DisasterRowWidth
        + CountPrefixWidth + (long)world.ResearchTargets.Count * ResearchTargetRowWidth
        + CountPrefixWidth + (long)world.ResearchProgress.Count * ResearchProgressRowWidth
        + CountPrefixWidth + (long)world.ResearchCompleted.Count * ResearchCompletedRowWidth
        + CountPrefixWidth + (long)world.ResearchEurekas.Count * ResearchEurekaRowWidth
        + CountPrefixWidth + (long)world.ResearchCostModifiers.Count * ResearchCostModifierRowWidth
        + CountPrefixWidth + (long)world.ResearchCredits.Count * ResearchCreditRowWidth
        + CountPrefixWidth + (long)world.ResearchExposures.Count * ResearchExposureRowWidth
        + CountPrefixWidth + (long)world.AgeStates.Count * AgeStateRowWidth
        + CountPrefixWidth + (long)world.AgeEligibility.Count * AgeEligibilityRowWidth
        + CountPrefixWidth + (long)world.AgeTransitions.Count * AgeTransitionRowWidth
        + CountPrefixWidth + (long)world.MilitaryUnits.Count * MilitaryUnitRowWidth
        + CountPrefixWidth + (long)world.UnitConversions.Count * UnitConversionRowWidth
        + CountPrefixWidth + (long)world.TransportEdges.Count * TransportEdgeRowWidth
        + CountPrefixWidth + (long)world.RoadDevelopments.Count * RoadDevelopmentRowWidth
        + CountPrefixWidth + (long)world.TaxPolicies.Count * TaxPolicyRowWidth
        + CountPrefixWidth + (long)world.Institutions.Count * InstitutionRowWidth
        + CountPrefixWidth + (long)world.ConstructionLabor.Count * ConstructionLaborRowWidth;
}
