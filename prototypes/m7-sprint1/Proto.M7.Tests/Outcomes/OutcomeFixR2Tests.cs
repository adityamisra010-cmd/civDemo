using Proto.M7.Events;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Proto.M7.Tests.Siege;
using FormationSpec = Proto.M7.War.Siege.FormationSpec;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// Lane W fix round 2: one semantic test (or more) per reproduced verifier finding W-R2-1 .. W-R2-10. Each asserts a property the system
/// ought to have by DIR5 / C31-C33 or by the lane's stated rule. The cases that pin changed behaviour failed on 9c5f135 (measured); the
/// coverage cases (W-R2-6, W-R2-7, W-R2-8 and the MV-1 / MV-5 pins) pass there, because the code was right, and each kills a mutant
/// that survived the whole suite there (measured). Every number is a TEST INPUT.
/// </summary>
public class OutcomeFixR2Tests
{
    private const long SackD = (long)Disposition.Sack, RazeD = (long)Disposition.Raze;
    private static readonly long Start = SiegeRig.At(1500, 100);
    private static readonly long Ws = Start - 30 * Day;

    // ------------------------------------------------------------------ a lane S rig with its knobs exposed

    /// <summary>Lane S's rig installation 0 (5 days of food: it capitulates), its garrison owned by its controller.</summary>
    private static InstallationSpec Inst(InstallationKind kind, int controller, int population = -1) => new(0, kind, controller, 10, 10, 10_000)
    {
        Civilians = 6_000, Food = SiegeRig.FoodDays(5), Water = 9_000L * 60, WaterInflowPerYear = 9_000L * 360, Missiles = 1_000L * 200,
        Fort = FortKind.StoneWall, Housing = 12_000, ExitX = 11, ExitY = 10, PopulationPolity = population,
    };

    private static OutcomePolity[] Pols(int seat0, int seat1) => new[]
    {
        new OutcomePolity(0, 100) { Seat = seat0, Institutions = Coin }, new OutcomePolity(1, 101) { Seat = seat1, Institutions = Coin },
        new OutcomePolity(2, 102) { Institutions = Coin },
    };

    /// <summary>Installation <paramref name="inst"/> besieged by <paramref name="besieger"/> from Start; the outcome world wraps it.</summary>
    private static BoundBuilt Sieged(InstallationSpec inst, int besieger, OutcomePolity[] pol, OutcomeSettlement[] set, OutcomeWar[] wars,
                                     bool stops = false, Action<EventScheduler, WarOutcomeWorld>? setup = null)
    {
        var forms = new[]
        {
            new FormationSpec(0, inst.Controller, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, inst.Controller, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
        };
        var sp = new SiegeSpec(0, besieger, 0, Start, AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: 10))
        {
            AttackerFood = SiegeScenarios.Food(13_000, 0, 60), AttackerSupplyPerYear = SiegeScenarios.RationYear(13_000, 0), CampCapacity = 20_000,
            AttackerWorksPermille = 300, AttackerAi = null, DefenderAi = null,
        };
        var s = new EventScheduler(Ws);
        var sched = SiegeRig.FlatSchedule();
        var sw = new SiegeWorld(Cal, sched, Ws, Start + 20 * SimInstant.MsPerYear, SiegeClock.WarPulse, SiegeCoefficients.Default, SiegeRig.Rates,
            new[] { SiegeRig.Powder, SiegeRig.Powder, SiegeRig.Powder }, ShortfallPolicy.ProRata, 7, new[] { inst }, forms, new[] { sp }, null,
            openingMoney: 1_000_000_000, homeFood: 1_000_000_000_000, homeMunitions: 1_000_000_000_000, homeMissiles: 1_000_000_000_000, homeTroops: 0);
        var w = new WarOutcomeWorld(Cal, sched, pol, set, wars, Array.Empty<OutcomeFormation>(), OutcomeCoefficients.Proposal,
                                    CaptiveExecutionRule.NotPermitted, RazePopulationRule.KillThenDisplace, siege: sw);
        w.Begin(s);
        setup?.Invoke(s, w);
        return new BoundBuilt(w, sw, s, new Runner(s, w, WarOutcomeWorld.Significant(true), stops), Start, Ws);
    }

    private static void DriveBound(BoundBuilt b, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    // ================================================================== W-R2-1: whose people live in a bound city is lane S's too

    /// <summary>Lane S's city 0 is held by polity 1, but its people are polity 0's (lane S: PopulationPolity 0). The outcome world must
    /// say the same (a disagreement is refused at construction). Agreeing, polity 0's capture is a liberation: lane S does not count it
    /// occupied, and the liberator may not sack its own people (L13): nothing moves.</summary>
    [Theory]
    [InlineData(-1, false)]   // -1 = lane W's initial controller (polity 1): disagrees with lane S
    [InlineData(2, false)]
    [InlineData(0, true)]
    public void ABoundCity_AgreesWithLaneSOnWhosePeopleLiveThere_ALiberatorMayNotSackThem(int laneWPopulation, bool agrees)
    {
        BoundBuilt Make(bool stops) => Sieged(Inst(InstallationKind.City, 1, population: 0), 0, Pols(-1, 0),
            new[]
            {
                new OutcomeSettlement(0, true, 1) { Installation = 0, Money = 500_000, Buildings = 1_000, BaseGrievancePermilleYear = 1_000, PopulationPolity = laneWPopulation },
                new OutcomeSettlement(1, true, 0) { Civilians = 1_000, Grain = 1_000, Money = 1_000, Buildings = 10 },
            },
            new[] { new OutcomeWar(0, Ws, new[] { 0 }, new[] { 1 }) }, stops);
        if (!agrees)
        {
            var e = Assert.Throws<ArgumentException>(() => Make(false));
            Assert.Contains("population polity", e.Message);
            return;
        }
        var plain = Make(false);
        plain.R.RunUntil(Start + 200 * Day);
        long tc = Assert.Single(plain.Siege.Captures).AtMs;
        var b = Make(true);
        DriveBound(b, Start + 200 * Day, at => at == tc && b.W.OrderResults.Count == 0 ? new[] { (OutcomeKinds.OrderDispose, Pack(0, 0), SackD) } : None);
        Assert.Equal(OutcomeRefusal.OwnPopulation, Assert.Single(b.W.OrderResults).Reason);
        Assert.Equal(0, b.W.SettlementRows()[0].PopulationPolity);
        Assert.Equal((0, false), (b.Siege.ControllerOf(0), b.Siege.IsOccupied(0)));
        var row = b.W.SettlementOutcomes[^1];
        Assert.Equal(((byte)Disposition.Occupy, 0L, 0L, 0L, 0L), (row.Disposition, row.Killed, row.Fled, row.LootMoney, row.GrievancePermilleYear));
        Assert.Equal(plain.W.Ledger.Snapshot(), b.W.Ledger.Snapshot());   // the refused sack moved no one and nothing
    }

    /// <summary>Standalone: the population polity (not the controller) decides whose own people they are (L13) and whose refugees the
    /// sacked become. City 3 is polity 1's; its people are polity 0's (a liberation: no sack) or polity 2's (a sack sends them to polity 2).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ThePopulationPolity_NotTheController_DecidesWhoseOwnPeopleTheyAre_AndWhoseRefugeesTheSackedBecome(int population)
    {
        long t = B(2) + Day;
        var b = Build((s, _) => Capture(s, t, 3, 0, 5_000), stops: true, settlements: x => { x[3] = x[3] with { PopulationPolity = population }; return x; });
        Drive(b, t + Day, at => at == t ? new[] { (OutcomeKinds.OrderDispose, Pack(0, 3), SackD) } : None);
        var row = b.W.SettlementOutcomes[^1];
        if (population == 0)
        {
            Assert.Equal(OutcomeRefusal.OwnPopulation, Assert.Single(b.W.OrderResults).Reason);
            Assert.Equal(((byte)Disposition.Occupy, 0L, 0L), (row.Disposition, row.Killed + row.Fled, row.GrievancePermilleYear));
            return;
        }
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        Assert.True(row.Fled > 0 && row.Killed > 0);
        Assert.Equal(row.Fled, b.W.Ledger.Balance(b.W.RefugeesOf(2)));          // the people's own polity, not the controller's (polity 1)
        Assert.Equal(0L, b.W.Ledger.Balance(b.W.RefugeesOf(1)));
        Assert.True(row.GrievancePermilleYear > 1_000);
    }

    // ================================================================== W-R2-2: one battle at a time per campaign formation

    private static FieldBattle Half(long start, int formation) => new(0, start, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        { new CollapseInput(formation, 1, new long[] { 25, 0, 0, 25, 0 }, 500, 1_000) });

    /// <summary>Battle A holds campaign formation 0 over (tb − 1 d, tb]. Battle B (to tb + 1 d) with the same formation is refused at
    /// scheduling if it starts before tb (pinned at tb − 1 ms) and accepted from tb on, when it is fought by what A left: A kills half of
    /// 3,000, B half of the 1,500 left. Two battles ending at one instant are refused; another formation may fight at the same time.</summary>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void ACampaignFormation_FightsOneBattleAtATime_InSequenceItFightsWhatTheLastLeft(long offset, bool accepted)
    {
        long tb = B(2) + Day;
        var b = Build();
        b.W.ScheduleBattle(b.S, tb, Half(tb - Day, 0));
        if (!accepted)
        {
            Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb + Day, Half(tb + offset, 0)));
            b.R.RunUntil(tb + 2 * Day);
            Assert.Equal((1_500L, 1_500L), (b.W.Ledger.Balance(b.W.KilledSink), b.W.Ledger.Balance(b.W.FormationAccount(0))));   // A alone
            return;
        }
        b.W.ScheduleBattle(b.S, tb + Day, Half(tb + offset, 0));
        b.R.RunUntil(tb + 2 * Day);
        Assert.Equal(new[] { 3_000L, 1_500L }, b.W.BattleSides.Where(x => x.Side == 1).Select(x => x.PersonnelStart).ToArray());
        Assert.Equal((2_250L, 750L), (b.W.Ledger.Balance(b.W.KilledSink), b.W.Ledger.Balance(b.W.FormationAccount(0))));
    }

    [Fact]
    public void TwoBattlesEndingAtOneInstant_CannotShareAFormation_TwoFormationsMayFightAtOnce()
    {
        long tb = B(2) + Day;
        var b = Build();
        b.W.ScheduleBattle(b.S, tb, Half(tb - Day, 0));
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb, Half(tb, 0)));          // zero-length, same end instant
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, tb + Day, Half(tb - 2 * Day, 0)));   // B's span contains A's
        b.W.ScheduleBattle(b.S, tb, Half(tb - Day, 1));                                            // formation 1 (1,000 men) at the same time
        b.R.RunUntil(tb + Day);
        Assert.Equal((1_500L, 500L), (b.W.Ledger.Balance(b.W.FormationAccount(0)), b.W.Ledger.Balance(b.W.FormationAccount(1))));
    }

    // ================================================================== W-R2-3: never the captive of one's own polity

    /// <summary>Polity 0 (cities 0 = seat, 3) loses its seat to polity 1 at t1 (its leader is polity 1's captive) and takes polity 1's
    /// city 1 at t2. At the t2 window polity 1 releases the leader, who goes to polity 0's lowest-id city, 1; polity 0 then razes city 1.
    /// The ruin is no seat, and nobody took it: the leader reigns on from city 3 (no captor, no fate row by its own polity), and the former
    /// captor has no captive left to decide on.</summary>
    [Fact]
    public void ALeaderReleasedIntoACityItsOwnPolityThenRazes_MovesToItsLowestIdCity_NeverItsOwnCaptive()
    {
        long t1 = B(1) + Day, t2 = B(2) + Day;
        var b = Build((s, _) => { Capture(s, t1, 0, 1); Capture(s, t2, 1, 0); }, stops: true,
            settlements: x => { x[1] = x[1] with { Controller = 1 }; x[3] = x[3] with { Controller = 0 }; return x; });
        Drive(b, t2 + Day, at => at == t2 ? new[]
        {
            (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Release, 0)),
            (OutcomeKinds.OrderDispose, Pack(0, 1), RazeD),
            (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Release, 0)),
        } : None);
        Assert.Equal(new[] { 0, 0, OutcomeRefusal.NotEligible }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.True(b.W.IsRazed(1));
        Assert.Equal((LeaderFate.Reigning, -1, 3), (b.W.FateOf(0), b.W.HeldBy(0), b.W.SeatOf(0)));
        Assert.Equal(new[] { ((byte)LeaderFate.Captive, (byte)FateCause.SeatTaken, 1, 0), ((byte)LeaderFate.Reigning, (byte)FateCause.Released, 1, 1) },
                     b.W.Fates.Select(f => (f.To, f.Cause, f.ByPolity, f.SettlementId)).ToArray());   // released into city 1, its lowest-id city then
        Assert.DoesNotContain(b.W.Fates, f => f.ByPolity == f.Polity || f.HeldBy == f.Polity);
        Assert.Equal(3, b.W.PolityRows()[0].Seat);
    }

    /// <summary>A released leader returns to its polity's lowest-id city (cities 1 and 3 remain: city 1), whatever the scan order.</summary>
    [Fact]
    public void AReleasedLeader_ReturnsToItsPolitysLowestIdCity()
    {
        long t1 = B(1) + Day, t2 = B(2) + Day;
        var b = Build((s, _) => Capture(s, t1, 0, 1), stops: true, probes: new[] { t2 },
            settlements: x => { x[3] = x[3] with { Controller = 0 }; return x; });
        Drive(b, t2 + Day, at => at == t2 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Release, 0)) } : None);
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        Assert.Equal((LeaderFate.Reigning, 1), (b.W.FateOf(0), b.W.SeatOf(0)));
        Assert.Equal(1, b.W.Fates[^1].SettlementId);
    }

    // ================================================================== W-R2-4: every lane S city is seen by the leader rule

    /// <summary>A lane S City that no settlement binds would be lost without the leader rule seeing it: refused at construction. An
    /// unbound Fort is accepted: its capture is recorded (siege row, no settlement) and, being no city, leaves the leader reigning.</summary>
    [Theory]
    [InlineData(InstallationKind.City)]
    [InlineData(InstallationKind.Fort)]
    public void ALaneSCity_MustBeBound_AFortNeedNotBe(InstallationKind kind)
    {
        BoundBuilt Make() => Sieged(Inst(kind, 0), 1, Pols(0, -1), new[] { new OutcomeSettlement(0, true, 0) { Civilians = 1_000, Grain = 1_000, Money = 1_000, Buildings = 10 } },
                                    new[] { new OutcomeWar(0, Ws, new[] { 1 }, new[] { 0 }) });
        if (kind == InstallationKind.City)
        {
            var e = Assert.Throws<ArgumentException>(() => Make());
            Assert.Contains("not bound", e.Message);
            return;
        }
        var b = Make();
        b.R.RunUntil(Start + 200 * Day);
        var c = Assert.Single(b.Siege.Captures);
        var battle = Assert.Single(b.W.Battles);
        Assert.Equal(((byte)BattleKind.Siege, (byte)SiegeOutcome.Capitulated, c.AtMs, -1, 0), (battle.Kind, battle.EndCode, battle.EndMs, battle.SettlementId, battle.WarId));
        Assert.Equal((LeaderFate.Reigning, 1L), (b.W.FateOf(0), b.W.CityCount(0)));
    }

    // ================================================================== W-R2-5: a battle that outlasts its war

    /// <summary>Polity 0 loses both its cities at B(1): it is eliminated and war 0 ends in conquest at B(1). A field battle between
    /// polities 1 and 0 declared in war 0 is recorded in war 0 (with war 0's pulse) if it ends at B(1) − 1 or at B(1) itself (world events
    /// at one instant land before the instant is settled), and outside any war if it ends at B(1) + 1. Its prisoners are taken either way.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void AFieldBattleThatOutlastsItsWar_IsRecordedOutsideIt_PinnedAtTheWarsEnd(long offset)
    {
        long te = B(1) + offset;
        var fb = new FieldBattle(0, te - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
            { new CollapseInput(0, 1, new long[] { 25, 0, 0, 0, 25 }, 500, 1_000), new CollapseInput(2, 0, new long[] { 50, 0, 0, 0, 0 }, 500, 1_000) });
        var b = Build((s, w) => { Capture(s, B(1), 0, 1); Capture(s, B(1), 1, 2); w.ScheduleBattle(s, te, fb); });
        b.R.RunUntil(B(1) + Day);
        Assert.Equal(((byte)WarOutcome.Conquest, B(1)), (b.W.WarRows()[0].Outcome, b.W.WarRows()[0].EndMs));
        var battle = Assert.Single(b.W.Battles, x => x.Kind == (byte)BattleKind.Field);
        Assert.Equal(te, battle.EndMs);
        if (offset <= 0) Assert.Equal((0, PulseOracle(te)), (battle.WarId, battle.Pulse));
        else Assert.Equal((-1, -1L), (battle.WarId, battle.Pulse));
        Assert.Equal(1_500L, b.W.Ledger.Balance(b.W.CustodyOf(1, 0)));
    }

    // ================================================================== W-R2-6: a lane S capture after the defender has left its war

    /// <summary>Polity 0 holds a lane S fort (unbound) and one standalone city, its seat. If that city is taken before the fort
    /// capitulates, polity 0 is eliminated (war 0 ends, or goes on without it): the fort's capture is recorded outside any war. If not,
    /// the capture is in war 0 with war 0's pulse.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ALaneSCapture_IsRecordedOnlyInAWarInWhichBothStillFight(bool eliminatedFirst, bool warGoesOn)
    {
        var wars = warGoesOn ? new[] { new OutcomeWar(0, Ws, new[] { 1 }, new[] { 0, 2 }) } : new[] { new OutcomeWar(0, Ws, new[] { 1 }, new[] { 0 }) };
        var b = Sieged(Inst(InstallationKind.Fort, 0), 1, Pols(0, -1), new[] { new OutcomeSettlement(0, true, 0) { Civilians = 1_000, Grain = 1_000, Money = 1_000, Buildings = 10 } },
                       wars, setup: (s, _) => { if (eliminatedFirst) WarOutcomeWorld.ScheduleCapture(s, Start - Day, 0, 1, 0, 1_000); });
        b.R.RunUntil(Start + 200 * Day);
        var c = Assert.Single(b.Siege.Captures);
        var battle = b.W.Battles[^1];
        Assert.Equal(((byte)BattleKind.Siege, c.AtMs), (battle.Kind, battle.EndMs));
        Assert.Equal(0, b.W.WarBetween(1, 0));                                   // the two were opposed in war 0 ...
        if (!eliminatedFirst) { Assert.Equal((0, (c.AtMs - Ws) / (7 * Day)), (battle.WarId, battle.Pulse)); return; }
        Assert.Equal(Start - Day, b.W.ParticipantRows().Single(p => p.Polity == 0).LeftMs);   // ... until polity 0 left it
        Assert.Equal((byte)(warGoesOn ? WarOutcome.Ongoing : WarOutcome.Conquest), b.W.WarRows()[0].Outcome);
        Assert.Equal((-1, -1L), (battle.WarId, battle.Pulse));
        Assert.Equal(c.Prisoners, b.W.Ledger.Balance(b.W.CustodyOf(1, 0)));
    }

    // ================================================================== W-R2-7: the captured garrison eats from its intake instant

    [Fact]
    public void TheGarrisonLaneSTakesPrisoner_EatsFromItsIntakeInstant()
    {
        var plain = BuildBound(Start, foodDays: 5);
        plain.R.RunUntil(Start + 200 * Day);
        var c = Assert.Single(plain.Siege.Captures);
        long ts = Cal.TurnStart(Cal.TurnOf(new SimInstant(c.AtMs)) + 1).Ms;
        Assert.True(ts > c.AtMs);
        var b = BuildBound(Start, foodDays: 5);
        b.R.RunUntil(c.AtMs);
        var cu = b.W.CustodyRows().Single(x => x.Captor == 1 && x.Origin == 0);
        Assert.Equal((c.AtMs, c.Prisoners, 0L), (cu.SettledMs, b.W.Ledger.Balance(cu.Account), b.W.Ledger.Balance(b.W.PrisonerFoodSink)));
        b.R.RunUntil(ts);
        var carry = default(ExactFraction);
        long due = RateIntegrator.IntegrateExact(checked(c.Prisoners * b.W.Rates.RationBase), ts - c.AtMs, SimInstant.MsPerYear, ref carry);
        Assert.True(due > 0);
        var snap = Assert.Single(b.W.TurnSnapshots, x => x.AtMs == ts);
        Assert.Equal(due, snap.Balances[b.W.PrisonerFoodSink]);                 // exactly their rations since the intake, no more
        cu = b.W.CustodyRows().Single(x => x.Captor == 1 && x.Origin == 0);
        Assert.Equal((ts, carry.Num, carry.Den, 0L), (cu.SettledMs, cu.CarryNum, cu.CarryDen, cu.FoodArrears));
    }

    // ================================================================== W-R2-8: three stated properties, now pinned

    /// <summary>The raze rule is about razes: a SACK kills the same under both values of the Director-level flag.</summary>
    [Fact]
    public void TheRazeRule_LeavesASackUntouched()
    {
        long t = B(2);
        Built Sacked(RazePopulationRule r)
        {
            var x = Build((s, _) => Capture(s, t, 0, 1, 5_000), stops: true, raze: r);
            Drive(x, t + Day, at => at == t ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), SackD) } : None);
            Assert.Equal(0, Assert.Single(x.W.OrderResults).Reason);
            return x;
        }
        var kill = Sacked(RazePopulationRule.KillThenDisplace);
        var displace = Sacked(RazePopulationRule.Displace);
        Assert.Equal(529L, displace.W.SettlementOutcomes[^1].Killed);            // the sack's killing at q = 1/2 (DispositionTests' golden)
        Assert.Equal(kill.W.SettlementOutcomes[^1], displace.W.SettlementOutcomes[^1]);
        Assert.Equal(kill.W.RecordHashHex(), displace.W.RecordHashHex());
        Assert.Equal(kill.W.Ledger.Snapshot(), displace.W.Ledger.Snapshot());
    }

    /// <summary>Polity 0 (cities 0 = seat, 1, 3) loses its seat to one victor and city 1 to the other at the same instant and survives
    /// (city 3): its leader is the captive of the polity that took the SEAT, not of the instant's last loss (city 1, key order).</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SeatAndAnotherCityLostToTwoVictorsAtOneInstant_TheLeaderIsTheSeatTakersCaptive(int seatTaker)
    {
        long t = B(2);
        int other = 3 - seatTaker;
        var b = Build((s, _) => { Capture(s, t, 0, seatTaker); Capture(s, t, 1, other); }, settlements: x => { x[3] = x[3] with { Controller = 0 }; return x; });
        b.R.RunUntil(t + Day);
        Assert.Equal((LeaderFate.Captive, seatTaker, 1L), (b.W.FateOf(0), b.W.HeldBy(0), b.W.CityCount(0)));
        var f = Assert.Single(b.W.Fates);
        Assert.Equal(((byte)FateCause.SeatTaken, seatTaker, 0, 0, t), (f.Cause, f.ByPolity, f.SettlementId, f.WarId, f.AtMs));
        Assert.Equal(other, b.W.PolityRows()[0].LastLossBy);                    // the instant's last loss was city 1, to the other victor
    }

    // ================================================================== W-R2-9: a captive-leader decision names the captivity's war

    /// <summary>Wars 0 (polities 1, 2 against 0) and 1 (polity 1 against 0, 3 days later). Polity 1 takes polity 0's seat in war 1: the
    /// release (or, where permitted, the execution) of the captive names war 1 and war 1's pulse, like the captivity, not war 0 (the
    /// lowest-id war between them).</summary>
    [Theory]
    [InlineData(LeaderDecision.Release)]
    [InlineData(LeaderDecision.Execute)]
    public void ACaptiveLeaderDecision_NamesTheWarOfTheCaptivity(LeaderDecision decision)
    {
        var wars = new[] { new OutcomeWar(0, W0, new[] { 1, 2 }, new[] { 0 }), new OutcomeWar(1, W0 + 3 * Day, new[] { 1 }, new[] { 0 }) };
        long t1 = B(1) + Day, t2 = B(3);
        var b = Build((s, _) => WarOutcomeWorld.ScheduleCapture(s, t1, 0, 1, 1, 5_000), stops: true, wars: wars, probes: new[] { t2 },
                      rule: CaptiveExecutionRule.Permitted);
        Drive(b, t2 + Day, at => at == t2 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)decision, 0)) } : None);
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        Assert.Equal(0, b.W.WarBetween(1, 0));
        Assert.Equal(2, b.W.Fates.Count);
        var captive = b.W.Fates[0];
        var d = b.W.Fates[1];
        Assert.Equal((byte)(decision == LeaderDecision.Release ? FateCause.Released : FateCause.ExecutedAsCaptive), d.Cause);
        Assert.Equal((1, 1), (captive.WarId, d.WarId));
        Assert.Equal((t2 - (W0 + 3 * Day)) / (7 * Day), d.Pulse);               // war 1's own clock
    }

    /// <summary>The captivity's war ended meanwhile (polity 0 took back its seat and every city of polities 1 and 2: war 0 ended in
    /// conquest): a release by the former captor afterwards is recorded outside any war, as a battle after its war is (W-R2-5).</summary>
    [Fact]
    public void ACaptiveLeaderReleasedAfterTheCaptivitysWarEnded_IsRecordedOutsideAnyWar()
    {
        long t1 = B(1) + Day, t2 = B(2), t3 = B(3);
        var b = Build((s, _) => { Capture(s, t1, 0, 1); foreach (int x in new[] { 0, 2, 3, 4 }) Capture(s, t2, x, 0); }, stops: true, probes: new[] { t3 });
        Drive(b, t3 + Day, at => at == t3 ? new[] { (OutcomeKinds.OrderLeader, Pack(1, 0), Value((long)LeaderDecision.Release, 0)) } : None);
        Assert.Equal(((byte)WarOutcome.Conquest, t2, 1), (b.W.WarRows()[0].Outcome, b.W.WarRows()[0].EndMs, b.W.WarRows()[0].WinnerSide));
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        var d = b.W.Fates.Single(f => f.Polity == 0 && f.Cause == (byte)FateCause.Released);
        Assert.Equal((t3, -1, -1L), (d.AtMs, d.WarId, d.Pulse));
        Assert.Equal(0, b.W.Fates.Single(f => f.Polity == 0 && f.Cause == (byte)FateCause.SeatTaken).WarId);
        Assert.Equal((LeaderFate.Reigning, 0), (b.W.FateOf(0), b.W.SeatOf(0)));     // its lowest-id city: the old seat, taken back
    }

    // ================================================================== W-R2-10: a scheduled battle's inputs are world state

    [Fact]
    public void AScheduledBattlesInputs_AreInTheStateHash()
    {
        long tb = B(2) + Day;
        FieldBattle Fb(long dead) => new(0, tb - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
            { new CollapseInput(0, 1, new long[] { 50 - dead, 0, 0, dead, 0 }, 500, 1_000) });
        var a = Build((s, w) => w.ScheduleBattle(s, tb, Fb(0)));
        var same = Build((s, w) => w.ScheduleBattle(s, tb, Fb(0)));
        var c = Build((s, w) => w.ScheduleBattle(s, tb, Fb(50)));
        var later = Build((s, w) => w.ScheduleBattle(s, tb + 1, Fb(0)));
        foreach (var x in new[] { a, same, c, later }) x.R.RunUntil(tb - 1);
        Assert.Equal(a.W.StateHashHex(), same.W.StateHashHex());
        Assert.NotEqual(a.W.StateHashHex(), c.W.StateHashHex());                 // the futures differ (0 vs 3,000 dead): so do the states
        Assert.NotEqual(a.W.StateHashHex(), later.W.StateHashHex());
        a.R.RunUntil(tb + Day); c.R.RunUntil(tb + Day);
        Assert.Equal((0L, 3_000L), (a.W.Ledger.Balance(a.W.KilledSink), c.W.Ledger.Balance(c.W.KilledSink)));
    }
}
