using System.Globalization;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// TEST-ONLY FIXTURE — a seed-42 world (256 px, 4 settlements) founded through the real
/// <see cref="UiSession"/> and played 6 End Turns, whose roads are then BUILT BY THE REAL
/// RoadDevelopmentSystem from real <c>DevelopRoads</c> orders (<see cref="RoadDevelopmentQuery.DevelopOrder"/>,
/// appended to the session's order log and stepped by End Turn). Only the preconditions are constructed,
/// through the simulation's own row types, so that every road class can be shown at once:
/// <list type="bullet">
/// <item>KNOWLEDGE — <c>ResearchCompletedRow</c>s for the player (the Sim.Tests <c>EligibleForA2</c>
///   precedent): Age II eligibility (cereal_cultivation, pottery_open_fired) and, phase by phase, each road
///   class's research (track_road; stone_dry; road_paved; motor_road);</item>
/// <item>MATERIALS — an endowment at the capital through <c>Ledger.Flow</c> (source, InitialEndowment; the
///   ResearchRigs.Stock precedent, conserved) of exactly what the phase's plan costs beyond what the
///   civilization holds; the last phase gets a quarter of its cost, so the route is PARTIALLY modernized;</item>
/// <item>FORMATIONS — two <c>MilitaryUnitRow</c>s (Slingers, Scouts) stationed at the capital beside the
///   founding warband, so the token fan-out and the automatic Age-II modernization have more than one
///   formation to act on.</item>
/// </list>
/// The player advances to Age II through the real AdvanceAge order on the first phase. Phases: 100% →
/// every route a Trackway; 50% → three Built roads; 30% → two Paved roads; 15% with a quarter of the
/// materials → one route part-way to Highway. Nothing here is reachable from production code.
/// </summary>
public sealed class RoadWorldFixture
{
    public UiSession Session { get; }
    public int Capital { get; }
    /// <summary>The TransportEdges id of the route left part-way between two classes.</summary>
    public int PartialRoute { get; }
    public IReadOnlyList<string> Log { get; }

    private static readonly PolityId Me = LaborOrderFactory.PlayerEmpire;

    public static void Know(WorldState w, SimConfig cfg, params string[] nodes)
    {
        foreach (string id in nodes)
            w.ResearchCompleted.Add(new ResearchCompletedRow(Me, cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Key));
    }

    public static void Endow(WorldState w, SettlementId at, GoodId good, long qty)
    {
        int row = GoodStockIndex.IndexOf(w.GoodStocks, at, good);
        if (row < 0) row = w.GoodStocks.Add(new GoodStockRow(at, good, Conserved.Zero, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(good),
            ReasonIds.InitialEndowment, qty, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    /// <summary>Endows the capital so the civilization holds <paramref name="share"/> of what the plan for
    /// <paramref name="pct"/>% costs, per good (nothing when it already holds that much).</summary>
    private static void EndowFor(WorldState w, SimConfig cfg, SettlementId cap, double pct, double share)
    {
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(w, cfg.Research!, cfg.Roads!, cfg.Goods!, Me, pct);
        Assert.NotEmpty(plan);
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(w, Me);
        var goods = new List<int>();
        var units = new List<long>();
        foreach (RoadDevelopmentStep step in plan)
            foreach (RoadMaterialCost c in step.Cost)
            {
                int at = goods.IndexOf(c.Good.Value);
                if (at < 0) { goods.Add(c.Good.Value); units.Add(c.Units); }
                else units[at] += c.Units;
            }
        for (int i = 0; i < goods.Count; i++)
        {
            long want = share >= 1.0 ? units[i] : (long)Math.Floor(units[i] * share);
            long have = RoadDevelopmentQuery.CivilizationStock(w.GoodStocks, payers, new GoodId(goods[i]));
            if (want > have) Endow(w, cap, new GoodId(goods[i]), want - have);
        }
    }

    private static UiSession Develop(WorldState w, double pct)
    {
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        s.Orders.Append(RoadDevelopmentQuery.DevelopOrder(s.World, Me, pct));
        s.EndTurn();
        return s;
    }

    public RoadWorldFixture()
    {
        var log = new List<string>();
        UiSession founded = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 6; t++) founded.EndTurn();
        SimConfig cfg = founded.Config;
        WorldState w = founded.World.Clone();
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId cap));
        Capital = cap.Value;

        // Two more formations at the capital (constructed rows), ids after the founding warband's.
        int maxId = 0;
        MilitaryUnitRow warband = default;
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            maxId = Math.Max(maxId, w.MilitaryUnits[i].Id);
            if (w.MilitaryUnits[i].Owner == Me) warband = w.MilitaryUnits[i];
        }
        w.MilitaryUnits.Add(new MilitaryUnitRow(maxId + 1, Me, 4, 401, cap, warband.X, warband.Y, 0.0, 0));   // Slingers
        w.MilitaryUnits.Add(new MilitaryUnitRow(maxId + 2, Me, 1, 101, cap, warband.X, warband.Y, 0.0, 0));   // Scouts

        // Phase 1: Age II through the real AdvanceAge order, and every route a Trackway.
        Know(w, cfg, "cereal_cultivation", "pottery_open_fired", "track_road");
        EndowFor(w, cfg, cap, 100.0, 1.0);
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        Assert.True(s.EmitAdvanceAge(2, cfg.Ages!.Surges[0].Key));
        s.Orders.Append(RoadDevelopmentQuery.DevelopOrder(s.World, Me, 100.0));
        s.EndTurn();
        log.Add($"turn {s.World.Clock.Turn}: age {AgeQuery.CurrentAge(s.World, cfg.Ages, Me)}, routes {s.World.TransportEdges.Count}");

        // Phase 2: Built roads on half the routes; phase 3: Paved roads on two; phase 4: a quarter of the
        // materials for one route toward Highway — a partial modernization (ADR-032 §10.3).
        w = s.World.Clone(); Know(w, cfg, "stone_dry"); EndowFor(w, cfg, cap, 50.0, 1.0); s = Develop(w, 50.0);
        w = s.World.Clone(); Know(w, cfg, "road_paved"); EndowFor(w, cfg, cap, 30.0, 1.0); s = Develop(w, 30.0);
        w = s.World.Clone(); Know(w, cfg, "motor_road"); EndowFor(w, cfg, cap, 15.0, 0.25);
        // Tools are worn by equipped farmers in the production phase of the same step (ProductionSystem,
        // tool wear), before RoadDevelopment runs last: endow the capital's farmers' worth on top, so
        // STONE (which nothing else draws here) is the binding good and the route is part-modernized.
        long pop = 0;
        for (int b = 0; b < w.Buckets.Count; b++) if (w.Buckets[b].Settlement == cap) pop += w.Buckets[b].Count.Value;
        Endow(w, cap, new GoodId(cfg.Goods!.IdOf("tools")), pop);
        s = Develop(w, 15.0);
        Session = s;

        PartialRoute = -1;
        for (int i = 0; i < s.World.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = s.World.TransportEdges[i];
            log.Add(string.Create(CultureInfo.InvariantCulture,
                $"route {e.Id} {e.A.Value}-{e.B.Value}: class {e.EdgeType} target {e.TargetClass} modernized {e.Modernization:0.####} km {e.LengthKm:0.#}"));
            if (e.Modernization > 0.0 && e.TargetClass != e.EdgeType) PartialRoute = e.Id;
        }
        Log = log;
    }
}

/// <summary>
/// Stream U3 (ADR-033 Appendix A; directive "WORLD UI") — THE WORLD MAP SHOWS WHAT IS: roads by class
/// with partial modernization and one owner per piece of ground; one place for map ink
/// (<see cref="MapInk"/>); settlements composed from state with no era gate (D-038 H5); institutions only
/// through <see cref="InstitutionMarkerSource"/>; formation tokens showing the current Age identity and
/// fanning out deterministically; painting never changes the world.
/// </summary>
public class WorldMapStateTests(RoadWorldFixture fx) : IClassFixture<RoadWorldFixture>
{
    private static readonly PolityId Me = LaborOrderFactory.PlayerEmpire;
    private static readonly WorldZoom[] Zooms = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    private WorldProjection Project() =>
        WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me);

    /// <summary>The whole 256-px world at 4 px per world px in a 1024-px viewport: nothing is culled.</summary>
    private static LensFrame PaintAll(DrawList d, WorldProjection p, WorldZoom z, MapInk? ink = null) =>
        WorldLens.Paint(d, ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024), ink: ink);

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    private static WorldProjection With(WorldProjection p, IReadOnlyList<SettlementLensView>? settlements = null,
        IReadOnlyList<UnitLensView>? units = null, IReadOnlyList<RoadSegment>? roads = null, IReadOnlyList<RouteLensView>? routes = null,
        int? playerAge = null) => new()
    {
        WorldSize = p.WorldSize, Settlements = settlements ?? p.Settlements, Units = units ?? p.Units, Roads = roads ?? p.Roads,
        Routes = routes ?? p.Routes, Territory = p.Territory, PlayerPolity = p.PlayerPolity, PlayerAge = playerAge ?? p.PlayerAge,
        PlayerAgeName = p.PlayerAgeName, Absent = p.Absent,
    };

    // ------------------------------------------------------------------ the fixture is what it claims

    [Fact]
    public void Fixture_RealOrdersBuiltSeveralClasses_AndOnePartialRoute()
    {
        WorldState w = fx.Session.World;
        var classes = new SortedSet<int>();
        for (int i = 0; i < w.TransportEdges.Count; i++) classes.Add(w.TransportEdges[i].EdgeType);
        Assert.True(classes.Count >= 3, "classes built: " + string.Join(",", classes) + "\n" + string.Join("\n", fx.Log));
        Assert.Contains(EdgeTypes.Trackway, classes);
        Assert.Contains(EdgeTypes.BuiltRoad, classes);
        Assert.Contains(EdgeTypes.PavedRoad, classes);
        Assert.True(fx.PartialRoute >= 0, string.Join("\n", fx.Log));
        Assert.True(TransportQuery.TryGetEdge(w, fx.PartialRoute, out TransportEdgeRow partial));
        Assert.Equal(EdgeTypes.Highway, partial.TargetClass);
        Assert.True(partial.Modernization > 0.01 && partial.Modernization < 0.99, string.Join("\n", fx.Log));
        Assert.Equal(2, AgeQuery.CurrentAge(w, fx.Session.Config.Ages!, Me));
        Assert.True(w.RoadDevelopments.Count >= w.TransportEdges.Count);   // every row came from a logged development
    }

    // ------------------------------------------------------------------ roads: projection

    [Fact]
    public void Routes_AreTheTravelledTransportEdges_DrawnSiteToSite()
    {
        WorldState w = fx.Session.World;
        WorldProjection p = Project();
        Assert.Equal(w.TransportEdges.Count, p.Routes.Count);
        for (int i = 0; i < w.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = w.TransportEdges[i];
            RouteLensView r = p.Routes[i];
            Assert.Equal((e.Id, e.A.Value, e.B.Value, e.EdgeType, e.TargetClass, e.Modernization, e.LengthKm),
                (r.Id, r.A, r.B, r.EdgeType, r.TargetClass, r.Modernization, r.LengthKm));
            SettlementLensView a = Assert.Single(p.Settlements, s => s.Id == e.A.Value);
            SettlementLensView b = Assert.Single(p.Settlements, s => s.Id == e.B.Value);
            Assert.Equal((a.X, a.Y, b.X, b.Y), (r.X0, r.Y0, r.X1, r.Y1));
            Assert.Equal(RoadLens.ClassName(fx.Session.Config, e.EdgeType), r.ClassName);
        }
        Assert.Equal("Trackway", RoadLens.ClassName(fx.Session.Config, EdgeTypes.Trackway));
        Assert.Equal("Paved road", RoadLens.ClassName(fx.Session.Config, EdgeTypes.PavedRoad));
        Assert.Equal("Dirt path", RoadLens.ClassName(fx.Session.Config, EdgeTypes.DirtPath));
        Assert.DoesNotContain(p.Absent, a => a.StartsWith("Roads", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ roads: one stroke owner per piece

    /// <summary>The Paths layer's accounting at every zoom: the pieces' commands sum to the layer's draw
    /// count; every route is drawn as ONE piece, or as two complementary pieces when partial; every
    /// baseline step is drawn exactly once unless a route's corridor covers it, and then never.</summary>
    private static void AssertOnePieceOwnerPerGround(WorldProjection p, LensFrame f, WorldZoom z)
    {
        int sum = 0;
        foreach (PathPiece piece in f.Paths) { sum += piece.Commands; Assert.True(piece.Commands > 0); }
        Assert.Equal(f.LayerDraws[(int)MapLayer.Paths], sum);
        foreach (RouteLensView r in p.Routes)
        {
            var mine = f.Paths.Where(x => x.Route == r.Id).OrderBy(x => x.From).ToList();
            if (r.IsPartial)
            {
                Assert.Equal(2, mine.Count);
                Assert.Equal((r.TargetClass, 0.0, r.Modernization), (mine[0].EdgeType, mine[0].From, mine[0].To));
                Assert.Equal((r.EdgeType, r.Modernization, 1.0), (mine[1].EdgeType, mine[1].From, mine[1].To));
            }
            else
                Assert.Equal((r.EdgeType, 0.0, 1.0), (Assert.Single(mine).EdgeType, mine[0].From, mine[0].To));
            foreach (PathPiece piece in mine)
            {
                int strokes = RoadLens.StyleOf(piece.EdgeType, z, MapInk.Default).Count;
                int label = z == WorldZoom.Settlement && piece == mine[^1] ? 1 : 0;   // at most one name per route
                Assert.InRange(piece.Commands, strokes, strokes + label);
            }
        }
        foreach (RoadSegment s in p.Roads)
        {
            int drawn = f.Paths.Count(x => x.NetworkEdge == s.Edge);
            Assert.Equal(s.CoveredBy >= 0 ? 0 : 1, drawn);
        }
        Assert.Equal(p.Routes.Count, f.Paths.Where(x => x.Route >= 0).Select(x => x.Route).Distinct().Count());
    }

    [Fact]
    public void RoadDrawAccounting_OneStrokeOwnerPerPiece_AtEveryZoom()
    {
        WorldProjection p = Project();
        Assert.NotEmpty(p.Routes);
        foreach (WorldZoom z in Zooms)
        {
            var d = new DrawList();
            LensFrame f = PaintAll(d, p, z);
            AssertOnePieceOwnerPerGround(p, f, z);
            // Every command of the frame is charged to exactly one map layer, and routes are visible at
            // World zoom as a network (major infrastructure), not only when zoomed in.
            Assert.Equal(d.Commands.Count, f.LayerDraws.Sum());
            if (z == WorldZoom.World) Assert.All(p.Routes, r => Assert.Contains(f.Paths, x => x.Route == r.Id && x.Commands > 0));
        }
    }

    /// <summary>Lays a dirt-path baseline ALONG <paramref name="route"/> in <paramref name="w"/>: a lattice
    /// chain of NetworkNodes/NetworkEdges (rows of the simulation's own type) through the lattice nodes
    /// nearest the route's centre line, end to end. Returns (lattice stride, edges added).</summary>
    private (int Stride, int Added) LayBaselineAlong(WorldState w, RouteLensView route)
    {
        var lattice = Sim.Core.Pathing.TraversalLattice.Build(w.Terrain!, fx.Session.Config.Transport.RiverCostFactor);
        int stride = w.Terrain!.Size / lattice.Size;
        int Node(double x, double y) => (int)(y / stride) * lattice.Size + (int)(x / stride);
        var chain = new List<int>();
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(route.X1 - route.X0), Math.Abs(route.Y1 - route.Y0)) / stride) * 2;
        for (int i = 0; i <= n; i++)
        {
            int node = Node(route.X0 + (route.X1 - route.X0) * i / n, route.Y0 + (route.Y1 - route.Y0) * i / n);
            if (chain.Count == 0 || chain[^1] != node) chain.Add(node);
        }
        int nextNode = 0, nextEdge = 0;
        for (int i = 0; i < w.NetworkNodes.Count; i++) nextNode = Math.Max(nextNode, w.NetworkNodes[i].Id.Value + 1);
        for (int i = 0; i < w.NetworkEdges.Count; i++) nextEdge = Math.Max(nextEdge, w.NetworkEdges[i].Id.Value + 1);
        int added = 0, prevId = -1;
        foreach (int node in chain)
        {
            int id = nextNode++;
            w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(id), node));
            if (prevId >= 0) { w.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(nextEdge++), new NetworkNodeId(prevId), new NetworkNodeId(id), EdgeTypes.DirtPath, 1.0)); added++; }
            prevId = id;
        }
        return (stride, added);
    }

    /// <summary>A real world whose dirt-path baseline runs ALONG a built route (laid by
    /// <see cref="LayBaselineAlong"/> between the capital and another settlement). The route draws that
    /// ground; the baseline steps inside its corridor are not drawn again; the steps outside it still are,
    /// once each.</summary>
    [Fact]
    public void RoadOverTheBaselineCorridor_IsNotDrawnTwice()
    {
        WorldState w = fx.Session.World.Clone();
        RouteLensView route = Project().Routes.First(r => r.A == fx.Capital || r.B == fx.Capital);
        (int stride, int added) = LayBaselineAlong(w, route);
        Assert.True(added >= 10, "chain too short to prove anything");

        WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        var covered = p.Roads.Where(s => s.CoveredBy >= 0).ToList();
        Assert.True(covered.Count >= added / 2, $"{covered.Count} of {added} corridor steps owned by the route");
        foreach (RoadSegment s in p.Roads)
        {
            // Covered exactly when inside some route's corridor; by the lowest such route id.
            var owners = p.Routes.Where(r => RoadLens.InCorridor(s, r, stride / 2.0)).Select(r => r.Id).ToList();
            Assert.Equal(owners.Count == 0 ? -1 : owners.Min(), s.CoveredBy);
        }
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            AssertOnePieceOwnerPerGround(p, f, z);
            foreach (RoadSegment s in covered) Assert.DoesNotContain(f.Paths, x => x.NetworkEdge == s.Edge);
        }
    }

    /// <summary>TIE-DENSE: a second, separate physical route between the same pair (ADR-032's multigraph)
    /// puts EVERY corridor step inside two corridors at once. The step belongs to the lower route id —
    /// whichever row comes first in the table, and whichever of the two has the lower id.</summary>
    [Fact]
    public void CorridorOwnership_Ties_GoToTheLowestRouteId_WhateverTheTableOrder()
    {
        WorldState baseWorld = fx.Session.World.Clone();
        RouteLensView route = Project().Routes.First(r => r.A == fx.Capital || r.B == fx.Capital);
        (int stride, int added) = LayBaselineAlong(baseWorld, route);
        Assert.True(added >= 10);
        Assert.True(TransportQuery.TryGetEdge(baseWorld, route.Id, out TransportEdgeRow row));
        var rows = new List<TransportEdgeRow>();
        for (int i = 0; i < baseWorld.TransportEdges.Count; i++) rows.Add(baseWorld.TransportEdges[i]);
        int maxId = rows.Max(r => r.Id);
        foreach (int twinId in new[] { maxId + 1, 0 })   // a twin with a higher id, then one with a lower id
        {
            TransportEdgeRow twin = row with { Id = twinId };
            int expectedOwner = Math.Min(row.Id, twinId);
            foreach (bool twinFirst in new[] { false, true })
            {
                WorldState w = baseWorld.Clone();
                w.TransportEdges.Clear();
                if (twinFirst) w.TransportEdges.Add(twin);
                foreach (TransportEdgeRow r in rows) w.TransportEdges.Add(r);
                if (!twinFirst) w.TransportEdges.Add(twin);
                WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
                var tied = p.Roads.Where(s => RoadLens.InCorridor(s, p.Routes.Single(r => r.Id == route.Id), stride / 2.0)).ToList();
                Assert.True(tied.Count >= added / 2);
                foreach (RoadSegment s in tied)
                {
                    var owners = p.Routes.Where(r => RoadLens.InCorridor(s, r, stride / 2.0)).Select(r => r.Id).ToList();
                    Assert.Contains(twinId, owners);   // a genuine tie
                    Assert.Equal(owners.Min(), s.CoveredBy);
                    Assert.True(s.CoveredBy <= expectedOwner);
                }
                Assert.Contains(tied, s => s.CoveredBy == expectedOwner);
            }
        }
    }

    [Fact]
    public void Corridor_AbsorbsStepsAlongTheRoute_NeverStepsThatCrossIt()
    {
        var route = new RouteLensView(1, 0, 1, 10, 10, 50, 10, EdgeTypes.BuiltRoad, EdgeTypes.BuiltRoad, 0.0, 160, "Built road", "Built road");
        Assert.True(RoadLens.InCorridor(new RoadSegment(14, 10, 18, 10, 1), route, 2.0));     // on the line
        Assert.True(RoadLens.InCorridor(new RoadSegment(14, 11.5, 18, 8.5, 1), route, 2.0));  // a shallow step inside the corridor
        Assert.False(RoadLens.InCorridor(new RoadSegment(14, 8, 14, 12, 1), route, 2.0));    // crosses it: ends exactly a half-block either side
        Assert.False(RoadLens.InCorridor(new RoadSegment(14, 13, 18, 13, 1), route, 2.0));   // parallel, outside the corridor
        Assert.False(RoadLens.InCorridor(new RoadSegment(54, 10, 58, 10, 1), route, 2.0));   // beyond the route's end
        Assert.False(RoadLens.InCorridor(new RoadSegment(18, 10, 18, 18, 1), route, 2.0));   // leaves it
    }

    // ------------------------------------------------------------------ roads: class styling

    [Fact]
    public void RoadClasses_AreStyledDistinctlyByTierAndByClass_WithMoreDetailBelowWorldZoom()
    {
        MapInk ink = MapInk.Default;
        static string Sig(IReadOnlyList<RoadStroke> s) => string.Join("|", s.Select(k => k.ToString()));
        foreach (WorldZoom z in Zooms)
        {
            var sigs = EdgeTypes.RoadClasses.Select(c => Sig(RoadLens.StyleOf(c, z, ink))).ToList();
            Assert.Equal(sigs.Count, sigs.Distinct(StringComparer.Ordinal).Count());   // every class distinct at every zoom
            // The TIER reads from the bed colour (the widest non-casing stroke at World zoom is the only
            // stroke): PATH umber/brown, ROAD dark-kerbed, HIGHWAY iron-red — three distinct inks.
            Rgba Main(int c) => RoadLens.StyleOf(c, z, ink) is var s && s.Count == 1 ? s[0].Color : s[1].Color;
            var tierInk = new Dictionary<int, HashSet<string>>();
            foreach (int c in EdgeTypes.RoadClasses)
            {
                int tier = EdgeTypes.TierOf(c);
                if (!tierInk.TryGetValue(tier, out HashSet<string>? set)) tierInk[tier] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(Main(c).ToString());
            }
            Assert.Equal(3, tierInk.Count);
            foreach (int t1 in tierInk.Keys) foreach (int t2 in tierInk.Keys)
                if (t1 != t2) Assert.Empty(tierInk[t1].Intersect(tierInk[t2]));
        }
        foreach (int c in EdgeTypes.RoadClasses)
        {
            Assert.Single(RoadLens.StyleOf(c, WorldZoom.World, ink));                           // World: plain network strokes
            Assert.True(RoadLens.StyleOf(c, WorldZoom.Regional, ink).Count >= 2);               // casing (+ class marks) below
            Assert.True(RoadLens.StyleOf(c, WorldZoom.Settlement, ink)[^1].Width >= RoadLens.StyleOf(c, WorldZoom.Regional, ink)[^1].Width);
        }
        // Engineered roads and highways are kerbed in the road edge ink; paths are not.
        foreach (int c in new[] { EdgeTypes.BuiltRoad, EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, EdgeTypes.Highway })
            Assert.Equal(ink.RoadEdge, RoadLens.StyleOf(c, WorldZoom.Regional, ink)[0].Color);
        // The baseline keeps exactly its previous look: dashed 1.6 at World zoom; casing + path below.
        Assert.Equal(new RoadStroke(Ink.With(ink.Path, 0.75), 1.6, (4, 3)), Assert.Single(RoadLens.StyleOf(EdgeTypes.DirtPath, WorldZoom.World, ink)));
        Assert.Equal([new RoadStroke(Ink.With(ink.PathCasing, 0.7), 4.4, null), new RoadStroke(Ink.With(ink.Path, 0.95), 2.2, null)],
            RoadLens.StyleOf(EdgeTypes.DirtPath, WorldZoom.Regional, ink));
    }

    [Fact]
    public void DrawnRoads_UseTheirClassStyle()
    {
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            var d = new DrawList();
            LensFrame f = PaintAll(d, p, z);
            // Replay the Paths layer's commands piece by piece (the pieces are emitted in order, but the
            // strokes of a route's pieces interleave by layer: casings, fills, marks).
            var lines = d.Commands.OfType<LineCmd>().ToList();
            foreach (RouteLensView r in p.Routes)
            {
                foreach (PathPiece piece in f.Paths.Where(x => x.Route == r.Id))
                {
                    (double ax, double ay) = (4 * (r.X0 + (r.X1 - r.X0) * piece.From), 4 * (r.Y0 + (r.Y1 - r.Y0) * piece.From));
                    (double bx, double by) = (4 * (r.X0 + (r.X1 - r.X0) * piece.To), 4 * (r.Y0 + (r.Y1 - r.Y0) * piece.To));
                    var drawn = lines.Where(l => l.X0 == ax && l.Y0 == ay && l.X1 == bx && l.Y1 == by).Select(l => new RoadStroke(l.Color, l.Width, l.Dash)).ToList();
                    Assert.Equal(RoadLens.StyleOf(piece.EdgeType, z, MapInk.Default), drawn);
                }
            }
        }
    }

    // ------------------------------------------------------------------ roads: partial modernization

    [Fact]
    public void PartialModernization_IsDrawnProportionally_TargetStyleFromA()
    {
        WorldProjection p = Project();
        RouteLensView r = Assert.Single(p.Routes, x => x.Id == fx.PartialRoute);
        Assert.True(r.IsPartial);
        double t = r.Modernization;
        foreach (WorldZoom z in Zooms)
        {
            var d = new DrawList();
            LensFrame f = PaintAll(d, p, z);
            var pieces = f.Paths.Where(x => x.Route == r.Id).OrderBy(x => x.From).ToList();
            Assert.Equal(2, pieces.Count);
            // Geometry: the target-class piece spans exactly fraction t of the drawn route, from A.
            (double ax, double ay, double bx, double by) = (r.X0 * 4, r.Y0 * 4, r.X1 * 4, r.Y1 * 4);
            double total = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            Rgba targetBed = RoadLens.StyleOf(r.TargetClass, z, MapInk.Default)[z == WorldZoom.World ? 0 : 1].Color;
            Rgba currentBed = RoadLens.StyleOf(r.EdgeType, z, MapInk.Default)[z == WorldZoom.World ? 0 : 1].Color;
            LineCmd target = d.Commands.OfType<LineCmd>().First(l => l.Color == targetBed && l.X0 == ax && l.Y0 == ay);
            LineCmd current = d.Commands.OfType<LineCmd>().First(l => l.Color == currentBed && l.X1 == bx && l.Y1 == by);
            double tl = Math.Sqrt((target.X1 - target.X0) * (target.X1 - target.X0) + (target.Y1 - target.Y0) * (target.Y1 - target.Y0));
            double cl = Math.Sqrt((current.X1 - current.X0) * (current.X1 - current.X0) + (current.Y1 - current.Y0) * (current.Y1 - current.Y0));
            Assert.Equal(t, tl / total, 9);
            Assert.Equal(1 - t, cl / total, 9);
            Assert.Equal((target.X1, target.Y1), (current.X0, current.Y0));   // the halves meet at the split
        }
        // At Settlement zoom the route is named with its progress.
        var town = new DrawList();
        WorldLens.Paint(town, ApproxTextMeasure.Instance, p, WorldZoom.Settlement, (x, y) => (x * 9, y * 9), 9, new RectD(0, 0, 2400, 2400));
        string pct = Math.Clamp((int)Math.Round(t * 100, MidpointRounding.AwayFromZero), 1, 99).ToString(CultureInfo.InvariantCulture);
        Assert.Contains(town.Commands.OfType<TextCmd>(), c => c.Text == r.ClassName + " - " + pct + "% to " + r.TargetName);
    }

    [Fact]
    public void PartialModernization_SplitTracksTheFraction_ForAnyFraction()
    {
        WorldProjection p = Project();
        RouteLensView r0 = p.Routes[0];
        foreach (double t in new[] { 0.001, 0.25, 0.5, 0.9, 0.999 })
        {
            RouteLensView r = r0 with { EdgeType = EdgeTypes.Trackway, TargetClass = EdgeTypes.MacadamRoad, Modernization = t };
            WorldProjection q = With(p, routes: [r]);
            LensFrame f = PaintAll(new DrawList(), q, WorldZoom.Regional);
            var pieces = f.Paths.Where(x => x.Route == r.Id).OrderBy(x => x.From).ToList();
            Assert.Equal([(EdgeTypes.MacadamRoad, 0.0, t), (EdgeTypes.Trackway, t, 1.0)], pieces.Select(x => (x.EdgeType, x.From, x.To)).ToList());
        }
        // Idle (fraction 0): one piece in the current class.
        LensFrame idle = PaintAll(new DrawList(), With(p, routes: [r0 with { TargetClass = r0.EdgeType, Modernization = 0.0 }]), WorldZoom.Regional);
        Assert.Equal((r0.EdgeType, 0.0, 1.0), idle.Paths.Where(x => x.Route == r0.Id).Select(x => (x.EdgeType, x.From, x.To)).Single());
    }

    // ------------------------------------------------------------------ settlements: no era gate

    /// <summary>Removes the Age banners (pole line, flag polygon, numeral — emitted consecutively by the
    /// banner painter) so the rest of a frame can be compared across Ages.</summary>
    private static List<DrawCmd> WithoutBanners(DrawList d)
    {
        var numerals = new HashSet<string>(StringComparer.Ordinal) { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        var r = new List<DrawCmd>();
        IReadOnlyList<DrawCmd> c = d.Commands;
        for (int i = 0; i < c.Count; i++)
        {
            if (i + 2 < c.Count && c[i] is LineCmd && c[i + 1] is PolygonCmd && c[i + 2] is TextCmd t && numerals.Contains(t.Text)) { i += 2; continue; }
            r.Add(c[i]);
        }
        return r;
    }

    [Fact]
    public void Settlements_AreComposedFromState_IdenticallyAtEveryAge_NoWallsWithoutWallState()
    {
        WorldProjection real = Project();
        Assert.All(real.Settlements, s => Assert.Equal(2, s.Age));   // the fixture's real Age II
        foreach (WorldZoom z in Zooms)
        {
            string? baseline = null;
            for (int age = 1; age <= 9; age++)
            {
                WorldProjection p = With(real, real.Settlements.Select(s => s with { Age = age }).ToList(), playerAge: age);
                var d = new DrawList();
                LensFrame f = PaintAll(d, p, z);
                Assert.Equal(p.Settlements.Count, f.LayerDraws[(int)MapLayer.AgeBanners] / 3);   // the Age still flies, one banner each
                var rest = new DrawList();
                foreach (DrawCmd cmd in WithoutBanners(d)) rest.Add(cmd);
                string svg = SvgWriter.Write(rest, 1024, 1024, null);
                baseline ??= svg;
                Assert.True(baseline == svg, $"Age {age} draws a different settlement than Age 1 at {z} zoom");
            }
        }
        // And no wall is drawn at any Age: no octagonal wall course (3 px ink lines) and no palisade
        // stakes (36 short radial lines) — the settlement layer emits no line at all without roads/units.
        foreach (int age in new[] { 1, 2, 3, 9 })
        {
            WorldProjection bare = With(real, real.Settlements.Select(s => s with { Age = age }).ToList(), units: [], roads: [], routes: []);
            foreach (WorldZoom z in new[] { WorldZoom.Regional, WorldZoom.Settlement })
            {
                var d = new DrawList();
                PaintAll(d, bare, z);
                Assert.DoesNotContain(WithoutBanners(d), c => c is LineCmd);
            }
        }
    }

    [Fact]
    public void Settlements_ShowTheirDwellingsAndCapital_ReadFromState()
    {
        WorldProjection p = Project();
        SettlementLensView cap = Assert.Single(p.Settlements, s => s.Id == fx.Capital);
        Assert.True(cap.IsCapital);
        var d = new DrawList();
        PaintAll(d, With(p, settlements: [cap], units: [], roads: [], routes: []), WorldZoom.Regional);
        Rgba dwelling = Ink.With(MapInk.Default.Dwelling, 0.85);
        Assert.Equal((int)Math.Clamp(cap.Dwellings / 25, 1, 48), d.Commands.OfType<RectCmd>().Count(r => r.Fill == dwelling));
        Assert.Single(d.Commands.OfType<PolygonCmd>(), poly => poly.Fill == MapInk.Default.CapitalMark && poly.Points.Length == 10);   // the star
    }

    // ------------------------------------------------------------------ institutions: only the marker source

    [Fact]
    public void InstitutionMarkers_ComeOnlyFromTheMarkerSource()
    {
        WorldState w = fx.Session.World.Clone();
        // ResearchCostModifiers rows are research-cost effects (ADR-029 §9), never institutions.
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(Me, 3, 0.8));
        WorldProjection real = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        Assert.All(real.Settlements, s => Assert.Empty(s.Institutions));
        Assert.Contains(real.Absent, a => a == "Institutions: none founded");
        foreach (WorldZoom z in Zooms)
            Assert.DoesNotContain(PaintAll(new DrawList(), real, z).Institutions, m => m.Key.StartsWith("institution:", StringComparison.Ordinal));

        // A stand-in source (the institutions stream's future table): exactly what it reports is drawn,
        // where it reports it, and the projection asks it once per settlement.
        int second = real.Settlements.First(s => s.Id != fx.Capital).Id;
        var asked = new List<int>();
        IReadOnlyList<InstitutionView> Source(IReadOnlyWorldState world, SimConfig cfg, SettlementId s)
        {
            asked.Add(s.Value);
            if (s.Value == fx.Capital) return [new InstitutionView(2, "Medical University", 1), new InstitutionView(3, "Engineering University", 3)];
            if (s.Value == second) return [new InstitutionView(9, "Some institution", 2)];
            return [];
        }
        WorldProjection seam = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me, Source);
        Assert.Equal(w.Settlements.Count, asked.Count);
        Assert.Equal(asked.Distinct().Count(), asked.Count);
        foreach (WorldZoom z in new[] { WorldZoom.Regional, WorldZoom.Settlement })
        {
            var got = PaintAll(new DrawList(), seam, z).Institutions.Where(m => m.Key.StartsWith("institution:", StringComparison.Ordinal))
                .Select(m => (m.Settlement, m.Key, m.Count)).OrderBy(x => x.Settlement).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
            var want = new List<(int, string, long)> { (fx.Capital, "institution:2", 1), (fx.Capital, "institution:3", 3), (second, "institution:9", 2) }
                .OrderBy(x => x.Item1).ThenBy(x => x.Item2, StringComparer.Ordinal).ToList();
            Assert.Equal(want, got);
        }
        var town = new DrawList();
        PaintAll(town, seam, WorldZoom.Settlement);
        Assert.Contains(town.Commands.OfType<TextCmd>(), t => t.Text == "Engineering University x3");
        Assert.Contains(town.Commands.OfType<TextCmd>(), t => t.Text == "Some institution x2");
    }

    [Fact]
    public void WorldLayer_ReadsNoInstitutionStateButTheSeam_SourceGuard()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot(), "Sim.Ui", "World"), "*.cs"))
        {
            string src = File.ReadAllText(file);
            Assert.DoesNotContain(".ResearchCostModifiers", src, StringComparison.Ordinal);   // no read of the table
            Assert.DoesNotContain("ResearchCostModifierRow", src, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------ units

    [Fact]
    public void UnitTokens_ShowTheCurrentAgeIdentity_AfterAutomaticModernization()
    {
        WorldState w = fx.Session.World;
        UnitFamilyContent fam = fx.Session.Config.UnitFamilies!;
        WorldProjection p = Project();
        Assert.Equal(3, MilitaryQuery.Units(w, Me).Length);
        // The real Age II transition modernized the formations in their rows (ADR-031)...
        UnitConversionRow[] conv = MilitaryQuery.Conversions(w, Me);
        Assert.Contains(conv, c => fam.IdentityByKey(c.FromIdentity)!.Name == "Warband" && fam.IdentityByKey(c.ToIdentity)!.Name == "Axe warriors");
        Assert.Contains(conv, c => fam.IdentityByKey(c.FromIdentity)!.Name == "Slingers" && fam.IdentityByKey(c.ToIdentity)!.Name == "Archers");
        // ...and the tokens show the CURRENT identity and family emblem of each row.
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            Assert.Equal(w.MilitaryUnits[i].Family, p.Units[i].FamilyKey);
            Assert.Equal(fam.IdentityByKey(w.MilitaryUnits[i].Identity)!.Name, p.Units[i].IdentityName);
        }
        var names = p.Units.Select(u => u.IdentityName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(["Archers", "Axe warriors", "Scouts"], names);   // Scouts: no Age II realization, preserved
        var d = new DrawList();
        PaintAll(d, p, WorldZoom.Regional);
        var labels = d.Commands.OfType<TextCmd>().Select(t => t.Text).ToList();
        foreach (string n in names) Assert.Contains(n, labels);
        Assert.DoesNotContain("Warband", labels);
        Assert.DoesNotContain("Slingers", labels);
    }

    [Fact]
    public void UnitTokens_SharingAStation_FanOutInIdOrder_NeverOverlap()
    {
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            Assert.Equal(p.Units.Select(u => u.Id).ToList(), f.UnitTokens);   // painted in table order (pinned)
            var mine = f.UnitPlacements.OrderBy(u => u.Id).ToList();
            Assert.Equal([0, 1, 2], mine.Select(u => u.Slot).ToList());       // ascending id → slots 0,1,2
            double tok = z == WorldZoom.World ? 9 : 12;
            for (int i = 1; i < mine.Count; i++)
            {
                (double dx, double dy) = WorldLens.FanOffset(z, i, tok);
                Assert.Equal(dx, mine[i].X - mine[0].X, 9);
                Assert.Equal(dy, mine[i].Y - mine[0].Y, 9);
                // Plates are 2·tok wide and 1.5·tok tall: a row is spaced wider than a plate, a column taller.
                Assert.True(z == WorldZoom.World ? mine[i].X - mine[i - 1].X > tok * 2 : mine[i].Y - mine[i - 1].Y > tok * 1.5, "tokens overlap");
            }
        }
    }

    /// <summary>TIE-DENSE: twelve formations at ONE settlement and eight at ONE field position, the table
    /// in scrambled orders. Every permutation gives every unit the same slot and the same screen position;
    /// slots follow ascending unit id; distinct anchors never share a fan.</summary>
    [Fact]
    public void UnitFanOut_IsDeterministic_TieDense_IndependentOfTableOrder()
    {
        WorldProjection p = Project();
        SettlementLensView st = p.Settlements[0];
        var units = new List<UnitLensView>();
        int[] stationedIds = [41, 7, 19, 3, 88, 12, 5, 60, 27, 2, 99, 14];
        foreach (int id in stationedIds) units.Add(new UnitLensView(id, Me.Value, st.X, st.Y, 1 + id % 12, "f", "u" + id, st.Id));
        int[] fieldIds = [33, 8, 71, 1, 56, 22, 90, 4];
        foreach (int id in fieldIds) units.Add(new UnitLensView(id, Me.Value, 200.25, 31.5, 1 + id % 12, "f", "u" + id, -1));
        units.Add(new UnitLensView(500, Me.Value, 200.25, 31.75, 3, "f", "lonely", -1));   // a different field position
        units.Add(new UnitLensView(501, Me.Value, 7, 7, 3, "f", "elsewhere", p.Settlements[1].Id));

        Dictionary<int, (double X, double Y, int Slot)>? reference = null;
        var orders = new List<List<UnitLensView>> { units, units.AsEnumerable().Reverse().ToList(), units.OrderBy(u => u.Id).ToList(),
            units.OrderBy(u => (u.Id * 7919) % 101).ToList(), units.OrderBy(u => -u.Id).ToList() };
        foreach (List<UnitLensView> order in orders)
        {
            WorldProjection q = With(p, units: order);
            int[] slots = WorldLens.FanSlots(q);
            LensFrame f = PaintAll(new DrawList(), q, WorldZoom.Regional);
            var placed = f.UnitPlacements.ToDictionary(u => u.Id, u => (u.X, u.Y, u.Slot));
            for (int i = 0; i < order.Count; i++) Assert.Equal(slots[i], placed[order[i].Id].Slot);
            if (reference is null) reference = placed;
            else Assert.Equal(reference.OrderBy(kv => kv.Key).ToList(), placed.OrderBy(kv => kv.Key).ToList());
        }
        // Slots are the rank of the id within its anchor.
        int[] sorted = [.. stationedIds.Order()];
        for (int i = 0; i < sorted.Length; i++) Assert.Equal(i, reference![sorted[i]].Slot);
        int[] fieldSorted = [.. fieldIds.Order()];
        for (int i = 0; i < fieldSorted.Length; i++) Assert.Equal(i, reference![fieldSorted[i]].Slot);
        Assert.Equal(0, reference![500].Slot);
        Assert.Equal(0, reference[501].Slot);
        // No two tokens of one fan overlap (token plates are 2s x 1.5s, s = 12 at Regional zoom).
        foreach (int[] fan in new[] { stationedIds, fieldIds })
            for (int i = 0; i < fan.Length; i++)
                for (int j = i + 1; j < fan.Length; j++)
                {
                    (double X, double Y, int _) a = reference[fan[i]], b = reference[fan[j]];
                    Assert.True(Math.Abs(a.X - b.X) >= 24 || Math.Abs(a.Y - b.Y) >= 18, $"units {fan[i]} and {fan[j]} overlap");
                }
    }

    // ------------------------------------------------------------------ one place for map ink

    [Fact]
    public void MapInk_Defaults_AreExactlyTheColoursTheLensDrewBefore()
    {
        MapInk k = MapInk.Default;
        static Rgba H(uint x) => Rgba.Hex(x);
        Assert.Equal(H(0x3A2E1F), k.Ink);
        Assert.Equal(H(0x1E1810), k.GlyphInk);
        Assert.Equal(H(0xF4EBD3), k.EmblemMark);
        Assert.Equal(H(0x9B2B2B), k.MedicalMark);
        Assert.Equal(H(0xFFD25A), k.Selection);
        Assert.Equal(H(0xC8962E), k.PlayerPolity);
        Assert.Equal([H(0x9B3B3B), H(0x3F6E8C), H(0x5C7F45), H(0x7A4F8C), H(0x2F7C74), H(0x8C6A3F)], k.Polities);
        Assert.Equal(H(0x777066), k.NoPolity);
        Assert.Equal(H(0xF4EBD3), k.SettlementHalo);
        Assert.Equal(H(0xFFF1C4), k.CapitalMark);
        Assert.Equal(H(0xF4EBD3), k.NamePlate);   // new (stream U3): the halo's paper, under names
        Assert.Equal(H(0xF4EBD3), k.BannerText);
        Assert.Equal(H(0xEADBB8), k.Footprint);
        Assert.Equal(H(0x8C4A3A), k.Dwelling);
        Assert.Equal(H(0xC9A35A), k.Granary);
        Assert.Equal(H(0xB06A44), k.Workshop);
        Assert.Equal(H(0x9A8F7A), k.StructureOther);
        Assert.Equal([H(0x8E3B32), H(0xE8E2D0), H(0x4F6F8F), H(0x3E7A6A), H(0x9AA344)], k.Universities);
        Assert.Equal(H(0x9A8F7A), k.InstitutionOther);
        Assert.Equal([H(0x8FA34E), H(0xC2A060), H(0x8A8A86), H(0xC07048), H(0x6C8FB0)], k.Sectors);
        Assert.Equal(H(0x6B4A2A), k.Path);
        Assert.Equal(H(0xF2E6C8), k.PathCasing);
        // Polity ink keeps its rule: player, then polity % 6, no polity grey.
        Assert.Equal(H(0xC8962E), WorldLens.PolityInk(1, 1));
        Assert.Equal(H(0x3F6E8C), WorldLens.PolityInk(7, 1));
        Assert.Equal(H(0x777066), WorldLens.PolityInk(-1, 1));
        // New road tokens come from the style bible's palette.
        foreach (Rgba road in new[] { k.Trackway, k.RoadEdge, k.BuiltRoad, k.PavedRoad, k.MacadamRoad, k.Highway, k.RoadMarks, k.HighwayCentre })
            Assert.Contains(road, Sim.Ui.Art.ParchmentPalette.BibleColors);
    }

    [Fact]
    public void MapInk_IsTheOnlyColourSourceInTheWorldLayer_SourceGuard()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot(), "Sim.Ui", "World"), "*.cs"))
        {
            if (Path.GetFileName(file) == "MapInk.cs") continue;
            string src = File.ReadAllText(file);
            Assert.DoesNotContain("Rgba.Hex(", src, StringComparison.Ordinal);
            Assert.DoesNotContain("Hex(0x", src, StringComparison.Ordinal);
            Assert.DoesNotContain("new Rgba(", src, StringComparison.Ordinal);
            Assert.DoesNotContain("ParchmentPalette.", src.Replace("using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;", "", StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    /// <summary>The era seam: a different <see cref="MapInk"/> changes only colours — every command keeps
    /// its kind, geometry, text and order.</summary>
    [Fact]
    public void MapInk_ChangesTheInk_NeverTheComposition()
    {
        static Rgba Shift(Rgba c) => new((byte)(255 - c.R), c.G, (byte)(c.B / 2), c.A);
        MapInk d0 = MapInk.Default;
        var other = d0 with
        {
            Ink = Shift(d0.Ink), GlyphInk = Shift(d0.GlyphInk), Path = Shift(d0.Path), Trackway = Shift(d0.Trackway),
            RoadEdge = Shift(d0.RoadEdge), Highway = Shift(d0.Highway), Footprint = Shift(d0.Footprint), Dwelling = Shift(d0.Dwelling),
            PlayerPolity = Shift(d0.PlayerPolity), Polities = d0.Polities.Select(Shift).ToList(), Sectors = d0.Sectors.Select(Shift).ToList(),
        };
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            var a = new DrawList();
            var b = new DrawList();
            LensFrame fa = PaintAll(a, p, z);
            LensFrame fb = PaintAll(b, p, z, other);
            Assert.Equal(fa.LayerDraws, fb.LayerDraws);
            Assert.Equal(a.Commands.Count, b.Commands.Count);
            static DrawCmd Uncolour(DrawCmd c) => c switch
            {
                LineCmd l => l with { Color = default },
                RectCmd r => r with { Fill = null, Stroke = null },
                CircleCmd ci => ci with { Fill = null, Stroke = null },
                PolygonCmd pg => new PolygonCmd(pg.Points, default),
                ArcCmd ar => ar with { Color = default },
                TextCmd t => t with { Color = default },
                _ => c,
            };
            for (int i = 0; i < a.Commands.Count; i++)
                Assert.Equal(SvgWriter.Write(One(Uncolour(a.Commands[i])), 10, 10, null), SvgWriter.Write(One(Uncolour(b.Commands[i])), 10, 10, null));
            Assert.NotEqual(SvgWriter.Write(a, 1024, 1024, null), SvgWriter.Write(b, 1024, 1024, null));
        }
        static DrawList One(DrawCmd c) { var l = new DrawList(); l.Add(c); return l; }
    }

    // ------------------------------------------------------------------ zoom

    [Fact]
    public void ZoomLevels_KeepTheirThresholds_AndEveryLevelShowsTheNetwork()
    {
        Assert.Equal(WorldZoom.World, WorldLens.LevelFor(7.0, 1600, 1000, 256));
        Assert.Equal(WorldZoom.Regional, WorldLens.LevelFor(7.2, 1600, 1000, 256));
        Assert.Equal(WorldZoom.Regional, WorldLens.LevelFor(24.0, 1600, 1000, 256));
        Assert.Equal(WorldZoom.Settlement, WorldLens.LevelFor(25.0, 1600, 1000, 256));
        Assert.Contains(WorldLayer.MajorInfrastructure, WorldLens.LayersFor(WorldZoom.World));
        Assert.Contains(WorldLayer.Roads, WorldLens.LayersFor(WorldZoom.Regional));
        Assert.Contains(WorldLayer.Roads, WorldLens.LayersFor(WorldZoom.Settlement));
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            Assert.True(f.LayerDraws[(int)MapLayer.Paths] > 0);
            Assert.Equal(p.Routes.Count, f.Paths.Where(x => x.Route >= 0).Select(x => x.Route).Distinct().Count());
        }
    }

    // ------------------------------------------------------------------ read-only

    [Fact]
    public void ProjectionAndPaint_NeverChangeTheWorldHash()
    {
        WorldState w = fx.Session.World;
        string hash = WorldHash.ComputeHex(w);
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            PaintAll(new DrawList(), p, z);
            WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, z, (x, y) => (x * 9, y * 9), 9, new RectD(0, 0, 2400, 2400), selected: fx.Capital);
        }
        AgePreview.LensSvg(fx.Session, WorldZoom.Settlement);
        Assert.Equal(hash, WorldHash.ComputeHex(w));
    }

    // ------------------------------------------------------------------ headless evidence

    /// <summary>Writes the road-fixture preview SVGs when CIV_ROAD_PREVIEW_OUT names a directory
    /// (docs/architecture/age-and-world-ui/render-previews.sh); otherwise a no-op.</summary>
    [Fact]
    public void Preview_RoadFixture_Svg()
    {
        string town = AgePreview.LensSvg(fx.Session, WorldZoom.Settlement);
        Assert.Contains("% to ", town, StringComparison.Ordinal);   // the partial route is named at settlement zoom
        string? outDir = Environment.GetEnvironmentVariable("CIV_ROAD_PREVIEW_OUT");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "12-roads-world-zoom.svg"), AgePreview.LensSvg(fx.Session, WorldZoom.World));
        File.WriteAllText(Path.Combine(outDir, "13-roads-regional-zoom.svg"), AgePreview.LensSvg(fx.Session, WorldZoom.Regional));
        File.WriteAllText(Path.Combine(outDir, "14-roads-settlement-zoom.svg"), town);
        File.WriteAllLines(Path.Combine(outDir, "roads-preview-log.txt"), fx.Log);
    }
}
