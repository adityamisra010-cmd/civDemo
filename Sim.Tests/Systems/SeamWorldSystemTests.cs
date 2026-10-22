using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// Batch 3 WS3 — THE SIMULATION ACROSS THE SEAM, on the <see cref="SeamWorld"/> fixture (one land strip straddling the
/// east-west seam; flat, the same layers are two islands). Every travel mechanism the simulation has wraps: the
/// catchment's pairwise travel costs (SettlementDistances), the flows that read them (migration, trade), the AI's
/// founding flood and the player's founding route (the same Pathfinder, so they agree), road length and maxRouteKm
/// candidacy (ADR-032 FR3 amended: on the cylinder), the donor order of roads, the construction purse and grain relief
/// (PayingSettlements), and the weather kernel. NO UNIT MOVEMENT SYSTEM EXISTS (M7; order kind 10 reserved) — the
/// "movement" the seam carries today is exactly these settlement-level flows and the founding parties.
/// </summary>
public class SeamWorldSystemTests
{
    // M6 Track EX (2026-10-22): the seam's settlement-level flows are an older layer premised on the all-known world (a
    // rival's land and settlements across the seam are known); the discovery layer is off here (TestConfigs.PreDiscovery,
    // the R4 rule). Under the fog the AI first explores (DiscoveryTests pins Explore's disc wrapping at the seam).
    private static SimConfig Cfg => TestConfigs.PreDiscovery(TestConfigs.Sim());
    private const int Size = SeamWorld.DefaultSize;

    private static WorldState WithDistances(WorldState w) =>
        UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Catchment(Cfg)).Step(w);

    private static double Distance(IReadOnlyWorldState w, int from, int to)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = w.SettlementDistances[i];
            if (d.From.Value == from && d.To.Value == to) return d.TravelCost;
        }
        throw new InvalidOperationException($"no distance row {from}->{to}");
    }

    [Fact]
    public void SettlementDistances_AcrossTheSeam_AreTheShortWayTravelCost_AndInfiniteFlat()
    {
        WorldState wrapped = WithDistances(SeamWorld.Found(true, SeamWorld.TwoSides()));
        WorldState flat = WithDistances(SeamWorld.Found(false, SeamWorld.TwoSides()));
        // 40 px = 10 lattice steps of ideal ground: exactly 10.0 cost units the short way, both directions.
        Assert.Equal(10.0, Distance(wrapped, 0, 1));
        Assert.Equal(10.0, Distance(wrapped, 1, 0));
        Assert.True(double.IsPositiveInfinity(Distance(flat, 0, 1)));
    }

    [Fact]
    public void Catchments_ReachAcrossTheSeam_AndStillPartition()
    {
        // Two settlements hugging the seam from either side (x = 2 and x = 250): on the cylinder each catchment
        // holds lattice columns on the far side of the seam, and no node is claimed twice; flat, each stops at its edge.
        int[] sites = [SeamWorld.Cell(2, Size / 2), SeamWorld.Cell(Size - 6, Size / 2 + 40)];
        WorldState wrapped = WithDistances(SeamWorld.Found(true, sites));
        WorldState flat = WithDistances(SeamWorld.Found(false, sites));
        (bool westAcross, bool eastAcross) Across(WorldState w)
        {
            int l = Size / 4;
            var seen = new bool[l * l];
            bool westAcross = false, eastAcross = false;
            for (int i = 0; i < w.CatchmentNodes.Count; i++)
            {
                CatchmentNodeRow c = w.CatchmentNodes[i];
                Assert.False(seen[c.LatticeNode], $"node {c.LatticeNode} claimed twice");
                seen[c.LatticeNode] = true;
                int column = c.LatticeNode % l;
                if (c.Settlement.Value == 0 && column >= l / 2) westAcross = true;
                if (c.Settlement.Value == 1 && column < l / 2) eastAcross = true;
            }
            return (westAcross, eastAcross);
        }
        Assert.Equal((true, true), Across(wrapped));
        Assert.Equal((false, false), Across(flat));
    }

    [Fact]
    public void MigrationAndTrade_FlowAcrossTheSeam_ReadingTheShortWayDistance()
    {
        // The production pipeline on both topologies, the trade capability known. On the cylinder the two
        // settlements are 10 cost units apart and people and goods move between them; flat they are two islands and the
        // +∞ distance sentinel stops every flow (the by-construction zero the migration model relies on).
        static (long Migrants, long Traded) Run(bool wrap)
        {
            SimConfig cfg = Cfg;
            // The two pieces differ (fertility 0.9 west, 0.3 east), so prices differ and goods have a reason to move.
            WorldState w = TestConfigs.KnowRecipes(TestConfigs.KnowTrade(
                SeamWorld.Found(wrap, SeamWorld.TwoSides(), sim: cfg, westFertility: 0.9, eastFertility: 0.3), cfg), cfg);
            TurnExecutor ex = UniversityRigs.Production(cfg);
            long migrants = 0, traded = 0;
            for (int t = 0; t < 80; t++)
            {
                w = ex.Step(w);
                for (int i = 0; i < w.MigrationFlows.Count; i++) migrants += w.MigrationFlows[i].Inflow;
                for (int i = 0; i < w.TradeFlows.Count; i++) traded += w.TradeFlows[i].Quantity;
            }
            return (migrants, traded);
        }
        (long wm, long wt) = Run(true);
        (long fm, long ft) = Run(false);
        Assert.Equal(0, fm);
        Assert.Equal(0, ft);
        Assert.True(wm > 0, $"no migrant crossed the seam in 80 turns (migrants {wm}, traded {wt})");
        Assert.True(wt > 0, $"no good was traded across the seam in 80 turns (migrants {wm}, traded {wt})");
    }

    [Fact]
    public void AiFounding_ReachesAcrossTheSeam_AndAgreesWithThePlayersRoute_CellForCell()
    {
        // The AI (polity 2) holds settlement 1 on the EAST edge; the better (more fertile) ground is the strip's WEST
        // piece, reachable only across the seam. The player (polity 1) holds settlement 0 far south on the east piece.
        int[] sites = [SeamWorld.Cell(215, 180), SeamWorld.Cell(236, 128)];
        foreach (bool wrap in new[] { true, false })
        {
            SimConfig cfg = Cfg;
            WorldState w = SeamWorld.Found(wrap, sites, aiEmpires: 1, sim: cfg, westFertility: 0.9, eastFertility: 0.3);
            WorldgenConfig wg = SeamWorld.Worldgen(aiEmpires: 1);
            SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(w, cfg, wg)!;
            var ai = new PolityId(2);
            var source = new SettlementId(1);
            bool[] reachable = AiFoundingPolicy.ReachableNodes(w, f, sites[1]);

            // PARITY: for every land cell, the AI's flood and the player's route preview (FoundingQuery.RouteFrom →
            // RouteQuery.Find → the same Pathfinder) agree on whether it can be reached.
            int westReachable = 0;
            for (int cell = 0; cell < Size * Size; cell++)
            {
                if (w.Terrain!.Water[cell] >= 0.5) continue;
                int node = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, cell);
                RouteQuery.Route route = FoundingQuery.RouteFrom(w, f, source, cell)!;
                Assert.Equal(reachable[node], route.Found);
                if (cell % Size < Size / 2 && route.Found) westReachable++;
            }
            if (!wrap) { Assert.Equal(0, westReachable); continue; }
            Assert.True(westReachable > 0);

            int pick = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), ai, source);
            Assert.True(pick >= 0, "the AI found no site");
            Assert.True(pick % Size < Size / 2, $"the AI's site {pick % Size},{pick / Size} is not across the seam");
            RouteQuery.Route r = FoundingQuery.RouteFrom(w, f, source, pick)!;
            Assert.True(r.Found);
            Assert.True(CrossesSeam(r.Nodes, f.Lattice.Size), "the AI's founding route does not cross the seam");
            Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, cfg, f, ai, source, pick));
        }
    }

    private static bool CrossesSeam(int[] nodes, int latticeSize)
    {
        for (int i = 1; i < nodes.Length; i++)
            if (Math.Abs(nodes[i] % latticeSize - nodes[i - 1] % latticeSize) == latticeSize - 1) return true;
        return false;
    }

    [Fact]
    public void RoadLength_IsTheStraightLineOnTheCylinder_AndMaxRouteKmCandidacyUsesIt()
    {
        // ADR-032 FR3 amended: 40 px × 4 km = 160 km the short way (the flat way would be 216 px = 864 km, past the
        // 600-km maxRouteKm, so a flat length would drop the only route this pair has).
        SimConfig cfg = Cfg;
        WorldState w = WithDistances(SeamWorld.Found(true, SeamWorld.TwoSides(), sim: cfg));
        Assert.Equal(160.0, RoadDevelopmentQuery.GeographicKm(w, new SettlementId(0), new SettlementId(1), double.NaN));
        Assert.Equal(160.0, GeographicDistance.Km(w.Terrain!, w.Settlements[0].SiteCell, w.Settlements[1].SiteCell));
        RoadsConfig roads = cfg.Roads!;
        Assert.True(roads.MaxRouteKm < 864.0 && roads.MaxRouteKm > 160.0, "the rig needs maxRouteKm between the two lengths");
        RouteStatus[] routes = RoadDevelopmentQuery.Routes(w, TestConfigs.Research(), roads, new PolityId(1));
        Assert.Single(routes);
        Assert.Equal(160.0, routes[0].LengthKm);
    }

    [Fact]
    public void PayingSettlements_RankByDistanceOnTheCylinder_TiesByIdAscending()
    {
        // Route endpoint A = settlement 0 at x = 6; endpoint B = settlement 4 at (30, y + 40). Settlement 1 sits at
        // x = 250 (12 px WEST of A across the seam), settlement 2 at x = 18 (12 px EAST of A, the same row): an exact
        // tie on the cylinder, broken by id; settlement 3 at x = 40 (34 px). Flat, settlement 1 is 244 px from A and
        // 220 px from B, and ranks LAST.
        int y = Size / 2;
        int[] sites = [SeamWorld.Cell(6, y), SeamWorld.Cell(250, y), SeamWorld.Cell(18, y), SeamWorld.Cell(40, y), SeamWorld.Cell(30, y + 40)];
        WorldState wrapped = SeamWorld.Found(true, sites);
        WorldState flat = SeamWorld.Found(false, sites);
        var a = new SettlementId(0);
        var b = new SettlementId(4);
        int[] Order(WorldState w) =>
            [.. RoadDevelopmentQuery.PayingSettlements(w, new PolityId(1), a, b).Select(s => s.Value)];
        // Endpoints first (distance 0, by id), then 1 and 2 tied at 12² (by id), then 3 at 34².
        Assert.Equal(new[] { 0, 4, 1, 2, 3 }, Order(wrapped));
        Assert.Equal(new[] { 0, 4, 2, 3, 1 }, Order(flat));
        // The tie (settlements 1 and 2, both 144 px² from A on the cylinder) is ordered by id: drop endpoint B's pull.
        int[] tie = [.. RoadDevelopmentQuery.PayingSettlements(wrapped, new PolityId(1), a, a).Select(s => s.Value)];
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, tie);
        Assert.Equal(MapTopology.SquaredPx(sites[1], sites[0], Size, true), MapTopology.SquaredPx(sites[2], sites[0], Size, true));
    }

    [Fact]
    public void TheWeatherKernel_ReadsTheSeamPairAtItsTrueSeparation()
    {
        // A pair 40 px apart ACROSS the seam draws exactly the weather of a pair 40 px apart INSIDE the map (the
        // kernel sees only the distance), and not the weather of the flat world's 216-px pair.
        TurnExecutor weather = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.HarvestWeather(Cfg));
        int y = Size / 2;
        WorldState across = SeamWorld.Found(true, [SeamWorld.Cell(20, y), SeamWorld.Cell(236, y)]);
        WorldState inside = SeamWorld.Found(true, [SeamWorld.Cell(20, y), SeamWorld.Cell(44, y + 32)]);   // 24² + 32² = 40²
        WorldState flatPair = SeamWorld.Found(false, [SeamWorld.Cell(20, y), SeamWorld.Cell(236, y)]);
        double kmPerCost = TransportQuery.KmPerCostUnit(across);
        Assert.True(GeographicDistance.TryIdealGroundCostUnits(across, across.Settlements[0].SiteCell, across.Settlements[1].SiteCell, kmPerCost, out double dAcross));
        Assert.True(GeographicDistance.TryIdealGroundCostUnits(inside, inside.Settlements[0].SiteCell, inside.Settlements[1].SiteCell, kmPerCost, out double dInside));
        Assert.Equal(dInside, dAcross);
        for (int t = 0; t < 4; t++)
        {
            across = weather.Step(across);
            inside = weather.Step(inside);
            flatPair = weather.Step(flatPair);
            Assert.True(WorldStates.TableEquals(across.HarvestWeather, inside.HarvestWeather), $"step {t + 1}: the seam pair's weather differs from the interior pair's");
        }
        Assert.False(WorldStates.TableEquals(across.HarvestWeather, flatPair.HarvestWeather));
    }

    [Fact]
    public void Entities_StayUnique_AcrossTheSeam_AfterPlay()
    {
        // Settlements, formations, catchment claims, distance rows and deposits keep ONE row per identity on the
        // cylinder — nothing is duplicated where the map wraps — and every formation's X is normalised into [0, Size).
        SimConfig cfg = Cfg;
        WorldState w = SeamWorld.Found(true, [SeamWorld.Cell(2, Size / 2), SeamWorld.Cell(Size - 3, Size / 2 + 20)], aiEmpires: 1, sim: cfg);
        TurnExecutor ex = UniversityRigs.Production(cfg);
        for (int t = 0; t < 12; t++) w = ex.Step(w);

        var ids = new HashSet<int>();
        var cells = new HashSet<int>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            Assert.True(ids.Add(w.Settlements[i].Id.Value), "duplicate settlement id");
            Assert.True(cells.Add(w.Settlements[i].SiteCell), "two settlements on one site cell");
        }
        Assert.True(w.MilitaryUnits.Count > 0, "the rig has no formation");
        var units = new HashSet<int>();
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = w.MilitaryUnits[i];
            Assert.True(units.Add(u.Id), "duplicate formation id");
            Assert.True(u.X >= 0.0 && u.X < Size, $"formation {u.Id} X {u.X} is not normalised");
        }
        var claimed = new HashSet<int>();
        for (int i = 0; i < w.CatchmentNodes.Count; i++)
            Assert.True(claimed.Add(w.CatchmentNodes[i].LatticeNode), "a lattice node claimed twice");
        var pairs = new HashSet<(int, int)>();
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            Assert.True(pairs.Add((w.SettlementDistances[i].From.Value, w.SettlementDistances[i].To.Value)), "duplicate distance row");
        Assert.Equal(w.Settlements.Count * (w.Settlements.Count - 1), w.SettlementDistances.Count);
        var deposits = new HashSet<(int, int)>();
        for (int i = 0; i < w.Deposits.Count; i++)
            Assert.True(deposits.Add((w.Deposits[i].Settlement.Value, w.Deposits[i].Good.Value)), "duplicate deposit row");
    }

    [Fact]
    public void ThePlaceCard_NamesTheNearestSettlementAcrossTheSeam()
    {
        // SiteAssessment's nearest-known list (the cell card) ranks by the squared distance ON THE CYLINDER: a cell at
        // x = 4 is 20 px from the settlement at x = 240 (across the seam) and 36 px from the one at x = 40.
        int y = Size / 2;
        SimConfig cfg = Cfg;
        WorldState w = SeamWorld.Found(true, [SeamWorld.Cell(40, y), SeamWorld.Cell(240, y)], sim: cfg);
        SiteAssessment.Prepared site = SiteAssessment.Prepare(w, cfg, SeamWorld.Worldgen())!;
        SiteAssessment.Report r = SiteAssessment.Assess(w, cfg, site, PolityKnowledge.Of(w, new PolityId(1), null), new PolityId(1),
            SeamWorld.Cell(4, y));
        Assert.Equal(1, r.Nearest[0].Id.Value);
        Assert.Equal(400, r.Nearest[0].DistanceSquaredPx);
        Assert.Equal(0, r.Nearest[1].Id.Value);
        Assert.Equal(36 * 36, r.Nearest[1].DistanceSquaredPx);
    }

    [Fact]
    public void TheReachHelpers_MirrorTheWrappedWindows_SoAStubHidingTheFarSideWithholds()
    {
        // A knowledge stub that knows every lattice node EXCEPT the east edge column (x ≥ Size − 4): a cell at x = 1
        // reads the east edge through BoxSum's square, the BFS diamond and the hinterland disc on the cylinder — so
        // each reach helper must say "not all known" there; flat the windows are cut at x = 0 and everything is known.
        int y = Size / 2, l = Size / 4;
        foreach (bool wrap in new[] { true, false })
        {
            WorldState w = SeamWorld.Found(wrap, SeamWorld.TwoSides());
            var known = new bool[l * l];
            for (int n = 0; n < known.Length; n++) known[n] = n % l != l - 1;
            PolityKnowledge k = PolityKnowledge.Restricted(w, new PolityId(1), known, known);
            Assert.Equal(wrap, k.WrapX);
            Assert.Equal(!wrap, SiteAssessment.KnowsSquare(k, Size, 1, y, 3));
            Assert.Equal(!wrap, SiteAssessment.KnowsDiamond(k, Size, 1, y, 3));
            Assert.Equal(!wrap, SiteAssessment.KnowsDisc(k, Size, 1, y, 12.5));
        }
    }

    [Fact]
    public void PolityKnowledge_NodeAt_NormalisesAPositionAcrossTheSeam()
    {
        int y = Size / 2, l = Size / 4;
        PolityKnowledge wrapped = PolityKnowledge.Of(SeamWorld.Found(true, SeamWorld.TwoSides()), new PolityId(1), null);
        PolityKnowledge flat = PolityKnowledge.Of(SeamWorld.Found(false, SeamWorld.TwoSides()), new PolityId(1), null);
        int row = (y / 4) * l;
        Assert.Equal(row + l - 1, wrapped.NodeAt(-0.5, y));          // half a pixel west of x = 0: the east edge column
        Assert.Equal(row + 0, wrapped.NodeAt(Size + 0.5, y));        // one circumference on: column 0
        Assert.Equal(row + 2, wrapped.NodeAt(8.5 - 3 * Size, y));
        Assert.Equal(-1, flat.NodeAt(-0.5, y));
        Assert.Equal(-1, flat.NodeAt(Size + 0.5, y));
        Assert.Equal(-1, wrapped.NodeAt(4.0, -1.0));                 // y keeps its hard edges
    }

    [Fact]
    public void HinterlandMeans_WrapTheSeam_SoASeamSiteSeesBothSides()
    {
        // A site at x = 2: its 12.5-px hinterland disc reaches x = −10..14 — columns 246..255 across the seam. The east
        // piece is raised (land elevation 0.7 there, 0.4 on the west piece; sea level 0.2), so the deposit channel's
        // elevation mean MIXES both pieces on the cylinder and sees the west piece alone on the flat map, where the disc
        // is cut at x = 0. The expectation is enumerated independently of HinterlandMeans' own loop.
        WorldgenConfig wg = SeamWorld.Worldgen();
        int site = SeamWorld.Cell(2, Size / 2);
        (double wrapped, int ew, int ww) = Means(true);
        (double flat, int ef, int wf) = Means(false);
        Assert.True(ew > 0 && ef == 0, "the rig's disc does not straddle the seam");
        Assert.Equal((ww * 0.2 + ew * 0.5) / (ww + ew), wrapped, 12);
        Assert.Equal(0.2, flat, 12);
        Assert.Equal(ww, wf);

        (double Mean, int East, int West) Means(bool wrap)
        {
            TerrainSet t = SeamWorld.Terrain(wrap);
            double[] elev = t.Elevation.ToArray();
            for (int i = 0; i < elev.Length; i++) if (i % Size >= Size / 2 && t.Water[i] < 0.5) elev[i] = 0.7;
            TerrainSet raised = TerrainSet.FromLayers(Size, t.KmPerPx, t.SeaLevel, elev, t.Water, t.Temperature, t.Moisture,
                t.Fertility, t.MovementCost, t.Rivers, [], wrap);
            int east = 0, west = 0;
            for (int dy = -12; dy <= 12; dy++)
                for (int dx = -12; dx <= 12; dx++)
                {
                    if (dx * dx + dy * dy > 12.5 * 12.5) continue;
                    int x = 2 + dx;
                    if (x >= 0) west++;
                    else if (wrap) east++;
                }
            (_, double e) = WorldFounding.HinterlandMeans(raised, wg, site, 12.5);
            return (e, east, west);
        }
    }
}
