using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World.Scene;

/// <summary>Background (demo/preview only): the paper, a faint survey grid, the world's edge.</summary>
internal static class BackgroundPainter
{
    public static void Paint(SceneContext c, DrawList dl, WorldBackdrop backdrop)
    {
        double worldW = backdrop.Width, worldH = backdrop.Height;
        dl.Rect(new RectD(0, 0, c.Proj.ViewW, c.Proj.ViewH), ParchmentPalette.PaperShade);
        (double x0, double y0) = c.Proj.ToScreen(new WorldPoint(0, 0));
        (double x1, double y1) = c.Proj.ToScreen(new WorldPoint(worldW, worldH));
        dl.Rect(new RectD(x0, y0, x1 - x0, y1 - y0), ParchmentPalette.PaperLight, Ink.With(ParchmentPalette.InkSoft, 0.55), 1.2);
        foreach (WorldRect w in backdrop.Water)
        {
            (double wx0, double wy0) = c.Proj.ToScreen(new WorldPoint(w.X, w.Y));
            (double wx1, double wy1) = c.Proj.ToScreen(new WorldPoint(w.X + w.W, w.Y + w.H));
            dl.Rect(new RectD(wx0, wy0, wx1 - wx0, wy1 - wy0), Ink.With(ParchmentPalette.Sea, 0.55));
            dl.Line(wx0, wy0, wx1, wy0, Ink.With(ParchmentPalette.DeepSea, 0.7), 1.2);
        }
        double step = GridStep(c.Proj.PxPerWorldUnit);
        Rgba grid = Ink.With(ParchmentPalette.PaperShade, 0.45);
        for (double gx = step; gx < worldW; gx += step)
        {
            (double sx, _) = c.Proj.ToScreen(new WorldPoint(gx, 0));
            if (sx >= 0 && sx <= c.Proj.ViewW) dl.Line(sx, Math.Max(0, y0), sx, Math.Min(c.Proj.ViewH, y1), grid, 0.8);
        }
        for (double gy = step; gy < worldH; gy += step)
        {
            (_, double sy) = c.Proj.ToScreen(new WorldPoint(0, gy));
            if (sy >= 0 && sy <= c.Proj.ViewH) dl.Line(Math.Max(0, x0), sy, Math.Min(c.Proj.ViewW, x1), sy, grid, 0.8);
        }
    }

    /// <summary>A grid pitch of 10, 50 or 100 world units, whichever keeps lines ≥ 60 px apart.</summary>
    public static double GridStep(double pxPerWorldUnit) =>
        pxPerWorldUnit * 10 >= 60 ? 10 : pxPerWorldUnit * 50 >= 60 ? 50 : 100;
}

/// <summary>
/// The settlement footprint: ONE composed sprite per settlement (D-038 H3) — a faint wash in
/// the controller's ink, the residential blocks its stage shows, and the civic core. Blocks
/// come from the view's fixed lattice; this painter only projects them.
/// </summary>
internal static class FootprintPainter
{
    public static void Paint(SceneContext c, DrawList dl, HitIndex hits)
    {
        if (c.Lod < c.Options.FootprintMinLod) return;
        CompositionLayout layout = c.Morph.Layout;
        for (int i = c.View.Settlements.Count - 1; i >= 0; i--)   // descending id: lowest on top
        {
            SettlementView s = c.View.Settlements[i];
            if (!s.Drawable) continue;
            (double cx, double cy) = c.Geometry.Settlement(s.Key);
            double alpha = s.Report.Visibility == ReportedVisibility.Remembered ? 0.45 : 1.0;
            Rgba ink = s.PolityInk is string t ? Ink.Token(t) : ParchmentPalette.InkSoft;
            double radius = c.Geometry.SpriteRadiusPx(s.Key);
            dl.Circle(cx, cy, radius + 3, Ink.With(ink, 0.10 * alpha), Ink.With(ink, 0.35 * alpha), 1.0);

            double side = layout.BlockSize * c.PxPerUnit;
            int shadow = WorldPaint.ShadowPx(side);
            foreach (LotGeometry b in s.Blocks)
            {
                double bx = cx + b.X * c.PxPerUnit - side / 2, by = cy + b.Y * c.PxPerUnit - side / 2;
                if (side < 3.0)
                {
                    dl.Rect(new RectD(bx, by, side, side), Ink.With(ParchmentPalette.InkSoft, 0.7 * alpha));
                    continue;
                }
                dl.Rect(new RectD(bx + shadow, by + shadow, side, side), Ink.With(ParchmentPalette.InkPrimary, ObjectLight.ShadowAlpha * alpha));
                dl.Rect(new RectD(bx, by, side, side), Ink.With(ParchmentPalette.PaperMid, alpha), Ink.With(ParchmentPalette.InkSoft, alpha),
                    Math.Max(0.6, side / 18));
                if (side >= 9) dl.Line(bx + 1.5, by + side / 2, bx + side - 1.5, by + side / 2, Ink.With(ParchmentPalette.InkSoft, 0.55 * alpha), 0.7);
            }

            double core = Math.Max(layout.PlazaRadius * c.PxPerUnit, c.Options.CoreMinPx);
            dl.Circle(cx + shadow, cy + shadow, core, Ink.With(ParchmentPalette.InkPrimary, ObjectLight.ShadowAlpha * alpha));
            dl.Circle(cx, cy, core, Ink.With(ParchmentPalette.PaperLight, alpha), Ink.With(ink, alpha), Math.Max(1.5, core / 5));
            dl.Circle(cx, cy, Math.Max(1.5, core * 0.35), Ink.With(ink, alpha));

            if (c.Options.HitSettlements && s.Report.Visibility != ReportedVisibility.Remembered)
                hits.Add(new HitRegion(s.Id, HitIndex.SettlementPriority, cx, cy, Math.Max(22.0, core + 4), 0, 0, cx, cy));
        }
    }
}

/// <summary>Small drawing helpers shared by the world painters.</summary>
internal static class WorldPaint
{
    /// <summary>Contact-shadow offset for a drawn box of <paramref name="sidePx"/>: one pixel per
    /// sixteen, at least one — the rule ObjectLight.ShadowOffset states for glyphs, along the
    /// same light (equal x and y components, down-right). One light for glyphs and parts.</summary>
    public static int ShadowPx(double sidePx) => Math.Max(1, (int)(sidePx / 16.0));

    /// <summary>The glyph size class to bake for a drawn size (the bake is scaled to it).</summary>
    public static SizeClass SizeFor(double px) => px >= 40 ? SizeClass.Px48 : px >= 28 ? SizeClass.Px32 : px >= 20 ? SizeClass.Px24 : SizeClass.Px16;

    public static void Badge(DrawList dl, double cx, double cy, string text, double size, Rgba fill, Rgba ink, Rgba frame)
    {
        double w = ApproxTextMeasure.Instance.Width(text, size, FontRole.Numeric) + 8;
        double h = size + 5;
        dl.Rect(new RectD(cx - w / 2, cy - h / 2, w, h), fill, frame, 0.9, 3);
        dl.Text(cx, cy - size * 0.62, text, size, ink, TextAlign.Center, FontRole.Numeric);
    }

    /// <summary>Text on a translucent paper plate (legible over any footprint).</summary>
    public static void Plate(DrawList dl, double x, double y, string text, double size, Rgba ink, TextAlign align, FontRole role, double alpha = 1.0)
    {
        double w = ApproxTextMeasure.Instance.Width(text, size, role) + 8;
        double left = align switch { TextAlign.Center => x - w / 2, TextAlign.Right => x - w, _ => x - 4 };
        dl.Rect(new RectD(left, y - 1, w, size + 4), Ink.With(ParchmentPalette.PaperLight, 0.78 * alpha), null, 0, 2);
        dl.Text(x, y, text, size, Ink.With(ink, alpha), align, role);
    }
}
