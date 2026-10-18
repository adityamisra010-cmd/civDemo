using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// WS1 (D-054) — THE WORDS FOR A QUEUE HEAD'S STATE, shared by the action surface and the settlement view so the two say
/// the same thing of the same reading. Every line is read from <see cref="ConstructionQuery.HeadStatus"/> — the one resolver
/// ConstructionSystem uses — and is an ESTIMATE (the turn re-reads the stocks production, trade and housing leave). The two
/// callers read DIFFERENT books on purpose: the action surface passes the player's pending orders (the queue as it will
/// stand at End Turn, pending cancels and enqueues applied), while the settlement view describes the COMMITTED queue and
/// passes none. So until End Turn delivers a pending order, the settlement view's status can differ from the action
/// surface's (e.g. a pending cancel of the head). No percentage in any era: figures are "N of M" (the research precedent).
/// Never "reserved".
/// </summary>
public static class ConstructionStatusText
{
    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
    private static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>The name of a goods.json project a settlement view or the queue shows.</summary>
    public static string ProjectName(SimConfig cfg, int projectId) =>
        cfg.Goods?.ProjectById(projectId) is { } p ? AvailableActionsQuery.ProjectName(cfg.Research, p) : "project " + Inv(projectId);

    /// <summary>"16 of 40 timber, 8 of 20 stone in" — the head's materials in, no labour, no percentage.</summary>
    public static string MaterialsIn(ConstructionProjectEntry project, long progress)
    {
        var parts = new List<string>(project.Inputs.Length);
        foreach (ProjectInput i in project.Inputs) parts.Add(Inv(ConstructionQuery.Delivered(i.Qty, progress)) + " of " + Inv(i.Qty) + " " + i.Good);
        return string.Join(", ", parts) + " in";
    }

    /// <summary>
    /// The status line of a head (<paramref name="r"/>): what the coming End Turn does to it, or why it waits —
    /// "waiting for stone: the empire has none", "waiting for stone: Alpha's Workshop, ordered earlier, takes the empire's
    /// 12 first", "waiting: no builders here - give Building a share in Labour", "waiting: the town cannot yet sustain it".
    /// <paramref name="buildingLabel"/> is the construction sector's knowledge-derived label (LabourActivities).
    /// </summary>
    public static string Status(HeadStatusReport r, SimConfig cfg, bool empire, string here, Func<int, string> name, string buildingLabel)
    {
        HeadResolution res = r.Resolution;
        GoodsConfig? goods = cfg.Goods;
        string good = res.LimitGood >= 0 && goods is not null ? goods.ById(res.LimitGood).Name : "materials";
        if (res.Completes) return "can be finished at End Turn (an estimate)";
        if (res.Progresses)
            return res.Limit == HeadLimitKind.Materials
                ? "work goes on at End Turn, as far as its " + good + " allows (an estimate)"
                : "work goes on at End Turn (an estimate)";
        string purse = empire ? "the empire" : here;
        return res.Limit switch
        {
            HeadLimitKind.UnknownProject => "waiting: this project is no longer known",
            HeadLimitKind.Viability => "waiting: the town cannot yet sustain it",
            HeadLimitKind.Labour => res.NoLabourAdults
                ? "waiting: no builders here - no one here can work"
                : "waiting: no builders here - give " + buildingLabel + " a share in Labour",
            HeadLimitKind.Unobtainable => "waiting for " + good + ": " + purse + (r.HeldAtStart > 0 ? " has too little" : " has none")
                + " and nothing here can make it yet",
            _ => r.Cause switch
            {
                MaterialCause.EmpireHasNone => "waiting for " + good + ": " + purse + " has none",
                MaterialCause.TakenByEarlier => "waiting for " + good + ": " + Earlier(r.TakenBy, cfg, name)
                    + ", ordered earlier, " + (r.TakenBy.Length == 1 ? "takes " : "take ")
                    + (empire ? "the empire's " : here + "'s ") + Inv(r.HeldAtStart) + " first",
                MaterialCause.HeldForRoads => "waiting for " + good + ": road works hold the rest of this turn's " + good
                    + " under the building/roads split",
                _ => "waiting for " + good + ": not enough yet",
            },
        };
    }

    private static string Earlier(QueueEntryRef[] heads, SimConfig cfg, Func<int, string> name)
    {
        var parts = new List<string>(heads.Length);
        foreach (QueueEntryRef h in heads) parts.Add(name(h.Settlement.Value) + "'s " + ProjectName(cfg, h.ProjectId));
        return parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[^1];
    }

    /// <summary>The waiting reason alone, for a head line "Next to build: Granary - not started; waiting for stone"
    /// (the part after "waiting"), or null when the head will advance.</summary>
    public static string? WaitingFor(HeadStatusReport r, SimConfig cfg)
    {
        HeadResolution res = r.Resolution;
        if (res.Progresses) return null;
        return res.Limit switch
        {
            HeadLimitKind.Materials or HeadLimitKind.Unobtainable when res.LimitGood >= 0 && cfg.Goods is { } g =>
                "waiting for " + g.ById(res.LimitGood).Name,
            HeadLimitKind.Labour => "waiting for builders",
            HeadLimitKind.Viability => "waiting until the town can sustain it",
            _ => "waiting",
        };
    }

    /// <summary>"Last turn: 8 timber and 4 stone went in" — the progress delta of <paramref name="entry"/> between the
    /// world the last End Turn stepped from and the current one; null when there is nothing to compare.</summary>
    public static string? LastTurn(IReadOnlyWorldState? previous, IReadOnlyWorldState world, SimConfig cfg, ConstructionQueueRow entry)
    {
        if (previous is null || cfg.Goods?.ProjectById(entry.ProjectId) is not { } project) return null;
        bool existed = false;
        for (int i = 0; i < previous.ConstructionQueue.Count && !existed; i++)
            existed = previous.ConstructionQueue[i] == entry;
        if (!existed) return null;
        long before = ConstructionQuery.ProgressOf(previous, entry), now = ConstructionQuery.ProgressOf(world, entry);
        if (now <= before) return "Last turn: nothing went in";
        var parts = new List<string>(project.Inputs.Length);
        foreach (ProjectInput i in project.Inputs)
        {
            long d = ConstructionQuery.Delivered(i.Qty, now) - ConstructionQuery.Delivered(i.Qty, before);
            if (d > 0) parts.Add(Inv(d) + " " + i.Good);
        }
        string what = parts.Count == 0 ? Num(ConstructionQuery.LabourBetween(project.LaborRequired, before, now)) + " adult-years of work"
            : parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[^1];
        return "Last turn: " + what + " went in";
    }

    /// <summary>"2nd", "3rd", "11th" — a queue position.</summary>
    public static string Ordinal(int n)
    {
        int mod100 = n % 100;
        string suffix = mod100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return Inv(n) + suffix;
    }
}
