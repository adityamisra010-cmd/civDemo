using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// A battlefield position or displacement in integer MILLIMETRES (spec §3, O-7/O-7a: continuous coordinates as fixed-point
/// integers, 1 unit = 1 mm). Documented range: every POSITION handed to a spatial query satisfies |X|, |Y| &lt;= MaxCoordMm
/// (10^9 mm = 1,000 km). Consequences that the code relies on (and the tests pin at the corners of the range):
/// a displacement component is at most 2*10^9 (fits long); a squared length is at most 8*10^18 (fits long, formed in Int128);
/// products of two squared lengths are at most 6.4*10^37 (fits Int128, max ~1.7*10^38). Queries throw on out-of-range input
/// instead of silently overflowing.
/// </summary>
public readonly record struct Vec2L(long X, long Y)
{
    /// <summary>Documented coordinate range of the prototype: |X|,|Y| &lt;= 10^9 mm.</summary>
    public const long MaxCoordMm = 1_000_000_000L;

    public static readonly Vec2L Zero = new(0, 0);

    public static Vec2L operator +(Vec2L a, Vec2L b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2L operator -(Vec2L a, Vec2L b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Exact dot product (Int128).</summary>
    public static Int128 Dot(Vec2L a, Vec2L b) => (Int128)a.X * b.X + (Int128)a.Y * b.Y;

    /// <summary>Exact z-component of the 2-D cross product (Int128).</summary>
    public static Int128 Cross(Vec2L a, Vec2L b) => (Int128)a.X * b.Y - (Int128)a.Y * b.X;

    /// <summary>Exact squared length (Int128; never negative).</summary>
    public Int128 LengthSq => Dot(this, this);

    /// <summary>floor(|v|) in mm: exact integer square root of the Int128 squared length.</summary>
    public long Length => (long)FixedMath.ISqrt((UInt128)LengthSq);

    /// <summary>floor(|a - b|) in mm.</summary>
    public static long Distance(Vec2L a, Vec2L b) => (b - a).Length;

    /// <summary>Exact squared distance.</summary>
    public static Int128 DistanceSq(Vec2L a, Vec2L b) => (b - a).LengthSq;

    public bool InDocumentedRange => X >= -MaxCoordMm && X <= MaxCoordMm && Y >= -MaxCoordMm && Y <= MaxCoordMm;

    /// <summary>Throws if the position is outside the documented range (call at every query entry point).</summary>
    public static void RequireInRange(Vec2L p, string name)
    {
        if (!p.InDocumentedRange) throw new ArgumentOutOfRangeException(name, "position outside the documented range |coord| <= 10^9 mm");
    }
}

/// <summary>Integer cell coordinates (may be negative / off-grid: CellOf is defined on the whole plane).</summary>
public readonly record struct CellCoord(long X, long Y);

/// <summary>Small exact helpers used by the spatial layer.</summary>
public static class SpatialMath
{
    /// <summary>ceil(sqrt(n)) for n &gt;= 0, exact.</summary>
    public static ulong ISqrtCeil(UInt128 n)
    {
        ulong r = FixedMath.ISqrt(n);
        return (UInt128)r * r == n ? r : r + 1;
    }
}
