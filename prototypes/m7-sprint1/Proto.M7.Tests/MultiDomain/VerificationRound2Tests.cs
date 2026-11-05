using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>
/// Lane N verification round 2: tests written to reproduce the round-2 findings against the tree, then kept as the semantic
/// tests of the fixes (N-R2-01 .. N-R2-03: the damage-grounded airframe is "cannot fly" everywhere — elimination, side view,
/// take-off loop).
/// </summary>
public class VerificationRound2Tests
{
    private const long Cell = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell);
    private static TerrainGrid Open() => new(64, 40, Cell, TerrainClass.Open);

    /// <summary>TEST INPUT: an AA gun that always hits for 16 (30 HP airframe -> 14, below the 1/2 abort threshold).</summary>
    private static FormationSpec Gun(long reloadMs = 4_000)
        => new(5, 1, DomainProfiles.GunCrew, 1, C(45, 20))
        { Weapons = new[] { DomainProfiles.AaGun with { Accuracy = 1_000, Damage = 16, ReloadMs = reloadMs } } };

    /// <summary>Records the side's own-air view of formation 3 each pulse, and issues scripted orders.</summary>
    private sealed class Recorder : IFormationController
    {
        private readonly ScriptedController _s;
        public readonly List<(long Pulse, OwnAirFormationView Air)> Seen = new();
        public Recorder(ScriptedController s) => _s = s;
        public void Decide(SideView view, List<OrderRequest> orders)
        {
            foreach (var a in view.Domain.Air) if (a.Id == 3) Seen.Add((view.Pulse, a));
            _s.Decide(view, orders);
        }
    }

    // ---------------------------------------------------------------- N-R2-01

    /// <summary>
    /// N-R2-01: elimination counts aircraft that can still fly. An airframe grounded by damage (no repair in battle) cannot,
    /// so a side whose only force is such an airframe is eliminated exactly as when the same airframe has no sortie left —
    /// the number of unused sorties must not change the outcome.
    /// </summary>
    [Fact]
    public void ASideLeftOnlyWithADamageGroundedAircraftIsEliminated()
    {
        (BattleResult R, DomainEntityView D) Run(int sorties)
        {
            var ftr = DomainProfiles.Fighter with { Sorties = sorties, TurnaroundMs = 1_000 };
            var setup = OnGrid(Open(), 12, null, null, MultiDomainScenarios.Flight(3, 0, ftr, 1), Gun());
            var b = new TacticalBattle(setup, Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20)))), PassiveController.Instance);
            var r = RunChecked(b, 40);
            return (r, D(b, 3000));
        }
        var (r1, d1) = Run(1);
        var (r3, d3) = Run(3);
        foreach (var d in new[] { d1, d3 })
        {
            Assert.NotEqual(EntityState.Dead, d.State);         // landed (at the end it leaves with its base), not shot down
            Assert.Equal(AirPhase.Based, d.Phase);
            Assert.Equal(14, d.Hp);
        }
        Assert.Equal(0, d1.SortiesLeft);
        Assert.Equal(2, d3.SortiesLeft);                         // sorties left, but it can never fly again
        Assert.True(r1.Reason == r3.Reason && r1.Side == r3.Side && r1.HoldsField == r3.HoldsField,
            $"sorties=1: {r1.Reason} side={r1.Side} holds={r1.HoldsField} pulses={r1.Pulses}; sorties=3: {r3.Reason} side={r3.Side} holds={r3.HoldsField} pulses={r3.Pulses}");
        foreach (var r in new[] { r1, r3 })
        {
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(0, r.Side);
            Assert.Equal(1, r.HoldsField);
        }
        Assert.Equal(r1.Pulses, r3.Pulses);
        Assert.Equal(r1.EndMs, r3.EndMs);
    }

    // ---------------------------------------------------------------- N-R2-02

    /// <summary>
    /// N-R2-02: in a multi-airframe formation the order validator accepts a launch order when ANY airframe is fit and ready;
    /// the take-off loop must still keep the damaged one on the ground. Two attack helicopters patrol over the gun (TEST INPUT:
    /// it fires once — 600 s reload); one is hit and both are recalled; the second patrol order launches only the fit one, and
    /// the damaged airframe neither takes off nor spends a sortie for the rest of the battle.
    /// </summary>
    [Fact]
    public void TheTakeOffLoopKeepsADamagedAirframeOnTheGroundWhenItsWingmanLaunches()
    {
        var helo = DomainProfiles.AttackHelicopter with { Sorties = 3, TurnaroundMs = 1_000 };
        var setup = OnGrid(Open(), 12, null, null, MultiDomainScenarios.Flight(3, 0, helo, 2), Gun(reloadMs: 600_000));
        var c0 = Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20))),
                        (1, new OrderRequest(3, OrderKind.Retreat, 0, -1, default)),
                        (4, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20))));
        var b = new TacticalBattle(setup, c0, PassiveController.Instance);
        long dmg = -1; int sorties = -1, gen = -1; bool fitRelaunched = false;
        RunUntil(b, () =>
        {
            if (dmg < 0)
                foreach (long id in new long[] { 3000, 3001 })
                {
                    var d = D(b, id);
                    if (d.Phase == AirPhase.Based && d.Hp < helo.Hp && b.CurrentPulse < 4) { dmg = id; sorties = d.SortiesLeft; gen = d.Generation; }
                }
            if (dmg >= 0)
            {
                var d = D(b, dmg);
                Assert.Equal(AirPhase.Based, d.Phase);
                Assert.Equal(sorties, d.SortiesLeft);
                Assert.Equal(gen, d.Generation);
                var w = D(b, dmg == 3000 ? 3001 : 3000);
                if (b.CurrentPulse >= 4 && w.Phase == AirPhase.Airborne) fitRelaunched = true;
            }
            return b.CurrentPulse >= 6;
        }, 8);
        Assert.True(dmg >= 0, "no helicopter was damaged and landed before the second patrol order");
        Assert.True(fitRelaunched, "the fit wingman did not launch on the second patrol order");
        var second = b.Orders.Single(o => o.Request.FormationId == 3 && o.Pulse == 4);
        Assert.Equal(OrderInvalid.None, second.Invalid);        // accepted: only the take-off loop holds the damaged one back
    }

    // ---------------------------------------------------------------- N-R2-03

    /// <summary>
    /// N-R2-03: the side view's Ready count is the validator's launch condition — an airframe grounded by damage is not ready.
    /// Every pulse after the damaged fighter lands, the view shows it Based and not Ready, and the launch order the
    /// controller issues regardless is refused NotReady (view and validator agree).
    /// </summary>
    [Fact]
    public void TheSideViewDoesNotCountADamageGroundedAirframeAsReady()
    {
        var ftr = DomainProfiles.Fighter with { Sorties = 3, TurnaroundMs = 1_000 };
        var s = new ScriptedController();
        for (int p = 0; p < 8; p++) s.Add(p, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20)));
        var rec = new Recorder(s);
        // A land formation keeps side 0 in the battle after its fighter is grounded (so the view is still produced).
        var inf = new FormationSpec(1, 0, DomainProfiles.Riflemen, 5, C(2, 2));
        var setup = OnGrid(Open(), 12, null, null, MultiDomainScenarios.Flight(3, 0, ftr, 1), Gun(), inf);
        var b = new TacticalBattle(setup, rec, PassiveController.Instance);
        long landedPulse = -1;
        RunUntil(b, () =>
        {
            var d = D(b, 3000);
            if (landedPulse < 0 && d.Phase == AirPhase.Based && d.Hp < ftr.Hp) landedPulse = b.CurrentPulse;
            return b.CurrentPulse >= 6;
        }, 8);
        Assert.True(landedPulse >= 0, "the fighter was never damaged and landed");
        var after = rec.Seen.Where(x => x.Pulse > landedPulse).ToArray();
        Assert.NotEmpty(after);
        foreach (var (p, a) in after)
        {
            Assert.Equal(1, a.Based);
            Assert.Equal(0, a.Ready);
            Assert.Equal(2, a.SortiesLeft);
            var o = b.Orders.Single(x => x.Request.FormationId == 3 && x.Pulse == p);
            Assert.Equal(OrderInvalid.NotReady, o.Invalid);
        }
    }

    // ---------------------------------------------------------------- N-R2-04

    /// <summary>
    /// N-R2-04: a hull does not bank idle loading time. In pulse 0 the hull lands formation 2 — one man, 1 000 ms of its
    /// 2 000 ms step (TEST INPUT: DisembarkMsPerPerson = 1 000) — so it is credited but not time-bound, and the 1 000 ms it did
    /// not use is dropped. Formation 3 (10 men, same hull) is ordered to land a pulse later and lands exactly
    /// floor(StepMs / DisembarkMs) = 2 per step from its first step; a banked remainder would land 3 in that first step.
    /// </summary>
    [Fact]
    public void AHullDoesNotBankIdleLoadingTime()
    {
        long step = new TacticalRules().StepMs;
        var t = DomainProfiles.SteamTransport with { Capacity = 30, DisembarkMsPerPerson = 1_000 };
        var setup = OnGrid(Coast(Cell, 20), 1, null, null,
            Ship(1, 0, t, C(19, 20)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 1, C(20, 20)) { EmbarkedOn = 1 },
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 10, C(20, 22)) { EmbarkedOn = 1 },
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20))),
                                                 (1, new OrderRequest(3, OrderKind.Disembark, 0, -1, C(25, 22)))), PassiveController.Instance);
        long Landed(long formation, long now)
        {
            long n = 0;
            foreach (var f in b.Personnel.Flows)
                if (f.Reason == PersonnelFlowReason.Disembark && f.AtMs == now && b.Personnel.AccountInfo(f.To).FormationId == formation) n += f.Amount;
            return n;
        }
        long first2 = -1; var series3 = new List<long>();
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            if ((now - setup.StartMs) % step != 0) return false;
            if (first2 < 0 && Landed(2, now) > 0) { first2 = now; Assert.Equal(1, Landed(2, now)); }
            long l3 = Landed(3, now);
            if (l3 > 0 || series3.Count > 0) series3.Add(l3);
            return series3.Count > 0 && b.Entities().Count(e => e.FormationId == 3 && e.State == EntityState.Embarked) == 0;
        }, 6);
        Assert.True(first2 >= 0, "formation 2 never landed: the idle-credit premise was not exercised");
        Assert.Equal(new long[] { 2, 2, 2, 2, 2 }, series3.ToArray());
        Assert.Null(b.PersonnelConsistency());
    }

    // ---------------------------------------------------------------- N-R2-05 (order-delivery pins, ±1 neighbours)

    /// <summary>
    /// ORDER-DELIVERY PIN (±1 neighbours) for the vessel orders not pinned in round 1: Patrol, Board, Escort. Delivered at step
    /// offset k, the order becomes the formation's directive AT pulseStart + k*StepMs, and the hull first turns / gets under
    /// way at that instant, never earlier.
    /// </summary>
    [Theory]
    [InlineData(OrderKind.Patrol, 3)] [InlineData(OrderKind.Patrol, 4)] [InlineData(OrderKind.Patrol, 5)]
    [InlineData(OrderKind.Board, 3)] [InlineData(OrderKind.Board, 4)] [InlineData(OrderKind.Board, 5)]
    [InlineData(OrderKind.Escort, 3)] [InlineData(OrderKind.Escort, 4)] [InlineData(OrderKind.Escort, 5)]
    public void VesselOrderKindsTakeEffectAtTheDeliveredStep(OrderKind kind, int k)
    {
        var s = Sea(100_000, 2, null,
            Ship(1, 0, DomainProfiles.Galley, M(1000, 2000)),
            Ship(2, 1, DomainProfiles.Galley, M(6000, 3800)),
            Ship(3, 0, DomainProfiles.SteamTransport, M(1000, 3800)));
        long step = s.Rules.StepMs, tk = s.StartMs + k * step;
        var o = kind switch
        {
            OrderKind.Patrol => new OrderRequest(1, OrderKind.Patrol, k, -1, M(3000, 1000)),
            OrderKind.Board => new OrderRequest(1, OrderKind.Board, k, 2),
            _ => new OrderRequest(1, OrderKind.Escort, k, 3),
        };
        var b = new TacticalBattle(s, Script((0, o)), PassiveController.Instance);
        var seen = new SortedDictionary<long, (int H, long V, OrderKind Dir)>();
        RunUntil(b, () => { var d = D(b, 1000); seen[b.Scheduler.NowMs] = (d.Heading, d.Speed, b.DomainDirective(1)); return b.Scheduler.NowMs >= tk + step; });
        Assert.True(b.Orders.Single(x => x.Side == 0).Accepted);
        var start = seen[s.StartMs];
        Assert.NotEqual(kind, start.Dir);
        Assert.Equal(start, seen[tk - step]);                                       // nothing changed before delivery
        Assert.Equal(kind, seen[tk].Dir);                                           // the directive at delivery
        Assert.NotEqual((start.H, start.V), (seen[tk].H, seen[tk].V));              // turning / getting under way at delivery
    }

    /// <summary>
    /// ORDER-DELIVERY PIN (±1 neighbours): shore bombardment delivered at step offset k fires its first area round AT
    /// pulseStart + k*StepMs (the target is in range and the guns loaded from the start), never earlier.
    /// </summary>
    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void BombardFiresFirstAtTheDeliveredStep(int k)
    {
        var s = OnGrid(Coast(Cell, 20), 3, null, null,
            Ship(1, 0, DomainProfiles.TurretWarship with { SightMm = 100_000 }, C(10, 20)),   // TEST INPUT: it cannot see the riflemen,
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 40, C(24, 20)));                  // so it makes no aimed fire before the order
        long step = s.Rules.StepMs, tk = s.StartMs + k * step;
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, k, -1, C(24, 20)))), PassiveController.Instance);
        RunUntil(b, () => b.Scheduler.NowMs >= tk + step);
        Assert.True(b.Orders.Single(x => x.Side == 0).Accepted);
        var shots = Executed(b, DomainKinds.DomainFire).Where(e => e.Source.Id == 1000).ToArray();
        Assert.All(shots, e => Assert.Equal(EntityRef.None.Id, e.Target.Id));      // every round is area fire at the ordered point
        var area = shots.Select(e => e.Key.TimeMs).ToArray();
        Assert.NotEmpty(area);
        Assert.Equal(tk, area.Min());                                               // none at tk - step; the first at tk
    }

    /// <summary>
    /// ORDER-DELIVERY PIN (±1 neighbours) for the air orders not pinned in round 1: Attack (strike aircraft on the enemy
    /// infantry) and Escort (fighter over its own infantry). Delivered at step offset k, the aircraft takes off exactly at
    /// pulseStart + k*StepMs and the directive changes then, never earlier.
    /// </summary>
    [Theory]
    [InlineData(OrderKind.Attack, 5)] [InlineData(OrderKind.Attack, 6)] [InlineData(OrderKind.Attack, 7)]
    [InlineData(OrderKind.Escort, 5)] [InlineData(OrderKind.Escort, 6)] [InlineData(OrderKind.Escort, 7)]
    public void AirOrderKindsTakeEffectAtTheDeliveredStep(OrderKind kind, int k)
    {
        var plane = kind == OrderKind.Attack ? DomainProfiles.StrikeAircraft : DomainProfiles.Fighter;
        var setup = OnGrid(Open(), 7, null, null,
            new FormationSpec(1, 0, DomainProfiles.Riflemen, 5, C(5, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 5, C(58, 20)),
            MultiDomainScenarios.Flight(3, 0, plane, 1));
        long step = setup.Rules.StepMs, tk = setup.StartMs + k * step;
        var o = new OrderRequest(3, kind, k, kind == OrderKind.Attack ? 2 : 1);
        var b = new TacticalBattle(setup, Script((0, o)), PassiveController.Instance);
        var seen = new SortedDictionary<long, (AirPhase P, OrderKind Dir)>();
        RunUntil(b, () => { seen[b.Scheduler.NowMs] = (D(b, 3000).Phase, b.DomainDirective(3)); return b.Scheduler.NowMs >= tk + step; });
        Assert.True(b.Orders.Single(x => x.Side == 0).Accepted);
        Assert.Equal(AirPhase.Based, seen[tk - step].P);
        Assert.NotEqual(kind, seen[tk - step].Dir);
        Assert.Equal(AirPhase.Airborne, seen[tk].P);
        Assert.Equal(kind, seen[tk].Dir);
    }

    // ---------------------------------------------------------------- N-R2-07

    /// <summary>Times of the rams galley 1000 makes on a line of three hulls 40 m apart that a full-speed ram sinks (TEST
    /// INPUT: unarmed, hull 50): after each impact the rammer stops and must regain ramming speed before the next hull.</summary>
    private static long[] RamTimes(long cooldownMs, long pulses = 6)
    {
        var g = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamCooldownMs = cooldownMs };
        var target = DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0, Hull = 50 };
        var tf = MultiDomainScenarios.Fleet(2, 1, target, 3, M(1110, 1000)) with
        { Entities = new[] { new EntitySpec(2000, M(1110, 960)), new EntitySpec(2001, M(1110, 1000)), new EntitySpec(2002, M(1110, 1040)) } };
        var s = Sea(50_000, 4, null, Ship(1, 0, g, M(1000, 1000)), tf);
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        RunUntil(b, () => b.CurrentPulse >= pulses, pulses + 2);
        return Executed(b, DomainKinds.Ram).Where(e => e.Source.Id == 1000).Select(e => e.Key.TimeMs).ToArray();
    }

    /// <summary>
    /// N-R2-07: the ram cooldown (RamCooldownMs, 30 s) spaces a hull's rams. Against a single hull it never binds (measured:
    /// after the impact the rammer is held in contact at speed 0 and never regains ramming speed there), but when the first
    /// hull sinks and the next one lies 40 m on, the rammer is back at ramming speed within ten seconds: with a 2 s cooldown
    /// (TEST INPUT) it rams again at once; with the 30 s cooldown no two of its rams are closer than 30 s.
    /// </summary>
    [Fact]
    public void TheRamCooldownSpacesAHullsRams()
    {
        var quick = RamTimes(2_000);
        var real = RamTimes(DomainProfiles.Galley.RamCooldownMs);
        Assert.Equal(30_000, DomainProfiles.Galley.RamCooldownMs);
        Assert.True(quick.Length >= 2 && quick[1] - quick[0] < 30_000, $"premise: the scenario must allow a quick second ram ([{string.Join(",", quick)}])");
        Assert.Equal(quick[0], real[0]);                                            // identical up to the first impact
        for (int i = 1; i < real.Length; i++) Assert.True(real[i] - real[i - 1] >= 30_000, $"rams at {real[i - 1]} and {real[i]}");
        Assert.DoesNotContain(quick[1], real);
    }

    // ---------------------------------------------------------------- N-R2-08

    /// <summary>
    /// N-R2-08: a sniper's personal concealment is lifted for the step after it fires. Heavy infantry stands 160 m from a
    /// sniper on open ground (inside its 240 m sight); the sniper's camouflage is a TEST INPUT of 2^-20, so silent it is never
    /// found in three pulses. The same sniper shooting at them is found: each shot exposes it at full clarity for one step.
    /// </summary>
    [Fact]
    public void ASniperIsExposedForTheStepAfterItFires()
    {
        (bool Found, int Shots) Run(bool fires)
        {
            var p = DomainProfiles.Sniper with { PersonalConcealment = Q32.FromRatio(1, 1 << 20) };
            if (!fires) p = p with { Ranged = 0, RangeCells = 0 };
            var s = new BattleSetup(TacticalScenarios.OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                TacticalScenarios.DefaultSides(),
                new[]
                {
                    new FormationSpec(1, 0, p, 1, TacticalScenarios.Cell(10, 16)),
                    new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, TacticalScenarios.Cell(18, 16)),
                }, 9);
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            b.RunUntilPulse(3);
            int shots = b.Scheduler.Trace.Entries.Count(e => e.Kind == TacticalKinds.Fire && e.Outcome == Proto.M7.Events.TraceOutcome.Executed && e.Source.Id == 1000);
            return (b.EverDetected(1, 1000), shots);
        }
        var silent = Run(false);
        var firing = Run(true);
        Assert.Equal(0, silent.Shots);
        Assert.False(silent.Found);
        Assert.True(firing.Shots > 0, "the sniper never fired");
        Assert.True(firing.Found);
    }
}
