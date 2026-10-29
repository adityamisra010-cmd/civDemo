using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using System.Globalization;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// RP / CP LIFECYCLE AUDIT, case (k) at the UI (research-cost-pass/rp-cp-lifecycle.md): every RP and CP number the research
/// screen's view model (ResearchSnapshot) and the action surface's research line show, compared headlessly with the
/// authoritative ResearchQuery value — and with what the NEXT step actually does (the session's own production preview, or a
/// research-only step). The stepped real world (SteppedWorldFixture: the shipped content, a player target set) is the input.
/// Since the display fix the (k2)–(k5) facts pin the CORRECTED screens (each states what the screen showed before it):
/// the CP a turn is the NEXT step's accrual, and with destination routing every research line names the node the step
/// FUNDS (a route node, an assist) or says the lane waits.
/// </summary>
public class ResearchPointDisplayTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly EraTable Era = UiSession.ProductionEra();

    [Fact]
    public void Snapshot_EveryRpAndCpNumber_IsResearchQuerys_AndTheNextStepFundsWhatTheRouteShows()
    {
        ResearchContent c = fx.Content;
        IReadOnlyWorldState w = fx.Session.World;
        ResearchSnapshot s = Screen(c, w).Snapshot!;   // as the progression screen builds it (the next step's dt)
        Assert.Equal(ResearchQuery.ResearchPointPool(w, c, Me), s.PointsPerTurn);
        Assert.Equal(ResearchQuery.CivicsPointsAccount(w, Me).BalanceMilli / 1000.0, s.CivicsBalance);
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, Me, NextDt(w)) / 1000.0, s.CivicsPerTurn);
        bool[] completed = ResearchQuery.CompletedMask(w, c, Me);
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchNodeView v = s.Nodes[i];
            bool done = completed[i] && !ResearchQuery.IsLevelled(c, i);
            Assert.Equal(done ? 0.0 : ResearchQuery.Progress(w, Me, c.Nodes[i].Key), v.Progress);
            Assert.Equal(ResearchQuery.EffectiveCost(w, c, Me, i), v.EffectiveCost);
        }
        foreach (ResearchTree lane in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchRoute shown = s.RouteOn(lane), truth = ResearchQuery.Route(w, c, Me, lane);
            Assert.Equal((truth.Destination, truth.Current, truth.Next, truth.RemainingCost, truth.Assisting),
                (shown.Destination, shown.Current, shown.Next, shown.RemainingCost, shown.Assisting));
        }

        // What the next End Turn does (the production step on this world and this turn's queued orders): within an era band the
        // CP accrued equals the CP a turn shown, and the technology route's CURRENT node gains min(RP, its remaining cost).
        Assert.Equal(Era.DtDaysAt(w.Clock.SimDays), w.Clock.DtDays);   // precondition: not on a band boundary
        IReadOnlyWorldState next = fx.Session.PreviewNextWorld();
        Assert.Equal(s.CivicsPerTurn * 1000.0,
            ResearchQuery.CivicsPointsAccount(next, Me).GeneratedMilli - ResearchQuery.CivicsPointsAccount(w, Me).GeneratedMilli);
        ResearchNodeId current = Assert.IsType<ResearchNodeId>(s.TechnologyRoute.Current);
        int ci = c.IndexOf(current);
        double before = ResearchQuery.Progress(w, Me, current), cost = ResearchQuery.EffectiveCost(w, c, Me, ci);
        if (ResearchQuery.IsCompleted(next, Me, current)) Assert.True(before + s.PointsPerTurn >= cost);
        else Assert.Equal(before + s.PointsPerTurn, ResearchQuery.Progress(next, Me, current));
    }

    [Fact]
    public void ActionSurfaceResearchLine_RpAndCp_AreResearchQuerys()
    {
        UiSession session = fx.Session;
        IReadOnlyWorldState w = session.World;
        ResearchContent c = fx.Content;
        int capital = EmpireQuery.TryGetCapital(w, Me, out SettlementId cap) ? cap.Value : -1;
        ActionSurfaceModel m = ActionSurface.ForSession(session, Era, capital, EraThemes.For(UiEras.Of(w, session.Config.Ages, Me)));
        ResearchBlock r = Assert.IsType<ResearchBlock>(m.Research);
        Assert.Equal(ResearchQuery.ResearchPointPool(w, c, Me), r.PointsPerTurn);
        CivicsLine civics = Assert.IsType<CivicsLine>(r.Civics);
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, Me, NextDt(w)) / 1000.0, civics.PointsPerTurn);
        Assert.Equal(ResearchQuery.CivicsPointsBalance(w, Me), civics.Balance);
    }

    [Fact]
    public void CpPerTurnShown_AtTheFirstTurnOfANewEraBand_IsTheNextStepsAccrual_OnEveryScreen()
    {
        // FIXED DISPLAY DISCREPANCY (k5; before the fix every surface showed the dt-10 accrual here — twice what the step
        // accrues). The same world placed on the first day of the Bronze/Iron band (−1500 = day 900 000), its clock still
        // recording the Neolithic step that brought it there (dt 10 years). The next step runs at the band's dt 5
        // (TurnExecutor reads dt from the era table at the date); the progression screen, the action surface and the Glass
        // Box card now all show that step's accrual, and the screen's "~N turns" estimates divide by it.
        ResearchContent c = fx.Content;
        WorldState w = ((WorldState)fx.Session.World).Clone();
        w.Clock = new SimClock(w.Clock.Turn, 2500L * SimClock.YearDays, 10L * SimClock.YearDays);
        Assert.Equal(5L * SimClock.YearDays, Era.DtDaysAt(w.Clock.SimDays));
        long staleMilli = ResearchQuery.CivicsPointsMilli(w, c, Me, 10.0);
        long accruedMilli = ResearchQuery.CivicsPointsMilli(w, c, Me, 5.0);
        var research = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]);
        WorldState next = research.Step(w);
        Assert.Equal(accruedMilli,
            ResearchQuery.CivicsPointsAccount(next, Me).GeneratedMilli - ResearchQuery.CivicsPointsAccount(w, Me).GeneratedMilli);
        // ADR-054 (2026-10-27) RE-RIG: Civics Points are now PER TURN (tuning.civicsPoints.perTurn), so the stale dt-10 figure
        // and the next step's dt-5 accrual are the same number; the surfaces must still show the accrual the next step makes.
        Assert.Equal(staleMilli, accruedMilli);

        // The progression screen (its snapshot, and a civic's "~N turns").
        ProgressionScreen screen = Screen(c, w);
        Assert.Equal(accruedMilli / 1000.0, screen.Snapshot!.CivicsPerTurn);
        bool[] done = ResearchQuery.CompletedMask(w, c, Me);
        bool[] open = ResearchQuery.AvailableMask(c, done);
        int civic = -1;
        for (int i = 0; i < c.Nodes.Count; i++)
            if (c.Nodes[i].Tree == ResearchTree.Civics && open[i] && !done[i] && screen.Snapshot.Nodes[i].State == NodeState.Available) { civic = i; break; }
        Assert.True(civic >= 0, "an available civic");
        ResearchNodeView cv = screen.Snapshot.Nodes[civic];
        double turns = Math.Ceiling(Math.Max(0, cv.EffectiveCost - cv.Progress) / (accruedMilli / 1000.0));
        Assert.Contains("~" + turns.ToString("0", CultureInfo.InvariantCulture) + " turns", screen.StateLine(civic), StringComparison.Ordinal);

        // The action surface's civics line.
        ActionSurfaceModel m = ActionSurface.Build(new ActionSurfaceInput(w, null, fx.Session.Config, Era, Me, -1, [],
            id => fx.Session.Names.Name(id), EraThemes.For(UiEras.Of(w, fx.Session.Config.Ages, Me))));
        Assert.Equal(accruedMilli / 1000.0, Assert.IsType<CivicsLine>(Assert.IsType<ResearchBlock>(m.Research).Civics).PointsPerTurn);

        // The Glass Box research card, read with the next step's context (as the UI's inspector reads it).
        string card = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Research, ActionQueryContext.ForNextStep(w, Era)).StatusLine;
        Assert.EndsWith("civics points banked, +" + (accruedMilli / 1000.0).ToString("#,0.#", CultureInfo.InvariantCulture) + " a turn.", card);
    }

    [Fact]
    public void RouteNode_TheScreensNameTheNodeTheStepFunds_TowardTheDestination()
    {
        // FIXED DISPLAY DISCREPANCY (k2 on the screens). A Technology destination D that is not open: the step funds a route
        // node x (ResearchQuery.Route's Current, what ResearchRoutePlanner.Fund gives the engine). Before the fix x's card on
        // the tree read "Available · ~N turns" and the status band "Research: D 0% +R/turn"; now x's card reads
        // "Researching · toward D · …" and the band names x and x's progress. The next step adds the RP shown to x.
        ResearchContent c = fx.Content;
        (WorldState w, ResearchRoute route) = WithTechnologyDestination(c, r => r.Current is { } cur && cur != r.Destination && !r.Assisting);
        ResearchNodeId x = route.Current!.Value, d = route.Destination!.Value;
        ProgressionScreen screen = Screen(c, w);
        Assert.StartsWith("Researching · toward " + Name(c, d) + " · ", screen.StateLine(c.IndexOf(x)));
        ResearchFigure band = StatusFigures.Research(Surface(w).Research);
        Assert.False(band.Idle);
        Assert.StartsWith("Research: " + Short(Name(c, x)) + " ", band.Text);
        string card = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Research, ActionQueryContext.ForNextStep(w, Era)).StatusLine;
        Assert.StartsWith("Researching " + Name(c, x) + " (toward " + Name(c, d) + "): ", card);
        WorldState next = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]).Step(w);
        double rp = ResearchQuery.ResearchPointPool(w, c, Me), before = ResearchQuery.Progress(w, Me, x);
        if (!ResearchQuery.IsCompleted(next, Me, x)) Assert.Equal(before + rp, ResearchQuery.Progress(next, Me, x));
    }

    [Fact]
    public void AssistingLane_IsShownResearchingForTheOtherTree_NotIdle()
    {
        // FIXED DISPLAY DISCREPANCY (k3 on the screens). No Technology destination; a Civics destination whose route needs a
        // technology: the free Technology lane ASSISTS (its RP are spent on that technology). Before the fix the band read
        // "Research idle - choose [K]", the action surface "Learning - idle … research points a turn go unused" and the
        // capsule "Research idle - choose an available node"; now each names the node funded and the destination served.
        ResearchContent c = fx.Content;
        (WorldState w, ResearchRoute tech) = WithCivicsDestinationTheTechnologyLaneAssists(c);
        ResearchNodeId t = tech.Current!.Value;
        ResearchFigure band = StatusFigures.Research(Surface(w).Research);
        Assert.False(band.Idle);
        Assert.StartsWith("Research: " + Short(Name(c, t)) + " ", band.Text);
        DrawList painted = Screen(c, w).Paint(1600, 1000, ApproxTextMeasure.Instance);
        Assert.DoesNotContain(painted.Commands, cmd => cmd is TextCmd tc && tc.Text.StartsWith("Research idle", StringComparison.Ordinal));
        Assert.Contains(painted.Commands, cmd => cmd is TextCmd tc && tc.Text == "ASSISTING");
        string card = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Research, ActionQueryContext.ForNextStep(w, Era)).StatusLine;
        Assert.StartsWith("Researching " + Name(c, t) + " (assisting the Civics tree toward " + Name(c, tech.AssistedDestination) + "): ", card);
        WorldState next = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]).Step(w);
        Assert.True(ResearchQuery.Progress(next, Me, t) > ResearchQuery.Progress(w, Me, t) || ResearchQuery.IsCompleted(next, Me, t));
    }

    [Fact]
    public void WaitingLane_IsShownWaiting_WithItsPointsUnused_NotResearching()
    {
        // FIXED DISPLAY DISCREPANCY (k4 on the screens). A Technology destination whose route can fund no technology now (it
        // waits on a civic) while the Civics lane works an open civic of its own: the step funds nothing on the Technology
        // lane (all RP lost). Before the fix the band read "Research: TD 0% +R/turn"; now it reads "Research waiting: TD"
        // with the attention style, and the Glass Box card says what it waits for and that the RP go unused.
        ResearchContent c = fx.Content;
        (WorldState w, ResearchRoute tech) = WithWaitingTechnologyDestination(c);
        ResearchNodeId td = tech.Destination!.Value;
        ResearchFigure band = StatusFigures.Research(Surface(w).Research);
        Assert.True(band.Idle);
        Assert.Equal("Research waiting: " + Short(Name(c, td)), band.Text);
        string card = InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Research, ActionQueryContext.ForNextStep(w, Era)).StatusLine;
        Assert.StartsWith("Waiting on the Civics tree: " + Name(c, td) + " needs ", card);
        Assert.Contains(" research points go unused this turn.", card, StringComparison.Ordinal);
        // Measured: the next step funds no technology (RP lost) — the progress of every technology is unchanged.
        WorldState next = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]).Step(w);
        for (int i = 0; i < c.TechnologyCount; i++)
            if (!ResearchQuery.IsCompleted(next, Me, c.Nodes[i].Key))
                Assert.Equal(ResearchQuery.Progress(w, Me, c.Nodes[i].Key), ResearchQuery.Progress(next, Me, c.Nodes[i].Key));
    }

    [Fact]
    public void ActionSurfaceAndCapsule_RouteFunding_IsResearchQuerysRoute()
    {
        // The new view-model fields behind the k2–k4 lines: the action surface's RouteFunding and the capsule's name line
        // are ResearchQuery.Route / Activity / WaitingOn, never recomputed.
        ResearchContent c = fx.Content;
        (WorldState w2, ResearchRoute r2) = WithTechnologyDestination(c, r => r.Current is { } cur && cur != r.Destination && !r.Assisting);
        ResearchBlock b2 = Assert.IsType<ResearchBlock>(Surface(w2).Research);
        RouteFunding f2 = Assert.IsType<RouteFunding>(b2.Funding);
        Assert.Equal((LaneActivity.Researching, r2.Current!.Value.Value, Name(c, r2.Destination)), (f2.Activity, f2.Node!.Key, f2.Toward));
        Assert.Equal(ResearchQuery.Progress(w2, Me, r2.Current.Value), f2.Node.Progress);
        Assert.StartsWith("now: " + Name(c, r2.Current) + " - ", ActionSurfaceScreen.FundingLine(f2, "figures", "unused"));
        ProgressionScreen s2 = Screen(c, w2);
        Assert.Equal(Name(c, r2.Destination) + " - now: " + Name(c, r2.Current), s2.CapsuleName(s2.Snapshot!, s2.Snapshot!.Nodes[c.IndexOf(r2.Destination!.Value)]));

        (WorldState w3, ResearchRoute r3) = WithCivicsDestinationTheTechnologyLaneAssists(c);
        ResearchBlock b3 = Assert.IsType<ResearchBlock>(Surface(w3).Research);
        Assert.Null(b3.Effective);
        Assert.False(b3.Idle);
        Assert.Equal((LaneActivity.Assisting, r3.Current!.Value.Value, Name(c, r3.AssistedDestination)), (b3.Funding!.Activity, b3.Funding.Node!.Key, b3.Funding.Toward));

        (WorldState w4, ResearchRoute r4) = WithWaitingTechnologyDestination(c);
        ResearchBlock b4 = Assert.IsType<ResearchBlock>(Surface(w4).Research);
        Assert.True(b4.Waiting);
        List<ResearchNodeId> on = ResearchQuery.WaitingOn(c, r4);
        Assert.NotEmpty(on);
        Assert.StartsWith(Name(c, on[0]), b4.Funding!.WaitingOn);
        Assert.StartsWith("waiting on the Civics tree (needs " + Name(c, on[0]), ActionSurfaceScreen.FundingLine(b4.Funding, "figures", "unused"));
        ProgressionScreen s4 = Screen(c, w4);
        Assert.StartsWith(Name(c, r4.Destination) + " - waiting on " + Name(c, on[0]), s4.CapsuleName(s4.Snapshot!, s4.Snapshot!.Nodes[c.IndexOf(r4.Destination!.Value)]));
    }

    // ------------------------------------------------------------------ rigs on the stepped world

    private static double NextDt(IReadOnlyWorldState w) => ActionQueryContext.ForNextStep(w, Era).NextDtYears!.Value;

    private ProgressionScreen Screen(ResearchContent c, IReadOnlyWorldState w)
    {
        var s = new ProgressionScreen(c, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        s.Paint(1600, 1000, ApproxTextMeasure.Instance);
        return s;
    }

    private ActionSurfaceModel Surface(IReadOnlyWorldState w) =>
        ActionSurface.Build(new ActionSurfaceInput(w, null, fx.Session.Config, Era, Me, -1, [], id => fx.Session.Names.Name(id),
            EraThemes.For(UiEras.Of(w, fx.Session.Config.Ages, Me))));

    private static string Name(ResearchContent c, ResearchNodeId? key) => key is { } k ? c.Nodes[c.IndexOf(k)].Name : "-";

    private static string Short(string name) =>
        name.Length <= StatusFigures.NameChars ? name : name[..(StatusFigures.NameChars - 3)].TrimEnd() + "...";

    /// <summary>The stepped world with the player's target rows replaced by <paramref name="keys"/> (dense indices).</summary>
    private WorldState WithTargets(ResearchContent c, params int[] keys)
    {
        WorldState w = ((WorldState)fx.Session.World).Clone();
        var keep = new List<ResearchTargetRow>();
        for (int r = 0; r < w.ResearchTargets.Count; r++)
            if (w.ResearchTargets[r].Polity.Value != Me.Value) keep.Add(w.ResearchTargets[r]);
        w.ResearchTargets.Clear();
        foreach (ResearchTargetRow row in keep) w.ResearchTargets.Add(row);
        foreach (int k in keys) w.ResearchTargets.Add(new ResearchTargetRow(Me, c.Nodes[k].Key));
        return w;
    }

    /// <summary>The first incomplete technology (index order) whose Technology route, as the only target, satisfies <paramref name="want"/>.</summary>
    private (WorldState, ResearchRoute) WithTechnologyDestination(ResearchContent c, Func<ResearchRoute, bool> want)
    {
        bool[] done = ResearchQuery.CompletedMask(fx.Session.World, c, Me);
        for (int i = 0; i < c.TechnologyCount; i++)
        {
            if (done[i]) continue;
            WorldState w = WithTargets(c, i);
            ResearchRoute r = ResearchQuery.Route(w, c, Me, ResearchTree.Technology);
            if (want(r)) return (w, r);
        }
        throw new InvalidOperationException("no such technology destination on the stepped world");
    }

    /// <summary>No Technology target; the first incomplete civic (index order) whose route the free Technology lane assists.</summary>
    private (WorldState, ResearchRoute) WithCivicsDestinationTheTechnologyLaneAssists(ResearchContent c)
    {
        bool[] done = ResearchQuery.CompletedMask(fx.Session.World, c, Me);
        for (int i = c.TechnologyCount; i < c.Nodes.Count; i++)
        {
            if (done[i] || c.Nodes[i].Tree != ResearchTree.Civics) continue;
            WorldState w = WithTargets(c, i);
            ResearchRoute r = ResearchQuery.Route(w, c, Me, ResearchTree.Technology);
            if (r is { Assisting: true, Current: not null }) return (w, r);
        }
        throw new InvalidOperationException("no civic destination the Technology lane assists on the stepped world");
    }

    /// <summary>A Technology destination that WAITS (its route funds no technology now) while the Civics lane works an
    /// open civic of its own (so the Technology lane has no route to assist). The stepped world has none as stepped, so the
    /// rig completes every TECHNOLOGY on the route of the first technology (index order) whose route also needs a civic:
    /// what remains of the route is civics only (the cross-tree wait of rp-cp-lifecycle d2 / k4).</summary>
    private (WorldState, ResearchRoute) WithWaitingTechnologyDestination(ResearchContent c)
    {
        bool[] done = ResearchQuery.CompletedMask(fx.Session.World, c, Me);
        bool[] open = ResearchQuery.AvailableMask(c, done);
        int civic = -1;
        for (int i = c.TechnologyCount; i < c.Nodes.Count; i++) if (c.Nodes[i].Tree == ResearchTree.Civics && open[i] && !done[i]) { civic = i; break; }
        Assert.True(civic >= 0);
        for (int i = 0; i < c.TechnologyCount; i++)
        {
            if (done[i] || open[i]) continue;
            WorldState w = WithTargets(c, i, civic);
            ResearchRoute r = ResearchQuery.Route(w, c, Me, ResearchTree.Technology);
            bool civicOnRoute = false, levelled = false;
            foreach (RouteBlocker b in r.Blockers)
            {
                civicOnRoute |= c.Nodes[b.Index].Tree == ResearchTree.Civics;
                levelled |= ResearchQuery.IsLevelled(c, b.Index);
            }
            if (!civicOnRoute || levelled) continue;
            foreach (RouteBlocker b in r.Blockers)
                if (c.Nodes[b.Index].Tree == ResearchTree.Technology) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, b.Key));
            r = ResearchQuery.Route(w, c, Me, ResearchTree.Technology);
            if (r.Destination is not null && r.Current is null) return (w, r);
        }
        throw new InvalidOperationException("no waiting technology destination on the stepped world");
    }

    [Fact]
    public void Snapshot_ShowsTheRpPoolUnchanged_WhenTheTechnologyLaneFundsNothing_NoWasteFigureExists()
    {
        // With the technology destination cleared, the step funds nothing on that lane (all RP lost, D-044 R20-D). The snapshot
        // still shows the full pool as "Research per turn" and the route as None; there is no field for idle or lost RP —
        // the capsule's "Research idle" text is the only signal.
        ResearchContent c = fx.Content;
        WorldState w = ((WorldState)fx.Session.World).Clone();
        var keep = new List<ResearchTargetRow>();
        for (int r = 0; r < w.ResearchTargets.Count; r++)
            if (!(w.ResearchTargets[r].Polity.Value == Me.Value && c.IndexOf(w.ResearchTargets[r].Node) is int i and >= 0
                && c.Nodes[i].Tree == ResearchTree.Technology))
                keep.Add(w.ResearchTargets[r]);
        w.ResearchTargets.Clear();
        foreach (ResearchTargetRow row in keep) w.ResearchTargets.Add(row);
        ResearchSnapshot s = ResearchSnapshot.Build(w, c, Me);
        Assert.Null(s.TargetIndex);
        Assert.Null(s.TechnologyRoute.Destination);
        Assert.Null(s.TechnologyRoute.Current);
        Assert.Equal(ResearchQuery.ResearchPointPool(w, c, Me), s.PointsPerTurn);
        Assert.True(s.PointsPerTurn > 0.0);
    }
}
