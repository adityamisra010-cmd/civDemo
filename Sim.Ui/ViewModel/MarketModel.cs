using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9a item 1: the per-settlement goods-and-price panel, PURE view-model
/// (no MonoGame/ImGui types — headless-testable like HudModel). One row per
/// good in registry order: stock (GoodStocks), price (Prices, grain units)
/// and the direction+size of the last move (PriceTerms.Delta). Grain is
/// LABELLED as the numeraire so a reader never wonders why its price sits at
/// 1.00 forever. READ-ONLY: this model reads existing WorldState tables and
/// emits strings; it neither creates orders nor adds sim state.
/// </summary>
public sealed record MarketGoodRow(
    int GoodId, string Name, bool IsNumeraire,
    long Stock, double? Price, double? Delta)
{
    /// <summary>The panel line. Absent price rows read "not yet measured" —
    /// prices are written by PriceSystem each turn, so before the first turn
    /// resolves NOTHING is published (Prev-lag, §3.2); a fabricated "1.0000"
    /// would claim a measurement that never happened (T2.9 gate precedent).</summary>
    public string Line => IsNumeraire
        ? string.Create(CultureInfo.InvariantCulture,
            $"{Name,-11} stock {Stock,7}  price 1.0000  (numeraire, pinned)")
        : Price is { } p
            ? string.Create(CultureInfo.InvariantCulture,
                $"{Name,-11} stock {Stock,7}  price {p:F4}  chg {Delta ?? 0.0:+0.0000;-0.0000}")
            : string.Create(CultureInfo.InvariantCulture,
                $"{Name,-11} stock {Stock,7}  price not yet measured");
}

/// <summary>
/// The T3.4 PriceTerms decomposition for one (settlement, good), surfaced:
/// which term drove the last move. The glass-box artifact nothing displayed
/// until now — each price change decomposes into five terms that sum exactly
/// to the observed change.
/// </summary>
public sealed record PriceBreakdown(
    string GoodName, double PrevPrice, double NewPrice, double Delta,
    IReadOnlyList<(string Name, double Value)> Terms, string Driver)
{
    /// <summary>The fixed term vocabulary, in PriceTermRow field order.</summary>
    public static readonly string[] TermNames =
        ["consumption", "input demand", "production", "stock release", "clamp"];

    public string HeaderLine => string.Create(CultureInfo.InvariantCulture,
        $"{GoodName}: {PrevPrice:F4} -> {NewPrice:F4}  (chg {Delta:+0.0000;-0.0000;+0.0000})");

    public string DriverLine => string.Create(CultureInfo.InvariantCulture, $"driver: {Driver}");

    public IReadOnlyList<string> TermLines
    {
        get
        {
            var lines = new List<string>(Terms.Count);
            foreach ((string name, double value) in Terms)
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"  {name,-13} {value:+0.0000;-0.0000;+0.0000}"));
            return lines;
        }
    }
}

public static class MarketModel
{
    /// <summary>Builds the goods table for one settlement, registry order.
    /// An id not present in the world yields rows with zero stock and no
    /// price — the panel renders them harmlessly (selection is pure UI state
    /// and never crashes anything, T2.4 doctrine).</summary>
    public static IReadOnlyList<MarketGoodRow> Rows(
        IReadOnlyWorldState world, int settlementId, GoodsConfig goods)
    {
        var settlement = new SettlementId(settlementId);
        var rows = new List<MarketGoodRow>(goods.Goods.Length);
        foreach (GoodEntry entry in goods.Goods)
        {
            var good = new GoodId(entry.Id);
            long stock = 0;
            for (int i = 0; i < world.GoodStocks.Count; i++)
            {
                if (world.GoodStocks[i].Settlement == settlement && world.GoodStocks[i].Good == good)
                { stock = world.GoodStocks[i].Amount.Value; break; }
            }
            double? price = null;
            for (int i = 0; i < world.Prices.Count; i++)
            {
                if (world.Prices[i].Settlement == settlement && world.Prices[i].Good == good)
                { price = world.Prices[i].Price; break; }
            }
            double? delta = null;
            for (int i = 0; i < world.PriceTerms.Count; i++)
            {
                if (world.PriceTerms[i].Settlement == settlement && world.PriceTerms[i].Good == good)
                { delta = world.PriceTerms[i].Delta; break; }
            }
            rows.Add(new MarketGoodRow(entry.Id, entry.Name, entry.Numeraire, stock, price, delta));
        }
        return rows;
    }

    /// <summary>The decomposition for one selected good, or null when no
    /// PriceTerms row exists yet (terms are rebuilt each turn; before the
    /// first turn resolves there is nothing to explain). The DRIVER is the
    /// term of largest magnitude — an argmax over doubles, so the composite
    /// key carries a stable integer tie-break: (|value|, −term index), i.e.
    /// strict-greater comparison walking the fixed term order, earliest term
    /// wins exact ties. All-zero terms name no driver ("none").</summary>
    public static PriceBreakdown? Breakdown(
        IReadOnlyWorldState world, int settlementId, int goodId, GoodsConfig goods)
    {
        var settlement = new SettlementId(settlementId);
        var good = new GoodId(goodId);
        for (int i = 0; i < world.PriceTerms.Count; i++)
        {
            PriceTermRow row = world.PriceTerms[i];
            if (row.Settlement != settlement || row.Good != good) continue;

            double[] values = [row.Consumption, row.InputDemand, row.Production, row.StockRelease, row.Clamp];
            var terms = new List<(string, double)>(values.Length);
            for (int t = 0; t < values.Length; t++)
                terms.Add((PriceBreakdown.TermNames[t], values[t]));

            int driver = -1;
            double best = 0.0;
            for (int t = 0; t < values.Length; t++)
            {
                double magnitude = Math.Abs(values[t]);
                if (magnitude > best) { best = magnitude; driver = t; } // strict >: earliest term keeps ties
            }
            return new PriceBreakdown(
                goods.ById(goodId).Name, row.PrevPrice, row.PrevPrice + row.Delta, row.Delta,
                terms, driver >= 0 ? PriceBreakdown.TermNames[driver] : "none (no movement)");
        }
        return null;
    }
}
