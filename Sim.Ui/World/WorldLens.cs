using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Ages;
using Sim.Ui.Render;
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

/// <summary>One settlement as the lens draws it — every field read from a published table.</summary>
public sealed record SettlementLensView(
    int Id, string Name, double X, double Y, int Controller, bool IsCapital, bool IsPlayer,
    long Population, long Dwellings, int SizeTier, int Age,
    IReadOnlyList<StructureView> Structures,
    double[]? Sectors, int RoadsInCatchment, int Notables, int ClassesActive);

/// <summary>One military formation token (a MilitaryUnits row).</summary>
public sealed record UnitLensView(int Id, int Owner, double X, double Y, int FamilyKey, string FamilyName, string IdentityName, int Location);

public sealed record RoadSegment(double X0, double Y0, double X1, double Y1, int EdgeType);

/// <summary>A territory block: one lattice node a settlement's catchment claims.</summary>
public readonly record struct TerritoryBlock(double X, double Y, double Size, int Polity);

/// <summary>
/// THE WORLD LENS PROJECTION — a read-only, aggregated view of real simulation state for the map
/// (D-047 Part 4 G, Part 5). Settlements, structures (counted per settlement × project; never
/// individually placed), the built network, catchment territories, formations, Ages. Built once per
/// turn; nothing here is invented: a system that does not exist is reported as such by
/// <see cref="Absent"/>, never drawn as if it did.
/// </summary>
public sealed class WorldProjection
{
    public required double WorldSize { get; init; }
    public required IReadOnlyList<SettlementLensView> Settlements { get; init; }
    public required IReadOnlyList<UnitLensView> Units { get; init; }
    public required IReadOnlyList<RoadSegment> Roads { get; init; }
    public required IReadOnlyList<TerritoryBlock> Territory { get; init; }
    public required int PlayerPolity { get; init; }
    public required int PlayerAge { get; init; }
    public required string PlayerAgeName { get; init; }
    /// <summary>Specialized universities the player's polity holds (ResearchCostModifiers rows) —
    /// polity-level only: the simulation does not place a university in a settlement.</summary>
    public required int PlayerUniversityTypes { get; init; }
    /// <summary>Honest empty states: systems the map would show if they existed.</summary>
    public required IReadOnlyList<string> Absent { get; init; }

    public static WorldProjection Build(IReadOnlyWorldState world, SimConfig cfg, Func<int, string> name, PolityId player)
    {
        int size = world.Terrain?.Size ?? 256;
        var lattice = world.Terrain is null ? null
            : Sim.Core.Pathing.TraversalLattice.Build(world.Terrain, cfg.Transport.RiverCostFactor);
        int lsize = lattice?.Size ?? 1, stride = lattice is null ? 1 : size / lattice.Size;

        // Network: node id -> lattice node, in table order (ids are table order).
        int maxId = -1;
        for (int i = 0; i < world.NetworkNodes.Count; i++) maxId = Math.Max(maxId, world.NetworkNodes[i].Id.Value);
        var anchor = new int[maxId + 1];
        for (int i = 0; i < world.NetworkNodes.Count; i++) anchor[world.NetworkNodes[i].Id.Value] = world.NetworkNodes[i].LatticeNode;
        var roads = new List<RoadSegment>();
        for (int i = 0; i < world.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow e = world.NetworkEdges[i];
            (double ax, double ay) = Center(anchor[e.A.Value], lsize, stride);
            (double bx, double by) = Center(anchor[e.B.Value], lsize, stride);
            roads.Add(new RoadSegment(ax, ay, bx, by, e.EdgeType));
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
            int cell = row.SiteCell;
            settlements.Add(new SettlementLensView(
                id.Value, name(id.Value), cell % size + 0.5, cell / size + 0.5, ctl, capital, ctl == player.Value,
                pop, dwellings, tier, age, structures, sectors, roadsHere, notables, classes));
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

        int universities = 0;
        for (int i = 0; i < world.ResearchCostModifiers.Count; i++) if (world.ResearchCostModifiers[i].Polity == player) universities++;

        int playerAge = ages is null ? 0 : AgeQuery.CurrentAge(world, ages, player);
        var absent = new List<string>
        {
            "Army movement and supply: not yet simulated - formations are shown where they are stationed",
        };
        if (universities == 0) absent.Add("Universities: none founded (no specialized-university state for this polity)");
        if (world.Structures.Count == 0) absent.Add("Structures: none built yet");

        return new WorldProjection
        {
            WorldSize = size, Settlements = settlements, Units = units, Roads = roads, Territory = territory,
            PlayerPolity = player.Value, PlayerAge = playerAge,
            PlayerAgeName = ages is null || playerAge == 0 ? "" : ages.Age(playerAge).Name,
            PlayerUniversityTypes = universities, Absent = absent,
        };
    }

    private static (double, double) Center(int node, int lsize, int stride) =>
        (node % lsize * stride + stride / 2.0, node / lsize * stride + stride / 2.0);

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

/// <summary>What one lens paint drew — the tests read this instead of pixels.</summary>
public sealed record LensFrame(WorldZoom Zoom, IReadOnlyList<WorldLayer> Layers, IReadOnlyList<int> UnitTokens, IReadOnlyList<int> SettlementsDrawn);

/// <summary>
/// THE WORLD LENS — zoom-dependent drawing of the projection over the map (D-047 Part 5).
/// Pure: given the projection, the camera (world px → screen px) and the viewport, it paints into a
/// <see cref="DrawList"/>. Morphology is AGGREGATED (footprint size from population, ring of
/// dwellings blocks from housing, one glyph per structure kind with a count), never per building.
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
    private static readonly Rgba[] SectorInk =
        [Rgba.Hex(0x8FA34E), Rgba.Hex(0xC2A060), Rgba.Hex(0x8A8A86), Rgba.Hex(0xC07048), Rgba.Hex(0x6C8FB0)];

    /// <summary>Polity ink: the player in gold, other polities in distinct muted hues.</summary>
    public static Rgba PolityInk(int polity, int player)
    {
        if (polity == player) return Rgba.Hex(0xC8962E);
        Rgba[] hues = [Rgba.Hex(0x9B3B3B), Rgba.Hex(0x3F6E8C), Rgba.Hex(0x5C7F45), Rgba.Hex(0x7A4F8C), Rgba.Hex(0x2F7C74), Rgba.Hex(0x8C6A3F)];
        int i = polity < 0 ? 0 : polity % hues.Length;
        return polity < 0 ? Rgba.Hex(0x777066) : hues[i];
    }

    private static Rgba A(Rgba c, double a) => Ink.With(c, a);

    /// <summary>Paints the lens. <paramref name="toScreen"/> maps world px to screen px; <paramref name="scale"/>
    /// is screen px per world px.</summary>
    public static LensFrame Paint(DrawList d, ITextMeasure m, WorldProjection p, WorldZoom zoom,
        Func<double, double, (double X, double Y)> toScreen, double scale, RectD viewport, int selected = -1)
    {
        IReadOnlyList<WorldLayer> layers = LayersFor(zoom);
        bool On(WorldLayer l) { foreach (WorldLayer x in layers) if (x == l) return true; return false; }
        var unitTokens = new List<int>();
        var drawn = new List<int>();
        Rgba ink = Rgba.Hex(0x3A2E1F);

        if (On(WorldLayer.Territories))
        {
            double alpha = zoom == WorldZoom.World ? 0.42 : 0.24;
            foreach (TerritoryBlock t in p.Territory)
            {
                (double x, double y) = toScreen(t.X, t.Y);
                double s = t.Size * scale;
                if (x > viewport.Right || y > viewport.Bottom || x + s < viewport.X || y + s < viewport.Y) continue;
                d.Rect(new RectD(x, y, s + 0.6, s + 0.6), A(PolityInk(t.Polity, p.PlayerPolity), alpha));
            }
        }

        if (On(WorldLayer.Roads) || On(WorldLayer.MajorInfrastructure))
        {
            bool major = !On(WorldLayer.Roads);
            double w = major ? 1.6 : zoom == WorldZoom.Settlement ? 3.2 : 2.2;
            foreach (RoadSegment r in p.Roads)
            {
                (double x0, double y0) = toScreen(r.X0, r.Y0);
                (double x1, double y1) = toScreen(r.X1, r.Y1);
                if (!major) d.Line(x0, y0, x1, y1, A(Rgba.Hex(0xF2E6C8), 0.7), w + 2.2);
                d.Line(x0, y0, x1, y1, A(Rgba.Hex(0x6B4A2A), major ? 0.75 : 0.95), w, major ? (4, 3) : null);
            }
        }

        foreach (SettlementLensView s in p.Settlements)
        {
            (double sx, double sy) = toScreen(s.X, s.Y);
            if (sx < viewport.X - 200 || sx > viewport.Right + 200 || sy < viewport.Y - 200 || sy > viewport.Bottom + 200) continue;
            drawn.Add(s.Id);
            Rgba pol = PolityInk(s.Controller, p.PlayerPolity);
            if (zoom == WorldZoom.World)
            {
                double r = 5 + Math.Sqrt(Math.Max(0, s.Population)) / 14.0;
                r = Math.Min(r, 16);
                d.Circle(sx, sy, r + 2, A(Rgba.Hex(0xF4EBD3), 0.9), ink, 1.2);
                d.Circle(sx, sy, r, pol, null);
                if (s.IsCapital) Star(d, sx, sy, r * 0.75, Rgba.Hex(0xFFF1C4));
                d.Text(sx, sy + r + 5, s.Name, 12.5, ink, TextAlign.Center, FontRole.Heading);
            }
            else PaintMorphology(d, m, p, s, sx, sy, scale, zoom, On);

            if (On(WorldLayer.AgeBanners) && s.Age > 0 && s.Controller >= 0)
                Banner(d, m, sx, sy - (zoom == WorldZoom.World ? 18 : FootprintRadius(s, scale, zoom) + 14), s.Age, pol, s.IsCapital);
            if (s.Id == selected) d.Circle(sx, sy, (zoom == WorldZoom.World ? 20 : FootprintRadius(s, scale, zoom) + 8), null, Rgba.Hex(0xFFD25A), 2.4);
        }

        if (On(WorldLayer.MilitaryFormations))
        {
            // Stack formations sharing a station so each row stays visible.
            var seen = new List<(double, double)>();
            foreach (UnitLensView u in p.Units)
            {
                int k = 0;
                foreach ((double X, double Y) q in seen) if (q.X == u.X && q.Y == u.Y) k++;
                seen.Add((u.X, u.Y));
                double tok = zoom == WorldZoom.World ? 9 : 12;
                // A formation stationed in a settlement is anchored to it and sits on its footprint's
                // lower-right rim; one in the field sits at its own position.
                double wx = u.X + 0.5, wy = u.Y + 0.5, rim = 0;
                foreach (SettlementLensView st in p.Settlements)
                    if (st.Id == u.Location) { wx = st.X; wy = st.Y; rim = zoom == WorldZoom.World ? 12 : FootprintRadius(st, scale, zoom) * 0.7; }
                (double ux, double uy) = toScreen(wx, wy);
                ux += rim + k * (tok * 2 + 4);
                uy += rim;
                UnitToken(d, ux, uy, tok, u.FamilyKey, PolityInk(u.Owner, p.PlayerPolity));
                if (zoom != WorldZoom.World) d.Text(ux + tok + 5, uy - 7, u.IdentityName, 11, ink, TextAlign.Left, FontRole.Caps);
                unitTokens.Add(u.Id);
            }
        }
        return new LensFrame(zoom, layers, unitTokens, drawn);
    }

    private static double FootprintRadius(SettlementLensView s, double scale, WorldZoom z)
    {
        double basePx = 2.0 + Math.Sqrt(Math.Max(0, s.Population)) / 22.0;   // world px
        double r = basePx * scale;
        return Math.Clamp(r, z == WorldZoom.Regional ? 14 : 34, z == WorldZoom.Regional ? 40 : 150);
    }

    private static void PaintMorphology(DrawList d, ITextMeasure m, WorldProjection p, SettlementLensView s,
        double sx, double sy, double scale, WorldZoom zoom, Func<WorldLayer, bool> on)
    {
        Rgba ink = Rgba.Hex(0x3A2E1F);
        Rgba pol = PolityInk(s.Controller, p.PlayerPolity);
        double r = FootprintRadius(s, scale, zoom);

        // Footprint: the settlement's built extent, styled by its controller's Age.
        d.Circle(sx, sy, r, A(Rgba.Hex(0xEADBB8), 0.92), null);
        if (s.Age >= 3)
        {
            var pts = new (double, double)[8];
            for (int i = 0; i < 8; i++) pts[i] = (sx + Math.Cos(Math.PI * (i + 0.5) / 4) * r, sy + Math.Sin(Math.PI * (i + 0.5) / 4) * r);
            for (int i = 0; i < 8; i++) d.Line(pts[i].Item1, pts[i].Item2, pts[(i + 1) % 8].Item1, pts[(i + 1) % 8].Item2, ink, 3.0);
            foreach ((double x, double y) q in pts) d.Rect(new RectD(q.x - 3, q.y - 3, 6, 6), ink);
        }
        else if (s.Age == 2)
        {
            d.Circle(sx, sy, r, null, Rgba.Hex(0x6B4A2A), 2.4);
            for (int i = 0; i < 36; i++)
            {
                double a = Math.PI * 2 * i / 36;
                d.Line(sx + Math.Cos(a) * r, sy + Math.Sin(a) * r, sx + Math.Cos(a) * (r + 4), sy + Math.Sin(a) * (r + 4), Rgba.Hex(0x6B4A2A), 1.6);
            }
        }
        else d.Circle(sx, sy, r, null, A(ink, 0.55), 1.2);

        // Dwellings: a ring of blocks, one per ~200 dwellings (aggregated; capped), hut dots in Age I.
        int blocks = (int)Math.Clamp(s.Dwellings / 25, 1, 48);   // one block per 25 dwellings (aggregated)
        for (int i = 0; i < blocks; i++)
        {
            double a = Math.PI * 2 * i / blocks + 0.3;
            double rr = r * (0.55 + 0.25 * ((i * 7) % 3) / 2.0);
            double bx = sx + Math.Cos(a) * rr, by = sy + Math.Sin(a) * rr;
            double bs = Math.Max(2.5, r / 9);
            if (s.Age <= 1) d.Circle(bx, by, bs * 0.6, A(Rgba.Hex(0x8F6B4E), 0.9), null);
            else d.Rect(new RectD(bx - bs / 2, by - bs / 2, bs, bs * 0.8), A(Rgba.Hex(0x8C4A3A), 0.85));
        }

        // Centre: the capital star / settlement mark.
        d.Circle(sx, sy, Math.Max(5, r / 6), pol, ink, 1.2);
        if (s.IsCapital) Star(d, sx, sy, Math.Max(4, r / 7), Rgba.Hex(0xFFF1C4));

        if (on(WorldLayer.InstitutionalPresence) || on(WorldLayer.InstitutionTypes))
        {
            // Embedded institution glyphs on the inner ring — one per structure KIND, with counts at settlement zoom.
            int k = 0;
            foreach (StructureView st in s.Structures)
            {
                double a = -Math.PI / 2 + k * 0.9;
                double gx = sx + Math.Cos(a) * r * 0.32, gy = sy + Math.Sin(a) * r * 0.32;
                double g = zoom == WorldZoom.Settlement ? 9 : 6;
                StructureGlyph(d, gx, gy, g, st.Kind);
                if (on(WorldLayer.InstitutionTypes))
                    d.Text(gx + g + 3, gy - 6, st.Name + (st.Count > 1 ? " x" + st.Count.ToString(CultureInfo.InvariantCulture) : ""), 11, ink, TextAlign.Left, FontRole.Caps);
                k++;
            }
            if (s.Notables > 0 && on(WorldLayer.InstitutionTypes))
                d.Text(sx, sy + r * 0.32 + 6, s.Notables.ToString(CultureInfo.InvariantCulture) + " notable(s) resident", 10.5, A(ink, 0.8), TextAlign.Center);
        }

        if (on(WorldLayer.ProductionSignals) && s.Sectors is not null)
        {
            // Labour allocation as a ring of sector arcs just outside the footprint.
            double start = -90;
            for (int i = 0; i < 5; i++)
            {
                double sweep = 360 * s.Sectors![i];
                double ringR = r + (zoom == WorldZoom.Settlement ? 10 : 6), ringW = zoom == WorldZoom.Settlement ? 6 : 4;
                if (sweep >= 359) d.Circle(sx, sy, ringR, null, SectorInk[i], ringW);
                else if (sweep > 0.5) d.Arc(sx, sy, ringR, start, sweep - 1.5, SectorInk[i], ringW);
                start += sweep;
            }
        }

        double ly = sy + r + (on(WorldLayer.ProductionSignals) && s.Sectors is not null ? 18 : 8);
        d.Text(sx, ly, s.Name, zoom == WorldZoom.Settlement ? 15 : 12.5, ink, TextAlign.Center, FontRole.Heading);
        ly += zoom == WorldZoom.Settlement ? 19 : 15;
        if (on(WorldLayer.PopulationScale))
        {
            d.Text(sx, ly, s.Population.ToString("#,0", CultureInfo.InvariantCulture) + " people  -  " + s.Dwellings.ToString("#,0", CultureInfo.InvariantCulture) + " dwellings", 11.5, ink, TextAlign.Center, FontRole.Numeric);
            ly += 15;
        }
        if (on(WorldLayer.InfrastructureDensity))
        {
            d.Text(sx, ly, s.RoadsInCatchment.ToString(CultureInfo.InvariantCulture) + " path link(s) in catchment  -  size tier " + s.SizeTier.ToString(CultureInfo.InvariantCulture), 11, A(ink, 0.85), TextAlign.Center);
            ly += 15;
        }
        if (on(WorldLayer.ProductionSignals) && zoom == WorldZoom.Settlement && s.Sectors is null)
            d.Text(sx, ly, "labour split: default (no allocation ordered)", 11, Ink.With(ink, 0.8), TextAlign.Center, FontRole.Caps);
        else if (on(WorldLayer.ProductionSignals) && zoom == WorldZoom.Settlement && s.Sectors is not null)
        {
            int top = 0;
            for (int i = 1; i < 5; i++) if (s.Sectors[i] > s.Sectors[top]) top = i;   // ties: lowest sector index
            string lab = "labour mostly " + SectorNames[top].ToLowerInvariant() + " (" + Math.Round(s.Sectors[top] * 100).ToString("0", CultureInfo.InvariantCulture) + "%)";
            double lw = m.Width(lab, 11, FontRole.Caps);
            d.Rect(new RectD(sx - lw / 2 - 14, ly + 2, 9, 9), SectorInk[top], ink, 0.8);
            d.Text(sx + 5, ly, lab, 11, ink, TextAlign.Center, FontRole.Caps);
        }
    }

    private static void Banner(DrawList d, ITextMeasure m, double x, double y, int age, Rgba pol, bool capital)
    {
        string t = AgePanelModel.Numeral(age);
        double w = m.Width(t, 11, FontRole.Caps) + 12;
        d.Line(x, y + 16, x, y + 2, Rgba.Hex(0x3A2E1F), 1.4);
        d.Polygon([(x, y - 10), (x + w, y - 10), (x + w - 4, y - 2), (x + w, y + 6), (x, y + 6)], pol);
        d.Text(x + 5, y - 8, t, 11, capital ? Rgba.Hex(0xFFF1C4) : Rgba.Hex(0xF4EBD3), TextAlign.Left, FontRole.Caps);
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

    private static void StructureGlyph(DrawList d, double x, double y, double g, StructureKind k)
    {
        Rgba ink = Rgba.Hex(0x3A2E1F);
        switch (k)
        {
            case StructureKind.Storage:   // granary: domed silo
                d.Rect(new RectD(x - g * 0.6, y - g * 0.2, g * 1.2, g * 0.9), Rgba.Hex(0xC9A35A), ink, 1);
                d.Arc(x, y - g * 0.2, g * 0.6, 180, 180, ink, 1.4);
                break;
            case StructureKind.Production: // workshop: anvil-roofed block with a chimney
                d.Rect(new RectD(x - g * 0.7, y - g * 0.3, g * 1.4, g * 0.9), Rgba.Hex(0xB06A44), ink, 1);
                d.Rect(new RectD(x + g * 0.25, y - g * 0.8, g * 0.25, g * 0.5), ink);
                break;
            default:
                d.Rect(new RectD(x - g / 2, y - g / 2, g, g), Rgba.Hex(0x9A8F7A), ink, 1);
                break;
        }
    }

    /// <summary>A formation token: a framed plate whose emblem is the unit FAMILY (twelve families,
    /// twelve emblems), filled with the owner's ink.</summary>
    public static void UnitToken(DrawList d, double x, double y, double s, int family, Rgba owner)
    {
        Rgba ink = Rgba.Hex(0x1E1810), pale = Rgba.Hex(0xF4EBD3);
        d.Rect(new RectD(x - s, y - s * 0.75, s * 2, s * 1.5), owner, ink, 1.4, 2);
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
