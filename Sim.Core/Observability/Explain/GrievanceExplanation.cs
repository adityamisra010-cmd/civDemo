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
/// WHAT IT REPRODUCES, AND HOW. NeedsGrievanceSystem.Step (NeedsGrievanceSystem.cs:148-285)
/// is read in full and its READS are repeated here in the same order from the
/// same tables: settlement population from Prev buckets, the PREV
/// SettlementVitals row, the class population from Prev buckets, the per-need
/// satisfactions the system itself just PUBLISHED on Next, the registry
/// weights, the aggregation tuning, and the previous grievance from Prev. Its
/// ARITHMETIC is not repeated: every number derived from those reads comes
/// from a CALL to the public static pure function the system itself calls on
/// the same operands — <see cref="NeedsGrievanceSystem.IsTierAGate"/>,
/// <see cref="NeedsGrievanceSystem.AggregateSatisfaction"/> (the gate + CES),
/// <see cref="NeedsGrievanceSystem.TurnoverPerYear"/>,
/// <see cref="NeedsGrievanceSystem.DecayRatePerYear"/>,
/// <see cref="NeedsGrievanceSystem.AccrualPerYear"/>,
/// <see cref="NeedsGrievanceSystem.TurnAccrual"/>, <see cref="NeedsGrievanceSystem.TurnDecay"/>
/// and <see cref="NeedsGrievanceSystem.StepGrievance"/> — integrated with the dt
/// the executor used (READ from Next's clock). That is what §0 means by
/// RECOMPUTED, and it is why this file holds no formula of its own: the T4.19
/// A2 verifier found the first cut re-implementing the system's private
/// turnover, decay, accrual and Euler step under the RECOMPUTED label (finding
/// D1), and a copied formula is exactly the second simulation §0 forbids — it
/// drifts silently the day the system changes. <see cref="Recomputed"/> equals
/// <see cref="Total"/> to the bit on every stepped world, and a test asserts
/// exactly that; the day the system stops calling these functions, or calls
/// them on other operands, that test is what goes red.
///
/// THE DECOMPOSITION IS OBSERVER-DEFINED, AND SAYS SO. W·(1−S) is a CES
/// aggregate and is NOT additive over needs, so no per-need "share of the
/// accrual" is a simulation quantity (§8 item 10). What the record carries per
/// bound need is (a) the READ satisfaction, (b) WeightedShortfall = w·(1−s), the
/// plain reading of "how unmet, how much it matters", and (c) MarginalLift =
/// S(s with s_n := 1) − S: how much the aggregate would rise if this need alone
/// were fully met, RECOMPUTED through the same <see cref="NeedsGrievanceSystem.AggregateSatisfaction"/>.
/// MarginalLift is what the primary is ranked by, because it respects the gate
/// — a starving class's comfort shortfall is correctly near-worthless. Neither
/// number is labelled as the need's contribution to G, because there is no
/// such number.
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
    public const string AttributionNote =
        "Observer-defined: W·(1−S) is a CES aggregate (σ<1) and is not additive over needs, so no per-need "
        + "contribution to grievance exists in the simulation. WeightedShortfall = w·(1−s) is the plain reading; "
        + "MarginalLift = S(s_n:=1) − S is recomputed through NeedsGrievanceSystem.AggregateSatisfaction and is "
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

    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.AggregateSatisfaction"/>: S, the CES
    /// aggregate over the bound needs that published a row, after the Tier-A gate.</summary>
    public double Aggregate { get; }
    /// <summary>W: Σ raw registry weights (READ config) of the bound needs that published a row.</summary>
    public double WeightSum { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.AccrualPerYear"/>(W, S).</summary>
    public double AccrualPerYear { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.TurnoverPerYear"/> on the READ prev
    /// vitals row (Births, Deaths, its DtYears) and the SUMMED population; 0 without a row.</summary>
    public double TurnoverPerYear { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.DecayRatePerYear"/>(tuning, turnover).</summary>
    public double DecayRatePerYear { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.TurnAccrual"/>(AccrualPerYear, dt).</summary>
    public double Accrual { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.TurnDecay"/>(DecayRatePerYear, Previous, dt).</summary>
    public double Decay { get; }
    /// <summary>RECOMPUTED <see cref="NeedsGrievanceSystem.StepGrievance"/>(Previous, AccrualPerYear,
    /// DecayRatePerYear, dt) — or 0 when the class or settlement had nobody (the system's two
    /// zeroing branches), or Previous when the settlement was not stepped. Equals Total.</summary>
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
        double gPrev = gPrevRow >= 0 ? prev.Grievances[gPrevRow].Value : 0.0;   // NeedsGrievanceSystem.cs:274-280
        double gNext = gNextRow >= 0 ? next.Grievances[gNextRow].Value : 0.0;
        double dt = next.Clock.DtYears;

        // --- the system's per-settlement reads, in its order (NeedsGrievanceSystem.cs:168-205)
        bool stepped = ExplainRows.SettlementPresent(prev, settlement);
        long settlementPop = ExplainRows.Population(prev, settlement);
        double turnover = 0.0;
        if (settlementPop > 0)
        {
            int v = ExplainRows.Vitals(prev, settlement);
            if (v >= 0)
            {
                SettlementVitalsRow row = prev.SettlementVitals[v];
                turnover = NeedsGrievanceSystem.TurnoverPerYear(row.Births, row.Deaths, settlementPop, row.DtYears);
            }
        }
        double decayRate = NeedsGrievanceSystem.DecayRatePerYear(tuning, turnover);
        long classPop = ExplainRows.ClassPopulation(prev, settlement, cls);

        // --- the bound needs that PUBLISHED a row, registry order (= the system's
        // sat[] order: it walks the registry and appends, NeedsGrievanceSystem.cs:240-268)
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
            isGate[bound] = NeedsGrievanceSystem.IsTierAGate(need.Id);
            boundIndex[bound] = r;
            rawWeightSum += need.Weight;
            bound++;
        }

        // --- RECOMPUTED: the system's own functions on the values read above
        // (NeedsGrievanceSystem.cs:270-272, 281-282; the functions themselves at 428-483).
        var adjusted = new double[n];
        double aggregate = NeedsGrievanceSystem.AggregateSatisfaction(
            sat.AsSpan(0, bound), isGate.AsSpan(0, bound), weight.AsSpan(0, bound), agg, adjusted.AsSpan(0, bound));
        double accrualPerYear = NeedsGrievanceSystem.AccrualPerYear(rawWeightSum, aggregate);
        double accrual = NeedsGrievanceSystem.TurnAccrual(accrualPerYear, dt);
        double decay = NeedsGrievanceSystem.TurnDecay(decayRate, gPrev, dt);
        double recomputed;
        if (!stepped) recomputed = gPrev;                       // not iterated: the row is whatever founding left
        else if (settlementPop == 0 || classPop == 0) recomputed = 0.0;   // NeedsGrievanceSystem.cs:187-193, 231-235
        else recomputed = NeedsGrievanceSystem.StepGrievance(gPrev, accrualPerYear, decayRate, dt);

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
                components[r] = new NeedComponent(need.Id, need.Name, false, NeedsGrievanceSystem.IsTierAGate(need.Id),
                    need.Weight, double.NaN, double.NaN, double.NaN, "not yet simulated");
                continue;
            }
            if (!published)
            {
                components[r] = new NeedComponent(need.Id, need.Name, false, NeedsGrievanceSystem.IsTierAGate(need.Id),
                    need.Weight, double.NaN, double.NaN, double.NaN,
                    !stepped
                        ? "founded this step — not in Prev, so the system did not step it and published no row"
                        : classPop == 0 || settlementPop == 0
                        ? "no members — no satisfaction row published"
                        : "bound in the registry but this class declares no basket for it — skipped by the system");
                continue;
            }

            double s = sat[slot];
            // S with THIS need alone fully met, through the same gate + aggregate the system calls.
            sat.AsSpan(0, bound).CopyTo(lifted);
            lifted[slot] = 1.0;
            double liftedAggregate = NeedsGrievanceSystem.AggregateSatisfaction(
                lifted.AsSpan(0, bound), isGate.AsSpan(0, bound), weight.AsSpan(0, bound), agg,
                liftedAdjusted.AsSpan(0, bound));
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
}
