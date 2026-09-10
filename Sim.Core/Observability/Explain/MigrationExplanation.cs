using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability.Explain;

/// <summary>
/// One settlement as a migration DESTINATION, as the mechanism sees it: its
/// smoothed attractiveness (the pull signal) and the three viability inputs.
/// Sorted (SmoothedAttractiveness DESC, Id ASC) in <see cref="MigrationExplanation.Others"/>.
/// </summary>
public readonly record struct Destination(
    SettlementId Id, double SmoothedAttractiveness, double DestinationDeficit,
    long GrainStock, long LastHarvest, bool GrainPresent, double Happiness);

/// <summary>
/// T4.19 — WHY PEOPLE LEFT, WHY THEY CAME (docs/observability-architecture.md §6).
///
/// WHAT IS STORED, read as stored. Per settlement per turn the migration
/// system records only Inflow and Outflow (MigrationFlowRow, MigrationSystem.cs:146-148,
/// 469-470). The DRIVERS it read are stored too, on other systems' rows, and
/// this record shows every one of them:
///   push — the source's PREV DeficitRatio (famine flight, gap-independent,
///          MigrationSystem.cs:20-25, 42-46, 297, 400-402);
///   pull — the SMOOTHED attractiveness S (MigrationSystem.cs:242-255), for this
///          settlement and EVERY other, because the mechanism responds to the
///          GAP max(0, S_dst − S_src) and a gap needs both ends visible;
///   viability — per destination: PREV deficit (repulsion), grain presence
///          (store > 0 or last harvest > 0, the absolute food gate) and happiness
///          (SettlementHappiness.Of on PREV), MigrationSystem.cs:170-174, 225-235;
///   unplaced — Σ BucketRow.UnplacedDeparture written when NO reachable
///          destination is viable (MigrationSystem.cs:317-343).
///
/// THE GAPS ARE FIELDS, NOT RECOMPUTATIONS. The pairwise From→To flows, the
/// damping matrix exp(−cost/D), the per-destination viability PRODUCTS and the
/// gap-closing scale are all computed transiently and discarded
/// (MigrationSystem.cs:258-277, 348-382). They COULD be recomputed here from
/// Prev — and they are deliberately not, because a recomputation is a second
/// implementation of MigrationSystem that would silently lie the day the system
/// changes. Each is a string field that says "not recorded" and where it is
/// computed (§8 item 5).
///
/// PREV versus NEXT, stated. The system reads Prev for every driver and writes
/// Next: flows are READ from Next; the smoothed signal the gaps used is the
/// UPDATED value the system computed this step and stored on Next
/// (MigrationSystem.cs:251-254, then 360, 401); the push deficit and the
/// viability inputs are READ from Prev.
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
        "Famine flight: desired outflow per bucket = BaseRate × CohortProfile × count × dt × damping × viability(dst) "
        + "× FamineFlightFactor × deficit_source — gap-INDEPENDENT, so a starving settlement empties toward any "
        + "reachable viable destination (MigrationSystem.cs:20-25, 42-46, 297, 400-402). Zero deficit means no flight desire.";

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

    // --- GAPS, as fields ---------------------------------------------------
    public const string PairwiseFlows =
        "not recorded — the From→To matrix is executed as Ledger.Transfers in ascending (source, dest, bucket) order "
        + "and only per-settlement totals survive (MigrationSystem.cs:409-474; §8 item 5).";
    public const string Damping =
        "not recorded — damping = exp(−travelCost / DampingDecayCostUnits) is built from Prev SettlementDistances "
        + "each step and discarded (MigrationSystem.cs:258-277).";
    public const string ViabilityProducts =
        "not recorded — viability = max(0, 1 − Repulsion × deficit) × (1 − w + w × happiness), zeroed by the "
        + "absolute food gate, is computed per destination each step and discarded (MigrationSystem.cs:225-235); "
        + "its INPUTS are the Destination fields here.";
    public const string GapScale =
        "not recorded — the per-pair gap-closing cap f × m* (MigrationSystem.cs:348-382) is transient.";

    private MigrationExplanation(
        SettlementId settlement, long inflow, long outflow, double push, double pull, bool pullRecorded,
        Destination self, Destination[] others, double unplaced)
    {
        Settlement = settlement; Inflow = inflow; Outflow = outflow; PushDeficit = push;
        Pull = pull; PullRecorded = pullRecorded; Self = self; Others = others; UnplacedDeparture = unplaced;
    }

    public static MigrationExplanation For(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId settlement)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));
        var grain = new GoodId(goods.GrainId);

        int f = ExplainRows.Flow(next, settlement);
        long inflow = f >= 0 ? next.MigrationFlows[f].Inflow : 0;
        long outflow = f >= 0 ? next.MigrationFlows[f].Outflow : 0;

        int d = ExplainRows.Deficit(prev, settlement);
        double push = d >= 0 ? prev.ConsumptionDeficits[d].DeficitRatio : 0.0;   // MigrationSystem.cs:182-184: 0 when absent

        int sm = ExplainRows.Smoothed(next, settlement);
        double pull = sm >= 0 ? next.SmoothedAttractiveness[sm].Value : double.NaN;

        Destination self = Describe(prev, next, cfg, grain, settlement);

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
            Destination dst = Describe(prev, next, cfg, grain, id);
            int j = k - 1;
            while (j >= 0 && Before(dst, others[j])) { others[j + 1] = others[j]; j--; }
            others[j + 1] = dst;
            k++;
        }

        double unplaced = 0.0;
        for (int i = 0; i < next.Buckets.Count; i++)
            if (next.Buckets[i].Settlement == settlement) unplaced += next.Buckets[i].UnplacedDeparture;

        return new MigrationExplanation(settlement, inflow, outflow, push, pull, sm >= 0, self, others, unplaced);
    }

    /// <summary>(value DESC, id ASC): a sorts strictly before b.</summary>
    private static bool Before(in Destination a, in Destination b)
    {
        if (a.SmoothedAttractiveness > b.SmoothedAttractiveness) return true;
        if (a.SmoothedAttractiveness < b.SmoothedAttractiveness) return false;
        return a.Id.Value < b.Id.Value;
    }

    private static Destination Describe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, GoodId grain, SettlementId id)
    {
        int sm = ExplainRows.Smoothed(next, id);
        double smoothed = sm >= 0 ? next.SmoothedAttractiveness[sm].Value : double.NaN;
        int d = ExplainRows.Deficit(prev, id);
        double deficit = d >= 0 ? prev.ConsumptionDeficits[d].DeficitRatio : 0.0;
        int g = GoodStockIndex.IndexOf(prev.GoodStocks, id, grain);
        long stock = g >= 0 ? prev.GoodStocks[g].Amount.Value : 0;
        long harvest = g >= 0 ? prev.GoodStocks[g].LastProducedUnits : 0;
        // The absolute food gate's presence test (MigrationSystem.cs:170-174): two READ longs, either > 0.
        bool present = stock > 0 || harvest > 0;
        // Only a settlement present in Prev is a destination the system scored;
        // a colony founded this step has no Prev rows and reads 0 / absent.
        double happiness = ExplainRows.SettlementPresent(prev, id)
            ? SettlementHappiness.Of(prev, id, cfg) : double.NaN;
        return new Destination(id, smoothed, deficit, stock, harvest, present, happiness);
    }
}
