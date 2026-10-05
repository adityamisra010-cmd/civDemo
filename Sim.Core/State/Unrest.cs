using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Core.State;

/// <summary>
/// M5 integration R2b — D-021 UNREST-LITE: THE ONE PLACE GRIEVANCE BECOMES BEHAVIOUR. H2 (Director 2026-10-05
/// §4–§6, §17, §18, RATIFIED in docs/d049-taxation-and-revolt-model.md) re-founds it on the POPULATION SEGMENT.
///
/// TAXATION IS CONTINUOUS PRESSURE, NOT A SWITCH. The levy never acts on revolt directly. It is felt by each
/// population segment — one class in one settlement, the D-010 bucket with its own grievance — as a burden offset by
/// what that segment otherwise has and what the state returns to it (<see cref="FeltBurden"/>):
///
///   felt = r × (1 − taxBurdenOffsetMax × P) × (1 − taxServiceOffsetMax × V)
///     r  the settlement's EFFECTIVE rate (declared × reach, <see cref="Governance.EffectiveTaxRate"/>);
///     P  the segment's PROVISION — the D-035-B CES of its other bound needs: food (Sustenance), housing (Shelter),
///        comfort goods / amenities (Comfort) — R4a's offset, unchanged;
///     V  the settlement's PUBLIC SERVICES delivered (<see cref="ServiceOffset"/>): public works (granary, workshop)
///        and institutions (universities, maturity-weighted) at the settlement, delivered as far as the state's
///        administrative REACH — development, institutions and state capacity.
///
/// The felt burden injures Dignity (D-035-D, the published satisfaction 1 − felt) and ACCRUES into the segment's own
/// stock, the LEVY'S GRIEVANCE T (<see cref="TaxGrievanceRow"/>, NeedsGrievanceSystem): its accrual is what the
/// ratified aggregation (D-035-B CES over the class's bound needs, weights and W unchanged) charges for the levy
/// ALONE — every other need held at 1 — so it is never diluted, or zeroed, by the segment's other shortfalls (the R2b
/// attribution did exactly that: a poorly-off settlement never protested the tax). Lower P or V → higher felt →
/// MORE accrual: the same rate weighs heavier on the poor. T decays at the grievance's own rate (base + generational
/// turnover, D-021 §8) plus protest's discharge — so pressure ACCUMULATES over turns under a levy and RECOVERS after
/// it is cut, and nothing about it is instantaneous.
///
/// PER SEGMENT, from T (PREV), with onset o = protestOnsetGrievance and tipping point U = uprisingGrievance:
///   protest p = clamp((T − o) / (U − o), 0, 1);
///   RISEN — the segment's own TIPPING POINT — when T ≥ U; past it a growing PORTION of the segment is in open revolt,
///   the rebel fraction q = clamp((T − U) / (U − o), 0, 1): the rising spreads through the segment over the same
///   span of accumulated grievance over which its protest grew (an identity, not a second knob). Some portion of a
///   segment revolts, never automatically all of it.
/// AND THEIR CONSEQUENCES, each growing with amplitude (D-021 Part 1) and each exactly inert at p = 0:
///   OUTPUT: realised production × (1 − r × Σ share·[protestOutputDragMax·p·(1 − q) + q]) — the protesting members
///     of a segment withhold their share of the levied effort in proportion to their protest; its REBELS withhold
///     all of it (r × their share: strike and riot against the exaction, D-010 "riot: damage, strikes"). Untaxed
///     protest or rising drags nothing (the carrier is the levy), so hunger cannot feed itself through it;
///   DISCHARGE (D-021 valve 1): the segment's T decays faster by protestDischargePerYear × p, and the settlement's
///     needs grievance by the population-weighted protest;
///   HAPPINESS / LEGITIMACY: the settlement reading is scaled by 1 − <see cref="LevyPressure"/> (the population-
///     weighted T / uprisingGrievance, capped at 1) — happiness deteriorates as the pressure accumulates and recovers
///     as it decays (SettlementHappiness.TaxSufficiency), never at the moment of the edict;
///   UPRISING OF THE SETTLEMENT: only when its REBELS CARRY it — more than unrest.uprisingPopulationShare (a
///     majority) of its people (<see cref="IsUprising"/>) — does the settlement throw off its ruler (RevoltSystem; the
///     ruler's final settlement still never revolts away, D-048 ruling 5). A rebel MINORITY is a revolt of part of
///     a segment: their labour is withheld, nobody else's, and the place stays its ruler's.
///
/// THERE IS NO tax ≥ X → revolt RULE ANYWHERE. A 100 % levy is permitted; at full reach it is felt at most at 1, and
/// a segment rises only once its accumulated T crosses its tipping point — after turns, by an amount and at a speed
/// set by its own provision, services and reach (the measured tables are in d049).
///
/// READ ISOLATION (scripts/check-read-isolation.sh). This file is the single simulation-side reader of the grievance
/// tables outside their owner; ProductionSystem, RevoltSystem, SettlementHappiness and NeedsGrievanceSystem's
/// discharge consult THIS reader and never the tables.
///
/// INERT WITHOUT CONFIG. With no needs.json <c>unrest</c> section every reader returns the neutral value (protest 0,
/// output ×1, no discharge, no uprising, no pressure). An untaxed world holds no TaxGrievance row, so every reader is
/// neutral there too. Deterministic: rows walked in table order, no RNG, no dictionary.
/// </summary>
public static class Unrest
{
    /// <summary>
    /// The settlement's NEEDS grievance: the POPULATION-WEIGHTED mean of its per-class grievance rows (a class
    /// with no members carries no weight; the stock is held by people, T2.13). No people → 0.
    /// </summary>
    public static double Grievance(IReadOnlyWorldState world, SettlementId settlement)
    {
        double weighted = 0.0;
        long people = 0;
        for (int g = 0; g < world.Grievances.Count; g++)
        {
            GrievanceRow row = world.Grievances[g];
            if (row.Settlement.Value != settlement.Value) continue;
            long members = Members(world, settlement, row.Class);
            if (members <= 0) continue;
            double value = double.IsNaN(row.Value) ? 0.0 : row.Value;
            weighted += value * members;
            people += members;
        }

        return people > 0 ? weighted / people : 0.0;
    }

    /// <summary>The people of class <paramref name="cls"/> in <paramref name="settlement"/> (every cohort).</summary>
    public static long Members(IReadOnlyWorldState world, SettlementId settlement, ClassId cls)
    {
        long members = 0;
        for (int b = 0; b < world.Buckets.Count; b++)
        {
            BucketRow bucket = world.Buckets[b];
            if (bucket.Settlement.Value == settlement.Value && bucket.Class.Value == cls.Value)
                members += bucket.Count.Value;
        }
        return members;
    }

    /// <summary>The settlement's people (every class and cohort).</summary>
    public static long Population(IReadOnlyWorldState world, SettlementId settlement)
    {
        long people = 0;
        for (int b = 0; b < world.Buckets.Count; b++)
            if (world.Buckets[b].Settlement.Value == settlement.Value) people += world.Buckets[b].Count.Value;
        return people;
    }

    /// <summary>
    /// THE LEVY'S GRIEVANCE OF ONE SEGMENT — the accumulated tax pressure T of class <paramref name="cls"/> in
    /// <paramref name="settlement"/> (its <see cref="TaxGrievanceRow"/>; no row → 0, NaN → 0). First row by table
    /// order (the owner keeps one per segment).
    /// </summary>
    public static double SegmentTaxGrievance(IReadOnlyWorldState world, SettlementId settlement, ClassId cls)
    {
        for (int i = 0; i < world.TaxGrievances.Count; i++)
        {
            TaxGrievanceRow row = world.TaxGrievances[i];
            if (row.Settlement.Value != settlement.Value || row.Class.Value != cls.Value) continue;
            return double.IsNaN(row.Value) ? 0.0 : row.Value;
        }
        return 0.0;
    }

    /// <summary>
    /// THE LEVY'S GRIEVANCE OF A SETTLEMENT: the population-weighted mean of its segments' T (a class with no members
    /// carries no weight). 0 with no people, no row, or no unrest section.
    /// </summary>
    public static double TaxGrievance(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 0.0;
        return Weighted(world, settlement, Reading.Grievance, u);
    }

    /// <summary>PROTEST INTENSITY of one segment in [0, 1], from its T (<see cref="ProtestOf"/>).</summary>
    public static double SegmentProtest(IReadOnlyWorldState world, SettlementId settlement, ClassId cls, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        return ProtestOf(SegmentTaxGrievance(world, settlement, cls), u);
    }

    /// <summary>
    /// THE SETTLEMENT'S PROTEST in [0, 1]: the population-weighted mean of its segments' protest intensities. 0 when
    /// the config carries no unrest section or no segment holds a levy grievance.
    /// </summary>
    public static double Protest(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 0.0;
        return Weighted(world, settlement, Reading.Protest, u);
    }

    /// <summary>The protest law on a grievance value (pure; the one expression every caller uses).</summary>
    public static double ProtestOf(double grievance, UnrestTuning u)
    {
        ArgumentNullException.ThrowIfNull(u);
        if (!(grievance > u.ProtestOnsetGrievance)) return 0.0;
        return Math.Clamp((grievance - u.ProtestOnsetGrievance) / (u.UprisingGrievance - u.ProtestOnsetGrievance), 0.0, 1.0);
    }

    /// <summary>
    /// THE SEGMENT'S TIPPING POINT: class <paramref name="cls"/> in <paramref name="settlement"/> has RISEN against the
    /// levy — it has members and its accumulated T stands at or above unrest.uprisingGrievance. A state of the
    /// segment only; whether the settlement follows is <see cref="IsUprising"/>.
    /// </summary>
    public static bool IsSegmentRisen(IReadOnlyWorldState world, SettlementId settlement, ClassId cls, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return false;
        if (Members(world, settlement, cls) <= 0) return false;
        return SegmentTaxGrievance(world, settlement, cls) >= u.UprisingGrievance;
    }

    /// <summary>The share of the settlement's people in RISEN segments (past their tipping point), in [0, 1].</summary>
    public static double RisenShare(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 0.0;
        return Weighted(world, settlement, Reading.Risen, u);
    }

    /// <summary>The REBEL FRACTION of a segment with levy grievance <paramref name="grievance"/>: 0 below its tipping
    /// point U, then clamp((T − U) / (U − onset), 0, 1) — the portion of the segment in open revolt (pure).</summary>
    public static double RebelFractionOf(double grievance, UnrestTuning u)
    {
        ArgumentNullException.ThrowIfNull(u);
        if (!(grievance > u.UprisingGrievance)) return 0.0;
        return Math.Clamp((grievance - u.UprisingGrievance) / (u.UprisingGrievance - u.ProtestOnsetGrievance), 0.0, 1.0);
    }

    /// <summary>The rebel fraction of one segment (<see cref="RebelFractionOf"/> on its T).</summary>
    public static double SegmentRebelFraction(IReadOnlyWorldState world, SettlementId settlement, ClassId cls, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        return RebelFractionOf(SegmentTaxGrievance(world, settlement, cls), u);
    }

    /// <summary>THE SETTLEMENT'S REBELS as a share of its people, in [0, 1]: Σ over segments of share × rebel
    /// fraction. 0 with no people, no levy grievance, or no unrest section.</summary>
    public static double RebelShare(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 0.0;
        return Weighted(world, settlement, Reading.Rebel, u);
    }

    /// <summary>
    /// THE SETTLEMENT'S UPRISING (RevoltSystem executes it — this reader never writes): its REBELS CARRY it, i.e. are
    /// MORE than unrest.uprisingPopulationShare of its people. Rebels short of that do not take the settlement with
    /// them: the revolt stays theirs.
    /// </summary>
    public static bool IsUprising(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return false;
        return RebelShare(world, settlement, cfg) > u.UprisingPopulationShare;
    }

    /// <summary>
    /// The factor protest and revolt leave on realised production: 1 − r × Σ share·[protestOutputDragMax·p·(1 − q) + q]
    /// — each segment's protesting members withhold their share of the LEVIED effort in proportion to its protest, its
    /// rebels withhold all of it; r the settlement's EFFECTIVE tax rate. EXACTLY 1.0 with no protest or no levy, so a
    /// quiet or untaxed world produces bit-identically.
    /// </summary>
    public static double OutputFactor(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 1.0;
        double r = Governance.EffectiveTaxRate(world, settlement, cfg);
        if (r <= 0.0) return 1.0;
        double withheld = Weighted(world, settlement, Reading.Withheld, u);
        if (withheld <= 0.0) return 1.0;
        return Math.Clamp(1.0 - r * withheld, 0.0, 1.0);
    }

    /// <summary>
    /// D-021 valve 1 for the settlement's NEEDS grievance — the extra decay rate (per year) its population-weighted
    /// protest contributes: protestDischargePerYear × <see cref="Protest"/>. 0 with no protest (the decay is then
    /// exactly the pre-R2b rate).
    /// </summary>
    public static double DischargePerYear(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        double p = Protest(world, settlement, cfg);
        return p > 0.0 ? u.ProtestDischargePerYear * p : 0.0;
    }

    /// <summary>D-021 valve 1 for ONE SEGMENT's levy grievance: protestDischargePerYear × its own protest.</summary>
    public static double SegmentDischargePerYear(IReadOnlyWorldState world, SettlementId settlement, ClassId cls, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        double p = ProtestOf(SegmentTaxGrievance(world, settlement, cls), u);
        return p > 0.0 ? u.ProtestDischargePerYear * p : 0.0;
    }

    /// <summary>
    /// THE ACCUMULATED LEVY PRESSURE on a settlement's welfare, in [0, 1]: the population-weighted mean of its
    /// segments' min(1, T / uprisingGrievance). It is what scales happiness (and so legitimacy): 0 before any levy has
    /// been felt (EXACTLY 0 in an untaxed world — happiness is then bit-identical), rising as the pressure accumulates,
    /// 1 when every segment has risen, and falling back as the memory decays once the levy is cut.
    /// </summary>
    public static double LevyPressure(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u || world.TaxGrievances.Count == 0) return 0.0;
        return Math.Clamp(Weighted(world, settlement, Reading.Pressure, u), 0.0, 1.0);
    }

    /// <summary>
    /// PUBLIC SERVICES, DEVELOPMENT, INSTITUTIONS AND STATE CAPACITY as the levy's offset, in [0, 1): what the state
    /// returns for what it takes (the fiscal exchange), delivered as far as it reaches,
    ///
    ///   V = reach × (1 − e^(−X)),   X = Σ completed public works at the settlement (goods.json projects that found no
    ///                                   institution — the granary, the public store the levy-in-kind filled, and the
    ///                                   workshop) + Σ maturity of the institutions it hosts (universities),
    ///
    /// with reach the controller's stored administrative reach there (<see cref="Governance.ControlStrength"/> — state
    /// capacity; roads raise it, D-040 C6) and 1 − e^(−X) ADR-028's saturating effect curve
    /// (<see cref="InstitutionEffects.Saturation"/>: each work adds less than the one before). 0 for an uncontrolled
    /// settlement and for one with nothing built. Real state only; services with no state yet (temples, police,
    /// sanitation, schools) are not invented here (d049 lists them as NOT WIRED).
    /// </summary>
    public static double ServiceOffset(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId ruler)) return 0.0;
        double reach = Governance.ControlStrength(world, ruler, settlement);
        if (!(reach > 0.0)) return 0.0;
        double x = 0.0;
        ConstructionProjectEntry[]? projects = cfg.Goods?.Projects;
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow row = world.Structures[i];
            if (row.Settlement.Value != settlement.Value || row.Count <= 0) continue;
            bool founds = false;
            if (projects is not null)
                foreach (ConstructionProjectEntry p in projects)
                    if (p.Id == row.ProjectId) { founds = p.Founds is not null; break; }
            if (!founds) x += row.Count;    // a university's building is counted through its institution's maturity
        }
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Settlement.Value == settlement.Value && row.Maturity > 0.0) x += row.Maturity;
        }
        return x > 0.0 ? reach * InstitutionEffects.Saturation(x) : 0.0;
    }

    /// <summary>
    /// THE FELT BURDEN of a levy on a segment with provision <paramref name="provision"/>:
    /// r × (1 − taxBurdenOffsetMax × P) × (1 − taxServiceOffsetMax × V), in [0, 1]. EXACTLY 0 untaxed. Coefficients
    /// inside the resolution equation (law 2), not buffs: each scales the felt burden and none acts without a levy.
    /// </summary>
    public static double FeltBurden(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg, double provision)
    {
        double r = Governance.EffectiveTaxRate(world, settlement, cfg);
        if (r <= 0.0) return 0.0;
        UnrestTuning? u = cfg.Needs?.Unrest;
        double offsetMax = u?.TaxBurdenOffsetMax ?? 0.0;
        double felt = offsetMax > 0.0 ? r * (1.0 - offsetMax * Math.Clamp(provision, 0.0, 1.0)) : r;
        double serviceMax = u?.TaxServiceOffsetMax ?? 0.0;
        if (serviceMax > 0.0)
        {
            double v = ServiceOffset(world, settlement, cfg);
            if (v > 0.0) felt *= 1.0 - serviceMax * Math.Clamp(v, 0.0, 1.0);
        }
        return Math.Clamp(felt, 0.0, 1.0);
    }

    private enum Reading { Grievance, Protest, Risen, Rebel, Withheld, Pressure }

    /// <summary>The share of a segment's LEVIED effort it withholds: drag × protest from its non-rebels, all of it from
    /// its rebels — protestOutputDragMax·p·(1 − q) + q.</summary>
    private static double Withheld(double t, UnrestTuning u)
    {
        double q = RebelFractionOf(t, u);
        return u.ProtestOutputDragMax * ProtestOf(t, u) * (1.0 - q) + q;
    }

    /// <summary>The population-weighted mean over the settlement's segments of a reading of their T: each segment
    /// (TaxGrievance row, one per (settlement, class)) weighs by its members, over the settlement's WHOLE population —
    /// a class with no levy grievance contributes zero. 0 with no people.</summary>
    private static double Weighted(IReadOnlyWorldState world, SettlementId settlement, Reading reading, UnrestTuning u)
    {
        long people = Population(world, settlement);
        if (people <= 0) return 0.0;
        double weighted = 0.0;
        for (int i = 0; i < world.TaxGrievances.Count; i++)
        {
            TaxGrievanceRow row = world.TaxGrievances[i];
            if (row.Settlement.Value != settlement.Value) continue;
            double t = double.IsNaN(row.Value) ? 0.0 : row.Value;
            if (!(t > 0.0)) continue;
            long members = Members(world, settlement, row.Class);
            if (members <= 0) continue;
            double value = reading switch
            {
                Reading.Grievance => t,
                Reading.Protest => ProtestOf(t, u),
                Reading.Risen => t >= u.UprisingGrievance ? 1.0 : 0.0,
                Reading.Rebel => RebelFractionOf(t, u),
                Reading.Withheld => Withheld(t, u),
                _ => Math.Min(1.0, t / u.UprisingGrievance),
            };
            weighted += value * members;
        }
        return weighted / people;
    }
}
