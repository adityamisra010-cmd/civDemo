using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World.Scene;

/// <summary>The one infrastructure graph (D-009): typed edges as strokes, typed nodes as marks.</summary>
internal static class InfrastructurePainter
{
    /// <summary>An edge's screen segment, cut back at either end by the sprite of the settlement
    /// its node belongs to, so a road meets a city at its edge instead of crossing its blocks.
    /// Visual only: the reported nodes do not move. NaN when nothing is left to draw.</summary>
    public static (double X0, double Y0, double X1, double Y1) Trimmed(SceneContext c, EdgeView e)
    {
        (double x0, double y0) = c.Proj.ToScreen(e.A);
        (double x1, double y1) = c.Proj.ToScreen(e.B);
        double dx = x1 - x0, dy = y1 - y0, len = Math.Sqrt(dx * dx + dy * dy);
        if (len <= 0) return (double.NaN, 0, 0, 0);
        double cutA = Cut(c, e.Report.NodeA), cutB = Cut(c, e.Report.NodeB);
        if (cutA + cutB >= len) return (double.NaN, 0, 0, 0);
        return (x0 + dx / len * cutA, y0 + dy / len * cutA, x1 - dx / len * cutB, y1 - dy / len * cutB);
    }

    private static double Cut(SceneContext c, string nodeKey)
    {
        NodeView? n = c.View.FindNode(new WorldEntityId(WorldEntityKind.InfraNode, nodeKey));
        if (n?.Report.SettlementKey is not string sk || c.View.Settlement(sk) is not SettlementView s || !s.Drawable) return 0;
        if (n.Type?.Glyph is not null) return 0;   // a port or a station is its own mark: meet it
        return c.Lod < c.Options.FootprintMinLod ? 0 : c.Geometry.SpriteRadiusPx(sk) + 2;
    }

    public static void Paint(SceneContext c, DrawList dl, HitIndex hits)
    {
        if (!c.Options.DrawInfrastructure) return;
        for (int i = c.View.Edges.Count - 1; i >= 0; i--)
        {
            EdgeView e = c.View.Edges[i];
            if (!e.Drawable) continue;
            double alpha = e.Report.Visibility == ReportedVisibility.Remembered ? 0.4 : 1.0;
            (double x0, double y0, double x1, double y1) = Trimmed(c, e);
            if (double.IsNaN(x0)) continue;
            double w = e.Stage.WidthPx;
            (double, double)? dash = e.Stage.Stroke switch
            {
                StrokeKind.Dashed => (6.0 * w / 2, 4.0 * w / 2),
                StrokeKind.Dotted => (1.4 * w, 3.2 * w),
                _ => null,
            };
            if (e.Stage.Stroke == StrokeKind.Solid && w >= 2.5)
                dl.Line(x0, y0, x1, y1, Ink.With(ParchmentPalette.PaperLight, 0.7 * alpha), w + 2);   // a casing reads as a made road
            dl.Line(x0, y0, x1, y1, Ink.With(Ink.Token(e.Stage.Ink), alpha), w, dash);
            if (alpha >= 1.0)
                hits.Add(new HitRegion(e.Id, HitIndex.EdgePriority, (x0 + x1) / 2, (y0 + y1) / 2, 7, 0, 0, (x0 + x1) / 2, (y0 + y1) / 2));
        }
        for (int i = c.View.Nodes.Count - 1; i >= 0; i--)
        {
            NodeView n = c.View.Nodes[i];
            if (!n.Drawable) continue;
            (double x, double y) = c.Proj.ToScreen(n.Report.Position);
            double alpha = n.Report.Visibility == ReportedVisibility.Remembered ? 0.4 : 1.0;
            if (n.Type?.Glyph is WorldGlyph g)
            {
                double size = c.Lod == WorldLod.Far ? 18 : 24;
                dl.Circle(x, y, size * 0.55, Ink.With(ParchmentPalette.PaperLight, 0.9 * alpha), Ink.With(ParchmentPalette.InkSoft, alpha), 1);
                dl.Glyph(x - size / 2, y - size / 2, size,
                    new GlyphSpec(g.Base, GlyphState.Complete, WorldPaint.SizeFor(size), g.Mark, Placement: Placement.Map), alpha);
                if (alpha >= 1.0) hits.Add(new HitRegion(n.Id, HitIndex.NodePriority, x, y, size * 0.6, 0, 0, x, y));
            }
            else if (c.Lod == WorldLod.Near)
            {
                dl.Circle(x, y, 2.5, Ink.With(ParchmentPalette.InkSoft, alpha));
            }
        }
    }
}

/// <summary>Resources at a settlement: a row of small heaps under its sprite (Mid and Near).</summary>
internal static class ResourcePainter
{
    public const double SizePx = 22;

    public static void Paint(SceneContext c, DrawList dl, HitIndex hits)
    {
        if (c.Lod == WorldLod.Far) return;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ResourceView r in c.View.Resources)
            if (r.Drawable) counts[r.Report.SettlementKey] = (counts.TryGetValue(r.Report.SettlementKey, out int n) ? n : 0) + 1;
        for (int i = c.View.Resources.Count - 1; i >= 0; i--)
        {
            ResourceView r = c.View.Resources[i];
            if (!r.Drawable) continue;
            SettlementView? s = c.View.Settlement(r.Report.SettlementKey);
            if (s is null || !s.Drawable) continue;
            (double x, double y) = Position(c, r, counts[r.Report.SettlementKey]);
            double alpha = r.Report.Visibility == ReportedVisibility.Remembered ? 0.4 : 1.0;
            dl.Circle(x, y, SizePx * 0.55, Ink.With(ParchmentPalette.PaperLight, 0.9 * alpha), Ink.With(ParchmentPalette.Verdigris, alpha), 1);
            dl.Glyph(x - SizePx / 2, y - SizePx / 2, SizePx,
                new GlyphSpec(GlyphBase.Heap, GlyphState.Complete, SizeClass.Px24, r.Type?.Mark ?? GlyphDomain.None, Placement: Placement.Map), alpha);
            if (alpha >= 1.0) hits.Add(new HitRegion(r.Id, HitIndex.ResourcePriority, x, y, SizePx * 0.55, 0, 0, x, y));
        }
    }

    /// <summary>The i-th of n resources: centred in a row just below the settlement's sprite.</summary>
    public static (double X, double Y) Position(SceneContext c, ResourceView r, int count)
    {
        (double cx, double cy) = c.Geometry.Settlement(r.Report.SettlementKey);
        double pitch = SizePx + 3;
        double below = c.Geometry.SpriteRadiusPx(r.Report.SettlementKey) + SizePx * 0.9 + (c.Options.DrawSettlementLabels ? 30 : 6);
        return (cx + (r.Index - (count - 1) / 2.0) * pitch, cy + below);
    }
}

/// <summary>
/// Mobile agents: ONE token per reported entity, whatever it stands for — a 500-strong army is
/// one banner with "500" above it, never five hundred marks. The token is the category's
/// silhouette (banner / emblem / circled mark) in the owner's ink ring, with an optional
/// heading tick. Nothing here moves anything: positions are reported, not animated.
/// </summary>
internal static class AgentPainter
{
    public static void Paint(SceneContext c, DrawList dl, HitIndex hits)
    {
        for (int i = c.View.Agents.Count - 1; i >= 0; i--)   // descending id: lowest on top
        {
            AgentView a = c.View.Agents[i];
            if (!a.Drawable || !c.Geometry.TryAgent(a.Id, out AgentPlacement p)) continue;
            bool ghost = a.Report.Visibility == ReportedVisibility.Remembered;
            double alpha = ghost ? 0.4 : 1.0;
            Rgba ink = a.PolityInk is string t ? Ink.Token(t) : ParchmentPalette.InkSoft;
            double r = p.SizePx * 0.52;

            if (a.Anchor == AgentAnchor.Settlement)
            {
                (double sx, double sy) = c.Proj.ToScreen(a.World);
                dl.Line(sx, sy, p.X, p.Y, Ink.With(ParchmentPalette.InkSoft, 0.7 * alpha), 1.0, (3.0, 3.0));
            }
            int sh = WorldPaint.ShadowPx(p.SizePx);
            dl.Circle(p.X + sh, p.Y + sh, r, Ink.With(ParchmentPalette.InkPrimary, ObjectLight.ShadowAlpha * alpha));
            dl.Circle(p.X, p.Y, r, Ink.With(ink, 0.92 * alpha));
            dl.Circle(p.X, p.Y, r * 0.8, Ink.With(ParchmentPalette.PaperLight, alpha));
            if (ghost) dl.Circle(p.X, p.Y, r + 3, null, Ink.With(ParchmentPalette.InkSoft, 0.8), 1.0);
            double g = p.SizePx * 0.84;
            var spec = new GlyphSpec(a.Type.Glyph.Base, GlyphState.Complete, WorldPaint.SizeFor(g), a.Type.Glyph.Mark, Placement: Placement.Map);
            dl.Glyph(p.X - g / 2, p.Y - g / 2, g, spec, alpha);

            if (a.HeadingDeg is double h)
            {
                double rad = h * Math.PI / 180.0;
                double sn = Math.Sin(rad), cs = Math.Cos(rad);
                dl.Line(p.X + sn * r, p.Y - cs * r, p.X + sn * (r + 9), p.Y - cs * (r + 9), Ink.With(ink, alpha), 2.2);
            }
            if (a.CountLabel is string count && !p.Stacked)
                WorldPaint.Badge(dl, p.X, p.Y - r - 9, count, 11, Ink.With(ParchmentPalette.PaperLight, alpha),
                    Ink.With(ParchmentPalette.InkPrimary, alpha), Ink.With(ParchmentPalette.InkSoft, alpha));

            if (!ghost)
            {
                hits.Add(new HitRegion(a.Id, HitIndex.AgentPriority, p.X, p.Y, r + 2, 0, 0, p.X, p.Y));
                if (p.Stacked)
                    hits.Add(new HitRegion(a.Id, HitIndex.AgentPriority, p.Label.X, p.Label.Y, 0, p.Label.W, p.Label.H, p.X, p.Y));
            }
        }
    }
}
