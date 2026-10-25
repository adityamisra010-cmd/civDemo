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
public enum LockReason { None = 0, MissingPrerequisites = 1, ResearchStage = 2, SubtreeNotExhausted = 4, FinalAgeCivicsNotExhausted = 8, MaxLevel = 16, AgeSweep = 32 }

public sealed record PrereqView(int ContentIndex, string Name, bool Completed, EdgeKind Kind);

public sealed record EurekaView(string Text, string Kind, double Weight, double MaxCredit, string? Condition,
    bool Fired, bool? HoldsNow, string System);

public sealed record UniversityView(string Name, string Role);

/// <summary>ADR-049: one operand effect of a levelled repeatable as the panel shows it — the operand's meaning, the benefit
/// the NEXT level adds (cap × k × (1 − k)^level) and the cumulative benefit of the levels researched
/// (ResearchQuery.RepeatableContribution: cap × (1 − (1 − k)^N)), both as improvement fractions of the operand's Σ, beside
/// its cap.</summary>
public sealed record RepeatEffectView(string Operand, string Meaning, bool Higher, double Cap, double GapFraction,
    double NextLevelBenefit, double Cumulative);

/// <summary>ADR-049: a levelled repeatable's levels — researched <see cref="Level"/> times, the next level's cost, and its
/// operand effects. Such a node is never shown as complete.</summary>
public sealed record RepeatView(int Level, int MaxLevel, double CostGrowth, double NextLevelCost,
    IReadOnlyList<RepeatEffectView> Effects)
{
    /// <summary>"Researched N×" (or "Not yet researched" at level 0).</summary>
    public string LevelText => Level == 0 ? "Not yet researched"
        : "Researched " + Level.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u00d7";
}

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
    IReadOnlyList<UniversityView> Universities, IReadOnlyList<string> Unlocks, bool IsRecursive, string? History = null,
    RepeatView? Repeat = null)
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
    /// <summary>The Technology lane's target (ADR-043); in the one-pool model, the one target, whichever tree.</summary>
    public required int? TargetIndex { get; init; }
    public required int CompletedTechnology { get; init; }
    public required int CompletedCivics { get; init; }

    /// <summary>ADR-043: whether the two-lane model is on (RP → Technology, CP → Civics).</summary>
    public bool TwoLanes { get; init; }
    /// <summary>ADR-043: the Civics lane's target (two-lane model only).</summary>
    public int? CivicsTargetIndex { get; init; }
    /// <summary>ADR-043: the banked Civics Points (whole CP).</summary>
    public double CivicsBalance { get; init; }
    /// <summary>ADR-043: the Civics Points the NEXT step accrues (ResearchQuery.CivicsPointsMilli / 1000 over the next step's
    /// dt — the era table's at the world's date; rp-cp-lifecycle k5).</summary>
    public double CivicsPerTurn { get; init; }

    /// <summary>ADR-048: whether destination research is on (research.json tuning.destinationRouting).</summary>
    public bool Routing { get; init; }
    /// <summary>ADR-048: the Technology lane's route — <see cref="ResearchQuery.Route"/> itself, never recomputed here.</summary>
    public ResearchRoute TechnologyRoute { get; init; } = ResearchRoute.None(ResearchTree.Technology);
    /// <summary>ADR-048: the Civics lane's route — <see cref="ResearchQuery.Route"/> itself.</summary>
    public ResearchRoute CivicsRoute { get; init; } = ResearchRoute.None(ResearchTree.Civics);
    /// <summary>ADR-048: per node, the Age sweep as it bears on it (<see cref="ResearchQuery.SweepBlock(ResearchContent, bool[], int)"/>).</summary>
    public IReadOnlyList<ResearchQuery.SweepState> Sweeps { get; init; } = [];

    /// <summary>ADR-048: the route of <paramref name="tree"/>'s lane.</summary>
    public ResearchRoute RouteOn(ResearchTree tree) => tree == ResearchTree.Civics ? CivicsRoute : TechnologyRoute;

    /// <summary>The target on <paramref name="tree"/>'s lane: in the two-lane model each tree's own; in the one-pool model
    /// the one target when it lies in that tree.</summary>
    public int? TargetOn(ResearchTree tree, IReadOnlyList<ResearchNodeView> nodes) =>
        TwoLanes ? (tree == ResearchTree.Civics ? CivicsTargetIndex : TargetIndex)
        : TargetIndex is int t && nodes[t].Tree == tree ? t : null;

    /// <summary>The points a turn that fund <paramref name="tree"/>: CP for Civics in the two-lane model, else RP.</summary>
    public double PointsPerTurnOn(ResearchTree tree) => TwoLanes && tree == ResearchTree.Civics ? CivicsPerTurn : PointsPerTurn;

    /// <summary>The unit of <see cref="PointsPerTurnOn"/>: "CP" or "RP".</summary>
    public string UnitOn(ResearchTree tree) => TwoLanes && tree == ResearchTree.Civics ? "CP" : "RP";

    /// <summary>The snapshot of <paramref name="world"/>. <paramref name="nextDtYears"/> is the length of the step the next End
    /// Turn plays (<see cref="ActionQueryContext.ForNextStep"/> over the session's era table) — the dt the CP a turn is
    /// shown for (rp-cp-lifecycle k5); null falls back to the clock's last dt, which is wrong at turn 0 and at the first
    /// turn of each era band.</summary>
    public static ResearchSnapshot Build(IReadOnlyWorldState world, ResearchContent content, PolityId polity, double? nextDtYears = null)
    {
        int n = content.Nodes.Count;
        bool[] completed = ResearchQuery.CompletedMask(world, content, polity);
        int[] levels = ResearchQuery.LevelMask(world, content, polity, completed);
        bool stage = ResearchQuery.StageReached(content, completed);
        bool twoLanes = content.Tuning.TwoLanes;
        int? target = null, civicTarget = null;
        if (!twoLanes)
        {
            if (ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId t)) { int ti = content.IndexOf(t); if (ti >= 0) target = ti; }
        }
        else
        {
            if (ResearchQuery.TryGetTarget(world, content, polity, ResearchTree.Technology, out ResearchNodeId t)) target = content.IndexOf(t);
            if (ResearchQuery.TryGetTarget(world, content, polity, ResearchTree.Civics, out ResearchNodeId c)) civicTarget = content.IndexOf(c);
        }

        var views = new ResearchNodeView[n];
        var sweeps = new ResearchQuery.SweepState[n];
        int doneTech = 0, doneCiv = 0;
        for (int i = 0; i < n; i++)
        {
            ResearchNode node = content.Nodes[i];
            if (completed[i]) { if (node.Tree == ResearchTree.Technology) doneTech++; else doneCiv++; }
            // ADR-049: a levelled repeatable is never shown complete — it stays available for its next level.
            bool levelled = ResearchQuery.IsLevelled(content, i);
            bool done = completed[i] && !levelled;
            int level = levelled ? ResearchQuery.Level(world, polity, node.Key) : 0;
            bool available = ResearchQuery.IsAvailable(content, i, completed, stage, levels[i]);
            double progress = done ? 0.0 : ResearchQuery.Progress(world, polity, node.Key);
            bool isTarget = target == i || civicTarget == i;

            LockReason lockReason = LockReason.None;
            if (!done && !available)
            {
                if (!ResearchQuery.PrerequisitesMet(content, i, completed)) lockReason |= LockReason.MissingPrerequisites;
                if (node.Branch >= 0 && !stage) lockReason |= LockReason.ResearchStage;
                if (node.IsRecursive && node.Branch >= 0 && !ResearchQuery.SubtreeExhausted(content, node.Branch, completed))
                    lockReason |= LockReason.SubtreeNotExhausted;
                if (node.IsRecursive && node.Tree == ResearchTree.Civics && !ResearchQuery.FinalAgeCivicsExhausted(content, completed))
                    lockReason |= LockReason.FinalAgeCivicsNotExhausted;
                if (levelled && level >= ResearchQuery.MaxRepeatableLevel) lockReason |= LockReason.MaxLevel;
            }
            // ADR-048: the engine's sweep predicate, reported on its own (never as a missing prerequisite).
            sweeps[i] = ResearchQuery.SweepBlock(content, completed, i);
            if (!done && sweeps[i].Blocked) lockReason |= LockReason.AgeSweep;
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
            RepeatView? repeat = null;
            if (levelled && node.Levels is { } rule)
            {
                var effects = new List<RepeatEffectView>();
                foreach (RepeatableLevelEffect e in rule.Effects)
                {
                    RepeatableOperand op = RepeatableOperands.Get(e.Operand);
                    effects.Add(new RepeatEffectView(e.Operand, op.Meaning, op.Higher, op.Cap, e.GapFraction,
                        ResearchQuery.RepeatableLevelBenefit(level + 1, e.GapFraction, op.Cap),
                        ResearchQuery.RepeatableContribution(level, e.GapFraction, op.Cap)));
                }
                repeat = new RepeatView(level, ResearchQuery.MaxRepeatableLevel, rule.CostGrowth, cost.EffectiveCost, effects);
            }
            var unlocks = new List<string>();
            foreach (int ent in node.UnlockedEntities) unlocks.Add(content.Entities[ent].Name ?? content.Entities[ent].Id);

            views[i] = new ResearchNodeView(i, node.Key, node.Name, node.Description, node.Age, node.Tree, node.Branch,
                node.Branch >= 0 ? content.Branches[node.Branch].Name : node.Tree == ResearchTree.Technology ? "Main Trunk" : "Civics",
                node.Domain, state, lockReason, available, isTarget, progress, cost.BaseCost, cost.EffectiveCost, cost.FloorBinds,
                terms, node.Prerequisite?.Source, prereqs, eurekas, pool.Ceiling, pool.Eureka, pool.ForeignExposure,
                pool.ExposureOffered, unis, unlocks, node.IsRecursive, KnowledgeTexts.For(node.Key), repeat);
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
            TwoLanes = twoLanes,
            CivicsTargetIndex = civicTarget,
            CivicsBalance = ResearchQuery.CivicsPointsBalance(world, polity),
            CivicsPerTurn = ResearchQuery.CivicsPointsMilli(world, content, polity, nextDtYears ?? world.Clock.DtYears) / 1000.0,
            Routing = content.Tuning.DestinationRouting,
            TechnologyRoute = content.Tuning.DestinationRouting ? ResearchQuery.Route(world, content, polity, ResearchTree.Technology) : ResearchRoute.None(ResearchTree.Technology),
            CivicsRoute = content.Tuning.DestinationRouting ? ResearchQuery.Route(world, content, polity, ResearchTree.Civics) : ResearchRoute.None(ResearchTree.Civics),
            Sweeps = sweeps,
        };
    }
}
