using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Core.Systems.Ages;

/// <summary>Raised on any unit-progression.json violation, with an actionable message naming the rule.</summary>
public sealed class UnitProgressionContentException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// M6.8 — THE M7 CONTRACT VOCABULARY the unit-progression content may name (docs/m6-plan/unit-progression-contract.md §3 and
/// docs/m6-plan/training-ap-contract.md §4). The fields are D-011 §3's formation state (strength, cohesion, fatigue,
/// ammunition, facing, frontage); each TERM names the coefficient of one M7 resolution equation a promotion may scale (Law 2:
/// a coefficient inside a named equation, never a free-floating bonus). None of these fields exists in this build: the
/// contract is what M7 must create before any promotion can have an effect (D-E option 1). A test pins this vocabulary to
/// the contract document's table, so the two cannot drift.
/// </summary>
public static class UnitProgressionContract
{
    /// <summary>D-011 §3's formation fields, in the order the contract lists them.</summary>
    public static readonly IReadOnlyList<string> Fields = ["strength", "cohesion", "fatigue", "ammunition", "facing", "frontage"];

    /// <summary>Every (field, term) pair a promotion effect may scale, in contract order.</summary>
    public static readonly IReadOnlyList<(string Field, string Term)> Terms =
    [
        ("strength", "losses_taken"), ("strength", "melee_effect"), ("strength", "fire_effect"),
        ("cohesion", "loss_rate"), ("cohesion", "recovery_rate"), ("cohesion", "rout_threshold"),
        ("fatigue", "accrual_rate"), ("fatigue", "recovery_rate"),
        ("ammunition", "capacity"), ("ammunition", "expenditure_rate"),
        ("facing", "turn_time"),
        ("frontage", "reform_time"),
    ];

    /// <summary>Names a promotion may NOT target, with the reason the loader states (contract §3.2).</summary>
    public static readonly IReadOnlyList<(string Name, string Reason)> Refused =
    [
        ("action_capacity", "maximum Action Capacity is raised only by explicit training (training-ap-contract.md; H7-02), never by a promotion"),
        ("experience", "experience is the EARNING input (MilitaryUnitRow.Experience), never a promotion target"),
        ("personnel", "a formation's people are a conserved stock moved only by Ledger.Transfer (Law 1)"),
        ("movement", "the movement budget is M7 movement content (unit charter §6.2), not a formation field of D-011 §3"),
    ];

    /// <summary>The lower and upper bound of a promotion's COMPOUNDED coefficient, scale^maxTimes (contract §3.3).</summary>
    public const double MinCompound = 0.5, MaxCompound = 2.0;

    /// <summary>The largest repeatCap the file may declare.</summary>
    public const int MaxRepeatCap = 10;

    /// <summary>The M7 action state that DEFINES a dormant carrier (training-ap-contract.md §2).</summary>
    public const string DormantState = "idle";

    /// <summary>The one training mode the contract admits: an explicit, ordered action (training-ap-contract.md §3).</summary>
    public const string ExplicitMode = "explicit";

    /// <summary>The one diminishing-returns rule (training-ap-contract.md §6): the k-th training lasts years × factor^(k−1).</summary>
    public const string EscalatingYears = "escalating_years";

    /// <summary>The bounds of the escalation factor.</summary>
    public const double MinFactor = 1.0, MaxFactor = 4.0;

    /// <summary>The largest cap (trainings per carrier) a profile may declare.</summary>
    public const int MaxTrainingCap = 10;

    /// <summary>The eligibility conditions a training profile may name (training-ap-contract.md §5), in contract order.</summary>
    public static readonly IReadOnlyList<string> TrainingConditions =
    [
        "owned", "stationed_at_controlled_settlement", "not_reequipping", "not_engaged", "supplied",
        "action_capacity_available", "below_cap",
    ];

    /// <summary>Conditions every profile must name: the cost and the cap always apply.</summary>
    public static readonly IReadOnlyList<string> MandatoryTrainingConditions = ["action_capacity_available", "below_cap"];

    public static bool IsField(string field)
    {
        for (int i = 0; i < Fields.Count; i++) if (string.Equals(Fields[i], field, StringComparison.Ordinal)) return true;
        return false;
    }

    public static bool IsTerm(string field, string term)
    {
        for (int i = 0; i < Terms.Count; i++)
            if (string.Equals(Terms[i].Field, field, StringComparison.Ordinal) && string.Equals(Terms[i].Term, term, StringComparison.Ordinal))
                return true;
        return false;
    }

    public static bool IsTrainingCondition(string condition)
    {
        for (int i = 0; i < TrainingConditions.Count; i++)
            if (string.Equals(TrainingConditions[i], condition, StringComparison.Ordinal)) return true;
        return false;
    }
}

/// <summary>One coefficient a promotion scales: <c>Term</c> of D-011 §3 field <c>Field</c>, multiplied by <c>Scale</c> inside
/// its M7 resolution equation. Inert: no equation exists in this build.</summary>
public sealed record PromotionEffect(string Field, string Term, double Scale);

/// <summary>One promotion of a tree (M6.9). <c>Requires</c> are promotion ids of the same tree; <c>ExclusiveGroup</c>, when
/// set, admits at most one held member; <c>MaxTimes</c> is how often it can be taken. Inert content: nothing holds it.</summary>
public sealed record Promotion(
    string Id, string Name, string Description, IReadOnlyList<PromotionEffect> Effects, IReadOnlyList<string> Requires,
    string? ExclusiveGroup, int MaxTimes, int TreeIndex);

/// <summary>One promotion tree: the land army-formation families it covers and its promotions in content order.</summary>
public sealed record PromotionTree(int Index, string Id, string Name, IReadOnlyList<string> Families, IReadOnlyList<Promotion> Promotions)
{
    /// <summary>The tree's first promotion with no prerequisite, in content order — the entry the unit card shows, or null.</summary>
    public Promotion? FirstRoot()
    {
        for (int i = 0; i < Promotions.Count; i++) if (Promotions[i].Requires.Count == 0) return Promotions[i];
        return null;
    }
}

/// <summary>One dormant training profile (M6.10; training-ap-contract.md §4–§6).</summary>
public sealed record TrainingProfile(
    string Id, string Name, IReadOnlyList<string> Families, IReadOnlyList<string> Eligibility,
    double ActionCapacityShare, double Years, int Increment, int Cap, string DiminishingRule, double Factor)
{
    /// <summary>The duration of the k-th training (k ≥ 1) under the escalating-years rule: years × factor^(k−1). Contract
    /// arithmetic only: no system calls it.</summary>
    public double YearsOfTraining(int k)
    {
        if (k < 1 || k > Cap) throw new ArgumentOutOfRangeException(nameof(k), "k must be in 1.." + Cap.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Years * Math.Pow(Factor, k - 1);
    }
}

/// <summary>The reason a refused training order states for a failed condition.</summary>
public sealed record TrainingRejection(string Condition, string Code, string Text);

/// <summary>The D-F training / Action Capacity contract as data (dormant: read by nothing).</summary>
public sealed record TrainingContract(
    string Status, string Mode, double PassiveGainPerYear, string DormantState,
    IReadOnlyList<TrainingProfile> Profiles, IReadOnlyList<TrainingRejection> Rejections);

/// <summary>
/// M6.9 / M6.10 — the loaded unit-progression content (Sim.Data/content/unit-progression.json). INERT by D-E and D-F (option
/// 1, RULED 2026-10-20): it is loaded and validated, and NO simulation system, query, order, AI policy or save table reads it
/// (source guard: UnitProgressionInertTests). Lookups are linear over content order (no dictionary).
/// </summary>
public sealed class UnitProgressionContent
{
    public required string Status { get; init; }
    public required int RepeatCap { get; init; }
    public required IReadOnlyList<PromotionTree> Trees { get; init; }
    public required TrainingContract Training { get; init; }

    /// <summary>The tree covering unit family <paramref name="familyId"/>, or null (a family is in at most one tree).</summary>
    public PromotionTree? TreeForFamily(string familyId)
    {
        for (int t = 0; t < Trees.Count; t++)
            for (int f = 0; f < Trees[t].Families.Count; f++)
                if (string.Equals(Trees[t].Families[f], familyId, StringComparison.Ordinal)) return Trees[t];
        return null;
    }

    public Promotion? PromotionById(string id)
    {
        for (int t = 0; t < Trees.Count; t++)
            for (int p = 0; p < Trees[t].Promotions.Count; p++)
                if (string.Equals(Trees[t].Promotions[p].Id, id, StringComparison.Ordinal)) return Trees[t].Promotions[p];
        return null;
    }

    /// <summary>The training profile covering unit family <paramref name="familyId"/>, or null.</summary>
    public TrainingProfile? TrainingForFamily(string familyId)
    {
        for (int t = 0; t < Training.Profiles.Count; t++)
            for (int f = 0; f < Training.Profiles[t].Families.Count; f++)
                if (string.Equals(Training.Profiles[t].Families[f], familyId, StringComparison.Ordinal)) return Training.Profiles[t];
        return null;
    }
}

/// <summary>
/// Loads and validates unit-progression.json on the T0.4 template (Sim.Core takes a Stream or string; loud, actionable
/// errors). The rules (docs/m6-plan/unit-progression-contract.md §4, training-ap-contract.md §8), each with its own
/// mutant test in UnitProgressionContentTests:
/// <list type="bullet">
/// <item>R1 ids: every tree, promotion and profile id carries its prefix (tree., promo., train.) and is unique file-wide.</item>
/// <item>R2 families: each exists in unit-families.json (when given); a tree covers only army formations; a family is in
/// at most one tree and at most one training profile.</item>
/// <item>R3 fields: every effect names a (field, term) of <see cref="UnitProgressionContract"/>; refused names say why.</item>
/// <item>R4 no duplicated stat: a (field, term) pair is granted by exactly one promotion in the whole file.</item>
/// <item>R5 prerequisites exist in the same tree, are not the promotion itself, and are acyclic.</item>
/// <item>R6 exclusivity: a group has two or more members, all of one tree; no member needs another member of its group;
/// no promotion needs two members of one group (it could never be earned).</item>
/// <item>R7 repeatability: maxTimes in 1..repeatCap; repeatCap in 1..<see cref="UnitProgressionContract.MaxRepeatCap"/>;
/// scale &gt; 0, ≠ 1, and scale^maxTimes within [<see cref="UnitProgressionContract.MinCompound"/>,
/// <see cref="UnitProgressionContract.MaxCompound"/>].</item>
/// <item>T1 training is explicit and dormant: mode 'explicit', passiveGainPerYear exactly 0, dormantState 'idle'.</item>
/// <item>T2 profiles: eligibility from the vocabulary (unique; the cost and cap conditions always present); cost share in
/// (0, 1]; years &gt; 0; increment ≥ 1; cap in 1..<see cref="UnitProgressionContract.MaxTrainingCap"/>; escalating
/// factor in [1, 4].</item>
/// <item>T3 rejections: one per condition, codes unique, text non-empty; every condition a profile names has one.</item>
/// </list>
/// </summary>
public static class UnitProgressionContentLoader
{
    public const int Schema = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { RespectNullableAnnotations = true };

    public static UnitProgressionContent Load(Stream json, UnitFamilyContent? families)
    {
        using var reader = new StreamReader(json);
        return Load(reader.ReadToEnd(), families);
    }

    public static UnitProgressionContent Load(string json, UnitFamilyContent? families)
    {
        FileJson? file;
        try { file = JsonSerializer.Deserialize<FileJson>(json, JsonOptions); }
        catch (JsonException e)
        {
            throw new UnitProgressionContentException($"unit-progression.json is not valid JSON or is missing required values: {e.Message}", e);
        }
        if (file is null) throw Fail("the document is empty (null).");
        if (file.Schema != Schema) throw Fail($"schema is {file.Schema}, expected {Schema}.");
        if (string.IsNullOrWhiteSpace(file.Status)) throw Fail("status must be stated.");
        if (file.RepeatCap < 1 || file.RepeatCap > UnitProgressionContract.MaxRepeatCap)
            throw Fail($"R7: repeatCap must be in 1..{UnitProgressionContract.MaxRepeatCap}, got {file.RepeatCap}.");

        var ids = new List<string>();
        var trees = new PromotionTree[file.Trees.Length];
        var treeFamilies = new List<string>();
        var granted = new List<(string Field, string Term, string By)>();
        var groupTree = new List<(string Group, int Tree)>();
        for (int t = 0; t < file.Trees.Length; t++)
        {
            TreeJson tj = file.Trees[t];
            string tp = $"trees[{t}] ('{tj.Id}')";
            Id(tj.Id, "tree.", tp, ids);
            Named(tj.Name, tp);
            if (tj.Families.Length == 0) throw Fail($"R2: {tp}: families must not be empty.");
            foreach (string fam in tj.Families)
            {
                Family(fam, tp, families, formationsOnly: true);
                if (treeFamilies.Contains(fam)) throw Fail($"R2: {tp}: family '{fam}' is already in another tree (a family is in at most one tree).");
                treeFamilies.Add(fam);
            }
            if (tj.Promotions.Length == 0) throw Fail($"{tp}: promotions must not be empty.");

            var promotions = new Promotion[tj.Promotions.Length];
            for (int p = 0; p < tj.Promotions.Length; p++)
            {
                PromotionJson pj = tj.Promotions[p];
                string pp = $"{tp}.promotions[{p}] ('{pj.Id}')";
                Id(pj.Id, "promo.", pp, ids);
                Named(pj.Name, pp);
                if (string.IsNullOrWhiteSpace(pj.Description)) throw Fail($"{pp}: description must not be empty.");
                if (pj.MaxTimes < 1 || pj.MaxTimes > file.RepeatCap)
                    throw Fail($"R7: {pp}: maxTimes must be in 1..repeatCap ({file.RepeatCap}), got {pj.MaxTimes}.");
                if (pj.Effects.Length == 0) throw Fail($"{pp}: effects must not be empty.");
                var effects = new PromotionEffect[pj.Effects.Length];
                for (int e = 0; e < pj.Effects.Length; e++)
                {
                    EffectJson ej = pj.Effects[e];
                    string ep = $"{pp}.effects[{e}]";
                    for (int r = 0; r < UnitProgressionContract.Refused.Count; r++)
                        if (string.Equals(UnitProgressionContract.Refused[r].Name, ej.Field, StringComparison.Ordinal))
                            throw Fail($"R3: {ep}: field '{ej.Field}' is refused: {UnitProgressionContract.Refused[r].Reason}.");
                    if (!UnitProgressionContract.IsField(ej.Field))
                        throw Fail($"R3: {ep}: field '{ej.Field}' is not a field the M7 contract names ({string.Join(", ", UnitProgressionContract.Fields)}).");
                    if (!UnitProgressionContract.IsTerm(ej.Field, ej.Term))
                        throw Fail($"R3: {ep}: term '{ej.Term}' is not a term of field '{ej.Field}' in the M7 contract.");
                    if (!double.IsFinite(ej.Scale) || ej.Scale <= 0 || ej.Scale == 1.0)
                        throw Fail($"R7: {ep}: scale must be a finite coefficient > 0 and not 1, got {ej.Scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}.");
                    double compound = Math.Pow(ej.Scale, pj.MaxTimes);
                    if (compound < UnitProgressionContract.MinCompound || compound > UnitProgressionContract.MaxCompound)
                        throw Fail($"R7: {ep}: the compounded coefficient scale^maxTimes = {compound.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} is outside " +
                                   $"[{UnitProgressionContract.MinCompound.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {UnitProgressionContract.MaxCompound.ToString(System.Globalization.CultureInfo.InvariantCulture)}].");
                    for (int g = 0; g < granted.Count; g++)
                        if (string.Equals(granted[g].Field, ej.Field, StringComparison.Ordinal) && string.Equals(granted[g].Term, ej.Term, StringComparison.Ordinal))
                            throw Fail($"R4: {ep}: stat {ej.Field}.{ej.Term} is already granted by '{granted[g].By}' (no stat is duplicated within or across trees).");
                    granted.Add((ej.Field, ej.Term, pj.Id));
                    effects[e] = new PromotionEffect(ej.Field, ej.Term, ej.Scale);
                }
                if (pj.ExclusiveGroup is { } group)
                {
                    if (string.IsNullOrWhiteSpace(group)) throw Fail($"R6: {pp}: exclusiveGroup, when present, must name the group.");
                    for (int g = 0; g < groupTree.Count; g++)
                        if (string.Equals(groupTree[g].Group, group, StringComparison.Ordinal) && groupTree[g].Tree != t)
                            throw Fail($"R6: {pp}: exclusive group '{group}' is already used by another tree (a group belongs to one tree).");
                    groupTree.Add((group, t));
                }
                promotions[p] = new Promotion(pj.Id, pj.Name, pj.Description, effects, pj.Requires, pj.ExclusiveGroup, pj.MaxTimes, t);
            }
            ValidateGraph(promotions, tp);
            trees[t] = new PromotionTree(t, tj.Id, tj.Name, tj.Families, promotions);
        }

        TrainingContract training = ValidateTraining(file.Training, ids, families);
        return new UnitProgressionContent { Status = file.Status, RepeatCap = file.RepeatCap, Trees = trees, Training = training };
    }

    /// <summary>R5 and R6 over one tree: prerequisites exist in the tree, acyclic; exclusive groups well formed and reachable.</summary>
    private static void ValidateGraph(Promotion[] promotions, string tp)
    {
        int n = promotions.Length;
        var requires = new int[n][];
        for (int p = 0; p < n; p++)
        {
            string pp = $"{tp}.promotions[{p}] ('{promotions[p].Id}')";
            var idx = new int[promotions[p].Requires.Count];
            for (int r = 0; r < idx.Length; r++)
            {
                string req = promotions[p].Requires[r];
                int at = -1;
                for (int q = 0; q < n; q++) if (string.Equals(promotions[q].Id, req, StringComparison.Ordinal)) at = q;
                if (at < 0) throw Fail($"R5: {pp}: prerequisite '{req}' is not a promotion of this tree.");
                if (at == p) throw Fail($"R5: {pp}: a promotion cannot require itself.");
                for (int s = 0; s < r; s++) if (idx[s] == at) throw Fail($"R5: {pp}: prerequisite '{req}' is listed twice.");
                idx[r] = at;
            }
            requires[p] = idx;
        }
        // Acyclic: depth-first colouring in content order (0 white, 1 on the stack, 2 done).
        var colour = new int[n];
        for (int p = 0; p < n; p++)
            if (colour[p] == 0 && Cycle(p, requires, colour) is int at)
                throw Fail($"R5: {tp}: the prerequisites form a cycle through '{promotions[at].Id}'.");
        // Transitive prerequisites of each promotion.
        var ancestors = new bool[n][];
        for (int p = 0; p < n; p++)
        {
            ancestors[p] = new bool[n];
            var stack = new Stack<int>();
            foreach (int r in requires[p]) stack.Push(r);
            while (stack.Count > 0)
            {
                int q = stack.Pop();
                if (ancestors[p][q]) continue;
                ancestors[p][q] = true;
                foreach (int r in requires[q]) stack.Push(r);
            }
        }
        for (int p = 0; p < n; p++)
        {
            string pp = $"{tp}.promotions[{p}] ('{promotions[p].Id}')";
            if (promotions[p].ExclusiveGroup is { } group)
            {
                int members = 0;
                for (int q = 0; q < n; q++)
                    if (string.Equals(promotions[q].ExclusiveGroup, group, StringComparison.Ordinal))
                    {
                        members++;
                        if (q != p && ancestors[p][q])
                            throw Fail($"R6: {pp}: it requires '{promotions[q].Id}' of its own exclusive group '{group}', so it could never be earned.");
                    }
                if (members < 2) throw Fail($"R6: {pp}: exclusive group '{group}' has only one member (a group excludes two or more).");
            }
            for (int a = 0; a < n; a++)
            {
                if (!ancestors[p][a] || promotions[a].ExclusiveGroup is not { } ga) continue;
                for (int b = a + 1; b < n; b++)
                    if (ancestors[p][b] && string.Equals(promotions[b].ExclusiveGroup, ga, StringComparison.Ordinal))
                        throw Fail($"R6: {pp}: it requires both '{promotions[a].Id}' and '{promotions[b].Id}' of exclusive group '{ga}', so it could never be earned.");
            }
        }
    }

    private static int? Cycle(int p, int[][] requires, int[] colour)
    {
        colour[p] = 1;
        foreach (int r in requires[p])
        {
            if (colour[r] == 1) return r;
            if (colour[r] == 0 && Cycle(r, requires, colour) is int at) return at;
        }
        colour[p] = 2;
        return null;
    }

    private static TrainingContract ValidateTraining(TrainingJson tj, List<string> ids, UnitFamilyContent? families)
    {
        if (string.IsNullOrWhiteSpace(tj.Status)) throw Fail("training.status must be stated.");
        if (!string.Equals(tj.Mode, UnitProgressionContract.ExplicitMode, StringComparison.Ordinal))
            throw Fail($"T1: training.mode must be '{UnitProgressionContract.ExplicitMode}' (training is an ordered action; a passive mode would improve dormant formations), got '{tj.Mode}'.");
        if (tj.PassiveGainPerYear != 0.0)
            throw Fail("T1: training.passiveGainPerYear must be exactly 0: a dormant formation never improves (D-F; training-ap-contract.md §2).");
        if (!string.Equals(tj.DormantState, UnitProgressionContract.DormantState, StringComparison.Ordinal))
            throw Fail($"T1: training.dormantState must name the M7 action state '{UnitProgressionContract.DormantState}', got '{tj.DormantState}'.");
        if (tj.Profiles.Length == 0) throw Fail("T2: training.profiles must not be empty.");

        var codes = new List<string>();
        var covered = new List<string>();
        var rejections = new TrainingRejection[tj.Rejections.Length];
        for (int r = 0; r < tj.Rejections.Length; r++)
        {
            RejectionJson rj = tj.Rejections[r];
            string rp = $"training.rejections[{r}] ('{rj.Code}')";
            if (!UnitProgressionContract.IsTrainingCondition(rj.Condition))
                throw Fail($"T3: {rp}: condition '{rj.Condition}' is not in the contract vocabulary.");
            if (covered.Contains(rj.Condition)) throw Fail($"T3: {rp}: condition '{rj.Condition}' already has a rejection.");
            if (string.IsNullOrWhiteSpace(rj.Code) || codes.Contains(rj.Code)) throw Fail($"T3: {rp}: the code must be non-empty and unique.");
            if (string.IsNullOrWhiteSpace(rj.Text)) throw Fail($"T3: {rp}: the text a refused order states must not be empty.");
            covered.Add(rj.Condition);
            codes.Add(rj.Code);
            rejections[r] = new TrainingRejection(rj.Condition, rj.Code, rj.Text);
        }

        var profileFamilies = new List<string>();
        var profiles = new TrainingProfile[tj.Profiles.Length];
        for (int p = 0; p < tj.Profiles.Length; p++)
        {
            ProfileJson pj = tj.Profiles[p];
            string pp = $"training.profiles[{p}] ('{pj.Id}')";
            Id(pj.Id, "train.", pp, ids);
            Named(pj.Name, pp);
            if (pj.Families.Length == 0) throw Fail($"R2: {pp}: families must not be empty.");
            foreach (string fam in pj.Families)
            {
                Family(fam, pp, families, formationsOnly: false);
                if (profileFamilies.Contains(fam)) throw Fail($"R2: {pp}: family '{fam}' is already in another training profile.");
                profileFamilies.Add(fam);
            }
            var seen = new List<string>();
            foreach (string c in pj.Eligibility)
            {
                if (!UnitProgressionContract.IsTrainingCondition(c)) throw Fail($"T2: {pp}: eligibility condition '{c}' is not in the contract vocabulary.");
                if (seen.Contains(c)) throw Fail($"T2: {pp}: eligibility condition '{c}' is listed twice.");
                if (!covered.Contains(c)) throw Fail($"T3: {pp}: eligibility condition '{c}' has no rejection (every condition states its reason).");
                seen.Add(c);
            }
            foreach (string m in UnitProgressionContract.MandatoryTrainingConditions)
                if (!seen.Contains(m)) throw Fail($"T2: {pp}: eligibility must include '{m}' (the cost and the cap always apply).");
            if (!double.IsFinite(pj.Cost.ActionCapacityShare) || pj.Cost.ActionCapacityShare <= 0 || pj.Cost.ActionCapacityShare > 1)
                throw Fail($"T2: {pp}: cost.actionCapacityShare must be in (0, 1].");
            if (!double.IsFinite(pj.Cost.Years) || pj.Cost.Years <= 0) throw Fail($"T2: {pp}: cost.years must be > 0 (a duration in sim-years, Law 3).");
            if (pj.Increment < 1) throw Fail($"T2: {pp}: increment must be >= 1.");
            if (pj.Cap < 1 || pj.Cap > UnitProgressionContract.MaxTrainingCap)
                throw Fail($"T2: {pp}: cap must be in 1..{UnitProgressionContract.MaxTrainingCap}, got {pj.Cap}.");
            if (!string.Equals(pj.Diminishing.Rule, UnitProgressionContract.EscalatingYears, StringComparison.Ordinal))
                throw Fail($"T2: {pp}: diminishing.rule must be '{UnitProgressionContract.EscalatingYears}', got '{pj.Diminishing.Rule}'.");
            if (!double.IsFinite(pj.Diminishing.Factor) || pj.Diminishing.Factor < UnitProgressionContract.MinFactor || pj.Diminishing.Factor > UnitProgressionContract.MaxFactor)
                throw Fail($"T2: {pp}: diminishing.factor must be in [{UnitProgressionContract.MinFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                           $"{UnitProgressionContract.MaxFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)}].");
            profiles[p] = new TrainingProfile(pj.Id, pj.Name, pj.Families, pj.Eligibility, pj.Cost.ActionCapacityShare, pj.Cost.Years,
                pj.Increment, pj.Cap, pj.Diminishing.Rule, pj.Diminishing.Factor);
        }
        return new TrainingContract(tj.Status, tj.Mode, tj.PassiveGainPerYear, tj.DormantState, profiles, rejections);
    }

    private static void Id(string id, string prefix, string path, List<string> ids)
    {
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(prefix, StringComparison.Ordinal) || id.Length == prefix.Length)
            throw Fail($"R1: {path}: id must start with '{prefix}' and name something.");
        if (ids.Contains(id)) throw Fail($"R1: {path}: duplicate id '{id}' (ids are unique across the file).");
        ids.Add(id);
    }

    private static void Named(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name)) throw Fail($"{path}: name must not be empty.");
    }

    private static void Family(string id, string path, UnitFamilyContent? families, bool formationsOnly)
    {
        if (string.IsNullOrWhiteSpace(id)) throw Fail($"R2: {path}: a family id must not be empty.");
        if (families is null) return;
        UnitFamily? family = families.FamilyById(id);
        if (family is null) throw Fail($"R2: {path}: family '{id}' is not a family in unit-families.json.");
        if (formationsOnly && family.Composition != UnitComposition.ArmyFormation)
            throw Fail($"R2: {path}: family '{id}' is not an army formation: D-011 §3's formation fields exist only for formations.");
    }

    private static UnitProgressionContentException Fail(string message) => new("unit-progression.json: " + message);

    private sealed record FileJson(
        [property: JsonPropertyName("schema"), JsonRequired] int Schema,
        [property: JsonPropertyName("status"), JsonRequired] string Status,
        [property: JsonPropertyName("repeatCap"), JsonRequired] int RepeatCap,
        [property: JsonPropertyName("trees"), JsonRequired] TreeJson[] Trees,
        [property: JsonPropertyName("training"), JsonRequired] TrainingJson Training);

    private sealed record TreeJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("families"), JsonRequired] string[] Families,
        [property: JsonPropertyName("promotions"), JsonRequired] PromotionJson[] Promotions);

    private sealed record PromotionJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("description"), JsonRequired] string Description,
        [property: JsonPropertyName("effects"), JsonRequired] EffectJson[] Effects,
        [property: JsonPropertyName("requires"), JsonRequired] string[] Requires,
        [property: JsonPropertyName("maxTimes"), JsonRequired] int MaxTimes,
        [property: JsonPropertyName("exclusiveGroup")] string? ExclusiveGroup = null);

    private sealed record EffectJson(
        [property: JsonPropertyName("field"), JsonRequired] string Field,
        [property: JsonPropertyName("term"), JsonRequired] string Term,
        [property: JsonPropertyName("scale"), JsonRequired] double Scale);

    private sealed record TrainingJson(
        [property: JsonPropertyName("status"), JsonRequired] string Status,
        [property: JsonPropertyName("mode"), JsonRequired] string Mode,
        [property: JsonPropertyName("passiveGainPerYear"), JsonRequired] double PassiveGainPerYear,
        [property: JsonPropertyName("dormantState"), JsonRequired] string DormantState,
        [property: JsonPropertyName("profiles"), JsonRequired] ProfileJson[] Profiles,
        [property: JsonPropertyName("rejections"), JsonRequired] RejectionJson[] Rejections);

    private sealed record ProfileJson(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("name"), JsonRequired] string Name,
        [property: JsonPropertyName("families"), JsonRequired] string[] Families,
        [property: JsonPropertyName("eligibility"), JsonRequired] string[] Eligibility,
        [property: JsonPropertyName("cost"), JsonRequired] CostJson Cost,
        [property: JsonPropertyName("increment"), JsonRequired] int Increment,
        [property: JsonPropertyName("cap"), JsonRequired] int Cap,
        [property: JsonPropertyName("diminishing"), JsonRequired] DiminishingJson Diminishing);

    private sealed record CostJson(
        [property: JsonPropertyName("actionCapacityShare"), JsonRequired] double ActionCapacityShare,
        [property: JsonPropertyName("years"), JsonRequired] double Years);

    private sealed record DiminishingJson(
        [property: JsonPropertyName("rule"), JsonRequired] string Rule,
        [property: JsonPropertyName("factor"), JsonRequired] double Factor);

    private sealed record RejectionJson(
        [property: JsonPropertyName("condition"), JsonRequired] string Condition,
        [property: JsonPropertyName("code"), JsonRequired] string Code,
        [property: JsonPropertyName("text"), JsonRequired] string Text);
}
