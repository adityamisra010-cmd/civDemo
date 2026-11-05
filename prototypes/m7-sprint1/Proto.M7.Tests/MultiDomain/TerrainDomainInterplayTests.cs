using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>
/// [TR x lane N] Where lane TR (terrain, deployment thirds, the DIR5 §2.6 hidden-stationary rule) meets lane N (naval, air,
/// transports, combined arms, snipers). Items 1-5 of TacticalBattle.HiddenStationaryDomain.cs, the terrain sight of the domain
/// observers and gunners, deployment of non-ground formations, and the measured attribution of the moved amphibious golden.
/// Coast grids: 64 x 40 cells of 50 m, water for x &lt;= 19.
/// </summary>
public class TerrainDomainInterplayTests
{
    private const long Cell = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell);
    private static readonly Q32 Tiny = Q32.FromRatio(1, 1 << 20);
    /// <summary>TEST INPUT: a transport that sees only 500 m (so it does not find the distant enemy).</summary>
    private static readonly VesselClass ShortSighted = DomainProfiles.SteamTransport with { SightMm = 500_000 };

    private static TacticalRules Legacy => new() { Inactivity = InactivityModel.LegacySideDisengage };

    private static ConcealmentTable ForestConceals(Q32 forest)
    {
        var t = new Q32[TerrainCostTable.ClassCount];
        for (int i = 0; i < t.Length; i++) t[i] = ConcealmentTable.PlaceholderTestTable().Multiplier((TerrainClass)i);
        t[(int)TerrainClass.Forest] = forest;
        return ConcealmentTable.Create(t);
    }

    // ---------------------------------------------------------------- item 1: vessel / aircraft progress feeds G

    /// <summary>
    /// A battle fought ONLY by ships (no land formation at all) must not end as a Standoff while the ships fight: their
    /// progress keeps the whole-field idle counter at 0. Measured: the sail line battle of the lane N golden runs well past
    /// five pulses and ends by elimination. (Mutant "domain progress ignored": Standoff at the end of pulse 4.)
    /// </summary>
    [Fact]
    public void ANavalOnlyBattleIsNotEndedAsAStandoffWhileTheShipsFight()
    {
        var b = new TacticalBattle(
            MultiDomainScenarios.LineBattle(DomainProfiles.ShipOfTheLine, 50_000, 42, new DomainRules { Wind = true, WindFrom = MultiDomainScenarios.Deg(90) }),
            Script((0, new OrderRequest(1, OrderKind.Attack, 0, 2))), Script((0, new OrderRequest(2, OrderKind.Attack, 0, 1))));
        var r = RunChecked(b, 400);
        Assert.NotEqual(BattleEndReason.Standoff, r.Reason);
        Assert.True(r.Pulses > 5, $"pulses {r.Pulses}");
        Assert.Contains(r.Formations, f => f.Killed + f.Surrendered > 0);
    }

    // ---------------------------------------------------------------- item 2: lane N bookkeeping still runs under DIR5

    /// <summary>
    /// Vessel morale recovery under Hold (lane N, DomainPulseEnd) runs under the DIR5 model exactly as under the legacy model:
    /// two idle, mutually unseen transports, the side-0 one starting at morale 500 (above the rout threshold 300). Both
    /// battles last five pulses (DIR5: Standoff; legacy: BothDisengaged) and the recovered morale is identical: 500 + 5 x 20
    /// (HoldRecovery) = 600. (Mutant "bookkeeping skipped under DIR5": morale stays 500.)
    /// </summary>
    [Fact]
    public void VesselMoraleRecoversUnderDir5ExactlyAsUnderTheLegacyModel()
    {
        long Morale(TacticalRules rules, out BattleResult r)
        {
            var s = Sea(Cell, 1, null,
                Ship(1, 0, ShortSighted, C(5, 20)) with { Morale = 500 },
                Ship(2, 1, ShortSighted, C(60, 20))) with { Rules = rules };
            r = RunChecked(new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance), 50);
            return r.Formations.Single(f => f.FormationId == 1).MoraleEnd;
        }
        long dir5 = Morale(new TacticalRules(), out var r5), legacy = Morale(Legacy, out var rl);
        Assert.Equal(BattleEndReason.Standoff, r5.Reason);
        Assert.Equal(BattleEndReason.BothDisengaged, rl.Reason);
        Assert.Equal(rl.Pulses, r5.Pulses);
        Assert.Equal(5, r5.Pulses);
        Assert.Equal(600, dir5);
        Assert.Equal(legacy, dir5);
    }

    // ---------------------------------------------------------------- item 3: the mutual guard counts every domain

    /// <summary>
    /// Two lone riflemen, mutually unseen and idle, neither exempt. With the mutual guard ON (TacticalRules.MutualStandoffGuard,
    /// PROPOSAL P-TR1 — default OFF since verify-TR-r1 V3): in a land-only battle the guard ends it as a Standoff at the end of
    /// pulse 4 (nobody routed). Add a transport to side 1 (it sees nobody and is not judged by the rule): the routs no longer
    /// eliminate BOTH sides, so both riflemen rout and side 1 holds the field with its ship. (Mutant "guard over land
    /// formations only": Standoff in the second battle too.) DEFAULT (guard off): the land-only battle routs both riflemen and
    /// ends BothEliminated at the end of pulse 4.
    /// </summary>
    [Fact]
    public void TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity()
    {
        BattleResult Run(bool ship, bool guard = true)
        {
            var forms = new List<FormationSpec>
            {
                new(1, 0, DomainProfiles.Riflemen, 1, C(60, 2)),
                new(2, 1, DomainProfiles.Riflemen, 1, C(60, 38)),
            };
            if (ship) forms.Add(Ship(3, 1, ShortSighted, C(5, 20)));
            return RunChecked(new TacticalBattle(OnGrid(Coast(Cell, 20), 1, null, new TacticalRules { MutualStandoffGuard = guard }, forms.ToArray()), PassiveController.Instance, PassiveController.Instance), 50);
        }
        var byDefault = Run(ship: false, guard: false);
        Assert.Equal(BattleEndReason.BothEliminated, byDefault.Reason);
        Assert.Equal(5, byDefault.Pulses);
        Assert.All(byDefault.Formations, f => { Assert.Equal(0, f.ActiveAtEnd); Assert.Equal(1, f.RoutedEscaped + f.Surrendered); });
        var land = Run(ship: false);
        Assert.Equal(BattleEndReason.Standoff, land.Reason);
        Assert.Equal(5, land.Pulses);
        Assert.All(land.Formations, f => Assert.Equal(1, f.ActiveAtEnd));
        var mixed = Run(ship: true);
        Assert.Equal(BattleEndReason.SideEliminated, mixed.Reason);
        Assert.Equal(0, mixed.Side);
        Assert.Equal(1, mixed.HoldsField);
        Assert.Equal(5, mixed.Pulses);
        Assert.All(mixed.Formations.Where(f => f.Domain == Domain.Land), f => Assert.Equal(1, f.RoutedEscaped));
    }

    // ---------------------------------------------------------------- item 4: exemption (a) across domains

    /// <summary>
    /// Exemption (a) with an enemy VESSEL: one rifleman concealed in a shore forest (TEST INPUT concealment 2^-20, so the ship
    /// never finds it), its only enemy a transport offshore. Engagement reach = 6 cells x 50 m + one pulse at 1.3 m/s =
    /// 378 m. Ship 150 m away: exempt (a), the battle ends as a Standoff; ship 900 m away: routed (side 0 eliminated).
    /// (Mutant "domain enemies not counted": routed at 150 m too.)
    /// </summary>
    [Fact]
    public void AConcealedFormationWithAnEnemyVesselInReachIsExempt()
    {
        (BattleResult R, TacticalBattle B) Run(long shipX)
        {
            var g = Coast(Cell, 20);
            g.Set(20, 20, TerrainClass.Forest);
            var s = OnGrid(g, 1, null, null,
                new FormationSpec(1, 0, DomainProfiles.Riflemen, 1, C(20, 20)),
                Ship(2, 1, ShortSighted, C(shipX, 20))) with { Concealment = ForestConceals(Tiny) };
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            return (RunChecked(b, 50), b);
        }
        var (near, bn) = Run(17);                                   // 150 m
        Assert.Equal(BattleEndReason.Standoff, near.Reason);
        Assert.False(bn.EverDetected(1, 1000));
        Assert.Contains(bn.InactivityVerdicts, v => v.FormationId == 1 && v.Exemptions == InactivityExemption.ConcealedEnemyInReach && !v.Routed);
        var (far, bf) = Run(2);                                     // 900 m
        Assert.Equal(BattleEndReason.SideEliminated, far.Reason);
        Assert.Equal(0, far.Side);
        Assert.Contains(bf.InactivityVerdicts, v => v.FormationId == 1 && v.Exemptions == InactivityExemption.None && v.Routed);
    }

    /// <summary>
    /// Exemption (a) with PERSONAL concealment (DIR5 §2.6 "active concealment", lane N's camouflaged sniper): a sniper standing in
    /// OPEN ground (no concealing terrain; camouflage TEST INPUT 2^-20, unarmed so it never fires) with an enemy holding 60 m
    /// away is exempt; with the enemy 200 m away (beyond its 79.2 m reach) it routs. (Mutant "personal concealment not
    /// counted": routed at 60 m too.)
    /// </summary>
    [Fact]
    public void ACamouflagedSniperInTheOpenWithAnEnemyInReachIsExempt()
    {
        (BattleResult R, TacticalBattle B) Run(long enemyCellX)
        {
            var sniper = DomainProfiles.Sniper with { PersonalConcealment = Tiny, Ranged = 0, RangeCells = 0 };
            var s = new BattleSetup(TacticalScenarios.OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                TacticalScenarios.DefaultSides(),
                new[]
                {
                    new FormationSpec(1, 0, sniper, 1, TacticalScenarios.Cell(10, 16)),
                    new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, TacticalScenarios.Cell(enemyCellX, 16)),
                }, 9);
            var b = new TacticalBattle(s, PassiveController.Instance, Script((0, new OrderRequest(2, OrderKind.Hold, 0))));
            return (RunChecked(b, 50), b);
        }
        var (near, bn) = Run(13);                                    // 60 m
        Assert.Equal(BattleEndReason.Standoff, near.Reason);
        Assert.False(bn.EverDetected(1, 1000));
        Assert.Contains(bn.InactivityVerdicts, v => v.FormationId == 1 && v.Exemptions == InactivityExemption.ConcealedEnemyInReach && !v.Routed);
        var (far, bf) = Run(20);                                     // 200 m
        Assert.Equal(BattleEndReason.SideEliminated, far.Reason);
        Assert.Contains(bf.InactivityVerdicts, v => v.FormationId == 1 && v.Routed);
    }

    // ---------------------------------------------------------------- item 5: bare point fire is not progress

    /// <summary>
    /// A warship (TEST INPUT: 500 m sight, so it never finds a target) bombards an EMPTY point for the whole battle; the only
    /// enemy holds explicitly far away (exempt). Under DIR5 the bare point shots are not progress, so the battle ends as a
    /// Standoff (measured: 6 pulses — five idle pulses after pulse 0) although the ship fires every reload. Under the mutant "point fire is progress" the
    /// battle never ends (measured on the first integration attempt: the amphibious battle tripped the 400-pulse guard) —
    /// here the 40-pulse harness guard fails. Control: the same bombardment aimed AT the enemy hits it and decides the battle.
    /// </summary>
    [Fact]
    public void ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive()
    {
        var gunship = DomainProfiles.TurretWarship with { SightMm = 500_000 };
        (BattleResult R, TacticalBattle B) Run(Vec2L aim)
        {
            var s = OnGrid(Coast(Cell, 20), 3, null, null,
                Ship(1, 0, gunship, C(10, 20)),
                new FormationSpec(2, 1, DomainProfiles.Riflemen, 10, C(34, 20)));
            var b = new TacticalBattle(s, Script((0, new OrderRequest(1, OrderKind.Bombard, 0, -1, aim))),
                                       Script((0, new OrderRequest(2, OrderKind.Hold, 0))));
            return (RunChecked(b, 40), b);
        }
        var (empty, be) = Run(C(40, 5));
        Assert.Equal(BattleEndReason.Standoff, empty.Reason);
        Assert.Equal(6, empty.Pulses);
        long pointShots = Executed(be, DomainKinds.DomainFire).Count(e => e.Source.Id == 1000 && e.Target.IsNone);
        Assert.True(pointShots >= 40, $"point shots {pointShots}");          // it fired all along
        Assert.Equal(0, empty.Formations.Single(f => f.FormationId == 1).DamageDealt);
        var (onTarget, _) = Run(C(34, 20));
        Assert.True(onTarget.Formations.Single(f => f.FormationId == 1).DamageDealt > 0);
        Assert.Equal(BattleEndReason.SideEliminated, onTarget.Reason);       // measured: the riflemen break in the first pulse
        Assert.Equal(1, onTarget.Side);
    }

    // ---------------------------------------------------------------- terrain sight for the domain observers

    /// <summary>
    /// [TR] Vessels and land observers of vessels use the lane-TR terrain sight (layers + structures): a ship does not see a
    /// rifleman standing right behind a shore building (8 m roof), and a rifleman behind a building does not see the ship. With
    /// the Slice-C-equivalent table (and the building made transparent in the concealment table, so only the structure rule
    /// differs) both are seen. (Mutants: Slice C LineOfSight back in the vessel-observer / land-observer path.)
    /// </summary>
    [Fact]
    public void ABuildingBlocksTheSightBetweenAShipAndTroopsBehindIt()
    {
        (bool ShipSawTroops, bool TroopsSawShip) Run(TerrainEffects fx)
        {
            var g = Coast(Cell, 20);
            g.Set(21, 20, TerrainClass.Building);
            var conceal = new Q32[TerrainCostTable.ClassCount];
            for (int i = 0; i < conceal.Length; i++) conceal[i] = ConcealmentTable.PlaceholderTestTable().Multiplier((TerrainClass)i);
            conceal[(int)TerrainClass.Building] = Q32.One;
            var s = OnGrid(g, 1, null, null,
                Ship(1, 0, ShortSighted, C(17, 20)),
                new FormationSpec(2, 1, DomainProfiles.Riflemen, 1, C(22, 20))) with { Effects = fx, Concealment = ConcealmentTable.Create(conceal) };
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            b.RunUntilPulse(2);
            return (b.EverDetected(0, 2000), b.EverDetectedDomain(1, 1000));
        }
        var on = Run(TerrainEffects.Placeholder);
        Assert.False(on.ShipSawTroops);
        Assert.False(on.TroopsSawShip);
        var off = Run(TerrainEffects.SliceCEquivalent());
        Assert.True(off.ShipSawTroops);
        Assert.True(off.TroopsSawShip);
    }

    // ---------------------------------------------------------------- deployment thirds and non-ground formations

    /// <summary>
    /// [TR] The thirds rule places GROUND formations only: a vessel on a lake in the middle third and troops that start aboard
    /// it are not ground deployments — the phase accepts the deployment, refuses edits to them (NotGroundFormation), leaves
    /// them unchanged on Close, and the battle constructs. The ground formations are still held to their thirds. (Mutant
    /// "placements include every formation": the vessel is refused.)
    /// </summary>
    [Fact]
    public void DeploymentThirdsApplyToGroundFormationsOnly()
    {
        var g = Coast(Cell, 20);
        for (int y = 15; y <= 25; y++) for (int x = 30; x <= 33; x++) g.Set(x, y, TerrainClass.Water);   // a lake in the middle third
        var vessel = Ship(3, 1, ShortSighted, C(31, 20));
        var aboard = new FormationSpec(4, 1, DomainProfiles.Riflemen, 5, C(31, 20)) { EmbarkedOn = 3 };
        var s = OnGrid(g, 1, null, null,
            new FormationSpec(1, 0, DomainProfiles.Riflemen, 1, C(21, 20)) { Entities = new[] { new EntitySpec(1000, C(20, 20)) } },
            new FormationSpec(2, 1, DomainProfiles.Riflemen, 1, C(60, 20)) { Entities = new[] { new EntitySpec(2000, C(60, 20)) } },
            vessel, aboard);
        Assert.True(DeploymentZones.Check(s).Accepted);
        var phase = new DeploymentPhase(s);
        Assert.Equal(DeploymentRefusal.NotGroundFormation, phase.Propose(1, 3, C(30, 20)).Refusal);
        Assert.Equal(DeploymentRefusal.NotGroundFormation, phase.Propose(1, 4, C(30, 20)).Refusal);
        Assert.Equal(DeploymentRefusal.MiddleThird, phase.Propose(0, 1, C(31, 2), new[] { new EntitySpec(1000, C(31, 2)) }).Refusal);
        Assert.True(phase.Close(out var battle).Accepted);
        Assert.Same(vessel, battle!.Formations.Single(f => f.Id == 3));
        Assert.Same(aboard, battle.Formations.Single(f => f.Id == 4));
        Assert.Equal(DeploymentRule.Thirds, battle.Deployment);
        var b = new TacticalBattle(battle, PassiveController.Instance, PassiveController.Instance);
        Assert.Equal(EntityState.Embarked, L(b, 4000).State);
    }

    // ---------------------------------------------------------------- the moved amphibious golden: measured attribution

    /// <summary>
    /// ATTRIBUTION of the amphibious golden that lane TR moved (lane N's constants are kept here as the control). With the
    /// terrain coefficients OFF (Slice-C-equivalent table) and the legacy inactivity model, the battle is lane N's battle bit
    /// for bit (trace, result and personnel hashes; the state hash differs only because lane TR appended its state to the
    /// encoding). Turning ONLY the missile transmission back to One (every other lane-TR coefficient at its placeholder) also
    /// reproduces lane N's battle: the terrain part of the move is the forest's missile transmission (the defending snipers in
    /// the forest belt deal 192 damage instead of 428). The rest of the move is the DIR5 inactivity model (Standoff after 19
    /// pulses instead of SideDisengaged after 15: the holding guns and the fighter stay instead of escaping intact).
    /// </summary>
    [Fact]
    public void TheAmphibiousGoldenMovesOnlyThroughMissileTransmissionAndTheDir5Rule()
    {
        BattleResult Run(TerrainEffects fx, InactivityModel model)
        {
            var s0 = MultiDomainScenarios.Amphibious(42);
            var s = s0 with { Effects = fx, Rules = s0.Rules with { Inactivity = model } };
            return RunChecked(new TacticalBattle(s, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence()));
        }
        const string laneNResult = "216C4A45CFB4B7624FBCAB3D2AE4B6500C8E0A4343DB7B0DB499C89A71A8AE15";   // measured on 7118ef0
        var off = Run(TerrainEffects.SliceCEquivalent(), InactivityModel.LegacySideDisengage);
        Assert.Equal(LaneNAmphibTrace, off.TraceHash);
        Assert.Equal(laneNResult, off.ResultHash());
        Assert.Equal(LaneNAmphibPersonnel, off.Personnel!.Hash);
        var tr1 = Run(WithTransmissionOne(TerrainEffects.Placeholder), InactivityModel.LegacySideDisengage);
        Assert.Equal(LaneNAmphibTrace, tr1.TraceHash);
        Assert.Equal(laneNResult, tr1.ResultHash());
        var on = Run(TerrainEffects.Placeholder, InactivityModel.LegacySideDisengage);
        Assert.NotEqual(LaneNAmphibTrace, on.TraceHash);
        Assert.Equal(428, off.Formations.Single(f => f.FormationId == 23).DamageDealt);
        Assert.Equal(192, on.Formations.Single(f => f.FormationId == 23).DamageDealt);
        Assert.Equal(BattleEndReason.SideDisengaged, on.Reason);
        var dir5 = Run(TerrainEffects.Placeholder, InactivityModel.HiddenStationary);
        Assert.Equal(DomainOrdersAndGoldenTests.AmphibTrace, dir5.TraceHash);
        Assert.Equal(BattleEndReason.Standoff, dir5.Reason);
        Assert.Equal(19, dir5.Pulses);
        Assert.Equal(15, on.Pulses);
        Assert.All(dir5.Formations, f => Assert.Equal(0, f.DisengagedEscaped));
    }

    /// <summary>Lane N's amphibious goldens as measured on 7118ef0 (before lane TR) — the attribution control.</summary>
    public const string LaneNAmphibTrace = "5B72E7570A0C988C45AF695EB6CC22CD61D19B334F89788024477D7C1AA39AAF";
    public const string LaneNAmphibPersonnel = "E28B1BE9CB570C32707A6757BBDC22F3A33891C64A15CB9C95245CA20765C963";

    private static TerrainEffects WithTransmissionOne(TerrainEffects p)
    {
        int n = TerrainCostTable.ClassCount;
        var fh = new long[n]; var op = new bool[n]; var tr = new Q32[n]; var ft = new Q32[n]; var oc = new Q32[n];
        for (int i = 0; i < n; i++)
        {
            var c = (TerrainClass)i;
            fh[i] = p.FeatureHeightMm(c); op[i] = p.Opaque(c); tr[i] = Q32.One; ft[i] = p.Footing(c); oc[i] = p.OrderCap(c);
        }
        return TerrainEffects.Create(fh, op, tr, ft, oc, p.ClimbNum, p.ClimbDen, p.MeleeSlope, p.MissileElevation, p.GradientCap, p.CohesionFloor);
    }
}
