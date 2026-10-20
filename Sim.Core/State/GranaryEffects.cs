using System.Globalization;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// M5 item 5 (D-053 §3, the Director's 2026-10-13 instruction, item 5) — WHAT A GRANARY DOES (sim.json
/// <c>granaries</c>; reference classes in its <c>_doc</c>). Two distinct effects of the built Structures of the goods.json
/// project the section names, each bounded and saturating on the ADR-028 curve S(x) = 1 − e^(−x)
/// (<see cref="InstitutionEffects.Saturation"/>; the <see cref="InstitutionEffects.MortalityMultiplier"/> precedent: a
/// coefficient INSIDE a resolution equation, driven by real structures, and the literal 1.0 when absent):
/// <list type="number">
/// <item><b>LOCAL FARM OUTPUT</b> — <see cref="FarmYieldFactor(GranariesConfig, long)"/> = 1 + farmYieldBonusMax ×
///   S(m / farmYieldScale), m = the granaries standing AT the settlement. ProductionSystem.Farm multiplies the
///   settlement's REALISED grain rate by it (after the Leontief minimum, beside weather and extraction, once) — but only
///   on CULTIVATED grain: while the settlement still harvests WILD FOOD (<see cref="LabourActivities.HarvestsWildFood"/>,
///   the same predicate that picks the forager rates) the factor is the literal 1.0 and is not applied. The mechanism is
///   the post-harvest loss of the settlement's OWN crop between the threshing floor and the store, part of which a
///   dedicated store saves: it recovers at most about half of the ×0.90 storage, vermin and threshing loss ADR-013
///   already deducts inside the derived yield (the storage/vermin share; threshing loss stays). It is local by
///   construction: a granary in A saves A's crop, never its sibling B's.</item>
/// <item><b>EMPIRE-WIDE FAMINE INTENSITY</b> — <see cref="FamineIntensityFactor(GranariesConfig, long)"/> = 1 −
///   famineReductionMax × S(n / famineReductionScale), n = the granaries standing anywhere in the settlement's
///   civilization (every settlement its controller commands, <see cref="EmpireQuery.TryGetController"/>; a stateless
///   settlement counts only its own). DemographicsSystem multiplies the EFFECTIVE deficit — the one input both
///   exceptional channels read (starvation mortality and fertility suppression, CR-015 G3(b)) — by it. NO GRAIN MOVES:
///   the coefficient stands for the civilization's ORGANISED RELIEF CAPACITY (the administration that rations,
///   distributes and directs relief in a shortage — the ever-normal granary system as an institution), not for stored
///   grain, so it makes no storage claim and no grain is credited, debited or transferred by it. n is an ABSOLUTE count:
///   one granary relieves a one-settlement and a sixty-settlement civilization alike. The NOMINAL deficit, the
///   FoodState classification, flight, happiness and every other reader of the deficit are untouched.</item>
/// </list>
/// NEITHER EFFECT IS STORAGE: a granary does not enlarge the store (consumption.granaryYearsOfDemand) and does not slow
/// its spoilage (consumption.grainSpoilagePerYear); ConsumptionSystem never reads this class.
///
/// Both factors are the literal 1.0 when the section is absent, switched off (<c>enabled</c> false), the project does
/// not resolve, or the count is 0 — and the two systems then skip the multiplication entirely, so every world without a
/// granary (every calibration world) computes the pre-granary instruction sequence. The two systems resolve the
/// project ONCE, at construction (<see cref="Bind"/>), so the per-settlement, per-turn seams scan no project names.
/// Pure: every caller passes the world it reads (systems pass PREV); table-order scans, no dictionary, no LINQ (law 5).
/// </summary>
public static class GranaryEffects
{
    /// <summary>The ACTIVE granary content: the section when present and switched on, else null.</summary>
    public static GranariesConfig? Active(SimConfig cfg) => cfg.Granaries is { Enabled: true } g ? g : null;

    /// <summary>The goods.json project id the granaries section names (resolved from content by name, never a code
    /// literal), or -1 when there is no section, no goods content or no such project. A string scan over the projects:
    /// the hot seams take a <see cref="GranaryBinding"/> resolved once per config instead.</summary>
    public static int ProjectId(SimConfig cfg)
    {
        if (cfg.Granaries is not { } g || cfg.Goods?.Projects is not { } projects) return -1;
        foreach (ConstructionProjectEntry p in projects)
            if (string.Equals(p.Name, g.Project, StringComparison.Ordinal)) return p.Id;
        return -1;
    }

    /// <summary>The granary content resolved ONCE for <paramref name="cfg"/>: the active section (null when absent or
    /// switched off) and the project id (-1 when it does not resolve). ProductionSystem and DemographicsSystem hold one
    /// from construction, so the per-settlement seams never scan project names.</summary>
    public static GranaryBinding Bind(SimConfig cfg) => new(Active(cfg), ProjectId(cfg));

    /// <summary>m: the granaries standing AT <paramref name="settlement"/> (its Structures count of the granary project;
    /// 0 when the project does not resolve). Queued granaries are not counted — only built Structures.</summary>
    public static long LocalGranaries(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        CountAt(world, ProjectId(cfg), settlement);

    /// <summary>n: the granaries standing anywhere in <paramref name="settlement"/>'s civilization (see the project-id
    /// overload). 0 when the project does not resolve.</summary>
    public static long EmpireGranaries(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        EmpireGranaries(world, ProjectId(cfg), settlement);

    /// <summary>
    /// n: the granaries (Structures of <paramref name="project"/>) standing anywhere in <paramref name="settlement"/>'s
    /// civilization — Σ over every settlement whose controller (<see cref="EmpireQuery.TryGetController"/>, the
    /// lowest-polity rule, so the answer is a function of the row SET) is the settlement's own controller. A stateless
    /// settlement is its own civilization: n = its local count. 0 when <paramref name="project"/> is negative.
    /// </summary>
    public static long EmpireGranaries(IReadOnlyWorldState world, int project, SettlementId settlement)
    {
        if (project < 0) return 0;
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId ruler)) return CountAt(world, project, settlement);
        long n = 0;
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            if (row.ProjectId != project || row.Count <= 0) continue;
            if (row.Settlement.Value == settlement.Value) { n = checked(n + row.Count); continue; }
            if (EmpireQuery.TryGetController(world, row.Settlement, out PolityId owner) && owner.Value == ruler.Value)
                n = checked(n + row.Count);
        }
        return n;
    }

    /// <summary>The LOCAL farm-output factor for m granaries at one settlement: 1 + farmYieldBonusMax ×
    /// (1 − e^(−m / farmYieldScale)) ∈ [1, 1 + farmYieldBonusMax]. Diminishing, as MEASURED on this double-precision
    /// function (<see cref="FarmStrictlyLessBelow"/>): each granary #2 … #(N − 1) adds STRICTLY less than the one before it
    /// (N = 35 at the shipped 0.05 / 1.0); from #N on the increments are rounding noise of a few 1e-16, not monotone, so
    /// "never adds more than the one before" is NOT claimed past N; and the factor never passes its ceiling, which it
    /// reaches only once e^(−m/scale) vanishes below the factor's precision. Exactly the literal 1.0 for m ≤ 0.</summary>
    public static double FarmYieldFactor(GranariesConfig g, long m) =>
        m > 0 ? 1.0 + g.FarmYieldBonusMax * InstitutionEffects.Saturation(m / g.FarmYieldScale) : 1.0;

    /// <summary>The EMPIRE-WIDE famine-intensity factor for n granaries in a civilization: 1 − famineReductionMax ×
    /// (1 − e^(−n / famineReductionScale)) ∈ [1 − famineReductionMax, 1]. Diminishing, as MEASURED on this double-precision
    /// function (<see cref="FamineStrictlyLessBelow"/>): each granary #2 … #(N − 1) reduces STRICTLY less than the one
    /// before it (N = 69 at the shipped 0.25 / 2.0); from #N on the steps are rounding noise, not monotone, so "never
    /// reduces more than the one before" is NOT claimed past N; never below its floor (reached only once e^(−n/scale)
    /// vanishes below the factor's precision), never above 1. Exactly the literal 1.0 for n ≤ 0.</summary>
    public static double FamineIntensityFactor(GranariesConfig g, long n) =>
        n > 0 ? 1.0 - g.FamineReductionMax * InstitutionEffects.Saturation(n / g.FamineReductionScale) : 1.0;

    /// <summary>THE LOCAL SEAM, for readers outside the system (the causal chain, the cards): the farm-output factor in
    /// force at <paramref name="settlement"/> on <paramref name="world"/> — the literal 1.0 when the effects are off, no
    /// granary stands here, or the settlement still harvests wild food. The same function ProductionSystem.Farm applies
    /// (<see cref="FarmYieldFactor(IReadOnlyWorldState, GranaryBinding, SettlementId, bool)"/>).</summary>
    public static double FarmYieldFactor(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (Active(cfg) is null) return 1.0;
        return FarmYieldFactor(world, Bind(cfg), settlement, LabourActivities.HarvestsWildFood(world, cfg, settlement));
    }

    /// <summary>
    /// THE LOCAL SEAM ProductionSystem.Farm applies (from PREV, with the binding it resolved at construction and the
    /// wild-food predicate it already evaluated for the yield choice): the literal 1.0 when
    /// <paramref name="harvestsWildFood"/> (pre-cultivation gathering is not the crop a granary saves), when the effects
    /// are off or the project does not resolve, or when no granary stands here; otherwise
    /// <see cref="FarmYieldFactor(GranariesConfig, long)"/> of the local count.
    /// </summary>
    public static double FarmYieldFactor(IReadOnlyWorldState world, GranaryBinding binding, SettlementId settlement, bool harvestsWildFood)
    {
        if (harvestsWildFood || binding.Active is not { } g || binding.Project < 0) return 1.0;
        long m = CountAt(world, binding.Project, settlement);
        return m > 0 ? FarmYieldFactor(g, m) : 1.0;
    }

    /// <summary>THE EMPIRE-WIDE SEAM, for readers outside the system: this settlement's famine-intensity factor on
    /// <paramref name="world"/>, the literal 1.0 when the effects are off or its civilization has no granary.</summary>
    public static double FamineIntensityFactor(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) =>
        FamineIntensityFactor(world, Bind(cfg), settlement);

    /// <summary>THE EMPIRE-WIDE SEAM the effective deficit reads (from PREV; DemographicsSystem passes the binding it
    /// resolved at construction): the literal 1.0 when the effects are off, the project does not resolve, or the
    /// civilization has no granary.</summary>
    public static double FamineIntensityFactor(IReadOnlyWorldState world, GranaryBinding binding, SettlementId settlement)
    {
        if (binding.Active is not { } g || binding.Project < 0) return 1.0;
        long n = EmpireGranaries(world, binding.Project, settlement);
        return n > 0 ? FamineIntensityFactor(g, n) : 1.0;
    }

    /// <summary>The upper search bound of the two horizons below (granaries in one place or one civilization).</summary>
    public const long HorizonLimit = 10_000;

    /// <summary>
    /// N for the LOCAL factor: every granary #2 … #(N − 1) at one settlement adds STRICTLY less than the one before it,
    /// and granary #N is the first that does not — measured on <see cref="FarmYieldFactor(GranariesConfig, long)"/>
    /// itself in double precision (35 at the shipped 0.05 / 1.0). Beyond N the increments are rounding noise (a few
    /// 1e-16, not monotone) and the factor sits at its ceiling: the cards say "strictly less below about N" and
    /// "nothing that shows after", never "each further one adds less" without a bound. <see cref="HorizonLimit"/> + 1
    /// when no such granary exists below the search bound.
    /// </summary>
    public static long FarmStrictlyLessBelow(GranariesConfig g)
    {
        double prev = FarmYieldFactor(g, 1) - FarmYieldFactor(g, 0);
        for (long k = 2; k <= HorizonLimit; k++)
        {
            double step = FarmYieldFactor(g, k) - FarmYieldFactor(g, k - 1);
            if (!(step < prev)) return k;
            prev = step;
        }
        return HorizonLimit + 1;
    }

    /// <summary>N for the EMPIRE-WIDE factor, defined as <see cref="FarmStrictlyLessBelow"/> on the reduction each granary
    /// adds to <see cref="FamineIntensityFactor(GranariesConfig, long)"/> (69 at the shipped 0.25 / 2.0).</summary>
    public static long FamineStrictlyLessBelow(GranariesConfig g)
    {
        double prev = FamineIntensityFactor(g, 0) - FamineIntensityFactor(g, 1);
        for (long k = 2; k <= HorizonLimit; k++)
        {
            double step = FamineIntensityFactor(g, k - 1) - FamineIntensityFactor(g, k);
            if (!(step < prev)) return k;
            prev = step;
        }
        return HorizonLimit + 1;
    }

    /// <summary>The farm-output bonus as a percentage of the realised harvest for m granaries ("+3.2"), invariant culture —
    /// for the cards that state the effect.</summary>
    public static string FarmBonusPercent(GranariesConfig g, long m) =>
        Percent(FarmYieldFactor(g, m) - 1.0);

    /// <summary>The famine-intensity reduction as a percentage for n granaries ("9.8"), invariant culture.</summary>
    public static string FamineReductionPercent(GranariesConfig g, long n) =>
        Percent(1.0 - FamineIntensityFactor(g, n));

    /// <summary>A share as a percentage with one decimal, invariant culture (0.0316 → "3.2").</summary>
    public static string Percent(double share) => (share * 100.0).ToString("0.0", CultureInfo.InvariantCulture);

    private static long CountAt(IReadOnlyWorldState world, int project, SettlementId settlement)
    {
        if (project < 0) return 0;
        long m = 0;
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            if (row.ProjectId == project && row.Settlement.Value == settlement.Value && row.Count > 0) m = checked(m + row.Count);
        }
        return m;
    }
}

/// <summary>M5 item 5: the granary content resolved once per config (<see cref="GranaryEffects.Bind"/>) — the active
/// section (null when absent or switched off) and the goods.json project id (-1 when it does not resolve).</summary>
public readonly record struct GranaryBinding(GranariesConfig? Active, int Project);
