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
/// uneven geography is the precondition for comparative advantage).</summary>
public sealed record GoodEntry(
    [property: JsonPropertyName("id"), JsonRequired] int Id,
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("category"), JsonRequired] string Category,
    [property: JsonPropertyName("bulkPerUnit"), JsonRequired] double BulkPerUnit,
    [property: JsonPropertyName("numeraire")] bool Numeraire = false,
    [property: JsonPropertyName("depositChannel")] string? DepositChannel = null,
    [property: JsonPropertyName("depositSpread")] double DepositSpread = 0.0);

public sealed record RecipeInput(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("perOutput"), JsonRequired] double PerOutput);

public sealed record RecipeOutput(
    [property: JsonPropertyName("good"), JsonRequired] string Good,
    [property: JsonPropertyName("qty"), JsonRequired] int Qty);

/// <summary>One recipe (T3.2 registers + validates; T3.3 executes). Requires
/// is an OPTIONAL D-020 availability predicate over published variables — a
/// knowledge gate, never a calendar date (law 4).</summary>
public sealed record RecipeEntry(
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("inputs"), JsonRequired] RecipeInput[] Inputs,
    [property: JsonPropertyName("laborPerOutput"), JsonRequired] double LaborPerOutput,
    [property: JsonPropertyName("output"), JsonRequired] RecipeOutput Output,
    [property: JsonPropertyName("requires")] string? Requires = null);

/// <summary>
/// The D-031 goods registry + recipe book (T3.2), loaded from goods.json on
/// the T0.4 template. Travels with SimConfig (like NeedsConfig) so system
/// construction stays single-config. GrainId is the D-030 numéraire's id —
/// exactly one good declares numeraire and its name must be "grain".
/// </summary>
public sealed record GoodsConfig(
    [property: JsonPropertyName("goods"), JsonRequired] GoodEntry[] Goods,
    [property: JsonPropertyName("recipes"), JsonRequired] RecipeEntry[] Recipes)
{
    /// <summary>The numéraire's id (D-030). Set at load after validation.</summary>
    [JsonIgnore] public int GrainId { get; init; }

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
    private static readonly string[] Categories = ["food", "raw", "processed"];
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
        if (cfg.Goods is null || cfg.Goods.Length == 0)
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
            if (!(r.LaborPerOutput >= 0.0) || !double.IsFinite(r.LaborPerOutput))
                throw new GoodsConfigException(
                    $"recipes[{i}] ({r.Name}).laborPerOutput must be a finite value >= 0, got {Inv(r.LaborPerOutput)}.");
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

        return cfg with { GrainId = numeraire };
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
