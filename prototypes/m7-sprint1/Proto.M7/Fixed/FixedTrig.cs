namespace Proto.M7.Fixed;

/// <summary>
/// Integer-only trigonometry for headings (lane N: naval/air kinematics). Angles are BINARY ANGLE UNITS (BAM): one full
/// turn = <see cref="Turn"/> = 65,536 units, so wrap-around is exact modular arithmetic and every heading is an int.
/// Convention: angle 0 points +X (east), angles grow counter-clockwise toward +Y.
///
/// TABLE. Sin is a quarter-wave table of Quarter + 1 entries in Q30 (value * 2^30), built once at type initialisation by
/// a Taylor series evaluated in Int128 Q60 fixed point (no floating point anywhere): x_r = r * (pi/2) / Quarter with
/// pi/2 in Q60 = 1,811,004,864,519,280,710 (floor of pi/2 * 2^60, a literal constant); terms t_{k+1} = -t_k * x^2 /
/// ((2k+2)(2k+3)) with truncating division, summed until a term is zero; the Q60 sum is rounded half-up to Q30. Endpoints
/// are exact by construction (sin 0 = 0, sin(quarter) = 2^30). Being a pure function of integer constants, the table is
/// bit-identical on every platform. Accuracy (tested): |sin^2 + cos^2 - 2^60| &lt;= 2^32 for every angle.
///
/// ATAN2. <see cref="Atan2"/> returns the table angle NEAREST to the direction of (dx, dy): a binary search for the largest
/// first-quadrant r with sin(r)*|dx| &lt;= cos(r)*|dy| (exact Int128 comparisons; monotone because sin rises and cos falls),
/// then r or r+1 by the smaller exact cross product, then the quadrant is restored by reflection. Inverse property (tested):
/// Atan2(Sin(a)*R, Cos(a)*R) is within 1 unit of a for every a.
/// </summary>
public static class FixedTrig
{
    public const int Turn = 65_536;
    public const int HalfTurn = Turn / 2;
    public const int Quarter = Turn / 4;
    public const int Eighth = Turn / 8;
    public const int OneShift = 30;
    /// <summary>1.0 in Q30.</summary>
    public const long One = 1L << OneShift;

    /// <summary>floor(pi/2 * 2^60).</summary>
    public const long HalfPiQ60 = 1_811_004_864_519_280_710;

    private static readonly long[] QuarterSin = BuildTable();

    private static long[] BuildTable()
    {
        var t = new long[Quarter + 1];
        for (int r = 0; r <= Quarter; r++)
        {
            Int128 x = (Int128)HalfPiQ60 * r / Quarter;                 // Q60, 0 <= x <= pi/2
            Int128 x2 = (x * x) >> 60;                                  // Q60
            Int128 term = x, sum = 0;
            for (int k = 0; term != 0; k++)
            {
                sum += term;
                Int128 next = term * x2 >> 60;                           // magnitude product, sign carried by term
                next /= (2 * k + 2) * (2 * k + 3);
                term = -next;
            }
            long q30 = (long)((sum + (Int128.One << 29)) >> 30);
            t[r] = q30;
        }
        t[0] = 0;
        t[Quarter] = One;
        return t;
    }

    /// <summary>Canonical angle in [0, Turn).</summary>
    public static int Norm(long a) => (int)FixedMath.ModFloor(a, Turn);

    /// <summary>sin(a) in Q30.</summary>
    public static long Sin(long a)
    {
        int n = Norm(a);
        int q = n / Quarter, r = n % Quarter;
        return q switch
        {
            0 => QuarterSin[r],
            1 => QuarterSin[Quarter - r],
            2 => -QuarterSin[r],
            _ => -QuarterSin[Quarter - r],
        };
    }

    /// <summary>cos(a) in Q30.</summary>
    public static long Cos(long a) => Sin(a + Quarter);

    /// <summary>Signed shortest difference to - from, in [-HalfTurn, HalfTurn). Exactly opposite headings give -HalfTurn.</summary>
    public static int Diff(long from, long to) => Norm(to - from + HalfTurn) - HalfTurn;

    /// <summary>Absolute angular separation in [0, HalfTurn].</summary>
    public static int Separation(long a, long b)
    {
        int d = Diff(a, b);
        return d < 0 ? -d : d;
    }

    /// <summary>Nearest table angle of the direction (dx, dy); 0 for the zero vector.</summary>
    public static int Atan2(long dy, long dx)
    {
        if (dx == 0 && dy == 0) return 0;
        Int128 ax = dx < 0 ? -(Int128)dx : dx, ay = dy < 0 ? -(Int128)dy : dy;
        int r;
        if (ax == 0) r = Quarter;
        else
        {
            // Largest r in [0, Quarter] with sin(r)*ax <= cos(r)*ay  (r = 0 always qualifies).
            int lo = 0, hi = Quarter;
            while (lo < hi)
            {
                int mid = lo + (hi - lo + 1) / 2;
                if ((Int128)QuarterSin[mid] * ax <= (Int128)QuarterSin[Quarter - mid] * ay) lo = mid; else hi = mid - 1;
            }
            r = lo;
            if (r < Quarter)
            {
                // Exact cross products: v x u_r >= 0 and u_{r+1} x v >= 0; the smaller one is the nearer table angle.
                Int128 toR = (Int128)QuarterSin[Quarter - r] * ay - (Int128)QuarterSin[r] * ax;
                Int128 toNext = (Int128)QuarterSin[r + 1] * ax - (Int128)QuarterSin[Quarter - r - 1] * ay;
                if (toNext < toR) r++;
            }
        }
        if (dx >= 0 && dy >= 0) return r;
        if (dx < 0 && dy >= 0) return HalfTurn - r;
        if (dx < 0) return HalfTurn + r;
        return Norm(Turn - r);
    }

    /// <summary>v * sin(a) rounded toward zero (sign-symmetric), exact Int128 intermediate.</summary>
    public static long ScaleSin(long v, long a) => ScaleQ30(v, Sin(a));

    /// <summary>v * cos(a) rounded toward zero (sign-symmetric).</summary>
    public static long ScaleCos(long v, long a) => ScaleQ30(v, Cos(a));

    private static long ScaleQ30(long v, long q30)
    {
        Int128 p = (Int128)v * q30;
        Int128 q = p >= 0 ? p >> OneShift : -((-p) >> OneShift);
        return (long)q;
    }

    /// <summary>Degrees (integer) to BAM, nearest (helper for readable rule tables; exact for multiples of 45).</summary>
    public static int FromDegrees(long degrees) => Norm(FixedMath.DivFloor(checked(degrees * Turn * 2 + 360), 720));
}
