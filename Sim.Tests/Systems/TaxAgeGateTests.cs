using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Governance;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// H2 (Director 2026-10-05 §7, RATIFIED in docs/d049-taxation-and-revolt-model.md) — TAXATION IS AN A3 CAPABILITY.
/// The Taxation civic stays researchable through its prerequisites in any Age (research is not Age-gated), but the
/// tax edict is OPERATIONAL only once the polity has ENTERED sim.json governance.taxationMinAge — evaluated inside
/// Governance.CanLevyTax, so GovernanceSystem (orders applied on PREV), the AI valve, the available-actions query /
/// UI and hand-built orders all meet the one predicate. Save/load and replay carry it because the Age is serialized
/// state (AgeStates) and the predicate reads nothing else new.
/// </summary>
public class TaxAgeGateTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly AgeContent Ages = TestConfigs.Ages();

    private static (WorldState World, PolityId Player) Founded() => GovernanceRigs.Founded();

    // ------------------------------------------------------------------ content

    [Fact]
    public void TheAgeRequirement_LivesInContent_AsTheBronzeAge()
    {
        Assert.Equal(3, Cfg.Governance!.TaxationMinAge);
        Assert.Equal("Bronze Age", Ages.Age(Cfg.Governance.TaxationMinAge!.Value).Name);
        // The Taxation civic itself carries the same Age tag in research.json (A3): the gate and the node agree.
        Assert.Equal("taxation", Cfg.Governance.TaxationRequires);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ALoaderRefuses_ATaxationMinAgeOutsideTheAges(int bad)
    {
        string json = TestConfigs.SimJson().Replace("\"taxationMinAge\": 3", $"\"taxationMinAge\": {bad}", StringComparison.Ordinal);
        Assert.NotEqual(TestConfigs.SimJson(), json);
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    // ------------------------------------------------------------------ the predicate, by Age

    [Fact]
    public void TaxationKnown_InA1AndA2_IsKnowledgeNotTheCapability_FromA3ItIsOpen()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.Equal(TaxGate.NeedsKnowledge, Governance.GateOf(w, Cfg, p));
        GovernanceRigs.GrantKnowledgeOnly(w, p);
        Assert.True(Governance.KnowsTaxation(w, Cfg, p));
        for (int age = 1; age <= 9; age++)
        {
            GovernanceRigs.EnterAge(w, p, age);
            bool open = age >= 3;
            Assert.Equal(open ? TaxGate.Open : TaxGate.NeedsAge, Governance.GateOf(w, Cfg, p));
            Assert.Equal(open, Governance.CanLevyTax(w, Cfg, p));
            Assert.Equal(open, AvailableActionsQuery.For(w, Cfg, p).Any(a => a.Order == OrderKind.SetTaxRate));
        }
    }

    [Fact]
    public void AgeAlone_WithoutTheKnowledge_OpensNothing()
    {
        (WorldState w, PolityId p) = Founded();
        GovernanceRigs.EnterAge(w, p, 5);
        Assert.Equal(TaxGate.NeedsKnowledge, Governance.GateOf(w, Cfg, p));
        Assert.False(Governance.CanLevyTax(w, Cfg, p));
    }

    [Fact]
    public void ADeclaredMinimumAge_WithNoAgeContent_FailsClosed_AndNoMinimumIsTheKnowledgeGateAlone()
    {
        (WorldState w, PolityId p) = Founded();
        GovernanceRigs.Grant(w, p);   // knowledge AND A3
        Assert.True(Governance.CanLevyTax(w, Cfg, p));
        Assert.False(Governance.CanLevyTax(w, Cfg with { Ages = null }, p));
        (WorldState a1, PolityId q) = Founded();
        GovernanceRigs.GrantKnowledgeOnly(a1, q);
        Assert.True(Governance.CanLevyTax(a1, TestConfigs.PreTaxAge(Cfg), q));   // the R5 gate, knowledge only
    }

    // ------------------------------------------------------------------ every caller

    [Fact]
    public void AHandBuiltEdict_InA2WithTaxationKnown_IsRefusedByTheSystem_AndAcceptedInA3()
    {
        (WorldState w, PolityId p) = Founded();
        GovernanceRigs.GrantKnowledgeOnly(w, p);
        GovernanceRigs.EnterAge(w, p, 2);
        var log = new OrderLog();
        log.Append(Governance.TaxOrder(w.Clock.Turn, p, 30));
        WorldState refused = GovernanceRigs.GovernanceOnly(log).Step(w);
        Assert.Equal(0, refused.TaxPolicies.Count);

        GovernanceRigs.EnterAge(w, p, 3);
        WorldState applied = GovernanceRigs.GovernanceOnly(log).Step(w);
        Assert.Equal(0.30, Governance.NominalTaxRate(applied, p), 12);
    }

    [Fact]
    public void TheAiValve_IsSilentInA2_AndSpeaksInA3()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded(aiEmpires: 1);
        PolityId ai = default;
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Source == CommandSource.Ai) ai = w.Polities[i].Id;
        GovernanceRigs.GrantKnowledgeOnly(w, ai);
        GovernanceRigs.EnterAge(w, ai, 2);
        Assert.Empty(AiGovernance.OrdersFor(w, Cfg, ai, w.Clock.Turn));
        Assert.DoesNotContain(AiOrders.For(w, Cfg), o => o.Kind == OrderKind.SetTaxRate);
        GovernanceRigs.EnterAge(w, ai, 3);
        Assert.NotEmpty(AiGovernance.OrdersFor(w, Cfg, ai, w.Clock.Turn));
        Assert.Empty(AiGovernance.OrdersFor(w, Cfg, player, w.Clock.Turn));
    }

    /// <summary>
    /// THE ORDER-DELIVERY PIN with the REAL Age transition (CLAUDE.md: every order-delivery semantic gets a turn-exact
    /// pin). A player in A2 who knows Taxation and the A3 entry issues, at turn t, BOTH the AdvanceAge to A3 and a
    /// SetTaxRate; at t+1 another SetTaxRate. The Age is in force in the state of t+1 (AgeTransitionSystem), but the
    /// turn-t edict is judged on PREV = state t, where the polity is still in A2 — refused. The turn-(t+1) edict is
    /// judged on state t+1 (A3) — applied, its row in state t+2. Production first answers it in t+2 → t+3.
    /// </summary>
    [Fact]
    public void AnEdictIssuedWithTheAdvance_IsRefused_TheNextTurnsIsApplied_TurnExact()
    {
        (WorldState w, PolityId p) = Founded();
        var known = new List<string>();
        foreach (string id in new[] { GovernanceRigs.TaxationNode, "arsenical_bronze", "wheel_solid", "law_code" })
            known.AddRange(WithAncestors(id));
        UniversityRigs.Grant(w, Research, p, known.Distinct().ToArray());
        GovernanceRigs.EnterAge(w, p, 2);
        Assert.True(AgeQuery.IsEligible(w, Ages, p), "rig: the player must be eligible for A3");
        long t = w.Clock.Turn;

        var log = new OrderLog();
        log.Append(AgeQuery.AdvanceOrder(w, p, 3, Ages.Surges[0].Key));
        log.Append(Governance.TaxOrder(t, p, 25));
        log.Append(Governance.TaxOrder(t + 1, p, 40));
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var ex = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.DevWorldgen())), log);

        WorldState s1 = ex.Step(w);
        Assert.Equal(3, AgeQuery.CurrentAge(s1, Ages, p));              // in A3 from state t+1
        Assert.False(Governance.HasPolicy(s1, p));                       // the turn-t edict was judged on A2: refused
        Assert.True(Governance.CanLevyTax(s1, Cfg, p));
        WorldState s2 = ex.Step(s1);
        Assert.Equal(0.40, Governance.NominalTaxRate(s2, p), 12);        // the turn-(t+1) edict: in force at t+2
    }

    /// <summary>Save/load and replay of the gate: a world in A2 with Taxation known, saved and loaded, refuses the
    /// same edict; the same world in A3 applies it; two runs from the same start agree hash for hash.</summary>
    [Fact]
    public void TheGate_SurvivesSaveLoad_AndReplaysExactly()
    {
        foreach (int age in new[] { 2, 3 })
        {
            (WorldState w, PolityId p) = Founded();
            GovernanceRigs.GrantKnowledgeOnly(w, p);
            GovernanceRigs.EnterAge(w, p, age);
            using var ms = new MemoryStream();
            Snapshot.Save(w, ms);
            ms.Position = 0;
            WorldState loaded = Snapshot.Load(ms, w.Terrain);
            Assert.Equal(Governance.CanLevyTax(w, Cfg, p), Governance.CanLevyTax(loaded, Cfg, p));
            Assert.Equal(age >= 3, Governance.CanLevyTax(loaded, Cfg, p));
            var log = new OrderLog();
            log.Append(Governance.TaxOrder(w.Clock.Turn, p, 30));
            WorldState a = GovernanceRigs.GovernanceOnly(log).Step(w), b = GovernanceRigs.GovernanceOnly(log).Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            Assert.Equal(age >= 3, Governance.HasPolicy(b, p));
        }
    }

    private static string[] WithAncestors(string nodeId)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        stack.Push(Research.IndexOfId(nodeId));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int q in Research.Nodes[i].PrerequisiteNodes) stack.Push(q);
        }
        var ids = new List<string>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) ids.Add(Research.Nodes[i].Id);
        return ids.ToArray();
    }
}

/// <summary>
/// F1 (2026-10-05; d049 §15) — the A3 requirement lives in two content places, research.json (the taxation node's Age
/// tag) and sim.json (governance.taxationMinAge). The four-stream load cross-validates them: the edict may not be
/// operational in an Age earlier than the earliest Age at which its knowledge requirement can be met.
/// </summary>
public class TaxAgeContentAgreementTests
{
    private static SimConfig Load(string simJson, string researchJson)
    {
        using var sim = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(simJson));
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        using var research = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(researchJson));
        return SimConfigLoader.Load(sim, needs, goods, research);
    }

    [Fact]
    public void TheShippedContent_Agrees_TheGateNodeIsA3_AndTheMinimumIsA3()
    {
        SimConfig cfg = Load(TestConfigs.SimJson(), TestConfigs.ResearchJson());
        var req = Sim.Core.Systems.Research.ResearchContentLoader.ParseRequirement(cfg.Research!, cfg.Governance!.TaxationRequires, "t");
        Assert.Equal(3, SimConfigLoader.EarliestRequirementAge(cfg.Research!, req));
        Assert.Equal(3, cfg.Governance.TaxationMinAge);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ALoaderRefuses_AMinimumAgeEarlierThanTheGateNodesAge(int minAge)
    {
        string sim = TestConfigs.SimJson().Replace("\"taxationMinAge\": 3", $"\"taxationMinAge\": {minAge}", StringComparison.Ordinal);
        Assert.NotEqual(TestConfigs.SimJson(), sim);
        SimConfigException e = Assert.Throws<SimConfigException>(() => Load(sim, TestConfigs.ResearchJson()));
        Assert.Contains("taxationMinAge", e.Message);
    }

    [Fact]
    public void ALoaderRefuses_AGateNodeMovedToALaterAgeThanTheMinimum_AndAcceptsALaterMinimum()
    {
        string research = TestConfigs.ResearchJson();
        int at = research.IndexOf("\"id\": \"taxation\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        int tag = research.IndexOf("\"age\": \"A3\"", at, StringComparison.Ordinal);
        string moved = research[..tag] + "\"age\": \"A4\"" + research[(tag + "\"age\": \"A3\"".Length)..];
        Assert.Throws<SimConfigException>(() => Load(TestConfigs.SimJson(), moved));
        // A minimum LATER than the node's Age is a legitimate design (knowledge first, capability later).
        string later = TestConfigs.SimJson().Replace("\"taxationMinAge\": 3", "\"taxationMinAge\": 5", StringComparison.Ordinal);
        Assert.Equal(5, Load(later, TestConfigs.ResearchJson()).Governance!.TaxationMinAge);
    }
}
