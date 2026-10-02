using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-031 — Age eligibility, explicit advancement, next-turn transition, surge plumbing and
/// the AI policy (D-047 rulings 12-14), on the FOUNDED dev world with the canonical
/// ages.json. Milestone facts are injected as the state the owning systems would publish
/// (completed-research rows, a formation removed), so each test states exactly which facts
/// hold. Most tests run ONLY the two Age systems, so nothing else moves between turns; the
/// non-retroactivity and determinism tests run the full production pipeline.
/// </summary>
public class AgeProgressionTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> FoundedSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> FoundedDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Solo() => FoundedSolo.Value.Clone();
    private static WorldState Duo() => FoundedDuo.Value.Clone();

    private static TurnExecutor AgeOnly(OrderLog? orders = null) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.AgeEligibility(Cfg), SystemCatalog.AgeTransition(Cfg)], orders);

    private static TurnExecutor Production(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    private static WorldState Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
        {
            int index = Research.IndexOfId(id);
            Assert.True(index >= 0, id);
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[index].Key));
        }
        return w;
    }

    /// <summary>A2 entry holds: core cereal_cultivation; supporting pottery (Technological) and the
    /// founding warband (Military Realization) — 2 of 2 required, 2 categories of 2.</summary>
    private static WorldState EligibleForA2(WorldState w, PolityId polity) =>
        Complete(w, polity, "cereal_cultivation", "pottery_open_fired");

    private static OrderRecord Advance(long turn, PolityId polity, int toAge, double surge) =>
        OrderRecord.From(turn, polity, OrderKind.AdvanceAge, toAge, surge);

    private static T[] Rows<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    private static int AgeOf(IReadOnlyWorldState w, PolityId p) => AgeQuery.CurrentAge(w, Ages, p);

    // ------------------------------------------------------------------ content and founding

    [Fact]
    public void Content_NineAges_FiveCategories_FoundingAgeIsA1_EveryEnteredAgeHasCoreAndCoverage()
    {
        Assert.Equal(9, Ages.Ages.Count);
        Assert.Equal(["Prehistoric / Stone Age", "Neolithic / Agricultural", "Bronze Age", "Iron Age", "Classical / Imperial",
            "Medieval", "Early Modern", "Industrial", "Modern / Contemporary"], Ages.Ages.Select(a => a.Name));
        Assert.Equal(5, Ages.Categories.Count);
        Assert.Equal(1, Ages.FoundingAge); // D-044 T7: no node complete at founding — the Stone Age.
        Assert.Null(Ages.Age(1).Entry);
        for (int a = 2; a <= 9; a++)
        {
            AgeEntryRequirements entry = Ages.Age(a).Entry!;
            Assert.NotEmpty(entry.Core);
            Assert.True(entry.MinCategories >= 2, $"A{a}: coverage must span at least two categories (D-043 A2)");
            // Every Age offers a Military Realization supporting milestone, and it reads formations — never research.
            Assert.Contains(entry.Supporting, m => m.Category == AgeMilestoneCategory.MilitaryRealization);
            foreach (AgeMilestone m in entry.Supporting.Concat(entry.Core))
                if (m.Category == AgeMilestoneCategory.MilitaryRealization) Assert.Equal(MilestoneFactKind.Formations, m.Fact.Kind);
        }
        Assert.True(Ages.Surges.Count >= 2);
    }

    [Fact]
    public void Founding_EveryEmpireStartsInA1_WithNoAgeRows_AndOneWarbandAtItsCapital()
    {
        WorldState w = Duo();
        Assert.Equal(0, w.AgeStates.Count);
        Assert.Equal(1, AgeOf(w, Player));
        Assert.Equal(1, AgeOf(w, Rival));
        Assert.Equal(2, w.MilitaryUnits.Count);
        foreach (PolityId p in new[] { Player, Rival })
        {
            MilitaryUnitRow unit = Assert.Single(MilitaryQuery.Units(w, p));
            Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId capital));
            Assert.Equal(capital, unit.Location);
            Assert.Equal("warband", TestConfigs.UnitFamilies().IdentityByKey(unit.Identity)!.Id);
            Assert.Equal(0.0, unit.Experience);
            Assert.Equal(0, unit.Army);
        }
        Assert.Equal([1, 2], Rows(w.MilitaryUnits).Select(u => u.Id));
    }

    [Fact]
    public void Founding_FormationsAreTokens_PopulationAndGoodsAreExactlyThoseOfAWorldWithoutUnitFamilies()
    {
        // Law 1: founding a formation draws no person and no good. Same seed, same config minus the
        // unit-family content: every conserved table is identical.
        WorldState with = Solo();
        WorldState without = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg with { UnitFamilies = null }, 42);
        Assert.Equal(0, without.MilitaryUnits.Count);
        Assert.True(WorldStates.TableEquals(with.Buckets, without.Buckets));
        Assert.True(WorldStates.TableEquals(with.GoodStocks, without.GoodStocks));
        Assert.True(WorldStates.TableEquals(with.LedgerFlows, without.LedgerFlows));
    }

    // ------------------------------------------------------------------ eligibility

    [Fact]
    public void Founding_NoEmpireIsEligible_OnlyTheWarbandMilestoneHolds_OnTheDevAndTheCanonicalWorld()
    {
        // The provisional A2 thresholds sit above the founding endowment (grain stores, dwellings,
        // population), so founding satisfies exactly one supporting milestone: the warband.
        foreach (WorldState w in new[] { Solo(), WorldFounding.Found(TestConfigs.Worldgen(), Cfg, 42) })
        {
            AgeEligibilityReport r = AgeQuery.Evaluate(w, Ages, Player)!;
            Assert.Equal(0, r.CoreMet);
            Assert.Equal(1, r.SupportingMet);
            Assert.Equal([AgeMilestoneCategory.MilitaryRealization], r.CategoriesCovered);
            Assert.False(r.Eligible);
        }
    }

    [Fact]
    public void Eligibility_CoreAndSupportingAndCoverageHold_IsEligible()
    {
        WorldState w = EligibleForA2(Solo(), Player);
        AgeEligibilityReport r = AgeQuery.Evaluate(w, Ages, Player)!;
        Assert.Equal(1, r.CurrentAge);
        Assert.Equal(2, r.NextAge);
        Assert.Equal(1, r.CoreMet);
        Assert.Equal(1, r.CoreTotal);
        Assert.Equal(2, r.SupportingMet); // pottery + warband
        Assert.Equal([AgeMilestoneCategory.Technological, AgeMilestoneCategory.MilitaryRealization], r.CategoriesCovered);
        Assert.Equal(0b10001, r.CategoryMask);
        Assert.True(r.Eligible);
        Assert.Empty(r.Remaining);
        Assert.True(AgeQuery.IsEligible(w, Ages, Player));
    }

    [Fact]
    public void Eligibility_MissingCore_IsNotEligible_AndSaysWhy()
    {
        WorldState w = Complete(Solo(), Player, "pottery_open_fired", "sheep_goat");
        AgeEligibilityReport r = AgeQuery.Evaluate(w, Ages, Player)!;
        Assert.Equal(0, r.CoreMet);
        Assert.Equal(3, r.SupportingMet);
        Assert.False(r.Eligible);
        Assert.Equal(["Core: Cultivated cereals"], r.Remaining);
    }

    [Fact]
    public void Eligibility_EnoughSupportingButOneCategory_IsNotEligible_CoverageIsEnforced()
    {
        // Two TECHNOLOGICAL supporting milestones and no formation: 2 of 2 supporting, but 1 of 2
        // categories — "the player cannot satisfy an Age merely by accumulating one easy category".
        WorldState w = Complete(Solo(), Player, "cereal_cultivation", "pottery_open_fired", "sheep_goat");
        w.MilitaryUnits.Clear();
        AgeEligibilityReport r = AgeQuery.Evaluate(w, Ages, Player)!;
        Assert.Equal(1, r.CoreMet);
        Assert.Equal(2, r.SupportingMet);
        Assert.Equal([AgeMilestoneCategory.Technological], r.CategoriesCovered);
        Assert.False(r.Eligible);
        Assert.Equal(["Category coverage: 1 of 2 categories"], r.Remaining);
    }

    [Fact]
    public void Eligibility_TooFewSupporting_IsNotEligible()
    {
        WorldState w = Complete(Solo(), Player, "cereal_cultivation"); // core + the warband only
        AgeEligibilityReport r = AgeQuery.Evaluate(w, Ages, Player)!;
        Assert.Equal(1, r.SupportingMet);
        Assert.False(r.Eligible);
        Assert.Contains("Supporting milestones: 1 of 2 required", r.Remaining);
    }

    [Fact]
    public void Eligibility_MilitaryRealization_ReadsRealFormations_NotResearchedMilitaryTechnology()
    {
        // Researching military technology (hafting, the spear unit's node; bow_simple) satisfies no
        // Military Realization milestone; only an owned formation does (ledger §10.4).
        WorldState w = Complete(Solo(), Player, "hafting", "bow_simple", "sling");
        w.MilitaryUnits.Clear();
        AgeMilestone military = Ages.Age(2).Entry!.Supporting.Single(m => m.Category == AgeMilestoneCategory.MilitaryRealization);
        Assert.False(AgeQuery.Milestone(w, Player, military).Met);
        w.MilitaryUnits.Add(new MilitaryUnitRow(5, Player, 3, 301, new SettlementId(0), 0, 0, 0, 0));
        Assert.True(AgeQuery.Milestone(w, Player, military).Met);
        // Another polity's formation is not ours.
        w.MilitaryUnits[0] = w.MilitaryUnits[0] with { Owner = Rival };
        Assert.False(AgeQuery.Milestone(w, Player, military).Met);
    }

    [Fact]
    public void Eligibility_IsPublishedEachStep_ByTheThinEvaluator_MatchingTheQuery()
    {
        WorldState w0 = EligibleForA2(Duo(), Player);
        WorldState w1 = AgeOnly().Step(w0);
        Assert.Equal(2, w1.AgeEligibility.Count); // one row per roster polity, roster order
        AgeEligibilityRow p = w1.AgeEligibility[0], r = w1.AgeEligibility[1];
        Assert.Equal(Player, p.Polity);
        Assert.Equal(2, p.NextAge);
        Assert.Equal(0, p.EvaluatedTurn);
        Assert.True(p.Eligible);
        AgeEligibilityReport live = AgeQuery.Evaluate(w0, Ages, Player)!;
        Assert.Equal((live.CoreMet, live.CoreTotal, live.SupportingMet, live.SupportingRequired, live.CategoryMask, live.CategoriesRequired),
            (p.CoreMet, p.CoreTotal, p.SupportingMet, p.SupportingRequired, p.CategoryMask, p.CategoriesRequired));
        Assert.Equal(Rival, r.Polity);
        Assert.False(r.Eligible);
    }

    // ------------------------------------------------------------------ explicit advancement, next-turn pin

    [Fact]
    public void AdvanceAge_WhenIneligible_IsRejected_NothingChanges()
    {
        WorldState w0 = Complete(Solo(), Player, "cereal_cultivation"); // not eligible
        Assert.Equal(AdvanceRejection.NotEligible, AgeQuery.CheckAdvance(w0, Ages, Player, 2, 1));
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 1));
        WorldState w1 = AgeOnly(orders).Step(w0);
        Assert.Equal(1, AgeOf(w1, Player));
        Assert.Equal(0, w1.AgeStates.Count);
        Assert.Equal(0, w1.AgeTransitions.Count);
        Assert.Equal(0, w1.UnitConversions.Count);
    }

    [Fact]
    public void AdvanceAge_StampedTurnT_TheStateOfTurnTPlus1_IsTheFirstInTheNewAge_TurnExact()
    {
        // Order stamped turn 3 (the player's decision during turn 3). States 0..3 are A1; state 4 is
        // the first in A2, EnteredTurn 4; the decision is recorded with DecisionTurn 3.
        var orders = new OrderLog();
        orders.Append(Advance(3, Player, 2, 4));
        TurnExecutor ex = AgeOnly(orders);
        WorldState w = EligibleForA2(Solo(), Player);
        var ages = new List<int> { AgeOf(w, Player) };
        for (int t = 0; t < 6; t++) { w = ex.Step(w); ages.Add(AgeOf(w, Player)); }
        Assert.Equal([1, 1, 1, 1, 2, 2, 2], ages);
        AgeStateRow row = AgeQuery.StateRow(w, Player)!.Value;
        Assert.Equal(new AgeStateRow(Player, 2, 4, 4, 4), row);
        Assert.Equal(new AgeTransitionRow(Player, 1, 2, 4, 3, 4), Assert.Single(Rows(w.AgeTransitions)));
    }

    [Fact]
    public void AdvanceAge_IsNotRetroactive_EveryOtherTableOfTheDecidingStepIsIdenticalToTheRunWithoutIt()
    {
        // Full production pipeline. The step that consumes the order resolves its turn entirely under
        // the old Age: research (no RP overflow, no progress moved), population, goods, everything
        // except the Age/military tables is bit-identical to the same step without the order.
        WorldState w0 = EligibleForA2(Solo(), Player);
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 1));
        WorldState with = Production(orders).Step(w0);
        WorldState without = Production().Step(w0);
        Assert.Equal(2, AgeOf(with, Player));
        Assert.Equal(1, AgeOf(without, Player));
        Assert.True(WorldStates.TableEquals(with.ResearchProgress, without.ResearchProgress));
        Assert.True(WorldStates.TableEquals(with.ResearchCompleted, without.ResearchCompleted));
        Assert.True(WorldStates.TableEquals(with.ResearchCredits, without.ResearchCredits));
        Assert.Equal(WorldHash.ComputeHex(StripAge(with)), WorldHash.ComputeHex(StripAge(without)));
    }

    private static WorldState StripAge(WorldState w)
    {
        WorldState c = w.Clone();
        c.AgeStates.Clear();
        c.AgeEligibility.Clear();
        c.AgeTransitions.Clear();
        c.MilitaryUnits.Clear();
        c.UnitConversions.Clear();
        return c;
    }

    [Fact]
    public void Eligible_WithoutAnOrder_NeverAdvances_ThePlayerMayDelayIndefinitely()
    {
        TurnExecutor ex = AgeOnly();
        WorldState w = EligibleForA2(Solo(), Player);
        for (int t = 0; t < 12; t++) w = ex.Step(w);
        Assert.Equal(1, AgeOf(w, Player));
        Assert.Equal(0, w.AgeTransitions.Count);
        Assert.True(w.AgeEligibility[0].Eligible); // eligible the whole time, still in A1
        // ...and advancing after the delay still works: the order is valid on turn 12.
        var orders = new OrderLog();
        orders.Append(Advance(12, Player, 2, 2));
        Assert.Equal(2, AgeOf(AgeOnly(orders).Step(w), Player));
    }

    [Fact]
    public void AdvanceAge_IsIrreversible_AndCannotSkip_OnlyCurrentPlusOneIsAccepted()
    {
        WorldState w = EligibleForA2(Solo(), Player);
        Assert.Equal(AdvanceRejection.WrongTargetAge, AgeQuery.CheckAdvance(w, Ages, Player, 3, 1)); // skip
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 1));
        orders.Append(Advance(1, Player, 2, 1));   // re-entering the current Age
        orders.Append(Advance(2, Player, 3, 1));   // A3 — not eligible
        TurnExecutor ex = AgeOnly(orders);
        w = ex.Step(w);
        Assert.Equal(2, AgeOf(w, Player));
        Assert.Equal(AdvanceRejection.WrongTargetAge, AgeQuery.CheckAdvance(w, Ages, Player, 2, 1));
        w = ex.Step(ex.Step(w));
        Assert.Equal(2, AgeOf(w, Player));
        Assert.Single(Rows(w.AgeTransitions));
        // No order kind lowers an Age: TargetId below 2 does not even load.
        Assert.Throws<SnapshotFormatException>(() => RoundTrip(Advance(0, Player, 1, 1)));
    }

    [Fact]
    public void AdvanceAge_TwoOrdersInOneTurn_TheFirstValidWins_InLogOrder()
    {
        WorldState w = EligibleForA2(Solo(), Player);
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 99)); // unknown surge: invalid, skipped
        orders.Append(Advance(0, Player, 2, 3));
        orders.Append(Advance(0, Player, 2, 5));
        w = AgeOnly(orders).Step(w);
        Assert.Equal(3, AgeQuery.StateRow(w, Player)!.Value.Surge);
        Assert.Single(Rows(w.AgeTransitions));
    }

    [Fact]
    public void Surge_ChoiceAndStartTurnAreStored_AndQueryable_NoNumericEffectExists()
    {
        WorldState w0 = EligibleForA2(Solo(), Player);
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 3));
        WorldState w1 = AgeOnly(orders).Step(w0);
        AgeStatus status = AgeQuery.Status(w1, Ages, Player);
        Assert.Equal(2, status.Current.Key);
        Assert.Equal(3, status.Next!.Key);
        Assert.Equal("plenty", status.Surge!.Id);
        Assert.Equal(1, status.SurgeStartTurn);
        Assert.Equal(1, status.EnteredTurn);
        Assert.Same(Ages.Surges, AgeQuery.SurgeOptions(Ages));
        foreach (AgeSurge s in Ages.Surges)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.Domain));
            Assert.NotEmpty(s.DownstreamSystems);
        }
    }

    [Fact]
    public void PendingAdvance_IsTheOrderTheNextStepApplies()
    {
        WorldState w0 = EligibleForA2(Solo(), Player);
        Assert.Null(AgeQuery.PendingAdvance(w0, Ages, [], Player));
        OrderRecord stale = Advance(-1, Player, 2, 1);
        OrderRecord bad = Advance(0, Player, 2, 99);
        OrderRecord good = AgeQuery.AdvanceOrder(w0, Player, 2, 6);
        PendingAgeAdvance p = AgeQuery.PendingAdvance(w0, Ages, [stale, bad, good], Player)!;
        Assert.Equal((1, 2, "arms", 0L, 1L), (p.FromAge, p.ToAge, p.Surge.Id, p.DecisionTurn, p.EffectiveTurn));
        var orders = new OrderLog();
        orders.Append(good);
        WorldState w1 = AgeOnly(orders).Step(w0);
        Assert.Equal(new AgeStateRow(Player, 2, 1, 6, 1), AgeQuery.StateRow(w1, Player)!.Value);
    }

    // ------------------------------------------------------------------ AI

    [Fact]
    public void Ai_AdvancesWhenEligible_ThroughTheSameOrderPathway_PlayerIsNeverAutoAdvanced()
    {
        WorldState w = EligibleForA2(EligibleForA2(Duo(), Player), Rival);
        OrderRecord ai = Assert.Single(AgeAdvancePolicy.OrdersForAi(w, Ages));
        Assert.Equal((Rival.Value, OrderKind.AdvanceAge, 2, (double)Ages.Surges[0].Key, 0L),
            (ai.ActorId, ai.Kind, ai.TargetId, ai.Amount, ai.Turn));
        Assert.Null(AgeAdvancePolicy.Decide(w, Ages, Player)); // the player decides for themself

        var orders = new OrderLog();
        TurnExecutor ex = AgeOnly(orders);
        for (int t = 0; t < 3; t++)
        {
            foreach (OrderRecord o in AgeAdvancePolicy.OrdersForAi(w, Ages)) orders.Append(o);
            w = ex.Step(w);
        }
        Assert.Equal(2, AgeOf(w, Rival));
        Assert.Equal(1, AgeOf(w, Player));
        Assert.Equal(1, w.AgeTransitions[0].EffectiveTurn);
        // In A2 the AI is not eligible for A3, so it issues nothing more.
        Assert.Empty(AgeAdvancePolicy.OrdersForAi(w, Ages));
    }

    [Fact]
    public void Ai_NotEligible_IssuesNothing()
    {
        WorldState w = Duo();
        Assert.Empty(AgeAdvancePolicy.OrdersForAi(w, Ages));
    }

    // ------------------------------------------------------------------ determinism, replay, research isolation

    [Fact]
    public void FullPipeline_AdvanceAndModernize_ReplaysHashForHash_AndSurvivesSaveLoad()
    {
        WorldState start = EligibleForA2(Solo(), Player);
        var orders = new OrderLog();
        orders.Append(Advance(1, Player, 2, 2));
        WorldState a = start, b = start;
        TurnExecutor ea = Production(orders), eb = Production(orders);
        for (int t = 0; t < 2; t++) { a = ea.Step(a); b = eb.Step(b); }
        // Save at turn 2, load, continue: identical to the uninterrupted run.
        using var ms = new MemoryStream();
        Snapshot.Save(b, ms);
        ms.Position = 0;
        b = Snapshot.Load(ms, b.Terrain);
        for (int t = 0; t < 2; t++) { a = ea.Step(a); b = eb.Step(b); }
        Assert.Equal(2, AgeOf(a, Player));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.True(WorldStates.StateEquals(a, b));
    }

    [Fact]
    public void AgeTransition_ConstructsNothing_NoFormationNoStructureNoGood_AndResearchCompletionStillConstructsNothing()
    {
        WorldState w0 = EligibleForA2(Solo(), Player);
        var orders = new OrderLog();
        orders.Append(Advance(0, Player, 2, 5));
        WorldState w1 = Production(orders).Step(w0);
        Assert.Equal(w0.MilitaryUnits.Count, w1.MilitaryUnits.Count);
        Assert.Equal(StructureCount(Production().Step(w0)), StructureCount(w1));
        // Research completion constructs nothing either (D-044 R14): completing a unit's node adds no formation.
        WorldState r = Complete(w1.Clone(), Player, "hafting", "bow_simple");
        WorldState r1 = AgeOnly().Step(r);
        Assert.Equal(w1.MilitaryUnits.Count, r1.MilitaryUnits.Count);
    }

    private static long StructureCount(IReadOnlyWorldState w)
    {
        long n = 0;
        for (int i = 0; i < w.Structures.Count; i++) n += w.Structures[i].Count;
        return n;
    }

    // ------------------------------------------------------------------ order load validation

    private static void RoundTrip(OrderRecord record)
    {
        var log = new OrderLog();
        log.Append(record);
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog.Load(ms);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(10, 1.0)]
    [InlineData(2, 0.0)]
    [InlineData(2, 1.5)]
    [InlineData(2, double.NaN)]
    public void AdvanceAge_MalformedPayload_IsRejectedAtLoad(int target, double surge)
    {
        Assert.Throws<SnapshotFormatException>(() => RoundTrip(Advance(0, Player, target, surge)));
    }

    [Fact]
    public void AdvanceAge_WellFormedPayload_Loads()
    {
        RoundTrip(Advance(0, Player, 9, 6));
    }

    // ------------------------------------------------------------------ content validation

    [Fact]
    public void AgesJson_MilitaryRealizationBackedByResearch_IsRejected()
    {
        string json = AgesJson().Replace(
            "\"category\": \"military_realization\", \"fact\": { \"kind\": \"formations\", \"min\": 1 }",
            "\"category\": \"military_realization\", \"fact\": { \"kind\": \"research\", \"nodes\": [\"hafting\"] }");
        var e = Assert.Throws<AgeContentException>(() => Load(json));
        Assert.Contains("Military Realization must measure real formations", e.Message);
    }

    [Fact]
    public void AgesJson_UnknownNode_IsRejected()
    {
        var e = Assert.Throws<AgeContentException>(() => Load(AgesJson().Replace("\"cereal_cultivation\"", "\"cereal_cultivashun\"")));
        Assert.Contains("cereal_cultivashun", e.Message);
    }

    [Fact]
    public void AgesJson_UnachievableCoverage_IsRejected()
    {
        var e = Assert.Throws<AgeContentException>(() => Load(AgesJson().Replace(
            "\"supportingRequired\": 2,\n        \"minCategories\": 2", "\"supportingRequired\": 2,\n        \"minCategories\": 3")));
        Assert.Contains("minCategories", e.Message);
    }

    private static AgeContent Load(string json) =>
        AgeContentLoader.Load(json, Research, Cfg.Goods, Cfg.Registries, TestConfigs.UnitFamilies());

    private static string AgesJson()
    {
        using var s = Sim.Data.DataFiles.OpenAges();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
