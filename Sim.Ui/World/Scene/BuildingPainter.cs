using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World.Scene;

/// <summary>
/// Buildings and institutions, drawn IN their settlement's composed sprite at the slot the
/// view assigned (D-038 H3/H4: one composed sprite, parts assembled at draw time, one light,
/// one ground plane). Far: not drawn (the footprint carries the settlement). Mid: the stage's
/// cumulative building PARTS — the composed sprite is the primary treatment at every level of
/// detail, never a ring of icons (H3) — with a small state glyph only when a source reports a
/// state other than operational (H6: glyphs say what is happening). Near: the parts plus a
/// glyph badge — the type's mark, or the specialization's when reported, and the state ring.
/// A multiplicity or a cluster shows as a badge on the one token.
/// </summary>
internal static class BuildingPainter
{
    public static void Paint(SceneContext c, DrawList dl, HitIndex hits)
    {
        if (c.Lod == WorldLod.Far) return;
        var items = new List<(WorldEntityId Order, StructureView? S, ClusterView? K)>();
        foreach (StructureView s in c.View.Structures)
            if (s.Drawable && s.Slot is not null) items.Add((s.Id, s, null));
        foreach (ClusterView k in c.View.Clusters)
            items.Add((k.Members[0], null, k));
        items.Sort((a, b) => b.Order.CompareTo(a.Order));   // descending id: lowest on top

        foreach ((WorldEntityId order, StructureView? s, ClusterView? k) in items)
        {
            string settlement = s?.Report.SettlementKey ?? k!.SettlementKey;
            SettlementView? sv = c.View.Settlement(settlement);
            if (sv is null || !sv.Drawable) continue;
            LotGeometry slot = s?.Slot ?? k!.Slot;
            (double x, double y, double side) = c.Geometry.Slot(settlement, slot, c.Morph.Layout);
            bool ghost = s is not null ? s.Report.Visibility == ReportedVisibility.Remembered : k!.Ghost;
            double alpha = ghost ? 0.45 : 1.0;
            string? ink = s?.PolityInk ?? sv.PolityInk;
            Pad(dl, x, y, side, ink, alpha);

            // The cluster shows its type's BASE stage: it stands for several reports, none of whose
            // stages it may claim.
            VisualType type = s?.Type ?? k!.Type;
            PartPainter.Paint(dl, type.PartsAt(s?.Stage.Index ?? 0), x, y, side, alpha);

            GlyphSpec icon = s?.Icon ?? k!.Icon;
            if (c.Lod == WorldLod.Near)
            {
                double badge = Math.Clamp(side * 0.34, 20, 30);
                dl.Glyph(x - side / 2 + 1, y - side / 2 + 1, badge, icon with { Size = WorldPaint.SizeFor(badge) }, alpha);
            }
            else if (icon.State != GlyphState.Complete)
            {
                const double badge = 16;
                dl.Glyph(x - side / 2 - 2, y - side / 2 - 2, badge, icon with { Size = SizeClass.Px16 }, alpha);
            }

            long times = k is not null ? k.TotalMultiplicity : s!.Report.Multiplicity;
            if (k is not null || times > 1)
            {
                string text = k is not null ? "+" + Morphology.Num(times) : "x" + Morphology.Num(times);
                WorldPaint.Badge(dl, x + side / 2 - 4, y + side / 2 - 4, text, 10.5, Ink.With(ParchmentPalette.InkPrimary, alpha),
                    Ink.With(ParchmentPalette.PaperLight, alpha), Ink.With(ParchmentPalette.InkPrimary, alpha));
            }

            WorldEntityId? target = k is not null ? k.HitTarget : ghost ? null : s!.Id;
            if (target is WorldEntityId t)
                hits.Add(new HitRegion(t, HitIndex.StructurePriority, x - side / 2, y - side / 2, 0, side, side, x, y));
        }
    }

    private static void Pad(DrawList dl, double x, double y, double side, string? inkToken, double alpha)
    {
        Rgba ink = inkToken is string t ? Ink.Token(t) : ParchmentPalette.InkSoft;
        double p = side * 0.96;
        dl.Rect(new RectD(x - p / 2, y - p / 2, p, p), Ink.With(ParchmentPalette.PaperLight, 0.92 * alpha), Ink.With(ink, 0.7 * alpha),
            Math.Max(1.0, side / 40), Math.Max(1.5, side / 14));
    }
}

/// <summary>
/// Building parts — the procedural pieces a visual stage adds (content: morphology.json).
/// Each is a few rectangles and lines in lot-local units, filled from the parchment palette,
/// outlined in ink, with a contact shadow along the one global light (ObjectLight).
/// </summary>
internal static class PartPainter
{
    public static void Paint(DrawList dl, IReadOnlyList<MorphPart> parts, double cx, double cy, double side, double alpha)
    {
        double stroke = Math.Max(0.8, side / 60);
        foreach (MorphPart p in parts)
        {
            var r = new RectD(cx + (p.X - p.W / 2) * side, cy + (p.Y - p.H / 2) * side, p.W * side, p.H * side);
            if (Shadowed(p.Kind))
            {
                int sh = WorldPaint.ShadowPx(Math.Max(r.W, r.H));
                if (p.Kind == "dome")
                    dl.Circle(r.CenterX + sh, r.CenterY + sh, Math.Min(r.W, r.H) / 2, Ink.With(ParchmentPalette.InkPrimary, ObjectLight.ShadowAlpha * alpha));
                else
                    dl.Rect(new RectD(r.X + sh, r.Y + sh, r.W, r.H), Ink.With(ParchmentPalette.InkPrimary, ObjectLight.ShadowAlpha * alpha));
            }
            Rgba outline = Ink.With(ParchmentPalette.InkPrimary, alpha);
            switch (p.Kind)
            {
                case "hall":
                    dl.Rect(r, Ink.With(ParchmentPalette.PaperLight, alpha), outline, stroke);
                    if (r.W >= r.H) dl.Line(r.X + stroke, r.CenterY, r.Right - stroke, r.CenterY, Ink.With(ParchmentPalette.InkSoft, alpha), stroke);
                    else dl.Line(r.CenterX, r.Y + stroke, r.CenterX, r.Bottom - stroke, Ink.With(ParchmentPalette.InkSoft, alpha), stroke);
                    break;
                case "wing":
                    dl.Rect(r, Ink.With(ParchmentPalette.PaperMid, alpha), outline, stroke);
                    break;
                case "block":
                    dl.Rect(r, Ink.With(ParchmentPalette.PaperShade, alpha), outline, stroke);
                    for (int i = 1; i <= 2; i++)
                    {
                        double hx = r.X + r.W * i / 3.0;
                        dl.Line(hx, r.Y + stroke, hx, r.Bottom - stroke, Ink.With(ParchmentPalette.InkSoft, 0.8 * alpha), stroke * 0.8);
                    }
                    break;
                case "tower":
                    dl.Rect(r, Ink.With(ParchmentPalette.InkSoft, alpha), outline, stroke);
                    break;
                case "dome":
                {
                    double rad = Math.Min(r.W, r.H) / 2;
                    dl.Circle(r.CenterX, r.CenterY, rad, Ink.With(ParchmentPalette.PaperLight, alpha), outline, stroke);
                    dl.Circle(r.CenterX, r.CenterY, rad * 0.45, null, Ink.With(ParchmentPalette.InkSoft, alpha), stroke * 0.8);
                    break;
                }
                case "green":
                    dl.Rect(r, Ink.With(ParchmentPalette.FertileGreen, 0.75 * alpha), Ink.With(ParchmentPalette.Verdigris, alpha), stroke * 0.8);
                    break;
                case "cross":
                {
                    // A plain Greek cross in INK — never red (the protected emblem; palette rule).
                    double t = Math.Min(r.W, r.H) / 3;
                    dl.Rect(new RectD(r.CenterX - t / 2, r.Y, t, r.H), Ink.With(ParchmentPalette.InkPrimary, alpha));
                    dl.Rect(new RectD(r.X, r.CenterY - t / 2, r.W, t), Ink.With(ParchmentPalette.InkPrimary, alpha));
                    break;
                }
                case "sawtooth":
                {
                    dl.Rect(r, Ink.With(ParchmentPalette.PaperShade, alpha), outline, stroke);
                    const int teeth = 3;
                    for (int i = 0; i < teeth; i++)
                    {
                        double x0 = r.X + r.W * i / teeth, x1 = r.X + r.W * (i + 1) / teeth;
                        dl.Polygon([(x0, r.Bottom), (x1, r.Y + r.H * 0.25), (x1, r.Bottom)], Ink.With(ParchmentPalette.InkSoft, 0.75 * alpha));
                    }
                    break;
                }
                case "stack":
                    dl.Rect(r, Ink.With(ParchmentPalette.InkPrimary, 0.85 * alpha));
                    break;
                case "yard":
                    dl.Rect(r, Ink.With(ParchmentPalette.PaperMid, 0.45 * alpha));
                    dl.Rect(r, null, Ink.With(ParchmentPalette.InkSoft, alpha), stroke);
                    break;
                case "basin":
                    dl.Rect(r, Ink.With(ParchmentPalette.River, 0.8 * alpha), Ink.With(ParchmentPalette.Sea, alpha), stroke, Math.Min(r.W, r.H) / 5);
                    break;
            }
        }
    }

    private static bool Shadowed(string kind) => kind is not ("green" or "cross" or "yard" or "basin");
}
