using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// Batch 3 WS3 (W1 fix F3) — THE CYLINDER REACHES EVERY CALLER OF THE SITING PRIMITIVES. The primitives
/// (<see cref="SettlementSiting.BoxSum"/>, <see cref="SettlementSiting.WaterDistanceBfs"/>) carry their own wrap tests;
/// these pin the WIRING — that each caller hands them <see cref="TerrainSet.WrapX"/>: the candidate-score raster
/// (<see cref="SettlementSiting.CandidateScores"/>, so turn-0 siting, <see cref="SettlementSiting.PrepareFrontier"/>,
/// colonisation, AI founding and FoundingQuery), the single-site chooser (<see cref="SettlementSiting.ChooseSite(TerrainSet, SitingConfig)"/>)
/// and the place card's two factor rasters (<see cref="SiteAssessment.Prepare"/>). Measured before this test: each of
/// those five call sites could pass <c>false</c> and every WS3 test still passed (verifier mutants M37, M38, M41, M42, M43).
///
/// THE RIG: the <see cref="SeamWorld"/> strip with the WEST piece barren (fertility 0) and the EAST piece fertile (0.6),
/// one water cell at (Size − 1, y₀) on the east edge, and the cell P = (0, y₀) on the west edge at fertility 0.9. On the
/// cylinder P's west neighbour IS that water cell (water distance 1) and P's fertility window reads three fertile east
/// columns across the seam; on the flat raster the same layers put P 48 px from the nearest water (beyond the 24-px
/// cutoff: no access) with a window cut at x = 0 over barren ground.
/// </summary>
public class SeamSitingWiringTests
{
    private const int Size = SeamWorld.DefaultSize;
    private static readonly int Y0 = Size / 2;
    private static int P => SeamWorld.Cell(0, Y0);

    private static TerrainSet Terrain(bool wrap)
    {
        TerrainSet t = SeamWorld.Terrain(wrap, westFertility: 0.0, eastFertility: 0.6);
        double[] elevation = t.Elevation.ToArray(), water = t.Water.ToArray(), moisture = t.Moisture.ToArray();
        double[] fertility = t.Fertility.ToArray(), movement = t.MovementCost.ToArray();
        int pond = SeamWorld.Cell(Size - 1, Y0);
        elevation[pond] = 0.0; water[pond] = 1.0; moisture[pond] = 1.0; fertility[pond] = 0.0; movement[pond] = 25.0;
        fertility[P] = 0.9;
        return TerrainSet.FromLayers(Size, t.KmPerPx, t.SeaLevel, elevation, water, t.Temperature, moisture, fertility,
            movement, t.Rivers, [], wrap);
    }

    private static SitingConfig Siting => SeamWorld.Worldgen().Siting;

    /// <summary>The window sum written out directly — modular in x on the cylinder (each column once), clamped in y —
    /// independent of BoxSum's integral image (so equal to it up to summation order).</summary>
    private static double BruteWindow(TerrainSet t, int x, int y, int r)
    {
        double sum = 0.0;
        for (int yy = Math.Max(0, y - r); yy <= Math.Min(Size - 1, y + r); yy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = x + dx;
                if (t.WrapX) xx = ((xx % Size) + Size) % Size;
                else if (xx < 0 || xx >= Size) continue;
                sum += t.Fertility[yy * Size + xx];
            }
        return sum;
    }

    [Fact]
    public void TheRig_PutsWaterAndFertileGroundAcrossTheSeamFromP_AndOnlyAcrossIt()
    {
        int r = Siting.FertilityRadiusPx;
        TerrainSet wrapped = Terrain(true), flat = Terrain(false);
        // On the cylinder: 3 east columns × (2r + 1) rows at 0.6, less the pond, plus P's own 0.9.
        Assert.Equal(3 * (2 * r + 1) * 0.6 - 0.6 + 0.9, BruteWindow(wrapped, 0, Y0, r), 12);
        Assert.Equal(0.9, BruteWindow(flat, 0, Y0, r), 12);
        Assert.Equal(1, SettlementSiting.WaterDistanceBfs(wrapped.Water, wrapped.Rivers, Size, wrapX: true)[P]);
        Assert.Equal(SeamWorld.HalfWidth(Size), SettlementSiting.WaterDistanceBfs(flat.Water, flat.Rivers, Size)[P]);
        Assert.True(SeamWorld.HalfWidth(Size) > Siting.WaterAccessCutoffPx, "flat, P must be beyond the water-access cutoff");
    }

    [Fact]
    public void CandidateScores_AtTheSeam_AreTheWrappedWindowTimesTheWrappedWaterAccess()
    {
        int r = Siting.FertilityRadiusPx, cutoff = Siting.WaterAccessCutoffPx;
        foreach (bool wrap in new[] { true, false })
        {
            TerrainSet t = Terrain(wrap);
            double[] scores = SettlementSiting.CandidateScores(t, Siting);
            double[] region = SettlementSiting.BoxSum(t.Fertility, Size, r, wrap);
            int[] waterDistance = SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, Size, wrap);
            for (int x = 0; x <= 2; x++)
            {
                int c = SeamWorld.Cell(x, Y0);
                // The primitive is the written-out window (to 9 places: BoxSum differences prefix sums over the whole map).
                Assert.Equal(BruteWindow(t, x, Y0, r), region[c], 9);
                Assert.Equal(region[c] * SettlementSiting.WaterAccess(waterDistance[c], cutoff), scores[c]);   // bit-exact
            }
        }
        // ...and the topology is visible in the raster: P scores on the cylinder, not on the flat map.
        double wrappedP = SettlementSiting.CandidateScores(Terrain(true), Siting)[P];
        Assert.Equal((3 * (2 * r + 1) * 0.6 - 0.6 + 0.9) * (1.0 - 1.0 / cutoff), wrappedP, 9);
        Assert.Equal(0.0, SettlementSiting.CandidateScores(Terrain(false), Siting)[P]);

        // The frontier (colonisation, AI founding, FoundingQuery) holds the same raster.
        Assert.Equal(wrappedP, SettlementSiting.PrepareFrontier(Terrain(true), Siting, 1.0).Scores[P]);
    }

    [Fact]
    public void ChooseSite_OnTheCylinder_PicksTheSeamCellWhoseWaterIsAcrossTheSeam()
    {
        // P's point fertility (0.9) beats every other land cell's (0.6), and on the cylinder its water is one step west,
        // across the seam: P is the unique argmax. Flat, P has no water access and the pick is elsewhere.
        Assert.Equal(P, SettlementSiting.ChooseSite(Terrain(true), Siting));
        Assert.NotEqual(P, SettlementSiting.ChooseSite(Terrain(false), Siting));
    }

    [Fact]
    public void ThePlaceCardsFactorRasters_ReadAcrossTheSeam_LikeTheSimulation()
    {
        int r = Siting.FertilityRadiusPx;
        SimConfig cfg = TestConfigs.Sim();
        int[] sites = [SeamWorld.Cell(230, Y0 + 30), SeamWorld.Cell(220, Y0 - 30)];
        foreach (bool wrap in new[] { true, false })
        {
            TerrainSet t = Terrain(wrap);
            WorldState w = WorldFounding.FoundOn(t, sites, SeamWorld.Worldgen(), cfg, 42);
            SiteAssessment.Prepared site = SiteAssessment.Prepare(w, cfg, SeamWorld.Worldgen())!;
            Assert.Equal(SettlementSiting.BoxSum(t.Fertility, Size, r, wrap), site.RegionFertility);
            Assert.Equal(SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, Size, wrap), site.WaterDistancePx);
            Assert.Equal(SettlementSiting.CandidateScores(t, Siting), site.Frontier.Scores);
            Assert.Equal(wrap ? 1 : SeamWorld.HalfWidth(Size), site.WaterDistancePx[P]);
            Assert.Equal(BruteWindow(t, 0, Y0, r), site.RegionFertility[P], 9);
        }
    }
}
