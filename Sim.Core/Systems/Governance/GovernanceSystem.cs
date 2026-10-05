using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Governance;

/// <summary>Tables owned by <see cref="GovernanceSystem"/>. <c>Controls</c> is a SANCTIONED SHARED
/// table (see SystemCatalog): Colonization APPENDS rows (inherited control), Revolt REMOVES rows
/// (lost control), and this system updates only the <see cref="ControlRow.Strength"/> FIELD of the
/// rows that exist when it runs (after both, by pipeline order). The three never contend for the
/// same field.</summary>
public readonly record struct GovernanceTables(Table<TaxPolicyRow> TaxPolicies, Table<ControlRow> Controls);

/// <summary>
/// ADR-033 D4 — THE GOVERNING LOOP'S WRITER (ported from <c>m5-full-build</c>, SystemId 22, which
/// ADR-029 §4 reserved for it). It does exactly two things, and neither moves a unit of any good.
///
/// <list type="number">
/// <item><b>ENACTS POLICY.</b> Each SetTaxRate order of this turn's batch, in log order, upserts
///   the issuing Empire's <see cref="TaxPolicyRow"/> — IF the issuer can levy a tax on PREV
///   (<see cref="State.Governance.CanLevyTax"/>: the Taxation knowledge AND, since H2, the polity's current Age at
///   least sim.json governance.taxationMinAge — A3; an Age entered by this very step's AdvanceAge does not count,
///   because PREV is judged) and the order targets the
///   issuer itself. An order failing either changes nothing (the ResearchSystem precedent); the
///   LAST valid order of the turn wins. A standing decision: it persists until legislated again.
///   The percentage becomes a fraction here (Amount / 100).</item>
/// <item><b>COMPUTES AUTHORITY.</b> It rewrites every <see cref="ControlRow.Strength"/> as the
///   ADMINISTRATIVE REACH of the row's Empire at the row's place,
///   <see cref="State.Governance.AdministrativeReach"/> evaluated on PREV (capital, controls and the
///   road-aware SettlementDistances) — the one computer of the one stored reach that every
///   consumer reads.</item>
/// </list>
///
/// TIMING, pinned by tests (GovernanceTimingTests). A SetTaxRate stamped turn t writes the policy
/// row into the state of turn t+1; production and every reader of happiness read it from PREV, so
/// output, migration and revolt first respond in the step t+1 → t+2 (the derived happiness READING
/// of the state t+1 already shows the burden). A road built by the step t → t+1 enters
/// SettlementDistances in the state of t+2 (CatchmentSystem reads PREV), Strength in the state of
/// t+3, and production in the step t+3 → t+4. A colony founded by the step t → t+1 has no distance
/// row from the capital until the state of t+2, so its Strength reads 0.0 (unadministered: untaxed
/// and unburdened) in the states of t+1 and t+2 and carries its reach from the state of t+3.
///
/// INERT WITHOUT A GOVERNANCE SECTION: no tax row is applied and Strength keeps what founding and
/// colonization wrote (1.0). STATELESS; no RNG (adds no RngStreams row); no Ledger call — there is
/// nothing to conserve, because a tax POLICY changes how hard a realm is worked and what that
/// costs, and the goods stay in the settlements that produced them (CR-008: no treasury).
/// </summary>
public sealed class GovernanceSystem(SimConfig cfg) : ISimSystem<GovernanceTables>
{
    public static readonly SystemId WellKnownId = new(22);
    public const string Name = "governance";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<GovernanceTables> ctx)
    {
        if (_cfg.Governance is not { } governance) return;   // the loop is inert without its tuning
        IReadOnlyWorldState prev = ctx.Prev;

        // --- 1. ENACT: valid orders become standing policy, in log order ----
        Table<TaxPolicyRow> policies = ctx.Owned.TaxPolicies;
        for (int o = 0; o < ctx.Orders.Count; o++)
        {
            OrderRecord order = ctx.Orders[o];
            if (order.Kind != OrderKind.SetTaxRate) continue;
            if (order.TargetId != order.ActorId) continue;                       // an Empire legislates only its own rate
            if (!(order.Amount >= 0.0 && order.Amount <= 100.0)) continue;       // NaN fails this too
            if (!State.Governance.CanLevyTax(prev, _cfg, order.Actor)) continue; // knowledge + Age gate, on PREV

            PolityId polity = order.Actor;
            var row = new TaxPolicyRow(polity, order.Amount / 100.0);
            bool replaced = false;
            for (int i = 0; i < policies.Count; i++)
            {
                if (policies[i].Polity.Value != polity.Value) continue;
                policies[i] = row;
                replaced = true;
                break;
            }

            if (!replaced) policies.Add(row);
        }

        // --- 2. AUTHORITY: Strength IS administrative reach -------------------
        // Recomputed every step from PREV rather than integrated, because reach is a statement
        // about the CURRENT network and the CURRENT seat: move or lose the capital, or build a road
        // that cuts the travel cost, and the answer changes at once. Nothing accumulates.
        Table<ControlRow> controls = ctx.Owned.Controls;
        for (int i = 0; i < controls.Count; i++)
        {
            ControlRow row = controls[i];
            double reach = State.Governance.AdministrativeReach(prev, row.Polity, row.Place, governance);
            if (BitConverter.DoubleToInt64Bits(reach) != BitConverter.DoubleToInt64Bits(row.Strength))
                controls[i] = row with { Strength = reach };
        }
    }
}
