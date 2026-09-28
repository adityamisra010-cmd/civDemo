using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Trees.View;

/// <summary>The right-hand panel of the Trees tab: DETAILS for the selected node (with the
/// path / prerequisite view) or the LEGEND (states, overlays, relationship kinds).</summary>
internal static class TreesPanels
{
    private static readonly Rgba InkP = ParchmentPalette.InkPrimary;
    private static readonly Rgba InkS = ParchmentPalette.InkSoft;
    private static readonly Rgba Gold = ParchmentPalette.GoldLeaf;

    private static double PanelTabs(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD r, bool legend)
    {
        Chrome.Panel(dl, r);
        double w = (r.W - 24) / 2;
        Chrome.Button(dl, hits, new RectD(r.X + 10, r.Y + 8, w, 24), "Details", !legend,
            new ToggleLegendAction(), "Show the selected node", enabled: legend && input.Ui.Selected is not null);
        Chrome.Button(dl, hits, new RectD(r.X + 14 + w, r.Y + 8, w, 24), "Legend", legend,
            new ToggleLegendAction(), "Show the legend", enabled: !legend);
        return r.Y + 42;
    }

    // --- details ---------------------------------------------------------------------------------

    /// <summary>Paints the details and returns the content's full height (for scrolling).</summary>
    public static double Details(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD r, TreesView view)
    {
        double top = PanelTabs(dl, hits, input, r, legend: false);
        if (input.Ui.Selected is not int sel) return 0;
        NodeView v = view.Nodes[sel];
        TreeGraph g = input.Graph;
        TreesDocument c = g.Content;
        ITextMeasure m = input.Measure;
        double x = r.X + 14, w = r.W - 28;
        var clip = new RectD(r.X, top, r.W, r.Bottom - top);
        dl.PushClip(clip);
        double y0 = top + 4 - input.Ui.DetailScroll;
        double y = y0;

        // Identity.
        dl.Glyph(x, y, 48, NodeVisuals.Glyph(v, SizeClass.Px48));
        double hx = x + 58;
        foreach (string line in Ink.Wrap(m, v.Def.Name, Chrome.Heading, w - 58).Take(2))
        {
            dl.Text(hx, y, line, Chrome.Heading, InkP, TextAlign.Left, FontRole.Heading);
            y += 22;
        }
        string lenses = string.Join(" + ", g.LensesOf(sel).Select(l => c.Lens(l).Name));
        dl.Text(hx, y + 2, Ink.Fit(m, $"{v.Type.Name.ToUpperInvariant()} · {lenses.ToUpperInvariant()}", 10.5, w - 58), 10.5, InkS, TextAlign.Left, FontRole.Caps);
        y = System.Math.Max(y + 22, top + 60);
        if (v.Def.Placeholder) { Chrome.Tag(dl, m, x, y, "DEMO CONTENT", ParchmentPalette.IronRed); y += 22; }

        // State: research and realization, kept visibly distinct.
        y = Chrome.SectionTitle(dl, x, y + 2, w, "State");
        dl.Text(x, y, v.State.Name, 15, InkP, TextAlign.Left, FontRole.Heading);
        dl.Text(x + w, y + 2, v.State.Track == StateTrack.Realization ? "research ✓ · realization" : v.State.Track.ToString().ToLowerInvariant(),
            11, InkS, TextAlign.Right, FontRole.Caps);
        y += 20;
        Chrome.Wrapped(dl, m, ref y, x, w, v.State.Legend, 12, InkS);
        if (!v.StateReported) { dl.Text(x, y, "The source reports no state for this node.", 12, ParchmentPalette.IronRed); y += 16; }
        else if (!v.StateValid)
        {
            Chrome.Wrapped(dl, m, ref y, x, w, $"The source reported '{v.Status!.StateId}', which the '{v.Type.Name}' type does not allow.", 12, ParchmentPalette.IronRed);
        }
        y += 4;
        NodeStatus? st = v.Status;
        ResearchCostDef? cost = v.Def.ResearchCost;
        string costText = cost?.Points is long cp ? $"{cp} RP" : "cost TBD";
        bool researched = v.State.ResearchRole == ResearchRole.Completed;
        string research = researched ? "complete"
            : st?.ResearchProgress is double rp ? $"{rp * 100:0} %" : v.State.ResearchRole == ResearchRole.Researchable ? "researchable" : "—";
        if (st?.ResearchPoints is long pts) research += $" · {pts} RP";
        dl.Text(x, y, "Research", 12, InkP);
        dl.Text(x + w, y, $"{research} · {costText}", 12, InkS, TextAlign.Right, FontRole.Numeric);
        y += 17;
        double researchBar = researched ? 1.0 : st?.ResearchProgress ?? 0.0;
        dl.Bar(new RectD(x, y, w, 5), researchBar, Gold, Ink.With(InkS, 0.6), ParchmentPalette.PaperMid);
        y += 12;
        if (c.StatesFor(v.Def.Type).Any(s => c.State(s).Track == StateTrack.Realization))
        {
            string realized = v.State.Track == StateTrack.Realization
                ? $"stage {v.State.Stage}/{GlyphSpec.MaxStage}" + (st?.RealizationProgress is double pr ? $" · {pr * 100:0} %" : "")
                : "not yet realized";
            dl.Text(x, y, "Realization", 12, InkP);
            dl.Text(x + w, y, realized, 12, InkS, TextAlign.Right, FontRole.Numeric);
            y += 17;
            double rb = v.State.Track == StateTrack.Realization ? st?.RealizationProgress ?? (v.State.Wash == WashMode.Full ? 1.0 : 0.0) : 0.0;
            dl.Bar(new RectD(x, y, w, 5), rb, ParchmentPalette.Verdigris, Ink.With(InkS, 0.6), ParchmentPalette.PaperMid);
            y += 12;
        }
        if (!string.IsNullOrEmpty(st?.Note)) Chrome.Wrapped(dl, m, ref y, x, w, st!.Note, 11.5, InkS);

        // WHY: the path / prerequisite view.
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Why — what precedes it");
        IReadOnlyList<GraphEdge> ups = g.DirectUpstream(sel);
        if (ups.Count == 0) { dl.Text(x, y, "Nothing precedes it in the content.", 12, InkS); y += 17; }
        foreach (GraphEdge e in ups)
        {
            int o = e.FlowFrom;
            NodeView ov = view.Nodes[o];
            ResearchRole role = ov.State.ResearchRole;
            string mark = role == ResearchRole.Completed ? "✓" : role == ResearchRole.Researching ? "◐" : "○";
            Rgba mc = role == ResearchRole.Completed ? ParchmentPalette.Verdigris : InkS;
            dl.Text(x, y, mark, 13, mc);
            string label = Ink.Fit(m, g.Node(o).Name, 12.5, w - 150);
            dl.Text(x + 18, y, label, 12.5, InkP);
            dl.Text(x + w, y + 1, $"{e.Kind.Name.ToLowerInvariant()} · {ov.State.Name.ToLowerInvariant()}", 10.5, InkS, TextAlign.Right);
            hits.Add(new Hit(new RectD(x, y - 2, w, 18), new SelectNodeAction(o, Center: true), "Select " + g.Node(o).Name));
            y += 18;
        }
        Chrome.Wrapped(dl, m, ref y, x, w,
            $"Availability rule: TBD (not implemented). The state shown is what {input.Trees.SourceLabel} reports.", 10.5, Ink.With(InkS, 0.85));

        // LEADS TO, then every other relationship, grouped by kind.
        IReadOnlyList<GraphEdge> downs = g.DirectDownstream(sel);
        if (downs.Count > 0)
        {
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Leads to");
            foreach (GraphEdge e in downs)
            {
                int o = e.FlowTo;
                dl.Text(x, y, "→", 12.5, InkS);
                dl.Text(x + 18, y, Ink.Fit(m, g.Node(o).Name, 12.5, w - 130), 12.5, InkP);
                dl.Text(x + w, y + 1, e.Kind.Name.ToLowerInvariant(), 10.5, InkS, TextAlign.Right);
                hits.Add(new Hit(new RectD(x, y - 2, w, 18), new SelectNodeAction(o, Center: true), "Select " + g.Node(o).Name));
                y += 18;
            }
        }
        GraphEdge[] others = g.Incident(sel).Select(i => g.Edges[i]).Where(e => !(e.Directed && e.Kind.Layering)).ToArray();
        if (others.Length > 0)
        {
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Other relationships");
            foreach (GraphEdge e in others)
            {
                bool outgoing = e.From == sel;
                int o = outgoing ? e.To : e.From;
                string sentence = outgoing ? $"{e.Kind.Verb} {g.Node(o).Name}" : $"{g.Node(o).Name} {e.Kind.Verb} this";
                dl.Text(x, y, Ink.Fit(m, sentence, 12, w), 12, InkP);
                hits.Add(new Hit(new RectD(x, y - 2, w, 18), new SelectNodeAction(o, Center: true), "Select " + g.Node(o).Name));
                y += 17;
            }
        }

        // Ages and milestones.
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Ages");
        AgesDocument ages = input.Content.Ages;
        string span = v.Def.AgeFrom is null ? "no Age data" :
            ages.Age(v.Def.AgeFrom).DisplayName + (v.Def.AgeTo is null ? " onward" : v.Def.AgeTo == v.Def.AgeFrom ? "" : " – " + ages.Age(v.Def.AgeTo).DisplayName);
        if (v.Def.AgeRefs.Count > 0) span += " · also " + string.Join(", ", v.Def.AgeRefs.Select(a => ages.Age(a).DisplayName));
        dl.Text(x, y, span + (v.Def.Placeholder ? " (demo assignment)" : ""), 12, InkP);
        y += 17;
        foreach (MilestoneDef md in ages.Milestones.Where(q => q.NodeRefs.Contains(v.Def.Id) || q.Id == v.Def.MilestoneRef))
        {
            Chrome.Check(dl, x, y, input.Ages.Milestone(md.Id).Completion);
            dl.Text(x + 20, y, Ink.Fit(m, $"{md.Name} ({ages.Age(md.Age.Length > 0 ? md.Age : ages.Ages.First(a => a.Milestones.Contains(md.Id)).Id).DisplayName})", 12, w - 20), 12, InkP);
            hits.Add(new Hit(new RectD(x, y - 2, w, 18), new SelectMilestoneAction(md.Id), "Milestone"));
            y += 19;
        }

        // Description, references, notes.
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Description");
        Chrome.Wrapped(dl, m, ref y, x, w, v.Def.ShortDescription, 12.5, InkP);
        if (v.Def.LongDescription.Length > 0) { y += 2; Chrome.Wrapped(dl, m, ref y, x, w, v.Def.LongDescription, 12, InkS); }
        if (v.Def.HistoricalReferences.Count > 0)
        {
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Historical references");
            foreach (string h in v.Def.HistoricalReferences) Chrome.Wrapped(dl, m, ref y, x, w, "· " + h, 12, InkS);
        }
        if (v.Def.DirectorNotes.Length > 0)
        {
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Director notes");
            Chrome.Wrapped(dl, m, ref y, x, w, v.Def.DirectorNotes, 12, InkS);
        }
        dl.Text(x, y + 6, v.Def.Id, 10.5, Ink.With(InkS, 0.7), TextAlign.Left, FontRole.Numeric);
        dl.PopClip();
        return y + 30 - y0;
    }

    // --- legend ------------------------------------------------------------------------------------

    public static void Legend(DrawList dl, List<Hit> hits, TreesFrameInput input, RectD r)
    {
        double top = PanelTabs(dl, hits, input, r, legend: true);
        TreesDocument c = input.Content.Trees;
        TreesUiState ui = input.Ui;
        ITextMeasure m = input.Measure;
        double x = r.X + 14, w = r.W - 28;
        double y = Chrome.SectionTitle(dl, x, top + 2, w, "States · click to filter");

        // Every state the content declares, with the glyph it draws. Two columns.
        double colW = w / 2;
        int i = 0;
        foreach (StateDef s in c.States)
        {
            double cx = x + (i % 2) * colW;
            double cy = y + (i / 2) * 30;
            bool on = ui.StateFilter.Contains(s.Id);
            var row = new RectD(cx - 3, cy - 2, colW - 4, 28);
            if (on) dl.Rect(row, Ink.With(Gold, 0.18), Gold, 1, 3);
            GlyphBase shape = s.Track == StateTrack.Milestone ? GlyphBase.Lozenge : s.Track == StateTrack.Realization ? GlyphBase.Portico : GlyphBase.Node;
            dl.Glyph(cx, cy, 24, NodeVisuals.Legend(s, shape, SizeClass.Px24));
            dl.Text(cx + 30, cy + 1, Ink.Fit(m, s.Name, 12.5, colW - 38), 12.5, InkP);
            dl.Text(cx + 30, cy + 15, s.Track.ToString().ToLowerInvariant(), 9.5, InkS, TextAlign.Left, FontRole.Caps);
            hits.Add(new Hit(row, new ToggleStateFilterAction(s.Id), s.Legend));
            i++;
        }
        y += (i + 1) / 2 * 30 + 4;
        Chrome.Wrapped(dl, m, ref y, x, w, "The ring is research; the fill and the stage pips are realization. Researched is not realized.", 11, InkS);

        // Overlays (task §11).
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Overlays");
        (string Label, Action<double, double> Draw)[] overlays =
        [
            ("selected", (ox, oy) => dl.Rect(new RectD(ox, oy, 26, 16), ParchmentPalette.PaperLight, Gold, 2.4, 3)),
            ("hovered", (ox, oy) => dl.Rect(new RectD(ox, oy, 26, 16), ParchmentPalette.PaperLight, InkP, 1.8, 3)),
            ("prerequisite path", (ox, oy) => dl.Rect(new RectD(ox, oy, 26, 16), ParchmentPalette.PaperLight, ParchmentPalette.Verdigris, 1.8, 3)),
            ("search match", (ox, oy) => { dl.Rect(new RectD(ox, oy, 26, 16), ParchmentPalette.PaperLight, InkS, 1, 3); dl.Rect(new RectD(ox, oy + 2, 4, 12), Gold); }),
            ("filtered / disabled", (ox, oy) => { dl.Rect(new RectD(ox, oy, 26, 16), Ink.With(ParchmentPalette.PaperLight, 0.4), Ink.With(InkS, 0.3), 1, 3); dl.Rect(new RectD(ox + 2, oy + 2, 22, 12), null, Ink.With(ParchmentPalette.IronRed, 0.5), 1, 2); }),
            ("researching (pulse)", (ox, oy) => dl.Circle(ox + 13, oy + 8, 8, Ink.With(Gold, 0.15), Gold, 2)),
            ("newly completed", (ox, oy) => { dl.Rect(new RectD(ox, oy + 1, 26, 14), Gold, null, 1, 3); dl.Text(ox + 13, oy + 2, "NEW", 9, ParchmentPalette.PaperLight, TextAlign.Center, FontRole.Caps); }),
            ("newly discovered", (ox, oy) => { dl.Rect(new RectD(ox - 4, oy + 1, 34, 14), Gold, null, 1, 3); dl.Text(ox + 13, oy + 2, "FOUND", 9, ParchmentPalette.PaperLight, TextAlign.Center, FontRole.Caps); }),
        ];
        for (int k = 0; k < overlays.Length; k++)
        {
            double ox = x + (k % 2) * colW + 4, oy = y + (k / 2) * 22;
            overlays[k].Draw(ox, oy);
            dl.Text(ox + 36, oy + 1, overlays[k].Label, 12, InkP);
        }
        y += (overlays.Length + 1) / 2 * 22 + 4;

        // Relationship kinds, each drawn in its own stroke.
        y = Chrome.SectionTitle(dl, x, y + 6, w, "Relationships");
        int k2 = 0;
        foreach (RelationKindDef kind in c.RelationKinds)
        {
            double ox = x + (k2 % 2) * colW, oy = y + (k2 / 2) * 19;
            Rgba ink = Ink.Token(kind.Ink);
            dl.Line(ox, oy + 8, ox + 26, oy + 8, ink, 1.6, Ink.Dash(kind.Stroke, 1.6));
            if (kind.Arrow) dl.Polygon([(ox + 30, oy + 8), (ox + 24, oy + 4.5), (ox + 24, oy + 11.5)], ink);
            dl.Text(ox + 36, oy + 1, kind.Name + (kind.Layering ? "" : " ·"), 12, InkP);
            k2++;
        }
        y += (k2 + 1) / 2 * 19 + 4;
        Chrome.Wrapped(dl, m, ref y, x, w, "· marks a kind that does not order the layout (feedback may point backward). Relationships are visualization/data concepts, not simulation rules.", 10.5, InkS);
        if (ui.StateFilter.Count > 0)
            Chrome.Button(dl, hits, new RectD(x, y + 4, w, 22), "Clear state filter", false, new ClearFiltersAction());
    }
}
