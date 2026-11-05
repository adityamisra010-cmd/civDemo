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
    /// issued.</summary>
    public const string AmphibTrace = "5B72E7570A0C988C45AF695EB6CC22CD61D19B334F89788024477D7C1AA39AAF";
    public const string AmphibState = "6E3963D9996E73C487C4DB1C2EADD77065BF6FB085181C39AF4065C8BFAF44A0";
    public const string AmphibPersonnel = "E28B1BE9CB570C32707A6757BBDC22F3A33891C64A15CB9C95245CA20765C963";
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
