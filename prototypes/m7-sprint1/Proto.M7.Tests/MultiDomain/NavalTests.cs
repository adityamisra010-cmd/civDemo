using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Naval module: kinematics, water-only movement, wind, arcs, ram, boarding, sinking, target choice.</summary>
public class NavalTests
{
    private static readonly VesselClass Tp = DomainProfiles.SteamTransport;

    [Theory]
    [InlineData(2000L)]
    [InlineData(1000L)]
    public void TurnRateSpeedAndAccelerationBoundsHoldEveryStep(long stepMs)
    {
        // A warship ordered to a point astern must turn round: per step |dHeading| <= TurnRate * dt, 0 <= speed <= max,
        // |dSpeed| <= Accel * dt — the per-second rates integrated over the step (law 3), for two step lengths.
        var w = DomainProfiles.TurretWarship;
        var s = Sea(100_000, 3, null, Ship(1, 0, w, M(3000, 2000)), Ship(2, 1, Tp, M(6000, 3500))) with
        { Rules = new TacticalRules { StepMs = stepMs } };
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, M(500, 2000)))), PassiveController.Instance);
        long maxTurn = HeadingMotion.MaxTurn(w.TurnRatePerS, stepMs), maxDv = FixedMath.MulDivFloor(w.AccelMmPerS2, stepMs, 1000);
        var prev = D(b, 1000);
        int steps = 0; bool turned = false;
        RunUntil(b, () =>
        {
            var d = D(b, 1000);
            if (d.Generation == prev.Generation && b.Scheduler.NowMs > s.StartMs)
            {
                Assert.True(FixedTrig.Separation(prev.Heading, d.Heading) <= maxTurn, $"turned {FixedTrig.Separation(prev.Heading, d.Heading)} > {maxTurn}");
                Assert.InRange(d.Speed, 0, w.MaxSpeedMmPerS);
                Assert.True(Math.Abs(d.Speed - prev.Speed) <= maxDv || d.Speed < prev.Speed, $"accelerated {d.Speed - prev.Speed} > {maxDv}");
                if (FixedTrig.Separation(d.Heading, FixedTrig.HalfTurn) < FixedTrig.FromDegrees(10)) turned = true;
                steps++;
            }
            prev = d;
            return b.CurrentPulse >= 3;
        });
        Assert.True(steps > 50);
        Assert.True(turned, "the warship never came round to the west");
    }

    [Fact]
    public void VesselsNeverLeaveTheWaterInTheAmphibiousBattle()
    {
        var setup = MultiDomainScenarios.Amphibious(7);
        var b = new TacticalBattle(setup, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence());
        long checks = 0;
        RunChecked(b, onInstant: () =>
        {
            foreach (var d in b.DomainEntities())
            {
                if (d.Domain != Domain.Naval || d.State is not (EntityState.Active or EntityState.Routing)) continue;
                Assert.True(NavalView.OnWater(setup.Grid, d.Pos), $"vessel {d.Id} on land at {d.Pos}");
                checks++;
            }
        });
        Assert.True(checks > 100);
    }

    [Fact]
    public void VesselSteeringInlandStopsAtTheShore()
    {
        // A short-ranged line ship ordered to bombard a point 1.5 km inland heads straight at it: the naval view of the grid
        // must stop it on the last water before the beach — it never enters a land cell.
        var g = Coast(50_000, 20);
        var s = OnGrid(g, 3, null, null,
            Ship(1, 0, DomainProfiles.ShipOfTheLine, MultiDomainScenarios.Cell(10, 20, 50_000)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 5, MultiDomainScenarios.Cell(62, 38, 50_000)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, 0, -1, MultiDomainScenarios.Cell(50, 20, 50_000)))), PassiveController.Instance);
        long maxX = 0;
        RunUntil(b, () =>
        {
            var d = D(b, 1000);
            Assert.True(NavalView.OnWater(g, d.Pos), $"vessel on land at {d.Pos}");
            maxX = Math.Max(maxX, d.Pos.X);
            return b.CurrentPulse >= 5;
        }, 6);
        Assert.True(maxX >= 19 * 50_000, "the vessel never reached the shoreline cells");
    }

    [Fact]
    public void EscortKeepsItsScreeningStationOnTheTransports()
    {
        // Two transports steam 4 km east; a warship escorting them stays within a few line spacings of them throughout.
        var w = DomainProfiles.TurretWarship;
        var s = Sea(100_000, 4, null,
            MultiDomainScenarios.Fleet(1, 0, Tp, 2, M(1000, 2000)),
            Ship(2, 0, w, M(1000, 2600)),
            Ship(3, 1, Tp with { Weapons = Array.Empty<WeaponSpec>() }, M(6300, 3900)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, M(5500, 2000))), (0, new OrderRequest(2, OrderKind.Escort, 0, 1))),
                                   PassiveController.Instance);
        long limit = 4 * w.RadiusMm * new DomainRules().LineSpacingRadii, worst = 0, moved = 0;
        RunUntil(b, () =>
        {
            if (b.CurrentPulse >= 2)
            {
                var t0 = D(b, 1000); var t1 = D(b, 1001); var e = D(b, 2000);
                var c = new Vec2L((t0.Pos.X + t1.Pos.X) / 2, (t0.Pos.Y + t1.Pos.Y) / 2);
                worst = Math.Max(worst, Vec2L.Distance(c, e.Pos));
                moved = t0.Pos.X;
            }
            return b.CurrentPulse >= 6;
        }, 7);
        Assert.True(moved > 2_500_000, "the transports did not get under way");
        Assert.True(worst <= limit, $"escort strayed {worst} mm from the transports (limit {limit})");
    }

    [Fact]
    public void SailingShipMakesNoWayInsideTheNoGoZoneButBeatsUpwind()
    {
        // Wind from the east (0°); a line ship facing east (in irons) is ordered to a point far to the east.
        var rules = new DomainRules { Wind = true, WindFrom = 0 };
        var v = DomainProfiles.ShipOfTheLine;
        var s = Sea(100_000, 1, rules, Ship(1, 0, v, M(1000, 2000)), Ship(2, 1, Tp, M(6000, 3800)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, M(5500, 2000)))), PassiveController.Instance);
        long x0 = D(b, 1000).Pos.X; long moving = 0;
        RunUntil(b, () =>
        {
            var d = D(b, 1000);
            if (FixedTrig.Separation(d.Heading, rules.WindFrom) <= rules.SailNoGo) Assert.Equal(0, d.Speed);
            if (d.Speed > 0) moving++;
            return b.CurrentPulse >= 4;
        });
        Assert.True(moving > 0);
        Assert.True(D(b, 1000).Pos.X > x0 + 50_000, "no upwind progress after 4 pulses");
        // Polar: no-go 0, beam reach 1000, dead run 750.
        Assert.Equal(0, rules.SailFactor(0));
        Assert.Equal(1000, rules.SailFactor(FixedTrig.Quarter));
        Assert.Equal(750, rules.SailFactor(FixedTrig.HalfTurn));
    }

    [Fact]
    public void EnginePropulsionIgnoresTheWind()
    {
        BattleResult Run(bool wind)
        {
            var s = MultiDomainScenarios.LineBattle(DomainProfiles.TurretWarship, 200_000, 5, new DomainRules { Wind = wind, WindFrom = 123 });
            return RunChecked(new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), Script((0, new OrderRequest(2, OrderKind.Attack, 0, 1)))));
        }
        var a = Run(false); var c = Run(true);
        Assert.Equal(a.TraceHash, c.TraceHash);
    }

    private static TacticalBattle ArcDuel(VesselClass shooter, Vec2L target)
        => new(Sea(50_000, 11, null, Ship(1, 0, shooter, M(1000, 1000)), Ship(2, 1, Tp, target)), PassiveController.Instance, PassiveController.Instance);

    private static int ShotsBy(TacticalBattle b, long shooterId)
    {
        int n = 0;
        foreach (var e in Executed(b, DomainKinds.DomainFire)) if (e.Source.Id == shooterId) n++;
        return n;
    }

    [Fact]
    public void BroadsideBearsOnlyAbeamBowGunsOnlyAheadTurretsAnywhere()
    {
        // Shooter faces east (side 0); the unarmed-against-ships transport sits 500 m dead ahead or 500 m abeam.
        var ahead = M(1500, 1000); var abeam = M(1000, 1500);
        var b = ArcDuel(DomainProfiles.ShipOfTheLine, ahead); b.RunUntilPulse(2);
        Assert.Equal(0, ShotsBy(b, 1000));
        b = ArcDuel(DomainProfiles.ShipOfTheLine, abeam); b.RunUntilPulse(2);
        Assert.True(ShotsBy(b, 1000) > 0);
        b = ArcDuel(DomainProfiles.GunGalley, abeam); b.RunUntilPulse(2);
        Assert.Equal(0, ShotsBy(b, 1000));
        b = ArcDuel(DomainProfiles.GunGalley, ahead); b.RunUntilPulse(2);
        Assert.True(ShotsBy(b, 1000) > 0);
        b = ArcDuel(DomainProfiles.TurretWarship, M(500, 1000)); b.RunUntilPulse(2);   // astern
        Assert.True(ShotsBy(b, 1000) > 0);
    }

    [Fact]
    public void TargetChoiceIsTieDenseLowestIdThenDesignatedFirst()
    {
        // Twelve enemy hulls at exactly the same distance (Pythagorean points of radius 500 m) around one turret warship.
        var pts = new (long, long)[] { (500, 0), (-500, 0), (0, 500), (0, -500), (300, 400), (-300, 400), (300, -400), (-300, -400), (400, 300), (-400, 300), (400, -300), (-400, -300) };
        long[] ids = { 37, 21, 44, 29, 52, 33, 48, 25, 41, 56, 31, 60 };
        FormationSpec[] Forms(bool permute)
        {
            var list = new List<FormationSpec> { Ship(1, 0, DomainProfiles.TurretWarship, M(1600, 1000)) };
            for (int i = 0; i < pts.Length; i++) list.Add(Ship(ids[i], 1, Tp, M(1600 + pts[i].Item1, 1000 + pts[i].Item2)));
            var arr = list.ToArray();
            if (permute) Array.Reverse(arr);
            return arr;
        }
        long FirstTarget(IFormationController c0, bool permute)
        {
            var b = new TacticalBattle(Sea(50_000, 9, null, Forms(permute)), c0, PassiveController.Instance);
            RunUntil(b, () => Executed(b, DomainKinds.DomainFire).Any(e => e.Source.Id == 1000));
            return Executed(b, DomainKinds.DomainFire).First(e => e.Source.Id == 1000).Target.Id;
        }
        Assert.Equal(21_000, FirstTarget(PassiveController.Instance, false));        // lowest id among equals
        Assert.Equal(21_000, FirstTarget(PassiveController.Instance, true));         // input order irrelevant
        var designate = Script((0, new OrderRequest(1, OrderKind.Attack, 0, 56)));
        Assert.Equal(56_000, FirstTarget(designate, false));                        // designated target first
    }

    [Fact]
    public void RamDamageScalesWithClosingSpeedAndOnlyAtRammingSpeed()
    {
        var g = DomainProfiles.Galley;
        var s = Sea(50_000, 4, null, Ship(1, 0, g, M(1000, 1000)), Ship(2, 1, g, M(1110, 1000)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), PassiveController.Instance);
        RunUntil(b, () => Executed(b, DomainKinds.Ram).Any());
        var ram = Executed(b, DomainKinds.Ram).First();
        var hit = Executed(b, DomainKinds.DomainHit).First(e => e.Key.TimeMs == ram.Key.TimeMs && e.Source.Id == 1000 && e.A == 1);
        Assert.InRange(hit.B, g.RamDamage / 2, g.RamDamage);                         // speed in [max/2, max]
        Assert.Equal(g.Hull - hit.B, D(b, 2000).Hp);                                 // deck archers never hole a hull
        Assert.Equal(0, D(b, 1000).Speed);                                           // the impact stops the rammer
        // Boarding approach capped below ramming speed: no ram at all.
        var slow = new DomainRules { BoardingSpeedMmPerS = 1_000 };
        var b2 = new TacticalBattle(Sea(50_000, 4, slow, Ship(1, 0, g, M(1000, 1000)), Ship(2, 1, g, M(1110, 1000))),
                                    Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        RunUntil(b2, () => Executed(b2, DomainKinds.Boarding).Any(), 10);
        Assert.Empty(Executed(b2, DomainKinds.Ram));
    }

    [Fact]
    public void BoardingCapturesTheTransportAndItsTroopsThroughTheLedger()
    {
        var g = DomainProfiles.Galley;
        var t = DomainProfiles.OaredTransport;
        var s = Sea(50_000, 6, null,
            Ship(1, 0, g, M(1000, 1000)),
            Ship(2, 1, t, M(1060, 1000)),
            new FormationSpec(3, 1, DomainProfiles.Riflemen, 30, M(2500, 1000)) { EmbarkedOn = 2 });
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        var r = RunChecked(b);
        var f2 = r.Formations.Single(f => f.FormationId == 2);
        var f3 = r.Formations.Single(f => f.FormationId == 3);
        Assert.Equal(1, f2.Surrendered);                                             // the transport was taken
        Assert.Equal(30, f3.Start);
        Assert.Equal(f3.Start, f3.Survivors + f3.Killed + f3.Surrendered);
        Assert.True(f3.Surrendered > 0);
        var p = r.Personnel!;
        Assert.Equal(f3.Surrendered, p.Count(3, PersonnelPlace.Captured));
        Assert.Equal(f3.Killed, p.Count(3, PersonnelPlace.Killed) + p.Count(3, PersonnelPlace.LostWithVessel));
        Assert.True(p.Count(2, PersonnelPlace.Captured) > 0);
        Assert.Equal(p.OpeningTotal, p.Total);
    }

    [Fact]
    public void StrikeColoursRuleCapturesAtTheThresholdBeforeAnyRout()
    {
        // Strike fraction 3/4 (TEST INPUT): the transport is taken as soon as the people aboard fall to 3/4 of those aboard
        // when the fight began, while the boarders outnumber them — before its morale breaks.
        var rules = new DomainRules { StrikeNum = 3, StrikeDen = 4 };
        var s = Sea(50_000, 6, rules,
            Ship(1, 0, DomainProfiles.Galley with { Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0 }, M(1000, 1000)),
            Ship(2, 1, DomainProfiles.OaredTransport, M(1060, 1000)),
            new FormationSpec(3, 1, DomainProfiles.Riflemen, 30, M(2500, 1000)) { EmbarkedOn = 2 });
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        long firstFightAboard = -1, lastAboard = -1; bool routed = false;
        RunUntil(b, () =>
        {
            var t = D(b, 2000);
            if (t.State == EntityState.Routing) routed = true;
            if (t.State == EntityState.Active)
            {
                lastAboard = t.Crew + t.Embarked;
                if (firstFightAboard < 0 && Executed(b, DomainKinds.Boarding).Any()) firstFightAboard = lastAboard;
            }
            return t.State == EntityState.Surrendered;
        }, 10);
        Assert.False(routed);
        var p = b.Personnel;
        long atCapture = p.Balance(2, PersonnelPlace.Captured) + p.Balance(3, PersonnelPlace.Captured);
        Assert.True(atCapture * 4 <= 70 * 3, $"captured with {atCapture} aboard");          // <= 3/4 of the 70 aboard at the start
        Assert.True(lastAboard * 4 > firstFightAboard * 3 || lastAboard == firstFightAboard, "should have been taken one step earlier");
        Assert.True(D(b, 1000).Crew > atCapture);                                          // boarders outnumber
    }

    [Fact]
    public void SinkingLosesTheCrewThroughTheLedgerAndShipwreckSurvivorsAreExact()
    {
        foreach (var (num, den) in new[] { (0L, 1L), (1L, 2L) })
        {
            var rules = new DomainRules { ShipwreckSurvivalNum = num, ShipwreckSurvivalDen = den };
            var frail = Tp with { Hull = 1 };
            var s = Sea(100_000, 8, rules, Ship(1, 0, DomainProfiles.TurretWarship, M(1000, 2000)), Ship(2, 1, frail, M(3000, 2000)));
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            long crewAtSinking = Tp.Crew;
            RunUntil(b, () =>
            {
                var d = D(b, 2000);
                if (d.State == EntityState.Active) crewAtSinking = d.Crew;
                return d.State == EntityState.Dead;
            });
            var p = b.Personnel;
            long lost = p.Balance(2, PersonnelPlace.LostWithVessel), saved = p.Balance(2, PersonnelPlace.Escaped), kia = p.Balance(2, PersonnelPlace.Killed);
            Assert.Equal(Tp.Crew, lost + saved + kia);
            long atSink = Tp.Crew - kia;                                              // KIA by the sinking hit itself, then the rest
            Assert.Equal(FixedMath.MulDivFloor(atSink, num, den), saved);
            Assert.True(crewAtSinking >= atSink);
        }
    }

    /// <summary>Crew factor (permille) of the vessel equations: 1000 at full crew, crew/needed floored at 1/4 below it.</summary>
    private static long CrewFactor(long crew, long need) => crew >= need ? 1000 : crew == 0 ? 0 : Math.Max(250, crew * 1000 / need);

    /// <summary>
    /// Crew losses slow the hull and lengthen its reload: a warship under way (MoveTo, quick acceleration as a TEST INPUT)
    /// passes a stationary raider whose short-ranged crew-killing gun (TEST INPUT: 60 casualties, 1 hull point per hit)
    /// thins its crew (morale losses switched off as a TEST INPUT so it does not rout), while its own gun fires at the
    /// raider throughout. At every step its speed stays within
    /// MaxSpeed x crewFactor of the crew it had when the step began, and every reload it starts lasts exactly
    /// ceil(Reload x 1000 / crewFactor) rounded up to the next step: fewer hands, slower ship, slower guns.
    /// </summary>
    [Fact]
    public void CrewLossesSlowTheHullAndLengthenItsReload()
    {
        var w = DomainProfiles.TurretWarship;
        var gun = new WeaponSpec("test_gun", TargetDomains.Surface, MountArc.Turret, MaxRangeMm: 8_000_000, ReloadMs: 10_000, Accuracy: 1_000) { HullDamage = 1 };
        var grape = gun with { Name = "test_grape", MaxRangeMm = 1_500_000, CrewDamage = 60 };
        var s = Sea(100_000, 13, null, Ship(1, 0, w with { Weapons = new[] { gun }, AccelMmPerS2 = 4_000 }, M(1000, 1000)),
                    Ship(2, 1, w with { Weapons = new[] { grape } }, M(4000, 1600))) with
        { Rules = new TacticalRules { MoraleLossFullStrength = 0 } };          // TEST INPUT: no rout, so the crew can be thinned
        long step = s.Rules.StepMs;
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, M(6000, 1000)))), PassiveController.Instance);
        var crewAt = new SortedDictionary<long, long>();
        long prevCrew = w.Crew, fastest = 0, slowSteps = 0;
        RunUntil(b, () =>
        {
            var d = D(b, 1000);
            crewAt[b.Scheduler.NowMs] = d.Crew;
            if (d.State == EntityState.Active && b.Scheduler.NowMs > s.StartMs)
            {
                long cap = w.MaxSpeedMmPerS * CrewFactor(prevCrew, w.Crew) / 1000;
                Assert.True(d.Speed <= cap, $"speed {d.Speed} > {cap} with crew {prevCrew}");
                if (prevCrew == w.Crew) fastest = Math.Max(fastest, d.Speed);
                else if (d.Speed > 0) slowSteps++;
            }
            prevCrew = d.Crew;
            return d.Crew <= w.Crew - 120 || d.State != EntityState.Active || b.CurrentPulse >= 6;
        }, 7);
        Assert.True(D(b, 1000).Crew <= w.Crew - 120, $"the raider did not thin the crew twice: {D(b, 1000)}");
        Assert.True(fastest > w.MaxSpeedMmPerS * CrewFactor(w.Crew - 60, w.Crew) / 1000, "never faster than the thinned-crew limit before the hits");
        Assert.True(slowSteps > 0);
        var shots = Executed(b, DomainKinds.DomainFire).Where(e => e.Source.Id == 1000).Select(e => e.Key.TimeMs).ToArray();
        int checkedLoss = 0;
        for (int i = 1; i < shots.Length; i++)
        {
            long crew = shots[i - 1] - step < s.StartMs ? w.Crew : crewAt[shots[i - 1] - step];   // crew when the reload began (before that instant's hits)
            long reload = (gun.ReloadMs * 1000 + CrewFactor(crew, w.Crew) - 1) / CrewFactor(crew, w.Crew);
            long expect = (reload + step - 1) / step * step;                  // the next step at or after the reload ends
            Assert.Equal(expect, shots[i] - shots[i - 1]);
            if (crew < w.Crew) checkedLoss++;
        }
        Assert.True(checkedLoss > 0, "no reload was measured after a crew loss");
    }

    [Fact]
    public void CapabilityDerivationUsesCapabilitiesNotDates()
    {
        foreach (var m in typeof(NavalDesign).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            foreach (var prm in m.GetParameters())
                Assert.True(prm.ParameterType == typeof(NavalCapability) || prm.ParameterType == typeof(VesselRole), $"{m.Name}({prm.ParameterType.Name})");
        Assert.Same(DomainProfiles.Galley, NavalDesign.Derive(NavalCapability.Oars, VesselRole.Warship));
        Assert.Same(DomainProfiles.ShipOfTheLine, NavalDesign.Derive(NavalCapability.Oars | NavalCapability.Sails | NavalCapability.Gunpowder, VesselRole.Warship));
        Assert.Same(DomainProfiles.TurretWarship, NavalDesign.Derive(NavalCapability.Sails | NavalCapability.Gunpowder | NavalCapability.SteamPower | NavalCapability.Turrets, VesselRole.Warship));
        Assert.Same(DomainProfiles.SteamFrigate, NavalDesign.Derive(NavalCapability.Gunpowder | NavalCapability.SteamPower, VesselRole.Warship));
        Assert.Same(DomainProfiles.ShipOfTheLine, NavalDesign.Derive(NavalCapability.Sails | NavalCapability.Gunpowder | NavalCapability.Turrets, VesselRole.Warship)); // turrets need steam
        Assert.Throws<ArgumentException>(() => NavalDesign.Derive(NavalCapability.Gunpowder, VesselRole.Warship));
        // Total over every capability set with some propulsion, propulsion matches, and every class validates.
        for (int c = 0; c < 64; c++)
        {
            var caps = (NavalCapability)c;
            if ((caps & (NavalCapability.Oars | NavalCapability.Sails | NavalCapability.SteamPower)) == 0) continue;
            foreach (var role in new[] { VesselRole.Warship, VesselRole.Transport })
            {
                var v = NavalDesign.Derive(caps, role);
                v.Validate();
                Assert.Equal(NavalDesign.PropulsionOf(caps), v.Propulsion);
                Assert.Equal(role == VesselRole.Transport, v.Capacity > 0);
            }
        }
    }

    // ---------------------------------------------------------------- verification round 1 (N-R1-02a, -03b, -03c, -05)

    /// <summary>
    /// N-R1-02a: troops aboard share a hit's personnel casualties. Per hit: people = min(CrewDamage, aboard), embarked victims
    /// = floor(people * embarked / aboard), the rest crew; several hits in one instant compose in execution order. Checked
    /// exactly (hull, embarked count, both Killed balances) at every instant the transport is hit and stays afloat.
    /// </summary>
    [Fact]
    public void HitsOnATransportKillEmbarkedTroopsInProportionWhileItIsAfloat()
    {
        var s = Sea(100_000, 12, null,
            Ship(1, 0, Tp with { Capacity = 30 }, M(3000, 2000)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, M(3000, 2000)) { EmbarkedOn = 1 },
            Ship(5, 1, DomainProfiles.TurretWarship, M(5000, 2000)));
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        long crew = Tp.Crew, emb = 20, crewKilled = 0, troopsKilled = 0;
        int verified = 0, withTroops = 0;
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            var v = D(b, 1000);
            long c = crew, e = emb, dc = 0, de = 0;
            foreach (var h in Executed(b, DomainKinds.DomainHit))
            {
                if (h.Key.TimeMs != now || h.Target.Id != 1000 || h.A != 1) continue;
                long aboard = c + e, people = Math.Min(h.C, aboard);
                long k = aboard == 0 ? 0 : FixedMath.MulDivFloor(people, e, aboard);
                dc += people - k; de += k; c -= people - k; e -= k;
            }
            if (dc + de > 0 && v.State == EntityState.Active)
            {
                Assert.Equal(c, v.Crew);
                Assert.Equal(e, v.Embarked);
                Assert.Equal(crewKilled + dc, b.Personnel.Balance(1, PersonnelPlace.Killed));
                Assert.Equal(troopsKilled + de, b.Personnel.Balance(2, PersonnelPlace.Killed));
                verified++;
                if (de > 0) withTroops++;
            }
            crew = v.Crew; emb = v.Embarked;
            crewKilled = b.Personnel.Balance(1, PersonnelPlace.Killed); troopsKilled = b.Personnel.Balance(2, PersonnelPlace.Killed);
            return v.State != EntityState.Active || withTroops >= 3;
        });
        Assert.True(withTroops >= 1, $"no hit killed troops aboard a floating transport ({verified} hit instants checked)");
        Assert.Null(b.PersonnelConsistency());
    }

    /// <summary>TEST INPUT: a six-round gun (limited ammunition: fires only at the designated formation).</summary>
    private static WeaponSpec SixRounds(long hull, long crew) =>
        new("test_six_rounds", TargetDomains.Surface, MountArc.Turret, MaxRangeMm: 3_000_000, ReloadMs: 4_000, Accuracy: 1_000)
        { HullDamage = hull, CrewDamage = crew, Ammo = 6 };

    /// <summary>
    /// N-R1-03b: damage control. A hull under Rally regains at most RepairPerS * dt per step, and in total no more than its
    /// repair pool (RepairPoolPercent of Hull) — after taking more damage than the pool, it ends at Hull - damage + pool.
    /// </summary>
    [Fact]
    public void RallyRepairsAtTheRateFromABoundedPool()
    {
        var gun = SixRounds(30, 0);                                  // no crew damage: no casualties, no morale loss, no rout
        var s = Sea(100_000, 5, null, Ship(1, 0, Tp, M(3000, 2000)),
                    Ship(5, 1, DomainProfiles.TurretWarship with { Weapons = new[] { gun } }, M(5000, 2000)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Rally))), Script((0, new OrderRequest(5, OrderKind.Attack, 0, 1))));
        long pool = FixedMath.MulDivFloor(Tp.Hull, Tp.RepairPoolPercent, 100);
        long perStep = FixedMath.MulDivFloor(Tp.RepairPerS, s.Rules.StepMs, 1000);
        long prevHp = Tp.Hull, damage = 0, repaired = 0;
        int quiet = 0; bool spent = false, rateSeen = false;
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            var v = D(b, 1000);
            long hit = 0;
            foreach (var h in Executed(b, DomainKinds.DomainHit)) if (h.Key.TimeMs == now && h.Target.Id == 1000 && h.A == 1) hit += h.B;
            long rep = v.Hp - prevHp + hit;
            Assert.InRange(rep, 0, perStep);
            if (rep == perStep) rateSeen = true;
            damage += hit; repaired += rep; prevHp = v.Hp;
            Assert.True(repaired <= pool, $"repaired {repaired} > pool {pool}");
            spent = Executed(b, DomainKinds.DomainFire).Count(e => e.Source.Id == 5000) == gun.Ammo;
            quiet = spent && rep == 0 && hit == 0 ? quiet + 1 : 0;
            return quiet >= 10;
        }, 30);
        Assert.True(spent, "the gun never fired all its rounds");
        Assert.True(damage > pool, $"precondition: damage {damage} must exceed the pool {pool}");
        Assert.True(rateSeen);
        Assert.Equal(pool, repaired);                                       // the pool, not the damage, bounds the repair
        Assert.Equal(Tp.Hull - damage + pool, D(b, 1000).Hp);
        Assert.Equal(EntityState.Active, D(b, 1000).State);
    }

    /// <summary>
    /// N-R1-03c: boarding casualties = integer part of fighters x p x lethality x dt plus a counter-RNG Bernoulli on the
    /// remainder (exact expectation, integer outcome). Two 20-man galleys (TEST INPUT; no weapons, no ram): 20 x 1/2 x 20 per
    /// mille/s x 2 s = 0.4 expected per side and step, so every casualty is the Bernoulli's. Every fight is checked exactly
    /// against the draw keyed (attacker, instant, defender, direction), from both sides' pre-fight numbers (simultaneous).
    /// </summary>
    [Fact]
    public void BoardingCasualtiesAreTheIntegerPartPlusABernoulliOnTheRemainder()
    {
        var g = DomainProfiles.Galley with { Crew = 20, Weapons = Array.Empty<WeaponSpec>(), RamDamage = 0 };
        var s = Sea(50_000, 9, null, Ship(1, 0, g, M(1000, 1000)), Ship(2, 1, g, M(1060, 1000)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Board, 0, 2))), PassiveController.Instance);
        var dr = new DomainRules();
        long Expected(long fighters, long attacker, long t, long defender, uint idx, long cap)
        {
            Q32 p = Q32.FromRatio(g.BoardingMelee, g.BoardingMelee + g.BoardingDefence);
            if (p < s.Rules.HitMin) p = s.Rules.HitMin;
            if (p > s.Rules.HitMax) p = s.Rules.HitMax;
            Int128 num = (Int128)fighters * p.Raw * dr.BoardingLethalityPermillePerS * s.Rules.StepMs;
            Int128 den = (Int128)Q32.OneRaw * 1_000_000;
            long whole = (long)(num / den), rem = (long)(num - (Int128)whole * den);
            var frac = new Q32(FixedMath.MulDivFloor(rem, Q32.OneRaw, (long)den));
            Assert.True(whole == 0 && frac.Raw > 0);                                    // the test's premise: E in (0, 1)
            if (Proto.M7.Rng.CounterRng.Chance(s.Seed, DomainStreams.Boarding, attacker, t, defender, idx, frac)) whole++;
            return Math.Min(whole, cap);
        }
        long HitOn(long target, long source, long t)
        {
            long n = 0;
            foreach (var h in Executed(b, DomainKinds.DomainHit))
                if (h.Key.TimeMs == t && h.Target.Id == target && h.Source.Id == source && h.A == 1 && h.B == 0) n += h.C;
            return n;
        }
        long crewA = g.Crew, crewB = g.Crew, casualties = 0;
        int fights = 0;
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            foreach (var f in Executed(b, DomainKinds.Boarding))
            {
                if (f.Key.TimeMs != now) continue;
                long a = f.Source.Id, d = f.Target.Id;
                long na = a == 1000 ? crewA : crewB, nd = d == 1000 ? crewA : crewB;
                long onD = Expected(na, a, now, d, 0, nd), onA = Expected(nd, d, now, a, 1, na);
                Assert.Equal(onD, HitOn(d, a, now));
                Assert.Equal(onA, HitOn(a, d, now));
                fights++; casualties += onD + onA;
            }
            crewA = D(b, 1000).Crew; crewB = D(b, 2000).Crew;
            return fights >= 30 || D(b, 1000).State != EntityState.Active || D(b, 2000).State != EntityState.Active;
        }, 20);
        Assert.True(fights >= 10, $"only {fights} boarding fights");
        Assert.True(casualties > 0, "no boarding casualty although every fight had a positive expectation");
    }

    /// <summary>
    /// N-R1-05: the unit of a vessel formation's FormationResult is the HULL (CAP-3 counted object), not the person. A transport
    /// that loses crew to gunfire but stays afloat reports Start = Survivors = 1 and Killed = 0, while its people's losses are
    /// in the battle's personnel report (BattleResult.Personnel) — the source for any per-person casualty measure.
    /// </summary>
    [Fact]
    public void VesselFormationResultsCountHullsAndThePersonnelReportCountsPeople()
    {
        var gun = SixRounds(10, 5);                                  // at most 60 hull of 200: the transport stays afloat
        var s = Sea(100_000, 5, null, Ship(1, 0, Tp, M(3000, 2000)),
                    Ship(5, 1, DomainProfiles.TurretWarship with { Weapons = new[] { gun } }, M(5000, 2000)));
        var b = new TacticalBattle(s, PassiveController.Instance, Script((0, new OrderRequest(5, OrderKind.Attack, 0, 1))));
        var r = RunChecked(b);
        var f1 = r.Formations.Single(f => f.FormationId == 1);
        Assert.Equal(Domain.Naval, f1.Domain);
        Assert.Equal(1, f1.Start);
        Assert.Equal(1, f1.Survivors);
        Assert.Equal(0, f1.Killed);
        Assert.Equal(0, f1.PersonnelLoss);
        long crewLost = r.Personnel!.Count(1, PersonnelPlace.Killed);
        Assert.True(crewLost > 0, "precondition: the transport lost crew");
        Assert.Equal(Tp.Crew, Enum.GetValues<PersonnelPlace>().Sum(pl => r.Personnel.Count(1, pl)));   // every crewman accounted for
    }
}
