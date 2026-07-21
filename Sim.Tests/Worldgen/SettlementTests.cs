using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Tests.Worldgen;

// T1.4 acceptance (siting): same seed → same site (twin); across 10 seeds the
// site is on land, within the water-access cutoff, and in the top fertility
// decile; the argmax tie-break is (score DESC, cell id ASC), pinned by a
// tie-DENSE raster (constitution rule).
public class SettlementTests
{
    private static WorldgenConfig Dev()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
    }

    [Fact]
    public void Siting_SameSeed_SameSite_Twin()
    {
        var cfg = Dev();
        WorldState a = WorldFounding.Found(cfg, seed: 42);
        WorldState b = WorldFounding.Found(cfg, seed: 42);

        Assert.Equal(1, a.Settlements.Count);
        Assert.Equal(a.Settlements[0].SiteCell, b.Settlements[0].SiteCell);
    }

    [Fact]
    public void Siting_AcrossTenSeeds_OnLand_WithinCutoff_TopFertilityDecile()
    {
        var cfg = Dev();
        int cutoff = cfg.Siting.WaterAccessCutoffPx;

        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int site = SettlementSiting.ChooseSite(terrain, cfg.Siting);

            // On land.
            Assert.True(terrain.Water[site] < 0.5, $"seed {seed}: site {site} is water");

            // Within the water-access cutoff: access > 0 ⇔ grid distance < cutoff.
            int[] waterDist = WaterDistance(terrain);
            Assert.True(waterDist[site] < cutoff,
                $"seed {seed}: site water distance {waterDist[site]} >= cutoff {cutoff}");

            // Top fertility decile among LAND cells.
            var landFert = new List<double>();
            for (int i = 0; i < terrain.Fertility.Length; i++)
                if (terrain.Water[i] < 0.5) landFert.Add(terrain.Fertility[i]);
            landFert.Sort();
            double decileThreshold = landFert[(int)(0.90 * (landFert.Count - 1))];
            Assert.True(terrain.Fertility[site] >= decileThreshold,
                $"seed {seed}: site fertility {terrain.Fertility[site]} below top-decile {decileThreshold}");
        }
    }

    [Fact]
    public void Siting_TieDenseRaster_LowestCellIdWins()
    {
        // Constitution rule (T1.4): the argmax over double scores ships a tie-dense
        // test proving the (score, id) tie-break. Uniform fertility with the whole
        // top row water makes every cell of row 1 share the EXACT maximum score
        // (equal fertility × equal access at grid distance 1). The lowest cell id
        // among those tied maxima — cell `size` — must win.
        const int size = 5;
        int cells = size * size;
        var fertility = new double[cells];
        var water = new double[cells];
        for (int i = 0; i < cells; i++) fertility[i] = 0.5;
        for (int x = 0; x < size; x++) water[x] = 1.0; // entire top row (y=0) is water

        int site = SettlementSiting.ChooseSite(fertility, water, size, waterAccessCutoffPx: 10);

        // Row y=1 = cells [size .. 2*size-1] all sit at grid distance 1 from water,
        // uniform fertility ⇒ tied maxima; the lowest id is `size`.
        Assert.Equal(size, site);
    }

    [Fact]
    public void Siting_AllWater_Throws()
    {
        const int size = 3;
        var fertility = new double[size * size];
        var water = new double[size * size];
        for (int i = 0; i < water.Length; i++) water[i] = 1.0;
        Assert.Throws<InvalidOperationException>(
            () => SettlementSiting.ChooseSite(fertility, water, size, waterAccessCutoffPx: 5));
    }

    // Independent 4-neighbor multi-source BFS grid distance from water (mirrors
    // the siting metric) — the test's own reference, not the code under test.
    private static int[] WaterDistance(TerrainSet terrain)
    {
        int size = terrain.Size, cells = size * size;
        var dist = new int[cells];
        var queue = new Queue<int>();
        ReadOnlySpan<double> water = terrain.Water;
        for (int i = 0; i < cells; i++)
        {
            if (water[i] >= 0.5) { dist[i] = 0; queue.Enqueue(i); }
            else dist[i] = int.MaxValue;
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % size, y = i / size, d = dist[i] + 1;
            if (x > 0 && dist[i - 1] == int.MaxValue) { dist[i - 1] = d; queue.Enqueue(i - 1); }
            if (x < size - 1 && dist[i + 1] == int.MaxValue) { dist[i + 1] = d; queue.Enqueue(i + 1); }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue.Enqueue(i - size); }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue.Enqueue(i + size); }
        }
        return dist;
    }
}
