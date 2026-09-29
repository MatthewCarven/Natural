using System.Numerics;

namespace Natural.Tests;

/// <summary>
/// The reference answers for ApFloat. A value is an exact rational (BigInteger over
/// BigInteger), and rounding is long division: floor the quotient, then compare the
/// remainder with half the divisor. That shares no code with ApFloat.RoundExact, which
/// rounds by testing bits. The IEEE encoder here likewise works on the rational, with
/// ordinary integer arithmetic for the fields.
/// </summary>
internal static class FloatOracle
{
    /// <summary>A rounded value, ±N × 2^E. N isn't normalised; N = 0 is a zero of the given sign.</summary>
    public readonly record struct Rounded(bool Negative, BigInteger N, long E);

    public static readonly RoundingMode[] Modes = Enum.GetValues<RoundingMode>();

    /// <summary>A finite ApFloat as an exact fraction, the sign on the numerator.</summary>
    public static (BigInteger Num, BigInteger Den) ToRational(ApFloat x)
    {
        BigInteger m = Oracle.ToBig(x.Significand);
        if (x.IsNegative) m = -m;
        long e = x.Exponent;
        return e >= 0 ? (m << (int)e, BigInteger.One) : (m, BigInteger.One << (int)-e);
    }

    /// <summary>
    /// num / den rounded to <paramref name="precision"/> bits. With <paramref name="minExp"/>,
    /// no kept bit may sit below 2^minExp, which is how IEEE's subnormals lose precision.
    /// A zero result (exact, or underflowed) comes back as +0; callers fix the sign.
    /// </summary>
    public static Rounded Round(BigInteger num, BigInteger den, int precision, RoundingMode mode,
                                long minExp = long.MinValue)
    {
        if (den.Sign < 0) { num = -num; den = -den; }
        if (num.IsZero) return new(false, 0, 0);
        bool negative = num.Sign < 0;
        num = BigInteger.Abs(num);

        // k = floor(log2(num / den)). The bit lengths get it to within one.
        long k = (long)(num.GetBitLength() - den.GetBitLength());
        if (CompareWithPowerOfTwo(num, den, k) < 0) k--;
        long ulp = Math.Max(k - precision + 1, minExp);

        // n = floor(num / den / 2^ulp); the remainder says which side of halfway the rest is.
        BigInteger scaledNum = ulp < 0 ? num << (int)-ulp : num;
        BigInteger scaledDen = ulp > 0 ? den << (int)ulp : den;
        BigInteger n = BigInteger.DivRem(scaledNum, scaledDen, out BigInteger rem);
        int vsHalf = (rem * 2).CompareTo(scaledDen);
        bool up = mode switch
        {
            RoundingMode.ToNearestEven => vsHalf > 0 || (vsHalf == 0 && !n.IsEven),
            RoundingMode.TowardZero => false,
            RoundingMode.TowardPositive => !negative && !rem.IsZero,
            RoundingMode.TowardNegative => negative && !rem.IsZero,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        return new(negative, up ? n + 1 : n, ulp);
    }

    /// <summary>Compares num / den with 2^k.</summary>
    private static int CompareWithPowerOfTwo(BigInteger num, BigInteger den, long k) =>
        k >= 0 ? num.CompareTo(den << (int)k) : (num << (int)-k).CompareTo(den);

    // ------------------------------------------------------------------
    // IEEE formats: w exponent bits, precision p, w + p bits in all
    // ------------------------------------------------------------------

    public static ulong CanonicalNaN(int w, int p) => (((1UL << w) - 1) << (p - 1)) | (1UL << (p - 2));

    /// <summary>The bit pattern for x in the format, rounded once from the exact value.</summary>
    public static ulong Encode(ApFloat x, int w, int p, RoundingMode mode)
    {
        int f = p - 1;
        ulong maxField = (1UL << w) - 1;
        ulong sign = x.IsNegative ? 1UL << (w + f) : 0;
        if (x.IsNaN) return CanonicalNaN(w, p);
        if (x.IsInfinity) return sign | (maxField << f);
        if (x.IsZero) return sign;

        long bias = (long)(maxField / 2), emin = 1 - bias, emax = bias;
        var (num, den) = ToRational(x);
        Rounded r = Round(num, den, p, mode, emin - f);
        if (r.N.IsZero) return sign;

        long top = (long)r.N.GetBitLength() - 1 + r.E;
        if (top > emax)
        {
            bool infinite = mode == RoundingMode.ToNearestEven
                || (mode == RoundingMode.TowardPositive && !x.IsNegative)
                || (mode == RoundingMode.TowardNegative && x.IsNegative);
            return infinite ? sign | (maxField << f) : sign | ((maxField - 1) << f) | ((1UL << f) - 1);
        }
        if (top < emin) return sign | (ulong)r.N;          // subnormal: r.E is emin - f already

        // Normal: N × 2^E as a p-bit integer times 2^(top - f); the field stores it minus 2^f.
        long shift = r.E - (top - f);
        BigInteger m = shift >= 0 ? r.N << (int)shift : r.N >> (int)-shift;
        return sign | ((ulong)(top + bias) << f) | ((ulong)m - (1UL << f));
    }

    /// <summary>What a bit pattern means, in <see cref="Describe(ApFloat)"/>'s notation.</summary>
    public static string Decode(ulong bits, int w, int p)
    {
        int f = p - 1;
        ulong maxField = (1UL << w) - 1;
        long bias = (long)(maxField / 2);
        bool negative = ((bits >> (w + f)) & 1) == 1;
        ulong field = (bits >> f) & maxField;
        ulong fraction = bits & ((1UL << f) - 1);
        if (field == maxField) return fraction != 0 ? "NaN" : negative ? "-inf" : "+inf";
        // A subnormal is 0.fraction at the smallest normal exponent: the same place values as field 1.
        BigInteger n = field == 0 ? fraction : fraction + (BigInteger.One << f);
        long e = Math.Max((long)field, 1) - bias - f;
        return Describe(new Rounded(negative, n, e));
    }

    // ------------------------------------------------------------------
    // Comparing, and describing failures
    // ------------------------------------------------------------------

    public static string Describe(Rounded r)
    {
        string sign = r.Negative ? "-" : "+";
        if (r.N.IsZero) return sign + "0";
        long zeros = (long)BigInteger.TrailingZeroCount(r.N);
        return $"{sign}0x{r.N >> (int)zeros:X}p{r.E + zeros}";
    }

    /// <summary>Sign, odd significand in hex, exponent: "-0x3p-2" is -0.75. Built without ApFloat's own text code.</summary>
    public static string Describe(ApFloat x)
    {
        if (x.IsNaN) return "NaN";
        string sign = x.IsNegative ? "-" : "+";
        if (x.IsInfinity) return sign + "inf";
        if (x.IsZero) return sign + "0";
        return $"{sign}0x{Oracle.ToBig(x.Significand):X}p{x.Exponent}";
    }

    public static void AssertRounded(Rounded expected, ApFloat actual, int precision, string context)
    {
        string want = Describe(expected), got = Describe(actual);
        Assert.True(want == got && actual.Precision == precision,
            $"{context}\n  expected {want} at {precision} bits\n  actual   {got} at {actual.Precision} bits");
    }

    public static void AssertSame(ApFloat expected, ApFloat actual, string context = "")
    {
        string want = Describe(expected), got = Describe(actual);
        Assert.True(want == got, $"{context}\n  expected {want}\n  actual   {got}");
    }

    // ------------------------------------------------------------------
    // Random operands
    // ------------------------------------------------------------------

    /// <summary>Enough precision to hold any value the random generators make, exactly.</summary>
    public const int Exact = 4096;

    /// <summary>A limb-biased significand (possibly zero, either sign, -0 included) at a random exponent, held exactly.</summary>
    public static ApFloat RandomFinite(Random rng, int maxLimbs, int maxExponent)
    {
        BigInteger m = Oracle.RandomBig(rng, maxLimbs);
        var x = new ApFloat(Oracle.ToAp(m), rng.Next(-maxExponent, maxExponent + 1), Exact);
        return m.IsZero && rng.Next(2) == 0 ? -x : x;
    }

    /// <summary>
    /// A random bit pattern in the format. Biased the way Oracle.RandomBig is: exponent
    /// fields at the edges (0, 1, the largest finite, all ones) and fractions that are all
    /// zeros, all ones, one bit, or long runs, because that's where encoders and rounding
    /// go wrong. <paramref name="hotTops"/>, if given, are leading-bit places to crowd round.
    /// </summary>
    public static ulong RandomIeeeBits(Random rng, int w, int p, params long[] hotTops)
    {
        int f = p - 1;
        ulong maxField = (1UL << w) - 1;
        long bias = (long)(maxField / 2);
        ulong field = rng.Next(12) switch
        {
            0 => 0,                                               // zero or subnormal
            1 => 1,                                               // the smallest normal binade
            2 => maxField - 1,                                    // the largest finite binade
            3 => maxField,                                        // infinity or NaN
            4 => (ulong)bias,                                     // [1, 2)
            5 or 6 when hotTops.Length > 0 =>
                (ulong)Math.Clamp(hotTops[rng.Next(hotTops.Length)] + rng.Next(-3, 4) + bias, 0, (long)maxField - 1),
            _ => 1 + (ulong)rng.NextInt64((long)maxField - 1),    // any normal
        };
        ulong sign = (ulong)rng.Next(2) << (w + f);
        return sign | (field << f) | RandomField(rng, f);
    }

    /// <summary>A random <paramref name="bits"/>-bit field, biased toward runs and edges.</summary>
    public static ulong RandomField(Random rng, int bits)
    {
        ulong mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
        ulong v = rng.Next(6) switch
        {
            0 => 0,
            1 => mask,
            2 => 1UL << rng.Next(bits),
            3 => mask ^ (1UL << rng.Next(bits)),
            4 => Runs(rng, bits),
            _ => (ulong)rng.NextInt64(long.MinValue, long.MaxValue),
        };
        return v & mask;
    }

    private static ulong Runs(Random rng, int bits)
    {
        ulong v = 0;
        for (int at = 0; at < bits;)
        {
            int length = rng.Next(1, 16);
            if (rng.Next(2) == 0) v |= (length >= 64 ? ulong.MaxValue : (1UL << length) - 1) << at;
            at += length;
        }
        return v;
    }

    /// <summary>Replaces the biased exponent field of a bit pattern.</summary>
    public static ulong WithField(ulong bits, int w, int p, ulong field)
    {
        int f = p - 1;
        ulong maxField = (1UL << w) - 1;
        return (bits & ~(maxField << f)) | ((field & maxField) << f);
    }
}
