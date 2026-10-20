using Sim.Core.Pathing;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// P4 (M5 map workstream): <see cref="WorldProjection.Build"/> no longer builds a <see cref="TraversalLattice"/> each
/// turn just to learn its geometry (a full pass over every terrain cell — 1024² at the canonical size); it takes the
/// geometry by arithmetic, <see cref="WorldProjection.LatticeGrid"/>. These tests pin that the map is unchanged:
/// over three seeds at the canonical size and several turns, a world whose size is not a multiple of the stride, and a
/// world with built routes of every class, the geometry equals the built lattice's, and the projection built on the
/// arithmetic geometry draws byte-identical SVG and an identical <see cref="LensFrame"/> to the projection built on
/// the built lattice's geometry (the pre-P4 path), at the fit, Regional, Settlement and maximum zooms.
/// Cross-commit evidence (the same digests computed on 82bde01 before the change and after it) is in the commit
/// message and docs/m5-hardening-measurements.md.
/// </summary>
public class WorldProjectionStrideTests(StrideWorldsFixture fx) : IClassFixture<StrideWorldsFixture>
{
    private static TraversalLattice Lattice(StrideWorldsFixture.Case c) =>
        TraversalLattice.Build(c.World.Terrain!, c.Config.Transport.RiverCostFactor);

    [Fact]
    public void TheArithmeticGrid_IsTheBuiltLatticesGeometry_OnEveryWorld()
    {
        Assert.NotEmpty(fx.Cases);
        foreach (StrideWorldsFixture.Case c in fx.Cases)
        {
            int size = c.World.Terrain!.Size;
            TraversalLattice lattice = Lattice(c);
            // The pre-P4 projection: lsize = lattice.Size, stride = size / lattice.Size.
            Assert.Equal((lattice.Size, size / lattice.Size), WorldProjection.LatticeGrid(size));
            // The lattice every system builds uses the stride the lens assumes (TraversalLattice.Build's default).
            Assert.Equal(WorldProjection.LatticeStride, lattice.Stride);
        }
    }

    [Fact]
    public void TheProjection_OnTheArithmeticGrid_DrawsTheSameMap_AsOnTheBuiltLattice()
    {
        var bands = new SortedSet<WorldZoom>();
        int territory = 0, roads = 0, routes = 0, covered = 0, sizes = 0, lastSize = -1;
        foreach (StrideWorldsFixture.Case c in fx.Cases)
        {
            int size = c.World.Terrain!.Size;
            TraversalLattice lattice = Lattice(c);
            WorldProjection after = WorldProjection.Build(c.World, c.Config, c.Name, UiPlayer.Empire);
            WorldProjection before = WorldProjection.Build(c.World, c.Config, c.Name, UiPlayer.Empire,
                grid: (lattice.Size, size / lattice.Size));
            Assert.Equal(ProjectionDigest.Of(before), ProjectionDigest.Of(after));
            foreach (ProjectionDigest.View v in ProjectionDigest.Views)
            {
                (string svgBefore, string frameBefore) = ProjectionDigest.Paint(before, v);
                (string svgAfter, string frameAfter) = ProjectionDigest.Paint(after, v);
                Assert.True(svgBefore == svgAfter, c.Label + " " + v.Name + ": the SVG bytes differ");
                Assert.Equal(frameBefore, frameAfter);
                bands.Add(Enum.Parse<WorldZoom>(frameAfter[(frameAfter.IndexOf(' ') + 1)..frameAfter.IndexOf('\n')]));
            }
            territory += after.Territory.Count;
            roads += after.Roads.Count;
            routes += after.Routes.Count;
            foreach (RoadSegment r in after.Roads) if (r.CoveredBy >= 0) covered++;
            if (size != lastSize) { sizes++; lastSize = size; }
        }
        // Not vacuous: every stride-dependent output is present somewhere — territory blocks, dirt-path steps, built
        // routes and path steps inside a route's corridor (corridor = stride / 2) — at every zoom band, on more than
        // one terrain size.
        Assert.Equal([WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement], bands);
        Assert.True(territory > 0 && roads > 0 && routes > 0 && covered > 0,
            $"territory {territory}, roads {roads}, routes {routes}, covered {covered}");
        Assert.True(sizes >= 3, "terrain sizes covered: " + sizes);
    }

    /// <summary>The control: the comparison above can see a geometry change. One more px of stride moves every
    /// territory block and dirt-path step, and the map's bytes with them.</summary>
    [Fact]
    public void Control_ADifferentGeometry_DrawsADifferentMap()
    {
        StrideWorldsFixture.Case c = fx.Cases[1];   // seed 42, turn 1: territory present
        int size = c.World.Terrain!.Size;
        (int lsize, int stride) = WorldProjection.LatticeGrid(size);
        WorldProjection right = WorldProjection.Build(c.World, c.Config, c.Name, UiPlayer.Empire);
        WorldProjection wrong = WorldProjection.Build(c.World, c.Config, c.Name, UiPlayer.Empire, grid: (lsize, stride + 1));
        Assert.NotEmpty(right.Territory);
        Assert.NotEqual(ProjectionDigest.Of(right), ProjectionDigest.Of(wrong));
        Assert.NotEqual(ProjectionDigest.Paint(right, ProjectionDigest.Views[0]).SvgSha, ProjectionDigest.Paint(wrong, ProjectionDigest.Views[0]).SvgSha);
    }
}
