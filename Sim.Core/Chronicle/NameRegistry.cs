using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Chronicle;

/// <summary>
/// Deterministic settlement names (T2.9): a pure function of (world seed,
/// settlement id, salt) over the data phonology — NO sim state, NO RngRegistry
/// stream (names are presentation, not mechanics; drawing from a sim stream
/// would perturb replay). Names are REGISTRY entries per ADR-001: variable-
/// length text lives outside sim tables, keyed by settlement id, and is never
/// serialized into snapshots.
///
/// COLLISION POLICY (stated per packet): names are assigned in ascending
/// settlement-id order; a generated name that collides with any already-
/// assigned name is REGENERATED with the salt incremented (salt starts at 0)
/// until unique — deterministic, no suffixing (a "-II" suffix would fake a
/// dynastic fact no mechanism recorded). The syllable space (~12×8×8 per
/// syllable, 2–3 syllables) dwarfs any M2 settlement count, so the loop
/// terminates in practice after 0–1 retries; a 10,000-salt sweep throws
/// loudly rather than spin.
/// </summary>
public sealed class NameRegistry
{
    private readonly Dictionary<int, string> _names = [];

    /// <summary>Builds names for every settlement present, ascending id.</summary>
    public static NameRegistry Build(ChronicleConfig cfg, ulong worldSeed, IReadOnlyWorldState world)
    {
        var ids = new List<int>(world.Settlements.Count);
        for (int i = 0; i < world.Settlements.Count; i++) ids.Add(world.Settlements[i].Id.Value);
        ids.Sort();
        var registry = new NameRegistry();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (int id in ids)
        {
            string name = UniqueName(cfg.Phonology, worldSeed, id, taken);
            registry._names[id] = name;
            taken.Add(name);
        }
        return registry;
    }

    public string Name(int settlementId) =>
        _names.TryGetValue(settlementId, out string? n)
            ? n
            : "settlement " + settlementId.ToString(CultureInfo.InvariantCulture);

    private static string UniqueName(
        PhonologyConfig p, ulong worldSeed, int id, HashSet<string> taken)
    {
        for (int salt = 0; salt < 10_000; salt++)
        {
            string candidate = Generate(p, worldSeed, id, salt);
            if (!taken.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException(
            $"name generation exhausted 10000 salts for settlement {id} — phonology space too small");
    }

    /// <summary>The pure generator: splitmix64 over (seed, id, salt) — an
    /// explicit mixing function, none of the banned randomness or hashing
    /// constructs (law 5).</summary>
    public static string Generate(PhonologyConfig p, ulong worldSeed, int settlementId, int salt)
    {
        ulong state = Mix(Mix(Mix(worldSeed) ^ (ulong)(uint)settlementId) ^ (ulong)(uint)salt);
        var sb = new System.Text.StringBuilder(12);
        int syllables = p.MinSyllables + (int)(Next(ref state) % (ulong)(p.MaxSyllables - p.MinSyllables + 1));
        for (int s = 0; s < syllables; s++)
        {
            sb.Append(p.Onsets[(int)(Next(ref state) % (ulong)p.Onsets.Length)]);
            sb.Append(p.Nuclei[(int)(Next(ref state) % (ulong)p.Nuclei.Length)]);
            // Codas only close the FINAL syllable — mid-word clusters like
            // "thr" from coda+onset collisions read as typos, not names.
            if (s == syllables - 1)
                sb.Append(p.Codas[(int)(Next(ref state) % (ulong)p.Codas.Length)]);
        }
        sb[0] = char.ToUpperInvariant(sb[0]);
        return sb.ToString();
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15ul;
        return Mix(state);
    }

    private static ulong Mix(ulong z)
    {
        z ^= z >> 30; z *= 0xBF58476D1CE4E5B9ul;
        z ^= z >> 27; z *= 0x94D049BB133111EBul;
        z ^= z >> 31;
        return z;
    }
}
