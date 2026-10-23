using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Colonization;

namespace Sim.Core.Systems.Discovery;

/// <summary>
/// M6 Track EX (D-062 D8; ADR-044 §9) — THE AI'S TWO DISCOVERY LANES, through the player's verbs and the player's
/// information class: every input is the polity's own <see cref="PolityKnowledge"/>, its own settlements' books
/// (<see cref="PolityKnowledge.ReadsInternals"/>) and its own stocks; each order is the one the action surface would issue,
/// and it is issued only when THE predicate DiscoverySystem applies admits it now (DiscoveryQuery.CheckProspect /
/// CheckExplore), so the AI never pays for a refusal.
/// <list type="bullet">
/// <item><b>Prospect</b> (OrderKind 15) — at most one per polity per turn, by the assay where it is open and affordable
///   and the survey otherwise (ADR-046): among its own settlements the predicate admits
///   (controlled, not yet surveyed, affordable), the argmin of the composite key (−knownEligible, settlementId) —
///   knownEligible = the concealable deposit goods the polity can SEE at the settlement (known abundance &gt; 0). The
///   contract's "most eligible rows" is read through the controller's knowledge: the raw eligible count includes concealed
///   rows the polity cannot see, which would be a leak (ADR-044 §9).</item>
/// <item><b>Explore</b> (OrderKind 14) — when the founding policy is due and has a source but finds NO KNOWN candidate site
///   (<see cref="WantsSites"/> and AiFoundingPolicy.Decide returned nothing): the argmin of (explored radius, settlementId)
///   over its settlements the predicate admits.</item>
/// </list>
/// Integer keys with a stable integer tie-break; no RNG, no dictionary, no LINQ (law 5).
/// </summary>
public static class AiDiscoveryPolicy
{
    /// <summary>The Prospect order for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Prospect(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (!DepositConcealment.Enabled(cfg)) return null;
        bool assay = DiscoveryQuery.AssayUnlocked(world, cfg, polity);   // once per polity (the requirement is parsed once)
        SettlementId? best = null;
        int bestCount = -1;
        ProspectMethod bestMethod = ProspectMethod.Survey;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementId s = world.Settlements[i].Id;
            // CheckProspect admits only a settlement the polity CONTROLS — its own books (PolityKnowledge.ReadsInternals).
            // ADR-046: the assay when it is open and affordable here (it spends bronze and less of the bulk goods), else the
            // survey; neither — no order at this settlement.
            ProspectMethod method;
            if (assay && DiscoveryQuery.CheckProspect(world, cfg, polity, s, ProspectMethod.Assay, true) == ProspectVerdict.Ok)
                method = ProspectMethod.Assay;
            else if (DiscoveryQuery.CheckProspect(world, cfg, polity, s, ProspectMethod.Survey, assay) == ProspectVerdict.Ok)
                method = ProspectMethod.Survey;
            else continue;
            int count = KnownEligible(world, cfg, s);
            // (−count, id) ascending: more visible eligible goods first, then the lower id.
            if (best is null || count > bestCount || (count == bestCount && s.Value < best.Value.Value))
            {
                best = s;
                bestCount = count;
                bestMethod = method;
            }
        }
        return best is { } b ? OrderRecord.From(world.Clock.Turn, polity, OrderKind.Prospect, b.Value, (double)(int)bestMethod) : null;
    }

    /// <summary>The Explore order for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Explore(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Fog is not { Enabled: true } fog) return null;
        SettlementId? best = null;
        int bestRadius = int.MaxValue;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementId s = world.Settlements[i].Id;
            if (DiscoveryQuery.CheckExplore(world, cfg, polity, s) != ExploreVerdict.Ok) continue;
            int radius = DiscoveryQuery.ExploredRadius(world, fog, polity, s);
            if (best is null || radius < bestRadius || (radius == bestRadius && s.Value < best.Value.Value))
            {
                best = s;
                bestRadius = radius;
            }
        }
        return best is { } b ? OrderRecord.From(world.Clock.Turn, polity, OrderKind.Explore, b.Value, 0.0) : null;
    }

    /// <summary>Whether the founding policy is looking for a site this turn: AI founding on, the polity due
    /// (AiFoundingPolicy.IsDue) and at least one source able to send a party (AiFoundingPolicy.RankedSources). When this
    /// holds and the founding policy issued nothing, it found no known candidate site.</summary>
    public static bool WantsSites(IReadOnlyWorldState world, SimConfig cfg, PolityId polity) =>
        cfg.Colonization?.AiFounding is { Enabled: true } ai
        && AiFoundingPolicy.IsDue(world, ai, polity)
        && AiFoundingPolicy.RankedSources(world, cfg, ai, polity).Length > 0;

    /// <summary>The concealable deposit goods the settlement's controller can see there (known abundance &gt; 0).</summary>
    public static int KnownEligible(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (cfg.Discovery is not { } d || cfg.Goods is not { } goods) return 0;
        int n = 0;
        foreach (string name in d.ConcealableGoods)
        {
            int id = goods.IdOf(name);
            if (id >= 0 && DepositConcealment.KnownAbundance(world, cfg, settlement, id) > 0.0) n++;
        }
        return n;
    }
}
