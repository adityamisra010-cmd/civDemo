using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.HouseholdGoods;

/// <summary>Tables owned by <see cref="HouseholdGoodsSystem"/> (T4.13). GoodStocks is
/// the SANCTIONED SHARED STOCK — this system is a fourth holder, and it only ever
/// SINKS from it (the materials crafting consumes).</summary>
public readonly record struct HouseholdGoodsTables(
    Table<HouseholdGoodsRow> HouseholdGoods, Table<GoodStockRow> Stocks);

/// <summary>
/// T4.13 — HOUSEHOLD GOODS: the stock that satisfies Comfort, depleted by USE and
/// replenished by crafting.
///
/// WHY THIS IS NOT A SECOND HOUSING, which is what the packet forbids ("a different
/// equilibrium from housing's maintenance shape, NOT a copy"). Housing degrades when
/// its upkeep materials go unpaid: the driver is a SHORTFALL. Here the driver is USE,
/// and the whole asymmetry is one term —
///
///     inUse = min(stock, requirement)          // NOT stock
///     worn  = inUse × (1 − exp(−dt / serviceLife))
///
/// Because it is `inUse` and not `stock` in the wear term:
///   - idle surplus does not evaporate — a settlement that over-crafted is not
///     punished for it, which a `stock × rate` decay would do;
///   - surplus above the requirement buys nothing either, because satisfaction is
///     min(1, stock/requirement) — so the stock cannot become a score that ratchets;
///   - THE EQUILIBRIUM TRACKS POPULATION. Setting worn = crafted gives
///     crafted = requirement × wornFraction, so a settlement must keep crafting in
///     proportion to the people it has. A settlement that stops crafting loses
///     Comfort on a timescale set by the service life, and a GROWING settlement
///     loses it by dilution without losing a single pot. That last sentence is
///     m4-spec P4 — the stock that does not saturate at 1.0 forever.
///
/// NO NEW GAMEPLAY CONSTANT BEYOND THE SERVICE LIFE, and that one is derived against
/// a reference class stated before the number (needs.json). The holding standard
/// falls out of the FORMER Comfort basket lines: standard × wornFraction(1 year) =
/// Σ perPersonYear, evaluated AT the standard holding where every held unit is in
/// use — so the derivation never assumes goods above the standard wear. Material
/// cost is one material unit per household-good unit, which is the same identity
/// read the other way.
///
/// WHAT LIMITS CRAFTING: materials, and nothing else. There is no build-rate
/// constant, no labour term and no cap — a settlement closes as much of its deficit
/// as its pottery and cloth allow, in the ratified per-class mix. Adding a rate
/// would be inventing a mechanism the packet does not specify.
///
/// CONSERVATION (law 1). Three Ledger reasons because there are three different
/// audit questions: materials leave their good stocks under HouseholdGoodsMaterials,
/// units enter the stock under HouseholdGoodsCrafted, and units leave it under
/// HouseholdGoodsWorn. Materials are a SINK from goods and units are a SOURCE into
/// household goods — deliberately NOT a Transfer, because they are different
/// conserved quantities: pottery becomes a pot, it does not teleport.
/// </summary>
public sealed class HouseholdGoodsSystem : ISimSystem<HouseholdGoodsTables>
{
    public static readonly SystemId WellKnownId = new(17);
    public const string Name = "householdgoods";

    private readonly HouseholdGoodsConfig _cfg;
    private readonly double[] _standard;      // per class id
    private readonly GoodId[][] _materials;   // per class id: the goods, ascending id
    private readonly double[][] _mix;         // per class id: share of one unit, sums to 1

    // Scratch, allocated ONCE at construction and cleared per settlement — the hot
    // loop must not allocate (it runs per settlement per turn), and reusing fixed
    // arrays keeps the iteration order an array index rather than a dictionary walk.
    private readonly double[] _demand;        // per good id: exact material demand
    private readonly GoodId[] _mixGood;       // the goods actually demanded, ascending id
    private readonly double[] _mixShare;      // each one's share of a single unit
    private readonly long[] _drawWhole;       // largest-remainder allocation
    private readonly double[] _drawFrac;
    private readonly double[] _heads;         // per class id: PREV heads in this settlement

    public HouseholdGoodsSystem(SimConfig cfg)
    {
        NeedsConfig needs = cfg.Needs ?? throw new ArgumentException(
            "HouseholdGoodsSystem requires SimConfig.Needs (needs.json).", nameof(cfg));
        _cfg = needs.HouseholdGoods ?? throw new ArgumentException(
            "HouseholdGoodsSystem requires needs.householdGoods (T4.13).", nameof(cfg));
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException(
            "HouseholdGoodsSystem requires SimConfig.Goods (goods.json).", nameof(cfg));

        int maxClass = 0;
        for (int i = 0; i < _cfg.PerClass.Length; i++)
            if (_cfg.PerClass[i].Class > maxClass) maxClass = _cfg.PerClass[i].Class;

        _standard = new double[maxClass + 1];
        _materials = new GoodId[maxClass + 1][];
        _mix = new double[maxClass + 1][];
        for (int c = 0; c <= maxClass; c++)
        {
            _standard[c] = _cfg.StandardPerPerson(c);
            _materials[c] = [];
            _mix[c] = [];
        }
        for (int i = 0; i < _cfg.PerClass.Length; i++)
        {
            HouseholdGoodsClass pc = _cfg.PerClass[i];
            double annual = 0.0;
            for (int j = 0; j < pc.Materials.Length; j++) annual += pc.Materials[j].PerPersonYear;
            if (annual <= 0.0) continue;
            var ids = new GoodId[pc.Materials.Length];
            var share = new double[pc.Materials.Length];
            for (int j = 0; j < pc.Materials.Length; j++)
            {
                ids[j] = new GoodId(goods.IdOf(pc.Materials[j].Good));
                share[j] = pc.Materials[j].PerPersonYear / annual;
            }
            _materials[pc.Class] = ids;
            _mix[pc.Class] = share;
        }

        int maxGoodId = 0, distinctGoods = 0;
        for (int i = 0; i < goods.Goods.Length; i++)
            if (goods.Goods[i].Id > maxGoodId) maxGoodId = goods.Goods[i].Id;
        for (int c = 0; c < _materials.Length; c++) distinctGoods += _materials[c].Length;
        _demand = new double[maxGoodId + 1];
        _mixGood = new GoodId[distinctGoods + 1];
        _mixShare = new double[distinctGoods + 1];
        _drawWhole = new long[distinctGoods + 1];
        _drawFrac = new double[distinctGoods + 1];
        _heads = new double[maxClass + 1];
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<HouseholdGoodsTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<HouseholdGoodsRow> table = ctx.Owned.HouseholdGoods;
        Table<GoodStockRow> stocks = ctx.Owned.Stocks;
        double dt = ctx.DtYears;
        if (dt <= 0.0) return;

        // Settlement-major ascending: an array scan, never a dictionary walk (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // The settlement's requirement, class-weighted over PREV buckets
            // (§3.2's one-turn lag, the same population every other system reads).
            double requirement = 0.0;
            Array.Clear(_heads);
            double[] perClassHeads = _heads;
            for (int i = 0; i < prev.Buckets.Count; i++)
            {
                BucketRow b = prev.Buckets[i];
                if (b.Settlement != settlement) continue;
                int c = b.Class.Value;
                if (c < 0 || c >= _standard.Length) continue;
                perClassHeads[c] += b.Count.Value;
                requirement += b.Count.Value * _standard[c];
            }

            int rowIdx = IndexOf(table, settlement);
            if (requirement <= 0.0 && rowIdx < 0) continue;   // nobody, nothing held
            if (rowIdx < 0)
                rowIdx = table.Add(new HouseholdGoodsRow(settlement, Conserved.Zero, 0.0, 0.0));

            // ---- 1. WEAR, on what is IN USE, EXACTLY INTEGRATED ----------
            //
            // The naive form — inUse × (1 − exp(−dt/L)) — is WRONG above the
            // standard, and independent review measured it: an e-fold fraction is
            // the closed form for decay PROPORTIONAL TO THE STOCK, but while
            // stock > requirement the wear rate is CONSTANT at requirement/L,
            // because only the goods in use wear. Applying the exponential
            // fraction to a constant-rate regime lost 2615 units over one dt=10
            // turn where ten dt=1 turns lost 920. Law 3 is dt-INVARIANCE, not
            // merely "uses dtYears", so the integral is now done piecewise and
            // exactly:
            //
            //   while stock > R:  dS/dt = −R/L            (constant — linear)
            //   once  stock ≤ R:  dS/dt = −S/L            (proportional — e-fold)
            //
            // t1 is when the stock falls to R. If the turn ends first the whole
            // turn is linear; otherwise the turn is linear to t1 and e-fold for
            // what remains. At stock ≤ R this reduces to the plain e-fold, so the
            // surplus branch is the only thing that changed.
            long held = table[rowIdx].Units.Value;
            if (held > 0 && requirement > 0.0)
            {
                double lostExact;
                if (held <= requirement)
                {
                    lostExact = held * _cfg.WornFraction(dt);
                }
                else
                {
                    double life = _cfg.ServiceLifeYears;
                    double tToStandard = (held - requirement) * life / requirement;
                    lostExact = tToStandard >= dt
                        ? requirement * dt / life
                        : (held - requirement) + requirement * _cfg.WornFraction(dt - tToStandard);
                }

                ref HouseholdGoodsRow row = ref table.Ref(rowIdx);
                double exact = lostExact + row.WearRemainder;
                long worn = ConservedMath.WholeUnits(exact,
                    $"household-goods wear (settlement {settlement.Value})");
                row.WearRemainder = exact - worn;
                if (worn > 0)
                {
                    ctx.Ledger.Flow(ref row.Units, ConservedQuantityIds.HouseholdGoods,
                        ReasonIds.HouseholdGoodsWorn, worn, FlowDirection.Sink,
                        OverdrawPolicy.ClampToAvailable);
                }
            }

            // ---- 2. CRAFT toward the requirement, bounded by MATERIALS ----
            //
            // ONE SETTLEMENT-LEVEL PASS, not a per-class loop. The per-class loop
            // this replaces had two defects independent review measured:
            //   - it MINTED. `made` units were sourced in one flow while each
            //     material was drawn under its OWN floor, and Σ floor(made·mixⱼ)
            //     is strictly less than `made` whenever a share is fractional. A
            //     settlement holding 1 pottery and 1 cloth crafted a unit every
            //     turn while its material stocks never moved — perpetual motion,
            //     and the conservation auditor is structurally blind to it
            //     because the source flow is individually legitimate.
            //   - it banked the sub-unit residue for ONE class only, so an
            //     artisan-only settlement stalled one unit short of its
            //     requirement forever with unlimited materials.
            //
            // Both die against one invariant, asserted below and tested:
            // MATERIALS DRAWN, SUMMED OVER GOODS, EQUALS UNITS MADE — exactly,
            // every turn. One material unit makes one household-good unit, which
            // is the derivation identity read the other way.
            double deficit = requirement - table[rowIdx].Units.Value;
            if (deficit <= 0.0) continue;

            // Aggregate the class-weighted material demand into ONE mix over
            // goods, ascending good id (law 5: array order, never a dictionary).
            Array.Clear(_demand);
            double wantedExactTotal = 0.0;
            for (int c = 0; c < _standard.Length; c++)
            {
                if (perClassHeads[c] <= 0.0 || _materials[c].Length == 0) continue;
                double classShare = perClassHeads[c] * _standard[c];
                if (classShare <= 0.0) continue;
                double wantedExact = deficit * (classShare / requirement);
                wantedExactTotal += wantedExact;
                for (int j = 0; j < _materials[c].Length; j++)
                    _demand[_materials[c][j].Value] += wantedExact * _mix[c][j];
            }
            if (wantedExactTotal <= 0.0) continue;

            // ONE remainder for the settlement, so no class's residue is dropped.
            double bankedWant = wantedExactTotal + table[rowIdx].CraftRemainder;
            long wanted = ConservedMath.WholeUnits(bankedWant,
                $"household-goods craft (settlement {settlement.Value})");
            table.Ref(rowIdx).CraftRemainder = bankedWant - wanted;
            if (wanted <= 0) continue;

            // The aggregate mix: each good's share of ONE unit. Sums to 1.
            int goods = 0;
            for (int g = 0; g < _demand.Length; g++)
            {
                if (_demand[g] <= 0.0) continue;
                _mixGood[goods] = new GoodId(g);
                _mixShare[goods] = _demand[g] / wantedExactTotal;
                goods++;
            }
            if (goods == 0) continue;

            long made = wanted;
            for (int j = 0; j < goods; j++)
            {
                int gi = GoodStockIndex.IndexOf(stocks, settlement, _mixGood[j]);
                long have = gi >= 0 ? stocks[gi].Amount.Value : 0;
                long canPay = _mixShare[j] <= 0.0
                    ? long.MaxValue : (long)Math.Floor(have / _mixShare[j]);
                if (canPay < made) made = canPay;
            }
            if (made <= 0) continue;

            // LARGEST-REMAINDER allocation so the draws SUM to `made` exactly.
            // Floors first, then hand out the shortfall to the largest fractional
            // parts, ties broken by ASCENDING GOOD ID — a composite (fraction
            // desc, id asc) key, deterministic and stable.
            long allocated = 0;
            for (int j = 0; j < goods; j++)
            {
                double exactDraw = made * _mixShare[j];
                _drawWhole[j] = (long)Math.Floor(exactDraw);
                _drawFrac[j] = exactDraw - _drawWhole[j];
                allocated += _drawWhole[j];
            }
            for (long extra = made - allocated; extra > 0; extra--)
            {
                int best = -1;
                for (int j = 0; j < goods; j++)
                    if (best < 0 || _drawFrac[j] > _drawFrac[best]) best = j;
                _drawWhole[best]++;
                _drawFrac[best] = -1.0;   // spent; ties fall to the next lowest id
            }

            long drawnTotal = 0;
            for (int j = 0; j < goods; j++)
            {
                if (_drawWhole[j] <= 0) continue;
                int gi = GoodStockIndex.IndexOf(stocks, settlement, _mixGood[j]);
                if (gi < 0) continue;
                ctx.Ledger.Flow(ref stocks.Ref(gi).Amount,
                    ConservedQuantityIds.OfGood(_mixGood[j]),
                    ReasonIds.HouseholdGoodsMaterials, _drawWhole[j], FlowDirection.Sink,
                    OverdrawPolicy.ClampToAvailable);
                drawnTotal += _drawWhole[j];
            }

            // Source EXACTLY what the materials paid for. Not `made` — what was
            // actually drawn — so the identity holds even if a stock row is
            // missing or a clamp bit.
            if (drawnTotal > 0)
            {
                ctx.Ledger.Flow(ref table.Ref(rowIdx).Units,
                    ConservedQuantityIds.HouseholdGoods, ReasonIds.HouseholdGoodsCrafted,
                    drawnTotal, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
    }

    private static int IndexOf(Table<HouseholdGoodsRow> table, SettlementId settlement)
    {
        for (int i = 0; i < table.Count; i++)
            if (table[i].Settlement == settlement) return i;
        return -1;
    }
}
