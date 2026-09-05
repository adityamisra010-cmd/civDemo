using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Governance;

/// <summary>Tables owned by <see cref="GovernanceSystem"/>. `Controls` is a
/// SANCTIONED SHARED table: Colonization appends rows (inherited control),
/// Revolt removes them (lost control), and this system updates the STRENGTH of
/// the rows that exist. The three never contend for the same field.</summary>
public readonly record struct GovernanceTables(
    Table<TaxPolicyRow> TaxPolicies, Table<ControlRow> Controls);

/// <summary>
/// M5 — THE GOVERNING LOOP'S WRITER. It does exactly two things, and neither of
/// them moves a single unit of any good.
///
///  1. ENACTS POLICY. Each `SetTaxRate` order in this turn's batch upserts the
///     issuing Empire's <see cref="TaxPolicyRow"/>. A standing decision: once
///     legislated it persists until legislated again, exactly as a sector
///     allocation does.
///
///  2. COMPUTES AUTHORITY. It writes each <see cref="ControlRow.Strength"/> as
///     the settlement's ADMINISTRATIVE REACH from its Empire's capital.
///
/// WHY (2) IS THE IMPORTANT HALF. `ControlRow.Strength` shipped at T4.3 as a
/// reserved slot written as the literal 1.0 and computed by nobody — the M5
/// foundations audit records it as a §4.1 "(c) chosen, never derived" failure.
/// This is its first computer, and it is computed the way D-040 C3 requires:
/// control carries a DISTANCE TERM over the network graph, travel cost and not
/// Euclidean distance. Strength stops being decoration and becomes the thing
/// that decides how much of a declared tax the state can actually collect.
///
/// WHAT THIS SYSTEM DOES NOT DO, because the shape was ruled against. It creates
/// no treasury, no receipt and no stock; it calls no Ledger method at all,
/// because there is nothing to conserve — a tax POLICY changes how hard a realm
/// is worked and what that costs, and the goods stay exactly where they were, in
/// the settlements that produced them.
///
/// Signals are read from PREV (§3.2), so a policy enacted this turn is visible to
/// production and happiness NEXT turn — the one-turn lag every other order-driven
/// mechanism in the tree already has, and the reason this system's pipeline
/// position cannot change what any other system sees.
/// </summary>
public sealed class GovernanceSystem(SimConfig cfg) : ISimSystem<GovernanceTables>
{
    public static readonly SystemId WellKnownId = new(22);
    public const string Name = "governance";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<GovernanceTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;

        // --- 1. ENACT: orders become standing policy, in log order -----------
        Table<TaxPolicyRow> policies = ctx.Owned.TaxPolicies;
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.SetTaxRate) continue;

            // The order carries a PERCENTAGE in [0,100] (the convention every
            // other allocation order uses); policy stores a FRACTION.
            double rate = Math.Clamp(order.Amount / 100.0, 0.0, 1.0);
            var polity = new PolityId(order.TargetId);

            bool replaced = false;
            for (int i = 0; i < policies.Count; i++)
            {
                if (policies[i].Polity.Value != polity.Value) continue;
                policies[i] = new TaxPolicyRow(polity, rate);
                replaced = true;
                break;
            }

            if (!replaced) policies.Add(new TaxPolicyRow(polity, rate));
        }

        // --- 2. AUTHORITY: strength IS administrative reach ------------------
        // Recomputed every turn from Prev rather than integrated, because reach
        // is a statement about the CURRENT network and the CURRENT seat: move the
        // capital, lose it, or open a river route and the answer changes at once.
        // There is nothing here to accumulate.
        Table<ControlRow> controls = ctx.Owned.Controls;
        for (int i = 0; i < controls.Count; i++)
        {
            ControlRow row = controls[i];
            double reach = State.Governance.AdministrativeReach(prev, row.Place, _cfg);
            if (reach != row.Strength) controls[i] = row with { Strength = reach };
        }
    }
}
