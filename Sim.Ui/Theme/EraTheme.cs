using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

// ============================================================================ token enums

/// <summary>The surface the civilization keeps its records on, era by era — what the panels are
/// "made of". Drives the procedural texture <see cref="PanelFrame"/> lays on a surface.</summary>
public enum MaterialKind { Stone = 1, Clay = 2, Bronze = 3, Iron = 4, Marble = 5, Vellum = 6, Paper = 7, Drafting = 8, Glass = 9 }

/// <summary>How a frame's corners are cut.</summary>
public enum CornerStyle { Organic = 1, Rounded = 2, Chamfered = 3, Square = 4, Fine = 5 }

/// <summary>The ornament vocabulary of an era (rules, bands, corner pieces).</summary>
public enum Motif { None = 0, Weave = 1, Chevron = 2, Rivet = 3, Meander = 4, Illumination = 5, Fleuron = 6, Dimension = 7, Hairline = 8 }

/// <summary>How fine a control is: coarse, chunky marks at A1 to precise, compact ones at A9.</summary>
public enum ControlGranularity { Coarse = 1, Simple = 2, Standard = 3, Fine = 4, Precise = 5 }

/// <summary>How progress is drawn: tally notches, segmented plates, a bar, a graduated bar.</summary>
public enum ProgressStyle { Notches = 1, Segments = 2, Bar = 3, GraduatedBar = 4 }

/// <summary>The hand an icon is drawn in. The icon's MEANING (check, ring, dot, lock) never changes
/// across eras — only its stroke (continuity: recognisable icons).</summary>
public enum IconStyle { Daubed = 1, Incised = 2, Cast = 3, Forged = 4, Carved = 5, Illuminated = 6, Engraved = 7, Technical = 8, Precise = 9 }

// ============================================================================ token groups

/// <summary>GROUND AND MATERIAL: the surfaces and their texture. <c>Field</c> is the screen's ground,
/// <c>Panel</c> a panel or card, <c>PanelRaised</c> a raised control, <c>PanelSunken</c> a well or
/// track, <c>Chrome</c> the bars. <c>Grain*</c> parameterise the procedural surface texture
/// (marks per 10 000 px², mark alpha, mark size in px).</summary>
public sealed record MaterialTokens(
    MaterialKind Kind,
    Rgba Field, Rgba FieldAlt, Rgba Panel, Rgba PanelRaised, Rgba PanelSunken, Rgba Chrome,
    Rgba Border, Rgba BorderStrong, Rgba Hairline, Rgba Accent, Rgba AccentSoft,
    Rgba Grain, double GrainDensity, double GrainAlpha, double GrainSize);

/// <summary>INK: text and rule colours on the era's ground, and the inks for text set directly on
/// the <see cref="MaterialTokens.Chrome"/> bars. The bars are the era's FRAME material — charred
/// wood, dark wood, cast bronze, forged iron, porphyry, the binding's leather, printer's black,
/// Prussian blue, graphite — darker than the content in every era (one consistent structure), so
/// their text is light: <c>OnChrome</c>, <c>OnChromeSoft</c>, and <c>OnChromeAccent</c> for the
/// emphasised figure on a bar.</summary>
public sealed record InkTokens(Rgba Text, Rgba TextSoft, Rgba TextDim, Rgba OnAccent, Rgba Rule,
    Rgba OnChrome, Rgba OnChromeSoft, Rgba OnChromeAccent);

/// <summary>The content lanes' hues (Technology trunk and subtrees, the external anchor lane, Civics).
/// Each lane keeps its hue family in every era; only the pigment changes.</summary>
public sealed record LaneHues(
    Rgba Main, Rgba Military, Rgba Medicine, Rgba Engineering, Rgba NaturalScience, Rgba Agriculture,
    Rgba External, Rgba Civics)
{
    /// <summary>The hue for a content lane id ("main", "military", …; anything else is a civics domain).</summary>
    public Rgba Of(string laneId) => laneId switch
    {
        "main" => Main,
        "military" => Military,
        "medicine" => Medicine,
        "engineering" => Engineering,
        "natural_science" => NaturalScience,
        "agriculture" => Agriculture,
        "external" => External,
        _ => Civics,
    };
}

/// <summary>
/// SEMANTIC COLOURS — the persistent visual DNA. Each token keeps its hue family in every era
/// (<see cref="SemanticFamilies"/>; pinned by test): what is completed is gold, what is being
/// researched is teal, what is available is blue, what is locked is grey, retained progress is
/// amber, danger is red — whether it is daubed in ochre at A1 or printed in a modern ink at A9.
/// The <c>*Fill</c> tokens are the state tints a card is filled with.
/// </summary>
public sealed record SemanticTokens(
    Rgba Available, Rgba Active, Rgba Completed, Rgba Locked, Rgba Progress, Rgba Danger, Rgba Positive,
    Rgba Food, Rgba Knowledge, Rgba Military, Rgba Infrastructure,
    Rgba Prerequisite, Rgba Dependent,
    Rgba AvailableFill, Rgba ActiveFill, Rgba CompletedFill, Rgba LockedFill,
    LaneHues Lanes);

/// <summary>EDGE TREATMENT: roughness in [0,1] (1 = hand-cut stone, 0 = a ruled line), the jitter
/// amplitude in px that roughness becomes, the corner cut and its size, the border weight, whether
/// the border is a double rule, and whether fasteners (studs, rivets, bolts) sit on it.</summary>
public sealed record EdgeTokens(double Roughness, double JitterPx, CornerStyle Corner, double CornerPx, double BorderPx,
    bool DoubleRule, bool Fasteners);

/// <summary>ORNAMENT: level 0 (none) … 4 (illuminated) and the era's motif. Ornament is not
/// sophistication: it peaks with the manuscript era and falls away again towards the precise
/// modern interface.</summary>
public sealed record OrnamentTokens(int Level, Motif Motif);

/// <summary>TYPOGRAPHY: the style of each <see cref="FontRole"/>, the era's size scale applied to
/// every DrawList run, and its line height (as a multiple of the size).</summary>
public sealed record TypographyTokens(
    TextStyle Body, TextStyle Heading, TextStyle Title, TextStyle Numeric, TextStyle Caps,
    double SizeScale, double LineHeight)
{
    public TextStyle For(FontRole role) => role switch
    {
        FontRole.Heading => Heading,
        FontRole.Title => Title,
        FontRole.Numeric => Numeric,
        FontRole.Caps => Caps,
        _ => Body,
    };

    /// <summary>A design size in this era's scale.</summary>
    public double Size(double designPx) => Math.Round(designPx * SizeScale * 100.0) / 100.0;

    /// <summary>The line advance for a run of <paramref name="designPx"/> in this era.</summary>
    public double Line(double designPx) => Math.Round(Size(designPx) * LineHeight * 100.0) / 100.0;
}

/// <summary>DENSITY: level 1 (sparse) … 5 (dense); the inner padding and gap of DrawList panels in
/// px; and how much the research tree shows (1: a card's name, Age, cost, state, notches and
/// cross-lane label; 2: + Eureka pips; 3: + prerequisite/dependent stubs and each lane segment's
/// completed share; 4: + discounts and the lane chips' done/total; 5: + the target's estimate of
/// turns to complete).</summary>
public sealed record DensityTokens(int Level, double Padding, double Gap, int CardDetail);

/// <summary>CONTROLS: granularity, the progress representation and its segment count, the control
/// stroke and the grab size (ImGui sliders and scrollbars).</summary>
public sealed record ControlTokens(ControlGranularity Granularity, ProgressStyle Progress, int ProgressSegments,
    double StrokePx, double GrabPx);

/// <summary>ICONS: the hand they are drawn in, stroke weight, wobble amplitude (hand-drawn eras) and
/// whether marks are filled.</summary>
public sealed record IconTokens(IconStyle Style, double StrokePx, double WobblePx, bool Filled);

/// <summary>CHARTS: sophistication 0 (tallies) … 4 (graduated axes and grids), and its parts.</summary>
public sealed record ChartTokens(int Sophistication, bool GridLines, bool Ticks, bool Labels, double LineWidth);

// ============================================================================ the theme

/// <summary>
/// THE ERA THEME — every presentation token of one visual era (ADR-033 D8). A pure value: built by
/// <see cref="EraThemes.For"/> from the era alone, never stored in the simulation, recomputed after
/// load, and shared by the live ImGui renderer and the headless SVG previews. The same layout regions,
/// navigation, interaction rules and semantic colours exist in every era (continuity); material,
/// edge, ornament, typography, density and control granularity evolve with the Age. The map's ink is
/// NOT a token group here: the world layer's one complete ink record is <c>Sim.Ui.World.MapInk</c>,
/// derived from this theme by <c>MapInk.For(theme)</c> (ADR-033 D8 convergence).
/// </summary>
public sealed record EraTheme(
    UiEra Era,
    string Name,
    string Medium,
    MaterialTokens Material,
    InkTokens Ink,
    SemanticTokens Semantic,
    EdgeTokens Edge,
    OrnamentTokens Ornament,
    TypographyTokens Type,
    DensityTokens Density,
    ControlTokens Controls,
    IconTokens Icons,
    ChartTokens Charts)
{
    /// <summary>The era's ordinal (the Age key it presents), 1..9.</summary>
    public int Ordinal => (int)Era;

    /// <summary>The canonical, byte-stable text of every token (<see cref="ThemeCanon"/>).</summary>
    public string Canonical() => ThemeCanon.Describe(this);
}
