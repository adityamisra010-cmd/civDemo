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
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<HouseholdGoodsTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<HouseholdGoodsRow> table = ctx.Owned.HouseholdGoods;
        Table<GoodStockRow> stocks = ctx.Owned.Stocks;
        double dt = ctx.DtYears;
        if (dt <= 0.0) return;
        double wornFraction = _cfg.WornFraction(dt);

        // Settlement-major ascending: an array scan, never a dictionary walk (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // The settlement's requirement, class-weighted over PREV buckets
            // (§3.2's one-turn lag, the same population every other system reads).
            double requirement = 0.0;
            var perClassHeads = new double[_standard.Length];
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

            // ---- 1. WEAR, on what is IN USE ------------------------------
            long held = table[rowIdx].Units.Value;
            if (held > 0)
            {
                double inUse = Math.Min(held, requirement);
                ref HouseholdGoodsRow row = ref table.Ref(rowIdx);
                double exact = inUse * wornFraction + row.WearRemainder;
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
            double deficit = requirement - table[rowIdx].Units.Value;
            if (deficit <= 0.0) continue;

            // Wanted units, class-weighted: each class's own material mix buys
            // that class's own share of the deficit.
            for (int c = 0; c < _standard.Length; c++)
            {
                if (perClassHeads[c] <= 0.0 || _materials[c].Length == 0) continue;
                double classShare = perClassHeads[c] * _standard[c];
                if (classShare <= 0.0) continue;
                double wantedExact = deficit * (classShare / requirement)
                    + (c == FirstClassWithMaterials() ? table[rowIdx].CraftRemainder : 0.0);
                if (wantedExact <= 0.0) continue;

                // How many whole units can the materials actually pay for?
                long wanted = ConservedMath.WholeUnits(wantedExact,
                    $"household-goods craft (settlement {settlement.Value}, class {c})");
                if (c == FirstClassWithMaterials())
                {
                    ref HouseholdGoodsRow rr = ref table.Ref(rowIdx);
                    rr.CraftRemainder = wantedExact - wanted;
                }
                if (wanted <= 0) continue;

                long affordable = wanted;
                for (int j = 0; j < _materials[c].Length; j++)
                {
                    int gi = GoodStockIndex.IndexOf(stocks, settlement, _materials[c][j]);
                    long have = gi >= 0 ? stocks[gi].Amount.Value : 0;
                    // Units this good can pay for at its share of one unit.
                    long canPay = _mix[c][j] <= 0.0
                        ? long.MaxValue
                        : (long)Math.Floor(have / _mix[c][j]);
                    if (canPay < affordable) affordable = canPay;
                }
                if (affordable <= 0) continue;

                // Draw the materials, then source the units. Both through the
                // Ledger; the two quantities are different, so this is never a
                // Transfer.
                long made = affordable;
                for (int j = 0; j < _materials[c].Length; j++)
                {
                    int gi = GoodStockIndex.IndexOf(stocks, settlement, _materials[c][j]);
                    if (gi < 0) continue;
                    double drawExact = made * _mix[c][j];
                    long draw = ConservedMath.WholeUnits(drawExact,
                        $"household-goods materials (settlement {settlement.Value})");
                    if (draw <= 0) continue;
                    ctx.Ledger.Flow(ref stocks.Ref(gi).Amount,
                        ConservedQuantityIds.OfGood(_materials[c][j]),
                        ReasonIds.HouseholdGoodsMaterials, draw, FlowDirection.Sink,
                        OverdrawPolicy.ClampToAvailable);
                }
                ctx.Ledger.Flow(ref table.Ref(rowIdx).Units,
                    ConservedQuantityIds.HouseholdGoods, ReasonIds.HouseholdGoodsCrafted,
                    made, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
    }

    /// <summary>The one class that banks the sub-unit craft residue, so the single
    /// CraftRemainder field has exactly one owner and cannot be double-counted.</summary>
    private int FirstClassWithMaterials()
    {
        for (int c = 0; c < _materials.Length; c++)
            if (_materials[c].Length > 0) return c;
        return -1;
    }

    private static int IndexOf(Table<HouseholdGoodsRow> table, SettlementId settlement)
    {
        for (int i = 0; i < table.Count; i++)
            if (table[i].Settlement == settlement) return i;
        return -1;
    }
}
