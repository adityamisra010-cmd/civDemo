namespace Sim.Ui.Art.Glyphs;

/// <summary>
/// THE COMPOSER: one glyph from one <see cref="GlyphSpec"/>, in the composition
/// order of docs/architecture/pre-m5-visual-system.md §2.8 —
///
///   shadow (Map only) → maturity wash → base outline in the register's
///   vocabulary → domain mark → state overlay (hatch / progress arc / gaps) →
///   badges — with the state ring around it all.
///
/// A pure, total function of the spec: same spec, same bytes. It reads nothing
/// but the spec (no world, no clock, no RNG), which is the read-only rule of §0
/// made structural — and pinned by the reflection test in GlyphGrammarTests.
/// Every visible pixel is one of ParchmentPalette.BibleColors (InkCanvas).
/// </summary>
public static class GlyphBaker
{
    /// <summary>Bake the glyph for <paramref name="spec"/> (normalised first — the
    /// LOD contract of GlyphSpec.Normalize). Output is a straight-alpha RGBA box of
    /// <c>spec.Pixels</c> a side over transparency.</summary>
    public static ArtImage Bake(GlyphSpec spec)
    {
        GlyphSpec s = spec.Normalize();
        var canvas = new InkCanvas(s.Pixels);
        if (s.Placement == Placement.Map)
        {
            // The contact shadow is the glyph's own alpha, offset along the one
            // global light (ObjectLight). Bake the flat glyph, take its alpha.
            var flat = new InkCanvas(s.Pixels);
            Draw(flat, s);
            (int dx, int dy) = ObjectLight.ShadowOffset(s.Size);
            canvas.ShadowFrom(flat, dx, dy);
        }
        Draw(canvas, s);
        return canvas.Compose();
    }

    // --- layout --------------------------------------------------------------

    private readonly record struct Frame(
        int S, double Hair, double Heavy, double C, double R, double BoxX, double BoxY, double Side)
    {
        public (double X, double Y) P(double u, double v) => (BoxX + u * Side, BoxY + v * Side);
        public double Len(double f) => f * Side;
    }

    private static Frame Layout(int size)
    {
        double hair = System.Math.Max(1.0, size / 16.0);
        double heavy = 1.6 * hair;
        double c = size / 2.0;
        double r = c - hair;                       // ring radius; heavy ring stays inside the box
        double inner = r - heavy / 2.0 - 0.5 * hair; // clear of the heaviest ring
        double side = 2.0 * inner * 0.78;          // silhouette box (corners clear the ring)
        return new Frame(size, hair, heavy, c, r, c - side / 2.0, c - side / 2.0, side);
    }

    // --- the composition -----------------------------------------------------

    private static void Draw(InkCanvas cv, GlyphSpec s)
    {
        Frame f = Layout(s.Pixels);
        bool registers = s.Pixels >= (int)SizeClass.Px32;
        bool badges = s.Pixels >= (int)SizeClass.Px48;

        double weight = registers ? f.Hair * LineWeight(s.Era) : f.Hair;
        InkCanvas.Layer outline = s.State == GlyphState.Locked ? InkCanvas.Layer.Ghost : InkCanvas.Layer.Primary;
        (double On, double Off)? outlineDash = s.State == GlyphState.Decayed ? (4.0 * f.Hair, 2.0 * f.Hair) : null;

        double washFraction = s.State switch
        {
            GlyphState.Locked => 0.0,
            GlyphState.Available => 0.0,
            GlyphState.InProgress => s.Progress,
            _ => s.Maturity,
        };

        // 1. Maturity wash: the base's fill region, rising from the baseline.
        Func<double, double, bool>? washClip = null;
        if (washFraction > 0.0)
        {
            double top = f.BoxY + f.Side * (1.0 - washFraction);
            washClip = (x, y) => y >= top;
        }

        // 2. Base fill region + outline in the register's vocabulary.
        Func<double, double, bool> region = DrawBase(cv, f, s, outline, weight, outlineDash, washClip, registers);

        // 3. Domain mark.
        if (s.Domain != GlyphDomain.None && s.Base != GlyphBase.Link)
            DrawMark(cv, f, s.Base, s.Domain);

        // 4. State overlay: scaffold hatch for work in progress.
        if (s.State == GlyphState.InProgress)
        {
            double pitch = System.Math.Max(2.0, s.Pixels / 8.0);
            cv.Hatch(InkCanvas.Layer.Hatch, f.BoxX, f.BoxY, f.BoxX + f.Side, f.BoxY + f.Side, pitch, region);
        }

        // 5. The state ring.
        DrawRing(cv, f, s);

        // 6. Badges (48 px only).
        if (badges)
        {
            if (s.Base == GlyphBase.Formation && s.Veterancy != Veterancy.Recruit)
                DrawChevrons(cv, f, s.Veterancy);
            if (washFraction >= 0.75 && s.State != GlyphState.InProgress)
                cv.Stroke(InkCanvas.Layer.Gold, [f.P(0.1, 0.985), f.P(0.9, 0.985)], f.Hair * 0.8);
        }
    }

    private static double LineWeight(EraRegister era) => era switch
    {
        EraRegister.Industrial => 1.25,
        EraRegister.Modern => 1.5,
        _ => 1.0,
    };

    // --- the state ring ------------------------------------------------------

    private static void DrawRing(InkCanvas cv, Frame f, GlyphSpec s)
    {
        var primary = InkCanvas.Layer.Primary;
        (double, double)? locked = (24.0, 12.0);
        switch (s.State)
        {
            case GlyphState.Locked:
                cv.Arc(primary, f.C, f.C, f.R, f.Hair, dashDeg: locked);
                break;
            case GlyphState.Available:
                cv.Arc(primary, f.C, f.C, f.R, f.Hair);
                break;
            case GlyphState.InProgress:
                cv.Arc(primary, f.C, f.C, f.R, f.Hair);
                cv.Arc(primary, f.C, f.C, f.R, f.Heavy, 0.0, 360.0 * s.Progress);
                break;
            case GlyphState.Complete:
                cv.Arc(primary, f.C, f.C, f.R, f.Heavy);
                break;
            case GlyphState.Stalled:
                cv.Arc(primary, f.C, f.C, f.R, f.Hair, dashDeg: locked);
                cv.Arc(primary, f.C, f.C, f.R, f.Heavy, 0.0, 360.0 * s.Progress);
                break;
            case GlyphState.Decayed:
                cv.Arc(primary, f.C, f.C, f.R, f.Hair, dashDeg: (40.0, 20.0));
                break;
        }
    }

    // --- bases ---------------------------------------------------------------

    /// <summary>Draws the silhouette (wash + outline) and returns its fill-region
    /// predicate for the overlays that clip to it.</summary>
    private static Func<double, double, bool> DrawBase(InkCanvas cv, Frame f, GlyphSpec s,
        InkCanvas.Layer outline, double w, (double, double)? dash,
        Func<double, double, bool>? washClip, bool registers)
    {
        var wash = InkCanvas.Layer.Wash;
        var soft = InkCanvas.Layer.Soft;
        switch (s.Base)
        {
            case GlyphBase.Hall:
            {
                (double, double)[] body = registers && s.Era == EraRegister.Industrial
                    ? [f.P(0.06, 0.94), f.P(0.06, 0.45), f.P(0.35, 0.2), f.P(0.35, 0.45), f.P(0.64, 0.2), f.P(0.64, 0.45), f.P(0.94, 0.2), f.P(0.94, 0.94)]
                    : registers && s.Era == EraRegister.Modern
                    ? [f.P(0.1, 0.94), f.P(0.1, 0.12), f.P(0.9, 0.12), f.P(0.9, 0.94)]
                    : registers && s.Era == EraRegister.Medieval
                    ? [f.P(0.06, 0.94), f.P(0.06, 0.46), f.P(0.5, 0.02), f.P(0.94, 0.46), f.P(0.94, 0.94)]
                    : [f.P(0.06, 0.94), f.P(0.06, 0.46), f.P(0.5, 0.08), f.P(0.94, 0.46), f.P(0.94, 0.94)];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                if (registers)
                {
                    switch (s.Era)
                    {
                        case EraRegister.Primitive:   // thatch: two strokes on the roof plane
                            cv.Stroke(soft, [f.P(0.3, 0.42), f.P(0.42, 0.22)], f.Hair * 0.7);
                            cv.Stroke(soft, [f.P(0.58, 0.22), f.P(0.7, 0.42)], f.Hair * 0.7);
                            break;
                        case EraRegister.Classical:   // plinth line and two columns
                            cv.Stroke(soft, [f.P(0.06, 0.84), f.P(0.94, 0.84)], f.Hair * 0.7);
                            cv.Stroke(soft, [f.P(0.32, 0.5), f.P(0.32, 0.84)], f.Hair * 0.7);
                            cv.Stroke(soft, [f.P(0.68, 0.5), f.P(0.68, 0.84)], f.Hair * 0.7);
                            break;
                        case EraRegister.Industrial:  // the stack
                            cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.78, 0.06), f.P(0.88, 0.06), f.P(0.88, 0.32), f.P(0.78, 0.32)]);
                            break;
                        case EraRegister.Modern:      // grid
                            cv.Stroke(soft, [f.P(0.37, 0.12), f.P(0.37, 0.94)], f.Hair * 0.6);
                            cv.Stroke(soft, [f.P(0.63, 0.12), f.P(0.63, 0.94)], f.Hair * 0.6);
                            cv.Stroke(soft, [f.P(0.1, 0.4), f.P(0.9, 0.4)], f.Hair * 0.6);
                            cv.Stroke(soft, [f.P(0.1, 0.67), f.P(0.9, 0.67)], f.Hair * 0.6);
                            break;
                    }
                }
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Tower:
            {
                (double, double)[] body = registers && s.Era == EraRegister.Modern
                    ? [f.P(0.3, 0.94), f.P(0.3, 0.08), f.P(0.7, 0.08), f.P(0.7, 0.94)]
                    : [f.P(0.32, 0.94), f.P(0.32, 0.2), f.P(0.68, 0.2), f.P(0.68, 0.94)];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                if (!registers || s.Era == EraRegister.Classical)
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.26, 0.14), f.P(0.74, 0.14), f.P(0.74, 0.2), f.P(0.26, 0.2)]);
                else if (s.Era == EraRegister.Primitive)
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.28, 0.2), f.P(0.5, 0.04), f.P(0.72, 0.2)]);
                else if (s.Era == EraRegister.Medieval)
                {
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.32, 0.1), f.P(0.42, 0.1), f.P(0.42, 0.2), f.P(0.32, 0.2)]);
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.45, 0.1), f.P(0.55, 0.1), f.P(0.55, 0.2), f.P(0.45, 0.2)]);
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.58, 0.1), f.P(0.68, 0.1), f.P(0.68, 0.2), f.P(0.58, 0.2)]);
                }
                else if (s.Era == EraRegister.Industrial)
                    cv.FillPolygon(InkCanvas.Layer.Primary, [f.P(0.42, 0.02), f.P(0.58, 0.02), f.P(0.58, 0.2), f.P(0.42, 0.2)]);
                else if (s.Era == EraRegister.Modern)
                {
                    cv.Stroke(soft, [f.P(0.3, 0.37), f.P(0.7, 0.37)], f.Hair * 0.6);
                    cv.Stroke(soft, [f.P(0.3, 0.65), f.P(0.7, 0.65)], f.Hair * 0.6);
                }
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Ring:
            {
                (double cx, double cy) = f.P(0.5, 0.5);
                double ro = f.Len(0.46), ri = f.Len(0.28);
                bool Region(double x, double y)
                {
                    double d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    return d2 <= ro * ro && d2 >= ri * ri;
                }
                if (washClip is not null)
                {
                    Func<double, double, bool> clip = washClip;
                    cv.Fill(wash, cx - ro, cy - ro, cx + ro, cy + ro, (x, y) => Region(x, y) && clip(x, y));
                }
                (double, double)? degDash = dash is null ? null : (30.0, 15.0);
                cv.Arc(outline, cx, cy, ro, w, dashDeg: degDash);
                cv.Arc(outline, cx, cy, ri, w, dashDeg: degDash);
                if (registers && s.Era == EraRegister.Medieval)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * System.Math.PI / 4.0;
                        (double sx, double sy) = (cx + System.Math.Sin(a) * ro, cy - System.Math.Cos(a) * ro);
                        (double ex, double ey) = (cx + System.Math.Sin(a) * (ro + f.Len(0.06)), cy - System.Math.Cos(a) * (ro + f.Len(0.06)));
                        cv.Stroke(InkCanvas.Layer.Primary, [(sx, sy), (ex, ey)], f.Hair);
                    }
                }
                return Region;
            }
            case GlyphBase.Dome:
            {
                (double cx, double cy) = f.P(0.5, 0.6);
                double r = f.Len(0.42);
                (double, double)[] plinth = [f.P(0.08, 0.6), f.P(0.92, 0.6), f.P(0.92, 0.92), f.P(0.08, 0.92)];
                bool Region(double x, double y) =>
                    (y <= cy && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) || InkCanvas.InsidePolygon(plinth, x, y);
                if (washClip is not null)
                {
                    Func<double, double, bool> clip = washClip;
                    cv.Fill(wash, cx - r, f.BoxY, cx + r, f.BoxY + f.Side, (x, y) => Region(x, y) && clip(x, y));
                }
                (double, double)? degDash = dash is null ? null : (30.0, 15.0);
                cv.Arc(outline, cx, cy, r, w, 270.0, 180.0, degDash);
                cv.Stroke(outline, plinth, w, closed: true, dash: dash);
                if (registers && s.Era == EraRegister.Classical)
                    cv.Stroke(InkCanvas.Layer.Primary, [f.P(0.5, 0.18), f.P(0.5, 0.06)], f.Hair);
                return Region;
            }
            case GlyphBase.Portico:
            {
                double apex = registers && s.Era == EraRegister.Medieval ? 0.02 : 0.08;
                (double, double)[] pediment = [f.P(0.06, 0.42), f.P(0.5, apex), f.P(0.94, 0.42)];
                (double, double)[] plinth = [f.P(0.06, 0.8), f.P(0.94, 0.8), f.P(0.94, 0.94), f.P(0.06, 0.94)];
                bool Region(double x, double y) => InkCanvas.InsidePolygon(pediment, x, y) || InkCanvas.InsidePolygon(plinth, x, y);
                if (washClip is not null)
                {
                    cv.FillPolygon(wash, pediment, washClip);
                    cv.FillPolygon(wash, plinth, washClip);
                }
                cv.Stroke(outline, pediment, w, closed: true, dash: dash);
                cv.Stroke(outline, plinth, w, closed: true, dash: dash);
                foreach (double u in new[] { 0.24, 0.5, 0.76 })
                    cv.Stroke(outline, [f.P(u, 0.42), f.P(u, 0.8)], w, dash: dash);
                return Region;
            }
            case GlyphBase.Formation:
            {
                (double, double)[] bar = [f.P(0.04, 0.28), f.P(0.96, 0.28), f.P(0.96, 0.72), f.P(0.04, 0.72)];
                if (washClip is not null) cv.FillPolygon(wash, bar, washClip);
                cv.Stroke(outline, bar, w, closed: true, dash: dash);
                int count = MarkCount(s.Strength);
                double mr = f.Len(0.07);
                for (int i = 0; i < count; i++)
                {
                    double u = 0.5 + (i - (count - 1) / 2.0) * 0.19;
                    (double mx, double my) = f.P(u, 0.5);
                    cv.FillCircle(InkCanvas.Layer.Primary, mx, my, System.Math.Max(0.6, mr));
                }
                return (x, y) => InkCanvas.InsidePolygon(bar, x, y);
            }
            case GlyphBase.Node:
            {
                (double cx, double cy) = f.P(0.5, 0.5);
                double r = f.Len(0.45);
                bool Region(double x, double y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
                if (washClip is not null) cv.FillCircle(wash, cx, cy, r, washClip);
                cv.Arc(outline, cx, cy, r, w, dashDeg: dash is null ? null : (30.0, 15.0));
                return Region;
            }
            case GlyphBase.Lozenge:
            {
                (double, double)[] d = [f.P(0.5, 0.04), f.P(0.96, 0.5), f.P(0.5, 0.96), f.P(0.04, 0.5)];
                if (washClip is not null) cv.FillPolygon(wash, d, washClip);
                cv.Stroke(outline, d, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(d, x, y);
            }
            case GlyphBase.Heap:
            {
                (double, double)[] centres = [f.P(0.3, 0.66), f.P(0.7, 0.66), f.P(0.5, 0.4)];
                double r = f.Len(0.24);
                bool Region(double x, double y)
                {
                    foreach ((double cx, double cy) in centres)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) return true;
                    return false;
                }
                if (washClip is not null)
                    foreach ((double cx, double cy) in centres) cv.FillCircle(wash, cx, cy, r, washClip);
                foreach ((double cx, double cy) in centres)
                    cv.Arc(outline, cx, cy, r, w, dashDeg: dash is null ? null : (30.0, 15.0));
                cv.Stroke(soft, [f.P(0.04, 0.94), f.P(0.96, 0.94)], f.Hair * 0.7);
                return Region;
            }
            case GlyphBase.Link:
            default:
            {
                (double ax, double ay) = f.P(0.16, 0.78);
                (double bx, double by) = f.P(0.84, 0.22);
                double r = f.Len(0.13);
                bool Region(double x, double y) =>
                    (x - ax) * (x - ax) + (y - ay) * (y - ay) <= r * r || (x - bx) * (x - bx) + (y - by) * (y - by) <= r * r;
                if (washClip is not null)
                {
                    cv.FillCircle(wash, ax, ay, r, washClip);
                    cv.FillCircle(wash, bx, by, r, washClip);
                }
                DrawLinkStroke(cv, f, s.Domain, (ax, ay), (bx, by), w, dash);
                cv.Arc(outline, ax, ay, r, w);
                cv.Arc(outline, bx, by, r, w);
                return Region;
            }
        }
    }

    /// <summary>Formation strength → mark count, the strength-band table of §2.1:
    /// 1..5 marks, thinning as strength drops, never zero for a living token.</summary>
    public static int MarkCount(double strength)
    {
        double s = GlyphSpec.Clamp01(strength);
        int n = (int)System.Math.Ceiling(s * 5.0);
        return n < 1 ? 1 : n > 5 ? 5 : n;
    }

    private static void DrawLinkStroke(InkCanvas cv, Frame f, GlyphDomain domain,
        (double X, double Y) a, (double X, double Y) b, double w, (double, double)? dash)
    {
        switch (domain)
        {
            case GlyphDomain.Maritime:   // sea lane: dashed in river blue
                cv.Stroke(InkCanvas.Layer.River, [a, b], w, dash: (3.0 * f.Hair, 2.0 * f.Hair));
                break;
            case GlyphDomain.Commerce:   // road: double hairline
            {
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len = System.Math.Sqrt(dx * dx + dy * dy);
                double nx = -dy / len * f.Hair * 0.9, ny = dx / len * f.Hair * 0.9;
                cv.Stroke(InkCanvas.Layer.Primary, [(a.X + nx, a.Y + ny), (b.X + nx, b.Y + ny)], f.Hair * 0.6, dash: dash);
                cv.Stroke(InkCanvas.Layer.Primary, [(a.X - nx, a.Y - ny), (b.X - nx, b.Y - ny)], f.Hair * 0.6, dash: dash);
                break;
            }
            case GlyphDomain.Craft:      // bridge: an arc over the midpoint
            {
                cv.Stroke(InkCanvas.Layer.Primary, [a, b], w, dash: dash);
                (double mx, double my) = ((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                cv.Arc(InkCanvas.Layer.Primary, mx, my + f.Len(0.1), f.Len(0.16), f.Hair, 270.0, 180.0);
                break;
            }
            case GlyphDomain.Agriculture: // canal: hatch ticks across the stroke
            {
                cv.Stroke(InkCanvas.Layer.Primary, [a, b], w, dash: dash);
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len = System.Math.Sqrt(dx * dx + dy * dy);
                double nx = -dy / len * f.Len(0.06), ny = dx / len * f.Len(0.06);
                for (int i = 1; i <= 3; i++)
                {
                    double t = i / 4.0;
                    (double px, double py) = (a.X + dx * t, a.Y + dy * t);
                    cv.Stroke(InkCanvas.Layer.Soft, [(px - nx, py - ny), (px + nx, py + ny)], f.Hair * 0.7);
                }
                break;
            }
            default:
                cv.Stroke(InkCanvas.Layer.Primary, [a, b], w, dash: dash);
                break;
        }
    }

    // --- domain marks --------------------------------------------------------

    private static void DrawMark(InkCanvas cv, Frame f, GlyphBase b, GlyphDomain domain)
    {
        // The slot: upper-right quadrant for building-like bases and nodes; the
        // centre for the lozenge and the heap; the upper-left corner for the
        // formation bar (whose centre carries the strength marks).
        (double u0, double v0, double u1, double v1) = b switch
        {
            GlyphBase.Lozenge or GlyphBase.Heap => (0.32, 0.32, 0.68, 0.68),
            GlyphBase.Formation => (0.02, 0.0, 0.34, 0.26),
            _ => (0.58, 0.04, 0.96, 0.42),
        };
        (double sx0, double sy0) = f.P(u0, v0);
        (double sx1, double sy1) = f.P(u1, v1);
        double sw = sx1 - sx0, sh = sy1 - sy0;
        (double X, double Y) M(double a, double c) => (sx0 + a * sw, sy0 + c * sh);
        double mw = System.Math.Max(0.75, f.Hair * 0.75);
        var ink = InkCanvas.Layer.Primary;

        switch (domain)
        {
            case GlyphDomain.Agriculture:
                cv.Stroke(ink, [M(0.5, 0.95), M(0.15, 0.15)], mw);
                cv.Stroke(ink, [M(0.5, 0.95), M(0.5, 0.05)], mw);
                cv.Stroke(ink, [M(0.5, 0.95), M(0.85, 0.15)], mw);
                break;
            case GlyphDomain.Craft:
            {
                (double cx, double cy) = M(0.5, 0.5);
                double r = 0.3 * System.Math.Min(sw, sh);
                cv.Arc(ink, cx, cy, r, mw);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * System.Math.PI / 3.0;
                    cv.Stroke(ink, [(cx + System.Math.Sin(a) * r, cy - System.Math.Cos(a) * r),
                                    (cx + System.Math.Sin(a) * r * 1.6, cy - System.Math.Cos(a) * r * 1.6)], mw);
                }
                break;
            }
            case GlyphDomain.Military:
                cv.Stroke(ink, [M(0.1, 0.9), M(0.5, 0.15), M(0.9, 0.9)], mw);
                break;
            case GlyphDomain.Maritime:
            {
                var wave = new (double, double)[9];
                var wave2 = new (double, double)[9];
                for (int i = 0; i < 9; i++)
                {
                    double a = i / 8.0;
                    double bwave = 0.3 + 0.12 * System.Math.Sin(2.0 * System.Math.PI * a);
                    wave[i] = M(a, bwave);
                    wave2[i] = M(a, bwave + 0.35);
                }
                cv.Stroke(ink, wave, mw);
                cv.Stroke(ink, wave2, mw);
                break;
            }
            case GlyphDomain.Letters:
                cv.Stroke(ink, [M(0.08, 0.2), M(0.48, 0.2), M(0.48, 0.85), M(0.08, 0.85)], mw, closed: true);
                cv.Stroke(ink, [M(0.52, 0.2), M(0.92, 0.2), M(0.92, 0.85), M(0.52, 0.85)], mw, closed: true);
                break;
            case GlyphDomain.Medicine:
                cv.FillPolygon(ink, [M(0.4, 0.08), M(0.6, 0.08), M(0.6, 0.92), M(0.4, 0.92)]);
                cv.FillPolygon(ink, [M(0.08, 0.4), M(0.92, 0.4), M(0.92, 0.6), M(0.08, 0.6)]);
                break;
            case GlyphDomain.Commerce:
                cv.Stroke(ink, [M(0.5, 0.1), M(0.5, 0.9)], mw);
                cv.Stroke(ink, [M(0.15, 0.3), M(0.85, 0.3)], mw);
                cv.Stroke(ink, [M(0.05, 0.6), M(0.3, 0.6)], mw);
                cv.Stroke(ink, [M(0.7, 0.6), M(0.95, 0.6)], mw);
                break;
            case GlyphDomain.Civic:
                cv.FillPolygon(ink, [M(0.4, 0.2), M(0.6, 0.2), M(0.6, 0.85), M(0.4, 0.85)]);
                cv.FillPolygon(ink, [M(0.22, 0.08), M(0.78, 0.08), M(0.78, 0.2), M(0.22, 0.2)]);
                cv.FillPolygon(ink, [M(0.22, 0.85), M(0.78, 0.85), M(0.78, 0.95), M(0.22, 0.95)]);
                break;
        }
    }

    // --- badges --------------------------------------------------------------

    private static void DrawChevrons(InkCanvas cv, Frame f, Veterancy vet)
    {
        int n = (int)vet;
        for (int i = 0; i < n; i++)
        {
            double cu = 0.5 + (i - (n - 1) / 2.0) * 0.26;
            InkCanvas.Layer layer = (vet == Veterancy.Elite && i == n - 1) ? InkCanvas.Layer.Gold : InkCanvas.Layer.Primary;
            cv.Stroke(layer, [f.P(cu - 0.09, 0.8), f.P(cu, 0.9), f.P(cu + 0.09, 0.8)], f.Hair);
        }
    }
}
