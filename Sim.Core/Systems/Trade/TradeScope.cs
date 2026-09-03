using Sim.Core.State;

namespace Sim.Core.Systems.Trade;

/// <summary>
/// M4-D/T4.6 — whether a trade crosses a polity boundary.
/// </summary>
public enum TradeScope
{
    /// <summary>At least one endpoint is commanded by no Empire, so the trade has
    /// no boundary to be on either side of. NOT an error: colonization founds
    /// settlements and writes no control row, so this is a reachable, legitimate
    /// state and the pair is neither domestic nor foreign.</summary>
    Unruled = 0,

    /// <summary>Both endpoints answer to the SAME Empire — internal trade.</summary>
    Domestic = 1,

    /// <summary>The endpoints answer to DIFFERENT Empires — foreign trade.</summary>
    Foreign = 2,
}

/// <summary>
/// T4.6 — FOREIGN TRADE IS A CLASSIFICATION, NOT A SECOND ECONOMY.
///
/// A trade is foreign when its two settlements answer to different Empires, and
/// that is the whole of it. The M3 machinery (D-034) already moves goods between
/// settlement-local inventories across a transport-cost DEADBAND; T4.6 adds the
/// polity dimension that machinery lacked, and adds nothing else. No second
/// pathway, no tariff, no treasury, no money, no separate foreign inventory —
/// the goods move exactly as they did.
///
/// DERIVED, NEVER STORED, and that is the load-bearing decision. Both endpoints
/// of a <see cref="TradeFlowRow"/> are already in the row, and control is already
/// in <see cref="ControlRow"/>, so the classification is a function of state that
/// exists. Persisting it would add a field, bump the canonical schema, move four
/// pinned world hashes, and create a second copy of a fact that can silently
/// disagree with the control relation it was derived from. So it is computed at
/// read time and the schema is untouched.
///
/// D-037 control remains authoritative: this asks the control relation who rules
/// a settlement and never infers ownership from anything else. There is no
/// EmpireId, no owner field, no trade-side identity.
///
/// WHAT IT IS NOT: this classifies; it does not gate, price, tax or forbid.
/// Nothing in the trade pipeline consults it yet, deliberately — the mechanisms
/// that would (tariffs, embargoes, treaties) are out of M4 scope, and a
/// classification with no consumer is the correct shape for a seam whose
/// consumers are later milestones.
/// </summary>
public static class TradeScopes
{
    /// <summary>
    /// Classify a settlement pair. Order-independent: <c>Classify(a, b)</c> and
    /// <c>Classify(b, a)</c> agree, because "crosses a boundary" is symmetric and
    /// the arbitrage system's pair loop visits each pair once in an arbitrary
    /// orientation.
    /// </summary>
    public static TradeScope Classify(IReadOnlyWorldState world, SettlementId from, SettlementId to)
    {
        if (!EmpireQuery.TryGetController(world, from, out PolityId a)) return TradeScope.Unruled;
        if (!EmpireQuery.TryGetController(world, to, out PolityId b)) return TradeScope.Unruled;
        return a.Value == b.Value ? TradeScope.Domestic : TradeScope.Foreign;
    }

    /// <summary>Classify a realised flow by its own endpoints.</summary>
    public static TradeScope Classify(IReadOnlyWorldState world, in TradeFlowRow flow)
        => Classify(world, flow.From, flow.To);

    /// <summary>
    /// How many of this turn's realised flows cross a polity boundary. A reader
    /// for reports and tests; it allocates nothing and iterates in table order.
    /// </summary>
    public static int CountForeignFlows(IReadOnlyWorldState world)
    {
        int count = 0;
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            if (Classify(world, world.TradeFlows[i]) == TradeScope.Foreign) count++;
        }

        return count;
    }
}
