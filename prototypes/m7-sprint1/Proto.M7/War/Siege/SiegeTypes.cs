using Proto.M7.Fixed;
using Proto.M7.WarEcon;

namespace Proto.M7.War.Siege;

/// <summary>Event and order kinds of the lane S siege/garrison world. Disjoint from WarKinds (40-43, 140-141), T3 (150+),
/// TacticalKinds (101-108) and DemoKinds.</summary>
public static class SiegeKinds
{
    public const int TheatrePulseEnd = 60;
    public const int SiegePulseEnd = 61;
    public const int TurnStart = 62;
    public const int TheatrePulseStart = 63;
    public const int ReliefArrival = 64;
    public const int SiegePulseStart = 65;
    public const int RaidArrival = 66;
    /// <summary>Significant: a siege ended (any outcome). Opens a decision window.</summary>
    public const int SiegeEnded = 67;
    /// <summary>Significant: an assault was resolved (either result). Opens a decision window.</summary>
    public const int AssaultResolved = 68;
    /// <summary>Test device (significant): opens a decision window at a chosen instant.</summary>
    public const int Probe = 69;
    /// <summary>Significant: an occupied installation's unrest reached 1 (revolt).</summary>
    public const int Revolt = 70;

    /// <summary>Defender: set the ration level (B = permille of the full ration, RationMin..1000). A = Pack(issuer, siegeId).</summary>
    public const int OrderRation = 180;
    /// <summary>Attacker: assault (B = permille of the melee arm committed, 1..1000). A = Pack(issuer, siegeId).</summary>
    public const int OrderAssault = 181;
    /// <summary>Defender: sally (B = permille of the garrison's melee committed, 1..1000). A = Pack(issuer, siegeId).</summary>
    public const int OrderSally = 182;
    /// <summary>Attacker: lift the siege. A = Pack(issuer, siegeId).</summary>
    public const int OrderLift = 183;
    /// <summary>Owner: garrison formation F at installation B. A = Pack(issuer, F).</summary>
    public const int OrderGarrisonAssign = 184;
    /// <summary>Owner: withdraw garrisoned formation F (it becomes mobile at the installation's cell). A = Pack(issuer, F).</summary>
    public const int OrderGarrisonWithdraw = 185;
    /// <summary>Owner: replace garrisoned formation F by mobile formation B, atomically. A = Pack(issuer, F).</summary>
    public const int OrderGarrisonReplace = 186;
    /// <summary>Owner: move personnel from mobile formation B/2^20 into garrisoned formation F (B mod 2^20 people). A = Pack(issuer, F).</summary>
    public const int OrderGarrisonReinforce = 187;

    public const long ReinforceStride = 1L << 20;
}

/// <summary>Reasons an order is executed as a recorded refusal (no state change).</summary>
public static class SiegeRefusal
{
    public const int BadTarget = 1;
    public const int NotOwner = 2;
    public const int WrongSide = 3;
    public const int SiegeOver = 4;
    public const int BadValue = 5;
    public const int Capacity = 6;
    public const int Eligibility = 7;
    public const int NotPresent = 8;
    public const int AlreadyGarrisoned = 9;
    public const int NotGarrisoned = 10;
    /// <summary>The installation is besieged (any active siege, with or without an open corridor): no garrison order can make a
    /// formation enter, leave or reinforce it; an open corridor's evacuation and reinforcement are the siege's own flows
    /// (SiegeSpec.EvacuationPerYear, ReinforcementPerYear). Fix r1 (S-V9): the text said "with no open corridor".</summary>
    public const int Invested = 11;
    public const int UnknownKind = 12;
    public const int NotController = 13;
}

/// <summary>Formation families relevant to garrisons (a stand-in for the unit charter's families; stable codes).</summary>
public enum FormationFamily : byte
{
    Infantry = 0,
    Ranged = 1,
    Cavalry = 2,
    Engineers = 3,
    /// <summary>Artillery crews (fortress or siege guns). In a garrison its personnel man the installation's guns.</summary>
    Artillery = 4,
    AirDefence = 5,
    Naval = 6,
    Air = 7,
}

/// <summary>Installation kinds that can host a garrison (DIR5 §2.3: "cities, forts, airports, harbours and other installations").</summary>
public enum InstallationKind : byte
{
    City = 0,
    Fort = 1,
    Harbour = 2,
    Airport = 3,
    Depot = 4,
}

/// <summary>Combat arms used by the siege equations. Count = 4.</summary>
public enum Arm : byte
{
    Melee = 0,
    Ranged = 1,
    Cavalry = 2,
    Engineers = 3,
}

/// <summary>Ranged weapon of a force's ranged arm: a CAPABILITY of the unit identity, never a date (Law 4).</summary>
public enum RangedKind : byte
{
    None = 0,
    Bow = 1,
    Firearm = 2,
    Rifle = 3,
}

/// <summary>Siege machine classes. Count = 4. The gunpowder transition keys on the presence of Bombard / ModernGun among
/// the besiegers (computed state), never on a date.</summary>
public enum EngineClass : byte
{
    /// <summary>Rams, bores and towers: close-in breaching, effective only once the approach works are complete.</summary>
    RamTower = 0,
    /// <summary>Torsion and traction/counterweight engines (catapult, trebuchet).</summary>
    Thrower = 1,
    /// <summary>Gunpowder siege artillery, smoothbore (bombard, siege gun).</summary>
    Bombard = 2,
    /// <summary>Modern heavy artillery (rifled, breech-loading, high explosive).</summary>
    ModernGun = 3,
}

/// <summary>Masonry fortification of a settlement, computed from what it has built (D-039 D1; building charter §5.12).</summary>
public enum FortKind : byte
{
    None = 0,
    Palisade = 1,
    MudbrickWall = 2,
    StoneWall = 3,
    Castle = 4,
    /// <summary>Bastion fort (trace italienne): low, thick, earth-backed, angled — the defender's answer to gunpowder.</summary>
    BastionFort = 5,
}

/// <summary>Classification of a siege by the besiegers' computed capability (reporting and Law 4 tests). The equations
/// themselves are continuous in the force composition; the label never gates a mechanism.</summary>
public enum SiegeRegime : byte
{
    Classical = 0,
    Gunpowder = 1,
    Modern = 2,
}

public enum SiegeOutcome : byte
{
    Active = 0,
    /// <summary>Siege health fell to the surrender threshold: unconditional capitulation (garrison taken prisoner).</summary>
    Capitulated = 1,
    /// <summary>An assault succeeded (garrison remnant taken prisoner).</summary>
    Stormed = 2,
    /// <summary>A relief force defeated the besiegers.</summary>
    Relieved = 3,
    /// <summary>The besiegers withdrew (order, doctrine, or collapse of the besieging army).</summary>
    Lifted = 4,
    /// <summary>Maximum duration reached and the defender was the weaker side: surrender on terms (garrison marches out).</summary>
    CapCapitulation = 5,
    /// <summary>Maximum duration reached and the defender held: the besiegers withdraw.</summary>
    CapLifted = 6,
    /// <summary>Every garrison soldier died, deserted or was lost: the settlement falls.</summary>
    GarrisonDestroyed = 7,
}

/// <summary>D-079 Q14 (OPEN): which clock evaluates a siege. A PARAMETER here; the Director decides.</summary>
public enum SiegeClockKind : byte
{
    /// <summary>(b) The Operational War Pulse of the besieging war (era-varying length).</summary>
    WarPulse = 0,
    /// <summary>(a) Literal DIR5 §2.2: tactical battle pulses of a fixed length (the prototype's placeholder is 60 s).</summary>
    BattlePulse = 1,
}

/// <summary>
/// How a store too small for the ration is shared between civilians and garrison. NOT CHOSEN (DIR5 §2.11: the food-allocation
/// interpretation is the Director's): both D-079 readings ("civilian → growth → military" and "civilian → military → growth")
/// reduce, inside a siege where growth is zero, to CiviliansFirst; historical siege practice was usually GarrisonFirst.
/// ProRata is a neutral comparison point. Every caller passes one explicitly.
/// </summary>
public enum ShortfallPolicy : byte
{
    ProRata = 0,
    CiviliansFirst = 1,
    GarrisonFirst = 2,
}

public enum FormationState : byte
{
    Mobile = 0,
    Garrisoned = 1,
    Captured = 2,
}

/// <summary>
/// Every coefficient of the siege equations (Law 2: coefficients inside resolution equations; no free-floating buff).
/// Units: "‰" = permille; "‰/yr" = permille per sim-year (Law 3: every rate is per sim-year and integrated over dt).
/// Provenance labels: DERIVED (from a stated physiological or historical figure, see the report §4), FITTED (set on the
/// calibration fit set; report §6), PLACEHOLDER (no source; a prototype value). None is a Director value.
///
/// REUSED FROM LANE E (Proto.M7.WarEcon; one table, <see cref="LogisticsRates"/>, passed to the world beside this one):
/// the rations (food is Grain in gm: civilians RationBase, soldiers RationBase + RationFieldExtra), the pay table (coin or in
/// kind by arm, Coinage capability), the munitions rates of gunpowder weapons (kg per firearm and per gun per combat day,
/// Gunpowder capability), the desertion and disease-mortality rates (<see cref="TroopLossRates"/>, Sanitation capability) and
/// the head-loss integrator (<see cref="HeadcountLoss"/>); the besiegers' line of communication is lane E's supply line
/// (<see cref="ArmyLogistics.Evaluate"/>). This record holds only what is siege-specific.
/// </summary>
public sealed record SiegeCoefficients
{
    // ---- consumption (water in person-days of the full need; missiles in shooter-days; food and munitions: lane E units)
    public long WaterPerPersonYear { get; init; } = 360;
    /// <summary>Water cannot be rationed below this share of the full need. DERIVED (≈2 of 3-4 litres/day minimum survival).</summary>
    public long WaterMinRationPermille { get; init; } = 600;
    /// <summary>Lowest ration an order may set. PLACEHOLDER.</summary>
    public long RationMinPermille { get; init; } = 150;
    /// <summary>Missiles drawn per non-gunpowder shooter per sim-year of siege: one shooter-day a day. DERIVED (definition of the unit).</summary>
    public long MissilesPerShooterYear { get; init; } = 360;
    /// <summary>Share of a field-battle combat day's munitions (lane E MunitionsPerFirearmCombatDay, MunitionsPerGunCombatDay)
    /// that a gunpowder weapon on the walls or in the lines fires per day of siege: the daily exchange of a siege is far below a
    /// battle's. PLACEHOLDER.</summary>
    public long SiegeFireCombatDayPermille { get; init; } = 100;
    /// <summary>Share of a combat day's munitions a gunpowder siege engine (bombard, modern gun) in battery fires per day of
    /// siege: a breaching battery fires every day. PLACEHOLDER (one combat day per day).</summary>
    public long BombardmentCombatDayPermille { get; init; } = 1_000;

    // ---- nutrition and the food system stand-in (starvation runs on the food system: D-039 D5, RDG-6)
    /// <summary>Body-reserve time constant: nutrition state relaxes to the ration actually eaten over this many days. DERIVED
    /// (weeks of fat reserve on a reduced ration).</summary>
    public long NutritionTauDays { get; init; } = 30;
    /// <summary>a_n: fighting/working capability lost at zero nutrition (linear in the shortfall). FITTED.</summary>
    public long HungerCapabilityPermille { get; init; } = 500;
    /// <summary>Below this nutrition state starvation deaths begin. DERIVED (≈1,000 of 2,000 kcal sustained).</summary>
    public long StarvationThresholdPermille { get; init; } = 500;
    /// <summary>Starvation mortality at zero nutrition. DERIVED (Leningrad winter 1941-42: ≈40-50%/yr at ≈1/4 ration).</summary>
    public long StarvationMaxPermillePerYear { get; init; } = 1_500;
    public long ThirstThresholdPermille { get; init; } = 600;
    /// <summary>Dehydration mortality at zero water. DERIVED (days to death without water).</summary>
    public long ThirstMaxPermillePerYear { get; init; } = 30_000;

    // ---- disease (both camps): logistic SIS on crowding and malnutrition, unconditionally stable semi-implicit step.
    // Deaths: lane E's disease mortality (TroopLossRates.DiseasePermilleYear: base, hunger factor, Sanitation) is the rate at
    // the background prevalence DiseaseFloor; a crowded, hungry city or camp dies in proportion to its prevalence above it.
    public long DiseaseBetaPermillePerYear { get; init; } = 10_000;
    public long DiseaseMalnutritionGainPermille { get; init; } = 2_000;
    public long DiseaseRecoveryPermillePerYear { get; init; } = 12_000;
    /// <summary>Background prevalence (‰ sick) of any body of people: the prevalence at which lane E's camp-disease rate applies.</summary>
    public long DiseaseFloorPermille { get; init; } = 50;

    // ---- morale (will) — relaxes toward a state-derived target
    public long MoraleRatePermillePerYear { get; init; } = 12_000;
    public long MoraleHungerPermille { get; init; } = 600;
    public long MoraleDiseasePermille { get; init; } = 500;
    public long MoraleBreachPermille { get; init; } = 250;
    public long MoraleCasualtyPermille { get; init; } = 500;
    /// <summary>b_o: hopelessness: target × (1 − b_o × enemy/(enemy + own)). FITTED.</summary>
    public long MoraleOddsPermille { get; init; } = 500;
    public long MoraleSupplyPermille { get; init; } = 600;
    public long MoraleUnpaidPermille { get; init; } = 500;
    /// <summary>Immediate fall of the defenders' morale when a relief is beaten (the event). PLACEHOLDER.</summary>
    public long FailedReliefShockPermille { get; init; } = 400;
    /// <summary>The lasting loss of hope once a relief has been beaten: the defender's morale TARGET × (1 − this) for the rest of the
    /// siege, keyed on the computed state "a relief was defeated" (a term of the target equation, Law 2). Was the same coefficient as
    /// the immediate shock (one number doing two jobs; fix r1, S-V8); same value, so nothing moved. PLACEHOLDER.</summary>
    public long ReliefHopeLostPermille { get; init; } = 400;
    public long RepelledAssaultBoostPermille { get; init; } = 50;
    public long FailedAssaultAttackerShockPermille { get; init; } = 100;
    /// <summary>Desertion from an INVESTED place (no open corridor): lane E's desertion rate is that of an army free to walk away; a
    /// besieged garrison's deserters must cross the besiegers' lines, which contain them in proportion to their completeness:
    /// rate × (1 − LinesContain × works of the besiegers). DESIGN (lane S); value PLACEHOLDER (750: a complete circumvallation lets a
    /// quarter through).</summary>
    public long LinesContainDesertionPermille { get; init; } = 750;

    // ---- fortification
    /// <summary>Defence multiplier contribution of intact masonry, by FortKind (fortFactor = 1 + wall × W² + field × E). FITTED (Castle,
    /// BastionFort) / PLACEHOLDER (others, interpolated).</summary>
    public long[] WallValuePermille { get; init; } = { 0, 2_000, 6_000, 15_000, 25_000, 20_000 };
    public long FieldWorksValuePermille { get; init; } = 5_000;
    /// <summary>Wall damage per engine per sim-year against a reference stone wall, by EngineClass. FITTED (Bombard on Constantinople);
    /// Thrower DERIVED as one fifth of a bombard (medieval engines rarely breached strong walls); RamTower and ModernGun PLACEHOLDER.</summary>
    public long[] EngineDamagePermillePerYear { get; init; } = { 3_000, 70, 350, 10_000 };
    /// <summary>Vulnerability of each FortKind to each EngineClass (‰ of the reference), [fort * 4 + engine].</summary>
    public long[] VulnerabilityPermille { get; init; } =
    {
        //  RamTower  Thrower  Bombard  ModernGun
              0,       0,       0,       0,      // None
           2_000,   2_000,   2_000,   2_000,     // Palisade
           1_200,   1_200,     500,   1_500,     // MudbrickWall (earth absorbs shot)
           1_000,   1_000,   1_000,   1_000,     // StoneWall (reference)
             700,     700,     800,   1_000,     // Castle (concentric, thicker)
             500,     300,     150,     800,     // BastionFort (low, thick, earth-backed, angled)
    };
    /// <summary>Damage to earthworks (field works) per engine per sim-year, by EngineClass: earth and rubble absorb shot and high
    /// explosive, so a modern gun that breaches masonry in days barely out-damages a bombard against earth. FITTED (Sevastopol).</summary>
    public long[] EarthDamagePermillePerYear { get; init; } = { 0, 10, 25, 25 };
    /// <summary>Maximum repair rate of masonry per sim-year with the full initial garrison labour, by FortKind.</summary>
    public long[] RepairMaxPermillePerYear { get; init; } = { 0, 4_000, 3_000, 2_000, 1_500, 2_000 };
    public long FieldWorksBuildPermillePerYear { get; init; } = 20_000;
    /// <summary>A practicable breach exists when masonry integrity is at or below this. PLACEHOLDER.</summary>
    public long BreachPracticablePermille { get; init; } = 350;
    /// <summary>Successful mines per sim-year: MineMax × engA / (engA + MineHalf) × (1 − counter), counter = CounterMax × engD /
    /// (engD + CounterHalf). Mining saturates with frontage; counter-mining is detection (listening), effective with few men. FITTED (Vienna).</summary>
    public long MineMaxPermillePerYear { get; init; } = 60_000;
    public long MineHalfEngineers { get; init; } = 3_000;
    public long CounterMineMaxPermille { get; init; } = 800;
    public long CounterMineHalfEngineers { get; init; } = 500;
    /// <summary>Masonry integrity removed by one completed mine, by FortKind.</summary>
    public long[] MineDamagePermille { get; init; } = { 0, 400, 350, 350, 300, 250 };
    /// <summary>Share of civilians that work on repairs and field works. PLACEHOLDER.</summary>
    public long CivilianLabourPermille { get; init; } = 300;
    /// <summary>Infantry labour on approach works counts at this share of an engineer. PLACEHOLDER.</summary>
    public long InfantryWorkPermille { get; init; } = 100;

    // ---- combat values (people-equivalents ‰)
    public long[] DefenceValuePermille { get; init; } = { 1_000, 1_000, 500, 700 };
    public long[] AssaultValuePermille { get; init; } = { 1_000, 400, 300, 600 };
    /// <summary>Values in a FIELD battle (relief battles, sallies, raids, the besiegers' lines): cavalry counts here, not at a breach.</summary>
    public long[] FieldValuePermille { get; init; } = { 1_000, 800, 1_500, 500 };
    /// <summary>A ranged soldier without ammunition still fights hand to hand at this value (‰ of a person).</summary>
    public long DryRangedPermille { get; init; } = 500;
    /// <summary>Ranged weapon factor by RangedKind (capability, not date).</summary>
    public long[] RangedKindFactorPermille { get; init; } = { 0, 1_000, 1_500, 3_000 };
    /// <summary>s_r: assault power gain per unit of ranged:melee support ratio, saturating at the cap ratio.</summary>
    public long RangedSupportPermille { get; init; } = 1_000;
    public long RangedSupportCapRatioPermille { get; init; } = 1_000;
    /// <summary>Assault support of each engine in people-equivalents ‰, by EngineClass.</summary>
    public long[] EngineSupportPermille { get; init; } = { 0, 20_000, 50_000, 500_000 };
    /// <summary>A defender gun (installation guns manned by the garrison's artillery) in people-equivalents ‰.</summary>
    public long DefenderGunValuePermille { get; init; } = 30_000;
    /// <summary>Skirmish/bombardment casualties inflicted per shooter per sim-year, by RangedKind.</summary>
    public long[] FireLethalityPermillePerYear { get; init; } = { 0, 300, 500, 1_000 };
    /// <summary>Casualties inflicted per engine per sim-year, by EngineClass.</summary>
    public long[] EngineLethalityPermillePerYear { get; init; } = { 0, 2_000, 10_000, 100_000 };
    public long DefenderGunLethalityPermillePerYear { get; init; } = 10_000;
    /// <summary>Attacker engines destroyed per defender gun per sim-year (counter-battery), before supply. PLACEHOLDER.</summary>
    public long CounterBatteryPermillePerYear { get; init; } = 200;

    // ---- assault, relief and sally (STAND-IN for the land module; see AssaultResolution)
    public long AssaultAttackerLossPermille { get; init; } = 80;
    public long AssaultAttackerLossCapPermille { get; init; } = 400;
    public long AssaultDefenderLossPermille { get; init; } = 50;
    public long AssaultDefenderLossCapPermille { get; init; } = 600;
    /// <summary>Per-battle fortune amplitude: odds × U[1 − f, 1 + f] (counter-RNG). PLACEHOLDER.</summary>
    public long FortunePermille { get; init; } = 250;
    /// <summary>Share of the garrison's capability that can meet an assault on one sector (the rest holds the circuit and reacts
    /// late). FITTED at 1000 (no reduction): 600 shortened Constantinople to 39 days and did not change Sevastopol.</summary>
    public long DefenceConcentrationPermille { get; init; } = 1_000;
    /// <summary>Share of the besiegers that must keep facing the city while a relief army attacks their lines (the two-front
    /// battle); only the rest meets the relief. FITTED (Vienna, Alesia).</summary>
    public long ReliefContainmentPermille { get; init; } = 400;
    /// <summary>Share of the besiegers that guard the works against a sally. PLACEHOLDER.</summary>
    public long SallyGuardPermille { get; init; } = 250;

    // ---- termination
    /// <summary>Siege health at or below which the garrison capitulates. FITTED.</summary>
    public long SurrenderHealthPermille { get; init; } = 200;
    /// <summary>Besieger health at or below which the besieging army disintegrates (automatic lifting). PLACEHOLDER.</summary>
    public long AttackerCollapsePermille { get; init; } = 100;

    // ---- garrisons (pay: lane E's pay table by arm, LogisticsRates; desertion: TroopLossRates)
    /// <summary>Garrison personnel per 1,000 civilians that fully suppress unrest (≈ the 20-per-1,000 force ratio of
    /// counter-insurgency studies). DERIVED.</summary>
    public long PolicePer1000 { get; init; } = 20;
    public long UnrestDecayPermillePerYear { get; init; } = 2_000;

    public void Validate()
    {
        static void Pos(long v, string n) { if (v <= 0) throw new ArgumentOutOfRangeException(n); }
        static void NonNeg(long v, string n) { if (v < 0) throw new ArgumentOutOfRangeException(n); }
        static void Unit(long v, string n) { if (v < 0 || v > 1000) throw new ArgumentOutOfRangeException(n); }
        static void Len(long[] a, int n, string name) { if (a.Length != n) throw new ArgumentException(name + " length"); foreach (var v in a) NonNeg(v, name); }
        Pos(WaterPerPersonYear, nameof(WaterPerPersonYear));
        Unit(WaterMinRationPermille, nameof(WaterMinRationPermille)); Unit(RationMinPermille, nameof(RationMinPermille));
        if (RationMinPermille == 0) throw new ArgumentOutOfRangeException(nameof(RationMinPermille));
        NonNeg(MissilesPerShooterYear, nameof(MissilesPerShooterYear)); NonNeg(SiegeFireCombatDayPermille, nameof(SiegeFireCombatDayPermille));
        NonNeg(BombardmentCombatDayPermille, nameof(BombardmentCombatDayPermille));
        Pos(NutritionTauDays, nameof(NutritionTauDays));
        Unit(HungerCapabilityPermille, nameof(HungerCapabilityPermille));
        Unit(StarvationThresholdPermille, nameof(StarvationThresholdPermille)); Pos(StarvationThresholdPermille, nameof(StarvationThresholdPermille));
        Unit(ThirstThresholdPermille, nameof(ThirstThresholdPermille)); Pos(ThirstThresholdPermille, nameof(ThirstThresholdPermille));
        NonNeg(StarvationMaxPermillePerYear, nameof(StarvationMaxPermillePerYear)); NonNeg(ThirstMaxPermillePerYear, nameof(ThirstMaxPermillePerYear));
        NonNeg(DiseaseBetaPermillePerYear, nameof(DiseaseBetaPermillePerYear)); NonNeg(DiseaseMalnutritionGainPermille, nameof(DiseaseMalnutritionGainPermille));
        NonNeg(DiseaseRecoveryPermillePerYear, nameof(DiseaseRecoveryPermillePerYear)); Unit(DiseaseFloorPermille, nameof(DiseaseFloorPermille));
        Pos(DiseaseFloorPermille, nameof(DiseaseFloorPermille));
        NonNeg(MoraleRatePermillePerYear, nameof(MoraleRatePermillePerYear));
        foreach (var (v, n) in new[] { (MoraleHungerPermille, "hunger"), (MoraleDiseasePermille, "disease"), (MoraleBreachPermille, "breach"),
                     (MoraleCasualtyPermille, "casualty"), (MoraleOddsPermille, "odds"), (MoraleSupplyPermille, "supply"), (MoraleUnpaidPermille, "unpaid"),
                     (FailedReliefShockPermille, "relief"), (ReliefHopeLostPermille, "hope"), (RepelledAssaultBoostPermille, "boost"), (FailedAssaultAttackerShockPermille, "shock"),
                     (BreachPracticablePermille, "breach"), (LinesContainDesertionPermille, "contain"), (CivilianLabourPermille, "labour"),
                     (InfantryWorkPermille, "work"), (AssaultAttackerLossCapPermille, "acap"), (AssaultDefenderLossCapPermille, "dcap"),
                     (FortunePermille, "fortune"), (SallyGuardPermille, "guard"), (DefenceConcentrationPermille, "concentration"), (ReliefContainmentPermille, "containment"), (SurrenderHealthPermille, "surrender"), (AttackerCollapsePermille, "collapse") })
            Unit(v, n);
        if (FortunePermille >= 1000) throw new ArgumentOutOfRangeException(nameof(FortunePermille), "fortune must stay below 1000 (odds stay positive)");
        Len(WallValuePermille, 6, nameof(WallValuePermille)); NonNeg(FieldWorksValuePermille, nameof(FieldWorksValuePermille));
        Len(EngineDamagePermillePerYear, 4, nameof(EngineDamagePermillePerYear)); Len(VulnerabilityPermille, 24, nameof(VulnerabilityPermille));
        Len(EarthDamagePermillePerYear, 4, nameof(EarthDamagePermillePerYear)); Len(RepairMaxPermillePerYear, 6, nameof(RepairMaxPermillePerYear));
        NonNeg(FieldWorksBuildPermillePerYear, nameof(FieldWorksBuildPermillePerYear));
        NonNeg(MineMaxPermillePerYear, nameof(MineMaxPermillePerYear)); Pos(MineHalfEngineers, nameof(MineHalfEngineers));
        Unit(CounterMineMaxPermille, nameof(CounterMineMaxPermille)); Pos(CounterMineHalfEngineers, nameof(CounterMineHalfEngineers));
        Len(FieldValuePermille, 4, nameof(FieldValuePermille)); NonNeg(DryRangedPermille, nameof(DryRangedPermille));
        Len(MineDamagePermille, 6, nameof(MineDamagePermille));
        foreach (var v in MineDamagePermille) Unit(v, nameof(MineDamagePermille));
        Len(DefenceValuePermille, 4, nameof(DefenceValuePermille)); Len(AssaultValuePermille, 4, nameof(AssaultValuePermille));
        Len(RangedKindFactorPermille, 4, nameof(RangedKindFactorPermille));
        NonNeg(RangedSupportPermille, nameof(RangedSupportPermille)); NonNeg(RangedSupportCapRatioPermille, nameof(RangedSupportCapRatioPermille));
        Len(EngineSupportPermille, 4, nameof(EngineSupportPermille)); NonNeg(DefenderGunValuePermille, nameof(DefenderGunValuePermille));
        Len(FireLethalityPermillePerYear, 4, nameof(FireLethalityPermillePerYear)); Len(EngineLethalityPermillePerYear, 4, nameof(EngineLethalityPermillePerYear));
        NonNeg(DefenderGunLethalityPermillePerYear, nameof(DefenderGunLethalityPermillePerYear)); NonNeg(CounterBatteryPermillePerYear, nameof(CounterBatteryPermillePerYear));
        NonNeg(AssaultAttackerLossPermille, nameof(AssaultAttackerLossPermille)); NonNeg(AssaultDefenderLossPermille, nameof(AssaultDefenderLossPermille));
        Pos(PolicePer1000, nameof(PolicePer1000));
        NonNeg(UnrestDecayPermillePerYear, nameof(UnrestDecayPermillePerYear));
    }

    public static readonly SiegeCoefficients Default = new();
}

/// <summary>The besieging force by arm (people), its ranged weapon (capability) and its machines by EngineClass.</summary>
public sealed record AttackerForce(long[] People, RangedKind Ranged, long[] Engines)
{
    public void Validate()
    {
        if (People.Length != 4 || Engines.Length != 4) throw new ArgumentException("4 arms and 4 engine classes");
        foreach (var v in People) if (v < 0) throw new ArgumentOutOfRangeException(nameof(People));
        foreach (var v in Engines) if (v < 0) throw new ArgumentOutOfRangeException(nameof(Engines));
    }

    public static AttackerForce Of(long melee, long ranged, long cavalry, long engineers, RangedKind kind,
                                   long rams = 0, long throwers = 0, long bombards = 0, long modernGuns = 0)
        => new(new[] { melee, ranged, cavalry, engineers }, kind, new[] { rams, throwers, bombards, modernGuns });
}

/// <summary>A relief army that arrives at ArrivalMs (it acts at the first siege pulse start at or after that instant).</summary>
public sealed record ReliefSpec(long ArrivalMs, long[] People, RangedKind Ranged);

/// <summary>The besiegers' line of communication from their depot to the siege (position = the route's end), carried by their
/// train (wagons, draft animals, trucks): lane E's <see cref="Route"/> and <see cref="ArmyLogistics.Evaluate"/> give the carriage
/// loss and the throughput.</summary>
public sealed record SiegeSupplyLine(Route Route, long Wagons, long DraftAnimals, long Trucks);

/// <summary>
/// AI doctrine for one side of an AI-controlled siege (the calibration controller; a policy, not a siege equation).
/// Thresholds in permille; cool-downs in ms (time-based, so the doctrine is clock-independent).
/// </summary>
public sealed record SiegeDoctrine
{
    // defender
    /// <summary>Ration to RationHigh (‰) when the store covers fewer than RationTriggerDays of the NET draw at full ration (full need
    /// minus the inflow through an open corridor); to RationLow below LowTriggerDays. A store that the inflow keeps full needs no
    /// rationing.</summary>
    public long RationTriggerDays { get; init; } = 60;
    public long RationHighPermille { get; init; } = 600;
    public long LowTriggerDays { get; init; } = 20;
    public long RationLowPermille { get; init; } = 250;
    // attacker
    /// <summary>Assault when the expected odds (before fortune) are at least this (‰ of even odds).</summary>
    public long AssaultMinOddsPermille { get; init; } = 1_300;
    public long AssaultCommitPermille { get; init; } = 800;
    public long AssaultCooldownMs { get; init; } = 7 * Proto.M7.Time.SimInstant.MsPerDay;
    /// <summary>Lift when the besiegers' own health falls to this (‰).</summary>
    public long LiftHealthPermille { get; init; } = 200;

    public static readonly SiegeDoctrine Default = new();
}

/// <summary>Everything that defines one siege. The defender is the installation's controller at StartMs.</summary>
public sealed record SiegeSpec(int Id, int Attacker, int InstallationId, long StartMs, AttackerForce Force)
{
    /// <summary>Opening food of the besiegers' field granary (Grain, gm). Resupply: through <see cref="SupplyLine"/> (lane E's supply
    /// line: their need grossed up for the carriage loss, capped by the line's throughput) or, without one, the flat delivered
    /// rate <see cref="AttackerSupplyPerYear"/> (gm per sim-year; e.g. a sea route given as an input).</summary>
    public long AttackerFood { get; init; }
    public long AttackerSupplyPerYear { get; init; }
    /// <summary>The besiegers' line of communication (lane E route and train). Null: the flat rate above.</summary>
    public SiegeSupplyLine? SupplyLine { get; init; }
    /// <summary>Opening munitions (kg) and missiles (shooter-days) of the besiegers' park; both are topped up from the attacker's
    /// home stocks to each sub-step's need (lane E's rule for munitions: shipped from the polity stock, no carriage limit).</summary>
    public long AttackerMunitions { get; init; }
    public long AttackerMissiles { get; init; }
    /// <summary>Besieger camp capacity (people) for the camp-disease crowding term.</summary>
    public long CampCapacity { get; init; } = 1;
    /// <summary>Besieger field works (circumvallation/contravallation, trenches) in [0, 1] ‰.</summary>
    public long AttackerWorksPermille { get; init; }
    /// <summary>An open corridor resupplies the defenders from their home stores (food in gm, munitions in kg and missiles in
    /// shooter-days per sim-year) and evacuates civilians (people per sim-year).</summary>
    public long CorridorFoodPerYear { get; init; }
    /// <summary>The corridor (food, ammunition, evacuation, reinforcement) flows only from this instant (e.g. a lake that must freeze).</summary>
    public long CorridorFromMs { get; init; }
    public long CorridorMunitionsPerYear { get; init; }
    public long CorridorMissilesPerYear { get; init; }
    public long EvacuationPerYear { get; init; }
    /// <summary>Reinforcements through the corridor (people per sim-year) from the defender's home reserve into its lowest-id
    /// garrisoned formation of a fighting family at the installation.</summary>
    public long ReinforcementPerYear { get; init; }
    /// <summary>Reinforcements of the besiegers (people per sim-year) from the attacker's home reserve, spread over the arms in the
    /// proportions of the opening force (floors; the remainder to the largest opening arm, lowest index on a tie).</summary>
    public long AttackerReinforcementPerYear { get; init; }
    /// <summary>The besiegers have cut the water inflow (aqueduct, river access). Wells/cisterns inside keep the store.</summary>
    public bool WaterCut { get; init; }
    public bool Mineable { get; init; } = true;
    /// <summary>Approach works the besiegers must complete before rams/towers can reach the wall (engineer-years); 0 = none needed.</summary>
    public long ApproachWorkEngineerYears { get; init; }
    public ReliefSpec? Relief { get; init; }
    /// <summary>Null = the side is human (orders only); otherwise the AI doctrine.</summary>
    public SiegeDoctrine? AttackerAi { get; init; } = SiegeDoctrine.Default;
    public SiegeDoctrine? DefenderAi { get; init; } = SiegeDoctrine.Default;
    public long InitialMoralePermille { get; init; } = 1_000;
    public long AttackerMoralePermille { get; init; } = 1_000;
}

/// <summary>An installation (settlement, fort, harbour, airport, depot) that can be garrisoned and besieged.</summary>
public sealed record InstallationSpec(int Id, InstallationKind Kind, int Controller, long X, long Y, long Capacity)
{
    public long Civilians { get; init; }
    /// <summary>Granary (Grain, gm: 1000 gm = one adult's year of food, D-015).</summary>
    public long Food { get; init; }
    public long Water { get; init; }
    public long WaterInflowPerYear { get; init; }
    /// <summary>Powder and shot (Munitions, kg) for gunpowder weapons and guns.</summary>
    public long Munitions { get; init; }
    /// <summary>Arrows, bolts and sling shot (Missiles, shooter-days) for non-gunpowder ranged arms.</summary>
    public long Missiles { get; init; }
    /// <summary>Installed guns (equipment) manned by the garrison's artillery personnel.</summary>
    public long Guns { get; init; }
    public long GunCrew { get; init; } = 10;
    public FortKind Fort { get; init; } = FortKind.None;
    public long WallPermille { get; init; } = 1_000;
    public long FieldWorksPermille { get; init; }
    /// <summary>Housing capacity (people) for the crowding term of disease.</summary>
    public long Housing { get; init; } = 1;
    public long LoyaltyPermille { get; init; } = 1_000;
    /// <summary>Unrest growth per sim-year when no garrison polices it, while the installation is OCCUPIED (held by a polity other
    /// than its population's own; fix r1, S-V7). An input (the population's disposition toward a foreign ruler). 0 = never restive.</summary>
    public long GrievancePermillePerYear { get; init; }
    /// <summary>The polity the population belongs to; -1 = the initial controller (an own city). Occupation = controller ≠ this.</summary>
    public int PopulationPolity { get; init; } = -1;
    /// <summary>Where a garrison that marches out (terms) or is withdrawn is placed.</summary>
    public long ExitX { get; init; }
    public long ExitY { get; init; }
}

/// <summary>A formation that can be mobile or garrisoned (one capability, one state: D-065 R11).</summary>
public sealed record FormationSpec(int Id, int Owner, FormationFamily Family, long Personnel, long X, long Y)
{
    public RangedKind Ranged { get; init; } = RangedKind.None;
    /// <summary>Installation id to start garrisoned in (-1 = mobile).</summary>
    public int GarrisonedAt { get; init; } = -1;
}

/// <summary>A raid on an installation, resolved at the first theatre pulse start at or after ArrivalMs.</summary>
public sealed record RaidSpec(int Id, int Raider, int InstallationId, long ArrivalMs, long People, RangedKind Ranged)
{
    /// <summary>Share of the installation's food carried off by a successful raid (‰).</summary>
    public long PillagePermille { get; init; } = 300;
}

/// <summary>The siege-evaluation clock (D-079 Q14 parameter).</summary>
public sealed record SiegeClock(SiegeClockKind Kind, long BattlePulseMs)
{
    public static SiegeClock WarPulse { get; } = new(SiegeClockKind.WarPulse, 0);
    /// <summary>The tactical battle pulse at the Slice D placeholder length (TacticalRules.PulseMs = 60,000 ms).</summary>
    public static SiegeClock BattlePulsePlaceholder { get; } = new(SiegeClockKind.BattlePulse, 60_000);
    public static SiegeClock Battle(long ms) => new(SiegeClockKind.BattlePulse, ms);
}

/// <summary>Small Q32 helpers used by the siege equations (all exact integer math, floor rounding).</summary>
public static class Q
{
    public static Q32 Permille(long p) => Q32.FromRatio(p, 1000);
    public static Q32 Clamp01(Q32 x) => x.Raw < 0 ? Q32.Zero : x.Raw > Q32.OneRaw ? Q32.One : x;
    public static Q32 Min(Q32 a, Q32 b) => a.Raw <= b.Raw ? a : b;
    public static Q32 Max(Q32 a, Q32 b) => a.Raw >= b.Raw ? a : b;
    public static Q32 Add(Q32 a, Q32 b) => new(checked(a.Raw + b.Raw));
    public static Q32 Sub(Q32 a, Q32 b) => new(checked(a.Raw - b.Raw));
    /// <summary>1 − c × (1 − x), for c and x in [0, 1]: a linear penalty of weight c on the shortfall of x.</summary>
    public static Q32 Penalty(Q32 c, Q32 x) => Clamp01(Sub(Q32.One, c.Mul(Sub(Q32.One, Clamp01(x)))));
    /// <summary>A per-sim-year rate (‰/yr, possibly scaled by a Q32 factor) integrated over dt ms: rate/1000 × dt / MsPerYear.</summary>
    public static Q32 OverDt(long permillePerYear, long dtMs)
        => new(FixedMath.MulDivFloor(checked(permillePerYear * dtMs), Q32.OneRaw, 1000 * Proto.M7.Time.SimInstant.MsPerYear));
    /// <summary>x / (1 + x): the implicit-Euler loss share of a stock exposed to hazard x over one step (bounded by 1, stable for any dt).</summary>
    public static Q32 ImplicitShare(Q32 x) => x.Raw <= 0 ? Q32.Zero : new(FixedMath.MulDivFloor(x.Raw, Q32.OneRaw, checked(Q32.OneRaw + x.Raw)));
    /// <summary>a / b as Q32, b &gt; 0; 0 when a ≤ 0.</summary>
    public static Q32 Ratio(long a, long b) => a <= 0 ? Q32.Zero : Q32.FromRatio(a, b);
    /// <summary>(a + k·b) / (1 + k): the implicit relaxation of a toward target b with step k (stable for any k ≥ 0).</summary>
    public static Q32 Relax(Q32 a, Q32 b, Q32 k)
        => new(FixedMath.MulDivFloor(checked(a.Raw + k.Mul(b).Raw), Q32.OneRaw, checked(Q32.OneRaw + k.Raw)));
    /// <summary>Permille text-free conversion for reports: floor(q × 1000).</summary>
    public static long ToPermille(Q32 q) => q.Scale(1000);
}
