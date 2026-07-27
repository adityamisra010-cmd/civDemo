using Sim.Core.State;

namespace Sim.Core.Systems.Consumption;

/// <summary>One resolved basket line: (class, need, good) at a per-person-year rate.</summary>
public readonly record struct BasketLine(ClassId Class, int Need, GoodId Good, double PerPersonYear);

/// <summary>
/// T3.5 — the resolved D-035-C basket book. needs.json names goods as STRINGS
/// (ADR-001: names live in config, rows carry ids); this resolves them once, at
/// construction, against goods.json and refuses unknown names loudly.
///
/// WHY A SHARED UNIT AND NOT A SYSTEM. Two systems need the same arithmetic:
/// ConsumptionSystem turns basket lines into flows out of the goods stores, and
/// NeedsGrievanceSystem turns the same lines plus the published fill ratios into
/// per-class satisfaction. Law 6 forbids the systems referencing each other, and
/// duplicating the demand equation in both would let them drift — a settlement
/// could be charged for a basket it was not credited with satisfying. A pure
/// static book with no state is neither a system nor a channel between systems;
/// it is the shared reading of one config file.
///
/// ORDER. Lines are sorted by (class, need, good id) at construction, so every
/// iteration over the book is in one fixed order regardless of how needs.json
/// was written (law 5 — no dictionary iteration, no authoring-order dependence).
/// </summary>
public sealed class BasketBook
{
    /// <summary>
    /// The Sustenance need id in the D-018 registry — the need whose basket
    /// lines are NUTRITION, and therefore the ones that are cohort-weighted,
    /// substitutable into the staple, and counted by the food-surplus ratio.
    ///
    /// It lives HERE, on the shared book, and not on ConsumptionSystem. Law 6
    /// says systems never reference each other, and it says nothing about
    /// `const` being exempt: NeedsGrievanceSystem reading
    /// `ConsumptionSystem.SustenanceNeedId` was a reference between systems
    /// even though a compile-time constant carries no runtime coupling. Every
    /// system that needs this id already reads the book.
    /// </summary>
    public const int SustenanceNeedId = 1;

    private readonly BasketLine[] _lines;
    private readonly GoodId[] _goods;       // distinct, ascending
    private readonly GoodId[] _foodGoods;   // distinct, ascending, Sustenance only

    public BasketBook(NeedsConfig needs, GoodsConfig goods)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(goods);

        BasketEntry[] entries = needs.Baskets.Entries;
        var lines = new BasketLine[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            int id = goods.IdOf(entries[i].Good);
            if (id < 0)
                throw new NeedsConfigException(
                    $"baskets.entries[{i}] names good '{entries[i].Good}', which is not in the "
                    + "goods registry — needs.json and goods.json disagree.");
            lines[i] = new BasketLine(
                new ClassId(entries[i].Class), entries[i].Need, new GoodId(id), entries[i].PerPersonYear);
        }
        Array.Sort(lines, static (a, b) =>
        {
            int c = a.Class.Value.CompareTo(b.Class.Value);
            if (c != 0) return c;
            c = a.Need.CompareTo(b.Need);
            return c != 0 ? c : a.Good.Value.CompareTo(b.Good.Value);
        });
        _lines = lines;

        var distinct = new List<GoodId>();
        for (int i = 0; i < lines.Length; i++)
            if (!distinct.Contains(lines[i].Good)) distinct.Add(lines[i].Good);
        distinct.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        _goods = [.. distinct];

        var food = new List<GoodId>();
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Need == SustenanceNeedId && !food.Contains(lines[i].Good)) food.Add(lines[i].Good);
        food.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        _foodGoods = [.. food];
    }

    /// <summary>Every line, in (class, need, good) order.</summary>
    public ReadOnlySpan<BasketLine> Lines => _lines;

    /// <summary>Every good any basket wants, ascending — the fixed iteration
    /// order for consumption flows.</summary>
    public ReadOnlySpan<GoodId> Goods => _goods;

    /// <summary>The goods that are FOOD — those serving Sustenance, ascending.
    /// Food lines are denominated in person-year-equivalents of nutrition, so
    /// these goods' units are commensurable with each other and with the
    /// nutritional requirement; a unit of any of them feeds the same person for
    /// the same time. That is what makes summing production across them
    /// meaningful, and it is why the food-surplus ratio must count all of them
    /// rather than grain alone.</summary>
    public ReadOnlySpan<GoodId> FoodGoods => _foodGoods;

    /// <summary>The lines of one (class, need) basket, in good order. Returns an
    /// empty span for a class that declares no basket for that need — an absent
    /// basket is absent demand, never a hidden default.</summary>
    public ReadOnlySpan<BasketLine> Basket(ClassId cls, int need)
    {
        int start = -1, end = -1;
        for (int i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].Class != cls || _lines[i].Need != need) continue;
            if (start < 0) start = i;
            end = i;
        }
        return start < 0 ? [] : _lines.AsSpan(start, end - start + 1);
    }
}
