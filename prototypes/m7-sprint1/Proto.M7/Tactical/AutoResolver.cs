using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// Calibrated coefficients of the AutoResolver (Slice E, O-16 delegated; lane P). Every value is a model coefficient INSIDE
/// the resolution equations (CLAUDE.md law 2), integer (permille or steps). They are FITTED against the PLACEHOLDER tactical
/// rules — not gameplay values; they move whenever a tactical placeholder moves (re-fit, never hand-tune).
/// [P] Two models live here, selected by <see cref="ModelVersion"/>: 1 = the Sprint 1 (Slice E) model exactly as fitted on
/// 2026-11-04 (<see cref="Sprint1"/>, kept bit-exact for comparison and its goldens); 2 = the lane-P terrain model (the
/// defaults, fitted on the lane-P TUNING cases only — tuning scenarios x seeds 1-80 — by ParityCalibrationP, H-P.md §4;
/// FROZEN before the acceptance run). Every model-2 default below is that fit's value.
/// </summary>
public sealed record AutoCoefficients
{
    /// <summary>[P] 1 = Sprint 1 model (straight axis, cell concealment only, no terrain combat terms, legacy inactivity);
    /// 2 = lane-P model (terrain: path axis, Naismith climb, layered sight, slope / footing / cohesion / impetus in melee,
    /// elevation and cover in missile fire; Defend doctrine; special abilities; DIR5 inactivity port).</summary>
    public long ModelVersion { get; init; } = 2;
    // [P fix r1, P-V4(c)] The inactivity rule is NOT a coefficient: model 2 applies the rule of the battle's own
    // TacticalRules.Inactivity (as the TacticalResolver does), so one setup is never resolved under two rules. Model 1 (Sprint 1)
    // implements only the legacy side-disengage rule, whatever the setup says (kept bit-exact for comparison).

    /// <summary>Fraction (permille) of a formation's alive entities that strike in a melee step. FITTED.</summary>
    public long MeleeEngagePermille { get; init; } = 500;
    /// <summary>Frontage limit: at most this many attackers (permille) per alive entity of the target strike in melee — a
    /// formation cannot bring more entities into contact than the enemy line offers. FITTED.</summary>
    public long MaxAttackersPerTargetPermille { get; init; } = 500;
    /// <summary>Fraction (permille) of a ranged formation's free (not in melee) entities that get a shot in a step. FITTED.</summary>
    public long RangedEngagePermille { get; init; } = 1000;
    /// <summary>Casualties = floor(damage * KillEfficiency / (1000 * MaxHp)): damage spread over targets wounds before it
    /// kills. FITTED.</summary>
    public long KillEfficiencyPermille { get; init; } = 800;
    /// <summary>Per-battle per-side multiplicative perturbation amplitude (permille), counter-RNG. FITTED.</summary>
    public long FortunePermille { get; init; } = 200;
    /// <summary>Per-step per-formation multiplicative perturbation amplitude (permille), counter-RNG. FITTED.</summary>
    public long NoisePermille { get; init; } = 500;
    /// <summary>Steps after an ambusher's first contact during which its victims deal it no damage (surprise). FITTED.</summary>
    public long AmbushFirstStrikeSteps { get; init; } = 0;
    /// <summary>Fraction (permille) of a ROUTED formation's remaining entities recorded as Surrendered. FITTED.</summary>
    public long SurrenderPermille { get; init; } = 0;
    /// <summary>[P] Model 2: fraction (permille) of a HOLDING (Defend) formation's alive entities that strike in a melee step —
    /// a line holding its slots fights only where it is touched, while an attacking formation's entities leave their slots
    /// and converge (the tactical "many-on-one" of an Attack directive, H-TR §1.5 observation). FITTED.</summary>
    public long HoldEngagePermille { get; init; } = 200;
    /// <summary>[P] Model 2: frontage limit against a HOLDING (Defend) target — attackers per alive entity of a line that stays
    /// in its slots (attackers converge on it; the line does not spread to meet them). FITTED.</summary>
    public long MaxAttackersPerHolderPermille { get; init; } = 2000;
    /// <summary>[P] Model 2: WOUND BUFFER — damage a formation absorbs as wounds before it converts into deaths, as a share
    /// (permille) of its start strength's hit points. In the tactical engine damage spreads over the entities in contact and
    /// kills only when one entity's HP is exhausted, so a weaker attack wounds many before it kills few (deaths are convex in
    /// damage); the Sprint 1 linear conversion (0 here) ignored that. dead = floor((damage x KillEff + phase - buffer) /
    /// MaxHp), never negative — still monotone in damage. FITTED.</summary>
    public long WoundBufferPermille { get; init; } = 0;
    /// <summary>[P] Model 2: CONVERGENCE MOBILITY — weight w (permille) of terrain mobility in the frontage limit against a
    /// holding line: attackers converge on a holder by moving around it in the contact zone, which terrain slows (class
    /// cost and the Naismith climb toward the holder's cell, lane TR's movement terms). The per-holder cap is multiplied by
    /// (1000 - w + w x m)/1000 with m = 1000 / (cost + climb) in permille of open ground. 0 = off. FITTED.</summary>
    public long ConvergenceMobilityPermille { get; init; } = 1000;
    /// <summary>[P] Model 2: FIRE SATURATION — at most this many effective shots (permille) per EXPOSED target entity (the
    /// target's front rank: min(alive, FormationColumns)) per volley. Tactical shooters each take the nearest tracked enemy
    /// entity, so a large volley concentrates on the front rank and overkills it, while a small one (a sniper team) does not.
    /// 1,000,000 = no cap. FITTED.</summary>
    public long RangedPerExposedPermille { get; init; } = 3000;

    /// <summary>Doctrine assumption (NOT fitted): a formation withdraws when its morale is below this at a pulse start —
    /// the battery's controller <see cref="KnowledgeAi.RetreatMorale"/> default 450 (PLACEHOLDER there).</summary>
    public long DoctrineRetreatMorale { get; init; } = 450;
    /// <summary>Doctrine assumption (NOT fitted): an Ambush-posture formation springs when a known enemy is within this many
    /// cells — <see cref="KnowledgeAi.AmbushTriggerCells"/> default 3 (PLACEHOLDER there).</summary>
    public long DoctrineAmbushTriggerCells { get; init; } = 3;

    /// <summary>[P] The Sprint 1 (Slice E) model exactly as fitted on 2026-11-04 (parity-report.md §3): kept so the old model
    /// can be measured on the lane-P battery next to the new one, and so its goldens stay pinned bit for bit.</summary>
    public static AutoCoefficients Sprint1 { get; } = new()
    {
        ModelVersion = 1,
        MeleeEngagePermille = 500, MaxAttackersPerTargetPermille = 500, RangedEngagePermille = 300, KillEfficiencyPermille = 600,
        FortunePermille = 200, NoisePermille = 0, AmbushFirstStrikeSteps = 0, SurrenderPermille = 50, HoldEngagePermille = 500,
        MaxAttackersPerHolderPermille = 500, WoundBufferPermille = 0, ConvergenceMobilityPermille = 0, RangedPerExposedPermille = 1_000_000,
    };

    public void Validate()
    {
        if (ModelVersion != 1 && ModelVersion != 2) throw new ArgumentOutOfRangeException(nameof(ModelVersion));
        if (MeleeEngagePermille < 1 || MeleeEngagePermille > 1000) throw new ArgumentOutOfRangeException(nameof(MeleeEngagePermille));
        if (HoldEngagePermille < 1 || HoldEngagePermille > 1000) throw new ArgumentOutOfRangeException(nameof(HoldEngagePermille));
        if (RangedEngagePermille < 1 || RangedEngagePermille > 1000) throw new ArgumentOutOfRangeException(nameof(RangedEngagePermille));
        if (MaxAttackersPerTargetPermille < 1 || MaxAttackersPerTargetPermille > 1_000_000) throw new ArgumentOutOfRangeException(nameof(MaxAttackersPerTargetPermille));
        if (MaxAttackersPerHolderPermille < 1 || MaxAttackersPerHolderPermille > 1_000_000) throw new ArgumentOutOfRangeException(nameof(MaxAttackersPerHolderPermille));
        if (KillEfficiencyPermille < 1 || KillEfficiencyPermille > 1000) throw new ArgumentOutOfRangeException(nameof(KillEfficiencyPermille));
        if (WoundBufferPermille < 0 || WoundBufferPermille > 1000) throw new ArgumentOutOfRangeException(nameof(WoundBufferPermille));
        if (ConvergenceMobilityPermille < 0 || ConvergenceMobilityPermille > 1000) throw new ArgumentOutOfRangeException(nameof(ConvergenceMobilityPermille));
        if (RangedPerExposedPermille < 1 || RangedPerExposedPermille > 1_000_000) throw new ArgumentOutOfRangeException(nameof(RangedPerExposedPermille));
        if (ModelVersion < 2 && ConvergenceMobilityPermille != 0) throw new ArgumentOutOfRangeException(nameof(ConvergenceMobilityPermille), "model 1 has no convergence mobility");
        if (ModelVersion < 2 && WoundBufferPermille != 0) throw new ArgumentOutOfRangeException(nameof(WoundBufferPermille), "model 1 has no wound buffer");
        if (FortunePermille < 0 || NoisePermille < 0 || FortunePermille + NoisePermille > 900)
            throw new ArgumentOutOfRangeException(nameof(NoisePermille), "fortune + noise must stay <= 900 permille (every contact step must deal positive damage: termination)");
        if (AmbushFirstStrikeSteps < 0 || AmbushFirstStrikeSteps > 1000) throw new ArgumentOutOfRangeException(nameof(AmbushFirstStrikeSteps));
        if (SurrenderPermille < 0 || SurrenderPermille > 1000) throw new ArgumentOutOfRangeException(nameof(SurrenderPermille));
        if (DoctrineRetreatMorale < 0 || DoctrineAmbushTriggerCells < 0) throw new ArgumentOutOfRangeException(nameof(DoctrineRetreatMorale));
    }
}

/// <summary>Counter-RNG stream ids of the AutoResolver (distinct from <see cref="TacticalStreams"/>).</summary>
public static class AutoStreams
{
    public const uint Fortune = 0x7AC1_0101;
    public const uint Noise = 0x7AC1_0102;
    public const uint Detection = 0x7AC1_0103;
    public const uint KillPhase = 0x7AC1_0104;
}

/// <summary>Per-step record of one formation's decision (inspection and tests).</summary>
/// <param name="Target">Formation id it attacked this step, or -1.</param>
/// <param name="Mode">0 none, 1 melee, 2 ranged, 3 melee + ranged.</param>
public readonly record struct AutoStepRecord(long Step, long FormationId, long PosMm, bool Moved, long Target, int Mode,
                                             long DamageDealtMilli, long Alive, long Morale);

/// <summary>[P fix r1, P-V5] One model-2 detection draw (inspection and tests; only when RecordLog): the per-entity chance, the
/// number of LIVING target entities it is aggregated over, the resulting chance 1 - (1 - p)^n and the outcome.</summary>
public readonly record struct AutoDetectionDraw(long Step, int Side, long TargetId, Q32 PerEntity, long Entities, Q32 Chance, bool Detected);

/// <summary>[P] Terrain terms of one AutoResolver attack (model 2; inspection and tests). Factors are the lane-TR
/// <see cref="TerrainCombat"/> factors evaluated at the two formations' current points. [P fix r2, P2-V5]
/// <paramref name="PerTargetPermille"/>: the frontage limit this melee attack was capped by (attackers per alive target entity,
/// permille — on a Defend line the convergence-mobility cap of the attacker's terrain and climb toward the holder); 0 for a
/// missile attack.</summary>
public readonly record struct AutoTerrainTerms(long Step, long AttackerId, long TargetId, int Mode, Q32 AttackFactor, Q32 DefenceFactor,
                                               Q32 Cover, Q32 HitChance, long GradientRaw, Vec2L AttackerPos, Vec2L TargetPos,
                                               long PerTargetPermille = 0);

/// <summary>
/// The AutoResolver (Slice E; spec §5; O-16 delegated; lane P): a SEPARATE, formation-level, integer model. It reads the same
/// <see cref="BattleSetup"/> as the tactical engine and produces the same <see cref="BattleResult"/> shape plus a duration in
/// pulses. It ignores the controllers: it assumes the doctrine of the battery's controller (<see cref="KnowledgeAi"/>:
/// advance on objectives, attack the nearest known enemy, ambush posture waits, [P] defend posture holds, withdraw below a
/// morale) — stated in <see cref="AutoCoefficients"/>. Domain: LAND only. Vessels, aircraft, land formations with domain
/// weapons (crew-served guns) or starting embarked are REFUSED (NotSupported): no auto model of them exists, so a battle
/// containing them must go to the TacticalResolver (<see cref="ResolverPolicy"/>; naval never defaults to auto, DIR5 §2.1).
///
/// MODEL (one abstract round = one tactical step of StepMs; Lanchester-type exchange on a 1-D battle axis):
///  - AXIS: model 1 — the straight segment from the mean anchor of side 0 to that of side 1, each formation a point x (mm)
///    on it with its own lateral offset. [P] Model 2 — the least-cost ROUTE between the two mean anchors
///    (<see cref="AutoAxis"/>: the tactical planner with the Naismith climb), each formation a point at arc length x on it.
///  - FACTORS per formation: count; profile melee/ranged/defence/HP/range/sight/speed; experience, supply (attack) and
///    leadership (morale loss) through <see cref="ConditionMath"/> (the SAME equations as the tactical engine); starting
///    morale; posture; composition (ranged formations fire from range while melee closes; in melee only the free share
///    keeps shooting); terrain at its current point (below).
///  - TERRAIN (model 1): movement cost (speed / class cost) and concealment of the target's cell. [P] TERRAIN (model 2, the
///    lane-TR coefficients evaluated at the formations' points — TerrainEffects is the SAME table the tactical engine uses):
///    movement speed / (class cost + Naismith climb per mm toward the point one cell ahead on the axis); detection by the
///    tactical sight query (<see cref="TerrainSight.Query"/>: canopy layers, elevation, buildings) from each observer
///    formation's point to the target's point at eye height, best clarity, times the stationary factor and the target's
///    personal concealment as in the tactical rule; melee hit chance <see cref="TerrainCombat.HitChance"/> with the attack
///    factor S(gradient cell(a) -&gt; cell(t)) · impetus(advance this step) · footing(cell a) · cohesion(a) and the defence
///    factor footing(cell t) · cohesion(t), cohesion = c0 + (1 - c0) · OrderCap(class of its cell); missile hit chance with
///    the elevation factor 1 + gamma · gradient(cell t -&gt; cell a) and the cover transmission of
///    <see cref="TerrainSight.FirePath"/> (an opaque structure on the way blocks the shot: not a valid shot).
///    On flat Road/Open ground every model-2 terrain factor is exactly One (the lane-TR identity guarantee).
///  - STEP k (all decisions from the pre-step state, damage applied simultaneously):
///      1. pulse start: doctrine withdrawal of formations with morale &lt; DoctrineRetreatMorale (they leave the fight and
///         escape after their withdrawal time to the exit edge at catch-up speed — model 1: the straight-line distance at the
///         current cell's cost; [P] model 2: the exit field's least-cost distance, climb included). A formation with no
///         passable path to its edge does not withdraw;
///      2. movement: a fighting formation not in contact moves toward the enemy (clamped at contact) when its side knows an
///         enemy (Attack; ranged stops inside range of a known target; ambush waits until a known enemy is within the
///         trigger; [P] model 2: a Defend formation never advances, and a melee formation within the tactical engage radius
///         of a known enemy closes at catch-up pace, as the tactical entities leave their slots to engage) or, knowing none,
///         toward its nearest objective (Normal posture only);
///      3. detection: per (side, unknown enemy formation) within an observer's sight, ONE counter-RNG draw; contact reveals;
///      4. combat: each fighting formation attacks its nearest enemy by (signed gap, id): melee in contact with the engaged
///         share ([P] model 2: HoldEngagePermille for a Defend formation) capped by the frontage limit (attackers per alive
///         target entity), ranged at a known target in range when it did not move ([P] model 2: at most one volley per
///         FireIntervalSteps, the lane-N fire interval); expected damage = shots * hit * dmg(att') in milli-HP (the tactical
///         damage formula), times (1000 + fortune + noise)/1000;
///      5. casualties floor((damage*KillEff + phase)/(1e6*MaxHp)) (milli-HP; per-formation counter-RNG phase), morale loss
///         via ConditionMath, rout below RoutThreshold (a routed formation with no passable path to its edge surrenders
///         entirely; otherwise the fitted SurrenderPermille share);
///      6. termination: a side with no Active entities ends the battle (withdrawing formations stay Active until they
///         escape); at a pulse end the inactivity rule — model 1 always, and [P] model 2 when the setup's
///         TacticalRules.Inactivity = LegacySideDisengage: the Sprint 1 five-pulse side-disengage rule; [P] model 2 when the
///         setup's rule is HiddenStationary (the default, as in the TacticalResolver): the DIR5 §2.6 hidden-stationary
///         predicate of <see cref="HiddenStationaryRule"/> per formation (hidden = no enemy formation currently TRACKS it —
///         tracking is kept while an observer has a visible line of sight within its sight or is in contact, lost otherwise,
///         re-acquired only by a new detection draw, as in the tactical engine; progress = moved, fought, took damage, a first
///         detection THIS formation made ([P fix r2] the observer in contact, else the first of the best clarity — was the
///         lowest-id formation of the side), [P fix r2] a new approach minimum (at least one cell) of the gap to the nearest
///         enemy its side tracks, whoever moved (tactical UpdateMinima); exemptions: (a) [P fix r2: only when the setup's
///         ActiveConcealmentRule is on, as in the tactical engine] concealed with an active enemy within reach, (b) Defend
///         posture (explicit Hold) or within one cell of an own objective, (c) Ambush posture still waiting) -&gt; ROUTED, and lane TR's whole-field
///         Standoff end (UNRATIFIED P-TR1) when no formation progressed for InactivityPulses pulses (a routed formation's
///         flight to its exit edge is progress, as the routing entities' movement is in the tactical engine).
///  - There is NO round cap: every round either consumes HP (every valid attack deals positive damage: model 1 checks it,
///    [P] model 2 floors a valid attack's expected damage at 1 milli-HP), closes a bounded gap, consumes a first detection,
///    or counts toward the inactivity rule.
/// </summary>
public sealed class AutoResolver : IBattleResolver
{
    public AutoResolver(AutoCoefficients? coefficients = null)
    {
        Coefficients = coefficients ?? new AutoCoefficients();
        Coefficients.Validate();
    }

    public AutoCoefficients Coefficients { get; }
    public ResolverKind Kind => ResolverKind.Auto;

    public BattleResult Resolve(BattleSetup setup, IFormationController side0, IFormationController side1)
        => new AutoBattle(setup, Coefficients).Run();
}

/// <summary>One AutoResolver battle (see <see cref="AutoResolver"/>). Integer only; arrays in ascending formation id.</summary>
public sealed class AutoBattle
{
    private sealed class AF
    {
        public long Id; public int Side; public FormationSpec Spec = null!; public UnitProfile P = null!;
        public long Start, Killed, Surrendered; public long DamageMilli; public long Morale;
        public long EffMelee, EffRanged, EffDefence, Speed;
        public Vec2L Anchor; public long X0, Moved; public long ObjX; public bool HasObj;
        public bool Routed, Withdrawing, Disengaged; public long EscapeStep = -1;
        public bool Triggered; public long FirstContactStep = -1;
        public bool MovedStep, FoughtStep, ActivePulse, CasualtiesPulse; public int Inactive;
        public long DealtMilli, TakenMilli, Attacks;
        /// <summary>Counter-RNG phase in [0, 1e6 * MaxHp) of the casualty staircase (see step 5).</summary>
        public long KillPhase;
        // ---- [P] model 2
        public long AdvanceStep;               // mm advanced in this step (charge impetus)
        public long NextFireStep;              // fire interval: earliest step of the next volley
        public long LastFoughtStep = -1;       // the step of its last attack: exposure and personal concealment (tactical RevealUntil)
        public long RoutStep = -1, FleeUntilStep = -1;   // [P fix r1, P-V4(b)] rout instant and the last step of its flight to the edge
        /// <summary>[P fix r2, P2-V1/P2-V2] Model 2: inactivity-rule progress that is neither movement nor combat — the first
        /// detection THIS formation made (tactical: the observer entity's Primary) and a new approach minimum to the nearest
        /// enemy its side tracks (tactical UpdateMinima). Read only by the inactivity rules, never by the morale recovery
        /// (the tactical Hold recovery depends on casualties only).</summary>
        public bool ProgressPulse;
        /// <summary>[P fix r2, P2-V2] Running minimum of |gap| to the nearest enemy formation its side TRACKS (-1 until the
        /// first measurement; never reset): the tactical BestEnemy potential.</summary>
        public long BestEnemy = -1;
        /// <summary>[P] Model 2: the standing order the doctrine gave at the last pulse start (the controller decides only at
        /// pulse starts): 0 none (standing Hold), 1 MoveTo objective, 2 Attack, 3 ambush wait (Hold), 4 Defend (explicit Hold).</summary>
        public int Order;
        public long Idle;                      // DIR5 hidden-stationary counter
        public Vec2L[] Objectives = Array.Empty<Vec2L>();
        public long Alive => Start - Killed - Surrendered;
        public bool Fighting => !Routed && !Withdrawing && !Disengaged && Alive > 0;
        public bool OnField(long step) => Alive > 0 && !Routed && !Disengaged && (!Withdrawing || step < EscapeStep);
    }

    private readonly BattleSetup _s;
    private readonly TacticalRules _r;
    private readonly AutoCoefficients _c;
    private readonly bool _v2;
    private readonly TerrainEffects _fx;
    private readonly AF[] _f;
    private readonly Vec2L _origin, _axis;
    private readonly long _axisLen;
    private readonly AutoAxis? _path;
    private readonly EdgeField[] _edge;
    private readonly long _contactMm, _spp;
    private readonly long[] _fortune = new long[2];
    private readonly bool[,] _known;
    private readonly bool[,] _tracked;      // [P fix r1, P-V4(a)] model 2: the side CURRENTLY tracks the formation (tactical Tracked)
    private readonly long[,] _firstKnown;
    private readonly List<AutoStepRecord> _log = new();
    private readonly List<AutoTerrainTerms> _terms = new();
    private readonly List<AutoDetectionDraw> _draws = new();
    private readonly List<InactivityVerdict> _verdicts = new();
    // [MOR] "the fight it won" (TacticalRules.LocalWinMoraleRise), as in the tactical engine: the step formation i last dealt damage
    // to formation j, at [i * n + j] (long.MinValue = never); null when the rule is off (coefficient 0: the exact identity).
    private readonly long[]? _lastHitStep;
    private readonly List<LocalWinRise> _localWins = new();
    private readonly IncrementalHash _trace = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] _buf = new byte[8];
    private long _steps;
    private long _fieldIdle;

    public AutoBattle(BattleSetup setup, AutoCoefficients coefficients)
    {
        _s = setup; _r = setup.Rules; _c = coefficients;
        _r.Validate(); _c.Validate();
        _v2 = _c.ModelVersion >= 2;
        _fx = setup.Effects ?? throw new ArgumentNullException(nameof(setup), "terrain effects");
        // Termination (verify-E E-V1): the smallest possible attack (1 shot, damage 1 HP, hit HitMin, lowest perturbation factor)
        // must deal positive expected milli-HP in this integer model.
        if (FixedMath.MulDivFloor(FixedMath.MulDivFloor(1000, _r.HitMin.Raw, Q32.OneRaw), 1000 - _c.FortunePermille - _c.NoisePermille, 1000) <= 0)
            throw new ArgumentException("rules.HitMin is too small for the AutoResolver: a minimal attack would deal 0 milli-HP (no termination)");
        if (setup.Sides.Length != 2) throw new ArgumentException("exactly two sides");
        EqualSides.ValidateRoles(setup);                                                           // [AP] DA6 / DA7 roles
        var specs = (FormationSpec[])setup.Formations.Clone();
        Array.Sort(specs, static (a, b) => a.Id.CompareTo(b.Id));
        _f = new AF[specs.Length];
        long[] sx = new long[2], sy = new long[2], sn = new long[2];
        for (int i = 0; i < specs.Length; i++)
        {
            var s = specs[i];
            if (i > 0 && s.Id == specs[i - 1].Id) throw new ArgumentException($"duplicate formation id {s.Id}");
            if (s.Side != 0 && s.Side != 1) throw new ArgumentException("side must be 0 or 1");
            if (s.Domain != Domain.Land) throw new NotSupportedException($"formation {s.Id}: the AutoResolver is calibrated for Land only ({s.Domain} auto model not built)");
            // [P] Land formations with crew-served domain weapons or starting aboard a transport belong to lane N's combined-arms
            // layer, which has no auto model: refuse them instead of silently ignoring their guns or their transport.
            if (s.Weapons is { Length: > 0 }) throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"formation {s.Id}: domain weapons have no AutoResolver model (TacticalResolver only)"));
            if (s.EmbarkedOn is not null) throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"formation {s.Id}: embarked formations have no AutoResolver model (TacticalResolver only)"));
            string? capErr = setup.Caps.Check(s.Profile.CapFamily, s.Count);
            if (capErr is not null) throw new ArgumentException($"formation {s.Id}: {capErr}");
            ConditionMath.Validate(s.Experience, s.Leadership, s.Supply);
            var f = new AF
            {
                Id = s.Id, Side = s.Side, Spec = s, P = s.Profile, Start = s.Count, Morale = s.Morale ?? _r.MoraleMax,
                EffMelee = ConditionMath.Attack(s.Profile.Melee, s.Experience, s.Supply),
                EffRanged = ConditionMath.Attack(s.Profile.Ranged, s.Experience, s.Supply),
                EffDefence = ConditionMath.Defence(s.Profile.Defence, s.Experience),
                Speed = FixedMath.MulDivFloor(_r.RefFootSpeedMmPerS, s.Profile.SpeedNum, s.Profile.SpeedDen),
                Anchor = s.Anchor,
            };
            if (f.Morale < 0 || f.Morale > _r.MoraleMax) throw new ArgumentOutOfRangeException(nameof(setup), "morale");
            _f[i] = f;
            sx[s.Side] += s.Anchor.X; sy[s.Side] += s.Anchor.Y; sn[s.Side]++;
        }
        if (sn[0] == 0 || sn[1] == 0) throw new ArgumentException("each side needs at least one formation");
        _origin = new Vec2L(FixedMath.DivFloor(sx[0], sn[0]), FixedMath.DivFloor(sy[0], sn[0]));
        var c1 = new Vec2L(FixedMath.DivFloor(sx[1], sn[1]), FixedMath.DivFloor(sy[1], sn[1]));
        _axis = c1 - _origin;
        _axisLen = _axis.Length;
        if (_axisLen == 0) throw new ArgumentException("the two sides' mean anchors coincide: no battle axis");
        if (_v2) _path = AutoAxis.Build(setup.Grid, setup.Costs, _fx, _origin, c1);
        foreach (var f in _f)
        {
            f.X0 = Project(f.Anchor);
            var objs = setup.Sides[f.Side].Objectives;
            f.Objectives = objs;
            if (objs.Length > 0 && f.Spec.Posture == Posture.Normal)
            {
                // nearest objective by (distance², index) — composite key with the index tie-break
                int bi = 0; Int128 bd = Vec2L.DistanceSq(f.Anchor, objs[0]);
                for (int k = 1; k < objs.Length; k++) { Int128 d = Vec2L.DistanceSq(f.Anchor, objs[k]); if (d < bd) { bd = d; bi = k; } }
                f.ObjX = Project(objs[bi]); f.HasObj = true;
            }
        }
        _edge = _v2
            ? new[] { new EdgeField(setup.Grid, setup.Costs, setup.Sides[0].Exit, _fx), new EdgeField(setup.Grid, setup.Costs, setup.Sides[1].Exit, _fx) }
            : new[] { new EdgeField(setup.Grid, setup.Costs, setup.Sides[0].Exit), new EdgeField(setup.Grid, setup.Costs, setup.Sides[1].Exit) };
        _contactMm = 2 * _r.EntityRadiusMm + _r.ContactReachMm;
        _spp = _r.PulseMs / _r.StepMs;
        _known = new bool[2, _f.Length];
        _tracked = new bool[2, _f.Length];
        _firstKnown = new long[2, _f.Length];
        for (int side = 0; side < 2; side++) for (int j = 0; j < _f.Length; j++) _firstKnown[side, j] = -1;
        for (int side = 0; side < 2; side++)
            _fortune[side] = Uniform(AutoStreams.Fortune, side, 0, 0, _c.FortunePermille);
        foreach (var f in _f)
            f.KillPhase = (long)CounterRng.NextBelow(_s.Seed, AutoStreams.KillPhase, f.Id, 0, 0, 0, checked((ulong)(1_000_000 * f.P.MaxHp)));
        if (_r.LocalWinMoraleRise > 0)                                          // [MOR]
        {
            _lastHitStep = new long[checked(_f.Length * _f.Length)];
            Array.Fill(_lastHitStep, long.MinValue);
        }
    }

    /// <summary>Per-step decision log (off by default: the battery runs thousands of auto battles).</summary>
    public bool RecordLog { get; init; }
    public IReadOnlyList<AutoStepRecord> Log => _log;
    /// <summary>[MOR] Every "fight it won" rise, in execution order (empty while TacticalRules.LocalWinMoraleRise is 0). AtMs is the
    /// step's instant on the battle timeline (StartMs + step × StepMs), as in the tactical engine.</summary>
    public IReadOnlyList<LocalWinRise> LocalWinRises => _localWins;
    /// <summary>[P] Model 2: the terrain terms of every attack (only when <see cref="RecordLog"/>).</summary>
    public IReadOnlyList<AutoTerrainTerms> TerrainTerms => _terms;
    /// <summary>[P fix r1] Model 2: every detection draw (only when <see cref="RecordLog"/>).</summary>
    public IReadOnlyList<AutoDetectionDraw> DetectionDraws => _draws;
    /// <summary>[P] Model 2, DIR5 rule: the pulse-end judgements of idle formations (first of a stretch, exemption changes, routs).</summary>
    public IReadOnlyList<InactivityVerdict> InactivityVerdicts => _verdicts;
    /// <summary>[P] The step at which <paramref name="side"/> first knew the formation (detection or contact), or -1.</summary>
    public long FirstKnownStep(int side, long formationId)
    {
        for (int j = 0; j < _f.Length; j++) if (_f[j].Id == formationId) return _firstKnown[side, j];
        throw new ArgumentException("unknown formation", nameof(formationId));
    }

    /// <summary>[P] Model 2: the battle axis (null in model 1).</summary>
    public AutoAxis? Axis => _path;

    // ------------------------------------------------------------------ geometry

    private long Project(Vec2L p) => _path is not null ? _path.Project(p) : (long)(Vec2L.Dot(p - _origin, _axis) / _axisLen);

    private long X(AF f) => f.Side == 0 ? f.X0 + f.Moved : f.X0 - f.Moved;

    /// <summary>Current 2-D point. Model 1: the anchor displaced by Moved along the axis toward the enemy. [P] Model 2: the
    /// axis point at the formation's arc length.</summary>
    private Vec2L Pos(AF f)
    {
        if (_path is not null) return _path.At(X(f));
        long m = f.Side == 0 ? f.Moved : -f.Moved;
        return new Vec2L(f.Anchor.X + FixedMath.MulDivFloor(_axis.X, m, _axisLen), f.Anchor.Y + FixedMath.MulDivFloor(_axis.Y, m, _axisLen));
    }

    private CellCoord CellAt(Vec2L p)
    {
        var g = _s.Grid; var c = g.CellOf(p);
        return new CellCoord(Math.Clamp(c.X, 0, g.Width - 1), Math.Clamp(c.Y, 0, g.Height - 1));
    }

    private TerrainClass ClassAt(Vec2L p)
    {
        var c = CellAt(p);
        return _s.Grid.ClassAt(c.X, c.Y);
    }

    /// <summary>Signed gap between a side-0 and a side-1 formation along the axis (front distance; &lt;= contact = in contact).</summary>
    private long Gap(AF a, AF b) => a.Side == 0 ? X(b) - X(a) : X(a) - X(b);

    private long Uniform(uint stream, long a, long b, long c, long amp)
        => amp == 0 ? 0 : (long)CounterRng.NextBelow(_s.Seed, stream, a, b, c, 0, (ulong)(2 * amp + 1)) - amp;

    /// <summary>Nearest enemy formation by composite key (signed gap, formation id), among fighting ones (and known ones when
    /// <paramref name="knownOnly"/>). -1 when none.</summary>
    private int Nearest(int fi, bool knownOnly, out long gap)
    {
        var f = _f[fi];
        int best = -1; gap = 0;
        for (int j = 0; j < _f.Length; j++)
        {
            var g = _f[j];
            if (g.Side == f.Side || !g.Fighting) continue;
            if (knownOnly && !_known[f.Side, j]) continue;
            long d = Gap(f, g);
            if (best < 0 || d < gap) { best = j; gap = d; }     // ascending j + strict '<' keeps the lower id on ties
        }
        return best;
    }

    private Q32 HitChance(long attack, long defence)
    {
        Q32 p = attack + defence <= 0 ? Q32.Zero : Q32.FromRatio(attack, attack + defence);
        if (p < _r.HitMin) p = _r.HitMin;
        if (p > _r.HitMax) p = _r.HitMax;
        return p;
    }

    private long DamageOf(long attack) => Math.Max(1, attack / _r.DamageDivisor);

    /// <summary>Expected milli-HP of <paramref name="shots"/> attacks at (attack, defence), with the perturbation factor.
    /// Like the tactical engine, an effective attack of 0 still hits at HitMin for max(1, 0) = 1 HP (verify-E E-V1/E-V4: the
    /// former 0 for attack 0 broke parity and let two zero-attack formations "fight" forever). Hence every attack deals
    /// at least floor(floor(1000 x HitMin) x 100 / 1000) &gt; 0 milli-HP (Validate keeps fortune + noise &lt;= 900): the
    /// termination argument below rests on it, and it is checked.</summary>
    private long ExpectedMilli(long shots, long attack, long defence, long factorPermille)
        => ExpectedMilliAt(shots, attack, HitChance(attack, defence), factorPermille);

    /// <summary>Expected milli-HP of <paramref name="shots"/> attacks of the given hit chance. Model 1 throws on a zero (its
    /// termination check); [P] model 2 floors a valid attack at 1 milli-HP (a missile through dense cover can have an expected
    /// value below one milli-HP; it still consumes HP, as every tactical attack can).</summary>
    private long ExpectedMilliAt(long shots, long attack, Q32 hit, long factorPermille)
    {
        if (shots <= 0) return 0;
        return ValidAttackMilli(ExpectedMilliOf(shots, DamageOf(attack), hit, factorPermille), _c.ModelVersion);
    }

    /// <summary>Expected milli-HP of <paramref name="shots"/> attacks of <paramref name="damagePerHit"/> HP at hit chance
    /// <paramref name="hit"/>, times factor/1000 (each product floored).</summary>
    public static long ExpectedMilliOf(long shots, long damagePerHit, Q32 hit, long factorPermille)
    {
        if (shots <= 0) return 0;
        long expected = FixedMath.MulDivFloor(checked(shots * checked(damagePerHit * 1000)), hit.Raw, Q32.OneRaw);
        return FixedMath.MulDivFloor(expected, factorPermille, 1000);
    }

    /// <summary>The termination guard of a VALID attack's expected damage: model 1 throws on a non-positive value (its
    /// checked invariant); [P] model 2 floors it at 1 milli-HP.</summary>
    public static long ValidAttackMilli(long expectedMilli, long modelVersion)
    {
        if (expectedMilli > 0) return expectedMilli;
        if (modelVersion >= 2) return 1;
        throw new InvalidOperationException("termination invariant violated: an attack dealt no damage");
    }

    private static long Share(long n, long permille) => n <= 0 ? 0 : Math.Max(1, FixedMath.MulDivFloor(n, permille, 1000));

    /// <summary>Deaths of a formation from its accumulated damage (step 5): floor(max(0, damage x KillEff + phase - buffer) /
    /// (1e6 x MaxHp)), buffer = woundBuffer x start x MaxHp x 1000 ([P] model 2's wound buffer; 0 = the Sprint 1 formula).
    /// Monotone non-decreasing in damage.</summary>
    public static long DeadOf(long damageMilli, long killEffPermille, long phase, long woundBufferPermille, long start, long maxHp)
    {
        Int128 conv = (Int128)damageMilli * killEffPermille + phase;
        if (woundBufferPermille > 0) conv = Int128.Max(0, conv - (Int128)woundBufferPermille * start * maxHp * 1000);
        return (long)(conv / checked(1_000_000 * maxHp));
    }

    /// <summary>[P] The convergence-mobility frontage limit: cap x (1000 - w + w x m)/1000 with m = min(1000, 10^6 / cost),
    /// cost = class cost of the attacker's cell + the climb toward the holder's cell (lane TR's movement terms); at least 1.</summary>
    public static long ConvergenceCapOf(long capPermille, long w, long costWithClimb)
    {
        if (w == 0) return capPermille;
        long m = Math.Min(1000, FixedMath.MulDivFloor(1000, 1000, costWithClimb));
        return Math.Max(1, FixedMath.MulDivFloor(capPermille, 1000 - w + FixedMath.MulDivFloor(w, m, 1000), 1000));
    }

    /// <summary>[P] Fire saturation: a volley's effective shots are at most perExposed (permille) per exposed target entity,
    /// exposed = min(target alive, front columns).</summary>
    public static long SaturatedShots(long shots, long targetAlive, long frontColumns, long perExposedPermille)
        => Math.Min(shots, Share(Math.Min(targetAlive, frontColumns), perExposedPermille));

    /// <summary>[P] 1 - (1 - p)^n in Q32 (exponentiation by squaring, each product floored): the chance that at least one of
    /// n independent per-entity draws of chance p succeeds. p for n &lt;= 1; One for p = One.</summary>
    public static Q32 AnyOf(Q32 p, long n)
    {
        if (n <= 1 || p == Q32.One || p == Q32.Zero) return p;
        Q32 q = p.Complement(), r = Q32.One;
        long e = n;
        while (e > 0)
        {
            if ((e & 1) != 0) r = r.Mul(q);
            q = q.Mul(q);
            e >>= 1;
        }
        return r.Complement();
    }

    // ------------------------------------------------------------------ [P] model-2 terrain terms

    /// <summary>[P] Melee hit chance of attacker a on target t (TerrainCombat, lane-TR coefficients at the two points).</summary>
    private Q32 MeleeHitV2(AF a, AF t, long k, out Q32 fA, out Q32 fD, out long g)
    {
        Vec2L pa = Pos(a), pt = Pos(t);
        g = TerrainCombat.GradientRaw(_s.Grid, pa, pt, _fx.GradientCap);
        long refMm = FixedMath.MulDivFloor(checked(a.Speed * _r.CatchUpNum), _r.StepMs, checked(1000 * _r.CatchUpDen));
        Q32 imp = TerrainCombat.ImpetusFactor(a.P.Impetus, a.AdvanceStep, refMm);
        Q32 cohA = _fx.OrderCap(ClassAt(pa)), cohT = _fx.OrderCap(ClassAt(pt));
        fA = TerrainCombat.MeleeAttackFactor(_fx, g, imp, ClassAt(pa), cohA);
        fD = TerrainCombat.MeleeDefenceFactor(_fx, ClassAt(pt), cohT);
        return TerrainCombat.HitChance(a.EffMelee, fA, t.EffDefence, fD, _r.HitMin, _r.HitMax);
    }

    /// <summary>[P] Missile hit chance of shooter a on target t: elevation factor and the cover the shot passes; false when an
    /// opaque structure blocks the shot (not a valid shot).</summary>
    private bool MissileHitV2(AF a, AF t, out Q32 p, out Q32 e, out Q32 cover, out long g)
    {
        Vec2L pa = Pos(a), pt = Pos(t);
        var fp = TerrainSight.FirePath(_s.Grid, _fx, pa, _r.MuzzleHeightMm, pt, _r.AimHeightMm, pt, 1, 1);
        g = TerrainCombat.GradientRaw(_s.Grid, pt, pa, _fx.GradientCap);   // > 0: shooter higher (downhill shot)
        e = TerrainCombat.MissileElevationFactor(_fx, g);
        cover = fp.Transmission;
        p = Q32.Zero;
        if (fp.Blocked) return false;
        p = TerrainCombat.HitChance(a.EffRanged, e, t.EffDefence, Q32.One, _r.HitMin, _r.HitMax);
        if (cover != Q32.One) p = p.Mul(cover);
        return true;
    }

    /// <summary>[P] Model 2 frontage limit (attackers per alive holder entity, permille) of attacker f on the holding line g:
    /// MaxAttackersPerHolder x (1000 - w + w x m)/1000, m = 1,000,000 / (class cost of f's cell + climb toward g's cell).</summary>
    private long ConvergenceCap(AF f, AF g)
    {
        long cap = _c.MaxAttackersPerHolderPermille, w = _c.ConvergenceMobilityPermille;
        if (w == 0) return cap;
        Vec2L pf = Pos(f), pg = Pos(g);
        CellCoord cf = CellAt(pf), cg = CellAt(pg);
        long cost = _s.Costs.Cost(_s.Grid.ClassAt(cf.X, cf.Y));
        if (cost == TerrainCostTable.Impassable) cost = TerrainCostTable.MaxCost;
        if (cf != cg)
        {
            long dh = _s.Grid.HeightAt(cg.X, cg.Y) - _s.Grid.HeightAt(cf.X, cf.Y);
            long run = Vec2L.Distance(_s.Grid.CellCenter(cf.X, cf.Y), _s.Grid.CellCenter(cg.X, cg.Y));
            cost = checked(cost + _fx.ClimbCostPerMm(dh, run));
        }
        return ConvergenceCapOf(cap, w, cost);
    }

    /// <summary>[P] Naismith climb cost per mm for a formation moving from its point toward the point one cell ahead on the
    /// axis (the tactical ClimbPerMm on the segment ahead); 0 on the flat or downhill.</summary>
    private long ClimbAhead(AF f)
    {
        if (_fx.ClimbNum == 0) return 0;
        long S = _s.Grid.CellSizeMm;
        Vec2L p = Pos(f);
        Vec2L ahead = _path!.At(f.Side == 0 ? X(f) + S : X(f) - S);
        CellCoord c0 = CellAt(p), c1 = CellAt(ahead);
        if (c0 == c1) return 0;
        long dh = _s.Grid.HeightAt(c1.X, c1.Y) - _s.Grid.HeightAt(c0.X, c0.Y);
        if (dh <= 0) return 0;
        long run = Vec2L.Distance(_s.Grid.CellCenter(c0.X, c0.Y), _s.Grid.CellCenter(c1.X, c1.Y));
        return _fx.ClimbCostPerMm(dh, run);
    }

    // ------------------------------------------------------------------ run

    public BattleResult Run()
    {
        if (_steps != 0) throw new InvalidOperationException("already run");
        long[] pending = new long[_f.Length];
        int[] target = new int[_f.Length];
        int[] mode = new int[_f.Length];
        for (long k = 0; ; k++)
        {
            _steps = k + 1;
            long pulse = k / _spp;
            bool pulseStart = k % _spp == 0, pulseEnd = k % _spp == _spp - 1;
            if (pulseStart)
            {
                foreach (var f in _f) { f.ActivePulse = false; f.CasualtiesPulse = false; f.ProgressPulse = false; }
                foreach (var f in _f)
                    if (f.Fighting && f.Morale < _c.DoctrineRetreatMorale && !Trapped(f)) StartWithdrawal(f, k);   // no path: it cannot withdraw
                if (_v2) DecideOrders();
            }
            foreach (var f in _f) { f.MovedStep = false; f.FoughtStep = false; f.AdvanceStep = 0; }

            // 2. movement (decisions from the pre-step positions, applied together)
            long[] adv = new long[_f.Length];
            for (int i = 0; i < _f.Length; i++)
            {
                var f = _f[i];
                if (!f.Fighting) continue;
                if (_v2) { adv[i] = AdvanceV2(i); continue; }
                int any = Nearest(i, false, out long gapAny);
                if (any < 0) continue;
                if (gapAny <= _contactMm) continue;                                  // in contact: stand and fight
                int t = Nearest(i, true, out long gapKnown);
                bool move, sprint = false;
                long limit = gapAny - _contactMm;
                if (t >= 0)
                {
                    if (f.Spec.Posture == Posture.Ambush && !f.Triggered)
                    {
                        if (gapKnown <= _c.DoctrineAmbushTriggerCells * _s.Grid.CellSizeMm) f.Triggered = true;
                    }
                    bool waiting = f.Spec.Posture == Posture.Ambush && !f.Triggered;
                    bool firing = f.P.IsRanged && gapKnown <= f.P.RangeCells * _s.Grid.CellSizeMm;
                    move = !waiting && !firing;
                    // [P] model 2: melee entities within the engage radius of a known enemy leave their slots at catch-up pace
                    sprint = _v2 && !f.P.IsRanged && gapKnown <= _r.EngageRadiusCells * _s.Grid.CellSizeMm;
                }
                else
                {
                    long toObj = f.HasObj ? (f.Side == 0 ? f.ObjX - X(f) : X(f) - f.ObjX) : 0;
                    move = f.HasObj && toObj > 0;
                    if (move) limit = Math.Min(limit, toObj);
                }
                if (!move) continue;
                long cost = _s.Costs.Cost(ClassAt(Pos(f)));
                if (cost == TerrainCostTable.Impassable) continue;
                if (_v2) cost = checked(cost + ClimbAhead(f));
                long step = sprint
                    ? FixedMath.MulDivFloor(checked(f.Speed * _r.CatchUpNum), _r.StepMs, checked(cost * _r.CatchUpDen))
                    : FixedMath.MulDivFloor(f.Speed, _r.StepMs, cost);
                adv[i] = Math.Min(step, limit);
            }
            for (int i = 0; i < _f.Length; i++)
                if (adv[i] > 0) { _f[i].Moved += adv[i]; _f[i].MovedStep = true; _f[i].ActivePulse = true; _f[i].AdvanceStep = adv[i]; }
            if (_v2) UpdateApproachMinima(k);     // [P fix r2, P2-V2] after the move, from the tracking of the previous step (tactical order)

            // 3. detection. [P fix r1, P-V4(a)] Model 2 keeps TRACKING as the tactical engine does: a tracked formation stays
            //    tracked while an observer has a visible line of sight within its sight range or is in contact, and is LOST
            //    otherwise; an untracked one (never known, or lost) needs a detection draw. Only the first ever detection is a
            //    first detection (activity). Model 1 never loses a formation it knows (unchanged).
            for (int side = 0; side < 2; side++)
                for (int j = 0; j < _f.Length; j++)
                {
                    var g = _f[j];
                    if (g.Side == side || !g.OnField(k)) continue;
                    bool tracked = _v2 ? _tracked[side, j] : _known[side, j];
                    if (tracked && !_v2) continue;
                    bool inSight = false, contact = false;
                    Q32 best = Q32.Zero;
                    // [P fix r2, P2-V1] the observer that makes a detection (tactical Detect: the nearest observer in contact,
                    // else the first observer of the best clarity — ascending formation id, strict comparisons)
                    int contactObs = -1, bestObs = -1;
                    long contactD = 0;
                    for (int oi = 0; oi < _f.Length; oi++)
                    {
                        var o = _f[oi];
                        if (o.Side != side || !o.OnField(k)) continue;
                        long d = Gap(o, g);
                        if (d <= _contactMm)
                        {
                            long ad = Math.Abs(d);
                            if (!contact || ad < contactD) { contactD = ad; contactObs = oi; }
                            contact = true;
                        }
                        if (d > o.P.SightCells * _s.Grid.CellSizeMm) continue;
                        if (!_v2) { inSight = true; continue; }
                        // [P] model 2: the tactical sight query between the two points (layers, elevation, structures)
                        var los = TerrainSight.Query(_s.Grid, _s.Concealment, _fx, Pos(o), _r.EyeHeightMm, Pos(g), _r.EyeHeightMm);
                        if (!los.Visible) continue;
                        if (!inSight || los.Clarity > best) { best = los.Clarity; bestObs = oi; }
                        inSight = true;
                    }
                    if (tracked)
                    {
                        if (!inSight && !contact) _tracked[side, j] = false;          // lost: hidden again until re-detected
                        continue;
                    }
                    bool detect = contact;
                    if (!detect && inSight)
                    {
                        Q32 clarity = _v2 ? best : _s.Concealment.Multiplier(ClassAt(Pos(g)));
                        // [P] attacked in the last step (tactical RevealUntil). [P fix r1, P-V1] LastFoughtStep starts at -1, so the
                        // former test "LastFoughtStep == k - 1" revealed EVERY formation at step 0 (-1 == -1): never attacked = never revealed.
                        bool revealed = _v2 && g.LastFoughtStep >= 0 && g.LastFoughtStep == k - 1;
                        bool exposed = g.MovedStep || g.FoughtStep || revealed || clarity == Q32.One;
                        Q32 p = exposed ? clarity : clarity.Mul(_r.StationaryConcealment);
                        if (_v2 && g.P.PersonalConcealment != Q32.One && !revealed) p = p.Mul(g.P.PersonalConcealment);   // lane N snipers
                        Q32 perEntity = p;
                        long n = g.Alive;                    // [P] the tactical draw is per LIVING target ENTITY: P(any of n) = 1 - (1 - p)^n
                        if (_v2) p = AnyOf(perEntity, n);
                        detect = CounterRng.Chance(_s.Seed, AutoStreams.Detection, g.Id, k, side, 0, p);
                        if (_v2 && RecordLog) _draws.Add(new AutoDetectionDraw(k, side, g.Id, perEntity, n, p, detect));
                    }
                    if (!detect) continue;
                    if (_v2) _tracked[side, j] = true;
                    if (_known[side, j]) continue;                                    // [P fix r1] re-acquired: not a first detection
                    _known[side, j] = true;
                    _firstKnown[side, j] = k;
                    // first detection: [P fix r2, P2-V1] model 2 credits the observer that made it (tactical: that observer entity's
                    // Primary), as inactivity-rule progress only; model 1 (Sprint 1, unchanged) the lowest-id on-field formation
                    if (_v2) _f[contact ? contactObs : bestObs].ProgressPulse = true;
                    else foreach (var o in _f) if (o.Side == side && o.OnField(k)) { o.ActivePulse = true; break; }
                }

            // 4. combat from the pre-damage state
            for (int i = 0; i < _f.Length; i++)
            {
                pending[i] = 0; target[i] = -1; mode[i] = 0;
            }
            for (int i = 0; i < _f.Length; i++)
            {
                var f = _f[i];
                if (!f.Fighting) continue;
                long factor = 1000 + _fortune[f.Side] + Uniform(AutoStreams.Noise, f.Id, k, 0, _c.NoisePermille);
                int m = Nearest(i, false, out long gm);
                long alive = f.Alive;
                if (m >= 0 && gm <= _contactMm)
                {
                    var g = _f[m];
                    if (g.FirstContactStep < 0) g.FirstContactStep = k;
                    if (f.FirstContactStep < 0) f.FirstContactStep = k;
                    if (!_known[f.Side, m]) { _known[f.Side, m] = true; _firstKnown[f.Side, m] = k; }
                    if (_v2) _tracked[f.Side, m] = true;                                // contact tracks
                    bool surprised = g.Spec.Posture == Posture.Ambush && k < g.FirstContactStep + _c.AmbushFirstStrikeSteps;
                    long engage = _v2 && f.Spec.Posture == Posture.Defend ? _c.HoldEngagePermille : _c.MeleeEngagePermille;
                    long perTarget = _v2 && g.Spec.Posture == Posture.Defend ? ConvergenceCap(f, g) : _c.MaxAttackersPerTargetPermille;
                    long engaged = Math.Min(alive, Math.Min(Share(alive, engage), Share(g.Alive, perTarget)));
                    if (!surprised)
                    {
                        long dm;
                        if (_v2)
                        {
                            Q32 hit = MeleeHitV2(f, g, k, out Q32 fa, out Q32 fd, out long gr);
                            dm = ExpectedMilliAt(engaged, f.EffMelee, hit, factor);
                            if (RecordLog) _terms.Add(new AutoTerrainTerms(k, f.Id, g.Id, 1, fa, fd, Q32.One, hit, gr, Pos(f), Pos(g), perTarget));
                        }
                        else dm = ExpectedMilli(engaged, f.EffMelee, g.EffDefence, factor);
                        pending[m] = checked(pending[m] + dm); f.DealtMilli += dm; f.Attacks += engaged;
                        target[i] = m; mode[i] |= 1;
                        if (_lastHitStep is not null && dm > 0) _lastHitStep[i * _f.Length + m] = k;      // [MOR] f fought g
                    }
                    long free = alive - engaged;
                    if (f.P.IsRanged && free > 0 && !f.MovedStep && !surprised && CanFire(f, k) && FiringStance(f))
                    {
                        long shots = Share(free, _c.RangedEngagePermille);
                        long dr = RangedMilli(f, g, shots, factor, k);
                        if (dr > 0)
                        {
                            pending[m] = checked(pending[m] + dr); f.DealtMilli += dr; f.Attacks += shots;
                            target[i] = m; mode[i] |= 2; Fired(f, k);
                            if (_lastHitStep is not null) _lastHitStep[i * _f.Length + m] = k;            // [MOR]
                        }
                    }
                    if (mode[i] != 0) { f.FoughtStep = true; if (_v2) f.LastFoughtStep = k; }
                    continue;
                }
                if (f.P.IsRanged && !f.MovedStep && CanFire(f, k) && FiringStance(f))
                {
                    int t = Nearest(i, true, out long gt);
                    if (t >= 0 && gt <= f.P.RangeCells * _s.Grid.CellSizeMm)
                    {
                        long shots = Share(alive, _c.RangedEngagePermille);
                        long dr = RangedMilli(f, _f[t], shots, factor, k);
                        if (dr > 0)
                        {
                            pending[t] = checked(pending[t] + dr); f.DealtMilli += dr; f.Attacks += shots;
                            target[i] = t; mode[i] = 2; f.FoughtStep = true; Fired(f, k); f.LastFoughtStep = k;
                            if (_lastHitStep is not null) _lastHitStep[i * _f.Length + t] = k;            // [MOR]
                        }
                    }
                }
            }

            // 5. damage, casualties, morale, rout (simultaneous)
            bool[]? inFight = _lastHitStep is null ? null : InFight(k);    // [MOR] before this step's casualties and routs
            for (int j = 0; j < _f.Length; j++)
            {
                if (pending[j] == 0) continue;
                var g = _f[j];
                g.DamageMilli = checked(g.DamageMilli + pending[j]);
                g.TakenMilli += pending[j];
                g.ActivePulse = true;
                // Casualty staircase with a per-formation random phase: dead = floor((damage * KillEff + phase) / (1e6 * MaxHp)).
                // Monotone in damage, unbiased in expectation, and it de-synchronises the two sides' rout instants (without
                // the phase, equal formations crossed the rout threshold in the same step far more often than the tactical
                // engine's simultaneous routs).
                long dead = Math.Min(g.Start - g.Surrendered,
                    DeadOf(g.DamageMilli, _c.KillEfficiencyPermille, g.KillPhase, _v2 ? _c.WoundBufferPermille : 0, g.Start, g.P.MaxHp));
                long newDead = dead - g.Killed;
                if (newDead <= 0) continue;
                g.Killed = dead;
                g.CasualtiesPulse = true;
                g.Morale = Math.Max(0, g.Morale - ConditionMath.MoraleLoss(newDead, _r.MoraleLossFullStrength, g.Start, g.Spec.Leadership));
            }
            for (int i = 0; i < _f.Length; i++) if (_f[i].FoughtStep) _f[i].ActivePulse = true;
            foreach (var g in _f)
                if (g.Fighting && g.Morale < _r.RoutThreshold) Rout(g, k);
            if (inFight is not null) LocalWins(k, inFight);               // [MOR] after every rout of the step
            for (int i = 0; i < _f.Length; i++)
            {
                var f = _f[i];
                long pos = X(f), tid = target[i] >= 0 ? _f[target[i]].Id : -1;
                if (RecordLog) _log.Add(new AutoStepRecord(k, f.Id, pos, f.MovedStep, tid, mode[i], f.DealtMilli, f.Alive, f.Morale));
                L(k); L(f.Id); L(pos); L(tid); L(f.Alive); L(f.DamageMilli); L(f.Morale); L(f.Routed ? 1 : 0); L(f.Withdrawing ? 1 : 0);
            }

            // 6. termination: elimination (a side with no Active entity) after every step
            if (Eliminated(k, pulse, false, out var endE)) return endE!;

            if (pulseEnd)
            {
                // morale recovery (Hold with no casualties: a formation that neither moved nor fought this pulse)
                foreach (var f in _f)
                    if (f.Fighting && !f.CasualtiesPulse && !f.ActivePulse && !f.Withdrawing)
                        f.Morale = Math.Min(_r.MoraleMax, f.Morale + _r.HoldRecovery);
                if (_v2 && _r.Inactivity == InactivityModel.HiddenStationary)       // [P fix r1, P-V4(c)] the setup's rule
                {
                    var end = PulseEndHiddenStationary(k, pulse);
                    if (end is not null) return end;
                    continue;
                }
                // inactivity rule (Sprint 1: side disengage)
                bool[] act = new bool[_f.Length];
                for (int i = 0; i < _f.Length; i++)
                {
                    var f = _f[i];
                    if (!f.OnField(k)) continue;
                    bool a = f.ActivePulse || f.ProgressPulse || (f.Withdrawing && k < f.EscapeStep);
                    if (!a && _r.ActiveConcealmentRule && Concealed(i))
                        foreach (var e in _f)
                            if (e.Side != f.Side && e.OnField(k) && (e.ActivePulse || e.ProgressPulse) && Math.Abs(Gap(e, f)) <= e.P.SightCells * _s.Grid.CellSizeMm) { a = true; break; }
                    act[i] = a;
                }
                bool[] has = new bool[2], allIdle = { true, true };
                for (int i = 0; i < _f.Length; i++)
                {
                    var f = _f[i];
                    if (!f.OnField(k)) continue;
                    f.Inactive = act[i] ? 0 : checked(f.Inactive + 1);
                    has[f.Side] = true;
                    if (f.Inactive < _r.InactivityPulses) allIdle[f.Side] = false;
                }
                bool d0 = has[0] && allIdle[0], d1 = has[1] && allIdle[1];
                if (d0 || d1)
                {
                    foreach (var f in _f) if (f.OnField(k) && ((f.Side == 0 && d0) || (f.Side == 1 && d1))) f.Disengaged = true;
                    return End(k, pulse, d0 && d1 ? BattleEndReason.BothDisengaged : BattleEndReason.SideDisengaged, d0 && d1 ? -1 : (d0 ? 0 : 1), true);
                }
            }
        }
    }

    /// <summary>[MOR] Formations in the fight at step k: on the field and not routed (the tactical engine's "not routed, with an
    /// Active entity": a withdrawal still in transit is in the fight, as the tactical retreating entities are Active).</summary>
    private bool[] InFight(long k)
    {
        var a = new bool[_f.Length];
        for (int i = 0; i < _f.Length; i++) a[i] = _f[i].OnField(k);
        return a;
    }

    /// <summary>[MOR] "The fight it won", the rule the tactical engine applies (TacticalBattle.LocalWins), through the same shared
    /// implementation (<see cref="LocalWinMorale.Apply"/>), with time in steps: a formation in the fight before this step's casualties
    /// that left it now (routed, or no one left alive) broke; each formation still in the fight that dealt it damage within the last
    /// PulseMs (= steps per pulse × StepMs) gains the rise.</summary>
    private void LocalWins(long k, bool[] inFightBefore)
    {
        int n = _f.Length;
        var now = InFight(k);
        bool broke = false;
        for (int i = 0; i < n; i++) if (inFightBefore[i] && !now[i]) { broke = true; break; }
        if (!broke) return;
        var ids = new long[n]; var sides = new int[n]; var starts = new long[n]; var morale = new long[n];
        for (int i = 0; i < n; i++) { ids[i] = _f[i].Id; sides[i] = _f[i].Side; starts[i] = _f[i].Start; morale[i] = _f[i].Morale; }
        if (!LocalWinMorale.Apply(_r.LocalWinMoraleRise, _r.MoraleMax, _spp, k, checked(_s.StartMs + k * _r.StepMs), ids, sides, starts,
                                  inFightBefore, now, _lastHitStep, morale, _localWins)) return;
        for (int i = 0; i < n; i++) _f[i].Morale = morale[i];
    }

    private bool Eliminated(long k, long pulse, bool atPulseEnd, out BattleResult? result)
    {
        bool a0 = false, a1 = false;
        foreach (var f in _f) if (f.OnField(k + 1)) { if (f.Side == 0) a0 = true; else a1 = true; }
        result = null;
        if (a0 && a1) return false;
        result = End(k, pulse, (!a0 && !a1) ? BattleEndReason.BothEliminated : BattleEndReason.SideEliminated, (!a0 && !a1) ? -1 : (!a0 ? 0 : 1), atPulseEnd);
        return true;
    }

    private void Rout(AF g, long k)
    {
        g.Routed = true;
        // Spec §4 (as the tactical Resolve): a routing formation with no passable path to its exit edge surrenders
        // entirely (verify-E E-V3); otherwise the fitted share is recorded as Surrendered.
        bool trapped = Trapped(g);
        g.Surrendered = trapped ? g.Alive : FixedMath.MulDivFloor(g.Alive, _c.SurrenderPermille, 1000);
        // [P fix r1, P-V4(b)] model 2: the routed survivors flee to their exit edge for the withdrawal travel time; the flight is
        // field progress for the DIR5 Standoff counter (in the tactical engine the routing entities move and leave the field).
        // It changes nothing else: a routed formation is off the field for every other rule, as before.
        g.RoutStep = k;
        g.FleeUntilStep = _v2 && !trapped && g.Alive > 0 ? k + ExitSteps(g) : k;
    }

    /// <summary>[P fix r2, P2-V2] Model 2 port of the tactical UpdateMinima clause (a): per fighting formation, the running
    /// minimum of |gap| to the nearest on-field enemy formation its side currently TRACKS; a new minimum at least one cell below
    /// the last recorded one is progress (ProgressPulse), whoever moved — so a hidden formation that a tracked enemy is
    /// approaching is not idle. The first measurement only initialises; the minimum is never reset. Clauses (b) objective and
    /// (c) exit edge need no port: the auto counts every advance (and every flight / withdrawal in transit) as progress.</summary>
    private void UpdateApproachMinima(long k)
    {
        long S = _s.Grid.CellSizeMm;
        for (int i = 0; i < _f.Length; i++)
        {
            var f = _f[i];
            if (!f.Fighting) continue;
            long d = -1;
            for (int j = 0; j < _f.Length; j++)
            {
                var g = _f[j];
                if (g.Side == f.Side || !g.OnField(k) || !_tracked[f.Side, j]) continue;
                long gj = Math.Abs(Gap(f, g));
                if (d < 0 || gj < d) d = gj;
            }
            if (d < 0) continue;
            if (f.BestEnemy < 0) { f.BestEnemy = d; continue; }
            if (d <= f.BestEnemy - S) { f.BestEnemy = d; f.ProgressPulse = true; }
        }
    }

    /// <summary>[P fix r1, P-V4(b)] Model 2: the routed formation is still fleeing at the end step <paramref name="kEnd"/> of a pulse
    /// (routed before it, escapes after it) — the tactical engine counts the routing members' approach to their edge as
    /// progress while any of them is on the field at the pulse end (Primary on BestEdge improvement), and nothing once all have
    /// left. Same convention as a withdrawal in transit (k &lt; EscapeStep).</summary>
    private bool InFlightAtPulseEnd(AF f, long kEnd) => f.Routed && f.RoutStep >= 0 && f.RoutStep < kEnd && kEnd < f.FleeUntilStep;

    /// <summary>[P] Model 2: the KnowledgeAi decision at a pulse start, from the side's knowledge AT that instant (an enemy
    /// learned during a pulse is acted upon at the next pulse start, as in the tactical engine):
    /// Defend -&gt; explicit Hold; a known enemy -&gt; Attack, except an Ambush formation still waiting while every known
    /// enemy is beyond the trigger distance; no known enemy and an objective (Normal posture) -&gt; MoveTo; else standing Hold.</summary>
    private void DecideOrders()
    {
        for (int i = 0; i < _f.Length; i++)
        {
            var f = _f[i];
            if (!f.Fighting) continue;
            if (f.Spec.Posture == Posture.Defend) { f.Order = 4; continue; }
            int t = Nearest(i, true, out long gapKnown);
            if (t >= 0)
            {
                if (f.Spec.Posture == Posture.Ambush && !f.Triggered && gapKnown > _c.DoctrineAmbushTriggerCells * _s.Grid.CellSizeMm) { f.Order = 3; continue; }
                if (f.Spec.Posture == Posture.Ambush) f.Triggered = true;
                f.Order = 2;
                continue;
            }
            f.Order = f.HasObj ? 1 : f.Spec.Posture == Posture.Ambush ? 3 : 0;
        }
    }

    /// <summary>[P] Model 2 firing stance (tactical FiringStance: Attack, Hold or Rally): no volleys while marching on a
    /// MoveTo order.</summary>
    private bool FiringStance(AF f) => !_v2 || f.Order != 1;

    /// <summary>[P] Model 2 movement of formation i in this step (mm along the axis toward the enemy), from its standing order:
    /// MoveTo — toward the objective (clamped at the objective and at contact); Attack — toward the nearest known enemy
    /// (clamped at contact; a ranged formation stops with a known enemy in range; a melee formation within the engage radius
    /// of a known enemy closes at catch-up pace); Hold / wait / Defend — none. Speed / (class cost + Naismith climb ahead).</summary>
    private long AdvanceV2(int i)
    {
        var f = _f[i];
        if (f.Order != 1 && f.Order != 2) return 0;
        int any = Nearest(i, false, out long gapAny);
        if (any < 0 || gapAny <= _contactMm) return 0;                               // in contact: stand and fight
        long limit = gapAny - _contactMm;
        bool sprint = false;
        if (f.Order == 1)
        {
            long toObj = f.Side == 0 ? f.ObjX - X(f) : X(f) - f.ObjX;
            if (toObj <= 0) return 0;
            limit = Math.Min(limit, toObj);
        }
        else
        {
            int t = Nearest(i, true, out long gapKnown);
            if (t < 0) return 0;
            if (f.P.IsRanged && gapKnown <= f.P.RangeCells * _s.Grid.CellSizeMm) return 0;
            sprint = !f.P.IsRanged && gapKnown <= _r.EngageRadiusCells * _s.Grid.CellSizeMm;
        }
        long cost = _s.Costs.Cost(ClassAt(Pos(f)));
        if (cost == TerrainCostTable.Impassable) return 0;
        cost = checked(cost + ClimbAhead(f));
        long step = sprint
            ? FixedMath.MulDivFloor(checked(f.Speed * _r.CatchUpNum), _r.StepMs, checked(cost * _r.CatchUpDen))
            : FixedMath.MulDivFloor(f.Speed, _r.StepMs, cost);
        return Math.Min(step, limit);
    }

    /// <summary>[P] Model 2: lane N's fire interval (one volley per FireIntervalSteps steps); always true in model 1.</summary>
    private bool CanFire(AF f, long k) => !_v2 || k >= f.NextFireStep;

    private void Fired(AF f, long k)
    {
        if (!_v2) return;
        f.NextFireStep = k + Math.Max(1, f.P.FireIntervalSteps);
    }

    /// <summary>Expected milli-HP of a volley of <paramref name="shots"/> from f at g; 0 when (model 2) a structure blocks it.
    /// [P] Model 2 caps the effective shots at RangedPerExposed per exposed target entity (fire saturation).</summary>
    private long RangedMilli(AF f, AF g, long shots, long factor, long k)
    {
        if (!_v2) return ExpectedMilli(shots, f.EffRanged, g.EffDefence, factor);
        shots = SaturatedShots(shots, g.Alive, _r.FormationColumns, _c.RangedPerExposedPermille);
        if (!MissileHitV2(f, g, out Q32 p, out Q32 e, out Q32 cover, out long gr)) return 0;
        long r = ExpectedMilliAt(shots, f.EffRanged, p, factor);
        if (RecordLog) _terms.Add(new AutoTerrainTerms(k, f.Id, g.Id, 2, e, Q32.One, cover, p, gr, Pos(f), Pos(g)));
        return r;
    }

    // ------------------------------------------------------------------ [P] DIR5 hidden-stationary rule (model 2)

    /// <summary>Hidden from the enemy side: the enemy side does not CURRENTLY track it (tactical HiddenNow). [P fix r1, P-V4(a)]:
    /// was "never known", so a formation the enemy had once seen and then lost was never hidden again.</summary>
    private bool Hidden(int i) => !_tracked[1 - _f[i].Side, i];

    /// <summary>Concealed from enemy formation e: its cell conceals, it has personal concealment, or the tactical sight query
    /// from e's point is blocked or attenuated.</summary>
    private bool ConcealedFrom(AF f, AF e)
    {
        Vec2L pf = Pos(f);
        if (_s.Concealment.Multiplier(ClassAt(pf)) < Q32.One || f.P.PersonalConcealment < Q32.One) return true;
        var los = TerrainSight.Query(_s.Grid, _s.Concealment, _fx, Pos(e), _r.EyeHeightMm, pf, _r.EyeHeightMm);
        return !los.Visible || los.Clarity < Q32.One;
    }

    private InactivityExemption ExemptionsOf(int i, long k)
    {
        var f = _f[i];
        var ex = InactivityExemption.None;
        long S = _s.Grid.CellSizeMm;
        long w = f.P.IsRanged ? f.P.RangeCells * S : _contactMm;
        long reach = checked(w + FixedMath.MulDivFloor(f.Speed, _r.PulseMs, 1000));
        if (_r.ActiveConcealmentRule)                                    // [P fix r2, P2-V4] clause (a) only when the setup enables it
            foreach (var e in _f)
            {
                if (e.Side == f.Side || !e.Fighting) continue;
                if (Math.Abs(Gap(f, e)) <= reach && ConcealedFrom(f, e)) { ex |= InactivityExemption.ConcealedEnemyInReach; break; }
            }
        if (f.Spec.Posture == Posture.Defend) ex |= InactivityExemption.DefensiveHold;
        else
        {
            Vec2L p = Pos(f);
            foreach (var o in f.Objectives) if (Vec2L.DistanceSq(p, o) <= (Int128)S * S) { ex |= InactivityExemption.DefensiveHold; break; }
        }
        if (f.Spec.Posture == Posture.Ambush && !f.Triggered) ex |= InactivityExemption.AmbushWait;
        return ex;
    }

    /// <summary>
    /// DIR5 §2.6 per formation at the end of pulse p (the lane-TR predicate, <see cref="HiddenStationaryRule"/>): counter
    /// c = hidden AND no progress ? min(c + 1, N) : 0; ROUTED when c &gt;= N and no exemption. Then lane TR's whole-field
    /// Standoff (UNRATIFIED P-TR1): when no fighting formation progressed in each of the last N pulses and nobody routed now,
    /// the battle ends with nobody removed. The mutual guard (TacticalRules.MutualStandoffGuard, default OFF) is honoured.
    /// </summary>
    private BattleResult? PulseEndHiddenStationary(long k, long pulse)
    {
        int n = _r.InactivityPulses;
        bool anyProgress = false;
        var rout = new bool[_f.Length];
        int routs = 0;
        for (int i = 0; i < _f.Length; i++)
        {
            var f = _f[i];
            if (!f.Fighting) { f.Idle = 0; if (InFlightAtPulseEnd(f, k)) anyProgress = true; continue; }
            bool progress = f.ActivePulse || f.ProgressPulse;          // [P fix r2] + first-detection credit and approach minima
            if (progress) anyProgress = true;
            long before = f.Idle;
            f.Idle = HiddenStationaryRule.NextCounter(f.Idle, Hidden(i), progress, n);
            if (f.Idle < n) continue;
            var ex = ExemptionsOf(i, k);
            bool r = HiddenStationaryRule.Routs(f.Idle, n, ex);
            if (before < n || r) _verdicts.Add(new InactivityVerdict(pulse, f.Id, f.Side, f.Idle, ex, r));
            if (r) { rout[i] = true; routs++; }
        }
        foreach (var f in _f) if (f.Withdrawing && k < f.EscapeStep) anyProgress = true;
        // [P fix r1, P-V4(b)] the whole-field counter is updated BEFORE the routs and is NOT reset by them (TacticalBattle
        // PulseEndHiddenStationary); a routed formation's flight counts as progress in the pulses it lasts (above).
        _fieldIdle = anyProgress ? 0 : Math.Min(_fieldIdle + 1, n);
        if (routs > 0 && _r.MutualStandoffGuard)
        {
            bool left0 = false, left1 = false;
            for (int i = 0; i < _f.Length; i++)
                if (_f[i].OnField(k + 1) && !rout[i]) { if (_f[i].Side == 0) left0 = true; else left1 = true; }
            if (!left0 && !left1) return End(k, pulse, BattleEndReason.Standoff, -1, true);
        }
        for (int i = 0; i < _f.Length; i++) if (rout[i]) Rout(_f[i], k);
        if (routs > 0)
        {
            if (Eliminated(k, pulse, true, out var e)) return e;
            return null;
        }
        if (_fieldIdle >= n) return End(k, pulse, BattleEndReason.Standoff, -1, true);
        return null;
    }

    /// <summary>Legacy rule's active-concealment clause: in a concealing cell and not known to the enemy. [P fix r1] Model 2 (the
    /// legacy rule is selectable through the setup's rules) mirrors the tactical IsConcealed: concealing cell or personal
    /// concealment, and not currently TRACKED; model 1 unchanged.</summary>
    private bool Concealed(int i)
    {
        var f = _f[i];
        if (_v2) return (_s.Concealment.Multiplier(ClassAt(Pos(f))) < Q32.One || f.P.PersonalConcealment < Q32.One) && !_tracked[1 - f.Side, i];
        return _s.Concealment.Multiplier(ClassAt(Pos(f))) < Q32.One && !_known[1 - f.Side, i];
    }

    /// <summary>No passable path from the formation's current cell to its exit edge (the tactical <see cref="EdgeField"/>
    /// Dijkstra field; an impassable cell is unreachable). Verify-E E-V3.</summary>
    private bool Trapped(AF f)
    {
        var c = CellAt(Pos(f));
        return _edge[f.Side].DistanceAt(_s.Grid.Index(c.X, c.Y)) == EdgeField.Unreachable;
    }

    private void StartWithdrawal(AF f, long k)
    {
        f.Withdrawing = true;
        if (_v2) { f.EscapeStep = k + ExitSteps(f); return; }
        long dist = _edge[f.Side].LineDistanceMm(Pos(f));
        long cost = _s.Costs.Cost(ClassAt(Pos(f)));
        if (cost == TerrainCostTable.Impassable) cost = TerrainCostTable.MaxCost;
        long perStep = Math.Max(1, FixedMath.MulDivFloor(checked(f.Speed * _r.CatchUpNum), _r.StepMs, checked(cost * _r.CatchUpDen)));
        f.EscapeStep = k + Math.Max(1, FixedMath.DivCeil(dist, perStep));
    }

    /// <summary>[P] Model 2 travel time (steps) from the formation's point to its exit edge: the exit field's least-cost
    /// distance (cost x mm, climb included) at catch-up pace on open-ground terms, at least 1. Shared by the doctrine
    /// withdrawal and [P fix r1] the flight of a routed formation.</summary>
    private long ExitSteps(AF f)
    {
        var c = CellAt(Pos(f));
        long d = _edge[f.Side].DistanceAt(_s.Grid.Index(c.X, c.Y));
        long perStepOpen = Math.Max(1, FixedMath.MulDivFloor(checked(f.Speed * _r.CatchUpNum), _r.StepMs, checked(1000 * _r.CatchUpDen)));
        return Math.Max(1, FixedMath.DivCeil(d, checked(perStepOpen * 1000)));
    }

    private void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(_buf, v); _trace.AppendData(_buf); }

    private BattleResult End(long k, long pulse, BattleEndReason reason, int side, bool atPulseEnd)
    {
        long endMs = _s.StartMs + (atPulseEnd ? (pulse + 1) * _r.PulseMs : k * _r.StepMs);
        var fr = new FormationResult[_f.Length];
        bool any0 = false, any1 = false;
        for (int i = 0; i < _f.Length; i++)
        {
            var f = _f[i];
            long alive = f.Alive;
            // Like the tactical End(): only fighting entities are Active at the end; every other survivor (routed, withdrawn —
            // including a withdrawal still in transit when the field is decided — or disengaged) is Escaped.
            long act = f.Fighting ? alive : 0;
            long esc = alive - act;
            long resc = f.Routed ? esc : 0;
            long desc = f.Disengaged ? esc : 0;
            if (act > 0) { if (f.Side == 0) any0 = true; else any1 = true; }
            fr[i] = new FormationResult(f.Id, f.Side, f.Spec.Domain, f.P.FamilyKey, f.Start, act + esc, f.Killed, f.Surrendered,
                                        act, esc, resc, desc, f.Morale, f.DealtMilli / 1000, f.TakenMilli / 1000, f.Attacks);
            if (!fr[i].Conserved) throw new InvalidOperationException($"conservation violated for formation {f.Id}");
        }
        int holds = any0 && !any1 ? 0 : any1 && !any0 ? 1 : -1;
        L(-1); L((long)reason); L(side); L(holds); L(endMs);
        string trace = Convert.ToHexString(_trace.GetHashAndReset());
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void S(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        S(_f.Length);
        foreach (var f in _f)
        {
            S(f.Id); S(f.Killed); S(f.Surrendered); S(f.DamageMilli); S(f.Morale); S(f.Moved); S(f.Routed ? 1 : 0);
            S(f.Withdrawing ? 1 : 0); S(f.Disengaged ? 1 : 0); S(f.EscapeStep); S(f.Inactive);
            if (_v2) { S(f.Idle); S(f.BestEnemy); }   // [P fix r2] the approach-minimum potential is state (tactical hashes BestEnemy)
        }
        if (_lastHitStep is not null)             // [MOR] appended only when the "fight it won" rule is on (identity at 0)
        {
            S(_lastHitStep.Length); foreach (long v in _lastHitStep) S(v);
            S(_localWins.Count); foreach (var x in _localWins) { S(x.AtMs); S(x.FormationId); S(x.BrokenFormationId); S(x.Rise); }
        }
        // [AP] D-082 DA6 / DA7 (DA6-R: "The rule is applied to the AutoResolver"): the reported winner, from the end reason and the
        // setup's roles, through the function the TacticalResolver calls too (EqualSides). The battle itself is unchanged.
        var (winner, basis) = EqualSides.Decide(reason, side, _s.Defender, _s.Ambusher, _r.EqualSidesAtStandoff);
        return new BattleResult(ResolverKind.Auto, reason, side, holds, pulse + 1, endMs, _steps, 0, trace, Convert.ToHexString(h.GetHashAndReset()), fr)
        { Winner = winner, WinBasis = basis };
    }
}
