using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>[TR] Which deployment rule a battle's start positions must satisfy.</summary>
public enum DeploymentRule : byte
{
    /// <summary>No zone check (unit-level mechanic probes and Sprint 1 scenarios that place forces in contact on purpose).</summary>
    Unrestricted = 0,
    /// <summary>DIR5 §2.14: each army inside its own third along the deployment axis; the middle third undeployable.</summary>
    Thirds = 1,
}

/// <summary>[TR] Why a deployment (or one entity of it) is refused. Stable codes. None = accepted.</summary>
public enum DeploymentRefusal : byte
{
    None = 0,
    /// <summary>The entity disc reaches into the middle third (undeployable before battle, DIR5 §2.14).</summary>
    MiddleThird = 1,
    /// <summary>The entity disc reaches into the ENEMY's third.</summary>
    EnemyThird = 2,
    /// <summary>The entity's cell is off the grid or impassable.</summary>
    NotPassable = 3,
    /// <summary>Two entity discs overlap.</summary>
    Overlap = 4,
    /// <summary>The deployment phase has ended (battle started): no further edits.</summary>
    PhaseClosed = 5,
    UnknownFormation = 6,
    NotOwnFormation = 7,
    /// <summary>The two sides' exit edges are not opposite (no deployment axis; thirds undefined).</summary>
    SidesNotOpposed = 8,
    /// <summary>Explicit placement count differs from the formation's Count, or a position is outside the coordinate range.</summary>
    BadPlacement = 9,
    /// <summary>[TR x lane N] The formation is not a ground deployment (a vessel or aircraft formation, or a land formation that
    /// starts aboard a transport): the thirds rule and the deployment phase place land formations on the ground only.</summary>
    NotGroundFormation = 10,
    /// <summary>[TR, verify-TR-r2 V6] Two entities of the battle share an id. Entity ids must be unique across every formation
    /// of every domain (the battle refuses duplicates); reported by the duplicated id (the smallest one).</summary>
    DuplicateId = 11,
}

/// <summary>[TR] Outcome of one deployment check or edit: the refusal and the FIRST offending entity id (ascending id), or -1.</summary>
public readonly record struct DeploymentVerdict(DeploymentRefusal Refusal, long EntityId)
{
    public bool Accepted => Refusal == DeploymentRefusal.None;
    public static readonly DeploymentVerdict Ok = new(DeploymentRefusal.None, -1);
}

/// <summary>[TR] One recorded deployment edit (accepted or refused) — the deployment analogue of the order log.</summary>
public readonly record struct DeploymentLogEntry(long Index, int Side, long FormationId, Vec2L Anchor, EntitySpec[]? Entities, DeploymentVerdict Verdict);

/// <summary>
/// [TR] DIR5 §2.14 deployment thirds, as an exact integer predicate.
///
/// AXIS: the two sides must exit through OPPOSITE edges (West/East: axis x, extent L = Width*S; North/South: axis y, extent
/// L = Height*S). Each army deploys in the third at its OWN exit edge (its rear); the middle third is undeployable.
/// THIRDS (half-open like the cells, no division, exact for every L): a coordinate u along the axis is in
///   the West/North third iff 3u &lt; L;   the middle third iff L &lt;= 3u &lt; 2L;   the East/South third iff 3u &gt;= 2L.
/// ENTITY: an entity disc (centre u, radius r) is deployable for its side iff the WHOLE disc is inside the own third:
///   West/North side: 3(u + r) &lt; L;   East/South side: 3(u - r) &gt;= 2L;
///   otherwise the refusal is MiddleThird if the disc reaches the middle third and not the enemy third, else EnemyThird.
///   Plus: its cell passable and in bounds, no disc overlap (Slice C closed discs), ids unique. Passes in a fixed order, so a
///   refusal is a pure function of the deployment (input order irrelevant): PASS 0 [verify-TR-r2 V6]: entity ids unique —
///   the smallest duplicated id is reported (DuplicateId); only unique ids make the id-sorted order below total, so the
///   later passes do not depend on the input order; PASS 1, entities in ascending id: coordinate range,
///   passability, zone — the first failing entity is reported; PASS 2 (only when pass 1 found nothing): overlap pairs (i, j),
///   i &lt; j, in ascending (i, j) — the first overlapping pair is reported by its lower id i. So a zone failure of ANY entity
///   is reported before an overlap of lower-id entities (verify-TR-r1 V12: the order is per pass, not one ascending sweep).
/// ASYMMETRY: nothing is normalized. Positions are accepted or refused exactly as given; an accepted deployment is passed to
///   the battle unchanged (DIR5 §2.14: realistic geographical and tactical asymmetries are preserved; the 20 m exit-distance
///   difference of the pre-F1 Meeting geometry is kept — tested).
/// AFTER START: the rule constrains START positions only; once the battle starts every passable cell is reachable (tested).
/// </summary>
public static class DeploymentZones
{
    public static bool Opposed(ExitEdge a, ExitEdge b) => (a, b) switch
    {
        (ExitEdge.West, ExitEdge.East) or (ExitEdge.East, ExitEdge.West) or (ExitEdge.North, ExitEdge.South) or (ExitEdge.South, ExitEdge.North) => true,
        _ => false,
    };

    /// <summary>Extent L (mm) along the deployment axis of a side exiting through <paramref name="exit"/>.</summary>
    public static long AxisExtentMm(TerrainGrid grid, ExitEdge exit)
        => exit is ExitEdge.West or ExitEdge.East ? grid.Width * grid.CellSizeMm : grid.Height * grid.CellSizeMm;

    /// <summary>Coordinate of p along the deployment axis of <paramref name="exit"/>.</summary>
    public static long AxisCoord(Vec2L p, ExitEdge exit) => exit is ExitEdge.West or ExitEdge.East ? p.X : p.Y;

    /// <summary>Which third (0 = West/North, 1 = middle, 2 = East/South) contains axis coordinate u (half-open, exact).</summary>
    public static int ThirdOf(long u, long extent)
    {
        Int128 u3 = (Int128)u * 3;
        if (u3 < extent) return 0;
        if (u3 < (Int128)extent * 2) return 1;
        return 2;
    }

    /// <summary>The refusal for one entity disc of a side exiting through <paramref name="exit"/> (None when its whole disc is
    /// inside the own third). Zone check only (passability and overlap are separate).</summary>
    public static DeploymentRefusal ZoneOf(TerrainGrid grid, ExitEdge exit, Vec2L pos, long radiusMm)
    {
        long L = AxisExtentMm(grid, exit);
        long u = AxisCoord(pos, exit);
        bool lowSide = exit is ExitEdge.West or ExitEdge.North;
        Int128 lo3 = ((Int128)u - radiusMm) * 3, hi3 = ((Int128)u + radiusMm) * 3;
        if (lowSide)
        {
            if (hi3 < L) return DeploymentRefusal.None;
            return hi3 >= (Int128)L * 2 ? DeploymentRefusal.EnemyThird : DeploymentRefusal.MiddleThird;
        }
        if (lo3 >= (Int128)L * 2) return DeploymentRefusal.None;
        return lo3 < L ? DeploymentRefusal.EnemyThird : DeploymentRefusal.MiddleThird;
    }

    /// <summary>[TR, verify-TR-r2 V6] Every entity id a setup implies, in formation input order: explicit Entities, or the
    /// generated ids Id*1000+slot — for EVERY formation (land on the ground, land aboard a transport, vessels, aircraft), the
    /// id space the battle requires to be duplicate-free.</summary>
    public static long[] EntityIds(BattleSetup setup)
    {
        var list = new List<long>();
        foreach (var s in setup.Formations)
        {
            if (s.Entities is not null) { foreach (var e in s.Entities) list.Add(e.Id); continue; }
            for (int k = 0; k < s.Count; k++) list.Add(checked(s.Id * 1000 + k));
        }
        return list.ToArray();
    }

    /// <summary>[TR, verify-TR-r2 V6] The smallest id that occurs more than once in <paramref name="ids"/> (any order).</summary>
    public static bool SmallestDuplicate(ReadOnlySpan<long> ids, out long id)
    {
        var a = ids.ToArray();
        Array.Sort(a);
        for (int i = 1; i < a.Length; i++) if (a[i] == a[i - 1]) { id = a[i]; return true; }
        id = -1;
        return false;
    }

    /// <summary>
    /// Full check of a set of entity placements (any order) of ONE battle: ids, zones (per entity's side), passability, overlap.
    /// PASS 0 (verify-TR-r2 V6): the smallest duplicated id (DuplicateId). Then two passes (verify-TR-r1 V12): PASS 1 over the
    /// entities in ascending id (range, passability, zone) returns the first
    /// failing entity; only if every entity passes, PASS 2 returns the first overlapping pair (i, j), i &lt; j, in ascending
    /// (i, j) order, reported by its lower id i. <paramref name="sides"/> must have two opposed exits.
    /// </summary>
    public static DeploymentVerdict Check(TerrainGrid grid, TerrainCostTable costs, SideSpec[] sides, long radiusMm,
                                          ReadOnlySpan<(long Id, int Side, Vec2L Pos)> placements)
    {
        if (sides.Length != 2 || !Opposed(sides[0].Exit, sides[1].Exit)) return new DeploymentVerdict(DeploymentRefusal.SidesNotOpposed, -1);
        var p = placements.ToArray();
        Array.Sort(p, static (a, b) => a.Id.CompareTo(b.Id));
        for (int i = 1; i < p.Length; i++)                                                        // [TR, r2 V6] PASS 0
            if (p[i].Id == p[i - 1].Id) return new DeploymentVerdict(DeploymentRefusal.DuplicateId, p[i].Id);
        foreach (var (id, side, pos) in p)
        {
            if (!pos.InDocumentedRange) return new DeploymentVerdict(DeploymentRefusal.BadPlacement, id);
            var c = grid.CellOf(pos);
            if (!grid.IsPassable(costs, c.X, c.Y)) return new DeploymentVerdict(DeploymentRefusal.NotPassable, id);
            var z = ZoneOf(grid, sides[side].Exit, pos, radiusMm);
            if (z != DeploymentRefusal.None) return new DeploymentVerdict(z, id);
        }
        for (int i = 0; i < p.Length; i++)
            for (int j = i + 1; j < p.Length; j++)
                if (Separation.Overlaps(p[i].Pos, radiusMm, p[j].Pos, radiusMm)) return new DeploymentVerdict(DeploymentRefusal.Overlap, p[i].Id);
        return DeploymentVerdict.Ok;
    }

    /// <summary>[TR x lane N] A GROUND deployment: a land formation that does not start aboard a transport. Vessel and aircraft
    /// formations and embarked troops are not placed on the ground, so the thirds rule does not apply to them (their
    /// deployment rule is not defined by DIR5 §2.14 — reported as an open point, not decided here).</summary>
    public static bool IsGroundFormation(FormationSpec s) => s.Domain == Domain.Land && s.EmbarkedOn is null;

    /// <summary>The GROUND entity placements a setup implies (explicit Entities, or the generated slot positions of
    /// TacticalBattle); non-ground formations (<see cref="IsGroundFormation"/>) contribute none.</summary>
    public static (long Id, int Side, Vec2L Pos)[] Placements(BattleSetup setup)
    {
        var list = new List<(long, int, Vec2L)>();
        var r = setup.Rules;
        foreach (var s in setup.Formations)
        {
            if (!IsGroundFormation(s)) continue;
            if (s.Entities is not null) { foreach (var e in s.Entities) list.Add((e.Id, s.Side, e.Pos)); continue; }
            for (int k = 0; k < s.Count; k++)
                list.Add((checked(s.Id * 1000 + k), s.Side, s.Anchor + TacticalBattle.SlotOffsetFor(r, setup.Sides[s.Side].Exit, s.Count, k)));
        }
        return list.ToArray();
    }

    /// <summary>Check of a whole setup: the sides, then (verify-TR-r2 V6) id uniqueness over EVERY entity of the setup
    /// (<see cref="EntityIds"/> — a ground entity may not reuse a vessel's, an aircraft's or an embarked soldier's id either),
    /// then the placement check of the ground entities.</summary>
    public static DeploymentVerdict Check(BattleSetup setup)
    {
        if (setup.Sides.Length != 2 || !Opposed(setup.Sides[0].Exit, setup.Sides[1].Exit)) return new DeploymentVerdict(DeploymentRefusal.SidesNotOpposed, -1);
        if (SmallestDuplicate(EntityIds(setup), out long dup)) return new DeploymentVerdict(DeploymentRefusal.DuplicateId, dup);
        return Check(setup.Grid, setup.Costs, setup.Sides, setup.Rules.EntityRadiusMm, Placements(setup));
    }

    /// <summary>
    /// [TR, verify-TR-r1 V2] Check of ONE EDIT: the <paramref name="edited"/> placements (one formation's entities at their
    /// proposed positions) against the <paramref name="others"/> (every other ground entity at its CURRENT position, valid or
    /// not). PASS 0 [verify-TR-r2 V6], edited entities in ascending id: an id used twice in the edit, or used by any other
    /// entity (<paramref name="others"/>, plus <paramref name="otherIds"/> — the ids of entities that are not ground placements:
    /// vessels, aircraft, embarked troops) is refused as DuplicateId (before, an edit that reused another formation's entity id
    /// was "the same entity" to the overlap pass and could be placed on top of it, and Close accepted a duplicate the battle
    /// then refused by an exception); PASS 1, edited entities in ascending id: range, passability, zone (exactly as the full
    /// check); PASS 2: overlap pairs (a, b) with a an edited entity and
    /// b any other placement (edited or not, b != a), in ascending (a, b) — reported by the edited id a. Another formation's
    /// invalid placement is NOT this edit's failure (it is that formation's, and Close refuses it), so one invalid formation
    /// can never block the edits that would repair the others (DIR5 §2.14: "Players can edit deployments before battle begins").
    /// </summary>
    public static DeploymentVerdict CheckEdit(TerrainGrid grid, TerrainCostTable costs, SideSpec[] sides, long radiusMm,
                                              ReadOnlySpan<(long Id, int Side, Vec2L Pos)> edited,
                                              ReadOnlySpan<(long Id, int Side, Vec2L Pos)> others,
                                              ReadOnlySpan<long> otherIds = default)
    {
        if (sides.Length != 2 || !Opposed(sides[0].Exit, sides[1].Exit)) return new DeploymentVerdict(DeploymentRefusal.SidesNotOpposed, -1);
        var a = edited.ToArray();
        Array.Sort(a, static (x, y) => x.Id.CompareTo(y.Id));
        var taken = new long[others.Length + otherIds.Length];                                   // [TR, r2 V6] PASS 0
        for (int k = 0; k < others.Length; k++) taken[k] = others[k].Id;
        otherIds.CopyTo(taken.AsSpan(others.Length));
        Array.Sort(taken);
        for (int k = 0; k < a.Length; k++)
            if ((k > 0 && a[k].Id == a[k - 1].Id) || Array.BinarySearch(taken, a[k].Id) >= 0)
                return new DeploymentVerdict(DeploymentRefusal.DuplicateId, a[k].Id);
        foreach (var (id, side, pos) in a)
        {
            if (!pos.InDocumentedRange) return new DeploymentVerdict(DeploymentRefusal.BadPlacement, id);
            var c = grid.CellOf(pos);
            if (!grid.IsPassable(costs, c.X, c.Y)) return new DeploymentVerdict(DeploymentRefusal.NotPassable, id);
            var z = ZoneOf(grid, sides[side].Exit, pos, radiusMm);
            if (z != DeploymentRefusal.None) return new DeploymentVerdict(z, id);
        }
        var all = new (long Id, int Side, Vec2L Pos)[a.Length + others.Length];
        a.CopyTo(all, 0);
        others.CopyTo(all.AsSpan(a.Length));
        Array.Sort(all, static (x, y) => x.Id.CompareTo(y.Id));
        foreach (var e in a)
            foreach (var o in all)
            {
                if (o.Id == e.Id) continue;                     // e itself (ids are unique between the edit and the rest: PASS 0)
                if (Separation.Overlaps(e.Pos, radiusMm, o.Pos, radiusMm)) return new DeploymentVerdict(DeploymentRefusal.Overlap, e.Id);
            }
        return DeploymentVerdict.Ok;
    }
}

/// <summary>
/// [TR] The pre-battle DEPLOYMENT PHASE (DIR5 §2.14: "Players can edit deployments before battle begins"; "Once deployment
/// ends and battle starts, the entire battlefield becomes accessible"). Holds the proposed placement of every formation,
/// accepts or refuses edits deterministically (the EDITED formation is checked against the others' current positions —
/// <see cref="DeploymentZones.CheckEdit"/>; a refused edit changes nothing), logs every edit, and on <see cref="Close"/> —
/// which requires the WHOLE deployment to be valid — yields the battle setup with EXPLICIT entity positions exactly as
/// deployed and <see cref="BattleSetup.Deployment"/> = Thirds (the battle re-checks them: defence in depth).
/// </summary>
public sealed class DeploymentPhase
{
    private readonly BattleSetup _base;
    private readonly FormationSpec[] _forms;          // current proposal, ascending formation id
    private readonly List<DeploymentLogEntry> _log = new();

    public DeploymentPhase(BattleSetup setup)
    {
        _base = setup;
        var f = (FormationSpec[])setup.Formations.Clone();
        Array.Sort(f, static (a, b) => a.Id.CompareTo(b.Id));
        _forms = f;
    }

    public bool IsClosed { get; private set; }
    public IReadOnlyList<DeploymentLogEntry> Log => _log;
    public IReadOnlyList<FormationSpec> Formations => _forms;

    /// <summary>The verdict of the CURRENT proposal (all formations).</summary>
    public DeploymentVerdict Current() => DeploymentZones.Check(_base with { Formations = _forms });

    /// <summary>
    /// Proposes a new placement for one formation of <paramref name="side"/>: a new anchor (entities on generated slots) or,
    /// when <paramref name="entities"/> is given, explicit entity positions (Count of them; ids free but unique across the
    /// battle — verify-TR-r2 V6: an id of another entity of ANY formation or domain is refused as DuplicateId). Accepted iff the
    /// edited formation passes <see cref="DeploymentZones.CheckEdit"/> against every other ground entity at its current position;
    /// refused edits change nothing. Other formations' validity does not enter (verify-TR-r1 V2: judging the whole deployment
    /// deadlocked a proposal with two invalid formations and let one player's invalid placement block the other's edits).
    /// </summary>
    public DeploymentVerdict Propose(int side, long formationId, Vec2L anchor, EntitySpec[]? entities = null)
    {
        DeploymentVerdict v;
        int i = Array.FindIndex(_forms, f => f.Id == formationId);
        if (IsClosed) v = new DeploymentVerdict(DeploymentRefusal.PhaseClosed, -1);
        else if (i < 0) v = new DeploymentVerdict(DeploymentRefusal.UnknownFormation, -1);
        else if (_forms[i].Side != side) v = new DeploymentVerdict(DeploymentRefusal.NotOwnFormation, -1);
        else if (!DeploymentZones.IsGroundFormation(_forms[i])) v = new DeploymentVerdict(DeploymentRefusal.NotGroundFormation, -1);
        else if (!anchor.InDocumentedRange || (entities is not null && entities.Length != _forms[i].Count))
            v = new DeploymentVerdict(DeploymentRefusal.BadPlacement, -1);
        else
        {
            var edited = _forms[i] with { Anchor = anchor, Entities = entities is null ? null : (EntitySpec[])entities.Clone() };
            var others = new FormationSpec[_forms.Length - 1];
            for (int k = 0, m = 0; k < _forms.Length; k++) if (k != i) others[m++] = _forms[k];
            var r = _base.Rules;
            v = DeploymentZones.CheckEdit(_base.Grid, _base.Costs, _base.Sides, r.EntityRadiusMm,
                                          DeploymentZones.Placements(_base with { Formations = new[] { edited } }),
                                          DeploymentZones.Placements(_base with { Formations = others }),
                                          DeploymentZones.EntityIds(_base with { Formations = others }));   // [TR, r2 V6]
            if (v.Accepted) _forms[i] = edited;
        }
        _log.Add(new DeploymentLogEntry(_log.Count, side, formationId, anchor, entities, v));
        return v;
    }

    /// <summary>
    /// Ends the deployment phase. Refused (returns the verdict, phase stays open) unless the current proposal is valid. On
    /// success returns the battle setup: every formation with explicit entity positions exactly as deployed (generated slots
    /// materialised), Deployment = Thirds.
    /// </summary>
    public DeploymentVerdict Close(out BattleSetup? battle)
    {
        battle = null;
        if (IsClosed) return new DeploymentVerdict(DeploymentRefusal.PhaseClosed, -1);
        var v = Current();
        if (!v.Accepted) return v;
        var r = _base.Rules;
        var outForms = new FormationSpec[_forms.Length];
        for (int i = 0; i < _forms.Length; i++)
        {
            var s = _forms[i];
            if (s.Entities is not null || !DeploymentZones.IsGroundFormation(s)) { outForms[i] = s; continue; }   // [TR x lane N]
            var es = new EntitySpec[s.Count];
            for (int k = 0; k < s.Count; k++)
                es[k] = new EntitySpec(checked(s.Id * 1000 + k), s.Anchor + TacticalBattle.SlotOffsetFor(r, _base.Sides[s.Side].Exit, s.Count, k));
            outForms[i] = s with { Entities = es };
        }
        IsClosed = true;
        battle = _base with { Formations = outForms, Deployment = DeploymentRule.Thirds };
        return v;
    }
}
