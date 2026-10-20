using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Governance;

/// <summary>
/// ADR-033 D4 — THE AI TAX VALVE (ported from <c>m5-full-build</c>; D-021 valve 6, "the state
/// acts by default": player inaction is not world inaction, so an AI Empire answers its own
/// legitimacy with its own competence). A PURE POLICY: not a system, no state written. It returns
/// SetTaxRate ORDERS for ONE AI polity, built by <see cref="State.Governance.TaxOrder"/> — the very
/// constructor the player's edict uses — so the AI's tax goes through OrderValidation and
/// GovernanceSystem exactly as the player's does, and an AI run replays from its log (the
/// AgeAdvancePolicy / RoadDevelopmentPolicy shape). There is no AI-only verb and no AI-only write.
///
/// NOT WIRED HERE (ADR-033 D5): the single deterministic AI order producer (stream S2's
/// <c>AiOrders</c>) calls <see cref="OrdersFor"/> per AI polity; this port deliberately does not
/// wire it into UiSession or the CLI.
///
/// THE RULE, stated so nothing is hidden (constants are sim.json <c>governance.ai</c>, no longer
/// code literals): an AI-commanded, non-extinct polity that CAN levy a tax
/// (<see cref="State.Governance.CanLevyTax"/>, the same knowledge + Age gate the player meets — H2: no AI levy
/// before A3) moves its
/// declared rate UP by stepPercent while legitimacy ≥ comfortableLegitimacy, DOWN by stepPercent
/// while legitimacy &lt; troubledLegitimacy, holds in the dead band between, and clamps to
/// [0, maxRatePercent]. It speaks only when the rate would change, so a settled world emits nothing.
/// One candidate order per polity, a fixed rule over two thresholds — no argmax over scores, so no
/// tie-break is needed. Reads only what the player could read (its own legitimacy and policy).
/// </summary>
public static class AiGovernance
{
    /// <summary>
    /// The SetTaxRate orders <paramref name="polity"/> issues this turn, stamped
    /// <paramref name="turn"/>: empty, or exactly one. Empty for a player-commanded or unregistered
    /// polity (the human decides for themselves), an extinct one (nothing to govern), a config
    /// without a governance section, a polity that cannot levy a tax yet, and a polity already at the
    /// rate it wants.
    /// </summary>
    public static OrderRecord[] OrdersFor(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, long turn)
    {
        if (cfg.Governance is not { } governance) return [];
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return [];
        if (EmpireQuery.IsExtinct(world, polity)) return [];
        if (!State.Governance.CanLevyTax(world, cfg, polity)) return [];

        double current = State.Governance.NominalTaxRate(world, polity) * 100.0;
        double target = TargetRatePercent(State.Governance.Legitimacy(world, polity, cfg), current, governance.Ai);

        // Only speak when it changes something; both sides come from the same arithmetic.
        if (target == current) return [];
        return [State.Governance.TaxOrder(turn, polity, target)];
    }

    /// <summary>
    /// The rate the valve wants next, in percent, given the realm's legitimacy and the current
    /// declared rate: up a step while comfortable, down a step while troubled, unchanged in the dead
    /// band; clamped to [0, maxRatePercent]. Pure arithmetic over the config's four constants.
    /// </summary>
    public static double TargetRatePercent(double legitimacy, double currentPercent, GovernanceAiConfig ai)
    {
        ArgumentNullException.ThrowIfNull(ai);
        double next = currentPercent;
        if (legitimacy >= ai.ComfortableLegitimacy) next = currentPercent + ai.StepPercent;
        else if (legitimacy < ai.TroubledLegitimacy) next = currentPercent - ai.StepPercent;
        return Math.Clamp(next, 0.0, ai.MaxRatePercent);
    }
}
