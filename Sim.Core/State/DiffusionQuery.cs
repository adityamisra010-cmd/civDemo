using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-058 (D-066 A7) — THE CONTACT-CARRIED EXPOSURE READ SEAM. Pure, read-only; DiffusionSystem computes with these
/// statics and the Glass Box may call the same ones. No RNG, no dictionary, no LINQ, no ordering over doubles (law 5).
///
/// <para><b>The carrier.</b> A foreign trade flow: a <see cref="TradeFlowRow"/> of the world (TradeArbitrageSystem's
/// observable of the step that produced it) between a settlement the receiver CONTROLS and a settlement it does not
/// (another Empire's, or a city state's). Contact is binary per partner: the quantity moved is not read.</para>
///
/// <para><b>No information leak.</b> The partner counts only when the receiver KNOWS that settlement through THE
/// information-class seam (<see cref="PolityKnowledge.Of(IReadOnlyWorldState, PolityId, SimConfig?)"/>.KnowsSettlement:
/// controlled, visible, or — fog on — sighted). With the fog off every settlement is known (the pre-M6 answer). A polity
/// is never exposed to knowledge carried from a settlement it does not know.</para>
///
/// <para><b>What is carried.</b> The partner settlement's knowledge (<see cref="SettlementKnowledge.MaskOf"/>: its
/// controller's completed nodes, or a city state's own): what the people met in that market know.</para>
/// </summary>
public static class DiffusionQuery
{
    /// <summary>Whether the exposure writer is on: research content with tuning.foreignExposure.enabled.</summary>
    public static bool Enabled(ResearchContent? content) => content?.Tuning.ForeignExposure is { Enabled: true };

    /// <summary>The settlements whose knowledge reaches <paramref name="receiver"/> through a foreign trade flow of
    /// <paramref name="world"/> it knows about — distinct, ascending by settlement id.</summary>
    public static SettlementId[] ContactPartners(IReadOnlyWorldState world, SimConfig? cfg, PolityId receiver)
    {
        if (world.TradeFlows.Count == 0) return [];
        PolityKnowledge knowledge = PolityKnowledge.Of(world, receiver, cfg);
        var partners = new List<int>();
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow flow = world.TradeFlows[i];
            if (flow.Quantity <= 0) continue;
            Consider(world, knowledge, receiver, flow.From, flow.To, partners);
            Consider(world, knowledge, receiver, flow.To, flow.From, partners);
        }
        partners.Sort();
        var result = new SettlementId[partners.Count];
        for (int i = 0; i < result.Length; i++) result[i] = new SettlementId(partners[i]);
        return result;
    }

    private static void Consider(IReadOnlyWorldState world, PolityKnowledge knowledge, PolityId receiver,
        SettlementId own, SettlementId partner, List<int> partners)
    {
        if (partners.Contains(partner.Value)) return;
        if (!EmpireQuery.ControlsSettlement(world, receiver, own)) return;
        if (EmpireQuery.ControlsSettlement(world, receiver, partner)) return;   // domestic trade carries nothing new
        if (!knowledge.KnowsSettlement(partner)) return;                       // no exposure from what it does not know
        partners.Add(partner.Value);
    }

    /// <summary>The union of the contact partners' knowledge (indexed by content node), or null when the receiver has no
    /// contact.</summary>
    public static bool[]? ContactKnowledge(IReadOnlyWorldState world, ResearchContent content, SimConfig? cfg, PolityId receiver)
    {
        SettlementId[] partners = ContactPartners(world, cfg, receiver);
        if (partners.Length == 0) return null;
        var known = new bool[content.Nodes.Count];
        foreach (SettlementId partner in partners)
        {
            bool[] mask = SettlementKnowledge.MaskOf(world, content, partner) ?? ResearchQuery.CompletedMask(world, content,
                SettlementKnowledge.HolderOf(world, partner));
            for (int i = 0; i < known.Length; i++) known[i] |= mask[i];
        }
        return known;
    }

    /// <summary>
    /// THE OFFER RULE: the cumulative offer after one step of contact — <paramref name="offered"/> raised by
    /// ratePerYear × dtYears × BaseCost (law 3), capped at the acceleration pool's ceiling (ceilingFraction × BaseCost) —
    /// and NEVER lowered (an offer above the cap, written by hand, is kept as it is). Monotone by construction.
    /// </summary>
    public static double NextOffered(double offered, double baseCost, double ceilingFraction, double ratePerYear, double dtYears)
    {
        double cap = ceilingFraction * baseCost;
        double raised = Math.Min(cap, offered + ratePerYear * dtYears * baseCost);
        return Math.Max(offered, raised);
    }
}
