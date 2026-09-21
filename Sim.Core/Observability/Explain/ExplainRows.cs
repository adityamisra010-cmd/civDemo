using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability.Explain;

/// <summary>
/// The row finders every explain query shares. Linear scans in table index
/// order (law 5 — never a dictionary), first match wins, −1 for absent: the
/// SAME scan shape the owning systems use to find these rows, so an
/// explanation cannot pick a different row than the system did when a table
/// carries a duplicate key. Each returns an INDEX rather than a value so the
/// caller can cite the row it read (<see cref="Link.SourceIndex"/>).
/// </summary>
internal static class ExplainRows
{
    public static int Housing(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Housing.Count; i++) if (w.Housing[i].Settlement == s) return i;
        return -1;
    }

    public static int Deficit(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s) return i;
        return -1;
    }

    public static int Catchment(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
            if (w.CatchmentSummaries[i].Settlement == s) return i;
        return -1;
    }

    public static int Weather(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.HarvestWeather.Count; i++)
            if (w.HarvestWeather[i].Settlement == s) return i;
        return -1;
    }

    public static int Vitals(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SettlementVitals.Count; i++)
            if (w.SettlementVitals[i].Settlement == s) return i;
        return -1;
    }

    public static int Smoothed(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SmoothedAttractiveness.Count; i++)
            if (w.SmoothedAttractiveness[i].Settlement == s) return i;
        return -1;
    }

    public static int Flow(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.MigrationFlows.Count; i++)
            if (w.MigrationFlows[i].Settlement == s) return i;
        return -1;
    }

    public static int Sectors(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) return i;
        return -1;
    }

    public static int Deposit(IReadOnlyWorldState w, SettlementId s, GoodId good)
    {
        for (int i = 0; i < w.Deposits.Count; i++)
            if (w.Deposits[i].Settlement == s && w.Deposits[i].Good == good) return i;
        return -1;
    }

    public static int Grievance(IReadOnlyWorldState w, SettlementId s, ClassId cls)
    {
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement == s && w.Grievances[i].Class == cls) return i;
        return -1;
    }

    public static int Satisfaction(IReadOnlyWorldState w, SettlementId s, ClassId cls, int needId)
    {
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow r = w.NeedSatisfactions[i];
            if (r.Settlement == s && r.Class == cls && r.NeedId == needId) return i;
        }
        return -1;
    }

    public static bool SettlementPresent(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return true;
        return false;
    }

    /// <summary>Σ BucketRow.Count over the settlement — the plain conserved sum.</summary>
    public static long Population(IReadOnlyWorldState w, SettlementId s)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    /// <summary>Σ BucketRow.Count over one (settlement, class).</summary>
    public static long ClassPopulation(IReadOnlyWorldState w, SettlementId s, ClassId cls)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s && w.Buckets[i].Class == cls) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    /// <summary>The allocation IN FORCE for the step that reads <paramref name="w"/>
    /// as Prev: the settlement's row, or <see cref="State.Sectors.Default"/> when it
    /// was never ordered — the exact fallback Production, Housing and Construction
    /// apply (ProductionSystem.cs:148-153).</summary>
    public static SectorAllocationRow SectorRow(IReadOnlyWorldState w, SettlementId s, out int index)
    {
        index = Sectors(w, s);
        return index >= 0 ? w.SectorAllocations[index] : State.Sectors.Default(s);
    }

    public static string GoodName(SimConfig cfg, GoodId good)
    {
        GoodEntry[]? goods = cfg.Goods?.Goods;
        if (goods is not null)
            for (int i = 0; i < goods.Length; i++) if (goods[i].Id == good.Value) return goods[i].Name;
        return "good " + good.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string ClassName(SimConfig cfg, ClassId cls)
    {
        ClassEntry[]? classes = cfg.Registries?.Classes;
        if (classes is not null)
            for (int i = 0; i < classes.Length; i++) if (classes[i].Id == cls.Value) return classes[i].Name;
        return "class " + cls.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static NeedEntry? Need(SimConfig cfg, int needId)
    {
        NeedEntry[]? needs = cfg.Needs?.Needs;
        if (needs is not null)
            for (int i = 0; i < needs.Length; i++) if (needs[i].Id == needId) return needs[i];
        return null;
    }
}
