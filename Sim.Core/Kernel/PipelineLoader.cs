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

    private static string[] Names(SystemRegistration[] available)
    {
        var names = new string[available.Length];
        for (int i = 0; i < available.Length; i++) names[i] = available[i].Name;
        return names;
    }
}
