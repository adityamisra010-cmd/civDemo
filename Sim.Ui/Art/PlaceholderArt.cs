namespace Sim.Ui.Art;

/// <summary>
/// PROGRAMMATIC PLACEHOLDER ART — the decoupling device of this packet.
///
/// Every asset in style-bible §4 gets a stand-in generated here: flat
/// in-palette swatches for the terrain washes, tileable value-noise for the
/// parchment base and grain, simple inked shapes for the UI furniture. The
/// renderer NEVER knows the difference: it loads whatever PNG sits at the
/// manifest path, so dropping the director's generated art into assets/
/// replaces these with zero code change.
///
/// Two properties are structural, not cosmetic, and are pinned by tests:
///  * SEAMLESS (bible §4/§5 seamless clause): every tileable placeholder is
///    generated from PERIODIC lattice noise, so it edge-wraps exactly — the
///    2×2 tiling test that judges the real art also passes on the stand-ins.
///  * IN-PALETTE (bible §1 single-cartographer rule): every pixel is derived
///    from ParchmentPalette; no placeholder may invent a color, so a
///    placeholder map is a truthful rehearsal of the real one's structure.
///
/// Determinism: a fixed integer hash, no System.Random — regenerating the
/// placeholders byte-reproduces them (Sim.Ui is outside the determinism
/// surface, but reproducible assets keep the gate build honest).
/// </summary>
public static class PlaceholderArt
{
    /// <summary>Writes every manifest asset that is MISSING beneath
    /// <paramref name="root"/>. Existing files are left alone — the director's
    /// real art always wins over a stand-in.</summary>
    public static IReadOnlyList<string> GenerateMissing(string root)
    {
        var written = new List<string>();
        foreach (AssetManifest.Entry entry in AssetManifest.All)
        {
            string path = Path.Combine(root, entry.RelativePath);
            if (File.Exists(path)) continue;
            PngCodec.Write(path, Generate(entry));
            written.Add(entry.RelativePath);
        }
        return written;
    }

    public static ArtImage Generate(AssetManifest.Entry entry) => entry.Kind switch
    {
        AssetManifest.AssetKind.ParchmentBase => Parchment(512, entry.Variant),
        AssetManifest.AssetKind.Grain => Grain(512),
        AssetManifest.AssetKind.TerrainWash => Wash(256, entry.TerrainClass!.Value),
        AssetManifest.AssetKind.CoastHairline => Hairline(64, 8),
        AssetManifest.AssetKind.UiPanel => Panel(64),
        AssetManifest.AssetKind.UiHeaderRule => HeaderRule(64, 8),
        AssetManifest.AssetKind.UiButtonPlate => ButtonPlate(64, 24),
        AssetManifest.AssetKind.UiAnnalsBackground => AnnalsBackground(128),
        AssetManifest.AssetKind.UiCompassRose => CompassRose(128),
        AssetManifest.AssetKind.SettlementMarker => Marker(64),
        _ => Wash(64, ParchmentPalette.TerrainClass.Plain),
    };

    // --- the substrate -------------------------------------------------------

    /// <summary>Aged paper stand-in: paper-mid ground, low-frequency mottle
    /// toward light and shade, faint fiber streaks. Seamless by construction.</summary>
    private static ArtImage Parchment(int size, int variant)
    {
        var img = New(size, size);
        uint seed = 0x9E3779B9u + (uint)variant * 0x85EBCA6Bu;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double u = (double)x / size, v = (double)y / size;
                // Mottle (low frequency) + fibre (high frequency). MEASURED
                // FINDING: averaging many octaves pulled the distribution to
                // the middle by the central limit and the sheet came out
                // nearly uniform — the paper stopped reading as paper once the
                // terrain washes were laid over it. Few octaves plus an
                // explicit contrast stretch put the tone back across the §2
                // paper range (light ↔ shade), which is what an aged sheet
                // actually does; the range itself is unchanged, so §1 holds.
                double mottle = 0.7 * Noise(u, v, 3, seed) + 0.3 * Noise(u, v, 6, seed ^ 0x1234u);
                double fibre = Noise(u, v, 41, seed ^ 0xABCDu);
                double t = TerrainSplat.Clamp01(
                    ((mottle * 0.78 + fibre * 0.22) - 0.5) * 2.1 + 0.5);
                // Paper light ↔ mid ↔ shade: mostly mid, edges of the range rare.
                ParchmentPalette.Rgba c = t < 0.5
                    ? Mix(ParchmentPalette.PaperLight, ParchmentPalette.PaperMid, t * 2.0)
                    : Mix(ParchmentPalette.PaperMid, ParchmentPalette.PaperShade, (t - 0.5) * 2.0);
                Set(img, x, y, c);
            }
        }
        return img;
    }

    /// <summary>Grain/age overlay: near-white grayscale multiplied over
    /// EVERYTHING (map and UI alike, bible §4 item 2). Very low contrast —
    /// the darkest texel is ~0.88, so the multiply reads as tooth, not dirt.</summary>
    private static ArtImage Grain(int size)
    {
        var img = New(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double u = (double)x / size, v = (double)y / size;
                double n = 0.5 * Noise(u, v, 37, 0x5F3Au) + 0.5 * Noise(u, v, 71, 0xC0FFEEu);
                byte g = TerrainSplat.Byte(255.0 * (0.88 + 0.12 * n));
                Set(img, x, y, new ParchmentPalette.Rgba(g, g, g));
            }
        }
        return img;
    }

    /// <summary>A terrain wash swatch: the class's bible color with a whisper
    /// of tonal variation (±3%), which is exactly what §5's per-asset slot
    /// asks for ("subtle tonal variation only").</summary>
    private static ArtImage Wash(int size, ParchmentPalette.TerrainClass cls)
    {
        var img = New(size, size);
        ParchmentPalette.Rgba baseColor = ParchmentPalette.Of(cls);
        uint seed = 0x2545F491u + (uint)cls * 0x9E3779B9u;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double u = (double)x / size, v = (double)y / size;
                double n = Noise(u, v, 5, seed) * 0.6 + Noise(u, v, 13, seed ^ 0x55u) * 0.4;
                double k = 0.97 + 0.06 * n;
                Set(img, x, y, new ParchmentPalette.Rgba(
                    TerrainSplat.Byte(baseColor.R * k),
                    TerrainSplat.Byte(baseColor.G * k),
                    TerrainSplat.Byte(baseColor.B * k)));
            }
        }
        return img;
    }

    /// <summary>The offshore "engraved sea" hairline: an ink-soft line with
    /// transparent margins, tiled ALONG a coast-parallel band.</summary>
    private static ArtImage Hairline(int width, int height)
    {
        var img = New(width, height);
        for (int y = 0; y < height; y++)
        {
            double d = Math.Abs(y - (height - 1) / 2.0) / (height / 2.0);
            byte alpha = TerrainSplat.Byte(255.0 * Math.Max(0.0, 1.0 - d * d * 3.0) * 0.55);
            for (int x = 0; x < width; x++)
                Set(img, x, y, ParchmentPalette.InkSoft with { A = alpha });
        }
        return img;
    }

    // --- UI furniture --------------------------------------------------------

    /// <summary>9-slice panel: parchment field, ink-primary keyline inset one
    /// texel from the edge, slightly heavier at the corners.</summary>
    private static ArtImage Panel(int size)
    {
        var img = New(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int edge = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
                ParchmentPalette.Rgba c;
                if (edge == 0) c = ParchmentPalette.PaperShade with { A = 210 };
                else if (edge is 2 or 3) c = ParchmentPalette.InkPrimary with { A = (byte)(edge == 2 ? 235 : 120) };
                else c = ParchmentPalette.PaperMid with { A = 236 };
                Set(img, x, y, c);
            }
        }
        // Corner emphasis: a short heavier stroke along both axes.
        for (int i = 4; i < 12; i++)
        {
            foreach ((int cx, int cy) in new[] { (i, 2), (2, i), (size - 1 - i, 2), (2, size - 1 - i),
                                                 (i, size - 3), (size - 3, i), (size - 1 - i, size - 3), (size - 3, size - 1 - i) })
                Set(img, cx, cy, ParchmentPalette.InkPrimary);
        }
        return img;
    }

    private static ArtImage HeaderRule(int width, int height)
    {
        var img = New(width, height);
        for (int y = 0; y < height; y++)
        {
            // A heavy rule with a hairline companion beneath — engraved-map habit.
            byte a = y switch { 2 => 235, 3 => 200, 5 => 90, _ => 0 };
            for (int x = 0; x < width; x++)
                Set(img, x, y, ParchmentPalette.InkPrimary with { A = a });
        }
        return img;
    }

    private static ArtImage ButtonPlate(int width, int height)
    {
        var img = New(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Math.Min(Math.Min(x, y), Math.Min(width - 1 - x, height - 1 - y));
                ParchmentPalette.Rgba c = edge == 0
                    ? ParchmentPalette.InkPrimary with { A = 200 }
                    : Mix(ParchmentPalette.PaperLight, ParchmentPalette.PaperMid, y / (double)height) with { A = 245 };
                Set(img, x, y, c);
            }
        }
        return img;
    }

    /// <summary>The Annals sheet: a slightly warmer parchment with a faint
    /// ruled line every 8 texels — a scholar's notebook, tiling vertically.</summary>
    private static ArtImage AnnalsBackground(int size)
    {
        var img = New(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double u = (double)x / size, v = (double)y / size;
                double n = Noise(u, v, 4, 0xA11Au);
                ParchmentPalette.Rgba c = Mix(ParchmentPalette.PaperLight, ParchmentPalette.PaperMid, 0.35 + 0.4 * n);
                if (y % 16 == 15) c = Mix(c, ParchmentPalette.InkSoft, 0.10);
                Set(img, x, y, c with { A = 248 });
            }
        }
        return img;
    }

    /// <summary>Corner compass rose: an eight-point star in ink over
    /// transparency. Decorative only — NOT symbology (bible §4 item 5).</summary>
    private static ArtImage CompassRose(int size)
    {
        var img = New(size, size);
        double c0 = (size - 1) / 2.0, outer = size * 0.46, inner = size * 0.15;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - c0, dy = y - c0;
                double r = Math.Sqrt(dx * dx + dy * dy);
                double a = Math.Atan2(dy, dx);
                // Eight points: radius modulated by |cos(4θ)|, cardinal arms longer.
                double petal = Math.Abs(Math.Cos(4.0 * a));
                double cardinal = Math.Abs(Math.Cos(2.0 * a));
                double reach = inner + (outer - inner) * (0.45 * petal + 0.55 * Math.Pow(cardinal, 6.0));
                byte alpha = 0;
                if (r <= reach) alpha = (byte)(r > reach - 1.6 ? 235 : 90);       // outline + wash
                if (Math.Abs(r - outer * 0.98) < 0.9) alpha = 200;                 // encircling ring
                if (r < size * 0.035) alpha = 235;                                 // hub
                if (alpha > 0) Set(img, x, y, ParchmentPalette.InkPrimary with { A = alpha });
            }
        }
        return img;
    }

    /// <summary>The generic settlement marker (bible §4 item 6, substrate-safe):
    /// a small inked ring on paper that reads at every zoom. NO size tiers, no
    /// era-specific vocabulary — that is the deferred symbology packet.</summary>
    private static ArtImage Marker(int size)
    {
        var img = New(size, size);
        double c0 = (size - 1) / 2.0, r0 = size * 0.34;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - size / 2.0, dy = y + 0.5 - size / 2.0;
                double r = Math.Sqrt(dx * dx + dy * dy);
                ParchmentPalette.Rgba c;
                if (r > r0) c = new ParchmentPalette.Rgba(0, 0, 0, 0);
                else if (r > r0 - size * 0.09) c = ParchmentPalette.InkPrimary;
                else c = ParchmentPalette.PaperLight with { A = 245 };
                Set(img, x, y, c);
            }
        }
        return img;
    }

    // --- tileable value noise ------------------------------------------------

    /// <summary>Value noise on a PERIODIC lattice of <paramref name="cells"/>
    /// cells across the unit square: because the lattice wraps, the image
    /// edge-wraps exactly — the seamless clause holds by construction, not by
    /// inspection. Smoothstep interpolation keeps it free of lattice creases.</summary>
    private static double Noise(double u, double v, int cells, uint seed)
    {
        double fx = u * cells, fy = v * cells;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        double tx = Smooth(fx - x0), ty = Smooth(fy - y0);
        int x1 = Mod(x0 + 1, cells), y1 = Mod(y0 + 1, cells);
        x0 = Mod(x0, cells); y0 = Mod(y0, cells);

        double a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
        double c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

    private static int Mod(int i, int n) { i %= n; return i < 0 ? i + n : i; }

    private static double Hash(int x, int y, uint seed)
    {
        uint h = (uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ seed;
        h ^= h >> 16; h *= 0x7FEB352Du;
        h ^= h >> 15; h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h / (double)uint.MaxValue;
    }

    // --- helpers -------------------------------------------------------------

    private static ArtImage New(int w, int h) => new(w, h, new byte[w * h * 4]);

    private static void Set(ArtImage img, int x, int y, ParchmentPalette.Rgba c)
    {
        int o = (y * img.Width + x) * 4;
        img.Rgba[o] = c.R; img.Rgba[o + 1] = c.G; img.Rgba[o + 2] = c.B; img.Rgba[o + 3] = c.A;
    }

    internal static ParchmentPalette.Rgba Mix(ParchmentPalette.Rgba a, ParchmentPalette.Rgba b, double t)
    {
        t = TerrainSplat.Clamp01(t);
        return new ParchmentPalette.Rgba(
            TerrainSplat.Byte(a.R + (b.R - a.R) * t),
            TerrainSplat.Byte(a.G + (b.G - a.G) * t),
            TerrainSplat.Byte(a.B + (b.B - a.B) * t),
            TerrainSplat.Byte(a.A + (b.A - a.A) * t));
    }
}
