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
        if (!TableEquals(a.Regions, b.Regions)) return false;
        if (!TableEquals(a.RngStreams, b.RngStreams)) return false;
        if (!TableEquals(a.Rainfall, b.Rainfall)) return false;
        if (!TableEquals(a.Biomass, b.Biomass)) return false;
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
