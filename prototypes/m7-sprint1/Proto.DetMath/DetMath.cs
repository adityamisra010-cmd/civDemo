using static Proto.DetMath.DoubleDouble;

namespace Proto.DetMath;

/// <summary>
/// O-17 prototype (implementation-spec §7): deterministic, libm-free replacements for the four
/// transcendental functions the campaign calls (Sim.Core: Math.Exp, Math.Log, Math.Pow, Math.Cos —
/// 48 source lines / 49 calls, enumerated in reports/O17-detmath.md).
///
/// Built ONLY from IEEE-754 basic operations (+, -, *, /, and sqrt — all correctly rounded by the
/// standard; Math.Sqrt compiles to sqrtsd on x64) in round-to-nearest-even, comparisons,
/// conversions between double and integers, and exact power-of-two scaling through
/// BitConverter.Int64BitsToDouble / DoubleToInt64Bits. No other System.Math call, no FMA, no float,
/// no table loaded from outside: every table is computed at type initialisation by the same basic
/// operations, so it is bit-identical on every IEEE-754 binary64 platform .NET runs on.
///
/// Method: the argument is reduced exactly (error-free transformations), the kernel is evaluated in
/// double-double (~106-bit) arithmetic, and the final double is the leading half of the normalised
/// double-double. The result is therefore the CORRECTLY ROUNDED value except when the exact result
/// lies within ~2^-95 relative of a rounding midpoint (probability ~2^-42 per call for a "random"
/// argument). This is a design aim, MEASURED (not proven) by the tests against an independent
/// arbitrary-precision oracle (Python decimal) — see reports/O17-detmath.md.
///
/// Domain notes:
///  * Cos is supported for |x| &lt;= 2^28 (Cody–Waite reduction with a 4-part pi/2, ~212 bits).
///    Larger |x| throws ArgumentOutOfRangeException rather than returning an unverified value.
///    The only campaign caller (Box–Muller, HarvestWeatherSystem.cs:324) passes 2*pi*u, u in [0,1).
///  * Special values (NaN, ±inf, ±0, negative bases, subnormals) follow IEEE-754 / C99 Annex F,
///    which is what .NET Math.Exp/Log/Pow/Cos return on Linux (asserted by tests).
/// </summary>
public static class DetMath
{
    // ---- exact constants (bit patterns generated with a 120-digit decimal oracle; see report) ----
    /// <summary>ln 2 high part, 32 significant bits (fdlibm ln2_hi): k * Ln2Hi is exact for |k| &lt; 2^21.</summary>
    private static readonly double Ln2Hi = BitConverter.Int64BitsToDouble(0x3FE62E42FEE00000);
    /// <summary>nearest double to ln2 - Ln2Hi.</summary>
    private static readonly double Ln2Mid = BitConverter.Int64BitsToDouble(0x3DEA39EF35793C76);
    /// <summary>nearest double to ln2 - Ln2Hi - Ln2Mid (total ~139 bits of ln 2).</summary>
    private static readonly double Ln2Lo = BitConverter.Int64BitsToDouble(0x3A8CC01F97B57A08);
    /// <summary>1/ln2 rounded; used only to CHOOSE k (any nearby k is fine; correctness does not depend on it).</summary>
    private const double InvLn2 = 1.4426950408889634;

    /// <summary>pi/2 as an unevaluated sum of four doubles (~212 bits), each the nearest double to the remainder.</summary>
    private static readonly double PiO2_1 = BitConverter.Int64BitsToDouble(0x3FF921FB54442D18);
    private static readonly double PiO2_2 = BitConverter.Int64BitsToDouble(0x3C91A62633145C07);
    private static readonly double PiO2_3 = BitConverter.Int64BitsToDouble(unchecked((long)0xB91F1976B7ED8FBCUL));
    private static readonly double PiO2_4 = BitConverter.Int64BitsToDouble(0x35B4CF98E804177D);
    /// <summary>2/pi rounded; used only to CHOOSE the quadrant k.</summary>
    private const double TwoOverPi = 0.6366197723675814;
    /// <summary>nearest double below pi/4 used as the "no reduction needed" bound.</summary>
    private const double PiO4 = 0.7853981633974483;

    private const double Two52 = 4503599627370496.0;          // 2^52
    private const double Two54 = 18014398509481984.0;         // 2^54
    private const double Two28 = 268435456.0;                 // 2^28
    private const double Two64 = 18446744073709551616.0;      // 2^64
    private const double TwoM27 = 7.450580596923828125E-09;   // 2^-27
    private const double Sqrt2 = 1.4142135623730951;          // nearest double to sqrt(2); a branch bound only
    private const double ExpOverflow = 709.79;                // exp(709.79) > DBL_MAX for certain (ln DBL_MAX = 709.7827…)
    private const double ExpUnderflow = -746.0;               // exp(-746) < 2^-1075: rounds to +0 for certain

    // ---- tables computed once from basic operations ----
    private const int ExpTaylorTerms = 10;                    // |r/256| <= 0.00136: r^11/11! < 2^-112
    private const int ExpSquarings = 8;                       // r is divided by 2^8 then squared back 8 times
    private const int LogSeriesTerms = 8;                     // |s| <= ~0.0021: s^16/17 < 2^-140
    private const int LogTableMin = 88, LogTableMax = 184;    // j = round(128 m), m in [sqrt(1/2), sqrt(2)] -> 91..181
    private const int TrigTerms = 15;                         // |r| <= pi/4: r^30/30! < 2^-113

    private static readonly double[] InvFactHi, InvFactLo;   // 1/n!, n = 0..ExpTaylorTerms
    private static readonly double[] OddInvHi, OddInvLo;     // 1/(2i+1), i = 0..29
    private static readonly double[] InvC;                   // nearest double to 128/j
    private static readonly double[] LogCHi, LogCLo;         // -log(InvC[j]) as double-double
    private static readonly double[] CosCHi, CosCLo;         // (-1)^n/(2n)!
    private static readonly double[] SinCHi, SinCLo;         // (-1)^n/(2n+1)!

    static DetMath()
    {
        InvFactHi = new double[ExpTaylorTerms + 1];
        InvFactLo = new double[ExpTaylorTerms + 1];
        double fh = 1.0, fl = 0.0;
        InvFactHi[0] = 1.0; InvFactLo[0] = 0.0;
        for (int n = 1; n <= ExpTaylorTerms; n++)
        {
            Div(fh, fl, n, 0.0, out fh, out fl);
            InvFactHi[n] = fh; InvFactLo[n] = fl;
        }

        OddInvHi = new double[30];
        OddInvLo = new double[30];
        for (int i = 0; i < 30; i++)
        {
            Div(1.0, 0.0, 2 * i + 1, 0.0, out OddInvHi[i], out OddInvLo[i]);
        }

        int count = LogTableMax - LogTableMin + 1;
        InvC = new double[count];
        LogCHi = new double[count];
        LogCLo = new double[count];
        for (int j = LogTableMin; j <= LogTableMax; j++)
        {
            double invc = 128.0 / j;                         // correctly rounded IEEE division
            InvC[j - LogTableMin] = invc;
            LogSeries(invc, 0.0, 30, out double lh, out double ll);   // log(invc) to ~2^-106
            LogCHi[j - LogTableMin] = -lh;
            LogCLo[j - LogTableMin] = -ll;
        }

        CosCHi = new double[TrigTerms];
        CosCLo = new double[TrigTerms];
        SinCHi = new double[TrigTerms];
        SinCLo = new double[TrigTerms];
        double ch = 1.0, cl = 0.0;                           // running 1/(2n)!
        double sh = 1.0, sl = 0.0;                           // running 1/(2n+1)!
        for (int n = 0; n < TrigTerms; n++)
        {
            if (n > 0)
            {
                Div(ch, cl, (2 * n - 1) * (2 * n), 0.0, out ch, out cl);
                Div(sh, sl, (2 * n) * (2 * n + 1), 0.0, out sh, out sl);
            }
            bool neg = (n & 1) == 1;
            CosCHi[n] = neg ? -ch : ch; CosCLo[n] = neg ? -cl : cl;
            SinCHi[n] = neg ? -sh : sh; SinCLo[n] = neg ? -sl : sl;
        }
    }

    // ======================================================================================
    // Public API
    // ======================================================================================

    /// <summary>e^x. Deterministic; aims at correct rounding (see class remarks).</summary>
    public static double Exp(double x)
    {
        if (double.IsNaN(x)) return x;                                  // NaN propagates
        if (x > ExpOverflow) return double.PositiveInfinity;
        if (x < ExpUnderflow) return 0.0;
        return ExpCore(x, 0.0);
    }

    /// <summary>Natural logarithm. Log(x&lt;0) = NaN, Log(±0) = -inf, Log(+inf) = +inf.</summary>
    public static double Log(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x < 0.0) return double.NaN;
        if (x == 0.0) return double.NegativeInfinity;
        if (x == double.PositiveInfinity) return x;
        LogCore(x, out double lh, out _);
        return lh;
    }

    /// <summary>x^y with the C99 Annex F / IEEE-754 pow special cases.</summary>
    public static double Pow(double x, double y)
    {
        if (y == 0.0) return 1.0;                              // even for NaN x
        if (x == 1.0) return 1.0;                              // even for NaN y
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;

        double ay = y < 0.0 ? -y : y;
        bool yIsInt = IsInteger(ay);
        bool yIsOddInt = yIsInt && IsOddInteger(ay);

        if (ay == double.PositiveInfinity)
        {
            double ax0 = x < 0.0 ? -x : x;
            if (ax0 == 1.0) return 1.0;                        // (-1)^±inf = 1
            bool bigBase = ax0 > 1.0;
            return (bigBase == (y > 0.0)) ? double.PositiveInfinity : 0.0;
        }

        if (x == 0.0)
        {
            bool negZero = BitConverter.DoubleToInt64Bits(x) < 0;
            if (y < 0.0) return (yIsOddInt && negZero) ? double.NegativeInfinity : double.PositiveInfinity;
            return (yIsOddInt && negZero) ? -0.0 : 0.0;
        }

        if (x == double.NegativeInfinity)
        {
            if (y < 0.0) return yIsOddInt ? -0.0 : 0.0;
            return yIsOddInt ? double.NegativeInfinity : double.PositiveInfinity;
        }
        if (x == double.PositiveInfinity) return y < 0.0 ? 0.0 : double.PositiveInfinity;

        bool negate = false;
        double ax = x;
        if (x < 0.0)
        {
            if (!yIsInt) return double.NaN;                    // negative base, non-integer exponent
            negate = yIsOddInt;
            ax = -x;
        }

        if (ax == 1.0) return negate ? -1.0 : 1.0;             // (-1)^integer: no logarithm needed
        double r = PowPositive(ax, y);
        return negate ? -r : r;
    }

    /// <summary>cos x for |x| &lt;= 2^28 (throws beyond; NaN/inf give NaN).</summary>
    public static double Cos(double x)
    {
        if (double.IsNaN(x)) return x;
        double ax = x < 0.0 ? -x : x;
        if (ax == double.PositiveInfinity) return double.NaN;
        if (ax < TwoM27) return 1.0;                           // cos x in (1 - 2^-55, 1]: rounds to 1
        if (ax > Two28)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x,
                "DetMath.Cos supports |x| <= 2^28 (prototype domain; no campaign caller exceeds 2*pi).");
        }

        double rh, rl;
        long q;
        if (ax <= PiO4)
        {
            rh = ax; rl = 0.0; q = 0;
        }
        else
        {
            double kd = (ax * TwoOverPi + Two52) - Two52;      // nearest integer, ax*2/pi < 2^28
            TwoProd(kd, PiO2_1, out double a, out double ae);  // exact: kd*P1 = a + ae
            TwoSum(ax, -a, out double s, out double se);       // exact
            Add(s, se, -ae, 0.0, out rh, out rl);
            TwoProd(kd, PiO2_2, out double b, out double be);
            Add(rh, rl, -b, -be, out rh, out rl);
            TwoProd(kd, PiO2_3, out double c, out double ce);
            Add(rh, rl, -c, -ce, out rh, out rl);
            Add(rh, rl, -(kd * PiO2_4), 0.0, out rh, out rl);
            q = ((long)kd) & 3;
        }

        double resH, resL;
        switch (q)
        {
            case 0: CosKernel(rh, rl, out resH, out resL); break;
            case 1: SinKernel(rh, rl, out resH, out resL); resH = -resH; break;
            case 2: CosKernel(rh, rl, out resH, out resL); resH = -resH; break;
            default: SinKernel(rh, rl, out resH, out resL); break;
        }
        return resH;
    }

    // ======================================================================================
    // Cores
    // ======================================================================================

    /// <summary>exp of the double-double argument (xh, xl); requires ExpUnderflow &lt;= xh &lt;= ExpOverflow.</summary>
    private static double ExpCore(double xh, double xl)
    {
        // k = nearest integer to x/ln2 (|x/ln2| < 1077, magic-number rounding is exact here).
        // (v + 2^52) - 2^52 rounds a NON-NEGATIVE v < 2^51 to the nearest integer; negatives go through -v.
        double v = xh * InvLn2;
        double kd = v >= 0.0 ? (v + Two52) - Two52 : -((-v + Two52) - Two52);
        int k = (int)kd;

        // r = x - k ln2, exactly reduced into a double-double.
        double r0 = xh - kd * Ln2Hi;                           // exact: kd*Ln2Hi exact, Sterbenz
        TwoProd(kd, Ln2Mid, out double p, out double pe);
        Add(r0, 0.0, -p, -pe, out double rh, out double rl);
        Add(rh, rl, -(kd * Ln2Lo), 0.0, out rh, out rl);
        if (xl != 0.0) Add(rh, rl, xl, 0.0, out rh, out rl);

        // r / 2^8 (exact), expm1 by Taylor (Horner, double-double), then (1+e)^2 = 1 + (2e + e^2) eight times.
        const double Inv256 = 0.00390625;
        rh *= Inv256; rl *= Inv256;
        double eh = InvFactHi[ExpTaylorTerms], el = InvFactLo[ExpTaylorTerms];
        for (int n = ExpTaylorTerms - 1; n >= 1; n--)
        {
            Mul(eh, el, rh, rl, out eh, out el);
            Add(eh, el, InvFactHi[n], InvFactLo[n], out eh, out el);
        }
        Mul(eh, el, rh, rl, out eh, out el);                   // e = expm1(r/256)
        for (int i = 0; i < ExpSquarings; i++)
        {
            Mul(eh, el, eh, el, out double sh, out double sl);
            Add(2.0 * eh, 2.0 * el, sh, sl, out eh, out el);
        }
        Add(1.0, 0.0, eh, el, out double yh, out double yl);   // exp(r) in [~0.7, ~1.42]

        if (k >= -1021)
        {
            if (k <= 1023) return yh * Pow2(k);                // exact scaling of the rounded result
            return (yh * 2.0) * Pow2(k - 1);                   // k = 1024: overflows to +inf correctly
        }

        // Subnormal result: round (yh + yl) * 2^k onto the 2^-1074 grid in ONE rounding.
        double f = Pow2Wide(k + 1074);                         // k + 1074 in [-2, 52]
        double hs = yh * f, ls = yl * f;                       // exact scalings, hs < 2^53
        double n0 = hs >= Two52 ? hs : (hs + Two52) - Two52;   // nearest-even integer to hs
        double hn = hs - n0;                                   // exact, |hn| <= 0.5
        if (hn + ls > 0.5 || (hn == 0.5 && ls > 0.0)) n0 += 1.0;
        else if (hn + ls < -0.5 || (hn == -0.5 && ls < 0.0)) n0 -= 1.0;
        return (n0 * Pow2(-1022)) * Pow2Wide(-52);             // n0 * 2^-1074, exact
    }

    /// <summary>log of a positive finite x as a double-double.</summary>
    private static void LogCore(double x, out double lh, out double ll)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        int e = (int)((bits >> 52) & 0x7FF);
        if (e == 0)
        {                                                      // subnormal: scale by 2^54 (exact)
            x *= Two54;
            bits = BitConverter.DoubleToInt64Bits(x);
            e = (int)((bits >> 52) & 0x7FF) - 54;
        }
        e -= 1023;
        double m = BitConverter.Int64BitsToDouble((bits & 0x000F_FFFF_FFFF_FFFFL) | 0x3FF0_0000_0000_0000L);
        if (m > Sqrt2) { m *= 0.5; e += 1; }                   // m in [sqrt(1/2), sqrt(2)]

        int j = (int)((m * 128.0 + Two52) - Two52);            // nearest integer to 128 m
        int ji = j - LogTableMin;
        double invc = InvC[ji];
        TwoProd(m, invc, out double yh, out double yl);        // y = m * invc exactly, |y - 1| < ~2^-8
        LogSeries(yh, yl, LogSeriesTerms, out double gh, out double gl);   // log y

        // log x = e ln2 + (-log invc) + log y
        double eh = e * Ln2Hi;                                 // exact (|e| <= 1075)
        TwoProd(e, Ln2Mid, out double p, out double pe);
        Add(eh, 0.0, p, pe, out double th, out double tl);
        Add(th, tl, e * Ln2Lo, 0.0, out th, out tl);
        Add(th, tl, LogCHi[ji], LogCLo[ji], out th, out tl);
        Add(th, tl, gh, gl, out lh, out ll);
    }

    /// <summary>log(z) for a double-double z in [0.5, 2] via 2 atanh((z-1)/(z+1)), terms series terms.</summary>
    private static void LogSeries(double zh, double zl, int terms, out double lh, out double ll)
    {
        double dh0 = zh - 1.0;                                 // exact (Sterbenz, zh in [0.5, 2])
        TwoSum(dh0, zl, out double dh, out double dl);         // z - 1
        Add(2.0, 0.0, dh, dl, out double wh, out double wl);   // z + 1
        if (dh == 0.0) { lh = 0.0; ll = 0.0; return; }
        Div(dh, dl, wh, wl, out double sh, out double sl);     // s
        Mul(sh, sl, sh, sl, out double s2h, out double s2l);
        double ph = OddInvHi[terms - 1], pl = OddInvLo[terms - 1];
        for (int i = terms - 2; i >= 0; i--)
        {
            Mul(ph, pl, s2h, s2l, out ph, out pl);
            Add(ph, pl, OddInvHi[i], OddInvLo[i], out ph, out pl);
        }
        Mul(ph, pl, sh, sl, out ph, out pl);
        lh = 2.0 * ph; ll = 2.0 * pl;                          // exact doubling
    }

    /// <summary>ax^y for finite positive ax != 1 and finite non-zero y.</summary>
    private static double PowPositive(double ax, double y)
    {
        // Exponents whose correctly rounded result IS a single IEEE basic operation. These matter because
        // pow can have results that are EXACTLY representable or EXACTLY a rounding midpoint (e.g.
        // (2^52+1)^0.5 lies 2^-108 below a midpoint; odd x in (2^26.5, 2^27) gives x^2 exactly on a
        // midpoint), which no fixed-precision exp(y log x) can round correctly.
        if (y == 1.0) return ax;
        if (y == 2.0) return ax * ax;                          // correctly rounded product
        if (y == -1.0) return 1.0 / ax;                        // correctly rounded quotient
        if (y == 0.5) return Math.Sqrt(ax);                    // IEEE-754 sqrt: correctly rounded (sqrtsd)

        double ay = y < 0.0 ? -y : y;
        if (ay <= 64.0 && IsInteger(ay) && TryIntegerPower(ax, (int)ay, y < 0.0, out double ip)) return ip;

        LogCore(ax, out double lh, out double ll);
        if (ay > Two64)
        {                                                      // |y log x| >= 2^64 * 2^-53: certain over/underflow
            return ((lh > 0.0) == (y > 0.0)) ? double.PositiveInfinity : 0.0;
        }
        TwoProd(y, lh, out double ph, out double pe);
        pe += y * ll;
        FastTwoSum(ph, pe, out ph, out double pl);
        if (ph > ExpOverflow) return double.PositiveInfinity;
        if (ph < ExpUnderflow) return 0.0;
        return ExpCore(ph, pl);
    }

    /// <summary>
    /// ax^n (or ax^-n) for an integer 3 &lt;= n &lt;= 64 by double-double binary powering. When the exact
    /// result fits in 54 bits (the only way it can be exactly a rounding midpoint) every intermediate power
    /// fits in 54 bits too, so every TwoProd is exact and the final rounding is the correct one. Declines
    /// (returns false) when the result could leave the normal range, so no intermediate overflows.
    /// </summary>
    private static bool TryIntegerPower(double ax, int n, bool negative, out double result)
    {
        result = 0.0;
        long bits = BitConverter.DoubleToInt64Bits(ax);
        int ex = (int)((bits >> 52) & 0x7FF);
        if (ex == 0) return false;                             // subnormal base: use the general path
        ex -= 1023;
        int mag = ex < 0 ? -ex : ex;
        if ((long)(mag + 1) * n > 900) return false;           // |log2 result| may exceed ~900: general path

        double ph = 1.0, pl = 0.0, bh = ax, bl = 0.0;
        int k = n;
        while (true)
        {
            if ((k & 1) == 1) Mul(ph, pl, bh, bl, out ph, out pl);
            k >>= 1;
            if (k == 0) break;
            Mul(bh, bl, bh, bl, out bh, out bl);
        }
        if (negative) Div(1.0, 0.0, ph, pl, out ph, out pl);
        result = ph;
        return true;
    }

    private static void CosKernel(double rh, double rl, out double ch, out double cl)
    {
        Mul(rh, rl, rh, rl, out double r2h, out double r2l);
        double ph = CosCHi[TrigTerms - 1], pl = CosCLo[TrigTerms - 1];
        for (int n = TrigTerms - 2; n >= 0; n--)
        {
            Mul(ph, pl, r2h, r2l, out ph, out pl);
            Add(ph, pl, CosCHi[n], CosCLo[n], out ph, out pl);
        }
        ch = ph; cl = pl;
    }

    private static void SinKernel(double rh, double rl, out double sh, out double sl)
    {
        Mul(rh, rl, rh, rl, out double r2h, out double r2l);
        double ph = SinCHi[TrigTerms - 1], pl = SinCLo[TrigTerms - 1];
        for (int n = TrigTerms - 2; n >= 0; n--)
        {
            Mul(ph, pl, r2h, r2l, out ph, out pl);
            Add(ph, pl, SinCHi[n], SinCLo[n], out ph, out pl);
        }
        Mul(ph, pl, rh, rl, out sh, out sl);
    }

    // ======================================================================================
    // Helpers (bit-exact)
    // ======================================================================================

    /// <summary>2^k for a normal exponent k in [-1022, 1023], built from bits.</summary>
    private static double Pow2(int k) => BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);

    /// <summary>2^k for k in [-1074, 1023] (subnormal powers of two included), built from bits.</summary>
    private static double Pow2Wide(int k) =>
        k >= -1022 ? Pow2(k) : BitConverter.Int64BitsToDouble(1L << (k + 1074));

    /// <summary>True when a finite non-negative a is an integer (basic operations only).</summary>
    private static bool IsInteger(double a)
    {
        if (a >= Two52) return true;                           // every double >= 2^52 is an integer
        return ((a + Two52) - Two52) == a;
    }

    /// <summary>True when a finite non-negative integer-valued a is odd.</summary>
    private static bool IsOddInteger(double a)
    {
        if (a >= 2.0 * Two52) return false;                    // >= 2^53: even
        return (((long)a) & 1L) == 1L;
    }
}
