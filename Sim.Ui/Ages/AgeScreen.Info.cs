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
    private RectD? _infoClip;   // the scrolled panel view while its body paints (UR-5): regions are cut to it

    /// <summary>The research content, for the research-next lines (set by the host; null = none shown).</summary>
    public ResearchContent? Research { get; set; }

    /// <summary>The inspectable regions painted last frame.</summary>
    public IReadOnlyList<InfoHit> InfoHits => _info;

    /// <summary>The top-most inspectable region at a point, or null.</summary>
    public InfoHit? InfoAt(double x, double y) => InfoRegistry.At(_info, x, y);

    private void Info(RectD r, InfoSubject subject, string label)
    {
        if (_infoClip is { } clip)
        {
            if (!r.Intersects(clip)) return;
            r = InfoRegistry.Intersect(r, clip);
        }
        if (r.W > 0.5 && r.H > 0.5) _info.Add(new InfoHit(r, subject, label));
    }

    /// <summary>The research-next line under an unmet research milestone; a click focuses that node in the trees.</summary>
    private double PaintResearchNext(DrawList d, ITextMeasure m, MilestoneLine l, double x, double y, double w)
    {
        if (l.Met || l.NextNodeKey < 0 || l.NextNodeName is not { } next) return y;
        EraTheme t = Theme;
        string text = "research next: " + next + (l.TargetNodeName is { } target && target != next ? ", then " + target : "") + "  [K]";
        // UR sizing: the Secondary role, wrapped (never dropped), indented with the milestone's name.
        double size = Px(TypeRole.Secondary), slot = Slot(TypeRole.Secondary);
        List<string> lines = Wrap(m, text, TypeRole.Secondary, w - Sp(22));
        double lw = 0;
        foreach (string line in lines) lw = Math.Max(lw, FlowText.Width(m, line, TypeRole.Secondary, Scale));
        var r = new RectD(x + Sp(22), y, Math.Min(w - Sp(22), lw + Sp(4)), lines.Count * slot + Sp(2));
        double ly = y;
        foreach (string line in lines)
        {
            d.Write(t, x + Sp(22), ly, line, size, Ink.Knowledge);
            ly += slot;
        }
        d.Line(r.X, r.Bottom - 1, r.Right - Sp(4), r.Bottom - 1, ThemeColor.Alpha(t.Semantic.Knowledge, 0.4), 0.8);
        if (_infoClip is not { } view || view.Contains(r.CenterX, r.CenterY)) _hits.Add(new AgeHitRegion(r, AgeHit.FocusNode, l.NextNodeKey));
        Info(r, InfoSubject.Node(new ResearchNodeId(l.NextNodeKey)), next);
        return r.Bottom + Sp(4);
    }
}
