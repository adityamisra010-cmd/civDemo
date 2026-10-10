using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.World;
using Xunit;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D8 — ONE MAP-INK SOURCE. The theme's map tokens (stream U1) and the world layer's MapInk (stream
/// U3) described the same thing; they converge on MapInk, the single complete record the world layer
/// consumes, derived from the era by <see cref="MapInk.For"/>. Identity inks are constant across the nine
/// eras; only the neutral ink, the casings and the legend paper follow the era; the parchment era draws
/// today's map exactly.
/// </summary>
public class MapInkEraTests
{
    /// <summary><paramref name="k"/> with every part that FOLLOWS THE ERA reset to the default: the six neutral inks
    /// and (packet P13) the map's hand. The hand is not an ink — it carries no colour — but the record now carries it
    /// and it differs by era by design, so it is reset here for the same reason the neutral inks are: what is left to
    /// compare is exactly the identity inks (re-scoped helper; no ink assertion changed). The hand's own pins are
    /// <see cref="MapHandEraTests"/>.</summary>
    private static MapInk Neutralised(MapInk k)
    {
        MapInk d = MapInk.Default;
        return k with
        {
            Ink = d.Ink, GlyphInk = d.GlyphInk, RoadEdge = d.RoadEdge,
            PathCasing = d.PathCasing, NamePlate = d.NamePlate, SettlementHalo = d.SettlementHalo,
            Hand = d.Hand,
        };
    }

    [Fact]
    public void TheParchmentEra_DrawsTodaysMap_Exactly()
    {
        Assert.Equal(MapInk.Default, MapInk.For(EraThemes.For(UiEra.Medieval)));
    }

    [Fact]
    public void IdentityInks_AreTheSameInEveryEra()
    {
        // Polities, sectors, universities, institutions, structures, dwellings, road-class fills and marks,
        // the selection ring and the capital mark: a civilization keeps its colour from A1 to A9.
        foreach (UiEra era in UiEras.All)
            Assert.Equal(MapInk.Default, Neutralised(MapInk.For(EraThemes.For(era))));
    }

    [Fact]
    public void TheNeutralInks_FollowTheEra_ByExactlyItsDepartureFromTheParchment()
    {
        foreach (UiEra era in UiEras.All)
        {
            EraTheme t = EraThemes.For(era);
            MapInk k = MapInk.For(t);
            Assert.Equal(t.Ink.Text, k.Ink);   // the line-work ink IS the era's text ink (Default.Ink is the bible ink)
            Rgba d = MapInk.Default.GlyphInk, i = t.Ink.Text, b = ParchmentPalette.InkPrimary;
            Assert.Equal(new Rgba(C(d.R + i.R - b.R), C(d.G + i.G - b.G), C(d.B + i.B - b.B)), k.GlyphInk);
            Rgba c = MapInk.Default.PathCasing, p = t.Material.Panel, pb = ParchmentPalette.PaperLight;
            Assert.Equal(new Rgba(C(c.R + p.R - pb.R), C(c.G + p.G - pb.G), C(c.B + p.B - pb.B)), k.PathCasing);
        }
        // And they really move: the Stone Age's ink is not the parchment's.
        Assert.NotEqual(MapInk.Default.Ink, MapInk.For(EraThemes.For(UiEra.Prehistoric)).Ink);
        Assert.NotEqual(MapInk.Default.NamePlate, MapInk.For(EraThemes.For(UiEra.Modern)).NamePlate);
    }

    [Fact]
    public void TheEraInk_StaysLegibleOnTheFrozenParchmentSubstrate()
    {
        // The map substrate is never themed (style bible §1), so every era's line-work ink must still read on it.
        foreach (UiEra era in UiEras.All)
        {
            MapInk k = MapInk.For(EraThemes.For(era));
            Assert.True(ThemeColor.Contrast(k.Ink, ParchmentPalette.PaperMid) >= 4.5, $"{era}: map ink contrast");
            Assert.True(ThemeColor.Contrast(k.GlyphInk, ParchmentPalette.PaperMid) >= ThemeColor.Contrast(k.Ink, ParchmentPalette.PaperMid), $"{era}: glyph ink darker");
            Assert.True(ThemeColor.Lightness(k.PathCasing) > 0.7, $"{era}: the casing stays pale");
        }
    }

    [Fact]
    public void TheMapInk_FadesWithTheTheme()
    {
        // The Age-transition cross-fade carries no map token: the map ink is derived from the faded theme.
        EraTheme a = EraThemes.For(UiEra.Prehistoric), b = EraThemes.For(UiEra.Neolithic);
        Assert.Equal(MapInk.For(a), MapInk.For(ThemeLerp.Between(a, b, 0)));
        Assert.Equal(MapInk.For(b), MapInk.For(ThemeLerp.Between(a, b, 1)));
        Assert.Equal(ThemeLerp.Between(a, b, 0.5).Ink.Text, MapInk.For(ThemeLerp.Between(a, b, 0.5)).Ink);
    }

    [Fact]
    public void TheThemeCarriesNoDuplicateMapTokens()
    {
        Assert.Null(typeof(EraTheme).GetProperty("Map"));
        Assert.Null(typeof(EraTheme).Assembly.GetType("Sim.Ui.Theme.MapInkTokens"));
    }

    // ------------------------------------------------------------------ the map's hand (packet P13)

    /// <summary>The hand is a THEME TOKEN GROUP (era-ui.md §8: a field on the record, a value in every era, carried to
    /// the lens by <see cref="MapInk.For"/>) and carries no colour — so the theme still holds no duplicate map ink.</summary>
    [Fact]
    public void TheMapHand_IsAThemeTokenGroup_WithNoColour_AndAValueInEveryEra()
    {
        Assert.Equal(typeof(MapHandTokens), typeof(EraTheme).GetProperty(nameof(EraTheme.MapHand))!.PropertyType);
        Assert.DoesNotContain(typeof(MapHandTokens).GetProperties(), p => p.PropertyType == typeof(Rgba));
        foreach (UiEra era in UiEras.All)
        {
            EraTheme t = EraThemes.For(era);
            Assert.NotNull(t.MapHand);
            Assert.Equal(t.MapHand, MapInk.For(t).Hand);   // the lens's one record carries exactly the theme's hand
            if (era == UiEra.Medieval) Assert.Equal(MapHandTokens.Parchment, t.MapHand);
            else Assert.NotEqual(MapHandTokens.Parchment, t.MapHand);   // every other era has a hand of its own
            // The map's hand is irregular exactly where the era's icon hand is (daubed A1, incised A2).
            Assert.Equal(t.Icons.WobblePx > 0, t.MapHand.WobblePx > 0);
        }
        // Today's map IS the parchment hand: base weights, a ruled line, the 2-px plaque, the swallowtail, the 3-px
        // name plate with no rule (the values the lens drew before the tokens existed).
        Assert.Equal(new MapHandTokens(1.0, 1.0, 0.0, PlateShape.Plaque, BannerFly.Swallowtail, 3.0, 0.0), MapHandTokens.Parchment);
        Assert.Equal(MapHandTokens.Parchment, MapInk.Default.Hand);
        Assert.Equal(2.0, WorldLens.PlateRadius(PlateShape.Plaque, 9));
        Assert.Equal(2.0, WorldLens.PlateRadius(PlateShape.Plaque, 12));
    }

    /// <summary>The hand is canonicalised (the determinism and save/load pins compare the canonical text) and
    /// cross-faded like every token: measures interpolate, the plate's cut and the banner's fly switch at the midpoint,
    /// and the lens's record follows the faded theme.</summary>
    [Fact]
    public void TheMapHand_IsCanonicalised_AndFadesWithTheTheme()
    {
        foreach (UiEra era in UiEras.All)
        {
            string c = EraThemes.For(era).Canonical();
            foreach (string token in new[] { "MapHand.StrokeScale=", "MapHand.EmblemScale=", "MapHand.WobblePx=", "MapHand.Plate=",
                "MapHand.Banner=", "MapHand.NamePlateCornerPx=", "MapHand.NamePlateRulePx=" })
                Assert.Contains(token, c, StringComparison.Ordinal);
        }
        Assert.Contains("MapHand.Plate=Pebble", EraThemes.For(UiEra.Prehistoric).Canonical(), StringComparison.Ordinal);

        EraTheme a = EraThemes.For(UiEra.Prehistoric), b = EraThemes.For(UiEra.Iron);
        Assert.Equal(a.Canonical(), ThemeLerp.Between(a, b, 0).Canonical());
        Assert.Equal(b.Canonical(), ThemeLerp.Between(a, b, 1).Canonical());
        MapHandTokens ha = a.MapHand, hb = b.MapHand, early = ThemeLerp.Between(a, b, 0.25).MapHand, late = ThemeLerp.Between(a, b, 0.75).MapHand;
        Assert.Equal(ha.StrokeScale + (hb.StrokeScale - ha.StrokeScale) * 0.25, early.StrokeScale);
        Assert.Equal(ha.WobblePx + (hb.WobblePx - ha.WobblePx) * 0.75, late.WobblePx);
        Assert.Equal(ha.NamePlateRulePx + (hb.NamePlateRulePx - ha.NamePlateRulePx) * 0.25, early.NamePlateRulePx);
        Assert.Equal((ha.Plate, ha.Banner), (early.Plate, early.Banner));
        Assert.Equal((hb.Plate, hb.Banner), (late.Plate, late.Banner));
        Assert.NotEqual(ha.Plate, hb.Plate);
        Assert.NotEqual(ha.Banner, hb.Banner);
        Assert.Equal(ThemeLerp.Between(a, b, 0.25).MapHand, MapInk.For(ThemeLerp.Between(a, b, 0.25)).Hand);
    }

    /// <summary>THE TESTS' OWN ORACLE for the plate cut (P13 review): the corner radius each <see cref="PlateShape"/>
    /// draws at token half-size <paramref name="s"/>, written as independent literals — never a call to
    /// <see cref="WorldLens.PlateRadius"/>, so a collapse of the painter's mapping cannot move the oracle with it.</summary>
    internal static double PlateCut(PlateShape plate, double s) => plate switch
    {
        PlateShape.Pebble => 0.75 * s,   // rounds the 2s x 1.5s plate's ends fully
        PlateShape.Tablet => 0.4 * s,
        PlateShape.Square => 0.0,
        PlateShape.Plaque => 2.0,        // today's 2-px plate
        _ => throw new ArgumentOutOfRangeException(nameof(plate), plate, "no plate cut pinned for this shape"),
    };

    /// <summary>THE TESTS' OWN ORACLE for the banner fly (P13 review): the cloth's vertices for each
    /// <see cref="BannerFly"/>, hoisted at <paramref name="x"/>, top <c>y − h</c>, foot <c>y + 6</c>, written as
    /// independent literals — never a call to <see cref="WorldLens.Fly"/>.</summary>
    internal static (double X, double Y)[] FlyCloth(BannerFly fly, double x, double y, double w, double h) => fly switch
    {
        BannerFly.Tapered => [(x, y - h), (x + w, y - h), (x + w, y), (x, y + 6)],
        BannerFly.Square => [(x, y - h), (x + w, y - h), (x + w, y + 6), (x, y + 6)],
        BannerFly.Swallowtail => [(x, y - h), (x + w, y - h), (x + w - 4, y - h * 0.35), (x + w, y + 6), (x, y + 6)],
        _ => throw new ArgumentOutOfRangeException(nameof(fly), fly, "no banner fly pinned for this cloth"),
    };

    /// <summary>THE PLATE CUT AND THE BANNER FLY ARE PINNED BY LITERALS (P13 review): each shape's corner radius at
    /// both token sizes (World 9, Regional/Settlement 12) and each fly's exact vertex list are numbers written here,
    /// so a mapping that collapses to the parchment's (every plate 2 px, every fly the swallowtail) fails. The four
    /// cuts and the three flies are pairwise distinct — every era token chooses a mark the player can see.</summary>
    [Fact]
    public void ThePlateCut_AndTheBannerFly_ArePinnedByLiterals()
    {
        // The plate cut, at the two token half-sizes the lens draws and at round sizes where 0.4 s is exact.
        Assert.Equal(6.75, WorldLens.PlateRadius(PlateShape.Pebble, 9));
        Assert.Equal(9.0, WorldLens.PlateRadius(PlateShape.Pebble, 12));
        Assert.Equal(4.0, WorldLens.PlateRadius(PlateShape.Tablet, 10));
        Assert.Equal(8.0, WorldLens.PlateRadius(PlateShape.Tablet, 20));
        Assert.Equal(0.0, WorldLens.PlateRadius(PlateShape.Square, 9));
        Assert.Equal(0.0, WorldLens.PlateRadius(PlateShape.Square, 12));
        Assert.Equal(2.0, WorldLens.PlateRadius(PlateShape.Plaque, 9));
        Assert.Equal(2.0, WorldLens.PlateRadius(PlateShape.Plaque, 12));
        foreach (PlateShape plate in Enum.GetValues<PlateShape>())
            foreach (double s in new[] { 9.0, 12.0 })
                Assert.Equal(PlateCut(plate, s), WorldLens.PlateRadius(plate, s));
        foreach (double s in new[] { 9.0, 12.0 })
            Assert.Equal(4, Enum.GetValues<PlateShape>().Select(plate => WorldLens.PlateRadius(plate, s)).Distinct().Count());

        // The banner fly, in a 24 x 20 box hoisted at (100, 200): top 180, foot 206 (20 x 0.35 = 7 is exact).
        Assert.Equal(new (double, double)[] { (100, 180), (124, 180), (124, 200), (100, 206) },
            WorldLens.Fly(BannerFly.Tapered, 100, 200, 24, 20));
        Assert.Equal(new (double, double)[] { (100, 180), (124, 180), (124, 206), (100, 206) },
            WorldLens.Fly(BannerFly.Square, 100, 200, 24, 20));
        Assert.Equal(new (double, double)[] { (100, 180), (124, 180), (120, 193), (124, 206), (100, 206) },
            WorldLens.Fly(BannerFly.Swallowtail, 100, 200, 24, 20));
        // And in a 48 x 40 box hoisted at (0, 0) (40 x 0.35 = 14 is exact): the fly scales with the box.
        Assert.Equal(new (double, double)[] { (0, -40), (48, -40), (48, 0), (0, 6) }, WorldLens.Fly(BannerFly.Tapered, 0, 0, 48, 40));
        Assert.Equal(new (double, double)[] { (0, -40), (48, -40), (48, 6), (0, 6) }, WorldLens.Fly(BannerFly.Square, 0, 0, 48, 40));
        Assert.Equal(new (double, double)[] { (0, -40), (48, -40), (44, -14), (48, 6), (0, 6) }, WorldLens.Fly(BannerFly.Swallowtail, 0, 0, 48, 40));
        foreach (BannerFly fly in Enum.GetValues<BannerFly>())
            Assert.Equal(FlyCloth(fly, 100, 200, 24, 20), WorldLens.Fly(fly, 100, 200, 24, 20));
    }

    /// <summary>Every banner fly stays inside the banner's box (hoist to width, top to the foot) and covers the
    /// numeral's whole field, at the World-zoom caption size and at twice it (UI scale 2).</summary>
    [Fact]
    public void EveryBannerFly_StaysInItsBox_AndCoversTheNumeralsField()
    {
        static bool Inside((double X, double Y)[] poly, double px, double py)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].Y > py) != (poly[j].Y > py) && px < (poly[j].X - poly[i].X) * (py - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
            return c;
        }
        foreach (BannerFly fly in Enum.GetValues<BannerFly>())
            foreach ((double w, double h) in new[] { (24.0, 18.0), (44.0, 18.0), (48.0, 36.0) })
            {
                const double x = 100, y = 200;
                (double X, double Y)[] cloth = WorldLens.Fly(fly, x, y, w, h);
                Assert.Equal(x, cloth.Min(q => q.X));
                Assert.Equal(x + w, cloth.Max(q => q.X));
                Assert.Equal(y - h, cloth.Min(q => q.Y));
                Assert.Equal(y + 6, cloth.Max(q => q.Y));
                double size = h / 1.2;   // the numeral is set at the caption size, h = 1.2 x size, from y - h + 1
                for (double px = x + 6.5; px <= x + w - 6.5; px += 1)
                    for (double py = y - h + 1.5; py <= y - h + 1 + size - 0.5; py += 1)
                        Assert.True(Inside(cloth, px, py), $"{fly} {w}x{h}: ({px}, {py}) of the numeral's field is off the cloth");
            }
    }

    /// <summary>The painters take the hand from its tokens only: no file of the world layer names an era, an era
    /// ordinal or the era themes (so no painter can switch on the era number), and none but <see cref="MapInk"/> names
    /// the parchment hand (a painter cannot hard-wire today's hand around the token).</summary>
    [Fact]
    public void ThePainters_SwitchOnTheHandsTokens_NeverOnTheEra_SourceGuard()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string[] files = Directory.GetFiles(Path.Combine(root, "Sim.Ui", "World"), "*.cs");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string src = File.ReadAllText(file);
            foreach (string banned in new[] { "UiEra", "EraThemes.", ".Ordinal", "UiEras." })
                Assert.DoesNotContain(banned, src, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\.Era\b", src);
            if (Path.GetFileName(file) != "MapInk.cs") Assert.DoesNotContain("MapHandTokens.Parchment", src, StringComparison.Ordinal);
        }
    }

    private static byte C(int v) => (byte)Math.Clamp(v, 0, 255);
}

/// <summary>
/// THE MAP'S HAND ON THE PAINTED LENS (packet P13; era-ui.md §8) — on the populated road world (every road class, a
/// partial route, three formations, a granary, an Age II advance; <see cref="RoadWorldFixture"/>) with the five
/// university halls at the capital through a stand-in institution seam: in every era the lens draws the SAME marks —
/// the same frame, the same commands in the same order and places, the network and territories untouched — and only
/// the tokenised ink and hand differ; each hand token shows exactly where it should; the hand is seeded from stable
/// keys and never thins a stroke below the floor.
/// </summary>
public class MapHandEraTests(RoadWorldFixture fx) : IClassFixture<RoadWorldFixture>
{
    private static readonly Sim.Core.State.PolityId Me = Sim.Ui.ViewModel.UiPlayer.Empire;
    private static readonly WorldZoom[] Zooms = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    /// <summary>The fixture's projection; the capital HAS the five university types (a test stand-in for the
    /// institution seam — the InstitutionWorldFixture precedent), so the hall glyphs and their emblems are drawn.</summary>
    private WorldProjection Project() =>
        WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me,
            (world, cfg, s) =>
            {
                if (s.Value != fx.Capital) return [];
                var r = new List<InstitutionView>();
                foreach (Sim.Core.Systems.Research.UniversityType u in cfg.Research!.UniversityTypes) r.Add(new InstitutionView(u.Key, u.Name, 1));
                return r;
            });

    /// <summary>The whole 256-px world at 4 px per world px (offset by a pan of (<paramref name="ox"/>,
    /// <paramref name="oy"/>) screen px): nothing is culled; the capital is selected.</summary>
    private (DrawList D, LensFrame F) Paint(WorldProjection p, WorldZoom z, MapInk ink, double ox = 0, double oy = 0)
    {
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, Sim.Ui.Render.ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4 + ox, y * 4 + oy), 4,
            new Sim.Ui.Render.RectD(ox, oy, 1024, 1024), selected: fx.Capital, ink: ink);
        return (d, f);
    }

    private static MapInk InkOf(UiEra era) => MapInk.For(EraThemes.For(era));

    /// <summary>LensFrame geometry: the layers, what was drawn, the draw-call accounting, the institution markers, the
    /// network's pieces and every token's placement (the unit hit regions are a function of these).</summary>
    internal static void AssertSameFrame(LensFrame a, LensFrame b, string what)
    {
        Assert.True(a.Zoom == b.Zoom, what);
        Assert.True(a.Layers.SequenceEqual(b.Layers), what + ": layers");
        Assert.True(a.UnitTokens.SequenceEqual(b.UnitTokens), what + ": unit tokens");
        Assert.True(a.SettlementsDrawn.SequenceEqual(b.SettlementsDrawn), what + ": settlements");
        Assert.True(a.LayerDraws.SequenceEqual(b.LayerDraws), what + ": draw-call accounting");
        Assert.True(a.Institutions.SequenceEqual(b.Institutions), what + ": institution markers");
        Assert.True(a.Paths.SequenceEqual(b.Paths), what + ": network pieces");
        Assert.True(a.UnitPlacements.SequenceEqual(b.UnitPlacements), what + ": token placements");
    }

    private static (double X0, double Y0, double X1, double Y1) Box((double X, double Y)[] pts) =>
        (pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));

    private static bool IsNumeral(DrawCmd c) => c is TextCmd t && t.Text is "I" or "II" or "III" or "IV" or "V" or "VI" or "VII" or "VIII" or "IX";

    /// <summary>
    /// GEOMETRY AND COMPOSITION IDENTICAL, HAND MAY DIFFER: in every era, at every zoom, the frame is the parchment
    /// frame and the command stream is the parchment stream command for command — same kind, same place: every text
    /// the same words at the same point and size; every rect the same rect; every circle and arc the same centre and
    /// radius; every polygon the same box (a banner's fly may be cut differently inside it); every line the same ends
    /// up to the hand's wobble. The territories and the network (the first commands, by the draw-call accounting) are
    /// drawn exactly as the parchment's but for ink: the hand never touches a road.
    /// </summary>
    [Fact]
    public void EveryEra_DrawsTheSameFrameAndMarks_OnlyTheInkAndHandDiffer()
    {
        WorldProjection p = Project();
        Assert.NotEmpty(p.Units);
        Assert.NotEmpty(p.Routes);
        foreach (WorldZoom z in Zooms)
        {
            (DrawList d0, LensFrame f0) = Paint(p, z, MapInk.Default);
            int network = f0.LayerDraws[(int)MapLayer.Territory] + f0.LayerDraws[(int)MapLayer.Paths];
            Assert.True(f0.LayerDraws[(int)MapLayer.Paths] > 0);
            foreach (UiEra era in UiEras.All)
            {
                MapInk k = InkOf(era);
                double wob = k.Hand.WobblePx + 1e-9;
                (DrawList d, LensFrame f) = Paint(p, z, k);
                string what = era + " at " + z;
                AssertSameFrame(f0, f, what);
                Assert.Equal(d0.Commands.Count, d.Commands.Count);
                for (int i = 0; i < d0.Commands.Count; i++)
                {
                    DrawCmd a = d0.Commands[i], b = d.Commands[i];
                    Assert.True(a.GetType() == b.GetType(), $"{what}: command {i} {a} vs {b}");
                    if (i < network)
                    {
                        // Territory and network: only the (era-shifted) colours may differ.
                        bool same = (a, b) switch
                        {
                            (LineCmd x, LineCmd y) => x with { Color = default } == y with { Color = default },
                            (RectCmd x, RectCmd y) => x with { Fill = null, Stroke = null } == y with { Fill = null, Stroke = null },
                            (TextCmd x, TextCmd y) => x with { Color = default } == y with { Color = default },
                            _ => false,
                        };
                        Assert.True(same, $"{what}: network command {i} {a} vs {b}");
                        continue;
                    }
                    switch (a, b)
                    {
                        case (TextCmd x, TextCmd y):
                            Assert.Equal(x with { Color = default }, y with { Color = default });
                            break;
                        case (RectCmd x, RectCmd y):
                            Assert.Equal(x.Rect, y.Rect);
                            Assert.Equal(x.Fill is null, y.Fill is null);
                            break;
                        case (CircleCmd x, CircleCmd y):
                            Assert.Equal((x.Cx, x.Cy, x.R), (y.Cx, y.Cy, y.R));
                            Assert.Equal((x.Fill is null, x.Stroke is null), (y.Fill is null, y.Stroke is null));
                            break;
                        case (ArcCmd x, ArcCmd y):
                            Assert.Equal((x.Cx, x.Cy, x.R, x.StartDeg, x.SweepDeg), (y.Cx, y.Cy, y.R, y.StartDeg, y.SweepDeg));
                            break;
                        case (PolygonCmd x, PolygonCmd y):
                        {
                            (double ax0, double ay0, double ax1, double ay1) = Box(x.Points);
                            (double bx0, double by0, double bx1, double by1) = Box(y.Points);
                            Assert.True(Math.Abs(ax0 - bx0) < 1e-9 && Math.Abs(ay0 - by0) < 1e-9 && Math.Abs(ax1 - bx1) < 1e-9 && Math.Abs(ay1 - by1) < 1e-9,
                                $"{what}: polygon {i} leaves its box");
                            // Only a banner's cloth (pole before, numeral after) may be cut differently.
                            if (!x.Points.SequenceEqual(y.Points))
                                Assert.True(d0.Commands[i - 1] is LineCmd && IsNumeral(d0.Commands[i + 1]), $"{what}: polygon {i} is not a banner but changed");
                            break;
                        }
                        case (LineCmd x, LineCmd y):
                            Assert.Equal(x.Dash, y.Dash);
                            Assert.True(Math.Abs(x.X0 - y.X0) <= wob && Math.Abs(x.Y0 - y.Y0) <= wob && Math.Abs(x.X1 - y.X1) <= wob && Math.Abs(x.Y1 - y.Y1) <= wob,
                                $"{what}: line {i} moved beyond the hand's wobble");
                            break;
                        default:
                            Assert.Equal(a, b);
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// EACH TOKEN SHOWS WHERE IT SHOULD, in every era: the token plates keep their box (the hit rect) and take the
    /// era's cut and frame weight; every banner's cloth is the era's fly in the parchment banner's box and its pole the
    /// era's weight; every name plate takes the era's corner and rule; every emblem stroke the emblem weight and every
    /// glyph frame or outline the stroke weight (× the parchment's base width, floored); the selection ring, the sector
    /// arcs and the legend swatch are not the hand's and keep their widths.
    /// </summary>
    [Fact]
    public void EachHandToken_ShowsExactlyWhereItShould_InEveryEra()
    {
        WorldProjection p = Project();
        MapInk k0 = MapInk.Default;
        foreach (WorldZoom z in Zooms)
        {
            (DrawList d0, LensFrame f0) = Paint(p, z, k0);
            double s = Sim.Ui.ViewModel.UnitSelection.TokenSize(z);
            int network = f0.LayerDraws[(int)MapLayer.Territory] + f0.LayerDraws[(int)MapLayer.Paths];
            foreach (UiEra era in UiEras.All)
            {
                MapInk k = InkOf(era);
                MapHandTokens h = k.Hand;
                double W(double w) => Math.Max(MapHandTokens.MinStrokePx, w * h.StrokeScale);
                double E(double w) => Math.Max(MapHandTokens.MinStrokePx, w * h.EmblemScale);
                (DrawList d, LensFrame f) = Paint(p, z, k);
                string what = era + " at " + z;
                // Token plates: the same box, the era's cut and frame weight.
                foreach (UnitPlacement u in f.UnitPlacements)
                {
                    var box = new Sim.Ui.Render.RectD(u.X - s, u.Y - s * 0.75, s * 2, s * 1.5);
                    RectCmd plate = Assert.Single(d.Commands.OfType<RectCmd>(), r => r.Rect == box);
                    // The oracle is the test's own literals (MapInkEraTests.PlateCut), never the painter's mapping;
                    // an era whose cut is not the parchment's draws a radius the parchment plate does not have.
                    Assert.Equal(MapInkEraTests.PlateCut(h.Plate, s), plate.Radius);
                    RectCmd plate0 = Assert.Single(d0.Commands.OfType<RectCmd>(), r => r.Rect == box);
                    if (h.Plate != PlateShape.Plaque) Assert.NotEqual(plate0.Radius, plate.Radius);
                    else Assert.Equal(plate0.Radius, plate.Radius);
                    Assert.Equal(W(1.4), plate.StrokeWidth);
                    Assert.Equal(k.GlyphInk, plate.Stroke);
                }
                int banners = 0, plates = 0, emblems = 0, frames = 0;
                for (int i = network; i < d0.Commands.Count; i++)
                {
                    DrawCmd a = d0.Commands[i], b = d.Commands[i];
                    // Banners: pole, cloth, numeral.
                    if (a is PolygonCmd cloth0 && d0.Commands[i - 1] is LineCmd pole0 && IsNumeral(d0.Commands[i + 1]))
                    {
                        (double X, double Y)[] q = cloth0.Points;   // the parchment's swallowtail
                        double x = q[0].X, w = q[1].X - x, y = q[4].Y - 6, hh = y - q[0].Y;
                        // The oracle is the test's own literals (MapInkEraTests.FlyCloth), never the painter's mapping;
                        // an era whose fly is not the swallowtail draws a cloth the parchment banner does not have.
                        (double X, double Y)[] want = MapInkEraTests.FlyCloth(h.Banner, x, y, w, hh);
                        (double X, double Y)[] got = ((PolygonCmd)b).Points;
                        Assert.Equal(want.Length, got.Length);
                        for (int j = 0; j < want.Length; j++) Assert.True(Math.Abs(want[j].X - got[j].X) < 1e-9 && Math.Abs(want[j].Y - got[j].Y) < 1e-9, $"{what}: banner {i}");
                        Assert.Equal(h.Banner == BannerFly.Swallowtail, got.SequenceEqual(q));
                        Assert.Equal(W(pole0.Width), ((LineCmd)d.Commands[i - 1]).Width);
                        banners++;
                        continue;
                    }
                    switch (a, b)
                    {
                        case (RectCmd x, RectCmd y) when x.Fill == Ink.With(k0.NamePlate, 0.78):
                            Assert.Equal(Ink.With(k.NamePlate, 0.78), y.Fill);
                            Assert.Equal(h.NamePlateCornerPx, y.Radius);
                            Assert.Equal(h.NamePlateRulePx > 0 ? Ink.With(k.Ink, 0.55) : null, y.Stroke);
                            Assert.Equal(h.NamePlateRulePx > 0 ? h.NamePlateRulePx : 1.0, y.StrokeWidth);
                            plates++;
                            break;
                        case (LineCmd x, LineCmd y) when x.Color == k0.EmblemMark || x.Color == k0.MedicalMark:
                            Assert.Equal(E(x.Width), y.Width);
                            emblems++;
                            break;
                        case (CircleCmd x, CircleCmd y) when x.Stroke is { } st && (st == k0.EmblemMark || st == k0.MedicalMark):
                            Assert.Equal(E(x.StrokeWidth), y.StrokeWidth);
                            emblems++;
                            break;
                        case (CircleCmd x, CircleCmd y) when x.Stroke is { } st && st == k0.Selection:
                            Assert.Equal(x.StrokeWidth, y.StrokeWidth);   // the selection ring is not the hand's
                            break;
                        case (CircleCmd x, CircleCmd y) when x.Stroke is not null:
                            Assert.Equal(W(x.StrokeWidth), y.StrokeWidth);   // settlement marks, footprints
                            frames++;
                            break;
                        case (RectCmd x, RectCmd y) when x.Fill is { } fl && k0.Sectors.Contains(fl):
                            Assert.Equal(x.StrokeWidth, y.StrokeWidth);      // the labour legend's swatch is not the hand's
                            break;
                        case (RectCmd x, RectCmd y) when x.Stroke is { } st && (st == k0.Ink || st == k0.GlyphInk):
                            Assert.Equal(W(x.StrokeWidth), y.StrokeWidth);   // structure, hall and token frames
                            frames++;
                            break;
                        case (RectCmd x, RectCmd _) when x.Stroke is not null:
                            Assert.Fail($"{what}: an unclassified stroked rect {x}");
                            break;
                        case (ArcCmd x, ArcCmd y) when x.Color == k0.Ink:
                            Assert.Equal(W(x.Width), y.Width);               // the granary's dome
                            frames++;
                            break;
                        case (ArcCmd x, ArcCmd y):
                            Assert.Equal(x.Width, y.Width);                  // sector arcs carry data, not the hand
                            break;
                    }
                }
                Assert.Equal(f.SettlementsDrawn.Count, banners);
                Assert.Equal(f.SettlementsDrawn.Count, plates);
                Assert.True(frames > 0, what + ": no glyph frame checked");
                if (z != WorldZoom.World) Assert.True(emblems > 0, what + ": no emblem checked");
            }
        }
    }

    /// <summary>The hand is SEEDED FROM STABLE KEYS: a panned map draws every handed stroke exactly translated (the
    /// wobble rides with the glyph, never with the screen position), and two paints are byte-identical.</summary>
    [Fact]
    public void TheHand_IsSeededFromStableKeys_NeverFromThePositionOrTheFrame()
    {
        WorldProjection p = Project();
        MapInk k = InkOf(UiEra.Prehistoric);
        Assert.True(k.Hand.WobblePx > 0);
        const double ox = 37.25, oy = -12.5;
        foreach (WorldZoom z in Zooms)
        {
            (DrawList a, _) = Paint(p, z, k);
            (DrawList again, _) = Paint(p, z, k);
            Assert.Equal(SvgWriter.Write(a, 1024, 1024, null), SvgWriter.Write(again, 1024, 1024, null));
            (DrawList b, _) = Paint(p, z, k, ox, oy);
            Assert.Equal(a.Commands.Count, b.Commands.Count);
            int wobbled = 0;
            (DrawList flat, _) = Paint(p, z, k with { Hand = k.Hand with { WobblePx = 0 } });
            for (int i = 0; i < a.Commands.Count; i++)
                if (a.Commands[i] is LineCmd x && b.Commands[i] is LineCmd y)
                {
                    Assert.True(Math.Abs(x.X0 + ox - y.X0) < 1e-9 && Math.Abs(x.Y0 + oy - y.Y0) < 1e-9
                        && Math.Abs(x.X1 + ox - y.X1) < 1e-9 && Math.Abs(x.Y1 + oy - y.Y1) < 1e-9, $"{z}: line {i} not translated");
                    if (!x.Equals(flat.Commands[i])) wobbled++;
                }
            Assert.True(wobbled > 0, $"{z}: the A1 hand wobbled nothing");
        }
    }

    /// <summary>No hand thins a stroke below <see cref="MapHandTokens.MinStrokePx"/>: every line, arc, stroked circle and
    /// stroked rect the lens draws — glyph frames, emblems, the pole, the name plates' rules — is at least the floor in
    /// every era (the lightest hands are floored, not hairlines). The one exception is not the hand's: the labour
    /// legend's 0.8-px swatch outline, the same in every era (pinned in <see cref="EachHandToken_ShowsExactlyWhereItShould_InEveryEra"/>).</summary>
    [Fact]
    public void NoHand_ThinsAStrokeBelowTheFloor()
    {
        foreach (UiEra era in UiEras.All)
        {
            MapHandTokens h = EraThemes.For(era).MapHand;
            Assert.True(h.NamePlateRulePx == 0 || h.NamePlateRulePx >= MapHandTokens.MinStrokePx, era + ": a hairline name-plate rule");
        }
        WorldProjection p = Project();
        foreach (WorldZoom z in Zooms)
            foreach (UiEra era in UiEras.All)
            {
                MapInk k = InkOf(era);
                (DrawList d, _) = Paint(p, z, k);
                foreach (DrawCmd c in d.Commands)
                {
                    double w = c switch
                    {
                        LineCmd l => l.Width,
                        ArcCmd ar => ar.Width,
                        CircleCmd { Stroke: not null } ci => ci.StrokeWidth,
                        RectCmd { Stroke: not null } r when !(r.Fill is { } f && k.Sectors.Contains(f)) => r.StrokeWidth,
                        _ => double.MaxValue,
                    };
                    Assert.True(w >= MapHandTokens.MinStrokePx, $"{era} at {z}: {c}");
                }
            }
        Assert.True(EraThemes.For(UiEra.Modern).MapHand.StrokeScale < 1.0);   // the floor has something to floor
    }
}
