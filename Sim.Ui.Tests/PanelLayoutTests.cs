using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T3.9a-b item 4: the default panel layout is proven non-overlapping at the
// design resolution HEADLESS, so the director's gate never re-discovers
// panel overlap at the default window size. Collapse persistence itself is
// ImGui-internal session state (every SetNextWindow* call in SimUiGame is
// FirstUseEver and the imgui.ini is disabled) — what is pinnable headless is
// the default geometry those first-use calls apply.
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
    public void FivePanels_AllPresent_TitlesAreTheWindowNames()
    {
        // The roster: every floating window the game Begins. A new panel
        // must join the layout (and this list) to get a default rect —
        // which is this pin doing its job: T3.9b's Trade panel had to come
        // through here, and through the disjointness proof below with it.
        Assert.Equal(5, PanelLayout.All.Count);
        Assert.Equal(["civ-sim", "Graphs", "Market", "Annals", "Trade"],
            PanelLayout.All.Select(p => p.Title).ToArray());
    }

    [Fact]
    public void DefaultRects_PairwiseDisjoint_AtTheDesignResolution()
    {
        for (int a = 0; a < PanelLayout.All.Count; a++)
        {
            for (int b = a + 1; b < PanelLayout.All.Count; b++)
            {
                Assert.False(PanelLayout.Overlap(PanelLayout.All[a], PanelLayout.All[b]),
                    $"default rects overlap: {PanelLayout.All[a].Title} vs {PanelLayout.All[b].Title}");
            }
        }
    }

    [Fact]
    public void DefaultRects_InsideTheDesignViewport()
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
    public void Overlap_DetectsIntrusion_AndIgnoresSharedEdges_NotVacuous()
    {
        // Positive control for the disjointness gate: a copy intruding one
        // pixel into the HUD must register as overlap...
        PanelRect hud = PanelLayout.Hud;
        var intruder = new PanelRect("intruder", hud.X + hud.Width - 1, hud.Y, 10, 10);
        Assert.True(PanelLayout.Overlap(hud, intruder));
        Assert.True(PanelLayout.Overlap(intruder, hud)); // symmetric
        // ...and edge-adjacency (shared boundary, zero shared area) must not,
        // or panels laid out flush would spuriously fail the gate.
        var adjacent = new PanelRect("adjacent", hud.X + hud.Width, hud.Y, 10, 10);
        Assert.False(PanelLayout.Overlap(hud, adjacent));
    }

    [Fact]
    public void ThePreT39abDefect_HudGrowingOverTheAnnals_WouldFailTheGate()
    {
        // Regression pin on the gate-Q4 defect shape: the pre-polish HUD was
        // AlwaysAutoResize with no height cap at (12,12), and the Annals
        // defaulted to (12,560). The moment auto-resize pushed the HUD's
        // height past 548 px it sat over the Annals — reconstructed here at
        // the minimal overflowing height to prove the disjointness gate
        // catches exactly that geometry.
        var oldAutoGrownHud = new PanelRect("civ-sim", 12, 12, 440, 549);
        var oldAnnals = new PanelRect("Annals", 12, 560, 560, 220);
        Assert.True(PanelLayout.Overlap(oldAutoGrownHud, oldAnnals));
    }
}
