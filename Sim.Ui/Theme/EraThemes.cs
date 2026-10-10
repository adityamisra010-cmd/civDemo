using Sim.Ui.Art;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;
using static Sim.Ui.Theme.ThemeColor;

namespace Sim.Ui.Theme;

/// <summary>
/// THE NINE ERAS' TOKENS — <see cref="For"/> is pure and deterministic: the era in, the theme out, no
/// time, no randomness, no world (procedural jitter downstream is seeded from stable ids in
/// <see cref="PanelFrame"/>). One continuum, not nine skins:
/// <list type="bullet">
/// <item><b>One ground polarity.</b> Every era is ink on a light record surface — limestone, clay,
/// sandstone, lime plaster, marble, vellum, laid paper, drafting film, coated stock — so no Age flips
/// the screen from dark to light; the parchment era (A6) is the style bible's palette exactly.</item>
/// <item><b>Semantic hues are constants.</b> Each semantic token is generated from a FIXED hue
/// (<see cref="SemanticFamilies"/>); an era only changes the pigment (saturation, lightness) — ochre
/// earth at A1, printer's inks at A7, clean modern inks at A9.</item>
/// <item><b>What evolves.</b> Material and its texture, edge roughness (1.0 → 0), corner cut,
/// ornament (rises to the illuminated manuscript, then falls away to the precise modern frame),
/// typography (heavier Garamond → inscriptional capitals → print Garamond → technical and modern Plex
/// Sans), density (1 → 5), control granularity (coarse → precise), progress (tally notches →
/// segments → bar → graduated bar), icons (daubed → precise) and charts (tallies → graduated grids).</item>
/// </list>
/// The values are TUNE: change a hex or a number here and every surface follows.
/// </summary>
public static class EraThemes
{
    private static readonly EraTheme[] Cache = BuildAll();

    /// <summary>The theme of <paramref name="era"/> (memoised; identical to <see cref="Build"/>).</summary>
    public static EraTheme For(UiEra era) => Cache[UiEras.FromAge((int)era).Ordinal() - 1];

    /// <summary>All nine themes, A1..A9.</summary>
    public static IReadOnlyList<EraTheme> All => Cache;

    private static EraTheme[] BuildAll()
    {
        var all = new EraTheme[UiEras.Count];
        for (int i = 0; i < all.Length; i++) all[i] = Build((UiEra)(i + 1));
        return all;
    }

    /// <summary>Builds a theme from scratch (no cache) — what the determinism tests compare.</summary>
    public static EraTheme Build(UiEra era) => Readable(UiEras.FromAge((int)era) switch
    {
        UiEra.Prehistoric => Prehistoric(),
        UiEra.Neolithic => Neolithic(),
        UiEra.Bronze => Bronze(),
        UiEra.Iron => Iron(),
        UiEra.Classical => Classical(),
        UiEra.Medieval => Medieval(),
        UiEra.EarlyModern => EarlyModern(),
        UiEra.Industrial => Industrial(),
        _ => Modern(),
    });

    /// <summary>The token-level contrast floor of <see cref="InkTokens.TextDim"/> on the panel (UR-1).</summary>
    public const double TextDimFloor = 5.0;

    /// <summary>
    /// THE READABILITY FLOORS (M5 polish, UI readability UR-1; Director directive 2026-10-06 §2: gameplay information
    /// beats decorative texture, and later eras must not shrink type). Applied to every era after its mood tokens:
    /// <list type="bullet">
    /// <item>the type scale never shrinks below the reference: <c>SizeScale ≥ 1.0</c> (A7–A9 were 0.98, 0.95, 0.94;
    /// the face, weight, tracking and case still evolve — ADR-033 D8's evolving type scale, without the shrink);</item>
    /// <item><see cref="InkTokens.TextDim"/> is darkened toward the body ink until it reaches <see cref="TextDimFloor"/>
    /// on the panel (it measured 3.55:1 at A6), so even the tertiary ink stays above 4.5:1 once rendered.</item>
    /// </list>
    /// The state fills' separation is built in <see cref="Semantics"/>; the semantic text inks are derived
    /// (<see cref="EraTheme.TextInk"/>).
    /// </summary>
    private static EraTheme Readable(EraTheme t) => t with
    {
        Ink = t.Ink with { TextDim = TextInks.Darken(t.Ink.TextDim, t.Ink.Text, t.Material.Panel, TextDimFloor) },
        Type = t.Type with { SizeScale = Math.Max(1.0, t.Type.SizeScale) },
        Semantic = DistinctStates(t.Semantic, t.Material, t.Ink.Text),
    };

    /// <summary>The minimum colour difference (CIE ΔE*ab) between any two of the four research-card state fills —
    /// Known, Target, Available, Locked (UR-4: the state table's fills are plainly distinct surfaces; Known and Locked
    /// were ΔE 6 apart at A1 and A6, Known and Target 1.00:1 in lightness).</summary>
    public const double StateFillDistinct = 12.0;

    /// <summary>The body ink's floor on every state fill (a card's name is primary text: 7:1).</summary>
    public const double StateFillTextFloor = 7.0;

    /// <summary>
    /// THE STATE FILLS, MADE DISTINCT (UR-4). Available and Locked are set by their lightness separation
    /// (<see cref="StateFillSeparation"/>); the Known (Completed gold) and Target (Active teal) fills are then the
    /// LEAST tinted mixes of the panel or the raised surface toward their pigments that sit at least
    /// <see cref="StateFillDistinct"/> from every other state fill while the body ink keeps
    /// <see cref="StateFillTextFloor"/> on them — the era's mood where it already works, a stronger tint only where two
    /// states were too alike. Integer search, deterministic; the hue family is the pigment's (pinned).
    /// </summary>
    private static SemanticTokens DistinctStates(SemanticTokens s, MaterialTokens m, Rgba text)
    {
        Rgba avail = s.AvailableFill, locked = s.LockedFill;
        var done = new List<(Rgba Fill, int K)>();
        var target = new List<(Rgba Fill, int K)>();
        foreach (Rgba baseFill in new[] { m.Panel, m.PanelRaised })
            for (int k = 16; k <= 70; k += 2)
            {
                Rgba d = Mix(baseFill, s.Completed, k / 100.0), a = Mix(baseFill, s.Active, k / 100.0);
                if (Contrast(text, d) >= StateFillTextFloor) done.Add((d, k));
                if (Contrast(text, a) >= StateFillTextFloor) target.Add((a, k));
            }
        if (done.Count == 0 || target.Count == 0) return s;
        (Rgba Done, Rgba Target, int Cost, double Min) best = (s.CompletedFill, s.ActiveFill, int.MaxValue, MinDelta(s.CompletedFill, s.ActiveFill, avail, locked));
        bool met = best.Min >= StateFillDistinct;
        foreach ((Rgba d, int kd) in done)
            foreach ((Rgba a, int ka) in target)
            {
                double min = MinDelta(d, a, avail, locked);
                bool ok = min >= StateFillDistinct;
                int cost = kd + ka;
                // Prefer any candidate that meets the floor, then the least tint (ties: the larger margin); when none
                // meets it, the largest minimum difference.
                if (ok && (!met || cost < best.Cost || (cost == best.Cost && min > best.Min))) { best = (d, a, cost, min); met = true; }
                else if (!ok && !met && min > best.Min) best = (d, a, cost, min);
            }
        return s with { CompletedFill = best.Done, ActiveFill = best.Target };
    }

    private static double MinDelta(Rgba done, Rgba target, Rgba avail, Rgba locked)
    {
        double m0 = Math.Min(ThemeColor.DeltaE(done, target), ThemeColor.DeltaE(done, avail));
        double m1 = Math.Min(ThemeColor.DeltaE(done, locked), ThemeColor.DeltaE(target, avail));
        double m2 = Math.Min(ThemeColor.DeltaE(target, locked), ThemeColor.DeltaE(avail, locked));
        return Math.Min(m0, Math.Min(m1, m2));
    }

    // ======================================================================== the nine eras

    /// <summary>A1 — charcoal and ochre on stone: rough hand-cut slabs, daubed marks, tally notches,
    /// heavy Garamond, low density. "The civilization has barely developed organized knowledge."</summary>
    private static EraTheme Prehistoric()
    {
        var m = new MaterialTokens(MaterialKind.Stone,
            Field: Hex(0xAC9A80), FieldAlt: Hex(0xA28F74), Panel: Hex(0xD4C6AB), PanelRaised: Hex(0xDED2BB), PanelSunken: Hex(0xAA987D),
            Chrome: Hex(0x3F3128), Border: Hex(0x2A221C), BorderStrong: Hex(0x1E1813), Hairline: Hex(0x7E6E58),
            Accent: Hex(0x93401F), AccentSoft: Hex(0xB98337),
            Grain: Hex(0x4B3F33), GrainDensity: 18.0, GrainAlpha: 0.34, GrainSize: 3.8);
        var ink = new InkTokens(Text: Hex(0x241E19), TextSoft: Hex(0x45392E), TextDim: Hex(0x66594A), OnAccent: Hex(0xF3EBDC), Rule: Hex(0x2A221C),
            OnChrome: Hex(0xEADDC6), OnChromeSoft: Hex(0xB9A88E), OnChromeAccent: Hex(0xE0A458));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 700, 0.005, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 700, 0.01, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 700, 0.03, TextCase.Upper),
            SizeScale: 1.06, LineHeight: 1.36);
        return new EraTheme(UiEra.Prehistoric, "Stone and ochre", "charcoal and ochre on stone",
            m, ink, Semantics(m, new Pigment(0.60, 0.02)),
            new EdgeTokens(Roughness: 1.0, JitterPx: 4.0, Corner: CornerStyle.Organic, CornerPx: 15, BorderPx: 2.8, DoubleRule: false, Fasteners: false),
            new OrnamentTokens(0, Motif.None), type,
            new DensityTokens(Level: 1, Padding: 20, Gap: 12, CardDetail: 1),
            new ControlTokens(ControlGranularity.Coarse, ProgressStyle.Notches, ProgressSegments: 10, StrokePx: 2.4, GrabPx: 16),
            new IconTokens(IconStyle.Daubed, StrokePx: 2.4, WobblePx: 1.3, Filled: true),
            new ChartTokens(Sophistication: 0, GridLines: false, Ticks: false, Labels: false, LineWidth: 2.4));
    }

    /// <summary>A2 — incised clay and woven reed: smoothed rounded tablets, a woven band, clay-token
    /// counters, the first agricultural ornament; cleaner nodes, a little more density.</summary>
    private static EraTheme Neolithic()
    {
        var m = new MaterialTokens(MaterialKind.Clay,
            Field: Hex(0xBE9C72), FieldAlt: Hex(0xB59168), Panel: Hex(0xDFC8A2), PanelRaised: Hex(0xE8D6B6), PanelSunken: Hex(0xB99871),
            Chrome: Hex(0x553A26), Border: Hex(0x4A3120), BorderStrong: Hex(0x352216), Hairline: Hex(0x8F7050),
            Accent: Hex(0xA14B27), AccentSoft: Hex(0xBC9A45),
            Grain: Hex(0x6C4A2C), GrainDensity: 11.0, GrainAlpha: 0.22, GrainSize: 1.9);
        var ink = new InkTokens(Text: Hex(0x2B1E14), TextSoft: Hex(0x4D3825), TextDim: Hex(0x6E5841), OnAccent: Hex(0xF6EBD8), Rule: Hex(0x4A3120),
            OnChrome: Hex(0xF0E1C6), OnChromeSoft: Hex(0xC8AE88), OnChromeAccent: Hex(0xE2B865));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 650, 0.01, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 650, 0.02, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 650, 0.05, TextCase.Upper),
            SizeScale: 1.04, LineHeight: 1.32);
        return new EraTheme(UiEra.Neolithic, "Clay and reed", "incised clay and woven reed",
            m, ink, Semantics(m, new Pigment(0.68, 0.01)),
            new EdgeTokens(Roughness: 0.5, JitterPx: 1.8, Corner: CornerStyle.Rounded, CornerPx: 11, BorderPx: 2.1, DoubleRule: false, Fasteners: false),
            new OrnamentTokens(1, Motif.Weave), type,
            new DensityTokens(Level: 2, Padding: 18, Gap: 11, CardDetail: 2),
            new ControlTokens(ControlGranularity.Simple, ProgressStyle.Notches, ProgressSegments: 10, StrokePx: 2.0, GrabPx: 14),
            new IconTokens(IconStyle.Incised, StrokePx: 2.0, WobblePx: 0.6, Filled: false),
            new ChartTokens(Sophistication: 1, GridLines: false, Ticks: false, Labels: false, LineWidth: 2.0));
    }

    /// <summary>A3 — cast bronze on sandstone: chamfered plaques with studs, a chevron band,
    /// segmented progress, the first inscriptional capitals; geometric organisation.</summary>
    private static EraTheme Bronze()
    {
        var m = new MaterialTokens(MaterialKind.Bronze,
            Field: Hex(0xC4B08A), FieldAlt: Hex(0xBBA67F), Panel: Hex(0xE2D3B1), PanelRaised: Hex(0xEADDC0), PanelSunken: Hex(0xB9A47F),
            Chrome: Hex(0x573C1A), Border: Hex(0x6E4A1F), BorderStrong: Hex(0x553714), Hairline: Hex(0x98805A),
            Accent: Hex(0x9C6B2B), AccentSoft: Hex(0x4C7A68),
            Grain: Hex(0x75572F), GrainDensity: 6.0, GrainAlpha: 0.18, GrainSize: 3.4);
        var ink = new InkTokens(Text: Hex(0x281D11), TextSoft: Hex(0x4A3A24), TextDim: Hex(0x6C5B42), OnAccent: Hex(0xF7EEDA), Rule: Hex(0x6E4A1F),
            OnChrome: Hex(0xF5E7C6), OnChromeSoft: Hex(0xD3B884), OnChromeAccent: Hex(0xEFC77A));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.015, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 600, 0.06, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 600, 0.07, TextCase.Upper),
            SizeScale: 1.02, LineHeight: 1.29);
        return new EraTheme(UiEra.Bronze, "Cast bronze", "cast bronze on sandstone",
            m, ink, Semantics(m, new Pigment(0.76, 0.0)),
            new EdgeTokens(Roughness: 0.18, JitterPx: 0.5, Corner: CornerStyle.Chamfered, CornerPx: 10, BorderPx: 2.0, DoubleRule: true, Fasteners: true),
            new OrnamentTokens(2, Motif.Chevron), type,
            new DensityTokens(Level: 2, Padding: 17, Gap: 10, CardDetail: 3),
            new ControlTokens(ControlGranularity.Simple, ProgressStyle.Segments, ProgressSegments: 5, StrokePx: 1.8, GrabPx: 13),
            new IconTokens(IconStyle.Cast, StrokePx: 1.8, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 1, GridLines: false, Ticks: true, Labels: false, LineWidth: 1.8));
    }

    /// <summary>A4 — forged iron on lime plaster: heavy square frames, rivets, a riveted bar, bolder
    /// structure; the more formal military and engineering presentation.</summary>
    private static EraTheme Iron()
    {
        var m = new MaterialTokens(MaterialKind.Iron,
            Field: Hex(0xB9B8B2), FieldAlt: Hex(0xB0AFA9), Panel: Hex(0xDAD8D2), PanelRaised: Hex(0xE4E2DD), PanelSunken: Hex(0xADABA4),
            Chrome: Hex(0x303337), Border: Hex(0x26282B), BorderStrong: Hex(0x18191B), Hairline: Hex(0x84827C),
            Accent: Hex(0xAE532C), AccentSoft: Hex(0x66707A),
            Grain: Hex(0x3C3E42), GrainDensity: 5.0, GrainAlpha: 0.13, GrainSize: 4.2);
        var ink = new InkTokens(Text: Hex(0x1C1D1F), TextSoft: Hex(0x3B3D41), TextDim: Hex(0x5E5E5B), OnAccent: Hex(0xF2EEE6), Rule: Hex(0x26282B),
            OnChrome: Hex(0xE8E5DE), OnChromeSoft: Hex(0xA9ACB0), OnChromeAccent: Hex(0xEE9A60));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 650, 0.01, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 700, 0.06, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 700, 0.06, TextCase.Upper),
            SizeScale: 1.0, LineHeight: 1.27);
        return new EraTheme(UiEra.Iron, "Forged iron", "forged iron on lime plaster",
            m, ink, Semantics(m, new Pigment(0.64, -0.01)),
            new EdgeTokens(Roughness: 0.06, JitterPx: 0.2, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 3.4, DoubleRule: false, Fasteners: true),
            new OrnamentTokens(2, Motif.Rivet), type,
            new DensityTokens(Level: 3, Padding: 16, Gap: 9, CardDetail: 3),
            new ControlTokens(ControlGranularity.Standard, ProgressStyle.Segments, ProgressSegments: 8, StrokePx: 2.0, GrabPx: 12),
            new IconTokens(IconStyle.Forged, StrokePx: 2.0, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 2, GridLines: false, Ticks: true, Labels: false, LineWidth: 1.6));
    }

    /// <summary>A5 — marble and gilt: a highly structured hierarchy, fine double rules, the meander,
    /// inscriptional capitals with wide tracking; administration and scholarship matured.</summary>
    private static EraTheme Classical()
    {
        var m = new MaterialTokens(MaterialKind.Marble,
            Field: Hex(0xD6D1C6), FieldAlt: Hex(0xCDC7BB), Panel: Hex(0xF1EEE7), PanelRaised: Hex(0xF7F5F0), PanelSunken: Hex(0xC7C0B1),
            Chrome: Hex(0x5A2733), Border: Hex(0x5A4636), BorderStrong: Hex(0x42332A), Hairline: Hex(0xA69C8A),
            Accent: Hex(0xA27C33), AccentSoft: Hex(0x6E2C3A),
            Grain: Hex(0x8E8678), GrainDensity: 1.2, GrainAlpha: 0.34, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x24201B), TextSoft: Hex(0x463E37), TextDim: Hex(0x6A6257), OnAccent: Hex(0xF7F2E8), Rule: Hex(0x5A4636),
            OnChrome: Hex(0xF3E9DE), OnChromeSoft: Hex(0xCDAEB0), OnChromeAccent: Hex(0xE6C277));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.02, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 600, 0.10, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 600, 0.10, TextCase.Upper),
            SizeScale: 1.0, LineHeight: 1.25);
        return new EraTheme(UiEra.Classical, "Marble and gilt", "marble and gilt",
            m, ink, Semantics(m, new Pigment(0.80, 0.0)),
            new EdgeTokens(Roughness: 0.0, JitterPx: 0.0, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 1.2, DoubleRule: true, Fasteners: false),
            new OrnamentTokens(3, Motif.Meander), type,
            new DensityTokens(Level: 3, Padding: 15, Gap: 9, CardDetail: 4),
            new ControlTokens(ControlGranularity.Standard, ProgressStyle.Segments, ProgressSegments: 10, StrokePx: 1.4, GrabPx: 12),
            new IconTokens(IconStyle.Carved, StrokePx: 1.5, WobblePx: 0.0, Filled: false),
            new ChartTokens(Sophistication: 2, GridLines: false, Ticks: true, Labels: true, LineWidth: 1.5));
    }

    /// <summary>A6 — vellum and gold leaf: the style bible's parchment palette exactly (the parchment
    /// substrate is the A6-centred material), ruled double borders, illuminated corners, rubrication;
    /// richer iconography, more formal institutional presentation.</summary>
    private static EraTheme Medieval()
    {
        var m = new MaterialTokens(MaterialKind.Vellum,
            Field: ParchmentPalette.PaperMid, FieldAlt: Hex(0xDAC8A1), Panel: ParchmentPalette.PaperLight, PanelRaised: Hex(0xF5ECD8),
            PanelSunken: ParchmentPalette.PaperShade, Chrome: Hex(0x4A3324),
            Border: ParchmentPalette.InkPrimary, BorderStrong: Hex(0x2A2116), Hairline: Hex(0x9A876A),
            Accent: ParchmentPalette.IronRed, AccentSoft: ParchmentPalette.GoldLeaf,
            Grain: ParchmentPalette.InkSoft, GrainDensity: 2.2, GrainAlpha: 0.10, GrainSize: 6.0);
        var ink = new InkTokens(Text: ParchmentPalette.InkPrimary, TextSoft: ParchmentPalette.InkSoft, TextDim: Hex(0x85745A),
            OnAccent: ParchmentPalette.PaperLight, Rule: ParchmentPalette.InkPrimary,
            OnChrome: ParchmentPalette.PaperLight, OnChromeSoft: ParchmentPalette.PaperShade, OnChromeAccent: Hex(0xD9B35E));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.01, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 600, 0.02, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 600, 0.06, TextCase.Upper),
            SizeScale: 1.0, LineHeight: 1.24);
        SemanticTokens s = Semantics(m, new Pigment(0.72, 0.0)) with
        {
            // The bible's accents ARE the parchment era's semantic inks (each inside its family).
            Completed = ParchmentPalette.GoldLeaf,
            Danger = ParchmentPalette.IronRed,
            Positive = ParchmentPalette.Verdigris,
        };
        s = s with { CompletedFill = Mix(m.Panel, s.Completed, 0.24) };
        return new EraTheme(UiEra.Medieval, "Vellum and gold leaf", "vellum and gold leaf",
            m, ink, s,
            new EdgeTokens(Roughness: 0.0, JitterPx: 0.0, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 1.4, DoubleRule: true, Fasteners: false),
            new OrnamentTokens(4, Motif.Illumination), type,
            new DensityTokens(Level: 4, Padding: 14, Gap: 8, CardDetail: 4),
            new ControlTokens(ControlGranularity.Standard, ProgressStyle.Bar, ProgressSegments: 0, StrokePx: 1.3, GrabPx: 11),
            new IconTokens(IconStyle.Illuminated, StrokePx: 1.4, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 3, GridLines: false, Ticks: true, Labels: true, LineWidth: 1.3));
    }

    /// <summary>A7 — print on laid paper: printer's black rules (thick-thin), fleurons, a cartographic
    /// graticule, print Garamond with tracked capitals; systematic, denser research presentation.</summary>
    private static EraTheme EarlyModern()
    {
        var m = new MaterialTokens(MaterialKind.Paper,
            Field: Hex(0xE5DFCD), FieldAlt: Hex(0xDCD5C1), Panel: Hex(0xF4F0E5), PanelRaised: Hex(0xF9F6EE), PanelSunken: Hex(0xCFC7B3),
            Chrome: Hex(0x27231F), Border: Hex(0x1F1B17), BorderStrong: Hex(0x141210), Hairline: Hex(0x9A9282),
            Accent: Hex(0xA23A28), AccentSoft: Hex(0x2F4A6A),
            Grain: Hex(0x9C9380), GrainDensity: 1.0, GrainAlpha: 0.34, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x1E1B18), TextSoft: Hex(0x423D36), TextDim: Hex(0x6C665B), OnAccent: Hex(0xFAF6EC), Rule: Hex(0x1F1B17),
            OnChrome: Hex(0xF4EFE3), OnChromeSoft: Hex(0xB4AC9C), OnChromeAccent: Hex(0xE5806B));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.0, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 400, 0.01, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 600, 0.09, TextCase.Upper),
            SizeScale: 0.98, LineHeight: 1.22);
        return new EraTheme(UiEra.EarlyModern, "Print on laid paper", "print on laid paper",
            m, ink, Semantics(m, new Pigment(0.82, -0.02)),
            new EdgeTokens(Roughness: 0.0, JitterPx: 0.0, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 1.8, DoubleRule: true, Fasteners: false),
            new OrnamentTokens(2, Motif.Fleuron), type,
            new DensityTokens(Level: 4, Padding: 13, Gap: 7, CardDetail: 5),
            new ControlTokens(ControlGranularity.Fine, ProgressStyle.Bar, ProgressSegments: 0, StrokePx: 1.1, GrabPx: 10),
            new IconTokens(IconStyle.Engraved, StrokePx: 1.2, WobblePx: 0.0, Filled: false),
            new ChartTokens(Sophistication: 3, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.2));
    }

    /// <summary>A8 — engineering drawings: a pale drafting ground with a fine grid, Prussian-blue
    /// line work, bolts, dimension lines, technical Plex Sans capitals, graduated progress, gridded
    /// charts; dense and systematic. "This civilization is industrializing."</summary>
    private static EraTheme Industrial()
    {
        var m = new MaterialTokens(MaterialKind.Drafting,
            Field: Hex(0xCEDAE2), FieldAlt: Hex(0xC4D1DA), Panel: Hex(0xEEF3F6), PanelRaised: Hex(0xF6F9FA), PanelSunken: Hex(0xB7C4CD),
            Chrome: Hex(0x1F3550), Border: Hex(0x1F3550), BorderStrong: Hex(0x142640), Hairline: Hex(0x8AA0B2),
            Accent: Hex(0xA9742C), AccentSoft: Hex(0x5B6B78),
            Grain: Hex(0x4F7AA3), GrainDensity: 12.0, GrainAlpha: 0.24, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x16202C), TextSoft: Hex(0x334356), TextDim: Hex(0x586877), OnAccent: Hex(0xF4F7F8), Rule: Hex(0x1F3550),
            OnChrome: Hex(0xE6EEF4), OnChromeSoft: Hex(0x9FB4C6), OnChromeAccent: Hex(0xE8B568));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.PlexSans, 600, 0.005, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.PlexSans, 600, 0.04, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSans, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.PlexSans, 600, 0.12, TextCase.Upper),
            SizeScale: 0.95, LineHeight: 1.18);
        return new EraTheme(UiEra.Industrial, "Engineering drawing", "engineering drawings on drafting film",
            m, ink, Semantics(m, new Pigment(0.92, -0.02)),
            new EdgeTokens(Roughness: 0.0, JitterPx: 0.0, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 1.3, DoubleRule: false, Fasteners: true),
            new OrnamentTokens(1, Motif.Dimension), type,
            new DensityTokens(Level: 5, Padding: 12, Gap: 6, CardDetail: 5),
            new ControlTokens(ControlGranularity.Fine, ProgressStyle.GraduatedBar, ProgressSegments: 10, StrokePx: 1.0, GrabPx: 9),
            new IconTokens(IconStyle.Technical, StrokePx: 1.1, WobblePx: 0.0, Filled: false),
            new ChartTokens(Sophistication: 4, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.0));
    }

    /// <summary>A9 — a modern civilization's interface: clean coated stock, hairline geometry, Plex
    /// Sans, compact precise controls, graduated bars, high density. Warm neutrals and the brass
    /// accent that has run through every Age keep it this civilization's interface rather than a
    /// generic dashboard.</summary>
    private static EraTheme Modern()
    {
        var m = new MaterialTokens(MaterialKind.Glass,
            Field: Hex(0xE4E4E1), FieldAlt: Hex(0xDBDBD7), Panel: Hex(0xF9F9F7), PanelRaised: Hex(0xFDFDFC), PanelSunken: Hex(0xD3D4D1),
            Chrome: Hex(0x23272C), Border: Hex(0x2B3036), BorderStrong: Hex(0x1C2024), Hairline: Hex(0xB2B5B5),
            Accent: Hex(0xA06A26), AccentSoft: Hex(0x4A5056),
            Grain: Hex(0xB2B5B5), GrainDensity: 0.0, GrainAlpha: 0.0, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x1B1F23), TextSoft: Hex(0x3B4249), TextDim: Hex(0x646B71), OnAccent: Hex(0xFFFFFF), Rule: Hex(0x2B3036),
            OnChrome: Hex(0xF1F1EF), OnChromeSoft: Hex(0xA5ABB1), OnChromeAccent: Hex(0xE3A75B));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.PlexSans, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.PlexSans, 600, 0.0, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.PlexSans, 400, 0.0, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSans, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.PlexSans, 600, 0.08, TextCase.Upper),
            SizeScale: 0.94, LineHeight: 1.16);
        return new EraTheme(UiEra.Modern, "Glass and graphite", "glass and graphite",
            m, ink, Semantics(m, new Pigment(1.0, -0.01)),
            new EdgeTokens(Roughness: 0.0, JitterPx: 0.0, Corner: CornerStyle.Fine, CornerPx: 3, BorderPx: 1.0, DoubleRule: false, Fasteners: false),
            new OrnamentTokens(0, Motif.Hairline), type,
            new DensityTokens(Level: 5, Padding: 11, Gap: 6, CardDetail: 5),
            new ControlTokens(ControlGranularity.Precise, ProgressStyle.GraduatedBar, ProgressSegments: 20, StrokePx: 1.0, GrabPx: 8),
            new IconTokens(IconStyle.Precise, StrokePx: 1.0, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 4, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.0));
    }

    // ======================================================================== shared derivations

    /// <summary>An era's pigment: how saturated its inks are and how far their lightness moves —
    /// the ONLY thing an era changes about a semantic colour; the hue is <see cref="SemanticFamilies"/>'.</summary>
    private readonly record struct Pigment(double Saturation, double LightShift);

    private static SemanticTokens Semantics(MaterialTokens m, Pigment p)
    {
        Rgba S(SemanticFamily f) => FromHsl(f.Hue, f.Saturation * p.Saturation, f.Lightness + p.LightShift);
        Rgba available = S(SemanticFamilies.Available), active = S(SemanticFamilies.Active),
             completed = S(SemanticFamilies.Completed), locked = S(SemanticFamilies.Locked);
        var lanes = new LaneHues(
            Main: Lane(42, 0.40, 0.40, p), Military: Lane(4, 0.50, 0.40, p), Medicine: Lane(148, 0.38, 0.33, p),
            Engineering: Lane(28, 0.58, 0.40, p), NaturalScience: Lane(214, 0.46, 0.42, p), Agriculture: Lane(80, 0.46, 0.34, p),
            External: FromHsl(215, 0.06, 0.48 + p.LightShift), Civics: Lane(276, 0.30, 0.45, p));
        return new SemanticTokens(
            Available: available, Active: active, Completed: completed, Locked: locked,
            Progress: S(SemanticFamilies.Progress), Danger: S(SemanticFamilies.Danger), Positive: S(SemanticFamilies.Positive),
            Food: S(SemanticFamilies.Food), Knowledge: S(SemanticFamilies.Knowledge), Military: S(SemanticFamilies.Military),
            Infrastructure: S(SemanticFamilies.Infrastructure),
            Prerequisite: S(SemanticFamilies.Prerequisite), Dependent: S(SemanticFamilies.Dependent),
            AvailableFill: AvailableFillOf(m, available), ActiveFill: Mix(m.Panel, active, 0.16),
            CompletedFill: Mix(m.Panel, completed, 0.24), LockedFill: LockedFillOf(m, AvailableFillOf(m, available)),
            Lanes: lanes);
    }

    /// <summary>The minimum lightness ratio between the Available and Locked card fills (UR-1): state is carried by
    /// SURFACE, not only by a hairline (they were 1.02–1.14:1 apart).</summary>
    public const double StateFillSeparation = 1.4;

    /// <summary>An available card: the raised surface with a breath of the Available hue.</summary>
    private static Rgba AvailableFillOf(MaterialTokens m, Rgba available) => Mix(m.PanelRaised, available, 0.08);

    /// <summary>A locked card: the panel sunk toward the well (and, where the era's well is too pale, on toward its
    /// border) just far enough to sit <see cref="StateFillSeparation"/> below the available fill.</summary>
    private static Rgba LockedFillOf(MaterialTokens m, Rgba availableFill)
    {
        for (int k = 45; k <= 100; k++)
        {
            Rgba f = Mix(m.Panel, m.PanelSunken, k / 100.0);
            if (Contrast(availableFill, f) >= StateFillSeparation) return f;
        }
        for (int k = 1; k <= 60; k++)
        {
            Rgba f = Mix(m.PanelSunken, m.Border, k / 100.0);
            if (Contrast(availableFill, f) >= StateFillSeparation) return f;
        }
        return Mix(m.PanelSunken, m.Border, 0.6);
    }

    private static Rgba Lane(double hue, double s, double l, Pigment p) => FromHsl(hue, s * p.Saturation, l + p.LightShift);
}

/// <summary>One semantic token's fixed identity: its hue (degrees) and the base saturation and
/// lightness an era's pigment scales; <c>Lo</c>..<c>Hi</c> is the hue family every era must stay
/// inside (wrapping through 0° when Lo &gt; Hi). <c>Neutral</c> families are judged by saturation
/// instead (a grey has no hue).</summary>
public readonly record struct SemanticFamily(string Name, double Hue, double Saturation, double Lightness, double Lo, double Hi, bool Neutral = false)
{
    /// <summary>Whether a hue lies in this family's range.</summary>
    public bool Contains(double hue) => Lo <= Hi ? hue >= Lo && hue <= Hi : hue >= Lo || hue <= Hi;
}

/// <summary>
/// THE SEMANTIC HUE FAMILIES — the persistent colour DNA across all nine eras (continuity
/// requirement). Every era's semantic token is generated from the family's hue; the tests pin that
/// every era's token lies inside its family.
/// </summary>
public static class SemanticFamilies
{
    public static readonly SemanticFamily Available = new("available", 210, 0.40, 0.45, 196, 222);
    public static readonly SemanticFamily Active = new("active", 181, 0.70, 0.29, 170, 198);
    public static readonly SemanticFamily Completed = new("completed", 42, 0.72, 0.40, 32, 54);
    public static readonly SemanticFamily Locked = new("locked", 36, 0.07, 0.46, 0, 360, Neutral: true);
    public static readonly SemanticFamily Progress = new("progress", 30, 0.70, 0.40, 20, 40);
    public static readonly SemanticFamily Danger = new("danger", 4, 0.62, 0.36, 348, 18);
    public static readonly SemanticFamily Positive = new("positive", 128, 0.38, 0.32, 105, 155);
    public static readonly SemanticFamily Food = new("food", 72, 0.48, 0.34, 55, 90);
    public static readonly SemanticFamily Knowledge = new("knowledge", 218, 0.50, 0.38, 204, 232);
    public static readonly SemanticFamily Military = new("military", 358, 0.52, 0.31, 345, 15);
    public static readonly SemanticFamily Infrastructure = new("infrastructure", 22, 0.50, 0.33, 12, 34);
    public static readonly SemanticFamily Prerequisite = new("prerequisite", 24, 0.80, 0.42, 14, 36);
    public static readonly SemanticFamily Dependent = new("dependent", 205, 0.62, 0.40, 192, 220);

    /// <summary>The highest saturation a neutral (locked) token may have.</summary>
    public const double NeutralMaxSaturation = 0.16;

    /// <summary>Every family with the token it governs, in a fixed order.</summary>
    public static IReadOnlyList<(SemanticFamily Family, Func<SemanticTokens, Rgba> Token)> All { get; } =
    [
        (Available, s => s.Available), (Active, s => s.Active), (Completed, s => s.Completed), (Locked, s => s.Locked),
        (Progress, s => s.Progress), (Danger, s => s.Danger), (Positive, s => s.Positive), (Food, s => s.Food),
        (Knowledge, s => s.Knowledge), (Military, s => s.Military), (Infrastructure, s => s.Infrastructure),
        (Prerequisite, s => s.Prerequisite), (Dependent, s => s.Dependent),
    ];
}
