using System.Text.Json.Nodes;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.Demo;
using Xunit;

namespace Sim.Ui.Tests.World;

/// <summary>The shipped content loads cleanly, and malformed content is a list of
/// diagnostics — never a crash, never a silently partial morphology (Trees 78e6b58 precedent).</summary>
public class WorldContentTests
{
    [Fact]
    public void ShippedMorphology_LoadsWithNoDiagnostics()
    {
        (WorldMorphology? m, IReadOnlyList<WorldDiagnostic> d) = WorldContentLoader.LoadMorphology(WorldTestKit.MorphologyJson);
        Assert.Empty(d);
        Assert.NotNull(m);
        Assert.NotNull(m!.Visual("university"));
        Assert.NotNull(m.Visual(WorldViewConstants.FallbackVisualType));
    }

    [Fact]
    public void ShippedDemo_LoadsWithNoDiagnostics_AndIsMarkedPlaceholder()
    {
        WorldMorphology m = WorldTestKit.Morph();
        (DemoWorldSource? s, IReadOnlyList<WorldDiagnostic> d) = DemoWorldSource.Load(WorldTestKit.DemoJson, m);
        Assert.Empty(d);
        Assert.NotNull(s);
        WorldSources b = s!.Sources();
        Assert.True(b.IsPlaceholder);
        Assert.True(b.Settlements.Current.IsPlaceholder);
        Assert.True(b.Agents.Current.IsPlaceholder);
        Assert.Contains("DEMO", s.Notice, StringComparison.Ordinal);
        foreach (SettlementReport r in b.Settlements.Current.Items) Assert.Contains("DEMO", r.Note, StringComparison.Ordinal);
    }

    /// <summary>Every task-listed category has a type: University, Hospital, Factory, Military
    /// Academy, Research Institute, Port, City (settlement stages), Army, Person, Band.</summary>
    [Fact]
    public void Content_CoversEveryRequiredCategory()
    {
        WorldMorphology m = WorldTestKit.Morph();
        foreach (string v in new[] { "university", "hospital", "factory", "military-academy", "research-institute" })
            Assert.NotNull(m.Visual(v));
        Assert.NotNull(m.Node("port")?.Glyph);
        Assert.Equal(5, m.Settlements.Stages.Count);
        foreach (string a in new[] { "army", "formation", "tank-formation", "fleet", "hero", "scientist", "engineer", "artist", "musician",
                     "leader", "band", "cultural-group", "expedition", "person" })
            Assert.NotNull(m.Agent(a));
    }

    private static IReadOnlyList<WorldDiagnostic> MorphWith(Action<JsonObject> edit)
    {
        JsonObject o = JsonNode.Parse(WorldTestKit.MorphologyJson)!.AsObject();
        edit(o);
        (WorldMorphology? m, IReadOnlyList<WorldDiagnostic> d) = WorldContentLoader.LoadMorphology(o.ToJsonString());
        Assert.Null(m);
        return d;
    }

    private static JsonObject Uni(JsonObject o) => o["visualTypes"]!.AsArray().Select(n => n!.AsObject()).First(v => (string?)v["id"] == "university");

    [Fact]
    public void Morphology_RejectsNonAscendingStages() =>
        Assert.Contains(MorphWith(o => Uni(o)["stages"]![2]!["min"] = 500), d => d.Message.Contains("ascending", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsAPartOutsideItsLot() =>
        Assert.Contains(MorphWith(o => Uni(o)["stages"]![0]!["adds"]![0]!["w"] = 1.4), d => d.Message.Contains("leaves its lot", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsATimeKeyedStageInput() =>
        Assert.Contains(MorphWith(o => Uni(o)["stageBy"] = new JsonObject { ["input"] = "established" }), d => d.Message.Contains("time", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsOverlappingSlots() =>
        Assert.Contains(MorphWith(o => o["layout"]!["slotRings"]![1]!["lots"] = 20), d => d.Message.Contains("overlap", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsUnknownGlyphNames() =>
        Assert.Contains(MorphWith(o => Uni(o)["glyph"]!["mark"] = "Figure"), d => d.Message.Contains("unknown GlyphDomain", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsNonLatin1Text() =>
        Assert.Contains(MorphWith(o => Uni(o)["name"] = "UniversΣty"), d => d.Message.Contains("Latin-1", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsAPersonDrawnAsABanner() =>
        Assert.Contains(MorphWith(o => o["agentTypes"]!.AsArray().First(a => (string?)a!["id"] == "hero")!["glyph"]!["base"] = "Standard"),
            d => d.Path.Contains("glyph.base", StringComparison.Ordinal));

    [Fact]
    public void Morphology_RejectsUnknownMembers_DuplicateKeys_AndNulls()
    {
        Assert.NotEmpty(MorphWith(o => o["surprise"] = 1));
        string dup = WorldTestKit.MorphologyJson.Replace("\"schema\": \"civ-sim/world-morphology@1\",",
            "\"schema\": \"civ-sim/world-morphology@1\", \"schema\": \"x\",", StringComparison.Ordinal);
        Assert.NotEmpty(WorldContentLoader.LoadMorphology(dup).Diagnostics);
        Assert.NotEmpty(MorphWith(o => o["polityInks"]!.AsArray().Add(null)));
    }

    private static IReadOnlyList<WorldDiagnostic> DemoWith(Action<JsonObject> edit)
    {
        JsonObject o = JsonNode.Parse(WorldTestKit.DemoJson)!.AsObject();
        edit(o);
        (DemoWorldSource? s, IReadOnlyList<WorldDiagnostic> d) = DemoWorldSource.Load(o.ToJsonString(), WorldTestKit.Morph());
        Assert.Null(s);
        return d;
    }

    private static JsonObject DemoEntity(JsonObject o, string section, string key) =>
        o[section]!.AsArray().Select(n => n!.AsObject()).First(e => (string?)e["key"] == key);

    [Fact]
    public void Demo_MustDeclarePlaceholder() =>
        Assert.Contains(DemoWith(o => o["placeholder"] = false), d => d.Path == "placeholder");

    [Fact]
    public void Demo_MayNotReportConstructionProgress() =>
        Assert.Contains(DemoWith(o => DemoEntity(o, "structures", "u-123")["timeline"]![0]!["inputs"]!["progress"] = 0.4),
            d => d.Message.Contains("progress", StringComparison.Ordinal));

    [Fact]
    public void Demo_RejectsDanglingReferences()
    {
        Assert.Contains(DemoWith(o => DemoEntity(o, "structures", "u-123")["settlement"] = "s-nowhere"), d => d.Message.Contains("unknown settlement", StringComparison.Ordinal));
        Assert.Contains(DemoWith(o => DemoEntity(o, "edges", "e-veyra-oskar")["a"] = "n-nowhere"), d => d.Message.Contains("unknown node", StringComparison.Ordinal));
        Assert.Contains(DemoWith(o => DemoEntity(o, "agents", "army-184")["polity"] = "p9"), d => d.Message.Contains("unknown polity", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_AnAgentIsInOnePlace()
    {
        Assert.Contains(DemoWith(o => DemoEntity(o, "agents", "army-184")["timeline"]![0]!["settlement"] = "s-veyra"),
            d => d.Message.Contains("one of them", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_RemovalMustBeLastAndNotFirst()
    {
        Assert.Contains(DemoWith(o => DemoEntity(o, "agents", "army-184")["timeline"]![0]!["removed"] = true),
            d => d.Message.Contains("first entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_DuplicateStepEntriesAreRejected() =>
        Assert.Contains(DemoWith(o => DemoEntity(o, "agents", "army-184")["timeline"]!.AsArray().Add(new JsonObject { ["step"] = 0, ["count"] = 7 })),
            d => d.Message.Contains("same step", StringComparison.Ordinal));
}
