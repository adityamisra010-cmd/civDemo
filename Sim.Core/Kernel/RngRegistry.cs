using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// Handle to one PCG32 stream whose state lives in a WorldState table row
/// (kernel contract §3.5). Draws mutate the row in place, so randomness travels
/// with the double-buffer clone and, later, snapshots (T0.7).
/// </summary>
public readonly struct RngStream
{
    private readonly Table<RngStreamRow> _streams;
    private readonly int _index;

    internal RngStream(Table<RngStreamRow> streams, int index)
    {
        _streams = streams;
        _index = index;
    }

    /// <summary>One canonical PCG32 draw.</summary>
    public uint NextUInt32()
    {
        ref RngStreamRow row = ref _streams.Ref(_index);
        ulong state = row.State;
        uint result = Pcg32.Next(ref state, row.Inc);
        row.State = state;
        return result;
    }

    /// <summary>
    /// A double in [0, 1) with 53 random bits, built from two 32-bit draws
    /// (§3.5): high 27 bits from the first draw, low 26 bits from the second —
    /// (a · 2²⁶ + b) / 2⁵³.
    /// </summary>
    public double NextDouble()
    {
        uint a = NextUInt32() >> 5;   // 27 bits
        uint b = NextUInt32() >> 6;   // 26 bits
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }
}

/// <summary>
/// One named stream per (system × region), D-007. Streams are created lazily on
/// first Get; creation order is deterministic because systems execute in the fixed
/// pipeline order. Per-stream seeds derive from (worldSeed, systemId, regionId) by
/// explicit splitmix64 mixing — stable across runs and refactors; GetHashCode is
/// never used (law 5).
/// </summary>
public sealed class RngRegistry
{
    private readonly WorldState _world;

    public RngRegistry(WorldState world) => _world = world;

    public RngStream Get(SystemId system, RegionId region)
    {
        Table<RngStreamRow> streams = _world.RngStreams;
        for (int i = 0; i < streams.Count; i++)
        {
            ref RngStreamRow row = ref streams.Ref(i);
            if (row.System == system && row.Region == region)
                return new RngStream(streams, i);
        }

        // Key packs SystemId into the high 32 bits and RegionId into the low 32,
        // so (a, b) and (b, a) are distinct keys by construction.
        ulong key = ((ulong)(uint)system.Value << 32) | (uint)region.Value;
        ulong s = SplitMix64.Mix(_world.Seed) ^ SplitMix64.Mix(key);
        ulong initState = SplitMix64.Next(ref s);
        ulong initSeq = SplitMix64.Next(ref s);

        (ulong state, ulong inc) = Pcg32.Seed(initState, initSeq);
        int index = streams.Add(new RngStreamRow(system, region, state, inc));
        return new RngStream(streams, index);
    }
}
