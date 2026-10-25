using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Progression;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// RP / CP LIFECYCLE AUDIT, case (k) at the UI (research-cost-pass/rp-cp-lifecycle.md): every RP and CP number the research
/// screen's view model (ResearchSnapshot) and the action surface's research line show, compared headlessly with the
/// authoritative ResearchQuery value — and with what the NEXT step actually does (the session's own production preview, or a
/// research-only step). The stepped real world (SteppedWorldFixture: the shipped content, a player target set) is the input.
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
        ResearchSnapshot s = ResearchSnapshot.Build(w, c, Me);
        Assert.Equal(ResearchQuery.ResearchPointPool(w, c, Me), s.PointsPerTurn);
        Assert.Equal(ResearchQuery.CivicsPointsAccount(w, Me).BalanceMilli / 1000.0, s.CivicsBalance);
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, Me, w.Clock.DtYears) / 1000.0, s.CivicsPerTurn);
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
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, Me, w.Clock.DtYears) / 1000.0, civics.PointsPerTurn);
        Assert.Equal(ResearchQuery.CivicsPointsBalance(w, Me), civics.Balance);
    }

    [Fact]
    public void CpPerTurnShown_AtTheFirstTurnOfANewEraBand_IsThePreviousBandsDt_TwiceWhatTheStepAccrues()
    {
        // DISPLAY DISCREPANCY. The same world placed on the first day of the Bronze/Iron band (−1500 = day 900 000), its clock
        // still recording the Neolithic step that brought it there (dt 10 years). The screen shows the dt-10 accrual; the next
        // step runs at the band's dt 5 (TurnExecutor reads dt from the era table at the date) and accrues half of it.
        ResearchContent c = fx.Content;
        WorldState w = ((WorldState)fx.Session.World).Clone();
        w.Clock = new SimClock(w.Clock.Turn, 2500L * SimClock.YearDays, 10L * SimClock.YearDays);
        Assert.Equal(5L * SimClock.YearDays, Era.DtDaysAt(w.Clock.SimDays));
        ResearchSnapshot s = ResearchSnapshot.Build(w, c, Me);
        long shownMilli = ResearchQuery.CivicsPointsMilli(w, c, Me, 10.0);
        long accruedMilli = ResearchQuery.CivicsPointsMilli(w, c, Me, 5.0);
        Assert.Equal(shownMilli / 1000.0, s.CivicsPerTurn);
        var research = new TurnExecutor(Era, [SystemCatalog.Research(fx.Session.Config)]);
        WorldState next = research.Step(w);
        Assert.Equal(accruedMilli,
            ResearchQuery.CivicsPointsAccount(next, Me).GeneratedMilli - ResearchQuery.CivicsPointsAccount(w, Me).GeneratedMilli);
        Assert.True(shownMilli > accruedMilli);
        Assert.InRange(shownMilli - 2 * accruedMilli, 0L, 1L);   // floor(2x) vs 2·floor(x): equal or one milli apart
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
