using System.Globalization;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// TEST-ONLY FIXTURE — a seed-42 world (256 px, 4 settlements) founded through the real
/// <see cref="UiSession"/> and played 6 End Turns, then CLONED and given state through the very row types
/// the simulation publishes: <see cref="StructureRow"/> (granary x2 + workshop x1 in the player's capital,
/// granary x1 in a second settlement) and <see cref="ResearchCostModifierRow"/> (all five
/// specialized-university types for the player, Engineering twice; one row for a polity that controls
/// nothing). Nothing here is reachable from production code.
/// <para>Stream U3 (M5 integration): the ResearchCostModifiers rows are now the NEGATIVE CONTROL — they
/// are research-cost effects, not institutions, so the lens must draw no university from them
/// (institutions come only through <see cref="InstitutionMarkerSource"/>). The university glyph, name
/// and count rendering is exercised through that seam with <see cref="SeamUniversities"/>.</para>
/// </summary>
public sealed class InstitutionWorldFixture
{
    public UiSession Session { get; }
    public int Capital { get; }
    public int Second { get; }
    public int Granary { get; }
    public int Workshop { get; }

    public InstitutionWorldFixture()
    {
        UiSession founded = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 6; t++) founded.EndTurn();
        WorldState w = founded.World.Clone();
        PolityId me = UiPlayer.Empire;
        Assert.True(EmpireQuery.TryGetCapital(w, me, out SettlementId cap));
        Capital = cap.Value;
        Second = -1;
        for (int i = 0; i < w.Settlements.Count && Second < 0; i++)
            if (w.Settlements[i].Id != cap) Second = w.Settlements[i].Id.Value;
        foreach (ConstructionProjectEntry p in founded.Config.Goods!.Projects!)
        {
            if (p.Name == "granary") Granary = p.Id;
            if (p.Name == "workshop") Workshop = p.Id;
        }
        w.Structures.Add(new StructureRow(cap, Granary, 2));
        w.Structures.Add(new StructureRow(cap, Workshop, 1));
        w.Structures.Add(new StructureRow(new SettlementId(Second), Granary, 1));
        foreach ((int type, double f) in new[] { (1, 0.9), (2, 0.85), (3, 0.8), (3, 0.95), (4, 0.9), (5, 0.7) })
            w.ResearchCostModifiers.Add(new ResearchCostModifierRow(me, type, f));
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(new PolityId(97), 2, 0.5));
        Session = UiSession.StartFrom(w, 42, 256, 4);
    }

    /// <summary>A stand-in for the institutions stream's implementation of
    /// <see cref="InstitutionMarkerSource.InstitutionsAt"/>: the capital HAS the five university types
    /// (Engineering twice), named from content. Test-only; production reads the real seam.</summary>
    public IReadOnlyList<InstitutionView> SeamUniversities(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (settlement.Value != Capital) return [];
        var r = new List<InstitutionView>();
        foreach (Sim.Core.Systems.Research.UniversityType u in cfg.Research!.UniversityTypes)
            r.Add(new InstitutionView(u.Key, u.Name, u.Key == 3 ? 2 : 1));
        return r;
    }
}

/// <summary>
/// Map layer ownership (docs/architecture/age-and-world-ui.md §5.1): each map layer has exactly one
/// owner and is drawn once; and the settlement-zoom institution markers are drawn from — and exactly
/// match — the state: structures from <c>Structures</c> rows, institutions only from
/// <see cref="InstitutionMarkerSource"/> (stream U3; never from <c>ResearchCostModifiers</c>).
/// </summary>
public class WorldLayerOwnershipTests(InstitutionWorldFixture fx) : IClassFixture<InstitutionWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly WorldZoom[] Zooms = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    private WorldProjection Project() =>
        WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me);

    /// <summary>The whole 256-px world at 4 px per world px fits the 1024-px viewport: nothing is culled.</summary>
    private static LensFrame PaintAll(DrawList d, WorldProjection p, WorldZoom z, bool territory = true) =>
        WorldLens.Paint(d, ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024),
            showTerritory: territory);

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    // ------------------------------------------------------------------ ownership

    [Fact]
    public void Registry_EveryLayerHasExactlyOneOwner_LegacyPassKeepsOnlyTerrainAndRivers()
    {
        Assert.Equal(Enum.GetValues<MapLayer>(), MapLayerOwnership.All);
        Assert.Equal([MapLayer.Terrain, MapLayer.Rivers], MapLayerOwnership.MapRendererLayers());
        foreach (MapLayer l in new[] { MapLayer.Territory, MapLayer.Paths, MapLayer.SettlementMarkers,
                     MapLayer.UnitTokens, MapLayer.InstitutionMarkers, MapLayer.AgeBanners })
            Assert.Equal(LayerOwner.WorldLens, MapLayerOwnership.OwnerOf(l));
        // Every lens information layer maps to a lens-owned map layer.
        foreach (WorldLayer wl in Enum.GetValues<WorldLayer>())
            Assert.Equal(LayerOwner.WorldLens, MapLayerOwnership.OwnerOf(MapLayerOwnership.MapLayerOf(wl)));
    }

    [Fact]
    public void Lens_DrawsOnlyItsOwnLayers_AndEachLensLayerAtSomeZoom()
    {
        WorldProjection p = Project();
        var drawnSomewhere = new bool[MapLayerOwnership.All.Length];
        foreach (WorldZoom z in Zooms)
        {
            var d = new DrawList();
            LensFrame f = PaintAll(d, p, z);
            int sum = 0;
            foreach (MapLayer l in MapLayerOwnership.All)
            {
                int n = f.LayerDraws[(int)l];
                sum += n;
                if (MapLayerOwnership.OwnerOf(l) == LayerOwner.MapRenderer) Assert.Equal(0, n);
                if (n > 0) drawnSomewhere[(int)l] = true;
            }
            Assert.Equal(d.Commands.Count, sum);   // every command is charged to exactly one layer
        }
        foreach (MapLayer l in MapLayerOwnership.All)
            Assert.Equal(MapLayerOwnership.OwnerOf(l) == LayerOwner.WorldLens, drawnSomewhere[(int)l]);
    }

    [Fact]
    public void TerritoryAndPaths_AreDrawnExactlyOncePerRow_NotDoubled()
    {
        WorldState w = fx.Session.World;
        WorldProjection p = Project();
        Assert.NotEmpty(p.Territory);
        Assert.NotEmpty(p.Roads);
        Assert.Equal(w.CatchmentNodes.Count, p.Territory.Count);
        Assert.Equal(w.NetworkEdges.Count, p.Roads.Count);
        Assert.Equal(0, w.TransportEdges.Count);   // this fixture has the dirt-path baseline only
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            int territory = z == WorldZoom.Settlement ? 0 : w.CatchmentNodes.Count;   // one block per claimed node
            Assert.Equal(territory, f.LayerDraws[(int)MapLayer.Territory]);
            // PIN UPDATED DELIBERATELY (stream U3, roads by class). The Paths layer is now owned piece by
            // piece: every baseline step no built route covers, plus every route piece; the pieces'
            // commands sum to the layer's draw count. With no TransportEdges this reduces EXACTLY to the
            // previous pin — strokes x NetworkEdges.Count (1 dashed stroke at World zoom, casing + path
            // below) — and each NetworkEdges row is drawn once, never doubled.
            int strokes = RoadLens.StyleOf(EdgeTypes.DirtPath, z, MapInk.Default).Count;
            Assert.Equal(z == WorldZoom.World ? 1 : 2, strokes);
            int sum = 0;
            foreach (PathPiece piece in f.Paths) sum += piece.Commands;
            Assert.Equal(f.LayerDraws[(int)MapLayer.Paths], sum);
            Assert.Equal(strokes * w.NetworkEdges.Count, f.LayerDraws[(int)MapLayer.Paths]);
            var edges = new List<int>();
            foreach (PathPiece piece in f.Paths) { Assert.Equal(-1, piece.Route); Assert.Equal(strokes, piece.Commands); edges.Add(piece.NetworkEdge); }
            edges.Sort();
            var want = new List<int>();
            for (int i = 0; i < w.NetworkEdges.Count; i++) want.Add(w.NetworkEdges[i].Id.Value);
            want.Sort();
            Assert.Equal(want, edges);
        }
        // The in-game territory toggle gates the lens's (sole) territory layer.
        Assert.Equal(0, PaintAll(new DrawList(), p, WorldZoom.World, territory: false).LayerDraws[(int)MapLayer.Territory]);
    }

    [Fact]
    public void LegacyMapPass_NoLongerDrawsTerritoryPathsMarkersOrNames()
    {
        string src = File.ReadAllText(Path.Combine(RepoRoot(), "Sim.Ui", "SimUiGame.cs"));
        foreach (string gone in new[] { "BuildTerritoryFills", "BuildPaths", "_territoryVertices", "_pathVertices", "_markerTexture", "AddText(pos" })
            Assert.DoesNotContain(gone, src, StringComparison.Ordinal);
        Assert.Contains("DrawWorldBuffer(_riverVertices)", src, StringComparison.Ordinal);
        Assert.Contains("WorldLens.Paint(", src, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ institutions

    /// <summary>The institution markers state implies: every positive StructureRow of a settlement, and
    /// every institution type <paramref name="source"/> reports for the settlement (the real seam unless a
    /// test passes a stand-in). <c>ResearchCostModifiers</c> rows imply NOTHING (stream U3).</summary>
    private static List<InstitutionMarker> Expected(IReadOnlyWorldState w, SimConfig cfg,
        Func<IReadOnlyWorldState, SimConfig, SettlementId, IReadOnlyList<InstitutionView>> source)
    {
        var r = new List<InstitutionMarker>();
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            for (int k = 0; k < w.Structures.Count; k++)
                if (w.Structures[k].Settlement == id && w.Structures[k].Count > 0)
                    r.Add(new InstitutionMarker(id.Value, "structure:" + w.Structures[k].ProjectId.ToString(CultureInfo.InvariantCulture), w.Structures[k].Count));
            foreach (InstitutionView iv in source(w, cfg, id))
                r.Add(new InstitutionMarker(id.Value, "institution:" + iv.TypeKey.ToString(CultureInfo.InvariantCulture), iv.Count));
        }
        return r;
    }

    private static void AssertMarkers(List<InstitutionMarker> expected, WorldProjection p)
    {
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            if (z == WorldZoom.World) { Assert.Empty(f.Institutions); Assert.Equal(0, f.LayerDraws[(int)MapLayer.InstitutionMarkers]); continue; }
            Comparison<InstitutionMarker> cmp = (a, b) => a.Settlement != b.Settlement ? a.Settlement.CompareTo(b.Settlement) : string.CompareOrdinal(a.Key, b.Key);
            var got = new List<InstitutionMarker>(f.Institutions); got.Sort(cmp);
            var want = new List<InstitutionMarker>(expected); want.Sort(cmp);
            Assert.Equal(want, got);
        }
    }

    [Fact]
    public void InstitutionMarkers_CorrespondExactlyToStateRows()
    {
        WorldState w = fx.Session.World;
        // The real seam: structures only — the ResearchCostModifiers rows (present: the negative
        // control) are research-cost effects, never drawn as universities.
        Assert.Equal(7, w.ResearchCostModifiers.Count);
        List<InstitutionMarker> expected = Expected(w, fx.Session.Config, InstitutionMarkerSource.InstitutionsAt);
        Assert.Equal(3, expected.Count);   // capital: granary, workshop; second: granary
        Assert.Contains(new InstitutionMarker(fx.Capital, "structure:" + fx.Granary.ToString(CultureInfo.InvariantCulture), 2), expected);
        Assert.Contains(new InstitutionMarker(fx.Second, "structure:" + fx.Granary.ToString(CultureInfo.InvariantCulture), 1), expected);
        WorldProjection p = Project();
        AssertMarkers(expected, p);
        Assert.Contains(p.Absent, a => a.StartsWith("Institutions: none founded", StringComparison.Ordinal));
        Assert.DoesNotContain(p.Absent, a => a.StartsWith("Structures", StringComparison.Ordinal));

        // Through the seam (the institutions stream's stand-in): exactly what it reports, at the capital.
        WorldProjection seam = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me, fx.SeamUniversities);
        List<InstitutionMarker> withSeam = Expected(w, fx.Session.Config, fx.SeamUniversities);
        Assert.Equal(3 + 5, withSeam.Count);
        Assert.Contains(new InstitutionMarker(fx.Capital, "institution:3", 2), withSeam);
        AssertMarkers(withSeam, seam);
        Assert.DoesNotContain(seam.Absent, a => a.StartsWith("Institutions", StringComparison.Ordinal));
    }

    [Fact]
    public void SettlementZoom_NamesAndCounts_RegionalZoom_GlyphsOnly()
    {
        WorldProjection p = WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me, fx.SeamUniversities);
        static List<string> Texts(DrawList d) { var r = new List<string>(); foreach (DrawCmd c in d.Commands) if (c is TextCmd t) r.Add(t.Text); return r; }
        var town = new DrawList();
        PaintAll(town, p, WorldZoom.Settlement);
        List<string> t = Texts(town);
        Assert.Contains("granary x2", t);
        Assert.Contains("workshop", t);
        Assert.Contains("Engineering University x2", t);
        foreach (string u in new[] { "Military University", "Medical University", "Natural Science University", "Agricultural University" })
            Assert.Contains(u, t);
        var region = new DrawList();
        PaintAll(region, p, WorldZoom.Regional);
        Assert.DoesNotContain(Texts(region), x => x.Contains("University", StringComparison.Ordinal));
        Assert.NotEqual(0, PaintAll(new DrawList(), p, WorldZoom.Regional).LayerDraws[(int)MapLayer.InstitutionMarkers]);
        // The real seam reports no institution: no university is named at any zoom.
        var real = new DrawList();
        PaintAll(real, Project(), WorldZoom.Settlement);
        Assert.Contains("granary x2", Texts(real));
        Assert.DoesNotContain(Texts(real), x => x.Contains("University", StringComparison.Ordinal));
    }

    [Fact]
    public void UniversityTypes_AreVisuallyDistinct()
    {
        var svgs = new HashSet<string>(StringComparer.Ordinal);
        var inks = new HashSet<string>(StringComparer.Ordinal);
        for (int k = 1; k <= 5; k++)
        {
            var d = new DrawList();
            WorldLens.UniversityGlyph(d, 50, 50, 20, k);
            Assert.True(svgs.Add(SvgWriter.Write(d, 100, 100, null)), $"type {k} glyph duplicates another");
            Assert.True(inks.Add(WorldLens.UniversityInk(k).ToString()), $"type {k} ink duplicates another");
        }
    }

    [Fact]
    public void InstitutionGlyphs_AreEmbeddedInTheFootprint()
    {
        WorldProjection p = WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me, fx.SeamUniversities);
        SettlementLensView cap = Assert.Single(p.Settlements, s => s.Id == fx.Capital);
        // Settlement zoom framing the capital: glyph bodies (rects) of institutions lie inside the footprint circle.
        double scale = 30;
        (double, double) ToScreen(double x, double y) => ((x - cap.X) * scale + 800, (y - cap.Y) * scale + 500);
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.Settlement, ToScreen, scale, new RectD(0, 0, 1600, 1000));
        Assert.Contains(f.Institutions, m => m.Settlement == fx.Capital && m.Key.StartsWith("institution:", StringComparison.Ordinal));
        // The footprint is the first filled circle centred on the capital.
        CircleCmd foot = (CircleCmd)d.Commands.First(c => c is CircleCmd cc && cc.Cx == 800 && cc.Cy == 500 && cc.Fill is not null);
        // Glyph bodies only: dwelling blocks are rects too since the era gate went (stream U3), so count
        // the rects filled with a structure or institution ink — the granary, the workshop body and the
        // five university halls — inside the footprint.
        MapInk ink = MapInk.Default;
        var glyphFills = new HashSet<string>(StringComparer.Ordinal) { ink.Granary.ToString(), ink.Workshop.ToString() };
        for (int k = 1; k <= 5; k++) glyphFills.Add(ink.InstitutionInk(k).ToString());
        var rects = d.Commands.OfType<RectCmd>()
            .Where(r => r.Fill is { } fill && glyphFills.Contains(fill.ToString()))
            .Where(r => Math.Abs(r.Rect.CenterX - 800) < foot.R && Math.Abs(r.Rect.CenterY - 500) < foot.R).ToList();
        Assert.True(rects.Count >= 2 + 5, $"{rects.Count} glyph bodies inside the footprint");
        Assert.Equal(7, f.Institutions.Count(m => m.Settlement == fx.Capital));
    }

    [Fact]
    public void ProjectionAndPaint_AreReadOnly()
    {
        string h = WorldHash.ComputeHex(fx.Session.World);
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms) PaintAll(new DrawList(), p, z);
        Assert.Equal(h, WorldHash.ComputeHex(fx.Session.World));
    }

    /// <summary>Writes the institution-fixture preview SVGs when CIV_INSTITUTION_PREVIEW_OUT names a
    /// directory (docs/architecture/age-and-world-ui/render-institution-preview.sh); otherwise a no-op.
    /// Stream U3: the preview reads the real institution seam, so it shows the structures and no
    /// university (the fixture's ResearchCostModifiers rows are not institutions).</summary>
    [Fact]
    public void Preview_InstitutionFixture_Svg()
    {
        string svg = AgePreview.LensSvg(fx.Session, WorldZoom.Settlement);
        Assert.Contains("granary x2", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("University", svg, StringComparison.Ordinal);
        string? outDir = Environment.GetEnvironmentVariable("CIV_INSTITUTION_PREVIEW_OUT");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "10-institutions-settlement-zoom.svg"), svg);
        File.WriteAllText(Path.Combine(outDir, "11-institutions-regional-zoom.svg"), AgePreview.LensSvg(fx.Session, WorldZoom.Regional));
    }
}
