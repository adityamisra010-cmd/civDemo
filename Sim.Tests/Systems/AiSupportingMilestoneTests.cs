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
/// ADR-046 §6 (M6 review batch 3, 2026-10-23) — THE SUPPORTING-MILESTONE GOAL (AiResearchPolicy.SupportingGoalClosure,
/// research.json tuning.aiSupportingMilestones). The AI pursues, on purpose, the research supporting milestones its next Age
/// still needs (supportingRequired and minCategories read from ages.json through AgeQuery.Evaluate), each by its cheapest
/// remaining route; it never drops a prerequisite and researches nothing beyond the deficit. The selection rule is pinned
/// as a pure function (category preference, deficits, the (cost, listed position) key, tie-dense) and then on the shipped
/// content with the AI placed in Age III facing Age IV.
/// </summary>
public class AiSupportingMilestoneTests
{
    // D-063 (tranche 2): the subject is the ADR-046 §6 supporting goal, authored and pinned on the pre-tranche-2 content, and
    // its switch-off twin (TestConfigs.PreAiSupporting) is built on that content, so both arms run on it (R4).
    private static readonly SimConfig Cfg = TestConfigs.PreTranche2(TestConfigs.Sim());
    private static readonly ResearchContent Research = Cfg.Research!;
    private static readonly AgeContent Ages = Cfg.Ages!;
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private const AgeMilestoneCategory Tech = AgeMilestoneCategory.Technological, Mat = AgeMilestoneCategory.MaterialEconomic,
        Inst = AgeMilestoneCategory.InstitutionalSocial, Sys = AgeMilestoneCategory.Systemic, Mil = AgeMilestoneCategory.MilitaryRealization;

    private static int Bit(AgeMilestoneCategory c) => 1 << ((int)c - 1);

    // ------------------------------------------------------------------ the selection rule, as a pure function

    // Age IV's supporting list, in ages.json order: abjad [Tech], census [Inst], tools [Mat], workshops [Mat],
    // merchants [Inst], expansion [Sys], formation [Mil]. Only the research ones (0 and 1) can be candidates.
    private static readonly AgeMilestoneCategory[] AgeIv = [Tech, Inst, Mat, Mat, Inst, Sys, Mil];

    [Fact]
    public void Select_AnUncoveredCategory_BeatsACheaperCandidateInACoveredOne()
    {
        int[]?[] route = [[1], [2], [3], null, null, null, null];
        double[] cost = [500.0, 900.0, 100.0, 0, 0, 0, 0];   // candidate 2 is cheapest but Mat is covered
        int mask = Bit(Mat) | Bit(Sys);
        Assert.Equal([0], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, mask, needCount: 1, needCategories: 1));
        // no category short: the cheapest of all
        Assert.Equal([2], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, mask, needCount: 1, needCategories: 0));
    }

    [Fact]
    public void Select_CategoriesFirst_ThenTheCount_EachPickTheCheapest()
    {
        int[]?[] route = [[1], [2], null, null, [5], null, null];
        double[] cost = [500.0, 300.0, 0, 0, 200.0, 0, 0];
        int mask = Bit(Mat) | Bit(Sys);
        // one category short (Tech or Inst): Inst's cheapest is 4 (200) < Tech 500 — 4; then the count: 1 (300)
        Assert.Equal([4, 1], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, mask, needCount: 2, needCategories: 1));
        // two categories short: Inst (4), then Tech (0) — never a second Inst while Tech is uncovered
        Assert.Equal([4, 0], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, mask, needCount: 2, needCategories: 2));
    }

    [Fact]
    public void Select_NoDeficit_NoCandidate_NothingChosen()
    {
        int[]?[] route = [[1], [2], null, null, null, null, null];
        double[] cost = [5.0, 5.0, 0, 0, 0, 0, 0];
        Assert.Empty(AiResearchPolicy.SelectSupporting(AgeIv, route, cost, 0, needCount: 0, needCategories: 0));
        Assert.Empty(AiResearchPolicy.SelectSupporting(AgeIv, new int[]?[7], cost, 0, needCount: 3, needCategories: 3));
        // a category short with no research candidate in it: the count stage still runs
        int[]?[] techOnly = [[1], null, null, null, null, null, null];
        Assert.Equal([0], AiResearchPolicy.SelectSupporting(AgeIv, techOnly, cost, Bit(Tech), needCount: 1, needCategories: 1));
    }

    [Fact]
    public void Select_TieDense_BitEqualCosts_GoToTheMilestoneListedFirst()
    {
        int[]?[] route = [[1], [2], [3], [4], [5], [6], [7]];
        double[] cost = [250.0, 250.0, 250.0, 250.0, 250.0, 250.0, 250.0];
        Assert.Equal([0, 1, 2], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, 0, needCount: 3, needCategories: 0));
        // categories short: the first listed of each uncovered category in turn (Tech 0, then Inst 1, then Mat 2)
        Assert.Equal([0, 1, 2], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, 0, needCount: 3, needCategories: 3));
        // Tech covered: the first listed uncovered is Inst (1)
        Assert.Equal([1], AiResearchPolicy.SelectSupporting(AgeIv, route, cost, Bit(Tech), needCount: 1, needCategories: 1));
    }

    // ------------------------------------------------------------------ the shipped content: the AI in Age III

    private static WorldState AgeThree()
    {
        WorldState w = DevDuo.Value.Clone();
        Assert.Equal(0, w.AgeStates.Count);
        w.AgeStates.Add(new AgeStateRow(Rival, 3, 0, 0, 0));
        Assert.Equal(3, AgeQuery.CurrentAge(w, Ages, Rival));
        return w;
    }

    private static void Complete(WorldState w, string id) =>
        w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, Research.Nodes[Research.IndexOfId(id)].Key));

    private static int Count(bool[] mask)
    {
        int n = 0;
        foreach (bool b in mask) if (b) n++;
        return n;
    }

    [Fact]
    public void AgeIII_TheGoal_IsTheCheapestResearchSupportingMilestone_ItsWholeRoute_AndNothingElse()
    {
        WorldState w = AgeThree();
        AgeEligibilityReport report = AgeQuery.Evaluate(w, Ages, Rival)!;
        Assert.Equal(4, report.NextAge);
        Assert.True(report.SupportingMet < report.SupportingRequired || report.CategoriesCovered.Count < report.CategoriesRequired);

        var routes = new AiResearchPolicy.Routes(w, Research, Rival);
        int abjad = Research.IndexOfId("abjad"), census = Research.IndexOfId("census");
        int[] abjadRoute = routes.Node(abjad), censusRoute = routes.Node(census);
        bool[] goal = AiResearchPolicy.SupportingGoalClosure(w, Research, Ages, Rival);

        // Both research supporting milestones are candidates; the deficits at founding (nothing met) need at least two
        // milestones from three categories, so both are chosen — their routes, and nothing else.
        bool[] expected = new bool[Research.Nodes.Count];
        foreach (int i in abjadRoute) expected[i] = true;
        foreach (int i in censusRoute) expected[i] = true;
        Assert.Equal(expected, goal);
        Assert.True(goal[abjad] && goal[census], "the AI pursues Alphabetic script and Census deliberately");
        AssertSufficient(w, goal);
    }

    [Fact]
    public void AgeIII_OnlyTheDeficit_IsPursued_AndAMetMilestoneDropsOut()
    {
        WorldState w = AgeThree();
        int abjad = Research.IndexOfId("abjad"), census = Research.IndexOfId("census");
        bool[] before = AiResearchPolicy.SupportingGoalClosure(w, Research, Ages, Rival);
        Assert.True(before[abjad] && before[census]);

        // Alphabetic script met: it leaves the goal, Census (still needed) stays, abjad's own route is gone.
        foreach (int i in new AiResearchPolicy.Routes(w, Research, Rival).Node(abjad)) Complete(w, Research.Nodes[i].Id);
        bool[] after = AiResearchPolicy.SupportingGoalClosure(w, Research, Ages, Rival);
        Assert.False(after[abjad]);
        Assert.True(after[census]);
        AssertSufficient(w, after);
        Assert.True(Count(after) < Count(before));

        // Census met too: both research milestones are met; the rest of the deficit is not research work — empty goal.
        foreach (int i in new AiResearchPolicy.Routes(w, Research, Rival).Node(census)) Complete(w, Research.Nodes[i].Id);
        Assert.DoesNotContain(true, AiResearchPolicy.SupportingGoalClosure(w, Research, Ages, Rival));
    }

    [Fact]
    public void AgeIII_TheChoice_WorksTheSupportingGoal_WhenItIsTheCheapest_AndNotWithTheSwitchOff()
    {
        // Isolate the goal: no road, tax or university goals; the core (Ironworking) met.
        SimConfig only = Cfg with { Roads = null, Governance = null, Institutions = null };
        WorldState w = AgeThree();
        foreach (int i in new AiResearchPolicy.Routes(w, Research, Rival).Node(Research.IndexOfId("iron_bloomery")))
            Complete(w, Research.Nodes[i].Id);
        bool[] completed = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] available = ResearchQuery.AvailableMask(Research, completed);

        bool[][] goals = AiResearchPolicy.Goals(w, only, Rival);
        Assert.Equal(AiResearchPolicy.GoalCount, goals.Length);
        Assert.DoesNotContain(true, goals[AiResearchPolicy.CoreGoal]);
        Assert.Contains(true, goals[AiResearchPolicy.SupportingGoal]);
        int chosen = AiResearchPolicy.Choose(w, only, Rival, completed, available);
        Assert.True(goals[AiResearchPolicy.SupportingGoal][chosen] && available[chosen],
            Research.Nodes[chosen].Id + " is not supporting-goal work");

        // The switch off (TestConfigs.PreAiSupporting): no supporting goal; the policy falls back to the cheapest anywhere.
        SimConfig off = TestConfigs.PreAiSupporting(Cfg) with { Roads = null, Governance = null, Institutions = null };
        Assert.DoesNotContain(true, AiResearchPolicy.Goals(w, off, Rival)[AiResearchPolicy.SupportingGoal]);
        Assert.Equal(AiResearchPolicy.Cheapest(w, Research, Rival, available, null), AiResearchPolicy.Choose(w, off, Rival, completed, available));
    }

    /// <summary>D-063: the same choice on the SHIPPED tranche-2 content — at Age III with Ironworking met and no road, tax
    /// or university goal, the AI works supporting-goal research.</summary>
    [Fact]
    public void AgeIII_OnTheShippedContent_TheChoice_WorksTheSupportingGoal()
    {
        SimConfig shipped = TestConfigs.Sim() with { Roads = null, Governance = null, Institutions = null };
        ResearchContent r = shipped.Research!;
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Sim(), 42);
        w.AgeStates.Add(new AgeStateRow(Rival, 3, 0, 0, 0));
        Assert.Equal(3, AgeQuery.CurrentAge(w, shipped.Ages!, Rival));
        foreach (int i in new AiResearchPolicy.Routes(w, r, Rival).Node(r.IndexOfId("iron_bloomery")))
            w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, r.Nodes[i].Key));
        bool[] completed = ResearchQuery.CompletedMask(w, r, Rival);
        bool[] available = ResearchQuery.AvailableMask(r, completed);
        bool[][] goals = AiResearchPolicy.Goals(w, shipped, Rival);
        Assert.DoesNotContain(true, goals[AiResearchPolicy.CoreGoal]);
        Assert.Contains(true, goals[AiResearchPolicy.SupportingGoal]);
        int chosen = AiResearchPolicy.Choose(w, shipped, Rival, completed, available);
        Assert.True(goals[AiResearchPolicy.SupportingGoal][chosen] && available[chosen], r.Nodes[chosen].Id + " is not supporting-goal work");
    }

    [Fact]
    public void TheGoal_NeedsTheCheapestRoute_AndIsEmptyAtFounding_WhenNothingIsShort()
    {
        // With the route closure off the supporting goal is inert (it is costed by the routes).
        WorldState w = AgeThree();
        SimConfig routesOff = TestConfigs.PreAiRoutes(Cfg);
        Assert.DoesNotContain(true, AiResearchPolicy.SupportingGoalClosure(w, routesOff.Research!, Ages, Rival));
        // At the final Age there is no next Age: empty.
        WorldState last = DevDuo.Value.Clone();
        last.AgeStates.Add(new AgeStateRow(Rival, AgeContent.AgeCount, 0, 0, 0));
        Assert.DoesNotContain(true, AiResearchPolicy.SupportingGoalClosure(last, Research, Ages, Rival));
    }

    /// <summary>Every goal node is unknown, and its own prerequisite is met inside completed ∪ goal — no required
    /// prerequisite is dropped.</summary>
    private static void AssertSufficient(WorldState w, bool[] goal)
    {
        bool[] done = ResearchQuery.CompletedMask(w, Research, Rival);
        bool[] have = (bool[])done.Clone();
        for (int i = 0; i < goal.Length; i++) if (goal[i]) have[i] = true;
        for (int i = 0; i < goal.Length; i++)
        {
            if (!goal[i]) continue;
            Assert.False(done[i], Research.Nodes[i].Id + " is completed but in the goal");
            Assert.True(Research.Nodes[i].Prerequisite is null || ResearchQuery.RequirementMet(Research, Research.Nodes[i].Prerequisite!, have),
                Research.Nodes[i].Id + "'s prerequisite is not met inside completed ∪ goal");
        }
    }
}
