using Proto.M7.Caps;
using Proto.M7.Rng;
using Proto.M7.Tactical;

namespace Proto.M7.Tests.LaneD;

/// <summary>
/// Lane D item 1: the DR24 cap check at formation creation and edit (Proto.M7/Caps). Persons-per-machine values used here are
/// labelled TEST INPUTS (CAP-3 is a TUNE rule of M7.6; no value is shipped).
/// </summary>
public class CapsRosterTests
{
    // TEST INPUTS (not shipped values): persons per counted machine.
    private const long TankCrew = 4, MortarCrew = 4, HullCrew = 200, HeliCrew = 7, PlaneCrew = 20;

    private static DirectorCaps Caps() => DirectorCaps.Values
        .WithCrew(CapFamily.Tanks, TankCrew).WithCrew(CapFamily.Mortars, MortarCrew).WithCrew(CapFamily.Ships, HullCrew)
        .WithCrew(CapFamily.Helicopters, HeliCrew).WithCrew(CapFamily.FixedWing, PlaneCrew);

    private static FormationRoster Roster(long depot0 = 100_000, long depot1 = 100_000) => new(Caps(), new[] { depot0, depot1 });

    [Fact]
    public void Dr24Values_EqualLaneNsBattleEntryTable_AndCiteTheDirectorDecision()
    {
        long[] dr24 = { 50, 60, 5, 3, 5, 1, 2, 5 };
        for (int f = 0; f < dr24.Length; f++)
        {
            Assert.Equal(dr24[f], DirectorCaps.CapOf((CapFamily)f));
            Assert.Equal(dr24[f], (long)ProposedCaps.Proposed.CapOf((CapFamily)f));   // the battle-entry check cannot drift
        }
        Assert.Equal(long.MaxValue, DirectorCaps.CapOf(CapFamily.Uncapped));
        Assert.Contains("DR24", DirectorCaps.Citation);
        Assert.Equal(CountedObject.CrewedTube, DirectorCaps.CountedObjectOf(CapFamily.Mortars));
        Assert.Equal(CountedObject.Hull, DirectorCaps.CountedObjectOf(CapFamily.Ships));
        Assert.Equal(CountedObject.Soldier, DirectorCaps.CountedObjectOf(CapFamily.Snipers));
        Assert.Equal(CountedObject.Uncounted, DirectorCaps.CountedObjectOf(CapFamily.Uncapped));
    }

    /// <summary>[CB, D-082 DA5, moved pins] The three soldier-class rows (heavy infantry 50, archers 60, snipers 2) were here: a
    /// soldier formation was refused one person above its cap (READING C1 (a), persons). Under DA5 a soldier cap bounds the battle
    /// entities the formation fights as, min(persons, cap), so one person above the cap is accepted (MEASURED on the DA5 code: the
    /// three rows failed with MaxPersons long.MaxValue for 50, 60 and 2). They moved to
    /// <see cref="Create_SoldierClasses_AreNeverRefusedForSize_TheyCountMinOfPersonsAndCapEntities_DA5"/>. The machine classes are
    /// unchanged: one entity per machine, so a person above cap × crew needs a machine past the cap.</summary>
    [Theory]
    [InlineData(CapFamily.Tanks, 5 * TankCrew)]
    [InlineData(CapFamily.Mortars, 5 * MortarCrew)]
    [InlineData(CapFamily.Ships, 5 * HullCrew)]
    [InlineData(CapFamily.Helicopters, 3 * HeliCrew)]
    [InlineData(CapFamily.FixedWing, 1 * PlaneCrew)]
    public void Create_AcceptedAtTheCap_RefusedOnePersonAbove(CapFamily cls, long maxPersons)
    {
        var r = Roster();
        Assert.Equal(maxPersons, r.Caps.MaxPersons(cls));
        var ok = r.Create(0, 1, cls, 1, maxPersons);
        Assert.True(ok.Accepted);
        Assert.Equal(DirectorCaps.CapOf(cls), ok.Counted);
        string before = Snapshot(r);
        var over = r.Create(0, 2, cls, 1, maxPersons + 1);
        Assert.Equal(CapRefusal.ExceedsCap, over.Refusal);
        Assert.Equal(DirectorCaps.CapOf(cls) + 1, over.Counted);   // ceil: a partly crewed machine is still a machine
        Assert.Equal(DirectorCaps.CapOf(cls), over.Cap);
        Assert.Equal(before, Snapshot(r));                           // a refused edit changes nothing
    }

    /// <summary>[CB, D-082 DA5] A soldier cap counts the battle entities the formation fights as, min(persons, cap): a soldier
    /// formation is accepted at any positive size (its persons are limited by the economy, here the depot), counted at most its cap;
    /// its full-strength persons (one person per entity) is the cap, its maximum persons long.MaxValue. The three rows moved here from
    /// <see cref="Create_AcceptedAtTheCap_RefusedOnePersonAbove"/> (one person above the cap is accepted now).</summary>
    [Theory]
    [InlineData(CapFamily.HeavyInfantry, 50)]
    [InlineData(CapFamily.ArchersLightInfantry, 60)]
    [InlineData(CapFamily.Snipers, 2)]
    public void Create_SoldierClasses_AreNeverRefusedForSize_TheyCountMinOfPersonsAndCapEntities_DA5(CapFamily cls, long cap)
    {
        var r = Roster(depot0: 2_000_000);
        Assert.Equal((long.MaxValue, cap, cap), (r.Caps.MaxPersons(cls), r.Caps.FullStrengthPersons(cls), DirectorCaps.CapOf(cls)));
        long id = 1;
        foreach (long persons in new[] { 1, cap - 1, cap, cap + 1, 3 * cap + 7, 1_000_000 })
        {
            if (persons <= 0) continue;
            var v = r.Create(0, id++, cls, 1, persons);
            Assert.Equal((CapRefusal.None, Math.Min(persons, cap), cap), (v.Refusal, v.Counted, v.Cap));
            Assert.Equal(Math.Min(persons, cap), r.Caps.EntitiesOf(cls, persons));
        }
        Assert.Equal(CapRefusal.DepotShort, r.Create(0, id, cls, 1, 1_000_000).Refusal);   // the economy (the depot) binds, not the cap
        Assert.Equal(r.OpeningPersons, r.TotalPersons);
    }

    [Fact]
    public void MachineClassWithoutACrewRule_IsRefused_NeverGuessed()
    {
        var r = new FormationRoster(DirectorCaps.Values, new[] { 1_000L });
        Assert.Equal(CapRefusal.NoCrewRule, r.Create(0, 1, CapFamily.Tanks, 605, 20).Refusal);
        Assert.Equal(CapRefusal.NoCrewRule, r.Create(0, 2, CapFamily.Mortars, 1, 4).Refusal);
        Assert.True(r.Create(0, 3, CapFamily.HeavyInfantry, 301, 20).Accepted);
        Assert.Throws<ArgumentException>(() => DirectorCaps.Values.WithCrew(CapFamily.HeavyInfantry, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => DirectorCaps.Values.WithCrew(CapFamily.Tanks, 0));
    }

    [Fact]
    public void UncappedClass_ReusesLaneNsCap5Treatment_NeverRefusedForSize()
    {
        var r = Roster(depot0: 2_000_000);
        var v = r.Create(0, 1, CapFamily.Uncapped, 801, 1_000_000);
        Assert.True(v.Accepted);
        Assert.Equal(long.MaxValue, v.Cap);
        Assert.Null(ProposedCaps.Proposed.Check(CapFamily.Uncapped, 1_000_000));    // lane N agrees
        Assert.Equal(CapRefusal.NonPositive, r.Create(0, 2, CapFamily.Uncapped, 801, 0).Refusal);
    }

    [Fact]
    public void Create_RefusalCodes()
    {
        var r = Roster(depot0: 30);
        Assert.Equal(CapRefusal.NonPositive, r.Create(0, 1, CapFamily.HeavyInfantry, 301, 0).Refusal);
        Assert.Equal(CapRefusal.NonPositive, r.Create(0, 1, CapFamily.HeavyInfantry, 301, -5).Refusal);
        Assert.Equal(CapRefusal.DepotShort, r.Create(0, 1, CapFamily.HeavyInfantry, 301, 31).Refusal);
        Assert.Equal(CapRefusal.NotOwner, r.Create(7, 1, CapFamily.HeavyInfantry, 301, 10).Refusal);
        Assert.True(r.Create(0, 1, CapFamily.HeavyInfantry, 301, 30).Accepted);
        Assert.Equal(CapRefusal.DuplicateId, r.Create(1, 1, CapFamily.HeavyInfantry, 301, 10).Refusal);
        Assert.Equal(0, r.DepotOf(0));
        // the cap is checked before the depot: an over-cap request is ExceedsCap even when the depot is short too.
        // [CB, D-082 DA5, moved pin] This was pinned with 51 heavy infantry (ExceedsCap). Under DA5 a soldier class is never over its
        // cap (51 persons fight as 50 entities), so the same request is refused only by the empty depot (MEASURED on the DA5 code:
        // DepotShort, not ExceedsCap). The cap-before-depot order is pinned on a machine class instead: 21 tank crew at 4 per tank
        // are 6 tanks > 5.
        Assert.Equal(CapRefusal.DepotShort, r.Create(0, 2, CapFamily.HeavyInfantry, 301, 51).Refusal);
        var v = r.Create(0, 2, CapFamily.Tanks, 605, 5 * TankCrew + 1);
        Assert.Equal((CapRefusal.ExceedsCap, 6L, 5L), (v.Refusal, v.Counted, v.Cap));
    }

    [Fact]
    public void Split_ChecksBothParts_AndKeepsOnePersonInTheSource()
    {
        var r = Roster();
        Assert.True(r.Create(0, 10, CapFamily.ArchersLightInfantry, 402, 60).Accepted);
        Assert.Equal(CapRefusal.NonPositive, r.Split(0, 10, 11, 60).Refusal);          // would empty the source
        Assert.Equal(CapRefusal.InsufficientPersons, r.Split(0, 10, 11, 61).Refusal);
        Assert.Equal(CapRefusal.NonPositive, r.Split(0, 10, 11, 0).Refusal);
        Assert.Equal(CapRefusal.DuplicateId, r.Split(0, 10, 10, 5).Refusal);
        Assert.Equal(CapRefusal.UnknownFormation, r.Split(0, 99, 11, 5).Refusal);
        Assert.Equal(CapRefusal.NotOwner, r.Split(1, 10, 11, 5).Refusal);
        var v = r.Split(0, 10, 11, 25);
        Assert.True(v.Accepted);
        Assert.Equal(35, r.Find(10)!.Persons);
        Assert.Equal(25, r.Find(11)!.Persons);
        Assert.Equal(402, r.Find(11)!.IdentityKey);
        Assert.Equal(CapFamily.ArchersLightInfantry, r.Find(11)!.Class);
        Assert.Equal(r.OpeningPersons, r.TotalPersons);
    }

    [Fact]
    public void Merge_RefusedAboveTheCap_AcrossClassesAndAcrossIdentities()
    {
        var r = Roster();
        Assert.True(r.Create(0, 1, CapFamily.HeavyInfantry, 303, 30).Accepted);
        Assert.True(r.Create(0, 2, CapFamily.HeavyInfantry, 303, 20).Accepted);
        Assert.True(r.Create(0, 3, CapFamily.HeavyInfantry, 303, 21).Accepted);
        Assert.True(r.Create(0, 4, CapFamily.HeavyInfantry, 304, 5).Accepted);           // another identity
        Assert.True(r.Create(0, 5, CapFamily.ArchersLightInfantry, 303, 5).Accepted);    // another class
        Assert.True(r.Create(1, 6, CapFamily.HeavyInfantry, 303, 5).Accepted);           // another owner
        Assert.Equal(CapRefusal.SameFormation, r.Merge(0, 1, 1).Refusal);
        Assert.Equal(CapRefusal.ClassMismatch, r.Merge(0, 1, 4).Refusal);
        Assert.Equal(CapRefusal.ClassMismatch, r.Merge(0, 1, 5).Refusal);
        Assert.Equal(CapRefusal.NotOwner, r.Merge(0, 1, 6).Refusal);
        // [CB, D-082 DA5, moved pin] 30 + 21 = 51 heavy infantry was refused ExceedsCap (counted 51 persons). Under DA5 it is
        // accepted: the merged formation fights as 50 entities standing for 51 persons (MEASURED on the DA5 code: None, not
        // ExceedsCap). The over-cap merge is pinned on tanks (machine class: one entity per tank): 12 + 9 crew at 4 per tank = 21
        // persons = 6 tanks > 5.
        var merged = r.Merge(0, 1, 3);
        Assert.Equal((CapRefusal.None, 50L, 50L), (merged.Refusal, merged.Counted, merged.Cap));
        Assert.Equal(51, r.Find(1)!.Persons);
        Assert.Null(r.Find(3));                                                          // the absorbed formation is gone
        Assert.True(r.Merge(0, 1, 2).Accepted);
        Assert.Equal(71, r.Find(1)!.Persons);
        Assert.Null(r.Find(2));
        Assert.True(r.Create(0, 7, CapFamily.Tanks, 605, 12).Accepted);
        Assert.True(r.Create(0, 8, CapFamily.Tanks, 605, 9).Accepted);
        Assert.True(r.Create(0, 9, CapFamily.Tanks, 605, 8).Accepted);
        var over = r.Merge(0, 7, 8);
        Assert.Equal((CapRefusal.ExceedsCap, 6L), (over.Refusal, over.Counted));
        Assert.True(r.Merge(0, 7, 9).Accepted);                                          // 20 = 5 tanks
        Assert.Equal(20, r.Find(7)!.Persons);
        Assert.Equal(r.OpeningPersons, r.TotalPersons);
    }

    [Fact]
    public void ReinforceDetachTransfer_Boundaries()
    {
        var r = Roster(depot0: 100);
        Assert.True(r.Create(0, 1, CapFamily.Tanks, 605, 12).Accepted);                  // 3 tanks
        Assert.True(r.Reinforce(0, 1, 8).Accepted);                                      // 20 people = 5 tanks
        Assert.Equal(CapRefusal.ExceedsCap, r.Reinforce(0, 1, 1).Refusal);                 // a 6th (partly crewed) tank
        Assert.Equal(CapRefusal.NonPositive, r.Detach(0, 1, 20).Refusal);                 // at least one stays
        Assert.Equal(CapRefusal.InsufficientPersons, r.Detach(0, 1, 21).Refusal);
        Assert.True(r.Detach(0, 1, 19).Accepted);
        Assert.Equal(1, r.Find(1)!.Persons);
        Assert.True(r.Create(0, 2, CapFamily.Tanks, 605, 16).Accepted);
        Assert.Equal(CapRefusal.InsufficientPersons, r.Transfer(0, 1, 2, 5).Refusal);     // only 1 in the source
        Assert.True(r.Reinforce(0, 1, 9).Accepted);                                      // 10
        Assert.Equal(CapRefusal.ExceedsCap, r.Transfer(0, 1, 2, 5).Refusal);              // 16 + 5 = 21 > 20
        Assert.True(r.Transfer(0, 1, 2, 4).Accepted);
        Assert.Equal(6, r.Find(1)!.Persons);
        Assert.Equal(20, r.Find(2)!.Persons);
        Assert.Equal(CapRefusal.SameFormation, r.Transfer(0, 2, 2, 1).Refusal);
        Assert.Equal(CapRefusal.ExceedsCap, r.Reinforce(0, 1, 15).Refusal);              // the cap is checked before the depot
        Assert.Equal(74, r.DepotOf(0));                                                   // 100 - 12 - 8 + 19 - 16 - 9
        Assert.True(r.Create(0, 3, CapFamily.Uncapped, 801, 1).Accepted);
        Assert.Equal(CapRefusal.DepotShort, r.Reinforce(0, 3, 74).Refusal);                 // 73 left
        Assert.True(r.Reinforce(0, 3, 73).Accepted);
        Assert.Equal(0, r.DepotOf(0));
        Assert.Equal(r.OpeningPersons, r.TotalPersons);
    }

    /// <summary>Conservation and the cap invariant under a long, deterministic, mixed sequence of edits (most refused): the
    /// people total is the opening total after EVERY edit (exact), and every formation is always within its cap.
    /// [verification round 1, D-R1-05] The ledger total is conserved by construction, so it cannot see people stranded in the
    /// account of a formation that no longer exists (verifier mutant V1 survived). Every person must be in a depot or in a LIVE
    /// formation: depots + the sum of formation persons = the opening total, after every edit.</summary>
    [Fact]
    public void AnyEditSequence_ConservesPeopleExactly_AndNeverLeavesAFormationAboveItsCap()
    {
        var r = Roster(depot0: 5_000, depot1: 3_000);
        var classes = new[] { CapFamily.HeavyInfantry, CapFamily.ArchersLightInfantry, CapFamily.Snipers, CapFamily.Tanks, CapFamily.Mortars, CapFamily.Uncapped };
        int accepted = 0, disbanded = 0;
        for (int i = 0; i < 4_000; i++)
        {
            ulong Draw(int k, ulong n) => CounterRng.NextBelow(7, 501, i, k, 0, 0, n);
            int owner = (int)Draw(0, 2);
            long a = (long)Draw(1, 40), b = (long)Draw(2, 40), p = (long)Draw(3, 70) - 5;
            var cls = classes[(int)Draw(4, (ulong)classes.Length)];
            ulong kind = Draw(5, 7);
            CapVerdict v = kind switch
            {
                0 => r.Create(owner, a, cls, (int)cls, p),
                1 => r.Split(owner, a, b, p),
                2 => r.Merge(owner, a, b),
                3 => r.Reinforce(owner, a, p),
                4 => r.Detach(owner, a, p),
                5 => r.Transfer(owner, a, b, p),
                _ => r.Disband(owner, a),
            };
            if (v.Accepted) accepted++;
            if (v.Accepted && kind == 6) disbanded++;
            Assert.Equal(r.OpeningPersons, r.TotalPersons);
            Int128 accounted = (Int128)r.DepotOf(0) + r.DepotOf(1);
            foreach (var f in r.Formations) accounted += f.Persons;
            Assert.Equal(r.OpeningPersons, accounted);
            foreach (var f in r.Formations)
            {
                Assert.True(f.Persons > 0);
                Assert.True(r.Caps.Check(f.Id, f.Class, f.Persons).Accepted, $"formation {f.Id} above its cap after edit {i}");
            }
            for (int k = 1; k < r.Formations.Count; k++) Assert.True(r.Formations[k - 1].Id < r.Formations[k].Id);
        }
        Assert.InRange(accepted, 200, 3_900);   // the sequence exercises both outcomes
        Assert.True(disbanded > 0, "the sequence disbands formations");
    }

    /// <summary>[verification round 1, D-R1-05] Disband returns every person of the formation to its owner's depot: exact depot
    /// values, the formation gone, the log row carrying the persons; a refused disband changes nothing.</summary>
    [Fact]
    public void Disband_ReturnsEveryPersonToTheDepot_AndTheRosterAccountsForEveryone()
    {
        var r = Roster(depot0: 100, depot1: 50);
        Assert.True(r.Create(0, 1, CapFamily.HeavyInfantry, 301, 30).Accepted);
        Assert.True(r.Create(0, 2, CapFamily.Tanks, 605, 12).Accepted);
        Assert.Equal(58, r.DepotOf(0));
        Assert.True(r.Disband(0, 1).Accepted);
        Assert.Equal(30, r.Log[^1].Persons);
        Assert.Equal(88, r.DepotOf(0));
        Assert.Null(r.Find(1));
        Assert.Equal(CapRefusal.NotOwner, r.Disband(1, 2).Refusal);
        Assert.Equal(CapRefusal.UnknownFormation, r.Disband(0, 1).Refusal);
        Assert.Equal((88L, 50L), (r.DepotOf(0), r.DepotOf(1)));
        Assert.True(r.Disband(0, 2).Accepted);
        Assert.Equal((100L, 50L), (r.DepotOf(0), r.DepotOf(1)));
        Assert.Empty(r.Formations);
        Assert.Equal(r.OpeningPersons, (Int128)r.DepotOf(0) + r.DepotOf(1));
    }

    /// <summary>TEST INPUT: every machine-class row of Sim.Data/content/unit-families.json at 45c3313 — key, line, personnel and the
    /// DR24 class the caps proposal (§3 CAP-3, corrected in verification round 1, D-R1-08) puts it in. Copied as constants with
    /// their lines: the prototype reads nothing from Sim.Data. Drones (2101, 1104) are listed apart (R6: their class is fixed in
    /// M7.6).</summary>
    private static readonly (int Key, int Line, long Personnel, CapFamily Class)[] MachineRows =
    {
        (605, 134, 60, CapFamily.Tanks),
        (1102, 291, 20, CapFamily.Helicopters),
        (901, 175, 40, CapFamily.Ships), (902, 176, 60, CapFamily.Ships), (903, 177, 60, CapFamily.Ships), (904, 178, 60, CapFamily.Ships),
        (905, 179, 60, CapFamily.Ships), (906, 180, 60, CapFamily.Ships),
        (1001, 191, 20, CapFamily.Ships), (1002, 192, 20, CapFamily.Ships), (1003, 193, 40, CapFamily.Ships), (1004, 194, 40, CapFamily.Ships),
        (1005, 195, 40, CapFamily.Ships), (1006, 196, 40, CapFamily.Ships),
        (1701, 250, 40, CapFamily.Ships), (1702, 251, 60, CapFamily.Ships), (1703, 252, 60, CapFamily.Ships),
        (1801, 265, 40, CapFamily.Ships), (907, 266, 60, CapFamily.Ships), (908, 279, 60, CapFamily.Ships), (1901, 280, 60, CapFamily.Ships),
        (1101, 209, 20, CapFamily.FixedWing), (1105, 210, 20, CapFamily.FixedWing), (1106, 211, 20, CapFamily.FixedWing), (1103, 212, 20, CapFamily.FixedWing),
        (2301, 330, 20, CapFamily.FixedWing), (2302, 331, 40, CapFamily.FixedWing), (2303, 332, 40, CapFamily.FixedWing),
    };

    private static readonly (int Key, int Line, long Personnel)[] DroneRows = { (2101, 304, 10), (1104, 305, 10) };

    /// <summary>
    /// [verification round 1, D-R1-08] CAP-3 option (A), one persons-per-machine value per class. The round-0 proposal took the
    /// fixed-wing value 20 from 1101 alone and said every shipped row then fits; the 40-personnel strike-aviation rows 2302 and
    /// 2303 count 2 airframes against the cap of 1 (verifier probe P5). The corrected minimal values are ceil(largest personnel of
    /// the class / cap): tanks 12, helicopters 7, ships 12, fixed-wing 40 (computed here independently, not copied). Each keeps
    /// every row of its class within the cap, each value minus one refuses a row, and the drones fit either candidate class.
    /// </summary>
    [Fact]
    public void Cap3_OptionA_PerClassCrewValues_MinimalValuesKeepEveryMachineRowWithinItsCap()
    {
        var old = DirectorCaps.Values.WithCrew(CapFamily.FixedWing, 20);
        var refused = MachineRows.Where(m => m.Class == CapFamily.FixedWing && !old.Check(m.Key, CapFamily.FixedWing, m.Personnel).Accepted)
                                 .Select(m => m.Key).ToArray();
        Assert.Equal(new[] { 2302, 2303 }, refused);
        Assert.Equal(2, old.Check(2302, CapFamily.FixedWing, 40).Counted);
        foreach (var (cls, expected) in new[] { (CapFamily.Tanks, 12L), (CapFamily.Helicopters, 7L), (CapFamily.Ships, 12L), (CapFamily.FixedWing, 40L) })
        {
            var rows = MachineRows.Where(m => m.Class == cls).ToArray();
            Assert.NotEmpty(rows);
            long largest = rows.Max(m => m.Personnel), cap = DirectorCaps.CapOf(cls);
            long minimal = largest / cap + (largest % cap == 0 ? 0 : 1);
            Assert.Equal(expected, minimal);
            var at = DirectorCaps.Values.WithCrew(cls, minimal);
            Assert.All(rows, m => Assert.True(at.Check(m.Key, cls, m.Personnel).Accepted, $"{m.Key} (line {m.Line})"));
            var below = DirectorCaps.Values.WithCrew(cls, minimal - 1);
            Assert.Contains(rows, m => below.Check(m.Key, cls, m.Personnel).Refusal == CapRefusal.ExceedsCap);
        }
        Assert.Equal(28, MachineRows.Length);                          // + 2 drone rows = the 30 rows option (B) may touch
        foreach (var (cls, v) in new[] { (CapFamily.FixedWing, 40L), (CapFamily.Helicopters, 7L) })
            Assert.All(DroneRows, d => Assert.True(DirectorCaps.Values.WithCrew(cls, v).Check(d.Key, cls, d.Personnel).Accepted));
    }

    /// <summary>TIE-DENSE: every formation at the same fill fraction. Recruits go round-robin in ascending id; the result and the
    /// roster hash do not depend on the order in which the formations were created.
    /// [CB, D-082 DA5, moved pin] The eight formations were heavy infantry at 10 of 50. Under DA5 a soldier formation has no persons
    /// cap to fill and takes no part (MEASURED on the DA5 code: [0, 0, 0, 0, 0, 0, 0, 0] instead of [2, 2, 2, 2, 2, 1, 1, 1]). The same
    /// tie-dense property is pinned on tanks: 4 crew of 20 (5 tanks × 4, the TEST INPUT crew), the same fill 1/5; one heavy-infantry
    /// formation in the roster gets nothing.
    /// [CB, verification round 1, CB-R1-01] Superseded attribution: "Under DA5 a soldier formation has no persons cap to fill and takes
    /// no part" is READING CB-4 (a), this lane's choice for ReinforceToCaps, not something DA5 forces. Under alternative (b) (soldier
    /// formations filled to their full complement of entities, one person each) the base test of 0d2db74 passes unchanged and the
    /// heavy-infantry formation below would take part (MEASURED, scratchpad probe). This pin moved because of READING CB-4 (a).</summary>
    [Fact]
    public void ReinforceToCaps_TieDense_RoundRobinByIdAndIndependentOfCreationOrder()
    {
        long[] ids = { 41, 7, 19, 3, 88, 12, 56, 30 };
        string? hash = null;
        long[]? first = null;
        for (int perm = 0; perm < 6; perm++)
        {
            var order = TestSupport.Permutation(ids.Length, (ulong)(100 + perm));
            var r = Roster(depot0: 1_000);
            foreach (int k in order) Assert.True(r.Create(0, ids[k], CapFamily.Tanks, 605, 4).Accepted);
            Assert.True(r.Create(0, 99, CapFamily.HeavyInfantry, 301, 10).Accepted);      // a soldier formation: no part (READING CB-4 (a))
            var got = r.ReinforceToCaps(0, 13);
            Assert.Equal(0, got[^1]);
            got = got[..^1];
            // ascending id 3,7,12,19,30,41,56,88: 13 = 8 + 5 -> the five lowest ids get 2, the rest 1
            Assert.Equal(new long[] { 2, 2, 2, 2, 2, 1, 1, 1 }, got);
            first ??= got;
            Assert.Equal(first, got);
            string h = Snapshot(r);
            hash ??= h;
            Assert.Equal(hash, h);
        }
    }

    /// <summary>TIE-DENSE across classes: 4/20 (tanks, 5 × 4 crew) and 200/1,000 (ships, 5 × 200 crew) are the same fraction; the
    /// exact cross-multiplication sees the tie and the id breaks it. An independent oracle (re-sort every step) agrees.
    /// [CB, D-082 DA5, moved pin] The tie was 10/50 (heavy infantry) = 12/60 (archers), with snipers at 1/2. Under DA5 soldier
    /// formations have no persons cap and take no part (MEASURED on the DA5 code: [0, 0, 0, 0] instead of the oracle's [0, 11, 0, 12]).
    /// The same exact cross-class tie is pinned on two machine classes (TEST INPUT crews); a heavy-infantry formation and an
    /// uncapped one take no part.
    /// [CB, verification round 1, CB-R1-01] Superseded attribution: "Under DA5 soldier formations have no persons cap and take no part"
    /// is READING CB-4 (a), not DA5 itself; under alternative (b) the base test of 0d2db74 passes unchanged (MEASURED, scratchpad
    /// probe). This pin moved because of READING CB-4 (a).</summary>
    [Fact]
    public void ReinforceToCaps_ExactFractionTiesAcrossClasses_BrokenByIdOnly_MatchesOracle()
    {
        var r = Roster(depot0: 2_000);
        Assert.True(r.Create(0, 9, CapFamily.Ships, 906, 200).Accepted);                 // 200/1,000 = 1/5
        Assert.True(r.Create(0, 4, CapFamily.Tanks, 605, 4).Accepted);                   // 4/20 = 1/5
        Assert.True(r.Create(0, 6, CapFamily.Helicopters, 1102, 14).Accepted);           // 14/21 = 2/3: last
        Assert.True(r.Create(0, 2, CapFamily.Uncapped, 801, 3).Accepted);                // takes no part
        Assert.True(r.Create(0, 3, CapFamily.HeavyInfantry, 301, 10).Accepted);          // takes no part (READING CB-4 (a))
        var before = new long[] { 3, 10, 4, 14, 200 };                                   // ascending id 2,3,4,6,9
        var max = new long[] { -1, -1, 5 * TankCrew, 3 * HeliCrew, 5 * HullCrew };
        var got = r.ReinforceToCaps(0, 23);
        var oracle = Oracle(before, max, new long[] { 2, 3, 4, 6, 9 }, 23);
        Assert.Equal(oracle, got);
        Assert.Equal((0L, 0L), (got[0], got[1]));     // uncapped, soldier
        Assert.Equal(23, got[2] + got[3] + got[4]);
        Assert.Equal(2_000 - 231 - 23, r.DepotOf(0));
        // One recruit at the exact tie 4/20 = 200/1,000: the lower id (4) takes it, whatever the creation order.
        foreach (bool shipsFirst in new[] { true, false })
        {
            var t = Roster(depot0: 1_000);
            if (shipsFirst) t.Create(0, 9, CapFamily.Ships, 906, 200);
            t.Create(0, 4, CapFamily.Tanks, 605, 4);
            if (!shipsFirst) t.Create(0, 9, CapFamily.Ships, 906, 200);
            Assert.Equal(new long[] { 1, 0 }, t.ReinforceToCaps(0, 1));
        }
        // the comparison is exact (cross-multiplication)
        Assert.True(FormationRoster.Less(10, 50, 4, 12, 60, 9));
        Assert.False(FormationRoster.Less(12, 60, 9, 10, 50, 4));
        Assert.True(FormationRoster.Less(11, 60, 9, 10, 50, 4));    // 11/60 < 1/5
    }

    /// <summary>[verification round 2, D-R2-03] ReinforceToCaps' two stopping rules (its documentation: "Stops when no capped
    /// formation has room or the depot is empty"), each binding alone, and what it returns is what moved. (Verifier mutants C1, filling
    /// one person past the cap, and C2, ignoring the depot, returned more than moved while their refused grants moved nobody.)
    /// [CB, D-082 DA5, moved pin] The two formations were heavy infantry at 45 of 50: [5, 5] at the caps, [2, 2] at the empty depot.
    /// Under DA5 they have no persons cap and take no part (MEASURED on the DA5 code: [0, 0] instead of [5, 5]); a heavy-infantry
    /// formation is reinforced past 50 persons directly (it still fights as 50 entities). The two stopping rules are pinned on tanks
    /// (TEST INPUT crew 4, cap 5: 20 persons): two formations of 18, 20 recruits. The caps bind: [2, 2], both at 20, and a second call
    /// moves nothing. The depot binds: 2 left after creation, so [1, 1] by the (fill, id) key, persons 19 and 19, depot 0.
    /// [CB, verification round 1, CB-R1-01] Superseded attribution: "Under DA5 they have no persons cap and take no part" is READING
    /// CB-4 (a), not DA5 itself; under alternative (b) the base test of 0d2db74 ([5, 5] at the caps, [2, 2] at the depot) passes
    /// unchanged (MEASURED, scratchpad probe). This pin moved because of READING CB-4 (a); the direct reinforcement past 50 persons
    /// below is DA5's (a soldier formation is never refused for size).</summary>
    [Fact]
    public void ReinforceToCaps_StopsAtEveryCapAndAtTheEmptyDepot_ReturningWhatMoved()
    {
        var r = Roster(depot0: 200);
        Assert.True(r.Create(0, 1, CapFamily.Tanks, 605, 18).Accepted);
        Assert.True(r.Create(0, 2, CapFamily.Tanks, 605, 18).Accepted);
        Assert.Equal(164, r.DepotOf(0));
        Assert.Equal(new long[] { 2, 2 }, r.ReinforceToCaps(0, 20));
        Assert.Equal((20L, 20L, 160L), (r.Find(1)!.Persons, r.Find(2)!.Persons, r.DepotOf(0)));
        Assert.Equal(new long[] { 0, 0 }, r.ReinforceToCaps(0, 20));
        Assert.Equal(160, r.DepotOf(0));
        var d = Roster(depot0: 38);
        Assert.True(d.Create(0, 1, CapFamily.Tanks, 605, 18).Accepted);
        Assert.True(d.Create(0, 2, CapFamily.Tanks, 605, 18).Accepted);
        Assert.Equal(2, d.DepotOf(0));
        Assert.Equal(new long[] { 1, 1 }, d.ReinforceToCaps(0, 20));
        Assert.Equal((19L, 19L, 0L), (d.Find(1)!.Persons, d.Find(2)!.Persons, d.DepotOf(0)));
        Assert.Equal(new long[] { 0, 0 }, d.ReinforceToCaps(0, 20));
        Assert.Equal((19L, 19L), (d.Find(1)!.Persons, d.Find(2)!.Persons));
        // READING CB-4 (a): soldier formations take no part; DA5: nothing stops them growing past the cap in persons
        var h = Roster(depot0: 200);
        Assert.True(h.Create(0, 1, CapFamily.HeavyInfantry, 301, 45).Accepted);
        Assert.True(h.Create(0, 2, CapFamily.HeavyInfantry, 301, 45).Accepted);
        Assert.Equal(new long[] { 0, 0 }, h.ReinforceToCaps(0, 20));
        Assert.Equal(110, h.DepotOf(0));
        var v = h.Reinforce(0, 1, 20);
        Assert.Equal((CapRefusal.None, 50L, 50L), (v.Refusal, v.Counted, v.Cap));
        Assert.Equal((65L, 90L), (h.Find(1)!.Persons, h.DepotOf(0)));
    }

    /// <summary>Brute force: each recruit to the argmin of (fraction, id) found by a full scan with rationals compared through
    /// BigInteger (independent arithmetic).</summary>
    private static long[] Oracle(long[] persons, long[] max, long[] ids, long recruits)
    {
        var p = (long[])persons.Clone();
        var got = new long[p.Length];
        for (long n = 0; n < recruits; n++)
        {
            int best = -1;
            for (int k = 0; k < p.Length; k++)
            {
                if (max[k] < 0 || p[k] >= max[k]) continue;
                if (best < 0) { best = k; continue; }
                var lhs = new System.Numerics.BigInteger(p[k]) * max[best];
                var rhs = new System.Numerics.BigInteger(p[best]) * max[k];
                if (lhs < rhs || (lhs == rhs && ids[k] < ids[best])) best = k;
            }
            if (best < 0) break;
            p[best]++; got[best]++;
        }
        return got;
    }

    [Theory]
    [InlineData(130L, 50L, new long[] { 44, 43, 43 })]
    [InlineData(100L, 50L, new long[] { 50, 50 })]
    [InlineData(1L, 50L, new long[] { 1 })]
    [InlineData(61L, 60L, new long[] { 31, 30 })]
    [InlineData(5L, 2L, new long[] { 2, 2, 1 })]
    public void SplitSizes_ExactFewestEvenParts(long persons, long max, long[] expected)
    {
        var s = FormationRoster.SplitSizes(persons, max);
        Assert.Equal(expected, s);
        long sum = 0;
        foreach (long x in s) { Assert.InRange(x, 1, max); sum += x; }
        Assert.Equal(persons, sum);
    }

    [Fact]
    public void SameEditSequence_SameLogAndHash()
    {
        string Run()
        {
            var r = Roster();
            r.Create(0, 1, CapFamily.HeavyInfantry, 301, 40);
            r.Create(0, 2, CapFamily.HeavyInfantry, 301, 40);
            r.Merge(0, 1, 2);
            r.Split(0, 1, 3, 15);
            r.Transfer(0, 1, 3, 30);
            r.Disband(0, 3);
            return r.HashHex();
        }
        Assert.Equal(Run(), Run());
    }

    private static string Snapshot(FormationRoster r)
    {
        var parts = new List<string> { r.DepotOf(0).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        foreach (var f in r.Formations) parts.Add(f.ToString());
        return string.Join("|", parts);
    }
}
