using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// Every numeric rule of the tactical slice. NOTHING here is a Director value except <see cref="InactivityPulses"/>
/// (brief §2: "a five-pulse inactivity rule"). Every other default is a PLACEHOLDER (invented for the prototype, labelled
/// per field). Integer only.
/// </summary>
public sealed record TacticalRules
{
    /// <summary>Tactical pulse length, ms. PLACEHOLDER (m7-spec.md:385 "fixed-duration pulses with smaller internal time
    /// steps" — no value decided).</summary>
    public long PulseMs { get; init; } = 60_000;
    /// <summary>Internal fixed step (hybrid tick), ms; must divide PulseMs. PLACEHOLDER.</summary>
    public long StepMs { get; init; } = 2_000;

    /// <summary>Reference foot speed on cost-1000 ground, mm/s, before the profile multiplier. PLACEHOLDER.</summary>
    public long RefFootSpeedMmPerS { get; init; } = 1_300;
    /// <summary>Entities may move CatchUpNum/CatchUpDen times the formation pace to regain their slot. PLACEHOLDER.</summary>
    public long CatchUpNum { get; init; } = 3;
    public long CatchUpDen { get; init; } = 2;

    /// <summary>Entity disc radius, mm. PLACEHOLDER.</summary>
    public long EntityRadiusMm { get; init; } = 300;
    /// <summary>Slot spacing inside a formation block, mm. PLACEHOLDER.</summary>
    public long SlotSpacingMm { get; init; } = 1_500;
    /// <summary>Block frontage in entities. PLACEHOLDER.</summary>
    public int FormationColumns { get; init; } = 10;
    /// <summary>Melee reach beyond touching discs: contact iff centre distance &lt;= rA + rB + reach. PLACEHOLDER.</summary>
    public long ContactReachMm { get; init; } = 600;
    /// <summary>Attack: melee entities leave their slot for a detected enemy within this many cells. PLACEHOLDER.</summary>
    public long EngageRadiusCells { get; init; } = 2;

    /// <summary>Observer eye / target heights for LOS (mm above ground). PLACEHOLDER.</summary>
    public long EyeHeightMm { get; init; } = 1_700;
    public long MuzzleHeightMm { get; init; } = 1_500;
    public long AimHeightMm { get; init; } = 1_000;

    /// <summary>Hit chance = attack/(attack+defence) clamped to [HitMin, HitMax]. Formula and clamp PLACEHOLDER.
    /// HitMin must be &gt; 0 (termination; enforced by <see cref="Validate"/>).</summary>
    public Q32 HitMin { get; init; } = Q32.FromRatio(1, 20);
    public Q32 HitMax { get; init; } = Q32.FromRatio(19, 20);
    /// <summary>Damage per hit = max(1, attack / DamageDivisor). PLACEHOLDER.</summary>
    public long DamageDivisor { get; init; } = 5;

    /// <summary>Formation morale scale [0, MoraleMax]. PLACEHOLDER.</summary>
    public long MoraleMax { get; init; } = 1_000;
    /// <summary>Morale lost per casualty = MoraleLossFullStrength / StartCount (losing the whole start = this much). PLACEHOLDER.</summary>
    public long MoraleLossFullStrength { get; init; } = 2_000;
    /// <summary>Below this a formation routs (all Active entities -&gt; Routing). PLACEHOLDER.</summary>
    public long RoutThreshold { get; init; } = 300;
    /// <summary>Per-pulse morale recovery when holding (Hold) / rallying (Rally) with no casualties that pulse. PLACEHOLDER.</summary>
    public long HoldRecovery { get; init; } = 20;
    public long RallyRecovery { get; init; } = 60;
    /// <summary>[MOR] "The fight it won" (docs/m7-plan/morale-proposal.md §3.2): when an enemy land formation G breaks (routs, or loses
    /// its last Active entity) at an instant, each land formation F of the other side that is still in the fight and dealt damage to G
    /// (by any of its weapons, crew-served ones included: [MOR r2, MOR-V2-03]) within the last PulseMs gains
    /// floor(LocalWinMoraleRise × Start(G) ÷ (Start(G) + Start(F))) morale, capped at MoraleMax, after
    /// every rout of that instant (a formation that breaks at the same instant gains nothing). Both resolvers implement it. Default 0:
    /// the exact identity (no tracking, no rise, every golden unchanged). Switching it on moves the parity battery, so it waits for the
    /// battery's re-measurement (D-079 D11, Q4); PROPOSAL value when switched on: 200. PLACEHOLDER.</summary>
    public long LocalWinMoraleRise { get; init; }
    /// <summary>Rally/Heal: HP regained per step from a per-entity pool of HealPoolNum/HealPoolDen x MaxHp. PLACEHOLDER.
    /// The pool is BOUNDED (termination: total damage potential is finite).</summary>
    public long HealPerStep { get; init; } = 1;
    public long HealPoolNum { get; init; } = 1;
    public long HealPoolDen { get; init; } = 2;

    /// <summary>
    /// Detection multiplier for a STATIONARY target whose sight line is concealed (clarity &lt; 1). A target that moved or
    /// fired, or that stands fully in the open (clarity = 1), uses 1. PLACEHOLDER (spec §4 names the factor, gives no value).
    /// </summary>
    public Q32 StationaryConcealment { get; init; } = Q32.FromRatio(1, 64);

    /// <summary>The Director's five-pulse inactivity rule (brief §2 Tactical battles). DIRECTOR VALUE.</summary>
    public int InactivityPulses { get; init; } = 5;
    /// <summary>The active-concealment clause of the inactivity rule (spec §4). Default ON; OFF exists only so a test can show
    /// the clause is what keeps an ambusher alive (teeth), never as a gameplay option.</summary>
    public bool ActiveConcealmentRule { get; init; } = true;

    /// <summary>Action budget N per formation per pulse. INPUT (2 vs 3 is OPEN; default 2 is PLACEHOLDER).</summary>
    public int ActionBudgetN { get; init; } = 2;

    /// <summary>[TR] Which inactivity rule applies. Default: the DIR5 §2.6 per-formation hidden-stationary rule
    /// (<see cref="HiddenStationaryRule"/>). LegacySideDisengage exists only for resolver-parity tests against the
    /// AutoResolver, which still implements the superseded Sprint 1 rule (lane P).</summary>
    public InactivityModel Inactivity { get; init; } = InactivityModel.HiddenStationary;

    /// <summary>[TR, verify-TR-r1 V3] The DIR5-model MUTUAL GUARD (lane TR's PROPOSAL P-TR1, not ratified): when the routs of
    /// one pulse end would leave neither side an Active entity, nobody routs and the battle ends as a Standoff. Default OFF:
    /// D-079 S304 records the five-pulse consequence as "routed or lost, per unit; not a draw or intact escape by default",
    /// so genuinely inactive formations on BOTH sides are routed and the battle ends BothEliminated. ON exists so the
    /// Director can rule on the alternative with a measured implementation (tests pin both).</summary>
    public bool MutualStandoffGuard { get; init; }

    /// <summary>[AP] D-082 DA6-R's PROPOSAL (not decided; docs/m7-plan/concealment-actions-proposal.md §6.4): when ON, lane TR's
    /// UNRATIFIED Standoff end (P-TR1: whole-field idle, nobody routed) counts as a result that separates neither side, so the
    /// designated defender (or, in an ambush, the ambusher) is reported the winner (<see cref="EqualSides"/>), in both resolvers.
    /// Default OFF: a Standoff reports no winner, as before. It changes the reported winner only, never the battle.</summary>
    public bool EqualSidesAtStandoff { get; init; }

    public void Validate()
    {
        if (PulseMs <= 0 || StepMs <= 0 || PulseMs % StepMs != 0) throw new ArgumentException("StepMs must divide PulseMs");
        if (RefFootSpeedMmPerS <= 0 || CatchUpNum <= 0 || CatchUpDen <= 0) throw new ArgumentException("speed");
        if (EntityRadiusMm <= 0 || EntityRadiusMm > Disc.MaxRadiusMm) throw new ArgumentException("radius");
        if (SlotSpacingMm < 2 * EntityRadiusMm) throw new ArgumentException("slot spacing must keep discs apart");
        if (FormationColumns < 1 || ContactReachMm < 0 || EngageRadiusCells < 0) throw new ArgumentException("layout");
        if (!HitMin.IsProbability || !HitMax.IsProbability || HitMin > HitMax) throw new ArgumentException("hit clamp");
        // Termination (D-tactical.md §4): an attack is primary activity, so every attack must be able to consume HP.
        // HitMin = 0 would let two formations in contact attack forever without a hit (verify-D F2).
        if (HitMin == Q32.Zero) throw new ArgumentException("hit clamp: HitMin must be > 0 (every attack needs a positive hit chance, or a battle in contact never ends)");
        if (DamageDivisor <= 0 || MoraleMax <= 0 || MoraleLossFullStrength < 0 || RoutThreshold < 0) throw new ArgumentException("morale/damage");
        if (LocalWinMoraleRise < 0) throw new ArgumentException("local-win morale rise");                    // [MOR]
        if (HealPerStep < 0 || HealPoolNum < 0 || HealPoolDen <= 0) throw new ArgumentException("heal");
        if (!StationaryConcealment.IsProbability) throw new ArgumentException("stationary concealment");
        if (InactivityPulses < 1) throw new ArgumentException("inactivity");
        if (Inactivity != InactivityModel.HiddenStationary && Inactivity != InactivityModel.LegacySideDisengage) throw new ArgumentException("inactivity model");
        _ = new ActionBudget(ActionBudgetN);
    }
}

/// <summary>Explicit placement of one entity (optional; otherwise entities are generated on the formation's slots).</summary>
public readonly record struct EntitySpec(long Id, Vec2L Pos);

/// <summary>
/// A player-commandable formation (owns orders) that the battle expands into Count battle-transient entities (O-10 a).
/// </summary>
public sealed record FormationSpec(long Id, int Side, UnitProfile Profile, int Count, Vec2L Anchor)
{
    public Domain Domain { get; init; } = Domain.Land;
    public Posture Posture { get; init; } = Posture.Normal;
    /// <summary>Starting morale (0..MoraleMax); null = MoraleMax.</summary>
    public long? Morale { get; init; }
    /// <summary>Explicit entity placements (any order; Count must equal its length). Null = generated slots, ids Id*1000+slot.</summary>
    public EntitySpec[]? Entities { get; init; }
    /// <summary>Slice E condition input: experience percent bonus [0, 100] (see <see cref="ConditionMath"/>; PLACEHOLDER mapping). Default 0 = identity.</summary>
    public long Experience { get; init; }
    /// <summary>Slice E condition input: leadership [0, 100] (see <see cref="ConditionMath"/>; PLACEHOLDER mapping). Default 0 = identity.</summary>
    public long Leadership { get; init; }
    /// <summary>Slice E condition input: supply fraction met, Q32 in [0, 1] (see <see cref="ConditionMath"/>; PLACEHOLDER mapping). Default One = identity.</summary>
    public Q32 Supply { get; init; } = Q32.One;

    // ---- lane N (naval / air / combined arms). Land formations leave all four null.
    /// <summary>Domain Naval: the vessel class of every hull (Count = hulls; Profile = the crew's personal profile).</summary>
    public VesselClass? Vessel { get; init; }
    /// <summary>Domain Air: the airframe class of every aircraft (Count = airframes; Profile = the aircrew's personal profile).</summary>
    public AirframeClass? Airframe { get; init; }
    /// <summary>Land only: domain weapons of each entity (crew-served guns: coastal, field, air defence), resolved by the
    /// combined-arms layer. Null = none (legacy land fire only).</summary>
    public WeaponSpec[]? Weapons { get; init; }
    /// <summary>Land only: the formation starts the battle aboard this own transport formation (Anchor is then unused).</summary>
    public long? EmbarkedOn { get; init; }
}

/// <summary>Per-side configuration.</summary>
public sealed record SideSpec(ExitEdge Exit)
{
    /// <summary>Objective points of this side (fixed for the battle; the inactivity rule's "objective" reference).</summary>
    public Vec2L[] Objectives { get; init; } = Array.Empty<Vec2L>();
}

/// <summary>Everything that defines a battle. Formations may be listed in any order (the engine sorts by id).</summary>
public sealed record BattleSetup(
    TerrainGrid Grid,
    TerrainCostTable Costs,
    ConcealmentTable Concealment,
    SideSpec[] Sides,
    FormationSpec[] Formations,
    ulong Seed)
{
    public TacticalRules Rules { get; init; } = new();
    public ProposedCaps Caps { get; init; } = ProposedCaps.Proposed;
    /// <summary>Global instant (ms on the B timeline) at which the battle starts.</summary>
    public long StartMs { get; init; }
    /// <summary>[TR] Terrain properties and coefficients of the resolution equations (DIR5 §2.7). Default: the lane-TR
    /// PLACEHOLDER table, which is the exact identity on flat Road/Open ground.</summary>
    public TerrainEffects Effects { get; init; } = TerrainEffects.Placeholder;
    /// <summary>[TR] Start-position rule (DIR5 §2.14). Unrestricted for unit-level probes; a battle produced by
    /// <see cref="DeploymentPhase.Close"/> carries Thirds, and the battle re-checks it at construction.</summary>
    public DeploymentRule Deployment { get; init; } = DeploymentRule.Unrestricted;
    /// <summary>Lane N: rules and air bases of the naval / air / combined-arms modules (null = defaults).</summary>
    public MultiDomainSetup? MultiDomain { get; init; }

    /// <summary>[AP] The battle's DEFENDER for D-082 DA6 (READING AP-1 (a), applied: the side the engagement that opened the
    /// battle names as attacked — the target side of the opening war-pulse Attack, the garrison of a siege assault, the side
    /// whose position the other advances on in a hand-built scenario; docs/m7-plan/concealment-actions-proposal.md §5). -1: none
    /// designated (a meeting engagement), so equal sides report no winner. Read only by <see cref="EqualSides"/> at the battle
    /// end: it never changes the battle itself.</summary>
    public int Defender { get; init; } = -1;
    /// <summary>[AP] D-082 DA7: the side that opened this battle as an AMBUSH launched from concealment (the AMBUSH definition of
    /// the proposal §5.2: its engaging agent was hidden or camouflaged and undetected by the defender's polity at the instant the
    /// engagement was made; set by the battle's creator — the war-pulse hand-off <c>EngagementBattle</c>, the sandbox, a
    /// scenario author). -1: not an ambush. On equal sides the ambusher wins (DA7-R (a)). Never changes the battle itself.</summary>
    public int Ambusher { get; init; } = -1;
}
