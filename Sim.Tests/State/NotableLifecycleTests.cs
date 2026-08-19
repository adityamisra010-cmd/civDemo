using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.Systems;

namespace Sim.Tests.State;

/// <summary>
/// T4.8 — NOTABLES AS PEOPLE (R-1). The packet's acceptance, verbatim from
/// m4-spec §4: "a notable can be BORN, DIE and DEFECT with no bookkeeping outside
/// the Ledger — the property Option B was taken for".
///
/// So these tests are about CONSERVATION, not about generals. There is no
/// AutoResolver here, no competence, no traits, no battle: that is M6, and
/// m4-spec §1.6 keeps it there.
/// </summary>
public class NotableLifecycleTests
{
    private static readonly SettlementId S0 = new(0);
    private static readonly SettlementId S1 = new(1);
    private static readonly PolityId P1 = new(1);
    private static readonly PolityId P2 = new(2);

    /// <summary>World population summed over BOTH carriers — buckets and notables.
    /// The whole point of R-1 Option B is that this total is the same quantity in
    /// both places, so a promotion must not change it.</summary>
    private static long People(WorldState w)
    {
        long total = 0;
        for (int i = 0; i < w.Buckets.Count; i++) total += w.Buckets[i].Count.Value;
        for (int i = 0; i < w.Notables.Count; i++) total += w.Notables[i].Count.Value;
        return total;
    }

    private static WorldState World(long adults)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = adults;
        return PopulationExactnessTests.BucketWorld(counts);
    }

    private static int AdultBucketRow(WorldState w)
    {
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].CohortIdx == 5) return i;
        return -1;
    }

    // --- BORN -----------------------------------------------------------------

    [Fact]
    public void Born_ExtractsExactlyOnePerson_WorldPopulationUnchanged()
    {
        WorldState w = World(adults: 100);
        var ledger = new Ledger(w.LedgerFlows);
        long before = People(w);
        int bucket = AdultBucketRow(w);

        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(1), P1);

        Assert.True(row >= 0);
        Assert.Equal(99, w.Buckets[bucket].Count.Value);   // the bucket is one lighter
        Assert.Equal(1, w.Notables[row].Count.Value);      // the notable IS that person
        Assert.Equal(before, People(w));                   // and the world is unchanged — EXACT
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
    }

    [Fact]
    public void Born_CarriesSettlementAndCohortOfTheBucketTheyLeft()
    {
        // A notable is a PERSON, so they are a person of some age, from somewhere.
        // Losing that would make the extraction silently remove someone from the
        // age pyramid and leave an age-less token behind.
        WorldState w = World(adults: 10);
        var ledger = new Ledger(w.LedgerFlows);
        int bucket = AdultBucketRow(w);

        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(7), P1);

        Assert.Equal(w.Buckets[bucket].Settlement, w.Notables[row].Settlement);
        Assert.Equal(w.Buckets[bucket].CohortIdx, w.Notables[row].CohortIdx);
        Assert.Equal(P1, w.Notables[row].Allegiance);
        Assert.Equal(7, w.Notables[row].Id.Value);
    }

    [Fact]
    public void Born_FromAnEmptyBucket_PromotesNobody_AndCreatesNoRow()
    {
        // Never from nothing (law 1). An empty bucket cannot furnish a person, and
        // the refusal must leave NO half-made notable behind.
        WorldState w = World(adults: 0);
        var ledger = new Ledger(w.LedgerFlows);
        int bucket = AdultBucketRow(w);

        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(1), P1);

        Assert.Equal(-1, row);
        Assert.Equal(0, w.Notables.Count);
        Assert.Equal(0, People(w));
    }

    // --- DIES -----------------------------------------------------------------

    [Fact]
    public void Dies_SinksThePerson_AndTheLedgerAccountsForThem()
    {
        WorldState w = World(adults: 100);
        var ledger = new Ledger(w.LedgerFlows);
        int bucket = AdultBucketRow(w);
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(1), P1);

        long moved = NotableLifecycle.Dies(ledger, w.Notables, row);

        Assert.Equal(1, moved);
        Assert.Equal(0, w.Notables[row].Count.Value);      // the slot is vacated…
        Assert.Equal(1, w.Notables.Count);                 // …but the identity is history, not deleted
        Assert.Equal(99, People(w));                       // one fewer person in the world
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);

        // The death is recorded under the SAME reason a bucket death uses: one
        // audit question, one answer.
        long sunk = 0;
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.Population
                && w.LedgerFlows[i].Reason == ReasonIds.Deaths) sunk = w.LedgerFlows[i].TotalSunk;
        Assert.Equal(1, sunk);
    }

    [Fact]
    public void Dies_Twice_TakesNobodyTheSecondTime()
    {
        // A vacated slot holds nobody, so a repeated death cannot mint a corpse.
        WorldState w = World(adults: 10);
        var ledger = new Ledger(w.LedgerFlows);
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, AdultBucketRow(w), new NotableId(1), P1);

        Assert.Equal(1, NotableLifecycle.Dies(ledger, w.Notables, row));
        Assert.Equal(0, NotableLifecycle.Dies(ledger, w.Notables, row));
        Assert.Equal(9, People(w));
    }

    // --- DEFECTS --------------------------------------------------------------

    [Fact]
    public void Defects_MovesTheSamePerson_KeepsIdentity_ChangesAllegiance()
    {
        WorldState w = World(adults: 100);
        var ledger = new Ledger(w.LedgerFlows);
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, AdultBucketRow(w), new NotableId(42), P1);
        long before = People(w);

        int newRow = NotableLifecycle.Defects(ledger, w.Notables, row, S1, P2);

        Assert.True(newRow >= 0);
        Assert.Equal(0, w.Notables[row].Count.Value);       // vacated the old post
        Assert.Equal(1, w.Notables[newRow].Count.Value);    // …and is standing in the new one
        Assert.Equal(42, w.Notables[newRow].Id.Value);      // SAME person
        Assert.Equal(P2, w.Notables[newRow].Allegiance);    // different master
        Assert.Equal(S1, w.Notables[newRow].Settlement);
        Assert.Equal(before, People(w));                    // nobody created, nobody lost
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
    }

    [Fact]
    public void Defects_LeavesExactlyOneLivingRowForTheIdentity()
    {
        // After a defection the id appears twice; only ONE row may hold the person.
        // A notable in two places at once is the duplication R-1 exists to prevent.
        WorldState w = World(adults: 100);
        var ledger = new Ledger(w.LedgerFlows);
        var id = new NotableId(9);
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, AdultBucketRow(w), id, P1);
        int newRow = NotableLifecycle.Defects(ledger, w.Notables, row, S1, P2);

        int living = 0;
        for (int i = 0; i < w.Notables.Count; i++)
            if (w.Notables[i].Id == id && w.Notables[i].Count.Value > 0) living++;
        Assert.Equal(1, living);
        Assert.Equal(newRow, NotableLifecycle.LivingRowOf(w.Notables, id));
    }

    [Fact]
    public void Defects_FromAVacatedSlot_MovesNobody()
    {
        WorldState w = World(adults: 10);
        var ledger = new Ledger(w.LedgerFlows);
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, AdultBucketRow(w), new NotableId(1), P1);
        NotableLifecycle.Dies(ledger, w.Notables, row);

        Assert.Equal(-1, NotableLifecycle.Defects(ledger, w.Notables, row, S1, P2));
        Assert.Equal(9, People(w));
    }

    // --- THE WHOLE LIFE -------------------------------------------------------

    [Fact]
    public void BornDefectsDies_TheWholeLife_IsConservationExactAtEveryStep()
    {
        // The packet's acceptance criterion end to end: "a notable can be BORN,
        // DIE and DEFECT with no bookkeeping outside the Ledger."
        WorldState w = World(adults: 50);
        var ledger = new Ledger(w.LedgerFlows);
        long start = People(w);

        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, AdultBucketRow(w), new NotableId(3), P1);
        Assert.Equal(start, People(w));
        Assert.True(ConservationAuditor.IsConserved(w, out string r1), r1);

        int defected = NotableLifecycle.Defects(ledger, w.Notables, row, S1, P2);
        Assert.Equal(start, People(w));
        Assert.True(ConservationAuditor.IsConserved(w, out string r2), r2);

        NotableLifecycle.Dies(ledger, w.Notables, defected);
        Assert.Equal(start - 1, People(w));                 // exactly one person left the world
        Assert.True(ConservationAuditor.IsConserved(w, out string r3), r3);

        Assert.Equal(-1, NotableLifecycle.LivingRowOf(w.Notables, new NotableId(3)));
    }

    [Fact]
    public void ANotableCannotBeMintedFromNothing_OnlyExtracted()
    {
        // Law 1 at the level that matters here: every notable traces to a bucket.
        // Promoting more people than the bucket holds must fail on the LAST one,
        // not silently create anybody.
        WorldState w = World(adults: 2);
        var ledger = new Ledger(w.LedgerFlows);
        int bucket = AdultBucketRow(w);

        Assert.True(NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(1), P1) >= 0);
        Assert.True(NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(2), P1) >= 0);
        Assert.Equal(-1, NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(3), P1));

        Assert.Equal(0, w.Buckets[bucket].Count.Value);
        Assert.Equal(2, People(w));
        Assert.Equal(2, w.Notables.Count); // the refused third left no row behind
    }

    [Fact]
    public void T48_ShipsNoBattleContract_TheResolverIsM6()
    {
        // Scope fence as a test so it cannot rot. m4-spec §1.6: "Strategic war is
        // AutoResolver ONLY at M4" — and the resolver itself is deferred to M6, so
        // T4.8's notables half must carry no battle vocabulary at all.
        // CODE, not prose: the file's own comments name BattleSetup/BattleOutcome
        // to say they are absent, and a fence that fired on its own documentation
        // would be measuring the wrong thing. Comment lines are stripped first.
        string source = System.IO.File.ReadAllText(RepoPath("Sim.Core/State/NotableLifecycle.cs"));
        var code = new System.Text.StringBuilder();
        foreach (string line in source.Split('\n'))
        {
            string t = line.TrimStart();
            if (t.StartsWith("//", StringComparison.Ordinal)) continue;
            code.Append(line).Append('\n');
        }
        string stripped = code.ToString();
        foreach (string banned in new[]
                 { "BattleSetup", "BattleOutcome", "Competence", "Experience",
                   "Prowess", "Morale", "Casualt" })
            Assert.DoesNotContain(banned, stripped, StringComparison.Ordinal);

        // The banned list is not vacuous: it really does fire on battle code.
        Assert.Contains("BattleSetup", "var setup = new BattleSetup();", StringComparison.Ordinal);

        // And the ROW itself carries identity, place, allegiance and the person —
        // nothing that could parameterize a resolver.
        var row = new NotableRow(new NotableId(1), S0, P1, 5, Conserved.Zero);
        Assert.Equal(5, row.CohortIdx);
        Assert.Equal(0, row.Count.Value);
    }

    private static string RepoPath(string relative)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return System.IO.Path.Combine(dir!.FullName, relative);
    }
}
