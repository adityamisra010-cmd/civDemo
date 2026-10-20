using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>
/// M5 S1d — WHAT THE PLACEMENT MODE PAINTS, as pure DrawList painters (the lens / InfoPanel precedent): over the map, the
/// route the reachability arm judges from the source to the inspected place (lattice node centres, the geometry the lens
/// draws paths with), the hovered place's footprint in the verdict's ink, and the chosen place's ring; beside the pointer,
/// the painted hint (never an ImGui tooltip). They read only the <see cref="PlacementMode"/> and the frontier lattice's
/// geometry: nothing here reads or writes the simulation.
/// </summary>
public static class PlacementOverlay
{
    /// <summary>The smallest radius (screen px at UI scale 1) a place's mark is drawn with, so a zoomed-out place is visible.</summary>
    public const double MinMarkPx = 7.0;

    /// <summary>A lattice node's centre in world (terrain) px: the block it aggregates, as the lens places paths.</summary>
    public static (double X, double Y) NodeCentre(TraversalLattice lattice, int node)
    {
        ArgumentNullException.ThrowIfNull(lattice);
        (int x, int y) = lattice.Coords(node);
        return (x * lattice.Stride + lattice.Stride / 2.0, y * lattice.Stride + lattice.Stride / 2.0);
    }

    /// <summary>The ink a verdict is shown in: the positive ink when the predicate answers Legal, else the danger ink.</summary>
    public static Rgba VerdictInk(EraTheme t, FoundingVerdict? v) => v == FoundingVerdict.Legal ? t.TextInk.Positive : t.TextInk.Danger;

    /// <summary>
    /// Paints the map marks of <paramref name="p"/> through <paramref name="toScreen"/> (world px → screen px): the route
    /// (when the mode has one to show), the hovered place's footprint and the chosen place's ring. Returns the number of
    /// commands it added (0 when the mode is off).
    /// <para>WS3: with <paramref name="wrapX"/> (a wrapped world; the game passes its camera's flag) the route is UNWRAPPED
    /// node by node (each centre at its copy nearest the previous one — <see cref="MapTopology.UnwrapNear"/>), so a route
    /// across the seam is one contiguous line the short way; the route and each mark are drawn at their copy nearest the
    /// centre of <paramref name="viewport"/> and at every other copy (world x ± the map's width) that touches it. Flat: exactly
    /// as before.</para>
    /// </summary>
    public static int PaintMap(DrawList d, EraTheme t, MapInk ink, Func<double, double, (double X, double Y)> toScreen,
        int terrainSize, TraversalLattice? lattice, PlacementMode p, double uiScale, bool wrapX = false, RectD? viewport = null)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(ink);
        ArgumentNullException.ThrowIfNull(toScreen);
        ArgumentNullException.ThrowIfNull(p);
        int before = d.Commands.Count;
        if (!p.Active || terrainSize <= 0) return 0;
        double s = Math.Max(0.5, uiScale);
        bool wrap = wrapX && viewport is not null;
        int[] ks = wrap ? [-1, 0, 1] : [0];
        Func<double, double, (double X, double Y)> Copy(int c) => c == 0 ? toScreen : (x, y) => toScreen(x + c * terrainSize, y);
        // The copies to draw an element of world x-extent [x0, x1] at: flat {0}; wrapped, the one nearest the viewport's
        // centre and every other whose screen span (padded by pad px) touches the viewport.
        List<int> Copies(double x0, double x1, double y, double pad)
        {
            var list = new List<int>(3);
            if (!wrap) { list.Add(0); return list; }
            RectD v = viewport!.Value;
            int primary = 0;
            double best = double.PositiveInfinity;
            foreach (int c in ks)
            {
                double mid = (Copy(c)(x0, y).X + Copy(c)(x1, y).X) / 2.0, dd = Math.Abs(mid - v.CenterX);
                if (dd < best) { best = dd; primary = c; }
            }
            foreach (int c in ks)
            {
                double a = Copy(c)(x0, y).X, b = Copy(c)(x1, y).X;
                if (c == primary || (Math.Max(a, b) + pad >= v.X && Math.Min(a, b) - pad <= v.Right)) list.Add(c);
            }
            return list;
        }

        if (p.Route is { Found: true } route && lattice is not null && route.Nodes.Length > 1)
        {
            var world = new (double X, double Y)[route.Nodes.Length];
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            for (int i = 0; i < world.Length; i++)
            {
                (double wx, double wy) = NodeCentre(lattice, route.Nodes[i]);
                if (wrapX && i > 0) wx = MapTopology.UnwrapNear(wx, world[i - 1].X, terrainSize);   // WS3: the short way, contiguous
                world[i] = (wx, wy);
                lo = Math.Min(lo, wx);
                hi = Math.Max(hi, wx);
            }
            foreach (int c in Copies(lo, hi, world[0].Y, 5.0 * s))
            {
                Func<double, double, (double X, double Y)> at = Copy(c);
                var pts = new (double X, double Y)[world.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = at(world[i].X, world[i].Y);
                d.Polyline(pts, ThemeColor.Alpha(ink.PathCasing, 0.85), 5.0 * s);
                d.Polyline(pts, ink.Selection, 2.5 * s);
            }
        }
        if (p.Hovered >= 0 && p.Hovered != p.Selected)
        {
            (int hx, int hy) = CellXY(p.Hovered, terrainSize);
            foreach (int c in Copies(hx, hx + 1, hy, MinMarkPx * s + 4))
                Mark(d, Copy(c), terrainSize, p.Hovered, VerdictInk(t, p.HoveredVerdict), 1.6 * s, MinMarkPx * s, ring: false);
        }
        if (p.Selected >= 0)
        {
            (int sx, int sy) = CellXY(p.Selected, terrainSize);
            foreach (int c in Copies(sx, sx + 1, sy, MinMarkPx * s * 1.4 + 4))
                Mark(d, Copy(c), terrainSize, p.Selected, VerdictInk(t, p.SelectedVerdict), 2.8 * s, MinMarkPx * s * 1.4, ring: true);
        }
        return d.Commands.Count - before;
    }

    /// <summary>A cell's own column and row (a position, no delta).</summary>
    private static (int X, int Y) CellXY(int cell, int size) => (cell % size, cell / size);

    private static void Mark(DrawList d, Func<double, double, (double X, double Y)> toScreen, int size, int cell, Rgba color,
        double width, double minRadius, bool ring)
    {
        (int x, int y) = CellXY(cell, size);
        (double x0, double y0) = toScreen(x, y);
        (double x1, double y1) = toScreen(x + 1, y + 1);
        double cx = (x0 + x1) / 2.0, cy = (y0 + y1) / 2.0;
        double half = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) / 2.0;
        if (!ring && half >= minRadius)
        {
            d.Rect(new RectD(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0)), ThemeColor.Alpha(color, 0.18), color, width);
            return;
        }
        double r = Math.Max(minRadius, half);
        d.Circle(cx, cy, r, ring ? ThemeColor.Alpha(color, 0.16) : null, color, width);
        if (ring) d.Circle(cx, cy, Math.Max(1.5, r * 0.22), color);
    }

    /// <summary>The hint's lines for the hovered place: the verdict in the player's words, the route and its cost (when shown),
    /// the party and its provisions, and what a click does. Empty when nothing is hovered.</summary>
    public static IReadOnlyList<(string Text, Rgba Ink)> HintLines(PlacementMode p, EraTheme t, TraversalLattice? lattice)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(t);
        if (!p.Active || p.Hovered < 0 || p.HoveredVerdict is not { } v) return [];
        var lines = new List<(string, Rgba)>();
        bool legal = v == FoundingVerdict.Legal;
        lines.Add(((legal ? "Found here: " : "Not here: ") + p.Reason(v, p.Hovered),
            VerdictInk(t, v)));
        if (lattice is not null && p.RouteCell == p.Hovered && p.RouteLine(lattice) is { } route) lines.Add((route, t.Ink.Text));
        if (p.PartyLine.Length > 0) lines.Add((Cap(p.PartyLine), t.Ink.TextSoft));
        lines.Add((legal ? "Click to choose this place, then Confirm." : "Click to keep this place in view and see why.", t.Ink.TextSoft));
        return lines;
    }

    /// <summary>Paints the hint's <paramref name="lines"/> beside the pointer (<paramref name="px"/>, <paramref name="py"/>),
    /// kept inside the viewport, at <paramref name="size"/> px; returns its rect.</summary>
    public static RectD PaintHint(DrawList d, ITextMeasure m, EraTheme t, double px, double py, double viewW, double viewH,
        IReadOnlyList<(string Text, Rgba Ink)> lines, double size)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(lines);
        double w = 0;
        foreach ((string text, _) in lines) w = Math.Max(w, m.Width(t, text, size));
        double line = t.Type.Line(size);
        w += 16;
        double h = line * lines.Count + 8;
        double x = Math.Min(px + 18, viewW - w - 4), y = Math.Min(py + 22, viewH - h - 4);
        var r = new RectD(Math.Max(0, x), Math.Max(0, y), w, h);
        d.Rect(r, ThemeColor.Alpha(t.Material.PanelRaised, 0.95), ThemeColor.Alpha(t.Material.Border, 0.85), 0.9, 3);
        double ty = r.Y + 4;
        foreach ((string text, Rgba ink) in lines)
        {
            d.Write(t, r.X + 8, ty, text, size, ink);
            ty += line;
        }
        return r;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
