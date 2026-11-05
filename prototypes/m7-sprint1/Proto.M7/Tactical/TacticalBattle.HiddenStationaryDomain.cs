using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// [TR x lane N] How the DIR5 §2.6 hidden-stationary rule (lane TR, <see cref="HiddenStationaryRule"/>) meets the naval / air /
/// combined-arms modules (lane N, TacticalBattle.Domain*.cs). The rule JUDGES LAND formations only (landed troops included:
/// they are land formations); vessel and aircraft formations are never routed by it — whether and how DIR5 §2.6 applies to a
/// hidden idle fleet or flight is NOT decided here (open point for the Director / lane N). They take part as follows:
/// <list type="number">
/// <item>PROGRESS: a vessel / aircraft formation with a present member that had primary activity this pulse, or with a state
///   change (lane N's own activity definition, the one its <c>DomainPulseEnd</c> uses), is progress for the whole-field idle
///   counter G. Without it a battle fought only by ships or aircraft would end as a Standoff after five pulses while they
///   fight (measured on the merge commit: the naval line battles did exactly that).</item>
/// <item>LANE-N BOOKKEEPING: <c>DomainPulseEnd</c> still runs at every pulse end (vessel morale recovery under Hold / Rally and
///   lane N's per-formation counter, which is part of the state hash); its side verdicts are discarded (no side-disengage
///   under DIR5).</item>
/// <item>MUTUAL GUARD (an OPTION, <c>TacticalRules.MutualStandoffGuard</c>, default OFF since verify-TR-r1 V3; by default both
///   sides' inactive formations rout and the battle ends BothEliminated): when it is ON, the routs of one pulse end may not
///   eliminate BOTH sides at once — the guard fires iff after the routs neither side keeps an Active entity of ANY domain
///   (counted as for elimination, <c>DomainActiveCounts</c>). In a land-only battle this is exactly the land guard ("every
///   formation with Active members would rout"). [verify-TR-r2 V8: this item still described the pre-r1 default.]</item>
/// <item>EXEMPTION (a): "concealed" includes lane N's personal concealment (camouflage &lt; 1, the sniper — DIR5 §2.6 speaks of
///   ACTIVE CONCEALMENT, not only terrain), and the enemy within engagement reach may be an on-field enemy vessel or aircraft
///   (horizontal distance, as lane N's searcher clause <c>DomainSearcherNear</c> measures it).</item>
/// <item>BARE POINT FIRE: under DIR5 a shot of an area weapon at a bare POINT (Bombard with no target entity) is not "attacking
///   a valid target" (D-tactical.md §4) and so is not progress by itself — for the formation's own counter and for G. Its blast
///   is progress when it lands on someone (the hit credits the shooter, <c>CreditSource</c>). Reason: with unlimited
///   ammunition a point bombardment is an UNBOUNDED source of activity, so counting it would let a battle run forever
///   (measured: the amphibious battle tripped the 400-pulse harness guard with item 1 alone). Implemented by restoring the
///   shooter's Primary flag to its value just before the shot, so activity before or after the shot in the same pulse still
///   counts exactly. The legacy model is unchanged.</item>
/// </list>
/// </summary>
public sealed partial class TacticalBattle
{
    /// <summary>[TR x lane N] Item 1: did any vessel / aircraft formation make progress this pulse (lane N's activity definition:
    /// a present member with primary activity, or a state change)?</summary>
    private bool DomainProgressThisPulse()
    {
        foreach (var f in _df)
        {
            bool present = false, act = f.StateChanged;
            foreach (int i in f.Members) if (_d[i].Present) { present = true; if (_d[i].Primary) act = true; }
            if (present && act) return true;
        }
        return false;
    }

    /// <summary>[TR x lane N] Item 2: lane N's pulse-end bookkeeping (vessel morale recovery, lane N's counters) without its
    /// side-disengage verdict.</summary>
    private void DomainPulseEndBookkeeping() => DomainPulseEnd(new bool[2], new[] { true, true });

    /// <summary>[TR x lane N] Item 3: true iff, once the formations flagged in <paramref name="routing"/> rout, NEITHER side keeps an
    /// Active entity of any domain (land entities in formations that do not rout now, plus the domain force counted as for
    /// elimination).</summary>
    private bool NoSideKeepsAnActiveEntityAfter(bool[] routing)
    {
        int a0 = 0, a1 = 0;
        foreach (var e in _e)
            if (e.State == EntityState.Active && !routing[e.F]) { if (e.Side == 0) a0++; else a1++; }
        if (_hasDomain) DomainActiveCounts(ref a0, ref a1);
        return a0 == 0 && a1 == 0;
    }

    /// <summary>[TR x lane N] Item 4: is an on-field enemy vessel / aircraft of <paramref name="side"/> within the horizontal
    /// distance whose square is <paramref name="limSq"/> of <paramref name="pos"/>?</summary>
    private bool DomainEnemyWithin(int side, Vec2L pos, Int128 limSq)
    {
        foreach (var x in _d)
            if (x.Side != side && x.OnField && Vec2L.DistanceSq(x.Pos, pos) <= limSq) return true;
        return false;
    }

    /// <summary>[TR x lane N] Item 5: the Primary flag of a shooter (land or domain entity) before its shot.</summary>
    private bool ShooterPrimary(long id)
    {
        int li = Array.BinarySearch(_ids, id);
        return li >= 0 ? _e[li].Primary : _d[DIndex(id)].Primary;
    }

    /// <summary>[TR x lane N] Item 5: under DIR5, undo the progress credit of a bare point shot (restore the pre-shot flag).</summary>
    private void UndoBarePointShotProgress(long id, bool before)
    {
        if (_r.Inactivity != InactivityModel.HiddenStationary) return;
        int li = Array.BinarySearch(_ids, id);
        if (li >= 0) _e[li].Primary = before;
        else _d[DIndex(id)].Primary = before;
    }

    /// <summary>[TR x lane N] Item 4: concealed for exemption (a) — the entity's cell conceals (multiplier &lt; 1) or it carries
    /// personal concealment &lt; 1 (lane N camouflage).</summary>
    private bool ConcealedForExemption(Ent e)
    {
        var c = _grid.CellOf(e.Pos);
        return _conceal.Multiplier(_grid.ClassAt(c.X, c.Y)) < Q32.One || e.P.PersonalConcealment < Q32.One;
    }
}
