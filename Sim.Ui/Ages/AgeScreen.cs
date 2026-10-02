using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Ui.Render;
using static Sim.Ui.Progression.ProgressionPalette;
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
    private string? _toastTitle, _toastBody;
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
        _toastBody = ageName + (surgeName is null ? "" : "  -  surge emphasis: " + surgeName)
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

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>Paints the docked capital Age panel into <paramref name="r"/>.</summary>
    public void PaintPanel(DrawList d, ITextMeasure m, RectD r, string capitalName, bool clearHits = true)
    {
        if (clearHits) _hits = [];
        _panelRect = r;
        AgePanelModel p = Panel;
        d.Rect(r, A(0x0C1117, 0.97), GoldDim, 1.2, 8);
        double x = r.X + 18, w = r.W - 36, y = r.Y + 14;
        d.Text(x, y, "CAPITAL  -  " + capitalName.ToUpperInvariant(), 11, GoldDim, TextAlign.Left, FontRole.Caps);
        var close = new RectD(r.Right - 34, r.Y + 10, 22, 22);
        d.Rect(close, ChromeRaised, Hairline, 1, 4);
        d.Line(close.X + 6, close.Y + 6, close.Right - 6, close.Bottom - 6, TextSoft, 1.6);
        d.Line(close.Right - 6, close.Y + 6, close.X + 6, close.Bottom - 6, TextSoft, 1.6);
        _hits.Add(new AgeHitRegion(close, AgeHit.ClosePanel, 0));
        y += 22;

        if (p.State == AgePanelState.NoContent)
        {
            d.Text(x, y, "Ages are not loaded in this session.", 14, TextSoft);
            return;
        }

        // Current Age banner.
        var banner = new RectD(x, y, w, 64);
        d.Rect(banner, Rgba.Hex(0x1A1710), Gold, 1, 6);
        d.Text(banner.X + 14, banner.Y + 10, "AGE " + AgePanelModel.Numeral(p.CurrentAge), 24, Gold, TextAlign.Left, FontRole.Heading);
        d.Text(banner.X + 96, banner.Y + 12, Ink.Fit(m, p.CurrentAgeName, 17, banner.W - 110, FontRole.Heading), 17, Text, TextAlign.Left, FontRole.Heading);
        string sub = p.Transitions.Count == 0 ? "founding Age" : "entered turn " + N(p.EnteredTurn)
            + (p.CurrentSurgeName is null ? "" : "  -  surge: " + p.CurrentSurgeName);
        d.Text(banner.X + 96, banner.Y + 38, Ink.Fit(m, sub, 12, banner.W - 110), 12, TextSoft);
        y = banner.Bottom + 12;

        if (p.State == AgePanelState.FinalAge)
        {
            d.Text(x, y, "This is the final Age. There is no further Age to enter.", 14, TextSoft);
            return;
        }

        // Next Age + state.
        d.Text(x, y, "NEXT", 11, TextDim, TextAlign.Left, FontRole.Caps);
        d.Text(x + 48, y - 2, Ink.Fit(m, "Age " + AgePanelModel.Numeral(p.NextAge!.Value) + "  -  " + p.NextAgeName, 15, w - 48, FontRole.Heading), 15, Text, TextAlign.Left, FontRole.Heading);
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
            d.Rect(c, ok[i] ? A(0x6CC28A, 0.14) : A(0xE0A040, 0.10), ok[i] ? Green : Amber, 1, 13);
            d.Text(c.CenterX, c.Y + 6, chips[i], 12, ok[i] ? Green : Amber, TextAlign.Center, FontRole.Numeric);
        }
        y += 38;

        d.Text(x, y, "CORE MILESTONES", 11, GoldDim, TextAlign.Left, FontRole.Caps);
        y += 18;
        foreach (MilestoneLine l in p.Core) y = PaintMilestone(d, m, l, x, y, w);
        y += 6;
        d.Text(x, y, "SUPPORTING MILESTONES  -  BY CATEGORY", 11, GoldDim, TextAlign.Left, FontRole.Caps);
        y += 18;
        foreach (CategoryGroup g in p.Categories)
        {
            if (y > r.Bottom - 70) break;
            d.Circle(x + 6, y + 8, 5, g.Covered ? Green : null, g.Covered ? Green : TextDim, 1.4);
            d.Text(x + 18, y + 1, g.Name.ToUpperInvariant(), 11.5, g.Covered ? Text : TextSoft, TextAlign.Left, FontRole.Caps);
            d.Text(x + w, y + 1, g.Milestones.Count == 0 ? "no milestone in this Age" : g.Covered ? "covered" : "not covered", 11, g.Covered ? Green : TextDim, TextAlign.Right);
            y += 19;
            foreach (MilestoneLine l in g.Milestones) { if (y > r.Bottom - 60) break; y = PaintMilestone(d, m, l, x + 14, y, w - 14); }
            y += 3;
        }

        if (p.State is AgePanelState.Eligible or AgePanelState.Pending && Flow is { } fl && y < r.Bottom - 90)
        {
            y += 6;
            d.Text(x, y, "YOUR MILITARY WILL MODERNIZE", 11, GoldDim, TextAlign.Left, FontRole.Caps);
            y += 17;
            if (fl.Modernization.Count == 0) { d.Text(x + 10, y, "No formations to modernize.", 12.5, TextDim); y += 17; }
            foreach (ModernizationLine ml in fl.Modernization)
            {
                if (y > r.Bottom - 60) break;
                string t = ml.Count.ToString(CultureInfo.InvariantCulture) + " x " + ml.From + (ml.Changes ? "  ->  " + ml.To : "  -  " + AdvanceFlowModel.OutcomeText(ml.Outcome));
                d.Text(x + 10, y, Ink.Fit(m, t, 12.5, w - 20), 12.5, ml.Changes ? Gold : TextSoft);
                y += 17;
            }
            if (p.State == AgePanelState.Pending && p.PendingSurgeName is { } ps)
            {
                d.Text(x + 10, y, "Selected surge: " + ps, 12.5, Cyan);
                y += 17;
            }
        }

        if (p.Remaining.Count > 0 && y < r.Bottom - 40)
        {
            y += 4;
            d.Text(x, y, "STILL REQUIRED", 11, Amber, TextAlign.Left, FontRole.Caps);
            y += 17;
            foreach (string s in p.Remaining)
            {
                if (y > r.Bottom - 22) break;
                d.Text(x + 10, y, "-  " + Ink.Fit(m, s, 12.5, w - 20), 12.5, TextSoft);
                y += 17;
            }
        }

        var know = new RectD(x, r.Bottom - 34, w, 24);
        d.Text(know.CenterX, know.Y + 5, "Open KNOWLEDGE & TECHNOLOGY  [K]", 11.5, Cyan, TextAlign.Center, FontRole.Caps);
        _hits.Add(new AgeHitRegion(know, AgeHit.OpenKnowledge, 0));
    }

    private void PaintStateBand(DrawList d, ITextMeasure m, RectD r)
    {
        AgePanelModel p = Panel;
        switch (p.State)
        {
            case AgePanelState.Eligible:
            {
                d.Rect(r, A(0xD8B866, 0.10), Gold, 1, 6);
                d.Text(r.X + 12, r.Y + 8, "Advancement available - optional", 13, Gold, TextAlign.Left, FontRole.Heading);
                d.Text(r.X + 12, r.Y + 27, Ink.Fit(m, "You may advance now or keep building in this Age.", 11.5, r.W * 0.52), 11.5, TextSoft);
                var btn = new RectD(r.Right - 176, r.Y + 12, 164, r.H - 24);
                d.Rect(btn, Rgba.Hex(0x3A2E12), Gold, 2, 6);
                d.Rect(new RectD(btn.X + 3, btn.Y + 3, btn.W - 6, 3), A(0xF5DE9A, 0.5));
                d.Text(btn.CenterX, btn.Y + 13, "ADVANCE AGE", 16, Gold, TextAlign.Center, FontRole.Caps);
                _hits.Add(new AgeHitRegion(btn, AgeHit.OpenAdvance, 0));
                break;
            }
            case AgePanelState.Pending:
            {
                d.Rect(r, A(0x5FD3E6, 0.10), Cyan, 1, 6);
                d.Text(r.X + 12, r.Y + 10, Ink.Fit(m, "Advancing to " + p.NextAgeName + " next turn", 15, r.W - 24, FontRole.Heading), 15, Cyan, TextAlign.Left, FontRole.Heading);
                d.Text(r.X + 12, r.Y + 36, Ink.Fit(m, "Surge emphasis: " + p.PendingSurgeName + "  -  takes effect at End Turn " + N(p.Pending!.DecisionTurn), 12, r.W - 24), 12, TextSoft);
                break;
            }
            default:
            {
                d.Rect(r, ChromeRaised, Hairline, 1, 6);
                d.Text(r.X + 12, r.Y + 10, "Not yet eligible", 14, TextSoft, TextAlign.Left, FontRole.Heading);
                double ty = r.Y + 32;
                foreach (string line in Ink.Wrap(m, "Progress toward the next Age is shown below. The Advance button appears once every requirement holds.", 11.5, r.W - 24))
                {
                    if (ty > r.Bottom - 14) break;
                    d.Text(r.X + 12, ty, line, 11.5, TextDim);
                    ty += 14;
                }
                break;
            }
        }
    }

    private static double PaintMilestone(DrawList d, ITextMeasure m, MilestoneLine l, double x, double y, double w)
    {
        Rgba c = l.Met ? Green : TextDim;
        if (l.Met)
        {
            d.Line(x + 2, y + 8, x + 6, y + 12, c, 2);
            d.Line(x + 6, y + 12, x + 13, y + 3, c, 2);
        }
        else d.Rect(new RectD(x + 2, y + 2, 11, 11), null, c, 1.3, 2);
        string count = l.Threshold > 1 ? N(l.Observed) + " / " + N(l.Threshold) : l.Met ? "done" : "not yet";
        double cwid = m.Width(count, 11.5, FontRole.Numeric) + 6;
        d.Text(x + 20, y, Ink.Fit(m, l.Name, 13, w - 26 - cwid), 13, l.Met ? Text : TextSoft);
        d.Text(x + w, y + 1, count, 11.5, l.Met ? Green : TextDim, TextAlign.Right, FontRole.Numeric);
        return y + 19;
    }

    /// <summary>Paints the full-screen ADVANCE AGE flow: surge emphasis cards, the modernization
    /// preview, and confirm/cancel.</summary>
    public void PaintFlow(DrawList d, ITextMeasure m, double width, double height)
    {
        if (!FlowOpen || Flow is null) return;
        AdvanceFlowModel f = Flow;
        d.Rect(new RectD(0, 0, width, height), A(0x05080B, 0.82));
        double mw = Math.Min(1360, width - 60), mh = Math.Min(900, height - 50);
        var r = new RectD((width - mw) / 2, (height - mh) / 2, mw, mh);
        d.Rect(r, Field, Gold, 1.5, 10);
        _panelRect = r;

        // Header: from -> to.
        d.Text(r.X + 28, r.Y + 20, "ADVANCE AGE", 12, GoldDim, TextAlign.Left, FontRole.Caps);
        d.Text(r.X + 28, r.Y + 40, "Age " + AgePanelModel.Numeral(f.FromAge) + "  " + f.FromAgeName, 18, TextSoft, TextAlign.Left, FontRole.Heading);
        double ax = r.X + 28 + m.Width("Age " + AgePanelModel.Numeral(f.FromAge) + "  " + f.FromAgeName, 18, FontRole.Heading) + 18;
        d.Line(ax, r.Y + 52, ax + 34, r.Y + 52, Gold, 2);
        d.Polygon([(ax + 34, r.Y + 46), (ax + 44, r.Y + 52), (ax + 34, r.Y + 58)], Gold);
        d.Text(ax + 58, r.Y + 36, "Age " + AgePanelModel.Numeral(f.ToAge) + "  " + f.ToAgeName, 24, Gold, TextAlign.Left, FontRole.Heading);
        d.Text(r.X + 28, r.Y + 78, "The new Age begins next turn. Choose where the transition's energy goes.", 13, TextSoft);
        d.Line(r.X + 20, r.Y + 102, r.Right - 20, r.Y + 102, Hairline, 1);

        // Surge cards.
        double y = r.Y + 116;
        d.Text(r.X + 28, y, "1   CHOOSE A SURGE EMPHASIS", 12.5, Gold, TextAlign.Left, FontRole.Caps);
        d.Text(r.Right - 28, y, "Effects are qualitative: the surge's strength and duration are not yet simulated.", 11.5, TextDim, TextAlign.Right);
        y += 24;
        int n = f.Surges.Count;
        int cols = n <= 3 ? n : (n + 1) / 2;
        double gap = 14, cardW = (mw - 56 - gap * (cols - 1)) / Math.Max(1, cols), cardH = 168;
        for (int i = 0; i < n; i++)
        {
            SurgeCard s = f.Surges[i];
            var c = new RectD(r.X + 28 + (i % cols) * (cardW + gap), y + (i / cols) * (cardH + gap), cardW, cardH);
            bool on = s.Key == SelectedSurge;
            d.Rect(c, on ? Rgba.Hex(0x2A2312) : AvailableFill, on ? Gold : Hairline, on ? 2.2 : 1, 8);
            if (on) d.Rect(new RectD(c.X, c.Y, 5, c.H), Gold);
            d.Text(c.X + 16, c.Y + 12, s.Domain.ToUpperInvariant(), 10.5, on ? Gold : GoldDim, TextAlign.Left, FontRole.Caps);
            d.Text(c.X + 16, c.Y + 28, Ink.Fit(m, s.Name, 17, c.W - 32, FontRole.Heading), 17, on ? Gold : Text, TextAlign.Left, FontRole.Heading);
            double ty = c.Y + 54;
            foreach (string line in Ink.Wrap(m, s.Description, 12, c.W - 32))
            {
                if (ty > c.Y + 100) break;
                d.Text(c.X + 16, ty, line, 12, TextSoft);
                ty += 15;
            }
            d.Text(c.X + 16, c.Bottom - 50, "EFFECT", 9.5, TextDim, TextAlign.Left, FontRole.Caps);
            d.Text(c.X + 66, c.Bottom - 51, Ink.Fit(m, s.ExpectedEffect, 11.5, c.W - 82), 11.5, Text);
            d.Text(c.X + 16, c.Bottom - 32, "FEEDS", 9.5, TextDim, TextAlign.Left, FontRole.Caps);
            double px = c.X + 66;
            foreach (string sys0 in s.DownstreamSystems)
            {
                string sys = Ink.Fit(m, sys0, 10.5, c.Right - 24 - px, FontRole.Caps);
                double pw = m.Width(sys, 10.5, FontRole.Caps) + 14;
                if (px + pw > c.Right - 10) break;
                d.Rect(new RectD(px, c.Bottom - 35, pw, 18), A(0x5FD3E6, 0.10), A(0x5FD3E6, 0.6), 1, 9);
                d.Text(px + pw / 2, c.Bottom - 32, sys, 10.5, Cyan, TextAlign.Center, FontRole.Caps);
                px += pw + 6;
            }
            d.Text(c.X + 16, c.Bottom - 14, Ink.Fit(m, "Shape: " + s.Shape, 10, c.W - 32), 10, TextDim);
            _hits.Add(new AgeHitRegion(c, AgeHit.Surge, s.Key));
        }
        y += ((n + cols - 1) / cols) * (cardH + gap) + 8;

        // Modernization.
        d.Line(r.X + 20, y - 6, r.Right - 20, y - 6, Hairline, 1);
        d.Text(r.X + 28, y + 4, "2   YOUR MILITARY WILL MODERNIZE", 12.5, Gold, TextAlign.Left, FontRole.Caps);
        d.Text(r.Right - 28, y + 4, "Automatic and free at the transition (ruling 18). Read from the unit-family graph.", 11.5, TextDim, TextAlign.Right);
        y += 28;
        double colW = (mw - 56 - 24) / 2;
        double lx = r.X + 28, rx = lx + colW + 24;
        d.Text(lx, y, "YOUR FORMATIONS (" + f.Formations.ToString(CultureInfo.InvariantCulture) + ")", 11, TextSoft, TextAlign.Left, FontRole.Caps);
        d.Text(rx, y, "ACROSS THE UNIT-FAMILY GRAPH", 11, TextSoft, TextAlign.Left, FontRole.Caps);
        double ly = y + 20, ry = y + 20, bottom = r.Bottom - 74;
        if (f.Modernization.Count == 0)
        {
            d.Text(lx, ly, "You have no formations to modernize.", 13, TextDim);
        }
        foreach (ModernizationLine l in f.Modernization)
        {
            if (ly > bottom - 30) break;
            var row = new RectD(lx, ly, colW, 40);
            d.Rect(row, ChromeRaised, l.Changes ? Gold : Hairline, 1, 6);
            d.Text(row.X + 12, row.Y + 6, l.Count.ToString(CultureInfo.InvariantCulture) + " x", 13, Text, TextAlign.Left, FontRole.Numeric);
            d.Text(row.X + 48, row.Y + 5, l.From, 14, Text, TextAlign.Left, FontRole.Heading);
            double fx = row.X + 48 + m.Width(l.From, 14, FontRole.Heading) + 10;
            if (l.Changes)
            {
                d.Line(fx, row.Y + 13, fx + 22, row.Y + 13, Gold, 2);
                d.Polygon([(fx + 22, row.Y + 8), (fx + 30, row.Y + 13), (fx + 22, row.Y + 18)], Gold);
                d.Text(fx + 38, row.Y + 5, l.To, 14, Gold, TextAlign.Left, FontRole.Heading);
            }
            d.Text(row.X + 48, row.Y + 24, Ink.Fit(m, l.FamilyName + "  -  " + AdvanceFlowModel.OutcomeText(l.Outcome), 11, row.W - 60), 11, TextSoft);
            ly += 46;
        }
        foreach (FamilyLine fl in f.FamilyChanges)
        {
            if (ry > bottom) break;
            bool change = fl.To is not null && fl.From != fl.To;
            d.Text(rx, ry, Ink.Fit(m, fl.FamilyName, 12, colW * 0.42), 12, change ? Text : TextDim);
            string txt = fl.From is null && fl.To is null ? "not yet realized"
                : fl.From is null ? "first appears: " + fl.To
                : change ? fl.From + "  ->  " + fl.To
                : fl.From + "  (" + AdvanceFlowModel.OutcomeText(fl.Outcome) + ")";
            d.Text(rx + colW * 0.44, ry, Ink.Fit(m, txt, 12, colW * 0.56), 12, change ? Gold : TextDim);
            ry += 18;
        }

        // Footer.
        var cancel = new RectD(r.Right - 28 - 360, r.Bottom - 58, 140, 40);
        var confirm = new RectD(r.Right - 28 - 206, r.Bottom - 58, 206, 40);
        d.Rect(cancel, ChromeRaised, Hairline, 1, 6);
        d.Text(cancel.CenterX, cancel.Y + 12, "NOT NOW", 13, TextSoft, TextAlign.Center, FontRole.Caps);
        _hits.Add(new AgeHitRegion(cancel, AgeHit.Cancel, 0));
        bool can = SelectedSurge != 0;
        d.Rect(confirm, can ? Rgba.Hex(0x3A2E12) : ChromeRaised, can ? Gold : Hairline, can ? 2 : 1, 6);
        d.Text(confirm.CenterX, confirm.Y + 11, "CONFIRM ADVANCE", 15, can ? Gold : TextDim, TextAlign.Center, FontRole.Caps);
        if (can) _hits.Add(new AgeHitRegion(confirm, AgeHit.Confirm, 0));
        string hint = can ? "Logged as an order now; the next turn begins in " + f.ToAgeName + "."
            : "Choose a surge emphasis to confirm.";
        d.Text(r.X + 28, r.Bottom - 46, hint, 12.5, can ? TextSoft : Amber);
    }

    /// <summary>Paints the Age-transition toast (top centre) while it is visible.</summary>
    public void PaintToast(DrawList d, ITextMeasure m, double width, double top)
    {
        if (!ToastVisible) return;
        double fade = Math.Clamp(Math.Min(_toastAge / 0.35, (ToastSeconds - _toastAge) / 0.8), 0, 1);
        double w = Math.Min(720, width - 40);
        var r = new RectD((width - w) / 2, top, w, 74);
        d.Rect(r, A(0x1A1710, 0.96 * fade), Ink.With(Gold, fade), 2, 8);
        d.Rect(new RectD(r.X + 6, r.Y + 6, r.W - 12, 3), Ink.With(Gold, 0.6 * fade));
        d.Text(r.CenterX, r.Y + 16, _toastTitle!, 20, Ink.With(Gold, fade), TextAlign.Center, FontRole.Heading);
        d.Text(r.CenterX, r.Y + 46, Ink.Fit(m, _toastBody!, 13, r.W - 30), 13, Ink.With(Text, fade), TextAlign.Center);
    }

    /// <summary>For previews: the toast frozen at full opacity.</summary>
    public void HoldToast() => _toastAge = 1.0;
}
