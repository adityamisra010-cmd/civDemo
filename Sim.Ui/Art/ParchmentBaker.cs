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
///
/// BATCH 3 WS3 — THE CYLINDER (<see cref="TerrainSet.WrapX"/>). The game draws this sheet with a U-wrapped sampler
/// (SimUiGame), so its last column must meet its first without a seam:
///   * every field sample is x-PERIODIC (<see cref="Sample"/>, <see cref="SampleF"/>, <see cref="IsWaterAt(ReadOnlySpan{double}, int, double, double, bool)"/>,
///     <see cref="DisplayedTexel(double, int, int, bool)"/>), and the coast's chamfer distance sees its x neighbours across
///     the seam (<see cref="CoastDistance(ReadOnlySpan{double}, int, bool)"/>) — the coast ink and the engraved sea run on;
///   * the wash tiles and the paper are sampled at a world-space UV whose span (627 px for the shipped art) divides no world
///     width, so over the last <see cref="SeamFadePx"/> world px the textured colour CROSS-FADES (smoothstep) into the colour
///     the same texel would take one circumference west — the colour the first columns continue from. This works for any
///     --size, where a span that had to divide every world width could not. Only the cross-fade band's last columns differ
///     from a flat bake's arithmetic; the band is visible only as a slight softening of the paper's grain over it.
/// A FLAT terrain takes exactly the pre-WS3 arithmetic: its bake is byte-identical (pinned).
/// </summary>
public static class ParchmentBaker
{
    public sealed record Options(
        int Supersample = 2,
        /// <summary>World pixels one terrain wash tile spans, or NULL to derive
        /// it from the art's own resolution (tileWidth / Supersample, so one
        /// output texel samples about one source texel). MEASURED FINDING on
        /// the real drop: a fixed 220 px span minified the director's 1254²
        /// washes ~5.7:1, throwing away the paint detail the art exists to
        /// provide and crunching its noise. Auto-scaling shows the art at its
        /// own scale and, as a bonus, lengthens the repeat period to ~627 px so
        /// a 1024 px world barely repeats at all.</summary>
        double? TileWorldSpanPx = null,
        /// <summary>World pixels one parchment tile spans. MEASURED FINDING:
        /// at ~870 (the tile's own size × 1.7) the paper's fibre was stretched
        /// to invisibility — the sheet showed only its low-frequency mottle
        /// and the "paper tooth" the bible asks for never reached the eye.
        /// At ~260 the fibre reads at map scale while the mottle still spans
        /// several hundred pixels.</summary>
        double? PaperWorldSpanPx = null,
        double CoastInkPx = 2.2,
        double EngravedBandPx = 26.0,
        double PaperInfluence = 1.0,
        /// <summary>Apply the seam-hiding cross-fade at the sampling stage
        /// (ArtImage.SampleWrappedCrossFaded). ON by default: real generated
        /// art only approximately edge-wraps. Switchable so the rendered-seam
        /// measurement can be taken both ways.</summary>
        bool CrossFadeSeams = true)
    {
        public static readonly Options Default = new();
    }

    public sealed record Result(int Size, byte[] Rgba, double BakeMilliseconds, int WorldSizePx)
    {
        public double MegabytesResident => Rgba.Length / (1024.0 * 1024.0);

        /// <summary>Scale the world-space draw must apply so the atlas spans
        /// the WORLD ([0, WorldSizePx] px), not its own texel count. The
        /// supersampled atlas is Size = WorldSizePx × ss texels; drawn at
        /// native size it covers ss× the world — the coastal-flooding defect
        /// (settlements over the wash painted for the cell at 1/ss of their
        /// coordinates). Pinned by CoastlineRenderTests.</summary>
        public double WorldDrawScale => WorldSizePx / (double)Size;
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

        bool wrap = terrain.WrapX;   // WS3: an x-periodic sheet (see the type's remarks); flat: the pre-WS3 bake exactly
        float[] coastDistance = wrap ? CoastDistance(waterMask, n, wrapX: true) : CoastDistance(waterMask, n);
        double fade = wrap ? SeamFadePx(n) : 0.0;
        ArtImage paper = art.ParchmentFor(worldSeed);
        double paperSpan = o.PaperWorldSpanPx ?? Math.Max(64.0, paper.Width / (double)ss);
        double paperMean = Luminance(ParchmentPalette.PaperMid);
        var tiles = new ArtImage[ParchmentPalette.TerrainClassCount];
        var tileSpans = new double[ParchmentPalette.TerrainClassCount];
        for (int c = 0; c < tiles.Length; c++)
        {
            tiles[c] = art.Terrain((ParchmentPalette.TerrainClass)c);
            tileSpans[c] = o.TileWorldSpanPx ?? Math.Max(64.0, tiles[c].Width / (double)ss);
        }

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

                if (wrap)
                {
                    WrappedTexel(pixels, (y * size + x) * 4, wx, wy, n, o, weights, elevationCopy, waterCopy, moistureCopy,
                        fertilityCopy, coastDistance, tiles, tileSpans, paper, paperSpan, paperMean, sea, landSpan, depthSpan, fade);
                    continue;
                }

                double elev = Sample(elevationCopy, n, wx, wy);
                bool isWater = IsWaterAt(waterCopy, n, wx, wy);
                TerrainSplat.Weights(
                    (elev - sea) / landSpan, (sea - elev) / depthSpan,
                    Sample(moistureCopy, n, wx, wy), Sample(fertilityCopy, n, wx, wy),
                    isWater, weights);

                // --- tiled wash blend -------------------------------------
                double r = 0, g = 0, b = 0;
                int dominant = 0;
                double dominantWeight = 0.0;
                for (int c = 0; c < weights.Length; c++)
                {
                    double w = weights[c];
                    if (w > dominantWeight) { dominantWeight = w; dominant = c; }
                    if (w < 0.02) continue;   // below one part in fifty: invisible, skip the fetch
                    ParchmentPalette.Rgba s = o.CrossFadeSeams
                        ? tiles[c].SampleWrappedCrossFaded(wx / tileSpans[c], wy / tileSpans[c])
                        : tiles[c].SampleWrapped(wx / tileSpans[c], wy / tileSpans[c]);
                    r += s.R * w; g += s.G * w; b += s.B * w;
                }
                // Normalize by the weight actually sampled (the skipped tail),
                // then break the repeat period with a second incommensurate
                // sample of the dominant wash.
                double sampled = 0.0;
                for (int c = 0; c < weights.Length; c++) if (weights[c] >= 0.02) sampled += weights[c];
                if (sampled > 0.0) { r /= sampled; g /= sampled; b /= sampled; }
                else { ParchmentPalette.Rgba f = TerrainSplat.Blend(weights); r = f.R; g = f.G; b = f.B; }

                ParchmentPalette.Rgba breakUp = tiles[dominant].SampleWrappedCrossFaded(
                    wx / tileSpans[dominant] * 0.371 + 0.19, wy / tileSpans[dominant] * 0.371 + 0.57);
                const double BreakUpMix = 0.30;
                r = r * (1.0 - BreakUpMix) + breakUp.R * BreakUpMix;
                g = g * (1.0 - BreakUpMix) + breakUp.G * BreakUpMix;
                b = b * (1.0 - BreakUpMix) + breakUp.B * BreakUpMix;

                // --- the paper prints through -----------------------------
                ParchmentPalette.Rgba paperTexel = o.CrossFadeSeams
                    ? paper.SampleWrappedCrossFaded(wx / paperSpan, wy / paperSpan)
                    : paper.SampleWrapped(wx / paperSpan, wy / paperSpan);
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
        return new Result(size, pixels, ms, n);
    }

    /// <summary>WS3: the width in world px of the seam cross-fade at the sheet's eastern edge: 32 px, or a quarter of a
    /// world narrower than 128 px.</summary>
    public static double SeamFadePx(int worldSizePx) => Math.Min(32.0, worldSizePx / 4.0);

    /// <summary>
    /// WS3: ONE TEXEL of a wrapped sheet at world (<paramref name="wx"/>, <paramref name="wy"/>) — the flat texel's pipeline
    /// with x-periodic field samples, the textured colour (washes, break-up, paper) cross-faded over the last
    /// <paramref name="fade"/> world px into its value one circumference west (<see cref="Textured"/>), then the coast ink.
    /// </summary>
    private static void WrappedTexel(byte[] pixels, int off, double wx, double wy, int n, Options o, Span<double> weights,
        double[] elevation, double[] water, double[] moisture, double[] fertility, float[] coastDistance,
        ArtImage[] tiles, double[] tileSpans, ArtImage paper, double paperSpan, double paperMean,
        double sea, double landSpan, double depthSpan, double fade)
    {
        double elev = Sample(elevation, n, wx, wy, wrapX: true);
        bool isWater = IsWaterAt(water, n, wx, wy, wrapX: true);
        TerrainSplat.Weights(
            (elev - sea) / landSpan, (sea - elev) / depthSpan,
            Sample(moisture, n, wx, wy, wrapX: true), Sample(fertility, n, wx, wy, wrapX: true),
            isWater, weights);
        int dominant = 0;
        double dominantWeight = 0.0;
        for (int c = 0; c < weights.Length; c++)
            if (weights[c] > dominantWeight) { dominantWeight = weights[c]; dominant = c; }

        (double r, double g, double b) = Textured(wx, wy, o, weights, dominant, tiles, tileSpans, paper, paperSpan, paperMean);
        double start = n - fade;
        if (fade > 0 && wx > start)
        {
            // The seam: blend toward the colour the texel takes one circumference west — where the sheet's first columns
            // continue from — reaching it at the edge (smoothstep, so the band starts and ends without a crease).
            double t = Math.Min(1.0, (wx - start) / fade);
            t = t * t * (3.0 - 2.0 * t);
            (double r2, double g2, double b2) = Textured(wx - n, wy, o, weights, dominant, tiles, tileSpans, paper, paperSpan, paperMean);
            r = r * (1.0 - t) + r2 * t;
            g = g * (1.0 - t) + g2 * t;
            b = b * (1.0 - t) + b2 * t;
        }

        double dist = SampleF(coastDistance, n, wx, wy, wrapX: true);
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
            double phase = dist / (o.EngravedBandPx / 3.0);
            double line = Math.Max(0.0, 1.0 - Math.Abs(phase - Math.Round(phase)) * 6.0);
            double fadeOut = 1.0 - dist / o.EngravedBandPx;
            double ink = 0.16 * line * fadeOut * fadeOut;
            r += (ParchmentPalette.InkSoft.R - r) * ink;
            g += (ParchmentPalette.InkSoft.G - g) * ink;
            b += (ParchmentPalette.InkSoft.B - b) * ink;
        }

        pixels[off] = TerrainSplat.Byte(r);
        pixels[off + 1] = TerrainSplat.Byte(g);
        pixels[off + 2] = TerrainSplat.Byte(b);
        pixels[off + 3] = 255;
    }

    /// <summary>WS3: the UV-dependent half of a texel — the weighted wash blend, the dominant wash's break-up sample and the
    /// paper's print-through — at world-space UV x <paramref name="ux"/> (the flat bake's own arithmetic, in its order).</summary>
    private static (double R, double G, double B) Textured(double ux, double wy, Options o, ReadOnlySpan<double> weights, int dominant,
        ArtImage[] tiles, double[] tileSpans, ArtImage paper, double paperSpan, double paperMean)
    {
        double r = 0, g = 0, b = 0;
        for (int c = 0; c < weights.Length; c++)
        {
            double w = weights[c];
            if (w < 0.02) continue;
            ParchmentPalette.Rgba s = o.CrossFadeSeams
                ? tiles[c].SampleWrappedCrossFaded(ux / tileSpans[c], wy / tileSpans[c])
                : tiles[c].SampleWrapped(ux / tileSpans[c], wy / tileSpans[c]);
            r += s.R * w; g += s.G * w; b += s.B * w;
        }
        double sampled = 0.0;
        for (int c = 0; c < weights.Length; c++) if (weights[c] >= 0.02) sampled += weights[c];
        if (sampled > 0.0) { r /= sampled; g /= sampled; b /= sampled; }
        else { ParchmentPalette.Rgba f = TerrainSplat.Blend(weights); r = f.R; g = f.G; b = f.B; }

        ParchmentPalette.Rgba breakUp = tiles[dominant].SampleWrappedCrossFaded(
            ux / tileSpans[dominant] * 0.371 + 0.19, wy / tileSpans[dominant] * 0.371 + 0.57);
        const double BreakUpMix = 0.30;
        r = r * (1.0 - BreakUpMix) + breakUp.R * BreakUpMix;
        g = g * (1.0 - BreakUpMix) + breakUp.G * BreakUpMix;
        b = b * (1.0 - BreakUpMix) + breakUp.B * BreakUpMix;

        ParchmentPalette.Rgba paperTexel = o.CrossFadeSeams
            ? paper.SampleWrappedCrossFaded(ux / paperSpan, wy / paperSpan)
            : paper.SampleWrapped(ux / paperSpan, wy / paperSpan);
        double paperFactor = 1.0 + o.PaperInfluence * (Luminance(paperTexel) - paperMean) / paperMean;
        return (r * paperFactor, g * paperFactor, b * paperFactor);
    }

    /// <summary>
    /// THE LAND/WATER HARD GATE (director's coastal-defect ruling): the paint
    /// decision at a continuous world position is the SIM'S CELL-LEVEL MASK —
    /// the nearest cell's <c>water</c> value, the same per-cell threshold
    /// <c>TerrainSet.IsWater</c> and settlement siting use. Land cells blend
    /// only land washes, water cells only water washes, and the transition
    /// falls exactly on cell edges — the coastline, where the CoastDistance
    /// ink already draws. A BILINEARLY interpolated mask thresholded at 0.5
    /// (the previous decision) happens to agree with the cell mask at ss=2
    /// (texel centres sit only ±0.25 from cell centres, where the own cell's
    /// bilinear weight is ≥ 9/16 &gt; 1/2), but at ss ≥ 3 corner texels can
    /// cross 0.5 and RESHAPE the coastline — the gate must not depend on the
    /// supersample factor. Pinned by CoastlineRenderTests.
    /// </summary>
    public static bool IsWaterAt(ReadOnlySpan<double> water, int n, double wx, double wy)
    {
        int mx = Math.Clamp((int)Math.Floor(wx + 0.5), 0, n - 1);
        int my = Math.Clamp((int)Math.Floor(wy + 0.5), 0, n - 1);
        return water[my * n + mx] >= 0.5;
    }

    /// <summary>WS3: <see cref="IsWaterAt(ReadOnlySpan{double}, int, double, double)"/> on a wrapped terrain — the nearest
    /// cell's column NORMALISED (the cell across the seam), y clamped; flat (<paramref name="wrapX"/> false): exactly it.</summary>
    public static bool IsWaterAt(ReadOnlySpan<double> water, int n, double wx, double wy, bool wrapX)
    {
        if (!wrapX) return IsWaterAt(water, n, wx, wy);
        int mx = Sim.Core.Pathing.MapTopology.WrapX((long)Math.Floor(wx + 0.5), n);
        int my = Math.Clamp((int)Math.Floor(wy + 0.5), 0, n - 1);
        return water[my * n + mx] >= 0.5;
    }

    /// <summary>
    /// Which atlas texel the world-space draw displays at a world position —
    /// the render-side half of the coastline contract, kept as pure math so
    /// the marker-over-wash agreement is testable headless. The atlas spans
    /// [0, worldSizePx] world px EXACTLY: texel t covers world
    /// [t·w/b, (t+1)·w/b), and its bake-sample position (t+0.5)/ss − 0.5
    /// (cell-index space) is the centre of that footprint — so a draw scaled
    /// by worldSizePx/bakeSize aligns every texel with the ground it was
    /// painted for, with no half-texel offset. THE COASTAL-FLOODING DEFECT
    /// was this mapping done wrong: the supersampled atlas drawn at NATIVE
    /// texel size showed, at world (x, y), the texel baked for the cell at
    /// (x/ss, y/ss) — five coastal settlements rendered in the sea. Pinned
    /// by CoastlineRenderTests, which fail on floor(worldPx).
    /// </summary>
    public static int DisplayedTexel(double worldPx, int worldSizePx, int bakeSize) =>
        Math.Clamp((int)Math.Floor(worldPx * bakeSize / (double)worldSizePx), 0, bakeSize - 1);

    /// <summary>WS3: the texel the U-wrapped draw displays at world x <paramref name="worldPx"/> — any copy of the world —
    /// the texel index NORMALISED (the GPU's AddressU = Wrap); along y (or flat) exactly
    /// <see cref="DisplayedTexel(double, int, int)"/>.</summary>
    public static int DisplayedTexel(double worldPx, int worldSizePx, int bakeSize, bool wrapX) =>
        wrapX ? Sim.Core.Pathing.MapTopology.WrapX((long)Math.Floor(worldPx * bakeSize / (double)worldSizePx), bakeSize)
              : DisplayedTexel(worldPx, worldSizePx, bakeSize);

    /// <summary>WS3: how many columns either side of a wrapped coast distance are computed (<see cref="CoastDistance(ReadOnlySpan{double}, int, bool)"/>)
    /// — beyond every distance the ink uses (the engraved band, 26 px), so every inked distance is the cylinder's.</summary>
    public const int CoastSeamPadPx = 64;

    /// <summary>
    /// WS3: <see cref="CoastDistance(ReadOnlySpan{double}, int)"/> on a CYLINDER: the same chamfer over the map widened by
    /// <see cref="CoastSeamPadPx"/> columns copied from across the seam on each side (column j is world column
    /// (j − pad) mod n), so a cell's boundary test and its two sweeps see their x neighbours across the seam; the centre n
    /// columns are kept. Every distance up to the pad is the cylinder's own (deterministic beyond it). Flat: exactly the flat one.
    /// </summary>
    public static float[] CoastDistance(ReadOnlySpan<double> water, int n, bool wrapX)
    {
        if (!wrapX) return CoastDistance(water, n);
        int pad = Math.Min(n, CoastSeamPadPx), m = n + 2 * pad;
        var wide = new double[m * n];
        for (int y = 0; y < n; y++)
            for (int j = 0; j < m; j++)
                wide[y * m + j] = water[y * n + Sim.Core.Pathing.MapTopology.WrapX(j - pad, n)];
        float[] d = CoastDistanceRect(wide, m, n);
        var result = new float[n * n];
        for (int y = 0; y < n; y++)
            Array.Copy(d, y * m + pad, result, y * n, n);
        return result;
    }

    /// <summary>The chamfer of <see cref="CoastDistance(ReadOnlySpan{double}, int)"/> over a <paramref name="w"/> ×
    /// <paramref name="h"/> raster (rows of <paramref name="w"/>).</summary>
    private static float[] CoastDistanceRect(double[] water, int w, int h)
    {
        var d = new float[w * h];
        const float Far = 1e9f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool wet = water[i] >= 0.5;
                bool boundary = (x > 0 && (water[i - 1] >= 0.5) != wet)
                    || (x < w - 1 && (water[i + 1] >= 0.5) != wet)
                    || (y > 0 && (water[i - w] >= 0.5) != wet)
                    || (y < h - 1 && (water[i + w] >= 0.5) != wet);
                d[i] = boundary ? 0f : Far;
            }
        const float O = 1f, D = 1.41421356f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float m = d[i];
                if (x > 0) m = Math.Min(m, d[i - 1] + O);
                if (y > 0) m = Math.Min(m, d[i - w] + O);
                if (x > 0 && y > 0) m = Math.Min(m, d[i - w - 1] + D);
                if (x < w - 1 && y > 0) m = Math.Min(m, d[i - w + 1] + D);
                d[i] = m;
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                float m = d[i];
                if (x < w - 1) m = Math.Min(m, d[i + 1] + O);
                if (y < h - 1) m = Math.Min(m, d[i + w] + O);
                if (x < w - 1 && y < h - 1) m = Math.Min(m, d[i + w + 1] + D);
                if (x > 0 && y < h - 1) m = Math.Min(m, d[i + w - 1] + D);
                d[i] = m;
            }
        return d;
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

    /// <summary>WS3: the bilinear sample on a cylinder — x PERIODIC (both corner columns normalised: the cell across the
    /// seam is the neighbour), y clamped as before. <paramref name="wrapX"/> false: exactly the flat sample. Public (no
    /// behaviour change) so the periodic field read is pinned directly, as IsWaterAt and DisplayedTexel are.</summary>
    public static double Sample(double[] field, int n, double x, double y, bool wrapX)
    {
        if (!wrapX) return Sample(field, n, x, y);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        int x1 = Sim.Core.Pathing.MapTopology.WrapX(x0 + 1, n), y1 = Math.Clamp(y0 + 1, 0, n - 1);
        x0 = Sim.Core.Pathing.MapTopology.WrapX(x0, n); y0 = Math.Clamp(y0, 0, n - 1);
        double a = field[y0 * n + x0], b = field[y0 * n + x1];
        double c = field[y1 * n + x0], e = field[y1 * n + x1];
        return (a + (b - a) * tx) + ((c + (e - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    /// <summary>WS3: <see cref="SampleF(float[], int, double, double)"/>, x periodic on a cylinder (the coast distance's
    /// read). Public for the same reason as <see cref="Sample(double[], int, double, double, bool)"/>.</summary>
    public static double SampleF(float[] field, int n, double x, double y, bool wrapX)
    {
        if (!wrapX) return SampleF(field, n, x, y);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = x - x0, ty = y - y0;
        int x1 = Sim.Core.Pathing.MapTopology.WrapX(x0 + 1, n), y1 = Math.Clamp(y0 + 1, 0, n - 1);
        x0 = Sim.Core.Pathing.MapTopology.WrapX(x0, n); y0 = Math.Clamp(y0, 0, n - 1);
        double a = field[y0 * n + x0], b = field[y0 * n + x1];
        double c = field[y1 * n + x0], e = field[y1 * n + x1];
        return (a + (b - a) * tx) + ((c + (e - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    /// <summary>Bilinear field sample at continuous terrain coordinates,
    /// clamped at the edges (a flat world; WS3's wrapped one is the overload above).</summary>
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
