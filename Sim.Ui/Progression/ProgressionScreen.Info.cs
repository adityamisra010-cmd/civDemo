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
    private (int Node, IReadOnlyWorldState? World, InfoCard? Card) _cardCache;

    /// <summary>The session's config (set by the host): the discoverability block and the condition readings read it.
    /// Null (a preview) paints the plain "opens the way to" list instead.</summary>
    public SimConfig? Config { get; set; }

    /// <summary>The inspectable regions painted last frame (screen coordinates).</summary>
    public IReadOnlyList<InfoHit> InfoHits => _info;

    private void InfoAdd(RectD r, InfoSubject subject, string label)
    {
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

    /// <summary>The detail panel's discoverability block for <paramref name="node"/>; returns the y after it.</summary>
    private double PaintDiscovery(DrawList d, ITextMeasure m, int node, double x, double w, double y)
    {
        EraTheme t = Theme;
        double L(double size) => t.Type.Line(size);
        ResearchNodeView n = Snapshot!.Nodes[node];
        if (NodeCard(node) is not { } card)
        {
            if (n.Unlocks.Count == 0) return y;
            y = Heading(d, x, w, y + 4, "OPENS THE WAY TO", 5);
            foreach (string line in ThemeText.Wrap(m, t, string.Join(", ", n.Unlocks), 13, w)) { d.Write(t, x, y, line, 13, t.Ink.TextSoft); y += L(13); }
            return y + 6;
        }

        var opens = new List<InfoLink>();
        var leads = new List<InfoLink>();
        foreach (InfoLink l in card.Enables)
        {
            if (l.Subject is { Kind: InfoKind.ResearchNode }) leads.Add(l);
            else opens.Add(l);
        }
        if (opens.Count > 0)
        {
            y = Heading(d, x, w, y, "OPENS THE WAY TO", 5);
            int shown = 0;
            foreach (InfoLink l in opens)
            {
                if (shown == 6) { y = Wrapped(d, m, t, "... and " + (opens.Count - shown).ToString(System.Globalization.CultureInfo.InvariantCulture) + " more - Shift+click the node for its card", x, y, w, 11.5, t.Ink.TextDim); break; }
                Rgba col = l.Subject is not null ? t.Semantic.Knowledge : t.Ink.Text;
                string label = ThemeText.Fit(m, t, l.Label, 13.5, w);
                d.Write(t, x, y, label, 13.5, col, TextAlign.Left, FontRole.Heading);
                if (l.Subject is { } s) InfoAdd(new RectD(x, y, m.Width(t, label, 13.5, FontRole.Heading), L(13.5)), s, l.Label);
                y += L(13.5);
                if (l.Note is { Length: > 0 } note) y = Wrapped(d, m, t, note, x + 10, y, w - 10, 11.5, t.Ink.TextDim);
                y += 2;
                shown++;
            }
        }
        if (leads.Count > 0)
        {
            y = Heading(d, x, w, y + 2, "LEADS TO", 6);
            var tokens = new List<(string, InfoSubject)>();
            var names = new List<string>();
            foreach (InfoLink l in leads) { names.Add(l.Label); tokens.Add((l.Label, l.Subject!.Value)); }
            y = WrappedTokens(d, m, t, string.Join(", ", names), x, y, w, 13, t.Semantic.Knowledge, tokens);
        }
        if (!card.KnowledgeOnly.IsDefaultOrEmpty)
        {
            y = Heading(d, x, w, y + 4, "DESCRIBED, NOT SIMULATED", 7);
            y = Wrapped(d, m, t, string.Join("; ", card.KnowledgeOnly), x, y, w, 12.5, t.Ink.TextSoft);
        }
        if (card.Effect == InfoEffect.KnowledgeOnly)
            y = Wrapped(d, m, t, "Knowledge only - no simulated effect in this build.", x, y + 2, w, 12.5, t.Semantic.Progress, FontRole.Heading);
        else if (card.Effect == InfoEffect.ResearchOnly)
            y = Wrapped(d, m, t, "No simulated effect of its own in this build: it opens further research.", x, y + 2, w, 12, t.Semantic.Progress);
        return y + 6;
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

    /// <summary>A wrapped line whose named tokens are registered where they are painted.</summary>
    private double WrappedTokens(DrawList d, ITextMeasure m, EraTheme t, string text, double x, double y, double w, double size, Rgba color,
        List<(string Token, InfoSubject Subject)> tokens)
    {
        foreach (string line in ThemeText.Wrap(m, t, text, size, Math.Max(20, w)))
        {
            d.Write(t, x, y, line, size, color);
            foreach ((string token, InfoSubject subject) in tokens)
            {
                int at = line.IndexOf(token, StringComparison.Ordinal);
                if (at < 0) continue;
                InfoAdd(new RectD(x + m.Width(t, line[..at], size), y, m.Width(t, token, size), t.Type.Line(size)), subject, token);
            }
            y += t.Type.Line(size);
        }
        return y;
    }
}
