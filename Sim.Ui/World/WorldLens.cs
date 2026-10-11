using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
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
public sealed record UnitLensView(int Id, int Owner, double X, double Y, int FamilyKey, string FamilyName, string IdentityName, int Location)
{
    /// <summary>P11: the token's emblem, keyed by the row's CURRENT identity (<see cref="UnitEmblem.Of"/>; the projection
    /// always sets it). Null — a view built without content — draws the family mark alone (<see cref="UnitEmblem.Unkeyed"/>).</summary>
    public UnitEmblem? Emblem { get; init; }
}

/// <summary>Where the lens placed one formation token (screen px, the token's centre) and its fan-out
/// slot among the formations sharing its anchor (0 = first in ascending unit id).</summary>
public sealed record UnitPlacement(int Id, double X, double Y, int Slot)
{
    /// <summary>P2: the half-size the token was drawn at (its plate is 2 × 1.5 times it) — blended between the bands'
    /// 9 and 12 px across the World ↔ Regional window; 0 when the placement was not made by a paint (the hit test then
    /// uses <see cref="ViewModel.UnitSelection.TokenSize"/> of the frame's band).</summary>
    public double Size { get; init; }
}

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
    /// <summary>P2: km per world px — the terrain's own scale (<c>TerrainSet.KmPerPx</c>; D-015's 4 km at the canonical
    /// config). The continuous level of detail is keyed on the km the view shows (<see cref="MapDetail"/>).</summary>
    public required double KmPerPx { get; init; }
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

    /// <summary>P12a: the kinds of part a settlement can hold, from content (<see cref="PartUniverse.Of"/>) — every settlement
    /// reserves a lot for each. Empty when the projection was built without content (a hand-made projection): every held
    /// kind then claims in (category, key) order.</summary>
    public PartUniverse Universe { get; init; } = PartUniverse.Empty;

    /// <summary>P12a: the settlement visual classes and composition lattice (<c>settlement-classes.json</c>).</summary>
    public SettlementClasses Classes { get; init; } = SettlementClasses.Default;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SettlementLensView, SettlementComposition> _compositions = new();

    /// <summary>P12a: the composition of one of this projection's settlements (<see cref="SettlementComposer.Compose"/> of
    /// the view, <see cref="Universe"/> and <see cref="Classes"/>) — a pure function of them, computed once per view instance
    /// (a memo, never state: a changed view is a new instance and is composed afresh).</summary>
    public SettlementComposition CompositionOf(SettlementLensView s) =>
        _compositions.GetValue(s, v => SettlementComposer.Compose(v, Universe, Classes));

    /// <summary>The stride every system builds the traversal lattice with (<c>TraversalLattice.Build</c>'s default; the
    /// m1 spec §3 stride-4 sampling). The lens needs only the lattice's GEOMETRY, never its costs.</summary>
    public const int LatticeStride = Sim.Core.Pathing.TraversalLattice.DefaultStride;

    /// <summary>
    /// The traversal lattice's geometry for a terrain of <paramref name="terrainSize"/> px, by arithmetic: nodes per
    /// side = size / <see cref="LatticeStride"/> (exactly <c>TraversalLattice.Build</c>'s <c>terrain.Size / stride</c>)
    /// and the block width in terrain px = size / nodes per side (exactly what this projection derived from the built
    /// lattice before — <c>size / lattice.Size</c>). Building the lattice to learn these two integers cost a full
    /// pass over every terrain cell each turn (P4); the pin is <c>WorldProjectionStrideTests</c>, which builds the
    /// lattice and compares both the geometry and the drawn map.
    /// </summary>
    public static (int LatticeSize, int Stride) LatticeGrid(int terrainSize)
    {
        int lsize = terrainSize / LatticeStride;
        return (lsize, terrainSize / lsize);
    }

    /// <summary>Builds the projection. <paramref name="institutions"/> replaces the institution seam
    /// (tests only); the game and previews always read <see cref="InstitutionMarkerSource.InstitutionsAt"/>.
    /// <paramref name="grid"/> replaces the lattice geometry (tests only: the stride pin builds the same projection
    /// on the geometry of a built <c>TraversalLattice</c>); otherwise <see cref="LatticeGrid"/> of the terrain size.
    /// <para>P7 — THE OBSERVER: <paramref name="knowledge"/> is what the player knows (null = <see cref="PlayerKnowledge.Of(IReadOnlyWorldState, PolityId)"/>
    /// for <paramref name="player"/>, the seam's production view: all-known until S2a, so the projection is byte-identical).
    /// The projection holds only what the observer knows, so every layer the lens paints from it — and every hit test that
    /// reads the frame it paints — is filtered at once: a settlement or a formation the observer does not know is not in
    /// it; a territory block, a dirt-path step (both ends) only on known nodes; a built route only between two known
    /// settlements. Nothing of a hidden settlement is read (its rows are never visited), and the honest empty states
    /// count only what is shown.</para></summary>
    public static WorldProjection Build(IReadOnlyWorldState world, SimConfig cfg, Func<int, string> name, PolityId player,
        Func<IReadOnlyWorldState, SimConfig, SettlementId, IReadOnlyList<InstitutionView>>? institutions = null,
        (int LatticeSize, int Stride)? grid = null, PlayerKnowledge? knowledge = null)
    {
        institutions ??= InstitutionMarkerSource.InstitutionsAt;
        PlayerKnowledge known = knowledge ?? PlayerKnowledge.Of(world, player);
        if (known.Player.Value != player.Value) throw new ArgumentException("the observer's knowledge must be the player's", nameof(knowledge));
        int size = world.Terrain?.Size ?? 256;
        (int lsize, int stride) = world.Terrain is null ? (1, 1) : grid ?? LatticeGrid(size);

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
            if (known.IsHiddenSettlement(e.A.Value) || known.IsHiddenSettlement(e.B.Value)) continue;   // P7: both ends known
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
            if (!known.Known(anchor[e.A.Value]) || !known.Known(anchor[e.B.Value])) continue;   // P7: both ends on known nodes
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
            if (!known.Known(row.LatticeNode) || known.IsHiddenSettlement(row.Settlement.Value)) continue;   // P7
            int ctl = EmpireQuery.TryGetController(world, row.Settlement, out PolityId p) ? p.Value : -1;
            (double cx, double cy) = Center(row.LatticeNode, lsize, stride);
            territory.Add(new TerritoryBlock(cx - stride / 2.0, cy - stride / 2.0, stride, ctl));
        }

        AgeContent? ages = cfg.Ages;
        var settlements = new List<SettlementLensView>();
        bool anyInstitution = false;
        bool anyStructure = false;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementRow row = world.Settlements[s];
            SettlementId id = row.Id;
            if (!known.KnowsSettlement(id.Value)) continue;   // P7: a settlement the observer does not know is not drawn, nor read
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
                // P12a: one view per project KIND (rows of one kind summed), ascending project id — whatever the table's
                // order, the settlement lists, keys and composes its structures the same way.
                int at = -1;
                for (int v = 0; v < structures.Count; v++) if (structures[v].ProjectId == st.ProjectId) { at = v; break; }
                if (at >= 0) { structures[at] = structures[at] with { Count = structures[at].Count + st.Count }; continue; }
                string pname = ProjectName(cfg, st.ProjectId);
                structures.Add(new StructureView(st.ProjectId, pname, KindOf(pname), st.Count));
            }
            structures.Sort(static (a, b) => a.ProjectId.CompareTo(b.ProjectId));   // project ids are unique here
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
                if (!known.Known(anchor[e.A.Value]) || !known.Known(anchor[e.B.Value])) continue;   // P7: the steps the map draws
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
            if (!known.KnowsUnit(u)) continue;   // P7: D-039 C1 — a formation not observed is UNKNOWN, never drawn
            UnitIdentity? ident = fam?.IdentityByKey(u.Identity);
            units.Add(new UnitLensView(u.Id, u.Owner.Value, u.X, u.Y, u.Family,
                fam?.FamilyByKey(u.Family)?.Name ?? "Formation", ident?.Name ?? "Formation", u.Location.Value)
            {
                Emblem = UnitEmblem.Of(fam, u.Family, u.Identity),   // P11: keyed by the CURRENT identity
            });
        }

        int playerAge = ages is null ? 0 : AgeQuery.CurrentAge(world, ages, player);
        var absent = new List<string>
        {
            "Army movement and supply arrive with the Battle Layer (M7) - formations are shown where they are stationed",
            // P11: a formation holds no people (MilitaryUnitRow has no headcount), so no token carries a count.
            FormationCountAbsent,
        };
        if (!anyInstitution) absent.Add("Institutions: none founded");
        // P7: the rows of a hidden settlement are not counted (all-known: exactly world.Structures.Count == 0).
        for (int k = 0; k < world.Structures.Count && !anyStructure; k++)
            if (!known.IsHiddenSettlement(world.Structures[k].Settlement.Value)) anyStructure = true;
        if (!anyStructure) absent.Add("Structures: none built yet");
        if (routes.Count == 0) absent.Add("Roads: none developed - the free dirt-path baseline only");

        return new WorldProjection
        {
            WorldSize = size, KmPerPx = world.Terrain?.KmPerPx ?? double.NaN, Settlements = settlements, Units = units, Roads = roads, Routes = routes, Territory = territory,
            PlayerPolity = player.Value, PlayerAge = playerAge,
            PlayerAgeName = ages is null || playerAge == 0 ? "" : ages.Age(playerAge).Name,
            Absent = absent, Universe = PartUniverse.Of(cfg),
        };
    }

    /// <summary>P11: the honest empty state for the formation count (ledger §24's "internal composition may appear as
    /// labels/counts" has no composition to show: <c>MilitaryUnitRow</c> holds no headcount, manpower or strength).</summary>
    public const string FormationCountAbsent =
        "Formation counts: not simulated - a formation holds no people, so no token shows a count";

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

/// <summary>
/// P3 (M5 map workstream): WHERE THE LENS DREW ONE SETTLEMENT, in screen px — the centre of its mark
/// (<paramref name="X"/>, <paramref name="Y"/>), its FOOTPRINT (the box of the mark as painted: at World zoom the halo
/// round the population mark, closer the built footprint's circle — its geometry, not its stroke, so the era's hand
/// never moves it) and its NAME PLATE (the paper plate under the name, which holds the name's run). Settlement picking
/// hit-tests exactly these (<see cref="ViewModel.SettlementSelection"/>), as formation picking hit-tests
/// <see cref="UnitPlacement"/>: the click and the mark agree at every level of detail.
/// </summary>
public readonly record struct SettlementHit(int Id, double X, double Y, RectD Footprint, RectD NamePlate);

/// <summary>
/// P12a (M5 map workstream): WHERE THE LENS DREW ONE PART of a settlement's composition — a structure kind's or an
/// institution type's glyph (<paramref name="Key"/>, the <see cref="InstitutionMarker"/> key) on its lot
/// (<paramref name="Ring"/>, <paramref name="Lot"/> of <see cref="SettlementComposition"/>), and the box its commands
/// cover on screen (<paramref name="Rect"/>: their bounds with stroke, without the anti-aliasing fringe). Every part lies
/// inside its settlement's footprint (<see cref="SettlementHit.Footprint"/>), so the settlement's click target holds it.
/// </summary>
public readonly record struct PartPlacement(int Settlement, string Key, int Ring, int Lot, RectD Rect);

/// <summary>Where the lens lays its legend out: the legend's box is right-aligned on <paramref name="Right"/>, its foot on
/// <paramref name="Bottom"/> (screen px), and it is never wider than <paramref name="MaxWidth"/>.</summary>
public readonly record struct LegendAnchor(double Right, double Bottom, double MaxWidth);

/// <summary>
/// P3: THE MAP LEGEND as one paint drew it — its box, its title, the information layers it lists (exactly the frame's
/// <see cref="LensFrame.Painted"/>, in paint order), each layer's entry (<see cref="WorldLens.LayerLabel"/>) and the
/// projection's honest empty states (<see cref="WorldProjection.Absent"/>, verbatim). Every command it emitted is
/// charged to <see cref="MapLayer.Legend"/>.
/// </summary>
public sealed record MapLegend(RectD Rect, string Title, IReadOnlyList<WorldLayer> Layers, IReadOnlyList<string> Entries,
    IReadOnlyList<string> Absent);

/// <summary>P9: where the lens lays the overlay's key out: the key's box is left-aligned on <paramref name="Left"/>, its foot
/// on <paramref name="Bottom"/> (screen px), and it is never wider than <paramref name="MaxWidth"/>. P9b: its box never
/// reaches above <paramref name="Top"/> (screen px; −∞: no cap) — when its rows do not fit beneath it, the box stops there
/// and shows a window of WHOLE rows starting at row <paramref name="Scroll"/> (clamped to the rows there are), with a
/// scroll bar; every row stays reachable, none is dropped.</summary>
public readonly record struct OverlayKeyAnchor(double Left, double Bottom, double MaxWidth, double Top = double.NegativeInfinity,
    int Scroll = 0);

/// <summary>P9: what the lens is asked to paint of the resource overlay: the bake, which of its layers the player shows
/// (parallel to <see cref="ResourceOverlay.Layers"/>), the opacity (0..1), the turn's status, where the key goes (null:
/// no key) and how a settlement is named. UI state only — none of it reaches the simulation.</summary>
public sealed record OverlayPaint(ResourceOverlay Bake, IReadOnlyList<bool> Shown, double Opacity, OverlayStatus? Status = null,
    OverlayKeyAnchor? Key = null, Func<int, string>? Names = null)
{
    /// <summary>P10: a 'now usable' shortcut's highlight — the layers whose qualifying places are outlined
    /// (<see cref="ResourceOverlay.Outline"/>; only a SHOWN layer is) and the rows the key opens with. Null: none.</summary>
    public OverlayHighlight? Highlight { get; init; }

    /// <summary>Whether any layer is shown.</summary>
    public bool Any
    {
        get
        {
            for (int i = 0; i < Shown.Count; i++) if (Shown[i]) return true;
            return false;
        }
    }
}

/// <summary>P9: THE OVERLAY'S KEY as one paint drew it — its box, its title and its rows (unwrapped, in order: ALL of them,
/// drawn or scrolled out of view); every command of it is charged to <see cref="MapLayer.ResourceOverlay"/>.</summary>
public sealed record OverlayKey(RectD Rect, string Title, IReadOnlyList<string> Rows)
{
    /// <summary>P9b: the first row drawn (0: the top) — the anchor's <see cref="OverlayKeyAnchor.Scroll"/>, clamped.</summary>
    public int Scroll { get; init; }

    /// <summary>P9b: the largest <see cref="Scroll"/> there is (0: every row fits the box, nothing scrolls) — at it the last
    /// row is drawn.</summary>
    public int MaxScroll { get; init; }

    /// <summary>P9b: how many rows, from <see cref="Scroll"/> on, this paint drew (whole rows only: a row is drawn with all its
    /// lines and its ramp, or not at all).</summary>
    public int RowsDrawn { get; init; }

    /// <summary>P9b: the rows this paint drew, in order.</summary>
    public IReadOnlyList<string> DrawnRows
    {
        get
        {
            var r = new List<string>();
            for (int i = Scroll; i < Scroll + RowsDrawn && i < Rows.Count; i++) r.Add(Rows[i]);
            return r;
        }
    }
}

/// <summary>P9: what one paint drew of the resource overlay: the layers that drew a rect in view (indices, in order), per
/// layer the rects emitted and the lattice nodes they cover, the key (null when none was asked for) and the realized-roll
/// markers (settlement, text).</summary>
public sealed record OverlayFrame(IReadOnlyList<int> Layers, IReadOnlyList<int> Rects, IReadOnlyList<int> Nodes, OverlayKey? Key,
    IReadOnlyList<(int Settlement, string Text)> Markers)
{
    /// <summary>P10: the layers whose outline this paint drew (shown layers of the highlight, in its order).</summary>
    public IReadOnlyList<int> OutlinedLayers { get; init; } = [];

    /// <summary>P10: the outline segments this paint drew in view, and the qualifying nodes the outlines enclose.</summary>
    public int OutlineSegments { get; init; }
    public int OutlineNodes { get; init; }
}

/// <summary>P10: what a 'now usable' shortcut asks the lens to highlight: the overlay layers (indices into
/// <see cref="ResourceOverlay.Layers"/>) whose qualifying places are outlined, and the rows the overlay's key opens with
/// (the shortcut's headline and its permission / knowledge lines). UI state only.</summary>
public sealed record OverlayHighlight(IReadOnlyList<int> Layers, IReadOnlyList<string> Rows);

/// <summary>What one lens paint drew — the tests read this instead of pixels.
/// <c>LayerDraws[(int)MapLayer]</c> counts the draw commands emitted per map layer (draw-call accounting
/// for the single-owner rule); <c>Institutions</c> lists every institution marker drawn; <c>Paths</c>
/// every network piece drawn (stroke ownership on the Paths layer); <c>UnitPlacements</c> where each
/// formation token went.</summary>
public sealed record LensFrame(WorldZoom Zoom, IReadOnlyList<WorldLayer> Layers, IReadOnlyList<int> UnitTokens,
    IReadOnlyList<int> SettlementsDrawn, IReadOnlyList<int> LayerDraws, IReadOnlyList<InstitutionMarker> Institutions,
    IReadOnlyList<PathPiece> Paths, IReadOnlyList<UnitPlacement> UnitPlacements)
{
    /// <summary>P3: where each drawn settlement's mark and name went (one per <see cref="SettlementsDrawn"/> id, in the
    /// same order) — the settlement hit geometry.</summary>
    public IReadOnlyList<SettlementHit> SettlementHits { get; init; } = [];

    /// <summary>P3: the draw-call accounting one level finer — <c>InfoDraws[(int)WorldLayer]</c> counts the commands
    /// emitted for each information layer. Every command charged to a lens map layer other than
    /// <see cref="MapLayer.Legend"/> is charged to exactly one information layer of that map layer
    /// (<see cref="MapLayerOwnership.MapLayerOf"/>), so for each such map layer the two counts agree.</summary>
    public IReadOnlyList<int> InfoDraws { get; init; } = [];

    /// <summary>P3: the information layers this paint actually DREW — those of <see cref="Layers"/>, in paint order, at
    /// least one of whose commands can touch the viewport (<see cref="DrawListCull.Bounds"/>). A layer that is on at
    /// this zoom but drew nothing there (territory toggled off, no formation, nothing in view) is not in it.</summary>
    public IReadOnlyList<WorldLayer> Painted { get; init; } = [];

    /// <summary>P3: the legend this paint drew, or null when none was asked for.</summary>
    public MapLegend? Legend { get; init; }

    /// <summary>P2: the level of detail this paint drew at (<see cref="Zoom"/> is its derived band). A banded paint carries
    /// its band's plateau (<see cref="MapDetail.Of"/>).</summary>
    public MapDetail Detail { get; init; } = MapDetail.Of(WorldZoom.World);

    /// <summary>P9: what this paint drew of the resource overlay, or null when no overlay layer was shown.</summary>
    public OverlayFrame? Overlay { get; init; }

    /// <summary>P12a: where each drawn part of each drawn settlement's composition went (one per <see cref="Institutions"/>
    /// marker, in the same order) — the parts' hit geometry.</summary>
    public IReadOnlyList<PartPlacement> Parts { get; init; } = [];
}

/// <summary>
/// THE WORLD LENS — zoom-dependent drawing of the projection over the map (D-047 Part 5).
/// Pure: given the projection, the camera (world px → screen px) and the viewport, it paints into a
/// <see cref="DrawList"/>. Morphology is AGGREGATED (footprint size from population on the compressive class scale,
/// dwelling blocks from housing and one glyph per structure kind and per institution type with a count, each on its
/// fixed lot of the settlement's composition — P12a, <see cref="SettlementComposer"/>), never per building, and it is a
/// READ OF STATE ONLY — no physical feature is gated by the Age or any
/// date (D-038 H5); the Age shows as the banner. Every colour comes from <see cref="MapInk"/>, and the era's
/// HAND — glyph stroke weight, the token plate's cut, the banner's fly, the name plate's ornament — from its
/// <see cref="MapInk.Hand"/> (packet P13): the painters switch on those tokens, never on an era or Age number.
/// </summary>
public static class WorldLens
{
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

    // ---------------------------------------------------------------- the map's hand (MapInk.Hand; packet P13)
    // Every painter below takes its hand from Stroke, Emblem, HandLine, PlateRadius and Fly and the name plate's
    // ornament tokens — the hand's tokens, never an era number. They change a mark's stroke
    // and outline only: no mark is added, removed or moved, so the lens's geometry and hit regions are the same
    // in every era, and the parchment hand (the default record's) reproduces today's commands exactly.

    /// <summary>A glyph frame or outline of base width <paramref name="basePx"/> in the era's weight (floored).</summary>
    private static double Stroke(MapInk k, double basePx) => Math.Max(MapHandTokens.MinStrokePx, basePx * k.Hand.StrokeScale);

    /// <summary>An emblem mark of base width <paramref name="basePx"/> in the era's weight (floored).</summary>
    private static double Emblem(MapInk k, double basePx) => Math.Max(MapHandTokens.MinStrokePx, basePx * k.Hand.EmblemScale);

    private const int UnitEmblemSalt = 0x4D31, HallEmblemSalt = 0x4D32, BannerPoleSalt = 0x4D33, UnitFormSalt = 0x4D34;

    /// <summary>A stroke in the era's hand: its ends displaced by up to <see cref="MapHandTokens.WobblePx"/>,
    /// seeded (<see cref="FrameNoise"/>) from the glyph's STABLE key — the unit family, the institution type, the
    /// settlement id — and the stroke's index, never from time, the frame or the screen position, so a glyph keeps
    /// its hand as the camera moves. A ruled hand (wobble 0) draws the line exactly.</summary>
    private static void HandLine(DrawList d, MapInk k, double x0, double y0, double x1, double y1, Rgba c, double w,
        int key, int salt, int stroke)
    {
        double a = k.Hand.WobblePx;
        if (a > 0)
        {
            int i = stroke * 4;
            x0 += a * FrameNoise.S(key, salt, i);
            y0 += a * FrameNoise.S(key, salt, i + 1);
            x1 += a * FrameNoise.S(key, salt, i + 2);
            y1 += a * FrameNoise.S(key, salt, i + 3);
        }
        d.Line(x0, y0, x1, y1, c, w);
    }

    /// <summary>The corner radius of a formation token's plate (half-size <paramref name="s"/>) in the era's cut. The
    /// plate's box is 2s × 1.5s in every era (the hit rect); a radius of 0.75s rounds its ends fully.</summary>
    public static double PlateRadius(PlateShape plate, double s) => plate switch
    {
        PlateShape.Pebble => s * 0.75,
        PlateShape.Tablet => s * 0.4,
        PlateShape.Square => 0.0,
        _ => 2.0,   // Plaque: today's 2-px plate
    };

    /// <summary>The number of information layers (<see cref="WorldLayer"/>): the length of <see cref="LensFrame.InfoDraws"/>.</summary>
    public static readonly int InfoLayerCount = Enum.GetValues<WorldLayer>().Length;

    /// <summary>Paints the lens at the PLATEAU of <paramref name="zoom"/> (<see cref="MapDetail.Of"/>) — the banded lens,
    /// command for command. <paramref name="toScreen"/> maps world px to screen px; <paramref name="scale"/>
    /// is screen px per world px; <paramref name="ink"/> is the map's ink and hand (the era seam, see
    /// <see cref="MapInk"/>; <see cref="MapInk.Default"/> when omitted). <paramref name="selectedUnit"/>'s token is
    /// framed in the selection ink after every token is drawn (it was framed by GameUi after the paint; P3 moved it here
    /// so the legend stays the last, topmost thing the lens draws). With <paramref name="legend"/> (P3) the map legend
    /// is painted last, over the map, at that anchor (<see cref="LensFrame.Legend"/>); without it, nothing of the
    /// legend is drawn.</summary>
    public static LensFrame Paint(DrawList d, ITextMeasure m, WorldProjection p, WorldZoom zoom,
        Func<double, double, (double X, double Y)> toScreen, double scale, RectD viewport, int selected = -1,
        bool showTerritory = true, MapInk? ink = null, EraTheme? theme = null, double uiScale = 1.0,
        LegendAnchor? legend = null, int selectedUnit = -1, OverlayPaint? overlay = null) =>
        Paint(d, m, p, MapDetail.Of(zoom), toScreen, scale, viewport, selected, showTerritory, ink, theme, uiScale, legend, selectedUnit, overlay);

    /// <summary>
    /// P2: paints the lens at a CONTINUOUS level of detail (<see cref="MapDetail"/> — what the game paints every frame).
    /// Each information layer is drawn at its opacity (<see cref="MapDetail.Alpha"/>; a layer at 0 is not drawn at all),
    /// and what a layer shares with the next band — the name, the banner, the formation tokens, the footprint across
    /// the Regional ↔ Settlement window, the glyph ring, the sector ring, the roads' widths — is drawn ONCE, at full
    /// opacity, with sizes and offsets blended continuously between the two bands' values (<see cref="MapDetail.Mix"/>).
    /// What only one side has fades: the World mark and the World network out, the footprint, the roads' casing look,
    /// the glyphs and the formation labels in across the World ↔ Regional window; territory and the glyph ring's
    /// regional size out, the key, the population and density lines, the route names in across the Regional ↔
    /// Settlement window. On a plateau every opacity is 0 or 1 and every blend an endpoint, so the commands are exactly
    /// the banded lens's (the <see cref="WorldZoom"/> overload).
    /// Persisting elements are charged to the layer of the derived band (<see cref="MapDetail.Band"/>).
    /// </summary>
    public static LensFrame Paint(DrawList d, ITextMeasure m, WorldProjection p, MapDetail detail,
        Func<double, double, (double X, double Y)> toScreen, double scale, RectD viewport, int selected = -1,
        bool showTerritory = true, MapInk? ink = null, EraTheme? theme = null, double uiScale = 1.0,
        LegendAnchor? legend = null, int selectedUnit = -1, OverlayPaint? overlay = null)
    {
        MapInk k = ink ?? MapInk.Default;
        _type = new MapType(theme, uiScale);
        WorldZoom zoom = detail.Band;
        double R = detail.Regional, S = detail.Settlement;
        IReadOnlyList<WorldLayer> layers = detail.Layers();
        var draws = new int[MapLayerOwnership.All.Length];
        var info = new int[InfoLayerCount];
        var visible = new bool[InfoLayerCount];
        var institutions = new List<InstitutionMarker>();
        var pieces = new List<PathPiece>();
        var placements = new List<UnitPlacement>();
        var hits = new List<SettlementHit>();
        var parts = new List<PartPlacement>();
        int mark = d.Commands.Count;
        // Draw-call accounting: every command emitted since the last mark is charged to one map layer and (P3) to one
        // information layer of it (the legend to none). An information layer is DRAWN once one of its commands can
        // touch the viewport; a layer's commands are bounded only until the first such one is found.
        void Charge(MapLayer l, WorldLayer? w)
        {
            int count = d.Commands.Count;
            draws[(int)l] += count - mark;
            if (w is WorldLayer wl)
            {
                info[(int)wl] += count - mark;
                for (int i = mark; i < count && !visible[(int)wl]; i++)
                    if (DrawListCull.Bounds(d.Commands[i]) is RectD b && DrawListCull.Touches(b, viewport)) visible[(int)wl] = true;
            }
            mark = count;
        }
        bool On(WorldLayer l) => detail.Alpha(l) > 0;
        var unitTokens = new List<int>();
        var drawn = new List<int>();

        // P9: the resource overlay's layers, under everything else the lens draws (the terrain shows through them).
        OverlayPaint? ov = overlay is { Any: true } ? overlay : null;
        int[] oRects = new int[ov?.Bake.Layers.Count ?? 0], oNodes = new int[oRects.Length];
        ov?.Bake.PaintLayers(d, ov.Shown, ov.Opacity, toScreen, viewport, k, oRects, oNodes);
        // P10: a 'now usable' shortcut's outline of the qualifying places on its shown layers, over their fills.
        var outlined = new List<int>();
        int outlineSegments = 0, outlineNodes = 0;
        if (ov?.Highlight is { } hl)
            foreach (int li in hl.Layers)
            {
                if ((uint)li >= (uint)ov.Bake.Layers.Count || li >= ov.Shown.Count || !ov.Shown[li] || outlined.Contains(li)) continue;
                outlineSegments += ov.Bake.PaintOutline(d, li, toScreen, viewport, A(k.Ink, 0.9), 2.0);
                outlineNodes += ov.Bake.Outline(li).Nodes;
                outlined.Add(li);
            }
        Charge(MapLayer.ResourceOverlay, null);

        if (On(WorldLayer.Territories) && showTerritory)
        {
            // P2: the tint thins continuously from the World band's to the Regional band's, and fades out toward Settlement.
            double alpha = MapDetail.Mix(0.42, 0.24, R) * detail.Alpha(WorldLayer.Territories);
            foreach (TerritoryBlock t in p.Territory)
            {
                (double x, double y) = toScreen(t.X, t.Y);
                double s = t.Size * scale;
                if (x > viewport.Right || y > viewport.Bottom || x + s < viewport.X || y + s < viewport.Y) continue;
                d.Rect(new RectD(x, y, s + 0.6, s + 0.6), A(k.PolityOf(t.Polity, p.PlayerPolity), alpha));
            }
        }
        Charge(MapLayer.Territory, WorldLayer.Territories);

        // The network: the dirt-path baseline (dashed at World zoom, cased below) and the built routes
        // by class on top — the World zoom's major infrastructure, the closer zooms' roads. P2: across the World ↔
        // Regional window the two looks cross-fade (the World network fading out under the cased one fading in).
        if (On(WorldLayer.MajorInfrastructure))
        {
            int from = d.Commands.Count;
            RoadLens.PaintNetwork(d, m, p, WorldZoom.World, 0, toScreen, viewport, k, pieces);
            d.FadeFrom(from, detail.Alpha(WorldLayer.MajorInfrastructure));
            Charge(MapLayer.Paths, WorldLayer.MajorInfrastructure);
        }
        if (On(WorldLayer.Roads))
        {
            int from = d.Commands.Count;
            RoadLens.PaintNetwork(d, m, p, S >= 1 ? WorldZoom.Settlement : WorldZoom.Regional, S, toScreen, viewport, k, pieces);
            d.FadeFrom(from, detail.Alpha(WorldLayer.Roads));
            Charge(MapLayer.Paths, WorldLayer.Roads);
        }

        // The information layer of the settlement marks: the sized mark at World zoom, the built footprint closer.
        WorldLayer marks = zoom == WorldZoom.World ? WorldLayer.MajorSettlements : WorldLayer.SettlementMorphology;
        double aMark = detail.Alpha(WorldLayer.MajorSettlements), aMorph = detail.Alpha(WorldLayer.SettlementMorphology);
        bool banners = On(WorldLayer.AgeBanners);
        foreach (SettlementLensView s in p.Settlements)
        {
            (double sx, double sy) = toScreen(s.X, s.Y);
            double rMark = MarkRadiusPx(p, s), unitPx = p.Classes.UnitPx(scale, S), rFoot = p.Classes.FootprintRadiusUnits(s.Population) * unitPx;
            // The name: ONE run, at a place and size blended between the bands' (never two names cross-fading) — under the
            // World mark, or under the footprint (and its sector ring) closer.
            double nameSize = MapDetail.Mix(_type.Size(TypeRole.Body, FontRole.Heading), _type.Size(TypeRole.Heading, FontRole.Heading), S);
            double worldNameY = sy + rMark + 5;
            double morphNameY = aMorph > 0 ? MorphNameY(sy, rFoot, detail.Alpha(WorldLayer.ProductionSignals) > 0 && s.Sectors is not null) : sy;
            double ly = MapDetail.Mix(worldNameY, morphNameY, R);
            bool banner = banners && s.Age > 0 && s.Controller >= 0;
            double bannerY = sy - MapDetail.Mix(18, rFoot + 14, R);
            // P12a review fix: the cull follows what this settlement paints (Reaches), never a fixed margin about its centre.
            if (!Reaches(m, p, s, sx, sy, rMark, rFoot, unitPx, nameSize, ly, banner, bannerY, s.Id == selected, detail, k, viewport)) continue;
            drawn.Add(s.Id);
            Rgba pol = k.PolityOf(s.Controller, p.PlayerPolity);
            double reach = 0;   // P3: the footprint's radius as painted — the halo at World zoom, the built footprint closer
            if (aMark > 0)
            {
                int from = d.Commands.Count;
                d.Circle(sx, sy, rMark + 2, A(k.SettlementHalo, 0.9), k.Ink, Stroke(k, 1.2));
                d.Circle(sx, sy, rMark, pol, null);
                if (s.IsCapital) Star(d, sx, sy, rMark * 0.75, k.CapitalMark);
                d.FadeFrom(from, aMark);
                Charge(MapLayer.SettlementMarkers, WorldLayer.MajorSettlements);
                reach = rMark + 2;
            }
            if (aMorph > 0)
            {
                PaintMorphology(d, m, p, s, sx, sy, rFoot, unitPx, S, detail, Charge, institutions, parts, k);
                reach = Math.Max(reach, rFoot);
            }
            RectD plate = Name(d, m, sx, ly, s.Name, nameSize, k);
            Charge(MapLayer.SettlementMarkers, marks);
            if (aMorph > 0) PaintSettlementLines(d, m, s, sx, ly + nameSize * 1.35, S, detail, Charge, k);
            if (s.Id == selected) d.Circle(sx, sy, MapDetail.Mix(20, rFoot + 8, R), null, k.Selection, 2.4);
            Charge(MapLayer.SettlementMarkers, marks);
            hits.Add(new SettlementHit(s.Id, sx, sy, new RectD(sx - reach, sy - reach, 2 * reach, 2 * reach), plate));

            if (banner) Banner(d, m, sx, bannerY, s.Age, pol, s.IsCapital, k, s.Id);
            Charge(MapLayer.AgeBanners, WorldLayer.AgeBanners);
        }

        if (On(WorldLayer.MilitaryFormations))
        {
            double tok = MapDetail.Mix(9, 12, R);
            SettlementLensView?[] stations = Stations(p);
            int[] slots = FanSlots(p);
            for (int i = 0; i < p.Units.Count; i++)
            {
                UnitLensView u = p.Units[i];
                // A formation stationed in a settlement is anchored to it, just outside the mark's east
                // edge at mid-height (clear of the name below and the banner above); one in the field sits
                // at its own position. Formations sharing an anchor FAN OUT in ascending unit id (stable,
                // never overlapping): a row eastward at World zoom, a labelled column below that (P2: the row
                // folds into the column across the World ↔ Regional window).
                SettlementLensView? station = stations[i];
                double wx = u.X + 0.5, wy = u.Y + 0.5, edge = 0;
                if (station is not null)
                {
                    wx = station.X; wy = station.Y;
                    edge = MapDetail.Mix(MarkRadiusPx(p, station) + 2, FootprintRadiusPx(p, station, scale, S), R) + tok + 3;
                }
                (double ux, double uy) = toScreen(wx, wy);
                (double fx, double fy) = FanOffset(R, slots[i], tok);
                ux += edge + fx;
                uy += fy;
                UnitToken(d, ux, uy, tok, u.Emblem ?? UnitEmblem.Unkeyed(u.FamilyKey), k.PolityOf(u.Owner, p.PlayerPolity), k);
                if (R > 0)
                {
                    int from = d.Commands.Count;
                    _type.Label(d, ux + tok + 5, uy - _type.Size(TypeRole.Caption, FontRole.Caps) * 0.62, u.IdentityName, TypeRole.Caption, FontRole.Caps, k.Ink, TextAlign.Left);
                    d.FadeFrom(from, R);
                }
                unitTokens.Add(u.Id);
                placements.Add(new UnitPlacement(u.Id, ux, uy, slots[i]) { Size = tok });
            }
            // The selected formation's frame, over every token (UnitSelection's hit rect, 4 px out).
            foreach (UnitPlacement up in placements)
                if (up.Id == selectedUnit)
                {
                    double ts = tok + 4;
                    d.Rect(new RectD(up.X - ts, up.Y - ts * 0.8, 2 * ts, 1.6 * ts), null, k.Selection, 2.4, 3);
                }
        }
        Charge(MapLayer.UnitTokens, WorldLayer.MilitaryFormations);

        // P9: the realized ore rolls beside the player's settlements (west of the footprint: the name is below, the banner
        // above, the formations east) — closer than the World band only, fading in with the Regional blend like the
        // formation labels (at World zoom the settlements stand too close for a line each) — then the overlay's key, both
        // before the legend, which stays the last thing drawn.
        OverlayFrame? overlayFrame = null;
        if (ov is not null)
        {
            var markers = new List<(int, string)>();
            if (ov.Status is { } status && R > 0)
                foreach (SettlementLensView s in p.Settlements)
                {
                    string text = ov.Bake.MarkerText(ov.Shown, status, s.Id);
                    if (text.Length == 0) continue;
                    (double sx, double sy) = toScreen(s.X, s.Y);
                    if (sx < viewport.X - 50 || sx > viewport.Right + 600 || sy < viewport.Y - 50 || sy > viewport.Bottom + 50) continue;
                    int from = d.Commands.Count;
                    double size = _type.Size(TypeRole.Caption, FontRole.Body);
                    double w = _type.Width(m, text, TypeRole.Caption, FontRole.Body);
                    double right = sx - FootprintRadiusPx(p, s, scale, S) - 8;
                    d.Rect(new RectD(right - w - 4, sy - size * 0.75, w + 8, size * 1.4), A(k.NamePlate, 0.85), A(k.Ink, 0.45), 1, 2);
                    _type.Label(d, right, sy - size * 0.65, text, TypeRole.Caption, FontRole.Body, k.Ink, TextAlign.Right);
                    d.FadeFrom(from, R);
                    markers.Add((s.Id, text));
                }
            Charge(MapLayer.ResourceOverlay, null);
            OverlayKey? okey = null;
            if (ov.Key is OverlayKeyAnchor kat)
            {
                okey = PaintOverlayKey(d, m, ov, kat, k);
                Charge(MapLayer.ResourceOverlay, null);
            }
            var shownLayers = new List<int>();
            for (int i = 0; i < oRects.Length; i++) if (oRects[i] > 0) shownLayers.Add(i);
            overlayFrame = new OverlayFrame(shownLayers, oRects, oNodes, okey, markers)
            {
                OutlinedLayers = outlined, OutlineSegments = outlineSegments, OutlineNodes = outlineNodes,
            };
        }

        var painted = new List<WorldLayer>();
        foreach (WorldLayer l in layers) if (visible[(int)l]) painted.Add(l);
        MapLegend? key = null;
        if (legend is LegendAnchor at)
        {
            key = PaintLegend(d, m, p, zoom, painted, at, k);
            Charge(MapLayer.Legend, null);
        }
        return new LensFrame(zoom, layers, unitTokens, drawn, draws, institutions, pieces, placements)
        {
            SettlementHits = hits, InfoDraws = info, Painted = painted, Legend = key, Detail = detail, Overlay = overlayFrame,
            Parts = parts,
        };
    }

    // ---------------------------------------------------------------- the legend (P3)

    /// <summary>What a legend entry says of an information layer: what its marks ARE, read from what the painters
    /// draw (never a claim the lens does not paint).</summary>
    public static string LayerLabel(WorldLayer layer) => layer switch
    {
        WorldLayer.Territories => "Territory: catchment claims, by holder",
        WorldLayer.MajorSettlements => "Settlements: mark sized by population",
        WorldLayer.MajorInfrastructure => "Paths (dashed) and built roads by class",
        WorldLayer.Roads => "Paths and built roads by class",
        WorldLayer.SettlementMorphology => "Footprint: size class from population, a block per "
            + SettlementClasses.Default.Lattice.DwellingsPerBlock.ToString(CultureInfo.InvariantCulture) + " dwellings",
        WorldLayer.InstitutionalPresence => "Structures and institutions: a glyph per kind",
        WorldLayer.InstitutionTypes => "Structures and institutions, named with counts",
        WorldLayer.InfrastructureDensity => "Path links in the catchment, size tier",
        WorldLayer.PopulationScale => "People and dwellings",
        WorldLayer.ProductionSignals => "Labour split: a ring of sector arcs",
        WorldLayer.MilitaryFormations => "Formations: a token each - family emblem, a pip per form in its line",
        WorldLayer.AgeBanners => "Age banners: the holder's Age",
        _ => layer.ToString(),
    };

    /// <summary>The legend's title at <paramref name="zoom"/>.</summary>
    public static string LegendTitle(WorldZoom zoom) => "Legend - " + zoom switch
    {
        WorldZoom.World => "world view",
        WorldZoom.Regional => "regional view",
        _ => "settlement view",
    };

    /// <summary>The heading over the honest empty states (<see cref="WorldProjection.Absent"/>) in the legend.</summary>
    public const string LegendAbsentHeading = "Not on the map";

    /// <summary>
    /// P3: THE MAP LEGEND — a plain DATA legend (no art): a paper plate in the era's hand (the name plate's ink, rule and
    /// corner), its title, one entry per information layer this paint DREW (<paramref name="painted"/>), then the
    /// projection's absent lines verbatim. Text is wrapped to the anchor's width, word by word; every run is a
    /// <see cref="TextCmd"/> (the headless FrameText and LastLensCommands read it); nothing here is an ImGui window.
    /// </summary>
    private static MapLegend PaintLegend(DrawList d, ITextMeasure m, WorldProjection p, WorldZoom zoom,
        IReadOnlyList<WorldLayer> painted, LegendAnchor at, MapInk k)
    {
        double pad = _type.Px(8), maxText = Math.Max(_type.Px(40), at.MaxWidth - 2 * pad);
        string title = LegendTitle(zoom);
        var entries = new List<string>();
        foreach (WorldLayer l in painted) entries.Add(LayerLabel(l));
        // The runs, in order: (text, role, font, gap before).
        var runs = new List<(string Text, TypeRole Role, FontRole Font, double Gap)>
        {
            (title, TypeRole.Caption, FontRole.Caps, 0),
        };
        void Add(string text, TypeRole role, FontRole font, double gap)
        {
            bool first = true;
            foreach (string line in _type.Wrap(m, text, role, font, maxText)) { runs.Add((line, role, font, first ? gap : 0)); first = false; }
        }
        double space = _type.Px(4);
        foreach (string e in entries) Add(e, TypeRole.Secondary, FontRole.Body, 0);
        if (p.Absent.Count > 0)
        {
            Add(LegendAbsentHeading, TypeRole.Caption, FontRole.Caps, space);
            foreach (string a in p.Absent) Add(a, TypeRole.Secondary, FontRole.Body, 0);
        }

        double w = 0, h = 0;
        foreach ((string text, TypeRole role, FontRole font, double gap) in runs)
        {
            w = Math.Max(w, _type.Width(m, text, role, font));
            h += gap + _type.Size(role, font) * 1.3;
        }
        double boxW = Math.Min(at.MaxWidth, w + 2 * pad), boxH = h + 2 * pad;
        var box = new RectD(at.Right - boxW, at.Bottom - boxH, boxW, boxH);
        MapHandTokens hand = k.Hand;
        d.Rect(box, A(k.NamePlate, 0.92), A(k.Ink, 0.55), Math.Max(1.0, hand.NamePlateRulePx), hand.NamePlateCornerPx);
        double y = box.Y + pad;
        foreach ((string text, TypeRole role, FontRole font, double gap) in runs)
        {
            y += gap;
            _type.Label(d, box.X + pad, y, text, role, font, k.Ink, TextAlign.Left);
            y += _type.Size(role, font) * 1.3;
        }
        return new MapLegend(box, title, [.. painted], entries, [.. p.Absent]);
    }

    /// <summary>
    /// P9: THE OVERLAY'S KEY — a plain data key on the legend's paper plate (no art, no ImGui window): its title, then
    /// <see cref="ResourceOverlay.KeyRows"/> word-wrapped to the anchor's width, each shown layer's name row followed by its
    /// ramp (one swatch per level, pale to deep, at the overlay's opacity — exactly the fills the layer is drawn with).
    /// P9b: THE BOX NEVER REACHES ABOVE THE ANCHOR'S <see cref="OverlayKeyAnchor.Top"/>. While the rows fit beneath it the
    /// key is drawn whole, exactly as before; when they do not, the box stops at the cap and shows the title and a window of
    /// WHOLE rows (each with all its lines and its ramp) from the anchor's <see cref="OverlayKeyAnchor.Scroll"/> on, with a
    /// scroll bar in its right padding — nothing is dropped: at <see cref="OverlayKey.MaxScroll"/> the last row is drawn, and
    /// every row between is drawn at some scroll. The box drawn is the key's <see cref="OverlayKey.Rect"/>, the region that
    /// takes the clicks over it — never more.
    /// </summary>
    private static OverlayKey PaintOverlayKey(DrawList d, ITextMeasure m, OverlayPaint ov, OverlayKeyAnchor at, MapInk k)
    {
        ResourceOverlay b = ov.Bake;
        double pad = _type.Px(8), maxText = Math.Max(_type.Px(40), at.MaxWidth - 2 * pad);
        double swatch = _type.Px(9), swatchH = _type.Px(8);
        var rows = new List<OverlayKeyRow>();
        // P10: a 'now usable' shortcut's rows open the key while one of its layers is shown.
        if (ov.Highlight is { } hl)
        {
            bool shown = false;
            foreach (int li in hl.Layers) shown |= (uint)li < (uint)ov.Shown.Count && ov.Shown[li];
            if (shown) foreach (string r in hl.Rows) rows.Add(new OverlayKeyRow(r));
        }
        rows.AddRange(b.KeyRows(ov.Shown, ov.Status, ov.Names ?? (id => "settlement " + id.ToString(CultureInfo.InvariantCulture))));
        // The runs, in order: (text, role, font, gap before, ramp layer after it, the row it belongs to: -1 the title).
        var runs = new List<(string Text, TypeRole Role, FontRole Font, double Gap, int Ramp, int Row)>
        {
            (ResourceOverlay.KeyTitle, TypeRole.Caption, FontRole.Caps, 0, -1, -1),
        };
        double space = _type.Px(4);
        for (int r = 0; r < rows.Count; r++)
        {
            OverlayKeyRow row = rows[r];
            bool heading = row.Ramp >= 0;
            (TypeRole role, FontRole font) = heading ? (TypeRole.Body, FontRole.Heading) : (TypeRole.Secondary, FontRole.Body);
            List<string> lines = _type.Wrap(m, row.Text, role, font, maxText);
            for (int i = 0; i < lines.Count; i++)
                runs.Add((lines[i], role, font, i == 0 && heading ? space : 0, i == lines.Count - 1 ? row.Ramp : -1, r));
        }
        double w = LevelSwatchWidth(swatch), h = 0, titleH = 0;
        var rowH = new double[rows.Count];
        foreach ((string text, TypeRole role, FontRole font, double gap, int ramp, int row) in runs)
        {
            w = Math.Max(w, _type.Width(m, text, role, font));
            double runH = gap + _type.Size(role, font) * 1.3 + (ramp >= 0 ? swatchH + space : 0);
            h += runH;
            if (row < 0) titleH += runH;
            else rowH[row] += runH;
        }
        double boxW = Math.Min(at.MaxWidth, w + 2 * pad), boxH = h + 2 * pad;
        // P9b: the cap. The rows drawn are [first, end); unscrolled, all of them.
        int first = 0, end = rows.Count, maxScroll = 0;
        bool scrolls = boxH > at.Bottom - at.Top;
        double view = 0;
        if (scrolls)
        {
            const double Eps = 1e-6;   // px: a row that fits to the summation's last bits fits
            boxH = Math.Max(0, at.Bottom - at.Top);
            view = Math.Max(0, boxH - 2 * pad - titleH);
            // The largest scroll: the first row of the longest tail that fits the window (the last row alone at least).
            maxScroll = Math.Max(0, rows.Count - 1);
            double tail = 0;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                tail += rowH[i];
                if (tail > view + Eps) break;
                maxScroll = i;
            }
            first = Math.Clamp(at.Scroll, 0, maxScroll);
            double used = 0;
            end = first;
            while (end < rows.Count && used + rowH[end] <= view + Eps) used += rowH[end++];
        }
        var box = new RectD(at.Left, at.Bottom - boxH, boxW, boxH);
        MapHandTokens hand = k.Hand;
        d.Rect(box, A(k.NamePlate, 0.92), A(k.Ink, 0.55), Math.Max(1.0, hand.NamePlateRulePx), hand.NamePlateCornerPx);
        double y = box.Y + pad;
        foreach ((string text, TypeRole role, FontRole font, double gap, int ramp, int row) in runs)
        {
            if (row >= 0 && (row < first || row >= end)) continue;   // P9b: scrolled out of the window
            y += gap;
            _type.Label(d, box.X + pad, y, text, role, font, k.Ink, TextAlign.Left);
            y += _type.Size(role, font) * 1.3;
            if (ramp < 0) continue;
            for (int v = 0; v < ResourceOverlay.LevelCount; v++)
                d.Rect(new RectD(box.X + pad + v * swatch, y, swatch, swatchH), ResourceOverlay.Fill(k, b.Layers[ramp], v, ov.Opacity));
            d.Rect(new RectD(box.X + pad, y, LevelSwatchWidth(swatch), swatchH), null, A(k.Ink, 0.45), 1);
            y += swatchH + space;
        }
        if (scrolls && view > 0)
        {
            // P9b: the scroll bar in the right padding (the text never reaches it): the track the window's height, the thumb
            // the window's share of the rows, at the scroll's share of the way down.
            double barW = _type.Px(3), barX = box.Right - pad / 2 - barW / 2, barY = box.Y + pad + titleH;
            double thumbH = Math.Min(view, Math.Max(_type.Px(12), view * view / Math.Max(view, h - titleH)));
            double thumbY = barY + (maxScroll > 0 ? (view - thumbH) * first / maxScroll : 0);
            d.Rect(new RectD(barX, barY, barW, view), A(k.Ink, 0.12), null, 1, barW / 2);
            d.Rect(new RectD(barX, thumbY, barW, thumbH), A(k.Ink, 0.55), null, 1, barW / 2);
        }
        var texts = new List<string>();
        foreach (OverlayKeyRow row in rows) texts.Add(row.Text);
        return new OverlayKey(box, ResourceOverlay.KeyTitle, texts) { Scroll = first, MaxScroll = maxScroll, RowsDrawn = end - first };
    }

    private static double LevelSwatchWidth(double swatch) => swatch * ResourceOverlay.LevelCount;

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
        FanOffset(zoom == WorldZoom.World ? 0.0 : 1.0, slot, tokenSize);

    /// <summary>P2: the fan offset at a Regional blend <paramref name="regional"/> (<see cref="MapDetail.Regional"/>): the
    /// World row (0) folds continuously into the closer bands' column (1).</summary>
    public static (double Dx, double Dy) FanOffset(double regional, int slot, double tokenSize) =>
        (MapDetail.Mix(slot * (tokenSize * 2 + 3), 0.0, regional), MapDetail.Mix(0.0, slot * FanPitch(tokenSize), regional));

    /// <summary>The radius of a settlement's mark at World zoom (screen px): its population's place on the compressive class
    /// scale (P12a, D-043 D1; <see cref="SettlementClasses.WorldMarkRadiusPx"/>) — strictly increasing in population up to
    /// the scale's stated saturation. It was 5 + √population / 14 capped at 16 px (saturated from 23,716 people).</summary>
    public static double MarkRadiusPx(WorldProjection p, SettlementLensView s) => p.Classes.WorldMarkRadiusPx(s.Population);

    /// <summary>The built footprint's radius (screen px): its population's footprint in lattice units
    /// (<see cref="SettlementClasses.FootprintRadiusUnits"/>) × the screen px of a unit at this zoom
    /// (<see cref="SettlementClasses.UnitPx"/>, the Settlement blend <paramref name="settlement"/> moving the band's floor —
    /// continuous across the Regional ↔ Settlement window). At any one zoom it is strictly increasing in population up to
    /// the scale's saturation: no clamp on the population side (P12a; it was the world size from √population × the scale,
    /// clamped to 14–40 px at Regional and 34–150 px at Settlement).</summary>
    public static double FootprintRadiusPx(WorldProjection p, SettlementLensView s, double scale, double settlement) =>
        p.Classes.FootprintRadiusUnits(s.Population) * p.Classes.UnitPx(scale, settlement);

    /// <summary>A settlement's aggregated morphology at Regional and Settlement zoom — the footprint, the dwellings, the
    /// centre mark, the embedded glyphs (and their key) and the sector ring; its name goes under them, at
    /// <see cref="MorphNameY"/>. Each run of commands is charged to its map layer and to the information layer it shows (P3). P2:
    /// the footprint, the dwellings, the centre and the sector ring fade in with their layers (the Regional blend);
    /// the glyph ring is drawn at the sum of its two layers' opacities (institutional
    /// presence, institution types) — fading in across the World ↔ Regional window, whole across the Regional ↔
    /// Settlement one, where its size and radius blend; the key and the notables line fade in with institution types.</summary>
    private static void PaintMorphology(DrawList d, ITextMeasure m, WorldProjection p, SettlementLensView s,
        double sx, double sy, double r, double unitPx, double settlement, MapDetail detail,
        Action<MapLayer, WorldLayer?> charge, List<InstitutionMarker> institutions, List<PartPlacement> parts, MapInk k)
    {
        Rgba ink = k.Ink;
        Rgba pol = k.PolityOf(s.Controller, p.PlayerPolity);
        int from = d.Commands.Count;
        // P12a: the settlement's composition — every lot in lattice units about its centre, a function of its state and the
        // content only (SettlementComposer); this paint only scales it by the zoom's px per unit.
        SettlementComposition comp = p.CompositionOf(s);
        CompositionLattice lattice = p.Classes.Lattice;

        // Footprint: the settlement's built extent (its radius from population), outlined. The
        // composition is a READ OF STATE (D-038 H5): no wall, palisade or tower is drawn, because no
        // settlement state records one — and the Age gates nothing physical (it flies as the banner).
        d.Circle(sx, sy, r, A(k.Footprint, 0.92), null);
        d.Circle(sx, sy, r, null, A(ink, 0.55), Stroke(k, 1.2));

        // Dwellings (P12a): one block per dwellingsPerBlock dwellings on the FIXED block lattice — the first lots in its
        // fixed order on the rings the footprint holds, a lot under a part's glyph hidden where it stands — so a new block
        // or a new part never moves a block already drawn, and a growing footprint opens rings outward (no cap of 48).
        // One form at every Age.
        double bw = lattice.BlockUnits * unitPx, bh = lattice.BlockHeightUnits * unitPx;
        foreach (CompositionLot lot in comp.Blocks)
        {
            double bx = sx + lot.X * unitPx, by = sy + lot.Y * unitPx;
            d.Rect(new RectD(bx - bw / 2, by - bh / 2, bw, bh), A(k.Dwelling, 0.85));
        }

        // Centre: the capital star / settlement mark, on the plaza the block lattice leaves open.
        double centre = Math.Max(5, lattice.PlazaRadiusUnits * unitPx);
        d.Circle(sx, sy, centre, pol, ink, Stroke(k, 1.2));
        if (s.IsCapital) Star(d, sx, sy, Math.Max(4, centre * 6 / 7), k.CapitalMark);
        d.FadeFrom(from, detail.Alpha(WorldLayer.SettlementMorphology));

        charge(MapLayer.SettlementMarkers, WorldLayer.SettlementMorphology);
        double aPresence = detail.Alpha(WorldLayer.InstitutionalPresence), aTypes = detail.Alpha(WorldLayer.InstitutionTypes);
        if (aPresence > 0 || aTypes > 0)
        {
            // Embedded glyphs inside the footprint — one per structure KIND and one per institution
            // TYPE (aggregated), with names and counts at settlement zoom.
            bool named = aTypes > 0;
            double aRing = Math.Min(1.0, aPresence + aTypes);
            WorldLayer ringLayer = detail.Band == WorldZoom.Settlement ? WorldLayer.InstitutionTypes : WorldLayer.InstitutionalPresence;
            int total = s.Structures.Count + s.Institutions.Count;
            double g = lattice.GlyphUnits * unitPx;   // P12a: the glyph fills its lot's box at every zoom
            int n = 0;
            // Settlement zoom: a key beside the footprint (left, right-aligned) names each embedded glyph
            // with its aggregated count, so labels never overprint the footprint or each other.
            void Key(int i, string label, long count, Action<double, double> glyph)
            {
                int at = d.Commands.Count;
                KeyEntry(d, sx, sy, r, total, i, label, count, glyph, ink);
                d.FadeFrom(at, aTypes);
                charge(MapLayer.InstitutionMarkers, WorldLayer.InstitutionTypes);
            }
            // P12a: each part on ITS lot of the composition (comp.Parts: the structures, then the institution types, in the
            // view's order) — never spaced by its index among the others, so an arrival re-spaces nothing.
            (double, double) Slot(int i) => (sx + comp.Parts[i].Lot.X * unitPx, sy + comp.Parts[i].Lot.Y * unitPx);
            void Placed(int i, int at)
            {
                RectD? box = null;
                for (int c = at; c < d.Commands.Count; c++)
                    if (DrawListCull.Bounds(d.Commands[c]) is RectD b) box = box is RectD u ? Union(u, b) : b;
                RectD rect = box is RectD bb ? bb.Inset(DrawListCull.FringePx) : new RectD(0, 0, 0, 0);
                parts.Add(new PartPlacement(s.Id, comp.Parts[i].Key, comp.Parts[i].Lot.Ring, comp.Parts[i].Lot.Index, rect));
            }
            foreach (StructureView st in s.Structures)
            {
                (double gx, double gy) = Slot(n++);
                int at = d.Commands.Count;
                StructureGlyph(d, gx, gy, g, st.Kind, k);
                Placed(n - 1, at);
                d.FadeFrom(at, aRing);
                charge(MapLayer.InstitutionMarkers, ringLayer);
                if (named) Key(n - 1, st.Name, st.Count, (x, y) => StructureGlyph(d, x, y, 7, st.Kind, k));
                institutions.Add(new InstitutionMarker(s.Id, "structure:" + st.ProjectId.ToString(CultureInfo.InvariantCulture), st.Count));
            }
            foreach (InstitutionView iv in s.Institutions)
            {
                (double gx, double gy) = Slot(n++);
                int at = d.Commands.Count;
                UniversityGlyph(d, gx, gy, g, iv.TypeKey, k);
                Placed(n - 1, at);
                d.FadeFrom(at, aRing);
                charge(MapLayer.InstitutionMarkers, ringLayer);
                if (named) Key(n - 1, iv.Name, iv.Count, (x, y) => UniversityGlyph(d, x, y, 7, iv.TypeKey, k));
                institutions.Add(new InstitutionMarker(s.Id, "institution:" + iv.TypeKey.ToString(CultureInfo.InvariantCulture), iv.Count));
            }
            if (s.Notables > 0 && named)
            {
                int at = d.Commands.Count;
                NotablesLine(d, s, sx, sy, r, ink);
                d.FadeFrom(at, aTypes);
            }
        }
        charge(MapLayer.InstitutionMarkers, detail.Band == WorldZoom.Settlement ? WorldLayer.InstitutionTypes : WorldLayer.InstitutionalPresence);

        double aSectors = detail.Alpha(WorldLayer.ProductionSignals);
        if (aSectors > 0 && s.Sectors is not null)
        {
            // Labour allocation as a ring of sector arcs just outside the footprint.
            int at = d.Commands.Count;
            double start = -90;
            for (int i = 0; i < 5; i++)
            {
                double sweep = 360 * s.Sectors![i];
                (double ringR, double ringW) = SectorRing(r, settlement);
                if (sweep >= 359) d.Circle(sx, sy, ringR, null, k.Sectors[i], ringW);
                else if (sweep > 0.5) d.Arc(sx, sy, ringR, start, sweep - 1.5, k.Sectors[i], ringW);
                start += sweep;
            }
            d.FadeFrom(at, aSectors);
        }
        charge(MapLayer.SettlementMarkers, WorldLayer.ProductionSignals);
    }

    /// <summary>Where a settlement's name goes at Regional and Settlement zoom (the y of its top): under the footprint of
    /// radius <paramref name="r"/>, clear of its sector ring when <paramref name="sectors"/> draws one.</summary>
    private static double MorphNameY(double sy, double r, bool sectors) => sy + r + (sectors ? 18 : 8);

    /// <summary>The sector ring about a footprint of radius <paramref name="r"/>: its radius and stroke width, blended by
    /// the Settlement blend <paramref name="settlement"/>.</summary>
    private static (double Radius, double Width) SectorRing(double r, double settlement) =>
        (r + MapDetail.Mix(6, 10, settlement), MapDetail.Mix(4, 6, settlement));

    /// <summary>Entry <paramref name="i"/> of <paramref name="total"/> in a settlement's key at Settlement zoom: the part's
    /// glyph and its name with its aggregated count, right-aligned beside (left of) the footprint of radius
    /// <paramref name="r"/>, the entries centred on the settlement's height.</summary>
    private static void KeyEntry(DrawList d, double sx, double sy, double r, int total, int i, string label, long count,
        Action<double, double> glyph, Rgba ink)
    {
        double pitch = Math.Max(18, _type.Size(TypeRole.Caption, FontRole.Caps) * 1.3);
        double ky = sy - total * pitch / 2 + i * pitch + pitch / 2;
        double kx = sx - r - 18;
        glyph(kx, ky);
        _type.Label(d, kx - 12, ky - _type.Size(TypeRole.Caption, FontRole.Caps) * 0.62, label + (count > 1 ? " x" + count.ToString(CultureInfo.InvariantCulture) : ""), TypeRole.Caption, FontRole.Caps, ink, TextAlign.Right);
    }

    /// <summary>The resident notables line of a settlement at Settlement zoom, just below its centre.</summary>
    private static void NotablesLine(DrawList d, SettlementLensView s, double sx, double sy, double r, Rgba ink) =>
        _type.Label(d, sx, sy + r * 0.12 + 6, s.Notables.ToString(CultureInfo.InvariantCulture) + " notable(s) resident", TypeRole.Secondary, FontRole.Body, ink, TextAlign.Center);

    /// <summary>
    /// P12a review fix — THE SETTLEMENT CULL FOLLOWS WHAT THE SETTLEMENT PAINTS. Whether anything <paramref name="s"/> paints
    /// at this level of detail can touch <paramref name="viewport"/>: the bounds the replay keeps
    /// (<see cref="DrawListCull.Bounds"/>) of every command its paint emits, taken in three steps.
    /// (1) The marks about its centre, by their radii — the World halo, the footprint (every dwelling block and part glyph
    /// lies inside it: the lattice is validated so, <see cref="SettlementClasses.Parse"/>), the plaza mark, the sector ring,
    /// the selection ring — each with its stroke and the anti-aliasing fringe; enough when they touch.
    /// (2) Otherwise what hangs outside them — the name plate, the lines under it, the key, the notables line, the banner.
    /// First a cheap vertical test that sets and measures nothing (a run of text is as tall as its set size and line count
    /// make it, whatever its letters): a settlement whose band of labels lies wholly above or below the viewport is culled.
    /// (3) Otherwise those labels are painted by their own painters into a throw-away list, at the same places, and their
    /// bounds tested — exact, however long a name, a key entry or a line is.
    /// It replaces a fixed 200 px margin about the centre, which culled a settlement whose footprint had outgrown it once the
    /// 150 px footprint clamp was gone (at ×32 the measured maximum reaches 209 px, the saturation 330 px), and every
    /// settlement whose key or lines reached past it. Pure: nothing it paints reaches the frame's list.
    /// </summary>
    private static bool Reaches(ITextMeasure m, WorldProjection p, SettlementLensView s, double sx, double sy,
        double rMark, double rFoot, double unitPx, double nameSize, double ly, bool banner, double bannerY, bool selected,
        MapDetail detail, MapInk k, RectD viewport)
    {
        double ring = 0;
        if (detail.Alpha(WorldLayer.MajorSettlements) > 0) ring = rMark + 2 + Stroke(k, 1.2);
        bool morph = detail.Alpha(WorldLayer.SettlementMorphology) > 0;
        if (morph)
        {
            ring = Math.Max(ring, Math.Max(rFoot, Math.Max(5, p.Classes.Lattice.PlazaRadiusUnits * unitPx)) + Stroke(k, 1.2));
            if (detail.Alpha(WorldLayer.ProductionSignals) > 0 && s.Sectors is not null)
            {
                (double ringR, double ringW) = SectorRing(rFoot, detail.Settlement);
                ring = Math.Max(ring, ringR + ringW);
            }
        }
        if (selected) ring = Math.Max(ring, MapDetail.Mix(20, rFoot + 8, detail.Regional) + 2.4);
        const double F = DrawListCull.FringePx;
        ring += F;
        if (DrawListCull.Touches(new RectD(sx - ring, sy - ring, 2 * ring, 2 * ring), viewport)) return true;

        double top = sy - ring, bottom = sy + ring;
        // A run set at design size `design` from `y`: DrawListCull bounds it from half its set size above y to one and a
        // half below per line (TextCmd), the fringe round that.
        void Run(double y, double design, string? text)
        {
            double z = _type.Set(design);
            int lines = 1;
            if (text is not null) foreach (char c in text) if (c == '\n') lines++;
            top = Math.Min(top, y - 0.5 * z - F);
            bottom = Math.Max(bottom, y - 0.5 * z + (lines + 1) * z + F);
        }
        void Span(double y0, double y1) { top = Math.Min(top, y0 - F); bottom = Math.Max(bottom, y1 + F); }
        double caps = _type.Size(TypeRole.Caption, FontRole.Caps), second = _type.Size(TypeRole.Secondary, FontRole.Body);
        double rule = k.Hand.NamePlateRulePx > 0 ? k.Hand.NamePlateRulePx : 0;
        Run(ly, nameSize, s.Name);                                          // the name and its plate (Name)
        Span(ly - 1 - rule, ly + nameSize * 1.3 + 1 + rule);
        if (morph)
        {
            double y = ly + nameSize * 1.35, data = _type.Size(TypeRole.Data, FontRole.Numeric);   // the lines (PaintSettlementLines)
            if (detail.Alpha(WorldLayer.PopulationScale) > 0) { Run(y, data, null); y += data * 1.3; }
            if (detail.Alpha(WorldLayer.InfrastructureDensity) > 0) { Run(y, second, null); y += second * 1.3; }
            if (detail.Alpha(WorldLayer.ProductionSignals) > 0 && detail.Settlement > 0)
            {
                Run(y, caps, s.TopSectorLabel);
                Span(y + caps * 0.25 - 0.8, y + caps * 0.25 + 9 + 0.8);
            }
            if (detail.Alpha(WorldLayer.InstitutionTypes) > 0)
            {
                // The key (KeyEntry): each entry's run, and its 7 px glyph with the widest stroke, emblem and wobble a hand draws.
                int total = s.Structures.Count + s.Institutions.Count;
                double pitch = Math.Max(18, caps * 1.3), glyph = 7 + Stroke(k, 1.4) + Emblem(k, 1.8) + k.Hand.WobblePx;
                for (int i = 0; i < total; i++)
                {
                    double ky = sy - total * pitch / 2 + i * pitch + pitch / 2;
                    Run(ky - caps * 0.62, caps, i < s.Structures.Count ? s.Structures[i].Name : s.Institutions[i - s.Structures.Count].Name);
                    Span(ky - glyph, ky + glyph);
                }
                if (s.Notables > 0) Run(sy + rFoot * 0.12 + 6, second, null);   // NotablesLine
            }
        }
        if (banner)
        {
            // The banner (Banner): the cloth from h above its foot to 6 below, the numeral, the pole with its wobble.
            double h = caps * 1.2, pole = Stroke(k, 1.4) + k.Hand.WobblePx;
            Span(bannerY - h, bannerY + 6);
            Run(bannerY - h + 1, caps, null);
            Span(bannerY + 2 - pole, bannerY + 16 + pole);
        }
        // A pixel of slack: this test only spares the exact one below, so rounding must never make it cull.
        if (bottom < viewport.Y - 1 || top > viewport.Bottom + 1) return false;

        var probe = new DrawList();
        Name(probe, m, sx, ly, s.Name, nameSize, k);
        if (morph)
        {
            PaintSettlementLines(probe, m, s, sx, ly + nameSize * 1.35, detail.Settlement, detail, static (_, _) => { }, k);
            if (detail.Alpha(WorldLayer.InstitutionTypes) > 0)
            {
                int total = s.Structures.Count + s.Institutions.Count, n = 0;
                foreach (StructureView st in s.Structures)
                    KeyEntry(probe, sx, sy, rFoot, total, n++, st.Name, st.Count, (x, y) => StructureGlyph(probe, x, y, 7, st.Kind, k), k.Ink);
                foreach (InstitutionView iv in s.Institutions)
                    KeyEntry(probe, sx, sy, rFoot, total, n++, iv.Name, iv.Count, (x, y) => UniversityGlyph(probe, x, y, 7, iv.TypeKey, k), k.Ink);
                if (s.Notables > 0) NotablesLine(probe, s, sx, sy, rFoot, k.Ink);
            }
        }
        if (banner) Banner(probe, m, sx, bannerY, s.Age, k.Ink, s.IsCapital, k, s.Id);
        foreach (DrawCmd c in probe.Commands)
            if (DrawListCull.Bounds(c) is RectD b && DrawListCull.Touches(b, viewport)) return true;
        return false;
    }

    /// <summary>The lines under a settlement's name at Settlement zoom — people and dwellings, path links and size tier,
    /// the labour split — from <paramref name="y"/> down. P2: each fades in with its layer's opacity (population scale,
    /// infrastructure density; the labour caption with the Settlement blend <paramref name="settlement"/>).</summary>
    private static void PaintSettlementLines(DrawList d, ITextMeasure m, SettlementLensView s, double sx, double y,
        double settlement, MapDetail detail, Action<MapLayer, WorldLayer?> charge, MapInk k)
    {
        Rgba ink = k.Ink;
        double ly = y;
        double data = _type.Size(TypeRole.Data, FontRole.Numeric), second = _type.Size(TypeRole.Secondary, FontRole.Body);
        double caps = _type.Size(TypeRole.Caption, FontRole.Caps);
        double aPop = detail.Alpha(WorldLayer.PopulationScale), aDensity = detail.Alpha(WorldLayer.InfrastructureDensity);
        if (aPop > 0)
        {
            int at = d.Commands.Count;
            _type.Label(d, sx, ly, s.Population.ToString("#,0", CultureInfo.InvariantCulture) + " people  -  " + s.Dwellings.ToString("#,0", CultureInfo.InvariantCulture) + " dwellings", TypeRole.Data, FontRole.Numeric, ink, TextAlign.Center);
            d.FadeFrom(at, aPop);
            ly += data * 1.3;
        }
        charge(MapLayer.SettlementMarkers, WorldLayer.PopulationScale);
        if (aDensity > 0)
        {
            int at = d.Commands.Count;
            _type.Label(d, sx, ly, s.RoadsInCatchment.ToString(CultureInfo.InvariantCulture) + " path link(s) in catchment  -  size tier " + s.SizeTier.ToString(CultureInfo.InvariantCulture), TypeRole.Secondary, FontRole.Body, ink, TextAlign.Center);
            d.FadeFrom(at, aDensity);
            ly += second * 1.3;
        }
        charge(MapLayer.SettlementMarkers, WorldLayer.InfrastructureDensity);
        bool production = detail.Alpha(WorldLayer.ProductionSignals) > 0 && settlement > 0;
        int caption = d.Commands.Count;
        if (production && s.Sectors is null)
            _type.Label(d, sx, ly, "labour split: default (no allocation ordered)", TypeRole.Caption, FontRole.Caps, ink, TextAlign.Center);
        else if (production && s.Sectors is not null)
        {
            int top = 0;
            for (int i = 1; i < 5; i++) if (s.Sectors[i] > s.Sectors[top]) top = i;   // ties: lowest sector index
            string sectorLabel = s.TopSectorLabel ?? SectorNames[top];
            string lab = "labour mostly " + sectorLabel.ToLowerInvariant() + " (" + Math.Round(s.Sectors[top] * 100).ToString("0", CultureInfo.InvariantCulture) + "%)";
            double lw = _type.Width(m, lab, TypeRole.Caption, FontRole.Caps);
            d.Rect(new RectD(sx - lw / 2 - 14, ly + caps * 0.25, 9, 9), k.Sectors[top], ink, 0.8);
            _type.Label(d, sx + 5, ly, lab, TypeRole.Caption, FontRole.Caps, ink, TextAlign.Center);
        }
        d.FadeFrom(caption, settlement);
        charge(MapLayer.SettlementMarkers, WorldLayer.ProductionSignals);
    }

    /// <summary>A settlement's name on a soft paper plate, so a road running under it never cuts the letters (UR-7: at
    /// the body role in the era's heading face — it was 12.5 px — and the heading role at settlement zoom; P2: at a size
    /// blended between the two across the Regional ↔ Settlement window). The plate's
    /// ornament is the era's hand (its corner and the rule round it); its rect is the same in every hand. Returns the
    /// plate's rect (P3: the name's hit rect — it holds the name's run, which is centred on it).</summary>
    private static RectD Name(DrawList d, ITextMeasure m, double x, double y, string name, double size, MapInk k)
    {
        double w = _type.WidthAt(m, name, size, FontRole.Heading);
        MapHandTokens hand = k.Hand;
        bool ruled = hand.NamePlateRulePx > 0;
        var plate = new RectD(x - w / 2 - 4, y - 1, w + 8, size * 1.3 + 2);
        d.Rect(plate, A(k.NamePlate, 0.78),
            ruled ? A(k.Ink, 0.55) : null, ruled ? hand.NamePlateRulePx : 1, hand.NamePlateCornerPx);
        _type.LabelAt(d, x, y, name, size, FontRole.Heading, k.Ink, TextAlign.Center);
        return plate;
    }

    /// <summary>A settlement's Age banner: the pole, the cloth in the controller's ink with the era's fly
    /// (<see cref="Fly"/>), the Age numeral. Three commands in every hand.</summary>
    private static void Banner(DrawList d, ITextMeasure m, double x, double y, int age, Rgba pol, bool capital, MapInk k, int settlement)
    {
        string t = AgePanelModel.Numeral(age);
        double size = _type.Size(TypeRole.Caption, FontRole.Caps);
        double w = _type.Width(m, t, TypeRole.Caption, FontRole.Caps) + 12;
        double h = size * 1.2;
        HandLine(d, k, x, y + 16, x, y + 2, k.Ink, Stroke(k, 1.4), settlement, BannerPoleSalt, 0);
        d.Polygon(Fly(k.Hand.Banner, x, y, w, h), pol);
        _type.Label(d, x + 6, y - h + 1, t, TypeRole.Caption, FontRole.Caps, capital ? k.CapitalMark : k.BannerText, TextAlign.Left);
    }

    /// <summary>The banner's cloth for <paramref name="fly"/>: hoisted at <paramref name="x"/>, from the top
    /// <c>y − h</c> to <c>y + 6</c>, <paramref name="w"/> wide. Every fly stays inside that box and leaves the
    /// numeral's field (x + 6 … x + w − 6, the numeral's height below the top) covered.</summary>
    public static (double X, double Y)[] Fly(BannerFly fly, double x, double y, double w, double h) => fly switch
    {
        BannerFly.Tapered => [(x, y - h), (x + w, y - h), (x + w, y), (x, y + 6)],
        BannerFly.Square => [(x, y - h), (x + w, y - h), (x + w, y + 6), (x, y + 6)],
        _ => [(x, y - h), (x + w, y - h), (x + w - 4, y - h * 0.35), (x + w, y + 6), (x, y + 6)],   // Swallowtail: today's
    };

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

        /// <summary>P2: the width of <paramref name="text"/> set at <paramref name="size"/> px (a size between two roles' —
        /// a blended label); at a role's own size exactly <see cref="Width"/>.</summary>
        public double WidthAt(ITextMeasure m, string text, double size, FontRole font) => theme is { } t
            ? m.Width(t, text, size, font)
            : m.Width(text, size, font);

        /// <summary>P2: a label set at <paramref name="size"/> px; at a role's own size exactly <see cref="Label"/>.</summary>
        public void LabelAt(DrawList d, double x, double y, string text, double size, FontRole font, Rgba color, TextAlign align)
        {
            if (theme is { } t) d.Write(t, x, y, text, size, color, align, font);
            else d.Text(x, y, text, size, color, align, font);
        }

        /// <summary><paramref name="px"/> design pixels at the UI scale.</summary>
        public double Px(double px) => px * S;

        /// <summary>The size a run of design size <paramref name="designSize"/> is SET at — what <see cref="LabelAt"/> hands
        /// the list (the era's size scale applied, <see cref="ThemeText.Write"/>); the design size with no theme.</summary>
        public double Set(double designSize) => theme is { } t ? t.Type.Size(designSize) : designSize;

        /// <summary>P3: <paramref name="text"/> word-wrapped so every SET line fits <paramref name="width"/> (a single
        /// word wider than it keeps its own line). Joining the lines with one space gives the text back.</summary>
        public List<string> Wrap(ITextMeasure m, string text, TypeRole role, FontRole font, double width)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Width(m, candidate, role, font) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }
    }

    private static RectD Union(RectD a, RectD b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new RectD(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
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
                d.Rect(new RectD(x - g * 0.6, y - g * 0.2, g * 1.2, g * 0.9), k.Granary, ink, Stroke(k, 1));
                d.Arc(x, y - g * 0.2, g * 0.6, 180, 180, ink, Stroke(k, 1.4));
                break;
            case StructureKind.Production: // workshop: anvil-roofed block with a chimney
                d.Rect(new RectD(x - g * 0.7, y - g * 0.3, g * 1.4, g * 0.9), k.Workshop, ink, Stroke(k, 1));
                d.Rect(new RectD(x + g * 0.25, y - g * 0.8, g * 0.25, g * 0.5), ink);
                break;
            default:
                d.Rect(new RectD(x - g / 2, y - g / 2, g, g), k.StructureOther, ink, Stroke(k, 1));
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
        int n = 0;
        // An emblem stroke in the era's hand, seeded from the TYPE (every hall of a type is drawn alike).
        void L(double x0, double y0, double x1, double y1, double w) =>
            HandLine(d, k, x0, y0, x1, y1, mark, Emblem(k, w), typeKey, HallEmblemSalt, n++);
        d.Polygon([(x - g * 0.85, y - g * 0.35), (x, y - g * 0.95), (x + g * 0.85, y - g * 0.35)], frame);
        d.Rect(new RectD(x - g * 0.75, y - g * 0.35, g * 1.5, g * 1.15), fill, frame, Stroke(k, 1));
        double e = g * 0.38, cy = y + g * 0.22;
        switch (typeKey)
        {
            case 1: L(x - e, cy - e, x + e, cy + e, 1.6); L(x - e, cy + e, x + e, cy - e, 1.6); break;
            case 2: d.Rect(new RectD(x - e * 0.3, cy - e, e * 0.6, e * 2), mark); d.Rect(new RectD(x - e, cy - e * 0.3, e * 2, e * 0.6), mark); break;
            case 3: d.Circle(x, cy, e * 0.75, null, mark, Emblem(k, 1.6)); for (int i = 0; i < 6; i++) { double a = Math.PI * i / 3; L(x + Math.Cos(a) * e * 0.75, cy + Math.Sin(a) * e * 0.75, x + Math.Cos(a) * e * 1.1, cy + Math.Sin(a) * e * 1.1, 1.4); } break;
            case 4: d.Circle(x, cy, e * 0.35, mark, null); d.Circle(x, cy, e, null, mark, Emblem(k, 1.1)); break;
            case 5: L(x, cy + e, x, cy - e, 1.4); L(x, cy - e * 0.2, x - e * 0.7, cy - e * 0.9, 1.2); L(x, cy - e * 0.2, x + e * 0.7, cy - e * 0.9, 1.2); break;
            default: d.Circle(x, cy, e * 0.5, mark, null); break;
        }
    }

    /// <summary>A formation token: a framed plate whose emblem is the unit FAMILY (twelve families,
    /// twelve emblems), filled with the owner's ink — the family mark alone (<see cref="UnitEmblem.Unkeyed"/>).</summary>
    public static void UnitToken(DrawList d, double x, double y, double s, int family, Rgba owner, MapInk? ink = null) =>
        UnitToken(d, x, y, s, UnitEmblem.Unkeyed(family), owner, ink);

    /// <summary>The PIP PITCH of the form pips at token half-size <paramref name="s"/>: nine pips span 1.44 s, inside the
    /// plate's 2 s width.</summary>
    public static double PipPitch(double s) => s * 0.18;

    /// <summary>
    /// P11: A FORMATION TOKEN KEYED BY ITS CURRENT IDENTITY (<see cref="UnitEmblem"/>) — a framed plate in the owner's
    /// ink; on it the ROLE mark of the identity's family (the twelve H1 emblems, command for command) and, below it, one
    /// short upright PIP per form of the identity's place in its family's line (none when the content does not know it).
    /// Non-anatomical by construction (D-038 C1): a plate, straight strokes, small polygons and circles, all inside the
    /// plate's 2s × 1.5s box (the hit rect) — no figure, no face, no limb, no frame of animation, and no number.
    /// </summary>
    public static void UnitToken(DrawList d, double x, double y, double s, UnitEmblem emblem, Rgba owner, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        int family = emblem.Glyph;
        Rgba frame = k.GlyphInk, pale = k.EmblemMark;
        int n = 0;
        // An emblem stroke in the era's hand, seeded from the FAMILY (every token of a family is drawn alike).
        void L(double x0, double y0, double x1, double y1, double w) =>
            HandLine(d, k, x0, y0, x1, y1, pale, Emblem(k, w), family, UnitEmblemSalt, n++);
        // The plate: the same 2s × 1.5s box (the hit rect) in every era; the era cuts its corners.
        d.Rect(new RectD(x - s, y - s * 0.75, s * 2, s * 1.5), owner, frame, Stroke(k, 1.4), PlateRadius(k.Hand.Plate, s));
        double e = s * 0.5;
        switch (family)
        {
            case 1: L(x - e, y + e * 0.6, x, y - e * 0.8, 1.6); L(x, y - e * 0.8, x + e, y + e * 0.6, 1.6); break; // light infantry: chevron
            case 2: L(x - e, y + e, x + e, y - e, 1.8); d.Polygon([(x + e, y - e), (x + e * 0.3, y - e * 0.8), (x + e * 0.8, y - e * 0.3)], pale); break; // spear
            case 3: L(x - e, y - e * 0.7, x + e, y + e * 0.7, 1.6); L(x - e, y + e * 0.7, x + e, y - e * 0.7, 1.6); break; // heavy infantry: crossed
            case 4: d.Circle(x, y, e * 0.7, null, pale, Emblem(k, 1.6)); d.Circle(x, y, 1.6, pale, null); break; // ranged: target
            case 5: L(x - e, y + e * 0.7, x + e, y - e * 0.7, 1.8); break; // light cavalry: slash
            case 6: L(x - e, y + e * 0.7, x + e, y - e * 0.7, 2.6); L(x - e, y - e * 0.2, x + e * 0.2, y + e * 0.7, 1.4); break;
            case 7: L(x - e, y + e * 0.7, x + e, y - e * 0.7, 1.8); d.Circle(x - e * 0.4, y - e * 0.3, 2, pale, null); break;
            case 8: d.Circle(x, y + e * 0.3, e * 0.45, null, pale, Emblem(k, 1.6)); L(x - e, y - e * 0.2, x + e, y - e * 0.2, 1.6); break; // siege
            case 9: d.Polygon([(x - e, y), (x + e, y), (x + e * 0.6, y + e * 0.6), (x - e * 0.6, y + e * 0.6)], pale); L(x, y, x, y - e, 1.4); break;
            case 10: d.Polygon([(x - e, y), (x + e, y), (x + e * 0.6, y + e * 0.6), (x - e * 0.6, y + e * 0.6)], pale); break;
            case 11: L(x - e, y, x + e, y, 1.6); L(x, y - e * 0.7, x, y + e * 0.7, 1.6); break;
            case 12: d.Polygon([(x, y - e), (x + e * 0.35, y + e * 0.6), (x - e * 0.35, y + e * 0.6)], pale); break;
            default: d.Circle(x, y, e * 0.5, pale, null); break;
        }
        // The form pips: one upright stroke per form, centred in the band under the role mark, seeded from the
        // IDENTITY (every token of an identity is drawn alike).
        int pips = Math.Min(emblem.Form, UnitEmblem.MaxPips);
        double pitch = PipPitch(s), top = y + s * 0.54, foot = y + s * 0.70;
        for (int i = 0; i < pips; i++)
        {
            double px = x + (i - (pips - 1) / 2.0) * pitch;
            HandLine(d, k, px, top, px, foot, pale, Emblem(k, 1.0), emblem.Identity, UnitFormSalt, i);
        }
    }
}
