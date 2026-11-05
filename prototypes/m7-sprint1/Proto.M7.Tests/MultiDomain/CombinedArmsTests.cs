using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Cross-domain engagements: shore bombardment, land guns against vessels, air strikes on vessels, escort,
/// danger-close refusal, engagement eligibility (NotCapable), and the default resolver for naval battles.</summary>
public class CombinedArmsTests
{
    private const long Cell = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell);

    [Fact]
    public void ShoreBombardmentKillsThroughTheLandDamagePathAndBreaksMorale()
    {
        var s = OnGrid(Coast(Cell, 20), 3, null, null,
            Ship(1, 0, DomainProfiles.TurretWarship, C(10, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 40, C(24, 20)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, 0, -1, C(24, 20)))), PassiveController.Instance);
        var r = RunChecked(b, 30);
        var f2 = r.Formations.Single(f => f.FormationId == 2);
        Assert.True(f2.Killed > 0);
        Assert.Equal(f2.Killed, r.Personnel!.Count(2, PersonnelPlace.Killed));
        Assert.True(f2.MoraleEnd < s.Rules.MoraleMax);
        Assert.True(r.Formations.Single(f => f.FormationId == 1).DamageDealt > 0);
    }

    [Fact]
    public void DangerCloseAreaFireIsRefusedNextToFriendlies()
    {
        // The bombard point lies on the warship side's OWN landed troops: not one shell is fired at it.
        var s = OnGrid(Coast(Cell, 20), 3, null, null,
            Ship(1, 0, DomainProfiles.TurretWarship, C(10, 20)),
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 10, C(24, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(60, 38)));
        var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, 0, -1, C(24, 20)))), PassiveController.Instance);
        b.RunUntilPulse(2);
        // (the warship still engages tracked enemies elsewhere — weapons free — but never the point among its own troops)
        Assert.DoesNotContain(Executed(b, DomainKinds.DomainFire), e => e.Source.Id == 1000 && e.Target.IsNone);
        // Teeth: the same order on a point clear of friendlies is fired.
        var b2 = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, 0, -1, C(40, 10)))), PassiveController.Instance);
        b2.RunUntilPulse(2);
        Assert.Contains(Executed(b2, DomainKinds.DomainFire), e => e.Source.Id == 1000 && e.Target.IsNone);
    }

    [Fact]
    public void CoastalGunsHoleShipsAndStrikeAircraftBombThem()
    {
        var gun = new FormationSpec(2, 1, DomainProfiles.GunCrew, 3, C(24, 20)) { Weapons = new[] { DomainProfiles.CoastalGun } };
        var s = OnGrid(Coast(Cell, 20), 5, null, null, Ship(1, 0, DomainProfiles.SteamTransport, C(12, 20)), gun);
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        RunUntil(b, () => D(b, 1000).Hp < DomainProfiles.SteamTransport.Hull, 10);
        Assert.Contains(Executed(b, DomainKinds.DomainFire), e => e.Source.Id >= 2000 && e.Source.Id < 2003 && e.Target.Id == 1000);

        var strike = MultiDomainScenarios.Flight(3, 1, DomainProfiles.StrikeAircraft, 1);
        var s2 = OnGrid(Coast(Cell, 20), 5, null, null, Ship(1, 0, DomainProfiles.SteamTransport, C(12, 20)),
                        new FormationSpec(2, 1, DomainProfiles.Riflemen, 5, C(60, 38)), strike) with
        { MultiDomain = new MultiDomainSetup { AirBases = new[] { C(0, 20), C(40, 20) } } };
        var b2 = new TacticalBattle(s2, PassiveController.Instance, Script((0, new OrderRequest(3, OrderKind.Attack, 0, 1))));
        RunUntil(b2, () => D(b2, 3000).Phase == AirPhase.Returning || D(b2, 3000).State != EntityState.Active, 10);
        Assert.Equal(DomainProfiles.Bombs.Ammo, Executed(b2, DomainKinds.DomainFire).Count(e => e.Source.Id == 3000 && e.Target.Id == 1000));
    }

    [Fact]
    public void EscortEngagesTheInterceptor()
    {
        var g = new TerrainGrid(64, 40, Cell, TerrainClass.Open);
        var s = OnGrid(g, 2, null, null,
            new FormationSpec(1, 0, DomainProfiles.Riflemen, 5, C(5, 20)),
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 5, C(58, 20)),
            MultiDomainScenarios.Flight(3, 0, DomainProfiles.StrikeAircraft, 1),
            MultiDomainScenarios.Flight(4, 0, DomainProfiles.Fighter, 1),
            MultiDomainScenarios.Flight(5, 1, DomainProfiles.Fighter, 1));
        var b = new TacticalBattle(s,
            Script((0, new OrderRequest(3, OrderKind.Patrol, 0, -1, C(40, 20))), (0, new OrderRequest(4, OrderKind.Escort, 1, 3))),
            Script((0, new OrderRequest(5, OrderKind.Attack, 0, 3))));
        b.RunUntilPulse(5);
        Assert.Contains(Executed(b, DomainKinds.DomainFire), e => e.Source.Id == 4000 && e.Target.Id == 5000);
    }

    [Fact]
    public void IneligibleOrdersAreRefusedAsNotCapable()
    {
        var s = OnGrid(Coast(Cell, 20), 1, null, null,
            Ship(1, 0, DomainProfiles.SteamTransport, C(10, 20)),
            Ship(2, 1, DomainProfiles.SteamTransport, C(10, 30)),
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 5, C(25, 20)),
            MultiDomainScenarios.Flight(4, 0, DomainProfiles.ReconAircraft, 1),
            new FormationSpec(5, 1, DomainProfiles.Riflemen, 5, C(60, 30)));
        var c0 = Script(
            (0, new OrderRequest(1, OrderKind.Attack, 0, 2)),        // transport: no surface weapon, no ram
            (0, new OrderRequest(4, OrderKind.Attack, 0, 5)),        // recon aircraft: unarmed
            (0, new OrderRequest(4, OrderKind.Board, 1, 2)),         // aircraft cannot board
            (0, new OrderRequest(3, OrderKind.Bombard, 0, 5)),       // riflemen have no area weapon
            (0, new OrderRequest(3, OrderKind.Patrol, 1, -1, C(30, 20))),   // land formations do not patrol
            (0, new OrderRequest(1, OrderKind.Embark, 1, 1)));       // vessels do not embark
        var b = new TacticalBattle(s, c0, PassiveController.Instance);
        b.RunUntilPulse(1);
        Assert.All(b.Orders.Where(o => o.Side == 0), o => Assert.Equal(OrderInvalid.NotCapable, o.Invalid));
    }

    [Fact]
    public void NavalBattleDefaultsToTheTacticalResolverAndIsResolvedTactically()
    {
        var setup = MultiDomainScenarios.LineBattle(DomainProfiles.TurretWarship, 200_000, 3);
        var r = BattleResolution.Resolve(setup, Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), Script((0, new OrderRequest(2, OrderKind.Attack, 0, 1))));
        Assert.Equal(ResolverKind.Tactical, r.Resolver);
        Assert.All(r.Formations, f => Assert.Equal(Domain.Naval, f.Domain));
        Assert.All(r.Formations, f => Assert.True(f.Conserved));
        Assert.True(r.Formations.Sum(f => f.Killed + f.Surrendered) > 0);   // fought out, not auto-resolved
    }

    /// <summary>
    /// No invented caps: crew-served guns belong to the siege family (field and coastal artillery) or to air defence, which
    /// have no Director cap (D-076 §5 CAP-5; D-079 R6). A battery larger than the mortar cap of 5 is accepted; the capped
    /// families keep their caps (snipers 2, ships 5).
    /// </summary>
    [Fact]
    public void CrewServedGunFamiliesHaveNoInventedCap()
    {
        Assert.Equal(CapFamily.Uncapped, DomainProfiles.GunCrew.CapFamily);
        Assert.Null(ProposedCaps.Proposed.Check(CapFamily.Uncapped, 500));
        Assert.Equal(int.MaxValue, ProposedCaps.Proposed.CapOf(CapFamily.Uncapped));
        Assert.Throws<ArgumentException>(() => ProposedCaps.Proposed.WithOverride(CapFamily.Uncapped, 5));
        Assert.NotNull(ProposedCaps.Proposed.Check(CapFamily.Uncapped, 0));
        var battery = new FormationSpec(2, 1, DomainProfiles.GunCrew, 12, C(30, 20)) { Weapons = new[] { DomainProfiles.AaGun } };
        var s = OnGrid(Coast(Cell, 20), 1, null, null, new FormationSpec(1, 0, DomainProfiles.Riflemen, 5, C(25, 5)), battery);
        _ = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);           // 12 AA teams: accepted
        var fleet = OnGrid(Coast(Cell, 20), 1, null, null, MultiDomainScenarios.Fleet(1, 0, DomainProfiles.TurretWarship, 6, C(10, 20)), battery);
        Assert.Contains("Ships", Assert.Throws<ArgumentException>(() => new TacticalBattle(fleet, PassiveController.Instance, PassiveController.Instance)).Message);
    }

    /// <summary>
    /// ONE ACQUISITION DRAW per (side, target, instant) against the best observer of ANY domain (a crowd of observers does
    /// not see better than its best one). Metamorphic test under common random numbers: ten riflemen stand still in a single
    /// forest cell (detection chance 1/2 x 1/64 per instant for every observer here). Side 0 watches them from a transport
    /// offshore; in the second battle a land formation that sees them exactly as well (same LOS clarity) is added within
    /// sight. With one draw against max(p_land, p_vessel) = p_vessel, every rifleman is first acquired at EXACTLY the same
    /// instant in both battles. Teeth: the land formation on its own does acquire riflemen in the window, so an independent
    /// second draw would advance some first acquisitions.
    /// </summary>
    [Fact]
    public void CrossDomainAcquisitionIsOneDrawAgainstTheBestObserver()
    {
        long[] FirstSeen(bool vessel, bool landNear)
        {
            var g = Coast(Cell, 20);
            g.Set(30, 20, TerrainClass.Forest);
            var forms = new List<FormationSpec>
            {
                new(1, 0, DomainProfiles.Riflemen, 5, landNear ? C(22, 20) : C(22, 2)),      // 400 m away, or out of sight (985 m)
                new(2, 1, DomainProfiles.Riflemen, 10, C(30, 20)),
            };
            if (vessel) forms.Add(Ship(3, 0, DomainProfiles.SteamTransport, C(19, 20)));    // 550 m offshore
            var b = new TacticalBattle(OnGrid(g, 21, null, null, forms.ToArray()), PassiveController.Instance, PassiveController.Instance);
            var first = new long[10];
            Array.Fill(first, -1);
            RunUntil(b, () =>
            {
                for (int k = 0; k < 10; k++) if (first[k] < 0 && b.EverDetected(0, 2000 + k)) first[k] = b.Scheduler.NowMs;
                return b.CurrentPulse >= 4;
            }, 5);
            return first;
        }
        var vesselOnly = FirstSeen(vessel: true, landNear: false);
        var both = FirstSeen(vessel: true, landNear: true);
        var landOnly = FirstSeen(vessel: false, landNear: true);
        Assert.Contains(vesselOnly, t => t >= 0);                                   // the vessel acquires riflemen in the window
        Assert.Contains(landOnly, t => t >= 0);                                     // so does the land formation on its own
        Assert.Equal(vesselOnly, both);                                             // together: exactly the vessel's acquisitions
    }
}
