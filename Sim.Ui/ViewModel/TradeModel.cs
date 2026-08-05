using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9b: the trade panel, PURE view-model (no MonoGame/ImGui types —
/// headless-testable like MarketModel). READ-ONLY: it reads TradeFlows and
/// Prices and emits strings; it creates no orders and adds no sim state.
///
/// THE PANEL'S HARD PROBLEM, stated because it shaped the design: on the
/// canonical world T3.6 measured ZERO flow, and an empty panel is produced
/// equally by "nothing traded" and "the panel is broken". So the model never
/// renders emptiness — it renders a DIAGNOSIS built from rows that already
/// exist, classifying every tradable good into exactly one of three states:
///
///   FLOWED       — TradeFlows carries rows for it; they are listed.
///   GAP ZERO     — every settlement prices it identically, so no pair has a
///                  gap to arbitrage at all. This is T3.6's escalation 2
///                  signature (11 of 13 non-grain goods pinned on COMMON band
///                  edges: stocked goods at the 0.05 floor, zero-stock goods
///                  at the 20.0 ceiling), and the panel prints the shared
///                  price so the pegging is visible rather than inferred.
///   GAP, NO FLOW — a real spread exists and nothing moved, which for the
///                  T3.6 mechanism means the gap did not clear the deadband
///                  (bulk x pathCost x 0.16). This is escalation 1's shape.
///
/// The three states are a CLASSIFICATION of data already present, not an
/// explanation engine: the deadband itself is not recomputed here (it needs
/// path costs and trade tuning, which is sim work this display packet does not
/// do). Both escalations are M4 material; the panel's job is to make the
/// measured state legible, never to fix it.
///
/// GRAIN is excluded and labelled: D-034 makes the numeraire non-tradable by
/// construction, so counting it as a silent good would be a fabricated defect.
/// </summary>
public enum TradeState
{
    Flowed,
    GapZero,
    GapNoFlow,
    Numeraire,
}

/// <summary>One tradable good's state this turn, with the numbers behind it.</summary>
public sealed record TradeGoodRow(
    int GoodId, string Name, TradeState State,
    double? MinPrice, double? MaxPrice, long TotalQuantity)
{
    /// <summary>The spread across settlements — the quantity arbitrage acts
    /// on. Null when fewer than two settlements have published a price.</summary>
    public double? Gap => MinPrice is { } lo && MaxPrice is { } hi ? hi - lo : null;

    public string Line => State switch
    {
        TradeState.Numeraire => string.Create(CultureInfo.InvariantCulture,
            $"{Name,-11} numeraire — never traded (D-034)"),
        TradeState.Flowed => string.Create(CultureInfo.InvariantCulture,
            $"{Name,-11} moved {TotalQuantity,7}  spread {Gap ?? 0.0:F4}"),
        TradeState.GapZero => string.Create(CultureInfo.InvariantCulture,
            $"{Name,-11} no spread — every market at {MinPrice ?? 0.0:F4}"),
        TradeState.GapNoFlow => string.Create(CultureInfo.InvariantCulture,
            $"{Name,-11} spread {Gap ?? 0.0:F4} — under the deadband, nothing moved"),
        _ => $"{Name,-11} unknown",
    };
}

/// <summary>One actual movement, listed when anything moves.</summary>
public sealed record TradeFlowLine(int From, int To, string Good, long Quantity)
{
    public string Line => string.Create(CultureInfo.InvariantCulture,
        $"  settlement {From} -> {To}  {Good,-11} {Quantity,7}");
}

public static class TradeModel
{
    /// <summary>
    /// Every tradable good's state, registry order. Prices are read across ALL
    /// settlements (trade is a world-level pairwise mechanism, unlike the
    /// per-settlement market panel).
    /// </summary>
    public static IReadOnlyList<TradeGoodRow> Rows(IReadOnlyWorldState world, GoodsConfig goods)
    {
        var rows = new List<TradeGoodRow>(goods.Goods.Length);
        foreach (GoodEntry entry in goods.Goods)
        {
            var good = new GoodId(entry.Id);

            long moved = 0;
            for (int i = 0; i < world.TradeFlows.Count; i++)
                if (world.TradeFlows[i].Good == good) moved += world.TradeFlows[i].Quantity;

            double? lo = null, hi = null;
            for (int i = 0; i < world.Prices.Count; i++)
            {
                if (world.Prices[i].Good != good) continue;
                double p = world.Prices[i].Price;
                if (lo is null || p < lo) lo = p;
                if (hi is null || p > hi) hi = p;
            }

            TradeState state;
            if (entry.Numeraire) state = TradeState.Numeraire;
            else if (moved > 0) state = TradeState.Flowed;
            else if (lo is { } l && hi is { } h && h - l > 0.0) state = TradeState.GapNoFlow;
            else state = TradeState.GapZero;

            rows.Add(new TradeGoodRow(entry.Id, entry.Name, state, lo, hi, moved));
        }
        return rows;
    }

    /// <summary>The individual movements, in table order (deterministic —
    /// TradeFlows is written in pair order by the trade system).</summary>
    public static IReadOnlyList<TradeFlowLine> Flows(IReadOnlyWorldState world, GoodsConfig goods)
    {
        var lines = new List<TradeFlowLine>(world.TradeFlows.Count);
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow row = world.TradeFlows[i];
            lines.Add(new TradeFlowLine(
                row.From.Value, row.To.Value, goods.ById(row.Good.Value).Name, row.Quantity));
        }
        return lines;
    }

    /// <summary>
    /// The headline. "No trade" is rendered as a DELIBERATE, counted state
    /// with its reasons — never as an empty panel a reader could mistake for a
    /// broken one. When trade does move, the same line reports how much.
    /// </summary>
    public static string SummaryLine(IReadOnlyList<TradeGoodRow> rows)
    {
        int flowed = 0, gapZero = 0, gapNoFlow = 0;
        long total = 0;
        foreach (TradeGoodRow r in rows)
        {
            switch (r.State)
            {
                case TradeState.Flowed: flowed++; total += r.TotalQuantity; break;
                case TradeState.GapZero: gapZero++; break;
                case TradeState.GapNoFlow: gapNoFlow++; break;
            }
        }
        if (flowed > 0)
            return string.Create(CultureInfo.InvariantCulture,
                $"{flowed} good(s) moved, {total} units total");
        return string.Create(CultureInfo.InvariantCulture,
            $"no trade this turn — {gapZero} good(s) with no spread, {gapNoFlow} under the deadband");
    }
}
