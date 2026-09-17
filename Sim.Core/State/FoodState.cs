using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>The four-state food classification (T4.21 §3.1). Ordered by severity.</summary>
public enum FoodStateKind
{
    Normal = 0,
    Stress = 1,
    Severe = 2,
    Famine = 3,
}

/// <summary>Why a settlement classifies as FAMINE (T4.21 §3.1). None outside Famine.</summary>
public enum FamineReason
{
    None = 0,
    Disaster = 1,
    Abandonment = 2,
    Both = 3,
}

/// <summary>
/// T4.21-1 (CR-015) — FAMINE IS EXCEPTIONAL. The derived four-state food
/// classification: a pure reading of the world, never a stock, never serialized
/// (the <see cref="SettlementHappiness"/> contract — static, recomputed on
/// demand, crossing no isolation boundary).
///
/// THE RULE.  With d the nominal deficit (ConsumptionDeficitRow.DeficitRatio):
///   FAMINE  iff d &gt; 0 AND (a famine-class disaster was APPLIED to the harvest
///           that produced d, OR food labour is deliberately abandoned);
///   SEVERE  iff d &gt; a           — adaptation is exhausted, starvation begins;
///   STRESS  iff 0 &lt; d ≤ a       — the cut is absorbed (belts tighten, nobody dies);
///   NORMAL  iff d = 0.
/// FAMINE never fires without a shortfall: a disaster a rich granary absorbs is
/// not a famine, and an abandonment the store still covers is not a famine. The
/// cause qualifier changes the RESPONSE FORM (no adaptation), never the magnitude,
/// which stays the food balance's own d (CR-003 §3 "decided by the food balance",
/// with the cause qualifier CR-015 adds).
///
/// ONE BOUNDARY CARRIES THE KERNEL SEMANTICS: SEVERE begins exactly where the
/// dead-zone of <see cref="EffectiveDeficit"/> ends (θ_sev := a), so the label
/// predicts the kernel's response — STRESS ⇒ starvation hazard identically 0,
/// SEVERE ⇒ starvation on the unabsorbed remainder, FAMINE ⇒ starvation on the
/// whole deficit. The demographic kernel's 1/3 birth full-stop
/// (1 − FamineFertilitySuppressionSlope × d ≤ 0) is REAL and is a boundary INSIDE
/// SEVERE — a kernel fact, not a state.
///
/// ABANDONMENT is read on the RAW sector row IN FORCE for the step — the same row
/// and the same Sectors.Default fallback ProductionSystem uses to decide that farm
/// labour and the herding pool are zero, so "deliberate abandonment" and "food
/// labour is zero" are one fact. Raw weights because that is the field
/// ProductionSystem reads (one fact, one field) — not because Share is undefined:
/// Sectors.Share guards the zero row sum and returns 0.0 for the all-zero row, so a
/// predicate on Share is behaviourally identical to this one for every reachable
/// row (the Share variant of M-FS-ABANDON is an equivalent mutant, recorded in
/// docs/t4.21-1-mutants.md). The all-zero row (reachable only from a hand-written
/// log) classifies as abandoned — nothing is farmed. The legacy LaborAllocation pct = 0 order writes
/// Farming 0 / Herding 0 / Construction 1.0 and IS abandonment. Single-sector
/// rows (Farming = 0 ∧ Herding &gt; 0, or Farming &gt; 0 ∧ Herding = 0) are NOT
/// abandonment (director's rule; the pure pastoralist's food-model outcome is
/// CR-015 G6). A world with no row reads Sectors.Default and is never abandoned.
///
/// TIMING (one-turn lag, no state). Both causes keep d &gt; 0 necessary, and they
/// carry different lag semantics ON PURPOSE: a policy is read as IN FORCE
/// (forward-looking — if the row stands, the next harvest is zero too); an event
/// is read as APPLIED (backward-looking — DisasterRow.AppliedMultiplier is the
/// factor ProductionSystem multiplied by in the step that produced d).
///
/// Every read is a table-order scan over the world passed in, which callers pass
/// as PREV (law 5: no dictionaries, no LINQ; law 6: tables, never systems).
/// </summary>
public static class FoodState
{
    /// <summary>Classify one settlement from the world passed in (PREV at every
    /// caller). <paramref name="reason"/> is None unless the result is Famine.</summary>
    public static FoodStateKind Of(
        IReadOnlyWorldState w, SettlementId s, SimConfig cfg, out FamineReason reason)
    {
        double d = DeficitRatio(w, s);
        bool struck = IsStruck(w, s);
        bool abandoned = IsAbandoned(w, s);

        if (d > 0.0 && (struck || abandoned))
        {
            reason = struck && abandoned ? FamineReason.Both
                : struck ? FamineReason.Disaster
                : FamineReason.Abandonment;
            return FoodStateKind.Famine;
        }
        reason = FamineReason.None;
        if (d > SevereThreshold(cfg)) return FoodStateKind.Severe;
        if (d > 0.0) return FoodStateKind.Stress;
        return FoodStateKind.Normal;
    }

    /// <summary>The nominal deficit above which adaptation is exhausted and
    /// starvation begins: exactly the absorbable shortfall <c>a</c> (θ_sev := a).</summary>
    public static double SevereThreshold(SimConfig cfg) =>
        cfg.FoodState.AdaptationAbsorbableShortfall;

    /// <summary>
    /// The EFFECTIVE deficit (T4.21 §3.2) — the dead-zone adaptation form,
    /// starvation mortality's ONE input outside FAMINE:
    ///   Famine → d (no adaptation possible);
    ///   else   → d ≤ a ? 0 : (d − a) / (1 − a)   (exactly 1 at d = 1).
    /// The null arm a = 0 is EXACT: (d − 0)/1 = d bit for bit, so today's linear
    /// response is one config value away. Every other reader of the deficit —
    /// fertility suppression's gate, the rebound release gate, destination
    /// viability, happiness, appropriation, demote-first — keeps the NOMINAL d.
    /// </summary>
    public static double EffectiveDeficit(double d, FoodStateKind state, SimConfig cfg)
    {
        if (state == FoodStateKind.Famine) return d;
        double a = cfg.FoodState.AdaptationAbsorbableShortfall;
        return d <= a ? 0.0 : (d - a) / (1.0 - a);
    }

    /// <summary>Farming == 0 ∧ Herding == 0 on the RAW row in force (absent row →
    /// Sectors.Default → never abandoned) — exactly ProductionSystem's row read.</summary>
    public static bool IsAbandoned(IReadOnlyWorldState w, SettlementId s)
    {
        SectorAllocationRow row = Sectors.Default(s);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
        {
            if (w.SectorAllocations[i].Settlement == s) { row = w.SectorAllocations[i]; break; }
        }
        return Sectors.Raw(row, Sectors.Farming) == 0.0 && Sectors.Raw(row, Sectors.Herding) == 0.0;
    }

    /// <summary>A famine-class disaster multiplier was APPLIED to the harvest that
    /// produced this world's deficit: DisasterRow.AppliedMultiplier &lt; 1 (absent
    /// row → false). Every row of the single shipped kind is famine-class by the
    /// §3.3 derivation, so presence is the predicate.</summary>
    public static bool IsStruck(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
        {
            if (w.Disasters[i].Settlement != s) continue;
            return w.Disasters[i].AppliedMultiplier < 1.0;
        }
        return false;
    }

    /// <summary>The nominal deficit ratio (absent row → 0, as every system reads it).</summary>
    public static double DeficitRatio(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
        {
            if (w.ConsumptionDeficits[i].Settlement == s)
                return w.ConsumptionDeficits[i].DeficitRatio;
        }
        return 0.0;
    }
}
