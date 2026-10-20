using System.Text.Json;
using System.Text.Json.Serialization;
using Sim.Core.Systems.Research;

namespace Sim.Core.Systems.Ages;

/// <summary>Raised on any unit-families.json schema violation, with an actionable message.</summary>
public sealed class UnitFamilyContentException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Whether a family's formations are internal composition of an Army MobileAgent
/// or are themselves MobileAgent tokens (D-043 B5; ledger §17.4).</summary>
public enum UnitComposition { ArmyFormation = 1, MobileAgent = 2 }

/// <summary>What happens to a formation whose family line has ENDED by the new Age
/// (D-047 Part 3: preserve, or convert to an explicitly defined generic successor).</summary>
public enum NoSuccessorRule { Preserve = 1, Generic = 2 }

/// <summary>One per-Age realization of a unit family (a generic unit identity).
/// <c>Age</c> is the first Age in which it realizes the family. A <c>Branch</c>
/// identity is a declared alternative realization; automatic conversion never
/// targets it (the mainline does), so modernization is deterministic.</summary>
public sealed record UnitIdentity(
    int Key, string Id, string Name, int Age, bool Branch, string HistoricalEmergence,
    IReadOnlyList<string> Equipment, string? RealizedBy, string? Reference, int FamilyKey);

/// <summary>A declared branching point of a family line (documentation for the UI;
/// conversion follows the mainline).</summary>
public sealed record UnitFamilyBranch(int AtAge, IReadOnlyList<string> Identities, string Rule);

/// <summary>One unit family — a military ROLE persisting across Ages (D-047 Part 2).</summary>
public sealed class UnitFamily
{
    public required int Index { get; init; }
    public required int Key { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string HistoricalEmergence { get; init; }
    public required UnitComposition Composition { get; init; }
    public required bool AutoModernize { get; init; }
    public required IReadOnlyList<UnitFamilyBranch> Branches { get; init; }
    /// <summary>The last Age the family has a realization in, or null if the line never ends.</summary>
    public required int? LineEndsAfterAge { get; init; }
    public required NoSuccessorRule NoSuccessor { get; init; }
    /// <summary>The generic successor family's key when <see cref="NoSuccessor"/> is Generic, else -1.</summary>
    public required int GenericSuccessorFamily { get; init; }
    public required string? NoSuccessorNote { get; init; }
    /// <summary>All identities (mainline and branch) in content order — Age ascending.</summary>
    public required IReadOnlyList<UnitIdentity> Line { get; init; }

    /// <summary>The first Age the family is realized in (its earliest mainline identity).</summary>
    public int FirstAge => Mainline(0).Age;

    /// <summary>The family's MAINLINE realization at <paramref name="age"/>: the mainline identity
    /// with the greatest Age &lt;= age, or null when the family does not exist yet at that Age or its
    /// line has ended before it.</summary>
    public UnitIdentity? RealizationAt(int age)
    {
        if (LineEndsAfterAge is int end && age > end) return null;
        UnitIdentity? best = null;
        for (int i = 0; i < Line.Count; i++)
            if (!Line[i].Branch && Line[i].Age <= age) best = Line[i];
        return best;
    }

    /// <summary>The predecessor of a mainline identity in this family, or null for the first.</summary>
    public UnitIdentity? Predecessor(UnitIdentity identity)
    {
        UnitIdentity? previous = null;
        for (int i = 0; i < Line.Count; i++)
        {
            if (Line[i].Branch) continue;
            if (Line[i].Key == identity.Key) return previous;
            previous = Line[i];
        }
        return null;
    }

    /// <summary>The next mainline identity after <paramref name="identity"/>, or null when the line
    /// has no later realization (a branch identity's successor is the next mainline identity after
    /// its Age).</summary>
    public UnitIdentity? Successor(UnitIdentity identity)
    {
        for (int i = 0; i < Line.Count; i++)
            if (!Line[i].Branch && Line[i].Age > identity.Age) return Line[i];
        return null;
    }

    private UnitIdentity Mainline(int ordinal)
    {
        int seen = 0;
        for (int i = 0; i < Line.Count; i++)
            if (!Line[i].Branch && seen++ == ordinal) return Line[i];
        throw new InvalidOperationException($"family '{Id}' has no mainline identity #{ordinal}");
    }
}

/// <summary>
/// ADR-031 — the loaded unit-family graph (unit-families.json). Lookups are linear or
/// index-based over content order; there is no dictionary (law 5).
/// </summary>
public sealed class UnitFamilyContent
{
    public required IReadOnlyList<UnitFamily> Families { get; init; }
    /// <summary>The identity every founded Empire fields (baseline.basic_military).</summary>
    public required UnitIdentity FoundingIdentity { get; init; }
    public required int FormationsPerPolity { get; init; }
    public required string FoundingBasis { get; init; }
    public required string Status { get; init; }

    public UnitFamily? FamilyByKey(int key)
    {
        for (int i = 0; i < Families.Count; i++) if (Families[i].Key == key) return Families[i];
        return null;
    }

    public UnitFamily? FamilyById(string id)
    {
        for (int i = 0; i < Families.Count; i++)
            if (string.Equals(Families[i].Id, id, StringComparison.Ordinal)) return Families[i];
        return null;
    }

    public UnitIdentity? IdentityByKey(int key)
    {
        for (int f = 0; f < Families.Count; f++)
            for (int i = 0; i < Families[f].Line.Count; i++)
                if (Families[f].Line[i].Key == key) return Families[f].Line[i];
        return null;
    }

    public UnitIdentity? IdentityById(string id)
    {
        for (int f = 0; f < Families.Count; f++)
            for (int i = 0; i < Families[f].Line.Count; i++)
                if (string.Equals(Families[f].Line[i].Id, id, StringComparison.Ordinal)) return Families[f].Line[i];
        return null;
    }
}

/// <summary>Loads and validates unit-families.json on the T0.4 template (Sim.Core takes a
/// Stream or string; loud, actionable errors).</summary>
public static class UnitFamilyContentLoader
{
    public const int Schema = 1;
    public const int AgeCount = 9;

    private static readonly JsonSerializerOptions JsonOptions = new() { RespectNullableAnnotations = true };

    public static UnitFamilyContent Load(Stream json, ResearchContent? research)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd(), research);
    }

    public static UnitFamilyContent Load(string json, ResearchContent? research)
    {
        FileJson? file;
        try { file = JsonSerializer.Deserialize<FileJson>(json, JsonOptions); }
        catch (JsonException e)
        {
            throw new UnitFamilyContentException($"unit-families.json is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (file is null) throw Fail("the document is empty (null).");
        if (file.Schema != Schema) throw Fail($"schema is {file.Schema}, expected {Schema}.");
        if (file.Families.Length == 0) throw Fail("families must not be empty.");

        var families = new UnitFamily[file.Families.Length];
        var familyKeys = new List<int>();
        var familyIds = new List<string>();
        var identityKeys = new List<int>();
        var identityIds = new List<string>();
        for (int f = 0; f < file.Families.Length; f++)
        {
            FamilyJson j = file.Families[f];
            string path = $"families[{f}] ('{j.Id}')";
            if (j.Key < 1) throw Fail($"{path}: key must be >= 1, got {j.Key}.");
            if (familyKeys.Contains(j.Key)) throw Fail($"{path}: duplicate family key {j.Key}.");
            if (familyIds.Contains(j.Id)) throw Fail($"{path}: duplicate family id.");
            familyKeys.Add(j.Key);
            familyIds.Add(j.Id);
            UnitComposition composition = j.Composition switch
            {
                "army_formation" => UnitComposition.ArmyFormation,
                "mobile_agent" => UnitComposition.MobileAgent,
                _ => throw Fail($"{path}: composition must be 'army_formation' or 'mobile_agent', got '{j.Composition}'."),
            };
            if (j.Line.Length == 0) throw Fail($"{path}: line must have at least one identity.");
            if (j.LineEndsAfterAge is int ends && (ends < 1 || ends > AgeCount))
                throw Fail($"{path}: lineEndsAfterAge must be in 1..{AgeCount}, got {ends}.");

            var line = new UnitIdentity[j.Line.Length];
            int lastMainAge = 0;
            int mainlineCount = 0;
            for (int i = 0; i < j.Line.Length; i++)
            {
                IdentityJson ij = j.Line[i];
                string ip = $"{path}.line[{i}] ('{ij.Id}')";
                if (ij.Key < 1) throw Fail($"{ip}: key must be >= 1, got {ij.Key}.");
                if (identityKeys.Contains(ij.Key)) throw Fail($"{ip}: duplicate identity key {ij.Key} (keys are global across families).");
                if (identityIds.Contains(ij.Id)) throw Fail($"{ip}: duplicate identity id (ids are global across families).");
                identityKeys.Add(ij.Key);
                identityIds.Add(ij.Id);
                if (ij.Age < 1 || ij.Age > AgeCount) throw Fail($"{ip}: age must be in 1..{AgeCount}, got {ij.Age}.");
                if (j.LineEndsAfterAge is int e && ij.Age > e)
                    throw Fail($"{ip}: age {ij.Age} is after the family's lineEndsAfterAge {e}.");
                if (ij.Branch)
                {
                    if (ij.Age < lastMainAge || mainlineCount == 0)
                        throw Fail($"{ip}: a branch identity must follow a mainline identity of an Age at or before its own.");
                }
                else
                {
                    if (ij.Age <= lastMainAge)
                        throw Fail($"{ip}: mainline identities must have strictly increasing ages (got {ij.Age} after {lastMainAge}); " +
                                   "declare an alternative realization of the same Age with \"branch\": true.");
                    lastMainAge = ij.Age;
                    mainlineCount++;
                }
                if (ij.RealizedBy is { } entity && research is not null && research.EntityIndexOf(entity) < 0)
                    throw Fail($"{ip}: realizedBy '{entity}' is not an entity in research.json.");
                line[i] = new UnitIdentity(ij.Key, ij.Id, ij.Name, ij.Age, ij.Branch, ij.HistoricalEmergence,
                    ij.Equipment ?? [], ij.RealizedBy, ij.Reference, j.Key);
            }
            if (mainlineCount == 0) throw Fail($"{path}: line must have at least one mainline (non-branch) identity.");

            NoSuccessorRule rule = j.NoSuccessor.Rule switch
            {
                "preserve" => NoSuccessorRule.Preserve,
                "generic" => NoSuccessorRule.Generic,
                _ => throw Fail($"{path}: noSuccessor.rule must be 'preserve' or 'generic', got '{j.NoSuccessor.Rule}'."),
            };
            if (rule == NoSuccessorRule.Generic && j.NoSuccessor.Family is null)
                throw Fail($"{path}: noSuccessor.rule 'generic' must name the generic successor family.");
            if (rule == NoSuccessorRule.Preserve && j.NoSuccessor.Family is not null)
                throw Fail($"{path}: noSuccessor.rule 'preserve' must not name a family.");

            var branches = new UnitFamilyBranch[(j.Branches ?? []).Length];
            for (int b = 0; b < branches.Length; b++)
            {
                BranchJson bj = j.Branches![b];
                for (int k = 0; k < bj.Identities.Length; k++)
                {
                    bool found = false;
                    for (int i = 0; i < line.Length; i++) if (line[i].Id == bj.Identities[k]) { found = true; break; }
                    if (!found) throw Fail($"{path}.branches[{b}]: identity '{bj.Identities[k]}' is not in this family's line.");
                }
                branches[b] = new UnitFamilyBranch(bj.AtAge, bj.Identities, bj.Rule);
            }

            families[f] = new UnitFamily
            {
                Index = f, Key = j.Key, Id = j.Id, Name = j.Name, Role = j.Role,
                HistoricalEmergence = j.HistoricalEmergence, Composition = composition,
                AutoModernize = j.AutoModernize, Branches = branches, LineEndsAfterAge = j.LineEndsAfterAge,
                NoSuccessor = rule, GenericSuccessorFamily = -1, NoSuccessorNote = j.NoSuccessor.Note, Line = line,
            };
        }

        // Second pass: resolve generic successors (the family must exist, differ, and not be a cycle).
        for (int f = 0; f < families.Length; f++)
        {
            FamilyJson j = file.Families[f];
            if (families[f].NoSuccessor != NoSuccessorRule.Generic) continue;
            int target = -1;
            for (int g = 0; g < families.Length; g++) if (families[g].Id == j.NoSuccessor.Family) target = families[g].Key;
            if (target < 0) throw Fail($"families[{f}] ('{j.Id}'): noSuccessor.family '{j.NoSuccessor.Family}' is not a family.");
            if (target == families[f].Key) throw Fail($"families[{f}] ('{j.Id}'): the generic successor must be another family.");
            families[f] = new UnitFamily
            {
                Index = f, Key = families[f].Key, Id = families[f].Id, Name = families[f].Name, Role = families[f].Role,
                HistoricalEmergence = families[f].HistoricalEmergence, Composition = families[f].Composition,
                AutoModernize = families[f].AutoModernize, Branches = families[f].Branches,
                LineEndsAfterAge = families[f].LineEndsAfterAge, NoSuccessor = NoSuccessorRule.Generic,
                GenericSuccessorFamily = target, NoSuccessorNote = families[f].NoSuccessorNote, Line = families[f].Line,
            };
        }
        for (int f = 0; f < families.Length; f++)
        {
            // A generic chain must terminate: follow it, and fail on revisiting a family.
            var seen = new List<int> { families[f].Key };
            UnitFamily cur = families[f];
            while (cur.NoSuccessor == NoSuccessorRule.Generic)
            {
                int next = cur.GenericSuccessorFamily;
                if (seen.Contains(next)) throw Fail($"families[{f}] ('{families[f].Id}'): the generic-successor chain is a cycle.");
                seen.Add(next);
                for (int g = 0; g < families.Length; g++) if (families[g].Key == next) cur = families[g];
            }
        }

        var content = new UnitFamilyContent
        {
            Families = families,
            FoundingIdentity = null!,
            FormationsPerPolity = file.Founding.FormationsPerPolity,
            FoundingBasis = file.Founding.Basis,
            Status = file.Status,
        };
        UnitIdentity founding = content.IdentityById(file.Founding.Identity)
            ?? throw Fail($"founding.identity '{file.Founding.Identity}' is not an identity in any family.");
        if (founding.Branch) throw Fail("founding.identity must be a mainline identity.");
        if (file.Founding.FormationsPerPolity < 0) throw Fail("founding.formationsPerPolity must be >= 0.");
        return new UnitFamilyContent
        {
            Families = families, FoundingIdentity = founding, FormationsPerPolity = file.Founding.FormationsPerPolity,
            FoundingBasis = file.Founding.Basis, Status = file.Status,
        };
    }

    private static UnitFamilyContentException Fail(string message) => new($"unit-families.json: {message}");

    private sealed record FileJson(
        [property: JsonPropertyName("schema"), JsonRequired] int Schema,
        [property: JsonPropertyName("status"), JsonRequired] string Status,
        [property: JsonPropertyName("founding"), JsonRequired] FoundingJson Founding,
        [property: JsonPropertyName("families"), JsonRequired] FamilyJson[] Families);

    private sealed record FoundingJson(
        [property: JsonPropertyName("identity"), JsonRequired] string Identity,
        [property: JsonPropertyName("formationsPerPolity"), JsonRequired] int FormationsPerPolity,
        [property: JsonPropertyName("basis"), JsonRequired] string Basis);

    private sealed record FamilyJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("role"), JsonRequired] string Role,
        [property: JsonPropertyName("historicalEmergence"), JsonRequired] string HistoricalEmergence,
        [property: JsonPropertyName("composition"), JsonRequired] string Composition,
        [property: JsonPropertyName("autoModernize"), JsonRequired] bool AutoModernize,
        [property: JsonPropertyName("branches")] BranchJson[]? Branches,
        [property: JsonPropertyName("lineEndsAfterAge")] int? LineEndsAfterAge,
        [property: JsonPropertyName("noSuccessor"), JsonRequired] NoSuccessorJson NoSuccessor,
        [property: JsonPropertyName("line"), JsonRequired] IdentityJson[] Line);

    private sealed record BranchJson(
        [property: JsonPropertyName("atAge"), JsonRequired] int AtAge,
        [property: JsonPropertyName("identities"), JsonRequired] string[] Identities,
        [property: JsonPropertyName("rule"), JsonRequired] string Rule);

    private sealed record NoSuccessorJson(
        [property: JsonPropertyName("rule"), JsonRequired] string Rule,
        [property: JsonPropertyName("family")] string? Family,
        [property: JsonPropertyName("note")] string? Note);

    private sealed record IdentityJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("age"), JsonRequired] int Age,
        [property: JsonPropertyName("historicalEmergence"), JsonRequired] string HistoricalEmergence,
        [property: JsonPropertyName("equipment")] string[]? Equipment,
        [property: JsonPropertyName("realizedBy")] string? RealizedBy,
        [property: JsonPropertyName("reference")] string? Reference,
        [property: JsonPropertyName("branch")] bool Branch = false);
}
