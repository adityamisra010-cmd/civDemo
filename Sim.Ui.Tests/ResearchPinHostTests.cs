using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Headless;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Director playtest 2026-11, through the real host (GameUi's frame loop, driven like a player by
/// <see cref="UiFrameHarness"/>): a click on a research card pins it and orders nothing; two clicks on the same card within
/// <see cref="CardDoubleClick.WindowSeconds"/> of FRAME time order it (the order a click used to give); two clicks too far
/// apart in time, or on two cards, order nothing; Escape clears the pin before it closes the screen; the Civics chip
/// carries the bank and its hover card states the bank and the rate.
/// </summary>
[Collection("ImGui context")]
public sealed class ResearchPinHostTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-pin-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private static (double X, double Y) Centre(ProgressionScreen s, int vertex)
    {
        PlacedVertex p = s.Layout.Placed[vertex];
        RectD c = s.Canvas;
        return (s.Camera.ToScreenX(p.CenterX, c.X), s.Camera.ToScreenY(p.CenterY, c.Y));
    }

    /// <summary>Two cards in view whose nodes the screen may order (available, not the target), as vertices.</summary>
    private static (int A, int B) TwoAvailable(ProgressionScreen s)
    {
        int a = -1, b = -1;
        foreach (int v in s.VisibleVertices())
        {
            if (s.Graph.Vertices[v].External || s.Layout.Placed[v].Hidden) continue;
            int ci = s.Graph.Vertices[v].ContentIndex;
            (double x, double y) = Centre(s, v);
            if (!s.Canvas.Contains(x, y) || s.Obscured(x, y) || s.Snapshot!.Nodes[ci] is not { Available: true, IsTarget: false }) continue;
            if (a < 0) a = v; else if (b < 0) { b = v; break; }
        }
        return (a, b);
    }

    [Fact]
    public void ThroughTheHost_AClickPins_ASlowSecondClickOrACardChangeOrdersNothing_ADoubleClickOrders()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "click"), Assets(), developer: false, 1920, 1080);
        h.Key(Keys.K);
        h.Idle(30);
        ProgressionScreen s = h.Ui.Progression!;
        Assert.True(h.Ui.ProgressionOpen && s.Docked);
        (int a, int b) = TwoAvailable(s);
        Assert.True(a >= 0 && b >= 0, "anti-vacuity: two available cards in view");
        int na = s.Graph.Vertices[a].ContentIndex, nb = s.Graph.Vertices[b].ContentIndex;

        // One click: pinned, nothing queued.
        (double ax, double ay) = Centre(s, a);
        h.Click(ax, ay);
        Assert.Equal(na, s.Pin);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        // A second click on the same card, after the window (30 frames = 0.5 s of frame time): still nothing.
        h.Idle(30);
        h.Click(ax, ay);
        Assert.Equal(na, s.Pin);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        // A click on another card right after (within the window): it moves the pin, and orders nothing.
        (double bx, double by) = Centre(s, b);
        h.Click(bx, by);
        Assert.Equal(nb, s.Pin);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        // A double-click on b: exactly the factory's order for b.
        h.Idle(30);
        OrderRecord? factory = ResearchOrderFactory.SetTarget(h.Ui.Session.World, s.Content, UiPlayer.Empire, s.Content.Nodes[nb].Key);
        h.DoubleClick(bx, by);
        Assert.Equal(nb, s.Pin);
        Assert.Equal(nb, s.PendingTarget);
        Assert.Equal(s.Content.Nodes[nb].Key.Value, h.Ui.Session.QueuedResearchChoice(s.Content.Nodes[nb].Tree));
        OrderRecord queued = Assert.Single(h.Ui.Session.QueuedOrders());
        Assert.Equal(factory!.Value.Kind, queued.Kind);
        Assert.Equal(factory.Value.ActorId, queued.ActorId);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void ThroughTheHost_EscapeClearsThePinFirst_ThenClosesTheScreen()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "escape"), Assets(), developer: false);
        h.Key(Keys.K);
        h.Idle(30);
        ProgressionScreen s = h.Ui.Progression!;
        (int a, _) = TwoAvailable(s);
        Assert.True(a >= 0);
        (double x, double y) = Centre(s, a);
        h.Click(x, y);
        Assert.Equal(s.Graph.Vertices[a].ContentIndex, s.Pin);
        Assert.True(s.Docked && !s.DrawerOpen);   // 1280 px: since D-072 the detail panel is docked in its own column
        h.Key(Keys.Escape);
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(-1, s.Pin);
        Assert.False(s.DrawerOpen);
        h.Key(Keys.Escape);
        Assert.False(h.Ui.ProgressionOpen);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheCivicsChip_CarriesTheBank_AndItsHoverCardStatesBankAndRate()
    {
        UiSession session = UiSession.Start(42);
        for (int t = 0; t < 3; t++) session.EndTurn();   // some Civics Points banked
        double bank = ResearchQuery.CivicsPointsBalance(session.World, UiPlayer.Empire);
        Assert.True(bank > 0, "anti-vacuity: Civics Points banked");
        using UiFrameHarness h = UiFrameHarness.Start(session, Path.Combine(_root, "chip"), Assets(), developer: false, 1920, 1009);
        h.Idle(3);
        string figure = StatusFigures.Bank(bank);
        Assert.Contains(h.Ui.FrameText.Texts(FrameSurface.Status), t => t.StartsWith("Civics", StringComparison.Ordinal) && t.EndsWith(figure + " banked", StringComparison.Ordinal));
        UiControl chip = h.Ui.Controls.Find("band-civics") ?? throw new InvalidOperationException("no Civics chip");
        Assert.Empty(h.Ui.CivicsCardLines);
        h.MoveTo((chip.X0 + chip.X1) / 2.0, (chip.Y0 + chip.Y1) / 2.0);
        Assert.Equal("Banked: " + figure + " (kept until spent, never lost)", h.Ui.CivicsCardLines[0]);
        Assert.StartsWith("Per turn: +", h.Ui.CivicsCardLines[1]);
        Assert.Contains(StatusFigures.CivicsCardTitle, h.Ui.FrameText.Texts(FrameSurface.Status));
        h.MoveTo(chip.X0 - 400, chip.Y1 + 300);
        Assert.Empty(h.Ui.CivicsCardLines);
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }
}
