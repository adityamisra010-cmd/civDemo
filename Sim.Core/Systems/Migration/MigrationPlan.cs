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
/// The scale chain (ADR-025 §2.4, pinned order): pair gap cap → destination
/// basin → source basin → vacancy → overdraw. Each stage is a pure function of
/// Prev and of the stages before it; the plan holds every stage so a test can
/// check the composition and the observer can print each factor.
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
    /// <summary>The PREV ConsumptionDeficits ratio (0 when the row is absent) — NOMINAL d (§3.4).</summary>
    public double[] Deficit { get; }
    /// <summary>T2.13 absolute food gate input: PREV grain store &gt; 0 OR last harvest &gt; 0.</summary>
    public bool[] AnyFood { get; }
    /// <summary>Destination viability: anyFood × max(0, 1 − Repulsion·d) × (1 − w + w·happiness). Nominal d.</summary>
    public double[] Viability { get; }
    /// <summary>Index of the settlement's row in PREV SmoothedAttractiveness, −1 on first sighting.</summary>
    public int[] SmoothedRowIndex { get; }
    /// <summary>S after this step's EMA update (the value Step writes to the owned table).</summary>
    public double[] Smoothed { get; }
    /// <summary>T4.4: whether the source has ANY SettlementDistances row (missing data ≠ isolation).</summary>
    public bool[] HasDistances { get; }
    /// <summary>Per settlement, its PREV bucket rows in table order (the bucket-key order).</summary>
    public int[][] BucketRows { get; }

    // --- source side (§3.4) ------------------------------------------------------
    /// <summary>ω_i = max_{j≠i} damping_ij × viability_j — EXIT OPENNESS ∈ [0,1]; 0 ⇒ die at home.</summary>
    public double[] ExitOpenness { get; }
    /// <summary>Z_i = Σ_{j≠i} damping_ij × viability_j — the share normaliser.</summary>
    public double[] ShareNormaliser { get; }
    /// <summary>Σ_b φ_b × count_b — the source's flight bound this turn, in heads, before shares and vacancy.</summary>
    public double[] FlightBound { get; }

    // --- destination side (§3.5) -------------------------------------------------
    /// <summary>f × M*_j^in — the destination basin's gap-inflow cap; +∞ when |B_j| &lt; 2 (no basin bound).</summary>
    public double[] GapInflowCap { get; }
    /// <summary>Σ_{i∈B_j} gapScale_ij × gapDesire_ij — the pair-capped gap inflow the basin cap compares against.</summary>
    public double[] GapInflowPairCapped { get; }
    /// <summary>destScale_j ∈ (0,1]; literally 1.0 unless the destination basin cap bites.</summary>
    public double[] DestScale { get; }
    /// <summary>f × M*_i^out — the source basin's gap-outflow cap; +∞ when |C_i| &lt; 2.</summary>
    public double[] GapOutflowCap { get; }
    /// <summary>Σ_{j∈C_i} gapScale_ij × destScale_j × gapDesire_ij — the outflow the source basin cap compares against.</summary>
    public double[] GapOutflowDestScaled { get; }
    /// <summary>srcScale_i ∈ (0,1]; literally 1.0 unless the source basin cap bites.</summary>
    public double[] SrcScale { get; }
    /// <summary>V_j = FoodHeadroom.Vacancy(prev, j) in adult-equivalents; +∞ when j carries no demand row.</summary>
    public double[] Vacancy { get; }
    /// <summary>cap_j = (1 − exp(−k·dt)) × V_j; +∞ when V_j is.</summary>
    public double[] VacancyCap { get; }
    /// <summary>in_j = Σ_i Σ_b (gapExecuted_ib→j + φ_b·count_b·w_ij) × cohortWeight[c_b] — desired inflow, adult-equivalents.</summary>
    public double[] DesiredInflowAe { get; }
    /// <summary>vacScale_j = in_j &gt; cap_j ? cap_j / in_j : 1.0; literally 1.0 on the fed path.</summary>
    public double[] VacancyScale { get; }

    // --- channel totals, heads, AFTER the overdraw scale (what the transfer loop asks for) ---
    public double[] GapIn { get; }
    public double[] FlightIn { get; }
    public double[] GapOut { get; }
    public double[] FlightOut { get; }

    // --- pairwise, [source, dest] --------------------------------------------
    /// <summary>exp(−travelCost / DampingDecayCostUnits); 0 when unreachable or no row.</summary>
    public double[,] Damping { get; }
    /// <summary>The pair's total UNCAPPED gap desire across the source's buckets (heads).</summary>
    public double[,] GapPairDesire { get; }
    /// <summary>T2.8 (a): the pair's gap-closing scale min(1, f·m* / gapDesire); 0 when moot.</summary>
    public double[,] GapScale { get; }
    /// <summary>gapScale × destScale × srcScale × vacScale — the executed gap scale (each factor multiplied only when &lt; 1).</summary>
    public double[,] GapScaleExecuted { get; }
    /// <summary>w_ij = damping_ij × viability_j / Z_i; 0 when Z_i = 0. Shares sum to 1 per source.</summary>
    public double[,] Share { get; }
    /// <summary>w_ij × vacScale_j when the source has flight (d &gt; 0, ω &gt; 0), else 0 — the transfer loop's flight gate.</summary>
    public double[,] FlightWeight { get; }
    /// <summary>The per-pair GAP push the transfer loop multiplies: damping × viability × (gapScaleExecuted × gap).</summary>
    public double[,] Push { get; }

    // --- per bucket row ---------------------------------------------------------
    /// <summary>D-037 B1 readout value for the row (0 unless B1's condition holds): (1 − e^{−profile·K·d·dt}) × count.</summary>
    public double[] Unplaced { get; }
    /// <summary>φ_b = 1 − exp(−CohortProfile[c_b] × K × ω_i × d_i × dt) — the row's per-turn flight fraction.</summary>
    public double[] FlightFraction { get; }
    /// <summary>Σ over destinations of the row's desire (gap + flight) — the overdraw scaler's denominator.</summary>
    public double[] DesiredTotal { get; }
    /// <summary>The overdraw scale: count / DesiredTotal when DesiredTotal &gt; count, else 1.0 (the backstop).</summary>
    public double[] OverdrawScale { get; }

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
        ExitOpenness = new double[settlementCount];
        ShareNormaliser = new double[settlementCount];
        FlightBound = new double[settlementCount];
        GapInflowCap = new double[settlementCount];
        GapInflowPairCapped = new double[settlementCount];
        DestScale = new double[settlementCount];
        GapOutflowCap = new double[settlementCount];
        GapOutflowDestScaled = new double[settlementCount];
        SrcScale = new double[settlementCount];
        Vacancy = new double[settlementCount];
        VacancyCap = new double[settlementCount];
        DesiredInflowAe = new double[settlementCount];
        VacancyScale = new double[settlementCount];
        GapIn = new double[settlementCount];
        FlightIn = new double[settlementCount];
        GapOut = new double[settlementCount];
        FlightOut = new double[settlementCount];
        Damping = new double[settlementCount, settlementCount];
        GapPairDesire = new double[settlementCount, settlementCount];
        GapScale = new double[settlementCount, settlementCount];
        GapScaleExecuted = new double[settlementCount, settlementCount];
        Share = new double[settlementCount, settlementCount];
        FlightWeight = new double[settlementCount, settlementCount];
        Push = new double[settlementCount, settlementCount];
        Unplaced = new double[bucketCount];
        FlightFraction = new double[bucketCount];
        DesiredTotal = new double[bucketCount];
        OverdrawScale = new double[bucketCount];
        for (int s = 0; s < settlementCount; s++)
        {
            GapInflowCap[s] = double.PositiveInfinity;
            DestScale[s] = 1.0;
            GapOutflowCap[s] = double.PositiveInfinity;
            SrcScale[s] = 1.0;
            Vacancy[s] = double.PositiveInfinity;
            VacancyCap[s] = double.PositiveInfinity;
            VacancyScale[s] = 1.0;
        }
        for (int i = 0; i < bucketCount; i++) OverdrawScale[i] = 1.0;
    }
}
