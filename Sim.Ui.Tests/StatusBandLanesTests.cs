using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Headless;
using Sim.Ui.Progression;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Director playtest 2026-10-29 — A SEPARATE CIVICS BUTTON. The status band had one research figure, opening the trees;
/// civics was reachable only through a tab inside them. Now the band has one figure per lane of the ADR-043 two-lane model:
/// Tech ([K], the Technology tab) and Civics ([C], the Civics tab), each stating its own lane (idle / chosen for next turn /
/// progress and points a turn / waiting / stops at End Turn) from the action surface's research block, and the two fit the
/// smallest window beside the strip and the Age chip.
/// </summary>
[Collection("ImGui context")]
public sealed class StatusBandLanesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-band-lanes-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    // ------------------------------------------------------------------ the figures (pure)

    private static readonly ResearchItem Cordage = new(11, "Cordage", 200.0, 54.0, "");
    private static readonly ResearchItem Oral = new(1401, "Oral tradition", 100.0, 18.9, "");
    private static readonly ResearchItem LongCivic = new(1433, "Independent electoral commissions", 400.0, 0.0, "");

    private static ResearchBlock Block(ResearchItem? target = null, ResearchItem? chosen = null, bool clear = false,
        CivicsLine? civics = null, RouteFunding? funding = null) =>
        new(target, chosen, clear, 12.34, 7, ImmutableArray<ResearchItem>.Empty, target is not null || chosen is not null, civics, funding);

    private static CivicsLine Civics(ResearchItem? target = null, ResearchItem? chosen = null, bool clear = false, RouteFunding? funding = null) =>
        new(target, chosen, clear, 3.14, 25.0, target is not null || chosen is not null, funding);

    [Fact]
    public void BothLanesIdle_EachFigureIsItsOwnCallToAction_WithItsOwnKey()
    {
        ResearchBlock b = Block(civics: Civics());
        ResearchFigure tech = StatusFigures.Research(b), civ = StatusFigures.Civics(b)!;
        Assert.Equal(("Tech idle - choose [K]", true), (tech.Text, tech.Idle));
        Assert.Equal(("Civics idle - choose [C]", true), (civ.Text, civ.Idle));
        Assert.Equal("Tech idle [K]", tech.Short);
        Assert.Equal("Civics idle [C]", civ.Short);
        Assert.Equal(tech.Short, tech.Tiny);
    }

    [Fact]
    public void EachLane_InProgress_ShowsItsOwnNodePercentAndPointsATurn_NotTheOthers()
    {
        ResearchBlock b = Block(target: Cordage, civics: Civics(target: Oral));
        ResearchFigure tech = StatusFigures.Research(b), civ = StatusFigures.Civics(b)!;
        // Tech: 54 of 200 = 27 %, the RP pool 12.34 a turn; Civics: 18.9 of 100 = 18 % (floored), the CP 3.14 a turn.
        Assert.Equal(("Tech: Cordage 27% +12.3/turn", false), (tech.Text, tech.Idle));
        Assert.Equal(("Civics: Oral tradition 18% +3.1/turn", false), (civ.Text, civ.Idle));
        Assert.Equal("Tech 27% +12.3/turn", tech.Short);
        Assert.Equal("Civics 18% +3.1/turn", civ.Short);
        Assert.Equal("Tech 27%", tech.Tiny);
        Assert.Equal("Civics 18%", civ.Tiny);
        // A lane's state never leaks into the other's figure: the Civics lane idle leaves the Tech figure in progress.
        ResearchBlock techOnly = Block(target: Cordage, civics: Civics());
        Assert.Equal("Tech: Cordage 27% +12.3/turn", StatusFigures.Research(techOnly).Text);
        Assert.Equal("Civics idle - choose [C]", StatusFigures.Civics(techOnly)!.Text);
        ResearchBlock civicOnly = Block(civics: Civics(target: Oral));
        Assert.Equal("Tech idle - choose [K]", StatusFigures.Research(civicOnly).Text);
        Assert.Equal("Civics: Oral tradition 18% +3.1/turn", StatusFigures.Civics(civicOnly)!.Text);
    }

    [Fact]
    public void ChosenThisTurn_AndStopQueued_ArePerLane()
    {
        ResearchBlock chosen = Block(chosen: Cordage, civics: Civics(chosen: LongCivic));
        Assert.Equal("Tech: Cordage next turn", StatusFigures.Research(chosen).Text);
        // The node's name is cut to StatusFigures.NameChars with an ellipsis, as the Tech figure's.
        Assert.Equal("Civics: Independent electoral... next turn", StatusFigures.Civics(chosen)!.Text);
        Assert.Equal("Civics chosen", StatusFigures.Civics(chosen)!.Short);
        Assert.False(StatusFigures.Civics(chosen)!.Idle);

        ResearchBlock stopping = Block(target: Cordage, clear: true, civics: Civics(target: Oral, clear: true));
        Assert.Equal(("Tech stops at End Turn", true), (StatusFigures.Research(stopping).Text, StatusFigures.Research(stopping).Idle));
        Assert.Equal(("Civics stops at End Turn", true), (StatusFigures.Civics(stopping)!.Text, StatusFigures.Civics(stopping)!.Idle));
        Assert.Equal("Civics stops", StatusFigures.Civics(stopping)!.Short);
    }

    [Fact]
    public void WaitingAndAssisting_ArePerLane()
    {
        // rp-cp-lifecycle k4: a civics destination waiting on the Technology tree — the Civics figure says so (attention).
        var waiting = new RouteFunding(LaneActivity.Waiting, null, "Oral tradition", "Technology", "Cordage");
        ResearchBlock b = Block(target: Cordage, civics: Civics(target: Oral, funding: waiting));
        ResearchFigure civ = StatusFigures.Civics(b)!;
        Assert.Equal(("Civics waiting: Oral tradition", true), (civ.Text, civ.Idle));
        Assert.Equal("Civics waiting", civ.Short);
        Assert.Equal("Tech: Cordage 27% +12.3/turn", StatusFigures.Research(b).Text);
        // k3: a Civics lane with no target ASSISTING the Technology route names the civic it funds — not idle.
        var assisting = new RouteFunding(LaneActivity.Assisting, Oral, "Cordage", "Technology", null);
        ResearchFigure assist = StatusFigures.Civics(Block(target: Cordage, civics: Civics(funding: assisting)))!;
        Assert.Equal(("Civics: Oral tradition 18% +3.1/turn", false), (assist.Text, assist.Idle));
    }

    [Fact]
    public void OnePoolModel_OrNoResearch_HasNoCivicsFigure_AndTheOneLaneIsResearch()
    {
        ResearchBlock onePool = Block(target: Cordage);
        Assert.Null(StatusFigures.Civics(onePool));
        Assert.Equal("Research: Cordage 27% +12.3/turn", StatusFigures.Research(onePool).Text);
        Assert.Equal("Research idle - choose [K]", StatusFigures.Research(Block()).Text);
        Assert.Null(StatusFigures.Civics(null));
        Assert.Equal("Knowledge [K]", StatusFigures.Research(null).Text);
    }

    // ------------------------------------------------------------------ the live band

    [Fact]
    public void TurnOne_TheBand_HasATechAndACivicsChip_EachOpeningItsOwnTab()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "chips"), Assets(), developer: false);
        UiControl tech = h.Ui.Controls.Find("band-research") ?? throw new InvalidOperationException("no Tech chip");
        UiControl civics = h.Ui.Controls.Find("band-civics") ?? throw new InvalidOperationException("no Civics chip");
        Assert.True(civics.X0 >= tech.X1, "the Civics chip sits after the Tech chip");
        Assert.Contains("Tech idle - choose [K]", h.Ui.FrameText.Texts(FrameSurface.Status));
        Assert.Contains("Civics idle - choose [C]", h.Ui.FrameText.Texts(FrameSurface.Status));

        Assert.True(h.ClickControl("band-civics"));
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Civics, h.Ui.Progression!.Tab);
        h.Key(Keys.Escape);
        Assert.False(h.Ui.ProgressionOpen);
        // The Tech chip opens on Technology even though the screen was last left on Civics.
        Assert.True(h.ClickControl("band-research"));
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Technology, h.Ui.Progression.Tab);
        h.Key(Keys.Escape);
        Assert.Empty(h.Ui.Session.QueuedOrders());   // the chips are UI only: no order
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheKeys_KOpensTechnology_COpensCivics_EachClosesItsOwnTab_AndCtrlCIsLeftAlone()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "keys"), Assets(), developer: false);
        h.Key(Keys.C);
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Civics, h.Ui.Progression!.Tab);
        h.Key(Keys.K);   // K from the Civics tab: to Technology, the screen stays open
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Technology, h.Ui.Progression.Tab);
        h.Key(Keys.C);   // C from the Technology tab: to Civics
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Civics, h.Ui.Progression.Tab);
        h.Key(Keys.C);   // C on the Civics tab: closes
        Assert.False(h.Ui.ProgressionOpen);
        h.Key(Keys.K);   // K opens on Technology (the screen was left on Civics)
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(TreeTab.Technology, h.Ui.Progression.Tab);
        h.Key(Keys.K);   // K on the Technology tab: closes
        Assert.False(h.Ui.ProgressionOpen);
        h.Key(Keys.C, Keys.LeftControl);   // Ctrl+C is not the Civics key
        Assert.False(h.Ui.ProgressionOpen);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }

    /// <summary>Both lanes in progress on their longest-named nodes, in the smallest window the game allows: the Tech, Civics
    /// and Age chips stay inside the window in that order without overlapping (the node names drop first, then the points);
    /// in a maximised window both lane figures are drawn in full.</summary>
    [Fact]
    public void BothLanesInProgress_TheChipsFitTheSmallestWindow_AndAreFullInAWideOne()
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        ResearchContent c = s.Config.Research!;
        bool tech = false, civic = false;
        foreach (ResearchNode n in c.Nodes.OrderByDescending(n => n.Name.Length).ThenBy(n => n.Key.Value))
        {
            if (!tech && n.Tree == ResearchTree.Technology) tech = s.EmitResearchOrder(n.Key);
            if (!civic && n.Tree == ResearchTree.Civics) civic = s.EmitResearchOrder(n.Key);
        }
        Assert.True(tech && civic, "anti-vacuity: a destination on each lane");
        for (int t = 0; t < 2; t++) s.EndTurn();
        int capital = EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap) ? cap.Value : -1;
        ActionSurfaceModel m = ActionSurface.ForSession(s, UiSession.ProductionEra(), capital,
            Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEras.Of(s.World, s.Config.Ages, UiPlayer.Empire)));
        ResearchFigure techFigure = StatusFigures.Research(m.Research), civicsFigure = StatusFigures.Civics(m.Research)!;
        Assert.Contains("%", techFigure.Text);
        Assert.Contains("%", civicsFigure.Text);

        using (UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, "narrow"), Assets(), developer: false,
            PanelLayout.MinWindowWidth, PanelLayout.MinWindowHeight))
        {
            h.Idle(3);
            UiControl r = h.Ui.Controls.Find("band-research")!.Value;
            UiControl cv = h.Ui.Controls.Find("band-civics")!.Value;
            UiControl a = h.Ui.Controls.Find("band-age")!.Value;
            Assert.True(r.X1 <= cv.X0 && cv.X1 <= a.X0, "the chips overlap: " + r.X1 + " > " + cv.X0 + " or " + cv.X1 + " > " + a.X0);
            Assert.True(a.X1 <= h.Width, "the Age chip leaves the " + h.Width + " px window: " + a.X1);
            Assert.Empty(h.Problems);
        }
        using (UiFrameHarness wide = UiFrameHarness.Start(s, Path.Combine(_root, "wide"), Assets(), developer: false, 1920, 1009))
        {
            wide.Idle(3);
            IReadOnlyList<string> band = wide.Ui.FrameText.Texts(FrameSurface.Status);
            Assert.Contains(techFigure.Text, band);
            Assert.Contains(civicsFigure.Text, band);
            Assert.Empty(wide.Problems);
        }
    }
}
