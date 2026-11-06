using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// DIR5 §4.1 (C31): "If an empire loses all its cities, its leader is executed." The transition happens exactly at the instant the
/// last city is lost, with simultaneous losses, captures vs razes and re-captures pinned, and the state transition, the war record,
/// the leader fate and the Ledger agree.
/// </summary>
public class LeaderExecutionTests
{
    /// <summary>Polity 0's seat moved to the fort (5): losing cities does not take its leader captive first.</summary>
    private static OutcomePolity[] SeatInFort(OutcomePolity[] p) { p[0] = p[0] with { Seat = 5 }; return p; }

    /// <summary>Polity 0 holds only city 0 (city 1 is polity 2's).</summary>
    private static OutcomeSettlement[] OnlyCity0(OutcomeSettlement[] x) { x[1] = x[1] with { Controller = 2 }; return x; }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void LastCityLost_ExecutesAtThatInstant_PulseExact(long offset)
    {
        long t = B(3) + offset;   // the start of War Pulse 3 and its two neighbours
        var b = Build((s, _) => { Capture(s, B(1) + 2 * Day, 0, 1); Capture(s, t, 1, 2); }, polities: SeatInFort);
        b.R.RunUntil(t - 1);
        Assert.Equal(LeaderFate.Reigning, b.W.FateOf(0));
        Assert.Equal(1, b.W.CityCount(0));
        Assert.Empty(b.W.Fates);
        b.R.RunUntil(t);
        Assert.Equal(LeaderFate.Executed, b.W.FateOf(0));
        var f = Assert.Single(b.W.Fates);
        Assert.Equal(t, f.AtMs);
        Assert.Equal(PulseOracle(t), f.Pulse);
        Assert.Equal(offset < 0 ? 2 : 3, f.Pulse);
        Assert.Equal(Cal.TurnOf(new SimInstant(t)), f.Turn);
        Assert.Equal((0, 100, 0, 2, 1), (f.Polity, f.LeaderId, f.WarId, f.ByPolity, f.SettlementId));
        Assert.Equal(((byte)LeaderFate.Reigning, (byte)LeaderFate.Executed, (byte)FateCause.LastCityLost), (f.From, f.To, f.Cause));
        // the leader is a person: death is a Ledger flow to the executed sink, exactly one
        Assert.Equal(0, b.W.Ledger.Balance(b.W.LeaderAccountOf(0)));
        Assert.Equal(1, b.W.Ledger.Balance(b.W.ExecutedSink));
        Assert.True(b.W.IsExtinct(0));
        Assert.Equal(0L, b.W.CityCount(0));
        Assert.Equal(0, b.W.ControllerOf(5));    // it still holds the fort, which is not a city
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void LastCityLost_AtATurnBoundary_RecordsTheTurnOfTheInstant(long offset)
    {
        long turn = Cal.TurnOf(new SimInstant(B(4))) + 1;
        long start = Cal.TurnStart(turn).Ms;
        long t = start + offset;
        var b = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, t, 1, 1); }, polities: SeatInFort);
        b.R.RunUntil(t);
        var f = Assert.Single(b.W.Fates);
        Assert.Equal(t, f.AtMs);
        Assert.Equal(offset < 0 ? turn - 1 : turn, f.Turn);
        Assert.Equal(PulseOracle(t), f.Pulse);
        // the battle record of the loss carries the same instant, turn and pulse
        var battle = b.W.Battles[^1];
        Assert.Equal((t, f.Turn, f.Pulse, 1), (battle.EndMs, battle.Turn, battle.Pulse, battle.SettlementId));
    }

    [Fact]
    public void OneCityLeft_NoExecution_AndAFortIsNotACity()
    {
        var b = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, B(2), 5, 2); }, polities: SeatInFort);
        b.R.RunUntil(B(5));
        Assert.Equal(LeaderFate.Captive, b.W.FateOf(0));          // its seat, the fort, was taken: a captive, not an execution
        Assert.Equal(1, b.W.CityCount(0));
        Assert.DoesNotContain(b.W.Fates, f => f.To == (byte)LeaderFate.Executed);
        Assert.Equal(1, b.W.Ledger.Balance(b.W.LeaderAccountOf(0)));
    }

    [Fact]
    public void TwoCitiesLostAtTheSameInstant_OneExecution_AttributedInKeyOrderNotSchedulingOrder()
    {
        long t = B(2) + 3 * Day;
        // scheduled in reverse settlement order: the event key (SourceId = settlement id) decides which loss was last
        var b = Build((s, _) => { Capture(s, t, 1, 2); Capture(s, t, 0, 1); });
        b.R.RunUntil(t - 1);
        Assert.Equal((LeaderFate.Reigning, 2L), (b.W.FateOf(0), b.W.CityCount(0)));
        b.R.RunUntil(t);
        var f = Assert.Single(b.W.Fates);   // its seat (0) fell at the same instant: one transition, Reigning → Executed
        Assert.Equal((t, (byte)LeaderFate.Reigning, (byte)LeaderFate.Executed, 2, 1), (f.AtMs, f.From, f.To, f.ByPolity, f.SettlementId));
        var twin = Build((s, _) => { Capture(s, t, 0, 1); Capture(s, t, 1, 2); });
        twin.R.RunUntil(t);
        Assert.Equal(b.W.StateHashHex(), twin.W.StateHashHex());
    }

    [Fact]
    public void TwoLossesInOnePulse_AtDifferentInstants_ExecuteAtTheSecondOnly()
    {
        long t1 = B(2) + 1 * Day, t2 = B(2) + 5 * Day;
        Assert.Equal(PulseOracle(t1), PulseOracle(t2));
        var b = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 1); }, polities: SeatInFort);
        b.R.RunUntil(t2 - 1);
        Assert.Equal(LeaderFate.Reigning, b.W.FateOf(0));
        Assert.Empty(b.W.Fates);
        b.R.RunUntil(t2);
        var f = Assert.Single(b.W.Fates);
        Assert.Equal((t2, 2L), (f.AtMs, f.Pulse));
    }

    [Fact]
    public void LastCityLostAndAnotherTakenAtTheSameInstant_TheLeaderLives()
    {
        long t = B(2);
        // polity 0 holds only city 0; at t it loses it to polity 1 and takes city 3 from polity 1: it ends the instant with a city
        var b = Build((s, _) => { Capture(s, t, 0, 1); Capture(s, t, 3, 0); }, polities: SeatInFort, settlements: OnlyCity0);
        b.R.RunUntil(t + Day);
        Assert.Equal(LeaderFate.Reigning, b.W.FateOf(0));
        Assert.Empty(b.W.Fates);
        Assert.Equal((1L, 1), (b.W.CityCount(0), b.W.ControllerOf(0)));
        // teeth: without the simultaneous capture the same loss executes the leader at t
        var alone = Build((s, _) => Capture(s, t, 0, 1), polities: SeatInFort, settlements: OnlyCity0);
        alone.R.RunUntil(t);
        Assert.Equal(t, Assert.Single(alone.W.Fates).AtMs);
    }

    [Fact]
    public void SwapOfLastCitiesAtTheSameInstant_NeitherLeaderIsExecuted_BothSeatsFallSoBothAreCaptive()
    {
        long t = B(2);
        // polity 0 holds only city 0 (its seat), polity 1 only city 2 (its seat); each takes the other's at t
        var b = Build((s, _) => { Capture(s, t, 0, 1); Capture(s, t, 2, 0); },
                      settlements: x => { x[1] = x[1] with { Controller = 2 }; x[3] = x[3] with { Controller = 2 }; return x; });
        b.R.RunUntil(t + Day);
        Assert.Equal((1L, 1L), (b.W.CityCount(0), b.W.CityCount(1)));
        Assert.Equal((LeaderFate.Captive, LeaderFate.Captive), (b.W.FateOf(0), b.W.FateOf(1)));
        Assert.Equal((1, 0), (b.W.HeldBy(0), b.W.HeldBy(1)));
        Assert.All(b.W.Fates, f => Assert.Equal((byte)FateCause.SeatTaken, f.Cause));
        Assert.Equal(2, b.W.Ledger.Balance(b.W.LeaderAccountOf(0)) + b.W.Ledger.Balance(b.W.LeaderAccountOf(1)));
    }

    [Fact]
    public void LossThenRecaptureLaterInThePulse_TheExecutionStands_AndThePolityIsNoLongerExtinct()
    {
        long t1 = B(2) + 1 * Day, t2 = B(2) + 4 * Day, t3 = B(3) + 1 * Day;
        // fix r1 (W-R1-5): polity 0 left war 0 at t1 (eliminated; the war ended in conquest), so the later captures are outside any war
        var b = Build((s, _) => { Capture(s, t1, 0, 1); WarOutcomeWorld.ScheduleCapture(s, t2, 0, 0, -1, 5_000); WarOutcomeWorld.ScheduleCapture(s, t3, 0, 1, -1, 5_000); },
                      polities: SeatInFort, settlements: OnlyCity0);
        b.R.RunUntil(t2 - 1);
        Assert.Equal(t1, Assert.Single(b.W.Fates).AtMs);
        Assert.True(b.W.IsExtinct(0));
        b.R.RunUntil(t2);
        Assert.False(b.W.IsExtinct(0));                          // derived: it controls a city again
        Assert.Equal(LeaderFate.Executed, b.W.FateOf(0));         // a fate never reverts: the polity is leaderless (succession: open)
        b.R.RunUntil(t3 + Day);
        Assert.Single(b.W.Fates);                                 // no second execution: there is no living leader
        Assert.True(b.W.IsExtinct(0));
        Assert.Equal(1, b.W.Ledger.Balance(b.W.ExecutedSink));
    }

    [Fact]
    public void SeatTakenFirst_LeaderCaptive_ThenTheLastCity_ExecutedFromCaptivity()
    {
        long t1 = B(1) + Day, t2 = B(4);
        var b = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 2); });
        b.R.RunUntil(t2 - 1);
        var c = Assert.Single(b.W.Fates);
        Assert.Equal((t1, (byte)LeaderFate.Captive, (byte)FateCause.SeatTaken, 1, 0), (c.AtMs, c.To, c.Cause, c.ByPolity, c.SettlementId));
        Assert.Equal((LeaderFate.Captive, 1, -1), (b.W.FateOf(0), b.W.HeldBy(0), b.W.SeatOf(0)));
        b.R.RunUntil(t2);
        var e = b.W.Fates[1];
        // DIR5 §4.1 is unconditional: the rule executes the leader wherever it is; the record names who held it and who took the last city
        Assert.Equal((t2, (byte)LeaderFate.Captive, (byte)LeaderFate.Executed, 2, 1, (byte)FateCause.LastCityLost), (e.AtMs, e.From, e.To, e.ByPolity, e.HeldBy, e.Cause));
    }

    [Fact]
    public void Elimination_WritesTheWarRecord_ParticipantExits_AndTheWarEndsInConquest()
    {
        long t = B(3);
        var b = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, t, 1, 2); });
        b.R.RunUntil(t - 1);
        Assert.Equal((-1L, (byte)WarOutcome.Ongoing), (b.W.WarRows()[0].EndMs, b.W.WarRows()[0].Outcome));
        b.R.RunUntil(t);
        var war = Assert.Single(b.W.WarRows());
        Assert.Equal((t, (byte)WarOutcome.Conquest, 0), (war.EndMs, war.Outcome, war.WinnerSide));
        var parts = b.W.ParticipantRows();
        var p0 = Assert.Single(parts, p => p.Polity == 0);
        Assert.Equal((t, (byte)ParticipantExit.Eliminated, (byte)1), (p0.LeftMs, p0.Exit, p0.Side));
        Assert.All(parts.Where(p => p.Polity != 0), p => Assert.Equal(-1, p.LeftMs));
        // the war record and the leader fate agree (DIR5 §4.1)
        Assert.Equal(war.EndMs, b.W.Fates[^1].AtMs);
    }

    [Fact]
    public void WhenSideZeroIsEliminated_TheConquestIsCreditedToSideOne()
    {
        // war 0 with polity 0 on side 0 this time: its elimination ends the war with side 1 the winner
        var wars = new[] { new OutcomeWar(0, W0, new[] { 0 }, new[] { 1, 2 }) };
        long t = B(3);
        var b = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, t, 1, 2); }, wars: wars);
        b.R.RunUntil(t - 1);
        Assert.Equal((byte)WarOutcome.Ongoing, b.W.WarRows()[0].Outcome);
        b.R.RunUntil(t);
        var war = b.W.WarRows()[0];
        Assert.Equal((t, (byte)WarOutcome.Conquest, 1), (war.EndMs, war.Outcome, war.WinnerSide));
    }

    [Fact]
    public void BothSidesEliminatedAtOneInstant_TheWarEndsInMutualCollapse()
    {
        // war 0: polity 0 against polity 1; in war 1 polity 2 takes the last city of each at the same instant
        var wars = new[] { new OutcomeWar(0, W0, new[] { 0 }, new[] { 1 }), new OutcomeWar(1, W0, new[] { 2 }, new[] { 0, 1 }) };
        long t = B(2);
        var b = Build((s, _) => { WarOutcomeWorld.ScheduleCapture(s, t, 0, 2, 1, 5_000); WarOutcomeWorld.ScheduleCapture(s, t, 2, 2, 1, 5_000); }, wars: wars,
                      settlements: x => { x[1] = x[1] with { Controller = 2 }; x[3] = x[3] with { Controller = 2 }; return x; });
        b.R.RunUntil(t);
        Assert.Equal((LeaderFate.Executed, LeaderFate.Executed), (b.W.FateOf(0), b.W.FateOf(1)));
        Assert.All(b.W.Fates, f => Assert.Equal((t, 1, 2), (f.AtMs, f.WarId, f.ByPolity)));
        var rows = b.W.WarRows();
        Assert.Equal((t, (byte)WarOutcome.MutualCollapse, -1), (rows[0].EndMs, rows[0].Outcome, rows[0].WinnerSide));
        Assert.Equal((t, (byte)WarOutcome.Conquest, 0), (rows[1].EndMs, rows[1].Outcome, rows[1].WinnerSide));
    }

    [Fact]
    public void LastCityTakenAndRazedAtTheWindow_TheExecutionIsAtTheCaptureInstant_AndTheAiTakesTheSamePath()
    {
        long t = B(2) + 2 * Day;
        var human = Build((s, _) => Capture(s, t, 0, 1), stops: true, settlements: OnlyCity0);
        var r = human.R.RunUntil(t + Day);
        Assert.Equal((RunStatus.Interrupted, t), (r.Status, r.AtMs));
        Assert.Equal(LeaderFate.Executed, human.W.FateOf(0));     // settled at (t, Resolve), before any order at t
        human.R.IssueOrder(OutcomeKinds.OrderDispose, Pack(1, 0), (long)Disposition.Raze);
        human.R.RunUntil(t + Day);
        Assert.Equal(0, Assert.Single(human.W.OrderResults).Reason);
        Assert.True(human.W.IsRazed(0));
        Assert.Equal(-1, human.W.ControllerOf(0));
        var f = Assert.Single(human.W.Fates);
        Assert.Equal((t, 0), (f.AtMs, f.SettlementId));
        var ai = Build((s, _) => Capture(s, t, 0, 1), settlements: OnlyCity0, ai: new[] { -1, (int)Disposition.Raze, -1 });
        ai.R.RunUntil(t + Day);
        Assert.True(ai.W.IsRazed(0));
        Assert.Equal(human.W.RecordHashHex(), ai.W.RecordHashHex());   // the same predicates and consequences, decided at the same instant
    }

    [Fact]
    public void RazingTheRazersOwnLastCity_IsRefused_AndTheAiFallsBackToOccupy()
    {
        long t = B(2);
        // polity 2 holds no city (city 4 is polity 1's); it takes city 0 and may not raze its only city
        Func<OutcomeSettlement[], OutcomeSettlement[]> set = x => { x[4] = x[4] with { Controller = 1 }; return x; };
        Func<OutcomePolity[], OutcomePolity[]> pol = p => { p[2] = p[2] with { Seat = -1 }; return p; };
        var human = Build((s, _) => Capture(s, t, 0, 2), stops: true, settlements: set, polities: pol);
        Drive(human, t + Day, at => at == t ? new[] { (OutcomeKinds.OrderDispose, Pack(2, 0), (long)Disposition.Raze) } : None);
        Assert.Equal(OutcomeRefusal.LastCity, Assert.Single(human.W.OrderResults).Reason);
        Assert.False(human.W.IsRazed(0));
        Assert.Equal(2, human.W.ControllerOf(0));
        Assert.Equal(LeaderFate.Reigning, human.W.FateOf(2));
        var ai = Build((s, _) => Capture(s, t, 0, 2), settlements: set, polities: pol, ai: new[] { -1, -1, (int)Disposition.Raze });
        ai.R.RunUntil(t + Day);
        Assert.False(ai.W.IsRazed(0));
        Assert.Equal((byte)Disposition.Occupy, ai.W.SettlementOutcomes[^1].Disposition);
    }

    [Fact]
    public void ReplayOfTheOrderLog_ReproducesTheStateAndTheRecord()
    {
        long t1 = B(1) + Day, t2 = B(3);
        var live = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 2); }, stops: true);
        Drive(live, B(8), at => at == t1 ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), (long)Disposition.Sack) }
                              : at == t2 ? new[] { (OutcomeKinds.OrderDispose, Pack(2, 1), (long)Disposition.Raze) } : None);
        Assert.Equal(2, live.W.OrderResults.Count(o => o.Reason == 0));
        var replay = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 2); }, replay: live.R.Orders);
        replay.R.RunUntil(B(8));
        Assert.Equal(live.W.StateHashHex(), replay.W.StateHashHex());
        Assert.Equal(live.W.RecordHashHex(), replay.W.RecordHashHex());
        Assert.Equal(live.R.WindowsHashHex(), replay.R.WindowsHashHex());
    }

    [Fact]
    public void EveryInstantConservesEveryCommodityExactly()
    {
        long t1 = B(1) + Day, t2 = B(3);
        var b = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 2); }, ai: new[] { -1, (int)Disposition.Sack, (int)Disposition.Raze });
        var before = Totals(b.W.Ledger);
        foreach (long t in new[] { t1 - 1, t1, t2 - 1, t2, B(9) })
        {
            b.R.RunUntil(t);
            Assert.Equal(before, Totals(b.W.Ledger));
        }
        Assert.True(b.W.IsRazed(1));
        Assert.True(b.W.Ledger.Balance(b.W.CiviliansKilledSink) > 0);
    }
}
