using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Info;
using Sim.Ui.Render;
using Sim.Ui.Theme;

namespace Sim.Ui.Ages;

/// <summary>
/// M5 polish (directive §9, §10) — the Age panel's INSPECTABLE REGIONS (the Age banner, the next Age, every milestone
/// line) and, under a research milestone not yet met, the node to RESEARCH NEXT toward it ("research next: Grinding
/// stone, then Cereal cultivation") — a click on it opens the trees focused on that node. The node is
/// InfoQuery.NextResearchToward over the milestone's listed node, read from content and completed knowledge.
/// </summary>
public sealed partial class AgeScreen
{
    private List<InfoHit> _info = [];

    /// <summary>The research content, for the research-next lines (set by the host; null = none shown).</summary>
    public ResearchContent? Research { get; set; }

    /// <summary>The inspectable regions painted last frame.</summary>
    public IReadOnlyList<InfoHit> InfoHits => _info;

    /// <summary>The top-most inspectable region at a point, or null.</summary>
    public InfoHit? InfoAt(double x, double y) => InfoRegistry.At(_info, x, y);

    private void Info(RectD r, InfoSubject subject, string label)
    {
        if (r.W > 0.5 && r.H > 0.5) _info.Add(new InfoHit(r, subject, label));
    }

    /// <summary>The research-next line under an unmet research milestone; a click focuses that node in the trees.</summary>
    private double PaintResearchNext(DrawList d, ITextMeasure m, MilestoneLine l, double x, double y, double w)
    {
        if (l.Met || l.NextNodeKey < 0 || l.NextNodeName is not { } next) return y;
        EraTheme t = Theme;
        string text = "research next: " + next + (l.TargetNodeName is { } target && target != next ? ", then " + target : "") + "  [K]";
        string fit = ThemeText.Fit(m, t, text, 11.5, w - 26);
        var r = new RectD(x + 20, y - 2, Math.Min(w - 26, m.Width(t, fit, 11.5) + 4), t.Type.Line(11.5) + 2);
        d.Write(t, x + 20, y - 1, fit, 11.5, t.Semantic.Knowledge);
        d.Line(r.X, r.Bottom - 1, r.Right - 4, r.Bottom - 1, ThemeColor.Alpha(t.Semantic.Knowledge, 0.4), 0.8);
        _hits.Add(new AgeHitRegion(r, AgeHit.FocusNode, l.NextNodeKey));
        Info(r, InfoSubject.Node(new ResearchNodeId(l.NextNodeKey)), next);
        return y + Math.Max(17, t.Type.Line(11.5) + 2);
    }
}
