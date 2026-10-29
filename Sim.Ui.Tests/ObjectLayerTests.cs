using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// M6 E1 — THE OBJECT LAYER (D-038 Parts B, C, G, H; D-043 D2; D-052 §7; D-066 A3/A4; style bible §8):
/// <list type="bullet">
/// <item>E1.1 THE ONE LIGHT — every contact shadow is its caster's silhouette translated by the one constant direction, at
///   the one constant length per height; the shadow ink is cast by the shared painter only; every object polygon is convex;</item>
/// <item>E1.2 COMPOSED PARTS — the parts drawn are exactly what state holds, each at its count stage (boundaries pinned),
///   its number at high zoom only, the overflow fold keeping the most significant (tie-dense, any input order), the
///   scaffold on the lot the built part will take, and no lot moved by any of it;</item>
/// <item>E1.3 TERRAIN — relief, forests and fields are objects of the lens (the substrate pin lives in EraSurfaceTests),
///   baked purely, drawn only on known nodes, and fields only where cultivation is REALIZED;</item>
/// <item>E1.4 THE REGISTER — a pure, monotone function of realized state; the Age changes nothing on the map but the
///   banner (Law 4), and no object painter names the Age;</item>
/// <item>E1.5 SILHOUETTES AND GROUPED TOKENS — the C1 guard extended (convex solids, bounded vertices, never a figure) and
///   D-052 §7's one token per type and place, its number the formations, tie-dense in any table order.</item>
/// </list>
/// </summary>
[Collection("ImGui context")]
public class ObjectLayerTests(InstitutionWorldFixture fx) : IClassFixture<InstitutionWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly WorldZoom[] Zooms = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    private SimConfig Cfg => fx.Session.Config;

    private WorldProjection Seam(IReadOnlyWorldState? w = null, SimConfig? cfg = null) =>
        WorldProjection.Build(w ?? fx.Session.World, cfg ?? Cfg, id => fx.Session.Names.Name(id), Me, fx.SeamUniversities);

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    /// <summary>A hand-built projection of the given settlements (no terrain, no units), the shipped classes.</summary>
    private static WorldProjection Hand(IReadOnlyList<SettlementLensView> settlements, IReadOnlyList<UnitLensView>? units = null) => new()
    {
        WorldSize = 256, KmPerPx = 4, Settlements = settlements, Units = units ?? [], Roads = [], Territory = [],
        PlayerPolity = 1, PlayerAge = 1, PlayerAgeName = "", Absent = [],
    };

    private static SettlementLensView View(int id, IReadOnlyList<StructureView>? structures = null, IReadOnlyList<InstitutionView>? institutions = null,
        long population = 2000, long dwellings = 300, int route = 0, IReadOnlyList<string>? building = null, int age = 1) =>
        new(id, "S" + id.ToString(CultureInfo.InvariantCulture), 128.5, 128.5, 1, false, true, population, dwellings, 2, age,
            structures ?? [], null, 0, 0, 0, institutions ?? [], null, true, route, building);

    /// <summary>Paints <paramref name="p"/> centred on (128.5, 128.5) at <paramref name="scale"/> px per world px, at the band
    /// <paramref name="z"/>'s plateau.</summary>
    private static (DrawList D, LensFrame F) Paint(WorldProjection p, WorldZoom z, double scale, MapInk? ink = null, int selectedUnit = -1)
    {
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, z, (x, y) => ((x - 128.5) * scale + 800, (y - 128.5) * scale + 500),
            scale, new RectD(0, 0, 1600, 1000), ink: ink, selectedUnit: selectedUnit);
        return (d, f);
    }

    // ================================================================== E1.1 the one light

    /// <summary>Every object the layer can draw, painted into one list: each part kind at each count stage in each register
    /// rung, the scaffold alone and beside, the overflow part, the dwelling block in each rung, every terrain mark kind, a
    /// field, every identity's token (alone and grouped) — and the lens over the institution fixture (every university hall,
    /// structures, dwellings, relief and forests of the real terrain, the Warband) at Regional and deep Settlement zoom.</summary>
    private List<DrawList> Scene()
    {
        MapInk k = MapInk.Default;
        var lists = new List<DrawList>();
        var d = new DrawList();
        var specs = new List<PartSpec>
        {
            new(false, 1, "granary", StructureKind.Storage), new(false, 2, "workshop", StructureKind.Production),
            new(false, 9, "x", StructureKind.Other), new(true, 6, "Law court", StructureKind.Other), new(true, 77, "Guild", StructureKind.Other),
        };
        for (int t = 1; t <= 5; t++) specs.Add(new PartSpec(true, t, "University", StructureKind.Other));
        foreach (PartSpec spec in specs)
            for (int reg = 0; reg < 4; reg++)
                for (int stage = 0; stage < 4; stage++)
                    foreach (double g in new[] { 4.0, 9.0, 30.0 })
                        WorldLens.PartGlyph(d, 300, 300, g, spec, stage, (ObjectRegister)reg, k);
        foreach (double g in new[] { 4.0, 30.0 })
        {
            WorldLens.ScaffoldGlyph(d, 100, 100, g, false, k);
            WorldLens.ScaffoldGlyph(d, 100, 100, g, true, k);
            for (int reg = 0; reg < 4; reg++) WorldLens.OverflowGlyph(d, 100, 100, g, (ObjectRegister)reg, k);
        }
        for (int reg = 0; reg < 4; reg++)
            foreach (double bw in new[] { 3.0, 8.0, 24.0 }) WorldLens.DwellingBlock(d, 50, 50, bw, bw * 0.8, (ObjectRegister)reg, k);
        int cell = 1;
        foreach (TerrainMarkKind m in new[] { TerrainMarkKind.Hill, TerrainMarkKind.Mountain, TerrainMarkKind.Peak, TerrainMarkKind.Forest })
            foreach (double pitch in new[] { 12.0, 30.0, 44.0 }) TerrainMarks.Mark(d, k, m, 400, 400, pitch, cell++);
        UnitFamilyContent fam = Cfg.UnitFamilies!;
        foreach (UnitFamily f in fam.Families)
            foreach (UnitIdentity ident in f.Line)
                foreach (double s in new[] { 9.0, 12.0, 30.0 })
                    WorldLens.FormationToken(d, 600, 600, s, UnitEmblem.Of(fam, f.Key, ident.Key), k.PolityOf(2, 1), k, 3);
        lists.Add(d);
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(x => x.Id == fx.Capital);
        foreach ((WorldZoom z, double scale) in new[] { (WorldZoom.Regional, 4.0), (WorldZoom.Settlement, 30.0) })
        {
            var lens = new DrawList();
            WorldLens.Paint(lens, ApproxTextMeasure.Instance, p, z, (x, y) => ((x - cap.X) * scale + 800, (y - cap.Y) * scale + 500), scale,
                new RectD(0, 0, 1600, 1000));
            lists.Add(lens);
        }
        return lists;
    }

    /// <summary>
    /// THE SINGLE-LIGHT INVARIANT (D-038 B1/B2, H4: "every part must be authored against the same light angle and the same
    /// ground plane"): every contact shadow in the scene is immediately followed by its CASTER with the same number of
    /// vertices; the shadow is the caster translated by ONE vector (every vertex alike); that vector's direction IS the
    /// constant (<see cref="ObjectLight.ShadowDirX"/>, <see cref="ObjectLight.ShadowDirY"/>) and its length IS
    /// <see cref="ObjectLight.ShadowLength"/> × the caster's height. A painter that cast its own direction or length fails.
    /// </summary>
    [Fact]
    public void EveryShadowOffset_IsTheOneLightsConstant_AppliedToItsCaster()
    {
        MapInk k = MapInk.Default;
        Rgba shadow = ObjectPaint.ShadowInk(k);
        int shadows = 0;
        foreach (DrawList d in Scene())
            for (int i = 0; i < d.Commands.Count; i++)
            {
                // A contact shadow is the shadow ink at ANY opacity (it fades in with its caster's size and opacity —
                // ObjectPaint.Solid); every one is checked.
                if (d.Commands[i] is not PolygonCmd s || (s.Fill.R, s.Fill.G, s.Fill.B) != (shadow.R, shadow.G, shadow.B) || s.Fill.A == 0) continue;
                shadows++;
                PolygonCmd caster = Assert.IsType<PolygonCmd>(d.Commands[i + 1]);
                Assert.NotEqual(shadow, caster.Fill);
                Assert.Equal(caster.Points.Length, s.Points.Length);
                double dx = s.Points[0].X - caster.Points[0].X, dy = s.Points[0].Y - caster.Points[0].Y;
                for (int v = 1; v < s.Points.Length; v++)
                {
                    Assert.True(Math.Abs(s.Points[v].X - caster.Points[v].X - dx) < 1e-9, "a shadow that is not a translation");
                    Assert.True(Math.Abs(s.Points[v].Y - caster.Points[v].Y - dy) < 1e-9, "a shadow that is not a translation");
                }
                double len = Math.Sqrt(dx * dx + dy * dy);
                Assert.True(len > 0);
                Assert.True(Math.Abs(dx / len - ObjectLight.ShadowDirX) < 1e-9 && Math.Abs(dy / len - ObjectLight.ShadowDirY) < 1e-9,
                    $"shadow direction ({dx / len}, {dy / len}) is not the one light's");
                double h = caster.Points.Max(q => q.Y) - caster.Points.Min(q => q.Y);
                Assert.True(Math.Abs(len - ObjectLight.ShadowLength * h) < 1e-9 * Math.Max(1, h), $"shadow length {len} for height {h}");
            }
        Assert.True(shadows > 1000, shadows + " shadows in the scene");
        // The constant itself: from the north-west, falling east-south-east, never varied.
        Assert.Equal(315.0, ObjectLight.FromDeg);
        Assert.True(ObjectLight.ShadowDirX > 0 && ObjectLight.ShadowDirY > 0);
        Assert.Equal(1.0, ObjectLight.ShadowDirX * ObjectLight.ShadowDirX + ObjectLight.ShadowDirY * ObjectLight.ShadowDirY, 12);
    }

    /// <summary>Every polygon the object layer emits is CONVEX (the ImGui backend fills convex polygons only — a concave
    /// one would draw wrong in the game while looking right in an SVG), and none is degenerate in a way the backend refuses.</summary>
    [Fact]
    public void EveryObjectPolygon_IsConvex()
    {
        int n = 0;
        // The lens's two pre-E1 marks that are not objects — the capital's star and the banner's swallowtail cloth (P13's
        // hand) — are not this layer's; every other polygon is.
        MapInk k = MapInk.Default;
        var notObjects = new HashSet<Rgba>(k.Polities) { k.CapitalMark, k.PlayerPolity, k.NoPolity };
        foreach (DrawList d in Scene())
            foreach (PolygonCmd g in d.Commands.OfType<PolygonCmd>().Where(g => !notObjects.Contains(g.Fill)))
            {
                Assert.True(ObjectPaint.IsConvex(g.Points), "a concave polygon: " + string.Join(" ", g.Points));
                n++;
            }
        Assert.True(n > 1000);
    }

    /// <summary>ONE SHARED PAINTER CASTS SHADOWS (source guard): no file of the world layer but <c>ObjectLight.cs</c> names
    /// the light's direction or offset, and the shadow ink is named only where it is defined (MapInk) and cast
    /// (ObjectPaint.ShadowInk) — so no part can carry a light of its own.</summary>
    [Fact]
    public void TheLight_IsStatedOnce_AndShadowsAreCastOnlyByTheSharedPainter_SourceGuard()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot(), "Sim.Ui", "World"), "*.cs"))
        {
            string name = Path.GetFileName(file), src = File.ReadAllText(file);
            if (name is "ObjectLight.cs") continue;
            // MapInk.cs names the shadow's opacity in its token's doc; the opacity itself is applied only in ObjectLight.cs.
            foreach (string banned in name == "MapInk.cs" ? new[] { "ShadowDirX", "ShadowDirY", "ShadowOffset(" } : new[] { "ShadowDirX", "ShadowDirY", "ShadowOffset(", "ShadowAlpha" })
                Assert.DoesNotContain(banned, src, StringComparison.Ordinal);
            if (name != "MapInk.cs") Assert.DoesNotContain("ObjectShadow", src, StringComparison.Ordinal);
        }
        // The object inks are the style bible's own hexes (the stops derive from them; no new hue).
        MapInk k = MapInk.Default;
        foreach (Rgba c in new[] { k.ReliefUpland, k.ReliefHill, k.ReliefPeak, k.Forest, k.FieldCrop, k.FieldFallow, k.Scaffold }
                     .Concat(k.RegisterWalls).Concat(k.RegisterRoofs))
            Assert.Contains(c, Sim.Ui.Art.ParchmentPalette.BibleColors);
    }

    // ================================================================== E1.2 parts from state, stages, numbers, fold

    /// <summary>
    /// PARTS FROM STATE on a POPULATED fixture (two structure kinds, the five university types through the seam, one of them
    /// twice): the parts the lens draws are exactly the kinds the settlement holds — a marker and a placement each, in the
    /// view's order — and each part paints exactly the buildings of its count's stage (its kind's wall ink, one front wall per
    /// building). A settlement with nothing built draws no part.
    /// </summary>
    [Fact]
    public void ThePartsDrawn_AreExactlyWhatStateHolds_EachAtItsCountsStage()
    {
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(x => x.Id == fx.Capital);
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.Settlement, (x, y) => ((x - cap.X) * 30 + 800, (y - cap.Y) * 30 + 500), 30,
            new RectD(0, 0, 1600, 1000));
        var want = cap.Structures.Select(x => ("structure:" + x.ProjectId.ToString(CultureInfo.InvariantCulture), x.Count))
            .Concat(cap.Institutions.Select(x => ("institution:" + x.TypeKey.ToString(CultureInfo.InvariantCulture), x.Count))).ToList();
        Assert.Equal(2 + 5, want.Count);
        Assert.Equal(want, f.Institutions.Where(m => m.Settlement == fx.Capital).Select(m => (m.Key, m.Count)).ToList());
        Assert.Equal(want.Select(w => w.Item1), f.Parts.Where(x => x.Settlement == fx.Capital).Select(x => x.Key));
        foreach (SettlementLensView s in p.Settlements.Where(s => s.Structures.Count + s.Institutions.Count == 0))
            Assert.DoesNotContain(f.Parts, x => x.Settlement == s.Id);
        _ = d;

        // Each count's stage, through the lens, on one settlement holding one kind (the granary).
        MapInk k = MapInk.Default;
        PartConventions c = SettlementClasses.Default.Parts;
        foreach (long count in new long[] { 1, 2, 9, 10, 11, 19, 20, 21, 29, 30, 31, 500 })
        {
            WorldProjection one = Hand([View(1, [new StructureView(5, "granary", StructureKind.Storage, count)])]);
            (DrawList dd, LensFrame ff) = Paint(one, WorldZoom.Settlement, 30);
            Assert.Equal([("structure:5", count)], ff.Institutions.Select(m => (m.Key, m.Count)).ToList());
            int walls = dd.Commands.OfType<PolygonCmd>().Count(g => g.Fill == k.Granary);
            // The part's buildings plus the key's one small granary (Settlement zoom names each kind beside the footprint).
            Assert.Equal(WorldLens.BuildingsAt(c.StageOf(count)) + 1, walls);
        }
    }

    /// <summary>D-043 D2'S STAGE BOUNDARIES, data-defined: 1 small, 10 expanded, 20 campus, 30 complex — each threshold the
    /// first count of its stage, the count below it the stage before; below 1 nothing. The four layouts draw 1, 2, 3 and 5
    /// buildings (each a front wall in the kind's ink), strictly more at each stage.</summary>
    [Fact]
    public void TheCountStages_SwitchExactlyAtTheirThresholds()
    {
        PartConventions c = SettlementClasses.Default.Parts;
        Assert.Equal(new long[] { 1, 10, 20, 30 }, c.CountStages);
        foreach ((long count, int stage) in new (long, int)[] { (0, -1), (-5, -1), (1, 0), (9, 0), (10, 1), (19, 1), (20, 2), (29, 2), (30, 3), (31, 3), (long.MaxValue, 3) })
            Assert.Equal(stage, c.StageOf(count));
        MapInk k = MapInk.Default;
        int previous = 0;
        for (int stage = 0; stage < 4; stage++)
        {
            var d = new DrawList();
            WorldLens.PartGlyph(d, 0, 0, 30, new PartSpec(true, 3, "Engineering", StructureKind.Other), stage, ObjectRegister.Masonry, k);
            int walls = d.Commands.OfType<PolygonCmd>().Count(g => g.Fill == k.InstitutionInk(3));
            Assert.Equal(WorldLens.BuildingsAt(stage), walls);
            Assert.True(walls > previous);
            previous = walls;
        }
    }

    /// <summary>The parts section is data, validated: a malformed one is refused naming the field, never defaulted.</summary>
    [Fact]
    public void ThePartConventions_AreDataDefined_AndAMalformedSectionIsRefused()
    {
        string json;
        using (Stream s = typeof(SettlementClasses).Assembly.GetManifestResourceStream(SettlementClasses.ResourceName)!)
        using (var r = new StreamReader(s)) json = r.ReadToEnd();
        Assert.Equal(8, SettlementClasses.Parse(json).Parts.MaxParts);
        Assert.Equal(5, SettlementClasses.Parse(json.Replace("\"maxParts\": 8", "\"maxParts\": 5", StringComparison.Ordinal)).Parts.MaxParts);
        foreach ((string from, string to, string field) in new[]
                 {
                     ("[1, 10, 20, 30]", "[1, 10, 20]", "countStages"), ("[1, 10, 20, 30]", "[2, 10, 20, 30]", "countStages[0]"),
                     ("[1, 10, 20, 30]", "[1, 20, 10, 30]", "ascend"), ("\"maxParts\": 8", "\"maxParts\": 1", "maxParts"),
                     ("\"maxParts\": 8", "\"maxParts\": 15", "maxParts"), ("\"maxParts\": 8", "\"maxPartz\": 8", "maxParts"),
                 })
        {
            InvalidDataException e = Assert.Throws<InvalidDataException>(() => SettlementClasses.Parse(json.Replace(from, to, StringComparison.Ordinal)));
            Assert.Contains(field, e.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>THE NUMBER AT HIGH ZOOM (D-043 D2): a part counted above one shows its number on a tag only at the
    /// Settlement band and only once its glyph is <see cref="WorldLens.CountTagMinGlyphPx"/> wide; a part of one never does;
    /// the Regional band never does.</summary>
    [Fact]
    public void APartsNumber_IsDrawnAtHighZoomOnly()
    {
        WorldProjection p = Hand([View(1, [new StructureView(5, "granary", StructureKind.Storage, 12), new StructureView(6, "workshop", StructureKind.Production, 1)])]);
        double unitScale = SettlementClasses.Default.Lattice.WorldPxPerUnit;
        bool Tag(WorldZoom z, double scale) => Paint(p, z, scale).D.Commands.OfType<TextCmd>().Any(t => t.Text == "12");
        bool AnyOne(WorldZoom z, double scale) => Paint(p, z, scale).D.Commands.OfType<TextCmd>().Any(t => t.Text == "1");
        Assert.True(Tag(WorldZoom.Settlement, WorldLens.CountTagMinGlyphPx * unitScale + 0.1));
        Assert.False(Tag(WorldZoom.Settlement, (WorldLens.CountTagMinGlyphPx - 0.5) * unitScale));
        Assert.False(Tag(WorldZoom.Regional, 40));
        Assert.False(AnyOne(WorldZoom.Settlement, 40));
    }

    /// <summary>
    /// THE OVERFLOW FOLD (D-066 A4), TIE-DENSE: a settlement holding ten kinds — every count equal, so significance is decided
    /// by the integer tie-breaks alone (institutions first, then the lower key) — shows maxParts − 1 = 7 and folds 3 into the
    /// overflow part (its marker counts what was folded), in every one of five input orders; the kept kinds are exactly the
    /// composite-key order's first seven; with one count raised, that kind is kept over an equal-count lower key.
    /// </summary>
    [Fact]
    public void TheFold_KeepsTheMostSignificant_TieDense_WhateverTheOrder()
    {
        var st = new List<StructureView>();
        for (int i = 0; i < 4; i++) st.Add(new StructureView(10 + i, "s" + i.ToString(CultureInfo.InvariantCulture), StructureKind.Other, 3));
        var iv = new List<InstitutionView>();
        for (int i = 0; i < 6; i++) iv.Add(new InstitutionView(20 + i, "i" + i.ToString(CultureInfo.InvariantCulture), 3));
        PartConventions c = SettlementClasses.Default.Parts;
        string[] expectKept = ["institution:20", "institution:21", "institution:22", "institution:23", "institution:24", "institution:25", "structure:10"];
        int[][] orders = [[0, 1, 2, 3], [3, 2, 1, 0], [2, 0, 3, 1], [1, 3, 0, 2], [3, 0, 2, 1]];
        int[][] iorders = [[0, 1, 2, 3, 4, 5], [5, 4, 3, 2, 1, 0], [2, 5, 0, 3, 1, 4], [4, 0, 5, 1, 3, 2], [1, 2, 3, 4, 5, 0]];
        for (int o = 0; o < orders.Length; o++)
        {
            SettlementLensView v = View(1, [.. orders[o].Select(i => st[i])], [.. iorders[o].Select(i => iv[i])]);
            List<ShownPart> shown = PartFold.Shown(v, c, out int foldedKinds, out long foldedCount);
            Assert.Equal(7, shown.Count);
            Assert.Equal(expectKept.OrderBy(x => x, StringComparer.Ordinal), shown.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(3, foldedKinds);
            Assert.Equal(9, foldedCount);
            (_, LensFrame f) = Paint(Hand([v]), WorldZoom.Settlement, 40);
            Assert.Equal(8, f.Institutions.Count);
            Assert.Equal(new InstitutionMarker(1, WorldLens.OverflowKey, 9), f.Institutions[^1]);
            Assert.Equal(8, f.Parts.Count);
        }
        // A raised count wins over an equal-kind lower key; at the limit exactly, nothing folds.
        st[3] = st[3] with { Count = 4 };
        List<ShownPart> raised = PartFold.Shown(View(1, st, iv), c, out _, out _);
        Assert.Contains(raised, x => x.Key == "structure:13");
        Assert.DoesNotContain(raised, x => x.Key == "structure:10");
        List<ShownPart> all = PartFold.Shown(View(1, st.Take(2).ToList(), iv), c, out int none, out long zero);
        Assert.Equal(8, all.Count);
        Assert.Equal((0, 0L), (none, zero));
    }

    /// <summary>
    /// NOTHING MOVES: a part's lot is the same whatever else the settlement holds, folds or builds — the overflow part and the
    /// scaffolds claim AFTER every kind, so for 64 settlements holding every content kind, adding a scaffold or an overflow
    /// claim moves no part's lot; the overflow lot is its own (no part stands on it); and a scaffold stands on the very lot the
    /// built part takes.
    /// </summary>
    [Fact]
    public void TheOverflowAndTheScaffolds_MoveNoPart_AndAScaffoldStandsWhereItsPartWill()
    {
        PartUniverse u = PartUniverse.Of(Cfg);
        SettlementClasses classes = SettlementClasses.Default;
        var st = u.StructureProjects.Select(id => new StructureView(id, "s", StructureKind.Other, 1)).ToList();
        var iv = u.InstitutionTypes.Select(t => new InstitutionView(t, "i", 1)).ToList();
        for (int id = 0; id < 64; id++)
        {
            SettlementComposition plain = SettlementComposer.Compose(View(id, st, iv), u, classes);
            SettlementComposition queued = SettlementComposer.Compose(View(id, st, iv, building: ["structure:" + u.StructureProjects[0].ToString(CultureInfo.InvariantCulture), "institution:99"]), u, classes);
            Assert.Equal(plain.Parts.Select(x => x.Lot), queued.Parts.Select(x => x.Lot));
            Assert.Equal(plain.Blocks, queued.Blocks);
            Assert.DoesNotContain(plain.Parts, x => x.Lot == plain.OverflowLot);
            // The queued kind the settlement already holds: its scaffold is on its part's lot.
            Assert.Equal(plain.Parts[0].Lot, queued.Scaffolds.Single(x => x.Key == plain.Parts[0].Key).Lot);
            // A kind not held: the scaffold's lot is the one it takes once built (holding it).
            SettlementComposition empty = SettlementComposer.Compose(View(id, [], [], building: ["institution:3"]), u, classes);
            SettlementComposition built = SettlementComposer.Compose(View(id, [], [new InstitutionView(3, "i", 1)]), u, classes);
            Assert.Equal(built.Parts.Single().Lot, empty.Scaffolds.Single().Lot);
        }
    }

    /// <summary>
    /// UNDER CONSTRUCTION, from state: a queued university project at the capital and a queued granary at a settlement that
    /// already has one put their part keys in the projection (structures first, ascending) — and the lens draws a scaffold
    /// for each (its lit wall in the scaffold ink), while neither adds a part or a marker (only what is built is a part, P12a).
    /// </summary>
    [Fact]
    public void AQueuedProject_DrawsAScaffold_NeverAPart()
    {
        WorldState w = fx.Session.World.Clone();
        ConstructionProjectEntry uni = InstitutionContent.ProjectOfType(Cfg, 1)!;
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(fx.Capital), 1, uni.Id));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(fx.Second), 1, fx.Granary));
        WorldProjection p = WorldProjection.Build(w, Cfg, id => fx.Session.Names.Name(id), Me);
        Assert.Equal(["institution:1"], p.Settlements.Single(s => s.Id == fx.Capital).Building);
        Assert.Equal(["structure:" + fx.Granary.ToString(CultureInfo.InvariantCulture)], p.Settlements.Single(s => s.Id == fx.Second).Building);
        WorldProjection before = WorldProjection.Build(fx.Session.World, Cfg, id => fx.Session.Names.Name(id), Me);
        Assert.All(before.Settlements, s => Assert.Null(s.Building));
        MapInk k = MapInk.Default;
        Rgba scaffold = MapInk.Lit(k.Scaffold);
        foreach (int at in new[] { fx.Capital, fx.Second })
        {
            SettlementLensView s = p.Settlements.Single(x => x.Id == at);
            var d = new DrawList();
            LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.Settlement, (x, y) => ((x - s.X) * 30 + 800, (y - s.Y) * 30 + 500), 30,
                new RectD(0, 0, 1600, 1000));
            var d0 = new DrawList();
            LensFrame f0 = WorldLens.Paint(d0, ApproxTextMeasure.Instance, before, WorldZoom.Settlement, (x, y) => ((x - s.X) * 30 + 800, (y - s.Y) * 30 + 500), 30,
                new RectD(0, 0, 1600, 1000));
            Assert.Equal(f0.Institutions, f.Institutions);
            Assert.Equal(f0.Parts, f.Parts);
            Assert.Equal(0, d0.Commands.OfType<LineCmd>().Count(l => l.Color == k.Scaffold));
            Assert.True(d.Commands.OfType<LineCmd>().Count(l => l.Color == k.Scaffold) >= 3, "no scaffold drawn at " + at);
        }
        // The granary's settlement already has one: the scaffold stands beside it (poles and brace only, no wall).
        Assert.Contains(p.Settlements.Single(x => x.Id == fx.Second).Structures, x => x.ProjectId == fx.Granary);
        Assert.Contains(p.Settlements.Single(x => x.Id == fx.Capital).Building!, x => x == "institution:1");
        _ = scaffold;
    }

    // ================================================================== E1.3 terrain

    /// <summary>THE TERRAIN MARKS: baked purely (two bakes of one terrain equal, cell for cell), from the immutable rasters
    /// only; the canonical-size fixture world carries every kind; marks are drawn ONLY on nodes the observer knows (a stub
    /// knowing the western half: no mark's point east of it), and the World band draws none.</summary>
    [Fact]
    public void TheTerrainMarks_ArePure_AndDrawnOnlyOnKnownNodes_FromTheRegionalBandIn()
    {
        IReadOnlyWorldState w = fx.Session.World;
        TerrainMarks a = TerrainMarks.Bake(w.Terrain!), b = TerrainMarks.Bake(w.Terrain!);
        for (int y = 0; y < a.Cells; y++) for (int x = 0; x < a.Cells; x++) Assert.Equal(a.KindAt(x, y), b.KindAt(x, y));
        Assert.Same(TerrainMarks.Of(w.Terrain!), TerrainMarks.Of(w.Terrain!));
        int[] census = a.Census();
        Assert.True(census[1] + census[2] + census[3] > 0 && census[4] > 0, string.Join(",", census));

        WorldProjection all = WorldProjection.Build(w, Cfg, id => fx.Session.Names.Name(id), Me);
        Assert.Equal(0, PaintAll(all, WorldZoom.World).F.LayerDraws[(int)MapLayer.ObjectTerrain]);
        Assert.True(PaintAll(all, WorldZoom.Regional).F.LayerDraws[(int)MapLayer.ObjectTerrain] > 0);

        int lsize = all.LatticeSize;
        var west = new bool[lsize * lsize];
        for (int n = 0; n < west.Length; n++) west[n] = n % lsize < lsize / 2;
        WorldProjection half = WorldProjection.Build(w, Cfg, id => fx.Session.Names.Name(id), Me, knowledge: KnowledgeStubs.Over(w, Me, west));
        (_, LensFrame f) = PaintAll(half, WorldZoom.Regional);
        Assert.True(f.LayerDraws[(int)MapLayer.ObjectTerrain] > 0);
        Assert.True(f.LayerDraws[(int)MapLayer.ObjectTerrain] < PaintAll(all, WorldZoom.Regional).F.LayerDraws[(int)MapLayer.ObjectTerrain]);
        // Every mark the object layer's terrain painter draws lies west of the known half's edge: a mark stands on the node
        // its place is on (a mark's jitter and half-width allowed: less than a cell).
        var marks = new DrawList();
        TerrainMarks.Paint(marks, MapInk.Default, half, (c, x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024), [0], half.WorldSize);
        Assert.NotEmpty(marks.Commands);
        double edge = lsize / 2 * all.Stride * 4.0;   // screen px of the half's edge (4 px per world px)
        foreach (PolygonCmd g in marks.Commands.OfType<PolygonCmd>())
            Assert.True(g.Points.Min(q => q.X) < edge, "a terrain mark east of the known half");
        Assert.Contains(marks.Commands.OfType<PolygonCmd>(), g => g.Points.Max(q => q.X) > edge - 3 * TerrainMarks.PitchPx * 4);
    }

    private static (DrawList D, LensFrame F) PaintAll(WorldProjection p, WorldZoom z)
    {
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
        return (d, f);
    }

    /// <summary>
    /// LAW 4 FOR FIELDS — a field is drawn only where cultivation is REALIZED: under the shipped config the fixture's
    /// settlements harvest wild food (cultivation not yet theirs: LabourActivities.HarvestsWildFood), so no field is laid;
    /// under the same world with the pre-cultivation switch off (cultivation the economy's from the start) the cultivating
    /// settlements lay fields — each on a known catchment node of its own, on fertile ground, at most its farming share.
    /// </summary>
    [Fact]
    public void Fields_AreLaidOnlyWhereCultivationIsRealized()
    {
        IReadOnlyWorldState w = fx.Session.World;
        Assert.True(Cfg.Farming.PreCultivation is { Enabled: true });
        for (int i = 0; i < w.Settlements.Count; i++) Assert.True(LabourActivities.HarvestsWildFood(w, Cfg, w.Settlements[i].Id));
        Assert.Empty(WorldProjection.Build(w, Cfg, id => fx.Session.Names.Name(id), Me).Fields);

        SimConfig cultivated = Cfg with { Farming = Cfg.Farming with { PreCultivation = Cfg.Farming.PreCultivation! with { Enabled = false } } };
        WorldProjection p = WorldProjection.Build(w, cultivated, id => fx.Session.Names.Name(id), Me);
        Assert.NotEmpty(p.Fields);
        foreach (IGrouping<int, FieldMark> bySettlement in p.Fields.GroupBy(x => x.Settlement))
        {
            var nodes = new HashSet<int>();
            for (int c = 0; c < w.CatchmentNodes.Count; c++) if (w.CatchmentNodes[c].Settlement.Value == bySettlement.Key) nodes.Add(w.CatchmentNodes[c].LatticeNode);
            Assert.All(bySettlement, f => Assert.Contains(f.Node, nodes));
            Assert.True(bySettlement.Count() <= Math.Ceiling(Sectors.Share(Sectors.Default(new SettlementId(bySettlement.Key)), Sectors.Farming) * nodes.Count));
        }
    }

    // ================================================================== E1.4 the register

    /// <summary>THE REGISTER FROM REALIZED STATE: each rung earned by the state its doc names, and MONOTONE — over a grid of
    /// structures 0..12, institutions 0..25 and achieved route classes 0..6, raising any input never lowers the register.</summary>
    [Fact]
    public void TheRegister_IsAMonotoneFunctionOfRealizedState()
    {
        Assert.Equal(ObjectRegister.Earthen, StyleRegister.Of(0, 0, 0));
        Assert.Equal(ObjectRegister.Earthen, StyleRegister.Of(0, 0, EdgeTypes.DirtPath));
        Assert.Equal(ObjectRegister.Timber, StyleRegister.Of(1, 0, 0));
        Assert.Equal(ObjectRegister.Timber, StyleRegister.Of(0, 0, EdgeTypes.Trackway));
        Assert.Equal(ObjectRegister.Timber, StyleRegister.Of(StyleRegister.MasonryStructures - 1, 0, 0));
        Assert.Equal(ObjectRegister.Masonry, StyleRegister.Of(StyleRegister.MasonryStructures, 0, 0));
        Assert.Equal(ObjectRegister.Masonry, StyleRegister.Of(0, 1, 0));
        Assert.Equal(ObjectRegister.Masonry, StyleRegister.Of(0, 0, EdgeTypes.BuiltRoad));
        Assert.Equal(ObjectRegister.Masonry, StyleRegister.Of(0, 0, EdgeTypes.PavedRoad));
        Assert.Equal(ObjectRegister.Masonry, StyleRegister.Of(0, StyleRegister.EngineeredInstitutions - 1, 0));
        Assert.Equal(ObjectRegister.Engineered, StyleRegister.Of(0, StyleRegister.EngineeredInstitutions, 0));
        Assert.Equal(ObjectRegister.Engineered, StyleRegister.Of(0, 0, EdgeTypes.MacadamRoad));
        Assert.Equal(ObjectRegister.Engineered, StyleRegister.Of(0, 0, EdgeTypes.Highway));
        for (long st = 0; st <= 12; st++)
            for (long inst = 0; inst <= 25; inst++)
                for (int route = 0; route <= 6; route++)
                {
                    ObjectRegister r = StyleRegister.Of(st, inst, route);
                    Assert.True(StyleRegister.Of(st + 1, inst, route) >= r);
                    Assert.True(StyleRegister.Of(st, inst + 1, route) >= r);
                    if (route < 6) Assert.True(StyleRegister.Of(st, inst, route + 1) >= r);
                }
        // The view form reads exactly those three inputs.
        SettlementLensView v = View(1, [new StructureView(5, "granary", StructureKind.Storage, 4), new StructureView(6, "w", StructureKind.Other, 6)],
            [new InstitutionView(1, "u", 2)], route: EdgeTypes.Trackway);
        Assert.Equal(StyleRegister.Of(10, 2, EdgeTypes.Trackway), StyleRegister.Of(v));
    }

    /// <summary>
    /// THE ROUTE INPUT IS THE ACHIEVED CLASS, never the class being built toward (a capability under way draws nothing,
    /// Law 4): on the road fixture — every class, one route left part-way — each settlement's input is the best EdgeType of
    /// a drawn route ending at it, and the partial route's TARGET class is not it.
    /// </summary>
    [Fact]
    public void TheRegistersRouteInput_IsTheClassAchieved_NotTheClassBuiltToward()
    {
        var road = new RoadWorldFixture();
        WorldProjection p = WorldProjection.Build(road.Session.World, road.Session.Config, id => road.Session.Names.Name(id), Me);
        Assert.NotEmpty(p.Routes);
        foreach (SettlementLensView s in p.Settlements)
        {
            int want = 0;
            foreach (RouteLensView r in p.Routes) if (r.A == s.Id || r.B == s.Id) want = Math.Max(want, r.EdgeType);
            Assert.Equal(want, s.TopRouteClass);
        }
        RouteLensView partial = p.Routes.Single(r => r.Id == road.PartialRoute);
        Assert.True(partial.TargetClass > partial.EdgeType);
    }

    /// <summary>
    /// LAW 4 — THE AGE BANNER IS THE ONLY AGE-KEYED ELEMENT (D-066 A3; D-038 H5): the institution fixture's projection with
    /// every settlement's Age and the player's Age set to each of the nine Ages paints, at every zoom and in one ink, the SAME
    /// commands once the banners (pole, cloth, numeral) are set aside — object layer, parts, register, terrain and tokens
    /// alike. And no file of the object layer names the Age (source guard, comments aside).
    /// </summary>
    [Fact]
    public void TheAge_ChangesNothingOnTheMapButTheBanner()
    {
        WorldProjection p0 = Seam();
        foreach (WorldZoom z in Zooms)
        {
            List<string>? first = null;
            for (int age = 1; age <= 9; age++)
            {
                WorldProjection pa = new()
                {
                    WorldSize = p0.WorldSize, WrapX = p0.WrapX, KmPerPx = p0.KmPerPx, Units = p0.Units, Roads = p0.Roads, Routes = p0.Routes,
                    Territory = p0.Territory, Fog = p0.Fog, PlayerPolity = p0.PlayerPolity, Absent = p0.Absent, Universe = p0.Universe,
                    Classes = p0.Classes, Terrain = p0.Terrain, KnownNodes = p0.KnownNodes, LatticeSize = p0.LatticeSize, Stride = p0.Stride,
                    Fields = p0.Fields, PlayerAge = age, PlayerAgeName = "Age " + age.ToString(CultureInfo.InvariantCulture),
                    Settlements = [.. p0.Settlements.Select(s => s with { Age = s.Controller >= 0 ? age : 0 })],
                };
                var d = new DrawList();
                WorldLens.Paint(d, ApproxTextMeasure.Instance, pa, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
                List<string> stripped = WithoutBanners(d.Commands);
                if (first is null) first = stripped;
                else Assert.Equal(first, stripped);
            }
        }
        string[] objectFiles = ["ObjectLight.cs", "PartManifest.cs", "StyleRegister.cs", "TerrainMarks.cs", "UnitSilhouette.cs", "WorldLens.Parts.cs"];
        foreach (string file in objectFiles)
        {
            string code = string.Join("\n", File.ReadAllLines(Path.Combine(RepoRoot(), "Sim.Ui", "World", file))
                .Select(l => { int c = l.IndexOf("//", StringComparison.Ordinal); return c >= 0 ? l[..c] : l; }));
            Assert.DoesNotMatch(@"\bAge\b|PlayerAge|AgeQuery|\.Age\b|UiEra|Ordinal\(", code);
        }
    }

    private static List<string> WithoutBanners(IReadOnlyList<DrawCmd> cmds)
    {
        static bool Numeral(DrawCmd c) => c is TextCmd t && t.Text is "I" or "II" or "III" or "IV" or "V" or "VI" or "VII" or "VIII" or "IX";
        var r = new List<string>();
        for (int i = 0; i < cmds.Count; i++)
        {
            if (i + 2 < cmds.Count && cmds[i] is LineCmd && cmds[i + 1] is PolygonCmd && Numeral(cmds[i + 2])) { i += 2; continue; }
            r.Add(cmds[i] is PolygonCmd g ? "Polygon " + string.Join(";", g.Points) + " " + g.Fill : cmds[i].ToString()!);
        }
        return r;
    }

    // ================================================================== E1.5 silhouettes and grouped tokens

    /// <summary>
    /// D-038 C1 EXTENDED TO SILHOUETTES — NOT NOW, NOT LATER: the token of EVERY identity of the content, in EVERY era's hand,
    /// at both token sizes and grouped or alone, is one plate, then a silhouette made ONLY of filled polygons — each CONVEX
    /// with at most <see cref="UnitSilhouette.MaxVertices"/> vertices, at most <see cref="UnitSilhouette.MaxPolygons"/> of
    /// them — inside the plate, then one upright pip per form, then (grouped only) ONE number above the plate: no circle,
    /// arc, curve or polyline that could make a head or a limb, no other text. A convex solid of a few vertices cannot be a
    /// figure.
    /// </summary>
    [Fact]
    public void TheSilhouette_IsConvexSolids_NeverAFigure_ForEveryIdentity_InEveryEra()
    {
        UnitFamilyContent fam = Cfg.UnitFamilies!;
        foreach (UiEra era in UiEras.All)
        {
            MapInk k = MapInk.For(EraThemes.For(era));
            foreach (double s in new[] { 9.0, 12.0 })
                foreach (UnitFamily f in fam.Families)
                    foreach (UnitIdentity ident in f.Line)
                        foreach (int count in new[] { 1, 4 })
                        {
                            UnitEmblem e = UnitEmblem.Of(fam, f.Key, ident.Key);
                            var d = new DrawList();
                            WorldLens.FormationToken(d, 500, 300, s, e, k.PolityOf(3, 1), k, count);
                            string what = era + " " + ident.Id + " x" + count.ToString(CultureInfo.InvariantCulture);
                            RectCmd plate = Assert.IsType<RectCmd>(d.Commands[0]);
                            Assert.Equal(new RectD(500 - s, 300 - s * 0.75, 2 * s, 1.5 * s), plate.Rect);
                            List<DrawCmd> rest = d.Commands.Skip(1).ToList();
                            List<PolygonCmd> silhouette = rest.TakeWhile(c => c is PolygonCmd).Cast<PolygonCmd>().ToList();
                            List<DrawCmd> after = rest.Skip(silhouette.Count).ToList();
                            Assert.InRange(silhouette.Count, 2, UnitSilhouette.MaxPolygons);
                            foreach (PolygonCmd g in silhouette)
                            {
                                Assert.InRange(g.Points.Length, 3, UnitSilhouette.MaxVertices);
                                Assert.True(ObjectPaint.IsConvex(g.Points), what + ": a concave silhouette polygon");
                                foreach ((double x, double y) in g.Points)
                                {
                                    Assert.InRange(x, 500 - s, 500 + s);
                                    Assert.InRange(y, 300 - s * 0.75, 300 + s * 0.54);   // above the pips' band
                                }
                            }
                            int pips = Math.Min(e.Form, UnitEmblem.MaxPips);
                            Assert.All(after.Take(pips), c => Assert.IsType<LineCmd>(c));
                            List<DrawCmd> tail = after.Skip(pips).ToList();
                            if (count == 1) Assert.Empty(tail);
                            else
                            {
                                TextCmd n = Assert.IsType<TextCmd>(Assert.Single(tail));
                                Assert.Equal(count.ToString(CultureInfo.InvariantCulture), n.Text);
                                Assert.True(n.Y < 300 - s * 0.75, what + ": the number is not above the plate");
                            }
                        }
        }
    }

    /// <summary>KEYED BY IDENTITY: every token of one identity is drawn alike (two paints identical); the twelve role shapes
    /// differ from one another (a spear hedge is not a ship); a modernization (a new identity) changes the token.</summary>
    [Fact]
    public void TheSilhouette_IsKeyedByTheIdentity()
    {
        UnitFamilyContent fam = Cfg.UnitFamilies!;
        string Draw(UnitEmblem e)
        {
            var d = new DrawList();
            UnitSilhouette.Paint(d, MapInk.Default, 0, 0, 12, e);
            return string.Join("|", d.Commands.Select(c => c is PolygonCmd g ? string.Join(";", g.Points) + g.Fill : c.ToString()));
        }
        var byRole = new Dictionary<SilhouetteRole, string>();
        foreach (UnitFamily f in fam.Families)
            foreach (UnitIdentity ident in f.Line)
            {
                UnitEmblem e = UnitEmblem.Of(fam, f.Key, ident.Key);
                Assert.Equal(Draw(e), Draw(e));
                SilhouetteRole role = UnitSilhouette.RoleOf(f.Key);
                byRole.TryAdd(role, Draw(e with { Form = 1 }));
            }
        Assert.Equal(byRole.Count, byRole.Values.Distinct().Count());
        Assert.True(byRole.Count >= 8);
        UnitIdentity warband = fam.FamilyById("heavy_infantry")!.Line[0], line = fam.FamilyById("heavy_infantry")!.Line[4];
        Assert.NotEqual(Draw(UnitEmblem.Of(fam, warband.FamilyKey, warband.Key)), Draw(UnitEmblem.Of(fam, line.FamilyKey, line.Key)));
    }

    /// <summary>
    /// D-052 §7 — ONE TOKEN PER UNIT TYPE AND PLACE, TIE-DENSE: three formations of one identity at a station, two of another
    /// there, two of the first identity at one field position and one a quarter-px away, and a rival's formation of the same
    /// identity at the same station — in five table orders. In every order and at every zoom: one token per (owner, place,
    /// identity), its id the lowest member id, its members all of them, its number the formations (3, 2, 2 — none for a
    /// formation alone); every formation is held by exactly one token; a click on a token picks its representative; the
    /// selected formation's frame is drawn on the token that holds it.
    /// </summary>
    [Fact]
    public void FormationsOfOneTypeAndPlace_AreOneToken_WithTheirNumber_WhateverTheOrder()
    {
        UnitFamilyContent fam = Cfg.UnitFamilies!;
        UnitIdentity a = fam.FamilyById("heavy_infantry")!.Line[0], b = fam.FamilyById("spear_anti_cavalry")!.Line[0];
        UnitLensView U(int id, int owner, UnitIdentity i, int loc, double x, double y) =>
            new(id, owner, x, y, i.FamilyKey, "f", i.Name, loc) { Emblem = UnitEmblem.Of(fam, i.FamilyKey, i.Key) };
        var units = new List<UnitLensView>
        {
            U(40, 1, a, 1, 128, 128), U(12, 1, a, 1, 128, 128), U(31, 1, a, 1, 128, 128),
            U(7, 1, b, 1, 128, 128), U(25, 1, b, 1, 128, 128),
            U(50, 1, a, -1, 140.25, 120.5), U(51, 1, a, -1, 140.25, 120.5), U(52, 1, a, -1, 140.25, 120.75),
            U(60, 2, a, 1, 128, 128),
        };
        var expect = new Dictionary<int, int[]>
        {
            [12] = [12, 31, 40], [7] = [7, 25], [50] = [50, 51], [52] = [52], [60] = [60],
        };
        List<UnitLensView>[] orders =
        [
            units, units.AsEnumerable().Reverse().ToList(), units.OrderBy(u => u.Id).ToList(),
            units.OrderBy(u => (u.Id * 7919) % 101).ToList(), units.OrderBy(u => -u.Id).ToList(),
        ];
        List<string>? reference = null;
        foreach (List<UnitLensView> order in orders)
            foreach (WorldZoom z in Zooms)
            {
                WorldProjection p = Hand([View(1)], order);
                (DrawList d, LensFrame f) = Paint(p, z, z == WorldZoom.World ? 4 : 12, selectedUnit: 31);
                Assert.Equal(expect.Keys.OrderBy(x => x), f.UnitTokens.OrderBy(x => x));
                foreach (UnitPlacement up in f.UnitPlacements) Assert.Equal(expect[up.Id], up.Members);
                Assert.Equal(units.Select(u => u.Id).OrderBy(x => x), f.UnitPlacements.SelectMany(u => u.Members).OrderBy(x => x));
                var numbers = d.Commands.OfType<TextCmd>().Select(t => t.Text).Where(t => t.All(char.IsDigit)).OrderBy(t => t, StringComparer.Ordinal).ToList();
                Assert.Equal(["2", "2", "3"], numbers);
                foreach (UnitPlacement up in f.UnitPlacements)
                    Assert.Equal(up.Id, UnitSelection.HitTest(f.UnitPlacements, f.Zoom, up.X, up.Y));
                UnitPlacement holder = f.UnitPlacements.Single(u => u.Holds(31));
                double ts = holder.Size + 4;
                Assert.Single(d.Commands.OfType<RectCmd>(), r => r.Stroke == MapInk.Default.Selection
                    && r.Rect == new RectD(holder.X - ts, holder.Y - ts * 0.8, 2 * ts, 1.6 * ts));
                if (z != WorldZoom.Settlement) continue;
                List<string> placements = f.UnitPlacements.OrderBy(u => u.Id).Select(u => u.Id.ToString(CultureInfo.InvariantCulture) + "@"
                    + u.X.ToString("R", CultureInfo.InvariantCulture) + "," + u.Y.ToString("R", CultureInfo.InvariantCulture) + "#" + u.Slot.ToString(CultureInfo.InvariantCulture)).ToList();
                if (reference is null) reference = placements;
                else Assert.Equal(reference, placements);
            }
    }

    /// <summary>The combined panel's composition line (D-052 §7): a grouped formation's card names how many of its type stand
    /// with it and which; a formation alone has none; it counts formations, never people.</summary>
    [Fact]
    public void TheCard_NamesTheTokensComposition()
    {
        WorldState w = fx.Session.World.Clone();
        MilitaryUnitRow mine = MilitaryQuery.Units(w, Me)[0];
        UnitCard? alone = UnitCardModel.Build(w, Cfg, mine.Id, Me, fx.Session.Names.Name);
        Assert.Equal("", alone!.Composition);
        int next = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) next = Math.Max(next, w.MilitaryUnits[i].Id + 1);
        w.MilitaryUnits.Add(mine with { Id = next });
        w.MilitaryUnits.Add(mine with { Id = next + 1, Identity = Cfg.UnitFamilies!.FamilyById("spear_anti_cavalry")!.Line[0].Key });
        UnitCard? grouped = UnitCardModel.Build(w, Cfg, mine.Id, Me, fx.Session.Names.Name);
        Assert.Equal("Grouped here: 2 formations of " + grouped!.Title + " (ids " + mine.Id.ToString(CultureInfo.InvariantCulture) + ", "
            + next.ToString(CultureInfo.InvariantCulture) + ") - one token on the map", grouped.Composition);
    }

    /// <summary>THE PARTS MANIFEST names every part the lens draws, and institutions are drawn from CONTENT: the five
    /// university types by key, a new type by a word of its name (a law court: the scales), any other by the generic hall.</summary>
    [Fact]
    public void TheManifest_ListsEveryPart_AndInstitutionsAreDrawnFromContent()
    {
        Assert.Equal(PartManifest.Entries.Length, PartManifest.Entries.Select(e => e.Id).Distinct().Count());
        foreach (string id in new[] { "dwelling", "structure.storage", "structure.production", "structure.other", "institution.university",
                     "institution.named", "institution.generic", "scaffold", "overflow", "relief.peak", "relief.mountain", "relief.hill", "forest", "field", "unit.silhouette" })
            Assert.Contains(PartManifest.Entries, e => e.Id == id);
        for (int t = 1; t <= 5; t++) Assert.Equal((InstitutionEmblem)t, PartManifest.InstitutionStyleOf(t, "anything"));
        Assert.Equal(InstitutionEmblem.Scales, PartManifest.InstitutionStyleOf(6, "Law court"));
        Assert.Equal(InstitutionEmblem.Scales, PartManifest.InstitutionStyleOf(11, "Court of justice"));
        Assert.Equal(InstitutionEmblem.Generic, PartManifest.InstitutionStyleOf(12, "Guild hall"));
        Assert.Equal(InstitutionEmblem.Generic, PartManifest.InstitutionStyleOf(13, "Outlaw camp"));   // a word, not a substring
    }
}
