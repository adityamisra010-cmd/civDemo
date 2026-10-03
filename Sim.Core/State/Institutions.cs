using System.Globalization;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-033 D6 — THE UNIVERSITY CONTENT LINKS, resolved from data (no type id or project id in code):
/// which goods.json project founds which research.json university type, and which type heals
/// (<c>institutions.universities.healthType</c>). Pure lookups over immutable config.
/// </summary>
public static class InstitutionContent
{
    /// <summary>Whether completing the project founds an institution (goods.json <c>founds</c>).</summary>
    public static bool FoundsInstitution(ConstructionProjectEntry project) => project.Founds is not null;

    /// <summary>The research.json universityTypes key the project founds, or -1 (not a founding project, or
    /// no research content).</summary>
    public static int TypeKeyOf(ResearchContent? research, ConstructionProjectEntry project)
    {
        if (research is null || project.Founds is not { } founds) return -1;
        foreach (UniversityType u in research.UniversityTypes)
            if (string.Equals(u.Id, founds.UniversityType, StringComparison.Ordinal)) return u.Key;
        return -1;
    }

    /// <summary>The goods.json project that founds university type <paramref name="typeKey"/>, or null.</summary>
    public static ConstructionProjectEntry? ProjectOfType(SimConfig cfg, int typeKey)
    {
        if (cfg.Goods?.Projects is not { } projects || cfg.Research is not { } research) return null;
        foreach (ConstructionProjectEntry p in projects)
            if (TypeKeyOf(research, p) == typeKey) return p;
        return null;
    }

    /// <summary>The university type record of key <paramref name="typeKey"/>, or null.</summary>
    public static UniversityType? TypeOf(ResearchContent? research, int typeKey)
    {
        if (research is null) return null;
        int i = research.UniversityTypeIndexOf(typeKey);
        return i < 0 ? null : research.UniversityTypes[i];
    }

    /// <summary>The key of the type whose maturity heals (config <c>healthType</c>), or -1.</summary>
    public static int HealthTypeKey(SimConfig cfg)
    {
        if (cfg.Institutions is not { } inst || cfg.Research is not { } research) return -1;
        foreach (UniversityType u in research.UniversityTypes)
            if (string.Equals(u.Id, inst.Universities.HealthType, StringComparison.Ordinal)) return u.Key;
        return -1;
    }
}

/// <summary>
/// ADR-033 D6 — THE ONE LABOUR READER (docs/institutions-universities.md §7). A university employs
/// scholars: real adults of its host, withdrawn from productive labour — never created, never destroyed
/// (they stay in their buckets; no Ledger flow moves them). Every sector pool reads the labour that
/// remains through <see cref="LabourAdults"/> — ProductionSystem (farming, herding/fishing, extraction,
/// crafting) and the construction pool's consumers (HousingSystem, PathBuildSystem,
/// ConstructionQuery.CapacityAdultYears) — so sector shares apply to adults − staff and a scholar is
/// never also a farmer or a builder.
///
/// staff(S) = min(adults(S), Σ_i at S staffShareAtMaturity × adultsPerUniversity × M_i): each university
/// at maturity employs the share σ of the market it needs (σ × A_u adults), growing into its staff as it
/// matures. Staffing is per INSTANCE, not per host: while the market term holds (n ≤ adults / A_u) the
/// host's staff never exceed σ of its adults, however large the host — staff proportional to host size
/// would compound with the per-instance market into a share ∝ adults (§7.1 records the measurement).
/// With no institution (or no institutions config) the reader returns <c>(double)adults</c> EXACTLY, so
/// every pool is bit-identical to the tree before it. Pure: reads only the world it is given (systems
/// pass PREV).
/// </summary>
public static class InstitutionStaffing
{
    /// <summary>The adults the settlement's institutions employ (≥ 0, ≤ adults).</summary>
    public static double Staff(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        StaffOf(world, cfg, settlement, BandViews.Adults(world.Buckets, settlement));

    /// <summary>The settlement's productive labour force: adults − staff (the raw adult count when no
    /// institution is staffed). Sector shares apply to THIS.</summary>
    public static double LabourAdults(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        long adults = BandViews.Adults(world.Buckets, settlement);
        double staff = StaffOf(world, cfg, settlement, adults);
        return staff > 0.0 ? adults - staff : adults;
    }

    /// <summary>One institution's staff at its current maturity: staffShareAtMaturity × adultsPerUniversity ×
    /// M (before the host's cap at its adult count).</summary>
    public static double StaffOfInstitution(SimConfig cfg, double maturity) =>
        cfg.Institutions is { } inst
            ? inst.Universities.StaffShareAtMaturity * inst.Universities.AdultsPerUniversity * Clamp01(maturity)
            : 0.0;

    private static double StaffOf(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, long adults)
    {
        if (cfg.Institutions is null || world.Institutions.Count == 0 || adults <= 0) return 0.0;
        double staff = 0.0;
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Settlement != settlement) continue;
            staff += StaffOfInstitution(cfg, row.Maturity);
        }
        if (!(staff > 0.0)) return 0.0;
        return Math.Min(adults, staff);
    }

    private static double Clamp01(double m) => m > 1.0 ? 1.0 : m > 0.0 ? m : 0.0;
}

/// <summary>Which viability term failed (none when viable). Ordered as the checks run.</summary>
public enum ViabilityFailure
{
    None = 0,
    /// <summary>No institutions config is loaded: nothing can be founded or sustained.</summary>
    NoConfig = 1,
    /// <summary>The host's adults are below the market the instances need (adultsPerUniversity × n).</summary>
    Market = 2,
    /// <summary>The host's published food_surplus_ratio is below the threshold.</summary>
    FoodSurplus = 3,
}

/// <summary>One viability reading at a settlement: the verdict, the failing term, and the inputs.</summary>
public readonly record struct UniversityViability(
    bool Viable, ViabilityFailure Failure, long Adults, long Hosted, long AdultsNeeded,
    double FoodSurplusRatio, double FoodSurplusNeeded);

/// <summary>
/// ADR-033 D6 / ADR-028 §1 — VIABILITY OF A UNIVERSITY AT A PLACE, type-specific and hierarchical
/// (docs/institutions-universities.md §4). Hierarchy is the content requirement (availability). Local
/// capacity is two terms read from published state: the MARKET — the host's adults must cover
/// adultsPerUniversity per university instance (n hosted to sustain, n + 1 to found; per instance and
/// scaled by population, never a maximum count) — and FOOD — the host's food_surplus_ratio must reach the
/// content's specialist thresholds (founding 1.3, sustaining 1.1: the Artisans emerge / recede pair). No
/// site constraint is invented. Every caller passes the world it judges (systems pass PREV): the
/// ConstructionSystem resolution gate and the AI use <see cref="ToFound"/>, InstitutionsSystem's
/// maturation uses <see cref="ToSustain"/>, and the action surface reports the same readings.
/// </summary>
public static class InstitutionViability
{
    /// <summary>University buildings the settlement hosts: the Structures count of every founding project
    /// there (all types). Equal to its institution rows except in the one step between a building's
    /// completion and its founding, when the new building already needs its market.</summary>
    public static long Hosted(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (cfg.Goods?.Projects is not { } projects) return 0;
        long hosted = 0;
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            if (row.Settlement != settlement || row.Count <= 0) continue;
            foreach (ConstructionProjectEntry p in projects)
                if (p.Id == row.ProjectId && p.Founds is not null) { hosted += row.Count; break; }
        }
        return hosted;
    }

    /// <summary>Can the settlement found ONE MORE university now?</summary>
    public static UniversityViability ToFound(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        Evaluate(world, cfg, settlement, extra: 1, founding: true);

    /// <summary>Can the settlement sustain (keep maturing) the universities it hosts?</summary>
    public static UniversityViability ToSustain(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        Evaluate(world, cfg, settlement, extra: 0, founding: false);

    /// <summary>The published food_surplus_ratio (absent = 0.0: an unpublished variable reads 0, the
    /// ProductionSystem precedent — a settlement with no surplus reading supports no scholar).</summary>
    public static double FoodSurplusRatio(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Variables.Count; i++)
        {
            VariableRow row = world.Variables[i];
            if (row.Settlement == settlement && row.VarId == Variables.FoodSurplusRatio) return row.Value;
        }
        return 0.0;
    }

    /// <summary>The reading as action-surface text, or null when viable ("needs 4000 adults (has 3100)").</summary>
    public static string? Blocker(UniversityViability v) => v.Viable ? null : "needs " + Shortfall(v);

    /// <summary>What a non-viable reading lacks, as the object of "needs …" (empty when viable).</summary>
    public static string Shortfall(UniversityViability v) => v.Failure switch
    {
        ViabilityFailure.None => "",
        ViabilityFailure.NoConfig => "an institutions configuration",
        ViabilityFailure.Market => $"{Num(v.AdultsNeeded)} adults (has {Num(v.Adults)})",
        ViabilityFailure.FoodSurplus =>
            $"a food surplus ratio of {Ratio(v.FoodSurplusNeeded)} (has {Ratio(v.FoodSurplusRatio)})",
        _ => "a viable host",
    };

    private static UniversityViability Evaluate(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, long extra, bool founding)
    {
        long adults = BandViews.Adults(world.Buckets, settlement);
        long hosted = Hosted(world, cfg, settlement);
        double fsr = FoodSurplusRatio(world, settlement);
        if (cfg.Institutions is not { } inst)
            return new UniversityViability(false, ViabilityFailure.NoConfig, adults, hosted, 0, fsr, 0.0);
        UniversitiesConfig u = inst.Universities;
        long needed = checked(u.AdultsPerUniversity * (hosted + extra));
        double food = founding ? u.FoundingFoodSurplusRatio : u.SustainingFoodSurplusRatio;
        if (adults < needed)
            return new UniversityViability(false, ViabilityFailure.Market, adults, hosted, needed, fsr, food);
        if (!(fsr >= food))
            return new UniversityViability(false, ViabilityFailure.FoodSurplus, adults, hosted, needed, fsr, food);
        return new UniversityViability(true, ViabilityFailure.None, adults, hosted, needed, fsr, food);
    }

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
    private static string Ratio(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}

/// <summary>
/// ADR-033 D6 — THE MATURATION LAW AND THE EFFECTS (docs/institutions-universities.md §5–§6). Pure
/// functions; InstitutionsSystem integrates with them, DemographicsSystem reads the mortality seam, and
/// InstitutionsQuery reports with the same functions, so every Glass Box number is recomputed by the
/// function the simulation itself calls.
/// </summary>
public static class InstitutionEffects
{
    /// <summary>Viable maturation over dt years: the exact solution of dM/dt = (1 − M)/τ,
    /// M' = 1 − (1 − M)·e^(−dt/τ) (law 3: dt-correct by construction). M = 1 is a fixed point.</summary>
    public static double Grow(double maturity, double dtYears, double tauYears) =>
        1.0 - (1.0 - maturity) * Math.Exp(-dtYears / tauYears);

    /// <summary>Non-viable decay over dt years: the exact solution of dM/dt = −M/τ, M' = M·e^(−dt/τ).
    /// M = 0 is a fixed point.</summary>
    public static double Decay(double maturity, double dtYears, double tauYears) =>
        maturity * Math.Exp(-dtYears / tauYears);

    /// <summary>The saturating effect curve S(X) = 1 − e^(−X) over a maturity-weighted count X ≥ 0:
    /// concave (each instance adds less than the one before), bounded by 1.</summary>
    public static double Saturation(double maturityWeighted) =>
        maturityWeighted > 0.0 ? 1.0 - Math.Exp(-maturityWeighted) : 0.0;

    /// <summary>What ONE MORE fully mature instance would add to S: S(X + 1) − S(X) = e^(−X)(1 − e^(−1)).</summary>
    public static double NextMarginal(double maturityWeighted) =>
        Saturation(maturityWeighted + 1.0) - Saturation(maturityWeighted);

    /// <summary>The research-cost factor of a type with maturity-weighted count X:
    /// 1 − maxResearchCostReduction × S(X) ∈ (0, 1] (ADR-029 §9 input contract).</summary>
    public static double ResearchFactor(UniversitiesConfig u, double maturityWeighted) =>
        1.0 - u.MaxResearchCostReduction * Saturation(maturityWeighted);

    /// <summary>X: the sum of maturities of <paramref name="owner"/>'s universities of type
    /// <paramref name="typeKey"/>, table order.</summary>
    public static double MaturityWeighted(IReadOnlyWorldState world, PolityId owner, int typeKey) =>
        MaturityWeighted(world.Institutions, owner, typeKey);

    /// <summary>As above, over a table (InstitutionsSystem reads the rows it has just written).</summary>
    public static double MaturityWeighted(IReadOnlyTable<InstitutionRow> rows, PolityId owner, int typeKey)
    {
        double x = 0.0;
        for (int i = 0; i < rows.Count; i++)
        {
            InstitutionRow row = rows[i];
            if (row.Polity.Value != owner.Value || row.Type != typeKey) continue;
            x += row.Maturity;
        }
        return x;
    }

    /// <summary>
    /// MEDICAL COVERAGE of a settlement: Σ over medical institutions (table order) of
    /// M_i × e^(−d / healthDecayCostUnits), d = 0 at the university's own settlement and its
    /// SettlementDistances travel cost elsewhere (∞ — unreachable — contributes exactly 0). Local
    /// specialization and diffusion; no polity filter (medicine travels with practitioners).
    /// </summary>
    public static double MedicalCoverage(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (cfg.Institutions is not { } inst || world.Institutions.Count == 0) return 0.0;
        int health = InstitutionContent.HealthTypeKey(cfg);
        if (health < 0) return 0.0;
        double decay = inst.Universities.HealthDecayCostUnits;
        double coverage = 0.0;
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Type != health || !(row.Maturity > 0.0)) continue;
            double d = row.Settlement == settlement ? 0.0 : TravelCost(world, row.Settlement, settlement);
            if (double.IsPositiveInfinity(d)) continue;
            coverage += row.Maturity * Math.Exp(-d / decay);
        }
        return coverage;
    }

    /// <summary>
    /// THE ONE PER-SETTLEMENT HEALTH SEAM DemographicsSystem reads (once per settlement per turn, from
    /// PREV): the multiplier on every cohort's BASE mortality rate, μ = 1 − maxMortalityReduction ×
    /// (1 − e^(−coverage)) ∈ [1 − maxMortalityReduction, 1] — bounded and saturating. Exactly the literal
    /// 1.0 when no medical university covers the settlement, so m × μ == m bit for bit.
    /// </summary>
    public static double MortalityMultiplier(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        double coverage = MedicalCoverage(world, cfg, settlement);
        if (!(coverage > 0.0)) return 1.0;
        return 1.0 - cfg.Institutions!.Universities.MaxMortalityReduction * (1.0 - Math.Exp(-coverage));
    }

    private static double TravelCost(IReadOnlyWorldState world, SettlementId from, SettlementId to)
    {
        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = world.SettlementDistances[i];
            if (row.From == from && row.To == to) return row.TravelCost;
        }
        return double.PositiveInfinity;   // never measured: unreachable as far as the network knows
    }
}
