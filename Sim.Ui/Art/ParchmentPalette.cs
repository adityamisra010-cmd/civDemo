namespace Sim.Ui.Art;

/// <summary>
/// THE authoritative palette from docs/style-bible-parchment.md §2, transcribed
/// hex-for-hex. Everything the renderer draws — washes, ink, UI furniture,
/// territory tints — resolves through here, so the single-cartographer rule
/// (§1) is enforceable at the source: no code path may invent a color.
///
/// Pure view-model: byte RGBA, no MonoGame types, headless-testable.
/// </summary>
public static class ParchmentPalette
{
    public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
    {
        public static Rgba Hex(uint rgb) =>
            new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    // --- parchment base (§2) -------------------------------------------------
    public static readonly Rgba PaperLight = Rgba.Hex(0xEFE3C8);
    public static readonly Rgba PaperMid = Rgba.Hex(0xE3D3AE);
    public static readonly Rgba PaperShade = Rgba.Hex(0xC9B588);

    // --- ink (§2) ------------------------------------------------------------
    public static readonly Rgba InkPrimary = Rgba.Hex(0x3A2E1F);
    public static readonly Rgba InkSoft = Rgba.Hex(0x6B5A3E);

    // --- land washes, low → high (§2) ---------------------------------------
    public static readonly Rgba LowlandGreen = Rgba.Hex(0xA9B080);
    public static readonly Rgba FertileGreen = Rgba.Hex(0x8F9C63);
    public static readonly Rgba PlainTan = Rgba.Hex(0xC4B183);
    public static readonly Rgba Arid = Rgba.Hex(0xCBBE93);
    public static readonly Rgba UplandUmber = Rgba.Hex(0xA98B63);
    public static readonly Rgba PeakPale = Rgba.Hex(0xDCC9A0);

    // --- water (§2) ----------------------------------------------------------
    public static readonly Rgba Shallows = Rgba.Hex(0x9DB3B0);
    public static readonly Rgba Sea = Rgba.Hex(0x7C99A0);
    public static readonly Rgba DeepSea = Rgba.Hex(0x5F7E88);
    public static readonly Rgba River = Rgba.Hex(0x6E8A93);

    // --- accents (§2) — sparingly; symbology is DEFERRED --------------------
    public static readonly Rgba IronRed = Rgba.Hex(0x8C4A3A);
    public static readonly Rgba Verdigris = Rgba.Hex(0x5E7A6B);
    public static readonly Rgba GoldLeaf = Rgba.Hex(0xB08A3E);

    /// <summary>EVERY color the style bible §2 declares — the closed set the
    /// single-cartographer rule (§1) is judged against. Nothing outside this
    /// list may appear in an asset or a draw call.</summary>
    public static readonly Rgba[] BibleColors =
    [
        PaperLight, PaperMid, PaperShade, InkPrimary, InkSoft,
        LowlandGreen, FertileGreen, PlainTan, Arid, UplandUmber, PeakPale,
        Shallows, Sea, DeepSea, River, IronRed, Verdigris, GoldLeaf,
    ];

    public static double Saturation(Rgba c)
    {
        int hi = Math.Max(c.R, Math.Max(c.G, c.B)), lo = Math.Min(c.R, Math.Min(c.G, c.B));
        return hi == 0 ? 0.0 : (hi - lo) / (double)hi;
    }

    /// <summary>The most saturated ink the bible itself sanctions (gold-leaf).
    /// The §1 gamut bar: an asset more saturated than this introduced a new
    /// ink chemistry.</summary>
    public static double MaxBibleSaturation()
    {
        double max = 0.0;
        foreach (Rgba c in BibleColors) max = Math.Max(max, Saturation(c));
        return max;
    }

    /// <summary>The most saturated TERRAIN WASH the bible declares (upland
    /// umber). The bar a territory wash is judged against: an ink wash that
    /// sits ON the map must never read louder than the map's own washes.</summary>
    public static double MaxTerrainWashSaturation()
    {
        double max = 0.0;
        for (int c = 0; c < TerrainClassCount; c++) max = Math.Max(max, Saturation(Of((TerrainClass)c)));
        return max;
    }

    /// <summary>A territory ink composited over parchment at the §2 wash
    /// strength — what the director actually SEES, and what the tests judge.</summary>
    public static Rgba TerritoryWashOverPaper(int settlementId)
    {
        Rgba ink = TerritoryInk(settlementId);
        double t = TerritoryWashStrength;
        return new Rgba(
            (byte)Math.Round(PaperMid.R + (ink.R - PaperMid.R) * t),
            (byte)Math.Round(PaperMid.G + (ink.G - PaperMid.G) * t),
            (byte)Math.Round(PaperMid.B + (ink.B - PaperMid.B) * t));
    }

    /// <summary>The nine terrain wash classes of §4 item 3, in manifest order.
    /// The splat blends these; each has exactly one asset tile.</summary>
    public enum TerrainClass
    {
        Lowland = 0, Fertile = 1, Plain = 2, Arid = 3, Upland = 4, Peak = 5,
        Shallows = 6, Sea = 7, Deep = 8,
    }

    public const int TerrainClassCount = 9;

    /// <summary>The bible color for a class — the tint a placeholder swatch
    /// paints and the fallback the renderer uses when a tile is missing.</summary>
    public static Rgba Of(TerrainClass c) => c switch
    {
        TerrainClass.Lowland => LowlandGreen,
        TerrainClass.Fertile => FertileGreen,
        TerrainClass.Plain => PlainTan,
        TerrainClass.Arid => Arid,
        TerrainClass.Upland => UplandUmber,
        TerrainClass.Peak => PeakPale,
        TerrainClass.Shallows => Shallows,
        TerrainClass.Sea => Sea,
        TerrainClass.Deep => DeepSea,
        _ => PaperMid,
    };

    /// <summary>Asset-manifest key for a class tile (assets/terrain/&lt;name&gt;.png).</summary>
    public static string TileName(TerrainClass c) => c switch
    {
        TerrainClass.Lowland => "lowland",
        TerrainClass.Fertile => "fertile",
        TerrainClass.Plain => "plain",
        TerrainClass.Arid => "arid",
        TerrainClass.Upland => "upland",
        TerrainClass.Peak => "peak",
        TerrainClass.Shallows => "shallows",
        TerrainClass.Sea => "sea",
        TerrainClass.Deep => "deep",
        _ => "plain",
    };

    /// <summary>
    /// A political territory tint as an INK WASH over parchment (§2 rule):
    /// the accent is composited at ~35% strength rather than painted opaque, so
    /// the paper and its washes still read through. Deterministic per
    /// settlement id — the same twelve hues the T2.4 palette assigned, pulled
    /// into the parchment gamut (warm-biased, desaturated).
    /// </summary>
    public const double TerritoryWashStrength = 0.35;

    public static Rgba TerritoryInk(int settlementId)
    {
        // Twelve muted plate colors, all inside the §2 accent gamut: earths,
        // sages, and slates. Warm-biased; nothing vivid (the §2 rule).
        Rgba[] plates =
        [
            Rgba.Hex(0x8C4A3A), Rgba.Hex(0x5E7A6B), Rgba.Hex(0xB08A3E),
            Rgba.Hex(0x6B5A3E), Rgba.Hex(0x7C99A0), Rgba.Hex(0x8F6B4E),
            Rgba.Hex(0x77694A), Rgba.Hex(0x5F7E88), Rgba.Hex(0x9A7B52),
            Rgba.Hex(0x6E7F5C), Rgba.Hex(0x8A5F55), Rgba.Hex(0x6A7A78),
        ];
        int i = settlementId % plates.Length;
        if (i < 0) i += plates.Length;
        return plates[i];
    }
}
