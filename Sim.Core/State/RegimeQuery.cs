using System.Globalization;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>ADR-057 — one regime module as a polity has it now: present or not, and why (derived every read).</summary>
public sealed record RegimeModuleView(string Id, string Name, bool Present, string Reason);

/// <summary>ADR-057 / D-066 A10 — one government-form civic as the presentation shows it: its research state
/// (<see cref="CivicState"/>) and the line the UI writes — "Available — adoption in M8" once researched, the reason
/// otherwise. There is no adopted state in M6 (D-D).</summary>
public sealed record GovernmentFormView(int ContentIndex, ResearchNodeId Key, string Id, string Name, CivicState State, string Line);

/// <summary>
/// ADR-057 (D-D option 1, RULED; D-066 A2 item 2, A10, A13) — THE REGIME READ SURFACE, READ-ONLY. The regime-module data
/// shape (sim.json <c>regime</c>) is answered here from the world every time it is asked: a module is PRESENT exactly while
/// its owner holds an ACTIVE institution (founded — <see cref="InstitutionLifecycle.Active"/> or Mature) of the module's
/// type; in M6 the one module is "codified law", present while the polity holds a law court. The GOVERNMENT LABEL is
/// DERIVED from the base label and the present modules' qualifiers ("chiefdom" → "law-giving chiefdom") and is NEVER
/// STORED and NEVER READ BY ANY MECHANISM — it exists for the presentation only (arch-FGH: "DERIVED … never read by any
/// mechanism"); RegimeLabelSourceGuardTests proves no file of Sim.Core other than this one names this class. No adoption
/// order exists, no state is written, and no Age, date or currency is read (law 4; I-3; I-6). Pure; table and content
/// order; no dictionary, no LINQ.
/// </summary>
public static class RegimeQuery
{
    /// <summary>The regime modules of <paramref name="polity"/>, content order.</summary>
    public static RegimeModuleView[] Modules(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Regime is not { } regime) return [];
        var result = new RegimeModuleView[regime.Modules.Length];
        for (int m = 0; m < result.Length; m++)
        {
            RegimeModuleConfig module = regime.Modules[m];
            InstitutionTypeInfo? type = TypeById(cfg, module.InstitutionType);
            long active = type is null ? 0 : ActiveCount(world, cfg, polity, type.Key);
            string typeName = type?.Name ?? module.InstitutionType;
            result[m] = new RegimeModuleView(module.Id, module.Name, active > 0,
                active > 0
                    ? "present: " + active.ToString(CultureInfo.InvariantCulture) + " active " + typeName.ToLowerInvariant() + (active == 1 ? "" : "s")
                    : "absent: needs an active " + typeName.ToLowerInvariant());
        }
        return result;
    }

    /// <summary>
    /// THE DERIVED GOVERNMENT LABEL of <paramref name="polity"/>: the present modules' qualifiers (content order), then the
    /// base label — "chiefdom", "law-giving chiefdom". Presentation only: never stored, never read by a mechanism. Empty when
    /// no regime content is loaded.
    /// </summary>
    public static string Label(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Regime is not { } regime) return "";
        string label = regime.BaseLabel;
        RegimeModuleView[] modules = Modules(world, cfg, polity);
        for (int m = modules.Length - 1; m >= 0; m--)
            if (modules[m].Present) label = regime.Modules[m].Qualifier + " " + label;
        return label;
    }

    /// <summary>D-066 A10: every government form (sim.json <c>regime.forms</c> order) with its research state and line.</summary>
    public static GovernmentFormView[] Forms(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Regime is not { } regime || cfg.Research is not { } research) return [];
        bool[] completed = ResearchQuery.CompletedMask(world, research, polity);
        bool[] available = ResearchQuery.AvailableMask(research, completed, ResearchQuery.LevelMask(world, research, polity, completed));
        var result = new List<GovernmentFormView>(regime.Forms.Length);
        foreach (string id in regime.Forms)
        {
            int i = research.IndexOfId(id);
            if (i < 0) continue;
            ResearchNode n = research.Nodes[i];
            CivicState state = ResearchQuery.CivicStateOf(completed, available, i);
            result.Add(new GovernmentFormView(i, n.Key, n.Id, n.Name, state, FormLine(research, completed, i, state)));
        }
        return [.. result];
    }

    /// <summary>The line a form shows: researched → "Available — adoption in M8"; researchable → it may be researched (which is
    /// not adopting it); locked → what it waits for (its incomplete prerequisites, else the subtree stage or the Age sweep).</summary>
    public static string FormLine(ResearchContent research, bool[] completed, int node, CivicState state)
    {
        switch (state)
        {
            case CivicState.Researched: return "Available - adoption in M8";
            case CivicState.Available: return "Not yet researched - open to research now (researching a form is not adopting it)";
        }
        ResearchNode n = research.Nodes[node];
        var missing = new List<string>();
        foreach (int a in n.PrerequisiteNodes) if (!completed[a]) missing.Add(research.Nodes[a].Name);
        if (missing.Count > 0) return "Locked - needs " + string.Join(", ", missing);
        ResearchQuery.SweepState sweep = ResearchQuery.SweepBlock(research, completed, node);
        if (sweep.Blocked)
            return "Locked - the Age " + sweep.CohortAge.ToString(CultureInfo.InvariantCulture) + " sweep: "
                + sweep.Outstanding.Length.ToString(CultureInfo.InvariantCulture) + " nodes outstanding";
        return "Locked";
    }

    /// <summary>How many institutions of type <paramref name="typeKey"/> <paramref name="polity"/> owns in an ACTIVE stage
    /// (founded: Active or Mature) — every founded row is.</summary>
    private static long ActiveCount(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, int typeKey)
    {
        long n = 0;
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Polity.Value != polity.Value || row.Type != typeKey) continue;
            InstitutionLifecycle stage = InstitutionsQuery.Stage(cfg, row.Type, row.Maturity);
            if (stage is InstitutionLifecycle.Active or InstitutionLifecycle.Mature) n++;
        }
        return n;
    }

    private static InstitutionTypeInfo? TypeById(SimConfig cfg, string id)
    {
        foreach (InstitutionTypeInfo t in InstitutionContent.Types(cfg))
            if (string.Equals(t.Id, id, StringComparison.Ordinal)) return t;
        return null;
    }
}

/// <summary>
/// ADR-057 (D-066 A6; m6-spec I-7) — THE OPTIONALITY VALIDATOR. "Optional" means "not needed for the next Age": a
/// government form of content Age x must not be NECESSARY for entering any Age y ≤ x + 1. A node is necessary for an entry
/// when every way of meeting the entry completes it:
/// <list type="bullet">
/// <item>a node's necessary set N(n) = {n} ∪ its prerequisite expression's (AND = union, OR = intersection, a NOT or a
///   comparison = nothing) ∪ the research stage's when n is in a subtree ∪ — with tuning.ageSweep — N of every sweep-cohort
///   node of Age(n) − 2 (ADR-048);</item>
/// <item>a Research fact (ANY of its nodes) needs the intersection of its nodes' N; a ResearchCount fact needs every node of
///   its tree only when its minimum is the whole tree; a Structures fact of one project needs N of the entities that gate it
///   (the project's building and what it founds); facts of other kinds name no research;</item>
/// <item>an entry needs the union over its CORE milestones, plus every node needed by more than (supporting − required)
///   supporting milestones (any admissible choice of supporting milestones includes one of them).</item>
/// </list>
/// The minimum-categories rule is not modelled (it can only add necessity, so the validator may miss a form it forces —
/// never flag one that is optional). Pure; content order; memoized over node indices.
/// </summary>
public static class RegimeContent
{
    /// <summary>Every (form, Age) where a government form is necessary for entering an Age at most one after its own, as
    /// readable lines; empty when the forms are optional (or no regime, research or Age content is loaded).</summary>
    public static IReadOnlyList<string> OptionalityViolations(SimConfig cfg)
    {
        var result = new List<string>();
        if (cfg.Regime is not { } regime || cfg.Research is not { } research || cfg.Ages is not { } ages) return result;
        var closure = new Closure(research, cfg.Goods);
        for (int age = 1; age <= AgeContent.AgeCount; age++)
        {
            if (ages.Age(age).Entry is not { } entry) continue;
            bool[] needed = closure.Entry(entry);
            foreach (string id in regime.Forms)
            {
                int f = research.IndexOfId(id);
                if (f < 0 || !needed[f]) continue;
                if (age <= research.Nodes[f].AgeNumber + 1)
                    result.Add($"'{id}' (Age {research.Nodes[f].AgeNumber}) is necessary for entering Age {age}");
            }
        }
        return result;
    }

    /// <summary>The nodes necessary for entering an Age with <paramref name="entry"/> (see the class summary), as a mask.</summary>
    public static bool[] NecessaryForEntry(ResearchContent research, GoodsConfig? goods, AgeEntryRequirements entry) =>
        new Closure(research, goods).Entry(entry);

    private sealed class Closure(ResearchContent research, GoodsConfig? goods)
    {
        private readonly bool[]?[] _memo = new bool[]?[research.Nodes.Count];
        private bool[]? _stage;

        public bool[] Entry(AgeEntryRequirements entry)
        {
            var needed = new bool[research.Nodes.Count];
            foreach (AgeMilestone m in entry.Core) Or(needed, Fact(m.Fact));
            int slack = entry.Supporting.Count - entry.SupportingRequired;
            if (entry.Supporting.Count > 0 && entry.SupportingRequired > 0)
            {
                var counts = new int[research.Nodes.Count];
                foreach (AgeMilestone m in entry.Supporting)
                {
                    bool[] f = Fact(m.Fact);
                    for (int i = 0; i < f.Length; i++) if (f[i]) counts[i]++;
                }
                for (int i = 0; i < counts.Length; i++) if (counts[i] > slack) needed[i] = true;
            }
            return needed;
        }

        private bool[] Fact(AgeMilestoneFact fact)
        {
            var none = new bool[research.Nodes.Count];
            switch (fact.Kind)
            {
                case MilestoneFactKind.Research:
                {
                    bool[]? acc = null;
                    foreach (int key in fact.NodeKeys)
                    {
                        int i = research.IndexOf(new ResearchNodeId(key));
                        if (i < 0) return none;
                        acc = acc is null ? (bool[])Of(i).Clone() : And(acc, Of(i));
                    }
                    return acc ?? none;
                }
                case MilestoneFactKind.ResearchCount:
                {
                    if (fact.Min < fact.TreeKeys.Length) return none;
                    foreach (int key in fact.TreeKeys)
                        if (research.IndexOf(new ResearchNodeId(key)) is int i and >= 0) Or(none, Of(i));
                    return none;
                }
                case MilestoneFactKind.Structures:
                {
                    if (fact.Ref < 0 || goods?.ProjectById(fact.Ref) is not { } project) return none;
                    if (project.Entity is { } building) Or(none, OfEntity(building));
                    if (project.Founds is { } founds) Or(none, OfEntity(founds.Entity));
                    return none;
                }
                default: return none;
            }
        }

        private bool[] OfEntity(string id)
        {
            var result = new bool[research.Nodes.Count];
            int e = research.EntityIndexOf(id);
            if (e < 0 || research.Entities[e].Requirement is not { } req) return result;
            Or(result, Expression(req));
            return result;
        }

        /// <summary>N(n), memoized. Prerequisites point to earlier nodes and the sweep to an earlier Age, so the recursion
        /// terminates (the loader refuses a prerequisite cycle).</summary>
        public bool[] Of(int node)
        {
            if (_memo[node] is { } done) return done;
            ResearchNode n = research.Nodes[node];
            var set = new bool[research.Nodes.Count];
            set[node] = true;
            if (n.Prerequisite is { } p) Or(set, Expression(p));
            if (n.Branch >= 0) Or(set, Stage());
            if (research.Tuning.AgeSweep && n.AgeNumber - 2 >= 1) Or(set, Cohort(n.AgeNumber - 2));
            _memo[node] = set;
            return set;
        }

        private readonly bool[]?[] _cohorts = new bool[]?[AgeContent.AgeCount + 1];

        /// <summary>The union of N over every sweep-cohort node of content Age <paramref name="age"/>, memoized.</summary>
        private bool[] Cohort(int age)
        {
            if (_cohorts[age] is { } done) return done;
            var set = new bool[research.Nodes.Count];
            for (int i = 0; i < research.Nodes.Count; i++)
                if (research.Nodes[i].AgeNumber == age && ResearchQuery.IsSweepCohortMember(research.Nodes[i])) Or(set, Of(i));
            _cohorts[age] = set;
            return set;
        }

        private bool[] Stage() => _stage ??= Expression(research.Stage);

        private bool[] Expression(Sim.Core.Systems.ClassMobility.Predicate p) =>
            p.Fold(atom => atom >= 0 && atom < research.Nodes.Count ? Of(atom) : new bool[research.Nodes.Count],
                (l, r) => { var u = (bool[])l.Clone(); Or(u, r); return u; },
                (l, r) => And(l, r),
                new bool[research.Nodes.Count]);

        private static void Or(bool[] into, bool[] add)
        {
            for (int i = 0; i < into.Length; i++) if (add[i]) into[i] = true;
        }

        private static bool[] And(bool[] a, bool[] b)
        {
            var r = new bool[a.Length];
            for (int i = 0; i < r.Length; i++) r[i] = a[i] && b[i];
            return r;
        }
    }
}
