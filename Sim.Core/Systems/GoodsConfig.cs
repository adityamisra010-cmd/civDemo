using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Systems;

/// <summary>Raised on any goods-config schema violation, with an actionable message.</summary>
public sealed class GoodsConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>One D-031 good. Names live HERE (ADR-001) — sim rows carry the id.
/// BulkPerUnit is the D-034 transport bulk. DepositChannel names the terrain
/// field the founding deposit roll derives from (null = no deposit — the good
/// is produced, not extracted); DepositSpread widens the seeded roll (ores:
/// uneven geography is the precondition for comparative advantage).
///
/// <para>ADR-056 (the M7 economy track, D-065 R2) adds three OPTIONAL fields, each absent on the fourteen base goods (so
/// their behaviour is byte-identical):</para>
/// <list type="bullet">
/// <item><c>Packet</c> — the economic-content packet the good belongs to (goods.json <c>packets[].id</c>, e.g. "ec1"
///   metals). A good of a DISABLED packet is filtered out at load
///   (<see cref="GoodsConfigLoader"/>), so the switch-off twin founds and steps exactly the pre-packet roster.</item>
/// <item><c>Knowledge</c> — a node expression in the research dialect (research.json node ids, AND/OR, no NOT — the
///   ADR-033 D4 form, parsed by ResearchContentLoader.ParseRequirement). For a deposit good it gates WORKING the deposit:
///   a settlement whose knowledge does not satisfy it puts no extraction labour there (the deposit still exists and is
///   still shown — geology, not technique). For a herd good it gates capture and breeding. TC-1: no research.json node or
///   entity is added; the gate names existing node ids.</item>
/// <item><c>Herd</c> — a herd good's breeding mechanism (<see cref="HerdEntry"/>, EC-2 horses).</item>
/// </list>
/// </summary>
public sealed record GoodEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("category"), JsonRequired] string Category,
    [property: JsonPropertyName("bulkPerUnit"), JsonRequired] double BulkPerUnit,
    [property: JsonPropertyName("numeraire")] bool Numeraire = false,
    [property: JsonPropertyName("depositChannel")] string? DepositChannel = null,
    [property: JsonPropertyName("depositSpread")] double DepositSpread = 0.0,
    [property: JsonPropertyName("packet")] string? Packet = null,
    [property: JsonPropertyName("knowledge")] string? Knowledge = null,
    [property: JsonPropertyName("herd")] HerdEntry? Herd = null);

/// <summary>
/// ADR-056 §5 (EC-2) — a HERD good's breeding mechanism: a conserved stock that grows from itself on the settlement's
/// pasture, distinct from the herding sector's food output (livestock, fish), and not food. All rates are per sim-year,
/// integrated EXACTLY over the turn's dtYears (law 3; ProductionSystem.Breed has the closed form).
///
/// DIMENSIONAL DECLARATION (S8 §4.1 requirement 2):
/// <list type="bullet">
/// <item><c>PastureGood</c>: the deposit good whose row at the settlement measures its pasture (dimensionless abundance,
///   the moisture channel's hinterland mean — "livestock").</item>
/// <item><c>HeadPerHerder</c>: head kept per herding-sector worker at pasture abundance 1.0 [head / worker]. The carrying
///   capacity is K = herders × HeadPerHerder × pasture [head].</item>
/// <item><c>CapturePerHerderPerYear</c>: wild head taken into the herd per herder-year at pasture 1.0
///   [head / (worker × year)] — the domestication inflow that seeds a herd from nothing but labour on land.</item>
/// <item><c>BreedingRatePerYear</c>: the herd's net increase rate at low density, and the rate an excess above capacity
///   is lost at [1 / year].</item>
/// <item><c>FodderGood</c> / <c>FodderPerHeadPerYear</c>: the supplementary fodder each head draws [fodder units /
///   (head × year)] — a Ledger sink on the settlement's fodder stock; a shortfall scales capacity and capture by the fed
///   share.</item>
/// </list>
/// </summary>
public sealed record HerdEntry(
    [property: JsonPropertyName("pastureGood"), JsonRequired] string PastureGood,
    [property: JsonPropertyName("headPerHerder"), JsonRequired] double HeadPerHerder,
    [property: JsonPropertyName("capturePerHerderPerYear"), JsonRequired] double CapturePerHerderPerYear,
    [property: JsonPropertyName("breedingRatePerYear"), JsonRequired] double BreedingRatePerYear,
    [property: JsonPropertyName("fodderGood"), JsonRequired] string FodderGood,
    [property: JsonPropertyName("fodderPerHeadPerYear"), JsonRequired] double FodderPerHeadPerYear);

/// <summary>ADR-056 §1 — one economic-content packet (goods.json <c>packets</c>): an id goods and recipes name in their
/// <c>packet</c> field, and THE SWITCH. A disabled packet's goods and recipes are removed at load, so the world it founds
/// and steps is the pre-packet world byte for byte (the switch-off twin).</summary>
public sealed record PacketEntry(
    [property: JsonPropertyName("id"), JsonRequired] string Id,
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled);

public sealed record RecipeInput(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("perOutput"), JsonRequired] double PerOutput);

public sealed record RecipeOutput(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("qty"), JsonRequired] int Qty);

/// <summary>
/// One recipe (T3.2 registers + validates; T3.3 executes). Requires is an
/// OPTIONAL D-020 availability predicate over published variables — a knowledge
/// gate, never a calendar date (law 4).
///
/// DIMENSIONAL DECLARATION (corrected at T3.3, S8 §4.1 requirement 2): a
/// recipe input's PerOutput and the recipe's LaborPerOutput are PER EXECUTION,
/// and one execution yields Output.Qty units. T3.2's data doc said "per output
/// unit", which contradicted the data it shipped — `toolmaking` declares bronze
/// 1.0 with qty 2, so a per-unit reading would make Qty decorative and delete
/// toolmaking's value-add. ProductionSystem divides PerOutput by Qty once, at
/// the point of use, to get the per-unit coefficient; the 2-tools-per-bronze
/// ratio is pinned exactly by
/// ProductionTests.Recipe_InputsAndLabor_ArePerEXECUTION_NotPerOutputUnit.
///
/// R1 (research → gameplay unlock pipeline) — `Entity` links the recipe to its research.json registry
/// entity of kind recipe (e.g. recipe.pottery_firing): the recipe runs in a settlement only when that
/// entity is knowledge-eligible for the settlement's controller (CraftingQuery.IsRecipeAvailable, the
/// one recipe predicate). Data, never an id in code; absent = no knowledge requirement; an entity whose
/// requirement is null is a declared BASELINE recipe. Cross-validated against research.json at the
/// four-file load (the entity must exist and be a recipe).
///
/// ADR-056 — <c>Knowledge</c>: an OPTIONAL node expression (research dialect, existing node ids; the ADR-033 D4 form) the
/// settlement's knowledge must ALSO satisfy, for a recipe TC-1 gives no research.json entity (the M7 economy packets add
/// none). <c>Packet</c>: the recipe's economic-content packet (filtered out at load when the packet is disabled).
/// </summary>
public sealed record RecipeEntry(
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("inputs"), JsonRequired] RecipeInput[] Inputs,
    [property: JsonPropertyName("laborPerOutput"), JsonRequired] double LaborPerOutput,
    [property: JsonPropertyName("output"), JsonRequired] RecipeOutput Output,
    [property: JsonPropertyName("requires")] string? Requires = null,
    [property: JsonPropertyName("entity")] string? Entity = null,
    [property: JsonPropertyName("knowledge")] string? Knowledge = null,
    [property: JsonPropertyName("packet")] string? Packet = null);

/// <summary>M4-D: one material requirement of a construction project. Absolute
/// units, NOT a per-output coefficient — a project is built once, not per unit,
/// which is exactly why it cannot borrow <see cref="RecipeInput"/>. WS1 (D-054):
/// the units go in incrementally, in lockstep with the work — after progress p of
/// N exactly ceil(Qty × p / N) have been delivered (ConstructionQuery.Delivered),
/// so the project takes exactly Qty in total. The loader bounds Qty so that
/// Qty × N stays inside a long.</summary>
public sealed record ProjectInput(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("qty"), JsonRequired] long Qty);

/// <summary>
/// M4-D — ONE CONSTRUCTABLE STRUCTURE, and a deliberate SECOND definition
/// alongside <see cref="RecipeEntry"/> rather than a reuse of it.
///
/// A recipe's contract is that it OUTPUTS A GOOD (<see cref="RecipeOutput"/> is
/// required and names one), and its inputs and labour are per EXECUTION with a
/// per-unit divisor. A structure is not a good: it has no stock row, no price,
/// no bulk, and nothing trades it. Expressing a granary as a recipe would mean
/// inventing a phantom good to be its output, which breaks the goods registry's
/// invariants and lies to the price and trade systems. So the extension is a
/// parallel entry, and it is kept as small as the job allows — a name, a
/// material list, and the labour the job takes.
///
/// DIMENSIONAL DECLARATION (S8 §4.1 requirement 2): `Inputs[].Qty` is in whole
/// units of the named good, absolute per project. `LaborRequired` is in
/// ADULT-YEARS — the same unit the construction sector's pool is measured in
/// (share × adults × dtYears), so the ratio capacity / LaborRequired is the
/// dimensionless share of the project one turn's builders can do, and no
/// conversion constant is invented. There is NO build-time field: how many turns
/// a project takes is emergent from what the settlement can marshal — its
/// builders' pace and its materials (WS1, D-054: labour is a pace, not a gate).
///
/// ADR-033 D3 — `Entity` links the project to its research.json registry entity
/// (e.g. building.granary): the project is AVAILABLE to an issuer only when that
/// entity is knowledge-eligible for it (ConstructionQuery.IsProjectAvailable), which
/// makes ADR-028's LOCKED / AVAILABLE expressible. Data, never an id in code; absent
/// = no knowledge requirement. Cross-validated against research.json at the
/// four-file load (the entity must exist and be a building, an institution or
/// infrastructure).
/// </summary>
public sealed record ConstructionProjectEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("inputs"), JsonRequired] ProjectInput[] Inputs,
    [property: JsonPropertyName("laborRequired"), JsonRequired] double LaborRequired,
    [property: JsonPropertyName("entity")] string? Entity = null,
    [property: JsonPropertyName("founds")] ProjectFounds? Founds = null);

/// <summary>
/// ADR-033 D6 — what a completed project FOUNDS besides its building: an institution (today one of the
/// five specialized universities). <c>Entity</c> is the institution's research.json entity (e.g.
/// <c>inst.university</c>): the project is AVAILABLE only when BOTH the project's own <c>entity</c> (the
/// building it constructs) and this one are knowledge-eligible (ConstructionQuery.IsKnowledgeEligible —
/// the one availability predicate). <c>UniversityType</c> is the research.json <c>universityTypes[].id</c>
/// of the institution the completion founds (InstitutionsSystem founds it the step after the building
/// stands). Data, never an id in code; cross-validated against research.json at the four-file load.
/// </summary>
public sealed record ProjectFounds(
    [property: JsonPropertyName("entity"), JsonRequired] string Entity,
    [property: JsonPropertyName("universityType"), JsonRequired] string UniversityType);

/// <summary>
/// The D-031 goods registry + recipe book (T3.2), loaded from goods.json on
/// the T0.4 template. Travels with SimConfig (like NeedsConfig) so system
/// construction stays single-config. GrainId is the D-030 numéraire's id —
/// exactly one good declares numeraire and its name must be "grain".
/// </summary>
public sealed record GoodsConfig(
    [property: JsonPropertyName("goods"), JsonRequired] GoodEntry[] Goods,
    [property: JsonPropertyName("recipes"), JsonRequired] RecipeEntry[] Recipes,
    [property: JsonPropertyName("projects")] ConstructionProjectEntry[]? Projects = null,
    [property: JsonPropertyName("packets")] PacketEntry[]? Packets = null)
{
    /// <summary>The numéraire's id (D-030). Set at load after validation.</summary>
    [JsonIgnore] public int GrainId { get; init; }

    /// <summary>ADR-056 §1: the names of the goods the load REMOVED because their packet is disabled (goods.json order).
    /// A sim.json list that names one (discovery.concealableGoods) skips it rather than failing: the switch-off twin must
    /// load the same sim.json.</summary>
    [JsonIgnore] public string[] DisabledGoodNames { get; init; } = [];

    /// <summary>ADR-056 §1: whether packet <paramref name="id"/> is declared and enabled in this config.</summary>
    public bool IsPacketEnabled(string id)
    {
        foreach (PacketEntry p in Packets ?? [])
            if (string.Equals(p.Id, id, StringComparison.Ordinal)) return p.Enabled;
        return false;
    }

    /// <summary>
    /// ADR-056 §1 — THE SWITCH-OFF TWIN of a loaded config: the same config with packet <paramref name="packet"/> disabled
    /// (its goods and recipes removed exactly as the load removes a disabled packet's, the result re-validated). Pure; the
    /// attribution controls (TestConfigs.PreEc*) build the pre-packet world with it.
    /// </summary>
    public GoodsConfig WithoutPacket(string packet)
    {
        PacketEntry[] packets = Packets ?? [];
        bool declared = false;
        var next = new PacketEntry[packets.Length];
        for (int i = 0; i < packets.Length; i++)
        {
            bool match = string.Equals(packets[i].Id, packet, StringComparison.Ordinal);
            declared |= match;
            next[i] = match ? packets[i] with { Enabled = false } : packets[i];
        }
        if (!declared) throw new GoodsConfigException($"packet '{packet}' is not declared in goods.json packets.");
        return GoodsConfigLoader.FilterAndValidate(this with { Packets = next });
    }

    /// <summary>The construction project with this id, or null if unknown.</summary>
    public ConstructionProjectEntry? ProjectById(int id)
    {
        ConstructionProjectEntry[] projects = Projects ?? [];
        for (int i = 0; i < projects.Length; i++) if (projects[i].Id == id) return projects[i];
        return null;
    }

    /// <summary>Whether the roster holds good <paramref name="id"/>.</summary>
    public bool Contains(int id)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i].Id == id) return true;
        return false;
    }

    public GoodEntry ById(int id)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i].Id == id) return Goods[i];
        throw new ArgumentOutOfRangeException(nameof(id), id, "unknown good id");
    }

    public int IdOf(string name)
    {
        for (int i = 0; i < Goods.Length; i++)
            if (string.Equals(Goods[i].Name, name, StringComparison.Ordinal)) return Goods[i].Id;
        return -1;
    }
}

public static class GoodsConfigLoader
{
    /// <summary>ADR-056 §5 adds "mount" (EC-2 horses: neither food, nor raw extraction, nor a crafted good).</summary>
    private static readonly string[] Categories = ["food", "raw", "processed", "mount"];
    private static readonly string[] Channels = ["moisture", "elevation", "water"];

    public static GoodsConfig Load(Stream json)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd());
    }

    public static GoodsConfig Load(string json)
    {
        GoodsConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<GoodsConfig>(json);
        }
        catch (JsonException e)
        {
            throw new GoodsConfigException(
                $"goods config is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (cfg is null) throw new GoodsConfigException("goods config is empty.");
        return FilterAndValidate(cfg);
    }

    /// <summary>
    /// ADR-056 §1 — the packet filter, then every rule below. A good or recipe naming an undeclared packet is refused; one
    /// naming a DISABLED packet is removed (its name recorded in <see cref="GoodsConfig.DisabledGoodNames"/>), so the
    /// remaining roster is validated exactly as a hand-written pre-packet file would be — a recipe of an enabled packet that
    /// needs a disabled packet's good fails here, by name, never mid-turn.
    /// </summary>
    internal static GoodsConfig FilterAndValidate(GoodsConfig cfg)
    {
        if (cfg.Goods is null || cfg.Goods.Length == 0)
            throw new GoodsConfigException("goods must have at least one entry.");
        PacketEntry[] packets = cfg.Packets ?? [];
        var packetIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < packets.Length; i++)
        {
            if (packets[i] is null || string.IsNullOrWhiteSpace(packets[i].Id))
                throw new GoodsConfigException($"packets[{i}].id must be non-empty.");
            if (!packetIds.Add(packets[i].Id))
                throw new GoodsConfigException($"packets[{i}].id '{packets[i].Id}' is a duplicate.");
        }
        var keptGoods = new List<GoodEntry>(cfg.Goods.Length);
        var disabled = new List<string>(cfg.DisabledGoodNames);
        for (int i = 0; i < cfg.Goods.Length; i++)
        {
            GoodEntry g = cfg.Goods[i];
            if (g is null) throw new GoodsConfigException($"goods[{i}] is null.");
            if (g.Packet is { } gp && !packetIds.Contains(gp))
                throw new GoodsConfigException($"goods[{i}] ({g.Name}).packet '{gp}' is not declared in packets.");
            if (g.Packet is { } on && !cfg.IsPacketEnabled(on)) { disabled.Add(g.Name); continue; }
            keptGoods.Add(g);
        }
        RecipeEntry[] recipes = cfg.Recipes ?? [];
        var keptRecipes = new List<RecipeEntry>(recipes.Length);
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeEntry r = recipes[i];
            if (r is null) throw new GoodsConfigException($"recipes[{i}] is null.");
            if (r.Packet is { } rp && !packetIds.Contains(rp))
                throw new GoodsConfigException($"recipes[{i}] ({r.Name}).packet '{rp}' is not declared in packets.");
            if (r.Packet is { } on && !cfg.IsPacketEnabled(on)) continue;
            keptRecipes.Add(r);
        }
        cfg = cfg with { Goods = [.. keptGoods], Recipes = [.. keptRecipes], DisabledGoodNames = [.. disabled] };
        return Validate(cfg);
    }

    private static GoodsConfig Validate(GoodsConfig cfg)
    {
        if (cfg.Goods.Length == 0)
            throw new GoodsConfigException("goods must have at least one entry.");

        int numeraire = -1;
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < cfg.Goods.Length; i++)
        {
            GoodEntry g = cfg.Goods[i];
            if (g is null) throw new GoodsConfigException($"goods[{i}] is null.");
            if (g.Id <= 0)
                throw new GoodsConfigException($"goods[{i}].id must be > 0, got {g.Id}.");
            if (i > 0 && g.Id <= cfg.Goods[i - 1].Id)
                throw new GoodsConfigException(
                    $"goods ids must be strictly ascending: [{i - 1}].id {cfg.Goods[i - 1].Id} >= [{i}].id {g.Id}.");
            if (string.IsNullOrWhiteSpace(g.Name))
                throw new GoodsConfigException($"goods[{i}].name must be non-empty.");
            if (!names.Add(g.Name))
                throw new GoodsConfigException($"goods[{i}].name '{g.Name}' is a duplicate.");
            if (Array.IndexOf(Categories, g.Category) < 0)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).category '{g.Category}' unknown — expected one of: {string.Join(", ", Categories)}.");
            if (!(g.BulkPerUnit > 0.0) || !double.IsFinite(g.BulkPerUnit))
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).bulkPerUnit must be a finite value > 0, got {Inv(g.BulkPerUnit)}.");
            if (g.DepositChannel is not null && Array.IndexOf(Channels, g.DepositChannel) < 0)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).depositChannel '{g.DepositChannel}' unknown — expected one of: {string.Join(", ", Channels)}.");
            if (g.DepositSpread is < 0.0 or >= 1.0 || !double.IsFinite(g.DepositSpread))
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).depositSpread must be in [0,1), got {Inv(g.DepositSpread)}.");
            if (g.DepositSpread > 0.0 && g.DepositChannel is null)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}) declares depositSpread without a depositChannel.");
            if (g.Numeraire)
            {
                if (numeraire >= 0)
                    throw new GoodsConfigException(
                        $"goods[{i}] ({g.Name}) declares numeraire, but '{cfg.ById(numeraire).Name}' already did — D-030 has exactly one.");
                if (!string.Equals(g.Name, "grain", StringComparison.Ordinal))
                    throw new GoodsConfigException(
                        $"the numeraire must be grain (D-030), got '{g.Name}'.");
                numeraire = g.Id;
            }
        }
        if (numeraire < 0)
            throw new GoodsConfigException("no good declares numeraire — D-030 requires grain to.");

        // ADR-056: the knowledge gate's SHAPE (that it names existing nodes is checked where goods.json meets research.json,
        // SimConfigLoader.ValidateProjectsAgainstContent).
        for (int i = 0; i < cfg.Goods.Length; i++)
        {
            GoodEntry g = cfg.Goods[i];
            if (g.Knowledge is not null && string.IsNullOrWhiteSpace(g.Knowledge))
                throw new GoodsConfigException($"goods[{i}] ({g.Name}).knowledge must be non-empty when present.");
            if (g.Knowledge is not null && g.DepositChannel is null && g.Herd is null)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).knowledge gates working a deposit or breeding a herd — the good has neither.");
            if (g.Herd is not { } h) continue;
            if (g.DepositChannel is not null)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}) declares both a herd and a depositChannel — a herd grows from itself, it is not extracted.");
            if (g.Numeraire)
                throw new GoodsConfigException($"goods[{i}] ({g.Name}): the numeraire cannot be a herd.");
            int pasture = cfg.IdOf(h.PastureGood);
            if (pasture < 0 || cfg.ById(pasture).DepositChannel is null)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).herd.pastureGood '{h.PastureGood}' is not a deposit good in the roster.");
            int fodder = cfg.IdOf(h.FodderGood);
            if (fodder < 0 || fodder == g.Id)
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).herd.fodderGood '{h.FodderGood}' is not another good in the roster.");
            CheckRate(i, g.Name, "headPerHerder", h.HeadPerHerder);
            CheckRate(i, g.Name, "capturePerHerderPerYear", h.CapturePerHerderPerYear);
            CheckRate(i, g.Name, "breedingRatePerYear", h.BreedingRatePerYear);
            CheckRate(i, g.Name, "fodderPerHeadPerYear", h.FodderPerHeadPerYear);
            if (!(h.HeadPerHerder > 0.0) || !(h.BreedingRatePerYear > 0.0))
                throw new GoodsConfigException(
                    $"goods[{i}] ({g.Name}).herd: headPerHerder and breedingRatePerYear must be > 0 (a herd with no " +
                    "capacity or no increase is not a herd).");
        }

        if (cfg.Recipes is null) cfg = cfg with { Recipes = [] };
        var recipeNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < cfg.Recipes.Length; i++)
        {
            RecipeEntry r = cfg.Recipes[i];
            if (r is null) throw new GoodsConfigException($"recipes[{i}] is null.");
            if (string.IsNullOrWhiteSpace(r.Name))
                throw new GoodsConfigException($"recipes[{i}].name must be non-empty.");
            if (!recipeNames.Add(r.Name))
                throw new GoodsConfigException($"recipes[{i}].name '{r.Name}' is a duplicate.");
            if (r.Inputs is null || r.Inputs.Length == 0)
                throw new GoodsConfigException(
                    $"recipes[{i}] ({r.Name}) has no inputs — production from nothing violates law 1.");
            foreach (RecipeInput input in r.Inputs)
            {
                if (cfg.IdOf(input.Good) < 0)
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}) input good '{input.Good}' is not in the roster.");
                if (!(input.PerOutput > 0.0) || !double.IsFinite(input.PerOutput))
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}) input '{input.Good}'.perOutput must be a finite value > 0, got {Inv(input.PerOutput)}.");
            }
            if (r.Output is null || cfg.IdOf(r.Output.Good) < 0)
                throw new GoodsConfigException(
                    $"recipes[{i}] ({r.Name}) output good '{r.Output?.Good}' is not in the roster.");
            if (r.Output.Qty <= 0)
                throw new GoodsConfigException(
                    $"recipes[{i}] ({r.Name}) output qty must be > 0, got {r.Output.Qty}.");
            foreach (RecipeInput input in r.Inputs)
                if (string.Equals(input.Good, r.Output.Good, StringComparison.Ordinal))
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}) consumes its own output '{input.Good}'.");
            // ADR-056 §5: a herd good banks its fodder in its stock row's ConsumeRemainder, so no recipe may consume it.
            foreach (RecipeInput input in r.Inputs)
                if (cfg.IdOf(input.Good) is var hid and >= 0 && cfg.ById(hid).Herd is not null)
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}) consumes the herd good '{input.Good}' — a herd is bred, held and traded, " +
                        "never a recipe input (its row's ConsumeRemainder banks its fodder).");
            // DUPLICATE INPUTS (T3.3 adversarial finding): two entries for one
            // good made the Leontief cap and the sink asymmetric — the cap took
            // the coefficient once, the sink loop charged it per ENTRY — so a
            // purely data-level edit crashed the turn with a Ledger overdraw.
            // CLAUDE.md says tuning data is always allowed, so the loader owes
            // an actionable rejection rather than a mid-turn exception.
            var inputNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecipeInput input in r.Inputs)
                if (!inputNames.Add(input.Good))
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}) lists input '{input.Good}' more than once — " +
                        "combine the entries into one perOutput coefficient.");
            if (!(r.LaborPerOutput >= 0.0) || !double.IsFinite(r.LaborPerOutput))
                throw new GoodsConfigException(
                    $"recipes[{i}] ({r.Name}).laborPerOutput must be a finite value >= 0, got {Inv(r.LaborPerOutput)}.");
            if (r.Knowledge is not null && string.IsNullOrWhiteSpace(r.Knowledge))
                throw new GoodsConfigException($"recipes[{i}] ({r.Name}).knowledge must be non-empty when present.");
            if (r.Requires is not null)
            {
                try
                {
                    _ = ClassMobility.Predicate.Parse(r.Requires);
                }
                catch (ClassMobility.PredicateFormatException ex)
                {
                    throw new GoodsConfigException(
                        $"recipes[{i}] ({r.Name}).requires: {ex.Message}", ex);
                }
            }
        }

        // M4-D: construction projects. Optional — a config with none is legal
        // and simply offers nothing to build.
        if (cfg.Projects is null) cfg = cfg with { Projects = [] };
        var projectIds = new HashSet<int>();
        var projectNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < cfg.Projects.Length; i++)
        {
            ConstructionProjectEntry pr = cfg.Projects[i];
            if (pr is null) throw new GoodsConfigException($"projects[{i}] is null.");
            if (string.IsNullOrWhiteSpace(pr.Name))
                throw new GoodsConfigException($"projects[{i}].name must be non-empty.");
            if (!projectNames.Add(pr.Name))
                throw new GoodsConfigException($"projects[{i}].name '{pr.Name}' is a duplicate.");
            if (pr.Id < 0)
                throw new GoodsConfigException(
                    $"projects[{i}] ({pr.Name}).id must be >= 0, got {pr.Id} — the id is carried by an " +
                    "order's Amount field and must be a non-negative whole number.");
            if (pr.Id >= 1 << Kernel.OrderLog.CancelProjectBits)
                throw new GoodsConfigException(
                    $"projects[{i}] ({pr.Name}).id must be below {1 << Kernel.OrderLog.CancelProjectBits}, got {pr.Id} — a cancel " +
                    "order (kind 13, D-056) carries the project id in 16 bits of its Amount.");
            if (!projectIds.Add(pr.Id))
                throw new GoodsConfigException($"projects[{i}] ({pr.Name}).id {pr.Id} is a duplicate.");
            if (pr.Inputs is null || pr.Inputs.Length == 0)
                throw new GoodsConfigException(
                    $"projects[{i}] ({pr.Name}) has no inputs — building from nothing violates law 1.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectInput input in pr.Inputs)
            {
                if (cfg.IdOf(input.Good) < 0)
                    throw new GoodsConfigException(
                        $"projects[{i}] ({pr.Name}) input good '{input.Good}' is not in the roster.");
                if (input.Qty <= 0)
                    throw new GoodsConfigException(
                        $"projects[{i}] ({pr.Name}) input '{input.Good}'.qty must be > 0, got {input.Qty}.");
                // WS1 (D-054): the lockstep delivery ceil(qty × p / N) is exact long arithmetic, so qty × N must fit.
                if (input.Qty > Sim.Core.State.ConstructionQuery.MaxInputQty)
                    throw new GoodsConfigException(
                        $"projects[{i}] ({pr.Name}) input '{input.Good}'.qty must be at most " +
                        $"{Sim.Core.State.ConstructionQuery.MaxInputQty} (the incremental delivery multiplies it by " +
                        $"{Sim.Core.State.ConstructionQuery.ProgressUnits} progress units in exact integer arithmetic), got {input.Qty}.");
                if (!seen.Add(input.Good))
                    throw new GoodsConfigException(
                        $"projects[{i}] ({pr.Name}) names input '{input.Good}' twice — one line per good, " +
                        "or the per-good draw and delivery accounting would check one line and consume both.");
            }
            if (!(pr.LaborRequired > 0.0) || !double.IsFinite(pr.LaborRequired))
                throw new GoodsConfigException(
                    $"projects[{i}] ({pr.Name}).laborRequired must be a finite value > 0 (adult-years), " +
                    $"got {Inv(pr.LaborRequired)}.");
            // ADR-033 D6: a project that FOUNDS an institution names the institution's entity and its
            // university type. The shape is checked here; that both exist in research.json is checked
            // where goods.json meets research.json (SimConfigLoader.ValidateProjectsAgainstContent).
            if (pr.Founds is { } founds)
            {
                if (string.IsNullOrWhiteSpace(founds.Entity))
                    throw new GoodsConfigException($"projects[{i}] ({pr.Name}).founds.entity must be non-empty.");
                if (string.IsNullOrWhiteSpace(founds.UniversityType))
                    throw new GoodsConfigException($"projects[{i}] ({pr.Name}).founds.universityType must be non-empty.");
                for (int j = 0; j < i; j++)
                    if (cfg.Projects[j].Founds is { } other
                        && string.Equals(other.UniversityType, founds.UniversityType, StringComparison.Ordinal))
                        throw new GoodsConfigException(
                            $"projects[{i}] ({pr.Name}) founds university type '{founds.UniversityType}', which " +
                            $"projects[{j}] ({cfg.Projects[j].Name}) already founds — one project per type, so a " +
                            "settlement's university count of a type is the count of one project's structures.");
            }
        }

        return cfg with { GrainId = numeraire };
    }

    private static void CheckRate(int i, string good, string field, double v)
    {
        if (!(v >= 0.0) || !double.IsFinite(v))
            throw new GoodsConfigException($"goods[{i}] ({good}).herd.{field} must be a finite value >= 0, got {Inv(v)}.");
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
