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

    // ------------------------------------------------------------------ the finalized corpus (ADR-029 addendum A)

    [Fact]
    public void Canonical_IntegratesEveryCorpusTechnology_InCorpusOrder_WithItsStableId()
    {
        // Ids, keys and names are the corpus's. Prerequisites, ages and Eurekas were then curated
        // by the Director's finalization (Group A and Group B), so they are not compared here.
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
            if (D047Renamed.TryGetValue(n.Id, out string? renamed))
            {
                // D-047 rulings 2, 3, 6 and 7 re-scoped these baseline-claiming nodes (id and key kept).
                Assert.Equal(renamed, n.Name);
                Assert.NotEqual(t.GetProperty("name").GetString(), n.Name);
            }
            else Assert.Equal(t.GetProperty("name").GetString(), n.Name);
            Assert.Equal(ResearchTree.Technology, n.Tree);
            Assert.Equal(t.GetProperty("age").GetString() == "F", n.Frontier);
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
    public void Canonical_Tuning_PerTurnResearchPoints_TheFloor_TheSharedCeiling_AndTheCostModel()
    {
        ResearchTuning t = Canonical.Tuning;
        Assert.Equal(0.08, t.RpCoefficient);                     // ADR-030 calibration values (tunable)
        Assert.Equal(0.699, t.RpExponent);
        Assert.Equal([(100.0, 2.0), (1_000.0, 10.0)], t.RpAnchors);
        Assert.Equal(0.2, t.EffectiveCostFloorFraction);         // EffectiveCost floor
        Assert.Equal(0.4, t.AccelerationCreditCeilingFraction);  // one shared Eureka + foreign-exposure pool
        Assert.Equal(new ResearchCostModel(92.4, 2.0), t.CostModel);
        Assert.Equal(["eureka", "foreign_exposure"], ResearchContentLoader.AccelerationSourceIds);
    }

    [Fact]
    public void Canonical_Costs_AreContentDerived_UTimesKToTheMagnitude_NoCalibrationAdjustment_NeverDepth()
    {
        ResearchCostModel m = Canonical.Tuning.CostModel;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            ResearchCostRationale r = n.CostRationale;
            Assert.Equal(0.0, r.CalibrationAdjustment);
            Assert.Equal(n.BaseCost, r.ContentCost);
            Assert.True(Math.Abs(r.Novelty + r.Difficulty + r.Material + r.Institutional + r.Breadth + r.PrerequisiteComplexity - r.Magnitude) <= 1e-9, n.Id);
            double formula = m.U * Math.Pow(m.K, r.Magnitude);
            Assert.True(Math.Abs(n.BaseCost - formula) <= Math.Max(5.0, 1e-3 * n.BaseCost), $"{n.Id}: {n.BaseCost} vs {formula}");
        }
        // Not depth-derived: within one Age and one depth, costs still differ by content.
        var depthCost = new Dictionary<(string, int), double>();   // test-side check, not sim logic
        bool varies = false;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (depthCost.TryGetValue((n.Age, n.Depth), out double c) && c != n.BaseCost) varies = true;
            depthCost[(n.Age, n.Depth)] = n.BaseCost;
        }
        Assert.True(varies, "costs within one Age and depth never differ: they would be depth-derived");
        // The cheapest A9 node is cheaper than most A2 nodes: content, not Age, sets it.
        Assert.Equal(740.0, Canonical.Nodes[Canonical.IndexOfId("oral_rehydration")].BaseCost);
    }

    /// <summary>The D-047 re-scopes: the ONLY nodes whose name departs from the corpus.</summary>
    private static readonly Dictionary<string, string> D047Renamed = new(StringComparer.Ordinal)
    {
        ["fire_making"] = "Controlled-fire pyrotechnology",
        ["adhesive_natural"] = "Compound adhesives",
        ["hafting"] = "Composite hafting",
        ["fishing_hook"] = "Line and pelagic fishing",
        ["raft"] = "Sea-crossing raft",
        ["dugout"] = "Load-carrying dugout",
    };

    [Fact]
    public void Canonical_IsInSyncWithTheCorpusFileItWasMigratedFrom()
    {
        byte[] corpus = File.ReadAllBytes(Path.Combine(RepoPaths.Root(), "tech-graph-v0.6.json"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(corpus)), Canonical.CorpusSha256);
    }

    [Fact]
    public void Canonical_Eurekas_81OnEightyNodes_17EvaluableToday_AllAuthored_EveryNodeWithinTheFortyPercentCeiling()
    {
        int total = 0, nodes = 0, evaluable = 0, authored = 0, inherited = 0;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (n.Eurekas.Count > 0) nodes++;
            double sum = 0.0;
            foreach (ResearchEureka e in n.Eurekas)
            {
                total++;
                sum += e.Weight;
                if (e.EvaluableNow) { evaluable++; Assert.Equal("machine-evaluable", e.Class); }
                else Assert.Equal("future-system:" + e.System, e.Class);
                Assert.False(string.IsNullOrWhiteSpace(e.Justification), $"{n.Id}: '{e.Text}' lost its justification");
                if (e.Source == "authored") authored++; else inherited++;
            }
            Assert.True(sum <= 0.4 + 1e-12, n.Id);
            Assert.True(n.EurekaTotalWeight <= 0.4, n.Id);
        }
        Assert.Equal(81, total);
        Assert.Equal(80, nodes);
        // D-047 ruling 10: the 8 inherited Eurekas were rewritten as authored, node-specific
        // circumstances; arsenical_bronze's generic copper-stock condition became a future-system
        // arsenical-ore exposure, so 18 -> 17 evaluable.
        Assert.Equal(17, evaluable);
        Assert.Equal((81, 0), (authored, inherited));
        foreach (ResearchNode n in Canonical.Nodes)
            foreach (ResearchEureka e in n.Eurekas)
                Assert.DoesNotContain("inherited", e.Justification, StringComparison.Ordinal);
        // railway is the one node with two conditions: 0.2 each, together the full 0.40.
        ResearchNode railway = Canonical.Nodes[Canonical.IndexOfId("railway")];
        Assert.Equal([0.2, 0.2], [railway.Eurekas[0].Weight, railway.Eurekas[1].Weight]);
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
                bool[] done = ReachWithout(excluded);
                Assert.True(ResearchQuery.IsAvailable(Canonical, i, done, ResearchQuery.StageReached(Canonical, done)),
                    $"{Canonical.Nodes[i].Id}: no witness state");
                Assert.False(cond.Evaluate(null, a => done[a], null), $"{Canonical.Nodes[i].Id}: '{cond.Source}' holds in the witness");
                checkedConditions++;
            }
        Assert.Equal(15, checkedConditions);
    }

    /// <summary>The largest completed set reachable while every excluded node stays incomplete.</summary>
    private static bool[] ReachWithout(bool[] excluded)
    {
        var done = new bool[Canonical.Nodes.Count];
        for (bool changed = true; changed;)
        {
            changed = false;
            bool stage = ResearchQuery.StageReached(Canonical, done);
            for (int j = 0; j < done.Length; j++)
                if (!done[j] && !excluded[j] && ResearchQuery.IsAvailable(Canonical, j, done, stage))
                    done[j] = changed = true;
        }
        return done;
    }

    private static bool[] Excluding(params string[] ids)
    {
        var excluded = new bool[Canonical.Nodes.Count];
        foreach (string id in ids) excluded[Canonical.IndexOfId(id)] = true;
        return excluded;
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
    public void GroupB_AWorldWithOnlyChineseScript_ReachesTheResearchStage()
    {
        // Group B: where the real dependency is generic writing, law, numeration or record-keeping,
        // Chinese script is an alternative pathway — no cuneiform and no hieroglyphic needed.
        bool[] done = ReachWithout(Excluding("cuneiform", "hieroglyphic"));
        Assert.True(done[Canonical.IndexOfId("chinese_script")]);
        Assert.True(ResearchQuery.StageReached(Canonical, done));
    }

    [Theory]
    [InlineData("law_code")]
    [InlineData("census")]
    [InlineData("geometry_practical")]
    [InlineData("astronomy_records")]
    [InlineData("abacus")]
    public void GroupB_CuneiformFreePaths_ReachTheGenericRecordKeepingNodes(string id)
    {
        Assert.True(ReachWithout(Excluding("cuneiform"))[Canonical.IndexOfId(id)], $"{id} needs cuneiform");
    }

    [Theory]
    [InlineData("numeral_sexagesimal")]
    [InlineData("arithmetic_babylonian")]
    public void GroupB_TheBabylonianNodes_StillRequireCuneiform_SpecificDependenciesKept(string id)
    {
        Assert.False(ReachWithout(Excluding("cuneiform"))[Canonical.IndexOfId(id)], $"{id} is reachable without cuneiform");
        // And structurally: cuneiform is a MUST on every path, directly or through a must-held prerequisite.
        Assert.Contains(Canonical.IndexOfId("cuneiform"), MustClosure(Canonical.IndexOfId(id)));
    }

    /// <summary>Every node that holds on EVERY path to <paramref name="index"/>: the prerequisite's
    /// must-held atoms, closed transitively over their own prerequisites.</summary>
    private static SortedSet<int> MustClosure(int index)
    {
        var must = new SortedSet<int>();
        var stack = new Stack<int>();
        stack.Push(index);
        while (stack.Count > 0)
            if (Canonical.Nodes[stack.Pop()].Prerequisite is { } p)
                foreach (int a in p.MustHoldAtoms()) if (must.Add(a)) stack.Push(a);
        return must;
    }

    [Fact]
    public void Canonical_Newspaper_IsRepaired_ItRequiresThePrintingPressAlone()
    {
        int newspaper = Canonical.EntityIndexOf("inst.newspaper");
        Assert.Empty(Canonical.Entities[newspaper].Unresolved);
        Assert.Equal("printing_press", Canonical.Entities[newspaper].Requirement!.Source);
        var mask = new bool[Canonical.Nodes.Count];
        Assert.False(ResearchQuery.IsKnowledgeEligible(Canonical, newspaper, mask));
        mask[Canonical.IndexOfId("printing_press")] = true;
        Assert.True(ResearchQuery.IsKnowledgeEligible(Canonical, newspaper, mask));
    }

    [Fact]
    public void Baseline_GranaryAndWorkshop_AreConstructibleWithZeroTechnology()
    {
        // A null requirement is valid and always met (ADR-028 §3): no node owns these.
        var none = new bool[Canonical.Nodes.Count];
        foreach (string id in new[] { "building.granary", "building.workshop" })
        {
            int e = Canonical.EntityIndexOf(id);
            Assert.Null(Canonical.Entities[e].Requirement);
            Assert.True(ResearchQuery.IsKnowledgeEligible(Canonical, e, none), id);
        }
    }

    [Fact]
    public void Canonical_RecursiveSet_TheTenRepeatables_WaitForTheirOwnSubtreesFiniteResearch()
    {
        int recursive = 0, speculative = 0, finite = 0;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (n.IsRecursive) { recursive++; Assert.True(n.HasRepeatableDescriptor); Assert.True(n.Frontier); Assert.True(n.Branch >= 0); }
            else finite++;
            if (n.IsSpeculative) speculative++;
        }
        Assert.Equal(10, recursive);
        Assert.Equal(6, speculative);
        Assert.Equal(420, finite);
        Assert.Equal([36, 21, 124, 43, 14], [.. Canonical.FiniteNodesBySubtree.Select(l => l.Count)]);

        // frontier_launch (engineering): with EVERYTHING else complete it is available; leave one finite
        // engineering node incomplete and it is not — per-subtree exhaustion, nothing else.
        int launch = Canonical.IndexOfId("frontier_launch");
        var all = new bool[Canonical.Nodes.Count];
        for (int i = 0; i < all.Length; i++) all[i] = !Canonical.Nodes[i].IsRecursive;
        Assert.True(ResearchQuery.IsAvailable(Canonical, launch, all, true));
        all[Canonical.IndexOfId("jet_airliner")] = false;
        Assert.False(ResearchQuery.IsAvailable(Canonical, launch, all, true));
        // ...and a finite node of ANOTHER subtree does not hold it back.
        all[Canonical.IndexOfId("jet_airliner")] = true;
        all[Canonical.Nodes[Canonical.FiniteNodesBySubtree[1][0]].Index] = false;      // a medicine node
        Assert.True(ResearchQuery.IsAvailable(Canonical, launch, all, true));
    }

    // ------------------------------------------------------------------ D-047 (Civ VI reference and progression rulings)

    [Fact]
    public void D047_TheFiveFoundingActivities_RequireNoNode_AndAreEligibleWithZeroNodes()
    {
        var none = new bool[Canonical.Nodes.Count];
        foreach (string id in new[] { "activity.farming", "activity.herding", "activity.fishing", "activity.logging", "activity.mining" })
        {
            int e = Canonical.EntityIndexOf(id);
            Assert.True(e >= 0, id);
            Assert.Null(Canonical.Entities[e].Requirement);
            Assert.Empty(Canonical.Entities[e].NodeAtoms);
            Assert.True(ResearchQuery.IsKnowledgeEligible(Canonical, e, none), id);
            foreach (ResearchNode n in Canonical.Nodes)
                Assert.DoesNotContain(e, n.UnlockedEntities);                // the reverse index agrees
        }
        // The classes D-047 keeps technology-owned stay gated: fish weirs (ruling 5), improved
        // coastal/seagoing (sail) and oceanic transport (ruling 7), road classes (ruling 8).
        foreach (string gated in new[] { "infra.fish_weir", "activity.coastal_shipping", "activity.ocean_shipping",
                                         "infra.road_track", "infra.road_paved", "infra.road_macadam" })
            Assert.False(ResearchQuery.IsKnowledgeEligible(Canonical, Canonical.EntityIndexOf(gated), none), gated);
    }

    [Fact]
    public void D047_ResearchBuildsNoVessel_TheWatercraftNodesUnlockTransportClasses()
    {
        // Ruling 7: primitive -> improved coastal/seagoing (sail_square) -> oceanic. The coastal and
        // oceanic shipping activities are the classes; no node unlocks a building or unit for them.
        Assert.Equal("sail_square", Canonical.Entities[Canonical.EntityIndexOf("activity.coastal_shipping")].Requirement!.Source);
        foreach (string ocean in new[] { "caravel", "carrack", "polynesian_canoe" })
            Assert.Contains(Canonical.EntityIndexOf("activity.ocean_shipping"), Canonical.Nodes[Canonical.IndexOfId(ocean)].UnlockedEntities);
        Assert.Contains(Canonical.Nodes[Canonical.IndexOfId("sail_square")].Capabilities, c => c.Contains("coastal and seagoing transport class", StringComparison.Ordinal));
        // Ruling 8: the road nodes are infrastructure-CLASS unlocks, and none was deleted.
        foreach (string road in new[] { "track_road", "road_paved", "macadam" })
            Assert.All(Canonical.Nodes[Canonical.IndexOfId(road)].Capabilities, c => Assert.Contains("class", c, StringComparison.Ordinal));
    }

    /// <summary>The earliest year (CE positive, BCE negative) carrying an explicit era marker
    /// (CE, BCE, kya, Mya) in an <c>emerged</c> text; null when no date has one. A range
    /// "1860-1880 CE" counts both ends. The heuristic of gap audit G-34.</summary>
    internal static double? EarliestMarkedYear(string? emerged)
    {
        if (emerged is null) return null;
        double? best = null;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(emerged,
                     @"(\d[\d,]*(?:\.\d+)?)(?:\s*[-–/]\s*(\d[\d,]*(?:\.\d+)?))?\s*(BCE|CE|kya|Mya)\b"))
            for (int g = 1; g <= 2; g++)
            {
                if (!m.Groups[g].Success) continue;
                double v = double.Parse(m.Groups[g].Value.Replace(",", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);
                double y = m.Groups[3].Value switch { "CE" => v, "BCE" => -v, "kya" => -v * 1000.0, _ => -v * 1e6 };
                if (best is null || y < best) best = y;
            }
        return best;
    }

    [Fact]
    public void D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent()
    {
        // Ruling 1 (strict): a prerequisite must be causally and historically possible before the node
        // it gates. For every dependent dated after 3000 BCE, no must-held (AND) prerequisite may carry
        // an explicitly dated emergence later than the dependent's. No exemption is needed: the eleven
        // G-34 hits were fixed or re-scoped by D-047 (per-edge table there). Bare-year texts
        // (e.g. genome_sequencing "1977", pcr "1983") carry no marker and are reviewed in D-047 by hand.
        Assert.Equal(1895.0, EarliestMarkedYear("1895 CE"));
        Assert.Equal(-200.0, EarliestMarkedYear("~100 CE Alexandria; ~200 BCE China"));
        Assert.Equal(1860.0, EarliestMarkedYear("1860-1880 CE"));
        Assert.Equal(-44000.0, EarliestMarkedYear("~44 kya Lebombo; ~20 kya Ishango"));
        Assert.Null(EarliestMarkedYear("1977 Sanger; 2001 human genome"));
        var violations = new List<string>();
        int dated = 0, edges = 0;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (EarliestMarkedYear(n.Emerged) is not { } y) continue;
            dated++;
            if (y <= -3000.0 || n.Prerequisite is null) continue;
            foreach (int a in n.Prerequisite.MustHoldAtoms())
            {
                if (EarliestMarkedYear(Canonical.Nodes[a].Emerged) is not { } ya) continue;
                edges++;
                if (ya > y) violations.Add($"{n.Id} ({y}) <- {Canonical.Nodes[a].Id} ({ya})");
            }
        }
        Assert.Empty(violations);
        Assert.Equal(Canonical.Nodes.Count - 83, dated);      // the 83 marker-free texts are not tested
        Assert.True(edges > 300, $"only {edges} dated edges checked");
        // The two Director examples, explicitly.
        Assert.DoesNotContain(Canonical.IndexOfId("vacuum_tube"), MustClosure(Canonical.IndexOfId("xray")));
        Assert.DoesNotContain(Canonical.IndexOfId("germ_theory"), MustClosure(Canonical.IndexOfId("canning")));
    }

    /// <summary>Every node in any prerequisite of <paramref name="index"/> (AND and OR), transitively.</summary>
    private static SortedSet<int> AnyClosure(int index)
    {
        var all = new SortedSet<int>();
        var stack = new Stack<int>();
        stack.Push(index);
        while (stack.Count > 0)
            foreach (int a in Canonical.Nodes[stack.Pop()].PrerequisiteNodes) if (all.Add(a)) stack.Push(a);
        return all;
    }

    [Fact]
    public void D047_PenicillinsPrerequisiteClosure_ContainsNoSurgeryFamilyNode()
    {
        SortedSet<int> closure = AnyClosure(Canonical.IndexOfId("antibiotic_penicillin"));
        Assert.Contains(Canonical.IndexOfId("germ_theory"), closure);
        Assert.True(Canonical.Nodes.Count(n => n.Family == "surgery") >= 6);
        Assert.DoesNotContain(closure, i => Canonical.Nodes[i].Family == "surgery");
    }

    [Fact]
    public void D047_Generations_AreLocalToTheirFamily()
    {
        // D-044 R7: a generation is an index within its own family, and no prerequisite (direct or
        // transitive, AND or OR) of a generation-g node is a same-family node of generation >= g.
        int generational = 0;
        foreach (ResearchNode n in Canonical.Nodes)
        {
            if (n.Generation is not { } g) continue;
            generational++;
            Assert.False(string.IsNullOrEmpty(n.Family), $"{n.Id}: a generation without a family");
            Assert.True(g >= 1, n.Id);
            foreach (int a in AnyClosure(n.Index))
            {
                ResearchNode p = Canonical.Nodes[a];
                if (p.Family == n.Family && p.Generation is { } pg)
                    Assert.True(pg < g, $"{n.Id} (gen {g}) requires {p.Id} (gen {pg}) of the same family");
            }
        }
        Assert.True(generational > 50, $"only {generational} generational nodes");
        // The D-047 finance repair: bills of exchange came first, and double entry builds on them.
        Assert.Equal(1, Canonical.Nodes[Canonical.IndexOfId("bill_of_exchange")].Generation);
        Assert.Equal(2, Canonical.Nodes[Canonical.IndexOfId("double_entry")].Generation);
    }

    [Fact]
    public void D047_ArsenicalBronzeEureka_IsArsenicalOreExposure_NotAGenericCopperStockCondition()
    {
        ResearchNode n = Canonical.Nodes[Canonical.IndexOfId("arsenical_bronze")];
        ResearchEureka e = Assert.Single(n.Eurekas);
        Assert.Null(e.Condition);                                   // copper-ore stock is not arsenical evidence
        Assert.False(e.EvaluableNow);
        Assert.Equal("authored", e.Source);
        Assert.Equal("exposure", e.Kind);
        Assert.Contains("rsenic", e.Text, StringComparison.Ordinal);
        Assert.Contains("arsenic", e.Justification, StringComparison.Ordinal);
        Assert.DoesNotContain("stock_copper_ore", e.Text, StringComparison.Ordinal);
        Assert.True(e.Weight <= 0.4);
        // No Eureka anywhere still reads the generic copper-ore stock except copper smelting,
        // whose authored justification ties the ore to the discovery itself.
        foreach (ResearchNode m in Canonical.Nodes)
            foreach (ResearchEureka x in m.Eurekas)
                if (x.Condition is { } c && c.Source.Contains("stock_copper_ore", StringComparison.Ordinal))
                    Assert.Equal("copper_smelting", m.Id);
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
        mismatch.Technologies[10] = mismatch.Technologies[10] with { Eurekas = [new Eu("circumstance: timber", "stock_timber > 0", EvaluableNow: false)] };
        Rejects(mismatch, "evaluable_now must be true exactly when 'when' carries a condition");
    }

    [Fact]
    public void Rejects_ImpliedEurekas_AConditionEveryPathToTheNodeAlreadySatisfies()
    {
        // c = a AND b: "a" holds whenever c is available — a flat cost cut, not a circumstance.
        Spec onAnd = Standard();
        onAnd.Technologies[2] = onAnd.Technologies[2] with { Eurekas = [new Eu("circumstance: a", "a")] };
        Rejects(onAnd, "implied Eureka");
        // Transitively: e needs (a AND c) OR (b AND d); every path to code (law AND c) passes a.
        Spec transitive = Standard();
        transitive.Civics[1] = transitive.Civics[1] with { Eurekas = [new Eu("circumstance: a", "a")] };
        Rejects(transitive, "implied Eureka");
        // Behind the stage: mil needs only a, but the stage (c = a AND b) guarantees b too.
        Spec byStage = Standard();
        byStage.Technologies[5] = byStage.Technologies[5] with { Eurekas = [new Eu("circumstance: b", "b")] };
        Rejects(byStage, "implied Eureka");
        // d = a OR b: "a" is NOT guaranteed (b alone opens d), so it is a real circumstance.
        Spec onOr = Standard();
        onOr.Technologies[3] = onOr.Technologies[3] with { Eurekas = [new Eu("circumstance: a", "a")] };
        Assert.Single(onOr.Load().Nodes[3].Eurekas);
    }

    [Fact]
    public void Rejects_MalformedEurekas_ClassWeightCeilingJustificationAndSource()
    {
        static Spec With(params Eu[] eurekas)
        {
            Spec spec = Standard();
            spec.Technologies[10] = spec.Technologies[10] with { Eurekas = eurekas };
            return spec;
        }
        Rejects(With(new Eu("circumstance: timber", "stock_timber > 0", Class: "future-system:goods state")), "class 'future-system:goods state' must be 'machine-evaluable'");
        Rejects(With(new Eu("an obsidian source", null, System: "resources/terrain", Class: "machine-evaluable")), "must be 'future-system:resources/terrain'");
        Rejects(With(new Eu("circumstance: timber", "stock_timber > 0", Weight: 0.0)), "weight 0 must be in (0, 0.4]");
        Rejects(With(new Eu("circumstance: timber", "stock_timber > 0", Weight: 0.5)), "weight 0.5 must be in (0, 0.4]");
        Rejects(With(new Eu("t", "stock_timber > 0", Weight: 0.3), new Eu("s", "stock_stone > 0", Weight: 0.2)), "above the 0.4 acceleration ceiling");
        Rejects(With(new Eu("t", "stock_timber > 0", Weight: 0.2), new Eu("s", "stock_stone > 0")), "explicit on every Eureka of a node or on none");
        Rejects(With(new Eu("circumstance: timber", "stock_timber > 0", Justification: " ")), "justification is empty");
        Rejects(With(new Eu("circumstance: timber", "stock_timber > 0", Source: "invented")), "source 'invented' is not one of authored, inherited");
    }

    [Fact]
    public void Rejects_CostsThatAreNotContentDerived_AdjustmentsMagnitudeFormulaAndFactorRanges()
    {
        string json = Standard().Json();
        RejectsJson(Mutate(json, "\"calibration_adjustment\":0", "\"calibration_adjustment\":120"), "no calibration adjustment is permitted");
        // a costs 2500 = 50 x 2^5.64: change the stored cost alone and its content_cost no longer matches it.
        RejectsJson(Mutate(json, "\"depth\":0,\"cost\":2500", "\"depth\":0,\"cost\":2600"), "content_cost 2500 differs from the node's cost 2600");
        // Change both consistently and the FORMULA still rejects it: 2600 is not 50 x 2^5.64 to within rounding.
        RejectsJson(Mutate(Mutate(json, "\"depth\":0,\"cost\":2500", "\"depth\":0,\"cost\":2600"), "\"content_cost\":2500", "\"content_cost\":2600"),
            "is not U × K^magnitude");
        // Within the authored rounding (±5 RP here) the same change is accepted.
        ResearchContentLoader.Load(Mutate(Mutate(json, "\"depth\":0,\"cost\":2500", "\"depth\":0,\"cost\":2504"), "\"content_cost\":2500", "\"content_cost\":2504"), TestConfigs.Sim().Goods);
        // magnitude must be the sum of the six factors.
        RejectsJson(Mutate(json, "\"novelty\":2,", "\"novelty\":1.5,"), "is not the sum of its factors");
        RejectsJson(Mutate(json, "\"content_cost\":2500", "\"content_cost\":2490"), "content_cost 2490 differs");
        RejectsJson(Mutate(json, "\"novelty\":2,", "\"novelty\":2.5,"), "factors.novelty 2.5 is outside its range");
        RejectsJson(Mutate(json, "\"tier\":\"T2\"", "\"tier\":\"T9\""), "tier 'T9' is not one of");
    }

    [Fact]
    public void Rejects_UniversityRelevance_ThatIsNotADomainClassification()
    {
        string json = Standard().Json();
        RejectsJson(Mutate(json, "\"type\":3,\"name\":\"Engineering\",\"role\":\"primary\"", "\"type\":9,\"name\":\"Engineering\",\"role\":\"primary\""),
            "type 9 is not a university type key");
        RejectsJson(Mutate(json, "\"type\":3,\"name\":\"Engineering\",\"role\":\"primary\"", "\"type\":3,\"name\":\"Engineering\",\"role\":\"secondary\""),
            "names exactly one primary university domain");
        // mil is a military-subtree node: its primary must be the Military University's domain.
        RejectsJson(Mutate(json, "\"type\":1,\"name\":\"Military\",\"role\":\"primary\"", "\"type\":3,\"name\":\"Engineering\",\"role\":\"primary\""),
            "must serve its own subtree");
    }

    [Fact]
    public void Rejects_ResearchSets_RecursiveWithoutDescriptor_FiniteWaitingOnRecursive_AndAStaleCount()
    {
        Spec ok = Standard();
        ok.Technologies.Add(new Node(12, "eng_more", "eng", Branch: "engineering"));
        ok.Recursive.Add("eng_more");
        Assert.True(ok.Load().Nodes[11].IsRecursive);

        string json = ok.Json();
        RejectsJson(Mutate(json, "\"finite_nodes_to_exhaust\":1", "\"finite_nodes_to_exhaust\":2"), "disagrees with the 1 finite nodes of its subtree");
        RejectsJson(Mutate(json, "\"recursive\":[\"eng_more\"]", "\"recursive\":[]"), "exactly when it carries a repeatable descriptor");

        Spec waits = Standard();
        waits.Technologies.Add(new Node(12, "eng_more", "eng", Branch: "engineering"));
        waits.Technologies.Add(new Node(13, "eng_after", "eng_more", Branch: "engineering"));
        waits.Recursive.Add("eng_more");
        Rejects(waits, "requires the recursive node 'eng_more'");

        Spec trunk = Standard();
        trunk.Technologies.Add(new Node(12, "loop", "a"));
        trunk.Recursive.Add("loop");
        Rejects(trunk, "a recursive node must belong to a subtree");
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
            ("\"schema\":\"civ-sim/research@2\"", "\"schema\":null"),
            ("\"justification\":\"rig justification\"", "\"justification\":null"),
            ("\"tier\":\"T2\"", "\"tier\":null"),
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
        // Exponent 1 (research = population × constant) is forbidden: Research Points are sublinear.
        Spec linear = Standard();
        linear.RpExponent = 1.0;
        Rejects(linear, "exponent 1 must be in (0, 1)");
        // The stated anchors must agree with the coefficient and exponent (to 0.1 %).
        Spec stale = Standard();
        stale.RpAnchors = [(10_000, 1_100)];
        Rejects(stale, "not the stated anchor 1100");
        Spec noFloor = Standard();
        noFloor.FloorFraction = 0.0;
        Rejects(noFloor, "tuning.effectiveCostFloorFraction 0 must be in (0, 1]");
        Spec noCeiling = Standard();
        noCeiling.CeilingFraction = 1.5;
        Rejects(noCeiling, "tuning.accelerationCreditCeilingFraction 1.5 must be in (0, 1]");
        Spec sources = Standard();
        sources.CreditSources = ["eureka"];
        Rejects(sources, "tuning.accelerationCreditSources must be exactly [eureka, foreign_exposure]");
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
