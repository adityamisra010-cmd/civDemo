using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Trees.View;

/// <summary>A clickable region and the UI action a click produces. The topmost (last
/// added) region under the pointer wins.</summary>
public sealed record Hit(RectD Rect, TreesAction Action, string Tooltip = "");

/// <summary>The rectangles of the screen for a viewport, in design pixels. Pure geometry.</summary>
public sealed record TreesScreenLayout(RectD Screen, RectD Header, RectD Toolbar, RectD Nav, RectD Canvas, RectD Detail, RectD StatusLine)
{
    public const double HeaderH = 58.0;
    public const double ToolbarH = 38.0;
    public const double NavW = 184.0;
    public const double DetailW = 336.0;
    public const double StatusH = 22.0;

    public static TreesScreenLayout For(double width, double height)
    {
        var screen = new RectD(0, 0, width, height);
        var header = new RectD(0, 0, width, HeaderH);
        double top = HeaderH;
        var toolbar = new RectD(NavW, top, width - NavW - DetailW, ToolbarH);
        var nav = new RectD(0, top, NavW, height - top);
        var detail = new RectD(width - DetailW, top, DetailW, height - top);
        var canvas = new RectD(NavW, top + ToolbarH, width - NavW - DetailW, height - top - ToolbarH - StatusH);
        var status = new RectD(NavW, height - StatusH, width - NavW - DetailW, StatusH);
        return new TreesScreenLayout(screen, header, toolbar, nav, canvas, detail, status);
    }
}

/// <summary>Everything one frame of the screen reads. All of it is read-only input; the
/// UI state is the only mutable thing and the painter does not mutate it.</summary>
public sealed record TreesFrameInput(
    TreesContentSet Content, TreeGraph Graph, TreeLayoutResult Layout,
    TreesStateSnapshot Trees, AgeStateSnapshot Ages, GallerySnapshot Gallery,
    TreesUiState Ui, StateAnimator Animator, double Now, double Width, double Height,
    ITextMeasure Measure, SessionContext? Session = null, string? PreviewStepLabel = null,
    int PreviewStep = 0, int PreviewStepCount = 0, IReadOnlyList<ContentDiagnostic>? Diagnostics = null,
    AgeForwardGuard? AgeGuard = null)
{
    /// <summary>The Age to show as current: the forward-only guard's, else the report's.</summary>
    public string CurrentAgeId => AgeGuard?.DisplayedAgeId ?? Ages.CurrentAgeId;
}

/// <summary>One painted frame: the draw list, the click regions, and where the canvas and
/// the search box are (the host routes drag/wheel and text input to them).</summary>
public sealed record TreesFrame(DrawList Draw, IReadOnlyList<Hit> Hits, TreesScreenLayout Layout, RectD SearchBox, TreesView? View,
    double DetailContentHeight = 0)
{
    /// <summary>The action under a point, topmost first, or null.</summary>
    public TreesAction? HitAt(double x, double y)
    {
        for (int i = Hits.Count - 1; i >= 0; i--) if (Hits[i].Rect.Contains(x, y)) return Hits[i].Action;
        return null;
    }
}

/// <summary>
/// THE TREES SCREEN — composes one frame (header, then the active tab) from content,
/// reported state and UI state into a backend-agnostic draw list. Pure: the same input
/// paints the same commands, which is how the headless tests and the SVG preview see
/// exactly what the game draws.
/// </summary>
public static class TreesScreen
{
    public static TreesFrame Paint(TreesFrameInput input)
    {
        // The painter never writes UI state. A camera that has never been placed is fitted
        // by the host (TreesHost) before the first paint.
        var dl = new DrawList();
        var hits = new List<Hit>();
        TreesScreenLayout lay = TreesScreenLayout.For(input.Width, input.Height);
        dl.Rect(lay.Screen, ParchmentPalette.PaperMid);

        TreesView? view = null;
        RectD search = default;
        double detail = 0;
        switch (input.Ui.Tab)
        {
            case TreesTab.Trees:
                view = TreesQuery.Evaluate(input.Graph, input.Trees, input.Ui, input.Content.Ages);
                (search, detail) = TreesCanvasPainter.Paint(dl, hits, input, lay, view);
                break;
            case TreesTab.Ages:
                AgesPainter.Paint(dl, hits, input, lay);
                break;
            case TreesTab.Gallery:
                GalleryPainter.Paint(dl, hits, input, lay);
                break;
        }
        PaintHeader(dl, hits, input, lay);
        return new TreesFrame(dl, hits, lay, search, view, detail);
    }

    // --- the header: civilization / Age / progression context ------------------------------

    private static void PaintHeader(DrawList dl, List<Hit> hits, TreesFrameInput input, TreesScreenLayout lay)
    {
        RectD h = lay.Header;
        dl.Rect(h, ParchmentPalette.InkPrimary);
        dl.Line(h.X, h.Bottom - 1.5, h.Right, h.Bottom - 1.5, ParchmentPalette.GoldLeaf, 2.0);
        ITextMeasure m = input.Measure;
        Rgba paper = ParchmentPalette.PaperLight;
        Rgba paperSoft = Ink.With(ParchmentPalette.PaperLight, 0.72);

        dl.Circle(34, 29, 21, ParchmentPalette.PaperLight);
        dl.Glyph(14, 9, 40, new GlyphSpec(GlyphBase.Emblem, GlyphState.Complete, SizeClass.Px48, Domain: GlyphDomain.Links), 1.0);
        dl.Text(64, 8, "THE TREES", 21, ParchmentPalette.GoldLeaf, TextAlign.Left, FontRole.Caps);
        string civ = input.Trees.CivilizationName;
        dl.Text(64, 33, Ink.Fit(m, civ, 13, 170), 13, paperSoft);

        // Age chip.
        AgesDocument ages = input.Content.Ages;
        double x = 250;
        if (ages.TryAge(input.CurrentAgeId, out AgeDef age))
        {
            AgeMotion am = input.Animator.Age(input.Now);
            var star = new GlyphSpec(age.IconBase, input.Ages.Transition?.Pending == true ? GlyphState.InProgress : GlyphState.Complete,
                SizeClass.Px32, Domain: age.IconMark, Era: age.Theme.Register,
                Maturity: input.Ages.CurrentProgress ?? 0.0, Progress: input.Ages.CurrentProgress ?? 0.0);
            dl.Circle(x + 17, 29, 18.5, ParchmentPalette.PaperLight);
            dl.Glyph(x, 12, 34, star);
            if (am.EnteredFlashT >= 0) dl.Circle(x + 17, 29, 17 + 14 * am.EnteredFlashT, null, Ink.With(ParchmentPalette.GoldLeaf, 1 - am.EnteredFlashT), 2.5);
            dl.Text(x + 42, 9, age.DisplayName.ToUpperInvariant(), 15, paper, TextAlign.Left, FontRole.Caps);
            if (input.Ages.Transition?.Pending == true && input.Ages.Transition.ToAgeId is string to && ages.TryAge(to, out AgeDef next))
                dl.Text(x + 48 + m.Width(age.DisplayName.ToUpperInvariant(), 15, FontRole.Caps), 10, "→ " + next.DisplayName.ToUpperInvariant(),
                    12.5, ParchmentPalette.GoldLeaf, TextAlign.Left, FontRole.Caps);
            string progress = input.Ages.CurrentProgress is double p ? $"{p * 100:0} %" : "— %";
            dl.Text(x + 42, 31, Ink.Fit(m, $"progress {progress}", 12.5, 170), 12.5, paperSoft, TextAlign.Left, FontRole.Numeric);
            dl.Bar(new RectD(x + 42, 48, 150, 4), input.Ages.CurrentProgress ?? 0.0, ParchmentPalette.GoldLeaf, Ink.With(paper, 0.5));
            hits.Add(new Hit(new RectD(x, 6, 200, 46), new SetTabAction(TreesTab.Ages), "Open the Age view"));
        }

        // Research line: reported, never computed.
        x = 470;
        ResearchHeader r = input.Trees.Research;
        string pts = r.PointsPerTurn is long ppt ? $"{ppt} RP / turn" : "RP / turn —";
        dl.Text(x, 9, "RESEARCH", 11, Ink.With(ParchmentPalette.GoldLeaf, 0.9), TextAlign.Left, FontRole.Caps);
        dl.Text(x + 74, 8, pts, 13, paper, TextAlign.Left, FontRole.Numeric);
        if (r.CurrentTarget is not null && input.Graph.TryIndexOf(r.CurrentTarget, out int ti))
        {
            NodeStatus? st = input.Trees.Status(r.CurrentTarget);
            string pct = st?.ResearchProgress is double rp ? $"{rp * 100:0} %" : "";
            string line = Ink.Fit(m, $"Researching: {input.Graph.Node(ti).Name} {pct}", 13, 260);
            dl.Text(x, 31, line, 13, paperSoft);
            hits.Add(new Hit(new RectD(x, 28, 260, 22), new OpenNodeAction(r.CurrentTarget), "Show the research target"));
        }
        else dl.Text(x, 31, "Researching: —", 13, paperSoft);

        // Session context (the live simulation's own clock, read-only), when hosted in the game.
        if (input.Session is SessionContext sc)
            dl.Text(x + 290, 31, $"turn {sc.Turn} · year {sc.WorldYear:0} · Δt {sc.DtYears:0.##} y", 12, paperSoft, TextAlign.Left, FontRole.Numeric);

        // Tabs and close.
        string[] names = ["THE TREES", "AGES", "GALLERY"];
        TreesTab[] tabs = [TreesTab.Trees, TreesTab.Ages, TreesTab.Gallery];
        double tx = h.Right - 44 - 3 * 104;
        for (int i = 0; i < 3; i++)
        {
            var rr = new RectD(tx + i * 104, 12, 100, 34);
            bool active = input.Ui.Tab == tabs[i];
            if (active) dl.Rect(rr, Ink.With(ParchmentPalette.PaperLight, 0.12), null, 1, 3);
            dl.Text(rr.CenterX, rr.Y + 8, names[i], 13, active ? ParchmentPalette.GoldLeaf : paperSoft, TextAlign.Center, FontRole.Caps);
            if (active) dl.Line(rr.X + 12, rr.Bottom - 3, rr.Right - 12, rr.Bottom - 3, ParchmentPalette.GoldLeaf, 2);
            hits.Add(new Hit(rr, new SetTabAction(tabs[i]), names[i]));
        }
        var close = new RectD(h.Right - 38, 14, 28, 28);
        dl.Line(close.X + 8, close.Y + 8, close.Right - 8, close.Bottom - 8, paperSoft, 2);
        dl.Line(close.Right - 8, close.Y + 8, close.X + 8, close.Bottom - 8, paperSoft, 2);
        hits.Add(new Hit(close, new CloseAction(), "Close (Esc)"));
    }
}

/// <summary>Shared panel furniture for the three tabs.</summary>
internal static class Chrome
{
    public const double Body = 14.0;
    public const double Small = 11.5;
    public const double Caps = 11.0;
    public const double Heading = 19.0;

    public static void Panel(DrawList dl, RectD r, bool light = true)
    {
        dl.Rect(r, light ? ParchmentPalette.PaperLight : ParchmentPalette.PaperMid);
        dl.Line(r.X, r.Y, r.X, r.Bottom, Ink.With(ParchmentPalette.InkSoft, 0.55), 1);
    }

    public static double SectionTitle(DrawList dl, double x, double y, double w, string text)
    {
        dl.Text(x, y, text.ToUpperInvariant(), Caps, ParchmentPalette.InkSoft, TextAlign.Left, FontRole.Caps);
        dl.Line(x, y + 16, x + w, y + 16, Ink.With(ParchmentPalette.InkSoft, 0.35), 1);
        return y + 22;
    }

    public static void Button(DrawList dl, List<Hit> hits, RectD r, string label, bool active, TreesAction action,
        string tooltip = "", bool enabled = true)
    {
        Rgba fill = active ? ParchmentPalette.InkPrimary : ParchmentPalette.PaperLight;
        Rgba text = active ? ParchmentPalette.PaperLight : enabled ? ParchmentPalette.InkPrimary : Ink.With(ParchmentPalette.InkSoft, 0.5);
        dl.Rect(r, fill, Ink.With(ParchmentPalette.InkSoft, enabled ? 0.7 : 0.3), 1, 3);
        dl.Text(r.CenterX, r.Y + (r.H - 13) / 2 - 1, label, 12.5, text, TextAlign.Center, FontRole.Body);
        if (enabled) hits.Add(new Hit(r, action, tooltip));
    }

    public static double Chip(DrawList dl, List<Hit> hits, ITextMeasure m, double x, double y, string label, bool active,
        TreesAction action, double h = 22)
    {
        double w = m.Width(label, 12, FontRole.Body) + 16;
        Button(dl, hits, new RectD(x, y, w, h), label, active, action);
        return w;
    }

    /// <summary>A "PLACEHOLDER" / "DEMO" tag.</summary>
    public static double Tag(DrawList dl, ITextMeasure m, double x, double y, string text, Rgba color)
    {
        double w = m.Width(text, 10, FontRole.Caps) + 10;
        dl.Rect(new RectD(x, y, w, 16), Ink.With(color, 0.12), Ink.With(color, 0.8), 1, 2);
        dl.Text(x + w / 2, y + 2, text, 10, color, TextAlign.Center, FontRole.Caps);
        return w;
    }

    public static void Wrapped(DrawList dl, ITextMeasure m, ref double y, double x, double w, string text, double size, Rgba color,
        double lineH = 0, double maxY = double.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        double lh = lineH > 0 ? lineH : size * 1.3;
        foreach (string line in Ink.Wrap(m, text, size, w))
        {
            if (y > maxY) return;
            dl.Text(x, y, line, size, color);
            y += lh;
        }
    }

    /// <summary>A checklist mark: ✓ complete, ~ partial, empty not started.</summary>
    public static void Check(DrawList dl, double x, double y, MilestoneCompletion c)
    {
        var box = new RectD(x, y, 15, 15);
        dl.Rect(box, c == MilestoneCompletion.Complete ? ParchmentPalette.InkPrimary : ParchmentPalette.PaperLight, ParchmentPalette.InkPrimary, 1.2, 2);
        if (c == MilestoneCompletion.Complete)
        {
            dl.Line(x + 3.5, y + 7.5, x + 6.5, y + 11, ParchmentPalette.PaperLight, 2);
            dl.Line(x + 6.5, y + 11, x + 12, y + 4, ParchmentPalette.PaperLight, 2);
        }
        else if (c == MilestoneCompletion.Partial)
        {
            dl.Bezier((x + 3, y + 8.5), (x + 5.5, y + 4.5), (x + 9.5, y + 11.5), (x + 12, y + 7.5), ParchmentPalette.InkPrimary, 1.8);
        }
    }
}
