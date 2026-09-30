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

    // Patterns are BigIntegers, so the same code serves binary16 and binary1024.

    public static BigInteger CanonicalNaN(int w, int p) =>
        (((BigInteger.One << w) - 1) << (p - 1)) | (BigInteger.One << (p - 2));

    /// <summary>The bit pattern for x in the format, rounded once from the exact value.</summary>
    public static BigInteger Encode(ApFloat x, int w, int p, RoundingMode mode)
    {
        int f = p - 1;
        BigInteger maxField = (BigInteger.One << w) - 1;
        BigInteger sign = x.IsNegative ? BigInteger.One << (w + f) : BigInteger.Zero;
        if (x.IsNaN) return CanonicalNaN(w, p);
        if (x.IsInfinity) return sign | (maxField << f);
        if (x.IsZero) return sign;

        long bias = (1L << (w - 1)) - 1, emin = 1 - bias;

        // Rounding commutes with scaling by 2^e, so round the significand alone, with the
        // subnormal floor moved to match. The numbers stay small however big the exponent
        // (binary1024's range reaches 2^67108864).
        BigInteger m = Oracle.ToBig(x.Significand);
        long e = x.Exponent;
        Rounded r = Round(x.IsNegative ? -m : m, BigInteger.One, p, mode, emin - f - e);
        return Place(x.IsNegative, r.N, r.E + e, w, p, mode);
    }

    /// <summary>
    /// The pattern for the exact value num / den (sign on num) rounded once into the format:
    /// what arithmetic in the format must produce. An exact zero takes <paramref name="negativeZero"/>.
    /// </summary>
    public static BigInteger EncodeRational(BigInteger num, BigInteger den, bool negativeZero, int w, int p, RoundingMode mode)
    {
        if (num.IsZero) return negativeZero ? BigInteger.One << (w + p - 1) : BigInteger.Zero;
        long bias = (1L << (w - 1)) - 1;
        Rounded r = Round(num, den, p, mode, 1 - bias - (p - 1));
        return Place(num.Sign < 0, r.N, r.E, w, p, mode);
    }

    /// <summary>Lays out a rounded ±n × 2^exponent: underflowed zero, overflow by mode, subnormal or normal.</summary>
    private static BigInteger Place(bool negative, BigInteger n, long exponent, int w, int p, RoundingMode mode)
    {
        int f = p - 1;
        BigInteger maxField = (BigInteger.One << w) - 1;
        BigInteger sign = negative ? BigInteger.One << (w + f) : BigInteger.Zero;
        long bias = (1L << (w - 1)) - 1, emin = 1 - bias, emax = bias;
        if (n.IsZero) return sign;

        long top = (long)n.GetBitLength() - 1 + exponent;
        if (top > emax)
        {
            bool infinite = mode == RoundingMode.ToNearestEven
                || (mode == RoundingMode.TowardPositive && !negative)
                || (mode == RoundingMode.TowardNegative && negative);
            return infinite ? sign | (maxField << f) : sign | ((maxField - 1) << f) | ((BigInteger.One << f) - 1);
        }
        if (top < emin) return sign | n;          // subnormal: the exponent is emin - f already

        // Normal: n × 2^exponent as a p-bit integer times 2^(top - f); the field stores it minus 2^f.
        long shift = exponent - (top - f);
        BigInteger significand = shift >= 0 ? n << (int)shift : n >> (int)-shift;
        return sign | ((BigInteger)(top + bias) << f) | (significand - (BigInteger.One << f));
    }

    /// <summary>What a bit pattern means, in <see cref="Describe(ApFloat)"/>'s notation.</summary>
    public static string Decode(BigInteger bits, int w, int p)
    {
        int f = p - 1;
        long maxField = (1L << w) - 1;
        long bias = maxField / 2;
        bool negative = !((bits >> (w + f)) & 1).IsZero;
        long field = (long)((bits >> f) & maxField);
        BigInteger fraction = bits & ((BigInteger.One << f) - 1);
        if (field == maxField) return !fraction.IsZero ? "NaN" : negative ? "-inf" : "+inf";
        // A subnormal is 0.fraction at the smallest normal exponent: the same place values as field 1.
        BigInteger n = field == 0 ? fraction : fraction + (BigInteger.One << f);
        long e = Math.Max(field, 1) - bias - f;
        return Describe(new Rounded(negative, n, e));
    }

    /// <summary>A pattern as <paramref name="length"/> little-endian bytes.</summary>
    public static byte[] ToBytes(BigInteger pattern, int length)
    {
        byte[] raw = pattern.ToByteArray(isUnsigned: true);
        var bytes = new byte[length];
        raw.AsSpan(0, Math.Min(raw.Length, length)).CopyTo(bytes);
        return bytes;
    }

    public static BigInteger FromBytes(byte[] littleEndian) => new(littleEndian, isUnsigned: true);

    // ------------------------------------------------------------------
    // Decimal text: the same long division, in base ten
    // ------------------------------------------------------------------

    /// <summary>
    /// |x| × 10^t rounded to an integer (directed modes look at x's sign). Also says whether
    /// it was an exact tie, which is where IEEE (to even) and .NET's own formatting (away
    /// from zero) part company.
    /// </summary>
    public static (BigInteger Q, bool Tie) ScaledRound(ApFloat x, long t, RoundingMode mode)
    {
        var (num, den) = ToRational(x);
        num = BigInteger.Abs(num);
        if (t >= 0) num *= BigInteger.Pow(10, (int)t);
        else den *= BigInteger.Pow(10, (int)-t);
        BigInteger q = BigInteger.DivRem(num, den, out BigInteger rem);
        int vsHalf = (rem * 2).CompareTo(den);
        bool up = mode switch
        {
            RoundingMode.ToNearestEven => vsHalf > 0 || (vsHalf == 0 && !q.IsEven),
            RoundingMode.TowardZero => false,
            RoundingMode.TowardPositive => !x.IsNegative && !rem.IsZero,
            RoundingMode.TowardNegative => x.IsNegative && !rem.IsZero,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        return (up ? q + 1 : q, vsHalf == 0);
    }

    /// <summary>floor(log10 |x|) for a finite non-zero x.</summary>
    public static long DecimalExponent(ApFloat x)
    {
        var (num, den) = ToRational(x);
        num = BigInteger.Abs(num);
        long e = (long)Math.Floor(BigInteger.Log10(num) - BigInteger.Log10(den));
        while (ComparePow10(num, den, e) < 0) e--;
        while (ComparePow10(num, den, e + 1) >= 0) e++;
        return e;
    }

    private static int ComparePow10(BigInteger num, BigInteger den, long e) =>
        e >= 0 ? num.CompareTo(den * BigInteger.Pow(10, (int)e)) : (num * BigInteger.Pow(10, (int)-e)).CompareTo(den);

    /// <summary>What "E&lt;n&gt;" should print (invariant culture), and whether rounding met a tie.</summary>
    public static (string Text, bool Tie) Scientific(ApFloat x, int n, RoundingMode mode, char e = 'E')
    {
        string sign = x.IsNegative ? "-" : "";
        string digits;
        long exponent = 0;
        bool tie = false;
        if (x.IsZero) digits = new string('0', n + 1);
        else
        {
            exponent = DecimalExponent(x);
            (BigInteger q, tie) = ScaledRound(x, n - exponent, mode);
            if (q == BigInteger.Pow(10, n + 1))
            {
                q /= 10;
                exponent++;
            }
            digits = q.ToString();
        }
        string body = n > 0 ? $"{digits[0]}.{digits[1..]}" : digits;
        return ($"{sign}{body}{e}{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent):000}", tie);
    }

    /// <summary>What "F&lt;n&gt;" should print (invariant culture), and whether rounding met a tie.</summary>
    public static (string Text, bool Tie) Fixed(ApFloat x, int n, RoundingMode mode)
    {
        string sign = x.IsNegative ? "-" : "";
        var (q, tie) = x.IsZero ? (BigInteger.Zero, false) : ScaledRound(x, n, mode);
        string digits = q.ToString().PadLeft(n + 1, '0');
        string body = n > 0 ? $"{digits[..^n]}.{digits[^n..]}" : digits;
        return (sign + body, tie);
    }

    /// <summary>
    /// Any decimal text reduced to its significant digits and the power of ten of the first:
    /// "0.00120" and "1.2E-003" are both "12e-3". Zero is "0".
    /// </summary>
    public static string Canonical(string text)
    {
        text = text.TrimStart('-', '+');
        int e = text.IndexOfAny(['E', 'e']);
        long exponent = e < 0 ? 0 : long.Parse(text[(e + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        string mantissa = e < 0 ? text : text[..e];
        int point = mantissa.IndexOf('.');
        string whole = point < 0 ? mantissa : mantissa[..point];
        string digits = (whole + (point < 0 ? "" : mantissa[(point + 1)..])).TrimStart('0');
        if (digits.Length == 0) return "0";
        // The first significant digit sits (whole digits after the leading zeros) places up.
        long lead = exponent + whole.TrimStart('0').Length - 1;
        if (whole.TrimStart('0').Length == 0)
            lead = exponent - (mantissa[(point + 1)..].Length - mantissa[(point + 1)..].TrimStart('0').Length) - 1;
        return $"{digits.TrimEnd('0')}e{lead}";
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
        ulong field = RandomExponentField(rng, w, hotTops);
        ulong sign = (ulong)rng.Next(2) << (w + f);
        return sign | (field << f) | RandomField(rng, f);
    }

    /// <summary>The same for formats of any width.</summary>
    public static BigInteger RandomIeeePattern(Random rng, int w, int p, params long[] hotTops)
    {
        int f = p - 1;
        ulong field = RandomExponentField(rng, w, hotTops);
        BigInteger sign = (BigInteger)rng.Next(2) << (w + f);
        return sign | ((BigInteger)field << f) | RandomWideField(rng, f);
    }

    private static ulong RandomExponentField(Random rng, int w, long[] hotTops)
    {
        ulong maxField = (1UL << w) - 1;
        long bias = (long)(maxField / 2);
        return rng.Next(12) switch
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
    }

    /// <summary>A random field of any width, biased the same way as <see cref="RandomField"/>.</summary>
    public static BigInteger RandomWideField(Random rng, int bits)
    {
        BigInteger mask = (BigInteger.One << bits) - 1;
        switch (rng.Next(6))
        {
            case 0: return 0;
            case 1: return mask;
            case 2: return BigInteger.One << rng.Next(bits);
            case 3: return mask ^ (BigInteger.One << rng.Next(bits));
            case 4:
                BigInteger v = 0;
                for (int at = 0; at < bits;)
                {
                    int length = rng.Next(1, 40);
                    if (rng.Next(2) == 0) v |= ((BigInteger.One << length) - 1) << at;
                    at += length;
                }
                return v & mask;
            default:
                var bytes = new byte[(bits >> 3) + 1];
                rng.NextBytes(bytes);
                return new BigInteger(bytes, isUnsigned: true) & mask;
        }
    }

    /// <summary>
    /// Two random operands. Some are unrelated; some are close (cancellation in - , quotients
    /// near 1); some have exponents chosen so the product or quotient lands among the
    /// subnormals or just past the largest finite value.
    /// </summary>
    public static (ulong, ulong) RandomPair(Random rng, int w, int p)
    {
        ulong a = RandomIeeeBits(rng, w, p), b = RandomIeeeBits(rng, w, p);
        ulong maxField = (1UL << w) - 1;
        long bias = (long)(maxField / 2), emin = 1 - bias, emax = bias;
        long fieldA = (long)((a >> (p - 1)) & maxField);
        switch (rng.Next(4))
        {
            case 0:
                // Close: a with some low bits changed, either sign.
                b = a ^ RandomField(rng, rng.Next(1, p)) ^ ((ulong)rng.Next(2) << (w + p - 1));
                break;
            case 1:
                // Product or quotient near the bottom or the top of the range.
                long target = rng.Next(2) == 0 ? emin - rng.Next(0, p + 3) : emax + rng.Next(-1, 2);
                long ea = Math.Max(fieldA, 1) - bias;
                long eb = rng.Next(2) == 0 ? target - ea : ea - target;
                b = WithField(b, w, p, (ulong)Math.Clamp(eb + bias, 1, (long)maxField - 1));
                break;
        }
        return rng.Next(2) == 0 ? (a, b) : (b, a);
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
