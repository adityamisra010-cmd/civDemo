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
    // TC-3 (D-069 R2, Director 2026-11-01): TEXT-only — the descriptions of frontier_propulsion, trade and taxation no longer
    // name another game. TEXT was de341a8eb3a7770ef36ee2585342a2baadf9c131b602b93f8af19a054d1e6c39; STRUCTURE unchanged.
    internal const string Tc3TextSha256 = "0b6dad3c014028a5689b5e33dc7c1a4a5de5c28a7b7721458ea3c6ff4ab846ee";
    // TC-4 (Director, 2026-11-02: player-facing descriptions rewritten without developer language): TEXT-only — 87 node
    // descriptions (20 technologies, 67 civics; the old texts are PreTc4Descriptions below and, with the developer notes
    // they carried, docs/m6-plan/research-description-notes.md). TEXT was Tc3TextSha256; STRUCTURE unchanged.
    internal const string Tc4TextSha256 = "32a6f56fe0f03160d10d0803c9a7590b71641e025a50d6ec14f2302d44a0e931";
    // TC-5 (D-074, Director 2026-11-03: the law court's effect said honestly): TEXT-only — Written law's description adds what
    // the court does (it lightens how heavily the tax is felt in its town) and that choosing laws is not yet part of the
    // game. TEXT was Tc4TextSha256; STRUCTURE unchanged.
    internal const string TextSha256 = "4399a4099e684337c6fe7a4018992ba85bbf72201d7d07a41fa7c246c60d6256";

    /// <summary>TC-5: Written law's description as it stood at TC-4.</summary>
    internal const string PreTc5LawCodeDesc = "Laws set down in writing, so that judgement no longer rests on memory and custom alone, as in the law codes of Ur-Nammu and Hammurabi. Your society can found a law court, and grievance builds more slowly.";

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

    internal const string Tc2TextSha256 = "de341a8eb3a7770ef36ee2585342a2baadf9c131b602b93f8af19a054d1e6c39";

    /// <summary>TC-3 attribution (D-069 R2): restoring the three pre-D-069 descriptions (their original text is kept in
    /// docs/m7-plan/unit-design-references.md §2) reproduces the TC-2 TEXT hash, and STRUCTURE is the TC-2 value throughout,
    /// so the three rewrites are the ONE change and it is text-only.</summary>
    [Fact]
    public void ThePreD069Descriptions_ReproduceTheTc2Text_D069IsTextOnly()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        RestorePreTc4Descriptions(doc);   // TC-4 is newer: back to the TC-3 text first
        Assert.Equal(Tc3TextSha256, Hashes(doc).Text);
        Assert.Equal(StructureSha256, Hashes(doc).Structure);
        JsonNode Node(string tree, string id) => doc[tree]!.AsArray().Single(n => (string?)n!["id"] == id)!;
        JsonNode propulsion = Node("technologies", "frontier_propulsion"), trade = Node("technologies", "trade"), tax = Node("civics", "taxation");
        Assert.Equal("Each level raises the transit velocity of probes and expeditions.", (string?)propulsion["desc"]);
        propulsion["desc"] = "Each level raises the transit velocity of probes and expeditions \u2014 the research analogue of Civ 6's laser station, which is a project.";
        trade["desc"] = ((string)trade["desc"]!).Replace("Its causal prerequisites are a record of quantity",
            "Placement: Civ VI unlocks trade routes and the Trader with the Ancient-Era Foreign Trade civic (after Code of Laws); here the causal prerequisites are a record of quantity", StringComparison.Ordinal);
        tax["desc"] = ((string)tax["desc"]!).Replace("Its causal prerequisites are counted obligations",
            "Placement: Civ VI has no tax technology \u2014 governments and their policies come through civics, starting with the first civic, Code of Laws (Chiefdom); here the causal prerequisites are counted obligations", StringComparison.Ordinal);
        Assert.Equal(Tc2TextSha256, Hashes(doc).Text);
        Assert.Equal(StructureSha256, Hashes(doc).Structure);
    }

    /// <summary>TC-4: the civic description tail the 60 government-form civics carried, and the plain tail that replaced it
    /// (the old one named a milestone and "the government layer", and its "Knowledge only" was not true: each of them has a
    /// realized effect).</summary>
    internal const string PreTc4CivicTail = " Knowledge only for now: adopting a form of government arrives with the government layer (M8).";
    internal const string Tc4CivicTail = " Your society cannot adopt it yet.";

    /// <summary>TC-4: the 27 descriptions rewritten whole (or, for trade, re-cased), as they stood at TC-3.</summary>
    internal static readonly (string Tree, string Id, string Desc)[] PreTc4Descriptions =
    [
        ("technologies", "fire_making",
            "Advanced, deliberate use of controlled fire \u2014 sustained managed hearths and earth ovens, heat treatment of stone, planned landscape burning. Fire itself (making, keeping, cooking) is baseline and needs no research (D-047 ruling 2)."),
        ("technologies", "adhesive_natural",
            "Engineered binders \u2014 resin loaded with ochre or beeswax, heat-processed bitumen \u2014 for load-bearing composite hafting. Simple gum, resin and sinew binding is baseline (D-047 ruling 3)."),
        ("technologies", "hafting",
            "Precision-fitted composite tools \u2014 shaped tang, socket or split haft bound with sinew and compound adhesive \u2014 that survive repeated impact. Simple hafted tools are baseline (D-047 ruling 3)."),
        ("technologies", "raft",
            "Large lashed log or reed rafts with steering oars, built for open-water crossings beyond sight of land. Simple riverine and shore-hugging craft are baseline (D-047 ruling 7); research unlocks the class and builds no vessel."),
        ("technologies", "dugout",
            "Large log hollowed by fire and adze into a stable load-carrying hull. Simple log floats and small canoes for river crossing are baseline (D-047 ruling 7); research unlocks the class and builds no vessel."),
        ("technologies", "fishing_hook",
            "Specialised hook-and-line gear \u2014 shell or bone hooks, weighted lines, lures \u2014 for deep-water and pelagic species. Basic fishing is baseline and needs no hook or node (D-047 rulings 4 and 6)."),
        ("technologies", "digging_stick_hoe",
            "Hafted blade for tillage \u2014 the cultivation tool before the plough. Digging-stick cultivation is baseline (D-047)."),
        ("technologies", "track_road",
            "Trackway \u2014 an IMPROVED PATH, not an engineered road: timber causeway and corduroy across marsh, cleared and stoned track (Sweet Track). An infrastructure-class unlock (path tier): road development builds the trackways, and basic dirt paths are baseline (D-047 ruling 8; ADR-032). The engineered built road additionally needs dry-stone construction (infra.road_built requires track_road AND stone_dry)."),
        ("technologies", "arsenical_bronze",
            "Copper smelted from arsenic-bearing ores \u2014 harder than copper, castable, and NEEDS NO TIN. Most early \"bronze\" was this."),
        ("technologies", "sail_square",
            "Wind propulsion \u2014 first on the Nile. Unlocks the improved coastal and seagoing transport class (D-047 ruling 7); primitive river and shore-hugging craft are baseline, and the realizing systems build the vessels."),
        ("technologies", "geometry_axiomatic",
            "Proof from stated axioms \u2014 Euclid. Knowledge that can be VERIFIED from the text alone."),
        ("technologies", "road_paved",
            "Surveyed, layered, cambered, drained, paved \u2014 Via Appia. Inca qhapaq \u00f1an independently (no wheels). An infrastructure-class unlock: construction systems build the roads (D-047 ruling 8)."),
        ("technologies", "caravel",
            "Lateen-rigged, shallow-draft, fast \u2014 the exploration ship of the Portuguese. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel."),
        ("technologies", "carrack",
            "Three-masted, square and lateen, high-castled \u2014 the ocean cargo and warship. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel."),
        ("technologies", "polynesian_canoe",
            "Double-hulled, crab-claw sail, stellar navigation \u2014 Pacific colonisation without compass or writing. Unlocks the oceanic transport class (D-047 ruling 7); research builds no vessel."),
        ("technologies", "macadam",
            "McAdam 1820 \u2014 compacted graded stone; cheap all-weather roads. An infrastructure-class unlock: construction systems build the roads (D-047 ruling 8)."),
        ("technologies", "frontier_military",
            "Each level raises weapon-system effectiveness in combat resolution. RELATIVE: a level matters only against rivals' levels, so the arms race never ends."),
        ("technologies", "frontier_cyber",
            "Each level raises network offence and defence. RELATIVE, like military research."),
        ("technologies", "motor_road",
            "Graded, surfaced, grade-separated road for motor traffic \u2014 Milano\u2013Laghi autostrada 1924, Cologne\u2013Bonn 1932, Reichsautobahn and US Interstate. An infrastructure-class unlock (highway tier): road development builds the highways as new alignments beside the older roads (ADR-032)."),
        ("technologies", "trade",
            "Formal exchange between communities: goods valued against a counted standard and carried by pack animal or boat to partners beyond the settlement \u2014 the caravan and boat trade of the Uruk expansion and the Old Assyrian merchants of Kanesh. Within-settlement sharing and exchange need no research; trade BETWEEN settlements (domestic or foreign) does. Its causal prerequisites are a record of quantity (clay tokens) and a carrier for goods over distance (donkey, camel or the square sail)."),
        ("civics", "law_code",
            "Written law: the civic knowledge of this form of organization. Reclassified from registry institution inst.law_code (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "legal_code_roman",
            "Systematic law: the civic knowledge of this form of organization. Reclassified from registry institution inst.legal_code_roman (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "census",
            "Census: the civic knowledge of this form of organization. Reclassified from registry institution inst.census (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "coined_wage",
            "Wage labour in coin: the civic knowledge of this form of organization. Reclassified from registry institution inst.coined_wage (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "patent",
            "Patent system: the civic knowledge of this form of organization. Reclassified from registry institution inst.patent (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "joint_stock",
            "Joint-stock company: the civic knowledge of this form of organization. Reclassified from registry institution inst.joint_stock (architecture \u00a75.7); the institution itself is established by its owning system once this civic is complete."),
        ("civics", "taxation",
            "Taxation: the levy of tribute in kind \u2014 a counted share of the harvest, herd or craft output owed to the ruler or temple, assessed against the harvest and delivered into sealed stores. Its evidence is the Uruk and Ur III grain-receipt tablets, the sealed storerooms of the Ubaid and Uruk temples, and the Egyptian harvest assessment recorded beside the Nile gauge. A community without a record of what is owed has gifts and feasting, not a levy, so no Stone Age society taxes. Researching it gives the polity the knowledge of the tax edict (SetTaxRate); the edict is operational only once the polity has entered Age III, the Bronze Age (sim.json governance.taxationMinAge). Related refinements, which no longer gate the edict: tax assessment by computation (arithmetic_babylonian), taxation by area (surveying), by weight (standard_weights) and in coin (coinage_electrum). Its causal prerequisites are counted obligations (clay tokens), sealed stores under administrative control (stamp seal) and an administrative record beyond memory (proto-cuneiform, hieroglyphic or Chinese script)."),
    ];

    /// <summary>Puts the TC-3 descriptions back: the 27 listed ones, and the old tail on every civic that ends with the
    /// TC-4 tail. Returns how many descriptions it changed.</summary>
    internal static int RestorePreTc4Descriptions(JsonObject doc)
    {
        int changed = 0;
        foreach ((string tree, string id, string desc) in PreTc4Descriptions)
        {
            JsonNode n = doc[tree]!.AsArray().Single(x => (string?)x!["id"] == id)!;
            Assert.NotEqual(desc, (string?)n["desc"]);
            n["desc"] = desc;
            changed++;
        }
        foreach (JsonNode? n in doc["civics"]!.AsArray())
        {
            string d = (string)n!["desc"]!;
            if (!d.EndsWith(Tc4CivicTail, StringComparison.Ordinal)) continue;
            n["desc"] = string.Concat(d.AsSpan(0, d.Length - Tc4CivicTail.Length), PreTc4CivicTail);
            changed++;
        }
        return changed;
    }

    /// <summary>TC-4 attribution (Director, 2026-11-02): restoring the TC-3 descriptions — the 27 rewritten whole and the
    /// old tail of the 60 government-form civics — reproduces the TC-3 TEXT hash, and STRUCTURE is the closure value
    /// throughout, so the 87 rewrites are the ONE change and it is text-only. No name changed.</summary>
    [Fact]
    public void ThePreTc4Descriptions_ReproduceTheTc3Text_Tc4IsTextOnly()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        Assert.Equal(StructureSha256, Hashes(doc).Structure);
        Assert.Equal(TextSha256, Hashes(doc).Text);
        Assert.Equal(87, RestorePreTc4Descriptions(doc));
        Assert.Equal(Tc3TextSha256, Hashes(doc).Text);
        Assert.Equal(StructureSha256, Hashes(doc).Structure);
    }

    /// <summary>TC-5 attribution (D-074): restoring Written law's TC-4 description reproduces the TC-4 TEXT hash, and STRUCTURE
    /// is the closure value throughout, so the one rewrite is the ONE change and it is text-only. No name changed.</summary>
    [Fact]
    public void ThePreTc5LawCodeDescription_ReproducesTheTc4Text_Tc5IsTextOnly()
    {
        JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = JsonNode.Parse(s)!.AsObject();
        Assert.Equal(TextSha256, Hashes(doc).Text);
        JsonNode law = doc["civics"]!.AsArray().Single(n => (string?)n!["id"] == "law_code")!;
        Assert.Equal("Written law", (string?)law["name"]);
        Assert.NotEqual(PreTc5LawCodeDesc, (string?)law["desc"]);
        law["desc"] = PreTc5LawCodeDesc;
        Assert.Equal(Tc4TextSha256, Hashes(doc).Text);
        Assert.Equal(StructureSha256, Hashes(doc).Structure);
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
