using Proto.M7.Morale;
using Proto.M7.Sandbox;
using Proto.M7.Spatial;
using Proto.M7.Support;
using Proto.M7.Tactical;
using Proto.M7.Tests.MultiDomain;
using Proto.M7.Tests.Tactical;
using Proto.M7.War.Outcomes;
using static Proto.M7.Tactical.TacticalScenarios;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Morale;

/// <summary>
/// [MOR2, verification round 1] Tests for the round-1 findings on lane MOR2's entry cost (D-083 DB1; docs/m7-plan/morale-proposal.md
/// §11; reports/H-MOR2.md §13). Each test names the finding it pins. Every number is a TEST INPUT or the PROPOSAL (settlement entry
/// cost 50,000; in-battle part 50 when switched on by the test). Real battles go through the stand-in hand-off
/// (<see cref="EntryCostTests.HandOff"/>) and lane D's settlement, as M7.15 would.
/// </summary>
public sealed class EntryCostRound1Tests
{
    private const long Cost = 50;
    private const long CostPpm = 50_000;
    private const long Cell50 = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell50);

    // ------------------------------------------------------------------ MOR2-R1-01: a formation that never entered can be in combat

    /// <summary>
    /// MOR2-R1-01 (and MOR2-R1-04's reading). Lane N's own scenario (NavalTests: a turret warship shoots a transport): 20 riflemen at
    /// morale 800 stay aboard the transport and never reach the field (EnteredAtMs null, no charge), yet 7 of them are killed aboard and
    /// their formation's morale falls to 100. Until round 1 the stand-in hand-off REFUSED this real battle report ("a formation that
    /// never reached the field has no charge, loss or casualty"). Now it is settled: no entry cost (DB1-R1 (a): it never entered), DR29's
    /// decrease of the morale the battle cost it, (800 − 100) × 1,000 = 700,000 (it took part in the combat: DR29), and no DA1 rise
    /// (its side lost anyway); lane W's collapse moves its 700 dead of 2,000 persons (7 of 20 entities) to the dead by ledger transfer —
    /// so "a formation that never entered moves nobody" (proposal §11.5, struck) did not hold. The transport, a vessel, entered at the
    /// battle's start. MEASURED values, exact.
    /// </summary>
    [Fact]
    public void NeverEntered_TroopsKilledAboardTheirTransport_TheHandOffSettlesIt_DR29Applies_NoEntryCost()
    {
        var setup = DT.Sea(100_000, 12, null,
            DT.Ship(1, 0, DomainProfiles.SteamTransport with { Capacity = 30 }, DT.M(3000, 2000)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, DT.M(3000, 2000)) { EmbarkedOn = 1, Morale = 800 },
            DT.Ship(5, 1, DomainProfiles.TurretWarship, DT.M(5000, 2000)));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = DT.RunChecked(b);
        var f2 = r.Formations.Single(x => x.FormationId == 2);
        Assert.Equal(((long?)null, 0L), (f2.EnteredAtMs, f2.EntryCharged));                       // never on the field
        Assert.Equal((7L, 100L), (f2.Killed, f2.MoraleEnd));                                      // ... yet killed aboard, morale fallen
        Assert.Equal(setup.StartMs, r.Formations.Single(x => x.FormationId == 1).EnteredAtMs);    // the transport: from the start
        Assert.Equal(1, BattleWinner.Reported(r));
        var mine = BattleCollapse.FromTactical(b, setup, new[] { (1L, 2), (2L, 0), (5L, 1) }).Single(x => x.CampaignFormation == 0);
        Assert.Equal(new long[] { 13, 0, 0, 7, 0 }, mine.EntityCounts);
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, EntryCostTests.HandOff(r, 2, army: 0, at, mine, startMorale: 800, beaten: 20, winnerSide: 20));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal(((byte)0, 0L, 0L, 0L, 700_000L, 0L), (m.Entered, m.EntryInBattle, m.EntryPpm, m.Restore, m.DecreasePpm, m.RisePpm));
        Assert.Equal((800_000L, 100_000L), (m.BeforePpm, m.AfterPpm));
        var row = rig.W.BattleRows.Single();
        Assert.Equal((2_000L, 700L, 0L, 0L, 0L), (row.Persons, row.Dead, row.Wounded, row.Captured, row.Deserted));
        Assert.Equal(1_300L, rig.W.Ledger.Balance(rig.W.Economy.MenAccountOf(0)));
    }

    // ------------------------------------------------------------------ MOR2-R1-02: an aircraft at its base is not on the field

    /// <summary>Lane N's ridge field (AirTests): riflemen a side, and an aircraft formation of side 0 (one airframe) at its base.</summary>
    private static BattleSetup AirField(ulong seed, AirframeClass a, Vec2L[]? obj0 = null, Vec2L[]? obj1 = null)
    {
        var g = new TerrainGrid(64, 40, Cell50, TerrainClass.Open);
        for (int y = 5; y < 35; y++) g.Set(30, y, TerrainClass.Hills, 40_000);
        var s = DT.OnGrid(g, seed, null, null,
            new FormationSpec(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)),
            MultiDomainScenarios.Flight(3, 0, a, 1) with { Morale = 800 });
        return obj0 is null ? s : s with { Sides = MultiDomainScenarios.Sides(obj0, obj1) };
    }

    /// <summary>
    /// MOR2-R1-02. An aircraft formation that never leaves its base (Phase Based at every completed instant, so never on the field by
    /// the engine's own DEnt.OnField) never enters: EnteredAtMs null (until round 1 it was reported entered at StartMs, and its army
    /// paid the 50,000 settlement entry term for a battle it never reached). Through the hand-off its army pays nothing: no entry, no
    /// decrease, no rise (800,000). The riflemen on the ground entered at the start; nobody was charged in battle (the part is off).
    /// </summary>
    [Fact]
    public void Aircraft_AFormationThatNeverLeavesItsBase_NeverEnters_AndItsArmyPaysNothing()
    {
        var setup = AirField(4, DomainProfiles.ReconAircraft);
        var b = new TacticalBattle(setup, new ScriptedController(), PassiveController.Instance);
        bool everUp = false; long instants = 0;
        var r = DT.RunChecked(b, onInstant: () => { instants++; if (DT.D(b, 3000).Phase != AirPhase.Based) everUp = true; });
        Assert.False(everUp);
        Assert.True(instants > 10, $"the battle ran ({instants} completed instants)");
        var f3 = r.Formations.Single(x => x.FormationId == 3);
        Assert.Null(f3.EnteredAtMs);
        Assert.Null(b.EnteredAtMs(3));
        Assert.Equal(800, f3.MoraleEnd);
        Assert.All(r.Formations.Where(x => x.FormationId != 3), x => Assert.Equal(setup.StartMs, x.EnteredAtMs));
        var mine = BattleCollapse.FromTactical(b, setup, new[] { (1L, 1), (2L, 2), (3L, 0) }).Single(x => x.CampaignFormation == 0);
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, EntryCostTests.HandOff(r, 3, army: 0, at, mine, startMorale: 800, beaten: 10, winnerSide: 10));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (m.Entered, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
    }

    /// <summary>
    /// MOR2-R1-02, TURN-EXACT: an aircraft formation enters at the FIRST take-off of one of its airframes — the instant the launch order
    /// is delivered (pulse p, step offset k): observed from outside the rule (the airframe's phase at each completed instant), its
    /// EnteredAtMs is null at the completed instant before and equals that instant at it; not StartMs, not one step later. The report
    /// carries the same instant. Nothing is charged (vessels and aircraft are outside the in-battle part, even when it is on).
    /// </summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(2, 3)]
    public void Aircraft_EntersAtItsFirstTakeOff_TurnExact(int pulse, int k)
    {
        var setup = AirField(7, DomainProfiles.Fighter);
        setup = setup with { Rules = setup.Rules with { EntryMoraleCost = Cost } };
        long tk = setup.StartMs + pulse * setup.Rules.PulseMs + k * setup.Rules.StepMs;
        var b = new TacticalBattle(setup, DT.Script((pulse, new OrderRequest(3, OrderKind.Patrol, k, -1, C(20, 20)))), PassiveController.Instance);
        Assert.Null(b.EnteredAtMs(3));
        long upAt = long.MinValue, before = long.MinValue; bool nullBefore = true; long? enteredAtUp = null;
        DT.RunUntil(b, () =>
        {
            if (DT.D(b, 3000).Phase == AirPhase.Based) { before = b.Scheduler.NowMs; nullBefore &= b.EnteredAtMs(3) is null; return false; }
            upAt = b.Scheduler.NowMs; enteredAtUp = b.EnteredAtMs(3);
            return true;
        });
        Assert.Equal(tk, upAt);                                                                      // the delivered step (lane N's pin)
        Assert.True(before < upAt && nullBefore);                                                   // not entered before it
        Assert.Equal(tk, enteredAtUp);                                                              // entered at it: not later
        Assert.DoesNotContain(b.EntryCharges, x => x.FormationId == 3);                            // outside the in-battle charge
        while (b.Advance())
            if (b.CurrentPulse > DT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE");
        var r = b.Result!;
        Assert.Equal(tk, r.Formations.Single(x => x.FormationId == 3).EnteredAtMs);
        Assert.Equal(0, r.Formations.Single(x => x.FormationId == 3).EntryCharged);
    }

    /// <summary>
    /// MOR2-R1-02, ONCE (DB1-R2 (a)): a second sortie is no new entry. Lane N's sorties-and-turnaround scenario (AirTests): the fighter
    /// takes off at pulse 0 step 1, turns straight back, lands, and after its turnaround takes off again at pulse 7. Observed from
    /// outside the rule (the airframe's phase at each completed instant: a take-off is a change from Based to not Based, which uses a
    /// sortie): two take-offs, and the formation's EnteredAtMs stays the FIRST take-off instant (StartMs + StepMs) after the second.
    /// </summary>
    [Fact]
    public void Aircraft_ASecondSortieIsNoNewEntry()
    {
        var a = DomainProfiles.Fighter with { TurnaroundMs = 300_000, Sorties = 2, SightMm = 100_000 };
        var setup = AirField(8, a, new[] { C(0, 39) }, new[] { C(63, 39) });
        var c0 = DT.Script(
            (0, new OrderRequest(3, OrderKind.Patrol, 1, -1, C(5, 20))),
            (0, new OrderRequest(3, OrderKind.Retreat, 2)),
            (7, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(5, 20))),
            (7, new OrderRequest(3, OrderKind.Retreat, 2)));
        var b = new TacticalBattle(setup, new CompositeController(c0, new OnlyFormations(new KnowledgeAi(), 1)), new KnowledgeAi());
        var takeOffs = new List<long>();
        bool wasBased = true;
        DT.RunUntil(b, () =>
        {
            var d = DT.D(b, 3000);
            bool based = d.Phase == AirPhase.Based;
            if (wasBased && !based) takeOffs.Add(b.Scheduler.NowMs);
            wasBased = based;
            return takeOffs.Count == 2 && b.Scheduler.NowMs > takeOffs[1];                         // one completed instant after the second
        });
        Assert.Equal(2, takeOffs.Count);
        Assert.Equal(0, DT.D(b, 3000).SortiesLeft);                                                // both sorties flown
        Assert.Equal(setup.StartMs + setup.Rules.StepMs, takeOffs[0]);
        Assert.True(takeOffs[1] >= setup.StartMs + 7 * setup.Rules.PulseMs, $"the second take-off at pulse 7 or later ({takeOffs[1]})");
        Assert.Equal(takeOffs[0], b.EnteredAtMs(3));                                               // still the first take-off
    }

    // ------------------------------------------------------------------ MOR2-R1-06 (V10): the clamp-at-0 entry reaches the settlement

    // [MOR3, D-085 DD1] lane MOR2's helper Distant(m, cost) (20 heavy infantry at m against a distant enemy) is removed: its one user
    // below is re-pinned on a fought battle, and the same unfought setup is NoFightingTests.Distant (the unfought decisive win).

    /// <summary>Lane W's collapse input of an AutoResolver formation (the War Sandbox's FromAuto form: no wounded category).</summary>
    private static CollapseInput FromAuto(FormationResult f, int campaignFormation, long moraleMax)
        => new(campaignFormation, f.Side, new[] { f.Survivors - f.RoutedEscaped, 0, f.RoutedEscaped, f.Killed, f.Surrendered }, f.MoraleEnd, moraleMax);

    /// <summary>
    /// MOR2-R1-06 (V10). The clamp at 0 of the IN-BATTLE charge, carried to the settlement: an army ruined to 30,000 (a battle from 800
    /// to 80: 800,000 − 50,000 − 720,000) fights a real battle whose formation starts at 30 with the in-battle part on (50): the entry
    /// takes all it has (charged 30 = its start), in both resolvers. The hand-off passes EntryChargedInBattle EQUAL to MoraleStart — the
    /// legal boundary, which the stand-in must accept (until round 1 nothing handed such a battle to the settlement, so refusing it,
    /// the verifier's V10, survived the whole suite). DR29 is then measured from 30 − 30 = 0, so it carries nothing; the entry term
    /// takes 50,000: clamp(30,000 − 50,000) = 0. One point more than the start is still refused.
    /// [MOR3, D-085 DD1, 2026-11-11: re-pinned on a FOUGHT battle. Lane MOR2's battle (formation 1 at 30 against a distant enemy) has no
    /// fighting — formation 1 routs (the AutoResolver: withdraws) at once and is gone before anyone strikes (MEASURED: FirstFightMs null
    /// in both resolvers) — so under DD1 nothing is charged in it and its settlement changes nothing (it is now pinned as an unfought
    /// decisive win, NoFightingTests.DD1_AnUnfoughtDecisiveWin_…). The boundary this test pins needs fighting: lane MOR2's three-formation
    /// battle with formation 1 at 30, whose first fight comes at 7,506,000 ms (the archers on formation 2, both resolvers); formation 1,
    /// gone by then, is charged at that instant all it has, 30 of 30 (PROPOSAL C1: every formation that entered, wherever it is).
    /// MEASURED moves: the setup Distant(30) → ThreeFormations with formation 1 at 30 (the knowledge-only AI with RetreatMorale 0, as in
    /// EntryCostTests' 30/40 battle); the row's AtMs StartMs → 7,506,000; and, because formation 1's side now WINS (formation 2 routs, the
    /// archers hold), the settlement takes DA1's rise too: clamp(30,000 − 50,000 − 0 + ⌊400,000 × 20 ÷ 40⌋) = 180,000 (was 0, a loss;
    /// one clamp over the three terms, by hand). The boundary itself is unchanged: EntryChargedInBattle = MoraleStart = 30 is accepted,
    /// DR29 is measured from 30 − 30 = 0 (decrease 0), the entry term is 50,000, and 31 is refused.]
    /// </summary>
    [Fact]
    public void ClampAtZero_AFormationThatEntersWithLessMoraleThanTheCost_IsSettled_ItsDR29StartsFromZero()
    {
        var three = EntryCostTests.ThreeFormations(1, Cost);                                                          // [MOR3] was Distant(30, Cost)
        var setup = three with { Formations = new[] { three.Formations[0] with { Morale = 30 }, three.Formations[1], three.Formations[2] } };
        var t = new TacticalBattle(setup, new KnowledgeAi { RetreatMorale = 0 }, new KnowledgeAi { RetreatMorale = 0 });
        var tr = TT.RunGuarded(t);
        Assert.Equal(7_506_000L, tr.FirstFightMs);                                                                    // [MOR3]
        Assert.Equal(new EntryCharge(7_506_000, 1, 30, 30), t.EntryCharges.Single(x => x.FormationId == 1));         // [MOR3] was StartMs
        var auto = new AutoBattle(setup, new AutoCoefficients()).Run();
        Assert.Equal(7_506_000L, auto.FirstFightMs);                                                                  // [MOR3]
        foreach (var (res, collapse) in new[]
                 {
                     (tr, BattleCollapse.FromTactical(t, setup, new[] { (1L, 0), (2L, 1), (3L, 2) })[0]),
                     (auto, FromAuto(auto.Formations.Single(x => x.FormationId == 1), 0, setup.Rules.MoraleMax)),
                 })
        {
            var f1 = res.Formations.Single(x => x.FormationId == 1);
            Assert.Equal(((long?)setup.StartMs, 30L, 0L), (f1.EnteredAtMs, f1.EntryCharged, f1.MoraleEnd));
            Assert.Equal(0, BattleWinner.Reported(res));                                                              // [MOR3] its side won
            long start = MoraleRig.WarStart();
            var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
            rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + Day, 80, -1, 0, 0));                        // ruined to 30,000
            var handOff = EntryCostTests.HandOff(res, 1, army: 0, start + 2 * Day, collapse, startMorale: 30, beaten: 20, winnerSide: 20);
            Assert.Equal(handOff.MoraleStart, handOff.EntryChargedInBattle);                                        // the boundary
            rig.W.ScheduleBattle(rig.S, handOff);
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, handOff with { EntryChargedInBattle = 31 }));
            MoraleRig.Run(rig, start + 2 * Day + 1);
            var m = rig.W.MoraleRows.Single(x => x.AtMs == start + 2 * Day);
            Assert.Equal((30_000L, 30L, 0L, CostPpm, 200_000L, 180_000L), (m.BeforePpm, m.EntryInBattle, m.DecreasePpm, m.EntryPpm, m.RisePpm, m.AfterPpm));   // [MOR3] after was 0
        }
    }

    // ------------------------------------------------------------------ MOR2-R1-12: counted once, in real battles with losses

    /// <summary>
    /// MOR2-R1-12. The entry cost is counted ONCE in a REAL battle in which the formation loses morale beyond the charge, in both
    /// resolvers: 30 heavy infantry at 800 against 40 (MeetingAsymmetric, the knowledge-only AI and the AutoResolver's doctrine), the
    /// in-battle part on (50). The formation ~~enters at 750~~ [MOR3, D-085 DD1: is charged to 750 at the battle's first fighting instant, PROPOSAL
    /// C1; the test passes unchanged] and ends well below it (end &lt; 700), so DR29's base matters: the settlement's
    /// decrease is exactly (800 − 50 − end) × 1,000 — not (800 − end) × 1,000 (the charge counted twice: lane MOR2's mutant E3) and not
    /// (800 − 100 − end) × 1,000 (the verifier's V14) — and the entry term is 50,000. Until round 1 only a hand-made stand-in row pinned
    /// this (the real battles used lossless archers ending at or above their start, where the base changes nothing).
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(5UL)]
    public void CountedOnce_InRealBattlesWithLosses_DR29IsMeasuredFromTheStartLessTheCharge(ulong seed)
    {
        var setup = MeetingAsymmetric(UnitProfiles.HeavyInfantry, 30, UnitProfiles.HeavyInfantry, 40, seed, new TacticalRules { EntryMoraleCost = Cost });
        setup = setup with { Formations = new[] { setup.Formations[0] with { Morale = 800 }, setup.Formations[1] } };
        var t = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var tr = TT.RunGuarded(t);
        var auto = new AutoBattle(setup, new AutoCoefficients()).Run();
        foreach (var (res, collapse) in new[]
                 {
                     (tr, BattleCollapse.FromTactical(t, setup, new[] { (1L, 0), (2L, 1) })[0]),
                     (auto, FromAuto(auto.Formations.Single(x => x.FormationId == 1), 0, setup.Rules.MoraleMax)),
                 })
        {
            var f1 = res.Formations.Single(x => x.FormationId == 1);
            Assert.Equal(((long?)setup.StartMs, Cost), (f1.EnteredAtMs, f1.EntryCharged));
            Assert.True(f1.MoraleEnd < 700, $"{res.Resolver}: the formation lost morale beyond the charge (end {f1.MoraleEnd})");
            long start = MoraleRig.WarStart(), at = start + 3 * Day;
            var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
            rig.W.ScheduleBattle(rig.S, EntryCostTests.HandOff(res, 1, army: 0, at, collapse, startMorale: 800, beaten: 40, winnerSide: 30));
            MoraleRig.Run(rig, at + 1);
            var m = rig.W.MoraleRows.Single();
            Assert.Equal(0, m.Restore);                                                                             // no medics
            Assert.Equal((Cost, CostPpm, (800 - Cost - f1.MoraleEnd) * 1_000), (m.EntryInBattle, m.EntryPpm, m.DecreasePpm));
            Assert.Equal(Math.Clamp(800_000 - CostPpm - m.DecreasePpm + m.RisePpm, 0, 1_000_000), m.AfterPpm);
        }
    }

    // ------------------------------------------------------------------ MOR2-R1-03: proposal §11.8 (3), corrected

    /// <summary>
    /// MOR2-R1-03. A consequence of PROPOSAL (a) for the mutual rout (proposal §11.8, as corrected): an army at campaign morale s × 1,000
    /// whose formation routs (all 50 entities routed, end 299) in the same instant as the enemy's, so that DA6 names it, the defender,
    /// the winner over an equal force (B = W = 2,000). Its settlement: decrease (s − 299) × 1,000, rise 200,000, entry 50,000 — after
    /// = 449,000 whatever s, so it nets a RISE from any start below 449 (+49,000 from 400), nothing at 449 and a loss above (−51,000 from
    /// 500). ~~The struck argument (3) ("DR29 takes at least (start − 300) × 1,000 and dominates the rise") holds only from 449 up.~~ Each
    /// army is first brought to s × 1,000 by a lost battle from 800 to s + 50 (800,000 − 50,000 − (750 − s) × 1,000). By hand.
    /// [MOR2, verification round 2, 2026-11-11, MOR2-R2-01: this row is ONE case of (a), not its consequence in general: a formation
    /// whose morale FELL to 299 in the battle before it routed (a fought mutual rout; the row is hand-made and records no casualty, which
    /// the settlement does not read; a real fought mutual rout is pinned by EntryCostRound2Tests.ProposalA_AFoughtMutualRout_…). Round 1
    /// took it for the general case on the premise "a formation that routed ends below 300", which is false: lane TR's inactivity rule
    /// routs a hidden, stationary formation at ANY morale (DIR5 §2.6), a start below 300 routs at once without loss, and the in-battle
    /// charge, when on, can itself rout a start in [300, 350). DR29 then takes nothing, and the defender's army nets +150,000 from any
    /// start (MEASURED in real battles: EntryCostRound2Tests.ProposalA_AnUnfoughtMutualRout_… and …_AnUnfoughtMutualWithdrawal_
    /// InTheAutoResolver_…). The struck sentence's "from 449 up" did not hold either (an unfought rout from 500 nets +150,000).]
    /// </summary>
    [Theory]
    [InlineData(400L, 49_000L)]
    [InlineData(449L, 0L)]
    [InlineData(500L, -51_000L)]
    public void ProposalA_AMutualRoutWonUnderDA6_NetsARiseFromALowStart_ByHand(long s, long net)
    {
        long start = MoraleRig.WarStart();
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + Day, s + 50, -1, 0, 0));
        var routed = new SupportBattle(0, start + 2 * Day, new CollapseInput(0, 0, new long[] { 0, 0, 50, 0, 0 }, 299, 1000), false, s, 2_000, 0)
                     { WinnerSide = 0, BeatenPersons = 2_000, WinnerSidePersons = 2_000 };
        rig.W.ScheduleBattle(rig.S, routed);
        MoraleRig.Run(rig, start + 2 * Day + 1);
        var m = rig.W.MoraleRows.Single(x => x.AtMs == start + 2 * Day);
        Assert.Equal(s * 1_000, m.BeforePpm);
        Assert.Equal(((s - 299) * 1_000, 200_000L, CostPpm), (m.DecreasePpm, m.RisePpm, m.EntryPpm));
        Assert.Equal(449_000L, m.AfterPpm);
        Assert.Equal(net, m.AfterPpm - m.BeforePpm);
    }

    // ------------------------------------------------------------------ MOR2-R1-11: the War Sandbox charges through the rules it runs

    /// <summary>
    /// MOR2-R1-11. The War Sandbox's Quick Battle runs the campaign's resolvers with the rules it is given, or the default rules
    /// (SandboxSession: <c>rules ?? new TacticalRules()</c>). So the in-battle part reaches the sandbox exactly through the rules: at the
    /// DEFAULT (0) nothing is charged; with the part switched on (50) every formation pays once ~~at its entry~~ [MOR3, D-085 DD1: at the battle's
    /// first fighting instant, PROPOSAL C1 — these Quick Battles are fought; the test passes unchanged], in the AutoResolver (AI
    /// against AI) and in the TacticalResolver (a player side). This is why the proposal's switch-on is a change of the rules' DEFAULT
    /// (proposal §11.7): a per-battle setting by the campaign hand-off would never reach a Quick Battle. Both forces at full morale.
    /// </summary>
    [Fact]
    public void WarSandbox_QuickBattle_ChargesTheInBattlePartThroughItsRules_NothingAtTheDefault()
    {
        foreach (var (rules, charged) in new[] { ((TacticalRules?)null, 0L), (new TacticalRules { EntryMoraleCost = Cost }, Cost) })
        {
            foreach (var player in new[] { false, true })
            {
                var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 11, rules);
                Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 50, Cell(8, 16), out _));
                Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 50, Cell(39, 16), out _));
                Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
                Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
                var r = player ? s.StartBattle(SandboxCommand.Player, SandboxCommand.Ai, player0: PassiveController.Instance)
                               : s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai);
                Assert.Equal(SandboxRefusal.None, r);
                Assert.Equal(player ? ResolverKind.Tactical : ResolverKind.Auto, s.ResolverUsed);
                long startMs = s.DeployedSetup!.StartMs;
                Assert.All(s.Result!.Formations, f => Assert.Equal(((long?)startMs, charged), (f.EnteredAtMs, f.EntryCharged)));
            }
        }
    }
}
