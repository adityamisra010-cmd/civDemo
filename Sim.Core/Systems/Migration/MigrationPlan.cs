namespace Sim.Core.Systems.Migration;

/// <summary>
/// T4.21-2 (docs/t4.21-architecture.md §3.11, ADR-025 §2.6) — everything
/// <see cref="MigrationSystem.Step"/> decides BEFORE it moves a person, as a
/// pure function of Prev. <see cref="MigrationSystem.Plan"/> builds one per
/// step; the transfer loop consumes it and recomputes nothing. The observer
/// calls the same static, so there is ONE implementation of the arithmetic and
/// no observer-side copy that could drift (the §0 "recomputed" field kind).
///
/// Every array is indexed by the PREV settlement row (0..SettlementCount−1) or
/// the PREV bucket row (0..BucketCount−1); pair matrices are [source, dest].
/// The values are the exact doubles the transfer loop multiplies — not
/// re-derived summaries — so the goldens pin the refactor: a plan that differs
/// by one ULP from what Step used to compute inline moves a hash.
///
/// NOT SERIALIZED, NOT STATE. A plan is discarded at the end of the step; the
/// only persistent migration state is the EMA filter table and the D-004
/// remainders, both written by Step from what the plan holds.
/// </summary>
public sealed class MigrationPlan
{
    public int SettlementCount { get; }
    public int BucketCount { get; }
    public double DtYears { get; }

    // --- per settlement, from Prev ------------------------------------------
    /// <summary>P: total PREV count over the settlement's buckets (raw; m* uses physics).</summary>
    public long[] Population { get; }
    /// <summary>R = AttractivenessLandWeight × EffectiveArableKm2 (T4.10: land only).</summary>
    public double[] Resources { get; }
    /// <summary>A = R / max(P, 1) — instantaneous attractiveness.</summary>
    public double[] Instant { get; }
    /// <summary>The PREV ConsumptionDeficits ratio (0 when the row is absent) — NOMINAL d.</summary>
    public double[] Deficit { get; }
    /// <summary>T2.13 absolute food gate input: PREV grain store &gt; 0 OR last harvest &gt; 0.</summary>
    public bool[] AnyFood { get; }
    /// <summary>Destination viability: anyFood × max(0, 1 − Repulsion·d) × (1 − w + w·happiness).</summary>
    public double[] Viability { get; }
    /// <summary>Index of the settlement's row in PREV SmoothedAttractiveness, −1 on first sighting.</summary>
    public int[] SmoothedRowIndex { get; }
    /// <summary>S after this step's EMA update (the value Step writes to the owned table).</summary>
    public double[] Smoothed { get; }
    /// <summary>T4.4: whether the source has ANY SettlementDistances row (missing data ≠ isolation).</summary>
    public bool[] HasDistances { get; }
    /// <summary>Per settlement, its PREV bucket rows in table order (the bucket-key order).</summary>
    public int[][] BucketRows { get; }

    // --- pairwise, [source, dest] --------------------------------------------
    /// <summary>exp(−travelCost / DampingDecayCostUnits); 0 when unreachable or no row.</summary>
    public double[,] Damping { get; }
    /// <summary>T2.8 (a): the pair's gap-closing scale min(1, f·m* / gapDesire); 0 when moot.</summary>
    public double[,] GapScale { get; }
    /// <summary>The per-pair push the transfer loop multiplies: damping × viability × (gapScale × gap + FFF × d).</summary>
    public double[,] Push { get; }

    // --- per bucket row ---------------------------------------------------------
    /// <summary>D-037 B1 readout value for the row (0 unless B1's condition holds).</summary>
    public double[] Unplaced { get; }
    /// <summary>Σ over destinations of the row's desire — the overdraw scaler's denominator.</summary>
    public double[] DesiredTotal { get; }

    internal MigrationPlan(int settlementCount, int bucketCount, double dtYears)
    {
        SettlementCount = settlementCount;
        BucketCount = bucketCount;
        DtYears = dtYears;
        Population = new long[settlementCount];
        Resources = new double[settlementCount];
        Instant = new double[settlementCount];
        Deficit = new double[settlementCount];
        AnyFood = new bool[settlementCount];
        Viability = new double[settlementCount];
        SmoothedRowIndex = new int[settlementCount];
        Smoothed = new double[settlementCount];
        HasDistances = new bool[settlementCount];
        BucketRows = new int[settlementCount][];
        Damping = new double[settlementCount, settlementCount];
        GapScale = new double[settlementCount, settlementCount];
        Push = new double[settlementCount, settlementCount];
        Unplaced = new double[bucketCount];
        DesiredTotal = new double[bucketCount];
    }
}
