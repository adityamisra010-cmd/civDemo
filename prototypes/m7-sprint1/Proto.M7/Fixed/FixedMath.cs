namespace Proto.M7.Fixed;

/// <summary>
/// Exact integer arithmetic primitives for the authoritative prototype (implementation-spec §2.1, O-7a).
/// Every operation is pure integer math (Int128 intermediates where a product can exceed long), so its
/// result is platform-independent by construction. Results that do not fit their return type THROW
/// (checked conversions; the project compiles with CheckForOverflowUnderflow=true) instead of wrapping.
/// </summary>
public static class FixedMath
{
    /// <summary>floor(a*b/c), exact, for c &gt; 0. The product is formed in Int128 so it never overflows.</summary>
    public static long MulDivFloor(long a, long b, long c)
    {
        if (c <= 0) throw new ArgumentOutOfRangeException(nameof(c), "divisor must be positive");
        Int128 p = (Int128)a * b;
        Int128 q = p / c;               // truncates toward zero
        Int128 r = p - q * c;
        if (r < 0) q -= 1;             // negative non-exact quotient: truncation went up, floor goes down
        return (long)q;                // checked: throws OverflowException if out of long range
    }

    /// <summary>ceil(a*b/c), exact, for c &gt; 0.</summary>
    public static long MulDivCeil(long a, long b, long c)
    {
        if (c <= 0) throw new ArgumentOutOfRangeException(nameof(c), "divisor must be positive");
        Int128 p = (Int128)a * b;
        Int128 q = p / c;
        Int128 r = p - q * c;
        if (r > 0) q += 1;             // positive non-exact quotient: truncation went down, ceil goes up
        return (long)q;
    }

    /// <summary>floor(a/b) for any sign combination, b != 0 (C# '/' truncates toward zero).</summary>
    public static long DivFloor(long a, long b)
    {
        if (b == 0) throw new DivideByZeroException();
        long q = a / b;                 // checked: long.MinValue / -1 throws
        long r = a - q * b;
        if (r != 0 && ((r < 0) != (b < 0))) q -= 1;
        return q;
    }

    /// <summary>ceil(a/b) for any sign combination, b != 0.</summary>
    public static long DivCeil(long a, long b)
    {
        if (b == 0) throw new DivideByZeroException();
        long q = a / b;
        long r = a - q * b;
        if (r != 0 && ((r < 0) == (b < 0))) q += 1;
        return q;
    }

    /// <summary>
    /// Floor modulo (result has the sign of the divisor): a - b*floor(a/b), b != 0. Computed from the truncating
    /// remainder so it is exact over the whole long range (b*floor(a/b) may itself leave the long range, e.g.
    /// ModFloor(long.MaxValue, -2) = -1); r and b have opposite signs when corrected, so r + b cannot overflow.
    /// </summary>
    public static long ModFloor(long a, long b)
    {
        if (b == 0) throw new DivideByZeroException();
        if (b == -1) return 0;          // long.MinValue % -1 throws in .NET; every value is a multiple of -1
        long r = a % b;
        if (r != 0 && ((r < 0) != (b < 0))) r += b;
        return r;
    }

    /// <summary>
    /// Exact floor square root of a 64-bit value: the unique r with r*r &lt;= n &lt; (r+1)*(r+1).
    /// Bitwise (digit-by-digit, base 4) method: no division, no floating point, fixed 32 iterations.
    /// </summary>
    public static ulong ISqrt(ulong n)
    {
        ulong x = n;
        ulong res = 0;
        ulong bit = 1UL << 62;
        while (bit > x) bit >>= 2;
        while (bit != 0)
        {
            if (x >= res + bit)
            {
                x -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        return res;
    }

    /// <summary>Exact floor square root of a 128-bit value (same bitwise method; result fits in 64 bits).</summary>
    public static ulong ISqrt(UInt128 n)
    {
        UInt128 x = n;
        UInt128 res = 0;
        UInt128 bit = UInt128.One << 126;
        while (bit > x) bit >>= 2;
        while (bit != 0)
        {
            if (x >= res + bit)
            {
                x -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        return (ulong)res;
    }
}
