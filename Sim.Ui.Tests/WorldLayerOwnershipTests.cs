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
/// <see cref="UiSession"/> and played 6 End Turns, then CLONED and given institution state through the
/// very row types the simulation publishes: <see cref="StructureRow"/> (granary x2 + workshop x1 in the
/// player's capital, granary x1 in a second settlement) and <see cref="ResearchCostModifierRow"/> (all
/// five specialized-university types for the player, Engineering twice; one row for a polity that
/// controls nothing). No system writes ResearchCostModifiers yet (ADR-029 §9), so a constructed row is
/// the only way a university exists; nothing here is reachable from production code.
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
        PolityId me = LaborOrderFactory.PlayerEmpire;
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
}

/// <summary>
/// Map layer ownership (docs/architecture/age-and-world-ui.md §5.1): each map layer has exactly one
/// owner and is drawn once; and the settlement-zoom institution markers (structures and the five
/// university types) are drawn from — and exactly match — the state rows.
/// </summary>
public class WorldLayerOwnershipTests(InstitutionWorldFixture fx) : IClassFixture<InstitutionWorldFixture>
{
    private static readonly PolityId Me = LaborOrderFactory.PlayerEmpire;
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
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            int territory = z == WorldZoom.Settlement ? 0 : w.CatchmentNodes.Count;   // one block per claimed node
            int strokes = z == WorldZoom.World ? 1 : 2;                              // dashed network, or casing + road
            Assert.Equal(territory, f.LayerDraws[(int)MapLayer.Territory]);
            Assert.Equal(strokes * w.NetworkEdges.Count, f.LayerDraws[(int)MapLayer.Paths]);
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
    /// every polity's universities (grouped by type, counted by rows) in that polity's capital.</summary>
    private static List<InstitutionMarker> Expected(IReadOnlyWorldState w)
    {
        var r = new List<InstitutionMarker>();
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            for (int k = 0; k < w.Structures.Count; k++)
                if (w.Structures[k].Settlement == id && w.Structures[k].Count > 0)
                    r.Add(new InstitutionMarker(id.Value, "structure:" + w.Structures[k].ProjectId.ToString(CultureInfo.InvariantCulture), w.Structures[k].Count));
            if (!EmpireQuery.TryGetController(w, id, out PolityId pol) || !EmpireQuery.TryGetCapital(w, pol, out SettlementId cap) || cap != id) continue;
            var byType = new SortedDictionary<int, long>();
            for (int k = 0; k < w.ResearchCostModifiers.Count; k++)
                if (w.ResearchCostModifiers[k].Polity == pol)
                    byType[w.ResearchCostModifiers[k].UniversityType] = byType.GetValueOrDefault(w.ResearchCostModifiers[k].UniversityType) + 1;
            foreach (KeyValuePair<int, long> kv in byType)
                r.Add(new InstitutionMarker(id.Value, "university:" + kv.Key.ToString(CultureInfo.InvariantCulture), kv.Value));
        }
        return r;
    }

    [Fact]
    public void InstitutionMarkers_CorrespondExactlyToStateRows()
    {
        WorldState w = fx.Session.World;
        List<InstitutionMarker> expected = Expected(w);
        Assert.Equal(3 + 5, expected.Count);   // capital: granary, workshop + 5 university types; second: granary
        Assert.Contains(new InstitutionMarker(fx.Capital, "university:3", 2), expected);
        Assert.Contains(new InstitutionMarker(fx.Capital, "structure:" + fx.Granary.ToString(CultureInfo.InvariantCulture), 2), expected);
        Assert.Contains(new InstitutionMarker(fx.Second, "structure:" + fx.Granary.ToString(CultureInfo.InvariantCulture), 1), expected);
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
        {
            LensFrame f = PaintAll(new DrawList(), p, z);
            if (z == WorldZoom.World) { Assert.Empty(f.Institutions); Assert.Equal(0, f.LayerDraws[(int)MapLayer.InstitutionMarkers]); continue; }
            Comparison<InstitutionMarker> cmp = (a, b) => a.Settlement != b.Settlement ? a.Settlement.CompareTo(b.Settlement) : string.CompareOrdinal(a.Key, b.Key);
            var got = new List<InstitutionMarker>(f.Institutions); got.Sort(cmp);
            var want = new List<InstitutionMarker>(expected); want.Sort(cmp);
            Assert.Equal(want, got);
        }
        // The legend no longer claims "none founded" / "none built".
        Assert.DoesNotContain(p.Absent, a => a.StartsWith("Universities", StringComparison.Ordinal));
        Assert.DoesNotContain(p.Absent, a => a.StartsWith("Structures", StringComparison.Ordinal));
    }

    [Fact]
    public void SettlementZoom_NamesAndCounts_RegionalZoom_GlyphsOnly()
    {
        WorldProjection p = Project();
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
        WorldProjection p = Project();
        SettlementLensView cap = Assert.Single(p.Settlements, s => s.Id == fx.Capital);
        // Settlement zoom framing the capital: glyph bodies (rects) of institutions lie inside the footprint circle.
        double scale = 30;
        (double, double) ToScreen(double x, double y) => ((x - cap.X) * scale + 800, (y - cap.Y) * scale + 500);
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.Settlement, ToScreen, scale, new RectD(0, 0, 1600, 1000));
        Assert.Contains(f.Institutions, m => m.Settlement == fx.Capital && m.Key.StartsWith("university:", StringComparison.Ordinal));
        // The footprint is the first filled circle centred on the capital.
        CircleCmd foot = (CircleCmd)d.Commands.First(c => c is CircleCmd cc && cc.Cx == 800 && cc.Cy == 500 && cc.Fill is not null);
        var rects = d.Commands.OfType<RectCmd>()
            .Where(r => Math.Abs(r.Rect.CenterX - 800) < foot.R && Math.Abs(r.Rect.CenterY - 500) < foot.R).ToList();
        Assert.True(rects.Count >= 3 + 5);
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
    /// directory (docs/architecture/age-and-world-ui/render-institution-preview.sh); otherwise a no-op.</summary>
    [Fact]
    public void Preview_InstitutionFixture_Svg()
    {
        string svg = AgePreview.LensSvg(fx.Session, WorldZoom.Settlement);
        Assert.Contains("Engineering University x2", svg, StringComparison.Ordinal);
        string? outDir = Environment.GetEnvironmentVariable("CIV_INSTITUTION_PREVIEW_OUT");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "10-institutions-settlement-zoom.svg"), svg);
        File.WriteAllText(Path.Combine(outDir, "11-institutions-regional-zoom.svg"), AgePreview.LensSvg(fx.Session, WorldZoom.Regional));
    }
}
