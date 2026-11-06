using Proto.M7.Events;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// Prisoner options (release, conscript, enslave where the captor's institutions allow, ransom, exchange) and captive-leader decisions
/// (release, ransom, execution behind the Director-level rule): eligibility derived from state, exact Ledger flows, the prisoners'
/// rations, and the delivery pins of each order.
/// </summary>
public class PrisonerAndLeaderDecisionTests
{
    private static readonly long TB = B(2) + Day;
    private const long Release = (long)PrisonerAction.Release, Conscript = (long)PrisonerAction.Conscript, Enslave = (long)PrisonerAction.Enslave;

    /// <summary>Formation 0 (polity 0, 3,000 men, side 1) fought as 50 entities: 30 fit, 20 surrendered → 1,200 into polity 1's custody.
    /// Formation 2 (polity 1, 2,500, side 0): 50 fit, or with <paramref name="mutual"/> 40 fit and 10 surrendered → 500 into polity 0's.</summary>
    private static FieldBattle Battle(bool mutual = false) => new(0, TB - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
    {
        new CollapseInput(0, 1, new long[] { 30, 0, 0, 0, 20 }, 400, 1_000),
        new CollapseInput(2, 0, mutual ? new long[] { 40, 0, 0, 0, 10 } : new long[] { 50, 0, 0, 0, 0 }, 900, 1_000),
    });

    private static Built WithPrisoners(bool mutual = false, long[]? probes = null, Func<OutcomePolity[], OutcomePolity[]>? polities = null)
        => Build((s, w) => w.ScheduleBattle(s, TB, Battle(mutual)), stops: true, probes: probes, polities: polities);

    private static long Bal(Built b, int account) => b.W.Ledger.Balance(account);

    [Fact]
    public void Intake_FromTheCollapse_PutsTheSurrenderedInTheCaptorsCustody()
    {
        var b = WithPrisoners();
        b.R.RunUntil(TB + Day);
        Assert.Equal((1_200L, 1_800L), (Bal(b, b.W.CustodyOf(1, 0)), Bal(b, b.W.FormationAccount(0))));
        var p = Assert.Single(b.W.PrisonerRows);
        Assert.Equal((TB, 1, 0, (byte)PrisonerAction.Intake, 1_200L, 0), (p.AtMs, p.Captor, p.Origin, p.Action, p.Count, p.BattleId));
    }

    /// <summary>Delivery pin: a prisoner order is a one-shot transaction applied AT the window instant it is issued in.</summary>
    [Theory]
    [InlineData(-1, OutcomeRefusal.NotHeld)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void Release_AppliesAtTheWindowInstant_PrisonersGoHomeOnParole(long offset, int expected)
    {
        long at = TB + offset;
        var b = WithPrisoners(probes: offset == 0 ? null : new[] { at });
        Drive(b, TB + Day, x => x == at ? new[] { (OutcomeKinds.OrderPrisoners, Pack(1, 0), Value(Release, 500)) } : None);
        var o = Assert.Single(b.W.OrderResults);
        Assert.Equal((at, expected), (o.AtMs, o.Reason));
        Assert.Equal(expected == 0 ? (700L, 500L) : (1_200L, 0L), (Bal(b, b.W.CustodyOf(1, 0)), Bal(b, b.W.ParoledOf(0))));
    }

    [Theory]
    [InlineData(OutcomeInstitution.Coinage, Conscript, 0)]
    [InlineData(OutcomeInstitution.Coinage | OutcomeInstitution.PrisonerConvention, Conscript, OutcomeRefusal.Institution)]
    [InlineData(OutcomeInstitution.Coinage, Enslave, OutcomeRefusal.Institution)]
    [InlineData(OutcomeInstitution.Coinage | OutcomeInstitution.Slavery, Enslave, 0)]
    [InlineData(OutcomeInstitution.Coinage | OutcomeInstitution.Slavery | OutcomeInstitution.PrisonerConvention, Enslave, OutcomeRefusal.Institution)]
    [InlineData(OutcomeInstitution.Coinage | OutcomeInstitution.PrisonerConvention, Release, 0)]
    public void PrisonerActions_AreGatedByTheCaptorsInstitutions(OutcomeInstitution held, long action, int expected)
    {
        var b = WithPrisoners(polities: p => { p[1] = p[1] with { Institutions = held }; return p; });
        Drive(b, TB + Day, x => x == TB ? new[] { (OutcomeKinds.OrderPrisoners, Pack(1, 0), Value(action, 300)) } : None);
        Assert.Equal(expected, Assert.Single(b.W.OrderResults).Reason);
        int dest = action == Conscript ? b.W.ImpressedOf(1) : action == Enslave ? b.W.EnslavedOf(1) : b.W.ParoledOf(0);
        Assert.Equal(expected == 0 ? 300L : 0L, Bal(b, dest));
        Assert.Equal(expected == 0 ? 900L : 1_200L, Bal(b, b.W.CustodyOf(1, 0)));
    }

    [Theory]
    [InlineData(1, 0, 1_201L, Release, OutcomeRefusal.NotHeld)]
    [InlineData(1, 1, 10L, Release, OutcomeRefusal.BadTarget)]
    [InlineData(1, 0, 0L, Release, OutcomeRefusal.BadValue)]
    [InlineData(1, 0, 10L, 9L, OutcomeRefusal.BadValue)]
    [InlineData(2, 0, 10L, Release, OutcomeRefusal.NotHeld)]          // polity 2 holds none of polity 0's men
    public void PrisonerActions_Refusals(int captor, int origin, long count, long action, int expected)
    {
        var b = WithPrisoners();
        Drive(b, TB + Day, x => x == TB ? new[] { (OutcomeKinds.OrderPrisoners, Pack(captor, origin), Value(action, count)) } : None);
        Assert.Equal(expected, Assert.Single(b.W.OrderResults).Reason);
        Assert.Equal(1_200L, Bal(b, b.W.CustodyOf(1, 0)));
    }

    [Fact]
    public void Ransom_NeedsTheOriginsStandingTerms_AndPaysForAsManyAsItCan()
    {
        // at TB: a ransom before any terms (refused), then the terms, then a ransom above them (refused), then one within them
        var b = WithPrisoners();
        Drive(b, TB + Day, x => x == TB ? new[]
        {
            (OutcomeKinds.OrderRansom, Pack(1, 0), 150L),
            (OutcomeKinds.OrderRansomTerms, Pack(0, 0), 200L),
            (OutcomeKinds.OrderRansom, Pack(1, 0), 250L),
            (OutcomeKinds.OrderRansom, Pack(1, 0), 150L),
        } : None);
        Assert.Equal(new[] { OutcomeRefusal.NoTerms, 0, OutcomeRefusal.NoTerms, 0 }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.Equal(0L, Bal(b, b.W.CustodyOf(1, 0)));
        Assert.Equal((2_000_000L - 180_000, 3_000_000L + 180_000, 1_000L + 1_200), (Bal(b, b.W.TreasuryOf(0)), Bal(b, b.W.TreasuryOf(1)), Bal(b, b.W.TroopsOf(0))));
        var r = b.W.PrisonerRows[^1];
        Assert.Equal(((byte)PrisonerAction.Ransom, 1_200L, 180_000L), (r.Action, r.Count, r.Money));
    }

    [Theory]
    [InlineData(100_000L, 0, 666L)]                       // 100,000 / 150 = 666 heads, the rest stay in custody
    [InlineData(100L, OutcomeRefusal.CannotPay, 0L)]
    public void Ransom_IsPartialWhenTheOriginCannotPayForAll(long treasury, int expected, long returned)
    {
        var b = WithPrisoners(polities: p => { p[0] = p[0] with { Treasury = treasury }; return p; });
        Drive(b, TB + Day, x => x == TB ? new[] { (OutcomeKinds.OrderRansomTerms, Pack(0, 0), 200L), (OutcomeKinds.OrderRansom, Pack(1, 0), 150L) } : None);
        Assert.Equal(expected, b.W.OrderResults[^1].Reason);
        Assert.Equal(1_200 - returned, Bal(b, b.W.CustodyOf(1, 0)));
        Assert.Equal(treasury - returned * 150, Bal(b, b.W.TreasuryOf(0)));
    }

    [Fact]
    public void Ransom_NeedsCoinageOnBothSides()
    {
        var b = WithPrisoners(polities: p => { p[0] = p[0] with { Institutions = OutcomeInstitution.None }; return p; });
        Drive(b, TB + Day, x => x == TB ? new[] { (OutcomeKinds.OrderRansomTerms, Pack(0, 0), 200L), (OutcomeKinds.OrderRansom, Pack(1, 0), 150L) } : None);
        Assert.Equal(OutcomeRefusal.Institution, b.W.OrderResults[^1].Reason);
    }

    /// <summary>Standing terms take effect at the instant they are issued: a later window can use them, an earlier one cannot.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, OutcomeRefusal.NoTerms)]
    public void RansomTerms_StandFromTheirInstant(long termsOffset, int expected)
    {
        long termsAt = TB + termsOffset;
        var b = WithPrisoners(probes: new[] { termsAt });
        Drive(b, TB + Day, x => x == termsAt ? new[] { (OutcomeKinds.OrderRansomTerms, Pack(0, 0), 200L) }
                              : x == TB ? new[] { (OutcomeKinds.OrderRansom, Pack(1, 0), 150L) } : None);
        Assert.Equal(expected, b.W.OrderResults.Single(o => o.Kind == OutcomeKinds.OrderRansom).Reason);
    }

    [Fact]
    public void Exchange_NeedsBothOffers_ThenReturnsOneForOne()
    {
        var b = WithPrisoners(mutual: true);
        b.R.RunUntil(TB + Day);   // stops at TB
        Assert.Equal((1_200L, 500L), (Bal(b, b.W.CustodyOf(1, 0)), Bal(b, b.W.CustodyOf(0, 1))));
        b.R.IssueOrder(OutcomeKinds.OrderExchange, Pack(1, 0), 300);
        b.R.RunUntil(TB + Day);
        Assert.Equal((1_200L, 500L), (Bal(b, b.W.CustodyOf(1, 0)), Bal(b, b.W.CustodyOf(0, 1))));   // one offer: nothing moves, it stands
        var probe = Build((s, w) => { w.ScheduleBattle(s, TB, Battle(true)); WarOutcomeWorld.ScheduleProbe(s, TB + 2 * Day); }, stops: true);
        Drive(probe, TB + 3 * Day, x => x == TB ? new[] { (OutcomeKinds.OrderExchange, Pack(1, 0), 300L) }
                                    : x == TB + 2 * Day ? new[] { (OutcomeKinds.OrderExchange, Pack(0, 1), 1_000L) } : None);
        var w2 = probe.W;
        Assert.Equal((900L, 200L), (w2.Ledger.Balance(w2.CustodyOf(1, 0)), w2.Ledger.Balance(w2.CustodyOf(0, 1))));
        Assert.Equal((1_300L, 1_300L), (w2.Ledger.Balance(w2.TroopsOf(0)), w2.Ledger.Balance(w2.TroopsOf(1))));
        var custody = w2.CustodyRows();
        Assert.Equal((0L, 700L), (custody.Single(c => c.Captor == 1 && c.Origin == 0).ExchangeOffer, custody.Single(c => c.Captor == 0 && c.Origin == 1).ExchangeOffer));
        Assert.Equal(2, w2.PrisonerRows.Count(r => r.Action == (byte)PrisonerAction.Exchange));
    }

    [Fact]
    public void Prisoners_EatTheBaseRationFromTheCaptorsGranary_ExactlyOverTurnSplits()
    {
        long y = 2 * SimInstant.MsPerYear;   // two years: the Early Modern turn is two years long, so a turn start falls inside
        var b = WithPrisoners(probes: new[] { TB + y });
        Drive(b, TB + y + Day, x => x == TB + y ? new[] { (OutcomeKinds.OrderPrisoners, Pack(1, 0), Value(Release, 1)) } : None);
        Assert.True(b.W.TurnSnapshots.Count(t => t.AtMs > TB && t.AtMs < TB + y) >= 1, "a campaign-turn start splits the span");
        long ration = LogisticsRates.Proposal().RationBase;
        Assert.Equal(2 * 1_200 * ration, b.W.Ledger.Balance(b.W.PrisonerFoodSink));     // 1,200 heads × two years × the base ration, exact
        Assert.Equal(50_000_000 - 2 * 1_200 * ration, b.W.Ledger.Balance(b.W.GranaryOf(1)));
        Assert.Equal(0L, b.W.FoodArrearsOf(1, 0));
    }

    [Fact]
    public void Prisoners_RationsAreSettledAtEveryTurnStart_TheSnapshotShowsWhatTheyAteSoFar()
    {
        // DIR5 §2.4 sub-stepping: the state at a campaign-turn start carries the rations eaten up to that boundary, not a stale balance
        var b = WithPrisoners();
        Drive(b, TB + 2 * SimInstant.MsPerYear, _ => None);
        var snap = b.W.TurnSnapshots.First(t => t.AtMs > TB);
        long ate = (long)((Int128)1_200 * LogisticsRates.Proposal().RationBase * (snap.AtMs - TB) / SimInstant.MsPerYear);
        Assert.True(ate > 0);
        Assert.Equal(ate, snap.Balances[b.W.PrisonerFoodSink]);
        Assert.Equal(50_000_000 - ate, snap.Balances[b.W.GranaryOf(1)]);
    }

    [Fact]
    public void Prisoners_OnAnEmptyGranary_RunUpUnpaidRations_NoMortalityHere()
    {
        long y = SimInstant.MsPerYear;
        var b = WithPrisoners(probes: new[] { TB + y }, polities: p => { p[1] = p[1] with { Granary = 100_000 }; return p; });
        Drive(b, TB + y + Day, x => x == TB + y ? new[] { (OutcomeKinds.OrderPrisoners, Pack(1, 0), Value(Release, 1)) } : None);
        long due = 1_200 * LogisticsRates.Proposal().RationBase;
        Assert.Equal((100_000L, due - 100_000), (b.W.Ledger.Balance(b.W.PrisonerFoodSink), b.W.FoodArrearsOf(1, 0)));
        Assert.Equal(1_199L, Bal(b, b.W.CustodyOf(1, 0)));                          // nobody died: prisoner mortality is attrition (M7.14)
    }

    // ------------------------------------------------------------------ captive leaders

    private static readonly long T1 = B(1) + Day;

    private static Built Captive(CaptiveExecutionRule rule = CaptiveExecutionRule.NotPermitted, long[]? probes = null, Func<OutcomePolity[], OutcomePolity[]>? polities = null)
        => Build((s, _) => Capture(s, T1, 0, 1), stops: true, rule: rule, probes: probes, polities: polities);

    [Theory]
    [InlineData(-1, OutcomeRefusal.NotEligible)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void LeaderRelease_ByTheCaptor_FromTheInstantOfCaptivity(long offset, int expected)
    {
        long at = T1 + offset;
        var b = Captive(probes: offset == 0 ? null : new[] { at });
        Drive(b, T1 + Day, x => x == at ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Release, 0)) } : None);
        Assert.Equal(expected, Assert.Single(b.W.OrderResults).Reason);
        if (expected != 0) { Assert.Equal(LeaderFate.Captive, b.W.FateOf(0)); return; }
        Assert.Equal((LeaderFate.Reigning, -1, 1), (b.W.FateOf(0), b.W.HeldBy(0), b.W.SeatOf(0)));   // back to its lowest-id city
        var f = b.W.Fates[^1];
        Assert.Equal((at, (byte)LeaderFate.Captive, (byte)LeaderFate.Reigning, (byte)FateCause.Released, 1), (f.AtMs, f.From, f.To, f.Cause, f.SettlementId));
    }

    [Fact]
    public void LeaderDecisions_OnlyTheCaptor_OnlyACaptive()
    {
        var b = Captive();
        Drive(b, T1 + Day, x => x == T1 ? new[]
        {
            (OutcomeKinds.OrderLeader, Pack(2, 0), Value((long)LeaderDecision.Release, 0)),   // not the captor
            (OutcomeKinds.OrderLeader, Pack(0, 1), Value((long)LeaderDecision.Release, 0)),   // polity 1's leader is free
            (OutcomeKinds.OrderLeader, Pack(1, 0), Value(9, 0)),                              // no such decision
        } : None);
        Assert.Equal(new[] { OutcomeRefusal.NotCaptor, OutcomeRefusal.NotEligible, OutcomeRefusal.BadValue }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.Equal(LeaderFate.Captive, b.W.FateOf(0));
    }

    [Fact]
    public void LeaderRansom_WithinTheStandingTerms_PaysTheCaptorAndFreesTheLeader()
    {
        var b = Captive();
        Drive(b, T1 + Day, x => x == T1 ? new[]
        {
            (OutcomeKinds.OrderRansomTerms, Pack(0, 1), 500_000L),
            (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Ransom, 600_000)),
            (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Ransom, 400_000)),
        } : None);
        Assert.Equal(new[] { 0, OutcomeRefusal.NoTerms, 0 }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.Equal((LeaderFate.Reigning, 1_600_000L, 3_400_000L), (b.W.FateOf(0), Bal(b, b.W.TreasuryOf(0)), Bal(b, b.W.TreasuryOf(1))));
        Assert.Equal((byte)FateCause.Ransomed, b.W.Fates[^1].Cause);
    }

    [Fact]
    public void CaptiveExecution_IsADirectorLevelRule_RefusedUnlessPermitted()
    {
        var no = Captive(CaptiveExecutionRule.NotPermitted);
        Drive(no, T1 + Day, x => x == T1 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Execute, 0)) } : None);
        Assert.Equal(OutcomeRefusal.NotPermitted, Assert.Single(no.W.OrderResults).Reason);
        Assert.Equal(LeaderFate.Captive, no.W.FateOf(0));

        var yes = Captive(CaptiveExecutionRule.Permitted);
        Drive(yes, T1 + Day, x => x == T1 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Execute, 0)) } : None);
        Assert.Equal(0, Assert.Single(yes.W.OrderResults).Reason);
        var f = yes.W.Fates[^1];
        Assert.Equal(((byte)LeaderFate.Captive, (byte)LeaderFate.Executed, (byte)FateCause.ExecutedAsCaptive, 1, 1), (f.From, f.To, f.Cause, f.ByPolity, f.HeldBy));
        Assert.Equal(1, yes.W.Ledger.Balance(yes.W.ExecutedSink));
        // not the leader rule: the polity still holds a city, stays in the war, and the war goes on
        Assert.Equal(1L, yes.W.CityCount(0));
        Assert.Equal((-1L, (byte)WarOutcome.Ongoing), (yes.W.WarRows()[0].EndMs, yes.W.WarRows()[0].Outcome));
        Assert.All(yes.W.ParticipantRows(), p => Assert.Equal(-1, p.LeftMs));
    }
}
