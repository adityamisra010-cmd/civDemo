namespace Proto.DetMath;

/// <summary>
/// Error-free transformations and double-double ("dd", ~106-bit) arithmetic built ONLY from IEEE-754
/// basic operations (+, -, *, /) in round-to-nearest-even. No FMA is used (Dekker/Veltkamp splitting
/// gives the exact product instead), so the result does not depend on whether the CPU or JIT has FMA:
/// RyuJIT never contracts a separate a*b+c into an FMA (recon-determinism.md §4, KNOWN .NET BEHAVIOUR).
/// References: Dekker 1971; Knuth TAOCP vol. 2 §4.2.2 (TwoSum); Shewchuk 1997; Hida, Li, Bailey QD library 2001.
/// All methods are pure functions of their arguments.
/// </summary>
internal static class DoubleDouble
{
    /// <summary>2^27 + 1, Veltkamp's splitter for binary64.</summary>
    private const double Splitter = 134217729.0;

    /// <summary>Knuth TwoSum: s + e == a + b exactly, s = fl(a + b). No precondition.</summary>
    internal static void TwoSum(double a, double b, out double s, out double e)
    {
        s = a + b;
        double bb = s - a;
        e = (a - (s - bb)) + (b - bb);
    }

    /// <summary>Dekker FastTwoSum: exact when |a| &gt;= |b| (or a == 0).</summary>
    internal static void FastTwoSum(double a, double b, out double s, out double e)
    {
        s = a + b;
        e = b - (s - a);
    }

    /// <summary>Veltkamp split: a == hi + lo, each with at most 26 significant bits. |a| must be below ~2^995.</summary>
    internal static void Split(double a, out double hi, out double lo)
    {
        double c = Splitter * a;
        hi = c - (c - a);
        lo = a - hi;
    }

    /// <summary>Dekker TwoProduct: p + e == a * b exactly (barring under/overflow), p = fl(a * b).</summary>
    internal static void TwoProd(double a, double b, out double p, out double e)
    {
        p = a * b;
        Split(a, out double ah, out double al);
        Split(b, out double bh, out double bl);
        e = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
    }

    /// <summary>dd + dd (the accurate "IEEE" variant of QD's add).</summary>
    internal static void Add(double ah, double al, double bh, double bl, out double rh, out double rl)
    {
        TwoSum(ah, bh, out double s, out double e);
        TwoSum(al, bl, out double t, out double f);
        e += t;
        FastTwoSum(s, e, out s, out e);
        e += f;
        FastTwoSum(s, e, out rh, out rl);
    }

    /// <summary>dd * dd.</summary>
    internal static void Mul(double ah, double al, double bh, double bl, out double rh, out double rl)
    {
        TwoProd(ah, bh, out double p, out double e);
        e += ah * bl + al * bh;
        FastTwoSum(p, e, out rh, out rl);
    }

    /// <summary>dd * double.</summary>
    internal static void MulD(double ah, double al, double b, out double rh, out double rl)
    {
        TwoProd(ah, b, out double p, out double e);
        e += al * b;
        FastTwoSum(p, e, out rh, out rl);
    }

    /// <summary>dd / dd by three quotient-digit long division (QD "accurate" division).</summary>
    internal static void Div(double ah, double al, double bh, double bl, out double rh, out double rl)
    {
        double q1 = ah / bh;
        MulD(bh, bl, q1, out double ph, out double pl);
        Add(ah, al, -ph, -pl, out double rh1, out double rl1);
        double q2 = rh1 / bh;
        MulD(bh, bl, q2, out ph, out pl);
        Add(rh1, rl1, -ph, -pl, out rh1, out rl1);
        double q3 = rh1 / bh;
        FastTwoSum(q1, q2, out double s, out double e);
        Add(s, e, q3, 0.0, out rh, out rl);
    }
}
