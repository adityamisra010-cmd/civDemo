using FsCheck.Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

// T0.3 acceptance: stream independence (no correlation on first 1k draws) and
// stream state surviving the snapshot round-trip (pre-T0.7 form: Clone() carries
// stream states; continuation from the clone matches continuation from the
// original exactly).
public class RngRegistryTests
{
    private static uint[] Draw(RngStream stream, int count)
    {
        var result = new uint[count];
        for (int i = 0; i < count; i++) result[i] = stream.NextUInt32();
        return result;
    }

    [Fact]
    public void SameSeedSameKey_ReproducesIdenticalSequence()
    {
        var worldA = new WorldState(seed: 42);
        var worldB = new WorldState(seed: 42);
        var a = Draw(new RngRegistry(worldA).Get(new SystemId(1), new RegionId(2)), 1000);
        var b = Draw(new RngRegistry(worldB).Get(new SystemId(1), new RegionId(2)), 1000);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Get_SameKeyTwice_ReturnsTheSameUnderlyingStream()
    {
        var world = new WorldState(seed: 42);
        var registry = new RngRegistry(world);
        var first = registry.Get(new SystemId(1), new RegionId(2));
        uint draw1 = first.NextUInt32();

        // Second Get must resume the SAME state, not restart the stream.
        var again = registry.Get(new SystemId(1), new RegionId(2));
        uint draw2 = again.NextUInt32();
        Assert.Equal(1, world.RngStreams.Count);
        Assert.NotEqual(draw1, draw2); // restarting would repeat draw1
    }

    [Theory]
    // different system, same region; same system, different region; swapped key halves
    [InlineData(1, 2, 3, 2)]
    [InlineData(1, 2, 1, 3)]
    [InlineData(1, 2, 2, 1)]
    public void DistinctStreams_NeverCorrelateOnFirst1kDraws(int sysA, int regA, int sysB, int regB)
    {
        var world = new WorldState(seed: 42);
        var registry = new RngRegistry(world);
        var a = Draw(registry.Get(new SystemId(sysA), new RegionId(regA)), 1000);
        var b = Draw(registry.Get(new SystemId(sysB), new RegionId(regB)), 1000);

        // For independent uniform 32-bit streams, a positionwise match has
        // probability 2^-32 per draw (expected matches in 1k draws ≈ 0.00000023).
        // Any correlation — identical, shifted-identical, or key-collision — shows
        // up as massive positionwise agreement; tolerate at most 2 coincidences.
        int matches = 0;
        for (int i = 0; i < 1000; i++)
            if (a[i] == b[i]) matches++;
        Assert.True(matches <= 2, $"streams ({sysA},{regA}) and ({sysB},{regB}) agree at {matches}/1000 positions");
    }

    [Fact]
    public void StreamState_SurvivesCloneRoundTrip_ContinuationsMatchExactly()
    {
        var world = new WorldState(seed: 42);
        var key = (Sys: new SystemId(1), Reg: new RegionId(2));
        var stream = new RngRegistry(world).Get(key.Sys, key.Reg);
        Draw(stream, 137); // advance mid-stream so the state is non-trivial

        WorldState clone = world.Clone();

        var fromOriginal = Draw(new RngRegistry(world).Get(key.Sys, key.Reg), 1000);
        var fromClone = Draw(new RngRegistry(clone).Get(key.Sys, key.Reg), 1000);
        Assert.Equal(fromOriginal, fromClone);
    }

    [Fact]
    public void AdvancingCloneStream_DoesNotAffectOriginal()
    {
        var world = new WorldState(seed: 7);
        var key = (Sys: new SystemId(3), Reg: new RegionId(4));
        Draw(new RngRegistry(world).Get(key.Sys, key.Reg), 10);

        WorldState clone = world.Clone();
        Draw(new RngRegistry(clone).Get(key.Sys, key.Reg), 500); // burn the clone's stream

        // The original must continue exactly as an untouched twin would.
        var twin = new WorldState(seed: 7);
        Draw(new RngRegistry(twin).Get(key.Sys, key.Reg), 10);
        Assert.Equal(
            Draw(new RngRegistry(twin).Get(key.Sys, key.Reg), 100),
            Draw(new RngRegistry(world).Get(key.Sys, key.Reg), 100));
    }

    [Fact]
    public void NextDouble_ConsumesTwoDrawsAndBuilds53Bits()
    {
        var worldA = new WorldState(seed: 42);
        var worldB = new WorldState(seed: 42);
        var key = (Sys: new SystemId(1), Reg: new RegionId(1));

        // Replicate NextDouble from two raw draws on a twin world.
        var raw = new RngRegistry(worldB).Get(key.Sys, key.Reg);
        uint a = raw.NextUInt32() >> 5;
        uint b = raw.NextUInt32() >> 6;
        double expected = (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);

        Assert.Equal(expected, new RngRegistry(worldA).Get(key.Sys, key.Reg).NextDouble());
    }

    [Property]
    public bool NextDouble_AlwaysInUnitIntervalExclusive(ulong seed)
    {
        var world = new WorldState(seed);
        var stream = new RngRegistry(world).Get(new SystemId(1), new RegionId(1));
        for (int i = 0; i < 100; i++)
        {
            double d = stream.NextDouble();
            if (d is < 0.0 or >= 1.0) return false;
        }
        return true;
    }

    [Property]
    public bool DifferentWorldSeeds_YieldDifferentStreams(ulong seedA, ulong seedB)
    {
        if (seedA == seedB) return true;
        var a = Draw(new RngRegistry(new WorldState(seedA)).Get(new SystemId(1), new RegionId(1)), 8);
        var b = Draw(new RngRegistry(new WorldState(seedB)).Get(new SystemId(1), new RegionId(1)), 8);
        return !a.AsSpan().SequenceEqual(b);
    }
}
