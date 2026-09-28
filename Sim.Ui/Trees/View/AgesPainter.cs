using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Trees.View;

/// <summary>
/// THE AGE VIEW (task §19; docs/architecture/ages-ui.md). Ages are a separate framework
/// around the Trees, not an eighth lens: a forward-only track of placeholder Ages, the
/// current (or viewed) Age with its REPORTED progress and transition status, and its
/// milestone checklist split into mandatory and supporting. The view computes no
/// progress and decides no transition — both are read from the Age state source.
/// </summary>
internal static class AgesPainter
{
    private static readonly Rgba InkP = ParchmentPalette.InkPrimary;
    private static readonly Rgba InkS = ParchmentPalette.InkSoft;
    private static readonly Rgba Gold = ParchmentPalette.GoldLeaf;

    public static AgeStanding Standing(AgesDocument ages, string currentAgeId, AgeDef age)
    {
        int current = ages.TryAge(currentAgeId, out AgeDef cur) ? cur.Order : int.MinValue;
        return age.Order < current ? AgeStanding.Passed : age.Order == current ? AgeStanding.Current : AgeStanding.Future;
    }

    public static void Paint(DrawList dl, List<Hit> hits, TreesFrameInput input, TreesScreenLayout lay)
    {
        AgesDocument ages = input.Content.Ages;
        AgeStateSnapshot st = input.Ages;
        ITextMeasure m = input.Measure;
        double top = lay.Header.Bottom;
        double H = lay.Screen.H, W = lay.Screen.W;
        AgeMotion motion = input.Animator.Age(input.Now);

        IReadOnlyList<AgeDef> order = ages.InOrder();
        AgeDef current = ages.TryAge(input.CurrentAgeId, out AgeDef c0) ? c0 : order[0];
        AgeDef viewed = input.Ui.ViewedAge is string va && ages.TryAge(va, out AgeDef v0) ? v0 : current;
        string? pendingTo = st.Transition?.Pending == true ? st.Transition.ToAgeId : null;

        // --- left: the Age track --------------------------------------------------------------
        var track = new RectD(0, top, 262, H - top);
        dl.Rect(track, ParchmentPalette.PaperLight);
        dl.Line(track.Right - 0.5, track.Y, track.Right - 0.5, track.Bottom, Ink.With(InkS, 0.55), 1);
        double x = 14, y = Chrome.SectionTitle(dl, x, top + 10, track.W - 28, "Ages · forward only");
        double rowH = System.Math.Min(62, (H - top - 190) / System.Math.Max(1, order.Count));
        double lineX = x + 20;
        dl.Line(lineX, y + 20, lineX, y + 20 + rowH * (order.Count - 1), Ink.With(InkS, 0.5), 2);
        foreach (AgeDef a in order)
        {
            AgeStanding standing = Standing(ages, input.CurrentAgeId, a);
            bool isNext = pendingTo == a.Id;
            GlyphState gs = standing switch
            {
                AgeStanding.Passed => GlyphState.Complete,
                AgeStanding.Current => GlyphState.InProgress,
                _ => isNext ? GlyphState.Discovered : GlyphState.Locked,
            };
            double prog = standing == AgeStanding.Passed ? 1.0 : standing == AgeStanding.Current ? st.CurrentProgress ?? 0.0 : 0.0;
            var row = new RectD(4, y, track.W - 8, rowH - 4);
            if (a.Id == viewed.Id) dl.Rect(row, Ink.With(Gold, 0.15), Ink.With(Gold, 0.9), 1, 3);
            dl.Glyph(lineX - 18, y + 2, 36, new GlyphSpec(a.IconBase, gs, SizeClass.Px32, Domain: a.IconMark, Era: a.Theme.Register,
                Maturity: prog, Progress: prog));
            if (standing == AgeStanding.Current && motion.EnteredFlashT >= 0)
                dl.Circle(lineX, y + 20, 18 + 16 * motion.EnteredFlashT, null, Ink.With(Gold, 1 - motion.EnteredFlashT), 3);
            if (isNext && motion.TransitionSweepT >= 0)
                dl.Circle(lineX, y + 20, 18 + 6 * System.Math.Sin(System.Math.PI * motion.TransitionSweepT), null, Ink.With(Gold, 0.8), 2);
            dl.Text(x + 46, y + 4, a.DisplayName, 16, standing == AgeStanding.Future ? InkS : InkP, TextAlign.Left, FontRole.Heading);
            string label = standing switch
            {
                AgeStanding.Passed => "passed",
                AgeStanding.Current => "current · " + (st.CurrentProgress is double p ? $"{p * 100:0} %" : "- %"),
                _ => isNext ? "next · transition pending" : "future",
            };
            dl.Text(x + 46, y + 24, label, 11.5, standing == AgeStanding.Current ? Gold : InkS, TextAlign.Left, FontRole.Caps);
            hits.Add(new Hit(row, new SetViewedAgeAction(a.Id), a.DisplayName));
            y += rowH;
        }
        Chrome.Wrapped(dl, m, ref y, x, track.W - 28,
            "An Age never regresses. Capabilities can still degrade independently (see Degraded in the Trees legend).", 11, InkS);
        if (input.AgeGuard?.RegressionReported == true)
            Chrome.Wrapped(dl, m, ref y, x, track.W - 28,
                $"WARNING: the source reported {input.AgeGuard.LastReportedAgeId}, earlier than the Age already shown. Ages move forward only; the later Age stays displayed.",
                11, ParchmentPalette.IronRed);
        TreesCanvasPainter.PaintSource(dl, hits, input, new RectD(0, H - 122, track.W, 122));

        // --- centre: the viewed Age and its checklist ------------------------------------------
        double cx = track.Right + 18, cw = W - 372 - cx - 14;
        y = top + 14;
        AgeStanding vs = Standing(ages, input.CurrentAgeId, viewed);
        dl.Glyph(cx, y, 58, new GlyphSpec(viewed.IconBase, vs == AgeStanding.Future ? GlyphState.Locked : GlyphState.Complete,
            SizeClass.Px48, Domain: viewed.IconMark, Era: viewed.Theme.Register,
            Maturity: vs == AgeStanding.Passed ? 1.0 : vs == AgeStanding.Current ? st.CurrentProgress ?? 0 : 0));
        dl.Text(cx + 70, y + 2, viewed.DisplayName.ToUpperInvariant(), 28, InkP, TextAlign.Left, FontRole.Caps);
        double tagX = cx + 70;
        tagX += Chrome.Tag(dl, m, tagX, y + 38, vs.ToString().ToUpperInvariant(), vs == AgeStanding.Current ? Gold : InkS) + 6;
        if (viewed.Placeholder) Chrome.Tag(dl, m, tagX, y + 38, "PLACEHOLDER AGE", ParchmentPalette.IronRed);
        y += 66;
        Chrome.Wrapped(dl, m, ref y, cx, cw, viewed.ShortDescription, 13, InkS);

        // Progress (reported) and transition status.
        double progress = vs == AgeStanding.Passed ? 1.0 : vs == AgeStanding.Current ? st.CurrentProgress ?? 0.0 : 0.0;
        string pct = vs == AgeStanding.Current ? (st.CurrentProgress is double pp ? $"{pp * 100:0} %" : "- %") : vs == AgeStanding.Passed ? "passed" : "not begun";
        y += 6;
        dl.Text(cx, y, "PROGRESS", 11, InkS, TextAlign.Left, FontRole.Caps);
        dl.Text(cx + cw, y - 3, pct, 17, InkP, TextAlign.Right, FontRole.Numeric);
        y += 18;
        dl.Bar(new RectD(cx, y, cw, 10), progress, Gold, Ink.With(InkS, 0.7), ParchmentPalette.PaperLight);
        y += 14;
        dl.Text(cx, y, $"as reported by {st.SourceLabel} - the completion rule is a Director decision (not computed here)", 10.5, Ink.With(InkS, 0.85));
        y += 20;

        var banner = new RectD(cx, y, cw, 30);
        if (vs == AgeStanding.Current && pendingTo is string to && ages.TryAge(to, out AgeDef next))
        {
            dl.Rect(banner, Ink.With(Gold, 0.14), Gold, 1.2, 4);
            if (motion.TransitionSweepT >= 0)
            {
                double sx = banner.X + banner.W * motion.TransitionSweepT;
                dl.PushClip(banner);
                dl.Rect(new RectD(sx - 40, banner.Y, 80, banner.H), Ink.With(Gold, 0.22));
                dl.PopClip();
            }
            dl.Text(banner.X + 12, banner.Y + 7, $"TRANSITION PENDING -> {next.DisplayName.ToUpperInvariant()}", 13, InkP, TextAlign.Left, FontRole.Caps);
            dl.Text(banner.Right - 10, banner.Y + 8, "presentation only", 10.5, InkS, TextAlign.Right);
        }
        else
        {
            dl.Rect(banner, ParchmentPalette.PaperLight, Ink.With(InkS, 0.5), 1, 4);
            AgeDef? after = order.FirstOrDefault(a => a.Order > viewed.Order);
            string msg = vs == AgeStanding.Current
                ? after is null ? "No later Age in the content." : $"Next transition: {after.DisplayName} - not pending"
                : vs == AgeStanding.Passed ? "This Age is behind the civilization. Ages do not regress." : "A future Age: its milestones are shown for planning only.";
            dl.Text(banner.X + 12, banner.Y + 7, msg, 13, InkS);
        }
        y += 40;

        // Previous / next.
        AgeDef? prev = order.LastOrDefault(a => a.Order < viewed.Order);
        AgeDef? nextAge = order.FirstOrDefault(a => a.Order > viewed.Order);
        if (prev is not null) Chrome.Button(dl, hits, new RectD(cx, y, 170, 24), $"« previous · {prev.DisplayName}", false, new SetViewedAgeAction(prev.Id));
        if (nextAge is not null) Chrome.Button(dl, hits, new RectD(cx + cw - 170, y, 170, 24), $"next · {nextAge.DisplayName} »", false, new SetViewedAgeAction(nextAge.Id));
        if (viewed.Id != current.Id) Chrome.Button(dl, hits, new RectD(cx + cw / 2 - 60, y, 120, 24), "current Age", false, new SetViewedAgeAction(null));
        y += 36;

        // The checklist.
        IReadOnlyList<MilestoneDef> ms = ages.MilestonesOf(viewed.Id);
        foreach ((string title, bool mandatory) in new[] { ("Mandatory milestones", true), ("Supporting milestones", false) })
        {
            MilestoneDef[] group = ms.Where(q => q.Mandatory == mandatory).ToArray();
            int done = group.Count(q => st.Milestone(q.Id).Completion == MilestoneCompletion.Complete);
            y = Chrome.SectionTitle(dl, cx, y + 2, cw, $"{title} · {done}/{group.Length}");
            if (group.Length == 0) { dl.Text(cx, y, "none", 12, InkS); y += 18; continue; }
            foreach (MilestoneDef md in group)
            {
                MilestoneStatus ms0 = st.Milestone(md.Id);
                var row = new RectD(cx - 4, y - 3, cw + 8, 38);
                if (input.Ui.SelectedMilestone == md.Id) dl.Rect(row, Ink.With(Gold, 0.15), Ink.With(Gold, 0.9), 1, 3);
                Chrome.Check(dl, cx, y + 1, ms0.Completion);
                dl.Text(cx + 24, y, md.Name, 14, InkP);
                double tx = cx + 24 + m.Width(md.Name, 14, FontRole.Body) + 10;
                MilestoneCategoryDef cat = ages.Category(md.Category);
                Chrome.Tag(dl, m, tx, y + 1, cat.Name.ToUpperInvariant() + (cat.Provisional ? "*" : ""), InkS);
                double pv = ms0.Progress ?? (ms0.Completion == MilestoneCompletion.Complete ? 1.0 : 0.0);
                dl.Bar(new RectD(cx + cw - 132, y + 5, 88, 6), pv, ms0.Completion == MilestoneCompletion.Complete ? ParchmentPalette.Verdigris : Gold, Ink.With(InkS, 0.6), ParchmentPalette.PaperLight);
                dl.Text(cx + cw, y + 1, $"{pv * 100:0} %", 11.5, InkS, TextAlign.Right, FontRole.Numeric);
                string sub = md.Prerequisites.Count == 0 ? "" : "after " + string.Join(", ", md.Prerequisites.Select(p0 => ages.Milestone(p0).Name + (st.Milestone(p0).Completion == MilestoneCompletion.Complete ? " (met)" : "")));
                if (ms0.Evidence.Length > 0) sub += (sub.Length > 0 ? " · " : "") + "evidence: " + ms0.Evidence;
                if (sub.Length == 0) sub = md.Description;
                dl.Text(cx + 24, y + 18, Ink.Fit(m, sub, 11.5, cw - 30), 11.5, InkS);
                hits.Add(new Hit(row, new SelectMilestoneAction(md.Id), md.Name));
                y += 40;
            }
        }
        dl.Text(cx, y + 2, "* categories are provisional UI/data categories", 10.5, Ink.With(InkS, 0.8));

        // --- right: milestone detail, or the Age's placeholder metadata ---------------------------
        var right = new RectD(W - 372, top, 372, H - top);
        Chrome.Panel(dl, right);
        x = right.X + 14;
        double w = right.W - 28;
        y = right.Y + 10;
        if (input.Ui.SelectedMilestone is string sid && ages.TryMilestone(sid, out MilestoneDef sm))
        {
            MilestoneStatus ss = st.Milestone(sm.Id);
            y = Chrome.SectionTitle(dl, x, y, w, "Milestone");
            GlyphState mg = ss.Completion switch { MilestoneCompletion.Complete => GlyphState.Complete, MilestoneCompletion.Partial => GlyphState.InProgress, _ => GlyphState.Available };
            dl.Glyph(x, y, 40, new GlyphSpec(GlyphBase.Lozenge, mg, SizeClass.Px48, Domain: GlyphDomain.Hourglass,
                Maturity: ss.Progress ?? 0, Progress: ss.Progress ?? 0));
            dl.Text(x + 50, y + 2, sm.Name, 17, InkP, TextAlign.Left, FontRole.Heading);
            dl.Text(x + 50, y + 24, $"{(sm.Mandatory ? "MANDATORY" : "SUPPORTING")} · {ages.Category(sm.Category).Name.ToUpperInvariant()}", 10.5, InkS, TextAlign.Left, FontRole.Caps);
            y += 50;
            if (sm.Placeholder) { Chrome.Tag(dl, m, x, y, "PLACEHOLDER MILESTONE", ParchmentPalette.IronRed); y += 22; }
            string comp = ss.Completion switch { MilestoneCompletion.Complete => "complete", MilestoneCompletion.Partial => "partial", _ => "not started" };
            dl.Text(x, y, $"Completion: {comp}" + (ss.Progress is double sp ? $" · {sp * 100:0} %" : ""), 13, InkP);
            y += 20;
            Chrome.Wrapped(dl, m, ref y, x, w, sm.Description, 12.5, InkS);
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Prerequisites");
            if (sm.Prerequisites.Count == 0) { dl.Text(x, y, "none", 12, InkS); y += 17; }
            foreach (string p in sm.Prerequisites)
            {
                Chrome.Check(dl, x, y, st.Milestone(p).Completion);
                dl.Text(x + 20, y, ages.Milestone(p).Name, 12.5, InkP);
                hits.Add(new Hit(new RectD(x, y - 2, w, 18), new SelectMilestoneAction(p), ""));
                y += 19;
            }
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Evidence / notes");
            Chrome.Wrapped(dl, m, ref y, x, w, ss.Evidence.Length > 0 ? "Reported: " + ss.Evidence : "Nothing reported.", 12, InkP);
            Chrome.Wrapped(dl, m, ref y, x, w, sm.EvidenceNotes, 12, InkS);
            y = Chrome.SectionTitle(dl, x, y + 6, w, "Historical source");
            Chrome.Wrapped(dl, m, ref y, x, w, sm.HistoricalSource.Length > 0 ? sm.HistoricalSource : "-", 12, InkS);
            if (sm.NodeRefs.Count > 0)
            {
                y = Chrome.SectionTitle(dl, x, y + 6, w, "In the Trees");
                foreach (string nid in sm.NodeRefs)
                {
                    string name = input.Graph.TryIndexOf(nid, out int ni) ? input.Graph.Node(ni).Name : nid;
                    dl.Text(x, y, "-> " + name, 12.5, InkP);
                    hits.Add(new Hit(new RectD(x, y - 2, w, 18), new OpenNodeAction(nid), "Show in the Trees"));
                    y += 18;
                }
            }
            Chrome.Button(dl, hits, new RectD(x, y + 8, w, 22), "Show the Age's metadata", false, new SelectMilestoneAction(null));
            return;
        }

        y = Chrome.SectionTitle(dl, x, y, w, viewed.DisplayName + " · metadata");
        void Field(string label, string value)
        {
            dl.Text(x, y, label.ToUpperInvariant(), 10.5, InkS, TextAlign.Left, FontRole.Caps);
            y += 15;
            Chrome.Wrapped(dl, m, ref y, x, w, value, 12, InkP);
            y += 4;
        }
        DateRangeDef dr = viewed.DateRange;
        Field("Date range", $"{dr.StartLabel} - {dr.EndLabel}" + (dr.StartYear is long sy ? $" ({sy}" + (dr.EndYear is long ey ? $"-{ey})" : ")") : "") + (dr.Note.Length > 0 ? ". " + dr.Note : ""));
        Field("Visual theme", $"register {viewed.Theme.Register} · accent {viewed.Theme.Accent} · motif {viewed.Theme.Motif}");
        Field("Icon", $"{viewed.IconBase} + {viewed.IconMark}");
        Field("Transition effect", $"{(viewed.TransitionAnimation.Length > 0 ? viewed.TransitionAnimation : "—")}. {viewed.TransitionNote}");
        Field("Turn length, dt (placeholder)", (viewed.DeltaT.YearsPerTurn is double yp ? $"{yp} years / turn. " : "years / turn: -. ") + viewed.DeltaT.Note);
        Field("Catch-up (placeholder)", (viewed.CatchUp.PathwayBased ? "pathway-based. " : "") + viewed.CatchUp.Note);
        Field("Historical context", viewed.HistoricalContext);
        Field("Milestones", $"{ms.Count} ({ms.Count(q => q.Mandatory)} mandatory) - click one for its checklist entry");
    }
}
