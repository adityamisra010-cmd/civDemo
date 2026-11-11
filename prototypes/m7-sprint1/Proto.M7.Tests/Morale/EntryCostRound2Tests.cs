using Proto.M7.Morale;
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
/// [MOR2, verification round 2] Tests for the round-2 findings on lane MOR2's entry cost (D-083 DB1; docs/m7-plan/morale-proposal.md
/// §11.8 and §11.2; reports/H-MOR2.md §14). Each test names the finding it pins. Every number is a TEST INPUT or the PROPOSAL
/// (settlement entry cost 50,000; in-battle part 50 when a test switches it on). Real battles go through the stand-in hand-off
/// (<see cref="EntryCostTests.HandOff"/>) and lane D's settlement, as M7.15 would.
/// </summary>
public sealed class EntryCostRound2Tests
{
    private const long Cost = 50;
    private const long CostPpm = 50_000;
    private const long Cell50 = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell50);

    // ------------------------------------------------------------------ MOR2-R2-01: what PROPOSAL (a) does to a mutual rout

    /// <summary>Two formations of 20 heavy infantry at morale <paramref name="s"/>, side 0 at column 6 and side 1 at column
    /// <paramref name="c1"/> of the open field, each side's objective at the other's position; side 1 defends (D-082 DA6); the in-battle
    /// entry charge <paramref name="cost"/>. TEST INPUT (the round-2 verifier's setup).</summary>
    private static BattleSetup Apart(long s, long c1, long cost) => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
        DefaultSides(new[] { Cell(c1, 16) }, new[] { Cell(6, 16) }),
        new[]
        {
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(6, 16)) { Morale = s },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(c1, 16)) { Morale = s },
        },
        3) { Rules = new TacticalRules { EntryMoraleCost = cost }, Defender = 1 };

    /// <summary>Lane W's collapse input of an AutoResolver formation (the War Sandbox's FromAuto form: no wounded category).</summary>
    private static CollapseInput FromAuto(FormationResult f, int campaignFormation, long moraleMax)
        => new(campaignFormation, f.Side, new[] { f.Survivors - f.RoutedEscaped, 0, f.RoutedEscaped, f.Killed, f.Surrendered }, f.MoraleEnd, moraleMax);

    /// <summary>Settles army <paramref name="army"/>'s part in the battle through the hand-off, from a campaign morale of
    /// <paramref name="s"/> × 1,000 (the hand-off's mapping): the army is first brought there from lane E's opening 800,000 by a lost
    /// battle from 800 to s + 50 (800,000 − 50,000 − (750 − s) × 1,000); at s = 800 it is there already. Equal forces (2,000 each).</summary>
    private static MoraleSettlementRow Settle(BattleResult r, long formationId, int army, CollapseInput collapse, long s)
    {
        long start = MoraleRig.WarStart(), at = start + 2 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        if (s != 800) rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(army, army, start + Day, s + 50, -1, 0, 0));
        rig.W.ScheduleBattle(rig.S, EntryCostTests.HandOff(r, formationId, army, at, collapse, startMorale: s, beaten: 2_000, winnerSide: 2_000));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single(x => x.AtMs == at);
        Assert.Equal(s * 1_000, m.BeforePpm);
        return m;
    }

    /// <summary>
    /// MOR2-R2-01. PROPOSAL (a) (proposal §11.8: DA1's rise does not depend on what the winner has left) applied to a mutual rout in
    /// which NO BLOW IS STRUCK, in the TacticalResolver: both formations stand still out of each other's sight (passive commanders) and
    /// rout at the same instant, so the battle ends BothEliminated and DA6 names the defender the winner (lane AP's Q-AP4 (a), applied
    /// pending the ruling). They rout by one of three routes, each observed from outside the settlement:
    /// - the INACTIVITY rule (lane TR, DIR5 §2.6, the default): hidden and stationary for five pulses, no exemption, both judged and
    ///   routed at the same pulse end — at ANY morale; meanwhile five quiet pulses of HoldRecovery (20 each) lifted them above their
    ///   start (320 → 420, 500 → 600, 800 → 900);
    /// - the rout THRESHOLD at the start instant: a start below 300 routs at once, without loss (250, 299);
    /// - ~~the IN-BATTLE ENTRY CHARGE itself, when the part is on (50): starts in [300, 350) are charged below 300 and rout at once
    ///   (320 → 270, 349 → 299); 500 → 450 still routs by inactivity (→ 550).~~
    /// ~~In every case DR29's decrease is 0 (the end is not below the start less the charge), so the defender's army nets DA1's rise less
    /// DB1's cost, 200,000 − 50,000 = +150,000, FROM ANY START — including 500, from which round 1's text said −51,000, and 250, below
    /// its "+149,000 bound" (the struck premise: "a formation that routed ends below 300"). The attacker's army pays the entry cost
    /// alone (−50,000).~~ Neither side held the field (HoldsField −1), so lane W's collapse deserts the share (1,000 − end) ÷ 1,000 of
    /// BOTH sides' routed men, the "winner's" included ~~: the army whose morale rises by 150,000 loses e.g. 800 of 2,000 men from 500~~.
    /// ~~This pins what the prototype does under (a); the alternatives (b), (b′) and (c′) are discussed in §11.8 (not applied).~~
    /// [MOR3, D-085 DD1, 2026-11-11: renamed from ProposalA_AnUnfoughtMutualRout_WonUnderDA6_TheDefendersArmyNetsTheRiseLessTheCost_
    /// FromAnyStart. The Director ruled: "No fighting doesn't affect morale at all then" (DD1; D-085 §3: "The measured +150,000 for the
    /// defender of an unfought mutual rout, and the attacker's −50,000, both become 0"). Every case moves, each change attributed to DD1
    /// and MEASURED: the battle had no fighting (FirstFightMs null, no attack by anyone), so the settlement changes NEITHER army's morale
    /// — the defender's s × 1,000 + 150,000 → s × 1,000 (entry 50,000 → 0, rise 200,000 → 0; the reported winner and DA6 basis are still
    /// recorded, Fought 0), the attacker's s × 1,000 − 50,000 → s × 1,000. The three cases with the in-battle part on (50) move IN THE BATTLE
    /// too, under PROPOSAL C1 (the charge falls only at a first fighting instant, and there is none): nobody is charged (EntryCharged 50
    /// → 0), so 320 and 349 no longer rout at the start (struck route) but by inactivity at 420 and 449, and 500 ends at 600, not 550 —
    /// the same battles as with the part off. Lane W's desertion of the routed men (persons, not morale) is unchanged in form: DD1 is a
    /// morale rule (morale proposal §12.3).]
    /// </summary>
    [Theory]
    [InlineData(250L, 0L, false, 250L)]
    [InlineData(299L, 0L, false, 299L)]
    [InlineData(320L, 0L, true, 420L)]
    [InlineData(500L, 0L, true, 600L)]
    [InlineData(800L, 0L, true, 900L)]
    [InlineData(320L, Cost, true, 420L)]          // [MOR3] was (320, Cost, false, 270): no charge without fighting (C1)
    [InlineData(349L, Cost, true, 449L)]          // [MOR3] was (349, Cost, false, 299)
    [InlineData(500L, Cost, true, 600L)]          // [MOR3] was (500, Cost, true, 550)
    public void DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart(long s, long cost, bool byInactivity, long end)
    {
        var setup = Apart(s, 40, cost);
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(b);
        Assert.Equal(BattleEndReason.BothEliminated, r.Reason);
        Assert.Equal((1, WinBasis.DefenderOnEqualSides, 1, -1), (r.Winner, r.WinBasis, BattleWinner.Reported(r), r.HoldsField));
        Assert.Null(r.FirstFightMs);                                                      // [MOR3] DD1-R2 (a): no attack, no fighting
        Assert.Empty(b.EntryCharges);                                                     // [MOR3] C1: nothing charged without fighting
        foreach (var f in r.Formations)                                                   // nobody struck a blow; everybody fled
            Assert.Equal((0L, 0L, 0L, 20L, (long?)setup.StartMs, 0L, end), (f.AttacksMade, f.Killed, f.DamageDealt, f.RoutedEscaped, f.EnteredAtMs, f.EntryCharged, f.MoraleEnd));
        int n = setup.Rules.InactivityPulses;
        if (byInactivity)
        {
            Assert.Equal(new[]
            {
                new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true),
                new InactivityVerdict(n - 1, 2, 1, n, InactivityExemption.None, true),
            }, b.InactivityVerdicts.ToArray());
            Assert.Equal(setup.StartMs + n * setup.Rules.PulseMs, r.EndMs);
            Assert.Equal(s + n * setup.Rules.HoldRecovery, end);                          // by hand: n quiet pulses of recovery [MOR3: no charge]
            Assert.True(end >= setup.Rules.RoutThreshold);                                  // routed above the threshold
        }
        else
        {
            Assert.Empty(b.InactivityVerdicts);
            Assert.Equal(setup.StartMs, r.EndMs);                                           // at the start instant
            Assert.Equal(s, end);                                                           // [MOR3: no charge]
            Assert.True(end < setup.Rules.RoutThreshold);
        }
        var col = BattleCollapse.FromTactical(b, setup, new[] { (1L, 0), (2L, 1) });
        var def = Settle(r, 2, 1, col[1], s);
        // [MOR3, D-085 DD1] was: Entered 1, EntryInBattle cost, EntryPpm 50,000, decrease 0, rise 200,000; after s × 1,000 + 150,000
        Assert.Equal(((byte)0, 1, (byte)1, 0L, 0L, 0L, 0L), (def.Fought, def.WinnerSide, def.Entered, def.EntryInBattle, def.EntryPpm, def.DecreasePpm, def.RisePpm));
        Assert.Equal(s * 1_000, def.AfterPpm);                                             // unchanged, whatever the start
        var att = Settle(r, 1, 0, col[0], s);
        // [MOR3, D-085 DD1] was: entry 50,000; after s × 1,000 − 50,000
        Assert.Equal(((byte)0, 0L, 0L, 0L, s * 1_000), (att.Fought, att.EntryPpm, att.DecreasePpm, att.RisePpm, att.AfterPpm));
        var split = BattleCollapse.Split(2_000, col[1], sideHeldField: r.HoldsField == 1);
        Assert.Equal((2_000 * (1_000 - end) / 1_000, 2_000 * end / 1_000), (split.Deserted, split.RoutedReturned));
    }

    /// <summary>
    /// MOR2-R2-01, the same in the AutoResolver, where DA6 is applied as D-082 DA6-R records it (not pending): the two formations stand
    /// symmetrically (side 1 at the mirror column of side 0's) and start below the AutoResolver's withdrawal line (450) ~~, or are charged
    /// below it by the in-battle part (480 → 430)~~; each withdraws in good order at its first decision, both leave the field at the same
    /// step, and the battle ends BothEliminated with the defender the winner. No blow is struck (no damage, no casualty) and nobody
    /// routed: all 20 of each escaped in good order. ~~The defender's army nets +150,000 here too (decrease 0, rise 200,000, entry 50,000).
    /// So the lift does not depend on lane AP's pending Q-AP4, and an alternative that withholds the rise only from a winner with nobody
    /// left in good order ((b′), proposal §11.8) would not remove it (these men withdrew in good order).~~
    /// [MOR3, D-085 DD1, 2026-11-11: renamed from ProposalA_AnUnfoughtMutualWithdrawal_InTheAutoResolver_WonUnderDA6_NetsTheRiseLessTheCost.
    /// MEASURED moves, each attributed to DD1: the defender's army s × 1,000 + 150,000 → s × 1,000 (no entry cost, no rise: Fought 0),
    /// and — not asserted before — the attacker's s × 1,000 − 50,000 → s × 1,000. The case (480, in-battle part 50) is removed from this
    /// theory: under C1 the charge falls only at a first fighting instant, so formations at 480 are not charged below the line, do not
    /// withdraw, and FIGHT (MEASURED: a fought mutual rout, first fighting step 131) — it is pinned as such in
    /// NoFightingTests.C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_…. In its place, the cases 250 and 449 with the part on:
    /// the same unfought battles, nothing charged.]
    /// </summary>
    [Theory]
    [InlineData(250L, 0L)]
    [InlineData(449L, 0L)]
    [InlineData(250L, Cost)]                       // [MOR3] replaces (480, Cost); see the note above
    [InlineData(449L, Cost)]                       // [MOR3]
    public void DD1_AnUnfoughtMutualWithdrawal_InTheAutoResolver_WonUnderDA6_ChangesNeitherArmysMorale(long s, long cost)
    {
        var setup = Apart(s, MirrorColumn(6), cost);
        var auto = new AutoBattle(setup, new AutoCoefficients());
        var a = auto.Run();
        Assert.Equal(BattleEndReason.BothEliminated, a.Reason);
        Assert.Equal((1, WinBasis.DefenderOnEqualSides, 1, -1), (a.Winner, a.WinBasis, BattleWinner.Reported(a), a.HoldsField));
        Assert.Null(a.FirstFightMs);                                                      // [MOR3] no fighting
        Assert.Empty(auto.EntryCharges);                                                  // [MOR3] nothing charged
        foreach (var f in a.Formations)                                                   // withdrawn in good order; no blow struck
            Assert.Equal((0L, 0L, 20L, 0L, 0L, s), (f.Killed, f.DamageDealt, f.Escaped, f.RoutedEscaped, f.EntryCharged, f.MoraleEnd));
        var def = Settle(a, 2, 1, FromAuto(a.Formations.Single(x => x.FormationId == 2), 1, setup.Rules.MoraleMax), s);
        Assert.Equal(((byte)0, 0L, 0L, 0L, 0L, s * 1_000), (def.Fought, def.EntryPpm, def.EntryInBattle, def.DecreasePpm, def.RisePpm, def.AfterPpm));
        var att = Settle(a, 1, 0, FromAuto(a.Formations.Single(x => x.FormationId == 1), 0, setup.Rules.MoraleMax), s);   // [MOR3] new
        Assert.Equal(((byte)0, 0L, s * 1_000), (att.Fought, att.EntryPpm, att.AfterPpm));
    }

    /// <summary>
    /// MOR2-R2-01, the case round 1's struck argument (3) does describe: a FOUGHT mutual rout. The same two formations under the
    /// knowledge-only AI close and fight (3 of 20 killed on each side, damage dealt by both) until both rout at one instant at 200:
    /// BothEliminated, the defender the winner. DR29 now takes (500 − 200) × 1,000 = 300,000 and outweighs the rise: the defender's army
    /// nets 200,000 − 50,000 − 300,000 = −150,000 (500,000 → 350,000). MEASURED values, exact. Under (a) a mutual rout lifts the winner
    /// only when its formations lost less morale in the battle than the rise less the cost.
    /// </summary>
    [Fact]
    public void ProposalA_AFoughtMutualRout_WonUnderDA6_DR29OutweighsTheRise()
    {
        var setup = Apart(500, 40, 0);
        var b = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var r = TT.RunGuarded(b);
        Assert.Equal(BattleEndReason.BothEliminated, r.Reason);
        Assert.Equal((1, WinBasis.DefenderOnEqualSides, 1), (r.Winner, r.WinBasis, BattleWinner.Reported(r)));
        foreach (var f in r.Formations)
        {
            Assert.Equal((3L, 17L, 200L), (f.Killed, f.RoutedEscaped, f.MoraleEnd));
            Assert.True(f.DamageDealt > 0, $"formation {f.FormationId} struck blows ({f.DamageDealt})");
        }
        Assert.Empty(b.InactivityVerdicts);
        var col = BattleCollapse.FromTactical(b, setup, new[] { (1L, 0), (2L, 1) });
        var def = Settle(r, 2, 1, col[1], 500);
        Assert.Equal((CostPpm, 300_000L, 200_000L, 350_000L), (def.EntryPpm, def.DecreasePpm, def.RisePpm, def.AfterPpm));
    }

    // ------------------------------------------------------------------ MOR2-R2-02: several domain formations, each its own entry

    /// <summary>The first completed instant at which an airframe of each aircraft formation is not at its base, observed from outside the
    /// rule (each domain entity's phase at every completed instant), in formation-id order; absent = never left its base.</summary>
    private static SortedDictionary<long, long> ObserveTakeOffs(TacticalBattle b, out BattleResult r, Action? alsoAtEachInstant = null)
    {
        var up = new SortedDictionary<long, long>();
        r = DT.RunChecked(b, onInstant: () =>
        {
            foreach (var d in b.DomainEntities())
                if (d.Domain == Domain.Air && d.Phase != AirPhase.Based && !up.ContainsKey(d.FormationId)) up[d.FormationId] = b.Scheduler.NowMs;
            alsoAtEachInstant?.Invoke();
        });
        return up;
    }

    /// <summary>
    /// MOR2-R2-02. Lane N's amphibious battle has SEVERAL domain formations: two vessel formations (10 warships, 11 transports) and
    /// three aircraft formations (13 strike, 14 fighter, side 0; 24 fighter, side 1), beside the riflemen 12 that land from the
    /// transports. READING MOR2-R2 (a) (proposal §11.2): every vessel formation enters at the battle's start, every aircraft formation at
    /// the first take-off of one of ITS OWN airframes, the landing formation at its first entity put ashore. Observed from outside the
    /// rule at every completed instant: 13 and 14 take off at the start's step, 24 a pulse later, 12 lands later still. Each formation's
    /// EnteredAtMs (report and accessor) equals its own observed instant. Until round 2 only the lowest-id domain formation of a battle
    /// was pinned, so a take-off recorded on the first domain formation instead of the airframe's own (the verifier's V2-06) and a start
    /// that entered only the first domain formation (V2-13) survived the whole suite (MEASURED: 1488 of 1488 each, H-MOR2 §14).
    /// </summary>
    [Fact]
    public void SeveralDomainFormations_EachAircraftFormationEntersAtItsOwnFirstTakeOff_EveryVesselFormationAtTheStart()
    {
        var setup = MultiDomainScenarios.Amphibious(42);
        var b = new TacticalBattle(setup, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence());
        long ashore = long.MinValue;
        var up = ObserveTakeOffs(b, out var r, () =>
        {
            if (ashore == long.MinValue && b.Entities().Any(e => e.FormationId == 12 && e.State == EntityState.Active)) ashore = b.Scheduler.NowMs;
        });
        var air = r.Formations.Where(f => f.Domain == Domain.Air).Select(f => f.FormationId).ToArray();
        var sea = r.Formations.Where(f => f.Domain == Domain.Naval).Select(f => f.FormationId).ToArray();
        Assert.Equal(new long[] { 13, 14, 24 }, air);                                       // the scenario: several of each
        Assert.Equal(new long[] { 10, 11 }, sea);
        Assert.Equal(new long[] { 13, 14, 24 }, up.Keys.ToArray());                         // every aircraft formation took off
        Assert.Equal(setup.StartMs, up[13]);
        Assert.Equal(setup.StartMs, up[14]);
        Assert.True(up[24] > up[13], $"formation 24 first took off later ({up[24]}) than 13 ({up[13]})");
        Assert.True(ashore > up[24], $"the riflemen landed ({ashore}) after the last first take-off ({up[24]})");
        foreach (var f in r.Formations)
        {
            long? expected = f.Domain switch
            {
                Domain.Air => up[f.FormationId],
                Domain.Naval => setup.StartMs,
                _ => f.FormationId == 12 ? ashore : setup.StartMs,
            };
            Assert.True(expected == f.EnteredAtMs, $"formation {f.FormationId} ({f.Domain}): EnteredAtMs {f.EnteredAtMs?.ToString() ?? "null"}, expected {expected}");
            Assert.Equal(f.EnteredAtMs, b.EnteredAtMs(f.FormationId));
            Assert.Equal(0, f.EntryCharged);
        }
    }

    /// <summary>
    /// MOR2-R2-02, TURN-EXACT with two aircraft formations of one side (lane N's ridge field): fighter formation 3 is kept at its base,
    /// fighter formation 4 is launched by an order delivered at pulse 0, step 5. Observed from outside the rule: formation 4's airframe
    /// leaves its base at exactly StartMs + 5 StepMs; formation 3's never does. Formation 4 entered at that instant — null at the
    /// completed instant before it, that instant at it — and formation 3 never entered (null). The entry is recorded on the formation
    /// whose airframe took off, not on the first aircraft formation of the battle (the verifier's V2-06 left formation 4 null).
    /// </summary>
    [Fact]
    public void TwoAircraftFormationsOfOneSide_OnlyTheOneThatTakesOffEnters_AtItsOwnTakeOff_TurnExact()
    {
        var g = new TerrainGrid(64, 40, Cell50, TerrainClass.Open);
        for (int y = 5; y < 35; y++) g.Set(30, y, TerrainClass.Hills, 40_000);
        var setup = DT.OnGrid(g, 7, null, null,
            new FormationSpec(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)),
            MultiDomainScenarios.Flight(3, 0, DomainProfiles.Fighter, 1),
            MultiDomainScenarios.Flight(4, 0, DomainProfiles.Fighter, 1));
        long tk = setup.StartMs + 5 * setup.Rules.StepMs;
        var b = new TacticalBattle(setup, DT.Script((0, new OrderRequest(4, OrderKind.Patrol, 5, -1, C(20, 20)))), PassiveController.Instance);
        Assert.Null(b.EnteredAtMs(3));
        Assert.Null(b.EnteredAtMs(4));
        long before = long.MinValue, upAt = long.MinValue;
        bool nullBefore = true;
        long? enteredAtUp = null;
        DT.RunUntil(b, () =>
        {
            if (DT.D(b, 4000).Phase == AirPhase.Based) { before = b.Scheduler.NowMs; nullBefore &= b.EnteredAtMs(4) is null; return false; }
            upAt = b.Scheduler.NowMs; enteredAtUp = b.EnteredAtMs(4);
            return true;
        });
        Assert.Equal(tk, upAt);                                                              // the delivered step
        Assert.True(before < upAt && nullBefore);                                            // not entered before it
        Assert.Equal(tk, enteredAtUp);                                                       // entered at it, not later
        bool threeUp = false;
        while (b.Advance())
        {
            if (DT.D(b, 3000).Phase != AirPhase.Based) threeUp = true;
            if (b.CurrentPulse > DT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE");
        }
        Assert.False(threeUp);                                                               // formation 3 never left its base
        var r = b.Result!;
        Assert.Equal(((long?)null, (long?)tk), (r.Formations.Single(x => x.FormationId == 3).EnteredAtMs, r.Formations.Single(x => x.FormationId == 4).EnteredAtMs));
        Assert.Null(b.EnteredAtMs(3));
    }
}
