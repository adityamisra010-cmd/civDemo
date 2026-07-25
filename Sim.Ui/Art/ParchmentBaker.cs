using Sim.Core.Worldgen;

namespace Sim.Ui.Art;

/// <summary>
/// THE SUBSTRATE BAKE (style-bible §4 items 1–4): turns the immutable
/// TerrainSet plus the art library into ONE parchment-atlas texture — the
/// painted sheet the camera flies over. Replaces the flat hypsometric fills of
/// T1.7 (TerrainBaker) entirely.
///
/// Pipeline, per output texel:
///   1. Sample the worldgen fields BILINEARLY at the texel's continuous world
///      position (the bake is supersampled ×N, so the washes carry more detail
///      than the 1024² field grid and stay smooth when the camera zooms in).
///   2. Splat weights from those fields (TerrainSplat) — continuous, no
///      classification edges.
///   3. Blend the weighted terrain wash TILES, sampled at a world-space UV.
///      Tiles are seamless (§4/§5 clause) and sampled with WRAPPED bilinear,
///      so no tile seam exists; a second, incommensurate-scale sample of the
///      dominant class breaks the repeat period so no tile GRID reads either
///      (the acceptance criterion "no visible tile grid at any zoom").
///   4. Multiply by the parchment base's local deviation from its own mean —
///      the paper's mottle and fiber print THROUGH every wash, which is what
///      makes the map read as one sheet rather than colored regions.
///   5. Ink the coast: a thin dark band on the land side of every shoreline,
///      plus faint parallel "engraved sea" hairlines offshore, both driven by
///      a chamfer distance-to-coast so they follow the coastline exactly.
///
/// Determinism: pure function of (terrain, library, options) — no RNG, no
/// wall-clock. Rows are independent, so the parallel bake is byte-identical
/// to the serial one (pinned by test). Sim.Ui sits outside the determinism
/// surface (ADR-009); this is engineering hygiene, not a law-5 obligation.
/// </summary>
public static class ParchmentBaker
{
    public sealed record Options(
        int Supersample = 2,
        double TileWorldSpanPx = 220.0,
        /// <summary>World pixels one parchment tile spans. MEASURED FINDING:
        /// at ~870 (the tile's own size × 1.7) the paper's fibre was stretched
        /// to invisibility — the sheet showed only its low-frequency mottle
        /// and the "paper tooth" the bible asks for never reached the eye.
        /// At ~260 the fibre reads at map scale while the mottle still spans
        /// several hundred pixels.</summary>
        double PaperWorldSpanPx = 260.0,
        double CoastInkPx = 2.2,
        double EngravedBandPx = 26.0,
        double PaperInfluence = 1.0)
    {
        public static readonly Options Default = new();
    }

    public sealed record Result(int Size, byte[] Rgba, double BakeMilliseconds)
    {
        public double MegabytesResident => Rgba.Length / (1024.0 * 1024.0);
    }

    public static Result Bake(TerrainSet terrain, AssetLibrary art, ulong worldSeed, Options? options = null)
    {
        Options o = options ?? Options.Default;
        int ss = Math.Max(1, o.Supersample);
        int n = terrain.Size, size = n * ss;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();

        ReadOnlySpan<double> elevation = terrain.Elevation;
        ReadOnlySpan<double> waterMask = terrain.Water;

        double minElev = double.MaxValue, maxElev = double.MinValue;
        for (int i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < minElev) minElev = elevation[i];
            if (elevation[i] > maxElev) maxElev = elevation[i];
        }
        double sea = terrain.SeaLevel;
        double landSpan = maxElev > sea ? maxElev - sea : 1.0;
        double depthSpan = sea > minElev ? sea - minElev : 1.0;

        float[] coastDistance = CoastDistance(waterMask, n);
        ArtImage paper = art.ParchmentFor(worldSeed);
        double paperMean = Luminance(ParchmentPalette.PaperMid);
        var tiles = new ArtImage[ParchmentPalette.TerrainClassCount];
        for (int c = 0; c < tiles.Length; c++) tiles[c] = art.Terrain((ParchmentPalette.TerrainClass)c);

        var pixels = new byte[size * size * 4];
        double[] elevationCopy = elevation.ToArray();
        double[] waterCopy = waterMask.ToArray();
        double[] moistureCopy = terrain.Moisture.ToArray();
        double[] fertilityCopy = terrain.Fertility.ToArray();

        Parallel.For(0, size, y =>
        {
            Span<double> weights = stackalloc double[ParchmentPalette.TerrainClassCount];
            double wy = (y + 0.5) / ss - 0.5;
            for (int x = 0; x < size; x++)
            {
                double wx = (x + 0.5) / ss - 0.5;

                double water = Sample(waterCopy, n, wx, wy);
                double elev = Sample(elevationCopy, n, wx, wy);
                bool isWater = water >= 0.5;
                TerrainSplat.Weights(
                    (elev - sea) / landSpan, (sea - elev) / depthSpan,
                    Sample(moistureCopy, n, wx, wy), Sample(fertilityCopy, n, wx, wy),
                    isWater, weights);

                // --- tiled wash blend -------------------------------------
                double u = wx / o.TileWorldSpanPx, v = wy / o.TileWorldSpanPx;
                double r = 0, g = 0, b = 0;
                int dominant = 0;
                double dominantWeight = 0.0;
                for (int c = 0; c < weights.Length; c++)
                {
                    double w = weights[c];
                    if (w > dominantWeight) { dominantWeight = w; dominant = c; }
                    if (w < 0.02) continue;   // below one part in fifty: invisible, skip the fetch
                    ParchmentPalette.Rgba s = tiles[c].SampleWrapped(u, v);
                    r += s.R * w; g += s.G * w; b += s.B * w;
                }
                // Normalize by the weight actually sampled (the skipped tail),
                // then break the repeat period with a second incommensurate
                // sample of the dominant wash.
                double sampled = 0.0;
                for (int c = 0; c < weights.Length; c++) if (weights[c] >= 0.02) sampled += weights[c];
                if (sampled > 0.0) { r /= sampled; g /= sampled; b /= sampled; }
                else { ParchmentPalette.Rgba f = TerrainSplat.Blend(weights); r = f.R; g = f.G; b = f.B; }

                ParchmentPalette.Rgba breakUp = tiles[dominant].SampleWrapped(u * 0.371 + 0.19, v * 0.371 + 0.57);
                const double BreakUpMix = 0.30;
                r = r * (1.0 - BreakUpMix) + breakUp.R * BreakUpMix;
                g = g * (1.0 - BreakUpMix) + breakUp.G * BreakUpMix;
                b = b * (1.0 - BreakUpMix) + breakUp.B * BreakUpMix;

                // --- the paper prints through -----------------------------
                ParchmentPalette.Rgba paperTexel = paper.SampleWrapped(
                    wx / o.PaperWorldSpanPx, wy / o.PaperWorldSpanPx);
                double paperFactor = 1.0 + o.PaperInfluence * (Luminance(paperTexel) - paperMean) / paperMean;
                r *= paperFactor; g *= paperFactor; b *= paperFactor;

                // --- ink the coast ----------------------------------------
                double dist = SampleF(coastDistance, n, wx, wy);
                if (!isWater && dist <= o.CoastInkPx)
                {
                    double k = 1.0 - dist / o.CoastInkPx;
                    double ink = 0.72 * k * k;
                    r += (ParchmentPalette.InkPrimary.R - r) * ink;
                    g += (ParchmentPalette.InkPrimary.G - g) * ink;
                    b += (ParchmentPalette.InkPrimary.B - b) * ink;
                }
                else if (isWater && dist <= o.EngravedBandPx)
                {
                    // Engraved sea: hairlines parallel to the coast, fading
                    // out with distance — the classic hand-map habit.
                    double phase = dist / (o.EngravedBandPx / 3.0);
                    double line = Math.Max(0.0, 1.0 - Math.Abs(phase - Math.Round(phase)) * 6.0);
                    double fade = 1.0 - dist / o.EngravedBandPx;
                    double ink = 0.16 * line * fade * fade;
                    r += (ParchmentPalette.InkSoft.R - r) * ink;
                    g += (ParchmentPalette.InkSoft.G - g) * ink;
                    b += (ParchmentPalette.InkSoft.B - b) * ink;
                }

                int off = (y * size + x) * 4;
                pixels[off] = TerrainSplat.Byte(r);
                pixels[off + 1] = TerrainSplat.Byte(g);
                pixels[off + 2] = TerrainSplat.Byte(b);
                pixels[off + 3] = 255;
            }
        });

        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency;
        return new Result(size, pixels, ms);
    }

    /// <summary>Chamfer distance (in terrain texels) from every cell to the
    /// nearest land/water boundary — two sweeps, exact enough for an ink band
    /// and orders faster than an exact EDT.</summary>
    public static float[] CoastDistance(ReadOnlySpan<double> water, int n)
    {
        var d = new float[n * n];
        const float Far = 1e9f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                bool w = water[i] >= 0.5;
                bool boundary = false;
                if (x > 0 && (water[i - 1] >= 0.5) != w) boundary = true;
                if (!boundary && x < n - 1 && (water[i + 1] >= 0.5) != w) boundary = true;
                if (!boundary && y > 0 && (water[i - n] >= 0.5) != w) boundary = true;
                if (!boundary && y < n - 1 && (water[i + n] >= 0.5) != w) boundary = true;
                d[i] = boundary ? 0f : Far;
            }
        }
        const float O = 1f, D = 1.41421356f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float m = d[i];
                if (x > 0) m = Math.Min(m, d[i - 1] + O);
                if (y > 0) m = Math.Min(m, d[i - n] + O);
                if (x > 0 && y > 0) m = Math.Min(m, d[i - n - 1] + D);
                if (x < n - 1 && y > 0) m = Math.Min(m, d[i - n + 1] + D);
                d[i] = m;
            }
        for (int y = n - 1; y >= 0; y--)
            for (int x = n - 1; x >= 0; x--)
            {
                int i = y * n + x;
                float m = d[i];
                if (x < n - 1) m = Math.Min(m, d[i + 1] + O);
                if (y < n - 1) m = Math.Min(m, d[i + n] + O);
                if (x < n - 1 && y < n - 1) m = Math.Min(m, d[i + n + 1] + D);
                if (x > 0 && y < n - 1) m = Math.Min(m, d[i + n - 1] + D);
                d[i] = m;
            }
        return d;
    }

    private static double Luminance(ParchmentPalette.Rgba c) =>
        0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    /// <summary>Bilinear field sample at continuous terrain coordinates,
    /// clamped at the edges (the world does not wrap).</summary>
    private static double Sample(double[] field, int n, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        int x1 = Math.Clamp(x0 + 1, 0, n - 1), y1 = Math.Clamp(y0 + 1, 0, n - 1);
        x0 = Math.Clamp(x0, 0, n - 1); y0 = Math.Clamp(y0, 0, n - 1);
        double a = field[y0 * n + x0], b = field[y0 * n + x1];
        double c = field[y1 * n + x0], e = field[y1 * n + x1];
        return (a + (b - a) * tx) + ((c + (e - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    private static double SampleF(float[] field, int n, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        int x1 = Math.Clamp(x0 + 1, 0, n - 1), y1 = Math.Clamp(y0 + 1, 0, n - 1);
        x0 = Math.Clamp(x0, 0, n - 1); y0 = Math.Clamp(y0, 0, n - 1);
        double a = field[y0 * n + x0], b = field[y0 * n + x1];
        double c = field[y1 * n + x0], e = field[y1 * n + x1];
        return (a + (b - a) * tx) + ((c + (e - c) * tx) - (a + (b - a) * tx)) * ty;
    }
}
