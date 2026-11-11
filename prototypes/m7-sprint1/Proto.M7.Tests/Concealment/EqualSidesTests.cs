using Proto.M7.Fixed;
using Proto.M7.Morale;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Tests.Morale;
using Proto.M7.Tests.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Concealment;

/// <summary>
/// [AP] D-082 DA6 ("If two sides are equal, the defending side wins.") and DA7 with DA7-R (a) (an ambush from concealment goes to
/// the ambusher), as <see cref="EqualSides"/> decides them in BOTH resolvers (docs/m7-plan/concealment-actions-proposal.md §6).
/// "Equal" (DA6-R, this lane's definition): the two sides' elimination instants are equal — BothEliminated (the same step / instant)
/// or BothDisengaged (the same pulse end); a Standoff only with the DA6-R extension ON (TacticalRules.EqualSidesAtStandoff,
/// default OFF). Every unit stat below is a TEST INPUT.
/// </summary>
public class EqualSidesTests
{
    // ---------------------------------------------------------------- the rule, exhaustively

    /// <summary>TIE-DENSE TRUTH TABLE: every end reason × eliminated side × designated defender × ambusher × extension. A decided end
    /// (SideEliminated / SideDisengaged) always goes to the other side whatever the roles; an equal end goes to the ambusher, else the
    /// defender, else nobody; a Standoff is equal only with the extension; None reports nobody.</summary>
    [Fact]
    public void Decide_TruthTable_EveryReasonSideRoleAndExtension()
    {
        var reasons = (BattleEndReason[])Enum.GetValues(typeof(BattleEndReason));
        int rows = 0;
        foreach (var reason in reasons)
            foreach (int side in new[] { -1, 0, 1 })
                foreach (int defender in new[] { -1, 0, 1 })
                    foreach (int ambusher in new[] { -1, 0, 1 })
                        foreach (bool ext in new[] { false, true })
                        {
                            var (w, b) = EqualSides.Decide(reason, side, defender, ambusher, ext);
                            rows++;
                            bool decided = reason is BattleEndReason.SideEliminated or BattleEndReason.SideDisengaged && side >= 0;
                            bool equal = reason is BattleEndReason.BothEliminated or BattleEndReason.BothDisengaged || (ext && reason == BattleEndReason.Standoff);
                            if (decided) Assert.Equal((1 - side, WinBasis.Decisive), (w, b));
                            else if (equal && ambusher >= 0) Assert.Equal((ambusher, WinBasis.AmbusherOnEqualSides), (w, b));
                            else if (equal && defender >= 0) Assert.Equal((defender, WinBasis.DefenderOnEqualSides), (w, b));
                            else Assert.Equal((-1, WinBasis.None), (w, b));
                        }
        Assert.Equal(reasons.Length * 3 * 3 * 3 * 2, rows);
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualSides.Decide(BattleEndReason.BothEliminated, -1, 2, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualSides.Decide(BattleEndReason.BothEliminated, -1, -1, -2, false));
    }

    // ---------------------------------------------------------------- the TacticalResolver at and around the boundary

    private static readonly TacticalRules Sure = new() { HitMin = Q32.One, HitMax = Q32.One };   // TEST INPUT: every blow lands

    /// <summary>A 1-v-1 (or 3-v-3) melee in contact from t = 0: heavy infantry (Melee 50 → 10 HP per blow, every blow lands). Side 0's
    /// entities have 50 + margin0 HP, side 1's 50 + margin1: at margin 0 both sides' last entities die at the SAME instant (the 5th
    /// exchange); one extra HP and that side survives the instant.</summary>
    internal static BattleSetup Duel(int pairs, long margin0, long margin1, int defender, int ambusher, TacticalRules? rules = null)
    {
        var p0 = UnitProfiles.HeavyInfantry with { Integrity = 50 + margin0 };
        var p1 = UnitProfiles.HeavyInfantry with { Integrity = 50 + margin1 };
        var e0 = new EntitySpec[pairs]; var e1 = new EntitySpec[pairs];
        for (int i = 0; i < pairs; i++)
        {
            long y = 300_000 + i * 10_000;
            e0[i] = new EntitySpec(1000 + i, new Vec2L(470_000, y));
            e1[i] = new EntitySpec(2000 + i, new Vec2L(471_000, y));
        }
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
        {
            new FormationSpec(1, 0, p0, pairs, new Vec2L(470_000, 300_000)) { Entities = e0 },
            new FormationSpec(2, 1, p1, pairs, new Vec2L(471_000, 300_000)) { Entities = e1 },
        }, 11) { Rules = rules ?? Sure, Defender = defender, Ambusher = ambusher };
    }

    /// <summary>
    /// TIE-DENSE AT AND AROUND THE BOUNDARY (TacticalResolver): margin (side 0 HP − side 1 HP) ∈ {−1, 0, +1} × defender ∈ {−1, 0, 1}
    /// × ambusher ∈ {−1, 0, 1}, with 1 and 3 simultaneous pairs. At 0 the result separates neither side (BothEliminated) and the
    /// ambusher, else the defender, else nobody wins; at ±1 the side that survived the instant wins (Decisive) whatever the roles.
    /// The reported winner (BattleWinner.Reported) equals the result's, and the battle itself (end instant, casualties) does not
    /// depend on the roles.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TacticalDuel_EqualEliminationGoesToTheAmbusherOrTheDefender_OneHpDecides(int pairs)
    {
        foreach (long margin in new long[] { -1, 0, 1 })
        {
            string? battle = null;
            foreach (int defender in new[] { -1, 0, 1 })
                foreach (int ambusher in new[] { -1, 0, 1 })
                {
                    var r = new TacticalBattle(Duel(pairs, Math.Max(0, margin), Math.Max(0, -margin), defender, ambusher), PassiveController.Instance, PassiveController.Instance).Run();
                    TT.AssertConserved(r);
                    string shape = $"{r.Reason}/{r.Side}/{r.HoldsField}/{r.EndMs}/{string.Join(",", r.Formations.Select(f => f.Killed))}";
                    battle ??= shape;
                    Assert.Equal(battle, shape);                                      // the roles never change the battle
                    if (margin == 0)
                    {
                        Assert.Equal((BattleEndReason.BothEliminated, -1), (r.Reason, r.HoldsField));
                        int expect = ambusher >= 0 ? ambusher : defender;
                        var basis = ambusher >= 0 ? WinBasis.AmbusherOnEqualSides : defender >= 0 ? WinBasis.DefenderOnEqualSides : WinBasis.None;
                        Assert.Equal((expect, basis), (r.Winner, r.WinBasis));
                        Assert.Equal(expect, BattleWinner.Reported(r));
                    }
                    else
                    {
                        int survivor = margin > 0 ? 0 : 1;
                        Assert.Equal((BattleEndReason.SideEliminated, 1 - survivor, survivor), (r.Reason, r.Side, r.HoldsField));
                        Assert.Equal((survivor, WinBasis.Decisive), (r.Winner, r.WinBasis));
                        Assert.Equal(survivor, BattleWinner.Reported(r));
                    }
                }
        }
    }

    /// <summary>The result hash takes the winner only when DA6 or DA7 decided it: a decided result and a no-winner result hash exactly
    /// as they did before lane AP (the same hash with the winner fields reset), an equal-sides result does not (teeth).</summary>
    [Fact]
    public void ResultHash_AppendsTheWinnerOnlyWhenTheEqualSidesRuleDecided()
    {
        var decided = new TacticalBattle(Duel(1, 1, 0, 1, -1), PassiveController.Instance, PassiveController.Instance).Run();
        var none = new TacticalBattle(Duel(1, 0, 0, -1, -1), PassiveController.Instance, PassiveController.Instance).Run();
        var da6 = new TacticalBattle(Duel(1, 0, 0, 1, -1), PassiveController.Instance, PassiveController.Instance).Run();
        var da7 = new TacticalBattle(Duel(1, 0, 0, 1, 0), PassiveController.Instance, PassiveController.Instance).Run();
        Assert.Equal(decided.ResultHash(), (decided with { Winner = -1, WinBasis = WinBasis.None }).ResultHash());
        Assert.Equal(none.ResultHash(), (none with { Winner = -1, WinBasis = WinBasis.None }).ResultHash());
        Assert.NotEqual(da6.ResultHash(), (da6 with { Winner = -1, WinBasis = WinBasis.None }).ResultHash());
        Assert.Equal(none.ResultHash(), (da6 with { Winner = -1, WinBasis = WinBasis.None }).ResultHash());   // the same battle otherwise
        Assert.NotEqual(da6.ResultHash(), da7.ResultHash());
        Assert.Equal((none.TraceHash, none.StateHash), (da6.TraceHash, da6.StateHash));
    }

    // ---------------------------------------------------------------- the AutoResolver at and around the boundary

    private static readonly AutoCoefficients Exact = new() { FortunePermille = 0, NoisePermille = 0, KillEfficiencyPermille = 1000 };   // TEST INPUT

    /// <summary>AutoResolver, BothEliminated: two one-entity formations in contact at step 0, each blow kills (Melee 500 → 100 HP ≥
    /// 50 HP, every blow lands, no perturbation): both die in step 0. The roles decide the reported winner exactly as in the
    /// TacticalResolver, and the battle does not depend on them.</summary>
    [Fact]
    public void AutoResolver_MutualKill_EqualSidesGoToTheAmbusherOrTheDefender()
    {
        var brute = UnitProfiles.HeavyInfantry with { Melee = 500 };
        string? battle = null;
        foreach (int defender in new[] { -1, 0, 1 })
            foreach (int ambusher in new[] { -1, 0, 1 })
            {
                var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
                {
                    new FormationSpec(1, 0, brute, 1, new Vec2L(470_000, 330_000)),
                    new FormationSpec(2, 1, brute, 1, new Vec2L(471_000, 330_000)),
                }, 3) { Rules = Sure, Defender = defender, Ambusher = ambusher };
                var r = new AutoBattle(s, Exact).Run();
                Assert.Equal((BattleEndReason.BothEliminated, 1L), (r.Reason, r.Pulses));
                string shape = $"{r.EndMs}/{string.Join(",", r.Formations.Select(f => f.Killed))}/{r.StateHash}";
                battle ??= shape;
                Assert.Equal(battle, shape);
                int expect = ambusher >= 0 ? ambusher : defender;
                Assert.Equal(expect, r.Winner);
                Assert.Equal(expect, BattleWinner.Reported(r));
                Assert.Equal(ambusher >= 0 ? WinBasis.AmbusherOnEqualSides : defender >= 0 ? WinBasis.DefenderOnEqualSides : WinBasis.None, r.WinBasis);
            }
    }

    /// <summary>Two idle forces beyond sight of each other under the legacy side-disengage rule: each side's withdrawal instant is the
    /// pulse end at which its counter reaches 5. A march of two cells toward the enemy (an objective) keeps a side active for pulse 0
    /// only, so its counter reaches 5 one pulse LATER. Returns the setup for (march0, march1).</summary>
    private static BattleSetup IdleApart(bool march0, bool march1, int defender, int ambusher)
    {
        Vec2L a = Cell(5, 16), b = Cell(42, 16);
        var sides = DefaultSides(march0 ? new[] { Cell(7, 16) } : null, march1 ? new[] { Cell(40, 16) } : null);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, new[]
        {
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, a),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, b),
        }, 5) { Rules = TT.Legacy, Defender = defender, Ambusher = ambusher };
    }

    /// <summary>
    /// TIE-DENSE AT AND AROUND THE BOUNDARY, BOTH RESOLVERS (the disengagement instants, pulse-exact): both idle → both withdraw at
    /// the same pulse end (BothDisengaged) → equal: the ambusher, else the defender, else nobody. One side marches in pulse 0 → it
    /// withdraws one pulse later than the other → the other's withdrawal decides (SideDisengaged): the marching side wins, whatever
    /// the roles. The same in the TacticalResolver (knowledge-only AI) and the AutoResolver (its doctrine).
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BothResolvers_SimultaneousWithdrawalIsEqual_OnePulseLaterIsDecided(bool march0, bool march1)
    {
        foreach (bool auto in new[] { false, true })
            foreach (int defender in new[] { -1, 0, 1 })
                foreach (int ambusher in new[] { -1, 0, 1 })
                {
                    var s = IdleApart(march0, march1, defender, ambusher);
                    var r = auto ? new AutoBattle(s, new AutoCoefficients()).Run() : TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));
                    if (!march0 && !march1)
                    {
                        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
                        Assert.Equal(ambusher >= 0 ? ambusher : defender, r.Winner);
                        Assert.Equal(r.Winner, BattleWinner.Reported(r));
                    }
                    else
                    {
                        int winner = march0 ? 0 : 1;
                        Assert.Equal((BattleEndReason.SideDisengaged, 1 - winner), (r.Reason, r.Side));
                        Assert.Equal((winner, WinBasis.Decisive), (r.Winner, r.WinBasis));
                    }
                    Assert.Equal(5L, r.Pulses);   // the battle ends at the EARLIER withdrawal (pulse 4's end) in every case
                }
    }

    // ---------------------------------------------------------------- the DA6-R Standoff extension (PROPOSAL, OFF by default)

    /// <summary>Two Defend lines in sight of each other, never attacking: the whole-field Standoff (lane TR's UNRATIFIED P-TR1) in both
    /// resolvers. Extension OFF (the default): no winner, as before. ON: the designated defender (DA6), or the ambusher (DA7).</summary>
    [Fact]
    public void StandoffExtension_OffByDefault_OnGivesTheRole_InBothResolvers()
    {
        Assert.False(new TacticalRules().EqualSidesAtStandoff);
        foreach (bool auto in new[] { false, true })
            foreach (bool ext in new[] { false, true })
                foreach (var (defender, ambusher) in new[] { (1, -1), (0, 1), (-1, -1) })
                {
                    var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
                    {
                        new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)) { Posture = Posture.Defend },
                        new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(20, 16)) { Posture = Posture.Defend },
                    }, 7) { Rules = new TacticalRules { EqualSidesAtStandoff = ext }, Defender = defender, Ambusher = ambusher };
                    var r = auto ? new AutoBattle(s, new AutoCoefficients()).Run() : TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));
                    Assert.Equal(BattleEndReason.Standoff, r.Reason);
                    int expect = !ext ? -1 : ambusher >= 0 ? ambusher : defender;
                    Assert.Equal(expect, r.Winner);
                    Assert.Equal(expect, BattleWinner.Reported(r));
                    Assert.Equal(!ext || expect < 0 ? WinBasis.None : ambusher >= 0 ? WinBasis.AmbusherOnEqualSides : WinBasis.DefenderOnEqualSides, r.WinBasis);
                }
    }

    /// <summary>What the extension would change on a REAL Standoff (MEASURED): lane N's amphibious battle ends as a Standoff under the
    /// DIR5 rule (the holding shore guns and fighter stay); its setup names side 1 (the shore) the defender. OFF: no winner. ON: side 1
    /// is reported the winner (DA6). The battle itself (trace, state, end) is identical either way.</summary>
    [Fact]
    public void StandoffExtension_OnLaneNsAmphibiousBattle_ChangesOnlyTheReportedWinner()
    {
        BattleResult Run(bool ext)
        {
            var s0 = MultiDomainScenarios.Amphibious(42);
            var s = s0 with { Rules = s0.Rules with { EqualSidesAtStandoff = ext } };
            return Proto.M7.Tests.MultiDomain.DT.RunChecked(new TacticalBattle(s, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence()));
        }
        var off = Run(false); var on = Run(true);
        Assert.Equal(BattleEndReason.Standoff, off.Reason);
        Assert.Equal((off.TraceHash, off.StateHash, off.EndMs, off.Pulses), (on.TraceHash, on.StateHash, on.EndMs, on.Pulses));
        Assert.Equal((-1, WinBasis.None), (off.Winner, off.WinBasis));
        Assert.Equal((1, WinBasis.DefenderOnEqualSides), (on.Winner, on.WinBasis));
        Assert.Equal(1, BattleWinner.Reported(on));
    }

    // ---------------------------------------------------------------- DA1: the equal-sides winner's morale rises

    /// <summary>
    /// D-082 DA1 through lane MOR's rule, fed by the resolver's reported winner: an equal battle (the TacticalResolver duel, both
    /// sides eliminated at one instant) with side 1 the defender → BattleWinner.Reported = 1, and side 1's army rises by
    /// floor(400,000 × B ÷ (B + W)) = 200,000 on top of DR29's decrease (800,000 − 100,000 + 200,000), while side 0's falls by its loss
    /// alone (700,000). The same battle opened as an ambush by side 0 → side 0's army rises instead. With no role, nobody rises.
    /// [MOR2, D-083 DB1: both armies entered the battle, so each also pays the 50,000 entry cost, its own settlement term: the winner
    /// 800,000 − 50,000 − 100,000 + 200,000 = 850,000, the other 650,000 (lane AP's 900,000 and 700,000 failed by exactly 50,000 before
    /// this update, MEASURED). Both duel entities were placed on the field at the start: the resolver reports EnteredAtMs = StartMs for
    /// both, which is what the hand-off reads as "entered". The rise is unchanged (lane AP's observation AP-R1-14, morale proposal §11.8).]
    /// </summary>
    [Theory]
    [InlineData(1, -1, 1)]
    [InlineData(1, 0, 0)]
    [InlineData(-1, -1, -1)]
    public void DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher(int defender, int ambusher, int expected)
    {
        var r = new TacticalBattle(Duel(1, 0, 0, defender, ambusher), PassiveController.Instance, PassiveController.Instance).Run();
        Assert.Equal(BattleEndReason.BothEliminated, r.Reason);
        int winner = BattleWinner.Reported(r);
        Assert.Equal(expected, winner);
        Assert.All(r.Formations, f => Assert.Equal(0L, f.EnteredAtMs));                         // [MOR2] both entered at the start (StartMs 0)
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, winner, beaten: 2_000, winnerSide: 2_000));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 700, winner, beaten: 2_000, winnerSide: 2_000));
        MoraleRig.Run(rig, at + 1);
        for (int army = 0; army < 2; army++)
            Assert.Equal(army == expected ? 850_000 : 650_000, rig.W.Economy.MoraleOf(army));    // [MOR2] − the 50,000 entry cost each
        Assert.Equal(new[] { expected == 0 ? 200_000L : 0L, expected == 1 ? 200_000L : 0L }, rig.W.MoraleRows.OrderBy(x => x.ArmyId).Select(x => x.RisePpm).ToArray());
        Assert.All(rig.W.MoraleRows, m => Assert.Equal(50_000L, m.EntryPpm));                    // [MOR2]
    }

    // ---------------------------------------------------------------- the battery's winner bases (H-AP §4 rests on them)

    /// <summary>
    /// THE WINNER BASES of lane P's battery outcomes (POutcome.Of; lane AP's measurement of DA6/DA7 on the battery, H-AP §4, reads its
    /// winners through them): HoldsField = the side left with Active entities (lane P's committed rule P1 input, and the two-argument
    /// form), ReportedDecisive = the side a separating end names (lane MOR's reading before DA6/DA7), Reported = BattleWinner.Reported
    /// (DA6/DA7 included). On the equal duel (both sides eliminated at one instant) with side 1 the defender, HoldsField and
    /// ReportedDecisive name nobody and Reported names the defender; opened as an ambush by side 0, Reported names side 0; with no role,
    /// nobody; on a decided duel (+1 HP) every basis names the survivor, whatever the roles. Every other outcome field is the same on
    /// every basis. [AP round 1, AP-R1-09: only the bench's verify cross-check covered the Reported basis.]
    /// </summary>
    [Fact]
    public void POutcome_WinnerBases_FieldHolderDecisiveEndOrReportedWinner()
    {
        foreach (var (m0, defender, ambusher, holds, decisive, reported) in new[]
                 { (0L, 1, -1, -1, -1, 1), (0L, 1, 0, -1, -1, 0), (0L, -1, -1, -1, -1, -1), (1L, 1, -1, 0, 0, 0), (1L, 1, 1, 0, 0, 0) })
        {
            var r = new TacticalBattle(Duel(1, m0, 0, defender, ambusher), PassiveController.Instance, PassiveController.Instance).Run();
            var h = POutcome.Of(r, 0, POutcomeWinner.HoldsField);
            var d = POutcome.Of(r, 0, POutcomeWinner.ReportedDecisive);
            var p = POutcome.Of(r, 0, POutcomeWinner.Reported);
            Assert.Equal(m0 == 0 ? BattleEndReason.BothEliminated : BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal((holds, decisive, reported), (h.Winner, d.Winner, p.Winner));
            Assert.Equal(BattleWinner.Reported(r), p.Winner);
            Assert.Equal(h, POutcome.Of(r, 0));                                         // the committed form is the HoldsField basis
            Assert.Equal(h with { Winner = 9 }, d with { Winner = 9 });
            Assert.Equal(h with { Winner = 9 }, p with { Winner = 9 });
        }
        var any = new TacticalBattle(Duel(1, 0, 0, 1, -1), PassiveController.Instance, PassiveController.Instance).Run();
        Assert.Throws<ArgumentOutOfRangeException>(() => POutcome.Of(any, 0, (POutcomeWinner)3));
    }

    // ---------------------------------------------------------------- roles: validation, battery assignment, real battles

    [Fact]
    public void BadRoles_AreRefusedByBothResolvers()
    {
        foreach (var (d, a) in new[] { (2, -1), (-2, -1), (-1, 2), (-1, -2) })
        {
            var s = Duel(1, 0, 0, -1, -1) with { Defender = d, Ambusher = a };
            Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoBattle(s, new AutoCoefficients()));
        }
    }

    /// <summary>Lane P's battery: the defender of every scenario (proposal §5.4) — role B in the 13 scenarios where role A advances on
    /// role B's position (the Defend terrain family, the forest block, the two ambush-posture scenarios), none in the 16 meeting
    /// engagements — is carried by the setup in both orientations; no scenario is a battle-level ambush. The Sprint 1 #4 and #8 and
    /// lane N's amphibious battle carry side 1. The meeting scenarios of Slice D keep none.</summary>
    [Fact]
    public void ParityBatteryRoles_AreTheDocumentedOnes()
    {
        var withDefender = new[]
        {
            PScenario.ArchersVsInfantryForest, PScenario.JungleAmbush, PScenario.ForestAmbushArchers, PScenario.HillCrest, PScenario.FordCrossing,
            PScenario.MarshFront, PScenario.ForestHoldArchers, PScenario.Defile, PScenario.ArchersOnHill, PScenario.HoldLineOpen,
            PScenario.HighRidge, PScenario.NarrowFordArchers, PScenario.UrbanStreet,
        };
        int n = 0;
        foreach (var d in ParityBatteryP.All)
            foreach (ulong seed in new ulong[] { 1, 2 })
            {
                var s = ParityBatteryP.Build(d.Id, seed);
                int expect = withDefender.Contains(d.Id) ? 1 : -1;
                Assert.Equal(expect, s.Defender);
                Assert.Equal(expect, ParityBatteryP.DefenderOf(d.Id));
                Assert.Equal(-1, s.Ambusher);
                n++;
            }
        Assert.Equal(58, n);
        Assert.Equal(13, ParityBatteryP.All.Count(d => ParityBatteryP.DefenderOf(d.Id) == 1));
        Assert.Equal(1, ParityScenarios.Build(ParityScenarioId.ArchersVsInfantryForest, 1, false).Defender);
        Assert.Equal(1, ParityScenarios.Build(ParityScenarioId.Ambush, 1, true).Defender);
        Assert.Equal(-1, ParityScenarios.Build(ParityScenarioId.EqualInfantry, 1, false).Defender);
        Assert.Equal(1, MultiDomainScenarios.Amphibious(1).Defender);
        Assert.Equal(-1, Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, 1).Defender);
        Assert.Equal(-1, CombinedArms(1).Defender);
    }

    /// <summary>REAL BATTLES (lane P's jungle-ambush, tuning seeds): the defender role changes no battle — the committed outcome (the
    /// field holder) and the state hash equal those of the same setup with the role cleared — and every equal end reports the defender.
    /// Tactical seeds 5 and 9 end BothEliminated (MEASURED in lane P's tuning CSV); the AutoResolver is run on all 80 tuning seeds.</summary>
    [Fact]
    public void JungleAmbush_EqualEndsReportTheDefender_AndTheRoleChangesNoBattle()
    {
        foreach (ulong seed in new ulong[] { 5, 9 })
        {
            var s = ParityBatteryP.Build(PScenario.JungleAmbush, seed);
            var r = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()).Run();
            var r0 = new TacticalBattle(s with { Defender = -1 }, new KnowledgeAi(), new KnowledgeAi()).Run();
            Assert.Equal((BattleEndReason.BothEliminated, 1, WinBasis.DefenderOnEqualSides), (r.Reason, r.Winner, r.WinBasis));
            Assert.Equal((-1, WinBasis.None), (r0.Winner, r0.WinBasis));
            Assert.Equal(POutcome.Of(r0, 0), POutcome.Of(r, 0));
            Assert.Equal((r0.StateHash, r0.TraceHash), (r.StateHash, r.TraceHash));
        }
        int equal = 0;
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var s = ParityBatteryP.Build(PScenario.JungleAmbush, seed);
            var r = new AutoBattle(s, new AutoCoefficients()).Run();
            var r0 = new AutoBattle(s with { Defender = -1 }, new AutoCoefficients()).Run();
            Assert.Equal(r0.StateHash, r.StateHash);
            if (r.Reason == BattleEndReason.BothEliminated) { equal++; Assert.Equal((1, WinBasis.DefenderOnEqualSides), (r.Winner, r.WinBasis)); }
            else if (r.Reason == BattleEndReason.SideEliminated) Assert.Equal((1 - r.Side, WinBasis.Decisive), (r.Winner, r.WinBasis));
        }
        Assert.True(equal > 0, "no equal end in 80 auto battles");
    }
}
