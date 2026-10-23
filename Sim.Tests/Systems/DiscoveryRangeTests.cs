using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 review item 3 (2026-10-23; ADR-046) — EXPLORE'S SPATIAL UNITS, pinned. The fog's radii (fog.seedRadiusNodes 3,
/// exploreStepNodes 3, exploreMaxRadiusNodes 15) are counted in STEPS OF THE KNOWLEDGE LATTICE (PolityKnowledge.Grid:
/// one node = a 4 × 4 px terrain block, 4 km a pixel = 16 km), measured as a EUCLIDEAN disc over node centres
/// (DiscoveryQuery.InDisc / OrDisc), east-west wrapped on a cylinder world and clipped at the poles. So the seed disc is
/// 48 km, each Explore adds 48 km, and the cap is 15 × 16 = 240 km — straight-line km over ideal ground, not a travel
/// cost. The tests pin the unit, the metric, the wrap, the edges and the maximum, and the UI wording in km.
/// </summary>
public class DiscoveryRangeTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly Lazy<WorldState> Canonical = new(() => WorldFounding.Found(TestConfigs.Worldgen(), Cfg, 42));

    [Fact]
    public void TheLattice_Is256Nodes_Of16Km_OnTheCanonicalMap()
    {
        WorldState w = Canonical.Value;
        (int l, int block, int size, bool wrap) = DiscoveryQuery.Lattice(w);
        Assert.Equal(1024, size);
        Assert.Equal(4, block);
        Assert.Equal(256, l);
        Assert.Equal(4.0, w.Terrain!.KmPerPx);
        Assert.Equal(16.0, DiscoveryQuery.RadiusKm(w, 1));
        Assert.Equal(16.0, LatticeGeometry.NodeStepsKm(1, block, w.Terrain.KmPerPx));
        Assert.True(wrap, "the canonical map is a cylinder");
    }

    [Fact]
    public void TheRadii_InKm_Seed48_Step48_Cap240()
    {
        WorldState w = Canonical.Value;
        FogConfig f = Cfg.Fog!;
        Assert.Equal((3, 3, 15), (f.SeedRadiusNodes, f.ExploreStepNodes, f.ExploreMaxRadiusNodes));
        Assert.Equal(48.0, DiscoveryQuery.RadiusKm(w, f.SeedRadiusNodes));
        Assert.Equal(48.0, DiscoveryQuery.RadiusKm(w, f.ExploreStepNodes));
        Assert.Equal(240.0, DiscoveryQuery.RadiusKm(w, f.ExploreMaxRadiusNodes));
        Assert.Equal("240 km", DiscoveryQuery.RangeText(w, f.ExploreMaxRadiusNodes));
    }

    [Fact]
    public void TheDisc_IsEuclideanInNodeSteps()
    {
        const int L = 256;
        int centre = 100 * L + 100;
        Assert.True(DiscoveryQuery.InDisc(L, false, centre, 15, 100 * L + 115));       // 15 east: on the rim
        Assert.False(DiscoveryQuery.InDisc(L, false, centre, 15, 100 * L + 116));      // 16 east: out
        Assert.True(DiscoveryQuery.InDisc(L, false, centre, 15, 112 * L + 109));       // (9, 12): 15 exactly
        Assert.False(DiscoveryQuery.InDisc(L, false, centre, 15, 111 * L + 111));      // (11, 11): 15.56 — a square would include it
        int count = 0;
        for (int dy = -15; dy <= 15; dy++) for (int dx = -15; dx <= 15; dx++) if (dx * dx + dy * dy <= 225) count++;
        Assert.Equal(count, DiscoveryQuery.DiscOffsets(15).Length);                      // 709 node centres ≈ π·15²
        Assert.Equal(709, count);
    }

    [Fact]
    public void TheDisc_WrapsEastWest_OnACylinder_AndNotOnAFlatMap()
    {
        const int L = 256;
        int nearSeam = 50 * L + 2;                           // x = 2
        int acrossSeam = 50 * L + (L - 10);                  // x = 246: 12 steps west across the seam
        Assert.True(DiscoveryQuery.InDisc(L, true, nearSeam, 15, acrossSeam));
        Assert.False(DiscoveryQuery.InDisc(L, false, nearSeam, 15, acrossSeam));
        var wrapped = new ulong[DiscoveryQuery.WordCount(L)];
        var flat = new ulong[DiscoveryQuery.WordCount(L)];
        DiscoveryQuery.OrDisc(wrapped, L, true, nearSeam, 15);
        DiscoveryQuery.OrDisc(flat, L, false, nearSeam, 15);
        Assert.Equal(709, Bits(wrapped));                    // the whole disc survives the seam
        Assert.True(Bits(flat) < 709);                       // clipped at the western edge
        Assert.True(DiscoveryQuery.Has(wrapped, acrossSeam));
    }

    [Fact]
    public void TheDisc_IsClippedAtThePoles_EvenOnACylinder()
    {
        const int L = 256;
        var polar = new ulong[DiscoveryQuery.WordCount(L)];
        DiscoveryQuery.OrDisc(polar, L, true, 0 * L + 100, 15);       // centred on the northern edge row
        int expected = 0;
        for (int dy = 0; dy <= 15; dy++) for (int dx = -15; dx <= 15; dx++) if (dx * dx + dy * dy <= 225) expected++;
        Assert.Equal(expected, Bits(polar));                          // only the southern half and the centre row
        Assert.False(DiscoveryQuery.InDisc(L, true, 0 * L + 100, 15, 255 * L + 100));   // no wrap north-south
    }

    [Fact]
    public void ExploreGrowsByOneStep_AndStopsAtTheCap_AndTheMenuSaysKm()
    {
        WorldState w = Canonical.Value.Clone();
        FogConfig f = Cfg.Fog!;
        var player = new PolityId(1);
        SettlementId s = Capital(w, player);
        Assert.Equal(6, DiscoveryQuery.NextRadius(w, f, player, s));
        w.Explorations.Add(new ExplorationRow(player, s, 15));
        Assert.Equal(15, DiscoveryQuery.NextRadius(w, f, player, s));        // capped: never 18
        Assert.Equal(ExploreVerdict.AtCap, DiscoveryQuery.CheckExplore(w, Cfg, player, s));
        Assert.Equal("explored to the limit (240 km around it)", DiscoveryQuery.ExploreBlocker(ExploreVerdict.AtCap, Cfg, w));

        WorldState fresh = Canonical.Value;
        var into = new List<ActionDescriptor>();
        AvailableActionsQuery.Discovery(fresh, Cfg, player, ActionQueryContext.None, into);
        ActionDescriptor explore = into.Find(a => a.Order == OrderKind.Explore && a.Targets[0].Id == Capital(fresh, player).Value)!;
        Assert.StartsWith("range 48 km → 96 km, cost ", explore.Detail);
        Assert.DoesNotContain("node", explore.Detail);
    }

    private static SettlementId Capital(IReadOnlyWorldState w, PolityId p)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, p, w.Settlements[i].Id)) return w.Settlements[i].Id;
        throw new InvalidOperationException("no settlement");
    }

    private static int Bits(ulong[] bits)
    {
        int n = 0;
        foreach (ulong b in bits) n += System.Numerics.BitOperations.PopCount(b);
        return n;
    }
}
