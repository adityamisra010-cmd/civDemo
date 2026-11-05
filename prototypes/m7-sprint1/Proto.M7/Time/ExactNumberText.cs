namespace Proto.M7.Time;

/// <summary>
/// Exact parsing of a JSON number token's TEXT (e.g. "0.5", "-1500", "10") into an exact rational
/// Mantissa / 10^Scale, with NO binary floating point at any step (spec §2.2: dtYears must be read
/// without the IEEE type). Exponent notation is rejected (not present in the shipped file; rejecting is
/// safer than guessing). Culture-independent: only ASCII '0'-'9', '-', '.' are accepted.
/// </summary>
public readonly record struct ExactNumber(long Mantissa, int Scale)
{
    public long Pow10Scale
    {
        get
        {
            long p = 1;
            for (int i = 0; i < Scale; i++) p *= 10; // checked: throws on overflow
            return p;
        }
    }

    /// <summary>this * factor as an exact integer; throws if not integral.</summary>
    public long TimesExactInteger(long factor)
    {
        long den = Pow10Scale;
        Int128 num = (Int128)Mantissa * factor;
        if (num % den != 0)
            throw new FormatException($"value {Mantissa}e-{Scale} x {factor} is not an exact integer");
        return (long)(num / den);
    }

    public long AsExactInteger() => TimesExactInteger(1);
}

public static class ExactNumberText
{
    public static ExactNumber Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length == 0) throw new FormatException("empty number");
        int i = 0;
        bool neg = false;
        if (utf8[0] == (byte)'-') { neg = true; i = 1; }
        if (i >= utf8.Length) throw new FormatException("sign without digits");
        long mant = 0;
        int scale = 0;
        bool seenPoint = false;
        int digits = 0;
        for (; i < utf8.Length; i++)
        {
            byte ch = utf8[i];
            if (ch == (byte)'.')
            {
                if (seenPoint) throw new FormatException("two points");
                seenPoint = true;
                continue;
            }
            if (ch < (byte)'0' || ch > (byte)'9')
                throw new FormatException($"unsupported character '{(char)ch}' in number (exponent notation is rejected)");
            mant = mant * 10 + (ch - (byte)'0'); // checked: throws on overflow
            digits++;
            if (seenPoint) scale++;
        }
        if (digits == 0) throw new FormatException("no digits");
        // Normalise trailing fractional zeros ("0.50" == "0.5").
        while (scale > 0 && mant % 10 == 0) { mant /= 10; scale--; }
        return new ExactNumber(neg ? -mant : mant, scale);
    }

    public static ExactNumber Parse(string ascii) => Parse(System.Text.Encoding.ASCII.GetBytes(ascii));
}
