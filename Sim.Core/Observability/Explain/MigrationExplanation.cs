using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Migration;

namespace Sim.Core.Observability.Explain;

/// <summary>
/// One settlement as a migration DESTINATION, as the mechanism sees it: its
/// smoothed attractiveness (the pull signal) and the three viability inputs.
/// Sorted (SmoothedAttractiveness DESC, Id ASC) in <see cref="MigrationExplanation.Others"/>.
/// </summary>
public readonly record struct Destination(
    SettlementId Id, double SmoothedAttractiveness, double DestinationDeficit,
    long GrainStock, long LastHarvest, bool GrainPresent, double Happiness,
    // T4.21-5 (spec §3.11): the DESTINATION-SIDE bounds, INDEXED out of the
    // MigrationPlan that MigrationSystem.Plan builds — the same planner Step
    // consumes. PlanRecorded is false for a settlement absent from Prev (it was
    // never planned), and every reading below is then NaN, because "not planned"
    // is not "planned to zero".
    bool PlanRecorded,
    double Vacancy,           // V_j in adult-equivalents; +inf when j carries no demand row
    double VacancyCap,        // cap_j = (1 - e^{-k dt}) x V_j
    double VacancyScale,      // < 1 IS "this destination refused refugees"
    double DesiredInflowAe,   // both channels together, adult-equivalents
    double GapInflowCap,      // f x M*_j^in; +inf when the basin is < 2
    double DestScale,
    double GapIn,             // heads, after the overdraw scale
    double FlightIn);         // heads, after the overdraw scale

/// <summary>
/// T4.19 — WHY PEOPLE LEFT, WHY THEY CAME (docs/observability-architecture.md §6).
///
/// WHAT IS STORED, read as stored. Per settlement per turn the migration
/// system records only Inflow and Outflow (MigrationFlowRow, MigrationSystem.cs
/// Step: the chronicle rows and the transfer loop's flows[...] updates). The
/// DRIVERS it read are stored too, on other systems' rows, and this record
/// shows every one of them:
///   push — the source's PREV DeficitRatio (the NOMINAL d that sets the flight
///          hazard φ = 1 − exp(−profile·K·ω·d·dt), gap-independent;
///          MigrationSystem.cs header "FLIGHT", Plan: "§3.4: φ per bucket");
///   pull — the SMOOTHED attractiveness S (MigrationSystem.Plan, "EMA filter
///          update"), for this settlement and EVERY other, because the
///          mechanism responds to the GAP max(0, S_dst − S_src) and a gap needs
///          both ends visible;
///   viability — per destination: PREV deficit (repulsion), grain presence
///          (store > 0 or last harvest > 0, the absolute food gate) and happiness
///          (SettlementHappiness.Of on PREV), MigrationSystem.Plan "T2.13:
///          destination viability";
///   unplaced — Σ BucketRow.UnplacedDeparture written when NO reachable
///          destination is viable (MigrationSystem.Plan "T4.4 (D-037 B1)"; the
///          value is the destination-free hazard, ADR-025 §2.5).
///
/// THE GAPS ARE FIELDS, NOT RECOMPUTATIONS. The pairwise From→To flows, the
/// damping matrix exp(−cost/D), the per-destination viability PRODUCTS and the
/// gap-closing scale are all computed transiently and discarded (they live in
/// the MigrationPlan that MigrationSystem.Plan builds and Step consumes). They
/// COULD be recomputed here from Prev — since T4.21-2 through the SAME public
/// static the system runs, which is the sanctioned way (spec §3.11: the
/// observer calls MigrationSystem.Plan; no formula copy). T4.21-5 adds those
/// RECOMPUTED fields (exit openness ω, φ, the basin caps, the vacancy cap and
/// scale, the per-destination channel totals); until then each is a string
/// field that says "not recorded" and where it is computed (§8 item 5).
///
/// PREV versus NEXT, stated. The system reads Prev for every driver and writes
/// Next: flows are READ from Next; the smoothed signal the gaps used is the
/// UPDATED value the system computed this step and stored on Next; the push
/// deficit and the viability inputs are READ from Prev.
/// </summary>
public sealed class MigrationExplanation
{
    public SettlementId Settlement { get; }

    /// <summary>READ next.MigrationFlows.</summary>
    public long Inflow { get; }
    public long Outflow { get; }

    /// <summary>READ prev.ConsumptionDeficits: the source deficit that drives famine flight.</summary>
    public double PushDeficit { get; }
    public const string PushReading =
        "Famine flight (T4.21-2, ADR-025): per bucket the fraction that leaves in a turn is the bounded hazard "
        + "φ = 1 − exp(−CohortProfile × K × ω × deficit_source × dt), K = BaseRate × FamineFlightFactor, ω = the best "
        + "exit's damping × viability(dst); shares w_j = damping × viability / Σ distribute WHERE, and a destination's "
        + "vacancy bound scales what it accepts — gap-INDEPENDENT, so a stressed settlement empties toward any "
        + "reachable viable destination at most φ < 1 of each bucket per turn, never all of it (MigrationSystem.cs "
        + "header \"FLIGHT\"; Plan \"§3.4\"). Zero deficit, or no viable exit (ω = 0), means no flight.";

    /// <summary>READ next.SmoothedAttractiveness: this settlement's pull signal after this step's EMA update.</summary>
    public double Pull { get; }
    /// <summary>Whether a smoothed row existed on Next (absent only before migration has ever run).</summary>
    public bool PullRecorded { get; }

    /// <summary>This settlement's own viability inputs (as a destination), READ from Prev.</summary>
    public Destination Self { get; }
    /// <summary>Every OTHER settlement in prev.Settlements, sorted (SmoothedAttractiveness DESC, Id ASC).</summary>
    public Destination[] Others { get; }

    /// <summary>SUMMED next.Buckets.UnplacedDeparture over this settlement — the flight desire
    /// stranded because no reachable destination was viable (D-037 B1's colonization input).</summary>
    public double UnplacedDeparture { get; }

    // --- T4.21-5: the SOURCE side of the plan, for this settlement -----------

    /// <summary>The dt the observed step integrated (next.Clock.DtYears), hence
    /// the dt the planner was called with — not a second choice of dt.</summary>
    public double DtYears { get; }
    /// <summary>Whether this settlement was a row of Prev and therefore planned.</summary>
    public bool PlanRecorded { get; }
    /// <summary>ω_i = max_j damping × viability — EXIT OPENNESS. Zero means die at home.</summary>
    public double ExitOpenness { get; }
    /// <summary>φ at cohort profile 1: the mix-free gauge of how hard this
    /// settlement flees, through MigrationSystem's own FlightFractionOf and
    /// FlightHazardScale statics (no restated product).</summary>
    public double FlightFractionPrime { get; }
    /// <summary>Σ_b φ_b × count_b — heads, before shares and the vacancy bound.</summary>
    public double FlightBound { get; }
    /// <summary>The flight channel total after the overdraw scale, in heads.</summary>
    public double FlightOut { get; }
    /// <summary>The gap channel total after the overdraw scale, in heads.</summary>
    public double GapOut { get; }
    /// <summary>f × M*_i^out — the source basin's gap-outflow cap; +∞ when the basin is &lt; 2.</summary>
    public double GapOutflowCap { get; }
    public double SrcScale { get; }

    // --- GAPS, as fields ---------------------------------------------------
    public const string PairwiseFlows =
        "not recorded — the From→To matrix is executed as Ledger.Transfers in ascending (source, dest, bucket) order "
        + "and only per-settlement totals survive (MigrationSystem.Step, the transfer loop; §8 item 5). Per-destination "
        + "CHANNEL totals are recomputable from MigrationSystem.Plan (T4.21-5).";
    public const string Damping =
        "not recorded — damping = exp(−travelCost / DampingDecayCostUnits) is built from Prev SettlementDistances "
        + "each step into the MigrationPlan and discarded (MigrationSystem.Plan, \"Damping matrix from Prev distances\").";
    public const string ViabilityProducts =
        "not recorded — viability = max(0, 1 − Repulsion × deficit) × (1 − w + w × happiness), zeroed by the "
        + "absolute food gate, is computed per destination each step and discarded (MigrationSystem.Plan, "
        + "\"T2.13: destination viability\"); its INPUTS are the Destination fields here.";
    public const string GapScale =
        "not recorded PER PAIR — the gap-closing cap f × m* is a [source, dest] matrix (MigrationSystem.Plan, "
        + "\"T2.8 (a)\") and only per-settlement totals survive the step. The PER-SETTLEMENT bounds it composes "
        + "with — the basin caps at both ends (\"§3.5b\") and the vacancy bound (\"§3.5c\") — are no longer "
        + "silent: T4.21-5 recomputes them through the same public planner and prints them on this settlement "
        + "and on every destination line.";

    private MigrationExplanation(
        SettlementId settlement, long inflow, long outflow, double push, double pull, bool pullRecorded,
        Destination self, Destination[] others, double unplaced,
        double dtYears, bool planRecorded, double exitOpenness, double flightFractionPrime,
        double flightBound, double flightOut, double gapOut, double gapOutflowCap, double srcScale)
    {
        Settlement = settlement; Inflow = inflow; Outflow = outflow; PushDeficit = push;
        Pull = pull; PullRecorded = pullRecorded; Self = self; Others = others; UnplacedDeparture = unplaced;
        DtYears = dtYears; PlanRecorded = planRecorded; ExitOpenness = exitOpenness;
        FlightFractionPrime = flightFractionPrime; FlightBound = flightBound; FlightOut = flightOut;
        GapOut = gapOut; GapOutflowCap = gapOutflowCap; SrcScale = srcScale;
    }

    public static MigrationExplanation For(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId settlement)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));
        var grain = new GoodId(goods.GrainId);

        // T4.21-5: ONE call to the public planner for the whole explanation —
        // the same static MigrationSystem.Step consumes, on the same Prev, at
        // the dt the step integrated. Every plan value below is INDEXED out of
        // it; nothing is re-derived.
        double dt = next.Clock.DtYears;
        NeedsConfig needs = cfg.Needs ?? throw new ArgumentException("SimConfig.Needs is not loaded.", nameof(cfg));
        MigrationPlan plan = MigrationSystem.Plan(prev, cfg, dt, new BasketBook(needs, goods));
        int[] planRow = PlanRows(prev);

        int f = ExplainRows.Flow(next, settlement);
        long inflow = f >= 0 ? next.MigrationFlows[f].Inflow : 0;
        long outflow = f >= 0 ? next.MigrationFlows[f].Outflow : 0;

        int d = ExplainRows.Deficit(prev, settlement);
        double push = d >= 0 ? prev.ConsumptionDeficits[d].DeficitRatio : 0.0;   // MigrationSystem.Plan (signals loop): 0 when absent

        int sm = ExplainRows.Smoothed(next, settlement);
        double pull = sm >= 0 ? next.SmoothedAttractiveness[sm].Value : double.NaN;

        Destination self = Describe(prev, next, cfg, grain, settlement, plan, planRow);

        // Every other settlement, then an insertion sort on the explicit
        // two-half key (value DESC, id ASC) — no comparer over doubles alone,
        // no LINQ; ids are unique so the order is total.
        int n = 0;
        for (int s = 0; s < prev.Settlements.Count; s++) if (prev.Settlements[s].Id != settlement) n++;
        var others = new Destination[n];
        int k = 0;
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId id = prev.Settlements[s].Id;
            if (id == settlement) continue;
            Destination dst = Describe(prev, next, cfg, grain, id, plan, planRow);
            int j = k - 1;
            while (j >= 0 && Before(dst, others[j])) { others[j + 1] = others[j]; j--; }
            others[j + 1] = dst;
            k++;
        }

        double unplaced = 0.0;
        for (int i = 0; i < next.Buckets.Count; i++)
            if (next.Buckets[i].Settlement == settlement) unplaced += next.Buckets[i].UnplacedDeparture;

        int src = Row(planRow, settlement);
        bool planned = src >= 0;
        double omega = planned ? plan.ExitOpenness[src] : double.NaN;
        return new MigrationExplanation(
            settlement, inflow, outflow, push, pull, sm >= 0, self, others, unplaced,
            dt, planned, omega,
            planned
                ? MigrationSystem.FlightFractionOf(
                    1.0, MigrationSystem.FlightHazardScale(cfg.Migration), omega, plan.Deficit[src], plan.DtYears)
                : double.NaN,
            planned ? plan.FlightBound[src] : double.NaN,
            planned ? plan.FlightOut[src] : double.NaN,
            planned ? plan.GapOut[src] : double.NaN,
            planned ? plan.GapOutflowCap[src] : double.NaN,
            planned ? plan.SrcScale[src] : double.NaN);
    }

    /// <summary>prev settlement id → plan row index, −1 when absent. An array,
    /// not a dictionary (law 5).</summary>
    private static int[] PlanRows(IReadOnlyWorldState prev)
    {
        int maxId = 0;
        for (int s = 0; s < prev.Settlements.Count; s++)
            maxId = Math.Max(maxId, prev.Settlements[s].Id.Value);
        var index = new int[maxId + 1];
        Array.Fill(index, -1);
        for (int s = 0; s < prev.Settlements.Count; s++) index[prev.Settlements[s].Id.Value] = s;
        return index;
    }

    private static int Row(int[] planRow, SettlementId id) =>
        id.Value >= 0 && id.Value < planRow.Length ? planRow[id.Value] : -1;

    /// <summary>(value DESC, id ASC): a sorts strictly before b.</summary>
    private static bool Before(in Destination a, in Destination b)
    {
        if (a.SmoothedAttractiveness > b.SmoothedAttractiveness) return true;
        if (a.SmoothedAttractiveness < b.SmoothedAttractiveness) return false;
        return a.Id.Value < b.Id.Value;
    }

    private static Destination Describe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, GoodId grain, SettlementId id,
        MigrationPlan plan, int[] planRow)
    {
        int sm = ExplainRows.Smoothed(next, id);
        double smoothed = sm >= 0 ? next.SmoothedAttractiveness[sm].Value : double.NaN;
        int d = ExplainRows.Deficit(prev, id);
        double deficit = d >= 0 ? prev.ConsumptionDeficits[d].DeficitRatio : 0.0;
        int g = GoodStockIndex.IndexOf(prev.GoodStocks, id, grain);
        long stock = g >= 0 ? prev.GoodStocks[g].Amount.Value : 0;
        long harvest = g >= 0 ? prev.GoodStocks[g].LastProducedUnits : 0;
        // The absolute food gate's presence test (MigrationSystem.Plan, the anyFood read): two READ longs, either > 0.
        bool present = stock > 0 || harvest > 0;
        // Only a settlement present in Prev is a destination the system scored;
        // a colony founded this step has no Prev rows and reads 0 / absent.
        double happiness = ExplainRows.SettlementPresent(prev, id)
            ? SettlementHappiness.Of(prev, id, cfg) : double.NaN;
        int j = Row(planRow, id);
        return j >= 0
            ? new Destination(id, smoothed, deficit, stock, harvest, present, happiness,
                true, plan.Vacancy[j], plan.VacancyCap[j], plan.VacancyScale[j], plan.DesiredInflowAe[j],
                plan.GapInflowCap[j], plan.DestScale[j], plan.GapIn[j], plan.FlightIn[j])
            : new Destination(id, smoothed, deficit, stock, harvest, present, happiness,
                false, double.NaN, double.NaN, double.NaN, double.NaN,
                double.NaN, double.NaN, double.NaN, double.NaN);
    }
}
