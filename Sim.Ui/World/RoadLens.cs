using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>
/// One built inter-settlement route as the lens draws it: a travelled <c>TransportEdges</c> row
/// (ADR-032 §10; Road mode, complete). The row is a settlement PAIR with no polyline in state, and the
/// simulation's own geometry for it is the straight line between the two sites: its <c>LengthKm</c> is
/// the straight-line geographic distance (ADR-032 §12.3) and the Pathfinder lane joins the two origin
/// lattice nodes directly (§10.5). The lens therefore draws it SITE CELL TO SITE CELL (A → B, A &lt; B).
/// <see cref="EdgeType"/> is the class fully achieved, <see cref="TargetClass"/> the class it is being
/// modernized toward and <see cref="Modernization"/> the fraction [0, 1) of that upgrade performed.
/// </summary>
public sealed record RouteLensView(
    int Id, int A, int B, double X0, double Y0, double X1, double Y1,
    int EdgeType, int TargetClass, double Modernization, double LengthKm, string ClassName, string TargetName)
{
    /// <summary>Part-way between two classes (ADR-032 §10.3, partial modernization).</summary>
    public bool IsPartial => Modernization > 0.0 && TargetClass != EdgeType;
}

/// <summary>One stroke of a road style: colour, width in screen px, optional dash.</summary>
public readonly record struct RoadStroke(Rgba Color, double Width, (double On, double Off)? Dash);

/// <summary>
/// One piece of the network the lens drew — THE UNIT OF STROKE OWNERSHIP on the Paths layer. Either a
/// baseline dirt-path lattice step (<see cref="NetworkEdge"/> = its <c>NetworkEdges</c> id,
/// <see cref="Route"/> = -1) or a part of a built route (<see cref="Route"/> = its <c>TransportEdges</c>
/// id, <see cref="NetworkEdge"/> = -1) drawn in class <see cref="EdgeType"/> over the fraction
/// [<see cref="From"/>, <see cref="To"/>] of the route from A to B. <see cref="Commands"/> is the number of
/// draw commands it emitted; the pieces' commands sum exactly to the Paths layer's draw count.
/// </summary>
public sealed record PathPiece(int NetworkEdge, int Route, int EdgeType, double From, double To, int Commands);

/// <summary>
/// THE ROAD LAYER of the world lens (directive "WORLD UI": roads visibly affect the world). Draws the
/// free dirt-path baseline (PathBuild's <c>NetworkEdges</c>) and every built route
/// (<see cref="RouteLensView"/>), styled by class, with ONE OWNER PER PIECE OF GROUND:
/// <list type="bullet">
/// <item>a baseline lattice step that lies inside a route's corridor is drawn by the route, never
///   also as a dashed path (<see cref="InCorridor"/>);</item>
/// <item>a partially modernized route is drawn as two complementary pieces — the modernized fraction
///   in the TARGET class's style, the rest in the CURRENT class's style (ADR-032 §10.3);</item>
/// <item>upgrades are in place (ADR-032 §10.2): a route is one row, so it is one drawn line, whatever
///   its class history.</item>
/// </list>
/// Style by TIER first (D-009: PATH brown single line · ROAD dark-kerbed with a pale fill · HIGHWAY
/// iron-red), class second (weight, fill and marks). World zoom draws the network as plain weighted
/// strokes; Regional and Settlement zoom add casing and class marks, and Settlement zoom names each
/// route (P2: the widths blend and the names fade in across the Regional ↔ Settlement window). Pure: reads
/// the projection only.
/// </summary>
public static class RoadLens
{
    private static Rgba A(Rgba c, double a) => Ink.With(c, a);

    /// <summary>How road class <paramref name="edgeType"/> is drawn at <paramref name="zoom"/>, strokes in
    /// paint order (casing first). <see cref="EdgeTypes.DirtPath"/> is exactly the baseline path look
    /// (dashed at World zoom, cased below); an unknown class draws as a dirt path.</summary>
    public static IReadOnlyList<RoadStroke> StyleOf(int edgeType, WorldZoom zoom, MapInk ink) =>
        zoom == WorldZoom.World ? WorldStyleOf(edgeType, ink) : StyleAt(edgeType, zoom == WorldZoom.Settlement ? 1.0 : 0.0, ink);

    /// <summary>The World band's look: plain weighted strokes.</summary>
    private static IReadOnlyList<RoadStroke> WorldStyleOf(int edgeType, MapInk ink) => edgeType switch
    {
        EdgeTypes.Trackway => [new(A(ink.Trackway, 0.95), 1.8, null)],
        EdgeTypes.BuiltRoad => [new(ink.RoadEdge, 2.2, null)],
        EdgeTypes.PavedRoad => [new(ink.RoadEdge, 2.6, null)],
        EdgeTypes.MacadamRoad => [new(ink.RoadEdge, 3.0, null)],
        EdgeTypes.Highway => [new(ink.Highway, 3.2, null)],
        _ => [new(A(ink.Path, 0.75), 1.6, (4, 3))],
    };

    /// <summary>
    /// P2: the cased look of the closer bands at a Settlement blend <paramref name="town"/> (<see cref="MapDetail.Settlement"/>):
    /// the same strokes at both bands, each width blended from the Regional band's (0) to the Settlement band's (1) —
    /// so a road widens continuously across the Regional ↔ Settlement window. At 0 and 1 exactly the banded styles.
    /// </summary>
    public static IReadOnlyList<RoadStroke> StyleAt(int edgeType, double town, MapInk ink)
    {
        double W(double regional, double settlement) => MapDetail.Mix(regional, settlement, town);
        switch (edgeType)
        {
            case EdgeTypes.Trackway:   // PATH tier: an improved path — a solid umber line, broken pale centre below World zoom
            {
                double w = W(2.8, 3.8);
                return [new(A(ink.PathCasing, 0.7), w + 2.2, null), new(A(ink.Trackway, 0.95), w, null), new(ink.PathCasing, w * 0.4, (3, 3))];
            }
            case EdgeTypes.BuiltRoad:  // ROAD tier: dark kerbs, tan bed
            {
                double w = W(3.2, 4.6);
                return [new(ink.RoadEdge, w + 2.2, null), new(ink.BuiltRoad, w, null)];
            }
            case EdgeTypes.PavedRoad:  // ROAD tier: dark kerbs, pale paving, a fine set centre
            {
                double w = W(3.6, 5.2);
                return [new(ink.RoadEdge, w + 2.2, null), new(ink.PavedRoad, w, null), new(A(ink.RoadMarks, 0.75), 1.0, (2, 2))];
            }
            case EdgeTypes.MacadamRoad: // ROAD tier: dark kerbs, umber bed, a continuous centre line
            {
                double w = W(3.8, 5.6);
                return [new(ink.RoadEdge, w + 2.2, null), new(ink.MacadamRoad, w, null), new(A(ink.RoadMarks, 0.9), 1.0, null)];
            }
            case EdgeTypes.Highway:    // HIGHWAY tier: wide dark kerbs, iron-red bed, a dashed pale centre line
            {
                double w = W(4.6, 6.4);
                return [new(ink.RoadEdge, w + 2.6, null), new(ink.Highway, w, null), new(ink.HighwayCentre, 1.2, (6, 4))];
            }
            default:                   // DirtPath (the free baseline) — today's path look, unchanged
            {
                double w = W(2.2, 3.2);
                return [new(A(ink.PathCasing, 0.7), w + 2.2, null), new(A(ink.Path, 0.95), w, null)];
            }
        }
    }

    /// <summary>The display name of a road class: the content's name for the class's research entity
    /// (research.json, e.g. "Paved road"); "Dirt path" for the free baseline.</summary>
    public static string ClassName(SimConfig cfg, int edgeType)
    {
        if (edgeType == EdgeTypes.DirtPath) return "Dirt path";
        string? entity = cfg.Roads?.ClassOf(edgeType)?.Entity;
        if (entity is not null && cfg.Research is { } rc)
        {
            int i = rc.EntityIndexOf(entity);
            if (i >= 0 && rc.Entities[i].Name is { Length: > 0 } n) return n;
        }
        return edgeType switch
        {
            EdgeTypes.Trackway => "Trackway",
            EdgeTypes.BuiltRoad => "Built road",
            EdgeTypes.PavedRoad => "Paved road",
            EdgeTypes.MacadamRoad => "Macadam road",
            EdgeTypes.Highway => "Highway",
            _ => "Road class " + edgeType.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Distance (world px) from a point to a segment.</summary>
    public static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double vx = bx - ax, vy = by - ay;
        double len2 = vx * vx + vy * vy;
        double t = len2 <= 0.0 ? 0.0 : Math.Clamp(((px - ax) * vx + (py - ay) * vy) / len2, 0.0, 1.0);
        double dx = px - (ax + t * vx), dy = py - (ay + t * vy);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// ONE OWNER PER PIECE OF GROUND: a baseline lattice step lies in a route's corridor — and is drawn by
    /// the route, not again as a path — when BOTH its lattice-node centres are strictly closer than
    /// <paramref name="halfWidth"/> to the route's centre line. The corridor is one lattice block wide
    /// (half-width = half the lattice stride): the resolution the dirt network is laid at. A step that
    /// merely crosses the route has its nodes on opposite sides and is never absorbed.
    /// </summary>
    public static bool InCorridor(RoadSegment step, RouteLensView route, double halfWidth) =>
        DistanceToSegment(step.X0, step.Y0, route.X0, route.Y0, route.X1, route.Y1) < halfWidth
        && DistanceToSegment(step.X1, step.Y1, route.X0, route.Y0, route.X1, route.Y1) < halfWidth;

    /// <summary>
    /// WS3: <see cref="InCorridor(RoadSegment, RouteLensView, double)"/> on a world <paramref name="wrapWidth"/> px round
    /// (0: flat, exactly the flat rule). Both primitives are held unwrapped relative to their own first point, so near the seam
    /// they may lie a circumference apart: the step is judged at its copy NEAREST the route's midpoint
    /// (<see cref="Sim.Core.Pathing.MapTopology.UnwrapNear"/>; both its ends move by the same whole circumference), which is
    /// the copy the route's corridor can hold — a route is at most maxRouteKm long, far under half a circumference.
    /// </summary>
    public static bool InCorridor(RoadSegment step, RouteLensView route, double halfWidth, double wrapWidth)
    {
        if (!(wrapWidth > 0)) return InCorridor(step, route, halfWidth);
        double mid = (route.X0 + route.X1) / 2.0;
        double shift = Sim.Core.Pathing.MapTopology.UnwrapNear(step.X0, mid, wrapWidth) - step.X0;
        return DistanceToSegment(step.X0 + shift, step.Y0, route.X0, route.Y0, route.X1, route.Y1) < halfWidth
            && DistanceToSegment(step.X1 + shift, step.Y1, route.X0, route.Y0, route.X1, route.Y1) < halfWidth;
    }

    /// <summary>Paints the Paths layer: the baseline steps no route owns, then every route (table
    /// order) on top, recording each piece drawn in <paramref name="pieces"/>. Below World zoom the look is
    /// <see cref="StyleAt"/> of the Settlement blend <paramref name="town"/> (P2), and each route is named once
    /// <paramref name="town"/> is above 0, the name faded in with it (the banded lens: named at Settlement zoom only).
    /// <para>WS3: <paramref name="copies"/> are the copies k of the world to consider (null or {0}: flat, exactly as
    /// before) at <paramref name="worldWidth"/> px a circumference. On a wrapped paint every step and route — held unwrapped
    /// relative to its first point (<see cref="WorldProjection.WrapX"/>) — is drawn at its copy nearest the viewport's centre
    /// (as the flat paint draws it whatever the viewport) and at every other copy whose bounds touch the viewport; a route
    /// is named at each copy drawn where its line is visible. Still ONE <see cref="PathPiece"/> per step or route part, its
    /// <see cref="PathPiece.Commands"/> summing every copy's. Returns the draws beyond each piece of ground's first.</para></summary>
    public static int PaintNetwork(DrawList d, ITextMeasure m, WorldProjection p, WorldZoom zoom, double town,
        Func<double, double, (double X, double Y)> toScreen, RectD viewport, MapInk ink, List<PathPiece> pieces,
        int[]? copies = null, double worldWidth = 0.0)
    {
        bool world = zoom == WorldZoom.World;
        IReadOnlyList<RoadStroke> Style(int cls) => world ? WorldStyleOf(cls, ink) : StyleAt(cls, town, ink);
        IReadOnlyList<RoadStroke> dirt = Style(EdgeTypes.DirtPath);
        int[] ks = copies is { Length: > 0 } ? copies : [0];
        int extra = 0;
        (double X, double Y) At(int c, double x, double y) => c == 0 ? toScreen(x, y) : toScreen(x + c * worldWidth, y);
        // WS3: the copy of a segment whose midpoint is nearest the viewport's centre (ascending k, the first nearest wins).
        int Primary(double x0, double y0, double x1, double y1)
        {
            if (ks.Length == 1) return ks[0];
            int best = ks[0];
            double bestD = double.PositiveInfinity;
            foreach (int c in ks)
            {
                double mid = (At(c, x0, y0).X + At(c, x1, y1).X) / 2.0;
                double dd = Math.Abs(mid - viewport.CenterX);
                if (dd < bestD) { bestD = dd; best = c; }
            }
            return best;
        }
        double Widest(IReadOnlyList<RoadStroke> styles)
        {
            double w = 0;
            foreach (RoadStroke k in styles) w = Math.Max(w, k.Width);
            return w;
        }
        foreach (RoadSegment s in p.Roads)
        {
            if (s.CoveredBy >= 0) continue;   // the route that runs here draws this ground
            int before = d.Commands.Count;
            int primary = Primary(s.X0, s.Y0, s.X1, s.Y1);
            bool first = true;
            foreach (int c in ks)
            {
                (double x0, double y0) = At(c, s.X0, s.Y0);
                (double x1, double y1) = At(c, s.X1, s.Y1);
                if (c != primary && !SegmentTouches(x0, y0, x1, y1, Widest(dirt) / 2.0, viewport)) continue;
                foreach (RoadStroke k in dirt) d.Line(x0, y0, x1, y1, k.Color, k.Width, k.Dash);
                if (!first) extra++;
                first = false;
            }
            pieces.Add(new PathPiece(s.Edge, -1, EdgeTypes.DirtPath, 0.0, 1.0, d.Commands.Count - before));
        }

        foreach (RouteLensView r in p.Routes)
        {
            // The modernized fraction runs from A (the lower settlement id) toward B: the state records
            // how much of the upgrade is done, not where, so the lens fixes one deterministic end.
            double t = r.IsPartial ? r.Modernization : 0.0;
            (int Cls, double From, double To)[] parts = t > 0.0
                ? [(r.TargetClass, 0.0, t), (r.EdgeType, t, 1.0)]
                : [(r.EdgeType, 0.0, 1.0)];
            var styles = new IReadOnlyList<RoadStroke>[parts.Length];
            var counts = new int[parts.Length];
            int layers = 0;
            double widest = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                styles[i] = Style(parts[i].Cls);
                layers = Math.Max(layers, styles[i].Count);
                widest = Math.Max(widest, Widest(styles[i]));
            }
            int primary = Primary(r.X0, r.Y0, r.X1, r.Y1);
            bool first = true;
            foreach (int c in ks)
            {
                var ends = new (double X0, double Y0, double X1, double Y1)[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    (double ax, double ay) = At(c, r.X0 + (r.X1 - r.X0) * parts[i].From, r.Y0 + (r.Y1 - r.Y0) * parts[i].From);
                    (double bx, double by) = At(c, r.X0 + (r.X1 - r.X0) * parts[i].To, r.Y0 + (r.Y1 - r.Y0) * parts[i].To);
                    ends[i] = (ax, ay, bx, by);
                }
                if (c != primary && !SegmentTouches(ends[0].X0, ends[0].Y0, ends[^1].X1, ends[^1].Y1, widest / 2.0, viewport)) continue;
                // Casings of every piece first, then fills, then marks — the two halves of a partial route
                // read as one road whose bed changes at the split.
                for (int k = 0; k < layers; k++)
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (k >= styles[i].Count) continue;
                        RoadStroke s = styles[i][k];
                        d.Line(ends[i].X0, ends[i].Y0, ends[i].X1, ends[i].Y1, s.Color, s.Width, s.Dash);
                        counts[i]++;
                    }
                if (!world && town > 0)
                {
                    int before = d.Commands.Count;
                    Func<double, double, (double X, double Y)> copy = c == 0 ? toScreen : (x, y) => At(c, x, y);
                    if (Label(d, m, r, t, copy, viewport, ink)) { d.FadeFrom(before, town); counts[^1]++; }
                }
                if (!first) extra++;
                first = false;
            }
            for (int i = 0; i < parts.Length; i++)
                pieces.Add(new PathPiece(-1, r.Id, parts[i].Cls, parts[i].From, parts[i].To, counts[i]));
        }
        return extra;
    }

    /// <summary>WS3: whether a segment (screen px) widened by <paramref name="halfWidth"/> either side, with the replay's
    /// fringe, can touch <paramref name="viewport"/> (its bounding box does).</summary>
    private static bool SegmentTouches(double x0, double y0, double x1, double y1, double halfWidth, RectD viewport)
    {
        double pad = halfWidth + DrawListCull.FringePx;
        double lx = Math.Min(x0, x1) - pad, ly = Math.Min(y0, y1) - pad;
        return DrawListCull.Touches(new RectD(lx, ly, Math.Max(x0, x1) + pad - lx, Math.Max(y0, y1) + pad - ly), viewport);
    }

    /// <summary>The route's name ("Paved road"; a partial route "Built road - 40% to Paved road") at the
    /// midpoint of its visible part, beside the line. False (nothing drawn) when no part is visible.</summary>
    private static bool Label(DrawList d, ITextMeasure m, RouteLensView r, double t,
        Func<double, double, (double X, double Y)> toScreen, RectD viewport, MapInk ink)
    {
        (double ax, double ay) = toScreen(r.X0, r.Y0);
        (double bx, double by) = toScreen(r.X1, r.Y1);
        if (!Clip(ref ax, ref ay, ref bx, ref by, viewport)) return false;
        double mx = (ax + bx) / 2, my = (ay + by) / 2;
        double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);
        double nx = len > 0 ? -dy / len : 0, ny = len > 0 ? dx / len : -1;
        if (ny > 0) { nx = -nx; ny = -ny; }   // always the side above the line
        string text = r.ClassName;
        if (t > 0.0)
        {
            int pct = Math.Clamp((int)Math.Round(t * 100.0, MidpointRounding.AwayFromZero), 1, 99);
            text += " - " + pct.ToString(CultureInfo.InvariantCulture) + "% to " + r.TargetName;
        }
        const double h = 11;
        double half = m.Width(text, h, FontRole.Caps) / 2;
        // Beside the line on its upper side: the text box's centre is pushed along the normal until its
        // nearest corner clears the road (half the widest stroke, 4.5 px) by 6 px — a box of half-extents
        // (half, h/2) reaches half·|nx| + (h/2)·|ny| toward the line.
        double dist = 4.5 + 6 + half * Math.Abs(nx) + h / 2 * Math.Abs(ny);
        d.Text(mx + nx * dist, my + ny * dist - h / 2, text, h, ink.Ink, TextAlign.Center, FontRole.Caps);
        return true;
    }

    /// <summary>Liang–Barsky: clips the segment to the rectangle in place; false when nothing remains.</summary>
    private static bool Clip(ref double x0, ref double y0, ref double x1, ref double y1, RectD r)
    {
        double t0 = 0, t1 = 1, dx = x1 - x0, dy = y1 - y0;
        double[] p = [-dx, dx, -dy, dy];
        double[] q = [x0 - r.X, r.Right - x0, y0 - r.Y, r.Bottom - y0];
        for (int i = 0; i < 4; i++)
        {
            if (p[i] == 0) { if (q[i] < 0) return false; continue; }
            double u = q[i] / p[i];
            if (p[i] < 0) { if (u > t1) return false; if (u > t0) t0 = u; }
            else { if (u < t0) return false; if (u < t1) t1 = u; }
        }
        double nx0 = x0 + t0 * dx, ny0 = y0 + t0 * dy, nx1 = x0 + t1 * dx, ny1 = y0 + t1 * dy;
        x0 = nx0; y0 = ny0; x1 = nx1; y1 = ny1;
        return true;
    }
}
