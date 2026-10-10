using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Ages;

public enum AgeHit { OpenAdvance, Surge, Confirm, Cancel, ClosePanel, OpenKnowledge, FocusNode }

public readonly record struct AgeHitRegion(RectD Rect, AgeHit Kind, int Arg);

/// <summary>What a click on the Age surfaces asks the host to do. The screen never writes state: the
/// advance is returned as the AdvanceAge order for the session to log.</summary>
public sealed record AgeCommand(bool ClosePanel, bool OpenKnowledge, OrderRecord? Order, int SurgeKey)
{
    public static readonly AgeCommand None = new(false, false, null, 0);

    /// <summary>M5 polish: the research node (key) to focus in the tree, or -1.</summary>
    public int FocusNode { get; init; } = -1;
}

/// <summary>
/// THE AGE SURFACES — the capital's Age panel, the full-screen ADVANCE AGE flow and the transition
/// toast — painted into a backend-agnostic <see cref="DrawList"/> (ImGui in the game, SVG for the
/// previews). Pure: it owns only UI state (whether the flow is open, the chosen surge, the toast);
/// it reads the world through <see cref="AgePanelModel"/> / <see cref="AdvanceFlowModel"/>, and a
/// confirm returns <see cref="AgeQuery.AdvanceOrder"/>, never a write.
/// </summary>
public sealed partial class AgeScreen(PolityId polity)
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
        Panel = AgePanelModel.Build(world, ages, queued, Polity, Research);
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
                case AgeHit.OpenKnowledge: return new AgeCommand(false, true, null, 0) { FocusNode = h.Arg };
                case AgeHit.FocusNode: return new AgeCommand(false, true, null, 0) { FocusNode = h.Arg };
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

    /// <summary>The UI scale the surfaces are set at (UR-1): every role size and length is × this.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>The pointer over the surfaces this frame (screen px), or null (UR-5: the controls answer hover).</summary>
    public (double X, double Y)? Pointer { get; private set; }

    /// <summary>Tracks the pointer for hover (null: elsewhere).</summary>
    public void PointerMove(double? x, double? y) => Pointer = x is double px && y is double py ? (px, py) : null;

    private bool Over(RectD r) => Pointer is (double px, double py) && r.Contains(px, py);

    // UR-5: the panel and the flow SCROLL (the wheel over them) rather than drop what does not fit.
    private double _panelScroll, _flowScroll, _panelMax, _flowMax;
    private RectD _flowBody;

    /// <summary>The wheel over the open flow scrolls its body; over the docked panel, the panel. True when consumed.</summary>
    public bool Wheel(double x, double y, double notches)
    {
        double step = 60 * Scale;
        if (FlowOpen) { _flowScroll = Math.Clamp(_flowScroll - notches * step, 0, Math.Max(0, _flowMax)); return true; }
        if (!_panelRect.Contains(x, y)) return false;
        _panelScroll = Math.Clamp(_panelScroll - notches * step, 0, Math.Max(0, _panelMax));
        return true;
    }

    /// <summary>How far the docked panel's content runs past its view (0 when it all fits) — never silently dropped.</summary>
    public double PanelOverflow => Math.Max(0, _panelMax);

    /// <summary>How far the flow's body runs past its view.</summary>
    public double FlowOverflow => Math.Max(0, _flowMax);

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);

    private double Px(TypeRole role, FontRole font = FontRole.Body) => TypeScale.Px(Theme, role, font) * Scale;
    private double Sp(double v) => v * Scale;
    private double Slot(TypeRole role, bool caps = false) => FlowText.Slot(role, Scale, caps);
    private List<string> Wrap(ITextMeasure m, string text, TypeRole role, double width, FontRole font = FontRole.Body) =>
        FlowText.Wrap(m, text, role, Scale, width, font);

    // The theme's text inks, once per theme.
    private EraTheme? _inkTheme;
    private TextInks _ink = null!;
    private TextInks Ink
    {
        get
        {
            if (!ReferenceEquals(_inkTheme, Theme)) { _inkTheme = Theme; _ink = Theme.TextInk; }
            return _ink;
        }
    }

    /// <summary>Writes a run in the era's style, fitted (shrunk to the Caption floor, then ellipsised) to <paramref name="w"/>.</summary>
    private void Put(DrawList d, ITextMeasure m, double x, double y, string text, double size, double w, Rgba color,
        TextAlign align = TextAlign.Left, FontRole role = FontRole.Body)
    {
        EraTheme t = Theme;
        double floor = TypeScale.Floor(t.Type.For(role).Face) * Scale / t.Type.SizeScale;
        double s = size <= floor ? size : Math.Max(floor, ThemeText.FitSize(m, t, text, size, w, role, Math.Min(1.0, floor / size)));
        d.Write(t, x, y, ThemeText.Fit(m, t, text, s, w, role), s, color, align, role);
    }

    /// <summary>The transition headline for an Age's full name: "Your civilization has entered the
    /// Bronze Age", "… the Neolithic / Agricultural Age" (the full name verbatim; "Age" is added only
    /// when the name does not already end with it).</summary>
    public static string EnteredLine(string ageName) =>
        "Your civilization has entered the " + ageName + (ageName.EndsWith("Age", StringComparison.Ordinal) ? "" : " Age");

    /// <summary>A button in the state table: Primary (accent-tinted, accent edge), Secondary (raised, hairline), or
    /// Disabled (sunken, dashed hairline, soft label). Hover warms Primary and Secondary.</summary>
    private void Button(DrawList d, ITextMeasure m, RectD r, string label, int id, bool primary, bool enabled)
    {
        EraTheme t = Theme;
        double caps = Px(TypeRole.Body, FontRole.Caps);
        bool hover = enabled && Over(r);
        if (!enabled)
        {
            d.Rect(r, A(t.Material.PanelSunken, 0.6));
            (double, double) dash = (Sp(5), Sp(4));
            Rgba hl = t.Material.Hairline;
            d.Line(r.X, r.Y, r.Right, r.Y, hl, 1, dash); d.Line(r.X, r.Bottom, r.Right, r.Bottom, hl, 1, dash);
            d.Line(r.X, r.Y, r.X, r.Bottom, hl, 1, dash); d.Line(r.Right, r.Y, r.Right, r.Bottom, hl, 1, dash);
            Put(d, m, r.CenterX, r.Y + (r.H - t.Type.Size(caps)) / 2 - Sp(2), label, caps, r.W - Sp(12), t.Ink.TextSoft, TextAlign.Center, FontRole.Caps);
            return;
        }
        Rgba fill = primary ? Mix(t.Material.PanelRaised, t.Material.Accent, hover ? 0.40 : 0.28) : hover ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : t.Material.PanelRaised;
        PanelFrame.Paint(d, r, t, id, FrameKind.Button, fill, primary ? t.Material.Accent : hover ? t.Material.BorderStrong : t.Material.Hairline, primary ? 2.0 : hover ? 1.4 : 0.9);
        Put(d, m, r.CenterX, r.Y + (r.H - t.Type.Size(caps)) / 2 - Sp(2), label, caps, r.W - Sp(12), t.Ink.Text, TextAlign.Center, FontRole.Caps);
    }

    /// <summary>A section heading in the panel/flow: capitals at the heading role in the accent's (or a semantic
    /// family's) TEXT ink.</summary>
    private double Heading(DrawList d, ITextMeasure m, double x, double y, double w, string text, Rgba ink)
    {
        Put(d, m, x, y, text, Px(TypeRole.Heading, FontRole.Caps), w, ink, TextAlign.Left, FontRole.Caps);
        return y + Slot(TypeRole.Heading, caps: true) + Sp(2);
    }

    /// <summary>
    /// THE DOCKED CAPITAL AGE PANEL (UR-5). It SCROLLS (the wheel over it, a bar and a "more below" mark when its
    /// content runs past the view) and never drops a line. Its blocks come in the order the player needs them: the
    /// banner; the STATUS (and the ADVANCE AGE button, 48 px, when eligible); STILL REQUIRED; the three progress chips
    /// in the body ink (≥ 7:1); the core and supporting milestones; the military modernization; and the way into
    /// Knowledge &amp; Technology. Flowed in the era-invariant reference type (<see cref="FlowText"/>), so its regions
    /// and hit rects are the same in every era.
    /// </summary>
    public void PaintPanel(DrawList d, ITextMeasure m, RectD r, string capitalName, bool clearHits = true)
    {
        if (clearHits) _hits = [];
        _info = [];
        _panelRect = r;
        EraTheme t = Theme;
        SemanticTokens s = t.Semantic;
        AgePanelModel p = Panel;
        PanelFrame.Paint(d, r, t, 501, FrameKind.Panel);
        double x = r.X + Sp(20), w = r.W - Sp(40);
        double caps = Px(TypeRole.Caption, FontRole.Caps), body = Px(TypeRole.Body), data = Px(TypeRole.Data, FontRole.Numeric);
        // The fixed header: whose panel it is, and its close button.
        var close = new RectD(r.Right - Sp(42), r.Y + Sp(10), Sp(30), Sp(30));
        Put(d, m, x, r.Y + Sp(16), "CAPITAL  -  " + capitalName.ToUpperInvariant(), caps, w - Sp(40), Ink.Accent, TextAlign.Left, FontRole.Caps);
        PanelFrame.Paint(d, close, t, 502, FrameKind.Button, Over(close) ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : null, Over(close) ? t.Material.BorderStrong : null);
        EraMarks.Close(d, t, close, t.Ink.TextSoft, 503);
        _hits.Add(new AgeHitRegion(close, AgeHit.ClosePanel, 0));

        // The fixed footer: the way into the trees (the K key and the status band's research chip do the same).
        var know = new RectD(x, r.Bottom - Sp(56), w, Sp(40));
        Button(d, m, know, "OPEN KNOWLEDGE & TECHNOLOGY  [K]", 509, primary: false, enabled: true);
        _hits.Add(new AgeHitRegion(know, AgeHit.OpenKnowledge, p.SuggestedNodeKey));   // M5 polish: opens at the research next
        var view = new RectD(r.X + Sp(4), r.Y + Sp(46), r.W - Sp(8), know.Y - Sp(8) - (r.Y + Sp(46)));
        d.PushClip(view);
        _infoClip = view;   // M5 polish: inspectable regions are cut to the scrolled view
        double top = view.Y + Sp(4);
        double y = top - _panelScroll;
        y = PaintPanelBody(d, m, p, x, w, y, view);
        _infoClip = null;
        d.PopClip();
        double content = y + _panelScroll - top;
        _panelMax = content - (view.H - Sp(8));
        if (_panelScroll > Math.Max(0, _panelMax)) _panelScroll = Math.Max(0, _panelMax);
        if (_panelMax > 0) ScrollMarks(d, r, view, content, _panelScroll, _panelMax);
    }

    private double PaintPanelBody(DrawList d, ITextMeasure m, AgePanelModel p, double x, double w, double y, RectD view)
    {
        EraTheme t = Theme;
        SemanticTokens s = t.Semantic;
        double caps = Px(TypeRole.Caption, FontRole.Caps), body = Px(TypeRole.Body), data = Px(TypeRole.Data, FontRole.Numeric);
        double second = Px(TypeRole.Secondary);
        if (p.State == AgePanelState.NoContent)
        {
            d.Write(t, x, y, "Ages are not loaded in this session.", body, t.Ink.TextSoft);
            return y + Slot(TypeRole.Body);
        }

        // (1) The banner: the Age numeral (Display) and its full name.
        List<string> nameLines = Wrap(m, p.CurrentAgeName, TypeRole.Heading, w - Sp(150), FontRole.Heading);
        double bh = Math.Max(Slot(TypeRole.Display), nameLines.Count * Slot(TypeRole.Heading) + Slot(TypeRole.Caption)) + Sp(20);
        var banner = new RectD(x, y, w, bh);
        PanelFrame.Paint(d, banner, t, 504, FrameKind.Card, Mix(t.Material.Panel, t.Material.Accent, 0.10), t.Material.Accent, 1.1);
        string numeral = "AGE " + AgePanelModel.Numeral(p.CurrentAge);
        Info(banner, InfoSubject.OfAge(p.CurrentAge), p.CurrentAgeName);
        d.Title(t, banner.X + Sp(14), banner.Y + Sp(10), numeral, Px(TypeRole.Display, FontRole.Title), Ink.Accent);
        double nx = banner.X + Sp(150);
        double ny = banner.Y + Sp(10);
        foreach (string line in nameLines) { d.Write(t, nx, ny, line, Px(TypeRole.Heading, FontRole.Heading), t.Ink.Text, TextAlign.Left, FontRole.Heading); ny += Slot(TypeRole.Heading); }
        string sub = p.Transitions.Count == 0 ? "founding Age" : "entered turn " + N(p.EnteredTurn) + (p.CurrentSurgeName is null ? "" : "  -  surge: " + p.CurrentSurgeName);
        Put(d, m, nx, ny, sub, Px(TypeRole.Caption), banner.Right - Sp(12) - nx, t.Ink.TextSoft);
        y = banner.Bottom + Sp(14);

        if (p.State == AgePanelState.FinalAge)
        {
            foreach (string line in Wrap(m, "This is the final Age. There is no further Age to enter.", TypeRole.Body, w)) { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += Slot(TypeRole.Body); }
            return y;
        }

        // (2) The status: where the capital stands, the next Age by name, and the action when there is one.
        y = PaintStatus(d, m, p, x, w, y, view) + Sp(12);

        // (3) STILL REQUIRED — what the player must still do, first.
        if (p.Remaining.Count > 0)
        {
            y = Heading(d, m, x, y, w, "STILL REQUIRED", Ink.Progress);
            int ri = 0;
            foreach (string sr in p.Remaining)
            {
                EraMarks.Tick(d, t, x + 1, y + Sp(6), Sp(12), false, s.Progress, 590 + ri++);
                foreach (string line in Wrap(m, sr, TypeRole.Body, w - Sp(22))) { d.Write(t, x + Sp(22), y, line, body, t.Ink.Text); y += Slot(TypeRole.Body); }
            }
            y += Sp(10);
        }

        // (4) The three progress chips — a label over its figure, in the body ink on the raised surface (≥ 7:1; they
        // measured 1.78:1), the edge and mark in the met / not-yet family.
        (string Label, string Figure, bool Ok)[] chips =
        [
            ("CORE", p.CoreMet + " / " + p.CoreTotal, p.CoreMet == p.CoreTotal),
            ("SUPPORTING", p.SupportingMet + " / " + p.SupportingRequired, p.SupportingMet >= p.SupportingRequired),
            ("CATEGORIES", p.CategoriesCovered + " / " + p.CategoriesRequired, p.CategoriesCovered >= p.CategoriesRequired),
        ];
        double chipH = Slot(TypeRole.Caption, caps: true) + Slot(TypeRole.Data) + Sp(12);
        double cw = (w - Sp(16)) / 3;
        for (int i = 0; i < 3; i++)
        {
            var c = new RectD(x + i * (cw + Sp(8)), y, cw, chipH);
            Rgba edge = chips[i].Ok ? s.Positive : s.Progress;
            PanelFrame.Paint(d, c, t, 505 + i, FrameKind.Chip, t.Material.PanelRaised, edge, 1.4);
            Put(d, m, c.X + Sp(10), c.Y + Sp(6), chips[i].Label, caps, c.W - Sp(20), t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
            double fy = c.Y + Sp(6) + Slot(TypeRole.Caption, caps: true);
            EraMarks.Tick(d, t, c.X + Sp(10), fy + Sp(4), Sp(13), chips[i].Ok, chips[i].Ok ? s.Positive : s.Progress, 508 + i);
            Put(d, m, c.X + Sp(30), fy, chips[i].Figure, data, c.W - Sp(40), t.Ink.Text, TextAlign.Left, FontRole.Numeric);
        }
        y += chipH + Sp(14);

        // (5) The milestones: core, then supporting by category.
        y = Heading(d, m, x, y, w, "CORE MILESTONES", Ink.Accent);
        int mi = 0;
        foreach (MilestoneLine l in p.Core) y = PaintMilestone(d, m, l, x, y, w, 520 + mi++);
        y += Sp(8);
        y = Heading(d, m, x, y, w, "SUPPORTING  -  BY CATEGORY", Ink.Accent);
        foreach (CategoryGroup g in p.Categories)
        {
            EraMarks.Tick(d, t, x + 1, y + Sp(6), Sp(12), g.Covered, g.Covered ? s.Positive : t.Ink.TextSoft, 560 + mi++);
            string state = g.Milestones.Count == 0 ? "no milestone in this Age" : g.Covered ? "covered" : "not covered";
            double sw = FlowText.Width(m, state, TypeRole.Secondary, Scale) + Sp(8);
            Put(d, m, x + Sp(22), y, g.Name, body, w - Sp(22) - sw, g.Covered ? t.Ink.Text : t.Ink.TextSoft, TextAlign.Left, FontRole.Heading);
            d.Write(t, x + w, y + Sp(2), state, second, g.Covered ? Ink.Positive : t.Ink.TextSoft, TextAlign.Right);
            y += Slot(TypeRole.Body);
            foreach (MilestoneLine l in g.Milestones) y = PaintMilestone(d, m, l, x + Sp(18), y, w - Sp(18), 520 + mi++);
            y += Sp(4);
        }

        // (6) The military modernization the advance brings.
        if (p.State is AgePanelState.Eligible or AgePanelState.Pending && Flow is { } fl)
        {
            y += Sp(8);
            y = Heading(d, m, x, y, w, "YOUR MILITARY WILL MODERNIZE", Ink.Accent);
            if (fl.Modernization.Count == 0) { d.Write(t, x + Sp(10), y, "No formations to modernize.", body, t.Ink.TextSoft); y += Slot(TypeRole.Body); }
            foreach (ModernizationLine ml in fl.Modernization)
            {
                string txt = ml.Count.ToString(CultureInfo.InvariantCulture) + " x " + ml.From + (ml.Changes ? "  ->  " + ml.To : "  -  " + AdvanceFlowModel.OutcomeText(ml.Outcome));
                foreach (string line in Wrap(m, txt, TypeRole.Body, w - Sp(10))) { d.Write(t, x + Sp(10), y, line, body, ml.Changes ? Ink.Military : t.Ink.TextSoft); y += Slot(TypeRole.Body); }
            }
            if (p.State == AgePanelState.Pending && p.PendingSurgeName is { } ps)
            {
                d.Write(t, x + Sp(10), y, "Selected surge: " + ps, body, Ink.Active);
                y += Slot(TypeRole.Body);
            }
        }

        // (7) The way into Knowledge & Technology is the panel's fixed footer.
        return y + Sp(10);
    }

    /// <summary>A scrolled view's bar and, while more lies below, a "more below" mark over its foot.</summary>
    private void ScrollMarks(DrawList d, RectD frame, RectD view, double content, double scroll, double max)
    {
        EraTheme t = Theme;
        var track = new RectD(frame.Right - Sp(11), view.Y + Sp(4), Sp(4), view.H - Sp(8));
        d.Rect(track, A(t.Material.Hairline, 0.5), null, 0, Sp(2));
        double th = Math.Max(Sp(24), track.H * view.H / Math.Max(view.H, content));
        d.Rect(new RectD(track.X, track.Y + (track.H - th) * (scroll / Math.Max(1, max)), track.W, th), t.Material.AccentSoft, null, 0, Sp(2));
        if (scroll < max - 0.5)
        {
            double cap = Px(TypeRole.Caption);
            var more = new RectD(view.X + Sp(12), view.Bottom - t.Type.Size(cap) - Sp(10), view.W - Sp(36), t.Type.Size(cap) + Sp(8));
            d.Rect(more, A(t.Material.Panel, 0.94));
            d.Write(t, more.CenterX, more.Y + Sp(2), "more below - scroll", cap, t.Ink.TextSoft, TextAlign.Center);
        }
    }

    private double PaintStatus(DrawList d, ITextMeasure m, AgePanelModel p, double x, double w, double y, RectD view)
    {
        EraTheme t = Theme;
        double heading = Px(TypeRole.Heading, FontRole.Heading), body = Px(TypeRole.Body);
        string next = "Next: Age " + AgePanelModel.Numeral(p.NextAge!.Value) + "  -  " + p.NextAgeName;
        (string head, Rgba ink, string line) = p.State switch
        {
            AgePanelState.Eligible => ("Advancement available - optional", Ink.Accent, "You may advance now or keep building in this Age."),
            AgePanelState.Pending => ("Advancing to " + p.NextAgeName + " next turn", Ink.Active,
                "Surge emphasis: " + p.PendingSurgeName + "  -  takes effect at End Turn " + N(p.Pending!.DecisionTurn) + "."),
            _ => ("Not yet eligible", t.Ink.Text, "The Advance button appears once every requirement below holds."),
        };
        List<string> headLines = Wrap(m, head, TypeRole.Heading, w - Sp(24), FontRole.Heading);
        List<string> lines = Wrap(m, line, TypeRole.Body, w - Sp(24));
        List<string> nextLines = Wrap(m, next, TypeRole.Body, w - Sp(24));
        bool button = p.State == AgePanelState.Eligible;
        double h = Sp(12) + headLines.Count * Slot(TypeRole.Heading) + (lines.Count + nextLines.Count) * Slot(TypeRole.Body) + (button ? Sp(58) : 0) + Sp(10);
        var r = new RectD(x, y, w, h);
        Rgba fill = p.State switch
        {
            AgePanelState.Eligible => Mix(t.Material.Panel, t.Material.Accent, 0.10),
            AgePanelState.Pending => t.Semantic.ActiveFill,
            _ => t.Material.PanelRaised,
        };
        PanelFrame.Paint(d, r, t, 510, FrameKind.Chip, fill, p.State == AgePanelState.Eligible ? t.Material.Accent : p.State == AgePanelState.Pending ? t.Semantic.Active : t.Material.Hairline,
            p.State == AgePanelState.NotEligible ? 0.9 : 1.2);
        double ty = r.Y + Sp(10);
        foreach (string l in headLines) { d.Write(t, r.X + Sp(12), ty, l, heading, ink, TextAlign.Left, FontRole.Heading); ty += Slot(TypeRole.Heading); }
        foreach (string l in lines) { d.Write(t, r.X + Sp(12), ty, l, body, t.Ink.TextSoft); ty += Slot(TypeRole.Body); }
        Info(new RectD(r.X + Sp(12), ty, r.W - Sp(24), nextLines.Count * Slot(TypeRole.Body)), InfoSubject.OfAge(p.NextAge!.Value), p.NextAgeName ?? "");
        foreach (string l in nextLines) { d.Write(t, r.X + Sp(12), ty, l, body, t.Ink.Text); ty += Slot(TypeRole.Body); }
        if (button)
        {
            var btn = new RectD(r.X + Sp(12), ty + Sp(6), r.W - Sp(24), Sp(48));
            Button(d, m, btn, "ADVANCE AGE", 511, primary: true, enabled: true);
            if (view.Contains(btn.CenterX, btn.CenterY)) _hits.Add(new AgeHitRegion(btn, AgeHit.OpenAdvance, 0));
            if (p.NextAge is int nextAge) Info(btn, InfoSubject.OfAge(nextAge), p.NextAgeName ?? "");
        }
        return r.Bottom;
    }

    private double PaintMilestone(DrawList d, ITextMeasure m, MilestoneLine l, double x, double y, double w, int id)
    {
        EraTheme t = Theme;
        Rgba c = l.Met ? t.Semantic.Positive : t.Ink.TextSoft;
        EraMarks.Tick(d, t, x + 1, y + Sp(6), Sp(12), l.Met, c, id);
        string count = l.Threshold > 1 ? N(l.Observed) + " / " + N(l.Threshold) : l.Met ? "done" : "not yet";
        if (!l.Met && l.Pending is not null) count = "pending";   // M5 R2b: an honest label, not a goal the player can reach today
        double data = Px(TypeRole.Data, FontRole.Numeric), body = Px(TypeRole.Body);
        double cwid = FlowText.Width(m, count, TypeRole.Data, Scale, FontRole.Numeric) + Sp(8);
        List<string> lines = Wrap(m, l.Name, TypeRole.Body, w - Sp(22) - cwid);
        double nameW = 0;
        foreach (string line in lines) nameW = Math.Max(nameW, FlowText.Width(m, line, TypeRole.Body, Scale));
        Info(new RectD(x + Sp(22), y, Math.Min(w - Sp(22) - cwid, nameW), lines.Count * Slot(TypeRole.Body)), InfoSubject.Milestone(l.AgeKey, l.Id), l.Name);
        d.Write(t, x + w, y + (t.Type.Size(body) - t.Type.Size(data)) / 2, count, data, l.Met ? Ink.Positive : t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
        foreach (string line in lines) { d.Write(t, x + Sp(22), y, line, body, l.Met ? t.Ink.Text : t.Ink.TextSoft); y += Slot(TypeRole.Body); }
        return PaintResearchNext(d, m, l, x, y, w);   // M5 polish (§10): the research to do next, clickable
    }

    /// <summary>
    /// THE ADVANCE AGE FLOW (UR-5): a modal with a fixed header (from → to), a fixed footer (NOT NOW and CONFIRM
    /// ADVANCE, 48 px) and a body that SCROLLS — the six surge cards in a grid that fits the window (three across from a
    /// 1,100 px modal, two below), each card as tall as its words; then the military modernization: your formations,
    /// and the unit-family graph's changes (the families not yet realized in one line). Text at the type scale's
    /// roles; nothing set below Caption; nothing dropped.
    /// </summary>
    public void PaintFlow(DrawList d, ITextMeasure m, double width, double height)
    {
        if (!FlowOpen || Flow is null) return;
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        AdvanceFlowModel f = Flow;
        d.Rect(new RectD(0, 0, width, height), A(t.Ink.Text, 0.55));
        double mw = Math.Min(Sp(1400), width - Sp(40)), mh = Math.Min(Sp(980), height - Sp(30));
        var r = new RectD((width - mw) / 2, (height - mh) / 2, mw, mh);
        PanelFrame.Paint(d, r, t, 530, FrameKind.Modal);
        _panelRect = r;
        double x = r.X + Sp(28), w = mw - Sp(56);

        // Header: from -> to.
        double y = r.Y + Sp(22);
        Put(d, m, x, y, "ADVANCE AGE", Px(TypeRole.Caption, FontRole.Caps), w, Ink.Accent, TextAlign.Left, FontRole.Caps);
        y += Slot(TypeRole.Caption, caps: true) + Sp(2);
        string from = "Age " + AgePanelModel.Numeral(f.FromAge) + "  " + f.FromAgeName;
        double fromW = FlowText.Width(m, from, TypeRole.Heading, Scale, FontRole.Heading);
        double disp = Px(TypeRole.Display, FontRole.Title);
        d.Write(t, x, y + Sp(8), from, Px(TypeRole.Heading, FontRole.Heading), t.Ink.TextSoft, TextAlign.Left, FontRole.Heading);
        double ax = x + fromW + Sp(18);
        EraMarks.Arrow(d, t, ax, y + Sp(22), ax + Sp(44), t.Material.Accent, 531);
        Put(d, m, ax + Sp(58), y, "Age " + AgePanelModel.Numeral(f.ToAge) + "  " + f.ToAgeName, disp, r.Right - Sp(28) - (ax + Sp(58)), t.Ink.Text, TextAlign.Left, FontRole.Title);
        y += Slot(TypeRole.Display) + Sp(2);
        d.Write(t, x, y, "The new Age begins next turn. Choose where the transition's energy goes.", Px(TypeRole.Body), t.Ink.TextSoft);
        y += Slot(TypeRole.Body) + Sp(6);
        PanelFrame.Rule(d, new RectD(r.X + Sp(20), y, r.W - Sp(40), Sp(6)), t, 532);
        y += Sp(12);

        // Footer: the decision.
        double footH = Sp(76);
        var foot = new RectD(r.X, r.Bottom - footH, r.W, footH);
        PanelFrame.Rule(d, new RectD(r.X + Sp(20), foot.Y, r.W - Sp(40), Sp(6)), t, 536);
        var confirm = new RectD(r.Right - Sp(28) - Sp(250), foot.Y + Sp(16), Sp(250), Sp(48));
        var cancel = new RectD(confirm.X - Sp(14) - Sp(170), foot.Y + Sp(16), Sp(170), Sp(48));
        Button(d, m, cancel, "NOT NOW", 534, primary: false, enabled: true);
        _hits.Add(new AgeHitRegion(cancel, AgeHit.Cancel, 0));
        bool can = SelectedSurge != 0;
        Button(d, m, confirm, "CONFIRM ADVANCE", 535, primary: true, enabled: can);
        if (can) _hits.Add(new AgeHitRegion(confirm, AgeHit.Confirm, 0));
        string hint = can ? "Logged as an order now; the next turn begins in " + f.ToAgeName + "." : "Choose a surge emphasis to confirm.";
        double hw = cancel.X - Sp(20) - x;
        List<string> hl = Wrap(m, hint, TypeRole.Body, hw);
        double hy = foot.Y + Sp(16) + (Sp(48) - hl.Count * Slot(TypeRole.Body)) / 2;
        foreach (string line in hl) { d.Write(t, x, hy, line, Px(TypeRole.Body), can ? t.Ink.TextSoft : Ink.Progress); hy += Slot(TypeRole.Body); }

        // The body scrolls between them.
        var body = new RectD(r.X + Sp(6), y, r.W - Sp(12), foot.Y - Sp(4) - y);
        _flowBody = body;
        d.PushClip(body);
        double top = body.Y + Sp(4);
        double end = PaintFlowBody(d, m, f, x, w, top - _flowScroll, body);
        d.PopClip();
        double content = end + _flowScroll - top;
        _flowMax = content - (body.H - Sp(8));
        if (_flowScroll > Math.Max(0, _flowMax)) _flowScroll = Math.Max(0, _flowMax);
        if (_flowMax > 0) ScrollMarks(d, r, body, content, _flowScroll, _flowMax);
    }

    private double PaintFlowBody(DrawList d, ITextMeasure m, AdvanceFlowModel f, double x, double w, double y, RectD view)
    {
        EraTheme t = Theme;
        SemanticTokens sm = t.Semantic;
        double body = Px(TypeRole.Body), second = Px(TypeRole.Secondary), capsC = Px(TypeRole.Caption, FontRole.Caps);

        // 1  Surge emphasis.
        y = Heading(d, m, x, y, w, "1   CHOOSE A SURGE EMPHASIS", Ink.Accent);
        foreach (string line in Wrap(m, "Effects are qualitative: the surge's strength and duration are not yet simulated.", TypeRole.Body, w))
        { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += Slot(TypeRole.Body); }
        if (f.Surges.Count > 0)
            foreach (string line in Wrap(m, "Shape of every surge: " + f.Surges[0].Shape, TypeRole.Secondary, w))
            { d.Write(t, x, y, line, second, t.Ink.TextSoft); y += Slot(TypeRole.Secondary); }
        y += Sp(10);
        int n = f.Surges.Count;
        int cols = w >= Sp(1040) ? 3 : 2;
        double gap = Sp(14), cardW = (w - gap * (cols - 1)) / cols, pad = Sp(16);
        double inner = cardW - 2 * pad;
        for (int row = 0; row * cols < n; row++)
        {
            // A row of cards is as tall as its wordiest card.
            double rowH = 0;
            var layouts = new List<(SurgeCard S, List<string> Desc, List<string> Eff)>();
            for (int i = row * cols; i < Math.Min(n, row * cols + cols); i++)
            {
                SurgeCard s = f.Surges[i];
                List<string> desc = Wrap(m, s.Description, TypeRole.Body, inner);
                List<string> eff = Wrap(m, s.ExpectedEffect, TypeRole.Body, inner - Sp(84));
                double h = pad + Slot(TypeRole.Caption, caps: true) + Slot(TypeRole.Heading) + Sp(4) + desc.Count * Slot(TypeRole.Body) + Sp(8)
                    + eff.Count * Slot(TypeRole.Body) + Sp(4) + Slot(TypeRole.Caption, caps: true) + Sp(10) + pad;
                rowH = Math.Max(rowH, h);
                layouts.Add((s, desc, eff));
            }
            for (int k = 0; k < layouts.Count; k++)
            {
                (SurgeCard s, List<string> desc, List<string> eff) = layouts[k];
                int i = row * cols + k;
                var c = new RectD(x + k * (cardW + gap), y, cardW, rowH);
                bool on = s.Key == SelectedSurge, hover = !on && Over(c) && view.Contains(Pointer!.Value.X, Pointer!.Value.Y);
                PanelFrame.Paint(d, c, t, 540 + i, FrameKind.Card, on ? Mix(t.Material.Panel, t.Material.Accent, 0.16) : hover ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.10) : t.Material.PanelRaised,
                    on ? t.Material.Accent : hover ? t.Material.BorderStrong : t.Material.Hairline, on ? 2.2 : hover ? 1.4 : 0.9);
                double ci = PanelFrame.ContentInset(t, FrameKind.Card);
                if (on) d.Rect(new RectD(c.X + ci, c.Y + ci + Sp(4), Sp(5), c.H - 2 * ci - Sp(8)), t.Material.Accent);
                double cy = c.Y + pad;
                Put(d, m, c.X + pad, cy, s.Domain.ToUpperInvariant() + (on ? "  -  CHOSEN" : ""), capsC, inner, on ? Ink.Accent : t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
                cy += Slot(TypeRole.Caption, caps: true);
                Put(d, m, c.X + pad, cy, s.Name, Px(TypeRole.Heading, FontRole.Heading), inner, t.Ink.Text, TextAlign.Left, FontRole.Heading);
                cy += Slot(TypeRole.Heading) + Sp(4);
                foreach (string line in desc) { d.Write(t, c.X + pad, cy, line, body, t.Ink.TextSoft); cy += Slot(TypeRole.Body); }
                cy += Sp(8);
                Put(d, m, c.X + pad, cy + Sp(3), "EFFECT", capsC, Sp(80), t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
                foreach (string line in eff) { d.Write(t, c.X + pad + Sp(84), cy, line, body, t.Ink.Text); cy += Slot(TypeRole.Body); }
                cy += Sp(4);
                Put(d, m, c.X + pad, cy + Sp(3), "FEEDS", capsC, Sp(80), t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
                double px = c.X + pad + Sp(84);
                int pk = 0;
                foreach (string sys in s.DownstreamSystems)
                {
                    double pw = FlowText.Width(m, sys, TypeRole.Caption, Scale, FontRole.Caps) + Sp(16);
                    if (px + pw > c.Right - pad) { pw = c.Right - pad - px; if (pw < Sp(60)) break; }
                    PanelFrame.Paint(d, new RectD(px, cy, pw, Slot(TypeRole.Caption, caps: true) + Sp(4)), t, 560 + i * 8 + pk++, FrameKind.Chip, Mix(t.Material.Panel, sm.Knowledge, 0.10), A(sm.Knowledge, 0.8), 0.9);
                    Put(d, m, px + pw / 2, cy + Sp(3), sys, capsC, pw - Sp(10), Ink.Knowledge, TextAlign.Center, FontRole.Caps);
                    px += pw + Sp(6);
                }
                if (view.Contains(c.CenterX, Math.Clamp(c.CenterY, view.Y + 1, view.Bottom - 1)) && c.Bottom > view.Y && c.Y < view.Bottom)
                    _hits.Add(new AgeHitRegion(Clip(c, view), AgeHit.Surge, s.Key));
            }
            y += rowH + gap;
        }
        y += Sp(6);

        // 2  Military modernization.
        y = Heading(d, m, x, y, w, "2   YOUR MILITARY WILL MODERNIZE", Ink.Accent);
        foreach (string line in Wrap(m, "Automatic and free at the transition (ruling 18). Read from the unit-family graph.", TypeRole.Body, w))
        { d.Write(t, x, y, line, body, t.Ink.TextSoft); y += Slot(TypeRole.Body); }
        y += Sp(8);
        double colW = (w - Sp(24)) / 2;
        double lx = x, rx = x + colW + Sp(24);
        Put(d, m, lx, y, "YOUR FORMATIONS (" + f.Formations.ToString(CultureInfo.InvariantCulture) + ")", capsC, colW, t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
        Put(d, m, rx, y, "ACROSS THE UNIT-FAMILY GRAPH", capsC, colW, t.Ink.TextSoft, TextAlign.Left, FontRole.Caps);
        double ly = y + Slot(TypeRole.Caption, caps: true) + Sp(4), ry = ly;
        if (f.Modernization.Count == 0) { d.Write(t, lx, ly, "You have no formations to modernize.", body, t.Ink.TextSoft); ly += Slot(TypeRole.Body); }
        int mk = 0;
        foreach (ModernizationLine l in f.Modernization)
        {
            double rh = Slot(TypeRole.Body) + Slot(TypeRole.Secondary) + Sp(14);
            var row = new RectD(lx, ly, colW, rh);
            PanelFrame.Paint(d, row, t, 580 + mk++, FrameKind.Card, t.Material.PanelRaised, l.Changes ? sm.Military : t.Material.Hairline, l.Changes ? 1.4 : 0.8);
            string head = l.Count.ToString(CultureInfo.InvariantCulture) + " x " + l.From + (l.Changes ? "  ->  " + l.To : "");
            Put(d, m, row.X + Sp(12), row.Y + Sp(6), head, Px(TypeRole.Body, FontRole.Heading), row.W - Sp(24), l.Changes ? Ink.Military : t.Ink.Text, TextAlign.Left, FontRole.Heading);
            Put(d, m, row.X + Sp(12), row.Y + Sp(6) + Slot(TypeRole.Body), l.FamilyName + "  -  " + AdvanceFlowModel.OutcomeText(l.Outcome), second, row.W - Sp(24), t.Ink.TextSoft);
            ly += rh + Sp(6);
        }
        var later = new List<string>();
        foreach (FamilyLine fl in f.FamilyChanges)
        {
            bool change = fl.To is not null && fl.From != fl.To;
            if (fl.From is null && fl.To is null) { later.Add(fl.FamilyName); continue; }
            string txt = fl.From is null ? "first appears: " + fl.To
                : change ? fl.From + "  ->  " + fl.To
                : fl.From + "  (" + AdvanceFlowModel.OutcomeText(fl.Outcome) + ")";
            double nameW = colW * 0.42;
            List<string> nl = Wrap(m, fl.FamilyName, TypeRole.Body, nameW - Sp(8));
            List<string> tl = Wrap(m, txt, TypeRole.Body, colW - nameW);
            double rowY = ry;
            foreach (string line in nl) { d.Write(t, rx, ry, line, body, change ? t.Ink.Text : t.Ink.TextSoft); ry += Slot(TypeRole.Body); }
            double ty2 = rowY;
            foreach (string line in tl) { d.Write(t, rx + nameW, ty2, line, body, change ? Ink.Military : t.Ink.TextSoft); ty2 += Slot(TypeRole.Body); }
            ry = Math.Max(ry, ty2) + Sp(2);
        }
        if (later.Count > 0)
        {
            ry += Sp(4);
            foreach (string line in Wrap(m, "Not yet realized in this Age: " + string.Join(", ", later) + ".", TypeRole.Body, colW))
            { d.Write(t, rx, ry, line, body, t.Ink.TextSoft); ry += Slot(TypeRole.Body); }
        }
        return Math.Max(ly, ry) + Sp(10);
    }

    private static RectD Clip(RectD a, RectD b)
    {
        double x0 = Math.Max(a.X, b.X), y0 = Math.Max(a.Y, b.Y), x1 = Math.Min(a.Right, b.Right), y1 = Math.Min(a.Bottom, b.Bottom);
        return new RectD(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
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
        double w = Math.Min(Sp(760), width - 40);
        double ins = Sp(14);   // clears every era's frame and ornament band
        double h = ins * 2 + Slot(TypeRole.Caption, caps: true) + Slot(TypeRole.Title) + Slot(TypeRole.Body) + Slot(TypeRole.Secondary) + Sp(4);
        var r = new RectD((width - w) / 2, top, w, h);
        // The frame fades with the panel: paint it into a scratch list, then copy with alpha.
        var frame = new DrawList();
        PanelFrame.Paint(frame, r, t, 550, FrameKind.Toast, null, t.Material.Accent, 1.4);
        foreach (DrawCmd c in frame.Commands) d.Add(Faded(c, fade));
        double y = r.Y + ins;
        Put(d, m, r.CenterX, y, _toastTitle!, Px(TypeRole.Caption, FontRole.Caps), r.W - Sp(40), A(Ink.Accent, fade), TextAlign.Center, FontRole.Caps);
        y += Slot(TypeRole.Caption, caps: true);
        Put(d, m, r.CenterX, y, _toastHeadline!, Px(TypeRole.Title, FontRole.Title), r.W - Sp(40), A(t.Ink.Text, fade), TextAlign.Center, FontRole.Title);
        y += Slot(TypeRole.Title);
        Put(d, m, r.CenterX, y, _toastBody!, Px(TypeRole.Body), r.W - Sp(40), A(t.Ink.TextSoft, fade), TextAlign.Center);
        y += Slot(TypeRole.Body);
        Put(d, m, r.CenterX, y, "Records are now kept in " + t.Medium + ".", Px(TypeRole.Secondary), r.W - Sp(40), A(Ink.Accent, fade), TextAlign.Center);
    }

    private static DrawCmd Faded(DrawCmd c, double f) => c switch
    {
        RectCmd r => r with { Fill = r.Fill is Rgba a ? FadeTo(a, f) : null, Stroke = r.Stroke is Rgba b ? FadeTo(b, f) : null },
        LineCmd l => l with { Color = FadeTo(l.Color, f) },
        PolygonCmd p => p with { Fill = FadeTo(p.Fill, f) },
        PolylineCmd p => p with { Color = FadeTo(p.Color, f) },
        CircleCmd ci => ci with { Fill = ci.Fill is Rgba a ? FadeTo(a, f) : null, Stroke = ci.Stroke is Rgba b ? FadeTo(b, f) : null },
        BezierCmd b => b with { Color = FadeTo(b.Color, f) },
        ArcCmd a => a with { Color = FadeTo(a.Color, f) },
        _ => c,
    };

    private static Rgba FadeTo(Rgba c, double f) => new(c.R, c.G, c.B, (byte)Math.Round(c.A * Math.Clamp(f, 0, 1)));

    /// <summary>For previews: the toast frozen at full opacity.</summary>
    public void HoldToast() => _toastAge = 1.0;
}
