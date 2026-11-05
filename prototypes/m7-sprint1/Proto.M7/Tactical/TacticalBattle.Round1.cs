using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// [TR, verify-TR-r1; potential changed in verify-TR-r2 V1] Running minima of a formation's REMAINING PATH LENGTH to the GOAL
/// CELLS of the movement orders it executed (one entry per distinct goal cell, never reset). Sorted by cell index (binary
/// search; no hashed containers). A new measurement initialises a goal's entry; afterwards a measurement is PROGRESS iff it is
/// at least one cell below the recorded minimum (then it becomes the minimum) — the same rule as the entity minima
/// (D-tactical.md §4).
/// POTENTIAL (r2): Rem(f) = |Anchor − P[i]| + Σ_{k=i}^{n−2} |P[k+1] − P[k]|, P the planned waypoint path, i = PathIdx (the next
/// waypoint); Rem = 0 once the path is exhausted. It falls by exactly the distance the anchor advances ALONG ITS PATH, so a
/// march that first leads away from the goal (around a wall, along a river bank to a ford) is progress; the r1 potential
/// |Anchor − centre(goal)| grew along such detours (verify-TR-r2 V1).
/// TERMINATION: every entry is a monotone non-increasing integer bounded below by 0 that falls by at least one cell per
/// progress event and starts at most at the longest planned path (an A* path visits each of the Width*Height cells at most
/// once: &lt;= Width*Height*S*√2, plus the anchor's offset from the first waypoint), and there are at most Width*Height goal
/// cells, so ANY controller — even one that keeps issuing new MoveTo orders — gets finitely many march-progress events per
/// formation.
/// </summary>
internal sealed class GoalMinima
{
    private readonly List<int> _cell = new();
    private readonly List<long> _best = new();
    // [TR, r2 V1] Suffix lengths of the path last measured (derived data, a pure function of that path array: not hashed).
    private Vec2L[]? _suffixOf;
    private long[] _suffix = Array.Empty<long>();

    /// <summary>[TR, r2 V1] Remaining length (mm) of <paramref name="path"/> from <paramref name="anchor"/>, whose next waypoint
    /// is <paramref name="idx"/>: |anchor − path[idx]| + the polyline length from path[idx] to the end; 0 when idx is past the
    /// end. Suffix sums are recomputed only when the formation's path array changes (reference identity).</summary>
    public long Remaining(Vec2L[] path, int idx, Vec2L anchor)
    {
        if (idx >= path.Length) return 0;
        if (!ReferenceEquals(path, _suffixOf))
        {
            var s = new long[path.Length];
            for (int k = path.Length - 2; k >= 0; k--) s[k] = checked(s[k + 1] + Vec2L.Distance(path[k], path[k + 1]));
            _suffix = s;
            _suffixOf = path;
        }
        return checked(Vec2L.Distance(anchor, path[idx]) + _suffix[idx]);
    }

    public int Count => _cell.Count;
    public int CellAt(int i) => _cell[i];
    public long BestAt(int i) => _best[i];

    /// <summary>Records distance <paramref name="d"/> to goal cell <paramref name="cell"/>; true iff it is a new minimum by at
    /// least <paramref name="cellMm"/>.</summary>
    public bool Track(int cell, long d, long cellMm)
    {
        int i = _cell.BinarySearch(cell);
        if (i < 0) { i = ~i; _cell.Insert(i, cell); _best.Insert(i, d); return false; }
        if (d <= _best[i] - cellMm) { _best[i] = d; return true; }
        return false;
    }
}

/// <summary>
/// [TR] Verification round 1 (verify-TR-r1) additions to the DIR5 §2.6 rule and its neighbours. Kept in this file so the
/// shared TacticalBattle.cs carries only one-line, [TR]-marked call sites.
/// <list type="bullet">
/// <item>V1 — MARCH PROGRESS: under the DIR5 model a formation executing a movement order (MoveTo, or an Attack marching on its
///   target's known position) whose anchor sets a new running minimum (by &gt;= one cell) of its REMAINING PATH LENGTH to the
///   order's goal cell (verify-TR-r2 V1: along the planned path, so detours around walls and marches to a ford count; r1
///   used the straight-line distance, which grows on such detours) makes PROGRESS in that pulse (<see cref="GoalMinima"/>).
///   A hidden formation that marches with a purpose is therefore not "stationary" (DIR5 §2.6); one that oscillates between
///   goals it has already reached sets no new minimum and is still stationary in effect (P-TR3 kept). The legacy model is
///   unchanged (no tracking).</item>
/// <item>V4 — CONCEALED BY TERRAIN for exemption (a): a member e is concealed from an Active enemy x within its engagement
///   reach iff its own cell conceals (multiplier &lt; 1), or it carries personal concealment &lt; 1 (lane N), or the TERRAIN on
///   the sight line from x to e hides it: <see cref="TerrainSight.Query"/> from x (eye height) to e (eye height) is not
///   visible (a building, a crest, a wall) or has clarity &lt; 1 (vegetation in between). A member that stands in the open
///   with a clear line to the enemy is NOT concealed, even when that enemy does not see it (e.g. beyond its sight range).</item>
/// <item>V11 — an ACCEPTED order on lane N's domain path (Embark, Disembark, Bombard, Attack on a domain target) clears the
///   explicit-Hold flag, exactly as every accepted land order does, so a later implicit Hold set by lane N (embark complete,
///   landing without a path) is never mistaken for an explicit one.</item>
/// <item>V13 — a landed entity's charge run-up (LastStepMm) is zero at its landing spot (a landing is not a run-up), and the
///   run-up of every entity whose profile carries an impetus is part of the state hash.</item>
/// </list>
/// </summary>
public sealed partial class TacticalBattle
{
    /// <summary>[TR, V1] Per step: march-progress flags of the formations executing a movement order (DIR5 model only).</summary>
    private void UpdateMarchProgress()
    {
        if (_r.Inactivity != InactivityModel.HiddenStationary) return;
        long S = _grid.CellSizeMm;
        foreach (var f in _f)
        {
            if (f.Routed || (f.Dir != OrderKind.MoveTo && f.Dir != OrderKind.Attack) || f.Path.Length == 0) continue;
            var gc = _grid.CellOf(f.Path[^1]);
            if (!_grid.InBounds(gc.X, gc.Y)) continue;
            f.Goals ??= new GoalMinima();
            long d = f.Goals.Remaining(f.Path, f.PathIdx, f.Anchor);                 // [TR, r2 V1] along the path, not straight
            if (f.Goals.Track(_grid.Index(gc.X, gc.Y), d, S)) f.MarchProgress = true;
        }
    }

    /// <summary>[TR, V4] Is member <paramref name="e"/> concealed by terrain (or personal concealment) from enemy entity
    /// <paramref name="x"/>? See the class summary.</summary>
    private bool ConcealedFrom(Ent e, Ent x)
    {
        if (ConcealedForExemption(e)) return true;
        var los = TerrainSight.Query(_grid, _conceal, _fx, x.Pos, _r.EyeHeightMm, e.Pos, _r.EyeHeightMm);
        return !los.Visible || los.Clarity < Q32.One;
    }

    /// <summary>[TR, V11] After the domain order path handled order <paramref name="index"/>: an accepted order of a LAND
    /// formation clears its explicit-Hold flag (no domain order is a Hold).</summary>
    private void ClearHoldExplicitIfAccepted(int index)
    {
        var entry = _log[index];
        if (entry.Invalid != OrderInvalid.None || entry.Budget != BudgetRefusal.None) return;
        int fi = FindForm(entry.Request.FormationId);
        if (fi >= 0) _f[fi].HoldExplicit = false;
    }

    /// <summary>[TR, V1 + V13] State appended to the state hash only when present (so battles without any tracked goal and
    /// without impetus profiles keep their encoding): goal minima per formation, and the run-up of impetus entities.</summary>
    private void AppendRound1State(IncrementalHash h)
    {
        long goals = 0, imp = 0;
        foreach (var f in _f) if (f.Goals is not null) goals += f.Goals.Count;
        foreach (var e in _e) if (e.P.Impetus != Q32.Zero) imp++;
        if (goals == 0 && imp == 0) return;
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(goals);
        foreach (var f in _f)
        {
            if (f.Goals is null) continue;
            for (int i = 0; i < f.Goals.Count; i++) { L(f.Id); L(f.Goals.CellAt(i)); L(f.Goals.BestAt(i)); }
        }
        L(imp);
        foreach (var e in _e) if (e.P.Impetus != Q32.Zero) { L(e.Id); L(e.LastStepMm); }
    }
}
