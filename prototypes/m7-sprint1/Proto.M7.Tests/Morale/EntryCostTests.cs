using System.Security.Cryptography;
using Proto.M7.Morale;
using Proto.M7.Spatial;
using Proto.M7.Support;
using Proto.M7.Tactical;
using Proto.M7.Tests.LaneD;
using Proto.M7.Tests.MultiDomain;
using Proto.M7.Tests.Tactical;
using Proto.M7.War.Outcomes;
using static Proto.M7.Tactical.TacticalScenarios;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Morale;

/// <summary>
/// [MOR2] D-083 DB1 ("Entering a battle costs morale, with or without casualties"; docs/m7-plan/morale-proposal.md §11). Two parts:
/// the SETTLEMENT term (every army whose formation entered the battle pays MoraleRates.EntryCostPpm = 50,000, PROPOSAL, its own term
/// in the one clamp; on by default) and the IN-BATTLE charge (TacticalRules.EntryMoraleCost, battle scale, 0 by default; PROPOSAL
/// 50 when switched on), charged once per land formation ~~at the instant it is placed on the field~~ [MOR3, D-085 DD1: at the battle's first
/// fighting instant, or at its placement when that comes later; never in a battle without fighting — PROPOSAL C1, morale proposal §12.4], through one function both
/// resolvers call (EntryMorale). DB1-R1 (a), DB1-R2 (a), DB1-R3 (a) are applied. Every number below is TEST INPUT or the PROPOSAL.
/// </summary>
public sealed class EntryCostTests
{
    private const long Cost = 50;                 // the in-battle PROPOSAL (battle scale), switched on by the tests that name it
    private const long CostPpm = 50_000;          // MoraleRates.EntryCostPpm, the settlement PROPOSAL (on by default)

    // ------------------------------------------------------------------ the equations, by hand

    [Fact]
    public void Equations_ByHand_TheEntryTermTheOneClampAndTheBattleScaleImage()
    {
        var r = MoraleRates.Proposal();
        Assert.Equal(CostPpm, r.EntryCostPpm);
        Assert.Equal(CostPpm, CampaignMorale.EntryPpm(true, r));
        Assert.Equal(0, CampaignMorale.EntryPpm(false, r));                                   // never reached the field: nothing
        Assert.Equal(0, CampaignMorale.EntryPpm(true, r with { EntryCostPpm = 0 }));
        // one clamp over the three terms: clamp(before − entry − decrease + rise, 0, 1,000,000)
        Assert.Equal(950_000, CampaignMorale.After(800_000, 0, 200_000, CostPpm));               // a lossless win over an equal force
        Assert.Equal(750_000, CampaignMorale.After(800_000, 0, 0, CostPpm));                     // a lossless loss
        Assert.Equal(650_000, CampaignMorale.After(800_000, 100_000, 0, CostPpm));               // a loss of 100 on the battle scale
        Assert.Equal(0, CampaignMorale.After(30_000, 0, 0, CostPpm));                            // the clamp at 0: not −20,000
        Assert.Equal(180_000, CampaignMorale.After(30_000, 0, 200_000, CostPpm));                // one clamp: not 200,000 (clamp first)
        Assert.Equal(1_000_000, CampaignMorale.After(900_000, 0, 200_000, CostPpm));             // the top: 1,050,000 clamped
        Assert.Equal(CampaignMorale.After(800_000, 100_000, 200_000), CampaignMorale.After(800_000, 100_000, 200_000, 0));   // entry 0 = lane MOR's rule
        // the battle-scale image the hand-off sets as TacticalRules.EntryMoraleCost: floor(costPpm × MoraleMax ÷ 1,000,000)
        Assert.Equal(50, CampaignMorale.EntryBattleScale(CostPpm, 1000));
        Assert.Equal(50, CampaignMorale.EntryBattleScale(50_999, 1000));
        Assert.Equal(37, CampaignMorale.EntryBattleScale(CostPpm, 750));
        Assert.Equal(0, CampaignMorale.EntryBattleScale(0, 1000));
        // the in-battle charge: the cost, or all the formation has
        Assert.Equal(50, EntryMorale.Charge(50, 800));
        Assert.Equal(30, EntryMorale.Charge(50, 30));
        Assert.Equal(0, EntryMorale.Charge(0, 800));
        Assert.Equal(0, EntryMorale.Charge(50, 0));
        // refusals
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMorale.After(800_000, 0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => (r with { EntryCostPpm = -1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (r with { EntryCostPpm = 1_000_001 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => EntryMorale.Charge(-1, 800));
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMorale.EntryBattleScale(-1, 1000));
        Assert.Throws<ArgumentException>(() => new TacticalRules { EntryMoraleCost = -1 }.Validate());
        Assert.Throws<ArgumentException>(() => new TacticalRules { EntryMoraleCost = 1001 }.Validate());
        new TacticalRules { EntryMoraleCost = 1000 }.Validate();
    }

    /// <summary>DB1-R2 (a): ONCE per formation per battle. ~~The first call records the entry instant and charges; every later call is a
    /// no-op;~~ at cost 0 the instant is recorded and nothing else happens (no row: the identity); a formation with less morale than
    /// the cost loses all of it (the clamp at 0).
    /// [MOR3, D-085 DD1, 2026-11-11: the function's contract changed (PROPOSAL C1, morale proposal §12.4) — Enter RECORDS the entry and
    /// charges only when the battle's fighting has begun (a later arrival); ChargeOnFighting charges an entered, uncharged formation at
    /// the battle's first fighting instant; a battle without fighting charges nothing. The properties this test pins are kept and pinned
    /// on the new contract: once per formation per battle (a second charge, a re-entry: nothing), the clamp at 0, nothing at cost 0; and
    /// two new: nothing is charged at an entry before the fighting, and a formation that never entered is not charged.]</summary>
    [Fact]
    public void EntryMorale_Enter_ChargesOncePerFormation_ByHand()
    {
        var log = new List<EntryCharge>();
        long? entered = null, at = null; long morale = 800, charged = 0;
        // entered before any fighting: the entry is recorded, nothing is charged (the battle may turn out to have no fighting, DD1)
        Assert.True(EntryMorale.Enter(50, 1_000, 7, false, ref entered, ref morale, ref charged, ref at, log));
        Assert.Equal((1_000L, 800L, 0L, (long?)null), (entered!.Value, morale, charged, at));
        Assert.Empty(log);
        // the battle's first fighting instant: charged once
        Assert.True(EntryMorale.ChargeOnFighting(50, 4_000, 7, entered, ref morale, ref charged, ref at, log));
        Assert.Equal((750L, 50L, (long?)4_000), (morale, charged, at));
        Assert.False(EntryMorale.ChargeOnFighting(50, 6_000, 7, entered, ref morale, ref charged, ref at, log));    // once
        Assert.False(EntryMorale.Enter(50, 5_000, 7, true, ref entered, ref morale, ref charged, ref at, log));       // re-entry: nothing
        Assert.Equal((1_000L, 750L, 50L, (long?)4_000), (entered!.Value, morale, charged, at));
        Assert.Equal(new[] { new EntryCharge(4_000, 7, 800, 50) }, log.ToArray());
        // entered after the fighting began (a later arrival): charged at its own entry
        long? eL = null, aL = null; long mL = 600, cL = 0;
        Assert.True(EntryMorale.Enter(50, 9_000, 10, true, ref eL, ref mL, ref cL, ref aL, log));
        Assert.Equal((9_000L, 550L, 50L, (long?)9_000), (eL!.Value, mL, cL, aL));
        Assert.Equal(new EntryCharge(9_000, 10, 600, 50), log[^1]);
        // never entered: the first fighting instant charges nothing
        long? eN = null, aN = null; long mN = 800, cN = 0;
        Assert.False(EntryMorale.ChargeOnFighting(50, 4_000, 11, eN, ref mN, ref cN, ref aN, log));
        Assert.Equal((800L, 0L, (long?)null), (mN, cN, aN));
        // cost 0: the instant only — nothing charged, logged or marked, before or after the fighting began
        long? e0 = null, a0 = null; long m0 = 800, c0 = 0;
        Assert.True(EntryMorale.Enter(0, 2_000, 8, true, ref e0, ref m0, ref c0, ref a0, log));
        Assert.False(EntryMorale.ChargeOnFighting(0, 4_000, 8, e0, ref m0, ref c0, ref a0, log));
        Assert.Equal((2_000L, 800L, 0L, (long?)null), (e0!.Value, m0, c0, a0));
        Assert.Equal(2, log.Count);
        // the clamp at 0
        long? e1 = null, a1 = null; long m1 = 30, c1 = 0;
        Assert.True(EntryMorale.Enter(50, 3_000, 9, false, ref e1, ref m1, ref c1, ref a1, log));
        Assert.True(EntryMorale.ChargeOnFighting(50, 4_000, 9, e1, ref m1, ref c1, ref a1, log));
        Assert.Equal((0L, 30L), (m1, c1));
        Assert.Equal(new EntryCharge(4_000, 9, 30, 30), log[^1]);
        long? e2 = null, a2 = null; long m2 = 800, c2 = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => EntryMorale.Enter(-1, 0, 1, false, ref e2, ref m2, ref c2, ref a2, log));
        Assert.Throws<ArgumentOutOfRangeException>(() => EntryMorale.ChargeOnFighting(-1, 0, 1, 0, ref m2, ref c2, ref a2, log));
    }

    // ------------------------------------------------------------------ the settlement (lane D's stand-in hand-off)

    /// <summary>DB1-R3 (a): a battle won WITHOUT LOSS changes the winner's campaign morale by DA1's rise less the entry cost, exactly:
    /// floor(400,000 × B ÷ (B + W)) − 50,000. The net is negative after beating a small force: with B = 1,000 it is 0 exactly at
    /// W = 7,000 (the rise is 50,000), −7 at W = 7,001 and +6 at W = 6,999 — so a lossless win over a force smaller than a seventh of
    /// the winning side nets a loss (morale proposal §11.4). The lossless battle: the formation ends where it started (800) with no
    /// casualty (all fit). By hand.</summary>
    [Theory]
    [InlineData(2_000L, 2_000L, 200_000L)]       // an equal force: +150,000
    [InlineData(1_000L, 2_000L, 133_333L)]       // half one's own: +83,333
    [InlineData(1_000L, 7_000L, 50_000L)]        // a seventh: exactly 0
    [InlineData(1_000L, 7_001L, 49_993L)]        // just smaller than a seventh: −7
    [InlineData(1_000L, 6_999L, 50_006L)]        // just larger: +6
    [InlineData(1_000L, 20_000L, 19_047L)]       // a twentieth: −30,953
    public void DB1_ALosslessWin_NetsTheRiseLessTheCost_Exactly(long beaten, long winnerSide, long rise)
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, Lossless(0, 0, at, winner: 0, beaten, winnerSide));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal((0L, rise, CostPpm), (m.DecreasePpm, m.RisePpm, m.EntryPpm));
        Assert.Equal(800_000 + rise - CostPpm, m.AfterPpm);
        Assert.Equal(800_000 + rise - CostPpm, rig.W.Economy.MoraleOf(0));
        Assert.Equal(Math.Sign(rise - CostPpm), Math.Sign(rig.W.Economy.MoraleOf(0) - 800_000));
    }

    /// <summary>DB1-R3 (a): a battle LOST without loss costs exactly the entry cost (800,000 → 750,000), and so does a lossless battle
    /// that names no winner; the winner of that battle, lossless too, nets its rise less the cost.</summary>
    [Fact]
    public void DB1_ALosslessLoss_CostsExactlyTheCost()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(3), start);
        rig.W.ScheduleBattle(rig.S, Lossless(0, 0, at, winner: 0, 2_000, 2_000));      // the winner
        rig.W.ScheduleBattle(rig.S, Lossless(1, 1, at, winner: 0, 2_000, 2_000));      // the loser
        rig.W.ScheduleBattle(rig.S, Lossless(2, 1, at, winner: -1, 0, 0));             // no winner reported
        MoraleRig.Run(rig, at + 1);
        Assert.Equal(new[] { 950_000L, 750_000L, 750_000L }, Enumerable.Range(0, 3).Select(a => rig.W.Economy.MoraleOf(a)).ToArray());
        Assert.All(rig.W.MoraleRows, x => Assert.Equal((0L, CostPpm, (byte)1), (x.DecreasePpm, x.EntryPpm, x.Entered)));
        Assert.Equal(new[] { 200_000L, 0L, 0L }, rig.W.MoraleRows.Select(x => x.RisePpm).ToArray());
    }

    /// <summary>
    /// D-083 §4: "the in-battle recovery can make it up before the battle ends. Lane D's net-loss settlement (start − end − restore,
    /// floored at 0) would then carry none of it". A REAL TacticalResolver battle: 60 archers (morale 800) hold and shoot ten heavy
    /// infantry to a rout without a loss (lane D's D-R2-01 scenario); HoldRecovery (+20 per casualty-free pulse) lifts them, so they end
    /// AT OR ABOVE their start — with the in-battle charge ON (they ~~enter at 750~~ [MOR3, D-085 DD1: are charged 50 at the battle's first
    /// fighting instant, their first volley, PROPOSAL C1; the test passes unchanged]) the recovery made up the whole charge. The settlement,
    /// fed by the battle report (FormationResult.EnteredAtMs, EntryCharged), still takes exactly the entry cost: 800,000 − 50,000 + DA1's
    /// rise, with the charge on and with it off. Lane D's net-loss term alone would give 800,000 + rise (the cost lost). TEST INPUT.
    /// </summary>
    [Theory]
    [InlineData(1UL, 10, 0L)]
    [InlineData(1UL, 10, Cost)]
    [InlineData(7UL, 20, 0L)]
    [InlineData(7UL, 20, Cost)]
    public void DB1_TheCostReachesTheCampaignExactly_WhenTheInBattleRecoveryMakesUpTheStartLoss(ulong seed, int enemies, long inBattle)
    {
        var setup = MeetingAsymmetric(UnitProfiles.Archers, 60, UnitProfiles.HeavyInfantry, enemies, seed);
        setup = setup with { Formations = new[] { setup.Formations[0] with { Morale = 800 }, setup.Formations[1] }, Rules = setup.Rules with { EntryMoraleCost = inBattle } };
        var battle = new TacticalBattle(setup, PassiveController.Instance, new KnowledgeAi());
        var res = TT.RunGuarded(battle);
        var archers = res.Formations.Single(x => x.FormationId == 1);
        Assert.Equal(0, archers.Killed);                                                    // lossless ...
        Assert.True(res.Formations.Single(x => x.FormationId == 2).Killed > 0);           // ... and they fought
        Assert.Equal((setup.StartMs, inBattle), (archers.EnteredAtMs!.Value, archers.EntryCharged));
        Assert.True(archers.MoraleEnd >= 800, $"the in-battle recovery made up the start loss: end {archers.MoraleEnd}");
        Assert.Equal(0, BattleWinner.Reported(res));                                       // the infantry broke
        Assert.Equal(0, CampaignMorale.CombatDecreasePpm(800 - inBattle, archers.MoraleEnd, 0, 1000));   // nothing carried by DR29
        var mine = BattleCollapse.FromTactical(battle, setup, new[] { (1L, 0), (2L, 1) })[0];
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, HandOff(res, 1, army: 0, at, mine, startMorale: 800, beaten: enemies, winnerSide: 60));
        MoraleRig.Run(rig, at + 1);
        long rise = 400_000L * enemies / (enemies + 60);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal((inBattle, CostPpm, 0L, rise), (m.EntryInBattle, m.EntryPpm, m.DecreasePpm, m.RisePpm));
        Assert.Equal(800_000 - CostPpm + rise, rig.W.Economy.MoraleOf(0));                  // the cost reached the campaign exactly
        Assert.NotEqual(800_000 + rise, rig.W.Economy.MoraleOf(0));                        // lane D's net loss alone would have lost it
    }

    /// <summary>DB1-R1 (a): a formation that never reaches the field pays nothing: its army's settlement records Entered 0 and changes
    /// nothing — no entry cost, no DR29 decrease, and no DA1 rise although its side won (it did not take part: DA1-R (a)). The stand-in
    /// refuses a "never entered" battle that carries ~~a loss, a casualty or~~ an in-battle charge.
    /// [MOR2, verification round 1, MOR2-R1-01, MOR2-R1-04] A formation that never reached the field CAN lose men and morale (troops
    /// killed aboard their transport, lane N; the real battle is EntryCostRound1Tests.NeverEntered_TroopsKilledAboard…), so the stand-in
    /// now ACCEPTS a never-entered battle with casualties and a loss (armies 2 and 3): DR29's decrease of what the battle cost it applies
    /// (800 → 600: 200,000), no entry cost, and — READING MOR2-R1 (a), "took part" = entered — no rise although army 2's side won; lane
    /// W's collapse moves its persons as for any formation (dead 400, wounded 200, captured 80, deserted 48 of 2,000: the routed 120 ×
    /// (1,000 − 600) ÷ 1,000). Army 4 is the same battle ENTERED: entry 50,000 and the rise 200,000 too (750,000), so the reading is what
    /// separates armies 2 and 4. The stand-in still refuses an in-battle charge for a formation that never entered (it is taken only at
    /// an entry), and a charge above the start or below 0. By hand.</summary>
    [Fact]
    public void DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(5), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.NeverEntered(0, 0, at, winner: 0, 2_000, 2_000));    // its side won
        rig.W.ScheduleBattle(rig.S, MoraleRig.NeverEntered(1, 1, at, winner: 0, 2_000, 2_000));    // its side lost
        var engaged = MoraleRig.NeverEntered(2, 0, at, winner: 0, 2_000, 2_000) with { Collapse = MoraleRig.Collapse(0, 600) };   // [MOR2 r1]
        rig.W.ScheduleBattle(rig.S, engaged);                                                                                     // its side won
        rig.W.ScheduleBattle(rig.S, MoraleRig.NeverEntered(3, 1, at, winner: 0, 2_000, 2_000) with { Collapse = MoraleRig.Collapse(1, 600) });   // lost
        rig.W.ScheduleBattle(rig.S, engaged with { ArmyId = 4, Entered = true });                                                // the same, entered
        MoraleRig.Run(rig, at + 1);
        Assert.Equal(new[] { 800_000L, 800_000L, 600_000L, 600_000L, 750_000L }, Enumerable.Range(0, 5).Select(a => rig.W.Economy.MoraleOf(a)).ToArray());
        var rows = rig.W.MoraleRows.OrderBy(x => x.ArmyId).ToArray();
        Assert.All(rows.Take(2), x => Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (x.Entered, x.EntryPpm, x.DecreasePpm, x.RisePpm, x.AfterPpm)));
        Assert.All(rows.Skip(2).Take(2), x => Assert.Equal(((byte)0, 0L, 200_000L, 0L, 600_000L), (x.Entered, x.EntryPpm, x.DecreasePpm, x.RisePpm, x.AfterPpm)));
        Assert.Equal(((byte)1, 50_000L, 200_000L, 200_000L, 750_000L), (rows[4].Entered, rows[4].EntryPpm, rows[4].DecreasePpm, rows[4].RisePpm, rows[4].AfterPpm));
        var persons = rig.W.BattleRows.OrderBy(x => x.ArmyId).ToArray();
        Assert.All(persons.Take(2), x => Assert.Equal((0L, 0L, 0L, 0L), (x.Dead, x.Wounded, x.Captured, x.Deserted)));
        Assert.All(persons.Skip(2), x => Assert.Equal((400L, 200L, 80L, 48L), (x.Dead, x.Wounded, x.Captured, x.Deserted)));
        var bad = MoraleRig.NeverEntered(0, 0, at, 0, 2_000, 2_000);
        // ~~casualties and a loss refused~~ [MOR2 r1: accepted, armies 2 and 3 above]
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, bad with { EntryChargedInBattle = 50 }));                         // a charge
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, engaged with { EntryChargedInBattle = 1 }));                      // [MOR2 r1] one point
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, Lossless(0, 0, at, 0, 2_000, 2_000) with { EntryChargedInBattle = 801 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, Lossless(0, 0, at, 0, 2_000, 2_000) with { EntryChargedInBattle = -1 }));
    }

    /// <summary>The clamp at 0, one clamp over the three terms. Army 0 is first ruined (a battle from 800 to 80 on the battle scale:
    /// 800,000 − 50,000 − 720,000 = 30,000), then fights a lossless battle it loses: 30,000 − 50,000 → 0, not −20,000. Army 1, ruined the
    /// same way, then WINS a lossless battle over an equal force: 30,000 − 50,000 + 200,000 = 180,000 (a rule that clamped the cost first
    /// would give 200,000).</summary>
    [Fact]
    public void DB1_TheClampAtZero_OneClampOverTheThreeTerms()
    {
        long start = MoraleRig.WarStart();
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + 1 * Day, 80, -1, 0, 0));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, start + 1 * Day, 80, -1, 0, 0));
        rig.W.ScheduleBattle(rig.S, Lossless(0, 0, start + 2 * Day, winner: 1, 2_000, 2_000));
        rig.W.ScheduleBattle(rig.S, Lossless(1, 1, start + 2 * Day, winner: 1, 2_000, 2_000));
        MoraleRig.Run(rig, start + 2 * Day + 1);
        var rows = rig.W.MoraleRows;
        Assert.Equal(new[] { 30_000L, 30_000L }, rows.Where(x => x.AtMs == start + Day).Select(x => x.AfterPpm).ToArray());
        Assert.Equal((0L, 180_000L), (rig.W.Economy.MoraleOf(0), rig.W.Economy.MoraleOf(1)));
    }

    /// <summary>TIE-DENSE: five armies' battles end at ONE instant — an entered winner, an entered loser, a winner's army that never
    /// reached the field, a loser whose battle charged 50 at the entry, a loser's army that never reached the field. In all 120
    /// scheduling orders they settle in army-id order (the event key's SourceId), each with its own exact result, and the state hash
    /// is the same. By hand: 850,000 (800,000 − 50,000 − 100,000 + 200,000), 250,000 (− 50,000 − 500,000), 800,000 (nothing), 700,000
    /// (− 50,000 − (750 − 700) × 1,000), 800,000.</summary>
    [Fact]
    public void DB1_TieDense_ManyArmiesSettleAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var battles = TieBattles(at);
        string? hash = null;
        int orders = 0;
        foreach (var order in Permutations(battles.Length))
        {
            var rig = MoraleRig.Build(MoraleRig.Armies(5), start);
            foreach (int i in order) rig.W.ScheduleBattle(rig.S, battles[i]);
            MoraleRig.Run(rig, at + 1);
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, rig.W.MoraleRows.Select(x => x.ArmyId).ToArray());
            Assert.Equal(new[] { 850_000L, 250_000L, 800_000L, 700_000L, 800_000L }, Enumerable.Range(0, 5).Select(a => rig.W.Economy.MoraleOf(a)).ToArray());
            Assert.Equal(new[] { CostPpm, CostPpm, 0L, CostPpm, 0L }, rig.W.MoraleRows.Select(x => x.EntryPpm).ToArray());
            hash ??= rig.W.StateHashHex();
            Assert.Equal(hash, rig.W.StateHashHex());
            orders++;
        }
        Assert.Equal(120, orders);
    }

    /// <summary>POPULATED-TABLE TEST of the version-2 settlement rows of the entry-cost cases (entered and never-entered armies, an
    /// in-battle charge, a rise, a clamp at 0): exact ExpectedLength 5 + 110 × rows, bit-exact round trip, the same bytes and SHA-256
    /// again, equal to the world's own encoded table, and every same-typed field pair told apart (lane D's field-swap guard).</summary>
    [Fact]
    public void DB1_SettlementRows_PopulatedTable_RoundTripBitExact_WithExactLength()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(5), start);
        foreach (var b in TieBattles(at)) rig.W.ScheduleBattle(rig.S, b);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at + 5 * Day + 3, 0, 0, 6_000, 3_000));        // the clamp at 0 (250,000 − 50,000 − 800,000)
        MoraleRig.Run(rig, at + 6 * Day);
        var rows = rig.W.MoraleRows.ToArray();
        Assert.Equal(6, rows.Length);
        Assert.Contains(rows, x => x.Entered == 0);
        Assert.Contains(rows, x => x.EntryInBattle == Cost);
        Assert.Contains(rows, x => x.AfterPpm == 0 && x.BeforePpm > 0);
        var bytes = MoraleCodec.Encode(rows);
        Assert.Equal(MoraleCodec.ExpectedLength(rows.Length), bytes.Length);
        Assert.Equal(5 + 111 * rows.Length, bytes.Length);                       // [MOR3, D-085 DD1] codec v3 (Fought appended): was 5 + 110 × rows
        var back = MoraleCodec.Decode(bytes);
        Assert.Equal(rows, back);
        Assert.Equal(bytes, MoraleCodec.Encode(back));
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(MoraleCodec.Encode(back)));
        Assert.Equal(bytes, rig.W.EncodedTables()[4]);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(rows);
    }

    // ------------------------------------------------------------------ in battle: both resolvers through one function

    /// <summary>DB1-R1 (a), DB1-R2 (a), BOTH RESOLVERS: every land formation placed on the field at the battle's start pays the in-battle
    /// charge ONCE, ~~at StartMs (a non-zero instant here),~~ and never again however long the battle runs; the two resolvers log the same
    /// rows (one function, D-079 D11). ~~Before the battle runs, each formation's morale is already its start less the charge.~~ The
    /// battle report carries EnteredAtMs = StartMs and EntryCharged = 50 for each. TEST INPUT geometry.
    /// [MOR3, D-085 DD1, 2026-11-11: renamed from …_PaysOnce_AtStartMs and re-pinned (PROPOSAL C1, morale proposal §12.4). The struck
    /// statements held while the charge fell at the entry. The entry is still recorded at StartMs, but the charge now falls at the
    /// battle's FIRST FIGHTING INSTANT, 7,332,000 ms (StartMs + 132,000: pulse 2 step 6; observed from outside the rule as the first
    /// executed attack in the trace, and as the AutoResolver's first step with a recorded attack, step 66): before the battle runs there
    /// is no row and each formation is at its start (900, 1000, 600); the rows stand at 7,332,000 with the morale each had then — the
    /// archers 640, having recovered 2 × 20 while holding through two quiet pulse ends (600 + 2 × HoldRecovery, by hand). MEASURED moves:
    /// AtMs 7,200,000 → 7,332,000 for all three rows; the archers' Before 600 → 640; the pre-run morale (850, 950, 550) → (900, 1000,
    /// 600).]</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtTheFirstFightingInstant(ulong seed)
    {
        var setup = ThreeFormations(seed, Cost);
        const long firstFight = 7_332_000;
        var expected = new[]
        {
            new EntryCharge(firstFight, 1, 900, Cost), new EntryCharge(firstFight, 2, 1000, Cost), new EntryCharge(firstFight, 3, 640, Cost),
        };
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var t = new TacticalBattle(setup, ai, ai);
        Assert.Empty(t.EntryCharges);                                                         // [MOR3] nothing before the fighting
        Assert.Equal((900L, 1000L, 600L), (t.Morale(1), t.Morale(2), t.Morale(3)));
        Assert.Equal(setup.StartMs, t.EnteredAtMs(1));
        var tr = TT.RunGuarded(t);
        Assert.Equal(firstFight, NoFightingTests.AttackEvents(t)[0].Key.TimeMs);              // [MOR3] observed from the trace
        Assert.Equal(firstFight, tr.FirstFightMs);
        Assert.True(tr.EndMs > setup.StartMs + 2 * setup.Rules.PulseMs, "the battle ran several pulses");
        Assert.Equal(expected, t.EntryCharges.ToArray());                                     // never charged again
        Assert.All(tr.Formations, f => Assert.Equal(((long?)setup.StartMs, Cost), (f.EnteredAtMs, f.EntryCharged)));
        var a = new AutoBattle(setup, new AutoCoefficients { DoctrineRetreatMorale = 0 }) { RecordLog = true };
        var ar = a.Run();
        Assert.Equal(66, a.Log.First(x => x.Mode != 0).Step);                                // [MOR3] observed from the step log
        Assert.Equal(expected, a.EntryCharges.ToArray());                                     // the same rows: one shared function
        Assert.All(ar.Formations, f => Assert.Equal(((long?)setup.StartMs, Cost), (f.EnteredAtMs, f.EntryCharged)));
    }

    /// <summary>The in-battle part at its default (0) is the exact identity in both resolvers: ~~the same result and state hashes as a
    /// setup whose rules never name it,~~ no row; the report still says when each formation entered (the settlement needs it whatever
    /// the in-battle part does). ~~Every golden of both resolvers pins the rest (MEASURED: unchanged).~~
    /// [MOR2, verification round 1, MOR2-R1-07] The struck comparison could not fail: its "unnamed" setup set EntryMoraleCost to the
    /// default, 0 — the same rule record (MEASURED: the verifier's V11, the entry record hashed also at cost 0, passed it and was caught
    /// only by two unrelated goldens). The identity is a property of compatibility with the engine BEFORE the entry code existed, so its
    /// oracle is that engine: the result, state and trace hashes below were recorded on the base commit 7cacc3a (lane MOR2's base, no
    /// entry code; the same scenarios with default rules; recorded by this round's temporary probe, scratchpad
    /// mor2-fix1/Fix1BaseHashes.cs.txt) for four battles — land formations entering at the start in both resolvers, a formation landing
    /// from a transport later, and an aircraft formation taking off — and the default, named (0) or not, must reproduce them exactly.</summary>
    [Fact]
    public void BothResolvers_TheInBattleDefault_IsTheIdentity()
    {
        var named = ThreeFormations(1, 0);
        var unnamed = named with { Rules = new TacticalRules() };
        Assert.Equal(0, new TacticalRules().EntryMoraleCost);
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        foreach (var s in new[] { named, unnamed })
        {
            var t = new TacticalBattle(s, ai, ai); var r = TT.RunGuarded(t);
            Assert.Equal(BaseThreeTactical, (r.ResultHash(), r.StateHash, r.TraceHash));
            Assert.Empty(t.EntryCharges);
            Assert.All(r.Formations, f => Assert.Equal(((long?)named.StartMs, 0L), (f.EnteredAtMs, f.EntryCharged)));
            var a = new AutoBattle(s, new AutoCoefficients()); var ar = a.Run();
            Assert.Equal(BaseThreeAuto, (ar.ResultHash(), ar.StateHash, ar.TraceHash));
            Assert.Empty(a.EntryCharges);
            Assert.All(ar.Formations, f => Assert.Equal(((long?)named.StartMs, 0L), (f.EnteredAtMs, f.EntryCharged)));
        }
        foreach (var rules in new[] { new TacticalRules { EntryMoraleCost = 0 }, new TacticalRules() })
        {
            var shore = DT.OnGrid(DT.Coast(Coast, 20), 1, null, rules,
                DT.Ship(1, 0, T30, C(19, 20)),
                new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)) { EmbarkedOn = 1, Morale = 800 },
                new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
            var sb = new TacticalBattle(shore, DT.Script((0, new OrderRequest(2, OrderKind.Disembark, 1, -1, C(25, 20)))), PassiveController.Instance);
            var sr = DT.RunChecked(sb);
            Assert.Equal(BaseShore, (sr.ResultHash(), sr.StateHash, sr.TraceHash));
            Assert.Equal(shore.StartMs + shore.Rules.StepMs, sr.Formations.Single(x => x.FormationId == 2).EnteredAtMs);   // it did land later
            Assert.Empty(sb.EntryCharges);
            var grid = new TerrainGrid(64, 40, Coast, TerrainClass.Open);
            for (int y = 5; y < 35; y++) grid.Set(30, y, TerrainClass.Hills, 40_000);
            var air = DT.OnGrid(grid, 7, null, rules,
                new FormationSpec(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
                new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)),
                MultiDomainScenarios.Flight(3, 0, DomainProfiles.Fighter, 1));
            var ab = new TacticalBattle(air, DT.Script((0, new OrderRequest(3, OrderKind.Patrol, 5, -1, C(20, 20)))), PassiveController.Instance);
            var abr = DT.RunChecked(ab);
            Assert.Equal(BaseAir, (abr.ResultHash(), abr.StateHash, abr.TraceHash));
            Assert.Equal(air.StartMs + 5 * air.Rules.StepMs, abr.Formations.Single(x => x.FormationId == 3).EnteredAtMs);    // it did take off
        }
    }

    // [MOR2, verification round 1, MOR2-R1-07] Recorded on the base commit 7cacc3a (before lane MOR2): (ResultHash, StateHash, TraceHash).
    private static readonly (string, string, string) BaseThreeTactical = ("EA2FB31A19A7E174048EDEF53E77C88EA9BBA3390AE7DB6EF57F667F6CFC01FB",
        "39059ED33777775CD7D36E1A51B51C0EEC1F1E4AD2C2F950408C9B65B9429A0A", "096D4C0483D9189EC6CC4551686A5399D9179EE86073291C8191AD6F7AC0FF5B");
    private static readonly (string, string, string) BaseThreeAuto = ("65FCB3BF61911E9879728F46CC94B0CC7D251A377C9335EB0297789487D9F97E",
        "454152F0A4D7E692E0B3C43FC955894DC9CBF10DE64438C8ED560E25FFD158F5", "B975D04A7E5C515076EDECBD2F636020FFCD3D836E6382FC913A8648BB68BF69");
    private static readonly (string, string, string) BaseShore = ("7D2C9B504A0F642F829EFCB06B3F5129C99B7C65CAFD1A1DCBED1FD69A89E1D0",
        "C2E208A79EEC63DCDA0B381E80247DD4C7D57EAFE3CDB4C567DE8C678A9917E4", "25C2E57EAA92C1D9BAA82EFD16DD1AD9691B8BCFF5337D8E73777E5C3A480A3E");
    private static readonly (string, string, string) BaseAir = ("89A8A99A4C86CCD445CCBADC8F828C8E62F3ABC636DAA6D26F4D3D6455136AF5",
        "21B3F48B0607C9A40E62501D1E12E5E142065B912A6415E9A7604C2CB722E216", "FD9080595B8EA9B0CBC4FF0DD3045ED4E91AFFB2BB60B7C02B296C1FD5D3F2D0");

    /// <summary>"Say whether the battle itself feels it" (brief; D-083 §4): with the in-battle part on, it does — ~~the charge is the
    /// formation's morale from its entry instant, so a formation that starts at 480, above the withdrawal line (450, the
    /// knowledge-only AI's RetreatMorale and the AutoResolver's doctrine), enters at 430 and withdraws at its first decision, in both
    /// resolvers; with the part off it stays and advances. Far apart, nobody fights before it is gone.~~ TEST INPUT geometry.
    /// [MOR3, D-085 DD1, 2026-11-11: renamed from …_TheBattleFeelsIt_AFormationEnteringBelowTheWithdrawalLineWithdrawsAtOnce and rebuilt
    /// (PROPOSAL C1, morale proposal §12.4). The struck behaviour was a change of morale BEFORE any fighting — the cost itself sent the
    /// formation away from a battle that then had no fighting, the very case D-085 §4 excludes ("it must not change morale in a battle
    /// that turns out to have no fighting"). MEASURED on lane MOR2's battle under C1: the formation at 480 is no longer charged at entry,
    /// advances and fights; its morale falls with its losses and it routs at the same instant as with the part off, so that battle no
    /// longer shows the charge in a decision. The battle feels the charge FROM THE FIRST FIGHTING INSTANT, shown here on a battle built
    /// for it: five heavy infantry at 480 hold (Defend) under the knowledge-only AI; one archer of the single-shot profile, ordered to
    /// Attack at pulse 2, looses ONE arrow at 214,000 ms (pulse 3), which misses; TacticalRules.HoldRecovery 0 (TEST INPUT) keeps the
    /// infantry's morale still while it holds. Off: the infantry stays at 480 and never withdraws (the battle ends in a Standoff).
    /// On: nothing is charged before 214,000 (no Retreat at pulses 0–3); at 214,000 the infantry is charged to 430, below the line, and
    /// withdraws at its next decision (pulse 4, 240,000 ms) in good order, untouched. The AutoResolver: the archer closes under its
    /// doctrine and looses its one volley at step 77 (154,000 ms); off, the infantry stays at 480; on, it is charged to 430 at step 77
    /// and withdraws at the next pulse start, escaping untouched in good order.]</summary>
    [Fact]
    public void BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_FromTheFirstFightingInstant_AFormationChargedBelowTheWithdrawalLineWithdrawsAtItsNextDecision()
    {
        BattleSetup S(long cost) => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(Array.Empty<Vec2L>(), Array.Empty<Vec2L>()),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.Archers with { FireIntervalSteps = 1_000_000 }, 1, Cell(16, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, Cell(24, 16)) { Posture = Posture.Defend, Morale = 480 },
            },
            2) { Rules = new TacticalRules { EntryMoraleCost = cost, HoldRecovery = 0 } };
        Assert.Equal(450, new KnowledgeAi().RetreatMorale);
        Assert.Equal(450, new AutoCoefficients().DoctrineRetreatMorale);
        ScriptedController Archer() => new ScriptedController().Add(2, new OrderRequest(1, OrderKind.Attack, 0, 2));
        var off = new TacticalBattle(S(0), Archer(), new KnowledgeAi());
        var offR = TT.RunGuarded(off);
        Assert.Equal(214_000L, NoFightingTests.AttackEvents(off).Single().Key.TimeMs);         // one arrow
        Assert.DoesNotContain(off.Orders, o => o.Request.FormationId == 2 && o.Request.Kind == OrderKind.Retreat);
        Assert.Equal((BattleEndReason.Standoff, 480L), (offR.Reason, offR.Formations.Single(f => f.FormationId == 2).MoraleEnd));
        var on = new TacticalBattle(S(Cost), Archer(), new KnowledgeAi());
        var onR = TT.RunGuarded(on);
        Assert.Equal(214_000L, onR.FirstFightMs);
        Assert.Equal(new EntryCharge(214_000, 2, 480, Cost), on.EntryCharges.Single(x => x.FormationId == 2));
        var retreat = Assert.Single(on.Orders, o => o.Request.FormationId == 2 && o.Request.Kind == OrderKind.Retreat);
        Assert.Equal((4L, 240_000L, OrderInvalid.None), (retreat.Pulse, retreat.AtMs, retreat.Invalid));   // its next decision
        var f2 = onR.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((0L, 0L, 5L, 0L, 430L), (f2.Killed, f2.DamageTaken, f2.Escaped, f2.RoutedEscaped, f2.MoraleEnd));   // gone in good order
        var aOff = new AutoBattle(S(0), new AutoCoefficients()) { RecordLog = true };
        var aOffR = aOff.Run();
        Assert.Equal(77, aOff.Log.Single(x => x.Mode != 0).Step);
        Assert.Equal((BattleEndReason.Standoff, 480L), (aOffR.Reason, aOffR.Formations.Single(f => f.FormationId == 2).MoraleEnd));
        var aOn = new AutoBattle(S(Cost), new AutoCoefficients()) { RecordLog = true };
        var aOnR = aOn.Run();
        Assert.Equal(154_000L, aOnR.FirstFightMs);
        Assert.Equal((480L, 430L), (aOn.Log.Single(x => x.Step == 76 && x.FormationId == 2).Morale, aOn.Log.Single(x => x.Step == 77 && x.FormationId == 2).Morale));
        var a2 = aOnR.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (aOnR.Reason, aOnR.Side));
        Assert.Equal((0L, 5L, 0L, 430L), (a2.Killed, a2.Escaped, a2.RoutedEscaped, a2.MoraleEnd));                     // withdrew in good order
    }

    /// <summary>The tactical state hash carries the entry record when the in-battle part is on: two battles whose formation starts at
    /// 30 or 40 lose all of it ~~at the entry~~ (the clamp at 0; a cost of 50) and then run identically — the same result, the same entity
    /// states and morale — yet their entry rows differ (charged 30 against 40), and so do their state hashes, in both resolvers.
    /// [MOR3, D-085 DD1, 2026-11-11 (PROPOSAL C1): formation 1 routs at the start (below the rout threshold; the AutoResolver withdraws
    /// it) and leaves without a blow; the battle's first fight comes later, at 7,506,000 ms (the archers' first volley at formation 2,
    /// both resolvers), and formation 1 is charged THEN, wherever it is — all it has, 30 or 40. MEASURED move: the rows' AtMs 7,200,000 →
    /// 7,506,000; the rest of the test unchanged and passing.]</summary>
    [Fact]
    public void BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual()
    {
        BattleSetup S(long m)
        {
            var s = ThreeFormations(1, Cost);
            return s with { Formations = new[] { s.Formations[0] with { Morale = m }, s.Formations[1], s.Formations[2] } };
        }
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var t30 = new TacticalBattle(S(30), ai, ai); var r30 = TT.RunGuarded(t30);
        var t40 = new TacticalBattle(S(40), ai, ai); var r40 = TT.RunGuarded(t40);
        Assert.Equal((7_506_000L, 7_506_000L), (r30.FirstFightMs, r40.FirstFightMs));                                       // [MOR3]
        Assert.Equal(new EntryCharge(7_506_000, 1, 30, 30), t30.EntryCharges[0]);                                           // [MOR3] was StartMs
        Assert.Equal(new EntryCharge(7_506_000, 1, 40, 40), t40.EntryCharges[0]);
        Assert.Equal(r30.ResultHash(), r40.ResultHash());
        Assert.Equal(t30.Entities().ToArray(), t40.Entities().ToArray());
        Assert.NotEqual(r30.StateHash, r40.StateHash);
        var a30 = new AutoBattle(S(30), new AutoCoefficients()).Run();
        var a40 = new AutoBattle(S(40), new AutoCoefficients()).Run();
        Assert.Equal(a30.ResultHash(), a40.ResultHash());
        Assert.NotEqual(a30.StateHash, a40.StateHash);
    }

    // ------------------------------------------------------------------ a later arrival: lane N's landing from a transport

    private const long Coast = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Coast);
    private static readonly VesselClass T30 = DomainProfiles.SteamTransport with { Capacity = 30 };

    /// <summary>Lane N's shore (TransportTests): a transport (formation 1) on the water next to the coast, riflemen (formation 2, 20,
    /// morale 800) aboard it, a distant enemy (formation 9, 5 riflemen) that sees nobody. TEST INPUT.</summary>
    private static BattleSetup Shore(long cost) => DT.OnGrid(DT.Coast(Coast, 20), 1, null, new TacticalRules { EntryMoraleCost = cost },
        DT.Ship(1, 0, T30, C(19, 20)),
        new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)) { EmbarkedOn = 1, Morale = 800 },
        new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));

    /// <summary>
    /// TURN-EXACT PIN of a later arrival (DB1-R1 (a): "later, at the instant it arrives as a reinforcement"). The riflemen start aboard
    /// (not on the field: they do not enter at the start) and are ordered ashore at step offset k of pulse 0 or 2. Observed from outside
    /// the rule (the entities' states at each completed instant): the first instant one of them stands on the field. ~~The formation's
    /// one entry row stands at exactly that instant — not at the battle's start, not one step later — with its start morale (800)
    /// before it; its morale is 800 at the completed instant before and 750 at that instant. The enemy, on the field from the start,
    /// paid at StartMs;~~ the transport (a vessel) is outside the in-battle rule. The AutoResolver refuses formations that start aboard
    /// (lane N's combined-arms layer has no auto model), so this arrival exists in the TacticalResolver only.
    /// [MOR3, D-085 DD1, 2026-11-11: renamed from …_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact. This shore battle has NO
    /// FIGHTING (the distant enemy sees nobody; it ends in a Standoff; MEASURED FirstFightMs null), so under C1 the in-battle part charges
    /// nobody at all — not the enemy at the start, not the riflemen at their landing: no row, the riflemen at 800 before and after. The
    /// ENTRY instant is pinned as before (EnteredAtMs = the first instant ashore, turn-exact). The charge of a later arrival is now pinned
    /// on a FOUGHT shore, at its landing when the fighting began before it and at the first fighting instant when it began after
    /// (NoFightingTests.C1_ALaterArrival_…). MEASURED moves: the enemy's row at StartMs and the riflemen's row at their landing → no
    /// rows; the riflemen's morale at the landing 750 → 800.]
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    [InlineData(2, 4)]
    public void Tactical_AFormationLandingFromATransport_EntersAtTheInstantItsFirstEntityIsPutAshore_TurnExact(int pulse, int k)
    {
        var setup = Shore(Cost);
        var b = new TacticalBattle(setup, DT.Script((pulse, new OrderRequest(2, OrderKind.Disembark, k, -1, C(25, 20)))), PassiveController.Instance);
        Assert.Null(b.EnteredAtMs(2));
        Assert.Empty(b.EntryCharges);                                                     // [MOR3] nothing before any fighting
        long ashoreAt = long.MinValue, prevMorale = -1, prevInstant = long.MinValue, moraleAt = -1;
        DT.RunUntil(b, () =>
        {
            bool ashore = b.Entities().Any(e => e.FormationId == 2 && e.State == EntityState.Active);
            if (!ashore) { prevMorale = b.Morale(2); prevInstant = b.Scheduler.NowMs; return false; }
            ashoreAt = b.Scheduler.NowMs; moraleAt = b.Morale(2);
            return true;
        });
        Assert.Equal(setup.StartMs + pulse * setup.Rules.PulseMs + k * setup.Rules.StepMs, ashoreAt);   // the delivered step (lane N's landing pin)
        Assert.True(ashoreAt > setup.StartMs);
        Assert.Equal((800L, 800L), (prevMorale, moraleAt));                               // [MOR3] was (800, 750): no fighting, no charge
        Assert.True(prevInstant < ashoreAt);
        Assert.Equal(ashoreAt, b.EnteredAtMs(2));                                         // the entry instant: not StartMs, not ashoreAt + StepMs
        while (b.Advance()) Assert.True(b.CurrentPulse <= DT.HarnessMaxPulses, "harness guard: non-termination");
        var r = b.Result!;
        Assert.Null(r.FirstFightMs);                                                      // [MOR3] the battle had no fighting
        Assert.Empty(b.EntryCharges);                                                     // [MOR3] so nobody was charged, ever
        Assert.Equal((ashoreAt, 0L), (r.Formations.Single(x => x.FormationId == 2).EnteredAtMs, r.Formations.Single(x => x.FormationId == 2).EntryCharged));
        Assert.Throws<NotSupportedException>(() => new AutoBattle(setup, new AutoCoefficients()));
    }

    /// <summary>DB1-R2 (a): a formation that leaves the field and is placed on it again pays ONCE. Lane N's swap (TransportTests): on each
    /// side two formations share a transport and swap places every pulse — one lands while the other boards. Formation 2 (ashore at
    /// the start) entered at StartMs; it boards, lands again, boards again …; formation 3 (aboard at the start) entered at its first
    /// landing. Observed: each of them left the field and came back at least once; yet each has exactly one entry row, at its first
    /// placement, and its morale was charged once (the battle has no casualty: nobody sees anybody). TEST INPUT.
    /// [MOR2, verification round 1, MOR2-R1-06 (V02)] Also at the DEFAULT cost 0: the report's EnteredAtMs is still each formation's
    /// FIRST placement (the settlement reads it whatever the in-battle part does), with no row and no charge (morale 900, 700 at the
    /// end). Until this round the re-entry was pinned at cost 50 only, so a once-guard that applied only while the cost was on survived
    /// the whole suite (MEASURED: the verifier's V02, 1474 of 1474 passed).
    /// [MOR3, D-085 DD1, 2026-11-11: this swap battle has NO FIGHTING ("nobody sees anybody"; MEASURED FirstFightMs null), so under C1 the
    /// cost-50 case now charges nothing at all, exactly like the default: no row, EntryCharged 0, morale 900 and 700 at the end. MEASURED
    /// moves of the cost-50 case: one row per formation at its first placement → no rows; EntryCharged 50 → 0; end morale (850, 650) →
    /// (900, 700). The ONCE property with the part on, re-entry included, is now pinned on the same swap made a fought battle
    /// (NoFightingTests.C1_OncePerFormation_WithReEntry_InAFoughtBattle); the entry instants (each formation's first placement) are pinned
    /// here at both costs, unchanged.]</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(Cost)]
    public void Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce(long cost)
    {
        var t = T30 with { SightMm = 500_000 };
        var forms = new[]
        {
            DT.Ship(1, 0, t, C(19, 30)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { Morale = 900 },
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { EmbarkedOn = 1, Morale = 700 },
            DT.Ship(4, 1, t, C(19, 6)),
            new FormationSpec(5, 1, DomainProfiles.Riflemen, 5, C(20, 6)),
            new FormationSpec(6, 1, DomainProfiles.Riflemen, 5, C(20, 6)) { EmbarkedOn = 4 },
        };
        var setup = DT.OnGrid(DT.Coast(Coast, 20), 1, null, new TacticalRules { EntryMoraleCost = cost, Inactivity = InactivityModel.LegacySideDisengage }, forms);
        ScriptedController Swap(long a, long c, long ship, long y)
        {
            var s = new ScriptedController();
            for (int p = 0; p < 40; p++)
            {
                long boarding = p % 2 == 0 ? a : c, landing = p % 2 == 0 ? c : a;
                s.Add(p, new OrderRequest(boarding, OrderKind.Embark, 0, ship));
                s.Add(p, new OrderRequest(landing, OrderKind.Disembark, 0, -1, C(21, y)));
            }
            return s;
        }
        var b = new TacticalBattle(setup, Swap(2, 3, 1, 30), Swap(5, 6, 4, 6));
        var placements = new Dictionary<long, List<long>>();                              // lookups only: formation → instants it came onto the field
        var wasOn = new Dictionary<long, bool>();
        foreach (long f in new long[] { 2, 3, 5, 6 }) { placements[f] = new List<long>(); wasOn[f] = false; }
        void Watch()
        {
            foreach (long f in new long[] { 2, 3, 5, 6 })
            {
                bool on = b.Entities().Any(e => e.FormationId == f && e.State == EntityState.Active);
                if (on && !wasOn[f]) placements[f].Add(b.Scheduler.NowMs);
                wasOn[f] = on;
            }
        }
        Watch();
        var r = DT.RunChecked(b, 40, Watch);
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        foreach (long f in new long[] { 2, 3, 5, 6 })
        {
            Assert.True(placements[f].Count >= 2, $"formation {f} left the field and was placed on it again ({placements[f].Count} placements)");
            Assert.Equal(placements[f][0], r.Formations.Single(x => x.FormationId == f).EnteredAtMs);   // its first placement, at either cost
            Assert.Equal(placements[f][0], b.EnteredAtMs(f));
            var rows = b.EntryCharges.Where(x => x.FormationId == f).ToArray();
            // [MOR3, D-085 DD1] no fighting, so at either cost: no row, nothing charged (was, at cost 50: one row at the first placement)
            Assert.Empty(rows); Assert.Equal(0, r.Formations.Single(x => x.FormationId == f).EntryCharged);
        }
        Assert.Null(r.FirstFightMs);                                                                // [MOR3] the battle had no fighting
        Assert.Equal(setup.StartMs, r.Formations.Single(x => x.FormationId == 2).EnteredAtMs);    // ashore at the start
        Assert.True(r.Formations.Single(x => x.FormationId == 3).EnteredAtMs > setup.StartMs);     // aboard at the start: its first landing
        Assert.Equal((900L, 700L), (r.Formations.Single(x => x.FormationId == 2).MoraleEnd, r.Formations.Single(x => x.FormationId == 3).MoraleEnd));   // [MOR3] was 900 − cost, 700 − cost
        Assert.All(r.Formations, f => Assert.Equal(0, f.Killed));
    }

    /// <summary>DB1-R1 (a): "A formation that never reaches the field ... does not enter and pays nothing". The riflemen stay aboard
    /// the whole battle (no landing order): no entry row, EnteredAtMs null, EntryCharged 0 and the end morale equals the start (800);
    /// the hand-off reads "never entered", so the army's settlement changes nothing (no entry cost, no decrease, no rise).</summary>
    [Fact]
    public void Tactical_AFormationThatNeverLeavesItsTransport_NeverEntersAndPaysNothing()
    {
        var setup = Shore(Cost);
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = DT.RunChecked(b, 60);
        var f2 = r.Formations.Single(x => x.FormationId == 2);
        Assert.Equal(20, f2.EmbarkedAtEnd);
        Assert.Equal(((long?)null, 0L, 800L), (f2.EnteredAtMs, f2.EntryCharged, f2.MoraleEnd));
        Assert.DoesNotContain(b.EntryCharges, x => x.FormationId == 2);
        Assert.Equal(setup.StartMs, r.Formations.Single(x => x.FormationId == 9).EnteredAtMs);
        Assert.Equal(setup.StartMs, r.Formations.Single(x => x.FormationId == 1).EnteredAtMs);    // the vessel: reported entered at the start
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        var collapse = new CollapseInput(0, 0, new long[] { f2.Start, 0, 0, 0, 0 }, f2.MoraleEnd, setup.Rules.MoraleMax);
        rig.W.ScheduleBattle(rig.S, HandOff(r, 2, army: 0, at, collapse, startMorale: 800, beaten: 5, winnerSide: 20));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal(((byte)0, 0L, 0L, 800_000L), (m.Entered, m.EntryPpm, m.DecreasePpm, m.AfterPpm));
    }

    // ------------------------------------------------------------------ the mutual-elimination win (lane AP's AP-R1-14; proposal §11.8)

    /// <summary>
    /// Lane AP's observation AP-R1-14 with a REAL battle through the hand-off: lane AP's duel (one entity a side, both killed in the
    /// same instant, side 1 the defender) ends BothEliminated and DA6 names the defender. Through lane W's collapse both formations are
    /// all dead; each settles from its battle-scale start 800 to its end 0. PROPOSAL (a), applied (morale proposal §11.8): DA1's rise
    /// does not depend on what the winner has left — the defender's army gains the full rise for an equal force, and DR29 takes its
    /// whole loss: clamp(800,000 − 50,000 − 800,000 + 200,000) = 150,000, the attacker's clamp(800,000 − 50,000 − 800,000) = 0. And the
    /// defender's army has no men left: the rise lifts nobody, which is why the aggregation over formations counts persons after the
    /// battle (§11.6). (Alternative (b), a rise scaled by the winner's share still in good order, would give 0 here; not adopted.)
    /// </summary>
    [Fact]
    public void DA1_AMutualEliminationWin_TheRiseIsUnchanged_AndTheWinnerHasNobodyLeft()
    {
        var duel = Proto.M7.Tests.Concealment.EqualSidesTests.Duel(1, 0, 0, 1, -1);
        var setup = duel with { Formations = duel.Formations.Select(f => f with { Morale = 800 }).ToArray() };
        var battle = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(battle);
        Assert.Equal((BattleEndReason.BothEliminated, 1), (r.Reason, BattleWinner.Reported(r)));
        var inputs = BattleCollapse.FromTactical(battle, setup, new[] { (1L, 0), (2L, 1) });
        Assert.All(inputs, c => Assert.Equal((c.Entities, 0L), (c.EntityCounts[(int)CollapseCategory.Dead], c.MoraleEnd)));   // both annihilated
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, HandOff(r, 1, army: 0, at, inputs[0], startMorale: 800, beaten: 2_000, winnerSide: 2_000));
        rig.W.ScheduleBattle(rig.S, HandOff(r, 2, army: 1, at, inputs[1], startMorale: 800, beaten: 2_000, winnerSide: 2_000));
        MoraleRig.Run(rig, at + 1);
        var defender = rig.W.MoraleRows.Single(x => x.ArmyId == 1);
        Assert.Equal((800_000L, 200_000L, CostPpm, 150_000L), (defender.DecreasePpm, defender.RisePpm, defender.EntryPpm, defender.AfterPpm));
        Assert.Equal((800_000L, 0L, 0L), (rig.W.MoraleRows.Single(x => x.ArmyId == 0).DecreasePpm, rig.W.MoraleRows.Single(x => x.ArmyId == 0).RisePpm,
                                          rig.W.MoraleRows.Single(x => x.ArmyId == 0).AfterPpm));
        Assert.Equal(0, rig.W.Ledger.Balance(rig.W.Economy.MenAccountOf(1)));                 // the winner's army has nobody left
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A lossless battle of one army: it ends where it started (800) with all 50 entities fit.</summary>
    private static SupportBattle Lossless(int army, int side, long at, int winner, long beaten, long winnerSide)
        => new(army, at, new CollapseInput(side, side, new long[] { 50, 0, 0, 0, 0 }, 800, 1000), false, 800, 2_000, 0)
           { WinnerSide = winner, BeatenPersons = beaten, WinnerSidePersons = winnerSide };

    /// <summary>The battle → campaign hand-off this lane defines for the stand-in (M7.15 builds the real one): the army of tactical
    /// formation <paramref name="formationId"/> entered iff the report gives it an entry instant; the in-battle charge it took is the
    /// report's EntryCharged; the winner is the one the report names. [MOR3, D-085 DD1] and the battle had fighting iff the report gives
    /// it a first fighting instant (BattleResult.FirstFightMs, DD1-R2 (a)).</summary>
    internal static SupportBattle HandOff(BattleResult r, long formationId, int army, long at, CollapseInput collapse, long startMorale, long beaten, long winnerSide)
    {
        var f = r.Formations.Single(x => x.FormationId == formationId);
        int winner = BattleWinner.Reported(r);
        return new SupportBattle(army, at, collapse, r.HoldsField == f.Side, startMorale, 2_000, 0)
        {
            Entered = f.EnteredAtMs is not null, EntryChargedInBattle = f.EntryCharged,
            WinnerSide = winner, BeatenPersons = winner >= 0 ? beaten : 0, WinnerSidePersons = winner >= 0 ? winnerSide : 0,
            Fought = BattleFighting.SettlesAsFought(r, formationId),                                                    // [MOR3] DD1-R1 (a)
        };
    }

    private static SupportBattle[] TieBattles(long at) => new[]
    {
        MoraleRig.Battle(0, 0, at, 700, 0, 4_000, 4_000),
        MoraleRig.Battle(1, 1, at, 300, 0, 4_000, 4_000),
        MoraleRig.NeverEntered(2, 0, at, 0, 4_000, 4_000),
        MoraleRig.Battle(3, 1, at, 700, 0, 4_000, 4_000) with { EntryChargedInBattle = Cost },
        MoraleRig.NeverEntered(4, 1, at, 0, 4_000, 4_000),
    };

    /// <summary>Side 0: heavy infantry 30 (morale 900) and archers 20 (morale 600); side 1: heavy infantry 40 (full morale). They close
    /// and fight for several pulses; the battle starts at 7,200,000 ms. TEST INPUT geometry. [MOR3: internal, reused by the D-085 DD1 tests;
    /// its first fighting instant is 7,332,000 in both resolvers (MEASURED, NoFightingTests).]</summary>
    internal static BattleSetup ThreeFormations(ulong seed, long cost)
        => new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { Cell(30, 16) }, new[] { Cell(16, 16) }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 30, Cell(14, 16)) { Morale = 900 },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 40, Cell(32, 16)),
                new FormationSpec(3, 0, UnitProfiles.Archers, 20, Cell(10, 16)) { Morale = 600, Posture = Posture.Defend },
            },
            seed) { Rules = new TacticalRules { EntryMoraleCost = cost }, StartMs = 7_200_000 };

    private static IEnumerable<int[]> Permutations(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = i;
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Rec(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }
}
