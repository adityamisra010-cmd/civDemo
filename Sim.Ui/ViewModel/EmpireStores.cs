using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>The three groups of the resource strip, from goods.json's categories (food | raw | processed).</summary>
public enum StoreGroup
{
    Food = 0,
    Materials = 1,
    Goods = 2,
}

/// <summary>One of the player's settlements holding a good, and how much.</summary>
public readonly record struct StoreHolder(int Settlement, long Amount);

/// <summary>
/// One good of the player's empire: the empire's stores (<see cref="Total"/>), the stores at the start of the last turn
/// (<see cref="Previous"/>; meaningful only when <see cref="HasPrevious"/>), what the player's own purse has set aside for
/// projects (<see cref="SetAside"/>), and the player's settlements holding it, largest first.
/// </summary>
public sealed record StoreGood(
    int Good, string Name, StoreGroup Group, long Total, long Previous, bool HasPrevious, long SetAside, StoreHolder[] Holders)
{
    /// <summary>The change since last turn (0 before the first turn).</summary>
    public long Change => HasPrevious ? Total - Previous : 0;
}

/// <summary>One group of the strip: the sum of its shown goods, now and at the start of the last turn.</summary>
public sealed record StoreGroupSummary(StoreGroup Group, long Total, long Previous, bool HasPrevious, StoreGood[] Goods)
{
    public long Change => HasPrevious ? Total - Previous : 0;
}

/// <summary>Which way a figure moved since last turn.</summary>
public enum StoreTrend
{
    /// <summary>No turn has been played: there is nothing to compare with.</summary>
    None = 0,
    Steady = 1,
    Rising = 2,
    Falling = 3,
}

/// <summary>
/// M5 resources view (the Director's 2026-10-19 instruction: "yes build resources view … i should not be able to see other
/// empire's data") — THE PLAYER'S EMPIRE STORES, as the top bar's resource strip, its hover cards and the RESOURCES view
/// read them. A pure function of (the world now, the world the last End Turn stepped from, the config, the player).
///
/// <para><b>Own empire only.</b> The player's settlements are the places the player's Empire controls — the control relation
/// (<c>ControlRow</c>, D-042 §5; <see cref="OwnSettlements"/>), the same set <c>EmpireStockpile.Holders</c> pools for the
/// player's purse and the EMPIRE view counts. The rows read are those settlements' <c>GoodStocks</c> rows and the player's OWN set-asides
/// (<see cref="SetAsideQuery.ViewerTotals"/>, P7: no other purse). No foreign settlement's row, no foreign polity's purse,
/// and no settlement row of the UI's own (the Sim.Core query enumerates them) is read — what the player knows of a rival is not
/// what the player owns.</para>
///
/// <para><b>Totals</b> are exact long sums, each settlement's FIRST row of the good (<see cref="EmpireStockpile.Available"/> —
/// the purse's own sum). <b>Change since last turn</b> compares with the same sum over the player's settlements of the world
/// the last End Turn stepped from (the empire as it was then: a settlement won or lost counts in the change).</para>
///
/// <para><b>Which goods are shown.</b> A good is shown when the empire holds some of it now, held some at the start of the
/// last turn (so a good that just ran out reads 0 with a falling mark rather than vanishing), or has some set aside. A good
/// the player has never held is not shown — the Stone Age strip stays short. Holding a good is how the player comes to use
/// it (extraction is endowment-bound, a recipe needs its knowledge), so no research rule is restated here. Arrays are
/// indexed in goods.json order; no dictionary, no LINQ. Counts only, never a percentage (the numeral-free eras).</para>
/// </summary>
public sealed record EmpireStores(int[] Settlements, StoreGood[] AllGoods, long[][] Amounts, StoreGroupSummary[] Groups)
{
    /// <summary>The goods shown, in goods.json order.</summary>
    public StoreGood[] Shown
    {
        get
        {
            var list = new List<StoreGood>();
            foreach (StoreGroupSummary g in Groups) list.AddRange(g.Goods);
            list.Sort((a, b) => a.Good.CompareTo(b.Good));
            return [.. list];
        }
    }

    /// <summary>The player's settlements: the places the Empire <paramref name="player"/> controls (ControlRow), ascending
    /// id — <see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId)"/>, the civilization's resource
    /// base (in a hand-built world with no control rows at all, every settlement: there is no other Empire). The strip, the
    /// cards, RESOURCES and the status band's POPULATION / FOOD / settlement count all read this one set.</summary>
    public static int[] OwnSettlements(IReadOnlyWorldState world, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(world);
        SettlementId[] ids = RoadDevelopmentQuery.PayingSettlements(world, player);
        var result = new int[ids.Length];
        for (int i = 0; i < ids.Length; i++) result[i] = ids[i].Value;
        return result;
    }

    /// <summary>The strip group of a goods.json category.</summary>
    public static StoreGroup GroupOf(string category) => category switch
    {
        "food" => StoreGroup.Food,
        "raw" => StoreGroup.Materials,
        _ => StoreGroup.Goods,
    };

    /// <summary>Builds the player's stores. <paramref name="previous"/> is the world the last End Turn stepped from (null
    /// before the first turn: no change is shown).</summary>
    public static EmpireStores Build(IReadOnlyWorldState world, IReadOnlyWorldState? previous, SimConfig cfg, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        int[] own = OwnSettlements(world, player);
        var ownIds = new SettlementId[own.Length];
        for (int i = 0; i < own.Length; i++) ownIds[i] = new SettlementId(own[i]);
        SettlementId[] prevIds = [];
        if (previous is not null)
        {
            int[] p = OwnSettlements(previous, player);
            prevIds = new SettlementId[p.Length];
            for (int i = 0; i < p.Length; i++) prevIds[i] = new SettlementId(p[i]);
        }
        (int Good, long Units)[] setAside = SetAsideQuery.ViewerTotals(world, cfg, player);

        IReadOnlyList<GoodEntry> goods = cfg.Goods is { } gc ? gc.Goods : [];
        var all = new StoreGood[goods.Count];
        var amounts = new long[goods.Count][];
        for (int g = 0; g < goods.Count; g++)
        {
            GoodEntry entry = goods[g];
            var gid = new GoodId(entry.Id);
            long total = EmpireStockpile.Available(world.GoodStocks, ownIds, gid);
            long prevTotal = previous is null ? 0 : EmpireStockpile.Available(previous.GoodStocks, prevIds, gid);
            long aside = 0;
            for (int k = 0; k < setAside.Length; k++) if (setAside[k].Good == entry.Id) aside = setAside[k].Units;
            amounts[g] = new long[own.Length];
            var holders = new List<StoreHolder>();
            for (int s = 0; s < own.Length; s++)
            {
                int idx = GoodStockIndex.IndexOf(world.GoodStocks, ownIds[s], gid);
                long amount = idx >= 0 ? world.GoodStocks[idx].Amount.Value : 0;
                amounts[g][s] = amount;
                if (amount > 0) holders.Add(new StoreHolder(own[s], amount));
            }
            // Largest first; ties to the lower settlement id (an integer composite key — no double is compared).
            holders.Sort((a, b) => a.Amount != b.Amount ? b.Amount.CompareTo(a.Amount) : a.Settlement.CompareTo(b.Settlement));
            all[g] = new StoreGood(entry.Id, entry.Name, GroupOf(entry.Category), total, prevTotal, previous is not null, aside, [.. holders]);
        }

        var groups = new List<StoreGroupSummary>();
        foreach (StoreGroup group in new[] { StoreGroup.Food, StoreGroup.Materials, StoreGroup.Goods })
        {
            var shown = new List<StoreGood>();
            long total = 0, prev = 0;
            foreach (StoreGood good in all)
            {
                if (good.Group != group || !IsShown(good)) continue;
                shown.Add(good);
                total = checked(total + good.Total);
                prev = checked(prev + good.Previous);
            }
            if (shown.Count > 0) groups.Add(new StoreGroupSummary(group, total, prev, previous is not null, [.. shown]));
        }
        return new EmpireStores(own, all, amounts, [.. groups]);
    }

    /// <summary>Whether a good is shown (see the header): held now, held at the start of the last turn, or set aside.</summary>
    public static bool IsShown(StoreGood good) => good.Total > 0 || (good.HasPrevious && good.Previous > 0) || good.SetAside > 0;

    /// <summary>The group summary of <paramref name="group"/>, or null when none of its goods is shown.</summary>
    public StoreGroupSummary? Group(StoreGroup group)
    {
        foreach (StoreGroupSummary g in Groups) if (g.Group == group) return g;
        return null;
    }

    /// <summary>The good with id <paramref name="good"/>, or null.</summary>
    public StoreGood? Good(int good)
    {
        foreach (StoreGood g in AllGoods) if (g.Good == good) return g;
        return null;
    }

    /// <summary>The settlement column index of <paramref name="settlement"/> in <see cref="Amounts"/>, or −1.</summary>
    public int ColumnOf(int settlement) => Array.IndexOf(Settlements, settlement);

    // ------------------------------------------------------------------ words (counts only, every era)

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>The strip's label of a group.</summary>
    public static string Label(StoreGroup group) => group switch
    {
        StoreGroup.Food => "Food stores",
        StoreGroup.Materials => "Materials",
        _ => "Goods",
    };

    /// <summary>The hover card's title of a group.</summary>
    public static string CardTitle(StoreGroup group) => group switch
    {
        StoreGroup.Food => "Food in your stores",
        StoreGroup.Materials => "Materials in your stores",
        _ => "Goods in your stores",
    };

    /// <summary>The strip's value: the group's empire total.</summary>
    public static string Value(StoreGroupSummary g) => N(g.Total);

    /// <summary>Which way <paramref name="change"/> points (None before the first turn).</summary>
    public static StoreTrend TrendOf(bool hasPrevious, long change) =>
        !hasPrevious ? StoreTrend.None : change > 0 ? StoreTrend.Rising : change < 0 ? StoreTrend.Falling : StoreTrend.Steady;

    /// <summary>"+12", "-5", "no change", "-" (no turn played yet).</summary>
    public static string ChangeText(bool hasPrevious, long change) =>
        !hasPrevious ? "-" : change > 0 ? "+" + N(change) : change < 0 ? "-" + N(-change) : "no change";

    /// <summary>The words the strip's mark stands for (the card's first line and the mark's FrameText).</summary>
    public static string ChangeSentence(bool hasPrevious, long change) =>
        !hasPrevious ? "No turn played yet: nothing to compare with."
        : change > 0 ? N(change) + " more than at the start of last turn."
        : change < 0 ? N(-change) + " fewer than at the start of last turn."
        : "The same as at the start of last turn.";

    /// <summary>The hover card's lines for one group: a summary, then one line per good — its empire total, the change
    /// since last turn, what is set aside for projects, and its largest holding settlements (the player's own, at most
    /// <paramref name="holders"/>).</summary>
    public static IReadOnlyList<string> CardLines(StoreGroupSummary g, Func<int, string> name, int holders = 3)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(name);
        var lines = new List<string> { "Your empire holds " + N(g.Total) + ". " + ChangeSentence(g.HasPrevious, g.Change) };
        foreach (StoreGood good in g.Goods) lines.Add(GoodLine(good, name, holders));
        return lines;
    }

    /// <summary>"timber: 120 (+12 since last turn); 40 set aside for projects; most at Alpha 80, Beta 40".</summary>
    public static string GoodLine(StoreGood good, Func<int, string> name, int holders = 3)
    {
        ArgumentNullException.ThrowIfNull(good);
        ArgumentNullException.ThrowIfNull(name);
        string line = good.Name + ": " + N(good.Total);
        if (good.HasPrevious) line += good.Change == 0 ? " (no change since last turn)" : " (" + ChangeText(true, good.Change) + " since last turn)";
        if (good.SetAside > 0) line += "; " + N(good.SetAside) + " set aside for projects";
        if (good.Holders.Length > 0)
        {
            var parts = new List<string>();
            for (int i = 0; i < good.Holders.Length && i < holders; i++) parts.Add(name(good.Holders[i].Settlement) + " " + N(good.Holders[i].Amount));
            line += "; most at " + string.Join(", ", parts);
            if (good.Holders.Length > holders)
                line += " and " + (good.Holders.Length - holders == 1 ? "one other" : N(good.Holders.Length - holders) + " others");
        }
        else line += "; none in store";
        return line;
    }

    /// <summary>A figure for the stores table.</summary>
    public static string Figure(long v) => N(v);
}
