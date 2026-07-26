using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Price;

/// <summary>Tables owned by <see cref="PriceSystem"/> (T3.4).</summary>
public sealed record PriceTables(Table<PriceRow> Prices, Table<PriceTermRow> Terms);

/// <summary>
/// T3.4 — THE PRICE SOLVER (D-033), the project's #1 flagged risk.
///
/// NO GLOBAL EQUILIBRIUM SOLVE, EVER. This is the fixed part of the mandate and
/// the thing the review exists to check. Concretely, what is banned and is not
/// here: no cross-settlement price coupling (each settlement's prices depend on
/// its own quantities and nothing else); no iteration to convergence (exactly
/// ONE damped step per good per turn, no inner loop, no residual, no tolerance);
/// no simultaneous system over goods (each good is stepped independently — a
/// good's own excess is the only thing that moves it); no matrix, no tâtonnement,
/// no auctioneer. If a future packet needs cross-good substitution it arrives as
/// a mechanism inside the excess-demand terms, never as a solve.
///
/// THE STEP, per settlement, per good, once per turn:
///   excess = consumptionDemand + inputDemand − production − stockRelease
///   scale  = max(production + stockRelease, MarketScaleFloor)
///   raw    = Lambda × p × (excess / scale) × dtYears
///   p'     = clamp(p + clamp(raw, ±MaxRelativeChangePerYear × p × dtYears),
///                  BandMin, BandMax)
///
/// GRAIN IS THE NUMERAIRE and its price is pinned at exactly 1.0 — not
/// "initialised to" 1.0, PINNED: it is written every turn and never stepped,
/// so no accumulation of tiny movements can drift the unit of account. Every
/// other price is denominated in it.
///
/// DT-CORRECTNESS (law 3). excess/scale is a ratio of PER-TURN quantities:
/// demand, production and stock release all scale with dt, so the ratio is
/// dimensionless and dt-invariant, and the single explicit × dtYears carries
/// the entire dt dependence. Both clamps are per-YEAR rates × dtYears for the
/// same reason (see PriceConfig: a literal per-turn constant is what law 3
/// forbids).
///
/// ONE-TURN LAG (§3.2). The quantity signals are read from PREV, so a price
/// responds to last turn's market. That is the frozen architecture, not a
/// shortcut, and it is also what keeps the solver honest: it cannot see the
/// production its own prices will induce, so there is no fixed point to chase.
///
/// DETERMINISM (law 5). Settlements in table row order, goods in registry
/// order, both fixed integer orders over arrays. There is no ordering or argmax
/// over double-valued scores anywhere in this system, so the composite-key
/// tie-break requirement does not attach — stated explicitly rather than
/// omitted.
///
/// EXPLAINABILITY (the glass-box law, shipping for the first time). Every step
/// writes a PriceTermRow decomposing the change into the four economic terms
/// plus the clamp. The terms share the common factor k and differ ONLY in which
/// measured quantity they multiply, which is what makes the decomposition
/// falsifiable: mislabel a term and it responds to the wrong perturbation.
/// The sum check is necessary and is not sufficient (it reconciles by
/// construction); the per-term sensitivity tests are the ones with teeth.
///
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class PriceSystem(SimConfig cfg) : ISimSystem<PriceTables>
{
    public static readonly SystemId WellKnownId = new(12);
    public const string Name = "price";

    private readonly SimConfig _cfg = cfg;

    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentException("PriceSystem requires SimConfig.Goods (goods.json) — grain is the numeraire."));

    public SystemId Id => WellKnownId;

    public void Step(SimContext<PriceTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        GoodsConfig? goods = _cfg.Goods;
        if (goods is null) return;

        Table<PriceRow> prices = ctx.Owned.Prices;
        Table<PriceTermRow> terms = ctx.Owned.Terms;
        terms.Clear(); // one row per (settlement, good) PER TURN — never stale (T3.3 precedent)

        PriceConfig p = _cfg.Price;

        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            for (int g = 0; g < goods.Goods.Length; g++)
            {
                var good = new GoodId(goods.Goods[g].Id);
                int row = FindPrice(prices, settlement, good);
                double prevPrice = row >= 0 ? prices[row].Price : 1.0;

                // GRAIN: pinned at exactly 1.0, every turn, no step, no drift.
                if (good == _grain)
                {
                    Write(prices, ref row, settlement, good, 1.0);
                    terms.Add(new PriceTermRow(settlement, good,
                        PrevPrice: 1.0, Consumption: 0.0, InputDemand: 0.0,
                        Production: 0.0, StockRelease: 0.0, Clamp: 0.0, Delta: 0.0));
                    continue;
                }

                // --- the measured quantities, all from PREV, all per-turn ----
                double consumptionDemand = 0.0, inputDemand = 0.0, production = 0.0, stock = 0.0;
                for (int i = 0; i < prev.GoodStocks.Count; i++)
                {
                    GoodStockRow st = prev.GoodStocks[i];
                    if (st.Settlement != settlement || st.Good != good) continue;
                    consumptionDemand = st.LastConsumptionDemandUnits;
                    inputDemand = st.LastInputDemandUnits;
                    production = st.LastProducedUnits;
                    stock = st.Amount.Value;
                    break;
                }

                double stockRelease = p.StockReleaseRatePerYear * stock * ctx.DtYears;
                double scale = Math.Max(production + stockRelease, p.MarketScaleFloor);

                // Common factor. Each term below multiplies it by ONE measured
                // quantity — the property the sensitivity tests exercise.
                double k = p.Lambda * prevPrice * ctx.DtYears / scale;

                double consumptionTerm = k * consumptionDemand;
                double inputDemandTerm = k * inputDemand;
                double productionTerm = -k * production;
                double stockReleaseTerm = -k * stockRelease;

                double raw = consumptionTerm + inputDemandTerm + productionTerm + stockReleaseTerm;

                // Rail 1: the per-step relative cap (per-year × dt — law 3).
                double maxStep = p.MaxRelativeChangePerYear * prevPrice * ctx.DtYears;
                double stepped = Math.Clamp(raw, -maxStep, maxStep);

                // Rail 2: the absolute band.
                double newPrice = Math.Clamp(prevPrice + stepped, p.BandMin, p.BandMax);

                double delta = newPrice - prevPrice;
                // The clamp term absorbs BOTH rails, so the decomposition closes
                // against the price actually written — never against the price
                // the unclamped equation wanted.
                double clampTerm = delta - raw;

                Write(prices, ref row, settlement, good, newPrice);
                terms.Add(new PriceTermRow(settlement, good,
                    PrevPrice: prevPrice,
                    Consumption: consumptionTerm,
                    InputDemand: inputDemandTerm,
                    Production: productionTerm,
                    StockRelease: stockReleaseTerm,
                    Clamp: clampTerm,
                    Delta: delta));
            }
        }
    }

    /// <summary>Locate a price row by (settlement, good) — never by index
    /// arithmetic, matching the GoodStocks convention (hand-built test worlds
    /// carry sparse rows).</summary>
    private static int FindPrice(Table<PriceRow> prices, SettlementId settlement, GoodId good)
    {
        for (int i = 0; i < prices.Count; i++)
            if (prices[i].Settlement == settlement && prices[i].Good == good) return i;
        return -1;
    }

    private static void Write(
        Table<PriceRow> prices, ref int row, SettlementId settlement, GoodId good, double price)
    {
        if (row >= 0) prices[row] = new PriceRow(settlement, good, price);
        else row = prices.Add(new PriceRow(settlement, good, price));
    }
}
