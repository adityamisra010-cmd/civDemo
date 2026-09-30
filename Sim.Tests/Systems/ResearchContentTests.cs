using System.Security.Cryptography;
using System.Text.Json;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-029 §2: the shipped research content is the MIGRATED corpus (D-044 R15, R23),
/// and every validator D-044 R24 names rejects its failure loudly, naming the rule.
/// Each rejection test starts from a VALID graph (the loader accepts the rig and the
/// canonical file — proven first) and changes exactly one thing.
/// </summary>
public class ResearchContentTests
{
    private static readonly ResearchContent Canonical = TestConfigs.Research();

    private static JsonDocument Corpus()
    {
        return JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepoPaths.Root(), "tech-graph-v0.6.json")));
    }

    // ------------------------------------------------------------------ the migrated corpus

    [Fact]
    public void Canonical_IntegratesEveryCorpusTechnology_InCorpusOrder_WithItsStableId()
    {
        using JsonDocument corpus = Corpus();
        JsonElement techs = corpus.RootElement.GetProperty("technologies");
        Assert.Equal(424, techs.GetArrayLength());
        Assert.Equal(424, Canonical.TechnologyCount);
        for (int i = 0; i < 424; i++)
        {
            JsonElement t = techs[i];
            ResearchNode n = Canonical.Nodes[i];
            Assert.Equal(t.GetProperty("id").GetString(), n.Id);
            Assert.Equal(i + 1, n.Key.Value);
            Assert.Equal(t.GetProperty("name").GetString(), n.Name);
            Assert.Equal(ResearchTree.Technology, n.Tree);
            JsonElement prereq = t.GetProperty("research").GetProperty("prereq");
            Assert.Equal(prereq.ValueKind == JsonValueKind.Null ? null : prereq.GetString(), n.Prerequisite?.Source);
            Assert.Equal(t.GetProperty("research").GetProperty("eureka").GetArrayLength(), n.Eurekas.Count);
            string age = t.GetProperty("age").GetString()!;
            Assert.Equal(age == "F" ? "A9" : age, n.Age);
            Assert.Equal(age == "F", n.Frontier);
        }
    }

    [Fact]
    public void Canonical_CivicsAreTheSixArchitectureCandidates_OnTheSameKeySpace()
    {
        Assert.Equal(6, Canonical.CivicsCount);
        string[] ids = new string[6];
        for (int i = 0; i < 6; i++)
        {
            ResearchNode n = Canonical.Nodes[424 + i];
            ids[i] = n.Id;
            Assert.Equal(ResearchTree.Civics, n.Tree);
            Assert.Equal(1001 + i, n.Key.Value);
            Assert.Equal(-1, n.Branch);
            foreach (int e in n.UnlockedEntities) Assert.Equal(ResearchEntityKind.Institution, Canonical.Entities[e].Kind);
        }
        Assert.Equal(["law_code", "legal_code_roman", "census", "coined_wage", "patent", "joint_stock"], ids);
    }

    [Fact]
    public void Canonical_TreeOneViews_TrunkAndExactlyFiveSubtrees_WithTheMeasuredCounts()
    {
        int[] counts = new int[6]; // trunk, then 1.1..1.5
        for (int i = 0; i < Canonical.TechnologyCount; i++) counts[Canonical.Nodes[i].Branch + 1]++;
        Assert.Equal([176, 37, 22, 130, 44, 15], counts);
        Assert.Equal(ResearchContentLoader.RuledBranchIds.Length, Canonical.Branches.Count);
        for (int b = 0; b < 5; b++)
        {
            Assert.Equal(ResearchContentLoader.RuledBranchIds[b], Canonical.Branches[b].Id);
            Assert.Equal($"1.{b + 1}", Canonical.Branches[b].Number);
            Assert.Equal(b, Canonical.UniversityTypes[b].Branch); // one specialized university type per subtree
        }
    }

    [Fact]
    public void Canonical_IsInSyncWithTheCorpusFileItWasMigratedFrom()
    {
        byte[] corpus = File.ReadAllBytes(Path.Combine(RepoPaths.Root(), "tech-graph-v0.6.json"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(corpus)), Canonical.CorpusSha256);
    }

    [Fact]
    public void Canonical_Eurekas_PreserveEveryCorpusString_AndOnlyFaithfulMappingsAreEvaluable()
    {
        var byStatus = new int[6];
        int total = 0;
        foreach (ResearchNode n in Canonical.Nodes)
            foreach (ResearchEureka e in n.Eurekas)
            {
                total++;
                byStatus[(int)e.Status]++;
                if (e.Status == EurekaStatus.Evaluable) Assert.NotNull(e.Condition);
                else Assert.Null(e.Condition);
            }
        Assert.Equal(801, total);
        Assert.Equal(93, byStatus[(int)EurekaStatus.Evaluable]);
        Assert.Equal(547, byStatus[(int)EurekaStatus.NoStateCarrier]);
        Assert.Equal(79, byStatus[(int)EurekaStatus.InstitutionStateAbsent]);
        Assert.Equal(61, byStatus[(int)EurekaStatus.ContactStateAbsent]);
        Assert.Equal(21, byStatus[(int)EurekaStatus.ImpliedByPrerequisites]);

        // A circumstance the node's prerequisites guarantee is declared, not evaluated (ADR-029 §7):
        // "sustained fire" on heat_treatment_stone, which requires fire_making.
        ResearchEureka fire = Canonical.Nodes[Canonical.IndexOfId("heat_treatment_stone")].Eurekas[1];
        Assert.Equal("circumstance: sustained fire", fire.Text);
        Assert.Equal(EurekaStatus.ImpliedByPrerequisites, fire.Status);
        // A different substance made from a good, and exchange, are not the good (ADR-029 §7 rule).
        foreach (ResearchNode n in Canonical.Nodes)
            foreach (ResearchEureka e in n.Eurekas)
                if (e.Text is "circumstance: wood ash" or "circumstance: lime or wood ash"
                    or "circumstance: long-distance exchange reaching a tin source")
                    Assert.Equal(EurekaStatus.NoStateCarrier, e.Status);
    }

    [Fact]
    public void Canonical_NoEvaluableKnowledgeEureka_IsGuaranteedByItsOwnNodesPrerequisites()
    {
        // For every evaluable condition that reads only knowledge atoms, exhibit a WITNESS: the
        // largest state reachable without the node and without every atom the condition names.
        // If the node is available there, the condition is false at a moment the node is
        // researchable, so it is a circumstance, not a flat cost cut (ADR-029 §7).
        int checkedConditions = 0;
        for (int i = 0; i < Canonical.Nodes.Count; i++)
            foreach (ResearchEureka e in Canonical.Nodes[i].Eurekas)
            {
                if (e.Condition is not { } cond || cond.QuantityIds.Count > 0) continue;
                var excluded = new bool[Canonical.Nodes.Count];
                excluded[i] = true;
                foreach (int a in cond.AtomIds) excluded[a] = true;
                var done = new bool[Canonical.Nodes.Count];
                for (bool changed = true; changed;)
                {
                    changed = false;
                    bool stage = ResearchQuery.StageReached(Canonical, done);
                    for (int j = 0; j < done.Length; j++)
                        if (!done[j] && !excluded[j] && ResearchQuery.IsAvailable(Canonical, j, done, stage))
                            done[j] = changed = true;
                }
                Assert.True(ResearchQuery.IsAvailable(Canonical, i, done, ResearchQuery.StageReached(Canonical, done)),
                    $"{Canonical.Nodes[i].Id}: no witness state");
                Assert.False(cond.Evaluate(null, a => done[a], null), $"{Canonical.Nodes[i].Id}: '{cond.Source}' holds in the witness");
                checkedConditions++;
            }
        Assert.Equal(2, checkedConditions); // copper_smelting (charcoal), windmill_post (gearing)
    }

    [Fact]
    public void Canonical_ResearchStage_IsReachedByMainTreeAndCivicsKnowledgeAlone()
    {
        var mask = new bool[Canonical.Nodes.Count];
        for (int i = 0; i < mask.Length; i++) mask[i] = Canonical.Nodes[i].Branch < 0; // everything but the subtrees
        Assert.True(ResearchQuery.StageReached(Canonical, mask));
        Assert.False(ResearchQuery.StageReached(Canonical, new bool[mask.Length]));
        // The stage needs a Civic (legal_code_roman) as well as technology: Tree 2 knowledge opens Tree 1's subtrees.
        mask[Canonical.IndexOfId("legal_code_roman")] = false;
        Assert.False(ResearchQuery.StageReached(Canonical, mask));
    }

    [Fact]
    public void Canonical_CorpusDefect_NewspaperIsDeclaredUnresolved_AndNeverKnowledgeEligible()
    {
        var all = new bool[Canonical.Nodes.Count];
        Array.Fill(all, true);
        int newspaper = Canonical.EntityIndexOf("inst.newspaper");
        Assert.Equal(["postal_imperial"], Canonical.Entities[newspaper].Unresolved);
        Assert.False(ResearchQuery.IsKnowledgeEligible(Canonical, newspaper, all));
        Assert.False(ResearchQuery.IsKnowledgeEligible(Canonical, Canonical.EntityIndexOf("inst.mass_schooling"), all));
        Assert.True(ResearchQuery.IsKnowledgeEligible(Canonical, Canonical.EntityIndexOf("inst.university"), all));
        // A null requirement is valid and always met (ADR-028 §3): the granary needs no technology.
        Assert.True(ResearchQuery.IsKnowledgeEligible(Canonical, Canonical.EntityIndexOf("building.granary"), new bool[all.Length]));
    }

    [Fact]
    public void Canonical_RepeatableFrontierNodes_KeepTheirDescriptor_ButCompleteOnce()
    {
        int repeatable = 0, frontier = 0;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (n.HasRepeatableDescriptor) { repeatable++; Assert.True(n.Frontier); }
            if (n.Frontier) { frontier++; Assert.Equal("A9", n.Age); }
        }
        Assert.Equal(10, repeatable);
        Assert.Equal(16, frontier);
    }

    // ------------------------------------------------------------------ the rig is valid (baseline for every rejection)

    [Fact]
    public void Rig_IsValid_SoEachRejectionBelowIsCausedByItsOneChange()
    {
        ResearchContent rig = Standard().Load();
        Assert.Equal(11, rig.TechnologyCount);
        Assert.Equal(2, rig.CivicsCount);
    }

    private static void Rejects(Spec spec, string fragment) =>
        Assert.Contains(fragment, Assert.Throws<ResearchContentException>(spec.Load).Message);

    private static void RejectsJson(string json, string fragment) =>
        Assert.Contains(fragment, Assert.Throws<ResearchContentException>(
            () => ResearchContentLoader.Load(json, TestConfigs.Sim().Goods)).Message);

    /// <summary>Replaces exactly one anchor; the anchor must exist (SimConfigTests precedent: a
    /// substitution that changes nothing would make the rejection test vacuous).</summary>
    private static string Mutate(string json, string anchor, string replacement)
    {
        int at = json.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(at >= 0, $"anchor not found: {anchor}");
        return string.Concat(json.AsSpan(0, at), replacement, json.AsSpan(at + anchor.Length));
    }

    // ------------------------------------------------------------------ D-044 R24 validators

    [Fact]
    public void Rejects_DuplicateIds_AcrossBothTrees()
    {
        Spec s = Standard();
        s.Civics.Add(new Node(1003, "b", "a"));
        Rejects(s, "duplicate node id 'b'");
    }

    [Fact]
    public void Rejects_KeysThatAreNotUniqueAndAscending()
    {
        Spec s = Standard();
        s.Civics[1] = s.Civics[1] with { Key = 1001 };
        Rejects(s, "is not strictly greater than the previous key");
    }

    [Fact]
    public void Rejects_InvalidTreeIds()
    {
        RejectsJson(Mutate(Standard().Json(), "{\"id\":\"civics\",\"number\":\"2\",\"name\":\"Civics\"}",
            "{\"id\":\"civics\",\"number\":\"2\",\"name\":\"Civics\"},{\"id\":\"diplomacy\",\"number\":\"3\",\"name\":\"Diplomacy\"}"),
            "trees must be exactly [technology, civics]");
    }

    [Fact]
    public void Rejects_InvalidBranchIds_NoIndustryAndEnergySubtree()
    {
        RejectsJson(Mutate(Standard().Json(), "\"id\":\"engineering\"", "\"id\":\"industry_energy\""),
            "'industry_energy' is invalid");
        Spec s = Standard();
        s.Technologies[5] = s.Technologies[5] with { Branch = "naval" };
        Rejects(s, "branch 'naval' is not a subtree id");
    }

    [Fact]
    public void Rejects_InvalidBranchAssignment_CivicsCarryNoSubtree_AndTechnologiesMustDeclareOne()
    {
        RejectsJson(Mutate(Standard().Json(), "\"key\":1001,\"id\":\"law\",\"name\":\"LAW\",\"desc\":\"rig node\",\"age\":\"A1\",\"frontier\":false,\"emerged\":null,",
            "\"key\":1001,\"id\":\"law\",\"name\":\"LAW\",\"desc\":\"rig node\",\"age\":\"A1\",\"frontier\":false,\"emerged\":null,\"branch\":null,"),
            "a Civics node carries no 'branch'");
        RejectsJson(Mutate(Standard().Json(), "\"emerged\":null,\"branch\":null,", "\"emerged\":null,"),
            "'branch' is required on a technology");
    }

    [Fact]
    public void Rejects_InvalidBranchAssignment_AMainTreeNodeRequiringASubtreeNode()
    {
        Spec s = Standard();
        s.Technologies.Add(new Node(12, "late", "d OR mil")); // even with a Main-tree alternative
        Rejects(s, "invalid subtree assignment");
    }

    [Fact]
    public void Rejects_MissingPrerequisites()
    {
        Spec s = Standard();
        s.Technologies[3] = s.Technologies[3] with { Prereq = "a OR ghost" };
        Rejects(s, "missing or malformed prerequisite");
    }

    [Fact]
    public void Rejects_CircularPrerequisites_NamingTheCycle()
    {
        Spec s = Standard();
        s.Technologies[0] = s.Technologies[0] with { Prereq = "d" }; // a <- d <- (a OR b): a requires d requires a
        var e = Assert.Throws<ResearchContentException>(() => ResearchContentLoader.Load(JsonWithoutDepth(s), TestConfigs.Sim().Goods));
        Assert.Contains("prerequisite cycle", e.Message);
        Assert.Contains("a requires d requires a", e.Message);

        Spec self = Standard();
        self.Technologies[1] = self.Technologies[1] with { Prereq = "b" };
        e = Assert.Throws<ResearchContentException>(() => ResearchContentLoader.Load(JsonWithoutDepth(self), TestConfigs.Sim().Goods));
        Assert.Contains("requires itself", e.Message);
    }

    /// <summary>A cyclic graph has no depth; the rig's depth pass would loop, so the
    /// cycle tests write depth 0 everywhere — the loader rejects the cycle before depth.</summary>
    private static string JsonWithoutDepth(Spec s)
    {
        var acyclic = new Spec { Stage = s.Stage };
        foreach (Node n in s.Technologies) acyclic.Technologies.Add(n with { Prereq = null });
        foreach (Node n in s.Civics) acyclic.Civics.Add(n with { Prereq = null });
        string json = acyclic.Json();
        // Restore each prerequisite in place: key-anchored replacement of that node's "prereq":null.
        var nodes = new List<Node>(s.Technologies);
        nodes.AddRange(s.Civics);
        foreach (Node n in nodes)
        {
            if (n.Prereq is null) continue;
            string anchor = $"\"key\":{n.Key},\"id\":\"{n.Id}\"";
            int at = json.IndexOf(anchor, StringComparison.Ordinal);
            int p = json.IndexOf("\"prereq\":null", at, StringComparison.Ordinal);
            json = string.Concat(json.AsSpan(0, p), $"\"prereq\":\"{n.Prereq}\"", json.AsSpan(p + "\"prereq\":null".Length));
        }
        return json;
    }

    [Fact]
    public void Rejects_NotInAPrerequisite_AvailabilityMustBeMonotone()
    {
        Spec s = Standard();
        s.Technologies[3] = s.Technologies[3] with { Prereq = "a AND NOT b" };
        Rejects(s, "NOT is not allowed in a prerequisite");
    }

    [Fact]
    public void Rejects_InvalidEurekaReferences_UnknownNames_AndDeadConditions()
    {
        Spec unknown = Standard();
        unknown.Technologies[10] = unknown.Technologies[10] with { Eurekas = [new Eu("circumstance: gold", "stock_gold > 0")] };
        Rejects(unknown, "invalid Eureka reference");

        Spec self = Standard();
        self.Technologies[10] = self.Technologies[10] with { Eurekas = [new Eu("circumstance: itself", "w")] };
        Rejects(self, "cannot hold while 'w' is still researchable");

        Spec descendant = Standard();
        descendant.Technologies[0] = descendant.Technologies[0] with { Eurekas = [new Eu("circumstance: c", "c")] }; // c requires a
        Rejects(descendant, "a dead Eureka");

        // Behind the stage: c IS the research stage, so the military subtree cannot open while c is
        // still researchable — a condition naming a subtree node on c can never hold.
        Spec stageGated = Standard();
        stageGated.Technologies[2] = stageGated.Technologies[2] with { Eurekas = [new Eu("circumstance: mil", "mil")] };
        Rejects(stageGated, "cannot hold while 'c' is still researchable");

        // Every alternative needs the node: still dead.
        Spec allAlternatives = Standard();
        allAlternatives.Technologies[0] = allAlternatives.Technologies[0] with { Eurekas = [new Eu("circumstance: c or w", "c OR w")] };
        Rejects(allAlternatives, "cannot hold while 'a' is still researchable");

        Spec mismatch = Standard();
        mismatch.Technologies[10] = mismatch.Technologies[10] with { Eurekas = [new Eu("circumstance: timber", "stock_timber > 0", "no-state-carrier")] };
        Rejects(mismatch, "'when' must be present exactly when status is 'evaluable'");
    }

    [Fact]
    public void Accepts_EurekasThatCanHoldWhileTheNodeIsResearchable_ThroughAnOrAlternative_OrAfterTheStage()
    {
        // d = a OR b: d can complete through b while a is still researchable, so "d" on a is live
        // (the pre-review check rejected it because d names a in one alternative).
        Spec orDependent = Standard();
        orDependent.Technologies[0] = orDependent.Technologies[0] with { Eurekas = [new Eu("circumstance: d", "d")] };
        Assert.Single(orDependent.Load().Nodes[0].Eurekas);

        // e = (a AND c) OR (b AND d): e completes through b AND d without a.
        Spec nested = Standard();
        nested.Technologies[0] = nested.Technologies[0] with { Eurekas = [new Eu("circumstance: e", "e")] };
        Assert.Single(nested.Load().Nodes[0].Eurekas);

        // One live alternative keeps an OR condition live: c needs a, b does not.
        Spec oneLive = Standard();
        oneLive.Technologies[0] = oneLive.Technologies[0] with { Eurekas = [new Eu("circumstance: c or b", "c OR b")] };
        Assert.Single(oneLive.Load().Nodes[0].Eurekas);

        // A subtree node may name another subtree's node: both open with the stage.
        Spec crossSubtree = Standard();
        crossSubtree.Technologies[7] = crossSubtree.Technologies[7] with { Eurekas = [new Eu("circumstance: mil", "mil")] };
        Assert.Single(crossSubtree.Load().Nodes[7].Eurekas);
    }

    [Fact]
    public void Rejects_ExplicitNullsInRequiredValues_AsAValidationFailure_NeverANullReference()
    {
        // D-044 R24 "validation failures must be explicit": [JsonRequired] only checks presence,
        // so each of these used to load a null (or crash with a NullReferenceException).
        string json = Standard().Json();
        foreach ((string anchor, string replacement) in new[]
        {
            ("\"desc\":\"rig node\"", "\"desc\":null"),
            ("\"eurekas\":[]", "\"eurekas\":null"),
            ("\"immediate\":[]", "\"immediate\":null"),
            ("\"secondaryDomains\":[]", "\"secondaryDomains\":null"),
            ("\"schema\":\"civ-sim/research@1\"", "\"schema\":null"),
        })
            RejectsJson(Mutate(json, anchor, replacement), "missing required values");
    }

    [Fact]
    public void Rejects_InvalidCosts()
    {
        Spec zero = Standard();
        zero.Technologies[2] = zero.Technologies[2] with { Cost = 0.0 };
        Rejects(zero, "cost 0 is invalid");
        Spec negative = Standard();
        negative.Civics[0] = negative.Civics[0] with { Cost = -5.0 };
        Rejects(negative, "cost -5 is invalid");
    }

    [Fact]
    public void Rejects_InvalidUnlockReferences()
    {
        string json = Standard().Json();
        // An unlock naming an entity that does not exist.
        RejectsJson(Mutate(json, "\"units\":[],\"buildings\":[\"building.hall\"]", "\"units\":[],\"buildings\":[\"building.hall\",\"building.ghost\"]"),
            "'building.ghost' is not a registry entity");
        // The right entity under the wrong kind list.
        RejectsJson(Mutate(json, "\"units\":[],\"buildings\":[\"building.hall\"]", "\"units\":[\"building.hall\"],\"buildings\":[]"),
            "is a Building, not a Unit");
        // The reverse index disagreeing with the authoritative entity requirement.
        RejectsJson(Mutate(json, "\"units\":[],\"buildings\":[\"building.hall\"]", "\"units\":[],\"buildings\":[]"),
            "requires this node but is not listed");
        // The same entity listed twice.
        RejectsJson(Mutate(json, "\"units\":[],\"buildings\":[\"building.hall\"]", "\"units\":[],\"buildings\":[\"building.hall\",\"building.hall\"]"),
            "'building.hall' is listed twice");
        // A Civic unlocking a building.
        Spec civicBuilds = Standard();
        civicBuilds.Entities.Add(new Entity("building.court", "building", "law"));
        Rejects(civicBuilds, "a Civics node makes institutional forms eligible");
    }

    [Fact]
    public void Rejects_OrphanNodes()
    {
        RejectsJson(Mutate(Standard().Json(), "\"capabilities\":[\"e capability\"]", "\"capabilities\":[]"),
            "orphan node");
    }

    [Fact]
    public void Rejects_UnreachableNodes_TheStageDeadlock_AndMainTreeKnowledgeBehindTheStage()
    {
        Spec deadlock = Standard();
        deadlock.Stage = "mil"; // only the stage can open mil
        Rejects(deadlock, "can never be satisfied by knowledge reachable before the stage");

        // Cross-tree: a Main-tree technology reaching subtree knowledge THROUGH a Civic.
        Spec crossTree = Standard();
        crossTree.Civics.Add(new Node(1003, "guild", "eng"));
        crossTree.Technologies.Add(new Node(12, "late", "guild"));
        Rejects(crossTree, "cannot be researched before the research stage");
    }

    [Fact]
    public void Rejects_ImmediateEffects_NoneIsRatified()
    {
        RejectsJson(Mutate(Standard().Json(), "\"effects\":{\"immediate\":[]}", "\"effects\":{\"immediate\":[{\"kind\":\"plus_ten_percent\"}]}"),
            "effects.immediate is not empty");
    }

    [Fact]
    public void Rejects_TuningOutsideItsRanges_LinearPopulationIsForbidden()
    {
        Spec linear = Standard();
        linear.Exponent = 1.0;
        Rejects(linear, "clpAdultExponent 1 must be in (0, 1)");
        Spec noEureka = Standard();
        noEureka.EurekaFraction = 0.0;
        Rejects(noEureka, "eurekaCreditFraction 0 must be in (0, 1]");
    }

    [Fact]
    public void Rejects_EntityRequirements_UndeclaredOrStaleUnresolvedReferences_AndInstitutionCycles()
    {
        Spec undeclared = Standard();
        undeclared.Entities.Add(new Entity("inst.post", "institution", "c AND postal_imperial"));
        Rejects(undeclared, "invalid reference");

        Spec stale = Standard();
        stale.Entities.Add(new Entity("inst.post", "institution", "c", ["postal_imperial"]));
        Rejects(stale, "is declared unresolved but the requirement does not use it");

        Spec cycle = Standard();
        cycle.Entities.Add(new Entity("inst.alpha", "institution", "beta"));
        cycle.Entities.Add(new Entity("inst.beta", "institution", "alpha"));
        Rejects(cycle, "institution requirement cycle");

        Spec prefix = Standard();
        prefix.Entities.Add(new Entity("building.oops", "unit", "a"));
        Rejects(prefix, "must start with 'unit.'");
    }

    [Fact]
    public void Rejects_AStaleDepth_AnUnknownAge_AndAMissingRequiredKey()
    {
        RejectsJson(Mutate(Standard().Json(), "\"depth\":1,\"cost\":2500,\"prereq\":\"a AND b\"", "\"depth\":7,\"cost\":2500,\"prereq\":\"a AND b\""),
            "does not match the longest prerequisite path");
        RejectsJson(Mutate(Standard().Json(), "\"key\":1,\"id\":\"a\",\"name\":\"A\",\"desc\":\"rig node\",\"age\":\"A1\"",
            "\"key\":1,\"id\":\"a\",\"name\":\"A\",\"desc\":\"rig node\",\"age\":\"F\""), "age 'F' is not one of the nine Ages");
        RejectsJson(Mutate(Standard().Json(), "\"generator\":\"ResearchRigs\"", "\"generatorX\":\"ResearchRigs\""),
            "missing required values");
    }

    [Fact]
    public void Rejects_AStockQuantityWhenNoGoodsRegistryIsAttached()
    {
        var e = Assert.Throws<ResearchContentException>(() => ResearchContentLoader.Load(Standard().Json(), goods: null));
        Assert.Contains("no goods registry is attached", e.Message);
    }
}
