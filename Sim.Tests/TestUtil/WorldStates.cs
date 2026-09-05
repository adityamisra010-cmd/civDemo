using Sim.Core.State;

namespace Sim.Tests.TestUtil;

// Full-field structural equality over WorldState. EXTEND THIS when WorldState
// grows — the clone/determinism tests rely on it covering every field (it is the
// pre-T0.7 stand-in for the canonical world hash).
public static class WorldStates
{
    public static bool StateEquals(WorldState a, WorldState b)
    {
        if (a.Seed != b.Seed) return false;
        if (a.Clock != b.Clock) return false;
        // Terrain (ADR-008): immutable, so content-hash equality is state equality.
        if ((a.Terrain is null) != (b.Terrain is null)) return false;
        if (a.Terrain is not null && b.Terrain is not null
            && !a.Terrain.ContentHash.AsSpan().SequenceEqual(b.Terrain.ContentHash)) return false;
        if (!TableEquals(a.Regions, b.Regions)) return false;
        if (!TableEquals(a.RngStreams, b.RngStreams)) return false;
        if (!TableEquals(a.Rainfall, b.Rainfall)) return false;
        if (!TableEquals(a.Biomass, b.Biomass)) return false;
        if (!TableEquals(a.Goods, b.Goods)) return false;
        if (!TableEquals(a.LedgerFlows, b.LedgerFlows)) return false;
        if (!TableEquals(a.NetworkNodes, b.NetworkNodes)) return false;
        if (!TableEquals(a.NetworkEdges, b.NetworkEdges)) return false;
        if (!TableEquals(a.Settlements, b.Settlements)) return false;
        if (!TableEquals(a.NetworkMeta, b.NetworkMeta)) return false;
        if (!TableEquals(a.CatchmentNodes, b.CatchmentNodes)) return false;
        if (!TableEquals(a.CatchmentSummaries, b.CatchmentSummaries)) return false;
        if (!TableEquals(a.Buckets, b.Buckets)) return false;
        if (!TableEquals(a.GoodStocks, b.GoodStocks)) return false;
        if (!TableEquals(a.Deposits, b.Deposits)) return false;
        if (!TableEquals(a.ConsumptionDeficits, b.ConsumptionDeficits)) return false;
        if (!TableEquals(a.SectorAllocations, b.SectorAllocations)) return false;
        if (!TableEquals(a.PathProgress, b.PathProgress)) return false;
        if (!TableEquals(a.Variables, b.Variables)) return false;
        if (!TableEquals(a.ClassStates, b.ClassStates)) return false;
        if (!TableEquals(a.SettlementDistances, b.SettlementDistances)) return false;
        if (!TableEquals(a.MigrationFlows, b.MigrationFlows)) return false;
        if (!TableEquals(a.SettlementVitals, b.SettlementVitals)) return false;
        if (!TableEquals(a.NeedSatisfactions, b.NeedSatisfactions)) return false;
        if (!TableEquals(a.Grievances, b.Grievances)) return false;
        if (!TableEquals(a.SmoothedAttractiveness, b.SmoothedAttractiveness)) return false;
        // T3.4c: the three tables this helper was BLIND to. Prices and PriceTerms
        // are T3.4's and PriceTerms is T3.4's too — that packet is ACCEPTED AND
        // MERGED, so this gap was never confined to T3.4b. Every
        // Assert.True(StateEquals(...)) was vacuous for all three; the hash
        // asserts standing beside them are what has actually been carrying those
        // tests. The header above says "EXTEND THIS when WorldState grows" and
        // three consecutive packets did not.
        if (!TableEquals(a.Prices, b.Prices)) return false;
        if (!TableEquals(a.PriceTerms, b.PriceTerms)) return false;
        if (!TableEquals(a.HarvestWeather, b.HarvestWeather)) return false;
        if (!TableEquals(a.TradeFlows, b.TradeFlows)) return false;
        if (!TableEquals(a.Housing, b.Housing)) return false;
        if (!TableEquals(a.Claims, b.Claims)) return false;
        if (!TableEquals(a.Controls, b.Controls)) return false;
        if (!TableEquals(a.Recognitions, b.Recognitions)) return false;
        // T4.8: Notables. Added WITH the table rather than after it, because the
        // comment above records three consecutive packets that did not — and an
        // independent review caught this packet about to be the fourth.
        if (!TableEquals(a.Notables, b.Notables)) return false;
        // M4 (D-042): Polities and Capitals. Added WITH the tables, for the same
        // reason T4.8 gave — a comparer that silently skips a table makes every
        // StateEquals assertion about it vacuous.
        if (!TableEquals(a.Polities, b.Polities)) return false;
        if (!TableEquals(a.Capitals, b.Capitals)) return false;
        // M4-D: the construction queue and structures, added WITH the tables —
        // a comparer that skips a table makes every assertion about it vacuous.
        if (!TableEquals(a.ConstructionQueue, b.ConstructionQueue)) return false;
        if (!TableEquals(a.Structures, b.Structures)) return false;
        // M5: tax policy, added WITH the table — the standing reason above.
        if (!TableEquals(a.TaxPolicies, b.TaxPolicies)) return false;
        return true;
    }

    private static bool TableEquals<T>(Table<T> a, Table<T> b) where T : unmanaged, IEquatable<T>
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }
}
