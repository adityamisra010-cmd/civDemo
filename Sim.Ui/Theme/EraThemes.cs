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
    public static EraTheme Build(UiEra era) => UiEras.FromAge((int)era) switch
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
    };

    // ======================================================================== the nine eras

    /// <summary>A1 — charcoal and ochre on stone: rough hand-cut slabs, daubed marks, tally notches,
    /// heavy Garamond, low density. "The civilization has barely developed organized knowledge."</summary>
    private static EraTheme Prehistoric()
    {
        var m = new MaterialTokens(MaterialKind.Stone,
            Field: Hex(0xC4B69C), FieldAlt: Hex(0xBBAC91), Panel: Hex(0xD5C9B1), PanelRaised: Hex(0xDFD5C2), PanelSunken: Hex(0xAE9F85),
            Chrome: Hex(0xB7A88D), Border: Hex(0x3A322A), BorderStrong: Hex(0x29231D), Hairline: Hex(0x8D7F69),
            Accent: Hex(0x9A4529), AccentSoft: Hex(0xB98A41),
            Grain: Hex(0x5E5142), GrainDensity: 9.0, GrainAlpha: 0.24, GrainSize: 2.6);
        var ink = new InkTokens(Text: Hex(0x29231E), TextSoft: Hex(0x4B4136), TextDim: Hex(0x6E6252), OnAccent: Hex(0xF3EBDC), Rule: Hex(0x3A322A));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 500, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 700, 0.005, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 700, 0.01, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 700, 0.03, TextCase.Upper),
            SizeScale: 1.06, LineHeight: 1.36);
        return new EraTheme(UiEra.Prehistoric, "Stone and ochre", "charcoal and ochre on stone",
            m, ink, Semantics(m, new Pigment(0.60, 0.02)),
            new EdgeTokens(Roughness: 1.0, JitterPx: 3.0, Corner: CornerStyle.Organic, CornerPx: 9, BorderPx: 2.2, DoubleRule: false, Fasteners: false),
            new OrnamentTokens(0, Motif.None), type,
            new DensityTokens(Level: 1, Padding: 20, Gap: 12, CardDetail: 1),
            new ControlTokens(ControlGranularity.Coarse, ProgressStyle.Notches, ProgressSegments: 10, StrokePx: 2.4, GrabPx: 16),
            new IconTokens(IconStyle.Daubed, StrokePx: 2.4, WobblePx: 1.3, Filled: true),
            new ChartTokens(Sophistication: 0, GridLines: false, Ticks: false, Labels: false, LineWidth: 2.4),
            Map(ink, m, UiEra.Prehistoric));
    }

    /// <summary>A2 — incised clay and woven reed: smoothed rounded tablets, a woven band, clay-token
    /// counters, the first agricultural ornament; cleaner nodes, a little more density.</summary>
    private static EraTheme Neolithic()
    {
        var m = new MaterialTokens(MaterialKind.Clay,
            Field: Hex(0xCDB591), FieldAlt: Hex(0xC4AA84), Panel: Hex(0xDDC8A3), PanelRaised: Hex(0xE7D6B5), PanelSunken: Hex(0xB89D75),
            Chrome: Hex(0xC2A479), Border: Hex(0x4A3320), BorderStrong: Hex(0x35241A), Hairline: Hex(0x977A55),
            Accent: Hex(0xA5512D), AccentSoft: Hex(0xB89A48),
            Grain: Hex(0x7A5A3A), GrainDensity: 7.0, GrainAlpha: 0.16, GrainSize: 1.7);
        var ink = new InkTokens(Text: Hex(0x2E2117), TextSoft: Hex(0x503B29), TextDim: Hex(0x75604A), OnAccent: Hex(0xF6EBD8), Rule: Hex(0x4A3320));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 500, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 650, 0.01, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 650, 0.02, TextCase.AsWritten),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 650, 0.05, TextCase.Upper),
            SizeScale: 1.04, LineHeight: 1.32);
        return new EraTheme(UiEra.Neolithic, "Clay and reed", "incised clay and woven reed",
            m, ink, Semantics(m, new Pigment(0.68, 0.01)),
            new EdgeTokens(Roughness: 0.5, JitterPx: 1.4, Corner: CornerStyle.Rounded, CornerPx: 9, BorderPx: 1.9, DoubleRule: false, Fasteners: false),
            new OrnamentTokens(1, Motif.Weave), type,
            new DensityTokens(Level: 2, Padding: 18, Gap: 11, CardDetail: 2),
            new ControlTokens(ControlGranularity.Simple, ProgressStyle.Notches, ProgressSegments: 10, StrokePx: 2.0, GrabPx: 14),
            new IconTokens(IconStyle.Incised, StrokePx: 2.0, WobblePx: 0.6, Filled: false),
            new ChartTokens(Sophistication: 1, GridLines: false, Ticks: false, Labels: false, LineWidth: 2.0),
            Map(ink, m, UiEra.Neolithic));
    }

    /// <summary>A3 — cast bronze on sandstone: chamfered plaques with studs, a chevron band,
    /// segmented progress, the first inscriptional capitals; geometric organisation.</summary>
    private static EraTheme Bronze()
    {
        var m = new MaterialTokens(MaterialKind.Bronze,
            Field: Hex(0xCDBE9D), FieldAlt: Hex(0xC4B391), Panel: Hex(0xDDCEAC), PanelRaised: Hex(0xE7DABD), PanelSunken: Hex(0xB4A27F),
            Chrome: Hex(0xC8AC78), Border: Hex(0x7A5426), BorderStrong: Hex(0x5C3D18), Hairline: Hex(0x9C8460),
            Accent: Hex(0x996628), AccentSoft: Hex(0x4F7E6C),
            Grain: Hex(0x7A6040), GrainDensity: 4.0, GrainAlpha: 0.13, GrainSize: 3.2);
        var ink = new InkTokens(Text: Hex(0x2A2014), TextSoft: Hex(0x4C3C27), TextDim: Hex(0x72624A), OnAccent: Hex(0xF7EEDA), Rule: Hex(0x6A4A22));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 450, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.015, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 600, 0.06, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 600, 0.07, TextCase.Upper),
            SizeScale: 1.02, LineHeight: 1.29);
        return new EraTheme(UiEra.Bronze, "Cast bronze", "cast bronze on sandstone",
            m, ink, Semantics(m, new Pigment(0.76, 0.0)),
            new EdgeTokens(Roughness: 0.18, JitterPx: 0.5, Corner: CornerStyle.Chamfered, CornerPx: 8, BorderPx: 1.8, DoubleRule: true, Fasteners: true),
            new OrnamentTokens(2, Motif.Chevron), type,
            new DensityTokens(Level: 2, Padding: 17, Gap: 10, CardDetail: 3),
            new ControlTokens(ControlGranularity.Simple, ProgressStyle.Segments, ProgressSegments: 5, StrokePx: 1.8, GrabPx: 13),
            new IconTokens(IconStyle.Cast, StrokePx: 1.8, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 1, GridLines: false, Ticks: true, Labels: false, LineWidth: 1.8),
            Map(ink, m, UiEra.Bronze));
    }

    /// <summary>A4 — forged iron on lime plaster: heavy square frames, rivets, a riveted bar, bolder
    /// structure; the more formal military and engineering presentation.</summary>
    private static EraTheme Iron()
    {
        var m = new MaterialTokens(MaterialKind.Iron,
            Field: Hex(0xC5C1B7), FieldAlt: Hex(0xBCB8AD), Panel: Hex(0xD6D2C8), PanelRaised: Hex(0xE0DDD5), PanelSunken: Hex(0xACA79D),
            Chrome: Hex(0xA9A59C), Border: Hex(0x2F3134), BorderStrong: Hex(0x1E1F21), Hairline: Hex(0x85817A),
            Accent: Hex(0xAE532C), AccentSoft: Hex(0x5A6068),
            Grain: Hex(0x4A4C50), GrainDensity: 3.2, GrainAlpha: 0.10, GrainSize: 4.0);
        var ink = new InkTokens(Text: Hex(0x1E1F21), TextSoft: Hex(0x3D3F43), TextDim: Hex(0x64635F), OnAccent: Hex(0xF2EEE6), Rule: Hex(0x2F3134));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 450, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 650, 0.01, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 700, 0.06, TextCase.Upper),
            Numeric: new TextStyle(TypeFace.PlexSerif, 400, 0.0, TextCase.AsWritten),
            Caps: new TextStyle(TypeFace.Garamond, 700, 0.06, TextCase.Upper),
            SizeScale: 1.0, LineHeight: 1.27);
        return new EraTheme(UiEra.Iron, "Forged iron", "forged iron on lime plaster",
            m, ink, Semantics(m, new Pigment(0.64, -0.01)),
            new EdgeTokens(Roughness: 0.06, JitterPx: 0.2, Corner: CornerStyle.Square, CornerPx: 0, BorderPx: 3.0, DoubleRule: false, Fasteners: true),
            new OrnamentTokens(2, Motif.Rivet), type,
            new DensityTokens(Level: 3, Padding: 16, Gap: 9, CardDetail: 3),
            new ControlTokens(ControlGranularity.Standard, ProgressStyle.Segments, ProgressSegments: 8, StrokePx: 2.0, GrabPx: 12),
            new IconTokens(IconStyle.Forged, StrokePx: 2.0, WobblePx: 0.0, Filled: true),
            new ChartTokens(Sophistication: 2, GridLines: false, Ticks: true, Labels: false, LineWidth: 1.6),
            Map(ink, m, UiEra.Iron));
    }

    /// <summary>A5 — marble and gilt: a highly structured hierarchy, fine double rules, the meander,
    /// inscriptional capitals with wide tracking; administration and scholarship matured.</summary>
    private static EraTheme Classical()
    {
        var m = new MaterialTokens(MaterialKind.Marble,
            Field: Hex(0xDCD6CA), FieldAlt: Hex(0xD3CCBF), Panel: Hex(0xECE8DF), PanelRaised: Hex(0xF4F1EA), PanelSunken: Hex(0xC9C1B2),
            Chrome: Hex(0xD9D0BE), Border: Hex(0x5E4A38), BorderStrong: Hex(0x45362A), Hairline: Hex(0xA89E8C),
            Accent: Hex(0xA8833A), AccentSoft: Hex(0x6E2C3A),
            Grain: Hex(0x9C9588), GrainDensity: 0.9, GrainAlpha: 0.30, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x26211C), TextSoft: Hex(0x48403A), TextDim: Hex(0x6F675C), OnAccent: Hex(0xF7F2E8), Rule: Hex(0x5A4A3A));
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
            new ChartTokens(Sophistication: 2, GridLines: false, Ticks: true, Labels: true, LineWidth: 1.5),
            Map(ink, m, UiEra.Classical));
    }

    /// <summary>A6 — vellum and gold leaf: the style bible's parchment palette exactly (the parchment
    /// substrate is the A6-centred material), ruled double borders, illuminated corners, rubrication;
    /// richer iconography, more formal institutional presentation.</summary>
    private static EraTheme Medieval()
    {
        var m = new MaterialTokens(MaterialKind.Vellum,
            Field: ParchmentPalette.PaperMid, FieldAlt: Hex(0xDAC8A1), Panel: ParchmentPalette.PaperLight, PanelRaised: Hex(0xF5ECD8),
            PanelSunken: ParchmentPalette.PaperShade, Chrome: Hex(0xD8C69C),
            Border: ParchmentPalette.InkPrimary, BorderStrong: Hex(0x2A2116), Hairline: Hex(0x9A876A),
            Accent: ParchmentPalette.IronRed, AccentSoft: ParchmentPalette.GoldLeaf,
            Grain: ParchmentPalette.InkSoft, GrainDensity: 2.2, GrainAlpha: 0.10, GrainSize: 6.0);
        var ink = new InkTokens(Text: ParchmentPalette.InkPrimary, TextSoft: ParchmentPalette.InkSoft, TextDim: Hex(0x85745A),
            OnAccent: ParchmentPalette.PaperLight, Rule: ParchmentPalette.InkPrimary);
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
            new ChartTokens(Sophistication: 3, GridLines: false, Ticks: true, Labels: true, LineWidth: 1.3),
            Map(ink, m, UiEra.Medieval));
    }

    /// <summary>A7 — print on laid paper: printer's black rules (thick-thin), fleurons, a cartographic
    /// graticule, print Garamond with tracked capitals; systematic, denser research presentation.</summary>
    private static EraTheme EarlyModern()
    {
        var m = new MaterialTokens(MaterialKind.Paper,
            Field: Hex(0xE6E0CF), FieldAlt: Hex(0xDDD6C3), Panel: Hex(0xF2EEE2), PanelRaised: Hex(0xF8F5EC), PanelSunken: Hex(0xCFC7B3),
            Chrome: Hex(0xE0D9C6), Border: Hex(0x1F1B17), BorderStrong: Hex(0x141210), Hairline: Hex(0x9A9282),
            Accent: Hex(0xA23A28), AccentSoft: Hex(0x2F4A6A),
            Grain: Hex(0xA79E89), GrainDensity: 1.0, GrainAlpha: 0.30, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x1E1B18), TextSoft: Hex(0x423D36), TextDim: Hex(0x6C665B), OnAccent: Hex(0xFAF6EC), Rule: Hex(0x1F1B17));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.Garamond, 600, 0.0, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.Garamond, 500, 0.01, TextCase.AsWritten),
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
            new ChartTokens(Sophistication: 3, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.2),
            Map(ink, m, UiEra.EarlyModern));
    }

    /// <summary>A8 — engineering drawings: a pale drafting ground with a fine grid, Prussian-blue
    /// line work, bolts, dimension lines, technical Plex Sans capitals, graduated progress, gridded
    /// charts; dense and systematic. "This civilization is industrializing."</summary>
    private static EraTheme Industrial()
    {
        var m = new MaterialTokens(MaterialKind.Drafting,
            Field: Hex(0xD6DDE0), FieldAlt: Hex(0xCDD5D9), Panel: Hex(0xEAEFF1), PanelRaised: Hex(0xF3F6F7), PanelSunken: Hex(0xBAC4C9),
            Chrome: Hex(0xC4CDD2), Border: Hex(0x1F3550), BorderStrong: Hex(0x162740), Hairline: Hex(0x8DA0AE),
            Accent: Hex(0xA9742C), AccentSoft: Hex(0x5B6B78),
            Grain: Hex(0x6C8CA8), GrainDensity: 12.0, GrainAlpha: 0.20, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x18222E), TextSoft: Hex(0x354558), TextDim: Hex(0x5C6C7A), OnAccent: Hex(0xF4F7F8), Rule: Hex(0x1F3550));
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
            new ChartTokens(Sophistication: 4, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.0),
            Map(ink, m, UiEra.Industrial));
    }

    /// <summary>A9 — a modern civilization's interface: clean coated stock, hairline geometry, Plex
    /// Sans, compact precise controls, graduated bars, high density. Warm neutrals and the brass
    /// accent that has run through every Age keep it this civilization's interface rather than a
    /// generic dashboard.</summary>
    private static EraTheme Modern()
    {
        var m = new MaterialTokens(MaterialKind.Glass,
            Field: Hex(0xE5E5E2), FieldAlt: Hex(0xDCDCD8), Panel: Hex(0xF7F7F4), PanelRaised: Hex(0xFCFCFA), PanelSunken: Hex(0xD3D4D1),
            Chrome: Hex(0xEDEDEA), Border: Hex(0x2B3036), BorderStrong: Hex(0x1C2024), Hairline: Hex(0xB2B5B5),
            Accent: Hex(0xA06A26), AccentSoft: Hex(0x4A5056),
            Grain: Hex(0xB2B5B5), GrainDensity: 0.0, GrainAlpha: 0.0, GrainSize: 1.0);
        var ink = new InkTokens(Text: Hex(0x1B1F23), TextSoft: Hex(0x3B4249), TextDim: Hex(0x646B71), OnAccent: Hex(0xFFFFFF), Rule: Hex(0x2B3036));
        var type = new TypographyTokens(
            Body: new TextStyle(TypeFace.PlexSans, 400, 0.0, TextCase.AsWritten),
            Heading: new TextStyle(TypeFace.PlexSans, 600, 0.0, TextCase.AsWritten),
            Title: new TextStyle(TypeFace.PlexSans, 500, 0.0, TextCase.AsWritten),
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
            new ChartTokens(Sophistication: 4, GridLines: true, Ticks: true, Labels: true, LineWidth: 1.0),
            Map(ink, m, UiEra.Modern));
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
            AvailableFill: Mix(m.Panel, available, 0.10), ActiveFill: Mix(m.Panel, active, 0.16),
            CompletedFill: Mix(m.Panel, completed, 0.24), LockedFill: Mix(m.Panel, m.PanelSunken, 0.45),
            Lanes: lanes);
    }

    private static Rgba Lane(double hue, double s, double l, Pigment p) => FromHsl(hue, s * p.Saturation, l + p.LightShift);

    /// <summary>The map inks: today's WorldLens literals, constant identity inks in every era, and the
    /// neutral inks following the era (exactly the literals at A6, the parchment era).</summary>
    private static MapInkTokens Map(InkTokens ink, MaterialTokens m, UiEra era)
    {
        bool parchment = era == UiEra.Medieval;
        Rgba mapInk = parchment ? Hex(0x3A2E1F) : ink.Text;
        return new MapInkTokens(
            Ink: mapInk,
            InkDark: parchment ? Hex(0x1E1810) : Mix(mapInk, Hex(0x000000), 0.35),
            Pale: Hex(0xF4EBD3), CapitalStar: Hex(0xFFF1C4), Selection: Hex(0xFFD25A),
            RoadCasing: Hex(0xF2E6C8), Road: Hex(0x6B4A2A), Footprint: Hex(0xEADBB8), Palisade: Hex(0x6B4A2A),
            Hut: Hex(0x8F6B4E), House: Hex(0x8C4A3A),
            Granary: Hex(0xC9A35A), Workshop: Hex(0xB06A44), StructureOther: Hex(0x9A8F7A), MedicalMark: Hex(0x9B2B2B),
            BannerText: Hex(0xF4EBD3), BannerTextCapital: Hex(0xFFF1C4),
            LegendPaper: parchment ? Hex(0xF4EBD3) : Mix(m.Panel, Hex(0xFFFFFF), 0.25),
            LegendInk: parchment ? Hex(0x6B5A3E) : ink.TextSoft,
            LegendWarn: Hex(0x8C4A3A),
            Polities: new PolityInks(Hex(0xC8962E), Hex(0x777066), Hex(0x9B3B3B), Hex(0x3F6E8C), Hex(0x5C7F45), Hex(0x7A4F8C), Hex(0x2F7C74), Hex(0x8C6A3F)),
            Sectors: new SectorInks(Hex(0x8FA34E), Hex(0xC2A060), Hex(0x8A8A86), Hex(0xC07048), Hex(0x6C8FB0)),
            Universities: new UniversityInks(Hex(0x8E3B32), Hex(0xE8E2D0), Hex(0x4F6F8F), Hex(0x3E7A6A), Hex(0x9AA344), Hex(0x9A8F7A)));
    }
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
