using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// What a waiting head's status needs to know of the head that holds it back (D-055; <see cref="HoldText.HeldForEarlier"/>):
/// the holder's own status (<see cref="Holder"/>, the same estimate — ConstructionQuery.HeadStatus — for its reason and the
/// remedy), the holder's EnqueuedTurn (<see cref="HolderTurn"/>) and the waiting head's own (<see cref="OwnTurn"/>) — the
/// building order's first key (−1: a migrated entry with no progress row). T1b fix round (2026-10-17, finding F7):
/// <see cref="UsedAhead"/> — the units of the waiting head's binding good the heads ahead of it in its purse DRAW this turn
/// (SetAsideQuery.DrawnAhead on the same walk), so the line's figures add up.
/// </summary>
public sealed record HoldContext(HeadStatusReport? Holder, long HolderTurn, long OwnTurn, long UsedAhead = 0);

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.6) — THE WORDS FOR MATERIAL RESERVATIONS.
/// One term: a project's units are "SET ASIDE" for it. "Held" keeps its two older meanings — possession ("held across your
/// settlements") and the building/roads split ("held for road works") — and never names a project's reservation, so the two
/// causes (and their remedies) never share a verb. Every line reads the hold rule through THE walk
/// (ConstructionQuery.HoldsWalk, via HeadStatus or SetAsideQuery) on the viewer's OWN purse (P7). Figures, never a percentage,
/// in any era.
/// </summary>
public static class HoldText
{
    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string GoodName(SimConfig cfg, int good) => cfg.Goods is { } g && good >= 0 ? g.ById(good).Name : "materials";

    /// <summary>"Alpha's granary".</summary>
    public static string Entry(SimConfig cfg, QueueEntryRef head, Func<int, string> name) =>
        name(head.Settlement.Value) + "'s " + ConstructionStatusText.ProjectName(cfg, head.ProjectId);

    /// <summary>
    /// THE WAITING HEAD'S LINE (MaterialCause.HeldForEarlier): the units of its binding good earlier heads set aside, the
    /// earliest holder, where it stands in the building order, the holder's own reason and the remedy —
    /// "waiting for stone: the empire's 12 are set aside for Alpha's granary, ordered earlier (turn 3), ahead in the building
    /// order - it has no builders (give Building a share in Labour at Alpha, or cancel it)". "ordered earlier" only when the
    /// holder's EnqueuedTurn is lower — its player-facing turn, EnqueuedTurn + 1, shown unless it is −1 (a migrated entry);
    /// an equal key reads "ordered the same turn, first in the building order". "N of the empire's M" when the purse holds
    /// more than is set aside; "and other projects" when the earliest holder sets aside fewer than all of them. T1b fix round
    /// (2026-10-17): two migrated entries (both keys −1: neither order turn is known) read "first in the building order" only
    /// (finding F10); what the heads ahead draw this turn is said too — "…, and 7 are used this turn by projects ahead of it"
    /// (finding F7).
    /// </summary>
    public static string HeldForEarlier(
        HeadStatusReport r, SimConfig cfg, bool empire, string here, Func<int, string> name, string buildingLabel, HoldContext? context)
    {
        int g = r.Resolution.LimitGood;
        string good = GoodName(cfg, g);
        long n = r.SetAsideAhead;
        string whose = empire ? "the empire's " : here + "'s ";
        string amount = r.HeldAtStart > n ? Inv(n) + " of " + whose + Inv(r.HeldAtStart) : whose + Inv(n);
        string line = "waiting for " + good + ": " + amount + (n == 1 ? " is" : " are") + " set aside for ";
        if (r.Holder is not { } holder) return line + "projects ahead in the building order";
        line += Entry(cfg, holder, name);
        long own = context?.Holder?.SetAside is { } sa && g >= 0 && g < sa.Length ? sa[g] : n;
        if (own < n) line += " and other projects";
        if (context is null) return line + ", ahead in the building order";
        line += context.HolderTurn < 0 && context.OwnTurn < 0 ? ", first in the building order"
            : context.HolderTurn < context.OwnTurn
                ? ", ordered earlier" + (context.HolderTurn >= 0 ? " (turn " + Inv(PlayerTurn.Current(context.HolderTurn)) + ")" : "")
                  + ", ahead in the building order"
                : ", ordered the same turn, first in the building order";
        if (context.UsedAhead > 0)
            line += ", and " + Inv(context.UsedAhead) + (context.UsedAhead == 1 ? " is" : " are") + " used this turn by projects ahead of it";
        return context.Holder is { } h && Reason(h, cfg, name(holder.Settlement.Value), buildingLabel) is { } why ? line + " - " + why : line;
    }

    /// <summary>
    /// The HOLDER's own reason and the remedy, from its own status: "it has no builders (give Building a share in Labour at
    /// Alpha, or cancel it)", "Alpha cannot yet sustain it (wait until it can, or cancel it)", "it is itself waiting for timber
    /// (cancel it to free them)", "it is being built a little each turn (give Building a larger share in Labour at Alpha to
    /// finish it sooner, or cancel it)". Null when the holder's status names no reason. T1b fix round (2026-10-17, finding F1):
    /// a holder that advances only as far as one of its goods allows (its limit Materials or Unobtainable) is not offered
    /// more labour, which would not move it — "it is being built only as far as its stone allows (cancel it to free them)",
    /// as its own row says ("as far as its stone allows"); the labour remedy is for a holder paced by its builders.
    /// </summary>
    public static string? Reason(HeadStatusReport holder, SimConfig cfg, string place, string buildingLabel)
    {
        HeadResolution hr = holder.Resolution;
        if (hr.Progresses)
            return hr.Limit is (HeadLimitKind.Materials or HeadLimitKind.Unobtainable) && hr.LimitGood >= 0
                ? "it is being built only as far as its " + GoodName(cfg, hr.LimitGood) + " allows (cancel it to free them)"
                : "it is being built a little each turn (give " + buildingLabel + " a larger share in Labour at " + place
                  + " to finish it sooner, or cancel it)";
        return hr.Limit switch
        {
            HeadLimitKind.Labour => hr.NoLabourAdults
                ? "no one at " + place + " can work (cancel it)"
                : "it has no builders (give " + buildingLabel + " a share in Labour at " + place + ", or cancel it)",
            HeadLimitKind.Viability => place + " cannot yet sustain it (wait until it can, or cancel it)",
            HeadLimitKind.Materials or HeadLimitKind.Unobtainable when hr.LimitGood >= 0 =>
                "it is itself waiting for " + GoodName(cfg, hr.LimitGood) + " (cancel it to free them)",
            HeadLimitKind.UnknownProject => "this project is no longer known (cancel it)",
            _ => null,
        };
    }

    /// <summary>
    /// THE HOLDER'S OWN ROW: what the head sets aside (<see cref="HeadStatusReport.SetAside"/>, ascending good id) —
    /// "sets aside 22 timber and 11 stone while it waits; projects ordered later wait for it" ("… for the rest of its work; …"
    /// for a head that advances this turn), and under a split of b &lt; 100 (<paramref name="split"/>) also "; the split lets
    /// road works use the rest". Null when it sets nothing aside (the rule off, a released head, a completing one).
    /// </summary>
    public static string? SetAsideLine(HeadStatusReport r, SimConfig cfg, bool split)
    {
        if (r.SetAside is not { Length: > 0 } sa) return null;
        var parts = new List<string>();
        for (int g = 0; g < sa.Length; g++) if (sa[g] > 0) parts.Add(Inv(sa[g]) + " " + GoodName(cfg, g));
        if (parts.Count == 0) return null;
        return "sets aside " + AllocationText.List(parts) + (r.Resolution.Progresses ? " for the rest of its work" : " while it waits")
            + "; projects ordered later wait for it" + (split ? "; the split lets road works use the rest" : "");
    }

    /// <summary>
    /// THE ROADS PREVIEW'S SET-ASIDE LINE: of each good the road order needs (<paramref name="goodIds"/>, in its cost order),
    /// what the viewer's heads (<paramref name="heads"/>, the split forecast's walk, in the building order) set aside —
    /// "40 timber is set aside for Alpha's granary; road works may not use it" ("…, 10 timber for Beta's workshop and 12 stone
    /// for Alpha's granary; road works may not use them"). Null when nothing the order needs is set aside.
    /// </summary>
    public static string? RoadsLine(HoldsWalkHead[] heads, int[] goodIds, SimConfig cfg, Func<int, string> name)
    {
        var parts = new List<string>();
        for (int h = 0; h < heads.Length; h++)
            for (int i = 0; i < goodIds.Length; i++)
            {
                long units = heads[h].HoldOf(goodIds[i]);
                if (units <= 0) continue;
                var head = new QueueEntryRef(heads[h].Entry.Settlement, heads[h].Entry.Slot, heads[h].Entry.ProjectId);
                parts.Add(Inv(units) + " " + GoodName(cfg, goodIds[i]) + (parts.Count == 0 ? " is set aside" : "") + " for " + Entry(cfg, head, name));
            }
        if (parts.Count == 0) return null;
        return AllocationText.List(parts) + "; road works may not use " + (parts.Count == 1 ? "it" : "them");
    }

    /// <summary>THE EMPIRE VIEW'S SUMMARY, from the viewer's OWN purse (SetAsideQuery.ViewerTotals — the published table):
    /// "Set aside for projects: 40 timber, 12 stone" (ascending good id). Null when nothing is set aside.</summary>
    public static string? EmpireSummary(IReadOnlyWorldState world, SimConfig cfg, PolityId viewer)
    {
        (int Good, long Units)[] totals = SetAsideQuery.ViewerTotals(world, cfg, viewer);
        if (totals.Length == 0) return null;
        var parts = new string[totals.Length];
        for (int i = 0; i < totals.Length; i++) parts[i] = Inv(totals[i].Units) + " " + GoodName(cfg, totals[i].Good);
        return "Set aside for projects: " + string.Join(", ", parts);
    }
}
