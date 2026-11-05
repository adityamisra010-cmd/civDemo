namespace Proto.M7.Fixed;

/// <summary>
/// Q32: a non-negative ratio/probability with 32 fractional bits, stored as <c>long Raw</c>; value = Raw / 2^32.
/// Representation choice (spec §2.1 allowed either): a LONG raw with 32 fractional bits rather than a uint
/// fraction, so that exactly 1.0 (Raw = 2^32) is representable (a certain event must be certain) and ratios
/// above 1 (multipliers) fit the same type. Probabilities are validated to [0, One] where required.
/// All arithmetic rounds toward negative infinity (floor) and is exact integer math.
/// </summary>
public readonly record struct Q32(long Raw) : IComparable<Q32>
{
    public const int FractionBits = 32;
    public const long OneRaw = 1L << FractionBits;

    public static readonly Q32 Zero = new(0);
    public static readonly Q32 One = new(OneRaw);

    /// <summary>floor(num/den * 2^32), den &gt; 0. Exact.</summary>
    public static Q32 FromRatio(long num, long den) => new(FixedMath.MulDivFloor(num, OneRaw, den));

    /// <summary>A probability in [0, 1]; throws otherwise.</summary>
    public static Q32 Probability(long num, long den)
    {
        if (num < 0 || den <= 0 || num > den) throw new ArgumentOutOfRangeException(nameof(num), "probability must be in [0,1]");
        return FromRatio(num, den);
    }

    public bool IsProbability => Raw >= 0 && Raw <= OneRaw;

    /// <summary>Product of two Q32 values, floor.</summary>
    public Q32 Mul(Q32 other) => new(FixedMath.MulDivFloor(Raw, other.Raw, OneRaw));

    /// <summary>floor(x * this) for an integer quantity x.</summary>
    public long Scale(long x) => FixedMath.MulDivFloor(x, Raw, OneRaw);

    /// <summary>ceil(x * this) for an integer quantity x.</summary>
    public long ScaleCeil(long x) => FixedMath.MulDivCeil(x, Raw, OneRaw);

    /// <summary>1 - p for a probability p.</summary>
    public Q32 Complement()
    {
        if (!IsProbability) throw new InvalidOperationException("complement of a non-probability");
        return new(OneRaw - Raw);
    }

    public int CompareTo(Q32 other) => Raw.CompareTo(other.Raw);
    public static bool operator <(Q32 a, Q32 b) => a.Raw < b.Raw;
    public static bool operator >(Q32 a, Q32 b) => a.Raw > b.Raw;
    public static bool operator <=(Q32 a, Q32 b) => a.Raw <= b.Raw;
    public static bool operator >=(Q32 a, Q32 b) => a.Raw >= b.Raw;
}
