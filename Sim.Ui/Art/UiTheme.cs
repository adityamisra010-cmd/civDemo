using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Sim.Ui.Render;
using Sim.Ui.Theme;

namespace Sim.Ui.Art;

/// <summary>
/// THE UI SKIN (style-bible §3 typography + §4 item 5 frame furniture; ADR-033 D8): ImGui styled
/// from the player's ERA THEME — <see cref="StyleFor"/> is the pure mapping (tested headless),
/// <see cref="Apply(EraTheme)"/> writes it into ImGui. In the parchment era (A6) this is the bible's
/// ink on parchment, as it always was; in the other eras the same widgets wear that era's material.
///
/// CONTINUITY. Only colours, rounding, borders and the horizontal padding/spacing vary with the era.
/// The design metrics the chrome geometry is computed from — the font pixel sizes, the window
/// padding and the frame padding's height — are the same in every era, so every panel, button and
/// rule sits on the same rect at A1 and at A9 (ChromeGeometry, pinned by test).
///
/// TYPOGRAPHY (§3): EB Garamond — a humanist old-style serif with the engraved feel the bible
/// names — carries labels and headers; IBM Plex Serif, a clean lining-figure companion, carries
/// dense numbers; IBM Plex Sans (OFL 1.1, added for the industrial and modern eras) carries the
/// A8–A9 headings and numbers. All are SIL Open Font License 1.1; the licence texts ship beside them
/// (assets/fonts/OFL-*.txt) and assets/fonts/README.md records provenance.
///
/// FALLBACK: a missing font file is NOT fatal — ImGui's built-in face is used and the debug panel
/// says so, the same discipline AssetLibrary applies to textures. The map must always open.
/// </summary>
public static class UiTheme
{
    public const string LabelFontFile = "EBGaramond-Variable.ttf";
    public const string NumberFontFile = "IBMPlexSerif-Regular.ttf";
    public const string SansFontFile = "IBMPlexSans-Regular.ttf";

    /// <summary>The atlas faces. <c>Body</c>/<c>Header</c>/<c>Numeric</c> are the pre-theme roles
    /// (EB Garamond 19/25, IBM Plex Serif 17); the init-only faces add Plex Serif at body size and
    /// Plex Sans at the three sizes, for the eras whose typography sets them.</summary>
    public sealed record Fonts(ImFontPtr Body, ImFontPtr Header, ImFontPtr Numeric, string Note)
    {
        public ImFontPtr SerifBody { get; init; }
        public ImFontPtr SansNumeric { get; init; }
        public ImFontPtr SansBody { get; init; }
        public ImFontPtr SansHeader { get; init; }

        private static bool Has(ImFontPtr f) { unsafe { return f.NativePtr != null; } }

        /// <summary>The atlas font for a face at the rasterisation closest to <paramref name="sizePx"/>
        /// (falling back to the pre-theme face when a file was missing).</summary>
        public ImFontPtr Face(TypeFace face, double sizePx)
        {
            bool large = sizePx >= (BodyFontPx + HeaderFontPx) / 2.0;
            bool small = sizePx < (NumericFontPx + BodyFontPx) / 2.0;
            return face switch
            {
                TypeFace.PlexSans => large ? Pick(SansHeader, Header) : small ? Pick(SansNumeric, Numeric) : Pick(SansBody, Body),
                TypeFace.PlexSerif => small ? Numeric : Pick(SerifBody, Numeric),
                _ => large ? Header : Body,
            };
        }

        /// <summary>The chrome's body, header and data faces in <paramref name="theme"/>'s typography.</summary>
        public (ImFontPtr Body, ImFontPtr Header, ImFontPtr Numeric) For(EraTheme theme) =>
            (Face(theme.Type.Body.Face, BodyFontPx), Face(theme.Type.Heading.Face, HeaderFontPx), Face(theme.Type.Numeric.Face, NumericFontPx));

        private static ImFontPtr Pick(ImFontPtr wanted, ImFontPtr fallback) => Has(wanted) ? wanted : fallback;
    }

    // THE DESIGN METRICS, named (T4.19 lane D). They were literals inside
    // LoadFonts' defaults and Apply's body, which meant the headless
    // view-model could not know the frame height the renderer would measure
    // — and the command bar's rule was placed against a frame height nothing
    // outside the draw call had ever seen. ChromeGeometry derives its rects
    // from these; Apply and LoadFonts read the SAME constants, so the tested
    // geometry and the styled screen cannot drift apart. ERA-INVARIANT.
    public const float BodyFontPx = 19f;
    public const float HeaderFontPx = 25f;
    public const float NumericFontPx = 17f;
    public static readonly Vector2 WindowPaddingPx = new(14, 12);
    public static readonly Vector2 FramePaddingPx = new(8, 5);
    public static readonly Vector2 ItemSpacingPx = new(9, 7);

    /// <summary>ImGui's GetFrameHeight() under the body face: FontSize +
    /// 2 × FramePadding.y = 29 px. The renderer still passes its MEASURED
    /// frame height into ChromeGeometry (fonts can fall back); this is the
    /// design value the headless tests pin against.</summary>
    public static float FrameHeightPx => BodyFontPx + 2f * FramePaddingPx.Y;

    // The atlas glyph ranges must outlive the atlas build: pinned once for the process.
    private static readonly ushort[] Ranges = UiText.GlyphRanges();
    private static GCHandle _rangesHandle;

    private static IntPtr RangesPtr()
    {
        if (!_rangesHandle.IsAllocated) _rangesHandle = GCHandle.Alloc(Ranges, GCHandleType.Pinned);
        return _rangesHandle.AddrOfPinnedObject();
    }

    /// <summary>Loads the faces into the ImGui atlas (Latin-1 plus the typographic code points of
    /// <see cref="UiText"/>), BUILDS it, and applies the text boundary's glyph remaps. Call BEFORE the
    /// renderer uploads the atlas texture (it then finds the atlas built and keeps the remaps).</summary>
    public static Fonts LoadFonts(string assetsRoot,
        float bodyPx = BodyFontPx, float headerPx = HeaderFontPx, float numericPx = NumericFontPx)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        string fontDir = Path.Combine(assetsRoot, "fonts");
        string label = Path.Combine(fontDir, LabelFontFile);
        string numeric = Path.Combine(fontDir, NumberFontFile);
        string sans = Path.Combine(fontDir, SansFontFile);
        IntPtr ranges = RangesPtr();
        ImFontPtr Add(string file, float px) => io.Fonts.AddFontFromFileTTF(file, px, default, ranges);

        Fonts fonts;
        if (!File.Exists(label) && !File.Exists(numeric))
        {
            ImFontPtr fallback = io.Fonts.AddFontDefault();
            fonts = new Fonts(fallback, fallback, fallback, $"fonts: DEFAULT (no faces in {fontDir})");
        }
        else
        {
            ImFontPtr body = File.Exists(label) ? Add(label, bodyPx) : io.Fonts.AddFontDefault();
            ImFontPtr header = File.Exists(label) ? Add(label, headerPx) : body;
            ImFontPtr numbers = File.Exists(numeric) ? Add(numeric, numericPx) : body;
            ImFontPtr serifBody = File.Exists(numeric) ? Add(numeric, bodyPx) : default;
            bool hasSans = File.Exists(sans);
            string note = File.Exists(label) && File.Exists(numeric)
                ? "fonts: EB Garamond + IBM Plex Serif" + (hasSans ? " + IBM Plex Sans" : "") + " (OFL 1.1)"
                : $"fonts: PARTIAL (missing {(File.Exists(label) ? NumberFontFile : LabelFontFile)})";
            fonts = new Fonts(body, header, numbers, note)
            {
                SerifBody = serifBody,
                SansNumeric = hasSans ? Add(sans, numericPx) : default,
                SansBody = hasSans ? Add(sans, bodyPx) : default,
                SansHeader = hasSans ? Add(sans, headerPx) : default,
            };
        }
        io.Fonts.Build();
        ApplyTextBoundary(io.Fonts);
        return fonts;
    }

    /// <summary>Whether <paramref name="font"/> resolves <paramref name="c"/> to a glyph of its own
    /// (a rasterised glyph or a text-boundary remap) rather than the '?' fallback.</summary>
    public static bool HasGlyph(ImFontPtr font, char c)
    {
        unsafe { return font.FindGlyphNoFallback(c).NativePtr != null; }
    }

    /// <summary>Whether two atlas handles are the same font.</summary>
    public static bool SameFont(ImFontPtr a, ImFontPtr b)
    {
        unsafe { return a.NativePtr == b.NativePtr; }
    }

    /// <summary>The ImGui text boundary (<see cref="UiText"/>): in every face, each single-character
    /// substitution's code point is remapped to its Latin-1 stand-in's glyph, so text handed straight
    /// to ImGui renders as DrawList text does. Requires a built atlas; re-apply after any rebuild.</summary>
    public static void ApplyTextBoundary(ImFontAtlasPtr atlas)
    {
        for (int i = 0; i < atlas.Fonts.Size; i++)
        {
            ImFontPtr font = atlas.Fonts[i];
            foreach ((char from, char to) in UiText.Remaps) font.AddRemapChar(from, to);
        }
    }

    // ================================================================== the style

    /// <summary>ImGui's style as data: metrics and colours (RGBA floats per <see cref="ImGuiCol"/>).</summary>
    public sealed record StyleSpec(
        float WindowRounding, float ChildRounding, float PopupRounding, float FrameRounding, float GrabRounding,
        float ScrollbarRounding, float TabRounding,
        float WindowBorderSize, float ChildBorderSize, float PopupBorderSize, float FrameBorderSize,
        Vector2 WindowPadding, Vector2 FramePadding, Vector2 ItemSpacing, Vector2 ItemInnerSpacing,
        float ScrollbarSize, float GrabMinSize,
        IReadOnlyList<(ImGuiCol Col, Vector4 Rgba)> Colors)
    {
        public Vector4 Color(ImGuiCol col)
        {
            foreach ((ImGuiCol c, Vector4 v) in Colors) if (c == col) return v;
            throw new KeyNotFoundException(col.ToString());
        }
    }

    /// <summary>
    /// THE PURE MAPPING from an era theme to ImGui's style. Era-invariant: the window padding, the
    /// frame padding's height and the font sizes (the chrome geometry). Era-variant: every colour,
    /// rounding by corner cut, border weights, the horizontal frame padding and item spacing
    /// (density), scrollbar and grab sizes (control granularity). The window border is left to
    /// <see cref="PanelFrame"/>, which paints every chrome window's edge in the era's hand.
    /// </summary>
    public static StyleSpec StyleFor(EraTheme t)
    {
        MaterialTokens m = t.Material;
        InkTokens ink = t.Ink;
        float round = t.Edge.Corner switch
        {
            CornerStyle.Organic => 7f, CornerStyle.Rounded => 5f, CornerStyle.Chamfered => 1f, CornerStyle.Fine => 3f, _ => 0f,
        };
        int level = t.Density.Level;   // 1 sparse … 5 dense
        float frameX = 11f - level;     // 10 … 6
        var itemSpacing = new Vector2(12f - level, 9f - level * 0.6f);   // (11, 8.4) … (7, 6)
        float grain = (float)(t.Controls.Granularity switch
        {
            ControlGranularity.Coarse => 1.6, ControlGranularity.Simple => 1.3, _ => 1.0,
        });
        static Vector4 V(ParchmentPalette.Rgba c, double a) => new(c.R / 255f, c.G / 255f, c.B / 255f, (float)a);
        ParchmentPalette.Rgba hover = ThemeColor.Mix(m.PanelRaised, m.Accent, 0.22);
        ParchmentPalette.Rgba header = ThemeColor.Mix(m.Panel, m.Accent, 0.20);
        var colors = new List<(ImGuiCol, Vector4)>
        {
            (ImGuiCol.Text, V(ink.Text, 1.0)),
            (ImGuiCol.TextDisabled, V(ink.TextDim, 0.85)),
            (ImGuiCol.WindowBg, V(m.Panel, 0.97)),
            (ImGuiCol.ChildBg, V(m.PanelRaised, 0.45)),
            (ImGuiCol.PopupBg, V(m.PanelRaised, 0.98)),
            (ImGuiCol.Border, V(m.Border, 0.75)),
            (ImGuiCol.BorderShadow, V(m.PanelSunken, 0.0)),
            (ImGuiCol.FrameBg, V(m.PanelRaised, 0.85)),
            (ImGuiCol.FrameBgHovered, V(hover, 1.0)),
            (ImGuiCol.FrameBgActive, V(m.Chrome, 1.0)),
            (ImGuiCol.TitleBg, V(m.Chrome, 0.95)),
            (ImGuiCol.TitleBgActive, V(m.Chrome, 1.0)),
            (ImGuiCol.TitleBgCollapsed, V(m.Chrome, 0.7)),
            (ImGuiCol.MenuBarBg, V(m.Chrome, 1.0)),
            (ImGuiCol.ScrollbarBg, V(m.PanelSunken, 0.35)),
            (ImGuiCol.ScrollbarGrab, V(ink.TextSoft, 0.55)),
            (ImGuiCol.ScrollbarGrabHovered, V(ink.TextSoft, 0.8)),
            (ImGuiCol.ScrollbarGrabActive, V(ink.Text, 0.9)),
            (ImGuiCol.CheckMark, V(ink.Text, 1.0)),
            (ImGuiCol.SliderGrab, V(ink.TextSoft, 0.9)),
            (ImGuiCol.SliderGrabActive, V(ink.Text, 1.0)),
            (ImGuiCol.Button, V(m.PanelRaised, 0.92)),
            (ImGuiCol.ButtonHovered, V(hover, 0.98)),
            (ImGuiCol.ButtonActive, V(m.Chrome, 1.0)),
            (ImGuiCol.Header, V(header, 0.55)),
            (ImGuiCol.HeaderHovered, V(header, 0.85)),
            (ImGuiCol.HeaderActive, V(ThemeColor.Mix(m.Panel, m.Accent, 0.32), 1.0)),
            (ImGuiCol.Separator, V(m.Hairline, 0.8)),
            (ImGuiCol.SeparatorHovered, V(m.Accent, 0.8)),
            (ImGuiCol.SeparatorActive, V(m.Accent, 1.0)),
            (ImGuiCol.ResizeGrip, V(ink.TextSoft, 0.35)),
            (ImGuiCol.ResizeGripHovered, V(ink.TextSoft, 0.6)),
            (ImGuiCol.ResizeGripActive, V(ink.Text, 0.8)),
            (ImGuiCol.Tab, V(m.Chrome, 0.9)),
            (ImGuiCol.TabHovered, V(hover, 1.0)),
            (ImGuiCol.TabSelected, V(m.PanelRaised, 1.0)),
            (ImGuiCol.TabSelectedOverline, V(m.Accent, 1.0)),
            (ImGuiCol.TabDimmed, V(m.Chrome, 0.7)),
            (ImGuiCol.TabDimmedSelected, V(m.Panel, 1.0)),
            (ImGuiCol.TabDimmedSelectedOverline, V(m.Accent, 0.6)),
            (ImGuiCol.PlotLines, V(ink.Text, 0.95)),
            (ImGuiCol.PlotLinesHovered, V(t.Semantic.Danger, 1.0)),
            (ImGuiCol.PlotHistogram, V(ink.TextSoft, 0.9)),
            (ImGuiCol.PlotHistogramHovered, V(m.Accent, 1.0)),
            (ImGuiCol.TableHeaderBg, V(m.Chrome, 1.0)),
            (ImGuiCol.TableBorderStrong, V(m.Border, 0.8)),
            (ImGuiCol.TableBorderLight, V(m.Hairline, 0.7)),
            (ImGuiCol.TableRowBg, V(m.Panel, 0.0)),
            (ImGuiCol.TableRowBgAlt, V(m.PanelSunken, 0.18)),
            (ImGuiCol.TextLink, V(t.Semantic.Knowledge, 1.0)),
            (ImGuiCol.TextSelectedBg, V(m.Accent, 0.35)),
            (ImGuiCol.DragDropTarget, V(m.Accent, 0.9)),
            (ImGuiCol.NavCursor, V(m.Accent, 1.0)),
            (ImGuiCol.NavWindowingHighlight, V(m.PanelRaised, 0.7)),
            (ImGuiCol.NavWindowingDimBg, V(ink.Text, 0.2)),
            (ImGuiCol.ModalWindowDimBg, V(ink.Text, 0.35)),
        };
        return new StyleSpec(
            WindowRounding: round, ChildRounding: round, PopupRounding: round, FrameRounding: round * 0.75f,
            GrabRounding: round * 0.75f, ScrollbarRounding: round, TabRounding: round * 0.75f,
            WindowBorderSize: 0f, ChildBorderSize: 0f, PopupBorderSize: 1f,
            FrameBorderSize: (float)Math.Clamp(t.Edge.BorderPx * 0.6, 1.0, 1.6),
            WindowPadding: WindowPaddingPx,
            FramePadding: new Vector2(frameX, FramePaddingPx.Y),
            ItemSpacing: itemSpacing,
            ItemInnerSpacing: new Vector2(7f - level * 0.4f, 4f),
            ScrollbarSize: 10f + 4f * grain - level * 0.4f,
            GrabMinSize: (float)t.Controls.GrabPx,
            Colors: colors);
    }

    /// <summary>Writes <see cref="StyleFor"/>(<paramref name="theme"/>) into ImGui's style. Cheap enough
    /// to call every frame of an Age-transition cross-fade.</summary>
    public static void Apply(EraTheme theme)
    {
        StyleSpec s = StyleFor(theme);
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = s.WindowRounding;
        style.ChildRounding = s.ChildRounding;
        style.PopupRounding = s.PopupRounding;
        style.FrameRounding = s.FrameRounding;
        style.GrabRounding = s.GrabRounding;
        style.ScrollbarRounding = s.ScrollbarRounding;
        style.TabRounding = s.TabRounding;
        style.WindowBorderSize = s.WindowBorderSize;
        style.ChildBorderSize = s.ChildBorderSize;
        style.PopupBorderSize = s.PopupBorderSize;
        style.FrameBorderSize = s.FrameBorderSize;
        style.WindowPadding = s.WindowPadding;
        style.FramePadding = s.FramePadding;
        style.ItemSpacing = s.ItemSpacing;
        style.ItemInnerSpacing = s.ItemInnerSpacing;
        style.ScrollbarSize = s.ScrollbarSize;
        style.GrabMinSize = s.GrabMinSize;
        foreach ((ImGuiCol col, Vector4 v) in s.Colors) style.Colors[(int)col] = v;
    }
}
