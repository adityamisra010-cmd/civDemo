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

    /// <summary>
    /// THE ATLAS FACES (M5 polish UR-1): every face rasterised at a LADDER of sizes, so a run of any size is set from
    /// the smallest raster at least as large as it — never a raster minified by more than one ladder step (≤ 10 %),
    /// never the 19 px raster shrunk to 11 px as before. The ladder is <see cref="Ladder"/> (the absolute sizes the
    /// DrawList screens still set) plus every size of the <see cref="TypeScale"/> at the UI scale. A face whose file
    /// is missing falls back to EB Garamond's rasters, then to ImGui's built-in face (never fatal).
    /// </summary>
    public sealed class Fonts
    {
        private readonly (float Px, ImFontPtr Font)[][] _faces;
        private readonly ImFontPtr _fallback;

        internal Fonts((float Px, ImFontPtr Font)[][] faces, ImFontPtr fallback, float scale, string note)
        {
            _faces = faces;
            _fallback = fallback;
            Scale = scale;
            Note = note;
        }

        /// <summary>The UI scale the atlas was rasterised for (<see cref="UiScale"/>).</summary>
        public float Scale { get; }

        /// <summary>Provenance for the developer BUILD tab: faces, licence, raster count, atlas size.</summary>
        public string Note { get; }

        /// <summary>The rasterised sizes of <paramref name="face"/>, ascending (empty when its file was missing).</summary>
        public IReadOnlyList<float> Sizes(TypeFace face)
        {
            var sizes = new List<float>();
            foreach ((float px, _) in _faces[(int)face]) sizes.Add(px);
            return sizes;
        }

        /// <summary>The atlas font for <paramref name="face"/> at <paramref name="sizePx"/>: the smallest raster at
        /// least as large (the largest when none is), so text is drawn from a raster of its own size or one ladder
        /// step above it — never minified further.</summary>
        public ImFontPtr Face(TypeFace face, double sizePx)
        {
            (float Px, ImFontPtr Font)[] rasters = _faces[(int)face];
            if (rasters.Length == 0) rasters = _faces[(int)TypeFace.Garamond];
            if (rasters.Length == 0) return _fallback;
            foreach ((float px, ImFontPtr font) in rasters)
                if (px >= sizePx - 0.01) return font;
            return rasters[^1].Font;
        }

        /// <summary>The raster size <see cref="Face"/> picks for (<paramref name="face"/>, <paramref name="sizePx"/>).</summary>
        public float RasterPx(TypeFace face, double sizePx)
        {
            (float Px, ImFontPtr Font)[] rasters = _faces[(int)face];
            if (rasters.Length == 0) rasters = _faces[(int)TypeFace.Garamond];
            if (rasters.Length == 0) return (float)sizePx;
            foreach ((float px, _) in rasters) if (px >= sizePx - 0.01) return px;
            return rasters[^1].Px;
        }

        /// <summary>The font of a <see cref="TypeRole"/> in the face <paramref name="theme"/> sets <paramref name="style"/>
        /// in, at this atlas's UI scale (the ImGui chrome's faces; era-invariant px per face).</summary>
        public ImFontPtr Role(EraTheme theme, TypeRole role, FontRole style = FontRole.Body)
        {
            TextStyle st = theme.Type.For(style);
            return Face(st.Face, RolePx(st.Face, role, st.Case == TextCase.Upper));
        }

        /// <summary>A role's size in px in <paramref name="face"/> at this atlas's scale (rounded to the raster).</summary>
        public float RolePx(TypeFace face, TypeRole role, bool caps = false) => ScaledPx(TypeScale.Px(role, face, caps), Scale);

        /// <summary>The pre-theme faces: EB Garamond body (20 px × s) and title (27 px × s), Plex Serif data (17 × s).</summary>
        public ImFontPtr Body => Face(TypeFace.Garamond, RolePx(TypeFace.Garamond, TypeRole.Body));
        public ImFontPtr Header => Face(TypeFace.Garamond, RolePx(TypeFace.Garamond, TypeRole.Title));
        public ImFontPtr Numeric => Face(TypeFace.PlexSerif, RolePx(TypeFace.PlexSerif, TypeRole.Data));

        /// <summary>The chrome's body, heading and data faces in <paramref name="theme"/>'s typography.</summary>
        public (ImFontPtr Body, ImFontPtr Header, ImFontPtr Numeric) For(EraTheme theme) =>
            (Role(theme, TypeRole.Body), Role(theme, TypeRole.Heading, FontRole.Heading), Role(theme, TypeRole.Data, FontRole.Numeric));
    }

    /// <summary>A type-scale size at UI scale <paramref name="scale"/>, rounded to the whole pixel the atlas holds.</summary>
    public static float ScaledPx(double designPx, double scale) => (float)Math.Round(designPx * scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The absolute raster ladder (px at any UI scale): every whole size from 10 to 24, then steps of at most 10 % to
    /// 40 — the sizes the DrawList screens that still set literal sizes (research, Age, map) ask for, 9.5 to 40 px.
    /// </summary>
    public static IReadOnlyList<float> Ladder { get; } =
        [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26, 27, 29, 31, 34, 37, 40];

    /// <summary>The sizes rasterised for <paramref name="face"/> at UI scale <paramref name="scale"/>: the
    /// <see cref="Ladder"/> and every <see cref="TypeScale"/> size of the face × scale, ascending and distinct.</summary>
    public static float[] RasterSizes(TypeFace face, double scale)
    {
        var all = new List<float>(Ladder);
        foreach (double px in TypeScale.SizesFor(face))
        {
            float s = ScaledPx(px, scale);
            if (!all.Contains(s)) all.Add(s);
        }
        all.Sort();
        return [.. all];
    }

    // THE DESIGN METRICS, named (T4.19 lane D; M5 polish UR-1). ChromeGeometry derives its rects from these; Apply
    // and LoadFonts read the SAME values, so the tested geometry and the styled screen cannot drift apart.
    // ERA-INVARIANT at a given UI scale: the body face changes with the era (Garamond 20 px, Plex 16 px — the same
    // x-height), and the frame padding's height compensates, so every frame is FrameHeightPx tall in every era.
    /// <summary>The body role in EB Garamond at s = 1 (TypeScale Body).</summary>
    public const float BodyFontPx = 20f;
    /// <summary>The title role in EB Garamond at s = 1 (TypeScale Title).</summary>
    public const float HeaderFontPx = 27f;
    /// <summary>The data role in IBM Plex Serif at s = 1 (TypeScale Data).</summary>
    public const float NumericFontPx = 17f;
    public static readonly Vector2 WindowPaddingPx = new(14, 12);
    /// <summary>The frame padding at s = 1 under the Garamond body face (the A1–A7 eras); the Plex eras pad more
    /// vertically so the frame height stays <see cref="FrameHeightPx"/> (<see cref="FramePaddingY"/>).</summary>
    public static readonly Vector2 FramePaddingPx = new(8, 5);
    public static readonly Vector2 ItemSpacingPx = new(9, 7);

    /// <summary>ImGui's GetFrameHeight() at s = 1 in every era: body px + 2 × FramePadding.y = 30 px. The renderer
    /// still passes its MEASURED frame height into ChromeGeometry (fonts can fall back); this is the design value the
    /// headless tests pin against.</summary>
    public const float FrameHeightPx = BodyFontPx + 2f * 5f;

    /// <summary>The body face's raster px in <paramref name="theme"/> at UI scale <paramref name="scale"/>.</summary>
    public static float BodyPx(EraTheme theme, double scale = 1.0) =>
        ScaledPx(TypeScale.Px(TypeRole.Body, theme.Type.Body.Face), scale);

    /// <summary>The vertical frame padding that keeps every era's frame <see cref="FrameHeightPx"/> × scale tall.</summary>
    public static float FramePaddingY(EraTheme theme, double scale = 1.0) =>
        Math.Max(2f, (FrameHeightPx * (float)scale - BodyPx(theme, scale)) / 2f);

    // The atlas glyph ranges must outlive the atlas build: pinned once for the process.
    private static readonly ushort[] Ranges = UiText.GlyphRanges();
    private static GCHandle _rangesHandle;

    private static IntPtr RangesPtr()
    {
        if (!_rangesHandle.IsAllocated) _rangesHandle = GCHandle.Alloc(Ranges, GCHandleType.Pinned);
        return _rangesHandle.AddrOfPinnedObject();
    }

    private static readonly string[] FaceFiles = [LabelFontFile, NumberFontFile, SansFontFile];

    /// <summary>Loads the faces into the ImGui atlas (Latin-1 plus the typographic code points of
    /// <see cref="UiText"/>) at every size of <see cref="RasterSizes"/> for UI scale <paramref name="scale"/>, BUILDS
    /// it, and applies the text boundary's glyph remaps. Call BEFORE the renderer uploads the atlas texture (it then
    /// finds the atlas built and keeps the remaps); to change the scale, <see cref="ImFontAtlasPtr.Clear"/> the atlas
    /// and load again.</summary>
    public static Fonts LoadFonts(string assetsRoot, double scale = 1.0)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        string fontDir = Path.Combine(assetsRoot, "fonts");
        IntPtr ranges = RangesPtr();
        var faces = new (float, ImFontPtr)[3][];
        int count = 0;
        var missing = new List<string>();
        for (int f = 0; f < 3; f++)
        {
            string file = Path.Combine(fontDir, FaceFiles[f]);
            if (!File.Exists(file)) { faces[f] = []; missing.Add(FaceFiles[f]); continue; }
            float[] sizes = RasterSizes((TypeFace)f, scale);
            faces[f] = new (float, ImFontPtr)[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                ImFontConfigPtr cfg;
                unsafe { cfg = ImGuiNative.ImFontConfig_ImFontConfig(); }
                // Horizontal oversampling keeps small runs crisp at fractional x; large runs do not need it, and
                // skipping it there halves their share of the atlas.
                cfg.OversampleH = sizes[i] < 20f ? 2 : 1;
                cfg.OversampleV = 1;
                faces[f][i] = (sizes[i], io.Fonts.AddFontFromFileTTF(file, sizes[i], cfg, ranges));
                unsafe { ImGuiNative.ImFontConfig_destroy(cfg.NativePtr); }
                count++;
            }
        }
        ImFontPtr fallback = count == 0 ? io.Fonts.AddFontDefault() : faces[0].Length > 0 ? faces[0][0].Item2 : FirstOf(faces);
        // ImGui's default face (anything drawn before a font is pushed — its own tooltips, error windows) is the
        // body role, not the ladder's smallest raster.
        var fonts = new Fonts(faces, fallback, (float)scale, "");
        unsafe { io.NativePtr->FontDefault = (count == 0 ? fallback : fonts.Body).NativePtr; }
        io.Fonts.Build();
        ApplyTextBoundary(io.Fonts);
        string note = count == 0
            ? $"fonts: DEFAULT (no faces in {fontDir})"
            : (missing.Count == 0 ? "fonts: EB Garamond + IBM Plex Serif + IBM Plex Sans (OFL 1.1)" : "fonts: PARTIAL (missing " + string.Join(", ", missing) + ")")
              + string.Create(System.Globalization.CultureInfo.InvariantCulture,
                  $"; {count} rasters at UI scale {scale:0.###}, atlas {io.Fonts.TexWidth}x{io.Fonts.TexHeight}");
        return new Fonts(faces, fallback, (float)scale, note);
    }

    private static ImFontPtr FirstOf((float, ImFontPtr)[][] faces)
    {
        foreach ((float, ImFontPtr)[] f in faces) if (f.Length > 0) return f[0].Item2;
        return default;
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
    public static StyleSpec StyleFor(EraTheme t, double scale = 1.0)
    {
        float k = (float)scale;
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
        ParchmentPalette.Rgba pressed = ThemeColor.Mix(m.PanelRaised, m.Accent, 0.34);   // light enough for the dark ink
        ParchmentPalette.Rgba header = ThemeColor.Mix(m.Panel, m.Accent, 0.20);
        var colors = new List<(ImGuiCol, Vector4)>
        {
            (ImGuiCol.Text, V(ink.Text, 1.0)),
            (ImGuiCol.TextDisabled, V(ink.TextDim, 1.0)),   // UR-1: the floored dim ink, opaque (was α 0.85: 2.83:1 at A6)
            // Transparent: every chrome window's surface is painted by its era frame
            // (ChromeFurniture, SimUiGame.DrawPanelFurniture), whose shape is the era's — a hand-cut
            // slab, a tablet, a plaque — not ImGui's rectangle.
            (ImGuiCol.WindowBg, V(m.Panel, 0.0)),
            (ImGuiCol.ChildBg, V(m.PanelRaised, 0.45)),
            (ImGuiCol.PopupBg, V(m.PanelRaised, 0.98)),
            (ImGuiCol.Border, V(m.Border, 0.75)),
            (ImGuiCol.BorderShadow, V(m.PanelSunken, 0.0)),
            (ImGuiCol.FrameBg, V(m.PanelRaised, 0.85)),
            (ImGuiCol.FrameBgHovered, V(hover, 1.0)),
            (ImGuiCol.FrameBgActive, V(pressed, 1.0)),
            (ImGuiCol.TitleBg, V(m.PanelSunken, 0.95)),
            (ImGuiCol.TitleBgActive, V(m.PanelSunken, 1.0)),
            (ImGuiCol.TitleBgCollapsed, V(m.PanelSunken, 0.7)),
            (ImGuiCol.MenuBarBg, V(m.PanelSunken, 1.0)),
            (ImGuiCol.ScrollbarBg, V(m.PanelSunken, 0.35)),
            (ImGuiCol.ScrollbarGrab, V(ink.TextSoft, 0.55)),
            (ImGuiCol.ScrollbarGrabHovered, V(ink.TextSoft, 0.8)),
            (ImGuiCol.ScrollbarGrabActive, V(ink.Text, 0.9)),
            (ImGuiCol.CheckMark, V(ink.Text, 1.0)),
            (ImGuiCol.SliderGrab, V(ink.TextSoft, 0.9)),
            (ImGuiCol.SliderGrabActive, V(ink.Text, 1.0)),
            (ImGuiCol.Button, V(m.PanelRaised, 0.92)),
            (ImGuiCol.ButtonHovered, V(hover, 0.98)),
            (ImGuiCol.ButtonActive, V(pressed, 1.0)),
            (ImGuiCol.Header, V(header, 0.55)),
            (ImGuiCol.HeaderHovered, V(header, 0.85)),
            (ImGuiCol.HeaderActive, V(ThemeColor.Mix(m.Panel, m.Accent, 0.32), 1.0)),
            (ImGuiCol.Separator, V(m.Hairline, 0.8)),
            (ImGuiCol.SeparatorHovered, V(m.Accent, 0.8)),
            (ImGuiCol.SeparatorActive, V(m.Accent, 1.0)),
            (ImGuiCol.ResizeGrip, V(ink.TextSoft, 0.35)),
            (ImGuiCol.ResizeGripHovered, V(ink.TextSoft, 0.6)),
            (ImGuiCol.ResizeGripActive, V(ink.Text, 0.8)),
            (ImGuiCol.Tab, V(m.PanelSunken, 0.9)),
            (ImGuiCol.TabHovered, V(hover, 1.0)),
            (ImGuiCol.TabSelected, V(m.PanelRaised, 1.0)),
            (ImGuiCol.TabSelectedOverline, V(m.Accent, 1.0)),
            (ImGuiCol.TabDimmed, V(m.PanelSunken, 0.7)),
            (ImGuiCol.TabDimmedSelected, V(m.Panel, 1.0)),
            (ImGuiCol.TabDimmedSelectedOverline, V(m.Accent, 0.6)),
            (ImGuiCol.PlotLines, V(ink.Text, 0.95)),
            (ImGuiCol.PlotLinesHovered, V(t.Semantic.Danger, 1.0)),
            (ImGuiCol.PlotHistogram, V(ink.TextSoft, 0.9)),
            (ImGuiCol.PlotHistogramHovered, V(m.Accent, 1.0)),
            (ImGuiCol.TableHeaderBg, V(m.PanelSunken, 1.0)),
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
        // UR-1: every metric is a design px × the UI scale; the vertical frame padding is the era body face's
        // complement to the era-invariant frame height (FramePaddingY).
        return new StyleSpec(
            WindowRounding: round * k, ChildRounding: round * k, PopupRounding: round * k, FrameRounding: round * 0.75f * k,
            GrabRounding: round * 0.75f * k, ScrollbarRounding: round * k, TabRounding: round * 0.75f * k,
            WindowBorderSize: 0f, ChildBorderSize: 0f, PopupBorderSize: 1f,
            FrameBorderSize: (float)Math.Clamp(t.Edge.BorderPx * 0.6, 1.0, 1.6),
            WindowPadding: WindowPaddingPx * k,
            FramePadding: new Vector2(frameX * k, FramePaddingY(t, scale)),
            ItemSpacing: itemSpacing * k,
            ItemInnerSpacing: new Vector2((7f - level * 0.4f) * k, 4f * k),
            ScrollbarSize: (10f + 4f * grain - level * 0.4f) * k,
            GrabMinSize: (float)t.Controls.GrabPx * k,
            Colors: colors);
    }

    /// <summary>Writes <see cref="StyleFor"/>(<paramref name="theme"/>) into ImGui's style. Cheap enough
    /// to call every frame of an Age-transition cross-fade.</summary>
    public static void Apply(EraTheme theme, double scale = 1.0)
    {
        StyleSpec s = StyleFor(theme, scale);
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
