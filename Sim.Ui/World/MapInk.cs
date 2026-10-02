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
/// <para>ERA SEAM (ADR-033 D8, one line): a presentation layer derives a <see cref="MapInk"/> from the
/// player's Age and passes it to <c>WorldLens.Paint(…, ink:)</c>; the map's composition stays a read
/// of state, only its ink changes. Nothing here reads the Age or any theme.</para>
/// </summary>
public sealed record MapInk
{
    /// <summary>Today's map: the exact colours the lens has always drawn.</summary>
    public static readonly MapInk Default = new();

    // ---------------------------------------------------------------- line work and labels
    /// <summary>Primary map ink: names, outlines, banner poles, structure-glyph frames (bible InkPrimary).</summary>
    public Rgba Ink { get; init; } = Rgba.Hex(0x3A2E1F);
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
}
