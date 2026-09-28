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
            GlyphState.Discovered => 0.0,
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

        // 3. Domain mark. The Emblem base has no silhouette, so when the LOD contract
        //    suppresses its mark (16 px) a centre dot keeps the glyph from being a bare ring.
        if (s.Domain != GlyphDomain.None && s.Base != GlyphBase.Link)
            DrawMark(cv, f, s.Base, s.Domain);
        else if (s.Base == GlyphBase.Emblem)
            cv.FillCircle(outline, f.C, f.C, System.Math.Max(1.2, f.Len(0.2)));

        // 4. State overlay: scaffold hatch for work in progress.
        if (s.State == GlyphState.InProgress)
        {
            double pitch = System.Math.Max(2.0, s.Pixels / 8.0);
            cv.Hatch(InkCanvas.Layer.Hatch, f.BoxX, f.BoxY, f.BoxX + f.Side, f.BoxY + f.Side, pitch, region);
        }

        // 5. The state ring, then the maturity-stage pips that sit on it (24 px up).
        DrawRing(cv, f, s);
        if (s.Stage > 0 && s.Pixels >= (int)SizeClass.Px24)
            DrawStagePips(cv, f, s.Stage);

        // 6. Badges (48 px only).
        if (badges)
        {
            if (GlyphSpec.CarriesVeterancy(s.Base) && s.Veterancy != Veterancy.Recruit)
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
            case GlyphState.Discovered:
                // Fifteen short dashes (Locked has ten long ones). MEASURED at 16 px: the
                // shorter (7,13)/(10,10) patterns left Formation peaking at alpha 191 and
                // (12,12) left Emblem at 8.6 % area; (15,9) clears every floor with margin
                // (min area 14.8 %, min peak 239, min pairwise difference 21.9 %).
                cv.Arc(primary, f.C, f.C, f.R, f.Hair, dashDeg: (15.0, 9.0));
                break;
        }
    }

    /// <summary>MAX_STAGE pips on the lower arc of the ring (from 7:30 to 4:30 o'clock):
    /// filled for each reached stage, hollow for the rest, so "2 of 4" reads as a count
    /// rather than a length. The hollow pip's outline lies wholly inside the filled
    /// disc, so reaching a stage only ever ADDS ink (tested: ink rises with stage).</summary>
    private static void DrawStagePips(InkCanvas cv, Frame f, int stage)
    {
        double r = System.Math.Max(1.1, f.Hair * 1.35);
        double w = System.Math.Max(0.6, f.Hair * 0.6);
        for (int i = 0; i < GlyphSpec.MaxStage; i++)
        {
            double deg = 225.0 - i * 30.0;                     // 225°, 195°, 165°, 135°: bottom arc, left to right
            double a = deg * System.Math.PI / 180.0;
            double px = f.C + System.Math.Sin(a) * f.R, py = f.C - System.Math.Cos(a) * f.R;
            if (i < stage)
                cv.FillCircle(InkCanvas.Layer.Primary, px, py, r);
            else
                cv.Arc(InkCanvas.Layer.Primary, px, py, r - w / 2.0, w);
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
            case GlyphBase.Hexagon:
            {
                var hex = new (double, double)[6];
                for (int i = 0; i < 6; i++)
                {
                    double a = i * System.Math.PI / 3.0;
                    hex[i] = f.P(0.5 + 0.47 * System.Math.Sin(a), 0.5 - 0.47 * System.Math.Cos(a));
                }
                if (washClip is not null) cv.FillPolygon(wash, hex, washClip);
                cv.Stroke(outline, hex, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(hex, x, y);
            }
            case GlyphBase.Shield:
            {
                (double, double)[] body =
                [
                    f.P(0.12, 0.08), f.P(0.88, 0.08), f.P(0.88, 0.45), f.P(0.8, 0.66), f.P(0.66, 0.83),
                    f.P(0.5, 0.94), f.P(0.34, 0.83), f.P(0.2, 0.66), f.P(0.12, 0.45),
                ];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Scroll:
            {
                (double, double)[] body = [f.P(0.16, 0.2), f.P(0.84, 0.2), f.P(0.84, 0.8), f.P(0.16, 0.8)];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                // The rolled ends: a half-roll along the top and bottom edges.
                cv.Arc(outline, f.P(0.16, 0.2).Item1, f.P(0.16, 0.2).Item2 + f.Len(0.06), f.Len(0.06), w, 180.0, 180.0);
                cv.Arc(outline, f.P(0.84, 0.8).Item1, f.P(0.84, 0.8).Item2 - f.Len(0.06), f.Len(0.06), w, 0.0, 180.0);
                if (registers)
                {
                    cv.Stroke(soft, [f.P(0.28, 0.42), f.P(0.72, 0.42)], f.Hair * 0.6);
                    cv.Stroke(soft, [f.P(0.28, 0.58), f.P(0.64, 0.58)], f.Hair * 0.6);
                }
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Standard:
            {
                (double, double)[] body =
                [
                    f.P(0.2, 0.06), f.P(0.8, 0.06), f.P(0.8, 0.72), f.P(0.5, 0.58), f.P(0.2, 0.72),
                ];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Field:
            {
                (double, double)[] body = [f.P(0.06, 0.3), f.P(0.94, 0.3), f.P(0.94, 0.92), f.P(0.06, 0.92)];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                foreach (double v in new[] { 0.46, 0.61, 0.76 })
                    cv.Stroke(soft, [f.P(0.14, v), f.P(0.86, v)], f.Hair * 0.7);
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Monument:
            {
                (double, double)[] body =
                [
                    f.P(0.06, 0.94), f.P(0.06, 0.72), f.P(0.22, 0.72), f.P(0.22, 0.48), f.P(0.36, 0.48),
                    f.P(0.36, 0.24), f.P(0.5, 0.06), f.P(0.64, 0.24), f.P(0.64, 0.48), f.P(0.78, 0.48),
                    f.P(0.78, 0.72), f.P(0.94, 0.72), f.P(0.94, 0.94),
                ];
                if (washClip is not null) cv.FillPolygon(wash, body, washClip);
                cv.Stroke(outline, body, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(body, x, y);
            }
            case GlyphBase.Star:
            {
                var star = new (double, double)[16];
                for (int i = 0; i < 16; i++)
                {
                    double a = i * System.Math.PI / 8.0;
                    double rr = i % 2 == 0 ? (i % 4 == 0 ? 0.48 : 0.36) : 0.16;
                    star[i] = f.P(0.5 + rr * System.Math.Sin(a), 0.5 - rr * System.Math.Cos(a));
                }
                if (washClip is not null) cv.FillPolygon(wash, star, washClip);
                cv.Stroke(outline, star, w, closed: true, dash: dash);
                return (x, y) => InkCanvas.InsidePolygon(star, x, y);
            }
            case GlyphBase.Emblem:
            {
                (double cx, double cy) = f.P(0.5, 0.5);
                double r = f.Len(0.46);
                bool Region(double x, double y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
                if (washClip is not null) cv.FillCircle(wash, cx, cy, r, washClip);
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
        //
        // Trees/Ages foundation: the Emblem base IS its mark (centred, large); the
        // Standard carries its class mark in the banner's field; the Hexagon and the
        // Shield carry theirs centred; the Star keeps the upper-right quadrant like a
        // building.
        (double u0, double v0, double u1, double v1) = b switch
        {
            GlyphBase.Lozenge or GlyphBase.Heap => (0.32, 0.32, 0.68, 0.68),
            GlyphBase.Formation => (0.02, 0.0, 0.34, 0.26),
            GlyphBase.Emblem => (0.14, 0.14, 0.86, 0.86),
            GlyphBase.Standard => (0.28, 0.12, 0.72, 0.52),
            GlyphBase.Hexagon => (0.27, 0.27, 0.73, 0.73),
            GlyphBase.Shield => (0.29, 0.2, 0.71, 0.62),
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
            // --- the seven Tree lens emblems ---------------------------------------
            case GlyphDomain.Knowledge:   // an oil lamp: flame over a bowl
                cv.FillPolygon(ink, [M(0.5, 0.04), M(0.63, 0.3), M(0.6, 0.46), M(0.5, 0.53), M(0.4, 0.46), M(0.37, 0.3)]);
                cv.FillPolygon(ink, [M(0.14, 0.62), M(0.86, 0.62), M(0.66, 0.9), M(0.34, 0.9)]);
                break;
            case GlyphDomain.Techniques:  // a hammer
                cv.FillPolygon(ink, [M(0.16, 0.1), M(0.84, 0.1), M(0.84, 0.34), M(0.16, 0.34)]);
                cv.FillPolygon(ink, [M(0.43, 0.34), M(0.57, 0.34), M(0.57, 0.94), M(0.43, 0.94)]);
                break;
            case GlyphDomain.Institutions: // a small pediment on three columns
                cv.FillPolygon(ink, [M(0.06, 0.34), M(0.5, 0.06), M(0.94, 0.34)]);
                foreach (double u in new[] { 0.24, 0.5, 0.76 })
                    cv.Stroke(ink, [M(u, 0.4), M(u, 0.82)], mw * 1.2);
                cv.Stroke(ink, [M(0.08, 0.9), M(0.92, 0.9)], mw * 1.2);
                break;
            case GlyphDomain.Infrastructure: // an arch bridge: deck, arch and piers
            {
                cv.Stroke(ink, [M(0.02, 0.34), M(0.98, 0.34)], mw * 1.2);
                (double ax, double ay) = M(0.5, 0.92);
                cv.Arc(ink, ax, ay, 0.4 * sw, mw * 1.2, 270.0, 180.0);
                cv.Stroke(ink, [M(0.1, 0.34), M(0.1, 0.94)], mw);
                cv.Stroke(ink, [M(0.9, 0.34), M(0.9, 0.94)], mw);
                break;
            }
            case GlyphDomain.Industry:    // a sawtooth works with a stack
                cv.Stroke(ink, [M(0.04, 0.92), M(0.04, 0.56), M(0.3, 0.36), M(0.3, 0.56), M(0.56, 0.36), M(0.56, 0.56), M(0.96, 0.56), M(0.96, 0.92)], mw, closed: true);
                cv.FillPolygon(ink, [M(0.72, 0.06), M(0.86, 0.06), M(0.86, 0.56), M(0.72, 0.56)]);
                break;
            case GlyphDomain.Applications: // an isometric crate
                cv.Stroke(ink, [M(0.5, 0.06), M(0.92, 0.28), M(0.92, 0.72), M(0.5, 0.94), M(0.08, 0.72), M(0.08, 0.28)], mw, closed: true);
                cv.Stroke(ink, [M(0.08, 0.28), M(0.5, 0.5), M(0.92, 0.28)], mw);
                cv.Stroke(ink, [M(0.5, 0.5), M(0.5, 0.94)], mw);
                break;

            // --- specialisation and system marks ---------------------------------
            case GlyphDomain.Dividers:    // engineering dividers: two legs, a pivot, a bow
            {
                cv.Stroke(ink, [M(0.5, 0.12), M(0.18, 0.94)], mw * 1.2);
                cv.Stroke(ink, [M(0.5, 0.12), M(0.82, 0.94)], mw * 1.2);
                (double px, double py) = M(0.5, 0.12);
                cv.FillCircle(ink, px, py, System.Math.Max(0.8, 0.09 * sw));
                cv.Stroke(ink, [M(0.3, 0.62), M(0.7, 0.62)], mw);
                break;
            }
            case GlyphDomain.Anvil:       // flat face, pointed horn to the left, narrow waist, footed base
                cv.FillPolygon(ink, [M(0.02, 0.2), M(0.96, 0.16), M(0.96, 0.4), M(0.72, 0.44), M(0.64, 0.68),
                                     M(0.88, 0.76), M(0.88, 0.94), M(0.12, 0.94), M(0.12, 0.76), M(0.36, 0.68),
                                     M(0.34, 0.44), M(0.26, 0.36)]);
                break;
            case GlyphDomain.Rail:        // two rails and three ties
                cv.Stroke(ink, [M(0.3, 0.04), M(0.3, 0.96)], mw * 1.2);
                cv.Stroke(ink, [M(0.7, 0.04), M(0.7, 0.96)], mw * 1.2);
                foreach (double v in new[] { 0.2, 0.5, 0.8 })
                    cv.Stroke(ink, [M(0.12, v), M(0.88, v)], mw);
                break;
            case GlyphDomain.Hourglass:
                cv.Stroke(ink, [M(0.14, 0.06), M(0.86, 0.06)], mw * 1.3);
                cv.Stroke(ink, [M(0.14, 0.94), M(0.86, 0.94)], mw * 1.3);
                cv.Stroke(ink, [M(0.24, 0.08), M(0.76, 0.08), M(0.5, 0.5), M(0.76, 0.92), M(0.24, 0.92), M(0.5, 0.5)], mw, closed: true);
                cv.FillPolygon(ink, [M(0.36, 0.84), M(0.64, 0.84), M(0.5, 0.64)]);
                break;
            case GlyphDomain.Links:       // three linked rings
            {
                double rr = 0.2 * System.Math.Min(sw, sh);
                foreach ((double a, double c) in new[] { (0.3, 0.36), (0.7, 0.36), (0.5, 0.7) })
                {
                    (double cx, double cy) = M(a, c);
                    cv.Arc(ink, cx, cy, rr, mw);
                }
                break;
            }

            // --- unit-class marks (objects only) -----------------------------------
            case GlyphDomain.Spear:       // heavy shaft, leaf-shaped head, butt spike
                cv.Stroke(ink, [M(0.5, 0.98), M(0.5, 0.36)], mw * 1.6);
                cv.FillPolygon(ink, [M(0.5, 0.0), M(0.72, 0.28), M(0.5, 0.46), M(0.28, 0.28)]);
                cv.FillPolygon(ink, [M(0.36, 0.52), M(0.64, 0.52), M(0.64, 0.58), M(0.36, 0.58)]);
                break;
            case GlyphDomain.Bow:
            {
                (double bx, double by) = M(0.78, 0.5);
                double br = 0.46 * sh;
                cv.Arc(ink, bx, by, br, mw * 1.3, 205.0, 130.0);
                double a0 = 205.0 * System.Math.PI / 180.0, a1 = 335.0 * System.Math.PI / 180.0;
                cv.Stroke(ink, [(bx + System.Math.Sin(a0) * br, by - System.Math.Cos(a0) * br),
                                (bx + System.Math.Sin(a1) * br, by - System.Math.Cos(a1) * br)], mw * 0.8);
                break;
            }
            case GlyphDomain.Horseshoe:
            {
                (double hx, double hy) = M(0.5, 0.48);
                double hr = 0.34 * System.Math.Min(sw, sh);
                cv.Arc(ink, hx, hy, hr, mw * 2.2, 240.0, 240.0);
                break;
            }
            case GlyphDomain.Rifles:      // crossed long arms
                cv.Stroke(ink, [M(0.1, 0.9), M(0.9, 0.1)], mw * 1.3);
                cv.Stroke(ink, [M(0.9, 0.9), M(0.1, 0.1)], mw * 1.3);
                cv.FillPolygon(ink, [M(0.04, 0.84), M(0.2, 0.96), M(0.26, 0.86), M(0.12, 0.76)]);
                cv.FillPolygon(ink, [M(0.96, 0.84), M(0.8, 0.96), M(0.74, 0.86), M(0.88, 0.76)]);
                break;
            case GlyphDomain.Cannon:
            {
                cv.Stroke(ink, [M(0.34, 0.62), M(0.94, 0.26)], mw * 2.4);
                (double wx, double wy) = M(0.34, 0.72);
                cv.Arc(ink, wx, wy, 0.2 * System.Math.Min(sw, sh), mw * 1.2);
                break;
            }
            case GlyphDomain.Tracks:      // hull, turret, gun, road wheels
            {
                cv.Stroke(ink, [M(0.06, 0.56), M(0.94, 0.56), M(0.84, 0.88), M(0.16, 0.88)], mw, closed: true);
                cv.FillPolygon(ink, [M(0.34, 0.34), M(0.66, 0.34), M(0.66, 0.56), M(0.34, 0.56)]);
                cv.Stroke(ink, [M(0.66, 0.44), M(0.98, 0.4)], mw * 1.2);
                foreach (double u in new[] { 0.28, 0.5, 0.72 })
                {
                    (double tx, double ty) = M(u, 0.72);
                    cv.FillCircle(ink, tx, ty, System.Math.Max(0.7, 0.07 * sw));
                }
                break;
            }
            case GlyphDomain.Aircraft:
                cv.Stroke(ink, [M(0.5, 0.04), M(0.5, 0.94)], mw * 1.4);
                cv.FillPolygon(ink, [M(0.04, 0.46), M(0.96, 0.46), M(0.96, 0.58), M(0.04, 0.58)]);
                cv.FillPolygon(ink, [M(0.3, 0.86), M(0.7, 0.86), M(0.7, 0.94), M(0.3, 0.94)]);
                break;
            case GlyphDomain.Ship:
                cv.FillPolygon(ink, [M(0.04, 0.64), M(0.96, 0.64), M(0.8, 0.9), M(0.2, 0.9)]);
                cv.Stroke(ink, [M(0.5, 0.06), M(0.5, 0.64)], mw);
                cv.FillPolygon(ink, [M(0.54, 0.1), M(0.86, 0.56), M(0.54, 0.56)]);
                break;
            case GlyphDomain.Eye:         // an almond eye with its pupil
            {
                cv.Stroke(ink, [M(0.04, 0.5), M(0.28, 0.24), M(0.5, 0.18), M(0.72, 0.24), M(0.96, 0.5),
                                M(0.72, 0.76), M(0.5, 0.82), M(0.28, 0.76)], mw, closed: true);
                (double ex, double ey) = M(0.5, 0.5);
                cv.FillCircle(ink, ex, ey, System.Math.Max(0.9, 0.16 * System.Math.Min(sw, sh)));
                break;
            }
            case GlyphDomain.StarOfCommand:
            {
                var st = new (double, double)[10];
                for (int i = 0; i < 10; i++)
                {
                    double a = i * System.Math.PI / 5.0;
                    double rr = i % 2 == 0 ? 0.48 : 0.2;
                    st[i] = M(0.5 + rr * System.Math.Sin(a), 0.52 - rr * System.Math.Cos(a));
                }
                cv.FillPolygon(ink, st);
                break;
            }
            case GlyphDomain.Laurel:      // two leafed branches rising from the foot
            {
                // Points on a circle about the slot centre; angles clockwise from 12 o'clock.
                // Left branch 190°→320°, right branch 170°→40°: they meet at the bottom.
                foreach ((double from, double to) in new[] { (190.0, 320.0), (170.0, 40.0) })
                {
                    var branch = new (double, double)[7];
                    for (int i = 0; i < 7; i++)
                    {
                        double a = (from + (to - from) * i / 6.0) * System.Math.PI / 180.0;
                        branch[i] = M(0.5 + 0.4 * System.Math.Sin(a), 0.5 - 0.4 * System.Math.Cos(a));
                    }
                    cv.Stroke(ink, branch, mw);
                    for (int i = 1; i < 7; i++)
                    {
                        (double lx, double ly) = branch[i];
                        cv.FillCircle(ink, lx, ly, System.Math.Max(0.8, 0.07 * sw));
                    }
                }
                break;
            }
            case GlyphDomain.Brush:
                cv.Stroke(ink, [M(0.86, 0.06), M(0.42, 0.56)], mw * 1.3);
                cv.FillPolygon(ink, [M(0.36, 0.5), M(0.5, 0.64), M(0.24, 0.94), M(0.08, 0.92), M(0.12, 0.76)]);
                break;
            case GlyphDomain.Flask:
                cv.Stroke(ink, [M(0.4, 0.04), M(0.6, 0.04)], mw * 1.2);
                cv.Stroke(ink, [M(0.42, 0.06), M(0.42, 0.38), M(0.1, 0.94), M(0.9, 0.94), M(0.58, 0.38), M(0.58, 0.06)], mw);
                cv.FillPolygon(ink, [M(0.26, 0.66), M(0.74, 0.66), M(0.88, 0.92), M(0.12, 0.92)]);
                break;

            // --- world visualization marks (objects) --------------------------------------
            case GlyphDomain.Lyre:        // two curved arms, a crossbar, a sound box, three strings
                cv.Stroke(ink, [M(0.3, 0.06), M(0.18, 0.22), M(0.2, 0.46), M(0.34, 0.66), M(0.36, 0.84)], mw * 1.3);
                cv.Stroke(ink, [M(0.7, 0.06), M(0.82, 0.22), M(0.8, 0.46), M(0.66, 0.66), M(0.64, 0.84)], mw * 1.3);
                cv.Stroke(ink, [M(0.18, 0.18), M(0.82, 0.18)], mw * 1.2);
                cv.FillPolygon(ink, [M(0.3, 0.8), M(0.7, 0.8), M(0.66, 0.96), M(0.34, 0.96)]);
                foreach (double u in new[] { 0.42, 0.5, 0.58 })
                    cv.Stroke(ink, [M(u, 0.2), M(u, 0.8)], mw * 0.6);
                break;
            case GlyphDomain.Compass:     // a four-point compass star over a ring
            {
                (double cx, double cy) = M(0.5, 0.5);
                double rr = 0.36 * System.Math.Min(sw, sh);
                cv.Arc(ink, cx, cy, rr, mw * 0.8);
                cv.FillPolygon(ink, [M(0.5, 0.0), M(0.58, 0.42), M(1.0, 0.5), M(0.58, 0.58), M(0.5, 1.0), M(0.42, 0.58), M(0.0, 0.5), M(0.42, 0.42)]);
                break;
            }
            case GlyphDomain.Anchor:      // ring, shank, stock, curved arms with flukes
            {
                (double rx, double ry) = M(0.5, 0.1);
                cv.Arc(ink, rx, ry, System.Math.Max(0.9, 0.07 * System.Math.Min(sw, sh)), mw);
                cv.Stroke(ink, [M(0.5, 0.18), M(0.5, 0.9)], mw * 1.4);
                cv.Stroke(ink, [M(0.28, 0.3), M(0.72, 0.3)], mw * 1.2);
                cv.Stroke(ink, [M(0.12, 0.6), M(0.2, 0.78), M(0.36, 0.9), M(0.5, 0.92), M(0.64, 0.9), M(0.8, 0.78), M(0.88, 0.6)], mw * 1.3);
                cv.FillPolygon(ink, [M(0.04, 0.62), M(0.2, 0.56), M(0.16, 0.72)]);
                cv.FillPolygon(ink, [M(0.96, 0.62), M(0.8, 0.56), M(0.84, 0.72)]);
                break;
            }
            case GlyphDomain.Drop:        // a water drop with a ripple line beneath
            {
                var drop = new (double, double)[13];
                for (int i = 0; i < 13; i++)
                {
                    double a = System.Math.PI * (i / 12.0);   // lower semicircle, left to right
                    drop[i] = M(0.5 - 0.3 * System.Math.Cos(a), 0.56 + 0.3 * System.Math.Sin(a));
                }
                cv.FillPolygon(ink, [M(0.5, 0.02), .. drop]);
                cv.Stroke(ink, [M(0.08, 0.98), M(0.3, 0.92), M(0.5, 0.98), M(0.7, 0.92), M(0.92, 0.98)], mw);
                break;
            }

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
