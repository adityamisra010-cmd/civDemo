using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Trade;

/// <summary>Tables owned by <see cref="TradeArbitrageSystem"/> (T3.6). GoodStocks
/// is the SANCTIONED SHARED STOCK (see SystemCatalog — trade is its third
/// holder); TradeFlows is trade's own per-turn observable.</summary>
public sealed record TradeArbitrageTables(
    Table<GoodStockRow> GoodStocks, Table<TradeFlowRow> TradeFlows);

/// <summary>
/// T3.6 — TRADE &amp; ARBITRAGE (D-034), the fixed mechanism and nothing else:
/// per CONNECTED settlement pair, per good, once per turn — if the price gap
/// exceeds the transport cost, goods flow from the low-price settlement to the
/// high-price settlement, capped at TUNE fraction f of the gap-closing
/// quantity. PAIRWISE ON THE EXISTING NETWORK: distances are the T2.5
/// SettlementDistances the catchment system already publishes over lattice +
/// built paths (one object, three jobs); an unreachable pair (PositiveInfinity)
/// moves exactly zero, forever, by construction. NO GLOBAL SOLVE: no
/// cross-pair coupling, no iteration to convergence, no residual — if this
/// system ever wants a second pass that re-reads its own effects, that is the
/// banned shape and a CR, not an optimization (directed prompt, D-034).
///
/// DECISIONS (docs/t3.6-spec.md, committed before this file):
/// (a) NO TRANSIT LOSS. Transport cost is a DEADBAND on the price gap — the
///     arbitrage threshold — never a consumed quantity. Nothing is sunk here;
///     the only Ledger verb in this file is Transfer, which conserves by
///     construction. A percentage loss without a carrier would be a store
///     drain in disguise (the fence's store-bounding backdoor).
/// (b) INSTANTANEOUS within the turn: reads PREV (prices, stocks — the §3.2
///     one-turn lag), transfers within the step. One Neolithic turn is ten
///     years; no pre-modern haul between adjacent settlements takes a decade.
///     The dt at which this breaks is stated in the spec's forward note.
/// (c) ORDERING NOT LOAD-BEARING, by construction — two sweeps:
///     COMPUTE every pair-good's desired flow purely from PREV (no sweep-1
///     result feeds another sweep-1 computation), then APPLY with a
///     same-factor per-settlement outflow scaling computed from sweep-1
///     TOTALS (never incrementally), integer floors, and the ±1-unit
///     remainders allocated in pair order — the pinned residue, tie-dense
///     tested. Sweep 2 never re-decides anything from its own effects.
///
/// THE UNITS. A price gap is grain-value per unit of the good. Transport cost
/// per unit = BulkPerUnit × pathCost × CostPerBulkCostUnit, where
/// CostPerBulkCostUnit (grain-value per bulk·cost-unit, derived — see
/// TradeConfig) converts bulk-times-distance into the gap's unit. FLOW only
/// when gap STRICTLY exceeds the threshold: at or below it, the deadband is a
/// true dead zone (nothing to arbitrage after paying the carrier).
///
/// THE GAP-CLOSING QUANTITY, from the price step's own linearization (D-033):
/// one unit moved low→high raises next-turn supply signals high-side and
/// lowers them low-side, so to first order the gap shrinks by
/// λ·dt·(p_low/scale_low + p_high/scale_high) per unit moved, where each
/// scale is the price step's own scale = max(production + stockRelease,
/// floor·dt), recomputed here from the SAME Prev fields the price step reads.
/// q* = (gap − threshold) / that sensitivity is the quantity that would close
/// the gap DOWN TO the deadband edge; the realised flow is f·q*, f &lt; 1
/// validated at load — a damped step toward parity, structurally never past
/// it. First-order is the right fidelity: the price step itself moves ~λ·dt
/// per turn and both rails clamp it; chasing the exact exponential here would
/// be a solve.
///
/// GRAIN, stated rather than discovered: the numeraire is PINNED at 1.0
/// everywhere (D-033), so its pairwise gap is structurally zero and grain
/// NEVER moves through this mechanism at M3. That is the R2 observable's
/// mechanical floor — grain redistribution awaits a mechanism that responds
/// to quantities, not prices (deficit-driven relief is a different decision).
///
/// dt-CORRECTNESS (law 3): dt enters through the same λ·dt and stockRelease·dt
/// the price step integrates; the deadband threshold is dt-FREE deliberately
/// (a per-unit cost of distance, not a rate).
///
/// DETERMINISM (law 5): settlements in table row order, pairs by (min row,
/// max row), goods in registry order — fixed integer orders over arrays; no
/// RNG, no dictionary iteration, no double-keyed ordering (the only ordering
/// is over integer indices; remainder allocation follows pair order, the
/// documented integer tie-break).
///
/// STATELESS: config is immutable tuning; TradeFlows is rebuilt every turn.
/// </summary>
public sealed class TradeArbitrageSystem(SimConfig cfg) : ISimSystem<TradeArbitrageTables>
{
    public static readonly SystemId WellKnownId = new(14);
    public const string Name = "trade";

    private readonly SimConfig _cfg = cfg;

    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentException(
            "TradeArbitrageSystem requires SimConfig.Goods (goods.json) — bulk and prices are per-good."));

    public SystemId Id => WellKnownId;

    /// <summary>One sweep-1 desired flow (pure function of Prev + config).</summary>
    private struct DesiredFlow
    {
        public int LowSettlementRow;   // row index into Prev.Settlements (the seller)
        public int HighSettlementRow;  // row index into Prev.Settlements (the buyer)
        public int GoodIndex;          // index into goods registry
        public long Desired;           // whole units, pre-scaling
        public long Scaled;            // whole units after the same-factor cap
    }

    public void Step(SimContext<TradeArbitrageTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        GoodsConfig? goods = _cfg.Goods;
        if (goods is null) return;

        Table<TradeFlowRow> flowsOut = ctx.Owned.TradeFlows;
        flowsOut.Clear(); // per-turn observable — never stale (T3.3 precedent)

        int settlementCount = prev.Settlements.Count;
        if (settlementCount < 2) return;

        PriceConfig p = _cfg.Price;
        TradeConfig t = _cfg.Trade;
        double dt = ctx.DtYears;

        // ---- SWEEP 1: COMPUTE — desired flows purely from Prev ----------
        var desired = new List<DesiredFlow>();
        for (int i = 0; i < settlementCount; i++)
        {
            for (int j = i + 1; j < settlementCount; j++)
            {
                double pathCost = PairCost(prev, prev.Settlements[i].Id, prev.Settlements[j].Id);
                if (double.IsInfinity(pathCost)) continue; // unreachable: zero, forever

                for (int g = 0; g < goods.Goods.Length; g++)
                {
                    GoodEntry entry = goods.Goods[g];
                    var good = new GoodId(entry.Id);
                    // Numeraire short-circuit — NOT a policy: grain's price is
                    // pinned at 1.0 both sides (D-033) so its gap is zero and
                    // the deadband below would skip it anyway; stated here so
                    // the exclusion is structural, not incidental (see header).
                    if (good == _grain) continue;

                    double priceI = PrevPrice(prev, prev.Settlements[i].Id, good);
                    double priceJ = PrevPrice(prev, prev.Settlements[j].Id, good);
                    double gap = Math.Abs(priceI - priceJ);
                    double threshold = entry.BulkPerUnit * pathCost * t.CostPerBulkCostUnit;
                    if (!(gap > threshold)) continue; // the deadband — a true dead zone

                    int lowRow = priceI <= priceJ ? i : j;
                    int highRow = priceI <= priceJ ? j : i;

                    double sensitivity = p.Lambda * dt * (
                        (lowRow == i ? priceI : priceJ) / MarketScale(prev, p, prev.Settlements[lowRow].Id, good, dt)
                        + (highRow == i ? priceI : priceJ) / MarketScale(prev, p, prev.Settlements[highRow].Id, good, dt));
                    if (!(sensitivity > 0.0)) continue;

                    long units = (long)(t.GapClosingFraction * (gap - threshold) / sensitivity);
                    if (units <= 0) continue;

                    desired.Add(new DesiredFlow
                    {
                        LowSettlementRow = lowRow,
                        HighSettlementRow = highRow,
                        GoodIndex = g,
                        Desired = units,
                        Scaled = units,
                    });
                }
            }
        }
        if (desired.Count == 0) return;

        // ---- SWEEP 2: APPLY — same-factor scaling from sweep-1 totals ----
        // For each (seller, good) whose total desired outflow exceeds the PREV
        // stock, every outflow scales by the SAME factor available/total
        // (computed from totals, never incrementally), integer-floored, with
        // the leftover whole units granted one at a time in pair order — the
        // pinned ±1 residue. Prev stock is the cap because sweep 2 may not
        // read its own effects; the Ledger's ClampToAvailable remains the
        // conservation rail underneath either way.
        for (int s = 0; s < settlementCount; s++)
        {
            for (int g = 0; g < goods.Goods.Length; g++)
            {
                long total = 0;
                for (int d = 0; d < desired.Count; d++)
                    if (desired[d].LowSettlementRow == s && desired[d].GoodIndex == g)
                        total += desired[d].Desired;
                if (total == 0) continue;

                long available = PrevStock(prev, prev.Settlements[s].Id, new GoodId(goods.Goods[g].Id));
                if (total <= available) continue;

                double factor = available / (double)total;
                long granted = 0;
                for (int d = 0; d < desired.Count; d++)
                {
                    if (desired[d].LowSettlementRow != s || desired[d].GoodIndex != g) continue;
                    DesiredFlow f = desired[d];
                    f.Scaled = (long)(f.Desired * factor); // integer floor
                    granted += f.Scaled;
                    desired[d] = f;
                }
                long remainder = available - granted;
                for (int d = 0; d < desired.Count && remainder > 0; d++) // pair order
                {
                    if (desired[d].LowSettlementRow != s || desired[d].GoodIndex != g) continue;
                    DesiredFlow f = desired[d];
                    if (f.Scaled >= f.Desired) continue;
                    f.Scaled++;
                    remainder--;
                    desired[d] = f;
                }
            }
        }

        // ---- Execute, in pair order, and publish the realised flows -----
        Table<GoodStockRow> stocks = ctx.Owned.GoodStocks;
        for (int d = 0; d < desired.Count; d++)
        {
            DesiredFlow f = desired[d];
            if (f.Scaled <= 0) continue;
            SettlementId from = prev.Settlements[f.LowSettlementRow].Id;
            SettlementId to = prev.Settlements[f.HighSettlementRow].Id;
            var good = new GoodId(goods.Goods[f.GoodIndex].Id);

            int fromRow = FindStock(stocks, from, good);
            int toRow = FindStock(stocks, to, good);
            if (fromRow < 0 || toRow < 0) continue; // no stock row: nothing to move / nowhere to put it

            long moved = ctx.Ledger.Transfer(
                ref stocks.Ref(fromRow).Amount, ref stocks.Ref(toRow).Amount,
                f.Scaled, OverdrawPolicy.ClampToAvailable);
            if (moved > 0)
                flowsOut.Add(new TradeFlowRow(from, to, good, moved));
        }
    }

    /// <summary>The pair's published travel cost (T2.5 SettlementDistances —
    /// rows exist both directions; either serves). Missing row = not yet
    /// computed = treat as unreachable this turn.</summary>
    private static double PairCost(IReadOnlyWorldState prev, SettlementId a, SettlementId b)
    {
        for (int i = 0; i < prev.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = prev.SettlementDistances[i];
            if (row.From == a && row.To == b) return row.TravelCost;
        }
        return double.PositiveInfinity;
    }

    /// <summary>Prev price, defaulting to 1.0 for a not-yet-priced row — the
    /// same default PriceSystem itself uses.</summary>
    private static double PrevPrice(IReadOnlyWorldState prev, SettlementId s, GoodId good)
    {
        for (int i = 0; i < prev.Prices.Count; i++)
            if (prev.Prices[i].Settlement == s && prev.Prices[i].Good == good)
                return prev.Prices[i].Price;
        return 1.0;
    }

    /// <summary>The price step's own market scale, recomputed from the same
    /// Prev fields it reads: max(production + stockRelease, floor·dt).</summary>
    private static double MarketScale(
        IReadOnlyWorldState prev, PriceConfig p, SettlementId s, GoodId good, double dt)
    {
        double production = 0.0, stock = 0.0;
        for (int i = 0; i < prev.GoodStocks.Count; i++)
        {
            GoodStockRow row = prev.GoodStocks[i];
            if (row.Settlement != s || row.Good != good) continue;
            production = row.LastProducedUnits;
            stock = row.Amount.Value;
            break;
        }
        double stockRelease = p.StockReleaseRatePerYear * stock * dt;
        return Math.Max(production + stockRelease, p.MarketScaleFloorPerYear * dt);
    }

    private static long PrevStock(IReadOnlyWorldState prev, SettlementId s, GoodId good)
    {
        for (int i = 0; i < prev.GoodStocks.Count; i++)
            if (prev.GoodStocks[i].Settlement == s && prev.GoodStocks[i].Good == good)
                return prev.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static int FindStock(Table<GoodStockRow> stocks, SettlementId s, GoodId good)
    {
        for (int i = 0; i < stocks.Count; i++)
            if (stocks[i].Settlement == s && stocks[i].Good == good) return i;
        return -1;
    }
}
