using Proto.M7.Tactical;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;

namespace Proto.M7.Support;

/// <summary>
/// [D] The support equations (pure integer functions; docs/m7-plan/support-capabilities-proposal.md §4). Every effect is a
/// coefficient INSIDE an existing equation (Law 2: mechanisms, not free-floating modifiers):
///   coverage          c = floor(medical staff × 1000 / men)                          (staff per 1000 men)
///   saturation        u(c) = c / (c + c0)                                            (diminishing returns; u &lt; 1)
///   battle save       saved = floor(dead × SaveMax × u(c))                           (lane W's Dead → Wounded, exact)
///   care rates        recover = R0 + R1 × u(c);  die = D0 × (1 − K × u(c))          (‰ per sim-year)
///   care flows        lane E's <see cref="HeadcountLoss"/>: competing flows on one survival product, compounded per day
///   morale restore    restore = floor(MoraleLoss(dead entities) × saved / dead persons × restore‰)   (the TacticalResolver's formula)
///   morale after      before − floor(max(0, start − end − restore) × 1,000,000 / MoraleMax), floored at 0  (never a rise; round 2)
///   limits            staff ≤ floor(men × MedicsPerThousandMax / 1000); wagons ≤ floor(men × WagonsPerThousandMax / 1000)
/// </summary>
public static class SupportMath
{
    public const long Ppm = 1_000_000;

    /// <summary>Medical staff per 1000 men (floor). 0 for an army with no men.</summary>
    public static long CoveragePerThousand(long staff, long men)
    {
        if (staff < 0 || men < 0) throw new ArgumentOutOfRangeException(nameof(staff));
        return men == 0 ? 0 : (long)((Int128)staff * 1000 / men);
    }

    /// <summary>The army's limit for one kind at <paramref name="men"/> men (floor).</summary>
    public static long Limit(SupportKind kind, long men, SupportRates r) => kind switch
    {
        SupportKind.FieldMedical => (long)((Int128)men * r.MedicsPerThousandMax / 1000),
        SupportKind.SupplyTrain => (long)((Int128)men * r.WagonsPerThousandMax / 1000),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>floor(x × u(c)) with u(c) = c / (c + c0): the saturating share of x at coverage c.</summary>
    public static long Saturated(long x, long coverage, SupportRates r)
    {
        if (x < 0 || coverage < 0) throw new ArgumentOutOfRangeException(nameof(x));
        if (coverage == 0) return 0;
        return (long)((Int128)x * coverage / ((Int128)coverage + r.SaturationCoverage));
    }

    /// <summary>The out-of-action men saved as wounded at a battle: floor(dead × SaveMax × u(c)). 0 without staff.</summary>
    public static long SavedFromDead(long dead, long coverage, SupportRates r)
    {
        if (dead < 0 || coverage < 0) throw new ArgumentOutOfRangeException(nameof(dead));
        if (coverage == 0 || dead == 0) return 0;
        return (long)((Int128)dead * r.SaveMaxPpm * coverage / ((Int128)Ppm * (coverage + r.SaturationCoverage)));
    }

    /// <summary>Lane W's split with <paramref name="saved"/> moved from Dead to Wounded (exact: the parts still sum to Persons).</summary>
    public static CollapseSplit WithSave(CollapseSplit s, long saved)
    {
        if (saved < 0 || saved > s.Dead) throw new ArgumentOutOfRangeException(nameof(saved));
        return s with { Dead = s.Dead - saved, Wounded = s.Wounded + saved };
    }

    /// <summary>Return-to-duty and death-from-wounds rates (‰ per sim-year) at coverage c.</summary>
    public static (long Recover, long Die) WoundRates(long coverage, SupportRates r)
    {
        long recover = checked(r.RecoveryBasePermilleYear + Saturated(r.RecoveryCarePermilleYear, coverage, r));
        long cut = Saturated(checked(r.DiedOfWoundsBasePermilleYear * r.DiedOfWoundsCareCutPermille), coverage, r) / 1000;
        return (recover, r.DiedOfWoundsBasePermilleYear - cut);
    }

    /// <summary>The care of <paramref name="wounded"/> over <paramref name="dtMs"/> at coverage c, in thousandths of a head
    /// (recovered, died), with lane E's daily compounding of per-sim-year rates (<see cref="HeadcountLoss"/>; Law 3).</summary>
    public static (long RecoveredMilli, long DiedMilli) CareMilli(long wounded, long coverage, long dtMs, SupportRates r)
    {
        var (rec, die) = WoundRates(coverage, r);
        long total = checked(rec + die);
        if (wounded == 0 || total == 0 || dtMs == 0) return (0, 0);
        Int128 survival = HeadcountLoss.Survival(total, dtMs);
        return (HeadcountLoss.LossMilli(wounded, rec, total, survival), HeadcountLoss.LossMilli(wounded, die, total, survival));
    }

    /// <summary>
    /// The morale (tactical scale) a battle's saved men give back: the TacticalResolver's casualty-driven loss of the battle's
    /// dead entities (<see cref="ConditionMath.MoraleLoss"/>), times the share of the dead persons that were saved, times the
    /// restore share. 0 when nobody was saved.
    /// </summary>
    public static long MoraleRestore(CollapseInput c, long deadPersons, long saved, long fullLoss, long leadership, SupportRates r)
    {
        if (saved <= 0 || deadPersons <= 0) return 0;
        long deadEntities = c.EntityCounts[(int)CollapseCategory.Dead];
        long entities = c.Entities;
        if (deadEntities == 0 || entities == 0) return 0;
        long loss = Math.Min(c.MoraleMax, ConditionMath.MoraleLoss(deadEntities, fullLoss, entities, leadership));
        return (long)((Int128)loss * saved * r.MoraleRestorePermille / ((Int128)deadPersons * 1000));
    }

    /// <summary>
    /// [D, verification round 2, D-R2-01] The army's campaign morale (micro units) after a battle: the battle's NET morale loss on the
    /// TacticalResolver's scale (start − end), less the medical restore, never below zero, mapped to micro units (× 1,000,000 ÷
    /// MoraleMax, floor) and subtracted from the campaign morale before the battle (floored at 0). A battle never raises campaign
    /// morale (DR29: "Morale decreases when troops participate in combat"): the TacticalResolver's in-battle recovery (Hold, Rally:
    /// per casualty-free pulse, capped at MoraleMax, not at the start) can end a formation above its start, and that gain is not
    /// carried. Before round 2 the campaign morale was SET to (end + restore) × 1,000,000 ÷ MoraleMax, which raised it through combat.
    /// </summary>
    public static long MoraleAfterBattle(long beforeMicro, long start, long end, long restore, long moraleMax)
    {
        if (moraleMax <= 0 || beforeMicro < 0 || start < 0 || start > moraleMax || end < 0 || end > moraleMax || restore < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeMicro));
        long net = Math.Max(0, start - end - restore);
        long lossMicro = (long)((Int128)net * 1_000_000 / moraleMax);
        return Math.Max(0, beforeMicro - lossMicro);
    }
}
