using Proto.M7.Caps;
using Proto.M7.Sandbox;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.Tests.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.LaneCB;

/// <summary>
/// Lane CB: D-082 DA5 ("Battlefield units"; READING DA5-R (a), applied) as ONE rule for lanes W and D: a DR24 cap bounds the battle
/// entities a formation fights as (DirectorCaps.EntitiesOf); a formation's size in persons is limited by the economy, not by the cap;
/// machine classes keep one entity per machine (lane D's crewed tube; lane N's hulls and airframes); uncapped classes keep one entity
/// per person (lane N's CAP-5 treatment). docs/m7-plan/battle-entity-proposal.md. Every persons-per-machine value is a labelled TEST
/// INPUT (CAP-3 is a TUNE rule of M7.6; no value is shipped).
/// </summary>
public class BattleEntityRuleTests
{
    private const long TankCrew = 4, MortarCrew = 4, HullCrew = 200, HeliCrew = 7, PlaneCrew = 20;   // TEST INPUTS
    private static readonly Vec2L West = Cell(8, 16), East = Cell(39, 16);

    private static DirectorCaps Crewed() => DirectorCaps.Values
        .WithCrew(CapFamily.Tanks, TankCrew).WithCrew(CapFamily.Mortars, MortarCrew).WithCrew(CapFamily.Ships, HullCrew)
        .WithCrew(CapFamily.Helicopters, HeliCrew).WithCrew(CapFamily.FixedWing, PlaneCrew);

    // ------------------------------------------------------------------ the rule itself

    /// <summary>THE ONE RULE, class by class, at its boundaries: a soldier class fights as min(persons, cap) entities (one person per
    /// entity up to the cap; above it the cap's entities share the persons), never above the cap whatever its persons (10¹² included);
    /// a machine class as one entity per machine, ceil(persons ÷ crew), which can pass the cap (the check refuses that); without a crew
    /// rule a machine class has no entity count (it throws; the check refuses NoCrewRule); an uncapped class as one entity per person.
    /// CountOf is EntitiesOf (the counted objects are the battle entities).</summary>
    [Fact]
    public void EntitiesOf_SoldierMinOfPersonsAndCap_MachineOnePerMachine_UncappedOnePerPerson()
    {
        var c = Crewed();
        foreach (var (cls, cap) in new[] { (CapFamily.HeavyInfantry, 50L), (CapFamily.ArchersLightInfantry, 60L), (CapFamily.Snipers, 2L) })
            foreach (long p in new[] { 0, 1, cap - 1, cap, cap + 1, 2 * cap, 1_000_000_000_000 })
            {
                Assert.Equal(Math.Min(p, cap), c.EntitiesOf(cls, p));
                Assert.Equal(c.EntitiesOf(cls, p), c.CountOf(cls, p));
                if (p > 0) Assert.True(c.Check(1, cls, p).Accepted);
            }
        // machine classes: one entity per machine (a partly crewed machine is a machine)
        Assert.Equal(new long[] { 1, 1, 2, 5, 6 }, new long[] { 1, 4, 5, 20, 21 }.Select(p => c.EntitiesOf(CapFamily.Tanks, p)).ToArray());
        Assert.Equal((CapRefusal.ExceedsCap, 6L, 5L), (c.Check(1, CapFamily.Tanks, 21).Refusal, c.Check(1, CapFamily.Tanks, 21).Counted, c.Check(1, CapFamily.Tanks, 21).Cap));
        Assert.Equal(5, c.EntitiesOf(CapFamily.Mortars, 5 * MortarCrew));                 // lane D §2.1: one entity per crewed tube
        Assert.Equal(CapRefusal.ExceedsCap, c.Check(1, CapFamily.Mortars, 5 * MortarCrew + 1).Refusal);
        Assert.Equal((5L, 6L), (c.EntitiesOf(CapFamily.Ships, 1_000), c.EntitiesOf(CapFamily.Ships, 1_001)));
        Assert.Equal((1L, 2L), (c.EntitiesOf(CapFamily.FixedWing, 20), c.EntitiesOf(CapFamily.FixedWing, 21)));
        Assert.Throws<InvalidOperationException>(() => DirectorCaps.Values.EntitiesOf(CapFamily.Tanks, 4));
        Assert.Equal(CapRefusal.NoCrewRule, DirectorCaps.Values.Check(1, CapFamily.Tanks, 4).Refusal);
        // uncapped (CAP-5): one entity per person, never refused for size
        Assert.Equal(1_000_000_000_000, c.EntitiesOf(CapFamily.Uncapped, 1_000_000_000_000));
        Assert.True(c.Check(1, CapFamily.Uncapped, 1_000_000_000_000).Accepted);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.EntitiesOf(CapFamily.HeavyInfantry, -1));
        // what a cap still bounds in persons: a machine class (cap × crew); a soldier class only its full complement of entities
        Assert.Equal((5 * TankCrew, 5 * TankCrew), (c.MaxPersons(CapFamily.Tanks), c.FullStrengthPersons(CapFamily.Tanks)));
        Assert.Equal((long.MaxValue, 50L), (c.MaxPersons(CapFamily.HeavyInfantry), c.FullStrengthPersons(CapFamily.HeavyInfantry)));
        Assert.Equal((long.MaxValue, long.MaxValue), (c.MaxPersons(CapFamily.Uncapped), c.FullStrengthPersons(CapFamily.Uncapped)));
        Assert.Equal((-1L, -1L), (DirectorCaps.Values.MaxPersons(CapFamily.Mortars), DirectorCaps.Values.FullStrengthPersons(CapFamily.Mortars)));
    }

    /// <summary>The roster under DA5: a soldier formation grows past its cap IN PERSONS by every edit that adds people (create,
    /// reinforce, transfer, merge, split) and is never refused for size; it stays at its cap IN ENTITIES (every verdict counts
    /// min(persons, cap)); people are conserved exactly after every edit (depot + formations = opening). A machine formation is still
    /// refused one person past cap × crew (one entity per machine).</summary>
    [Fact]
    public void Roster_ASoldierFormationGrowsPastItsCapInPersons_ByEveryEdit_AndStaysAtItsCapInEntities()
    {
        var r = new FormationRoster(Crewed(), new long[] { 1_000_000 });
        void Conserved() => Assert.Equal(r.OpeningPersons, (Int128)r.DepotOf(0) + r.Formations.Sum(f => f.Persons));
        var v = r.Create(0, 1, CapFamily.HeavyInfantry, 301, 50);
        Assert.Equal((CapRefusal.None, 50L), (v.Refusal, v.Counted)); Conserved();
        v = r.Reinforce(0, 1, 950);
        Assert.Equal((CapRefusal.None, 50L), (v.Refusal, v.Counted)); Conserved();
        Assert.Equal(1_000, r.Find(1)!.Persons);
        v = r.Split(0, 1, 2, 600);
        Assert.Equal(CapRefusal.None, v.Refusal); Conserved();
        Assert.Equal((400L, 600L), (r.Find(1)!.Persons, r.Find(2)!.Persons));
        v = r.Transfer(0, 2, 1, 300);
        Assert.Equal((CapRefusal.None, 50L), (v.Refusal, v.Counted)); Conserved();
        Assert.Equal((700L, 300L), (r.Find(1)!.Persons, r.Find(2)!.Persons));
        v = r.Create(0, 3, CapFamily.HeavyInfantry, 301, 30);
        v = r.Merge(0, 1, 3);
        Assert.Equal((CapRefusal.None, 50L), (v.Refusal, v.Counted)); Conserved();
        Assert.Equal(730, r.Find(1)!.Persons);
        v = r.Detach(0, 1, 729);
        Assert.Equal((CapRefusal.None, 1L), (v.Refusal, v.Counted)); Conserved();   // one person left: one entity
        // a machine formation: one entity per machine, so one person past cap × crew is refused (and changes nothing)
        Assert.True(r.Create(0, 10, CapFamily.Tanks, 605, 5 * TankCrew).Accepted);
        long depot = r.DepotOf(0);
        v = r.Reinforce(0, 10, 1);
        Assert.Equal((CapRefusal.ExceedsCap, 6L), (v.Refusal, v.Counted));
        Assert.Equal((5 * TankCrew, depot), (r.Find(10)!.Persons, r.DepotOf(0)));
        Conserved();
    }

    // ------------------------------------------------------------------ the sandbox: lane D's force creation = lane W's encounter

    /// <summary>The War Sandbox fields only soldier classes and uncapped classes (no machine family has a battle profile yet), so the
    /// one rule needs no crew value there; <see cref="SandboxSession.Fielded"/> refuses a machine-counted family as a programming
    /// error rather than guessing a crew. Every catalogue family's entity count is the one rule's.</summary>
    [Fact]
    public void SandboxCatalogue_HoldsNoMachineCountedClass_AndFieldedRefusesOne()
    {
        foreach (var f in SandboxCatalogue.All)
        {
            Assert.False(DirectorCaps.IsMachineCounted(f.CapClass), f.Name);
            foreach (long p in new long[] { 1, 2, 49, 50, 51, 60, 61, 999 })
                Assert.Equal(DirectorCaps.Values.EntitiesOf(f.CapClass, p), SandboxSession.Fielded(f, p));
        }
        var tank = SandboxCatalogue.Find(3, 301)! with { CapClass = CapFamily.Tanks };
        Assert.Throws<InvalidOperationException>(() => SandboxSession.Fielded(tank, 20));
    }

    /// <summary>ONE RULE (lanes W and D): for every family the sandbox fields and every size around its cap, the sandbox's own force
    /// creation (lane D) and a Campaign Encounter (lane W's campaign formations) create the SAME formation: the same entity count, the
    /// same persons behind it (spec and ledger), the same profile, anchor and weapons.</summary>
    [Fact]
    public void ForceCreationAndCampaignEncounter_FieldTheSameFormation_ForEveryFamilyAndSize()
    {
        foreach (var f in SandboxCatalogue.All)
        {
            long cap = DirectorCaps.CapOf(f.CapClass);
            long[] sizes = f.CapClass == CapFamily.Uncapped ? new long[] { 1, 50, 999 } : new[] { 1, cap - 1, cap, cap + 1, 2_500 };
            foreach (long p in sizes.Where(x => x > 0).Distinct())
            {
                var quick = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
                Assert.Equal(SandboxRefusal.None, quick.AddForce(0, f.FamilyKey, f.IdentityKey, p, West, out var ids));
                var enc = SandboxSession.FromEncounter(new CampaignEncounterSnapshot(OpenField(), new[] { (0, 0, f.FamilyKey, f.IdentityKey, p, West) }), 1);
                var a = quick.Formations.Single(); var b = enc.Formations.Single();
                Assert.Equal(ids[0], a.Id);
                Assert.Equal((a.Id, a.Side, a.Count, a.Persons, a.Anchor, a.Profile), (b.Id, b.Side, b.Count, b.Persons, b.Anchor, b.Profile));
                Assert.Equal(a.Weapons ?? Array.Empty<WeaponSpec>(), b.Weapons ?? Array.Empty<WeaponSpec>());
                Assert.Equal((long)a.Count, DirectorCaps.Values.EntitiesOf(f.CapClass, p));
                Assert.Equal((p, p, (long?)p), (quick.Persons(a.Id), enc.Persons(b.Id), a.Persons));
            }
        }
    }

    /// <summary>THE REMAINDER RULE (Law 1), end to end in the sandbox: a formation of P persons fights as n entities, each standing
    /// for the share P ÷ n, and its persons are integerized once, at the collapse, by the largest-remainder method (lane W's
    /// BattleCollapse.Apportion, ties to the lowest category): every casualty row sums to its persons exactly, each category is within
    /// one person of its quota P × (entities of the category) ÷ n, the sandbox ledger is conserved, and the persons in each formation
    /// after the battle are its fit and returned men. 1,013 against 997 heavy infantry (shares 20.26 and 19.94: remainders
    /// everywhere), AutoResolver, three seeds. [CB, verification round 1, CB-R1-07] Each category also equals an independent
    /// largest-remainder oracle's share exactly: before round 1 only the within-one bound was asserted, which every rounding meets, so
    /// the method was pinned by lane W's CollapseTests alone (verifier mutant V13, every leftover person to Fit, passed these cases).
    /// In every seed the 1,013-person formation's one leftover person goes to Dead (remainder 34 of 50 against 16), not to Fit.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void TheCollapse_IntegerizesTheShareOnce_ByTheLargestRemainder_Exactly(ulong seed)
    {
        var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), seed);
        Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 1_013, West, out _));
        Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 997, East, out _));
        Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
        Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
        Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
        Assert.Equal((Int128)SandboxSession.MintOpening, s.Ledger.Total(Commodity.People));
        long max = s.DeployedSetup!.Rules.MoraleMax;
        bool remainder = false, leftoverPastFit = false;
        foreach (var c in s.Casualties)
        {
            var r = s.Result!.Formations.Single(x => x.FormationId == c.FormationId);
            Assert.Equal(c.Persons, c.Fit + c.Wounded + c.RoutedReturned + c.Deserted + c.Dead + c.Captured);
            Assert.Equal(c.Fit + c.RoutedReturned, s.Persons(c.FormationId));
            long n = r.Start;
            Assert.Equal(50, n);
            // quotas of the AutoResolver's end categories (no wounded in its model): fit, routed, dead, surrendered
            (long Count, long Got)[] cats =
            {
                (r.Survivors - r.RoutedEscaped, c.Fit), (r.RoutedEscaped, c.RoutedReturned + c.Deserted), (r.Killed, c.Dead), (r.Surrendered, c.Captured),
            };
            foreach (var (count, got) in cats)
            {
                Int128 q = (Int128)c.Persons * count;
                Assert.True(got == (long)(q / n) || got == (long)(q / n) + 1, $"{got} not within one of {c.Persons} × {count} ÷ {n}");
                if (q % n != 0) remainder = true;
            }
            // [CB, verification round 1, CB-R1-07] "within one of its quota" holds for any rounding that hands each leftover person to
            // some category (verifier mutant V13, every leftover to Fit, survived it: these battles leave one leftover person per
            // formation). The largest-remainder method itself is pinned here, against an independent oracle, category by category.
            long[] counts = { r.Survivors - r.RoutedEscaped, 0, r.RoutedEscaped, r.Killed, r.Surrendered };
            var expected = LargestRemainder(c.Persons, counts);
            Assert.Equal(expected, new[] { c.Fit, c.Wounded, c.RoutedReturned + c.Deserted, c.Dead, c.Captured });
            for (int k = 1; k < counts.Length; k++)
                if (expected[k] > (long)((Int128)c.Persons * counts[k] / n)) leftoverPastFit = true;
            bool held = s.Result.HoldsField == c.Side;
            long routed = c.RoutedReturned + c.Deserted;
            Assert.Equal(held ? 0 : (long)((Int128)routed * (max - r.MoraleEnd) / max), c.Deserted);
        }
        Assert.True(remainder, "the pin has teeth: some category's quota is not a whole number");
        Assert.True(leftoverPastFit, "the pin has teeth: some leftover person goes to a category other than the first (Fit)");
    }

    /// <summary>[CB, verification round 1, CB-R1-07] An independent largest-remainder (Hamilton) oracle: floor(P × w_c ÷ n) per
    /// category, then the leftover persons one each to the categories ordered by remainder, largest first, equal remainders to the
    /// lowest category index (lane W's rule). Written with a sort, not with BattleCollapse.Apportion's repeated scan.</summary>
    private static long[] LargestRemainder(long persons, long[] weights)
    {
        Int128 n = 0;
        foreach (long w in weights) n += w;
        var share = new long[weights.Length];
        var rem = new Int128[weights.Length];
        long given = 0;
        for (int k = 0; k < weights.Length; k++)
        {
            Int128 x = (Int128)persons * weights[k];
            share[k] = (long)(x / n);
            rem[k] = x % n;
            given += share[k];
        }
        int[] order = Enumerable.Range(0, weights.Length).OrderByDescending(k => rem[k]).ThenBy(k => k).ToArray();
        for (long i = 0; i < persons - given; i++) share[order[i]]++;
        return share;
    }

    /// <summary>PERFORMANCE AT LARGE P and Law 1 at the extreme: two formations of 10⁹ persons each fight as 50 against 50 entities
    /// (the battle is the 50-against-50 battle: equal result hash) and the collapse moves exactly 2 × 10⁷ persons per entity of every
    /// category, in Int128 arithmetic, with the sandbox ledger conserved. The battle's cost does not depend on P (MEASURED in the
    /// bench, reports/H-CB.md §5); this pins that the persons cannot leak into it.</summary>
    [Fact]
    public void ABillionPersonsPerSide_FightAsFiftyEntities_AndCollapseExactly()
    {
        SandboxSession Fight(long persons)
        {
            var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 5);
            Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, persons, West, out _));
            Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, persons, East, out _));
            Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
            Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
            Assert.Equal(SandboxRefusal.None, s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
            return s;
        }
        var big = Fight(1_000_000_000);
        var small = Fight(50);
        Assert.Equal(small.Result!.ResultHash(), big.Result!.ResultHash());
        Assert.Equal((Int128)SandboxSession.MintOpening, big.Ledger.Total(Commodity.People));
        for (int i = 0; i < 2; i++)
        {
            var b = big.Casualties[i]; var m = small.Casualties[i];
            const long k = 20_000_000;
            Assert.Equal((k * m.Fit, k * m.Wounded, k * m.Dead, k * m.Captured, k * (m.RoutedReturned + m.Deserted)),
                         (b.Fit, b.Wounded, b.Dead, b.Captured, b.RoutedReturned + b.Deserted));
        }
        Assert.True(big.DeadTotal > 0);
    }

    // ------------------------------------------------------------------ the battle: persons behind entities

    /// <summary>Both resolvers refuse a formation whose persons cannot stand behind its entities (fewer persons than entities), and
    /// Persons on a vessel formation (its crew is lane N's personnel ledger); Persons equal to the count, or null, is the identity
    /// (the same result as a battle without it). [CB, verification round 1, CB-R1-06] And Persons on an AIRCRAFT formation (its crew
    /// is in the personnel ledger too): only the vessel half of the contract was tested, so a check that refused vessels alone
    /// (verifier mutant V6) passed the whole suite. Each refusal is matched by its message, so an unrelated ArgumentException cannot
    /// stand in for it; the same battles without Persons construct.</summary>
    [Fact]
    public void BothResolvers_RefusePersonsThatCannotStandBehindTheEntities_AndNullOrEqualIsTheIdentity()
    {
        var setup = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 3);
        BattleSetup With(long? persons) => setup with { Formations = new[] { setup.Formations[0] with { Persons = persons }, setup.Formations[1] } };
        var ai = new KnowledgeAi();
        Assert.Throws<ArgumentException>(() => new TacticalBattle(With(19), ai, ai));
        Assert.Throws<ArgumentException>(() => new AutoBattle(With(19), new AutoCoefficients()));
        var plain = TT.RunGuarded(new TacticalBattle(setup, ai, ai));
        Assert.Equal(plain.ResultHash(), TT.RunGuarded(new TacticalBattle(With(20), ai, ai)).ResultHash());
        Assert.Equal(new AutoBattle(setup, new AutoCoefficients()).Run().ResultHash(), new AutoBattle(With(20), new AutoCoefficients()).Run().ResultHash());
        var naval = MultiDomainScenarios.LineBattle(DomainProfiles.Galley, 50_000, 1);
        var fleet = naval.Formations[0];
        Assert.Equal(Domain.Naval, fleet.Domain);
        var bad = naval with { Formations = naval.Formations.Select(f => f.Id == fleet.Id ? f with { Persons = 1_000 } : f).ToArray() };
        var ex = Assert.Throws<ArgumentException>(() => new TacticalBattle(bad, ai, ai));
        Assert.Contains($"formation {fleet.Id}: Persons is for land formations", ex.Message);
        _ = new TacticalBattle(naval, ai, ai);                                              // the same battle without Persons
        // [CB, round 1, CB-R1-06] an aircraft formation (lane N's strike aircraft of the amphibious scenario)
        var amphibious = MultiDomainScenarios.Amphibious(1);
        var flight = amphibious.Formations.Single(f => f.Id == 13);
        Assert.Equal(Domain.Air, flight.Domain);
        _ = new TacticalBattle(amphibious, ai, ai);                                         // constructs without Persons
        var badAir = amphibious with { Formations = amphibious.Formations.Select(f => f.Id == flight.Id ? f with { Persons = 2 } : f).ToArray() };
        ex = Assert.Throws<ArgumentException>(() => new TacticalBattle(badAir, ai, ai));
        Assert.Contains($"formation {flight.Id}: Persons is for land formations", ex.Message);
    }

    /// <summary>Lane MOR's in-battle scenario (A, 50 heavy infantry at 900, breaks G, 10 heavy infantry; B never fights G): the
    /// formations, their entities and the battle are lane MOR's; only the persons their entities stand for change.</summary>
    private static BattleSetup Scenario(ulong seed, long rise, long? personsA, long? personsG)
    {
        Vec2L a = Cell(14, 16), b = Cell(4, 16), g = Cell(34, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { a }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, a) { Morale = 900, Persons = personsA },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, g) { Persons = personsG },
                new FormationSpec(3, 0, UnitProfiles.HeavyInfantry, 10, b) { Posture = Posture.Defend, Morale = 800 },
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    /// <summary>
    /// THE ENTITY'S MORALE WEIGHT (docs/m7-plan/battle-entity-proposal.md §4): lane MOR's "fight it won" rise is
    /// floor(c × B ÷ (B + W)) over the two formations' SIZES, which both resolvers now read as the persons their entities stand for
    /// (EntityShare.PersonsOf). With c = 200 (lane MOR's PROPOSAL value, a TEST INPUT here):
    /// - persons null (one per entity): B = 10, W = 50, rise floor(2,000 ÷ 60) = 33, lane MOR's number;
    /// - G's 10 entities stand for 40 persons, A's 50 for 50: floor(8,000 ÷ 90) = 88 (entity counts would give 33);
    /// - A's 50 entities stand for 5,000 persons, G's 10 for 10: floor(2,000 ÷ 5,010) = 0, no rise and no row;
    /// - [CB, verification round 1, CB-R1-05] A's 50 entities stand for 60 persons, G's 10 for 10: floor(2,000 ÷ 70) = 28 (entity
    ///   counts would give 33).
    /// [CB, round 1, CB-R1-05] The 88 case is a RESOLVER-LEVEL TEST INPUT that the one rule never produces: a heavy-infantry formation of
    /// 40 persons fights as min(40, 50) = 40 entities, not 10 (persons exceed entities only above the cap), and the resolvers accept the
    /// pairing because EntityShare.Validate checks P ≥ n and the land domain only, not n = EntitiesOf(class, P). It pins the weighing
    /// itself. The configurations the rule produces here are 33 (persons within the cap), 28 (A's 60 persons above its cap of 50) and
    /// 0 (A's 5,000).
    /// In each case and in each resolver the battle is otherwise the rule-off battle (the persons change nothing else: the entities do
    /// not yet scale by their share, §3), A's end morale differs from it by exactly the rise, and the rise row stands at the end
    /// instant. (The TacticalResolver's A ends this battle at its start, 900, so the room under MoraleMax is 100: MEASURED with a
    /// temporary probe, seeds 1-3; the AutoResolver's A ends at 740, 660 and 780. The persons were chosen so no rise is capped.)
    /// </summary>
    [Theory]
    [InlineData(1UL, null, null, 33L)]
    [InlineData(1UL, 50L, 40L, 88L)]
    [InlineData(1UL, 5_000L, 10L, 0L)]
    [InlineData(2UL, 50L, 40L, 88L)]
    [InlineData(3UL, 5_000L, 10L, 0L)]
    [InlineData(1UL, 60L, null, 28L)]
    [InlineData(3UL, 60L, null, 28L)]
    public void TheFightItWon_WeighsTheFormationsByTheirPersons_InBothResolvers(ulong seed, long? personsA, long? personsG, long expected)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var offT = TT.RunGuarded(new TacticalBattle(Scenario(seed, 0, personsA, personsG), ai, ai));
        var onB = new TacticalBattle(Scenario(seed, 200, personsA, personsG), ai, ai);
        var onT = TT.RunGuarded(onB);
        var c = new AutoCoefficients { DoctrineRetreatMorale = 0 };
        var offA = new AutoBattle(Scenario(seed, 0, personsA, personsG), c).Run();
        var onAB = new AutoBattle(Scenario(seed, 200, personsA, personsG), c);
        var onA = onAB.Run();
        foreach (var (off, on, rises) in new[] { (offT, onT, onB.LocalWinRises), (offA, onA, onAB.LocalWinRises) })
        {
            Assert.Equal(BattleEndReason.SideEliminated, on.Reason);
            Assert.Equal(expected == 0 ? Array.Empty<LocalWinRise>() : new[] { new LocalWinRise(on.EndMs, 1, 2, expected) }, rises.ToArray());
            Assert.True(off.Formations.Single(f => f.FormationId == 1).MoraleEnd + expected <= 1_000, "the rise is not hidden by the cap");
            Assert.Equal((off.Reason, off.Side, off.HoldsField, off.Pulses, off.EndMs), (on.Reason, on.Side, on.HoldsField, on.Pulses, on.EndMs));
            for (int i = 0; i < off.Formations.Length; i++)
            {
                var x = off.Formations[i]; var y = on.Formations[i];
                Assert.Equal(x with { MoraleEnd = x.MoraleEnd + (y.FormationId == 1 ? expected : 0) }, y);
            }
        }
        // the persons change nothing else in either resolver: the rule-off battle is the battle without persons
        Assert.Equal(TT.RunGuarded(new TacticalBattle(Scenario(seed, 0, null, null), ai, ai)).ResultHash(), offT.ResultHash());
        Assert.Equal(new AutoBattle(Scenario(seed, 0, null, null), c).Run().ResultHash(), offA.ResultHash());
    }

    // ------------------------------------------------------------------ verification round 1

    /// <summary>
    /// [CB, verification round 1, CB-R1-03] THE SANDBOX'S PERSONS REACH ITS BATTLES, in both resolvers. The sandbox is the one fielding
    /// path that creates formations whose persons exceed their entities, and its battles run on the setup lane TR's deployment phase
    /// rebuilds; no test followed the persons through that rebuild (verifier mutant V1, the rebuild dropping Persons, passed the whole
    /// suite), so a sandbox session with lane MOR's coefficient set would have weighed the "fight it won" rise by entities unseen.
    /// A heavy-infantry force of P_A persons (50 entities for P_A ≥ 50) breaks one of 10 (10 entities), lane MOR's c = 200 (a TEST
    /// INPUT) against c = 0, sessions otherwise identical: the deployed setup carries each formation's persons, and A's end morale
    /// rises by floor(200 × 10 ÷ (10 + P_A)): 33 at P_A = 50, 28 at 60, 0 at 5,000 (by entities it would be 33 every time). Both
    /// sides AI take the AutoResolver (lane P's policy); side 0 commanded through the player path (a knowledge-only AI as its
    /// controller) takes the TacticalResolver. The battle is otherwise the c = 0 battle. Seeds and sizes MEASURED with a temporary
    /// probe so no rise is hidden by MoraleMax (the TacticalResolver's A ends at MoraleMax in seeds 1 and 2, so its branch uses seed 3).
    /// </summary>
    [Theory]
    [InlineData(false, 1UL)]
    [InlineData(false, 3UL)]
    [InlineData(true, 3UL)]
    public void SandboxPersons_ReachTheBattle_AndTheFightItWonWeighsThem_InBothResolvers(bool tactical, ulong seed)
    {
        SandboxSession Fight(long personsA, long rise)
        {
            var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), seed, new TacticalRules { LocalWinMoraleRise = rise });
            Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, personsA, West, out var a));
            Assert.Equal(SandboxRefusal.None, s.AddForce(1, 3, 301, 10, East, out var g));
            Assert.Equal(SandboxRefusal.None, s.OpenDeployment());
            Assert.Equal(SandboxRefusal.None, s.CloseDeployment());
            var deployed = s.DeployedSetup!.Formations;
            Assert.Equal((50, (long?)personsA), (deployed.Single(f => f.Id == a[0]).Count, deployed.Single(f => f.Id == a[0]).Persons));
            Assert.Equal((10, (long?)10), (deployed.Single(f => f.Id == g[0]).Count, deployed.Single(f => f.Id == g[0]).Persons));
            Assert.Equal(SandboxRefusal.None, tactical ? s.StartBattle(SandboxCommand.Player, SandboxCommand.Ai, new KnowledgeAi())
                                                       : s.StartBattle(SandboxCommand.Ai, SandboxCommand.Ai));
            Assert.Equal(tactical ? ResolverKind.Tactical : ResolverKind.Auto, s.ResolverUsed);
            return s;
        }
        foreach (var (personsA, expected) in new[] { (50L, 33L), (60L, 28L), (5_000L, 0L) })
        {
            var off = Fight(personsA, 0).Result!;
            var on = Fight(personsA, 200).Result!;
            Assert.Equal((BattleEndReason.SideEliminated, 1), (on.Reason, on.Side));
            Assert.Equal((off.Reason, off.Side, off.HoldsField, off.Pulses, off.EndMs), (on.Reason, on.Side, on.HoldsField, on.Pulses, on.EndMs));
            for (int i = 0; i < off.Formations.Length; i++)
            {
                var x = off.Formations[i]; var y = on.Formations[i];
                Assert.Equal(x with { MoraleEnd = x.MoraleEnd + (y.FormationId == 1 ? expected : 0) }, y);
            }
            Assert.True(off.Formations.Single(f => f.FormationId == 1).MoraleEnd + 33 <= 1_000, "the entity-weighted rise would not be hidden by the cap");
        }
    }

    /// <summary>
    /// [CB, verification round 1, CB-R1-02] A FORCE THE MINT CANNOT COVER IS REFUSED, LOGGED, AND CHANGES NOTHING ELSE. Under D-082 DA5
    /// a force of any size is one formation, so a force above the mint's free balance reached the mint transfer, which threw after the
    /// formation's id and ledger account were taken and before the edit was logged (verifier probe, reproduced: the session's hash
    /// differed from a fresh session's, the next force got id 2, and the replay did not reproduce the session). Now AddForce refuses it
    /// (MintShort) before anything is minted: one logged row, the ledger's accounts and balances as before, the next force gets id 1,
    /// and the replay reproduces the session. The boundary is exact: a force of exactly the mint's balance is accepted, one more person
    /// is refused. The uncapped gun crews (one entity per person) take the same check.
    /// </summary>
    [Fact]
    public void AddForce_AForceTheMintCannotCover_IsRefusedMintShort_LoggedAndChangingNothingElse()
    {
        static (long[] Balances, int Accounts) LedgerOf(SandboxSession x)
        {
            var l = x.Ledger;
            var b = new long[l.AccountCount];
            for (int i = 0; i < b.Length; i++) b[i] = l.Balance(i);
            return (b, l.AccountCount);
        }
        var fresh = LedgerOf(new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1));
        foreach (var (family, identity) in new[] { (3, 301), (8, 4992) })
        {
            var s = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
            Assert.Equal(SandboxRefusal.MintShort, s.AddForce(0, family, identity, SandboxSession.MintOpening + 1, West, out var none));
            Assert.Empty(none);
            Assert.Equal((1, (byte)SandboxRefusal.MintShort, -1L), (s.Log.Count, s.Log[0].Refusal, s.Log[0].FormationId));
            Assert.Empty(s.Formations);
            var now = LedgerOf(s);
            Assert.Equal(fresh.Accounts, now.Accounts);                                       // no ledger account was opened
            Assert.Equal(fresh.Balances, now.Balances);                                       // nobody was minted
            Assert.Equal(SandboxRefusal.None, s.AddForce(0, 3, 301, 50, West, out var ids));
            Assert.Equal(1, ids[0]);                                                          // no formation id was used up
            Assert.Equal(s.HashHex(), SandboxSession.Replay(s).HashHex());
        }
        // two forces: the second exceeds what the first left in the mint; then the exact boundary
        var t = new SandboxSession(SandboxMode.QuickBattle, OpenField(), 1);
        Assert.Equal(SandboxRefusal.None, t.AddForce(0, 3, 301, 600_000_000_000, West, out _));
        Assert.Equal(SandboxRefusal.MintShort, t.AddForce(1, 3, 301, 600_000_000_000, East, out _));
        long left = t.Ledger.Balance(t.MintAccount);
        Assert.Equal(SandboxSession.MintOpening - 600_000_000_000, left);
        Assert.Equal(SandboxRefusal.None, t.AddForce(1, 3, 301, left, East, out var exact));
        Assert.Equal(2, exact[0]);
        Assert.Equal(0, t.Ledger.Balance(t.MintAccount));
        Assert.Equal(SandboxRefusal.MintShort, t.AddForce(1, 3, 301, 1, East, out _));
        Assert.Equal(2, t.Formations.Count);
        Assert.Equal((Int128)SandboxSession.MintOpening, t.Ledger.Total(Commodity.People));
        Assert.Equal(t.HashHex(), SandboxSession.Replay(t).HashHex());
    }
}
