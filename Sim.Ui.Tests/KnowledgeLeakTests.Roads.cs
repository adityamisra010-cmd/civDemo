using System.Globalization;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M6 PLAYTEST FIX (Director, 2026-10-29) — NO ROAD OVER UNKNOWN LAND. The playtest screenshot showed built routes drawn
/// across the fog: the projection admits a route when both its ENDS are known (P7), and the road lens drew the whole line
/// between them, over lattice nodes the player has never seen. The lens now draws — and names — a route only over KNOWN
/// nodes (<see cref="RoadLens.KnownSpans"/>; remembered land is known). These tests run on the REAL fog (the fog rig, the
/// shipped config through the production seam): a route planted between two of the player's own known settlements whose
/// straight line crosses unknown land draws no stroke, and no name, over an unknown node; changing what lies only on unknown
/// nodes leaves the road draw list identical (with its all-known positive control); and with everything known the road
/// draw list is exactly the uncut one.
/// </summary>
public partial class KnowledgeLeakTests
{
    /// <summary>The fog rig's world after the step with a travelled route planted between the two KNOWN player settlements
    /// whose straight line crosses the most UNKNOWN land (ties: the first pair in table order); <paramref name="target"/> and
    /// <paramref name="modernization"/> make it a partial route. Returns the route's id.</summary>
    private static int PlantRouteAcrossTheFog(WorldState w, PlayerKnowledge k, int target, double modernization)
    {
        WorldProjection p = WorldProjection.Build(w, FogPlayed.Value.Config, FogPlayed.Value.Name, Player, knowledge: k);
        Assert.NotNull(p.KnownNodes);   // the real fog: something is unknown
        int bestA = -1, bestB = -1, bestUnknown = 0;
        for (int i = 0; i < p.Settlements.Count; i++)
            for (int j = i + 1; j < p.Settlements.Count; j++)
            {
                SettlementLensView a = p.Settlements[i], b = p.Settlements[j];
                int lo = Math.Min(a.Id, b.Id), hi = Math.Max(a.Id, b.Id);
                if (!k.KnowsSettlement(lo) || !k.KnowsSettlement(hi)) continue;
                int unknown = UnknownSamples(p, k, a.X, a.Y, b.X, b.Y);
                if (unknown > bestUnknown) { bestUnknown = unknown; bestA = lo; bestB = hi; }   // strictly more: table order breaks a tie
            }
        Assert.True(bestUnknown > 0, "no two known settlements of the fog rig have unknown land between them - the test is void");
        int edge = 0;
        for (int i = 0; i < w.TransportEdges.Count; i++) edge = Math.Max(edge, w.TransportEdges[i].Id + 1);
        w.TransportEdges.Add(new TransportEdgeRow(edge, new SettlementId(bestA), new SettlementId(bestB), EdgeTypes.Trackway, TransportModes.Road,
            TransportEdgeStates.Complete, 1000, 120.0, 0, 1, 1, target, modernization, 1.0));
        return edge;
    }

    /// <summary>How many of 256 points along (x0, y0) → (x1, y1) lie on lattice nodes <paramref name="k"/> does not know.</summary>
    private static int UnknownSamples(WorldProjection p, PlayerKnowledge k, double x0, double y0, double x1, double y1)
    {
        int unknown = 0;
        for (int i = 0; i < 256; i++)
        {
            double t = (i + 0.5) / 256.0;
            if (!k.Known(NodeAt(p, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))) unknown++;
        }
        return unknown;
    }

    /// <summary>The lattice node holding world point (x, y): x on the cylinder's circumference, the last row and column
    /// reaching the map's edge.</summary>
    private static int NodeAt(WorldProjection p, double x, double y)
    {
        if (p.WrapX) x -= Math.Floor(x / p.WorldSize) * p.WorldSize;
        int col = Math.Clamp((int)Math.Floor(x / p.Stride), 0, p.LatticeSize - 1);
        int row = Math.Clamp((int)Math.Floor(y / p.Stride), 0, p.LatticeSize - 1);
        return row * p.LatticeSize + col;
    }

    /// <summary>A flat camera framing <paramref name="route"/> with a 100 px margin: world → screen, its inverse and the
    /// viewport.</summary>
    private sealed record RoadCamera(double MinX, double MinY, double Zoom, RectD Viewport)
    {
        public static RoadCamera Framing(RouteLensView route)
        {
            double minX = Math.Min(route.X0, route.X1), minY = Math.Min(route.Y0, route.Y1);
            double spanX = Math.Abs(route.X1 - route.X0), spanY = Math.Abs(route.Y1 - route.Y0);
            double zoom = 1000.0 / Math.Max(1.0, Math.Max(spanX, spanY));
            return new RoadCamera(minX, minY, zoom, new RectD(0, 0, spanX * zoom + 200, spanY * zoom + 200));
        }
        public (double X, double Y) ToScreen(double x, double y) => ((x - MinX) * Zoom + 100, (y - MinY) * Zoom + 100);
        public (double X, double Y) ToWorld(double x, double y) => ((x - 100) / Zoom + MinX, (y - 100) / Zoom + MinY);
    }

    /// <summary>The road layer alone (<see cref="RoadLens.PaintNetwork"/>) at one band: its draw list and its pieces.</summary>
    private static (DrawList D, List<PathPiece> Pieces) RoadPaint(WorldProjection p, WorldZoom zoom, RoadCamera cam)
    {
        var d = new DrawList();
        var pieces = new List<PathPiece>();
        RoadLens.PaintNetwork(d, ApproxTextMeasure.Instance, p, zoom, zoom == WorldZoom.Settlement ? 1.0 : 0.0, cam.ToScreen,
            cam.Viewport, MapInk.Default, pieces);
        return (d, pieces);
    }

    private static readonly WorldZoom[] RoadBands = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    [Fact]
    public void ARouteBetweenKnownEnds_DrawsNoPieceAndNoName_OverUnknownLand()
    {
        // THE PLAYTEST FINDING, on the real fog: both ends known, the middle across land the player has never seen. Every
        // stroke the road layer draws (routes and dirt-path steps alike) lies over known nodes along its whole length; the
        // route's name sits on a known stretch; the route is drawn (some of it is known) and CUT (not all of it is drawn).
        Rig r = FogPlayed.Value;
        WorldState w = r.Next.Clone();
        PlayerKnowledge k = Gate(Lens.Fog, w);
        int id = PlantRouteAcrossTheFog(w, k, EdgeTypes.BuiltRoad, 0.35);
        WorldProjection p = WorldProjection.Build(w, r.Config, r.Name, Player, knowledge: k);
        RouteLensView route = Assert.Single(p.Routes, x => x.Id == id);
        Assert.True(UnknownSamples(p, k, route.X0, route.Y0, route.X1, route.Y1) > 0, "the planted route crosses no unknown node - void");
        Assert.True(k.Known(NodeAt(p, route.X0, route.Y0)) && k.Known(NodeAt(p, route.X1, route.Y1)), "the route's ends must be on known land");
        RoadCamera cam = RoadCamera.Framing(route);
        int named = 0;
        foreach (WorldZoom zoom in RoadBands)
        {
            (DrawList d, List<PathPiece> pieces) = RoadPaint(p, zoom, cam);
            int lines = 0;
            foreach (DrawCmd c in d.Commands)
            {
                if (c is LineCmd l)
                {
                    lines++;
                    (double ax, double ay) = cam.ToWorld(l.X0, l.Y0);
                    (double bx, double by) = cam.ToWorld(l.X1, l.Y1);
                    int unknown = UnknownSamples(p, k, ax, ay, bx, by);
                    Assert.True(unknown == 0, zoom + ": a road stroke (" + R(ax) + "," + R(ay) + " > " + R(bx) + "," + R(by) + ") crosses "
                        + unknown.ToString(CultureInfo.InvariantCulture) + " samples of unknown land");
                }
                else if (c is TextCmd text)
                {
                    // A route's name is pushed off its line along the upper normal by RoadLens.Label's distance: undo that for
                    // the route it names, and the anchor — the midpoint of the stretch it names — is on known land.
                    bool anchored = false;
                    foreach (RouteLensView x in p.Routes)
                    {
                        double dx = x.X1 - x.X0, dy = x.Y1 - x.Y0, len = Math.Sqrt(dx * dx + dy * dy);
                        if (!(len > 0)) continue;
                        double nx = -dy / len, ny = dx / len;
                        if (ny > 0) { nx = -nx; ny = -ny; }
                        double half = ApproxTextMeasure.Instance.Width(text.Text, text.Size, FontRole.Caps) / 2;
                        double dist = 4.5 + 6 + half * Math.Abs(nx) + text.Size / 2 * Math.Abs(ny);
                        (double mx, double my) = cam.ToWorld(text.X - nx * dist, text.Y + text.Size / 2 - ny * dist);
                        if (RoadLens.DistanceToSegment(mx, my, x.X0, x.Y0, x.X1, x.Y1) > 1e-6) continue;
                        anchored = true;
                        if (x.Id == id) named++;
                        Assert.True(k.Known(NodeAt(p, mx, my)), zoom + ": route " + x.Id.ToString(CultureInfo.InvariantCulture) + "'s name is anchored over unknown land");
                        break;
                    }
                    Assert.True(anchored, zoom + ": a name on the road layer is anchored on no route");
                }
            }
            Assert.True(lines > 0);
            var mine = pieces.Where(x => x.Route == id).ToList();
            Assert.NotEmpty(mine);
            double drawn = 0;
            foreach (PathPiece piece in mine)
            {
                Assert.True(piece.Commands > 0);
                Assert.True(UnknownSamples(p, k, route.X0 + (route.X1 - route.X0) * piece.From, route.Y0 + (route.Y1 - route.Y0) * piece.From,
                    route.X0 + (route.X1 - route.X0) * piece.To, route.Y0 + (route.Y1 - route.Y0) * piece.To) == 0, zoom + ": a route piece lies over unknown land");
                drawn += piece.To - piece.From;
            }
            Assert.True(drawn < 1.0, zoom + ": the whole route was drawn - nothing over the unknown land was cut");
            // The partial split survives the cut: both classes still drawn on their own known stretches.
            Assert.Contains(mine, x => x.EdgeType == EdgeTypes.BuiltRoad && x.To <= 0.35);
            Assert.Contains(mine, x => x.EdgeType == EdgeTypes.Trackway && x.From >= 0.35);
            // The baseline dirt steps the layer drew: both ends on known nodes (P7).
            foreach (PathPiece piece in pieces.Where(x => x.NetworkEdge >= 0))
            {
                RoadSegment s = Assert.Single(p.Roads, x => x.Edge == piece.NetworkEdge);
                Assert.True(k.Known(NodeAt(p, s.X0, s.Y0)) && k.Known(NodeAt(p, s.X1, s.Y1)));
            }
        }
        Assert.True(named > 0, "the route was never named - its label was not checked");

        // THE CONTROL: all-known, the same route is drawn whole, over the land the fog hides.
        WorldProjection all = WorldProjection.Build(w, r.Config, r.Name, Player, knowledge: All(w));
        (_, List<PathPiece> whole) = RoadPaint(all, WorldZoom.World, cam);
        Assert.Equal(new (double From, double To)[] { (0.0, 0.35), (0.35, 1.0) }, whole.Where(x => x.Route == id).Select(x => (x.From, x.To)).ToArray());
    }

    [Fact]
    public void ChangingOnlyWhatLiesOnUnknownNodes_LeavesTheRoadDrawListIdentical()
    {
        // METAMORPHIC, on the real fog. World A: the planted partial route, its modernization split inside an unknown stretch
        // of it. World B: A with, only on unknown land, the split moved within that stretch, the terrain under every unknown
        // node changed, dirt-path steps planted on unknown nodes (one with a known end) and routes planted to settlements the
        // player does not know. The road draw list (routes, steps and names; every band) is IDENTICAL. Names (Settlement zoom)
        // state the route's own progress, so the split is moved only at the unnamed bands; the rest everywhere. CONTROL:
        // all-known, the same changes do change the road draw list.
        Rig r = FogPlayed.Value;
        WorldState a = r.Next.Clone();
        PlayerKnowledge ka = Gate(Lens.Fog, a);
        int id = PlantRouteAcrossTheFog(a, ka, EdgeTypes.Trackway, 0.0);
        WorldProjection p0 = WorldProjection.Build(a, r.Config, r.Name, Player, knowledge: ka);
        RouteLensView route = Assert.Single(p0.Routes, x => x.Id == id);
        // The longest run of UNKNOWN samples along the route (1024 samples; ties: the first) — measured on the knowledge itself,
        // not through the lens under test; the split moves between two samples strictly inside it.
        const int n = 1024;
        int runFrom = -1, runLength = 0;
        for (int i = 0, start = -1; i <= n; i++)
        {
            bool unknownHere = i < n && !ka.Known(NodeAt(p0, route.X0 + (route.X1 - route.X0) * ((i + 0.5) / n), route.Y0 + (route.Y1 - route.Y0) * ((i + 0.5) / n)));
            if (unknownHere) { if (start < 0) start = i; continue; }
            if (start >= 0 && i - start > runLength) { runFrom = start; runLength = i - start; }
            start = -1;
        }
        Assert.True(runLength >= 4, "the planted route has no unknown stretch - void");
        double gapFrom = (runFrom + 1.5) / n, gapTo = (runFrom + runLength - 1.5) / n;
        double t1 = gapFrom + (gapTo - gapFrom) / 3.0, t2 = gapFrom + 2.0 * (gapTo - gapFrom) / 3.0;
        SetModernization(a, id, EdgeTypes.BuiltRoad, t1);

        WorldState Changed(double split)
        {
            WorldState b = a.Clone();
            SetModernization(b, id, EdgeTypes.BuiltRoad, split);
            PlayerKnowledge kb = Gate(Lens.Fog, b);
            b.Terrain = UnderUnknownNodes(b.Terrain!, kb, 1);
            // Dirt-path steps on unknown nodes: one with both ends unknown, one from the route's known end into the unknown.
            int lsize = kb.Core.LatticeSize;
            var unknown = new List<int>();
            for (int n = 0; n < lsize * lsize && unknown.Count < 3; n++) if (!kb.Known(n)) unknown.Add(n);
            Assert.Equal(3, unknown.Count);
            int knownEnd = -1;
            for (int i = 0; i < b.Settlements.Count; i++) if (b.Settlements[i].Id.Value == route.A) knownEnd = kb.Core.NodeOfCell(b.Settlements[i].SiteCell);
            Assert.True(knownEnd >= 0 && kb.Known(knownEnd));
            int node = 0, step = 0;
            for (int i = 0; i < b.NetworkNodes.Count; i++) node = Math.Max(node, b.NetworkNodes[i].Id.Value + 1);
            for (int i = 0; i < b.NetworkEdges.Count; i++) step = Math.Max(step, b.NetworkEdges[i].Id.Value + 1);
            foreach (int n in new[] { unknown[0], unknown[1], knownEnd, unknown[2] }) b.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(node++), n));
            b.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(step++), new NetworkNodeId(node - 4), new NetworkNodeId(node - 3), EdgeTypes.DirtPath, 1.0));
            b.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(step++), new NetworkNodeId(node - 2), new NetworkNodeId(node - 1), EdgeTypes.DirtPath, 1.0));
            // Routes to settlements the player does not know: from its own route's end, and between two of the AI's.
            IReadOnlyList<int> hidden = KnowledgeStubs.SettlementsOf(b, Hidden);
            Assert.True(hidden.Count >= 2 && hidden.All(h => kb.IsHiddenSettlement(h)));
            int edge = 0;
            for (int i = 0; i < b.TransportEdges.Count; i++) edge = Math.Max(edge, b.TransportEdges[i].Id + 1);
            foreach ((int x, int y) in new[] { (route.A, hidden[0]), (hidden[0], hidden[1]) })
                b.TransportEdges.Add(new TransportEdgeRow(edge++, new SettlementId(Math.Min(x, y)), new SettlementId(Math.Max(x, y)), EdgeTypes.PavedRoad,
                    TransportModes.Road, TransportEdgeStates.Complete, 1000, 90.0, 0, 1, 1, EdgeTypes.PavedRoad, 0.0, 1.0));
            return b;
        }

        WorldState moved = Changed(t2), kept = Changed(t1);
        RoadCamera cam = RoadCamera.Framing(route);
        foreach (WorldZoom zoom in RoadBands)
        {
            WorldState b = zoom == WorldZoom.Settlement ? kept : moved;
            string sa = RoadSvg(WorldProjection.Build(a, r.Config, r.Name, Player, knowledge: Gate(Lens.Fog, a)), zoom, cam);
            string sb = RoadSvg(WorldProjection.Build(b, r.Config, r.Name, Player, knowledge: Gate(Lens.Fog, b)), zoom, cam);
            Assert.True(sa == sb, zoom + ": the road draw list shows what lies on unknown land");
            string ca = RoadSvg(WorldProjection.Build(a, r.Config, r.Name, Player, knowledge: All(a)), zoom, cam);
            string cb = RoadSvg(WorldProjection.Build(b, r.Config, r.Name, Player, knowledge: All(b)), zoom, cam);
            Assert.True(ca != cb, zoom + ": the changes do not reach the all-known road draw list - the comparison proves nothing");
        }
    }

    [Fact]
    public void WithEverythingKnown_TheRoadDrawListIsTheUncutOne()
    {
        // THE SWITCH-OFF CONTROL. The fog-off world (discovery disabled: the P7 rig, TestFog.PreDiscovery) with a partial route
        // planted: its projection carries no knowledge mask, every route is drawn whole — one piece per part, exactly [0, t] and
        // [t, 1] — and the road draw list is byte-identical to the same projection given an ALL-TRUE mask (the cutting path
        // taken, nothing cut), at every band.
        Rig r = Played.Value;
        WorldState w = PlantedNext(Lens.Stub, 1);
        int id = w.TransportEdges[w.TransportEdges.Count - 1].Id;
        SetModernization(w, id, EdgeTypes.PavedRoad, 0.4);
        Assert.True(All(w).AllKnown);
        WorldProjection p = WorldProjection.Build(w, r.Config, r.Name, Player);
        Assert.Null(p.KnownNodes);
        Assert.NotEmpty(p.Routes);
        var mask = new bool[p.LatticeSize * p.LatticeSize];
        Array.Fill(mask, true);
        WorldProjection masked = new()
        {
            WorldSize = p.WorldSize, WrapX = p.WrapX, KmPerPx = p.KmPerPx, Settlements = p.Settlements, Units = p.Units, Roads = p.Roads,
            Routes = p.Routes, Territory = p.Territory, PlayerPolity = p.PlayerPolity, PlayerAge = p.PlayerAge, PlayerAgeName = p.PlayerAgeName,
            Absent = p.Absent, KnownNodes = mask, LatticeSize = p.LatticeSize, Stride = p.Stride,
        };
        RouteLensView route = Assert.Single(p.Routes, x => x.Id == id);
        RoadCamera cam = RoadCamera.Framing(route);
        foreach (RouteLensView x in p.Routes) Assert.Equal(new (double From, double To)[] { (0.0, 1.0) }, RoadLens.KnownSpans(x, masked));
        foreach (WorldZoom zoom in RoadBands)
        {
            (DrawList d0, List<PathPiece> pieces0) = RoadPaint(p, zoom, cam);
            (DrawList d1, List<PathPiece> pieces1) = RoadPaint(masked, zoom, cam);
            Assert.Equal(d0.Commands, d1.Commands);
            Assert.Equal(SvgWriter.Write(d0, cam.Viewport.W, cam.Viewport.H), SvgWriter.Write(d1, cam.Viewport.W, cam.Viewport.H));
            Assert.Equal(pieces0, pieces1);
            Assert.Equal(new (double From, double To)[] { (0.0, 0.4), (0.4, 1.0) }, pieces0.Where(x => x.Route == id).Select(x => (x.From, x.To)).ToArray());
            foreach (RouteLensView x in p.Routes)
            {
                var mine = pieces0.Where(q => q.Route == x.Id).ToList();
                Assert.Equal(x.IsPartial ? 2 : 1, mine.Count);
                Assert.Equal(0.0, mine[0].From);
                Assert.Equal(1.0, mine[^1].To);
            }
        }
    }

    /// <summary>Sets route <paramref name="id"/>'s modernization toward <paramref name="target"/>.</summary>
    private static void SetModernization(WorldState w, int id, int target, double modernization)
    {
        for (int i = 0; i < w.TransportEdges.Count; i++)
            if (w.TransportEdges[i].Id == id) w.TransportEdges[i] = w.TransportEdges[i] with { TargetClass = target, Modernization = modernization };
    }

    /// <summary>The road layer's draw list at one band, as SVG.</summary>
    private static string RoadSvg(WorldProjection p, WorldZoom zoom, RoadCamera cam) =>
        SvgWriter.Write(RoadPaint(p, zoom, cam).D, cam.Viewport.W, cam.Viewport.H);
}
