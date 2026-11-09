using System.Security.Cryptography;
using Proto.M7.Caps;
using Proto.M7.Events;
using Proto.M7.Sandbox;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.Tests.Outcomes;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.LaneD;

/// <summary>
/// Lane D item 3: the War Sandbox core (Proto.M7/Sandbox). Field = lane D's use of the 48 x 32 open battle field of
/// TacticalScenarios (thirds along x: West third cells 0-15, middle 16-31, East third 32-47). Every number is a TEST INPUT.
/// </summary>
public class SandboxTests
{
    private static readonly Vec2L West = Cell(8, 16), East = Cell(39, 16);

    private static SandboxSession QuickAiBattle(ulong seed = 11, int n0 = 50, int n1 = 50)
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), seed);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, n0, West, out _));
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, n1, East, out _));
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        return s;
    }

    /// <summary>DR30: an AI-vs-AI sandbox battle uses the AutoResolver (lane P's model 2 as it stands; its acceptance is OPEN, Q4),
    /// with no rendering, and with the resolver exactly as the campaign uses it: the sandbox result equals a direct AutoResolver run
    /// of the same deployed setup ("Campaign resolver rules remain unchanged").</summary>
    [Fact]
    public void AiVsAi_UsesTheAutoResolverModel2_Unchanged()
    {
        var s = QuickAiBattle();
        Assert.Equal(ResolverKind.Auto, s.ResolverUsed);
        Assert.Equal(ResolverKind.Auto, s.Result!.Resolver);
        Assert.Equal(2, new AutoCoefficients().ModelVersion);
        var direct = new AutoResolver().Resolve(s.DeployedSetup!, new KnowledgeAi(), new KnowledgeAi());
        Assert.Equal(direct.ResultHash(), s.Result.ResultHash());
        Assert.Equal(ResolverKind.Auto, ResolverPolicy.Default(s.DeployedSetup!, Commander.Ai, Commander.Ai));
    }

    /// <summary>D15 (PROPOSAL accounting form): forces are minted from the sandbox's own mint; the battle's casualties go through
    /// lane W's collapse into the sandbox's own sinks; the sandbox total is the mint's opening exactly, at every stage.</summary>
    [Fact]
    public void SandboxAccounting_ConservesExactly_InItsOwnLedger()
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 5);
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
        s.AddForce(0, 4, 402, 60, West, out _);
        s.AddForce(0, 3, 301, 50, Cell(4, 8), out _);
        s.AddForce(1, 3, 301, 90, East, out var east);
        Assert.Equal(2, east.Length);                                                     // 90 at cap 50 -> 45 + 45
        Assert.Equal(SandboxSession.MintOpening - 200, s.Ledger.Balance(s.MintAccount));
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
        s.OpenDeployment();
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai);
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
        long dead = 0, wounded = 0, captured = 0, deserted = 0;
        foreach (var c in s.Casualties)
        {
            Assert.Equal(c.Persons, c.Fit + c.Wounded + c.RoutedReturned + c.Deserted + c.Dead + c.Captured);
            Assert.Equal(c.Fit + c.RoutedReturned, s.Persons(c.FormationId));
            dead += c.Dead; wounded += c.Wounded; captured += c.Captured; deserted += c.Deserted;
        }
        Assert.Equal((dead, wounded, captured, deserted), (s.DeadTotal, s.WoundedTotal, s.CapturedTotal, s.DesertedTotal));
        Assert.True(dead > 0);
    }

    /// <summary>Prisoners go to the sandbox's captured sink, the dead to its dead sink — each equal to the per-formation collapse sums.
    /// A TacticalResolver battle (a passive player side holding) whose losing side's exit edge is walled off: its routing men cannot
    /// reach the edge and surrender (the TacticalResolver's rule), so the battle takes prisoners.</summary>
    [Fact]
    public void SandboxAccounting_PrisonersAndDeadReachTheirOwnSinks()
    {
        var s = PrisonersSession();
        Assert.Equal(ResolverKind.Tactical, s.ResolverUsed);
        long captured = s.Casualties.Sum(c => c.Captured), dead = s.Casualties.Sum(c => c.Dead);
        Assert.True(captured > 0, "the scenario must take prisoners");
        Assert.Equal(captured, s.CapturedTotal);
        Assert.Equal(dead, s.DeadTotal);
        Assert.Equal(s.Result!.Formations.Where(f => f.Side == 0).Sum(f => f.Surrendered), s.Casualties.Where(c => c.Side == 0).Sum(c => c.Captured));
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
        // [verification round 1, D-R1-01] per formation, against the battle's own result (one entity per person in the sandbox):
        // the dead are its killed, the captured its surrendered, the living its survivors, and the side that held the field
        // loses no deserter.
        foreach (var c in s.Casualties)
        {
            var r = s.Result.Formations.Single(x => x.FormationId == c.FormationId);
            Assert.Equal(r.Start, c.Persons);
            Assert.Equal(r.Killed, c.Dead);
            Assert.Equal(r.Surrendered, c.Captured);
            Assert.Equal(r.Survivors, c.Fit + c.Wounded + c.RoutedReturned + c.Deserted);
            if (s.Result.HoldsField == c.Side) Assert.Equal(0, c.Deserted);
        }
    }

    /// <summary>The prisoners scenario: a passive warband of 30 (side 0, a player side that holds) whose West exit is walled off,
    /// against 150 knowledge-AI warriors (side 1). TacticalResolver; seed 17. Its men who rout cannot escape and surrender.</summary>
    private static SandboxSession PrisonersSession()
    {
        var s = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 17);
        for (int y = 0; y < 32; y++) Assert.Equal(SandboxRefusal.None, s.EditTerrain(1, y, TerrainClass.Wall));   // no way to the West edge
        s.AddForce(0, 3, 301, 30, West, out _);                                          // a warband holding
        s.AddForce(1, 3, 301, 150, East, out _);
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Player, SandboxCommand.Ai, player0: PassiveController.Instance));
        return s;
    }

    /// <summary>
    /// [verification round 1, D-R1-01] The sandbox casualty accounting of an AI-vs-AI (AutoResolver) battle is the battle's own
    /// result, per formation, computed here by hand and without calling lane W's collapse (one entity per person, so the
    /// apportionment is exact): dead = killed; captured = surrendered; wounded = 0 (the AutoResolver has no wounded); fit =
    /// survivors − routed escapes; deserted = floor(routed escapes × (MoraleMax − end morale) / MoraleMax) on the side that did
    /// NOT hold the field and 0 on the side that did; returned = routed escapes − deserted. Before round 1 only conservation and
    /// the result hash were tested, and two wrong mappings passed (verifier mutants V9 and V10). Three seeds; each has deserters
    /// on the losing side (teeth for V9) and routed escapes (teeth for V10); two of them also have routed escapes on the side that
    /// held the field.
    /// </summary>
    [Theory]
    [InlineData(11UL)]
    [InlineData(13UL)]
    [InlineData(4UL)]
    public void SandboxAccounting_MatchesTheBattleResult_PerFormation(ulong seed)
    {
        var s = AutoBattleSession(seed);
        Assert.Equal(ResolverKind.Auto, s.ResolverUsed);
        long max = s.DeployedSetup!.Rules.MoraleMax, routed = 0, deserted = 0;
        Assert.Equal(4, s.Casualties.Count);
        foreach (var c in s.Casualties)
        {
            var r = s.Result!.Formations.Single(x => x.FormationId == c.FormationId);
            bool held = s.Result.HoldsField == c.Side;
            long expectDeserted = held ? 0 : r.RoutedEscaped * (max - r.MoraleEnd) / max;
            Assert.Equal(r.Side, c.Side);
            Assert.Equal(r.Start, c.Persons);
            Assert.Equal(r.Killed, c.Dead);
            Assert.Equal(r.Surrendered, c.Captured);
            Assert.Equal(0, c.Wounded);
            Assert.Equal(r.Survivors - r.RoutedEscaped, c.Fit);
            Assert.Equal(expectDeserted, c.Deserted);
            Assert.Equal(r.RoutedEscaped - expectDeserted, c.RoutedReturned);
            Assert.Equal(c.Fit + c.RoutedReturned, s.Persons(c.FormationId));
            routed += r.RoutedEscaped;
            deserted += c.Deserted;
        }
        Assert.True(routed > 0 && deserted > 0, "routed escapes and deserters: the per-formation pin has teeth");
    }

    /// <summary>An AI-vs-AI (AutoResolver) battle: 50 warriors and 60 archers (side 0) against 100 warriors (side 1).</summary>
    private static SandboxSession AutoBattleSession(ulong seed)
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), seed);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 50, West, out _));
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 4, 402, 60, Cell(4, 8), out _));
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 100, East, out _));
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        return s;
    }

    /// <summary>
    /// [verification round 1, D-R1-03] READING C1 (caps proposal §2.2), pinned as the stand-ins' CURRENT behaviour so that a change
    /// to either rule shows here: lane D's caps prototype counts PERSONS (a 3,000-person heavy-infantry formation is refused);
    /// the Campaign Encounter inherits lane W's ENTITY rule (the same 3,000 persons fight as one formation of 50 entities, which
    /// the battle-entry check accepts); the sandbox's own force creation splits 3,000 at the persons cap into 60 formations.
    /// </summary>
    [Fact]
    public void CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b()
    {
        var v = DirectorCaps.Values.Check(1, CapFamily.HeavyInfantry, 3_000);
        Assert.Equal((CapRefusal.ExceedsCap, 3_000L, 50L), (v.Refusal, v.Counted, v.Cap));                       // (a) persons
        var enc = SandboxSession.FromEncounter(new CampaignEncounterSnapshot(OpenField(), new[] { (0, 0, 3, 301, 3_000L, West), (1, 1, 3, 301, 2_500L, East) }), 3);
        Assert.Equal(2, enc.Formations.Count);
        Assert.Equal((3_000L, 50), (enc.Persons(enc.Formations[0].Id), enc.Formations[0].Count));                // (b) entities
        Assert.Equal((2_500L, 50), (enc.Persons(enc.Formations[1].Id), enc.Formations[1].Count));
        Assert.Null(ProposedCaps.Proposed.Check(CapFamily.HeavyInfantry, enc.Formations[0].Count));
        var wide = new SandboxSession(SandboxMode.QuickBattle, new TerrainGrid(120, 100, CellMm, TerrainClass.Open), 3);
        Assert.Equal(SandboxRefusal.None, wide.AddForce(0, 3, 301, 3_000, Cell(30, 0), out var ids));
        Assert.Equal(60, ids.Length);
        Assert.All(ids, i => Assert.Equal(50, wide.Formations.Single(f => f.Id == i).Count));
    }

    /// <summary>ISOLATION (DR30 "Isolated from campaign population and campaign casualties"): a Campaign Encounter loaded from lane
    /// W's campaign world, fought to the end in the sandbox, leaves the campaign world's state hash and every campaign ledger total
    /// identical; and the campaign world, run on afterwards, equals a twin that never met a sandbox.</summary>
    [Fact]
    public void CampaignWorld_StateHashIdentical_BeforeAndAfterASandboxBattle()
    {
        var campaign = OutcomeRig.Build();
        var twin = OutcomeRig.Build();
        long t1 = OutcomeRig.B(3);
        campaign.R.RunUntil(t1);
        twin.R.RunUntil(t1);
        string h0 = campaign.W.StateHashHex();
        var totals0 = OutcomeRig.Totals(campaign.W.Ledger);
        var snap = SandboxEncounters.FromOutcomeWorld(campaign.W, OpenField(), new[] { (0, 0, 3, 301, West), (2, 1, 3, 301, East) });
        Assert.Equal(3_000, snap.Forces[0].Persons);
        Assert.Equal(2_500, snap.Forces[1].Persons);
        var s = SandboxSession.FromEncounter(snap, 3);
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        Assert.True(s.DeadTotal > 0);
        Assert.Equal(h0, campaign.W.StateHashHex());
        Assert.Equal(totals0, OutcomeRig.Totals(campaign.W.Ledger));
        Assert.Equal(3_000, campaign.W.Ledger.Balance(campaign.W.FormationAccount(0)));    // the campaign formation lost nobody
        long t2 = OutcomeRig.B(12);
        campaign.R.RunUntil(t2);
        twin.R.RunUntil(t2);
        Assert.Equal(twin.W.StateHashHex(), campaign.W.StateHashHex());
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
    }

    /// <summary>DR30: "Terrain editing is available in the sandbox" / "Do not allow campaign encounter terrain editing".</summary>
    [Fact]
    public void TerrainEditing_AllowedInTheSandbox_RefusedInACampaignEncounter()
    {
        var quick = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 1);
        Assert.Equal(SandboxRefusal.None, quick.EditTerrain(20, 16, TerrainClass.Forest, 3_000));
        Assert.Equal(TerrainClass.Forest, quick.Terrain.ClassAt(20, 16));
        Assert.Equal(3_000, quick.Terrain.HeightAt(20, 16));
        Assert.Equal(SandboxRefusal.OutOfGrid, quick.EditTerrain(60, 16, TerrainClass.Forest));
        var campaignTerrain = OpenField();
        var enc = SandboxSession.FromEncounter(new CampaignEncounterSnapshot(campaignTerrain, new[] { (0, 0, 3, 301, 40L, West), (1, 1, 3, 301, 40L, East) }), 1);
        Assert.Equal(SandboxRefusal.TerrainLocked, enc.EditTerrain(20, 16, TerrainClass.Forest));
        Assert.Equal(TerrainClass.Open, enc.Terrain.ClassAt(20, 16));
        // the snapshot is a copy: the session's grid is not the campaign's grid
        campaignTerrain.Set(21, 16, TerrainClass.Hills, 500);
        Assert.Equal(TerrainClass.Open, enc.Terrain.ClassAt(21, 16));
        Assert.Throws<ArgumentException>(() => new SandboxSession(SandboxMode.CampaignEncounter, OpenField(), 1));
    }

    /// <summary>DR33 / DR30: any designed unit family regardless of campaign era; campaign availability stays gated by the computed
    /// Age (READING R2). Identities the prototype has no battle profile for are refused (UnknownFamily), not invented.</summary>
    [Fact]
    public void ForceCreation_AnyDesignedFamilyAnyEra_CampaignAvailabilityStillByComputedAge()
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        var sniper = SandboxCatalogue.Find(4, 4990)!;
        Assert.Equal(8, sniper.IdentityAge);
        Assert.False(CampaignAvailability.Available(2, sniper));     // a computed-Age-2 polity cannot field it in the campaign
        Assert.True(CampaignAvailability.Available(8, sniper));
        Assert.False(CampaignAvailability.Available(0, SandboxCatalogue.Find(3, 301)!));
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 4, 4990, 2, West, out _));        // the sandbox does not ask
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 8, 4992, 12, Cell(4, 24), out _));
        Assert.Equal(SandboxRefusal.UnknownFamily, s.AddForce(0, 18, 1801, 5, West, out _)); // submarines: no battle profile
        Assert.Equal(SandboxRefusal.UnknownFamily, s.AddForce(0, 3, 302, 5, West, out _));   // bronze infantry: no profile yet
        Assert.Equal(SandboxRefusal.BadSide, s.AddForce(2, 3, 301, 5, West, out _));
        Assert.Equal(SandboxRefusal.NonPositive, s.AddForce(0, 3, 301, 0, West, out _));
        foreach (var f in SandboxCatalogue.All) Assert.Equal(f.FamilyKey, f.Profile.FamilyKey);
    }

    /// <summary>READING S1, alternative (a) applied: an experimental force larger than one formation's DR24 cap is created as the
    /// fewest formations within the cap (the engine refuses an over-cap formation); uncapped classes stay one formation.</summary>
    [Fact]
    public void ForceCreation_SplitsAtTheDirectorCap()
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        s.AddForce(0, 3, 301, 130, Cell(4, 2), out var hi);
        s.AddForce(0, 4, 4990, 5, Cell(10, 2), out var sn);
        s.AddForce(1, 8, 4992, 100, Cell(40, 2), out var gun);
        long[] Sizes(long[] ids) => ids.Select(i => s.Formations.Single(f => f.Id == i).Count).Select(c => (long)c).ToArray();
        Assert.Equal(new long[] { 44, 43, 43 }, Sizes(hi));
        Assert.Equal(new long[] { 2, 2, 1 }, Sizes(sn));
        Assert.Equal(new long[] { 100 }, Sizes(gun));
        foreach (var f in s.Formations) Assert.Null(ProposedCaps.Proposed.Check(f.Profile.CapFamily, f.Count));
    }

    /// <summary>DR31 / READING R5: deployment only through lane TR's DeploymentPhase — no battle before a valid deployment, the
    /// middle third refused with lane TR's own code, the closed setup carries the Thirds rule; the scenario is frozen once the
    /// deployment phase is open.</summary>
    [Fact]
    public void Deployment_AlwaysThroughTheThirds()
    {
        var s = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 9);
        Assert.Equal(SandboxRefusal.NoForces, s.OpenDeployment());
        s.AddForce(0, 3, 301, 40, West, out var w0);
        s.AddForce(1, 3, 301, 40, Cell(24, 16), out _);                  // placed in the middle third
        Assert.Equal(SandboxRefusal.NotDeployed, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.DeploymentOpen, s.AddForce(0, 3, 301, 5, West, out _));
        Assert.Equal(SandboxRefusal.DeploymentOpen, s.EditTerrain(1, 1, TerrainClass.Forest));
        Assert.Equal(SandboxRefusal.DeploymentRefused, s.CloseDeployment());
        Assert.Equal((long)DeploymentRefusal.MiddleThird, s.Log[^1].A);
        Assert.Equal(SandboxRefusal.DeploymentRefused, s.Deploy(0, w0[0], Cell(20, 16)));          // own formation into the middle
        Assert.Equal((long)DeploymentRefusal.MiddleThird, s.Log[^1].A);
        long east = s.Formations.Single(f => f.Side == 1).Id;
        Assert.Equal(SandboxRefusal.DeploymentRefused, s.Deploy(1, east, Cell(8, 30)));             // into the enemy's third
        Assert.Equal((long)DeploymentRefusal.EnemyThird, s.Log[^1].A);
        Assert.Equal(SandboxRefusal.None, s.Deploy(1, east, East));
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(DeploymentRule.Thirds, s.DeployedSetup!.Deployment);
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        Assert.Equal(SandboxRefusal.BattleStarted, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
    }

    /// <summary>TURN-EXACT ORDER-DELIVERY PIN (replay and inspection): a player order submitted for pulse p with step offset k is
    /// delivered at StartMs + p·PulseMs + k·StepMs — not a pulse earlier or later — and the replay delivers every order at the same
    /// pulse and instant with the same verdict.</summary>
    [Fact]
    public void PlayerBattle_OrderDeliveryPin_AndReplay()
    {
        var s = new SandboxSession(SandboxMode.BattleTest, OpenField(), 21);
        s.AddForce(0, 3, 301, 40, West, out var mine);
        s.AddForce(1, 4, 402, 40, East, out var theirs);
        s.OpenDeployment();
        s.CloseDeployment();
        var rules = s.DeployedSetup!.Rules;
        long steps = rules.PulseMs / rules.StepMs;
        var player = new ScriptedController()
            .Add(0, new OrderRequest(mine[0], OrderKind.Attack, 0, theirs[0]))
            .Add(2, new OrderRequest(mine[0], OrderKind.Hold, 3))
            .Add(3, new OrderRequest(mine[0], OrderKind.Attack, (int)steps, theirs[0]));         // offset outside the pulse
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Player, SandboxCommand.Ai, player0: player));
        Assert.Equal(ResolverKind.Tactical, s.ResolverUsed);
        var log = s.BattleOrders.Where(o => o.Side == 0).ToArray();
        long t0 = s.DeployedSetup.StartMs;
        Assert.Equal((0L, t0, true), (log[0].Pulse, log[0].AtMs, log[0].Accepted));
        Assert.Equal((2L, t0 + 2 * rules.PulseMs + 3 * rules.StepMs), (log[1].Pulse, log[1].AtMs));
        Assert.Equal((3L, t0 + 3 * rules.PulseMs, OrderInvalid.BadStep), (log[2].Pulse, log[2].AtMs, log[2].Invalid));
        var r = SandboxSession.Replay(s);
        Assert.Equal(s.BattleOrders.Select(o => (o.Pulse, o.Side, o.AtMs, o.Invalid, o.Budget)).ToArray(),
                     r.BattleOrders.Select(o => (o.Pulse, o.Side, o.AtMs, o.Invalid, o.Budget)).ToArray());
        Assert.Equal(s.Result!.TraceHash, r.Result!.TraceHash);
        Assert.Equal(s.Result.StateHash, r.Result.StateHash);
        Assert.Equal(s.HashHex(), r.HashHex());
    }

    /// <summary>Replay of a whole session — terrain edits, refused edits, split forces, deployment edits (refused and accepted), an
    /// AI-vs-AI battle — reproduces the session hash; a different seed does not (the pin has teeth).</summary>
    [Fact]
    public void Replay_ReproducesAWholeSession_BitForBit()
    {
        SandboxSession Build(ulong seed)
        {
            var s = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), seed);
            s.EditTerrain(22, 16, TerrainClass.Forest);
            s.EditTerrain(23, 15, TerrainClass.Hills, 4_000);
            s.EditTerrain(-1, 0, TerrainClass.Forest);                    // refused, logged
            s.AddForce(0, 3, 301, 70, Cell(6, 10), out _);
            s.AddForce(0, 4, 402, 60, Cell(3, 20), out _);
            s.AddForce(1, 3, 301, 100, Cell(40, 10), out var e);
            s.OpenDeployment();
            s.Deploy(1, e[1], Cell(20, 20));                              // refused (middle third)
            s.Deploy(1, e[1], Cell(42, 22));
            s.CloseDeployment();
            s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai);
            return s;
        }
        var a = Build(4);
        var b = SandboxSession.Replay(a);
        Assert.Equal(a.HashHex(), b.HashHex());
        Assert.Equal(a.Log.ToArray(), b.Log.ToArray());
        Assert.NotEqual(a.HashHex(), Build(5).HashHex());
        // an encounter replays too
        var enc = SandboxSession.FromEncounter(new CampaignEncounterSnapshot(OpenField(), new[] { (0, 0, 3, 301, 400L, West), (1, 1, 4, 402, 300L, East) }), 8);
        enc.OpenDeployment(); enc.CloseDeployment(); enc.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai);
        Assert.Equal(enc.HashHex(), SandboxSession.Replay(enc).HashHex());
    }

    /// <summary>POPULATED tables: the session log and the casualty rows of a real session encode to their exact lengths, decode
    /// bit-exactly and re-encode to the same bytes. [verification round 1, D-R1-07] The session was an AutoResolver battle whose
    /// casualty rows had Wounded = Captured = 0 in every row, so an encoder swapping the two round-tripped (verifier mutant V12).
    /// The log is now the prisoners session's (terrain edits, forces, deployment, a player battle); the casualty table holds the
    /// rows of two real battles: the prisoners battle (TacticalResolver: wounded and prisoners) and an AutoResolver battle (routed
    /// men who returned and who deserted). Every pair of same-typed fields must be told apart by some row.</summary>
    [Fact]
    public void Codec_PopulatedTables_RoundTripBitExact()
    {
        var s = PrisonersSession();
        var casualties = s.Casualties.Concat(AutoBattleSession(11).Casualties).ToArray();
        Assert.True(s.Log.Count >= 5 && casualties.Length == 8);
        Assert.True(casualties.Sum(c => c.Wounded) > 0 && casualties.Sum(c => c.Captured) > 0 && casualties.Sum(c => c.Deserted) > 0);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(s.Log);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(casualties);
        byte[] e = SandboxCodec.EncodeEdits(s.Log);
        Assert.Equal(SandboxCodec.ExpectedLength(s.Log.Count, SandboxCodec.EditRowBytes), e.Length);
        var eb = SandboxCodec.DecodeEdits(e);
        Assert.Equal(s.Log.ToArray(), eb);
        Assert.Equal(SHA256.HashData(e), SHA256.HashData(SandboxCodec.EncodeEdits(eb)));
        byte[] c = SandboxCodec.EncodeCasualties(casualties);
        Assert.Equal(SandboxCodec.ExpectedLength(casualties.Length, SandboxCodec.CasualtyRowBytes), c.Length);
        var cb = SandboxCodec.DecodeCasualties(c);
        Assert.Equal(casualties, cb);
        Assert.Equal(SHA256.HashData(c), SHA256.HashData(SandboxCodec.EncodeCasualties(cb)));
        Assert.Equal((50, 68), (SandboxCodec.EditRowBytes, SandboxCodec.CasualtyRowBytes));
        Assert.Throws<FormatException>(() => SandboxCodec.DecodeEdits(new byte[] { 1, 2, 0, 0, 0 }));
    }

    /// <summary>
    /// [verification round 1, D-R1-10] DR30 "Do not allow campaign encounter terrain editing" holds beyond the API: the session's
    /// terrain is handed out only as a COPY (the <see cref="SandboxSession.Terrain"/> property and the deployed setup's grid), so a
    /// write to what the caller holds reaches neither the session nor its battle, and the replay reproduces the session. Before
    /// round 1 the property returned the live grid (verifier probe P3: the encounter's cell became Forest; the replay's stayed Open).
    /// </summary>
    [Fact]
    public void Terrain_IsHandedOutAsACopy_SoTheLockAndTheReplayHold()
    {
        var enc = SandboxSession.FromEncounter(new CampaignEncounterSnapshot(OpenField(), new[] { (0, 0, 3, 301, 40L, West), (1, 1, 3, 301, 40L, East) }), 1);
        Assert.Equal(SandboxRefusal.TerrainLocked, enc.EditTerrain(20, 16, TerrainClass.Forest));
        enc.Terrain.Set(20, 16, TerrainClass.Forest, 0);                       // a write to the grid the caller was handed
        Assert.Equal(TerrainClass.Open, enc.Terrain.ClassAt(20, 16));          // does not reach the session
        Assert.Equal(SandboxRefusal.None, enc.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, enc.CloseDeployment());
        enc.DeployedSetup!.Grid.Set(21, 16, TerrainClass.Wall, 0);             // nor the deployed battle setup
        enc.DeployedSetup!.Sides[0].Objectives[0] = Cell(0, 0);
        Assert.Equal(TerrainClass.Open, enc.DeployedSetup!.Grid.ClassAt(21, 16));
        Assert.Equal(TerrainClass.Open, enc.Terrain.ClassAt(21, 16));
        Assert.NotEqual(Cell(0, 0), enc.DeployedSetup!.Sides[0].Objectives[0]);
        Assert.Equal(SandboxRefusal.None, enc.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        var replay = SandboxSession.Replay(enc);
        Assert.Equal(enc.Result!.ResultHash(), replay.Result!.ResultHash());
        Assert.Equal(enc.HashHex(), replay.HashHex());
        // In an editable session too, the logged edit is the only way in.
        var ed = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 2);
        ed.Terrain.Set(5, 5, TerrainClass.Wall, 0);
        Assert.Equal(TerrainClass.Open, ed.Terrain.ClassAt(5, 5));
        Assert.Equal(SandboxRefusal.None, ed.EditTerrain(5, 5, TerrainClass.Wall));
        Assert.Equal(TerrainClass.Wall, ed.Terrain.ClassAt(5, 5));
        Assert.Equal(TerrainClass.Wall, ed.TerrainCopy().ClassAt(5, 5));
        // Not even through the deployment phase, which the session still handed out live in round 1 (a limitation recorded in the
        // sandbox proposal §7; since verification round 2, D-R2-06, it hands out a copy): the phase works on its own copy of the grid.
        Assert.Equal(SandboxRefusal.None, ed.AddForce(0, 3, 301, 10, West, out _));
        Assert.Equal(SandboxRefusal.None, ed.AddForce(1, 3, 301, 10, East, out _));
        Assert.Equal(SandboxRefusal.None, ed.OpenDeployment());
        Assert.True(ed.Deployment!.Close(out var phaseSetup).Accepted);
        phaseSetup!.Grid.Set(6, 6, TerrainClass.Wall, 0);
        Assert.Equal(TerrainClass.Open, ed.Terrain.ClassAt(6, 6));
    }

    /// <summary>
    /// [verification round 1, D-R1-09] The gun crew (lane N's test identity 4992, standing for the field artillery 805 it cites) is
    /// fielded WITH lane N's FieldGun: its profile has Ranged 0 because the gun is a domain weapon given on the formation. Before
    /// round 1 the sandbox gave none, so its gun crews could not fire (verifier probe P2: archers killed 0). The weapon array the
    /// caller sees is a copy (neither the session nor the catalogue can be changed through it). A battle with a domain weapon is
    /// outside the AutoResolver's model: AI-vs-AI it runs on the TacticalResolver (READING A, applied). Lane N's guns fire from a
    /// stationary crew: with the gun-crew side holding (a passive player side) the guns kill archers, and the same deployed battle
    /// with the weapons removed leaves the archers unharmed, which isolates the guns' effect. (Under the knowledge-only AI the
    /// crews advance and never fire; that AI limitation is recorded in the sandbox proposal, not pinned here.)
    /// </summary>
    [Fact]
    public void GunCrew_IsFieldedWithItsFieldGun_AndFires()
    {
        Assert.Equal(new[] { DomainProfiles.FieldGun }, SandboxCatalogue.Find(8, 4992)!.Weapons!.ToArray());
        SandboxSession Deployed(out long gunId)
        {
            var s = new SandboxSession(SandboxMode.BattleTest, OpenField(), 1);
            Assert.Equal(SandboxRefusal.None, s.AddForce(0, 8, 4992, 12, West, out var g));
            Assert.Equal(SandboxRefusal.None, s.AddForce(1, 4, 402, 60, East, out _));
            Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
            Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
            gunId = g[0];
            return s;
        }
        var held = Deployed(out long id);
        var gun = held.Formations.Single(f => f.Id == id);
        Assert.Equal(new[] { DomainProfiles.FieldGun }, gun.Weapons);
        gun.Weapons![0] = DomainProfiles.AaGun;                                   // writing to the copy
        Assert.Equal(DomainProfiles.FieldGun, held.Formations.Single(f => f.Id == id).Weapons![0]);
        Assert.Equal(DomainProfiles.FieldGun, SandboxCatalogue.Find(8, 4992)!.Weapons![0]);
        var deployed = held.DeployedSetup!;
        Assert.Equal(new[] { DomainProfiles.FieldGun }, deployed.Formations.Single(f => f.Id == id).Weapons);
        Assert.NotNull(ResolverPolicy.AutoUnsupportedReason(deployed));
        var ai = Deployed(out _);
        Assert.Equal(SandboxRefusal.None, ai.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        Assert.Equal(ResolverKind.Tactical, ai.ResolverUsed);                    // READING A (a): the fallback
        Assert.Equal(SandboxRefusal.None, held.StartBattle(SandboxCommand.Player, SandboxCommand.Ai, player0: PassiveController.Instance));
        Assert.Equal(ResolverKind.Tactical, held.ResolverUsed);
        long archersKilled = held.Result!.Formations.Where(r => r.Side == 1).Sum(r => r.Killed);
        Assert.True(archersKilled > 0, "the field guns fire");
        var unarmed = deployed with { Formations = deployed.Formations.Select(f => f with { Weapons = null }).ToArray() };
        var res = new TacticalBattle(unarmed, PassiveController.Instance, new KnowledgeAi()).Run();
        Assert.Equal(0, res.Formations.Where(r => r.Side == 1).Sum(r => r.Killed));
    }

    /// <summary>
    /// [verification round 1, D-R1-14] A force of modest size is laid out INSIDE the field: column by column from the anchor, 4 cells
    /// apart, rows wrapped inside the grid, further columns toward the side's own exit edge; it deploys through the thirds. A force
    /// that cannot fit is refused whole (OutOfGrid; nothing minted). Before round 1 only the first anchor was checked: a 1,000-man
    /// warband put 16 of its 20 formations off the 48 x 32 field and deployment was refused (verifier probe P6). Small forces keep
    /// their places (the first column is the old layout).
    /// </summary>
    [Fact]
    public void ForceCreation_LargeForce_IsLaidOutInsideTheField_AndDeploys()
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 1_000, West, out var west));           // West = cell (8, 16)
        Assert.Equal(20, west.Length);
        var anchors = west.Select(i => s.Formations.Single(f => f.Id == i).Anchor).ToArray();
        for (int k = 0; k < anchors.Length; k++)
            Assert.Equal(Cell(8 - 4 * (k / 8), (16 + 4 * (k % 8)) % 32), anchors[k]);                  // columns x = 8, 4, 0
        Assert.Equal(anchors.Length, anchors.Distinct().Count());
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 1_000, East, out var east));           // East = cell (39, 16)
        for (int k = 0; k < east.Length; k++)
            Assert.Equal(Cell(39 + 4 * (k / 8), (16 + 4 * (k % 8)) % 32), s.Formations.Single(f => f.Id == east[k]).Anchor);   // x = 39, 43, 47
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());                                          // every part in its own third
        Assert.Equal(40, s.DeployedSetup!.Formations.Length);
        // A force that cannot fit is refused whole: 1,250 heavy infantry = 25 parts need a fourth column (x = 4, 0, -4).
        var t = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        long mint = t.Ledger.Balance(t.MintAccount);
        Assert.Equal(SandboxRefusal.OutOfGrid, t.AddForce(0, 3, 301, 1_250, Cell(4, 16), out var none));
        Assert.Empty(none);
        Assert.Empty(t.Formations);
        Assert.Equal(mint, t.Ledger.Balance(t.MintAccount));
        Assert.Equal((byte)SandboxRefusal.OutOfGrid, t.Log[^1].Refusal);
        Assert.Equal(SandboxRefusal.None, t.AddForce(0, 3, 301, 1_200, Cell(8, 0), out var fits));      // 24 parts: x = 8, 4, 0
        Assert.Equal(24, fits.Length);
        // small forces: the first column is the pre-round-1 layout
        var u = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        Assert.Equal(SandboxRefusal.None, u.AddForce(0, 3, 301, 130, Cell(4, 2), out var hi));
        Assert.Equal(new[] { Cell(4, 2), Cell(4, 6), Cell(4, 10) }, hi.Select(i => u.Formations.Single(f => f.Id == i).Anchor).ToArray());
    }

    /// <summary>
    /// [verification round 2, D-R2-06] The inspection surface hands out COPIES of the ledger, the deployment phase and the battle
    /// result, so nothing reaches the session except its logged edits, and the replay reproduces it. Before round 2 all three were
    /// live: an unlogged DeploymentPhase.Propose was accepted and the battle was fought from it (verifier probe P_D: deployed anchor
    /// (50000, 90000) in the session, (170000, 330000) in the replay, hashes unequal); a transfer through the handed-out ledger, or a
    /// write into the result's formation array, changed the session as well. A proposal tried on the copy gets lane TR's real verdict
    /// (the copy is the session's phase, edit for edit), so it can serve as a what-if.
    /// </summary>
    [Fact]
    public void InspectionSurface_HandsOutCopies_OfTheLedgerTheDeploymentPhaseAndTheResult()
    {
        var s = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 9);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 40, West, out var w0));
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 40, East, out var e0));
        // the ledger: a transfer on the copy moves nobody in the session
        var ledger = s.Ledger;
        Assert.NotSame(ledger, s.Ledger);
        Assert.Equal(s.Ledger.Snapshot(), ledger.Snapshot());
        string h0 = s.HashHex();
        ledger.Transfer(s.MintAccount, ledger.AccountCount - 1, 100);
        Assert.Equal(40, s.Persons(e0[0]));
        Assert.Equal(SandboxSession.MintOpening - 80, s.Ledger.Balance(s.MintAccount));
        Assert.Equal(h0, s.HashHex());
        // the deployment phase: a proposal on the copy is judged by lane TR but is not the session's
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        string h1 = s.HashHex();
        var phase = s.Deployment!;
        Assert.True(phase.Propose(0, w0[0], Cell(2, 4)).Accepted);
        Assert.Equal(h1, s.HashHex());
        Assert.Empty(s.Deployment!.Log);
        Assert.Equal(West, s.Deployment!.Formations.Single(f => f.Id == w0[0]).Anchor);
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(West, s.DeployedSetup!.Formations.Single(f => f.Id == w0[0]).Anchor);
        // the result: a write into the copy's formation array changes nothing in the session
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        string h2 = s.HashHex();
        var result = s.Result!;
        long killed = result.Formations[0].Killed;
        result.Formations[0] = result.Formations[0] with { Killed = killed + 12_345 };
        Assert.Equal(killed, s.Result!.Formations[0].Killed);
        Assert.Equal(h2, s.HashHex());
        // the replay reproduces the session
        var replay = SandboxSession.Replay(s);
        Assert.Equal(s.HashHex(), replay.HashHex());
        Assert.Equal(s.Result!.ResultHash(), replay.Result!.ResultHash());
    }

    /// <summary>[verification round 2, D-R2-06] The deployment phase handed out is the session's phase edit for edit: the same log,
    /// with lane TR's verdicts, refused edits included, and the phase closed where the session closed it, so an edit after the close
    /// is refused PhaseClosed in the copy too (were the copy closed only at the end, that edit would be accepted).</summary>
    [Fact]
    public void Deployment_IsHandedOutAsTheSessionsPhase_EditForEdit()
    {
        var s = new SandboxSession(SandboxMode.ScenarioEditor, OpenField(), 3);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 30, West, out var w0));
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 30, East, out _));
        Assert.Null(s.Deployment);
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.DeploymentRefused, s.Deploy(0, w0[0], Cell(24, 16)));    // the middle third
        Assert.Equal(SandboxRefusal.None, s.Deploy(0, w0[0], Cell(6, 10)));
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.DeploymentRefused, s.Deploy(0, w0[0], Cell(4, 20)));    // after the close: lane TR's PhaseClosed
        var copy = s.Deployment!;
        Assert.True(copy.IsClosed);
        var deployRows = s.Log.Where(r => r.Kind == (byte)SandboxEditKind.Deploy).ToArray();
        Assert.Equal(deployRows.Length, copy.Log.Count);
        for (int i = 0; i < deployRows.Length; i++)
        {
            Assert.Equal(new Vec2L(deployRows[i].B, deployRows[i].C), copy.Log[i].Anchor);
            Assert.Equal(deployRows[i].A, (long)copy.Log[i].Verdict.Refusal);           // the session logs lane TR's code in A
        }
        Assert.Equal(DeploymentRefusal.PhaseClosed, copy.Log[^1].Verdict.Refusal);
        Assert.Equal(Cell(6, 10), copy.Formations.Single(f => f.Id == w0[0]).Anchor);
        Assert.Equal(Cell(6, 10), s.DeployedSetup!.Formations.Single(f => f.Id == w0[0]).Anchor);
    }
}
