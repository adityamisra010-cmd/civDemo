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
/// <item><b>Build first</b> (ADR-046 §7, sim.json discovery.aiBuildFirst): a method whose bill would leave the purse unable
///   to cover the polity's ESSENTIAL construction is skipped there — <see cref="DefersToBuilding"/> — unless a concealed
///   resource is strategically required (<see cref="StrategicNeed"/>).</item>
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
        // ADR-046 §7: the strategic exception is judged once per polity; with it (or the switch off) nothing defers.
        bool buildFirst = cfg.Discovery is { AiBuildFirst: true } && StrategicNeed(world, cfg, polity) < 0;
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
            if (assay && DiscoveryQuery.CheckProspect(world, cfg, polity, s, ProspectMethod.Assay, true) == ProspectVerdict.Ok
                && !(buildFirst && DefersToBuilding(world, cfg, polity, s, ProspectMethod.Assay)))
                method = ProspectMethod.Assay;
            else if (DiscoveryQuery.CheckProspect(world, cfg, polity, s, ProspectMethod.Survey, assay) == ProspectVerdict.Ok
                && !(buildFirst && DefersToBuilding(world, cfg, polity, s, ProspectMethod.Survey)))
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

    /// <summary>
    /// ADR-046 §7 (M6 review batch 3, 2026-10-23) — BUILD FIRST: whether the AI holds back a Prospect of
    /// <paramref name="method"/> at <paramref name="settlement"/> because paying its bill would leave the purse unable to
    /// cover the polity's ESSENTIAL construction. The purse is the one construction reads
    /// (ConstructionQuery.Holders / Unclaimed: the holding less every queued project's remaining claim); the RESERVE is,
    /// for each settlement the polity rules on that purse whose queue is idle (and, with incremental construction, has
    /// labour adults) — the settlements AiConstructionPolicy may order into — the FIRST ordinary project in goods.json order
    /// that is not built there and is AVAILABLE there (the project the construction policy is waiting to afford), counted
    /// only when none of its inputs is a CRAFTED good (ConstructionQuery.IsCraftedGood): the granary counts; the workshop
    /// (tools) never holds a survey back, since its wait is not for the purse's raw goods. The bill defers when, for any of
    /// its goods, unclaimed − reserve &lt; bill. Nothing here changes the bill or the affordability check: an order the
    /// policy issues is still admitted by DiscoveryQuery.CheckProspect, and DiscoverySystem charges it as before.
    /// </summary>
    public static bool DefersToBuilding(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement,
        ProspectMethod method)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects) return false;
        SettlementId[] holders = ConstructionQuery.Holders(world, cfg, settlement);
        foreach ((GoodId good, long qty) in DiscoveryQuery.ProspectBill(cfg, method))
        {
            if (qty <= 0) continue;
            long free = ConstructionQuery.Unclaimed(world.GoodStocks, world.ConstructionQueue, world.ConstructionProgress, goods, holders, good);
            if (free - EssentialReserve(world, cfg, goods, projects, polity, holders, good) < qty) return true;
        }
        return false;
    }

    /// <summary>The reserve of <paramref name="good"/> on the purse <paramref name="holders"/> for the polity's essential
    /// construction (<see cref="DefersToBuilding"/>), summed in settlement-table order.</summary>
    public static long EssentialReserve(IReadOnlyWorldState world, SimConfig cfg, GoodsConfig goods,
        ConstructionProjectEntry[] projects, PolityId polity, SettlementId[] holders, GoodId good)
    {
        PolityKnowledge knows = PolityKnowledge.Of(world, polity, cfg);
        bool incremental = ConstructionQuery.Incremental(cfg);
        long reserve = 0;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            if (!knows.ReadsInternals(settlement) || !ConstructionQuery.IsHolder(holders, settlement)) continue;   // its own books
            if (ConstructionQuery.Queue(world, settlement).Length > 0) continue;
            if (incremental && !(InstitutionStaffing.LabourAdults(world, cfg, settlement) > 0.0)) continue;
            ConstructionProjectEntry? wish = null;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (InstitutionContent.FoundsInstitution(project)) continue;
                if (ConstructionQuery.Built(world, settlement, project.Id) > 0) continue;
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;
                wish = project;
                break;
            }
            if (wish is null) continue;
            bool essential = true;
            foreach (ProjectInput input in wish.Inputs)
                if (ConstructionQuery.IsCraftedGood(goods, goods.IdOf(input.Good))) { essential = false; break; }
            if (essential) reserve = checked(reserve + ConstructionQuery.InputOf(goods, wish, good));
        }
        return reserve;
    }

    /// <summary>
    /// ADR-046 §7 — THE STRATEGIC EXCEPTION: the first concealable good (goods.json id order) that some recipe the polity
    /// KNOWS consumes (CraftingQuery.IsKnownBy — its own completed knowledge) while it sees no deposit of it (known
    /// abundance &gt; 0) at any settlement it rules: finding that ore is what a survey is for, so it is not held back for
    /// construction. -1 when there is none (the polity knows no such recipe, or sees every good its recipes need).
    /// </summary>
    public static int StrategicNeed(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Goods is not { } goods || !DepositConcealment.Enabled(cfg)) return -1;
        bool[] concealable = DepositConcealment.ConcealableMask(cfg);
        PolityKnowledge knows = PolityKnowledge.Of(world, polity, cfg);
        int best = -1;
        foreach (RecipeEntry recipe in goods.Recipes)
        {
            if (!CraftingQuery.IsKnownBy(world, cfg.Research, polity, recipe)) continue;
            foreach (RecipeInput input in recipe.Inputs)
            {
                int id = goods.IdOf(input.Good);
                if ((uint)id >= (uint)concealable.Length || !concealable[id] || (best >= 0 && id >= best)) continue;
                bool seen = false;
                for (int s = 0; s < world.Settlements.Count && !seen; s++)
                {
                    SettlementId settlement = world.Settlements[s].Id;
                    seen = knows.ReadsInternals(settlement) && DepositConcealment.KnownAbundance(world, cfg, settlement, id) > 0.0;
                }
                if (!seen) best = id;
            }
        }
        return best;
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

    /// <summary>The concealable deposit goods the settlement's controller can see there (known abundance &gt; 0).
    /// ADR-056: and can WORK (EconomyKnowledge.IsWorkable — an economy packet's ore counts once the settlement's knowledge
    /// meets its goods.json gate; every base good always counts), so an ore nobody there can smelt does not reorder the
    /// survey queue before it matters.</summary>
    public static int KnownEligible(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (cfg.Discovery is not { } d || cfg.Goods is not { } goods) return 0;
        int n = 0;
        foreach (string name in d.ConcealableGoods)
        {
            int id = goods.IdOf(name);
            if (id >= 0 && DepositConcealment.KnownAbundance(world, cfg, settlement, id) > 0.0
                && EconomyKnowledge.IsWorkable(world, cfg, settlement, id)) n++;
        }
        return n;
    }
}
