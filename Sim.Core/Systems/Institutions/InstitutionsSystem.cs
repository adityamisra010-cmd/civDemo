using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Core.Systems.Institutions;

/// <summary>Tables owned by <see cref="InstitutionsSystem"/> (ADR-033 D6): the institution instances and
/// the specialized-university cost seam (ADR-029 §9), of which it is the ONE writer.</summary>
public sealed record InstitutionsTables(
    Table<InstitutionRow> Institutions, Table<ResearchCostModifierRow> CostModifiers);

/// <summary>
/// ADR-033 D6 — UNIVERSITIES AS REAL INSTITUTIONS on ADR-028's lifecycle
/// (docs/institutions-universities.md). SystemId 28, last in pipeline.json: it reads only PREV and writes
/// only its own two tables, which no other system reads in the same step, so its slot changes no other
/// system's result.
///
/// THE STEP, all signals from PREV:
///  1. MATURE every institution that existed in PREV, in table order: while its host is VIABLE to sustain
///     (InstitutionViability.ToSustain — the market and the content's food-surplus threshold), the exact
///     closed form M' = 1 − (1 − M)·e^(−dt/τ_grow); while not (hunger, population loss), M' = M·e^(−dt/τ_decay).
///  2. FOUND one row per university building ConstructionSystem completed and nobody founded yet: per
///     settlement (table order) and founding project (goods.json order), the project's Structures count in
///     PREV minus the rows of its type there. Id = one past the highest id held; Polity = the settlement's
///     controller in PREV (0 when stateless); FoundedTurn = the turn of the state being written; Maturity =
///     0 — a new university has no effect (no instant magic). An order stamped t is built in the step
///     t→t+1 and founded in t+1→t+2: the one-turn lag of single-writer ownership, pinned turn-exactly.
///  3. EFFECTS — THE RESEARCH COST SEAM: ResearchCostModifiers is REBUILT from the maturities just written —
///     one row per (owner, type) with maturity-weighted count X > 0, owners in roster order, types in
///     content order, Factor = 1 − maxResearchCostReduction × (1 − e^(−X)) ∈ (0, 1]. The table in any state
///     is therefore a pure function of that state's Institutions rows; ResearchSystem reads it from PREV and
///     the ADR-029 seam applies each factor only to Technology nodes of the type's subtree (floor 20 %,
///     Eureka ceiling 40 % of BASE untouched). The health effect is a per-settlement READ
///     (InstitutionEffects.MortalityMultiplier, DemographicsSystem) and needs no stored row.
///
/// D-021 (the paired-feedback rule): this system closes the research → university loop, and its brake ships
/// with it — staff are withdrawn from every sector pool through InstitutionStaffing.LabourAdults (linear in
/// Σ maturity, the loop's amplitude), which lowers output and the food surplus the sustaining test reads,
/// so maturity decays where the institutions outgrow what the settlement can feed (InstitutionBrakeTests).
///
/// INERT without an institutions section, research content or goods content (toy and hand-written
/// configs). No RNG, no Ledger flow (no people, money or goods move — scholars stay in their buckets), no
/// dictionary, no ordering over doubles (law 5).
/// </summary>
public sealed class InstitutionsSystem(SimConfig cfg) : ISimSystem<InstitutionsTables>
{
    public static readonly SystemId WellKnownId = new(28);
    public const string Name = "institutions";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<InstitutionsTables> ctx)
    {
        if (_cfg.Institutions is not { } institutions || _cfg.Research is not { } research
            || _cfg.Goods?.Projects is not { } projects) return;
        UniversitiesConfig u = institutions.Universities;
        IReadOnlyWorldState prev = ctx.Prev;
        Table<InstitutionRow> table = ctx.Owned.Institutions;

        // 1. MATURE the rows PREV held (NEXT starts as PREV's clone, so they are rows [0, held)).
        int held = table.Count;
        for (int i = 0; i < held; i++)
        {
            ref InstitutionRow row = ref table.Ref(i);
            row.Maturity = InstitutionViability.ToSustain(prev, _cfg, row.Settlement).Viable
                ? InstitutionEffects.Grow(row.Maturity, ctx.DtYears, u.MaturityTauYears)
                : InstitutionEffects.Decay(row.Maturity, ctx.DtYears, u.DecayTauYears);
        }

        // 2. FOUND what ConstructionSystem built and nobody founded yet.
        long foundedTurn = prev.Clock.Turn + 1;
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;
            foreach (ConstructionProjectEntry project in projects)
            {
                int type = InstitutionContent.TypeKeyOf(research, project);
                if (type < 0) continue;
                long built = ConstructionQuery.Built(prev, settlement, project.Id);
                long have = Count(table, settlement, type);
                if (built <= have) continue;
                PolityId owner = EmpireQuery.TryGetController(prev, settlement, out PolityId controller) ? controller : new PolityId(0);
                for (long k = have; k < built; k++)
                    table.Add(new InstitutionRow(NextId(table), owner, settlement, type, foundedTurn, 0.0));
            }
        }

        // 3. EFFECTS — rebuild the research cost seam from the maturities just written.
        Table<ResearchCostModifierRow> modifiers = ctx.Owned.CostModifiers;
        modifiers.Clear();
        var seen = new List<int>();
        for (int p = 0; p < prev.Polities.Count; p++)
        {
            PolityId polity = prev.Polities[p].Id;
            if (seen.Contains(polity.Value)) continue;   // a duplicated roster row counts once
            seen.Add(polity.Value);
            foreach (UniversityType type in research.UniversityTypes)
            {
                double x = InstitutionEffects.MaturityWeighted(ctx.Owned.Institutions, polity, type.Key);
                if (!(x > 0.0)) continue;
                modifiers.Add(new ResearchCostModifierRow(polity, type.Key, InstitutionEffects.ResearchFactor(u, x)));
            }
        }
    }

    private static long Count(Table<InstitutionRow> table, SettlementId settlement, int type)
    {
        long n = 0;
        for (int i = 0; i < table.Count; i++)
            if (table[i].Settlement == settlement && table[i].Type == type) n++;
        return n;
    }

    /// <summary>One past the highest id the table holds (1 for an empty table). Rows are never removed in
    /// this pass, so an id is never reused.</summary>
    private static int NextId(Table<InstitutionRow> table)
    {
        int next = 1;
        for (int i = 0; i < table.Count; i++)
            if (table[i].Id >= next) next = table[i].Id + 1;
        return next;
    }
}
