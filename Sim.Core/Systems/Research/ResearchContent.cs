using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sim.Core.State;
using Sim.Core.Systems.ClassMobility;

namespace Sim.Core.Systems.Research;

/// <summary>Raised on any research-content violation, with an actionable message
/// that names the file path, the node and the rule (T0.4 loader template).</summary>
public sealed class ResearchContentException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>The two player-facing research trees (D-044 R3). Nothing else is a tree.</summary>
public enum ResearchTree { Technology = 1, Civics = 2 }

/// <summary>Registry entity kinds whose knowledge requirement research.json carries (ADR-029 §10).</summary>
public enum ResearchEntityKind { Building = 1, Infrastructure = 2, Institution = 3, Unit = 4, Activity = 5, Project = 6 }

/// <summary>Why a Eureka is, or is not, machine-evaluable (ADR-029 §7).</summary>
public enum EurekaStatus
{
    /// <summary>Carries a D-020 research-dialect condition the engine evaluates.</summary>
    Evaluable = 1,
    /// <summary>The circumstance names something simulation state does not carry.</summary>
    NoStateCarrier = 2,
    /// <summary>"institution present: …" — no institutions system exists.</summary>
    InstitutionStateAbsent = 3,
    /// <summary>"contact with a civilization holding this" — no contact state exists (D-035-C carrier test).</summary>
    ContactStateAbsent = 4,
    /// <summary>The circumstance names knowledge the node's own prerequisites already guarantee
    /// (e.g. "circumstance: fire" on a node that requires fire_making). As a condition it would
    /// hold whenever the node is available, so it would be a flat cost cut, not a circumstance.
    /// It is declared instead of evaluated (ADR-029 §7).</summary>
    ImpliedByPrerequisites = 5,
}

/// <summary>TUNE values (chosen, not derived — S8 §4.1(c)); see ADR-029 §6–§7.</summary>
public sealed record ResearchTuning(double ClpCoefficient, double ClpAdultExponent, double EurekaCreditFraction);

/// <summary>One of the five Tree-1 subtrees (D-044 R3). <see cref="Index"/> is its
/// position 0..4; <see cref="Key"/> its stable data key.</summary>
public sealed record ResearchBranch(int Index, int Key, string Id, string Number, string Name);

/// <summary>A specialized-university type and the subtree it serves (D-044 R5).
/// <see cref="Key"/> is what ResearchCostModifierRow.UniversityType carries.</summary>
public sealed record UniversityType(int Key, string Id, string Name, int Branch);

/// <summary>One Eureka of a node: the corpus prose, its status, and — when
/// evaluable — the parsed condition. <see cref="Index"/> is its position in the
/// node's list, which is what ResearchEurekaRow.Eureka carries.</summary>
public sealed record ResearchEureka(int Index, string Text, EurekaStatus Status, Predicate? Condition);

/// <summary>
/// One Technology or Civics node (D-044 R6, R12). Immutable. <see cref="Index"/> is
/// the dense position in <see cref="ResearchContent.Nodes"/> (technologies first,
/// then civics, keys ascending) and the atom id prerequisite predicates use;
/// <see cref="Key"/> is the stable id rows and orders carry.
/// </summary>
public sealed class ResearchNode
{
    public required int Index { get; init; }
    public required ResearchNodeId Key { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    /// <summary>Age relevance — METADATA ONLY (D-044 R13, law 4): nothing gates on it.</summary>
    public required string Age { get; init; }
    public required bool Frontier { get; init; }
    public required string? Emerged { get; init; }
    public required ResearchTree Tree { get; init; }
    /// <summary>Subtree index 0..4, or -1 for the Main Technology Tree. Always -1 for Civics.</summary>
    public required int Branch { get; init; }
    public required string Domain { get; init; }
    public required IReadOnlyList<string> SecondaryDomains { get; init; }
    public required int Depth { get; init; }
    public required double BaseCost { get; init; }
    /// <summary>Null for a root node (no prerequisite).</summary>
    public required Predicate? Prerequisite { get; init; }
    /// <summary>Distinct prerequisite node indices, in source order (union over OR).</summary>
    public required IReadOnlyList<int> PrerequisiteNodes { get; init; }
    public required IReadOnlyList<ResearchEureka> Eurekas { get; init; }
    public required IReadOnlyList<string> Capabilities { get; init; }
    public required IReadOnlyList<string> Techniques { get; init; }
    public required IReadOnlyList<string> Applications { get; init; }
    /// <summary>Entity indices this node appears in the knowledge requirement of.</summary>
    public required IReadOnlyList<int> UnlockedEntities { get; init; }
    /// <summary>Node indices that name this node as a prerequisite, in index order (derived).</summary>
    public required IReadOnlyList<int> Dependents { get; init; }
    public required string? Family { get; init; }
    public required int? Generation { get; init; }
    /// <summary>The corpus's repeatable-frontier descriptor is kept as data; levels
    /// are NOT implemented — the node completes once (D-044 Part D T6).</summary>
    public required bool HasRepeatableDescriptor { get; init; }
    public bool IsTrunk => Tree == ResearchTree.Technology && Branch < 0;
}

/// <summary>A registry entity with its knowledge requirement (ADR-029 §10).</summary>
public sealed class ResearchEntity
{
    public required int Index { get; init; }
    public required string Id { get; init; }
    public required ResearchEntityKind Kind { get; init; }
    public required string? Name { get; init; }
    /// <summary>Null = no knowledge requirement (valid; ADR-028 §3 — never filled with an invented one).</summary>
    public required Predicate? Requirement { get; init; }
    /// <summary>Corpus references that resolve to nothing, declared in data; they read false.</summary>
    public required IReadOnlyList<string> Unresolved { get; init; }
    /// <summary>Distinct node indices in the requirement, source order.</summary>
    public required IReadOnlyList<int> NodeAtoms { get; init; }
    /// <summary>Distinct institution entity indices in the requirement, source order.</summary>
    public required IReadOnlyList<int> InstitutionAtoms { get; init; }
}

/// <summary>
/// The loaded, validated research content (ADR-029 §2): the ONE authoritative
/// graph (D-044 R16). Technologies and Civics are two trees over one node list;
/// the five subtrees are views of the technology nodes by their Branch. Immutable
/// after load, so one instance may be shared by every consumer of a config.
/// </summary>
public sealed class ResearchContent
{
    public required ResearchTuning Tuning { get; init; }
    public required IReadOnlyList<ResearchBranch> Branches { get; init; }
    public required IReadOnlyList<UniversityType> UniversityTypes { get; init; }
    /// <summary>The university / research institutional stage (D-044 R4) — a
    /// prerequisite-style predicate over nodes (atoms are node indices).</summary>
    public required Predicate Stage { get; init; }
    public required IReadOnlyList<ResearchNode> Nodes { get; init; }
    public required IReadOnlyList<ResearchEntity> Entities { get; init; }
    public required int TechnologyCount { get; init; }
    /// <summary>Quantity id → good (roster order). Eureka conditions name them stock_&lt;good&gt;.</summary>
    public required IReadOnlyList<GoodId> QuantityGoods { get; init; }
    public required string CorpusSha256 { get; init; }

    // Lookup arrays, set by the loader: keys ascending (node order), and ids sorted
    // ordinally with the node index each maps to — binary search, never a dictionary.
    internal ResearchNodeId[] SortedKeys { get; init; } = [];
    internal string[] SortedIds { get; init; } = [];
    internal int[] SortedIdIndex { get; init; } = [];

    public int CivicsCount => Nodes.Count - TechnologyCount;

    /// <summary>Atom id that entity requirements use for a declared-unresolved name; reads false.</summary>
    public int UnresolvedAtom => Nodes.Count + Entities.Count;

    /// <summary>Dense index of a node key, or -1. Binary search over ascending keys (no dictionary).</summary>
    public int IndexOf(ResearchNodeId key)
    {
        int i = Array.BinarySearch(SortedKeys, key);
        return i >= 0 ? i : -1;
    }

    /// <summary>Dense index of a node id, or -1. Ordinal binary search.</summary>
    public int IndexOfId(string id)
    {
        int i = Array.BinarySearch(SortedIds, id, StringComparer.Ordinal);
        return i >= 0 ? SortedIdIndex[i] : -1;
    }

    /// <summary>Index of a university type key, or -1.</summary>
    public int UniversityTypeIndexOf(int key)
    {
        for (int i = 0; i < UniversityTypes.Count; i++) if (UniversityTypes[i].Key == key) return i;
        return -1;
    }

    /// <summary>Index of an entity id, or -1 (ordinal linear scan).</summary>
    public int EntityIndexOf(string id)
    {
        for (int i = 0; i < Entities.Count; i++)
            if (string.Equals(Entities[i].Id, id, StringComparison.Ordinal)) return i;
        return -1;
    }
}

/// <summary>
/// Loads and validates research.json (ADR-029 §2) on the T0.4 template: Sim.Core
/// takes a string/Stream, the JSON binds to [JsonRequired] records, and EVERY rule
/// below fails loudly and first with a message naming the file path and the node.
/// The rules are the ruled validators (D-044 R24) plus the ones the model needs:
/// duplicate ids and keys · tree and subtree ids · missing prerequisites ·
/// prerequisite cycles · Eureka references · costs · unlock references and the
/// reverse-index agreement · orphans · unreachable nodes · cross-tree and
/// cross-subtree dependencies · subtree assignment · stage deadlock · immediate
/// effects (none is ratified) · tuning ranges.
/// </summary>
public static class ResearchContentLoader
{
    public const string Schema = "civ-sim/research@1";

    /// <summary>The five subtree ids D-044 R3 rules, in number order 1.1–1.5.</summary>
    public static readonly string[] RuledBranchIds = ["military", "medicine", "engineering", "natural_science", "agriculture"];

    private static readonly string[] Ages = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"];
    private static readonly string[] EurekaStatusNames = ["evaluable", "no-state-carrier", "institution-state-absent", "contact-state-absent", "implied-by-prerequisites"];
    private static readonly string[] EntityKindNames = ["building", "infrastructure", "institution", "unit", "activity", "project"];
    private static readonly string[] EntityPrefixes = ["building.", "infra.", "inst.", "unit.", "activity.", "project."];
    private static readonly string[] UnlockListNames = ["buildings", "infrastructure", "institutions", "units", "activities", "projects"];

    private static readonly JsonSerializerOptions JsonOptions = new() { RespectNullableAnnotations = true };

    public static ResearchContent Load(Stream json, GoodsConfig? goods)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd(), goods);
    }

    public static ResearchContent Load(string json, GoodsConfig? goods)
    {
        ResearchFileJson? file;
        try
        {
            // RespectNullableAnnotations: an explicit JSON null in a non-nullable member is a
            // JsonException naming its path, never a null smuggled into the model.
            file = JsonSerializer.Deserialize<ResearchFileJson>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            throw new ResearchContentException(
                $"research.json is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (file is null) throw new ResearchContentException("research.json is empty (null document).");
        return Build(file, goods);
    }

    private static ResearchContent Build(ResearchFileJson f, GoodsConfig? goods)
    {
        if (!string.Equals(f.Schema, Schema, StringComparison.Ordinal))
            throw Fail($"schema is '{f.Schema}', expected '{Schema}'.");
        if (f.Source.CorpusSha256.Length != 64 || !IsLowerHex(f.Source.CorpusSha256))
            throw Fail("source.corpusSha256 must be the 64-character lower-case hex SHA-256 of the corpus it was migrated from.");

        ResearchTuning tuning = ValidateTuning(f.Tuning);
        ValidateTrees(f.Trees);
        ResearchBranch[] branches = ValidateBranches(f.Branches);
        UniversityType[] universities = ValidateUniversities(f.UniversityTypes, branches);

        // ---- nodes: identity, keys, ids (technologies then civics — one id space) ----
        int techCount = f.Technologies.Length;
        var raw = new (NodeJson Json, ResearchTree Tree, string Path)[techCount + f.Civics.Length];
        for (int i = 0; i < techCount; i++) raw[i] = (f.Technologies[i], ResearchTree.Technology, $"technologies[{i}]");
        for (int i = 0; i < f.Civics.Length; i++) raw[techCount + i] = (f.Civics[i], ResearchTree.Civics, $"civics[{i}]");
        int n = raw.Length;
        if (techCount == 0) throw Fail("technologies is empty; the Technology tree needs nodes.");
        if (f.Civics.Length == 0) throw Fail("civics is empty; the Civics tree needs nodes (D-044 R12).");

        var keys = new ResearchNodeId[n];
        for (int i = 0; i < n; i++)
        {
            (NodeJson j, _, string path) = raw[i];
            if (j.Key < 1) throw Fail($"{path} ({j.Id}): key {j.Key} must be >= 1.");
            if (i > 0 && j.Key <= keys[i - 1].Value)
                throw Fail($"{path} ({j.Id}): key {j.Key} is not strictly greater than the previous key " +
                           $"{keys[i - 1].Value} — keys must be unique and ascending across technologies then civics.");
            keys[i] = new ResearchNodeId(j.Key);
            if (!IsNodeId(j.Id))
                throw Fail($"{path}: id '{j.Id}' must match [a-z][a-z0-9_]* (atoms of the prerequisite language).");
            if (j.Id is "AND" or "OR" or "NOT")
                throw Fail($"{path}: id '{j.Id}' is a reserved keyword.");
            if (string.IsNullOrWhiteSpace(j.Name)) throw Fail($"{path} ({j.Id}): name is empty.");
            if (Array.IndexOf(Ages, j.Age) < 0)
                throw Fail($"{path} ({j.Id}): age '{j.Age}' is not one of the nine Ages A1..A9 " +
                           "(age F is normalized to A9 with frontier = true; architecture §17.5).");
            if (!(double.IsFinite(j.Cost) && j.Cost > 0.0))
                throw Fail($"{path} ({j.Id}): cost {Inv(j.Cost)} is invalid — a research cost must be finite and > 0 CLP.");
            if (j.Effects.Immediate.Length != 0)
                throw Fail($"{path} ({j.Id}): effects.immediate is not empty. No immediate-effect kind is ratified, and a " +
                           "free-floating effect would be a permanent modifier (law 2); completion grants eligibility only.");
        }
        string[] ids = new string[n];
        for (int i = 0; i < n; i++) ids[i] = raw[i].Json.Id;
        int[] idOrder = new int[n];
        for (int i = 0; i < n; i++) idOrder[i] = i;
        string[] sortedIds = (string[])ids.Clone();
        Array.Sort(sortedIds, idOrder, StringComparer.Ordinal);
        for (int i = 1; i < n; i++)
            if (string.Equals(sortedIds[i], sortedIds[i - 1], StringComparison.Ordinal))
                throw Fail($"duplicate node id '{sortedIds[i]}' ({raw[idOrder[i - 1]].Path} and {raw[idOrder[i]].Path}) — " +
                           "one authoritative graph (D-044 R16): a node exists once across both trees.");

        int IdLookup(string name)
        {
            int k = Array.BinarySearch(sortedIds, name, StringComparer.Ordinal);
            return k >= 0 ? idOrder[k] : -1;
        }

        // ---- subtree assignment (technologies carry a branch or null; civics none) ----
        int[] branchOf = new int[n];
        for (int i = 0; i < n; i++)
        {
            (NodeJson j, ResearchTree tree, string path) = raw[i];
            if (tree == ResearchTree.Civics)
            {
                if (j.Branch.ValueKind != JsonValueKind.Undefined)
                    throw Fail($"{path} ({j.Id}): a Civics node carries no 'branch' — Tree 2 has no numbered subtrees (D-044 R3).");
                branchOf[i] = -1;
                continue;
            }
            if (j.Branch.ValueKind == JsonValueKind.Null) { branchOf[i] = -1; continue; }
            if (j.Branch.ValueKind != JsonValueKind.String)
                throw Fail($"{path} ({j.Id}): 'branch' is required on a technology — null for the Main Technology Tree " +
                           $"or one of {string.Join(", ", RuledBranchIds)}.");
            string b = j.Branch.GetString()!;
            int bi = -1;
            for (int k = 0; k < branches.Length; k++) if (string.Equals(branches[k].Id, b, StringComparison.Ordinal)) bi = k;
            if (bi < 0)
                throw Fail($"{path} ({j.Id}): branch '{b}' is not a subtree id; the five subtrees are " +
                           $"{string.Join(", ", RuledBranchIds)} (D-044 R3), and null is the Main Technology Tree.");
            branchOf[i] = bi;
        }

        // ---- prerequisites: parse, missing references, self references ----
        var prereqSymbols = new PredicateSymbols(
            name => IdLookup(name), _ => -1, "a Technology or Civics node id");
        var prereq = new Predicate?[n];
        var prereqNodes = new int[n][];
        for (int i = 0; i < n; i++)
        {
            (NodeJson j, _, string path) = raw[i];
            if (j.Prereq is null) { prereqNodes[i] = []; continue; }
            try { prereq[i] = Predicate.Parse(j.Prereq, prereqSymbols); }
            catch (PredicateFormatException e)
            {
                throw Fail($"{path} ({j.Id}).prereq: missing or malformed prerequisite — {e.Message}", e);
            }
            if (prereq[i]!.QuantityIds.Count != 0 || prereq[i]!.ReadsVariables)
                throw Fail($"{path} ({j.Id}).prereq: a prerequisite is an expression over node ids only; it may not compare variables.");
            if (prereq[i]!.UsesNot)
                throw Fail($"{path} ({j.Id}).prereq: NOT is not allowed in a prerequisite — prerequisites are AND / OR / nested " +
                           "(D-044 R8), so completing knowledge can never make a node LESS available.");
            prereqNodes[i] = [.. prereq[i]!.AtomIds];
            if (Array.IndexOf(prereqNodes[i], i) >= 0)
                throw Fail($"{path} ({j.Id}).prereq: the node requires itself — a prerequisite cycle of length one.");
        }

        // ---- acyclicity (iterative three-colour DFS in index order; names the cycle) ----
        int[] topo = TopologicalOrder(n, prereqNodes, ids);

        // ---- depth (longest prerequisite path; union over OR) must match the data ----
        int[] depth = new int[n];
        foreach (int i in topo)
        {
            int d = 0;
            foreach (int p in prereqNodes[i]) d = Math.Max(d, depth[p] + 1);
            depth[i] = d;
            if (raw[i].Json.Depth != d)
                throw Fail($"{raw[i].Path} ({ids[i]}): depth {raw[i].Json.Depth} does not match the longest prerequisite " +
                           $"path {d} — regenerate with scripts/migrate-research-corpus.py.");
        }

        // ---- dependents (derived reverse index, index order) ----
        var depLists = new List<int>[n];
        for (int i = 0; i < n; i++) depLists[i] = [];
        for (int i = 0; i < n; i++) foreach (int p in prereqNodes[i]) depLists[p].Add(i);

        // ---- subtree assignment: no trunk technology requires subtree knowledge ----
        for (int i = 0; i < techCount; i++)
        {
            if (branchOf[i] >= 0) continue;
            foreach (int p in prereqNodes[i])
                if (branchOf[p] >= 0)
                    throw Fail($"{raw[i].Path} ({ids[i]}): invalid subtree assignment — this Main-tree technology requires " +
                               $"'{ids[p]}' of the {branches[branchOf[p]].Name} subtree. Main-tree knowledge must never sit " +
                               "behind the research stage (architecture §8.4.5); move one of them.");
        }

        // ---- the research stage ----
        Predicate stage;
        try { stage = Predicate.Parse(f.ResearchStage.Requires, prereqSymbols); }
        catch (PredicateFormatException e) { throw Fail($"researchStage.requires: {e.Message}", e); }
        if (stage.QuantityIds.Count != 0 || stage.ReadsVariables || stage.UsesNot)
            throw Fail("researchStage.requires is an AND / OR expression over node ids only.");

        // ---- entities (knowledge requirements; institutions may reference institutions) ----
        ResearchEntity[] entities = BuildEntities(f.Entities, n, IdLookup);

        // ---- unlock lists: references exist, kinds match, reverse-index agreement ----
        var unlocked = new int[n][];
        for (int i = 0; i < n; i++)
            unlocked[i] = ValidateUnlocks(raw[i].Json, raw[i].Tree, raw[i].Path, i, entities);

        // ---- Eurekas ----
        GoodId[] quantityGoods = goods is null ? [] : new GoodId[goods.Goods.Length];
        string[] quantityNames = goods is null ? [] : new string[goods.Goods.Length];
        if (goods is not null)
            for (int g = 0; g < goods.Goods.Length; g++)
            {
                quantityGoods[g] = new GoodId(goods.Goods[g].Id);
                quantityNames[g] = "stock_" + goods.Goods[g].Name.Replace('-', '_');
            }
        var eurekaSymbols = new PredicateSymbols(
            name => IdLookup(name),
            name =>
            {
                for (int g = 0; g < quantityNames.Length; g++)
                    if (string.Equals(quantityNames[g], name, StringComparison.Ordinal)) return g;
                return -1;
            },
            goods is null
                ? "a node id (no goods registry is attached, so stock_<good> quantities are unavailable)"
                : $"a node id, or a quantity ({string.Join(", ", quantityNames)})");
        var eurekas = new ResearchEureka[n][];
        for (int i = 0; i < n; i++)
        {
            (NodeJson j, _, string path) = raw[i];
            var list = new ResearchEureka[j.Eurekas.Length];
            for (int e = 0; e < j.Eurekas.Length; e++)
            {
                EurekaJson ej = j.Eurekas[e];
                string ep = $"{path} ({j.Id}).eurekas[{e}]";
                int statusIdx = Array.IndexOf(EurekaStatusNames, ej.Status);
                if (statusIdx < 0)
                    throw Fail($"{ep}: status '{ej.Status}' is not one of {string.Join(", ", EurekaStatusNames)}.");
                var status = (EurekaStatus)(statusIdx + 1);
                if (string.IsNullOrWhiteSpace(ej.Text)) throw Fail($"{ep}: text is empty.");
                if ((ej.When is null) != (status != EurekaStatus.Evaluable))
                    throw Fail($"{ep}: 'when' must be present exactly when status is 'evaluable' (got status '{ej.Status}').");
                Predicate? cond = null;
                if (ej.When is not null)
                {
                    try { cond = Predicate.Parse(ej.When, eurekaSymbols); }
                    catch (PredicateFormatException ex)
                    {
                        throw Fail($"{ep}: invalid Eureka reference — {ex.Message}", ex);
                    }
                }
                list[e] = new ResearchEureka(e, ej.Text, status, cond);
            }
            eurekas[i] = list;
        }

        // ---- orphans: a node whose completion would change nothing ----
        for (int i = 0; i < n; i++)
        {
            NodeJson j = raw[i].Json;
            if (depLists[i].Count == 0 && unlocked[i].Length == 0 && j.Unlocks.Capabilities.Length == 0)
                throw Fail($"{raw[i].Path} ({j.Id}): orphan node — nothing requires it, it unlocks no entity and declares " +
                           "no capability, so completing it would change nothing.");
            foreach (string c in j.Unlocks.Capabilities)
                if (string.IsNullOrWhiteSpace(c)) throw Fail($"{raw[i].Path} ({j.Id}).unlocks.capabilities has an empty entry.");
        }

        // ---- reachability: before the stage (Main tree + Civics), then with the stage open ----
        bool[] reached = new bool[n];
        Reach(n, techCount, branchOf, prereq, reached, stageOpen: false);
        for (int i = 0; i < techCount; i++)
            if (branchOf[i] < 0 && !reached[i])
                throw Fail($"{raw[i].Path} ({ids[i]}): this Main-tree technology cannot be researched before the research " +
                           "stage — its prerequisites (through Civics) reach subtree knowledge. Invalid cross-tree dependency.");
        if (!stage.Evaluate(null, a => reached[a], null))
            throw Fail("researchStage.requires can never be satisfied by knowledge reachable before the stage — it waits on " +
                       "subtree knowledge that only the stage can open (a deadlock), so the five subtrees could never open (D-044 R4).");
        // With monotone (NOT-free) prerequisites over an acyclic graph whose every
        // reference exists, the checks above already imply this one. It is kept as the
        // stated invariant: every node is reachable by SOME sequence of completions.
        Reach(n, techCount, branchOf, prereq, reached, stageOpen: true);
        for (int i = 0; i < n; i++)
            if (!reached[i])
                throw Fail($"{raw[i].Path} ({ids[i]}): unreachable — no sequence of completions satisfies its prerequisites.");

        // ---- dead Eurekas: a condition that cannot hold while its node is still researchable ----
        // A Eureka is evaluated only while its node is AVAILABLE (not complete). Everything
        // that can be completed WITHOUT the node is what "reached without i" computes: the
        // same two-pass fixpoint, with i held incomplete and the stage opening only if it can
        // without i. A NOT-free condition is dead when it cannot be true even with every
        // comparison taken as true (Predicate.CanHold). An OR alternative that needs the node
        // does not kill the condition when another alternative can hold, and a condition
        // naming subtree knowledge on a node the stage itself needs is dead.
        for (int i = 0; i < n; i++)
        {
            bool[]? without = null;
            foreach (ResearchEureka eu in eurekas[i])
            {
                if (eu.Condition is not { } cond || cond.AtomIds.Count == 0) continue;
                if (without is null)
                {
                    without = new bool[n];
                    Reach(n, techCount, branchOf, prereq, without, stageOpen: false, excluded: i);
                    if (stage.Evaluate(null, a => without[a], null))
                        Reach(n, techCount, branchOf, prereq, without, stageOpen: true, excluded: i);
                }
                bool[] w = without;
                if (!cond.CanHold(a => w[a]))
                    throw Fail($"{raw[i].Path} ({ids[i]}).eurekas[{eu.Index}]: invalid Eureka reference — the condition " +
                               $"'{cond.Source}' cannot hold while '{ids[i]}' is still researchable: the knowledge it names can " +
                               $"only be completed after '{ids[i]}' itself (a dead Eureka).");
            }
        }

        // ---- assemble ----
        var nodes = new ResearchNode[n];
        for (int i = 0; i < n; i++)
        {
            (NodeJson j, ResearchTree tree, _) = raw[i];
            nodes[i] = new ResearchNode
            {
                Index = i,
                Key = keys[i],
                Id = j.Id,
                Name = j.Name,
                Description = j.Desc,
                Age = j.Age,
                Frontier = j.Frontier,
                Emerged = j.Emerged,
                Tree = tree,
                Branch = branchOf[i],
                Domain = j.Domain,
                SecondaryDomains = j.SecondaryDomains,
                Depth = depth[i],
                BaseCost = j.Cost,
                Prerequisite = prereq[i],
                PrerequisiteNodes = prereqNodes[i],
                Eurekas = eurekas[i],
                Capabilities = j.Unlocks.Capabilities,
                Techniques = j.Unlocks.Techniques,
                Applications = j.Unlocks.Applications,
                UnlockedEntities = unlocked[i],
                Dependents = depLists[i].ToArray(),
                Family = j.Family,
                Generation = j.Generation,
                HasRepeatableDescriptor = j.Repeatable.ValueKind == JsonValueKind.Object,
            };
        }
        return new ResearchContent
        {
            Tuning = tuning,
            Branches = branches,
            UniversityTypes = universities,
            Stage = stage,
            Nodes = nodes,
            Entities = entities,
            TechnologyCount = techCount,
            QuantityGoods = quantityGoods,
            CorpusSha256 = f.Source.CorpusSha256,
            SortedKeys = keys,
            SortedIds = sortedIds,
            SortedIdIndex = idOrder,
        };
    }

    // ------------------------------------------------------------------ pieces

    private static ResearchTuning ValidateTuning(TuningJson t)
    {
        if (!(double.IsFinite(t.ClpCoefficient) && t.ClpCoefficient > 0.0))
            throw Fail($"tuning.clpCoefficient {Inv(t.ClpCoefficient)} must be finite and > 0.");
        if (!(double.IsFinite(t.ClpAdultExponent) && t.ClpAdultExponent > 0.0 && t.ClpAdultExponent < 1.0))
            throw Fail($"tuning.clpAdultExponent {Inv(t.ClpAdultExponent)} must be in (0, 1): population is an input with " +
                       "diminishing marginal contribution, and 'CLP = population × constant' is forbidden (architecture §8.1.2).");
        if (!(double.IsFinite(t.EurekaCreditFraction) && t.EurekaCreditFraction > 0.0 && t.EurekaCreditFraction <= 1.0))
            throw Fail($"tuning.eurekaCreditFraction {Inv(t.EurekaCreditFraction)} must be in (0, 1].");
        return new ResearchTuning(t.ClpCoefficient, t.ClpAdultExponent, t.EurekaCreditFraction);
    }

    private static void ValidateTrees(TreeJson[] trees)
    {
        if (trees.Length != 2
            || !string.Equals(trees[0].Id, "technology", StringComparison.Ordinal)
            || !string.Equals(trees[1].Id, "civics", StringComparison.Ordinal))
        {
            string got = string.Join(", ", Array.ConvertAll(trees, t => t.Id));
            throw Fail($"trees must be exactly [technology, civics] — two top-level trees and no others (D-044 R3); got [{got}].");
        }
    }

    private static ResearchBranch[] ValidateBranches(BranchJson[] branches)
    {
        if (branches.Length != RuledBranchIds.Length)
            throw Fail($"branches has {branches.Length} entries; Tree 1 has exactly five subtrees (D-044 R3).");
        var result = new ResearchBranch[branches.Length];
        for (int i = 0; i < branches.Length; i++)
        {
            BranchJson b = branches[i];
            if (!string.Equals(b.Id, RuledBranchIds[i], StringComparison.Ordinal))
                throw Fail($"branches[{i}]: id '{b.Id}' is invalid; the subtrees are, in order, " +
                           $"{string.Join(", ", RuledBranchIds)} (D-044 R3 — no Industry & Energy, Naval or Science subtree).");
            if (i > 0 && b.Key <= result[i - 1].Key)
                throw Fail($"branches[{i}] ({b.Id}): key {b.Key} must be strictly ascending.");
            string number = $"1.{i + 1}";
            if (!string.Equals(b.Number, number, StringComparison.Ordinal))
                throw Fail($"branches[{i}] ({b.Id}): number '{b.Number}' must be '{number}'.");
            if (string.IsNullOrWhiteSpace(b.Name)) throw Fail($"branches[{i}] ({b.Id}): name is empty.");
            result[i] = new ResearchBranch(i, b.Key, b.Id, b.Number, b.Name);
        }
        return result;
    }

    private static UniversityType[] ValidateUniversities(UniversityTypeJson[] types, ResearchBranch[] branches)
    {
        var result = new UniversityType[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            UniversityTypeJson u = types[i];
            if (u.Key < 1 || (i > 0 && u.Key <= result[i - 1].Key))
                throw Fail($"universityTypes[{i}] ({u.Id}): key {u.Key} must be >= 1 and strictly ascending.");
            for (int k = 0; k < i; k++)
                if (string.Equals(result[k].Id, u.Id, StringComparison.Ordinal))
                    throw Fail($"universityTypes[{i}]: duplicate id '{u.Id}'.");
            int bi = -1;
            for (int k = 0; k < branches.Length; k++) if (string.Equals(branches[k].Id, u.Branch, StringComparison.Ordinal)) bi = k;
            if (bi < 0)
                throw Fail($"universityTypes[{i}] ({u.Id}): branch '{u.Branch}' is not a subtree id — a specialized university " +
                           "reduces the cost of one subtree's nodes (D-044 R5).");
            result[i] = new UniversityType(u.Key, u.Id, u.Name, bi);
        }
        return result;
    }

    private static ResearchEntity[] BuildEntities(EntityJson[] json, int nodeCount, Func<string, int> nodeLookup)
    {
        int m = json.Length;
        var kinds = new ResearchEntityKind[m];
        for (int i = 0; i < m; i++)
        {
            EntityJson e = json[i];
            int k = Array.IndexOf(EntityKindNames, e.Kind);
            if (k < 0) throw Fail($"entities[{i}] ({e.Id}): kind '{e.Kind}' is not one of {string.Join(", ", EntityKindNames)}.");
            if (!e.Id.StartsWith(EntityPrefixes[k], StringComparison.Ordinal) || e.Id.Length == EntityPrefixes[k].Length)
                throw Fail($"entities[{i}]: id '{e.Id}' of kind {e.Kind} must start with '{EntityPrefixes[k]}'.");
            for (int p = 0; p < i; p++)
                if (string.Equals(json[p].Id, e.Id, StringComparison.Ordinal))
                    throw Fail($"entities[{i}]: duplicate entity id '{e.Id}' (also entities[{p}]).");
            kinds[i] = (ResearchEntityKind)(k + 1);
        }

        int InstitutionLookup(string shortId)
        {
            for (int i = 0; i < m; i++)
                if (kinds[i] == ResearchEntityKind.Institution
                    && json[i].Id.Length == 5 + shortId.Length
                    && string.CompareOrdinal(json[i].Id, 5, shortId, 0, shortId.Length) == 0)
                    return i;
            return -1;
        }

        var result = new ResearchEntity[m];
        var instAtoms = new int[m][];
        for (int i = 0; i < m; i++)
        {
            EntityJson e = json[i];
            string path = $"entities[{i}] ({e.Id})";
            string[] declared = e.Unresolved;
            Predicate? req = null;
            var nodeAtoms = new List<int>();
            var insts = new List<int>();
            var hitUnresolved = new List<string>();
            if (e.Requires is null)
            {
                if (declared.Length != 0) throw Fail($"{path}: declares unresolved references but has no requirement.");
            }
            else
            {
                int sentinel = nodeCount + m;
                var symbols = new PredicateSymbols(
                    name =>
                    {
                        int nd = nodeLookup(name);
                        if (nd >= 0) return nd;
                        int inst = InstitutionLookup(name);
                        if (inst >= 0) return nodeCount + inst;
                        if (Array.IndexOf(declared, name) >= 0)
                        {
                            if (!hitUnresolved.Contains(name)) hitUnresolved.Add(name);
                            return sentinel;
                        }
                        return -1;
                    },
                    _ => -1,
                    "a Technology or Civics node id, an institution short id, or a name declared in 'unresolved'");
                try { req = Predicate.Parse(e.Requires, symbols); }
                catch (PredicateFormatException ex)
                {
                    throw Fail($"{path}.requires: invalid reference — {ex.Message} (declare a corpus reference that resolves " +
                               "to nothing in 'unresolved'; never drop it silently).", ex);
                }
                if (req.QuantityIds.Count != 0 || req.ReadsVariables)
                    throw Fail($"{path}.requires is an expression over node and institution ids only.");
                foreach (int a in req.AtomIds)
                {
                    if (a < nodeCount) nodeAtoms.Add(a);
                    else if (a < sentinel)
                    {
                        if (a - nodeCount == i) throw Fail($"{path}.requires names the institution itself.");
                        insts.Add(a - nodeCount);
                    }
                }
                foreach (string d in declared)
                    if (!hitUnresolved.Contains(d))
                        throw Fail($"{path}: '{d}' is declared unresolved but the requirement does not use it, or it " +
                                   "resolves after all — keep the unresolved list exact.");
            }
            instAtoms[i] = [.. insts];
            result[i] = new ResearchEntity
            {
                Index = i, Id = e.Id, Kind = kinds[i], Name = e.Name, Requirement = req,
                Unresolved = declared, NodeAtoms = [.. nodeAtoms], InstitutionAtoms = [.. insts],
            };
        }

        // Institution references must be acyclic (iterative DFS, index order).
        var colour = new int[m];
        for (int root = 0; root < m; root++)
        {
            if (colour[root] != 0) continue;
            var stack = new Stack<(int Node, int Next)>();
            stack.Push((root, 0));
            colour[root] = 1;
            while (stack.Count > 0)
            {
                (int node, int next) = stack.Pop();
                if (next < instAtoms[node].Length)
                {
                    stack.Push((node, next + 1));
                    int child = instAtoms[node][next];
                    if (colour[child] == 1)
                        throw Fail($"entities: institution requirement cycle through '{json[child].Id}' and '{json[node].Id}'.");
                    if (colour[child] == 0) { colour[child] = 1; stack.Push((child, 0)); }
                }
                else colour[node] = 2;
            }
        }
        return result;
    }

    private static int[] ValidateUnlocks(NodeJson j, ResearchTree tree, string path, int nodeIndex, ResearchEntity[] entities)
    {
        string[][] lists =
        [
            j.Unlocks.Buildings, j.Unlocks.Infrastructure, j.Unlocks.Institutions,
            j.Unlocks.Units, j.Unlocks.Activities, j.Unlocks.Projects,
        ];
        var listed = new List<int>();
        for (int k = 0; k < lists.Length; k++)
        {
            var kind = (ResearchEntityKind)(k + 1);
            foreach (string id in lists[k])
            {
                int e = -1;
                for (int x = 0; x < entities.Length; x++)
                    if (string.Equals(entities[x].Id, id, StringComparison.Ordinal)) { e = x; break; }
                if (e < 0)
                    throw Fail($"{path} ({j.Id}).unlocks.{UnlockListNames[k]}: '{id}' is not a registry entity — invalid unlock reference.");
                if (entities[e].Kind != kind)
                    throw Fail($"{path} ({j.Id}).unlocks.{UnlockListNames[k]}: '{id}' is a {entities[e].Kind}, not a {kind}.");
                if (listed.Contains(e))
                    throw Fail($"{path} ({j.Id}).unlocks: '{id}' is listed twice.");
                if (tree == ResearchTree.Civics && kind != ResearchEntityKind.Institution)
                    throw Fail($"{path} ({j.Id}).unlocks.{UnlockListNames[k]}: a Civics node makes institutional forms " +
                               "eligible (architecture §5.4); it cannot unlock a " + kind + ".");
                listed.Add(e);
            }
        }
        // Reverse-index agreement (registry authority rule): listed ⇔ the entity's requirement names this node.
        for (int e = 0; e < entities.Length; e++)
        {
            bool named = false;
            foreach (int a in entities[e].NodeAtoms) if (a == nodeIndex) { named = true; break; }
            if (named != listed.Contains(e))
                throw Fail($"{path} ({j.Id}).unlocks: '{entities[e].Id}' " +
                           (named ? "requires this node but is not listed" : "is listed but its requirement does not name this node") +
                           " — the unlock lists are the reverse index of the entity requirements, which are authoritative.");
        }
        listed.Sort();
        return [.. listed];
    }

    /// <summary>Iterative three-colour DFS in index order; throws naming the cycle.</summary>
    private static int[] TopologicalOrder(int n, int[][] prereqNodes, string[] ids)
    {
        var colour = new int[n];
        var order = new List<int>(n);
        var path = new List<int>();
        for (int root = 0; root < n; root++)
        {
            if (colour[root] != 0) continue;
            var stack = new Stack<(int Node, int Next)>();
            stack.Push((root, 0));
            colour[root] = 1;
            path.Add(root);
            while (stack.Count > 0)
            {
                (int node, int next) = stack.Pop();
                if (next < prereqNodes[node].Length)
                {
                    stack.Push((node, next + 1));
                    int child = prereqNodes[node][next];
                    if (colour[child] == 1)
                    {
                        int from = path.IndexOf(child);
                        var names = new List<string>();
                        for (int k = from; k < path.Count; k++) names.Add(ids[path[k]]);
                        names.Add(ids[child]);
                        throw Fail($"prerequisite cycle: {string.Join(" requires ", names)} — prerequisite edges must be acyclic (D-044 R8).");
                    }
                    if (colour[child] == 0)
                    {
                        colour[child] = 1;
                        path.Add(child);
                        stack.Push((child, 0));
                    }
                }
                else
                {
                    colour[node] = 2;
                    path.RemoveAt(path.Count - 1);
                    order.Add(node); // post-order: every prerequisite precedes its dependents
                }
            }
        }
        return [.. order];
    }

    private static IEnumerable<int> Closure(IReadOnlyList<int> roots, IReadOnlyList<IReadOnlyCollection<int>> edges, int n)
    {
        var seen = new bool[n];
        var stack = new Stack<int>();
        foreach (int r in roots) stack.Push(r);
        var result = new List<int>();
        while (stack.Count > 0)
        {
            int x = stack.Pop();
            if (seen[x]) continue;
            seen[x] = true;
            result.Add(x);
            foreach (int y in edges[x]) stack.Push(y);
        }
        result.Sort();
        return result;
    }

    private static IEnumerable<int> Closure(IReadOnlyList<int> roots, List<int>[] edges, int n) =>
        Closure(roots, Array.ConvertAll(edges, e => (IReadOnlyCollection<int>)e), n);

    /// <summary>Fixpoint of "researchable given what is reached", in index order.</summary>
    private static void Reach(int n, int techCount, int[] branchOf, Predicate?[] prereq, bool[] reached, bool stageOpen,
        int excluded = -1)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < n; i++)
            {
                if (reached[i] || i == excluded) continue;
                if (!stageOpen && i < techCount && branchOf[i] >= 0) continue;
                if (prereq[i] is null || prereq[i]!.Evaluate(null, a => reached[a], null))
                {
                    reached[i] = true;
                    changed = true;
                }
            }
        }
    }

    private static bool IsNodeId(string id)
    {
        if (string.IsNullOrEmpty(id) || !(id[0] >= 'a' && id[0] <= 'z')) return false;
        foreach (char c in id)
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
        return true;
    }

    private static bool IsLowerHex(string s)
    {
        foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }

    private static ResearchContentException Fail(string message, Exception? inner = null) =>
        new($"research.json: {message}", inner);

    private static string Inv(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ JSON shape

    private sealed record ResearchFileJson(
        [property: JsonPropertyName("schema"), JsonRequired] string Schema,
        [property: JsonPropertyName("source"), JsonRequired] SourceJson Source,
        [property: JsonPropertyName("tuning"), JsonRequired] TuningJson Tuning,
        [property: JsonPropertyName("trees"), JsonRequired] TreeJson[] Trees,
        [property: JsonPropertyName("branches"), JsonRequired] BranchJson[] Branches,
        [property: JsonPropertyName("researchStage"), JsonRequired] StageJson ResearchStage,
        [property: JsonPropertyName("universityTypes"), JsonRequired] UniversityTypeJson[] UniversityTypes,
        [property: JsonPropertyName("technologies"), JsonRequired] NodeJson[] Technologies,
        [property: JsonPropertyName("civics"), JsonRequired] NodeJson[] Civics,
        [property: JsonPropertyName("entities"), JsonRequired] EntityJson[] Entities);

    private sealed record SourceJson(
        [property: JsonPropertyName("corpus"), JsonRequired] string Corpus,
        [property: JsonPropertyName("corpusVersion"), JsonRequired] string CorpusVersion,
        [property: JsonPropertyName("corpusSha256"), JsonRequired] string CorpusSha256,
        [property: JsonPropertyName("generator"), JsonRequired] string Generator);

    private sealed record TuningJson(
        [property: JsonPropertyName("clpCoefficient"), JsonRequired] double ClpCoefficient,
        [property: JsonPropertyName("clpAdultExponent"), JsonRequired] double ClpAdultExponent,
        [property: JsonPropertyName("eurekaCreditFraction"), JsonRequired] double EurekaCreditFraction);

    private sealed record TreeJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("number"), JsonRequired] string Number,
        [property: JsonPropertyName("name"), JsonRequired] string Name);

    private sealed record BranchJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("number"), JsonRequired] string Number,
        [property: JsonPropertyName("name"), JsonRequired] string Name);

    private sealed record StageJson(
        [property: JsonPropertyName("requires"), JsonRequired] string Requires);

    private sealed record UniversityTypeJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("branch"), JsonRequired] string Branch);

    private sealed record NodeJson(
        [property: JsonPropertyName("key"), JsonRequired] int Key,
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("desc"), JsonRequired] string Desc,
        [property: JsonPropertyName("age"), JsonRequired] string Age,
        [property: JsonPropertyName("frontier"), JsonRequired] bool Frontier,
        [property: JsonPropertyName("emerged"), JsonRequired] string? Emerged,
        [property: JsonPropertyName("domain"), JsonRequired] string Domain,
        [property: JsonPropertyName("secondaryDomains"), JsonRequired] string[] SecondaryDomains,
        [property: JsonPropertyName("depth"), JsonRequired] int Depth,
        [property: JsonPropertyName("cost"), JsonRequired] double Cost,
        [property: JsonPropertyName("prereq"), JsonRequired] string? Prereq,
        [property: JsonPropertyName("eurekas"), JsonRequired] EurekaJson[] Eurekas,
        [property: JsonPropertyName("unlocks"), JsonRequired] UnlocksJson Unlocks,
        [property: JsonPropertyName("family"), JsonRequired] string? Family,
        [property: JsonPropertyName("generation"), JsonRequired] int? Generation,
        [property: JsonPropertyName("effects"), JsonRequired] EffectsJson Effects,
        [property: JsonPropertyName("repeatable"), JsonRequired] JsonElement Repeatable,
        // Present (string or null) on technologies, ABSENT on civics — a JsonElement
        // so "missing" (Undefined) and "null" stay distinguishable.
        [property: JsonPropertyName("branch")] JsonElement Branch = default);

    private sealed record EurekaJson(
        [property: JsonPropertyName("text"), JsonRequired] string Text,
        [property: JsonPropertyName("when"), JsonRequired] string? When,
        [property: JsonPropertyName("status"), JsonRequired] string Status);

    private sealed record UnlocksJson(
        [property: JsonPropertyName("capabilities"), JsonRequired] string[] Capabilities,
        [property: JsonPropertyName("units"), JsonRequired] string[] Units,
        [property: JsonPropertyName("buildings"), JsonRequired] string[] Buildings,
        [property: JsonPropertyName("institutions"), JsonRequired] string[] Institutions,
        [property: JsonPropertyName("infrastructure"), JsonRequired] string[] Infrastructure,
        [property: JsonPropertyName("activities"), JsonRequired] string[] Activities,
        [property: JsonPropertyName("projects"), JsonRequired] string[] Projects,
        [property: JsonPropertyName("techniques"), JsonRequired] string[] Techniques,
        [property: JsonPropertyName("applications"), JsonRequired] string[] Applications);

    private sealed record EffectsJson(
        [property: JsonPropertyName("immediate"), JsonRequired] JsonElement[] Immediate);

    private sealed record EntityJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("kind"), JsonRequired] string Kind,
        [property: JsonPropertyName("name"), JsonRequired] string? Name,
        [property: JsonPropertyName("requires"), JsonRequired] string? Requires,
        [property: JsonPropertyName("unresolved"), JsonRequired] string[] Unresolved);
}
