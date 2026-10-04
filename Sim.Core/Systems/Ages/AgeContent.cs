using System.Text.Json;
using System.Text.Json.Serialization;
using Sim.Core.Systems.Research;

namespace Sim.Core.Systems.Ages;

/// <summary>Raised on any ages.json schema violation, with an actionable message.</summary>
public sealed class AgeContentException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The five ratified Age-milestone categories (D-047 ruling 12; ledger §10.4).
/// The value is the category's stable key in ages.json.</summary>
public enum AgeMilestoneCategory
{
    Technological = 1,
    MaterialEconomic = 2,
    InstitutionalSocial = 3,
    Systemic = 4,
    MilitaryRealization = 5,
}

/// <summary>
/// The kinds of milestone FACT. Each reads a table an owning system already publishes
/// (D-047 ruling 12: owning systems publish the facts; a thin evaluator reads them). The
/// owner of each is named by <see cref="AgeMilestoneFact.Owner"/>, so the Glass Box can say
/// whose state a milestone measures.
/// </summary>
public enum MilestoneFactKind
{
    /// <summary>ANY of the listed research nodes is completed (ResearchSystem).</summary>
    Research = 1,
    /// <summary>At least Min completed nodes in a tree, or in both (ResearchSystem).</summary>
    ResearchCount = 2,
    /// <summary>At least Min people in the polity's controlled settlements (DemographicsSystem).</summary>
    Population = 3,
    /// <summary>At least Min controlled settlements (founding + ColonizationSystem).</summary>
    Settlements = 4,
    /// <summary>At least Min completed structures, of one project or any (ConstructionSystem).</summary>
    Structures = 5,
    /// <summary>At least Min units of one good summed over controlled settlements (GoodStocks, Ledger-owned).</summary>
    GoodStock = 6,
    /// <summary>At least Min controlled settlements where a class is active (ClassMobilitySystem).</summary>
    ClassActive = 7,
    /// <summary>At least Min whole units moved last turn into or out of controlled settlements (TradeArbitrageSystem).</summary>
    TradeVolume = 8,
    /// <summary>At least Min dwellings in controlled settlements (HousingSystem).</summary>
    Dwellings = 9,
    /// <summary>At least Min owned military formations, of one family or any (AgeTransitionSystem's MilitaryUnits —
    /// REAL formations, never researched technology: ledger §10.4), whose CURRENT identity is realized at
    /// or after the fact's <c>minIdentityAge</c> (M5 R2b: the founding warband line, converted for free at
    /// each Age entry, never counts as the new Age's military realization).</summary>
    Formations = 10,
}

/// <summary>One milestone fact, resolved at load: the listed research node keys (Research), the
/// sorted keys of the counted tree (ResearchCount; every node key when the tree is "any"), a
/// good/project/class/family key (<c>Ref</c>, -1 for "any"), the threshold, and the owning system
/// whose published table the fact reads.</summary>
public sealed record AgeMilestoneFact(
    MilestoneFactKind Kind, IReadOnlyList<int> NodeKeys, IReadOnlyList<string> NodeIds,
    ResearchTree? Tree, int[] TreeKeys, long Min, int Ref, string? RefName, string Owner,
    // M5 R2b: Formations only — the SORTED unit-identity keys that count (every identity of the family,
    // or of any family, realized at Age >= MinIdentityAge). Null for every other kind.
    int[]? IdentityKeys = null, int MinIdentityAge = 0);

/// <summary>One milestone of an Age's entry requirements. <c>Pending</c> (M5 R2b) names the not-yet-built
/// mechanism a milestone's fact depends on (e.g. Battle Layer (M7) recruitment): the fact is still evaluated honestly
/// over real state, but nothing in the shipped game can produce it yet, and every surface says so
/// rather than manufacturing evidence. Null when the milestone is reachable today.</summary>
public sealed record AgeMilestone(
    string Id, string Name, string Description, AgeMilestoneCategory Category, bool Core, AgeMilestoneFact Fact,
    string? Pending = null);

/// <summary>The requirements for ENTERING an Age from the one before it (D-043 A2).</summary>
public sealed record AgeEntryRequirements(
    IReadOnlyList<AgeMilestone> Core, IReadOnlyList<AgeMilestone> Supporting, int SupportingRequired, int MinCategories);

/// <summary>One of the nine ratified Ages (ledger §10). <c>Entry</c> is null for the founding Age
/// and any Age below it.</summary>
public sealed record AgeDefinition(int Key, string Id, string Name, AgeEntryRequirements? Entry);

public sealed record AgeCategoryDefinition(AgeMilestoneCategory Key, string Id, string Name);

/// <summary>One Age-surge emphasis the advancing polity may choose (D-047 ruling 14). Qualitative only:
/// no numeric effect exists (ledger §10.5/§27).</summary>
public sealed record AgeSurge(
    int Key, string Id, string Name, string Description, string Domain, string ExpectedEffect,
    IReadOnlyList<string> DownstreamSystems);

/// <summary>ADR-031 — the loaded Age content (ages.json).</summary>
public sealed class AgeContent
{
    public const int AgeCount = 9;

    /// <summary>The nine Ages; <c>Ages[k - 1]</c> is Age k.</summary>
    public required IReadOnlyList<AgeDefinition> Ages { get; init; }
    public required IReadOnlyList<AgeCategoryDefinition> Categories { get; init; }
    /// <summary>Surge emphases, key ascending.</summary>
    public required IReadOnlyList<AgeSurge> Surges { get; init; }
    public required int FoundingAge { get; init; }
    public required string FoundingAgeBasis { get; init; }
    public required string SurgeShape { get; init; }
    public required string Status { get; init; }

    public AgeDefinition Age(int key) => key >= 1 && key <= AgeCount
        ? Ages[key - 1]
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Ages are 1..9");

    public AgeSurge? SurgeByKey(int key)
    {
        for (int i = 0; i < Surges.Count; i++) if (Surges[i].Key == key) return Surges[i];
        return null;
    }

    public AgeCategoryDefinition Category(AgeMilestoneCategory key) => Categories[(int)key - 1];
}

/// <summary>Loads and validates ages.json. Every fact reference is resolved against the content it
/// names (research nodes, goods, projects, classes, unit families), so a typo fails the load and
/// can never become a silently unsatisfiable milestone.</summary>
public static class AgeContentLoader
{
    public const int Schema = 1;

    private static readonly string[] CategoryIds =
        ["technological", "material_economic", "institutional_social", "systemic", "military_realization"];

    private static readonly JsonSerializerOptions JsonOptions = new() { RespectNullableAnnotations = true };

    public static AgeContent Load(Stream json, ResearchContent? research, GoodsConfig? goods,
        RegistriesConfig registries, UnitFamilyContent? families)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd(), research, goods, registries, families);
    }

    public static AgeContent Load(string json, ResearchContent? research, GoodsConfig? goods,
        RegistriesConfig registries, UnitFamilyContent? families)
    {
        FileJson? file;
        try { file = JsonSerializer.Deserialize<FileJson>(json, JsonOptions); }
        catch (JsonException e)
        {
            throw new AgeContentException($"ages.json is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (file is null) throw Fail("the document is empty (null).");
        if (file.Schema != Schema) throw Fail($"schema is {file.Schema}, expected {Schema}.");

        if (file.Categories.Length != CategoryIds.Length)
            throw Fail($"categories must list exactly the five ratified categories, got {file.Categories.Length}.");
        var categories = new AgeCategoryDefinition[CategoryIds.Length];
        for (int c = 0; c < CategoryIds.Length; c++)
        {
            CategoryJson cj = file.Categories[c];
            if (cj.Key != c + 1 || cj.Id != CategoryIds[c])
                throw Fail($"categories[{c}] must be key {c + 1} id '{CategoryIds[c]}' (D-047 ruling 12), got key {cj.Key} id '{cj.Id}'.");
            categories[c] = new AgeCategoryDefinition((AgeMilestoneCategory)(c + 1), cj.Id, cj.Name);
        }

        if (file.FoundingAge < 1 || file.FoundingAge > AgeContent.AgeCount)
            throw Fail($"foundingAge must be in 1..{AgeContent.AgeCount}, got {file.FoundingAge}.");
        if (file.Ages.Length != AgeContent.AgeCount)
            throw Fail($"ages must list exactly the nine ratified Ages (ledger §10), got {file.Ages.Length}.");

        var milestoneIds = new List<string>();
        var ages = new AgeDefinition[AgeContent.AgeCount];
        for (int a = 0; a < ages.Length; a++)
        {
            AgeJson aj = file.Ages[a];
            string path = $"ages[{a}] ('{aj.Id}')";
            if (aj.Key != a + 1) throw Fail($"{path}: key must be {a + 1} (Ages are listed in order), got {aj.Key}.");
            AgeEntryRequirements? entry = null;
            if (aj.Key > file.FoundingAge)
            {
                if (aj.Entry is null) throw Fail($"{path}: an Age above the founding Age must declare its entry requirements.");
                entry = BuildEntry(aj.Entry, path, aj.Key, milestoneIds, research, goods, registries, families);
            }
            else if (aj.Entry is not null)
            {
                throw Fail($"{path}: the founding Age and the Ages below it cannot be entered and must not declare entry requirements.");
            }
            ages[a] = new AgeDefinition(aj.Key, aj.Id, aj.Name, entry);
        }

        if (file.Surges.Length == 0) throw Fail("surges must list at least one emphasis (D-047 ruling 14: the player chooses one).");
        var surges = new AgeSurge[file.Surges.Length];
        for (int s = 0; s < surges.Length; s++)
        {
            SurgeJson sj = file.Surges[s];
            if (sj.Key < 1) throw Fail($"surges[{s}]: key must be >= 1, got {sj.Key}.");
            if (s > 0 && sj.Key <= surges[s - 1].Key) throw Fail($"surges[{s}]: keys must be strictly ascending.");
            surges[s] = new AgeSurge(sj.Key, sj.Id, sj.Name, sj.Description, sj.Domain, sj.ExpectedEffect, sj.DownstreamSystems);
        }

        return new AgeContent
        {
            Ages = ages, Categories = categories, Surges = surges, FoundingAge = file.FoundingAge,
            FoundingAgeBasis = file.FoundingAgeBasis, SurgeShape = file.SurgeShape, Status = file.Status,
        };
    }

    private static AgeEntryRequirements BuildEntry(EntryJson ej, string path, int ageKey, List<string> milestoneIds,
        ResearchContent? research, GoodsConfig? goods, RegistriesConfig registries, UnitFamilyContent? families)
    {
        var core = new AgeMilestone[ej.Core.Length];
        for (int i = 0; i < core.Length; i++)
            core[i] = BuildMilestone(ej.Core[i], $"{path}.entry.core[{i}]", ageKey, true, milestoneIds, research, goods, registries, families);
        var supporting = new AgeMilestone[ej.Supporting.Length];
        var distinct = new bool[CategoryIds.Length + 1];
        int distinctCount = 0;
        for (int i = 0; i < supporting.Length; i++)
        {
            supporting[i] = BuildMilestone(ej.Supporting[i], $"{path}.entry.supporting[{i}]", ageKey, false, milestoneIds, research, goods, registries, families);
            int k = (int)supporting[i].Category;
            if (!distinct[k]) { distinct[k] = true; distinctCount++; }
        }
        if (core.Length == 0) throw Fail($"{path}.entry: at least one mandatory core milestone is required (D-043 A2).");
        if (ej.SupportingRequired < 0 || ej.SupportingRequired > supporting.Length)
            throw Fail($"{path}.entry: supportingRequired {ej.SupportingRequired} must be in 0..{supporting.Length} (the supporting list's length).");
        if (ej.MinCategories < 0 || ej.MinCategories > distinctCount || ej.MinCategories > ej.SupportingRequired)
            throw Fail($"{path}.entry: minCategories {ej.MinCategories} must be achievable: at most the {distinctCount} distinct " +
                       $"categories the supporting list spans and at most supportingRequired ({ej.SupportingRequired}).");
        return new AgeEntryRequirements(core, supporting, ej.SupportingRequired, ej.MinCategories);
    }

    private static AgeMilestone BuildMilestone(MilestoneJson mj, string path, int ageKey, bool core, List<string> ids,
        ResearchContent? research, GoodsConfig? goods, RegistriesConfig registries, UnitFamilyContent? families)
    {
        if (ids.Contains(mj.Id)) throw Fail($"{path}: duplicate milestone id '{mj.Id}'.");
        ids.Add(mj.Id);
        int category = Array.IndexOf(CategoryIds, mj.Category);
        if (category < 0) throw Fail($"{path} ('{mj.Id}'): category '{mj.Category}' is not one of {string.Join(", ", CategoryIds)}.");
        AgeMilestoneFact fact = BuildFact(mj.Fact, $"{path} ('{mj.Id}').fact", research, goods, registries, families);
        var cat = (AgeMilestoneCategory)(category + 1);
        if (fact.Kind == MilestoneFactKind.Formations && cat != AgeMilestoneCategory.MilitaryRealization)
            throw Fail($"{path} ('{mj.Id}'): a formations fact belongs to the Military Realization category.");
        if (cat == AgeMilestoneCategory.MilitaryRealization && fact.Kind != MilestoneFactKind.Formations)
            throw Fail($"{path} ('{mj.Id}'): Military Realization must measure real formations, never research " +
                       "(ledger §10.4: researching a military technology does not satisfy Military Realization).");
        // M5 R2b (Director decision 11): MILITARY REALIZATION OF AN AGE IS AGE-APPROPRIATE. The founding
        // warband exists from turn 1 and modernizes for free at every Age entry (ruling 18), so a formations
        // fact that any identity satisfies is held by the founding line for EVERY Age and measures nothing.
        // A formations fact must therefore count only identities realized at or after the Age it is entering.
        if (fact.Kind == MilestoneFactKind.Formations && fact.MinIdentityAge < ageKey)
            throw Fail($"{path} ('{mj.Id}'): a formations fact entering Age {ageKey} must set minIdentityAge >= {ageKey} — " +
                       "otherwise the founding warband line (free modernization, ruling 18) satisfies it in every Age.");
        if (mj.Pending is { } pending && string.IsNullOrWhiteSpace(pending))
            throw Fail($"{path} ('{mj.Id}'): pending, when present, must name the mechanism the milestone waits for.");
        return new AgeMilestone(mj.Id, mj.Name, mj.Description, cat, core, fact, mj.Pending);
    }

    private static AgeMilestoneFact BuildFact(FactJson fj, string path,
        ResearchContent? research, GoodsConfig? goods, RegistriesConfig registries, UnitFamilyContent? families)
    {
        long min = fj.Min ?? 1;
        if (min < 1) throw Fail($"{path}: min must be >= 1, got {min}.");
        switch (fj.Kind)
        {
            case "research":
            {
                if (research is null) throw Fail($"{path}: a research fact needs research.json loaded first.");
                if (fj.Nodes is not { Length: > 0 }) throw Fail($"{path}: a research fact lists at least one node id.");
                var keys = new int[fj.Nodes.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    int index = research.IndexOfId(fj.Nodes[i]);
                    if (index < 0) throw Fail($"{path}: node '{fj.Nodes[i]}' is not in research.json.");
                    keys[i] = research.Nodes[index].Key.Value;
                }
                if (fj.Min is not null) throw Fail($"{path}: a research fact holds when ANY listed node is complete; it takes no min.");
                return new AgeMilestoneFact(MilestoneFactKind.Research, keys, fj.Nodes, null, [], 1, -1, null, "research");
            }
            case "research_count":
            {
                if (research is null) throw Fail($"{path}: a research_count fact needs research.json loaded first.");
                ResearchTree? tree = fj.Tree switch
                {
                    "technology" => ResearchTree.Technology,
                    "civics" => ResearchTree.Civics,
                    null or "any" => null,
                    _ => throw Fail($"{path}: tree must be 'technology', 'civics' or 'any', got '{fj.Tree}'."),
                };
                var treeKeys = new List<int>();
                for (int i = 0; i < research.Nodes.Count; i++)
                    if (tree is null || research.Nodes[i].Tree == tree) treeKeys.Add(research.Nodes[i].Key.Value);
                int[] sorted = [.. treeKeys];
                Array.Sort(sorted);
                if (min > sorted.Length) throw Fail($"{path}: min {min} exceeds the {sorted.Length} nodes the tree has — never satisfiable.");
                return new AgeMilestoneFact(MilestoneFactKind.ResearchCount, [], [], tree, sorted, min, -1, fj.Tree, "research");
            }
            case "population":
                return new AgeMilestoneFact(MilestoneFactKind.Population, [], [], null, [], min, -1, null, "demographics");
            case "settlements":
                return new AgeMilestoneFact(MilestoneFactKind.Settlements, [], [], null, [], min, -1, null, "colonization");
            case "structures":
            {
                int project = -1;
                if (fj.Project is { } name)
                {
                    ConstructionProjectEntry[] projects = goods?.Projects ?? [];
                    for (int i = 0; i < projects.Length; i++) if (projects[i].Name == name) project = projects[i].Id;
                    if (project < 0) throw Fail($"{path}: project '{name}' is not in goods.json projects.");
                }
                return new AgeMilestoneFact(MilestoneFactKind.Structures, [], [], null, [], min, project, fj.Project, "construction");
            }
            case "good_stock":
            {
                if (goods is null) throw Fail($"{path}: a good_stock fact needs goods.json loaded first.");
                int good = fj.Good is { } g ? goods.IdOf(g) : -1;
                if (good < 0) throw Fail($"{path}: good '{fj.Good}' is not in goods.json.");
                return new AgeMilestoneFact(MilestoneFactKind.GoodStock, [], [], null, [], min, good, fj.Good, "production");
            }
            case "class_active":
            {
                int cls = -1;
                for (int i = 0; i < registries.Classes.Length; i++)
                    if (registries.Classes[i].Name == fj.Class) cls = registries.Classes[i].Id;
                if (cls < 0) throw Fail($"{path}: class '{fj.Class}' is not in sim.json registries.classes.");
                return new AgeMilestoneFact(MilestoneFactKind.ClassActive, [], [], null, [], min, cls, fj.Class, "classmobility");
            }
            case "trade_volume":
                return new AgeMilestoneFact(MilestoneFactKind.TradeVolume, [], [], null, [], min, -1, null, "trade");
            case "dwellings":
                return new AgeMilestoneFact(MilestoneFactKind.Dwellings, [], [], null, [], min, -1, null, "housing");
            case "formations":
            {
                int family = -1;
                if (fj.Family is { } id)
                {
                    if (families is null) throw Fail($"{path}: a family-specific formations fact needs unit-families.json loaded first.");
                    family = families.FamilyById(id)?.Key ?? throw Fail($"{path}: family '{id}' is not in unit-families.json.");
                }
                int minAge = fj.MinIdentityAge ?? 0;
                int[]? identities = null;
                if (fj.MinIdentityAge is not null)
                {
                    if (minAge < 1 || minAge > AgeContent.AgeCount) throw Fail($"{path}: minIdentityAge must be in 1..{AgeContent.AgeCount}, got {minAge}.");
                    if (families is null) throw Fail($"{path}: minIdentityAge needs unit-families.json loaded first.");
                    var keys = new List<int>();
                    for (int f = 0; f < families.Families.Count; f++)
                    {
                        UnitFamily fam = families.Families[f];
                        if (family >= 0 && fam.Key != family) continue;
                        for (int i = 0; i < fam.Line.Count; i++) if (fam.Line[i].Age >= minAge) keys.Add(fam.Line[i].Key);
                    }
                    identities = [.. keys];
                    Array.Sort(identities);
                }
                return new AgeMilestoneFact(MilestoneFactKind.Formations, [], [], null, [], min, family, fj.Family, "agetransition",
                    identities, minAge);
            }
            default:
                throw Fail($"{path}: unknown fact kind '{fj.Kind}' (known: research, research_count, population, settlements, " +
                           "structures, good_stock, class_active, trade_volume, dwellings, formations).");
        }
    }

    private static AgeContentException Fail(string message) => new($"ages.json: {message}");

    private sealed record FileJson(
        [property: JsonPropertyName("schema"), JsonRequired] int Schema,
        [property: JsonPropertyName("status"), JsonRequired] string Status,
        [property: JsonPropertyName("foundingAge"), JsonRequired] int FoundingAge,
        [property: JsonPropertyName("foundingAgeBasis"), JsonRequired] string FoundingAgeBasis,
        [property: JsonPropertyName("categories"), JsonRequired] CategoryJson[] Categories,
        [property: JsonPropertyName("ages"), JsonRequired] AgeJson[] Ages,
        [property: JsonPropertyName("surges"), JsonRequired] SurgeJson[] Surges,
        [property: JsonPropertyName("surgeShape"), JsonRequired] string SurgeShape);

    private sealed record CategoryJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name);

    private sealed record AgeJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("entry")] EntryJson? Entry);

    private sealed record EntryJson(
        [property: JsonPropertyName("core"), JsonRequired] MilestoneJson[] Core,
        [property: JsonPropertyName("supporting"), JsonRequired] MilestoneJson[] Supporting,
        [property: JsonPropertyName("supportingRequired"), JsonRequired] int SupportingRequired,
        [property: JsonPropertyName("minCategories"), JsonRequired] int MinCategories);

    private sealed record MilestoneJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("description"), JsonRequired] string Description,
        [property: JsonPropertyName("category"), JsonRequired] string Category,
        [property: JsonPropertyName("fact"), JsonRequired] FactJson Fact,
        [property: JsonPropertyName("pending")] string? Pending = null);

    private sealed record FactJson(
        [property: JsonPropertyName("kind"), JsonRequired] string Kind,
        [property: JsonPropertyName("nodes")] string[]? Nodes,
        [property: JsonPropertyName("tree")] string? Tree,
        [property: JsonPropertyName("min")] long? Min,
        [property: JsonPropertyName("project")] string? Project,
        [property: JsonPropertyName("good")] string? Good,
        [property: JsonPropertyName("class")] string? Class,
        [property: JsonPropertyName("family")] string? Family,
        [property: JsonPropertyName("minIdentityAge")] int? MinIdentityAge = null);

    private sealed record SurgeJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("description"), JsonRequired] string Description,
        [property: JsonPropertyName("domain"), JsonRequired] string Domain,
        [property: JsonPropertyName("expectedEffect"), JsonRequired] string ExpectedEffect,
        [property: JsonPropertyName("downstreamSystems"), JsonRequired] string[] DownstreamSystems);
}
