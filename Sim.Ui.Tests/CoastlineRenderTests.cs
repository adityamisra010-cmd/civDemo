using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Ui.Art;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// One canonical founded world (seed 42 — the world the director plays) plus
/// ONE real bake of it through the real asset library, shared by every test in
/// <see cref="CoastlineRenderTests"/> (the bake costs seconds; the checks are
/// array math).
/// </summary>
public sealed class CanonicalBakeFixture
{
    public WorldState World { get; }
    public TerrainSet Terrain { get; }
    public AssetLibrary Art { get; }
    public ParchmentBaker.Result Bake { get; }
    public Sim.Core.Chronicle.NameRegistry Names { get; }

    public CanonicalBakeFixture()
    {
        Sim.Core.Systems.SimConfig cfg;
        using (var s = global::Sim.Data.DataFiles.OpenSim())
        using (var n = global::Sim.Data.DataFiles.OpenNeeds())
        using (var g = global::Sim.Data.DataFiles.OpenGoods())
            cfg = Sim.Core.Systems.SimConfigLoader.Load(s, n, g);
        WorldgenConfig wg;
        using (var s = global::Sim.Data.DataFiles.OpenWorldgen()) wg = WorldgenConfigLoader.Load(s);
        World = WorldFounding.Found(wg, cfg, 42);
        Terrain = World.Terrain!;
        Art = AssetLibrary.Load();
        Bake = ParchmentBaker.Bake(Terrain, Art, 42);
        Names = Sim.Core.Chronicle.NameRegistry.Build(
            Sim.Core.Chronicle.ChronicleConfigLoader.Load(global::Sim.Data.DataFiles.OpenChronicle()),
            42, World);
    }

    /// <summary>
    /// The land/water wash-family discriminant, DERIVED FROM THE LOADED ASSETS
    /// rather than invented: mean blue/green ratio per wash tile. MEASURED on
    /// the director's drop — land washes (umber/olive) 0.549–0.741, water
    /// washes (grey-blue) 0.920–1.041; both inks are land-ratio (0.674/0.689),
    /// so coast ink (land side, ≤72%) cannot flip a land texel's family and
    /// engraved-sea ink (water side, ≤16%) leaves water texels ≥ 0.90. The
    /// threshold is the midpoint of the families' extremes; the margin is
    /// asserted so a future art drop that blurs the families fails LOUDLY here
    /// instead of silently green-lighting a meaningless classifier.
    /// </summary>
    public (double Threshold, double MaxLand, double MinWater) FamilyDiscriminant()
    {
        double maxLand = 0.0, minWater = double.MaxValue;
        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
        {
            ArtImage img = Art.Terrain((ParchmentPalette.TerrainClass)c);
            double g = 0, b = 0;
            for (int i = 0; i < img.Width * img.Height; i++)
            {
                g += img.Rgba[i * 4 + 1];
                b += img.Rgba[i * 4 + 2];
            }
            double ratio = b / g;
            if (c < (int)ParchmentPalette.TerrainClass.Shallows) maxLand = Math.Max(maxLand, ratio);
            else minWater = Math.Min(minWater, ratio);
        }
        return ((maxLand + minWater) / 2.0, maxLand, minWater);
    }

    /// <summary>Blue/green ratio of a baked texel.</summary>
    public double TexelRatio(int tx, int ty)
    {
        int off = (ty * Bake.Size + tx) * 4;
        return Bake.Rgba[off + 2] / (double)Math.Max(1, (int)Bake.Rgba[off + 1]);
    }
}

/// <summary>
/// THE COASTLINE CONTRACT (director's coastal-defect ruling on the real-art
/// gate): what the map DISPLAYS at a world position must agree with the sim's
/// cell-level land/water mask there. Two halves, each pinned:
///   * BAKE — every atlas texel is painted from the wash set of its own
///     cell's side of the mask: land cells blend only land washes, water
///     cells only water washes, transition exactly at the coastline
///     (<see cref="ParchmentBaker.IsWaterAt"/>, the hard cell-level gate).
///   * DISPLAY — the world-space draw shows, at world position q, the texel
///     that was baked FOR q (<see cref="ParchmentBaker.DisplayedTexel"/>).
///     THE DEFECT LIVED HERE: the 2048² supersampled atlas drawn at native
///     texel size into a 1024-px world displayed, under a marker at (x, y),
///     the wash painted for the cell at (x/2, y/2) — five coastal settlements
///     (Diles, Toloumou, Naethaehun, Hikiavur, Mothian) "stood in the sea".
/// </summary>
public class CoastlineRenderTests(CanonicalBakeFixture fx) : IClassFixture<CanonicalBakeFixture>
{
    [Fact]
    public void WashFamilies_AreSeparable_OnTheseAssets()
    {
        (double thr, double maxLand, double minWater) = fx.FamilyDiscriminant();
        Assert.True(minWater - maxLand >= 0.05,
            $"land washes reach B/G {maxLand:F3} while water washes start at {minWater:F3} — " +
            "families too close to classify; the coastline tests would be meaningless");
        Assert.InRange(thr, maxLand, minWater);
    }

    /// <summary>
    /// THE PINNED SETTLEMENT TEST (director's ruling, verbatim requirement):
    /// for the canonical founded world, every settlement's site pixel and its
    /// immediate neighbourhood must be painted from the LAND wash set — no
    /// settlement may render in water. The site pixel is the site CELL's whole
    /// displayed footprint (every atlas texel the draw shows inside the cell);
    /// the immediate neighbourhood is the 3×3 cell block around it, where each
    /// sim-LAND cell must likewise display land washes. (Sim-WATER neighbours
    /// are the real sea beside a coastal town — they must stay water, which is
    /// <see cref="NoWaterCell_DisplaysALandWash_BaysAndInletsStayWater"/>'s
    /// half of the contract. A settlement rendered ON land BESIDE water is
    /// correct; IN water is the defect.)
    ///
    /// PROVEN TO FAIL ON THE DEFECTIVE BUILD: with the atlas drawn at native
    /// texel size (the display mapping this test was cut against), 5 of 12
    /// markers — Diles, Toloumou, Naethaehun, Hikiavur, Mothian — sat on
    /// grey-blue sea texels (B/G 0.94–1.00). Toloumou, Naethaehun and
    /// Hikiavur are the three the director named at the gate.
    /// </summary>
    [Fact]
    public void EverySettlement_SitePixelAndLandNeighbourhood_PaintFromTheLandWashSet()
    {
        (double thr, _, _) = fx.FamilyDiscriminant();
        int n = fx.Terrain.Size;
        ReadOnlySpan<double> water = fx.Terrain.Water;
        Assert.Equal(12, fx.World.Settlements.Count);   // the canonical world, not a vacuous sweep

        var offenders = new List<string>();
        for (int i = 0; i < fx.World.Settlements.Count; i++)
        {
            SettlementRow s = fx.World.Settlements[i];
            int cx = s.SiteCell % n, cy = s.SiteCell / n;

            // The marker's own texel — what the director's eye lands on.
            LineGeometry.Vertex p = OverlayMeshes.SettlementPosition(s, n);
            int mx = ParchmentBaker.DisplayedTexel(p.X, n, fx.Bake.Size);
            int my = ParchmentBaker.DisplayedTexel(p.Y, n, fx.Bake.Size);
            double markerRatio = fx.TexelRatio(mx, my);
            if (markerRatio >= thr)
                offenders.Add($"{fx.Names.Name(s.Id.Value)} marker at world ({p.X:F1},{p.Y:F1}) displays " +
                              $"texel ({mx},{my}) B/G {markerRatio:F2} — a WATER wash");

            // Site pixel + neighbourhood: every sim-land cell in the 3×3 block
            // must display land washes across its whole footprint.
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = Math.Clamp(cx + dx, 0, n - 1), gy = Math.Clamp(cy + dy, 0, n - 1);
                    if (water[gy * n + gx] >= 0.5) continue;   // real sea beside the town
                    int tx0 = ParchmentBaker.DisplayedTexel(gx + 1e-9, n, fx.Bake.Size);
                    int tx1 = ParchmentBaker.DisplayedTexel(gx + 1 - 1e-9, n, fx.Bake.Size);
                    int ty0 = ParchmentBaker.DisplayedTexel(gy + 1e-9, n, fx.Bake.Size);
                    int ty1 = ParchmentBaker.DisplayedTexel(gy + 1 - 1e-9, n, fx.Bake.Size);
                    for (int ty = ty0; ty <= ty1; ty++)
                        for (int tx = tx0; tx <= tx1; tx++)
                        {
                            double ratio = fx.TexelRatio(tx, ty);
                            if (ratio >= thr)
                                offenders.Add($"{fx.Names.Name(s.Id.Value)} land cell ({gx},{gy}) displays " +
                                              $"texel ({tx},{ty}) B/G {ratio:F2} — a WATER wash");
                        }
                }
        }
        Assert.True(offenders.Count == 0,
            $"{offenders.Count} settlement texel(s) render in water:\n  " +
            string.Join("\n  ", offenders.Take(8)));
    }

    /// <summary>
    /// THE INVERSE (director's ruling item 4): no water cell renders with a
    /// land wash — bays, lakes and inlets stay water. Swept at DISPLAY level
    /// over every sim-water cell's centre, and jointly the forward direction
    /// over every sim-land cell, so the whole displayed map agrees with the
    /// mask — the strongest form of "the transition occurs exactly at the
    /// coastline". Engraved-sea hairlines are bounded at 16% land-ratio ink,
    /// which leaves water texels' B/G ≥ 0.90 — comfortably above the
    /// discriminant midpoint (~0.83 on the director's drop).
    /// </summary>
    [Fact]
    public void NoWaterCell_DisplaysALandWash_BaysAndInletsStayWater()
    {
        (double thr, _, _) = fx.FamilyDiscriminant();
        int n = fx.Terrain.Size;
        ReadOnlySpan<double> water = fx.Terrain.Water;

        long waterChecked = 0, landChecked = 0, mismatches = 0;
        var offenders = new List<string>();
        for (int cy = 0; cy < n; cy++)
            for (int cx = 0; cx < n; cx++)
            {
                bool simWater = water[cy * n + cx] >= 0.5;
                int tx = ParchmentBaker.DisplayedTexel(cx + 0.5, n, fx.Bake.Size);
                int ty = ParchmentBaker.DisplayedTexel(cy + 0.5, n, fx.Bake.Size);
                bool paintsWater = fx.TexelRatio(tx, ty) >= thr;
                if (simWater) waterChecked++; else landChecked++;
                if (paintsWater != simWater)
                {
                    mismatches++;
                    if (offenders.Count < 8)
                        offenders.Add($"{(simWater ? "WATER" : "LAND")} cell ({cx},{cy}) displays texel " +
                                      $"({tx},{ty}) B/G {fx.TexelRatio(tx, ty):F2} — " +
                                      $"a {(paintsWater ? "WATER" : "LAND")} wash");
                }
            }

        Assert.True(mismatches == 0,
            $"{mismatches} of {waterChecked + landChecked} cells display the wrong wash family, e.g.:\n  " +
            string.Join("\n  ", offenders));
        // Non-vacuity: the canonical world has real sea and real land.
        Assert.True(waterChecked > 100_000, $"only {waterChecked} water cells checked — sweep is vacuous");
        Assert.True(landChecked > 100_000, $"only {landChecked} land cells checked — sweep is vacuous");
    }

    /// <summary>
    /// THE BAKE HALF, structurally: the paint decision is the sim's CELL mask
    /// for ANY supersample factor. The old decision — bilinear-interpolated
    /// mask thresholded at 0.5 — happens to equal the cell mask at ss=2 (texel
    /// centres sit ±0.25 from cell centres, own-cell weight ≥ 9/16), so the
    /// canonical bake never leaked; but at ss=3 corner texels sit ±1/3 from
    /// centres and neighbour weight can reach 5/9 &gt; 1/2, RESHAPING the
    /// coastline. This pin makes the gate independent of the supersample
    /// choice — and proves the distinction has teeth by exhibiting a texel
    /// where the two decisions differ.
    /// </summary>
    [Fact]
    public void WaterDecision_IsTheCellMask_ForAnySupersample()
    {
        // A single land cell in a sea corner — the sharpest coastal geometry.
        const int n = 4;
        var mask = new double[n * n];
        Array.Fill(mask, 1.0);
        mask[1 * n + 1] = 0.0;   // land at (1,1)

        bool bilinearEverDisagreed = false;
        foreach (int ss in new[] { 1, 2, 3, 4, 5 })
            for (int ty = 0; ty < n * ss; ty++)
                for (int tx = 0; tx < n * ss; tx++)
                {
                    double wx = (tx + 0.5) / ss - 0.5, wy = (ty + 0.5) / ss - 0.5;
                    int cellX = Math.Clamp((int)Math.Floor(wx + 0.5), 0, n - 1);
                    int cellY = Math.Clamp((int)Math.Floor(wy + 0.5), 0, n - 1);
                    bool cellMask = mask[cellY * n + cellX] >= 0.5;
                    Assert.Equal(cellMask, ParchmentBaker.IsWaterAt(mask, n, wx, wy));
                    if (Bilinear(mask, n, wx, wy) >= 0.5 != cellMask) bilinearEverDisagreed = true;
                }
        Assert.True(bilinearEverDisagreed,
            "the bilinear-threshold decision never disagreed with the cell mask on this geometry — " +
            "the structural pin is not exercising the difference it exists to forbid");

        // Boundary exactness: the decision flips exactly at the cell edge.
        Assert.False(ParchmentBaker.IsWaterAt(mask, n, 1.499, 1.0));
        Assert.True(ParchmentBaker.IsWaterAt(mask, n, 1.501, 1.0));
    }

    /// <summary>
    /// THE DISPLAY HALF, structurally: for any supersample factor, the texel
    /// the draw shows at world position q must be one that the baker SAMPLED
    /// inside q's cell — so the displayed coastline is the baked coastline.
    /// With the defective native-size draw, the displayed texel at q was
    /// baked for the cell at q/ss: wrong for every ss &gt; 1.
    /// </summary>
    [Fact]
    public void DisplayedTexel_WasBakedForThatWorldPosition()
    {
        foreach (int n in new[] { 16, 1024 })
            foreach (int ss in new[] { 1, 2, 4 })
            {
                int bakeSize = n * ss;
                for (int c = 0; c < n; c += Math.Max(1, n / 16))
                    foreach (double q in new[] { c + 0.01, c + 0.5, c + 0.99 })
                    {
                        int t = ParchmentBaker.DisplayedTexel(q, n, bakeSize);
                        double sampledAt = (t + 0.5) / ss - 0.5;   // Bake's wx for this texel
                        int sampledCell = Math.Clamp((int)Math.Floor(sampledAt + 0.5), 0, n - 1);
                        Assert.True(sampledCell == c,
                            $"world {q:F2} (cell {c}, n={n}, ss={ss}) displays texel {t}, " +
                            $"which was baked for cell {sampledCell} — the substrate is not " +
                            "drawn at world scale");
                    }
            }
    }

    private static double Bilinear(ReadOnlySpan<double> f, int n, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        int x1 = Math.Clamp(x0 + 1, 0, n - 1), y1 = Math.Clamp(y0 + 1, 0, n - 1);
        x0 = Math.Clamp(x0, 0, n - 1); y0 = Math.Clamp(y0, 0, n - 1);
        double p = f[y0 * n + x0] + (f[y0 * n + x1] - f[y0 * n + x0]) * tx;
        double q = f[y1 * n + x0] + (f[y1 * n + x1] - f[y1 * n + x0]) * tx;
        return p + (q - p) * ty;
    }
}
