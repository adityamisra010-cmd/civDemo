using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.TestUtil;

/// <summary>
/// ADR-056 — THE PRE-PACKET PROJECTION of a world stepped with economy packets ON: the same world with every row of the
/// packets' goods removed (GoodStocks, Deposits, Prices, PriceTerms, TradeFlows, ConstructionHolds and the goods' Ledger
/// flow rows). The packets append goods; they change no existing row's layout, so a world in which no packet good was
/// ever made, worked, priced away from its rest value or traded is the pre-packet world PLUS inert rows — and stripping
/// them returns the pre-packet stream byte for byte (the SchemaProjection construction, applied to content).
///
/// A packet row that is NOT inert is BEHAVIOUR, never layout: <see cref="Strip"/> asserts every removed stock row holds
/// nothing and banks nothing, every removed ledger row is zero and no trade or hold row is removed — so a projection can
/// never hide a packet that acted. Prices are the exception that proves the rule: a packet good with no market sits at
/// its rest price 1.0 but its PriceTerm row is written every turn; both are removed and asserted at rest.
/// </summary>
public static class EconomyProjection
{
    /// <summary>A clone of <paramref name="world"/> without the rows of <paramref name="cfg"/>'s goods of the given packets
    /// (all enabled packets when none is named). Throws when a removed row is not inert.</summary>
    public static WorldState Strip(WorldState world, SimConfig cfg, params string[] packets)
    {
        GoodsConfig goods = cfg.Goods!;
        var drop = new bool[256];
        foreach (GoodEntry g in goods.Goods)
            if (g.Packet is { } p && (packets.Length == 0 || Array.IndexOf(packets, p) >= 0)) drop[g.Id] = true;
        WorldState w = world.Clone();

        Keep(w.GoodStocks, r =>
        {
            if (!drop[r.Good.Value]) return true;
            if (r.Amount.Value != 0 || r.ProduceRemainder != 0.0 || r.ConsumeRemainder != 0.0 || r.LastProducedUnits != 0
                || r.LastInputDemandUnits != 0 || r.LastConsumptionDemandUnits != 0 || r.LastConsumptionEatenUnits != 0)
                throw new InvalidOperationException($"good {r.Good.Value} at settlement {r.Settlement.Value} is not inert — behaviour, not layout");
            return false;
        });
        Keep(w.Deposits, r => !drop[r.Good.Value]);
        Keep(w.Prices, r =>
        {
            if (!drop[r.Good.Value]) return true;
            if (r.Price != 1.0) throw new InvalidOperationException($"good {r.Good.Value} priced {r.Price} — a market moved it");
            return false;
        });
        Keep(w.PriceTerms, r =>
        {
            if (!drop[r.Good.Value]) return true;
            if (r.Delta != 0.0) throw new InvalidOperationException($"good {r.Good.Value} price moved by {r.Delta}");
            return false;
        });
        Keep(w.TradeFlows, r => !drop[r.Good.Value] ? true : throw new InvalidOperationException($"good {r.Good.Value} traded"));
        Keep(w.ConstructionHolds, r => !drop[r.Good] ? true : throw new InvalidOperationException($"good {r.Good} held"));
        // ADR-056 §13 (the M7 merge): a re-equipment's escrowed packet good is behaviour (the good was made and spent).
        Keep(w.ReequipEscrow, r => !drop[r.Good] ? true : throw new InvalidOperationException($"good {r.Good} escrowed"));
        Keep(w.LedgerFlows, r =>
        {
            int good = r.Quantity.Value - ConservedQuantityIds.OfGood(new GoodId(0)).Value;
            if (good < 0 || good >= drop.Length || !drop[good]) return true;
            if (r.TotalSourced != 0 || r.TotalSunk != 0)
                throw new InvalidOperationException($"good {good} has ledger flow (reason {r.Reason.Value})");
            return false;
        });
        return w;
    }

    private static readonly Lazy<SimConfig> Shipped = new(TestConfigs.Sim);

    /// <summary><see cref="Strip"/> of every economy packet the SHIPPED goods.json declares — the projection every
    /// pre-packet pin is reached through (SchemaProjection). A world founded without the packets has none of their rows,
    /// so it is returned as a plain clone.</summary>
    public static WorldState StripShipped(WorldState world) => Strip(world, Shipped.Value);

    /// <summary>The hash of <see cref="Strip"/>.</summary>
    public static string HashWithout(WorldState world, SimConfig cfg, params string[] packets) =>
        WorldHash.ComputeHex(Strip(world, cfg, packets));

    private static void Keep<T>(Table<T> table, Func<T, bool> keep) where T : unmanaged
    {
        var rows = new List<T>(table.Count);
        for (int i = 0; i < table.Count; i++) if (keep(table[i])) rows.Add(table[i]);
        table.Clear();
        foreach (T r in rows) table.Add(r);
    }
}
