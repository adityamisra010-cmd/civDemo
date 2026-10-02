using Sim.Ui.Art;
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
    private static MapInk Neutralised(MapInk k)
    {
        MapInk d = MapInk.Default;
        return k with
        {
            Ink = d.Ink, GlyphInk = d.GlyphInk, RoadEdge = d.RoadEdge,
            PathCasing = d.PathCasing, NamePlate = d.NamePlate, SettlementHalo = d.SettlementHalo,
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

    private static byte C(int v) => (byte)Math.Clamp(v, 0, 255);
}
