using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// TREE CLOSURE (docs/m6-plan/knowledge-redesign/tree-closure.md; the Director's closure checklist, 2026-10-26): the
/// technology and civics trees are frozen as a CONTENT milestone. Two hashes of the shipped research.json pin it:
/// <list type="bullet">
/// <item>STRUCTURE — every node's id, key, tree, content Age, prerequisite expression, unlocks, effects (immediate,
///   realized, future) and repeatable descriptor, in file order, plus the entity list. Any change here is a node-content
///   or dependency change: allowed only as a defect fix or an explicitly approved change, recorded in tree-closure.md
///   with the new hash.</item>
/// <item>TEXT — every node's name and description. Kept apart so an educational-text correction is visibly text-only.</item>
/// </list>
/// Costs and tuning (pacing scale, switches) are NOT in the closure hashes: they belong to the research-economy
/// configuration, whose default adoption is a separate decision (research-cost-pass/decision-docket.md).
/// </summary>
public sealed class TreeClosureBaselineTests
{
    // tree-closure.md §2 records these values and the commit they were taken at.
    internal const string StructureSha256 = "918502844849f4378fe8a0e06c118bd1c59c476e61400909e852ed12e4b9d1d0";
    internal const string TextSha256 = "de341a8eb3a7770ef36ee2585342a2baadf9c131b602b93f8af19a054d1e6c39";

    internal static (string Structure, string Text) Hashes()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        var structure = new JsonArray();
        var text = new JsonArray();
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? n in doc[tree]!.AsArray())
            {
                structure.Add(new JsonArray(tree, n!["id"]!.DeepClone(), n["key"]!.DeepClone(), n["age"]!.DeepClone(),
                    n["prereq"]?.DeepClone(), n["unlocks"]!.DeepClone(), n["effects"]!.DeepClone(), n["repeatable"]?.DeepClone()));
                text.Add(new JsonArray(n["id"]!.DeepClone(), n["name"]!.DeepClone(), n["desc"]!.DeepClone()));
            }
        structure.Add(doc["entities"]!.DeepClone());
        return (Sha(structure.ToJsonString()), Sha(text.ToJsonString()));
    }

    private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public void ShippedTrees_MatchTheClosureBaseline_Structure()
    {
        Assert.Equal(StructureSha256, Hashes().Structure);
    }

    [Fact]
    public void ShippedTrees_MatchTheClosureBaseline_Text()
    {
        Assert.Equal(TextSha256, Hashes().Text);
    }

    [Fact]
    public void ClosureHashes_IgnoreCostsAndTuning_ButSeeAPrerequisiteChange()
    {
        // the projection itself: a cost or tuning edit leaves both hashes; a prerequisite edit moves STRUCTURE only
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        string Project(JsonObject d)
        {
            var a = new JsonArray();
            foreach (string tree in new[] { "technologies", "civics" })
                foreach (JsonNode? n in d[tree]!.AsArray())
                    a.Add(new JsonArray(n!["id"]!.DeepClone(), n["prereq"]?.DeepClone(), n["unlocks"]!.DeepClone()));
            return Sha(a.ToJsonString());
        }
        string before = Project(doc);
        var costEdit = doc.DeepClone().AsObject();
        costEdit["technologies"]![3]!["cost"] = 1.0;
        costEdit["tuning"]!["researchOverflow"] = true;
        Assert.Equal(before, Project(costEdit));
        var prereqEdit = doc.DeepClone().AsObject();
        prereqEdit["technologies"]![3]!["prereq"] = "fire_making";
        Assert.NotEqual(before, Project(prereqEdit));
    }
}
