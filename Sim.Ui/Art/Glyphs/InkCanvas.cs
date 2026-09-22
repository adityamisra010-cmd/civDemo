namespace Sim.Ui.Art.Glyphs;

/// <summary>
/// THE COVERAGE RASTERISER — HeaderRuleBaker's technique generalised from one
/// ornament to a family of devices. Every shape is an INSIDE predicate evaluated
/// on a 4×4 subsample grid per pixel; the fraction inside is the pixel's coverage
/// on that layer. Coverage on a layer accumulates by MAX, never by sum, so
/// overlapping ink of the same kind cannot exceed 1.0.
///
/// COMPOSITION KEEPS RGB PALETTE-EXACT (style-bible §1 single-cartographer rule,
/// made exact the way HeaderRuleBakerTests makes it): each layer has ONE ink and
/// one alpha scale; a pixel's colour is the ink of the layer whose scaled coverage
/// is highest (ties to the higher-priority layer), and its alpha is that scaled
/// coverage. Antialiasing therefore lives only in alpha — "intermediate RGB would
/// bake a paper assumption into the ink" — and no pixel ever leaves
/// ParchmentPalette.BibleColors.
///
/// PURE AND DETERMINISTIC: doubles and bytes, no MonoGame types, no RNG, no clock.
/// </summary>
internal sealed class InkCanvas
{
    /// <summary>Layers in ascending priority. The composer picks the highest scaled
    /// coverage; ties go to the later member.</summary>
    internal enum Layer
    {
        Shadow = 0,   // InkPrimary @ ObjectLight.ShadowAlpha — Map placement only
        Wash = 1,     // InkSoft @ 0.45 — the maturity fill
        Hatch = 2,    // InkSoft @ 0.60 — scaffold hatch
        River = 3,    // River blue @ 1.0 — sea-lane strokes
        Soft = 4,     // InkSoft @ 1.0 — hairlines, secondary strokes
        Ghost = 5,    // InkPrimary @ 0.65 — the Locked silhouette
        Primary = 6,  // InkPrimary @ 1.0 — outlines, rings, marks
        Gold = 7,     // GoldLeaf @ 1.0 — the rare-emphasis accent
    }

    private const int LayerCount = 8;
    private const int Sub = 4;

    private static readonly ParchmentPalette.Rgba[] Inks =
    [
        ParchmentPalette.InkPrimary, ParchmentPalette.InkSoft, ParchmentPalette.InkSoft,
        ParchmentPalette.River, ParchmentPalette.InkSoft, ParchmentPalette.InkPrimary,
        ParchmentPalette.InkPrimary, ParchmentPalette.GoldLeaf,
    ];

    private static readonly double[] Scales =
    [
        ObjectLight.ShadowAlpha, 0.45, 0.60, 1.0, 1.0, 0.65, 1.0, 1.0,
    ];

    public int Size { get; }
    private readonly double[][] _cov;

    public InkCanvas(int size)
    {
        Size = size;
        _cov = new double[LayerCount][];
        for (int i = 0; i < LayerCount; i++) _cov[i] = new double[size * size];
    }

    /// <summary>Raw coverage of one layer at a pixel (tests and the shadow pass read it).</summary>
    public double CoverageAt(Layer layer, int x, int y) => _cov[(int)layer][y * Size + x];

    // --- primitives ----------------------------------------------------------

    /// <summary>Rasterise an inside-predicate over a bounding box (pixel coords).</summary>
    public void Fill(Layer layer, double x0, double y0, double x1, double y1, Func<double, double, bool> inside)
    {
        int px0 = System.Math.Max(0, (int)System.Math.Floor(x0));
        int py0 = System.Math.Max(0, (int)System.Math.Floor(y0));
        int px1 = System.Math.Min(Size - 1, (int)System.Math.Ceiling(x1));
        int py1 = System.Math.Min(Size - 1, (int)System.Math.Ceiling(y1));
        double[] cov = _cov[(int)layer];
        for (int y = py0; y <= py1; y++)
        {
            for (int x = px0; x <= px1; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < Sub; sy++)
                    for (int sx = 0; sx < Sub; sx++)
                        if (inside(x + (sx + 0.5) / Sub, y + (sy + 0.5) / Sub)) hits++;
                if (hits == 0) continue;
                double c = hits / (double)(Sub * Sub);
                int o = y * Size + x;
                if (c > cov[o]) cov[o] = c;
            }
        }
    }

    /// <summary>Filled polygon (even-odd), points in pixel coords.</summary>
    public void FillPolygon(Layer layer, (double X, double Y)[] pts, Func<double, double, bool>? clip = null)
    {
        (double x0, double y0, double x1, double y1) = Bounds(pts);
        Fill(layer, x0, y0, x1, y1, (x, y) => InsidePolygon(pts, x, y) && (clip is null || clip(x, y)));
    }

    /// <summary>Filled disc.</summary>
    public void FillCircle(Layer layer, double cx, double cy, double r, Func<double, double, bool>? clip = null)
    {
        Fill(layer, cx - r, cy - r, cx + r, cy + r,
            (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r && (clip is null || clip(x, y)));
    }

    /// <summary>
    /// Stroked polyline of the given width. <paramref name="dash"/> is (on, off)
    /// in pixels along the path, or null for a solid stroke; the dash phase starts
    /// at the first point so a closed outline's gaps are deterministic.
    /// </summary>
    public void Stroke(Layer layer, (double X, double Y)[] pts, double width, bool closed = false, (double On, double Off)? dash = null)
    {
        int n = pts.Length;
        if (n < 2) return;
        int segs = closed ? n : n - 1;
        double half = width / 2.0;
        // Cumulative length at each segment start, for the dash phase.
        var start = new double[segs + 1];
        for (int i = 0; i < segs; i++)
        {
            (double X, double Y) a = pts[i], b = pts[(i + 1) % n];
            start[i + 1] = start[i] + System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        }
        (double x0, double y0, double x1, double y1) = Bounds(pts);
        Fill(layer, x0 - half, y0 - half, x1 + half, y1 + half, (x, y) =>
        {
            double best = double.MaxValue, bestAlong = 0.0;
            for (int i = 0; i < segs; i++)
            {
                (double X, double Y) a = pts[i], b = pts[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len2 = dx * dx + dy * dy;
                double t = len2 <= 0.0 ? 0.0 : ((x - a.X) * dx + (y - a.Y) * dy) / len2;
                if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
                double qx = a.X + t * dx, qy = a.Y + t * dy;
                double d = System.Math.Sqrt((x - qx) * (x - qx) + (y - qy) * (y - qy));
                if (d < best) { best = d; bestAlong = start[i] + t * System.Math.Sqrt(len2); }
            }
            if (best > half) return false;
            if (dash is null) return true;
            double period = dash.Value.On + dash.Value.Off;
            double phase = bestAlong % period;
            return phase < dash.Value.On;
        });
    }

    /// <summary>
    /// A ring or arc: |dist − r| ≤ width/2, from <paramref name="startDeg"/>
    /// clockwise (screen space, 0° = 12 o'clock) through <paramref name="sweepDeg"/>.
    /// Dash is (on, off) in DEGREES so the pattern is independent of radius.
    /// </summary>
    public void Arc(Layer layer, double cx, double cy, double r, double width,
        double startDeg = 0.0, double sweepDeg = 360.0, (double On, double Off)? dashDeg = null)
    {
        if (sweepDeg <= 0.0) return;
        double half = width / 2.0;
        Fill(layer, cx - r - half, cy - r - half, cx + r + half, cy + r + half, (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            double d = System.Math.Sqrt(dx * dx + dy * dy);
            if (System.Math.Abs(d - r) > half) return false;
            // Clockwise angle from 12 o'clock in screen space (y down).
            double ang = System.Math.Atan2(dx, -dy) * 180.0 / System.Math.PI;
            if (ang < 0.0) ang += 360.0;
            double rel = ang - startDeg;
            if (rel < 0.0) rel += 360.0;
            if (rel > sweepDeg) return false;
            if (dashDeg is null) return true;
            double period = dashDeg.Value.On + dashDeg.Value.Off;
            return rel % period < dashDeg.Value.On;
        });
    }

    /// <summary>Diagonal hatch (45°) clipped to a predicate, line pitch in pixels.</summary>
    public void Hatch(Layer layer, double x0, double y0, double x1, double y1, double pitch, Func<double, double, bool> clip)
    {
        Fill(layer, x0, y0, x1, y1, (x, y) =>
        {
            if (!clip(x, y)) return false;
            double s = (x + y) % pitch;
            if (s < 0) s += pitch;
            return s < 1.0;
        });
    }

    /// <summary>Copy another canvas's composed alpha in as this canvas's Shadow
    /// layer, offset by (dx, dy) — the contact shadow of the Map placement.</summary>
    public void ShadowFrom(InkCanvas source, int dx, int dy)
    {
        double[] cov = _cov[(int)Layer.Shadow];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || ty < 0 || tx >= Size || ty >= Size) continue;
                double a = source.ComposedAlpha(x, y);
                if (a > cov[ty * Size + tx]) cov[ty * Size + tx] = a;
            }
        }
    }

    // --- composition ---------------------------------------------------------

    /// <summary>The composed alpha at a pixel: the max scaled coverage over layers.</summary>
    public double ComposedAlpha(int x, int y)
    {
        int o = y * Size + x;
        double best = 0.0;
        for (int l = 0; l < LayerCount; l++)
        {
            double v = _cov[l][o] * Scales[l];
            if (v > best) best = v;
        }
        return best;
    }

    /// <summary>Compose to straight-alpha RGBA: palette-exact ink, coverage alpha.</summary>
    public ArtImage Compose()
    {
        var rgba = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int o = y * Size + x;
                double best = 0.0;
                int bestLayer = -1;
                for (int l = 0; l < LayerCount; l++)
                {
                    double v = _cov[l][o] * Scales[l];
                    if (v > 0.0 && v >= best) { best = v; bestLayer = l; }
                }
                if (bestLayer < 0) continue;
                ParchmentPalette.Rgba ink = Inks[bestLayer];
                int p = o * 4;
                rgba[p] = ink.R; rgba[p + 1] = ink.G; rgba[p + 2] = ink.B;
                rgba[p + 3] = (byte)System.Math.Round(255.0 * System.Math.Clamp(best, 0.0, 1.0));
            }
        }
        return new ArtImage(Size, Size, rgba);
    }

    // --- geometry helpers ----------------------------------------------------

    internal static bool InsidePolygon((double X, double Y)[] pts, double x, double y)
    {
        bool inside = false;
        int n = pts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            (double xi, double yi) = pts[i];
            (double xj, double yj) = pts[j];
            bool crosses = (yi > y) != (yj > y);
            if (crosses && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    internal static (double, double, double, double) Bounds((double X, double Y)[] pts)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach ((double x, double y) in pts)
        {
            if (x < x0) x0 = x; if (x > x1) x1 = x;
            if (y < y0) y0 = y; if (y > y1) y1 = y;
        }
        return (x0, y0, x1, y1);
    }
}
