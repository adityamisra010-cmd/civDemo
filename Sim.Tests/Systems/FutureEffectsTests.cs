using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// CR-025 ruling / D-063 R1 amendment (2026-10-26) — DECLARED FUTURE EFFECTS (research.json <c>effects.future</c>): the
/// loader's schema and refusals, the shipped declarations parsed exactly, and the property that matters most — a future
/// effect is NEVER a realized effect: it enters no operand index, no Σ, no snapshot, and moves no world. The research
/// card's rendering ("Future (M7 …): …, not simulated in this build", never Simulated) is InfoQueryTests.
/// </summary>
public class FutureEffectsTests
{
    private static JsonObject Entry(string milestone = "M7", string system = "Battle Layer - mounted movement",
        string? operand = null, string description = "riders move faster") =>
        new() { ["milestone"] = milestone, ["system"] = system, ["operand"] = operand, ["description"] = description };

    /// <summary>The pre-tranche-2 document (no realized effect) with <paramref name="entries"/> as technology 0's
    /// effects.future.</summary>
    private static string WithFuture(params JsonObject[] entries)
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        var list = new JsonArray();
        foreach (JsonObject e in entries) list.Add(e);
        doc["technologies"]![0]!["effects"]!["future"] = list;
        return doc.ToJsonString();
    }

    private static ResearchContentException Refused(string json) =>
        Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(json));

    [Fact]
    public void AValidDeclaration_Loads_AndIsExposedOnTheNode()
    {
        ResearchContent c = KnowledgeEffectsRigs.Load(WithFuture(
            Entry(),
            Entry("unscheduled", "freight-time consumer", "roads.baselineKmPerDay", "light vehicles shorten transit")));
        FutureEffect[] f = [.. c.Nodes[0].FutureEffects];
        Assert.Equal(2, f.Length);
        Assert.Equal(new FutureEffect("M7", "Battle Layer - mounted movement", null, "riders move faster"), f[0]);
        Assert.Equal(new FutureEffect("unscheduled", "freight-time consumer", KnowledgeOperand.RoadsBaselineKmPerDay, "light vehicles shorten transit"), f[1]);
        Assert.Empty(c.Nodes[1].FutureEffects);   // absent = the empty list
    }

    [Fact]
    public void TheLoader_RefusesAnUnknownMilestone()
    {
        foreach (string m in new[] { "M6", "M5", "m7", "M12", "", "later" })
            Assert.Contains("must be one of M7, M8, M9, M10, M11, unscheduled", Refused(WithFuture(Entry(milestone: m))).Message);
    }

    [Fact]
    public void TheLoader_RefusesABlankSystemOrDescription()
    {
        Assert.Contains("system is empty", Refused(WithFuture(Entry(system: " "))).Message);
        Assert.Contains("description is empty", Refused(WithFuture(Entry(description: ""))).Message);
    }

    [Fact]
    public void TheLoader_RefusesAnUnregisteredOperand_AndAnOperandConsumedInPlay()
    {
        Assert.Contains("is not a registered operand", Refused(WithFuture(Entry(operand: "roads.notAnOperand"))).Message);
        // An operand a live system reads: the effect would be realized today, so it must be declared realized.
        string e = Refused(WithFuture(Entry(operand: "catchment.hinterlandRadiusKm"))).Message;
        Assert.Contains("is consumed in play by CatchmentSystem", e);
        Assert.Contains("effects.future[0]", e);
    }

    [Fact]
    public void TheLoader_RefusesAMagnitude_AMissingOperandMember_AndASystemTwice()
    {
        JsonObject withDelta = Entry();
        withDelta["delta"] = 0.04;   // a future effect never carries a magnitude
        Refused(WithFuture(withDelta));
        JsonObject noOperand = Entry();
        noOperand.Remove("operand");   // operand is required, explicitly null when there is none
        Refused(WithFuture(noOperand));
        Assert.Contains("appears twice in one node", Refused(WithFuture(Entry(), Entry(milestone: "M8"))).Message);
    }

    [Fact]
    public void TheShippedDeclarations_AreTheirJson_EntryForEntry()
    {
        JsonObject doc;
        using (Stream s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        ResearchContent c = TestConfigs.Research();
        int declared = 0, nodes = 0;
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? node in doc[tree]!.AsArray())
            {
                ResearchNode parsed = c.Nodes[c.IndexOfId(node!["id"]!.GetValue<string>())];
                JsonArray? list = node["effects"]!["future"] as JsonArray;
                Assert.Equal(list?.Count ?? 0, parsed.FutureEffects.Count);
                if (list is null) continue;
                nodes++;
                for (int k = 0; k < list.Count; k++)
                {
                    FutureEffect f = parsed.FutureEffects[k];
                    Assert.Equal(list[k]!["milestone"]!.GetValue<string>(), f.Milestone);
                    Assert.Equal(list[k]!["system"]!.GetValue<string>(), f.System);
                    Assert.Equal(list[k]!["description"]!.GetValue<string>(), f.Description);
                    Assert.Equal(list[k]!["operand"]?.GetValue<string>(), f.Operand is { } op ? KnowledgeOperands.Of(op).Id : null);
                    declared++;
                }
            }
        Assert.Equal(23, nodes);      // the six future-dependent nodes of the CR-025 ruling + the 17 of its §8 gap audit
        Assert.Equal(26, declared);   // 8 + 18 (the 17 gap-audit nodes; rocket declares two)
    }

    /// <summary>A future effect is never realized: a content whose ONLY declarations are future effects — naming the
    /// unconsumed operand among them — has no realized effect, an empty operand index, the inert snapshot, and every
    /// operand at its base for a holder that completed every node.</summary>
    [Fact]
    public void AFutureEffect_IsNeverRealized_NoIndexNoSigmaNoSnapshot()
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        doc["tuning"]!["knowledgeEffects"] = true;
        JsonArray techs = doc["technologies"]!.AsArray();
        for (int k = 0; k < 5; k++)
            techs[k]!["effects"]!["future"] = new JsonArray(
                Entry("unscheduled", "freight-time consumer", "roads.baselineKmPerDay", "faster"), Entry("M8", "alliances", null, "joint command"));
        ResearchContent c = KnowledgeEffectsRigs.Load(doc.ToJsonString());
        Assert.Equal(2, c.Nodes[0].FutureEffects.Count);
        Assert.False(c.HasRealizedEffects);
        for (int op = 0; op < KnowledgeOperands.Count; op++) Assert.Empty(c.NodesWith((KnowledgeOperand)op));

        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        var p = new PolityId(1);
        foreach (ResearchNode n in c.Nodes) w.ResearchCompleted.Add(new ResearchCompletedRow(p, n.Key));
        Assert.Same(KnowledgeEffectsSnapshot.Inert, KnowledgeEffects.Snapshot(w, c));
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
        {
            Assert.Equal(0.0, KnowledgeEffects.Improvement(w, c, p, info.Operand));
            Assert.Equal(2.5, KnowledgeEffects.Apply(w, c, p, info.Operand, 2.5));
        }
    }

    /// <summary>On the SHIPPED content: completing every future-declaring node adds nothing to any operand's Σ (their
    /// effects lists are empty), so the holder's improvements equal those without them, operand for operand.</summary>
    [Fact]
    public void TheShippedFutureNodes_AddNothingToAnySigma()
    {
        ResearchContent c = TestConfigs.Research();
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        var p = new PolityId(1);
        WorldState with = w.Clone();
        int granted = 0;
        foreach (ResearchNode n in c.Nodes)
            if (n.FutureEffects.Count > 0 && !ResearchQuery.IsCompleted(with, p, n.Key))
            {
                with.ResearchCompleted.Add(new ResearchCompletedRow(p, n.Key));
                granted++;
            }
        Assert.Equal(23, granted);
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            Assert.Equal(KnowledgeEffects.Improvement(w, c, p, info.Operand), KnowledgeEffects.Improvement(with, c, p, info.Operand));
    }
    /// <summary>
    /// CR-025 ruling — IN PLAY, on the canonical founded world (seed 42, one AI empire) with the AI running 40 turns, every
    /// holder having completed the 8 retargeted and every future-declaring node (the 6 of the ruling and, since CR-025 §8,
    /// the 17 of the gap audit; granted: the canonical AI reaches none of the ruling's 17 in 300 turns, CR-025 §2.3). Three contents run the same granted world: shipped; shipped with every
    /// effects.future removed; shipped with the 8 retargeted realized effects removed (the pre-ruling content's behaviour:
    /// before the ruling those 8 deltas sat on an operand no system consumes). Removing the future declarations changes
    /// nothing, hash for hash; removing the retargets changes the world — the retargets are real gameplay, the future
    /// declarations are not. The third content is the attribution twin of any pin the retargets move.
    /// </summary>
    [Fact]
    public void InPlay_TheRetargetsChangeTheWorld_AndTheFutureDeclarationsDoNot()
    {
        string[] retargets = ["horse_domestication", "horseshoe", "internal_combustion", "bridge_stone", "bridge_trusts",
            "turnpike_trusts", "sprung_coach", "road_houses"];
        SimConfig shipped = TestConfigs.Sim();
        ResearchContent c = shipped.Research!;
        SimConfig noFuture = shipped with { Research = Edited(doc => Each(doc, (id, effects) => effects.Remove("future"))) };
        SimConfig noRetargets = shipped with
        {
            Research = Edited(doc => Each(doc, (id, effects) => { if (Array.IndexOf(retargets, id) >= 0) effects.Remove("realized"); })),
        };
        Assert.Empty(noRetargets.Research!.Nodes[noRetargets.Research.IndexOfId("sprung_coach")].RealizedEffects);
        Assert.Empty(noFuture.Research!.Nodes[noFuture.Research.IndexOfId("saddle")].FutureEffects);

        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, shipped, 42);
        var holders = new List<PolityId>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            PolityId h = SettlementKnowledge.HolderOf(w, w.Settlements[i].Id);
            if (!holders.Contains(h)) holders.Add(h);
        }
        Assert.True(holders.Count >= 2);
        int granted = 0;
        foreach (PolityId h in holders)
            foreach (ResearchNode n in c.Nodes)
                if ((n.FutureEffects.Count > 0 || Array.IndexOf(retargets, n.Id) >= 0) && !ResearchQuery.IsCompleted(w, h, n.Key))
                {
                    w.ResearchCompleted.Add(new ResearchCompletedRow(h, n.Key));
                    granted++;
                }
        Assert.Equal(31 * holders.Count, granted);   // 8 retargets + 6 CR-025 future nodes + 17 gap-audit future nodes

        const int turns = 40;
        string live = WorldHash.ComputeHex(RunAi(w.Clone(), shipped, turns));
        Assert.Equal(live, WorldHash.ComputeHex(RunAi(w.Clone(), noFuture, turns)));
        Assert.NotEqual(live, WorldHash.ComputeHex(RunAi(w.Clone(), noRetargets, turns)));
    }

    /// <summary>
    /// CR-025 §8 (2026-10-26) — THE GAP-AUDIT DECLARATIONS ARE GAMEPLAY-INERT, IN PLAY (the CR-025 twin method). The
    /// canonical founded world (seed 42, one AI empire), every holder granted the 17 gap-audit nodes plus a control node
    /// (sanitary_engineering, whose only effect is a realized demographics.mortalityPerYear delta), the AI running 40 turns
    /// under three contents: shipped; shipped with the 17 nodes' effects.future removed — which IS the pre-audit content,
    /// the declarations being the audit's only content change; and shipped with the control's realized effect removed.
    /// The first two give the same world hash; the third differs, so the method has teeth.
    /// </summary>
    [Fact]
    public void InPlay_TheGapAuditDeclarations_ChangeNoWorld()
    {
        const string control = "sanitary_engineering";
        string[] gaps = [.. Tranche2ContentTests.GapAuditDisposition.Select(g => g.Id)];
        Assert.Equal(17, gaps.Length);
        SimConfig shipped = TestConfigs.Sim();
        ResearchContent c = shipped.Research!;
        SimConfig preAudit = shipped with
        {
            Research = Edited(doc => Each(doc, (id, effects) => { if (Array.IndexOf(gaps, id) >= 0) Assert.True(effects.Remove("future"), id); })),
        };
        SimConfig noControl = shipped with { Research = Edited(doc => Each(doc, (id, effects) => { if (id == control) effects.Remove("realized"); })) };
        foreach (string id in gaps)
        {
            Assert.NotEmpty(c.Nodes[c.IndexOfId(id)].FutureEffects);
            Assert.Empty(preAudit.Research!.Nodes[preAudit.Research.IndexOfId(id)].FutureEffects);
        }
        Assert.Equal("demographics.mortalityPerYear", KnowledgeOperands.Of(Assert.Single(c.Nodes[c.IndexOfId(control)].RealizedEffects).Operand).Id);
        Assert.Empty(noControl.Research!.Nodes[noControl.Research.IndexOfId(control)].RealizedEffects);

        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, shipped, 42);
        var holders = new List<PolityId>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            PolityId h = SettlementKnowledge.HolderOf(w, w.Settlements[i].Id);
            if (!holders.Contains(h)) holders.Add(h);
        }
        Assert.True(holders.Count >= 2);
        int granted = 0;
        foreach (PolityId h in holders)
            foreach (string id in (string[])[.. gaps, control])
            {
                ResearchNodeId key = c.Nodes[c.IndexOfId(id)].Key;
                Assert.False(ResearchQuery.IsCompleted(w, h, key), id + " is complete at founding");
                w.ResearchCompleted.Add(new ResearchCompletedRow(h, key));
                granted++;
            }
        Assert.Equal(18 * holders.Count, granted);

        const int turns = 40;
        string live = WorldHash.ComputeHex(RunAi(w.Clone(), shipped, turns));
        Assert.Equal(live, WorldHash.ComputeHex(RunAi(w.Clone(), preAudit, turns)));
        Assert.NotEqual(live, WorldHash.ComputeHex(RunAi(w.Clone(), noControl, turns)));
    }

    private static ResearchContent Edited(Action<JsonObject> edit)
    {
        JsonObject doc;
        using (Stream s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        edit(doc);
        return KnowledgeEffectsRigs.Load(doc.ToJsonString());
    }

    private static void Each(JsonObject doc, Action<string, JsonObject> onEffects)
    {
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? node in doc[tree]!.AsArray())
                onEffects(node!["id"]!.GetValue<string>(), node["effects"]!.AsObject());
    }

    private static WorldState RunAi(WorldState w, SimConfig cfg, int turns)
    {
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            w = exec.Step(w);
        }
        return w;
    }
}
