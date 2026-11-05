using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Order economy across domains, turn-exact delivery of vessel orders, replay, permutation invariance, goldens.</summary>
public class DomainOrdersAndGoldenTests
{
    /// <summary>GOLDENS (Linux x64, .NET 10, measured 2026-11-05 on lane/N): the amphibious combined-arms battle, seed 42,
    /// scripted attacker + AI defender. Integer-only => expected platform-independent (cross-platform untested, O-17). They move
    /// whenever a domain rule, placeholder or event encoding changes (re-pin with attribution).
    /// RE-PINNED in verification round 1: was trace 14B8A338…F7DA / state E0AA0503…ADD1 / personnel 08280726…E85C. Cause:
    /// N-R1-06 only (an airframe at or below the damage-abort threshold is no longer relaunched). Measured attribution: the
    /// tree with only the N-R1-01 shared hull budget reproduces the old three hashes; the tree with only the N-R1-06 launch
    /// gate reproduces the new ones.
    /// RE-PINNED in verification round 2: was trace 3C5DA2E7…7944 / state 9A439187…6B48 (personnel unchanged). Cause: N-R2-03
    /// only (the side view's Ready count now applies the launch gate's fitness check). Measured attribution: the tree with only
    /// the N-R2-01 elimination change reproduces the old hashes; the tree with only N-R2-03 reproduces the new ones; the order
    /// logs differ by exactly one order — the defence DomainAi's "p14 s1 F24 Attack" (refused NotReady before) is no longer
    /// issued.
    /// [TR] RE-PINNED on the lane TR merge: was trace 5B72E757…9AAF / state 6E3963D9…44A0 / personnel E28B1BE9…C963. Causes
    /// (measured, pinned by TerrainDomainInterplayTests.TheAmphibiousGoldenMovesOnlyThroughMissileTransmissionAndTheDir5Rule):
    /// (1) the lane-TR missile transmission of the forest belt (the defending snipers deal 192 instead of 428 damage) — with
    /// only that coefficient set to One, or with the whole Slice-C-equivalent table, under the legacy inactivity model, the
    /// trace, result and personnel hashes are lane N's; (2) the DIR5 §2.6 inactivity model (Standoff after 19 pulses instead
    /// of SideDisengaged after 15); (3) the state-hash encoding, which lane TR extended with its own state.
    /// [TR] verification round 1 (verify-TR-r1 V1): AmphibState re-pinned (was 736BB5069BF4A7D550D16BC8BFEEDD1B512418EC63D4D67CC9C683BBB35414F4)
    /// because the encoding appends the march-progress goal minima. MEASURED attribution: with that appended block disabled the
    /// previous value is reproduced exactly; trace and personnel hashes did not move (the battle is unchanged).
    /// [TR] verification round 2 (verify-TR-r2 V1): AmphibState re-pinned again (was 4F258FDCF7F67BC8603ABEB480253F5BAA8EDBBAE353ACD6C524FEDCFBB1B31E)
    /// because the goal-minimum VALUES changed (remaining path length instead of the straight-line distance). MEASURED
    /// attribution: with only the minimum values left out of the appended block, the r1 and the r2 potential give the same state
    /// hash (0ACE9B0697BA2338…); trace and personnel hashes did not move.</summary>
    public const string AmphibTrace = "075371D6B76D42F78134D38790105C95096C903CE0BC1C21B51132E8684AD6C0";
    public const string AmphibState = "5F3F0088D42110F584D3FA3AFB2C8896A78512B105BEEB93578B27DBC79CFD3A";
    public const string AmphibPersonnel = "6B252A73C8FEE91A06D008DFBC61A94D9699BCE37952591422016BF1E5427103";
    /// <summary>GOLDEN: sail line battle with wind, seed 42, both sides Attack at pulse 0.</summary>
    public const string SailTrace = "8433C5E996DC2D8579E063A0E7FDC4C8466EB079DBBD65DEF248C5C3CC077685";

    [Fact]
    public void LaneNKindsAreChargedToTheSixBudgetClasses()
    {
        Assert.Equal(OrderKind.MoveTo, ActionBudget.ClassOf(OrderKind.Embark));
        Assert.Equal(OrderKind.MoveTo, ActionBudget.ClassOf(OrderKind.Disembark));
        Assert.Equal(OrderKind.MoveTo, ActionBudget.ClassOf(OrderKind.Patrol));
        Assert.Equal(OrderKind.MoveTo, ActionBudget.ClassOf(OrderKind.Escort));
        Assert.Equal(OrderKind.Attack, ActionBudget.ClassOf(OrderKind.Board));
        Assert.Equal(OrderKind.Attack, ActionBudget.ClassOf(OrderKind.Bombard));
        var b = new ActionBudget(2);
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.Board));
        Assert.Equal(BudgetRefusal.KindRepeated, b.TryAccept(OrderKind.Bombard));      // Attack class already used
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.Patrol));
        Assert.Equal(BudgetRefusal.TotalExhausted, b.TryAccept(OrderKind.Rally));      // two actions maximum
        var c = new ActionBudget(3);
        Assert.Equal(BudgetRefusal.None, c.TryAccept(OrderKind.Retreat));
        Assert.Equal(BudgetRefusal.AttackAfterRetreat, c.TryAccept(OrderKind.Board));   // no retreat-then-board chain
    }

    [Fact]
    public void VesselOrdersPassTheSameBudgetInBattle()
    {
        var s = Sea(100_000, 1, null, Ship(1, 0, DomainProfiles.Galley, M(1000, 2000)), Ship(2, 1, DomainProfiles.Galley, M(5000, 2000)));
        var c0 = Script((0, new OrderRequest(1, OrderKind.Board, 0, 2)), (0, new OrderRequest(1, OrderKind.Attack, 1, 2)),
                        (0, new OrderRequest(1, OrderKind.Patrol, 2, -1, M(2000, 2000))), (0, new OrderRequest(1, OrderKind.Hold, 3)));
        var b = new TacticalBattle(s, c0, PassiveController.Instance);
        b.RunUntilPulse(1);
        var o = b.Orders.Where(x => x.Side == 0).ToArray();
        Assert.True(o[0].Accepted);
        Assert.Equal(BudgetRefusal.KindRepeated, o[1].Budget);
        Assert.True(o[2].Accepted);
        Assert.Equal(BudgetRefusal.TotalExhausted, o[3].Budget);
        Assert.Equal(OrderKind.Patrol, b.DomainDirective(1));
    }

    /// <summary>ORDER-DELIVERY PIN (±1 neighbours): a vessel MoveTo delivered at step offset k first changes the hull's
    /// heading / speed AT pulseStart + k*StepMs, never earlier.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void VesselOrderTakesEffectAtTheDeliveredStep(int k)
    {
        var s = Sea(100_000, 2, null, Ship(1, 0, DomainProfiles.TurretWarship, M(1000, 2000)), Ship(2, 1, DomainProfiles.SteamTransport, M(6000, 3800)));
        long step = s.Rules.StepMs, tk = s.StartMs + k * step;
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.MoveTo, k, -1, M(1000, 3800)))), PassiveController.Instance);
        var seen = new SortedDictionary<long, (int H, long V)>();
        RunUntil(b, () => { var d = D(b, 1000); seen[b.Scheduler.NowMs] = (d.Heading, d.Speed); return b.Scheduler.NowMs >= tk + step; });
        var start = seen[s.StartMs];
        Assert.Equal(start, seen[tk - step]);                                       // nothing moved before delivery
        Assert.NotEqual(start, seen[tk]);                                           // turning / getting under way at delivery
    }

    private static TacticalBattle Amphib(ulong seed, bool reversed = false)
    {
        var setup = MultiDomainScenarios.Amphibious(seed);
        if (reversed)
        {
            var f = (FormationSpec[])setup.Formations.Clone();
            Array.Reverse(f);
            setup = setup with { Formations = f };
        }
        return new TacticalBattle(setup, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence());
    }

    [Fact]
    public void AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden()
    {
        var live = Amphib(42);
        var r1 = RunChecked(live);
        var replay = new TacticalBattle(MultiDomainScenarios.Amphibious(42), ScriptedController.FromLog(live.Orders, 0), ScriptedController.FromLog(live.Orders, 1));
        var r2 = RunChecked(replay);
        Assert.Equal(r1.TraceHash, r2.TraceHash);
        Assert.Equal(r1.StateHash, r2.StateHash);
        Assert.Equal(r1.ResultHash(), r2.ResultHash());
        Assert.Equal(r1.Personnel!.Hash, r2.Personnel!.Hash);
        Assert.Equal(live.Orders, replay.Orders);
        Assert.All(r1.Formations, f => Assert.True(f.Conserved));
        Assert.Equal(r1.Personnel.OpeningTotal, r1.Personnel.Total);
        // every domain took part: vessels, aircraft, landed troops, guns, snipers
        Assert.Contains(r1.Formations, f => f.Domain == Domain.Naval && f.DamageDealt > 0);
        Assert.Contains(r1.Formations, f => f.Domain == Domain.Air && f.AttacksMade > 0);
        Assert.Contains(live.Personnel.Flows, x => x.Reason == PersonnelFlowReason.Disembark);
        Assert.Equal(AmphibTrace, r1.TraceHash);
        Assert.Equal(AmphibState, r1.StateHash);
        Assert.Equal(AmphibPersonnel, r1.Personnel.Hash);
    }

    [Fact]
    public void FormationInputOrderDoesNotMatter()
    {
        var a = RunChecked(Amphib(3));
        var b = RunChecked(Amphib(3, reversed: true));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Equal(a.StateHash, b.StateHash);
    }

    [Fact]
    public void SailLineBattleGoldenAndSeedSensitivity()
    {
        BattleResult Run(ulong seed) => RunChecked(new TacticalBattle(
            MultiDomainScenarios.LineBattle(DomainProfiles.ShipOfTheLine, 50_000, seed, new DomainRules { Wind = true, WindFrom = MultiDomainScenarios.Deg(90) }),
            Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), Script((0, new OrderRequest(2, OrderKind.Attack, 0, 1)))), 400);
        var r = Run(42);
        Assert.Equal(SailTrace, r.TraceHash);
        Assert.Equal(r.TraceHash, Run(42).TraceHash);
        Assert.NotEqual(r.TraceHash, Run(43).TraceHash);
    }

    [Fact]
    public void LandOnlyBattleHasNoDomainSectionAndKeepsItsLedgerBalanced()
    {
        var b = new TacticalBattle(TacticalScenarios.CombinedArms(42), new KnowledgeAi(), new KnowledgeAi());
        var r = RunChecked(b);
        Assert.False(b.HasDomainModules);
        Assert.Empty(b.DomainEntities());
        Assert.Equal(Proto.M7.Tests.Tactical.ReplayAndMetamorphicTests.GoldenTraceHash, r.TraceHash);   // Slice D golden untouched
        foreach (var f in r.Formations)
            Assert.Equal(f.Killed, r.Personnel!.Count(f.FormationId, PersonnelPlace.Killed));
    }
}
