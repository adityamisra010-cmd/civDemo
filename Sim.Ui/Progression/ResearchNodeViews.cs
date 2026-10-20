using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Ui.Progression;

/// <summary>A node's research state as the progression screen shows it. Ordered so that a
/// node is in exactly ONE state: Completed › CurrentTarget › Available › Partial › Locked.
/// Partial = holds progress but is neither the target nor available (e.g. its subtree closed
/// after a content change); an AVAILABLE node with progress stays Available and shows a bar.</summary>
public enum NodeState { Completed = 1, CurrentTarget = 2, Available = 3, Partial = 4, Locked = 5 }

/// <summary>Why a locked node cannot be researched (several may hold).</summary>
[Flags]
public enum LockReason { None = 0, MissingPrerequisites = 1, ResearchStage = 2, SubtreeNotExhausted = 4 }

public sealed record PrereqView(int ContentIndex, string Name, bool Completed, EdgeKind Kind);

public sealed record EurekaView(string Text, string Kind, double Weight, double MaxCredit, string? Condition,
    bool Fired, bool? HoldsNow, string System);

public sealed record UniversityView(string Name, string Role);

/// <summary>
/// Everything a node card and the detail panel show, computed ONCE per world through
/// <see cref="ResearchQuery"/> — the same statics the ResearchSystem computes with — so the
/// screen can never disagree with the simulation (ADR-029 §12). Pure; never writes state.
/// </summary>
public sealed record ResearchNodeView(
    int ContentIndex, ResearchNodeId Key, string Name, string Description, string Age, ResearchTree Tree, int Branch,
    string BranchName, string Domain, NodeState State, LockReason Lock, bool Available, bool IsTarget, double Progress,
    double BaseCost, double EffectiveCost, bool FloorBinds, IReadOnlyList<(string University, double Factor)> CostTerms,
    string? PrerequisiteExpression, IReadOnlyList<PrereqView> Prerequisites, IReadOnlyList<EurekaView> Eurekas,
    double EurekaCeiling, double EurekaCredited, double ForeignCredited, double ExposureOffered,
    IReadOnlyList<UniversityView> Universities, IReadOnlyList<string> Unlocks, bool IsRecursive)
{
    /// <summary>Progress toward EffectiveCost, 0..1.</summary>
    public double Fraction => EffectiveCost > 0 ? Math.Clamp(Progress / EffectiveCost, 0.0, 1.0) : 0.0;
    public int EurekasFired { get { int k = 0; foreach (EurekaView e in Eurekas) if (e.Fired) k++; return k; } }
}

/// <summary>The whole polity's research picture at one world state.</summary>
public sealed class ResearchSnapshot
{
    public required long Turn { get; init; }
    public required IReadOnlyList<ResearchNodeView> Nodes { get; init; }   // content order
    public required bool StageReached { get; init; }
    public required string StageExpression { get; init; }
    public required double PointsPerTurn { get; init; }
    public required int? TargetIndex { get; init; }
    public required int CompletedTechnology { get; init; }
    public required int CompletedCivics { get; init; }

    public static ResearchSnapshot Build(IReadOnlyWorldState world, ResearchContent content, PolityId polity)
    {
        int n = content.Nodes.Count;
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        bool stage = ResearchQuery.StageReached(content, completed);
        int? target = null;
        if (ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId t)) { int ti = content.IndexOf(t); if (ti >= 0) target = ti; }

        var views = new ResearchNodeView[n];
        int doneTech = 0, doneCiv = 0;
        for (int i = 0; i < n; i++)
        {
            ResearchNode node = content.Nodes[i];
            bool done = completed[i];
            if (done) { if (node.Tree == ResearchTree.Technology) doneTech++; else doneCiv++; }
            bool available = ResearchQuery.IsAvailable(content, i, completed, stage);
            double progress = done ? 0.0 : ResearchQuery.Progress(world, polity, node.Key);
            bool isTarget = target == i;

            LockReason lockReason = LockReason.None;
            if (!done && !available)
            {
                if (!ResearchQuery.PrerequisitesMet(content, i, completed)) lockReason |= LockReason.MissingPrerequisites;
                if (node.Branch >= 0 && !stage) lockReason |= LockReason.ResearchStage;
                if (node.IsRecursive && node.Branch >= 0 && !ResearchQuery.SubtreeExhausted(content, node.Branch, completed))
                    lockReason |= LockReason.SubtreeNotExhausted;
            }
            NodeState state = done ? NodeState.Completed
                : isTarget ? NodeState.CurrentTarget
                : available ? NodeState.Available
                : progress > 0 ? NodeState.Partial
                : NodeState.Locked;

            ResearchQuery.CostBreakdown cost = ResearchQuery.EffectiveCostBreakdown(world, content, polity, node.Key);
            var terms = new List<(string, double)>();
            foreach (ResearchQuery.CostTerm term in cost.Terms) terms.Add((term.UniversityId, term.Factor));

            IReadOnlyList<int> must = node.Prerequisite?.MustHoldAtoms() ?? [];
            var prereqs = new PrereqView[node.PrerequisiteNodes.Count];
            for (int k = 0; k < prereqs.Length; k++)
            {
                int p = node.PrerequisiteNodes[k];
                bool and = false;
                for (int m = 0; m < must.Count; m++) if (must[m] == p) { and = true; break; }
                prereqs[k] = new PrereqView(p, content.Nodes[p].Name, completed[p], and ? EdgeKind.And : EdgeKind.Or);
            }

            ResearchQuery.EurekaState[] eu = ResearchQuery.Eurekas(world, content, polity, node.Key);
            var eurekas = new EurekaView[eu.Length];
            for (int e = 0; e < eu.Length; e++)
                eurekas[e] = new EurekaView(eu[e].Text, eu[e].Kind, eu[e].Weight, eu[e].MaxCredit, eu[e].Condition,
                    eu[e].Fired, eu[e].HoldsNow, eu[e].System);
            ResearchQuery.AccelerationPool pool = ResearchQuery.AccelerationPoolOf(world, content, polity, node.Key);

            var unis = new List<UniversityView>();
            foreach (ResearchUniversityRelevance r in node.UniversityRelevance)
            {
                int u = content.UniversityTypeIndexOf(r.UniversityTypeKey);
                unis.Add(new UniversityView(u >= 0 ? content.UniversityTypes[u].Name : "#" + r.UniversityTypeKey.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Role));
            }
            var unlocks = new List<string>();
            foreach (int ent in node.UnlockedEntities) unlocks.Add(content.Entities[ent].Name ?? content.Entities[ent].Id);

            views[i] = new ResearchNodeView(i, node.Key, node.Name, node.Description, node.Age, node.Tree, node.Branch,
                node.Branch >= 0 ? content.Branches[node.Branch].Name : node.Tree == ResearchTree.Technology ? "Main Trunk" : "Civics",
                node.Domain, state, lockReason, available, isTarget, progress, cost.BaseCost, cost.EffectiveCost, cost.FloorBinds,
                terms, node.Prerequisite?.Source, prereqs, eurekas, pool.Ceiling, pool.Eureka, pool.ForeignExposure,
                pool.ExposureOffered, unis, unlocks, node.IsRecursive);
        }

        return new ResearchSnapshot
        {
            Turn = world.Clock.Turn,
            Nodes = views,
            StageReached = stage,
            StageExpression = content.Stage.Source,
            PointsPerTurn = ResearchQuery.ResearchPointPool(world, content, polity),
            TargetIndex = target,
            CompletedTechnology = doneTech,
            CompletedCivics = doneCiv,
        };
    }
}
