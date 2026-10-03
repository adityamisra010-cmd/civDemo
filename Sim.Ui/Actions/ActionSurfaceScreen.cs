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
public sealed class ActionSurfaceScreen
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

    private EraTheme T => Theme;
    private double L(double size) => T.Type.Line(size);
    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);
    private static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string N1(double v) => v.ToString("#,0.#", CultureInfo.InvariantCulture);
    private static string P(double fraction) => (fraction * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>The identity ink of a labour sector — the map's sector arcs, constant in every era.</summary>
    public static Rgba SectorInk(int sector) => MapInk.Default.Sectors[Math.Clamp(sector, 0, MapInk.Default.Sectors.Count - 1)];

    /// <summary>
    /// Paints the surface from (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="width"/> and returns
    /// the height it used; records the hit regions <see cref="Click"/> answers.
    /// </summary>
    public double Paint(DrawList d, ITextMeasure m, double x, double y, double width)
    {
        _hits = [];
        if (Model is not { } model) return 0;
        EraTheme t = T;
        double y0 = y;
        bool flat = model.Layout == SurfaceLayout.Flat;
        double gap = Math.Max(8, t.Density.Gap);

        if (!model.Notices.IsDefaultOrEmpty) y = PaintNotices(d, m, model, x, y, width) + gap;
        if (model.Labour is { } labour) y = PaintLabour(d, m, model, labour, x, y, width, flat) + gap;
        if (model.Research is { } research) y = PaintResearch(d, m, model, research, x, y, width, flat) + gap;
        if (model.Age is { } age) y = PaintAge(d, m, age, x, y, width, flat) + gap;
        if (model.Construction is { } construction) y = PaintConstruction(d, m, model, construction, x, y, width, flat) + gap;
        if (model.Roads is { } roads) y = PaintRoads(d, m, model, roads, x, y, width, flat) + gap;
        if (model.Governance is { } gov) y = PaintGovernance(d, m, model, gov, x, y, width, flat) + gap;
        if (model.Production is { } production) y = PaintProduction(d, m, production, x, y, width, flat) + gap;
        if (model.Military is { } military) y = PaintMilitary(d, m, model, military, x, y, width, flat) + gap;
        if (model.Standing is { } standing) y = PaintStanding(d, m, model, standing, x, y, width, flat);
        return y - y0;
    }

    /// <summary>A block's heading: at the flat density a plain lead-in line, at the grouped densities a
    /// capitalised header in the era's accent with the era's rule under it.</summary>
    private double Heading(DrawList d, ITextMeasure m, string text, double x, double y, double w, bool flat, int id)
    {
        EraTheme t = T;
        if (flat)
        {
            d.Write(t, x, y, ThemeText.Fit(m, t, text, 15, w, FontRole.Heading), 15, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            return y + L(15) + 2;
        }
        d.Write(t, x, y, ThemeText.Fit(m, t, text, 12, w, FontRole.Caps), 12, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        double ry = y + L(12) + 2;
        PanelFrame.Rule(d, new RectD(x, ry, w, Math.Max(2, t.Edge.BorderPx)), t, id);
        return ry + Math.Max(2, t.Edge.BorderPx) + 6;
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

    private RectD Button(DrawList d, ITextMeasure m, string label, double rightX, double y, int id, bool primary, double size = 12.5)
    {
        EraTheme t = T;
        double bw = m.Width(t, label, size, FontRole.Body) + 22, bh = L(size) + 8;
        var r = new RectD(rightX - bw, y, bw, bh);
        PanelFrame.Paint(d, r, t, id, FrameKind.Button,
            primary ? Mix(t.Material.PanelRaised, t.Material.Accent, 0.24) : t.Material.PanelRaised,
            primary ? t.Material.Accent : t.Material.Border, primary ? 1.6 : 0.9);
        d.Write(t, r.CenterX, r.Y + 4, label, size, t.Ink.Text, TextAlign.Center);
        return r;
    }

    // ------------------------------------------------------------------ notices

    private double PaintNotices(DrawList d, ITextMeasure m, ActionSurfaceModel model, double x, double y, double w)
    {
        EraTheme t = T;
        var lines = new List<string>();
        foreach (string n in model.Notices) lines.AddRange(ThemeText.Wrap(m, t, n, 12.5, w - 28));
        double h = 12 + L(11) + lines.Count * L(12.5) + 8;
        var r = new RectD(x, y, w, h);
        PanelFrame.Paint(d, r, t, 790, FrameKind.Card, Mix(t.Material.Panel, t.Material.Accent, 0.10), t.Material.Accent, 1.0);
        d.Write(t, x + 14, y + 8, "THIS TURN", 11, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        double ly = y + 8 + L(11);
        foreach (string l in lines) { d.Write(t, x + 14, ly, l, 12.5, t.Ink.Text); ly += L(12.5); }
        return r.Bottom;
    }

    // ------------------------------------------------------------------ labour

    private double PaintLabour(DrawList d, ITextMeasure m, ActionSurfaceModel model, LabourBlock l, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = l.Control;
        string title = flat ? "Our people's work at " + l.Name : "Work - " + l.Name;
        double arrows = l.Controlled.Length > 1 ? 54 : 0;
        y = Heading(d, m, title, x, y, w - arrows, flat, 800);
        if (arrows > 0)
        {
            int at = 0;
            for (int i = 0; i < l.Controlled.Length; i++) if (l.Controlled[i].Value == l.Settlement.Value) at = i;
            int prev = l.Controlled[(at - 1 + l.Controlled.Length) % l.Controlled.Length].Value;
            int next = l.Controlled[(at + 1) % l.Controlled.Length].Value;
            double ay = y - (flat ? L(15) : L(12) + 10) - 2;
            var pr = new RectD(x + w - 50, ay, 22, 20);
            var nr = new RectD(x + w - 24, ay, 22, 20);
            PanelFrame.Paint(d, pr, t, 801, FrameKind.Button);
            PanelFrame.Paint(d, nr, t, 802, FrameKind.Button);
            d.Write(t, pr.CenterX, pr.Y + 2, "«", 13, t.Ink.TextSoft, TextAlign.Center);
            d.Write(t, nr.CenterX, nr.Y + 2, "»", 13, t.Ink.TextSoft, TextAlign.Center);
            _hits.Add(new ActionHit(pr, ActionHitKind.PrevSettlement, prev, 0));
            _hits.Add(new ActionHit(nr, ActionHitKind.NextSettlement, next, 0));
        }
        if (l.Note is { } note) y = Wrapped(d, m, note, x, y, w, 11.5, t.Ink.TextDim) + 2;

        double cw = Math.Min(spec.IsSlider ? 196 : 190, w * 0.56);
        double labelW = w - cw - 10;
        int k = 0;
        foreach (LabourEntry e in l.Entries)
        {
            double rowH = Math.Max(L(14), 18);
            // The activity's name keeps every identity it expresses ("Gathering wood & stone · Logging"): when it
            // does not fit beside the control it takes its own line and the control sits under it.
            if (m.Width(t, e.Label, 14, FontRole.Heading) > labelW)
                y = Wrapped(d, m, e.Label, x, y, w, 14, t.Ink.Text, FontRole.Heading);
            else
                d.Write(t, x, y, e.Label, 14, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            PaintLabourControl(d, m, spec, e.Sector, new RectD(x + w - cw, y, cw, rowH), 810 + 10 * k);
            y += rowH + 1;
            string makes = "makes " + e.Produces;
            if (!flat && e.LearnedFrom is { } lf) makes += "  -  learned: " + lf;
            y = Wrapped(d, m, makes, x + 8, y, w - 8, 11.5, t.Ink.TextSoft);
            if (e.NewMarker is { } marker)
            {
                d.Write(t, x + 8, y, ThemeText.Fit(m, t, marker, 11.5, w - 8), 11.5, t.Material.Accent, TextAlign.Left, FontRole.Heading);
                y += L(11.5);
            }
            y += Math.Max(3, t.Density.Gap * 0.45);
            k++;
        }

        // The Empire across all its settlements.
        y = Wrapped(d, m, SummaryLine(l), x, y, w, 11.5, t.Ink.TextDim) + 4;

        if (LabourChanged)
        {
            RectD apply = Button(d, m, spec.Numerals ? "Apply the allocation" : "Set them to work", x + w, y, 870, primary: true);
            RectD reset = Button(d, m, "Undo", apply.X - 8, y, 871, primary: false);
            _hits.Add(new ActionHit(apply, ActionHitKind.LabourApply, 0, 0));
            _hits.Add(new ActionHit(reset, ActionHitKind.LabourReset, 0, 0));
            y = apply.Bottom + 2;
            y = Wrapped(d, m, "Takes effect at End Turn.", x, y, w, 11, t.Ink.TextDim);
        }
        else if (l.HasQueued)
        {
            y = Wrapped(d, m, "Set: the new split takes effect at End Turn.", x, y, w, 12, t.Semantic.Active, FontRole.Heading);
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
                    if (spec.Kind == LabourControlKind.Pebbles)
                    {
                        // A laid stone: irregular, seeded from the slot (FrameNoise), never from time.
                        double jr = rad * (0.86 + 0.22 * FrameNoise.U(id, 31, i)), jx = FrameNoise.S(id, 32, i) * 1.2, jy = FrameNoise.S(id, 33, i) * 1.2;
                        if (on) d.Circle(cx + jx, cy + jy, jr, ink, A(t.Ink.Text, 0.55), 1.0);
                        else d.Circle(cx + jx, cy + jy, jr * 0.8, null, A(t.Ink.TextDim, 0.55), 1.0);
                    }
                    else
                    {
                        // A pressed clay counter: a disc with a pressed centre.
                        if (on) { d.Circle(cx, cy, rad, ink, A(t.Ink.Text, 0.45), 0.9); d.Circle(cx, cy, rad * 0.35, A(t.Ink.Text, 0.35)); }
                        else d.Circle(cx, cy, rad * 0.85, null, A(t.Ink.TextDim, 0.55), 0.9);
                    }
                    _hits.Add(new ActionHit(hit, ActionHitKind.LabourSlot, sector, i));
                }
                break;
            }
            case LabourControlKind.Notches:
            {
                double numW = 38;
                double segW = (r.W - numW) / spec.Units;
                for (int i = 0; i < spec.Units; i++)
                {
                    var seg = new RectD(r.X + i * segW + 0.6, r.Y + r.H * 0.2, Math.Max(1, segW - 1.2), r.H * 0.6);
                    d.Rect(seg, i < value ? ink : A(t.Material.PanelSunken, 0.7), A(t.Material.Border, 0.5), 0.5);
                    _hits.Add(new ActionHit(new RectD(r.X + i * segW, r.Y - 2, segW, r.H + 4), ActionHitKind.LabourSlot, sector, i));
                }
                d.Write(t, r.Right, r.Y + 1, (value * spec.PercentPerUnit).ToString(CultureInfo.InvariantCulture) + "%", 12.5, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
                break;
            }
            default:
            {
                double numW = 44, btn = 16;
                var minus = new RectD(r.X, r.Y + (r.H - btn) / 2, btn, btn);
                var track = new RectD(minus.Right + 6, r.Y + r.H / 2 - 3, r.W - numW - 2 * btn - 14, 6);
                var plus = new RectD(track.Right + 6, minus.Y, btn, btn);
                d.Rect(track, A(t.Material.PanelSunken, 0.8), A(t.Material.Border, 0.6), 0.6);
                d.Rect(new RectD(track.X, track.Y, track.W * value / spec.Units, track.H), ink);
                if (spec.Kind == LabourControlKind.PreciseSlider)
                    for (int g = 1; g < 10; g++) d.Line(track.X + track.W * g / 10, track.Bottom, track.X + track.W * g / 10, track.Bottom + 2, A(t.Material.Border, 0.6), 0.5);
                double hx = track.X + track.W * value / spec.Units, gr = Math.Max(4, t.Controls.GrabPx * 0.42);
                d.Circle(hx, track.CenterY, gr, t.Material.PanelRaised, t.Material.BorderStrong, 1.1);
                PanelFrame.Paint(d, minus, t, id + 1, FrameKind.Button);
                PanelFrame.Paint(d, plus, t, id + 2, FrameKind.Button);
                d.Write(t, minus.CenterX, minus.Y - 1, "-", 12, t.Ink.Text, TextAlign.Center);
                d.Write(t, plus.CenterX, plus.Y - 1, "+", 12, t.Ink.Text, TextAlign.Center);
                d.Write(t, r.Right, r.Y + 1, value.ToString(CultureInfo.InvariantCulture) + "%", 12.5, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
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
        y = Heading(d, m, "Learning", x, y, w, flat, 830);
        // The trees link sits at the right of the block's first line (the trees are where a subject is chosen).
        RectD open = Button(d, m, r.Idle ? "Choose [K]" : "Open the trees [K]", x + w, y, 831, primary: r.Idle);
        _hits.Add(new ActionHit(open, ActionHitKind.ResearchOpen, 0, 0));
        double first = w - open.W - 8;
        if (r.Idle)
        {
            string idle = r.ClearQueued ? "stopping at End Turn: research will be idle" : "idle - choose what to learn";
            y = Math.Max(Wrapped(d, m, idle, x, y + 3, first, 13, t.Semantic.Progress, FontRole.Heading), open.Bottom + 2);
            y = Wrapped(d, m, (model.Control.Numerals ? N1(r.PointsPerTurn) + " research points a turn go unused; " : "what our people learn each turn goes unused; ")
                + r.Available.ToString(CultureInfo.InvariantCulture) + " subjects are open to us.", x, y, w, 11.5, t.Ink.TextSoft);
        }
        else
        {
            ResearchItem item = r.Effective!;
            bool chosenNow = r.Chosen is not null;
            y = Math.Max(Wrapped(d, m, item.Name, x, y + 3, first, 14, t.Semantic.Active, FontRole.Heading), open.Bottom + 2);
            double frac = item.Cost > 0 ? item.Progress / item.Cost : 0;
            EraMarks.Progress(d, t, new RectD(x, y, Math.Min(220, w * 0.6), 10), frac, t.Semantic.Active, 832);
            y += 14;
            string line = N0(item.Progress) + " of " + N0(item.Cost) + " research points, +" + N1(r.PointsPerTurn) + " a turn";
            if (chosenNow) line = "chosen this turn - research starts at End Turn (" + line + ")";
            y = Wrapped(d, m, line, x, y, w, 11.5, chosenNow ? t.Semantic.Active : t.Ink.TextSoft);
            if (r.CanClear)
            {
                RectD stop = Button(d, m, "Stop", x + w, y + 2, 833, primary: false, size: 11.5);
                _hits.Add(new ActionHit(stop, ActionHitKind.ResearchClear, 0, 0));
                d.Write(t, x, y + 5, ThemeText.Fit(m, t, "progress is never lost when you stop or switch", 11, w - stop.W - 10), 11, t.Ink.TextDim);
                y = stop.Bottom + 2;
            }
        }
        if (!flat && !r.Retained.IsDefaultOrEmpty)
        {
            var parts = new List<string>();
            foreach (ResearchItem ri in r.Retained) parts.Add(ri.Name + " " + N0(ri.Progress) + "/" + N0(ri.Cost));
            y = Wrapped(d, m, "retained progress: " + string.Join(" · ", parts), x, y + 2, w, 11.5, t.Ink.TextSoft);
        }
        return y;
    }

    // ------------------------------------------------------------------ Age

    private double PaintAge(DrawList d, ITextMeasure m, AgeBlock a, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "A new Age" : "The Age", x, y, w, flat, 840);
        RectD go = Button(d, m, "Advance...", x + w, y, 841, primary: true);
        _hits.Add(new ActionHit(go, ActionHitKind.AgeAdvance, 0, 0));
        y = Wrapped(d, m, "Our people are ready to enter the " + a.NextName + ".", x, y + 3, w - go.W - 10, 13, t.Material.Accent, FontRole.Heading);
        y = Math.Max(y, go.Bottom + 2);
        return Wrapped(d, m, "Optional: choose a surge emphasis; the next turn begins in the new Age.", x, y, w, 11.5, t.Ink.TextSoft);
    }

    // ------------------------------------------------------------------ construction

    private double PaintConstruction(DrawList d, ITextMeasure m, ActionSurfaceModel model, ConstructionBlock c, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Building at " + c.Name : "Building - " + c.Name, x, y, w, flat, 850);
        if (c.Capacity is { } cap)
        {
            string line = !model.Control.Numerals
                ? "This turn our builders can give about " + N0(cap.Available) + " adult-years to projects and paths."
                : "Builders this turn: " + N1(cap.Pool) + " adult-years; housing took " + N1(cap.Housing) + ", " + N1(cap.Available)
                  + " left for a project and for path-making.";
            y = Wrapped(d, m, line, x, y, w, 11.5, t.Ink.TextSoft) + 2;
            if (model.Layout == SurfaceLayout.Detailed)
                y = Wrapped(d, m, "Path-making also banks the labour a finished project used (ADR-033 D10, open).", x, y, w, 11, t.Ink.TextDim) + 2;
        }
        int k = 0;
        foreach (ProjectEntry p in c.Projects)
        {
            RectD build = Button(d, m, "Build", x + w, y, 852 + k, primary: p.Blocker is null, size: 12);
            _hits.Add(new ActionHit(build, ActionHitKind.Build, c.Settlement.Value, p.ProjectId));
            double tw = w - build.W - 8;
            string head = p.Name + (p.Built > 0 ? "  (" + p.Built.ToString(CultureInfo.InvariantCulture) + " built)" : "");
            d.Write(t, x, y + 2, ThemeText.Fit(m, t, head, 14, tw, FontRole.Heading), 14, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            y = Math.Max(y + 2 + L(14), build.Bottom) + 1;
            if (p.Blocker is { } blocker)
                y = Wrapped(d, m, "not yet: " + blocker, x + 8, y, w - 8, 11.5, t.Semantic.Progress);
            else
                y = Wrapped(d, m, "can be built: " + p.Materials + ", " + N1(p.LabourAdultYears) + " adult-years", x + 8, y, w - 8, 11.5, t.Semantic.Positive);
            if (!flat)
                y = Wrapped(d, m, "takes " + p.Materials + " and " + N1(p.LabourAdultYears) + " adult-years"
                    + (p.Queued > 0 ? " · " + p.Queued.ToString(CultureInfo.InvariantCulture) + " queued" : "")
                    + (p.LearnedFrom is { } lf ? " · learned: " + lf : ""), x + 8, y, w - 8, 11, t.Ink.TextDim);
            y += Math.Max(3, t.Density.Gap * 0.4);
            k++;
        }
        if (!c.Queue.IsDefaultOrEmpty)
        {
            var names = new List<string>();
            foreach (QueueEntry q in c.Queue) names.Add(q.Name);
            y = Wrapped(d, m, "Queue: " + string.Join(", then ", names), x, y, w, 12, t.Ink.Text, FontRole.Heading);
            y = Wrapped(d, m, c.HeadBlocker is { } hb ? "first in line waits - " + hb : "first in line can be built at End Turn", x + 8, y, w - 8, 11.5,
                c.HeadBlocker is null ? t.Semantic.Positive : t.Semantic.Progress);
        }
        if (c.OrderedThisTurn > 0)
            y = Wrapped(d, m, c.OrderedThisTurn.ToString(CultureInfo.InvariantCulture) + " ordered this turn - joins the queue at End Turn.", x, y, w, 11.5, t.Semantic.Active);
        return y;
    }

    // ------------------------------------------------------------------ roads (read only; transport is frozen)

    private double PaintRoads(DrawList d, ITextMeasure m, ActionSurfaceModel model, RoadsBlock rb, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = model.Control;
        y = Heading(d, m, "Roads - " + rb.ClassName, x, y, w, flat, 870 + 30);
        if (rb.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x, y, w, 11, t.Ink.TextDim);

        if (rb.OrderedPercent is { } ordered)
        {
            y = Wrapped(d, m, "Ordered this turn: develop " + N0(ordered) + "% of the demand - applied at End Turn.", x, y, w, 12, t.Semantic.Active, FontRole.Heading);
        }
        else
        {
            d.Write(t, x, y + 1, "Modernize", 13, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            var r = new RectD(x + 84, y, Math.Min(196, w - 84 - 92), Math.Max(L(13), 18));
            PaintShareControl(d, spec, r, _roadDraft, t.Semantic.Infrastructure, ActionHitKind.RoadSlot, ActionHitKind.RoadTrack, 905, minUnits: 1);
            RectD go = Button(d, m, "Develop", x + w, y, 906, primary: true, size: 12);
            _hits.Add(new ActionHit(go, ActionHitKind.RoadApply, 0, 0));
            y = Math.Max(r.Bottom, go.Bottom) + 2;
            y = Wrapped(d, m, (spec.Numerals ? N0(_roadDraft) + "% of " : "a share of ") + "the demand on our busiest routes, heaviest-used first.", x, y, w, 11.5, t.Ink.TextSoft);
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
                y = Wrapped(d, m, line, x, y, w, 11.5, t.Ink.Text);
                y = Wrapped(d, m, "An estimate: production uses tools before roads are built in the same turn.", x, y, w, 11, t.Ink.TextDim);
            }
        }
        if (rb.Blocker is { } blocker) y = Wrapped(d, m, "not yet: " + blocker, x, y, w, 11.5, t.Semantic.Progress);
        int shown = Math.Min(model.Layout == SurfaceLayout.Detailed ? 8 : 4, rb.Routes.Length);
        for (int i = 0; i < shown; i++)
        {
            RouteEntry re = rb.Routes[i];
            string line = re.Label + ": " + N0(re.LengthKm) + " km, used " + N0(re.Usage) + ", " + N0(re.ModernizedPercent) + "% modernized, speed x"
                + (1.0 / Math.Max(1e-9, re.CostFactor)).ToString("0.00", CultureInfo.InvariantCulture) + (model.Layout == SurfaceLayout.Detailed ? ", " + N0(re.CapacityTonnesPerYear) + " t/yr" : "")
                + "; completing it needs " + re.Cost;
            y = Wrapped(d, m, line, x + 8, y, w - 8, 11, t.Ink.TextSoft);
        }
        if (shown < rb.Routes.Length)
            y = Wrapped(d, m, "... and " + (rb.Routes.Length - shown).ToString(CultureInfo.InvariantCulture) + " more eligible route(s).", x + 8, y, w - 8, 11, t.Ink.TextDim);
        return y;
    }

    /// <summary>A single 0..100 share in the era's control (the tax levy, the road percentage).</summary>
    private void PaintShareControl(DrawList d, LabourControlSpec spec, RectD r, int percent, Rgba ink, ActionHitKind slot, ActionHitKind track, int id, int minUnits = 0)
    {
        EraTheme t = T;
        if (!spec.IsSlider)
        {
            int units = spec.Units, value = (int)Math.Round(percent / (double)spec.PercentPerUnit, MidpointRounding.AwayFromZero);
            double numW = spec.Numerals ? 38 : 0;
            double s = (r.W - numW) / units, rad = Math.Min(s * 0.36, r.H * 0.38);
            for (int i = 0; i < units; i++)
            {
                var hit = new RectD(r.X + i * s, r.Y - 2, s, r.H + 4);
                bool on = i < value;
                if (spec.Kind == LabourControlKind.Notches)
                    d.Rect(new RectD(hit.X + 0.6, r.Y + r.H * 0.2, Math.Max(1, s - 1.2), r.H * 0.6), on ? ink : A(t.Material.PanelSunken, 0.7), A(t.Material.Border, 0.5), 0.5);
                else if (on) d.Circle(hit.CenterX, r.CenterY, rad, ink, A(t.Ink.Text, 0.5), 0.9);
                else d.Circle(hit.CenterX, r.CenterY, rad * 0.8, null, A(t.Ink.TextDim, 0.55), 0.9);
                _hits.Add(new ActionHit(hit, slot, 0, i));
            }
            if (spec.Numerals) d.Write(t, r.Right, r.Y + 1, percent.ToString(CultureInfo.InvariantCulture) + "%", 12.5, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
            return;
        }
        var tr = new RectD(r.X, r.CenterY - 3, r.W - 46, 6);
        d.Rect(tr, A(t.Material.PanelSunken, 0.8), A(t.Material.Border, 0.6), 0.6);
        d.Rect(new RectD(tr.X, tr.Y, tr.W * percent / 100.0, tr.H), ink);
        d.Circle(tr.X + tr.W * percent / 100.0, tr.CenterY, Math.Max(4, t.Controls.GrabPx * 0.42), t.Material.PanelRaised, t.Material.BorderStrong, 1.1);
        d.Write(t, r.Right, r.Y + 1, percent.ToString(CultureInfo.InvariantCulture) + "%", 12.5, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
        _hits.Add(new ActionHit(new RectD(tr.X, r.Y - 2, tr.W, r.H + 4), track, 0, 0));
        _ = minUnits;
        _ = id;
    }

    // ------------------------------------------------------------------ governance

    private double PaintGovernance(DrawList d, ITextMeasure m, ActionSurfaceModel model, GovernanceBlock g, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        LabourControlSpec spec = model.Control;
        y = Heading(d, m, flat ? "The tax edict" : "Governance - the tax edict", x, y, w, flat, 920);
        if (g.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x, y, w, 11, t.Ink.TextDim);
        string declared = !g.HasPolicy ? "No levy declared yet."
            : spec.Numerals ? "Declared levy: " + g.DeclaredPercent.ToString(CultureInfo.InvariantCulture) + "%."
            : "A levy is declared.";
        y = Wrapped(d, m, declared, x, y, w, 12.5, t.Ink.Text, FontRole.Heading);
        d.Write(t, x, y + 1, "Levy", 13, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        var r = new RectD(x + 66, y, Math.Min(200, w - 66 - 90), Math.Max(L(13), 18));
        PaintShareControl(d, spec, r, Math.Max(0, _taxDraft), t.Semantic.Danger, ActionHitKind.TaxSlot, ActionHitKind.TaxTrack, 921);
        int baseline = g.QueuedPercent ?? g.DeclaredPercent;
        double bottom = r.Bottom;
        if (_taxDraft >= 0 && (_taxDraft != baseline || !g.HasPolicy && g.QueuedPercent is null))
        {
            RectD go = Button(d, m, "Declare", x + w, y, 922, primary: true, size: 12);
            _hits.Add(new ActionHit(go, ActionHitKind.TaxApply, 0, 0));
            bottom = Math.Max(bottom, go.Bottom);
        }
        y = bottom + 2;
        if (g.QueuedPercent is { } q)
            y = Wrapped(d, m, "Declared this turn: " + q.ToString(CultureInfo.InvariantCulture) + "% - in force from the next turn, felt the turn after.", x, y, w, 11.5, t.Semantic.Active);
        y = Wrapped(d, m, "A levy raises what our people produce and lowers their happiness; it reaches only as far as our administration does.", x, y, w, 11, t.Ink.TextDim);
        y = Wrapped(d, m, g.Legitimacy, x, y, w, 12, t.Ink.Text, FontRole.Numeric);
        int shown = model.Layout == SurfaceLayout.Detailed ? g.Collection.Length : Math.Min(4, g.Collection.Length);
        for (int i = 0; i < shown; i++) y = Wrapped(d, m, g.Collection[i], x + 8, y, w - 8, 11, t.Ink.TextSoft, FontRole.Numeric);
        if (shown < g.Collection.Length)
            y = Wrapped(d, m, "... " + (g.Collection.Length - shown).ToString(CultureInfo.InvariantCulture) + " more settlement(s).", x + 8, y, w - 8, 11, t.Ink.TextDim);
        return y;
    }

    // ------------------------------------------------------------------ military (information only)

    private double PaintMilitary(DrawList d, ITextMeasure m, ActionSurfaceModel model, MilitaryBlock mb, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Basic fighting" : "Arms - basic fighting", x, y, w, flat, 940);
        int k = 0;
        foreach (FormationEntry f in mb.Formations)
        {
            EraMarks.State(d, t, x + 7, y + L(13) / 2, 5, MarkKind.Completed, 1, 941 + k, dim: true);
            y = Wrapped(d, m, f.Identity + " at " + f.Where, x + 18, y, w - 18, 13, t.Semantic.Military, FontRole.Heading);
            // The family line and the formation's CURRENT Age identity (ADR-031: it modernizes with the Age).
            string form = f.Family + (f.IdentityAge > 0 ? " - its Age " + AgePanelModel.Numeral(f.IdentityAge) + " form" : "");
            y = Wrapped(d, m, form, x + 18, y, w - 18, 11.5, t.Ink.TextSoft);
            if (!flat && f.Line.Length > 0) y = Wrapped(d, m, "line: " + f.Line, x + 18, y, w - 18, 11, t.Ink.TextSoft);
            k++;
        }
        if (mb.NextAgeName is { } next && !mb.Modernization.IsDefaultOrEmpty)
            y = Wrapped(d, m, "At the next Age (" + next + "): " + string.Join("; ", mb.Modernization), x, y + 1, w, 11.5, t.Ink.TextSoft);
        return Wrapped(d, m, mb.Note, x, y, w, 11, t.Ink.TextDim);
    }

    // ------------------------------------------------------------------ production (R1)

    private double PaintProduction(DrawList d, ITextMeasure m, ProductionBlock p, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Crafts we know" : "Production - crafts we know", x, y, w, flat, 930);
        int k = 0;
        foreach (ProductionEntry e in p.Entries)
        {
            EraMarks.State(d, t, x + 7, y + L(13) / 2, 5, e.Settlements > 0 ? MarkKind.Completed : MarkKind.Available, 1, 931 + k, dim: e.Settlements == 0);
            y = Wrapped(d, m, e.Name, x + 18, y, w - 18, 13, t.Ink.Text, FontRole.Heading);
            string where = e.Settlements > 0
                ? "made in " + e.Settlements.ToString(CultureInfo.InvariantCulture) + " settlement(s)"
                : e.Blocker ?? "made nowhere yet";
            y = Wrapped(d, m, e.Detail + " · " + where, x + 18, y, w - 18, 11.5, t.Ink.TextSoft);
            if (e.LearnedFrom is { } lf && !flat) y = Wrapped(d, m, "learned: " + lf, x + 18, y, w - 18, 11, t.Ink.TextDim);
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
        return Wrapped(d, m, string.Join(" · ", s.Items), x, y, w, 12, t.Ink.TextSoft);
    }
}
