using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

public class CombatAndKnowledgeTests
{
    private static FormationSpec One(long fid, int side, UnitProfile p, long eid, Vec2L pos)
        => new(fid, side, p, 1, pos) { Entities = new[] { new EntitySpec(eid, pos) } };

    /// <summary>Simultaneity (spec §2.3/§4): two entities that strike each other at the same instant both die.</summary>
    [Fact]
    public void MutualKillAtOneInstantKillsBoth()
    {
        var p = UnitProfiles.HeavyInfantry with { Integrity = 1 };      // TEST INPUT: 1 HP
        var rules = new TacticalRules { HitMin = Q32.One, HitMax = Q32.One };   // TEST INPUT: certain hits
        Vec2L a = Cell(20, 16), b = a + new Vec2L(1_000, 0);              // within contact (2r + reach = 1,200 mm)
        var setup = TT.Setup(OpenField(), DefaultSides(), 1, rules, One(1, 0, p, 10, a), One(2, 1, p, 20, b));
        var battle = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(battle);
        Assert.Equal(BattleEndReason.BothEliminated, r.Reason);
        Assert.Equal(0, r.EndMs);
        Assert.All(r.Formations, f => Assert.Equal(1, f.Killed));
        // trace: both Damage events precede the Resolve at instant 0
        var tr = battle.Scheduler.Trace.Entries;
        int resolve = -1, dmg = 0;
        for (int i = 0; i < tr.Count; i++)
        {
            if (tr[i].Kind == TacticalKinds.Damage) { Assert.Equal(0, tr[i].Key.TimeMs); dmg++; Assert.Equal(-1, resolve); }
            if (tr[i].Kind == TacticalKinds.Resolve && resolve < 0) resolve = i;
        }
        Assert.Equal(2, dmg);
        Assert.True(resolve > 0);
        // invalidation: the dead entities' old generation is no longer live
        Assert.False(battle.IsLive(new EntityRef(10, 0)));
        Assert.True(battle.IsLive(new EntityRef(10, battle.Entities()[0].Generation)));
    }

    /// <summary>Tie-dense targeting: four equidistant detected enemies — the LOWEST id is shot, for every input order.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void NearestTargetTieBreaksOnLowestId(ulong perm)
    {
        Vec2L c = Cell(20, 16);
        const long d = 20_000;   // 1 cell; archers' range is 2 cells
        var pts = new[] { new EntitySpec(907, c + new Vec2L(d, 0)), new EntitySpec(903, c + new Vec2L(-d, 0)),
                          new EntitySpec(905, c + new Vec2L(0, d)), new EntitySpec(901, c + new Vec2L(0, -d)) };
        // one single-entity formation per target (anchor = its position, so nobody drifts to a slot); formation ids are
        // deliberately NOT in entity-id order, so the tie-break provably uses the ENTITY id
        long[] fids = { 2, 5, 3, 4 };
        var forms = new List<FormationSpec> { One(1, 0, UnitProfiles.Archers, 50, c) };
        var order = TestSupport.Permutation(4, perm);
        for (int i = 0; i < 4; i++) forms.Add(One(fids[order[i]], 1, UnitProfiles.HeavyInfantry, pts[order[i]].Id, pts[order[i]].Pos));
        var setup = TT.Setup(OpenField(), DefaultSides(), 9, null, forms.ToArray());
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        b.Start();
        while (b.Advance() && !b.Scheduler.Trace.Entries.Any(e => e.Kind == TacticalKinds.Fire)) { }
        var fire = b.Scheduler.Trace.Entries.First(e => e.Kind == TacticalKinds.Fire);
        Assert.Equal(50, fire.Source.Id);
        Assert.Equal(901, fire.Target.Id);
        Assert.Equal(0, fire.Key.TimeMs);
    }

    /// <summary>Concealment: a stationary entity deep in jungle is never acquired when the stationary factor is 0 (TEST INPUT);
    /// the same entity in the open is acquired at the first detection instant.</summary>
    [Fact]
    public void StationaryConcealedEntityIsNotDetectedButOpenOneIs()
    {
        var rules = new TacticalRules { StationaryConcealment = Q32.Zero };
        FormationSpec obs = One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(20, 16));
        FormationSpec hid = One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(28, 16));     // 8 cells: inside sight (12)
        var g = OpenField();
        TT.Fill(g, 26, 14, 30, 18, TerrainClass.Jungle);
        var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 4, rules, obs, hid), PassiveController.Instance, PassiveController.Instance);
        b.RunUntilPulse(3);
        Assert.False(b.EverDetected(0, 20));
        var open = new TacticalBattle(TT.Setup(OpenField(), DefaultSides(), 4, rules, obs, hid), PassiveController.Instance, PassiveController.Instance);
        open.Start();
        while (open.Advance() && !open.Scheduler.Trace.Entries.Any(e => e.Kind == TacticalKinds.Detect)) { }
        open.Advance();   // execute the first Detect
        Assert.True(open.IsTracked(0, 20));

        // The FACTOR itself (verify-D F7): above, jungle clarity alone (three jungle cells on the line) can keep the target
        // hidden for 3 pulses, so that half cannot tell whether the factor is applied. Here the target's ONLY cover is its own
        // jungle cell (clarity 1/4 per draw, 90 draws in 3 pulses). With the factor removed (StationaryConcealment = 1, the
        // control) clarity alone lets it be acquired; with factor 0 the SAME stationary target is never acquired. A mutant that
        // ignores the factor (p = best clarity) acquires it in both runs.
        bool Acquired(Q32 factor)
        {
            var g1 = OpenField();
            g1.Set(28, 16, TerrainClass.Jungle);
            var bb = new TacticalBattle(TT.Setup(g1, DefaultSides(), 4, new TacticalRules { StationaryConcealment = factor }, obs, hid),
                                        PassiveController.Instance, PassiveController.Instance);
            bb.RunUntilPulse(3);
            Assert.Equal(EntityState.Active, bb.Entities().Single(e => e.Id == 20).State);
            return bb.EverDetected(0, 20);
        }
        Assert.True(Acquired(Q32.One));      // control: clarity alone does not hide it
        Assert.False(Acquired(Q32.Zero));    // the stationary-concealment factor does
    }

    /// <summary>Knowledge-only AI (metamorphic): moving an UNDETECTED enemy formation elsewhere (still undetected) cannot change
    /// the AI's decisions.</summary>
    [Fact]
    public void AiDecisionsDependOnlyOnKnowledge()
    {
        IReadOnlyList<AiDecision> Run(Vec2L hiddenAt, out bool detected)
        {
            var ai = new KnowledgeAi();
            var setup = TT.Setup(OpenField(), DefaultSides(new[] { Cell(34, 16) }), 8, null,
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)),
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, hiddenAt));
            var b = new TacticalBattle(setup, ai, PassiveController.Instance);
            b.RunUntilPulse(3);
            detected = Enumerable.Range(0, 20).Any(k => b.EverDetected(0, 2000 + k));
            return ai.Decisions.ToArray();
        }
        var d1 = Run(Cell(44, 4), out bool det1);
        var d2 = Run(Cell(44, 27), out bool det2);
        Assert.False(det1); Assert.False(det2);
        Assert.Equal(d1, d2);
        Assert.Contains(d1, x => x.Issued && x.Order.Kind == OrderKind.MoveTo);
    }

    [Fact]
    public void AiTargetChoiceTieBreaksOnFormationThenEntityId()
    {
        var own = new[] { new OwnFormationView(1, Posture.Normal, OrderKind.Hold, -1, default, 1000, 10, 10, 0, Cell(10, 10), Cell(10, 10), 0, 2) };
        // three known enemies at the SAME distance; formations 7 and 5 -> 5 wins (then entity id)
        var known = new[]
        {
            new KnownEnemy(7001, 7, Cell(12, 10), 0, true),
            new KnownEnemy(5009, 5, Cell(8, 10), 0, true),
            new KnownEnemy(5003, 5, Cell(10, 12), 0, false),
        };
        var ai = new KnowledgeAi();
        var orders = new List<OrderRequest>();
        ai.Decide(new SideView(0, 0, 0, CellMm, own, known, Array.Empty<Vec2L>()), orders);
        Assert.Single(orders);
        Assert.Equal(OrderKind.Attack, orders[0].Kind);
        Assert.Equal(5, orders[0].TargetFormationId);
        // objectives tie: equal distance -> lower index
        var ai2 = new KnowledgeAi(); orders.Clear();
        ai2.Decide(new SideView(0, 0, 0, CellMm, own, Array.Empty<KnownEnemy>(), new[] { Cell(10, 14), Cell(10, 6) }), orders);
        Assert.Equal(Cell(10, 14), orders[0].Pos);
    }

    /// <summary>Instant-exact order delivery pin: an order for step k of pulse p takes effect at pulseStart + k*StepMs —
    /// not before, not at the pulse boundary.</summary>
    [Fact]
    public void OrderTakesEffectAtItsStepInstant()
    {
        var setup = Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 2);
        long pulseMs = setup.Rules.PulseMs, stepMs = setup.Rules.StepMs;
        var s0 = new ScriptedController().Add(1, new OrderRequest(1, OrderKind.MoveTo, 3, -1, Cell(16, 16)));
        var b = new TacticalBattle(setup, s0, PassiveController.Instance);
        long due = pulseMs + 3 * stepMs;
        b.Start();
        while (b.Advance())
        {
            var last = b.Scheduler.Trace.Entries[^1];
            if (last.Key.TimeMs < due || (last.Key.TimeMs == due && last.Kind != TacticalKinds.Order)) Assert.Equal(OrderKind.Hold, b.Directive(1));
            if (last.Kind == TacticalKinds.Order) { Assert.Equal(due, last.Key.TimeMs); break; }
        }
        Assert.Equal(OrderKind.MoveTo, b.Directive(1));
        Assert.Equal(due, b.Orders.Single().AtMs);
        // the next Step at the same instant (Arrival class) already moves the anchor
        Vec2L before = b.Anchor(1);
        while (b.Advance() && b.Scheduler.Trace.Entries[^1].Kind != TacticalKinds.Step) { }
        Assert.Equal(due, b.Scheduler.Trace.Entries[^1].Key.TimeMs);
        Assert.NotEqual(before, b.Anchor(1));
    }

    /// <summary>Retreat order: orderly withdrawal to the exit edge, entities Escaped (not routed), preserved as survivors.</summary>
    [Fact]
    public void RetreatOrderWithdrawsToTheEdgeAndEscapes()
    {
        var setup = TT.Setup(OpenField(), DefaultSides(), 3, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(3, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(40, 16)));
        var r = TT.RunGuarded(new TacticalBattle(setup, new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Retreat)), PassiveController.Instance));
        var f1 = r.Formations[0];
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.Equal(20, f1.Escaped);
        Assert.Equal(0, f1.RoutedEscaped);
        Assert.Equal(0, f1.DisengagedEscaped);
        Assert.Equal(20, f1.Survivors);
        TT.AssertConserved(r);
    }

    /// <summary>Rout: a formation whose morale falls below the threshold routs; its routing entities flee and escape.</summary>
    [Fact]
    public void CasualtiesBreakMoraleAndTheFormationRouts()
    {
        var rules = new TacticalRules();
        Vec2L hi = Cell(20, 16);
        var setup = TT.Setup(OpenField(), DefaultSides(), 6, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, hi) { Morale = rules.RoutThreshold + 10 },
            new FormationSpec(2, 1, UnitProfiles.Archers, 20, hi + new Vec2L(30_000, 0)));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(b);
        var f1 = r.Formations[0];
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.True(f1.Killed >= 1);
        Assert.True(f1.RoutedEscaped >= 1);
        Assert.True(f1.MoraleEnd < rules.RoutThreshold);
        TT.AssertConserved(r);
    }

    /// <summary>Surrender: routing entities with no passable path to their exit edge (an island ringed by water) surrender.</summary>
    [Fact]
    public void RoutedEntitiesWithNoPathToTheEdgeSurrender()
    {
        var g = OpenField();
        TT.Fill(g, 19, 14, 23, 18, TerrainClass.Water);
        TT.Fill(g, 20, 15, 22, 17, TerrainClass.Open);      // 3x3 island, no land path to any edge
        var rules = new TacticalRules();
        var setup = TT.Setup(g, DefaultSides(), 2, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(22, 16) + new Vec2L(6_000, 0)) { Morale = rules.RoutThreshold + 10 },
            new FormationSpec(2, 1, UnitProfiles.Archers, 20, Cell(24, 16) + new Vec2L(-8_000, 0)));
        var r = TT.RunGuarded(new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance));
        var f1 = r.Formations[0];
        Assert.Equal(0, r.Side);
        Assert.True(f1.Killed >= 1);
        Assert.True(f1.Surrendered >= 1);
        Assert.Equal(0, f1.Survivors);
        Assert.Equal(f1.Start, f1.Killed + f1.Surrendered);
    }

    /// <summary>Rally/Heal (placeholder): HP regenerates HealPerStep per step from a BOUNDED pool (pool = num/den x MaxHp).</summary>
    [Theory]
    [InlineData(1L, 2L, 50L)]    // pool 25: 35 + 15 = 50 (capped at MaxHp)
    [InlineData(1L, 10L, 40L)]   // pool 5:  35 + 5  = 40 (pool exhausted)
    public void RallyHealsFromABoundedPool(long num, long den, long expectHp)
    {
        var rules = new TacticalRules { HitMin = Fixed.Q32.One, HitMax = Fixed.Q32.One, HealPoolNum = num, HealPoolDen = den };   // TEST INPUTS
        Vec2L c = Cell(20, 16);
        var setup = TT.Setup(OpenField(), DefaultSides(), 3, rules,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, c), One(2, 1, UnitProfiles.Archers, 20, c + new Vec2L(30_000, 0)));
        var b = new TacticalBattle(setup,
            new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Rally)),
            new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Retreat, 1)));   // one volley, then withdraws
        b.RunUntilPulse(1);
        var hi = b.Entities().Single(e => e.Id == 10);
        Assert.Equal(expectHp, hi.Hp);
        Assert.Equal(EntityState.Active, hi.State);
    }

    /// <summary>Exact conservation (no epsilon) over scenarios x seeds; every battle terminates (no pulse cap).</summary>
    [Fact]
    public void AggregationConservesExactlyAcrossSeeds()
    {
        var reasons = new HashSet<BattleEndReason>();
        long start = 0, accounted = 0;
        for (ulong s = 1; s <= 8; s++)
        {
            foreach (var setup in new[]
            {
                Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s),
                Meeting(UnitProfiles.Archers, 60, UnitProfiles.Archers, 60, s),
                Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.Archers, 60, s),
                CombinedArms(s),
            })
            {
                var r = TT.RunGuarded(new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi()));
                Assert.NotEqual(BattleEndReason.None, r.Reason);
                TT.AssertConserved(r);
                reasons.Add(r.Reason);
                foreach (var f in r.Formations) { start += f.Start; accounted += f.Survivors + f.Killed + f.Surrendered; }
            }
        }
        Assert.Equal(start, accounted);
        Assert.True(reasons.Count >= 1);
    }
}
