using Proto.M7.Spatial;

namespace Proto.M7.WarEcon;

/// <summary>
/// Road class of a route segment. DirtPath, Track and Built are the campaign's road classes (sim.json roads.classes
/// edgeType 1, 2, 3; speedFactor 1.0 / 0.85 / 0.7 CITED). None is trackless ground (off the network, D-079 Q11 open).
/// </summary>
public enum RoadClass : byte
{
    None = 0,
    DirtPath = 1,
    Track = 2,
    Built = 3,
}

/// <summary>How a supply-line segment carries freight. Chosen per segment from infrastructure AND capability (Law 4).</summary>
public enum CarriageMode : byte
{
    /// <summary>Pack animals and porters on trackless ground.</summary>
    Pack = 0,
    /// <summary>Animal-drawn wagons on a road (any class).</summary>
    Wagon = 1,
    /// <summary>River or coastal boats on a waterway segment.</summary>
    Water = 2,
    /// <summary>Railway (Railways capability and a rail segment).</summary>
    Rail = 3,
    /// <summary>Motor trucks on a road (MotorTransport capability); burns fuel instead of cargo.</summary>
    Motor = 4,
}

/// <summary>
/// Every per-sim-year rate and calibration constant of the war economy, in integer units (Law 3: rates are per sim-year
/// and are integrated with exact dtYears = dtMs / MsPerYear; Law 7 stocks are long). NOTHING here is decided: the values of
/// <see cref="Proposal"/> are a labelled PROPOSAL with the reference each one comes from (reports/H-E.md §3 calibration
/// table). Units: grain in milli-units (gm; 1000 gm = 1 shipped grain unit = one adult's year of food, D-015 / sim.json
/// military.upkeepGrainPerPersonYear 1.0), money in "soldier-months" (one month of an infantryman's pay), distance in
/// metres, munitions in kg, equipment in milli-kits, fuel in litres. Fractions are per mille (‰) unless stated.
/// </summary>
public sealed record LogisticsRates
{
    // ---- rations (men) ----
    /// <summary>Standing ration, gm per man-year. CITED: sim.json military.upkeepGrainPerPersonYear 1.0 (x1000).</summary>
    public long RationBase { get; init; }
    /// <summary>Extra ration in the field (camp labour, guard, entrenching), gm per man-year. PROPOSAL.</summary>
    public long RationFieldExtra { get; init; }
    /// <summary>Extra ration while marching, gm per man-year of marching. PROPOSAL: base+field+march = 1900 gm/yr, about
    /// 1.33 kg grain per day at 0.252 kg per gm, the order of Engels' 3 lb (1.36 kg) per soldier-day (secondary sources,
    /// contested by O'Connor 2013).</summary>
    public long RationMarchExtra { get; init; }

    // ---- fodder (animals): hard feed (grain) and roughage, the latter met by grazing or by substituted grain ----
    public long HorseHardRest { get; init; }
    public long HorseHardWorkExtra { get; init; }
    public long HorseRoughageAsGrain { get; init; }
    public long DraftHardRest { get; init; }
    public long DraftHardWorkExtra { get; init; }
    public long DraftRoughageAsGrain { get; init; }

    // ---- pay ----
    /// <summary>Coin pay per man-year by arm (foot, mounted, gun crew), in soldier-months. PROPOSAL.</summary>
    public long PayFoot { get; init; }
    public long PayMounted { get; init; }
    public long PayGunCrew { get; init; }
    /// <summary>Wages in kind per man-year (no coinage), gm of grain on top of the ration. PROPOSAL.</summary>
    public long WageInKindFoot { get; init; }
    public long WageInKindMounted { get; init; }
    public long WageInKindGunCrew { get; init; }

    // ---- munitions (gunpowder only) ----
    public long MunitionsPerFirearmCombatDay { get; init; }
    public long MunitionsPerGunCombatDay { get; init; }
    public long MunitionsPerFirearmYearDrill { get; init; }

    // ---- equipment wear, milli-kits ----
    public long KitWearPerManYear { get; init; }
    public long KitWearPerMan1000Km { get; init; }
    public long KitWearPerManCombatDay { get; init; }

    // ---- fuel ----
    /// <summary>Litres per truck per 1000 km. PROPOSAL (about 0.4 L/km for a 2.5-3 t truck).</summary>
    public long FuelPerTruck1000Km { get; init; }

    // ---- movement ----
    /// <summary>Sustained march speed in metres per day on open ground (rest days included), by the slowest element.</summary>
    public long MarchFootMPerDay { get; init; }
    public long MarchMountedMPerDay { get; init; }
    public long MarchOxTrainMPerDay { get; init; }
    public long MarchMotorMPerDay { get; init; }
    public long MarchRailMPerDay { get; init; }

    // ---- supply line ----
    /// <summary>"Radius" km at which carriage by this mode consumes the whole cargo (loss = km / radius on open ground).</summary>
    public long PackRadiusKm { get; init; }
    public long WagonRadiusKm { get; init; }
    public long WaterRadiusKm { get; init; }
    public long RailRadiusKm { get; init; }
    /// <summary>Magazines (commissariat) cut land-carriage loss to this ‰ of the base. PROPOSAL.</summary>
    public long MagazineLossPermille { get; init; }
    /// <summary>Payload per wagon / per truck in gm (kg / 0.252).</summary>
    public long WagonPayloadGm { get; init; }
    public long TruckPayloadGm { get; init; }
    /// <summary>Throughput of one route segment, gm per sim-year, by class (sim.json roads.classes capacityTonnesPerYear CITED
    /// for DirtPath 100,000 t, Track 150,000 t, Built 300,000 t; trackless, water and rail PROPOSAL).</summary>
    public long CapacityNoneGmPerYear { get; init; }
    public long CapacityDirtGmPerYear { get; init; }
    public long CapacityTrackGmPerYear { get; init; }
    public long CapacityBuiltGmPerYear { get; init; }
    public long CapacityWaterGmPerYear { get; init; }
    public long CapacityRailGmPerYear { get; init; }

    // ---- foraging (stock draws: nominal per-year rates on what is left, applied once per elapsed day; StockDraw) ----
    /// <summary>Orderly foraging in an enemy locality: ‰ per year of the grain left there (nominal, applied daily). PROPOSAL,
    /// calibrated so a 7-day span takes 40 % (the former per-pulse share): 25,335 ‰/yr = 70.4 ‰ a day.</summary>
    public long ForageForeignPermilleYear { get; init; }
    /// <summary>Forced requisition (enemy locality, supply failed) and plunder (mutineers, any locality): ‰ per year of what
    /// is left (nominal, applied daily). PROPOSAL, calibrated so a 7-day span takes 90 %: 100,910 ‰/yr = 280.3 ‰ a day.</summary>
    public long ForageForcedPermilleYear { get; init; }
    /// <summary>Contributions levied on an enemy locality's money: ‰ per year of the money left there (nominal, applied
    /// daily). PROPOSAL, calibrated so a 7-day span takes 10 %: 5,378 ‰/yr = 14.9 ‰ a day.</summary>
    public long ContributionPermilleYear { get; init; }
    /// <summary>Days of food each man carries on his back (part of the train capacity). PROPOSAL (Roman and Napoleonic
    /// practice of 3-4 days, unverified).</summary>
    public long CarryDaysPerMan { get; init; }

    // ---- home front stand-in (campaign economy, settled at turn starts) ----
    /// <summary>Harvest per head of population per year, gm. PROPOSAL stand-in (a 15% surplus over subsistence).</summary>
    public long HarvestPerCapitaYear { get; init; }
    /// <summary>Harvest lost per mobilised man-year, gm. CITED (measured): F-economy recruitment twins, 1.3-1.9 grain per
    /// soldier-year; the midpoint 1.6 is taken.</summary>
    public long LostHarvestPerSoldierYear { get; init; }
    /// <summary>Deaths per unfed person-year, ‰ (civilian famine at a campaign settlement). PROPOSAL.</summary>
    public long FamineMortalityPermille { get; init; }

    // ---- condition and cascade (per-mille rates per year; morale and cohesion on 0..1000) ----
    public long MoraleRecoveryPerYear { get; init; }
    /// <summary>[MOR] The morale (‰ of the scale) toward which a fed, paid, loyal army's morale recovers, and toward which an excess
    /// above it decays (docs/m7-plan/morale-proposal.md §4.4). Default 1000 = the top of the scale: lane E's model exactly (the
    /// recovery runs to the cap and nothing can lie above it). Lower values are a PROPOSAL for M7.14's calibration (§4.4 option (b):
    /// a victory can then lift an army above its steady level, and the lift fades); not lane E's default.</summary>
    public long MoraleBaselinePerMille { get; init; } = 1000;
    /// <summary>[MOR] The rate (‰ of the scale per sim-year) at which morale above <see cref="MoraleBaselinePerMille"/> decays toward it
    /// (the excess a victory left), integrated over the exact pulse length. It acts only when the baseline lies below the cap.
    /// PROPOSAL: lane E's recovery rate mirrored.</summary>
    public long MoraleElationDecayPerYear { get; init; } = 600;
    public long MoraleHungerPenaltyPerYear { get; init; }
    public long MoraleArrearsPenaltyPerMonthPerYear { get; init; }
    public long CohesionRecoveryPerYear { get; init; }
    public long CohesionArrearsPenaltyPerMonthPerYear { get; init; }
    public long CohesionHungerPenaltyPerYear { get; init; }
    public long MutinyCohesionThreshold { get; init; }
    public long MutinyRecoveredCohesion { get; init; }
    public long DesertionBasePermilleYear { get; init; }
    public long DesertionLowMoralePermilleYear { get; init; }
    public long DesertionPerArrearsMonthPermilleYear { get; init; }
    public long DesertionArrearsCapPermilleYear { get; init; }
    public long DesertionHungerPermilleYear { get; init; }
    public long DesertionMutinyPermilleYear { get; init; }
    public long DiseaseBasePermilleYear { get; init; }
    /// <summary>Disease multiplier per unit hunger and per unit equipment deficit, ‰ added to 1000.</summary>
    public long DiseaseHungerFactorPermille { get; init; }
    public long DiseaseExposureFactorPermille { get; init; }
    /// <summary>Sanitation multiplies disease mortality by this ‰.</summary>
    public long DiseaseSanitationPermille { get; init; }
    /// <summary>Deaths from starvation itself, ‰ per year at a total ration shortfall (scaled by the shortfall; not reduced
    /// by Sanitation). PROPOSAL: about a quarter of the men a month with no food at all.</summary>
    public long StarvationDeathPermilleYear { get; init; }
    public long AnimalDeathBasePermilleYear { get; init; }
    public long AnimalDeathStarvePermilleYear { get; init; }
    /// <summary>An army whose men fall to this ‰ of its initial strength disbands.</summary>
    public long DisbandStrengthPermille { get; init; }
    /// <summary>Days a mutinous army's cohesion must have been at zero before it disbands (a duration, Law 3; the zero
    /// instant is interpolated inside the pulse, detection is at pulse ends). PROPOSAL: 7 days (the former "two pulses" at
    /// the 7-day pulse length).</summary>
    public long DisbandZeroCohesionDays { get; init; }

    // ---- ships' companies (lane N vessel classes; NavalLogistics). Every value PROPOSAL, sources in reports/H-E.md §3a ----
    /// <summary>Extra ration at sea, gm per man-year (replaces the field extra): the sea ration was heavier than a soldier's
    /// (biscuit, salt meat, pease, beer; general knowledge, unverified). Base + sea = 1800 gm/yr, about 1.26 kg/day.</summary>
    public long RationSeaExtra { get; init; }
    /// <summary>Extra ration of a rower while rowing, gm per rower-year of rowing (heavy sustained labour). PROPOSAL.</summary>
    public long RationRowExtra { get; init; }
    /// <summary>Rowers per 1000 of an oared vessel's crew (a trireme: about 170 oarsmen of 200; unverified).</summary>
    public long RowersPermille { get; init; }
    /// <summary>Seaman's pay per man-year in soldier-months (coin) and wages in kind (gm) without coinage. PROPOSAL.</summary>
    public long PaySeaman { get; init; }
    public long WageInKindSeaman { get; init; }
    /// <summary>Passage speed (m/day, averaged over a passage) of oared and sailing vessels by propulsion: oared vessels beach
    /// or anchor at night and row in short legs; sail averages a few knots over contrary and slack winds (their lane N
    /// MaxSpeedMmPerS is a sprint or a best point of sailing, not a passage speed).</summary>
    public long PassageOarMPerDay { get; init; }
    public long PassageSailMPerDay { get; init; }
    /// <summary>Engine-driven vessels steam on passage at this ‰ of their lane N class's MaxSpeedMmPerS (sustained sea speed
    /// below trial speed, for coal economy and machinery). PROPOSAL 550 ‰: steam frigate 270.9 km/day (6.1 kn), turret
    /// warship 731.8 km/day (16.5 kn).</summary>
    public long PassageEngineSustainedPermille { get; init; }
    /// <summary>Provisions a hull can stow, in days of its company's sea ration, by propulsion (galleys carried very little,
    /// sailing warships months; steamers give stowage to coal).</summary>
    public long StoresDaysOar { get; init; }
    public long StoresDaysSail { get; init; }
    public long StoresDaysEngine { get; init; }
    /// <summary>Bunker coal, kg per steam hull per 1000 km steamed. PROPOSAL.</summary>
    public long CoalPerHull1000Km { get; init; }
    /// <summary>Powder and shot, kg per gun-armed hull per day of action. PROPOSAL.</summary>
    public long MunitionsPerArmedHullCombatDay { get; init; }
    /// <summary>Scurvy: days without fresh food before deaths begin; then ‰/yr per 30 days beyond the onset, capped; the
    /// Antiscorbutics capability multiplies the rate by this ‰. Sanitation does not affect scurvy (it is dietary).</summary>
    public long ScurvyOnsetDays { get; init; }
    public long ScurvyPermilleYearPerMonth { get; init; }
    public long ScurvyMaxPermilleYear { get; init; }
    public long ScurvyAntiscorbuticPermille { get; init; }

    // ---- finance (turn-level, stand-in: the campaign has no treasury today, CR-008) ----
    /// <summary>Credit limit as ‰ of annual revenue (Banking only). PROPOSAL.</summary>
    public long CreditLimitPermilleOfRevenue { get; init; }
    /// <summary>Interest per year on war debt, ‰. PROPOSAL.</summary>
    public long InterestPermilleYear { get; init; }

    /// <summary>The PROPOSED calibration (reports/H-E.md §3). Not decided; every number is labelled there.</summary>
    public static LogisticsRates Proposal() => new()
    {
        RationBase = 1000, RationFieldExtra = 300, RationMarchExtra = 600,
        HorseHardRest = 4000, HorseHardWorkExtra = 2400, HorseRoughageAsGrain = 3500,
        DraftHardRest = 1000, DraftHardWorkExtra = 1500, DraftRoughageAsGrain = 5000,
        PayFoot = 12, PayMounted = 30, PayGunCrew = 15,
        WageInKindFoot = 500, WageInKindMounted = 1200, WageInKindGunCrew = 600,
        MunitionsPerFirearmCombatDay = 1, MunitionsPerGunCombatDay = 400, MunitionsPerFirearmYearDrill = 5,
        KitWearPerManYear = 250, KitWearPerMan1000Km = 400, KitWearPerManCombatDay = 20,
        FuelPerTruck1000Km = 400,
        MarchFootMPerDay = 20_000, MarchMountedMPerDay = 30_000, MarchOxTrainMPerDay = 15_000,
        MarchMotorMPerDay = 150_000, MarchRailMPerDay = 400_000,
        PackRadiusKm = 150, WagonRadiusKm = 600, WaterRadiusKm = 6_000, RailRadiusKm = 40_000,
        MagazineLossPermille = 600,
        WagonPayloadGm = 1_984, TruckPayloadGm = 11_905,
        CapacityNoneGmPerYear = 79_365_000, CapacityDirtGmPerYear = 396_825_000, CapacityTrackGmPerYear = 595_238_000,
        CapacityBuiltGmPerYear = 1_190_476_000, CapacityWaterGmPerYear = 3_968_254_000, CapacityRailGmPerYear = 7_936_508_000,
        ForageForeignPermilleYear = 25_335, ForageForcedPermilleYear = 100_910, ContributionPermilleYear = 5_378, CarryDaysPerMan = 4,
        HarvestPerCapitaYear = 1150, LostHarvestPerSoldierYear = 1600, FamineMortalityPermille = 200,
        MoraleRecoveryPerYear = 600, MoraleHungerPenaltyPerYear = 2400, MoraleArrearsPenaltyPerMonthPerYear = 60,
        CohesionRecoveryPerYear = 400, CohesionArrearsPenaltyPerMonthPerYear = 90, CohesionHungerPenaltyPerYear = 1200,
        MutinyCohesionThreshold = 250, MutinyRecoveredCohesion = 500,
        DesertionBasePermilleYear = 50, DesertionLowMoralePermilleYear = 400, DesertionPerArrearsMonthPermilleYear = 30,
        DesertionArrearsCapPermilleYear = 600, DesertionHungerPermilleYear = 1500, DesertionMutinyPermilleYear = 300,
        DiseaseBasePermilleYear = 60, DiseaseHungerFactorPermille = 4000, DiseaseExposureFactorPermille = 1000,
        DiseaseSanitationPermille = 300, StarvationDeathPermilleYear = 3000,
        AnimalDeathBasePermilleYear = 50, AnimalDeathStarvePermilleYear = 1500,
        DisbandStrengthPermille = 100, DisbandZeroCohesionDays = 7,
        CreditLimitPermilleOfRevenue = 3000, InterestPermilleYear = 80,
        RationSeaExtra = 800, RationRowExtra = 800, RowersPermille = 850, PaySeaman = 12, WageInKindSeaman = 500,
        PassageOarMPerDay = 60_000, PassageSailMPerDay = 140_000, PassageEngineSustainedPermille = 550,
        StoresDaysOar = 5, StoresDaysSail = 120, StoresDaysEngine = 90,
        CoalPerHull1000Km = 60_000, MunitionsPerArmedHullCombatDay = 5_000,
        ScurvyOnsetDays = 56, ScurvyPermilleYearPerMonth = 400, ScurvyMaxPermilleYear = 4_000, ScurvyAntiscorbuticPermille = 50,
    };
}

/// <summary>
/// Terrain logistics per Proto.M7.Spatial.TerrainClass: march cost (x1000; 1000 = open ground; inverse speed), grazing
/// share for animals (‰ of roughage the ground supplies), and equipment-wear factor (x1000). March costs reuse the Slice C
/// PLACEHOLDER movement table (Road 700 = sim.json road_built speedFactor 0.7 CITED; Open 1000 by definition; Forest
/// 1500, Hills 1800, Jungle 2500, Mountain 4000 PLACEHOLDER) so "jungle and mountains increase costs" (DIR5 §2.11) holds
/// by construction. Grazing and wear are PROPOSAL. Water is passable only by a naval force (a land army halts at it); Wall and
/// Building are impassable. Marsh and Ford (lane TR) are passable at the Slice C table's cost.
/// </summary>
public sealed class TerrainLogistics
{
    private readonly long[] _march, _graze, _wear;

    private TerrainLogistics(long[] march, long[] graze, long[] wear) { _march = march; _graze = graze; _wear = wear; }

    public long MarchCost(TerrainClass c) => _march[(int)c];
    public long GrazePermille(TerrainClass c) => _graze[(int)c];
    public long WearFactor(TerrainClass c) => _wear[(int)c];

    public static TerrainLogistics Proposal()
    {
        var cost = TerrainCostTable.PlaceholderTestTable();
        var march = new long[TerrainCostTable.ClassCount];
        for (int i = 0; i < march.Length; i++) march[i] = cost.Cost((TerrainClass)i);
        // One entry per TerrainClass code (TerrainCostTable.ClassCount; lane TR appended Marsh, Ford, Building).
        //                     Road Open Forest Jungle Hills Mountain Water Wall Marsh Ford Building
        var graze = new long[] { 600, 800, 400, 300, 600, 250, 0, 0, 500, 600, 0 };
        var wear = new long[] { 800, 1000, 1300, 1800, 1400, 2000, 0, 0, 1600, 1300, 0 };
        if (graze.Length != TerrainCostTable.ClassCount || wear.Length != TerrainCostTable.ClassCount)
            throw new InvalidOperationException("terrain logistics needs one entry per terrain class");
        return new TerrainLogistics(march, graze, wear);
    }
}
