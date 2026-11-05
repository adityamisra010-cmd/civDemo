using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// Calibrated coefficients of the AutoResolver (Slice E, O-16 delegated). Every value is a model coefficient INSIDE the
/// resolution equations (CLAUDE.md law 2), integer (permille or steps). Defaults are the values FITTED on the fit half of the
/// parity battery (seeds 1-20, both orientations; reports/parity-report.md §3) against the PLACEHOLDER tactical rules of
/// Slice D — they are not gameplay values and move whenever a tactical placeholder moves (re-fit, never hand-tune).
/// </summary>
public sealed record AutoCoefficients
{
    /// <summary>Fraction (permille) of a formation's alive entities that strike in a melee step. FITTED.</summary>
    public long MeleeEngagePermille { get; init; } = 500;
    /// <summary>Frontage limit: at most this many attackers (permille) per alive entity of the target strike in melee — a
    /// formation cannot bring more entities into contact than the enemy line offers. FITTED.</summary>
    public long MaxAttackersPerTargetPermille { get; init; } = 500;
    /// <summary>Fraction (permille) of a ranged formation's free (not in melee) entities that get a shot in a step. FITTED.</summary>
    public long RangedEngagePermille { get; init; } = 300;
    /// <summary>Casualties = floor(damage * KillEfficiency / (1000 * MaxHp)): damage spread over targets wounds before it
    /// kills. FITTED.</summary>
    public long KillEfficiencyPermille { get; init; } = 600;
    /// <summary>Per-battle per-side multiplicative perturbation amplitude (permille), counter-RNG. FITTED.</summary>
    public long FortunePermille { get; init; } = 200;
    /// <summary>Per-step per-formation multiplicative perturbation amplitude (permille), counter-RNG. FITTED.</summary>
    public long NoisePermille { get; init; }
    /// <summary>Steps after an ambusher's first contact during which its victims deal it no damage (surprise). FITTED.</summary>
    public long AmbushFirstStrikeSteps { get; init; }
    /// <summary>Fraction (permille) of a ROUTED formation's remaining entities recorded as Surrendered. FITTED.</summary>
    public long SurrenderPermille { get; init; } = 50;

    /// <summary>Doctrine assumption (NOT fitted): a formation withdraws when its morale is below this at a pulse start —
    /// the battery's controller <see cref="KnowledgeAi.RetreatMorale"/> default 450 (PLACEHOLDER there).</summary>
    public long DoctrineRetreatMorale { get; init; } = 450;
    /// <summary>Doctrine assumption (NOT fitted): an Ambush-posture formation springs when a known enemy is within this many
    /// cells — <see cref="KnowledgeAi.AmbushTriggerCells"/> default 3 (PLACEHOLDER there).</summary>
    public long DoctrineAmbushTriggerCells { get; init; } = 3;

    public void Validate()
    {
        if (MeleeEngagePermille < 1 || MeleeEngagePermille > 1000) throw new ArgumentOutOfRangeException(nameof(MeleeEngagePermille));
        if (RangedEngagePermille < 1 || RangedEngagePermille > 1000) throw new ArgumentOutOfRangeException(nameof(RangedEngagePermille));
        if (MaxAttackersPerTargetPermille < 1 || MaxAttackersPerTargetPermille > 1_000_000) throw new ArgumentOutOfRangeException(nameof(MaxAttackersPerTargetPermille));
        if (KillEfficiencyPermille < 1 || KillEfficiencyPermille > 1000) throw new ArgumentOutOfRangeException(nameof(KillEfficiencyPermille));
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

/// <summary>
/// The AutoResolver (Slice E; spec §5; O-16 delegated): a SEPARATE, formation-level, integer model. It reads the same
/// <see cref="BattleSetup"/> as the tactical engine and produces the same <see cref="BattleResult"/> shape plus a duration in
/// pulses. It ignores the controllers: it assumes the doctrine of the battery's controller (<see cref="KnowledgeAi"/>:
/// advance on objectives, attack the nearest known enemy, ambush posture waits, withdraw below a morale) — stated in
/// <see cref="AutoCoefficients"/>. Domain: Land only (the coefficients are calibrated on land battles; naval/air auto models
/// are not built — O-1: auto is optional and never the default).
///
/// MODEL (one abstract round = one tactical step of StepMs; Lanchester-type exchange on a 1-D battle axis):
///  - AXIS: from the mean anchor of side 0 to that of side 1; each formation is a point x (mm) on it, side 0 faces +x.
///  - FACTORS per formation: count; profile melee/ranged/defence/HP/range/sight/speed; experience, supply (attack) and
///    leadership (morale loss) through <see cref="ConditionMath"/> (the SAME equations as the tactical engine); starting
///    morale; posture; terrain at its current 2-D point: movement cost (speed / cost, as the tactical anchor) and
///    concealment (detection of a stationary target, the tactical detection formula with the rules' stationary factor);
///    composition: ranged formations fire from range while melee closes, and in melee only the free share keeps shooting.
///  - STEP k (all decisions from the pre-step state, damage applied simultaneously):
///      1. pulse start: doctrine withdrawal of formations with morale &lt; DoctrineRetreatMorale (they leave the fight and
///         escape after their withdrawal time to the exit edge at catch-up speed; the time uses the straight-line distance to
///         the edge line at the current cell's cost, NOT the path length). A formation with no passable path to its edge
///         (EdgeField unreachable) does not withdraw;
///      2. movement: a fighting formation not in contact moves toward the enemy by speed*StepMs/cost (clamped at contact)
///         when its side knows an enemy (Attack; ranged stops inside range of a known target; ambush waits until a known
///         enemy is within the trigger) or, knowing none, toward its nearest objective (Normal posture only);
///      3. detection: per (side, unknown enemy formation) within an observer's sight, ONE counter-RNG draw with
///         p = concealment(target cell) [x StationaryConcealment if the target did not move or fight]; contact reveals;
///      4. combat: each fighting formation attacks its nearest enemy by (signed gap, id): melee in contact with the engaged
///         share (capped by the frontage limit: attackers per alive target entity), ranged at a known target in range when it did not move; expected damage = shots * hit(att', def') *
///         dmg(att') in milli-HP (the tactical hit/damage formulas and clamp), times (1000 + fortune + noise)/1000;
///      5. casualties floor((damage*KillEff + phase)/(1e6*MaxHp)) (milli-HP; per-formation counter-RNG phase), morale loss via
///         ConditionMath, rout below RoutThreshold (a routed formation with no passable path to its edge surrenders entirely,
///         as in the tactical engine; otherwise the fitted SurrenderPermille share);
///      6. termination exactly as the tactical rules: a side with no Active entities ends the battle (withdrawing
///         formations stay Active until they escape); at a pulse end the five-pulse inactivity rule (movement or damage or
///         a first detection is activity; a concealed formation is active while an active enemy is within its sight).
///  - There is NO round cap: every round either consumes HP (every attack deals positive damage, also at effective attack
///    0: HitMin &gt; 0, damage max(1, .), fortune + noise &lt;= 900; checked in ExpectedMilli), closes a bounded gap,
///    consumes a first detection, or counts toward the inactivity rule.
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
        public long Alive => Start - Killed - Surrendered;
        public bool Fighting => !Routed && !Withdrawing && !Disengaged && Alive > 0;
        public bool OnField(long step) => Alive > 0 && !Routed && !Disengaged && (!Withdrawing || step < EscapeStep);
    }

    private readonly BattleSetup _s;
    private readonly TacticalRules _r;
    private readonly AutoCoefficients _c;
    private readonly AF[] _f;
    private readonly Vec2L _origin, _axis;
    private readonly long _axisLen;
    private readonly EdgeField[] _edge;
    private readonly long _contactMm, _spp;
    private readonly long[] _fortune = new long[2];
    private readonly bool[,] _known;
    private readonly List<AutoStepRecord> _log = new();
    private readonly IncrementalHash _trace = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] _buf = new byte[8];
    private long _steps;

    public AutoBattle(BattleSetup setup, AutoCoefficients coefficients)
    {
        _s = setup; _r = setup.Rules; _c = coefficients;
        _r.Validate(); _c.Validate();
        // Termination (verify-E E-V1): the smallest possible attack (1 shot, damage 1 HP, hit HitMin, lowest perturbation factor)
        // must deal positive expected milli-HP in this integer model.
        if (FixedMath.MulDivFloor(FixedMath.MulDivFloor(1000, _r.HitMin.Raw, Q32.OneRaw), 1000 - _c.FortunePermille - _c.NoisePermille, 1000) <= 0)
            throw new ArgumentException("rules.HitMin is too small for the AutoResolver: a minimal attack would deal 0 milli-HP (no termination)");
        if (setup.Sides.Length != 2) throw new ArgumentException("exactly two sides");
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
        foreach (var f in _f)
        {
            f.X0 = Project(f.Anchor);
            var objs = setup.Sides[f.Side].Objectives;
            if (objs.Length > 0 && f.Spec.Posture == Posture.Normal)
            {
                // nearest objective by (distance², index) — composite key with the index tie-break
                int bi = 0; Int128 bd = Vec2L.DistanceSq(f.Anchor, objs[0]);
                for (int k = 1; k < objs.Length; k++) { Int128 d = Vec2L.DistanceSq(f.Anchor, objs[k]); if (d < bd) { bd = d; bi = k; } }
                f.ObjX = Project(objs[bi]); f.HasObj = true;
            }
        }
        _edge = new[] { new EdgeField(setup.Grid, setup.Costs, setup.Sides[0].Exit), new EdgeField(setup.Grid, setup.Costs, setup.Sides[1].Exit) };
        _contactMm = 2 * _r.EntityRadiusMm + _r.ContactReachMm;
        _spp = _r.PulseMs / _r.StepMs;
        _known = new bool[2, _f.Length];
        for (int side = 0; side < 2; side++)
            _fortune[side] = Uniform(AutoStreams.Fortune, side, 0, 0, _c.FortunePermille);
        foreach (var f in _f)
            f.KillPhase = (long)CounterRng.NextBelow(_s.Seed, AutoStreams.KillPhase, f.Id, 0, 0, 0, checked((ulong)(1_000_000 * f.P.MaxHp)));
    }

    /// <summary>Per-step decision log (off by default: the battery runs thousands of auto battles).</summary>
    public bool RecordLog { get; init; }
    public IReadOnlyList<AutoStepRecord> Log => _log;

    // ------------------------------------------------------------------ geometry

    private long Project(Vec2L p) => (long)(Vec2L.Dot(p - _origin, _axis) / _axisLen);

    private long X(AF f) => f.Side == 0 ? f.X0 + f.Moved : f.X0 - f.Moved;

    /// <summary>Current 2-D point: the anchor displaced by Moved along the axis toward the enemy.</summary>
    private Vec2L Pos(AF f)
    {
        long m = f.Side == 0 ? f.Moved : -f.Moved;
        return new Vec2L(f.Anchor.X + FixedMath.MulDivFloor(_axis.X, m, _axisLen), f.Anchor.Y + FixedMath.MulDivFloor(_axis.Y, m, _axisLen));
    }

    private TerrainClass ClassAt(Vec2L p)
    {
        var g = _s.Grid; var c = g.CellOf(p);
        long cx = Math.Clamp(c.X, 0, g.Width - 1), cy = Math.Clamp(c.Y, 0, g.Height - 1);
        return g.ClassAt(cx, cy);
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
    {
        if (shots <= 0) return 0;
        long perHitMilli = checked(DamageOf(attack) * 1000);
        long expected = FixedMath.MulDivFloor(checked(shots * perHitMilli), HitChance(attack, defence).Raw, Q32.OneRaw);
        long r = FixedMath.MulDivFloor(expected, factorPermille, 1000);
        if (r <= 0) throw new InvalidOperationException("termination invariant violated: an attack dealt no damage");
        return r;
    }

    private static long Share(long n, long permille) => n <= 0 ? 0 : Math.Max(1, FixedMath.MulDivFloor(n, permille, 1000));

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
                foreach (var f in _f) { f.ActivePulse = false; f.CasualtiesPulse = false; }
                foreach (var f in _f)
                    if (f.Fighting && f.Morale < _c.DoctrineRetreatMorale && !Trapped(f)) StartWithdrawal(f, k);   // no path: it cannot withdraw
            }
            foreach (var f in _f) { f.MovedStep = false; f.FoughtStep = false; }

            // 2. movement (decisions from the pre-step positions, applied together)
            long[] adv = new long[_f.Length];
            for (int i = 0; i < _f.Length; i++)
            {
                var f = _f[i];
                if (!f.Fighting) continue;
                int any = Nearest(i, false, out long gapAny);
                if (any < 0) continue;
                if (gapAny <= _contactMm) continue;                                  // in contact: stand and fight
                int t = Nearest(i, true, out long gapKnown);
                bool move;
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
                adv[i] = Math.Min(FixedMath.MulDivFloor(f.Speed, _r.StepMs, cost), limit);
            }
            for (int i = 0; i < _f.Length; i++)
                if (adv[i] > 0) { _f[i].Moved += adv[i]; _f[i].MovedStep = true; _f[i].ActivePulse = true; }

            // 3. detection
            for (int side = 0; side < 2; side++)
                for (int j = 0; j < _f.Length; j++)
                {
                    var g = _f[j];
                    if (g.Side == side || !g.OnField(k) || _known[side, j]) continue;
                    bool inSight = false, contact = false;
                    foreach (var o in _f)
                    {
                        if (o.Side != side || !o.OnField(k)) continue;
                        long d = Gap(o, g);
                        if (d <= _contactMm) contact = true;
                        if (d <= o.P.SightCells * _s.Grid.CellSizeMm) inSight = true;
                    }
                    bool detect = contact;
                    if (!detect && inSight)
                    {
                        Q32 clarity = _s.Concealment.Multiplier(ClassAt(Pos(g)));
                        bool exposed = g.MovedStep || g.FoughtStep || clarity == Q32.One;
                        Q32 p = exposed ? clarity : clarity.Mul(_r.StationaryConcealment);
                        detect = CounterRng.Chance(_s.Seed, AutoStreams.Detection, g.Id, k, side, 0, p);
                    }
                    if (!detect) continue;
                    _known[side, j] = true;
                    foreach (var o in _f) if (o.Side == side && o.OnField(k)) { o.ActivePulse = true; break; }   // first detection
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
                    _known[f.Side, m] = true;
                    bool surprised = g.Spec.Posture == Posture.Ambush && k < g.FirstContactStep + _c.AmbushFirstStrikeSteps;
                    long engaged = Math.Min(alive, Math.Min(Share(alive, _c.MeleeEngagePermille), Share(g.Alive, _c.MaxAttackersPerTargetPermille)));
                    if (!surprised)
                    {
                        long dm = ExpectedMilli(engaged, f.EffMelee, g.EffDefence, factor);
                        pending[m] = checked(pending[m] + dm); f.DealtMilli += dm; f.Attacks += engaged;
                        target[i] = m; mode[i] |= 1;
                    }
                    long free = alive - engaged;
                    if (f.P.IsRanged && free > 0 && !f.MovedStep && !surprised)
                    {
                        long shots = Share(free, _c.RangedEngagePermille);
                        long dr = ExpectedMilli(shots, f.EffRanged, g.EffDefence, factor);
                        pending[m] = checked(pending[m] + dr); f.DealtMilli += dr; f.Attacks += shots;
                        target[i] = m; mode[i] |= 2;
                    }
                    if (mode[i] != 0) f.FoughtStep = true;
                    continue;
                }
                if (f.P.IsRanged && !f.MovedStep)
                {
                    int t = Nearest(i, true, out long gt);
                    if (t >= 0 && gt <= f.P.RangeCells * _s.Grid.CellSizeMm)
                    {
                        long shots = Share(alive, _c.RangedEngagePermille);
                        long dr = ExpectedMilli(shots, f.EffRanged, _f[t].EffDefence, factor);
                        pending[t] = checked(pending[t] + dr); f.DealtMilli += dr; f.Attacks += shots;
                        target[i] = t; mode[i] = 2; f.FoughtStep = true;
                    }
                }
            }

            // 5. damage, casualties, morale, rout (simultaneous)
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
                    (long)(((Int128)g.DamageMilli * _c.KillEfficiencyPermille + g.KillPhase) / checked(1_000_000 * g.P.MaxHp)));
                long newDead = dead - g.Killed;
                if (newDead <= 0) continue;
                g.Killed = dead;
                g.CasualtiesPulse = true;
                g.Morale = Math.Max(0, g.Morale - ConditionMath.MoraleLoss(newDead, _r.MoraleLossFullStrength, g.Start, g.Spec.Leadership));
            }
            for (int i = 0; i < _f.Length; i++) if (_f[i].FoughtStep) _f[i].ActivePulse = true;
            foreach (var g in _f)
                if (g.Fighting && g.Morale < _r.RoutThreshold)
                {
                    g.Routed = true;
                    // Spec §4 (as the tactical Resolve): a routing formation with no passable path to its exit edge surrenders
                    // entirely (verify-E E-V3); otherwise the fitted share is recorded as Surrendered.
                    g.Surrendered = Trapped(g) ? g.Alive : FixedMath.MulDivFloor(g.Alive, _c.SurrenderPermille, 1000);
                }
            for (int i = 0; i < _f.Length; i++)
            {
                var f = _f[i];
                long pos = X(f), tid = target[i] >= 0 ? _f[target[i]].Id : -1;
                if (RecordLog) _log.Add(new AutoStepRecord(k, f.Id, pos, f.MovedStep, tid, mode[i], f.DealtMilli, f.Alive, f.Morale));
                L(k); L(f.Id); L(pos); L(tid); L(f.Alive); L(f.DamageMilli); L(f.Morale); L(f.Routed ? 1 : 0); L(f.Withdrawing ? 1 : 0);
            }

            // 6. termination: elimination (a side with no Active entity) after every step
            bool a0 = false, a1 = false;
            foreach (var f in _f) if (f.OnField(k + 1)) { if (f.Side == 0) a0 = true; else a1 = true; }
            if (!a0 || !a1)
                return End(k, pulse, (!a0 && !a1) ? BattleEndReason.BothEliminated : BattleEndReason.SideEliminated, (!a0 && !a1) ? -1 : (!a0 ? 0 : 1), false);

            if (pulseEnd)
            {
                // morale recovery (Hold with no casualties: a formation that neither moved nor fought this pulse)
                foreach (var f in _f)
                    if (f.Fighting && !f.CasualtiesPulse && !f.ActivePulse && !f.Withdrawing)
                        f.Morale = Math.Min(_r.MoraleMax, f.Morale + _r.HoldRecovery);
                // inactivity rule
                bool[] act = new bool[_f.Length];
                for (int i = 0; i < _f.Length; i++)
                {
                    var f = _f[i];
                    if (!f.OnField(k)) continue;
                    bool a = f.ActivePulse || (f.Withdrawing && k < f.EscapeStep);
                    if (!a && _r.ActiveConcealmentRule && Concealed(i))
                        foreach (var e in _f)
                            if (e.Side != f.Side && e.OnField(k) && e.ActivePulse && Math.Abs(Gap(e, f)) <= e.P.SightCells * _s.Grid.CellSizeMm) { a = true; break; }
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

    private bool Concealed(int i)
    {
        var f = _f[i];
        return _s.Concealment.Multiplier(ClassAt(Pos(f))) < Q32.One && !_known[1 - f.Side, i];
    }

    /// <summary>No passable path from the formation's current cell to its exit edge (the tactical <see cref="EdgeField"/>
    /// Dijkstra field; an impassable cell is unreachable). Verify-E E-V3.</summary>
    private bool Trapped(AF f)
    {
        var g = _s.Grid; var c = g.CellOf(Pos(f));
        long cx = Math.Clamp(c.X, 0, g.Width - 1), cy = Math.Clamp(c.Y, 0, g.Height - 1);
        return _edge[f.Side].DistanceAt(g.Index(cx, cy)) == EdgeField.Unreachable;
    }

    private void StartWithdrawal(AF f, long k)
    {
        f.Withdrawing = true;
        long dist = _edge[f.Side].LineDistanceMm(Pos(f));
        long cost = _s.Costs.Cost(ClassAt(Pos(f)));
        if (cost == TerrainCostTable.Impassable) cost = TerrainCostTable.MaxCost;
        long perStep = Math.Max(1, FixedMath.MulDivFloor(checked(f.Speed * _r.CatchUpNum), _r.StepMs, checked(cost * _r.CatchUpDen)));
        f.EscapeStep = k + Math.Max(1, FixedMath.DivCeil(dist, perStep));
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
        }
        return new BattleResult(ResolverKind.Auto, reason, side, holds, pulse + 1, endMs, _steps, 0, trace, Convert.ToHexString(h.GetHashAndReset()), fr);
    }
}
