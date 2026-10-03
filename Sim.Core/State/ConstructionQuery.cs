using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>Why a construction project is, or is not, AVAILABLE to an issuer in a settlement (ADR-033 D3).
/// Availability is legality (ADR-028's LOCKED / AVAILABLE); affordability is reported separately.</summary>
public enum ProjectAvailability
{
    Available = 0,
    /// <summary>No settlement has this id.</summary>
    UnknownSettlement = 1,
    /// <summary>The issuer does not control the settlement (M4-D §12: an Empire builds only where it rules).</summary>
    NotControlled = 2,
    /// <summary>goods.json defines no project with this id (or no goods content is loaded).</summary>
    UnknownProject = 3,
    /// <summary>The project's research entity is not knowledge-eligible for the issuer — ADR-028 LOCKED.</summary>
    NotKnowledgeEligible = 4,
}

/// <summary>One input a project lacks in a settlement now: <c>Required</c> units of <c>Good</c>, <c>Held</c> present.</summary>
public readonly record struct MaterialShortfall(GoodId Good, string Name, long Required, long Held);

/// <summary>
/// ADR-033 D3 — THE CONSTRUCTION READ SEAM (the ResearchQuery / AgeQuery precedent): pure, read-only, no
/// state, no cache. <b>ConstructionSystem computes with these same statics</b>: it accepts an
/// EnqueueConstruction order iff <see cref="IsProjectAvailable"/> holds on PREV, gates the queue head on
/// <see cref="CapacityAdultYears"/> and <see cref="MaterialsAvailable"/>, and AvailableActionsQuery lists a
/// project iff the same predicate holds on the world asked about — one predicate, two callers (ADR-033 D2),
/// so the UI never offers a project the simulation would refuse and an order the UI never offered is
/// refused on the same rule.
///
/// AVAILABLE = the settlement exists, the issuer controls it, goods.json defines the project, and the
/// project's research entity (goods.json <c>projects[].entity</c>, data — no id in code) is
/// knowledge-eligible for the issuer (ResearchQuery.IsKnowledgeEligible; a null requirement is always met,
/// ADR-028 §3). A project with no entity link has no knowledge requirement. A config with no research
/// content has no knowledge to gate on: every linked project is knowledge-eligible there (the baseline
/// construction capability predates research). Materials and capacity are AFFORDABILITY — transient
/// blockers the query reports, never reasons to hide the project.
/// </summary>
public static class ConstructionQuery
{
    /// <summary>THE EnqueueConstruction predicate (see the header).</summary>
    public static bool IsProjectAvailable(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId) =>
        Availability(world, cfg, issuer, settlement, projectId) == ProjectAvailability.Available;

    /// <summary>The predicate's answer with its reason.</summary>
    public static ProjectAvailability Availability(
        IReadOnlyWorldState world, SimConfig cfg, PolityId issuer, SettlementId settlement, int projectId)
    {
        if (!SettlementExists(world, settlement)) return ProjectAvailability.UnknownSettlement;
        // Guarded on a non-empty relation: a world with no Controls at all (a hand-built toy) has
        // nothing to check against. In a FOUNDED world the relation is never empty (M4-C).
        if (world.Controls.Count > 0 && !EmpireQuery.ControlsSettlement(world, issuer, settlement))
            return ProjectAvailability.NotControlled;
        if (cfg.Goods?.ProjectById(projectId) is not { } project) return ProjectAvailability.UnknownProject;
        if (!IsKnowledgeEligible(world, cfg.Research, issuer, project)) return ProjectAvailability.NotKnowledgeEligible;
        return ProjectAvailability.Available;
    }

    /// <summary>Whether the project's research entity is knowledge-eligible for the issuer (see the header).
    /// An entity id the research content does not define is never eligible (content changed under a save).
    /// ADR-033 D6: a project that FOUNDS an institution (goods.json <c>founds</c>) needs the institution's
    /// entity knowledge-eligible too — a university project builds <c>building.university</c> and founds
    /// <c>inst.university</c>, and BOTH gate (the research stage's own reading, D-044 R4;
    /// docs/institutions-universities.md §3). Still the one availability predicate.</summary>
    public static bool IsKnowledgeEligible(
        IReadOnlyWorldState world, ResearchContent? research, PolityId issuer, ConstructionProjectEntry project)
    {
        if (research is null) return true;
        if (project.Entity is null && project.Founds is null) return true;
        bool[] completed = ResearchQuery.CompletedMask(world, research, issuer);
        if (project.Entity is { } building && !Eligible(research, building, completed)) return false;
        return project.Founds is not { } founds || Eligible(research, founds.Entity, completed);
    }

    private static bool Eligible(ResearchContent research, string entityId, bool[] completed)
    {
        int entity = research.EntityIndexOf(entityId);
        return entity >= 0 && ResearchQuery.IsKnowledgeEligible(research, entity, completed);
    }

    /// <summary>
    /// The settlement's construction capacity this turn, in adult-years: construction share × LABOUR adults ×
    /// dtYears, less housing's published draw (§3.2 one-turn lag), floored at zero (the lag means a shrinking
    /// pool can transiently owe more than it has). The SAME arithmetic, in the same order, that gates
    /// ConstructionSystem's queue head — it is the function the system calls. LABOUR adults are the adults
    /// left after institutional staff (ADR-033 D6, InstitutionStaffing.LabourAdults — the one labour reader;
    /// exactly the adult count when no institution is staffed).
    /// </summary>
    public static double CapacityAdultYears(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, double dtYears)
    {
        SectorAllocationRow shares = Sectors.Default(settlement);
        for (int i = 0; i < world.SectorAllocations.Count; i++)
        {
            if (world.SectorAllocations[i].Settlement == settlement)
            { shares = world.SectorAllocations[i]; break; }
        }

        double adults = InstitutionStaffing.LabourAdults(world, cfg, settlement);
        double capacity = Sectors.Share(shares, Sectors.Construction) * adults * dtYears;
        for (int i = 0; i < world.Housing.Count; i++)
        {
            if (world.Housing[i].Settlement != settlement) continue;
            capacity = Math.Max(0.0, capacity - world.Housing[i].LastLaborUsed);
            break;
        }
        return capacity;
    }

    /// <summary>Every input present IN FULL in <paramref name="stocks"/> (checked before any draw — the
    /// all-or-nothing rule ConstructionSystem applies to its NEXT stocks and the query to the world's).</summary>
    public static bool MaterialsAvailable(
        IReadOnlyTable<GoodStockRow> stocks, GoodsConfig goods, SettlementId settlement, ConstructionProjectEntry project)
    {
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            int idx = GoodStockIndex.IndexOf(stocks, settlement, new GoodId(goods.IdOf(input.Good)));
            if (idx < 0 || stocks[idx].Amount.Value < input.Qty) return false;
        }
        return true;
    }

    /// <summary>The inputs the settlement lacks now, input order (empty when every input is present in full).</summary>
    public static MaterialShortfall[] Shortfalls(
        IReadOnlyTable<GoodStockRow> stocks, GoodsConfig goods, SettlementId settlement, ConstructionProjectEntry project)
    {
        var result = new List<MaterialShortfall>();
        for (int i = 0; i < project.Inputs.Length; i++)
        {
            ProjectInput input = project.Inputs[i];
            var good = new GoodId(goods.IdOf(input.Good));
            int idx = GoodStockIndex.IndexOf(stocks, settlement, good);
            long held = idx < 0 ? 0 : stocks[idx].Amount.Value;
            if (held < input.Qty) result.Add(new MaterialShortfall(good, input.Good, input.Qty, held));
        }
        return [.. result];
    }

    /// <summary>
    /// The transient blocker the action surface shows for an AVAILABLE project, or null when it could be
    /// built this turn: the inputs short ("needs 40 timber (has 12)") and, when the turn's length is known,
    /// the construction labour short; for a project that founds an institution (ADR-033 D6), the host's
    /// founding viability first ("needs 4000 adults (has 3100)"). Information, not a disabled future
    /// control (ADR-033 D2) — each reading is the function ConstructionSystem gates the head with.
    /// </summary>
    public static string? Blocker(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, ConstructionProjectEntry project, double? dtYears)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("a construction blocker needs goods content", nameof(cfg));
        var parts = new List<string>();
        if (InstitutionContent.FoundsInstitution(project)
            && InstitutionViability.ToFound(world, cfg, settlement) is { Viable: false } viability)
            parts.Add(InstitutionViability.Shortfall(viability));
        foreach (MaterialShortfall s in Shortfalls(world.GoodStocks, goods, settlement, project))
            parts.Add($"{Num(s.Required)} {s.Name} (has {Num(s.Held)})");
        if (dtYears is { } dt && dt > 0.0)
        {
            double capacity = CapacityAdultYears(world, cfg, settlement, dt);
            if (capacity < project.LaborRequired)
                parts.Add($"{Num(project.LaborRequired)} adult-years of construction labour (has {Num(capacity)} this turn)");
        }
        return parts.Count == 0 ? null : "needs " + string.Join(", ", parts);
    }

    /// <summary>The order the player's build button and the AI policy issue — identical for both: stamped with
    /// the CURRENT turn (delivered to the next step), TargetId = the settlement, Amount = the project id.</summary>
    public static OrderRecord EnqueueOrder(IReadOnlyWorldState world, PolityId issuer, SettlementId settlement, int projectId) =>
        OrderRecord.From(world.Clock.Turn, issuer, OrderKind.EnqueueConstruction, settlement.Value, projectId);

    /// <summary>The settlement's queued projects, head (lowest slot) first.</summary>
    public static ConstructionQueueRow[] Queue(IReadOnlyWorldState world, SettlementId settlement)
    {
        var rows = new List<ConstructionQueueRow>();
        for (int i = 0; i < world.ConstructionQueue.Count; i++)
            if (world.ConstructionQueue[i].Settlement == settlement) rows.Add(world.ConstructionQueue[i]);
        rows.Sort(static (a, b) => a.Slot.CompareTo(b.Slot));   // slots are unique per settlement
        return [.. rows];
    }

    /// <summary>How many of <paramref name="projectId"/> the settlement has built.</summary>
    public static long Built(IReadOnlyWorldState world, SettlementId settlement, int projectId)
    {
        for (int i = 0; i < world.Structures.Count; i++)
            if (world.Structures[i].Settlement == settlement && world.Structures[i].ProjectId == projectId)
                return world.Structures[i].Count;
        return 0;
    }

    private static bool SettlementExists(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == settlement.Value) return true;
        return false;
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
}
