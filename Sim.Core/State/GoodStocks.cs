namespace Sim.Core.State;

/// <summary>Lookup helpers for the T3.2 per-(settlement, good) stock table.
/// Linear scans in the fixed row order (law 5) — hot-path indexing waits for
/// a measured need.</summary>
public static class GoodStockIndex
{
    public static int IndexOf(IReadOnlyTable<GoodStockRow> stocks, SettlementId settlement, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == settlement && stocks[i].Good == good) return i;
        return -1;
    }

    public static int IndexOf(Table<GoodStockRow> stocks, SettlementId settlement, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == settlement && stocks[i].Good == good) return i;
        return -1;
    }
}
