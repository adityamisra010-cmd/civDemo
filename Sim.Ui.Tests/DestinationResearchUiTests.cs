using System.Globalization;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-048 — the research panel under the Age sweep and destination research: every node's eligibility, lock reason and
/// sweep line are the ENGINE's (ResearchQuery.AvailableMask / SweepBlock); the ROUTE block shows exactly
/// ResearchQuery.Route (destination, current, next, blockers by reason); a click on an unavailable node issues the
/// destination order the factory builds; CANCEL DESTINATION issues the lane's existing clear order. No UI-side eligibility
/// or route computation exists to disagree with.
/// </summary>
public class DestinationResearchUiTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const double W = 1600, H = 1000;

    private static readonly Lazy<ResearchContent> Both = new(() => ContentWith(sweep: true, routing: true));

    /// <summary>The shipped research.json with the two ADR-048 switches set (a test-only variant).</summary>
    private static ResearchContent ContentWith(bool sweep, bool routing)
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        doc["tuning"]!["ageSweep"] = sweep;
        doc["tuning"]!["destinationRouting"] = routing;
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(doc.ToJsonString(), Sim.Core.Systems.GoodsConfigLoader.Load(goods));
    }

    /// <summary>The stepped world with the player's Technology target replaced by <paramref name="destination"/>.</summary>
    private WorldState WithDestination(ResearchContent c, int destination)
    {
        WorldState w = fx.Session.World.Clone();
        var keep = new List<ResearchTargetRow>();
        for (int i = 0; i < w.ResearchTargets.Count; i++)
        {
            ResearchTargetRow row = w.ResearchTargets[i];
            int index = c.IndexOf(row.Node);
            if (row.Polity.Value == Me.Value && index >= 0 && c.Nodes[index].Tree == ResearchTree.Technology) continue;
            keep.Add(row);
        }
        w.ResearchTargets.Clear();
        foreach (ResearchTargetRow row in keep) w.ResearchTargets.Add(row);
        if (destination >= 0) w.ResearchTargets.Add(new ResearchTargetRow(Me, c.Nodes[destination].Key));
        return w;
    }

    /// <summary>The first incomplete Age-3 technology (the sweep blocks it from this early world).</summary>
    private static int Age3(ResearchContent c, IReadOnlyWorldState w)
    {
        bool[] done = ResearchQuery.CompletedMask(w, c, Me);
        for (int i = 0; i < c.TechnologyCount; i++) if (c.Nodes[i].AgeNumber == 3 && !done[i]) return i;
        throw new InvalidOperationException("no Age-3 technology left");
    }

    private ProgressionScreen Screen(ResearchContent c, IReadOnlyWorldState w)
    {
        var s = new ProgressionScreen(c, Me);
        s.Refresh(w);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        return s;
    }

    private static string Name(ResearchContent c, ResearchNodeId? key) => key is { } k ? c.Nodes[c.IndexOf(k)].Name : "-";

    [Fact]
    public void EveryNode_TheScreensEligibilityAndSweepAreTheEngines()
    {
        ResearchContent c = Both.Value;
        WorldState w = WithDestination(c, -1);
        ProgressionScreen s = Screen(c, w);
        bool[] done = ResearchQuery.CompletedMask(w, c, Me);
        bool[] engine = ResearchQuery.AvailableMask(c, done);
        int sweepBlocked = 0;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchNodeView v = s.Snapshot!.Nodes[i];
            Assert.Equal(engine[i], v.Available);
            ResearchQuery.SweepState sweep = ResearchQuery.SweepBlock(c, done, i);
            Assert.Equal(!done[i] && sweep.Blocked, v.Lock.HasFlag(LockReason.AgeSweep));
            if (!v.Lock.HasFlag(LockReason.AgeSweep)) { Assert.Null(s.SweepLine(i)); continue; }
            sweepBlocked++;
            var names = new List<string>();
            foreach (ResearchNodeId k in sweep.Outstanding) names.Add(c.Nodes[c.IndexOf(k)].Name);
            Assert.Equal("Age " + sweep.CohortAge.ToString(CultureInfo.InvariantCulture) + " sweep: "
                + sweep.Outstanding.Length.ToString(CultureInfo.InvariantCulture) + " nodes outstanding: " + string.Join(", ", names),
                s.SweepLine(i));
        }
        Assert.True(sweepBlocked > 100, "the sweep should block every later-Age node of this early world");
    }

    [Fact]
    public void RouteBlock_ShowsExactlyTheEnginesRoute()
    {
        ResearchContent c = Both.Value;
        WorldState w0 = fx.Session.World;
        int dest = Age3(c, w0);
        WorldState w = WithDestination(c, dest);
        ProgressionScreen s = Screen(c, w);
        ResearchRoute engine = ResearchQuery.Route(w, c, Me, ResearchTree.Technology);
        ResearchRoute shown = s.Snapshot!.RouteOn(ResearchTree.Technology);
        Assert.Equal(c.Nodes[dest].Key, engine.Destination);
        Assert.Equal((engine.Destination, engine.Current, engine.Next, engine.RemainingCost, engine.Feasible),
            (shown.Destination, shown.Current, shown.Next, shown.RemainingCost, shown.Feasible));
        Assert.Equal(engine.Ordered, shown.Ordered);
        Assert.Equal(engine.Blockers, shown.Blockers);
        Assert.NotNull(engine.Current);

        List<string> lines = s.RouteLines(ResearchTree.Technology);
        Assert.Equal("Destination: " + c.Nodes[dest].Name, lines[0]);
        Assert.Equal("Current research: " + Name(c, engine.Current), lines[1]);
        Assert.Equal("Next on route: " + Name(c, engine.Next), lines[2]);
        int sweepLines = 0;
        for (int k = 1; k <= 9; k++)
        {
            List<RouteBlocker> group = engine.BlockersOf(RouteReason.Sweep, k);
            if (group.Count == 0) continue;
            var names = new List<string>();
            foreach (RouteBlocker b in group) names.Add(c.Nodes[b.Index].Name);
            string expected = "Age " + k.ToString(CultureInfo.InvariantCulture) + " sweep: " + group.Count.ToString(CultureInfo.InvariantCulture)
                + (group.Count == 1 ? " node" : " nodes") + " outstanding: " + string.Join(", ", names);
            Assert.Contains(expected, lines);
            sweepLines++;
        }
        Assert.Equal(1, sweepLines);   // an Age-3 destination: the Age-1 sweep
        // The panel paints the block (the intro panel, no node selected) and the capsule names the destination.
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        var texts = new List<string>();
        foreach (DrawCmd cmd in d.Commands) if (cmd is TextCmd t) texts.Add(t.Text);
        Assert.Contains("ROUTE", texts);
        Assert.Contains("DESTINATION", texts);
        Assert.Contains(texts, t => t.StartsWith("Current research:", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("Age 1 sweep:", StringComparison.Ordinal));
    }

    [Fact]
    public void ClickingAnUnavailableNode_IssuesTheDestinationOrder_OnlyWhenRoutingIsOn()
    {
        ResearchContent on = Both.Value;
        WorldState w = WithDestination(on, -1);
        int pick = Age3(on, w);
        ProgressionScreen s = Screen(on, w);
        Assert.False(s.Snapshot!.Nodes[pick].Available);
        s.Selected = pick;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        HitRegion btn = Find(s, HitKind.SetTarget, pick);
        ProgressionCommand cmd = s.Click(btn.Rect.CenterX, btn.Rect.CenterY);
        OrderRecord expected = ResearchOrderFactory.SetTarget(w, on, Me, on.Nodes[pick].Key)!.Value;
        Assert.Equal(expected, cmd.Order);
        Assert.Equal(on.Nodes[pick].Key.Value, cmd.Order!.Value.TargetId);

        // Routing off (D-063: the shipped content ships it on since tranche 2, so the control is the same file with only
        // tuning.destinationRouting off): the same locked node offers no order.
        ResearchContent off = ContentWith(sweep: true, routing: false);
        ProgressionScreen t = Screen(off, w);
        t.Selected = pick;
        t.Paint(W, H, ApproxTextMeasure.Instance);
        foreach (HitRegion h in t.Hits) Assert.False(h.Kind == HitKind.SetTarget && h.Arg == pick);
        Assert.Null(ResearchOrderFactory.SetTarget(w, off, Me, off.Nodes[pick].Key));
        Assert.Empty(t.RouteLines(ResearchTree.Technology));
        foreach (HitRegion h in t.Hits) Assert.NotEqual(HitKind.CancelDestination, h.Kind);
    }

    [Fact]
    public void CancelDestination_IssuesTheLanesClearOrder_Once()
    {
        ResearchContent c = Both.Value;
        WorldState w = WithDestination(c, Age3(c, fx.Session.World));
        ProgressionScreen s = Screen(c, w);
        // D-063 (tranche 2): the route block above the button lists every outstanding node of the Age-1 sweep (41 names on
        // this world), so at 1600 x 1000 the button sits below the panel's fold — scroll the panel to it, as a player would.
        for (int k = 0; k < 40 && !s.Hits.Any(h => h.Kind == HitKind.CancelDestination); k++)
        {
            s.Wheel(s.DetailRect.CenterX, s.DetailRect.CenterY, -1);
            s.Paint(W, H, ApproxTextMeasure.Instance);
        }
        HitRegion cancel = Find(s, HitKind.CancelDestination, (int)ResearchTree.Technology);
        ProgressionCommand cmd = s.Click(cancel.Rect.CenterX, cancel.Rect.CenterY);
        Assert.Equal(ResearchQuery.ClearTargetOrder(w, Me, ResearchTree.Technology), cmd.Order);
        Assert.Equal(ResearchQuery.ClearTechnologyTargetId, cmd.Order!.Value.TargetId);
        Assert.Equal(ResearchTree.Technology, cmd.CancelLane);                 // the host logs it via the session's lane clear
        Assert.Null(cmd.Node);
        Assert.True(s.CancelPending(ResearchTree.Technology));
        s.Paint(W, H, ApproxTextMeasure.Instance);
        foreach (HitRegion h in s.Hits) Assert.NotEqual(HitKind.CancelDestination, h.Kind);   // not offered twice
    }

    private static HitRegion Find(ProgressionScreen s, HitKind kind, int arg)
    {
        foreach (HitRegion h in s.Hits) if (h.Kind == kind && h.Arg == arg) return h;
        throw new InvalidOperationException($"no {kind} {arg}");
    }
}
