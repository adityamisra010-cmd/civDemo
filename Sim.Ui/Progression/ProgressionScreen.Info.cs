using System.Collections.Immutable;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Info;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Progression;

/// <summary>
/// M5 polish (directive §9, §10) — the research screen's INSPECTABLE REGIONS (every visible card, every prerequisite
/// row, every "opens" line, the lens pages' items) and the detail panel's DISCOVERABILITY block: what the node opens
/// (each entity with what realizes it in this build, or "not built in this build"), the Age milestones naming it, the
/// tax edict or research stage it feeds, the nodes it leads to, and the capability strings it only describes —
/// "knowledge only - no simulated effect in this build" where true. Every line is the node's InfoQuery card; the
/// screen invents nothing.
/// </summary>
public sealed partial class ProgressionScreen
{
    private List<InfoHit> _info = [];
    private RectD? _infoClip;   // the detail panel's scrolled view while it paints (UR-4): regions are cut to it
    private (int Node, IReadOnlyWorldState? World, InfoCard? Card) _cardCache;

    /// <summary>The session's config (set by the host): the discoverability block and the condition readings read it.
    /// Null (a preview) paints the plain "opens the way to" list instead.</summary>
    public SimConfig? Config { get; set; }

    /// <summary>The inspectable regions painted last frame (screen coordinates).</summary>
    public IReadOnlyList<InfoHit> InfoHits => _info;

    private void InfoAdd(RectD r, InfoSubject subject, string label)
    {
        if (_infoClip is { } clip)
        {
            if (!r.Intersects(clip)) return;
            r = InfoRegistry.Intersect(r, clip);
        }
        if (r.W > 0.5 && r.H > 0.5) _info.Add(new InfoHit(r, subject, label));
    }

    /// <summary>A tree card: registered where it is visible inside the canvas.</summary>
    private void InfoCardHit(RectD r, ResearchNodeView v)
    {
        RectD c = Canvas;
        if (!r.Intersects(c)) return;
        InfoAdd(InfoRegistry.Intersect(r, c), InfoSubject.Node(v.Key), v.Name);
    }

    private void InfoNode(RectD r, int contentIndex) =>
        InfoAdd(r, InfoSubject.Node(Content.Nodes[contentIndex].Key), Content.Nodes[contentIndex].Name);

    /// <summary>A D-020 condition with its live value in plain words (InfoQuery.ConditionReading), or verbatim without a config.</summary>
    private string ConditionText(string condition) =>
        _world is not null && Config is not null ? InfoQuery.ConditionReading(_world, Config, Polity, condition) : condition;

    private InfoCard? NodeCard(int node)
    {
        if (Config is null || _world is null) return null;
        if (_cardCache.Node == node && ReferenceEquals(_cardCache.World, _world) && _cardCache.Card is { } cached) return cached;
        InfoCard card = InfoQuery.Card(_world, Config, Polity, InfoSubject.Node(Content.Nodes[node].Key),
            actions: ImmutableArray<ActionDescriptor>.Empty);
        _cardCache = (node, _world, card);
        return card;
    }

    /// <summary>The detail panel's discoverability block for <paramref name="node"/> (inside ENABLES; UR-4 sizing: the
    /// body role, wrapped, never dropped); returns the y after it. Painted only when the node has an InfoQuery card.</summary>
    private double PaintDiscovery(DrawList d, ITextMeasure m, int node, double x, double w, double y)
    {
        EraTheme t = Theme;
        if (NodeCard(node) is not { } card) return y;

        var opens = new List<InfoLink>();
        var leads = new List<InfoLink>();
        foreach (InfoLink l in card.Enables)
        {
            if (l.Subject is { Kind: InfoKind.ResearchNode }) leads.Add(l);
            else opens.Add(l);
        }
        if (opens.Count > 0)
        {
            y = SubHeading(d, m, x, w, y, "OPENS THE WAY TO");
            int shown = 0;
            foreach (InfoLink l in opens)
            {
                if (shown == 6) { y = Wrapped(d, m, "... and " + (opens.Count - shown).ToString(System.Globalization.CultureInfo.InvariantCulture) + " more - Shift+click the node for its card", x, y, w, TypeRole.Secondary, t.Ink.TextSoft); break; }
                Rgba col = l.Subject is not null ? Ink.Knowledge : t.Ink.Text;
                double size = Px(TypeRole.Body, FontRole.Heading), slot = FlowText.Slot(TypeRole.Body, _scale);
                foreach (string line in FlowText.Wrap(m, l.Label, TypeRole.Body, _scale, w, FontRole.Heading))
                {
                    d.Write(t, x, y, line, size, col, TextAlign.Left, FontRole.Heading);
                    if (l.Subject is { } s) InfoAdd(new RectD(x, y, FlowText.Width(m, line, TypeRole.Body, _scale, FontRole.Heading), slot), s, l.Label);
                    y += slot;
                }
                if (l.Note is { Length: > 0 } note) y = Wrapped(d, m, note, x + Sp(10), y, w - Sp(10), TypeRole.Secondary, t.Ink.TextSoft);
                y += Sp(2);
                shown++;
            }
        }
        if (leads.Count > 0)
        {
            y = SubHeading(d, m, x, w, y + Sp(2), "LEADS TO");
            var tokens = new List<(string, InfoSubject)>();
            var names = new List<string>();
            foreach (InfoLink l in leads) { names.Add(l.Label); tokens.Add((l.Label, l.Subject!.Value)); }
            y = WrappedTokens(d, m, string.Join(", ", names), x, y, w, TypeRole.Body, Ink.Knowledge, tokens);
        }
        if (!card.KnowledgeOnly.IsDefaultOrEmpty)
        {
            y = SubHeading(d, m, x, w, y + Sp(4), "DESCRIBED, NOT SIMULATED");
            y = Wrapped(d, m, string.Join("; ", card.KnowledgeOnly), x, y, w, TypeRole.Body, t.Ink.TextSoft);
        }
        if (card.Effect == InfoEffect.KnowledgeOnly)
            y = Statement(d, m, "Knowledge only - no simulated effect in this build.", x, y + Sp(2), w, Ink.Progress, FontRole.Heading);
        else if (card.Effect == InfoEffect.ResearchOnly)
            y = Statement(d, m, "No simulated effect of its own in this build: it opens further research.", x, y + Sp(2), w, Ink.Progress);
        return y + Sp(6);
    }

    /// <summary>The node's effect statement: ONE run, fitted (down to the role's floor) when it fits on a line there —
    /// it is read as a single verdict — and wrapped at the body role otherwise (never dropped).</summary>
    private double Statement(DrawList d, ITextMeasure m, string text, double x, double y, double w, Rgba color, FontRole font = FontRole.Body)
    {
        double design = Px(TypeRole.Body, font);
        double size = FitFloor(m, text, design, w, font);
        if (m.Width(Theme, text, size, font) > w) return Wrapped(d, m, text, x, y, w, TypeRole.Body, color, font);
        WriteFit(d, m, x, y, text, design, w, color, TextAlign.Left, font);
        return y + FlowText.Slot(TypeRole.Body, _scale);
    }

    /// <summary>A sub-heading inside ENABLES: capitals at the caption role in the accent's text ink.</summary>
    private double SubHeading(DrawList d, ITextMeasure m, double x, double w, double y, string text)
    {
        WriteFit(d, m, x, y, text, Px(TypeRole.Caption, FontRole.Caps), w, Ink.Accent, TextAlign.Left, FontRole.Caps);
        return y + FlowText.Slot(TypeRole.Caption, _scale, caps: true) + Sp(2);
    }

    private double Wrapped(DrawList d, ITextMeasure m, string text, double x, double y, double w, TypeRole role, Rgba color, FontRole font = FontRole.Body)
    {
        EraTheme t = Theme;
        double size = Px(role, font), slot = FlowText.Slot(role, _scale);
        foreach (string line in FlowText.Wrap(m, text, role, _scale, Math.Max(20, w), font))
        {
            d.Write(t, x, y, line, size, color, TextAlign.Left, font);
            y += slot;
        }
        return y;
    }

    /// <summary>A wrapped line whose named tokens are registered where they are painted (a token the wrap splits is
    /// registered on each line it occupies).</summary>
    private double WrappedTokens(DrawList d, ITextMeasure m, string text, double x, double y, double w, TypeRole role, Rgba color,
        List<(string Token, InfoSubject Subject)> tokens)
    {
        EraTheme t = Theme;
        double size = Px(role), slot = FlowText.Slot(role, _scale);
        List<string> lines = FlowText.Wrap(m, text, role, _scale, Math.Max(20, w));
        var starts = new int[lines.Count];
        int len = 0;
        for (int i = 0; i < lines.Count; i++) { starts[i] = len; len += lines[i].Length + 1; }
        string joined = string.Join(" ", lines);
        for (int i = 0; i < lines.Count; i++) d.Write(t, x, y + i * slot, lines[i], size, color);
        foreach ((string token, InfoSubject subject) in tokens)
        {
            int at = token.Length == 0 ? -1 : joined.IndexOf(token, StringComparison.Ordinal);
            if (at < 0) continue;
            int end = at + token.Length;
            for (int i = 0; i < lines.Count; i++)
            {
                int a = Math.Max(at, starts[i]) - starts[i], b = Math.Min(end, starts[i] + lines[i].Length) - starts[i];
                if (b <= a) continue;
                double x0 = x + FlowText.Width(m, lines[i][..a], role, _scale);
                InfoAdd(new RectD(x0, y + i * slot, FlowText.Width(m, lines[i][a..b], role, _scale), slot), subject, token);
            }
        }
        return y + lines.Count * slot;
    }
}
