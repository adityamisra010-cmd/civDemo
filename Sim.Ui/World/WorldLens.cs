using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>The three map zoom levels of D-047 Part 5 (the fourth, "deep UI", is the full-screen
/// progression and Age surfaces).</summary>
public enum WorldZoom { World = 0, Regional = 1, Settlement = 2 }

/// <summary>The information layers the world lens can draw. Which ones are on is a pure function of
/// the zoom level (<see cref="WorldLens.LayersFor"/>).</summary>
public enum WorldLayer
{
    Territories,
    MajorSettlements,
    MajorInfrastructure,
    Roads,
    SettlementMorphology,
    InstitutionalPresence,
    InstitutionTypes,
    InfrastructureDensity,
    PopulationScale,
    ProductionSignals,
    MilitaryFormations,
    AgeBanners,
}

/// <summary>One structure kind in a settlement, aggregated (M4-D counts; never per-building).</summary>
public sealed record StructureView(int ProjectId, string Name, StructureKind Kind, long Count);

public enum StructureKind { Storage, Production, Other }

/// <summary>One settlement as the lens draws it — every field read from a published table (the
/// institutions through <see cref="InstitutionMarkerSource"/>, the one institution seam).</summary>
public sealed record SettlementLensView(
    int Id, string Name, double X, double Y, int Controller, bool IsCapital, bool IsPlayer,
    long Population, long Dwellings, int SizeTier, int Age,
    IReadOnlyList<StructureView> Structures,
    double[]? Sectors, int RoadsInCatchment, int Notables, int ClassesActive,
    IReadOnlyList<InstitutionView> Institutions,
    // Integration item 5: the knowledge-derived label of the LARGEST sector (LabourActivities — what the
    // controller's knowledge calls it, never "farming" before farming is known); null when uncontrolled or
    // never allocated. Ties: the lowest sector index, as the caption's own rule.
    string? TopSectorLabel = null);

/// <summary>One institution marker the lens drew: a structure kind (Key "structure:{projectId}") or an
/// institution type from <see cref="InstitutionMarkerSource"/> (Key "institution:{typeKey}") in a
/// settlement, with the aggregated count.</summary>
public sealed record InstitutionMarker(int Settlement, string Key, long Count);

/// <summary>One military formation token (a MilitaryUnits row): its CURRENT family and identity — the
/// row as the last Age transition left it (ADR-031 automatic modernization).</summary>
public sealed record UnitLensView(int Id, int Owner, double X, double Y, int FamilyKey, string FamilyName, string IdentityName, int Location);

/// <summary>Where the lens placed one formation token (screen px, the token's centre) and its fan-out
/// slot among the formations sharing its anchor (0 = first in ascending unit id).</summary>
public sealed record UnitPlacement(int Id, double X, double Y, int Slot);

/// <summary>One lattice step of the free dirt-path baseline (a <c>NetworkEdges</c> row, id
/// <paramref name="Edge"/>) between lattice-node centres, in world px. <paramref name="CoveredBy"/> is
/// the id of the built route whose corridor it lies in (that route draws this ground), or -1.</summary>
public sealed record RoadSegment(double X0, double Y0, double X1, double Y1, int EdgeType, int Edge = -1, int CoveredBy = -1);

/// <summary>A territory block: one lattice node a settlement's catchment claims.</summary>
public readonly record struct TerritoryBlock(double X, double Y, double Size, int Polity);

/// <summary>
/// THE WORLD LENS PROJECTION — a read-only, aggregated view of real simulation state for the map
/// (D-047 Part 4 G, Part 5). Settlements, structures (counted per settlement × project; never
/// individually placed), institutions (through <see cref="InstitutionMarkerSource"/> only), the
/// dirt-path baseline and the built routes (<c>TransportEdges</c>), catchment territories, formations,
/// Ages. Built once per turn; nothing here is invented: a system that does not exist is reported as
/// such by <see cref="Absent"/>, never drawn as if it did.
/// </summary>
public sealed class WorldProjection
{
    public required double WorldSize { get; init; }
    public required IReadOnlyList<SettlementLensView> Settlements { get; init; }
    public required IReadOnlyList<UnitLensView> Units { get; init; }
    /// <summary>The free dirt-path baseline: PathBuild's lattice steps, in <c>NetworkEdges</c> order.</summary>
    public required IReadOnlyList<RoadSegment> Roads { get; init; }
    /// <summary>The built inter-settlement routes: travelled <c>TransportEdges</c> rows, in table order.</summary>
    public IReadOnlyList<RouteLensView> Routes { get; init; } = [];
    public required IReadOnlyList<TerritoryBlock> Territory { get; init; }
    public required int PlayerPolity { get; init; }
    public required int PlayerAge { get; init; }
    public required string PlayerAgeName { get; init; }
    /// <summary>Honest empty states: systems the map would show if they existed.</summary>
    public required IReadOnlyList<string> Absent { get; init; }

    /// <summary>Builds the projection. <paramref name="institutions"/> replaces the institution seam
    /// (tests only); the game and previews always read <see cref="InstitutionMarkerSource.InstitutionsAt"/>.</summary>
    public static WorldProjection Build(IReadOnlyWorldState world, SimConfig cfg, Func<int, string> name, PolityId player,
        Func<IReadOnlyWorldState, SimConfig, SettlementId, IReadOnlyList<InstitutionView>>? institutions = null)
    {
        institutions ??= InstitutionMarkerSource.InstitutionsAt;
        int size = world.Terrain?.Size ?? 256;
        var lattice = world.Terrain is null ? null
            : Sim.Core.Pathing.TraversalLattice.Build(world.Terrain, cfg.Transport.RiverCostFactor);
        int lsize = lattice?.Size ?? 1, stride = lattice is null ? 1 : size / lattice.Size;

        // Network: node id -> lattice node, in table order (ids are table order).
        int maxId = -1;
        for (int i = 0; i < world.NetworkNodes.Count; i++) maxId = Math.Max(maxId, world.NetworkNodes[i].Id.Value);
        var anchor = new int[maxId + 1];
        for (int i = 0; i < world.NetworkNodes.Count; i++) anchor[world.NetworkNodes[i].Id.Value] = world.NetworkNodes[i].LatticeNode;

        // Built routes: every travelled TransportEdges row, site cell to site cell (the simulation's
        // own geometry for a route — see RouteLensView), table order.
        var routes = new List<RouteLensView>();
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            if (!RoadPerformance.IsTravelled(e)) continue;
            if (!SiteOf(world, e.A, size, out double ax, out double ay) || !SiteOf(world, e.B, size, out double bx, out double by)) continue;
            routes.Add(new RouteLensView(e.Id, e.A.Value, e.B.Value, ax, ay, bx, by, e.EdgeType, e.TargetClass, e.Modernization,
                e.LengthKm, RoadLens.ClassName(cfg, e.EdgeType), RoadLens.ClassName(cfg, e.TargetClass)));
        }

        // The dirt-path baseline; a step inside a route's corridor (half a lattice block either side of
        // its centre line) is that route's ground. Inside several corridors (routes meeting at a
        // settlement, parallel routes of one pair) it belongs to the LOWEST route id — a stable, unique
        // integer key, whatever the table order.
        double corridor = stride / 2.0;
        var roads = new List<RoadSegment>();
        for (int i = 0; i < world.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            (double ax, double ay) = Center(anchor[e.A.Value], lsize, stride);
            (double bx, double by) = Center(anchor[e.B.Value], lsize, stride);
            var step = new RoadSegment(ax, ay, bx, by, e.EdgeType, e.Id.Value);
            int owner = -1;
            foreach (RouteLensView r in routes)
                if ((owner < 0 || r.Id < owner) && RoadLens.InCorridor(step, r, corridor)) owner = r.Id;
            roads.Add(owner < 0 ? step : step with { CoveredBy = owner });
        }

        // Catchment claims: lattice node -> settlement, and territory blocks coloured by controller.
        var territory = new List<TerritoryBlock>();
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            int ctl = EmpireQuery.TryGetController(world, row.Settlement, out PolityId p) ? p.Value : -1;
            (double cx, double cy) = Center(row.LatticeNode, lsize, stride);
            territory.Add(new TerritoryBlock(cx - stride / 2.0, cy - stride / 2.0, stride, ctl));
        }

        AgeContent? ages = cfg.Ages;
        var settlements = new List<SettlementLensView>();
        bool anyInstitution = false;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementRow row = world.Settlements[s];
            SettlementId id = row.Id;
            int ctl = EmpireQuery.TryGetController(world, id, out PolityId controller) ? controller.Value : -1;
            bool capital = ctl >= 0 && EmpireQuery.TryGetCapital(world, controller, out SettlementId cap) && cap.Value == id.Value;
            long pop = 0;
            for (int b = 0; b < world.Buckets.Count; b++) if (world.Buckets[b].Settlement == id) pop += world.Buckets[b].Count.Value;
            long dwellings = 0;
            for (int h = 0; h < world.Housing.Count; h++) if (world.Housing[h].Settlement == id) dwellings += world.Housing[h].Dwellings.Value;
            int tier = 0;
            for (int c = 0; c < world.CatchmentSummaries.Count; c++) if (world.CatchmentSummaries[c].Settlement == id) tier = world.CatchmentSummaries[c].SizeTier;
            var structures = new List<StructureView>();
            for (int k = 0; k < world.Structures.Count; k++)
            {
                StructureRow st = world.Structures[k];
                if (st.Settlement != id || st.Count <= 0) continue;
                // ADR-033 D6: a project that FOUNDS an institution (a university building) is drawn ONCE, as the
                // institution, through InstitutionMarkerSource — never also as a generic structure glyph.
                if (FoundsInstitution(cfg, st.ProjectId)) continue;
                string pname = ProjectName(cfg, st.ProjectId);
                structures.Add(new StructureView(st.ProjectId, pname, KindOf(pname), st.Count));
            }
            double[]? sectors = null;
            for (int a = 0; a < world.SectorAllocations.Count; a++)
            {
                SectorAllocationRow sa = world.SectorAllocations[a];
                if (sa.Settlement != id) continue;
                sectors = [sa.Farming, sa.Herding, sa.Extraction, sa.Crafting, sa.Construction];
            }
            // Infrastructure density: built network edges with an endpoint in this settlement's catchment.
            var mine = new HashSet<int>();
            for (int c = 0; c < world.CatchmentNodes.Count; c++) if (world.CatchmentNodes[c].Settlement == id) mine.Add(world.CatchmentNodes[c].LatticeNode);
            int roadsHere = 0;
            for (int i = 0; i < world.NetworkEdges.Count; i++)
            {
                NetworkEdgeRow e = world.NetworkEdges[i];
                if (mine.Contains(anchor[e.A.Value]) || mine.Contains(anchor[e.B.Value])) roadsHere++;
            }
            int notables = 0;
            for (int n = 0; n < world.Notables.Count; n++) if (world.Notables[n].Settlement == id) notables++;
            int classes = 0;
            for (int c = 0; c < world.ClassStates.Count; c++) if (world.ClassStates[c].Settlement == id && world.ClassStates[c].Active != 0) classes++;
            int age = ages is not null && ctl >= 0 ? AgeQuery.CurrentAge(world, ages, controller) : 0;
            string? topLabel = null;
            if (ctl >= 0 && sectors is not null)
            {
                int top = 0;
                for (int k = 1; k < sectors.Length; k++) if (sectors[k] > sectors[top]) top = k;
                foreach (LabourActivity act in LabourActivities.For(world, cfg, controller))
                    if (act.Settlement == id && act.Sector == top) { topLabel = act.Label; break; }
            }
            int cell = row.SiteCell;
            IReadOnlyList<InstitutionView> held = institutions(world, cfg, id);
            if (held.Count > 0) anyInstitution = true;
            settlements.Add(new SettlementLensView(
                id.Value, name(id.Value), cell % size + 0.5, cell / size + 0.5, ctl, capital, ctl == player.Value,
                pop, dwellings, tier, age, structures, sectors, roadsHere, notables, classes, held, topLabel));
        }

        var units = new List<UnitLensView>();
        UnitFamilyContent? fam = cfg.UnitFamilies;
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = world.MilitaryUnits[i];
            UnitIdentity? ident = fam?.IdentityByKey(u.Identity);
            units.Add(new UnitLensView(u.Id, u.Owner.Value, u.X, u.Y, u.Family,
                fam?.FamilyByKey(u.Family)?.Name ?? "Formation", ident?.Name ?? "Formation", u.Location.Value));
        }

        int playerAge = ages is null ? 0 : AgeQuery.CurrentAge(world, ages, player);
        var absent = new List<string>
        {
            "Army movement and supply: not yet simulated - formations are shown where they are stationed",
        };
        if (!anyInstitution) absent.Add("Institutions: none founded");
        if (world.Structures.Count == 0) absent.Add("Structures: none built yet");
        if (routes.Count == 0) absent.Add("Roads: none developed - the free dirt-path baseline only");

        return new WorldProjection
        {
            WorldSize = size, Settlements = settlements, Units = units, Roads = roads, Routes = routes, Territory = territory,
            PlayerPolity = player.Value, PlayerAge = playerAge,
            PlayerAgeName = ages is null || playerAge == 0 ? "" : ages.Age(playerAge).Name,
            Absent = absent,
        };
    }

    private static bool SiteOf(IReadOnlyWorldState world, SettlementId id, int size, out double x, out double y)
    {
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            if (world.Settlements[s].Id.Value != id.Value) continue;
            int cell = world.Settlements[s].SiteCell;
            x = cell % size + 0.5;
            y = cell / size + 0.5;
            return true;
        }
        x = y = 0;
        return false;
    }

    private static (double, double) Center(int node, int lsize, int stride) =>
        (node % lsize * stride + stride / 2.0, node / lsize * stride + stride / 2.0);

    private static bool FoundsInstitution(SimConfig cfg, int projectId) =>
        cfg.Goods?.ProjectById(projectId) is { } p && InstitutionContent.FoundsInstitution(p);

    private static string ProjectName(SimConfig cfg, int projectId)
    {
        ConstructionProjectEntry[] projects = cfg.Goods?.Projects ?? [];
        foreach (ConstructionProjectEntry p in projects) if (p.Id == projectId) return p.Name;
        return "structure " + projectId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Visual classification of a construction project (presentation only).</summary>
    public static StructureKind KindOf(string projectName) => projectName switch
    {
        "granary" => StructureKind.Storage,
        "workshop" => StructureKind.Production,
        _ => StructureKind.Other,
    };
}

/// <summary>What one lens paint drew — the tests read this instead of pixels.
/// <c>LayerDraws[(int)MapLayer]</c> counts the draw commands emitted per map layer (draw-call accounting
/// for the single-owner rule); <c>Institutions</c> lists every institution marker drawn; <c>Paths</c>
/// every network piece drawn (stroke ownership on the Paths layer); <c>UnitPlacements</c> where each
/// formation token went.</summary>
public sealed record LensFrame(WorldZoom Zoom, IReadOnlyList<WorldLayer> Layers, IReadOnlyList<int> UnitTokens,
    IReadOnlyList<int> SettlementsDrawn, IReadOnlyList<int> LayerDraws, IReadOnlyList<InstitutionMarker> Institutions,
    IReadOnlyList<PathPiece> Paths, IReadOnlyList<UnitPlacement> UnitPlacements);

/// <summary>
/// THE WORLD LENS — zoom-dependent drawing of the projection over the map (D-047 Part 5).
/// Pure: given the projection, the camera (world px → screen px) and the viewport, it paints into a
/// <see cref="DrawList"/>. Morphology is AGGREGATED (footprint size from population, ring of
/// dwellings blocks from housing, one glyph per structure kind and per institution type with a count),
/// never per building, and it is a READ OF STATE ONLY — no physical feature is gated by the Age or any
/// date (D-038 H5); the Age shows as the banner. Every colour comes from <see cref="MapInk"/>.
/// </summary>
public static class WorldLens
{
    /// <summary>The zoom level for a camera: by the share of the world's width the view spans.
    /// More than 55% → World; more than 16% → Regional; otherwise Settlement.</summary>
    public static WorldZoom LevelFor(double zoom, double viewportW, double viewportH, double worldSize)
    {
        double span = Math.Min(viewportW, viewportH) / Math.Max(1e-9, zoom) / worldSize;
        return span > 0.55 ? WorldZoom.World : span > 0.16 ? WorldZoom.Regional : WorldZoom.Settlement;
    }

    private static readonly WorldLayer[] WorldLayers =
        [WorldLayer.Territories, WorldLayer.MajorInfrastructure, WorldLayer.MajorSettlements, WorldLayer.MilitaryFormations, WorldLayer.AgeBanners];
    private static readonly WorldLayer[] RegionalLayers =
        [WorldLayer.Territories, WorldLayer.Roads, WorldLayer.SettlementMorphology, WorldLayer.InstitutionalPresence,
         WorldLayer.ProductionSignals, WorldLayer.MilitaryFormations, WorldLayer.AgeBanners];
    private static readonly WorldLayer[] SettlementLayers =
        [WorldLayer.Roads, WorldLayer.SettlementMorphology, WorldLayer.InfrastructureDensity, WorldLayer.InstitutionTypes,
         WorldLayer.PopulationScale, WorldLayer.ProductionSignals, WorldLayer.MilitaryFormations, WorldLayer.AgeBanners];

    /// <summary>The layers drawn at a zoom level, in paint order.</summary>
    public static IReadOnlyList<WorldLayer> LayersFor(WorldZoom z) => z switch
    {
        WorldZoom.World => WorldLayers,
        WorldZoom.Regional => RegionalLayers,
        _ => SettlementLayers,
    };

    public static readonly string[] SectorNames = ["Farming", "Herding", "Extraction", "Crafting", "Construction"];

    /// <summary>Polity ink of the default map (<see cref="MapInk.PolityOf"/>).</summary>
    public static Rgba PolityInk(int polity, int player) => MapInk.Default.PolityOf(polity, player);

    private static Rgba A(Rgba c, double a) => Ink.With(c, a);

    /// <summary>Paints the lens. <paramref name="toScreen"/> maps world px to screen px; <paramref name="scale"/>
    /// is screen px per world px; <paramref name="ink"/> is the map's ink (the era seam, see
    /// <see cref="MapInk"/>; <see cref="MapInk.Default"/> when omitted).</summary>
    public static LensFrame Paint(DrawList d, ITextMeasure m, WorldProjection p, WorldZoom zoom,
        Func<double, double, (double X, double Y)> toScreen, double scale, RectD viewport, int selected = -1,
        bool showTerritory = true, MapInk? ink = null, EraTheme? theme = null, double uiScale = 1.0)
    {
        MapInk k = ink ?? MapInk.Default;
        _type = new MapType(theme, uiScale);
        IReadOnlyList<WorldLayer> layers = LayersFor(zoom);
        var draws = new int[MapLayerOwnership.All.Length];
        var institutions = new List<InstitutionMarker>();
        var pieces = new List<PathPiece>();
        var placements = new List<UnitPlacement>();
        int mark = d.Commands.Count;
        // Draw-call accounting: every command emitted since the last mark is charged to one map layer.
        void Charge(MapLayer l) { draws[(int)l] += d.Commands.Count - mark; mark = d.Commands.Count; }
        bool On(WorldLayer l) { foreach (WorldLayer x in layers) if (x == l) return true; return false; }
        var unitTokens = new List<int>();
        var drawn = new List<int>();

        if (On(WorldLayer.Territories) && showTerritory)
        {
            double alpha = zoom == WorldZoom.World ? 0.42 : 0.24;
            foreach (TerritoryBlock t in p.Territory)
            {
                (double x, double y) = toScreen(t.X, t.Y);
                double s = t.Size * scale;
                if (x > viewport.Right || y > viewport.Bottom || x + s < viewport.X || y + s < viewport.Y) continue;
                d.Rect(new RectD(x, y, s + 0.6, s + 0.6), A(k.PolityOf(t.Polity, p.PlayerPolity), alpha));
            }
        }
        Charge(MapLayer.Territory);

        // The network: the dirt-path baseline (dashed at World zoom, cased below) and the built routes
        // by class on top — the World zoom's major infrastructure, the closer zooms' roads.
        if (On(WorldLayer.Roads) || On(WorldLayer.MajorInfrastructure))
            RoadLens.PaintNetwork(d, m, p, zoom, toScreen, viewport, k, pieces);
        Charge(MapLayer.Paths);

        foreach (SettlementLensView s in p.Settlements)
        {
            (double sx, double sy) = toScreen(s.X, s.Y);
            if (sx < viewport.X - 200 || sx > viewport.Right + 200 || sy < viewport.Y - 200 || sy > viewport.Bottom + 200) continue;
            drawn.Add(s.Id);
            Rgba pol = k.PolityOf(s.Controller, p.PlayerPolity);
            if (zoom == WorldZoom.World)
            {
                double r = WorldMarkRadius(s);
                d.Circle(sx, sy, r + 2, A(k.SettlementHalo, 0.9), k.Ink, 1.2);
                d.Circle(sx, sy, r, pol, null);
                if (s.IsCapital) Star(d, sx, sy, r * 0.75, k.CapitalMark);
                Name(d, m, sx, sy + r + 5, s.Name, TypeRole.Body, k);
            }
            else PaintMorphology(d, m, p, s, sx, sy, scale, zoom, On, Charge, institutions, k);
            if (s.Id == selected) d.Circle(sx, sy, (zoom == WorldZoom.World ? 20 : FootprintRadius(s, scale, zoom) + 8), null, k.Selection, 2.4);
            Charge(MapLayer.SettlementMarkers);

            if (On(WorldLayer.AgeBanners) && s.Age > 0 && s.Controller >= 0)
                Banner(d, m, sx, sy - (zoom == WorldZoom.World ? 18 : FootprintRadius(s, scale, zoom) + 14), s.Age, pol, s.IsCapital, k);
            Charge(MapLayer.AgeBanners);
        }

        if (On(WorldLayer.MilitaryFormations))
        {
            double tok = zoom == WorldZoom.World ? 9 : 12;
            SettlementLensView?[] stations = Stations(p);
            int[] slots = FanSlots(p);
            for (int i = 0; i < p.Units.Count; i++)
            {
                UnitLensView u = p.Units[i];
                // A formation stationed in a settlement is anchored to it, just outside the mark's east
                // edge at mid-height (clear of the name below and the banner above); one in the field sits
                // at its own position. Formations sharing an anchor FAN OUT in ascending unit id (stable,
                // never overlapping): a row eastward at World zoom, a labelled column below that.
                SettlementLensView? station = stations[i];
                double wx = u.X + 0.5, wy = u.Y + 0.5, edge = 0;
                if (station is not null)
                {
                    wx = station.X; wy = station.Y;
                    edge = (zoom == WorldZoom.World ? WorldMarkRadius(station) + 2 : FootprintRadius(station, scale, zoom)) + tok + 3;
                }
                (double ux, double uy) = toScreen(wx, wy);
                (double fx, double fy) = FanOffset(zoom, slots[i], tok);
                ux += edge + fx;
                uy += fy;
                UnitToken(d, ux, uy, tok, u.FamilyKey, k.PolityOf(u.Owner, p.PlayerPolity), k);
                if (zoom != WorldZoom.World) _type.Label(d, ux + tok + 5, uy - _type.Size(TypeRole.Caption, FontRole.Caps) * 0.62, u.IdentityName, TypeRole.Caption, FontRole.Caps, k.Ink, TextAlign.Left);
                unitTokens.Add(u.Id);
                placements.Add(new UnitPlacement(u.Id, ux, uy, slots[i]));
            }
        }
        Charge(MapLayer.UnitTokens);
        return new LensFrame(zoom, layers, unitTokens, drawn, draws, institutions, pieces, placements);
    }

    /// <summary>The settlement each formation (parallel to <c>p.Units</c>) is stationed in, when the
    /// projection has it; null for a formation in the field.</summary>
    private static SettlementLensView?[] Stations(WorldProjection p)
    {
        var result = new SettlementLensView?[p.Units.Count];
        for (int i = 0; i < p.Units.Count; i++)
        {
            if (p.Units[i].Location < 0) continue;
            foreach (SettlementLensView st in p.Settlements)
                if (st.Id == p.Units[i].Location) { result[i] = st; break; }
        }
        return result;
    }

    /// <summary>THE FAN-OUT SLOTS (parallel to <c>p.Units</c>): a formation's slot is how many formations
    /// sharing its anchor — the same settlement, or in the field exactly the same position — have a
    /// smaller unit id. Integer keys only, independent of table order, so the layout is stable across
    /// turns and saves (unit ids are stable and never reused, ADR-031).</summary>
    public static int[] FanSlots(WorldProjection p)
    {
        SettlementLensView?[] stations = Stations(p);
        var slots = new int[p.Units.Count];
        for (int i = 0; i < p.Units.Count; i++)
            for (int j = 0; j < p.Units.Count; j++)
            {
                if (p.Units[j].Id >= p.Units[i].Id) continue;
                bool same = stations[i] is not null || stations[j] is not null
                    ? stations[i] is not null && stations[j] is not null && stations[i]!.Id == stations[j]!.Id
                    : p.Units[i].X == p.Units[j].X && p.Units[i].Y == p.Units[j].Y;
                if (same) slots[i]++;
            }
        return slots;
    }

    /// <summary>Vertical distance between fanned-out tokens in a column: a token plate is 1.5 × size tall, plus a gap.</summary>
    public static double FanPitch(double tokenSize) => tokenSize * 1.5 + 4;

    /// <summary>The screen offset of fan slot <paramref name="slot"/> from its anchor: at World zoom (no
    /// labels) a row eastward, plate width (2 × size) plus a gap apart; below it a column downward,
    /// <see cref="FanPitch"/> apart, each token's identity label to its right.</summary>
    public static (double Dx, double Dy) FanOffset(WorldZoom zoom, int slot, double tokenSize) =>
        zoom == WorldZoom.World ? (slot * (tokenSize * 2 + 3), 0.0) : (0.0, slot * FanPitch(tokenSize));

    /// <summary>The radius of a settlement's mark at World zoom (from population, capped).</summary>
    private static double WorldMarkRadius(SettlementLensView s) => Math.Min(5 + Math.Sqrt(Math.Max(0, s.Population)) / 14.0, 16);

    private static double FootprintRadius(SettlementLensView s, double scale, WorldZoom z)
    {
        double basePx = 2.0 + Math.Sqrt(Math.Max(0, s.Population)) / 22.0;   // world px
        double r = basePx * scale;
        return Math.Clamp(r, z == WorldZoom.Regional ? 14 : 34, z == WorldZoom.Regional ? 40 : 150);
    }

    private static void PaintMorphology(DrawList d, ITextMeasure m, WorldProjection p, SettlementLensView s,
        double sx, double sy, double scale, WorldZoom zoom, Func<WorldLayer, bool> on,
        Action<MapLayer> charge, List<InstitutionMarker> institutions, MapInk k)
    {
        Rgba ink = k.Ink;
        Rgba pol = k.PolityOf(s.Controller, p.PlayerPolity);
        double r = FootprintRadius(s, scale, zoom);

        // Footprint: the settlement's built extent (its radius from population), outlined. The
        // composition is a READ OF STATE (D-038 H5): no wall, palisade or tower is drawn, because no
        // settlement state records one — and the Age gates nothing physical (it flies as the banner).
        d.Circle(sx, sy, r, A(k.Footprint, 0.92), null);
        d.Circle(sx, sy, r, null, A(ink, 0.55), 1.2);

        // Dwellings: a ring of blocks, one per 25 dwellings (aggregated; capped) — one form at every Age.
        int blocks = (int)Math.Clamp(s.Dwellings / 25, 1, 48);
        for (int i = 0; i < blocks; i++)
        {
            double a = Math.PI * 2 * i / blocks + 0.3;
            double rr = r * (0.55 + 0.25 * ((i * 7) % 3) / 2.0);
            double bx = sx + Math.Cos(a) * rr, by = sy + Math.Sin(a) * rr;
            double bs = Math.Max(2.5, r / 9);
            d.Rect(new RectD(bx - bs / 2, by - bs / 2, bs, bs * 0.8), A(k.Dwelling, 0.85));
        }

        // Centre: the capital star / settlement mark.
        d.Circle(sx, sy, Math.Max(5, r / 6), pol, ink, 1.2);
        if (s.IsCapital) Star(d, sx, sy, Math.Max(4, r / 7), k.CapitalMark);

        charge(MapLayer.SettlementMarkers);
        if (on(WorldLayer.InstitutionalPresence) || on(WorldLayer.InstitutionTypes))
        {
            // Embedded glyphs inside the footprint — one per structure KIND and one per institution
            // TYPE (aggregated), with names and counts at settlement zoom.
            bool named = on(WorldLayer.InstitutionTypes);
            int total = s.Structures.Count + s.Institutions.Count;
            double g = zoom == WorldZoom.Settlement ? 9 : 6;
            double ring = r * (zoom == WorldZoom.Settlement ? 0.42 : 0.4);
            int n = 0;
            // Settlement zoom: a key beside the footprint (left, right-aligned) names each embedded glyph
            // with its aggregated count, so labels never overprint the footprint or each other.
            void Key(int i, string label, long count, Action<double, double> glyph)
            {
                double pitch = Math.Max(18, _type.Size(TypeRole.Caption, FontRole.Caps) * 1.3);
                double ky = sy - total * pitch / 2 + i * pitch + pitch / 2;
                double kx = sx - r - 18;
                glyph(kx, ky);
                _type.Label(d, kx - 12, ky - _type.Size(TypeRole.Caption, FontRole.Caps) * 0.62, label + (count > 1 ? " x" + count.ToString(CultureInfo.InvariantCulture) : ""), TypeRole.Caption, FontRole.Caps, ink, TextAlign.Right);
            }
            (double, double) Slot(int i)
            {
                double a = -Math.PI / 2 + Math.PI * 2 * i / Math.Max(1, total);
                return (sx + Math.Cos(a) * ring, sy + Math.Sin(a) * ring);
            }
            foreach (StructureView st in s.Structures)
            {
                (double gx, double gy) = Slot(n++);
                StructureGlyph(d, gx, gy, g, st.Kind, k);
                if (named) Key(n - 1, st.Name, st.Count, (x, y) => StructureGlyph(d, x, y, 7, st.Kind, k));
                institutions.Add(new InstitutionMarker(s.Id, "structure:" + st.ProjectId.ToString(CultureInfo.InvariantCulture), st.Count));
            }
            foreach (InstitutionView iv in s.Institutions)
            {
                (double gx, double gy) = Slot(n++);
                UniversityGlyph(d, gx, gy, g, iv.TypeKey, k);
                if (named) Key(n - 1, iv.Name, iv.Count, (x, y) => UniversityGlyph(d, x, y, 7, iv.TypeKey, k));
                institutions.Add(new InstitutionMarker(s.Id, "institution:" + iv.TypeKey.ToString(CultureInfo.InvariantCulture), iv.Count));
            }
            if (s.Notables > 0 && named)
                _type.Label(d, sx, sy + r * 0.12 + 6, s.Notables.ToString(CultureInfo.InvariantCulture) + " notable(s) resident", TypeRole.Secondary, FontRole.Body, ink, TextAlign.Center);
        }
        charge(MapLayer.InstitutionMarkers);

        if (on(WorldLayer.ProductionSignals) && s.Sectors is not null)
        {
            // Labour allocation as a ring of sector arcs just outside the footprint.
            double start = -90;
            for (int i = 0; i < 5; i++)
            {
                double sweep = 360 * s.Sectors![i];
                double ringR = r + (zoom == WorldZoom.Settlement ? 10 : 6), ringW = zoom == WorldZoom.Settlement ? 6 : 4;
                if (sweep >= 359) d.Circle(sx, sy, ringR, null, k.Sectors[i], ringW);
                else if (sweep > 0.5) d.Arc(sx, sy, ringR, start, sweep - 1.5, k.Sectors[i], ringW);
                start += sweep;
            }
        }

        double ly = sy + r + (on(WorldLayer.ProductionSignals) && s.Sectors is not null ? 18 : 8);
        TypeRole nameRole = zoom == WorldZoom.Settlement ? TypeRole.Heading : TypeRole.Body;
        Name(d, m, sx, ly, s.Name, nameRole, k);
        ly += _type.Size(nameRole, FontRole.Heading) * 1.35;
        double data = _type.Size(TypeRole.Data, FontRole.Numeric), second = _type.Size(TypeRole.Secondary, FontRole.Body);
        double caps = _type.Size(TypeRole.Caption, FontRole.Caps);
        if (on(WorldLayer.PopulationScale))
        {
            _type.Label(d, sx, ly, s.Population.ToString("#,0", CultureInfo.InvariantCulture) + " people  -  " + s.Dwellings.ToString("#,0", CultureInfo.InvariantCulture) + " dwellings", TypeRole.Data, FontRole.Numeric, ink, TextAlign.Center);
            ly += data * 1.3;
        }
        if (on(WorldLayer.InfrastructureDensity))
        {
            _type.Label(d, sx, ly, s.RoadsInCatchment.ToString(CultureInfo.InvariantCulture) + " path link(s) in catchment  -  size tier " + s.SizeTier.ToString(CultureInfo.InvariantCulture), TypeRole.Secondary, FontRole.Body, ink, TextAlign.Center);
            ly += second * 1.3;
        }
        if (on(WorldLayer.ProductionSignals) && zoom == WorldZoom.Settlement && s.Sectors is null)
            _type.Label(d, sx, ly, "labour split: default (no allocation ordered)", TypeRole.Caption, FontRole.Caps, ink, TextAlign.Center);
        else if (on(WorldLayer.ProductionSignals) && zoom == WorldZoom.Settlement && s.Sectors is not null)
        {
            int top = 0;
            for (int i = 1; i < 5; i++) if (s.Sectors[i] > s.Sectors[top]) top = i;   // ties: lowest sector index
            string sectorLabel = s.TopSectorLabel ?? SectorNames[top];
            string lab = "labour mostly " + sectorLabel.ToLowerInvariant() + " (" + Math.Round(s.Sectors[top] * 100).ToString("0", CultureInfo.InvariantCulture) + "%)";
            double lw = _type.Width(m, lab, TypeRole.Caption, FontRole.Caps);
            d.Rect(new RectD(sx - lw / 2 - 14, ly + caps * 0.25, 9, 9), k.Sectors[top], ink, 0.8);
            _type.Label(d, sx + 5, ly, lab, TypeRole.Caption, FontRole.Caps, ink, TextAlign.Center);
        }
    }

    /// <summary>A settlement's name on a soft paper plate, so a road running under it never cuts the letters (UR-7: at
    /// the body role in the era's heading face — it was 12.5 px — and the heading role at settlement zoom).</summary>
    private static void Name(DrawList d, ITextMeasure m, double x, double y, string name, TypeRole role, MapInk k)
    {
        double size = _type.Size(role, FontRole.Heading);
        double w = _type.Width(m, name, role, FontRole.Heading);
        d.Rect(new RectD(x - w / 2 - 4, y - 1, w + 8, size * 1.3 + 2), A(k.NamePlate, 0.78), null, 1, 3);
        _type.Label(d, x, y, name, role, FontRole.Heading, k.Ink, TextAlign.Center);
    }

    private static void Banner(DrawList d, ITextMeasure m, double x, double y, int age, Rgba pol, bool capital, MapInk k)
    {
        string t = AgePanelModel.Numeral(age);
        double size = _type.Size(TypeRole.Caption, FontRole.Caps);
        double w = _type.Width(m, t, TypeRole.Caption, FontRole.Caps) + 12;
        double h = size * 1.2;
        d.Line(x, y + 16, x, y + 2, k.Ink, 1.4);
        d.Polygon([(x, y - h), (x + w, y - h), (x + w - 4, y - h * 0.35), (x + w, y + 6), (x, y + 6)], pol);
        _type.Label(d, x + 6, y - h + 1, t, TypeRole.Caption, FontRole.Caps, capital ? k.CapitalMark : k.BannerText, TextAlign.Left);
    }

    /// <summary>The map's type for this paint (UR-7): the era's styles at the type scale's roles × the UI scale; with no
    /// theme (previews, tests), the same roles in the pre-theme faces.</summary>
    [ThreadStatic] private static MapType _type;

    private readonly struct MapType(EraTheme? theme, double uiScale)
    {
        private double S => uiScale > 0 ? uiScale : 1.0;

        /// <summary>The design size of a role (before the era's size scale, which <see cref="ThemeText.Write"/> applies).</summary>
        public double Size(TypeRole role, FontRole font) => theme is { } t
            ? TypeScale.Px(t, role, font) * S
            : TypeScale.Px(role, font == FontRole.Numeric ? TypeFace.PlexSerif : TypeFace.Garamond, font == FontRole.Caps) * S;

        public double Width(ITextMeasure m, string text, TypeRole role, FontRole font) => theme is { } t
            ? m.Width(t, text, Size(role, font), font)
            : m.Width(text, Size(role, font), font);

        public void Label(DrawList d, double x, double y, string text, TypeRole role, FontRole font, Rgba color, TextAlign align)
        {
            if (theme is { } t) d.Write(t, x, y, text, Size(role, font), color, align, font);
            else d.Text(x, y, text, Size(role, font), color, align, font);
        }
    }

    private static void Star(DrawList d, double cx, double cy, double r, Rgba c)
    {
        var pts = new (double, double)[10];
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + Math.PI * i / 5;
            double rr = i % 2 == 0 ? r : r * 0.45;
            pts[i] = (cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr);
        }
        d.Polygon(pts, c);
    }

    private static void StructureGlyph(DrawList d, double x, double y, double g, StructureKind kind, MapInk k)
    {
        Rgba ink = k.Ink;
        switch (kind)
        {
            case StructureKind.Storage:   // granary: domed silo
                d.Rect(new RectD(x - g * 0.6, y - g * 0.2, g * 1.2, g * 0.9), k.Granary, ink, 1);
                d.Arc(x, y - g * 0.2, g * 0.6, 180, 180, ink, 1.4);
                break;
            case StructureKind.Production: // workshop: anvil-roofed block with a chimney
                d.Rect(new RectD(x - g * 0.7, y - g * 0.3, g * 1.4, g * 0.9), k.Workshop, ink, 1);
                d.Rect(new RectD(x + g * 0.25, y - g * 0.8, g * 0.25, g * 0.5), ink);
                break;
            default:
                d.Rect(new RectD(x - g / 2, y - g / 2, g, g), k.StructureOther, ink, 1);
                break;
        }
    }

    /// <summary>Fill of each specialized-university type (keys 1..5: military, medical, engineering,
    /// natural science, agricultural) — five distinct inks (<see cref="MapInk.InstitutionInk"/>).</summary>
    public static Rgba UniversityInk(int typeKey, MapInk? ink = null) => (ink ?? MapInk.Default).InstitutionInk(typeKey);

    /// <summary>An institution glyph: a pedimented hall in the type's ink, with the type's emblem on its
    /// face — for the five universities crossed blades, a cross, a gear, an eye/orbit, a sheaf.</summary>
    public static void UniversityGlyph(DrawList d, double x, double y, double g, int typeKey, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        Rgba frame = k.GlyphInk;
        Rgba fill = k.InstitutionInk(typeKey);
        Rgba mark = typeKey == 2 ? k.MedicalMark : k.EmblemMark;
        d.Polygon([(x - g * 0.85, y - g * 0.35), (x, y - g * 0.95), (x + g * 0.85, y - g * 0.35)], frame);
        d.Rect(new RectD(x - g * 0.75, y - g * 0.35, g * 1.5, g * 1.15), fill, frame, 1);
        double e = g * 0.38, cy = y + g * 0.22;
        switch (typeKey)
        {
            case 1: d.Line(x - e, cy - e, x + e, cy + e, mark, 1.6); d.Line(x - e, cy + e, x + e, cy - e, mark, 1.6); break;
            case 2: d.Rect(new RectD(x - e * 0.3, cy - e, e * 0.6, e * 2), mark); d.Rect(new RectD(x - e, cy - e * 0.3, e * 2, e * 0.6), mark); break;
            case 3: d.Circle(x, cy, e * 0.75, null, mark, 1.6); for (int i = 0; i < 6; i++) { double a = Math.PI * i / 3; d.Line(x + Math.Cos(a) * e * 0.75, cy + Math.Sin(a) * e * 0.75, x + Math.Cos(a) * e * 1.1, cy + Math.Sin(a) * e * 1.1, mark, 1.4); } break;
            case 4: d.Circle(x, cy, e * 0.35, mark, null); d.Circle(x, cy, e, null, mark, 1.1); break;
            case 5: d.Line(x, cy + e, x, cy - e, mark, 1.4); d.Line(x, cy - e * 0.2, x - e * 0.7, cy - e * 0.9, mark, 1.2); d.Line(x, cy - e * 0.2, x + e * 0.7, cy - e * 0.9, mark, 1.2); break;
            default: d.Circle(x, cy, e * 0.5, mark, null); break;
        }
    }

    /// <summary>A formation token: a framed plate whose emblem is the unit FAMILY (twelve families,
    /// twelve emblems), filled with the owner's ink.</summary>
    public static void UnitToken(DrawList d, double x, double y, double s, int family, Rgba owner, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        Rgba frame = k.GlyphInk, pale = k.EmblemMark;
        d.Rect(new RectD(x - s, y - s * 0.75, s * 2, s * 1.5), owner, frame, 1.4, 2);
        double e = s * 0.5;
        switch (family)
        {
            case 1: d.Line(x - e, y + e * 0.6, x, y - e * 0.8, pale, 1.6); d.Line(x, y - e * 0.8, x + e, y + e * 0.6, pale, 1.6); break; // light infantry: chevron
            case 2: d.Line(x - e, y + e, x + e, y - e, pale, 1.8); d.Polygon([(x + e, y - e), (x + e * 0.3, y - e * 0.8), (x + e * 0.8, y - e * 0.3)], pale); break; // spear
            case 3: d.Line(x - e, y - e * 0.7, x + e, y + e * 0.7, pale, 1.6); d.Line(x - e, y + e * 0.7, x + e, y - e * 0.7, pale, 1.6); break; // heavy infantry: crossed
            case 4: d.Circle(x, y, e * 0.7, null, pale, 1.6); d.Circle(x, y, 1.6, pale, null); break; // ranged: target
            case 5: d.Line(x - e, y + e * 0.7, x + e, y - e * 0.7, pale, 1.8); break; // light cavalry: slash
            case 6: d.Line(x - e, y + e * 0.7, x + e, y - e * 0.7, pale, 2.6); d.Line(x - e, y - e * 0.2, x + e * 0.2, y + e * 0.7, pale, 1.4); break;
            case 7: d.Line(x - e, y + e * 0.7, x + e, y - e * 0.7, pale, 1.8); d.Circle(x - e * 0.4, y - e * 0.3, 2, pale, null); break;
            case 8: d.Circle(x, y + e * 0.3, e * 0.45, null, pale, 1.6); d.Line(x - e, y - e * 0.2, x + e, y - e * 0.2, pale, 1.6); break; // siege
            case 9: d.Polygon([(x - e, y), (x + e, y), (x + e * 0.6, y + e * 0.6), (x - e * 0.6, y + e * 0.6)], pale); d.Line(x, y, x, y - e, pale, 1.4); break;
            case 10: d.Polygon([(x - e, y), (x + e, y), (x + e * 0.6, y + e * 0.6), (x - e * 0.6, y + e * 0.6)], pale); break;
            case 11: d.Line(x - e, y, x + e, y, pale, 1.6); d.Line(x, y - e * 0.7, x, y + e * 0.7, pale, 1.6); break;
            case 12: d.Polygon([(x, y - e), (x + e * 0.35, y + e * 0.6), (x - e * 0.35, y + e * 0.6)], pale); break;
            default: d.Circle(x, y, e * 0.5, pale, null); break;
        }
    }
}
