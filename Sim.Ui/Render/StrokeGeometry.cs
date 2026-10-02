namespace Sim.Ui.Render;

/// <summary>
/// Pure stroke geometry for backends that lack it natively (ImGui has no dashed strokes and
/// expects clockwise polygons). Kept here, headless and tested, because the ImGui backend
/// cannot run in the test host.
/// </summary>
public static class StrokeGeometry
{
    /// <summary>The smallest step the dash walker ever takes, in pixels. A remainder below
    /// half an ulp of the running distance would otherwise leave it in place forever
    /// (review finding, wf_40fe2a9f-261: the game froze on the overlay's first frame).</summary>
    public const double MinStep = 1e-6;

    /// <summary>A hard cap on walker steps per call: a bound that holds whatever the input.</summary>
    public const int MaxSteps = 200_000;

    /// <summary>
    /// The "on" pieces of a polyline dashed by arc length with period (on, off), phase
    /// continuous across vertices. Terminates for every input: each step advances t by at
    /// least <see cref="MinStep"/> and never by less than one representable double, and the
    /// walk stops after <see cref="MaxSteps"/> steps — in practice it takes a few per dash.
    /// </summary>
    public static List<((double X, double Y) A, (double X, double Y) B)> Dashes(
        IReadOnlyList<(double X, double Y)> pts, double on, double off)
    {
        var result = new List<((double, double), (double, double))>();
        double period = on + off;
        if (!(on > 0) || !(off >= 0) || !(period > 0) || double.IsInfinity(period) || pts.Count < 2) return result;
        double s = 0;   // arc length at the start of the current segment
        int steps = 0;
        for (int i = 1; i < pts.Count && steps < MaxSteps; i++)
        {
            (double x0, double y0) = pts[i - 1];
            (double x1, double y1) = pts[i];
            double len = System.Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            if (!(len > 1e-9) || double.IsInfinity(len)) continue;
            double t = 0;
            while (t < len && steps++ < MaxSteps)
            {
                double phase = (s + t) % period;
                bool drawing = phase < on;
                double run = System.Math.Min(len - t, drawing ? on - phase : period - phase);
                run = System.Math.Max(run, MinStep);
                double end = System.Math.Min(len, t + run);
                if (end <= t) end = System.Math.Min(len, System.Math.BitIncrement(t));
                if (drawing)
                    result.Add(((x0 + (x1 - x0) * t / len, y0 + (y1 - y0) * t / len),
                                (x0 + (x1 - x0) * end / len, y0 + (y1 - y0) * end / len)));
                t = end;
            }
            s += len;
        }
        return result;
    }

    /// <summary>A point on a cubic Bézier at parameter <paramref name="t"/>.</summary>
    public static (double X, double Y) BezierAt((double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, double t)
    {
        double u = 1 - t;
        double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
        return (a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
    }

    /// <summary>Twice the signed area in screen space (y down): positive for a polygon whose
    /// points run clockwise on screen.</summary>
    public static double SignedArea2(IReadOnlyList<(double X, double Y)> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            (double x0, double y0) = pts[i];
            (double x1, double y1) = pts[(i + 1) % pts.Count];
            a += x0 * y1 - x1 * y0;
        }
        return a;
    }

    /// <summary>The points in clockwise screen order (ImGui's anti-aliased fill puts its
    /// fringe outside only for clockwise polygons).</summary>
    public static (double X, double Y)[] Clockwise(IReadOnlyList<(double X, double Y)> pts)
    {
        var copy = pts.ToArray();
        if (SignedArea2(copy) < 0) Array.Reverse(copy);
        return copy;
    }
}
