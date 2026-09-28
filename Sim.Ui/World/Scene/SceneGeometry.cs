using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>An agent token's screen placement and, when it shares a cluster, its stacked label.</summary>
public readonly record struct AgentPlacement(double X, double Y, double SizePx, bool Stacked, RectD Label, int ClusterSize);

/// <summary>
/// THE SCREEN GEOMETRY of one frame, computed ONCE and read by every painter and by the hit
/// index, so what is drawn and what is clickable cannot disagree. Pure: view + content +
/// projection in, positions out.
///
/// Agent decluttering (docs §8): tokens stay at their true positions — overlap is allowed —
/// and only LABELS stack. Clusters are the connected components of "within R screen pixels",
/// measured on WORLD deltas × zoom (so a pan never regroups), over every drawable agent
/// before any viewport cull; a cluster's labels stack beside its lowest-id member in id order.
/// Settlement-attached agents fan beside their settlement at a constant screen offset.
/// </summary>
public sealed class SceneGeometry
{
    public const double DeclutterRadiusPx = 30.0;
    public const double LabelPitchPx = 17.0;
    public const double AttachRadiusPx = 34.0;

    private readonly Dictionary<string, (double X, double Y)> _settlement = new(StringComparer.Ordinal);
    private readonly Dictionary<WorldEntityId, AgentPlacement> _agents = new();
    private readonly Dictionary<string, double> _radius = new(StringComparer.Ordinal);

    public SceneGeometry(WorldView view, WorldMorphology morph, WorldProjection proj, WorldLod lod, double pxPerUnit)
    {
        PxPerUnit = pxPerUnit;
        foreach (SettlementView s in view.Settlements)
        {
            _settlement[s.Key] = proj.ToScreen(s.Report.Position);
            double r = morph.Layout.PlazaRadius;
            foreach (LotGeometry b in s.Blocks) r = Math.Max(r, Math.Sqrt(b.X * b.X + b.Y * b.Y) + morph.Layout.BlockSize * 0.71);
            _radius[s.Key] = r;
        }
        foreach (StructureView st in view.Structures)
            if (st.Drawable && st.Slot is LotGeometry g && _radius.TryGetValue(st.Report.SettlementKey, out double r0))
                _radius[st.Report.SettlementKey] = Math.Max(r0, Math.Sqrt(g.X * g.X + g.Y * g.Y) + morph.Layout.SlotSize * 0.71);
        foreach (ClusterView c in view.Clusters)
            if (_radius.TryGetValue(c.SettlementKey, out double r1))
                _radius[c.SettlementKey] = Math.Max(r1, Math.Sqrt(c.Slot.X * c.Slot.X + c.Slot.Y * c.Slot.Y) + morph.Layout.SlotSize * 0.71);

        double token = TokenPx(lod);
        // Positioned agents (reported position or graph location): cluster by world delta × zoom.
        var free = new List<AgentView>();
        foreach (AgentView a in view.Agents)
            if (a.Drawable && a.Anchor is AgentAnchor.Position or AgentAnchor.Graph) free.Add(a);
        free.Sort((x, y) => x.Id.CompareTo(y.Id));
        int[] comp = Components(free, proj.PxPerWorldUnit);
        for (int i = 0; i < free.Count; i++)
        {
            AgentView a = free[i];
            (double sx, double sy) = proj.ToScreen(a.World);
            double size = token * morph.SizeScale(a.Report.Count);
            int members = 0, rank = 0, anchor = -1;
            for (int j = 0; j < free.Count; j++)
            {
                if (comp[j] != comp[i]) continue;
                if (anchor < 0) anchor = j;
                if (j < i) rank++;
                members++;
            }
            RectD label = default;
            if (members > 1)
            {
                (double ax, double ay) = proj.ToScreen(free[anchor].World);
                string text = LabelText(a);
                // Hit rects are sized with the headless measure in EVERY backend, so the game and
                // the screenshots agree on what is clickable (the real font sizes plates only).
                double w = ApproxTextMeasure.Instance.Width(text, 12, FontRole.Body) + 12;
                label = new RectD(ax + token * 0.75, ay - token * 0.6 + rank * LabelPitchPx, w, LabelPitchPx - 2);
            }
            _agents[a.Id] = new AgentPlacement(sx, sy, size, members > 1, label, members);
        }
        // Settlement-attached agents: a fixed fan beside the settlement, outside its click target.
        foreach (AgentView a in view.Agents)
        {
            if (!a.Drawable || a.Anchor != AgentAnchor.Settlement) continue;
            (double sx, double sy) = proj.ToScreen(a.World);
            // Outside the settlement's sprite and its click target, at a fixed angle per index.
            double deg = -60.0 + (a.AttachIndex % 7) * 24.0;
            double r = Math.Max(AttachRadiusPx, SpriteRadiusPx(a.Report.AttachedSettlementKey!) + token * 0.7) + 30.0 * (a.AttachIndex / 7);
            double rad = deg * Math.PI / 180.0;
            _agents[a.Id] = new AgentPlacement(sx + r * Math.Sin(rad), sy - r * Math.Cos(rad), token * 0.8, false, default, 1);
        }
    }

    public double PxPerUnit { get; }

    public static double TokenPx(WorldLod lod) => lod switch { WorldLod.Near => 40, WorldLod.Mid => 32, _ => 26 };

    /// <summary>The composed sprite's radius in screen pixels (what it currently shows).</summary>
    public double SpriteRadiusPx(string key) => (_radius.TryGetValue(key, out double r) ? r : 0) * PxPerUnit;

    public (double X, double Y) Settlement(string key) => _settlement.TryGetValue(key, out var p) ? p : (double.NaN, double.NaN);

    public bool TryAgent(WorldEntityId id, out AgentPlacement p) => _agents.TryGetValue(id, out p);

    /// <summary>A slot's screen centre and side, inside its settlement's sprite.</summary>
    public (double X, double Y, double Side) Slot(string settlementKey, LotGeometry slot, CompositionLayout layout)
    {
        (double cx, double cy) = Settlement(settlementKey);
        return (cx + slot.X * PxPerUnit, cy + slot.Y * PxPerUnit, layout.SlotSize * PxPerUnit);
    }

    public static string LabelText(AgentView a) =>
        a.CountLabel is string c ? c + "  " + a.Report.DisplayName : a.Report.DisplayName;

    /// <summary>Connected components (union-find, deterministic) of "within R pixels", using
    /// world deltas × px-per-world-unit; component id = the lowest index in it.</summary>
    private static int[] Components(List<AgentView> agents, double zoom)
    {
        int n = agents.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        double r2 = DeclutterRadiusPx * DeclutterRadiusPx;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double dx = (agents[i].World.X - agents[j].World.X) * zoom, dy = (agents[i].World.Y - agents[j].World.Y) * zoom;
                if (dx * dx + dy * dy > r2) continue;
                int a = Find(i), b = Find(j);
                if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
            }
        var comp = new int[n];
        for (int i = 0; i < n; i++) comp[i] = Find(i);
        return comp;
    }
}
