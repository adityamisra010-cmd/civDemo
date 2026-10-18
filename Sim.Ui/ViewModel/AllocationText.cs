using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// WS2 (D-054) — THE WORDS FOR THE BUILDING/ROADS MATERIAL SPLIT, shared by the action surface's Materials block, the
/// construction and roads blocks and the EMPIRE view, so each says the same thing of the same reading
/// (State.ResourceAllocation's rule). The shares are GUARANTEED SHARES of what the empire holds when building work starts —
/// never amounts spent regardless of need — and the wording says so. Never "reserved". The numeral-free eras (A1–A3) get
/// "shares in 10" and words for fractions, never a percent sign; the numeral eras may print percentages. D-055 (2026-10-16):
/// a project's reservation is "set aside" (HoldText); "held" here means the split's share for road works only.
/// </summary>
public static class AllocationText
{
    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>The goods the split covers, from content (every project input and road material), in good-id order:
    /// "timber, stone and tools".</summary>
    public static string Goods(SimConfig cfg)
    {
        int[] ids = ResourceAllocation.EligibleGoodIds(cfg);
        if (ids.Length == 0 || cfg.Goods is not { } goods) return "building materials";
        var names = new List<string>(ids.Length);
        foreach (int id in ids) names.Add(goods.ById(id).Name);
        return List(names);
    }

    /// <summary>"a, b and c".</summary>
    public static string List(IReadOnlyList<string> parts)
    {
        if (parts.Count == 0) return "";
        if (parts.Count == 1) return parts[0];
        var head = new string[parts.Count - 1];
        for (int i = 0; i < head.Length; i++) head[i] = parts[i];
        return string.Join(", ", head) + " and " + parts[^1];
    }

    /// <summary>The default's line (D-055, the hold rule on): "Building first (default): road works get only what is left after
    /// building work uses or sets aside what it needs."</summary>
    public const string DefaultLine = "Building first (default): road works get only what is left after building work uses or sets aside what it needs.";

    /// <summary>The default's line with the hold rule off (TestConfigs.PreReservations — batch 3's words exactly).</summary>
    public const string DefaultLineWithoutHolds = "Building first (default): road works get only what building work leaves.";

    /// <summary>The default's line for <paramref name="cfg"/>: <see cref="DefaultLine"/> with the hold rule on
    /// (Reservations.Enabled), <see cref="DefaultLineWithoutHolds"/> with it off.</summary>
    public static string DefaultLineFor(SimConfig cfg) => Reservations.Enabled(cfg) ? DefaultLine : DefaultLineWithoutHolds;

    /// <summary>The block's explanation: the two guaranteed shares with spill-over, the turns without road works, the
    /// dwellings' own draw. At the default (b = 100) the shares are not restated (the default line says it).</summary>
    public static string Explanation(int buildingsPercent, string goods)
    {
        string share = buildingsPercent == ResourceAllocation.DefaultBuildingsPercent
            ? "On a turn you order road works, this split decides how the empire's " + goods + " are shared between building work and road works."
            : "On a turn you order road works, building work is guaranteed " + BuildingShares(buildingsPercent) + " of the empire's " + goods
              + ", and road works " + RoadShares(buildingsPercent) + ".";
        return share + " Whatever one side does not need, the other may use. On turns without road works, building work may use "
            + "all of it. Dwellings take their own settlement's timber before either.";
    }

    /// <summary>"7 shares in 10" (b a multiple of 10) or "73 shares in 100".</summary>
    public static string BuildingShares(int b) => b % 10 == 0 ? Inv(b / 10) + " shares in 10" : Inv(b) + " shares in 100";

    /// <summary>Road works' share, in the same unit as <see cref="BuildingShares"/>: "3" or "27".</summary>
    public static string RoadShares(int b) => b % 10 == 0 ? Inv((100 - b) / 10) : Inv(100 - b);

    /// <summary>The control's readout: "Building 70% · Roads 30%" in the numeral eras; "building work keeps 7 shares in 10,
    /// road works 3" in the numeral-free eras.</summary>
    public static string Readout(int b, bool numerals) => numerals
        ? "Building " + Inv(b) + "% · Roads " + Inv(100 - b) + "%"
        : "building work keeps " + BuildingShares(b) + ", road works " + RoadShares(b);

    /// <summary>"Set this turn: building 5, roads 5 - applies at End Turn, to this turn's work".</summary>
    public static string Queued(int b, bool numerals) =>
        "Set this turn: " + (numerals ? "building " + Inv(b) + "%, roads " + Inv(100 - b) + "%"
            : b % 10 == 0 ? "building " + Inv(b / 10) + ", roads " + Inv((100 - b) / 10)
            : "building " + Inv(b) + " in 100, roads " + Inv(100 - b))
        + " - applies at End Turn, to this turn's work";

    /// <summary>The EMPIRE view's read-only line: "Materials when roads are ordered: building work keeps 7 shares in 10,
    /// road works 3 (default: building first)"; at the default, D-055 (<paramref name="setsAside"/>: the hold rule on)
    /// "… building first (default) - road works get only what is left after building work uses or sets aside what it needs",
    /// batch 3's "… - road works get only what building work leaves" with it off.</summary>
    public static string EmpireLine(int b, bool setsAside = true) => b == ResourceAllocation.DefaultBuildingsPercent
        ? setsAside
            ? "Materials when roads are ordered: building first (default) - road works get only what is left after building work uses or sets aside what it needs"
            : "Materials when roads are ordered: building first (default) - road works get only what building work leaves"
        : "Materials when roads are ordered: building work keeps " + BuildingShares(b) + ", road works " + RoadShares(b)
          + " (default: building first)";

    /// <summary>A fraction of an order in words: "all of it", "none of it", "about two-fifths of it" (nearest fifth,
    /// never rounded to all or none).</summary>
    public static string ShareWords(double fraction)
    {
        if (fraction >= 1.0) return "all of it";
        if (!(fraction > 0.0)) return "none of it";
        int fifths = Math.Clamp((int)Math.Round(fraction * 5.0, MidpointRounding.AwayFromZero), 1, 4);
        return fifths switch { 1 => "about a fifth of it", 2 => "about two-fifths of it", 3 => "about three-fifths of it", _ => "about four-fifths of it" };
    }

    /// <summary>A part of an order in the numeral eras: "all of it" (<paramref name="part"/> ≥ <paramref name="whole"/>),
    /// "none of it" (nothing), otherwise "about N% of it" with N = ⌊100 · part / whole⌋ held to 1–99 — FLOORED and never 100
    /// or 0 for a partial payment (WS1's rule: "floor it and cap it at 99"; <see cref="ShareWords"/>' 1–4 fifths in the
    /// numeral-free eras). Exact integer arithmetic on the units: no double is rounded. Batch 3 R3 (C2 review F9): the
    /// roads preview rounded, so a road one timber short of its 1535 read "about 100% of it".</summary>
    public static string SharePercent(long part, long whole)
    {
        if (whole <= 0 || part >= whole) return "all of it";
        if (part <= 0) return "none of it";
        long pct = Math.Clamp((long)((Int128)part * 100 / whole), 1L, 99L);
        return "about " + Inv(pct) + "% of it";
    }

    /// <summary>
    /// The construction block's line (it reads ONLY the road order queued this turn): "With no road works ordered this
    /// turn, building work may use everything it needs.", the default's "building first" line, the starvation guard's "The
    /// road works ordered this turn could pay nothing from their share: …", or "This turn building work
    /// may use up to: 42 timber, 18 stone - the rest is held for road works under the split (an estimate on current
    /// stocks)." Null when the split does not apply. D-055 (the hold rule on): the no-road and default lines say that building
    /// work uses OR SETS ASIDE what it needs, oldest order first — "With no road works ordered this turn, building work may use
    /// or set aside everything it needs, oldest order first." and "Building first (default): this turn building work may use
    /// or set aside everything it needs, oldest order first, and road works get what is left."; batch 3's words with it off.
    /// </summary>
    public static string? ConstructionLine(AllocationView? view, SimConfig cfg)
    {
        if (view is null) return null;
        bool holds = Reservations.Enabled(cfg);
        if (view.RoadOrder is null)
            return holds
                ? "With no road works ordered this turn, building work may use or set aside everything it needs, oldest order first."
                : "With no road works ordered this turn, building work may use everything it needs.";
        if (view.IsDefault)
            return holds
                ? "Building first (default): this turn building work may use or set aside everything it needs, oldest order first, and road works get what is left."
                : "Building first (default): this turn building work may use everything it needs, and road works get what it leaves.";
        if (view.Split.RoadPaysNothingFromItsShare)
            return "The road works ordered this turn could pay nothing from their share: building work may use everything it needs.";
        var parts = new List<string>();
        bool held = false;
        for (int i = 0; i < view.Split.Goods.Length; i++)
        {
            AllocationSplit s = view.Split.Splits[i];
            if (s.Supply <= 0) continue;
            parts.Add(Inv(s.BuildingCap) + " " + GoodName(cfg, view.Split.Goods[i]));
            if (s.BuildingCap < s.Supply) held = true;
        }
        if (parts.Count == 0 || !held)
            return "The road works ordered this turn need nothing building work would use: building work may use everything it needs.";
        return "This turn building work may use up to: " + string.Join(", ", parts)
            + " - the rest is held for road works under the split (an estimate on current stocks).";
    }

    /// <summary>A good's content name.</summary>
    public static string GoodName(SimConfig cfg, int good) => cfg.Goods is { } g ? g.ById(good).Name : "good " + Inv(good);
}
