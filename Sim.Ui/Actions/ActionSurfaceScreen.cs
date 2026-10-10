using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Actions;

/// <summary>What a click on the action surface asks the host to do. Order-bearing commands are dispatched
/// through the session's guarded emitters (<see cref="ActionDispatch"/>); the rest move the UI.</summary>
public enum ActionCommandKind
{
    None = 0,
    /// <summary>Emit the five-sector labour batch for <see cref="ActionCommand.Settlement"/>.</summary>
    ApplyLabour = 1,
    ClearResearch = 2,
    /// <summary>Open the Technology / Civics trees (UI only).</summary>
    OpenResearch = 3,
    /// <summary>Open the capital's Age panel and its ADVANCE AGE flow (the unchanged order path).</summary>
    AdvanceAge = 4,
    /// <summary>Queue <see cref="ActionCommand.Project"/> in <see cref="ActionCommand.Settlement"/>.</summary>
    Enqueue = 5,
    /// <summary>Develop roads: <see cref="ActionCommand.Percent"/>% of eligible demand.</summary>
    DevelopRoads = 6,
    /// <summary>Declare the tax levy at <see cref="ActionCommand.Percent"/>%.</summary>
    SetTax = 7,
    /// <summary>Select another controlled settlement (UI only).</summary>
    SelectSettlement = 8,
}

public sealed record ActionCommand(
    ActionCommandKind Kind, int Settlement = -1, ImmutableArray<int> Weights = default, int Project = -1, double Percent = 0.0)
{
    public static readonly ActionCommand None = new(ActionCommandKind.None);

    /// <summary>Whether the command would append an order (the rest only move the UI).</summary>
    public bool IsOrder => Kind is ActionCommandKind.ApplyLabour or ActionCommandKind.ClearResearch or ActionCommandKind.Enqueue
        or ActionCommandKind.DevelopRoads or ActionCommandKind.SetTax;
}

/// <summary>THE ONE DISPATCH of an order-bearing command, through the session's guarded emitters (each
/// refuses what the simulation would refuse). Returns true when an order was appended.</summary>
public static class ActionDispatch
{
    public static bool Apply(UiSession session, ActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        return command.Kind switch
        {
            ActionCommandKind.ApplyLabour => !command.Weights.IsDefault && session.EmitSectorOrders(command.Weights.AsSpan(), command.Settlement),
            ActionCommandKind.ClearResearch => session.ClearResearchTarget(),
            ActionCommandKind.Enqueue => session.EmitConstructionOrder(command.Settlement, command.Project),
            ActionCommandKind.DevelopRoads => session.EmitRoadOrder(command.Percent),
            ActionCommandKind.SetTax => session.EmitTaxOrder((int)Math.Round(command.Percent, MidpointRounding.AwayFromZero)),
            _ => false,
        };
    }
}

/// <summary>What a painted region of the surface does when clicked.</summary>
public enum ActionHitKind
{
    LabourSlot, LabourTrack, LabourMinus, LabourPlus, LabourApply, LabourReset,
    ResearchOpen, ResearchClear, AgeAdvance, Build,
    RoadSlot, RoadTrack, RoadApply,
    TaxSlot, TaxTrack, TaxApply,
    PrevSettlement, NextSettlement,
}

public readonly record struct ActionHit(RectD Rect, ActionHitKind Kind, int A, int B);

/// <summary>
/// THE ACTION SURFACE, painted (ADR-033 D1/D2; directive "ACTION / OPTION UI EVOLUTION"). It paints an
/// <see cref="ActionSurfaceModel"/> into a backend-agnostic <see cref="DrawList"/> — the game replays it through
/// the ImGui backend inside the POLICY panel, the headless preview through the SVG writer — so the live and
/// the previewed surface are the same code. It owns UI state only (the labour, tax and road drafts and a
/// slider drag); a click answers with an <see cref="ActionCommand"/> for the host to dispatch, never a write.
///
/// THE ERA DECIDES HOW, THE QUERY DECIDES WHAT. Which blocks exist is the model's (the query's); the era's
/// tokens decide their form: <see cref="LabourControlSpec"/> (pebbles and counters with no numerals at
/// A1–A3, a notched rule of twenty from A4, sliders with numbers from A7) and <see cref="SurfaceLayout"/>
/// (a short flat list at A1, domain groups with headers, denser detail at the later densities).
/// </summary>
public sealed partial class ActionSurfaceScreen
{
    public EraTheme Theme { get; set; } = EraThemes.For(UiEra.Prehistoric);
    public ActionSurfaceModel? Model { get; private set; }
    public IReadOnlyList<ActionHit> Hits => _hits;

    private List<ActionHit> _hits = [];
    private IReadOnlyWorldState? _world;
    private SimConfig? _cfg;
    private PolityId _player;
    private Func<int, string> _name = id => "settlement " + id.ToString(CultureInfo.InvariantCulture);

    // UI state only — nothing here is read by the simulation, and none of it is an order until dispatched.
    private readonly int[] _draft = new int[Sectors.Count];
    private (int Settlement, long Turn, int Units) _draftKey = (-1, -1, 0);
    private int _taxDraft = -1;
    private long _taxDraftTurn = -1;
    private int _roadDraft = 10;
    private ActionHitKind? _drag;
    private int _dragSector = -1;
    private RectD _dragTrack;
    private RoadPlanPreview? _roadPreview;

    /// <summary>The labour split the control holds, in the era's units.</summary>
    public ReadOnlySpan<int> Draft => _draft;

    /// <summary>The tax levy the control holds, in percent, or -1 when there is no tax edict.</summary>
    public int TaxDraft => _taxDraft;

    /// <summary>The road development percentage the control holds.</summary>
    public int RoadDraft => _roadDraft;

    /// <summary>Whether the labour control differs from what the settlement runs (or has queued).</summary>
    public bool LabourChanged
    {
        get
        {
            if (Model?.Labour is not { } l) return false;
            ImmutableArray<int> b = l.Baseline;
            for (int s = 0; s < Sectors.Count; s++) if (_draft[s] != b[s]) return true;
            return false;
        }
    }

    /// <summary>Takes a new model (after an End Turn, an order, a selection change or an era change). The labour
    /// draft is reset to the settlement's baseline when the settlement, the turn or the era's unit count changed;
    /// the tax draft when the turn changed.</summary>
    public void Refresh(ActionSurfaceModel model, IReadOnlyWorldState world, SimConfig cfg, PolityId player, Func<int, string> name)
    {
        Model = model;
        _world = world;
        _cfg = cfg;
        _player = player;
        _name = name;
        if (model.Labour is { } l)
        {
            var key = (l.Settlement.Value, world.Clock.Turn, l.Control.Units);
            if (key != _draftKey)
            {
                for (int s = 0; s < Sectors.Count; s++) _draft[s] = l.Baseline[s];
                _draftKey = key;
            }
        }
        if (model.Governance is { } g)
        {
            if (_taxDraftTurn != world.Clock.Turn || _taxDraft < 0)
            {
                _taxDraft = g.QueuedPercent ?? g.DeclaredPercent;
                _taxDraftTurn = world.Clock.Turn;
            }
        }
        else _taxDraft = -1;
        int unit = model.Control.PercentPerUnit;
        _roadDraft = Math.Clamp((_roadDraft + unit - 1) / unit * unit, unit, 100);
        _roadPreview = null;
    }

    // ================================================================== input

    /// <summary>A click (press) at a screen point: changes a draft, starts a slider drag, or answers with the
    /// command to dispatch.</summary>
    public ActionCommand Click(double x, double y)
    {
        if (Model is not { } model) return ActionCommand.None;
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            ActionHit h = _hits[i];
            if (!h.Rect.Contains(x, y)) continue;
            LabourControlSpec spec = model.Control;
            switch (h.Kind)
            {
                case ActionHitKind.LabourSlot:
                    MoveLabour(h.A, _draft[h.A] == h.B + 1 ? h.B : h.B + 1);
                    return ActionCommand.None;
                case ActionHitKind.LabourTrack:
                    _drag = h.Kind; _dragSector = h.A; _dragTrack = h.Rect;
                    MoveLabour(h.A, TrackValue(h.Rect, x, spec.Units));
                    return ActionCommand.None;
                case ActionHitKind.LabourMinus: MoveLabour(h.A, _draft[h.A] - 1); return ActionCommand.None;
                case ActionHitKind.LabourPlus: MoveLabour(h.A, _draft[h.A] + 1); return ActionCommand.None;
                case ActionHitKind.LabourReset:
                    if (model.Labour is { } lr) for (int s = 0; s < Sectors.Count; s++) _draft[s] = lr.Baseline[s];
                    return ActionCommand.None;
                case ActionHitKind.LabourApply:
                    if (model.Labour is not { } la || !LabourChanged) return ActionCommand.None;
                    return new ActionCommand(ActionCommandKind.ApplyLabour, la.Settlement.Value, Weights(spec));
                case ActionHitKind.ResearchOpen: return new ActionCommand(ActionCommandKind.OpenResearch);
                case ActionHitKind.ResearchClear: return new ActionCommand(ActionCommandKind.ClearResearch);
                case ActionHitKind.AgeAdvance: return new ActionCommand(ActionCommandKind.AdvanceAge);
                case ActionHitKind.Build: return new ActionCommand(ActionCommandKind.Enqueue, h.A, Project: h.B);
                case ActionHitKind.RoadSlot:
                    _roadDraft = Math.Clamp((h.B + 1) * spec.PercentPerUnit, spec.PercentPerUnit, 100);
                    _roadPreview = null;
                    return ActionCommand.None;
                case ActionHitKind.RoadTrack:
                    _drag = h.Kind; _dragTrack = h.Rect;
                    _roadDraft = Math.Clamp(TrackValue(h.Rect, x, 100), 1, 100);
                    _roadPreview = null;
                    return ActionCommand.None;
                case ActionHitKind.RoadApply: return new ActionCommand(ActionCommandKind.DevelopRoads, Percent: _roadDraft);
                case ActionHitKind.TaxSlot:
                    _taxDraft = (_taxDraft == (h.B + 1) * spec.PercentPerUnit ? h.B : h.B + 1) * spec.PercentPerUnit;
                    return ActionCommand.None;
                case ActionHitKind.TaxTrack:
                    _drag = h.Kind; _dragTrack = h.Rect;
                    _taxDraft = TrackValue(h.Rect, x, 100);
                    return ActionCommand.None;
                case ActionHitKind.TaxApply: return new ActionCommand(ActionCommandKind.SetTax, Percent: _taxDraft);
                case ActionHitKind.PrevSettlement:
                case ActionHitKind.NextSettlement:
                    return new ActionCommand(ActionCommandKind.SelectSettlement, h.A);
            }
        }
        return ActionCommand.None;
    }

    /// <summary>The pointer moved with the button held: continues a slider drag. True when one is in progress.</summary>
    public bool Drag(double x)
    {
        if (_drag is not { } kind || Model is not { } model) return false;
        switch (kind)
        {
            case ActionHitKind.LabourTrack: MoveLabour(_dragSector, TrackValue(_dragTrack, x, model.Control.Units)); break;
            case ActionHitKind.RoadTrack: _roadDraft = Math.Clamp(TrackValue(_dragTrack, x, 100), 1, 100); _roadPreview = null; break;
            case ActionHitKind.TaxTrack: _taxDraft = TrackValue(_dragTrack, x, 100); break;
        }
        return true;
    }

    /// <summary>The button was released: ends any drag.</summary>
    public void Release() { _drag = null; _dragSector = -1; }

    private void MoveLabour(int sector, int requested)
    {
        if (Model?.Labour is not { } l) return;
        SectorAllocationModel.Rebalance(_draft, sector, Math.Clamp(requested, 0, l.Control.Units), l.Control.Units);
    }

    private static int TrackValue(RectD track, double x, int units) =>
        (int)Math.Round(Math.Clamp((x - track.X) / Math.Max(1.0, track.W), 0.0, 1.0) * units, MidpointRounding.AwayFromZero);

    /// <summary>The draft as the order's percentage weights (units × points per unit; they sum to 100).</summary>
    public ImmutableArray<int> Weights(LabourControlSpec spec)
    {
        var w = new int[Sectors.Count];
        for (int s = 0; s < Sectors.Count; s++) w[s] = _draft[s] * spec.PercentPerUnit;
        return [.. w];
    }

    // ================================================================== painting

    /// <summary>The UI scale the surface is set at (UR-1): every role size and spacing is × this.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>The pointer over the surface this frame (screen px), or null when it is elsewhere (UR-6: the
    /// surface's controls answer hover, as the ImGui chrome's do).</summary>
    public (double X, double Y)? Pointer { get; private set; }

    /// <summary>Tracks the pointer for hover (null: not over the surface).</summary>
    public void PointerMove(double? x, double? y) => Pointer = x is double px && y is double py ? (px, py) : null;

    /// <summary>How a DrawList control is drawn (UR-6, the spec's one state table).</summary>
    public enum ControlStyle
    {
        /// <summary>The block's verb: accent-tinted, an accent edge, the label in the body role.</summary>
        Primary,
        /// <summary>Any other control: the raised surface, a hairline edge.</summary>
        Secondary,
        /// <summary>Blocked: a sunken dashed plate, the label in the soft ink after a lock — it reads as unavailable.</summary>
        Disabled,
    }

    private EraTheme T => Theme;
    private double L(double size) => T.Type.Line(size);
    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);
    private static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string N1(double v) => v.ToString("#,0.#", CultureInfo.InvariantCulture);
    private static string P(double fraction) => (fraction * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";

    // UR-6: every run is set by ROLE (TypeScale), never by a literal: the design size of a role in the face the era
    // sets the font role in, × the UI scale (ThemeText.Write applies the era's size scale on top, never below 1).
    private double Px(TypeRole role, FontRole style = FontRole.Body) => TypeScale.Px(T, role, style) * Scale;
    private double Body => Px(TypeRole.Body);
    private double BodyBold => Px(TypeRole.Body, FontRole.Heading);
    // UR-7 (the census, M5 polish stage B): 72–88 % of this surface's characters were explanations at the Secondary
    // role ("makes wild food (grain)", blockers, notes) — what the player reads to decide is set at the BODY role in
    // the soft ink: the hierarchy is ink and weight (the heading and the labels are bold, the figures a column), not a
    // smaller size.
    private double Second => Px(TypeRole.Body);
    private double Caption => Px(TypeRole.Caption);
    private double Data => Px(TypeRole.Data, FontRole.Numeric);
    private double Sp(double px) => px * Scale;

    /// <summary>The identity ink of a labour sector — the map's sector arcs, constant in every era.</summary>
    public static Rgba SectorInk(int sector) => MapInk.Default.Sectors[Math.Clamp(sector, 0, MapInk.Default.Sectors.Count - 1)];

    /// <summary>
    /// Paints the surface from (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="width"/> and returns
    /// the height it used; records the hit regions <see cref="Click"/> answers. UR-6: the blocks in DECISION order —
    /// what you ordered this turn, then Labour, Learning, the Age (when it can advance), Tax (when available),
    /// Building, Roads, Crafts, Arms, and what the people do on their own.
    /// </summary>
    public double Paint(DrawList d, ITextMeasure m, double x, double y, double width)
    {
        _hits = [];
        _info = [];
        if (Model is not { } model) return 0;
        EraTheme t = T;
        double y0 = y;
        bool flat = model.Layout == SurfaceLayout.Flat;
        double gap = Math.Max(Sp(14), t.Density.Gap * Scale * 1.4);

        if (!model.Notices.IsDefaultOrEmpty) y = PaintNotices(d, m, model, x, y, width) + gap;
        if (model.Labour is { } labour) y = PaintLabour(d, m, model, labour, x, y, width, flat) + gap;
        if (model.Research is { } research) y = PaintResearch(d, m, model, research, x, y, width, flat) + gap;
        if (model.Age is { } age) y = PaintAge(d, m, age, x, y, width, flat) + gap;
        if (model.Governance is { } gov) y = PaintGovernance(d, m, model, gov, x, y, width, flat) + gap;
        if (model.Construction is { } construction) y = PaintConstruction(d, m, model, construction, x, y, width, flat) + gap;
        if (model.Roads is { } roads) y = PaintRoads(d, m, model, roads, x, y, width, flat) + gap;
        if (model.Production is { } production) y = PaintProduction(d, m, production, x, y, width, flat) + gap;
        if (model.Military is { } military) y = PaintMilitary(d, m, model, military, x, y, width, flat) + gap;
        if (!model.Locked.IsDefaultOrEmpty) y = PaintLocked(d, m, model, x, y, width, flat) + gap;   // M5 polish: visibly locked
        if (model.Standing is { } standing) y = PaintStanding(d, m, model, standing, x, y, width, flat);
        return y - y0;
    }

    /// <summary>A block's heading (UR-6: the Heading role): at the flat density a plain lead-in line, at the grouped
    /// densities a capitalised header in the accent's TEXT ink with the era's rule under it.</summary>
    private double Heading(DrawList d, ITextMeasure m, string text, double x, double y, double w, bool flat, int id)
    {
        EraTheme t = T;
        if (flat)
        {
            double hs = Px(TypeRole.Heading, FontRole.Heading);
            d.Write(t, x, y, ThemeText.Fit(m, t, text, hs, w, FontRole.Heading), hs, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            return y + L(hs) + Sp(4);
        }
        double cs = Px(TypeRole.Heading, FontRole.Caps);
        d.Write(t, x, y, ThemeText.Fit(m, t, text, cs, w, FontRole.Caps), cs, t.TextInk.Accent, TextAlign.Left, FontRole.Caps);
        double ry = y + L(cs) + Sp(2);
        PanelFrame.Rule(d, new RectD(x, ry, w, Math.Max(2, t.Edge.BorderPx)), t, id);
        return ry + Math.Max(2, t.Edge.BorderPx) + Sp(8);
    }

    private double Wrapped(DrawList d, ITextMeasure m, string text, double x, double y, double w, double size, Rgba color, FontRole role = FontRole.Body)
    {
        foreach (string line in ThemeText.Wrap(m, T, text, size, w, role))
        {
            d.Write(T, x, y, line, size, color, TextAlign.Left, role);
            y += L(size);
        }
        return y;
    }

    /// <summary>One word of a rich line: its text and whether it is emphasised.</summary>
    private readonly record struct RichWord(string Text, bool Bold);

    /// <summary>
    /// UR-6 — A BLOCKER, READABLE: "needs 40 timber (has 0), 20 stone (has 0)" wrapped at the Secondary role in the
    /// progress family's TEXT ink, with every MISSING QUANTITY ("40 timber") emphasised — the thing the player has to
    /// go and get is the first thing the eye lands on (they were 11.5 px amber at 1.88:1).
    /// </summary>
    private double Blocker(DrawList d, ITextMeasure m, string lead, string blocker, double x, double y, double w, double size,
        List<(string Token, InfoSubject Subject)>? tokens = null)
    {
        EraTheme t = T;
        var words = new List<RichWord>();
        foreach (string word in lead.Split(' ', StringSplitOptions.RemoveEmptyEntries)) words.Add(new RichWord(word, false));
        string[] parts = blocker.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            // "<quantity> <good>" followed by "(has …)": the missing amount and what it is.
            bool qty = i + 2 < parts.Length && StartsWithDigit(parts[i]) && parts[i + 2].StartsWith("(has", StringComparison.Ordinal);
            if (qty)
            {
                words.Add(new RichWord(parts[i], true));
                words.Add(new RichWord(parts[i + 1], true));
                i++;
                continue;
            }
            words.Add(new RichWord(parts[i], false));
        }
        double space = m.Width(t, " ", size);
        double lx = x;
        var line = new List<(RichWord Word, double X, double W)>();   // M5 polish: the painted line, for its good tokens
        foreach (RichWord word in words)
        {
            FontRole role = word.Bold ? FontRole.Heading : FontRole.Body;
            double ww = m.Width(t, word.Text, size, role);
            if (lx > x && lx + ww > x + w) { BlockerTokens(m, line, y, size, tokens); line.Clear(); lx = x; y += L(size); }
            d.Write(t, lx, y, word.Text, size, word.Bold ? t.Ink.Text : t.TextInk.Progress, TextAlign.Left, role);
            line.Add((word, lx, ww));
            lx += ww + space;
        }
        BlockerTokens(m, line, y, size, tokens);
        return y + L(size);
    }

    private static bool StartsWithDigit(string s) => s.Length > 0 && char.IsDigit(s[0]);

    /// <summary>
    /// A DrawList control in the state table's style (UR-6): Primary, Secondary or Disabled, and HOVER when the
    /// pointer is over it (the accent mixed into the fill, the strong border) — DrawList controls had no hover at
    /// all. Right-aligned at <paramref name="rightX"/>; returns its rect.
    /// </summary>
    private RectD Button(DrawList d, ITextMeasure m, string label, double rightX, double y, int id, ControlStyle style)
    {
        EraTheme t = T;
        MaterialTokens mt = t.Material;
        double size = style == ControlStyle.Primary ? Body : Second;
        double lockW = style == ControlStyle.Disabled ? Sp(18) : 0;
        double bw = m.Width(t, label, size, FontRole.Body) + Sp(24) + lockW, bh = Math.Max(Sp(32), L(size) + Sp(10));
        var r = new RectD(rightX - bw, y, bw, bh);
        bool hover = Pointer is (double px, double py) && r.Contains(px, py);
        switch (style)
        {
            case ControlStyle.Primary:
            {
                Rgba fill = Mix(mt.PanelRaised, mt.Accent, hover ? 0.42 : 0.28);
                PanelFrame.Paint(d, r, t, id, FrameKind.Button, fill, hover ? mt.BorderStrong : mt.Accent, hover ? 2.0 : 1.6);
                d.Write(t, r.CenterX, r.Y + (bh - L(size)) / 2 + Sp(1), label, size, t.Ink.Text, TextAlign.Center, FontRole.Heading);
                break;
            }
            case ControlStyle.Secondary:
            {
                Rgba fill = hover ? Mix(mt.PanelRaised, mt.Accent, 0.22) : mt.PanelRaised;
                PanelFrame.Paint(d, r, t, id, FrameKind.Button, fill, hover ? mt.BorderStrong : mt.Border, hover ? 1.4 : 0.9);
                d.Write(t, r.CenterX, r.Y + (bh - L(size)) / 2 + Sp(1), label, size, t.Ink.Text, TextAlign.Center);
                break;
            }
            default:
            {
                // Disabled: sunken, a dashed hairline edge, a lock, the label in the soft ink. Hover still answers
                // (the edge firms), because the click still explains (the queue line, the blocker under it).
                d.Rect(r, A(mt.PanelSunken, 0.6));
                Rgba edge = hover ? mt.Border : mt.Hairline;
                (double, double) dash = (Sp(4), Sp(3));
                d.Line(r.X, r.Y, r.Right, r.Y, edge, 1.0, dash);
                d.Line(r.Right, r.Y, r.Right, r.Bottom, edge, 1.0, dash);
                d.Line(r.Right, r.Bottom, r.X, r.Bottom, edge, 1.0, dash);
                d.Line(r.X, r.Bottom, r.X, r.Y, edge, 1.0, dash);
                EraMarks.State(d, t, r.X + Sp(12) + Sp(3), r.CenterY, Sp(6), MarkKind.Locked, 0, id, dim: false);
                d.Write(t, r.X + Sp(12) + lockW + (bw - Sp(24) - lockW) / 2, r.Y + (bh - L(size)) / 2 + Sp(1), label, size, t.Ink.TextSoft, TextAlign.Center);
                break;
            }
        }
        return r;
    }

    // ------------------------------------------------------------------ notices

    private double PaintNotices(DrawList d, ITextMeasure m, ActionSurfaceModel model, double x, double y, double w)
    {
        EraTheme t = T;
        double size = Second, cap = Px(TypeRole.Caption, FontRole.Caps);
        var lines = new List<string>();
        foreach (string n in model.Notices) lines.AddRange(ThemeText.Wrap(m, t, n, size, w - Sp(28)));
        double h = Sp(12) + L(cap) + Sp(2) + lines.Count * L(size) + Sp(10);
        var r = new RectD(x, y, w, h);
        PanelFrame.Paint(d, r, t, 790, FrameKind.Card, Mix(t.Material.Panel, t.Material.Accent, 0.10), t.Material.Accent, 1.0);
        d.Write(t, x + Sp(14), y + Sp(10), "THIS TURN", cap, t.TextInk.Accent, TextAlign.Left, FontRole.Caps);
        double ly = y + Sp(10) + L(cap) + Sp(2);
        foreach (string l in lines) { d.Write(t, x + Sp(14), ly, l, size, t.Ink.Text); ly += L(size); }
        return r.Bottom;
    }

    // ------------------------------------------------------------------ labour

    private double PaintLabour(DrawList d, ITextMeasure m, ActionSurfaceModel model, LabourBlock l, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = l.Control;
        string title = flat ? "Our people's work at " + l.Name : "Work - " + l.Name;
        double arrowW = Sp(28);
        double arrows = l.Controlled.Length > 1 ? 2 * arrowW + Sp(8) : 0;
        double top = y;
        y = Heading(d, m, title, x, y, w - arrows, flat, 800);
        if (arrows > 0)
        {
            int at = 0;
            for (int i = 0; i < l.Controlled.Length; i++) if (l.Controlled[i].Value == l.Settlement.Value) at = i;
            int prev = l.Controlled[(at - 1 + l.Controlled.Length) % l.Controlled.Length].Value;
            int next = l.Controlled[(at + 1) % l.Controlled.Length].Value;
            double ah = Sp(26);
            var pr = new RectD(x + w - 2 * arrowW - Sp(4), top, arrowW, ah);
            var nr = new RectD(x + w - arrowW, top, arrowW, ah);
            foreach ((RectD r, int id, string glyph) in new[] { (pr, 801, "«"), (nr, 802, "»") })
            {
                bool hover = Pointer is (double px, double py) && r.Contains(px, py);
                PanelFrame.Paint(d, r, t, id, FrameKind.Button, hover ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : null,
                    hover ? t.Material.BorderStrong : null);
                d.Write(t, r.CenterX, r.Y + (ah - L(Body)) / 2, glyph, Body, t.Ink.Text, TextAlign.Center);
            }
            _hits.Add(new ActionHit(pr, ActionHitKind.PrevSettlement, prev, 0));
            _hits.Add(new ActionHit(nr, ActionHitKind.NextSettlement, next, 0));
        }
        if (l.Note is { } note) y = Wrapped(d, m, note, x, y, w, Second, t.Ink.TextSoft) + Sp(2);

        double cw = Math.Min(spec.IsSlider ? Sp(196) : Sp(190), w * 0.5);
        double labelW = w - cw - Sp(10);
        int k = 0;
        foreach (LabourEntry e in l.Entries)
        {
            double rowH = Math.Max(L(BodyBold), Sp(22));
            // The activity's name keeps every identity it expresses ("Gathering wood & stone · Logging"): when it
            // does not fit beside the control it takes its own line and the control sits under it.
            Info(new RectD(x, y, Math.Min(labelW, m.Width(t, e.Label, BodyBold, FontRole.Heading)), L(BodyBold)), InfoSubject.Sector(e.Sector, l.Settlement.Value), e.Label);
            if (m.Width(t, e.Label, BodyBold, FontRole.Heading) > labelW)
                y = Wrapped(d, m, e.Label, x, y, w, BodyBold, t.Ink.Text, FontRole.Heading);
            else
                d.Write(t, x, y, e.Label, BodyBold, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            PaintLabourControl(d, m, spec, e.Sector, new RectD(x + w - cw, y, cw, rowH), 810 + 10 * k);
            y += rowH + Sp(1);
            string makes = "makes " + e.Produces;
            // M5 polish (§10): the research a labour activity came from is named in every era ("from …" at A1).
            if (e.LearnedFrom is { } lf) makes += flat ? "  -  from " + lf : "  -  learned: " + lf;
            y = WrappedInfo(d, m, makes, x + Sp(8), y, w - Sp(8), Second, t.Ink.TextSoft, FontRole.Body, GoodTokens());
            if (e.NewMarker is { } marker)
            {
                d.Write(t, x + Sp(8), y, ThemeText.Fit(m, t, marker, Second, w - Sp(8)), Second, t.TextInk.Accent, TextAlign.Left, FontRole.Heading);
                y += L(Second);
            }
            y += Math.Max(Sp(5), t.Density.Gap * Scale * 0.55);
            k++;
        }

        // The Empire across all its settlements.
        y = Wrapped(d, m, SummaryLine(l), x, y, w, Second, t.Ink.TextSoft) + Sp(6);

        if (LabourChanged)
        {
            RectD apply = Button(d, m, spec.Numerals ? "Apply the allocation" : "Set them to work", x + w, y, 870, ControlStyle.Primary);
            RectD reset = Button(d, m, "Undo", apply.X - Sp(8), y, 871, ControlStyle.Secondary);
            _hits.Add(new ActionHit(apply, ActionHitKind.LabourApply, 0, 0));
            _hits.Add(new ActionHit(reset, ActionHitKind.LabourReset, 0, 0));
            y = apply.Bottom + Sp(4);
            y = Wrapped(d, m, "Takes effect at End Turn.", x, y, w, Caption, t.Ink.TextSoft);
        }
        else if (l.HasQueued)
        {
            y = Wrapped(d, m, "Set: the new split takes effect at End Turn.", x, y, w, Second, t.TextInk.Active, FontRole.Heading);
        }
        return y;
    }

    /// <summary>The Empire summary: numeral-free in the pebble and counter eras ("most work goes to …"),
    /// shares with numerals from the Standard eras.</summary>
    public static string SummaryLine(LabourBlock l)
    {
        LabourSummary s = l.Summary;
        string head = s.Settlements == 1 ? "Our one settlement" : "All " + s.Settlements.ToString(CultureInfo.InvariantCulture) + " of our settlements";
        int[] order = ActionSurface.RankedSectors(s.Shares);
        if (!l.Control.Numerals)
            return head + ": most work goes to " + s.Labels[order[0]] + ", then " + s.Labels[order[1]] + ".";
        var parts = new List<string>(order.Length);
        foreach (int sector in order) parts.Add(s.Labels[sector] + " " + P(s.Shares[sector]));
        return head + " (" + N0(s.Adults) + " adults): " + string.Join(" · ", parts);
    }

    private void PaintLabourControl(DrawList d, ITextMeasure m, LabourControlSpec spec, int sector, RectD r, int id)
    {
        EraTheme t = T;
        Rgba ink = SectorInk(sector);
        int value = _draft[sector];
        switch (spec.Kind)
        {
            case LabourControlKind.Pebbles:
            case LabourControlKind.Counters:
            {
                double slot = r.W / spec.Units;
                double rad = Math.Min(slot * 0.36, r.H * 0.38);
                for (int i = 0; i < spec.Units; i++)
                {
                    var hit = new RectD(r.X + i * slot, r.Y - 2, slot, r.H + 4);
                    double cx = hit.CenterX, cy = r.CenterY;
                    bool on = i < value;
                    // UR-6 hover: the slot under the pointer is ringed in the strong border — where a click would set.
                    if (Pointer is (double px, double py) && hit.Contains(px, py))
                        d.Circle(cx, cy, rad + Sp(2.5), null, t.Material.BorderStrong, 1.4);
                    if (spec.Kind == LabourControlKind.Pebbles)
                    {
                        // A laid stone: irregular, seeded from the slot (FrameNoise), never from time.
                        double jr = rad * (0.86 + 0.22 * FrameNoise.U(id, 31, i)), jx = FrameNoise.S(id, 32, i) * 1.2, jy = FrameNoise.S(id, 33, i) * 1.2;
                        if (on) d.Circle(cx + jx, cy + jy, jr, ink, A(t.Ink.Text, 0.55), 1.0);
                        else d.Circle(cx + jx, cy + jy, jr * 0.8, null, A(t.Ink.TextSoft, 0.7), 1.0);
                    }
                    else
                    {
                        // A pressed clay counter: a disc with a pressed centre.
                        if (on) { d.Circle(cx, cy, rad, ink, A(t.Ink.Text, 0.45), 0.9); d.Circle(cx, cy, rad * 0.35, A(t.Ink.Text, 0.35)); }
                        else d.Circle(cx, cy, rad * 0.85, null, A(t.Ink.TextSoft, 0.7), 0.9);
                    }
                    _hits.Add(new ActionHit(hit, ActionHitKind.LabourSlot, sector, i));
                }
                break;
            }
            case LabourControlKind.Notches:
            {
                double numW = m.Width(t, "100%", Data, FontRole.Numeric) + Sp(6);
                double segW = (r.W - numW) / spec.Units;
                for (int i = 0; i < spec.Units; i++)
                {
                    var seg = new RectD(r.X + i * segW + 0.6, r.Y + r.H * 0.2, Math.Max(1, segW - 1.2), r.H * 0.6);
                    var hit = new RectD(r.X + i * segW, r.Y - 2, segW, r.H + 4);
                    bool hover = Pointer is (double px, double py) && hit.Contains(px, py);
                    d.Rect(seg, i < value ? ink : A(t.Material.PanelSunken, 0.7), hover ? t.Material.BorderStrong : A(t.Material.Border, 0.5), hover ? 1.2 : 0.5);
                    _hits.Add(new ActionHit(hit, ActionHitKind.LabourSlot, sector, i));
                }
                d.Write(t, r.Right, r.Y + (r.H - L(Data)) / 2, (value * spec.PercentPerUnit).ToString(CultureInfo.InvariantCulture) + "%", Data, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
                break;
            }
            default:
            {
                double numW = m.Width(t, "100%", Data, FontRole.Numeric) + Sp(6), btn = Sp(18);
                var minus = new RectD(r.X, r.Y + (r.H - btn) / 2, btn, btn);
                var track = new RectD(minus.Right + Sp(6), r.Y + r.H / 2 - 3, Math.Max(10, r.W - numW - 2 * btn - Sp(14)), 6);
                var plus = new RectD(track.Right + Sp(6), minus.Y, btn, btn);
                d.Rect(track, A(t.Material.PanelSunken, 0.8), A(t.Material.Border, 0.6), 0.6);
                d.Rect(new RectD(track.X, track.Y, track.W * value / spec.Units, track.H), ink);
                if (spec.Kind == LabourControlKind.PreciseSlider)
                    for (int g = 1; g < 10; g++) d.Line(track.X + track.W * g / 10, track.Bottom, track.X + track.W * g / 10, track.Bottom + 2, A(t.Material.Border, 0.6), 0.5);
                double hx = track.X + track.W * value / spec.Units, gr = Math.Max(4, t.Controls.GrabPx * 0.42 * Scale);
                bool overTrack = Pointer is (double tx, double ty) && new RectD(track.X, r.Y - 2, track.W, r.H + 4).Contains(tx, ty);
                d.Circle(hx, track.CenterY, gr + (overTrack ? 1.5 : 0), overTrack ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : t.Material.PanelRaised, t.Material.BorderStrong, 1.1);
                foreach ((RectD b, int bid, string glyph) in new[] { (minus, id + 1, "-"), (plus, id + 2, "+") })
                {
                    bool hover = Pointer is (double px, double py) && b.Contains(px, py);
                    PanelFrame.Paint(d, b, t, bid, FrameKind.Button, hover ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : null, hover ? t.Material.BorderStrong : null);
                    d.Write(t, b.CenterX, b.Y + (btn - L(Second)) / 2, glyph, Second, t.Ink.Text, TextAlign.Center);
                }
                d.Write(t, r.Right, r.Y + (r.H - L(Data)) / 2, value.ToString(CultureInfo.InvariantCulture) + "%", Data, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
                // The hit rect spans exactly the track's width (taller, for an easy grip), so the value a click
                // sets is the value under the pointer: TrackValue maps the rect's x-range onto 0..units.
                _hits.Add(new ActionHit(new RectD(track.X, r.Y - 2, track.W, r.H + 4), ActionHitKind.LabourTrack, sector, 0));
                _hits.Add(new ActionHit(minus, ActionHitKind.LabourMinus, sector, 0));
                _hits.Add(new ActionHit(plus, ActionHitKind.LabourPlus, sector, 0));
                break;
            }
        }
    }

    // ------------------------------------------------------------------ research

    private double PaintResearch(DrawList d, ITextMeasure m, ActionSurfaceModel model, ResearchBlock r, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        string learning = r.Idle ? "Learning - idle" : "Learning";
        Info(HeadingRect(m, learning, x, y, w * 0.5, flat), InfoSubject.Research, "Learning");
        y = Heading(d, m, learning, x, y, w, flat, 830);
        // The trees link sits at the right of the block's first line (the trees are where a subject is chosen).
        RectD open = Button(d, m, r.Idle ? "Choose [K]" : "Open the trees [K]", x + w, y, 831, r.Idle ? ControlStyle.Primary : ControlStyle.Secondary);
        _hits.Add(new ActionHit(open, ActionHitKind.ResearchOpen, 0, 0));
        Info(open, InfoSubject.Research, "Learning");
        double first = w - open.W - Sp(10);
        if (r.Idle)
        {
            string idle = r.ClearQueued ? "stopping at End Turn: research will be idle" : "Nothing is being learned - choose what to learn.";
            y = Math.Max(Wrapped(d, m, idle, x, y + Sp(3), first, Body, t.TextInk.Progress, FontRole.Heading), open.Bottom + Sp(4));
            y = Wrapped(d, m, (model.Control.Numerals ? N1(r.PointsPerTurn) + " research points a turn go unused; " : "what our people learn each turn goes unused; ")
                + r.Available.ToString(CultureInfo.InvariantCulture) + " subjects are open to us.", x, y, w, Second, t.Ink.TextSoft);
        }
        else
        {
            ResearchItem item = r.Effective!;
            bool chosenNow = r.Chosen is not null;
            Info(new RectD(x, y + Sp(3), Math.Min(first, m.Width(t, item.Name, Body, FontRole.Heading)), L(Body)), InfoSubject.Node(new ResearchNodeId((int)item.Key)), item.Name);
            y = Math.Max(Wrapped(d, m, item.Name, x, y + Sp(3), first, Body, t.TextInk.Active, FontRole.Heading), open.Bottom + Sp(4));
            double frac = item.Cost > 0 ? item.Progress / item.Cost : 0;
            EraMarks.Progress(d, t, new RectD(x, y, Math.Min(Sp(240), w * 0.6), Sp(10)), frac, t.Semantic.Active, 832);
            y += Sp(16);
            string line = N0(item.Progress) + " of " + N0(item.Cost) + " research points, +" + N1(r.PointsPerTurn) + " a turn";
            if (chosenNow) line = "chosen this turn - research starts at End Turn (" + line + ")";
            y = Wrapped(d, m, line, x, y, w, Second, chosenNow ? t.TextInk.Active : t.Ink.TextSoft);
            if (r.CanClear)
            {
                RectD stop = Button(d, m, "Stop", x + w, y + Sp(4), 833, ControlStyle.Secondary);
                _hits.Add(new ActionHit(stop, ActionHitKind.ResearchClear, 0, 0));
                Info(stop, InfoSubject.Research, "Learning");
                d.Write(t, x, y + Sp(4) + (stop.H - L(Caption)) / 2, ThemeText.Fit(m, t, "progress is never lost when you stop or switch", Caption, w - stop.W - Sp(10)), Caption, t.Ink.TextSoft);
                y = stop.Bottom + Sp(4);
            }
        }
        if (!flat && !r.Retained.IsDefaultOrEmpty)
        {
            var parts = new List<string>();
            foreach (ResearchItem ri in r.Retained) parts.Add(ri.Name + " " + N0(ri.Progress) + "/" + N0(ri.Cost));
            y = Wrapped(d, m, "retained progress: " + string.Join(" · ", parts), x, y + Sp(2), w, Caption, t.Ink.TextSoft);
        }
        return y;
    }

    // ------------------------------------------------------------------ Age

    private double PaintAge(DrawList d, ITextMeasure m, AgeBlock a, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "A new Age" : "The Age", x, y, w, flat, 840);
        RectD go = Button(d, m, "Advance...", x + w, y, 841, ControlStyle.Primary);
        _hits.Add(new ActionHit(go, ActionHitKind.AgeAdvance, 0, 0));
        Info(go, InfoSubject.OfAge(a.NextAge), a.NextName);
        Info(new RectD(x, y + Sp(3), w - go.W - Sp(10), L(Body)), InfoSubject.OfAge(a.NextAge), a.NextName);
        y = Wrapped(d, m, "Our people are ready to enter the " + a.NextName + ".", x, y + Sp(3), w - go.W - Sp(10), Body, t.TextInk.Accent, FontRole.Heading);
        y = Math.Max(y, go.Bottom + Sp(4));
        return Wrapped(d, m, "Optional: choose a surge emphasis; the next turn begins in the new Age.", x, y, w, Second, t.Ink.TextSoft);
    }

    // ------------------------------------------------------------------ construction

    private double PaintConstruction(DrawList d, ITextMeasure m, ActionSurfaceModel model, ConstructionBlock c, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Building at " + c.Name : "Building - " + c.Name, x, y, w, flat, 850);
        if (c.Capacity is { } cap)
        {
            string line = !model.Control.Numerals
                ? "Estimate for the coming turn: our builders can give about " + N0(cap.Available) + " adult-years to projects and paths."
                : "Builders, estimated for the coming turn: " + N1(cap.Pool) + " adult-years; housing's last draw " + N1(cap.Housing)
                  + ", about " + N1(cap.Available) + " left for a project and for path-making.";
            y = Wrapped(d, m, line, x, y, w, Second, t.Ink.TextSoft) + Sp(4);
            if (model.Layout == SurfaceLayout.Detailed)
                y = Wrapped(d, m, "An estimate from the current state: the turn re-reads labour, housing's draw and its own length when it resolves.",
                    x, y, w, Caption, t.Ink.TextSoft) + Sp(2);
            if (model.Layout == SurfaceLayout.Detailed)
                y = Wrapped(d, m, "Path-making also banks the labour a finished project used (ADR-033 D10, open).", x, y, w, Caption, t.Ink.TextSoft) + Sp(2);
        }
        int k = 0;
        foreach (ProjectEntry p in c.Projects)
        {
            // UR-6: a project its settlement cannot build yet LOOKS unavailable (sunken, dashed, a lock). The click still
            // queues it — materials are affordability, not legality (ADR-033 D3: the queue head waits until it can be
            // built) — and the queue line under the block says so.
            RectD build = Button(d, m, "Build", x + w, y, 852 + k, p.Blocker is null ? ControlStyle.Primary : ControlStyle.Disabled);
            _hits.Add(new ActionHit(build, ActionHitKind.Build, c.Settlement.Value, p.ProjectId));
            Info(build, InfoSubject.OfProject(p.ProjectId, c.Settlement.Value), p.Name);
            double tw = w - build.W - Sp(10);
            string head = p.Name + (p.Built > 0 ? "  (" + p.Built.ToString(CultureInfo.InvariantCulture) + " built)" : "");
            double hy = y + (build.H - L(BodyBold)) / 2;
            Info(new RectD(x, hy, Math.Min(tw, m.Width(t, p.Name, BodyBold, FontRole.Heading)), L(BodyBold)), InfoSubject.OfProject(p.ProjectId, c.Settlement.Value), p.Name);
            d.Write(t, x, hy, ThemeText.Fit(m, t, head, BodyBold, tw, FontRole.Heading), BodyBold, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            y = build.Bottom + Sp(3);
            if (p.Blocker is { } blocker)
                y = Blocker(d, m, "not yet:", blocker, x + Sp(8), y, w - Sp(8), Second, GoodTokens());
            else
                y = WrappedInfo(d, m, "can be built: " + p.Materials + ", " + N1(p.LabourAdultYears) + " adult-years", x + Sp(8), y, w - Sp(8), Second, t.TextInk.Positive, FontRole.Body, GoodTokens());
            // M5 polish: where a missing material comes from, and what a built one does in this build.
            if (p.Chain is { } chain) y = WrappedInfo(d, m, "comes from: " + chain, x + Sp(8), y, w - Sp(8), Second, t.TextInk.Progress, FontRole.Body, GoodTokens());
            if (p.Effects is { } effects) y = Wrapped(d, m, "does: " + effects, x + Sp(8), y, w - Sp(8), Second, t.Ink.TextSoft);
            if (!flat)
                y = Wrapped(d, m, "takes " + p.Materials + " and " + N1(p.LabourAdultYears) + " adult-years"
                    + (p.Queued > 0 ? " · " + p.Queued.ToString(CultureInfo.InvariantCulture) + " queued" : "")
                    + (p.LearnedFrom is { } lf ? " · learned: " + lf : ""), x + Sp(8), y, w - Sp(8), Caption, t.Ink.TextSoft);
            y += Math.Max(Sp(6), t.Density.Gap * Scale * 0.5);
            k++;
        }
        if (!c.Queue.IsDefaultOrEmpty)
        {
            var names = new List<string>();
            foreach (QueueEntry q in c.Queue) names.Add(q.Name);
            y = Wrapped(d, m, "Queue: " + string.Join(", then ", names), x, y, w, Body, t.Ink.Text, FontRole.Heading);
            y = c.HeadBlocker is { } hb
                ? Blocker(d, m, "first in line waits -", hb, x + Sp(8), y, w - Sp(8), Second)
                : Wrapped(d, m, "first in line can be built at End Turn", x + Sp(8), y, w - Sp(8), Second, t.TextInk.Positive);
        }
        if (c.OrderedThisTurn > 0)
            y = Wrapped(d, m, c.OrderedThisTurn.ToString(CultureInfo.InvariantCulture) + " ordered this turn - joins the queue at End Turn.", x, y, w, Second, t.TextInk.Active);
        return y;
    }

    // ------------------------------------------------------------------ roads (read only; transport is frozen)

    private double PaintRoads(DrawList d, ITextMeasure m, ActionSurfaceModel model, RoadsBlock rb, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = model.Control;
        Info(HeadingRect(m, "Roads - " + rb.ClassName, x, y, w, flat), InfoSubject.OfRoadClass(rb.TargetClass), rb.ClassName);
        y = Heading(d, m, "Roads - " + rb.ClassName, x, y, w, flat, 870 + 30);
        if (rb.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x, y, w, Caption, t.Ink.TextSoft);

        if (rb.OrderedPercent is { } ordered)
        {
            y = Wrapped(d, m, "Ordered this turn: develop " + N0(ordered) + "% of the demand - applied at End Turn.", x, y, w, Second, t.TextInk.Active, FontRole.Heading);
        }
        else
        {
            double labelW = m.Width(t, "Modernize", BodyBold, FontRole.Heading) + Sp(10);
            double rowH = Math.Max(L(BodyBold), Sp(22));
            RectD go = Button(d, m, "Develop", x + w, y, 906, ControlStyle.Primary);
            double ry = y + (go.H - rowH) / 2;
            d.Write(t, x, ry, "Modernize", BodyBold, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            var r = new RectD(x + labelW, ry, Math.Max(Sp(60), Math.Min(Sp(200), w - labelW - go.W - Sp(14))), rowH);
            PaintShareControl(d, m, spec, r, _roadDraft, t.Semantic.Infrastructure, ActionHitKind.RoadSlot, ActionHitKind.RoadTrack, 905, minUnits: 1);
            _hits.Add(new ActionHit(go, ActionHitKind.RoadApply, 0, 0));
            Info(go, InfoSubject.OfRoadClass(rb.TargetClass), rb.ClassName);
            y = Math.Max(r.Bottom, go.Bottom) + Sp(4);
            y = Wrapped(d, m, (spec.Numerals ? N0(_roadDraft) + "% of " : "a share of ") + "the demand on our busiest routes, heaviest-used first.", x, y, w, Second, t.Ink.TextSoft);
            if (_world is not null && _cfg is not null)
            {
                _roadPreview ??= ActionSurface.RoadPlan(_world, _cfg, _player, _roadDraft, _name);
                RoadPlanPreview p = _roadPreview;
                string payers = p.Payers.Length <= 3 ? string.Join(", ", p.Payers)
                    : p.Payers[0] + ", " + p.Payers[1] + ", " + p.Payers[2] + " and " + (p.Payers.Length - 3).ToString(CultureInfo.InvariantCulture) + " more";
                string line = p.Routes.ToString(CultureInfo.InvariantCulture) + " route(s): " + string.Join(", ", p.RouteLabels)
                    + ". Estimated cost on current stocks: " + p.Cost
                    + (p.Affordable >= 1.0 ? " (affordable)" : " (we can pay " + P(p.Affordable) + " of it now)")
                    + ". Our settlements nearest the route pay first: " + payers + ".";
                y = Wrapped(d, m, line, x, y, w, Second, t.Ink.Text);
                y = Wrapped(d, m, "An estimate: production uses tools before roads are built in the same turn.", x, y, w, Caption, t.Ink.TextSoft);
            }
        }
        if (rb.Blocker is { } blocker) y = Blocker(d, m, "not yet:", blocker, x, y, w, Second);
        int shown = Math.Min(model.Layout == SurfaceLayout.Detailed ? 8 : 4, rb.Routes.Length);
        for (int i = 0; i < shown; i++)
        {
            RouteEntry re = rb.Routes[i];
            string line = re.Label + ": " + N0(re.LengthKm) + " km, used " + N0(re.Usage) + ", " + N0(re.ModernizedPercent) + "% modernized, speed x"
                + (1.0 / Math.Max(1e-9, re.CostFactor)).ToString("0.00", CultureInfo.InvariantCulture) + (model.Layout == SurfaceLayout.Detailed ? ", " + N0(re.CapacityTonnesPerYear) + " t/yr" : "")
                + "; completing it needs " + re.Cost;
            y = Wrapped(d, m, line, x + Sp(8), y, w - Sp(8), Caption, t.Ink.TextSoft);
        }
        if (shown < rb.Routes.Length)
            y = Wrapped(d, m, "... and " + (rb.Routes.Length - shown).ToString(CultureInfo.InvariantCulture) + " more eligible route(s).", x + Sp(8), y, w - Sp(8), Caption, t.Ink.TextSoft);
        return y;
    }

    /// <summary>A single 0..100 share in the era's control (the tax levy, the road percentage).</summary>
    private void PaintShareControl(DrawList d, ITextMeasure m, LabourControlSpec spec, RectD r, int percent, Rgba ink, ActionHitKind slot, ActionHitKind track, int id, int minUnits = 0)
    {
        EraTheme t = T;
        double numW = m.Width(t, "100%", Data, FontRole.Numeric) + Sp(6);
        if (!spec.IsSlider)
        {
            int units = spec.Units, value = (int)Math.Round(percent / (double)spec.PercentPerUnit, MidpointRounding.AwayFromZero);
            double nw = spec.Numerals ? numW : 0;
            double s = (r.W - nw) / units, rad = Math.Min(s * 0.36, r.H * 0.38);
            for (int i = 0; i < units; i++)
            {
                var hit = new RectD(r.X + i * s, r.Y - 2, s, r.H + 4);
                bool on = i < value;
                bool hover = Pointer is (double px, double py) && hit.Contains(px, py);
                if (spec.Kind == LabourControlKind.Notches)
                    d.Rect(new RectD(hit.X + 0.6, r.Y + r.H * 0.2, Math.Max(1, s - 1.2), r.H * 0.6), on ? ink : A(t.Material.PanelSunken, 0.7),
                        hover ? t.Material.BorderStrong : A(t.Material.Border, 0.5), hover ? 1.2 : 0.5);
                else
                {
                    if (hover) d.Circle(hit.CenterX, r.CenterY, rad + Sp(2.5), null, t.Material.BorderStrong, 1.4);
                    if (on) d.Circle(hit.CenterX, r.CenterY, rad, ink, A(t.Ink.Text, 0.5), 0.9);
                    else d.Circle(hit.CenterX, r.CenterY, rad * 0.8, null, A(t.Ink.TextSoft, 0.7), 0.9);
                }
                _hits.Add(new ActionHit(hit, slot, 0, i));
            }
            if (spec.Numerals) d.Write(t, r.Right, r.Y + (r.H - L(Data)) / 2, percent.ToString(CultureInfo.InvariantCulture) + "%", Data, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
            return;
        }
        var tr = new RectD(r.X, r.CenterY - 3, Math.Max(10, r.W - numW), 6);
        d.Rect(tr, A(t.Material.PanelSunken, 0.8), A(t.Material.Border, 0.6), 0.6);
        d.Rect(new RectD(tr.X, tr.Y, tr.W * percent / 100.0, tr.H), ink);
        bool overTrack = Pointer is (double tx, double ty) && new RectD(tr.X, r.Y - 2, tr.W, r.H + 4).Contains(tx, ty);
        d.Circle(tr.X + tr.W * percent / 100.0, tr.CenterY, Math.Max(4, t.Controls.GrabPx * 0.42 * Scale) + (overTrack ? 1.5 : 0),
            overTrack ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.22) : t.Material.PanelRaised, t.Material.BorderStrong, 1.1);
        d.Write(t, r.Right, r.Y + (r.H - L(Data)) / 2, percent.ToString(CultureInfo.InvariantCulture) + "%", Data, t.Ink.Text, TextAlign.Right, FontRole.Numeric);
        _hits.Add(new ActionHit(new RectD(tr.X, r.Y - 2, tr.W, r.H + 4), track, 0, 0));
        _ = minUnits;
        _ = id;
    }

    // ------------------------------------------------------------------ governance

    private double PaintGovernance(DrawList d, ITextMeasure m, ActionSurfaceModel model, GovernanceBlock g, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = model.Control;
        Info(HeadingRect(m, flat ? "The tax edict" : "Governance - the tax edict", x, y, w, flat), InfoSubject.Tax, "Tax edict");
        y = Heading(d, m, flat ? "The tax edict" : "Governance - the tax edict", x, y, w, flat, 920);
        if (g.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x, y, w, Caption, t.Ink.TextSoft);
        string declared = !g.HasPolicy ? "No levy declared yet."
            : spec.Numerals ? "Declared levy: " + g.DeclaredPercent.ToString(CultureInfo.InvariantCulture) + "%."
            : "A levy is declared.";
        y = Wrapped(d, m, declared, x, y, w, Body, t.Ink.Text, FontRole.Heading);
        int baseline = g.QueuedPercent ?? g.DeclaredPercent;
        bool declare = _taxDraft >= 0 && (_taxDraft != baseline || !g.HasPolicy && g.QueuedPercent is null);
        double labelW = m.Width(t, "Levy", BodyBold, FontRole.Heading) + Sp(12);
        double rowH = Math.Max(L(BodyBold), Sp(22));
        double bottom;
        RectD? go = declare ? Button(d, m, "Declare", x + w, y, 922, ControlStyle.Primary) : null;
        double goW = go is RectD gr ? gr.W + Sp(14) : Sp(96);
        double ry = go is RectD g0 ? y + (g0.H - rowH) / 2 : y;
        d.Write(t, x, ry, "Levy", BodyBold, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        var r = new RectD(x + labelW, ry, Math.Max(Sp(60), Math.Min(Sp(210), w - labelW - goW)), rowH);
        PaintShareControl(d, m, spec, r, Math.Max(0, _taxDraft), t.Semantic.Danger, ActionHitKind.TaxSlot, ActionHitKind.TaxTrack, 921);
        bottom = r.Bottom;
        if (go is RectD gb)
        {
            _hits.Add(new ActionHit(gb, ActionHitKind.TaxApply, 0, 0));
            Info(gb, InfoSubject.Tax, "Tax edict");
            bottom = Math.Max(bottom, gb.Bottom);
        }
        y = bottom + Sp(4);
        if (g.QueuedPercent is { } q)
            y = Wrapped(d, m, "Declared this turn: " + q.ToString(CultureInfo.InvariantCulture) + "% - in force from the next turn, felt the turn after.", x, y, w, Second, t.TextInk.Active);
        y = Wrapped(d, m, "A levy raises what our people produce and lowers their happiness; it reaches only as far as our administration does.", x, y, w, Second, t.Ink.TextSoft);
        y = Wrapped(d, m, g.Legitimacy, x, y, w, Data, t.Ink.Text, FontRole.Numeric);
        int shown = model.Layout == SurfaceLayout.Detailed ? g.Collection.Length : Math.Min(4, g.Collection.Length);
        for (int i = 0; i < shown; i++) y = Wrapped(d, m, g.Collection[i], x + Sp(8), y, w - Sp(8), Px(TypeRole.Caption, FontRole.Numeric), t.Ink.TextSoft, FontRole.Numeric);
        if (shown < g.Collection.Length)
            y = Wrapped(d, m, "... " + (g.Collection.Length - shown).ToString(CultureInfo.InvariantCulture) + " more settlement(s).", x + Sp(8), y, w - Sp(8), Caption, t.Ink.TextSoft);
        return y;
    }

    // ------------------------------------------------------------------ military (information only)

    private double PaintMilitary(DrawList d, ITextMeasure m, ActionSurfaceModel model, MilitaryBlock mb, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Basic fighting" : "Arms - basic fighting", x, y, w, flat, 940);
        int k = 0;
        double ind = Sp(22);
        foreach (FormationEntry f in mb.Formations)
        {
            EraMarks.State(d, t, x + Sp(8), y + L(Body) / 2, Sp(5.5), MarkKind.Completed, 1, 941 + k, dim: true);
            Info(new RectD(x + ind, y, Math.Min(w - ind, m.Width(t, f.Identity, Body, FontRole.Heading)), L(Body)), InfoSubject.OfFormation((int)f.Id), f.Identity);
            y = Wrapped(d, m, f.Identity + " at " + f.Where, x + ind, y, w - ind, Body, t.TextInk.Military, FontRole.Heading);
            // The family line and the formation's CURRENT Age identity (ADR-031: it modernizes with the Age).
            string form = f.Family + (f.IdentityAge > 0 ? " - its Age " + AgePanelModel.Numeral(f.IdentityAge) + " form" : "");
            y = Wrapped(d, m, form, x + ind, y, w - ind, Second, t.Ink.TextSoft);
            if (!flat && f.Line.Length > 0) y = Wrapped(d, m, "line: " + f.Line, x + ind, y, w - ind, Caption, t.Ink.TextSoft);
            k++;
        }
        if (mb.NextAgeName is { } next && !mb.Modernization.IsDefaultOrEmpty)
            y = Wrapped(d, m, "At the next Age (" + next + "): " + string.Join("; ", mb.Modernization), x, y + Sp(2), w, Second, t.Ink.TextSoft);
        return Wrapped(d, m, mb.Note, x, y, w, Caption, t.Ink.TextSoft);
    }

    // ------------------------------------------------------------------ production (R1)

    private double PaintProduction(DrawList d, ITextMeasure m, ProductionBlock p, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Crafts we know" : "Production - crafts we know", x, y, w, flat, 930);
        int k = 0;
        double ind = Sp(22);
        foreach (ProductionEntry e in p.Entries)
        {
            EraMarks.State(d, t, x + Sp(8), y + L(Body) / 2, Sp(5.5), e.Settlements > 0 ? MarkKind.Completed : MarkKind.Available, 1, 931 + k, dim: e.Settlements == 0);
            Info(new RectD(x + ind, y, Math.Min(w - ind, m.Width(t, e.Name, Body, FontRole.Heading)), L(Body)), InfoSubject.OfRecipe(RecipeOf(e)), e.Name);
            y = Wrapped(d, m, e.Name, x + ind, y, w - ind, Body, t.Ink.Text, FontRole.Heading);
            // M5 polish: a condition is never shown without its live value and a plain reading (InfoQuery.ConditionReading).
            string where = e.Settlements > 0
                ? "made in " + e.Settlements.ToString(CultureInfo.InvariantCulture) + " settlement(s)"
                : e.Condition is { } cond ? "runs where " + cond + "; in no settlement yet" : e.Blocker ?? "made nowhere yet";
            y = WrappedInfo(d, m, e.Detail + " · " + where, x + ind, y, w - ind, Second, t.Ink.TextSoft, FontRole.Body, GoodTokens());
            if (e.Missing is { } missing) y = WrappedInfo(d, m, "cannot produce: " + missing, x + ind, y, w - ind, Second, t.TextInk.Progress, FontRole.Body, GoodTokens());
            if (e.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x + ind, y, w - ind, Caption, t.Ink.TextSoft);
            k++;
        }
        return y;
    }

    // ------------------------------------------------------------------ standing

    private double PaintStanding(DrawList d, ITextMeasure m, ActionSurfaceModel model, StandingBlock s, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Without our orders" : "On their own", x, y, w, flat, 960);
        _ = model;
        return WrappedInfo(d, m, string.Join(" · ", s.Items), x, y, w, Second, t.Ink.TextSoft, FontRole.Body, StandingTokens(s));
    }
}
