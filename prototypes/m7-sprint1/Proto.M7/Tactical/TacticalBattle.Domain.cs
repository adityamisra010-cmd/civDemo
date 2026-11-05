using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>Event kinds of the naval / air / combined-arms modules (stable codes; part of the trace hash).</summary>
public static class DomainKinds
{
    /// <summary>A domain weapon fires (Fire class). A = mount index, B/C = aim point (area fire), Target = entity or none.</summary>
    public const int DomainFire = 121;
    /// <summary>Effect lands (Damage class). A = effect kind (0 land HP, 1 vessel hull+people, 2 airframe HP; [RAM] 3 a rammer's
    /// own bow — vessel hull, no people, applied like 1), B, C = amounts.</summary>
    public const int DomainHit = 122;
    /// <summary>Boarding fight between two grappled vessels (Fire class).</summary>
    public const int Boarding = 123;
    /// <summary>Ram strike (Fire class). [RAM] A = <see cref="RamOutcome"/>, B = normal closing speed v_n, C = closing speed v_c (mm/s).</summary>
    public const int Ram = 124;
    /// <summary>[RAM] Bow contact under the ram directive that is NOT a ram (D-081): normal closing speed v_n below ramming speed
    /// ([r2] was the centre-line v_c), or a hull this rammer has already rammed (RM1). B = v_n, C = v_c (mm/s). Fire class.</summary>
    public const int RamCollision = 125;
}

/// <summary>Counter-RNG streams of the domain modules (distinct from the land streams).</summary>
public static class DomainStreams
{
    public const uint Hit = 0x7AD0_0001;
    public const uint Dispersion = 0x7AD0_0002;
    public const uint Boarding = 0x7AD0_0003;
    public const uint Victims = 0x7AD0_0004;
    public const uint MovePriority = 0x7AD0_0005;
    public const uint Blast = 0x7AD0_0006;
}

/// <summary>Read-only snapshot of one vessel or aircraft entity (tests, inspection).</summary>
public readonly record struct DomainEntityView(long Id, long FormationId, int Side, Domain Domain, EntityState State, AirPhase Phase,
    Vec2L Pos, int Heading, long Speed, long Hp, long Crew, long Embarked, long Fuel, int SortiesLeft, int Generation);

/// <summary>
/// LANE N — the naval, air and combined-arms modules of the TacticalResolver (DIR5 §2, §2.1; m7-spec REV:418-433). They are
/// part of the SAME <see cref="TacticalBattle"/>: one clock, one event queue and order of resolution, one id space, one
/// order log and replay, one knowledge model (m7-spec I.1). Domain-specific equations live here; the land code calls in
/// through a handful of delimited hooks (see the "lane N hook" comments in TacticalBattle.cs). With no vessel, aircraft,
/// domain weapon or embarked formation in the setup, every hook is a no-op and the land battle is bit-identical to Slice D
/// (the existing trace/state goldens are unchanged — tested).
///
/// Entities: one vessel entity = one hull; one aircraft entity = one airframe (CAP-3 counted objects). Vessel hull points
/// and airframe HP are integrity (not conserved); crews and embarked troops are PERSONNEL, conserved through the
/// <see cref="PersonnelLedger"/>. Order of resolution at one instant is the land order (System → Arrival → Detection →
/// Fire → Damage → Resolve); inside a phase every loop runs in ascending entity / formation id, and every random draw is a
/// counter-RNG draw keyed by (stream, ids, instant), so input order and unrelated events cannot change any outcome.
/// </summary>
public sealed partial class TacticalBattle
{
    private sealed class DEnt
    {
        public long Id; public int F; public int Side; public int Slot; public Domain Dom;
        public VesselClass? V; public AirframeClass? A;
        public Vec2L Pos; public int Heading; public long Speed;
        public long Hp; public long Crew; public EntityState State; public int Gen;
        public AirPhase Phase; public long Fuel; public int SortiesLeft; public long ReadyAtMs;
        public long[] NextFireMs = Array.Empty<long>(); public int[] Ammo = Array.Empty<int>();
        public bool Primary, Moved, RoutedEscape, Disengaged;
        public long BestEnemy = -1, BestObj = -1, BestEdge = -1;
        public long OpCarryMs;
        public long RepairPool;
        public long Embarked;
        public long PendingEmbarkedCasualties;
        public long FightStartAboard; public int BoardedBy = -1; public bool CrashPending;
        public int CrewAccount;
        public long Aboard => checked(Crew + Embarked);
        /// <summary>Still part of the battle (aircraft: including at base).</summary>
        public bool Present => State == EntityState.Active || State == EntityState.Routing;
        /// <summary>Physically on the battlefield: can see, be seen, move, fire, be engaged.</summary>
        public bool OnField => Present && (Dom != Domain.Air || Phase != AirPhase.Based);
        public long AltitudeMm => Dom == Domain.Air ? A!.AltitudeMm : 0;
    }

    private sealed class DForm
    {
        public long Id; public int Side; public FormationSpec Spec = null!; public Domain Dom;
        public VesselClass? V; public AirframeClass? A;
        public int[] Members = Array.Empty<int>(); public int Start; public long StartPersonnel;
        public ActionBudget Budget = null!; public OrderKind Dir = OrderKind.Hold; public long DirTarget = -1; public Vec2L DirPos;
        public Vec2L[] Path = Array.Empty<Vec2L>(); public int PathIdx; public int PathLeader = -1;
        public long Morale; public bool Routed; public int Inactive; public bool StateChanged; public long CasualtiesPulse; public long KilledStep;
        public long DamageDealt, DamageTaken, Attacks;
        public int AccKilled, AccLost, AccCaptured, AccEscaped;
    }

    private sealed class DKnow
    {
        public bool[] Tracked = null!, Ever = null!, Gone = null!;
        public Vec2L[] LastSeen = null!;
        public long[] LastSeenMs = null!;
    }

    private struct LandAccounts { public int Field, Killed, Lost, Captured, Escaped; }

    // ------------------------------------------------------------------ domain state (empty in a land-only battle)

    private bool _hasDomain;
    private DomainRules _dr = new();
    private DEnt[] _d = Array.Empty<DEnt>();
    private long[] _dids = Array.Empty<long>();
    private DForm[] _df = Array.Empty<DForm>();
    private long[] _dfids = Array.Empty<long>();
    private DKnow[] _dk = Array.Empty<DKnow>();
    private TerrainGrid? _nav;
    private GridPathfinder? _navPf;
    private EdgeField[]? _navEdge;
    private Vec2L[] _airBase = new Vec2L[2];
    private int[] _carrier = Array.Empty<int>();            // land entity index -> carrying vessel index (-1 none)
    private int[] _embarkOnto = Array.Empty<int>();         // land formation index -> transport DForm index (-1 none)
    private bool[] _landingSet = Array.Empty<bool>();       // land formation index -> a Disembark order is standing
    private Vec2L[] _landingTo = Array.Empty<Vec2L>();
    private long[][]? _lwNext;                               // land entity -> per-mount next fire instant
    private int[][]? _lwAmmo;
    private bool[][] _preTracked = Array.Empty<bool[]>();
    private readonly PersonnelLedger _ledger = new();
    private LandAccounts[] _lacc = Array.Empty<LandAccounts>();
    private PersonnelFlowReason? _deathReason;

    public PersonnelLedger Personnel => _ledger;
    public bool HasDomainModules => _hasDomain;

    // ------------------------------------------------------------------ construction (called at the end of the constructor)

    private void DomainInit(FormationSpec[] dspecs)
    {
        _dr = _setup.MultiDomain?.Rules ?? new DomainRules();
        _dr.Validate();
        _carrier = new int[_e.Length];
        Array.Fill(_carrier, -1);
        _embarkOnto = new int[_f.Length];
        Array.Fill(_embarkOnto, -1);
        _landingSet = new bool[_f.Length];
        _landingTo = new Vec2L[_f.Length];
        bool landDomain = false;
        foreach (var f in _f) if (f.Spec.Weapons is not null || f.Spec.EmbarkedOn is not null) landDomain = true;
        foreach (var f in _f)
            if (f.Spec.Vessel is not null || f.Spec.Airframe is not null)
                throw new ArgumentException($"formation {f.Id}: a Land formation cannot carry a vessel or airframe class");
        _hasDomain = dspecs.Length > 0 || landDomain;

        // Air bases: given, or the centre of the side's exit edge.
        for (int side = 0; side < 2; side++)
        {
            Vec2L? given = _setup.MultiDomain?.AirBases is { } ab && ab.Length == 2 ? ab[side] : null;
            _airBase[side] = given ?? EdgeCentre(_setup.Sides[side].Exit);
            if (!_airBase[side].InDocumentedRange) throw new ArgumentOutOfRangeException(nameof(_setup), "air base out of range");
        }

        bool anyNaval = false;
        foreach (var s in dspecs) if (s.Domain == Domain.Naval) anyNaval = true;
        if (anyNaval)
        {
            _nav = NavalView.Build(_grid);
            _navPf = new GridPathfinder(_nav, _costs);
            _navEdge = new[] { new EdgeField(_nav, _costs, _setup.Sides[0].Exit), new EdgeField(_nav, _costs, _setup.Sides[1].Exit) };
        }

        // Domain formations and entities (sorted by id).
        _df = new DForm[dspecs.Length];
        _dfids = new long[dspecs.Length];
        var ents = new List<DEnt>();
        for (int i = 0; i < dspecs.Length; i++)
        {
            var s = dspecs[i];
            if (s.Id < 0 || s.Id > 1_000_000_000) throw new ArgumentOutOfRangeException(nameof(dspecs), "formation id out of range");
            if (s.Side != 0 && s.Side != 1) throw new ArgumentException("side must be 0 or 1");
            if (s.Weapons is not null || s.EmbarkedOn is not null) throw new ArgumentException($"formation {s.Id}: Weapons/EmbarkedOn are land-formation fields");
            if (s.Domain == Domain.Naval && (s.Vessel is null || s.Airframe is not null)) throw new ArgumentException($"formation {s.Id}: a Naval formation needs a VesselClass (and no airframe)");
            if (s.Domain == Domain.Air && (s.Airframe is null || s.Vessel is not null)) throw new ArgumentException($"formation {s.Id}: an Air formation needs an AirframeClass (and no vessel)");
            s.Vessel?.Validate();
            s.Airframe?.Validate();
            var cap = s.Vessel?.Cap ?? s.Airframe!.Cap;
            string? capErr = _setup.Caps.Check(cap, s.Count);
            if (capErr is not null) throw new ArgumentException($"formation {s.Id}: {capErr}");
            ConditionMath.Validate(s.Experience, s.Leadership, s.Supply);
            var f = new DForm
            {
                Id = s.Id, Side = s.Side, Spec = s, Dom = s.Domain, V = s.Vessel, A = s.Airframe, Start = s.Count,
                Budget = new ActionBudget(_r.ActionBudgetN), Morale = s.Morale ?? _r.MoraleMax,
            };
            if (f.Morale < 0 || f.Morale > _r.MoraleMax) throw new ArgumentOutOfRangeException(nameof(dspecs), "morale");
            _df[i] = f; _dfids[i] = s.Id;
            int facing = FixedTrig.Atan2(FwdOf(s.Side).Y, FwdOf(s.Side).X);
            long[] eid = new long[s.Count]; Vec2L[] epos = new Vec2L[s.Count];
            if (s.Entities is not null)
            {
                if (s.Entities.Length != s.Count) throw new ArgumentException($"formation {s.Id}: Entities.Length != Count");
                var es = (EntitySpec[])s.Entities.Clone();
                Array.Sort(es, static (a, b) => a.Id.CompareTo(b.Id));
                for (int k = 0; k < es.Length; k++) { eid[k] = es[k].Id; epos[k] = es[k].Pos; }
            }
            else
            {
                if (s.Count >= 1000) throw new ArgumentException("generated ids need Count < 1000");
                for (int k = 0; k < s.Count; k++)
                {
                    eid[k] = checked(s.Id * 1000 + k);
                    epos[k] = s.Domain == Domain.Naval ? s.Anchor - HeadingMotion.Offset(facing, checked(k * LineSpacing(s.Vessel!))) : s.Anchor;
                }
            }
            for (int k = 0; k < s.Count; k++)
            {
                if (!epos[k].InDocumentedRange) throw new ArgumentOutOfRangeException(nameof(dspecs), $"entity {eid[k]} position");
                var d = new DEnt { Id = eid[k], F = i, Side = s.Side, Slot = k, Dom = s.Domain, V = s.Vessel, A = s.Airframe, Heading = facing };
                if (s.Domain == Domain.Naval)
                {
                    if (!NavalView.OnWater(_grid, epos[k])) throw new ArgumentException($"vessel {eid[k]} does not start on a water cell");
                    d.Pos = epos[k]; d.Hp = s.Vessel!.Hull; d.Crew = s.Vessel.Crew;
                    d.RepairPool = FixedMath.MulDivFloor(s.Vessel.Hull, s.Vessel.RepairPoolPercent, 100);
                    d.NextFireMs = new long[s.Vessel.Weapons.Length]; d.Ammo = new int[s.Vessel.Weapons.Length];
                    for (int m = 0; m < d.Ammo.Length; m++) { d.Ammo[m] = s.Vessel.Weapons[m].Ammo; d.NextFireMs[m] = long.MinValue; }
                }
                else
                {
                    d.Pos = _airBase[s.Side]; d.Hp = s.Airframe!.Hp; d.Crew = s.Airframe.Crew; d.Phase = AirPhase.Based;
                    d.Fuel = s.Airframe.EnduranceMs; d.SortiesLeft = s.Airframe.Sorties; d.ReadyAtMs = long.MinValue;
                    d.NextFireMs = new long[s.Airframe.Weapons.Length]; d.Ammo = new int[s.Airframe.Weapons.Length];
                    for (int m = 0; m < d.Ammo.Length; m++) { d.Ammo[m] = s.Airframe.Weapons[m].Ammo; d.NextFireMs[m] = long.MinValue; }
                }
                ents.Add(d);
            }
        }
        ents.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        _d = ents.ToArray();
        _dids = new long[_d.Length];
        for (int i = 0; i < _d.Length; i++)
        {
            if (i > 0 && _d[i].Id == _d[i - 1].Id) throw new ArgumentException($"duplicate entity id {_d[i].Id}");
            if (Array.BinarySearch(_ids, _d[i].Id) >= 0) throw new ArgumentException($"entity id {_d[i].Id} is used by a land entity");
            _dids[i] = _d[i].Id;
        }
        var members = new List<int>[_df.Length];
        for (int i = 0; i < _df.Length; i++) members[i] = new List<int>();
        for (int i = 0; i < _d.Length; i++) members[_d[i].F].Add(i);
        _grappledAt = new long[_d.Length];
        Array.Fill(_grappledAt, long.MinValue);
        RamInit();                                                                   // [RAM] D-081 per-hull ram state
        for (int i = 0; i < _df.Length; i++) _df[i].Members = members[i].ToArray();
        for (int i = 0; i < _d.Length; i++)
            for (int j = i + 1; j < _d.Length; j++)
                if (_d[i].Dom == Domain.Naval && _d[j].Dom == Domain.Naval
                    && Separation.Overlaps(_d[i].Pos, _d[i].V!.RadiusMm, _d[j].Pos, _d[j].V!.RadiusMm))
                    throw new ArgumentException($"vessels {_d[i].Id} and {_d[j].Id} overlap at start");

        // Land formations with domain weapons.
        foreach (var f in _f)
        {
            if (f.Spec.Weapons is null) continue;
            if (f.Spec.Weapons.Length == 0) throw new ArgumentException($"formation {f.Id}: empty Weapons");
            foreach (var w in f.Spec.Weapons) w.Validate();
            _lwNext ??= new long[_e.Length][];
            _lwAmmo ??= new int[_e.Length][];
            foreach (int i in f.Members)
            {
                _lwNext[i] = new long[f.Spec.Weapons.Length];
                Array.Fill(_lwNext[i], long.MinValue);
                _lwAmmo[i] = new int[f.Spec.Weapons.Length];
                for (int m = 0; m < f.Spec.Weapons.Length; m++) _lwAmmo[i][m] = f.Spec.Weapons[m].Ammo;
            }
        }

        // Formations that start aboard transports.
        for (int fi = 0; fi < _f.Length; fi++)
        {
            var f = _f[fi];
            if (f.Spec.EmbarkedOn is not long carrierForm) continue;
            int ti = FindDForm(carrierForm);
            if (ti < 0 || _df[ti].Side != f.Side || _df[ti].Dom != Domain.Naval || _df[ti].V!.Capacity == 0)
                throw new ArgumentException($"formation {f.Id}: EmbarkedOn {carrierForm} is not an own transport formation");
            foreach (int ei in f.Members)
            {
                int carrier = -1;
                foreach (int vi in _df[ti].Members) if (_d[vi].Embarked < _d[vi].V!.Capacity) { carrier = vi; break; }
                if (carrier < 0) throw new ArgumentException($"formation {f.Id}: transport formation {carrierForm} lacks capacity");
                var e = _e[ei];
                e.State = EntityState.Embarked; e.Pos = _d[carrier].Pos;
                _carrier[ei] = carrier; _d[carrier].Embarked++;
            }
        }

        _dk = new DKnow[2];
        for (int s = 0; s < 2; s++)
            _dk[s] = new DKnow
            {
                Tracked = new bool[_d.Length], Ever = new bool[_d.Length], Gone = new bool[_d.Length],
                LastSeen = new Vec2L[_d.Length], LastSeenMs = new long[_d.Length],
            };
        _preTracked = new[] { new bool[_e.Length], new bool[_e.Length] };

        // Personnel ledger: opening balances = where everybody actually is at the start.
        _lacc = new LandAccounts[_f.Length];
        for (int fi = 0; fi < _f.Length; fi++)
        {
            var f = _f[fi];
            long onField = 0;
            foreach (int i in f.Members) if (_e[i].State != EntityState.Embarked) onField++;
            _lacc[fi].Field = _ledger.Open(f.Id, PersonnelPlace.Field, -1, onField);
            for (int vi = 0; vi < _d.Length; vi++)                     // people aboard at the start, per carrier, ascending vessel id
            {
                long n = 0;
                foreach (int i in f.Members) if (_carrier[i] == vi) n++;
                if (n > 0) _ledger.Open(f.Id, PersonnelPlace.Aboard, _d[vi].Id, n);
            }
            _lacc[fi].Killed = _ledger.Open(f.Id, PersonnelPlace.Killed, -1, 0);
            _lacc[fi].Lost = _ledger.Open(f.Id, PersonnelPlace.LostWithVessel, -1, 0);
            _lacc[fi].Captured = _ledger.Open(f.Id, PersonnelPlace.Captured, -1, 0);
            _lacc[fi].Escaped = _ledger.Open(f.Id, PersonnelPlace.Escaped, -1, 0);
        }
        foreach (var f in _df)
        {
            foreach (int i in f.Members)
            {
                _d[i].CrewAccount = _ledger.Open(f.Id, PersonnelPlace.Crew, _d[i].Id, _d[i].Crew);
                f.StartPersonnel = checked(f.StartPersonnel + _d[i].Crew);
            }
            f.AccKilled = _ledger.Open(f.Id, PersonnelPlace.Killed, -1, 0);
            f.AccLost = _ledger.Open(f.Id, PersonnelPlace.LostWithVessel, -1, 0);
            f.AccCaptured = _ledger.Open(f.Id, PersonnelPlace.Captured, -1, 0);
            f.AccEscaped = _ledger.Open(f.Id, PersonnelPlace.Escaped, -1, 0);
        }
    }

    private Vec2L EdgeCentre(ExitEdge e)
    {
        long W = _grid.Width * _grid.CellSizeMm, H = _grid.Height * _grid.CellSizeMm, h = _grid.CellSizeMm / 2;
        return e switch
        {
            ExitEdge.West => new Vec2L(h, H / 2),
            ExitEdge.East => new Vec2L(W - h, H / 2),
            ExitEdge.North => new Vec2L(W / 2, h),
            _ => new Vec2L(W / 2, H - h),
        };
    }

    private Vec2L FwdOf(int side) => _setup.Sides[side].Exit switch
    {
        ExitEdge.West => new Vec2L(1, 0), ExitEdge.East => new Vec2L(-1, 0), ExitEdge.North => new Vec2L(0, 1), _ => new Vec2L(0, -1),
    };

    private long LineSpacing(VesselClass v) => checked(v.RadiusMm * _dr.LineSpacingRadii);

    private int FindDForm(long id) => Array.BinarySearch(_dfids, id);
    private int DIndex(long id) => Array.BinarySearch(_dids, id);

    private bool DomainIsLive(EntityRef r)
    {
        int i = Array.BinarySearch(_dids, r.Id);
        return i >= 0 && _d[i].Gen == r.Generation;
    }

    private bool ExecuteDomain(in ScheduledEvent ev)
    {
        long t = ev.Key.TimeMs;
        switch (ev.Kind)
        {
            case DomainKinds.DomainFire: DomainFire(t, ev); return true;
            case DomainKinds.DomainHit: DomainHit(t, ev); return true;
            case DomainKinds.Boarding: BoardingFight(t, ev); return true;
            case DomainKinds.Ram: RamStrike(t, ev); return true;
            case DomainKinds.RamCollision: RamCollisionStrike(t, ev); return true;     // [RAM]
            default: return false;
        }
    }

    // ------------------------------------------------------------------ inspection surface

    public IReadOnlyList<DomainEntityView> DomainEntities()
    {
        var v = new DomainEntityView[_d.Length];
        for (int i = 0; i < _d.Length; i++)
        {
            var d = _d[i];
            v[i] = new DomainEntityView(d.Id, _df[d.F].Id, d.Side, d.Dom, d.State, d.Phase, d.Pos, d.Heading, d.Speed, d.Hp, d.Crew,
                                        d.Embarked, d.Fuel, d.SortiesLeft, d.Gen);
        }
        return v;
    }

    public bool IsTrackedDomain(int side, long entityId) => _dk[side].Tracked[DIndexOrThrow(entityId)];
    public bool EverDetectedDomain(int side, long entityId) => _dk[side].Ever[DIndexOrThrow(entityId)];
    public OrderKind DomainDirective(long formationId) => _df[FindDFormOrThrow(formationId)].Dir;
    public long DomainMorale(long formationId) => _df[FindDFormOrThrow(formationId)].Morale;
    public int DomainInactivityCounter(long formationId) => _df[FindDFormOrThrow(formationId)].Inactive;
    /// <summary>Vessel entity index carrying a land entity, as the carrier's entity id (-1 = not aboard).</summary>
    public long CarrierOf(long landEntityId) { int c = _carrier[EntIndex(landEntityId)]; return c < 0 ? -1 : _d[c].Id; }
    /// <summary>Instant until which a land entity is exposed (landing exposure, or a shot it fired); inspection only.</summary>
    public long RevealedUntilMs(long landEntityId) => _e[EntIndex(landEntityId)].RevealUntilMs;

    /// <summary>
    /// Cross-check of the personnel ledger against the entity states (tests, inspection). Null = consistent: the ledger total
    /// equals its opening total; for every land formation Field = Active + Routing, Aboard (per carrier) = Embarked on that
    /// carrier, Killed + LostWithVessel = Dead, Captured = Surrendered, Escaped = Escaped; for every vessel / aircraft the
    /// Crew account equals its crew count.
    /// </summary>
    public string? PersonnelConsistency()
    {
        if (_ledger.Total != _ledger.OpeningTotal) return $"ledger total {_ledger.Total} != opening {_ledger.OpeningTotal}";
        for (int fi = 0; fi < _f.Length; fi++)
        {
            var f = _f[fi];
            long field = 0, emb = 0, dead = 0, sur = 0, esc = 0;
            foreach (int i in f.Members)
                switch (_e[i].State)
                {
                    case EntityState.Active or EntityState.Routing: field++; break;
                    case EntityState.Embarked: emb++; break;
                    case EntityState.Dead: dead++; break;
                    case EntityState.Surrendered: sur++; break;
                    default: esc++; break;
                }
            var a = _lacc[fi];
            if (_ledger.Balance(a.Field) != field) return $"F{f.Id} Field {_ledger.Balance(a.Field)} != {field}";
            if (_ledger.Balance(f.Id, PersonnelPlace.Aboard) != emb) return $"F{f.Id} Aboard {_ledger.Balance(f.Id, PersonnelPlace.Aboard)} != {emb}";
            if (_ledger.Balance(a.Killed) + _ledger.Balance(a.Lost) != dead) return $"F{f.Id} Killed+Lost != Dead {dead}";
            if (_ledger.Balance(a.Captured) != sur) return $"F{f.Id} Captured != {sur}";
            if (_ledger.Balance(a.Escaped) != esc) return $"F{f.Id} Escaped != {esc}";
            for (int vi = 0; vi < _d.Length; vi++)
            {
                long n = 0;
                foreach (int i in f.Members) if (_carrier[i] == vi) n++;
                long acc = n == 0 ? 0 : _ledger.Balance(_ledger.Account(f.Id, PersonnelPlace.Aboard, _d[vi].Id));
                if (acc != n) return $"F{f.Id} aboard vessel {_d[vi].Id}: ledger {acc} != {n}";
                if (n > 0 && _d[vi].Embarked < n) return $"vessel {_d[vi].Id} Embarked count {_d[vi].Embarked} < {n}";
            }
        }
        foreach (var d in _d)
            if (_ledger.Balance(d.CrewAccount) != d.Crew) return $"crew account of {d.Id}: {_ledger.Balance(d.CrewAccount)} != {d.Crew}";
        return null;
    }

    private int DIndexOrThrow(long id) { int i = DIndex(id); if (i < 0) throw new ArgumentException("unknown domain entity"); return i; }
    private int FindDFormOrThrow(long id) { int i = FindDForm(id); if (i < 0) throw new ArgumentException("unknown domain formation"); return i; }

    // ------------------------------------------------------------------ personnel ledger glue

    /// <summary>Hook from SetState (land entities): mirror the state change as a ledger transfer.</summary>
    private void OnLandStateChanged(Ent e, EntityState from, EntityState to)
    {
        int fi = e.F;
        int a = LandAccount(e, from), b = LandAccount(e, to);
        if (a == b) return;
        var reason = to switch
        {
            EntityState.Dead => _deathReason ?? PersonnelFlowReason.KilledInAction,
            EntityState.Surrendered => PersonnelFlowReason.Captured,
            EntityState.Escaped => _deathReason == PersonnelFlowReason.ShipwreckSurvivor ? PersonnelFlowReason.ShipwreckSurvivor : PersonnelFlowReason.Escaped,
            EntityState.Embarked => PersonnelFlowReason.Embark,
            _ => PersonnelFlowReason.Disembark,
        };
        if (to == EntityState.Dead && reason == PersonnelFlowReason.LostWithVessel) b = _lacc[fi].Lost;
        _ledger.Transfer(a, b, 1, _s.NowMs, reason);
    }

    private int LandAccount(Ent e, EntityState s)
    {
        var acc = _lacc[e.F];
        return s switch
        {
            EntityState.Active or EntityState.Routing => acc.Field,
            EntityState.Embarked => _ledger.Account(_f[e.F].Id, PersonnelPlace.Aboard, _d[_carrier[EntIndex(e.Id)]].Id),
            EntityState.Dead => acc.Killed,
            EntityState.Surrendered => acc.Captured,
            _ => acc.Escaped,
        };
    }

    /// <summary>Embark / land transition: NOT a primary activity (a reversible move cannot keep a battle alive — termination).</summary>
    private void SetEmbarkState(Ent e, EntityState s, int carrier)
    {
        int i = EntIndex(e.Id);
        var old = e.State;
        if (s == EntityState.Embarked) { _carrier[i] = carrier; _d[carrier].Embarked++; }
        OnLandStateChanged(e, old, s);
        if (s != EntityState.Embarked) { _d[_carrier[i]].Embarked--; _carrier[i] = -1; }
        e.State = s;
        e.Gen = checked(e.Gen + 1);
        var k = _k[1 - e.Side];
        if (s == EntityState.Embarked && k.Tracked[i]) k.Tracked[i] = false;     // seen boarding; now inside a hull
    }

    /// <summary>A land entity aboard a vessel leaves the battle with it (sunk, captured, escaped).</summary>
    private void LeaveWithCarrier(int landIndex, EntityState s, PersonnelFlowReason? reason)
    {
        var e = _e[landIndex];
        int carrier = _carrier[landIndex];
        _deathReason = reason;
        SetState(e, s);
        _deathReason = null;
        _d[carrier].Embarked--;
        _carrier[landIndex] = -1;
        if (s == EntityState.Dead) _f[e.F].KilledStep++;
    }

    private void SetDState(DEnt d, EntityState s)
    {
        d.State = s;
        d.Gen = checked(d.Gen + 1);
        d.Primary = true;
        _df[d.F].StateChanged = true;
        if (s != EntityState.Active && s != EntityState.Routing)
        {
            int i = DIndex(d.Id);
            var k = _dk[1 - d.Side];
            if (k.Tracked[i]) { k.Tracked[i] = false; k.Gone[i] = true; }
        }
    }

    // ------------------------------------------------------------------ orders

    /// <summary>Budget class of an order kind: every lane-N kind is charged as one of the six land kinds.</summary>
    public static OrderKind BudgetClass(OrderKind k) => ActionBudget.ClassOf(k);

    /// <summary>Hook at the top of ApplyOrder. Returns false when the legacy land path handles the order unchanged.</summary>
    private bool TryApplyDomainOrder(long t, int index)
    {
        var entry = _log[index];
        var o = entry.Request;
        int fi = FindForm(o.FormationId), di = FindDForm(o.FormationId);
        bool legacyKind = (byte)o.Kind <= (byte)OrderKind.Reengage;
        if (di < 0)
        {
            if (fi < 0) return false;                                            // unknown: legacy path reports it
            bool domainTarget = (o.Kind == OrderKind.Attack || o.Kind == OrderKind.Reengage) && FindDForm(o.TargetFormationId) >= 0;
            if (legacyKind && !domainTarget) return false;                      // ordinary land order
        }
        OrderInvalid inv;
        Action? apply;
        if (di >= 0) inv = ValidateDomainOrder(t, _df[di], entry.Side, o, out apply);
        else inv = ValidateLandDomainOrder(t, fi, entry.Side, o, out apply);
        BudgetRefusal br = BudgetRefusal.None;
        if (inv == OrderInvalid.None)
        {
            var budget = di >= 0 ? _df[di].Budget : _f[fi].Budget;
            br = budget.TryAccept(BudgetClass(o.Kind));
        }
        _log[index] = entry with { Invalid = inv, Budget = br };
        if (inv == OrderInvalid.None && br == BudgetRefusal.None) apply!();
        return true;
    }

    private OrderInvalid ValidateLandDomainOrder(long t, int fi, int side, OrderRequest o, out Action? apply)
    {
        apply = null;
        var f = _f[fi];
        if (f.Side != side) return OrderInvalid.NotOwnFormation;
        switch (o.Kind)
        {
            case OrderKind.Embark:
            {
                if (CountState(f, EntityState.Active) == 0) return OrderInvalid.NotOnField;
                int ti = FindDForm(o.TargetFormationId);
                if (ti < 0 || _df[ti].Side != f.Side || _df[ti].Dom != Domain.Naval) return OrderInvalid.BadTarget;
                if (_df[ti].V!.Capacity == 0) return OrderInvalid.NotCapable;
                long free = 0;
                foreach (int vi in _df[ti].Members) if (_d[vi].State == EntityState.Active) free += _d[vi].V!.Capacity - _d[vi].Embarked;
                if (free < CountState(f, EntityState.Active)) return OrderInvalid.NoCapacity;
                if (!EmbarkPoint(_df[ti], out Vec2L pt)) return OrderInvalid.NoPath;
                var pr = _pf.FindPath(_grid.CellOf(f.Anchor), _grid.CellOf(pt));
                if (!pr.Found) return OrderInvalid.NoPath;
                var path = PathMovement.Waypoints(_grid, pr.Cells);
                path[^1] = pt;
                apply = () =>
                {
                    f.Dir = OrderKind.MoveTo; f.DirPos = pt; f.Path = path; f.PathIdx = 0; f.PathTargetCell = -1;
                    _embarkOnto[fi] = ti; _landingSet[fi] = false;
                };
                return OrderInvalid.None;
            }
            case OrderKind.Disembark:
            {
                bool any = false;
                foreach (int i in f.Members) if (_e[i].State == EntityState.Embarked) any = true;
                if (!any) return OrderInvalid.NotEmbarked;
                if (!o.Pos.InDocumentedRange) return OrderInvalid.BadTarget;
                var c = _grid.CellOf(o.Pos);
                if (!_grid.IsPassable(_costs, c.X, c.Y)) return OrderInvalid.BadTarget;
                apply = () => { _landingSet[fi] = true; _landingTo[fi] = o.Pos; _embarkOnto[fi] = -1; };
                return OrderInvalid.None;
            }
            case OrderKind.Bombard:
            case OrderKind.Attack:
            case OrderKind.Reengage:
            {
                if (CountState(f, EntityState.Active) == 0) return OrderInvalid.NotOnField;
                var ws = f.Spec.Weapons;
                if (ws is null) return OrderInvalid.NotCapable;
                if (o.Kind == OrderKind.Reengage && f.Dir != OrderKind.Retreat) return OrderInvalid.NothingToReengage;
                var inv = ValidateFireTarget(f.Side, ws, o, out var dom);
                if (inv != OrderInvalid.None) return inv;
                var kind = o.Kind == OrderKind.Reengage ? OrderKind.Attack : o.Kind;
                apply = () => { f.Dir = kind; f.DirTarget = o.Kind == OrderKind.Bombard && o.TargetFormationId < 0 ? -1 : o.TargetFormationId; f.DirPos = o.Pos; ClearPath(f); _embarkOnto[fi] = -1; };
                return OrderInvalid.None;
            }
            default:
                return OrderInvalid.NotCapable;
        }
    }

    /// <summary>Common target validation for Attack / Bombard by a weapon set.</summary>
    private OrderInvalid ValidateFireTarget(int side, WeaponSpec[] ws, OrderRequest o, out Domain targetDomain)
    {
        targetDomain = Domain.Land;
        if (o.Kind == OrderKind.Bombard)
        {
            bool area = false;
            foreach (var w in ws) if (w.Indirect && (w.Targets & TargetDomains.Land) != 0) area = true;
            if (!area) return OrderInvalid.NotCapable;
            if (o.TargetFormationId < 0)
            {
                if (!o.Pos.InDocumentedRange) return OrderInvalid.BadTarget;
                var c = _grid.CellOf(o.Pos);
                return _grid.InBounds(c.X, c.Y) ? OrderInvalid.None : OrderInvalid.BadTarget;
            }
            int lf = FindForm(o.TargetFormationId);
            if (lf < 0 || _f[lf].Side == side) return OrderInvalid.BadTarget;
            return OrderInvalid.None;
        }
        int tl = FindForm(o.TargetFormationId), td = FindDForm(o.TargetFormationId);
        if (tl < 0 && td < 0) return OrderInvalid.BadTarget;
        int tside = tl >= 0 ? _f[tl].Side : _df[td].Side;
        if (tside == side) return OrderInvalid.BadTarget;
        targetDomain = tl >= 0 ? Domain.Land : _df[td].Dom;
        var need = targetDomain switch { Domain.Land => TargetDomains.Land, Domain.Naval => TargetDomains.Surface, _ => TargetDomains.Air };
        foreach (var w in ws) if ((w.Targets & need) != 0) return OrderInvalid.None;
        return OrderInvalid.NotCapable;
    }

    private OrderInvalid ValidateDomainOrder(long t, DForm f, int side, OrderRequest o, out Action? apply)
    {
        apply = null;
        if (f.Side != side) return OrderInvalid.NotOwnFormation;
        bool anyActive = false, anyAirborne = false;
        int ready = 0, withSorties = 0;
        foreach (int i in f.Members)
        {
            var d = _d[i];
            if (d.State != EntityState.Active) continue;
            anyActive = true;
            if (d.Dom == Domain.Air)
            {
                if (d.Phase != AirPhase.Based) anyAirborne = true;
                else if (d.SortiesLeft > 0) { withSorties++; if (t >= d.ReadyAtMs && AirFit(d)) ready++; }   // N-R1-06: fit to fly
            }
        }
        if (!anyActive) return OrderInvalid.NotOnField;
        var weapons = f.V?.Weapons ?? f.A!.Weapons;
        OrderInvalid inv;
        switch (o.Kind)
        {
            case OrderKind.Hold:
                break;
            case OrderKind.Rally:
                if (f.Dom != Domain.Naval) return OrderInvalid.NotCapable;
                break;
            case OrderKind.Retreat:
                break;
            case OrderKind.MoveTo:
            case OrderKind.Patrol:
                if (!o.Pos.InDocumentedRange) return OrderInvalid.BadTarget;
                {
                    var c = _grid.CellOf(o.Pos);
                    if (!_grid.InBounds(c.X, c.Y)) return OrderInvalid.BadTarget;
                    if (f.Dom == Domain.Naval)
                    {
                        if (!NavalView.IsWater(_grid, c.X, c.Y)) return OrderInvalid.BadTarget;
                        int lead = LeadVessel(f);
                        if (lead < 0 || !_navPf!.FindPath(_grid.CellOf(_d[lead].Pos), c).Found) return OrderInvalid.NoPath;
                    }
                }
                break;
            case OrderKind.Attack:
            case OrderKind.Reengage:
                if (o.Kind == OrderKind.Reengage && f.Dir != OrderKind.Retreat) return OrderInvalid.NothingToReengage;
                inv = ValidateFireTarget(f.Side, weapons, o with { Kind = OrderKind.Attack }, out var dom);
                if (inv == OrderInvalid.NotCapable && f.Dom == Domain.Naval && f.V!.RamDamage > 0 && dom == Domain.Naval) inv = OrderInvalid.None;
                if (inv != OrderInvalid.None) return inv;
                break;
            case OrderKind.Board:
            {
                if (f.Dom != Domain.Naval) return OrderInvalid.NotCapable;
                int ti = FindDForm(o.TargetFormationId);
                if (ti < 0 || _df[ti].Side == f.Side || _df[ti].Dom != Domain.Naval) return OrderInvalid.BadTarget;
                break;
            }
            case OrderKind.Bombard:
                inv = ValidateFireTarget(f.Side, weapons, o, out _);
                if (inv != OrderInvalid.None) return inv;
                break;
            case OrderKind.Escort:
            {
                if (o.TargetFormationId == f.Id) return OrderInvalid.BadTarget;
                int tl = FindForm(o.TargetFormationId), td = FindDForm(o.TargetFormationId);
                if (tl < 0 && td < 0) return OrderInvalid.BadTarget;
                int tside = tl >= 0 ? _f[tl].Side : _df[td].Side;
                if (tside != f.Side) return OrderInvalid.BadTarget;
                break;
            }
            default:
                return OrderInvalid.NotCapable;
        }
        bool launch = false;
        if (f.Dom == Domain.Air && o.Kind != OrderKind.Retreat && o.Kind != OrderKind.Hold)
        {
            if (!anyAirborne)
            {
                if (withSorties == 0) return OrderInvalid.NoSorties;
                if (ready == 0) return OrderInvalid.NotReady;
            }
            launch = ready > 0;
        }
        else if (f.Dom == Domain.Air && o.Kind == OrderKind.Hold && !anyAirborne) return OrderInvalid.NotReady;
        var kind = o.Kind == OrderKind.Reengage ? OrderKind.Attack : o.Kind;
        apply = () =>
        {
            f.Dir = kind; f.DirTarget = o.TargetFormationId; f.DirPos = o.Pos;
            f.Path = Array.Empty<Vec2L>(); f.PathIdx = 0; f.PathLeader = -1;
            if (f.Dom == Domain.Air && kind == OrderKind.Hold) f.DirPos = DCentroid(f, airborneOnly: true);
            if (f.Dom == Domain.Naval && (kind == OrderKind.MoveTo || kind == OrderKind.Patrol)) PlanVesselPath(f);
            if (f.Dom == Domain.Air)
            {
                foreach (int i in f.Members)
                {
                    var d = _d[i];
                    if (d.State != EntityState.Active) continue;
                    if (kind == OrderKind.Retreat && d.Phase == AirPhase.Airborne) d.Phase = AirPhase.Returning;
                    else if (kind != OrderKind.Retreat && d.Phase == AirPhase.Returning && AirCanContinue(d)) d.Phase = AirPhase.Airborne;
                    if (launch && d.Phase == AirPhase.Based && d.SortiesLeft > 0 && t >= d.ReadyAtMs && AirFit(d)) TakeOff(d, t);
                }
            }
        };
        return OrderInvalid.None;
    }

    private void TakeOff(DEnt d, long t)
    {
        var a = d.A!;
        d.Phase = AirPhase.Airborne;
        d.SortiesLeft--;
        d.Pos = _airBase[d.Side];
        d.Speed = a.CruiseMmPerS;
        d.Fuel = a.EnduranceMs;
        var fwd = FwdOf(d.Side);
        d.Heading = FixedTrig.Atan2(fwd.Y, fwd.X);
        for (int m = 0; m < d.Ammo.Length; m++) { d.Ammo[m] = a.Weapons[m].Ammo; d.NextFireMs[m] = long.MinValue; }
        d.Gen = checked(d.Gen + 1);
        d.Primary = true;                       // bounded: at most Sorties take-offs per airframe
        d.BestEnemy = d.BestObj = d.BestEdge = -1;
    }

    // ------------------------------------------------------------------ pulse start / view

    private void DomainPulseStart()
    {
        foreach (var f in _df) { f.Budget.Refresh(); f.StateChanged = false; f.CasualtiesPulse = 0; }
        foreach (var d in _d) d.Primary = false;
    }

    private void FillDomainView(SideView view, int side)
    {
        if (!_hasDomain) return;
        var vessels = new List<OwnVesselFormationView>();
        var air = new List<OwnAirFormationView>();
        foreach (var f in _df)
        {
            if (f.Side != side) continue;
            if (f.Dom == Domain.Naval)
            {
                int afloat = 0; long hull = 0, crew = 0, cap = 0, emb = 0;
                foreach (int i in f.Members)
                {
                    var d = _d[i];
                    if (!d.Present) continue;
                    afloat++; hull += d.Hp; crew += d.Crew; cap += d.V!.Capacity - d.Embarked; emb += d.Embarked;
                }
                if (afloat == 0) continue;
                int lead = LeadVessel(f);
                vessels.Add(new OwnVesselFormationView(f.Id, f.V!.Name, afloat, f.Start, hull, crew, f.Morale, DCentroid(f, false),
                    lead >= 0 ? _d[lead].Heading : 0, f.Dir, f.DirTarget, f.DirPos, cap, emb, f.Budget.Used, f.Budget.N));
            }
            else
            {
                int up = 0, based = 0, rdy = 0, lost = 0, sorties = 0; long minFuel = long.MaxValue;
                foreach (int i in f.Members)
                {
                    var d = _d[i];
                    if (!d.Present) { lost++; continue; }
                    sorties += d.SortiesLeft;
                    if (d.Phase == AirPhase.Based) { based++; if (d.SortiesLeft > 0 && view.NowMs >= d.ReadyAtMs && AirFit(d)) rdy++; }   // N-R2-03: the validator's launch condition
                    else { up++; minFuel = Math.Min(minFuel, d.Fuel); }
                }
                if (lost == f.Start) continue;
                air.Add(new OwnAirFormationView(f.Id, f.A!.Name, up, based, rdy, lost, sorties, minFuel == long.MaxValue ? 0 : minFuel,
                    DCentroid(f, airborneOnly: up > 0), f.Dir, f.DirTarget, f.DirPos, f.Budget.Used, f.Budget.N));
            }
        }
        var embarked = new List<EmbarkedFormationView>();
        for (int fi = 0; fi < _f.Length; fi++)
        {
            var f = _f[fi];
            if (f.Side != side) continue;
            int aboard = 0, onField = 0; long carrierForm = -1;
            foreach (int i in f.Members)
            {
                if (_e[i].State == EntityState.Embarked) { aboard++; if (carrierForm < 0) carrierForm = _df[_d[_carrier[i]].F].Id; }
                else if (_e[i].OnField) onField++;
            }
            if (aboard > 0) embarked.Add(new EmbarkedFormationView(f.Id, carrierForm, aboard, onField));
        }
        var k = _dk[side];
        var known = new List<KnownDomainEnemy>();
        for (int i = 0; i < _d.Length; i++)
            if (k.Ever[i] && !k.Gone[i]) known.Add(new KnownDomainEnemy(_d[i].Id, _df[_d[i].F].Id, _d[i].Dom, k.LastSeen[i], k.LastSeenMs[i], k.Tracked[i]));
        view.Domain = new DomainView(vessels.ToArray(), air.ToArray(), embarked.ToArray(), known.ToArray());
    }

    private Vec2L DCentroid(DForm f, bool airborneOnly)
    {
        long sx = 0, sy = 0, n = 0;
        foreach (int i in f.Members)
        {
            var d = _d[i];
            if (!(airborneOnly ? d.OnField : d.Present)) continue;
            sx += d.Pos.X; sy += d.Pos.Y; n++;
        }
        if (n == 0) return f.Dom == Domain.Air ? _airBase[f.Side] : f.Spec.Anchor;
        return new Vec2L(FixedMath.DivFloor(sx, n), FixedMath.DivFloor(sy, n));
    }

    /// <summary>Lowest-id Active vessel of the formation (the line leader), or -1.</summary>
    private int LeadVessel(DForm f)
    {
        foreach (int i in f.Members) if (_d[i].State == EntityState.Active) return i;
        return -1;
    }

    // ------------------------------------------------------------------ elimination / inactivity / end

    /// <summary>
    /// Adds the domain force to the per-side counts. Elimination (onFieldOnly = false): Active vessels and every aircraft that
    /// can still fly (airborne, or at base with a sortie left and an airframe fit to fly — N-R2-01: one grounded by damage is
    /// never repaired in battle, so it cannot) keep a side in the battle. Holding the field (onFieldOnly = true): only what is
    /// physically there — Active vessels and airborne aircraft.
    /// </summary>
    private void DomainActiveCounts(ref int a0, ref int a1, bool onFieldOnly = false)
    {
        foreach (var d in _d)
        {
            if (d.State != EntityState.Active) continue;
            if (d.Dom == Domain.Air && d.Phase == AirPhase.Based && (onFieldOnly || d.SortiesLeft == 0 || !AirFit(d))) continue;
            if (d.Side == 0) a0++; else a1++;
        }
    }

    /// <summary>PulseEnd hook: morale recovery and the five-pulse inactivity rule for vessel / air formations.</summary>
    private void DomainPulseEnd(bool[] hasField, bool[] allInactive)
    {
        foreach (var f in _df)
        {
            if (f.Dom != Domain.Naval || f.Routed || f.CasualtiesPulse != 0) continue;
            bool any = false;
            foreach (int i in f.Members) if (_d[i].State == EntityState.Active) any = true;
            if (!any) continue;
            long rec = f.Dir == OrderKind.Hold ? _r.HoldRecovery : f.Dir == OrderKind.Rally ? _r.RallyRecovery : 0;
            f.Morale = Math.Min(_r.MoraleMax, f.Morale + rec);
        }
        foreach (var f in _df)
        {
            bool present = false, act = f.StateChanged;
            foreach (int i in f.Members) if (_d[i].Present) { present = true; if (_d[i].Primary) act = true; }
            if (!present) continue;
            f.Inactive = act ? 0 : checked(f.Inactive + 1);
            hasField[f.Side] = true;
            if (f.Inactive < _r.InactivityPulses) allInactive[f.Side] = false;
        }
    }

    /// <summary>A side disengages: its vessels and aircraft leave (Escaped, preserved), with everyone aboard.</summary>
    private void DomainDisengage(bool d0, bool d1)
    {
        foreach (var d in _d)
        {
            if (!d.Present || !((d.Side == 0 && d0) || (d.Side == 1 && d1))) continue;
            d.Disengaged = true;
            LeaveBattleAlive(d);
        }
    }

    private void LeaveBattleAlive(DEnt d)
    {
        SetDState(d, EntityState.Escaped);
        for (int i = 0; i < _e.Length; i++)
            if (_carrier[i] >= 0 && _d[_carrier[i]] == d) LeaveWithCarrier(i, EntityState.Escaped, null);
    }

    /// <summary>End hook (start of End): routing vessels / aircraft count as having fled (Escaped). Aircraft still airborne or
    /// returning stay Active (no return flight is simulated after End) and count among the survivors.</summary>
    private void DomainEndStart()
    {
        foreach (var d in _d)
            if (d.State == EntityState.Routing) { d.RoutedEscape = true; LeaveBattleAlive(d); }
    }

    private FormationResult[] DomainResults(FormationResult[] land)
    {
        if (_df.Length == 0) return land;
        var all = new List<FormationResult>(land);
        foreach (var f in _df)
        {
            long act = 0, esc = 0, resc = 0, desc = 0, dead = 0, sur = 0;
            foreach (int i in f.Members)
            {
                var d = _d[i];
                switch (d.State)
                {
                    case EntityState.Active: act++; break;
                    case EntityState.Escaped: esc++; if (d.RoutedEscape) resc++; if (d.Disengaged) desc++; break;
                    case EntityState.Dead: dead++; break;
                    case EntityState.Surrendered: sur++; break;
                    default: throw new InvalidOperationException("routing vessel/aircraft left after End");
                }
            }
            var r = new FormationResult(f.Id, f.Side, f.Dom, f.Spec.Profile.FamilyKey, f.Start, act + esc, dead, sur, act, esc, resc, desc,
                                        f.Morale, f.DamageDealt, f.DamageTaken, f.Attacks);
            if (!r.Conserved) throw new InvalidOperationException($"conservation violated for formation {f.Id}");
            all.Add(r);
        }
        all.Sort(static (a, b) => a.FormationId.CompareTo(b.FormationId));
        return all.ToArray();
    }

    private void AppendDomainState(IncrementalHash h)
    {
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_d.Length);
        foreach (var d in _d)
        {
            L(d.Id); L((long)d.State); L((long)d.Phase); L(d.Pos.X); L(d.Pos.Y); L(d.Heading); L(d.Speed); L(d.Hp); L(d.Crew);
            L(d.Embarked); L(d.Fuel); L(d.SortiesLeft); L(d.ReadyAtMs); L(d.Gen); L(d.OpCarryMs); L(RamHashSlot(DIndex(d.Id))); L(d.RepairPool);   // [RAM] slot was RamReadyMs
            L(d.BestEnemy); L(d.BestObj); L(d.BestEdge); L(d.RoutedEscape ? 1 : 0); L(d.Disengaged ? 1 : 0);
            L(d.FightStartAboard); L(d.BoardedBy);
            foreach (var x in d.NextFireMs) L(x);
            foreach (var x in d.Ammo) L(x);
        }
        L(_df.Length);
        foreach (var f in _df) { L(f.Id); L(f.Morale); L(f.Inactive); L((long)f.Dir); L(f.DirTarget); L(f.DirPos.X); L(f.DirPos.Y); L(f.PathIdx); L(f.Path.Length); }
        for (int s = 0; s < 2; s++)
            for (int i = 0; i < _d.Length; i++) L((_dk[s].Tracked[i] ? 1 : 0) | (_dk[s].Ever[i] ? 2 : 0) | (_dk[s].Gone[i] ? 4 : 0));
        for (int i = 0; i < _e.Length; i++) L(_carrier[i]);
        for (int i = 0; i < _f.Length; i++) { L(_embarkOnto[i]); L(_landingSet[i] ? 1 : 0); L(_landingTo[i].X); L(_landingTo[i].Y); }
        if (_lwNext is not null)
            for (int i = 0; i < _e.Length; i++)
                if (_lwNext[i] is { } nx) { foreach (var x in nx) L(x); foreach (var x in _lwAmmo![i]) L(x); }
        AppendRamState(h);                                                           // [RAM] empty without ram ships
        _ledger.AppendTo(h);
    }

    /// <summary>Active-concealment clause: an enemy vessel or aircraft with primary activity this pulse, within its sight of
    /// the hidden entity, is "searching" too.</summary>
    private bool DomainSearcherNear(Ent e)
    {
        foreach (var x in _d)
        {
            if (x.Side == e.Side || !x.OnField || !x.Primary) continue;
            long env = x.V?.SightMm ?? x.A!.SightMm;
            if (Vec2L.DistanceSq(x.Pos, e.Pos) <= (Int128)env * env) return true;
        }
        return false;
    }
}
