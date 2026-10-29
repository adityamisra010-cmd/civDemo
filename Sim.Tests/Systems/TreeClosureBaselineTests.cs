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
    // tree-closure.md §2 records these values and the commit they were taken at; §5 records each approved change.
    // TC-2 (RC-1, Director-authorized 2026-10-27, docs/m7-plan/aviation-progression.md §10): `aircraft` content Age A9 -> A8,
    // its only STRUCTURE field that moved (TEXT unchanged; its cost is outside both hashes). TC-1 STRUCTURE was
    // 918502844849f4378fe8a0e06c118bd1c59c476e61400909e852ed12e4b9d1d0 (AircraftAgeA9_ReproducesTheTc1Structure proves the
    // attribution: restoring that one field restores the TC-1 hash).
    internal const string Tc1StructureSha256 = "918502844849f4378fe8a0e06c118bd1c59c476e61400909e852ed12e4b9d1d0";
    internal const string StructureSha256 = "489dc44e73c6de6b2a1419abf609371e6a0110a84d6e24e6c66af17f446fa702";
    internal const string TextSha256 = "de341a8eb3a7770ef36ee2585342a2baadf9c131b602b93f8af19a054d1e6c39";

    internal static (string Structure, string Text) Hashes()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        return Hashes(doc);
    }

    internal static (string Structure, string Text) Hashes(JsonObject doc)
    {
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

    /// <summary>TC-2 attribution: the shipped content with `aircraft` back at content Age A9 (its cost and cost rationale are
    /// outside the hash) reproduces the TC-1 STRUCTURE hash, so RC-1's Age retag is the ONE structural change.</summary>
    [Fact]
    public void AircraftAgeA9_ReproducesTheTc1Structure_RC1IsTheOnlyStructuralChange()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        JsonNode aircraft = doc["technologies"]!.AsArray().Single(n => (string?)n!["id"] == "aircraft")!;
        Assert.Equal("A8", (string?)aircraft["age"]);
        Assert.Equal("internal_combustion AND glider", (string?)aircraft["prereq"]);   // RC-1 changes no prerequisite
        aircraft["age"] = "A9";
        Assert.Equal(Tc1StructureSha256, Hashes(doc).Structure);
        Assert.Equal(TextSha256, Hashes(doc).Text);
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
