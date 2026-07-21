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
        if (!TableEquals(a.PopBands, b.PopBands)) return false;
        if (!TableEquals(a.FoodStores, b.FoodStores)) return false;
        if (!TableEquals(a.ConsumptionDeficits, b.ConsumptionDeficits)) return false;
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
