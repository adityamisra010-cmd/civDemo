using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Ages;

public enum AgeHit { OpenAdvance, Surge, Confirm, Cancel, ClosePanel, OpenKnowledge }

public readonly record struct AgeHitRegion(RectD Rect, AgeHit Kind, int Arg);

/// <summary>What a click on the Age surfaces asks the host to do. The screen never writes state: the
/// advance is returned as the AdvanceAge order for the session to log.</summary>
public sealed record AgeCommand(bool ClosePanel, bool OpenKnowledge, OrderRecord? Order, int SurgeKey)
{
    public static readonly AgeCommand None = new(false, false, null, 0);
}

/// <summary>
/// THE AGE SURFACES — the capital's Age panel, the full-screen ADVANCE AGE flow and the transition
/// toast — painted into a backend-agnostic <see cref="DrawList"/> (ImGui in the game, SVG for the
/// previews). Pure: it owns only UI state (whether the flow is open, the chosen surge, the toast);
/// it reads the world through <see cref="AgePanelModel"/> / <see cref="AdvanceFlowModel"/>, and a
/// confirm returns <see cref="AgeQuery.AdvanceOrder"/>, never a write.
/// </summary>
public sealed class AgeScreen(PolityId polity)
{
    public PolityId Polity { get; } = polity;
    public AgePanelModel Panel { get; private set; } = AgePanelModel.Empty;
    public AdvanceFlowModel? Flow { get; private set; }
    public bool FlowOpen { get; private set; }
    /// <summary>The chosen surge emphasis key, or 0 when none is chosen yet (confirm is disabled).</summary>
    public int SelectedSurge { get; set; }
    public IReadOnlyList<AgeHitRegion> Hits => _hits;

    private IReadOnlyWorldState? _world;
    private List<AgeHitRegion> _hits = [];

    // The transition toast (UI time only; it never reads or writes simulation state).
    private string? _toastTitle, _toastHeadline, _toastBody;
    private double _toastAge = double.MaxValue;
    public const double ToastSeconds = 6.0;
    public bool ToastVisible => _toastTitle is not null && _toastAge < ToastSeconds;

    public void Refresh(IReadOnlyWorldState world, AgeContent? ages, UnitFamilyContent? families, IReadOnlyList<OrderRecord> queued)
    {
        _world = world;
        Panel = AgePanelModel.Build(world, ages, queued, Polity);
        Flow = AdvanceFlowModel.Build(world, ages, families, Polity);
        if (FlowOpen && !Panel.CanAdvance) FlowOpen = false;
    }

    public void OpenFlow() { if (Panel.CanAdvance && Flow is not null) FlowOpen = true; }
    public void CloseFlow() => FlowOpen = false;

    /// <summary>Shows the civilization-state change toast after an Age transition.</summary>
    public void ShowTransition(int toAge, string ageName, string? surgeName, int converted)
    {
        _toastTitle = "A NEW AGE BEGINS - AGE " + AgePanelModel.Numeral(toAge);
        _toastHeadline = EnteredLine(ageName);
        _toastBody = "Age " + AgePanelModel.Numeral(toAge) + " - " + ageName + (surgeName is null ? "" : "  -  surge emphasis: " + surgeName)
            + (converted > 0 ? "  -  " + converted.ToString(CultureInfo.InvariantCulture) + " formation(s) modernized" : "");
        _toastAge = 0;
    }

    public void Advance(double dt) { if (_toastAge < double.MaxValue) _toastAge += dt; }

    public AgeCommand Click(double x, double y)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            AgeHitRegion h = _hits[i];
            if (!h.Rect.Contains(x, y)) continue;
            // While the flow is open it is modal: only its own regions answer.
            if (FlowOpen && h.Kind is not (AgeHit.Surge or AgeHit.Confirm or AgeHit.Cancel)) continue;
            switch (h.Kind)
            {
                case AgeHit.OpenAdvance: OpenFlow(); return AgeCommand.None;
                case AgeHit.ClosePanel: return new AgeCommand(true, false, null, 0);
                case AgeHit.OpenKnowledge: return new AgeCommand(false, true, null, 0);
                case AgeHit.Surge: SelectedSurge = h.Arg; return AgeCommand.None;
                case AgeHit.Cancel: FlowOpen = false; return AgeCommand.None;
                case AgeHit.Confirm: return Confirm();
            }
        }
        return AgeCommand.None;
    }

    /// <summary>Confirm: exactly <see cref="AgeQuery.AdvanceOrder"/> for the chosen surge, or nothing
    /// when no surge is chosen or the panel is not eligible. Closes the flow.</summary>
    public AgeCommand Confirm()
    {
        if (_world is null || Flow is null || !Panel.CanAdvance || SelectedSurge == 0) return AgeCommand.None;
        OrderRecord order = Flow.Order(_world, Polity, SelectedSurge);
        FlowOpen = false;
        return new AgeCommand(false, false, order, SelectedSurge);
    }

    /// <summary>Whether a screen point falls on a painted Age surface (the host routes it here).</summary>
    public bool Captures(double x, double y)
    {
        foreach (AgeHitRegion h in _hits) if (h.Rect.Contains(x, y)) return true;
        return FlowOpen || _panelRect.Contains(x, y);
    }

    private RectD _panelRect;

    // ================================================================== painting

    /// <summary>The era the Age surfaces are painted in (ADR-033 D8), handed in by the host from the
    /// player's authoritative Age; the founding era until it is set.</summary>
    public EraTheme Theme { get; set; } = EraThemes.For(UiEra.Prehistoric);

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);

    /// <summary>The transition headline for an Age's full name: "Your civilization has entered the
    /// Bronze Age", "… the Neolithic / Agricultural Age" (the full name verbatim; "Age" is added only
    /// when the name does not already end with it).</summary>
    public static string EnteredLine(string ageName) =>
        "Your civilization has entered the " + ageName + (ageName.EndsWith("Age", StringComparison.Ordinal) ? "" : " Age");

    /// <summary>Paints the docked capital Age panel into <paramref name="r"/>.</summary>
    public void PaintPanel(DrawList d, ITextMeasure m, RectD r, string capitalName, bool clearHits = true)
    {
        if (clearHits) _hits = [];
        _panelRect = r;
        EraTheme t = Theme;
        SemanticTokens s = t.Semantic;
        AgePanelModel p = Panel;
        PanelFrame.Paint(d, r, t, 501, FrameKind.Panel);
        // Era-invariant placement (continuity): the content box clears every era's frame and
        // ornament band, so the regions — and the hit rects — are the same in every era.
        double x = r.X + 20, w = r.W - 40;
        double y = r.Y + 20;
        double L(double size) => t.Type.Line(size);
        d.Write(t, x, y, ThemeText.Fit(m, t, "CAPITAL  -  " + capitalName.ToUpperInvariant(), 11, w - 40, FontRole.Caps), 11, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        var close = new RectD(r.Right - 34, r.Y + 10, 22, 22);
        PanelFrame.Paint(d, close, t, 502, FrameKind.Button);
        EraMarks.Close(d, t, close, t.Ink.TextSoft, 503);
        _hits.Add(new AgeHitRegion(close, AgeHit.ClosePanel, 0));
        y += 22;

        if (p.State == AgePanelState.NoContent)
        {
            d.Write(t, x, y, "Ages are not loaded in this session.", 14, t.Ink.TextSoft);
            return;
        }

        // Current Age banner.
        var banner = new RectD(x, y, w, 64);
        PanelFrame.Paint(d, banner, t, 504, FrameKind.Card, Mix(t.Material.Panel, t.Material.Accent, 0.10), t.Material.Accent, 1.1);
        string numeral = "AGE " + AgePanelModel.Numeral(p.CurrentAge);
        d.Title(t, banner.X + 14, banner.Y + 10, numeral, 24, t.Material.Accent);
        double nx = banner.X + 14 + Math.Max(82, m.Width(t, numeral, 24, FontRole.Title) + 14);   // clear of the numeral in any era's type
        d.Write(t, nx, banner.Y + 12, ThemeText.Fit(m, t, p.CurrentAgeName, 17, banner.Right - 12 - nx, FontRole.Heading), 17, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        string sub = p.Transitions.Count == 0 ? "founding Age" : "entered turn " + N(p.EnteredTurn)
            + (p.CurrentSurgeName is null ? "" : "  -  surge: " + p.CurrentSurgeName);
        d.Write(t, nx, banner.Y + 38, ThemeText.Fit(m, t, sub, 12, banner.Right - 12 - nx), 12, t.Ink.TextSoft);
        y = banner.Bottom + 12;

        if (p.State == AgePanelState.FinalAge)
        {
            d.Write(t, x, y, "This is the final Age. There is no further Age to enter.", 14, t.Ink.TextSoft);
            return;
        }

        // Next Age + state.
        d.Write(t, x, y, "NEXT", 11, t.Ink.TextDim, TextAlign.Left, FontRole.Caps);
        d.Write(t, x + 48, y - 2, ThemeText.Fit(m, t, "Age " + AgePanelModel.Numeral(p.NextAge!.Value) + "  -  " + p.NextAgeName, 15, w - 48, FontRole.Heading), 15, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        y += 26;
        PaintStateBand(d, m, new RectD(x, y, w, 70));
        y += 82;

        // Requirements summary chips.
        string[] chips =
        [
            "Core " + p.CoreMet + "/" + p.CoreTotal,
            "Supporting " + p.SupportingMet + "/" + p.SupportingRequired,
            "Categories " + p.CategoriesCovered + "/" + p.CategoriesRequired,
        ];
        bool[] ok = [p.CoreMet == p.CoreTotal, p.SupportingMet >= p.SupportingRequired, p.CategoriesCovered >= p.CategoriesRequired];
        double cw = (w - 16) / 3;
        for (int i = 0; i < 3; i++)
        {
            var c = new RectD(x + i * (cw + 8), y, cw, 26);
            Rgba col = ok[i] ? s.Positive : s.Progress;
            PanelFrame.Paint(d, c, t, 505 + i, FrameKind.Chip, Mix(t.Material.Panel, col, 0.12), col, 1.0);
            d.Write(t, c.CenterX, c.Y + 6, ThemeText.Fit(m, t, chips[i], 12, c.W - 8, FontRole.Numeric), 12, col, TextAlign.Center, FontRole.Numeric);
        }
        y += 38;

        d.Write(t, x, y, "CORE MILESTONES", 11, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        y += 18;
        int mi = 0;
        foreach (MilestoneLine l in p.Core) y = PaintMilestone(d, m, l, x, y, w, 520 + mi++);
        y += 6;
        d.Write(t, x, y, "SUPPORTING MILESTONES  -  BY CATEGORY", 11, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        y += 18;
        foreach (CategoryGroup g in p.Categories)
        {
            if (y > r.Bottom - 70) break;
            EraMarks.Tick(d, t, x + 1, y + 2, 12, g.Covered, g.Covered ? s.Positive : t.Ink.TextDim, 560 + mi++);
            d.Write(t, x + 18, y + 1, g.Name.ToUpperInvariant(), 11.5, g.Covered ? t.Ink.Text : t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
            d.Write(t, x + w, y + 1, g.Milestones.Count == 0 ? "no milestone in this Age" : g.Covered ? "covered" : "not covered", 11, g.Covered ? s.Positive : t.Ink.TextDim, TextAlign.Right);
            y += Math.Max(19, L(11.5));
            foreach (MilestoneLine l in g.Milestones) { if (y > r.Bottom - 60) break; y = PaintMilestone(d, m, l, x + 14, y, w - 14, 520 + mi++); }
            y += 3;
        }

        if (p.State is AgePanelState.Eligible or AgePanelState.Pending && Flow is { } fl && y < r.Bottom - 90)
        {
            y += 6;
            d.Write(t, x, y, "YOUR MILITARY WILL MODERNIZE", 11, t.Material.Accent, TextAlign.Left, FontRole.Caps);
            y += 17;
            if (fl.Modernization.Count == 0) { d.Write(t, x + 10, y, "No formations to modernize.", 12.5, t.Ink.TextDim); y += 17; }
            foreach (ModernizationLine ml in fl.Modernization)
            {
                if (y > r.Bottom - 60) break;
                string txt = ml.Count.ToString(CultureInfo.InvariantCulture) + " x " + ml.From + (ml.Changes ? "  ->  " + ml.To : "  -  " + AdvanceFlowModel.OutcomeText(ml.Outcome));
                d.Write(t, x + 10, y, ThemeText.Fit(m, t, txt, 12.5, w - 20), 12.5, ml.Changes ? s.Military : t.Ink.TextSoft);
                y += Math.Max(17, L(12.5));
            }
            if (p.State == AgePanelState.Pending && p.PendingSurgeName is { } ps)
            {
                d.Write(t, x + 10, y, "Selected surge: " + ps, 12.5, s.Active);
                y += 17;
            }
        }

        if (p.Remaining.Count > 0 && y < r.Bottom - 64)
        {
            y += 4;
            d.Write(t, x, y, "STILL REQUIRED", 11, s.Progress, TextAlign.Left, FontRole.Caps);
            y += 17;
            foreach (string sr in p.Remaining)
            {
                if (y > r.Bottom - 54) break;
                d.Write(t, x + 10, y, "-  " + ThemeText.Fit(m, t, sr, 12.5, w - 20), 12.5, t.Ink.TextSoft);
                y += Math.Max(17, L(12.5));
            }
        }

        var know = new RectD(x, r.Bottom - 34, w, 24);
        d.Write(t, know.CenterX, know.Y + 5, "Open KNOWLEDGE & TECHNOLOGY  [K]", 11.5, s.Knowledge, TextAlign.Center, FontRole.Caps);
        _hits.Add(new AgeHitRegion(know, AgeHit.OpenKnowledge, 0));
    }

    private void PaintStateBand(DrawList d, ITextMeasure m, RectD r)
    {
        EraTheme t = Theme;
        AgePanelModel p = Panel;
        switch (p.State)
        {
            case AgePanelState.Eligible:
            {
                PanelFrame.Paint(d, r, t, 510, FrameKind.Chip, Mix(t.Material.Panel, t.Material.Accent, 0.10), t.Material.Accent, 1.0);
                d.Write(t, r.X + 12, r.Y + 8, "Advancement available - optional", 13, t.Material.Accent, TextAlign.Left, FontRole.Heading);
                d.Write(t, r.X + 12, r.Y + 27, ThemeText.Fit(m, t, "You may advance now or keep building in this Age.", 11.5, r.W * 0.52), 11.5, t.Ink.TextSoft);
                var btn = new RectD(r.Right - 176, r.Y + 12, 164, r.H - 24);
                PanelFrame.Paint(d, btn, t, 511, FrameKind.Button, Mix(t.Material.PanelRaised, t.Material.Accent, 0.28), t.Material.Accent, 2.0);
                d.Write(t, btn.CenterX, btn.Y + 13, "ADVANCE AGE", 16, t.Ink.Text, TextAlign.Center, FontRole.Caps);
                _hits.Add(new AgeHitRegion(btn, AgeHit.OpenAdvance, 0));
                break;
            }
            case AgePanelState.Pending:
            {
                PanelFrame.Paint(d, r, t, 512, FrameKind.Chip, t.Semantic.ActiveFill, t.Semantic.Active, 1.0);
                d.Write(t, r.X + 12, r.Y + 10, ThemeText.Fit(m, t, "Advancing to " + p.NextAgeName + " next turn", 15, r.W - 24, FontRole.Heading), 15, t.Semantic.Active, TextAlign.Left, FontRole.Heading);
                d.Write(t, r.X + 12, r.Y + 36, ThemeText.Fit(m, t, "Surge emphasis: " + p.PendingSurgeName + "  -  takes effect at End Turn " + N(p.Pending!.DecisionTurn), 12, r.W - 24), 12, t.Ink.TextSoft);
                break;
            }
            default:
            {
                PanelFrame.Paint(d, r, t, 513, FrameKind.Chip, t.Material.PanelRaised, t.Material.Hairline, 0.9);
                d.Write(t, r.X + 12, r.Y + 10, "Not yet eligible", 14, t.Ink.TextSoft, TextAlign.Left, FontRole.Heading);
                double ty = r.Y + 32;
                foreach (string line in ThemeText.Wrap(m, t, "Progress toward the next Age is shown below. The Advance button appears once every requirement holds.", 11.5, r.W - 24))
                {
                    if (ty > r.Bottom - 14) break;
                    d.Write(t, r.X + 12, ty, line, 11.5, t.Ink.TextDim);
                    ty += t.Type.Line(11.5) * 0.92;
                }
                break;
            }
        }
    }

    private double PaintMilestone(DrawList d, ITextMeasure m, MilestoneLine l, double x, double y, double w, int id)
    {
        EraTheme t = Theme;
        Rgba c = l.Met ? t.Semantic.Positive : t.Ink.TextDim;
        EraMarks.Tick(d, t, x + 1, y + 2, 12, l.Met, c, id);
        string count = l.Threshold > 1 ? N(l.Observed) + " / " + N(l.Threshold) : l.Met ? "done" : "not yet";
        double cwid = m.Width(t, count, 11.5, FontRole.Numeric) + 6;
        d.Write(t, x + 20, y, ThemeText.Fit(m, t, l.Name, 13, w - 26 - cwid), 13, l.Met ? t.Ink.Text : t.Ink.TextSoft);
        d.Write(t, x + w, y + 1, count, 11.5, l.Met ? t.Semantic.Positive : t.Ink.TextDim, TextAlign.Right, FontRole.Numeric);
        return y + Math.Max(19, t.Type.Line(13));
    }

    /// <summary>Paints the full-screen ADVANCE AGE flow: surge emphasis cards, the modernization
    /// preview, and confirm/cancel.</summary>
    public void PaintFlow(DrawList d, ITextMeasure m, double width, double height)
    {
        if (!FlowOpen || Flow is null) return;
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        AdvanceFlowModel f = Flow;
        d.Rect(new RectD(0, 0, width, height), A(t.Ink.Text, 0.55));
        double mw = Math.Min(1360, width - 60), mh = Math.Min(900, height - 50);
        var r = new RectD((width - mw) / 2, (height - mh) / 2, mw, mh);
        PanelFrame.Paint(d, r, t, 530, FrameKind.Modal);
        _panelRect = r;

        // Header: from -> to.
        const double top = 6;   // clears every era's ornament band (era-invariant placement)
        d.Write(t, r.X + 28, r.Y + 20 + top, "ADVANCE AGE", 12, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        string from = "Age " + AgePanelModel.Numeral(f.FromAge) + "  " + f.FromAgeName;
        d.Write(t, r.X + 28, r.Y + 40 + top, from, 18, t.Ink.TextSoft, TextAlign.Left, FontRole.Heading);
        double ax = r.X + 28 + m.Width(t, from, 18, FontRole.Heading) + 18;
        EraMarks.Arrow(d, t, ax, r.Y + 52 + top, ax + 44, t.Material.Accent, 531);
        d.Write(t, ax + 58, r.Y + 36 + top, "Age " + AgePanelModel.Numeral(f.ToAge) + "  " + f.ToAgeName, 24, t.Ink.Text, TextAlign.Left, FontRole.Title);
        d.Write(t, r.X + 28, r.Y + 78 + top, "The new Age begins next turn. Choose where the transition's energy goes.", 13, t.Ink.TextSoft);
        PanelFrame.Rule(d, new RectD(r.X + 20, r.Y + 99 + top, r.W - 40, 6), t, 532);

        // Surge cards.
        double y = r.Y + 116 + top;
        d.Write(t, r.X + 28, y, "1   CHOOSE A SURGE EMPHASIS", 12.5, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        d.Write(t, r.Right - 28, y, "Effects are qualitative: the surge's strength and duration are not yet simulated.", 11.5, t.Ink.TextDim, TextAlign.Right);
        y += 24;
        int n = f.Surges.Count;
        int cols = n <= 3 ? n : (n + 1) / 2;
        double gap = 14, cardW = (mw - 56 - gap * (cols - 1)) / Math.Max(1, cols), cardH = 168;
        for (int i = 0; i < n; i++)
        {
            SurgeCard s = f.Surges[i];
            var c = new RectD(r.X + 28 + (i % cols) * (cardW + gap), y + (i / cols) * (cardH + gap), cardW, cardH);
            bool on = s.Key == SelectedSurge;
            PanelFrame.Paint(d, c, t, 540 + i, FrameKind.Card, on ? Mix(t.Material.Panel, t.Material.Accent, 0.14) : t.Material.PanelRaised,
                on ? t.Material.Accent : t.Material.Hairline, on ? 1.8 : 0.9);
            double ci = PanelFrame.ContentInset(t, FrameKind.Card);
            if (on) d.Rect(new RectD(c.X + ci, c.Y + ci + 4, 4, c.H - 2 * ci - 8), t.Material.Accent);
            d.Write(t, c.X + 16, c.Y + 12, s.Domain.ToUpperInvariant(), 10.5, on ? t.Material.Accent : t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
            d.Write(t, c.X + 16, c.Y + 28, ThemeText.Fit(m, t, s.Name, 17, c.W - 32, FontRole.Heading), 17, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            double ty = c.Y + 54;
            foreach (string line in ThemeText.Wrap(m, t, s.Description, 12, c.W - 32))
            {
                if (ty > c.Y + 100) break;
                d.Write(t, c.X + 16, ty, line, 12, t.Ink.TextSoft);
                ty += Math.Min(16, t.Type.Line(12));
            }
            d.Write(t, c.X + 16, c.Bottom - 50, "EFFECT", 9.5, t.Ink.TextDim, TextAlign.Left, FontRole.Caps);
            d.Write(t, c.X + 66, c.Bottom - 51, ThemeText.Fit(m, t, s.ExpectedEffect, 11.5, c.W - 82), 11.5, t.Ink.Text);
            d.Write(t, c.X + 16, c.Bottom - 32, "FEEDS", 9.5, t.Ink.TextDim, TextAlign.Left, FontRole.Caps);
            double px = c.X + 66;
            int pk = 0;
            foreach (string sys0 in s.DownstreamSystems)
            {
                string sys = ThemeText.Fit(m, t, sys0, 10.5, c.Right - 24 - px, FontRole.Caps);
                double pw = m.Width(t, sys, 10.5, FontRole.Caps) + 14;
                if (px + pw > c.Right - 10) break;
                PanelFrame.Paint(d, new RectD(px, c.Bottom - 35, pw, 18), t, 560 + i * 8 + pk++, FrameKind.Chip, Mix(t.Material.Panel, sm.Knowledge, 0.10), A(sm.Knowledge, 0.7), 0.9);
                d.Write(t, px + pw / 2, c.Bottom - 32, sys, 10.5, sm.Knowledge, TextAlign.Center, FontRole.Caps);
                px += pw + 6;
            }
            d.Write(t, c.X + 16, c.Bottom - 14, ThemeText.Fit(m, t, "Shape: " + s.Shape, 10, c.W - 32), 10, t.Ink.TextDim);
            _hits.Add(new AgeHitRegion(c, AgeHit.Surge, s.Key));
        }
        y += ((n + cols - 1) / cols) * (cardH + gap) + 8;

        // Modernization.
        PanelFrame.Rule(d, new RectD(r.X + 20, y - 9, r.W - 40, 6), t, 533);
        d.Write(t, r.X + 28, y + 4, "2   YOUR MILITARY WILL MODERNIZE", 12.5, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        d.Write(t, r.Right - 28, y + 4, "Automatic and free at the transition (ruling 18). Read from the unit-family graph.", 11.5, t.Ink.TextDim, TextAlign.Right);
        y += 28;
        double colW = (mw - 56 - 24) / 2;
        double lx = r.X + 28, rx = lx + colW + 24;
        d.Write(t, lx, y, "YOUR FORMATIONS (" + f.Formations.ToString(CultureInfo.InvariantCulture) + ")", 11, t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
        d.Write(t, rx, y, "ACROSS THE UNIT-FAMILY GRAPH", 11, t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
        double ly = y + 20, ry = y + 20, bottom = r.Bottom - 74;
        if (f.Modernization.Count == 0)
        {
            d.Write(t, lx, ly, "You have no formations to modernize.", 13, t.Ink.TextDim);
        }
        int mk = 0;
        foreach (ModernizationLine l in f.Modernization)
        {
            if (ly > bottom - 30) break;
            var row = new RectD(lx, ly, colW, 40);
            PanelFrame.Paint(d, row, t, 580 + mk++, FrameKind.Card, t.Material.PanelRaised, l.Changes ? sm.Military : t.Material.Hairline, l.Changes ? 1.2 : 0.8);
            d.Write(t, row.X + 12, row.Y + 6, l.Count.ToString(CultureInfo.InvariantCulture) + " x", 13, t.Ink.Text, TextAlign.Left, FontRole.Numeric);
            d.Write(t, row.X + 48, row.Y + 5, l.From, 14, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            double fx = row.X + 48 + m.Width(t, l.From, 14, FontRole.Heading) + 10;
            if (l.Changes)
            {
                EraMarks.Arrow(d, t, fx, row.Y + 13, fx + 30, sm.Military, 590 + mk);
                d.Write(t, fx + 38, row.Y + 5, l.To, 14, sm.Military, TextAlign.Left, FontRole.Heading);
            }
            d.Write(t, row.X + 48, row.Y + 24, ThemeText.Fit(m, t, l.FamilyName + "  -  " + AdvanceFlowModel.OutcomeText(l.Outcome), 11, row.W - 60), 11, t.Ink.TextSoft);
            ly += 46;
        }
        foreach (FamilyLine fl in f.FamilyChanges)
        {
            if (ry > bottom) break;
            bool change = fl.To is not null && fl.From != fl.To;
            d.Write(t, rx, ry, ThemeText.Fit(m, t, fl.FamilyName, 12, colW * 0.42), 12, change ? t.Ink.Text : t.Ink.TextDim);
            string txt = fl.From is null && fl.To is null ? "not yet realized"
                : fl.From is null ? "first appears: " + fl.To
                : change ? fl.From + "  ->  " + fl.To
                : fl.From + "  (" + AdvanceFlowModel.OutcomeText(fl.Outcome) + ")";
            d.Write(t, rx + colW * 0.44, ry, ThemeText.Fit(m, t, txt, 12, colW * 0.56), 12, change ? sm.Military : t.Ink.TextDim);
            ry += 18;
        }

        // Footer.
        var cancel = new RectD(r.Right - 28 - 360, r.Bottom - 58, 140, 40);
        var confirm = new RectD(r.Right - 28 - 206, r.Bottom - 58, 206, 40);
        PanelFrame.Paint(d, cancel, t, 534, FrameKind.Button);
        d.Write(t, cancel.CenterX, cancel.Y + 12, "NOT NOW", 13, t.Ink.TextSoft, TextAlign.Center, FontRole.Caps);
        _hits.Add(new AgeHitRegion(cancel, AgeHit.Cancel, 0));
        bool can = SelectedSurge != 0;
        PanelFrame.Paint(d, confirm, t, 535, FrameKind.Button, can ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.28) : t.Material.PanelRaised,
            can ? t.Material.Accent : t.Material.Hairline, can ? 2.0 : 0.9);
        d.Write(t, confirm.CenterX, confirm.Y + 11, "CONFIRM ADVANCE", 15, can ? t.Ink.Text : t.Ink.TextDim, TextAlign.Center, FontRole.Caps);
        if (can) _hits.Add(new AgeHitRegion(confirm, AgeHit.Confirm, 0));
        string hint = can ? "Logged as an order now; the next turn begins in " + f.ToAgeName + "."
            : "Choose a surge emphasis to confirm.";
        d.Write(t, r.X + 28, r.Bottom - 46, hint, 12.5, can ? t.Ink.TextSoft : sm.Progress);
    }

    /// <summary>
    /// THE AGE-TRANSITION PANEL (top centre) while it is visible: a restrained panel in the NEW era's
    /// frame — "A NEW AGE BEGINS", "Your civilization has entered the …", the surge and the
    /// formations modernized, and the medium the interface now keeps its records in. It fades in and
    /// out over a few seconds of UI time; nothing else animates and nothing is written.
    /// </summary>
    public void PaintToast(DrawList d, ITextMeasure m, double width, double top)
    {
        if (!ToastVisible) return;
        EraTheme t = Theme;
        double fade = Math.Clamp(Math.Min(_toastAge / 0.35, (ToastSeconds - _toastAge) / 0.8), 0, 1);
        double w = Math.Min(760, width - 40);
        var r = new RectD((width - w) / 2, top, w, 122);
        // The frame fades with the panel: paint it into a scratch list, then copy with alpha.
        var frame = new DrawList();
        PanelFrame.Paint(frame, r, t, 550, FrameKind.Toast, null, t.Material.Accent, 1.4);
        foreach (DrawCmd c in frame.Commands) d.Add(Faded(c, fade));
        const double ins = 14;   // clears every era's frame and ornament band
        d.Write(t, r.CenterX, r.Y + ins + 6, _toastTitle!, 12, A(t.Material.Accent, fade), TextAlign.Center, FontRole.Caps);
        d.Write(t, r.CenterX, r.Y + ins + 26, ThemeText.Fit(m, t, _toastHeadline!, 21, r.W - 40, FontRole.Title), 21, A(t.Ink.Text, fade), TextAlign.Center, FontRole.Title);
        d.Write(t, r.CenterX, r.Y + ins + 56, ThemeText.Fit(m, t, _toastBody!, 12.5, r.W - 40), 12.5, A(t.Ink.TextSoft, fade), TextAlign.Center);
        d.Write(t, r.CenterX, r.Y + ins + 78, ThemeText.Fit(m, t, "Records are now kept in " + t.Medium + ".", 11.5, r.W - 40), 11.5, A(t.Material.Accent, fade), TextAlign.Center);
    }

    private static DrawCmd Faded(DrawCmd c, double f) => c switch
    {
        RectCmd r => r with { Fill = r.Fill is Rgba a ? Scale(a, f) : null, Stroke = r.Stroke is Rgba b ? Scale(b, f) : null },
        LineCmd l => l with { Color = Scale(l.Color, f) },
        PolygonCmd p => p with { Fill = Scale(p.Fill, f) },
        PolylineCmd p => p with { Color = Scale(p.Color, f) },
        CircleCmd ci => ci with { Fill = ci.Fill is Rgba a ? Scale(a, f) : null, Stroke = ci.Stroke is Rgba b ? Scale(b, f) : null },
        BezierCmd b => b with { Color = Scale(b.Color, f) },
        ArcCmd a => a with { Color = Scale(a.Color, f) },
        _ => c,
    };

    private static Rgba Scale(Rgba c, double f) => new(c.R, c.G, c.B, (byte)Math.Round(c.A * Math.Clamp(f, 0, 1)));

    /// <summary>For previews: the toast frozen at full opacity.</summary>
    public void HoldToast() => _toastAge = 1.0;
}
