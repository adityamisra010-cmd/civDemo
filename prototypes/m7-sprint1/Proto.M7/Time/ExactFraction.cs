using Proto.M7.Fixed;

namespace Proto.M7.Time;

/// <summary>
/// An exact rational number Num/Den (DIR5 §2.5 "preserve fractional time proportionally, never discard it"; ADR-061 §3.10).
///
/// Canonical form: Den &gt; 0 and gcd(|Num|, Den) = 1, so one value has exactly one representation and structural
/// equality, ordering and the canonical state-hash encoding (Num, Den) are all exact. default(ExactFraction) is 0/1:
/// the denominator is stored as Den - 1, so a zero-initialised field is already the canonical zero.
///
/// Arithmetic: every operation forms its result in Int128 intermediates (operands are cross-reduced by gcd first) and
/// narrows back to long with an explicit range check. Proto.M7 builds with CheckForOverflowUnderflow=true, so an Int128
/// intermediate that overflowed would also throw. A result that does not fit is REFUSED with OverflowException; it is
/// never wrapped, rounded or approximated. Every operation is pure: on refusal no input is changed, so a caller that
/// computes all new values before assigning any of them is atomic (see RateIntegrator.IntegrateExact).
/// </summary>
public readonly record struct ExactFraction : IComparable<ExactFraction>
{
    private readonly long _denMinusOne;

    private ExactFraction(long num, long den)
    {
        Num = num;
        _denMinusOne = den - 1;
    }

    public long Num { get; }
    public long Den => _denMinusOne + 1;

    public static readonly ExactFraction Zero = default;
    public static readonly ExactFraction One = new(1, 1);

    public bool IsZero => Num == 0;
    public bool IsNegative => Num < 0;

    /// <summary>num/den reduced to canonical form. den must be non-zero.</summary>
    public static ExactFraction Of(long num, long den) => Canonical(num, den);

    /// <summary>An exact ratio of Int128 quantities, reduced, refused when the reduced form does not fit long.</summary>
    public static ExactFraction Ratio(Int128 num, Int128 den) => Canonical(num, den);

    public static ExactFraction FromInteger(long n) => new(n, 1);

    public static ExactFraction operator +(ExactFraction a, ExactFraction b)
    {
        long g = Gcd(a.Den, b.Den);
        long bOverG = b.Den / g, aOverG = a.Den / g;
        Int128 num = (Int128)a.Num * bOverG + (Int128)b.Num * aOverG;
        Int128 den = (Int128)aOverG * b.Den;
        return Canonical(num, den);
    }

    public static ExactFraction operator -(ExactFraction a) => Canonical(-(Int128)a.Num, a.Den);

    public static ExactFraction operator -(ExactFraction a, ExactFraction b) => a + (-b);

    public static ExactFraction operator *(ExactFraction a, ExactFraction b)
    {
        // Cross-reduction keeps intermediates small: (a.N/g1)(b.N/g2) / ((a.D/g2)(b.D/g1)).
        long g1 = Gcd(Abs(a.Num), b.Den), g2 = Gcd(Abs(b.Num), a.Den);
        Int128 num = (Int128)(a.Num / g1) * (b.Num / g2);
        Int128 den = (Int128)(a.Den / g2) * (b.Den / g1);
        return Canonical(num, den);
    }

    /// <summary>this * k / m, exact (the proportional rescale primitive: a quantity measured over m expressed over k).</summary>
    public ExactFraction Scale(long k, long m)
    {
        if (m == 0) throw new DivideByZeroException("scale denominator is zero");
        return this * Of(k, m);
    }

    /// <summary>Largest integer &lt;= this.</summary>
    public long Floor() => FixedMath.DivFloor(Num, Den);

    /// <summary>Smallest integer &gt;= this.</summary>
    public long Ceiling() => -FixedMath.DivFloor(-Num, Den);

    /// <summary>this - Floor(): the fractional part, always in [0, 1).</summary>
    public ExactFraction FractionalPart() => this - FromInteger(Floor());

    public int CompareTo(ExactFraction other) => ((Int128)Num * other.Den).CompareTo((Int128)other.Num * Den);
    public static bool operator <(ExactFraction a, ExactFraction b) => a.CompareTo(b) < 0;
    public static bool operator >(ExactFraction a, ExactFraction b) => a.CompareTo(b) > 0;
    public static bool operator <=(ExactFraction a, ExactFraction b) => a.CompareTo(b) <= 0;
    public static bool operator >=(ExactFraction a, ExactFraction b) => a.CompareTo(b) >= 0;

    /// <summary>Invariant-culture text "Num/Den" (diagnostics only; never parsed by simulation code).</summary>
    public override string ToString()
        => Num.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + Den.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static ExactFraction Canonical(Int128 num, Int128 den)
    {
        if (den == 0) throw new DivideByZeroException("zero denominator");
        if (den < 0) { num = -num; den = -den; }
        Int128 g = Gcd128(num < 0 ? -num : num, den);
        if (g > 1) { num /= g; den /= g; }
        if (num > long.MaxValue || num < -long.MaxValue || den > long.MaxValue)
            throw new OverflowException($"exact fraction refused: reduced form does not fit long (num {num}, den {den})");
        return new ExactFraction((long)num, (long)den);
    }

    private static long Abs(long v) => v < 0 ? -v : v;

    private static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a < 0 ? -a : a;
    }

    private static Int128 Gcd128(Int128 a, Int128 b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
