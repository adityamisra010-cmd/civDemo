using FsCheck.Xunit;
using Sim.Core.State;

namespace Sim.Tests.State;

// T0.2 acceptance: clone round-trip equality, plus buffer isolation in both
// directions (§3.2 — Prev and Next must share no mutable state).
public class WorldStateCloneTests
{
    private static WorldState BuildWorld(params int[] regionValues)
    {
        var world = new WorldState();
        foreach (int v in regionValues)
            world.Regions.Add(new RegionRow(new RegionId(v)));
        return world;
    }

    private static bool StateEquals(WorldState a, WorldState b)
    {
        if (a.Regions.Count != b.Regions.Count) return false;
        for (int i = 0; i < a.Regions.Count; i++)
            if (a.Regions[i] != b.Regions[i]) return false;
        return true;
    }

    [Fact]
    public void CloneRoundTrip_CloneEqualsOriginal()
    {
        var world = BuildWorld(0, 1, 2, 42);
        var clone = world.Clone();
        Assert.True(StateEquals(world, clone));
    }

    [Fact]
    public void CloneOfEmptyWorld_EqualsOriginal()
    {
        var world = new WorldState();
        var clone = world.Clone();
        Assert.True(StateEquals(world, clone));
    }

    [Fact]
    public void MutatingClone_DoesNotAffectOriginal()
    {
        var world = BuildWorld(1, 2);
        var clone = world.Clone();

        clone.Regions[0] = new RegionRow(new RegionId(99));
        clone.Regions.Add(new RegionRow(new RegionId(3)));

        Assert.Equal(2, world.Regions.Count);
        Assert.Equal(new RegionId(1), world.Regions[0].Id);
    }

    [Fact]
    public void MutatingOriginal_DoesNotAffectClone()
    {
        var world = BuildWorld(1, 2);
        var clone = world.Clone();

        world.Regions[1] = new RegionRow(new RegionId(77));
        world.Regions.Add(new RegionRow(new RegionId(4)));

        Assert.Equal(2, clone.Regions.Count);
        Assert.Equal(new RegionId(2), clone.Regions[1].Id);
    }

    [Property]
    public bool CloneRoundTrip_HoldsForArbitraryRegionSets(int[]? regionValues)
    {
        var world = BuildWorld(regionValues ?? []);
        var clone = world.Clone();
        return StateEquals(world, clone);
    }
}
