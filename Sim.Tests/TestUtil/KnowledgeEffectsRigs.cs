using System.Globalization;
using System.Text.Json.Nodes;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Tests.TestUtil;

/// <summary>
/// ADR-047 — research contents that DECLARE realized effects, built from the shipped research.json (which ships none in
/// tranche 2). Each is the shipped document with <c>effects.realized</c> written onto chosen nodes; the ON and OFF twins
/// differ ONLY in <c>tuning.knowledgeEffects</c>, so a test comparing them on one world sees the realized effects and
/// nothing else (the nodes' other unlocks are identical in both arms).
/// </summary>
internal static class KnowledgeEffectsRigs
{
    /// <summary>The delta every probe effect carries (the contract's maximum).</summary>
    public const double ProbeDelta = 0.5;

    /// <summary>
    /// THE PROBE NODE of each operand: the LATE technologies (content order, from the last backwards), one per operand —
    /// no early knowledge closure (trade, taxation, recipes) and no three-turn AI run reaches them, so a fixture's own
    /// rows never carry a probe effect. fog.formationSightNodes gets a SECOND probe (<see cref="ExtraSightProbe"/>): its
    /// integer base 1 × 1.5 floors back to 1, so a visible step needs Σ = 1.0.
    /// </summary>
    public static string ProbeNode(KnowledgeOperand op) => ProbeIds.Value[(int)op];

    /// <summary>The second fog.formationSightNodes probe (see <see cref="ProbeNode"/>).</summary>
    public static string ExtraSightProbe => ProbeIds.Value[KnowledgeOperands.Count];

    private static readonly Lazy<string[]> ProbeIds = new(() =>
    {
        JsonArray techs = ShippedDocument()["technologies"]!.AsArray();
        var ids = new string[KnowledgeOperands.Count + 1];
        for (int k = 0; k < ids.Length; k++) ids[k] = techs[techs.Count - 1 - k]!["id"]!.GetValue<string>();
        return ids;
    });

    /// <summary>The shipped content with one probe effect (delta <see cref="ProbeDelta"/>) per operand, switch ON.</summary>
    public static ResearchContent ProbeOn => ProbeOnContent.Value;

    /// <summary>The same document with tuning.knowledgeEffects false (the attribution twin of <see cref="ProbeOn"/>).</summary>
    public static ResearchContent ProbeOff => ProbeOffContent.Value;

    private static readonly Lazy<ResearchContent> ProbeOnContent = new(() => Load(ProbeJson(on: true)));
    private static readonly Lazy<ResearchContent> ProbeOffContent = new(() => Load(ProbeJson(on: false)));

    /// <summary>The fixture config: shipped sim.json without the pre-cultivation (forager) layer — every settlement farms
    /// CULTIVATED grain, the crop the farming and granary operands improve — carrying <paramref name="research"/>.</summary>
    public static SimConfig Cfg(ResearchContent research) =>
        SingleSwitchTwins.PreForagerOnly(TestConfigs.Sim()) with { Research = research };

    public static string ProbeJson(bool on)
    {
        JsonObject doc = ShippedDocument();
        JsonArray techs = doc["technologies"]!.AsArray();
        for (int k = 0; k < KnowledgeOperands.Count; k++)
            SetRealized(techs[techs.Count - 1 - k]!.AsObject(), (KnowledgeOperands.All[k].Id, ProbeDelta));
        SetRealized(techs[techs.Count - 1 - KnowledgeOperands.Count]!.AsObject(), ("fog.formationSightNodes", ProbeDelta));
        doc["tuning"]!["knowledgeEffects"] = on;
        return doc.ToJsonString();
    }

    /// <summary>A POPULATED content: realized effects on the FIRST technologies (early nodes an AI completes within a
    /// short run), several operands per node, deltas in the content-design range — for the replay and identity runs.</summary>
    public static string EarlyJson(bool on)
    {
        JsonObject doc = ShippedDocument();
        JsonArray techs = doc["technologies"]!.AsArray();
        JsonArray civics = doc["civics"]!.AsArray();
        for (int k = 0; k < KnowledgeOperands.Count; k++)
        {
            // Two operands per node across the first technologies, and every operand once more on an early civic.
            SetRealized(techs[k / 2]!.AsObject(), Append(techs[k / 2]!.AsObject(), KnowledgeOperands.All[k].Id, 0.02 + 0.01 * (k % 7)));
            SetRealized(civics[k % civics.Count]!.AsObject(), Append(civics[k % civics.Count]!.AsObject(), KnowledgeOperands.All[k].Id, 0.05));
        }
        doc["tuning"]!["knowledgeEffects"] = on;
        return doc.ToJsonString();
    }

    public static ResearchContent Load(string json)
    {
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(json, GoodsConfigLoader.Load(goods));
    }

    public static JsonObject ShippedDocument()
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        return JsonNode.Parse(stream)!.AsObject();
    }

    /// <summary>Writes <paramref name="effects"/> as the node's effects.realized (effects.immediate kept).</summary>
    public static void SetRealized(JsonObject node, params (string Operand, double Delta)[] effects)
    {
        var list = new JsonArray();
        foreach ((string operand, double delta) in effects)
            list.Add(new JsonObject { ["operand"] = operand, ["delta"] = delta });
        node["effects"]!["realized"] = list;
    }

    /// <summary>A node's current realized effects plus one more (for nodes that collect several).</summary>
    private static (string, double)[] Append(JsonObject node, string operand, double delta)
    {
        var result = new List<(string, double)>();
        if (node["effects"]!["realized"] is JsonArray existing)
            foreach (JsonNode? e in existing)
                result.Add((e!["operand"]!.GetValue<string>(), e["delta"]!.GetValue<double>()));
        foreach ((string o, _) in result) if (o == operand) return [.. result];
        result.Add((operand, delta));
        return [.. result];
    }

    /// <summary>Completes the node with content id <paramref name="id"/> for <paramref name="holder"/> (a
    /// ResearchCompleted row, exactly what ResearchSystem writes).</summary>
    public static void Grant(WorldState w, ResearchContent content, PolityId holder, string id)
    {
        ResearchNodeId key = content.Nodes[content.IndexOfId(id)].Key;
        if (!ResearchQuery.IsCompleted(w, holder, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(holder, key));
    }

    public static string Inv(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
