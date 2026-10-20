using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Ages;

/// <summary>AgeTransitionSystem's owned tables (ADR-003 ownership by construction).</summary>
public sealed record AgeTransitionTables(
    Table<AgeStateRow> States,
    Table<AgeTransitionRow> Transitions,
    Table<MilitaryUnitRow> Units,
    Table<UnitConversionRow> Conversions);

/// <summary>
/// ADR-031 — AGE ADVANCEMENT AND ITS RATIFIED CONSEQUENCES (D-047 rulings 13, 14, 18).
///
/// <list type="number">
/// <item><b>Explicit decision only.</b> A polity changes Age only through an AdvanceAge
///   order (player or AI, one order pathway — ledger §2.4). Eligibility alone never
///   advances anyone: a polity may stay eligible indefinitely (ruling 13).</item>
/// <item><b>Validation on PREV.</b> The order must name the polity's NEXT Age, a known surge
///   emphasis, and the polity must be eligible in the state the order was issued against
///   (<see cref="AgeQuery.CheckAdvance(IReadOnlyWorldState, AgeContent, in OrderRecord)"/>,
///   the function the UI calls). The first valid order of the polity in log order wins;
///   anything else changes nothing (the ResearchSystem consumption-time precedent).</item>
/// <item><b>Next-turn effect.</b> An order stamped turn t is delivered to the step t → t+1.
///   Every other system of that step reads PREV, so turn t is resolved entirely under the
///   old Age — nothing is retroactive. The new Age is written into NEXT, so the state of turn
///   t+1 is the first under the new Age (EnteredTurn = t+1): "the next turn begins under the
///   new Age".</item>
/// <item><b>Surge.</b> The chosen emphasis and its start turn (t+1) are stored on the Age row.
///   No numeric effect is applied anywhere (ledger §10.5/§27 defer the formula; D-043
///   A7/DD-13 forbid a generic Age modifier table).</item>
/// <item><b>Modernization.</b> In the same step, every formation the polity owns is converted
///   by <see cref="MilitaryQuery.PlanFor"/> (automatic, free — ruling 18): the family's
///   realization at the new Age, or preserved when none exists, or the family's explicit
///   generic successor when its line has ended. Id, owner, location, position, experience and
///   army membership are kept. Every outcome — preserved ones too — is logged.</item>
/// </list>
///
/// WHY ONE SYSTEM OWNS BOTH AGE AND FORMATIONS: modernization is a consequence of the Age
/// transition the Director ruled into the same instant (ruling 13: the click "triggers the
/// appropriate automatic unit modernization"). Splitting it into a second system that read
/// PREV AgeStates would modernize one turn LATE. No other writer of MilitaryUnits exists;
/// when recruitment or movement arrives its ownership is re-split under a reviewed record
/// (the SystemCatalog shared-stock precedent). Formations hold no people and no goods, so the
/// free conversion moves no conserved stock (law 1; CR-017 §4).
///
/// Irreversible (ledger §10.1): an order can only name current + 1. No dt (a discrete event,
/// law 3 does not apply); no calendar input (law 4); no RNG; no Ledger. Inert without Age
/// content.
/// </summary>
public sealed class AgeTransitionSystem(AgeContent? ages, UnitFamilyContent? families) : ISimSystem<AgeTransitionTables>
{
    /// <summary>SystemId 26.</summary>
    public static readonly SystemId WellKnownId = new(26);
    public const string Name = "agetransition";

    public SystemId Id => WellKnownId;

    public void Step(SimContext<AgeTransitionTables> ctx)
    {
        if (ages is null) return;
        IReadOnlyWorldState prev = ctx.Prev;
        AgeTransitionTables owned = ctx.Owned;
        long decisionTurn = prev.Clock.Turn;
        long effectiveTurn = decisionTurn + 1;
        var processed = new List<int>();

        for (int pi = 0; pi < prev.Polities.Count; pi++)
        {
            PolityId polity = prev.Polities[pi].Id;
            if (processed.Contains(polity.Value)) continue;
            processed.Add(polity.Value);

            int index = AgeQuery.ResolveAdvance(prev, ages, ctx.Orders, polity);
            if (index < 0) continue;
            OrderRecord order = ctx.Orders[index];
            int fromAge = AgeQuery.CurrentAge(prev, ages, polity);
            int toAge = order.TargetId;
            int surge = (int)order.Amount;

            var row = new AgeStateRow(polity, toAge, effectiveTurn, surge, effectiveTurn);
            int at = -1;
            for (int r = 0; r < owned.States.Count; r++)
                if (owned.States[r].Polity.Value == polity.Value) { at = r; break; }
            if (at >= 0) owned.States[at] = row;
            else owned.States.Add(row);
            owned.Transitions.Add(new AgeTransitionRow(polity, fromAge, toAge, surge, decisionTurn, effectiveTurn));

            if (families is null) continue;
            for (int u = 0; u < owned.Units.Count; u++)
            {
                MilitaryUnitRow unit = owned.Units[u];
                if (unit.Owner.Value != polity.Value) continue;
                UnitConversionPlan plan = MilitaryQuery.PlanFor(families, unit, toAge);
                owned.Conversions.Add(new UnitConversionRow(
                    effectiveTurn, unit.Id, polity, plan.FromFamily, plan.FromIdentity, plan.ToFamily, plan.ToIdentity,
                    fromAge, toAge, (int)plan.Outcome));
                if (plan.Changes)
                    owned.Units[u] = unit with { Family = plan.ToFamily, Identity = plan.ToIdentity };
            }
        }
    }
}
