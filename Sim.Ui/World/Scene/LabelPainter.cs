using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>Labels and counts, above everything they name: settlement names and visual stage,
/// structure names (Near), agent names, stacked labels for crowded agents, node names.</summary>
internal static class LabelPainter
{
    public static void Paint(SceneContext c, DrawList dl)
    {
        if (c.Options.DrawSettlementLabels)
            foreach (SettlementView s in c.View.Settlements)
            {
                if (!s.Drawable) continue;
                (double x, double y) = c.Geometry.Settlement(s.Key);
                double below = Math.Max(c.Geometry.SpriteRadiusPx(s.Key), c.Options.CoreMinPx) + 6;
                double alpha = s.Report.Visibility == ReportedVisibility.Remembered ? 0.5 : 1.0;
                double size = c.Lod == WorldLod.Far ? 12 : 14;
                WorldPaint.Plate(dl, x, y + below, s.Report.DisplayName, size, ParchmentPalette.InkPrimary, TextAlign.Center, FontRole.Heading, alpha);
                if (c.Lod != WorldLod.Far)
                    WorldPaint.Plate(dl, x, y + below + size + 4, s.Stage.Name + (c.View.IsPlaceholder ? " (visual)" : ""), 11,
                        ParchmentPalette.InkSoft, TextAlign.Center, FontRole.Body, alpha);
            }

        if (c.Lod == WorldLod.Near)
            foreach (StructureView s in c.View.Structures)
            {
                if (!s.Drawable || s.Slot is not LotGeometry slot) continue;
                (double x, double y, double side) = c.Geometry.Slot(s.Report.SettlementKey, slot, c.Morph.Layout);
                if (side < 44) continue;
                string text = Ink.Fit(c.Measure, s.Report.DisplayName, 10.5, side + 4);
                WorldPaint.Plate(dl, x, y + side / 2 + 1, text, 10.5, ParchmentPalette.InkPrimary, TextAlign.Center, FontRole.Body,
                    s.Report.Visibility == ReportedVisibility.Remembered ? 0.5 : 1.0);
            }

        foreach (AgentView a in c.View.Agents)
        {
            if (!a.Drawable || !c.Geometry.TryAgent(a.Id, out AgentPlacement p)) continue;
            double alpha = a.Report.Visibility == ReportedVisibility.Remembered ? 0.5 : 1.0;
            string suffix = a.Report.Visibility == ReportedVisibility.Remembered ? " (last known)" : "";
            if (p.Stacked)
            {
                dl.Rect(p.Label, Ink.With(ParchmentPalette.PaperLight, 0.9 * alpha), Ink.With(ParchmentPalette.InkSoft, 0.8 * alpha), 0.8, 2);
                dl.Text(p.Label.X + 6, p.Label.Y + 1.5, SceneGeometry.LabelText(a) + suffix, 12, Ink.With(ParchmentPalette.InkPrimary, alpha));
            }
            else if (c.Lod != WorldLod.Far || a.Anchor == AgentAnchor.Settlement)
            {
                WorldPaint.Plate(dl, p.X, p.Y + p.SizePx * 0.55 + 3, a.Report.DisplayName + suffix, 11.5, ParchmentPalette.InkPrimary,
                    TextAlign.Center, FontRole.Body, alpha);
            }
        }

        if (c.Lod != WorldLod.Far && c.Options.DrawInfrastructure)
            foreach (NodeView n in c.View.Nodes)
            {
                if (!n.Drawable || n.Type?.Glyph is null) continue;
                (double x, double y) = c.Proj.ToScreen(n.Report.Position);
                WorldPaint.Plate(dl, x, y + 15, n.Report.DisplayName, 10.5, ParchmentPalette.InkSoft, TextAlign.Center, FontRole.Body);
            }
    }
}

/// <summary>Selection and hover: a gold ring on what is selected, a soft ring on what is hovered
/// — drawn at the entity's composed position at every level of detail (a structure too small
/// to draw is ringed through its settlement; a collapsed one through its cluster token).</summary>
internal static class SelectionPainter
{
    public static void Paint(SceneContext c, DrawList dl)
    {
        if (c.Ui.MirrorSettlementKey is string mirror && c.View.Settlement(mirror) is SettlementView ms && ms.Drawable)
            Ring(c, dl, ms.Id, ParchmentPalette.GoldLeaf, 2.5, false);
        if (c.Ui.Hovered is WorldEntityId h && h != c.Ui.Selected) Ring(c, dl, h, ParchmentPalette.InkSoft, 1.5, false);
        if (c.Ui.Selected is WorldEntityId s) Ring(c, dl, s, ParchmentPalette.GoldLeaf, 2.8, true);
    }

    private static void Ring(SceneContext c, DrawList dl, WorldEntityId id, ParchmentPalette.Rgba color, double width, bool strong)
    {
        switch (id.Kind)
        {
            case WorldEntityKind.Settlement when c.View.FindSettlement(id) is SettlementView s && s.Drawable:
            {
                (double x, double y) = c.Geometry.Settlement(s.Key);
                double core = Math.Max(c.Morph.Layout.PlazaRadius * c.PxPerUnit, c.Options.CoreMinPx);
                dl.Circle(x, y, core + 5, null, color, width);
                if (strong) dl.Circle(x, y, c.Geometry.SpriteRadiusPx(s.Key) + 8, null, Ink.With(color, 0.8), 1.2);
                break;
            }
            case WorldEntityKind.Structure when c.View.FindStructure(id) is StructureView st && st.Drawable:
            {
                LotGeometry? slot = st.Slot;
                if (slot is null && st.ClusterKey is string ck)
                    foreach (ClusterView k in c.View.Clusters) if (k.Key == ck) { slot = k.Slot; break; }
                if (c.Lod == WorldLod.Far || slot is not LotGeometry g)
                {
                    (double x, double y) = c.Geometry.Settlement(st.Report.SettlementKey);
                    dl.Circle(x, y, c.Geometry.SpriteRadiusPx(st.Report.SettlementKey) + 6, null, color, width);
                    break;
                }
                (double sx, double sy, double side) = c.Geometry.Slot(st.Report.SettlementKey, g, c.Morph.Layout);
                dl.Rect(new RectD(sx - side / 2 - 3, sy - side / 2 - 3, side + 6, side + 6), null, color, width, 4);
                break;
            }
            case WorldEntityKind.Agent when c.Geometry.TryAgent(id, out AgentPlacement p):
                dl.Circle(p.X, p.Y, p.SizePx * 0.52 + 5, null, color, width);
                if (strong && p.Stacked) dl.Rect(p.Label, null, color, 1.5, 2);
                break;
            case WorldEntityKind.Resource when c.View.FindResource(id) is ResourceView r && r.Drawable:
            {
                int n = 0;
                foreach (ResourceView o in c.View.Resources) if (o.Drawable && o.Report.SettlementKey == r.Report.SettlementKey) n++;
                (double x, double y) = ResourcePainter.Position(c, r, n);
                dl.Circle(x, y, ResourcePainter.SizePx * 0.55 + 4, null, color, width);
                break;
            }
            case WorldEntityKind.InfraNode when c.View.FindNode(id) is NodeView nv && nv.Drawable:
            {
                (double x, double y) = c.Proj.ToScreen(nv.Report.Position);
                dl.Circle(x, y, 16, null, color, width);
                break;
            }
            case WorldEntityKind.InfraEdge when c.View.FindEdge(id) is EdgeView e && e.Drawable:
            {
                (double x0, double y0, double x1, double y1) = InfrastructurePainter.Trimmed(c, e);
                if (!double.IsNaN(x0)) dl.Line(x0, y0, x1, y1, Ink.With(color, 0.6), e.Stage.WidthPx + 5);
                break;
            }
        }
    }
}
