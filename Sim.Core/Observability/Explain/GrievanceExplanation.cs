using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Core.Observability.Explain;

/// <summary>One need's component of a class's grievance. Unbound needs are
/// listed with <see cref="Bound"/> false and NaN values, never omitted, so the
/// player can see the registry is bigger than the model.</summary>
public readonly record struct NeedComponent(
    int NeedId, string Name, bool Bound, bool IsTierAGate, double Weight,
    double Satisfaction, double WeightedShortfall, double MarginalLift, string Note);

/// <summary>
/// T4.19 — WHY THIS CLASS IS AGGRIEVED (docs/observability-architecture.md §5).
///
/// WHAT IT REPRODUCES, AND HOW. NeedsGrievanceSystem.Step (NeedsGrievanceSystem.cs:152-302)
/// is read in full and its reads are repeated here in the same order from the
/// same tables: settlement population from Prev buckets, the generational
/// turnover from the PREV SettlementVitals row, the class population from Prev
/// buckets, the per-need satisfactions the system itself just PUBLISHED on Next,
/// the registry weights, the Tier-A gate ids from the system's own public array,
/// the aggregation tuning, and the previous grievance from Prev. It then calls
/// the same two PUBLIC functions the system calls —
/// <see cref="NeedsAggregation.ApplyTierAGate"/> and <see cref="NeedsAggregation.Aggregate"/>
/// — and integrates with the dt the executor used (READ from Next's clock).
/// <see cref="Recomputed"/> therefore equals <see cref="Total"/> to the bit on
/// every stepped world, and a test asserts exactly that; the day the system's
/// arithmetic changes and this does not, that test is what goes red.
///
/// THE ONE CONSTANT IT STATES RATHER THAN READS: the expectation baseline is a
/// private const on the system, fixed at 1.0 (NeedsGrievanceSystem.cs:86), with
/// habituation deferred (D-018 §4). It is repeated here as a literal and named
/// in docs/explain-queries.md as such; the exact-reproduction test is the drift
/// detector for it.
///
/// THE DECOMPOSITION IS OBSERVER-DEFINED, AND SAYS SO. W·(1−S) is a CES
/// aggregate and is NOT additive over needs, so no per-need "share of the
/// accrual" is a simulation quantity (§8 item 10). What the record carries per
/// bound need is (a) the READ satisfaction, (b) WeightedShortfall = w·(1−s), the
/// plain reading of "how unmet, how much it matters", and (c) MarginalLift =
/// S(s with s_n := 1) − S: how much the aggregate would rise if this need alone
/// were fully met, RECOMPUTED through the same gate + aggregate. MarginalLift is
/// what the primary is ranked by, because it respects the gate — a starving
/// class's comfort shortfall is correctly near-worthless. Neither number is
/// labelled as the need's contribution to G, because there is no such number.
///
/// THE PRIMARY is argmax MarginalLift with ties broken by LOWEST need id: a
/// composite key (lift DESC, needId ASC), compared as two explicit halves
/// exactly as AppropriationSystem.RichestOtherGrainRow compares (stock DESC,
/// id ASC). Never table order, never a double-only scan — a strictly-greater
/// scan happens to give the same answer only while ids are ascending in the
/// table, which is a property of the loader, not of the argmax.
///
/// PURE. Reads two read-only worlds and a config, allocates its own arrays,
/// returns a record. Nothing here is serialized, and no system reads it.
/// </summary>
public sealed class GrievanceExplanation
{
    /// <summary>NeedsGrievanceSystem's private expectation baseline, restated
    /// (see the type header). 1.0 = "everything fully met is the expectation".</summary>
    private const double Expectation = 1.0;

    public const string AttributionNote =
        "Observer-defined: W·(1−S) is a CES aggregate (σ<1) and is not additive over needs, so no per-need "
        + "contribution to grievance exists in the simulation. WeightedShortfall = w·(1−s) is the plain reading; "
        + "MarginalLift = S(s_n:=1) − S is recomputed through NeedsAggregation.ApplyTierAGate + Aggregate and is "
        + "what the primary is ranked by (lift DESC, need id ASC).";

    public SettlementId Settlement { get; }
    public ClassId Class { get; }
    public string ClassName { get; }

    /// <summary>READ next.Grievances (0 when the row is absent).</summary>
    public double Total { get; }
    /// <summary>READ prev.Grievances (0 when absent — the system's own default).</summary>
    public double Previous { get; }
    /// <summary>DIFFERENCED: Total − Previous.</summary>
    public double Delta { get; }
    /// <summary>READ next.Clock.DtYears — the dt the step that produced Next integrated with.</summary>
    public double DtYears { get; }

    /// <summary>SUMMED prev buckets, all classes — the turnover denominator.</summary>
    public long SettlementPopulation { get; }
    /// <summary>SUMMED prev buckets, this class — zero means the system zeroed G and published no rows.</summary>
    public long ClassPopulation { get; }
    /// <summary>False when the settlement was not in prev.Settlements (founded this step): the system did not step it.</summary>
    public bool SteppedBySystem { get; }

    /// <summary>RECOMPUTED S: the CES aggregate over the bound needs after the Tier-A gate.</summary>
    public double Aggregate { get; }
    /// <summary>W: Σ raw registry weights of the bound needs that published a row.</summary>
    public double WeightSum { get; }
    /// <summary>W × max(0, 1 − S), per year.</summary>
    public double AccrualPerYear { get; }
    /// <summary>READ prev vitals: (Births + Deaths) / population / row.DtYears; 0 without a row.</summary>
    public double TurnoverPerYear { get; }
    /// <summary>BaseDecayPerYear + (1 − InheritFraction) × TurnoverPerYear.</summary>
    public double DecayRatePerYear { get; }
    /// <summary>AccrualPerYear × dt — the turn's accrual.</summary>
    public double Accrual { get; }
    /// <summary>DecayRatePerYear × Previous × dt — the turn's decay.</summary>
    public double Decay { get; }
    /// <summary>max(0, Previous + Accrual − Decay), or 0 when the class/settlement had nobody —
    /// the same expression, same association, as NeedsGrievanceSystem.cs:298-299. Equals Total.</summary>
    public double Recomputed { get; }

    /// <summary>Every registry need, registry order.</summary>
    public NeedComponent[] Needs { get; }
    /// <summary>The argmax need id, or −1 when no bound need published a row.</summary>
    public int PrimaryNeedId { get; }
    /// <summary>The §5 chain under the primary need (null when there is none).</summary>
    public CausalChain? PrimaryChain { get; }

    private GrievanceExplanation(
        SettlementId settlement, ClassId cls, string className, double total, double previous, double dt,
        long settlementPop, long classPop, bool stepped, double aggregate, double weightSum,
        double accrualPerYear, double turnover, double decayRate, double accrual, double decay, double recomputed,
        NeedComponent[] needs, int primary, CausalChain? chain)
    {
        Settlement = settlement; Class = cls; ClassName = className;
        Total = total; Previous = previous; Delta = total - previous; DtYears = dt;
        SettlementPopulation = settlementPop; ClassPopulation = classPop; SteppedBySystem = stepped;
        Aggregate = aggregate; WeightSum = weightSum; AccrualPerYear = accrualPerYear;
        TurnoverPerYear = turnover; DecayRatePerYear = decayRate; Accrual = accrual; Decay = decay;
        Recomputed = recomputed; Needs = needs; PrimaryNeedId = primary; PrimaryChain = chain;
    }

    public static GrievanceExplanation For(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId settlement, ClassId cls)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        NeedsConfig needsCfg = cfg.Needs
            ?? throw new ArgumentException("SimConfig.Needs is not loaded — the explanation reads the registry.", nameof(cfg));
        GrievanceTuning tuning = needsCfg.Grievance;
        AggregationTuning agg = needsCfg.Aggregation;
        NeedEntry[] registry = needsCfg.Needs;

        // --- READ: the stock before and after, and the dt the step used ------
        int gPrevRow = ExplainRows.Grievance(prev, settlement, cls);
        int gNextRow = ExplainRows.Grievance(next, settlement, cls);
        double gPrev = gPrevRow >= 0 ? prev.Grievances[gPrevRow].Value : 0.0;   // NeedsGrievanceSystem.cs:287-293
        double gNext = gNextRow >= 0 ? next.Grievances[gNextRow].Value : 0.0;
        double dt = next.Clock.DtYears;

        // --- the system's per-settlement reads, in its order (NeedsGrievanceSystem.cs:176-211)
        bool stepped = ExplainRows.SettlementPresent(prev, settlement);
        long settlementPop = ExplainRows.Population(prev, settlement);
        double turnover = 0.0;
        if (settlementPop > 0)
        {
            int v = ExplainRows.Vitals(prev, settlement);
            if (v >= 0 && prev.SettlementVitals[v].DtYears > 0.0)
            {
                SettlementVitalsRow row = prev.SettlementVitals[v];
                turnover = (row.Births + row.Deaths) / (double)settlementPop / row.DtYears;
            }
        }
        double decayRate = tuning.BaseDecayPerYear + (1.0 - tuning.InheritFraction) * turnover;
        long classPop = ExplainRows.ClassPopulation(prev, settlement, cls);

        // --- the bound needs that PUBLISHED a row, registry order (= the system's
        // sat[] order: it walks the registry and appends, NeedsGrievanceSystem.cs:244-274)
        int n = registry.Length;
        var sat = new double[n];
        var weight = new double[n];
        var isGate = new bool[n];
        var boundIndex = new int[n];      // registry index of each bound slot
        int bound = 0;
        double rawWeightSum = 0.0;
        for (int r = 0; r < n; r++)
        {
            NeedEntry need = registry[r];
            if (!need.Bound) continue;
            int row = ExplainRows.Satisfaction(next, settlement, cls, need.Id);
            if (row < 0) continue;      // the system skipped it (empty basket) or published nothing (no members)
            sat[bound] = next.NeedSatisfactions[row].Value;
            weight[bound] = need.Weight;
            isGate[bound] = IsTierAGate(need.Id);
            boundIndex[bound] = r;
            rawWeightSum += need.Weight;
            bound++;
        }

        // --- RECOMPUTED through the public gate + aggregate (NeedsGrievanceSystem.cs:276-284)
        double aggregate = Expectation;
        var adjusted = new double[n];
        if (bound > 0)
        {
            NeedsAggregation.ApplyTierAGate(
                sat.AsSpan(0, bound), isGate.AsSpan(0, bound), weight.AsSpan(0, bound),
                agg.TierAFloor, agg.TierAGain, agg.TierACollapse, adjusted.AsSpan(0, bound));
            aggregate = NeedsAggregation.Aggregate(
                sat.AsSpan(0, bound), adjusted.AsSpan(0, bound), agg.Sigma, agg.SatisfactionFloor);
        }
        double accrualPerYear = rawWeightSum * Math.Max(0.0, Expectation - aggregate);

        // Same operands, same association as `gPrev + accrualPerYear * dt - decayRate * gPrev * dt`
        // (NeedsGrievanceSystem.cs:298): (gPrev + (accrualPerYear·dt)) − ((decayRate·gPrev)·dt).
        double accrual = accrualPerYear * dt;
        double decay = decayRate * gPrev * dt;
        double recomputed;
        if (!stepped) recomputed = gPrev;                       // not iterated: the row is whatever founding left
        else if (settlementPop == 0 || classPop == 0) recomputed = 0.0;   // NeedsGrievanceSystem.cs:191-197, 233-241
        else recomputed = Math.Max(0.0, gPrev + accrual - decay);

        // --- per-need components + the primary -------------------------------
        var components = new NeedComponent[n];
        var lifted = new double[n];
        var liftedAdjusted = new double[n];
        int best = -1;
        double bestLift = 0.0;
        int bestId = 0;
        int slot = 0;
        for (int r = 0; r < n; r++)
        {
            NeedEntry need = registry[r];
            bool published = slot < bound && boundIndex[slot] == r;
            if (!need.Bound)
            {
                components[r] = new NeedComponent(need.Id, need.Name, false, IsTierAGate(need.Id), need.Weight,
                    double.NaN, double.NaN, double.NaN, "not yet simulated");
                continue;
            }
            if (!published)
            {
                components[r] = new NeedComponent(need.Id, need.Name, false, IsTierAGate(need.Id), need.Weight,
                    double.NaN, double.NaN, double.NaN,
                    classPop == 0 || settlementPop == 0
                        ? "no members — no satisfaction row published"
                        : "bound in the registry but this class declares no basket for it — skipped by the system");
                continue;
            }

            double s = sat[slot];
            // S with THIS need alone fully met, through the same gate + aggregate.
            sat.AsSpan(0, bound).CopyTo(lifted);
            lifted[slot] = 1.0;
            NeedsAggregation.ApplyTierAGate(
                lifted.AsSpan(0, bound), isGate.AsSpan(0, bound), weight.AsSpan(0, bound),
                agg.TierAFloor, agg.TierAGain, agg.TierACollapse, liftedAdjusted.AsSpan(0, bound));
            double liftedAggregate = NeedsAggregation.Aggregate(
                lifted.AsSpan(0, bound), liftedAdjusted.AsSpan(0, bound), agg.Sigma, agg.SatisfactionFloor);
            double lift = liftedAggregate - aggregate;

            components[r] = new NeedComponent(need.Id, need.Name, true, isGate[slot], need.Weight,
                s, need.Weight * (1.0 - s), lift, "");

            // (lift DESC, needId ASC), both halves compared explicitly — the
            // RichestOtherGrainRow shape (AppropriationSystem.cs:260-262).
            if (!double.IsNaN(lift)
                && !(best >= 0 && (lift < bestLift || (lift == bestLift && need.Id > bestId))))
            {
                best = r; bestLift = lift; bestId = need.Id;
            }
            slot++;
        }

        CausalChain? chain = best >= 0
            ? CausalChain.ForNeed(prev, next, cfg, settlement, cls, registry[best].Id) : null;

        return new GrievanceExplanation(
            settlement, cls, ExplainRows.ClassName(cfg, cls), gNext, gPrev, dt,
            settlementPop, classPop, stepped, aggregate, rawWeightSum, accrualPerYear, turnover, decayRate,
            accrual, decay, recomputed, components, best >= 0 ? registry[best].Id : -1, chain);
    }

    /// <summary>The system's own gate list, read from its public array —
    /// never a copy (NeedsGrievanceSystem.cs:99).</summary>
    private static bool IsTierAGate(int needId)
    {
        int[] gates = NeedsGrievanceSystem.TierAGateNeedIds;
        for (int i = 0; i < gates.Length; i++) if (gates[i] == needId) return true;
        return false;
    }
}
