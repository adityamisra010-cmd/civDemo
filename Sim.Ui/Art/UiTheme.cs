using ImGuiNET;

namespace Sim.Ui.Art;

/// <summary>
/// THE UI SKIN (style-bible §3 typography + §4 item 5 frame furniture): ImGui
/// styled as ink on parchment, and the two OFL faces loaded from
/// assets/fonts/.
///
/// TYPOGRAPHY (§3): EB Garamond — a humanist old-style serif with the engraved
/// feel the bible names — carries settlement labels and panel headers; IBM
/// Plex Serif, a clean lining-figure companion, carries dense numbers and
/// tables, because "readability wins over theme in the data panels". Both are
/// SIL Open Font License 1.1; the license texts ship beside them
/// (assets/fonts/OFL-*.txt) and assets/fonts/README.md records provenance.
///
/// FALLBACK: a missing font file is NOT fatal — ImGui's built-in face is used
/// and the debug panel says so, the same discipline AssetLibrary applies to
/// textures. The map must always open.
/// </summary>
public static class UiTheme
{
    public const string LabelFontFile = "EBGaramond-Variable.ttf";
    public const string NumberFontFile = "IBMPlexSerif-Regular.ttf";

    public sealed record Fonts(ImFontPtr Body, ImFontPtr Header, ImFontPtr Numeric, string Note);

    /// <summary>Loads the bible faces into the ImGui atlas. Call BEFORE the
    /// renderer builds its font texture.</summary>
    public static Fonts LoadFonts(string assetsRoot, float bodyPx = 19f, float headerPx = 25f, float numericPx = 17f)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        string fontDir = Path.Combine(assetsRoot, "fonts");
        string label = Path.Combine(fontDir, LabelFontFile);
        string numeric = Path.Combine(fontDir, NumberFontFile);

        if (!File.Exists(label) && !File.Exists(numeric))
        {
            ImFontPtr fallback = io.Fonts.AddFontDefault();
            return new Fonts(fallback, fallback, fallback,
                $"fonts: DEFAULT (no faces in {fontDir})");
        }

        ImFontPtr body = File.Exists(label)
            ? io.Fonts.AddFontFromFileTTF(label, bodyPx)
            : io.Fonts.AddFontDefault();
        ImFontPtr header = File.Exists(label)
            ? io.Fonts.AddFontFromFileTTF(label, headerPx)
            : body;
        ImFontPtr numbers = File.Exists(numeric)
            ? io.Fonts.AddFontFromFileTTF(numeric, numericPx)
            : body;

        string note = File.Exists(label) && File.Exists(numeric)
            ? "fonts: EB Garamond + IBM Plex Serif (OFL 1.1)"
            : $"fonts: PARTIAL (missing {(File.Exists(label) ? NumberFontFile : LabelFontFile)})";
        return new Fonts(body, header, numbers, note);
    }

    /// <summary>Repaints ImGui in the §2 palette: parchment fields, ink text,
    /// no blue-grey chrome anywhere. Panels are square-cornered and lightly
    /// bordered — a printed plate, not a glassy widget.</summary>
    public static void Apply()
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 0f;
        style.GrabRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 1f;
        style.WindowPadding = new System.Numerics.Vector2(14, 12);
        style.FramePadding = new System.Numerics.Vector2(8, 5);
        style.ItemSpacing = new System.Numerics.Vector2(9, 7);

        Set(ImGuiCol.WindowBg, ParchmentPalette.PaperMid, 0.97f);
        Set(ImGuiCol.ChildBg, ParchmentPalette.PaperLight, 0.55f);
        Set(ImGuiCol.PopupBg, ParchmentPalette.PaperLight, 0.98f);
        Set(ImGuiCol.Border, ParchmentPalette.InkPrimary, 0.75f);
        Set(ImGuiCol.BorderShadow, ParchmentPalette.PaperShade, 0f);
        Set(ImGuiCol.Text, ParchmentPalette.InkPrimary, 1f);
        Set(ImGuiCol.TextDisabled, ParchmentPalette.InkSoft, 0.75f);
        Set(ImGuiCol.TitleBg, ParchmentPalette.PaperShade, 0.95f);
        Set(ImGuiCol.TitleBgActive, ParchmentPalette.PaperShade, 1f);
        Set(ImGuiCol.TitleBgCollapsed, ParchmentPalette.PaperShade, 0.7f);
        Set(ImGuiCol.FrameBg, ParchmentPalette.PaperLight, 0.85f);
        Set(ImGuiCol.FrameBgHovered, ParchmentPalette.PaperLight, 1f);
        Set(ImGuiCol.FrameBgActive, ParchmentPalette.PaperShade, 1f);
        Set(ImGuiCol.Button, ParchmentPalette.PaperLight, 0.9f);
        Set(ImGuiCol.ButtonHovered, ParchmentPalette.Arid, 0.95f);
        Set(ImGuiCol.ButtonActive, ParchmentPalette.PaperShade, 1f);
        Set(ImGuiCol.SliderGrab, ParchmentPalette.InkSoft, 0.9f);
        Set(ImGuiCol.SliderGrabActive, ParchmentPalette.InkPrimary, 1f);
        Set(ImGuiCol.CheckMark, ParchmentPalette.InkPrimary, 1f);
        Set(ImGuiCol.Separator, ParchmentPalette.InkSoft, 0.55f);
        Set(ImGuiCol.ScrollbarBg, ParchmentPalette.PaperShade, 0.35f);
        Set(ImGuiCol.ScrollbarGrab, ParchmentPalette.InkSoft, 0.55f);
        Set(ImGuiCol.ScrollbarGrabHovered, ParchmentPalette.InkSoft, 0.8f);
        Set(ImGuiCol.ScrollbarGrabActive, ParchmentPalette.InkPrimary, 0.9f);
        Set(ImGuiCol.PlotLines, ParchmentPalette.InkPrimary, 0.95f);
        Set(ImGuiCol.PlotLinesHovered, ParchmentPalette.IronRed, 1f);
        Set(ImGuiCol.PlotHistogram, ParchmentPalette.InkSoft, 0.9f);
        Set(ImGuiCol.ResizeGrip, ParchmentPalette.InkSoft, 0.35f);
        Set(ImGuiCol.ResizeGripHovered, ParchmentPalette.InkSoft, 0.6f);
        Set(ImGuiCol.ResizeGripActive, ParchmentPalette.InkPrimary, 0.8f);
    }

    private static void Set(ImGuiCol target, ParchmentPalette.Rgba c, float alpha)
    {
        ImGui.GetStyle().Colors[(int)target] =
            new System.Numerics.Vector4(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
    }
}
