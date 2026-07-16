using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Trade;

/// <summary>Writable handles to TradeSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct TradeTables(Table<GoodsRow> Goods);

/// <summary>
/// M0 toy system: shuffles one toy good between the first two regions via
/// Ledger.Transfer, with amounts from its RNG streams. Exercises BOTH overdraw
/// policies by turn parity: even turns request an unclamped random amount under
/// ClampToAvailable (overdraw clamps, truthfully reported); odd turns use Throw
/// with the amount pre-capped to available (never throws in normal stepping).
/// The initial endowment is itself a Ledger.Flow source (stocks are born at zero —
/// law 1 has no off-ledger genesis). Trade volume is a per-year rate integrated
/// with dtYears (law 3). STATELESS: no instance fields.
/// </summary>
public sealed class TradeSystem : ISimSystem<TradeTables>
{
    public static readonly SystemId WellKnownId = new(3);
    public const string Name = "trade";

    /// <summary>TUNE: initial toy-good endowment per region, base units.</summary>
    public const long InitialEndowment = 1_000_000;

    /// <summary>TUNE: maximum trade volume, base units per year.</summary>
    public const double MaxTradePerYear = 5_000.0;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<TradeTables> ctx)
    {
        IReadOnlyTable<RegionRow> regions = ctx.Prev.Regions;
        if (regions.Count < 2) return; // the toy needs two trading partners

        Table<GoodsRow> goods = ctx.Owned.Goods;

        // First turn: create both stocks at zero and endow them through the Ledger.
        if (goods.Count == 0)
        {
            for (int i = 0; i < 2; i++)
            {
                int idx = goods.Add(new GoodsRow(regions[i].Id, Conserved.Zero));
                ctx.Ledger.Flow(
                    ref goods.Ref(idx).Amount, ConservedQuantityIds.ToyGood,
                    ReasonIds.InitialEndowment, InitialEndowment,
                    FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }

        RngStream rng = ctx.Rng(regions[0].Id); // one stream per route (keyed by first region)
        bool aToB = rng.NextUInt32() % 2 == 0;
        long requested = (long)(rng.NextDouble() * MaxTradePerYear * ctx.DtYears);

        int fromIdx = aToB ? 0 : 1, toIdx = aToB ? 1 : 0;
        ref Conserved from = ref goods.Ref(fromIdx).Amount;
        ref Conserved to = ref goods.Ref(toIdx).Amount;

        if (ctx.Prev.Clock.Turn % 2 == 0)
        {
            // Even turn: unclamped request — overdraw resolves by clamping.
            ctx.Ledger.Transfer(ref from, ref to, requested, OverdrawPolicy.ClampToAvailable);
        }
        else
        {
            // Odd turn: strict policy — cap the request so Throw never fires in
            // normal stepping (a throwing system would abort the turn).
            long capped = requested <= from.Value ? requested : from.Value;
            ctx.Ledger.Transfer(ref from, ref to, capped, OverdrawPolicy.Throw);
        }
    }
}
