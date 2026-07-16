namespace Sim.Core.Kernel;

/// <summary>
/// SplitMix64 (Steele/Lea/Flood; Vigna's reference constants) — explicit, stable
/// integer mixing for deriving per-stream PCG32 seeds from
/// (worldSeed, SystemId, RegionId) keys. Used because key-to-seed derivation must
/// be deterministic across runs and refactors; GetHashCode is banned as logic
/// input (law 5).
/// </summary>
public static class SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>The stateless splitmix64 finalizer: a bijective 64-bit mix.</summary>
    public static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>One splitmix64 stream step: advance by the golden gamma, then mix.</summary>
    public static ulong Next(ref ulong state)
    {
        state += GoldenGamma;
        return Mix(state);
    }
}
