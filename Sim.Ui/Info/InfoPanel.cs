using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Info;

/// <summary>What a click on the info card asks the host to do. None of it is an order.</summary>
public enum InfoCommandKind { None = 0, Close = 1, Back = 2, Open = 3, ShowInTree = 4 }

public readonly record struct InfoCommand(InfoCommandKind Kind, InfoSubject Subject = default, ResearchNodeId Node = default)
{
    public static readonly InfoCommand None = new(InfoCommandKind.None);
}

public enum InfoHitKind { Close, Back, ShowInTree, Link }

public readonly record struct InfoPanelHit(RectD Rect, InfoHitKind Kind, InfoSubject Subject);

/// <summary>
/// M5 polish (directive §9) — THE PINNED INFO CARD: which subject is open (and a short history for Back), the card
/// built for it by <see cref="InfoQuery.Card"/> (rebuilt only when the world, the subject or the queued orders change),
/// and its painter. The painter is ONE backend-agnostic DrawList painter used in both places the card is shown — the
/// right-hand context column over the map and the research screen's detail column — in the era's hand, from the
/// theme's tokens only. UI state only: opening, navigating and closing a card never writes the world and never
/// issues an order (a link opens another card; "Show in research tree" moves the camera).
/// </summary>
public sealed class InfoPanel
{
    public const int HistoryLimit = 8;

    private readonly List<InfoSubject> _history = [];
    private List<InfoPanelHit> _hits = [];
    private object? _builtFor;
    private InfoSubject _builtSubject;
    private int _builtQueued = -1;

    /// <summary>The open subject, or null.</summary>
    public InfoSubject? Subject { get; private set; }

    /// <summary>The card for <see cref="Subject"/> (after <see cref="Ensure"/>).</summary>
    public InfoCard? Card { get; private set; }

    public bool IsOpen => Subject is not null;

    /// <summary>The previous subjects (oldest first) Back returns to.</summary>
    public IReadOnlyList<InfoSubject> History => _history;

    /// <summary>The hit regions painted last frame.</summary>
    public IReadOnlyList<InfoPanelHit> Hits => _hits;

    /// <summary>The card's vertical scroll (the research overlay scrolls the card itself; the context panel uses its window).</summary>
    public double Scroll { get; private set; }

    /// <summary>Opens a subject (the current one goes on the history).</summary>
    public void Open(InfoSubject subject)
    {
        if (Subject is { } current && current != subject)
        {
            _history.Add(current);
            if (_history.Count > HistoryLimit) _history.RemoveAt(0);
        }
        Subject = subject;
        Card = null;
        Scroll = 0;
    }

    public void Back()
    {
        if (_history.Count == 0) { Close(); return; }
        Subject = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        Card = null;
        Scroll = 0;
    }

    public void Close()
    {
        Subject = null;
        Card = null;
        _history.Clear();
        _hits = [];
        Scroll = 0;
    }

    public void ScrollBy(double dy, double contentHeight, double viewHeight) =>
        Scroll = Math.Clamp(Scroll + dy, 0, Math.Max(0, contentHeight - viewHeight));

    /// <summary>Builds the card when the world, the subject or the number of queued orders changed.</summary>
    public InfoCard? Ensure(IReadOnlyWorldState world, SimConfig cfg, PolityId player, ActionQueryContext context,
        Func<int, string> names, int queued)
    {
        if (Subject is not { } s) return null;
        if (Card is null || !ReferenceEquals(_builtFor, world) || _builtSubject != s || _builtQueued != queued)
        {
            Card = InfoQuery.Card(world, cfg, player, s, context, names);
            _builtFor = world;
            _builtSubject = s;
            _builtQueued = queued;
        }
        return Card;
    }

    /// <summary>What a click at (x, y) asks for; <paramref name="visible"/> limits it to the card's visible region.</summary>
    public InfoCommand Click(double x, double y, RectD? visible = null)
    {
        if (visible is { } v && !v.Contains(x, y)) return InfoCommand.None;
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            InfoPanelHit h = _hits[i];
            if (!h.Rect.Contains(x, y)) continue;
            return h.Kind switch
            {
                InfoHitKind.Close => new InfoCommand(InfoCommandKind.Close),
                InfoHitKind.Back => new InfoCommand(InfoCommandKind.Back),
                InfoHitKind.ShowInTree when Card?.ResearchNode is { } n => new InfoCommand(InfoCommandKind.ShowInTree, default, n),
                InfoHitKind.Link => new InfoCommand(InfoCommandKind.Open, h.Subject),
                _ => InfoCommand.None,
            };
        }
        return InfoCommand.None;
    }

    // ================================================================== painting

    private static Rgba A(Rgba c, double alpha) => ThemeColor.Alpha(c, alpha);
    private static Rgba Mix(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);

    /// <summary>
    /// Paints the card at (<paramref name="x"/>, <paramref name="y"/>) in <paramref name="w"/> and returns the height
    /// used; records the hit regions <see cref="Click"/> answers. <paramref name="showClose"/> draws the card's own close
    /// button (the research overlay; the context panel has its header's).
    /// </summary>
    public double Paint(DrawList d, ITextMeasure m, EraTheme t, double x, double y, double w, bool showClose)
    {
        _hits = [];
        if (Card is not { } c) return 0;
        double y0 = y;
        double L(double s) => t.Type.Line(s);

        // Header: what kind of thing it is, Back / close at the right.
        double right = x + w;
        if (showClose)
        {
            var close = new RectD(right - 24, y, 24, 22);
            PanelFrame.Paint(d, close, t, 7001, FrameKind.Button);
            EraMarks.Close(d, t, close, t.Ink.TextSoft, 7002);
            _hits.Add(new InfoPanelHit(close, InfoHitKind.Close, default));
            right = close.X - 6;
        }
        if (_history.Count > 0)
        {
            string back = "< Back";
            double bw = m.Width(t, back, 11.5) + 16;
            var br = new RectD(right - bw, y, bw, 22);
            PanelFrame.Paint(d, br, t, 7003, FrameKind.Button);
            d.Write(t, br.CenterX, br.Y + 4, back, 11.5, t.Ink.TextSoft, TextAlign.Center);
            _hits.Add(new InfoPanelHit(br, InfoHitKind.Back, default));
            right = br.X - 6;
        }
        foreach (string line in ThemeText.Wrap(m, t, c.Kind.ToUpperInvariant(), 10.5, Math.Max(40, right - x), FontRole.Caps))
        {
            d.Write(t, x, y + 3, line, 10.5, t.Material.Accent, TextAlign.Left, FontRole.Caps);
            y += L(10.5);
        }
        y = Math.Max(y, y0 + 24) + 2;
        foreach (string line in ThemeText.Wrap(m, t, c.Title, 20, w, FontRole.Heading))
        {
            d.Write(t, x, y, line, 20, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            y += L(20);
        }
        y += 2;

        // Status: a chip in the state's colour, then the status line.
        Rgba sc = StatusColor(t, c.Status);
        string chip = StatusWord(c.Status);
        double cw = m.Width(t, chip, 10.5, FontRole.Caps) + 16;
        PanelFrame.Paint(d, new RectD(x, y, cw, 18), t, 7004, FrameKind.Chip, Mix(t.Material.Panel, sc, 0.16), sc, 1.0);
        d.Write(t, x + 8, y + 3, chip, 10.5, sc, TextAlign.Left, FontRole.Caps);
        y += 22;
        y = Wrapped(d, m, t, c.StatusLine, x, y, w, 13, t.Ink.Text);
        if (c.Summary is { Length: > 0 } sum && c.Status is not (InfoStatus.Known or InfoStatus.Met or InfoStatus.Researching))
            y = Wrapped(d, m, t, "Why not yet: " + sum, x, y + 2, w, 13, t.Semantic.Progress, FontRole.Heading);
        if (c.What.Length > 0) y = Wrapped(d, m, t, c.What, x, y + 4, w, 12.5, t.Ink.TextSoft);

        // Show in research tree.
        if (c.ResearchNode is { } node && c.ResearchNodeName is { } nodeName)
        {
            y += 6;
            string label = "Show in research tree: " + nodeName;
            string fit = ThemeText.Fit(m, t, label, 12.5, w - 20);
            double bw = Math.Min(w, m.Width(t, fit, 12.5) + 20);
            var br = new RectD(x, y, bw, L(12.5) + 8);
            PanelFrame.Paint(d, br, t, 7005, FrameKind.Button, Mix(t.Material.PanelRaised, t.Semantic.Knowledge, 0.12), t.Semantic.Knowledge, 1.1);
            d.Write(t, br.X + 10, br.Y + 4, fit, 12.5, t.Semantic.Knowledge, TextAlign.Left);
            _hits.Add(new InfoPanelHit(br, InfoHitKind.ShowInTree, InfoSubject.Node(node)));
            y = br.Bottom + 2;
        }

        y = Section(d, m, t, "WHY NOT YET", c.WhyLocked, x, y, w, 7100);
        y = Section(d, m, t, c.Subject.Kind == InfoKind.Good ? "WHERE IT COMES FROM" : "WHAT ENABLES IT", c.EnabledBy, x, y, w, 7200);
        if (c.RequirementSource is { Length: > 0 } src && c.Subject.Kind != InfoKind.AgeMilestone)
            y = Wrapped(d, m, t, "requires: " + src.Replace('_', ' '), x, y, w, 11, t.Ink.TextDim, FontRole.Numeric);
        if (!c.Prerequisites.IsDefaultOrEmpty)
            y = Section(d, m, t, "RESEARCH " + (c.ResearchNodeName ?? "").ToUpperInvariant() + " NEEDS", c.Prerequisites, x, y, w, 7300);
        y = Section(d, m, t, "REQUIRES", c.Requirements, x, y, w, 7400);
        foreach (InfoSection s in c.More) y = Section(d, m, t, s.Heading.ToUpperInvariant(), s.Lines, x, y, w, 7500);
        y = Section(d, m, t, c.Subject.Kind == InfoKind.Good ? "WHAT USES IT" : "WHAT IT ENABLES", c.Enables, x, y, w, 7600);
        if (!c.KnowledgeOnly.IsDefaultOrEmpty)
        {
            y = Heading(d, t, "DESCRIBED, NOT SIMULATED", x, y + 6, w, 7700);
            y = Wrapped(d, m, t, "Knowledge only - no system simulates these in this build:", x, y, w, 11.5, t.Ink.TextDim);
            foreach (string k in c.KnowledgeOnly) y = Wrapped(d, m, t, "-  " + k, x + 6, y, w - 6, 12.5, t.Ink.TextSoft);
        }
        if (c.RealizedBy.Length > 0)
        {
            y = Heading(d, t, "IN THIS BUILD", x, y + 6, w, 7800);
            y = Wrapped(d, m, t, c.RealizedBy, x, y, w, 12, c.Effect == InfoEffect.KnowledgeOnly ? t.Semantic.Progress : t.Ink.TextSoft);
        }
        return y - y0 + 6;
    }

    private double Section(DrawList d, ITextMeasure m, EraTheme t, string heading, ImmutableArray<InfoLink> lines, double x, double y, double w, int id)
    {
        if (lines.IsDefaultOrEmpty) return y;
        y = Heading(d, t, heading, x, y + 6, w, id);
        int k = 0;
        foreach (InfoLink l in lines)
        {
            double tx = x;
            if (l.Met is bool met)
            {
                EraMarks.Tick(d, t, x + 1, y + 3, 10, met, met ? t.Semantic.Positive : t.Semantic.Danger, id + 1 + k);
                tx = x + 16;
            }
            Rgba col = l.Subject is not null ? t.Semantic.Knowledge : t.Ink.Text;
            double ly = y;
            List<string> label = ThemeText.Wrap(m, t, l.Label, 13, x + w - tx, l.Subject is not null ? FontRole.Heading : FontRole.Body);
            double widest = 0;
            foreach (string line in label)
            {
                d.Write(t, tx, y, line, 13, col, TextAlign.Left, l.Subject is not null ? FontRole.Heading : FontRole.Body);
                widest = Math.Max(widest, m.Width(t, line, 13, l.Subject is not null ? FontRole.Heading : FontRole.Body));
                y += t.Type.Line(13);
            }
            if (l.Subject is { } s)
            {
                d.Line(tx, y - 2, tx + widest, y - 2, A(col, 0.45), 0.8);
                _hits.Add(new InfoPanelHit(new RectD(tx - 2, ly, Math.Max(12, widest + 4), y - ly), InfoHitKind.Link, s));
            }
            if (l.Note is { Length: > 0 } note) y = Wrapped(d, m, t, note, tx + 6, y, x + w - tx - 6, 11, t.Ink.TextDim);
            y += 2;
            k++;
        }
        return y;
    }

    private static double Heading(DrawList d, EraTheme t, string text, double x, double y, double w, int id)
    {
        d.Write(t, x, y, text, 10.5, t.Material.Accent, TextAlign.Left, FontRole.Caps);
        PanelFrame.Rule(d, new RectD(x, y + t.Type.Line(10.5) + 1, w, 4), t, id);
        return y + t.Type.Line(10.5) + 8;
    }

    private static double Wrapped(DrawList d, ITextMeasure m, EraTheme t, string text, double x, double y, double w, double size, Rgba color, FontRole role = FontRole.Body)
    {
        foreach (string line in ThemeText.Wrap(m, t, text, size, Math.Max(20, w), role))
        {
            d.Write(t, x, y, line, size, color, TextAlign.Left, role);
            y += t.Type.Line(size);
        }
        return y;
    }

    public static string StatusWord(InfoStatus s) => s switch
    {
        InfoStatus.Known => "KNOWN",
        InfoStatus.Available => "AVAILABLE",
        InfoStatus.Blocked => "AVAILABLE - WAITS",
        InfoStatus.Researchable => "OPEN TO RESEARCH",
        InfoStatus.Researching => "BEING RESEARCHED",
        InfoStatus.Locked => "LOCKED",
        InfoStatus.NeedsAge => "NEEDS AN AGE",
        InfoStatus.Met => "MET",
        InfoStatus.NotMet => "NOT YET MET",
        InfoStatus.Pending => "PENDING",
        InfoStatus.NotSimulated => "NOT SIMULATED",
        InfoStatus.Inert => "NOT LOADED",
        _ => "UNKNOWN",
    };

    public static Rgba StatusColor(EraTheme t, InfoStatus s) => s switch
    {
        InfoStatus.Known or InfoStatus.Met => t.Semantic.Completed,
        InfoStatus.Available or InfoStatus.Researchable => t.Semantic.Available,
        InfoStatus.Researching => t.Semantic.Active,
        InfoStatus.Blocked or InfoStatus.NeedsAge or InfoStatus.NotMet or InfoStatus.Pending => t.Semantic.Progress,
        InfoStatus.Locked => t.Semantic.Locked,
        _ => t.Ink.TextDim,
    };

    /// <summary>The one-line hover hint painted beside the pointer over an inspectable object (not an ImGui tooltip).</summary>
    public const string HintText = "Shift+click: details";

    /// <summary>Paints the hover hint near (<paramref name="px"/>, <paramref name="py"/>), kept inside the viewport; returns its rect.</summary>
    public static RectD PaintHint(DrawList d, ITextMeasure m, EraTheme t, double px, double py, double viewW, double viewH)
    {
        double w = m.Width(t, HintText, 11.5) + 14, h = t.Type.Line(11.5) + 6;
        double x = Math.Min(px + 16, viewW - w - 4), y = Math.Min(py + 20, viewH - h - 4);
        if (y < 0) y = 0;
        var r = new RectD(Math.Max(0, x), y, w, h);
        d.Rect(r, A(t.Material.PanelRaised, 0.94), A(t.Material.Border, 0.8), 0.8, 3);
        d.Write(t, r.X + 7, r.Y + 3, HintText, 11.5, t.Ink.TextSoft);
        return r;
    }

    public static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
}
