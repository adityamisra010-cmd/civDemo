using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Trees.View;

/// <summary>
/// THE TREES TAB — toolbar, lens navigation, the graph canvas, and the details / legend
/// panel. Every node, edge, band and legend entry is generated from content and reported
/// state; nothing here names a technology.
/// </summary>
internal static class TreesCanvasPainter
{
    private static readonly Rgba InkP = ParchmentPalette.InkPrimary;
    private static readonly Rgba InkS = ParchmentPalette.InkSoft;
    private static readonly Rgba Gold = ParchmentPalette.GoldLeaf;
    private static readonly Rgba Path = ParchmentPalette.Verdigris;

    /// <summary>Paints the tab; returns the search box and the detail content height.</summary>
    public static (RectD Search, double DetailHeight) Paint(DrawList dl, List<Hit> hits, TreesFrameInput input, TreesScreenLayout lay, TreesView view)
    {
        TreesUiState ui = input.Ui;
        PaintCanvas(dl, hits, input, lay.Canvas, view);
        RectD search = PaintToolbar(dl, hits, input, lay);
        PaintStatus(dl, input, lay.StatusLine, view);
        PaintNav(dl, hits, input, lay.Nav);
        double detail = 0;
        if (ui.LegendOpen || ui.Selected is null) TreesPanels.Legend(dl, hits, input, lay.Detail);
        else detail = TreesPanels.Details(dl, hits, input, lay.Detail, view);
        return (search, detail);
    }

    // --- toolbar -------------------------------------------------------------------------------

    private static RectD PaintToolbar(DrawList dl, List<Hit> hits, TreesFrameInput input, TreesScreenLayout lay)
    {
        RectD t = lay.Toolbar;
        TreesUiState ui = input.Ui;
        ITextMeasure m = input.Measure;
        dl.Rect(t, ParchmentPalette.PaperMid);
        dl.Line(t.X, t.Bottom - 0.5, t.Right, t.Bottom - 0.5, Ink.With(InkS, 0.4), 1);

        // Search box (the host puts a text input over it).
        var search = new RectD(t.X + 10, t.Y + 7, 176, 24);
        dl.Rect(search, ParchmentPalette.PaperLight, Ink.With(InkS, 0.8), 1, 3);
        dl.Circle(search.X + 12, search.Y + 11, 5, null, InkS, 1.4);
        dl.Line(search.X + 15.5, search.Y + 14.5, search.X + 19, search.Y + 18, InkS, 1.6);
        dl.Text(search.X + 26, search.Y + 4, ui.Search.Length > 0 ? Ink.Fit(m, ui.Search, 13, 140) : "Search nodes…", 13,
            ui.Search.Length > 0 ? InkP : Ink.With(InkS, 0.7));
        hits.Add(new Hit(search, new FocusSearchAction(), "Search by name or id"));

        double x = search.Right + 12;
        bool sel = ui.Selected is not null;
        (string Label, FocusMode Mode)[] focus = [("Prerequisites", FocusMode.Prerequisites), ("Downstream", FocusMode.Downstream), ("Lineage", FocusMode.Lineage)];
        foreach ((string label, FocusMode mode) in focus)
        {
            double w = m.Width(label, 12.5, FontRole.Body) + 18;
            Chrome.Button(dl, hits, new RectD(x, t.Y + 7, w, 24), label, ui.Focus == mode, new SetFocusAction(mode),
                "Focus on the selected node's " + label.ToLowerInvariant(), enabled: sel);
            x += w + 4;
        }
        x += 8;
        string mode2 = ui.FilterMode == FilterMode.Dim ? "Dim" : "Hide";
        Chrome.Button(dl, hits, new RectD(x, t.Y + 7, 58, 24), mode2, false,
            new SetFilterModeAction(ui.FilterMode == FilterMode.Dim ? FilterMode.Hide : FilterMode.Dim), "Filtered nodes: dim or hide");
        x += 62;
        string typeLabel = ui.TypeFilter is null ? "Type: all" : "Type: " + input.Content.Trees.NodeType(ui.TypeFilter).Name;
        double tw = System.Math.Min(170, m.Width(typeLabel, 12.5, FontRole.Body) + 18);
        Chrome.Button(dl, hits, new RectD(x, t.Y + 7, tw, 24), Ink.Fit(m, typeLabel, 12.5, tw - 12), ui.TypeFilter is not null,
            new SetTypeFilterAction(NextType(input.Content.Trees, ui.TypeFilter)), "Cycle the node-type filter");
        x += tw + 4;
        Chrome.Button(dl, hits, new RectD(x, t.Y + 7, 52, 24), "Clear", false, new ClearFiltersAction(), "Clear filters, search and focus",
            enabled: ui.AnyFilter || ui.Focus != FocusMode.None || ui.Lens is not null);

        // Zoom.
        double zx = t.Right - 3 * 30 - 8;
        Chrome.Button(dl, hits, new RectD(zx, t.Y + 7, 26, 24), "−", false, new ZoomAction(1 / 1.25), "Zoom out");
        Chrome.Button(dl, hits, new RectD(zx + 30, t.Y + 7, 26, 24), "+", false, new ZoomAction(1.25), "Zoom in");
        Chrome.Button(dl, hits, new RectD(zx + 60, t.Y + 7, 26, 24), "⤢", false, new FitAction(), "Fit the graph");
        return search;
    }

    private static string? NextType(TreesDocument c, string? current)
    {
        if (current is null) return c.NodeTypes[0].Id;
        int i = c.NodeTypes.ToList().FindIndex(t => t.Id == current);
        return i < 0 || i + 1 >= c.NodeTypes.Count ? null : c.NodeTypes[i + 1].Id;
    }

    private static void PaintStatus(DrawList dl, TreesFrameInput input, RectD r, TreesView view)
    {
        dl.Rect(r, ParchmentPalette.PaperMid);
        dl.Line(r.X, r.Y + 0.5, r.Right, r.Y + 0.5, Ink.With(InkS, 0.35), 1);
        string left = $"zoom {input.Ui.Camera.Zoom * 100:0} % · {view.VisibleCount}/{view.Nodes.Count} nodes shown";
        if (view.ActiveFilters.Count > 0) left += " · " + string.Join(" · ", view.ActiveFilters);
        dl.Text(r.X + 10, r.Y + 4, Ink.Fit(input.Measure, left, 11.5, r.W - 240), 11.5, InkS, TextAlign.Left, FontRole.Numeric);
        dl.Text(r.Right - 10, r.Y + 4, "drag to pan · wheel to zoom · click to select", 11.5, Ink.With(InkS, 0.75), TextAlign.Right);
    }

    // --- the lens navigation ----------------------------------------------------------------

    private static void PaintNav(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD r)
    {
        dl.Rect(r, ParchmentPalette.PaperLight);
        dl.Line(r.Right - 0.5, r.Y, r.Right - 0.5, r.Bottom, Ink.With(InkS, 0.55), 1);
        TreesUiState ui = input.Ui;
        TreeGraph g = input.Graph;
        double x = r.X + 10, w = r.W - 20;
        double y = Chrome.SectionTitle(dl, x, r.Y + 10, w, "Lenses");

        void LensRow(string? lensId, string name, GlyphDomain mark, int total, int done)
        {
            var row = new RectD(r.X + 4, y, r.W - 8, 30);
            bool active = ui.Lens == lensId;
            if (active) dl.Rect(row, Ink.With(Gold, 0.16), Ink.With(Gold, 0.9), 1, 3);
            dl.Glyph(row.X + 4, row.Y + 3, 24, new GlyphSpec(GlyphBase.Emblem, active ? GlyphState.Complete : GlyphState.Available,
                SizeClass.Px24, Domain: mark));
            dl.Text(row.X + 34, row.Y + 6, name, 13.5, active ? InkP : Ink.With(InkP, 0.85));
            dl.Text(row.Right - 6, row.Y + 8, $"{done}/{total}", 11, InkS, TextAlign.Right, FontRole.Numeric);
            hits.Add(new Hit(row, new SetLensAction(lensId), lensId is null ? "The whole graph" : "Focus the " + name + " lens"));
            y += 32;
        }

        int Done(IEnumerable<int> nodes) => nodes.Count(i =>
            TreesQueryState.Resolve(g.Content, g.Node(i), input.Trees.Status(g.Node(i).Id)).State.ResearchRole == ResearchRole.Completed);
        LensRow(null, "Civilization", GlyphDomain.Links, g.Count, Done(Enumerable.Range(0, g.Count)));
        foreach (LensDef l in g.Content.LensesInOrder())
        {
            int[] members = Enumerable.Range(0, g.Count).Where(i => g.InLens(i, l.Id)).ToArray();
            LensRow(l.Id, l.Name, l.Mark, members.Length, Done(members));
        }

        // Age filter.
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Age filter");
        double cx = x;
        void AgeChip(string label, string? id)
        {
            double cw = input.Measure.Width(label, 12, FontRole.Body) + 14;
            if (cx + cw > r.Right - 8) { cx = x; y += 26; }
            Chrome.Button(dl, hits, new RectD(cx, y, cw, 22), label, ui.AgeFilter == id, new SetAgeFilterAction(id));
            cx += cw + 4;
        }
        AgeChip("All", null);
        foreach (AgeDef a in input.Content.Ages.InOrder()) AgeChip(a.Id.Replace("AGE_", ""), a.Id);
        y += 30;

        // Age context: current Age, progress, relevant milestones.
        y = Chrome.SectionTitle(dl, x, y + 4, w, "Age context");
        AgesDocument ages = input.Content.Ages;
        if (ages.TryAge(input.CurrentAgeId, out AgeDef age))
        {
            dl.Glyph(x, y, 32, new GlyphSpec(age.IconBase, GlyphState.Complete, SizeClass.Px32, Domain: age.IconMark,
                Era: age.Theme.Register, Maturity: input.Ages.CurrentProgress ?? 0.0));
            dl.Text(x + 40, y + 1, age.DisplayName, 14, InkP, TextAlign.Left, FontRole.Heading);
            string pct = input.Ages.CurrentProgress is double p ? $"{p * 100:0} %" : "—";
            dl.Text(x + 40, y + 18, $"progress {pct}", 11.5, InkS, TextAlign.Left, FontRole.Numeric);
            dl.Bar(new RectD(x, y + 38, w, 5), input.Ages.CurrentProgress ?? 0.0, Gold, Ink.With(InkS, 0.6), ParchmentPalette.PaperMid);
            IReadOnlyList<MilestoneDef> ms = ages.MilestonesOf(age.Id);
            int mand = ms.Count(q => q.Mandatory), mandDone = ms.Count(q => q.Mandatory && input.Ages.Milestone(q.Id).Completion == MilestoneCompletion.Complete);
            dl.Text(x, y + 48, $"mandatory milestones {mandDone}/{mand}", 11.5, InkS, TextAlign.Left, FontRole.Numeric);
            y += 66;
            // Milestones that reference the selected node (relevant milestone status).
            if (ui.Selected is int s)
            {
                string id = g.Node(s).Id;
                foreach (MilestoneDef md in ages.Milestones.Where(q => q.NodeRefs.Contains(id)).Take(3))
                {
                    Chrome.Check(dl, x, y, input.Ages.Milestone(md.Id).Completion);
                    dl.Text(x + 20, y, Ink.Fit(input.Measure, md.Name, 12, w - 20), 12, InkP);
                    hits.Add(new Hit(new RectD(x, y, w, 18), new SelectMilestoneAction(md.Id), "Milestone"));
                    y += 20;
                }
            }
            Chrome.Button(dl, hits, new RectD(x, y + 2, w, 22), "Open the Age view ▸", false, new SetTabAction(TreesTab.Ages));
            y += 30;
        }

        // Source: which state source is being shown, and the placeholder preview controls.
        PaintSource(dl, hits, input, new RectD(r.X, System.Math.Max(y + 6, r.Bottom - 118), r.W, 118));
    }

    internal static void PaintSource(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD r)
    {
        double x = r.X + 10, w = r.W - 20;
        double y = Chrome.SectionTitle(dl, x, r.Y + 4, w, "State source");
        bool placeholder = input.Trees.IsPlaceholder;
        Rgba c = placeholder ? ParchmentPalette.IronRed : ParchmentPalette.Verdigris;
        Chrome.Tag(dl, input.Measure, x, y, placeholder ? "PLACEHOLDER" : "LIVE", c);
        y += 20;
        dl.Text(x, y, Ink.Fit(input.Measure, input.Trees.SourceLabel, 11, w), 11, InkS);
        y += 16;
        if (input.PreviewStepCount > 1)
        {
            dl.Text(x, y, Ink.Fit(input.Measure, $"step {input.PreviewStep + 1}/{input.PreviewStepCount}: {input.PreviewStepLabel}", 11, w), 11, InkP);
            y += 18;
            Chrome.Button(dl, hits, new RectD(x, y, 30, 22), "◀", false, new PreviewStepAction(-1), "Previous placeholder step (preview only)");
            Chrome.Button(dl, hits, new RectD(x + 34, y, 30, 22), "▶", false, new PreviewStepAction(+1), "Next placeholder step (preview only)");
        }
        Chrome.Button(dl, hits, new RectD(x + w - 64, y, 64, 22), "Reload", false, new ReloadContentAction(), "Reload ui-content/trees");
        int warnings = input.Diagnostics?.Count(d => d.Severity == DiagnosticSeverity.Warning) ?? 0;
        if (warnings > 0) dl.Text(x + 72, y + 4, $"{warnings} warning(s)", 11, ParchmentPalette.IronRed);
    }

    // --- the graph canvas -----------------------------------------------------------------------

    private static void PaintCanvas(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD canvas, TreesView view)
    {
        TreesUiState ui = input.Ui;
        GraphCamera cam = ui.Camera;
        TreeLayoutResult lay = input.Layout;
        TreeGraph g = input.Graph;
        double z = cam.Zoom;
        dl.Rect(canvas, ParchmentPalette.PaperMid);
        dl.PushClip(canvas);
        hits.Add(new Hit(canvas, new SelectNodeAction(null), ""));   // empty canvas: clear the selection

        (double X, double Y) S(double wx, double wy) => cam.WorldToScreen(wx, wy, canvas);

        // Bands: one per lens, alternating tint, each with a header strip whose label sticks
        // to the canvas's left edge while the graph pans.
        foreach (BandBox b in lay.Bands)
        {
            (double x0, double y0) = S(0, b.Y);
            (double x1, double y1) = S(lay.Width, b.Y + b.Height);
            var band = new RectD(x0, y0, x1 - x0, y1 - y0);
            if (!band.Intersects(canvas)) continue;
            bool focused = ui.Lens == b.LensId;
            Rgba tint = focused ? Ink.With(Gold, 0.10) : b.Band % 2 == 0 ? Ink.With(ParchmentPalette.PaperLight, 0.55) : Ink.With(ParchmentPalette.PaperLight, 0.0);
            dl.Rect(band, tint);
            double strip = lay.Options.BandHeaderHeight * z;
            dl.Rect(new RectD(band.X, band.Y, band.W, strip), Ink.With(focused ? Gold : InkS, focused ? 0.16 : 0.07));
            dl.Line(band.X, band.Y, band.Right, band.Y, Ink.With(InkS, 0.3), 1);
            LensDef lens = g.Content.Lens(b.LensId);
            double gs = System.Math.Clamp(strip - 4, 12, 22);
            double lx = System.Math.Max(band.X, canvas.X) + 6;
            double ly = band.Y + (strip - gs) / 2;
            dl.Glyph(lx, ly, gs, new GlyphSpec(GlyphBase.Emblem, GlyphState.Complete, NodeVisuals.SizeFor(gs), Domain: lens.Mark));
            double ts = System.Math.Clamp(12.5 * z, 10.0, 14.0);
            dl.Text(lx + gs + 6, band.Y + (strip - ts) / 2 - 1, lens.Name.ToUpperInvariant(), ts, focused ? Gold : InkS, TextAlign.Left, FontRole.Caps);
            if (z >= 0.85)
            {
                double dx = lx + gs + 16 + input.Measure.Width(lens.Name.ToUpperInvariant(), ts, FontRole.Caps);
                dl.Text(dx, band.Y + (strip - 11.5) / 2, Ink.Fit(input.Measure, "— " + lens.ShortDescription, 11.5, canvas.Right - dx - 8), 11.5, Ink.With(InkS, 0.8));
            }
        }

        // Edges, weakest first, so emphasised ones draw on top.
        foreach (EdgeView ev in view.Edges.OrderByDescending(e => (int)e.Emphasis).ThenBy(e => e.OnPath ? 1 : 0).ThenBy(e => e.Edge.Index))
        {
            if (ev.Emphasis == Emphasis.Hidden) continue;
            PaintEdge(dl, input, canvas, ev);
        }

        // Nodes, dimmed first.
        foreach (NodeView nv in view.Nodes.OrderByDescending(v => (int)v.Emphasis).ThenBy(v => v.Selected ? 1 : 0).ThenBy(v => v.Index))
        {
            if (nv.Emphasis == Emphasis.Hidden) continue;
            PaintNode(dl, hits, input, canvas, nv);
        }
        dl.PopClip();
        dl.Rect(canvas, null, Ink.With(InkS, 0.35), 1);
    }

    private static double Alpha(Emphasis e) => e switch
    {
        Emphasis.Normal => 1.0,
        Emphasis.Context => 0.6,
        Emphasis.Dimmed => 0.22,
        _ => 0.0,
    };

    private static void PaintEdge(DrawList dl, TreesFrameInput input, RectD canvas, EdgeView ev)
    {
        GraphCamera cam = input.Ui.Camera;
        TreeLayoutResult lay = input.Layout;
        double z = cam.Zoom;
        GraphEdge e = ev.Edge;
        NodeBox a = lay.Box(e.FlowFrom), b = lay.Box(e.FlowTo);
        (double X, double Y) S(double wx, double wy) => cam.WorldToScreen(wx, wy, canvas);

        (double X, double Y) p0, p1, p2, p3;
        if (b.X > a.Right + 4)
        {
            p0 = S(a.Right, a.CenterY); p3 = S(b.X, b.CenterY);
            double dx = System.Math.Max(40 * z, (p3.X - p0.X) * 0.45);
            p1 = (p0.X + dx, p0.Y); p2 = (p3.X - dx, p3.Y);
        }
        else if (b.Right < a.X - 4 && !e.Kind.Layering)
        {
            // Feedback pointing back: leave from the source's left, arrive at the target's right,
            // bowed below the row so it never hides under the cards it passes.
            p0 = S(a.X, a.CenterY); p3 = S(b.Right, b.CenterY);
            double bow = 34 * z + System.Math.Abs(p0.X - p3.X) * 0.08;
            p1 = (p0.X - 30 * z, p0.Y + bow); p2 = (p3.X + 30 * z, p3.Y + bow);
        }
        else
        {
            // Same column (or overlapping): hook out to the right and back.
            p0 = S(a.Right, a.CenterY); p3 = S(b.Right, b.CenterY);
            double hook = 36 * z;
            p1 = (p0.X + hook, p0.Y); p2 = (p3.X + hook, p3.Y);
        }

        double minX = System.Math.Min(System.Math.Min(p0.X, p1.X), System.Math.Min(p2.X, p3.X));
        double maxX = System.Math.Max(System.Math.Max(p0.X, p1.X), System.Math.Max(p2.X, p3.X));
        double minY = System.Math.Min(System.Math.Min(p0.Y, p1.Y), System.Math.Min(p2.Y, p3.Y));
        double maxY = System.Math.Max(System.Math.Max(p0.Y, p1.Y), System.Math.Max(p2.Y, p3.Y));
        if (!new RectD(minX - 4, minY - 4, maxX - minX + 8, maxY - minY + 8).Intersects(canvas)) return;

        double alpha = Alpha(ev.Emphasis);
        Rgba ink = ev.OnPath ? Path : Ink.Token(e.Kind.Ink);
        double width = (ev.OnPath ? 2.6 : ev.CrossLens ? 1.7 : 1.3) * System.Math.Clamp(z, 0.6, 1.4);
        dl.Bezier(p0, p1, p2, p3, Ink.With(ink, alpha * (ev.OnPath ? 1.0 : 0.85)), width, Ink.Dash(e.Kind.Stroke, width));
        if (e.Kind.Arrow && e.Directed)
        {
            double tx = p3.X - p2.X, ty = p3.Y - p2.Y;
            double len = System.Math.Sqrt(tx * tx + ty * ty);
            if (len < 1e-6) { tx = 1; ty = 0; len = 1; }
            tx /= len; ty /= len;
            double s = 7 * System.Math.Clamp(z, 0.6, 1.4);
            dl.Polygon([p3, (p3.X - tx * s - ty * s * 0.55, p3.Y - ty * s + tx * s * 0.55), (p3.X - tx * s + ty * s * 0.55, p3.Y - ty * s - tx * s * 0.55)],
                Ink.With(ink, alpha));
        }
        // Diffusion pathways (and any other steady edge animation) carry travelling dots.
        foreach (double t in input.Animator.EdgeDots(e.Kind.Id, e.Index, input.Now))
        {
            (double bx, double by) = BezierAt(p0, p1, p2, p3, t);
            dl.Circle(bx, by, 3.2 * System.Math.Clamp(z, 0.7, 1.3), Ink.With(ParchmentPalette.River, alpha));
        }
    }

    internal static (double X, double Y) BezierAt((double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, double t)
    {
        double u = 1 - t;
        double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
        return (a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
    }

    private static void PaintNode(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD canvas, NodeView v)
    {
        GraphCamera cam = input.Ui.Camera;
        double z = cam.Zoom;
        NodeBox box = input.Layout.Box(v.Index);
        (double x, double y) = cam.WorldToScreen(box.X, box.Y, canvas);
        var card = new RectD(x, y, box.W * z, box.H * z);
        if (!card.Intersects(canvas)) return;
        double alpha = Alpha(v.Emphasis);
        NodeMotion motion = input.Animator.Node(v.Def.Id, input.Now);

        // Card.
        if (v.Selected) dl.Rect(card.Inset(-4), Ink.With(Gold, 0.22), null, 1, 7);
        Rgba border = v.Selected ? Gold : v.OnPath || v.InFocus ? Path : v.Hovered ? InkP : Ink.With(InkS, 0.75);
        double bw = v.Selected ? 2.4 : v.OnPath || v.Hovered ? 1.8 : 1.0;
        dl.Rect(card, Ink.With(ParchmentPalette.PaperLight, 0.35 + 0.65 * alpha), Ink.With(border, alpha), bw, 5 * System.Math.Min(1, z));
        if (v.Disabled)
            dl.Rect(card.Inset(2.5 * z), null, Ink.With(ParchmentPalette.IronRed, 0.5 * alpha), 1, 4 * z);
        if (v.SearchMatch) dl.Rect(new RectD(card.X, card.Y + 3 * z, 4 * z, card.H - 6 * z), Gold);

        // Glyph: the reported state, never the animation.
        double gsz = 40 * z;
        double gx = card.X + 6 * z, gy = card.Y + (card.H - gsz) / 2;
        (double gcx, double gcy) = (gx + gsz / 2, gy + gsz / 2);
        if (motion.HaloAlpha > 0)
            dl.Circle(gcx, gcy, gsz * 0.56 * motion.HaloScale, Ink.With(Gold, motion.HaloAlpha * 0.35 * alpha), Ink.With(Gold, motion.HaloAlpha * alpha), 2.2);
        dl.Glyph(gx, gy, gsz, NodeVisuals.Glyph(v, NodeVisuals.SizeFor(gsz)), alpha);
        if (motion.OneShotT >= 0)
        {
            double t = motion.OneShotT;
            Rgba fc = v.State.GlyphState == GlyphState.Decayed ? ParchmentPalette.IronRed : Gold;
            if (motion.OneShot == AnimationKind.FlashRing)
                dl.Circle(gcx, gcy, gsz * (0.5 + 0.7 * t), null, Ink.With(fc, (1 - t) * alpha), 3.0 * (1 - t) + 0.8);
            else if (motion.OneShot == AnimationKind.Shimmer)
                for (int k = 0; k < 4; k++)
                {
                    double ang = (k * 90 + 360 * t * 0.25) * System.Math.PI / 180;
                    double r0 = gsz * 0.58, r1 = gsz * (0.7 + 0.25 * t);
                    dl.Line(gcx + System.Math.Cos(ang) * r0, gcy + System.Math.Sin(ang) * r0, gcx + System.Math.Cos(ang) * r1, gcy + System.Math.Sin(ang) * r1,
                        Ink.With(Gold, (1 - t) * alpha), 1.8);
                }
        }
        if (motion.PipFillT >= 0)
            dl.Circle(gcx, gy + gsz * 0.9, gsz * 0.14 * (1 + motion.PipFillT), null, Ink.With(Gold, (1 - motion.PipFillT) * alpha), 2);

        // Text: name, then state (or research %). Level of detail follows zoom, with a
        // legibility floor: names keep a minimum size and hide only when zoomed far out.
        double nameSize = System.Math.Clamp(14 * z, 10.0, 15.0);
        double tx = gx + gsz + 6 * z, tw = card.Right - tx - 5 * z;
        bool detail = card.H >= nameSize * 2.6;
        if (z >= 0.36)
        {
            List<string> lines = Ink.Wrap(input.Measure, v.Def.Name, nameSize, tw);
            bool two = lines.Count > 1 && card.H >= nameSize * 2.3;
            string l1 = two ? lines[0] : Ink.Fit(input.Measure, v.Def.Name, nameSize, tw);
            double ny = two ? card.Y + (card.H - 2.15 * nameSize) / 2 : detail ? card.Y + 5 * z : card.CenterY - nameSize * 0.62;
            dl.Text(tx, ny, l1, nameSize, Ink.With(InkP, alpha));
            if (two) dl.Text(tx, ny + nameSize * 1.1, Ink.Fit(input.Measure, string.Join(" ", lines.Skip(1)), nameSize, tw), nameSize, Ink.With(InkP, alpha));
            if (detail && !two)
            {
                string sub = v.State.Name;
                if (v.State.ResearchRole == ResearchRole.Researching && v.Status?.ResearchProgress is double rp) sub += $" · {rp * 100:0} %";
                if (!v.StateReported) sub = "no state reported";
                else if (!v.StateValid) sub = "invalid state: " + v.Status!.StateId;
                double subSize = System.Math.Clamp(11 * z, 9.5, 12.0);
                dl.Text(tx, ny + nameSize * 1.25, Ink.Fit(input.Measure, sub, subSize, tw), subSize,
                    Ink.With(v.StateValid || !v.StateReported ? InkS : ParchmentPalette.IronRed, alpha));
            }
            // Research / realization bar along the card's foot.
            double? bar = v.State.Wash switch
            {
                WashMode.ResearchProgress => v.Status?.ResearchProgress,
                WashMode.RealizationProgress or WashMode.MilestoneProgress => v.Status?.RealizationProgress,
                WashMode.Full => 1.0,
                _ => null,
            };
            if (bar is double f && detail)
                dl.Bar(new RectD(tx, card.Bottom - 8 * z, tw, 3 * z), f,
                    Ink.With(v.State.Track == StateTrack.Research ? Gold : ParchmentPalette.Verdigris, alpha), Ink.With(InkS, 0.4 * alpha));
        }
        if ((motion.NewlyCompleted || motion.NewlyDiscovered) && z >= 0.5)
        {
            string tag = motion.NewlyCompleted ? "NEW" : "FOUND";
            double tw2 = input.Measure.Width(tag, 9, FontRole.Caps) + 8;
            dl.Rect(new RectD(card.Right - tw2 - 3, card.Y - 7, tw2, 14), Gold, null, 1, 3);
            dl.Text(card.Right - tw2 / 2 - 3, card.Y - 6, tag, 9, ParchmentPalette.PaperLight, TextAlign.Center, FontRole.Caps);
        }
        double hx0 = System.Math.Max(card.X, canvas.X), hy0 = System.Math.Max(card.Y, canvas.Y);
        double hx1 = System.Math.Min(card.Right, canvas.Right), hy1 = System.Math.Min(card.Bottom, canvas.Bottom);
        if (hx1 > hx0 && hy1 > hy0) hits.Add(new Hit(new RectD(hx0, hy0, hx1 - hx0, hy1 - hy0), new SelectNodeAction(v.Index), v.Def.Name));
    }
}
