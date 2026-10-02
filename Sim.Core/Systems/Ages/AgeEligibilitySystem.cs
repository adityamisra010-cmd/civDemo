using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Ages;

/// <summary>AgeEligibilitySystem's owned table (ADR-003 ownership by construction).</summary>
public sealed record AgeEligibilityTables(Table<AgeEligibilityRow> Eligibility);

/// <summary>
/// ADR-031 — THE THIN AGE EVALUATOR (D-047 ruling 12; AGE-MS-1). Each step it publishes,
/// for every roster polity below the last Age, whether that polity's entry requirements
/// for its next Age hold: mandatory core milestones, the supporting count, and category
/// coverage. The facts are those the owning systems published in PREV; the combination
/// rule is <see cref="AgeQuery.EvaluateFor"/>, the same static the UI calls.
///
/// It is NOT a progression system (ledger §2.3): it applies no effect, decides nothing,
/// advances nobody and owns nothing but its published summary. Eligibility makes a polity
/// ABLE to advance; only an AdvanceAge order advances it (D-047 ruling 13).
///
/// The Age it evaluates against is the polity's Age at the END of this step — an advance
/// AgeTransitionSystem applies this step is accounted for through the shared
/// <see cref="AgeQuery.ResolveAdvance"/> (no system reference; law 6), so the published row
/// never describes an Age the polity has already entered.
///
/// Stateless; no RNG; no Ledger; no dt (eligibility is a predicate over state, law 3 does not
/// apply). Inert without Age content (a toy world or a config loaded without ages.json).
/// </summary>
public sealed class AgeEligibilitySystem(AgeContent? content) : ISimSystem<AgeEligibilityTables>
{
    /// <summary>SystemId 25. 17, 19 and 22 stay reserved for unmerged branches (ADR-029 §3).</summary>
    public static readonly SystemId WellKnownId = new(25);
    public const string Name = "ageeligibility";

    public SystemId Id => WellKnownId;

    public void Step(SimContext<AgeEligibilityTables> ctx)
    {
        if (content is null) return;
        IReadOnlyWorldState prev = ctx.Prev;
        Table<AgeEligibilityRow> owned = ctx.Owned.Eligibility;
        owned.Clear();
        var processed = new List<int>();
        for (int pi = 0; pi < prev.Polities.Count; pi++)
        {
            PolityId polity = prev.Polities[pi].Id;
            if (processed.Contains(polity.Value)) continue; // a doubled roster row is one Empire
            processed.Add(polity.Value);

            int age = AgeQuery.CurrentAge(prev, content, polity);
            if (AgeQuery.ResolveAdvance(prev, content, ctx.Orders, polity) >= 0) age++;
            if (AgeQuery.EvaluateFor(prev, content, polity, age) is not { } report) continue;
            owned.Add(new AgeEligibilityRow(
                polity, report.NextAge, prev.Clock.Turn, report.CoreMet, report.CoreTotal,
                report.SupportingMet, report.SupportingRequired, report.CategoryMask, report.CategoriesRequired,
                report.Eligible));
        }
    }
}
