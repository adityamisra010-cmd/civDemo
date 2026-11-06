using Proto.M7.Fixed;

namespace Proto.M7.Tactical;

/// <summary>
/// [P] Integer summary of one scenario's outcomes under one resolver: counts, sums and sums of squares (exact), plus the
/// nearest-rank quantiles for reporting. Means and variances are kept as exact rationals; every comparison is integer.
/// </summary>
public sealed record PStats(
    int N, int WinA, int WinB, int Draw,
    long CasASum, long CasASumSq, long CasBSum, long CasBSumSq,
    long DurSum, long DurSumSq,
    Dist CasA, Dist CasB, Dist Pulses, Dist DurationMs)
{
    public static PStats Of(IReadOnlyList<POutcome> o)
    {
        if (o.Count == 0) throw new ArgumentException("no outcomes");
        int a = 0, b = 0, d = 0;
        long cas = 0, casSq = 0, cbs = 0, cbsSq = 0, du = 0, duSq = 0;
        var ca = new long[o.Count]; var cb = new long[o.Count]; var p = new long[o.Count]; var dm = new long[o.Count];
        for (int i = 0; i < o.Count; i++)
        {
            var x = o[i];
            if (x.Winner == 0) a++; else if (x.Winner == 1) b++; else d++;
            cas = checked(cas + x.CasA); casSq = checked(casSq + x.CasA * x.CasA);
            cbs = checked(cbs + x.CasB); cbsSq = checked(cbsSq + x.CasB * x.CasB);
            du = checked(du + x.DurationMs); duSq = checked(duSq + x.DurationMs * x.DurationMs);
            ca[i] = x.CasA; cb[i] = x.CasB; p[i] = x.Pulses; dm[i] = x.DurationMs;
        }
        return new PStats(o.Count, a, b, d, cas, casSq, cbs, cbsSq, du, duSq, Dist.Of(ca), Dist.Of(cb), Dist.Of(p), Dist.Of(dm));
    }

    /// <summary>Count of an outcome class: 0 = role A won, 1 = role B won, -1 = neither held the field.</summary>
    public int Count(int outcome) => outcome == 0 ? WinA : outcome == 1 ? WinB : Draw;

    /// <summary>Modal outcome by composite key (count desc, then A &lt; B &lt; draw).</summary>
    public int Modal => WinA >= WinB && WinA >= Draw ? 0 : WinB >= Draw ? 1 : -1;
    public long ModalPermille => FixedMath.MulDivFloor(Count(Modal), 1000, N);

    /// <summary>Mean, floor (reporting only; checks use exact cross-multiplication).</summary>
    public static long MeanFloor(long sum, int n) => FixedMath.DivFloor(sum, n);
}

/// <summary>The checks of one (reference, candidate) comparison under rule P1. Gap fields are reporting values (floor).</summary>
public sealed record PCheck(
    bool ModalRequired, int RefModal, int CandModal, bool ModalOk,
    long WinBand, long WinGapA, long WinGapB, long WinGapDraw, bool WinOk,
    long CasABand, long CasAGap, bool CasAOk,
    long CasBBand, long CasBGap, bool CasBOk,
    long DurBand, long DurGap, bool DurOk,
    long PulseMedianGap, bool PulsesOk)
{
    public bool Pass => ModalOk && WinOk && CasAOk && CasBOk && DurOk && PulsesOk;

    public string FailedList()
    {
        var l = new List<string>();
        if (!ModalOk) l.Add("victor");
        if (!WinOk) l.Add("win");
        if (!CasAOk) l.Add("casA");
        if (!CasBOk) l.Add("casB");
        if (!DurOk) l.Add("duration");
        if (!PulsesOk) l.Add("pulses");
        return string.Join(",", l);
    }
}

/// <summary>
/// [P] PARITY ACCEPTANCE RULE P1 — a PROPOSAL for the Director (D-079 Q4), PRE-COMMITTED in H-P.md §2 before any tuning.
/// The constants below are the committed values; changing any of them after the acceptance run is forbidden by the lane
/// brief ("never relax the bands after seeing results"). All arithmetic is integer (exact rationals, ISqrt ceilings).
///
/// Per scenario, the candidate C (AutoResolver) is compared with the reference R (TacticalResolver) on two INDEPENDENT
/// samples of n_C and n_R battles (independent seeds; the battery's seeds are independent by construction):
///  V  VICTOR: if R's modal outcome rate is &gt;= <see cref="ModalThresholdPermille"/>, C's modal outcome must equal it.
///  W  WIN RATES: for each outcome class k in {A wins, B wins, neither}: |r_C(k) - r_R(k)| &lt;= max(WinSubstantive,
///     z * sqrt(p~(1 - p~) (1/n_C + 1/n_R))), p~ = (k_R + 1/2)/(n_R + 1) the Laplace-adjusted reference rate.
///  CA, CB  CASUALTIES per role (fraction of the role's start strength killed or surrendered, permille):
///     |mean_C - mean_R| &lt;= max(CasSubstantive, z * s_R * sqrt(1/n_C + 1/n_R), one entity of the role = ceil(1000/start)),
///     s_R the reference sample standard deviation.
///  D  DURATION (battle end instant - start, ms): |mean_C - mean_R| &lt;= max(DurRelative * mean_R, z * s_R * sqrt(1/n_C +
///     1/n_R), one internal step).
///  P  PULSES: |median_C - median_R| &lt;= 1 pulse (the campaign-facing duration unit).
/// z = <see cref="ZMilli"/>/1000: Bonferroni for ~180 sampling checks (29 scenarios x 6: three win rates, two casualty means,
/// one duration mean = 174; [P fix r1, P-V9] the earlier "~150" here disagreed with H-P.md §2) at a 5 % battery-wide false-rejection rate. The
/// sampling terms use only the REFERENCE's spread, so a noisier candidate does not widen its own band.
/// BATTERY ACCEPTANCE: every scenario of the battery passes every check on the ACCEPTANCE seeds — the held-out scenarios
/// (never seen by the calibration) decide; the tuning scenarios on the same fresh seeds must pass too (seed
/// generalisation) — AND the reference passes against its own independent control sample in every scenario (attainability:
/// a scenario whose bands reject the TacticalResolver against itself is INCONCLUSIVE, never a pass). No "k of n" acceptance.
/// </summary>
public static class ParityCriteriaP1
{
    public const long ZMilli = 3_600;
    public const long ModalThresholdPermille = 600;
    public const long WinSubstantivePermille = 100;
    public const long CasSubstantivePermille = 30;
    public const long DurRelativePermille = 150;
    public const long PulseMedianGapMax = 1;
    /// <summary>Minimum independent acceptance battles per scenario (n_C = n_R = this on the acceptance seeds).</summary>
    public const int MinAcceptanceN = 160;

    /// <summary>ceil(sqrt(x)) for x &gt;= 0.</summary>
    public static long ISqrtCeil(UInt128 x)
    {
        ulong r = (ulong)FixedMath.ISqrt(x);
        return (UInt128)r * r == x ? (long)r : (long)r + 1;
    }

    /// <summary>Win-rate band, permille: max(WinSubstantive, ceil(z sqrt(p~(1-p~)(1/nC + 1/nR)) x 1000)).
    /// permille^2 = (2k+1)(2nR+1-2k)(nC+nR) zMilli^2 / ((2nR+2)^2 nC nR).</summary>
    public static long WinBand(int kRef, int nRef, int nCand)
    {
        if (nRef <= 0 || nCand <= 0 || kRef < 0 || kRef > nRef) throw new ArgumentOutOfRangeException(nameof(kRef));
        UInt128 num = (UInt128)(ulong)(2L * kRef + 1) * (ulong)(2L * nRef + 1 - 2L * kRef) * (ulong)(nCand + nRef) * (ulong)(ZMilli * ZMilli);
        UInt128 den = (UInt128)(ulong)((2L * nRef + 2) * (2L * nRef + 2)) * (ulong)nCand * (ulong)nRef;
        long s = ISqrtCeil((num + den - 1) / den);
        return Math.Max(WinSubstantivePermille, s);
    }

    /// <summary>Sampling floor of a mean comparison: ceil(z s_R sqrt(1/nC + 1/nR)), s_R^2 = (nR SumSq - Sum^2)/(nR (nR - 1)).
    /// 0 when nR &lt; 2 (no spread estimate) or the reference is constant.</summary>
    public static long MeanSamplingFloor(long sum, long sumSq, int nRef, int nCand)
    {
        if (nRef < 2) return 0;
        Int128 ss = (Int128)nRef * sumSq - (Int128)sum * sum;          // nR^2 * population variance >= 0
        if (ss <= 0) return 0;
        // value^2 = ss / (nR (nR-1)) * (nC + nR)/(nC nR) * zMilli^2 / 1e6
        UInt128 num = (UInt128)ss * (ulong)(nCand + nRef) * (ulong)(ZMilli * ZMilli);
        UInt128 den = (UInt128)(ulong)nRef * (ulong)(nRef - 1) * (ulong)nCand * (ulong)nRef * 1_000_000UL;
        return ISqrtCeil((num + den - 1) / den);
    }

    /// <summary>True iff |sumC/nC - sumR/nR| &lt;= band (exact).</summary>
    public static bool MeanWithin(long sumC, int nC, long sumR, int nR, long band)
        => Int128.Abs((Int128)sumC * nR - (Int128)sumR * nC) <= (Int128)band * nC * nR;

    /// <summary>|sumC/nC - sumR/nR|, floor (reporting).</summary>
    public static long MeanGap(long sumC, int nC, long sumR, int nR)
        => (long)(Int128.Abs((Int128)sumC * nR - (Int128)sumR * nC) / ((Int128)nC * nR));

    private static long RateGap(int kC, int nC, int kR, int nR)
        => (long)(Int128.Abs((Int128)kC * nR * 1000 - (Int128)kR * nC * 1000) / ((Int128)nC * nR));

    private static bool RateWithin(int kC, int nC, int kR, int nR, long bandPermille)
        => Int128.Abs((Int128)kC * nR * 1000 - (Int128)kR * nC * 1000) <= (Int128)bandPermille * nC * nR;

    /// <summary>Checks candidate C against reference R under rule P1. startA / startB = role start strengths (entity
    /// resolution floor); stepMs = the internal step (duration resolution floor).</summary>
    public static PCheck Check(PStats r, PStats c, long startA, long startB, long stepMs)
    {
        bool modalReq = r.ModalPermille >= ModalThresholdPermille;
        bool modalOk = !modalReq || c.Modal == r.Modal;

        bool winOk = true;
        long winBandMax = 0;
        foreach (int k in new[] { 0, 1, -1 })
        {
            long band = WinBand(r.Count(k), r.N, c.N);
            winBandMax = Math.Max(winBandMax, band);
            if (!RateWithin(c.Count(k), c.N, r.Count(k), r.N, band)) winOk = false;
        }
        long bandA = Math.Max(Math.Max(CasSubstantivePermille, MeanSamplingFloor(r.CasASum, r.CasASumSq, r.N, c.N)), FixedMath.DivCeil(1000, startA));
        long bandB = Math.Max(Math.Max(CasSubstantivePermille, MeanSamplingFloor(r.CasBSum, r.CasBSumSq, r.N, c.N)), FixedMath.DivCeil(1000, startB));
        long durRel = FixedMath.MulDivFloor(PStats.MeanFloor(r.DurSum, r.N), DurRelativePermille, 1000);
        long bandD = Math.Max(Math.Max(durRel, MeanSamplingFloor(r.DurSum, r.DurSumSq, r.N, c.N)), stepMs);
        long pulseGap = Math.Abs(c.Pulses.P50 - r.Pulses.P50);
        return new PCheck(
            modalReq, r.Modal, c.Modal, modalOk,
            winBandMax, RateGap(c.WinA, c.N, r.WinA, r.N), RateGap(c.WinB, c.N, r.WinB, r.N), RateGap(c.Draw, c.N, r.Draw, r.N), winOk,
            bandA, MeanGap(c.CasASum, c.N, r.CasASum, r.N), MeanWithin(c.CasASum, c.N, r.CasASum, r.N, bandA),
            bandB, MeanGap(c.CasBSum, c.N, r.CasBSum, r.N), MeanWithin(c.CasBSum, c.N, r.CasBSum, r.N, bandB),
            bandD, MeanGap(c.DurSum, c.N, r.DurSum, r.N), MeanWithin(c.DurSum, c.N, r.DurSum, r.N, bandD),
            pulseGap, pulseGap <= PulseMedianGapMax);
    }
}
