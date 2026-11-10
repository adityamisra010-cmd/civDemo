using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Air module: recon under fog, fuel / bingo, no hovering, sorties and turnaround, interception, air defence.</summary>
public class AirTests
{
    private const long Cell = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell);

    /// <summary>Open land 64 x 40 x 50 m with a 40 m ridge at column 30 (rows 5-34): no ground line of sight across it.</summary>
    private static TerrainGrid Ridge()
    {
        var g = new TerrainGrid(64, 40, Cell, TerrainClass.Open);
        for (int y = 5; y < 35; y++) g.Set(30, y, TerrainClass.Hills, 40_000);
        return g;
    }

    private static BattleSetup Land(ulong seed, params FormationSpec[] extra) => Land(seed, null, extra);

    /// <summary>obj: objectives for BOTH sides at opposite far corners (their KnowledgeAi then keep marching apart for ~20
    /// pulses, which keeps the battle going without contact).</summary>
    private static BattleSetup Land(ulong seed, Vec2L? obj1, params FormationSpec[] extra)
    {
        var forms = new List<FormationSpec>
        {
            new(1, 0, DomainProfiles.Riflemen, 10, C(25, 20)),
            new(2, 1, DomainProfiles.Riflemen, 10, C(36, 20)),       // behind the ridge, 550 m away
        };
        forms.AddRange(extra);
        var setup = OnGrid(Ridge(), seed, null, null, forms.ToArray());
        if (obj1 is { } o) setup = setup with { Sides = MultiDomainScenarios.Sides(new[] { C(0, 39) }, new[] { o }) };
        return setup;
    }

    [Fact]
    public void ReconAircraftRevealsAFormationHiddenFromTheGround()
    {
        var recon = MultiDomainScenarios.Flight(3, 0, DomainProfiles.ReconAircraft, 1);
        bool Seen(bool fly)
        {
            var c0 = fly ? Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(36, 20)))) : new ScriptedController();
            var b = new TacticalBattle(Land(4, recon), c0, PassiveController.Instance);
            b.RunUntilPulse(3);
            return b.EverDetected(0, 2000);
        }
        Assert.False(Seen(false));          // the ridge hides it from every ground observer
        Assert.True(Seen(true));            // an aircraft looks down past the ridge
    }

    [Fact]
    public void BingoFuelBringsTheAircraftHomeWhateverItIsOrdered()
    {
        // TEST INPUT: two minutes of fuel, so bingo comes well inside the five pulses this contactless battle lasts (on
        // station the fighter loiters at minimum speed, burning half the cruise rate).
        var shortLegs = DomainProfiles.Fighter with { EnduranceMs = 120_000 };
        var f = MultiDomainScenarios.Flight(3, 0, shortLegs, 1);
        var c0 = new ScriptedController();
        for (int p = 0; p < 12; p++) c0.Add(p, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(60, 35)));   // keep ordering it out
        var b = new TacticalBattle(Land(5, f), c0, PassiveController.Instance);
        bool wasUp = false, landed = false, returned = false;
        RunUntil(b, () =>
        {
            var d = D(b, 3000);
            Assert.NotEqual(EntityState.Dead, d.State);                              // never runs dry in the air
            if (d.Phase != AirPhase.Based) { wasUp = true; Assert.True(d.Fuel > 0); }
            if (d.Phase == AirPhase.Returning) returned = true;
            if (wasUp && d.Phase == AirPhase.Based) { landed = true; Assert.Equal(shortLegs.EnduranceMs, d.Fuel); }
            return landed;
        }, 12);
        Assert.True(returned && landed);
    }

    [Fact]
    public void FixedWingCannotHoverRotaryHoverBurnsFuel()
    {
        var fw = MultiDomainScenarios.Flight(3, 0, DomainProfiles.Fighter, 1);
        var heli = MultiDomainScenarios.Flight(4, 0, DomainProfiles.AttackHelicopter, 1);
        var setup = Land(6, fw, heli);
        var b = new TacticalBattle(setup,
            Script((0, new OrderRequest(3, OrderKind.Patrol, 1, -1, C(10, 10))), (0, new OrderRequest(4, OrderKind.Patrol, 0, -1, C(4, 20)))),
            PassiveController.Instance);
        DomainEntityView? pf = null, ph = null; int hoverSteps = 0, fwSteps = 0, floorSteps = 0;
        var hc = DomainProfiles.AttackHelicopter;
        long hoverBurn = Proto.M7.Fixed.FixedMath.MulDivCeil(setup.Rules.StepMs, hc.CruiseMmPerS * hc.HoverBurnPercent / 100, hc.CruiseMmPerS);
        RunUntil(b, () =>
        {
            var f = D(b, 3000); var h = D(b, 4000);
            if (f.Phase != AirPhase.Based && pf is { Phase: not AirPhase.Based } p1)
            {
                Assert.True(f.Speed >= DomainProfiles.Fighter.MinSpeedMmPerS);
                Assert.NotEqual(p1.Pos, f.Pos);                                     // always moving: it circles, never parks
                if (f.Speed == DomainProfiles.Fighter.MinSpeedMmPerS) floorSteps++;   // loitering on the minimum-speed floor
                fwSteps++;
            }
            if (h.Phase != AirPhase.Based && ph is { Phase: not AirPhase.Based } p2)
            {
                Assert.True(h.Fuel < p2.Fuel);                                       // every airborne step costs fuel
                if (h.Speed == 0 && p2.Speed == 0) { Assert.Equal(hoverBurn, p2.Fuel - h.Fuel); hoverSteps++; }
            }
            pf = f; ph = h;
            return b.CurrentPulse >= 4;
        });
        Assert.True(fwSteps > 60);
        Assert.True(floorSteps > 10, $"the fighter never loitered at its minimum speed ({floorSteps})");
        Assert.True(hoverSteps > 10, $"the helicopter never hovered ({hoverSteps})");
    }

    /// <summary>ORDER-DELIVERY PIN: a launch order at step offset k takes off exactly at pulseStart + k*StepMs (±1 neighbours).</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void TakeOffHappensAtTheDeliveredStep(int k)
    {
        var setup = Land(7, MultiDomainScenarios.Flight(3, 0, DomainProfiles.Fighter, 1));
        long step = setup.Rules.StepMs, tk = setup.StartMs + k * step;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(3, OrderKind.Patrol, k, -1, C(20, 20)))), PassiveController.Instance);
        var phase = new SortedDictionary<long, AirPhase>();
        RunUntil(b, () => { phase[b.Scheduler.NowMs] = D(b, 3000).Phase; return b.Scheduler.NowMs >= tk + step; });
        Assert.Equal(AirPhase.Based, phase[tk - step]);
        Assert.Equal(AirPhase.Airborne, phase[tk]);
    }

    [Fact]
    public void SortiesAndTurnaroundAreEnforced()
    {
        // Sight 100 m (TEST INPUT): the fighter must not spot the enemy, or the land AI would turn and attack.
        var a = DomainProfiles.Fighter with { TurnaroundMs = 300_000, Sorties = 2, SightMm = 100_000 };
        var c0 = Script(
            (0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(5, 20))),
            (0, new OrderRequest(3, OrderKind.Retreat, 2)),            // straight back (2 actions: Patrol + Retreat)
            (1, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(5, 20))),   // turnaround not finished
            (7, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(5, 20))),   // ready again: second (last) sortie
            (7, new OrderRequest(3, OrderKind.Retreat, 2)),
            (14, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(5, 20))));  // no sorties left
        // Side 1 marches on a distant objective for ~20 pulses, so the battle outlasts the turnaround and both sorties.
        var b = new TacticalBattle(Land(8, C(63, 39), MultiDomainScenarios.Flight(3, 0, a, 1)),
                                   new CompositeController(c0, new OnlyFormations(new KnowledgeAi(), 1)), new KnowledgeAi());
        b.RunUntilPulse(15);
        Assert.False(b.IsOver);
        var mine = b.Orders.Where(o => o.Request.FormationId == 3).ToArray();
        Assert.True(mine[0].Accepted && mine[1].Accepted);
        Assert.Equal(OrderInvalid.NotReady, mine[2].Invalid);
        Assert.True(mine[3].Accepted && mine[4].Accepted);
        Assert.Equal(OrderInvalid.NoSorties, mine[5].Invalid);
    }

    [Fact]
    public void FighterInterceptsAndAirDefenceEngagesOnlyAirborneTargetsInRange()
    {
        var strike = MultiDomainScenarios.Flight(3, 0, DomainProfiles.StrikeAircraft, 1);
        var fighter = MultiDomainScenarios.Flight(4, 1, DomainProfiles.Fighter, 1);
        var aa = new FormationSpec(5, 1, DomainProfiles.GunCrew, 2, C(40, 20)) { Weapons = new[] { DomainProfiles.AaGun } };
        var b = new TacticalBattle(Land(9, strike, fighter, aa),
            Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(38, 20)))),
            Script((0, new OrderRequest(4, OrderKind.Attack, 0, 3))));
        b.RunUntilPulse(4);
        var fire = Executed(b, DomainKinds.DomainFire).ToArray();
        Assert.Contains(fire, e => e.Source.Id == 4000 && e.Target.Id == 3000);         // interception
        Assert.Contains(fire, e => (e.Source.Id == 5000 || e.Source.Id == 5001) && e.Target.Id == 3000);   // air defence
        Assert.DoesNotContain(fire, e => (e.Source.Id == 5000 || e.Source.Id == 5001) && e.Target.Id < 3000); // never at ground targets
        // Out of range: long-sighted guns in the far corner TRACK the strike aircraft patrolling ~3 km away but never fire.
        var farAa = new FormationSpec(5, 1, DomainProfiles.GunCrew with { SightCells = 100 }, 2, C(62, 38)) { Weapons = new[] { DomainProfiles.AaGun } };
        var far = new TacticalBattle(Land(9, strike, farAa), Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(2, 2)))), PassiveController.Instance);
        far.RunUntilPulse(3);
        Assert.True(far.EverDetectedDomain(1, 3000));
        Assert.DoesNotContain(Executed(far, DomainKinds.DomainFire), e => e.Source.Id == 5000 || e.Source.Id == 5001);
    }

    [Fact]
    public void EmptyRacksSendTheAircraftHome()
    {
        var strike = MultiDomainScenarios.Flight(3, 0, DomainProfiles.StrikeAircraft, 1);
        var b = new TacticalBattle(Land(10, strike), Script((0, new OrderRequest(3, OrderKind.Bombard, 0, -1, C(45, 30)))), PassiveController.Instance);
        RunUntil(b, () => D(b, 3000).Phase == AirPhase.Returning, 6);
        int drops = Executed(b, DomainKinds.DomainFire).Count(e => e.Source.Id == 3000);
        Assert.Equal(DomainProfiles.Bombs.Ammo, drops);                              // released both bombs, then turned for home
    }

    /// <summary>
    /// Damage abort (DomainRules.DamagedAbortPermille, default 1/2): a fighter on station over an air-defence gun is hit; at
    /// the next step it turns for home if and only if its airframe HP fell to half or less. Fuel is plenty and its guns are
    /// unlimited, so damage is the only possible cause. The accurate gun is a TEST INPUT (it must hit inside the window).
    /// </summary>
    [Theory]
    [InlineData(16L, true)]       // 30 HP - 16 = 14 <= 15: abort
    [InlineData(10L, false)]      // 30 HP - 10 = 20 > 15: stays on station
    public void DamageAbortSendsTheAircraftHomeOnlyAtTheThreshold(long gunDamage, bool aborts)
    {
        var fighter = MultiDomainScenarios.Flight(3, 0, DomainProfiles.Fighter, 1);
        var gun = DomainProfiles.AaGun with { Accuracy = 1_000, Damage = gunDamage };
        var aa = new FormationSpec(5, 1, DomainProfiles.GunCrew, 1, C(45, 20)) { Weapons = new[] { gun } };
        var setup = Land(12, fighter, aa);
        long step = setup.Rules.StepMs;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20)))), PassiveController.Instance);
        long hitAt = -1;
        RunUntil(b, () =>
        {
            if (hitAt < 0 && D(b, 3000).Hp < DomainProfiles.Fighter.Hp) hitAt = b.Scheduler.NowMs;
            return hitAt >= 0 && b.Scheduler.NowMs >= hitAt + step;
        }, 6);
        Assert.True(hitAt >= 0, "the gun never hit the fighter");
        var after = D(b, 3000);
        Assert.Equal(DomainProfiles.Fighter.Hp - gunDamage, after.Hp);               // exactly one hit so far
        Assert.True(after.Fuel > DomainProfiles.Fighter.EnduranceMs / 2);            // not a fuel return
        Assert.Equal(aborts ? AirPhase.Returning : AirPhase.Airborne, after.Phase);
    }

    /// <summary>
    /// Air support to the land battle: a strike aircraft ordered to attack the riflemen hidden behind the ridge finds them by
    /// looking down and bombs them. The blasts go through the LAND damage path (HP, death, the formation's losses) and the
    /// personnel ledger: every land entity a bomb hit (bomb damage 60 &gt;= rifleman HP 40) is dead and counted Killed.
    /// </summary>
    [Fact]
    public void StrikeAircraftSupportsTheLandBattleThroughTheLandDamagePath()
    {
        var strike = MultiDomainScenarios.Flight(3, 0, DomainProfiles.StrikeAircraft, 1);
        var b = new TacticalBattle(Land(11, strike), Script((0, new OrderRequest(3, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        RunUntil(b, () => D(b, 3000).Phase == AirPhase.Returning, 8);
        Assert.Equal(DomainProfiles.Bombs.Ammo, Executed(b, DomainKinds.DomainFire).Count(e => e.Source.Id == 3000));   // both bombs on the target
        var victims = Executed(b, DomainKinds.DomainHit).Where(e => e.Source.Id == 3000 && e.A == 0).Select(e => e.Target.Id).Distinct().ToArray();
        Assert.NotEmpty(victims);
        Assert.All(victims, id => Assert.Equal(2L, L(b, id).FormationId));           // only the enemy riflemen were near the blasts
        Assert.All(victims, id => Assert.Equal(EntityState.Dead, L(b, id).State));
        Assert.Equal(victims.Length, b.Entities().Count(e => e.FormationId == 2 && e.State == EntityState.Dead));
        Assert.Equal(victims.Length, b.Personnel.Balance(2, PersonnelPlace.Killed));
        Assert.Null(b.PersonnelConsistency());
    }

    // ---------------------------------------------------------------- verification round 1 (N-R1-03d, -06)

    /// <summary>
    /// N-R1-03d: an aircraft looks DOWN, so its clarity is the cover of the target's own cell, not the cells along a ground
    /// sight line. The recon flight of <see cref="ReconAircraftRevealsAFormationHiddenFromTheGround"/> sees the riflemen behind
    /// the ridge on open ground (clarity 1); under a full canopy (TEST INPUT: Forest concealment 0) over their position it does
    /// not see them at all.
    /// </summary>
    [Fact]
    public void LookDownClarityIsTheCanopyOfTheTargetsOwnCell()
    {
        var recon = MultiDomainScenarios.Flight(3, 0, DomainProfiles.ReconAircraft, 1);
        bool Seen(bool canopy)
        {
            var g = Ridge();
            if (canopy) for (int y = 14; y <= 26; y++) for (int x = 32; x <= 40; x++) g.Set(x, y, TerrainClass.Forest);
            var conceal = ConcealmentTable.Create(new[]
            {
                Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.Zero, Proto.M7.Fixed.Q32.FromRatio(1, 4),
                Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.Zero,
                // [TR] ClassCount 8 -> 11 (Marsh, Ford, Building appended); none of the three occurs on this grid.
                Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.One, Proto.M7.Fixed.Q32.Zero,
                // [AP] ClassCount 11 -> 13 (Earthwork, Ditch appended; placeholder values); neither occurs on this grid.
                Proto.M7.Fixed.Q32.FromRatio(1, 2), Proto.M7.Fixed.Q32.One,
            });
            var setup = Land(4, recon) with { Grid = g, Concealment = conceal };
            var b = new TacticalBattle(setup, Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(36, 20)))), PassiveController.Instance);
            bool overhead = false;
            RunUntil(b, () =>
            {
                if (Vec2L.Distance(D(b, 3000).Pos, C(36, 20)) <= 300_000) overhead = true;
                return b.CurrentPulse >= 3;
            }, 4);
            Assert.True(overhead, "the recon aircraft never flew over the target");
            if (canopy) Assert.All(b.Entities().Where(e => e.FormationId == 2), e => Assert.Equal(TerrainClass.Forest, g.ClassAt(g.CellOf(e.Pos).X, g.CellOf(e.Pos).Y)));
            return b.Entities().Any(e => e.FormationId == 2 && b.EverDetected(0, e.Id));
        }
        Assert.True(Seen(false));
        Assert.False(Seen(true));
    }

    /// <summary>
    /// N-R1-06: an airframe that aborted at the damage threshold is not repaired in battle, so it stays on the ground: later
    /// orders cannot launch it (refused NotReady), no sortie is spent and it makes no further take-off or landing.
    /// </summary>
    [Fact]
    public void AnAircraftGroundedByDamageIsNotRelaunched()
    {
        var ftr = DomainProfiles.Fighter with { Sorties = 3, TurnaroundMs = 1_000 };          // TEST INPUT: quick turnaround
        var fighter = MultiDomainScenarios.Flight(3, 0, ftr, 1);
        var gun = DomainProfiles.AaGun with { Accuracy = 1_000, Damage = 16 };               // one hit: 30 -> 14 HP, below 1/2
        var aa = new FormationSpec(5, 1, DomainProfiles.GunCrew, 1, C(45, 20)) { Weapons = new[] { gun } };
        var c0 = new ScriptedController();
        for (int p = 0; p < 8; p++) c0.Add(p, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(45, 20)));
        var b = new TacticalBattle(Land(12, fighter, aa), c0, PassiveController.Instance);
        long landedAt = -1; int sortiesAtLanding = -1, gen = -1;
        RunUntil(b, () =>
        {
            var d = D(b, 3000);
            if (landedAt < 0 && d.Phase == AirPhase.Based && d.Hp < ftr.Hp) { landedAt = b.Scheduler.NowMs; sortiesAtLanding = d.SortiesLeft; gen = d.Generation; }
            if (landedAt >= 0)
            {
                Assert.Equal(AirPhase.Based, d.Phase);
                Assert.Equal(sortiesAtLanding, d.SortiesLeft);
                Assert.Equal(gen, d.Generation);
            }
            return b.CurrentPulse >= 6;
        }, 8);
        Assert.True(landedAt >= 0, "the fighter was never damaged and landed");
        Assert.Equal(ftr.Sorties - 1, sortiesAtLanding);
        var later = b.Orders.Where(o => o.Request.FormationId == 3 && o.AtMs > landedAt).ToArray();
        Assert.NotEmpty(later);
        Assert.All(later, o => Assert.Equal(OrderInvalid.NotReady, o.Invalid));
    }
}
