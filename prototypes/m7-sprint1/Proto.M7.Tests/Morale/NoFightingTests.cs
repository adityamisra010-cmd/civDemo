using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Morale;
using Proto.M7.Rng;
using Proto.M7.Sandbox;
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
/// [MOR3] D-085 DD1 ("No fighting doesn't affect morale at all then. There has to be done fighting to alter morale"; docs/m7-plan/
/// morale-proposal.md §12). Three parts, each with its own section below:
/// <list type="bullet">
/// <item>THE FOUGHT REPORT (DD1-R2 (a), applied: at least one attack resolved against an enemy formation, whether or not it dealt
///   damage): both resolvers report the battle's first fighting instant (BattleResult.FirstFightMs). Its oracle here is always observed
///   from OUTSIDE the rule's code: the tactical engine's event trace (the executed attack events) and the AutoResolver's step log (the
///   steps in which a formation's recorded attack mode is not 0). [MOR3, verification round 1, MOR3-R1-01] The trace's attack events
///   include missile shots that had no line of fire, which are no attacks; so they serve as the oracle only once validated against the
///   battle's attack count (<see cref="AttackEvents"/>), and a battle with such shots is observed with the line-of-fire oracle
///   (<see cref="RunObservingShots"/>), which recomputes each shot's line of fire from outside the rule.</item>
/// <item>THE SETTLEMENT (DD1-R1 (a), applied: fighting is read per battle): a battle with fighting settles as before; a battle without
///   fighting changes no army's morale (no entry cost, no DR29 decrease, no DA1 rise), recorded on the settlement row (codec v3).</item>
/// <item>THE IN-BATTLE PART (D-085 §4; PROPOSAL C1): the in-battle entry charge, when switched on, falls at the battle's first fighting
///   instant (or at a later entry), so it changes nothing before that instant and nothing at all in a battle without fighting.</item>
/// </list>
/// Every geometry, seed and profile below is TEST INPUT; every number asserted is MEASURED on this tree or derived by hand from the
/// PROPOSAL values (settlement entry cost 50,000; DA1 rise R = 400,000; in-battle part 50 when a test switches it on).
/// </summary>
public sealed class NoFightingTests
{
    private const long Cost = 50;
    private const long CostPpm = 50_000;
    private const long Coast = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Coast);

    /// <summary>A single-shot archer (TEST INPUT): an archer whose fire interval outlasts any battle here, so it looses one arrow.</summary>
    private static readonly UnitProfile OneShot = UnitProfiles.Archers with { FireIntervalSteps = 1_000_000 };

    // ------------------------------------------------------------------ helpers (observation from outside the rule)

    /// <summary>The executed ATTACK events of a FINISHED battle's trace, in execution order: melee blows and missile shots
    /// (TacticalKinds.Fire), crew-served and domain weapon shots (DomainFire), boarding fights, rams. A bow contact that is not a ram
    /// (RamCollision) is not in the list (READING MOR3-R2 (a)).
    /// [MOR3, verification round 1, MOR3-R1-01] Not every event listed is an attack. The trace holds every Fire event the engine
    /// EXECUTED, and a missile shot that its handler finds without a line of fire (blocked by a wall, the ground or a structure) is
    /// executed and is no attack: "a shot with no line of fire is not fired, so it is no attack" (morale proposal §12.2). The trace
    /// alone cannot tell such a shot from a fired one that missed. So the list is an oracle of fighting only once it is VALIDATED
    /// against the battle's own attack count: <see cref="UncountedLandAttackEvents"/> must be 0 (every executed Fire event, and every
    /// land shooter's DomainFire event, was a counted attack). A battle with a blocked shot fails that check here; it is observed with
    /// the line-of-fire oracle <see cref="RunObservingShots"/>. While a battle runs, <see cref="AttackEventsSoFar"/> gives the list
    /// unvalidated, and each test that reads it then validates the finished battle with this method.</summary>
    internal static TraceEntry[] AttackEvents(TacticalBattle b)
    {
        Assert.True(b.IsOver, "the attack-event oracle is validated on a finished battle");
        long uncounted = UncountedLandAttackEvents(b, b.Result!);
        Assert.True(uncounted == 0, $"{uncounted} executed Fire events of this battle were no attacks (shots with no line of fire): " +
                                    "its trace's attack events are no oracle of fighting; observe it with RunObservingShots");
        return AttackEventsSoFar(b);
    }

    /// <summary>[MOR3, verification round 1, MOR3-R1-01] The list of <see cref="AttackEvents"/> while the battle runs, NOT validated.</summary>
    internal static TraceEntry[] AttackEventsSoFar(TacticalBattle b)
        => b.Scheduler.Trace.Entries.Where(e => e.Outcome == TraceOutcome.Executed
            && (e.Kind == TacticalKinds.Fire || e.Kind == DomainKinds.DomainFire || e.Kind == DomainKinds.Boarding || e.Kind == DomainKinds.Ram)).ToArray();

    /// <summary>[MOR3, verification round 1, MOR3-R1-01] The executed melee and missile Fire events, and the DomainFire events of land
    /// shooters (crew-served weapons), that the engine did NOT count as attacks: their number less the land formations' counted attacks
    /// (Σ AttacksMade, the count each resolver keeps next to its fought report). Each such event counts one attack unless its handler
    /// rejects it as no shot (a missile shot with no line of fire), so 0 means every land attack event of the trace was an attack. Vessel
    /// and aircraft formations are left out (their events are counted on their own formations; none of them is a missile Fire event).</summary>
    internal static long UncountedLandAttackEvents(TacticalBattle b, BattleResult r)
    {
        var land = b.Entities();
        var ids = land.Select(e => e.Id).ToArray();
        Array.Sort(ids);
        var forms = land.Select(e => e.FormationId).Distinct().ToArray();
        Array.Sort(forms);
        long events = b.Scheduler.Trace.Entries.Count(e => e.Outcome == TraceOutcome.Executed
            && (e.Kind == TacticalKinds.Fire || (e.Kind == DomainKinds.DomainFire && Array.BinarySearch(ids, e.Source.Id) >= 0)));
        long counted = r.Formations.Where(f => Array.BinarySearch(forms, f.FormationId) >= 0).Sum(f => f.AttacksMade);
        return events - counted;
    }

    /// <summary>[MOR3, verification round 1, MOR3-R1-01] One executed missile shot as the line-of-fire oracle classified it: Block is
    /// LosBlock.None for a shot with a line of fire (an attack, hit or miss), otherwise what stopped it (no attack).</summary>
    internal readonly record struct ObservedShot(TraceEntry Event, LosBlock Block);

    /// <summary>[MOR3, verification round 1, MOR3-R1-01] The LINE-OF-FIRE ORACLE, observed from outside the rule: runs the battle to its
    /// end (TT.RunGuarded's guard) and classifies every executed missile shot (TacticalKinds.Fire, mode 2) right after it executed, at its
    /// own instant, with the public line-of-fire functions that the engine's Fire handler applies (CITED, TacticalBattle.Fire): first
    /// Projectiles.DirectFire from the shooter toward its target past the shooter's enemies on the field (blocked by a wall, the ground or
    /// the grid's edge), then, on a geometric hit, TerrainSight.FirePath to the entity struck (blocked by an opaque structure).</summary>
    internal static (BattleResult R, ObservedShot[] Shots) RunObservingShots(TacticalBattle b, BattleSetup s)
    {
        var shots = new List<ObservedShot>();
        int seen = 0;
        b.Start();
        while (b.Advance())
        {
            Classify();
            Assert.True(b.CurrentPulse <= TT.HarnessMaxPulses, "harness guard: non-termination is a FAILURE");
        }
        Classify();
        return (b.Result!, shots.ToArray());

        void Classify()
        {
            var trace = b.Scheduler.Trace.Entries;
            for (; seen < trace.Count; seen++)
            {
                var e = trace[seen];
                if (e.Outcome == TraceOutcome.Executed && e.Kind == TacticalKinds.Fire && e.A == 2) shots.Add(new ObservedShot(e, LineOfFire(b, s, e)));
            }
        }
    }

    /// <summary>What stopped a missile shot, recomputed from outside the rule (LosBlock.None: it had a line of fire).</summary>
    private static LosBlock LineOfFire(TacticalBattle b, BattleSetup s, TraceEntry shot)
    {
        var ents = b.Entities();
        var a = ents.Single(x => x.Id == shot.Source.Id);
        var tg = ents.Single(x => x.Id == shot.Target.Id);
        var discs = ents.Where(x => x.Side != a.Side && (x.State == EntityState.Active || x.State == EntityState.Routing))
                        .Select(x => new Disc(x.Id, x.Pos, s.Rules.EntityRadiusMm)).ToArray();
        var line = Projectiles.DirectFire(s.Grid, a.Pos, s.Rules.MuzzleHeightMm, tg.Pos, s.Rules.AimHeightMm, discs, a.Id);
        if (line.Outcome == FireOutcome.Blocked) return line.Block;
        if (line.Outcome != FireOutcome.Hit) return LosBlock.None;
        var struck = ents.Single(x => x.Id == line.EntityId);
        var path = TerrainSight.FirePath(s.Grid, s.Effects, a.Pos, s.Rules.MuzzleHeightMm, tg.Pos, s.Rules.AimHeightMm, struck.Pos, line.TNum, line.TDen);
        return path.Blocked ? path.Block : LosBlock.None;
    }

    private static TraceEntry[] Executed(TacticalBattle b, int kind)
        => b.Scheduler.Trace.Entries.Where(e => e.Outcome == TraceOutcome.Executed && e.Kind == kind).ToArray();

    /// <summary>Lane W's collapse input of an AutoResolver formation (the War Sandbox's FromAuto form: no wounded category).</summary>
    private static CollapseInput FromAuto(FormationResult f, int campaignFormation, long moraleMax)
        => new(campaignFormation, f.Side, new[] { f.Survivors - f.RoutedEscaped, 0, f.RoutedEscaped, f.Killed, f.Surrendered }, f.MoraleEnd, moraleMax);

    /// <summary>Settles army <paramref name="army"/>'s part in a real battle through the hand-off (EntryCostTests.HandOff, which reads the
    /// winner and — [MOR3] — whether the battle was fought from the report), from a campaign morale of <paramref name="s"/> × 1,000 (the
    /// hand-off's mapping): the army is first brought there from lane E's opening 800,000 by a fought stand-in battle lost from 800 to
    /// s + 50 (800,000 − 50,000 − (750 − s) × 1,000); at s = 800 it is there already.</summary>
    private static MoraleSettlementRow Settle(BattleResult r, long formationId, int army, CollapseInput collapse, long s, long beaten, long winnerSide)
    {
        long start = MoraleRig.WarStart(), at = start + 2 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        if (s != 800) rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(army, army, start + Day, s + 50, -1, 0, 0));
        rig.W.ScheduleBattle(rig.S, EntryCostTests.HandOff(r, formationId, army, at, collapse, startMorale: s, beaten: beaten, winnerSide: winnerSide));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single(x => x.AtMs == at);
        Assert.Equal(s * 1_000, m.BeforePpm);
        return m;
    }

    /// <summary>One archer of the single-shot profile (side 0, column <paramref name="col"/>, row 16) and five heavy infantry (side 1,
    /// anchored at column 24), both formations at 800 (lane E's opening campaign morale on the battle scale), Defend posture, no
    /// objectives. At column 22 the archer has the infantry within its range from the start; at column 21 it never has (MEASURED).</summary>
    private static BattleSetup Shot(ulong seed, long col, long cost)
        => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(Array.Empty<Vec2L>(), Array.Empty<Vec2L>()),
            new[]
            {
                new FormationSpec(1, 0, OneShot, 1, Cell(col, 16)) { Posture = Posture.Defend, Morale = 800 },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, Cell(24, 16)) { Posture = Posture.Defend, Morale = 800 },
            },
            seed) { Rules = new TacticalRules { EntryMoraleCost = cost } };

    /// <summary>The same two formations with the archer out of range at column 16; ordered (scripted) to Attack formation 2 at pulse 2,
    /// it closes and looses its one arrow at 214,000 ms (MEASURED). <paramref name="hold"/>: TacticalRules.HoldRecovery (TEST INPUT 0
    /// where a test needs the infantry's morale to stand still while it holds).</summary>
    private static BattleSetup Delayed(ulong seed, long cost, long infantryMorale, long hold = 20)
        => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(Array.Empty<Vec2L>(), Array.Empty<Vec2L>()),
            new[]
            {
                new FormationSpec(1, 0, OneShot, 1, Cell(16, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, Cell(24, 16)) { Posture = Posture.Defend, Morale = infantryMorale },
            },
            seed) { Rules = new TacticalRules { EntryMoraleCost = cost, HoldRecovery = hold } };

    private static ScriptedController AttackAtPulse2() => DT.Script((2, new OrderRequest(1, OrderKind.Attack, 0, 2)));

    // ================================================================== 1. THE FOUGHT REPORT (DD1-R2 (a))

    /// <summary>The scenarios of <see cref="TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace"/>.</summary>
    private static (TacticalBattle B, BattleResult R) DomainScenario(string kind)
    {
        switch (kind)
        {
            case "melee":
            {
                var b = new TacticalBattle(MeetingAsymmetric(UnitProfiles.HeavyInfantry, 30, UnitProfiles.HeavyInfantry, 40, 1), new KnowledgeAi(), new KnowledgeAi());
                return (b, TT.RunGuarded(b));
            }
            case "missile":
            {
                var b = new TacticalBattle(Shot(2, 22, 0), PassiveController.Instance, PassiveController.Instance);
                return (b, TT.RunGuarded(b));
            }
            case "landGuns":
            {
                var gun = new FormationSpec(2, 1, DomainProfiles.GunCrew, 3, C(24, 20)) { Weapons = new[] { DomainProfiles.CoastalGun } };
                var s = DT.OnGrid(DT.Coast(Coast, 20), 5, null, null, DT.Ship(1, 0, DomainProfiles.SteamTransport, C(12, 20)), gun);
                var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
                return (b, DT.RunChecked(b, 60));
            }
            case "vesselGuns":
            {
                var b = new TacticalBattle(MultiDomainScenarios.LineBattle(DomainProfiles.TurretWarship, 50_000, 3), new KnowledgeAi(), new KnowledgeAi());
                return (b, DT.RunChecked(b, 120));
            }
            case "aircraft":
            {
                var g = new TerrainGrid(64, 40, Coast, TerrainClass.Open);
                for (int y = 5; y < 35; y++) g.Set(30, y, TerrainClass.Hills, 40_000);
                var s = DT.OnGrid(g, 11, null, null, new FormationSpec(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
                    new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)), MultiDomainScenarios.Flight(3, 0, DomainProfiles.StrikeAircraft, 1));
                var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(3, OrderKind.Attack, 0, 2))), PassiveController.Instance);
                return (b, DT.RunChecked(b, 80));
            }
            case "boarding":
            {
                var unarmed = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>() };
                var s = DT.Sea(50_000, 6, null, DT.Ship(1, 0, unarmed, DT.M(1000, 1000)), DT.Ship(2, 1, DomainProfiles.OaredTransport, DT.M(1060, 1000)),
                    new FormationSpec(3, 1, DomainProfiles.Riflemen, 30, DT.M(2500, 1000)) { EmbarkedOn = 2 });
                var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
                return (b, DT.RunChecked(b));
            }
            case "ram":
            {
                var b = RamVerificationRound2Tests.ObliqueMeeting();
                return (b, DT.RunChecked(b, 60));
            }
            default: throw new ArgumentException(kind);
        }
    }

    /// <summary>
    /// DD1-R2 (a) in EVERY DOMAIN of the TacticalResolver: the battle's first fighting instant is the instant of its first attack —
    /// a melee blow (heavy infantry meeting), a missile shot that MISSED (the single-shot archer: one Fire event, no Damage event at
    /// all), a land formation's crew-served gun firing at a hull (lane N's coastal guns), a vessel's guns (lane N's line battle), an
    /// aircraft's bomb (lane N's strike aircraft), a boarding fight (an unarmed galley boards a transport: its first attack is the
    /// fight aboard), a ram (lane RAM's oblique meeting: a glancing ram). The oracle is the event trace, read from outside the rule;
    /// the first attack's kind, shooter and target are pinned so the case pins the domain it names. MEASURED instants.
    /// </summary>
    [Theory]
    [InlineData("melee", 148_000L, TacticalKinds.Fire, 1L, 1000L, -1L)]
    [InlineData("missile", 0L, TacticalKinds.Fire, 2L, 1000L, -1L)]
    [InlineData("landGuns", 0L, DomainKinds.DomainFire, -1L, 2000L, 1000L)]
    [InlineData("vesselGuns", 0L, DomainKinds.DomainFire, -1L, 1000L, 2000L)]
    [InlineData("aircraft", 14_000L, DomainKinds.DomainFire, -1L, 3000L, 2009L)]
    [InlineData("boarding", 26_000L, DomainKinds.Boarding, -1L, 1000L, 2000L)]
    [InlineData("ram", 198_000L, DomainKinds.Ram, -1L, 1000L, 2000L)]
    public void TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace(string kind, long first, int eventKind, long mode, long source, long target)
    {
        var (b, r) = DomainScenario(kind);
        var attacks = AttackEvents(b);
        Assert.NotEmpty(attacks);
        var a = attacks[0];
        Assert.Equal((eventKind, source), (a.Kind, a.Source.Id));
        if (mode >= 0) Assert.Equal(mode, a.A);                                          // 1 melee, 2 missile
        if (target >= 0) Assert.Equal(target, a.Target.Id);
        Assert.Equal(first, a.Key.TimeMs);
        Assert.Equal(first, r.FirstFightMs);
        Assert.True(r.Fought);
        Assert.True(r.Formations.Sum(f => f.AttacksMade) > 0);
        if (kind == "missile")
        {
            Assert.Single(attacks);                                                     // exactly one attack in the whole battle
            Assert.Empty(Executed(b, TacticalKinds.Damage));                            // and it missed
            Assert.All(r.Formations, f => Assert.Equal(0, f.DamageDealt));
        }
    }

    /// <summary>
    /// DD1-R2 (a) in the AutoResolver: its first fighting instant is StartMs + k × StepMs for the first step k in which a formation
    /// strikes in melee or looses a volley, observed from its step log (the recorded attack mode). Melee: the same heavy-infantry
    /// meeting as the TacticalResolver's case above, whose first blow falls at the same instant, 148,000 (step 74, mode 1); a volley:
    /// the single-shot archer at column 22, step 0, mode 2 — one volley in the whole battle (MEASURED).
    /// </summary>
    [Fact]
    public void AutoResolver_TheFirstStepWithAnAttack_IsTheBattlesFirstFightingInstant_ObservedFromItsStepLog()
    {
        var melee = new AutoBattle(MeetingAsymmetric(UnitProfiles.HeavyInfantry, 30, UnitProfiles.HeavyInfantry, 40, 1), new AutoCoefficients()) { RecordLog = true };
        var mr = melee.Run();
        var m = melee.Log.First(x => x.Mode != 0);
        Assert.Equal((74L, 1L, 1), (m.Step, m.FormationId, m.Mode));
        Assert.Equal(m.Step * new TacticalRules().StepMs, mr.FirstFightMs);
        Assert.Equal(148_000L, mr.FirstFightMs);
        var shot = new AutoBattle(Shot(2, 22, 0), new AutoCoefficients()) { RecordLog = true };
        var sr = shot.Run();
        var attacks = shot.Log.Where(x => x.Mode != 0).ToArray();
        Assert.Equal((0L, 1L, 2), (attacks.Single().Step, attacks.Single().FormationId, attacks.Single().Mode));
        Assert.Equal(0L, sr.FirstFightMs);
        var none = new AutoBattle(Shot(2, 21, 0), new AutoCoefficients()) { RecordLog = true };
        var nr = none.Run();
        Assert.DoesNotContain(none.Log, x => x.Mode != 0);
        Assert.Null(nr.FirstFightMs);
        Assert.False(nr.Fought);
    }

    /// <summary>
    /// DD1-R2 (a) for MELEE: "whether or not it dealt damage" — a blow whose hit roll fails is fighting at its own instant. A 1 v 1
    /// heavy-infantry meeting (seed 5, TEST INPUT found by a probe search): the two first blows, at 148,000 ms, both miss (two Fire
    /// events, no Damage event at that instant); the first damage comes at 156,000. The battle's first fighting instant is 148,000, not
    /// 156,000. (The missile counterpart, a single arrow that misses, is the boundary test's.) MEASURED.
    /// </summary>
    [Fact]
    public void DD1_R2a_AMeleeBlowThatMisses_IsFighting_AtItsOwnInstant()
    {
        var b = new TacticalBattle(MeetingAsymmetric(UnitProfiles.HeavyInfantry, 1, UnitProfiles.HeavyInfantry, 1, 5), new KnowledgeAi(), new KnowledgeAi());
        var r = TT.RunGuarded(b);
        var blows = AttackEvents(b);
        Assert.Equal((TacticalKinds.Fire, 1L, 148_000L), (blows[0].Kind, blows[0].A, blows[0].Key.TimeMs));
        Assert.Equal(2, blows.Count(e => e.Key.TimeMs == 148_000));
        var damage = Executed(b, TacticalKinds.Damage);
        Assert.DoesNotContain(damage, e => e.Key.TimeMs == 148_000);                      // both blows missed
        Assert.Equal(156_000L, damage[0].Key.TimeMs);                                     // the first that landed
        Assert.Equal(148_000L, r.FirstFightMs);
    }

    /// <summary>
    /// The report against the attack count, over lane P's TUNING battery (its 20 scenarios at seed 1), in both resolvers: the battle
    /// had fighting exactly when some formation made an attack (FirstFightMs set ⟺ Σ AttacksMade &gt; 0; the battery is land only, so it
    /// holds no bare-point bombard and no ram, the two places where the report and the count part, READINGS MOR3-R1 and MOR3-R2), and
    /// FirstFightMs is the first attack observed from outside the rule (the tactical trace; the AutoResolver's step log).
    /// </summary>
    [Fact]
    public void TheReportAgreesWithTheAttackCount_AndIsTheFirstAttack_OnLanePsTuningBattery_BothResolvers()
    {
        var cases = ParityBatteryP.TuningCases().Where(c => c.Seed == 1).ToArray();
        Assert.Equal(20, cases.Length);
        int fought = 0;
        foreach (var c in cases)
        {
            var setup = ParityBatteryP.Build(c.Scenario, c.Seed);
            var t = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
            var tr = TT.RunGuarded(t);
            var first = AttackEvents(t).Select(e => (long?)e.Key.TimeMs).FirstOrDefault();
            Assert.Equal(first, tr.FirstFightMs);
            Assert.Equal(tr.Formations.Sum(f => f.AttacksMade) > 0, tr.Fought);
            var a = new AutoBattle(setup, new AutoCoefficients()) { RecordLog = true };
            var ar = a.Run();
            long? afirst = a.Log.Where(x => x.Mode != 0).Select(x => (long?)(setup.StartMs + x.Step * setup.Rules.StepMs)).FirstOrDefault();
            Assert.Equal(afirst, ar.FirstFightMs);
            Assert.Equal(ar.Formations.Sum(f => f.AttacksMade) > 0, ar.Fought);
            if (tr.Fought) fought++;
        }
        Assert.True(fought > 0, "premise: the sample has fought battles");
    }

    /// <summary>A transport-free coast: a warship (TEST INPUT: sight 100 m, so it makes no aimed fire) and 40 riflemen of the other side at
    /// C(24, 20); the warship is ordered to bombard a ground POINT from pulse 0 step 3. In-battle part on (50).</summary>
    private static (TacticalBattle B, BattleResult R, BattleSetup S) Bombard(Vec2L point)
    {
        var s = DT.OnGrid(DT.Coast(Coast, 20), 3, null, new TacticalRules { EntryMoraleCost = Cost },
            DT.Ship(1, 0, DomainProfiles.TurretWarship with { SightMm = 100_000 }, C(10, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 40, C(24, 20)) { Morale = 800 });
        var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(1, OrderKind.Bombard, 3, -1, point))), PassiveController.Instance);
        return (b, DT.RunChecked(b, 60), s);
    }

    /// <summary>
    /// READING MOR3-R1 (a), applied (morale proposal §12.2): a bombard at a bare ground POINT is resolved against whatever its blast
    /// reaches, so it is fighting only where the blast reaches an entity or hull of an enemy formation. On the riflemen's own point the
    /// first round (6,000 ms = pulse 0, step 3: the trace's first DomainFire, aimed at no entity) is the battle's first fighting
    /// instant: 17 riflemen die, the rest rout, and the in-battle charge falls on the riflemen at that instant (the warship is a vessel,
    /// outside the in-battle charge). On an EMPTY point (C(40, 10), well clear of everybody) the warship fires 59 rounds and its attack
    /// count is 59, but no round is resolved against an enemy formation: FirstFightMs stays null, nothing is charged, and when the
    /// riflemen rout by lane TR's inactivity rule (hidden from the warship and stationary) the decisive "win" of the warship's side
    /// changes no army's morale. Alternative (b), not applied: every round counts (as the attack count does), so shelling empty ground
    /// would be fighting. MEASURED.
    /// </summary>
    [Fact]
    public void READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy()
    {
        var (hb, hr, hs) = Bombard(C(24, 20));
        var hFires = Executed(hb, DomainKinds.DomainFire);
        Assert.True(hFires[0].Target.IsNone);
        Assert.Equal(hs.StartMs + 3 * hs.Rules.StepMs, hFires[0].Key.TimeMs);
        Assert.Equal(hFires[0].Key.TimeMs, hr.FirstFightMs);
        Assert.Equal(new[] { new EntryCharge(6_000, 2, 800, Cost) }, hb.EntryCharges.ToArray());
        Assert.Equal(17, hr.Formations.Single(f => f.FormationId == 2).Killed);

        var (eb, er, es) = Bombard(C(40, 10));
        var eFires = Executed(eb, DomainKinds.DomainFire);
        Assert.Equal(59, eFires.Length);
        Assert.All(eFires, e => Assert.True(e.Target.IsNone));                         // every round at the point
        Assert.Equal(59, er.Formations.Single(f => f.FormationId == 1).AttacksMade);   // the attack count says 59 attacks...
        Assert.Null(er.FirstFightMs);                                                   // ...none resolved against an enemy formation
        Assert.Empty(eb.EntryCharges);
        Assert.Equal((BattleEndReason.SideEliminated, 1, 0), (er.Reason, er.Side, BattleWinner.Reported(er)));
        var f2 = er.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((0L, 40L, 0L), (f2.Killed, f2.RoutedEscaped, f2.DamageTaken));
        // the decisive "win" over riflemen who fled shelling that never reached them changes neither army's morale
        var col = BattleCollapse.FromTactical(eb, es, new[] { (1L, 0), (2L, 1) });                // in formation-id order: the warship, the riflemen
        var loser = Settle(er, 2, 1, col[1], 800, beaten: 40, winnerSide: 200);
        Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (loser.Fought, loser.EntryPpm, loser.DecreasePpm, loser.RisePpm, loser.AfterPpm));
        var winner = Settle(er, 1, 0, col[0], 800, beaten: 40, winnerSide: 200);
        Assert.Equal((0, (byte)0, 0L, 0L, 800_000L), (winner.WinnerSide, winner.Fought, winner.EntryPpm, winner.RisePpm, winner.AfterPpm));
    }

    /// <summary>The warship of <see cref="Bombard"/> (sight 100 m) bombards the sea POINT C(12, 5), where an unarmed oared transport of side
    /// <paramref name="transportSide"/> lies (formation 3); ten riflemen of side 1 stand far inland at C(60, 38). TEST INPUT.</summary>
    private static (TacticalBattle B, BattleResult R) BombardAHull(int transportSide)
    {
        var s = DT.OnGrid(DT.Coast(Coast, 20), 3, null, new TacticalRules { EntryMoraleCost = Cost },
            DT.Ship(1, 0, DomainProfiles.TurretWarship with { SightMm = 100_000 }, C(10, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(60, 38)) { Morale = 800 },
            DT.Ship(3, transportSide, DomainProfiles.OaredTransport with { Weapons = Array.Empty<WeaponSpec>() }, C(12, 5)));
        var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(1, OrderKind.Bombard, 3, -1, C(12, 5)))), PassiveController.Instance);
        return (b, DT.RunChecked(b, 60));
    }

    /// <summary>
    /// READING MOR3-R1 (a) for HULLS (morale proposal §12.2): a bare-point round whose blast reaches the hull of an ENEMY formation is
    /// fighting; one whose blast reaches only a FRIENDLY hull is not (DD1-R2 (a): "an attack resolved against an enemy formation"). The
    /// warship shells the point where a transport lies. Enemy transport: the battle's first fighting instant is the first round's
    /// (6,000 ms; the trace's first DomainFire, aimed at no entity). Friendly transport: the rounds hit it (friendly fire: hull hits on
    /// it in the trace) and still no attack was resolved against an enemy formation, so the battle has no fighting. (A friendly LAND
    /// entity is never in a bare-point blast: the danger-close rule refuses such a round — CITED, DangerClose — so the land half needs no
    /// such case.) MEASURED.
    /// </summary>
    [Fact]
    public void READING_MOR3_R1_ABarePointBlastOnAnEnemyHull_IsFighting_OnAFriendlyHull_IsNot()
    {
        var (eb, er) = BombardAHull(1);
        var eFires = Executed(eb, DomainKinds.DomainFire);
        Assert.True(eFires[0].Target.IsNone);
        Assert.Equal(6_000L, eFires[0].Key.TimeMs);
        Assert.Equal(6_000L, er.FirstFightMs);

        var (fb, fr) = BombardAHull(0);
        var fFires = Executed(fb, DomainKinds.DomainFire);
        Assert.NotEmpty(fFires);
        Assert.All(fFires, e => Assert.True(e.Target.IsNone));                         // every round at the point
        Assert.Contains(Executed(fb, DomainKinds.DomainHit), e => e.Target.Id == 3000); // friendly fire on the transport's hull
        Assert.Null(fr.FirstFightMs);
    }

    /// <summary>
    /// READING MOR3-R2 (a), applied (morale proposal §12.2): a bow contact that is not a ram is not an attack. Lane RAM's RAM2-V2 battle
    /// (a blind, unarmed rammer lying still is run into by a fast unarmed hulk): the only contact is a collision at 60,000 ms with no
    /// normal speed (RAM2-V2: "no ram is credited ... nobody is damaged"); the engine counts no attack for it. FirstFightMs is null at
    /// the completed instant of the collision and at the battle's end (a Standoff at 360,000 ms). Alternative (b), not applied: the ram
    /// attempt resolved as a contact counts, and the battle would be fought from 60,000 ms. MEASURED.
    /// </summary>
    [Fact]
    public void READING_MOR3_R2_ARamCollisionAlone_IsNoFighting()
    {
        var rammer = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), SightMm = 1 };
        var hulk = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0, Hull = 1000, AccelMmPerS2 = 2_300 };
        const long X0 = 1_000_000, Yc = 1_025_000, Contact = 16_000;
        var s = new BattleSetup(MultiDomainScenarios.OpenSea(50_000), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                                new[] { new SideSpec(ExitEdge.West), new SideSpec(ExitEdge.North) },
                                new[] { DT.Ship(1, 0, rammer, new Vec2L(X0, Yc)), DT.Ship(2, 1, hulk, new Vec2L(X0 + Contact + 200_000, Yc)) }, 4)
                { MultiDomain = new MultiDomainSetup { Rules = new DomainRules() } };
        var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))),
                                   DT.Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, new Vec2L(X0 - 600_000, Yc)))));
        long? seenAt = null, firstFightThen = -1;
        var r = DT.RunChecked(b, 200, () =>
        {
            if (seenAt is null && Executed(b, DomainKinds.RamCollision).Length > 0) { seenAt = b.Scheduler.NowMs; firstFightThen = b.FirstFightMs; }
        });
        var coll = Assert.Single(Executed(b, DomainKinds.RamCollision));
        Assert.Equal((60_000L, 0L), (coll.Key.TimeMs, coll.B));                             // the collision, no normal speed
        Assert.Equal(60_000L, seenAt);
        Assert.Null(firstFightThen);                                                       // not fighting at its own instant
        Assert.Empty(AttackEvents(b));
        Assert.Null(r.FirstFightMs);
        Assert.Equal((BattleEndReason.Standoff, 360_000L), (r.Reason, r.EndMs));
        Assert.All(r.Formations, f => Assert.Equal(0, f.AttacksMade));
    }

    // ================================================================== 2. THE SETTLEMENT (DD1, DD1-R1 (a))

    /// <summary>
    /// THE BOUNDARY (D-085 §4: "exactly one attack against none"), both resolvers, through the hand-off and lane D's settlement. The
    /// single-shot archer at column 22 looses ONE arrow at the start (seed 2: it misses — the trace holds one Fire event and no Damage
    /// event) and nothing else happens until lane TR's Standoff ends the battle with no winner: the battle WAS fought (DD1-R2 (a):
    /// "whether or not it dealt damage"), so both armies pay the entry cost: 800,000 → 750,000 each (no loss, no winner). At column 21
    /// the archer never has the infantry in range: no attack at all, so neither army's morale changes (800,000; Fought 0 on the row). The
    /// AutoResolver gives the same: one volley at step 0 against none. And TURN-EXACT where the instant matters (the in-battle part on):
    /// the delayed single shot (closing from column 16 on an Attack order at pulse 2) falls at 214,000 ms — the trace's one Fire event —
    /// and the in-battle charge falls on both formations exactly then, not a step earlier: each formation's morale at the completed
    /// instant before equals the same battle's with the part off. MEASURED.
    /// </summary>
    [Fact]
    public void DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers()
    {
        var map = new[] { (1L, 0), (2L, 1) };
        // TacticalResolver: one missed arrow
        var s1 = Shot(2, 22, 0);
        var b1 = new TacticalBattle(s1, PassiveController.Instance, PassiveController.Instance);
        var r1 = TT.RunGuarded(b1);
        var one = Assert.Single(AttackEvents(b1));
        Assert.Equal((TacticalKinds.Fire, 2L, 0L), (one.Kind, one.A, one.Key.TimeMs));
        Assert.Empty(Executed(b1, TacticalKinds.Damage));
        Assert.Equal((0L, BattleEndReason.Standoff, -1), (r1.FirstFightMs, r1.Reason, BattleWinner.Reported(r1)));
        var c1 = BattleCollapse.FromTactical(b1, s1, map);
        foreach (var (fid, army) in new[] { (1L, 0), (2L, 1) })
        {
            var m = Settle(r1, fid, army, c1[army], 800, beaten: 0, winnerSide: 0);
            Assert.Equal(((byte)1, CostPpm, 0L, 0L, 750_000L), (m.Fought, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
        }
        // TacticalResolver: no attack
        var s0 = Shot(2, 21, 0);
        var b0 = new TacticalBattle(s0, PassiveController.Instance, PassiveController.Instance);
        var r0 = TT.RunGuarded(b0);
        Assert.Empty(AttackEvents(b0));
        Assert.Null(r0.FirstFightMs);
        Assert.Equal(BattleEndReason.Standoff, r0.Reason);
        var c0 = BattleCollapse.FromTactical(b0, s0, map);
        foreach (var (fid, army) in new[] { (1L, 0), (2L, 1) })
        {
            var m = Settle(r0, fid, army, c0[army], 800, beaten: 0, winnerSide: 0);
            Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (m.Fought, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
        }
        // AutoResolver: one volley against none
        foreach (var (col, fought) in new[] { (22L, true), (21L, false) })
        {
            var a = new AutoBattle(Shot(2, col, 0), new AutoCoefficients()) { RecordLog = true };
            var ar = a.Run();
            Assert.Equal(fought ? 1 : 0, a.Log.Count(x => x.Mode != 0));
            Assert.Equal(fought ? (long?)0L : null, ar.FirstFightMs);
            foreach (var (fid, army) in new[] { (1L, 0), (2L, 1) })
            {
                var m = Settle(ar, fid, army, FromAuto(ar.Formations.Single(x => x.FormationId == fid), army, 1000), 800, beaten: 0, winnerSide: 0);
                Assert.Equal(fought ? (byte)1 : (byte)0, m.Fought);
                Assert.Equal(fought ? 750_000L : 800_000L, m.AfterPpm);
            }
        }
        // turn-exact: the delayed single shot, in-battle part on, against the same battle with it off (completed instants in step)
        var off = new TacticalBattle(Delayed(2, 0, 800), AttackAtPulse2(), new KnowledgeAi());
        var on = new TacticalBattle(Delayed(2, Cost, 800), AttackAtPulse2(), new KnowledgeAi());
        long shotAt = -1;
        off.Start(); on.Start();
        while (!off.IsOver && (shotAt < 0 || off.Scheduler.NowMs <= shotAt))
        {
            if (off.Scheduler.TryPeekKey(out var k) && k.TimeMs > off.Scheduler.NowMs)
            {
                while (!on.IsOver && on.Scheduler.TryPeekKey(out var k2) && k2.TimeMs <= off.Scheduler.NowMs) on.Advance();
                Assert.Equal(off.Scheduler.NowMs, on.Scheduler.NowMs);
                if (shotAt < 0 && AttackEventsSoFar(off).Length > 0) shotAt = off.Scheduler.NowMs;
                long charge = shotAt >= 0 ? Cost : 0;                                     // nothing before the shot's instant; the charge at it
                Assert.Equal((off.Morale(1) - charge, off.Morale(2) - charge), (on.Morale(1), on.Morale(2)));
            }
            off.Advance();
        }
        Assert.Equal(214_000L, shotAt);
        Assert.Equal(new[] { new EntryCharge(214_000, 1, 1000, Cost), new EntryCharge(214_000, 2, 860, Cost) }, on.EntryCharges.ToArray());
        Assert.Equal(214_000L, on.FirstFightMs);
        // [MOR3, verification round 1, MOR3-R1-01] the oracle read while the battle ran, validated on the finished battle
        while (off.Advance()) Assert.True(off.CurrentPulse <= TT.HarnessMaxPulses, "harness guard: non-termination is a FAILURE");
        Assert.Equal(shotAt, AttackEvents(off)[0].Key.TimeMs);
    }

    /// <summary>The setup of an unfought decisive win: formation 1 (side 0) at <paramref name="m1"/>, below the TacticalResolver's rout
    /// threshold and the AutoResolver's withdrawal line, so it leaves at once; formation 2 (side 1) 34 columns away at 800. TEST INPUT.</summary>
    private static BattleSetup Distant(long m1, long cost = 0) => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
        DefaultSides(new[] { Cell(40, 16) }, new[] { Cell(6, 16) }),
        new[] { new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(6, 16)) { Morale = m1 }, new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(40, 16)) { Morale = 800 } },
        3) { Rules = new TacticalRules { EntryMoraleCost = cost } };

    /// <summary>
    /// DD1: "There is no rise for its reported winner" — and not only for an equal-sides "win" (D-085 §3: the alternative (c′) "is
    /// adopted in a wider form"). An UNFOUGHT DECISIVE win: formation 1 starts at 250 and leaves at once (the TacticalResolver routs it,
    /// the AutoResolver withdraws it in good order), no blow is struck, and the battle ends SideEliminated with side 1 the decisive
    /// winner — in both resolvers. Through the hand-off: the loser's army (250,000) and the winner's (800,000) are unchanged; the
    /// winner's row still records the reported winner (WinnerSide 1) with Fought 0 and no rise. Lane MOR2 measured the AutoResolver
    /// form of this case (one column off symmetry) as a decisive win of +150,000 for its army (H-MOR2 §14.2); under DD1 it is 0.
    /// </summary>
    [Fact]
    public void DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers()
    {
        var setup = Distant(250);
        var t = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var tr = TT.RunGuarded(t);
        var a = new AutoBattle(setup, new AutoCoefficients()).Run();
        foreach (var (r, c) in new[]
                 {
                     (tr, BattleCollapse.FromTactical(t, setup, new[] { (1L, 0), (2L, 1) })),
                     (a, new[] { FromAuto(a.Formations.Single(x => x.FormationId == 1), 0, 1000), FromAuto(a.Formations.Single(x => x.FormationId == 2), 1, 1000) }),
                 })
        {
            Assert.Equal((BattleEndReason.SideEliminated, 0, 1, WinBasis.Decisive), (r.Reason, r.Side, BattleWinner.Reported(r), r.WinBasis));
            Assert.Null(r.FirstFightMs);
            Assert.All(r.Formations, f => Assert.Equal((0L, 0L, 0L), (f.AttacksMade, f.DamageDealt, f.Killed)));
            var loser = Settle(r, 1, 0, c[0], 250, beaten: 20, winnerSide: 20);
            Assert.Equal(((byte)0, 0L, 0L, 0L, 250_000L), (loser.Fought, loser.EntryPpm, loser.DecreasePpm, loser.RisePpm, loser.AfterPpm));
            var winner = Settle(r, 2, 1, c[1], 800, beaten: 20, winnerSide: 20);
            Assert.Equal((1, (byte)0, 0L, 0L, 800_000L), (winner.WinnerSide, winner.Fought, winner.EntryPpm, winner.RisePpm, winner.AfterPpm));
        }
    }

    /// <summary>
    /// READING DD1-R1 (a), applied: fighting is read per BATTLE. Lane MOR2's three-formation battle with a fourth formation (side 0,
    /// ten heavy infantry on Defend at column 2) that never comes within anyone's reach: it strikes no blow and takes none (attacks 0,
    /// damage 0 both ways), yet it entered at the start and the battle had fighting (first fighting instant 7,332,000), so its army
    /// pays the entry cost (D-083 DB1: "Simply entering battke should cost morale") — and, its side having won (side 1 eliminated), the
    /// rise, as every army whose formation entered (READING MOR2-R1 (a)): 800,000 − 50,000 + ⌊400,000 × 40 ÷ (40 + 60)⌋ = 910,000; its
    /// morale ended above its start (holding, it recovered), so DR29 takes nothing. Under the alternative (b), per formation, it would
    /// settle as unfought (800,000). In both resolvers. MEASURED and by hand.
    /// </summary>
    [Fact]
    public void DD1_R1a_AFormationThatEnteredAFoughtBattleWithoutStrikingABlow_PaysTheEntryCost_BothResolvers()
    {
        var b3 = EntryCostTests.ThreeFormations(1, 0);
        var setup = b3 with { Formations = b3.Formations.Append(new FormationSpec(4, 0, UnitProfiles.HeavyInfantry, 10, Cell(2, 16)) { Morale = 800, Posture = Posture.Defend }).ToArray() };
        var t = new TacticalBattle(setup, new KnowledgeAi { RetreatMorale = 0 }, new KnowledgeAi { RetreatMorale = 0 });
        var tr = TT.RunGuarded(t);
        var ar = new AutoBattle(setup, new AutoCoefficients { DoctrineRetreatMorale = 0 }).Run();
        foreach (var (r, c) in new[]
                 {
                     (tr, BattleCollapse.FromTactical(t, setup, new[] { (1L, 1), (2L, 2), (3L, 3), (4L, 0) })[3]),   // formation-id order: 4 is last
                     (ar, FromAuto(ar.Formations.Single(x => x.FormationId == 4), 0, 1000)),
                 })
        {
            var f4 = r.Formations.Single(x => x.FormationId == 4);
            Assert.Equal((0L, 0L, 0L, (long?)setup.StartMs), (f4.AttacksMade, f4.DamageDealt, f4.DamageTaken, f4.EnteredAtMs));
            Assert.True(f4.MoraleEnd >= 800);
            Assert.Equal(7_332_000L, r.FirstFightMs);
            Assert.Equal((1, 0), (r.Side, BattleWinner.Reported(r)));
            Assert.True(BattleFighting.SettlesAsFought(r, 4));                               // the battle's answer, not the formation's
            var m = Settle(r, 4, 0, c, 800, beaten: 40, winnerSide: 60);
            Assert.Equal(((byte)1, (byte)1, CostPpm, 0L, 160_000L, 910_000L), (m.Fought, m.Entered, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
        }
    }

    /// <summary>
    /// DD1 at the settlement, by hand, on lane D's stand-in: a battle without fighting changes the army's campaign morale by NOTHING,
    /// whatever its report says — entered, an end morale below its start (no resolver produces that without fighting, DERIVED: without
    /// an attack there is no casualty, and the in-battle charge falls only at the first fighting instant; the settlement does not rely
    /// on it), and its own side named the winner over an equal force. The row records Fought 0, entry 0, decrease 0, rise 0 and after =
    /// before, and still the reported winner. The same battle fought: entry 50,000, decrease 100,000, rise 200,000 (850,000). A battle
    /// that never entered and had no fighting: unchanged. And the stand-in refuses an in-battle entry charge for a battle without
    /// fighting (the in-battle part charges only at the first fighting instant). CampaignMorale.IfFought by hand.
    /// </summary>
    [Fact]
    public void DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport()
    {
        Assert.Equal((0L, 200_000L, 0L), (CampaignMorale.IfFought(false, 200_000), CampaignMorale.IfFought(true, 200_000), CampaignMorale.IfFought(true, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMorale.IfFought(false, -1));
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(3), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, 2_000, 2_000) with { Fought = false });
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 700, 1, 2_000, 2_000));
        rig.W.ScheduleBattle(rig.S, MoraleRig.NeverEntered(2, 0, at, -1, 0, 0) with { Fought = false });
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, -1, 0, 0) with { Fought = false, EntryChargedInBattle = Cost }));
        MoraleRig.Run(rig, at + 1);
        var rows = rig.W.MoraleRows.ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(((byte)0, (byte)1, 0, 700L, 0L, 0L, 0L, 800_000L, 800_000L),
            (rows[0].Fought, rows[0].Entered, rows[0].WinnerSide, rows[0].MoraleEnd, rows[0].EntryPpm, rows[0].DecreasePpm, rows[0].RisePpm, rows[0].BeforePpm, rows[0].AfterPpm));
        Assert.Equal(((byte)1, CostPpm, 100_000L, 200_000L, 850_000L), (rows[1].Fought, rows[1].EntryPpm, rows[1].DecreasePpm, rows[1].RisePpm, rows[1].AfterPpm));
        Assert.Equal(((byte)0, (byte)0, 800_000L), (rows[2].Fought, rows[2].Entered, rows[2].AfterPpm));
        Assert.Equal(new[] { 800_000L, 850_000L, 800_000L }, Enumerable.Range(0, 3).Select(a => rig.W.Economy.MoraleOf(a)).ToArray());
        Assert.Equal(1, MoraleRig.Battle(0, 0, at, 700, -1, 0, 0).Fought ? 1 : 0);              // the stand-in's default: fought
    }

    /// <summary>The five battles of the tie-dense test: fought and unfought, winners and losers, an in-battle charge.</summary>
    private static SupportBattle[] MixedBattles(long at) => new[]
    {
        MoraleRig.Battle(0, 0, at, 700, 0, 4_000, 4_000),                                            // fought, won: 850,000
        MoraleRig.Battle(1, 1, at, 840, 1, 4_000, 4_000) with { Fought = false },                     // unfought "win": 800,000
        MoraleRig.Battle(2, 0, at, 300, 1, 4_000, 4_000),                                            // fought, lost: 250,000
        MoraleRig.Battle(3, 1, at, 820, 0, 4_000, 4_000) with { Fought = false },                     // unfought loss: 800,000
        MoraleRig.Battle(4, 0, at, 650, -1, 0, 0) with { EntryChargedInBattle = Cost },               // fought, charged in battle: 650,000
    };

    /// <summary>
    /// TIE-DENSE (CLAUDE.md): five armies settle at ONE instant — fought and unfought battles mixed, a fought and an unfought "win", a
    /// fought and an unfought loss, an in-battle charge — scheduled in all 120 orders: the rows come out in army-id order (the event key's
    /// stable integer tie-break), with the same results by hand and the same state hash whatever the order. And one army with a fought
    /// and an unfought battle at the same instant, in both scheduling orders: the unfought battle is the identity (its row's after equals
    /// its before), so the order does not matter at all and the army ends at the fought battle's 650,000 either way.
    /// </summary>
    [Fact]
    public void DD1_TieDense_FoughtAndUnfoughtBattles_ManyArmiesAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        string? hash = null;
        int orders = 0;
        foreach (var order in Permutations(5))
        {
            var rig = MoraleRig.Build(MoraleRig.Armies(5), start);
            var battles = MixedBattles(at);
            foreach (int i in order) rig.W.ScheduleBattle(rig.S, battles[i]);
            MoraleRig.Run(rig, at + 1);
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, rig.W.MoraleRows.Select(x => x.ArmyId).ToArray());
            Assert.Equal(new[] { 850_000L, 800_000L, 250_000L, 800_000L, 650_000L }, Enumerable.Range(0, 5).Select(a => rig.W.Economy.MoraleOf(a)).ToArray());
            Assert.Equal(new byte[] { 1, 0, 1, 0, 1 }, rig.W.MoraleRows.Select(x => x.Fought).ToArray());
            Assert.Equal(new[] { 200_000L, 0L, 0L, 0L, 0L }, rig.W.MoraleRows.Select(x => x.RisePpm).ToArray());
            hash ??= rig.W.StateHashHex();
            Assert.Equal(hash, rig.W.StateHashHex());
            orders++;
        }
        Assert.Equal(120, orders);
        foreach (bool unfoughtFirst in new[] { true, false })
        {
            var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
            var fought = MoraleRig.Battle(0, 0, at, 700, -1, 0, 0);
            var unfought = MoraleRig.Battle(0, 0, at, 800, 0, 2_000, 2_000) with { Fought = false };
            foreach (var b in unfoughtFirst ? new[] { unfought, fought } : new[] { fought, unfought }) rig.W.ScheduleBattle(rig.S, b);
            MoraleRig.Run(rig, at + 1);
            Assert.Equal(650_000L, rig.W.Economy.MoraleOf(0));
            var u = rig.W.MoraleRows.Single(x => x.Fought == 0);
            Assert.Equal(u.BeforePpm, u.AfterPpm);
        }
    }

    /// <summary>
    /// POPULATED-TABLE TEST (CLAUDE.md) of the version-3 settlement rows (MoraleSettlementRow + Fought): six real rows of the world —
    /// fought and unfought, entered and never entered, a rise, an in-battle charge, a clamp at 0 — encoded with the exact
    /// ExpectedLength 5 + 111 × rows, decoded bit-exactly, re-encoded to the same bytes and SHA-256, equal to the world's own encoded
    /// table, every same-typed field pair told apart (lane D's field-swap guard: the bytes Side, Entered and Fought differ pairwise in
    /// some row). A version-2 table and a Fought byte other than 0 or 1 are refused.
    /// </summary>
    [Fact]
    public void DD1_SettlementRows_Version3_PopulatedTable_RoundTripBitExact_WithExactLength()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(5), start);
        foreach (var b in MixedBattles(at)) rig.W.ScheduleBattle(rig.S, b);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(2, 0, at + 5 * Day + 3, 0, 1, 6_000, 3_000));         // the clamp at 0 (250,000 − 50,000 − 800,000)
        MoraleRig.Run(rig, at + 6 * Day);
        var rows = rig.W.MoraleRows.ToArray();
        Assert.Equal(6, rows.Length);
        Assert.Contains(rows, x => x.Fought == 0 && x.Entered == 1 && x.WinnerSide == x.Side);
        Assert.Contains(rows, x => x.AfterPpm == 0 && x.BeforePpm > 0);
        Assert.Equal(3, MoraleCodec.Version);
        var bytes = MoraleCodec.Encode(rows);
        Assert.Equal(MoraleCodec.ExpectedLength(rows.Length), bytes.Length);
        Assert.Equal(5 + 111 * rows.Length, bytes.Length);
        var back = MoraleCodec.Decode(bytes);
        Assert.Equal(rows, back);
        Assert.Equal(bytes, MoraleCodec.Encode(back));
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(MoraleCodec.Encode(back)));
        Assert.Equal(bytes, rig.W.EncodedTables()[4]);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(rows);
        var v2 = (byte[])bytes.Clone(); v2[0] = 2;
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(v2));
        var badFought = (byte[])bytes.Clone(); badFought[MoraleCodec.HeaderBytes + 110] = 2;           // row 0's Fought (its last byte)
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(badFought));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoraleCodec.Encode(new[] { rows[0] with { Fought = 2 } }));
    }

    /// <summary>BattleFighting reads the battle (DD1-R1 (a)): the same answer for every formation of a battle, the report's
    /// FirstFightMs; a formation that is not the battle's is refused.</summary>
    [Fact]
    public void BattleFighting_ReadsTheBattleNotTheFormation_AndRefusesAStranger()
    {
        var fr = new[]
        {
            new FormationResult(1, 0, Proto.M7.Tactical.Domain.Land, 1, 10, 10, 0, 0, 10, 0, 0, 0, 800, 0, 0, 0),
            new FormationResult(2, 1, Proto.M7.Tactical.Domain.Land, 1, 10, 10, 0, 0, 10, 0, 0, 0, 800, 5, 0, 1),
        };
        var fought = new BattleResult(ResolverKind.Tactical, BattleEndReason.Standoff, -1, -1, 1, 0, 0, 0, "", "", fr) { FirstFightMs = 4_000 };
        var unfought = fought with { FirstFightMs = null };
        Assert.True(BattleFighting.SettlesAsFought(fought, 1));                               // formation 1 made no attack
        Assert.True(BattleFighting.SettlesAsFought(fought, 2));
        Assert.False(BattleFighting.SettlesAsFought(unfought, 2));                            // formation 2's count says 1; the report says no
        Assert.Throws<ArgumentException>(() => BattleFighting.SettlesAsFought(fought, 3));
    }

    // ================================================================== 3. THE IN-BATTLE PART (PROPOSAL C1)

    /// <summary>
    /// "The in-battle part, when switched on in a test, changes nothing before the first fighting instant" (brief), TURN-EXACT, both
    /// resolvers. Lane MOR2's three-formation battle run twice, with the part off and on (50), side by side: at every completed instant
    /// before 7,332,000 ms — the battle's first attack, read from the trace of the run with the part OFF — both runs have the same
    /// morale for every formation and the same entities; at 7,332,000 each formation's morale is lower by exactly the charge (900 / 1000 /
    /// 640 against 850 / 950 / 590: the archers had recovered from 600 while holding). The AutoResolver: its step logs are identical record
    /// for record before step 66 (7,332,000 ms, its first fighting step), and at step 66 every formation's record differs only by its
    /// morale, lower by 50. MEASURED.
    /// </summary>
    [Fact]
    public void C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers()
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var off = new TacticalBattle(EntryCostTests.ThreeFormations(1, 0), ai, ai);
        var on = new TacticalBattle(EntryCostTests.ThreeFormations(1, Cost), ai, ai);
        off.Start(); on.Start();
        long? firstAttack = null;
        int instants = 0;
        while (!off.IsOver && !on.IsOver)
        {
            if (off.Scheduler.TryPeekKey(out var k) && k.TimeMs > off.Scheduler.NowMs)
            {
                while (!on.IsOver && on.Scheduler.TryPeekKey(out var k2) && k2.TimeMs <= off.Scheduler.NowMs) on.Advance();
                Assert.Equal(off.Scheduler.NowMs, on.Scheduler.NowMs);
                firstAttack ??= AttackEventsSoFar(off).Select(e => (long?)e.Key.TimeMs).FirstOrDefault();
                long now = off.Scheduler.NowMs;
                if (firstAttack is null)
                {
                    Assert.Equal((off.Morale(1), off.Morale(2), off.Morale(3)), (on.Morale(1), on.Morale(2), on.Morale(3)));
                    Assert.Equal(off.Entities().ToArray(), on.Entities().ToArray());
                    Assert.Empty(on.EntryCharges);
                    instants++;
                }
                else
                {
                    Assert.Equal(7_332_000L, now);
                    Assert.Equal((900L, 1000L, 640L), (off.Morale(1), off.Morale(2), off.Morale(3)));
                    Assert.Equal((850L, 950L, 590L), (on.Morale(1), on.Morale(2), on.Morale(3)));
                    break;
                }
            }
            off.Advance();
        }
        Assert.Equal(7_332_000L, firstAttack);
        Assert.True(instants >= 66, $"premise: the first fight came late ({instants} completed instants before it)");
        // [MOR3, verification round 1, MOR3-R1-01] the oracle read while the battle ran, validated on the finished battle
        while (off.Advance()) Assert.True(off.CurrentPulse <= TT.HarnessMaxPulses, "harness guard: non-termination is a FAILURE");
        Assert.Equal(firstAttack, AttackEvents(off)[0].Key.TimeMs);
        var aOff = new AutoBattle(EntryCostTests.ThreeFormations(1, 0), new AutoCoefficients { DoctrineRetreatMorale = 0 }) { RecordLog = true };
        var aOn = new AutoBattle(EntryCostTests.ThreeFormations(1, Cost), new AutoCoefficients { DoctrineRetreatMorale = 0 }) { RecordLog = true };
        Assert.Equal(7_332_000L, aOff.Run().FirstFightMs);
        aOn.Run();
        int firstStep = (int)aOff.Log.First(x => x.Mode != 0).Step;
        Assert.Equal(66, firstStep);
        for (int i = 0; i < aOff.Log.Count && aOff.Log[i].Step <= firstStep; i++)
        {
            var x0 = aOff.Log[i]; var x1 = aOn.Log[i];
            Assert.Equal(x0.Step < firstStep ? x0 : x0 with { Morale = x0.Morale - Cost }, x1);
        }
    }

    /// <summary>
    /// C1, WHERE IN THE INSTANT (proposal §12.4): the charge falls at the start of the instant's resolution, before its rout check, so a
    /// formation charged below the rout threshold (300) routs AT the battle's first fighting instant, not at a later one. The delayed
    /// single arrow (it misses) against five heavy infantry at 320 that hold with no orders (PassiveController) and no recovery
    /// (HoldRecovery 0, TEST INPUT): at every completed instant before the arrow both runs have the infantry Active at 320; at the
    /// arrow's instant the run with the part on has it at 270 and routed — the rout leaves side 1 without an Active entity, so the
    /// battle ends at that same instant and its five routed men leave as routed escapes (Escaped; RoutedEscaped 5 in the result) — the
    /// run with the part off at 320 and Active. Charged after the rout check, the formation would still be Active at that instant.
    /// TURN-EXACT, MEASURED.
    /// </summary>
    [Fact]
    public void C1_TheChargeActsOnTheRoutCheckOfItsOwnInstant_TurnExact()
    {
        var off = new TacticalBattle(Delayed(2, 0, 320, hold: 0), AttackAtPulse2(), PassiveController.Instance);
        var on = new TacticalBattle(Delayed(2, Cost, 320, hold: 0), AttackAtPulse2(), PassiveController.Instance);
        EntityState[] States(TacticalBattle b) => b.Entities().Where(e => e.FormationId == 2).Select(e => e.State).ToArray();
        long shotAt = -1;
        int before = 0;
        off.Start(); on.Start();
        while (!off.IsOver && shotAt < 0)
        {
            if (off.Scheduler.TryPeekKey(out var k) && k.TimeMs > off.Scheduler.NowMs)
            {
                while (!on.IsOver && on.Scheduler.TryPeekKey(out var k2) && k2.TimeMs <= off.Scheduler.NowMs) on.Advance();
                Assert.Equal(off.Scheduler.NowMs, on.Scheduler.NowMs);
                if (AttackEventsSoFar(off).Length > 0) shotAt = off.Scheduler.NowMs;
                if (shotAt < 0)
                {
                    Assert.Equal((320L, 320L), (off.Morale(2), on.Morale(2)));
                    Assert.All(States(on), s => Assert.Equal(EntityState.Active, s));
                    before++;
                }
                else
                {
                    Assert.Equal((320L, 270L), (off.Morale(2), on.Morale(2)));
                    Assert.All(States(off), s => Assert.Equal(EntityState.Active, s));
                    var gone = States(on);
                    Assert.Equal(5, gone.Length);
                    Assert.All(gone, s => Assert.Equal(EntityState.Escaped, s));        // routed; the battle ended at this instant
                }
            }
            if (shotAt < 0) off.Advance();
        }
        Assert.Equal(214_000L, shotAt);
        Assert.True(before > 0);
        Assert.Equal(new EntryCharge(214_000, 2, 320, Cost), on.EntryCharges.Single(x => x.FormationId == 2));
        while (on.Advance()) Assert.True(on.CurrentPulse <= DT.HarnessMaxPulses, "harness guard: non-termination");
        Assert.Equal((5L, 0L, 270L), (on.Result!.Formations.Single(f => f.FormationId == 2).RoutedEscaped,
                                      on.Result!.Formations.Single(f => f.FormationId == 2).Killed, on.Result!.Formations.Single(f => f.FormationId == 2).MoraleEnd));
        // [MOR3, verification round 1, MOR3-R1-01] the oracle read while the battle ran, validated on the finished battle
        while (off.Advance()) Assert.True(off.CurrentPulse <= TT.HarnessMaxPulses, "harness guard: non-termination is a FAILURE");
        Assert.Equal(shotAt, AttackEvents(off)[0].Key.TimeMs);
    }

    /// <summary>The unfought mutual rout of lane MOR2's round 2: two formations of 20 heavy infantry at <paramref name="s"/>, side 0 at
    /// column 6 and side 1 at <paramref name="c1"/>, side 1 the defender (D-082 DA6). TEST INPUT.</summary>
    private static BattleSetup Apart(long s, long c1, long cost) => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
        DefaultSides(new[] { Cell(c1, 16) }, new[] { Cell(6, 16) }),
        new[]
        {
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(6, 16)) { Morale = s },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(c1, 16)) { Morale = s },
        },
        3) { Rules = new TacticalRules { EntryMoraleCost = cost }, Defender = 1 };

    /// <summary>
    /// D-085 §4: the in-battle part "must not change morale in a battle that turns out to have no fighting". With the part ON (50), the
    /// unfought battles of lane MOR2's round 2 are the SAME battles as with it off — the same result hash and the same event trace, no
    /// entry row, nothing charged: in the TacticalResolver the mutual rout at 320, 349 and 500 (lane MOR2 measured 320 and 349 routing at
    /// once because the charge took them below 300 at the start, and 500 ending at 550), in the AutoResolver the mirrored mutual withdrawal
    /// at 250 and 449. MEASURED.
    /// </summary>
    [Fact]
    public void C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers()
    {
        foreach (long s in new[] { 320L, 349L, 500L })
        {
            var off = TT.RunGuarded(new TacticalBattle(Apart(s, 40, 0), PassiveController.Instance, PassiveController.Instance));
            var onB = new TacticalBattle(Apart(s, 40, Cost), PassiveController.Instance, PassiveController.Instance);
            var on = TT.RunGuarded(onB);
            Assert.Null(on.FirstFightMs);
            Assert.Equal((off.ResultHash(), off.TraceHash), (on.ResultHash(), on.TraceHash));
            Assert.Empty(onB.EntryCharges);
            Assert.All(on.Formations, f => Assert.Equal((0L, s + 5 * 20), (f.EntryCharged, f.MoraleEnd)));   // five quiet pulses of recovery
        }
        foreach (long s in new[] { 250L, 449L })
        {
            var off = new AutoBattle(Apart(s, MirrorColumn(6), 0), new AutoCoefficients()).Run();
            var onB = new AutoBattle(Apart(s, MirrorColumn(6), Cost), new AutoCoefficients());
            var on = onB.Run();
            Assert.Null(on.FirstFightMs);
            Assert.Equal((off.ResultHash(), off.TraceHash), (on.ResultHash(), on.TraceHash));
            Assert.Empty(onB.EntryCharges);
            Assert.All(on.Formations, f => Assert.Equal((0L, s), (f.EntryCharged, f.MoraleEnd)));
        }
    }

    /// <summary>Lane N's shore (EntryCostTests.Shore) made a FOUGHT battle: beside the transport (1) with the riflemen (2, 20, morale 800)
    /// aboard, a single-shot archer (3, side 0, at <paramref name="archerCol"/>) and five heavy infantry (9, side 1, Defend) at C(62, 2).
    /// The in-battle part on (50). TEST INPUT.</summary>
    private static BattleSetup FoughtShore(long archerCol) => DT.OnGrid(DT.Coast(Coast, 20), 1, null, new TacticalRules { EntryMoraleCost = Cost },
        DT.Ship(1, 0, DomainProfiles.SteamTransport with { Capacity = 30 }, C(19, 20)),
        new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)) { EmbarkedOn = 1, Morale = 800 },
        new FormationSpec(3, 0, OneShot, 1, C(archerCol, 2)) { Posture = archerCol == 61 ? Posture.Defend : Posture.Normal },
        new FormationSpec(9, 1, UnitProfiles.HeavyInfantry, 5, C(62, 2)) { Posture = Posture.Defend });

    /// <summary>
    /// C1 for a LATER ARRIVAL, turn-exact (DB1-R1 (a): "later, at the instant it arrives"): the riflemen are put ashore from the transport
    /// at pulse 0 step 1 or pulse 2 step 4 (2,000 or 128,000 ms; observed: the first completed instant one of them is Active).
    /// (a) The battle's fighting began BEFORE they land (the archer at C(61, 2) looses its arrow at the start: the trace's first attack
    /// at 0): they are charged AT THEIR LANDING — 800 at the completed instant before, 750 at it — and the two formations on the field at
    /// the first fighting instant were charged then. (b) The fighting begins AFTER they land (the archer at C(58, 2), out of range, is
    /// ordered to Attack at pulse 3 and shoots at 258,000): they stay at 800 from their landing until the completed instant before
    /// 258,000 and are charged AT THE FIRST FIGHTING INSTANT, with everybody else. Each formation once. MEASURED.
    /// </summary>
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(2, 4, true)]
    [InlineData(0, 1, false)]
    [InlineData(2, 4, false)]
    public void C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact(int pulse, int k, bool fightFirst)
    {
        var setup = FoughtShore(fightFirst ? 61 : 58);
        var side0 = DT.Script((pulse, new OrderRequest(2, OrderKind.Disembark, k, -1, C(25, 20))));
        if (!fightFirst) side0.Add(3, new OrderRequest(3, OrderKind.Attack, 0, 9));
        var b = new TacticalBattle(setup, side0, PassiveController.Instance);
        long landedAt = -1, fightAt = -1, moraleBeforeCharge = -1;
        var moraleAt = new SortedDictionary<long, long>();
        var r = DT.RunChecked(b, 60, () =>
        {
            long now = b.Scheduler.NowMs;
            if (landedAt < 0 && b.Entities().Any(e => e.FormationId == 2 && e.State == EntityState.Active)) landedAt = now;
            if (fightAt < 0 && AttackEventsSoFar(b).Length > 0) fightAt = AttackEventsSoFar(b)[0].Key.TimeMs;
            moraleAt[now] = b.Morale(2);
        });
        Assert.Equal(fightAt, AttackEvents(b)[0].Key.TimeMs);                                   // [MOR3-R1-01] validated on the finished battle
        long expectedLanding = setup.StartMs + pulse * setup.Rules.PulseMs + k * setup.Rules.StepMs;
        Assert.Equal(expectedLanding, landedAt);
        Assert.Equal(fightFirst ? 0L : 258_000L, fightAt);
        Assert.Equal(fightAt, r.FirstFightMs);
        long chargeAt = Math.Max(landedAt, fightAt);
        foreach (var (t, m) in moraleAt) if (t < chargeAt) Assert.Equal(800, m); else break;     // aboard, then ashore: never charged before
        moraleBeforeCharge = moraleAt.Where(x => x.Key < chargeAt).Select(x => x.Value).Last();
        Assert.Equal(800, moraleBeforeCharge);
        Assert.Equal(750, moraleAt[chargeAt]);                                                   // charged exactly at chargeAt
        var rows = b.EntryCharges.ToArray();
        Assert.Equal(new EntryCharge(chargeAt, 2, 800, Cost), rows.Single(x => x.FormationId == 2));
        Assert.Equal(new[] { new EntryCharge(fightAt, 3, 1000, Cost), new EntryCharge(fightAt, 9, 1000, Cost) },
                     rows.Where(x => x.FormationId != 2).ToArray());
        Assert.Equal(landedAt, r.Formations.Single(x => x.FormationId == 2).EnteredAtMs);
    }

    /// <summary>
    /// C1 and DB1-R2 (a) ONCE, with re-entry, in a FOUGHT battle: lane N's swap (two formations a side taking turns aboard every pulse)
    /// beside a single-shot exchange elsewhere (archer 7 against heavy infantry 8 at C(58..60, 20): the first attack at the start).
    /// Formations 2 and 5 (ashore at the start) are charged at the first fighting instant (0), formations 3 and 6 (aboard at the start)
    /// at their first landing (4,000); each of the four leaves the field and comes back at least twice and is charged exactly once (end
    /// morale 850, 650, 950, 950: each start less 50, nobody hurt). Lane MOR2's swap without the exchange is unfought (EntryCostTests,
    /// re-pinned): there the part charges nothing at all. MEASURED.
    /// </summary>
    [Fact]
    public void C1_OncePerFormation_WithReEntry_InAFoughtBattle()
    {
        var t = DomainProfiles.SteamTransport with { Capacity = 30, SightMm = 500_000 };
        var forms = new[]
        {
            DT.Ship(1, 0, t, C(19, 30)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { Morale = 900 },
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { EmbarkedOn = 1, Morale = 700 },
            DT.Ship(4, 1, t, C(19, 6)),
            new FormationSpec(5, 1, DomainProfiles.Riflemen, 5, C(20, 6)),
            new FormationSpec(6, 1, DomainProfiles.Riflemen, 5, C(20, 6)) { EmbarkedOn = 4 },
            new FormationSpec(7, 0, OneShot, 1, C(58, 20)) { Posture = Posture.Defend },
            new FormationSpec(8, 1, UnitProfiles.HeavyInfantry, 5, C(60, 20)) { Posture = Posture.Defend },
        };
        var setup = DT.OnGrid(DT.Coast(Coast, 20), 1, null, new TacticalRules { EntryMoraleCost = Cost, Inactivity = InactivityModel.LegacySideDisengage }, forms);
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
        var placements = new SortedDictionary<long, List<long>>();
        var wasOn = new SortedDictionary<long, bool>();
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
        Assert.Equal(0L, r.FirstFightMs);
        Assert.Equal(0L, AttackEvents(b)[0].Key.TimeMs);
        foreach (long f in new long[] { 2, 3, 5, 6 })
        {
            Assert.True(placements[f].Count >= 2, $"formation {f} left the field and was placed on it again ({placements[f].Count} placements)");
            var row = Assert.Single(b.EntryCharges, x => x.FormationId == f);
            Assert.Equal(Math.Max(placements[f][0], r.FirstFightMs!.Value), row.AtMs);
            Assert.Equal(Cost, r.Formations.Single(x => x.FormationId == f).EntryCharged);
        }
        Assert.Equal((0L, 4_000L), (b.EntryCharges.Single(x => x.FormationId == 2).AtMs, b.EntryCharges.Single(x => x.FormationId == 3).AtMs));
        Assert.Equal(new[] { 850L, 650L, 950L, 950L }, new long[] { 2, 3, 5, 6 }.Select(f => r.Formations.Single(x => x.FormationId == f).MoraleEnd).ToArray());
        Assert.All(r.Formations, f => Assert.Equal(0, f.Killed));
    }

    /// <summary>
    /// What C1 removes, in the AutoResolver: lane MOR2's round-2 case of two equal formations at 480 placed symmetrically, with the part
    /// on, was charged to 430 AT ENTRY — below the withdrawal line (450) — so both withdrew in good order at their first decision and the
    /// battle ended BothEliminated without a blow: an unfought "win" the cost itself had made (MOR2 §14: "+150,000"). Under C1 nothing is
    /// charged before the first fight, so at 480 both advance and FIGHT, with the part off or on: the first fighting step is 131
    /// (262,000 ms), both rout in one instant (2 killed and 18 routed a side; BothEliminated, DA6 names the defender). With the part on
    /// both are charged at 262,000 and end at 230 instead of 280. The settlement counts the charge once: from 480,000 the defender's
    /// army ends at 430,000 and the attacker's at 230,000 whether the part is on or off (decrease (480 − charge − end) × 1,000 = 200,000
    /// in both, rise 200,000, entry 50,000). MEASURED and by hand.
    /// </summary>
    [Fact]
    public void C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver()
    {
        foreach (long cost in new[] { 0L, Cost })
        {
            var a = new AutoBattle(Apart(480, MirrorColumn(6), cost), new AutoCoefficients());
            var r = a.Run();
            Assert.Equal(262_000L, r.FirstFightMs);
            Assert.Equal((BattleEndReason.BothEliminated, 1, WinBasis.DefenderOnEqualSides), (r.Reason, BattleWinner.Reported(r), r.WinBasis));
            Assert.All(r.Formations, f => Assert.Equal((2L, 18L, cost, 280 - cost), (f.Killed, f.RoutedEscaped, f.EntryCharged, f.MoraleEnd)));
            Assert.Equal(cost == 0 ? Array.Empty<EntryCharge>() : new[] { new EntryCharge(262_000, 1, 480, Cost), new EntryCharge(262_000, 2, 480, Cost) },
                         a.EntryCharges.ToArray());
            var def = Settle(r, 2, 1, FromAuto(r.Formations.Single(x => x.FormationId == 2), 1, 1000), 480, beaten: 2_000, winnerSide: 2_000);
            Assert.Equal(((byte)1, CostPpm, 200_000L, 200_000L, 430_000L), (def.Fought, def.EntryPpm, def.DecreasePpm, def.RisePpm, def.AfterPpm));
            var att = Settle(r, 1, 0, FromAuto(r.Formations.Single(x => x.FormationId == 1), 0, 1000), 480, beaten: 2_000, winnerSide: 2_000);
            Assert.Equal(((byte)1, CostPpm, 200_000L, 0L, 230_000L), (att.Fought, att.EntryPpm, att.DecreasePpm, att.RisePpm, att.AfterPpm));
        }
    }

    // ================================================================== 4. THE WAR SANDBOX (D-085 §4)

    /// <summary>
    /// The War Sandbox under DD1 (proposal §12.5): it keeps no campaign morale and settles nothing (D15), and it runs the campaign's
    /// resolvers with its rules (DR30), so DD1 reaches it through the in-battle part. With rules carrying the part (50): a Quick Battle
    /// in which both sides are players who never order anything, 31 columns apart (out of each other's sight), has no fighting —
    /// FirstFightMs null and nothing charged; the same forces AI against AI (the AutoResolver) fight, and every formation is charged once,
    /// at the battle's first fighting instant. MEASURED.
    /// [MOR3, verification round 1, MOR3-R1-04] "Once" and "at the first fighting instant" are now observed: the sandbox ran the AutoResolver on
    /// its deployed setup, and the same AutoBattle run again is the same battle (its result and trace hashes) whose charge rows are exactly one
    /// per formation, each at the first fighting instant (232,000 ms), each the part (50) from the formation's morale then (1000). Before, the
    /// test read only each formation's EntryCharged (assigned, not summed: a second charge would not show) and an instant after the start; a
    /// charge one step late and a charge at every fighting step both passed it (MEASURED, mutants R04m and R04m2, H-MOR3 §13).
    /// </summary>
    [Fact]
    public void WarSandbox_QuickBattle_WithoutFighting_ChargesNothing_WithFighting_ChargesAtTheFirstFight()
    {
        foreach (bool players in new[] { true, false })
        {
            var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 11, new TacticalRules { EntryMoraleCost = Cost });
            Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 50, Cell(8, 16), out _));
            Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 50, Cell(39, 16), out _));
            Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
            Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
            var started = players ? s.StartBattle(SandboxCommand.Player, SandboxCommand.Player, PassiveController.Instance, PassiveController.Instance)
                                  : s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai);
            Assert.Equal(SandboxRefusal.None, started);
            var res = s.Result!;
            if (players)
            {
                Assert.Equal(ResolverKind.Tactical, s.ResolverUsed);
                Assert.Null(res.FirstFightMs);
                Assert.All(res.Formations, f => Assert.Equal((0L, 0L), (f.EntryCharged, f.AttacksMade)));
            }
            else
            {
                Assert.Equal(ResolverKind.Auto, s.ResolverUsed);
                Assert.NotNull(res.FirstFightMs);
                Assert.True(res.FirstFightMs > s.DeployedSetup!.StartMs, "the forces had to close first");
                Assert.All(res.Formations, f => Assert.Equal(Cost, f.EntryCharged));
                Assert.Equal(232_000L, res.FirstFightMs);                                                        // [MOR3-R1-04] MEASURED
                var again = new AutoBattle(s.DeployedSetup!, new AutoCoefficients());
                var ar = again.Run();
                Assert.Equal((res.ResultHash(), res.TraceHash), (ar.ResultHash(), ar.TraceHash));              // the sandbox's battle
                Assert.Equal(res.Formations.Select(f => new EntryCharge(232_000, f.FormationId, 1000, Cost)).ToArray(), again.EntryCharges.ToArray());
            }
        }
    }

    // ================================================================== 5. VERIFICATION ROUND 1 (MOR3-R1-01, -02, -03)

    /// <summary>[MOR3, verification round 1, MOR3-R1-01] The battles of the line-of-fire tests (TEST INPUT, seed 1): side 0's shooters
    /// (three entities) and a side-0 spotter (one heavy infantryman, who sees the target), side 1's five heavy infantry; every formation
    /// holds (Defend) at 800, so nobody moves. Between the shooters and their target stands either a WALL (column 21, rows 13 to 19:
    /// archers at (20, 16), the infantry at (22, 16), the spotter at (23, 9), the verifier's setup) or a full-height column of BUILDINGS
    /// (column 22: riflemen at (20, 16), the spotter at (24, 16), the infantry at (25, 16), 100 m from the riflemen, inside their 120 m).</summary>
    private static BattleSetup Obstructed(bool wall, long cost)
    {
        var g = OpenField();
        if (wall) for (int y = 13; y <= 19; y++) g.Set(21, y, TerrainClass.Wall);
        else for (int y = 0; y < FieldH; y++) g.Set(22, y, TerrainClass.Building);
        return new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(Array.Empty<Vec2L>(), Array.Empty<Vec2L>()),
            new[]
            {
                new FormationSpec(1, 0, wall ? UnitProfiles.Archers : DomainProfiles.Riflemen, 3, Cell(20, 16)) { Posture = Posture.Defend, Morale = 800 },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, wall ? Cell(22, 16) : Cell(25, 16)) { Posture = Posture.Defend, Morale = 800 },
                new FormationSpec(3, 0, UnitProfiles.HeavyInfantry, 1, wall ? Cell(23, 9) : Cell(24, 16)) { Posture = Posture.Defend, Morale = 800 },
            },
            1) { Rules = new TacticalRules { EntryMoraleCost = cost } };
    }

    /// <summary>
    /// [MOR3, verification round 1, MOR3-R1-01] DD1-R2 (a) at the TacticalResolver's line of fire, "a shot with no line of fire is not
    /// fired, so it is no attack" (morale proposal §12.2), at both of the Fire handler's checks: the projectile line (a WALL) and the
    /// layer (an opaque STRUCTURE). The shooters loose at their known target at every step of the battle: 540 executed missile Fire
    /// events (three shooters, 180 steps). The line-of-fire oracle, observed from outside the rule at each shot's own instant, finds
    /// every one of them blocked: by the wall on the projectile line, or by the buildings in the fire path. The engine counted none of
    /// them as an attack (Σ AttacksMade 0, so the trace's attack events are no oracle for this battle and AttackEvents refuses it), nobody
    /// was hurt, and the battle reports NO fighting (FirstFightMs null) and ends in lane TR's Standoff at 360,000 ms. So it changes no
    /// army's morale: through the hand-off both armies settle with Fought 0 and stay at 800,000. With the in-battle part on nothing is
    /// charged and the battle is the same battle (result and trace hashes). The verifier's mutant V1 (fighting reported before the
    /// projectile-line check) and this round's V1b (before the layer check) report fighting at 0 here and charge all three formations
    /// with the part on (MEASURED by probe, H-MOR3 §13). MEASURED.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DD1_R2a_AShotWithNoLineOfFire_IsNoAttack_TheBattleHasNoFighting_TacticalResolver(bool wall)
    {
        var map = new[] { (1L, 0), (2L, 1), (3L, 2) };
        string? off = null;
        foreach (long cost in new[] { 0L, Cost })
        {
            var s = Obstructed(wall, cost);
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            var (r, shots) = RunObservingShots(b, s);
            var fires = Executed(b, TacticalKinds.Fire);
            Assert.Equal(540, fires.Length);
            Assert.All(fires, e => Assert.Equal(2L, e.A));                                    // missile shots only, not one blow
            Assert.Equal(540, shots.Length);
            Assert.All(shots, x => Assert.Equal(wall ? LosBlock.Wall : LosBlock.Structure, x.Block));   // not one had a line of fire
            Assert.Equal(0L, shots[0].Event.Key.TimeMs);
            Assert.Equal(0L, r.Formations.Sum(f => f.AttacksMade));                           // the engine counted none of them
            Assert.Equal(540L, UncountedLandAttackEvents(b, r));                              // so AttackEvents refuses this battle
            Assert.Empty(Executed(b, TacticalKinds.Damage));
            Assert.Null(r.FirstFightMs);
            Assert.False(r.Fought);
            Assert.Equal((BattleEndReason.Standoff, 360_000L, -1), (r.Reason, r.EndMs, BattleWinner.Reported(r)));
            if (cost == 0)
            {
                off = r.ResultHash() + "/" + r.TraceHash;
                var col = BattleCollapse.FromTactical(b, s, map);
                foreach (var (fid, army) in new[] { (1L, 0), (2L, 1) })
                {
                    var m = Settle(r, fid, army, col[army], 800, beaten: 0, winnerSide: 0);
                    Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (m.Fought, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
                }
            }
            else
            {
                Assert.Empty(b.EntryCharges);
                Assert.All(r.Formations, f => Assert.Equal(0L, f.EntryCharged));
                Assert.Equal(off, r.ResultHash() + "/" + r.TraceHash);
            }
        }
    }

    /// <summary>
    /// [MOR3, verification round 1, MOR3-R1-01, MOR3-R1-03] DD1-R2 (a) at the AutoResolver's line of fire, a volley "which the model looses
    /// only with a line of fire" (morale proposal §12.2), on the BUILDINGS battle above. Observed from outside the rule, a volley was due
    /// from step 0: side 0 knew the infantry from step 0 (the spotter's detection), the riflemen had not moved, and the infantry stood out
    /// of contact and inside the riflemen's range along the battle axis; but the straight line between the two formations' points on the
    /// axis runs through the buildings (TerrainSight.FirePath: blocked by a structure), so the model loosed nothing: no attack in its step
    /// log, Σ AttacksMade 0, NO fighting (FirstFightMs null), lane TR's Standoff at 360,000 ms. The TacticalResolver agrees on this battle:
    /// no fighting either (the test above). Both armies settle with Fought 0 at 800,000; with the in-battle part on nothing is charged and
    /// the result is the same. The verifier's mutant V2 (fighting reported before the volley's line-of-fire test) reports fighting at 0
    /// here (MEASURED by probe, H-MOR3 §13). On the WALL battle the two resolvers do NOT agree: the AutoResolver's formation-level contact
    /// and its line of fire ignore walls, so it fights from step 0 there while the TacticalResolver has no fighting. That is lane P's
    /// model, recorded as a parity item (morale proposal §12.10) and not asserted here. MEASURED.
    /// </summary>
    [Fact]
    public void DD1_R2a_AVolleyWithNoLineOfFire_IsNoAttack_TheBattleHasNoFighting_AutoResolver_AsInTheTacticalResolver()
    {
        var s = Obstructed(wall: false, 0);
        var a = new AutoBattle(s, new AutoCoefficients()) { RecordLog = true };
        var r = a.Run();
        var rifles = a.Log.Single(x => x.Step == 0 && x.FormationId == 1);
        var target = a.Log.Single(x => x.Step == 0 && x.FormationId == 2);
        Assert.Equal(0L, a.FirstKnownStep(0, 2));                                              // side 0 knew the infantry from step 0
        Assert.False(rifles.Moved);
        long gap = target.PosMm - rifles.PosMm, contact = 2 * s.Rules.EntityRadiusMm + s.Rules.ContactReachMm;
        Assert.InRange(gap, contact + 1, DomainProfiles.Riflemen.RangeCells * s.Grid.CellSizeMm);   // out of contact, inside range
        var axis = a.Axis!;
        var line = TerrainSight.FirePath(s.Grid, s.Effects, axis.At(rifles.PosMm), s.Rules.MuzzleHeightMm, axis.At(target.PosMm), s.Rules.AimHeightMm,
                                         axis.At(target.PosMm), 1, 1);
        Assert.Equal((true, LosBlock.Structure), (line.Blocked, line.Block));                  // ...and no line of fire
        Assert.DoesNotContain(a.Log, x => x.Mode != 0);
        Assert.Equal(0L, r.Formations.Sum(f => f.AttacksMade));
        Assert.Null(r.FirstFightMs);
        Assert.Equal((BattleEndReason.Standoff, 360_000L), (r.Reason, r.EndMs));
        Assert.Null(TT.RunGuarded(new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance)).FirstFightMs);   // both agree
        foreach (var (fid, army) in new[] { (1L, 0), (2L, 1) })
        {
            var m = Settle(r, fid, army, FromAuto(r.Formations.Single(x => x.FormationId == fid), army, 1000), 800, beaten: 0, winnerSide: 0);
            Assert.Equal(((byte)0, 0L, 0L, 0L, 800_000L), (m.Fought, m.EntryPpm, m.DecreasePpm, m.RisePpm, m.AfterPpm));
        }
        var on = new AutoBattle(Obstructed(wall: false, Cost), new AutoCoefficients());
        var ron = on.Run();
        Assert.Null(ron.FirstFightMs);
        Assert.Empty(on.EntryCharges);
        Assert.Equal(r.ResultHash(), ron.ResultHash());
    }

    /// <summary>
    /// [MOR3, verification round 1, MOR3-R1-02] DD1-R2 (a), "whether or not it dealt damage", for a DIRECT domain weapon shot at a LAND
    /// entity that MISSES. Lane N's strike case with an attack helicopter (rockets: direct fire, no blast) ordered to attack riflemen
    /// beyond a ridge (seed 14, TEST INPUT found by probe): its first rocket, at 0 ms, is aimed at a land entity of formation 2 and misses
    /// (no DomainHit at 0; the first rocket that hits is at 36,000). The battle's first fighting instant is 0, not 36,000. The verifier's
    /// mutant V5a (such a shot is fighting only when it hits) reports 36,000 here (MEASURED by probe). MEASURED.
    /// </summary>
    [Fact]
    public void DD1_R2a_ADirectDomainShotAtALandEntityThatMisses_IsFighting_AtItsOwnInstant()
    {
        var g = new TerrainGrid(64, 40, Coast, TerrainClass.Open);
        for (int y = 5; y < 35; y++) g.Set(30, y, TerrainClass.Hills, 40_000);
        var s = DT.OnGrid(g, 14, null, null, new FormationSpec(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)), MultiDomainScenarios.Flight(3, 0, DomainProfiles.AttackHelicopter, 1));
        var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(3, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        var r = DT.RunChecked(b, 80);
        var first = AttackEvents(b)[0];
        Assert.Equal((DomainKinds.DomainFire, 3000L, 0L), (first.Kind, first.Source.Id, first.Key.TimeMs));
        Assert.Equal(2L, DT.L(b, first.Target.Id).FormationId);                                // aimed at a land entity of formation 2
        Assert.False(DomainProfiles.AttackHelicopter.Weapons[(int)first.A].Indirect);           // a direct weapon: no blast
        var hits = Executed(b, DomainKinds.DomainHit);
        Assert.DoesNotContain(hits, h => h.Key.TimeMs == 0);                                    // it missed
        Assert.Equal(36_000L, hits[0].Key.TimeMs);                                              // the first rocket that hit
        Assert.Equal(0L, r.FirstFightMs);
    }

    /// <summary>Where an indirect round fell, recomputed from outside the rule from the trace's aim point (B, C) and the battle's counter
    /// RNG, as DomainFire scatters it (CITED, TacticalBattle.DomainCombat.cs): uniformly over the square of half-width
    /// floor(range × DispersionPermille ÷ 1000) around the aim point, the range measured from the shooter (on the ground) to the aim point.</summary>
    private static Vec2L ImpactOf(ulong seed, TraceEntry round, Vec2L shooter, WeaponSpec w)
    {
        var aim = new Vec2L(round.B, round.C);
        long range = (long)FixedMath.ISqrt((UInt128)Vec2L.DistanceSq(shooter, aim));
        long half = FixedMath.MulDivFloor(range, w.DispersionPermille, 1000);
        ulong span = (ulong)checked(2 * half + 1);
        long ox = (long)CounterRng.NextBelow(seed, DomainStreams.Dispersion, round.Source.Id, round.Key.TimeMs, round.A, 0, span) - half;
        long oy = (long)CounterRng.NextBelow(seed, DomainStreams.Dispersion, round.Source.Id, round.Key.TimeMs, round.A, 1, span) - half;
        return new Vec2L(aim.X + ox, aim.Y + oy);
    }

    /// <summary>
    /// [MOR3, verification round 1, MOR3-R1-02] DD1-R2 (a) for an INDIRECT crew-served round aimed at a LAND entity whose blast reaches no
    /// enemy ("hit or miss, wherever its dispersion lands", morale proposal §12.2). A field-gun crew (side 0) shells a lone rifleman (side 1,
    /// the only entity of its side) 900 m away (seed 6, TEST INPUT found by probe): the first round, at 0 ms, aimed at the rifleman, falls
    /// 23,343 mm from him (recomputed from outside the rule: the trace's aim point and DomainFire's dispersion draw), beyond the gun's
    /// 15,000 mm blast, so it reaches nobody; the first round to hit him falls at 16,000. The battle's first fighting instant is 0. The
    /// verifier's mutant V5c (such a round is fighting only when its blast reaches an enemy) reports 16,000 here (MEASURED by probe).
    /// MEASURED.
    /// </summary>
    [Fact]
    public void DD1_R2a_AnIndirectRoundAimedAtALandEntity_IsFighting_WhereverItLands()
    {
        var g = new TerrainGrid(64, 40, Coast, TerrainClass.Open);
        var gun = new FormationSpec(1, 0, DomainProfiles.GunCrew, 1, C(10, 20)) { Weapons = new[] { DomainProfiles.FieldGun } };
        var s = DT.OnGrid(g, 6, null, null, gun, new FormationSpec(2, 1, DomainProfiles.Riflemen, 1, C(28, 20)));
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        Vec2L gunAt = default, riflemanAt = default;
        var r = DT.RunChecked(b, 80, () => { if (b.Scheduler.NowMs == 0) { gunAt = DT.L(b, 1000).Pos; riflemanAt = DT.L(b, 2000).Pos; } });
        var first = AttackEvents(b)[0];
        Assert.Equal((DomainKinds.DomainFire, 1000L, 2000L, 0L), (first.Kind, first.Source.Id, first.Target.Id, first.Key.TimeMs));
        Assert.True(DomainProfiles.FieldGun.Indirect);
        Assert.Single(b.Entities(), e => e.Side == 1);                                         // the rifleman is his side's only entity
        long miss = Vec2L.Distance(ImpactOf(s.Seed, first, gunAt, DomainProfiles.FieldGun), riflemanAt);
        Assert.Equal(23_343L, miss);
        Assert.True(miss > DomainProfiles.FieldGun.BlastRadiusMm, "the first round's blast reached no enemy");
        var hits = Executed(b, DomainKinds.DomainHit);
        Assert.DoesNotContain(hits, h => h.Key.TimeMs == 0);
        Assert.Equal(16_000L, hits[0].Key.TimeMs);
        Assert.Equal(0L, r.FirstFightMs);
    }

    /// <summary>
    /// [MOR3, verification round 1, MOR3-R1-02] DD1-R2 (a) for a BOARDING fight that costs nobody ("both crews strike", morale proposal
    /// §12.2): an unarmed galley with a crew of 6 boards a transport with a crew of 6 and nobody embarked (seed 10, TEST INPUT found by
    /// probe). The first boarding fight is at 26,000 ms, with all six men still aboard each hull at that instant and no casualty from it;
    /// the first casualty of the boarding falls at 48,000. The battle's first fighting instant is 26,000. The verifier's mutant V6 (a
    /// boarding fight is fighting only when it causes casualties) reports 48,000 here (MEASURED by probe). MEASURED.
    /// </summary>
    [Fact]
    public void DD1_R2a_ABoardingFightWithoutCasualties_IsFighting_AtItsOwnInstant()
    {
        var unarmed = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), Crew = 6 };
        var transport = DomainProfiles.OaredTransport with { Crew = 6 };
        var s = DT.Sea(50_000, 10, null, DT.Ship(1, 0, unarmed, DT.M(1000, 1000)), DT.Ship(2, 1, transport, DT.M(1060, 1000)));
        var b = new TacticalBattle(s, DT.Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        (long A, long B) crewsThen = (-1, -1);
        var r = DT.RunChecked(b, DT.HarnessMaxPulses, () => { if (b.Scheduler.NowMs == 26_000) crewsThen = (DT.D(b, 1000).Crew, DT.D(b, 2000).Crew); });
        var first = AttackEvents(b)[0];
        Assert.Equal((DomainKinds.Boarding, 1000L, 2000L, 26_000L), (first.Kind, first.Source.Id, first.Target.Id, first.Key.TimeMs));
        Assert.Equal((6L, 6L), crewsThen);                                                      // both crews aboard, nobody lost
        var hits = Executed(b, DomainKinds.DomainHit);
        Assert.DoesNotContain(hits, h => h.Key.TimeMs == 26_000);
        Assert.Equal(48_000L, hits[0].Key.TimeMs);                                              // the first casualty of the boarding
        Assert.Equal(26_000L, r.FirstFightMs);
    }

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
