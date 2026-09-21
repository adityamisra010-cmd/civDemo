using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// The default screen geometry, proven headless at the design resolution.
///
/// T3.9a-b established the disjointness proof, and it is kept. T4.18 adds the
/// test that proof could never have been: that the WORLD gets most of the
/// screen. Five permanent non-overlapping panels covered 1280×800 almost
/// entirely and passed the old gate cleanly — "no two panels overlap" certifies
/// a full screen as correct, which is how the layout got that way.
/// </summary>
public class PanelLayoutTests
{
    [Fact]
    public void DesignResolution_IsTheProjectDefaultWindow()
    {
        // 1280×800 — SimUiGame's ctor reads its PreferredBackBuffer size FROM
        // these constants, so the tested layout and the actual default window
        // cannot drift apart. Re-target deliberately, in both places at once.
        Assert.Equal(1280, PanelLayout.DesignWidth);
        Assert.Equal(800, PanelLayout.DesignHeight);
    }

    [Fact]
    public void TheChrome_IsThreeElements_AndTheContextPanelIsNotOneOfThem()
    {
        // The roster, and the distinction that IS the redesign: status, command
        // and selection are always on screen; the context panel is usually
        // absent. A fourth permanent element joining Always is the regression
        // this pin exists to catch.
        Assert.Equal(3, PanelLayout.Always.Count);
        Assert.Equal(["##status", "##command", "##selection"],
            PanelLayout.Always.Select(p => p.Title).ToArray());
        Assert.DoesNotContain(PanelLayout.Context, PanelLayout.Always);
        Assert.Equal(4, PanelLayout.All.Count);
    }

    [Fact]
    public void WithNoSectionOpen_TheWorldGetsMostOfTheScreen()
    {
        // THE TEST THE OLD LAYOUT WOULD HAVE FAILED. Its five permanent panels
        // left the map essentially nothing, and every gate passed. Bars cost
        // 104 px of height between them and nothing else is reserved, so the
        // clear map area is over 85% of the viewport.
        float viewport = PanelLayout.DesignWidth * (float)PanelLayout.DesignHeight;
        float clear = PanelLayout.ClearMapArea;

        Assert.True(clear / viewport > 0.85f,
            $"the clean world view keeps only {clear / viewport:P0} of the screen");
    }

    [Fact]
    public void WithASectionOpen_TheWorldStillHoldsTheMajorityOfTheScreen()
    {
        // Opening a panel must not put the game back where it started: even
        // with the widest surface open, the map keeps more than half.
        float viewport = PanelLayout.DesignWidth * (float)PanelLayout.DesignHeight;
        Assert.True(PanelLayout.MapAreaWithContextOpen / viewport > 0.55f,
            $"an open section leaves the map {PanelLayout.MapAreaWithContextOpen / viewport:P0}");
    }

    [Fact]
    public void EverythingThatCanBeOnScreenAtOnce_IsPairwiseDisjoint()
    {
        // The chrome plus one open section is the maximum simultaneous set, and
        // it must not overlap — kept from T3.9a-b, now over the new geometry.
        for (int a = 0; a < PanelLayout.All.Count; a++)
        {
            for (int b = a + 1; b < PanelLayout.All.Count; b++)
            {
                Assert.False(PanelLayout.Overlap(PanelLayout.All[a], PanelLayout.All[b]),
                    $"rects overlap: {PanelLayout.All[a].Title} vs {PanelLayout.All[b].Title}");
            }
        }
    }

    [Fact]
    public void EveryRect_IsInsideTheDesignViewport()
    {
        foreach (PanelRect p in PanelLayout.All)
        {
            Assert.True(p.Width > 0 && p.Height > 0, p.Title);
            Assert.True(p.X >= 0 && p.Y >= 0, p.Title);
            Assert.True(p.X + p.Width <= PanelLayout.DesignWidth, p.Title);
            Assert.True(p.Y + p.Height <= PanelLayout.DesignHeight, p.Title);
        }
    }

    [Fact]
    public void TheSelectionCard_FloatsOverTheMapWithoutCarvingAColumnOutOfIt()
    {
        // It is a small overlay, not a panel: under a tenth of the map band, or
        // it would be the old HUD growing back by another name.
        float band = PanelLayout.DesignWidth
            * (PanelLayout.DesignHeight - PanelLayout.Status.Height - PanelLayout.Command.Height);
        float card = PanelLayout.Selection.Width * PanelLayout.Selection.Height;

        Assert.True(card / band < 0.10f, $"the selection card takes {card / band:P0} of the map band");
    }

    [Fact]
    public void Overlap_DetectsIntrusion_AndIgnoresSharedEdges_NotVacuous()
    {
        // Positive control for the disjointness gate: a copy intruding one
        // pixel into the status band must register as overlap...
        PanelRect status = PanelLayout.Status;
        var intruder = new PanelRect("intruder", status.X + status.Width - 1, status.Y, 10, 10);
        Assert.True(PanelLayout.Overlap(status, intruder));
        Assert.True(PanelLayout.Overlap(intruder, status)); // symmetric
        // ...and edge-adjacency (shared boundary, zero shared area) must not,
        // or panels laid out flush would spuriously fail the gate.
        var adjacent = new PanelRect("adjacent", status.X + status.Width, status.Y, 10, 10);
        Assert.False(PanelLayout.Overlap(status, adjacent));
    }

    [Fact]
    public void ThePreT418Layout_WouldFailTheBreathingRoomGate()
    {
        // The regression pin, reconstructed: the five panels this replaced, at
        // their real default rects. They are pairwise disjoint — they passed
        // every gate — and together they leave the map almost nothing. This is
        // the shape the new tests exist to keep out.
        PanelRect[] old =
        [
            new("civ-sim", 12, 12, 440, 776),
            new("Graphs", 828, 12, 440, 448),
            new("Market", 828, 472, 440, 316),
            new("Annals", 464, 560, 352, 228),
            new("Trade", 464, 12, 352, 300),
        ];

        for (int a = 0; a < old.Length; a++)
        {
            for (int b = a + 1; b < old.Length; b++)
            {
                Assert.False(PanelLayout.Overlap(old[a], old[b]));   // it really did pass
            }
        }

        float covered = 0;
        foreach (PanelRect p in old) covered += p.Width * p.Height;
        float viewport = PanelLayout.DesignWidth * (float)PanelLayout.DesignHeight;

        // Measured, not estimated: 863,456 px of panel over a 1,024,000 px
        // viewport is 84.3%, and the panels sit ON the map rather than beside
        // it — so what was left for the world was the gaps between them.
        Assert.True(covered / viewport > 0.84f,
            $"the old layout covered {covered / viewport:P1} of the screen");
        Assert.True(PanelLayout.ClearMapArea / viewport > covered / viewport);
    }
}
