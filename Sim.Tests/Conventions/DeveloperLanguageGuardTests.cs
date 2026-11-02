using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Conventions;

/// <summary>
/// Director, 2026-11-02 (tree-closure.md §5, TC-4): THE RESEARCH TREES' PLAYER-VISIBLE TEXT CARRIES NO DEVELOPER
/// LANGUAGE. The research panel and the info card paint, for every node of <c>Sim.Data/content/research.json</c>, its
/// <c>name</c> and <c>desc</c>, its <c>unlocks.capabilities</c>, <c>unlocks.techniques</c> and
/// <c>unlocks.applications</c>, each Idea's <c>text</c> (the code calls it a Eureka, D-069 R4), each declared future
/// effect's <c>description</c>, and the <c>name</c> of every entity a node opens. None of them may carry decision or ADR
/// ids, ruling numbers, registry or reclassification notes, entity or code ids, section signs, config keys, milestone
/// ids, "owning system" or "-class unlock" phrasing, the old civic template, or ALL-CAPS emphasis. Developer notes live
/// in <c>docs/</c> (docs/m6-plan/research-description-notes.md).
///
/// WHAT IT DOES NOT SCAN, stated plainly:
/// <list type="bullet">
/// <item>A future effect's <c>system</c> and <c>milestone</c>: the info card prints them as a label by design
///   ("Future (M7 Battle Layer - chariots): …", CR-025 ruling; pinned by InfoQueryTests), so a milestone id there is
///   the format, not a leak.</item>
/// <item>An Idea's <c>when</c> condition: it is an expression the info card translates (InfoQuery.ConditionReading).</item>
/// <item>The <c>baseline</c> section's <c>providedBy</c> notes, <c>_doc</c> strings, cost rationales and every other
///   non-node field: they are not node text.</item>
/// </list>
/// THE STRUCTURE-PINNED EXCEPTIONS: <c>unlocks</c> and <c>effects</c> are inside the tree-closure STRUCTURE hash, which
/// the Director's 2026-11-02 instruction did not open. The developer language that sits in those fields is listed in
/// <see cref="StructurePinnedExceptions"/> exactly, so that it is visible, cannot grow (any NEW hit fails), and cannot go
/// stale (an entry that no longer matches, or no longer exists, fails). Clearing it needs an approved STRUCTURE change.
/// </summary>
public class DeveloperLanguageGuardTests
{
    /// <summary>The rules: a name, a pattern, and a sample the teeth test proves it flags.</summary>
    internal static readonly (string Name, string Pattern, string Sample)[] Rules =
    [
        ("decision or ADR id",       @"\b(?:D|DD|ADR|CR|RC|TC|EC)-\d+",                                  "(D-047 ruling 2)"),
        ("ruling number",            @"(?i:\brulings?\s+\d)",                                              "per rulings 4 and 6"),
        ("registry / reclassified",  @"(?i:\breclassified\b|\bregistry\s+institution\b)",                 "Reclassified from registry institution"),
        ("entity id",                @"\b(?:inst|infra|unit|building|activity|project|baseline)\.[a-z][a-z0-9_]*\b", "inst.law_code"),
        ("section sign",             @"§",                                                                  "architecture §5.7"),
        ("architecture reference",   @"(?i:\(architecture\b)",                                             "(architecture 5.7)"),
        ("TUNE marker",              @"\bTUNE\b",                                                           "a TUNE value"),
        ("baseline jargon",          @"(?i:\b(?:is|are)\s+baseline\b|\bbaseline\s*\()",                   "Simple tools are baseline"),
        ("owning-system jargon",     @"(?i:\b(?:owning|realizing|realising|consuming|construction)\s+systems?\b)", "established by its owning system"),
        ("class-unlock jargon",      @"(?i:-class\s+unlock\b|\bunlocks?\s+the\s+[a-z ]*?class\b|\bunlock:)", "An infrastructure-class unlock"),
        ("milestone id",             @"\(M\d{1,2}\)|\b(?:before|after|until|in|with|from)\s+M\d{1,2}\b",   "arrives with the government layer (M8)"),
        ("layer jargon",             @"(?i:\b(?:government|battle|politics|society|diplomacy)\s+layer\b)", "the government layer"),
        ("civic template",           @"(?i:the\s+civic\s+knowledge\s+of\s+this\s+form\s+of\s+organi[sz]ation)", "X: the civic knowledge of this form of organization."),
        ("node jargon",              @"(?i:\bnodes?\b)",                                                    "needs no hook or node"),
        ("snake_case code id",       @"\b[a-z][a-z0-9]*_[a-z0-9_]+\b",                                      "requires track_road AND stone_dry"),
        ("config key",               @"\b[a-z]+\.[a-z]+[A-Z]\w*|\.json\b",                                  "(sim.json governance.taxationMinAge)"),
        ("code identifier",          @"\b[A-Z][a-z]+(?:[A-Z][a-z]+){2,}\b",                                 "the tax edict (SetTaxRate)"),
        ("ALL-CAPS emphasis",        @"\b[A-Z]{3,}\b",                                                      "an IMPROVED PATH"),
    ];

    /// <summary>All-capitals words that are real acronyms or names, not emphasis (every one occurs in the content).
    /// Roman numerals are allowed by form.</summary>
    internal static readonly string[] Acronyms =
    [
        "ABO", "AGR", "ALARA", "ARPANET", "BCE", "CANDU", "CMOS", "CPU", "CRISPR", "DNA", "EDSAC", "ENIAC", "GPS", "GSM",
        "ICE", "LBK", "MOX", "MRI", "PCR", "PHWR", "PUREX", "RBMK", "RNA", "TCP", "VLSI",
    ];

    private static readonly Regex Roman = new(@"^M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})$", RegexOptions.CultureInvariant);

    private static readonly Regex[] Compiled = Array.ConvertAll(Rules, r => new Regex(r.Pattern, RegexOptions.CultureInvariant));

    /// <summary>Every rule match in <paramref name="text"/>, as (rule name, matched text).</summary>
    internal static List<(string Rule, string Match)> Scan(string text)
    {
        var hits = new List<(string, string)>();
        for (int i = 0; i < Compiled.Length; i++)
            foreach (Match m in Compiled[i].Matches(text))
            {
                if (Rules[i].Name == "ALL-CAPS emphasis" && (Array.IndexOf(Acronyms, m.Value) >= 0 || Roman.IsMatch(m.Value))) continue;
                hits.Add((Rules[i].Name, m.Value));
            }
        return hits;
    }

    /// <summary>One player-visible string: where it is (tree:id field), the text, and whether the field is inside the
    /// tree-closure STRUCTURE hash (unlocks, effects).</summary>
    internal readonly record struct PlayerText(string Where, string Text, bool StructurePinned);

    /// <summary>Every player-visible string of the research content, in file order.</summary>
    internal static List<PlayerText> PlayerTexts(JsonObject doc)
    {
        var all = new List<PlayerText>();
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? n in doc[tree]!.AsArray())
            {
                string at = tree + ":" + (string)n!["id"]!;
                all.Add(new(at + " name", (string)n["name"]!, false));
                all.Add(new(at + " desc", (string)n["desc"]!, false));
                foreach (string list in new[] { "capabilities", "techniques", "applications" })
                    foreach (JsonNode? s in n["unlocks"]![list]!.AsArray())
                        all.Add(new(at + " unlocks." + list, (string)s!, true));
                foreach (JsonNode? e in n["eurekas"]!.AsArray())
                    all.Add(new(at + " idea.text", (string)e!["text"]!, false));
                if (n["effects"]!["future"] is JsonArray future)
                    foreach (JsonNode? f in future)
                        all.Add(new(at + " future.description", (string)f!["description"]!, true));
            }
        foreach (JsonNode? e in doc["entities"]!.AsArray())
            if (e!["name"] is JsonNode name)
                all.Add(new("entity:" + (string)e["id"]! + " name", (string)name!, false));
        return all;
    }

    /// <summary>The developer language that sits inside STRUCTURE-pinned fields (unlocks, effects) and so could not be
    /// rewritten under a TEXT-only change (tree-closure.md §5, TC-4). Exact: where, text.</summary>
    internal static readonly (string Where, string Text)[] StructurePinnedExceptions =
    [
        ("technologies:maize unlocks.capabilities", "nixtamalization required for nutrition (separate node)"),
        ("technologies:tempering unlocks.capabilities", "tough AND hard blades"),
        ("technologies:saddle future.description", "a stable seat for cavalry and long rides; formations do not move before M7"),
        ("technologies:water_mill unlocks.capabilities", "the energy resource type of ADR-019 \u00a75"),
        ("technologies:cartography unlocks.capabilities", "D-040 B1 computed extent made explicit"),
        ("technologies:telescope unlocks.capabilities", "naval reconnaissance (D-039 B)"),
        ("technologies:telegraph unlocks.capabilities", "D-039 A3 order latency collapse"),
        ("technologies:dynamo unlocks.capabilities", "electricity as a produced CAPACITY"),
        ("technologies:electricity_generation unlocks.capabilities", "electricity as CAPACITY (ADR-019 \u00a75)"),
        ("technologies:photography unlocks.capabilities", "D-039 reconnaissance by camera (later)"),
        ("technologies:aircraft unlocks.capabilities", "aerial reconnaissance (D-039 B: reconnaissance no longer walks)"),
        ("technologies:satellite unlocks.capabilities", "orbital observation (a pointed observer, never a map reveal \u2014 D-039 B3, D-040 B3)"),
        ("technologies:radio unlocks.capabilities", "wireless command (D-039 A)"),
        ("technologies:radio unlocks.capabilities", "broadcast media (D-041 C1)"),
        ("technologies:radar unlocks.capabilities", "D-039 B reconnaissance at radio range"),
        ("technologies:television unlocks.capabilities", "mass visual media (D-041 C1)"),
        ("technologies:internet unlocks.capabilities", "D-039 A2 lag collapse"),
        ("technologies:internet unlocks.capabilities", "D-041 media at global scale"),
        ("technologies:gps unlocks.capabilities", "D-040 B1 extent: known everywhere"),
        ("technologies:cotton_cultivation unlocks.capabilities", "cotton textiles (spinning, with spinning_spindle)"),
        ("technologies:fission_weapon unlocks.capabilities", "nuclear weapon (D-011 deterrence abstraction; knowledge only, no mechanic)"),
    ];

    private static JsonObject Shipped()
    {
        using var s = Sim.Data.DataFiles.OpenResearch();
        return JsonNode.Parse(s)!.AsObject();
    }

    internal static List<string> Violations(JsonObject doc)
    {
        var report = new List<string>();
        foreach (PlayerText p in PlayerTexts(doc))
        {
            List<(string Rule, string Match)> hits = Scan(p.Text);
            if (hits.Count == 0) continue;
            if (p.StructurePinned && Array.Exists(StructurePinnedExceptions, x => x.Where == p.Where && x.Text == p.Text)) continue;
            report.Add(p.Where + ": " + string.Join(", ", hits.Select(h => $"\"{h.Match}\" ({h.Rule})")) + "\n    " + p.Text);
        }
        return report;
    }

    [Fact]
    public void ShippedResearch_PlayerVisibleText_CarriesNoDeveloperLanguage()
    {
        JsonObject doc = Shipped();
        List<PlayerText> texts = PlayerTexts(doc);
        // non-vacuous: both trees, their descriptions, Ideas, capabilities and entity names are really read
        Assert.True(texts.Count(t => t.Where.EndsWith(" desc", StringComparison.Ordinal)) >= 1000, "expected every node's desc");
        Assert.Contains(texts, t => t.Where == "civics:law_code desc");
        Assert.Contains(texts, t => t.Where.EndsWith(" idea.text", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Where.EndsWith(" unlocks.capabilities", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Where.EndsWith(" future.description", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Where.StartsWith("entity:", StringComparison.Ordinal));
        List<string> report = Violations(doc);
        Assert.True(report.Count == 0,
            "Player-visible research text carries developer language (move the note to docs/m6-plan/research-description-notes.md):\n"
            + string.Join("\n", report));
    }

    [Fact]
    public void StructurePinnedExceptions_AreExact_AndStillNeeded()
    {
        // an exception that no longer exists, moved into a TEXT field, or no longer matches any rule is stale: remove it
        List<PlayerText> texts = PlayerTexts(Shipped());
        foreach ((string where, string text) in StructurePinnedExceptions)
        {
            Assert.True(texts.Exists(t => t.Where == where && t.Text == text && t.StructurePinned), $"stale exception: {where}: {text}");
            Assert.True(Scan(text).Count > 0, $"exception no longer matches any rule: {where}: {text}");
        }
    }

    // ------------------------------------------------------------------ teeth

    [Fact]
    public void Teeth_EveryRule_FlagsItsSample_InsideOrdinaryText()
    {
        foreach (var (name, _, sample) in Rules)
        {
            string text = "Plain historical prose, then " + sample + ", then more.";
            Assert.True(Scan(text).Any(h => h.Rule == name), $"rule '{name}' did not flag its sample \"{sample}\"");
        }
    }

    [Theory]
    [InlineData("Written law: the civic knowledge of this form of organization. Reclassified from registry institution inst.law_code (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete.")]
    [InlineData("Fire itself (making, keeping, cooking) is baseline and needs no research (D-047 ruling 2).")]
    [InlineData("Simple riverine and shore-hugging craft are baseline (D-047 ruling 7); research unlocks the class and builds no vessel.")]
    [InlineData("Knowledge only for now: adopting a form of government arrives with the government layer (M8).")]
    [InlineData("Trackway \u2014 an IMPROVED PATH, not an engineered road")]
    [InlineData("the edict is operational only once the polity has entered Age III, the Bronze Age (sim.json governance.taxationMinAge)")]
    [InlineData("Unlocks the improved coastal and seagoing transport class; the realizing systems build the vessels.")]
    [InlineData("RELATIVE: a level matters only against rivals' levels")]
    [InlineData("electricity as CAPACITY (ADR-019 \u00a75)")]
    public void Teeth_TheRemovedDeveloperForms_AreFlagged(string text)
    {
        Assert.NotEmpty(Scan(text));
    }

    [Theory]
    [InlineData("Land registry")]
    [InlineData("A monarch rules without the consent of the estates.")]
    [InlineData("then rises together to married men and to ruling elders.")]
    [InlineData("Employers deduct income tax from every wage packet and pay it to the state.")]
    [InlineData("ARPANET 1969, TCP/IP 1983, Web 1991 \u2014 packet network of networks.")]
    [InlineData("Ur III grain-receipt tablets; Age III, the Bronze Age; Louis XIV")]
    [InlineData("McAdam 1820 \u2014 compacted graded stone")]
    [InlineData("DNA, RNA, PCR and CRISPR; GPS; MRI; ~3,500 BCE")]
    [InlineData("Basic fishing needs no hooks and no research.")]
    [InlineData("the steam engine; a heat engine; the working class; the factory system")]
    [InlineData("Milano\u2013Laghi autostrada 1924, Cologne\u2013Bonn 1932, Reichsautobahn and US Interstate")]
    [InlineData("Uranium-235 and plutonium-239; the B-29")]
    [InlineData("Your society cannot adopt it yet.")]
    public void Teeth_OrdinaryHistoricalText_IsNotFlagged(string text)
    {
        var hits = Scan(text);
        Assert.True(hits.Count == 0, $"false positive on \"{text}\": " + string.Join(", ", hits.Select(h => $"{h.Rule} '{h.Match}'")));
    }
}
