using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Disaster;
using Sim.Core.Systems.Harvest;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Housing;
using Sim.Core.Systems.Price;
using Sim.Core.Systems.Production;
using Sim.Core.Systems.Growth;
using Sim.Core.Systems.Migration;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Systems.Research;
using Sim.Core.Systems.Trade;
using Sim.Core.Systems.Weather;

namespace Sim.Core;

/// <summary>
/// The composition root for systems — THE single place where owned tables are
/// handed out (§3.1 ownership by construction, ADR-003). Each registration builds
/// that system's typed context with writable handles to its own Next tables and
/// nothing else; systems never see a writable WorldState. Any new system's
/// ownership claim lands here, reviewable at a glance.
/// The executor and pipeline loader consume these registrations generically.
///
/// SANCTIONED SHARED STOCK (T1.5; T3.2 the FoodStore migrated into the GRAIN
/// row of GoodStocks; RE-RECORDED at T3.3 when Farming became Production).
/// GoodStocks is handed to BOTH Production and Consumption. A stock that one
/// system fills and another drains cannot have a single writer; both mutations
/// go exclusively through the Ledger (law 1) and the per-turn audit holds the
/// pair to exactness. This paragraph is the reviewable record of that share, so
/// it states the split at FIELD level:
///
///   PRODUCTION owns, on every row it touches: Amount via Ledger (reasons
///     Harvest for grain, Produced for everything else, InputsConsumed for
///     recipe inputs, ToolWear for farm-tool depreciation), ProduceRemainder on
///     every produced row, LastProducedUnits on every row (zeroed each step),
///     and ConsumeRemainder on the TOOLS row and on every RECIPE-INPUT row.
///   CONSUMPTION owns: Amount via Ledger (reason Eaten) and ConsumeRemainder on
///     the GRAIN row.
///   HOUSING (T3.8, the FOURTH holder) owns: Amount via Ledger SINK only, reason
///     HousingMaterials, on the TIMBER and CLAY rows (build + upkeep draws) —
///     never a source, never another good, no remainder field touched.
///   ROAD DEVELOPMENT (ADR-032, the SIXTH holder) owns: Amount via Ledger SINK
///     only, reason ConstructionMaterials, on the rows its roads.classes materials
///     name, at the ISSUING civilization's controlled settlements in ascending id
///     (never an endpoint as such) — never a source, no remainder field.
///     It runs only on a turn carrying a DevelopRoads order.
///   TRADE (T3.6, the third holder) owns: Amount via Ledger.TRANSFER ONLY —
///     conserving cross-settlement moves within a good, never a source or
///     sink, and NO remainder field (whole units only; sub-unit intent is
///     dropped, not banked — a banked trade remainder would be new serialized
///     state the mandate does not ask for). Trade touches no other field.
///
/// THE COLLISION THIS RECORD EXISTS TO CATCH (T3.3 adversarial finding — the
/// paragraph had gone stale and still named Farming, mis-assigning
/// ConsumeRemainder wholly to Consumption): no shipped recipe consumes grain
/// today, so the two ConsumeRemainder owners never meet. THE FIRST RECIPE THAT
/// TAKES GRAIN AS AN INPUT — a T3.5 food basket, a brewing recipe, or a
/// data-only goods.json edit — puts two systems on one accumulator with two
/// different meanings, and each turn's carry would clobber the other's. Whoever
/// adds that recipe must split the field or serialize the two writers.
///
/// SANCTIONED SHARED TABLE — Controls (M4 T4.4/T4.13; ADR-033 D4 adds the third
/// holder), split at FIELD level and by pipeline order (colonization → revolt →
/// governance):
///   COLONIZATION APPENDS rows — a colony inherits its parent's controller, at
///     the founding Strength 1.0 — and touches no existing row.
///   REVOLT REMOVES rows (a settlement at zero happiness stops obeying),
///     preserving the relative order of every surviving row, then (R3) APPENDS one
///     row per revolted settlement for the new AI polity it founds (Strength 1.0).
///   GOVERNANCE (ADR-033 D4) rewrites ONLY the Strength field of the rows that
///     exist when it runs, as the administrative reach computed on PREV — never
///     adds or removes a row, never touches Polity or Place.
///
/// SANCTIONED SHARED TABLE — AgeStates (H2, Director 2026-10-05 §8: a revolt-born
/// polity inherits its parent's current Age), split by row KEY and pipeline order
/// (revolt → agetransition), exactly as ResearchCompleted is:
///   REVOLT APPENDS one row only for a polity it CREATES this step (a key absent
///     from PREV), copied from the parent's PREV row; it never touches another row.
///   AGETRANSITION upserts rows only for polities on PREV's roster, from their
///     AdvanceAge orders — so it never reads or writes the row revolt appended.
/// </summary>
public static class SystemCatalog
{
    public static SystemRegistration Catchment(SimConfig cfg)
    {
        var system = new CatchmentSystem(cfg);
        return new SystemRegistration(CatchmentSystem.WellKnownId, CatchmentSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<CatchmentTables>(
                prev, new CatchmentTables(next.CatchmentNodes, next.CatchmentSummaries,
                    next.SettlementDistances), rng,
                CatchmentSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Weather()
    {
        var system = new WeatherSystem();
        return new SystemRegistration(WeatherSystem.WellKnownId, WeatherSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<WeatherTables>(
                prev, new WeatherTables(next.Rainfall), rng, WeatherSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Growth()
    {
        var system = new GrowthSystem();
        return new SystemRegistration(GrowthSystem.WellKnownId, GrowthSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<GrowthTables>(
                prev, new GrowthTables(next.Biomass), rng, GrowthSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Trade()
    {
        var system = new TradeSystem();
        return new SystemRegistration(TradeSystem.WellKnownId, TradeSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<TradeTables>(
                prev, new TradeTables(next.Goods), rng, TradeSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Production(SimConfig cfg)
    {
        var system = new ProductionSystem(cfg);
        return new SystemRegistration(ProductionSystem.WellKnownId, ProductionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ProductionTables>(
                prev, new ProductionTables(next.GoodStocks), rng, ProductionSystem.WellKnownId,
                dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T4.5 (D-037 B3): stateless settlements appropriate grain when their
    /// own subsistence fails. Owns no tables; moves grain between existing stock
    /// rows via Ledger.Transfer, which conserves by construction.</summary>
    public static SystemRegistration Appropriation(SimConfig cfg)
    {
        var system = new Systems.Appropriation.AppropriationSystem(cfg);
        return new SystemRegistration(
            Systems.Appropriation.AppropriationSystem.WellKnownId,
            Systems.Appropriation.AppropriationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(
                new SimContext<Systems.Appropriation.AppropriationTables>(
                    prev, new Systems.Appropriation.AppropriationTables(next.GoodStocks), rng,
                    Systems.Appropriation.AppropriationSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Consumption(SimConfig cfg)
    {
        var system = new ConsumptionSystem(cfg);
        return new SystemRegistration(ConsumptionSystem.WellKnownId, ConsumptionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ConsumptionTables>(
                prev, new ConsumptionTables(next.GoodStocks, next.ConsumptionDeficits), rng,
                ConsumptionSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration HarvestWeather(SimConfig cfg)
    {
        var system = new HarvestWeatherSystem(cfg);
        return new SystemRegistration(HarvestWeatherSystem.WellKnownId, HarvestWeatherSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<HarvestWeatherTables>(
                prev, new HarvestWeatherTables(next.HarvestWeather), rng,
                HarvestWeatherSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T4.21-1 (CR-015 §3.3): the famine-class production shock — the
    /// ONLY place its owned Disasters table is handed out (ADR-003).</summary>
    public static SystemRegistration Disaster(SimConfig cfg)
    {
        var system = new DisasterSystem(cfg);
        return new SystemRegistration(DisasterSystem.WellKnownId, DisasterSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<DisasterTables>(
                prev, new DisasterTables(next.Disasters), rng,
                DisasterSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Price(SimConfig cfg)
    {
        var system = new PriceSystem(cfg);
        return new SystemRegistration(PriceSystem.WellKnownId, PriceSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<PriceTables>(
                prev, new PriceTables(next.Prices, next.PriceTerms), rng,
                PriceSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T3.6 (D-034): the arbitrage system — name "trade"; the retired
    /// M0 toy is "toytrade" (director decision 3, 2026-07-28; the roster guard
    /// in PipelineLoader refuses any duplicate). Third holder of the GoodStocks
    /// share — see the ownership record above.</summary>
    public static SystemRegistration TradeArbitrage(SimConfig cfg)
    {
        var system = new TradeArbitrageSystem(cfg);
        return new SystemRegistration(TradeArbitrageSystem.WellKnownId, TradeArbitrageSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<TradeArbitrageTables>(
                prev, new TradeArbitrageTables(next.GoodStocks, next.TradeFlows), rng,
                TradeArbitrageSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>T3.8: the housing stock — fourth holder of the GoodStocks share
    /// (see the ownership record above).</summary>
    public static SystemRegistration Housing(SimConfig cfg)
    {
        var system = new HousingSystem(cfg);
        return new SystemRegistration(HousingSystem.WellKnownId, HousingSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<HousingTables>(
                prev, new HousingTables(next.Housing, next.GoodStocks), rng,
                HousingSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>M4-D: the settlement construction queue — FIFTH holder of the
    /// GoodStocks share (see the ownership record above). It draws materials the
    /// same way housing does, under its own reason.</summary>
    public static SystemRegistration Construction(SimConfig cfg)
    {
        var system = new ConstructionSystem(cfg);
        return new SystemRegistration(ConstructionSystem.WellKnownId, ConstructionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ConstructionTables>(
                prev, new ConstructionTables(next.ConstructionQueue, next.Structures, next.GoodStocks, next.ConstructionLabor),
                rng, ConstructionSystem.WellKnownId, dtDays, dtYears, orders,
                new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration Demographics(SimConfig cfg)
    {
        var system = new DemographicsSystem(cfg);
        return new SystemRegistration(DemographicsSystem.WellKnownId, DemographicsSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<DemographicsTables>(
                prev, new DemographicsTables(next.Buckets, next.SettlementVitals), rng,
                DemographicsSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// T2.6 table communication (law 6, reviewable record): NeedsGrievance
    /// reads the PREV SettlementVitals chronicle that Demographics writes (the
    /// D-021 generational-turnover input) — a single-writer table read across
    /// a turn boundary, not a shared stock; no sanction needed. Its OWN tables
    /// (NeedSatisfactions, Grievances) are read by nothing but UI/chronicle —
    /// the CI read-isolation grep enforces that with an allowlist.
    /// </summary>
    public static SystemRegistration NeedsGrievance(SimConfig cfg)
    {
        var system = new NeedsGrievanceSystem(cfg);
        return new SystemRegistration(NeedsGrievanceSystem.WellKnownId, NeedsGrievanceSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<NeedsGrievanceTables>(
                prev, new NeedsGrievanceTables(next.NeedSatisfactions, next.Grievances, next.TaxGrievances), rng,
                NeedsGrievanceSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// SANCTIONED SHARED STOCK (T2.2): Buckets is handed to BOTH Demographics
    /// (births/deaths/starvation/aging; owns Birth/Death/Starvation/Aging
    /// remainders) and ClassMobility (same-cohort adult class transfers; owns
    /// MobilityRemainder). Every mutation goes exclusively through the Ledger
    /// (law 1) and the per-turn audit holds the pair to exactness — the same
    /// reviewable pattern as the T1.5 FoodStores share above.
    /// </summary>
    public static SystemRegistration ClassMobility(SimConfig cfg)
    {
        var system = new ClassMobilitySystem(cfg);
        return new SystemRegistration(ClassMobilitySystem.WellKnownId, ClassMobilitySystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ClassMobilityTables>(
                prev, new ClassMobilityTables(next.Buckets, next.Variables, next.ClassStates), rng,
                ClassMobilitySystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// SANCTIONED SHARED STOCK (T2.5): Buckets is now handed to THREE systems —
    /// Demographics, ClassMobility (see above), and Migration (cross-settlement
    /// same-key Ledger.Transfers; owns MigrationRemainder). Same discipline:
    /// every mutation through the Ledger (law 1), per-turn audit exact.
    /// </summary>
    public static SystemRegistration Migration(SimConfig cfg)
    {
        var system = new MigrationSystem(cfg);
        return new SystemRegistration(MigrationSystem.WellKnownId, MigrationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<MigrationTables>(
                prev, new MigrationTables(next.Buckets, next.MigrationFlows,
                    next.SmoothedAttractiveness), rng,
                MigrationSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// T4.4 colonization. Takes the WORLDGEN config as well, because frontier
    /// siting is a terrain question and `SitingConfig` (the score floor, the
    /// jitter and ADR-018's `minSpacingKm`) lives there. It is OPTIONAL: toy and
    /// hand-built worlds have no terrain and no worldgen config, and the system
    /// no-ops for them, so `pipeline.json` stays valid everywhere.
    ///
    /// `next.Settlements` is a NEW ownership grant — before T4.4 no system could
    /// append a settlement and the table was immutable for the whole simulation.
    /// </summary>
    public static SystemRegistration Colonization(SimConfig cfg, Worldgen.WorldgenConfig? worldgen)
    {
        var system = worldgen is null ? null
            : new Systems.Colonization.ColonizationSystem(cfg, worldgen);
        return new SystemRegistration(
            Systems.Colonization.ColonizationSystem.WellKnownId,
            Systems.Colonization.ColonizationSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system?.Step(
                new SimContext<Systems.Colonization.ColonizationTables>(
                    prev, new Systems.Colonization.ColonizationTables(
                        next.Settlements, next.Buckets, next.GoodStocks, next.Deposits,
                        next.ClassStates, next.Grievances, next.SmoothedAttractiveness,
                        next.Controls), rng,
                    Systems.Colonization.ColonizationSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>M4: revolt — a settlement at zero happiness loses its control
    /// relation. Shares the `Controls` table with Colonization (which appends on
    /// inheritance) and Governance (which rewrites Strength); the header above
    /// records it as a sanctioned shared table.</summary>
    public static SystemRegistration Revolt(SimConfig cfg)
    {
        var system = new Systems.Revolt.RevoltSystem(cfg);
        return new SystemRegistration(
            Systems.Revolt.RevoltSystem.WellKnownId, Systems.Revolt.RevoltSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(
                new SimContext<Systems.Revolt.RevoltTables>(
                    prev, new Systems.Revolt.RevoltTables(next.Controls, next.Polities, next.ResearchCompleted, next.AgeStates), rng,
                    Systems.Revolt.RevoltSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-033 D4 (the M5 governing loop, SystemId 22): enacts SetTaxRate orders that
    /// pass the tax gate (Governance.CanLevyTax on PREV: the Taxation knowledge AND the minimum
    /// Age, H2) into the TaxPolicies table it owns, and rewrites
    /// ControlRow.Strength as the administrative reach — the THIRD holder of the shared
    /// `Controls` table, field-level split recorded in the header above (it runs after
    /// Colonization and Revolt and touches only Strength). Inert without a governance
    /// section in the config.</summary>
    public static SystemRegistration Governance(SimConfig cfg)
    {
        var system = new Systems.Governance.GovernanceSystem(cfg);
        return new SystemRegistration(
            Systems.Governance.GovernanceSystem.WellKnownId, Systems.Governance.GovernanceSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(
                new SimContext<Systems.Governance.GovernanceTables>(
                    prev, new Systems.Governance.GovernanceTables(next.TaxPolicies, next.Controls), rng,
                    Systems.Governance.GovernanceSystem.WellKnownId,
                    dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    public static SystemRegistration PathBuild(SimConfig cfg)
    {
        var system = new PathBuildSystem(cfg);
        return new SystemRegistration(PathBuildSystem.WellKnownId, PathBuildSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<PathBuildTables>(
                prev, new PathBuildTables(next.SectorAllocations, next.PathProgress,
                    next.NetworkNodes, next.NetworkEdges, next.NetworkMeta), rng,
                PathBuildSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-029 (D-044): the research engine. It owns the research state
    /// (target, progress, completed knowledge, fired Eurekas). It READS the
    /// specialized-university cost factors from Prev and is not handed them, because
    /// their writer is the institutions system (ADR-033 D6: <see cref="Institutions"/>).
    /// Inert when the config carries no research content.</summary>
    public static SystemRegistration Research(SimConfig cfg)
    {
        var system = new ResearchSystem(cfg.Research);
        return new SystemRegistration(ResearchSystem.WellKnownId, ResearchSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<ResearchTables>(
                prev, new ResearchTables(next.ResearchTargets, next.ResearchProgress,
                    next.ResearchCompleted, next.ResearchEurekas, next.ResearchCredits),
                rng, ResearchSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-031 (D-047 ruling 12): the thin Age evaluator. Owns only the published
    /// next-Age eligibility summary. Inert without Age content.</summary>
    public static SystemRegistration AgeEligibility(SimConfig cfg)
    {
        var system = new Systems.Ages.AgeEligibilitySystem(cfg.Ages);
        return new SystemRegistration(Systems.Ages.AgeEligibilitySystem.WellKnownId, Systems.Ages.AgeEligibilitySystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<Systems.Ages.AgeEligibilityTables>(
                prev, new Systems.Ages.AgeEligibilityTables(next.AgeEligibility),
                rng, Systems.Ages.AgeEligibilitySystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-031 (D-047 rulings 13, 14, 18): explicit Age advancement, the surge record,
    /// and the automatic free modernization of the advancing polity's formations. SOLE OWNER of
    /// MilitaryUnits (worldgen founds the rows; no other system writes them) — modernization is
    /// part of the transition instant, see the system's header. Inert without Age content.</summary>
    public static SystemRegistration AgeTransition(SimConfig cfg)
    {
        var system = new Systems.Ages.AgeTransitionSystem(cfg.Ages, cfg.UnitFamilies);
        return new SystemRegistration(Systems.Ages.AgeTransitionSystem.WellKnownId, Systems.Ages.AgeTransitionSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<Systems.Ages.AgeTransitionTables>(
                prev, new Systems.Ages.AgeTransitionTables(next.AgeStates, next.AgeTransitions, next.MilitaryUnits, next.UnitConversions),
                rng, Systems.Ages.AgeTransitionSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-032: the one authoritative road-development operation (DevelopRoads, player
    /// and AI alike). SOLE OWNER of TransportEdges and RoadDevelopments; sixth holder of the
    /// GoodStocks share (see the ownership record above). Inert without roads tuning.</summary>
    public static SystemRegistration RoadDevelopment(SimConfig cfg)
    {
        var system = new Systems.Roads.RoadDevelopmentSystem(cfg);
        return new SystemRegistration(Systems.Roads.RoadDevelopmentSystem.WellKnownId, Systems.Roads.RoadDevelopmentSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<Systems.Roads.RoadDevelopmentTables>(
                prev, new Systems.Roads.RoadDevelopmentTables(next.TransportEdges, next.RoadDevelopments, next.GoodStocks),
                rng, Systems.Roads.RoadDevelopmentSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>ADR-033 D6 (SystemId 28): universities as real institutions — founds an institution row for
    /// every university building ConstructionSystem completed, matures it while its host is viable, and
    /// REBUILDS the specialized-university cost seam (ResearchCostModifiers, ADR-029 §9) from the maturities.
    /// SOLE OWNER of Institutions and of ResearchCostModifiers (ResearchSystem only reads the latter, from
    /// Prev). Reads Structures, Controls, Buckets, Variables and the roster from Prev only. Inert without an
    /// institutions section.</summary>
    public static SystemRegistration Institutions(SimConfig cfg)
    {
        var system = new Systems.Institutions.InstitutionsSystem(cfg);
        return new SystemRegistration(Systems.Institutions.InstitutionsSystem.WellKnownId, Systems.Institutions.InstitutionsSystem.Name,
            (prev, next, rng, dtDays, dtYears, orders) => system.Step(new SimContext<Systems.Institutions.InstitutionsTables>(
                prev, new Systems.Institutions.InstitutionsTables(next.Institutions, next.ResearchCostModifiers),
                rng, Systems.Institutions.InstitutionsSystem.WellKnownId, dtDays, dtYears, orders, new Ledger(next.LedgerFlows))));
    }

    /// <summary>
    /// All systems that exist at the current milestone — M1 production systems
    /// first, retired T0.x toys last (still registered: the toy preset and the
    /// kernel-invariant tests keep running them).
    /// </summary>
    public static SystemRegistration[] All(SimConfig cfg, Worldgen.WorldgenConfig? worldgen = null) =>
        [Catchment(cfg), HarvestWeather(cfg), Disaster(cfg), Production(cfg), Appropriation(cfg), Consumption(cfg), Price(cfg), TradeArbitrage(cfg),
         Housing(cfg), Construction(cfg), ClassMobility(cfg), Migration(cfg), Colonization(cfg, worldgen), Revolt(cfg), Governance(cfg), Demographics(cfg), NeedsGrievance(cfg), PathBuild(cfg),
         Research(cfg), AgeEligibility(cfg), AgeTransition(cfg), RoadDevelopment(cfg), Institutions(cfg),
         Weather(), Growth(), Trade()];
}
