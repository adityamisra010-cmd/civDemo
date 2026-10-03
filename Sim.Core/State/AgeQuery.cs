using Sim.Core.Kernel;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>The live status of one milestone: whether it holds, and the observed quantity
/// against its threshold (for a research fact: listed nodes completed against 1).</summary>
public sealed record MilestoneStatus(AgeMilestone Milestone, bool Met, long Observed, long Threshold);

/// <summary>
/// ADR-031 — the full evaluation of a polity's entry requirements for its NEXT Age
/// (D-047 ruling 12: mandatory core, supporting count, category coverage).
/// <c>Remaining</c> lists, in plain words, what is still missing; empty when eligible.
/// </summary>
public sealed record AgeEligibilityReport(
    int CurrentAge, int NextAge,
    IReadOnlyList<MilestoneStatus> Core, IReadOnlyList<MilestoneStatus> Supporting,
    int CoreMet, int CoreTotal, int SupportingMet, int SupportingRequired,
    IReadOnlyList<AgeMilestoneCategory> CategoriesCovered, int CategoryMask, int CategoriesRequired,
    bool Eligible, IReadOnlyList<string> Remaining);

/// <summary>A polity's Age, its chosen surge, and its next-Age eligibility (null at the final Age).</summary>
public sealed record AgeStatus(
    PolityId Polity, AgeDefinition Current, AgeDefinition? Next, long EnteredTurn,
    AgeSurge? Surge, long SurgeStartTurn, AgeEligibilityReport? Eligibility);

/// <summary>An AdvanceAge decision queued for the current turn: the next turn begins in
/// <c>ToAge</c> (EffectiveTurn = DecisionTurn + 1).</summary>
public sealed record PendingAgeAdvance(
    PolityId Polity, int FromAge, int ToAge, AgeSurge Surge, long DecisionTurn, long EffectiveTurn);

/// <summary>Why an AdvanceAge order would change nothing.</summary>
public enum AdvanceRejection
{
    None = 0,
    UnknownPolity = 1,
    AtFinalAge = 2,
    WrongTargetAge = 3,
    UnknownSurge = 4,
    NotEligible = 5,
}

/// <summary>
/// ADR-031 — THE Age read seam (the ResearchQuery precedent). Pure, read-only queries
/// over <see cref="IReadOnlyWorldState"/> and the loaded <see cref="AgeContent"/>.
/// <b>AgeEligibilitySystem and AgeTransitionSystem compute with these same statics</b>,
/// so the eligibility the UI shows is the eligibility the simulation applies.
///
/// THE THIN EVALUATOR (D-047 ruling 12; ledger §2.3). Each milestone fact reads ONE
/// table an owning system publishes (research completion, buckets, controls, structures,
/// stocks, classes, trade flows, housing, formations); this class only combines them by
/// the content's core / supporting / coverage rule. It owns no state, applies no effect,
/// and is not a progression system.
///
/// AGE IS COMPUTED STATE (law 4): nothing here reads a date, an era band or the turn
/// number to decide anything — the turn appears only as a stamp on records.
/// Iteration is over table indices and content order; no dictionary, no LINQ (law 5).
/// The UI reads through here and never writes; advancing goes through the AdvanceAge
/// order (ledger §2.4), built by <see cref="AdvanceOrder"/>.
/// </summary>
public static class AgeQuery
{
    // ------------------------------------------------------------------ state

    /// <summary>The polity's Age row, or null when it has never advanced (it is then in the
    /// founding Age).</summary>
    public static AgeStateRow? StateRow(IReadOnlyWorldState world, PolityId polity)
    {
        for (int i = 0; i < world.AgeStates.Count; i++)
            if (world.AgeStates[i].Polity.Value == polity.Value) return world.AgeStates[i];
        return null;
    }

    /// <summary>The polity's current Age (1..9). Absence of a row is the founding Age.</summary>
    public static int CurrentAge(IReadOnlyWorldState world, AgeContent content, PolityId polity) =>
        StateRow(world, polity) is { } row ? row.Age : content.FoundingAge;

    /// <summary>The Age after the current one, or null at the final Age.</summary>
    public static AgeDefinition? NextAge(IReadOnlyWorldState world, AgeContent content, PolityId polity)
    {
        int current = CurrentAge(world, content, polity);
        return current < AgeContent.AgeCount ? content.Age(current + 1) : null;
    }

    /// <summary>Everything the Age panel shows for one polity.</summary>
    public static AgeStatus Status(IReadOnlyWorldState world, AgeContent content, PolityId polity)
    {
        int current = CurrentAge(world, content, polity);
        AgeStateRow? row = StateRow(world, polity);
        return new AgeStatus(
            polity, content.Age(current), NextAge(world, content, polity),
            row?.EnteredTurn ?? 0, row is { } r ? content.SurgeByKey(r.Surge) : null, row?.SurgeStartTurn ?? 0,
            Evaluate(world, content, polity));
    }

    /// <summary>The surge emphases the advancing polity may choose, key ascending.</summary>
    public static IReadOnlyList<AgeSurge> SurgeOptions(AgeContent content) => content.Surges;

    /// <summary>The polity's Age-transition history, oldest first.</summary>
    public static AgeTransitionRow[] Transitions(IReadOnlyWorldState world, PolityId polity)
    {
        var result = new List<AgeTransitionRow>();
        for (int i = 0; i < world.AgeTransitions.Count; i++)
            if (world.AgeTransitions[i].Polity.Value == polity.Value) result.Add(world.AgeTransitions[i]);
        return [.. result];
    }

    // ------------------------------------------------------------------ the evaluator

    /// <summary>Evaluates the polity's entry requirements for its next Age over the published
    /// state of <paramref name="world"/>. Null at the final Age.</summary>
    public static AgeEligibilityReport? Evaluate(IReadOnlyWorldState world, AgeContent content, PolityId polity) =>
        EvaluateFor(world, content, polity, CurrentAge(world, content, polity));

    /// <summary>As <see cref="Evaluate"/>, for a stated current Age (the systems use this to
    /// account for an advance applied in the same step).</summary>
    public static AgeEligibilityReport? EvaluateFor(IReadOnlyWorldState world, AgeContent content, PolityId polity, int currentAge)
    {
        if (currentAge >= AgeContent.AgeCount) return null;
        AgeEntryRequirements entry = content.Age(currentAge + 1).Entry
            ?? throw new InvalidOperationException($"Age {currentAge + 1} declares no entry requirements");

        var core = new MilestoneStatus[entry.Core.Count];
        int coreMet = 0;
        for (int i = 0; i < core.Length; i++)
        {
            core[i] = Milestone(world, polity, entry.Core[i]);
            if (core[i].Met) coreMet++;
        }
        var supporting = new MilestoneStatus[entry.Supporting.Count];
        int supportingMet = 0, mask = 0;
        for (int i = 0; i < supporting.Length; i++)
        {
            supporting[i] = Milestone(world, polity, entry.Supporting[i]);
            if (!supporting[i].Met) continue;
            supportingMet++;
            mask |= 1 << ((int)entry.Supporting[i].Category - 1);
        }
        var covered = new List<AgeMilestoneCategory>();
        for (int k = 1; k <= 5; k++) if ((mask & (1 << (k - 1))) != 0) covered.Add((AgeMilestoneCategory)k);

        var remaining = new List<string>();
        for (int i = 0; i < core.Length; i++)
            if (!core[i].Met) remaining.Add($"Core: {core[i].Milestone.Name}");
        if (supportingMet < entry.SupportingRequired)
            remaining.Add($"Supporting milestones: {supportingMet} of {entry.SupportingRequired} required");
        if (covered.Count < entry.MinCategories)
            remaining.Add($"Category coverage: {covered.Count} of {entry.MinCategories} categories");

        bool eligible = coreMet == core.Length && supportingMet >= entry.SupportingRequired && covered.Count >= entry.MinCategories;
        return new AgeEligibilityReport(
            currentAge, currentAge + 1, core, supporting, coreMet, core.Length, supportingMet, entry.SupportingRequired,
            covered, mask, entry.MinCategories, eligible, remaining);
    }

    /// <summary>Whether the polity may advance now (its next Age's requirements hold).</summary>
    public static bool IsEligible(IReadOnlyWorldState world, AgeContent content, PolityId polity) =>
        Evaluate(world, content, polity) is { Eligible: true };

    /// <summary>One milestone's live status over published state.</summary>
    public static MilestoneStatus Milestone(IReadOnlyWorldState world, PolityId polity, AgeMilestone milestone)
    {
        AgeMilestoneFact fact = milestone.Fact;
        long observed = Observe(world, polity, fact);
        return new MilestoneStatus(milestone, observed >= fact.Min, observed, fact.Min);
    }

    /// <summary>The quantity a fact measures for the polity — the ONLY place facts are read.
    /// Settlement-scoped facts count each settlement once, credited to its lowest-id controller
    /// (EmpireQuery.TryGetController), the ResearchQuery.Population convention.</summary>
    public static long Observe(IReadOnlyWorldState world, PolityId polity, AgeMilestoneFact fact)
    {
        switch (fact.Kind)
        {
            case MilestoneFactKind.Research:
            {
                // Completed-knowledge rows of this polity, matched against the listed node keys.
                long count = 0;
                for (int n = 0; n < fact.NodeKeys.Count; n++)
                    if (ResearchQuery.IsCompleted(world, polity, new ResearchNodeId(fact.NodeKeys[n]))) count++;
                return count;
            }
            case MilestoneFactKind.ResearchCount:
            {
                long count = 0;
                for (int i = 0; i < world.ResearchCompleted.Count; i++)
                {
                    ResearchCompletedRow row = world.ResearchCompleted[i];
                    if (row.Polity.Value != polity.Value) continue;
                    if (Array.BinarySearch(fact.TreeKeys, row.Node.Value) >= 0) count++;
                }
                return count;
            }
            case MilestoneFactKind.Population:
                return ResearchQuery.Population(world, polity);
            case MilestoneFactKind.Settlements:
            {
                long count = 0;
                for (int s = 0; s < world.Settlements.Count; s++)
                    if (Controls(world, polity, world.Settlements[s].Id)) count++;
                return count;
            }
            case MilestoneFactKind.Structures:
            {
                long count = 0;
                checked
                {
                    for (int i = 0; i < world.Structures.Count; i++)
                    {
                        StructureRow row = world.Structures[i];
                        if ((fact.Ref < 0 || row.ProjectId == fact.Ref) && Controls(world, polity, row.Settlement)) count += row.Count;
                    }
                }
                return count;
            }
            case MilestoneFactKind.GoodStock:
            {
                long total = 0;
                checked
                {
                    for (int i = 0; i < world.GoodStocks.Count; i++)
                    {
                        GoodStockRow row = world.GoodStocks[i];
                        if (row.Good.Value == fact.Ref && Controls(world, polity, row.Settlement)) total += row.Amount.Value;
                    }
                }
                return total;
            }
            case MilestoneFactKind.ClassActive:
            {
                long count = 0;
                for (int i = 0; i < world.ClassStates.Count; i++)
                {
                    ClassStateRow row = world.ClassStates[i];
                    if (row.Class.Value == fact.Ref && row.Active != 0 && Controls(world, polity, row.Settlement)) count++;
                }
                return count;
            }
            case MilestoneFactKind.TradeVolume:
            {
                long total = 0;
                checked
                {
                    for (int i = 0; i < world.TradeFlows.Count; i++)
                    {
                        TradeFlowRow row = world.TradeFlows[i];
                        if (Controls(world, polity, row.From) || Controls(world, polity, row.To)) total += row.Quantity;
                    }
                }
                return total;
            }
            case MilestoneFactKind.Dwellings:
            {
                long total = 0;
                checked
                {
                    for (int i = 0; i < world.Housing.Count; i++)
                        if (Controls(world, polity, world.Housing[i].Settlement)) total += world.Housing[i].Dwellings.Value;
                }
                return total;
            }
            case MilestoneFactKind.Formations:
            {
                long count = 0;
                for (int i = 0; i < world.MilitaryUnits.Count; i++)
                {
                    MilitaryUnitRow unit = world.MilitaryUnits[i];
                    if (unit.Owner.Value != polity.Value || (fact.Ref >= 0 && unit.Family != fact.Ref)) continue;
                    // M5 R2b: only formations whose CURRENT identity is realized at or after the fact's Age
                    // count — the founding line, converted for free at each entry, is never new realization.
                    if (fact.IdentityKeys is { } keys && Array.BinarySearch(keys, unit.Identity) < 0) continue;
                    count++;
                }
                return count;
            }
            default:
                throw new InvalidOperationException($"unknown milestone fact kind {fact.Kind}");
        }
    }

    // ------------------------------------------------------------------ advancing (the order pathway)

    /// <summary>Builds the AdvanceAge order the UI dispatches and the AI policy issues: stamped with the
    /// current turn, TargetId = the next Age, Amount = the surge key (ledger §2.4: one order pathway).</summary>
    public static OrderRecord AdvanceOrder(IReadOnlyWorldState world, PolityId polity, int toAge, int surgeKey) =>
        OrderRecord.From(world.Clock.Turn, polity, OrderKind.AdvanceAge, toAge, surgeKey);

    /// <summary>Whether an AdvanceAge to <paramref name="toAge"/> with <paramref name="surgeKey"/>
    /// would be applied if issued against <paramref name="world"/>, and if not, why.</summary>
    public static AdvanceRejection CheckAdvance(
        IReadOnlyWorldState world, AgeContent content, PolityId polity, int toAge, int surgeKey)
    {
        if (!EmpireQuery.TryGetCommandSource(world, polity, out _)) return AdvanceRejection.UnknownPolity;
        int current = CurrentAge(world, content, polity);
        if (current >= AgeContent.AgeCount) return AdvanceRejection.AtFinalAge;
        if (toAge != current + 1) return AdvanceRejection.WrongTargetAge;
        if (content.SurgeByKey(surgeKey) is null) return AdvanceRejection.UnknownSurge;
        if (!IsEligible(world, content, polity)) return AdvanceRejection.NotEligible;
        return AdvanceRejection.None;
    }

    /// <summary>As <see cref="CheckAdvance"/> for an order record (Amount must carry a whole surge key).</summary>
    public static AdvanceRejection CheckAdvance(IReadOnlyWorldState world, AgeContent content, in OrderRecord order)
    {
        if (order.Kind != OrderKind.AdvanceAge) throw new ArgumentException("not an AdvanceAge order", nameof(order));
        int surge = order.Amount >= 1.0 && order.Amount <= int.MaxValue && order.Amount == Math.Floor(order.Amount)
            ? (int)order.Amount : -1;
        return CheckAdvance(world, content, order.Actor, order.TargetId, surge);
    }

    /// <summary>
    /// The AdvanceAge decision queued for the CURRENT turn of <paramref name="world"/> among
    /// <paramref name="queued"/> (the UI's not-yet-stepped orders), or null. The FIRST valid order of
    /// the polity stamped with the current turn wins, in list order — exactly the rule the transition
    /// system applies, so what the UI calls pending is what the next step applies.
    /// </summary>
    public static PendingAgeAdvance? PendingAdvance(
        IReadOnlyWorldState world, AgeContent content, IReadOnlyList<OrderRecord> queued, PolityId polity)
    {
        for (int i = 0; i < queued.Count; i++)
        {
            OrderRecord order = queued[i];
            if (order.Kind != OrderKind.AdvanceAge || order.ActorId != polity.Value || order.Turn != world.Clock.Turn) continue;
            if (CheckAdvance(world, content, order) != AdvanceRejection.None) continue;
            return new PendingAgeAdvance(polity, order.TargetId - 1, order.TargetId,
                content.SurgeByKey((int)order.Amount)!, order.Turn, order.Turn + 1);
        }
        return null;
    }

    /// <summary>The index of the order in <paramref name="batch"/> that advances <paramref name="polity"/>
    /// this step, or -1. The first VALID AdvanceAge order of the polity, in log order, wins; any later
    /// one targets an Age that is no longer next and changes nothing. Shared by both Age systems so
    /// they agree on the polity's Age at the end of the step.</summary>
    public static int ResolveAdvance(IReadOnlyWorldState prev, AgeContent content, OrderBatch batch, PolityId polity)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            OrderRecord order = batch[i];
            if (order.Kind != OrderKind.AdvanceAge || order.ActorId != polity.Value) continue;
            if (CheckAdvance(prev, content, order) == AdvanceRejection.None) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------ helpers

    private static bool Controls(IReadOnlyWorldState world, PolityId polity, SettlementId place) =>
        EmpireQuery.TryGetController(world, place, out PolityId controller) && controller.Value == polity.Value;
}
