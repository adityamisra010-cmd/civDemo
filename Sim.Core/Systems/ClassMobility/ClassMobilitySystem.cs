using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.ClassMobility;

/// <summary>Writable handles to ClassMobilitySystem's tables (built by
/// SystemCatalog only). Buckets is SHARED with Demographics (see SystemCatalog:
/// both mutate people exclusively through the Ledger; mobility owns
/// MobilityRemainder, demographics owns the other four).</summary>
public readonly record struct ClassMobilityTables(
    Table<BucketRow> Buckets, Table<VariableRow> Variables, Table<ClassStateRow> ClassStates);

/// <summary>
/// ClassMobility (T2.2, D-020/D-027): the class system's engine, three jobs in
/// pinned order per settlement:
///
/// 1. PUBLISH VARIABLES (into its own Variables table, upserted per turn):
///    food_surplus_ratio = Prev LastHarvestUnits / Prev DemandUnits (0 when
///    demand is 0), artisan_share = artisan adults / total adults from Prev
///    buckets (0 when no adults). Consumers — including this system's own
///    predicate evaluation — read the PREV turn's rows (one-turn lag, §3.2).
///
/// 2. EMERGENCE LATCH: for each non-base class, evaluate its emerge/recede
///    predicates against PREV variables. Inactive + emerge true → Active = 1;
///    active + recede true → Active = 0. The latch (persistent, serialized) IS
///    the hysteresis: with emerge X / recede Y &lt; X, a signal oscillating
///    inside the (Y, X) band crosses neither threshold and produces at most
///    one transition. Recede absent = never recedes.
///
/// 3. MOBILITY FLOWS — same-cohort, ADULT-cohorts-only Ledger.Transfers
///    between the base class and the class under management (M2: Peasants ⇄
///    Artisans; conserve by construction, no source/sink footprint):
///    - target share = min(cap, max(0, slope × (surplus − 1))) — capped
///      saturating in the PREV surplus ratio; target is 0 while inactive.
///    - the share relaxes toward target: moved/yr = gap-in-people ×
///      PromoteRatePerYear (promotion, peasants → artisans) or the reverse on
///      overshoot, integrated × dtYears through per-source-row
///      MobilityRemainders, distributed over adult cohorts in ascending cohort
///      order proportionally to the source class's PREV cohort counts.
///    - FAMINE DEMOTE-FIRST: Prev deficit &gt; 0 forces demotion of
///      FamineDemoteRatePerYear × artisan adults × deficit per year (faster
///      than promotion; TUNE) regardless of predicates — artisans starve back
///      to the fields BEFORE peasant starvation peaks (Demographics' deficit
///      response is rate-limited; this drain is deliberately steeper).
///
/// Births stay group-local by T2.1 mechanics (artisan parents bear artisans);
/// children and elders NEVER move class here — only adult cohorts transfer.
/// STATELESS: config is immutable tuning; predicates parsed once in the ctor
/// (already validated at config load).
/// </summary>
public sealed class ClassMobilitySystem : ISimSystem<ClassMobilityTables>
{
    public static readonly SystemId WellKnownId = new(9);
    public const string Name = "classmobility";

    private readonly SimConfig _cfg;
    private readonly Predicate?[] _emerge;
    private readonly Predicate?[] _recede;

    public ClassMobilitySystem(SimConfig cfg)
    {
        _cfg = cfg;
        int n = cfg.Registries.Classes.Length;
        _emerge = new Predicate?[n];
        _recede = new Predicate?[n];
        for (int i = 0; i < n; i++)
        {
            ClassEntry e = cfg.Registries.Classes[i];
            _emerge[i] = e.Emerge is null ? null : Predicate.Parse(e.Emerge);
            _recede[i] = e.Recede is null ? null : Predicate.Parse(e.Recede);
        }
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ClassMobilityTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        MobilityConfig m = _cfg.Mobility;

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // --- 1. publish -------------------------------------------------
            double surplus = 0.0;
            long demand = 0, harvest = 0;
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            {
                if (prev.ConsumptionDeficits[i].Settlement == settlement)
                {
                    demand = prev.ConsumptionDeficits[i].DemandUnits;
                    break;
                }
            }
            for (int i = 0; i < prev.FoodStores.Count; i++)
            {
                if (prev.FoodStores[i].Settlement == settlement)
                {
                    harvest = prev.FoodStores[i].LastHarvestUnits;
                    break;
                }
            }
            if (demand > 0) surplus = harvest / (double)demand;

            long totalAdults = BandViews.Adults(prev.Buckets, settlement);
            ClassId artisanClass = new(_cfg.Registries.Classes.Length > 1
                ? _cfg.Registries.Classes[1].Id : _cfg.Registries.Classes[0].Id);
            long artisanAdults = AdultsOfClass(prev.Buckets, settlement, artisanClass);
            double artisanShare = totalAdults > 0 ? artisanAdults / (double)totalAdults : 0.0;

            Upsert(ctx.Owned.Variables, settlement, Variables.FoodSurplusRatio, surplus);
            Upsert(ctx.Owned.Variables, settlement, Variables.ArtisanShare, artisanShare);

            // --- 2. latch (evaluated on PREV variables — one-turn lag) ------
            for (int c = 1; c < _cfg.Registries.Classes.Length; c++)
            {
                var cls = new ClassId(_cfg.Registries.Classes[c].Id);
                int stateIdx = FindState(ctx.Owned.ClassStates, settlement, cls);
                if (stateIdx < 0) continue; // never founded (toy world)
                int active = ctx.Owned.ClassStates[stateIdx].Active;

                // WARM-UP GUARD (review finding): before this settlement has
                // ever published variables (turn 1, or a hand-built state),
                // PrevVariable would feed the predicates 0.0 — and a latched
                // class would "recede" because data is MISSING, not because
                // the surplus died. The latch holds until real values exist.
                if (HasPublished(prev, settlement))
                {
                    Predicate.VariableReader read = varId => PrevVariable(prev, settlement, varId);
                    if (active == 0 && _emerge[c] is not null && _emerge[c]!.Evaluate(read))
                        active = 1;
                    else if (active == 1 && _recede[c] is not null && _recede[c]!.Evaluate(read))
                        active = 0;
                }
                ctx.Owned.ClassStates[stateIdx] = new ClassStateRow(settlement, cls, active);

                // --- 3. mobility (M2: one managed class) --------------------
                if (c == 1)
                {
                    MoveAdults(ctx, prev, settlement, m, active,
                        baseClass: new ClassId(_cfg.Registries.Classes[0].Id),
                        managed: cls, totalAdults, artisanAdults, surplus);
                }
            }
        }
    }

    private void MoveAdults(
        SimContext<ClassMobilityTables> ctx, IReadOnlyWorldState prev, SettlementId settlement,
        MobilityConfig m, int active, ClassId baseClass, ClassId managed,
        long totalAdults, long managedAdults, double surplus)
    {
        if (totalAdults <= 0) return;

        // PREV deficit drives the famine valve (one-turn lag, §3.2).
        double deficit = 0.0;
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
        {
            if (prev.ConsumptionDeficits[i].Settlement == settlement)
            {
                deficit = prev.ConsumptionDeficits[i].DeficitRatio;
                break;
            }
        }

        double targetShare = active == 1
            ? Math.Min(m.TargetShareCap, Math.Max(0.0, m.TargetShareSlope * (surplus - 1.0)))
            : 0.0;
        double gapPeople = targetShare * totalAdults - managedAdults;

        double movePerYear;
        ClassId from, to;
        if (deficit > 0.0)
        {
            // FAMINE DEMOTE-FIRST: overrides the relaxation entirely.
            movePerYear = m.FamineDemoteRatePerYear * deficit * managedAdults;
            from = managed; to = baseClass;
        }
        else if (gapPeople > 0.0)
        {
            movePerYear = m.PromoteRatePerYear * gapPeople;
            from = baseClass; to = managed;
        }
        else
        {
            movePerYear = m.PromoteRatePerYear * -gapPeople; // overshoot relaxes back
            from = managed; to = baseClass;
        }
        if (movePerYear <= 0.0) return;

        // Distribute over ADULT cohorts, ascending (pinned), proportional to
        // the SOURCE class's PREV cohort counts; same-cohort transfers only.
        // FULL DRAIN is exact: when the requested move covers the whole source
        // class, each cohort moves its entire PREV count — no fractional-floor
        // stragglers (a proportional floor leaves ~1 person per cohort behind,
        // which broke the demote-first ordering by one turn).
        long fromAdults = AdultsOfClass(prev.Buckets, settlement, from);
        if (fromAdults <= 0) return;
        bool fullDrain = movePerYear * ctx.DtYears >= fromAdults;
        double moveTotal = Math.Min(movePerYear * ctx.DtYears, fromAdults);

        for (int cohort = Cohorts.FirstAdult; cohort < Cohorts.FirstElder; cohort++)
        {
            int srcIdx = FindBucket(ctx.Owned.Buckets, settlement, from, cohort);
            int dstIdx = FindBucket(ctx.Owned.Buckets, settlement, to, cohort);
            if (srcIdx < 0 || dstIdx < 0) continue;
            long prevSrc = prev.Buckets[srcIdx].Count.Value;
            if (prevSrc <= 0 && ctx.Owned.Buckets[srcIdx].MobilityRemainder == 0.0) continue;

            ref BucketRow src = ref ctx.Owned.Buckets.Ref(srcIdx);
            long moved;
            if (fullDrain)
            {
                moved = prevSrc;
                src.MobilityRemainder = 0.0; // nobody left to owe a fraction
            }
            else
            {
                double exact = moveTotal * (prevSrc / (double)fromAdults) + src.MobilityRemainder;
                moved = (long)Math.Floor(exact);
                src.MobilityRemainder = exact - moved; // sub-person fraction only
            }
            if (moved <= 0) continue;
            ctx.Ledger.Transfer(
                ref ctx.Owned.Buckets.Ref(srcIdx).Count, ref ctx.Owned.Buckets.Ref(dstIdx).Count,
                moved, OverdrawPolicy.ClampToAvailable);
        }
    }

    private static bool HasPublished(IReadOnlyWorldState prev, SettlementId settlement)
    {
        for (int i = 0; i < prev.Variables.Count; i++)
            if (prev.Variables[i].Settlement == settlement) return true;
        return false;
    }

    private static double PrevVariable(IReadOnlyWorldState prev, SettlementId settlement, int varId)
    {
        for (int i = 0; i < prev.Variables.Count; i++)
        {
            if (prev.Variables[i].Settlement == settlement && prev.Variables[i].VarId == varId)
                return prev.Variables[i].Value;
        }
        return 0.0; // never published yet (turn 1) → conservative zero
    }

    public static long AdultsOfClass(IReadOnlyTable<BucketRow> buckets, SettlementId settlement, ClassId cls)
    {
        long total = 0;
        checked
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                BucketRow row = buckets[i];
                if (row.Settlement == settlement && row.Class == cls && BandViews.IsAdult(row.CohortIdx))
                    total += row.Count.Value;
            }
        }
        return total;
    }

    private static int FindBucket(Table<BucketRow> buckets, SettlementId settlement, ClassId cls, int cohort)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Settlement == settlement && buckets[i].Class == cls
                && buckets[i].CohortIdx == cohort) return i;
        }
        return -1;
    }

    private static int FindState(Table<ClassStateRow> states, SettlementId settlement, ClassId cls)
    {
        for (int i = 0; i < states.Count; i++)
            if (states[i].Settlement == settlement && states[i].Class == cls) return i;
        return -1;
    }

    private static void Upsert(Table<VariableRow> table, SettlementId settlement, int varId, double value)
    {
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i].Settlement == settlement && table[i].VarId == varId)
            {
                table[i] = new VariableRow(settlement, varId, value);
                return;
            }
        }
        table.Add(new VariableRow(settlement, varId, value));
    }
}
