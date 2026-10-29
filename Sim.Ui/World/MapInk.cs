using Sim.Ui.Art;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>
/// THE MAP'S INK — the ONE place every colour the world lens draws is defined (<c>Sim.Ui/World/*</c>
/// holds no other colour literal; a source guard pins that). Each token names a ROLE on the map, not
/// a hue, so a presentation layer can restyle the map without touching the drawing code.
/// <para><see cref="Default"/> is today's single look: every pre-existing token keeps the exact value
/// the lens drew before the tokens existed, so every existing renderer pin holds. The road-class tokens
/// are new; they are taken from the style bible's §2 palette (warm, desaturated; bible hex named in each
/// comment).</para>
/// <para>ERA SEAM (ADR-033 D8): this is the ONE complete map-ink record — the era theme carries no map
/// INK tokens of its own. <see cref="For"/> derives the era's ink from the theme the interface paints with,
/// and the game (and the headless previews) pass it to <c>WorldLens.Paint(…, ink:)</c>; the map's
/// composition stays a read of state, only its ink changes. Nothing here reads the Age itself.</para>
/// <para>THE HAND (packet P13): the record also carries the era's <see cref="Hand"/> — stroke weight, plate cut,
/// banner fly and name-plate ornament, no colour — taken from the theme by <see cref="For"/> like the ink, so the
/// lens's painters switch on these tokens and never on an era number.</para>
/// </summary>
public sealed record MapInk
{
    /// <summary>Today's map: the exact colours the lens has always drawn — and the parchment era's
    /// (A6, the style bible's palette) ink exactly: <c>For(A6) == Default</c>.</summary>
    public static readonly MapInk Default = new();

    /// <summary>
    /// THE ERA'S MAP INK. IDENTITY inks — the polities, the sectors, the universities and other
    /// institutions, the structures and dwellings, every road class's fill and marks, the selection ring
    /// and the capital's mark — are the SAME in every era, so a civilization keeps its colour from A1 to
    /// A9 and a granary reads as a granary. Only the NEUTRAL inks follow the era: the line-work ink, the
    /// darkest glyph ink and the engineered roads' kerb casing move with the era's text ink; the pale
    /// paper inks — the path casing, the name plate under names and the settlement halo (the map's
    /// legend paper) — move with the era's record surface. Each moves by EXACTLY the era's departure
    /// from the style bible's parchment (<see cref="ParchmentPalette.InkPrimary"/> for the inks,
    /// <see cref="ParchmentPalette.PaperLight"/> for the paper), so the parchment era — which IS the
    /// bible palette — draws today's map byte for byte. Pure: the theme in, the record out; the map
    /// substrate (the parchment bake) is never themed (style bible §1).
    /// </summary>
    public static MapInk For(EraTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        MapInk d = Default;
        Rgba ink = theme.Ink.Text, paper = theme.Material.Panel;
        Rgba Dark(Rgba c) => Shift(c, ink, ParchmentPalette.InkPrimary);
        Rgba Pale(Rgba c) => Shift(c, paper, ParchmentPalette.PaperLight);
        return d with
        {
            Ink = Dark(d.Ink), GlyphInk = Dark(d.GlyphInk), RoadEdge = Dark(d.RoadEdge),
            PathCasing = Pale(d.PathCasing), NamePlate = Pale(d.NamePlate), SettlementHalo = Pale(d.SettlementHalo),
            Hand = theme.MapHand,
        };
    }

    // ---------------------------------------------------------------- the hand (no colour)
    /// <summary>THE MAP'S HAND (packet P13; era-ui.md §8): how the lens strokes its glyphs, cuts its token plates,
    /// flies its Age banners and ornaments its name plates in the era — the theme's <see cref="EraTheme.MapHand"/>,
    /// carried here so the lens reads ONE presentation record. It changes how a mark is drawn, never which marks are
    /// drawn, where, or how many: the lens's geometry, layers, draw-call accounting and hit regions are the same in
    /// every era. The default is today's map (<see cref="MapHandTokens.Parchment"/>, the parchment era's hand).</summary>
    public MapHandTokens Hand { get; init; } = MapHandTokens.Parchment;

    /// <summary><paramref name="c"/> moved channel by channel by (<paramref name="era"/> − <paramref name="bible"/>),
    /// clamped to a byte; alpha kept.</summary>
    private static Rgba Shift(Rgba c, Rgba era, Rgba bible) =>
        new(Clamp(c.R + era.R - bible.R), Clamp(c.G + era.G - bible.G), Clamp(c.B + era.B - bible.B), c.A);

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    // ---------------------------------------------------------------- line work and labels
    /// <summary>Primary map ink: names, outlines, banner poles, structure-glyph frames (bible InkPrimary).</summary>
    public Rgba Ink { get; init; } = Rgba.Hex(0x3A2E1F);
    /// <summary>M6 EX (ADR-044): undiscovered land — blank parchment (bible PaperShade), drawn opaque over the terrain.</summary>
    public Rgba FogUnknown { get; init; } = Rgba.Hex(0xE2D3AE);
    /// <summary>M6 EX (ADR-044): remembered land (known, out of sight) — a dark wash the lens draws translucent.</summary>
    public Rgba FogRemembered { get; init; } = Rgba.Hex(0x1E1810);
    /// <summary>The darkest ink: university-hall outlines and formation-token frames.</summary>
    public Rgba GlyphInk { get; init; } = Rgba.Hex(0x1E1810);
    /// <summary>Pale marks drawn on a glyph or token face (emblems).</summary>
    public Rgba EmblemMark { get; init; } = Rgba.Hex(0xF4EBD3);
    /// <summary>The medical university's cross (the one emblem that is not pale).</summary>
    public Rgba MedicalMark { get; init; } = Rgba.Hex(0x9B2B2B);
    /// <summary>The selected settlement's ring.</summary>
    public Rgba Selection { get; init; } = Rgba.Hex(0xFFD25A);

    // ---------------------------------------------------------------- polities
    /// <summary>The player's polity (territory, settlement marks, banners, tokens).</summary>
    public Rgba PlayerPolity { get; init; } = Rgba.Hex(0xC8962E);
    /// <summary>Other polities, by <c>polity % Count</c>.</summary>
    public IReadOnlyList<Rgba> Polities { get; init; } =
        [Rgba.Hex(0x9B3B3B), Rgba.Hex(0x3F6E8C), Rgba.Hex(0x5C7F45), Rgba.Hex(0x7A4F8C), Rgba.Hex(0x2F7C74), Rgba.Hex(0x8C6A3F)];
    /// <summary>A settlement or claim no polity controls.</summary>
    public Rgba NoPolity { get; init; } = Rgba.Hex(0x777066);

    // ---------------------------------------------------------------- settlements
    /// <summary>The pale halo behind a settlement mark at World zoom.</summary>
    public Rgba SettlementHalo { get; init; } = Rgba.Hex(0xF4EBD3);
    /// <summary>The capital's star and the capital's banner numeral.</summary>
    public Rgba CapitalMark { get; init; } = Rgba.Hex(0xFFF1C4);
    /// <summary>The soft paper plate under a settlement's name (roads may run beneath it).</summary>
    public Rgba NamePlate { get; init; } = Rgba.Hex(0xF4EBD3);
    /// <summary>A banner numeral on a settlement that is not the capital.</summary>
    public Rgba BannerText { get; init; } = Rgba.Hex(0xF4EBD3);
    /// <summary>The built footprint's fill (Regional and Settlement zoom).</summary>
    public Rgba Footprint { get; init; } = Rgba.Hex(0xEADBB8);
    /// <summary>A dwelling block (one per 25 dwellings, aggregated) — the same form at every Age.</summary>
    public Rgba Dwelling { get; init; } = Rgba.Hex(0x8C4A3A);

    // ---------------------------------------------------------------- structures and institutions
    /// <summary>Granary (storage) structure glyph.</summary>
    public Rgba Granary { get; init; } = Rgba.Hex(0xC9A35A);
    /// <summary>Workshop (production) structure glyph.</summary>
    public Rgba Workshop { get; init; } = Rgba.Hex(0xB06A44);
    /// <summary>Any other structure glyph.</summary>
    public Rgba StructureOther { get; init; } = Rgba.Hex(0x9A8F7A);
    /// <summary>Institution halls by university type key 1..5 (military, medical, engineering, natural
    /// science, agricultural) — five distinct inks.</summary>
    public IReadOnlyList<Rgba> Universities { get; init; } =
        [Rgba.Hex(0x8E3B32), Rgba.Hex(0xE8E2D0), Rgba.Hex(0x4F6F8F), Rgba.Hex(0x3E7A6A), Rgba.Hex(0x9AA344)];
    /// <summary>An institution hall whose type key is not one of the five universities.</summary>
    public Rgba InstitutionOther { get; init; } = Rgba.Hex(0x9A8F7A);

    // ---------------------------------------------------------------- labour (production signals)
    /// <summary>Sector arcs: farming, herding, extraction, crafting, construction.</summary>
    public IReadOnlyList<Rgba> Sectors { get; init; } =
        [Rgba.Hex(0x8FA34E), Rgba.Hex(0xC2A060), Rgba.Hex(0x8A8A86), Rgba.Hex(0xC07048), Rgba.Hex(0x6C8FB0)];

    // ---------------------------------------------------------------- the resource overlay (M5 P9)
    /// <summary>P9: the pale end every resource-overlay ramp starts from — the lowest level of every layer. An identity
    /// ink (the same in every era): the overlay's meaning is its ramp, not the era's paper.</summary>
    public Rgba OverlayLow { get; init; } = Rgba.Hex(0xF4ECD2);
    // The six deep inks are hues the parchment terrain does not use (no green, no tan): a ramp must read over every
    // ground it lies on.
    /// <summary>P9: the deep end of the moisture deposit channel's ramp.</summary>
    public Rgba OverlayMoisture { get; init; } = Rgba.Hex(0x4B2E8C);
    /// <summary>P9: the deep end of the water-proximity deposit channel's ramp (any channel the deposit function reads as
    /// the squared moisture mean).</summary>
    public Rgba OverlayWater { get; init; } = Rgba.Hex(0x0B6F80);
    /// <summary>P9: the deep end of the elevation deposit channel's ramp.</summary>
    public Rgba OverlayElevation { get; init; } = Rgba.Hex(0xA03C14);
    /// <summary>P9: the deep end of the settlement-suitability (unjittered siting score) ramp.</summary>
    public Rgba OverlaySuitability { get; init; } = Rgba.Hex(0x8C1460);
    /// <summary>P9: the deep end of the region-fertility factor's ramp.</summary>
    public Rgba OverlayFertility { get; init; } = Rgba.Hex(0x8A6A00);
    /// <summary>P9: the deep end of the water-access factor's ramp.</summary>
    public Rgba OverlayAccess { get; init; } = Rgba.Hex(0x1A3F9C);

    /// <summary>P9: the overlay's colour at ramp position <paramref name="t"/> (0 = <see cref="OverlayLow"/>, 1 =
    /// <paramref name="deep"/>; clamped), at <paramref name="alpha"/> (0..1, rounded to the byte). Each channel is
    /// rounded to the byte: a pure function of its arguments.</summary>
    public Rgba OverlayRamp(Rgba deep, double t, double alpha)
    {
        double u = Math.Clamp(t, 0.0, 1.0);
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * u);
        return new Rgba(Mix(OverlayLow.R, deep.R), Mix(OverlayLow.G, deep.G), Mix(OverlayLow.B, deep.B),
            (byte)Math.Round(255.0 * Math.Clamp(alpha, 0.0, 1.0)));
    }

    // ---------------------------------------------------------------- the network
    /// <summary>The free dirt-path baseline's line (and a route's unmodernized dirt-path remainder).</summary>
    public Rgba Path { get; init; } = Rgba.Hex(0x6B4A2A);
    /// <summary>The pale casing under paths and trackways at Regional and Settlement zoom.</summary>
    public Rgba PathCasing { get; init; } = Rgba.Hex(0xF2E6C8);
    /// <summary>Trackway (PATH tier, an improved path) — bible InkSoft #6B5A3E.</summary>
    public Rgba Trackway { get; init; } = Rgba.Hex(0x6B5A3E);
    /// <summary>The kerb/casing of engineered roads and highways (ROAD and HIGHWAY tiers) — bible InkPrimary #3A2E1F.</summary>
    public Rgba RoadEdge { get; init; } = Rgba.Hex(0x3A2E1F);
    /// <summary>Built road fill (ROAD tier) — bible PaperShade #C9B588.</summary>
    public Rgba BuiltRoad { get; init; } = Rgba.Hex(0xC9B588);
    /// <summary>Paved road fill (ROAD tier) — bible PaperLight #EFE3C8.</summary>
    public Rgba PavedRoad { get; init; } = Rgba.Hex(0xEFE3C8);
    /// <summary>Macadam road fill (ROAD tier) — bible UplandUmber #A98B63.</summary>
    public Rgba MacadamRoad { get; init; } = Rgba.Hex(0xA98B63);
    /// <summary>Highway fill (HIGHWAY tier) — bible IronRed #8C4A3A.</summary>
    public Rgba Highway { get; init; } = Rgba.Hex(0x8C4A3A);
    /// <summary>Class marks drawn on a road's fill (paving setts, the macadam centre line) — bible InkSoft #6B5A3E.</summary>
    public Rgba RoadMarks { get; init; } = Rgba.Hex(0x6B5A3E);
    /// <summary>The highway's centre line — bible PaperLight #EFE3C8.</summary>
    public Rgba HighwayCentre { get; init; } = Rgba.Hex(0xEFE3C8);

    /// <summary>Polity ink: the player in <see cref="PlayerPolity"/>, others by <c>polity % Polities.Count</c>,
    /// no polity (negative id) in <see cref="NoPolity"/>.</summary>
    public Rgba PolityOf(int polity, int player)
    {
        if (polity == player) return PlayerPolity;
        if (polity < 0 || Polities.Count == 0) return NoPolity;
        return Polities[polity % Polities.Count];
    }

    /// <summary>The hall ink of an institution type key: the university types 1..5, otherwise
    /// <see cref="InstitutionOther"/>.</summary>
    public Rgba InstitutionInk(int typeKey) =>
        typeKey >= 1 && typeKey <= Universities.Count ? Universities[typeKey - 1] : InstitutionOther;

    // ---------------------------------------------------------------- the object layer (M6 E1; D-038 Part B, style bible §8)
    // IDENTITY inks, the same in every era (a granary reads as a granary from A1 to A9). Every base is a style-bible §2 hex
    // (named in each comment) and every lit or shade face is DERIVED from its base by Lit / Shade below — no new hue
    // (style bible §2, "object-layer shading stops").
    /// <summary>E1: the contact shadow under every object, drawn at <see cref="ObjectLight.ShadowAlpha"/> — bible InkPrimary
    /// #3A2E1F deepened; used for nothing else (the single-light test identifies shadows by it).</summary>
    public Rgba ObjectShadow { get; init; } = Rgba.Hex(0x2C2318);
    /// <summary>E1: relief — upland and mountain mass (bible UplandUmber #A98B63).</summary>
    public Rgba ReliefUpland { get; init; } = Rgba.Hex(0xA98B63);
    /// <summary>E1: relief — low hills (bible PlainTan #C4B183).</summary>
    public Rgba ReliefHill { get; init; } = Rgba.Hex(0xC4B183);
    /// <summary>E1: relief — the pale cap of the highest peaks (bible PeakPale #DCC9A0).</summary>
    public Rgba ReliefPeak { get; init; } = Rgba.Hex(0xDCC9A0);
    /// <summary>E1: forest crowns (bible Verdigris #5E7A6B).</summary>
    public Rgba Forest { get; init; } = Rgba.Hex(0x5E7A6B);
    /// <summary>E1: a cultivated field's crop strip (bible FertileGreen #8F9C63).</summary>
    public Rgba FieldCrop { get; init; } = Rgba.Hex(0x8F9C63);
    /// <summary>E1: a cultivated field's fallow strip (bible PlainTan #C4B183).</summary>
    public Rgba FieldFallow { get; init; } = Rgba.Hex(0xC4B183);
    /// <summary>E1: the scaffold of a part under construction (bible InkSoft #6B5A3E).</summary>
    public Rgba Scaffold { get; init; } = Rgba.Hex(0x6B5A3E);
    /// <summary>E1.4: the walls of the style register's four rungs (<see cref="ObjectRegister"/>) — earthen (bible Arid
    /// #CBBE93), timber (bible UplandUmber #A98B63), masonry (bible PaperMid #E3D3AE), engineered (bible PaperShade
    /// #C9B588).</summary>
    public IReadOnlyList<Rgba> RegisterWalls { get; init; } =
        [Rgba.Hex(0xCBBE93), Rgba.Hex(0xA98B63), Rgba.Hex(0xE3D3AE), Rgba.Hex(0xC9B588)];
    /// <summary>E1.4: the roofs of the register's rungs — thatch (bible PlainTan #C4B183), shingle (bible InkSoft #6B5A3E),
    /// tile (bible IronRed #8C4A3A), slate (bible DeepSea #5F7E88).</summary>
    public IReadOnlyList<Rgba> RegisterRoofs { get; init; } =
        [Rgba.Hex(0xC4B183), Rgba.Hex(0x6B5A3E), Rgba.Hex(0x8C4A3A), Rgba.Hex(0x5F7E88)];

    /// <summary>The paper the LIT stop lightens toward (bible PaperLight) and the ink the SHADE stop deepens toward (bible
    /// InkPrimary) — constants of the object layer's one light, never era-shifted.</summary>
    private static readonly Rgba LitToward = Rgba.Hex(0xEFE3C8), ShadeToward = Rgba.Hex(0x3A2E1F);

    /// <summary>E1: the LIT face of an object whose base is <paramref name="c"/> — the face turned to the one light
    /// (<see cref="ObjectLight"/>): <paramref name="c"/> moved <see cref="ObjectLight.LitStop"/> of the way to the paper,
    /// channel by channel, rounded to the byte; alpha kept.</summary>
    public static Rgba Lit(Rgba c) => Toward(c, LitToward, ObjectLight.LitStop);

    /// <summary>E1: the SHADE face of an object whose base is <paramref name="c"/> — <paramref name="c"/> moved
    /// <see cref="ObjectLight.ShadeStop"/> of the way to the ink; alpha kept.</summary>
    public static Rgba Shade(Rgba c) => Toward(c, ShadeToward, ObjectLight.ShadeStop);

    private static Rgba Toward(Rgba c, Rgba to, double t) =>
        new((byte)Math.Round(c.R + (to.R - c.R) * t), (byte)Math.Round(c.G + (to.G - c.G) * t),
            (byte)Math.Round(c.B + (to.B - c.B) * t), c.A);

    /// <summary>E1.4: the wall ink of register rung <paramref name="rung"/> (clamped to the rungs there are).</summary>
    public Rgba WallOf(int rung) => RegisterWalls[Math.Clamp(rung, 0, RegisterWalls.Count - 1)];

    /// <summary>E1.4: the roof ink of register rung <paramref name="rung"/> (clamped).</summary>
    public Rgba RoofOf(int rung) => RegisterRoofs[Math.Clamp(rung, 0, RegisterRoofs.Count - 1)];
}
