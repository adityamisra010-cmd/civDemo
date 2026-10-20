using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>ADR-028 §3's lifecycle of one (polity, settlement, university type) slot, or of an instance.
/// LOCKED and AVAILABLE are DERIVED (the availability predicate); UNDER_CONSTRUCTION is the queue entry (and
/// the one step between a building's completion and its founding); ACTIVE and MATURE read the stored
/// maturity of a founded instance.</summary>
public enum InstitutionLifecycle
{
    Locked = 1,
    Available = 2,
    UnderConstruction = 3,
    Active = 4,
    Mature = 5,
}

/// <summary>The DERIVED saturation reading of a polity's specialty (ADR-028 §2.2 — never a stored flag):
/// None (no maturity yet), Diminishing (X &gt; 0: the next university adds less than the first did),
/// Saturated (S(X) ≥ saturatedAt: the next adds almost nothing).</summary>
public enum SaturationReading
{
    None = 0,
    Diminishing = 1,
    Saturated = 2,
}

/// <summary>One founded institution as the UI shows it: identity, owner, site, founding turn, the stored
/// maturity and its lifecycle reading, the adults it employs now, whether its host sustains it (and why
/// not), and — for the healing type — the coverage it gives its own settlement (its maturity, d = 0).</summary>
public sealed record InstitutionInstanceView(
    int Id, int TypeKey, string TypeId, string TypeName, string Branch,
    PolityId Owner, SettlementId Settlement, long FoundedTurn,
    double Maturity, InstitutionLifecycle Stage, double Staff,
    bool Viable, string? ViabilityBlocker, bool Heals);

/// <summary>One university specialty of a polity: how many it owns, the maturity-weighted count X, the
/// saturation S(X) with its reading, the research-cost factor its branch's Technology nodes carry NOW (the
/// stored ResearchCostModifiers row ResearchSystem reads; 1.0 when none), and the extra cut one more mature
/// university would give (maxResearchCostReduction × (S(X + 1) − S(X)) — diminishing returns made visible).</summary>
public sealed record UniversitySpecialtyView(
    int TypeKey, string TypeId, string TypeName, string Branch,
    int Count, double MaturityWeighted, double Saturation, SaturationReading Reading,
    double ResearchFactor, double NextMarginalCut, bool Heals);

/// <summary>A settlement's medical coverage (diffused from every medical university by travel cost) and the
/// multiplier DemographicsSystem applies to its base mortality (1.0 = no effect).</summary>
public sealed record MedicalCoverageView(SettlementId Settlement, double Coverage, double MortalityMultiplier);

/// <summary>
/// ADR-033 D6 — THE INSTITUTIONS READ SURFACE the UI uses (streams U2/U4 render it; the world map reads
/// <c>InstitutionMarkerSource</c>). Pure and read-only, no cache, no state: every number is recomputed by the
/// function the simulation itself calls (InstitutionStaffing, InstitutionViability, InstitutionEffects,
/// ConstructionQuery), so the UI can never disagree with the systems. Iteration is table and content order;
/// no dictionary, no LINQ (law 5).
/// </summary>
public static class InstitutionsQuery
{
    /// <summary>The institutions <paramref name="owner"/> owns, table order (= founding order).</summary>
    public static InstitutionInstanceView[] Instances(IReadOnlyWorldState world, SimConfig cfg, PolityId owner)
    {
        var result = new List<InstitutionInstanceView>();
        for (int i = 0; i < world.Institutions.Count; i++)
            if (world.Institutions[i].Polity.Value == owner.Value) result.Add(View(world, cfg, world.Institutions[i]));
        return [.. result];
    }

    /// <summary>The institutions in <paramref name="settlement"/>, whoever owns them, table order.</summary>
    public static InstitutionInstanceView[] At(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        var result = new List<InstitutionInstanceView>();
        for (int i = 0; i < world.Institutions.Count; i++)
            if (world.Institutions[i].Settlement == settlement) result.Add(View(world, cfg, world.Institutions[i]));
        return [.. result];
    }

    /// <summary>Every university type (content order) as <paramref name="owner"/>'s specialty reading.</summary>
    public static UniversitySpecialtyView[] Specialties(IReadOnlyWorldState world, SimConfig cfg, PolityId owner)
    {
        if (cfg.Research is not { } research) return [];
        int health = InstitutionContent.HealthTypeKey(cfg);
        double saturatedAt = cfg.Institutions?.Universities.SaturatedAt ?? 1.0;
        double maxCut = cfg.Institutions?.Universities.MaxResearchCostReduction ?? 0.0;
        var result = new UniversitySpecialtyView[research.UniversityTypes.Count];
        for (int t = 0; t < result.Length; t++)
        {
            UniversityType type = research.UniversityTypes[t];
            int count = 0;
            for (int i = 0; i < world.Institutions.Count; i++)
                if (world.Institutions[i].Polity.Value == owner.Value && world.Institutions[i].Type == type.Key) count++;
            double x = InstitutionEffects.MaturityWeighted(world, owner, type.Key);
            double saturation = InstitutionEffects.Saturation(x);
            SaturationReading reading = !(x > 0.0) ? SaturationReading.None
                : saturation >= saturatedAt ? SaturationReading.Saturated : SaturationReading.Diminishing;
            result[t] = new UniversitySpecialtyView(type.Key, type.Id, type.Name, research.Branches[type.Branch].Name,
                count, x, saturation, reading, StoredFactor(world, owner, type.Key),
                maxCut * InstitutionEffects.NextMarginal(x), type.Key == health);
        }
        return result;
    }

    /// <summary>The settlement's medical coverage and mortality multiplier (the seam DemographicsSystem reads).</summary>
    public static MedicalCoverageView Health(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        new(settlement, InstitutionEffects.MedicalCoverage(world, cfg, settlement),
            InstitutionEffects.MortalityMultiplier(world, cfg, settlement));

    /// <summary>
    /// The lifecycle of the (polity, settlement, type) slot: MATURE / ACTIVE when the settlement holds a
    /// founded institution of the type owned by the polity (the most mature one decides), else
    /// UNDER_CONSTRUCTION when the type's project is queued there or built but not yet founded, else
    /// AVAILABLE when the availability predicate admits the project, else LOCKED.
    /// </summary>
    public static InstitutionLifecycle Lifecycle(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement, int typeKey)
    {
        double best = -1.0;
        long founded = 0;
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Settlement != settlement || row.Type != typeKey) continue;
            founded++;
            if (row.Polity.Value == polity.Value && row.Maturity > best) best = row.Maturity;
        }
        if (best >= 0.0) return Stage(cfg, best);
        if (InstitutionContent.ProjectOfType(cfg, typeKey) is not { } project) return InstitutionLifecycle.Locked;
        foreach (ConstructionQueueRow row in ConstructionQuery.Queue(world, settlement))
            if (row.ProjectId == project.Id) return InstitutionLifecycle.UnderConstruction;
        if (ConstructionQuery.Built(world, settlement, project.Id) > founded) return InstitutionLifecycle.UnderConstruction;
        return ConstructionQuery.IsProjectAvailable(world, cfg, polity, settlement, project.Id)
            ? InstitutionLifecycle.Available : InstitutionLifecycle.Locked;
    }

    /// <summary>ACTIVE or MATURE for a stored maturity (MATURE at or above matureAt).</summary>
    public static InstitutionLifecycle Stage(SimConfig cfg, double maturity) =>
        cfg.Institutions is { } inst && maturity >= inst.Universities.MatureAt
            ? InstitutionLifecycle.Mature : InstitutionLifecycle.Active;

    /// <summary>The adults every institution of the settlement employs now (the labour reader's staff).</summary>
    public static double StaffAt(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        InstitutionStaffing.Staff(world, cfg, settlement);

    private static InstitutionInstanceView View(IReadOnlyWorldState world, SimConfig cfg, InstitutionRow row)
    {
        UniversityType? type = InstitutionContent.TypeOf(cfg.Research, row.Type);
        string branch = type is not null && cfg.Research is { } research ? research.Branches[type.Branch].Name : "";
        UniversityViability viability = InstitutionViability.ToSustain(world, cfg, row.Settlement);
        double staff = InstitutionStaffing.StaffOfInstitution(cfg, row.Maturity);
        return new InstitutionInstanceView(
            row.Id, row.Type, type?.Id ?? "", type?.Name ?? "Institution", branch,
            row.Polity, row.Settlement, row.FoundedTurn, row.Maturity, Stage(cfg, row.Maturity), staff,
            viability.Viable, InstitutionViability.Blocker(viability), row.Type == InstitutionContent.HealthTypeKey(cfg));
    }

    private static double StoredFactor(IReadOnlyWorldState world, PolityId owner, int typeKey)
    {
        double factor = 1.0;
        for (int i = 0; i < world.ResearchCostModifiers.Count; i++)
        {
            ResearchCostModifierRow row = world.ResearchCostModifiers[i];
            if (row.Polity.Value == owner.Value && row.UniversityType == typeKey) factor *= row.Factor;
        }
        return factor;
    }
}
