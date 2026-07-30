using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Kernel;

/// <summary>Raised on any pipeline-config violation, with an actionable message.</summary>
public sealed class PipelineFormatException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Loader for pipeline.json (§3.3: the turn pipeline's system order is DATA).
/// Follows the T0.4 loader template: string/Stream in, filesystem-free,
/// strongly-typed rows, loud actionable errors. Resolves names against the
/// registered systems and returns registrations in configured order.
/// </summary>
public static class PipelineLoader
{
    private sealed record PipelineJson(
        [property: JsonPropertyName("pipeline")] List<string>? Pipeline);

    public static SystemRegistration[] Load(Stream json, SystemRegistration[] available)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd(), available);
    }

    public static SystemRegistration[] Load(string json, SystemRegistration[] available)
    {
        PipelineJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PipelineJson>(json);
        }
        catch (JsonException e)
        {
            throw new PipelineFormatException($"pipeline config is not valid JSON: {e.Message}", e);
        }

        if (parsed?.Pipeline is null || parsed.Pipeline.Count == 0)
            throw new PipelineFormatException(
                "pipeline config must contain a non-empty 'pipeline' array of system names.");

        // T3.6 (director decision 3b, T3.5b item-4 idiom): a duplicate NAME or
        // ID in the AVAILABLE roster would make every preset naming it bind
        // silently to whichever registration wins the linear scan — silent
        // mis-binding, the config-fails-quietly class this project keeps
        // paying for. Refuse the roster itself, before any preset entry binds.
        var rosterNames = new string[available.Length];
        var rosterIds = new int[available.Length];
        for (int i = 0; i < available.Length; i++)
        { rosterNames[i] = available[i].Name; rosterIds[i] = available[i].Id.Value; }
        ValidateRoster(rosterNames, rosterIds);

        string known = string.Join(", ", Names(available));
        var result = new SystemRegistration[parsed.Pipeline.Count];

        for (int i = 0; i < parsed.Pipeline.Count; i++)
        {
            string name = parsed.Pipeline[i];

            for (int j = 0; j < i; j++)
            {
                if (parsed.Pipeline[j] == name)
                    throw new PipelineFormatException(
                        $"pipeline[{i}] '{name}' is a duplicate (already listed at position {j}); " +
                        "each system may appear exactly once.");
            }

            SystemRegistration? match = null;
            for (int j = 0; j < available.Length; j++)
            {
                if (available[j].Name == name) { match = available[j]; break; }
            }
            result[i] = match ?? throw new PipelineFormatException(
                $"pipeline[{i}] '{name}' is not a registered system; known systems: {known}.");
        }

        return result;
    }

    /// <summary>
    /// The roster guard, PURE over projections so its arms are attackable
    /// directly (SystemRegistration's ctor is internal, deliberately — tests
    /// cannot fabricate colliding registrations, so the guard's logic is
    /// exposed instead). Throws on any duplicate name or duplicate id.
    /// </summary>
    public static void ValidateRoster(string[] names, int[] ids)
    {
        for (int i = 0; i < names.Length; i++)
        {
            for (int j = i + 1; j < names.Length; j++)
            {
                if (names[i] == names[j])
                    throw new PipelineFormatException(
                        $"the system roster is AMBIGUOUS: two registrations share the name "
                        + $"'{names[i]}' (ids {ids[i]} and {ids[j]}). A preset naming it would bind "
                        + "silently to whichever wins. Rename one (the M0 toy/real split uses "
                        + "'toytrade' vs 'trade').");
                if (ids[i] == ids[j])
                    throw new PipelineFormatException(
                        $"the system roster is AMBIGUOUS: registrations '{names[i]}' and "
                        + $"'{names[j]}' share WellKnownId {ids[i]}. Ids key RNG streams and "
                        + "ownership; a duplicate is a determinism fault. Assign a fresh id.");
            }
        }
    }

    private static string[] Names(SystemRegistration[] available)
    {
        var names = new string[available.Length];
        for (int i = 0; i < available.Length; i++) names[i] = available[i].Name;
        return names;
    }
}
