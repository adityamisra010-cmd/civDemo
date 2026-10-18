using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Tests.Pathing.Legacy;

namespace Sim.Tests.Pathing;

/// <summary>
/// D-056 (2026-10-18; the Director's mandate §7: "a dedicated O5 early-exit test") — THE O5 STOP ITSELF. Batch 4's O5
/// (D-055) stops <see cref="Pathfinder.DistancesTo(TraversalLattice, IReadOnlyWorldState, int, ReadOnlySpan{int})"/>'s
/// Dijkstra once every passable target is settled. Its RESULTS are the batch-3 full isochrone's bit for bit
/// (LegacyPathfinderDifferentialTests) — so no result-level test could tell a search that stops from one that floods the
/// component, and until now only `sim bench` could. The settled-count overload makes the stop observable:
/// <list type="bullet">
/// <item>targets all near the origin: the search settles FEWER nodes than the full isochrone reaches, and the costs are
///   the isochrone's (the reference) bit for bit — flat, wrapped, and across the seam on the 64-wide SeamWorld lattice;</item>
/// <item>a passable target in another component (behind the wall): the search runs to exhaustion — it settles exactly the
///   origin's whole component, the isochrone's reached set — and reads +∞ there;</item>
/// <item>a duplicate target is awaited once (awaited twice, the count would never reach zero and the search would flood);
///   impassable targets are never awaited; no passable target, or an impassable origin, settles nothing.</item>
/// </list>
/// A mutant that deletes the stop (or awaits duplicates twice) keeps every result and fails here.
/// </summary>
public class DistancesToEarlyExitTests
{
    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    private static (int Reached, double[] Field) Reference(TraversalLattice lattice, IReadOnlyWorldState world, int origin)
    {
        Pathfinder.IsochroneResult full = Pathfinder.Isochrone(lattice, world, origin, double.MaxValue);
        var field = new double[lattice.NodeCount];
        Array.Fill(field, double.PositiveInfinity);
        for (int i = 0; i < full.Reached.Length; i++) field[full.Reached[i]] = full.Costs[i];
        return (full.Reached.Length, field);
    }

    private static void AssertCosts(double[] field, int[] targets, double[] got)
    {
        Assert.Equal(targets.Length, got.Length);
        for (int i = 0; i < targets.Length; i++) Assert.Equal(Bits(field[targets[i]]), Bits(got[i]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NearTargets_StopBeforeTheFlood_WithTheReferenceCosts(bool wrap)
    {
        TraversalLattice lattice = DifferentialLattices.Walled(9, wrap);
        var world = new WorldState(1);
        int origin = 0 * 9 + 0, near = 1 * 9 + 1, nearer = 0 * 9 + 1;              // (0,0); (1,1); (1,0)
        (int reached, double[] field) = Reference(lattice, world, origin);
        int[] targets = [near, nearer];
        double[] got = Pathfinder.DistancesTo(lattice, world, origin, targets, out int settled);
        AssertCosts(field, targets, got);
        Assert.True(settled > 0 && settled < reached, $"settled {settled} of the {reached} the full isochrone reaches");
        Assert.Equal(got, Pathfinder.DistancesTo(lattice, world, origin, targets));          // the 4-argument form is this one
    }

    [Fact]
    public void ATargetBehindTheWall_RunsToExhaustion_SettlingTheWholeComponent_AndReadsInfinity()
    {
        TraversalLattice lattice = DifferentialLattices.Walled(9, wrap: false);       // the wall splits the flat map in two
        var world = new WorldState(1);
        int origin = 0, sameSide = 1, farSide = 8;                                    // x = 8 lies beyond the wall columns 3–4
        (int reached, double[] field) = Reference(lattice, world, origin);
        Assert.True(double.IsPositiveInfinity(field[farSide]) && lattice.IsPassable(farSide));
        int[] targets = [sameSide, farSide];
        double[] got = Pathfinder.DistancesTo(lattice, world, origin, targets, out int settled);
        AssertCosts(field, targets, got);
        Assert.Equal(reached, settled);
    }

    [Fact]
    public void OnTheCylinder_TheSameTargetBehindTheWallIsReachedAcrossTheSeam_AndTheSearchStops()
    {
        TraversalLattice lattice = DifferentialLattices.Walled(9, wrap: true);        // the seam joins the two halves
        var world = new WorldState(1);
        int origin = 0, farSide = 8;                                                  // x = 8 is x = 0's west neighbour
        (int reached, double[] field) = Reference(lattice, world, origin);
        Assert.False(double.IsPositiveInfinity(field[farSide]));
        int[] targets = [farSide];
        double[] got = Pathfinder.DistancesTo(lattice, world, origin, targets, out int settled);
        AssertCosts(field, targets, got);
        Assert.True(settled < reached, $"settled {settled} of {reached}: the seam neighbour must be found without a flood");
    }

    [Fact]
    public void OnTheSeamWorld_AcrossTheSeam_TheStopFiresAndTheCostsAreTheReference()
    {
        DifferentialLattices.Case c = DifferentialLattices.Get("seam-varied");
        int[] passable = DifferentialLattices.PassableNodes(c.Lattice);
        int size = c.Lattice.Size;
        // An origin in the easternmost passable column and the passable node nearest to it across the seam (column 0..2).
        int origin = -1, across = -1;
        foreach (int p in passable) if (c.Lattice.Coords(p).X >= size - 2) { origin = p; break; }
        Assert.True(origin >= 0);
        int oy = c.Lattice.Coords(origin).Y;
        foreach (int p in passable)
        {
            (int x, int y) = c.Lattice.Coords(p);
            if (x <= 1 && Math.Abs(y - oy) <= 1) { across = p; break; }
        }
        Assert.True(across >= 0, "a passable node one step across the seam");
        (int reached, double[] field) = Reference(c.Lattice, c.World, origin);
        int[] targets = [across];
        double[] got = Pathfinder.DistancesTo(c.Lattice, c.World, origin, targets, out int settled);
        AssertCosts(field, targets, got);
        Assert.True(settled * 10 < reached, $"settled {settled} of {reached}");
    }

    [Fact]
    public void ADuplicateIsAwaitedOnce_ImpassableTargetsNever_AndNothingToAwaitSettlesNothing()
    {
        TraversalLattice lattice = DifferentialLattices.Walled(9, wrap: false);
        var world = new WorldState(1);
        int origin = 0, near = 1, wall = 3;                                           // x = 3 is a wall column
        Assert.False(lattice.IsPassable(wall));
        (int reached, double[] field) = Reference(lattice, world, origin);

        int[] dup = [near, near, near];
        double[] got = Pathfinder.DistancesTo(lattice, world, origin, dup, out int settledDup);
        AssertCosts(field, dup, got);
        Pathfinder.DistancesTo(lattice, world, origin, [near], out int settledOne);
        Assert.Equal(settledOne, settledDup);
        Assert.True(settledDup < reached);

        int[] withWall = [wall, near];
        got = Pathfinder.DistancesTo(lattice, world, origin, withWall, out int settledWall);
        AssertCosts(field, withWall, got);
        Assert.Equal(settledOne, settledWall);                                        // the wall is not awaited

        Pathfinder.DistancesTo(lattice, world, origin, [wall], out int none);
        Assert.Equal(0, none);
        Pathfinder.DistancesTo(lattice, world, wall, [near], out int fromWall);
        Assert.Equal(0, fromWall);
        Pathfinder.DistancesTo(lattice, world, origin, [origin], out int self);
        Assert.Equal(1, self);                                                        // the origin is settled first
    }
}
