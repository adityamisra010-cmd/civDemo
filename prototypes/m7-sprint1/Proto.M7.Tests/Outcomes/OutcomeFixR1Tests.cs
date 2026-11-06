using System.Reflection;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.War.Outcomes.Presentation;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Proto.M7.Tests.Siege;
using FormationSpec = Proto.M7.War.Siege.FormationSpec;
using Xunit.Abstractions;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// Lane W fix round 1: one semantic test (or more) per reproduced verifier finding W-R1-1 .. W-R1-11. Each asserts a property the
/// system ought to have by DIR5 / C31-C33 or by the lane's stated PROPOSAL rule; each failed (or did not compile) on 157130e.
/// </summary>
public class OutcomeFixR1Tests
{
    private readonly ITestOutputHelper _out;
    public OutcomeFixR1Tests(ITestOutputHelper o) => _out = o;

    private const long SackD = (long)Disposition.Sack, RazeD = (long)Disposition.Raze, OccupyD = (long)Disposition.Occupy;

    // ================================================================== W-R1-1: elimination after a captive execution

    /// <summary>A polity whose captive leader was executed (Permitted) and which then loses its last city has lost all its cities: it
    /// leaves its wars as Eliminated at that instant (pinned with ±1) and the war whose whole side is gone ends; no second fate.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void CaptiveExecuted_ThenLastCityLost_ThePolityIsEliminatedAtThatInstant_AndTheWarEnds(long offset)
    {
        long t1 = B(1) + Day, t2 = B(3);
        var b = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 2); }, stops: true, rule: CaptiveExecutionRule.Permitted);
        Drive(b, t2 + offset, at => at == t1 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Execute, 0)) } : None);
        Assert.Equal(0, b.W.OrderResults[0].Reason);
        var p0 = b.W.ParticipantRows().Single(p => p.Polity == 0);
        var war = b.W.WarRows()[0];
        _out.WriteLine($"offset {offset}: cities {b.W.CityCount(0)}, participant left {p0.LeftMs} exit {p0.Exit}; war outcome {war.Outcome} end {war.EndMs} winner {war.WinnerSide}");
        if (offset < 0)
        {
            Assert.Equal(1L, b.W.CityCount(0));
            Assert.Equal((-1L, (byte)ParticipantExit.None), (p0.LeftMs, p0.Exit));
            Assert.Equal(((byte)WarOutcome.Ongoing, -1L), (war.Outcome, war.EndMs));
        }
        else
        {
            Assert.Equal(0L, b.W.CityCount(0));
            Assert.Equal((t2, (byte)ParticipantExit.Eliminated), (p0.LeftMs, p0.Exit));
            Assert.Equal(((byte)WarOutcome.Conquest, t2, 0), (war.Outcome, war.EndMs, war.WinnerSide));
        }
        // the fate is the captive execution's alone: no second fate row, one leader dead, exactly one person in the executed sink
        Assert.Equal(new[] { ((byte)LeaderFate.Captive, (byte)FateCause.SeatTaken), ((byte)LeaderFate.Executed, (byte)FateCause.ExecutedAsCaptive) },
                     b.W.Fates.Select(f => (f.To, f.Cause)).ToArray());
        Assert.Equal((0L, 1L), (b.W.Ledger.Balance(b.W.LeaderAccountOf(0)), b.W.Ledger.Balance(b.W.ExecutedSink)));
    }

    // ================================================================== W-R1-2: one campaign formation, one collapse input

    [Fact]
    public void OneCampaignFormationFieldedAsTwoBattleFormations_IsRefused_NotMisSplit()
    {
        long tb = B(2) + Day;
        var twice = new FieldBattle(0, tb - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        {
            new CollapseInput(0, 1, new long[] { 0, 0, 0, 25, 0 }, 500, 1_000),   // 25 entities, all dead
            new CollapseInput(0, 1, new long[] { 25, 0, 0, 0, 0 }, 500, 1_000),   // the same campaign formation's other 25, all fit
        });
        var b = Build();
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb, twice));
        b.R.RunUntil(tb + Day);
        Assert.Empty(b.W.Battles);                                                       // nothing was scheduled
        Assert.Equal(3_000L, b.W.Ledger.Balance(b.W.FormationAccount(0)));
        // the same men handed off one-to-one collapse exactly: half the entities dead → half the people, the start counted once
        var once = new FieldBattle(0, tb - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        {
            new CollapseInput(0, 1, new long[] { 25, 0, 0, 25, 0 }, 500, 1_000),
        });
        var c = Build((s, w) => w.ScheduleBattle(s, tb, once));
        c.R.RunUntil(tb + Day);
        Assert.Equal((1_500L, 1_500L, 3_000L), (c.W.Ledger.Balance(c.W.KilledSink), c.W.Ledger.Balance(c.W.FormationAccount(0)),
                                                 c.W.BattleSides.Single(x => x.Side == 1).PersonnelStart));
    }

    [Fact]
    public void FromTactical_RefusesTwoTacticalFormationsForOneCampaignFormation()
    {
        var setup = TacticalScenarios.CombinedArms(42);
        var battle = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        battle.Run();
        Assert.Throws<ArgumentException>(() => BattleCollapse.FromTactical(battle, setup, new (long, int)[] { (1, 0), (2, 0), (3, 2), (4, 3) }));
        Assert.Equal(new[] { 0, 1, 2, 3 }, BattleCollapse.FromTactical(battle, setup, new (long, int)[] { (1, 0), (2, 1), (3, 2), (4, 3) })
                                                         .Select(i => i.CampaignFormation).ToArray());
    }

    // ================================================================== W-R1-3: casualties by side, polity and cause

    /// <summary>Every casualty of a field battle is recorded under the side and the polity that suffered it, by cause, exactly
    /// (formation 0, polity 0, fights on battle side 1 and loses the field; formation 2, polity 1, on side 0 holds it).</summary>
    [Fact]
    public void FieldBattleCasualties_AreRecordedUnderTheSufferingSideAndPolity_ByCause_Exactly()
    {
        long tb = B(2) + Day;
        var fb = new FieldBattle(0, tb - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        {
            new CollapseInput(0, 1, new long[] { 20, 5, 10, 5, 10 }, 400, 1_000),   // 3,000: fit 1,200, wounded 300, routed 600, dead 300, surrendered 600
            new CollapseInput(2, 0, new long[] { 40, 2, 0, 3, 5 }, 900, 1_000),     // 2,500: fit 2,000, wounded 100, dead 150, surrendered 250
        });
        var b = Build((s, w) => w.ScheduleBattle(s, tb, fb));
        b.R.RunUntil(tb + Day);
        var bid = Assert.Single(b.W.Battles).BattleId;
        // side 1 lost the field: (1,000 − 400) / 1,000 of its 600 routed desert = 360
        Assert.Equal(new[]
        {
            (bid, (byte)0, 1, (byte)CasualtyCause.Killed, 150L), (bid, (byte)0, 1, (byte)CasualtyCause.Wounded, 100L), (bid, (byte)0, 1, (byte)CasualtyCause.Captured, 250L),
            (bid, (byte)1, 0, (byte)CasualtyCause.Killed, 300L), (bid, (byte)1, 0, (byte)CasualtyCause.Wounded, 300L), (bid, (byte)1, 0, (byte)CasualtyCause.Captured, 600L),
            (bid, (byte)1, 0, (byte)CasualtyCause.Deserted, 360L),
        }, b.W.Casualties.Select(c => (c.BattleId, c.Side, c.Polity, c.Cause, c.Count)).ToArray());
        Assert.Equal(new[] { ((byte)0, 1, 1, 2_500L), ((byte)1, 0, 1, 3_000L) }, b.W.BattleSides.Select(x => (x.Side, x.Polity, x.Formations, x.PersonnelStart)).ToArray());
        // the captured of each side are in the OTHER polity's custody, exactly as recorded
        Assert.Equal((600L, 250L), (b.W.Ledger.Balance(b.W.CustodyOf(1, 0)), b.W.Ledger.Balance(b.W.CustodyOf(0, 1))));
        Assert.Equal((300L + 150, 360L), (b.W.Ledger.Balance(b.W.KilledSink), b.W.Ledger.Balance(b.W.DesertedSink)));
    }

    private static readonly long Start = SiegeRig.At(1500, 100);

    /// <summary>A lane S capitulation: the garrison taken prisoner is the DEFENDER's casualty (side 1, polity 0), the sides are the
    /// besieger (side 0, polity 1) and the defender, and the siege's battle row carries the war of the two parties and that war's pulse,
    /// the same war and pulse as the leader-fate row it causes (W-R1-3, W-R1-5).</summary>
    [Fact]
    public void ASiegeCapture_RecordsTheCapturedUnderTheDefender_AndTheBattleAndTheFateInOneWarAndPulse()
    {
        var b = BuildBound(Start, foodDays: 5);
        b.R.RunUntil(Start + 200 * Day);
        var c = Assert.Single(b.Siege.Captures);
        var battle = Assert.Single(b.W.Battles);
        var f = Assert.Single(b.W.Fates);
        long pulse = (c.AtMs - b.WarStart) / (7 * Day);
        Assert.Equal((0, pulse, c.AtMs), (battle.WarId, battle.Pulse, battle.EndMs));
        Assert.Equal((battle.WarId, battle.Pulse, battle.EndMs), (f.WarId, f.Pulse, f.AtMs));
        Assert.Equal(new[] { ((byte)0, 1), ((byte)1, 0) }, b.W.BattleSides.Select(x => (x.Side, x.Polity)).ToArray());
        Assert.True(c.Prisoners > 0);
        Assert.Equal(new[] { (battle.BattleId, (byte)1, 0, (byte)CasualtyCause.Captured, c.Prisoners) },
                     b.W.Casualties.Select(x => (x.BattleId, x.Side, x.Polity, x.Cause, x.Count)).ToArray());
    }

    // ================================================================== W-R1-4 / W-R1-8: the raze's Ledger flows under both raze rules

    [Theory]
    [InlineData(RazePopulationRule.KillThenDisplace)]
    [InlineData(RazePopulationRule.Displace)]
    public void Raze_TheExpelledAreRefugees_TheDeadAreOnlyWhatTheRuleKills_AndBothRulesTakeTheSameLoot(RazePopulationRule rule)
    {
        long t = B(2) + 2 * Day;
        Built Razed(RazePopulationRule r)
        {
            var x = Build((s, _) => Capture(s, t, 0, 1, 5_000), stops: true, raze: r);
            Drive(x, t + Day, at => at == t ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), RazeD) } : None);
            Assert.Equal(0, Assert.Single(x.W.OrderResults).Reason);
            return x;
        }
        var b = Razed(rule);
        var w = b.W;
        var row = w.SettlementOutcomes[^1];
        Assert.Equal(rule, w.RazeRule);
        Assert.Equal(10_000L, row.Killed + row.Fled);
        Assert.Equal(row.Fled, w.Ledger.Balance(w.RefugeesOf(0)));                   // expelled people are refugees of their own polity
        Assert.Equal(row.Killed, w.Ledger.Balance(w.CiviliansKilledSink));           // the dead are the record's, no more
        if (rule == RazePopulationRule.Displace) Assert.Equal((0L, 10_000L), (row.Killed, row.Fled));
        else Assert.Equal(529L, row.Killed);                                        // the sack's killing at q = 1/2 (DispositionTests' golden)
        var other = Razed(rule == RazePopulationRule.Displace ? RazePopulationRule.KillThenDisplace : RazePopulationRule.Displace).W.SettlementOutcomes[^1];
        Assert.Equal((other.LootMoney, other.LootGrain, other.GrainBurnt, other.Burnt), (row.LootMoney, row.LootGrain, row.GrainBurnt, row.Burnt));
        Assert.Equal((1_000_000L, 2_000L), (row.LootMoney, row.Burnt));
        Assert.Equal(Totals(Build(raze: rule).W.Ledger), Totals(w.Ledger));
    }

    /// <summary>A raze of a lane-S-bound city by a victor that holds another city: the ruin is no city (the victor's count is its own
    /// city only), lane S keeps it under the victor with no people, and the people go exactly where the record says.</summary>
    [Theory]
    [InlineData(RazePopulationRule.KillThenDisplace)]
    [InlineData(RazePopulationRule.Displace)]
    public void RazeOfABoundCity_StopsCountingIt_AndItsPeopleGoExactlyWhereTheRecordSays(RazePopulationRule rule)
    {
        var plain = Bound(Start, foodDays: 5, secondCity: true, raze: rule);
        plain.R.RunUntil(Start + 200 * Day);
        long tc = Assert.Single(plain.Siege.Captures).AtMs;
        var b = Bound(Start, foodDays: 5, secondCity: true, raze: rule, stops: true);
        long civ = -1, refugees = -1, killed = -1;
        Int128[]? totals = null;
        while (true)
        {
            var r = b.R.RunUntil(tc);
            if (r.Status == RunStatus.ReachedLimit) break;
            if (r.AtMs != tc || civ >= 0) continue;
            civ = b.Siege.Civilians(0);
            refugees = b.W.Ledger.Balance(b.Siege.ElsewhereOf(0));
            killed = b.W.Ledger.Balance(b.W.CiviliansKilledSink);
            totals = Totals(b.W.Ledger);
            b.R.IssueOrder(OutcomeKinds.OrderDispose, Pack(1, 0), RazeD);
        }
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        var row = b.W.SettlementOutcomes[^1];
        Assert.Equal((byte)Disposition.Raze, row.Disposition);
        Assert.True(b.W.IsRazed(0));
        Assert.Equal(1, b.Siege.ControllerOf(0));                  // lane S has no destroyed state: it stays the victor's, empty
        Assert.Equal(b.Siege.ControllerOf(0), b.W.ControllerOf(0)); // fix r2 (W-R2-8, MV-3): and the two worlds still agree on it
        Assert.Equal(1, b.W.SettlementRows()[0].Controller);
        Assert.Equal(1L, b.W.CityCount(1));                        // its own city 1 only: the ruin is no city
        Assert.Equal(0L, b.Siege.Civilians(0));
        Assert.True(civ > 0);
        Assert.Equal(civ, row.Killed + row.Fled);
        Assert.Equal(refugees + row.Fled, b.W.Ledger.Balance(b.Siege.ElsewhereOf(0)));
        Assert.Equal(killed + row.Killed, b.W.Ledger.Balance(b.W.CiviliansKilledSink));
        if (rule == RazePopulationRule.Displace) Assert.Equal(0L, row.Killed); else Assert.True(row.Killed > 0);
        Assert.Equal(totals, Totals(b.W.Ledger));
    }

    [Fact]
    public void TheRazeRule_IsADirectorLevelFlag_WithNoDefault()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(raze: (RazePopulationRule)7));
        var p = typeof(WarOutcomeWorld).GetConstructors().Single().GetParameters().Single(x => x.ParameterType == typeof(RazePopulationRule));
        Assert.False(p.IsOptional);                                // every caller passes one (as for CaptiveExecutionRule)
        var q = typeof(WarOutcomeWorld).GetConstructors().Single().GetParameters().Single(x => x.ParameterType == typeof(CaptiveExecutionRule));
        Assert.False(q.IsOptional);
    }

    // ================================================================== W-R1-5: the war of a record

    /// <summary>Polities 1 and 0 are opposed in two wars (war 1 starts 3 days later, so its pulses differ). Polity 1 takes polity 0's
    /// seat and then its last city in war 1: the captive and the execution fate rows name war 1 and war 1's pulse, like the battle rows
    /// that caused them (the lowest-id war between the two would be war 0).</summary>
    [Fact]
    public void TheFateRow_NamesTheWarAndPulseOfTheCaptureThatCausedIt()
    {
        var wars = new[] { new OutcomeWar(0, W0, new[] { 1, 2 }, new[] { 0 }), new OutcomeWar(1, W0 + 3 * Day, new[] { 1 }, new[] { 0 }) };
        long t1 = B(1) + Day, t2 = B(3);
        var b = Build((s, _) => { WarOutcomeWorld.ScheduleCapture(s, t1, 0, 1, 1, 5_000); WarOutcomeWorld.ScheduleCapture(s, t2, 1, 1, 1, 5_000); }, wars: wars);
        b.R.RunUntil(t2 + Day);
        Assert.Equal(0, b.W.WarBetween(1, 0));
        Assert.Equal(new[] { ((byte)LeaderFate.Captive, 0), ((byte)LeaderFate.Executed, 1) }, b.W.Fates.Select(f => (f.To, f.SettlementId)).ToArray());
        for (int i = 0; i < 2; i++)
        {
            var f = b.W.Fates[i];
            var battle = b.W.Battles[i];
            Assert.Equal((f.SettlementId, f.AtMs), (battle.SettlementId, battle.EndMs));
            Assert.Equal((1, battle.Pulse), (f.WarId, f.Pulse));
            Assert.Equal(1, battle.WarId);
            Assert.Equal((f.AtMs - (W0 + 3 * Day)) / (7 * Day), f.Pulse);       // war 1's own clock
        }
        Assert.Equal(1, b.W.PolityRows()[0].LastLossWar);
        // a capture declared outside any war between polities at war: the battle and the fate both say "no war"
        var nw = Build((s, _) => { WarOutcomeWorld.ScheduleCapture(s, t1, 0, 1, -1, 1_000); WarOutcomeWorld.ScheduleCapture(s, t2, 1, 2, -1, 1_000); });
        nw.R.RunUntil(t2 + Day);
        Assert.Equal(LeaderFate.Executed, nw.W.FateOf(0));
        Assert.All(nw.W.Fates, f => Assert.Equal((-1, -1L), (f.WarId, f.Pulse)));
        Assert.All(nw.W.Battles, x => Assert.Equal((-1, -1L), (x.WarId, x.Pulse)));
    }

    [Fact]
    public void TheCaptureDevice_RefusesAWarInWhichThePartiesAreNotOpposed()
    {
        // allies: polities 1 and 2 are both on side 0 of war 0
        var allies = Build((s, _) => Capture(s, B(2), 3, 2));
        Assert.Throws<InvalidOperationException>(() => allies.R.RunUntil(B(3)));
        // after its end: polity 0 loses both cities at B(1) (eliminated; war 0 ends in conquest); a re-capture "in war 0" is refused
        Action<EventScheduler, WarOutcomeWorld> lost = (s, _) => { Capture(s, B(1), 0, 1); Capture(s, B(1), 1, 2); };
        var ended = Build((s, w) => { lost(s, w); Capture(s, B(2), 0, 0); });
        ended.R.RunUntil(B(2) - 1);
        Assert.Equal(((byte)WarOutcome.Conquest, B(1)), (ended.W.WarRows()[0].Outcome, ended.W.WarRows()[0].EndMs));
        Assert.Throws<InvalidOperationException>(() => ended.R.RunUntil(B(2)));
        // the same re-capture outside any war is accepted and recorded with no war; a capture between opposed parties is accepted
        var outside = Build((s, w) => { lost(s, w); WarOutcomeWorld.ScheduleCapture(s, B(2), 0, 0, -1, 1_000); });
        outside.R.RunUntil(B(3));
        Assert.Equal((0, -1), (outside.W.ControllerOf(0), outside.W.Battles[^1].WarId));
        var opposed = Build((s, _) => Capture(s, B(2), 0, 2));
        opposed.R.RunUntil(B(3));
        Assert.Equal((2, 0), (opposed.W.ControllerOf(0), opposed.W.Battles[^1].WarId));
        // a war that goes on without the eliminated polity: war 0 = polity 1 against polities 0 and 2; polity 0 is eliminated at B(1)
        // while polity 2 fights on, so the war is ongoing, but polity 0 has left it: its re-capture "in war 0" is refused
        var goesOn = new[] { new OutcomeWar(0, W0, new[] { 1 }, new[] { 0, 2 }) };
        var left = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, B(1), 1, 1); Capture(s, B(2), 0, 0); }, wars: goesOn);
        left.R.RunUntil(B(2) - 1);
        Assert.Equal(((byte)WarOutcome.Ongoing, B(1)), (left.W.WarRows()[0].Outcome, left.W.ParticipantRows().Single(p => p.Polity == 0).LeftMs));
        Assert.Throws<InvalidOperationException>(() => left.R.RunUntil(B(2)));
    }

    // ================================================================== W-R1-6: no sack of one's own population (PROPOSAL L13)

    [Fact]
    public void OnesOwnPopulation_CannotBeSackedOrRazed_ARecaptureIsALiberation()
    {
        long t1 = B(1) + Day, t2 = B(3);
        Action<EventScheduler, WarOutcomeWorld> script = (s, _) => { Capture(s, t1, 0, 1, 10_000); Capture(s, t2, 0, 0, 10_000); };
        var b = Build(script, stops: true);
        long civ = -1, money = -1, treasury = -1;
        Drive(b, t2 + Day, at =>
        {
            if (at == t1) return new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), SackD) };          // teeth: a foreign victor may sack it
            if (at != t2) return None;
            civ = b.W.Ledger.Balance(b.W.CivilianAccount(0)); money = b.W.Ledger.Balance(b.W.MoneyAccount(0)); treasury = b.W.Ledger.Balance(b.W.TreasuryOf(0));
            return new[] { (OutcomeKinds.OrderDispose, Pack(0, 0), SackD), (OutcomeKinds.OrderDispose, Pack(0, 0), RazeD), (OutcomeKinds.OrderDispose, Pack(0, 0), OccupyD) };
        });
        Assert.Equal(new[] { 0, OutcomeRefusal.OwnPopulation, OutcomeRefusal.OwnPopulation, 0 }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.True(b.W.SettlementOutcomes[0].Killed > 0);
        var row = b.W.SettlementOutcomes[^1];
        Assert.Equal(((byte)Disposition.Occupy, 0L, 0L, 0L, 0L), (row.Disposition, row.Killed, row.Fled, row.LootMoney, row.GrievancePermilleYear));
        Assert.Equal((civ, money, treasury), (b.W.Ledger.Balance(b.W.CivilianAccount(0)), b.W.Ledger.Balance(b.W.MoneyAccount(0)), b.W.Ledger.Balance(b.W.TreasuryOf(0))));
        Assert.False(b.W.IsRazed(0));
        // an AI liberator whose doctrine is to sack goes through the same check and falls back to Occupy
        var ai = Build(script, ai: new[] { (int)Disposition.Sack, -1, -1 });
        ai.R.RunUntil(t2 + Day);
        Assert.Equal(((byte)Disposition.Occupy, 0L), (ai.W.SettlementOutcomes[^1].Disposition, ai.W.SettlementOutcomes[^1].Killed));
    }

    // ================================================================== W-R1-9: the eligibility matrix over lane S's siege endings

    private sealed class Wins : IAssaultResolver
    {
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k) => new(true, Q32.One, Q32.One, Q32.Zero, Q32.Zero);
    }

    private static readonly long Modern = SiegeRig.At(1950, 10);

    private static SiegeSpec Stalemate(SiegeSpec s) => s with { CorridorFoodPerYear = SiegeScenarios.RationYear(3_000, 6_000), CorridorMissilesPerYear = 1_000L * 360, Mineable = false };

    /// <summary>The rig for one siege ending (lane S's own recipes: SiegeWorldTests, OutcomeSiegeIntegrationTests).</summary>
    private static (BoundBuilt B, Func<long, (int, long, long)[]> Script) Ending(SiegeOutcome kind, long[] probes)
    {
        switch (kind)
        {
            case SiegeOutcome.Capitulated: return (Bound(Start, foodDays: 5, stops: true, probes: probes), _ => None);
            case SiegeOutcome.Stormed:
                return (Bound(Start, stops: true, probes: probes.Append(Start).Distinct().ToArray(), resolver: new Wins()),
                        at => at == Start ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 1_000L) } : None);
            case SiegeOutcome.GarrisonDestroyed: return (Bound(Start, stops: true, probes: probes, formations: Array.Empty<FormationSpec>()), _ => None);
            case SiegeOutcome.CapCapitulation:
                return (Bound(Modern, stops: true, probes: probes, schedule: SiegeRig.ProposedSchedule(), rates: SiegeRig.NoDesertion, siege: Stalemate,
                              inst: i => i with { LoyaltyPermille = 350 }), _ => None);
            case SiegeOutcome.CapLifted: return (Bound(Modern, stops: true, probes: probes, schedule: SiegeRig.ProposedSchedule(), siege: Stalemate), _ => None);
            case SiegeOutcome.Lifted:
                return (Bound(Start, stops: true, probes: probes.Append(Start + Day).Distinct().ToArray()),
                        at => at == Start + Day ? new[] { (SiegeKinds.OrderLift, Pack(1, 0), 0L) } : None);
            case SiegeOutcome.Relieved:
                return (Bound(Start, stops: true, probes: probes, resolver: new Wins(),
                              siege: s => s with { Relief = new ReliefSpec(Start + 10 * Day, new long[] { 20_000, 0, 0, 0 }, RangedKind.None) }), _ => None);
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>C32's eligibility matrix through lane S: only an ending that passes control (Capitulated, Stormed, GarrisonDestroyed,
    /// CapCapitulation) offers the besieger a disposition at its instant; Relieved, Lifted and CapLifted offer none (the order is a
    /// recorded refusal that changes nothing; the city stays the defender's and its leader reigns).</summary>
    [Theory]
    [InlineData(SiegeOutcome.Capitulated, true)]
    [InlineData(SiegeOutcome.Stormed, true)]
    [InlineData(SiegeOutcome.GarrisonDestroyed, true)]
    [InlineData(SiegeOutcome.CapCapitulation, true)]
    [InlineData(SiegeOutcome.Relieved, false)]
    [InlineData(SiegeOutcome.Lifted, false)]
    [InlineData(SiegeOutcome.CapLifted, false)]
    public void SiegeEndings_EligibilityMatrix_OnlyAPassOfControlOffersADisposition(SiegeOutcome kind, bool capture)
    {
        long limit = (kind == SiegeOutcome.CapCapitulation || kind == SiegeOutcome.CapLifted ? Modern : Start) + 2 * SimInstant.MsPerYear;
        var (twin, twinScript) = Ending(kind, Array.Empty<long>());
        DriveBound(twin, limit, twinScript);
        var end = Assert.Single(twin.Siege.Ends);
        Assert.Equal(kind, end.Outcome);
        long te = end.AtMs;
        var (b, script) = Ending(kind, new[] { te });
        long[]? before = null;
        var dispose = (OutcomeKinds.OrderDispose, Pack(1, 0), SackD);
        DriveBound(b, te, at =>
        {
            var o = script(at);
            if (at != te || before is not null) return o;
            before = b.W.Ledger.Snapshot();
            return o.Append(dispose).ToArray();
        });
        var result = b.W.OrderResults.Single(o => o.Kind == OutcomeKinds.OrderDispose);
        _out.WriteLine($"{kind}: end at {te}, disposition order reason {result.Reason}, controller {b.W.ControllerOf(0)}, fate {b.W.FateOf(0)}");
        Assert.Equal(te, result.AtMs);
        if (capture)
        {
            Assert.Equal(0, result.Reason);
            var row = Assert.Single(b.W.SettlementOutcomes);
            Assert.Equal(((byte)Disposition.Sack, 0, 1, te), (row.Disposition, row.From, row.To, row.AtMs));
            Assert.Equal((byte)kind, b.W.Battles[row.BattleId].EndCode);
            Assert.Equal((1, LeaderFate.Executed), (b.W.ControllerOf(0), b.W.FateOf(0)));     // city 0 was polity 0's only city
            Assert.Single(b.W.Battles);                                                           // fix r2: one row per siege
        }
        else
        {
            Assert.Equal(OutcomeRefusal.NotEligible, result.Reason);
            Assert.Empty(b.W.SettlementOutcomes);
            Assert.Equal((0, LeaderFate.Reigning), (b.W.ControllerOf(0), b.W.FateOf(0)));
            Assert.Equal(before, b.W.Ledger.Snapshot());                                          // a refusal changes nothing
            // fix r2 (W-R2-9): the raised siege is a battle of the record: the besieger (side 0) against the defender, who kept the
            // place (winner side 1), in their war, at the ending instant; no casualty is attributable; the cue is SiegeRaised
            var battle = Assert.Single(b.W.Battles);
            Assert.Equal(((byte)BattleKind.Siege, (byte)BattleMechanism.SiegeHealth, (byte)kind, kind == SiegeOutcome.CapLifted ? Modern : Start, te, 0, 1, 0),
                         (battle.Kind, battle.Mechanism, battle.EndCode, battle.StartMs, battle.EndMs, battle.SettlementId, battle.WinnerSide, battle.WarId));
            Assert.Equal(b.W.PulseOf(0, te), battle.Pulse);
            Assert.Equal(new[] { ((byte)0, 1), ((byte)1, 0) }, b.W.BattleSides.Select(x => (x.Side, x.Polity)).ToArray());
            Assert.Empty(b.W.Casualties);
            Assert.Equal(CinematicCue.SiegeRaised, CinematicSelector.ForBattle(battle, 1));
        }
    }

    // ================================================================== W-R1-10: the sack's input is in the state hash

    [Fact]
    public void TheSackIntensityInput_IsInTheRows_AndSoInTheStateHash()
    {
        long t = B(2);
        var b = Build((s, _) => Capture(s, t, 0, 1, 5_000), stops: true);
        Assert.Equal(t, b.R.RunUntil(t + Day).AtMs);           // at the post-battle window, where Sackers sets the sack's intensity
        string h0 = b.W.StateHashHex();
        Assert.Equal(5_000L, b.W.SettlementRows()[0].Sackers);
        var xs = (Array)typeof(WarOutcomeWorld).GetField("_x", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b.W)!;
        var x0 = xs.GetValue(0)!;
        x0.GetType().GetField("Sackers")!.SetValue(x0, 1L);
        Assert.NotEqual(h0, b.W.StateHashHex());
        // bound: the sackers are the besiegers present at the end (lane S's AttackerPeople then), not the start strength of the side row
        var bb = BuildBound(Start, foodDays: 5);
        bb.R.RunUntil(Start + 200 * Day);
        long tc = bb.Siege.Captures[0].AtMs;
        var at = BuildBound(Start, foodDays: 5);
        at.R.RunUntil(tc);
        var row = at.W.SettlementRows()[0];
        Assert.Equal(at.Siege.AttackerPeople(0), row.Sackers);
        Assert.NotEqual(at.W.BattleSides.Single(x => x.Side == 0).PersonnelStart, row.Sackers);
    }

    // ================================================================== W-R1-11: scheduling checks and the exchange delivery pin

    [Fact]
    public void ScheduleBattle_ChecksItsPolitiesAtScheduling_NotInsideTheEventLoop()
    {
        long tb = B(2) + Day;
        var b = Build();
        var same = new FieldBattle(0, tb - Day, 0, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        {
            new CollapseInput(0, 0, new long[] { 40, 0, 0, 0, 10 }, 500, 1_000), new CollapseInput(1, 1, new long[] { 50, 0, 0, 0, 0 }, 500, 1_000),
        });
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb, same));
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb, same with { WarId = -1 }));   // no war check to hide behind
        Assert.Throws<ArgumentOutOfRangeException>(() => b.W.ScheduleBattle(b.S, tb, same with { Side1Polity = 9, Formations = Array.Empty<CollapseInput>() }));
        var allies = new FieldBattle(0, tb - Day, 1, 2, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, Array.Empty<CollapseInput>());
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb, allies));   // on the same side of war 0
        b.W.ScheduleBattle(b.S, tb, allies with { WarId = -1 });                         // outside any war they may fight
        b.R.RunUntil(tb + Day);
        Assert.Equal(-1, Assert.Single(b.W.Battles).WarId);
    }

    private static FieldBattle Mutual(long tb) => new(0, tb - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
    {
        new CollapseInput(0, 1, new long[] { 30, 0, 0, 0, 20 }, 400, 1_000),   // 1,200 of polity 0's men into polity 1's custody
        new CollapseInput(2, 0, new long[] { 40, 0, 0, 0, 10 }, 900, 1_000),   // 500 of polity 1's men into polity 0's
    });

    /// <summary>Delivery pin (with ±1 neighbours): an exchange takes effect at the instant the second standing offer is issued, never
    /// before; polity 1 offers at TA, polity 0 at TA + offset.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Exchange_TakesEffectAtTheInstantTheSecondOfferStands(long offset)
    {
        long tb = B(2) + Day, ta = tb + 2 * Day, tOther = ta + offset, te = Math.Max(ta, tOther);
        var b = Build((s, w) => { w.ScheduleBattle(s, tb, Mutual(tb)); WarOutcomeWorld.ScheduleProbe(s, ta); if (tOther != ta) WarOutcomeWorld.ScheduleProbe(s, tOther); }, stops: true);
        Func<long, (int, long, long)[]> orders = at =>
            (at == ta ? new[] { (OutcomeKinds.OrderExchange, Pack(1, 0), 300L) } : None)
            .Concat(at == tOther ? new[] { (OutcomeKinds.OrderExchange, Pack(0, 1), 1_000L) } : None).ToArray();
        Drive(b, te - 1, orders);
        Assert.Equal((1_200L, 500L), (b.W.Ledger.Balance(b.W.CustodyOf(1, 0)), b.W.Ledger.Balance(b.W.CustodyOf(0, 1))));   // not yet
        Assert.DoesNotContain(b.W.PrisonerRows, r => r.Action == (byte)PrisonerAction.Exchange);
        Drive(b, ta + 2 * Day, orders);
        Assert.Equal(new[] { 0, 0 }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.Equal((900L, 200L), (b.W.Ledger.Balance(b.W.CustodyOf(1, 0)), b.W.Ledger.Balance(b.W.CustodyOf(0, 1))));
        var ex = b.W.PrisonerRows.Where(r => r.Action == (byte)PrisonerAction.Exchange).ToArray();
        Assert.Equal(new[] { (te, 300L), (te, 300L) }, ex.Select(r => (r.AtMs, r.Count)).ToArray());
    }

    // ================================================================== helpers

    private static void DriveBound(BoundBuilt b, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    /// <summary>OutcomeRig.BuildBound with lane S's knobs (start, schedule, rates, siege, installation, garrison, resolver) and an
    /// optional unbound second city held by polity 1 (so polity 1 may raze what it takes).</summary>
    private static BoundBuilt Bound(long start, long foodDays = 120, bool stops = false, long[]? probes = null, IAssaultResolver? resolver = null,
                                    WarPulseSchedule? schedule = null, LogisticsRates? rates = null, Func<SiegeSpec, SiegeSpec>? siege = null,
                                    Func<InstallationSpec, InstallationSpec>? inst = null, FormationSpec[]? formations = null, bool secondCity = false,
                                    RazePopulationRule raze = RazePopulationRule.KillThenDisplace)
    {
        long ws = start - 30 * Day;
        var i = new InstallationSpec(0, InstallationKind.City, 0, 10, 10, 10_000)
        {
            Civilians = 6_000, Food = SiegeRig.FoodDays(foodDays), Water = 9_000L * 60, WaterInflowPerYear = 9_000L * 360, Missiles = 1_000L * 200,
            Fort = FortKind.StoneWall, Housing = 12_000, ExitX = 11, ExitY = 10,
        };
        if (inst is not null) i = inst(i);
        formations ??= new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
        };
        var sp = new SiegeSpec(0, 1, 0, start, AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: 10))
        {
            AttackerFood = SiegeScenarios.Food(13_000, 0, 60), AttackerSupplyPerYear = SiegeScenarios.RationYear(13_000, 0), CampCapacity = 20_000,
            AttackerWorksPermille = 300, AttackerAi = null, DefenderAi = null,
        };
        if (siege is not null) sp = siege(sp);
        var s = new EventScheduler(ws);
        var sched = schedule ?? SiegeRig.FlatSchedule();
        var sw = new SiegeWorld(Cal, sched, ws, start + 20 * SimInstant.MsPerYear, SiegeClock.WarPulse, SiegeCoefficients.Default, rates ?? SiegeRig.Rates,
            new[] { SiegeRig.Powder, SiegeRig.Powder, SiegeRig.Powder }, ShortfallPolicy.ProRata, 7, new[] { i }, formations, new[] { sp }, null,
            openingMoney: 1_000_000_000, homeFood: 1_000_000_000_000, homeMunitions: 1_000_000_000_000, homeMissiles: 1_000_000_000_000, homeTroops: 0,
            resolver: resolver);
        var pol = new[]
        {
            new OutcomePolity(0, 100) { Seat = 0, Institutions = Coin },
            new OutcomePolity(1, 101) { Institutions = Coin },
            new OutcomePolity(2, 102) { Institutions = Coin },
        };
        var set = new List<OutcomeSettlement> { new(0, true, 0) { Installation = 0, Money = 500_000, Buildings = 1_000, BaseGrievancePermilleYear = 1_000 } };
        if (secondCity) set.Add(new OutcomeSettlement(1, true, 1) { Civilians = 1_000, Grain = 1_000, Money = 1_000, Buildings = 10 });
        var wars = new[] { new OutcomeWar(0, ws, new[] { 1 }, new[] { 0 }) };
        var w = new WarOutcomeWorld(Cal, sched, pol, set.ToArray(), wars, Array.Empty<OutcomeFormation>(), OutcomeCoefficients.Proposal,
                                    CaptiveExecutionRule.NotPermitted, raze, siege: sw);
        w.Begin(s);
        foreach (var p in probes ?? Array.Empty<long>()) SiegeWorld.ScheduleProbe(s, p);
        var r = new Runner(s, w, WarOutcomeWorld.Significant(true), stops);
        return new BoundBuilt(w, sw, s, r, start, ws);
    }
}
