using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Core.Systems.Construction;

/// <summary>
/// ADR-033 D5 — THE PROVISIONAL AI CONSTRUCTION POLICY (the AgeAdvancePolicy / RoadDevelopmentPolicy shape).
/// It is NOT a system and writes no state: it returns EnqueueConstruction ORDERS built by
/// <see cref="ConstructionQuery.EnqueueOrder"/> — the constructor the player's build action uses — for
/// projects <see cref="ConstructionQuery.IsProjectAvailable"/> admits, the very predicate ConstructionSystem
/// applies, which the session appends to the order log; the AI's buildings go through ConstructionSystem
/// exactly as the player's do and an AI run replays from its log.
///
/// THE RULE, stated so nothing about it is hidden:
/// <list type="number">
/// <item><b>Ordinary projects</b> (granary, workshop — every project that founds no institution): for each
/// settlement an AI-commanded polity may build in, in settlement-table order, AT MOST ONE order per turn — and
/// only while that settlement's queue is EMPTY (one project waits at a time, so a blocked head is never buried
/// under more). It enqueues the first such project in goods.json order that the settlement has not built yet,
/// that is AVAILABLE to the polity there, and whose every material is present IN FULL in the settlement now
/// (the construction-labour gate is applied by the system when it resolves the queue). Building each project
/// once per settlement keeps the AI from sinking every unit of timber and stone into duplicate structures.</item>
/// <item><b>Universities</b> (ADR-033 D6; projects that found an institution): AT MOST ONE founding per
/// polity per turn, decided after the ordinary projects. The SPECIALTY is the AVAILABLE university type the
/// polity holds FEWEST instances of — counted PROSPECTIVELY: its founded rows, the buildings of the type
/// completed in its settlements and not yet founded, and the projects of the type in their queues, so the
/// one-turn founding lag and a new university's zero maturity never make the AI order the same specialty
/// twice — then the lowest maturity-weighted count X, then the LOWER type key (composite key (n, X, key)).
/// A type whose prospective count is already SATURATED (S(n) = 1 − e^(−n) ≥ saturatedAt: one more mature
/// instance would add less than 5 % of what the first did) is not founded again — the AI's reading of the
/// derived SATURATED stage, not a cap on the world (the player may found more). The SITE is, among the
/// polity's settlements with an EMPTY queue and no order this turn where the project is AVAILABLE, the
/// founding viability holds (InstitutionViability.ToFound — the gate ConstructionSystem applies), every
/// material is present in full and the construction capacity covers the labour (affordable this turn), the
/// one with the LARGEST spare market (adults − adultsPerUniversity × the universities it already hosts, an
/// integer), ties to the LOWER settlement id (composite key). No university is founded where any of those
/// fails, so the AI never queues one that would only block its queue.</item>
/// </list>
/// Deterministic and tie-free: integer table and content order, composite keys with integer tie-breaks
/// (tie-dense tests: InstitutionAiTests). Strategic construction is later AI work. Player-commanded polities
/// are never touched.
/// </summary>
public static class AiConstructionPolicy
{
    /// <summary>The orders the AI issues for <paramref name="polity"/> this turn for ordinary projects only (no
    /// turn length is known, so no university's labour can be judged affordable).</summary>
    public static OrderRecord[] Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity) =>
        Decide(world, cfg, polity, dtYears: null);

    /// <summary>As <see cref="Decide(IReadOnlyWorldState, SimConfig, PolityId)"/>; <paramref name="dtYears"/>
    /// is the length of the step the orders will be delivered to — the construction capacity a university
    /// must fit in (AiOrders passes it). Null: ordinary projects only.</summary>
    public static OrderRecord[] Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, double? dtYears)
    {
        if (cfg.Goods is not { } goods || goods.Projects is not { Length: > 0 } projects) return [];
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return [];

        var orders = new List<OrderRecord>();
        var ordered = new List<int>();   // settlements given an order this turn
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            if (ConstructionQuery.Queue(world, settlement).Length > 0) continue;
            foreach (ConstructionProjectEntry project in projects)
            {
                if (InstitutionContent.FoundsInstitution(project)) continue;   // universities: the polity-level decision below
                if (ConstructionQuery.Built(world, settlement, project.Id) > 0) continue;
                if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;
                if (!ConstructionQuery.MaterialsAvailable(world.GoodStocks, goods, settlement, project)) continue;
                orders.Add(ConstructionQuery.EnqueueOrder(world, polity, settlement, project.Id));
                ordered.Add(settlement.Value);
                break;
            }
        }

        if (dtYears is { } dt && University(world, cfg, polity, dt, ordered) is { } university) orders.Add(university);
        return [.. orders];
    }

    /// <summary>The one university founding the AI orders this turn, or null (see the header, item 2).
    /// Settlements in <paramref name="alreadyOrdered"/> (ids) already received an order this turn.</summary>
    public static OrderRecord? University(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, double dtYears, IReadOnlyList<int>? alreadyOrdered = null)
    {
        if (cfg.Goods is not { } goods || cfg.Research is not { } research || cfg.Institutions is not { } institutions) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        UniversitiesConfig u = institutions.Universities;

        // The specialty: composite key (n ascending, X ascending, type key ascending) over the AVAILABLE,
        // not prospectively saturated types that have a site. Content order is ascending key, and the best is
        // replaced only on a STRICTLY lower n, or an equal n and a STRICTLY lower X.
        int bestType = -1;
        long bestN = 0;
        double bestX = 0.0;
        SettlementId bestSite = default;
        ConstructionProjectEntry? bestProject = null;
        foreach (UniversityType type in research.UniversityTypes)
        {
            if (InstitutionContent.ProjectOfType(cfg, type.Key) is not { } project) continue;
            long n = ProspectiveCount(world, polity, type.Key, project);
            if (InstitutionEffects.Saturation(n) >= u.SaturatedAt) continue;
            if (bestType >= 0 && n > bestN) continue;
            double x = InstitutionEffects.MaturityWeighted(world, polity, type.Key);
            if (bestType >= 0 && n == bestN && !(x < bestX)) continue;
            if (Site(world, cfg, goods, polity, project, dtYears, u.AdultsPerUniversity, alreadyOrdered) is not { } site) continue;
            (bestType, bestN, bestX, bestSite, bestProject) = (type.Key, n, x, site, project);
        }
        return bestProject is null ? null : ConstructionQuery.EnqueueOrder(world, polity, bestSite, bestProject.Id);
    }

    /// <summary>The polity's instances of university type <paramref name="typeKey"/>, counted prospectively:
    /// its founded rows of the type, plus — in the settlements it controls — the type's buildings completed and
    /// not yet founded (the Structures count less every row of the type there) and the type's projects waiting
    /// in the queue.</summary>
    public static long ProspectiveCount(IReadOnlyWorldState world, PolityId polity, int typeKey, ConstructionProjectEntry project)
    {
        long n = 0;
        for (int i = 0; i < world.Institutions.Count; i++)
            if (world.Institutions[i].Polity.Value == polity.Value && world.Institutions[i].Type == typeKey) n++;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, settlement)) continue;
            long rows = 0;
            for (int i = 0; i < world.Institutions.Count; i++)
                if (world.Institutions[i].Settlement == settlement && world.Institutions[i].Type == typeKey) rows++;
            n += Math.Max(0, ConstructionQuery.Built(world, settlement, project.Id) - rows);
            for (int i = 0; i < world.ConstructionQueue.Count; i++)
                if (world.ConstructionQueue[i].Settlement == settlement && world.ConstructionQueue[i].ProjectId == project.Id) n++;
        }
        return n;
    }

    /// <summary>The site for one university project: the eligible settlement with the largest spare market,
    /// ties to the lower settlement id (composite key over integers); null when none is eligible.</summary>
    public static SettlementId? Site(
        IReadOnlyWorldState world, SimConfig cfg, GoodsConfig goods, PolityId polity, ConstructionProjectEntry project,
        double dtYears, long marketPerUniversity, IReadOnlyList<int>? alreadyOrdered = null)
    {
        SettlementId? best = null;
        long bestSpare = 0;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            if (alreadyOrdered is not null && Contains(alreadyOrdered, settlement.Value)) continue;
            if (ConstructionQuery.Queue(world, settlement).Length > 0) continue;
            if (!ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)) continue;
            UniversityViability v = InstitutionViability.ToFound(world, cfg, settlement);
            if (!v.Viable) continue;
            if (!ConstructionQuery.MaterialsAvailable(world.GoodStocks, goods, settlement, project)) continue;
            if (!(ConstructionQuery.CapacityAdultYears(world, cfg, settlement, dtYears) >= project.LaborRequired)) continue;
            long spare = v.Adults - marketPerUniversity * v.Hosted;
            if (best is { } held && (spare < bestSpare || (spare == bestSpare && settlement.Value > held.Value))) continue;
            (best, bestSpare) = (settlement, spare);
        }
        return best;
    }

    private static bool Contains(IReadOnlyList<int> values, int value)
    {
        for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
        return false;
    }
}
