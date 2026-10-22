using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// M6 Track EX (D-062; ADR-044 §2–§3) — CONCEALED DEPOSITS: THE ONE PREDICATE every deposit consumer reads through.
///
/// <para><b>Eligibility (D1).</b> The denominator is the existing <see cref="DepositRow"/> per (settlement, good), written at
/// founding and colonization — generation, quantities and placement are untouched. A row is ELIGIBLE when its abundance is
/// above 0 and its good is named in sim.json <c>discovery.concealableGoods</c> (the geological deposits; biological and food
/// goods are surface-evident and never concealed).</para>
///
/// <para><b>Classification (D2) — derived, never stored.</b> For a settlement with n eligible rows, k = floor(n × s + u) are
/// CONCEALED, s = <c>discovery.concealedShare</c> and u ∈ [0, 1) a stateless SplitMix64 hash of (world seed,
/// <see cref="ConcealSalt"/>, settlement) — so the expected share is exactly s. The concealed rows are the k LOWEST by the
/// composite key (hash(seed, salt, settlement, good), goodId): an integer key with a stable integer tie-break, no double is
/// ordered. Nothing here draws from RngRegistry, so concealment cannot move any stream.</para>
///
/// <para><b>Effect (D3).</b> A concealed row is UNDISCOVERED for polity P until <see cref="SettlementSurveyRow"/>(P, settlement)
/// exists (the Prospect order). For the settlement's CURRENT CONTROLLER an undiscovered concealed row reads as abundance 0 in
/// every consumer (<see cref="Abundance(IReadOnlyWorldState, SimConfig?, in DepositRow)"/>): production, labour, construction,
/// the info cards, the site assessment and the resource overlay. A settlement no polity controls has no surveyor, so its
/// concealed rows read 0 too; a settlement that changes hands is undiscovered for its new controller until that polity
/// prospects it. Developer surfaces (Explain, snapshot diff) read the raw table.</para>
///
/// <para>Switch: <c>discovery.enabled</c> false (or the section absent, or no goods content) conceals nothing — every read
/// returns <see cref="DepositRow.Abundance"/> unchanged. Pure, read-only, no dictionary, no LINQ, doubles only for ratios (law 5).</para>
/// </summary>
public static class DepositConcealment
{
    /// <summary>The salt of the concealment hash — distinct from every founding salt, so concealment is independent of the
    /// deposit roll it classifies.</summary>
    public const ulong ConcealSalt = 0xC0CEA1ED_5EC2E7A5UL;

    /// <summary>The good a Prospect order sinks (goods.json "tools").</summary>
    public const string ProspectGood = "tools";

    /// <summary>Whether concealment is in force for <paramref name="cfg"/>.</summary>
    public static bool Enabled(SimConfig? cfg) => cfg?.Discovery is { Enabled: true } && cfg.Goods is not null;

    /// <summary>Whether good <paramref name="good"/> is concealable under <paramref name="cfg"/>.</summary>
    public static bool IsConcealable(SimConfig cfg, int good)
    {
        bool[] mask = ConcealableMask(cfg);
        return (uint)good < (uint)mask.Length && mask[good];
    }

    /// <summary>The concealable goods as a mask indexed by good id (resolved from discovery.concealableGoods once per call).</summary>
    public static bool[] ConcealableMask(SimConfig cfg)
    {
        if (cfg.Discovery is not { } d || cfg.Goods is not { } goods) return [];
        int max = -1;
        foreach (GoodEntry g in goods.Goods) if (g.Id > max) max = g.Id;
        var mask = new bool[max + 1];
        foreach (string name in d.ConcealableGoods)
        {
            int id = goods.IdOf(name);
            if (id >= 0) mask[id] = true;
        }
        return mask;
    }

    private static bool In(bool[] mask, int good) => (uint)good < (uint)mask.Length && mask[good];

    /// <summary>The u ∈ [0, 1) of settlement <paramref name="settlement"/>'s concealment count.</summary>
    public static double Fraction(ulong seed, int settlement)
    {
        ulong h = SplitMix64.Mix(seed ^ ConcealSalt ^ ((ulong)(uint)settlement << 32));
        return (h >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>The row key of good <paramref name="good"/> at <paramref name="settlement"/>.</summary>
    public static ulong RowKey(ulong seed, int settlement, int good) =>
        SplitMix64.Mix(SplitMix64.Mix(seed ^ ConcealSalt ^ ((ulong)(uint)settlement << 32)) ^ (ulong)(uint)good);

    /// <summary>k = floor(n × s + u), clamped to [0, n].</summary>
    public static int ConcealedCount(int eligible, double share, double u)
    {
        if (eligible <= 0) return 0;
        double x = Math.Floor(eligible * share + u);
        return x <= 0.0 ? 0 : x >= eligible ? eligible : (int)x;
    }

    /// <summary>
    /// THE CLASSIFICATION: the goods of <paramref name="settlement"/>'s CONCEALED deposit rows over
    /// <paramref name="deposits"/> (the stored table, or a scratch roll for a site not yet founded under the id it would get),
    /// ascending by the composite key. Empty when concealment is off.
    /// </summary>
    public static int[] ConcealedGoods(IReadOnlyTable<DepositRow> deposits, ulong seed, SimConfig? cfg, SettlementId settlement)
    {
        if (!Enabled(cfg)) return [];
        bool[] mask = ConcealableMask(cfg!);
        int n = 0;
        for (int i = 0; i < deposits.Count; i++)
        {
            DepositRow d = deposits[i];
            if (d.Settlement.Value == settlement.Value && d.Abundance > 0.0 && In(mask, d.Good.Value)) n++;
        }
        int k = ConcealedCount(n, cfg!.Discovery!.ConcealedShare, Fraction(seed, settlement.Value));
        if (k == 0) return [];
        var keys = new (ulong Key, int Good)[n];
        int j = 0;
        for (int i = 0; i < deposits.Count; i++)
        {
            DepositRow d = deposits[i];
            if (d.Settlement.Value == settlement.Value && d.Abundance > 0.0 && In(mask, d.Good.Value))
                keys[j++] = (RowKey(seed, settlement.Value, d.Good.Value), d.Good.Value);
        }
        return LowestByKey(keys, k);
    }

    /// <summary>THE ORDERING of the classification: the goods of the <paramref name="k"/> entries lowest by the composite key
    /// (Key, Good) — an unsigned integer key with a stable integer tie-break (equal keys: the lower good id first), so the
    /// result is a function of the entries' SET, independent of their order. Sorts <paramref name="keys"/> in place.</summary>
    public static int[] LowestByKey((ulong Key, int Good)[] keys, int k)
    {
        Array.Sort(keys, static (a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Good.CompareTo(b.Good));
        k = Math.Clamp(k, 0, keys.Length);
        var result = new int[k];
        for (int i = 0; i < k; i++) result[i] = keys[i].Good;
        return result;
    }

    /// <summary>Whether the (settlement, good) deposit is CONCEALED (classification only — who has surveyed it is
    /// <see cref="IsHiddenFrom"/>'s question).</summary>
    public static bool IsConcealed(IReadOnlyWorldState world, SimConfig? cfg, SettlementId settlement, GoodId good)
    {
        int[] concealed = ConcealedGoods(world.Deposits, world.Seed, cfg, settlement);
        for (int i = 0; i < concealed.Length; i++) if (concealed[i] == good.Value) return true;
        return false;
    }

    /// <summary>Whether polity <paramref name="polity"/> has prospected <paramref name="settlement"/>.</summary>
    public static bool HasSurveyed(IReadOnlyWorldState world, PolityId polity, SettlementId settlement)
    {
        for (int i = 0; i < world.SettlementSurveys.Count; i++)
        {
            SettlementSurveyRow row = world.SettlementSurveys[i];
            if (row.Polity.Value == polity.Value && row.Settlement.Value == settlement.Value) return true;
        }
        return false;
    }

    /// <summary>Whether the (settlement, good) deposit is hidden from <paramref name="viewer"/>: concealed and not surveyed by
    /// it (null viewer: no polity — hidden whenever concealed).</summary>
    public static bool IsHiddenFrom(IReadOnlyWorldState world, SimConfig? cfg, PolityId? viewer, SettlementId settlement, GoodId good)
    {
        if (!Enabled(cfg)) return false;
        if (viewer is { } p && HasSurveyed(world, p, settlement)) return false;
        return IsConcealed(world, cfg, settlement, good);
    }

    /// <summary>The settlement's CURRENT controller (EmpireQuery.TryGetController), or null for a stateless settlement.</summary>
    public static PolityId? Controller(IReadOnlyWorldState world, SettlementId settlement) =>
        EmpireQuery.TryGetController(world, settlement, out PolityId p) ? p : null;

    /// <summary>
    /// THE MASK one settlement's consumers read through: the goods of the settlement's deposit rows hidden from its current
    /// controller (concealed and not surveyed by it) — null when nothing there is hidden (always with concealment off), so a
    /// caller pays one classification per settlement.
    /// </summary>
    public static int[]? HiddenGoods(IReadOnlyWorldState world, SimConfig? cfg, SettlementId settlement)
    {
        if (!Enabled(cfg)) return null;
        if (Controller(world, settlement) is { } p && HasSurveyed(world, p, settlement)) return null;
        int[] concealed = ConcealedGoods(world.Deposits, world.Seed, cfg, settlement);
        return concealed.Length == 0 ? null : concealed;
    }

    /// <summary>Whether <paramref name="good"/> is in a <see cref="HiddenGoods"/> mask.</summary>
    public static bool Contains(int[]? hidden, int good)
    {
        if (hidden is null) return false;
        for (int i = 0; i < hidden.Length; i++) if (hidden[i] == good) return true;
        return false;
    }

    /// <summary>
    /// THE READ every non-developer consumer makes of one deposit row: its abundance as the settlement's controller knows it
    /// — 0 for an undiscovered concealed row, else <see cref="DepositRow.Abundance"/>.
    /// </summary>
    public static double Abundance(IReadOnlyWorldState world, SimConfig? cfg, in DepositRow row) =>
        Contains(HiddenGoods(world, cfg, row.Settlement), row.Good.Value) ? 0.0 : row.Abundance;

    /// <summary>The known abundance of good <paramref name="good"/> at <paramref name="settlement"/> (the sum of its rows as
    /// <see cref="Abundance(IReadOnlyWorldState, SimConfig?, in DepositRow)"/> reads them).</summary>
    public static double KnownAbundance(IReadOnlyWorldState world, SimConfig? cfg, SettlementId settlement, int good)
    {
        int[]? hidden = HiddenGoods(world, cfg, settlement);
        if (Contains(hidden, good)) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < world.Deposits.Count; i++)
            if (world.Deposits[i].Settlement.Value == settlement.Value && world.Deposits[i].Good.Value == good)
                sum += world.Deposits[i].Abundance;
        return sum;
    }

    /// <summary>The number of ELIGIBLE rows at <paramref name="settlement"/> (the AI's prospect key).</summary>
    public static int EligibleCount(IReadOnlyWorldState world, SimConfig? cfg, SettlementId settlement)
    {
        if (!Enabled(cfg)) return 0;
        bool[] mask = ConcealableMask(cfg!);
        int n = 0;
        for (int i = 0; i < world.Deposits.Count; i++)
        {
            DepositRow d = world.Deposits[i];
            if (d.Settlement.Value == settlement.Value && d.Abundance > 0.0 && In(mask, d.Good.Value)) n++;
        }
        return n;
    }
}
