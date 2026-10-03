using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// R3 (Director R2-final §0A, §2, §4, §21 KNOWLEDGE) — KNOWLEDGE NEVER DECAYS.
///
/// A revolt founds a NEW AI polity holding a complete copy of its former polity's completed knowledge at the instant
/// of separation; afterwards the two research independently. Annexation is a union. Both go through the one domain
/// operation <see cref="KnowledgeTransfer.MergeInto"/>; revolt is exercised through the real RevoltSystem and the
/// real ResearchSystem (the production pipeline order: revolt before research).
/// </summary>
public class KnowledgeMonotonicTests
{
    private static readonly ResearchContent Rig = Standard().Load();
    private static readonly PolityId P1 = new(Player);
    private static readonly PolityId P2 = new(2);   // the polity the revolt founds (one above the roster)

    // a b c d e (trunk) + law (civic): the parent's knowledge at separation.
    private static readonly int[] ParentKnowledge = [1, 2, 3, 4, 5, 1001];

    /// <summary>Two settlements of 10 000 adults under the player; settlement 1 is destitute (unfed and unhoused —
    /// the revolt corner), settlement 0 comfortable. The player knows <see cref="ParentKnowledge"/> and has some
    /// in-progress research.</summary>
    private static WorldState Split()
    {
        WorldState w = World([Player], [Player, Player], 10_000);
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < 2; i++)
        {
            var s = new SettlementId(i);
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, i == 1 ? 1.0 : 0.0, 10_000));
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (i == 0)
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, 5_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        WithCompleted(w, ParentKnowledge);
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(6), 700.0));   // effort, not knowledge
        return w;
    }

    private static TurnExecutor RevoltThenResearch(OrderLog? orders = null)
    {
        SimConfig cfg = TestConfigs.Sim() with { Research = Rig };
        return new(FlatEra(10.0), [SystemCatalog.Revolt(cfg), SystemCatalog.Research(cfg)], orders);
    }

    private static int[] Known(IReadOnlyWorldState w, PolityId p)
    {
        var keys = new List<int>();
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity == p) keys.Add(w.ResearchCompleted[i].Node.Value);
        keys.Sort();
        return [.. keys];
    }

    [Fact]
    public void Revolt_FoundsANewAiPolity_WithTheCompleteParentKnowledge_AndTheParentLosesNothing()
    {
        WorldState w = RevoltThenResearch().Step(Split());

        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId founded));
        Assert.Equal(P2, founded);
        Assert.True(EmpireQuery.TryGetCommandSource(w, P2, out CommandSource source));
        Assert.Equal(CommandSource.Ai, source);
        Assert.Equal(ParentKnowledge, Known(w, P1));     // no knowledge is lost on revolt
        Assert.Equal(ParentKnowledge, Known(w, P2));     // complete copy: no filtering, no subset
        // Knowledge = completed nodes: in-progress effort stays with the parent (INFERRED, R3).
        Assert.Equal(700.0, Progress(w, 6, Player));
        Assert.Equal(0.0, Progress(w, 6, 2));
    }

    [Fact]
    public void AfterSeparation_TheTwoPolitiesResearchIndependently_NothingFlowsEitherWay()
    {
        // Parent researches mil (6); the new polity researches med (7). Each completes its own node and never
        // receives the other's.
        var orders = new OrderLog();
        orders.Append(Target(0, 6));
        orders.Append(Target(1, 7, polity: 2));   // the new polity is on the roster from world 1
        TurnExecutor ex = RevoltThenResearch(orders);
        WorldState w = Run(ex, Split(), 8);

        Assert.True(Done(w, 6, Player));
        Assert.True(Done(w, 7, 2));
        Assert.False(Done(w, 7, Player), "the city-state's research must not appear in the parent");
        Assert.False(Done(w, 6, 2), "the parent's post-separation research must not appear in the city-state");
        Assert.Equal([1, 2, 3, 4, 5, 6, 1001], Known(w, P1));
        Assert.Equal([1, 2, 3, 4, 5, 7, 1001], Known(w, P2));
    }

    [Fact]
    public void Annexation_IsAUnion_TheConquerorKeepsItsOwnAndGainsTheAnnexedPolitysExtra()
    {
        var table = new Table<ResearchCompletedRow>();
        var conqueror = new PolityId(1);
        var cityState = new PolityId(2);
        foreach (int k in new[] { 1, 2, 3, 4, 5 }) table.Add(new ResearchCompletedRow(conqueror, new ResearchNodeId(k)));
        foreach (int k in new[] { 1, 2, 3, 4, 5, 7 }) table.Add(new ResearchCompletedRow(cityState, new ResearchNodeId(k)));

        Assert.Equal(1, KnowledgeTransfer.MergeInto(table, cityState, conqueror));
        var w = new WorldState(1);
        for (int i = 0; i < table.Count; i++) w.ResearchCompleted.Add(table[i]);
        Assert.Equal([1, 2, 3, 4, 5, 7], Known(w, conqueror));
        Assert.Equal([1, 2, 3, 4, 5, 7], Known(w, cityState));   // nothing deleted before or after the merge

        // Idempotent and monotone: merging again adds nothing; a holder merged into itself is a no-op.
        Assert.Equal(0, KnowledgeTransfer.MergeInto(table, cityState, conqueror));
        Assert.Equal(0, KnowledgeTransfer.MergeInto(table, conqueror, conqueror));
        Assert.Equal(12, table.Count);
    }

    [Fact]
    public void Annexation_OfAPolityKnowingLess_RollsNothingBack()
    {
        var table = new Table<ResearchCompletedRow>();
        var conqueror = new PolityId(1);
        var annexed = new PolityId(2);
        foreach (int k in new[] { 1, 2, 3, 4, 5, 6 }) table.Add(new ResearchCompletedRow(conqueror, new ResearchNodeId(k)));
        table.Add(new ResearchCompletedRow(annexed, new ResearchNodeId(1)));
        Assert.Equal(0, KnowledgeTransfer.MergeInto(table, annexed, conqueror));
        Assert.Equal(7, table.Count);
    }

    [Fact]
    public void BothCases_SurviveSaveLoad_BitExact()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 6));
        orders.Append(Target(1, 7, polity: 2));
        WorldState w = Run(RevoltThenResearch(orders), Split(), 8);
        // and an annexation-style union on top, so the merged rows are in the saved state too
        KnowledgeTransfer.MergeInto(w.ResearchCompleted, P2, P1);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(w), ms.Length);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(w, back));
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(back));
        Assert.Equal(Known(w, P1), Known(back, P1));
        Assert.Equal(Known(w, P2), Known(back, P2));
        Assert.Equal(2, back.Polities.Count);
    }

    [Fact]
    public void BothCases_ReplayIdentically_AndALoadedWorldContinuesLikeTheLiveOne()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 6));
        orders.Append(Target(1, 7, polity: 2));
        WorldState a = Run(RevoltThenResearch(orders), Split(), 8);
        WorldState b = Run(RevoltThenResearch(orders), Split(), 8);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));

        // Save right after the separation (world 1), load, and continue to world 8.
        TurnExecutor live = RevoltThenResearch(orders);
        WorldState w1 = live.Step(Split());
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w1, writer);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState resumed = Run(RevoltThenResearch(orders), CanonicalSchema.Read(reader), 7);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(resumed));
    }

    [Fact]
    public void KnowledgeNeverDecays_OverAThousandYears_UnderEveryTransition()
    {
        // 100 turns at dt 10 with the revolt in the first; the completed set of every holder only ever grows.
        var orders = new OrderLog();
        orders.Append(Target(0, 6));
        orders.Append(Target(1, 7, polity: 2));
        TurnExecutor ex = RevoltThenResearch(orders);
        WorldState w = Split();
        int[] p1 = Known(w, P1), p2 = Known(w, P2);
        for (int t = 0; t < 100; t++)
        {
            w = ex.Step(w);
            int[] n1 = Known(w, P1), n2 = Known(w, P2);
            foreach (int k in p1) Assert.Contains(k, n1);
            foreach (int k in p2) Assert.Contains(k, n2);
            (p1, p2) = (n1, n2);
        }
    }
}
