using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Core.State;

/// <summary>
/// M5 integration R2b — D-021 UNREST-LITE: THE ONE PLACE GRIEVANCE BECOMES BEHAVIOUR.
///
/// D-021 ruled that grievance drives no behaviour until M5 ships the unrest valves "with the gas
/// pedal", and the M5 gas pedal is the tax (ADR-033 D4). This pure reader is the brake that ships
/// with it: a settlement's grievance stock (NeedsGrievanceSystem, fed since R2b by the Dignity need
/// through D-035-D's tax-burden carrier) expresses as PROTEST once it passes an onset, and protest
/// has three consequences, each growing with its intensity (D-021 Part 1: the negative loop must
/// STRENGTHEN WITH AMPLITUDE):
///
///   the LEVY'S grievance G_tax (<see cref="TaxGrievance"/>, PREV) → protest p = clamp((G_tax − onset) / (uprising − onset), 0, 1)
///     → OUTPUT: realised production × (1 − dragMax × p × r), r the effective tax rate (ProductionSystem, via
///       <see cref="Governance.OutputMultiplier"/>; d009 "protest (production drag)")
///     → DISCHARGE: the grievance decay rate gains discharge × p     (NeedsGrievanceSystem; D-021 valve
///       1, "expression discharges pressure" — the outburst vents the stock even when nothing was fixed,
///       which is what makes an episode END)
///     → UPRISING: at p = 1 (G_tax ≥ uprising) the settlement throws off its ruler (RevoltSystem drops the
///       control relation) — revolt reachable before total deprivation.
///
/// AND THE LOOP CLOSES THROUGH THE TAX: an uprising ends the levy on that settlement (no controller,
/// effective rate 0, Dignity satisfied), so accrual falls back and the discharge drains the stock.
///
/// WHY READ GRIEVANCE AND NOT HAPPINESS. Happiness (<see cref="SettlementHappiness"/>) is a memoryless
/// reading of the CURRENT conditions; D-021 Part 5 rules the opposite for politics ("people remember").
/// Grievance is the ratified memory stock — accrued per year with dt, decaying generationally — so
/// protest builds over years of exaction and outlasts its cause for a while, instead of flicking on and
/// off with each turn's levy.
///
/// READ ISOLATION (scripts/check-read-isolation.sh). This file is the single simulation-side reader of
/// the grievance table outside its owner; ProductionSystem, RevoltSystem and NeedsGrievanceSystem's
/// discharge consult THIS reader and never the table. The gate's allowlist names it with that reason.
///
/// INERT WITHOUT CONFIG. With no needs.json <c>unrest</c> section every reader returns the neutral value
/// (protest 0, output ×1, no discharge, no uprising), so hand-written configs behave as before R2b.
/// Deterministic: rows walked in table order, no RNG, no dictionary.
/// </summary>
public static class Unrest
{
    /// <summary>
    /// The settlement's grievance: the POPULATION-WEIGHTED mean of its per-class grievance rows (a class
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
            long members = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
            {
                BucketRow bucket = world.Buckets[b];
                if (bucket.Settlement.Value == settlement.Value && bucket.Class.Value == row.Class.Value)
                    members += bucket.Count.Value;
            }
            if (members <= 0) continue;
            double value = double.IsNaN(row.Value) ? 0.0 : row.Value;
            weighted += value * members;
            people += members;
        }

        return people > 0 ? weighted / people : 0.0;
    }

    /// <summary>
    /// THE GRIEVANCE THE LEVY CARRIES — the part of the settlement's grievance stock that is the tax's: per
    /// class, G × the share of the aggregate shortfall that lifting the levy would close,
    ///
    ///   share = (S(Dignity := 1) − S) / (1 − S)      clamped to [0, 1],
    ///
    /// with S the D-035-B aggregate of the class's published satisfactions (PREV's NeedSatisfactions,
    /// recomputed through <see cref="NeedsGrievanceSystem.AggregateSatisfaction"/> — the system's own
    /// function, never a copy), population-weighted over classes. It is ZERO wherever the Dignity need is
    /// fully met (no levy) or publishes nothing (no tax instrument), so grievance from other needs —
    /// pottery and cloth before they are known, a monotonous diet — never ignites a protest against a
    /// ruler who takes nothing; and a levy on an already-aggrieved settlement is charged only with the part
    /// of the stock its own exaction explains. WHY A SHARE OF THE STOCK AND NOT A SECOND STOCK: the stock is
    /// the ratified memory (D-021); a second, tax-only stock would be a new serialized table the M5 brake
    /// does not need. The share is the CURRENT attribution (an INFERRED simplification: grievance accrued
    /// under a past levy is re-attributed by today's shortfalls), stated rather than hidden.
    /// </summary>
    public static double TaxGrievance(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        NeedsConfig? needs = cfg.Needs;
        if (needs is null) return 0.0;
        int dignityId = -1;
        for (int n = 0; n < needs.Needs.Length; n++)
            if (needs.Needs[n].Bound && needs.Needs[n].FromTaxBurden) { dignityId = needs.Needs[n].Id; break; }
        if (dignityId < 0) return 0.0;

        int count = needs.Needs.Length;
        var sat = new double[count];
        var weight = new double[count];
        var gate = new bool[count];
        var adjusted = new double[count];
        double weighted = 0.0;
        long people = 0;
        for (int g = 0; g < world.Grievances.Count; g++)
        {
            GrievanceRow row = world.Grievances[g];
            if (row.Settlement.Value != settlement.Value) continue;
            long members = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
            {
                BucketRow bucket = world.Buckets[b];
                if (bucket.Settlement.Value == settlement.Value && bucket.Class.Value == row.Class.Value)
                    members += bucket.Count.Value;
            }
            if (members <= 0) continue;
            people += members;

            // The class's published satisfactions, registry order (the system's own sat[] order).
            int bound = 0, dignitySlot = -1;
            for (int n = 0; n < count; n++)
            {
                NeedEntry need = needs.Needs[n];
                if (!need.Bound) continue;
                int found = -1;
                for (int i = 0; i < world.NeedSatisfactions.Count; i++)
                {
                    NeedSatisfactionRow r = world.NeedSatisfactions[i];
                    if (r.Settlement.Value == settlement.Value && r.Class.Value == row.Class.Value && r.NeedId == need.Id) { found = i; break; }
                }
                if (found < 0) continue;
                if (need.Id == dignityId) dignitySlot = bound;
                sat[bound] = world.NeedSatisfactions[found].Value;
                weight[bound] = need.Weight;
                gate[bound] = NeedsGrievanceSystem.IsTierAGate(need.Id);
                bound++;
            }
            if (dignitySlot < 0 || !(sat[dignitySlot] < 1.0)) continue;   // no levy felt: nothing is the tax's

            double s0 = NeedsGrievanceSystem.AggregateSatisfaction(
                sat.AsSpan(0, bound), gate.AsSpan(0, bound), weight.AsSpan(0, bound), needs.Aggregation, adjusted.AsSpan(0, bound));
            if (!(s0 < 1.0)) continue;
            double felt = sat[dignitySlot];
            sat[dignitySlot] = 1.0;
            double s1 = NeedsGrievanceSystem.AggregateSatisfaction(
                sat.AsSpan(0, bound), gate.AsSpan(0, bound), weight.AsSpan(0, bound), needs.Aggregation, adjusted.AsSpan(0, bound));
            sat[dignitySlot] = felt;
            double share = Math.Clamp((s1 - s0) / (1.0 - s0), 0.0, 1.0);
            double value = double.IsNaN(row.Value) ? 0.0 : row.Value;
            weighted += value * share * members;
        }

        return people > 0 ? weighted / people : 0.0;
    }

    /// <summary>
    /// PROTEST INTENSITY in [0, 1]: 0 at or below the onset grievance, 1 at or above the uprising
    /// grievance, linear between. 0 when the config carries no unrest section.
    /// </summary>
    public static double Protest(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        return ProtestOf(TaxGrievance(world, settlement, cfg), u);
    }

    /// <summary>The protest law on a grievance value (pure; the one expression every caller uses).</summary>
    public static double ProtestOf(double grievance, UnrestTuning u)
    {
        ArgumentNullException.ThrowIfNull(u);
        if (!(grievance > u.ProtestOnsetGrievance)) return 0.0;
        return Math.Clamp((grievance - u.ProtestOnsetGrievance) / (u.UprisingGrievance - u.ProtestOnsetGrievance), 0.0, 1.0);
    }

    /// <summary>
    /// The factor protest leaves on realised production: 1 − protestOutputDragMax × p × r, with r the
    /// settlement's EFFECTIVE tax rate (<see cref="Governance.EffectiveTaxRate"/>). The drag is the
    /// withholding of the levied effort — strikes against the corvée, shirked quotas, sabotage of the
    /// exaction — so its carrier is the levy itself and it scales with it: an untaxed settlement's protest
    /// (a famine's grievance) drags nothing, which keeps hunger → protest → less food from becoming a doom
    /// loop (D-021 Part 1); a heavily taxed one in open protest loses more than the levy gains. EXACTLY 1.0
    /// with no protest or no levy, so a quiet or untaxed world produces bit-identically.
    /// </summary>
    public static double OutputFactor(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 1.0;
        double r = Governance.EffectiveTaxRate(world, settlement, cfg);
        if (r <= 0.0) return 1.0;
        double p = ProtestOf(TaxGrievance(world, settlement, cfg), u);
        if (p <= 0.0) return 1.0;
        return Math.Clamp(1.0 - u.ProtestOutputDragMax * p * r, 0.0, 1.0);
    }

    /// <summary>
    /// D-021 valve 1 — the extra grievance decay rate (per year) protest's expression contributes:
    /// protestDischargePerYear × p. 0 with no protest (the decay is then exactly the pre-R2b rate).
    /// </summary>
    public static double DischargePerYear(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return 0.0;
        double p = ProtestOf(TaxGrievance(world, settlement, cfg), u);
        return p > 0.0 ? u.ProtestDischargePerYear * p : 0.0;
    }

    /// <summary>
    /// The UPRISING condition: protest at full intensity (grievance at or above
    /// <see cref="UnrestTuning.UprisingGrievance"/>). RevoltSystem executes it — this reader never writes.
    /// </summary>
    public static bool IsUprising(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Needs?.Unrest is not { } u) return false;
        return TaxGrievance(world, settlement, cfg) >= u.UprisingGrievance;
    }
}
