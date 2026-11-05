using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>Read-only snapshot of one entity (tests, inspection).</summary>
public readonly record struct EntityView(long Id, long FormationId, int Side, EntityState State, long Hp, Vec2L Pos, int Generation,
                                         bool RoutedEscape, bool Disengaged);

/// <summary>
/// The tactical vertical slice (spec §4) — an <see cref="IEventWorld"/> driven by the Slice B scheduler on the Slice C spatial
/// layer. Integer only.
///
/// TIME. Pulses of PulseMs on the global ms timeline; inside a pulse a fixed-step Step event every StepMs (hybrid, O-5).
/// At one instant the classes run in this order (EventClass doc): System [PulseEnd p0, PulseStart p1, Order p5] →
/// Arrival [Step: anchors, entity moves, separation, minima] → Detection [detection + target choice; schedules Fire] →
/// Fire [hit rolls; schedules Damage] → Damage [HP loss] → Resolve [deaths, morale, rout, heal, escape, surrender,
/// elimination]. All shots at an instant are taken against the pre-damage world and all damage lands before any death is
/// resolved (simultaneity: a mutual kill kills both).
///
/// ENTITIES. Formation → Count battle-transient entities (O-10 a) on slot offsets around the formation anchor; every loop runs
/// in ascending entity id (input order is irrelevant: the engine sorts). Movement via the Slice C separation step on the
/// shared grid; anchors follow Slice C A* paths; Routing / Retreating entities descend the exit-distance field.
/// Generation is bumped on every state change, so an event referencing a stale entity is dropped by the scheduler.
///
/// KNOWLEDGE. Per side: tracked flag, ever-detected flag, last-seen point/instant. A side ACQUIRES an untracked enemy with ONE
/// counter-RNG draw per (side, target, instant): p = best clarity over its observers within sight with LOS, times the
/// stationary-concealment factor when the target is stationary, has not fired, and is concealed (clarity &lt; 1). Contact
/// reveals automatically. A tracked enemy stays tracked while any observer within sight has LOS (no re-roll); otherwise it
/// is lost and remembered at its last-seen point. (One draw per side rather than per observer: a crowd does not see better
/// than its best-placed observer — a design choice under O-6, reported.) Controllers see only <see cref="SideView"/>.
///
/// COMBAT (O-15 b): individual entities; melee needs contact; ranged uses Slice C direct fire against the enemy discs
/// (friendly discs transparent — PLACEHOLDER simplification); hit chance attack/(attack+defence) clamped; damage
/// max(1, attack/DamageDivisor). All PLACEHOLDER.
///
/// TERMINATION (O-4: no pulse ceiling): elimination of Active entities on a side, or the five-pulse inactivity rule with the
/// active-concealment clause (see <see cref="PulseEnd"/>). There is NO maximum pulse count anywhere in this class.
/// </summary>
public sealed partial class TacticalBattle : IEventWorld
{
    private sealed class Ent
    {
        public long Id; public int F; public int Side; public int Slot; public UnitProfile P = null!;
        public Vec2L Pos; public long Hp; public long HealPool; public EntityState State; public int Gen;
        public bool Moved; public bool WantedMove; public long RevealUntilMs = long.MinValue; public bool Primary;
        public long BestEnemy = -1, BestObj = -1, BestEdge = -1;
        public bool RoutedEscape, Disengaged;
        public long Speed;
        /// <summary>lane N (snipers): earliest instant of the next legacy ranged shot (FireIntervalSteps).</summary>
        public long NextRangedMs = long.MinValue;
        public bool OnField => State == EntityState.Active || State == EntityState.Routing;
    }

    private sealed class Form
    {
        public long Id; public int Side; public FormationSpec Spec = null!; public UnitProfile P = null!;
        public int Start; public long Morale; public OrderKind Dir = OrderKind.Hold; public long DirTarget = -1; public Vec2L DirPos;
        public Vec2L Anchor; public Vec2L[] Path = Array.Empty<Vec2L>(); public int PathIdx; public long PathTargetCell = -1;
        public ActionBudget Budget = null!; public int Inactive; public bool StateChanged; public long CasualtiesPulse; public long KilledStep;
        public bool Routed; public int[] Members = Array.Empty<int>(); public long Speed;
        public long FwdX, FwdY;
        public long DamageDealt, DamageTaken, Attacks;
        /// <summary>Slice E: effective stats through <see cref="ConditionMath"/> (identity at default conditions).</summary>
        public long EffMelee, EffRanged, EffDefence;
    }

    private sealed class Knowledge
    {
        public bool[] Tracked = null!, Ever = null!, Gone = null!;
        public Vec2L[] LastSeen = null!;
        public long[] LastSeenMs = null!;
    }

    private readonly BattleSetup _setup;
    private readonly TacticalRules _r;
    private readonly TerrainGrid _grid;
    private readonly TerrainCostTable _costs;
    private readonly ConcealmentTable _conceal;
    private readonly GridPathfinder _pf;
    private readonly EdgeField[] _edge;
    private readonly IFormationController[] _ctl;
    private readonly Ent[] _e;
    private readonly long[] _ids;
    private readonly Form[] _f;
    private readonly long[] _fids;
    private readonly Knowledge[] _k;
    private readonly EventScheduler _s;
    private readonly List<OrderLogEntry> _log = new();
    private readonly List<OrderRequest> _scratchOrders = new();
    private readonly Disc[][] _enemyDiscs = new Disc[2][];
    private readonly long _contactMm;
    private readonly int _stepsPerPulse;

    private long _pulse = -1;
    private long _pulseStartMs;
    private bool _started;
    private BattleResult? _result;

    public TacticalBattle(BattleSetup setup, IFormationController side0, IFormationController side1)
    {
        _setup = setup;
        _r = setup.Rules;
        _r.Validate();
        _grid = setup.Grid; _costs = setup.Costs; _conceal = setup.Concealment;
        if (setup.Sides.Length != 2) throw new ArgumentException("exactly two sides");
        _ctl = new[] { side0, side1 };
        _pf = new GridPathfinder(_grid, _costs);
        _edge = new[] { new EdgeField(_grid, _costs, setup.Sides[0].Exit), new EdgeField(_grid, _costs, setup.Sides[1].Exit) };
        _stepsPerPulse = checked((int)(_r.PulseMs / _r.StepMs));
        _contactMm = 2 * _r.EntityRadiusMm + _r.ContactReachMm;

        // Formations: sorted by id (input order irrelevant).
        var allSpecs = (FormationSpec[])setup.Formations.Clone();
        Array.Sort(allSpecs, static (a, b) => a.Id.CompareTo(b.Id));
        // lane N hook: vessel / air formations go to the domain modules (TacticalBattle.Domain.cs); ids are unique across all.
        for (int i = 1; i < allSpecs.Length; i++)
            if (allSpecs[i].Id == allSpecs[i - 1].Id) throw new ArgumentException($"duplicate formation id {allSpecs[i].Id}");
        var specs = Array.FindAll(allSpecs, static x => x.Domain == Domain.Land);
        var domainSpecs = Array.FindAll(allSpecs, static x => x.Domain != Domain.Land);
        _f = new Form[specs.Length];
        _fids = new long[specs.Length];
        var ents = new List<Ent>();
        for (int i = 0; i < specs.Length; i++)
        {
            var s = specs[i];
            if (i > 0 && s.Id == specs[i - 1].Id) throw new ArgumentException($"duplicate formation id {s.Id}");
            if (s.Id < 0 || s.Id > 1_000_000_000) throw new ArgumentOutOfRangeException(nameof(setup), "formation id out of range");
            if (s.Side != 0 && s.Side != 1) throw new ArgumentException("side must be 0 or 1");
            string? capErr = setup.Caps.Check(s.Profile.CapFamily, s.Count);
            if (capErr is not null) throw new ArgumentException($"formation {s.Id}: {capErr}");
            bool aboard = s.EmbarkedOn is not null;          // lane N: starts aboard a transport (placed by DomainInit)
            if (!aboard) RequirePassable(s.Anchor, $"formation {s.Id} anchor");
            var f = new Form
            {
                Id = s.Id, Side = s.Side, Spec = s, P = s.Profile, Start = s.Count,
                Morale = s.Morale ?? _r.MoraleMax, Anchor = s.Anchor, Budget = new ActionBudget(_r.ActionBudgetN),
                Speed = FixedMath.MulDivFloor(_r.RefFootSpeedMmPerS, s.Profile.SpeedNum, s.Profile.SpeedDen),
            };
            if (f.Morale < 0 || f.Morale > _r.MoraleMax) throw new ArgumentOutOfRangeException(nameof(setup), "morale");
            ConditionMath.Validate(s.Experience, s.Leadership, s.Supply);
            f.EffMelee = ConditionMath.Attack(s.Profile.Melee, s.Experience, s.Supply);
            f.EffRanged = ConditionMath.Attack(s.Profile.Ranged, s.Experience, s.Supply);
            f.EffDefence = ConditionMath.Defence(s.Profile.Defence, s.Experience);
            if (f.Speed <= 0) throw new ArgumentException("speed must be positive");
            (f.FwdX, f.FwdY) = setup.Sides[s.Side].Exit switch
            {
                ExitEdge.West => (1L, 0L), ExitEdge.East => (-1L, 0L), ExitEdge.North => (0L, 1L), _ => (0L, -1L),
            };
            _f[i] = f; _fids[i] = s.Id;
            // Entities: explicit (any order, sorted by id) or generated on the slots with ids Id*1000 + slot.
            long[] eid; Vec2L[] epos;
            if (s.Entities is not null)
            {
                if (s.Entities.Length != s.Count) throw new ArgumentException($"formation {s.Id}: Entities.Length != Count");
                var es = (EntitySpec[])s.Entities.Clone();
                Array.Sort(es, static (a, b) => a.Id.CompareTo(b.Id));
                eid = new long[es.Length]; epos = new Vec2L[es.Length];
                for (int k = 0; k < es.Length; k++) { eid[k] = es[k].Id; epos[k] = es[k].Pos; }
            }
            else
            {
                if (s.Count >= 1000) throw new ArgumentException("generated ids need Count < 1000");
                eid = new long[s.Count]; epos = new Vec2L[s.Count];
                for (int k = 0; k < s.Count; k++) { eid[k] = checked(s.Id * 1000 + k); epos[k] = s.Anchor + SlotOffset(f, k); }
            }
            for (int k = 0; k < eid.Length; k++)
            {
                if (!aboard) RequirePassable(epos[k], $"entity {eid[k]}");
                ents.Add(new Ent
                {
                    Id = eid[k], F = i, Side = s.Side, Slot = k, P = s.Profile, Pos = epos[k], Hp = s.Profile.MaxHp,
                    HealPool = FixedMath.MulDivFloor(s.Profile.MaxHp, _r.HealPoolNum, _r.HealPoolDen),
                    Speed = f.Speed,
                });
            }
        }
        ents.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        _e = ents.ToArray();
        if (_e.Length > MoverIndexMask + 1) throw new ArgumentException("at most 65,536 entities per battle (mover key layout)");
        _ids = new long[_e.Length];
        for (int i = 0; i < _e.Length; i++)
        {
            if (i > 0 && _e[i].Id == _e[i - 1].Id) throw new ArgumentException($"duplicate entity id {_e[i].Id}");
            _ids[i] = _e[i].Id;
        }
        for (int i = 0; i < _e.Length; i++)
            for (int j = i + 1; j < _e.Length; j++)
                if (_f[_e[i].F].Spec.EmbarkedOn is null && _f[_e[j].F].Spec.EmbarkedOn is null
                    && Separation.Overlaps(_e[i].Pos, _r.EntityRadiusMm, _e[j].Pos, _r.EntityRadiusMm))
                    throw new ArgumentException($"entities {_e[i].Id} and {_e[j].Id} overlap at start");
        var members = new List<int>[_f.Length];
        for (int i = 0; i < _f.Length; i++) members[i] = new List<int>();
        for (int i = 0; i < _e.Length; i++) members[_e[i].F].Add(i);
        for (int i = 0; i < _f.Length; i++) _f[i].Members = members[i].ToArray();
        _k = new Knowledge[2];
        for (int s = 0; s < 2; s++)
            _k[s] = new Knowledge
            {
                Tracked = new bool[_e.Length], Ever = new bool[_e.Length], Gone = new bool[_e.Length],
                LastSeen = new Vec2L[_e.Length], LastSeenMs = new long[_e.Length],
            };
        _s = new EventScheduler(setup.StartMs);
        DomainInit(domainSpecs);      // lane N hook
    }

    // ------------------------------------------------------------------ public surface

    public EventScheduler Scheduler => _s;
    public bool IsOver => _result is not null;
    public BattleResult? Result => _result;
    public long CurrentPulse => _pulse;
    public IReadOnlyList<OrderLogEntry> Orders => _log;
    public int StepsPerPulse => _stepsPerPulse;

    public IReadOnlyList<EntityView> Entities()
    {
        var v = new EntityView[_e.Length];
        for (int i = 0; i < _e.Length; i++)
        {
            var e = _e[i];
            v[i] = new EntityView(e.Id, _f[e.F].Id, e.Side, e.State, e.Hp, e.Pos, e.Gen, e.RoutedEscape, e.Disengaged);
        }
        return v;
    }

    public int InactivityCounter(long formationId) => _f[FormIndex(formationId)].Inactive;
    public long Morale(long formationId) => _f[FormIndex(formationId)].Morale;
    public Vec2L Anchor(long formationId) => _f[FormIndex(formationId)].Anchor;
    public OrderKind Directive(long formationId) => _f[FormIndex(formationId)].Dir;
    public bool IsTracked(int side, long entityId) => _k[side].Tracked[EntIndex(entityId)];
    public bool EverDetected(int side, long entityId) => _k[side].Ever[EntIndex(entityId)];

    /// <summary>Runs to termination. There is no pulse or event cap (O-4); a test harness may guard with its own loop.</summary>
    public BattleResult Run()
    {
        Start();
        while (Advance()) { }
        return _result!;
    }

    public void Start()
    {
        if (_started) throw new InvalidOperationException("already started");
        _started = true;
        _s.Schedule(_setup.StartMs, EventClass.System, 1, 0, TacticalKinds.PulseStart, 0, _setup.StartMs);
    }

    /// <summary>Executes one event. Returns false once the battle is over.</summary>
    public bool Advance()
    {
        if (!_started) Start();
        if (_result is not null) return false;
        if (!_s.TryPeekKey(out _)) throw new InvalidOperationException("event queue empty before termination (engine bug)");
        _s.ExecuteNext(this, out _);
        return _result is null;
    }

    /// <summary>Runs until the given pulse has started (or the battle ended). For tests and the bench.</summary>
    public void RunUntilPulse(long pulse)
    {
        if (!_started) Start();
        while (_result is null && _pulse < pulse) Advance();
    }

    // ------------------------------------------------------------------ IEventWorld

    public bool IsLive(EntityRef r)
    {
        int i = Array.BinarySearch(_ids, r.Id);
        if (i >= 0) return _e[i].Gen == r.Generation;
        return DomainIsLive(r);       // lane N hook (false when there are no vessels / aircraft)
    }

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long t = ev.Key.TimeMs;
        switch (ev.Kind)
        {
            case TacticalKinds.PulseStart: PulseStart(t, ev.A); break;
            case TacticalKinds.Order: ApplyOrder(t, ev.C); break;
            case TacticalKinds.Step: Step(t, ev.A, ev.B); break;
            case TacticalKinds.Detect: Detect(t); break;
            case TacticalKinds.Fire: Fire(t, ev); break;
            case TacticalKinds.Damage: Damage(ev); break;
            case TacticalKinds.Resolve: Resolve(t); break;
            case TacticalKinds.PulseEnd: PulseEnd(t, ev.A); break;
            default:
                if (ExecuteDomain(ev)) break;     // lane N hook
                throw new InvalidOperationException($"unknown tactical event kind {ev.Kind}");
        }
    }

    // ------------------------------------------------------------------ pulse start / orders

    private void PulseStart(long t, long p)
    {
        _pulse = p;
        _pulseStartMs = t;
        foreach (var f in _f) { f.Budget.Refresh(); f.StateChanged = false; f.CasualtiesPulse = 0; }
        foreach (var e in _e) e.Primary = false;
        DomainPulseStart();       // lane N hook
        for (int side = 0; side < 2; side++)
        {
            var view = BuildView(side, t);
            _scratchOrders.Clear();
            _ctl[side].Decide(view, _scratchOrders);
            foreach (var o in _scratchOrders)
            {
                long index = _log.Count;
                if (o.StepOffset < 0 || o.StepOffset >= _stepsPerPulse)
                {
                    _log.Add(new OrderLogEntry(index, p, side, o, t, OrderInvalid.BadStep, BudgetRefusal.None));
                    continue;
                }
                long at = t + o.StepOffset * _r.StepMs;
                _log.Add(new OrderLogEntry(index, p, side, o, at, OrderInvalid.None, BudgetRefusal.None));
                _s.Schedule(at, EventClass.System, 5, index, TacticalKinds.Order, o.FormationId, (long)o.Kind, index);
            }
        }
        _s.Schedule(t, EventClass.Arrival, 0, 0, TacticalKinds.Step, p, 0);
        _s.Schedule(t + _r.PulseMs, EventClass.System, 0, 0, TacticalKinds.PulseEnd, p);
    }

    private void ApplyOrder(long t, long index)
    {
        if (TryApplyDomainOrder(t, (int)index)) return;     // lane N hook (false: the land path below handles it unchanged)
        var entry = _log[(int)index];
        var o = entry.Request;
        OrderInvalid inv = OrderInvalid.None;
        Vec2L[]? path = null;
        int fi = FindForm(o.FormationId);
        Form? f = fi >= 0 ? _f[fi] : null;
        if (f is null) inv = OrderInvalid.UnknownFormation;
        else if (f.Side != entry.Side) inv = OrderInvalid.NotOwnFormation;
        else if (CountState(f, EntityState.Active) == 0) inv = OrderInvalid.NotOnField;
        else if (o.Kind == OrderKind.Attack || o.Kind == OrderKind.Reengage)
        {
            int ti = FindForm(o.TargetFormationId);
            if (ti < 0 || _f[ti].Side == f.Side) inv = OrderInvalid.BadTarget;
            else if (o.Kind == OrderKind.Reengage && f.Dir != OrderKind.Retreat) inv = OrderInvalid.NothingToReengage;
        }
        else if (o.Kind == OrderKind.MoveTo)
        {
            if (!o.Pos.InDocumentedRange) inv = OrderInvalid.BadTarget;
            else
            {
                var gc = _grid.CellOf(o.Pos);
                if (!_grid.IsPassable(_costs, gc.X, gc.Y)) inv = OrderInvalid.BadTarget;
                else
                {
                    var ac = _grid.CellOf(f.Anchor);
                    var pr = _pf.FindPath(ac, gc);
                    if (!pr.Found) inv = OrderInvalid.NoPath;
                    else { path = PathMovement.Waypoints(_grid, pr.Cells); path[^1] = o.Pos; }
                }
            }
        }
        else if ((byte)o.Kind > (byte)OrderKind.Reengage) inv = OrderInvalid.BadTarget;

        BudgetRefusal br = BudgetRefusal.None;
        if (inv == OrderInvalid.None) br = f!.Budget.TryAccept(o.Kind);
        _log[(int)index] = entry with { Invalid = inv, Budget = br };
        if (inv != OrderInvalid.None || br != BudgetRefusal.None) return;

        switch (o.Kind)
        {
            case OrderKind.Hold: f!.Dir = OrderKind.Hold; ClearPath(f); break;
            case OrderKind.MoveTo: f!.Dir = OrderKind.MoveTo; f.DirPos = o.Pos; f.Path = path!; f.PathIdx = 0; f.PathTargetCell = -1; break;
            case OrderKind.Attack: f!.Dir = OrderKind.Attack; f.DirTarget = o.TargetFormationId; ClearPath(f); break;
            case OrderKind.Retreat: f!.Dir = OrderKind.Retreat; ClearPath(f); break;
            case OrderKind.Rally: f!.Dir = OrderKind.Rally; ClearPath(f); break;
            case OrderKind.Reengage: f!.Dir = OrderKind.Attack; f.DirTarget = o.TargetFormationId; ClearPath(f); break;
        }
    }

    private static void ClearPath(Form f) { f.Path = Array.Empty<Vec2L>(); f.PathIdx = 0; f.PathTargetCell = -1; }

    private SideView BuildView(int side, long t)
    {
        var own = new List<OwnFormationView>();
        foreach (var f in _f)
        {
            if (f.Side != side) continue;
            int act = CountState(f, EntityState.Active), rout = CountState(f, EntityState.Routing);
            if (act + rout == 0) continue;
            own.Add(new OwnFormationView(f.Id, f.Spec.Posture, f.Dir, f.DirTarget, f.DirPos, f.Morale, f.Start, act, rout,
                                         f.Anchor, Centroid(f), f.Budget.Used, f.Budget.N));
        }
        var k = _k[side];
        var known = new List<KnownEnemy>();
        for (int i = 0; i < _e.Length; i++)
            if (k.Ever[i] && !k.Gone[i]) known.Add(new KnownEnemy(_e[i].Id, _f[_e[i].F].Id, k.LastSeen[i], k.LastSeenMs[i], k.Tracked[i]));
        var view = new SideView(side, _pulse, t, _grid.CellSizeMm, own.ToArray(), known.ToArray(), _setup.Sides[side].Objectives);
        FillDomainView(view, side);       // lane N hook
        return view;
    }

    // ------------------------------------------------------------------ step: movement

    private void Step(long t, long p, long j)
    {
        foreach (var f in _f) MoveAnchor(f);

        int n = 0;
        foreach (var e in _e) if (e.OnField) n++;
        var movers = new Mover[n];
        int m = 0;
        foreach (var e in _e)
        {
            if (!e.OnField) continue;
            Vec2L d = Desired(e);
            e.WantedMove = d != e.Pos;
            movers[m++] = new Mover(MoverKey(t, e), e.Pos, _r.EntityRadiusMm, d);
        }
        var res = Separation.Step(movers, _grid, _costs);
        for (int i = 0; i < res.Length; i++)
        {
            var e = _e[(int)(res[i].Id & MoverIndexMask)];
            e.Moved = res[i].Pos != e.Pos;
            e.Pos = res[i].Pos;
        }
        UpdateMinima();
        if (_hasDomain) DomainStep(t);        // lane N hook: vessels, aircraft, embark / landing
        _s.Schedule(t, EventClass.Detection, 0, 0, TacticalKinds.Detect, p, j);
        _s.Schedule(t, EventClass.Resolve, 0, 0, TacticalKinds.Resolve, p, j);
        if (j + 1 < _stepsPerPulse) _s.Schedule(t + _r.StepMs, EventClass.Arrival, 0, 0, TacticalKinds.Step, p, j + 1);
    }

    private const long MoverIndexMask = (1L << 16) - 1;

    /// <summary>
    /// Movement priority of an entity in this step's separation pass (Slice C resolves movers in ascending mover id).
    /// Using the entity id directly gave the lower-id SIDE first move every step, a systematic bias measured at 14 vs 2
    /// losses in 20 mirrored 50v50 battles (and 2 vs 16 with the ids swapped). Priority is therefore a counter-RNG draw
    /// keyed (seed, instant, entity id) — a pure function, independent of input order — in the high bits, with the entity's
    /// array index in the low 16 bits so keys are unique and map back exactly. (Collision priority is O-6 delegated.)
    /// </summary>
    private long MoverKey(long t, Ent e)
    {
        long idx = EntIndex(e.Id);
        ulong draw = CounterRng.Draw(_setup.Seed, TacticalStreams.MovePriority, e.Id, t, 0, 0);
        return (long)(draw >> 17) << 16 | idx;
    }

    private void MoveAnchor(Form f)
    {
        if (CountState(f, EntityState.Active) == 0) return;
        if (f.Dir == OrderKind.Attack)
        {
            if (!KnownCentroid(f.Side, f.DirTarget, out Vec2L tp)) return;
            long eng = _r.EngageRadiusCells * _grid.CellSizeMm;
            if (Vec2L.DistanceSq(f.Anchor, tp) <= (Int128)eng * eng) return;
            var tc = _grid.CellOf(tp);
            if (!_grid.InBounds(tc.X, tc.Y)) return;
            long tcell = _grid.Index(tc.X, tc.Y);
            if (tcell != f.PathTargetCell || f.PathIdx >= f.Path.Length)
            {
                var pr = _pf.FindPath(_grid.CellOf(f.Anchor), tc);
                if (!pr.Found) return;
                f.Path = PathMovement.Waypoints(_grid, pr.Cells);
                f.PathIdx = 0;
                f.PathTargetCell = tcell;
            }
        }
        else if (f.Dir != OrderKind.MoveTo) return;
        var ac = _grid.CellOf(f.Anchor);
        long cost = _costs.Cost(_grid.ClassAt(ac.X, ac.Y));
        long adv = FixedMath.MulDivFloor(f.Speed, _r.StepMs, cost);   // mm = speed[mm/s] * step[ms] / 1000 * 1000/cost
        while (adv > 0 && f.PathIdx < f.Path.Length)
        {
            Vec2L wp = f.Path[f.PathIdx];
            long d = Vec2L.Distance(f.Anchor, wp);
            if (d <= adv) { f.Anchor = wp; adv -= d; f.PathIdx++; }
            else { f.Anchor = Toward(f.Anchor, wp, adv); adv = 0; }
        }
    }

    private Vec2L Desired(Ent e)
    {
        var f = _f[e.F];
        var c = _grid.CellOf(e.Pos);
        int ci = _grid.Index(c.X, c.Y);
        long cost = _costs.Cost(_grid.ClassAtIndex(ci));
        long maxStep = FixedMath.MulDivFloor(checked(e.Speed * _r.CatchUpNum), _r.StepMs, checked(cost * _r.CatchUpDen));
        if (e.State == EntityState.Routing || f.Dir == OrderKind.Retreat)
        {
            int next = _edge[e.Side].NextCell(ci);
            if (next < 0) return e.Pos;
            return Toward(e.Pos, _grid.CellCenter(_grid.IndexX(next), _grid.IndexY(next)), maxStep);
        }
        if (NearestTracked(e, _contactMm, out _) >= 0) return e.Pos;                       // in contact: stand and fight
        if (e.P.IsRanged && FiringStance(f.Dir) && NearestTracked(e, e.P.RangeCells * _grid.CellSizeMm, out _) >= 0) return e.Pos;
        if (!e.P.IsRanged && f.Dir == OrderKind.Attack)
        {
            int en = NearestTracked(e, _r.EngageRadiusCells * _grid.CellSizeMm, out _);
            if (en >= 0) return Toward(e.Pos, _e[en].Pos, maxStep);
        }
        return Toward(e.Pos, f.Anchor + SlotOffset(f, e.Slot), maxStep);
    }

    private static bool FiringStance(OrderKind d) => d == OrderKind.Attack || d == OrderKind.Hold || d == OrderKind.Rally;

    private Vec2L SlotOffset(Form f, int slot)
    {
        int cols = Math.Min(f.Start, _r.FormationColumns);
        long row = slot / cols, col = slot % cols;
        long sp = _r.SlotSpacingMm;
        long fwd = -row * sp;
        long lat = col * sp - (cols - 1) * sp / 2;
        // forward unit (fx, fy); lateral unit (-fy, fx)
        return new Vec2L(fwd * f.FwdX - lat * f.FwdY, fwd * f.FwdY + lat * f.FwdX);
    }

    private static Vec2L Toward(Vec2L a, Vec2L b, long maxStep)
    {
        if (maxStep <= 0) return a;
        long d = Vec2L.Distance(a, b);
        if (d <= maxStep) return b;
        return new Vec2L(a.X + ScaleTowardZero(b.X - a.X, maxStep, d), a.Y + ScaleTowardZero(b.Y - a.Y, maxStep, d));
    }

    /// <summary>
    /// v*num/den rounded TOWARD ZERO (sign-symmetric): floor rounding would move entities heading -x/-y up to 1 mm per step
    /// farther than mirror-image entities heading +x/+y. Removing that asymmetry did NOT measurably change the 10-seed
    /// outcome distribution (D-tactical.md §9: a residual East/West bias remains, cause not isolated).
    /// </summary>
    private static long ScaleTowardZero(long v, long num, long den)
        => v >= 0 ? FixedMath.MulDivFloor(v, num, den) : -FixedMath.MulDivFloor(-v, num, den);

    /// <summary>
    /// Inactivity-rule minima: per entity a running minimum distance to (a) the nearest TRACKED enemy, (b) the nearest objective
    /// of its side, (c) its exit edge while retreating/routing. A primary activity is recorded only when the distance falls at
    /// least one cell below the last RECORDED minimum; the first measurement only initialises. The minima are never reset
    /// (not by new orders either), so each is a bounded, monotone potential (oscillation cannot create new minima).
    /// </summary>
    private void UpdateMinima()
    {
        long S = _grid.CellSizeMm;
        foreach (var e in _e)
        {
            if (!e.OnField) continue;
            int en = NearestTracked(e, long.MaxValue, out _);
            if (en >= 0) Track(ref e.BestEnemy, Vec2L.Distance(e.Pos, _e[en].Pos), S, e);
            var objs = _setup.Sides[e.Side].Objectives;
            if (objs.Length > 0)
            {
                long best = long.MaxValue;
                foreach (var o in objs) best = Math.Min(best, Vec2L.Distance(e.Pos, o));
                Track(ref e.BestObj, best, S, e);
            }
            if (e.State == EntityState.Routing || _f[e.F].Dir == OrderKind.Retreat)
                Track(ref e.BestEdge, _edge[e.Side].LineDistanceMm(e.Pos), S, e);
        }
    }

    private static void Track(ref long best, long d, long cell, Ent e)
    {
        if (best < 0) { best = d; return; }
        if (d <= best - cell) { best = d; e.Primary = true; }
    }

    // ------------------------------------------------------------------ detection + target choice

    private void Detect(long t)
    {
        long S = _grid.CellSizeMm;
        if (_hasDomain) DomainPreDetect();        // lane N hook
        for (int side = 0; side < 2; side++)
        {
            var k = _k[side];
            for (int ti = 0; ti < _e.Length; ti++)
            {
                var tg = _e[ti];
                if (tg.Side == side || !tg.OnField) continue;
                bool contact = false, visible = false;
                int contactObs = -1, bestObs = -1;
                Int128 contactD = 0;
                Q32 best = Q32.Zero;
                for (int oi = 0; oi < _e.Length; oi++)
                {
                    var ob = _e[oi];
                    if (ob.Side != side || !ob.OnField) continue;
                    Int128 dsq = Vec2L.DistanceSq(ob.Pos, tg.Pos);
                    if (dsq <= (Int128)_contactMm * _contactMm && (!contact || dsq < contactD))
                    {
                        contact = true; contactD = dsq; contactObs = oi;
                    }
                    long sight = ob.P.SightCells * S;
                    if (dsq > (Int128)sight * sight) continue;
                    if (visible && (k.Tracked[ti] || best == Q32.One)) continue;   // a tracked target needs one LOS; clarity cannot exceed 1
                    var los = LineOfSight.Query(_grid, _conceal, ob.Pos, _r.EyeHeightMm, tg.Pos, _r.EyeHeightMm);
                    if (!los.Visible) continue;
                    visible = true;
                    if (bestObs < 0 || los.Clarity > best) { best = los.Clarity; bestObs = oi; }
                }
                if (k.Tracked[ti])
                {
                    if (visible || contact) { k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t; }
                    else k.Tracked[ti] = false;
                    continue;
                }
                bool detect = contact;
                if (!detect && visible)
                {
                    bool exposed = tg.Moved || tg.RevealUntilMs >= t || best == Q32.One;
                    Q32 p = exposed ? best : best.Mul(_r.StationaryConcealment);
                    if (tg.P.PersonalConcealment != Q32.One && tg.RevealUntilMs < t) p = p.Mul(tg.P.PersonalConcealment);   // lane N: snipers
                    detect = CounterRng.Chance(_setup.Seed, TacticalStreams.Detection, tg.Id, t, side, 0, p);
                }
                if (!detect) continue;
                k.Tracked[ti] = true; k.LastSeen[ti] = tg.Pos; k.LastSeenMs[ti] = t;
                if (!k.Ever[ti])
                {
                    k.Ever[ti] = true;
                    _e[contact ? contactObs : bestObs].Primary = true;      // new first detection
                }
            }
        }

        if (_hasDomain) DomainKnowledge(t);       // lane N hook: vessel / aircraft observers; knowledge of vessels / aircraft

        // Enemy discs per side for direct fire (positions are fixed until the next Step).
        for (int side = 0; side < 2; side++)
        {
            int n = 0;
            foreach (var e in _e) if (e.Side != side && e.OnField) n++;
            var d = new Disc[n];
            int m = 0;
            foreach (var e in _e) if (e.Side != side && e.OnField) d[m++] = new Disc(e.Id, e.Pos, _r.EntityRadiusMm);
            _enemyDiscs[side] = d;
        }

        // Target choice (ascending id): melee if an enemy is in contact, else ranged within weapon range.
        foreach (var a in _e)
        {
            if (a.State != EntityState.Active) continue;
            var f = _f[a.F];
            if (f.Dir == OrderKind.Retreat) continue;
            int tgt = NearestTracked(a, _contactMm, out _);
            if (tgt >= 0)
            {
                if (a.P.Melee > 0) ScheduleFire(t, a, _e[tgt], 1);
                continue;
            }
            if (a.P.IsRanged && FiringStance(f.Dir) && !a.Moved && t >= a.NextRangedMs)   // lane N: fire interval (snipers)
            {
                tgt = NearestTracked(a, a.P.RangeCells * S, out _);
                if (tgt >= 0) ScheduleFire(t, a, _e[tgt], 2);
            }
        }
        if (_hasDomain) DomainTargets(t);         // lane N hook: domain weapons, boarding, ramming
    }

    private void ScheduleFire(long t, Ent a, Ent tg, long mode)
        => _s.Schedule(t, EventClass.Fire, 0, a.Id, TacticalKinds.Fire, new EntityRef(tg.Id, tg.Gen), new EntityRef(a.Id, a.Gen), mode);

    /// <summary>
    /// Nearest enemy entity that is on the field and TRACKED by e's side, within maxDist (mm), by composite key
    /// (distance², entity id) — ascending-id scan with strict improvement keeps the lower id on ties. -1 when none.
    /// </summary>
    private int NearestTracked(Ent e, long maxDist, out Int128 dsq)
    {
        var k = _k[e.Side];
        int best = -1; dsq = 0;
        Int128 lim = maxDist == long.MaxValue ? Int128.MaxValue : (Int128)maxDist * maxDist;
        for (int i = 0; i < _e.Length; i++)
        {
            var x = _e[i];
            if (x.Side == e.Side || !x.OnField || !k.Tracked[i]) continue;
            Int128 d = Vec2L.DistanceSq(e.Pos, x.Pos);
            if (d > lim) continue;
            if (best < 0 || d < dsq) { best = i; dsq = d; }
        }
        return best;
    }

    // ------------------------------------------------------------------ fire / damage

    private Q32 HitChance(long attack, long defence)
    {
        Q32 p = attack + defence <= 0 ? Q32.Zero : Q32.FromRatio(attack, attack + defence);
        if (p < _r.HitMin) p = _r.HitMin;
        if (p > _r.HitMax) p = _r.HitMax;
        return p;
    }

    private long DamageOf(long attack) => Math.Max(1, attack / _r.DamageDivisor);

    private void Fire(long t, in ScheduledEvent ev)
    {
        var a = _e[EntIndex(ev.Source.Id)];
        var tg = _e[EntIndex(ev.Target.Id)];
        var fa = _f[a.F];
        if (ev.A == 1)
        {
            a.Primary = true; a.RevealUntilMs = t + _r.StepMs; fa.Attacks++;
            if (CounterRng.Chance(_setup.Seed, TacticalStreams.Hit, a.Id, t, tg.Id, 0, HitChance(fa.EffMelee, _f[tg.F].EffDefence)))
                _s.Schedule(t, EventClass.Damage, 0, tg.Id, TacticalKinds.Damage, new EntityRef(tg.Id, tg.Gen), new EntityRef(a.Id, a.Gen), DamageOf(fa.EffMelee));
            return;
        }
        var r = Projectiles.DirectFire(_grid, a.Pos, _r.MuzzleHeightMm, tg.Pos, _r.AimHeightMm, _enemyDiscs[a.Side], a.Id);
        if (r.Outcome == FireOutcome.Blocked) return;          // not a valid shot: no activity
        a.Primary = true; a.RevealUntilMs = t + _r.StepMs; fa.Attacks++;
        a.NextRangedMs = t + a.P.FireIntervalSteps * _r.StepMs;      // lane N: FireIntervalSteps (default 1 = next step)
        if (r.Outcome != FireOutcome.Hit) return;
        var struck = _e[EntIndex(r.EntityId)];
        if (CounterRng.Chance(_setup.Seed, TacticalStreams.Hit, a.Id, t, struck.Id, 0, HitChance(fa.EffRanged, _f[struck.F].EffDefence)))
            _s.Schedule(t, EventClass.Damage, 0, struck.Id, TacticalKinds.Damage, new EntityRef(struck.Id, struck.Gen), new EntityRef(a.Id, a.Gen), DamageOf(fa.EffRanged));
    }

    private void Damage(in ScheduledEvent ev)
    {
        var tg = _e[EntIndex(ev.Target.Id)];
        var src = _e[EntIndex(ev.Source.Id)];
        long dmg = Math.Min(ev.A, tg.Hp);
        tg.Hp -= dmg;
        tg.Primary = true; src.Primary = true;
        _f[tg.F].DamageTaken += dmg; _f[src.F].DamageDealt += dmg;
    }

    // ------------------------------------------------------------------ resolve

    private void Resolve(long t)
    {
        if (_hasDomain) DomainResolve(t);         // lane N hook: losses aboard, sinking, capture, aircraft down, vessel morale
        foreach (var e in _e)
            if (e.OnField && e.Hp <= 0) { SetState(e, EntityState.Dead); _f[e.F].KilledStep++; }
        foreach (var f in _f)
        {
            if (f.KilledStep > 0)
            {
                long loss = ConditionMath.MoraleLoss(f.KilledStep, _r.MoraleLossFullStrength, f.Start, f.Spec.Leadership);
                f.Morale = Math.Max(0, f.Morale - loss);
                f.CasualtiesPulse += f.KilledStep;
                f.KilledStep = 0;
            }
            if (!f.Routed && f.Morale < _r.RoutThreshold && CountState(f, EntityState.Active) > 0)
            {
                f.Routed = true;
                foreach (int i in f.Members) if (_e[i].State == EntityState.Active) SetState(_e[i], EntityState.Routing);
            }
        }
        foreach (var e in _e)
        {
            if (e.State != EntityState.Active || _f[e.F].Dir != OrderKind.Rally) continue;
            long h = Math.Min(Math.Min(_r.HealPerStep, e.HealPool), e.P.MaxHp - e.Hp);
            if (h > 0) { e.Hp += h; e.HealPool -= h; }
        }
        foreach (var e in _e)
        {
            bool routing = e.State == EntityState.Routing;
            if (!routing && !(e.State == EntityState.Active && _f[e.F].Dir == OrderKind.Retreat)) continue;
            var c = _grid.CellOf(e.Pos);
            if (_edge[e.Side].OnEdge(c.X, c.Y)) { e.RoutedEscape = routing; SetState(e, EntityState.Escaped); continue; }
            if (!routing) continue;
            if (_edge[e.Side].DistanceAt(_grid.Index(c.X, c.Y)) == EdgeField.Unreachable) { SetState(e, EntityState.Surrendered); continue; }
            if (e.WantedMove && !e.Moved && EnemyActiveInContact(e)) SetState(e, EntityState.Surrendered);
        }
        CheckElimination(t);
    }

    private bool EnemyActiveInContact(Ent e)
    {
        Int128 lim = (Int128)_contactMm * _contactMm;
        foreach (var x in _e)
            if (x.Side != e.Side && x.State == EntityState.Active && Vec2L.DistanceSq(x.Pos, e.Pos) <= lim) return true;
        return false;
    }

    private void SetState(Ent e, EntityState s)
    {
        var old = e.State;
        e.State = s;
        e.Gen = checked(e.Gen + 1);
        e.Primary = true;
        _f[e.F].StateChanged = true;
        if (s != EntityState.Active && s != EntityState.Routing)
        {
            int i = EntIndex(e.Id);
            var k = _k[1 - e.Side];
            if (k.Tracked[i]) { k.Tracked[i] = false; k.Gone[i] = true; }   // the enemy saw it fall / leave
        }
        OnLandStateChanged(e, old, s);        // lane N hook: personnel ledger transfer
    }

    private void CheckElimination(long t)
    {
        int a0 = 0, a1 = 0;
        foreach (var e in _e) if (e.State == EntityState.Active) { if (e.Side == 0) a0++; else a1++; }
        if (_hasDomain) DomainActiveCounts(ref a0, ref a1);       // lane N hook
        if (a0 > 0 && a1 > 0) return;
        if (a0 == 0 && a1 == 0) End(t, BattleEndReason.BothEliminated, -1);
        else End(t, BattleEndReason.SideEliminated, a0 == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ pulse end: morale recovery + inactivity rule

    /// <summary>
    /// The five-pulse inactivity rule (brief §2; spec §4):
    ///  - PRIMARY activity of an entity in the pulse (bounded-progress events): dealt or received damage; attacked a valid
    ///    target (a melee blow, or a ranged shot not blocked by terrain); credited with a side's FIRST detection of an enemy
    ///    entity; a new running minimum (by &gt;= one cell) of its distance to a tracked enemy, an objective of its side, or (while
    ///    retreating/routing) its exit edge; changed state (rout, surrender, escape, death).
    ///  - ACTIVE CONCEALMENT: an entity that is concealed (its cell's concealment multiplier &lt; 1 and the enemy does not track
    ///    it) is active iff some enemy entity with primary activity this pulse is within that enemy's sight range of it (the
    ///    distance at which it would be detected if not concealed), measured at pulse end: it is hiding from someone who is
    ///    actually searching / advancing.
    ///  - A formation is active iff one of its on-field entities is active or one of its entities changed state this pulse.
    ///    Counter per formation, reset on activity. A side is disengaged when every formation it still has on the field has
    ///    counter &gt;= InactivityPulses; its entities become Escaped (preserved, counted as survivors) — never deleted.
    /// </summary>
    private void PulseEnd(long t, long p)
    {
        foreach (var f in _f)
        {
            if (f.Routed || CountState(f, EntityState.Active) == 0 || f.CasualtiesPulse != 0) continue;
            long rec = f.Dir == OrderKind.Hold ? _r.HoldRecovery : f.Dir == OrderKind.Rally ? _r.RallyRecovery : 0;
            f.Morale = Math.Min(_r.MoraleMax, f.Morale + rec);
        }
        var activeEnt = new bool[_e.Length];
        for (int i = 0; i < _e.Length; i++)
        {
            var e = _e[i];
            if (!e.OnField) continue;
            activeEnt[i] = e.Primary || (_r.ActiveConcealmentRule && IsConcealed(i) && SearcherNear(e));
        }
        bool[] hasField = new bool[2], allInactive = { true, true };
        foreach (var f in _f)
        {
            bool onField = false, act = f.StateChanged;
            foreach (int i in f.Members) if (_e[i].OnField) { onField = true; if (activeEnt[i]) act = true; }
            if (!onField) continue;
            f.Inactive = act ? 0 : checked(f.Inactive + 1);
            hasField[f.Side] = true;
            if (f.Inactive < _r.InactivityPulses) allInactive[f.Side] = false;
        }
        if (_hasDomain) DomainPulseEnd(hasField, allInactive);        // lane N hook
        bool d0 = hasField[0] && allInactive[0], d1 = hasField[1] && allInactive[1];
        if (d0 || d1)
        {
            foreach (var e in _e)
                if (e.OnField && ((e.Side == 0 && d0) || (e.Side == 1 && d1))) { e.Disengaged = true; SetState(e, EntityState.Escaped); }
            if (_hasDomain) DomainDisengage(d0, d1);                    // lane N hook
            End(t, d0 && d1 ? BattleEndReason.BothDisengaged : BattleEndReason.SideDisengaged, d0 && d1 ? -1 : (d0 ? 0 : 1));
            return;
        }
        _s.Schedule(t, EventClass.System, 1, 0, TacticalKinds.PulseStart, p + 1, t);
    }

    private bool IsConcealed(int i)
    {
        var e = _e[i];
        var c = _grid.CellOf(e.Pos);
        return (_conceal.Multiplier(_grid.ClassAt(c.X, c.Y)) < Q32.One || e.P.PersonalConcealment < Q32.One)   // lane N: snipers
               && !_k[1 - e.Side].Tracked[i];
    }

    private bool SearcherNear(Ent e)
    {
        foreach (var x in _e)
        {
            if (x.Side == e.Side || !x.OnField || !x.Primary) continue;
            long env = x.P.SightCells * _grid.CellSizeMm;
            if (Vec2L.DistanceSq(x.Pos, e.Pos) <= (Int128)env * env) return true;
        }
        return _hasDomain && DomainSearcherNear(e);       // lane N hook
    }

    // ------------------------------------------------------------------ end + aggregation

    private void End(long t, BattleEndReason reason, int side)
    {
        foreach (var e in _e)
            if (e.State == EntityState.Routing) { e.RoutedEscape = true; SetState(e, EntityState.Escaped); }   // fled when the field was decided
        if (_hasDomain) DomainEndStart();         // lane N hook
        var fr = new FormationResult[_f.Length];
        for (int i = 0; i < _f.Length; i++)
        {
            var f = _f[i];
            long act = 0, esc = 0, resc = 0, desc = 0, dead = 0, sur = 0, emb = 0;
            foreach (int m in f.Members)
            {
                var e = _e[m];
                switch (e.State)
                {
                    case EntityState.Active: act++; break;
                    case EntityState.Embarked: emb++; break;       // lane N: still aboard a transport (a survivor)
                    case EntityState.Escaped: esc++; if (e.RoutedEscape) resc++; if (e.Disengaged) desc++; break;
                    case EntityState.Dead: dead++; break;
                    case EntityState.Surrendered: sur++; break;
                    default: throw new InvalidOperationException("routing entity left after End");
                }
            }
            fr[i] = new FormationResult(f.Id, f.Side, f.Spec.Domain, f.P.FamilyKey, f.Start, act + esc + emb, dead, sur, act, esc, resc, desc,
                                        f.Morale, f.DamageDealt, f.DamageTaken, f.Attacks) { EmbarkedAtEnd = emb };
            if (!fr[i].Conserved) throw new InvalidOperationException($"conservation violated for formation {f.Id}");
        }
        int holds = -1;
        bool any0 = false, any1 = false;
        foreach (var e in _e) if (e.State == EntityState.Active) { if (e.Side == 0) any0 = true; else any1 = true; }
        if (_hasDomain)       // lane N hook: vessels and aircraft also hold the field
        {
            int h0 = 0, h1 = 0;
            DomainActiveCounts(ref h0, ref h1, onFieldOnly: true);
            any0 |= h0 > 0; any1 |= h1 > 0;
        }
        if (any0 && !any1) holds = 0; else if (any1 && !any0) holds = 1;
        fr = DomainResults(fr);       // lane N hook (unchanged when there are no vessel / air formations)
        _result = new BattleResult(ResolverKind.Tactical, reason, side, holds, _pulse + 1, t, _s.ExecutedCount, _s.DroppedCount,
                                   _s.Trace.HashHex(), StateHashHex(), fr) { Personnel = _ledger.Report() };
    }

    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_e.Length);
        foreach (var e in _e)
        {
            L(e.Id); L((long)e.State); L(e.Hp); L(e.Pos.X); L(e.Pos.Y); L(e.Gen); L(e.HealPool);
            L(e.RoutedEscape ? 1 : 0); L(e.Disengaged ? 1 : 0); L(e.BestEnemy); L(e.BestObj); L(e.BestEdge);
        }
        L(_f.Length);
        foreach (var f in _f) { L(f.Id); L(f.Morale); L(f.Inactive); L((long)f.Dir); L(f.DirTarget); L(f.Anchor.X); L(f.Anchor.Y); }
        L(_log.Count);
        foreach (var o in _log)
        {
            L(o.Pulse); L(o.Side); L(o.AtMs); L(o.Request.FormationId); L((long)o.Request.Kind); L(o.Request.StepOffset);
            L(o.Request.TargetFormationId); L(o.Request.Pos.X); L(o.Request.Pos.Y); L((long)o.Invalid); L((long)o.Budget);
        }
        for (int s = 0; s < 2; s++)
            for (int i = 0; i < _e.Length; i++) L((_k[s].Tracked[i] ? 1 : 0) | (_k[s].Ever[i] ? 2 : 0) | (_k[s].Gone[i] ? 4 : 0));
        if (_hasDomain) AppendDomainState(h);     // lane N hook
        return Convert.ToHexString(h.GetHashAndReset());
    }

    // ------------------------------------------------------------------ helpers

    private bool KnownCentroid(int side, long formationId, out Vec2L c)
    {
        c = default;
        int fi = FindForm(formationId);
        if (fi < 0) return false;
        var k = _k[side];
        long sx = 0, sy = 0, n = 0;
        foreach (int i in _f[fi].Members)
            if (k.Ever[i] && !k.Gone[i]) { sx += k.LastSeen[i].X; sy += k.LastSeen[i].Y; n++; }
        if (n == 0) return false;
        c = new Vec2L(FixedMath.DivFloor(sx, n), FixedMath.DivFloor(sy, n));
        return true;
    }

    private Vec2L Centroid(Form f)
    {
        long sx = 0, sy = 0, n = 0;
        foreach (int i in f.Members) if (_e[i].OnField) { sx += _e[i].Pos.X; sy += _e[i].Pos.Y; n++; }
        return n == 0 ? f.Anchor : new Vec2L(FixedMath.DivFloor(sx, n), FixedMath.DivFloor(sy, n));
    }

    private int CountState(Form f, EntityState s)
    {
        int n = 0;
        foreach (int i in f.Members) if (_e[i].State == s) n++;
        return n;
    }

    private void RequirePassable(Vec2L p, string what)
    {
        if (!p.InDocumentedRange) throw new ArgumentOutOfRangeException(what);
        var c = _grid.CellOf(p);
        if (!_grid.IsPassable(_costs, c.X, c.Y)) throw new ArgumentException($"{what} is not on a passable cell");
    }

    private int FindForm(long id) => Array.BinarySearch(_fids, id);
    private int FormIndex(long id) { int i = FindForm(id); if (i < 0) throw new ArgumentException("unknown formation"); return i; }
    private int EntIndex(long id) { int i = Array.BinarySearch(_ids, id); if (i < 0) throw new ArgumentException("unknown entity"); return i; }
}
